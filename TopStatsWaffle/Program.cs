using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using DemoInfo;
using System.Threading;
using Newtonsoft.Json;

namespace TopStatsWaffle
{
    //Config class, currently only holds api key

    //Todo: add some other settings that can be written in a text file
    //Todo: add a formal config parser
    public class Config
    {
        public Dictionary<string, string> keyVals = new Dictionary<string, string>();

        public Config(string path)
        {
            StreamReader sr = new StreamReader(path);

            string ln;
            while((ln = sr.ReadLine()) != null)
            {
                string[] elements = ln.Split('=');
                if(elements.Count() == 2)
                    keyVals.Add(elements[0], elements[1]);
            }

            if (this.keyVals["apikey"] == null || this.keyVals["apikey"] == "")
            {
                Debug.Warn("CFG::STEAM_API_KEY::NOT_FOUND SteamID's will not be resolved");
            }
        }
    }

    class Program
    {
        private static void helpText()
        {
            Debug.White("========= HELP ==========\n" +
                "-config    \t [path]                    \t Path to config file (Legacy)\n" +
                "-folders   \t [paths (space seperated)] \t Processes all demo files in each folder specified\n" +
                "-demos     \t [paths (space seperated)] \t Processess a list of single demo files at paths\n" +
                "-recursive                              \t Switch for recursive demo search\n" +
                "-noguid                                 \t Disables GUID prefix\n"
                );
        }

        //Program entry point
        static void Main(string[] args)
        {
            string cfgPath = "config.cfg";

            bool recursive = false;
            bool noguid = false;

            List<string> foldersToProcess = new List<string>();
            List<string> demosToProcess = new List<string>();

            if (args.Count() == 0)
            {
                helpText();
                return;
            }

            for(int i = 0; i < args.Count(); i++)
            {
                if (args[i] == "-config")
                {
                    if (i < args.Count())
                        cfgPath = args[i + 1];

                    i++;
                }

                if(args[i] == "-folders")
                {
                    bool searching = true;
                    while (i < args.Count() - 1 && searching)
                    {
                        i++;

                        if (args[i][0] == '-')
                            searching = false;
                        else
                            foldersToProcess.Add(args[i]);
                    }
                    i--;
                }

                if (args[i] == "-demos")
                {
                    bool searching = true;
                    while (i < args.Count() - 1 && searching)
                    {
                        i++;

                        if (args[i][0] == '-')
                            searching = false;
                        else
                            demosToProcess.Add(args[i]);
                    }
                    i--;
                }

                if (args[i] == "-recursive")
                    recursive = true;

                if (args[i] == "-noguid")
                    noguid = true;

                if(args[i] == "-help")
                {
                    helpText();
                    return;
                }
            }


            if (!Directory.Exists("matches"))
                Directory.CreateDirectory("matches");

            List<string> demos = new List<string>();
            foreach(string folder in foldersToProcess)
            {
                string[] subDemos = Directory.GetFiles(Path.GetFullPath(folder), "*.dem", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                foreach (string demo in subDemos)
                    demos.Add(demo);
            }

            foreach (string demo in demosToProcess)
                demos.Add(demo);

            Debug.Info("Starting processing of {0} demos", demos.Count());
            DateTime startTime = DateTime.Now;

            int passCount = 0;

            Console.CursorVisible = false;
            //Process all the found demos
            for (int i = 0; i < demos.Count(); i++)
            {
                MatchData mdTest = MatchData.fromDemoFile(demos[i]);

                Dictionary<string, IEnumerable<Player>> ce = new Dictionary<string, IEnumerable<Player>>();

                ce.Add("Deaths", (from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                    select (player as PlayerKilledEventArgs).Killer));

                ce.Add("Kills", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                select (player as PlayerKilledEventArgs).Victim);

                ce.Add("Headshots", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                    where (player as PlayerKilledEventArgs).Headshot
                                    select (player as PlayerKilledEventArgs).Killer);

                ce.Add("Assists", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                    where (player as PlayerKilledEventArgs).Assister != null
                                    select (player as PlayerKilledEventArgs).Assister);

                ce.Add("Shots", from player in mdTest.getEvents<WeaponFiredEventArgs>()
                                select (player as WeaponFiredEventArgs).Shooter);

                ce.Add("Defuses", from player in mdTest.getEvents<BombDefuseEventArgs>()
                                    select (player as BombEventArgs).Player);

                ce.Add("Plants", from player in mdTest.getEvents<BombEventArgs>()
                                    select (player as BombEventArgs).Player);

                if (mdTest.passed)
                {
                    mdTest.SaveCSV("matches/" + (noguid ? Guid.NewGuid().ToString("N") : "") + " " + Path.GetFileNameWithoutExtension(demos[i]) + ".csv", ce);
                    passCount++;
                }
            }
            Console.CursorVisible = true;

            Debug.Blue("========== PROCESSING COMPLETE =========\n");
            DateTime end = DateTime.Now;

            Debug.White("Processing took {0} minutes\n", (end - startTime).TotalMinutes);
            Debug.White("Passed: {0}\n", passCount);
            Debug.White("Failed: {0}\n", demos.Count() - passCount);

            return;
        }
    }
}