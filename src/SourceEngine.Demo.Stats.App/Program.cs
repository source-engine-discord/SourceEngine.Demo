using System;
using System.Collections.Generic;
using System.IO;

using ShellProgressBar;

using SourceEngine.Demo.Parser;
using SourceEngine.Demo.Stats.Models;

namespace SourceEngine.Demo.Stats.App
{
    internal class Program
    {
        private readonly Options options;
        private readonly Writer writer;

        public Program(Options options)
        {
            this.options = options;
            writer = new Writer(options.Output, options.CopyInputName, options.CopyInputDirectories);
        }

        public void ParseStats()
        {
            uint passCount = 0;
            uint failCount = 0;

            List<DemoInformation> info = GetDemoInfo();
            using var pBar = new ProgressBar(info.Count, "Processing demos");
            var barOptions = new ProgressBarOptions { ForegroundColor = ConsoleColor.Blue };

            // Process all the found demos.
            foreach (DemoInformation demoInfo in info)
            {
                using ChildProgressBar childBar = pBar.Spawn(int.MaxValue, demoInfo.DemoName, barOptions);

                try
                {
                    Processor processor = ParseSingle(demoInfo, childBar);

                    childBar.Message = $"{demoInfo.DemoName}: Writing JSON file";
                    WriteJson(demoInfo, processor);

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

        private Processor ParseSingle(DemoInformation demoInfo, ChildProgressBar pBar)
        {
            // Create the demo and stats parsers.
            using FileStream file = File.OpenRead(demoInfo.DemoName);
            using var parser = new DemoParser(
                file,
                !options.NoChickens,
                !options.NoPlayerPositions,
                options.RescueZones
            );
            var collector = new Collector(parser, demoInfo);

            // Set up events to report progress.
            IProgress<float> progress = pBar.AsProgress<float>();
            parser.TickDone += (_, _) => progress.Report(parser.ParsingProgess);
            parser.MatchStarted += (_, _) => pBar.Message = $"{demoInfo.DemoName}: Match started";
            parser.RoundOfficiallyEnded += (_, _) =>
                pBar.Message = $"{demoInfo.DemoName}: Round {collector.RoundOfficiallyEndedCount} ended";

            // Start parsing.
            CollectedData data = collector.Collect();

            // TODO: remove the DemoParser dependency from Processor.
            // It may be fine now, but it is concerning that it keeps a reference to it after it gets disposed.
            return new Processor(parser, demoInfo, data);
        }

        private List<DemoInformation> GetDemoInfo()
        {
            var info = new List<DemoInformation>();

            foreach (string folder in options.Folders)
            {
                string[] subDemos = Directory.GetFiles(
                    Path.GetFullPath(folder),
                    "*.dem",
                    options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly
                );

                foreach (string demo in subDemos)
                    info.Add(new DemoInformation(demo, options.GameMode, options.TestType, options.Date, folder));
            }

            foreach (string demo in options.Demos)
                info.Add(new DemoInformation(demo, options.GameMode, options.TestType, options.Date));

            return info;
        }

        private void WriteJson(DemoInformation demoInfo, Processor processor)
        {
            AllStats stats = processor.GetAllStats();
            writer.Write(stats, demoInfo, stats.mapInfo.MapName);

            if (!options.NoPlayerPositions)
            {
                PlayerPositionsStats positions = processor.GetPlayerPositionsStats(stats.mapInfo.DemoName);
                writer.Write(positions, demoInfo, stats.mapInfo.MapName, "_playerpositions");
            }
        }
    }
}
