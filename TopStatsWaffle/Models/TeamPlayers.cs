using DemoInfo;
using System.Collections.Generic;

namespace TopStatsWaffle.Models
{
    public class TeamPlayers
    {
        public int Round { get; set; }
        public List<Player> Terrorists { get; set; }
        public List<Player> CounterTerrorists { get; set; }

        public TeamPlayers() { }
    }
}
