namespace TopStatsWaffle.Models
{
    public class FeedbackMessage
    {
        public int Round { get; set; }
        public long SteamID { get; set; }
        public string TeamName { get; set; }
        public string Message { get; set; }

        public FeedbackMessage() { }
    }
}
