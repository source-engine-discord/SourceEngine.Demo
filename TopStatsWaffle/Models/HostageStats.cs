namespace TopStatsWaffle.Models
{
    public class HostageStats
    {
        public char Hostage { get; set; }
        public int? HostageIndex { get; set; }
        public int PickedUps { get; set; }
        public int Rescues { get; set; }

        public HostageStats() { }
    }
}
