// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
// cspell:ignore langTagRecords nameID
using System.Text;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Parses a font's <c>name</c> table and resolves the family, subfamily, full, and PostScript
///     name strings, preferring the typographic name identifiers (<c>16</c>/<c>17</c>) over the
///     standard ones (<c>1</c>/<c>2</c>) and preferring a Windows/Unicode BMP platform record over
///     a Macintosh platform record when both are present for the same name identifier.
/// </summary>
/// <remarks>
///     <para>
///     Only two platform/encoding combinations are decoded: Windows (<c>platformID</c> 3) with
///     encoding <c>1</c> (Unicode BMP) or <c>10</c> (Unicode full repertoire), both decoded as
///     big-endian UTF-16, and Macintosh (<c>platformID</c> 1) with encoding <c>0</c> (Roman),
///     decoded one byte per character via a fixed Mac OS Roman lookup table for bytes
///     <c>&gt;= 0x80</c>. Any other platform/encoding combination is ignored - not decoded, not
///     preferred, not used as a fallback.
///     </para>
///     <para>
///     <c>name</c> is an optional table: this class never throws. A table too short to contain
///     its 6-byte header, or whose declared record count does not fit the table's bounds, is
///     treated as <see cref="Empty"/>. Otherwise, each of the table's individual 12-byte name
///     records is validated independently - a record whose string bytes would fall outside the
///     table's bounds, or whose platform/encoding pair is not one of the two decoded
///     combinations, is silently skipped without discarding any other, well-formed record.
///     </para>
/// </remarks>
internal sealed class NameTable
{
    /// <summary>
    ///     The Windows platform ID (<c>platformID == 3</c>).
    /// </summary>
    private const int PlatformWindows = 3;

    /// <summary>
    ///     The Macintosh platform ID (<c>platformID == 1</c>).
    /// </summary>
    private const int PlatformMacintosh = 1;

    /// <summary>
    ///     The Macintosh Roman encoding ID (<c>encodingID == 0</c>), used with
    ///     <see cref="PlatformMacintosh"/>.
    /// </summary>
    private const int EncodingMacRoman = 0;

    /// <summary>
    ///     The Windows Unicode BMP encoding ID (<c>encodingID == 1</c>), used with
    ///     <see cref="PlatformWindows"/>.
    /// </summary>
    private const int EncodingWindowsUnicodeBmp = 1;

    /// <summary>
    ///     The Windows Unicode full-repertoire encoding ID (<c>encodingID == 10</c>), used with
    ///     <see cref="PlatformWindows"/>.
    /// </summary>
    private const int EncodingWindowsUnicodeFull = 10;

    /// <summary>
    ///     The Windows US-English language ID (<c>0x0409</c>), preferred among multiple Windows
    ///     platform records for the same name identifier.
    /// </summary>
    private const int LanguageWindowsUsEnglish = 0x0409;

    /// <summary>
    ///     The Macintosh English language ID (<c>0</c>), preferred among multiple Macintosh
    ///     platform records for the same name identifier.
    /// </summary>
    private const int LanguageMacintoshEnglish = 0;

    /// <summary>
    ///     The typographic family name identifier (<c>nameID</c> 16).
    /// </summary>
    private const int NameIdTypographicFamily = 16;

    /// <summary>
    ///     The typographic subfamily name identifier (<c>nameID</c> 17).
    /// </summary>
    private const int NameIdTypographicSubfamily = 17;

    /// <summary>
    ///     The standard family name identifier (<c>nameID</c> 1).
    /// </summary>
    private const int NameIdFamily = 1;

    /// <summary>
    ///     The standard subfamily name identifier (<c>nameID</c> 2).
    /// </summary>
    private const int NameIdSubfamily = 2;

    /// <summary>
    ///     The full font name identifier (<c>nameID</c> 4).
    /// </summary>
    private const int NameIdFull = 4;

    /// <summary>
    ///     The PostScript name identifier (<c>nameID</c> 6).
    /// </summary>
    private const int NameIdPostScript = 6;

