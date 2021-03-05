using System.Collections.Generic;

using SourceEngine.Demo.Parser.Packet;

namespace SourceEngine.Demo.Parser
{
    public class Player
    {
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

        internal Entity Entity;

        public bool Disconnected { get; set; }

        internal int ActiveWeaponID;

        public Equipment ActiveWeapon
        {
            get
            {
                if (ActiveWeaponID == DemoParser.INDEX_MASK) return null;

                return rawWeapons[ActiveWeaponID];
            }
        }

        internal Dictionary<int, Equipment> rawWeapons = new Dictionary<int, Equipment>();

        public IEnumerable<Equipment> Weapons
        {
            get { return rawWeapons.Values; }
        }

        public bool IsAlive
        {
            get { return HP > 0; }
        }

        public Team Team { get; set; }

        public bool HasDefuseKit { get; set; }

        public bool HasHelmet { get; set; }

        internal int TeamID;

        internal int[] AmmoLeft = new int[32];

        public AdditionalPlayerInformation AdditionaInformations { get; internal set; }

        public Player()
        {
            Velocity = new Vector();
            LastAlivePosition = new Vector();
        }

        public Player(Player player)
        {
            if (player != null)
            {
                this.Name = player.Name;
                this.SteamID = player.SteamID;
                this.Position = player.Position;
                this.EntityID = player.EntityID;
                this.UserID = player.UserID;
                this.HP = player.HP;
                this.Armor = player.Armor;
                this.LastAlivePosition = player.LastAlivePosition;
                this.Velocity = player.Velocity;
                this.ViewDirectionX = player.ViewDirectionX;
                this.ViewDirectionY = player.ViewDirectionY;
                this.FlashDuration = player.FlashDuration;
                this.Money = player.Money;
                this.CurrentEquipmentValue = player.CurrentEquipmentValue;
                this.FreezetimeEndEquipmentValue = player.FreezetimeEndEquipmentValue;
                this.RoundStartEquipmentValue = player.RoundStartEquipmentValue;
                this.IsDucking = player.IsDucking;
                this.Entity = player.Entity;
                this.Disconnected = player.Disconnected;
                this.ActiveWeaponID = player.ActiveWeaponID;
                this.rawWeapons = player.rawWeapons;
                this.Team = player.Team;
                this.HasDefuseKit = player.HasDefuseKit;
                this.HasHelmet = player.HasHelmet;
                this.TeamID = player.TeamID;
                this.AmmoLeft = player.AmmoLeft;
                this.AdditionaInformations = player.AdditionaInformations;
            }
        }

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
