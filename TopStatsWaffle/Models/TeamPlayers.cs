using DemoInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class TeamPlayers
    {
        public List<Player> Terrorists { get; set; }
        public List<Player> CounterTerrorists { get; set; }
        public int Round { get; set; }

        public TeamPlayers() { }
    }
}
