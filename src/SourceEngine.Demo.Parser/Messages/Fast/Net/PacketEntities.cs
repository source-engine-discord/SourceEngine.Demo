﻿using System;
using System.IO;

using SourceEngine.Demo.Parser.BitStream;
using SourceEngine.Demo.Parser.Packet.Handler;

namespace SourceEngine.Demo.Parser.Messages.Fast.Net
{
    public struct PacketEntities
    {
        public int MaxEntries;
        public int UpdatedEntries;
        private int _IsDelta;

        public bool IsDelta => _IsDelta != 0;

        private int _UpdateBaseline;

        public bool UpdateBaseline => _UpdateBaseline != 0;

        public int Baseline;
        public int DeltaFrom;

        public void Parse(IBitStream bitstream, DemoParser parser)
        {
            while (!bitstream.ChunkFinished)
            {
                var desc = bitstream.ReadProtoInt32();
                var wireType = desc & 7;
                var fieldnum = desc >> 3;

                if (fieldnum == 7 && wireType == 2)
                {
                    // Entity data is special.
                    // We'll simply hope that gaben is nice and sends
                    // entity_data last, just like he should.

                    var len = bitstream.ReadProtoInt32();
                    bitstream.BeginChunk(len * 8);
                    PacketEntitesHandler.Apply(this, bitstream, parser);
                    bitstream.EndChunk();
                    if (!bitstream.ChunkFinished)
                        throw new NotImplementedException("Lord Gaben wasn't nice to us :/");

                    break;
                }

                if (wireType != 0)
                    throw new InvalidDataException();

                var val = bitstream.ReadProtoInt32();

                // Silently drop other cases.
                switch (fieldnum)
                {
                    case 1:
                        MaxEntries = val;
                        break;
                    case 2:
                        UpdatedEntries = val;
                        break;
                    case 3:
                        _IsDelta = val;
                        break;
                    case 4:
                        _UpdateBaseline = val;
                        break;
                    case 5:
                        Baseline = val;
                        break;
                    case 6:
                        DeltaFrom = val;
                        break;
                }
            }
        }
    }
}
