using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SourceEngine.Demo.Parser.BitStream
{
    public static class BitStreamExtensions
    {
        public static uint ReadUBitInt(this IBitStream bs)
        {
            uint ret = bs.ReadInt(6);

            switch (ret & (16 | 32))
            {
                case 16:
                    ret = (ret & 15) | (bs.ReadInt(4) << 4);
                    break;
                case 32:
                    ret = (ret & 15) | (bs.ReadInt(8) << 4);
                    break;
                case 48:
                    ret = (ret & 15) | (bs.ReadInt(32 - 4) << 4);
                    break;
            }

            return ret;
        }

        public static string ReadString(this IBitStream bs)
        {
            return bs.ReadString(int.MaxValue);
        }

        public static string ReadString(this IBitStream bs, int limit)
        {
            var result = new List<byte>(512);

            for (int pos = 0; pos < limit; pos++)
            {
                var b = bs.ReadByte();
                if (b == 0 || b == 10)
                    break;

                result.Add(b);
            }

            return Encoding.ASCII.GetString(result.ToArray());
        }

        public static string ReadDataTableString(this IBitStream bs)
        {
            using var memstream = new MemoryStream();

            // not particularly efficient, but probably fine
            for (byte b = bs.ReadByte(); b != 0; b = bs.ReadByte())
                memstream.WriteByte(b);

            return Encoding.Default.GetString(memstream.GetBuffer(), 0, checked((int)memstream.Length));
        }

        public static string ReadCString(this IBitStream reader, int length)
        {
            return Encoding.Default.GetString(reader.ReadBytes(length)).Split(new[] { '\0' }, 2)[0];
        }

        #region Protobuf

        public static uint ReadProtoUInt32(this IBitStream bs)
        {
            uint tmpByte = 0x80;
            uint result = 0;

            for (int count = 0; (tmpByte & 0x80) != 0; count++)
            {
                if (count > 5)
                    throw new InvalidDataException("VarInt32 out of range");

                tmpByte = bs.ReadByte();
                result |= (tmpByte & 0x7F) << (7 * count);
            }

            return result;
        }

        public static uint ReadProtoSInt32(this IBitStream bs)
        {
            uint result = bs.ReadProtoUInt32();
            return (uint)((result >> 1) ^ -(result & 1));
        }

        public static string ReadProtoString(this IBitStream reader)
        {
            return Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadProtoInt32()));
        }

        #endregion
    }
}
