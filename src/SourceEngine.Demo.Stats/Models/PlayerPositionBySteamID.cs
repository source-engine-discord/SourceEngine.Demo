namespace SourceEngine.Demo.Stats.Models
{
    public class PlayerPositionBySteamID
    {
        public long SteamID { get; set; }
        public string TeamSide { get; set; }
        public int XPosition { get; set; }
        public int YPosition { get; set; }
        public int ZPosition { get; set; }

        public PlayerPositionBySteamID() { }
    }
}
