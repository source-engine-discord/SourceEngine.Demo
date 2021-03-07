using System;
using System.Collections.Generic;
using System.Linq;

using SourceEngine.Demo.Parser;
using SourceEngine.Demo.Parser.Entities;
using SourceEngine.Demo.Stats.Models;

namespace SourceEngine.Demo.Stats
{
    public partial class MatchData
    {
        private void TickDoneEventHandler(object sender, TickDoneEventArgs e)
        {
            foreach (Player p in dp.PlayingParticipants)
            {
                addTick(p, PSTATUS.PLAYING);

                if (p.IsAlive)
                    addTick(p, PSTATUS.ALIVE);
            }

            foreach (Player p in dp.Participants)
            {
                if (!p.Disconnected)
                    addTick(p, PSTATUS.ONSERVER);
            }
        }

        #region Server Events

        private void MatchStartedEventHandler(object sender, MatchStartedEventArgs e)
        {
            var currentfeedbackMessages = new List<FeedbackMessage>();

            //stores all fb messages so that they aren't lost when stats are reset
            if (events.Count > 0 && events.Any(k => k.Key.Name.ToString() == "FeedbackMessage"))
                foreach (FeedbackMessage message in events.Where(k => k.Key.Name.ToString() == "FeedbackMessage")
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

                                // overwrites whatever the TimeInRound value was before, 0 is generally used for
                                // messages sent in Warmup
                                TimeInRound = 0,
                            }
                        );
                }

            events = new Dictionary<Type, List<object>>(); //resets all stats stored

            addEvent(typeof(MatchStartedEventArgs), e);

