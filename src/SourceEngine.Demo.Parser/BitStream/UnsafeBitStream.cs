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
        private const int SLED_SIZE = 4; // 4 bytes
        private const int BUFFER_SIZE = 2048 + SLED_SIZE;

        // MSB masks (protobuf varint end signal)
        private const uint MSB_1 = 0x00000080; // 1 byte
        private const uint MSB_2 = 0x00008000; // 2 bytes
        private const uint MSB_3 = 0x00800000; // 3 bytes
        private const uint MSB_4 = 0x80000000; // 4 bytes

        // byte masks (except MSB)
        private const uint MASK_1 = 0x0000007F; // first 7 bits
        private const uint MASK_2 = 0x00007F00; // skip 1 byte then mask 7 bits
        private const uint MASK_3 = 0x007F0000; // skip 2 bytes then mask 7 bits
        private const uint MASK_4 = 0x7F000000; // skip 3 bytes then mask 7 bits

        /// <summary>
        /// Stack of end positions for chunks currently being read.
        /// </summary>
        /// <seealso cref="BeginChunk"/>
        /// <seealso cref="EndChunk"/>
        private readonly Stack<long> chunkTargets = new();

        /// <summary>
        /// Buffer that stores data read from the <see cref="stream"/>.
        /// </summary>
        /// <remarks>
        /// The first 4 bytes, referred to as the "sled" (size defined by
        /// <see cref="SLED_SIZE"/>) are reserved for storing the last 4 bytes
        /// read fully from the stream. This guarantees a certain amount of bits
        /// will always be available for a read.
        /// </remarks>
        /// <seealso cref="EndChunk"/>
        /// <seealso cref="RefillBuffer"/>
        private readonly byte[] buffer = new byte[BUFFER_SIZE];

        /// <summary>
        /// Pinned handle to the <see cref="buffer"/>, which protects it from
        /// garbage collection and allows its address to be resolved.
        /// </summary>
        /// <seealso cref="Dispose()"/>
        private GCHandle bufferHandle;

        /// <summary>
        /// Pointer to the <see cref="buffer"/>.
        /// </summary>
        private byte* bufferPtr;

        /// <summary>
        /// Underlying stream from which data is read.
        /// </summary>
        /// <seealso cref="EndChunk"/>
        /// <seealso cref="RefillBuffer"/>
        private Stream stream;

        /// <summary>
        /// <c>true</c> if the end of the <see cref="stream"/> has been reached.
        /// </summary>
        private bool reachedEnd;

        /// <summary>
        /// Pointer offset that points to to the first unread bit in the <see cref="buffer"/>.
        /// </summary>
        /// <seealso cref="Advance"/>
        private int offset;

        /// <summary>
        /// Number of bits currently read into the <see cref="buffer"/>, excluding the sled bits.
        /// </summary>
        /// <remarks>
        /// Any <see cref="offset"/> such that <c>(offset - SLED_SIZE * 4) >= bufferedBits</c>
        /// points to invalid or old data.
        /// </remarks>
        private int bufferedBits;

        /// <summary>
        /// Position in the <see cref="stream"/> which corresponds to
        /// the start of the <see cref="buffer"/> (i.e. index 0, the sled start).
        /// </summary>
        private long globalStartPosition;

        /// <summary>
        /// Position of the first unprocessed bit in the <see cref="stream"/>.
        /// </summary>
        /// <remarks>
        /// Technically this bit has already been read and placed into the
        /// <see cref="buffer"/>, so consider it "unprocessed".
        /// </remarks>
        /// <seealso cref="globalStartPosition"/>
        /// <seealso cref="offset"/>
        private long GlobalPosition => globalStartPosition + offset;

        public void Initialize(Stream stream)
        {
            bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            bufferPtr = (byte*)bufferHandle.AddrOfPinnedObject().ToPointer();

            this.stream = stream;
            RefillBuffer();

            // Move the offset past the sled since its still empty after the first refill.
            // (RefillBuffer copies into the sled and *then* reads, meaning there was nothing to copy the first time)
            offset = SLED_SIZE * 8;
        }

        void IDisposable.Dispose()
        {
            Dispose();
            GC.SuppressFinalize(this);
        }

        public uint ReadInt(int bitCount)
        {
            uint result = PeekInt(bitCount);
            Advance(bitCount);

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
            bool bit = (bufferPtr[offset >> 3] & (1 << (offset & 7))) != 0;
            Advance(1);

            return bit;
        }

        public byte ReadByte()
        {
            return ReadByte(8);
        }

        public byte ReadByte(int bitCount)
        {
            BitStreamUtil.AssertMaxBits(8, bitCount);

            return (byte)ReadInt(bitCount);
        }

        public byte[] ReadBytes(int count)
        {
            var ret = new byte[count];
            ReadBytes(ret, count);

            return ret;
        }

        public int ReadSignedInt(int bitCount)
        {
            BitStreamUtil.AssertMaxBits(32, bitCount);

            // Just like PeekInt, but before the right shift, cast to a signed long for sign extension.
            var result = (int)(
                (long)(
                    *(ulong*)(bufferPtr + ((offset >> 3) & ~3))
                    << (8 * 8 - (offset & (8 * 4 - 1)) - bitCount)
                ) >> (8 * 8 - bitCount)
            );

            Advance(bitCount);

            return result;
        }

        public float ReadFloat()
        {
            uint iResult = ReadInt(32);

            return *(float*)&iResult; // Standard reinterpret cast.
        }

        public byte[] ReadBits(int count)
        {
            // Shifting right by 3 is a division by 8 to convert bits to bytes.
            // Allocate space for an extra byte to store the data that's past the byte boundary.
            byte[] result = new byte[(count + 7) >> 3];
            ReadBytes(result, count >> 3);

            // AND with 7 (modulo 8) to get the amount of bits past the byte boundary.
            // If there are extra bits past the boundary, read them as a byte and add it to the end of the array.
            if ((count & 7) != 0)
                result[count >> 3] = ReadByte(count & 7);

            return result;
        }

        public int ReadProtobufVarInt()
        {
            // Only used for debug assertions.
            var availableBits = bufferedBits + SLED_SIZE * 8 - offset;

            // Start by overflowingly reading 32 bits.
            // Reading beyond the buffer contents is safe in this case,
            // because the sled ensures that we stay inside of the buffer.
            uint buf = PeekInt(32, true);

            // Always take the first byte; read the rest if necessary.
            // Normally, the least significant group of 7 bits is first.
            // However, PeekInt already returns these groups in reverse.
            uint result = buf & MASK_1;
            BitStreamUtil.AssertMaxBits(availableBits, 1 * 8);

            // The MSB at every byte boundary will be set if there are more bytes that need to be decoded.
            if ((buf & MSB_1) != 0)
            {
                // Right shift to get rid of the MSBs from the previous bytes (in this case only 1 previous byte).
                result |= (buf & MASK_2) >> 1;
                BitStreamUtil.AssertMaxBits(availableBits, 1 * 8);

                if ((buf & MSB_2) != 0)
                {
                    result |= (buf & MASK_3) >> 2;
                    BitStreamUtil.AssertMaxBits(availableBits, 2 * 8);

                    if ((buf & MSB_3) != 0)
                    {
                        result |= (buf & MASK_4) >> 3;
                        BitStreamUtil.AssertMaxBits(availableBits, 3 * 8);

                        if ((buf & MSB_4) != 0)
                        {
                            // Unfortunately, it's larger than 4 bytes (it's probably a negative integer). That's rare.
                            // This algorithm is limited to 4 bytes since the peek and masks are 32-bit integers.
                            // Fall back to the slow implementation.
                            return BitStreamUtil.ReadProtobufVarIntStub(this);
                        }
                        else
                        {
                            Advance(4 * 8);
                        }
                    }
                    else
                    {
                        Advance(3 * 8);
                    }
                }
                else
                {
                    Advance(2 * 8);
                }
            }
            else
            {
                Advance(1 * 8);
            }

            return unchecked((int)result);
        }

        public void BeginChunk(int length)
        {
            chunkTargets.Push(GlobalPosition + length);
        }

        public void EndChunk()
        {
            // To provide at least a little (and cheap) bit of sanity even
            // when performance is of utmost importance, this implementation
            // chooses a nice tradeoff: Unlike the BitArrayStream, it lets you
            // read beyond chunk boundaries. Here, we have to calculate the
            // number of read bits anyways so we know how much we need to skip,
            // so we might as well verify that this difference isn't negative.
            var target = chunkTargets.Pop();

            // How many bits remain ahead of the current position until the target is reached.
            var delta = checked((int)(target - GlobalPosition));

            if (delta < 0)
            {
                throw new InvalidOperationException("Someone read beyond a chunk boundary");
            }
            else if (delta > 0)
            {
                // Prefer seeking to skip to the target if the stream supports seeking.
                if (stream.CanSeek)
                {
                    // Number of bits in the buffer that have not been consumed yet.
                    // Subtract the offset because all bits up to the offset have already been consumed.
                    int bufferedBitsToSkip = bufferedBits - offset + (SLED_SIZE * 8);

                    // The number of bits in the buffer is less than the number
                    // of bits that need to be skipped to reach the end of the chunk.
                    // As mentioned above, the sled (converted to bits) needs to be added to get the true value.
                    if (bufferedBitsToSkip < delta)
                    {
                        if (reachedEnd)
                            throw new EndOfStreamException();

                        // Number of bits between the last valid bit in the buffer and the target.
                        // The shr by 3 is a division by 8 to convert bits to bytes.
                        int unbufferedBitsToSkip = delta - bufferedBitsToSkip;
                        stream.Seek(unbufferedBitsToSkip >> 3, SeekOrigin.Current);

                        // Unlike RefillBuffer, which reads 4 bytes,
                        // this reads 8 because the sled also has to be populated.
                        int readOffset, bytesRead = 1337; // I'll cry if this ends up in the generated code
                        for (readOffset = 0; readOffset < 8 && bytesRead != 0; readOffset += bytesRead)
                            bytesRead = stream.Read(buffer, readOffset, BUFFER_SIZE - readOffset);

                        // Don't count the sled bytes towards the amount of bits in the buffer.
                        // The reason it has to be done here but not in RefillBuffer is that
                        // this had to fill an empty sled while the latter did not.
                        bufferedBits = 8 * (readOffset - SLED_SIZE);

                        if (bytesRead == 0)
                        {
                            // The sled can be consumed now since the end of the stream was reached.
                            bufferedBits += SLED_SIZE * 8;
                            reachedEnd = true;
                        }

                        // Modulo 8 because the stream can only seek to a byte boundary;
                        // there may be up to 7 more bits that need to be skipped.
                        offset = unbufferedBitsToSkip & 7;
                        globalStartPosition = target - offset;
                    }
                    else
                    {
                        // The target is within the range of valid bits in the buffer. However, a refill may be required
                        // if there are 4 or fewer bytes remaining in the buffer after the target.
                        Advance(delta);
                    }
                }
                else
                {
                    // The stream doesn't support seeking unfortunately; read and discard instead.
                    Advance(delta);
                }
            }
        }

        public bool ChunkFinished => chunkTargets.Peek() == GlobalPosition;

        ~UnsafeBitStream()
        {
            Dispose();
        }

        /// <summary>
        /// Frees <see cref="bufferHandle"/>, the handle to the <see cref="buffer"/>.
        /// </summary>
        private void Dispose()
        {
            var nullptr = (byte*)IntPtr.Zero.ToPointer();

            if (bufferPtr != nullptr)
            {
                // GCHandle docs state that Free() must only be called once.
                // So we use bufferPtr to ensure that.
                bufferPtr = nullptr;
                bufferHandle.Free();
            }
        }

        /// <summary>
        /// Advances the cursor by <paramref name="count"/> bits and refills the buffer as necessary.
        /// Adds <paramref name="count"/> to the current buffer <see cref="offset"/>.
        /// </summary>
        /// <remarks>
        /// Note that <see cref="offset"/> is relative to the start of the sled rather than the end of the sled. If the
        /// offset is allowed to reach and consume the last <see cref="SLED_SIZE"/> bytes of the <see cref="buffer"/>,
        /// the next <see cref="RefillBuffer"/> call will copy already-consumed bytes into the sled. Therefore, a refill
        /// is required when there are <see cref="SLED_SIZE"/> or less bytes remaining after the advanced position.
        /// </remarks>
        /// <param name="count">The number of bits by which to advance the cursor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Advance(int count)
        {
            if ((offset += count) >= bufferedBits)
                RefillBuffer();
        }

        /// <summary>
        /// Fills the <see cref="buffer"/> with data from the <see cref="stream"/> until the bit pointed to
        /// by the <see cref="offset"/> is within the buffer.
        /// </summary>
        /// <remarks>
        /// Copies the last <see cref="SLED_SIZE"/> bytes into the first four bytes of the <see cref="buffer"/>, called
        /// the "sled", before overwriting the buffer with new data. The sled should always contain unconsumed bits.
        /// Furthermore, the buffer should always contain at least <see cref="SLED_SIZE"/> bytes in addition to the
        /// bytes in the sled. Maintaining a sled means that the buffer guarantees a certain amount of bits will always
        /// be available for a read.
        /// </remarks>
        /// <exception cref="EndOfStreamException">The <see cref="stream"/> has no more data.</exception>
        /// <seealso cref="Advance"/>
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
                if (reachedEnd)
                {
                    if (bufferedBits == 0)
                        throw new EndOfStreamException();

                    // Another late overrun detection:
                    // Offset SHOULD be < 0 after this.
                    // So Offset < BitsInBuffer.
                    // So we don't loop again.
                    // If it's not, we'll loop again which is exactly what we want
                    // as we overran the stream and wanna hit the throw above.
                    offset -= bufferedBits + 1;
                    globalStartPosition += bufferedBits + 1;
                    *(uint*)bufferPtr = 0; // safety
                    bufferedBits = 0;
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
                *(uint*)bufferPtr = *(uint*)(bufferPtr + (bufferedBits >> 3));

                // The reads below overwrite the current bits in the buffer, therefore advancing the buffer forward.
                offset -= bufferedBits; // Offset moves backward (moving the buffer forward makes the target closer).
                globalStartPosition += bufferedBits; // Start position of the buffer moves forward.

                // Read until at least 4 bytes are read or the end of the stream is reached (when bytesRead == 0).
                // It needs at least 4 bytes because that's the sled size, and subsequent refills expect to be able
                // to refill the sled.
                //
                // Read() does not guarantee a minimum number of bytes read, only a maximum. Therefore, it may take
                // multiple reads to read 4 bytes, despite the number of bytes requested being much larger than 4.
                //
                // The buffer starts being filled past the sled, which are be the first four bytes in the buffer.
                // Each read, the offset is updated with the number of bytes read.
                int readOffset, bytesRead = 1337; // I'll cry if this ends up in the generated code
                for (readOffset = 0; readOffset < 4 && bytesRead != 0; readOffset += bytesRead)
                    bytesRead = stream.Read(buffer, SLED_SIZE + readOffset, BUFFER_SIZE - SLED_SIZE - readOffset);

                // readOffset is the sum of the bytes read from all Read() calls in the loop above.
                bufferedBits = 8 * readOffset;

                // 0 bytes read means the end of the stream was reached.
                if (bytesRead == 0)
                {
                    // The last 4 bytes are normally reserved to be copied over to the front by the next refill.
                    // However, since the end of the stream was reached, these last bytes no longer need to be reserved.
                    // Therefore, count the sled bits towards the total number of bits in the buffer.
                    // Advance will now refill now if the new position has <= 4 bytes after it in the buffer.
                    bufferedBits += SLED_SIZE * 8;
                    reachedEnd = true;
                }
            }
            // Keep reading until enough bits are read to reach the offset.
            // If the offset is too far ahead from the original position,
            // data at the start of the buffer will get discarded since it can't all fit.
            // See Advance for details on why this doesn't account for the size of the sled.
            while (offset >= bufferedBits);
        }

        /// <summary>
        /// Retrieves up to 32 bits from the <see cref="buffer"/>.
        /// </summary>
        /// <param name="bitCount">The number of bits to read, up to a maximum of 32.</param>
        /// <param name="mayOverflow">
        /// <c>true</c> if more bits can be read than are available in the <see cref="buffer"/>.
        /// This only affects a debug assertion.
        /// </param>
        /// <returns>The bits read in the form of an unsigned integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint PeekInt(int bitCount, bool mayOverflow = false)
        {
            BitStreamUtil.AssertMaxBits(32, bitCount);
            #if CHECK_INTEGRITY
            Debug.Assert(
                mayOverflow || offset + bitCount <= bufferedBits + SLED_SIZE * 8,
                "gg",
                "This code just fell apart. We're all dead. offset={0} bitCount={1} bufferedBits={2}",
                offset,
                bitCount,
                bufferedBits
            );
            #endif

            // Summary:
            // Read 8 bytes after aligning the offset to a 4-byte boundary.
            // Shift left to discard bits from bitCount + 1 to 32 & MSBs that are past the non-aligned offset + 4 bytes.
            // Shift back to the right to discard the LSBs that are before the non-aligned offset.
            return (uint)(
                (
                    // Read 8 bytes (64 bits) and dereference into a ulong.
                    // It's a ulong so there's extra space to shift to the left later.
                    *(ulong*)(
                        bufferPtr + (
                            // Shifting to the left by 3 divides by 8 to convert the offset from bits to bytes.
                            (offset >> 3)
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
                        - (offset & (8 * 4 - 1))
                        // Lastly, subtract the number of bits to read.
                        // Any bits between bitCount + 1 and 32 will also get discarded.
                        - bitCount
                    )
                )
                // Shift back to the right.
                // This time, don't include the alignment remainder.
                // The least significant bytes will be discarded e.g. bytes 56 and 57 when offset is 58 bytes.
                >> (8 * 8 - bitCount)
            );
        }

        /// <summary>
        /// Reads bytes into <paramref name="outBuffer"/>.
        /// </summary>
        /// <param name="outBuffer">The buffer to read the bytes into.</param>
        /// <param name="count">Amount of bytes to read.</param>
        private void ReadBytes(byte[] outBuffer, int count)
        {
            if (count < 3)
            {
                for (int i = 0; i < count; i++)
                    outBuffer[i] = ReadByte();
            }
            else if ((offset & 7) == 0) // Modulo 8; no remainder means the offset is aligned to a byte boundary.
            {
                int outOffset = 0; // Amount of bytes that have been copied.

                while (outOffset < count)
                {
                    // Read until the end of the input buffer if the remaining amount to read exceeds the buffer.
                    // Note: Offset is never greater than BitsInBuffer (otherwise the buffer would've been refilled).
                    int remainingBytes = Math.Min((bufferedBits - offset) >> 3, count - outOffset);
                    Buffer.BlockCopy(buffer, offset >> 3, outBuffer, outOffset, remainingBytes);
                    outOffset += remainingBytes;

                    Advance(remainingBytes * 8);
                }
            }
            else
            {
                // Offset is not aligned, so use CopyBytes because it can handle misalignment.
                fixed (byte* outPtr = outBuffer)
                {
                    int outOffset = 0;

                    while (outOffset < count)
                    {
                        int remainingBytes = Math.Min(((bufferedBits - offset) >> 3) + 1, count - outOffset);
                        CopyBytes(outPtr + outOffset, remainingBytes);
                        outOffset += remainingBytes;

                        // CopyBytes takes care of refilling the buffer as necessary.
                    }
                }
            }
        }

        /// <summary>
        /// Copies bytes from <see cref="buffer"/> to <paramref name="outBuffer"/>,
        /// supporting a misaligned <see cref="offset"/>.
        /// </summary>
        /// <remarks>
        /// This is probably the most significant unsafe speedup: copying about 64 bits at a time instead of 8 bits.
        /// </remarks>
        /// <param name="outBuffer">Pointer to the buffer to copy the bytes into.</param>
        /// <param name="count">Amount of bytes to copy.</param>
        private void CopyBytes(byte* outBuffer, int count)
        {
            // Begin by aligning to the first byte.
            // These values will make more sense after looking at the loop below.
            int misalign = 8 - (offset & 7); // Modulo 8; calculate the amount of bits until the *next* boundary.
            int realign = sizeof(ulong) * 8 - misalign; // How many bits from the current value will be copied.
            ulong step = ReadByte(misalign); // Read until the next byte boundary is reached.

            // Pointers are ulongs instead of bytes to allow copying 8 bytes at a time.
            var inPtr = (ulong*)(bufferPtr + (offset >> 3)); // Pointer to input buffer, starting at the aligned offset.
            var outPtr = (ulong*)outBuffer;

            // Main loop - copy 8 bytes at a time.
            // Subtract 1 byte because it was already read above (exactly 1 byte was read if it was already aligned).
            for (int i = 0; i < (count - 1) / sizeof(ulong); i++)
            {
                // Get 8 bytes and advance the input pointer.
                ulong current = *inPtr++;

                // LSBs in `step` contain the remaining bytes from the previous read that are nonaligned.
                // Shift the current value past those nonaligned bytes. The MSBs of `current` are truncated.
                step |= current << misalign;

                // Copy the 8 bytes into the output and advance the output pointer.
                *outPtr++ = step;

                // Store the MSBs that were truncated above. They'll be copied in the next iteration.
                step = current >> realign;
            }

            // Now process the rest. They're not aligned to an 8-byte boundary, so they have to be read 1 at a time.
            int rest = (count - 1) % sizeof(ulong); // Amount of bytes not copied by the loop above.
            offset += (count - rest - 1) * 8; // Adjust the offset to account for the bytes read above.

            // Output pointer as bytes, starting at the position the loop left it at.
            var outBytePtr = (byte*)outPtr;

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
            outBytePtr[0] = (byte)((ReadInt(8 - misalign) << misalign) | step);

            // Now it's aligned. Simply read the remaining bytes 1-by-1 into the output buffer.
            for (int i = 1; i < rest + 1; i++)
                outBytePtr[i] |= ReadByte();
        }
    }
}
