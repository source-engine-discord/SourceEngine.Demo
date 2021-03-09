using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json;

using SourceEngine.Demo.Parser;
using SourceEngine.Demo.Parser.Entities;
using SourceEngine.Demo.Parser.Structs;
using SourceEngine.Demo.Stats.Models;

namespace SourceEngine.Demo.Stats
{
    internal enum PSTATUS
    {
        ONSERVER,
        PLAYING,
        ALIVE,
    }

    public class TickCounter
    {
        public string detectedName = "NOT FOUND";
        public long ticksAlive;
        public long ticksOnServer;
        public long ticksPlaying;
    }

    public partial class MatchData
    {
        private static DemoParser dp;
        private ProcessedData processedData = new();
        public readonly Dictionary<int, long> playerLookups = new();
        public readonly Dictionary<int, int> playerReplacements = new();

        private readonly Dictionary<int, TickCounter> playerTicks = new();

        private readonly DemoInformation demoInfo;

        // Used in ValidateBombsite() for knowing when a bombsite plant site has been changed from '?' to an actual bombsite letter
        public bool changingPlantedRoundsToA, changingPlantedRoundsToB;

        public bool passed;

        /// <summary>
        /// Adds new player lookups and tick values
        /// </summary>
        /// <param name="p"></param>
        /// <returns>Whether or not the userID given has newly been / was previously stored</returns>
        public bool BindPlayer(Player p)
        {
            int duplicateIdToRemoveTicks = 0;
            int duplicateIdToRemoveLookup = 0;

            if (p.Name != "unconnected" && p.Name != "GOTV")
            {
                if (!playerTicks.ContainsKey(p.UserID))
                {
                    // check if player has been added twice with different UserIDs
                    (int userId, TickCounter counter) = playerTicks.FirstOrDefault(x => x.Value.detectedName == p.Name);

                    if (userId != 0)
                    {
                        // copy duplicate's information across
                        playerTicks.Add(
                            p.UserID,
                            new TickCounter
                            {
                                detectedName = counter.detectedName,
                                ticksAlive = counter.ticksAlive,
                                ticksOnServer = counter.ticksOnServer,
                                ticksPlaying = counter.ticksPlaying,
                            }
                        );

                        duplicateIdToRemoveTicks = userId;
                    }
                    else
                    {
                        var detectedName = string.IsNullOrWhiteSpace(p.Name) ? "NOT FOUND" : p.Name;
                        playerTicks.Add(p.UserID, new TickCounter { detectedName = detectedName });
                    }
                }

                if (!playerLookups.ContainsKey(p.UserID))
                {
                    // check if player has been added twice with different UserIDs
                    KeyValuePair<int, long> duplicate = playerLookups.FirstOrDefault(x => x.Value == p.SteamID);

                    if (duplicate.Key == 0) // if the steam ID was 0
                        duplicate = playerLookups.FirstOrDefault(x => x.Key == duplicateIdToRemoveTicks);

                    if (p.SteamID != 0)
                        playerLookups.Add(p.UserID, p.SteamID);
                    else if (p.SteamID == 0 && duplicate.Key != 0)
                        playerLookups.Add(p.UserID, duplicate.Value);

                    duplicateIdToRemoveLookup = duplicate.Key;
                }

                // remove duplicates
                if (duplicateIdToRemoveTicks != 0 || duplicateIdToRemoveLookup != 0)
                {
                    if (duplicateIdToRemoveTicks != 0)
                        playerTicks.Remove(duplicateIdToRemoveTicks);

                    if (duplicateIdToRemoveLookup != 0)
                        playerLookups.Remove(duplicateIdToRemoveLookup);

                    /* store duplicate userIDs for replacing in events later on */
                    var idRemoved = duplicateIdToRemoveLookup != 0
                        ? duplicateIdToRemoveLookup
                        : duplicateIdToRemoveTicks;

                    // removes any instance of the old userID pointing to a different userID
                    if (playerReplacements.Any(r => r.Key == idRemoved))
                        playerReplacements.Remove(idRemoved);

                    // tries to avoid infinite loops by removing the old entry
                    if (playerReplacements.Any(r => r.Key == p.UserID && r.Value == idRemoved))
                        playerReplacements.Remove(p.UserID);

                    // replace current mappings between an ancient userID & the old userID, to use the new userID as the value instead
                    if (playerReplacements.Any(r => r.Value == idRemoved))
                    {
                        IEnumerable<int> keysToReplaceValue =
                            playerReplacements.Where(r => r.Value == idRemoved).Select(r => r.Key);

                        foreach (var userId in keysToReplaceValue.ToList())
                            playerReplacements[userId] = p.UserID;
                    }

                    playerReplacements.Add(
                        idRemoved,
                        p.UserID
                    ); // Creates a new entry that maps the player's old user ID to their new user ID
                }

                return true;
            }

            return false;
        }

        private void addTick(Player p, PSTATUS status)
        {
            bool userIdStored = BindPlayer(p);

            if (userIdStored)
            {
                if (status == PSTATUS.ONSERVER)
                    playerTicks[p.UserID].ticksOnServer++;

                if (status == PSTATUS.ALIVE)
                    playerTicks[p.UserID].ticksAlive++;

                if (status == PSTATUS.PLAYING)
                    playerTicks[p.UserID].ticksPlaying++;
            }
        }

        public MatchData(
            DemoInformation demoInfo,
            bool parseChickens,
            bool parsePlayerPositions,
            uint? hostagerescuezonecountoverride,
            bool lowOutputMode)
        {
            string file = demoInfo.DemoName;
            this.demoInfo = demoInfo;

            // automatically decides rescue zone amounts unless overridden with a provided parameter
            if (hostagerescuezonecountoverride is not { } hostageRescueZones)
            {
                if (demoInfo.GameMode is GameMode.DangerZone)
                    hostageRescueZones = 2;
                else if (demoInfo.GameMode is GameMode.Hostage)
                    hostageRescueZones = 1;
                else
                    hostageRescueZones = 0;
            }

            //Create demo parser instance
            dp = new DemoParser(
                File.OpenRead(file),
                parseChickens,
                parsePlayerPositions,
                hostageRescueZones
            );

            dp.ParseHeader();

            // SERVER EVENTS ===================================================
            dp.MatchStarted += MatchStartedEventHandler;
            dp.ChickenKilled += ChickenKilledEventHandler;
            dp.SayText2 += SayText2EventHandler;
            dp.RoundEnd += RoundEndEventHandler;
            dp.RoundOfficiallyEnded += RoundOfficiallyEndedEventHandler;
            dp.SwitchSides += SwitchSidesEventHandler;
            dp.FreezetimeEnded += FreezetimeEndedEventHandler;

            // PLAYER EVENTS ===================================================
            dp.PlayerKilled += PlayerKilledEventHandler;
            dp.PlayerHurt += PlayerHurtEventHandler;
            dp.RoundMVP += RoundMVPEventHandler;
            dp.PlayerDisconnect += PlayerDisconnectEventHandler;
            dp.PlayerBind += PlayerBindEventHandler;
            dp.PlayerPositions += PlayerPositionsEventHandler;

            // BOMB EVENTS =====================================================
            dp.BombPlanted += BombPlantedEventHandler;
            dp.BombExploded += BombExplodedEventHandler;
            dp.BombDefused += BombDefusedEventHandler;

            // HOSTAGE EVENTS =====================================================
            dp.HostageRescued += HostageRescuedEventHandler;
            dp.HostagePickedUp += HostagePickedUpEventHandler;

            // WEAPON EVENTS ===================================================
            dp.WeaponFired += WeaponFiredEventHandler;

            // GRENADE EVENTS ==================================================
            dp.ExplosiveNadeExploded += GrenadeEventHandler;
            dp.FireNadeStarted += GrenadeEventHandler;
            dp.SmokeNadeStarted += GrenadeEventHandler;
            dp.FlashNadeExploded += GrenadeEventHandler;
            dp.DecoyNadeStarted += GrenadeEventHandler;

            // PLAYER TICK HANDLER ============================================
            dp.TickDone += TickDoneEventHandler;

            ProgressViewer pv = null;
            if (!lowOutputMode)
                pv = SetUpProgressBar();

            try
            {
                dp.ParseToEnd();
                FinishProcessedData();
                pv?.End();
                passed = true;
            }
            catch (Exception)
            {
                pv?.Error();
            }

            dp.Dispose();
        }

        private ProgressViewer SetUpProgressBar()
        {
            const int interval = 2500;
            int progMod = interval;
            var pv = new ProgressViewer(Path.GetFileName(demoInfo.DemoName));

            // PROGRESS BAR ==================================================
            dp.TickDone += (_, _) =>
            {
                progMod++;

                if (progMod >= interval)
                {
                    progMod = 0;

                    pv.percent = dp.ParsingProgess;
                    pv.Draw();
                }
            };

            // Print rounds complete out to console.
            dp.MatchStarted += (_, _) =>
            {
                Console.WriteLine("\n");
                Console.WriteLine("Match restarted.");
            };

            dp.RoundOfficiallyEnded += (_, _) =>
            {
                // Stop the progress bar getting in the way of the first row.
                if (roundOfficiallyEndedCount == 1)
                    Console.WriteLine("\n");

                Console.WriteLine("Round " + roundOfficiallyEndedCount + " complete.");
            };

            return pv;
        }

