using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class BombsitePlant
    {
        public int Round { get; set; }
        public long SteamID { get; set; }
        public char Bombsite { get; set; }

        public BombsitePlant() { }
    }
}
