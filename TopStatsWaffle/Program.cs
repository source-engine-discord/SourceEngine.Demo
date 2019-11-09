using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DemoInfo;
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
                        "-config       [path]                     Path to config file\n" +
                        "-folders      [paths (space seperated)]  Processes all demo files in each folder specified\n" +
                        "-demos        [paths (space seperated)]  Processess a list of single demo files at paths\n" +
                        "-recursive                               Switch for recursive demo search\n" +
                        "-steaminfo                               Takes steam names from steam\n" +
                        "-clear                                   Clears the data folder\n" +
                        "-nochickens                              Disables checks for number of chickens killed when parsing\n" +
                        "-samefilename                            Uses the demo's filename as the output filename\n" +
                        "-samefolderstructure                     Uses the demo's folder structure inside the root folder for the output json file\n"
                );
        }

        //Program entry point
        static void Main(string[] args)
        {
            string cfgPath = "config.cfg";

            bool recursive = false;
            bool steaminfo = false;
            bool clear = false;
            bool parseChickens = true;
            string outputRootFolder = "parsed";
            bool sameFilename = false;
            bool samefolderstructure = false;

            List<string> foldersToProcess = new List<string>();
            List<string> demosToProcess = new List<string>();

            if (args.Count() == 0)
            {
                helpText();
                return;
            }

            for(int i = 0; i < args.Count(); i++)
            {
                var arg = args[i].ToLower();

                if (arg == "-config")
                {
                    if (i < args.Count())
                        cfgPath = args[i + 1];

                    i++;
                }
                else if (arg == "-folders")
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
                else if (arg == "-demos")
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
                else if (arg == "-output")
                {
                    outputRootFolder = args[i+1];
                    i++;
                }
                else if (arg == "-clear")
                {
                    clear = true;
                }
                else if (arg == "-steaminfo")
                {
                    steaminfo = true;
                }
                else if (arg == "-recursive")
                {
                    recursive = true;
                }
                else if (arg == "-help")
                {
                    helpText();
                    return;
                }
                else if (arg == "-nochickens")
                {
                    parseChickens = false;
                }
                else if (arg == "-samefilename")
                {
                    sameFilename = true;
                }
                else if (arg == "-samefolderstructure")
                {
                    samefolderstructure = true;
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

            //Clear by recreating folder
            if (clear && Directory.Exists(outputRootFolder))
            {
                Directory.Delete(outputRootFolder, true);
                Directory.CreateDirectory(outputRootFolder);
            }
            else if(!Directory.Exists(outputRootFolder))
            {
                Directory.CreateDirectory(outputRootFolder);
            }

            List<List<string>> demosInformation = new List<List<string>>();

            foreach (string folder in foldersToProcess)
            {
                try
                {
                    string[] subDemos = Directory.GetFiles(Path.GetFullPath(folder), "*.dem", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                    foreach (string demo in subDemos)
                    {
                        string[] pathSplit = demo.Split('\\');
                        string testDate, testType, mapname;

                        Guid guid;
                        string[] filenameSplit = pathSplit[pathSplit.Count() - 1].Split('.');
                        bool isFaceitDemo = Guid.TryParse(filenameSplit[0], out guid);

                        if (isFaceitDemo)
                        {
                            testDate = "unknown";
                            testType = "unknown";
                            mapname = "unknown";
                        }
                        else
                        {
                            filenameSplit = pathSplit[pathSplit.Count() - 1].Split('_', '.');

                            bool isSEDiscordDemo = filenameSplit.Count() > 5 ? true : false;

                            if (isSEDiscordDemo)
                            {
                                testDate = $"{ filenameSplit[1] }/{ filenameSplit[0] }/{ filenameSplit[2] }";
                                testType = $"{ filenameSplit[filenameSplit.Count() - 2] }";
                                mapname = $"{ filenameSplit[3] }";

                                for (int i = 4; i < filenameSplit.Count() - 2; i++)
                                {
                                    mapname += $"_{ filenameSplit[i] }";
                                }
                            }
                            else //cannot determine demo name format
                            {
                                testDate = "unknown";
                                testType = "unknown";
                                mapname = "unknown";
                            }
                        }

                        demosInformation.Add(new List<string>() { demo, mapname, testDate, testType });
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }
            }

            foreach (string demo in demosToProcess)
            {
                try
                {
                    string testDate, testType, mapname;

                    Guid guid;
                    string[] filenameSplit = demo.Split('.');
                    bool isFaceitDemo = Guid.TryParse(filenameSplit[0], out guid);

                    if (isFaceitDemo)
                    {
                        testDate = "unknown";
                        testType = "unknown";
                        mapname = "unknown";
                    }
                    else
                    {
                        filenameSplit = demo.Split('_', '.');

                        bool isSEDiscordDemo = filenameSplit.Count() > 5 ? true : false;

                        if (isSEDiscordDemo)
                        {
                            testDate = $"{ filenameSplit[1] }/{ filenameSplit[0] }/{ filenameSplit[2] }";
                            testType = $"{ filenameSplit[filenameSplit.Count() - 2] }";
                            mapname = $"{ filenameSplit[3] }";

                            for (int i = 4; i < filenameSplit.Count() - 2; i++)
                            {
                                mapname += $"_{ filenameSplit[i] }";
                            }
                        }
                        else //cannot determine demo name format
                        {
                            testDate = "unknown";
                            testType = "unknown";
                            mapname = "unknown";
                        }
                    }

                    demosInformation.Add(new List<string>() { demo, mapname, testDate, testType });
                }
                catch (Exception e)
                {
                    throw e;
                }
            }

            Debug.Info("Starting processing of {0} demos", demosInformation.Count());
            DateTime startTime = DateTime.Now;

            int passCount = 0;

            Console.CursorVisible = false;
            //Process all the found demos
            for (int i = 0; i < demosInformation.Count(); i++)
            {
                MatchData mdTest = MatchData.fromDemoFile(demosInformation[i][0], parseChickens);

                Dictionary<string, IEnumerable<MatchStartedEventArgs>> mse = new Dictionary<string, IEnumerable<MatchStartedEventArgs>>();
                Dictionary<string, IEnumerable<SwitchSidesEventArgs>> sse = new Dictionary<string, IEnumerable<SwitchSidesEventArgs>>();
                Dictionary<string, IEnumerable<FeedbackMessage>> fme = new Dictionary<string, IEnumerable<FeedbackMessage>>();
                Dictionary<string, IEnumerable<TeamPlayers>> tpe = new Dictionary<string, IEnumerable<TeamPlayers>>();
                Dictionary<string, IEnumerable<PlayerKilledEventArgs>> pke = new Dictionary<string, IEnumerable<PlayerKilledEventArgs>>();
                Dictionary<string, IEnumerable<Player>> pe = new Dictionary<string, IEnumerable<Player>>();
                Dictionary<string, IEnumerable<Equipment>> pwe = new Dictionary<string, IEnumerable<Equipment>>();
                Dictionary<string, IEnumerable<int>> poe = new Dictionary<string, IEnumerable<int>>();
                Dictionary<string, IEnumerable<char>> be = new Dictionary<string, IEnumerable<char>>();
                Dictionary<string, IEnumerable<BombPlanted>> bpe = new Dictionary<string, IEnumerable<BombPlanted>>();
                Dictionary<string, IEnumerable<BombExploded>> bee = new Dictionary<string, IEnumerable<BombExploded>>();
                Dictionary<string, IEnumerable<BombDefused>> bde = new Dictionary<string, IEnumerable<BombDefused>>();
                Dictionary<string, IEnumerable<char>> he = new Dictionary<string, IEnumerable<char>>();
                Dictionary<string, IEnumerable<HostageRescued>> hre = new Dictionary<string, IEnumerable<HostageRescued>>();
                Dictionary<string, IEnumerable<DisconnectedPlayer>> dpe = new Dictionary<string, IEnumerable<DisconnectedPlayer>>();
                Dictionary<string, IEnumerable<Team>> te = new Dictionary<string, IEnumerable<Team>>();
                Dictionary<string, IEnumerable<RoundEndReason>> re = new Dictionary<string, IEnumerable<RoundEndReason>>();
                Dictionary<string, IEnumerable<double>> le = new Dictionary<string, IEnumerable<double>>();
                Dictionary<string, IEnumerable<TeamEquipmentStats>> tes = new Dictionary<string, IEnumerable<TeamEquipmentStats>>();
                Dictionary<string, IEnumerable<NadeEventArgs>> ge = new Dictionary<string, IEnumerable<NadeEventArgs>>();
                Dictionary<string, IEnumerable<SmokeEventArgs>> gse = new Dictionary<string, IEnumerable<SmokeEventArgs>>();
                Dictionary<string, IEnumerable<FlashEventArgs>> gfe = new Dictionary<string, IEnumerable<FlashEventArgs>>();
                Dictionary<string, IEnumerable<GrenadeEventArgs>> gge = new Dictionary<string, IEnumerable<GrenadeEventArgs>>();
                Dictionary<string, IEnumerable<FireEventArgs>> gie = new Dictionary<string, IEnumerable<FireEventArgs>>();
                Dictionary<string, IEnumerable<DecoyEventArgs>> gde = new Dictionary<string, IEnumerable<DecoyEventArgs>>();
                Dictionary<string, IEnumerable<ChickenKilledEventArgs>> cke = new Dictionary<string, IEnumerable<ChickenKilledEventArgs>>();
                Dictionary<string, IEnumerable<ShotFired>> sfe = new Dictionary<string, IEnumerable<ShotFired>>();

                mse.Add("MatchStarts", from start in mdTest.getEvents<MatchStartedEventArgs>()
                                       select (start as MatchStartedEventArgs));

                sse.Add("SwitchSides", from switchSide in mdTest.getEvents<SwitchSidesEventArgs>()
                                       select (switchSide as SwitchSidesEventArgs));

                fme.Add("Messages", from message in mdTest.getEvents<FeedbackMessage>()
                                    select (message as FeedbackMessage));

                pke.Add("PlayerKilledEvents", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                              select (player as PlayerKilledEventArgs));

                pe.Add("Kills", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                select (player as PlayerKilledEventArgs).Killer);

                pe.Add("Deaths", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                 select (player as PlayerKilledEventArgs).Victim);

                pe.Add("Headshots", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                    where (player as PlayerKilledEventArgs).Headshot
                                    select (player as PlayerKilledEventArgs).Killer);

                pe.Add("Assists", from player in mdTest.getEvents<PlayerKilledEventArgs>()
                                  where (player as PlayerKilledEventArgs).Assister != null
                                  select (player as PlayerKilledEventArgs).Assister);

                pwe.Add("WeaponKillers", from weapon in mdTest.getEvents<PlayerKilledEventArgs>()
                                         select (weapon as PlayerKilledEventArgs).Weapon);

                poe.Add("PenetratedObjects", from penetration in mdTest.getEvents<PlayerKilledEventArgs>()
                                             select (penetration as PlayerKilledEventArgs).PenetratedObjects);

                pe.Add("MVPs", from player in mdTest.getEvents<RoundMVPEventArgs>()
                               select (player as RoundMVPEventArgs).Player);

                pe.Add("Shots", from player in mdTest.getEvents<WeaponFiredEventArgs>()
                                select (player as WeaponFiredEventArgs).Shooter);

                pe.Add("Plants", from player in mdTest.getEvents<BombPlanted>()
                                 select (player as BombPlanted).Player);

                pe.Add("Defuses", from player in mdTest.getEvents<BombDefused>()
                                  select (player as BombDefused).Player);

                pe.Add("Rescues", from player in mdTest.getEvents<HostageRescued>()
                                  select (player as HostageRescued).Player);

                bpe.Add("BombsitePlants", (from plant in mdTest.getEvents<BombPlanted>()
                                           select plant as BombPlanted)
                                          .GroupBy(p => p.Round)
                                          .Select(p => p.FirstOrDefault()));

                bee.Add("BombsiteExplosions", (from explode in mdTest.getEvents<BombExploded>()
                                               select explode as BombExploded)
                                               .GroupBy(p => p.Round)
                                               .Select(p => p.FirstOrDefault()));

                bde.Add("BombsiteDefuses", (from defuse in mdTest.getEvents<BombDefused>()
                                               select defuse as BombDefused)
                                               .GroupBy(p => p.Round)
                                               .Select(p => p.FirstOrDefault()));

                be.Add("PlantsSites", (from plant in mdTest.getEvents<BombPlanted>()
                                       select plant as BombPlanted)
                                      .GroupBy(p => p.Round)
                                      .Select(p => p.FirstOrDefault().Bombsite));

                be.Add("ExplosionsSites", (from explosion in mdTest.getEvents<BombExploded>()
                                           select explosion as BombExploded)
                                           .GroupBy(p => p.Round)
                                           .Select(p => p.FirstOrDefault().Bombsite));

                be.Add("DefusesSites", (from defuse in mdTest.getEvents<BombDefused>()
                                        select defuse as BombDefused)
                                        .GroupBy(p => p.Round)
                                        .Select(p => p.FirstOrDefault().Bombsite));

                hre.Add("HostageRescues", (from hostage in mdTest.getEvents<HostageRescued>()
                                           select hostage as HostageRescued));

                he.Add("RescuedHostages", from rescue in mdTest.getEvents<HostageRescued>()
                                          select (rescue as HostageRescued).Hostage);

                dpe.Add("DisconnectedPlayers", from disconnection in mdTest.getEvents<DisconnectedPlayer>()
                                               select (disconnection as DisconnectedPlayer));

                te.Add("RoundsWonTeams", from team in mdTest.getEvents<RoundEndedEventArgs>()
                                         select (team as RoundEndedEventArgs).Winner);

                re.Add("RoundsWonReasons", from reason in mdTest.getEvents<RoundEndedEventArgs>()
                                           select (reason as RoundEndedEventArgs).Reason);

                le.Add("RoundsLengths", from length in mdTest.getEvents<RoundEndedEventArgs>()
                                        select (length as RoundEndedEventArgs).Length);

                tpe.Add("TeamPlayers", from teamPlayers in mdTest.getEvents<TeamPlayers>()
                                       where (teamPlayers as TeamPlayers).Round <= te["RoundsWonTeams"].Count() // removes extra TeamPlayers if freezetime_end event triggers once a playtest is finished
                                       select (teamPlayers as TeamPlayers));

                tes.Add("TeamEquipmentStats", from round in mdTest.getEvents<TeamEquipmentStats>()
                                              select (round as TeamEquipmentStats));

                ge.Add("AllNadesThrown", from nade in mdTest.getEvents<NadeEventArgs>()
                                         select (nade as NadeEventArgs));

                cke.Add("ChickensKilled", from chickenKill in mdTest.getEvents<ChickenKilledEventArgs>()
                                          select (chickenKill as ChickenKilledEventArgs));

                sfe.Add("ShotsFired", from shot in mdTest.getEvents<ShotFired>()
                                      select (shot as ShotFired));

                TanookiStats tanookiStats = tanookiStatsCreator(tpe, dpe);


                if (mdTest.passed)
                {
                    mdTest.CreateFiles(demosInformation[i], sameFilename, samefolderstructure, parseChickens, foldersToProcess, outputRootFolder, tanookiStats, mse, sse, fme, tpe, pke, pe, pwe, poe, bpe, bee, bde, be, hre, he, te, re, le, tes, ge, cke, sfe);
                    passCount++;
                }
            }

            Console.CursorVisible = true;

            Debug.Blue("========== PROCESSING COMPLETE =========\n");
            DateTime end = DateTime.Now;

            Debug.White("Processing took {0} minutes\n", (end - startTime).TotalMinutes);
            Debug.White("Passed: {0}\n", passCount);
            Debug.White("Failed: {0}\n", demosInformation.Count() - passCount);
        }

        private static TanookiStats tanookiStatsCreator(Dictionary<string, IEnumerable<TeamPlayers>> tpe, Dictionary<string, IEnumerable<DisconnectedPlayer>> dpe)
        {
            TanookiStats tanookiStats = new TanookiStats() { Joined = false, Left = false, RoundJoined = -1, RoundLeft = -1, RoundsLasted = -1 };
            long tanookiId = 76561198123165941;

            if (tpe["TeamPlayers"].Any(t => t.Terrorists.Any(p => p.SteamID == tanookiId)) || tpe["TeamPlayers"].Any(t => t.CounterTerrorists.Any(p => p.SteamID == tanookiId)))
            {
                tanookiStats.Joined = true;
                tanookiStats.RoundJoined = 0; // set incase he joined in warmup but does not play any rounds

                IEnumerable<int> playedRoundsT = tpe["TeamPlayers"].Where(t => t.Round > 0 && t.Terrorists.Any(p => p.SteamID == tanookiId)).Select(r => r.Round);
                IEnumerable<int> playedRoundsCT = tpe["TeamPlayers"].Where(t => t.Round > 0 && t.CounterTerrorists.Any(p => p.SteamID == tanookiId)).Select(r => r.Round);

                tanookiStats.RoundsLasted = playedRoundsT.Count() + playedRoundsCT.Count();

                bool playedTSide = (playedRoundsT.Count() > 0) ? true : false;
                bool playedCTSide = (playedRoundsCT.Count() > 0) ? true : false;

                tanookiStats.RoundJoined = playedTSide ? (playedCTSide ? ((playedRoundsT.First() < playedRoundsCT.First()) ? playedRoundsT.First() : playedRoundsCT.First()) : playedRoundsT.First()) : (playedCTSide ? playedRoundsCT.First() : tanookiStats.RoundJoined);
            }

            if (dpe["DisconnectedPlayers"].Any(d => d.PlayerDisconnectEventArgs.Player != null && d.PlayerDisconnectEventArgs.Player.SteamID == tanookiId))
            {
                // checks if he played a round later on than his last disconnect (he left and joined back)
                int finalDisconnectRound = dpe["DisconnectedPlayers"].Where(d => d.PlayerDisconnectEventArgs.Player.SteamID == tanookiId).Reverse().Select(r => r.Round).First();
                tanookiStats.RoundLeft = (finalDisconnectRound > tanookiStats.RoundsLasted) ? finalDisconnectRound : tanookiStats.RoundLeft;

                tanookiStats.Left = (tanookiStats.RoundLeft > -1) ? true : false;
            }

            return tanookiStats;
        }
    }
}