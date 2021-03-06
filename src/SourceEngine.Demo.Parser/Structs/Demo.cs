using System;

using SourceEngine.Demo.Parser.BitStream;

namespace SourceEngine.Demo.Parser.Structs
{
    /// <summary>
    /// A Demo header.
    /// </summary>
    public class DemoHeader
    {
        private const int MAX_OSPATH = 260;

        public string Filestamp { get; private set; } // Should be HL2DEMO

        public int Protocol { get; private set; } // Should be DEMO_PROTOCOL (4)

        [Obsolete("This was a typo. Use NetworkProtocol instead")]
        public int NetworkProtocal => NetworkProtocol;

        public int NetworkProtocol { get; private set; } // Should be PROTOCOL_VERSION

        public string ServerName { get; private set; } // Name of server

        public string ClientName { get; private set; } // Name of client who recorded the game

        public string MapName { get; private set; } // Name of map

        public string GameDirectory { get; private set; } // Name of game directory (com_gamedir)

        public float PlaybackTime { get; private set; } // Time of track

        public int PlaybackTicks { get; private set; } // # of ticks in track

        public int PlaybackFrames { get; private set; } // # of frames in track

        public int SignonLength { get; private set; } // length of sigondata in bytes

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
