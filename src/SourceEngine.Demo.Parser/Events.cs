using System;
using System.Collections.Generic;
using System.Diagnostics;

using SourceEngine.Demo.Parser.Entities;
using SourceEngine.Demo.Parser.Structs;

namespace SourceEngine.Demo.Parser
{
    public class PlayerPositionsEventArgs : EventArgs
    {
        public double CurrentTime { get; set; }

        public List<PlayerPositionEventArgs> PlayerPositions { get; set; }
    }

    public class PlayerPositionEventArgs : EventArgs
    {
        public Player Player { get; set; }
    }

    public class HeaderParsedEventArgs : EventArgs
    {
        public HeaderParsedEventArgs(DemoHeader header)
        {
            Header = header;
        }

        public DemoHeader Header { get; private set; }
    }

    public class TickDoneEventArgs : EventArgs { }

    public class MatchStartedEventArgs : EventArgs
    {
        public string Mapname { get; set; }

        public bool HasBombsites { get; set; }
    }

    public class RoundAnnounceMatchStartedEventArgs : EventArgs { }

    public class RoundEndedEventArgs : EventArgs
    {
        /// <summary>
        /// The winning team. Spectate for everything that isn't CT or T.
        /// </summary>
        public Team Winner;

        public RoundEndReason Reason { get; set; }

        public string Message { get; set; }

        public double Length { get; set; }
    }

    public class SwitchSidesEventArgs : EventArgs
    {
        public int RoundBeforeSwitch { get; set; }
    }

    public class RoundOfficiallyEndedEventArgs : EventArgs
    {
        /// <summary>
        /// The winning team. Spectate for everything that isn't CT or T.
        /// </summary>
        public Team Winner;

        public RoundEndReason Reason { get; set; }

        public string Message { get; set; }

        public double Length { get; set; }
    }

    public class RoundMVPEventArgs : EventArgs
    {
        public Player Player { get; set; }

        public RoundMVPReason Reason { get; set; }
    }

    public class RoundStartedEventArgs : EventArgs
    {
        public int TimeLimit { get; set; }

        public int FragLimit { get; set; }

        public string Objective { get; set; }
    }

    public class WinPanelMatchEventArgs : EventArgs { }

    public class RoundFinalEventArgs : EventArgs { }

    public class LastRoundHalfEventArgs : EventArgs { }

    public class FreezetimeEndedEventArgs : EventArgs
    {
        /// <summary>
        /// The current time value of the DemoParser
        /// </summary>
        public float TimeEnd { get; set; }
    }

    public class PlayerTeamEventArgs : EventArgs
    {
        public Player Swapped { get; internal set; }

        public Team NewTeam { get; internal set; }

        public Team OldTeam { get; internal set; }

        public bool Silent { get; internal set; }

        public bool IsBot { get; internal set; }
    }

    public class OtherKilledEventArgs : EventArgs { }

    public class ChickenKilledEventArgs : EventArgs { }

    public class PlayerKilledEventArgs : EventArgs
    {
        public int Round { get; set; }

        public double TimeInRound { get; set; }

        public Equipment Weapon { get; internal set; }

        [Obsolete("Use \"Victim\" instead. This will be removed soon™", false)]
        public Player DeathPerson => Victim;

        public Player Victim { get; set; }

        public bool VictimBotTakeover { get; set; }

        public Player Killer { get; set; }

        public bool KillerBotTakeover { get; set; }

        public Player Assister { get; set; }

        public bool AssisterBotTakeover { get; set; }

        public bool Suicide { get; set; }

        public bool TeamKill { get; set; }

        public int PenetratedObjects { get; set; }

        public bool Headshot { get; set; }

        public bool AssistedFlash { get; set; }
    }

    public class BotTakeOverEventArgs : EventArgs
    {
        public Player Taker { get; internal set; }
    }

    public class WeaponFiredEventArgs : EventArgs
    {
        public Equipment Weapon { get; internal set; }

        public Player Shooter { get; internal set; }

        public double TimeInRound { get; set; }
    }

