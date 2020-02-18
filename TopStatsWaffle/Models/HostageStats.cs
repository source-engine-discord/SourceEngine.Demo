namespace TopStatsWaffle.Models
{
    public class hostageStats
    {
        public char Hostage { get; set; }
        public int? HostageIndex { get; set; }
        public int PickedUps { get; set; }
        public int Rescues { get; set; }

        public hostageStats() { }
    }
}
