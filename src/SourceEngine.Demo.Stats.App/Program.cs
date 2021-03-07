using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using CommandLine;
using CommandLine.Text;

using SourceEngine.Demo.Parser;
using SourceEngine.Demo.Parser.Entities;
using SourceEngine.Demo.Stats.Models;

namespace SourceEngine.Demo.Stats.App
{
    public class Options
    {
        private string testDateOverride;
        private uint? hostageRescueZoneCountOverride;

        [Option("config", Default = "config.cfg", HelpText = "Path to config file.")]
        public string Config { get; set; }

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

        [Option("steaminfo", Default = false, HelpText = "Retrieve player names from Steam.")]
        public bool SteamInfo { get; set; }

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

    //Config class, currently only holds api key

    //Todo: add some other settings that can be written in a text file
    //Todo: add a formal config parser
    internal class Config
    {
        internal readonly Dictionary<string, string> keyVals = new();

        internal Config(string path)
        {
            var sr = new StreamReader(path);

            Debug.Info("Reading config from {0}", path);

            string ln;

            while ((ln = sr.ReadLine()) != null)
            {
                string[] elements = ln.Split('=');
                if (elements.Length == 2)
                    keyVals.Add(elements[0], elements[1]);
            }

            if (!keyVals.ContainsKey("apikey"))
                throw new Exception("CFG::STEAM_API_KEY::NOT_FOUND");

            if (keyVals["apikey"] == "" || keyVals["apikey"] == null)
                throw new Exception("CFG:STEAM_API_KEY::INVALID");

            sr.Close();
        }
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

            if (opts.SteamInfo)
            {
                if (File.Exists(opts.Config))
                    try
                    {
                        var cfg = new Config(opts.Config);
                        Steam.setAPIKey(cfg.keyVals["apikey"]);

                        if (Steam.getSteamUserNamesLookupTable(new List<long> { 76561198072130043 }) == null)
                            throw new Exception("CONFIG::STEAM_API_KEY::INVALID");
                    }
                    catch (Exception e)
                    {
                        Debug.Error("CONFIG ERROR... INFO:: {0}\nSteam names will not be retrieved!!!", e.Message);
                    }
                else
                    Debug.Error("Config unreadable... Steam names will not be retrieved!!!");
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