    public class NadeEventArgs : EventArgs
    {
        public NadeEventArgs() { }

        internal NadeEventArgs(EquipmentElement type)
        {
            NadeType = type;
        }

        public Vector Position { get; set; }

        public EquipmentElement NadeType { get; set; }

        public Player ThrownBy { get; set; }
    }

    public class FireEventArgs : NadeEventArgs
    {
        public FireEventArgs() : base(EquipmentElement.Incendiary) { }
    }

    public class SmokeEventArgs : NadeEventArgs
    {
        public SmokeEventArgs() : base(EquipmentElement.Smoke) { }
    }

    public class DecoyEventArgs : NadeEventArgs
    {
        public DecoyEventArgs() : base(EquipmentElement.Decoy) { }
    }

    public class FlashEventArgs : NadeEventArgs
    {
        //

        public FlashEventArgs() : base(EquipmentElement.Flash) { }

        //previous blind implementation
        public Player[] FlashedPlayers { get; set; }
    }

    public class GrenadeEventArgs : NadeEventArgs
    {
        public GrenadeEventArgs() : base(EquipmentElement.HE) { }
    }

    public class BombEventArgs : EventArgs
    {
        public Player Player { get; set; }

        public char? Site { get; set; }

        public double TimeInRound { get; set; }
    }

    public class BombDefuseEventArgs : EventArgs
    {
        public Player Player { get; set; }

        public bool HasKit { get; set; }

        public double TimeInRound { get; set; }
    }

    public class HostageRescuedEventArgs : EventArgs
    {
        public int Round { get; set; }

        public double TimeInRound { get; set; }

        public Player Player { get; set; }

        public char Hostage { get; set; }

        public int HostageIndex { get; set; }

        public int RescueZone { get; set; }
    }

    public class HostagePickedUpEventArgs : EventArgs
    {
        public int Round { get; set; }

        public double TimeInRound { get; set; }

        public Player Player { get; set; }

        public char Hostage { get; set; }

        public int HostageIndex { get; set; }
    }

    public class PlayerHurtEventArgs : EventArgs
    {
        /// <summary>
        /// The round the event has occurred
        /// </summary>
        public int Round { get; set; }

        /// <summary>
        /// The time in the round the event has occurred
        /// </summary>
        public double TimeInRound { get; set; }

        /// <summary>
        /// The hurt player
        /// </summary>
        public Player Player { get; set; }

        /// <summary>
        /// The attacking player
        /// </summary>
        public Player Attacker { get; set; }

        /// <summary>
        /// Remaining health points of the player
        /// </summary>
        public int Health { get; set; }

        /// <summary>
        /// Remaining armor points of the player
        /// </summary>
        public int Armor { get; set; }

        /// <summary>
        /// The Weapon used to attack.
        /// Note: This might be not the same as the raw event
        /// we replace "hpk2000" with "usp-s" if the attacker
        /// is currently holding it - this value is originally
        /// networked "wrong". By using this property you always
        /// get the "right" weapon
        /// </summary>
        /// <value>The weapon.</value>
        public Equipment Weapon { get; set; }

        /// <summary>
        /// The original "weapon"-value from the event.
        /// Might be wrong for USP, CZ and M4A1-S
        /// </summary>
        /// <value>The weapon string.</value>
        public string WeaponString { get; set; }

        /// <summary>
        /// The damage done to the players health
        /// </summary>
        public int HealthDamage { get; set; }

        /// <summary>
        /// The damage done to the players armor
        /// </summary>
        public int ArmorDamage { get; set; }

        /// <summary>
        /// Where the Player was hit.
        /// </summary>
        /// <value>The hitgroup.</value>
        public HitGroup HitGroup { get; set; }

        /// <summary>
        /// Shows if it is possible that the player was killed by a bomb explosion (player_death is not triggered when it is due to
        /// the bomb (unsure if this is always or just sometimes)).
        /// </summary>
        /// <value>The hitgroup.</value>
        public bool PossiblyKilledByBombExplosion { get; set; }
    }

