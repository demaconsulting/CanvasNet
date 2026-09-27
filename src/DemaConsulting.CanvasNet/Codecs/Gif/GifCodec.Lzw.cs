namespace DemaConsulting.CanvasNet.Codecs;

public static partial class GifCodec
{
    /// <summary>
    ///     Decodes a GIF-flavor LZW byte stream (variable-width codes packed
    ///     least-significant-bit-first, a file-declared minimum code size, Clear code =
    ///     <c>1 &lt;&lt; minCodeSize</c>, end-of-information code = Clear code + 1, standard
    ///     non-early-change code-width growth) into exactly <paramref name="expectedIndexCount"/>
    ///     palette-index bytes.
    /// </summary>
    /// <param name="data">The concatenated compressed image data (from <see cref="ReadSubBlocks"/>).</param>
    /// <param name="minCodeSize">
    ///     The minimum LZW code size, as declared by the byte immediately preceding the image's
    ///     sub-block chain. Must be between 2 and 8 inclusive.
    /// </param>
    /// <param name="expectedIndexCount">The exact number of palette-index bytes to produce.</param>
    /// <returns>An array of exactly <paramref name="expectedIndexCount"/> palette-index bytes.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="minCodeSize"/> is out of range, the stream does not start
    ///     with a Clear code, an invalid or out-of-range code is encountered, decoding would
    ///     produce more than <paramref name="expectedIndexCount"/> bytes, or the stream ends
    ///     before both exactly <paramref name="expectedIndexCount"/> bytes have been produced and
    ///     an end-of-information code has been read.
    /// </exception>
    /// <remarks>
    ///     Decoding always continues until an end-of-information code is read - it never stops
    ///     merely because <paramref name="expectedIndexCount"/> bytes have already been produced -
    ///     and any code that would decode past exactly that many bytes is rejected immediately,
    ///     rather than silently truncated. This ensures a compressed stream that omits the
    ///     required end-of-information code, or that decodes to a different pixel count than the
    ///     Image Descriptor declared, is always treated as malformed input.
    /// </remarks>
    private static byte[] DecodeGifLzw(byte[] data, int minCodeSize, int expectedIndexCount)
    {
        if (minCodeSize is < 2 or > 8)
        {
            throw new InvalidDataException($"Invalid GIF LZW minimum code size {minCodeSize}.");
        }

        var clearCode = 1 << minCodeSize;
        var eoiCode = clearCode + 1;
        var firstAvailableCode = eoiCode + 1;

        var reader = new GifLzwBitReader(data);
        var output = new byte[expectedIndexCount];
        var outputCount = 0;

        var codeSize = minCodeSize + 1;
        var table = new List<byte[]>();
        byte[]? previousEntry = null;

        var firstCode = reader.ReadCode(codeSize);
        if (firstCode != clearCode)
        {
            throw new InvalidDataException("GIF LZW stream does not start with a Clear code.");
        }

        while (true)
        {
            var code = reader.ReadCode(codeSize);
            if (code == eoiCode)
            {
                break;
            }

            if (code == clearCode)
            {
                table.Clear();
                codeSize = minCodeSize + 1;
                previousEntry = null;
                continue;
            }

            var entry = ResolveGifLzwEntry(code, clearCode, firstAvailableCode, table, previousEntry);

            if (outputCount + entry.Length > expectedIndexCount)
            {
                throw new InvalidDataException(
                    "GIF LZW stream decoded more palette-index bytes than the Image Descriptor declared.");
            }

            Array.Copy(entry, 0, output, outputCount, entry.Length);
            outputCount += entry.Length;

            if (previousEntry is not null && firstAvailableCode + table.Count < 4096)
            {
                var newEntry = new byte[previousEntry.Length + 1];
                previousEntry.CopyTo(newEntry, 0);
                newEntry[^1] = entry[0];
                table.Add(newEntry);

                var nextCode = firstAvailableCode + table.Count;
                if (nextCode == 1 << codeSize && codeSize < 12)
                {
                    codeSize++;
                }
            }

            previousEntry = entry;
        }

        if (outputCount != expectedIndexCount)
        {
            throw new InvalidDataException("Truncated GIF LZW stream (insufficient decoded pixel data).");
        }

        return output;
    }

    /// <summary>
    ///     Resolves the byte sequence for a single decoded GIF LZW <paramref name="code"/>: a
    ///     literal single-index byte for codes below <paramref name="clearCode"/>, an existing
    ///     table entry for already-known codes, or the classic LZW "KwKwK" reconstruction
    ///     (previous entry plus its own first byte) for the one code that is always exactly one
    ///     past the current table end.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="code"/> is invalid.</exception>
    private static byte[] ResolveGifLzwEntry(
        int code,
        int clearCode,
        int firstAvailableCode,
        List<byte[]> table,
        byte[]? previousEntry)
    {
        if (code < clearCode)
        {
            return [(byte)code];
        }

        if (code - firstAvailableCode < table.Count)
        {
            return table[code - firstAvailableCode];
        }

        if (code - firstAvailableCode == table.Count && previousEntry is not null)
        {
            var entry = new byte[previousEntry.Length + 1];
            previousEntry.CopyTo(entry, 0);
            entry[^1] = previousEntry[0];
            return entry;
        }

        throw new InvalidDataException("Invalid GIF LZW code sequence.");
    }

    /// <summary>
    ///     Unpacks variable-width LZW codes from bytes packed least-significant-bit-first, as
    ///     required by the GIF specification (the reverse bit order from the TIFF LZW variant -
    ///     see <see cref="TiffCodec"/>'s own <c>LzwBitReader</c> remarks).
    /// </summary>
    private sealed class GifLzwBitReader(byte[] data)
    {
        private int _bytePos;
        private uint _bitBuffer;
        private int _bitCount;

        public int ReadCode(int bits)
        {
            while (_bitCount < bits)
            {
                if (_bytePos >= data.Length)
                {
                    throw new InvalidDataException(
                        "Truncated GIF LZW stream (missing end-of-information code).");
                }

                _bitBuffer |= (uint)data[_bytePos] << _bitCount;
                _bytePos++;
                _bitCount += 8;
            }

            var code = (int)(_bitBuffer & ((1u << bits) - 1));
            _bitBuffer >>= bits;
            _bitCount -= bits;
            return code;
        }
    }
}