            //adds all stored fb messages back
            foreach (FeedbackMessage feedbackMessage in currentfeedbackMessages)
                addEvent(typeof(FeedbackMessage), feedbackMessage);
        }

        private void ChickenKilledEventHandler(object sender, ChickenKilledEventArgs e)
        {
            addEvent(typeof(ChickenKilledEventArgs), e);
        }

        private void SayText2EventHandler(object sender, SayText2EventArgs e)
        {
            addEvent(typeof(SayText2EventArgs), e);

            var text = e.Text.ToString();

            if (IsMessageFeedback(text))
            {
                int round = GetCurrentRoundNum(this, demoInfo.GameMode);

                long steamId = e.Sender?.SteamID ?? 0;

                Player player = null;

                if (steamId != 0)
                    player = dp.Participants.FirstOrDefault(p => p.SteamID == steamId);

                var teamName = player?.Team.ToString();
                teamName = teamName == "Spectate" ? "Spectator" : teamName;

                bool playerAlive = CheckIfPlayerAliveAtThisPointInRound(this, player, round);

                List<object> roundsOfficiallyEndedEvents =
                    events.Any(k => k.Key.Name.ToString() == "RoundOfficiallyEndedEventArgs")
                        ? events.Where(k => k.Key.Name.ToString() == "RoundOfficiallyEndedEventArgs")
                            .Select(v => v.Value).ElementAt(0)
                        : null;

                List<object> freezetimesEndedEvents =
                    events.Any(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs")
                        ? events.Where(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs").Select(v => v.Value)
                            .ElementAt(0)
                        : null;

                int numOfRoundsOfficiallyEnded = roundsOfficiallyEndedEvents?.Count > 0
                    ? roundsOfficiallyEndedEvents.Count
                    : 0;

                int numOfFreezetimesEnded = freezetimesEndedEvents?.Count > 0 ? freezetimesEndedEvents.Count : 0;
                float timeInRound = 0; // Stays as '0' if sent during freezetime

                if (numOfFreezetimesEnded > numOfRoundsOfficiallyEnded)
                {
                    // would it be better to use '.OrderByDescending(f => f.TimeEnd).FirstOrDefault()' ?
                    var freezetimeEnded = (FreezetimeEndedEventArgs)freezetimesEndedEvents.LastOrDefault();
                    timeInRound = dp.CurrentTime - freezetimeEnded.TimeEnd;
                }

                var feedbackMessage = new FeedbackMessage
                {
                    Round = round,
                    SteamID = steamId,
                    TeamName = teamName, // works out TeamName in GetFeedbackMessages() if it is null
                    XCurrentPosition = player?.Position.X,
                    YCurrentPosition = player?.Position.Y,
                    ZCurrentPosition = player?.Position.Z,
                    XLastAlivePosition = playerAlive ? player?.LastAlivePosition.X : null,
                    YLastAlivePosition = playerAlive ? player?.LastAlivePosition.Y : null,
                    ZLastAlivePosition = playerAlive ? player?.LastAlivePosition.Z : null,
                    XCurrentViewAngle = player?.ViewDirectionX,
                    YCurrentViewAngle = player?.ViewDirectionY,
                    SetPosCommandCurrentPosition = GenerateSetPosCommand(player),
                    Message = text,

                    // counts messages sent after the round_end event fires as the next round, set to '0' as if it
                    // was the next round's warmup (done this way instead of using round starts to avoid potential
                    // issues when restarting rounds)
                    TimeInRound = timeInRound,
                };

                addEvent(typeof(FeedbackMessage), feedbackMessage);
            }
        }

        private void RoundEndEventHandler(object sender, RoundEndedEventArgs e)
        {
            IEnumerable<List<object>> roundsEndedEvents =
                events.Where(k => k.Key.Name.ToString() == "RoundEndedEventArgs").Select(v => v.Value);

            IEnumerable<List<object>> roundsOfficiallyEndedEvents = events
                .Where(k => k.Key.Name.ToString() == "RoundOfficiallyEndedEventArgs").Select(v => v.Value);

            IEnumerable<List<object>> freezetimesEndedEvents = events
                .Where(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs").Select(v => v.Value);

            int numOfRoundsEnded = roundsEndedEvents.Any() ? roundsEndedEvents.ElementAt(0).Count : 0;
            int numOfRoundsOfficiallyEnded = roundsOfficiallyEndedEvents.Any()
                ? roundsOfficiallyEndedEvents.ElementAt(0).Count
                : 0;

            int numOfFreezetimesEnded = freezetimesEndedEvents.Any() ? freezetimesEndedEvents.ElementAt(0).Count : 0;

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
                if (events.Any(k => k.Key.Name.ToString() == "FeedbackMessage"))
                    foreach (FeedbackMessage message in events.Where(k => k.Key.Name.ToString() == "FeedbackMessage")
                        .Select(v => v.Value).ElementAt(0))
                    {
                        if (message.Round == numOfFreezetimesEnded)
                            message.TimeInRound = -1;
                    }
            }

            addEvent(typeof(RoundEndedEventArgs), e);
        }

        private void RoundOfficiallyEndedEventHandler(object sender, RoundOfficiallyEndedEventArgs e)
        {
            IEnumerable<List<object>> roundsEndedEvents =
                events.Where(k => k.Key.Name.ToString() == "RoundEndedEventArgs").Select(v => v.Value);

            IEnumerable<List<object>> roundsOfficiallyEndedEvents = events
                .Where(k => k.Key.Name.ToString() == "RoundOfficiallyEndedEventArgs").Select(v => v.Value);

            IEnumerable<List<object>> freezetimesEndedEvents = events
                .Where(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs").Select(v => v.Value);

            int numOfRoundsEnded = roundsEndedEvents.Any() ? roundsEndedEvents.ElementAt(0).Count : 0;
            int numOfRoundsOfficiallyEnded = roundsOfficiallyEndedEvents.Any()
                ? roundsOfficiallyEndedEvents.ElementAt(0).Count
                : 0;

            int numOfFreezetimesEnded = freezetimesEndedEvents.Any() ? freezetimesEndedEvents.ElementAt(0).Count : 0;

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
                if (events.Any(k => k.Key.Name.ToString() == "FeedbackMessage"))
                    foreach (FeedbackMessage message in events.Where(k => k.Key.Name.ToString() == "FeedbackMessage")
                        .Select(v => v.Value).ElementAt(0))
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

            addEvent(typeof(RoundOfficiallyEndedEventArgs), e);
        }

        private void SwitchSidesEventHandler(object sender, SwitchSidesEventArgs e)
        {
            int roundsCount = GetEvents<RoundOfficiallyEndedEventArgs>().Count;

            var switchSidesEventArgs = new SwitchSidesEventArgs
            {
                RoundBeforeSwitch = roundsCount + 1,
            }; // announce_phase_end event occurs before round_officially_ended event

            addEvent(typeof(SwitchSidesEventArgs), switchSidesEventArgs);
        }

        private void FreezetimeEndedEventHandler(object sender, FreezetimeEndedEventArgs e)
        {
            IEnumerable<List<object>> freezetimesEndedEvents = events
                .Where(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs").Select(v => v.Value);

            IEnumerable<List<object>> roundsEndedEvents =
                events.Where(k => k.Key.Name.ToString() == "RoundEndedEventArgs").Select(v => v.Value);

            IEnumerable<List<object>> roundsOfficiallyEndedEvents = events
                .Where(k => k.Key.Name.ToString() == "RoundOfficiallyEndedEventArgs").Select(v => v.Value);

            int numOfFreezetimesEnded = freezetimesEndedEvents.Any() ? freezetimesEndedEvents.ElementAt(0).Count : 0;

            int numOfRoundsEnded = roundsEndedEvents.Any() ? roundsEndedEvents.ElementAt(0).Count : 0;
            int numOfRoundsOfficiallyEnded = roundsOfficiallyEndedEvents.Any()
                ? roundsOfficiallyEndedEvents.ElementAt(0).Count
                : 0;

            //Console.WriteLine("dp.FreezetimeEnded -- Ended: " + numOfRoundsEnded + " - " + numOfRoundsOfficiallyEnded + " - " + numOfFreezetimesEnded);

            /*	The final round in a match does not throw a round_officially_ended event, but a round_freeze_end event is thrown after the game ends,
                so assume that a game has ended if a second round_freeze_end event is found in the same round as a round_end_event and NO round_officially_ended event.
                This does mean that if a round_officially_ended event is not triggered due to demo error, the parse will mess up. */
            var minRoundsForWin = GetMinRoundsForWin(demoInfo.GameMode, demoInfo.TestType);

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

                dp.stopParsingDemo = true; // forcefully stops the demo from being parsed any further to avoid events

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

            addEvent(typeof(FreezetimeEndedEventArgs), e);

            //work out teams at current round
            int roundsCount = GetEvents<RoundOfficiallyEndedEventArgs>().Count;
            IEnumerable<Player> players = dp.PlayingParticipants;

            var teams = new TeamPlayers
            {
                Terrorists = players.Where(p => p.Team is Team.Terrorist).ToList(),
                CounterTerrorists = players.Where(p => p.Team is Team.CounterTerrorist).ToList(),
                Round = roundsCount + 1,
            };

            addEvent(typeof(TeamPlayers), teams);

            int tEquipValue = 0, ctEquipValue = 0;
            int tExpenditure = 0, ctExpenditure = 0;

            foreach (Player player in teams.Terrorists)
            {
                tEquipValue += player.CurrentEquipmentValue; // player.FreezetimeEndEquipmentValue = 0 ???

                // (player.FreezetimeEndEquipmentValue = 0 - player.RoundStartEquipmentValue) ???
                tExpenditure += player.CurrentEquipmentValue - player.RoundStartEquipmentValue;
            }

            foreach (Player player in teams.CounterTerrorists)
            {
                ctEquipValue += player.CurrentEquipmentValue; // player.FreezetimeEndEquipmentValue = 0 ???

                // (player.FreezetimeEndEquipmentValue = 0 - player.RoundStartEquipmentValue) ???
                ctExpenditure += player.CurrentEquipmentValue - player.RoundStartEquipmentValue;
            }

            var teamEquipmentStats = new TeamEquipment
            {
                Round = roundsCount + 1,
                TEquipValue = tEquipValue,
                CTEquipValue = ctEquipValue,
                TExpenditure = tExpenditure,
                CTExpenditure = ctExpenditure,
            };

            addEvent(typeof(TeamEquipment), teamEquipmentStats);
        }

        #endregion

        #region Player Events

        private void PlayerKilledEventHandler(object sender, PlayerKilledEventArgs e)
        {
            e.Round = GetCurrentRoundNum(this, demoInfo.GameMode);

            addEvent(typeof(PlayerKilledEventArgs), e);
        }

        private void PlayerHurtEventHandler(object sender, PlayerHurtEventArgs e)
        {
            var round = GetCurrentRoundNum(this, demoInfo.GameMode);

            if (e.PossiblyKilledByBombExplosion) // a player_death event is not triggered due to death by bomb explosion
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

                addEvent(typeof(PlayerKilledEventArgs), playerKilledEventArgs);
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
                Attacker = attacker,
                XPositionAttacker = attacker.Position?.X ?? 0,
                YPositionAttacker = attacker.Position?.Y ?? 0,
                ZPositionAttacker = attacker.Position?.Z ?? 0,
                Health = e.Health,
                Armor = e.Armor,
                Weapon = e.Weapon,
                HealthDamage = e.HealthDamage,
                ArmorDamage = e.ArmorDamage,
                HitGroup = e.HitGroup,
                PossiblyKilledByBombExplosion = e.PossiblyKilledByBombExplosion,
            };

            addEvent(typeof(PlayerHurt), playerHurt);
        }

        private void RoundMVPEventHandler(object sender, RoundMVPEventArgs e)
        {
            addEvent(typeof(RoundMVPEventArgs), e);
        }

        private void PlayerDisconnectEventHandler(object sender, PlayerDisconnectEventArgs e)
        {
            if (e.Player != null && e.Player.Name != "unconnected" && e.Player.Name != "GOTV")
            {
                int round = GetCurrentRoundNum(this, demoInfo.GameMode);

                var disconnectedPlayer = new DisconnectedPlayer
                {
                    PlayerDisconnectEventArgs = e,
                    Round = round - 1,
                };

                addEvent(typeof(DisconnectedPlayer), disconnectedPlayer);
            }
        }

        private void PlayerBindEventHandler(object sender, PlayerBindEventArgs e)
        {
            BindPlayer(e.Player);
        }

        private void PlayerPositionsEventHandler(object sender, PlayerPositionsEventArgs e)
        {
            foreach (PlayerPositionEventArgs playerPosition in e.PlayerPositions)
            {
                if (events.Count > 0 && events.Any(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs"))
                {
                    int round = GetCurrentRoundNum(this, demoInfo.GameMode);

                    if (round > 0 && playerPosition.Player.SteamID > 0)
                    {
                        bool playerAlive = CheckIfPlayerAliveAtThisPointInRound(this, playerPosition.Player, round);
                        List<object> freezetimeEndedEvents = events
                            .Where(k => k.Key.Name.ToString() == "FreezetimeEndedEventArgs").Select(v => v.Value)
                            .ElementAt(0);

                        var freezetimeEndedEventLast = (FreezetimeEndedEventArgs)freezetimeEndedEvents.LastOrDefault();

                        var freezetimeEndedThisRound = freezetimeEndedEvents.Count >= round;

                        if (playerAlive && freezetimeEndedThisRound)
                        {
                            var playerPositionsInstance = new PlayerPositionsInstance
                            {
                                Round = round,
                                TimeInRound = (int)e.CurrentTime - (int)freezetimeEndedEventLast.TimeEnd,
                                SteamID = playerPosition.Player.SteamID,
                                TeamSide = playerPosition.Player.Team is Team.Terrorist ? "T" : "CT",
                                XPosition = playerPosition.Player.Position.X,
                                YPosition = playerPosition.Player.Position.Y,
                                ZPosition = playerPosition.Player.Position.Z,
                            };

                            addEvent(typeof(PlayerPositionsInstance), playerPositionsInstance);
                        }
                    }
                }
            }
        }

        #endregion

        #region Bomb Events

        private void BombPlantedEventHandler(object sender, BombEventArgs e)
        {
            int roundsCount = GetEvents<RoundOfficiallyEndedEventArgs>().Count;

            var bombPlanted = new BombPlanted
            {
                Round = roundsCount + 1,
                TimeInRound = e.TimeInRound,
                Player = e.Player,
                Bombsite = e.Site,
            };

            addEvent(typeof(BombPlanted), bombPlanted);
        }

        private void BombExplodedEventHandler(object sender, BombEventArgs e)
        {
            int roundsCount = GetEvents<RoundOfficiallyEndedEventArgs>().Count;

            var bombExploded = new BombExploded
            {
                Round = roundsCount + 1,
                TimeInRound = e.TimeInRound,
                Player = e.Player,
                Bombsite = e.Site,
            };

            addEvent(typeof(BombExploded), bombExploded);
        }

        private void BombDefusedEventHandler(object sender, BombEventArgs e)
        {
            int roundsCount = GetEvents<RoundOfficiallyEndedEventArgs>().Count;

            var bombDefused = new BombDefused
            {
                Round = roundsCount + 1,
                TimeInRound = e.TimeInRound,
                Player = e.Player,
                Bombsite = e.Site,
                HasKit = e.Player.HasDefuseKit,
            };

            addEvent(typeof(BombDefused), bombDefused);
        }

        #endregion

        #region Hostage Events

        private void HostageRescuedEventHandler(object sender, HostageRescuedEventArgs e)
        {
            int roundsCount = GetEvents<RoundOfficiallyEndedEventArgs>().Count;

            var hostageRescued = new HostageRescued
            {
                Round = roundsCount + 1,
                TimeInRound = e.TimeInRound,
                Player = e.Player,
                Hostage = e.Hostage,
                HostageIndex = e.HostageIndex,
                RescueZone = e.RescueZone,
            };

            addEvent(typeof(HostageRescued), hostageRescued);
        }

        private void HostagePickedUpEventHandler(object sender, HostagePickedUpEventArgs e)
        {
            int roundsCount = GetEvents<RoundOfficiallyEndedEventArgs>().Count;

            var hostagePickedUp = new HostagePickedUp
            {
                Round = roundsCount + 1,
                TimeInRound = e.TimeInRound,
                Player = e.Player,
                Hostage = e.Hostage,
                HostageIndex = e.HostageIndex,
            };

            addEvent(typeof(HostagePickedUp), hostagePickedUp);
        }

        #endregion

        #region Weapon Events

        private void WeaponFiredEventHandler(object sender, WeaponFiredEventArgs e)
        {
            addEvent(typeof(WeaponFiredEventArgs), e);

            var round = GetCurrentRoundNum(this, demoInfo.GameMode);

            var shotFired = new ShotFired
            {
                Round = round,
                TimeInRound = e.TimeInRound,
                Shooter = e.Shooter,
                TeamSide = e.Shooter.Team.ToString(),
                Weapon = new Equipment(e.Weapon),
            };

            addEvent(typeof(ShotFired), shotFired);
        }

        #endregion

        #region Grenade Events

        private void ExplosiveNadeExplodedEventHandler(object sender, GrenadeEventArgs e)
        {
            addEvent(typeof(GrenadeEventArgs), e);
            addEvent(typeof(NadeEventArgs), e);
        }

        private void FireNadeStartedEventHandler(object sender, FireEventArgs e)
        {
            addEvent(typeof(FireEventArgs), e);
            addEvent(typeof(NadeEventArgs), e);
        }

        private void SmokeNadeStartedEventHandler(object sender, SmokeEventArgs e)
        {
            addEvent(typeof(SmokeEventArgs), e);
            addEvent(typeof(NadeEventArgs), e);
        }

        private void FlashNadeExplodedEventHandler(object sender, FlashEventArgs e)
        {
            addEvent(typeof(FlashEventArgs), e);
            addEvent(typeof(NadeEventArgs), e);
        }

        private void DecoyNadeStartedEventHandler(object sender, DecoyEventArgs e)
        {
            addEvent(typeof(DecoyEventArgs), e);
            addEvent(typeof(NadeEventArgs), e);
        }

        #endregion
    }
}