    public class BlindEventArgs : EventArgs
    {
        public Player Player { get; set; }

        public Player Attacker { get; set; }

        public float? FlashDuration { get; set; }
    }

    public class PlayerBindEventArgs : EventArgs
    {
        public Player Player { get; set; }
    }

    public class PlayerDisconnectEventArgs : EventArgs
    {
        public Player Player { get; set; }
    }

    /// <summary>
    /// Occurs when the server use the "say" command
    /// I don't know the purpose of IsChat and IsChatAll because they are everytime false
    /// </summary>
    public class SayTextEventArgs : EventArgs
    {
        /// <summary>
        /// Should be everytime 0 as it's a message from the server
        /// </summary>
        public int EntityIndex { get; set; }

        /// <summary>
        /// Message sent by the server
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Everytime false as the message is public
        /// </summary>
        public bool IsChat { get; set; }

        /// <summary>
        /// Everytime false as the message is public
        /// </summary>
        public bool IsChatAll { get; set; }
    }

    /// <summary>
    /// Occurs when a player use the say command
    /// Not sure about IsChat and IsChatAll, GOTV doesn't record chat team so this 2 bool are every time true
    /// </summary>
    public class SayText2EventArgs : EventArgs
    {
        /// <summary>
        /// The player who sent the message
        /// </summary>
        public Player Sender { get; set; }

        /// <summary>
        /// The message sent
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Not sure about it, maybe it's to indicate say_team or say
        /// </summary>
        public bool IsChat { get; set; }

        /// <summary>
        /// true if the message is for all players ?
        /// </summary>
        public bool IsChatAll { get; set; }

        /// <summary>
        /// The time through the demo that the message was sent (manually added to the event in SayText2.cs)
        /// </summary>
        public float TimeSent { get; set; }
    }

    /// <summary>
    /// Occurs when the server display a player rank
    /// It occurs only with Valve demos, at the end of a Matchmaking.
    /// So for a 5v5 match there will be 10 events triggered
    /// </summary>
    public class RankUpdateEventArgs : EventArgs
    {
        /// <summary>
        /// Player's SteamID64
        /// </summary>
        public long SteamId { get; set; }

        /// <summary>
        /// Player's rank at the beginning of the match
        /// </summary>
        public int RankOld { get; set; }

        /// <summary>
        /// Player's rank the end of the match
        /// </summary>
        public int RankNew { get; set; }

        /// <summary>
        /// Number of win that the player have
        /// </summary>
        public int WinCount { get; set; }

        /// <summary>
        /// Number of rank the player win / lost between the beginning and the end of the match
        /// </summary>
        public float RankChange { get; set; }
    }

    /// <summary>
    /// Data parsed from a <c>CSVCMsg_ServerInfo</c> network message.
    /// </summary>
    public class ServerInfoEventArgs : EventArgs
    {
        /// <summary>
        /// Network protocol version.
        /// </summary>
        /// <remarks>
        /// Derived from the <c>PatchVersion</c> in <c>steam.inf</c>.
        /// </remarks>
        public int Protocol { get; set; }

        /// <summary>
        /// Number of servers spawned since the start.
        /// </summary>
        /// <remarks>
        /// A server is spawned when there is a new game or the level changes.
        /// </remarks>
        public int ServerCount { get; set; }

        /// <summary>
        /// <see langword="true"/> if the server is dedicated.
        /// </summary>
        public bool IsDedicated { get; set; }

        /// <summary>
        /// <see langword="true"/> if Valve hosts the server.
        /// </summary>
        public bool IsOfficialValveServer { get; set; }

        /// <summary>
        /// <see langword="true"/> if the server is an HLTV/SourceTV/GOTV proxy.
        /// </summary>
        public bool IsHltv { get; set; }

        /// <summary>
        /// <see langword="true"/> if the server is a replay proxy.
        /// </summary>
        public bool IsReplay { get; set; }

