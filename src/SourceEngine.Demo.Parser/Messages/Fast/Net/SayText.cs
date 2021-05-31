using System.IO;

using SourceEngine.Demo.Parser.BitStream;

namespace SourceEngine.Demo.Parser.Messages.Fast.Net
{
    /// <summary>
    /// FastNetMessage adaptation of CCSUsrMsg_SayText protobuf message
    /// </summary>
    public struct SayText
    {
        public int EntityIndex;
        public string Text;
        private int _chat;

        public bool Chat => _chat != 0;

        private int _textAllChat;

        public bool TextAllChat => _textAllChat != 0;

        public void Parse(IBitStream bitstream, DemoParser parser)
        {
            while (!bitstream.ChunkFinished)
            {
                var desc = bitstream.ReadProtoInt32();
                var wireType = desc & 7;
                var fieldnum = desc >> 3;

                if (wireType == 0 && fieldnum == 1)
                    EntityIndex = bitstream.ReadProtoInt32();
                else if (wireType == 2 && fieldnum == 2)
                    Text = bitstream.ReadProtoString();
                else if (wireType == 0 && fieldnum == 3)
                    _chat = bitstream.ReadProtoInt32();
                else if (wireType == 0 && fieldnum == 4)
                    _textAllChat = bitstream.ReadProtoInt32();
                else
                    throw new InvalidDataException();
            }

            Raise(parser);
        }

        private void Raise(DemoParser parser)
        {
            var e = new SayTextEventArgs
            {
                EntityIndex = EntityIndex,
                Text = Text,
                IsChat = Chat,
                IsChatAll = TextAllChat,
            };

            parser.RaiseSayText(e);
        }
    }
}
