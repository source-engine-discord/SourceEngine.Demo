using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json;

using SourceEngine.Demo.Parser;
using SourceEngine.Demo.Parser.Constants;
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
        public long ticksAlive = 0;
        public long ticksOnServer = 0;
        public long ticksPlaying = 0;
    }

    public class PlayerWeapon
    {
        public string name;
    }

    public class MatchData
    {
        private const string winReasonTKills = "TerroristWin",
            winReasonCtKills = "CTWin",
            winReasonBombed = "TargetBombed",
            winReasonDefused = "BombDefused",
            winReasonRescued = "HostagesRescued",
            winReasonNotRescued = "HostagesNotRescued",
            winReasonTSaved = "TargetSaved",
            winReasonDangerZone = "SurvivalWin";

        private const string
            winReasonUnknown = "Unknown"; // Caused by an error where the round_end event was not triggered for a round

        private static DemoParser dp;

        public bool
            changingPlantedRoundsToA = false,
            changingPlantedRoundsToB =
                false; // Used in ValidateBombsite() for knowing when a bombsite plant site has been changed from '?' to an actual bombsite letter

        public Dictionary<Type, List<object>> events = new();

        public bool passed = false;
        public readonly Dictionary<int, long> playerLookups = new();
        public readonly Dictionary<int, int> playerReplacements = new();

        private readonly Dictionary<int, TickCounter> playerTicks = new();

        private void addEvent(Type type, object ev)
        {
            //Create if doesn't exist
            if (!events.ContainsKey(type))
                events.Add(type, new List<object>());

            events[type].Add(ev);
        }

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
                    var duplicate = playerLookups.FirstOrDefault(x => x.Value == p.SteamID);

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
                        var keysToReplaceValue = playerReplacements.Where(r => r.Value == idRemoved).Select(r => r.Key);

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

        public static MatchData FromDemoFile(
            DemoInformation demoInformation,
            bool parseChickens,
            bool parsePlayerPositions,
            int hostagerescuezonecountoverride,
            bool lowOutputMode)
        {
            string file = demoInformation.DemoName;
            string gamemode = demoInformation.GameMode;
            string testType = demoInformation.TestType;

            MatchData md = new MatchData();

            //Create demo parser instance
            dp = new DemoParser(
                File.OpenRead(file),
                parseChickens,
                parsePlayerPositions,
                gamemode,
                hostagerescuezonecountoverride
            );

            dp.ParseHeader();

            dp.PlayerBind += (object sender, PlayerBindEventArgs e) => { md.BindPlayer(e.Player); };

            dp.PlayerPositions += (object sender, PlayerPositionsEventArgs e) =>
            {
                foreach (PlayerPositionEventArgs playerPosition in e.PlayerPositions)
                {
                    if (md.events.Count > 0
                        && md.events.Any(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs"))
                    {
                        int round = GetCurrentRoundNum(md, gamemode);

                        if (round > 0 && playerPosition.Player.SteamID > 0)
                        {
                            bool playerAlive = CheckIfPlayerAliveAtThisPointInRound(md, playerPosition.Player, round);
                            var freezetimeEndedEvents = md.events
                                .Where(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs").Select(v => v.Value)
                                .ElementAt(0);

                            var freezetimeEndedEventLast =
                                (FreezetimeEndedEventArgs)freezetimeEndedEvents.LastOrDefault();

                            var freezetimeEndedThisRound = freezetimeEndedEvents.Count >= round;

                            if (playerAlive && freezetimeEndedThisRound)
                            {
                                var teamSide = playerPosition.Player.Team.ToString().ToLower() == "terrorist"
                                    ? "T"
                                    : "CT";

                                var playerPositionsInstance = new PlayerPositionsInstance
                                {
                                    Round = round,
                                    TimeInRound = (int)e.CurrentTime - (int)freezetimeEndedEventLast.TimeEnd,
                                    SteamID = playerPosition.Player.SteamID,
                                    TeamSide = teamSide,
                                    XPosition = playerPosition.Player.Position.X,
                                    YPosition = playerPosition.Player.Position.Y,
                                    ZPosition = playerPosition.Player.Position.Z,
                                };

                                md.addEvent(typeof(PlayerPositionsInstance), playerPositionsInstance);
                            }
                        }
                    }
                }
            };

            // SERVER EVENTS ===================================================
            dp.MatchStarted += (object sender, MatchStartedEventArgs e) =>
            {
                List<FeedbackMessage> currentfeedbackMessages = new List<FeedbackMessage>();

                //stores all fb messages so that they aren't lost when stats are reset
                if (md.events.Count > 0 && md.events.Any(k => k.Key.Name.ToString() == "FeedbackMessage"))
                    foreach (FeedbackMessage message in md.events.Where(k => k.Key.Name.ToString() == "FeedbackMessage")
                        .Select(v => v.Value).ElementAt(0))
                    {
                        var text = message.Message;

                        if (IsMessageFeedback(text))

                            //Sets round to 0 as anything before a match start event should always be classed as warmup
                            currentfeedbackMessages.Add(
                                new FeedbackMessage
                                {
                                    Round = 0,
                                    SteamID = message.SteamID,
                                    TeamName = message.TeamName,
                                    XCurrentPosition = message.XCurrentPosition,
                                    YCurrentPosition = message.YCurrentPosition,
                                    ZCurrentPosition = message.ZCurrentPosition,
                                    XLastAlivePosition = message.XLastAlivePosition,
                                    YLastAlivePosition = message.YLastAlivePosition,
                                    ZLastAlivePosition = message.ZLastAlivePosition,
                                    XCurrentViewAngle = message.XCurrentViewAngle,
                                    YCurrentViewAngle = message.YCurrentViewAngle,
                                    SetPosCommandCurrentPosition = message.SetPosCommandCurrentPosition,
                                    Message = message.Message,
                                    TimeInRound =
                                        0, // overwrites whatever the TimeInRound value was before, 0 is generally used for messages sent in Warmup
                                }
                            );
                    }

                md.events = new Dictionary<Type, List<object>>(); //resets all stats stored

                md.addEvent(typeof(MatchStartedEventArgs), e);

                //adds all stored fb messages back
                foreach (var feedbackMessage in currentfeedbackMessages)
                    md.addEvent(typeof(FeedbackMessage), feedbackMessage);

                //print rounds complete out to console
                if (!lowOutputMode)
                {
                    int roundsCount = md.GetEvents<RoundOfficiallyEndedEventArgs>().Count;

                    Console.WriteLine("\n");
                    Console.WriteLine("Match restarted.");
                }
            };

            dp.ChickenKilled += (object sender, ChickenKilledEventArgs e) =>
            {
                md.addEvent(typeof(ChickenKilledEventArgs), e);
            };

            dp.SayText2 += (object sender, SayText2EventArgs e) =>
            {
                md.addEvent(typeof(SayText2EventArgs), e);

                var text = e.Text.ToString();

                if (IsMessageFeedback(text))
                {
                    int round = GetCurrentRoundNum(md, gamemode);

                    long steamId = e.Sender == null ? 0 : e.Sender.SteamID;

                    Player player = null;

                    if (steamId != 0)
                        player = dp.Participants.FirstOrDefault(p => p.SteamID == steamId);
                    else
                        player = null;

                    var teamName = player != null ? player.Team.ToString() : null;
                    teamName = teamName == "Spectate" ? "Spectator" : teamName;

                    bool playerAlive = CheckIfPlayerAliveAtThisPointInRound(md, player, round);
                    string[] currentPositions = SplitPositionString(player?.Position.ToString());
                    string[] lastAlivePositions =
                        playerAlive ? null : SplitPositionString(player?.LastAlivePosition.ToString());

                    string setPosCurrentPosition = GenerateSetPosCommand(
                        currentPositions,
                        player?.ViewDirectionX,
                        player?.ViewDirectionY
                    );

                    var roundsOfficiallyEndedEvents =
                        md.events.Any(k => k.Key.Name.ToString() == "RoundOfficiallyEndedEventArgs")
                            ? md.events.Where(k => k.Key.Name.ToString() == "RoundOfficiallyEndedEventArgs")
                                .Select(v => v.Value).ElementAt(0)
                            : null;

                    var freezetimesEndedEvents = md.events.Any(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs")
                        ? md.events.Where(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs").Select(v => v.Value)
                            .ElementAt(0)
                        : null;

                    int numOfRoundsOfficiallyEnded = roundsOfficiallyEndedEvents?.Count > 0
                        ? roundsOfficiallyEndedEvents.Count
                        : 0;

                    int numOfFreezetimesEnded =
                        freezetimesEndedEvents?.Count > 0 ? freezetimesEndedEvents.Count : 0;

                    float timeInRound = 0; // Stays as '0' if sent during freezetime

                    if (numOfFreezetimesEnded > numOfRoundsOfficiallyEnded)
                    {
                        var freezetimeEnded =
                            (FreezetimeEndedEventArgs)freezetimesEndedEvents
                                .LastOrDefault(); // would it be better to use '.OrderByDescending(f => f.TimeEnd).FirstOrDefault()' ?

                        timeInRound = dp.CurrentTime - freezetimeEnded.TimeEnd;
                    }

                    FeedbackMessage feedbackMessage = new FeedbackMessage
                    {
                        Round = round,
                        SteamID = steamId,
                        TeamName = teamName, // works out TeamName in GetFeedbackMessages() if it is null
                        XCurrentPosition = double.Parse(currentPositions[0]),
                        YCurrentPosition = double.Parse(currentPositions[1]),
                        ZCurrentPosition = double.Parse(currentPositions[2]),
                        XLastAlivePosition =
                            lastAlivePositions != null ? (double?)double.Parse(lastAlivePositions[0]) : null,
                        YLastAlivePosition =
                            lastAlivePositions != null ? (double?)double.Parse(lastAlivePositions[1]) : null,
                        ZLastAlivePosition =
                            lastAlivePositions != null ? (double?)double.Parse(lastAlivePositions[2]) : null,
                        XCurrentViewAngle = player?.ViewDirectionX,
                        YCurrentViewAngle = player?.ViewDirectionY,
                        SetPosCommandCurrentPosition = setPosCurrentPosition,
                        Message = text,
                        TimeInRound =
                            timeInRound, // counts messages sent after the round_end event fires as the next round, set to '0' as if it was the next round's warmup (done this way instead of using round starts to avoid potential issues when restarting rounds)
                    };

                    md.addEvent(typeof(FeedbackMessage), feedbackMessage);
                }
            };

            dp.RoundEnd += (object sender, RoundEndedEventArgs e) =>
            {
                var roundsEndedEvents = md.events.Where(k => k.Key.Name.ToString() == "RoundEndedEventArgs")
                    .Select(v => v.Value);

                var roundsOfficiallyEndedEvents = md.events
                    .Where(k => k.Key.Name.ToString() == "RoundOfficiallyEndedEventArgs").Select(v => v.Value);

                var freezetimesEndedEvents = md.events.Where(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs")
                    .Select(v => v.Value);

                int numOfRoundsEnded = roundsEndedEvents.Any() ? roundsEndedEvents.ElementAt(0).Count : 0;
                int numOfRoundsOfficiallyEnded = roundsOfficiallyEndedEvents.Any()
                    ? roundsOfficiallyEndedEvents.ElementAt(0).Count
                    : 0;

                int numOfFreezetimesEnded =
                    freezetimesEndedEvents.Any() ? freezetimesEndedEvents.ElementAt(0).Count : 0;

                //Console.WriteLine("dp.RoundEnd -- " + numOfRoundsEnded + " - " + numOfRoundsOfficiallyEnded + " - " + numOfFreezetimesEnded);

                // if round_officially_ended event did not get fired in this round due to error
                while (numOfRoundsEnded > numOfRoundsOfficiallyEnded)
                {
                    var roundEndedEvent =
                        (RoundEndedEventArgs)roundsEndedEvents.ElementAt(0).ElementAt(numOfRoundsOfficiallyEnded);

                    dp.RaiseRoundOfficiallyEnded(
                        new RoundOfficiallyEndedEventArgs // adds the missing RoundOfficiallyEndedEvent
                        {
                            Message = roundEndedEvent.Message,
                            Reason = roundEndedEvent.Reason,
                            Winner = roundEndedEvent.Winner,
                            Length = roundEndedEvent.Length + 4, // guesses the round length
                        }
                    );

                    numOfRoundsOfficiallyEnded = roundsOfficiallyEndedEvents.ElementAt(0).Count;
                }

                // if round_freeze_end event did not get fired in this round due to error
                while (numOfRoundsEnded >= numOfFreezetimesEnded)
                {
                    dp.RaiseFreezetimeEnded(
                        new FreezetimeEndedEventArgs
                        {
                            TimeEnd = -1, // no idea when this actually ended without guessing
                        }
                    );

                    numOfFreezetimesEnded = freezetimesEndedEvents.ElementAt(0).Count;

                    // set the TimeInRound value to '-1' for any feedback messages sent this round, as it will be wrong
                    if (md.events.Any(k => k.Key.Name.ToString() == "FeedbackMessage"))
                        foreach (FeedbackMessage message in md.events
                            .Where(k => k.Key.Name.ToString() == "FeedbackMessage").Select(v => v.Value)?.ElementAt(0))
                        {
                            if (message.Round == numOfFreezetimesEnded)
                                message.TimeInRound = -1;
                        }
                }

                md.addEvent(typeof(RoundEndedEventArgs), e);
            };

            dp.RoundOfficiallyEnded += (object sender, RoundOfficiallyEndedEventArgs e) =>
            {
                var roundsEndedEvents = md.events.Where(k => k.Key.Name.ToString() == "RoundEndedEventArgs")
                    .Select(v => v.Value);

                var roundsOfficiallyEndedEvents = md.events
                    .Where(k => k.Key.Name.ToString() == "RoundOfficiallyEndedEventArgs").Select(v => v.Value);

                var freezetimesEndedEvents = md.events.Where(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs")
                    .Select(v => v.Value);

                int numOfRoundsEnded = roundsEndedEvents.Any() ? roundsEndedEvents.ElementAt(0).Count : 0;
                int numOfRoundsOfficiallyEnded = roundsOfficiallyEndedEvents.Any()
                    ? roundsOfficiallyEndedEvents.ElementAt(0).Count
                    : 0;

                int numOfFreezetimesEnded =
                    freezetimesEndedEvents.Any() ? freezetimesEndedEvents.ElementAt(0).Count : 0;

                //Console.WriteLine("dp.RoundOfficiallyEnded -- " + numOfRoundsEnded + " - " + numOfRoundsOfficiallyEnded + " - " + numOfFreezetimesEnded);

                // if round_end event did not get fired in this round due to error
                while (numOfRoundsOfficiallyEnded >= numOfRoundsEnded)
                {
                    dp.RaiseRoundEnd(
                        new RoundEndedEventArgs
                        {
                            Winner = Team.Unknown,
                            Message = "Unknown",
                            Reason = RoundEndReason.Unknown,
                            Length = 0,
                        }
                    );

                    numOfRoundsEnded = roundsEndedEvents.ElementAt(0).Count;
                }

                // if round_freeze_end event did not get fired in this round due to error
                while (numOfRoundsOfficiallyEnded >= numOfFreezetimesEnded)
                {
                    dp.RaiseFreezetimeEnded(
                        new FreezetimeEndedEventArgs
                        {
                            TimeEnd = -1, // no idea when this actually ended without guessing
                        }
                    );

                    numOfFreezetimesEnded = freezetimesEndedEvents.ElementAt(0).Count;

                    // set the TimeInRound value to '-1' for any feedback messages sent this round, as it will be wrong
                    if (md.events.Any(k => k.Key.Name.ToString() == "FeedbackMessage"))
                        foreach (FeedbackMessage message in md.events
                            .Where(k => k.Key.Name.ToString() == "FeedbackMessage").Select(v => v.Value)?.ElementAt(0))
                        {
                            if (message.Round == numOfFreezetimesEnded)
                                message.TimeInRound = -1;
                        }
                }

                // update round length round_end event for this round
                var roundEndedEvent = (RoundEndedEventArgs)roundsEndedEvents.ElementAt(0).LastOrDefault();
                e.Message = roundEndedEvent.Message;
                e.Reason = roundEndedEvent.Reason;
                e.Winner = roundEndedEvent.Winner;

                md.addEvent(typeof(RoundOfficiallyEndedEventArgs), e);

                //print rounds complete out to console
                if (!lowOutputMode)
                {
                    int roundsCount = md.GetEvents<RoundOfficiallyEndedEventArgs>().Count;

                    //stops the progress bar getting in the way of the first row
                    if (roundsCount == 1)
                        Console.WriteLine("\n");

                    Console.WriteLine("Round " + roundsCount + " complete.");
                }
            };

            dp.SwitchSides += (object sender, SwitchSidesEventArgs e) =>
            {
                int roundsCount = md.GetEvents<RoundOfficiallyEndedEventArgs>().Count;

                SwitchSidesEventArgs switchSidesEventArgs =
                    new SwitchSidesEventArgs
                    {
                        RoundBeforeSwitch = roundsCount + 1,
                    }; // announce_phase_end event occurs before round_officially_ended event

                md.addEvent(typeof(SwitchSidesEventArgs), switchSidesEventArgs);
            };

            dp.FreezetimeEnded += (object sender, FreezetimeEndedEventArgs e) =>
            {
                var freezetimesEndedEvents = md.events.Where(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs")
                    .Select(v => v.Value);

                var roundsEndedEvents = md.events.Where(k => k.Key.Name.ToString() == "RoundEndedEventArgs")
                    .Select(v => v.Value);

                var roundsOfficiallyEndedEvents = md.events
                    .Where(k => k.Key.Name.ToString() == "RoundOfficiallyEndedEventArgs").Select(v => v.Value);

                int numOfFreezetimesEnded =
                    freezetimesEndedEvents.Any() ? freezetimesEndedEvents.ElementAt(0).Count : 0;

                int numOfRoundsEnded = roundsEndedEvents.Any() ? roundsEndedEvents.ElementAt(0).Count : 0;
                int numOfRoundsOfficiallyEnded = roundsOfficiallyEndedEvents.Any()
                    ? roundsOfficiallyEndedEvents.ElementAt(0).Count
                    : 0;

                //Console.WriteLine("dp.FreezetimeEnded -- Ended: " + numOfRoundsEnded + " - " + numOfRoundsOfficiallyEnded + " - " + numOfFreezetimesEnded);

                /*	The final round in a match does not throw a round_officially_ended event, but a round_freeze_end event is thrown after the game ends,
                    so assume that a game has ended if a second round_freeze_end event is found in the same round as a round_end_event and NO round_officially_ended event.
                    This does mean that if a round_officially_ended event is not triggered due to demo error, the parse will mess up. */
                var minRoundsForWin = GetMinRoundsForWin(gamemode, testType);

                if (numOfFreezetimesEnded == numOfRoundsOfficiallyEnded + 1 && numOfFreezetimesEnded == numOfRoundsEnded
                    && numOfRoundsEnded >= minRoundsForWin)
                {
                    Console.WriteLine("Assuming the parse has finished.");

                    var roundEndedEvent =
                        (RoundEndedEventArgs)roundsEndedEvents.ElementAt(0).ElementAt(numOfRoundsOfficiallyEnded);

                    dp.RaiseRoundOfficiallyEnded(
                        new RoundOfficiallyEndedEventArgs // adds the missing RoundOfficiallyEndedEvent
                        {
                            Reason = roundEndedEvent.Reason,
                            Message = roundEndedEvent.Message,
                            Winner = roundEndedEvent.Winner,
                            Length = roundEndedEvent.Length + 4, // guesses the round length
                        }
                    );

                    numOfRoundsOfficiallyEnded = roundsOfficiallyEndedEvents.ElementAt(0).Count;

                    dp.stopParsingDemo =
                        true; // forcefully stops the demo from being parsed any further to avoid events

                    // (such as player deaths to world) happening in a next round (a round that never actually occurs)

                    return;
                }

                // if round_end event did not get fired in the previous round due to error
                while (numOfFreezetimesEnded > numOfRoundsEnded)
                {
                    dp.RaiseRoundEnd(
                        new RoundEndedEventArgs
                        {
                            Winner = Team.Unknown,
                            Message = "Unknown",
                            Reason = RoundEndReason.Unknown,
                            Length = 0,
                        }
                    );

                    numOfRoundsEnded = roundsEndedEvents.ElementAt(0).Count;
                }

                // if round_officially_ended event did not get fired in the previous round due to error
                while (numOfFreezetimesEnded > numOfRoundsOfficiallyEnded)
                {
                    var roundEndedEvent =
                        (RoundEndedEventArgs)roundsEndedEvents.ElementAt(0).ElementAt(numOfRoundsOfficiallyEnded);

                    dp.RaiseRoundOfficiallyEnded(
                        new RoundOfficiallyEndedEventArgs // adds the missing RoundOfficiallyEndedEvent
                        {
                            Reason = roundEndedEvent.Reason,
                            Message = roundEndedEvent.Message,
                            Winner = roundEndedEvent.Winner,
                            Length = roundEndedEvent.Length + 4, // guesses the round length
                        }
                    );

                    numOfRoundsOfficiallyEnded = roundsOfficiallyEndedEvents.ElementAt(0).Count;
                }

                md.addEvent(typeof(FreezetimeEndedEventArgs), e);

                //work out teams at current round
                int roundsCount = md.GetEvents<RoundOfficiallyEndedEventArgs>().Count;
                var players = dp.PlayingParticipants;

                TeamPlayers teams = new TeamPlayers
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
                    tExpenditure +=
                        player.CurrentEquipmentValue
                        - player
                            .RoundStartEquipmentValue; // (player.FreezetimeEndEquipmentValue = 0 - player.RoundStartEquipmentValue) ???
                }

                foreach (var player in teams.CounterTerrorists)
                {
                    ctEquipValue += player.CurrentEquipmentValue; // player.FreezetimeEndEquipmentValue = 0 ???
                    ctExpenditure +=
                        player.CurrentEquipmentValue
                        - player
                            .RoundStartEquipmentValue; // (player.FreezetimeEndEquipmentValue = 0 - player.RoundStartEquipmentValue) ???
                }

                TeamEquipment teamEquipmentStats = new TeamEquipment
                {
                    Round = roundsCount + 1,
                    TEquipValue = tEquipValue,
                    CTEquipValue = ctEquipValue,
                    TExpenditure = tExpenditure,
                    CTExpenditure = ctExpenditure,
                };

                md.addEvent(typeof(TeamEquipment), teamEquipmentStats);
            };

            // PLAYER EVENTS ===================================================
            dp.PlayerKilled += (object sender, PlayerKilledEventArgs e) =>
            {
                e.Round = GetCurrentRoundNum(md, gamemode);

                md.addEvent(typeof(PlayerKilledEventArgs), e);
            };

            dp.PlayerHurt += (object sender, PlayerHurtEventArgs e) =>
            {
                var round = GetCurrentRoundNum(md, gamemode);

                if (e.PossiblyKilledByBombExplosion
                ) // a player_death event is not triggered due to death by bomb explosion
                {
                    var playerKilledEventArgs = new PlayerKilledEventArgs
                    {
                        Round = round,
                        TimeInRound = e.TimeInRound,
                        Killer = e.Attacker,
                        KillerBotTakeover = false, // ?
                        Victim = e.Player,
                        VictimBotTakeover = false, // ?
                        Assister = null,
                        AssisterBotTakeover = false, // ?
                        Suicide = false,
                        TeamKill = false,
                        PenetratedObjects = 0,
                        Headshot = false,
                        AssistedFlash = false,
                    };

                    md.addEvent(typeof(PlayerKilledEventArgs), playerKilledEventArgs);
                }

                var player = new Player(e.Player);
                var attacker = new Player(e.Attacker);

                var playerHurt = new PlayerHurt
                {
                    Round = round,
                    TimeInRound = e.TimeInRound,
                    Player = player,
                    XPositionPlayer = player.Position.X,
                    YPositionPlayer = player.Position.Y,
                    ZPositionPlayer = player.Position.Z,
                    Attacker = attacker ?? null,
                    XPositionAttacker = attacker != null && attacker.Position != null ? attacker.Position.X : 0,
                    YPositionAttacker = attacker != null && attacker.Position != null ? attacker.Position.Y : 0,
                    ZPositionAttacker = attacker != null && attacker.Position != null ? attacker.Position.Z : 0,
                    Health = e.Health,
                    Armor = e.Armor,
                    Weapon = attacker != null ? e.Weapon : null,
                    HealthDamage = e.HealthDamage,
                    ArmorDamage = e.ArmorDamage,
                    Hitgroup = e.Hitgroup,
                    PossiblyKilledByBombExplosion = e.PossiblyKilledByBombExplosion,
                };

                md.addEvent(typeof(PlayerHurt), playerHurt);
            };

            dp.RoundMVP += (object sender, RoundMVPEventArgs e) => { md.addEvent(typeof(RoundMVPEventArgs), e); };

            dp.PlayerDisconnect += (object sender, PlayerDisconnectEventArgs e) =>
            {
                if (e.Player != null && e.Player.Name != "unconnected" && e.Player.Name != "GOTV")
                {
                    int round = GetCurrentRoundNum(md, gamemode);

                    DisconnectedPlayer disconnectedPlayer = new DisconnectedPlayer
                    {
                        PlayerDisconnectEventArgs = e,
                        Round = round - 1,
                    };

                    md.addEvent(typeof(DisconnectedPlayer), disconnectedPlayer);
                }
            };

            // BOMB EVENTS =====================================================
            dp.BombPlanted += (object sender, BombEventArgs e) =>
            {
                int roundsCount = md.GetEvents<RoundOfficiallyEndedEventArgs>().Count;

                BombPlanted bombPlanted = new BombPlanted
                {
                    Round = roundsCount + 1,
                    TimeInRound = e.TimeInRound,
                    Player = e.Player,
                    Bombsite = e.Site,
                };

                md.addEvent(typeof(BombPlanted), bombPlanted);
            };

            dp.BombExploded += (object sender, BombEventArgs e) =>
            {
                int roundsCount = md.GetEvents<RoundOfficiallyEndedEventArgs>().Count;

                BombExploded bombExploded = new BombExploded
                {
                    Round = roundsCount + 1,
                    TimeInRound = e.TimeInRound,
                    Player = e.Player,
                    Bombsite = e.Site,
                };

                md.addEvent(typeof(BombExploded), bombExploded);
            };

            dp.BombDefused += (object sender, BombEventArgs e) =>
            {
                int roundsCount = md.GetEvents<RoundOfficiallyEndedEventArgs>().Count;

                BombDefused bombDefused = new BombDefused
                {
                    Round = roundsCount + 1,
                    TimeInRound = e.TimeInRound,
                    Player = e.Player,
                    Bombsite = e.Site,
                    HasKit = e.Player.HasDefuseKit,
                };

                md.addEvent(typeof(BombDefused), bombDefused);
            };

            // HOSTAGE EVENTS =====================================================
            dp.HostageRescued += (object sender, HostageRescuedEventArgs e) =>
            {
                int roundsCount = md.GetEvents<RoundOfficiallyEndedEventArgs>().Count;

                HostageRescued hostageRescued = new HostageRescued
                {
                    Round = roundsCount + 1,
                    TimeInRound = e.TimeInRound,
                    Player = e.Player,
                    Hostage = e.Hostage,
                    HostageIndex = e.HostageIndex,
                    RescueZone = e.RescueZone,
                };

                md.addEvent(typeof(HostageRescued), hostageRescued);
            };

            // HOSTAGE EVENTS =====================================================
            dp.HostagePickedUp += (object sender, HostagePickedUpEventArgs e) =>
            {
                int roundsCount = md.GetEvents<RoundOfficiallyEndedEventArgs>().Count;

                HostagePickedUp hostagePickedUp = new HostagePickedUp
                {
                    Round = roundsCount + 1,
                    TimeInRound = e.TimeInRound,
                    Player = e.Player,
                    Hostage = e.Hostage,
                    HostageIndex = e.HostageIndex,
                };

                md.addEvent(typeof(HostagePickedUp), hostagePickedUp);
            };

            // WEAPON EVENTS ===================================================
            dp.WeaponFired += (object sender, WeaponFiredEventArgs e) =>
            {
                md.addEvent(typeof(WeaponFiredEventArgs), e);

                var round = GetCurrentRoundNum(md, gamemode);

                ShotFired shotFired = new ShotFired
                {
                    Round = round,
                    TimeInRound = e.TimeInRound,
                    Shooter = e.Shooter,
                    TeamSide = e.Shooter.Team.ToString(),
                    Weapon = new Equipment(e.Weapon),
                };

                md.addEvent(typeof(ShotFired), shotFired);
            };

            // GRENADE EVENTS ==================================================
            dp.ExplosiveNadeExploded += (object sender, GrenadeEventArgs e) =>
            {
                md.addEvent(typeof(GrenadeEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.FireNadeStarted += (object sender, FireEventArgs e) =>
            {
                md.addEvent(typeof(FireEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.SmokeNadeStarted += (object sender, SmokeEventArgs e) =>
            {
                md.addEvent(typeof(SmokeEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.FlashNadeExploded += (object sender, FlashEventArgs e) =>
            {
                md.addEvent(typeof(FlashEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.DecoyNadeStarted += (object sender, DecoyEventArgs e) =>
            {
                md.addEvent(typeof(DecoyEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            // PLAYER TICK HANDLER ============================================
            dp.TickDone += (object sender, TickDoneEventArgs e) =>
            {
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

            if (!lowOutputMode)
            {
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
                catch (Exception)
                {
                    pv.Error();
                }
            }
            else
            {
                try
                {
                    dp.ParseToEnd();

                    md.passed = true;
                }
                catch (Exception) { }
            }

            dp.Dispose();

            return md;
        }

        public AllOutputData CreateFiles(ProcessedData processedData, bool createJsonFile = true)
        {
            var mapDateSplit =
                !string.IsNullOrWhiteSpace(processedData.DemoInformation.TestDate)
                && processedData.DemoInformation.TestDate != "unknown"
                    ? processedData.DemoInformation.TestDate.Split('/')
                    : null;

            var mapDateString = mapDateSplit != null && mapDateSplit.Length >= 3
                ? mapDateSplit[2] + "_" + mapDateSplit[0] + "_" + mapDateSplit[1]
                : string.Empty;

            var mapNameSplit = processedData.MatchStartValues.Any()
                ? processedData.MatchStartValues.ElementAt(0).Mapname.Split('/')
                : new string[] { processedData.DemoInformation.MapName };

            var mapNameString = mapNameSplit.Length > 2 ? mapNameSplit[2] : mapNameSplit[0];

            var dataAndPlayerNames = GetDataAndPlayerNames(processedData);

            PlayerPositionsStats playerPositionsStats = null;

            AllStats allStats = new AllStats
            {
                versionNumber = GetVersionNumber(),
                supportedGamemodes = GetSupportedGamemodes(),
                mapInfo = GetMapInfo(processedData, mapNameSplit),
                tanookiStats = processedData.tanookiStats,
            };

            if (CheckIfStatsShouldBeCreated("playerStats", processedData.DemoInformation.GameMode))
                allStats.playerStats = GetPlayerStats(
                    processedData,
                    dataAndPlayerNames.Data,
                    dataAndPlayerNames.PlayerNames
                );

            var generalroundsStats = GetGeneralRoundsStats(processedData, dataAndPlayerNames.PlayerNames);
            if (CheckIfStatsShouldBeCreated("winnersStats", processedData.DemoInformation.GameMode))
                allStats.winnersStats = generalroundsStats.winnersStats;

            if (CheckIfStatsShouldBeCreated("roundsStats", processedData.DemoInformation.GameMode))
                allStats.roundsStats = generalroundsStats.roundsStats;

            if (CheckIfStatsShouldBeCreated("bombsiteStats", processedData.DemoInformation.GameMode))
                allStats.bombsiteStats = GetBombsiteStats(processedData);

            if (CheckIfStatsShouldBeCreated("hostageStats", processedData.DemoInformation.GameMode))
                allStats.hostageStats = GetHostageStats(processedData);

            if (CheckIfStatsShouldBeCreated("rescueZoneStats", processedData.DemoInformation.GameMode))
                allStats.rescueZoneStats = GetRescueZoneStats();

            string[] nadeTypes = { "Flash", "Smoke", "HE", "Incendiary", "Decoy" };
            var nadeGroups = GetNadeGroups(processedData, nadeTypes);
            if (CheckIfStatsShouldBeCreated("grenadesTotalStats", processedData.DemoInformation.GameMode))
                allStats.grenadesTotalStats = GetGrenadesTotalStats(nadeGroups, nadeTypes);

            if (CheckIfStatsShouldBeCreated("grenadesSpecificStats", processedData.DemoInformation.GameMode))
                allStats.grenadesSpecificStats = GetGrenadesSpecificStats(
                    nadeGroups,
                    nadeTypes,
                    dataAndPlayerNames.PlayerNames
                );

            if (CheckIfStatsShouldBeCreated("killsStats", processedData.DemoInformation.GameMode))
                allStats.killsStats = GetKillsStats(processedData, dataAndPlayerNames.PlayerNames);

            if (CheckIfStatsShouldBeCreated("feedbackMessages", processedData.DemoInformation.GameMode))
                allStats.feedbackMessages = GetFeedbackMessages(processedData, dataAndPlayerNames.PlayerNames);

            if (processedData.ParseChickens && CheckIfStatsShouldBeCreated(
                "chickenStats",
                processedData.DemoInformation.GameMode
            ))
                allStats.chickenStats = GetChickenStats(processedData);

            if (CheckIfStatsShouldBeCreated("teamStats", processedData.DemoInformation.GameMode))
                allStats.teamStats = GetTeamStats(
                    processedData,
                    allStats,
                    dataAndPlayerNames.PlayerNames,
                    generalroundsStats.SwitchSides
                );

            if (CheckIfStatsShouldBeCreated("firstDamageStats", processedData.DemoInformation.GameMode))
                allStats.firstDamageStats = GetFirstDamageStats(processedData);

            // JSON creation
            if (createJsonFile)
                CreateJsonAllStats(processedData, allStats, mapNameString, mapDateString);

            if (processedData.ParsePlayerPositions && CheckIfStatsShouldBeCreated(
                "playerPositionsStats",
                processedData.DemoInformation.GameMode
            ))
            {
                playerPositionsStats = GetPlayerPositionsStats(processedData, allStats);
                CreateJsonPlayerPositionsStats(
                    processedData,
                    allStats,
                    playerPositionsStats,
                    mapNameString,
                    mapDateString
                );
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
            Dictionary<long, Dictionary<string, long>> data = new Dictionary<long, Dictionary<string, long>>();
            Dictionary<long, Dictionary<string, string>> playerNames =
                new Dictionary<long, Dictionary<string, string>>();

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

        public static List<string> GetSupportedGamemodes()
        {
            return Gamemodes.GetAll();
        }

        public static mapInfo GetMapInfo(ProcessedData processedData, string[] mapNameSplit)
        {
            mapInfo mapInfo = new mapInfo
            {
                MapName = processedData.DemoInformation.MapName,
                TestType = processedData.DemoInformation.TestType,
                TestDate = processedData.DemoInformation.TestDate,
            };

            mapInfo.MapName =
                mapNameSplit.Length > 2
                    ? mapNameSplit[2]
                    : mapInfo.MapName; // use the map name from inside the demo itself if possible, otherwise use the map name from the demo file's name

            mapInfo.WorkshopID = mapNameSplit.Length > 2 ? mapNameSplit[1] : "unknown";
            mapInfo.DemoName =
                processedData.DemoInformation.DemoName.Split('\\').Last()
                    .Replace(
                        ".dem",
                        string.Empty
                    ); // the filename of the demo, for Faceit games this is also in the "demo_url" value

            // attempts to get the game mode
            var roundsWonReasons = GetRoundsWonReasons(processedData.RoundEndReasonValues);

            // use the provided game mode if given as a parameter
            if (!string.IsNullOrWhiteSpace(processedData.DemoInformation.GameMode)
                && processedData.DemoInformation.GameMode.ToLower() != "notprovided")
            {
                mapInfo.GameMode = processedData.DemoInformation.GameMode;

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
                mapInfo.GameMode = Gamemodes.DangerZone;
            }
            else if (processedData.TeamPlayersValues.Any(
                t => t.Terrorists.Count > 2
                    && processedData.TeamPlayersValues.Any(ct => ct.CounterTerrorists.Count > 2)
            ))
            {
                if (dp.bombsiteAIndex > -1 || dp.bombsiteBIndex > -1
                    || processedData.MatchStartValues.Any(m => m.HasBombsites))
                    mapInfo.GameMode = Gamemodes.Defuse;
                else if ((dp.hostageAIndex > -1 || dp.hostageBIndex > -1)
                    && !processedData.MatchStartValues.Any(m => m.HasBombsites))
                    mapInfo.GameMode = Gamemodes.Hostage;
                else // what the hell is this game mode ??
                    mapInfo.GameMode = Gamemodes.Unknown;
            }
            else
            {
                if (dp.bombsiteAIndex > -1 || dp.bombsiteBIndex > -1
                    || processedData.MatchStartValues.Any(m => m.HasBombsites))
                    mapInfo.GameMode = Gamemodes.WingmanDefuse;
                else if ((dp.hostageAIndex > -1 || dp.hostageBIndex > -1)
                    && !processedData.MatchStartValues.Any(m => m.HasBombsites))
                    mapInfo.GameMode = Gamemodes.WingmanHostage;
                else // what the hell is this game mode ??
                    mapInfo.GameMode = Gamemodes.Unknown;
            }

            return mapInfo;
        }

        public List<playerStats> GetPlayerStats(
            ProcessedData processedData,
            Dictionary<long, Dictionary<string, long>> data,
            Dictionary<long, Dictionary<string, string>> playerNames)
        {
            List<playerStats> playerStats = new List<playerStats>();

            // remove team kills and suicides from kills (easy messy implementation)
            foreach (var kill in processedData.PlayerKilledEventsValues)
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

            int counter = 0;

            foreach (long player in data.Keys)
            {
                var match = playerNames.Where(p => p.Key.ToString() == player.ToString());
                var playerName = match.ElementAt(0).Value.ElementAt(0).Value;
                var steamID = match.ElementAt(0).Key;

                List<int> statsList1 = new List<int>();

                foreach (string catagory in processedData.PlayerValues.Keys)
                {
                    if (data[player].ContainsKey(catagory))
                        statsList1.Add((int)data[player][catagory]);
                    else
                        statsList1.Add(0);
                }

                List<long> statsList2 = new List<long>();

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

                counter++;
            }

            return playerStats;
        }

        public GeneralroundsStats GetGeneralRoundsStats(
            ProcessedData processedData,
            Dictionary<long, Dictionary<string, string>> playerNames)
        {
            List<roundsStats> roundsStats = new List<roundsStats>();

            // winning team & total rounds stats
            IEnumerable<SwitchSidesEventArgs> switchSides = processedData.SwitchSidesValues;
            var roundsWonTeams = GetRoundsWonTeams(processedData.TeamValues);
            var roundsWonReasons = GetRoundsWonReasons(processedData.RoundEndReasonValues);
            int totalRoundsWonTeamAlpha = 0, totalRoundsWonTeamBeta = 0;

            for (int i = 0; i < roundsWonTeams.Count; i++)
            {
                if (roundsWonReasons.Count > i) // game was abandoned early
                {
                    string reason = string.Empty;
                    string half = string.Empty;
                    bool isOvertime = switchSides.Count() >= 2 && i >= switchSides.ElementAt(1).RoundBeforeSwitch
                        ? true
                        : false;

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
                        if (roundsWonTeams.ElementAt(i).ToString() == "Terrorist")
                            totalRoundsWonTeamAlpha++;
                        else if (roundsWonTeams.ElementAt(i).ToString() == "CounterTerrorist")
                            totalRoundsWonTeamBeta++;
                    }
                    else
                    {
                        if (roundsWonTeams.ElementAt(i).ToString() == "Terrorist")
                            totalRoundsWonTeamBeta++;
                        else if (roundsWonTeams.ElementAt(i).ToString() == "CounterTerrorist")
                            totalRoundsWonTeamAlpha++;
                    }

                    //win method
                    reason = roundsWonReasons[i].ToString() switch
                    {
                        winReasonTKills => "T Kills",
                        winReasonCtKills => "CT Kills",
                        winReasonBombed => "Bombed",
                        winReasonDefused => "Defused",
                        winReasonRescued => "HostagesRescued",
                        winReasonNotRescued => "HostagesNotRescued",
                        winReasonTSaved => "TSaved",
                        winReasonDangerZone => "Danger Zone Won",
                        winReasonUnknown => "Unknown",
                        _ => reason,
                    };

                    // team count values
                    int roundNum = i + 1;
                    var currentRoundTeams =
                        processedData.TeamPlayersValues.FirstOrDefault(t => t.Round == roundNum);

                    foreach (var player in currentRoundTeams.Terrorists) // make sure steamID's aren't 0
                    {
                        player.SteamID = player.SteamID == 0
                            ? GetSteamIdByPlayerName(playerNames, player.Name)
                            : player.SteamID;
                    }

                    foreach (var player in currentRoundTeams.CounterTerrorists) // make sure steamID's aren't 0
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
                    var teamEquipValues = processedData.TeamEquipmentValues.Count() >= i
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
                    BombPlanted bombPlanted = null;
                    BombExploded bombExploded = null;
                    BombDefused bombDefused = null;
                    BombPlantedError bombPlantedError = null;

                    if (processedData.BombsitePlantValues.Any(p => p.Round == roundNum))
                    {
                        bombPlanted = processedData.BombsitePlantValues.FirstOrDefault(p => p.Round == roundNum);

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
                        string[] positions = SplitPositionString(bombPlanted.Player.LastAlivePosition.ToString());
                        bombPlanted.XPosition = double.Parse(positions[0]);
                        bombPlanted.YPosition = double.Parse(positions[1]);
                        bombPlanted.ZPosition = double.Parse(positions[2]);
                    }

                    if (processedData.BombsiteExplodeValues.Any(p => p.Round == roundNum))
                    {
                        bombExploded = processedData.BombsiteExplodeValues.FirstOrDefault(p => p.Round == roundNum);

                        bombsite = bombsite != null ? bombsite :
                            bombExploded.Bombsite == null ? null : bombExploded.Bombsite.ToString();
                    }

                    if (processedData.BombsiteDefuseValues.Any(p => p.Round == roundNum))
                    {
                        bombDefused = processedData.BombsiteDefuseValues.FirstOrDefault(p => p.Round == roundNum);

                        bombsite = bombsite != null ? bombsite :
                            bombDefused.Bombsite == null ? null : bombDefused.Bombsite.ToString();
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
                            var newHostagePickedUpValues = processedData.HostagePickedUpValues.ToList();
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
                            var newHostagePickedUpValues = processedData.HostagePickedUpValues.ToList();
                            newHostagePickedUpValues.Add(hostagePickedUpB);
                            processedData.HostagePickedUpValues = newHostagePickedUpValues;
                        }

                        //rescue position
                        string[] positionsRescueA = hostageRescuedA != null
                            ? SplitPositionString(hostageRescuedA.Player.LastAlivePosition.ToString())
                            : null;

                        if (positionsRescueA != null)
                        {
                            hostageRescuedA.XPosition = double.Parse(positionsRescueA[0]);
                            hostageRescuedA.YPosition = double.Parse(positionsRescueA[1]);
                            hostageRescuedA.ZPosition = double.Parse(positionsRescueA[2]);
                        }

                        string[] positionsRescueB = hostageRescuedB != null
                            ? SplitPositionString(hostageRescuedB.Player.LastAlivePosition.ToString())
                            : null;

                        if (positionsRescueB != null)
                        {
                            hostageRescuedB.XPosition = double.Parse(positionsRescueB[0]);
                            hostageRescuedB.YPosition = double.Parse(positionsRescueB[1]);
                            hostageRescuedB.ZPosition = double.Parse(positionsRescueB[2]);
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
            List<bombsiteStats> bombsiteStats = new List<bombsiteStats>();

            var bombsiteATrigger = dp?.triggers.Count > 0
                ? dp.triggers.FirstOrDefault(x => x.Index == dp.bombsiteAIndex)
                : null;

            var bombsiteBTrigger = dp?.triggers.Count > 0
                ? dp.triggers.FirstOrDefault(x => x.Index == dp.bombsiteBIndex)
                : null;

            List<char> bombsitePlants = new List<char>(processedData.BombsitePlantValues.Select(x => (char)x.Bombsite));
            List<char> bombsiteExplosions =
                new List<char>(processedData.BombsiteExplodeValues.Select(x => (char)x.Bombsite));

            List<char> bombsiteDefuses =
                new List<char>(processedData.BombsiteDefuseValues.Select(x => (char)x.Bombsite));

            int plantsA = bombsitePlants.Count(b => b.ToString().Equals("A"));
            int explosionsA = bombsiteExplosions.Count(b => b.ToString().Equals("A"));
            int defusesA = bombsiteDefuses.Count(b => b.ToString().Equals("A"));

            int plantsB = bombsitePlants.Count(b => b.ToString().Equals("B"));
            int explosionsB = bombsiteExplosions.Count(b => b.ToString().Equals("B"));
            int defusesB = bombsiteDefuses.Count(b => b.ToString().Equals("B"));

            bombsiteStats.Add(
                new bombsiteStats
                {
                    Bombsite = 'A',
                    Plants = plantsA,
                    Explosions = explosionsA,
                    Defuses = defusesA,
                    XPositionMin = bombsiteATrigger?.Min.X,
                    YPositionMin = bombsiteATrigger?.Min.Y,
                    ZPositionMin = bombsiteATrigger?.Min.Z,
                    XPositionMax = bombsiteATrigger?.Max.X,
                    YPositionMax = bombsiteATrigger?.Max.Y,
                    ZPositionMax = bombsiteATrigger?.Max.Z,
                }
            );

            bombsiteStats.Add(
                new bombsiteStats
                {
                    Bombsite = 'B',
                    Plants = plantsB,
                    Explosions = explosionsB,
                    Defuses = defusesB,
                    XPositionMin = bombsiteBTrigger?.Min.X,
                    YPositionMin = bombsiteBTrigger?.Min.Y,
                    ZPositionMin = bombsiteBTrigger?.Min.Z,
                    XPositionMax = bombsiteBTrigger?.Max.X,
                    YPositionMax = bombsiteBTrigger?.Max.Y,
                    ZPositionMax = bombsiteBTrigger?.Max.Z,
                }
            );

            return bombsiteStats;
        }

        public static List<hostageStats> GetHostageStats(ProcessedData processedData)
        {
            List<hostageStats> hostageStats = new List<hostageStats>();

            var hostageIndexA = processedData.HostageRescueValues.FirstOrDefault(r => r.Hostage == 'A')
                ?.HostageIndex;

            var hostageIndexB = processedData.HostageRescueValues.FirstOrDefault(r => r.Hostage == 'B')
                ?.HostageIndex;

            List<char> hostagePickedUps = new List<char>(processedData.HostagePickedUpValues.Select(x => x.Hostage));
            List<char> hostageRescues = new List<char>(processedData.HostageRescueValues.Select(x => x.Hostage));

            int pickedUpsA = hostagePickedUps.Count(b => b.ToString().Equals("A"));
            int pickedUpsB = hostagePickedUps.Count(b => b.ToString().Equals("B"));

            int rescuesA = hostageRescues.Count(b => b.ToString().Equals("A"));
            int rescuesB = hostageRescues.Count(b => b.ToString().Equals("B"));

            hostageStats.Add(
                new hostageStats
                {
                    Hostage = 'A',
                    HostageIndex = hostageIndexA,
                    PickedUps = pickedUpsA,
                    Rescues = rescuesA,
                }
            );

            hostageStats.Add(
                new hostageStats
                {
                    Hostage = 'B',
                    HostageIndex = hostageIndexB,
                    PickedUps = pickedUpsB,
                    Rescues = rescuesB,
                }
            );

            return hostageStats;
        }

        public static List<rescueZoneStats> GetRescueZoneStats()
        {
            List<rescueZoneStats> rescueZoneStats = new List<rescueZoneStats>();

            if (dp?.triggers?.Count > 0)
                foreach (var rescueZone in dp.triggers.Where(
                    x => x.Index != dp.bombsiteAIndex && x.Index != dp.bombsiteBIndex
                ))
                {
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

        public static List<IEnumerable<NadeEventArgs>> GetNadeGroups(ProcessedData processedData, string[] nadeTypes)
        {
            var flashes = processedData.GrenadeValues.Where(f => f.NadeType.ToString().Equals(nadeTypes[0]));
            var smokes = processedData.GrenadeValues.Where(f => f.NadeType.ToString().Equals(nadeTypes[1]));
            var hegrenades = processedData.GrenadeValues.Where(f => f.NadeType.ToString().Equals(nadeTypes[2]));
            var incendiaries = processedData.GrenadeValues.Where(
                f => f.NadeType.ToString().Equals(nadeTypes[3]) || f.NadeType.ToString().Equals("Molotov")
            ); // should never be "Molotov" as all molotovs are down as incendiaries, specified why in DemoParser.cs, search for "FireNadeStarted".

            var decoys = processedData.GrenadeValues.Where(f => f.NadeType.ToString().Equals(nadeTypes[4]));

            return new List<IEnumerable<NadeEventArgs>>
            {
                flashes,
                smokes,
                hegrenades,
                incendiaries,
                decoys,
            };
        }

        public static List<grenadesTotalStats> GetGrenadesTotalStats(
            List<IEnumerable<NadeEventArgs>> nadeGroups,
            string[] nadeTypes)
        {
            List<grenadesTotalStats> grenadesTotalStats = new List<grenadesTotalStats>();

            for (int i = 0; i < nadeTypes.Length; i++)
            {
                grenadesTotalStats.Add(
                    new grenadesTotalStats
                    {
                        NadeType = nadeTypes[i],
                        AmountUsed = nadeGroups.ElementAt(i).Count(),
                    }
                );
            }

            return grenadesTotalStats;
        }

        public static List<grenadesSpecificStats> GetGrenadesSpecificStats(
            IEnumerable<IEnumerable<NadeEventArgs>> nadeGroups,
            string[] nadeTypes,
            Dictionary<long, Dictionary<string, string>> playerNames)
        {
            List<grenadesSpecificStats> grenadesSpecificStats = new List<grenadesSpecificStats>();

            foreach (var nadeGroup in nadeGroups)
            {
                if (nadeGroup.Any())
                {
                    bool flashGroup =
                        nadeGroup.ElementAt(0).NadeType.ToString() == nadeTypes[0]
                            ? true
                            : false; //check if in flash group

                    foreach (var nade in nadeGroup)
                    {
                        string[] positions = SplitPositionString(nade.Position.ToString());

                        //retrieve steam ID using player name if the event does not return it correctly
                        long steamId = nade.ThrownBy.SteamID == 0
                            ? GetSteamIdByPlayerName(playerNames, nade.ThrownBy.Name)
                            : nade.ThrownBy.SteamID;

                        if (flashGroup)
                        {
                            var flash = nade as FlashEventArgs;
                            int numOfPlayersFlashed = flash.FlashedPlayers.Length;

                            grenadesSpecificStats.Add(
                                new grenadesSpecificStats
                                {
                                    NadeType = nade.NadeType.ToString(),
                                    SteamID = steamId,
                                    XPosition = double.Parse(positions[0]),
                                    YPosition = double.Parse(positions[1]),
                                    ZPosition = double.Parse(positions[2]),
                                    NumPlayersFlashed = numOfPlayersFlashed,
                                }
                            );
                        }
                        else
                        {
                            grenadesSpecificStats.Add(
                                new grenadesSpecificStats
                                {
                                    NadeType = nade.NadeType.ToString(),
                                    SteamID = steamId,
                                    XPosition = double.Parse(positions[0]),
                                    YPosition = double.Parse(positions[1]),
                                    ZPosition = double.Parse(positions[2]),
                                }
                            );
                        }
                    }
                }
            }

            return grenadesSpecificStats;
        }

        public static List<killsStats> GetKillsStats(
            ProcessedData processedData,
            Dictionary<long, Dictionary<string, string>> playerNames)
        {
            List<killsStats> killsStats = new List<killsStats>();

            List<Player> kills = new List<Player>(processedData.PlayerValues["Kills"].ToList());
            List<Player> deaths = new List<Player>(processedData.PlayerValues["Deaths"].ToList());

            List<Equipment> weaponKillers = new List<Equipment>(processedData.WeaponValues.ToList());
            List<int> penetrations = new List<int>(processedData.PenetrationValues.ToList());

            for (int i = 0; i < deaths.Count; i++)
            {
                if (kills.ElementAt(i) != null && kills.ElementAt(i).LastAlivePosition != null
                    && deaths.ElementAt(i) != null && deaths.ElementAt(i).LastAlivePosition != null)
                {
                    var playerKilledEvent = processedData.PlayerKilledEventsValues.ElementAt(i);

                    if (playerKilledEvent != null)
                    {
                        int round = playerKilledEvent.Round;

                        string[] killPositionSplit =
                            SplitPositionString(kills.ElementAt(i).LastAlivePosition.ToString());

                        string killPositions = $"{killPositionSplit[0]},{killPositionSplit[1]},{killPositionSplit[2]}";

                        string[] deathPositionSplit =
                            SplitPositionString(deaths.ElementAt(i).LastAlivePosition.ToString());

                        string deathPositions =
                            $"{deathPositionSplit[0]},{deathPositionSplit[1]},{deathPositionSplit[2]}";

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
                            weaponUsed = weaponKillers.ElementAt(i).OriginalString.ToString();
                            weaponUsedClass = "Unknown";
                            weaponUsedType = "Unknown";
                        }

                        bool firstKillOfTheRound =
                            !killsStats.Any(k => k.Round == round && k.FirstKillOfTheRound == true);

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
                                XPositionKill = double.Parse(killPositionSplit[0]),
                                YPositionKill = double.Parse(killPositionSplit[1]),
                                ZPositionKill = double.Parse(killPositionSplit[2]),
                                VictimSteamID = victimSteamId,
                                VictimBotTakeover = playerKilledEvent.VictimBotTakeover,
                                XPositionDeath = double.Parse(deathPositionSplit[0]),
                                YPositionDeath = double.Parse(deathPositionSplit[1]),
                                ZPositionDeath = double.Parse(deathPositionSplit[2]),
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
            List<FeedbackMessage> feedbackMessages = new List<FeedbackMessage>();

            foreach (var message in processedData.MessagesValues)
            {
                var currentRoundTeams = processedData.TeamPlayersValues
                    .FirstOrDefault(t => t.Round == message.Round);

                if (currentRoundTeams != null && (message.SteamID == 0 || message.TeamName == null)
                ) // excludes warmup round
                {
                    // retrieve steam ID using player name if the event does not return it correctly
                    foreach (var player in currentRoundTeams.Terrorists)
                    {
                        player.SteamID = player.SteamID == 0
                            ? GetSteamIdByPlayerName(playerNames, player.Name)
                            : player.SteamID;
                    }

                    foreach (var player in currentRoundTeams.CounterTerrorists)
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
            List<teamStats> teamStats = new List<teamStats>();

            int swappedSidesCount = 0;
            int currentRoundChecking = 1;

            foreach (var teamPlayers in processedData.TeamPlayersValues)
            {
                // players in each team per round
                swappedSidesCount = switchSides.Count() > swappedSidesCount
                    ? switchSides.ElementAt(swappedSidesCount).RoundBeforeSwitch == currentRoundChecking - 1
                        ?
                        swappedSidesCount + 1
                        : swappedSidesCount
                    : swappedSidesCount;

                bool firstHalf = swappedSidesCount % 2 == 0 ? true : false;

                var currentRoundTeams =
                    processedData.TeamPlayersValues.FirstOrDefault(t => t.Round == teamPlayers.Round);

                var alphaPlayers = currentRoundTeams != null
                    ? firstHalf ? currentRoundTeams.Terrorists : currentRoundTeams.CounterTerrorists
                    : null;

                var bravoPlayers = currentRoundTeams != null
                    ? firstHalf ? currentRoundTeams.CounterTerrorists : currentRoundTeams.Terrorists
                    : null;

                List<long> alphaSteamIds = new List<long>();
                List<long> bravoSteamIds = new List<long>();

                foreach (var player in alphaPlayers)
                {
                    player.SteamID = player.SteamID == 0
                        ? GetSteamIdByPlayerName(playerNames, player.Name)
                        : player.SteamID;

                    alphaSteamIds.Add(player.SteamID);
                }

                foreach (var player in bravoPlayers)
                {
                    player.SteamID = player.SteamID == 0
                        ? GetSteamIdByPlayerName(playerNames, player.Name)
                        : player.SteamID;

                    bravoSteamIds.Add(player.SteamID);
                }

                // attempts to remove and stray players that are supposedly on a team, even though they exceed the max players per team and they are not in player lookups
                // (also most likely have a steam ID of 0)
                List<long> alphaSteamIdsToRemove = new List<long>();
                List<long> bravoSteamIdsToRemove = new List<long>();

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
                var deathsThisRound = processedData.PlayerKilledEventsValues.Where(k => k.Round == teamPlayers.Round);

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
                var shotsFiredThisRound = processedData.ShotsFiredValues.Where(s => s.Round == teamPlayers.Round);

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
            List<firstDamageStats> firstDamageStats = new List<firstDamageStats>();

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

            foreach (var roundsGroup in processedData.PlayerHurtValues.GroupBy(x => x.Round))
            {
                int lastRound = processedData.RoundEndReasonValues.Count();

                foreach (var round in roundsGroup.Where(x => x.Round > 0 && x.Round <= lastRound).Select(x => x.Round)
                    .Distinct())
                {
                    foreach (var steamIdsGroup in roundsGroup.Where(
                        x => x.Round == round && x.Player?.SteamID != 0 && x.Player?.SteamID != x.Attacker?.SteamID
                            && x.Weapon.Class != EquipmentClass.Grenade && x.Weapon.Class != EquipmentClass.Equipment
                            && x.Weapon.Class != EquipmentClass.Unknown && x.Weapon.Weapon != EquipmentElement.Unknown
                            && x.Weapon.Weapon != EquipmentElement.Bomb && x.Weapon.Weapon != EquipmentElement.World
                    ).OrderBy(x => x.TimeInRound).GroupBy(x => x.Attacker.SteamID))
                    {
                        var firstDamage = steamIdsGroup.FirstOrDefault();

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
            List<PlayerPositionByRound> playerPositionByRound = new List<PlayerPositionByRound>();

            // create playerPositionByRound with empty PlayerPositionByTimeInRound
            foreach (var roundsGroup in processedData.PlayerPositionsValues.GroupBy(x => x.Round))
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
            foreach (var playerPositionsStat in playerPositionByRound)
            {
                foreach (var timeInRoundsGroup in processedData.PlayerPositionsValues
                    .Where(x => x.Round == playerPositionsStat.Round).GroupBy(x => x.TimeInRound))
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
            foreach (var playerPositionsStat in playerPositionByRound)
            {
                foreach (var playerPositionByTimeInRound in playerPositionsStat.PlayerPositionByTimeInRound)
                {
                    foreach (var steamIdsGroup in processedData.PlayerPositionsValues
                        .Where(
                            x => x.Round == playerPositionsStat.Round
                                && x.TimeInRound == playerPositionByTimeInRound.TimeInRound
                        ).GroupBy(x => x.SteamID).Distinct())
                    {
                        foreach (var playerPositionsInstance in steamIdsGroup)
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

        public static string GetOutputJsonFilepath(
            ProcessedData processedData,
            AllStats allStats,
            PlayerPositionsStats playerPositionsStats,
            string mapNameString,
            string mapDateString)
        {
            string filename = processedData.SameFilename ? allStats.mapInfo.DemoName : Guid.NewGuid().ToString();

            string path = string.Empty;

            if (processedData.FoldersToProcess.Count > 0 && processedData.SameFolderStructure)
                foreach (var folder in processedData.FoldersToProcess)
                {
                    string[] splitPath = Path.GetDirectoryName(processedData.DemoInformation.DemoName).Split(
                        new string[] { string.Concat(folder, "\\") },
                        StringSplitOptions.None
                    );

                    path = splitPath.Length > 1
                        ? string.Concat(processedData.OutputRootFolder, "\\", splitPath.LastOrDefault(), "\\")
                        : string.Concat(processedData.OutputRootFolder, "\\");

                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        if (!Directory.Exists(path))
                            Directory.CreateDirectory(path);

                        break;
                    }
                }
            else
                path = string.Concat(processedData.OutputRootFolder, "\\");

            if (mapDateString != string.Empty)
                path += mapDateString + "_";

            path += mapNameString + "_" + filename;

            if (playerPositionsStats != null)
                path += "_playerpositions";

            path += ".json";

            return path;
        }

        public static void CreateJsonAllStats(
            ProcessedData processedData,
            AllStats allStats,
            string mapNameString,
            string mapDateString)
        {
            var outputFilepath = string.Empty;

            try
            {
                outputFilepath = GetOutputJsonFilepath(processedData, allStats, null, mapNameString, mapDateString);

                StreamWriter sw = new StreamWriter(outputFilepath, false);

                string json = JsonConvert.SerializeObject(allStats, Formatting.Indented);

                sw.WriteLine(json);
                /* JSON creation end*/

                sw.Close();
            }
            catch (Exception)
            {
                Console.WriteLine("Could not create json file.");
                Console.WriteLine(string.Concat("Filename: ", outputFilepath));
                Console.WriteLine(string.Concat("Demoname: ", allStats.mapInfo.DemoName));
            }
        }

        public static void CreateJsonPlayerPositionsStats(
            ProcessedData processedData,
            AllStats allStats,
            PlayerPositionsStats playerPositionsStats,
            string mapNameString,
            string mapDateString)
        {
            var outputFilepath = string.Empty;

            try
            {
                outputFilepath = GetOutputJsonFilepath(
                    processedData,
                    allStats,
                    playerPositionsStats,
                    mapNameString,
                    mapDateString
                );

                StreamWriter sw = new StreamWriter(outputFilepath, false);

                string json = JsonConvert.SerializeObject(playerPositionsStats, Formatting.Indented);

                sw.WriteLine(json);
                /* JSON creation end*/

                sw.Close();
            }
            catch (Exception)
            {
                Console.WriteLine("Could not create json file.");
                Console.WriteLine(string.Concat("Filename: ", outputFilepath));
                Console.WriteLine(string.Concat("Demoname: ", allStats.mapInfo.DemoName));
            }
        }

        public static long GetSteamIdByPlayerName(Dictionary<long, Dictionary<string, string>> playerNames, string name)
        {
            if (name == "unconnected") return 0;

            var steamId = playerNames.Where(p => p.Value.Values.ElementAt(0) == name).Select(p => p.Key)
                .FirstOrDefault(); // steamID will be 0 if not found

            return steamId;
        }

        public IEnumerable<object> SelectWeaponsEventsByName(string name)
        {
            var shots = from shot in GetEvents<WeaponFiredEventArgs>()
                where (shot as WeaponFiredEventArgs).Weapon.Weapon.ToString() == name
                select shot;

            return shots;
        }

        public List<object> GetEvents<T>()
        {
            Type t = typeof(T);

            return events.ContainsKey(t) ? events[t] : new List<object>();
        }

        public static List<Team> GetRoundsWonTeams(IEnumerable<Team> teamValues)
        {
            var roundsWonTeams = teamValues.ToList();
            roundsWonTeams.RemoveAll(
                r => !r.ToString().Equals("Terrorist") && !r.ToString().Equals("CounterTerrorist")
                    && !r.ToString().Equals("Unknown")
            );

            return roundsWonTeams;
        }

        public static List<RoundEndReason> GetRoundsWonReasons(IEnumerable<RoundEndReason> roundEndReasonValues)
        {
            var roundsWonReasons = roundEndReasonValues.ToList();
            roundsWonReasons.RemoveAll(
                r => !r.ToString().Equals(winReasonTKills) && !r.ToString().Equals(winReasonCtKills)
                    && !r.ToString().Equals(winReasonBombed) && !r.ToString().Equals(winReasonDefused)
                    && !r.ToString().Equals(winReasonRescued) && !r.ToString().Equals(winReasonNotRescued)
                    && !r.ToString().Equals(winReasonTSaved) && !r.ToString().Equals(winReasonDangerZone)
                    && !r.ToString().Equals("Unknown")
            );

            return roundsWonReasons;
        }

        public static int GetCurrentRoundNum(MatchData md, string gamemode)
        {
            int roundsCount = md.GetEvents<RoundOfficiallyEndedEventArgs>().Count;
            List<TeamPlayers> teamPlayersList = md.GetEvents<TeamPlayers>().Cast<TeamPlayers>().ToList();

            int round = 0;

            if (teamPlayersList.Count > 0 && teamPlayersList.Any(t => t.Round == 1))
            {
                var teamPlayers = teamPlayersList.First(t => t.Round == 1);

                if (teamPlayers.Terrorists.Count > 0 && teamPlayers.CounterTerrorists.Count > 0)
                    round = roundsCount + 1;
            }

            // add 1 for roundsCount when in danger zone
            if (gamemode == Gamemodes.DangerZone)
                round++;

            return round;
        }

        public static bool CheckIfPlayerAliveAtThisPointInRound(MatchData md, Player player, int round)
        {
            long steamId = player == null ? 0 : player.SteamID;

            var kills = md.events.Where(k => k.Key.Name.ToString() == "PlayerKilledEventArgs")
                .Select(v => (PlayerKilledEventArgs)v.Value.ElementAt(0));

            return !kills.Any(x => x.Round == round && x.Victim?.SteamID != 0 && x.Victim.SteamID == player?.SteamID);
        }

        public int CheckForUpdatedUserId(int userId)
        {
            int newUserId = playerReplacements.Where(u => u.Key == userId).Select(u => u.Value).FirstOrDefault();

            return newUserId == 0 ? userId : newUserId;
        }

        public static string[] SplitPositionString(string position)
        {
            var positionString = position.Split(
                new string[] { "{X: ", ", Y: ", ", Z: ", " }" },
                StringSplitOptions.None
            );

            return positionString.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        }

        public static string GenerateSetPosCommand(
            string[] currentPositions,
            float? viewDirectionX,
            float? viewDirectionY)
        {
            return string.Concat(
                "setpos ",
                currentPositions[0],
                " ",
                currentPositions[1],
                " ",
                currentPositions[2],
                "; setang ",
                Convert.ToString(viewDirectionX) ?? "0.0",
                " ",
                Convert.ToString(viewDirectionY) ?? "0.0" // Z axis is optional
            );
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

        public static int? GetMinRoundsForWin(string gamemode, string testType)
        {
            switch (gamemode.ToLower(), testType.ToLower())
            {
                case (Gamemodes.WingmanDefuse, "casual"):
                case (Gamemodes.WingmanDefuse, "competitive"):
                case (Gamemodes.WingmanHostage, "casual"):
                case (Gamemodes.WingmanHostage, "competitive"):
                    return 9;
                case (Gamemodes.Defuse, "casual"):
                case (Gamemodes.Hostage, "casual"):
                case (Gamemodes.Unknown, "casual")
                    : // assumes that it is a classic match. Would be better giving the -gamemodeoverride parameter to get around this as it cannot figure out the game mode
                    return 11;
                case (Gamemodes.Defuse, "competitive"):
                case (Gamemodes.Hostage, "competitive"):
                case (Gamemodes.Unknown, "competitive")
                    : // assumes that it is a classic match. Would be better giving the -gamemodeoverride parameter to get around this as it cannot figure out the game mode
                    return 16;
                case (Gamemodes.DangerZone, "casual"):
                case (Gamemodes.DangerZone, "competitive"):
                    return 2;
                default:
                    return null;
            }
        }

        private static bool CheckIfStatsShouldBeCreated(string typeName, string gamemode)
        {
            switch (typeName.ToLower())
            {
                case "tanookiStats":
                case "winnersstats":
                case "bombsitestats":
                case "hostagestats":
                case "teamstats":
                    return gamemode != Gamemodes.DangerZone;
                case "playerstats":
                case "roundsstats":
                case "rescuezonestats":
                case "grenadestotalstats":
                case "grenadesspecificstats":
                case "killsstats":
                case "feedbackmessage":
                case "chickenstats":
                case "firstdamagestats":
                case "playerpositionsstats":
                default:
                    return true;
            }
        }
    }
}
