// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
// cspell:ignore macStyle fsSelection usWeightClass isFixedPitch italicAngle
namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Derives a font's bold/italic/fixed-pitch style metadata from the optional <c>OS/2</c> and
///     <c>post</c> tables and the required <c>head.macStyle</c> field.
/// </summary>
/// <remarks>
///     <para>
///     Every source table/field is consulted independently and OR'd together - the first
///     applicable signal to fire for a given boolean wins, and every source that can contribute
///     to that boolean is always consulted, not merely as a fallback for another source's
///     absence: <c>head.macStyle</c> always contributes alongside <c>OS/2</c>/<c>post</c> when
///     they are present, rather than being consulted only when they are absent (see this class's
///     unit design documentation for the rationale).
///     </para>
///     <para>
///     <c>OS/2</c> and <c>post</c> are both optional, and each field read from them is
///     individually gated by its own minimum table-length check - a table present but too short
///     for one particular field simply skips that field's contribution, rather than discarding
///     the whole table or throwing. This class never throws.
///     </para>
/// </remarks>
internal sealed class StyleTable
{
    /// <summary>
    ///     The <c>OS/2 fsSelection</c> BOLD bit (bit 5, <c>0x20</c>).
    /// </summary>
    private const int Os2FsSelectionBold = 0x20;

    /// <summary>
    ///     The <c>OS/2 fsSelection</c> ITALIC bit (bit 0, <c>0x1</c>).
    /// </summary>
    private const int Os2FsSelectionItalic = 0x1;

    /// <summary>
    ///     The minimum <c>usWeightClass</c> value (per the OpenType <c>OS/2</c> specification's
    ///     weight class scale) treated as a bold-weight design.
    /// </summary>
    private const int BoldWeightClassThreshold = 600;

    /// <summary>
    ///     The byte offset, within <c>OS/2</c>, of the 16-bit <c>usWeightClass</c> field.
    /// </summary>
    private const int Os2UsWeightClassOffset = 4;

    /// <summary>
    ///     The minimum <c>OS/2</c> table length required to read <c>usWeightClass</c>.
    /// </summary>
    private const int Os2UsWeightClassMinLength = 6;

    /// <summary>
    ///     The byte offset, within <c>OS/2</c>, of the 16-bit <c>fsSelection</c> field.
    /// </summary>
    private const int Os2FsSelectionOffset = 62;

    /// <summary>
    ///     The minimum <c>OS/2</c> table length required to read <c>fsSelection</c>.
    /// </summary>
    private const int Os2FsSelectionMinLength = 64;

    /// <summary>
    ///     The byte offset, within <c>post</c>, of the 32-bit <c>italicAngle</c> field.
    /// </summary>
    private const int PostItalicAngleOffset = 4;

    /// <summary>
    ///     The minimum <c>post</c> table length required to read <c>italicAngle</c>.
    /// </summary>
    private const int PostItalicAngleMinLength = 8;

    /// <summary>
    ///     The byte offset, within <c>post</c>, of the 32-bit <c>isFixedPitch</c> field. Per the
    ///     OpenType <c>post</c> table specification, the fixed 32-byte header is
    ///     <c>version</c> (4 bytes) + <c>italicAngle</c> (4 bytes) + <c>underlinePosition</c>
    ///     (2 bytes) + <c>underlineThickness</c> (2 bytes) + <c>isFixedPitch</c> (4 bytes) + four
    ///     more <see langword="uint"/> memory-usage fields.
    /// </summary>
    private const int PostIsFixedPitchOffset = 12;

    /// <summary>
    ///     The minimum <c>post</c> table length required to read <c>isFixedPitch</c>.
    /// </summary>
    private const int PostIsFixedPitchMinLength = 16;

    /// <summary>
    ///     The <c>head.macStyle</c> Bold bit (bit 0, <c>0x1</c>).
    /// </summary>
    private const int MacStyleBold = 0x1;

    /// <summary>
    ///     The <c>head.macStyle</c> Italic bit (bit 1, <c>0x2</c>).
    /// </summary>
    private const int MacStyleItalic = 0x2;

    /// <summary>
    ///     Whether this font is a bold-weight design.
    /// </summary>
    public bool IsBold { get; }

    /// <summary>
    ///     Whether this font is an italic/oblique design.
    /// </summary>
    public bool IsItalic { get; }

    /// <summary>
    ///     Whether this font is a fixed-pitch (monospaced) design.
    /// </summary>
    public bool IsFixedPitch { get; }

    private StyleTable(bool isBold, bool isItalic, bool isFixedPitch)
    {
        IsBold = isBold;
        IsItalic = isItalic;
        IsFixedPitch = isFixedPitch;
    }

    /// <summary>
    ///     Derives bold/italic/fixed-pitch style metadata from the given tables.
    /// </summary>
    /// <param name="data">The complete font file contents.</param>
    /// <param name="macStyle">The already-decoded <c>head.macStyle</c> field.</param>
    /// <param name="os2">
    ///     The <c>OS/2</c> table's offset and length within <paramref name="data"/>, or
    ///     <see langword="null"/> if the font has no <c>OS/2</c> table.
    /// </param>
    /// <param name="post">
    ///     The <c>post</c> table's offset and length within <paramref name="data"/>, or
    ///     <see langword="null"/> if the font has no <c>post</c> table.
    /// </param>
    /// <returns>The derived <see cref="StyleTable"/>. Never throws.</returns>
    public static StyleTable Parse(byte[] data, int macStyle, (int Offset, int Length)? os2, (int Offset, int Length)? post)
    {
        var isBold = (macStyle & MacStyleBold) != 0;
        var isItalic = (macStyle & MacStyleItalic) != 0;
        var isFixedPitch = false;

        if (os2 is { } os2Range)
        {
            if (os2Range.Length >= Os2FsSelectionMinLength)
            {
                var fsSelection = SfntContainer.ReadUInt16(data, os2Range.Offset + Os2FsSelectionOffset);
                isBold |= (fsSelection & Os2FsSelectionBold) != 0;
                isItalic |= (fsSelection & Os2FsSelectionItalic) != 0;
            }

            if (os2Range.Length >= Os2UsWeightClassMinLength)
            {
                var usWeightClass = SfntContainer.ReadUInt16(data, os2Range.Offset + Os2UsWeightClassOffset);
                isBold |= usWeightClass >= BoldWeightClassThreshold;
            }
        }

        if (post is { } postRange)
        {
            if (postRange.Length >= PostItalicAngleMinLength)
            {
                var italicAngle = SfntContainer.ReadInt32(data, postRange.Offset + PostItalicAngleOffset);
                isItalic |= italicAngle != 0;
            }

            if (postRange.Length >= PostIsFixedPitchMinLength)
            {
                var isFixedPitchField = SfntContainer.ReadInt32(data, postRange.Offset + PostIsFixedPitchOffset);
                isFixedPitch = isFixedPitchField != 0;
            }
        }

        return new StyleTable(isBold, isItalic, isFixedPitch);
    }
}
