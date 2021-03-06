using System.IO;

namespace SourceEngine.Demo.Parser.Structs
{
    /// <summary>
    /// A playerinfo, based on playerinfo_t by Volvo.
    /// </summary>
    public class PlayerInfo
    {
        internal PlayerInfo() { }

        internal PlayerInfo(BinaryReader reader)
        {
            Version = reader.ReadInt64SwapEndian();
            XUID = reader.ReadInt64SwapEndian();
            Name = reader.ReadCString(128);
            UserID = reader.ReadInt32SwapEndian();
            GUID = reader.ReadCString(33);
            FriendsID = reader.ReadInt32SwapEndian();
            FriendsName = reader.ReadCString(128);

            IsFakePlayer = reader.ReadBoolean();
            IsHLTV = reader.ReadBoolean();

            CustomFiles0 = reader.ReadInt32();
            CustomFiles1 = reader.ReadInt32();
            CustomFiles2 = reader.ReadInt32();
            CustomFiles3 = reader.ReadInt32();

            FilesDownloaded = reader.ReadByte();
        }

        /// version for future compatibility
        public long Version { get; set; }

        // network xuid
        public long XUID { get; set; }

        // scoreboard information
        public string Name { get; set; } //MAX_PLAYER_NAME_LENGTH=128

        // local server user ID, unique while server is running
        public int UserID { get; set; }

        // global unique player identifier
        public string GUID { get; set; } //33bytes

        // friends identification number
        public int FriendsID { get; set; }

        // friends name
        public string FriendsName { get; set; } //128

        // true, if player is a bot controlled by game.dll
        public bool IsFakePlayer { get; set; }

        // true if player is the HLTV proxy
        public bool IsHLTV { get; set; }

        // custom files CRC for this player
        public int CustomFiles0 { get; set; }

        public int CustomFiles1 { get; set; }

        public int CustomFiles2 { get; set; }

        public int CustomFiles3 { get; set; }

        private byte FilesDownloaded { get; set; }

        public static int SizeOf => 8 + 8 + 128 + 4 + 3 + 4 + 1 + 1 + 4 * 8 + 1;

        public static PlayerInfo ParseFrom(BinaryReader reader)
        {
            return new(reader);
        }
    }
}
