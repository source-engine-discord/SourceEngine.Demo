namespace TopStatsWaffle.Models
{
    public class playerStats
    {
        public string PlayerName { get; set; }
        public long SteamID { get; set; }
        public int Kills { get; set; }
        public int KillsIncludingBots { get; set; }
        public int Deaths { get; set; }
        public int DeathsIncludingBots { get; set; }
        public int Headshots { get; set; }
        public int Assists { get; set; }
        public int AssistsIncludingBots { get; set; }
        public int MVPs { get; set; }
        public int Shots { get; set; }
        public int Plants { get; set; }
        public int Defuses { get; set; }
        public int Rescues { get; set; }
        public long TicksAlive { get; set; }
        public long TicksOnServer { get; set; }
        public long TicksPlaying { get; set; }

        public playerStats() { }
    }
}
