using DemoInfo;
using System.Collections.Generic;

namespace TopStatsWaffle.Models
{
	public class GeneralRoundsStats
	{
		public List<RoundsStats> RoundsStats { get; set; }
		public WinnersStats WinnersStats { get; set; }
		public IEnumerable<SwitchSidesEventArgs> SwitchSides { get;set; }

		public GeneralRoundsStats() { }
	}
}
