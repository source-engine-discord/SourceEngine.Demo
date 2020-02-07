using DemoInfo;

namespace TopStatsWaffle.Models
{
    public class HostageRescued
    {
        public int Round { get; set; }

        public double TimeInRound { get; set; }

        public Player Player { get; set; }

        public char Hostage { get; set; }

        public HostageRescued() { }
    }
}
