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

            Debug.Info("Reading config from {0}", path);

            string ln;
            while((ln = sr.ReadLine()) != null)
            {
                string[] elements = ln.Split('=');
                if(elements.Count() == 2)
                    keyVals.Add(elements[0], elements[1]);
            }

            if (!this.keyVals.ContainsKey("apikey"))
                throw new Exception("CFG::STEAM_API_KEY::NOT_FOUND");

            if(this.keyVals["apikey"] == "" || this.keyVals["apikey"] == null)
                throw new Exception("CFG:STEAM_API_KEY::INVALID");

            sr.Close();
        }
    }

    class Program
    {
        private static void helpText()
        {
            Debug.White("                             ========= HELP ==========\n\n" +
                        "Command line parameters:\n\n" +
                        "-config       [path]                     Path to config file\n\n" +
                        "-folders      [paths (space seperated)]  Processes all demo files in each folder specified\n" +
                        "-demos        [paths (space seperated)]  Processess a list of single demo files at paths\n" +
                        "-recursive                               Switch for recursive demo search\n\n" +
                        "-noguid                                  Disables GUID prefix on output files\n" + 
                        "-concat                                  Joins all csv's into one big one\n" +
                        "-steaminfo                               Takes steam names from steam\n" +
                        "-noclear                                 Disables clearing the data folder\n"
                );
        }

        //Program entry point
        static void Main(string[] args)
        {
            string cfgPath = "config.cfg";

            bool recursive = false;
            bool noguid = false;
            bool concat = false;
            bool steaminfo = false;
            bool clear = true;

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

                if(args[i] == "-noclear")
                {
                    clear = false;
                }

                if(args[i] == "-steaminfo")
                {
                    steaminfo = true;
                }

                if (args[i] == "-concat")
                {
                    concat = true;
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

            if (steaminfo)
            {
                if (File.Exists(cfgPath))
                {
                    try
                    {
                        Config cfg = new Config(cfgPath);

                        Steam.setAPIKey(cfg.keyVals["apikey"]);

                        if(Steam.getSteamUserNamesLookupTable(new List<long>() { 76561198072130043 }) == null)
                        {
                            throw new Exception("CONFIG::STEAM_API_KEY::INVALID");
                        }
                        
                    }
                    catch (Exception e)
                    {
                        Debug.Error("CONFIG ERROR... INFO:: {0}\nSteam names will not be retrieved!!!", e.Message);
                        steaminfo = false;
                    }
                }
                else
                {
                    Debug.Error("Config unreadable... Steam names will not be retrieved!!!");
                    steaminfo = false;
                }
            }


            if (!Directory.Exists("matches"))
                Directory.CreateDirectory("matches");

            //Clear by recreating folder
            if (clear)
            {
                Directory.Delete("matches", true);
                Directory.CreateDirectory("matches");
            }

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
                Dictionary<string, IEnumerable<Team>> te = new Dictionary<string, IEnumerable<Team>>();
                Dictionary<string, IEnumerable<RoundEndReason>> re = new Dictionary<string, IEnumerable<RoundEndReason>>();

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

                te.Add("RoundsWonTeams", from team in mdTest.getEvents<RoundEndedEventArgs>()
                                       select (team as RoundEndedEventArgs).Winner);

                re.Add("RoundsWonReasons", from reason in mdTest.getEvents<RoundEndedEventArgs>()
                                       select (reason as RoundEndedEventArgs).Reason);

                if (mdTest.passed)
                {
                    mdTest.SaveCSV("matches/" + (noguid ? "" : Guid.NewGuid().ToString("N")) + " " + Path.GetFileNameWithoutExtension(demos[i]) + ".csv", ce, te, re);
                    passCount++;
                }
            }

            Console.CursorVisible = true;

            Debug.Blue("========== PROCESSING COMPLETE =========\n");
            DateTime end = DateTime.Now;

            Debug.White("Processing took {0} minutes\n", (end - startTime).TotalMinutes);
            Debug.White("Passed: {0}\n", passCount);
            Debug.White("Failed: {0}\n", demos.Count() - passCount);

            if(concat)
            {
                Console.CursorVisible = false;

                string[] matches = Directory.GetFiles(Path.GetFullPath("matches"), "*.csv", SearchOption.AllDirectories);
                Debug.Blue("\n========== JOINING {0} MATCHES ==========\n\n", matches.Count());

                ProgressViewer pv = new ProgressViewer("Reading CSV's (0 of " + matches.Count() + ")");

                Dictionary<long, Dictionary<string, long>> totalPlayer = new Dictionary<long, Dictionary<string, long>>();
                Dictionary<string, string> totalTeam = new Dictionary<string, string>();
                Dictionary<string, string> totalRoundEndReason = new Dictionary<string, string>();

                int num = 0;
                foreach(string match in matches)
                {
                    num++;
                    List<string> headers = new List<string>();

                    StreamReader sr = new StreamReader(match);

                    string ln;

                    /* player stats */
                    headers = sr.ReadLine().Split(',').ToList();
                    while ((ln = sr.ReadLine()) != string.Empty)
                    {
                        string[] elements = ln.Split(',');

                        if (!totalPlayer.ContainsKey(long.Parse(elements[0])))
                            totalPlayer.Add(long.Parse(elements[0]), new Dictionary<string, long>());

                        for (int i = 1; i < elements.Count(); i++)
                        {
                            if (!totalPlayer[long.Parse(elements[0])].ContainsKey(headers[i]))
                                totalPlayer[long.Parse(elements[0])].Add(headers[i], 0);

                            totalPlayer[long.Parse(elements[0])][headers[i]] += long.Parse(elements[i]);
                        }
                    }
                    /* player stats end */

                    /* round wins team stats */
                    headers = sr.ReadLine().Split(',').ToList();
                    while ((ln = sr.ReadLine()) != string.Empty)
                    {
                        string[] elements = ln.Split(',');

                        if (!totalTeam.ContainsKey(elements[0]))
                            totalTeam.Add(elements[0], elements[1]);
                    }
                    /* round wins team stats end*/

                    /* round end reason stats */
                    headers = sr.ReadLine().Split(',').ToList();
                    while ((ln = sr.ReadLine()) != null) // != string.Empty if adding another stats group below
                    {
                        string[] elements = ln.Split(',');

                        if (!totalRoundEndReason.ContainsKey(elements[0]))
                            totalRoundEndReason.Add(elements[0], elements[1]);
                    }
                    /* round end reason stats end */

                    sr.Close();

                    pv.percent = (float)num / (float)matches.Count();
                    pv.title = "Reading CSV's (" + num + " of " + matches.Count() + ")";
                    pv.Draw();
                }

                pv.End();

                string fpath = (noguid ? "" : Guid.NewGuid().ToString("N")) + " all.csv";
                StreamWriter sw = new StreamWriter(fpath, false);

                List<string> allPlayerHeaders = new List<string>();
                foreach(long p in totalPlayer.Keys)
                {
                    foreach (string catagory in totalPlayer[p].Keys)
                        if (!allPlayerHeaders.Contains(catagory))
                            allPlayerHeaders.Add(catagory);
                }

                string header = "SteamID,";
                Dictionary<long, string> lookup = new Dictionary<long, string>();
                if (steaminfo)
                {
                    header += "Name,";

                    lookup = Steam.getSteamUserNamesLookupTable(totalPlayer.Keys.ToList());
                }
                foreach (string catagory in allPlayerHeaders)
                {
                    header += catagory + ",";
                }

                sw.WriteLine(header.Substring(0, header.Length - 1));

                foreach (long player in totalPlayer.Keys)
                {
                    string playerLine = player + ",";

                    if (steaminfo)
                        playerLine += lookup[player] + ",";

                    foreach (string catagory in totalPlayer[player].Keys)
                    {
                        if (totalPlayer[player].ContainsKey(catagory))
                            playerLine += totalPlayer[player][catagory] + ",";
                        else
                            playerLine += "0,";
                    }

                    sw.WriteLine(playerLine.Substring(0, playerLine.Length - 1));
                }

                foreach (string round in totalTeam.Keys)
                {
                    string teamLine = round + ",";

                    if (totalTeam[round].ToString() != null && totalTeam[round].ToString() != string.Empty)
                        teamLine += totalTeam[round] + ",";
                    else
                        teamLine += "0,";

                    sw.WriteLine(teamLine.Substring(0, teamLine.Length - 1));
                }

                foreach (string round in totalRoundEndReason.Keys)
                {
                    string roundEndReasonLine = round + ",";

                    if (totalRoundEndReason[round].ToString() != null && totalRoundEndReason[round].ToString() != string.Empty)
                        roundEndReasonLine += totalRoundEndReason[round] + ",";
                    else
                        roundEndReasonLine += "0,";

                    sw.WriteLine(roundEndReasonLine.Substring(0, roundEndReasonLine.Length - 1));
                }

                sw.Close();

                Console.CursorVisible = true;

                Debug.Blue("========== CONCATENATION COMPLETE =========\n");
                Debug.White("Saved to: {0}", fpath);
            }

            return;
        }
    }
}