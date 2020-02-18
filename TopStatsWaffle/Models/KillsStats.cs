namespace TopStatsWaffle.Models
{
    public class killsStats
    {
        public int Round { get; set; }
        public double TimeInRound { get; set; }
        public string Weapon { get; set; }
        public long KillerSteamID { get; set; }
        public bool KillerBotTakeover { get; internal set; }
        public double XPositionKill { get; set; }
        public double YPositionKill { get; set; }
        public double ZPositionKill { get; set; }
        public long VictimSteamID { get; set; }
        public bool VictimBotTakeover { get; internal set; }
        public double XPositionDeath { get; set; }
        public double YPositionDeath { get; set; }
        public double ZPositionDeath { get; set; }
        public long AssisterSteamID { get; set; }
        public bool AssisterBotTakeover { get; internal set; }
        public bool FirstKillOfTheRound { get; set; }
        public bool Suicide { get; internal set; }
        public bool TeamKill { get; internal set; }
        public int PenetrationsCount { get; set; }
        public bool Headshot { get; internal set; }
        public bool AssistedFlash { get; internal set; }

        public killsStats() { }
    }
}