        private void FinishProcessedData()
        {
            // Only keep the first event for each round.
            // TODO: is this still necessary?
            processedData.BombsitePlantValues = processedData.BombsitePlantValues
                .GroupBy(e => e.Round)
                .Select(group => group.FirstOrDefault())
                .ToList();

            processedData.BombsiteDefuseValues = processedData.BombsiteDefuseValues
                .GroupBy(e => e.Round)
                .Select(group => group.FirstOrDefault())
                .ToList();

            processedData.BombsiteExplodeValues = processedData.BombsiteExplodeValues
                .GroupBy(e => e.Round)
                .Select(group => group.FirstOrDefault())
                .ToList();

            // Remove extra TeamPlayers if freezetime_end event triggers once a playtest is finished.
            processedData.TeamPlayersValues = processedData.TeamPlayersValues
                .Where(tp => tp.Round <= processedData.TeamValues.Count)
                .ToList();

            processedData.tanookiStats = TanookiStatsCreator(
                processedData.TeamPlayersValues,
                processedData.DisconnectedPlayerValues
            );

            processedData.WriteTicks = true;
        }

        private static tanookiStats TanookiStatsCreator(
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

        public AllStats GetAllStats()
        {
            var mapNameSplit = processedData.MatchStartValues.Any()
                ? processedData.MatchStartValues.ElementAt(0).Mapname.Split('/')
                : new[] { demoInfo.MapName };

            DataAndPlayerNames dataAndPlayerNames = GetDataAndPlayerNames(processedData);

            var allStats = new AllStats
            {
                versionNumber = GetVersionNumber(),
                supportedGamemodes = Enum.GetNames(typeof(GameMode)).Select(gm => gm.ToLower()).ToList(),
                mapInfo = GetMapInfo(processedData, mapNameSplit),
                tanookiStats = processedData.tanookiStats,
            };

            if (CheckIfStatsShouldBeCreated("playerStats", demoInfo.GameMode))
                allStats.playerStats = GetPlayerStats(
                    processedData,
                    dataAndPlayerNames.Data,
                    dataAndPlayerNames.PlayerNames
                );

            GeneralroundsStats generalroundsStats = GetGeneralRoundsStats(
                processedData,
                dataAndPlayerNames.PlayerNames
            );

            if (CheckIfStatsShouldBeCreated("winnersStats", demoInfo.GameMode))
                allStats.winnersStats = generalroundsStats.winnersStats;

            if (CheckIfStatsShouldBeCreated("roundsStats", demoInfo.GameMode))
                allStats.roundsStats = generalroundsStats.roundsStats;

            if (CheckIfStatsShouldBeCreated("bombsiteStats", demoInfo.GameMode))
                allStats.bombsiteStats = GetBombsiteStats(processedData);

            if (CheckIfStatsShouldBeCreated("hostageStats", demoInfo.GameMode))
                allStats.hostageStats = GetHostageStats(processedData);

            if (CheckIfStatsShouldBeCreated("rescueZoneStats", demoInfo.GameMode))
                allStats.rescueZoneStats = GetRescueZoneStats();

            Dictionary<EquipmentElement, List<NadeEventArgs>> nadeGroups = processedData.GrenadeValues
                .Where(e => e.NadeType >= EquipmentElement.Decoy && e.NadeType <= EquipmentElement.HE)
                .GroupBy(e => e.NadeType).ToDictionary(g => g.Key, g => g.ToList());

            if (CheckIfStatsShouldBeCreated("grenadesTotalStats", demoInfo.GameMode))
                allStats.grenadesTotalStats = GetGrenadesTotalStats(nadeGroups);

            if (CheckIfStatsShouldBeCreated("grenadesSpecificStats", demoInfo.GameMode))
                allStats.grenadesSpecificStats = GetGrenadesSpecificStats(nadeGroups, dataAndPlayerNames.PlayerNames);

            if (CheckIfStatsShouldBeCreated("killsStats", demoInfo.GameMode))
                allStats.killsStats = GetKillsStats(processedData, dataAndPlayerNames.PlayerNames);

            if (CheckIfStatsShouldBeCreated("feedbackMessages", demoInfo.GameMode))
                allStats.feedbackMessages = GetFeedbackMessages(processedData, dataAndPlayerNames.PlayerNames);

            if (dp.ParseChickens && CheckIfStatsShouldBeCreated(
                "chickenStats",
                demoInfo.GameMode
            ))
                allStats.chickenStats = GetChickenStats(processedData);

            if (CheckIfStatsShouldBeCreated("teamStats", demoInfo.GameMode))
                allStats.teamStats = GetTeamStats(
                    processedData,
                    allStats,
                    dataAndPlayerNames.PlayerNames,
                    generalroundsStats.SwitchSides
                );

            if (CheckIfStatsShouldBeCreated("firstDamageStats", demoInfo.GameMode))
                allStats.firstDamageStats = GetFirstDamageStats(processedData);

            return allStats;
        }

        public AllOutputData CreateFiles(string outputRoot,
            List<string> foldersToProcess,
            bool sameFileName,
            bool sameFolderStructure,
            bool createJsonFile = true)
        {
            AllStats allStats = GetAllStats();
            PlayerPositionsStats playerPositionsStats = null;

            if (dp.ParsePlayerPositions && CheckIfStatsShouldBeCreated(
                "playerPositionsStats",
                demoInfo.GameMode
            ))
            {
                playerPositionsStats = GetPlayerPositionsStats(processedData, allStats);
            }

            if (createJsonFile)
            {
                string path = GetOutputPathWithoutExtension(
                    outputRoot,
                    foldersToProcess,
                    demoInfo,
                    allStats.mapInfo.MapName,
                    sameFileName,
                    sameFolderStructure
                );

                WriteJson(allStats, path + ".json");

                if (playerPositionsStats is not null)
                    WriteJson(playerPositionsStats, path + "_playerpositions.json");
            }

            // return for testing purposes
            return new AllOutputData
            {
                AllStats = allStats,
                PlayerPositionsStats = playerPositionsStats,
            };
        }

        public DataAndPlayerNames GetDataAndPlayerNames(ProcessedData processedData)
        {
            var data = new Dictionary<long, Dictionary<string, long>>();
            var playerNames = new Dictionary<long, Dictionary<string, string>>();

            foreach (string catagory in processedData.PlayerValues.Keys)
            {
                foreach (Player p in processedData.PlayerValues[catagory])
                {
                    //Skip players not in this category
                    if (p == null)
                        continue;

                    // checks for an updated userID for the user, loops in case it has changed more than once
                    int userId = p.UserID;

                    while (CheckForUpdatedUserId(userId) != userId)
                        userId = CheckForUpdatedUserId(userId);

                    if (!playerLookups.ContainsKey(userId))
                        continue;

                    //Add player to collections list if doesn't exist
                    if (!playerNames.ContainsKey(playerLookups[userId]))
                        playerNames.Add(playerLookups[userId], new Dictionary<string, string>());

                    if (!data.ContainsKey(playerLookups[userId]))
                        data.Add(playerLookups[userId], new Dictionary<string, long>());

                    //Add category to dictionary if doesn't exist
                    if (!playerNames[playerLookups[userId]].ContainsKey("Name"))
                        playerNames[playerLookups[userId]].Add("Name", p.Name);

                    if (!data[playerLookups[userId]].ContainsKey(catagory))
                        data[playerLookups[userId]].Add(catagory, 0);

                    //Increment it
                    data[playerLookups[userId]][catagory]++;
                }
            }

            return new DataAndPlayerNames
            {
                Data = data,
                PlayerNames = playerNames,
            };
        }

        public static versionNumber GetVersionNumber()
        {
            return new() { Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3) };
        }

        public mapInfo GetMapInfo(ProcessedData processedData, string[] mapNameSplit)
        {
            var mapInfo = new mapInfo
            {
                MapName = demoInfo.MapName,
                TestType = demoInfo.TestType.ToString().ToLower(),
                TestDate = demoInfo.TestDate,
            };

            mapInfo.MapName =
                mapNameSplit.Length > 2
                    ? mapNameSplit[2]
                    : mapInfo.MapName; // use the map name from inside the demo itself if possible, otherwise use the map name from the demo file's name

            mapInfo.WorkshopID = mapNameSplit.Length > 2 ? mapNameSplit[1] : "unknown";
            mapInfo.DemoName =
                demoInfo.DemoName.Split('\\').Last()
                    .Replace(
                        ".dem",
                        string.Empty
                    ); // the filename of the demo, for Faceit games this is also in the "demo_url" value

            // attempts to get the game mode
            GetRoundsWonReasons(processedData.RoundEndReasonValues);

            // use the provided game mode if given as a parameter
            if (demoInfo.GameMode is not GameMode.Unknown)
            {
                mapInfo.GameMode = demoInfo.GameMode.ToString().ToLower();

                return mapInfo;
            }

            // work out the game mode if it wasn't provided as a parameter
            if (processedData.TeamPlayersValues.Any(
                    t => t.Terrorists.Count > 10
                        && processedData.TeamPlayersValues.Any(ct => ct.CounterTerrorists.Count == 0)
                ) || // assume danger zone if more than 10 Terrorists and 0 CounterTerrorists
                dp.hostageAIndex > -1 && dp.hostageBIndex > -1
                && !processedData.MatchStartValues.Any(
                    m => m.HasBombsites
                ) // assume danger zone if more than one hostage rescue zone
            )
            {
                mapInfo.GameMode = nameof(GameMode.DangerZone).ToLower();
            }
            else if (processedData.TeamPlayersValues.Any(
                t => t.Terrorists.Count > 2 && processedData.TeamPlayersValues.Any(ct => ct.CounterTerrorists.Count > 2)
            ))
            {
                if (dp.bombsiteAIndex > -1 || dp.bombsiteBIndex > -1
                    || processedData.MatchStartValues.Any(m => m.HasBombsites))
                    mapInfo.GameMode = nameof(GameMode.Defuse).ToLower();
                else if ((dp.hostageAIndex > -1 || dp.hostageBIndex > -1)
                    && !processedData.MatchStartValues.Any(m => m.HasBombsites))
                    mapInfo.GameMode = nameof(GameMode.Hostage).ToLower();
                else // what the hell is this game mode ??
                    mapInfo.GameMode = nameof(GameMode.Unknown).ToLower();
            }
            else
            {
                if (dp.bombsiteAIndex > -1 || dp.bombsiteBIndex > -1
                    || processedData.MatchStartValues.Any(m => m.HasBombsites))
                    mapInfo.GameMode = nameof(GameMode.WingmanDefuse).ToLower();
                else if ((dp.hostageAIndex > -1 || dp.hostageBIndex > -1)
                    && !processedData.MatchStartValues.Any(m => m.HasBombsites))
                    mapInfo.GameMode = nameof(GameMode.WingmanHostage).ToLower();
                else // what the hell is this game mode ??
                    mapInfo.GameMode = nameof(GameMode.Unknown).ToLower();
            }

            return mapInfo;
        }

