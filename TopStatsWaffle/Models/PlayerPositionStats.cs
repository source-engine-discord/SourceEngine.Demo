using DemoInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class PlayerPositionStats
    {
        public long KillerSteamID { get; set; }
        public double XPositionKill { get; set; }
        public double YPositionKill { get; set; }
        public double ZPositionKill { get; set; }
        public long VictimSteamID { get; set; }
        public double XPositionDeath { get; set; }
        public double YPositionDeath { get; set; }
        public double ZPositionDeath { get; set; }
        public string Weapon { get; set; }
        public int PenetrationsCount { get; set; }

        public PlayerPositionStats() { }
    }
}
