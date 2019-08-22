using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class BombsiteStats
    {
        public char Bombsite { get; set; }
        public int Plants { get; set; }
        public int Explosions { get; set; }
        public int Defuses { get; set; }

        public BombsiteStats() { }
    }
}
