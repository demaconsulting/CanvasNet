using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class JpegCodec
{
    // ================================================================================================
    // Shared Huffman table construction (used by both the decoder's DHT tables and the encoder's
    // Annex K default tables)
    // ================================================================================================

    /// <summary>
    ///     A canonical Huffman decode table built from 16 code-length counts and a flat symbol
    ///     list, using the min-code/max-code/val-pointer arrays described in ITU-T T.81 Annex F.
    /// </summary>
    private sealed class HuffmanTable
    {
        public readonly int[] MinCode = new int[17];
        public readonly int[] MaxCode = new int[17];
        public readonly int[] ValPtr = new int[17];
        public required byte[] Values;

        /// <summary>
        ///     Builds a canonical Huffman table from 16 code-length counts (<paramref name="bits"/>,
        ///     indices 0-15 correspond to code lengths 1-16) and the flat symbol list in code order.
        /// </summary>
        public static HuffmanTable Build(byte[] bits, byte[] values)
        {
            var table = new HuffmanTable { Values = values };
            for (var length = 1; length <= 16; length++)
            {
                table.MaxCode[length] = -1;
            }

            var code = 0;
            var pointer = 0;
            for (var length = 1; length <= 16; length++)
            {
                var count = bits[length - 1];
                if (count > 0)
                {
                    table.ValPtr[length] = pointer;
                    table.MinCode[length] = code;
                    code += count;
                    pointer += count;
                    table.MaxCode[length] = code - 1;
                }

                code <<= 1;
            }

            return table;
        }
    }

    /// <summary>
    ///     Reads bits MSB-first from a JPEG entropy-coded data region, transparently removing
    ///     byte-stuffing (<c>0xFF 0x00</c> -&gt; literal <c>0xFF</c>) and throwing
    ///     <see cref="InvalidDataException"/> if an unstuffed marker is encountered where entropy
    ///     data was expected.
    /// </summary>
    private sealed class BitReader(byte[] data, int start)
    {
        private int _bitBuffer;
        private int _bitCount;

        public int Position { get; private set; } = start;

        public int ReadBit()
        {
            if (_bitCount == 0)
            {
                if (Position >= data.Length)
                {
                    throw new InvalidDataException("Unexpected end of stream while reading JPEG entropy-coded data.");
                }

                var b = data[Position++];
                if (b == MarkerPrefix)
                {
                    if (Position >= data.Length || data[Position] != 0x00)
                    {
                        throw new InvalidDataException("Unexpected marker encountered while reading JPEG entropy-coded data.");
                    }

                    Position++;
                }

                _bitBuffer = b;
                _bitCount = 8;
            }

            _bitCount--;
            return (_bitBuffer >> _bitCount) & 1;
        }

        public int ReadBits(int count)
        {
            var value = 0;
            for (var i = 0; i < count; i++)
            {
                value = (value << 1) | ReadBit();
            }

            return value;
        }

        /// <summary>
        ///     Discards any unread bits remaining in the current byte, aligning the reader to the
        ///     next byte boundary (used before expecting a restart marker).
        /// </summary>
        public void Realign() => _bitCount = 0;

        /// <summary>
        ///     Consumes an expected restart marker (<c>0xFFD0</c>-<c>0xFFD7</c>) at the current byte
        ///     position, throwing <see cref="InvalidDataException"/> if one is not present.
        /// </summary>
        public void ExpectRestartMarker()
        {
            if (Position + 1 >= data.Length || data[Position] != MarkerPrefix ||
                data[Position + 1] < MarkerRst0 || data[Position + 1] > MarkerRst7)
            {
                throw new InvalidDataException("Expected a JPEG restart marker but did not find one.");
            }

            Position += 2;
        }

        public static int Decode(HuffmanTable table, BitReader reader)
        {
            var code = reader.ReadBit();
            var length = 1;
            while (code > table.MaxCode[length])
            {
                code = (code << 1) | reader.ReadBit();
                length++;
                if (length > 16)
                {
                    throw new InvalidDataException("Invalid JPEG Huffman code encountered.");
                }
            }

            return table.Values[table.ValPtr[length] + (code - table.MinCode[length])];
        }

        /// <summary>
        ///     Implements the standard JPEG "EXTEND" procedure (ITU-T T.81 Annex F.2.2.1): reads
        ///     <paramref name="size"/> magnitude bits and sign-extends them to a signed value.
        /// </summary>
        public int Receive(int size)
        {
            if (size == 0)
            {
                return 0;
            }

            var value = ReadBits(size);
            var threshold = 1 << (size - 1);
            return value < threshold ? value - (1 << size) + 1 : value;
        }
    }

    /// <summary>
    ///     Writes bits MSB-first to a JPEG entropy-coded data region, transparently applying
    ///     byte-stuffing (<c>0xFF</c> -&gt; <c>0xFF 0x00</c>).
    /// </summary>
    private sealed class BitWriter(Stream stream)
    {
        private int _bitBuffer;
        private int _bitCount;

        public void WriteBits(int value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                WriteBit((value >> i) & 1);
            }
        }

        private void WriteBit(int bit)
        {
            _bitBuffer = (_bitBuffer << 1) | bit;
            _bitCount++;
            if (_bitCount == 8)
            {
                FlushByte();
            }
        }

        private void FlushByte()
        {
            var b = (byte)_bitBuffer;
            stream.WriteByte(b);
            if (b == MarkerPrefix)
            {
                stream.WriteByte(0x00);
            }

            _bitBuffer = 0;
            _bitCount = 0;
        }

        /// <summary>
        ///     Pads the current partial byte with 1-bits and flushes it, as required before
        ///     writing a marker (ITU-T T.81 Section F.1.2.3).
        /// </summary>
        public void FlushWithPadding()
        {
            while (_bitCount != 0)
            {
                WriteBit(1);
            }
        }
    }

}
