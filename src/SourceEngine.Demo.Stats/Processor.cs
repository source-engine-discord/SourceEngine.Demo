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
    public class Processor
    {
        private static DemoParser parser;
        private readonly CollectedData data;
        private readonly DemoInformation demoInfo;

        // Used in ValidateBombsite() for knowing when a bombsite plant site has been changed from '?' to an actual bombsite letter
        public bool changingPlantedRoundsToA, changingPlantedRoundsToB;

        public Processor(DemoParser parser, DemoInformation demoInfo, CollectedData data)
        {
            this.demoInfo = demoInfo;
            Processor.parser = parser;
            this.data = data;
        }

        public AllStats GetAllStats()
        {
            var mapNameSplit = data.MatchStartValues.Any()
                ? data.MatchStartValues.ElementAt(0).Mapname.Split('/')
                : new[] { demoInfo.MapName };

            DataAndPlayerNames dataAndPlayerNames = GetDataAndPlayerNames(data);

            var allStats = new AllStats
            {
                versionNumber = GetVersionNumber(),
                supportedGamemodes = Enum.GetNames(typeof(GameMode)).Select(gm => gm.ToLower()).ToList(),
                mapInfo = GetMapInfo(data, mapNameSplit),
                tanookiStats = data.tanookiStats,
            };

            if (CheckIfStatsShouldBeCreated("playerStats", demoInfo.GameMode))
                allStats.playerStats = GetPlayerStats(
                    data,
                    dataAndPlayerNames.Data,
                    dataAndPlayerNames.PlayerNames
                );

            GeneralroundsStats generalroundsStats = GetGeneralRoundsStats(
                data,
                dataAndPlayerNames.PlayerNames
            );

            if (CheckIfStatsShouldBeCreated("winnersStats", demoInfo.GameMode))
                allStats.winnersStats = generalroundsStats.winnersStats;

            if (CheckIfStatsShouldBeCreated("roundsStats", demoInfo.GameMode))
                allStats.roundsStats = generalroundsStats.roundsStats;

            if (CheckIfStatsShouldBeCreated("bombsiteStats", demoInfo.GameMode))
                allStats.bombsiteStats = GetBombsiteStats(data);

            if (CheckIfStatsShouldBeCreated("hostageStats", demoInfo.GameMode))
                allStats.hostageStats = GetHostageStats(data);

            if (CheckIfStatsShouldBeCreated("rescueZoneStats", demoInfo.GameMode))
                allStats.rescueZoneStats = GetRescueZoneStats();

            Dictionary<EquipmentElement, List<NadeEventArgs>> nadeGroups = data.GrenadeValues
                .Where(e => e.NadeType >= EquipmentElement.Decoy && e.NadeType <= EquipmentElement.HE)
                .GroupBy(e => e.NadeType).ToDictionary(g => g.Key, g => g.ToList());

            if (CheckIfStatsShouldBeCreated("grenadesTotalStats", demoInfo.GameMode))
                allStats.grenadesTotalStats = GetGrenadesTotalStats(nadeGroups);

            if (CheckIfStatsShouldBeCreated("grenadesSpecificStats", demoInfo.GameMode))
                allStats.grenadesSpecificStats = GetGrenadesSpecificStats(nadeGroups, dataAndPlayerNames.PlayerNames);

            if (CheckIfStatsShouldBeCreated("killsStats", demoInfo.GameMode))
                allStats.killsStats = GetKillsStats(data, dataAndPlayerNames.PlayerNames);

            if (CheckIfStatsShouldBeCreated("feedbackMessages", demoInfo.GameMode))
                allStats.feedbackMessages = GetFeedbackMessages(data, dataAndPlayerNames.PlayerNames);

            if (parser.ParseChickens && CheckIfStatsShouldBeCreated(
                "chickenStats",
                demoInfo.GameMode
            ))
                allStats.chickenStats = GetChickenStats(data);

            if (CheckIfStatsShouldBeCreated("teamStats", demoInfo.GameMode))
                allStats.teamStats = GetTeamStats(
                    data,
                    allStats,
                    dataAndPlayerNames.PlayerNames,
                    generalroundsStats.SwitchSides
                );

            if (CheckIfStatsShouldBeCreated("firstDamageStats", demoInfo.GameMode))
                allStats.firstDamageStats = GetFirstDamageStats(data);

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

            if (parser.ParsePlayerPositions && CheckIfStatsShouldBeCreated(
                "playerPositionsStats",
                demoInfo.GameMode
            ))
            {
                playerPositionsStats = GetPlayerPositionsStats(data, allStats);
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

        public DataAndPlayerNames GetDataAndPlayerNames(CollectedData collectedData)
        {
            var data = new Dictionary<long, Dictionary<string, long>>();
            var playerNames = new Dictionary<long, Dictionary<string, string>>();

            foreach (string catagory in collectedData.PlayerValues.Keys)
            {
                foreach (Player p in collectedData.PlayerValues[catagory])
                {
                    //Skip players not in this category
                    if (p == null)
                        continue;

                    // checks for an updated userID for the user, loops in case it has changed more than once
                    int userId = p.UserID;

                    while (CheckForUpdatedUserId(userId) != userId)
                        userId = CheckForUpdatedUserId(userId);

                    if (!collectedData.PlayerLookups.ContainsKey(userId))
                        continue;

                    //Add player to collections list if doesn't exist
                    if (!playerNames.ContainsKey(collectedData.PlayerLookups[userId]))
                        playerNames.Add(collectedData.PlayerLookups[userId], new Dictionary<string, string>());

                    if (!data.ContainsKey(collectedData.PlayerLookups[userId]))
                        data.Add(collectedData.PlayerLookups[userId], new Dictionary<string, long>());

                    //Add category to dictionary if doesn't exist
                    if (!playerNames[collectedData.PlayerLookups[userId]].ContainsKey("Name"))
                        playerNames[collectedData.PlayerLookups[userId]].Add("Name", p.Name);

                    if (!data[collectedData.PlayerLookups[userId]].ContainsKey(catagory))
                        data[collectedData.PlayerLookups[userId]].Add(catagory, 0);

                    //Increment it
                    data[collectedData.PlayerLookups[userId]][catagory]++;
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

        public mapInfo GetMapInfo(CollectedData collectedData, string[] mapNameSplit)
        {
            var mapInfo = new mapInfo
            {
                MapName = demoInfo.MapName,
                TestType = demoInfo.TestType.ToString().ToLower(),
                TestDate = demoInfo.TestDate,
                Crc = collectedData.MapCrc,
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
            GetRoundsWonReasons(collectedData.RoundEndReasonValues);

            // use the provided game mode if given as a parameter
            if (demoInfo.GameMode is not GameMode.Unknown)
            {
                mapInfo.GameMode = demoInfo.GameMode.ToString().ToLower();

                return mapInfo;
            }

            // work out the game mode if it wasn't provided as a parameter
            if (collectedData.TeamPlayersValues.Any(
                    t => t.Terrorists.Count > 10
                        && collectedData.TeamPlayersValues.Any(ct => ct.CounterTerrorists.Count == 0)
                ) || // assume danger zone if more than 10 Terrorists and 0 CounterTerrorists
                parser.hostageAIndex > -1 && parser.hostageBIndex > -1
                && !collectedData.MatchStartValues.Any(
                    m => m.HasBombsites
                ) // assume danger zone if more than one hostage rescue zone
            )
            {
                mapInfo.GameMode = nameof(GameMode.DangerZone).ToLower();
            }
            else if (collectedData.TeamPlayersValues.Any(
                t => t.Terrorists.Count > 2 && collectedData.TeamPlayersValues.Any(ct => ct.CounterTerrorists.Count > 2)
            ))
            {
                if (parser.bombsiteAIndex > -1 || parser.bombsiteBIndex > -1
                    || collectedData.MatchStartValues.Any(m => m.HasBombsites))
                    mapInfo.GameMode = nameof(GameMode.Defuse).ToLower();
                else if ((parser.hostageAIndex > -1 || parser.hostageBIndex > -1)
                    && !collectedData.MatchStartValues.Any(m => m.HasBombsites))
                    mapInfo.GameMode = nameof(GameMode.Hostage).ToLower();
                else // what the hell is this game mode ??
                    mapInfo.GameMode = nameof(GameMode.Unknown).ToLower();
            }
            else
            {
                if (parser.bombsiteAIndex > -1 || parser.bombsiteBIndex > -1
                    || collectedData.MatchStartValues.Any(m => m.HasBombsites))
                    mapInfo.GameMode = nameof(GameMode.WingmanDefuse).ToLower();
                else if ((parser.hostageAIndex > -1 || parser.hostageBIndex > -1)
                    && !collectedData.MatchStartValues.Any(m => m.HasBombsites))
                    mapInfo.GameMode = nameof(GameMode.WingmanHostage).ToLower();
                else // what the hell is this game mode ??
                    mapInfo.GameMode = nameof(GameMode.Unknown).ToLower();
            }

            return mapInfo;
        }

        public List<playerStats> GetPlayerStats(
            CollectedData collectedData,
            Dictionary<long, Dictionary<string, long>> data,
            Dictionary<long, Dictionary<string, string>> playerNames)
        {
            var playerStats = new List<playerStats>();

            // remove team kills and suicides from kills (easy messy implementation)
            foreach (PlayerKilledEventArgs kill in collectedData.PlayerKilledEventsValues)
            {
                if (kill.Killer != null && kill.Killer.Name != "unconnected")
                {
                    // checks for an updated userID for the user, loops in case it has changed more than once
                    int userId = kill.Killer.UserID;

                    while (CheckForUpdatedUserId(userId) != userId)
                        userId = CheckForUpdatedUserId(userId);

                    if (kill.Suicide)
                        data[collectedData.PlayerLookups[userId]]["Kills"] -= 1;
                    else if (kill.TeamKill)
                        data[collectedData.PlayerLookups[userId]]["Kills"] -= 2;
                }
            }

            foreach (long player in data.Keys)
            {
                IEnumerable<KeyValuePair<long, Dictionary<string, string>>> match =
                    playerNames.Where(p => p.Key.ToString() == player.ToString());

                var playerName = match.ElementAt(0).Value.ElementAt(0).Value;
                var steamID = match.ElementAt(0).Key;

                var statsList1 = new List<int>();

                foreach (string catagory in collectedData.PlayerValues.Keys)
                {
                    if (data[player].ContainsKey(catagory))
                        statsList1.Add((int)data[player][catagory]);
                    else
                        statsList1.Add(0);
                }

                var statsList2 = new List<long>();

                if (collectedData.WriteTicks)
                    if (collectedData.PlayerLookups.Any(p => p.Value == player))
                        foreach (int userid in collectedData.PlayerLookups.Keys)
                        {
                            if (collectedData.PlayerLookups[userid] == player)
                            {
                                statsList2.Add(collectedData.PlayerTicks[userid].TicksAlive);

                                statsList2.Add(collectedData.PlayerTicks[userid].TicksOnServer);

                                statsList2.Add(collectedData.PlayerTicks[userid].TicksPlaying);

                                break;
                            }
                        }

                int numOfKillsAsBot = collectedData.PlayerKilledEventsValues.Count(
                    k => k.Killer != null && k.Killer.Name.ToString() == playerName && k.KillerBotTakeover
                );

                int numOfDeathsAsBot = collectedData.PlayerKilledEventsValues.Count(
                    k => k.Victim != null && k.Victim.Name.ToString() == playerName && k.VictimBotTakeover
                );

                int numOfAssistsAsBot = collectedData.PlayerKilledEventsValues.Count(
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
            CollectedData collectedData,
            Dictionary<long, Dictionary<string, string>> playerNames)
        {
            var roundsStats = new List<roundsStats>();

            // winning team & total rounds stats
            IEnumerable<SwitchSidesEventArgs> switchSides = collectedData.SwitchSidesValues;
            List<Team> roundsWonTeams = GetRoundsWonTeams(collectedData.TeamValues);
            List<RoundEndReason> roundsWonReasons = GetRoundsWonReasons(collectedData.RoundEndReasonValues);
            int totalRoundsWonTeamAlpha = 0, totalRoundsWonTeamBeta = 0;

            for (int i = 0; i < roundsWonTeams.Count; i++)
            {
                if (roundsWonReasons.Count > i) // game was abandoned early
                {
                    string reason = string.Empty;
                    string half;
                    bool isOvertime = switchSides.Count() >= 2 && i >= switchSides.ElementAt(1).RoundBeforeSwitch;

                    int overtimeCount = 0;
                    double roundLength = collectedData.RoundLengthValues.ElementAt(i);

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
                        collectedData.TeamPlayersValues.FirstOrDefault(t => t.Round == roundNum);

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
                    TeamEquipment teamEquipValues = collectedData.TeamEquipmentValues.Count() >= i
                        ? collectedData.TeamEquipmentValues.ElementAt(i)
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
                        collectedData.BombsitePlantValues.FirstOrDefault(p => p.Round == roundNum);

                    BombExploded bombExploded =
                        collectedData.BombsiteExplodeValues.FirstOrDefault(p => p.Round == roundNum);

                    BombDefused bombDefused =
                        collectedData.BombsiteDefuseValues.FirstOrDefault(p => p.Round == roundNum);

                    if (bombPlanted is not null)
                    {
                        bombsite = bombPlanted.Bombsite?.ToString();

                        //check to see if either of the bombsites have bugged out
                        if (bombsite == "?")
                        {
                            bombPlantedError = ValidateBombsite(
                                collectedData.BombsitePlantValues,
                                (char)bombPlanted.Bombsite
                            );

                            //update data to ensure that future references to it are also updated
                            bombPlanted.Bombsite = bombPlantedError.Bombsite;

                            if (bombExploded is not null)
                                bombExploded.Bombsite = bombPlantedError.Bombsite;

                            if (bombDefused is not null)
                                bombDefused.Bombsite = bombPlantedError.Bombsite;

                            bombsite = bombPlantedError.Bombsite.ToString();
                        }

                        //plant position
                        bombPlanted.XPosition = bombPlanted.Player.LastAlivePosition.X;
                        bombPlanted.YPosition = bombPlanted.Player.LastAlivePosition.Y;
                        bombPlanted.ZPosition = bombPlanted.Player.LastAlivePosition.Z;
                    }

                    bombsite ??= bombDefused?.Bombsite?.ToString();
                    bombsite ??= bombExploded?.Bombsite?.ToString();

                    var timeInRoundPlanted = bombPlanted?.TimeInRound;
                    var timeInRoundExploded = bombExploded?.TimeInRound;
                    var timeInRoundDefused = bombDefused?.TimeInRound;

                    // hostage picked up/rescued
                    HostagePickedUp hostagePickedUpA = null, hostagePickedUpB = null;
                    HostageRescued hostageRescuedA = null, hostageRescuedB = null;
                    HostagePickedUpError hostageAPickedUpError = null, hostageBPickedUpError = null;

                    if (collectedData.HostagePickedUpValues.Any(r => r.Round == roundNum)
                        || collectedData.HostageRescueValues.Any(r => r.Round == roundNum))
                    {
                        hostagePickedUpA = collectedData.HostagePickedUpValues.FirstOrDefault(
                            r => r.Round == roundNum && r.Hostage == 'A'
                        );

                        hostagePickedUpB = collectedData.HostagePickedUpValues.FirstOrDefault(
                            r => r.Round == roundNum && r.Hostage == 'B'
                        );

                        hostageRescuedA = collectedData.HostageRescueValues.FirstOrDefault(
                            r => r.Round == roundNum && r.Hostage == 'A'
                        );

                        hostageRescuedB = collectedData.HostageRescueValues.FirstOrDefault(
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
                                collectedData.HostagePickedUpValues.ToList();

                            newHostagePickedUpValues.Add(hostagePickedUpA);
                            collectedData.HostagePickedUpValues = newHostagePickedUpValues;
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
                                collectedData.HostagePickedUpValues.ToList();

                            newHostagePickedUpValues.Add(hostagePickedUpB);
                            collectedData.HostagePickedUpValues = newHostagePickedUpValues;
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

        public static List<bombsiteStats> GetBombsiteStats(CollectedData collectedData)
        {
            BoundingBox bombsiteATrigger = parser?.Triggers.GetValueOrDefault(parser.bombsiteAIndex);
            BoundingBox bombsiteBTrigger = parser?.Triggers.GetValueOrDefault(parser.bombsiteBIndex);

            return new()
            {
                new()
                {
                    Bombsite = 'A',
                    Plants = collectedData.BombsitePlantValues.Count(plant => plant.Bombsite == 'A'),
                    Explosions = collectedData.BombsiteExplodeValues.Count(explosion => explosion.Bombsite == 'A'),
                    Defuses = collectedData.BombsiteDefuseValues.Count(defuse => defuse.Bombsite == 'A'),
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
                    Plants = collectedData.BombsitePlantValues.Count(plant => plant.Bombsite == 'B'),
                    Explosions = collectedData.BombsiteExplodeValues.Count(explosion => explosion.Bombsite == 'B'),
                    Defuses = collectedData.BombsiteDefuseValues.Count(defuse => defuse.Bombsite == 'B'),
                    XPositionMin = bombsiteBTrigger?.Min.X,
                    YPositionMin = bombsiteBTrigger?.Min.Y,
                    ZPositionMin = bombsiteBTrigger?.Min.Z,
                    XPositionMax = bombsiteBTrigger?.Max.X,
                    YPositionMax = bombsiteBTrigger?.Max.Y,
                    ZPositionMax = bombsiteBTrigger?.Max.Z,
                },
            };
        }

        public static List<hostageStats> GetHostageStats(CollectedData collectedData)
        {
            return new()
            {
                new()
                {
                    Hostage = 'A',
                    HostageIndex =
                        collectedData.HostageRescueValues.FirstOrDefault(r => r.Hostage == 'A')?.HostageIndex,
                    PickedUps = collectedData.HostagePickedUpValues.Count(pickup => pickup.Hostage == 'A'),
                    Rescues = collectedData.HostageRescueValues.Count(rescue => rescue.Hostage == 'A'),
                },
                new()
                {
                    Hostage = 'B',
                    HostageIndex =
                        collectedData.HostageRescueValues.FirstOrDefault(r => r.Hostage == 'B')?.HostageIndex,
                    PickedUps = collectedData.HostagePickedUpValues.Count(pickup => pickup.Hostage == 'B'),
                    Rescues = collectedData.HostageRescueValues.Count(rescue => rescue.Hostage == 'B'),
                },
            };
        }

        public static List<rescueZoneStats> GetRescueZoneStats()
        {
            var rescueZoneStats = new List<rescueZoneStats>();

            if (parser is null)
                return rescueZoneStats;

            foreach ((int entityId, BoundingBox rescueZone) in parser.Triggers)
            {
                if (entityId == parser.bombsiteAIndex || entityId == parser.bombsiteBIndex)
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
            CollectedData collectedData,
            Dictionary<long, Dictionary<string, string>> playerNames)
        {
            var killsStats = new List<killsStats>();

            var kills = new List<Player>(collectedData.PlayerValues["Kills"].ToList());
            var deaths = new List<Player>(collectedData.PlayerValues["Deaths"].ToList());

            var weaponKillers = new List<Equipment>(collectedData.WeaponValues.ToList());
            var penetrations = new List<int>(collectedData.PenetrationValues.ToList());

            for (int i = 0; i < deaths.Count; i++)
            {
                if (kills.ElementAt(i) != null && kills.ElementAt(i).LastAlivePosition != null
                    && deaths.ElementAt(i) != null && deaths.ElementAt(i).LastAlivePosition != null)
                {
                    PlayerKilledEventArgs playerKilledEvent = collectedData.PlayerKilledEventsValues.ElementAt(i);

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
            CollectedData collectedData,
            Dictionary<long, Dictionary<string, string>> playerNames)
        {
            var feedbackMessages = new List<FeedbackMessage>();

            foreach (FeedbackMessage message in collectedData.MessagesValues)
            {
                TeamPlayers currentRoundTeams =
                    collectedData.TeamPlayersValues.FirstOrDefault(t => t.Round == message.Round);

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

        public static chickenStats GetChickenStats(CollectedData collectedData)
        {
            return new() { Killed = collectedData.ChickenValues.Count() };
        }

        public List<teamStats> GetTeamStats(
            CollectedData collectedData,
            AllStats allStats,
            Dictionary<long, Dictionary<string, string>> playerNames,
            IEnumerable<SwitchSidesEventArgs> switchSides)
        {
            var teamStats = new List<teamStats>();

            int swappedSidesCount = 0;
            int currentRoundChecking = 1;

            foreach (TeamPlayers teamPlayers in collectedData.TeamPlayersValues)
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
                    collectedData.TeamPlayersValues.FirstOrDefault(t => t.Round == teamPlayers.Round);

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
                        if (collectedData.PlayerLookups.All(l => l.Value != steamId))
                            alphaSteamIdsToRemove.Add(steamId);
                    }
                else if (allStats.mapInfo.TestType.ToLower().Contains("casual") && alphaSteamIds.Count > 10)
                    foreach (var steamId in alphaSteamIds)
                    {
                        if (collectedData.PlayerLookups.All(l => l.Value != steamId))
                            alphaSteamIdsToRemove.Add(steamId);
                    }

                if (allStats.mapInfo.TestType.ToLower().Contains("comp") && bravoSteamIds.Count > 5)
                    foreach (var steamId in bravoSteamIds)
                    {
                        if (collectedData.PlayerLookups.All(l => l.Value != steamId))
                            bravoSteamIdsToRemove.Add(steamId);
                    }
                else if (allStats.mapInfo.TestType.ToLower().Contains("casual") && bravoSteamIds.Count > 10)
                    foreach (var steamId in bravoSteamIds)
                    {
                        if (collectedData.PlayerLookups.All(l => l.Value != steamId))
                            bravoSteamIdsToRemove.Add(steamId);
                    }

                // remove the steamIDs if necessary
                foreach (var steamId in alphaSteamIdsToRemove)
                    alphaSteamIds.Remove(steamId);

                foreach (var steamId in bravoSteamIdsToRemove)
                    bravoSteamIds.Remove(steamId);

                // kills/death stats this round
                IEnumerable<PlayerKilledEventArgs> deathsThisRound =
                    collectedData.PlayerKilledEventsValues.Where(k => k.Round == teamPlayers.Round);

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
                    collectedData.ShotsFiredValues.Where(s => s.Round == teamPlayers.Round);

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

        public static List<firstDamageStats> GetFirstDamageStats(CollectedData collectedData)
        {
            var firstDamageStats = new List<firstDamageStats>();

            foreach (var round in collectedData.PlayerHurtValues.Select(x => x.Round).Distinct())
            {
                firstDamageStats.Add(
                    new firstDamageStats
                    {
                        Round = round,
                        FirstDamageToEnemyByPlayers = new List<DamageGivenByPlayerInRound>(),
                    }
                );
            }

            foreach (IGrouping<int, PlayerHurt> roundsGroup in collectedData.PlayerHurtValues.GroupBy(x => x.Round))
            {
                int lastRound = collectedData.RoundEndReasonValues.Count();

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

        public static PlayerPositionsStats GetPlayerPositionsStats(CollectedData collectedData, AllStats allStats)
        {
            var playerPositionByRound = new List<PlayerPositionByRound>();

            // create playerPositionByRound with empty PlayerPositionByTimeInRound
            foreach (IGrouping<int, PlayerPositionsInstance> roundsGroup in collectedData.PlayerPositionsValues.GroupBy(
                x => x.Round
            ))
            {
                int lastRound = collectedData.RoundEndReasonValues.Count();

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
                foreach (IGrouping<int, PlayerPositionsInstance> timeInRoundsGroup in collectedData
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
                    foreach (IGrouping<long, PlayerPositionsInstance> steamIdsGroup in collectedData
                        .PlayerPositionsValues
                        .Where(
                            x => x.Round == playerPositionsStat.Round
                                && x.TimeInRound == playerPositionByTimeInRound.TimeInRound
                        ).GroupBy(x => x.SteamID).Distinct())
                    {
                        foreach (PlayerPositionsInstance playerPositionsInstance in steamIdsGroup)
                        {
                            // skip players who have died this round
                            if (!collectedData.PlayerKilledEventsValues.Any(
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

        public int CheckForUpdatedUserId(int userId)
        {
            int newUserId = data.PlayerReplacements.Where(u => u.Key == userId).Select(u => u.Value).FirstOrDefault();

            return newUserId == 0 ? userId : newUserId;
        }

        public BombPlantedError ValidateBombsite(IEnumerable<BombPlanted> bombPlantedArray, char bombsite)
        {
            char validatedBombsite = bombsite;
            string errorMessage = null;

            if (bombsite == '?')
            {
                bool hasA = bombPlantedArray.Any(x => x.Bombsite == 'A');
                bool hasB = bombPlantedArray.Any(x => x.Bombsite == 'B');

                if (hasA && (!hasB || changingPlantedRoundsToB))
                {
                    //assume B site trigger's bounding box is broken
                    changingPlantedRoundsToB = true;
                    validatedBombsite = 'B';
                    errorMessage = "Assuming plant was at B site.";
                }
                else if (!hasA && (hasB || changingPlantedRoundsToA))
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
