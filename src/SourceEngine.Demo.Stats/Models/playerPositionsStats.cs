using System.Collections.Generic;

namespace SourceEngine.Demo.Stats.Models
{
	public class PlayerPositionsStats
    {
        public string DemoName { get; set; }
        public List<PlayerPositionByRound> PlayerPositionByRound { get; set; }

        public PlayerPositionsStats() { }
    }
}