    /// <summary>
    ///     Maps Mac OS Roman bytes <c>0x80</c>-<c>0xFF</c> (index <c>byte - 0x80</c>) to their
    ///     Unicode codepoint, per the Unicode Consortium's published <c>MACROMAN.TXT</c>/Apple
    ///     <c>ROMAN.TXT</c> mapping. Bytes below <c>0x80</c> never consult this table: Mac Roman
    ///     is ASCII-identical in that range.
    /// </summary>
    private static readonly char[] MacRomanHighBytes =
    [
        '\u00C4', '\u00C5', '\u00C7', '\u00C9', '\u00D1', '\u00D6', '\u00DC', '\u00E1', // 0x80-0x87
        '\u00E0', '\u00E2', '\u00E4', '\u00E3', '\u00E5', '\u00E7', '\u00E9', '\u00E8', // 0x88-0x8F
        '\u00EA', '\u00EB', '\u00ED', '\u00EC', '\u00EE', '\u00EF', '\u00F1', '\u00F3', // 0x90-0x97
        '\u00F2', '\u00F4', '\u00F6', '\u00F5', '\u00FA', '\u00F9', '\u00FB', '\u00FC', // 0x98-0x9F
        '\u2020', '\u00B0', '\u00A2', '\u00A3', '\u00A7', '\u2022', '\u00B6', '\u00DF', // 0xA0-0xA7
        '\u00AE', '\u00A9', '\u2122', '\u00B4', '\u00A8', '\u2260', '\u00C6', '\u00D8', // 0xA8-0xAF
        '\u221E', '\u00B1', '\u2264', '\u2265', '\u00A5', '\u00B5', '\u2202', '\u2211', // 0xB0-0xB7
        '\u220F', '\u03C0', '\u222B', '\u00AA', '\u00BA', '\u03A9', '\u00E6', '\u00F8', // 0xB8-0xBF
        '\u00BF', '\u00A1', '\u00AC', '\u221A', '\u0192', '\u2248', '\u2206', '\u00AB', // 0xC0-0xC7
        '\u00BB', '\u2026', '\u00A0', '\u00C0', '\u00C3', '\u00D5', '\u0152', '\u0153', // 0xC8-0xCF
        '\u2013', '\u2014', '\u201C', '\u201D', '\u2018', '\u2019', '\u00F7', '\u25CA', // 0xD0-0xD7
        '\u00FF', '\u0178', '\u2044', '\u20AC', '\u2039', '\u203A', '\uFB01', '\uFB02', // 0xD8-0xDF
        '\u2021', '\u00B7', '\u201A', '\u201E', '\u2030', '\u00C2', '\u00CA', '\u00C1', // 0xE0-0xE7
        '\u00CB', '\u00C8', '\u00CD', '\u00CE', '\u00CF', '\u00CC', '\u00D3', '\u00D4', // 0xE8-0xEF
        '\uF8FF', '\u00D2', '\u00DA', '\u00DB', '\u00D9', '\u0131', '\u02C6', '\u02DC', // 0xF0-0xF7
        '\u00AF', '\u02D8', '\u02D9', '\u02DA', '\u00B8', '\u02DD', '\u02DB', '\u02C7', // 0xF8-0xFF
    ];

    /// <summary>
    ///     A <see cref="NameTable"/> with every field <see langword="null"/>: the font has no
    ///     usable <c>name</c> table.
    /// </summary>
    public static readonly NameTable Empty = new(null, null, null, null);

    /// <summary>
    ///     The typographic family name (<c>nameID</c> 16) if present; otherwise the standard
    ///     family name (<c>nameID</c> 1); otherwise <see langword="null"/>.
    /// </summary>
    public string? FamilyName { get; }

    /// <summary>
    ///     The typographic subfamily name (<c>nameID</c> 17) if present; otherwise the standard
    ///     subfamily name (<c>nameID</c> 2); otherwise <see langword="null"/>.
    /// </summary>
    public string? SubfamilyName { get; }

    /// <summary>
    ///     The full font name (<c>nameID</c> 4), or <see langword="null"/>.
    /// </summary>
    public string? FullName { get; }

    /// <summary>
    ///     The PostScript name (<c>nameID</c> 6), or <see langword="null"/>.
    /// </summary>
    public string? PostScriptName { get; }

    private NameTable(string? familyName, string? subfamilyName, string? fullName, string? postScriptName)
    {
        FamilyName = familyName;
        SubfamilyName = subfamilyName;
        FullName = fullName;
        PostScriptName = postScriptName;
    }

