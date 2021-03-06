using System;

using SourceEngine.Demo.Parser.BitStream;

namespace SourceEngine.Demo.Parser.Structs
{
    /// <summary>
    /// Header of a demo file.
    /// </summary>
    public class DemoHeader
    {
        private const int MAX_OSPATH = 260;

        /// <summary>
        /// File type.
        /// </summary>
        /// <remarks>
        /// Should always be <c>DEMO_HEADER_ID</c> (<c>"HL2DEMO"</c>).
        /// </remarks>
        public string Filestamp { get; private set; }

        /// <summary>
        /// Demo protocol version number.
        /// </summary>
        /// <remarks>
        /// Should always be <c>DEMO_PROTOCOL</c> (<c>4</c>).
        /// </remarks>
        public int Protocol { get; private set; }

        /// <summary>
        /// Network protocol version.
        /// </summary>
        /// <remarks>
        /// Derived from the <c>PatchVersion</c> in <c>steam.inf</c>.
        /// </remarks>
        public int NetworkProtocol { get; private set; }

        /// <summary>
        /// Name of the server.
        /// </summary>
        public string ServerName { get; private set; }

        /// <summary>
        /// Name of the client that recorded the game.
        /// </summary>
        public string ClientName { get; private set; }

        /// <summary>
        /// Name of the map.
        /// </summary>
        public string MapName { get; private set; }

        /// <summary>
        /// Path to the game directory.
        /// </summary>
        public string GameDirectory { get; private set; }

        /// <summary>
        /// Length of the demo in seconds.
        /// </summary>
        public float PlaybackTime { get; private set; }

        /// <summary>
        /// Number of ticks in the demo.
        /// </summary>
        public int PlaybackTicks { get; private set; }

        /// <summary>
        /// Number of frames in the demo.
        /// </summary>
        public int PlaybackFrames { get; private set; }

        /// <summary>
        /// Length of the sign-on data in bytes.
        /// </summary>
        public int SignonLength { get; private set; }

        /// <summary>
        /// Parse a raw data stream into a new <see cref="DemoHeader"/>.
        /// </summary>
        /// <param name="reader">The data stream to parse.</param>
        /// <returns>A <see cref="DemoHeader"/> containing values from the parsed stream.</returns>
        public static DemoHeader ParseFrom(IBitStream reader)
        {
            return new()
            {
                Filestamp = reader.ReadCString(8),
                Protocol = reader.ReadSignedInt(32),
                NetworkProtocol = reader.ReadSignedInt(32),
                ServerName = reader.ReadCString(MAX_OSPATH),
                ClientName = reader.ReadCString(MAX_OSPATH),
                MapName = reader.ReadCString(MAX_OSPATH),
                GameDirectory = reader.ReadCString(MAX_OSPATH),
                PlaybackTime = reader.ReadFloat(),
                PlaybackTicks = reader.ReadSignedInt(32),
                PlaybackFrames = reader.ReadSignedInt(32),
                SignonLength = reader.ReadSignedInt(32),
            };
        }
    }

    /// <summary>
    /// A split.
    /// </summary>
    internal class Split
    {
        private const int FDEMO_NORMAL = 0, FDEMO_USE_ORIGIN2 = 1, FDEMO_USE_ANGLES2 = 2, FDEMO_NOINTERP = 4;

        public int Flags { get; private set; }

        public Vector viewOrigin { get; private set; }

        public QAngle viewAngles { get; private set; }

        public QAngle localViewAngles { get; private set; }

        public Vector viewOrigin2 { get; private set; }

        public QAngle viewAngles2 { get; private set; }

        public QAngle localViewAngles2 { get; private set; }

        public Vector ViewOrigin => (Flags & FDEMO_USE_ORIGIN2) != 0 ? viewOrigin2 : viewOrigin;

        public QAngle ViewAngles => (Flags & FDEMO_USE_ANGLES2) != 0 ? viewAngles2 : viewAngles;

        public QAngle LocalViewAngles => (Flags & FDEMO_USE_ANGLES2) != 0 ? localViewAngles2 : localViewAngles;

        public static Split Parse(IBitStream reader)
        {
            return new()
            {
                Flags = reader.ReadSignedInt(32),
                viewOrigin = Vector.Parse(reader),
                viewAngles = QAngle.Parse(reader),
                localViewAngles = QAngle.Parse(reader),
                viewOrigin2 = Vector.Parse(reader),
                viewAngles2 = QAngle.Parse(reader),
                localViewAngles2 = QAngle.Parse(reader),
            };
        }
    }

    internal class CommandInfo
    {
        public Split[] u { get; private set; }

        public static CommandInfo Parse(IBitStream reader)
        {
            return new() { u = new[] { Split.Parse(reader), Split.Parse(reader) } };
        }
    }

    /// <summary>
    /// The demo-commands as given by Valve.
    /// </summary>
    internal enum DemoCommand
    {
        /// <summary>
        /// it's a startup message, process as fast as possible
        /// </summary>
        Signon = 1,

        /// <summary>
        /// it's a normal network packet that we stored off
        /// </summary>
        Packet,

        /// <summary>
        /// sync client clock to demo tick
        /// </summary>
        Synctick,

        /// <summary>
        /// Console Command
        /// </summary>
        ConsoleCommand,

        /// <summary>
        /// user input command
        /// </summary>
        UserCommand,

        /// <summary>
        /// network data tables
        /// </summary>
        DataTables,

        /// <summary>
        /// end of time.
        /// </summary>
        Stop,

        /// <summary>
        /// a blob of binary data understood by a callback function
        /// </summary>
        CustomData,

        StringTables,

        /// <summary>
        /// Last Command
        /// </summary>
        LastCommand = StringTables,

        /// <summary>
        /// First Command
        /// </summary>
        FirstCommand = Signon,
    }
}
