namespace SourceEngine.Demo.Stats.Models
{
	public class PlayerPositionBySteamID
	{
		public long SteamID { get; set; }
		public double XPosition { get; set; }
		public double YPosition { get; set; }
		public double ZPosition { get; set; }

		public PlayerPositionBySteamID() { }
	}
}
