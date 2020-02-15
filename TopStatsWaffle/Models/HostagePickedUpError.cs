namespace TopStatsWaffle.Models
{
	public class HostagePickedUpError
	{
		public char Hostage { get; set; }
		public int HostageIndex { get; set; }
		public string ErrorMessage { get; set; }

		public HostagePickedUpError() { }
	}
}
