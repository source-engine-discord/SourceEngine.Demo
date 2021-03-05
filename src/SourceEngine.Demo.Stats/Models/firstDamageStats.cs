using System.Collections.Generic;

namespace SourceEngine.Demo.Stats.Models
{
    public class firstDamageStats
    {
        public int Round { get; set; }

        public List<DamageGivenByPlayerInRound> FirstDamageToEnemyByPlayers { get; set; }

        public firstDamageStats() { }
    }
}
