using SourceEngine.Demo.Parser;
using System;
using System.Collections.Generic;
using System.Text;

namespace SourceEngine.Demo.Stats.Models
{
	public class PlayerHurt
	{
		public int Round { get; set; }
		public double TimeInRound { get; set; }
		public Player Player { get; set; }
		public Player Attacker { get; set; }
		public int Health { get; set; }
		public int Armor { get; set; }
		public Equipment Weapon { get; set; }
		public int HealthDamage { get; set; }
		public int ArmorDamage { get; set; }
		public Hitgroup Hitgroup { get; set; }
		public bool PossiblyKilledByBombExplosion { get; set; }

		public PlayerHurt() { }

		public PlayerHurt(PlayerHurtEventArgs playerHurtEventArgs, int round)
		{
			Round = round;
			TimeInRound = playerHurtEventArgs.TimeInRound;
			Player = new Player(playerHurtEventArgs.Player);
			Attacker = new Player(playerHurtEventArgs.Attacker);
			Health = playerHurtEventArgs.Health;
			Armor = playerHurtEventArgs.Armor;
			Weapon = new Equipment(playerHurtEventArgs.Weapon);
			HealthDamage = playerHurtEventArgs.HealthDamage;
			ArmorDamage = playerHurtEventArgs.ArmorDamage;
			Hitgroup = playerHurtEventArgs.Hitgroup;
			PossiblyKilledByBombExplosion = playerHurtEventArgs.PossiblyKilledByBombExplosion;
		}
	}
}
