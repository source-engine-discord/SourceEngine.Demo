using SourceEngine.Demo.Parser;

namespace SourceEngine.Demo.Stats.Models
{
    public class HostageRescued
    {
        public int Round { get; set; }

        public double TimeInRound { get; set; }

        public Player Player { get; set; }

        public char Hostage { get; set; }

        public int HostageIndex { get; set; }

        public int RescueZone { get; set; }

        public double XPosition { get; set; }

        public double YPosition { get; set; }

        public double ZPosition { get; set; }
    }
}
