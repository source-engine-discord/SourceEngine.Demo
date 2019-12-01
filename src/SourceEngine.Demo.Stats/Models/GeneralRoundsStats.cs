using DemoInfo;
using System.Collections.Generic;

namespace SourceEngine.Demo.Stats.Models
{
	public class GeneralroundsStats
	{
		public List<roundsStats> roundsStats { get; set; }
		public winnersStats winnersStats { get; set; }
		public IEnumerable<SwitchSidesEventArgs> SwitchSides { get;set; }

		public GeneralroundsStats() { }
	}
}
