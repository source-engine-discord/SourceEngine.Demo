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