        /// <summary>
        /// Only <see langword="true"/> if the server is an HLTV/SourceTV/GOTV proxy.
        /// </summary>
        public bool IsRedirectingToProxyRelay { get; set; }

        /// <summary>
        /// Operating system (of the server?)
        /// </summary>
        /// <remarks>
        /// Seems to be the ASCII character 'w' for Windows and 'l' for Linux.
        /// </remarks>
        public int OperatingSystem { get; set; }

        /// <summary>
        /// CRC32 of the loaded map (BSP file).
        /// </summary>
        public uint MapCrc { get; set; }

        /// <summary>
        /// CRC32 of client.dll.
        /// </summary>
        public uint ClientCrc { get; set; }

        /// <summary>
        /// CRC32 of the network string table.
        /// </summary>
        public uint StringTableCrc { get; set; }

        /// <summary>
        /// Maximum number of players the server supports.
        /// </summary>
        public int MaxClients { get; set; }

        /// <summary>
        /// Number of unique server classes.
        /// </summary>
        public int MaxClasses { get; set; }

        /// <summary>
        /// Slot number of the client.
        /// </summary>
        /// <remarks>
        /// An index into the <c>CBaseServer.m_Clients</c> array.
        /// HLTV/SourceTV/GOTV and replay proxies seem to have their own positions.
        /// </remarks>
        public int PlayerSlot { get; set; }

        /// <summary>
        /// Time, in seconds, for one tick to elapse.
        /// </summary>
        public float TickInterval { get; set; }

        /// <summary>
        /// Name of the game's directory.
        /// </summary>
        public string GameDir { get; set; }

        /// <summary>
        /// Name of the loaded map.
        /// </summary>
        public string MapName { get; set; }

        /// <summary>
        /// Name of the in-use map group.
        /// </summary>
        /// <remarks>
        /// The map group is set with the <c>mapgroup</c> option for SRCDS or the <c>mapgroup</c> console command.
        /// Map groups are defined in <c>gamemodes_server.txt</c>.
        /// </remarks>
        public string MapGroupName { get; set; }

        /// <summary>
        /// Name of the sky texture used by the map.
        /// </summary>
        /// <remarks>
        /// This is the same as the value of the <c>sv_skyname</c> cvar.
        /// </remarks>
        public string SkyName { get; set; }

        /// <summary>
        /// Host name of the server as set by the <c>hostname</c> cvar.
        /// </summary>
        /// <remarks>
        /// If <c>host_name_store</c> is set to 0, the name will instead be 'Counter-Strike: Global Offensive'.
        /// </remarks>
        public string HostName { get; set; }

        /// <summary>
        /// Public IP of the server. Always 0 since the game no longer expose the IP here.
        /// </summary>
        public uint PublicIp { get; set; }

        /// <summary>
        /// Published file ID for the community map that is loaded. 0 if a non-UGC map is loaded.
        /// </summary>
        /// <remarks>
        /// UGC means user-generated content.
        /// </remarks>
        public ulong UgcMapId { get; set; }
    }

    public class Equipment
    {
        private const string WEAPON_PREFIX = "weapon_";

        public Equipment()
        {
            Weapon = EquipmentElement.Unknown;
        }

        public Equipment(string originalString)
        {
            OriginalString = originalString;
            Weapon = MapEquipment(originalString);
        }

        public Equipment(string originalString, string skin)
        {
            OriginalString = originalString;
            Weapon = MapEquipment(originalString);
            SkinID = skin;
        }

        public Equipment(Equipment equipment)
        {
            if (equipment == null)
                return;

            EntityID = equipment.EntityID;
            Weapon = equipment.Weapon;
            OriginalString = equipment.OriginalString;
            SkinID = equipment.SkinID;
            AmmoInMagazine = equipment.AmmoInMagazine;
            AmmoType = equipment.AmmoType;
            Owner = new Player(equipment.Owner);
        }

        internal int EntityID { get; set; }

        public EquipmentElement Weapon { get; set; }

        public EquipmentClass Class => (EquipmentClass)((int)Weapon / 100 + 1);

