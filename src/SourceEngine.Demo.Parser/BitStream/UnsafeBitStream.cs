using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SourceEngine.Demo.Parser.BitStream
{
    public unsafe class UnsafeBitStream : IBitStream
    {
        private const int SLED = 4; // 4 bytes
        private const int BUFSIZE = 2048 + SLED;

        // MSB masks (protobuf varint end signal)
        private const uint MSB_1 = 0x00000080; // 1 byte
        private const uint MSB_2 = 0x00008000; // 2 bytes
        private const uint MSB_3 = 0x00800000; // 3 bytes
        private const uint MSB_4 = 0x80000000; // 4 bytes

        // byte masks (except MSB)
        private const uint MSK_1 = 0x0000007F; // first 7 bits
        private const uint MSK_2 = 0x00007F00; // skip 1 byte then mask 7 bits
        private const uint MSK_3 = 0x007F0000; // skip 2 bytes then mask 7 bits
        private const uint MSK_4 = 0x7F000000; // skip 3 bytes then mask 7 bits

        /// <summary>
        /// Stack of end positions for chunks currently being read.
        /// </summary>
        /// <seealso cref="BeginChunk"/>
        /// <seealso cref="EndChunk"/>
        private readonly Stack<long> ChunkTargets = new();

        /// <summary>
        /// Buffer that stores data read from the <see cref="Underlying"/> stream.
        /// </summary>
        /// <remarks>
        /// The first 4 bytes, referred to as the "sled" (size defined by
        /// <see cref="SLED"/>) are reserved for storing the last 4 bytes read
        /// fully from the stream. This guarantees a certain amount of bits
        /// will always be available for a read.
        /// </remarks>
        /// <seealso cref="EndChunk"/>
        /// <seealso cref="RefillBuffer"/>
        private readonly byte[] Buffer = new byte[BUFSIZE];

        /// <summary>
        /// Pinned handle to the <see cref="Buffer"/>, which protects it from
        /// garbage collection and allows its address to be resolved.
        /// </summary>
        /// <seealso cref="Dispose()"/>
        private GCHandle HBuffer;

        /// <summary>
        /// Pointer to the <see cref="Buffer"/>.
        /// </summary>
        private byte* PBuffer;

        /// <summary>
        /// Underlying stream from which data is read.
        /// </summary>
        /// <seealso cref="EndChunk"/>
        /// <seealso cref="RefillBuffer"/>
        private Stream Underlying;

        /// <summary>
        /// <c>true</c> if the end of the <see cref="Underlying"/> stream has been reached.
        /// </summary>
        private bool EndOfStream;

        /// <summary>
        /// Pointer offset that points to to the first unread bit in the <see cref="Buffer"/>.
        /// </summary>
        /// <seealso cref="TryAdvance"/>
        private int Offset;

        /// <summary>
        /// Number of bits currently read into the <see cref="Buffer"/>, excluding the sled bits.
        /// </summary>
        /// <remarks>
        /// Any <see cref="Offset"/> such that <c>(Offset - SLED * 4) >= BitsInBuffer</c>
        /// points to invalid or old data.
        /// </remarks>
        private int BitsInBuffer;

        /// <summary>
        /// Position in the <see cref="Underlying"/> stream which corresponds to
        /// the start of the <see cref="Buffer"/> (i.e. index 0, the sled start).
        /// </summary>
        private long LazyGlobalPosition;

        /// <summary>
        /// Position of the first unprocessed bit in the <see cref="Underlying"/> stream.
        /// </summary>
        /// <remarks>
        /// Technically this bit has already been read and placed into the
        /// <see cref="Buffer"/>, so consider it "unprocessed".
        /// </remarks>
        /// <seealso cref="LazyGlobalPosition"/>
        /// <seealso cref="Offset"/>
        private long ActualGlobalPosition => LazyGlobalPosition + Offset;

        public void Initialize(Stream underlying)
        {
            HBuffer = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
            PBuffer = (byte*)HBuffer.AddrOfPinnedObject().ToPointer();

            Underlying = underlying;
            RefillBuffer();

            // Move the offset past the sled since its still empty after the first refill.
            // (RefillBuffer copies into the sled and *then* reads, meaning there was nothing to copy the first time)
            Offset = SLED * 8;
        }

        void IDisposable.Dispose()
        {
            Dispose();
            GC.SuppressFinalize(this);
        }

        public uint ReadInt(int numBits)
        {
            uint result = PeekInt(numBits);
            if (TryAdvance(numBits))
                RefillBuffer();

            return result;
        }

        public bool ReadBit()
        {
            // Convert the offset from bits to bytes (right shift by 3 is a division by 8).
            // Read a byte from the buffer at the offset. For example, offset 44 will read byte 5, which is bits 40-47.
            // Then, mask off the byte to get the value of the single bit the offset points to.
            // AND by 7 (modulo 8) to get the amount of bits past the byte boundary (e.g. 4 bits for an offset of 44).
            // Shift left by that amount to create the bit mask (e.g. 4 creates mask 0001 0000 to select the 4th MSB).
            // Finally AND the read byte with the mask.
            bool bit = (PBuffer[Offset >> 3] & (1 << (Offset & 7))) != 0;
            if (TryAdvance(1))
                RefillBuffer();

            return bit;
        }

        public byte ReadByte()
        {
            var ret = (byte)PeekInt(8);
            if (TryAdvance(8))
                RefillBuffer();

            return ret;
        }

        public byte ReadByte(int bits)
        {
            BitStreamUtil.AssertMaxBits(8, bits);

            var ret = (byte)PeekInt(bits);
            if (TryAdvance(bits))
                RefillBuffer();

            return ret;
        }

        public byte[] ReadBytes(int bytes)
        {
            var ret = new byte[bytes];
            ReadBytes(ret, bytes);

            return ret;
        }

        public int ReadSignedInt(int numBits)
        {
            BitStreamUtil.AssertMaxBits(32, numBits);

            // Just like PeekInt, but before the right shift, cast to a signed long for sign extension.
            var result = (int)(
                (long)(
                    *(ulong*)(PBuffer + ((Offset >> 3) & ~3))
                    << (8 * 8 - (Offset & (8 * 4 - 1)) - numBits)
                ) >> (8 * 8 - numBits)
            );

            if (TryAdvance(numBits))
                RefillBuffer();

            return result;
        }

        public float ReadFloat()
        {
            uint iResult = PeekInt(32); // omfg please inline this
            if (TryAdvance(32))
                RefillBuffer();

            return *(float*)&iResult; // standard reinterpret cast
        }

        public byte[] ReadBits(int bits)
        {
            // Shifting right by 3 is a division by 8 to convert bits to bytes.
            // Allocate space for an extra byte to store the data that's past the byte boundary.
            byte[] result = new byte[(bits + 7) >> 3];
            ReadBytes(result, bits >> 3);

            // AND with 7 (modulo 8) to get the amount of bits past the byte boundary.
            // If there are extra bits past the boundary, read them as a byte and add it to the end of the array.
            if ((bits & 7) != 0)
                result[bits >> 3] = ReadByte(bits & 7);

            return result;
        }

        public int ReadProtobufVarInt()
        {
            // Only used for debug assertions.
            var availableBits = BitsInBuffer + SLED * 8 - Offset;

            // Start by overflowingly reading 32 bits.
            // Reading beyond the buffer contents is safe in this case,
            // because the sled ensures that we stay inside of the buffer.
            uint buf = PeekInt(32, true);

            // Always take the first byte; read the rest if necessary.
            // Normally, the least significant group of 7 bits is first.
            // However, PeekInt already returns these groups in reverse.
            uint result = buf & MSK_1;
            BitStreamUtil.AssertMaxBits(availableBits, 1 * 8);

            // The MSB at every byte boundary will be set if there are more bytes that need to be decoded.
            if ((buf & MSB_1) != 0)
            {
                // Right shift to get rid of the MSBs from the previous bytes (in this case only 1 previous byte).
                result |= (buf & MSK_2) >> 1;
                BitStreamUtil.AssertMaxBits(availableBits, 1 * 8);

                if ((buf & MSB_2) != 0)
                {
                    result |= (buf & MSK_3) >> 2;
                    BitStreamUtil.AssertMaxBits(availableBits, 2 * 8);

                    if ((buf & MSB_3) != 0)
                    {
                        result |= (buf & MSK_4) >> 3;
                        BitStreamUtil.AssertMaxBits(availableBits, 3 * 8);

                        if ((buf & MSB_4) != 0) {
                            // Unfortunately, it's larger than 4 bytes (it's probably a negative integer). That's rare.
                            // This algorithm is limited to 4 bytes since the peek and masks are 32-bit integers.
                            // Fall back to the slow implementation.
                            return BitStreamUtil.ReadProtobufVarIntStub(this);
                        }
                        else if (TryAdvance(4 * 8))
                        {
                            RefillBuffer();
                        }
                    }
                    else if (TryAdvance(3 * 8))
                    {
                        RefillBuffer();
                    }
                }
                else if (TryAdvance(2 * 8))
                {
                    RefillBuffer();
                }
            }
            else if (TryAdvance(1 * 8))
            {
                RefillBuffer();
            }

            return unchecked((int)result);
        }

        public void BeginChunk(int length)
        {
            ChunkTargets.Push(ActualGlobalPosition + length);
        }

        public void EndChunk()
        {
            // To provide at least a little (and cheap) bit of sanity even
            // when performance is of utmost importance, this implementation
            // chooses a nice tradeoff: Unlike the BitArrayStream, it lets you
            // read beyond chunk boundaries. Here, we have to calculate the
            // number of read bits anyways so we know how much we need to skip,
            // so we might as well verify that this difference isn't negative.
            var target = ChunkTargets.Pop();

            // How many bits remain ahead of the current position until the target is reached.
            var delta = checked((int)(target - ActualGlobalPosition));

            if (delta < 0)
            {
                throw new InvalidOperationException("Someone read beyond a chunk boundary");
            }
            else if (delta > 0)
            {
                // Prefer seeking to skip to the target if the stream supports seeking.
                if (Underlying.CanSeek)
                {
                    // Number of bits in the buffer that have not been consumed yet.
                    // Subtract the offset because all bits up to the offset have already been consumed.
                    int bufferedBitsToSkip = BitsInBuffer - Offset + (SLED * 8);

                    // The number of bits in the buffer is less than the number
                    // of bits that need to be skipped to reach the end of the chunk.
                    // As mentioned above, the sled (converted to bits) needs to be added to get the true value.
                    if (bufferedBitsToSkip < delta)
                    {
                        if (EndOfStream)
                            throw new EndOfStreamException();

                        // Number of bits between the last valid bit in the buffer and the target.
                        // The shr by 3 is a division by 8 to convert bits to bytes.
                        int unbufferedBitsToSkip = delta - bufferedBitsToSkip;
                        Underlying.Seek(unbufferedBitsToSkip >> 3, SeekOrigin.Current);

                        // Unlike RefillBuffer, which reads 4 bytes,
                        // this reads 8 because the sled also has to be populated.
                        int offset, bytesRead = 1337; // I'll cry if this ends up in the generated code
                        for (offset = 0; offset < 8 && bytesRead != 0; offset += bytesRead)
                            bytesRead = Underlying.Read(Buffer, offset, BUFSIZE - offset);

                        // Don't count the sled bytes towards the amount of bits in the buffer.
                        // The reason it has to be done here but not in RefillBuffer is that
                        // this had to fill an empty sled while the latter did not.
                        BitsInBuffer = 8 * (offset - SLED);

                        if (bytesRead == 0)
                        {
                            // The sled can be consumed now since the end of the stream was reached.
                            BitsInBuffer += SLED * 8;
                            EndOfStream = true;
                        }

                        // Modulo 8 because the stream can only seek to a byte boundary;
                        // there may be up to 7 more bits that need to be skipped.
                        Offset = unbufferedBitsToSkip & 7;
                        LazyGlobalPosition = target - Offset;
                    }
                    else if (TryAdvance(delta))
                    {
                        // The target is within the range of valid bits in the buffer. However, TryAdvance is true, so
                        // there are 4 or less bytes remaining in the buffer after the target, and a refill is needed.
                        RefillBuffer();
                    }
                }
                else if (TryAdvance(delta))
                {
                    // The stream doesn't support seeking unfortunately; read and discard instead.
                    RefillBuffer();
                }
            }
        }

        public bool ChunkFinished => ChunkTargets.Peek() == ActualGlobalPosition;

        ~UnsafeBitStream()
        {
            Dispose();
        }

        /// <summary>
        /// Frees <see cref="HBuffer"/>, the handle to the <see cref="Buffer"/>.
        /// </summary>
        private void Dispose()
        {
            var nullptr = (byte*)IntPtr.Zero.ToPointer();

            if (PBuffer != nullptr)
            {
                // GCHandle docs state that Free() must only be called once.
                // So we use PBuffer to ensure that.
                PBuffer = nullptr;
                HBuffer.Free();
            }
        }

        /// <summary>
        /// Advances the cursor by <paramref name="howMuch"/> bits.
        /// Adds <paramref name="howMuch"/> to the current buffer <see cref="Offset"/>.
        /// </summary>
        /// <remarks>
        /// Note that <see cref="Offset"/> is relative to the start of the sled rather than the end of the sled. If the
        /// offset is allowed to reach and consume the last <see cref="SLED"/> bytes of the <see cref="Buffer"/>, then
        /// the next <see cref="RefillBuffer"/> call will copy already-consumed bytes into the sled. Therefore, a refill
        /// is required when there are <see cref="SLED"/> or less bytes remaining after the advanced position.
        /// <p>
        /// Apparently mono can't inline the old <c>Advance()</c> because that would mess up the call stack:
        /// <c>Advance->RefillBuffer->Stream.Read</c> which could then throw. <c>Advance</c>'s stack frame would be
        /// missing. Because of that, the call to <see cref="RefillBuffer"/> has to be inlined manually.
        /// </p>
        /// </remarks>
        /// <example><c>
        /// if (TryAdvance(howMuch))
        ///     RefillBuffer();
        /// </c></example>
        /// <param name="howMuch">The number of bits by which to advance the cursor.</param>
        /// <returns>
        /// True if the advanced position surpassed the last valid bit in the buffer or there are &lt;=
        /// <see cref="SLED"/> bytes remaining in the buffer after the advanced position.
        /// In such cases, <see cref="RefillBuffer"/> needs to be called right after this function.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryAdvance(int howMuch)
        {
            return (Offset += howMuch) >= BitsInBuffer;
        }

        /// <summary>
        /// Fills the <see cref="Buffer"/> with data from the <see cref="Underlying"/> stream until the bit pointed to
        /// by the <see cref="Offset"/> is within the buffer.
        /// </summary>
        /// <remarks>
        /// Copies the last <see cref="SLED"/> bytes into the first four bytes of the <see cref="Buffer"/>, called the
        /// "sled", before overwriting the buffer with new data. The sled should always contain unconsumed bits.
        /// Furthermore, the buffer should always contain at least <see cref="SLED"/> bytes in addition to the bytes in
        /// the sled. Maintaining a sled means that the buffer guarantees a certain amount of bits will always be
        /// available for a read.
        /// </remarks>
        /// <exception cref="EndOfStreamException">The <see cref="Underlying"/> stream has no more data.</exception>
        /// <seealso cref="TryAdvance"/>
        private void RefillBuffer()
        {
            do
            {
                // End of stream detection:
                // These if clauses are kinda reversed, so this is how we're gonna do it:
                // a) your average read:
                //    None of them trigger. End of story.
                // b) the first read into the last buffer:
                //    the (bytesRead == 0) down there fires
                // c) the LAST read (end of stream follows):
                //    the if (EndOfStream) fires, setting BitsInBuffer to 0 and zeroing out
                //    the head of the buffer, so we read zeroes instead of random other stuff
                // d) the (overflowing) read after the last read:
                //    BitsInBuffer is 0 now, so we throw
                //
                // Just like chunking, this safety net has as little performance overhead as possible,
                // at the cost of throwing later than it could (which can be too late in some
                // scenarios; as in: you stop using the bitstream before it throws).
                if (EndOfStream)
                {
                    if (BitsInBuffer == 0)
                        throw new EndOfStreamException();

                    // Another late overrun detection:
                    // Offset SHOULD be < 0 after this.
                    // So Offset < BitsInBuffer.
                    // So we don't loop again.
                    // If it's not, we'll loop again which is exactly what we want
                    // as we overran the stream and wanna hit the throw above.
                    Offset -= BitsInBuffer + 1;
                    LazyGlobalPosition += BitsInBuffer + 1;
                    *(uint*)PBuffer = 0; // safety
                    BitsInBuffer = 0;
                    continue;
                }

                // Copy the sled - copy the last 4 bytes to the first 4 bytes. The first 4 bytes are always reserved for
                // the sled and are not counted towards BitsInBuffer. Therefore, using BitsInBuffer directly as the
                // offset, without offsetting by the sled size, means the last 4 read bytes will be copied to the sled.
                //
                // For example, if 16 bytes were read, then bytes 0-3 will be the sled and bytes 4-19 will be the read
                // bytes. An offset of 16 means bytes 16-19, the last 4 read bytes, will be moved into the sled.
                //
                // (BitsInBuffer >> 3) is really (BitsInBuffer / 8), which just converts bits to bytes.
                // While this is a floored division, it doesn't matter because BitsInBuffer is guaranteed to be a
                // multiple of a byte; its value comes from Stream.Read, which can only read entire bytes.
                *(uint*)PBuffer = *(uint*)(PBuffer + (BitsInBuffer >> 3));

                // The reads below overwrite the current bits in the buffer, therefore advancing the buffer forward.
                Offset -= BitsInBuffer; // Offset moves backward (moving the buffer forward makes the target closer).
                LazyGlobalPosition += BitsInBuffer; // Start position of the buffer moves forward.

                // Read until at least 4 bytes are read or the end of the stream is reached (when bytesRead == 0).
                // It needs at least 4 bytes because that's the sled size, and subsequent refills expect to be able
                // to refill the sled.
                //
                // Read() does not guarantee a minimum number of bytes read, only a maximum. Therefore, it may take
                // multiple reads to read 4 bytes, despite the number of bytes requested being much larger than 4.
                //
                // The buffer starts being filled past the sled, which are be the first four bytes in the buffer.
                // Each read, the offset is updated with the number of bytes read.
                int offset, bytesRead = 1337; // I'll cry if this ends up in the generated code
                for (offset = 0; offset < 4 && bytesRead != 0; offset += bytesRead)
                    bytesRead = Underlying.Read(Buffer, SLED + offset, BUFSIZE - SLED - offset);

                // offset is the sum of the bytes read from all Read() calls in the loop above.
                BitsInBuffer = 8 * offset;

                // 0 bytes read means the end of the stream was reached.
                if (bytesRead == 0)
                {
                    // The last 4 bytes are normally reserved to be copied over to the front by the next refill.
                    // However, since the end of the stream was reached, these last bytes no longer need to be reserved.
                    // Therefore, count the sled bits towards the total number of bits in the buffer.
                    // TryAdvance will return false now if the new position has <= 4 bytes after it in the buffer.
                    BitsInBuffer += SLED * 8;
                    EndOfStream = true;
                }
            }
            // Keep reading until enough bits are read to reach the offset.
            // If the offset is too far ahead from the original position,
            // data at the start of the buffer will get discarded since it can't all fit.
            // See TryAdvance for details on why this doesn't account for the size of the sled.
            while (Offset >= BitsInBuffer);
        }

        /// <summary>
        /// Retrieves up to 32 bits from the <see cref="Buffer"/>.
        /// </summary>
        /// <param name="numBits">The number of bits to read, up to a maximum of 32.</param>
        /// <param name="mayOverflow">
        /// <c>true</c> if more bits can be read than are available in the <see cref="Buffer"/>.
        /// This only affects a debug assertion.
        /// </param>
        /// <returns>The bits read in the form of an unsigned integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint PeekInt(int numBits, bool mayOverflow = false)
        {
            BitStreamUtil.AssertMaxBits(32, numBits);
            #if CHECK_INTEGRITY
            Debug.Assert(
                mayOverflow || Offset + numBits <= BitsInBuffer + SLED * 8,
                "gg",
                "This code just fell apart. We're all dead. Offset={0} numBits={1} BitsInBuffer={2}",
                Offset,
                numBits,
                BitsInBuffer
            );
            #endif

            // Summary:
            // Read 8 bytes after aligning the offset to a 4-byte boundary.
            // Shift left to discard bits from numBits + 1 to 32 & MSBs that are past the non-aligned offset + 4 bytes.
            // Shift back to the right to discard the LSBs that are before the non-aligned offset.
            return (uint)(
                (
                    // Read 8 bytes (64 bits) and dereference into a ulong.
                    // It's a ulong so there's extra space to shift to the left later.
                    *(ulong*)(
                        PBuffer + (
                            // Shifting to the left by 3 divides by 8 to convert the offset from bits to bytes.
                            (Offset >> 3)
                            // Align the offset to a 4-byte boundary rather than 1-byte as a micro-optimisation.
                            // AND with the complement of 3 sets the 2 least significant bits to 0.
                            //
                            // For example, an offset of 464 bits (58 bytes) will become +56 (remainder of 2 bytes).
                            & ~3
                        )
                    )
                    // Shift the ulong to the left.
                    // For example, offset of 58 bytes and reading 32 bits results in shifting by 64 - 16 - 32 = 16.
                    // This discards the 16 MSBs, resulting in only bytes 56 to 62 remaining (along with 0s as LSBs).
                    << (
                        // Start with 64 bits (8 bytes, length of ulong).
                        8 * 8
                        // Subtract the number of bits over the 4 byte-boundary, calculated with (Offset % (8 * 4)).
                        // All the most-significant bits past the offset + boundary get discarded.
                        - (Offset & (8 * 4 - 1))
                        // Lastly, subtract the number of bits to read.
                        // Any bits between numBits + 1 and 32 will also get discarded.
                        - numBits
                    )
                )
                // Shift back to the right.
                // This time, don't include the alignment remainder.
                // The least significant bytes will be discarded e.g. bytes 56 and 57 when offset is 58 bytes.
                >> (8 * 8 - numBits)
            );
        }

        /// <summary>
        /// Reads bytes into <paramref name="ret"/>.
        /// </summary>
        /// <param name="ret">Pointer to the buffer to read the bytes into.</param>
        /// <param name="bytes">Amount of bytes to read.</param>
        private void ReadBytes(byte[] ret, int bytes)
        {
            if (bytes < 3)
            {
                for (int i = 0; i < bytes; i++)
                    ret[i] = ReadByte();
            }
            else if ((Offset & 7) == 0) // Modulo 8; no remainder means the offset is aligned to a byte boundary.
            {
                int offset = 0; // Amount of bytes that have been copied.

                while (offset < bytes)
                {
                    // Read until the end of the input buffer if the remaining amount to read exceeds the buffer.
                    // Note: Offset is never greater than BitsInBuffer (otherwise the buffer would've been refilled).
                    int remainingBytes = Math.Min((BitsInBuffer - Offset) >> 3, bytes - offset);
                    System.Buffer.BlockCopy(Buffer, Offset >> 3, ret, offset, remainingBytes);
                    offset += remainingBytes;

                    if (TryAdvance(remainingBytes * 8))
                        RefillBuffer();
                }
            }
            else
            {
                // Offset is not aligned, so use HyperspeedCopyRound because it can handle misalignment.
                fixed (byte* retptr = ret)
                {
                    int offset = 0;

                    while (offset < bytes)
                    {
                        int remainingBytes = Math.Min(((BitsInBuffer - Offset) >> 3) + 1, bytes - offset);
                        HyperspeedCopyRound(remainingBytes, retptr + offset);
                        offset += remainingBytes;

                        // HyperspeedCopyRound takes care of refilling the buffer as necessary.
                    }
                }
            }
        }

        /// <summary>
        /// Copies bytes from <see cref="Buffer"/> to <paramref name="retptr"/>,
        /// supporting a misaligned <see cref="Offset"/>.
        /// </summary>
        /// <remarks>
        /// This is probably the most significant unsafe speedup: copying about 64 bits at a time instead of 8 bits.
        /// </remarks>
        /// <param name="bytes">Amount of bytes to copy.</param>
        /// <param name="retptr">Pointer to the buffer to copy the bytes into.</param>
        private void HyperspeedCopyRound(int bytes, byte* retptr) // you spin me right round baby right round...
        {
            // Begin by aligning to the first byte.
            // These values will make more sense after looking at the loop below.
            int misalign = 8 - (Offset & 7); // Modulo 8; calculate the amount of bits until the *next* boundary.
            int realign = sizeof(ulong) * 8 - misalign; // How many bits from the current value will be copied.
            ulong step = ReadByte(misalign); // Read until the next byte boundary is reached.

            // Pointers are ulongs instead of bytes to allow copying 8 bytes at a time.
            var inptr = (ulong*)(PBuffer + (Offset >> 3)); // Pointer to input buffer, starting at the aligned offset.
            var outptr = (ulong*)retptr;

            // Main loop - copy 8 bytes at a time.
            // Subtract 1 byte because it was already read above (exactly 1 byte was read if it was already aligned).
            for (int i = 0; i < (bytes - 1) / sizeof(ulong); i++)
            {
                // Get 8 bytes and advance the input pointer.
                ulong current = *inptr++;

                // LSBs in `step` contain the remaining bytes from the previous read that are nonaligned.
                // Shift the current value past those nonaligned bytes. The MSBs of `current` are truncated.
                step |= current << misalign;

                // Copy the 8 bytes into the output and advance the output pointer.
                *outptr++ = step;

                // Store the MSBs that were truncated above. They'll be copied in the next iteration.
                step = current >> realign;
            }

            // Now process the rest. They're not aligned to an 8-byte boundary, so they have to be read 1 at a time.
            int rest = (bytes - 1) % sizeof(ulong); // Amount of bytes not copied by the loop above.
            Offset += (bytes - rest - 1) * 8; // Adjust the offset to account for the bytes read above.

            // Output pointer as bytes, starting at the position the loop left it at.
            var bout = (byte*)outptr;

            // The read at the start aligned the offset to a byte boundary. However, this caused the amount of bits
            // to read to become nonaligned. Now, the opposite needs to be true: the number of bits to read has to be
            // aligned, but the offset is no longer used directly, so it can be nonaligned.
            //
            // Ex: Start with Offset = 46, bytes = 32, rest = 7 (56 bytes).
            // The loop above excluded 1 byte, but really only 2 bits were read to reach the next boundary @ 48 bits.
            // This means there are really 56 + (8 - 2) = 62 bits left to read, which is misaligned by (8 - 2) = 6 bits.
            // Once those 6 bits are read, the alignment is fixed and the rest is as simple as reading 56 / 8 = 7 bytes.

            // Start by reading until the next byte boundary.
            // Note that `step` still contains the truncated MSBs from the final read in the loop.
            // It contains exactly `misalign` number of bits, and 8 - misalign will be the bits to the next boundary.
            // As done in the loop above, shift the read value past the LSBs of `step`.
            bout[0] = (byte)((ReadInt(8 - misalign) << misalign) | step);

            // Now it's aligned. Simply read the remaining bytes 1-by-1 into the output buffer.
            for (int i = 1; i < rest + 1; i++)
                bout[i] |= ReadByte();
        }
    }
}
