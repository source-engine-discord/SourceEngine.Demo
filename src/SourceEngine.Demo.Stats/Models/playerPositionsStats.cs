namespace SourceEngine.Demo.Stats.Models
{
	public class playerPositionsStats
    {
        public int Round { get; set; }
        public double TimeInRound { get; set; }
        public long PlayerSteamID { get; set; }
        public double XPosition { get; set; }
        public double YPosition { get; set; }
        public double ZPosition { get; set; }

        public playerPositionsStats() { }
    }
}