        public string SubclassName
        {
            get
            {
                return Weapon switch
                {
                    EquipmentElement.M249 => "LMG",
                    EquipmentElement.Negev => "LMG",
                    EquipmentElement.Scout => "Sniper",
                    EquipmentElement.AWP => "Sniper",
                    EquipmentElement.Zeus => "Zeus",
                    EquipmentElement.Knife => "Knife",
                    EquipmentElement.Fists => "Fists",
                    EquipmentElement.Melee => "Melee",
                    EquipmentElement.Shield => "Shield",
                    _ => Class switch
                    {
                        EquipmentClass.Heavy => "Shotgun",
                        EquipmentClass.Rifle => "AssaultRifle",
                        _ => Class.ToString(),
                    },
                };
            }
        }

        public string OriginalString { get; set; }

        public string SkinID { get; set; }

        public int AmmoInMagazine { get; set; }

        internal int AmmoType { get; set; }

        public Player Owner { get; set; }

        public int ReserveAmmo => Owner != null && AmmoType != -1 ? Owner.AmmoLeft[AmmoType] : -1;

        public static EquipmentElement MapEquipment(string name)
        {
            name = name.ToLower();

            if (name.StartsWith(WEAPON_PREFIX))
                name = name.Substring(WEAPON_PREFIX.Length);

            if (name.Contains("knife"))
                return EquipmentElement.Knife;

            switch (name)
            {
                case "ak47":
                    return EquipmentElement.AK47;
                case "aug":
                    return EquipmentElement.AUG;
                case "awp":
                    return EquipmentElement.AWP;
                case "bayonet":
                    return EquipmentElement.Knife;
                case "bizon":
                    return EquipmentElement.Bizon;
                case "c4":
                    return EquipmentElement.Bomb;
                case "deagle":
                    return EquipmentElement.Deagle;
                case "decoy":
                case "decoygrenade":
                    return EquipmentElement.Decoy;
                case "elite":
                    return EquipmentElement.DualBarettas;
                case "famas":
                    return EquipmentElement.Famas;
                case "fiveseven":
                    return EquipmentElement.FiveSeven;
                case "flashbang":
                    return EquipmentElement.Flash;
                case "g3sg1":
                    return EquipmentElement.G3SG1;
                case "galil":
                case "galilar":
                    return EquipmentElement.Gallil;
                case "glock":
                    return EquipmentElement.Glock;
                case "hegrenade":
                    return EquipmentElement.HE;
                case "hkp2000":
                    return EquipmentElement.P2000;
                case "incgrenade":
                case "incendiarygrenade":
                    return EquipmentElement.Incendiary;
                case "m249":
                    return EquipmentElement.M249;
                case "m4a1":
                    return EquipmentElement.M4A4;
                case "mac10":
                    return EquipmentElement.Mac10;
                case "mag7":
                    return EquipmentElement.Swag7;
                case "molotov":
                case "molotovgrenade":
                case "molotov_projectile":
                    return EquipmentElement.Molotov;
                case "mp7":
                    return EquipmentElement.MP7;
                case "mp9":
                    return EquipmentElement.MP9;
                case "negev":
                    return EquipmentElement.Negev;
                case "nova":
                    return EquipmentElement.Nova;
                case "p250":
                    return EquipmentElement.P250;
                case "p90":
                    return EquipmentElement.P90;
                case "sawedoff":
                    return EquipmentElement.SawedOff;
                case "scar20":
                    return EquipmentElement.Scar20;
                case "sg556":
                    return EquipmentElement.SG556;
                case "smokegrenade":
                    return EquipmentElement.Smoke;
                case "ssg08":
                    return EquipmentElement.Scout;
                case "taser":
                    return EquipmentElement.Zeus;
                case "tec9":
                    return EquipmentElement.Tec9;
                case "ump45":
                    return EquipmentElement.UMP;
                case "xm1014":
                    return EquipmentElement.XM1014;
                case "m4a1_silencer":
                case "m4a1_silencer_off":
                    return EquipmentElement.M4A1;
                case "cz75a":
                    return EquipmentElement.CZ;
                case "usp":
                case "usp_silencer":
                case "usp_silencer_off":
                    return EquipmentElement.USP;
                case "world":
                    return EquipmentElement.World;
                case "inferno":
                    return EquipmentElement.Incendiary;
                case "revolver":
                    return EquipmentElement.Revolver;
                case "mp5sd":
                    return EquipmentElement.MP5SD;
                case "breachcharge":
                    return EquipmentElement.BreachCharge;
                case "healthshot":
                    return EquipmentElement.HealthShot;
                case "fists":
                    return EquipmentElement.Fists;
                case "melee":
                case "axe":
                case "hammer":
                case "spanner":
                    return EquipmentElement.Melee;
                case "tablet":
                    return EquipmentElement.Tablet;
                case "bumpmine":
                    return EquipmentElement.BumpMine;
                case "shield":
                    return EquipmentElement.Shield;
                case "zonerepulsor":
                    return EquipmentElement.ZoneRepulsor;
                case "snowball":
                    return EquipmentElement.Snowball;
                case "diversion":
                case "diversiongrenade":
                    return EquipmentElement.Diversion;
                case "sensor":
                case "sensorgrenade":
                    return EquipmentElement.Sensor;
                case "trigger_hurt":
                    return EquipmentElement.TriggerHurt;
                case "prop_exploding_barrel":
                    return EquipmentElement.ExplodingBarrel;

                // These crash the game when given via give weapon_[mp5navy|...], and cannot be purchased in-game.
                // yet the server-classes are networked, so I need to resolve them.
                case "scar17":
                case "sg550":
                case "mp5navy":
                case "p228":
                case "scout":
                case "sg552":
                case "tmp":
                    return EquipmentElement.Unknown;
                default:
                    Trace.WriteLine($"Unknown weapon {name}", "Equipment.MapEquipment()");
                    return EquipmentElement.Unknown;
            }
        }
    }

