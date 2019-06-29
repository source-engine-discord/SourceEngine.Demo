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
        public int XPosition { get; set; }
        public int YPosition { get; set; }
        public int ZPosition { get; set; }
        public int? NumPlayersFlashed { get; set; }

        public GrenadesSpecificStats() { }
    }
}
