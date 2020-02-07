using DemoInfo;

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