        public List<playerStats> GetPlayerStats(
            ProcessedData processedData,
            Dictionary<long, Dictionary<string, long>> data,
            Dictionary<long, Dictionary<string, string>> playerNames)
        {
            var playerStats = new List<playerStats>();

            // remove team kills and suicides from kills (easy messy implementation)
            foreach (PlayerKilledEventArgs kill in processedData.PlayerKilledEventsValues)
            {
                if (kill.Killer != null && kill.Killer.Name != "unconnected")
                {
                    // checks for an updated userID for the user, loops in case it has changed more than once
                    int userId = kill.Killer.UserID;

                    while (CheckForUpdatedUserId(userId) != userId)
                        userId = CheckForUpdatedUserId(userId);

                    if (kill.Suicide)
                        data[playerLookups[userId]]["Kills"] -= 1;
                    else if (kill.TeamKill)
                        data[playerLookups[userId]]["Kills"] -= 2;
                }
            }

            foreach (long player in data.Keys)
            {
                IEnumerable<KeyValuePair<long, Dictionary<string, string>>> match =
                    playerNames.Where(p => p.Key.ToString() == player.ToString());

                var playerName = match.ElementAt(0).Value.ElementAt(0).Value;
                var steamID = match.ElementAt(0).Key;

                var statsList1 = new List<int>();

                foreach (string catagory in processedData.PlayerValues.Keys)
                {
                    if (data[player].ContainsKey(catagory))
                        statsList1.Add((int)data[player][catagory]);
                    else
                        statsList1.Add(0);
                }

                var statsList2 = new List<long>();

                if (processedData.WriteTicks)
                    if (playerLookups.Any(p => p.Value == player))
                        foreach (int userid in playerLookups.Keys)
                        {
                            if (playerLookups[userid] == player)
                            {
                                statsList2.Add(playerTicks[userid].ticksAlive);

                                statsList2.Add(playerTicks[userid].ticksOnServer);

                                statsList2.Add(playerTicks[userid].ticksPlaying);

                                break;
                            }
                        }

                int numOfKillsAsBot = processedData.PlayerKilledEventsValues.Count(
                    k => k.Killer != null && k.Killer.Name.ToString() == playerName && k.KillerBotTakeover
                );

                int numOfDeathsAsBot = processedData.PlayerKilledEventsValues.Count(
                    k => k.Victim != null && k.Victim.Name.ToString() == playerName && k.VictimBotTakeover
                );

                int numOfAssistsAsBot = processedData.PlayerKilledEventsValues.Count(
                    k => k.Assister != null && k.Assister.Name.ToString() == playerName && k.AssisterBotTakeover
                );

                playerStats.Add(
                    new playerStats
                    {
                        PlayerName = playerName,
                        SteamID = steamID,
                        Kills = statsList1.ElementAt(0) - numOfKillsAsBot,
                        KillsIncludingBots = statsList1.ElementAt(0),
                        Deaths = statsList1.ElementAt(1) - numOfDeathsAsBot,
                        DeathsIncludingBots = statsList1.ElementAt(1),
                        Headshots = statsList1.ElementAt(2),
                        Assists = statsList1.ElementAt(3) - numOfAssistsAsBot,
                        AssistsIncludingBots = statsList1.ElementAt(3),
                        MVPs = statsList1.ElementAt(4),
                        Shots = statsList1.ElementAt(5),
                        Plants = statsList1.ElementAt(6),
                        Defuses = statsList1.ElementAt(7),
                        Rescues = statsList1.ElementAt(8),
                        TicksAlive = statsList2.ElementAt(0),
                        TicksOnServer = statsList2.ElementAt(1),
                        TicksPlaying = statsList2.ElementAt(2),
                    }
                );
            }

            return playerStats;
        }

