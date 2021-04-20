using System.Collections.Generic;

using SourceEngine.Demo.Parser;
using SourceEngine.Demo.Parser.Entities;

namespace SourceEngine.Demo.Stats.Models
{
    public class ProcessedData
    {
        public tanookiStats tanookiStats { get; set; }

        public List<MatchStartedEventArgs> MatchStartValues { get; set; } = new();

        public List<SwitchSidesEventArgs> SwitchSidesValues { get; set; } = new();

        public List<FeedbackMessage> MessagesValues { get; set; } = new();

        public List<TeamPlayers> TeamPlayersValues { get; set; } = new();

        public List<PlayerHurt> PlayerHurtValues { get; set; } = new();

        public List<PlayerKilledEventArgs> PlayerKilledEventsValues { get; set; } = new();

        public List<DisconnectedPlayer> DisconnectedPlayerValues { get; set; } = new();

        public Dictionary<string, List<Player>> PlayerValues { get; set; } = new()
        {
            { "Kills", new List<Player>() },
            { "Deaths", new List<Player>() },
            { "Headshots", new List<Player>() },
            { "Assists", new List<Player>() },
            { "MVPs", new List<Player>() },
            { "Shots", new List<Player>() },
            { "Plants", new List<Player>() },
            { "Defuses", new List<Player>() },
            { "Rescues", new List<Player>() },
        };

        public List<Equipment> WeaponValues { get; set; } = new();

        public List<int> PenetrationValues { get; set; } = new();

        public List<BombPlanted> BombsitePlantValues { get; set; } = new();

        public List<BombExploded> BombsiteExplodeValues { get; set; } = new();

        public List<BombDefused> BombsiteDefuseValues { get; set; } = new();

        public List<HostageRescued> HostageRescueValues { get; set; } = new();

        public List<HostagePickedUp> HostagePickedUpValues { get; set; } = new();

        public List<Team> TeamValues { get; set; } = new();

        public List<RoundEndReason> RoundEndReasonValues { get; set; } = new();

        public List<double> RoundLengthValues { get; set; } = new();

        public List<TeamEquipment> TeamEquipmentValues { get; set; } = new();

        public List<NadeEventArgs> GrenadeValues { get; set; } = new();

        public List<ChickenKilledEventArgs> ChickenValues { get; set; } = new();

        public List<ShotFired> ShotsFiredValues { get; set; } = new();

        public List<PlayerPositionsInstance> PlayerPositionsValues { get; set; } = new();

        public Dictionary<int, long> PlayerLookups { get; set; } = new();

        public Dictionary<int, int> PlayerReplacements { get; set; } = new();

        public Dictionary<int, TickCounter> PlayerTicks { get; set; } = new();

        public bool WriteTicks { get; set; }
    }
}
