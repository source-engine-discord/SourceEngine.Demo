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
        private readonly List<RoundEndedEventArgs> roundEndedEvents = new();
        private int freezetimeEndedCount;
        private float lastFreezetimeEnd = -1;

        public int RoundOfficiallyEndedCount { get; private set; }

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
            // Reset all stats stored except for the feedback messages.
            processedData = new ProcessedData { MessagesValues = processedData.MessagesValues };
            roundEndedEvents.Clear();
            RoundOfficiallyEndedCount = 0;
            freezetimeEndedCount = 0;
            lastFreezetimeEnd = -1;

            // Modify some properties of the saved feedback messages.
            foreach (FeedbackMessage feedbackMessage in processedData.MessagesValues)
            {
                feedbackMessage.Round = 0;

                // Overwrite whatever the TimeInRound value was before.
                // 0 is generally used for messages sent in Warmup.
                feedbackMessage.TimeInRound = 0;
            }

            processedData.MatchStartValues.Add(e);
        }

        private void ChickenKilledEventHandler(object sender, ChickenKilledEventArgs e)
        {
            processedData.ChickenValues.Add(e);
        }

        private void SayText2EventHandler(object sender, SayText2EventArgs e)
        {
            if (!IsMessageFeedback(e.Text))
                return;

            int round = GetCurrentRoundNum(this, demoInfo.GameMode);

            long steamId = e.Sender?.SteamID ?? 0;
            Player player = null;
            if (steamId != 0)
                player = dp.Participants.FirstOrDefault(p => p.SteamID == steamId);

            var teamName = player?.Team.ToString();
            teamName = teamName == "Spectate" ? "Spectator" : teamName;

            bool playerAlive = CheckIfPlayerAliveAtThisPointInRound(this, player, round);

            float timeInRound = 0; // Stays as '0' if sent during freezetime
            if (freezetimeEndedCount > RoundOfficiallyEndedCount)
                timeInRound = dp.CurrentTime - lastFreezetimeEnd;

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
                Message = e.Text,

                // counts messages sent after the round_end event fires as the next round, set to '0' as if it
                // was the next round's warmup (done this way instead of using round starts to avoid potential
                // issues when restarting rounds)
                TimeInRound = timeInRound,
            };

            processedData.MessagesValues.Add(feedbackMessage);
        }

        private void RoundEndEventHandler(object sender, RoundEndedEventArgs e)
        {
            // Raise a round_officially_ended event if one did not get fired in the previous round due to an error.
            // Since raising an event is synchronous in this case,
            // the handler will have incremented roundOfficiallyEndedCount by the next iteration.
            while (roundEndedEvents.Count > RoundOfficiallyEndedCount)
            {
                // The round_officially_ended event doesn't provide any data.
                // Therefore, the data for the round must be retrieved from the corresponding round_end event.
                RoundEndedEventArgs roundEndedEvent = roundEndedEvents.ElementAtOrDefault(RoundOfficiallyEndedCount);

                dp.RaiseRoundOfficiallyEnded(
                    new RoundOfficiallyEndedEventArgs
                    {
                        Message = roundEndedEvent?.Message ?? "Unknown",
                        Reason = roundEndedEvent?.Reason ?? RoundEndReason.Unknown,
                        Winner = roundEndedEvent?.Winner ?? Team.Unknown,
                        Length = (roundEndedEvent?.Length ?? -4) + 4, // Guess the round length. Use -4 to make it 0.
                    }
                );
            }

            // Raise a round_freeze_end event if one did not get fired in the previous round due to an error.
            // Since raising an event is synchronous in this case,
            // the handler will have incremented freezetimeEndedCount by the next iteration.
            while (roundEndedEvents.Count >= freezetimeEndedCount)
            {
                dp.RaiseFreezetimeEnded(
                    new FreezetimeEndedEventArgs
                    {
                        TimeEnd = -1, // no idea when this actually ended without guessing
                    }
                );

                // Set TimeInRound to '-1' for all feedback messages sent this round, as it will be wrong.
                foreach (FeedbackMessage message in processedData.MessagesValues)
                {
                    if (message.Round == freezetimeEndedCount)
                        message.TimeInRound = -1;
                }
            }

            roundEndedEvents.Add(e);
        }

        private void RoundOfficiallyEndedEventHandler(object sender, RoundOfficiallyEndedEventArgs e)
        {
            // Raise a round_end event if one did not get fired in the previous round due to an error.
            // Since raising an event is synchronous in this case,
            // the handler will have incremented roundEndedEvents.Count by the next iteration.
            while (RoundOfficiallyEndedCount >= roundEndedEvents.Count)
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
            }

            // Raise a round_freeze_end event if one did not get fired in the previous round due to an error.
            // Since raising an event is synchronous in this case,
            // the handler will have incremented freezetimeEndedCount by the next iteration.
            while (RoundOfficiallyEndedCount >= freezetimeEndedCount)
            {
                // No idea when this actually ended without guessing.
                dp.RaiseFreezetimeEnded(new FreezetimeEndedEventArgs { TimeEnd = -1 });

                // Set TimeInRound to '-1' for all feedback messages sent this round, as it will be wrong.
                foreach (FeedbackMessage message in processedData.MessagesValues)
                {
                    if (message.Round == freezetimeEndedCount)
                        message.TimeInRound = -1;
                }
            }

            // The round_officially_ended event doesn't provide data.
            // Therefore, the data for the round must be retrieved from the corresponding round_end event.
            // The exception is the length, which is determined by the GameEventHandler before raising the event.
            RoundEndedEventArgs roundEndedEvent = roundEndedEvents.LastOrDefault();

            processedData.RoundLengthValues.Add(e.Length);
            processedData.RoundEndReasonValues.Add(roundEndedEvent?.Reason ?? RoundEndReason.Unknown);
            processedData.TeamValues.Add(roundEndedEvent?.Winner ?? Team.Unknown);
            RoundOfficiallyEndedCount++;
        }

        private void SwitchSidesEventHandler(object sender, SwitchSidesEventArgs e)
        {
            // announce_phase_end event occurs before round_officially_ended event
            processedData.SwitchSidesValues.Add(
                new SwitchSidesEventArgs
                {
                    RoundBeforeSwitch = RoundOfficiallyEndedCount + 1,
                }
            );
        }

        private void FreezetimeEndedEventHandler(object sender, FreezetimeEndedEventArgs e)
        {
            // The final round in a match does not throw a round_officially_ended event, but a round_freeze_end event is
            // thrown after the game ends. Therefore, assume that a game has ended if a second round_freeze_end event is
            // found in the same round as a round_end_event and NO round_officially_ended event. This does mean that if
            // a round_officially_ended event is not triggered due to demo error, the parse will mess up.
            var minRoundsForWin = GetMinRoundsForWin(demoInfo.GameMode, demoInfo.TestType);

            if (freezetimeEndedCount == RoundOfficiallyEndedCount + 1 && freezetimeEndedCount == roundEndedEvents.Count
                && roundEndedEvents.Count >= minRoundsForWin)
            {
                Console.WriteLine("Assuming the parse has finished.");
                RoundEndedEventArgs roundEndedEvent = roundEndedEvents.ElementAtOrDefault(RoundOfficiallyEndedCount);

                dp.RaiseRoundOfficiallyEnded(
                    new RoundOfficiallyEndedEventArgs
                    {
                        Reason = roundEndedEvent?.Reason ?? RoundEndReason.Unknown,
                        Message = roundEndedEvent?.Message ?? "Unknown",
                        Winner = roundEndedEvent?.Winner ?? Team.Unknown,
                        Length = (roundEndedEvent?.Length ?? -4) + 4, // Guess the round length. Use -4 to make it 0.
                    }
                );

                // Forcefully stops the demo from being parsed any further to avoid events (such as player deaths to
                // world) happening in a next round (a round that never actually occurs).
                dp.stopParsingDemo = true;
                return;
            }

            // Raise a round_end event if one did not get fired in the previous round due to an error.
            // Since raising an event is synchronous in this case,
            // the handler will have incremented roundEndedEvents.Count by the next iteration.
            while (freezetimeEndedCount > roundEndedEvents.Count)
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
            }

            // Raise a round_officially_ended event if one did not get fired in the previous round due to an error.
            // Since raising an event is synchronous in this case,
            // the handler will have incremented roundOfficiallyEndedCount by the next iteration.
            while (freezetimeEndedCount > RoundOfficiallyEndedCount)
            {
                RoundEndedEventArgs roundEndedEvent = roundEndedEvents.ElementAtOrDefault(RoundOfficiallyEndedCount);

                dp.RaiseRoundOfficiallyEnded(
                    new RoundOfficiallyEndedEventArgs
                    {
                        Reason = roundEndedEvent?.Reason ?? RoundEndReason.Unknown,
                        Message = roundEndedEvent?.Message ?? "Unknown",
                        Winner = roundEndedEvent?.Winner ?? Team.Unknown,
                        Length = (roundEndedEvent?.Length ?? -4) + 4, // Guess the round length. Use -4 to make it 0.
                    }
                );
            }

            freezetimeEndedCount++;
            lastFreezetimeEnd = e.TimeEnd;

            TeamPlayers teams = GetTeams();
            processedData.TeamPlayersValues.Add(teams);
            processedData.TeamEquipmentValues.Add(GetTeamEquipment(teams));
        }

        private TeamPlayers GetTeams()
        {
            //work out teams at current round
            IEnumerable<Player> players = dp.PlayingParticipants;

            return new TeamPlayers
            {
                Terrorists = players.Where(p => p.Team is Team.Terrorist).ToList(),
                CounterTerrorists = players.Where(p => p.Team is Team.CounterTerrorist).ToList(),
                Round = RoundOfficiallyEndedCount + 1,
            };
        }

        private TeamEquipment GetTeamEquipment(TeamPlayers teams)
        {
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

            return new TeamEquipment
            {
                Round = RoundOfficiallyEndedCount + 1,
                TEquipValue = tEquipValue,
                CTEquipValue = ctEquipValue,
                TExpenditure = tExpenditure,
                CTExpenditure = ctExpenditure,
            };
        }

        #endregion

        #region Player Events

        private void PlayerKilledEventHandler(object sender, PlayerKilledEventArgs e)
        {
            e.Round = GetCurrentRoundNum(this, demoInfo.GameMode);

            processedData.PlayerKilledEventsValues.Add(e);
            processedData.WeaponValues.Add(e.Weapon);
            processedData.PenetrationValues.Add(e.PenetratedObjects);
            processedData.PlayerValues["Kills"].Add(e.Killer);
            processedData.PlayerValues["Deaths"].Add(e.Victim);

            if (e.Headshot)
                processedData.PlayerValues["Headshots"].Add(e.Killer);

            if (e.Assister is not null)
                processedData.PlayerValues["Assists"].Add(e.Assister);
        }

        private void PlayerHurtEventHandler(object sender, PlayerHurtEventArgs e)
        {
            var round = GetCurrentRoundNum(this, demoInfo.GameMode);

            // a player_death event is not triggered due to death by bomb explosion
            if (e.PossiblyKilledByBombExplosion)
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

                processedData.PlayerKilledEventsValues.Add(playerKilledEventArgs);
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

            processedData.PlayerHurtValues.Add(playerHurt);
        }

        private void RoundMVPEventHandler(object sender, RoundMVPEventArgs e)
        {
            processedData.PlayerValues["MVPs"].Add(e.Player);
        }

        private void PlayerDisconnectEventHandler(object sender, PlayerDisconnectEventArgs e)
        {
            if (e.Player != null && e.Player.Name != "unconnected" && e.Player.Name != "GOTV")
                processedData.DisconnectedPlayerValues.Add(
                    new DisconnectedPlayer
                    {
                        PlayerDisconnectEventArgs = e,
                        Round = GetCurrentRoundNum(this, demoInfo.GameMode) - 1,
                    }
                );
        }

        private void PlayerBindEventHandler(object sender, PlayerBindEventArgs e)
        {
            BindPlayer(e.Player);
        }

        private void PlayerPositionsEventHandler(object sender, PlayerPositionsEventArgs e)
        {
            foreach (PlayerPositionEventArgs playerPosition in e.PlayerPositions)
            {
                if (freezetimeEndedCount == 0)
                    continue;

                int round = GetCurrentRoundNum(this, demoInfo.GameMode);
                if (round <= 0 || playerPosition.Player.SteamID <= 0)
                    continue;

                bool playerAlive = CheckIfPlayerAliveAtThisPointInRound(this, playerPosition.Player, round);
                var freezetimeEndedThisRound = freezetimeEndedCount >= round;
                if (!playerAlive || !freezetimeEndedThisRound)
                    continue;

                processedData.PlayerPositionsValues.Add(
                    new PlayerPositionsInstance
                    {
                        Round = round,
                        TimeInRound = (int)e.CurrentTime - (int)lastFreezetimeEnd,
                        SteamID = playerPosition.Player.SteamID,
                        TeamSide = playerPosition.Player.Team is Team.Terrorist ? "T" : "CT",
                        XPosition = playerPosition.Player.Position.X,
                        YPosition = playerPosition.Player.Position.Y,
                        ZPosition = playerPosition.Player.Position.Z,
                    }
                );
            }
        }

        #endregion

        #region Bomb Events

        private void BombPlantedEventHandler(object sender, BombEventArgs e)
        {
            processedData.PlayerValues["Plants"].Add(e.Player);
            processedData.BombsitePlantValues.Add(
                new BombPlanted
                {
                    Round = RoundOfficiallyEndedCount + 1,
                    TimeInRound = e.TimeInRound,
                    Player = e.Player,
                    Bombsite = e.Site,
                }
            );
        }

        private void BombExplodedEventHandler(object sender, BombEventArgs e)
        {
            processedData.BombsiteExplodeValues.Add(
                new BombExploded
                {
                    Round = RoundOfficiallyEndedCount + 1,
                    TimeInRound = e.TimeInRound,
                    Player = e.Player,
                    Bombsite = e.Site,
                }
            );
        }

        private void BombDefusedEventHandler(object sender, BombEventArgs e)
        {
            processedData.PlayerValues["Defuses"].Add(e.Player);
            processedData.BombsiteDefuseValues.Add(
                new BombDefused
                {
                    Round = RoundOfficiallyEndedCount + 1,
                    TimeInRound = e.TimeInRound,
                    Player = e.Player,
                    Bombsite = e.Site,
                    HasKit = e.Player.HasDefuseKit,
                }
            );
        }

        #endregion

        #region Hostage Events

        private void HostageRescuedEventHandler(object sender, HostageRescuedEventArgs e)
        {
            processedData.PlayerValues["Rescues"].Add(e.Player);
            processedData.HostageRescueValues.Add(
                new HostageRescued
                {
                    Round = RoundOfficiallyEndedCount + 1,
                    TimeInRound = e.TimeInRound,
                    Player = e.Player,
                    Hostage = e.Hostage,
                    HostageIndex = e.HostageIndex,
                    RescueZone = e.RescueZone,
                }
            );
        }

        private void HostagePickedUpEventHandler(object sender, HostagePickedUpEventArgs e)
        {
            processedData.HostagePickedUpValues.Add(
                new HostagePickedUp
                {
                    Round = RoundOfficiallyEndedCount + 1,
                    TimeInRound = e.TimeInRound,
                    Player = e.Player,
                    Hostage = e.Hostage,
                    HostageIndex = e.HostageIndex,
                }
            );
        }

        #endregion

        #region Weapon Events

        private void WeaponFiredEventHandler(object sender, WeaponFiredEventArgs e)
        {
            processedData.PlayerValues["Shots"].Add(e.Shooter);
            processedData.ShotsFiredValues.Add(
                new ShotFired
                {
                    Round = GetCurrentRoundNum(this, demoInfo.GameMode),
                    TimeInRound = e.TimeInRound,
                    Shooter = e.Shooter,
                    TeamSide = e.Shooter.Team.ToString(),
                    Weapon = new Equipment(e.Weapon),
                }
            );
        }

        private void GrenadeEventHandler(object sender, NadeEventArgs e)
        {
            processedData.GrenadeValues.Add(e);
        }

        #endregion
    }
}
