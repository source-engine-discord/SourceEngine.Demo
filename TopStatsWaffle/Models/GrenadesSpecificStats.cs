using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class GrenadesSpecificStats
    {
        public string NadeType { get; set; }
        public long SteamID { get; set; }
        public double XPosition { get; set; }
        public double YPosition { get; set; }
        public double ZPosition { get; set; }
        public int? NumPlayersFlashed { get; set; }

        public GrenadesSpecificStats() { }
    }
}
