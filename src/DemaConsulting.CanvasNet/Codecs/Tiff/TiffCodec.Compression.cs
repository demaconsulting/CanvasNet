namespace DemaConsulting.CanvasNet.Codecs;

public static partial class TiffCodec
{
    /// <summary>
    ///     Reverses the TIFF horizontal-differencing predictor (Predictor tag value 2) on one
    ///     already-decompressed row, in place: each sample becomes the running sum of itself and
    ///     the sample <paramref name="samplesPerPixel"/> positions to its left in the same row.
    /// </summary>
    private static void RemoveHorizontalPredictor(Span<byte> row, int samplesPerPixel)
    {
        for (var i = samplesPerPixel; i < row.Length; i++)
        {
            row[i] = (byte)(row[i] + row[i - samplesPerPixel]);
        }
    }

    /// <summary>
    ///     Applies the TIFF horizontal-differencing predictor (Predictor tag value 2) to one row,
    ///     in place, before compression: each sample becomes the difference between itself and the
    ///     sample <paramref name="samplesPerPixel"/> positions to its left in the same row.
    /// </summary>
    private static void ApplyHorizontalPredictor(Span<byte> row, int samplesPerPixel)
    {
        for (var i = row.Length - 1; i >= samplesPerPixel; i--)
        {
            row[i] = (byte)(row[i] - row[i - samplesPerPixel]);
        }
    }

