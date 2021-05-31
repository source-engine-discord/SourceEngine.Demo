using System.IO;

using SourceEngine.Demo.Parser.BitStream;

namespace SourceEngine.Demo.Parser.Messages.Fast.Net
{
    /// <summary>
    /// FastNetMessage adaptation of CCSUsrMsg_ServerRankUpdate protobuf message
    /// We don't raise this event but instead each RankUpdate events that it contains
    /// </summary>
    public struct ServerRankUpdate
    {
        public static void Parse(IBitStream bitstream, DemoParser parser)
        {
            while (!bitstream.ChunkFinished)
            {
                var desc = bitstream.ReadProtoInt32();
                var wireType = desc & 7;
                var fieldnum = desc >> 3;

                if (wireType == 2 && fieldnum == 1)
                {
                    bitstream.BeginChunk(bitstream.ReadProtoInt32() * 8);
                    new RankUpdate().Parse(bitstream, parser);
                    bitstream.EndChunk();
                }
                else
                {
                    throw new InvalidDataException();
                }
            }
        }
    }
}