        public GeneralroundsStats GetGeneralRoundsStats(
            ProcessedData processedData,
            Dictionary<long, Dictionary<string, string>> playerNames)
        {
            var roundsStats = new List<roundsStats>();

            // winning team & total rounds stats
            IEnumerable<SwitchSidesEventArgs> switchSides = processedData.SwitchSidesValues;
            List<Team> roundsWonTeams = GetRoundsWonTeams(processedData.TeamValues);
            List<RoundEndReason> roundsWonReasons = GetRoundsWonReasons(processedData.RoundEndReasonValues);
            int totalRoundsWonTeamAlpha = 0, totalRoundsWonTeamBeta = 0;

            for (int i = 0; i < roundsWonTeams.Count; i++)
            {
                if (roundsWonReasons.Count > i) // game was abandoned early
                {
                    string reason = string.Empty;
                    string half;
                    bool isOvertime = switchSides.Count() >= 2 && i >= switchSides.ElementAt(1).RoundBeforeSwitch;

                    int overtimeCount = 0;
                    double roundLength = processedData.RoundLengthValues.ElementAt(i);

                    // determines which half / side it is
                    if (isOvertime)
                    {
                        int lastNormalTimeRound = switchSides.ElementAt(1).RoundBeforeSwitch;
                        int roundsPerOTHalf = switchSides.Count() >= 3
                            ? switchSides.ElementAt(2).RoundBeforeSwitch - lastNormalTimeRound
                            : 3; // just assume 3 rounds per OT half if it cannot be checked

                        int roundsPerOT = roundsPerOTHalf * 2;

                        int roundsIntoOT = i + 1 - lastNormalTimeRound;
                        overtimeCount = (int)Math.Ceiling((double)roundsIntoOT / roundsPerOT);

                        int currentOTHalf = (int)Math.Ceiling((double)roundsIntoOT / roundsPerOTHalf);
                        half = currentOTHalf % 2 == 1 ? "First" : "Second";
                    }
                    else
                    {
                        half = switchSides.Any()
                            ? i < switchSides.ElementAt(0).RoundBeforeSwitch ? "First" : "Second"
                            : "First";
                    }

                    // total rounds calculation
                    if (GetIfTeamSwapOrdersAreNormalOrderByHalfAndOvertimeCount(half, overtimeCount))
                    {
                        if (roundsWonTeams.ElementAt(i) is Team.Terrorist)
                            totalRoundsWonTeamAlpha++;
                        else if (roundsWonTeams.ElementAt(i) is Team.CounterTerrorist)
                            totalRoundsWonTeamBeta++;
                    }
                    else
                    {
                        if (roundsWonTeams.ElementAt(i) is Team.Terrorist)
                            totalRoundsWonTeamBeta++;
                        else if (roundsWonTeams.ElementAt(i) is Team.CounterTerrorist)
                            totalRoundsWonTeamAlpha++;
                    }

                    //win method
                    reason = roundsWonReasons[i] switch
                    {
                        RoundEndReason.TerroristsWin => "T Kills",
                        RoundEndReason.CTsWin => "CT Kills",
                        RoundEndReason.TargetBombed => "Bombed",
                        RoundEndReason.BombDefused => "Defused",
                        RoundEndReason.HostagesRescued => "HostagesRescued",
                        RoundEndReason.HostagesNotRescued => "HostagesNotRescued",
                        RoundEndReason.TargetSaved => "TSaved",
                        RoundEndReason.SurvivalWin => "Danger Zone Won",
                        RoundEndReason.Unknown => "Unknown",
                        _ => reason,
                    };

                    // team count values
                    int roundNum = i + 1;
                    TeamPlayers currentRoundTeams =
                        processedData.TeamPlayersValues.FirstOrDefault(t => t.Round == roundNum);

                    foreach (Player player in currentRoundTeams.Terrorists) // make sure steamID's aren't 0
                    {
                        player.SteamID = player.SteamID == 0
                            ? GetSteamIdByPlayerName(playerNames, player.Name)
                            : player.SteamID;
                    }

                    foreach (Player player in currentRoundTeams.CounterTerrorists) // make sure steamID's aren't 0
                    {
                        player.SteamID = player.SteamID == 0
                            ? GetSteamIdByPlayerName(playerNames, player.Name)
                            : player.SteamID;
                    }

                    int playerCountTeamA = currentRoundTeams != null
                        ? GetIfTeamSwapOrdersAreNormalOrderByHalfAndOvertimeCount(half, overtimeCount)
                            ?
                            currentRoundTeams.Terrorists.Count
                            : currentRoundTeams.CounterTerrorists.Count
                        : 0;

                    int playerCountTeamB = currentRoundTeams != null
                        ? GetIfTeamSwapOrdersAreNormalOrderByHalfAndOvertimeCount(half, overtimeCount)
                            ?
                            currentRoundTeams.CounterTerrorists.Count
                            : currentRoundTeams.Terrorists.Count
                        : 0;

                    // equip values
                    TeamEquipment teamEquipValues = processedData.TeamEquipmentValues.Count() >= i
                        ? processedData.TeamEquipmentValues.ElementAt(i)
                        : null;

                    int equipValueTeamA = teamEquipValues != null
                        ? GetIfTeamSwapOrdersAreNormalOrderByHalfAndOvertimeCount(half, overtimeCount)
                            ?
                            teamEquipValues.TEquipValue
                            : teamEquipValues.CTEquipValue
                        : 0;

                    int equipValueTeamB = teamEquipValues != null
                        ? GetIfTeamSwapOrdersAreNormalOrderByHalfAndOvertimeCount(half, overtimeCount)
                            ?
                            teamEquipValues.CTEquipValue
                            : teamEquipValues.TEquipValue
                        : 0;

                    int expenditureTeamA = teamEquipValues != null
                        ? GetIfTeamSwapOrdersAreNormalOrderByHalfAndOvertimeCount(half, overtimeCount)
                            ?
                            teamEquipValues.TExpenditure
                            : teamEquipValues.CTExpenditure
                        : 0;

                    int expenditureTeamB = teamEquipValues != null
                        ? GetIfTeamSwapOrdersAreNormalOrderByHalfAndOvertimeCount(half, overtimeCount)
                            ?
                            teamEquipValues.CTExpenditure
                            : teamEquipValues.TExpenditure
                        : 0;

                    // bombsite planted/exploded/defused at
                    string bombsite = null;
                    BombPlantedError bombPlantedError = null;

                    BombPlanted bombPlanted =
                        processedData.BombsitePlantValues.FirstOrDefault(p => p.Round == roundNum);

                    BombExploded bombExploded =
                        processedData.BombsiteExplodeValues.FirstOrDefault(p => p.Round == roundNum);

                    BombDefused bombDefused =
                        processedData.BombsiteDefuseValues.FirstOrDefault(p => p.Round == roundNum);

                    if (bombDefused is not null)
                    {
                        bombsite ??= bombDefused.Bombsite is null ? null : bombDefused.Bombsite.ToString();
                    }
                    else if (bombExploded is not null)
                    {
                        bombsite ??= bombExploded.Bombsite is null ? null : bombExploded.Bombsite.ToString();
                    }
                    else if (bombPlanted is not null)
                    {
                        bombsite = bombPlanted.Bombsite.ToString();

                        //check to see if either of the bombsites have bugged out
                        if (bombsite == "?")
                        {
                            bombPlantedError = ValidateBombsite(
                                processedData.BombsitePlantValues,
                                (char)bombPlanted.Bombsite
                            );

                            //update data to ensure that future references to it are also updated
                            processedData.BombsitePlantValues.FirstOrDefault(p => p.Round == roundNum).Bombsite =
                                bombPlantedError.Bombsite;

                            if (processedData.BombsiteExplodeValues.FirstOrDefault(p => p.Round == roundNum) != null)
                                processedData.BombsiteExplodeValues.FirstOrDefault(p => p.Round == roundNum).Bombsite =
                                    bombPlantedError.Bombsite;

                            if (processedData.BombsiteDefuseValues.FirstOrDefault(p => p.Round == roundNum) != null)
                                processedData.BombsiteDefuseValues.FirstOrDefault(p => p.Round == roundNum).Bombsite =
                                    bombPlantedError.Bombsite;

                            bombsite = bombPlantedError.Bombsite.ToString();
                        }

                        //plant position
                        bombPlanted.XPosition = bombPlanted.Player.LastAlivePosition.X;
                        bombPlanted.YPosition = bombPlanted.Player.LastAlivePosition.Y;
                        bombPlanted.ZPosition = bombPlanted.Player.LastAlivePosition.Z;
                    }

                    var timeInRoundPlanted = bombPlanted?.TimeInRound;
                    var timeInRoundExploded = bombExploded?.TimeInRound;
                    var timeInRoundDefused = bombDefused?.TimeInRound;

                    // hostage picked up/rescued
                    HostagePickedUp hostagePickedUpA = null, hostagePickedUpB = null;
                    HostageRescued hostageRescuedA = null, hostageRescuedB = null;
                    HostagePickedUpError hostageAPickedUpError = null, hostageBPickedUpError = null;

                    if (processedData.HostagePickedUpValues.Any(r => r.Round == roundNum)
                        || processedData.HostageRescueValues.Any(r => r.Round == roundNum))
                    {
                        hostagePickedUpA = processedData.HostagePickedUpValues.FirstOrDefault(
                            r => r.Round == roundNum && r.Hostage == 'A'
                        );

                        hostagePickedUpB = processedData.HostagePickedUpValues.FirstOrDefault(
                            r => r.Round == roundNum && r.Hostage == 'B'
                        );

                        hostageRescuedA = processedData.HostageRescueValues.FirstOrDefault(
                            r => r.Round == roundNum && r.Hostage == 'A'
                        );

                        hostageRescuedB = processedData.HostageRescueValues.FirstOrDefault(
                            r => r.Round == roundNum && r.Hostage == 'B'
                        );

                        if (hostagePickedUpA == null && hostageRescuedA != null)
                        {
                            hostagePickedUpA = GenerateNewHostagePickedUp(hostageRescuedA);

                            hostageAPickedUpError = new HostagePickedUpError
                            {
                                Hostage = hostagePickedUpA.Hostage,
                                HostageIndex = hostagePickedUpA.HostageIndex,
                                ErrorMessage = "Assuming Hostage A was picked up; cannot assume TimeInRound.",
                            };

                            //update data to ensure that future references to it are also updated
                            List<HostagePickedUp> newHostagePickedUpValues =
                                processedData.HostagePickedUpValues.ToList();

                            newHostagePickedUpValues.Add(hostagePickedUpA);
                            processedData.HostagePickedUpValues = newHostagePickedUpValues;
                        }

                        if (hostagePickedUpB == null && hostageRescuedB != null)
                        {
                            hostagePickedUpB = GenerateNewHostagePickedUp(hostageRescuedB);

                            hostageBPickedUpError = new HostagePickedUpError
                            {
                                Hostage = hostagePickedUpB.Hostage,
                                HostageIndex = hostagePickedUpB.HostageIndex,
                                ErrorMessage = "Assuming Hostage B was picked up; cannot assume TimeInRound.",
                            };

                            //update data to ensure that future references to it are also updated
                            List<HostagePickedUp> newHostagePickedUpValues =
                                processedData.HostagePickedUpValues.ToList();

                            newHostagePickedUpValues.Add(hostagePickedUpB);
                            processedData.HostagePickedUpValues = newHostagePickedUpValues;
                        }

                        //rescue position
                        Vector positionRescueA = hostageRescuedA?.Player.LastAlivePosition;
                        if (positionRescueA != null)
                        {
                            hostageRescuedA.XPosition = positionRescueA.X;
                            hostageRescuedA.YPosition = positionRescueA.Y;
                            hostageRescuedA.ZPosition = positionRescueA.Z;
                        }

                        Vector positionRescueB = hostageRescuedB?.Player.LastAlivePosition;
                        if (positionRescueB != null)
                        {
                            hostageRescuedB.XPosition = positionRescueB.X;
                            hostageRescuedB.YPosition = positionRescueB.Y;
                            hostageRescuedB.ZPosition = positionRescueB.Z;
                        }
                    }

                    var timeInRoundRescuedHostageA = hostageRescuedA?.TimeInRound;
                    var timeInRoundRescuedHostageB = hostageRescuedB?.TimeInRound;

                    roundsStats.Add(
                        new roundsStats
                        {
                            Round = i + 1,
                            Half = half,
                            Overtime = overtimeCount,
                            Length = roundLength,
                            Winners = roundsWonTeams[i].ToString(),
                            WinMethod = reason,
                            BombsitePlantedAt = bombsite,
                            BombPlantPositionX = bombPlanted?.XPosition,
                            BombPlantPositionY = bombPlanted?.YPosition,
                            BombPlantPositionZ = bombPlanted?.ZPosition,
                            BombsiteErrorMessage = bombPlantedError?.ErrorMessage,
                            PickedUpHostageA = hostagePickedUpA != null,
                            PickedUpHostageB = hostagePickedUpB != null,
                            PickedUpAllHostages = hostagePickedUpA != null && hostagePickedUpB != null,
                            HostageAPickedUpErrorMessage = hostageAPickedUpError?.ErrorMessage,
                            HostageBPickedUpErrorMessage = hostageBPickedUpError?.ErrorMessage,
                            RescuedHostageA = hostageRescuedA != null,
                            RescuedHostageB = hostageRescuedB != null,
                            RescuedAllHostages = hostageRescuedA != null && hostageRescuedB != null,
                            RescuedHostageAPositionX = hostageRescuedA?.XPosition,
                            RescuedHostageAPositionY = hostageRescuedA?.YPosition,
                            RescuedHostageAPositionZ = hostageRescuedA?.ZPosition,
                            RescuedHostageBPositionX = hostageRescuedB?.XPosition,
                            RescuedHostageBPositionY = hostageRescuedB?.YPosition,
                            RescuedHostageBPositionZ = hostageRescuedB?.ZPosition,
                            TimeInRoundPlanted = timeInRoundPlanted,
                            TimeInRoundExploded =
                                timeInRoundExploded, // for danger zone, this should be the first bomb that explodes
                            TimeInRoundDefused = timeInRoundDefused,
                            TimeInRoundRescuedHostageA = timeInRoundRescuedHostageA,
                            TimeInRoundRescuedHostageB = timeInRoundRescuedHostageB,
                            TeamAlphaPlayerCount = playerCountTeamA,
                            TeamBetaPlayerCount = playerCountTeamB,
                            TeamAlphaEquipValue = equipValueTeamA,
                            TeamBetaEquipValue = equipValueTeamB,
                            TeamAlphaExpenditure = expenditureTeamA,
                            TeamBetaExpenditure = expenditureTeamB,
                        }
                    );
                }
            }

            // work out winning team
            string winningTeam = totalRoundsWonTeamAlpha >= totalRoundsWonTeamBeta
                ? totalRoundsWonTeamAlpha > totalRoundsWonTeamBeta ? "Team Alpha" : "Draw"
                : "Team Bravo";

            // winners stats
            var winnersStats = new winnersStats
            {
                WinningTeam = winningTeam,
                TeamAlphaRounds = totalRoundsWonTeamAlpha,
                TeamBetaRounds = totalRoundsWonTeamBeta,
            };

            return new GeneralroundsStats
            {
                roundsStats = roundsStats,
                winnersStats = winnersStats,
                SwitchSides = switchSides,
            };
        }

