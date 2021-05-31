using System;
using System.Diagnostics;
using System.IO;

namespace SourceEngine.Demo.Parser.BitStream
{
    public static class BitStreamUtil
    {
        /// <summary>
        /// Creates an instance of the preferred <see cref="IBitStream" /> implementation for streams.
        /// </summary>
        public static IBitStream Create(Stream stream)
        {
            var bs = new UnsafeBitStream();
            bs.Initialize(stream);
            return bs;
        }

        /// <summary>
        /// Creates an instance of the preferred <see cref="IBitStream" /> implementation for byte arrays.
        /// </summary>
        public static IBitStream Create(byte[] data)
        {
            var bs = new UnsafeBitStream();
            bs.Initialize(new MemoryStream(data));

            return bs;
        }

        public static int ReadProtoInt32Stub(IBitStream reader)
        {
            byte b = 0x80;
            int result = 0;

            for (int count = 0; (b & 0x80) != 0; count++)
            {
                b = reader.ReadByte();

                if (count < 4 || count == 4 && ((b & 0xF8) == 0 || (b & 0xF8) == 0xF8))
                {
                    result |= (b & ~0x80) << (7 * count);
                }
                else
                {
                    if (count >= 10)
                        throw new OverflowException("Nope nope nope nope! 10 bytes max!");

                    if (count == 9 ? b != 1 : (b & 0x7F) != 0x7F)
                        throw new NotSupportedException("more than 32 bits are not supported");
                }
            }

            return result;
        }

        [Conditional("INTEGRITY_CHECK")]
        public static void AssertMaxBits(int max, int actual)
        {
            Debug.Assert(
                actual <= max,
                "trying to read too many bits",
                "Attempted to read {0} bits (max={1})",
                actual,
                max
            );
        }
    }
}
