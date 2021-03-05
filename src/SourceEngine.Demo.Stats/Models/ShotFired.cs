using SourceEngine.Demo.Parser;

namespace SourceEngine.Demo.Stats.Models
{
    public class ShotFired
    {
        public ShotFired() { }

        public int Round { get; set; }

        public double TimeInRound { get; set; }

        public Player Shooter { get; set; }

        public string TeamSide { get; set; }

        public Equipment Weapon { get; set; }
    }
}
