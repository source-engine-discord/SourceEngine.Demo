namespace SourceEngine.Demo.Stats.Models
{
    public class DamageGivenByPlayerInRound
    {
        public DamageGivenByPlayerInRound() { }

        public double TimeInRound { get; set; }

        public string TeamSideShooter { get; set; }

        public long SteamIDShooter { get; set; }

        public double XPositionShooter { get; set; }

        public double YPositionShooter { get; set; }

        public double ZPositionShooter { get; set; }

        public string TeamSideVictim { get; set; }

        public long SteamIDVictim { get; set; }

        public double XPositionVictim { get; set; }

        public double YPositionVictim { get; set; }

        public double ZPositionVictim { get; set; }

        public string Weapon { get; set; }

        public string WeaponClass { get; set; }

        public string WeaponType { get; set; }
    }
}
