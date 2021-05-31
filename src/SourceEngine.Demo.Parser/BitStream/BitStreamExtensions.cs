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
            return unchecked((uint)bs.ReadProtoUInt64());
        }

        public static int ReadProtoInt32(this IBitStream bs)
        {
            return unchecked((int)bs.ReadProtoUInt64());
        }

        public static long ReadProtoInt64(this IBitStream bs)
        {
            return unchecked((long)bs.ReadProtoUInt64());
        }

        public static int ReadProtoSInt32(this IBitStream bs)
        {
            ulong result = bs.ReadProtoUInt64();
            return (int)(result >> 1) ^ -(int)(result & 1);
        }

        public static long ReadProtoSInt64(this IBitStream bs)
        {
            ulong result = bs.ReadProtoUInt64();
            return (long)(result >> 1) ^ -(long)(result & 1);
        }

        public static string ReadProtoString(this IBitStream reader)
        {
            return Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadProtoInt32()));
        }

        #endregion
    }
}
