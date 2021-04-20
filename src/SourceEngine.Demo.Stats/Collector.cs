using System.Collections.Generic;
using System.Linq;

using SourceEngine.Demo.Parser;
using SourceEngine.Demo.Parser.Entities;
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

    public partial class Collector
    {
        private ProcessedData processedData = new();
        private readonly DemoParser dp;
        private readonly DemoInformation demoInfo;

        public Collector(DemoParser parser, DemoInformation info)
        {
            dp = parser;
            demoInfo = info;

            BindEventHandlers();
        }

        public ProcessedData Collect()
        {
            dp.ParseHeader();
            dp.ParseToEnd();
            FinaliseData();

            return processedData;
        }

        private void AddTick(Player p, PSTATUS status)
        {
            bool userIdStored = BindPlayer(p);

            if (userIdStored)
            {
                if (status == PSTATUS.ONSERVER)
                    processedData.PlayerTicks[p.UserID].ticksOnServer++;

                if (status == PSTATUS.ALIVE)
                    processedData.PlayerTicks[p.UserID].ticksAlive++;

                if (status == PSTATUS.PLAYING)
                    processedData.PlayerTicks[p.UserID].ticksPlaying++;
            }
        }

        private void BindEventHandlers()
        {
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

            // HOSTAGE EVENTS ==================================================
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

            // PLAYER TICK HANDLER =============================================
            dp.TickDone += TickDoneEventHandler;
        }

        /// <summary>
        /// Adds new player lookups and tick values
        /// </summary>
        /// <param name="p"></param>
        /// <returns>Whether or not the userID given has newly been / was previously stored</returns>
        private bool BindPlayer(Player p)
        {
            int duplicateIdToRemoveTicks = 0;
            int duplicateIdToRemoveLookup = 0;

            if (p.Name != "unconnected" && p.Name != "GOTV")
            {
                if (!processedData.PlayerTicks.ContainsKey(p.UserID))
                {
                    // check if player has been added twice with different UserIDs
                    (int userId, TickCounter counter) =
                        processedData.PlayerTicks.FirstOrDefault(x => x.Value.detectedName == p.Name);

                    if (userId != 0)
                    {
                        // copy duplicate's information across
                        processedData.PlayerTicks.Add(
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
                        processedData.PlayerTicks.Add(p.UserID, new TickCounter { detectedName = detectedName });
                    }
                }

                if (!processedData.PlayerLookups.ContainsKey(p.UserID))
                {
                    // check if player has been added twice with different UserIDs
                    KeyValuePair<int, long> duplicate =
                        processedData.PlayerLookups.FirstOrDefault(x => x.Value == p.SteamID);

                    if (duplicate.Key == 0) // if the steam ID was 0
                        duplicate = processedData.PlayerLookups.FirstOrDefault(x => x.Key == duplicateIdToRemoveTicks);

                    if (p.SteamID != 0)
                        processedData.PlayerLookups.Add(p.UserID, p.SteamID);
                    else if (p.SteamID == 0 && duplicate.Key != 0)
                        processedData.PlayerLookups.Add(p.UserID, duplicate.Value);

                    duplicateIdToRemoveLookup = duplicate.Key;
                }

                // remove duplicates
                if (duplicateIdToRemoveTicks != 0 || duplicateIdToRemoveLookup != 0)
                {
                    if (duplicateIdToRemoveTicks != 0)
                        processedData.PlayerTicks.Remove(duplicateIdToRemoveTicks);

                    if (duplicateIdToRemoveLookup != 0)
                        processedData.PlayerLookups.Remove(duplicateIdToRemoveLookup);

                    /* store duplicate userIDs for replacing in events later on */
                    var idRemoved = duplicateIdToRemoveLookup != 0
                        ? duplicateIdToRemoveLookup
                        : duplicateIdToRemoveTicks;

                    // removes any instance of the old userID pointing to a different userID
                    if (processedData.PlayerReplacements.Any(r => r.Key == idRemoved))
                        processedData.PlayerReplacements.Remove(idRemoved);

                    // tries to avoid infinite loops by removing the old entry
                    if (processedData.PlayerReplacements.Any(r => r.Key == p.UserID && r.Value == idRemoved))
                        processedData.PlayerReplacements.Remove(p.UserID);

                    // replace current mappings between an ancient userID & the old userID, to use the new userID as the value instead
                    if (processedData.PlayerReplacements.Any(r => r.Value == idRemoved))
                    {
                        IEnumerable<int> keysToReplaceValue = processedData.PlayerReplacements
                            .Where(r => r.Value == idRemoved).Select(r => r.Key);

                        foreach (var userId in keysToReplaceValue.ToList())
                            processedData.PlayerReplacements[userId] = p.UserID;
                    }

                    processedData.PlayerReplacements.Add(
                        idRemoved,
                        p.UserID
                    ); // Creates a new entry that maps the player's old user ID to their new user ID
                }

                return true;
            }

            return false;
        }

        private static bool CheckIfPlayerAliveAtThisPointInRound(Collector collector, Player player, int round)
        {
            return !collector.processedData.PlayerKilledEventsValues.Any(
                e => e.Round == round && e.Victim?.SteamID != 0 && e.Victim.SteamID == player?.SteamID
            );
        }

        private static tanookiStats CreateTanookiStats(
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

        private void FinaliseData()
        {
            // Only keep the first event for each round.
            // TODO: is this still necessary?
            processedData.BombsitePlantValues = processedData.BombsitePlantValues.GroupBy(e => e.Round)
                .Select(group => group.FirstOrDefault()).ToList();

            processedData.BombsiteDefuseValues = processedData.BombsiteDefuseValues.GroupBy(e => e.Round)
                .Select(group => group.FirstOrDefault()).ToList();

            processedData.BombsiteExplodeValues = processedData.BombsiteExplodeValues.GroupBy(e => e.Round)
                .Select(group => group.FirstOrDefault()).ToList();

            // Remove extra TeamPlayers if freezetime_end event triggers once a playtest is finished.
            processedData.TeamPlayersValues = processedData.TeamPlayersValues
                .Where(tp => tp.Round <= processedData.TeamValues.Count).ToList();

            processedData.tanookiStats = CreateTanookiStats(
                processedData.TeamPlayersValues,
                processedData.DisconnectedPlayerValues
            );

            processedData.WriteTicks = true;
        }

        private static string GenerateSetPosCommand(Player player)
        {
            if (player is null)
                return "";

            // Z axis for setang is optional.
            return $"setpos {player.Position.X} {player.Position.Y} {player.Position.Z}; "
                + $"setang {player.ViewDirectionX} {player.ViewDirectionY}";
        }

        private static int GetCurrentRoundNum(Collector collector, GameMode gameMode)
        {
            List<TeamPlayers> teamPlayersList = collector.processedData.TeamPlayersValues;
            int round = 0;

            if (teamPlayersList.Count > 0 && teamPlayersList.Any(t => t.Round == 1))
            {
                TeamPlayers teamPlayers = teamPlayersList.First(t => t.Round == 1);

                if (teamPlayers.Terrorists.Count > 0 && teamPlayers.CounterTerrorists.Count > 0)
                    round = collector.RoundOfficiallyEndedCount + 1;
            }

            // add 1 for roundsCount when in danger zone
            if (gameMode is GameMode.DangerZone)
                round++;

            return round;
        }

        private static int? GetMinRoundsForWin(GameMode gameMode, TestType testType)
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

        private static bool IsMessageFeedback(string text)
        {
            return text.ToLower().StartsWith(">fb") || text.ToLower().StartsWith(">feedback")
                || text.ToLower().StartsWith("!fb") || text.ToLower().StartsWith("!feedback");
        }
    }
}
