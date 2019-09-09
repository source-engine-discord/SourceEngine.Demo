using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using TopStatsWaffle.Serialization;
using DemoInfo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopStatsWaffle.Models;

namespace TopStatsWaffle
{
    enum PSTATUS
    {
        ONSERVER,
        PLAYING,
        ALIVE
    }

    class TickCounter
    {
        public long ticksOnServer = 0;
        public long ticksPlaying = 0;
        public long ticksAlive = 0;

        public string detectedName = "NOT FOUND";
    }

    public class PlayerWeapon
    {
        public string name;
    }

    class MatchData
    {
        public Dictionary<Type, List<object>> events = new Dictionary<Type, List<object>>();

        public Dictionary<int, TickCounter> playerTicks = new Dictionary<int, TickCounter>();
        public Dictionary<int, long> playerLookups = new Dictionary<int, long>();
        public Dictionary<int, int> playerReplacements = new Dictionary<int, int>();

        const string winReasonTKills = "TerroristWin", winReasonCtKills = "CTWin", winReasonBombed = "TargetBombed", winReasonDefused = "BombDefused", winReasonRescued = "HostagesRescued", winReasonNotRescued = "HostagesNotRescued", winReasonTSaved = "TargetSaved";

        public bool passed = false;

        void addEvent(Type type, object ev)
        {
            //Create if doesnt exist
            if (!this.events.ContainsKey(type))
                this.events.Add(type, new List<object>());

            events[type].Add(ev);
        }

        /// <summary>
        /// Adds new player lookups and tick values
        /// </summary>
        /// <param name="p"></param>
        /// <returns>Whether or not the userID given has newly been / was previously stored</returns>
        bool bindPlayer(Player p)
        {
            int duplicateIdToRemoveTicks = 0;
            int duplicateIdToRemoveLookup = 0;


            if (p.Name != "unconnected" && p.Name != "GOTV")
            {
                if (!playerTicks.ContainsKey(p.UserID))
                {
                    // check if player has been added twice with different UserIDs
                    var duplicate = playerTicks.Where(x => x.Value.detectedName == p.Name).FirstOrDefault();

                    if (duplicate.Key != 0)
                    {
                        // copy duplicate's information across
                        playerTicks.Add(p.UserID, new TickCounter()
                        {
                            detectedName = duplicate.Value.detectedName,
                            ticksAlive = duplicate.Value.ticksAlive,
                            ticksOnServer = duplicate.Value.ticksOnServer,
                            ticksPlaying = duplicate.Value.ticksPlaying,
                        });

                        duplicateIdToRemoveTicks = duplicate.Key;
                    }
                    else
                    {
                        var detectedName = (string.IsNullOrWhiteSpace(p.Name)) ? "NOT FOUND" : p.Name;
                        playerTicks.Add(p.UserID, new TickCounter() { detectedName = detectedName } );
                    }
                }

                if (!playerLookups.ContainsKey(p.UserID))
                {
                    // check if player has been added twice with different UserIDs
                    var duplicate = playerLookups.Where(x => x.Value == p.SteamID).FirstOrDefault();

                    if (duplicate.Key == 0) // if the steam ID was 0
                    {
                        duplicate = playerLookups.Where(x => x.Key == duplicateIdToRemoveTicks).FirstOrDefault();
                    }
                    
                    if (p.SteamID != 0)
                    {
                        playerLookups.Add(p.UserID, p.SteamID);
                    }
                    else if (p.SteamID == 0 && duplicate.Key != 0)
                    {
                        playerLookups.Add(p.UserID, duplicate.Value);
                    }

                    duplicateIdToRemoveLookup = duplicate.Key;
                }

                // remove duplicates
                if (duplicateIdToRemoveTicks != 0 || duplicateIdToRemoveLookup != 0)
                {
                    if (duplicateIdToRemoveTicks != 0)
                    {
                        playerTicks.Remove(duplicateIdToRemoveTicks);
                    }
                    if (duplicateIdToRemoveLookup != 0)
                    {
                        playerLookups.Remove(duplicateIdToRemoveLookup);
                    }

                    /* store duplicate userIDs for replacing in events later on */
                    var idRemoved = (duplicateIdToRemoveLookup != 0) ? duplicateIdToRemoveLookup : duplicateIdToRemoveTicks;

                    // removes any instance of the old userID pointing to a different userID
                    if (playerReplacements.Any(r => r.Key == idRemoved))
                    {
                        playerReplacements.Remove(idRemoved);
                    }

                    // tries to avoid infinite loops by removing the old entry
                    if (playerReplacements.Any(r => r.Key == p.UserID && r.Value == idRemoved))
                    {
                        playerReplacements.Remove(p.UserID);
                    }

                    // replace current mappings between an ancient userID & the old userID, to use the new userID as the value instead
                    if (playerReplacements.Any(r => r.Value == idRemoved))
                    {
                        var keysToReplaceValue = playerReplacements.Where(r => r.Value == idRemoved).Select(r => r.Key);

                        foreach (var userId in keysToReplaceValue.ToList())
                        {
                            playerReplacements[userId] = p.UserID;
                        }
                    }

                    playerReplacements.Add(idRemoved, p.UserID); // Creates a new entry that maps the player's old user ID to their new user ID
                }

                return true;
            }

            return false;
        }

