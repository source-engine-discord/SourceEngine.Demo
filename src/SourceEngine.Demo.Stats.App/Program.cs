using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using CommandLine;
using CommandLine.Text;

using ShellProgressBar;

using SourceEngine.Demo.Parser;
using SourceEngine.Demo.Stats.Models;

namespace SourceEngine.Demo.Stats.App
{
    internal class Options
    {
        private string testDateOverride;
        private uint hostageRescueZoneCountOverride;

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
                + "If unset, attempt to parse it from the file name instead assuming the format date_mapname_testtype. "
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
            Default = 4u,
            HelpText = "Number of hostage rescue zones in the map. Valid values: 0-4"
        )]
        public uint HostageRescueZoneCountOverride
        {
            get => hostageRescueZoneCountOverride;
            set
            {
                if (value > 4)
                    throw new Exception("Value must be between 0 and 4, inclusive.");

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
                .WithParsed(ParseStats)
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
        }

        private static void ParseStats(Options opts)
        {
            uint passCount = 0;
            uint failCount = 0;

            List<DemoInformation> info = GetDemoInfo(opts);
            using var pBar = new ProgressBar(info.Count, "Processing demos");
            var barOptions = new ProgressBarOptions { ForegroundColor = ConsoleColor.Blue };

            // Process all the found demos.
            foreach (DemoInformation demoInfo in info)
            {
                using ChildProgressBar childBar = pBar.Spawn(int.MaxValue, demoInfo.DemoName, barOptions);

                try
                {
                    ParseSingle(opts, demoInfo, childBar);

                    childBar.Message = $"{demoInfo.DemoName}: Finished";
                    childBar.AsProgress<float>().Report(1); // Force to 100%.

                    passCount++;
                    pBar.ForegroundColor = failCount == 0 ? ConsoleColor.Green : ConsoleColor.Yellow;
                }
                catch (Exception e)
                {
                    childBar.ForegroundColor = ConsoleColor.Red;
                    childBar.Message = $"{demoInfo.DemoName}: Failed";
                    childBar.WriteErrorLine(e.ToString());

                    failCount++;
                    pBar.ForegroundColor = passCount == 0 ? ConsoleColor.Red : ConsoleColor.Yellow;
                }

                pBar.Tick();
            }

            pBar.Message = $"Finished with {passCount} passes and {failCount} fails";
        }

        private static void ParseSingle(Options opts, DemoInformation demoInfo, ChildProgressBar pBar)
        {
            // Create the demo and stats parsers.
            using FileStream file = File.OpenRead(demoInfo.DemoName);
            using var parser = new DemoParser(
                file,
                !opts.NoChickens,
                !opts.NoPlayerOptions,
                opts.HostageRescueZoneCountOverride
            );
            var collector = new Collector(parser, demoInfo);

            // Set up events to report progress.
            IProgress<float> progress = pBar.AsProgress<float>();
            parser.TickDone += (_, _) => progress.Report(parser.ParsingProgess);
            parser.MatchStarted += (_, _) => pBar.Message = $"{demoInfo.DemoName}: Match started";
            parser.RoundOfficiallyEnded += (_, _) =>
                pBar.Message = $"{demoInfo.DemoName}: Round {collector.RoundOfficiallyEndedCount} ended";

            // Start parsing.
            var processor = new Processor(parser, demoInfo, collector.Collect());
            processor.CreateFiles(
                opts.Output,
                opts.Folders.ToList(),
                opts.SameFileName,
                opts.SameFolderStructure
            );
        }

        private static List<DemoInformation> GetDemoInfo(Options opts)
        {
            var info = new List<DemoInformation>();

            foreach (string folder in opts.Folders)
            {
                string[] subDemos = Directory.GetFiles(
                    Path.GetFullPath(folder),
                    "*.dem",
                    opts.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly
                );

                foreach (string demo in subDemos)
                    info.Add(new DemoInformation(demo, opts.GameModeOverride, opts.TestType, opts.TestDateOverride));
            }

            foreach (string demo in opts.Demos)
                info.Add(new DemoInformation(demo, opts.GameModeOverride, opts.TestType, opts.TestDateOverride));

            return info;
        }
    }
}
