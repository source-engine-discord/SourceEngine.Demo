using DemoInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class ShotFired
    {
        public int Round { get; set; }

        public Equipment Weapon { get; set; }

        public Player Shooter { get; set; }

        public ShotFired() { }
    }
}
