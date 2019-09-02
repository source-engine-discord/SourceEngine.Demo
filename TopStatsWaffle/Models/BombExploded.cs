using DemoInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class BombExploded
    {
        public int Round { get; set; }
        public double TimeInRound { get; set; }
        public Player Player { get; set; }
        public char Bombsite { get; set; }

        public BombExploded() { }
    }
}
