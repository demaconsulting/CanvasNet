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
    /// <param name="data">The PackBits-encoded bytes.</param>
    /// <param name="maxOutputBytes">
    ///     The hard upper limit on the decoded size (the most the strip's declared dimensions could
    ///     need); a run that would push the output past it is rejected before it is written.
    /// </param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when a literal or repeat run's data extends past the end of <paramref name="data"/>,
    ///     or the decoded output would exceed <paramref name="maxOutputBytes"/>.
    /// </exception>
    private static byte[] DecodePackBits(byte[] data, long maxOutputBytes)
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

                CheckPackBitsOutputLimit(output.Length, count, maxOutputBytes);
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

                CheckPackBitsOutputLimit(output.Length, count, maxOutputBytes);
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
    ///     Throws when appending <paramref name="count"/> bytes to <paramref name="length"/> bytes
    ///     of already-decoded output would exceed <paramref name="maxOutputBytes"/>.
    /// </summary>
    private static void CheckPackBitsOutputLimit(long length, int count, long maxOutputBytes)
    {
        if (length + count > maxOutputBytes)
        {
            throw new InvalidDataException(
                $"Decoded TIFF PackBits data exceeds the expected size of {maxOutputBytes} bytes.");
        }
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
    /// <remarks>
    ///     TIFF 6.0 Section 13 applies a code-width "early change" convention to LZW
    ///     <em>decoders</em> only (widening one table entry sooner than a naive symmetric decoder
    ///     would, to compensate for the one-iteration lag inherent in how an LZW decoder registers
    ///     new table entries relative to the encoder). This encoder is the other half of that
    ///     asymmetric pair: it uses the plain, unadjusted thresholds (code size bumps immediately
    ///     after table entry 511/1023/2047 is created), matching the canonical libtiff
    ///     <c>LZWPreEncode</c>/<c>LZWEncode</c> behavior. This must stay numerically different from
    ///     the decoder's early-change thresholds (511/1023/2047, see <see cref="DecodeLzw"/>) - making them symmetric (either by adjusting
    ///     this encoder to match the decoder's "early" thresholds, or vice versa) breaks
    ///     interoperability with every TIFF-flavor LZW reader, including this library's own
    ///     <see cref="DecodeLzw"/> and third-party readers such as libtiff/Pillow.
    /// </remarks>
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

            if (nextCode is 512 or 1024 or 2048)
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
    ///     Decodes a TIFF-flavor LZW (TIFF 6.0 Section 13) byte stream (variable-width 9-12 bit
    ///     codes, MSB-first bit packing, clear code 256, end-of-information code 257) through the
    ///     shared <see cref="LzwDecoder"/>, requiring - as TIFF does - that the stream begin with
    ///     a Clear code.
    /// </summary>
    /// <remarks>
    ///     Per TIFF 6.0 Section 13's "early change" convention, the decoder widens the code size
    ///     one table entry <em>earlier</em> than <see cref="EncodeLzw"/> does (511/1023/2047 table
    ///     entries versus the encoder's plain 512/1024/2048), hence <c>earlyChange: true</c>. This
    ///     is intentional and required: an LZW decoder can only materialize a new table entry one
    ///     iteration after the encoder created the corresponding entry (it needs to see the next
    ///     code first), so decoding with the encoder's plain thresholds would read the widened
    ///     code one bit short. Widening one entry early exactly compensates for that lag so both
    ///     sides switch widths at the same absolute bit position in the stream. Do not change
    ///     these thresholds to match <see cref="EncodeLzw"/>'s plain/unadjusted thresholds -
    ///     doing so reintroduces this exact class of bug (bit stream misalignment and
    ///     <see cref="System.IO.InvalidDataException"/> on decode, or silent corruption).
    /// </remarks>
    /// <param name="data">The LZW-encoded bytes.</param>
    /// <param name="maxOutputBytes">
    ///     The hard upper limit on the decoded size (the most the strip's declared dimensions could
    ///     need); output past it is rejected as soon as it is produced.
    /// </param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream does not begin with a Clear code, an invalid code is
    ///     encountered, the stream ends before an end-of-information code is read, or the decoded
    ///     output would exceed <paramref name="maxOutputBytes"/>.
    /// </exception>
    private static byte[] DecodeLzw(byte[] data, long maxOutputBytes)
    {
        // The first 9-bit code must be Clear (the shared decoder treats it as optional).
        if (data.Length < 2 || ((data[0] << 1) | (data[1] >> 7)) != LzwClearCode)
        {
            throw new InvalidDataException("TIFF LZW stream does not start with a Clear code.");
        }

        return LzwDecoder.Decode(data, earlyChange: true, maxOutputBytes);
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
}
