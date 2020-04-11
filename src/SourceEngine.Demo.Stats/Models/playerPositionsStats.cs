using System.Collections.Generic;

namespace SourceEngine.Demo.Stats.Models
{
	public class playerPositionsStats
    {
        public int Round { get; set; }
        public List<PlayerPositionByTimeInRound> PlayerPositionByTimeInRound { get; set; }

        public playerPositionsStats() { }
    }
}
