using System;
using System.Collections.Generic;
using System.Text;

namespace SourceEngine.Demo.Stats.Models
{
    public class firstDamageStats
    {
        public int Round { get; set; }
        public List<DamageGivenByPlayerInRound> FirstDamageToEnemyByPlayers { get; set; }

        public firstDamageStats() { }
    }
}
