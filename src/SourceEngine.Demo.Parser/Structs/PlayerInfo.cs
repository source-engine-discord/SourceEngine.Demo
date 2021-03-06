using System.IO;

namespace SourceEngine.Demo.Parser.Structs
{
    /// <summary>
    /// Engine-related information on a player.
    /// </summary>
    /// <remarks>
    /// Does not contain game-related information.
    /// Based on <c>player_info_t</c> by Valve.
    /// </remarks>
    public class PlayerInfo
    {
        /// <summary>
        /// Initialises a new instance of the <see cref="PlayerInfo" /> class that is empty.
        /// </summary>
        internal PlayerInfo() { }

        /// <summary>
        /// Initialises a new instance of the <see cref="PlayerInfo" /> class with values read from binary data.
        /// </summary>
        /// <param name="reader">The binary data from which to read values for all properties.</param>
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

        /// <summary>
        /// Version number for future compatibility.
        /// </summary>
        public long Version { get; set; }

        /// <summary>
        /// 64-bit representation of the player's Steam ID.
        /// </summary>
        /// <remarks>
        /// <c>0</c> if <see cref="IsFakePlayer" /> is <c>true</c>.
        /// </remarks>
        public long XUID { get; set; }

        /// <summary>
        /// Game-chosen name as it appears on the scoreboard.
        /// </summary>
        /// <remarks>
        /// Always equal to <see cref="FriendsName" />.
        /// </remarks>
        public string Name { get; set; } // MAX_PLAYER_NAME_LENGTH = 128

        /// <summary>
        /// Local server-specific user ID.
        /// </summary>
        /// <remarks>
        /// Unique while the server is running.
        /// </remarks>
        public int UserID { get; set; }

        /// <summary>
        /// String representation of the player's Steam ID in the format 'STEAM_X:Y:Z'.
        /// </summary>
        /// <remarks>
        /// May also be <c>"STEAM_ID_LAN"</c> or <c>"STEAM_ID_PENDING"</c>.
        /// Set to <c>"BOT"</c> if <see cref="IsFakePlayer" /> is <c>true</c>.
        /// </remarks>
        public string GUID { get; set; } // 33 bytes

        /// <summary>
        /// Account number.
        /// </summary>
        /// <remarks>
        /// This is what's in the last part of the Steam ID i.e. the 'Z' in 'STEAM_X:Y:Z'.
        /// </remarks>
        public int FriendsID { get; set; }

        /// <summary>
        /// Equivalent to <see cref="Name" />.
        /// </summary>
        /// <remarks>
        /// It's unclear but it may be the custom nickname given by the client to their friend.
        /// </remarks>
        public string FriendsName { get; set; } // 128

        /// <summary>
        /// <c>true</c> if the player is a bot.
        /// </summary>
        public bool IsFakePlayer { get; set; }

        /// <summary>
        /// <c>true</c> if the player is the HTLV proxy.
        /// </summary>
        public bool IsHLTV { get; set; }

        /// <summary>
        /// CRC32 of the player's custom logo (spray) file.
        /// </summary>
        public int CustomFiles0 { get; set; }

        /// <summary>
        /// CRC32 of the player's custom sound (jingle) file.
        /// </summary>
        public int CustomFiles1 { get; set; }

        /// <summary>
        /// CRC32 of the player's custom model file.
        /// </summary>
        /// <remarks>
        /// Supposedly unused.
        /// </remarks>
        public int CustomFiles2 { get; set; }

        /// <summary>
        /// CRC32 of the player's custom text file. Supposedly unused.
        /// </summary>
        /// <remarks>
        /// Supposedly unused.
        /// </remarks>
        public int CustomFiles3 { get; set; }

        /// <summary>
        /// The count of files that were downloaded from this player.
        /// </summary>
        /// <remarks>
        /// Increases each time the server downloads a new file.
        /// </remarks>
        public byte FilesDownloaded { get; set; }

        /// <summary>
        /// The total size, in bytes, of the contained data.
        /// </summary>
        public static int SizeOf => 8 + 8 + 128 + 4 + 3 + 4 + 1 + 1 + 4 * 8 + 1;

        /// <summary>
        /// Parse binary data into a new <see cref="PlayerInfo" />.
        /// </summary>
        /// <param name="reader">The binary data to parse.</param>
        /// <returns>A <see cref="PlayerInfo" /> containing values from the parsed binary data.</returns>
        public static PlayerInfo ParseFrom(BinaryReader reader)
        {
            return new(reader);
        }
    }
}
