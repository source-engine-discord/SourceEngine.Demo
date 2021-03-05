using System.Collections.Generic;

using SourceEngine.Demo.Parser.Packet;

namespace SourceEngine.Demo.Parser
{
    public class Player
    {
        internal int ActiveWeaponID;

        internal int[] AmmoLeft = new int[32];

        internal Entity Entity;

        internal Dictionary<int, Equipment> rawWeapons = new();

        internal int TeamID;

        public Player()
        {
            Velocity = new Vector();
            LastAlivePosition = new Vector();
        }

        public Player(Player player)
        {
            if (player == null)
                return;

            Name = player.Name;
            SteamID = player.SteamID;
            Position = player.Position;
            EntityID = player.EntityID;
            UserID = player.UserID;
            HP = player.HP;
            Armor = player.Armor;
            LastAlivePosition = player.LastAlivePosition;
            Velocity = player.Velocity;
            ViewDirectionX = player.ViewDirectionX;
            ViewDirectionY = player.ViewDirectionY;
            FlashDuration = player.FlashDuration;
            Money = player.Money;
            CurrentEquipmentValue = player.CurrentEquipmentValue;
            FreezetimeEndEquipmentValue = player.FreezetimeEndEquipmentValue;
            RoundStartEquipmentValue = player.RoundStartEquipmentValue;
            IsDucking = player.IsDucking;
            Entity = player.Entity;
            Disconnected = player.Disconnected;
            ActiveWeaponID = player.ActiveWeaponID;
            rawWeapons = player.rawWeapons;
            Team = player.Team;
            HasDefuseKit = player.HasDefuseKit;
            HasHelmet = player.HasHelmet;
            TeamID = player.TeamID;
            AmmoLeft = player.AmmoLeft;
            AdditionaInformations = player.AdditionaInformations;
        }

        public string Name { get; set; }

        public long SteamID { get; set; }

        public Vector Position { get; set; }

        public int EntityID { get; set; }

        public int UserID { get; set; }

        public int HP { get; set; }

        public int Armor { get; set; }

        public Vector LastAlivePosition { get; set; }

        public Vector Velocity { get; set; }

        public float ViewDirectionX { get; set; }

        public float ViewDirectionY { get; set; }

        public float FlashDuration { get; set; }

        public int Money { get; set; }

        public int CurrentEquipmentValue { get; set; }

        public int FreezetimeEndEquipmentValue { get; set; }

        public int RoundStartEquipmentValue { get; set; }

        public bool IsDucking { get; set; }

        public bool Disconnected { get; set; }

        public Equipment ActiveWeapon => ActiveWeaponID == DemoParser.INDEX_MASK ? null : rawWeapons[ActiveWeaponID];

        public IEnumerable<Equipment> Weapons => rawWeapons.Values;

        public bool IsAlive => HP > 0;

        public Team Team { get; set; }

        public bool HasDefuseKit { get; set; }

        public bool HasHelmet { get; set; }

        public AdditionalPlayerInformation AdditionaInformations { get; internal set; }

        /// <summary>
        /// Copy this instance for multi-threading use.
        /// </summary>
        public Player Copy()
        {
            Player me = new Player();
            me.EntityID = -1; //this should bot be copied
            me.Entity = null;

            me.Name = Name;
            me.SteamID = SteamID;
            me.HP = HP;
            me.Armor = Armor;

            me.ViewDirectionX = ViewDirectionX;
            me.ViewDirectionY = ViewDirectionY;
            me.Disconnected = Disconnected;
            me.FlashDuration = FlashDuration;

            me.Team = Team;

            me.ActiveWeaponID = ActiveWeaponID;
            me.rawWeapons = new Dictionary<int, Equipment>(rawWeapons);

            me.HasDefuseKit = HasDefuseKit;
            me.HasHelmet = HasHelmet;

            if (Position != null)
                me.Position = Position.Copy(); //Vector is a class, not a struct - thus we need to make it thread-safe.

            if (LastAlivePosition != null)
                me.LastAlivePosition = LastAlivePosition.Copy();

            if (Velocity != null)
                me.Velocity = Velocity.Copy();

            return me;
        }
    }

    public enum Team
    {
        Spectate = 1,
        Terrorist = 2,
        CounterTerrorist = 3,
        Unknown = 4, // Caused by an error where the round_end event was not triggered for a round
    }
}
