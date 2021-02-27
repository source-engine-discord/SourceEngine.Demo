using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using SourceEngine.Demo.Parser;
using SourceEngine.Demo.Stats.Models;

namespace SourceEngine.Demo.Stats.App
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

            if (!keyVals.ContainsKey("apikey"))
                throw new Exception("CFG::STEAM_API_KEY::NOT_FOUND");

            if(keyVals["apikey"] == "" || keyVals["apikey"] == null)
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
                        "-config                            [path]                      Path to config file\n" +
                        "-folders                           [paths (space seperated)]   Processes all demo files in each folder specified\n" +
                        "-demos                             [paths (space seperated)]   Processess a list of single demo files at paths\n" +
                        "-gamemodeoverride                  [string]                    Defines the gamemode for the match instead of having the parser attempt to figure it out -> (defuse / hostage / wingman / dangerzone)\n" +
                        "-testtype                          [string]                    Defines the test type for the match. Otherwise it attempts to grab it from the filename in SE Discord's filename formatting. Only matters for defuse and hostage. -> (competitive / casual)\n" +
                        "-hostagerescuezonecountoverride    [int]                       Defines the number of hostage rescue zones in the map. Without this, the parser assumes hostage has 1 and danger zone has 2 -> (0-4)\n" +
                        "-recursive                                                     Switch for recursive demo search\n" +
                        "-steaminfo                                                     Takes steam names from steam\n" +
                        "-clear                                                         Clears the data folder\n" +
                        "-nochickens                                                    Disables checks for number of chickens killed when parsing\n" +
                        "-noplayerpositions                                             Disables checks for player positions when parsing\n" +
                        "-samefilename                                                  Uses the demo's filename as the output filename\n" +
                        "-samefolderstructure                                           Uses the demo's folder structure inside the root folder for the output json file\n" +
                        "-lowoutputmode                                                 Does not print out the progress bar and round completed messages to console\n" +
						"\n"
                );
        }

        //Program entry point
        static void Main(string[] args)
        {
            string cfgPath = "config.cfg";
            string gamemodeoverride = "notprovided";
            string testType = "unknown";
            int hostagerescuezonecountoverride = 0;

            bool recursive = false;
            bool steaminfo = false;
            bool clear = false;
            bool parseChickens = true;
            bool parsePlayerPositions = true;
            string outputRootFolder = "parsed";
            bool sameFilename = false;
            bool sameFolderStructure = false;
            bool lowOutputMode = false;

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
                else if (arg == "-gamemodeoverride")
                {
                    if (i < args.Count())
                        gamemodeoverride = args[i + 1];

                    i++;
                }
                else if (arg == "-testtype")
                {
                    if (i < args.Count())
                        testType = args[i + 1];

                    i++;
                }
                else if (arg == "-hostagerescuezonecountoverride")
                {
                    if (i < args.Count())
                        if (!int.TryParse(args[i + 1], out hostagerescuezonecountoverride))
                            Debug.Error("Could not parse -hostagerescuezonecountoverride value as an int. Make sure that a number is provided.");

                    i++;
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
                else if (arg == "-noplayerpositions")
                {
                    parsePlayerPositions = false;
                }
                else if (arg == "-samefilename")
                {
                    sameFilename = true;
                }
                else if (arg == "-samefolderstructure")
                {
					sameFolderStructure = true;
                }
                else if (arg == "-lowoutputmode")
                {
					lowOutputMode = true;
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

                        if(Steam.getSteamUserNamesLookupTable(new List<long> { 76561198072130043 }) == null)
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

            //If the optional parameter -gamemodeoverride has been provided
            if (!string.IsNullOrWhiteSpace(gamemodeoverride))
            {
                //Make sure a valid gamemode has been given
                if (gamemodeoverride != "notprovided" && gamemodeoverride != "defuse" && gamemodeoverride != "hostage" && gamemodeoverride != "wingman" && gamemodeoverride != "dangerzone")
                {
                    Debug.Error("Invalid gamemode. Can be removed to have the parser attempt to figure it out itself. Accepted values are 'defuse', 'hostage', 'wingman' & 'dangerzone'");
                    return;
                }

                //Make sure a valid test type has been given
                if ((gamemodeoverride == "defuse" || gamemodeoverride == "hostage") && (testType != "casual" && testType != "competitive"))
                {
                    Debug.Error("Invalid test type. It must be 'casual' or 'competitive' when gamemode is either 'defuse' or 'hostage'.");
                    return;
                }
            }

            // Ensure only values 0-4 are provided when overriding the hostage rescue zone count
            if (hostagerescuezonecountoverride < 0 || hostagerescuezonecountoverride > 4)
            {
                Debug.Error("Invalid hostagerescuezonecountoverride amount. Specify between 0 and 4.");
                return;
            }

            //Ensure all folders to get demos from are created to avoid exceptions
            foreach (var folder in foldersToProcess)
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
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

            List<DemoInformation> demosInformation = new List<DemoInformation>();

            foreach (string folder in foldersToProcess)
            {
                try
                {
                    string[] subDemos = Directory.GetFiles(Path.GetFullPath(folder), "*.dem", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                    foreach (string demo in subDemos)
                    {
                        string[] pathSplit = demo.Split('\\');

						string[] filenameSplit = pathSplit[pathSplit.Count() - 1].Split('.');
						bool isFaceitDemo = Guid.TryParse(filenameSplit[0], out Guid guid);

                        AddDemoInformation(demosInformation, demo, testType, gamemodeoverride, isFaceitDemo, filenameSplit, pathSplit);
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
					string[] filenameSplit = demo.Split('.');
					bool isFaceitDemo = Guid.TryParse(filenameSplit[0], out Guid guid);

                    AddDemoInformation(demosInformation, demo, testType, gamemodeoverride, isFaceitDemo, filenameSplit, Array.Empty<string>());

                }
                catch (Exception e)
                {
                    throw e;
                }
            }

            Debug.Info("Starting processing of {0} demos.\n", demosInformation.Count());
            DateTime startTime = DateTime.Now;

            int passCount = 0;

            Console.CursorVisible = false;
            //Process all the found demos
            for (int i = 0; i < demosInformation.Count(); i++)
            {
				Console.WriteLine($"Parsing demo {demosInformation[i].DemoName}");

                MatchData mdTest = MatchData.FromDemoFile(demosInformation[i], parseChickens, parsePlayerPositions, hostagerescuezonecountoverride, lowOutputMode);

				IEnumerable<MatchStartedEventArgs> mse = new List<MatchStartedEventArgs>();
				IEnumerable<SwitchSidesEventArgs> sse = new List<SwitchSidesEventArgs>();
				IEnumerable<FeedbackMessage> fme = new List<FeedbackMessage>();
				IEnumerable<TeamPlayers> tpe = new List<TeamPlayers>();
                IEnumerable<PlayerHurt> ph = new List<PlayerHurt>();
                IEnumerable<PlayerKilledEventArgs> pke = new List<PlayerKilledEventArgs>();
				Dictionary<string, IEnumerable<Player>> pe = new Dictionary<string, IEnumerable<Player>>();
				IEnumerable<Equipment> pwe = new List<Equipment>();
				IEnumerable<int> poe = new List<int>();
				IEnumerable<BombPlanted> bpe = new List<BombPlanted>();
				IEnumerable<BombExploded> bee = new List<BombExploded>();
				IEnumerable<BombDefused> bde = new List<BombDefused>();
				IEnumerable<HostageRescued> hre = new List<HostageRescued>();
				IEnumerable<HostagePickedUp> hpu = new List<HostagePickedUp>();
				IEnumerable<DisconnectedPlayer> dpe = new List<DisconnectedPlayer>();
				IEnumerable<Team> te = new List<Team>();
				IEnumerable<RoundEndReason> re = new List<RoundEndReason>();
				IEnumerable<double> le = new List<double>();
				IEnumerable<TeamEquipment> tes = new List<TeamEquipment>();
				IEnumerable<NadeEventArgs> ge = new List<NadeEventArgs>();
				//IEnumerable<SmokeEventArgs> gse = new List<SmokeEventArgs>();
				//IEnumerable<FlashEventArgs> gfe = new List<FlashEventArgs>();
				//IEnumerable<GrenadeEventArgs> gge = new List<GrenadeEventArgs>();
				//IEnumerable<FireEventArgs> gie = new List<FireEventArgs>();
				//IEnumerable<DecoyEventArgs> gde = new List<DecoyEventArgs>();
				IEnumerable<ChickenKilledEventArgs> cke = new List<ChickenKilledEventArgs>();
				IEnumerable<ShotFired> sfe = new List<ShotFired>();
                IEnumerable<PlayerPositionsInstance> ppe = new List<PlayerPositionsInstance>();


                mse = (from start in mdTest.GetEvents<MatchStartedEventArgs>()
                      select (start as MatchStartedEventArgs));

                sse = (from switchSide in mdTest.GetEvents<SwitchSidesEventArgs>()
                      select (switchSide as SwitchSidesEventArgs));

                fme = (from message in mdTest.GetEvents<FeedbackMessage>()
                      select (message as FeedbackMessage));

                ph = (from player in mdTest.GetEvents<PlayerHurt>()
                     select (player as PlayerHurt));

				pke = (from player in mdTest.GetEvents<PlayerKilledEventArgs>()
                      select (player as PlayerKilledEventArgs));

                pe.Add("Kills", from player in mdTest.GetEvents<PlayerKilledEventArgs>()
                                select (player as PlayerKilledEventArgs).Killer);

                pe.Add("Deaths", from player in mdTest.GetEvents<PlayerKilledEventArgs>()
                                 select (player as PlayerKilledEventArgs).Victim);

                pe.Add("Headshots", from player in mdTest.GetEvents<PlayerKilledEventArgs>()
                                    where (player as PlayerKilledEventArgs).Headshot
                                    select (player as PlayerKilledEventArgs).Killer);

                pe.Add("Assists", from player in mdTest.GetEvents<PlayerKilledEventArgs>()
                                  where (player as PlayerKilledEventArgs).Assister != null
                                  select (player as PlayerKilledEventArgs).Assister);

                pwe = (from weapon in mdTest.GetEvents<PlayerKilledEventArgs>()
                      select (weapon as PlayerKilledEventArgs).Weapon);

				poe = (from penetration in mdTest.GetEvents<PlayerKilledEventArgs>()
                      select (penetration as PlayerKilledEventArgs).PenetratedObjects);

                pe.Add("MVPs", from player in mdTest.GetEvents<RoundMVPEventArgs>()
                               select (player as RoundMVPEventArgs).Player);

                pe.Add("Shots", from player in mdTest.GetEvents<WeaponFiredEventArgs>()
                                select (player as WeaponFiredEventArgs).Shooter);

                pe.Add("Plants", from player in mdTest.GetEvents<BombPlanted>()
                                 select (player as BombPlanted).Player);

                pe.Add("Defuses", from player in mdTest.GetEvents<BombDefused>()
                                  select (player as BombDefused).Player);

                pe.Add("Rescues", from player in mdTest.GetEvents<HostageRescued>()
                                  select (player as HostageRescued).Player);

				bpe = (from plant in mdTest.GetEvents<BombPlanted>()
                      select plant as BombPlanted)
                      .GroupBy(p => p.Round)
                      .Select(p => p.FirstOrDefault());

                bee = (from explode in mdTest.GetEvents<BombExploded>()
                      select explode as BombExploded)
                      .GroupBy(p => p.Round)
                      .Select(p => p.FirstOrDefault());

                bde = (from defuse in mdTest.GetEvents<BombDefused>()
                      select defuse as BombDefused)
                      .GroupBy(p => p.Round)
                      .Select(p => p.FirstOrDefault());

                hre = (from hostage in mdTest.GetEvents<HostageRescued>()
                      select hostage as HostageRescued);

                hpu = (from hostage in mdTest.GetEvents<HostagePickedUp>()
                      select hostage as HostagePickedUp);

                dpe = (from disconnection in mdTest.GetEvents<DisconnectedPlayer>()
                      select (disconnection as DisconnectedPlayer));

                te = (from team in mdTest.GetEvents<RoundOfficiallyEndedEventArgs>()
                     select (team as RoundOfficiallyEndedEventArgs).Winner);

                re = (from reason in mdTest.GetEvents<RoundOfficiallyEndedEventArgs>()
                     select (reason as RoundOfficiallyEndedEventArgs).Reason);

                le = (from length in mdTest.GetEvents<RoundOfficiallyEndedEventArgs>()
                     select (length as RoundOfficiallyEndedEventArgs).Length);

                tpe = (from teamPlayers in mdTest.GetEvents<TeamPlayers>()
                      where (teamPlayers as TeamPlayers).Round <= te.Count() // removes extra TeamPlayers if freezetime_end event triggers once a playtest is finished
                      select (teamPlayers as TeamPlayers));

                tes = (from round in mdTest.GetEvents<TeamEquipment>()
                      select (round as TeamEquipment));

                ge = (from nade in mdTest.GetEvents<NadeEventArgs>()
                     select (nade as NadeEventArgs));

                cke = (from chickenKill in mdTest.GetEvents<ChickenKilledEventArgs>()
                      select (chickenKill as ChickenKilledEventArgs));

                sfe = (from shot in mdTest.GetEvents<ShotFired>()
                      select (shot as ShotFired));

                ppe = (from playerPos in mdTest.GetEvents<PlayerPositionsInstance>()
                       select (playerPos as PlayerPositionsInstance));


                if (mdTest.passed)
                {
					// create the json output files using the data gathered
					var processedData = new ProcessedData()
					{
						DemoInformation = demosInformation[i],
						SameFilename = sameFilename,
						SameFolderStructure = sameFolderStructure,
						ParseChickens = parseChickens,
                        ParsePlayerPositions = parsePlayerPositions,
                        FoldersToProcess = foldersToProcess,
						OutputRootFolder = outputRootFolder,
						MatchStartValues = mse,
						SwitchSidesValues = sse,
						MessagesValues = fme,
						TeamPlayersValues = tpe,
                        PlayerHurtValues = ph,
                        PlayerKilledEventsValues = pke,
						PlayerValues = pe,
						WeaponValues = pwe,
						PenetrationValues = poe,
						BombsitePlantValues = bpe,
						BombsiteExplodeValues = bee,
						BombsiteDefuseValues = bde,
						HostageRescueValues = hre,
						HostagePickedUpValues = hpu,
						TeamValues = te,
						RoundEndReasonValues = re,
						RoundLengthValues = le,
						TeamEquipmentValues = tes,
						GrenadeValues = ge,
						ChickenValues = cke,
						ShotsFiredValues = sfe,
                        PlayerPositionsValues = ppe,
						WriteTicks = true
					};

                    AllOutputData allOutputData = mdTest.CreateFiles(processedData);

                    passCount++;

					Console.WriteLine($"Finished parsing demo {demosInformation[i].DemoName}.\n");
				}
				else
				{
					Console.WriteLine($"Failed parsing demo {demosInformation[i].DemoName}.\n");
				}
            }

            Console.CursorVisible = true;

            Debug.Blue("========== PROCESSING COMPLETE =========\n");
            DateTime end = DateTime.Now;

            Debug.White("Processing took {0} minutes\n", (end - startTime).TotalMinutes);
            Debug.White("Passed: {0}\n", passCount);
            Debug.White("Failed: {0}\n", demosInformation.Count() - passCount);
        }


        private static void AddDemoInformation(List<DemoInformation> demosInformation, string demo, string testType, string gamemode, bool isFaceitDemo, string[] filenameSplit, string[] pathSplit)
        {
            string testDate, mapname;

            if (isFaceitDemo)
            {
                testDate = "unknown";
                mapname = "unknown";
            }
            else
            {
                if (pathSplit.Count() > 0) // searching by folder
                {
                    filenameSplit = pathSplit[pathSplit.Count() - 1].Split('_', '.');
                }
                else // searching by demo
                {
                    filenameSplit = demo.Split('_', '.');
                }

                bool isSEDiscordDemo = filenameSplit.Count() > 5 ? true : false;

                if (isSEDiscordDemo)
                {
                    testDate = $"{ filenameSplit[1] }/{ filenameSplit[0] }/{ filenameSplit[2] }";
                    mapname = $"{ filenameSplit[3] }";

                    for (int i = 4; i < filenameSplit.Count() - 2; i++)
                    {
                        mapname += $"_{ filenameSplit[i] }";
                    }
                }
                else //cannot determine demo name format
                {
                    testDate = "unknown";
                    mapname = "unknown";
                }
            }

            demosInformation.Add(new DemoInformation { DemoName = demo, MapName = mapname, TestDate = testDate, TestType = testType, GameMode = gamemode });
        }
    }
}
