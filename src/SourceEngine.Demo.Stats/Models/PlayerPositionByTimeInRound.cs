using System.Collections.Generic;

namespace SourceEngine.Demo.Stats.Models
{
    public class PlayerPositionByTimeInRound
    {
        public int TimeInRound { get; set; }

        public List<PlayerPositionBySteamID> PlayerPositionBySteamID { get; set; }
    }
}
