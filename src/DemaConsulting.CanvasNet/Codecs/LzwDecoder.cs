namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Internal, output-bounded decoder for the MSB-first, variable-width (9-12 bit) LZW variant
///     used by TIFF (TIFF 6.0 section 13) and by the PDF <c>LZWDecode</c> filter (ISO 32000
///     section 7.4.4), shared by <see cref="TiffCodec"/> and, through the <c>InternalsVisibleTo</c>
///     grant, by the <c>DemaConsulting.CanvasNet.Pdf</c> package.
/// </summary>
/// <remarks>
///     <para>
///         The two formats use the same stream layout: an initial table of 258 entries (codes 0-255
///         are literal bytes, 256 is Clear, 257 is end-of-data, 258 is the first dynamically
///         assigned code), codes packed most-significant-bit first, the code width starting at 9
///         bits and growing to at most 12. They differ only in when the width grows. TIFF always
///         grows it "early" (one table entry sooner than a naive decoder would); PDF makes the
///         choice selectable through <c>/EarlyChange</c> (default early), so the shared decoder
///         takes it as a parameter and TIFF passes <see langword="true"/>.
///     </para>
///     <para>
///         The decoder is hard-bounded: a caller-supplied maximum output size is enforced before any
///         byte beyond it is written, and the output buffer grows on demand in proportion to the
///         bytes actually produced, so a tiny crafted stream whose table entries chain into an
///         enormous expansion (a decompression bomb) can never allocate more than the limit. It
///         is stateless and therefore thread-safe. This type is <c>internal</c>: it is not part of
///         the public API surface.
///     </para>
/// </remarks>
internal static class LzwDecoder
{
    /// <summary>The fixed Clear-table code.</summary>
    private const int ClearCode = 256;

    /// <summary>The fixed end-of-data code.</summary>
    private const int EndCode = 257;

    /// <summary>The first dynamically assigned table code.</summary>
    private const int FirstCode = 258;

    /// <summary>The maximum number of table entries (12-bit codes).</summary>
    private const int MaxTableSize = 4096;

    /// <summary>The initial (and post-Clear) code width, in bits.</summary>
    private const int InitialCodeWidth = 9;

    /// <summary>The maximum code width, in bits.</summary>
    private const int MaxCodeWidth = 12;

    /// <summary>The smallest initial output-buffer size, in bytes.</summary>
    private const int InitialBufferBytes = 4096;

