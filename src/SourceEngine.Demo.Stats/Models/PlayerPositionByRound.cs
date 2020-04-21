using System.Collections.Generic;

namespace SourceEngine.Demo.Stats.Models
{
	public class PlayerPositionByRound
	{
		public int Round { get; set; }
		public List<PlayerPositionByTimeInRound> PlayerPositionByTimeInRound { get; set; }

		public PlayerPositionByRound() { }
	}
}