        void addTick(Player p, PSTATUS status)
        {
            bool userIdStored = bindPlayer(p);

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

        public static MatchData fromDemoFile(string file)
        {
            MatchData md = new MatchData();

            //Create demo parser instance
            DemoParser dp = new DemoParser(File.OpenRead(file));

            dp.ParseHeader();

            dp.PlayerBind += (object sender, PlayerBindEventArgs e) => {
                md.bindPlayer(e.Player);
            };

            // SERVER EVENTS ===================================================
            dp.MatchStarted += (object sender, MatchStartedEventArgs e) => {
                //prints blank space out to console
                Console.WriteLine();

                List<FeedbackMessage> currentFeedbackMessages = new List<FeedbackMessage>();

                //stores all fb messages so that they aren't lost when stats are reset
                if (md.events.Count() > 0 && md.events.Any(k => k.Key.Name.ToString() == "FeedbackMessage"))
                {
                    foreach (FeedbackMessage message in md.events.Where(k => k.Key.Name.ToString() == "FeedbackMessage").Select(v => v.Value).ElementAt(0))
                    {
                        if (message.Message.ToLower().Contains(">fb ") || message.Message.ToLower().Contains(">feedback "))
                        {
                            //Sets round to 0 as anything before a match start event should always be classed as warmup
                            currentFeedbackMessages.Add(new FeedbackMessage() { Round = 0, SteamID = message.SteamID, TeamName = message.TeamName, Message = message.Message });
                        }
                    }
                }

                md.events = new Dictionary<Type, List<object>>(); //resets all stats stored

                md.addEvent(typeof(MatchStartedEventArgs), e);

                //adds all stored fb messages back
                foreach (var feedbackMessage in currentFeedbackMessages)
                {
                    md.addEvent(typeof(FeedbackMessage), feedbackMessage);
                }
            };

            dp.ChickenKilled += (object sender, ChickenKilledEventArgs e) => {
                md.addEvent(typeof(ChickenKilledEventArgs), e);
            };

            dp.SayText2 += (object sender, SayText2EventArgs e) => {
                md.addEvent(typeof(SayText2EventArgs), e);

                var text = e.Text.ToString();

                if (text.ToLower().Contains(">fb") || text.ToLower().Contains(">feedback") || text.ToLower().Contains("> fb") || text.ToLower().Contains("> feedback"))
                {
                    int round = getCurrentRoundNum(md);

                    long steamId = e.Sender == null ? 0 : e.Sender.SteamID;

                    Player player = null;
                    if (steamId != 0)
                    {
                        player = dp.Participants.Where(p => p.SteamID == steamId).FirstOrDefault();
                    }
                    else
                    {
                        player = null;
                    }

                    var teamName = (player != null) ? player.Team.ToString() : null;
                    teamName = (teamName == "Spectate") ? "Spectator" : teamName;

                    FeedbackMessage feedbackMessage = new FeedbackMessage() { Round = round, SteamID = steamId, TeamName = teamName, Message = text }; // works out TeamName in SaveFiles() if it is null

                    md.addEvent(typeof(FeedbackMessage), feedbackMessage);
                }
            };

            dp.RoundEnd += (object sender, RoundEndedEventArgs e) => {
                var roundsEndedEvents = md.events.Where(k => k.Key.Name.ToString() == "RoundEndedEventArgs").Select(v => v.Value);
                var freezetimesEndedEvents = md.events.Where(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs").Select(v => v.Value);

                int numOfRoundsEnded = roundsEndedEvents.Count() > 0 ? roundsEndedEvents.ElementAt(0).Count() : 0;
                int numOfFreezetimesEnded = freezetimesEndedEvents.Count() > 0 ? freezetimesEndedEvents.ElementAt(0).Count() : 0;

                // if round_freeze_end event did not get fired in this round due to error
                while (numOfFreezetimesEnded <= numOfRoundsEnded)
                {
                    dp.RaiseFreezetimeEnded();
                    numOfFreezetimesEnded = freezetimesEndedEvents.ElementAt(0).Count();
                }

                md.addEvent(typeof(RoundEndedEventArgs), e);

                //print rounds complete out to console
                int roundsCount = md.getEvents<RoundEndedEventArgs>().Count();
                Console.WriteLine("Round " + roundsCount + " complete.");
            };

            dp.SwitchSides += (object sender, SwitchSidesEventArgs e) => {
                int roundsCount = md.getEvents<RoundEndedEventArgs>().Count();

                SwitchSidesEventArgs switchSidesEventArgs = new SwitchSidesEventArgs() { RoundBeforeSwitch = roundsCount };

                md.addEvent(typeof(SwitchSidesEventArgs), switchSidesEventArgs);
            };

            dp.FreezetimeEnded += (object sender, FreezetimeEndedEventArgs e) => {
                md.addEvent(typeof(FreezetimeEndedEventArgs), e);

                //work out teams at current round
                int roundsCount = md.getEvents<RoundEndedEventArgs>().Count();
                var players = dp.PlayingParticipants;

                TeamPlayers teams = new TeamPlayers()
                {
                    Terrorists = players.Where(p => p.Team.ToString().Equals("Terrorist")).ToList(),
                    CounterTerrorists = players.Where(p => p.Team.ToString().Equals("CounterTerrorist")).ToList(),
                    Round = roundsCount + 1,
                };

                md.addEvent(typeof(TeamPlayers), teams);

                int tEquipValue = 0, ctEquipValue = 0;
                int tExpenditure = 0, ctExpenditure = 0;

                foreach (var player in teams.Terrorists)
                {
                    tEquipValue += player.CurrentEquipmentValue; // player.FreezetimeEndEquipmentValue = 0 ???
                    tExpenditure += (player.CurrentEquipmentValue - player.RoundStartEquipmentValue); // (player.FreezetimeEndEquipmentValue = 0 - player.RoundStartEquipmentValue) ???
                }
                foreach (var player in teams.CounterTerrorists)
                {
                    ctEquipValue += player.CurrentEquipmentValue; // player.FreezetimeEndEquipmentValue = 0 ???
                    ctExpenditure += (player.CurrentEquipmentValue - player.RoundStartEquipmentValue); // (player.FreezetimeEndEquipmentValue = 0 - player.RoundStartEquipmentValue) ???
                }

                TeamEquipmentStats teamEquipmentStats = new TeamEquipmentStats() { Round = roundsCount + 1, TEquipValue = tEquipValue, CTEquipValue = ctEquipValue, TExpenditure = tExpenditure, CTExpenditure = ctExpenditure };

                md.addEvent(typeof(TeamEquipmentStats), teamEquipmentStats);
            };

            // PLAYER EVENTS ===================================================
            dp.PlayerKilled += (object sender, PlayerKilledEventArgs e) => {
                e.Round = getCurrentRoundNum(md);

                md.addEvent(typeof(PlayerKilledEventArgs), e);
            };

            dp.RoundMVP += (object sender, RoundMVPEventArgs e) => {
                md.addEvent(typeof(RoundMVPEventArgs), e);
            };

            dp.PlayerDisconnect += (object sender, PlayerDisconnectEventArgs e) => {
                if (e.Player != null && e.Player.Name != "unconnected" && e.Player.Name != "GOTV")
                {
                    int roundsCount = getCurrentRoundNum(md);

                    DisconnectedPlayer disconnectedPlayer = new DisconnectedPlayer() { PlayerDisconnectEventArgs = e, Round = roundsCount - 1 };

                    md.addEvent(typeof(DisconnectedPlayer), disconnectedPlayer);
                }
            };

            // BOMB EVENTS =====================================================
            dp.BombPlanted += (object sender, BombEventArgs e) => {
                int roundsCount = md.getEvents<RoundEndedEventArgs>().Count();

                BombPlanted bombPlanted = new BombPlanted() { Round = roundsCount + 1, TimeInRound = e.TimeInRound, Player = e.Player, Bombsite = e.Site };

                md.addEvent(typeof(BombPlanted), bombPlanted);
            };

            dp.BombExploded += (object sender, BombEventArgs e) => {
                int roundsCount = md.getEvents<RoundEndedEventArgs>().Count();

                BombExploded bombExploded = new BombExploded() { Round = roundsCount + 1, TimeInRound = e.TimeInRound, Player = e.Player, Bombsite = e.Site };

                md.addEvent(typeof(BombExploded), bombExploded);
            };

            dp.BombDefused += (object sender, BombEventArgs e) => {
                int roundsCount = md.getEvents<RoundEndedEventArgs>().Count();

                BombDefused bombDefused = new BombDefused() { Round = roundsCount + 1, TimeInRound = e.TimeInRound, Player = e.Player, Bombsite = e.Site, HasKit = e.Player.HasDefuseKit };

                md.addEvent(typeof(BombDefused), bombDefused);
            };

            // HOSTAGE EVENTS =====================================================
            dp.HostageRescued += (object sender, HostageRescuedEventArgs e) => {
                int roundsCount = md.getEvents<RoundEndedEventArgs>().Count();

                HostageRescued hostageRescued = new HostageRescued { Round = roundsCount + 1, TimeInRound = e.TimeInRound, Player = e.Player, Hostage = e.Hostage };

                md.addEvent(typeof(HostageRescued), hostageRescued);
            };

            // WEAPON EVENTS ===================================================
            dp.WeaponFired += (object sender, WeaponFiredEventArgs e) => {
                md.addEvent(typeof(WeaponFiredEventArgs), e);

                var round = getCurrentRoundNum(md);

                ShotFired shotFired = new ShotFired() { Round = round, Shooter = e.Shooter, Weapon = e.Weapon };

                md.addEvent(typeof(ShotFired), shotFired);
            };

            // GRENADE EVENTS ==================================================
            dp.ExplosiveNadeExploded += (object sender, GrenadeEventArgs e) => {
                md.addEvent(typeof(GrenadeEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.FireNadeStarted += (object sender, FireEventArgs e) => {
                md.addEvent(typeof(FireEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.SmokeNadeStarted += (object sender, SmokeEventArgs e) => {
                md.addEvent(typeof(SmokeEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.FlashNadeExploded += (object sender, FlashEventArgs e) => {
                md.addEvent(typeof(FlashEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.DecoyNadeStarted += (object sender, DecoyEventArgs e) => {
                md.addEvent(typeof(DecoyEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            // PLAYER TICK HANDLER ============================================
            dp.TickDone += (object sender, TickDoneEventArgs e) => {
                foreach (Player p in dp.PlayingParticipants)
                {
                    md.addTick(p, PSTATUS.PLAYING);

                    if (p.IsAlive)
                        md.addTick(p, PSTATUS.ALIVE);
                }

                foreach (Player p in dp.Participants)
                {
                    if (!p.Disconnected)
                        md.addTick(p, PSTATUS.ONSERVER);
                }
            };

            const int interval = 2500;
            int progMod = interval;

            ProgressViewer pv = new ProgressViewer(Path.GetFileName(file));

            // PROGRESS BAR ==================================================
            dp.TickDone += (object sender, TickDoneEventArgs e) =>
            {
                progMod++;
                if (progMod >= interval)
                {
                    progMod = 0;

                    pv.percent = dp.ParsingProgess;
                    pv.Draw();
                }
            };

            try
            {
                dp.ParseToEnd();
                pv.End();

                md.passed = true;
            }
            catch (Exception e)
            {
                pv.Error();
            }

            dp.Dispose();
            return md;
        }

        public void CreateFiles(
            List<string> demo, bool noguid, TanookiStats tanookiStats, Dictionary<string, IEnumerable<MatchStartedEventArgs>> matchStartValues, Dictionary<string, IEnumerable<SwitchSidesEventArgs>> switchSidesValues,
            Dictionary<string, IEnumerable<FeedbackMessage>> messagesValues, Dictionary<string, IEnumerable<TeamPlayers>> teamPlayersValues, Dictionary<string, IEnumerable<PlayerKilledEventArgs>> playerKilledEventsValues,
            Dictionary<string, IEnumerable<Player>> playerValues, Dictionary<string, IEnumerable<Equipment>> weaponValues, Dictionary<string, IEnumerable<int>> penetrationValues,
            Dictionary<string, IEnumerable<BombPlanted>> bombsitePlantValues, Dictionary<string, IEnumerable<BombExploded>> bombsiteExplodeValues, Dictionary<string, IEnumerable<BombDefused>> bombsiteDefuseValues,
            Dictionary<string, IEnumerable<char>> bombsiteValues, Dictionary<string, IEnumerable<HostageRescued>> hostageRescueValues, Dictionary<string, IEnumerable<char>> hostageValues,
            Dictionary<string, IEnumerable<Team>> teamValues, Dictionary<string, IEnumerable<RoundEndReason>> roundEndReasonValues, Dictionary<string, IEnumerable<double>> roundLengthValues,
            Dictionary<string, IEnumerable<TeamEquipmentStats>> teamEquipmentValues, Dictionary<string, IEnumerable<NadeEventArgs>> grenadeValues, Dictionary<string, IEnumerable<ChickenKilledEventArgs>> chickenValues,
            Dictionary<string, IEnumerable<ShotFired>> shotsFiredValues, bool writeTicks = true
        )
        {
            var mapDateSplit = (!string.IsNullOrWhiteSpace(demo[2]) && demo[2] != "unknown") ? demo[2].Split('/')  : null;
            var mapDateString = (mapDateSplit != null && mapDateSplit.Count() >= 3) ? (mapDateSplit[2] + "_" + mapDateSplit[0] + "_" + mapDateSplit[1]) : string.Empty;

            var mapNameSplit = (matchStartValues["MatchStarts"].Count() > 0) ? matchStartValues["MatchStarts"].ElementAt(0).Mapname.Split('/') : new string[] { demo[1] };
            var mapNameString = mapNameSplit.Count() > 2 ? mapNameSplit[2] : mapNameSplit[0];

            /* demo parser version */
            VersionNumber versionNumber = new VersionNumber() { Version = "1.0.1" };
            /* demo parser version end */

            /* Supported gamemodes */
            List<string> supportedGamemodes = new List<string>() { "Defuse", "Hostage", "Wingman" };
            /* Supported gamemodes end */

            /* map info */
            MapInfo mapInfo = new MapInfo() { MapName = demo[1], TestDate = demo[2], TestType = demo[3] };

            mapInfo.MapName = (mapNameSplit.Count() > 2) ? mapNameSplit[2] : mapInfo.MapName; // use the mapname from inside the demo itself if possible, otherwise use the mapname from the demo file's name
            mapInfo.WorkshopID = (mapNameSplit.Count() > 2) ? mapNameSplit[1] : "unknown";

            // attempts to get the gamemode
            var roundsWonReasons = getRoundsWonReasons(roundEndReasonValues);

            if (matchStartValues["MatchStarts"].Any(m => m.HasBombsites) || bombsiteValues["PlantsSites"].Count() > 0 || roundsWonReasons.Any(w => w.ToString() == winReasonBombed) || roundsWonReasons.Any(w => w.ToString() == winReasonDefused) || roundsWonReasons.Any(w => w.ToString() == winReasonTSaved))
            {
                if (teamPlayersValues["TeamPlayers"].Any(t => t.Terrorists.Count() > 2 && teamPlayersValues["TeamPlayers"].Any(ct => ct.CounterTerrorists.Count() > 2)))
                {
                    mapInfo.GameMode = "Defuse";
                }
                else
                {
                    mapInfo.GameMode = "Wingman";
                }
            }
            else // assumes it's hostage if no bomb events are triggered --- is there a better way to decide this since matches with no plants will fall into this category ???
                 // maybe instead check the entity list for entities that are bombs or hostages?
            {
                mapInfo.GameMode = "Hostage";
            }
            /* map info end */

            /* player stats */
            List<PlayerStats> playerStats = new List<PlayerStats>();

            Dictionary<long, Dictionary<string, long>> data = new Dictionary<long, Dictionary<string, long>>();
            Dictionary<long, Dictionary<string, string>> playerNames = new Dictionary<long, Dictionary<string, string>>();

            foreach (string catagory in playerValues.Keys)
            {
                foreach (Player p in playerValues[catagory])
                {
                    //Skip players not in this catagory
                    if (p == null)
                        continue;

                    // checks for an updated userID for the user, loops incase it has changed more than once
                    int userId = p.UserID;
                    while (checkForUpdatedUserId(userId) != userId)
                    {
                        userId = checkForUpdatedUserId(userId);
                    }

                    if (!playerLookups.ContainsKey(userId))
                        continue;

                    //Add player to collections list if doesnt exist
                    if (!playerNames.ContainsKey(playerLookups[userId]))
                        playerNames.Add(playerLookups[userId], new Dictionary<string, string>());

                    if (!data.ContainsKey(playerLookups[userId]))
                        data.Add(playerLookups[userId], new Dictionary<string, long>());

                    //Add catagory to dictionary if doesnt exist
                    if (!playerNames[playerLookups[userId]].ContainsKey("Name"))
                        playerNames[playerLookups[userId]].Add("Name", p.Name);

                    if (!data[playerLookups[userId]].ContainsKey(catagory))
                        data[playerLookups[userId]].Add(catagory, 0);

                    //Increment it
                    data[playerLookups[userId]][catagory]++;
                }
            }

            // remove teamkills and suicides from kills (easy messy implementation)
            foreach (var kill in playerKilledEventsValues["PlayerKilledEvents"])
            {
                if (kill.Killer != null && kill.Killer.Name != "unconnected")
                {
                    // checks for an updated userID for the user, loops incase it has changed more than once
                    int userId = kill.Killer.UserID;
                    while (checkForUpdatedUserId(userId) != userId)
                    {
                        userId = checkForUpdatedUserId(userId);
                    }

                    if (kill.Suicide)
                    {
                        data[playerLookups[userId]]["Kills"] -= 1;
                    }
                    else if (kill.TeamKill)
                    {
                        data[playerLookups[userId]]["Kills"] -= 2;
                    }
                }
            }

            int counter = 0;
            foreach (long player in data.Keys)
            {
                var match = playerNames.Where(p => p.Key.ToString() == player.ToString());
                var playerName = match.ElementAt(0).Value.ElementAt(0).Value;
                var steamID = match.ElementAt(0).Key;

                List<int> statsList1 = new List<int>();
                foreach (string catagory in playerValues.Keys)
                {
                    if (data[player].ContainsKey(catagory))
                    {
                        statsList1.Add((int)data[player][catagory]);
                    }
                    else
                    {
                        statsList1.Add(0);
                    }
                }

                List<long> statsList2 = new List<long>();
                if (writeTicks)
                {
                    if (playerLookups.Any(p => p.Value == player))
                    {
                        foreach (int userid in playerLookups.Keys)
                        {
                            if (playerLookups[userid] == player)
                            {
                                statsList2.Add(this.playerTicks[userid].ticksAlive);

                                statsList2.Add(this.playerTicks[userid].ticksOnServer);

                                statsList2.Add(this.playerTicks[userid].ticksPlaying);

                                break;
                            }
                        }
                    }
                }

                int numOfKillsAsBot = playerKilledEventsValues["PlayerKilledEvents"].Where(k => (k.Killer != null) && (k.Killer.Name.ToString() == playerName.ToString()) && (k.KillerBotTakeover)).Count();
                int numOfDeathsAsBot = playerKilledEventsValues["PlayerKilledEvents"].Where(k => (k.Victim != null) && (k.Victim.Name.ToString() == playerName.ToString()) && (k.VictimBotTakeover)).Count();
                int numOfAssistsAsBot = playerKilledEventsValues["PlayerKilledEvents"].Where(k => (k.Assister != null) && (k.Assister.Name.ToString() == playerName.ToString()) && (k.AssisterBotTakeover)).Count();

                playerStats.Add(new PlayerStats()
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
                });

                counter++;
            }
            /* player stats end */

            /* winning team stats, round wins team and reason stats */
            WinnersStats winnersStats = new WinnersStats();
            List<RoundsStats> roundsStats = new List<RoundsStats>();

            // winning team & total rounds stats
            IEnumerable<SwitchSidesEventArgs> switchSides = switchSidesValues["SwitchSides"];
            var roundsWonTeams = getRoundsWonTeams(teamValues);
            int totalRoundsWonTeamAlpha = 0, totalRoundsWonTeamBeta = 0;

            for (int i = 0; i < roundsWonTeams.Count(); i++)
            {
                if (roundsWonReasons.Count() > i) // game was abandoned early
                {
                    string reason = string.Empty;
                    string half = string.Empty;
                    bool isOvertime = ((switchSides.Count() >= 2) && (i >= switchSides.ElementAt(1).RoundBeforeSwitch)) ? true : false;
                    int overtimeNum = 0;
                    double roundLength = roundLengthValues["RoundsLengths"].ElementAt(i);

                    // determines which half / side it is
                    if (isOvertime)
                    {
                        int lastNormalTimeRound = switchSides.ElementAt(1).RoundBeforeSwitch;
                        int roundsPerOTHalf = (switchSides.Count() >= 3) ? (switchSides.ElementAt(2).RoundBeforeSwitch - lastNormalTimeRound) : 3; // just assume 3 rounds per OT half if it cannot be checked
                        int roundsPerOT = roundsPerOTHalf * 2;

                        int roundsIntoOT = (i + 1) - lastNormalTimeRound;
                        overtimeNum = (int)Math.Ceiling((double)roundsIntoOT / roundsPerOT);

                        double currentOTHalf = (int)Math.Ceiling((double)roundsIntoOT / roundsPerOTHalf);
                        half = currentOTHalf % 2 == 1 ? "First" : "Second";
                    }
                    else
                    {
                        half = (switchSides.Count() > 0) ? ((i < switchSides.ElementAt(0).RoundBeforeSwitch) ? "First" : "Second") : "First";
                    }

                    // total rounds calculation
                    if (half == "First")
                    {
                        if (roundsWonTeams.ElementAt(i).ToString() == "Terrorist")
                        {
                            totalRoundsWonTeamAlpha++;
                        }
                        else if (roundsWonTeams.ElementAt(i).ToString() == "CounterTerrorist")
                        {
                            totalRoundsWonTeamBeta++;
                        }
                    }
                    else if (half == "Second")
                    {
                        if (roundsWonTeams.ElementAt(i).ToString() == "Terrorist")
                        {
                            totalRoundsWonTeamBeta++;
                        }
                        else if (roundsWonTeams.ElementAt(i).ToString() == "CounterTerrorist")
                        {
                            totalRoundsWonTeamAlpha++;
                        }
                    }

                    //win method
                    switch (roundsWonReasons[i].ToString())
                    {
                        case winReasonTKills:
                            reason = "T Kills";
                            break;
                        case winReasonCtKills:
                            reason = "CT Kills";
                            break;
                        case winReasonBombed:
                            reason = "Bombed";
                            break;
                        case winReasonDefused:
                            reason = "Defused";
                            break;
                        case winReasonRescued:
                            reason = "HostagesRescued";
                            break;
                        case winReasonNotRescued:
                            reason = "HostagesNotRescued";
                            break;
                        case winReasonTSaved:
                            reason = "TSaved";
                            break;
                    }

                    // team count values
                    int roundNum = i + 1;
                    var currentRoundTeams = teamPlayersValues["TeamPlayers"].Where(t => t.Round == roundNum).FirstOrDefault();

                    foreach (var player in currentRoundTeams.Terrorists) // make sure steamID's aren't 0
                    {
                        player.SteamID = (player.SteamID == 0) ? getSteamIdByPlayerName(playerNames, player.Name) : player.SteamID;
                    }
                    foreach (var player in currentRoundTeams.CounterTerrorists) // make sure steamID's aren't 0
                    {
                        player.SteamID = (player.SteamID == 0) ? getSteamIdByPlayerName(playerNames, player.Name) : player.SteamID;
                    }

                    int playerCountTeamA = (currentRoundTeams != null) ? (half == "First" ? currentRoundTeams.Terrorists.Count() : currentRoundTeams.CounterTerrorists.Count()) : 0;
                    int playerCountTeamB = (currentRoundTeams != null) ? (half == "First" ? currentRoundTeams.CounterTerrorists.Count() : currentRoundTeams.Terrorists.Count()) : 0;

                    // equip values
                    var teamEquipValues = teamEquipmentValues["TeamEquipmentStats"].Count() >= i ? teamEquipmentValues["TeamEquipmentStats"].ElementAt(i) : null;
                    int equipValueTeamA = (teamEquipValues != null) ? (half == "First" ? teamEquipValues.TEquipValue : teamEquipValues.CTEquipValue) : 0;
                    int equipValueTeamB = (teamEquipValues != null) ? (half == "First" ? teamEquipValues.CTEquipValue : teamEquipValues.TEquipValue) : 0;
                    int expenditureTeamA = (teamEquipValues != null) ? (half == "First" ? teamEquipValues.TExpenditure : teamEquipValues.CTExpenditure) : 0;
                    int expenditureTeamB = (teamEquipValues != null) ? (half == "First" ? teamEquipValues.CTExpenditure : teamEquipValues.TExpenditure) : 0;

                    // bombsite planted/exploded/defused at
                    string bombsite = null;
                    BombPlanted bombPlanted = null; BombExploded bombExploded = null; BombDefused bombDefused = null;

                    if (bombsitePlantValues["BombsitePlants"].Any(p => p.Round == roundNum))
                    {
                        bombPlanted = bombsitePlantValues["BombsitePlants"].Where(p => p.Round == roundNum).FirstOrDefault();
                        bombsite = bombPlanted.Bombsite.ToString();
                    }
                    if (bombsiteExplodeValues["BombsiteExplosions"].Any(p => p.Round == roundNum))
                    {
                        bombExploded = bombsiteExplodeValues["BombsiteExplosions"].Where(p => p.Round == roundNum).FirstOrDefault();
                        bombsite = (bombsite != null) ? bombsite : bombExploded.Bombsite.ToString();
                    }
                    if (bombsiteDefuseValues["BombsiteDefuses"].Any(p => p.Round == roundNum))
                    {
                        bombDefused = bombsiteDefuseValues["BombsiteDefuses"].Where(p => p.Round == roundNum).FirstOrDefault();
                        bombsite = (bombsite != null) ? bombsite : bombDefused.Bombsite.ToString();
                    }

                    var timeInRoundPlanted = (bombPlanted != null) ? bombPlanted.TimeInRound : 0;
                    var timeInRoundExploded = (bombExploded != null) ? bombExploded.TimeInRound : 0;
                    var timeInRoundDefused = (bombDefused != null) ? bombDefused.TimeInRound : 0;

                    // hostage rescued
                    bool rescuedHostageA = false, rescuedHostageB = false, rescuedAllHostages = false;
                    HostageRescued hostageRescuedA = null, hostageRescuedB = null;

                    if (hostageRescueValues["HostageRescues"].Any(r => r.Round == roundNum))
                    {
                        hostageRescuedA = hostageRescueValues["HostageRescues"].Where(r => r.Round == roundNum && r.Hostage == 'A').FirstOrDefault();
                        hostageRescuedB = hostageRescueValues["HostageRescues"].Where(r => r.Round == roundNum && r.Hostage == 'B').FirstOrDefault();

                        rescuedHostageA = (hostageRescuedA != null) ? true : false;
                        rescuedHostageB = (hostageRescuedB != null) ? true : false;

                        rescuedAllHostages = (rescuedHostageA && rescuedHostageB) ? true : false;
                    }

                    var timeInRoundRescuedHostageA = (rescuedHostageA != false) ? hostageRescuedA.TimeInRound : 0;
                    var timeInRoundRescuedHostageB = (rescuedHostageB != false) ? hostageRescuedB.TimeInRound : 0;

                    roundsStats.Add(new RoundsStats()
                    {
                        Round = i + 1,
                        Half = half,
                        Overtime = overtimeNum,
                        Length = roundLength,
                        Winners = roundsWonTeams[i].ToString(),
                        WinMethod = reason,
                        BombsitePlantedAt = bombsite,
                        RescuedHostageA = rescuedHostageA,
                        RescuedHostageB = rescuedHostageB,
                        RescuedAllHostages = rescuedAllHostages,
                        TimeInRoundPlanted = timeInRoundPlanted,
                        TimeInRoundExploded = timeInRoundExploded,
                        TimeInRoundDefused = timeInRoundDefused,
                        TimeInRoundRescuedHostageA = timeInRoundRescuedHostageA,
                        TimeInRoundRescuedHostageB = timeInRoundRescuedHostageB,
                        TeamAlphaPlayerCount = playerCountTeamA,
                        TeamBetaPlayerCount = playerCountTeamB,
                        TeamAlphaEquipValue = equipValueTeamA,
                        TeamBetaEquipValue = equipValueTeamB,
                        TeamAlphaExpenditure = expenditureTeamA,
                        TeamBetaExpenditure = expenditureTeamB,
                    });
                }
            }

            // work out winning team
            string winningTeam = (totalRoundsWonTeamAlpha >= totalRoundsWonTeamBeta) ? (totalRoundsWonTeamAlpha > totalRoundsWonTeamBeta) ? "Team Alpha" : "Draw" : "Team Bravo";

            // winners stats
            winnersStats = new WinnersStats() { WinningTeam = winningTeam, TeamAlphaRounds = totalRoundsWonTeamAlpha, TeamBetaRounds = totalRoundsWonTeamBeta };
            /* winning team stats, round wins team and reason stats end */

            /* bombsite stats */
            List<BombsiteStats> bombsiteStats = new List<BombsiteStats>();

            List<char> bombsitePlants = new List<char>(bombsiteValues["PlantsSites"]);
            List<char> bombsiteExplosions = new List<char>(bombsiteValues["ExplosionsSites"]);
            List<char> bombsiteDefuses = new List<char>(bombsiteValues["DefusesSites"]);

            int plantsA = bombsitePlants.Where(b => b.ToString().Equals("A")).Count();
            int explosionsA = bombsiteExplosions.Where(b => b.ToString().Equals("A")).Count();
            int defusesA = bombsiteDefuses.Where(b => b.ToString().Equals("A")).Count();

            int plantsB = bombsitePlants.Where(b => b.ToString().Equals("B")).Count();
            int explosionsB = bombsiteExplosions.Where(b => b.ToString().Equals("B")).Count();
            int defusesB = bombsiteDefuses.Where(b => b.ToString().Equals("B")).Count();

            bombsiteStats.Add(new BombsiteStats() { Bombsite = 'A', Plants = plantsA, Explosions = explosionsA, Defuses = defusesA });
            bombsiteStats.Add(new BombsiteStats() { Bombsite = 'B', Plants = plantsB, Explosions = explosionsB, Defuses = defusesB });
            /* bombsite stats end */

            /* hostage stats */
            List<HostageStats> hostageStats = new List<HostageStats>();

            List<char> hostageRescues = new List<char>(hostageValues["RescuedHostages"]);

            int rescuesA = hostageRescues.Where(b => b.ToString().Equals("A")).Count();
            int rescuesB = hostageRescues.Where(b => b.ToString().Equals("B")).Count();

            hostageStats.Add(new HostageStats() { Hostage = 'A', Rescues = rescuesA });
            hostageStats.Add(new HostageStats() { Hostage = 'B', Rescues = rescuesB });
            /* hostage stats end */

            /* Grenades total stats */
            List<GrenadesTotalStats> grenadesTotalStats = new List<GrenadesTotalStats>();

            string[] nadeTypes = { "Flash", "Smoke", "HE", "Incendiary", "Decoy" };

            List<NadeEventArgs> nades = new List<NadeEventArgs>(grenadeValues.ElementAt(0).Value);
            var flashes = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[0]));
            var smokes = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[1]));
            var hegrenades = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[2]));
            var incendiaries = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[3]));
            var decoys = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[4]));

            List<IEnumerable<NadeEventArgs>> nadeGroups = new List<IEnumerable<NadeEventArgs>>() { flashes, smokes, hegrenades, incendiaries, decoys };

            for (int i = 0; i < nadeTypes.Count(); i++)
            {
                grenadesTotalStats.Add(new GrenadesTotalStats() { NadeType = nadeTypes[i], AmountUsed = nadeGroups.ElementAt(i).Count() });
            }
            /* Grenades total stats end */

            /* Grenades specific stats */
            List<GrenadesSpecificStats> grenadesSpecificStats = new List<GrenadesSpecificStats>();

            foreach (var nadeGroup in nadeGroups)
            {
                if (nadeGroup.Count() > 0)
                {
                    bool flashGroup = nadeGroup.ElementAt(0).NadeType.ToString() == nadeTypes[0] ? true : false; //check if in flash group

                    foreach (var nade in nadeGroup)
                    {
                        string[] positionSplit = nade.Position.ToString().Split(new string[] { "{X: ", ", Y: ", ", Z: ", "}" }, StringSplitOptions.None);
                        string positions = $"{ positionSplit[1] },{ positionSplit[2] },{ positionSplit[3] }";

                        //retrieve steam ID using player name if the event does not return it correctly
                        long steamId = (nade.ThrownBy.SteamID == 0) ? getSteamIdByPlayerName(playerNames, nade.ThrownBy.Name) : nade.ThrownBy.SteamID;

                        if (flashGroup)
                        {
                            var flash = nade as FlashEventArgs;
                            int numOfPlayersFlashed = flash.FlashedPlayers.Count();

                            grenadesSpecificStats.Add(new GrenadesSpecificStats() { NadeType = nade.NadeType.ToString(), SteamID = steamId, XPosition = double.Parse(positionSplit[1]), YPosition = double.Parse(positionSplit[2]), ZPosition = double.Parse(positionSplit[3]), NumPlayersFlashed = numOfPlayersFlashed });
                        }
                        else
                        {
                            grenadesSpecificStats.Add(new GrenadesSpecificStats() { NadeType = nade.NadeType.ToString(), SteamID = steamId, XPosition = double.Parse(positionSplit[1]), YPosition = double.Parse(positionSplit[2]), ZPosition = double.Parse(positionSplit[3]) });
                        }
                    }
                }
            }
            /* Grenades specific stats end */

            /* Player Kills/Death Positions */
            List<KillsStats> killsStats = new List<KillsStats>();

            List<Player> kills = new List<Player>(playerValues["Kills"].ToList());
            List<Player> deaths = new List<Player>(playerValues["Deaths"].ToList());

            List<Equipment> weaponKillers = new List<Equipment>(weaponValues["WeaponKillers"].ToList());
            List<int> penetrations = new List<int>(penetrationValues["PenetratedObjects"].ToList());

            for (int i = 0; i < deaths.Count(); i++)
            {
                if (kills.ElementAt(i) != null && kills.ElementAt(i).Position != null && deaths.ElementAt(i) != null && deaths.ElementAt(i).Position != null)
                {
                    var playerKilledEvent = playerKilledEventsValues["PlayerKilledEvents"].ElementAt(i);

                    if (playerKilledEvent != null)
                    {
                        int round = playerKilledEvent.Round;

                        string[] killPositionSplit = kills.ElementAt(i).Position.ToString().Split(new string[] { "{X: ", ", Y: ", ", Z: ", "}" }, StringSplitOptions.None);
                        string killPositions = $"{ killPositionSplit[1] },{ killPositionSplit[2] },{ killPositionSplit[3] }";

                        string[] deathPositionSplit = deaths.ElementAt(i).Position.ToString().Split(new string[] { "{X: ", ", Y: ", ", Z: ", "}" }, StringSplitOptions.None);
                        string deathPositions = $"{ deathPositionSplit[1] },{ deathPositionSplit[2] },{ deathPositionSplit[3] }";

                        //retrieve steam ID using player name if the event does not return it correctly
                        long killerSteamId = kills.ElementAt(i) != null ? ((kills.ElementAt(i).SteamID == 0) ? getSteamIdByPlayerName(playerNames, kills.ElementAt(i).Name) : kills.ElementAt(i).SteamID) : 0;
                        long victimSteamId = deaths.ElementAt(i) != null ? ((deaths.ElementAt(i).SteamID == 0) ? getSteamIdByPlayerName(playerNames, deaths.ElementAt(i).Name) : deaths.ElementAt(i).SteamID) : 0;
                        long assisterSteamId = playerKilledEvent.Assister != null ? ((playerKilledEvent.Assister.SteamID == 0) ? getSteamIdByPlayerName(playerNames, playerKilledEvent.Assister.Name) : playerKilledEvent.Assister.SteamID) : 0;

                        var weaponUsed = weaponKillers.ElementAt(i).Weapon.ToString();
                        var numOfPenetrations = penetrations.ElementAt(i);

                        if (weaponUsed == null || weaponUsed == string.Empty)
                        {
                            weaponUsed = weaponKillers.ElementAt(i).OriginalString.ToString();
                        }

                        bool firstKillOfTheRound = (killsStats.Any(k => k.Round == round && k.FirstKillOfTheRound == true)) ? false : true;

                        killsStats.Add(new KillsStats()
                        {
                            Round = round,
                            TimeInRound = playerKilledEvent.TimeInRound,
                            Weapon = weaponUsed,
                            KillerSteamID = killerSteamId,
                            KillerBotTakeover = playerKilledEvent.KillerBotTakeover,
                            XPositionKill = double.Parse(killPositionSplit[1]),
                            YPositionKill = double.Parse(killPositionSplit[2]),
                            ZPositionKill = double.Parse(killPositionSplit[3]),
                            VictimSteamID = victimSteamId,
                            VictimBotTakeover = playerKilledEvent.VictimBotTakeover,
                            XPositionDeath = double.Parse(deathPositionSplit[1]),
                            YPositionDeath = double.Parse(deathPositionSplit[2]),
                            ZPositionDeath = double.Parse(deathPositionSplit[3]),
                            AssisterSteamID = assisterSteamId,
                            AssisterBotTakeover = playerKilledEvent.AssisterBotTakeover,
                            FirstKillOfTheRound = firstKillOfTheRound,
                            Suicide = playerKilledEvent.Suicide,
                            TeamKill = playerKilledEvent.TeamKill,
                            PenetrationsCount = numOfPenetrations,
                            Headshot = playerKilledEvent.Headshot,
                            AssistedFlash = playerKilledEvent.AssistedFlash,
                        });
                    }
                }
            }
            /* Player Kills/Death Positions end */

            /* Feedback Messages */
            List<FeedbackMessage> feedbackMessages = new List<FeedbackMessage>();

            foreach (var message in messagesValues["Messages"])
            {
                var currentRoundTeams = teamPlayersValues["TeamPlayers"].Where(t => t.Round == message.Round).FirstOrDefault();

                if (currentRoundTeams != null && (message.SteamID == 0 || message.TeamName == null)) // excludes warmup round
                {
                    // retrieve steam ID using player name if the event does not return it correctly
                    foreach (var player in currentRoundTeams.Terrorists)
                    {
                        player.SteamID = (player.SteamID == 0) ? getSteamIdByPlayerName(playerNames, player.Name) : player.SteamID;
                    }
                    foreach (var player in currentRoundTeams.CounterTerrorists)
                    {
                        player.SteamID = (player.SteamID == 0) ? getSteamIdByPlayerName(playerNames, player.Name) : player.SteamID;
                    }

                    if (currentRoundTeams.Terrorists.Any(p => p.SteamID == message.SteamID))
                    {
                        message.TeamName = "Terrorist";
                    }
                    else if (currentRoundTeams.CounterTerrorists.Any(p => p.SteamID == message.SteamID))
                    {
                        message.TeamName = "CounterTerrorist";
                    }
                    else
                    {
                        message.TeamName = "Spectator";
                    }
                }

                feedbackMessages.Add(message);
            }
            /* Feedback Messages end */

            /* chickens killed stats */
            ChickenStats chickenStats = new ChickenStats() { Killed = chickenValues["ChickensKilled"].Count() };
            /* chickens killed stats end */

            /* team stats */
            List<TeamStats> teamStats = new List<TeamStats>();

            var firstHalf = true;
            int swappedSidesCount = 0;
            int currentRoundChecking = 1;
            
            foreach (var teamPlayers in teamPlayersValues["TeamPlayers"])
            {
                // players in each team per round
                swappedSidesCount = switchSides.Count() > swappedSidesCount ? (switchSides.ElementAt(swappedSidesCount).RoundBeforeSwitch == currentRoundChecking - 1 ? swappedSidesCount + 1 : swappedSidesCount) : swappedSidesCount;
                firstHalf = (swappedSidesCount % 2 == 0) ? true : false;

                var currentRoundTeams = teamPlayersValues["TeamPlayers"].Where(t => t.Round == teamPlayers.Round).FirstOrDefault();

                var alphaPlayers = (currentRoundTeams != null) ? (firstHalf ? currentRoundTeams.Terrorists : currentRoundTeams.CounterTerrorists) : null;
                var bravoPlayers = (currentRoundTeams != null) ? (firstHalf ? currentRoundTeams.CounterTerrorists : currentRoundTeams.Terrorists) : null;

                List<long> alphaSteamIds = new List<long>();
                List<long> bravoSteamIds = new List<long>();

                foreach (var player in alphaPlayers)
                {
                    player.SteamID = (player.SteamID == 0) ? getSteamIdByPlayerName(playerNames, player.Name) : player.SteamID;
                    alphaSteamIds.Add(player.SteamID);
                }
                foreach (var player in bravoPlayers)
                {
                    player.SteamID = (player.SteamID == 0) ? getSteamIdByPlayerName(playerNames, player.Name) : player.SteamID;
                    bravoSteamIds.Add(player.SteamID);
                }

                // attempts to remove and stray players that are supposedly on a team, even though they exceed the max players per team and they are not in player lookups
                // (also most likely have a steam ID of 0)
                List<long> alphaSteamIdsToRemove = new List<long>();
                List<long> bravoSteamIdsToRemove = new List<long>();

                if (mapInfo.TestType.ToLower().Contains("comp") && alphaSteamIds.Count() > 5)
                {
                    foreach (var steamId in alphaSteamIds)
                    {
                        if (!playerLookups.Any(l => l.Value == steamId))
                        {
                            alphaSteamIdsToRemove.Add(steamId);
                        }
                    }
                }
                else if (mapInfo.TestType.ToLower().Contains("casual") && alphaSteamIds.Count() > 10)
                {
                    foreach (var steamId in alphaSteamIds)
                    {
                        if (!playerLookups.Any(l => l.Value == steamId))
                        {
                            alphaSteamIdsToRemove.Add(steamId);
                        }
                    }
                }

                if (mapInfo.TestType.ToLower().Contains("comp") && bravoSteamIds.Count() > 5)
                {
                    foreach (var steamId in bravoSteamIds)
                    {
                        if (!playerLookups.Any(l => l.Value == steamId))
                        {
                            bravoSteamIdsToRemove.Add(steamId);
                        }
                    }
                }
                else if (mapInfo.TestType.ToLower().Contains("casual") && bravoSteamIds.Count() > 10)
                {
                    foreach (var steamId in bravoSteamIds)
                    {
                        if (!playerLookups.Any(l => l.Value == steamId))
                        {
                            bravoSteamIdsToRemove.Add(steamId);
                        }
                    }
                }

                // remove the steamIDs if necessary
                foreach (var steamId in alphaSteamIdsToRemove)
                {
                    alphaSteamIds.Remove(steamId);
                }
                foreach (var steamId in bravoSteamIdsToRemove)
                {
                    bravoSteamIds.Remove(steamId);
                }

                // kills/death stats this round
                var deathsThisRound = playerKilledEventsValues["PlayerKilledEvents"].Where(k => k.Round == teamPlayers.Round);

                // kills this round
                int alphaKills = deathsThisRound.Where(d => d.Killer != null
                                                            && alphaSteamIds.Contains(d.Killer.SteamID))
                                                            .Count();
                int bravoKills = deathsThisRound.Where(d => d.Killer != null
                                                            && bravoSteamIds.Contains(d.Killer.SteamID))
                                                            .Count();

                // deaths this round
                int alphaDeaths = deathsThisRound.Where(d => d.Victim != null
                                                            && alphaSteamIds.Contains(d.Victim.SteamID))
                                                            .Count();
                int bravoDeaths = deathsThisRound.Where(d => d.Victim != null
                                                            && bravoSteamIds.Contains(d.Victim.SteamID))
                                                            .Count();

                // assists this round
                int alphaAssists = deathsThisRound.Where(d => d.Assister != null
                                                                && alphaSteamIds.Contains(d.Assister.SteamID))
                                                                .Count();
                int bravoAssists = deathsThisRound.Where(d => d.Assister != null
                                                                && bravoSteamIds.Contains(d.Assister.SteamID))
                                                                .Count();

                // flash assists this round
                int alphaFlashAssists = deathsThisRound.Where(d => d.Assister != null
                                                                    && alphaSteamIds.Contains(d.Assister.SteamID)
                                                                    && d.AssistedFlash)
                                                                    .Count();
                int bravoFlashAssists = deathsThisRound.Where(d => d.Assister != null
                                                                    && bravoSteamIds.Contains(d.Assister.SteamID)
                                                                    && d.AssistedFlash)
                                                                    .Count();

                // headshots this round
                int alphaHeadshots = deathsThisRound.Where(d => d.Killer != null
                                                                && alphaSteamIds.Contains(d.Killer.SteamID)
                                                                && d.Headshot)
                                                                .Count();
                int bravoHeadshots = deathsThisRound.Where(d => d.Killer != null
                                                                && bravoSteamIds.Contains(d.Killer.SteamID)
                                                                && d.Headshot)
                                                                .Count();

                // teamkills this round
                int alphaTeamkills = deathsThisRound.Where(d => d.Killer != null
                                                            && d.Victim != null
                                                            && alphaSteamIds.Contains(d.Killer.SteamID)
                                                            && alphaSteamIds.Contains(d.Victim.SteamID)
                                                            && d.Killer.SteamID != d.Victim.SteamID)
                                                            .Count();
                int bravoTeamkills = deathsThisRound.Where(d => d.Killer != null
                                                            && d.Victim != null
                                                            && bravoSteamIds.Contains(d.Killer.SteamID)
                                                            && bravoSteamIds.Contains(d.Victim.SteamID)
                                                            && d.Killer.SteamID != d.Victim.SteamID)
                                                            .Count();

                // suicides this round
                int alphaSuicides = deathsThisRound.Where(d => d.Killer != null
                                                            && d.Victim != null
                                                            && alphaSteamIds.Contains(d.Killer.SteamID)
                                                            && d.Killer.SteamID != 0
                                                            && d.Suicide)
                                                            .Count();
                int bravoSuicides = deathsThisRound.Where(d => d.Killer != null
                                                            && d.Victim != null
                                                            && bravoSteamIds.Contains(d.Killer.SteamID)
                                                            && d.Killer.SteamID != 0
                                                            && d.Suicide)
                                                            .Count();

                // wallbang kills this round
                int alphaWallbangKills = deathsThisRound.Where(d => d.Killer != null
                                                                && alphaSteamIds.Contains(d.Killer.SteamID)
                                                                && d.PenetratedObjects > 0)
                                                                .Count();
                int bravoWallbangKills = deathsThisRound.Where(d => d.Killer != null
                                                                && bravoSteamIds.Contains(d.Killer.SteamID)
                                                                && d.PenetratedObjects > 0)
                                                                .Count();

                // total number of walls penetrated through for kills this round
                int alphaWallbangsTotalForAllKills = deathsThisRound.Where(d => d.Killer != null
                                                                            && alphaSteamIds.Contains(d.Killer.SteamID))
                                                                            .Select(d => d.PenetratedObjects)
                                                                            .DefaultIfEmpty()
                                                                            .Sum();


                int bravoWallbangsTotalForAllKills = deathsThisRound.Where(d => d.Killer != null
                                                                            && bravoSteamIds.Contains(d.Killer.SteamID))
                                                                            .Select(d => d.PenetratedObjects)
                                                                            .DefaultIfEmpty()
                                                                            .Sum();

                // most number of walls penetrated through in a single kill this round
                int alphaWallbangsMostInOneKill = deathsThisRound.Where(d => d.Killer != null
                                                                            && alphaSteamIds.Contains(d.Killer.SteamID))
                                                                            .Select(d => d.PenetratedObjects)
                                                                            .DefaultIfEmpty()
                                                                            .Max();
                int bravoWallbangsMostInOneKill = deathsThisRound.Where(d => d.Killer != null
                                                                            && bravoSteamIds.Contains(d.Killer.SteamID))
                                                                            .Select(d => d.PenetratedObjects)
                                                                            .DefaultIfEmpty()
                                                                            .Max();

                // shots fired this round
                var shotsFiredThisRound = shotsFiredValues["ShotsFired"].Where(s => s.Round == teamPlayers.Round);

                int alphaShotsFired = shotsFiredThisRound.Where(s => s.Shooter != null && alphaSteamIds.Contains(s.Shooter.SteamID)).Count();
                int bravoShotsFired = shotsFiredThisRound.Where(s => s.Shooter != null && bravoSteamIds.Contains(s.Shooter.SteamID)).Count();

                teamStats.Add(new TeamStats() {
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
                });
                
                currentRoundChecking++;
            }
            /* team stats end */


            AllStats allStats = new AllStats()
            {
                VersionNumber = versionNumber,
                SupportedGamemodes = supportedGamemodes,
                MapInfo = mapInfo,
                TanookiStats = tanookiStats,
                PlayerStats = playerStats,
                WinnersStats = winnersStats,
                RoundsStats = roundsStats,
                BombsiteStats = bombsiteStats,
                HostageStats = hostageStats,
                GrenadesTotalStats = grenadesTotalStats,
                GrenadesSpecificStats = grenadesSpecificStats,
                KillsStats = killsStats,
                FeedbackMessages = feedbackMessages,
                ChickenStats = chickenStats,
                TeamStats = teamStats,
            };

            /* JSON creation */
            Guid guid = Guid.NewGuid();
            string path = "matches/";
            if (mapDateString != string.Empty)
            {
                path += mapDateString + "_";
            }
            path += mapNameString + "_" + (noguid ? "" : guid.ToString("N")) + ".json";

            if (File.Exists(path))
                File.Delete(path);

            StreamWriter sw = new StreamWriter(path, false);

            string json = JsonConvert.SerializeObject(new
            {
                versionNumber,
                supportedGamemodes,
                mapInfo,
                tanookiStats,
                playerStats,
                winnersStats,
                roundsStats,
                bombsiteStats,
                hostageStats,
                grenadesTotalStats,
                grenadesSpecificStats,
                killsStats,
                feedbackMessages,
                chickenStats,
                teamStats,
            },
                Formatting.Indented
            );

            sw.WriteLine(json);
            /* JSON creation end*/

            sw.Close();
        }

        public long getSteamIdByPlayerName(Dictionary<long, Dictionary<string, string>> playerNames, string name)
        {
            if (name == "unconnected") return 0;

            var steamId = playerNames.Where(p => p.Value.Values.ElementAt(0) == name).Select(p => p.Key).FirstOrDefault(); // steamID will be 0 if not found

            return steamId;
        }

        public IEnumerable<object> selectWeaponsEventsByName(string name)
        {
            var shots = (from shot in getEvents<WeaponFiredEventArgs>()
                         where (shot as WeaponFiredEventArgs).Weapon.Weapon.ToString() == name
                         select shot);

            return shots;
        }

        public List<object> getEvents<T>()
        {
            Type t = typeof(T);

            if (this.events.ContainsKey(t))
                return this.events[t];

            return new List<object>();
        }

        public List<Team> getRoundsWonTeams(Dictionary<string, IEnumerable<Team>> teamValues)
        {
            var roundsWonTeams = teamValues["RoundsWonTeams"].ToList();
            roundsWonTeams.RemoveAll(r => !r.ToString().Equals("Terrorist")
                                       && !r.ToString().Equals("CounterTerrorist")
            );

            return roundsWonTeams;
        }

        public List<RoundEndReason> getRoundsWonReasons(Dictionary<string, IEnumerable<RoundEndReason>> roundEndReasonValues)
        {
            var roundsWonReasons = roundEndReasonValues["RoundsWonReasons"].ToList();
            roundsWonReasons.RemoveAll(r => !r.ToString().Equals(winReasonTKills)
                                         && !r.ToString().Equals(winReasonCtKills)
                                         && !r.ToString().Equals(winReasonBombed)
                                         && !r.ToString().Equals(winReasonDefused)
                                         && !r.ToString().Equals(winReasonRescued)
                                         && !r.ToString().Equals(winReasonNotRescued)
                                         && !r.ToString().Equals(winReasonTSaved)
            );

            return roundsWonReasons;
        }

        public static int getCurrentRoundNum(MatchData md)
        {
            int roundsCount = md.getEvents<RoundEndedEventArgs>().Count();
            List<TeamPlayers> teamPlayersList = md.getEvents<TeamPlayers>().Cast<TeamPlayers>().ToList();

            int round = 0;
            if (teamPlayersList.Count() > 0 && teamPlayersList.Any(t => t.Round == 1))
            {
                var teamPlayers = teamPlayersList.Where(t => t.Round == 1).First();
                if (teamPlayers.Terrorists.Count() > 0 && teamPlayers.CounterTerrorists.Count() > 0)
                {
                    round = roundsCount + 1;
                }
            }

            return round;
        }

        public int checkForUpdatedUserId(int userId)
        {
            int newUserId = playerReplacements.Where(u => u.Key == userId).Select(u => u.Value).FirstOrDefault();

            return (newUserId != 0) ? newUserId : userId;
        }
    }
}