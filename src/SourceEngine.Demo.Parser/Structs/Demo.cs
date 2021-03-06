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

    [Flags]
    internal enum SplitFlags
    {
        Normal,
        UseResampledOrigin,
        UseResampledAngles,

        /// <summary>
        /// Don't interpolate between this and the last value.
        /// </summary>
        NoInterpolation,
    }

    /// <summary>
    /// Contains the origin and view angles for a specific split screen player.
    /// </summary>
    internal class Split
    {
        public SplitFlags Flags { get; private set; }

        /// <summary>
        /// Player origin relative to its parent.
        /// </summary>
        public Vector ViewOrigin { get; private set; }

        /// <summary>
        /// Player view angles as seen by the server.
        /// </summary>
        public QAngle ViewAngles { get; private set; }

        /// <summary>
        /// Player view angles as seen by the client.
        /// </summary>
        /// <remarks>
        /// Used in client-side prediction.
        /// </remarks>
        public QAngle LocalViewAngles { get; private set; }

        /// <summary>
        /// Resampled player origin relative to its parent.
        /// </summary>
        /// <remarks>
        /// Set if the demo is smoothed. Otherwise set to 0.
        /// </remarks>
        public Vector ResampledViewOrigin { get; private set; }

        /// <summary>
        /// Resampled player view angles as seen by the server.
        /// </summary>
        /// <remarks>
        /// Set if the demo is smoothed. Otherwise set to 0.
        /// </remarks>
        public QAngle ResampledViewAngles { get; private set; }

        /// <summary>
        /// Resampled player view angles as seen by the client.
        /// </summary>
        /// <remarks>
        /// Used in client-side prediction.
        /// Set if the demo is smoothed. Otherwise set to 0.
        /// </remarks>
        public QAngle ResampledLocalViewAngles { get; private set; }

        /// <summary>
        /// Player origin relative to its parent.
        /// </summary>
        /// <remarks>
        /// Use the resampled value if the demo is smoothed.
        /// </remarks>
        public Vector ActualViewOrigin =>
            (Flags & SplitFlags.UseResampledOrigin) != 0 ? ResampledViewOrigin : ViewOrigin;

        /// <summary>
        /// Player view angles as seen by the server.
        /// </summary>
        /// <remarks>
        /// Use the resampled value if the demo is smoothed.
        /// </remarks>
        public QAngle ActualViewAngles =>
            (Flags & SplitFlags.UseResampledAngles) != 0 ? ResampledViewAngles : ViewAngles;

        /// <summary>
        /// Player view angles as seen by the client.
        /// </summary>
        /// <remarks>
        /// Used in client-side prediction.
        /// Use the resampled value if the demo is smoothed.
        /// </remarks>
        public QAngle ActualLocalViewAngles =>
            (Flags & SplitFlags.UseResampledAngles) != 0 ? ResampledLocalViewAngles : LocalViewAngles;

        /// <summary>
        /// Parse a raw data stream into a new <see cref="Split"/>.
        /// </summary>
        /// <param name="reader">The data stream to parse.</param>
        /// <returns>A <see cref="Split"/> containing values from the parsed stream.</returns>
        public static Split Parse(IBitStream reader)
        {
            return new()
            {
                Flags = (SplitFlags)reader.ReadSignedInt(32),
                ViewOrigin = Vector.Parse(reader),
                ViewAngles = QAngle.Parse(reader),
                LocalViewAngles = QAngle.Parse(reader),
                ResampledViewOrigin = Vector.Parse(reader),
                ResampledViewAngles = QAngle.Parse(reader),
                ResampledLocalViewAngles = QAngle.Parse(reader),
            };
        }
    }

    internal class CommandInfo
    {
        public Split[] Splits { get; private set; }

        public static CommandInfo Parse(IBitStream reader)
        {
            return new() { Splits = new[] { Split.Parse(reader), Split.Parse(reader) } };
        }
    }

    /// <summary>
    /// The demo-commands as given by Valve.
    /// </summary>
    internal enum DemoCommand
    {
        /// <summary>
        /// It's a startup message; process as fast as possible.
        /// </summary>
        Signon = 1,

        /// <summary>
        /// It's a normal network packet that we stored off.
        /// </summary>
        Packet,

        /// <summary>
        /// Sync client clock to demo tick.
        /// </summary>
        SyncTick,

        /// <summary>
        /// Console command.
        /// </summary>
        ConsoleCommand,

        /// <summary>
        /// User input command.
        /// </summary>
        UserCommand,

        /// <summary>
        /// Network data tables.
        /// </summary>
        DataTables,

        /// <summary>
        /// End of time.
        /// </summary>
        Stop,

        /// <summary>
        /// A blob of binary data understood by a callback function.
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
