using DemoInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class BombPlanted
    {
        public int Round { get; set; }
        public Player Player { get; set; }
        public char Bombsite { get; set; }

        public BombPlanted() { }
    }
}
