using SourceEngine.Demo.Parser;
using SourceEngine.Demo.Parser.Entities;

namespace SourceEngine.Demo.Stats.Models
{
    public class PlayerHurt
    {
        public int Round { get; set; }

        public double TimeInRound { get; set; }

        public Player Player { get; set; }

        public double XPositionPlayer { get; set; }

        public double YPositionPlayer { get; set; }

        public double ZPositionPlayer { get; set; }

        public Player Attacker { get; set; }

        public double XPositionAttacker { get; set; }

        public double YPositionAttacker { get; set; }

        public double ZPositionAttacker { get; set; }

        public int Health { get; set; }

        public int Armor { get; set; }

        public Equipment Weapon { get; set; }

        public int HealthDamage { get; set; }

        public int ArmorDamage { get; set; }

        public HitGroup HitGroup { get; set; }

        public bool PossiblyKilledByBombExplosion { get; set; }
    }
}
