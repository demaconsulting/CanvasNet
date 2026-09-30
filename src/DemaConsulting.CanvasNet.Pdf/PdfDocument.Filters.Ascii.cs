// cspell:ignore Ascii
namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>The lowest valid ASCII85Decode data-character code point (<c>!</c>).</summary>
    private const byte Ascii85MinChar = 0x21;

    /// <summary>The highest valid ASCII85Decode data-character code point (<c>u</c>).</summary>
    private const byte Ascii85MaxChar = 0x75;

    /// <summary>
    ///     Decodes an <c>ASCII85Decode</c>-filtered stream (ISO 32000-1/2 section 7.4.3): groups
    ///     of 5 base-85 digit characters (<c>!</c> through <c>u</c>, code points <c>0x21</c>-
    ///     <c>0x75</c>) decode to 4 bytes each, the character <c>z</c> is shorthand for an
    ///     all-zero 4-byte group when it occurs at a group boundary, a final partial group of
    ///     <c>n</c> (2-4) characters decodes to <c>n - 1</c> bytes after padding with the digit
    ///     value 84 (<c>u</c>), and the two-character sequence <c>~&gt;</c> marks EOD. All
    ///     PDF white-space characters are ignored wherever they occur.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when an out-of-range character is encountered, <c>z</c> occurs in the middle of
    ///     a group, a decoded group's value exceeds <c>2^32 - 1</c>, a final partial group
    ///     contains exactly one character, or the <c>~&gt;</c> EOD marker is missing.
    /// </exception>
    private static byte[] DecodeAscii85(byte[] data)
    {
        var output = new List<byte>();
        var group = new List<byte>(5);
        var foundEod = false;

        var i = 0;
        while (i < data.Length)
        {
            var b = data[i];

            if (IsPdfWhitespace(b))
            {
                i++;
                continue;
            }

            if (b == (byte)'~')
            {
                i++;
                while (i < data.Length && IsPdfWhitespace(data[i]))
                {
                    i++;
                }

                if (i >= data.Length || data[i] != (byte)'>')
                {
                    throw new InvalidDataException("ASCII85Decode stream has a malformed ~> EOD marker.");
                }

                foundEod = true;
                break;
            }

            if (b == (byte)'z')
            {
                if (group.Count != 0)
                {
                    throw new InvalidDataException("ASCII85Decode 'z' shorthand may only occur at a group boundary.");
                }

                output.AddRange(new byte[4]);
                i++;
                continue;
            }

            if (b is < Ascii85MinChar or > Ascii85MaxChar)
            {
                throw new InvalidDataException($"Invalid ASCII85Decode character 0x{b:X2}.");
            }

            group.Add((byte)(b - Ascii85MinChar));
            i++;

            if (group.Count == 5)
            {
                AppendAscii85Group(group, 5, output);
                group.Clear();
            }
        }

        if (!foundEod)
        {
            throw new InvalidDataException("ASCII85Decode stream is missing its ~> EOD marker.");
        }

        if (group.Count == 1)
        {
            throw new InvalidDataException("ASCII85Decode final partial group must contain at least 2 characters.");
        }

        if (group.Count > 0)
        {
            var significantChars = group.Count;
            while (group.Count < 5)
            {
                group.Add(84);
            }

            AppendAscii85Group(group, significantChars, output);
        }

        return [.. output];
    }

    /// <summary>Decodes one full 5-character ASCII85 group into up to 4 bytes, appending them to <paramref name="output"/>.</summary>
    /// <exception cref="InvalidDataException">Thrown when the group's base-85 value exceeds <c>2^32 - 1</c>.</exception>
    private static void AppendAscii85Group(IReadOnlyList<byte> group, int significantChars, List<byte> output)
    {
        var value = 0UL;
        for (var i = 0; i < 5; i++)
        {
            value = (value * 85) + group[i];
        }

        if (value > uint.MaxValue)
        {
            throw new InvalidDataException("ASCII85Decode group value exceeds 2^32 - 1.");
        }

        Span<byte> bytes = [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
        var emitCount = significantChars - 1;
        for (var i = 0; i < emitCount; i++)
        {
            output.Add(bytes[i]);
        }
    }

    /// <summary>
    ///     Decodes an <c>ASCIIHexDecode</c>-filtered stream (ISO 32000-1/2 section 7.4.2): pairs
    ///     of hexadecimal digits decode to bytes, all PDF white-space characters are ignored, and
    ///     a <c>&gt;</c> character marks EOD; an odd trailing digit is implicitly padded with a
    ///     zero low nibble.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a non-hexadecimal, non-whitespace character is encountered, or the
    ///     <c>&gt;</c> EOD marker is missing.
    /// </exception>
    private static byte[] DecodeAsciiHex(byte[] data)
    {
        var output = new List<byte>();
        int? highNibble = null;
        var foundEod = false;

        foreach (var b in data)
        {
            if (b == (byte)'>')
            {
                foundEod = true;
                break;
            }

            if (IsPdfWhitespace(b))
            {
                continue;
            }

            var value = HexDigitValue(b) ?? throw new InvalidDataException($"Invalid ASCIIHexDecode character 0x{b:X2}.");
            if (highNibble is null)
            {
                highNibble = value;
            }
            else
            {
                output.Add((byte)((highNibble.Value << 4) | value));
                highNibble = null;
            }
        }

        if (!foundEod)
        {
            throw new InvalidDataException("ASCIIHexDecode stream is missing its > EOD marker.");
        }

        if (highNibble is not null)
        {
            output.Add((byte)(highNibble.Value << 4));
        }

        return [.. output];
    }

    /// <summary>Returns whether <paramref name="b"/> is one of the PDF specification's white-space byte values.</summary>
    private static bool IsPdfWhitespace(byte b) => b is 0x00 or 0x09 or 0x0A or 0x0C or 0x0D or 0x20;

    /// <summary>Returns the numeric value (0-15) of a hexadecimal digit character, or <see langword="null"/> when it is not one.</summary>
    private static int? HexDigitValue(byte b) => b switch
    {
        >= (byte)'0' and <= (byte)'9' => b - (byte)'0',
        >= (byte)'A' and <= (byte)'F' => b - (byte)'A' + 10,
        >= (byte)'a' and <= (byte)'f' => b - (byte)'a' + 10,
        _ => null,
    };
}