        public static List<bombsiteStats> GetBombsiteStats(ProcessedData processedData)
        {
            BoundingBox bombsiteATrigger = dp?.Triggers.GetValueOrDefault(dp.bombsiteAIndex);
            BoundingBox bombsiteBTrigger = dp?.Triggers.GetValueOrDefault(dp.bombsiteBIndex);

            return new()
            {
                new()
                {
                    Bombsite = 'A',
                    Plants = processedData.BombsitePlantValues.Count(plant => plant.Bombsite == 'A'),
                    Explosions = processedData.BombsiteExplodeValues.Count(explosion => explosion.Bombsite == 'A'),
                    Defuses = processedData.BombsiteDefuseValues.Count(defuse => defuse.Bombsite == 'A'),
                    XPositionMin = bombsiteATrigger?.Min.X,
                    YPositionMin = bombsiteATrigger?.Min.Y,
                    ZPositionMin = bombsiteATrigger?.Min.Z,
                    XPositionMax = bombsiteATrigger?.Max.X,
                    YPositionMax = bombsiteATrigger?.Max.Y,
                    ZPositionMax = bombsiteATrigger?.Max.Z,
                },
                new()
                {
                    Bombsite = 'B',
                    Plants = processedData.BombsitePlantValues.Count(plant => plant.Bombsite == 'B'),
                    Explosions = processedData.BombsiteExplodeValues.Count(explosion => explosion.Bombsite == 'B'),
                    Defuses = processedData.BombsiteDefuseValues.Count(defuse => defuse.Bombsite == 'B'),
                    XPositionMin = bombsiteBTrigger?.Min.X,
                    YPositionMin = bombsiteBTrigger?.Min.Y,
                    ZPositionMin = bombsiteBTrigger?.Min.Z,
                    XPositionMax = bombsiteBTrigger?.Max.X,
                    YPositionMax = bombsiteBTrigger?.Max.Y,
                    ZPositionMax = bombsiteBTrigger?.Max.Z,
                },
            };
        }

        public static List<hostageStats> GetHostageStats(ProcessedData processedData)
        {
            return new()
            {
                new()
                {
                    Hostage = 'A',
                    HostageIndex =
                        processedData.HostageRescueValues.FirstOrDefault(r => r.Hostage == 'A')?.HostageIndex,
                    PickedUps = processedData.HostagePickedUpValues.Count(pickup => pickup.Hostage == 'A'),
                    Rescues = processedData.HostageRescueValues.Count(rescue => rescue.Hostage == 'A'),
                },
                new()
                {
                    Hostage = 'B',
                    HostageIndex =
                        processedData.HostageRescueValues.FirstOrDefault(r => r.Hostage == 'B')?.HostageIndex,
                    PickedUps = processedData.HostagePickedUpValues.Count(pickup => pickup.Hostage == 'B'),
                    Rescues = processedData.HostageRescueValues.Count(rescue => rescue.Hostage == 'B'),
                },
            };
        }

        public static List<rescueZoneStats> GetRescueZoneStats()
        {
            var rescueZoneStats = new List<rescueZoneStats>();

            if (dp is null)
                return rescueZoneStats;

            foreach ((int entityId, BoundingBox rescueZone) in dp.Triggers)
            {
                if (entityId == dp.bombsiteAIndex || entityId == dp.bombsiteBIndex)
                    continue;

                rescueZoneStats.Add(
                    new rescueZoneStats
                    {
                        XPositionMin = rescueZone.Min.X,
                        YPositionMin = rescueZone.Min.Y,
                        ZPositionMin = rescueZone.Min.Z,
                        XPositionMax = rescueZone.Max.X,
                        YPositionMax = rescueZone.Max.Y,
                        ZPositionMax = rescueZone.Max.Z,
                    }
                );
            }

            return rescueZoneStats;
        }

        public static List<grenadesTotalStats> GetGrenadesTotalStats(
            Dictionary<EquipmentElement, List<NadeEventArgs>> nadeGroups)
        {
            var grenadesTotalStats = new List<grenadesTotalStats>(nadeGroups.Count);

            foreach ((EquipmentElement nadeType, List<NadeEventArgs> events) in nadeGroups)
            {
                grenadesTotalStats.Add(
                    new grenadesTotalStats
                    {
                        NadeType = nadeType.ToString(),
                        AmountUsed = events.Count,
                    }
                );
            }

            return grenadesTotalStats;
        }

        public static List<grenadesSpecificStats> GetGrenadesSpecificStats(
            Dictionary<EquipmentElement, List<NadeEventArgs>> nadeGroups,
            Dictionary<long, Dictionary<string, string>> playerNames)
        {
            var grenadesSpecificStats = new List<grenadesSpecificStats>(nadeGroups.Count);

            foreach ((EquipmentElement nadeType, List<NadeEventArgs> events) in nadeGroups)
            {
                foreach (NadeEventArgs nade in events)
                {
                    // Retrieve Steam ID using player name if the event does not return it correctly.
                    long steamId = nade.ThrownBy.SteamID == 0
                        ? GetSteamIdByPlayerName(playerNames, nade.ThrownBy.Name)
                        : nade.ThrownBy.SteamID;

                    var stats = new grenadesSpecificStats
                    {
                        NadeType = nade.NadeType.ToString(),
                        SteamID = steamId,
                        XPosition = nade.Position.X,
                        YPosition = nade.Position.Y,
                        ZPosition = nade.Position.Z,
                    };

                    if (nadeType is EquipmentElement.Flash)
                    {
                        var flash = nade as FlashEventArgs;
                        stats.NumPlayersFlashed = flash.FlashedPlayers.Length;
                    }

                    grenadesSpecificStats.Add(stats);
                }
            }

            return grenadesSpecificStats;
        }

        public static List<killsStats> GetKillsStats(
            ProcessedData processedData,
            Dictionary<long, Dictionary<string, string>> playerNames)
        {
            var killsStats = new List<killsStats>();

            var kills = new List<Player>(processedData.PlayerValues["Kills"].ToList());
            var deaths = new List<Player>(processedData.PlayerValues["Deaths"].ToList());

            var weaponKillers = new List<Equipment>(processedData.WeaponValues.ToList());
            var penetrations = new List<int>(processedData.PenetrationValues.ToList());

            for (int i = 0; i < deaths.Count; i++)
            {
                if (kills.ElementAt(i) != null && kills.ElementAt(i).LastAlivePosition != null
                    && deaths.ElementAt(i) != null && deaths.ElementAt(i).LastAlivePosition != null)
                {
                    PlayerKilledEventArgs playerKilledEvent = processedData.PlayerKilledEventsValues.ElementAt(i);

                    if (playerKilledEvent != null)
                    {
                        int round = playerKilledEvent.Round;

                        Vector killPosition = kills.ElementAt(i).LastAlivePosition;
                        Vector deathPosition= deaths.ElementAt(i).LastAlivePosition;

                        //retrieve steam ID using player name if the event does not return it correctly
                        long killerSteamId = kills.ElementAt(i) != null
                            ? kills.ElementAt(i).SteamID == 0
                                ?
                                GetSteamIdByPlayerName(playerNames, kills.ElementAt(i).Name)
                                : kills.ElementAt(i).SteamID
                            : 0;

                        long victimSteamId = deaths.ElementAt(i) != null
                            ? deaths.ElementAt(i).SteamID == 0
                                ?
                                GetSteamIdByPlayerName(playerNames, deaths.ElementAt(i).Name)
                                : deaths.ElementAt(i).SteamID
                            : 0;

                        long assisterSteamId = playerKilledEvent.Assister != null
                            ? playerKilledEvent.Assister.SteamID == 0
                                ?
                                GetSteamIdByPlayerName(playerNames, playerKilledEvent.Assister.Name)
                                : playerKilledEvent.Assister.SteamID
                            : 0;

                        var weaponUsed = weaponKillers.ElementAt(i).Weapon.ToString();
                        var weaponUsedClass = weaponKillers.ElementAt(i).Class.ToString();
                        var weaponUsedType = weaponKillers.ElementAt(i).SubclassName;
                        var numOfPenetrations = penetrations.ElementAt(i);

                        if (string.IsNullOrEmpty(weaponUsed))
                        {
                            weaponUsed = weaponKillers.ElementAt(i).OriginalString;
                            weaponUsedClass = "Unknown";
                            weaponUsedType = "Unknown";
                        }

                        bool firstKillOfTheRound = !killsStats.Any(k => k.Round == round && k.FirstKillOfTheRound);

                        killsStats.Add(
                            new killsStats
                            {
                                Round = round,
                                TimeInRound = playerKilledEvent.TimeInRound,
                                Weapon = weaponUsed,
                                WeaponClass = weaponUsedClass,
                                WeaponType = weaponUsedType,
                                KillerSteamID = killerSteamId,
                                KillerBotTakeover = playerKilledEvent.KillerBotTakeover,
                                XPositionKill = killPosition.X,
                                YPositionKill = killPosition.Y,
                                ZPositionKill = killPosition.Z,
                                VictimSteamID = victimSteamId,
                                VictimBotTakeover = playerKilledEvent.VictimBotTakeover,
                                XPositionDeath = deathPosition.X,
                                YPositionDeath = deathPosition.Y,
                                ZPositionDeath = deathPosition.Z,
                                AssisterSteamID = assisterSteamId,
                                AssisterBotTakeover = playerKilledEvent.AssisterBotTakeover,
                                FirstKillOfTheRound = firstKillOfTheRound,
                                Suicide = playerKilledEvent.Suicide,
                                TeamKill = playerKilledEvent.TeamKill,
                                PenetrationsCount = numOfPenetrations,
                                Headshot = playerKilledEvent.Headshot,
                                AssistedFlash = playerKilledEvent.AssistedFlash,
                            }
                        );
                    }
                }
            }

            return killsStats;
        }

