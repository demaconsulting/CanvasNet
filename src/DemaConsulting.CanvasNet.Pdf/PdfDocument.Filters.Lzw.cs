// cspell:ignore Lzw
namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>The fixed PDF LZWDecode Clear-table code.</summary>
    private const int LzwClearCode = 256;

    /// <summary>The fixed PDF LZWDecode end-of-data code.</summary>
    private const int LzwEodCode = 257;

    /// <summary>The first dynamically assigned PDF LZWDecode table code.</summary>
    private const int LzwFirstCode = 258;

    /// <summary>The maximum number of PDF LZWDecode table entries (12-bit codes).</summary>
    private const int LzwMaxTableSize = 4096;

    /// <summary>
    ///     Decodes an <c>LZWDecode</c>-filtered stream (ISO 32000-1/2 section 7.4.4): an initial
    ///     258-entry table (codes 0-255 literal bytes, 256 Clear, 257 EOD, first dynamically
    ///     assigned code 258), MSB-first variable 9-12 bit code packing, and code-width growth
    ///     timing controlled by <paramref name="earlyChange"/>.
    /// </summary>
    /// <remarks>
    ///     This is an independent, from-scratch implementation of the PDF-variant algorithm - it
    ///     shares no code with (and was only structurally informed by, per the task's hard
    ///     constraint) <c>Codecs/Gif/GifCodec.Lzw.cs</c> (LSB-first, no early-change concept) or
    ///     <c>Codecs/Tiff/TiffCodec.Compression.cs</c> (MSB-first, but fixed early-change
    ///     semantics with no <c>/EarlyChange</c> toggle). A dynamically assigned code's dictionary
    ///     entry is added the moment its preceding code has been decoded (mirroring the classic
    ///     LZW decompressor's own well-established "no entry for the very first code after a
    ///     Clear" asymmetry against the encoder's own "no entry for its final flushed code" -
    ///     both sides make the same total number of additions, and the encoder's code-width
    ///     growth threshold is offset from the decoder's own by exactly the resulting one-entry
    ///     lag; a compliant PDF LZWDecode encoder must apply that same offset for the two sides
    ///     to remain synchronized, independent of the <c>/EarlyChange</c> value chosen).
    /// </remarks>
    /// <param name="data">The raw (pre-<c>LZWDecode</c>) stream bytes.</param>
    /// <param name="earlyChange">
    ///     The resolved <c>/DecodeParms /EarlyChange</c> value: <see langword="true"/> (the PDF
    ///     default) grows the code width one code earlier than <see langword="false"/>.
    /// </param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the bit stream is truncated before an <c>EOD</c> code is reached, or when
    ///     a decoded code is invalid (out of range for the current table state).
    /// </exception>
    private static byte[] DecodeLzw(byte[] data, bool earlyChange)
    {
        var earlyChangeAmount = earlyChange ? 1 : 0;
        var reader = new LzwBitReader(data);
        var prefixOf = new int[LzwMaxTableSize];
        var suffixOf = new byte[LzwMaxTableSize];

        var codeWidth = 9;
        var maxCode = (1 << codeWidth) - 1 - earlyChangeAmount;
        var nextCode = LzwFirstCode;
        var output = new List<byte>();
        var sequence = new List<byte>();
        int? oldCode = null;

        while (true)
        {
            var code = reader.ReadCode(codeWidth) ?? throw new InvalidDataException(
                "Truncated LZWDecode stream: missing EOD code.");

            if (code == LzwEodCode)
            {
                break;
            }

            if (code == LzwClearCode)
            {
                codeWidth = 9;
                maxCode = (1 << codeWidth) - 1 - earlyChangeAmount;
                nextCode = LzwFirstCode;
                oldCode = null;
                continue;
            }

            sequence.Clear();
            if (oldCode is null)
            {
                if (code >= 256)
                {
                    throw new InvalidDataException($"Invalid LZWDecode code {code} before any table entry exists.");
                }

                sequence.Add((byte)code);
            }
            else if (code < nextCode)
            {
                ExpandLzwCode(code, prefixOf, suffixOf, sequence);
            }
            else if (code == nextCode)
            {
                // The classic "KwKwK" special case: the code being decoded is the one about to
                // be added to the table (the previous sequence followed by its own first byte).
                ExpandLzwCode(oldCode.Value, prefixOf, suffixOf, sequence);
                sequence.Add(sequence[0]);
            }
            else
            {
                throw new InvalidDataException($"Invalid LZWDecode code {code}: not yet present in the table.");
            }

            output.AddRange(sequence);

            if (oldCode is not null && nextCode < LzwMaxTableSize)
            {
                prefixOf[nextCode] = oldCode.Value;
                suffixOf[nextCode] = sequence[0];
                nextCode++;

                if (nextCode > maxCode && codeWidth < 12)
                {
                    codeWidth++;
                    maxCode = (1 << codeWidth) - 1 - earlyChangeAmount;
                }
            }

            oldCode = code;
        }

        return [.. output];
    }

    /// <summary>Expands a table code back into its full byte sequence, appending it to <paramref name="sequence"/>.</summary>
    private static void ExpandLzwCode(int code, int[] prefixOf, byte[] suffixOf, List<byte> sequence)
    {
        var stack = new Stack<byte>();
        var current = code;
        while (current >= LzwFirstCode)
        {
            stack.Push(suffixOf[current]);
            current = prefixOf[current];
        }

        sequence.Add((byte)current);
        while (stack.Count > 0)
        {
            sequence.Add(stack.Pop());
        }
    }

    /// <summary>An MSB-first (high-order bit first, per ISO 32000-1/2 section 7.4.4.2), variable-width bit reader over a byte array.</summary>
    private sealed class LzwBitReader(byte[] data)
    {
        private int _bytePosition;
        private int _bitPosition;

        /// <summary>Reads the next <paramref name="width"/>-bit code, or <see langword="null"/> when the stream is exhausted.</summary>
        public int? ReadCode(int width)
        {
            var value = 0;
            for (var i = 0; i < width; i++)
            {
                if (_bytePosition >= data.Length)
                {
                    return null;
                }

                var bit = (data[_bytePosition] >> (7 - _bitPosition)) & 1;
                value = (value << 1) | bit;

                _bitPosition++;
                if (_bitPosition == 8)
                {
                    _bitPosition = 0;
                    _bytePosition++;
                }
            }

            return value;
        }
    }
}