    /// <summary>
    ///     Decodes a PackBits (TIFF 6.0 Section 9) run-length encoded byte stream.
    /// </summary>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when a literal or repeat run's data extends past the end of <paramref name="data"/>.
    /// </exception>
    private static byte[] DecodePackBits(byte[] data)
    {
        using var output = new MemoryStream();
        var i = 0;
        while (i < data.Length)
        {
            var control = unchecked((sbyte)data[i]);
            i++;
            if (control >= 0)
            {
                var count = control + 1;
                if (i + count > data.Length)
                {
                    throw new InvalidDataException("Truncated PackBits literal run.");
                }

                output.Write(data, i, count);
                i += count;
            }
            else if (control != -128)
            {
                var count = -control + 1;
                if (i >= data.Length)
                {
                    throw new InvalidDataException("Truncated PackBits repeat run.");
                }

                var value = data[i];
                i++;
                for (var j = 0; j < count; j++)
                {
                    output.WriteByte(value);
                }
            }

            // control == -128 is a documented no-op and is simply skipped
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Encodes a byte stream using PackBits (TIFF 6.0 Section 9) run-length encoding,
    ///     preferring a repeat run for any sequence of 2 or more identical bytes and otherwise
    ///     emitting a literal run.
    /// </summary>
    private static byte[] EncodePackBits(byte[] data)
    {
        using var output = new MemoryStream();
        var i = 0;
        while (i < data.Length)
        {
            var runLength = FindPackBitsRunLength(data, i);

            if (runLength >= 2)
            {
                output.WriteByte(unchecked((byte)-(runLength - 1)));
                output.WriteByte(data[i]);
                i += runLength;
            }
            else
            {
                var start = i;
                var length = FindPackBitsLiteralLength(data, ref i);
                output.WriteByte((byte)(length - 1));
                output.Write(data, start, length);
            }
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Counts how many bytes starting at <paramref name="start"/> repeat the value at
    ///     <paramref name="start"/>, capped at the PackBits maximum run length of 128.
    /// </summary>
    private static int FindPackBitsRunLength(byte[] data, int start)
    {
        var runLength = 1;
        while (start + runLength < data.Length && runLength < 128 && data[start + runLength] == data[start])
        {
            runLength++;
        }

        return runLength;
    }

    /// <summary>
    ///     Advances <paramref name="i"/> past a run of non-repeating ("literal") bytes, stopping
    ///     as soon as a repeat run of 2 or more is found or the PackBits maximum literal length of
    ///     128 is reached, and returns the number of literal bytes found.
    /// </summary>
    private static int FindPackBitsLiteralLength(byte[] data, ref int i)
    {
        var length = 0;
        while (i < data.Length && length < 128)
        {
            var lookaheadRun = FindPackBitsRunLength(data, i);
            if (lookaheadRun >= 2)
            {
                break;
            }

            i++;
            length++;
        }

        return length;
    }

    /// <summary>
    ///     Encodes a byte stream using the TIFF-flavor LZW algorithm (TIFF 6.0 Section 13):
    ///     variable-width 9-12 bit codes, MSB-first bit packing, clear code 256, end-of-information
    ///     code 257.
    /// </summary>
    private static byte[] EncodeLzw(byte[] data)
    {
        var writer = new LzwBitWriter();
        var codeSize = 9;
        var nextCode = LzwFirstCode;
        var table = new Dictionary<(int Prefix, byte Next), int>();

        writer.WriteCode(LzwClearCode, codeSize);

        if (data.Length == 0)
        {
            writer.WriteCode(LzwEoiCode, codeSize);
            return writer.ToArray();
        }

        var prefixCode = (int)data[0];
        for (var i = 1; i < data.Length; i++)
        {
            var next = data[i];
            if (table.TryGetValue((prefixCode, next), out var existingCode))
            {
                prefixCode = existingCode;
                continue;
            }

            writer.WriteCode(prefixCode, codeSize);
            table[(prefixCode, next)] = nextCode;
            nextCode++;

            if (nextCode is 511 or 1023 or 2047)
            {
                codeSize++;
            }

            if (nextCode >= LzwMaxCode)
            {
                writer.WriteCode(LzwClearCode, codeSize);
                table.Clear();
                nextCode = LzwFirstCode;
                codeSize = 9;
            }

            prefixCode = next;
        }

        writer.WriteCode(prefixCode, codeSize);
        writer.WriteCode(LzwEoiCode, codeSize);
        return writer.ToArray();
    }

    /// <summary>
    ///     Decodes a TIFF-flavor LZW (TIFF 6.0 Section 13) byte stream, mirroring the table
    ///     construction of <see cref="EncodeLzw"/> exactly (variable-width 9-12 bit codes,
    ///     MSB-first bit packing, clear code 256, end-of-information code 257).
    /// </summary>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream does not begin with a Clear code, an invalid code is
    ///     encountered, or the stream ends before an end-of-information code is read.
    /// </exception>
    private static byte[] DecodeLzw(byte[] data)
    {
        var reader = new LzwBitReader(data);
        using var output = new MemoryStream();
        var codeSize = 9;
        var table = new List<byte[]>();
        byte[]? previousEntry = null;

        var firstCode = reader.ReadCode(codeSize);
        if (firstCode != LzwClearCode)
        {
            throw new InvalidDataException("TIFF LZW stream does not start with a Clear code.");
        }

        while (true)
        {
            var code = reader.ReadCode(codeSize);
            if (code == LzwEoiCode)
            {
                break;
            }

            if (code == LzwClearCode)
            {
                table.Clear();
                codeSize = 9;
                previousEntry = null;
                continue;
            }

            var entry = ResolveLzwEntry(code, table, previousEntry);
            output.Write(entry, 0, entry.Length);

            if (previousEntry is not null)
            {
                codeSize = AddLzwTableEntry(table, previousEntry, entry, codeSize);
            }

            previousEntry = entry;
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Resolves the byte sequence for a single decoded LZW <paramref name="code"/>: a literal
    ///     byte value for codes below 256, an existing table entry for already-known codes, or
    ///     the classic LZW "KwKwK" reconstruction (previous entry plus its own first byte) for
    ///     the one code that is always exactly one past the current table end.
    /// </summary>
    /// <exception cref="System.IO.InvalidDataException">Thrown when <paramref name="code"/> is invalid.</exception>
    private static byte[] ResolveLzwEntry(int code, List<byte[]> table, byte[]? previousEntry)
    {
        if (code < 256)
        {
            return [(byte)code];
        }

        if (code - LzwFirstCode < table.Count)
        {
            return table[code - LzwFirstCode];
        }

        if (code - LzwFirstCode == table.Count && previousEntry is not null)
        {
            var entry = new byte[previousEntry.Length + 1];
            previousEntry.CopyTo(entry, 0);
            entry[^1] = previousEntry[0];
            return entry;
        }

        throw new InvalidDataException("Invalid TIFF LZW code sequence.");
    }

    /// <summary>
    ///     Appends a new table entry formed from <paramref name="previousEntry"/> plus the first
    ///     byte of <paramref name="entry"/>, then widens <paramref name="codeSize"/> if the table
    ///     has just grown past a code-width boundary, returning the (possibly updated) code size.
    /// </summary>
    private static int AddLzwTableEntry(List<byte[]> table, byte[] previousEntry, byte[] entry, int codeSize)
    {
        var newEntry = new byte[previousEntry.Length + 1];
        previousEntry.CopyTo(newEntry, 0);
        newEntry[^1] = entry[0];
        table.Add(newEntry);

        var nextCode = LzwFirstCode + table.Count;
        if (nextCode is 511 or 1023 or 2047)
        {
            codeSize++;
        }

        return codeSize;
    }

    /// <summary>
    ///     Packs variable-width (9-12 bit) LZW codes into bytes MSB-first, as required by the
    ///     TIFF specification (the reverse bit order from the GIF LZW variant).
    /// </summary>
    private sealed class LzwBitWriter
    {
        private readonly List<byte> _bytes = [];
        private ulong _bitBuffer;
        private int _bitCount;

        public void WriteCode(int code, int bits)
        {
            _bitBuffer = (_bitBuffer << bits) | (uint)(code & ((1 << bits) - 1));
            _bitCount += bits;
            while (_bitCount >= 8)
            {
                _bitCount -= 8;
                _bytes.Add((byte)((_bitBuffer >> _bitCount) & 0xFF));
            }

            _bitBuffer &= (1UL << _bitCount) - 1;
        }

        public byte[] ToArray()
        {
            if (_bitCount > 0)
            {
                var pad = 8 - _bitCount;
                _bytes.Add((byte)((_bitBuffer << pad) & 0xFF));
            }

            return [.. _bytes];
        }
    }

    /// <summary>
    ///     Unpacks variable-width (9-12 bit) LZW codes from bytes packed MSB-first, as required by
    ///     the TIFF specification.
    /// </summary>
    private sealed class LzwBitReader(byte[] data)
    {
        private int _bytePos;
        private ulong _bitBuffer;
        private int _bitCount;

        public int ReadCode(int bits)
        {
            while (_bitCount < bits)
            {
                if (_bytePos >= data.Length)
                {
                    throw new InvalidDataException("Truncated TIFF LZW stream (missing end-of-information code).");
                }

                _bitBuffer = (_bitBuffer << 8) | data[_bytePos];
                _bytePos++;
                _bitCount += 8;
            }

            _bitCount -= bits;
            return (int)((_bitBuffer >> _bitCount) & ((1UL << bits) - 1));
        }
    }
}
