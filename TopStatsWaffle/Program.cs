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
using TopStatsWaffle.Models;

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
            /*if (clear)
            {
                Directory.Delete("matches", true);
                Directory.CreateDirectory("matches");
            }*/

            List<List<string>> demos = new List<List<string>>();

            foreach(string folder in foldersToProcess)
            {
                string[] subDemos = Directory.GetFiles(Path.GetFullPath(folder), "*.dem", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                foreach (string demo in subDemos)
                {
                    string[] pathSplit = demo.Split('\\');
                    string[] filenameSplit = pathSplit[pathSplit.Count()-1].Split('_', '.');

                    string mapDate = $"{ filenameSplit[1] }/{ filenameSplit[0] }/{ filenameSplit[2] }";
                    string testType = $"{ filenameSplit[filenameSplit.Count() - 2] }";
                    string mapname = $"{ filenameSplit[3] }";

                    for (int i=4; i < filenameSplit.Count() - 2; i++)
                    {
                        mapname += $"_{ filenameSplit[i] }";
                    }

                    demos.Add(new List<string>() { demo, mapname, mapDate, testType });
                }
            }

            foreach (string demo in demosToProcess)
            {
                string[] filenameSplit = demo.Split('_', '.');

                string mapDate = $"{ filenameSplit[1] }/{ filenameSplit[0] }/{ filenameSplit[2] }";
                string testType = $"{ filenameSplit[filenameSplit.Count() - 2] }";
                string mapname = $"{ filenameSplit[3] }";

                for (int i = 4; i < filenameSplit.Count() - 2; i++)
                {
                    mapname += $"_{ filenameSplit[i] }";
                }

                demos.Add(new List<string>() { demo, mapname, mapDate, testType });
            }

            Debug.Info("Starting processing of {0} demos", demos.Count());
            DateTime startTime = DateTime.Now;

            int passCount = 0;

            Console.CursorVisible = false;
            //Process all the found demos
            for (int i = 0; i < demos.Count(); i++)
            {
                MatchData mdTest = MatchData.fromDemoFile(demos[i][0]);

                Dictionary<string, IEnumerable<TeamPlayers>> tpe = new Dictionary<string, IEnumerable<TeamPlayers>>();
                Dictionary<string, IEnumerable<Player>> pe = new Dictionary<string, IEnumerable<Player>>();
                Dictionary<string, IEnumerable<Vector>> ve = new Dictionary<string, IEnumerable<Vector>>();
                Dictionary<string, IEnumerable<char>> be = new Dictionary<string, IEnumerable<char>>();
                Dictionary<string, IEnumerable<DisconnectedPlayer>> dpe = new Dictionary<string, IEnumerable<DisconnectedPlayer>>();
                Dictionary<string, IEnumerable<Team>> te = new Dictionary<string, IEnumerable<Team>>();
                Dictionary<string, IEnumerable<RoundEndReason>> re = new Dictionary<string, IEnumerable<RoundEndReason>>();
                Dictionary<string, IEnumerable<TeamEquipmentStats>> tes = new Dictionary<string, IEnumerable<TeamEquipmentStats>>();
                Dictionary<string, IEnumerable<NadeEventArgs>> ge = new Dictionary<string, IEnumerable<NadeEventArgs>>();
                Dictionary<string, IEnumerable<SmokeEventArgs>> gse = new Dictionary<string, IEnumerable<SmokeEventArgs>>();
                Dictionary<string, IEnumerable<FlashEventArgs>> gfe = new Dictionary<string, IEnumerable<FlashEventArgs>>();
                Dictionary<string, IEnumerable<GrenadeEventArgs>> gge = new Dictionary<string, IEnumerable<GrenadeEventArgs>>();
                Dictionary<string, IEnumerable<FireEventArgs>> gie = new Dictionary<string, IEnumerable<FireEventArgs>>();
                Dictionary<string, IEnumerable<DecoyEventArgs>> gde = new Dictionary<string, IEnumerable<DecoyEventArgs>>();

                tpe.Add("TeamPlayers", from change in mdTest.getEvents<TeamPlayers>()
                                 select (change as TeamPlayers));

                pe.Add("Kills", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                select (player as PlayerKilledEventArgs).Killer);

                ve.Add("KillPositions", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                select (player as PlayerKilledEventArgs).KillerPosition);

                pe.Add("Deaths", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                 select (player as PlayerKilledEventArgs).Victim);

                ve.Add("DeathPositions", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                 select (player as PlayerKilledEventArgs).VictimPosition);

                pe.Add("Headshots", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                    where (player as PlayerKilledEventArgs).Headshot
                                    select (player as PlayerKilledEventArgs).Killer);

                pe.Add("Assists", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                  where (player as PlayerKilledEventArgs).Assister != null
                                  select (player as PlayerKilledEventArgs).Assister);

                pe.Add("MVPs", from player in mdTest.getEvents<RoundMVPEventArgs>()
                                select (player as RoundMVPEventArgs).Player);

                pe.Add("Shots", from player in mdTest.getEvents<WeaponFiredEventArgs>()
                                select (player as WeaponFiredEventArgs).Shooter);

                pe.Add("Plants", from player in mdTest.getEvents<BombEventArgs>()
                                 select (player as BombEventArgs).Player);

                pe.Add("Defuses", from player in mdTest.getEvents<BombDefuseEventArgs>()
                                  select (player as BombEventArgs).Player);

                be.Add("PlantsSites", from site in mdTest.getEvents<BombEventArgs>()
                                      select (site as BombEventArgs).Site);

                be.Add("DefusesSites", from site in mdTest.getEvents<BombDefuseEventArgs>()
                                  select (site as BombEventArgs).Site);

                dpe.Add("DisconnectedPlayers", from disconnection in mdTest.getEvents<DisconnectedPlayer>()
                                    select (disconnection as DisconnectedPlayer));

                te.Add("RoundsWonTeams", from team in mdTest.getEvents<RoundEndedEventArgs>()
                                       select (team as RoundEndedEventArgs).Winner);

                re.Add("RoundsWonReasons", from reason in mdTest.getEvents<RoundEndedEventArgs>()
                                       select (reason as RoundEndedEventArgs).Reason);

                tes.Add("TeamEquipmentStats", from round in mdTest.getEvents<TeamEquipmentStats>()
                                           select (round as TeamEquipmentStats));

                ge.Add("AllNadesThrown", from f in mdTest.getEvents<NadeEventArgs>()
                                  select (f as NadeEventArgs));

                TanookiStats tanookiStats = tanookiStatsCreator(tpe, dpe);


                if (mdTest.passed)
                {
                    mdTest.SaveCSV(demos[i], noguid, tanookiStats, tpe, pe, ve, be, te, re, tes, ge);
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

                string totalVersionNumber;
                Dictionary<string, List<string>> totalMap = new Dictionary<string, List<string>>();
                Dictionary<string, List<string>> totalTanooki = new Dictionary<string, List<string>>();
                Dictionary<string, Dictionary<string, string>> totalPlayerName = new Dictionary<string, Dictionary<string, string>>();
                Dictionary<long, Dictionary<string, long>> totalPlayer = new Dictionary<long, Dictionary<string, long>>();
                Dictionary<string, List<string>> totalTeam = new Dictionary<string, List<string>> ();
                Dictionary<string, List<string>> totalRoundEndReason = new Dictionary<string, List<string>>();
                Dictionary<string, List<int>> totalBombsite = new Dictionary<string, List<int>>();
                Dictionary<string, int> totalGrenadesTotal = new Dictionary<string, int>();
                Dictionary<string, Dictionary<string, string>> totalGrenadesSpecific = new Dictionary<string, Dictionary<string, string>>();
                Dictionary<string, Dictionary<string, string>> totalPlayerPositions = new Dictionary<string, Dictionary<string, string>>();

                int num = 0;
                foreach(string match in matches)
                {
                    num++;
                    List<string> headers = new List<string>();

                    StreamReader sr = new StreamReader(match);

                    string ln;

                    /* version number */
                    headers = sr.ReadLine().Split(',').ToList();
                    while ((ln = sr.ReadLine()) != string.Empty)
                    {
                        totalVersionNumber = ln;
                    }
                    /* version number end */

                    /* map info */
                    headers = sr.ReadLine().Split(',').ToList();
                    while ((ln = sr.ReadLine()) != string.Empty)
                    {
                        string[] elements = ln.Split(',');

                        if (!totalMap.ContainsKey(elements[0]))
                            totalMap.Add(elements[0], new List<string>() { elements[1], elements[2] });
                    }
                    /* map info end */

                    /* tanooki leave stats */
                    headers = sr.ReadLine().Split(',').ToList();
                    while ((ln = sr.ReadLine()) != string.Empty)
                    {
                        string[] elements = ln.Split(',');

                        if (!totalTanooki.ContainsKey(elements[0]))
                            totalTanooki.Add(elements[0], new List<string>() { elements[1], elements[2], elements[3], elements[4] });
                    }
                    /* tanooki leave stats end */

                    /* player stats */
                    headers = sr.ReadLine().Split(',').ToList();
                    while ((ln = sr.ReadLine()) != string.Empty)
                    {
                        string[] elements = ln.Split(',');

                        if (!totalPlayerName.ContainsKey(elements[0]))
                            totalPlayerName.Add(elements[0], new Dictionary<string, string>());

                        for (int i = 1; i < elements.Count(); i++)
                        {
                            if (!totalPlayerName[elements[0]].ContainsKey(headers[i]))
                                totalPlayerName[elements[0]].Add(headers[i], "");

                            totalPlayerName[elements[0]][headers[i]] += long.Parse(elements[i]);
                        }
                    }
                    /* player stats end */

                    /* round wins team stats */
                    headers = sr.ReadLine().Split(',').ToList();
                    while ((ln = sr.ReadLine()) != string.Empty)
                    {
                        string[] elements = ln.Split(',');

                        if (!totalTeam.ContainsKey(elements[0]))
                            totalTeam.Add(elements[0], new List<string>() { elements[1], elements[2] });
                    }
                    /* round wins team stats end*/

                    /* round end reason stats */
                    headers = sr.ReadLine().Split(',').ToList();
                    while ((ln = sr.ReadLine()) != string.Empty)
                    {
                        string[] elements = ln.Split(',');

                        if (!totalRoundEndReason.ContainsKey(elements[0]))
                            totalRoundEndReason.Add(elements[0], new List<string>() { elements[1], elements[2] });
                    }
                    /* round end reason stats end */

                    /* bombsite stats */
                    headers = sr.ReadLine().Split(',').ToList();
                    while ((ln = sr.ReadLine()) != string.Empty)
                    {
                        string[] elements = ln.Split(',');

                        if (!totalBombsite.ContainsKey(elements[0]))
                            totalBombsite.Add(elements[0], new List<int>() { int.Parse(elements[1]), int.Parse(elements[2]) });
                    }
                    /* bombsite stats end */

                    /* Grenades total stats */
                    headers = sr.ReadLine().Split(',').ToList();
                    while ((ln = sr.ReadLine()) != string.Empty)
                    {
                        string[] elements = ln.Split(',');

                        if (!totalGrenadesTotal.ContainsKey(elements[0]))
                            totalGrenadesTotal.Add(elements[0], int.Parse(elements[1]));
                    }
                    /* Grenades total stats end */

                    /* Grenades specific stats */
                    headers = sr.ReadLine().Split(',').ToList();
                    while ((ln = sr.ReadLine()) != string.Empty)
                    {
                        /*
                        string[] elements = ln.Split(',');

                        if (!totalGrenadesSpecific.ContainsKey(elements[0]))
                            totalGrenadesSpecific.Add(elements[0], new Dictionary<string, string>());

                        for (int i = 1; i < elements.Count(); i++)
                        {
                            if (!totalGrenadesSpecific[elements[0]].ContainsKey(headers[i]))
                                totalGrenadesSpecific[elements[0]].Add(headers[i], string.Empty);

                            totalGrenadesSpecific[elements[0]][headers[i]] += elements[i];
                        }
                        */

                        string[] elements = ln.Split(',');

                        Guid id = Guid.NewGuid();

                        totalGrenadesSpecific.Add(id.ToString(), new Dictionary<string, string>());

                        for (int i = 0; i < elements.Count(); i++)
                        {
                            totalGrenadesSpecific[id.ToString()].Add(headers[i], elements[i]);
                        }
                    }
                    /* Grenades specific stats end */

                    /* Player Kills/Death Positions */
                    headers = sr.ReadLine().Split(',').ToList();
                    while ((ln = sr.ReadLine()) != null) // != string.Empty if adding another stats group below
                    {
                        string[] elements = ln.Split(',');

                        Guid id = Guid.NewGuid();

                        totalPlayerPositions.Add(id.ToString(), new Dictionary<string, string>());

                        for (int i = 0; i < elements.Count(); i++)
                        {
                            totalPlayerPositions[id.ToString()].Add(headers[i], elements[i]);
                        }
                    }
                    /* Player Kills/Death Positions end */

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

        private static TanookiStats tanookiStatsCreator(Dictionary<string, IEnumerable<TeamPlayers>> tpe, Dictionary<string, IEnumerable<DisconnectedPlayer>> dpe)
        {
            TanookiStats tanookiStats = new TanookiStats() { Joined = false, Left = false, RoundJoined = -1, RoundLeft = -1, RoundsLasted = -1 };
            long tanookiId = 76561198123165941;

            if (tpe["TeamPlayers"].Any(t => t.Terrorists.Any(p => p.SteamID == tanookiId)) || tpe["TeamPlayers"].Any(t => t.CounterTerrorists.Any(p => p.SteamID == tanookiId)))
            {
                tanookiStats.Joined = true;

                foreach (var round in tpe["TeamPlayers"])
                {
                    foreach (var player in round.Terrorists)
                        if (player.SteamID == tanookiId)
                        {
                            tanookiStats.RoundJoined = round.Round;
                            goto TanookiLeftGoto;
                        }

                    foreach (var player in round.Terrorists)
                        if (player.SteamID == tanookiId)
                        {
                            tanookiStats.RoundJoined = round.Round;
                            goto TanookiLeftGoto;
                        }
                }

                TanookiLeftGoto:
                if (dpe["DisconnectedPlayers"].Any(d => d.PlayerDisconnectEventArgs.Player.SteamID == tanookiId))
                {
                    tanookiStats.Left = true;

                    foreach (var disconnection in dpe["DisconnectedPlayers"].Reverse())
                    {
                        if (disconnection.PlayerDisconnectEventArgs.Player.SteamID == tanookiId)
                        {
                            tanookiStats.Left = true;
                            tanookiStats.RoundLeft = disconnection.Round;
                            goto TanookiRoundsLastedGoto;
                        }
                    }
                }
            }

            TanookiRoundsLastedGoto:
            tanookiStats.RoundsLasted = tanookiStats.RoundLeft - tanookiStats.RoundJoined;
            if (tanookiStats.RoundsLasted < 0)
                tanookiStats.RoundsLasted = 0;

            return tanookiStats;
        }
    }
}