    public enum EquipmentElement
    {
        Unknown = -100,

        // Pistols
        P2000 = 1,
        Glock = 2,
        P250 = 3,
        Deagle = 4,
        FiveSeven = 5,
        DualBarettas = 6,
        Tec9 = 7,
        CZ = 8,
        USP = 9,
        Revolver = 10,

        // SMGs
        MP7 = 101,
        MP9 = 102,
        Bizon = 103,
        Mac10 = 104,
        UMP = 105,
        P90 = 106,
        MP5SD = 107,

        // Heavy
        SawedOff = 201,
        Nova = 202,
        Swag7 = 203,
        XM1014 = 204,
        M249 = 205,
        Negev = 206,

        // Rifle
        Gallil = 301,
        Famas = 302,
        AK47 = 303,
        M4A4 = 304,
        M4A1 = 305,
        Scout = 306,
        SG556 = 307,
        AUG = 308,
        AWP = 309,
        Scar20 = 310,
        G3SG1 = 311,

        // Equipment
        Zeus = 401,
        Kevlar = 402,
        Helmet = 403,
        Bomb = 404,
        Knife = 405,
        DefuseKit = 406,
        World = 407,
        HealthShot = 408,
        Fists = 409,
        Melee = 410, // axe, hammer & spanner (throwable handheld weapons)
        Tablet = 411,
        BumpMine = 412,
        Shield = 413,
        ZoneRepulsor = 414, // from co-op mission

        // Grenades
        Decoy = 501,
        Molotov = 502,
        Incendiary = 503,
        Flash = 504,
        Smoke = 505,
        HE = 506,
        BreachCharge = 507,
        Snowball = 508,
        Diversion = 509,
        Sensor = 510,

        // Brush Entities
        TriggerHurt = 601,

        // Point Entities
        ExplodingBarrel = 701,
    }

    public enum EquipmentClass
    {
        Unknown = 0,
        Pistol = 1,
        SMG = 2,
        Heavy = 3,
        Rifle = 4,
        Equipment = 5,
        Grenade = 6,
    }
}
