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

        public bool passed = false;

        void addEvent(Type type, object ev)
        {
            //Create if doesnt exist
            if (!this.events.ContainsKey(type))
                this.events.Add(type, new List<object>());

            events[type].Add(ev);
        }

        void bindPlayer(Player p)
        {
            if (!playerTicks.ContainsKey(p.EntityID))
                playerTicks.Add(p.EntityID, new TickCounter());

            if (!playerLookups.ContainsKey(p.EntityID))
                if (p.SteamID != 0)
                    playerLookups.Add(p.EntityID, p.SteamID);

            if (playerTicks[p.EntityID].detectedName == "NOT FOUND" && p.Name != "" && p.Name != null)
                playerTicks[p.EntityID].detectedName = p.Name;
        }

        void addTick(Player p, PSTATUS status)
        {
            bindPlayer(p);

            if (status == PSTATUS.ONSERVER)
                playerTicks[p.EntityID].ticksOnServer++;

            if (status == PSTATUS.ALIVE)
                playerTicks[p.EntityID].ticksAlive++;

            if (status == PSTATUS.PLAYING)
                playerTicks[p.EntityID].ticksPlaying++;
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

                if (dp.Map != null && dp.Map.ToString() != string.Empty)
                {
                    e.Mapname = dp.Map.ToString();
                }
                else if (dp.Header.MapName != null && dp.Header.MapName.ToString() != string.Empty)
                {
                    e.Mapname = dp.Header.MapName.ToString();
                }
                else
                {
                    e.Mapname = "unknown";
                }

                List<FeedbackMessage> currentFeedbackMessages = new List<FeedbackMessage>();

                //stores all fb messages so that they aren't lost when stats are reset
                if (md.events.Count() > 0 && md.events.Any(k => k.Key.Name.ToString() == "FeedbackMessage"))
                {
                    foreach (FeedbackMessage message in md.events.Where(k => k.Key.Name.ToString() == "FeedbackMessage").Select(v => v.Value).ElementAt(0))
                    {
                        if (message.Message.ToLower().Contains(">fb ") || message.Message.ToLower().Contains(">feedback "))
                        {
                            currentFeedbackMessages.Add(new FeedbackMessage() { Round = message.Round, SteamID = message.SteamID, TeamName = message.TeamName, Message = message.Message });
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

                var rounds = md.getEvents<RoundEndedEventArgs>();
                var text = e.Text.ToString();

                if (text.ToLower().Contains(">fb ") || text.ToLower().Contains(">feedback "))
                {
                    var round = string.Empty;
                    if (rounds.Count() > 0)
                    {
                        round = "Round" + (rounds.Count());
                    }
                    else
                    {
                        round = "Warmup";
                    }

                    long steamId = e.Sender == null ? 0 : e.Sender.SteamID;

                    FeedbackMessage feedbackMessage = new FeedbackMessage() { Round = round, SteamID = steamId, TeamName = null, Message = text }; // works out TeamName in SaveFiles()

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

                //work out teams at current round
                var rounds = md.getEvents<RoundEndedEventArgs>();
                var players = dp.PlayingParticipants;

                TeamPlayers teamsEachRound = new TeamPlayers()
                {
                    Terrorists = players.Where(p => p.Team.ToString().Equals("Terrorist")).ToList(),
                    CounterTerrorists = players.Where(p => p.Team.ToString().Equals("CounterTerrorist")).ToList(),
                    Round = rounds.Count()
                };

                md.addEvent(typeof(TeamPlayers), teamsEachRound);
            };

            dp.SwitchSides += (object sender, SwitchSidesEventArgs e) => {
                int roundsCount = md.getEvents<RoundEndedEventArgs>().Count();

                SwitchSidesEventArgs switchSidesEventArgs = new SwitchSidesEventArgs() { RoundBeforeSwitch = roundsCount };

                md.addEvent(typeof(SwitchSidesEventArgs), switchSidesEventArgs);
            };

            dp.FreezetimeEnded += (object sender, FreezetimeEndedEventArgs e) => {
                md.addEvent(typeof(FreezetimeEndedEventArgs), e);

                var rounds = md.getEvents<RoundEndedEventArgs>();
                var players = dp.PlayingParticipants;

                TeamPlayers teamsEachRound = new TeamPlayers()
                {
                    Terrorists = players.Where(p => p.Team.ToString().Equals("Terrorist")).ToList(),
                    CounterTerrorists = players.Where(p => p.Team.ToString().Equals("CounterTerrorist")).ToList(),
                    Round = rounds.Count() - 1 //takes into account 1 warmup round
                };

                int tEquipValue = 0, ctEquipValue = 0;
                int tExpenditure = 0, ctExpenditure = 0;

                foreach (var player in teamsEachRound.Terrorists)
                {
                    tEquipValue += player.CurrentEquipmentValue; // player.FreezetimeEndEquipmentValue = 0 ???
                    tExpenditure += (player.CurrentEquipmentValue - player.RoundStartEquipmentValue); // (player.FreezetimeEndEquipmentValue = 0 - player.RoundStartEquipmentValue) ???
                }

                foreach (var player in teamsEachRound.CounterTerrorists)
                {
                    ctEquipValue += player.CurrentEquipmentValue; // player.FreezetimeEndEquipmentValue = 0 ???
                    ctExpenditure += (player.CurrentEquipmentValue - player.RoundStartEquipmentValue); // (player.FreezetimeEndEquipmentValue = 0 - player.RoundStartEquipmentValue) ???
                }

                TeamEquipmentStats teamEquipmentStats = new TeamEquipmentStats() { Round = rounds.Count() + 1, TEquipValue = tEquipValue, CTEquipValue = ctEquipValue, TExpenditure = tExpenditure, CTExpenditure = ctExpenditure };

                md.addEvent(typeof(TeamEquipmentStats), teamEquipmentStats);
            };

            // PLAYER EVENTS ===================================================
            dp.PlayerKilled += (object sender, PlayerKilledEventArgs e) => {
                md.addEvent(typeof(PlayerKilledEventArgs), e);
            };

            dp.RoundMVP += (object sender, RoundMVPEventArgs e) => {
                md.addEvent(typeof(RoundMVPEventArgs), e);
            };

            dp.PlayerDisconnect += (object sender, PlayerDisconnectEventArgs e) => {
                var rounds = md.getEvents<RoundEndedEventArgs>();

                DisconnectedPlayer disconnectedPlayer = new DisconnectedPlayer() { PlayerDisconnectEventArgs = e, Round = rounds.Count() - 1 };

                md.addEvent(typeof(DisconnectedPlayer), disconnectedPlayer);
            };

            // BOMB EVENTS =====================================================
            dp.BombPlanted += (object sender, BombEventArgs e) => {
                md.addEvent(typeof(BombEventArgs), e);
            };

            dp.BombDefused += (object sender, BombEventArgs e) => {
                md.addEvent(typeof(BombDefuseEventArgs), e);
            };

            // WEAPON EVENTS ===================================================
            dp.WeaponFired += (object sender, WeaponFiredEventArgs e) => {
                md.addEvent(typeof(WeaponFiredEventArgs), e);
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
            Dictionary<string, IEnumerable<Player>> playerValues, Dictionary<string, IEnumerable<Equipment>> weaponValues, Dictionary<string, IEnumerable<char>> bombsiteValues, Dictionary<string, IEnumerable<Team>> teamValues,
            Dictionary<string, IEnumerable<RoundEndReason>> roundEndReasonValues, Dictionary<string, IEnumerable<int>> roundLengthValues, Dictionary<string, IEnumerable<TeamEquipmentStats>> teamEquipmentValues, Dictionary<string, IEnumerable<NadeEventArgs>> grenadeValues,
            Dictionary<string, IEnumerable<ChickenKilledEventArgs>> chickenValues, bool writeTicks = true
        )
        {
            var mapDateSplit = (!string.IsNullOrWhiteSpace(demo[2]) && demo[2] != "unknown") ? demo[2].Split('/')  : null;
            var mapDateString = (mapDateSplit != null && mapDateSplit.Count() >= 3) ? (mapDateSplit[2] + "_" + mapDateSplit[0] + "_" + mapDateSplit[1]) : string.Empty;

            var mapNameSplit = matchStartValues["MatchStarts"].ElementAt(0).Mapname.Split('/');
            var mapNameString = mapNameSplit.Count() > 2 ? mapNameSplit[2] : mapNameSplit[0];

            Guid guid = Guid.NewGuid();
            string path = "matches/";
            if (mapDateString != string.Empty)
            {
                path += mapDateString + "_";
            }
            path += mapNameString + "_" + (noguid ? "" : guid.ToString("N")) + ".csv";

            if (File.Exists(path))
                File.Delete(path);

            StreamWriter sw = new StreamWriter(path, false);

            /* demo parser version */
            VersionNumber versionNumber = new VersionNumber();

            string header = "Version Number";
            string version = "0.0.14";

            sw.WriteLine(header);
            sw.WriteLine(version);

            versionNumber.Version = version;
            /* demo parser version end */

            /* Supported gamemodes */
            List<string> supportedGamemodes = new List<string>() { "Defuse", "Wingman" };

            sw.WriteLine(string.Empty);

            header = "Supported Gamemodes";

            sw.WriteLine(header);

            foreach (var gamemode in supportedGamemodes)
            {
                sw.WriteLine($"{ gamemode }");
            }
            /* Supported gamemodes end */

            /* map info */
            MapInfo mapInfo = new MapInfo() { MapName = matchStartValues["MatchStarts"].ElementAt(0).Mapname, TestDate = demo[2], TestType = demo[3] };

            mapNameSplit = matchStartValues["MatchStarts"].ElementAt(0).Mapname.Split('/');

            var mapNameInDemo = mapNameSplit[mapNameSplit.Count() - 1];
            if (mapNameInDemo != null && mapNameInDemo != string.Empty)
            {
                mapInfo.MapName = mapNameInDemo;
            }

            sw.WriteLine(string.Empty);

            header = "Mapname,Test Date,Test Type";
            string[] headerSplit = header.Split(',');

            sw.WriteLine(header);
            sw.WriteLine($"{ demo[1] },{ demo[2] },{ demo[3] }");
            /* map info end */

            /* tanooki leave stats */
            sw.WriteLine(string.Empty);

            header = "Tanooki Joined,Tanooki Left,Tanooki Round Joined,Tanooki Round Left,Tanooki Rounds Lasted";

            sw.WriteLine(header);
            sw.WriteLine($"{ tanookiStats.Joined },{ tanookiStats.Left },{ tanookiStats.RoundJoined },{ tanookiStats.RoundLeft },{ tanookiStats.RoundsLasted },");
            /* tanooki leave stats end */

            /* player stats */
            List<PlayerStats> playerStats = new List<PlayerStats>();

            sw.WriteLine(string.Empty);

            header = "Player Name,SteamID,";

            foreach (string catagory in playerValues.Keys)
            {
                header += catagory + ",";
            }

            if (writeTicks)
                header += "Ticks Alive,Ticks on Server,Ticks playing,";

            sw.WriteLine(header.Substring(0, header.Length - 1));

            Dictionary<long, Dictionary<string, long>> data = new Dictionary<long, Dictionary<string, long>>();
            Dictionary<long, Dictionary<string, string>> playerNames = new Dictionary<long, Dictionary<string, string>>();

            foreach (string catagory in playerValues.Keys)
            {
                foreach (Player p in playerValues[catagory])
                {
                    //Skip players not in this catagory
                    if (p == null)
                        continue;

                    if (!playerLookups.ContainsKey(p.EntityID))
                        continue;

                    //Add player to collections list if doesnt exist
                    if (!playerNames.ContainsKey(playerLookups[p.EntityID]))
                        playerNames.Add(playerLookups[p.EntityID], new Dictionary<string, string>());

                    if (!data.ContainsKey(playerLookups[p.EntityID]))
                        data.Add(playerLookups[p.EntityID], new Dictionary<string, long>());

                    //Add catagory to dictionary if doesnt exist
                    if (!playerNames[playerLookups[p.EntityID]].ContainsKey("Name"))
                        playerNames[playerLookups[p.EntityID]].Add("Name", p.Name);

                    if (!data[playerLookups[p.EntityID]].ContainsKey(catagory))
                        data[playerLookups[p.EntityID]].Add(catagory, 0);

                    //Increment it
                    data[playerLookups[p.EntityID]][catagory]++;
                }
            }

            // remove teamkills and suicides from kills (easy messy implementation)
            foreach (var kill in playerKilledEventsValues["PlayerKilledEvents"])
            {
                if (kill.Suicide)
                {
                    data[playerLookups[kill.Killer.EntityID]]["Kills"] -= 1;
                }
                else if (kill.TeamKill)
                {
                    data[playerLookups[kill.Killer.EntityID]]["Kills"] -= 2;
                }
            }

            int counter = 0;
            foreach (long player in data.Keys)
            {
                var match = playerNames.Where(p => p.Key.ToString() == player.ToString());
                var playerName = match.ElementAt(0).Value.ElementAt(0).Value;
                var steamID = match.ElementAt(0).Key;

                string playerLine = $"{ playerName },{ player },";

                List<int> statsList1 = new List<int>();
                foreach (string catagory in playerValues.Keys)
                {
                    if (data[player].ContainsKey(catagory))
                    {
                        playerLine += data[player][catagory] + ",";
                        statsList1.Add((int)data[player][catagory]);
                    }
                    else
                    {
                        playerLine += "0,";
                        statsList1.Add(0);
                    }
                }

                List<long> statsList2 = new List<long>();
                if (writeTicks)
                {
                    if (playerLookups.ContainsValue(player))
                    {
                        foreach (int entid in playerLookups.Keys)
                        {
                            if (playerLookups[entid] == player)
                            {
                                playerLine += this.playerTicks[entid].ticksAlive + ",";
                                statsList2.Add(this.playerTicks[entid].ticksAlive);

                                playerLine += this.playerTicks[entid].ticksOnServer + ",";
                                statsList2.Add(this.playerTicks[entid].ticksOnServer);

                                playerLine += this.playerTicks[entid].ticksPlaying + ",";
                                statsList2.Add(this.playerTicks[entid].ticksPlaying);

                                break;
                            }
                        }
                    }
                    else
                    {
                        playerLine += "0,0,0,";
                    }
                }

                string[] stats = playerLine.Split(',');

                int numOfKillsAsBot = playerKilledEventsValues["PlayerKilledEvents"].Where(k => (k.Killer != null) && (k.Killer.Name.ToString() == playerName.ToString()) && (k.KillerBotTakeover)).Count();
                int numOfDeathsAsBot = playerKilledEventsValues["PlayerKilledEvents"].Where(k => (k.Victim != null) && (k.Victim.Name.ToString() == playerName.ToString()) && (k.VictimBotTakeover)).Count();

                playerStats.Add(new PlayerStats()
                {
                    PlayerName = playerName,
                    SteamID = steamID,
                    Kills = statsList1.ElementAt(0) - numOfKillsAsBot,
                    KillsIncludingBots = statsList1.ElementAt(0),
                    Deaths = statsList1.ElementAt(1) - numOfDeathsAsBot,
                    DeathsIncludingBots = statsList1.ElementAt(1),
                    Headshots = statsList1.ElementAt(2),
                    Assists = statsList1.ElementAt(3),
                    MVPs = statsList1.ElementAt(4),
                    Shots = statsList1.ElementAt(5),
                    Plants = statsList1.ElementAt(6),
                    Defuses = statsList1.ElementAt(7),
                    TicksAlive = statsList2.ElementAt(0),
                    TicksOnServer = statsList2.ElementAt(1),
                    TicksPlaying = statsList2.ElementAt(2),
                });

                sw.WriteLine(playerLine.Substring(0, playerLine.Length - 1));

                counter++;
            }
            /* player stats end */

            /* winning team stats, round wins team and reason stats */
            WinnersStats winnersStats;
            List<RoundsStats> roundsStats = new List<RoundsStats>();

            // winning team & total rounds stats
            IEnumerable<SwitchSidesEventArgs> switchSides = switchSidesValues["SwitchSides"];
            var roundsWonReasons = getRoundsWonReasons(roundEndReasonValues);
            var roundsWonTeams = getRoundsWonTeams(teamValues);
            int totalRoundsWonTeamAlpha = 0, totalRoundsWonTeamBeta = 0;

            List<string> roundStatsStrings = new List<string>();

            for (int i = 0; i < roundsWonTeams.Count(); i++)
            {
                string reason = string.Empty;
                string half = string.Empty;
                bool isOvertime = ((switchSides.Count() >= 2) && (i >= switchSides.ElementAt(1).RoundBeforeSwitch)) ? true : false;
                int overtimeNum = 0;
                int roundLength = roundLengthValues["RoundsLengths"].ElementAt(i);

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
                    half = (i < switchSides.ElementAt(0).RoundBeforeSwitch) ? "First" : "Second";
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
                const string tKills = "TerroristWin", ctKills = "CTWin", bombed = "TargetBombed", defused = "BombDefused", timeout = "TargetSaved";

                switch (roundsWonReasons[i].ToString())
                {
                    case tKills:
                        reason = "T Kills";
                        break;
                    case ctKills:
                        reason = "CT Kills";
                        break;
                    case bombed:
                        reason = "Bombed";
                        break;
                    case defused:
                        reason = "Defused";
                        break;
                    case timeout:
                        reason = "Timeout";
                        break;
                }

                //equip values
                var teamEquipValues = teamEquipmentValues["TeamEquipmentStats"].Count() >= i ? teamEquipmentValues["TeamEquipmentStats"].ElementAt(i) : null;
                int equipValueTeamA = (teamEquipValues != null) ? (half == "First" ? teamEquipValues.TEquipValue : teamEquipValues.CTEquipValue) : 0;
                int equipValueTeamB = (teamEquipValues != null) ? (half == "First" ? teamEquipValues.CTEquipValue : teamEquipValues.TEquipValue) : 0;
                int expenditureTeamA = (teamEquipValues != null) ? (half == "First" ? teamEquipValues.TExpenditure : teamEquipValues.CTExpenditure) : 0;
                int expenditureTeamB = (teamEquipValues != null) ? (half == "First" ? teamEquipValues.CTExpenditure : teamEquipValues.TExpenditure) : 0;

                roundStatsStrings.Add($"Round{ i + 1 },{ half },{ overtimeNum }, { roundLength } { roundsWonTeams[i].ToString() },{ reason },{ equipValueTeamA },{ equipValueTeamB },{ expenditureTeamA },{ expenditureTeamB }");

                roundsStats.Add(new RoundsStats()
                {
                    Round = $"Round{ i + 1 }",
                    Half = half,
                    Overtime = overtimeNum,
                    Length = roundLength,
                    Winners = roundsWonTeams[i].ToString(),
                    WinMethod = reason,
                    TeamAlphaEquipValue = equipValueTeamA,
                    TeamBetaEquipValue = equipValueTeamB,
                    TeamAlphaExpenditure = expenditureTeamA,
                    TeamBetaExpenditure = expenditureTeamB,
                });
            }

            // work out winning team
            string winningTeam = (totalRoundsWonTeamAlpha >= totalRoundsWonTeamBeta) ? (totalRoundsWonTeamAlpha > totalRoundsWonTeamBeta) ? "Team Alpha" : "Draw" : "Team Bravo";

            // winners stats
            sw.WriteLine(string.Empty);

            header = "Winning Team,Team Alpha Rounds,Team Bravo Rounds";
            sw.WriteLine(header);
            sw.WriteLine($"{ winningTeam },{ totalRoundsWonTeamAlpha },{ totalRoundsWonTeamBeta }");

            winnersStats = new WinnersStats() { WinningTeam = winningTeam, TeamAlphaRounds = totalRoundsWonTeamAlpha, TeamBetaRounds = totalRoundsWonTeamBeta };

            // rounds stats
            sw.WriteLine(string.Empty);

            header = "Round,Half,Overtime,Length,Winners,Win Method,Alpha Equip Value,Bravo Equip Value,Alpha Expenditure,Bravo Expenditure";
            sw.WriteLine(header);

            foreach (var roundString in roundStatsStrings)
            {
                sw.WriteLine(roundString);
            }
            /* winning team stats, round wins team and reason stats end */

            /* bombsite stats */
            List<BombsiteStats> bombsiteStats = new List<BombsiteStats>();

            sw.WriteLine(string.Empty);

            List<char> bombsitePlants = new List<char>(bombsiteValues.ElementAt(0).Value);
            List<char> bombsiteDefuses = new List<char>(bombsiteValues.ElementAt(1).Value);

            header = "Bombsite,Plants,Defuses";
            sw.WriteLine(header);

            sw.WriteLine($"A,{ bombsitePlants.Where(b => b.ToString().Equals("A")).Count() },{ bombsiteDefuses.Where(b => b.ToString().Equals("A")).Count() }");
            sw.WriteLine($"B,{ bombsitePlants.Where(b => b.ToString().Equals("B")).Count() },{ bombsiteDefuses.Where(b => b.ToString().Equals("B")).Count() }");

            bombsiteStats.Add(new BombsiteStats() { Bombsite = 'A', Plants = bombsitePlants.Where(b => b.ToString().Equals("A")).Count(), Defuses = bombsiteDefuses.Where(b => b.ToString().Equals("A")).Count() });
            bombsiteStats.Add(new BombsiteStats() { Bombsite = 'B', Plants = bombsitePlants.Where(b => b.ToString().Equals("B")).Count(), Defuses = bombsiteDefuses.Where(b => b.ToString().Equals("B")).Count() });
            /* bombsite stats end */

            /* Grenades total stats */
            List<GrenadesTotalStats> grenadesTotalStats = new List<GrenadesTotalStats>();

            sw.WriteLine(string.Empty);

            string[] nadeTypes = { "Flash", "Smoke", "HE", "Incendiary", "Decoy" };

            List<NadeEventArgs> nades = new List<NadeEventArgs>(grenadeValues.ElementAt(0).Value);
            var flashes = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[0]));
            var smokes = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[1]));
            var hegrenades = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[2]));
            var incendiaries = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[3]));
            var decoys = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[4]));

            List<IEnumerable<NadeEventArgs>> nadeGroups = new List<IEnumerable<NadeEventArgs>>() { flashes, smokes, hegrenades, incendiaries, decoys };

            header = "Nade Type,Amount Used";
            sw.WriteLine(header);

            for (int i = 0; i < nadeTypes.Count(); i++)
            {
                sw.WriteLine($"{ nadeTypes[i] },{ nadeGroups.ElementAt(i).Count() }");

                grenadesTotalStats.Add(new GrenadesTotalStats() { NadeType = nadeTypes[i], AmountUsed = nadeGroups.ElementAt(i).Count() });
            }
            /* Grenades total stats end */

            /* Grenades specific stats */
            List<GrenadesSpecificStats> grenadesSpecificStats = new List<GrenadesSpecificStats>();

            sw.WriteLine(string.Empty);

            header = "Nade Type,SteamID,X Position,Y Position,Z Position,Num Players Flashed";

            sw.WriteLine(header);

            foreach (var nadeGroup in nadeGroups)
            {
                if (nadeGroup.Count() > 0)
                {
                    bool flashGroup = nadeGroup.ElementAt(0).NadeType.ToString() == nadeTypes[0] ? true : false; //check if in flash group

                    foreach (var nade in nadeGroup)
                    {
                        string[] positionSplit = nade.Position.ToString().Split(new string[] { "{X: ", ", Y: ", ", Z: ", "}" }, StringSplitOptions.None);
                        string positions = $"{ positionSplit[1] },{ positionSplit[2] },{ positionSplit[3] }";


                        if (flashGroup)
                        {
                            var flash = nade as FlashEventArgs;
                            int numOfPlayersFlashed = flash.FlashedPlayers.Count();

                            sw.WriteLine($"{ nade.NadeType.ToString() },{ nade.ThrownBy.SteamID.ToString() },{ positions },{ numOfPlayersFlashed }");

                            grenadesSpecificStats.Add(new GrenadesSpecificStats() { NadeType = nade.NadeType.ToString(), SteamID = nade.ThrownBy.SteamID, XPosition = double.Parse(positionSplit[1]), YPosition = double.Parse(positionSplit[2]), ZPosition = double.Parse(positionSplit[3]), NumPlayersFlashed = numOfPlayersFlashed });
                        }
                        else
                        {
                            sw.WriteLine($"{ nade.NadeType.ToString() },{ nade.ThrownBy.SteamID.ToString() },{ positions }");

                            grenadesSpecificStats.Add(new GrenadesSpecificStats() { NadeType = nade.NadeType.ToString(), SteamID = nade.ThrownBy.SteamID, XPosition = double.Parse(positionSplit[1]), YPosition = double.Parse(positionSplit[2]), ZPosition = double.Parse(positionSplit[3]) });
                        }
                    }
                }
            }
            /* Grenades specific stats end */

            /* Player Kills/Death Positions */
            List<PlayerPositionStats> playerPositionStats = new List<PlayerPositionStats>();

            sw.WriteLine(string.Empty);

            header = "Kill X Position,Kill Y Position,Kill Z Position,Death X Position,Death Y Position,Death Z Position,Weapon";
            sw.WriteLine(header);

            List<Player> kills = new List<Player>(playerValues["Kills"].ToList());
            List<Player> deaths = new List<Player>(playerValues["Deaths"].ToList());

            List<Equipment> weaponKillers = new List<Equipment>(weaponValues["WeaponKillers"].ToList());

            for (int i = 0; i < deaths.Count(); i++)
            {
                if (kills.ElementAt(i) != null && kills.ElementAt(i).Position != null && deaths.ElementAt(i) != null && deaths.ElementAt(i).Position != null)
                {
                    string[] killPositionSplit = kills.ElementAt(i).Position.ToString().Split(new string[] { "{X: ", ", Y: ", ", Z: ", "}" }, StringSplitOptions.None);
                    string killPositions = $"{ killPositionSplit[1] },{ killPositionSplit[2] },{ killPositionSplit[3] }";

                    string[] deathPositionSplit = deaths.ElementAt(i).Position.ToString().Split(new string[] { "{X: ", ", Y: ", ", Z: ", "}" }, StringSplitOptions.None);
                    string deathPositions = $"{ deathPositionSplit[1] },{ deathPositionSplit[2] },{ deathPositionSplit[3] }";

                    var weaponUsed = weaponKillers.ElementAt(i).Weapon.ToString();

                    if (weaponUsed == null || weaponUsed == string.Empty)
                    {
                        weaponUsed = weaponKillers.ElementAt(i).OriginalString.ToString();
                    }

                    sw.WriteLine($"{ killPositions },{ deathPositions },{ weaponUsed }");

                    playerPositionStats.Add(new PlayerPositionStats()
                    {
                        XPositionKill = double.Parse(killPositionSplit[1]),
                        YPositionKill = double.Parse(killPositionSplit[2]),
                        ZPositionKill = double.Parse(killPositionSplit[3]),
                        XPositionDeath = double.Parse(deathPositionSplit[1]),
                        YPositionDeath = double.Parse(deathPositionSplit[2]),
                        ZPositionDeath = double.Parse(deathPositionSplit[3]),
                        Weapon = weaponUsed,
                    });
                }
            }
            /* Player Kills/Death Positions end */

            /* Feedback Messages */
            List<FeedbackMessage> feedbackMessages = new List<FeedbackMessage>();

            sw.WriteLine(string.Empty);

            header = "Player,Message";

            sw.WriteLine(header);

            foreach (var message in messagesValues["Messages"])
            {
                int roundNum = 0;
                if (message.Round != "Warmup")
                {
                    roundNum = int.Parse(message.Round.Remove(0, 5));
                }

                var currentRoundTeams = (roundNum == 0) ? teamPlayersValues["TeamPlayers"].ElementAt(0) : teamPlayersValues["TeamPlayers"].ElementAt(roundNum - 1);

                if (currentRoundTeams.Terrorists.Any(p => p.SteamID == message.SteamID))
                {
                    message.TeamName = "Terrorist";
                }
                else if (currentRoundTeams.CounterTerrorists.Any(p => p.SteamID == message.SteamID))
                {
                    message.TeamName = "CounterTerrorist";
                }

                sw.WriteLine($"{ message.Round },{ message.SteamID },{ message.TeamName },{ message.Message }");

                feedbackMessages.Add(message);
            }
            /* Feedback Messages end */

            /* chickens killed stats */
            ChickenStats chickenStats = new ChickenStats();

            sw.WriteLine(string.Empty);

            chickenStats.Killed = chickenValues.Count();

            header = "Killed";
            sw.WriteLine(header);

            sw.WriteLine($"{ chickenStats.Killed }");
            /* chickens killed stats end */

            sw.Close();

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
                GrenadesTotalStats = grenadesTotalStats,
                GrenadesSpecificStats = grenadesSpecificStats,
                PlayerPositionStats = playerPositionStats,
                FeedbackMessages = feedbackMessages,
                ChickenStats = chickenStats,
            };

            /* JSON creation */
            path = "matches/";
            if (mapDateString != string.Empty)
            {
                path += mapDateString + "_";
            }
            path += mapNameString + "_" + (noguid ? "" : guid.ToString("N")) + ".json";

            if (File.Exists(path))
                File.Delete(path);

            StreamWriter sw2 = new StreamWriter(path, false);

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
                grenadesTotalStats,
                grenadesSpecificStats,
                playerPositionStats,
                feedbackMessages,
                chickenStats,
            },
                Formatting.Indented
            );

            sw2.WriteLine(json);
            /* JSON creation end*/

            sw2.Close();
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
            const string tKills = "TerroristWin", ctKills = "CTWin", bombed = "TargetBombed", defused = "BombDefused", timeout = "TargetSaved";

            var roundsWonReasons = roundEndReasonValues["RoundsWonReasons"].ToList();
            roundsWonReasons.RemoveAll(r => !r.ToString().Equals(tKills)
                                         && !r.ToString().Equals(ctKills)
                                         && !r.ToString().Equals(bombed)
                                         && !r.ToString().Equals(defused)
                                         && !r.ToString().Equals(timeout)
            );

            return roundsWonReasons;
        }
    }
}