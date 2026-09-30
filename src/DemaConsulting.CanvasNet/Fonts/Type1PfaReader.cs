// cspell:ignore eexec sniff cleartomark
using System.Text;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Reads the "PFA" plain-ASCII framing of a Type 1 font program - the same font program as
///     a <c>.pfb</c> file, but with its encrypted region conventionally represented as
///     hexadecimal digit text rather than raw binary bytes - and reassembles the underlying
///     cleartext and <c>eexec</c>-encrypted byte regions that <see cref="Type1Table.Parse"/>
///     expects.
/// </summary>
/// <remarks>
///     A PFA file's cleartext region precedes the literal ASCII keyword <c>eexec</c>; its exact
///     content is irrelevant here (<see cref="Type1Table.Parse"/> never inspects cleartext bytes,
///     only counts them), so this reader reuses the original file bytes from the start of the
///     file through the end of the <c>eexec</c> keyword verbatim as the cleartext region. The
///     encrypted region follows as a run of hexadecimal digit characters (conventionally wrapped
///     at a fixed column width, so embedded whitespace/newlines are tolerated and skipped) that
///     decodes to the same bytes a <c>.pfb</c> file's binary segment would contain, including its
///     own conventional trailing zero-padding, which is trimmed after decoding (not before, since
///     <c>'0'</c> is itself a valid hex digit and can't be distinguished from padding except by
///     position).
/// </remarks>
internal static class Type1PfaReader
{
    private const string EexecKeyword = "eexec";

    /// <summary>
    ///     Determines whether <paramref name="data"/> looks like a PFA file, without fully
    ///     parsing it.
    /// </summary>
    /// <param name="data">The candidate font file bytes.</param>
    /// <returns><see langword="true"/> if <paramref name="data"/> starts with <c>%!</c>.</returns>
    public static bool TrySniff(byte[] data) => data.Length >= 2 && data[0] == '%' && data[1] == '!';

    /// <summary>
    ///     Reassembles a PFA file's cleartext and hex-encoded encrypted regions into the single
    ///     contiguous byte layout - <c>length1</c> cleartext bytes immediately followed by
    ///     <c>length2</c> encrypted bytes - that <see cref="Type1Table.Parse"/> expects.
    /// </summary>
    /// <param name="data">The complete PFA file bytes.</param>
    /// <returns>
    ///     The reassembled font file bytes, along with the cleartext and encrypted region
    ///     lengths.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the file does not contain the literal <c>eexec</c> keyword, the following
    ///     hexadecimal run has an odd number of digits, a non-hexadecimal/non-whitespace byte is
    ///     encountered before any hex digit has been read, or zero bytes remain after decoding and
    ///     trimming trailing zero padding.
    /// </exception>
    public static (byte[] FontFileBytes, int Length1, int Length2) Read(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var text = Encoding.Latin1.GetString(data);
        var keywordIndex = text.IndexOf(EexecKeyword, StringComparison.Ordinal);
        if (keywordIndex < 0)
        {
            throw new InvalidDataException("Type 1 PFA file is missing the required 'eexec' keyword.");
        }

        var length1 = keywordIndex + EexecKeyword.Length;

        var encrypted = new List<byte>();
        var haveHighNibble = false;
        var highNibble = 0;
        var sawAnyHexDigit = false;

        for (var pos = length1; pos < data.Length; pos++)
        {
            var b = data[pos];
            var digit = HexDigitValue(b);
            if (digit >= 0)
            {
                sawAnyHexDigit = true;
                if (haveHighNibble)
                {
                    encrypted.Add((byte)((highNibble << 4) | digit));
                    haveHighNibble = false;
                }
                else
                {
                    highNibble = digit;
                    haveHighNibble = true;
                }

                continue;
            }

            if (IsWhitespace(b))
            {
                continue;
            }

            if (!sawAnyHexDigit)
            {
                throw new InvalidDataException(
                    "Type 1 PFA file's encrypted region contains a non-hexadecimal byte before any hex digit was read.");
            }

            // A non-hex, non-whitespace byte after at least one hex digit marks the end of the
            // hexadecimal run (for example the conventional trailing '0' padding is itself valid
            // hex, but PostScript operators like 'cleartomark' are not).
            break;
        }

        if (haveHighNibble)
        {
            throw new InvalidDataException("Type 1 PFA file's encrypted region has an odd number of hexadecimal digits.");
        }

        // Trim the conventional trailing zero-padding (512 zero bytes is the usual convention,
        // but only the fact that it's trailing zeros matters here).
        var end = encrypted.Count;
        while (end > 0 && encrypted[end - 1] == 0)
        {
            end--;
        }

        if (end == 0)
        {
            throw new InvalidDataException("Type 1 PFA file's encrypted region decodes to zero bytes.");
        }

        var fontFileBytes = new byte[length1 + end];
        Array.Copy(data, 0, fontFileBytes, 0, length1);
        for (var i = 0; i < end; i++)
        {
            fontFileBytes[length1 + i] = encrypted[i];
        }

        return (fontFileBytes, length1, end);
    }

    private static int HexDigitValue(byte b) => b switch
    {
        >= (byte)'0' and <= (byte)'9' => b - '0',
        >= (byte)'a' and <= (byte)'f' => b - 'a' + 10,
        >= (byte)'A' and <= (byte)'F' => b - 'A' + 10,
        _ => -1,
    };

    private static bool IsWhitespace(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or 0;
}