        public static List<FeedbackMessage> GetFeedbackMessages(
            ProcessedData processedData,
            Dictionary<long, Dictionary<string, string>> playerNames)
        {
            var feedbackMessages = new List<FeedbackMessage>();

            foreach (FeedbackMessage message in processedData.MessagesValues)
            {
                TeamPlayers currentRoundTeams =
                    processedData.TeamPlayersValues.FirstOrDefault(t => t.Round == message.Round);

                if (currentRoundTeams != null && (message.SteamID == 0 || message.TeamName == null)
                ) // excludes warmup round
                {
                    // retrieve steam ID using player name if the event does not return it correctly
                    foreach (Player player in currentRoundTeams.Terrorists)
                    {
                        player.SteamID = player.SteamID == 0
                            ? GetSteamIdByPlayerName(playerNames, player.Name)
                            : player.SteamID;
                    }

                    foreach (Player player in currentRoundTeams.CounterTerrorists)
                    {
                        player.SteamID = player.SteamID == 0
                            ? GetSteamIdByPlayerName(playerNames, player.Name)
                            : player.SteamID;
                    }

                    if (currentRoundTeams.Terrorists.Any(p => p.SteamID == message.SteamID))
                        message.TeamName = "Terrorist";
                    else if (currentRoundTeams.CounterTerrorists.Any(p => p.SteamID == message.SteamID))
                        message.TeamName = "CounterTerrorist";
                    else
                        message.TeamName = "Spectator";
                }

                feedbackMessages.Add(message);
            }

            return feedbackMessages;
        }

        public static chickenStats GetChickenStats(ProcessedData processedData)
        {
            return new() { Killed = processedData.ChickenValues.Count() };
        }

        public List<teamStats> GetTeamStats(
            ProcessedData processedData,
            AllStats allStats,
            Dictionary<long, Dictionary<string, string>> playerNames,
            IEnumerable<SwitchSidesEventArgs> switchSides)
        {
            var teamStats = new List<teamStats>();

            int swappedSidesCount = 0;
            int currentRoundChecking = 1;

            foreach (TeamPlayers teamPlayers in processedData.TeamPlayersValues)
            {
                // players in each team per round
                swappedSidesCount = switchSides.Count() > swappedSidesCount
                    ? switchSides.ElementAt(swappedSidesCount).RoundBeforeSwitch == currentRoundChecking - 1
                        ?
                        swappedSidesCount + 1
                        : swappedSidesCount
                    : swappedSidesCount;

                bool firstHalf = swappedSidesCount % 2 == 0;

                TeamPlayers currentRoundTeams =
                    processedData.TeamPlayersValues.FirstOrDefault(t => t.Round == teamPlayers.Round);

                List<Player> alphaPlayers = currentRoundTeams != null
                    ? firstHalf ? currentRoundTeams.Terrorists : currentRoundTeams.CounterTerrorists
                    : null;

                List<Player> bravoPlayers = currentRoundTeams != null
                    ? firstHalf ? currentRoundTeams.CounterTerrorists : currentRoundTeams.Terrorists
                    : null;

                var alphaSteamIds = new List<long>();
                var bravoSteamIds = new List<long>();

                foreach (Player player in alphaPlayers)
                {
                    player.SteamID = player.SteamID == 0
                        ? GetSteamIdByPlayerName(playerNames, player.Name)
                        : player.SteamID;

                    alphaSteamIds.Add(player.SteamID);
                }

                foreach (Player player in bravoPlayers)
                {
                    player.SteamID = player.SteamID == 0
                        ? GetSteamIdByPlayerName(playerNames, player.Name)
                        : player.SteamID;

                    bravoSteamIds.Add(player.SteamID);
                }

                // attempts to remove and stray players that are supposedly on a team, even though they exceed the max players per team and they are not in player lookups
                // (also most likely have a steam ID of 0)
                var alphaSteamIdsToRemove = new List<long>();
                var bravoSteamIdsToRemove = new List<long>();

                if (allStats.mapInfo.TestType.ToLower().Contains("comp") && alphaSteamIds.Count > 5)
                    foreach (var steamId in alphaSteamIds)
                    {
                        if (playerLookups.All(l => l.Value != steamId))
                            alphaSteamIdsToRemove.Add(steamId);
                    }
                else if (allStats.mapInfo.TestType.ToLower().Contains("casual") && alphaSteamIds.Count > 10)
                    foreach (var steamId in alphaSteamIds)
                    {
                        if (playerLookups.All(l => l.Value != steamId))
                            alphaSteamIdsToRemove.Add(steamId);
                    }

                if (allStats.mapInfo.TestType.ToLower().Contains("comp") && bravoSteamIds.Count > 5)
                    foreach (var steamId in bravoSteamIds)
                    {
                        if (playerLookups.All(l => l.Value != steamId))
                            bravoSteamIdsToRemove.Add(steamId);
                    }
                else if (allStats.mapInfo.TestType.ToLower().Contains("casual") && bravoSteamIds.Count > 10)
                    foreach (var steamId in bravoSteamIds)
                    {
                        if (playerLookups.All(l => l.Value != steamId))
                            bravoSteamIdsToRemove.Add(steamId);
                    }

                // remove the steamIDs if necessary
                foreach (var steamId in alphaSteamIdsToRemove)
                    alphaSteamIds.Remove(steamId);

                foreach (var steamId in bravoSteamIdsToRemove)
                    bravoSteamIds.Remove(steamId);

                // kills/death stats this round
                IEnumerable<PlayerKilledEventArgs> deathsThisRound =
                    processedData.PlayerKilledEventsValues.Where(k => k.Round == teamPlayers.Round);

                // kills this round
                int alphaKills =
                    deathsThisRound.Count(d => d.Killer != null && alphaSteamIds.Contains(d.Killer.SteamID));

                int bravoKills =
                    deathsThisRound.Count(d => d.Killer != null && bravoSteamIds.Contains(d.Killer.SteamID));

                // deaths this round
                int alphaDeaths =
                    deathsThisRound.Count(d => d.Victim != null && alphaSteamIds.Contains(d.Victim.SteamID));

                int bravoDeaths =
                    deathsThisRound.Count(d => d.Victim != null && bravoSteamIds.Contains(d.Victim.SteamID));

                // assists this round
                int alphaAssists =
                    deathsThisRound.Count(d => d.Assister != null && alphaSteamIds.Contains(d.Assister.SteamID));

                int bravoAssists =
                    deathsThisRound.Count(d => d.Assister != null && bravoSteamIds.Contains(d.Assister.SteamID));

                // flash assists this round
                int alphaFlashAssists = deathsThisRound.Count(
                    d => d.Assister != null && alphaSteamIds.Contains(d.Assister.SteamID) && d.AssistedFlash
                );

                int bravoFlashAssists = deathsThisRound.Count(
                    d => d.Assister != null && bravoSteamIds.Contains(d.Assister.SteamID) && d.AssistedFlash
                );

                // headshots this round
                int alphaHeadshots = deathsThisRound.Count(
                    d => d.Killer != null && alphaSteamIds.Contains(d.Killer.SteamID) && d.Headshot
                );

                int bravoHeadshots = deathsThisRound.Count(
                    d => d.Killer != null && bravoSteamIds.Contains(d.Killer.SteamID) && d.Headshot
                );

                // team kills this round
                int alphaTeamkills = deathsThisRound.Count(
                    d => d.Killer != null && d.Victim != null && alphaSteamIds.Contains(d.Killer.SteamID)
                        && alphaSteamIds.Contains(d.Victim.SteamID) && d.Killer.SteamID != d.Victim.SteamID
                );

                int bravoTeamkills = deathsThisRound.Count(
                    d => d.Killer != null && d.Victim != null && bravoSteamIds.Contains(d.Killer.SteamID)
                        && bravoSteamIds.Contains(d.Victim.SteamID) && d.Killer.SteamID != d.Victim.SteamID
                );

                // suicides this round
                int alphaSuicides = deathsThisRound.Count(
                    d => d.Killer != null && d.Victim != null && alphaSteamIds.Contains(d.Killer.SteamID)
                        && d.Killer.SteamID != 0 && d.Suicide
                );

                int bravoSuicides = deathsThisRound.Count(
                    d => d.Killer != null && d.Victim != null && bravoSteamIds.Contains(d.Killer.SteamID)
                        && d.Killer.SteamID != 0 && d.Suicide
                );

                // wallbang kills this round
                int alphaWallbangKills = deathsThisRound.Count(
                    d => d.Killer != null && alphaSteamIds.Contains(d.Killer.SteamID) && d.PenetratedObjects > 0
                );

                int bravoWallbangKills = deathsThisRound.Count(
                    d => d.Killer != null && bravoSteamIds.Contains(d.Killer.SteamID) && d.PenetratedObjects > 0
                );

                // total number of walls penetrated through for kills this round
                int alphaWallbangsTotalForAllKills = deathsThisRound
                    .Where(d => d.Killer != null && alphaSteamIds.Contains(d.Killer.SteamID))
                    .Select(d => d.PenetratedObjects).DefaultIfEmpty().Sum();

                int bravoWallbangsTotalForAllKills = deathsThisRound
                    .Where(d => d.Killer != null && bravoSteamIds.Contains(d.Killer.SteamID))
                    .Select(d => d.PenetratedObjects).DefaultIfEmpty().Sum();

                // most number of walls penetrated through in a single kill this round
                int alphaWallbangsMostInOneKill = deathsThisRound
                    .Where(d => d.Killer != null && alphaSteamIds.Contains(d.Killer.SteamID))
                    .Select(d => d.PenetratedObjects).DefaultIfEmpty().Max();

                int bravoWallbangsMostInOneKill = deathsThisRound
                    .Where(d => d.Killer != null && bravoSteamIds.Contains(d.Killer.SteamID))
                    .Select(d => d.PenetratedObjects).DefaultIfEmpty().Max();

                // shots fired this round
                IEnumerable<ShotFired> shotsFiredThisRound =
                    processedData.ShotsFiredValues.Where(s => s.Round == teamPlayers.Round);

                int alphaShotsFired =
                    shotsFiredThisRound.Count(s => s.Shooter != null && alphaSteamIds.Contains(s.Shooter.SteamID));

                int bravoShotsFired =
                    shotsFiredThisRound.Count(s => s.Shooter != null && bravoSteamIds.Contains(s.Shooter.SteamID));

                teamStats.Add(
                    new teamStats
                    {
                        Round = teamPlayers.Round,
                        TeamAlpha = alphaSteamIds,
                        TeamAlphaKills = alphaKills - (alphaTeamkills + alphaSuicides),
                        TeamAlphaDeaths = alphaDeaths,
                        TeamAlphaAssists = alphaAssists,
                        TeamAlphaFlashAssists = alphaFlashAssists,
                        TeamAlphaHeadshots = alphaHeadshots,
                        TeamAlphaTeamkills = alphaTeamkills,
                        TeamAlphaSuicides = alphaSuicides,
                        TeamAlphaWallbangKills = alphaWallbangKills,
                        TeamAlphaWallbangsTotalForAllKills = alphaWallbangsTotalForAllKills,
                        TeamAlphaWallbangsMostInOneKill = alphaWallbangsMostInOneKill,
                        TeamAlphaShotsFired = alphaShotsFired,
                        TeamBravo = bravoSteamIds,
                        TeamBravoKills = bravoKills - (bravoTeamkills + bravoSuicides),
                        TeamBravoDeaths = bravoDeaths,
                        TeamBravoAssists = bravoAssists,
                        TeamBravoFlashAssists = bravoFlashAssists,
                        TeamBravoHeadshots = bravoHeadshots,
                        TeamBravoTeamkills = bravoTeamkills,
                        TeamBravoSuicides = bravoSuicides,
                        TeamBravoWallbangKills = bravoWallbangKills,
                        TeamBravoWallbangsTotalForAllKills = bravoWallbangsTotalForAllKills,
                        TeamBravoWallbangsMostInOneKill = bravoWallbangsMostInOneKill,
                        TeamBravoShotsFired = bravoShotsFired,
                    }
                );

                currentRoundChecking++;
            }

            return teamStats;
        }