    /// <summary>
    ///     Decodes an MSB-first LZW stream (initial Clear code optional) up to and including its
    ///     end-of-data code, refusing to produce more than <paramref name="maxOutputBytes"/> bytes.
    /// </summary>
    /// <param name="data">The LZW-encoded bytes.</param>
    /// <param name="earlyChange">
    ///     <see langword="true"/> to grow the code width one table entry earlier (TIFF; the PDF
    ///     default); <see langword="false"/> for the later PDF <c>/EarlyChange 0</c> timing.
    /// </param>
    /// <param name="maxOutputBytes">
    ///     The hard upper limit on the decoded size, in bytes. Output of exactly this size is
    ///     accepted; one byte more is rejected as soon as it would be produced. The output buffer
    ///     starts at <c>min(maxOutputBytes, max(4 KiB, 4 * data.Length))</c> bytes and doubles
    ///     (capped at <paramref name="maxOutputBytes"/>) only when it has really been filled.
    /// </param>
    /// <returns>The decoded bytes, no more than <paramref name="maxOutputBytes"/> long.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="maxOutputBytes"/> is negative or larger than the maximum
    ///     .NET array length, the bit stream ends before an end-of-data code is read, a code is
    ///     invalid for the current table state, or the decoded output would exceed
    ///     <paramref name="maxOutputBytes"/>.
    /// </exception>
    internal static byte[] Decode(ReadOnlySpan<byte> data, bool earlyChange, long maxOutputBytes)
    {
        if (maxOutputBytes < 0 || maxOutputBytes > Array.MaxLength)
        {
            throw new InvalidDataException($"The LZW output limit {maxOutputBytes} is not representable.");
        }

        var earlyChangeAmount = earlyChange ? 1 : 0;

        // Entry n (>= FirstCode) is the sequence of entry prefixOf[n] followed by suffixOf[n];
        // lengthOf[n] is its total length. Entries 0-255 are the single literal bytes.
        var prefixOf = new int[MaxTableSize];
        var suffixOf = new byte[MaxTableSize];
        var lengthOf = new int[MaxTableSize];
        for (var literal = 0; literal < 256; literal++)
        {
            suffixOf[literal] = (byte)literal;
            lengthOf[literal] = 1;
        }

        var output = new byte[(int)Math.Min(maxOutputBytes, Math.Max(InitialBufferBytes, data.Length * 4L))];
        var count = 0;

        var codeWidth = InitialCodeWidth;
        var maxCode = (1 << codeWidth) - 1 - earlyChangeAmount;
        var nextCode = FirstCode;
        var oldCode = -1;

        var bytePosition = 0;
        ulong bitBuffer = 0;
        var bitCount = 0;

        while (true)
        {
            while (bitCount < codeWidth)
            {
                if (bytePosition >= data.Length)
                {
                    throw new InvalidDataException("Truncated LZW stream: missing end-of-data code.");
                }

                bitBuffer = (bitBuffer << 8) | data[bytePosition++];
                bitCount += 8;
            }

            bitCount -= codeWidth;
            var code = (int)((bitBuffer >> bitCount) & ((1UL << codeWidth) - 1));
            bitBuffer &= (1UL << bitCount) - 1;

            if (code == EndCode)
            {
                break;
            }

            if (code == ClearCode)
            {
                codeWidth = InitialCodeWidth;
                maxCode = (1 << codeWidth) - 1 - earlyChangeAmount;
                nextCode = FirstCode;
                oldCode = -1;
                continue;
            }

            // Work out the length of the sequence this code stands for (validating the code), and
            // reject it before anything is written if it would push the output past the limit.
            int sequenceLength;
            if (oldCode < 0)
            {
                if (code >= 256)
                {
                    throw new InvalidDataException($"Invalid LZW code {code} before any table entry exists.");
                }

                sequenceLength = 1;
            }
            else if (code < nextCode)
            {
                sequenceLength = lengthOf[code];
            }
            else if (code == nextCode)
            {
                // The classic "KwKwK" case: the code being decoded is the one about to be added to
                // the table (the previous sequence followed by its own first byte).
                sequenceLength = lengthOf[oldCode] + 1;
            }
            else
            {
                throw new InvalidDataException($"Invalid LZW code {code}: not yet present in the table.");
            }

            if (sequenceLength > maxOutputBytes - count)
            {
                throw new InvalidDataException(
                    $"Decoded LZW data exceeds the maximum supported size of {maxOutputBytes} bytes.");
            }

            if (count + sequenceLength > output.Length)
            {
                var capacity = (int)Math.Min(maxOutputBytes, Math.Max(count + (long)sequenceLength, output.Length * 2L));
                Array.Resize(ref output, capacity);
            }

            if (oldCode >= 0 && code == nextCode)
            {
                WriteSequence(oldCode, output, count, sequenceLength - 1, prefixOf, suffixOf);
                output[count + sequenceLength - 1] = output[count];
            }
            else
            {
                WriteSequence(code, output, count, sequenceLength, prefixOf, suffixOf);
            }

            if (oldCode >= 0 && nextCode < MaxTableSize)
            {
                prefixOf[nextCode] = oldCode;
                suffixOf[nextCode] = output[count];
                lengthOf[nextCode] = lengthOf[oldCode] + 1;
                nextCode++;

                if (nextCode > maxCode && codeWidth < MaxCodeWidth)
                {
                    codeWidth++;
                    maxCode = (1 << codeWidth) - 1 - earlyChangeAmount;
                }
            }

            count += sequenceLength;
            oldCode = code;
        }

        if (count != output.Length)
        {
            Array.Resize(ref output, count);
        }

        return output;
    }

    /// <summary>
    ///     Writes the full byte sequence of a table <paramref name="code"/> into
    ///     <paramref name="output"/>, back to front, starting at <paramref name="start"/>.
    /// </summary>
    /// <param name="code">The table code (a literal below 256, or an entry from 258).</param>
    /// <param name="output">The output buffer.</param>
    /// <param name="start">The index of the sequence's first byte.</param>
    /// <param name="length">The sequence's length.</param>
    /// <param name="prefixOf">The table's prefix links.</param>
    /// <param name="suffixOf">The table's suffix bytes.</param>
    private static void WriteSequence(int code, byte[] output, int start, int length, int[] prefixOf, byte[] suffixOf)
    {
        var position = start + length - 1;
        var current = code;
        while (current >= FirstCode)
        {
            output[position--] = suffixOf[current];
            current = prefixOf[current];
        }

        output[position] = (byte)current;
    }
}
