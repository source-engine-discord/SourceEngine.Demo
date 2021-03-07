using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using CommandLine;
using CommandLine.Text;

using SourceEngine.Demo.Stats.Models;

namespace SourceEngine.Demo.Stats.App
{
    internal class Options
    {
        private string testDateOverride;
        private uint? hostageRescueZoneCountOverride;

        [Option(
            "folders",
            Separator = ' ',
            HelpText = "Space-delimited list of directories in which to search for demos to parse."
        )]
        public IEnumerable<string> Folders { get; set; }

        [Option("demos", Separator = ' ', HelpText = "Space-delimited list of paths to individual demos to parse.")]
        public IEnumerable<string> Demos { get; set; }

        [Option("output", Default = "parsed", HelpText = "Path to the output directory.")]
        public string Output { get; set; }

        [Option(
            "gamemodeoverride",
            Default = TestType.Unknown,
            HelpText = "Assume the demo is for this game mode rather than attempting to infer the game mode."
        )]
        public GameMode GameModeOverride { get; set; }

        [Option(
            "testtype",
            Default = TestType.Unknown,
            HelpText = "The playtest type of the recorded match. "
                + "If unset, attempt to parse it from the file name instead assuming the format date_mapname_testtype."
                + "Only relevant for defuse and hostage game modes."
        )]
        public TestType TestType { get; set; }

        [Option(
            "testdateoverride",
            Required = false,
            HelpText = "Recording date of the match in dd/MM/yyyy format. "
                + "If unset, attempt to parse from the file name instead assuming the format date_mapname_testtype."
        )]
        public string TestDateOverride
        {
            get => testDateOverride;
            set
            {
                if (!DateTime.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, 0, out _))
                    throw new FormatException("Date must be in the format dd/MM/yyyy.");

                testDateOverride = value;
            }
        }

        [Option(
            "hostagerescuezonecountoverride",
            HelpText ="Number of hostage rescue zones in the map. "
                + "If unset, a total of 1 is assumed for the hostage game mode and 2 for Danger Zone. "
                + "Valid values: 0-2"
        )]
        public uint? HostageRescueZoneCountOverride
        {
            get => hostageRescueZoneCountOverride;
            set
            {
                if (value is { } count && count > 2)
                    throw new Exception("Value must be between 0 and 2, inclusive.");

                hostageRescueZoneCountOverride = value;
            }
        }

        [Option("recursive", Default = false, HelpText = "Recursively search for demos.")]
        public bool Recursive { get; set; }

        [Option("clear", Default = false, HelpText = "Clear the data folder.")]
        public bool Clear { get; set; }

        [Option("nochickens", Default = false, HelpText = "Disable counting of chicken death stats.")]
        public bool NoChickens { get; set; }

        [Option("noplayerpositions", Default = false, HelpText = "Disable parsing of player positions.")]
        public bool NoPlayerOptions { get; set; }

        [Option("samefilename", Default = false, HelpText = "Use the demo's filename as the output filename.")]
        public bool SameFileName { get; set; }

        [Option(
            "samefolderstructure",
            Default = false,
            HelpText = "Use the demo's folder structure inside the root folder for the output JSON file."
        )]
        public bool SameFolderStructure { get; set; }

        [Option(
            "lowoutputmode",
            Default = false,
            HelpText = "Don't output the progress bar and 'round completed' messages to the console."
        )]
        public bool LowOutputMode { get; set; }
    }

    internal static class Program
    {
        private static void Main(string[] args)
        {
            var parser = new CommandLine.Parser(
                with =>
                {
                    with.CaseInsensitiveEnumValues = true;
                    with.HelpWriter = null;
                }
            );
            ParserResult<Options> parserResult = parser.ParseArguments<Options>(args);

            parserResult
                .WithParsed(ProcessOptions)
                .WithNotParsed(errs => DisplayHelp(parserResult, errs));
        }

        private static void DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
        {
            var helpText = HelpText.AutoBuild(result, h =>
            {
                h.AddEnumValuesToHelpText = true;
                return h;
            });

            if (errs.Any())
                Console.Error.WriteLine(helpText);
            else
                Console.WriteLine(helpText);
        }

        private static void ProcessOptions(Options opts)
        {
            if ((opts.GameModeOverride is GameMode.Defuse || opts.GameModeOverride is GameMode.Hostage)
                && opts.TestType is TestType.Unknown)
            {
                Debug.Error("A test type must be given when the game mode is defuse or hostage.");
                return;
            }

            // Ensure all folders to get demos from are created to avoid exceptions.
            foreach (string folder in opts.Folders)
            {
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
            }

            // Clear by recreating folder.
            if (opts.Clear && Directory.Exists(opts.Output))
            {
                Directory.Delete(opts.Output, true);
                Directory.CreateDirectory(opts.Output);
            }
            else if (!Directory.Exists(opts.Output))
            {
                Directory.CreateDirectory(opts.Output);
            }

            Debug.Info("Starting processing of demos.\n");
            DateTime startTime = DateTime.Now;

            uint total = 0;
            uint passCount = 0;

            Console.CursorVisible = false;

            //Process all the found demos
            foreach (DemoInformation demoInfo in GetDemoInfo(opts))
            {
                Console.WriteLine($"Parsing demo {demoInfo.DemoName}");

                var matchData = new MatchData(
                    demoInfo,
                    !opts.NoChickens,
                    !opts.NoPlayerOptions,
                    opts.HostageRescueZoneCountOverride,
                    opts.LowOutputMode
                );

                if (matchData.passed)
                {
                    matchData.CreateFiles(
                        opts.Output,
                        opts.Folders.ToList(),
                        opts.SameFileName,
                        opts.SameFolderStructure
                    );

                    passCount++;
                    Console.WriteLine($"Finished parsing demo {demoInfo.DemoName}.\n");
                }
                else
                {
                    Console.WriteLine($"Failed parsing demo {demoInfo.DemoName}.\n");
                }

                total++;
            }

            Console.CursorVisible = true;

            Debug.Blue("========== PROCESSING COMPLETE =========\n");
            DateTime end = DateTime.Now;

            Debug.White("Processing took {0} minutes\n", (end - startTime).TotalMinutes);
            Debug.White("Passed: {0}\n", passCount);
            Debug.White("Failed: {0}\n", total - passCount);
        }

        private static IEnumerable<DemoInformation> GetDemoInfo(Options opts)
        {
            foreach (string folder in opts.Folders)
            {
                string[] subDemos = Directory.GetFiles(
                    Path.GetFullPath(folder),
                    "*.dem",
                    opts.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly
                );

                foreach (string demo in subDemos)
                    yield return new DemoInformation(demo, opts.GameModeOverride, opts.TestType, opts.TestDateOverride);
            }

            foreach (string demo in opts.Demos)
                yield return new DemoInformation(demo, opts.GameModeOverride, opts.TestType, opts.TestDateOverride);
        }
    }
}
