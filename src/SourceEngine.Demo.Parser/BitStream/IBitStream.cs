using System;
using System.IO;

namespace SourceEngine.Demo.Parser.BitStream
{
    /// <summary>
    /// A stream that can read data at bit boundaries.
    /// </summary>
    /// <remarks>
    /// <p>
    /// Useful for reading data which is tightly packed without any alignment,
    /// such as protobuf messages.
    /// </p>
    /// <p>
    /// It supports "chunking", which allows reading data up to a certain point
    /// and then skipping the rest. A chunk is created with a given length.
    /// Any remaining unread data within the bounds of the chunk is skipped
    /// until the end of the chunk is reached. Chunks can be nested in a
    /// stack-like manner.
    /// </p>
    /// </remarks>
    public interface IBitStream : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether the current chunk was fully read.
        /// </summary>
        /// <remarks>
        /// The return value is undefined if there's no current chunk.
        /// </remarks>
        /// <value>
        /// <c>true</c> if chunk is finished; otherwise, <c>false</c>.
        /// </value>
        bool ChunkFinished { get; }

        /// <summary>
        /// Initialize the object with a <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The stream which contains the underlying data.</param>
        void Initialize(Stream stream);

        /// <summary>
        /// Reads the specified number of bits from the current stream position
        /// as an unsigned integer (<see cref="uint"/>).
        /// </summary>
        /// <param name="bitCount">Number of bits to read.</param>
        /// <returns>The read <see cref="uint"/>.</returns>
        /// <seealso cref="ReadSignedInt"/>
        uint ReadInt(int bitCount);

        /// <summary>
        /// Reads the specified number of bits from the current stream position
        /// as a <see cref="int"/>.
        /// </summary>
        /// <param name="bitCount">Number of bits to read.</param>
        /// <returns>The read <see cref="int"/>.</returns>
        /// <seealso cref="ReadInt"/>
        int ReadSignedInt(int bitCount);

        /// <summary>
        /// Reads a single bit from the current stream position as a
        /// <see cref="bool"/>.
        /// </summary>
        /// <returns>
        /// The read bit as a <see cref="bool"/>, with a true value meaning the
        /// bit was set.
        /// </returns>
        /// <seealso cref="ReadBits"/>
        bool ReadBit();

        /// <summary>
        /// Reads a <see cref="byte"/> from the current stream position.
        /// </summary>
        /// <returns>The read <see cref="byte"/>.</returns>
        /// <seealso cref="ReadByte(int)"/>
        /// <seealso cref="ReadBytes"/>
        byte ReadByte();

        /// <summary>
        /// Reads the specified number of bits from the current stream position
        /// as a <see cref="byte"/>.
        /// </summary>
        /// <param name="bitCount">Number of bits to read.</param>
        /// <returns>The read <see cref="byte"/>.</returns>
        /// <seealso cref="ReadByte()"/>
        /// <seealso cref="ReadBytes"/>
        byte ReadByte(int bitCount);

        /// <summary>
        /// Reads the specified amount of bytes from the current stream position.
        /// </summary>
        /// <param name="count">Number of bytes to read.</param>
        /// <returns>An array of the read bytes.</returns>
        /// <seealso cref="ReadByte()"/>
        /// <seealso cref="ReadByte(int)"/>
        byte[] ReadBytes(int count);

        /// <summary>
        /// Reads a 32-bit float (<see cref="float"/>) from the current stream
        /// position.
        /// </summary>
        /// <returns>The read <see cref="float"/>.</returns>
        float ReadFloat();

        /// <summary>
        /// Reads the specified amount of bits from the current stream position
        /// as bytes.
        /// </summary>
        /// <param name="count">Number of bits to read.</param>
        /// <returns>A <see cref="Byte"/> array of the read bits.</returns>
        /// <seealso cref="ReadBit"/>
        /// <seealso cref="ReadBytes"/>
        byte[] ReadBits(int count);

        /// <summary>
        /// Reads a Protobuf varint from the current stream position as a 32-bit
        /// signed integer (<see cref="int"/>).
        /// </summary>
        /// <remarks>
        /// Can be used to read the following Protobuf types:
        /// <list type="bullet">
        /// <item><c>int32</c></item>
        /// <item><c>uint32</c> (cast the return value to <see cref="uint"/>)</item>
        /// <item><c>bool</c> (cast the return value to <see cref="bool"/>)</item>
        /// <item><c>enum</c> (32-bit only)</item>
        /// </list>
        /// While <c>sint32</c> is a varint, it is different from the types
        /// above since it uses ZigZag encoding. Hence, this function cannot
        /// decode that type.
        /// </remarks>
        /// <returns>The read varint as an <see cref="int"/>.</returns>
        /// <seealso href="https://developers.google.com/protocol-buffers/docs/encoding#varints"/>
        int ReadProtobufVarInt();

        /// <summary>
        /// Begins a chunk.
        /// </summary>
        /// <param name="length">The chunk's length in bits.</param>
        /// <remarks>
        /// You must not try to read beyond the end of a chunk. Doing
        /// so may corrupt the bitstream's state, leading to
        /// implementation-defined behavior of all methods except
        /// <see cref="IDisposable.Dispose"/>.
        /// </remarks>
        void BeginChunk(int length);

        /// <summary>
        /// Ends a chunk.
        /// </summary>
        /// <remarks>
        /// If there's no current chunk, this method <i>may</i> throw
        /// and leave the bitstream in an undefined state that can
        /// be cleaned up safely by disposing it.
        /// Alternatively, it may also return normally if it didn't
        /// corrupt or otherwise modify the bitstream's state.
        /// </remarks>
        void EndChunk();
    }
}