        public static List<firstDamageStats> GetFirstDamageStats(ProcessedData processedData)
        {
            var firstDamageStats = new List<firstDamageStats>();

            foreach (var round in processedData.PlayerHurtValues.Select(x => x.Round).Distinct())
            {
                firstDamageStats.Add(
                    new firstDamageStats
                    {
                        Round = round,
                        FirstDamageToEnemyByPlayers = new List<DamageGivenByPlayerInRound>(),
                    }
                );
            }

            foreach (IGrouping<int, PlayerHurt> roundsGroup in processedData.PlayerHurtValues.GroupBy(x => x.Round))
            {
                int lastRound = processedData.RoundEndReasonValues.Count();

                foreach (var round in roundsGroup.Where(x => x.Round > 0 && x.Round <= lastRound).Select(x => x.Round)
                    .Distinct())
                {
                    foreach (IGrouping<long, PlayerHurt> steamIdsGroup in roundsGroup.Where(
                        x => x.Round == round && x.Player?.SteamID != 0 && x.Player?.SteamID != x.Attacker?.SteamID
                            && x.Weapon.Class != EquipmentClass.Grenade && x.Weapon.Class != EquipmentClass.Equipment
                            && x.Weapon.Class != EquipmentClass.Unknown && x.Weapon.Weapon != EquipmentElement.Unknown
                            && x.Weapon.Weapon != EquipmentElement.Bomb && x.Weapon.Weapon != EquipmentElement.World
                    ).OrderBy(x => x.TimeInRound).GroupBy(x => x.Attacker.SteamID))
                    {
                        PlayerHurt firstDamage = steamIdsGroup.FirstOrDefault();

                        var firstDamageByPlayer = new DamageGivenByPlayerInRound
                        {
                            TimeInRound = firstDamage.TimeInRound,
                            TeamSideShooter = firstDamage.Attacker.Team.ToString(),
                            SteamIDShooter = firstDamage.Attacker.SteamID,
                            XPositionShooter = firstDamage.XPositionAttacker,
                            YPositionShooter = firstDamage.YPositionAttacker,
                            ZPositionShooter = firstDamage.ZPositionAttacker,
                            TeamSideVictim = firstDamage.Player.Team.ToString(),
                            SteamIDVictim = firstDamage.Player.SteamID,
                            XPositionVictim = firstDamage.XPositionPlayer,
                            YPositionVictim = firstDamage.YPositionPlayer,
                            ZPositionVictim = firstDamage.ZPositionPlayer,
                            Weapon = firstDamage.Weapon.Weapon.ToString(),
                            WeaponClass = firstDamage.Weapon.Class.ToString(),
                            WeaponType = firstDamage.Weapon.SubclassName,
                        };

                        firstDamageStats[round - 1].FirstDamageToEnemyByPlayers.Add(firstDamageByPlayer);
                    }
                }
            }

            return firstDamageStats;
        }

        public static PlayerPositionsStats GetPlayerPositionsStats(ProcessedData processedData, AllStats allStats)
        {
            var playerPositionByRound = new List<PlayerPositionByRound>();

            // create playerPositionByRound with empty PlayerPositionByTimeInRound
            foreach (IGrouping<int, PlayerPositionsInstance> roundsGroup in processedData.PlayerPositionsValues.GroupBy(
                x => x.Round
            ))
            {
                int lastRound = processedData.RoundEndReasonValues.Count();

                foreach (var round in roundsGroup.Where(x => x.Round > 0 && x.Round <= lastRound).Select(x => x.Round)
                    .Distinct())
                {
                    playerPositionByRound.Add(
                        new PlayerPositionByRound
                        {
                            Round = round,
                            PlayerPositionByTimeInRound = new List<PlayerPositionByTimeInRound>(),
                        }
                    );
                }
            }

            //create PlayerPositionByTimeInRound with empty PlayerPositionBySteamId
            foreach (PlayerPositionByRound playerPositionsStat in playerPositionByRound)
            {
                foreach (IGrouping<int, PlayerPositionsInstance> timeInRoundsGroup in processedData
                    .PlayerPositionsValues.Where(x => x.Round == playerPositionsStat.Round).GroupBy(x => x.TimeInRound))
                {
                    foreach (var timeInRound in timeInRoundsGroup.Select(x => x.TimeInRound).Distinct())
                    {
                        playerPositionsStat.PlayerPositionByTimeInRound.Add(
                            new PlayerPositionByTimeInRound
                            {
                                TimeInRound = timeInRound,
                                PlayerPositionBySteamID = new List<PlayerPositionBySteamID>(),
                            }
                        );
                    }
                }
            }

            //create PlayerPositionBySteamId
            foreach (PlayerPositionByRound playerPositionsStat in playerPositionByRound)
            {
                foreach (PlayerPositionByTimeInRound playerPositionByTimeInRound in playerPositionsStat
                    .PlayerPositionByTimeInRound)
                {
                    foreach (IGrouping<long, PlayerPositionsInstance> steamIdsGroup in processedData
                        .PlayerPositionsValues
                        .Where(
                            x => x.Round == playerPositionsStat.Round
                                && x.TimeInRound == playerPositionByTimeInRound.TimeInRound
                        ).GroupBy(x => x.SteamID).Distinct())
                    {
                        foreach (PlayerPositionsInstance playerPositionsInstance in steamIdsGroup)
                        {
                            // skip players who have died this round
                            if (!processedData.PlayerKilledEventsValues.Any(
                                x => x.Round == playerPositionsStat.Round && x.Victim?.SteamID != 0
                                    && x.Victim.SteamID == playerPositionsInstance.SteamID
                                    && x.TimeInRound <= playerPositionByTimeInRound.TimeInRound
                            ))
                                playerPositionByTimeInRound.PlayerPositionBySteamID.Add(
                                    new PlayerPositionBySteamID
                                    {
                                        SteamID = playerPositionsInstance.SteamID,
                                        TeamSide = playerPositionsInstance.TeamSide,
                                        XPosition = (int)playerPositionsInstance.XPosition,
                                        YPosition = (int)playerPositionsInstance.YPosition,
                                        ZPosition = (int)playerPositionsInstance.ZPosition,
                                    }
                                );
                        }
                    }
                }
            }

            var playerPositionsStats = new PlayerPositionsStats
            {
                DemoName = allStats.mapInfo.DemoName,
                PlayerPositionByRound = playerPositionByRound,
            };

            return playerPositionsStats;
        }

