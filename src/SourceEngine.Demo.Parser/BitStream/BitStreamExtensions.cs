using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SourceEngine.Demo.Parser.BitStream
{
    /// <summary>
    /// Extension methods for reading various data types from an
    /// <see cref="IBitStream"/>, including many Protobuf types.
    /// </summary>
    public static class BitStreamExtensions
    {
        /// <summary>
        /// Reads an unsigned integer with a variable bit length from the
        /// current stream position.
        /// </summary>
        /// <remarks>
        /// This is a niche format that's used in very few places.
        /// Most significantly, it's used to read the current entity index when
        /// parsing the delta header for packed entities.
        /// </remarks>
        /// <param name="bs">The stream from which to read.</param>
        /// <returns>The read integer.</returns>
        public static uint ReadUBitInt(this IBitStream bs)
        {
            uint ret = bs.ReadInt(6);

            // The 5th and 6th bits determine how many more bits to read.
            switch (ret & (16 | 32))
            {
                case 16:
                    // OR with the first 4 bits. Newly read bits will go after.
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

        /// <summary>
        /// Reads an ASCII <see cref="string"/> from the current stream position
        /// until a null terminator, a newline, or the specified character
        /// <paramref name="limit"/> is reached.
        /// </summary>
        /// <param name="bs">The stream from which to read.</param>
        /// <param name="limit">The maximum amount of characters to read.</param>
        /// <returns>
        /// The read ASCII <see cref="string"/> without the null terminator or newline.
        /// </returns>
        public static string ReadString(this IBitStream bs, int limit = int.MaxValue)
        {
            var result = new List<byte>(512);

            for (int pos = 0; pos < limit; pos++)
            {
                var b = bs.ReadByte();
                if (b == 0 || b == 10)
                    break; // Break if a null terminator or newline is reached.

                result.Add(b);
            }

            return Encoding.ASCII.GetString(result.ToArray());
        }

        /// <summary>
        /// Reads a null-terminated <see cref="string"/> of an unknown length
        /// from the current stream position.
        /// </summary>
        /// <remarks>
        /// Uses the default encoding.
        /// </remarks>
        /// <param name="bs">The stream from which to read.</param>
        /// <returns>The read <see cref="string"/> without the null terminator.</returns>
        /// <seealso cref="ReadCString"/>
        public static string ReadDataTableString(this IBitStream bs)
        {
            using var memstream = new MemoryStream();

            // not particularly efficient, but probably fine
            for (byte b = bs.ReadByte(); b != 0; b = bs.ReadByte())
                memstream.WriteByte(b);

            return Encoding.Default.GetString(memstream.GetBuffer(), 0, checked((int)memstream.Length));
        }

        /// <summary>
        /// Reads a null-terminated <see cref="string"/> of the specified length
        /// from the current stream position.
        /// </summary>
        /// <remarks>
        /// Uses the default encoding.
        /// </remarks>
        /// <param name="reader">The stream from which to read.</param>
        /// <param name="length">The length of the string to read.</param>
        /// <returns>The read <see cref="string"/> without the null terminator.</returns>
        /// <seealso cref="ReadDataTableString"/>
        public static string ReadCString(this IBitStream reader, int length)
        {
            return Encoding.Default.GetString(reader.ReadBytes(length)).Split(new[] { '\0' }, 2)[0];
        }

        #region Protobuf

        /// <summary>
        /// Reads a Protobuf varint from the current stream position as a
        /// <see cref="bool"/>.
        /// </summary>
        /// <remarks>
        /// Known as <c>bool</c> in the Protobuf language.
        /// </remarks>
        /// <returns>The read varint as a <see cref="bool"/>.</returns>
        /// <seealso cref="IBitStream.ReadProtoUInt64"/>
        /// <seealso href="https://developers.google.com/protocol-buffers/docs/encoding#varints"/>
        public static bool ReadProtoBool(this IBitStream bs)
        {
            return bs.ReadProtoUInt64() != 0;
        }

        /// <summary>
        /// Reads a Protobuf varint from the current stream position as a 32-bit
        /// unsigned integer (<see cref="uint"/>).
        /// </summary>
        /// <remarks>
        /// Known as <c>uint64</c> in the Protobuf language.
        /// </remarks>
        /// <returns>The read varint as a <see cref="uint"/>.</returns>
        /// <seealso cref="ReadProtoInt32"/>
        /// <seealso href="https://developers.google.com/protocol-buffers/docs/encoding#varints"/>
        public static uint ReadProtoUInt32(this IBitStream bs)
        {
            return unchecked((uint)bs.ReadProtoUInt64());
        }

        /// <summary>
        /// Reads a Protobuf varint from the current stream position as a 32-bit
        /// signed integer (<see cref="int"/>).
        /// </summary>
        /// <remarks>
        /// Known as <c>int32</c> in the Protobuf language.
        /// </remarks>
        /// <returns>The read varint as a <see cref="int"/>.</returns>
        /// <seealso cref="ReadProtoUInt32"/>
        /// <seealso href="https://developers.google.com/protocol-buffers/docs/encoding#varints"/>
        public static int ReadProtoInt32(this IBitStream bs)
        {
            return unchecked((int)bs.ReadProtoUInt64());
        }

        /// <summary>
        /// Reads a Protobuf varint from the current stream position as a 64-bit
        /// signed integer (<see cref="long"/>).
        /// </summary>
        /// <remarks>
        /// Known as <c>int64</c> in the Protobuf language.
        /// </remarks>
        /// <returns>The read varint as a <see cref="long"/>.</returns>
        /// <seealso cref="IBitStream.ReadProtoUInt64"/>
        /// <seealso href="https://developers.google.com/protocol-buffers/docs/encoding#varints"/>
        public static long ReadProtoInt64(this IBitStream bs)
        {
            return unchecked((long)bs.ReadProtoUInt64());
        }

        // It refuses to inline this...
        /// <summary>
        /// Reads a Protobuf ZigZag-encoded varint from the current stream
        /// position as a 32-bit signed integer
        /// (<see cref="int"/>).
        /// </summary>
        /// <remarks>
        /// Known as <c>sint32</c> in the Protobuf language.
        /// </remarks>
        /// <param name="bs">The stream from which to read.</param>
        /// <returns>The read varint as an <see cref="int"/>.</returns>
        /// <seealso cref="ReadProtoSInt64"/>
        /// <seealso href="https://developers.google.com/protocol-buffers/docs/encoding#signed_integers"/>
        public static int ReadProtoSInt32(this IBitStream bs)
        {
            ulong result = bs.ReadProtoUInt64();
            return (int)(result >> 1) ^ -(int)(result & 1);
        }

        /// <summary>
        /// Reads a Protobuf ZigZag-encoded varint as a 64-bit signed integer
        /// (<see cref="long"/>).
        /// </summary>
        /// <remarks>
        /// Known as <c>sint64</c> in the Protobuf language.
        /// </remarks>
        /// <param name="bs">The stream from which to read.</param>
        /// <returns>The read varint as a <see cref="long"/>.</returns>
        /// <seealso cref="ReadProtoSInt32"/>
        /// <seealso href="https://developers.google.com/protocol-buffers/docs/encoding#signed_integers"/>
        public static long ReadProtoSInt64(this IBitStream bs)
        {
            ulong result = bs.ReadProtoUInt64();
            return (long)(result >> 1) ^ -(long)(result & 1);
        }

        // TODO: possible candidate for inlining.
        /// <summary>
        /// Reads exactly 64 bits from the current stream position as an
        /// unsigned long (<see cref="ulong"/>).
        /// </summary>
        /// <remarks>
        /// Known as <c>fixed64</c> in the Protobuf language.
        /// </remarks>
        /// <param name="bs">The stream from which to read.</param>
        /// <returns>The read <see cref="ulong"/>.</returns>
        /// <seealso cref="ReadProtoFixed64"/>
        /// <seealso href="https://developers.google.com/protocol-buffers/docs/encoding#non-varint_numbers"/>
        public static ulong ReadProtoFixed64(this IBitStream bs)
        {
            uint low = bs.ReadProtoFixed32();
            ulong high = bs.ReadProtoFixed32();

            return (high << 32) | low;
        }

        /// <summary>
        /// Reads exactly 64 bits from the current stream position as a signed
        /// long (<see cref="long"/>).
        /// </summary>
        /// <remarks>
        /// Known as <c>sfixed64</c> in the Protobuf language.
        /// </remarks>
        /// <param name="bs">The stream from which to read.</param>
        /// <returns>The read <see cref="long"/>.</returns>
        /// <seealso cref="ReadProtoFixed64"/>
        /// <seealso href="https://developers.google.com/protocol-buffers/docs/encoding#non-varint_numbers"/>
        public static long ReadProtoSFixed64(this IBitStream bs)
        {
            return unchecked((long)bs.ReadProtoFixed64());
        }

        /// <summary>
        /// Reads exactly 32 bits from the current stream position as an
        /// unsigned integer (<see cref="uint"/>).
        /// </summary>
        /// <remarks>
        /// Known as <c>fixed32</c> in the Protobuf language.
        /// </remarks>
        /// <param name="bs">The stream from which to read.</param>
        /// <returns>The read <see cref="uint"/>.</returns>
        /// <seealso cref="IBitStream.ReadInt"/>
        /// <seealso cref="ReadProtoSFixed32"/>
        /// <seealso href="https://developers.google.com/protocol-buffers/docs/encoding#non-varint_numbers"/>
        public static uint ReadProtoFixed32(this IBitStream bs)
        {
            return bs.ReadInt(32);
        }

        /// <summary>
        /// Reads exactly 32 bits from the current stream position as a signed
        /// integer (<see cref="int"/>).
        /// </summary>
        /// <remarks>
        /// Known as <c>sfixed32</c> in the Protobuf language.
        /// </remarks>
        /// <param name="bs">The stream from which to read.</param>
        /// <returns>The read <see cref="int"/>.</returns>
        /// <seealso cref="IBitStream.ReadInt"/>
        /// <seealso cref="ReadProtoFixed32"/>
        /// <seealso href="https://developers.google.com/protocol-buffers/docs/encoding#non-varint_numbers"/>
        public static int ReadProtoSFixed32(this IBitStream bs)
        {
            return unchecked((int)bs.ReadInt(32));
        }

        /// <summary>
        /// Reads a UTF-8 protobuf-encoded <see cref="string"/> from the current
        /// stream position .
        /// </summary>
        /// <remarks>
        /// Known as <c>string</c> in the Protobuf language.
        /// </remarks>
        /// <param name="reader">The stream from which to read.</param>
        /// <returns>The read <see cref="string"/>.</returns>
        /// <seealso href="https://developers.google.com/protocol-buffers/docs/encoding#strings"/>
        public static string ReadProtoString(this IBitStream reader)
        {
            return Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadProtoInt32()));
        }

        #endregion
    }
}
