using DemoInfo;
using System.Collections.Generic;

namespace TopStatsWaffle.Models
{
	public class GeneralroundsStats
	{
		public List<roundsStats> roundsStats { get; set; }
		public winnersStats winnersStats { get; set; }
		public IEnumerable<SwitchSidesEventArgs> SwitchSides { get;set; }

		public GeneralroundsStats() { }
	}
}