        public static string GetOutputPathWithoutExtension(
            string outputRoot,
            List<string> foldersToProcess,
            DemoInformation demoInfo,
            string mapName,
            bool sameFileName,
            bool sameFolderStructure)
        {
            string filename = sameFileName
                ? Path.GetFileNameWithoutExtension(demoInfo.DemoName)
                : Guid.NewGuid().ToString();

            string mapDateString = demoInfo.TestDate is null
                ? string.Empty
                : demoInfo.TestDate.Replace('/', '_');

            string path = string.Empty;

            if (foldersToProcess.Count > 0 && sameFolderStructure)
                foreach (var folder in foldersToProcess)
                {
                    string[] splitPath = Path.GetDirectoryName(demoInfo.DemoName).Split(
                        new[] { string.Concat(folder, "\\") },
                        StringSplitOptions.None
                    );

                    path = splitPath.Length > 1
                        ? string.Concat(outputRoot, "\\", splitPath.LastOrDefault(), "\\")
                        : string.Concat(outputRoot, "\\");

                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        if (!Directory.Exists(path))
                            Directory.CreateDirectory(path);

                        break;
                    }
                }
            else
                path = string.Concat(outputRoot, "\\");

            if (mapDateString != string.Empty)
                path += mapDateString + "_";

            path += mapName + "_" + filename;

            return path;
        }

        public static void WriteJson(object stats, string path)
        {
            try
            {
                using var sw = new StreamWriter(path, false);
                string json = JsonConvert.SerializeObject(stats, Formatting.Indented);
                sw.WriteLine(json);
            }
            catch (Exception)
            {
                Console.WriteLine("Could not create json file.");
                Console.WriteLine(string.Concat("Filename: ", path));
            }
        }

        public static long GetSteamIdByPlayerName(Dictionary<long, Dictionary<string, string>> playerNames, string name)
        {
            if (name == "unconnected") return 0;

            var steamId = playerNames.Where(p => p.Value.Values.ElementAt(0) == name).Select(p => p.Key)
                .FirstOrDefault(); // steamID will be 0 if not found

            return steamId;
        }

        public static List<Team> GetRoundsWonTeams(IEnumerable<Team> teamValues)
        {
            List<Team> roundsWonTeams = teamValues.ToList();
            roundsWonTeams.RemoveAll(
                team => team is not Team.Terrorist && team is not Team.CounterTerrorist && team is not Team.Unknown
            );

            return roundsWonTeams;
        }

        public static List<RoundEndReason> GetRoundsWonReasons(IEnumerable<RoundEndReason> roundEndReasonValues)
        {
            List<RoundEndReason> roundsWonReasons = roundEndReasonValues.ToList();
            roundsWonReasons.RemoveAll(
                reason => reason is not RoundEndReason.TerroristsWin && reason is not RoundEndReason.CTsWin
                    && reason is not RoundEndReason.TargetBombed && reason is not RoundEndReason.BombDefused
                    && reason is not RoundEndReason.HostagesRescued && reason is not RoundEndReason.HostagesNotRescued
                    && reason is not RoundEndReason.TargetSaved && reason is not RoundEndReason.SurvivalWin
                    && reason is not RoundEndReason.Unknown
            );

            return roundsWonReasons;
        }

        public static int GetCurrentRoundNum(MatchData md, GameMode gameMode)
        {
            List<TeamPlayers> teamPlayersList = md.processedData.TeamPlayersValues;
            int round = 0;

            if (teamPlayersList.Count > 0 && teamPlayersList.Any(t => t.Round == 1))
            {
                TeamPlayers teamPlayers = teamPlayersList.First(t => t.Round == 1);

                if (teamPlayers.Terrorists.Count > 0 && teamPlayers.CounterTerrorists.Count > 0)
                    round = md.roundOfficiallyEndedCount + 1;
            }

            // add 1 for roundsCount when in danger zone
            if (gameMode is GameMode.DangerZone)
                round++;

            return round;
        }

        public static bool CheckIfPlayerAliveAtThisPointInRound(MatchData md, Player player, int round)
        {
            return !md.processedData.PlayerKilledEventsValues.Any(
                e => e.Round == round && e.Victim?.SteamID != 0 && e.Victim.SteamID == player?.SteamID
            );
        }

        public int CheckForUpdatedUserId(int userId)
        {
            int newUserId = playerReplacements.Where(u => u.Key == userId).Select(u => u.Value).FirstOrDefault();

            return newUserId == 0 ? userId : newUserId;
        }

        public static string GenerateSetPosCommand(Player player)
        {
            if (player is null)
                return "";

            // Z axis for setang is optional.
            return $"setpos {player.Position.X} {player.Position.Y} {player.Position.Z}; "
                + $"setang {player.ViewDirectionX} {player.ViewDirectionY}";
        }

        public static bool IsMessageFeedback(string text)
        {
            return text.ToLower().StartsWith(">fb") || text.ToLower().StartsWith(">feedback")
                || text.ToLower().StartsWith("!fb") || text.ToLower().StartsWith("!feedback");
        }

        public BombPlantedError ValidateBombsite(IEnumerable<BombPlanted> bombPlantedArray, char bombsite)
        {
            char validatedBombsite = bombsite;
            string errorMessage = null;

            if (bombsite == '?')
            {
                if (bombPlantedArray.Any(x => x.Bombsite == 'A')
                    && (!bombPlantedArray.Any(x => x.Bombsite == 'B') || changingPlantedRoundsToB))
                {
                    //assume B site trigger's bounding box is broken
                    changingPlantedRoundsToB = true;
                    validatedBombsite = 'B';
                    errorMessage = "Assuming plant was at B site.";
                }
                else if (!bombPlantedArray.Any(x => x.Bombsite == 'A')
                    && (bombPlantedArray.Any(x => x.Bombsite == 'B') || changingPlantedRoundsToA))
                {
                    //assume A site trigger's bounding box is broken
                    changingPlantedRoundsToA = true;
                    validatedBombsite = 'A';
                    errorMessage = "Assuming plant was at A site.";
                }
                else
                {
                    //both bombsites are having issues
                    //may be an issue with instances?
                    errorMessage = "Couldn't assume either bombsite was the plant location.";
                }
            }

            return new BombPlantedError
            {
                Bombsite = validatedBombsite,
                ErrorMessage = errorMessage,
            };
        }

        public static HostagePickedUp GenerateNewHostagePickedUp(HostageRescued hostageRescued)
        {
            return new()
            {
                Hostage = hostageRescued.Hostage,
                HostageIndex = hostageRescued.HostageIndex,
                Player = new Player(hostageRescued.Player),
                Round = hostageRescued.Round,
                TimeInRound = -1,
            };
        }

        public static bool GetIfTeamSwapOrdersAreNormalOrderByHalfAndOvertimeCount(string half, int overtimeCount)
        {
            return half == "First" && overtimeCount % 2 == 0
                || half == "Second"
                && overtimeCount % 2
                == 1; // the team playing T Side first switches each OT for example, this checks the OT count for swaps
        }

        public static int? GetMinRoundsForWin(GameMode gameMode, TestType testType)
        {
            switch (gameMode, testType)
            {
                case (GameMode.WingmanDefuse, TestType.Casual):
                case (GameMode.WingmanDefuse, TestType.Competitive):
                case (GameMode.WingmanHostage, TestType.Casual):
                case (GameMode.WingmanHostage, TestType.Competitive):
                    return 9;
                case (GameMode.Defuse, TestType.Casual):
                case (GameMode.Hostage, TestType.Casual):
                case (GameMode.Unknown, TestType.Casual):
                    // assumes that it is a classic match. Would be better giving the -gamemodeoverride parameter to get
                    // around this as it cannot figure out the game mode
                    return 11;
                case (GameMode.Defuse, TestType.Competitive):
                case (GameMode.Hostage, TestType.Competitive):
                case (GameMode.Unknown, TestType.Competitive):
                    // assumes that it is a classic match. Would be better giving the -gamemodeoverride parameter to get
                    // around this as it cannot figure out the game mode
                    return 16;
                case (GameMode.DangerZone, TestType.Casual):
                case (GameMode.DangerZone, TestType.Competitive):
                    return 2;
                default:
                    return null;
            }
        }

        private static bool CheckIfStatsShouldBeCreated(string typeName, GameMode gameMode)
        {
            switch (typeName.ToLower())
            {
                case "tanookiStats":
                case "winnersstats":
                case "bombsitestats":
                case "hostagestats":
                case "teamstats":
                    return gameMode is not GameMode.DangerZone;
                default:
                    return true;
            }
        }
    }
}
