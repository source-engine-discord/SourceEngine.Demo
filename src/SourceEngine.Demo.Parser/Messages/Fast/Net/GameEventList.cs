using System.Collections.Generic;
using System.IO;

using SourceEngine.Demo.Parser.BitStream;
using SourceEngine.Demo.Parser.Packet.Handler;

namespace SourceEngine.Demo.Parser.Messages.Fast.Net
{
    public struct GameEventList
    {
        public struct Key
        {
            public int Type;
            public string Name;

            public void Parse(IBitStream bitstream)
            {
                while (!bitstream.ChunkFinished)
                {
                    var desc = bitstream.ReadProtoInt32();
                    var wireType = desc & 7;
                    var fieldnum = desc >> 3;

                    if (wireType == 0 && fieldnum == 1)
                        Type = bitstream.ReadProtoInt32();
                    else if (wireType == 2 && fieldnum == 2)
                        Name = bitstream.ReadProtoString();
                    else
                        throw new InvalidDataException();
                }
            }
        }

        public struct Descriptor
        {
            public int EventId;
            public string Name;
            public Key[] Keys;

            public void Parse(IBitStream bitstream)
            {
                var keys = new List<Key>();

                while (!bitstream.ChunkFinished)
                {
                    var desc = bitstream.ReadProtoInt32();
                    var wireType = desc & 7;
                    var fieldnum = desc >> 3;

                    if (wireType == 0 && fieldnum == 1)
                    {
                        EventId = bitstream.ReadProtoInt32();
                    }
                    else if (wireType == 2 && fieldnum == 2)
                    {
                        Name = bitstream.ReadProtoString();
                    }
                    else if (wireType == 2 && fieldnum == 3)
                    {
                        var length = bitstream.ReadProtoInt32();
                        bitstream.BeginChunk(length * 8);
                        var key = new Key();
                        key.Parse(bitstream);
                        keys.Add(key);
                        bitstream.EndChunk();
                    }
                    else
                    {
                        throw new InvalidDataException();
                    }
                }

                Keys = keys.ToArray();
            }
        }

        public static void Parse(IBitStream bitstream, DemoParser parser)
        {
            GameEventHandler.HandleGameEventList(ReadDescriptors(bitstream), parser);
        }

        private static IEnumerable<Descriptor> ReadDescriptors(IBitStream bitstream)
        {
            while (!bitstream.ChunkFinished)
            {
                var desc = bitstream.ReadProtoInt32();
                var wireType = desc & 7;
                var fieldnum = desc >> 3;
                if (wireType != 2 || fieldnum != 1)
                    throw new InvalidDataException();

                var length = bitstream.ReadProtoInt32();
                bitstream.BeginChunk(length * 8);
                var descriptor = new Descriptor();
                descriptor.Parse(bitstream);
                yield return descriptor;

                bitstream.EndChunk();
            }
        }
    }
}
