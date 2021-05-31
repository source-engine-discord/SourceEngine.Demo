﻿using SourceEngine.Demo.Parser.BitStream;

namespace SourceEngine.Demo.Parser.Messages.Fast.Net
{
    /// <summary>
    /// FastNetMessage adaptation of CCSUsrMsg_ServerRankUpdate.RankUpdate protobuf message
    /// </summary>
    public class RankUpdate
    {
        private const long VALVE_MAGIC_NUMBER = 76561197960265728;

        public int AccountId;
        public int NumWins;
        public float RankChange;
        public int RankNew;
        public int RankOld;

        public void Parse(IBitStream bitstream, DemoParser parser)
        {
            while (!bitstream.ChunkFinished)
            {
                var desc = bitstream.ReadProtoInt32();
                var wireType = desc & 7;
                var fieldnum = desc >> 3;

                if (wireType == 0 && fieldnum == 1)
                    AccountId = bitstream.ReadProtoInt32();
                else if (wireType == 0 && fieldnum == 2)
                    RankOld = bitstream.ReadProtoInt32();
                else if (wireType == 0 && fieldnum == 3)
                    RankNew = bitstream.ReadProtoInt32();
                else if (wireType == 0 && fieldnum == 4)
                    NumWins = bitstream.ReadProtoInt32();
                else if (wireType == 5 && fieldnum == 5)
                    RankChange = bitstream.ReadFloat();
            }

            Raise(parser);
        }

        private void Raise(DemoParser parser)
        {
            var e = new RankUpdateEventArgs
            {
                SteamId = AccountId + VALVE_MAGIC_NUMBER,
                RankOld = RankOld,
                RankNew = RankNew,
                WinCount = NumWins,
                RankChange = RankChange,
            };

            parser.RaiseRankUpdate(e);
        }
    }
}
