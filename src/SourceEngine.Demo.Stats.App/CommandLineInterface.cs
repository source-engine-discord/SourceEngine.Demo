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
        internal static class CommandLineInterface
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

            parserResult.WithParsed(ProcessOptions).WithParsed(opts => new Program(opts).ParseStats())
                .WithNotParsed(errs => DisplayHelp(parserResult, errs));
        }

        private static void DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
        {
            var helpText = HelpText.AutoBuild(
                result,
                h =>
                {
                    h.AddEnumValuesToHelpText = true;
                    return h;
                }
            );

            if (errs.Any())
                Console.Error.WriteLine(helpText);
            else
                Console.WriteLine(helpText);
        }

        private static void ProcessOptions(Options opts)
        {
            if ((opts.GameMode is GameMode.Defuse || opts.GameMode is GameMode.Hostage)
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
    }

    internal class Options
    {
        private string date;
        private uint rescueZones;

        #region Input Options

        [Option("demos", Separator = ' ', HelpText = "Space-delimited list of paths to individual demos to parse.")]
        public IEnumerable<string> Demos { get; set; }

        [Option(
            "folders",
            Separator = ' ',
            HelpText = "Space-delimited list of directories in which to search for demos to parse."
        )]
        public IEnumerable<string> Folders { get; set; }

        [Option("recursive", Default = false, HelpText = "Recursively search for demos.")]
        public bool Recursive { get; set; }

        #endregion

        #region Output Options

        [Option("output", Default = "parsed", HelpText = "Path to the output directory.")]
        public string Output { get; set; }

        [Option("clear", Default = false, HelpText = "Clear the output directory.")]
        public bool Clear { get; set; }

        [Option(
            "copy-input-dirs",
            Default = false,
            HelpText = "Use the demo's folder structure inside the root folder for the output JSON file."
        )]
        public bool CopyInputDirectories { get; set; }

        [Option("copy-input-name", Default = false, HelpText = "Use the demo's filename as the output filename.")]
        public bool CopyInputName { get; set; }

        #endregion

        #region Overrides

        [Option(
            "date",
            Required = false,
            HelpText = "Recording date of the match in dd/MM/yyyy format. "
                + "If unset, attempt to parse from the file name instead assuming the format date_mapname_testtype."
        )]
        public string Date
        {
            get => date;
            set
            {
                if (!DateTime.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, 0, out _))
                    throw new FormatException("Date must be in the format dd/MM/yyyy.");

                date = value;
            }
        }

        [Option(
            "game-mode",
            Default = TestType.Unknown,
            HelpText = "Assume the demo is for this game mode rather than attempting to infer the game mode."
        )]
        public GameMode GameMode { get; set; }

        [Option(
            "rescue-zones",
            Default = 4u,
            HelpText = "Number of hostage rescue zones in the map. Valid values: 0-4"
        )]
        public uint RescueZones
        {
            get => rescueZones;
            set
            {
                if (value > 4)
                    throw new Exception("Value must be between 0 and 4, inclusive.");

                rescueZones = value;
            }
        }

        [Option(
            "test-type",
            Default = TestType.Unknown,
            HelpText = "The playtest type of the recorded match. "
                + "If unset, attempt to parse it from the file name instead assuming the format date_mapname_testtype. "
                + "Only relevant for defuse and hostage game modes."
        )]
        public TestType TestType { get; set; }

        #endregion

        #region Parsing Options

        [Option("no-chickens", Default = false, HelpText = "Disable counting of chicken death stats.")]
        public bool NoChickens { get; set; }

        [Option("no-pos", Default = false, HelpText = "Disable parsing of player positions.")]
        public bool NoPlayerPositions { get; set; }

        #endregion
    }
}
