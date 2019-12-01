using System.Collections.Generic;

using SourceEngine.Demo.Parser;

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
