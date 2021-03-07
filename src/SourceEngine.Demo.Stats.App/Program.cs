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

            var demosInformation = new List<DemoInformation>();

            foreach (string folder in opts.Folders)
            {
                string[] subDemos = Directory.GetFiles(
                    Path.GetFullPath(folder),
                    "*.dem",
                    opts.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly
                );

                foreach (string demo in subDemos)
                {
                    demosInformation.Add(
                        new DemoInformation(demo, opts.GameModeOverride, opts.TestType, opts.TestDateOverride)
                    );
                }
            }

            foreach (string demo in opts.Demos)
            {
                demosInformation.Add(
                    new DemoInformation(demo, opts.GameModeOverride, opts.TestType, opts.TestDateOverride)
                );
            }

            Debug.Info("Starting processing of {0} demos.\n", demosInformation.Count);
            DateTime startTime = DateTime.Now;

            int passCount = 0;

            Console.CursorVisible = false;

            //Process all the found demos
            foreach (DemoInformation t in demosInformation)
            {
                Console.WriteLine($"Parsing demo {t.DemoName}");

                MatchData mdTest = MatchData.FromDemoFile(
                    t,
                    !opts.NoChickens,
                    !opts.NoPlayerOptions,
                    opts.HostageRescueZoneCountOverride,
                    opts.LowOutputMode
                );

                IEnumerable<MatchStartedEventArgs> mse;
                IEnumerable<SwitchSidesEventArgs> sse;
                IEnumerable<FeedbackMessage> fme;
                IEnumerable<TeamPlayers> tpe;
                IEnumerable<PlayerHurt> ph;
                IEnumerable<PlayerKilledEventArgs> pke;
                var pe = new Dictionary<string, IEnumerable<Player>>();
                IEnumerable<Equipment> pwe;
                IEnumerable<int> poe;
                IEnumerable<BombPlanted> bpe;
                IEnumerable<BombExploded> bee;
                IEnumerable<BombDefused> bde;
                IEnumerable<HostageRescued> hre;
                IEnumerable<HostagePickedUp> hpu;
                IEnumerable<DisconnectedPlayer> dpe;
                IEnumerable<Team> te;
                IEnumerable<RoundEndReason> re;
                IEnumerable<double> le;
                IEnumerable<TeamEquipment> tes;
                IEnumerable<NadeEventArgs> ge;

                //IEnumerable<SmokeEventArgs> gse;
                //IEnumerable<FlashEventArgs> gfe;
                //IEnumerable<GrenadeEventArgs> gge;
                //IEnumerable<FireEventArgs> gie;
                //IEnumerable<DecoyEventArgs> gde;
                IEnumerable<ChickenKilledEventArgs> cke;
                IEnumerable<ShotFired> sfe;
                IEnumerable<PlayerPositionsInstance> ppe;

                mse = from start in mdTest.GetEvents<MatchStartedEventArgs>() select start as MatchStartedEventArgs;

                sse = from switchSide in mdTest.GetEvents<SwitchSidesEventArgs>()
                    select switchSide as SwitchSidesEventArgs;

                fme = from message in mdTest.GetEvents<FeedbackMessage>() select message as FeedbackMessage;

                ph = from player in mdTest.GetEvents<PlayerHurt>() select player as PlayerHurt;

                pke = from player in mdTest.GetEvents<PlayerKilledEventArgs>() select player as PlayerKilledEventArgs;

                pe.Add(
                    "Kills",
                    from player in mdTest.GetEvents<PlayerKilledEventArgs>()
                    select (player as PlayerKilledEventArgs).Killer
                );

                pe.Add(
                    "Deaths",
                    from player in mdTest.GetEvents<PlayerKilledEventArgs>()
                    select (player as PlayerKilledEventArgs).Victim
                );

                pe.Add(
                    "Headshots",
                    from player in mdTest.GetEvents<PlayerKilledEventArgs>()
                    where (player as PlayerKilledEventArgs).Headshot
                    select (player as PlayerKilledEventArgs).Killer
                );

                pe.Add(
                    "Assists",
                    from player in mdTest.GetEvents<PlayerKilledEventArgs>()
                    where (player as PlayerKilledEventArgs).Assister != null
                    select (player as PlayerKilledEventArgs).Assister
                );

                pwe = from weapon in mdTest.GetEvents<PlayerKilledEventArgs>()
                    select (weapon as PlayerKilledEventArgs).Weapon;

                poe = from penetration in mdTest.GetEvents<PlayerKilledEventArgs>()
                    select (penetration as PlayerKilledEventArgs).PenetratedObjects;

                pe.Add(
                    "MVPs",
                    from player in mdTest.GetEvents<RoundMVPEventArgs>() select (player as RoundMVPEventArgs).Player
                );

                pe.Add(
                    "Shots",
                    from player in mdTest.GetEvents<WeaponFiredEventArgs>()
                    select (player as WeaponFiredEventArgs).Shooter
                );

                pe.Add("Plants", from player in mdTest.GetEvents<BombPlanted>() select (player as BombPlanted).Player);

                pe.Add("Defuses", from player in mdTest.GetEvents<BombDefused>() select (player as BombDefused).Player);

                pe.Add(
                    "Rescues",
                    from player in mdTest.GetEvents<HostageRescued>() select (player as HostageRescued).Player
                );

                bpe = (from plant in mdTest.GetEvents<BombPlanted>() select plant as BombPlanted).GroupBy(p => p.Round)
                    .Select(p => p.FirstOrDefault());

                bee = (from explode in mdTest.GetEvents<BombExploded>() select explode as BombExploded)
                    .GroupBy(p => p.Round).Select(p => p.FirstOrDefault());

                bde = (from defuse in mdTest.GetEvents<BombDefused>() select defuse as BombDefused)
                    .GroupBy(p => p.Round).Select(p => p.FirstOrDefault());

                hre = from hostage in mdTest.GetEvents<HostageRescued>() select hostage as HostageRescued;

                hpu = from hostage in mdTest.GetEvents<HostagePickedUp>() select hostage as HostagePickedUp;

                dpe = from disconnection in mdTest.GetEvents<DisconnectedPlayer>()
                    select disconnection as DisconnectedPlayer;

                te = from team in mdTest.GetEvents<RoundOfficiallyEndedEventArgs>()
                    select (team as RoundOfficiallyEndedEventArgs).Winner;

                re = from reason in mdTest.GetEvents<RoundOfficiallyEndedEventArgs>()
                    select (reason as RoundOfficiallyEndedEventArgs).Reason;

                le = from length in mdTest.GetEvents<RoundOfficiallyEndedEventArgs>()
                    select (length as RoundOfficiallyEndedEventArgs).Length;

                tpe = from teamPlayers in mdTest.GetEvents<TeamPlayers>()
                    where (teamPlayers as TeamPlayers).Round
                        <= te.Count() // removes extra TeamPlayers if freezetime_end event triggers once a playtest is finished
                    select teamPlayers as TeamPlayers;

                tes = from round in mdTest.GetEvents<TeamEquipment>() select round as TeamEquipment;

                ge = from nade in mdTest.GetEvents<NadeEventArgs>() select nade as NadeEventArgs;

                cke = from chickenKill in mdTest.GetEvents<ChickenKilledEventArgs>()
                    select chickenKill as ChickenKilledEventArgs;

                sfe = from shot in mdTest.GetEvents<ShotFired>() select shot as ShotFired;

                ppe = from playerPos in mdTest.GetEvents<PlayerPositionsInstance>()
                    select playerPos as PlayerPositionsInstance;

                tanookiStats tanookiStats = tanookiStatsCreator(tpe, dpe);

                if (mdTest.passed)
                {
                    // create the json output files using the data gathered
                    var processedData = new ProcessedData
                    {
                        DemoInformation = t,
                        SameFilename = opts.SameFileName,
                        SameFolderStructure = opts.SameFolderStructure,
                        ParseChickens = !opts.NoChickens,
                        ParsePlayerPositions = !opts.NoPlayerOptions,
                        FoldersToProcess = opts.Folders.ToList(),
                        OutputRootFolder = opts.Output,
                        tanookiStats = tanookiStats,
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
                        WriteTicks = true,
                    };

                    mdTest.CreateFiles(processedData);

                    passCount++;

                    Console.WriteLine($"Finished parsing demo {t.DemoName}.\n");
                }
                else
                {
                    Console.WriteLine($"Failed parsing demo {t.DemoName}.\n");
                }
            }

            Console.CursorVisible = true;

            Debug.Blue("========== PROCESSING COMPLETE =========\n");
            DateTime end = DateTime.Now;

            Debug.White("Processing took {0} minutes\n", (end - startTime).TotalMinutes);
            Debug.White("Passed: {0}\n", passCount);
            Debug.White("Failed: {0}\n", demosInformation.Count - passCount);
        }

        //Program entry point
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

        private static tanookiStats tanookiStatsCreator(
            IEnumerable<TeamPlayers> tpe,
            IEnumerable<DisconnectedPlayer> dpe)
        {
            var tanookiStats = new tanookiStats
            {
                Joined = false,
                Left = false,
                RoundJoined = -1,
                RoundLeft = -1,
                RoundsLasted = -1,
            };

            const long tanookiId = 76561198123165941;

            if (tpe.Any(t => t.Terrorists.Any(p => p.SteamID == tanookiId))
                || tpe.Any(t => t.CounterTerrorists.Any(p => p.SteamID == tanookiId)))
            {
                tanookiStats.Joined = true;
                tanookiStats.RoundJoined = 0; // set in case he joined in warmup but does not play any rounds

                IEnumerable<int> playedRoundsT =
                    tpe.Where(t => t.Round > 0 && t.Terrorists.Any(p => p.SteamID == tanookiId)).Select(r => r.Round);

                IEnumerable<int> playedRoundsCT =
                    tpe.Where(t => t.Round > 0 && t.CounterTerrorists.Any(p => p.SteamID == tanookiId))
                        .Select(r => r.Round);

                tanookiStats.RoundsLasted = playedRoundsT.Count() + playedRoundsCT.Count();

                bool playedTSide = playedRoundsT.Any();
                bool playedCTSide = playedRoundsCT.Any();

                tanookiStats.RoundJoined = playedTSide ? playedCTSide ? playedRoundsT.First() < playedRoundsCT.First()
                        ?
                        playedRoundsT.First()
                        : playedRoundsCT.First() : playedRoundsT.First() :
                    playedCTSide ? playedRoundsCT.First() : tanookiStats.RoundJoined;
            }

            if (dpe.Any(
                d => d.PlayerDisconnectEventArgs.Player != null
                    && d.PlayerDisconnectEventArgs.Player.SteamID == tanookiId
            ))
            {
                // checks if he played a round later on than his last disconnect (he left and joined back)
                int finalDisconnectRound = dpe.Where(d => d.PlayerDisconnectEventArgs.Player.SteamID == tanookiId)
                    .Reverse().Select(r => r.Round).First();

                tanookiStats.RoundLeft = finalDisconnectRound > tanookiStats.RoundsLasted
                    ? finalDisconnectRound
                    : tanookiStats.RoundLeft;

                tanookiStats.Left = tanookiStats.RoundLeft > -1;
            }

            return tanookiStats;
        }
    }
}
