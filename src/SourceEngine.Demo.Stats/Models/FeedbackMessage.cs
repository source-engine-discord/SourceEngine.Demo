namespace SourceEngine.Demo.Stats.Models
{
    public class FeedbackMessage
    {
        public int Round { get; set; }
        public long SteamID { get; set; }
        public string TeamName { get; set; }
        public double XCurrentPosition { get; set; }
        public double YCurrentPosition { get; set; }
        public double ZCurrentPosition { get; set; }
        public double? XLastAlivePosition { get; set; } // If alive, LastAlivePosition values are set to null in dp.SayText2 +=
        public double? YLastAlivePosition { get; set; }
        public double? ZLastAlivePosition { get; set; }
        public float? XCurrentViewAngle { get; set; }
        public float? YCurrentViewAngle { get; set; }
        public string SetPosCommandCurrentPosition { get; set; }
        public string Message { get; set; }
        public double TimeInRound { get; set; }

        public FeedbackMessage() { }
    }
}
