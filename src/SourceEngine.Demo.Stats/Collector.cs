using System.Collections.Generic;
using System.Linq;

using SourceEngine.Demo.Parser;
using SourceEngine.Demo.Parser.Entities;
using SourceEngine.Demo.Stats.Models;

namespace SourceEngine.Demo.Stats
{
    internal enum PlayerStatus
    {
        OnServer,
        Playing,
        Alive,
    }

    public class TickCounter
    {
        public string PlayerName = "NOT FOUND";
        public long TicksAlive;
        public long TicksOnServer;
        public long TicksPlaying;
    }

    public partial class Collector
    {
        private CollectedData data = new();
        private readonly DemoParser parser;
        private readonly DemoInformation demoInfo;

        public Collector(DemoParser parser, DemoInformation info)
        {
            this.parser = parser;
            demoInfo = info;

            BindEventHandlers();
        }

        public CollectedData Collect()
        {
            parser.ParseHeader();
            parser.ParseToEnd();
            FinaliseData();

            return data;
        }

        private void AddTick(Player p, PlayerStatus status)
        {
            bool userIdStored = BindPlayer(p);

            if (userIdStored)
            {
                if (status == PlayerStatus.OnServer)
                    data.PlayerTicks[p.UserID].TicksOnServer++;

                if (status == PlayerStatus.Alive)
                    data.PlayerTicks[p.UserID].TicksAlive++;

                if (status == PlayerStatus.Playing)
                    data.PlayerTicks[p.UserID].TicksPlaying++;
            }
        }

