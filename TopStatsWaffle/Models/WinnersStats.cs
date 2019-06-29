using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class WinnersStats
    {
        public string WinningTeam { get; set; }
        public int TeamAlphaRounds { get; set; }
        public int TeamBetaRounds { get; set; }

        public WinnersStats() { }
    }
}
