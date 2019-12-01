using DemoInfo;
using System.Collections.Generic;

namespace SourceEngine.Demo.Stats.Models
{
    public class TeamPlayers
    {
        public int Round { get; set; }
        public List<Player> Terrorists { get; set; }
        public List<Player> CounterTerrorists { get; set; }

        public TeamPlayers() { }
    }
}