        private void BindEventHandlers()
        {
            // SERVER EVENTS ===================================================
            parser.MatchStarted += MatchStartedEventHandler;
            parser.ChickenKilled += ChickenKilledEventHandler;
            parser.SayText2 += SayText2EventHandler;
            parser.RoundEnd += RoundEndEventHandler;
            parser.RoundOfficiallyEnded += RoundOfficiallyEndedEventHandler;
            parser.SwitchSides += SwitchSidesEventHandler;
            parser.FreezetimeEnded += FreezetimeEndedEventHandler;

            // PLAYER EVENTS ===================================================
            parser.PlayerKilled += PlayerKilledEventHandler;
            parser.PlayerHurt += PlayerHurtEventHandler;
            parser.RoundMVP += RoundMVPEventHandler;
            parser.PlayerDisconnect += PlayerDisconnectEventHandler;
            parser.PlayerBind += PlayerBindEventHandler;
            parser.PlayerPositions += PlayerPositionsEventHandler;

            // BOMB EVENTS =====================================================
            parser.BombPlanted += BombPlantedEventHandler;
            parser.BombExploded += BombExplodedEventHandler;
            parser.BombDefused += BombDefusedEventHandler;

            // HOSTAGE EVENTS ==================================================
            parser.HostageRescued += HostageRescuedEventHandler;
            parser.HostagePickedUp += HostagePickedUpEventHandler;

            // WEAPON EVENTS ===================================================
            parser.WeaponFired += WeaponFiredEventHandler;

            // GRENADE EVENTS ==================================================
            parser.ExplosiveNadeExploded += GrenadeEventHandler;
            parser.FireNadeStarted += GrenadeEventHandler;
            parser.SmokeNadeStarted += GrenadeEventHandler;
            parser.FlashNadeExploded += GrenadeEventHandler;
            parser.DecoyNadeStarted += GrenadeEventHandler;

            // PLAYER TICK HANDLER =============================================
            parser.TickDone += TickDoneEventHandler;
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

            if (p.Name is "unconnected" or "GOTV")
                return false;

            // Add the player to PlayerTicks if they aren't already in there.
            if (!data.PlayerTicks.ContainsKey(p.UserID))
            {
                // Check if player has been added twice with different user IDs by comparing player names.
                (int userId, TickCounter counter) =
                    data.PlayerTicks.FirstOrDefault(x => x.Value.PlayerName == p.Name);

                // A player with the same name was found in PlayerTicks.
                if (userId != 0)
                {
                    // Copy duplicate's information across.
                    data.PlayerTicks.Add(
                        p.UserID,
                        new TickCounter
                        {
                            PlayerName = counter.PlayerName,
                            TicksAlive = counter.TicksAlive,
                            TicksOnServer = counter.TicksOnServer,
                            TicksPlaying = counter.TicksPlaying,
                        }
                    );

                    duplicateIdToRemoveTicks = userId; // Mark the previous ID for removal.
                }
                else
                {
                    var detectedName = string.IsNullOrWhiteSpace(p.Name) ? "NOT FOUND" : p.Name;
                    data.PlayerTicks.Add(p.UserID, new TickCounter { PlayerName = detectedName });
                }
            }

            // Add the player to PlayerLookups if they aren't already in there.
            if (!data.PlayerLookups.ContainsKey(p.UserID))
            {
                // Check if player has been added twice with different user IDs by comparing Steam IDs.
                KeyValuePair<int, long> duplicate =
                    data.PlayerLookups.FirstOrDefault(x => x.Value == p.SteamID);

                // A duplicate was not found. Try again by using the duplicate PlayerTicks user ID.
                if (duplicate.Key == 0)
                    duplicate = data.PlayerLookups.FirstOrDefault(x => x.Key == duplicateIdToRemoveTicks);

                if (p.SteamID != 0)
                    // Given player has a valid Steam ID; map their user ID to their Steam ID.
                    data.PlayerLookups.Add(p.UserID, p.SteamID);
                else if (duplicate.Key != 0)
                    // Map user ID to the duplicate Steam ID when the given player's Steam ID is 0.
                    data.PlayerLookups.Add(p.UserID, duplicate.Value);

                // Mark the previous ID for removal. Will remain 0 if no duplicate was found.
                duplicateIdToRemoveLookup = duplicate.Key;
            }

            // Remove duplicates.
            if (duplicateIdToRemoveTicks != 0 || duplicateIdToRemoveLookup != 0)
            {
                if (duplicateIdToRemoveTicks != 0)
                    data.PlayerTicks.Remove(duplicateIdToRemoveTicks);

                if (duplicateIdToRemoveLookup != 0)
                    data.PlayerLookups.Remove(duplicateIdToRemoveLookup);

                // Store duplicate user IDs for replacing in events later on.
                var idRemoved = duplicateIdToRemoveLookup != 0
                    ? duplicateIdToRemoveLookup
                    : duplicateIdToRemoveTicks;

                // Remove any instance of the old ID pointing to a different user ID.
                // This probably isn't necessary, but it's left to avoid the risk of breaking anything.
                data.PlayerReplacements.Remove(idRemoved);

                // Remove the old entry to try to avoid infinite loops.
                if (data.PlayerReplacements.TryGetValue(p.UserID, out int storedOldId) && storedOldId == idRemoved)
                    data.PlayerReplacements.Remove(p.UserID);

                // Replace all occurrences of mappings *to* the old ID with the new ID.
                foreach ((int key, int value) in data.PlayerReplacements)
                {
                    if (value == idRemoved)
                        data.PlayerReplacements[key] = p.UserID;
                }

                // Create a new entry that maps the player's old user ID to their new user ID.
                data.PlayerReplacements.Add(idRemoved, p.UserID);
            }

            return true;
        }

        private static bool CheckIfPlayerAliveAtThisPointInRound(Collector collector, Player player, int round)
        {
            return !collector.data.PlayerKilledEventsValues.Any(
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
            data.BombsitePlantValues = data.BombsitePlantValues.GroupBy(e => e.Round)
                .Select(group => group.FirstOrDefault()).ToList();

            data.BombsiteDefuseValues = data.BombsiteDefuseValues.GroupBy(e => e.Round)
                .Select(group => group.FirstOrDefault()).ToList();

            data.BombsiteExplodeValues = data.BombsiteExplodeValues.GroupBy(e => e.Round)
                .Select(group => group.FirstOrDefault()).ToList();

            // Remove extra TeamPlayers if freezetime_end event triggers once a playtest is finished.
            data.TeamPlayersValues = data.TeamPlayersValues
                .Where(tp => tp.Round <= data.TeamValues.Count).ToList();

            data.tanookiStats = CreateTanookiStats(
                data.TeamPlayersValues,
                data.DisconnectedPlayerValues
            );

            data.WriteTicks = true;
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
            List<TeamPlayers> teamPlayersList = collector.data.TeamPlayersValues;
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
