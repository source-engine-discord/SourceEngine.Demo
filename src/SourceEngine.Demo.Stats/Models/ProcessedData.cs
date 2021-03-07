using System.Collections.Generic;

using SourceEngine.Demo.Parser;
using SourceEngine.Demo.Parser.Entities;

namespace SourceEngine.Demo.Stats.Models
{
    public class ProcessedData
    {
        public tanookiStats tanookiStats { get; set; }

        public IEnumerable<MatchStartedEventArgs> MatchStartValues { get; set; }

        public IEnumerable<SwitchSidesEventArgs> SwitchSidesValues { get; set; }

        public IEnumerable<FeedbackMessage> MessagesValues { get; set; }

        public IEnumerable<TeamPlayers> TeamPlayersValues { get; set; }

        public IEnumerable<PlayerHurt> PlayerHurtValues { get; set; }

        public IEnumerable<PlayerKilledEventArgs> PlayerKilledEventsValues { get; set; }

        public Dictionary<string, IEnumerable<Player>> PlayerValues { get; set; }

        public IEnumerable<Equipment> WeaponValues { get; set; }

        public IEnumerable<int> PenetrationValues { get; set; }

        public IEnumerable<BombPlanted> BombsitePlantValues { get; set; }

        public IEnumerable<BombExploded> BombsiteExplodeValues { get; set; }

        public IEnumerable<BombDefused> BombsiteDefuseValues { get; set; }

        public IEnumerable<HostageRescued> HostageRescueValues { get; set; }

        public IEnumerable<HostagePickedUp> HostagePickedUpValues { get; set; }

        public IEnumerable<Team> TeamValues { get; set; }

        public IEnumerable<RoundEndReason> RoundEndReasonValues { get; set; }

        public IEnumerable<double> RoundLengthValues { get; set; }

        public IEnumerable<TeamEquipment> TeamEquipmentValues { get; set; }

        public IEnumerable<NadeEventArgs> GrenadeValues { get; set; }

        public IEnumerable<ChickenKilledEventArgs> ChickenValues { get; set; }

        public IEnumerable<ShotFired> ShotsFiredValues { get; set; }

        public IEnumerable<PlayerPositionsInstance> PlayerPositionsValues { get; set; }

        public bool WriteTicks { get; set; }
    }
}