    /// <summary>
    ///     Parses a <c>name</c> table and resolves the family/subfamily/full/PostScript name
    ///     strings.
    /// </summary>
    /// <param name="data">The complete font file contents.</param>
    /// <param name="tableOffset">The offset of the <c>name</c> table within <paramref name="data"/>.</param>
    /// <param name="tableLength">The length of the <c>name</c> table.</param>
    /// <returns>
    ///     A <see cref="NameTable"/> with every resolvable field populated, or <see cref="Empty"/>
    ///     if the table's own 6-byte header is too short, or its declared record count does not
    ///     fit the table's bounds.
    /// </returns>
    public static NameTable Parse(byte[] data, int tableOffset, int tableLength)
    {
        if (tableLength < 6)
        {
            return Empty;
        }

        var count = SfntContainer.ReadUInt16(data, tableOffset + 2);
        var stringOffsetField = SfntContainer.ReadUInt16(data, tableOffset + 4);

        const int recordSize = 12;
        const int headerSize = 6;
        var tableEnd = (long)tableOffset + tableLength;
        var recordsEnd = (long)tableOffset + headerSize + (long)count * recordSize;
        if (recordsEnd > tableEnd)
        {
            return Empty;
        }

        var stringAreaStart = tableOffset + stringOffsetField;

        // Every candidate record found for each name identifier, keyed by nameID, in table order.
        var windowsCandidates = new Dictionary<int, List<(int LanguageId, string Value)>>();
        var macintoshCandidates = new Dictionary<int, List<(int LanguageId, string Value)>>();

        for (var i = 0; i < count; i++)
        {
            var recordOffset = tableOffset + headerSize + i * recordSize;
            var platformId = SfntContainer.ReadUInt16(data, recordOffset);
            var encodingId = SfntContainer.ReadUInt16(data, recordOffset + 2);
            var languageId = SfntContainer.ReadUInt16(data, recordOffset + 4);
            var nameId = SfntContainer.ReadUInt16(data, recordOffset + 6);
            var length = SfntContainer.ReadUInt16(data, recordOffset + 8);
            var offset = SfntContainer.ReadUInt16(data, recordOffset + 10);

            if (nameId is not (NameIdFamily or NameIdSubfamily or NameIdFull or NameIdPostScript
                or NameIdTypographicFamily or NameIdTypographicSubfamily))
            {
                // Not one of the six name identifiers this class resolves; skip decoding it.
                continue;
            }

            var stringStart = (long)stringAreaStart + offset;
            var stringEnd = stringStart + length;
            if (stringStart < tableOffset || stringEnd > tableEnd)
            {
                // This one record's string bytes fall outside the table's bounds; skip only this
                // record, leaving every other well-formed record resolvable.
                continue;
            }

            string? value = (platformId, encodingId) switch
            {
                (PlatformWindows, EncodingWindowsUnicodeBmp) or (PlatformWindows, EncodingWindowsUnicodeFull) =>
                    DecodeWindowsUnicode(data, (int)stringStart, length),
                (PlatformMacintosh, EncodingMacRoman) => DecodeMacRoman(data, (int)stringStart, length),
                _ => null,
            };

            if (value is null)
            {
                continue;
            }

            var candidates = platformId == PlatformWindows ? windowsCandidates : macintoshCandidates;
            if (!candidates.TryGetValue(nameId, out var list))
            {
                list = [];
                candidates[nameId] = list;
            }

            list.Add((languageId, value));
        }

        string? Resolve(int nameId) =>
            ResolvePreferred(windowsCandidates, nameId, LanguageWindowsUsEnglish) ??
            ResolvePreferred(macintoshCandidates, nameId, LanguageMacintoshEnglish);

        var familyName = Resolve(NameIdTypographicFamily) ?? Resolve(NameIdFamily);
        var subfamilyName = Resolve(NameIdTypographicSubfamily) ?? Resolve(NameIdSubfamily);
        var fullName = Resolve(NameIdFull);
        var postScriptName = Resolve(NameIdPostScript);

        return new NameTable(familyName, subfamilyName, fullName, postScriptName);
    }

    /// <summary>
    ///     Selects the preferred candidate string for a name identifier from one platform's
    ///     candidate list: the entry matching <paramref name="preferredLanguageId"/> if present,
    ///     otherwise the first candidate encountered in table order.
    /// </summary>
    private static string? ResolvePreferred(
        Dictionary<int, List<(int LanguageId, string Value)>> candidates,
        int nameId,
        int preferredLanguageId)
    {
        if (!candidates.TryGetValue(nameId, out var list) || list.Count == 0)
        {
            return null;
        }

        foreach (var candidate in list)
        {
            if (candidate.LanguageId == preferredLanguageId)
            {
                return candidate.Value;
            }
        }

        return list[0].Value;
    }

    /// <summary>
    ///     Decodes a Windows-platform name record's bytes as big-endian UTF-16.
    /// </summary>
    private static string DecodeWindowsUnicode(byte[] data, int offset, int length) =>
        Encoding.BigEndianUnicode.GetString(data, offset, length);

    /// <summary>
    ///     Decodes a Macintosh-platform (Roman) name record's bytes, one byte per character: bytes
    ///     below <c>0x80</c> map directly to the same ASCII codepoint, and bytes at or above
    ///     <c>0x80</c> map through <see cref="MacRomanHighBytes"/>.
    /// </summary>
    private static string DecodeMacRoman(byte[] data, int offset, int length) =>
        string.Create(length, (data, offset), static (span, state) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                var b = state.data[state.offset + i];
                span[i] = b < 0x80 ? (char)b : MacRomanHighBytes[b - 0x80];
            }
        });
}
