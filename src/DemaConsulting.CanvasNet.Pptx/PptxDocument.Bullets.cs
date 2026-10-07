using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Pptx;

/// <summary>
///     Implements the <see cref="PptxDocument"/> <c>&lt;a:buAutoNum&gt;</c> rendered-string
///     formatter (Phase 2 Follow-Up: Bullets and Numbering).
/// </summary>
/// <remarks>
///     <para>
///         The OOXML schema defines a much larger <c>ST_TextAutonumberScheme</c> enumeration (for
///         example double-byte/Asian/Thai/Hindi numbering schemes) than this phase supports. This
///         deliberately supports only the eleven Latin-numeral variants listed below - the
///         overwhelmingly common case in real-world decks - and fails gracefully, per-bullet, for
///         every other (recognized-but-unsupported) <c>type</c> value: <see cref="FormatAutoNumber"/>
///         returns <see langword="null"/>, and the caller (<c>PptxDocument.TextLayout.cs</c>'s
///         <c>BuildLines</c>) simply paints no glyph for that one paragraph while the paragraph's
///         text itself still renders normally and the auto-number counter state still advances -
///         this mirrors the broader codebase's established convention for optional, nullable-
///         returning resolvers (see <c>PptxDocument.TextInheritance.cs</c>'s <c>GetFontSizeEmu</c>/
///         <c>ParseLineSpacing</c> et al.), rather than the separate
///         <see cref="PptxUnsupportedFeatureException"/> convention used for malformed/unsupported
///         constructs that abort an entire render.
///     </para>
/// </remarks>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     Formats a 1-based auto-number counter value into its rendered bullet string for a
    ///     <c>&lt;a:buAutoNum type="..."/&gt;</c> scheme, or <see langword="null"/> for an
    ///     unrecognized/unsupported <paramref name="type"/> (see this class's remarks).
    /// </summary>
    /// <param name="value">The 1-based counter value (see <c>PptxDocument.TextLayout.cs</c>'s auto-number counter state machine).</param>
    /// <param name="type">The <c>&lt;a:buAutoNum type="..."/&gt;</c> scheme name, for example <c>"arabicPeriod"</c>.</param>
    /// <returns>The rendered bullet string, or <see langword="null"/> for an unsupported <paramref name="type"/>.</returns>
    internal static string? FormatAutoNumber(int value, string type) =>
        type switch
        {
            "arabicPeriod" => value.ToString(CultureInfo.InvariantCulture) + ".",
            "arabicParenR" => value.ToString(CultureInfo.InvariantCulture) + ")",
            "arabicPlain" => value.ToString(CultureInfo.InvariantCulture),
            "alphaLcPeriod" => FormatAlpha(value, upper: false) + ".",
            "alphaUcPeriod" => FormatAlpha(value, upper: true) + ".",
            "alphaLcParenR" => FormatAlpha(value, upper: false) + ")",
            "alphaUcParenR" => FormatAlpha(value, upper: true) + ")",
            "romanLcPeriod" => FormatRoman(value, upper: false) + ".",
            "romanUcPeriod" => FormatRoman(value, upper: true) + ".",
            "romanLcParenR" => FormatRoman(value, upper: false) + ")",
            "romanUcParenR" => FormatRoman(value, upper: true) + ")",
            _ => null,
        };

    /// <summary>
    ///     Formats a 1-based counter value as a base-26 "spreadsheet column"-style alphabetic
    ///     sequence (<c>a, b, ..., z, aa, ab, ...</c>), matching PowerPoint's own
    ///     <c>alphaLc*</c>/<c>alphaUc*</c> auto-number rendering for values beyond 26.
    /// </summary>
    private static string FormatAlpha(int value, bool upper)
    {
        if (value < 1)
        {
            value = 1;
        }

        var builder = new StringBuilder();
        var remaining = value;
        while (remaining > 0)
        {
            remaining--;
            var digit = (char)('a' + (remaining % 26));
            builder.Insert(0, digit);
            remaining /= 26;
        }

        var result = builder.ToString();
        return upper ? result.ToUpperInvariant() : result;
    }

    /// <summary>
    ///     Formats a 1-based counter value as a standard subtractive-notation Roman numeral,
    ///     matching PowerPoint's own <c>romanLc*</c>/<c>romanUc*</c> auto-number rendering. No
    ///     upper value cap is applied - large values naturally produce a long run of repeated
    ///     <c>"M"</c> thousands, mirroring the unbounded nature of a real auto-numbered list.
    /// </summary>
    private static string FormatRoman(int value, bool upper)
    {
        if (value < 1)
        {
            value = 1;
        }

        var builder = new StringBuilder();
        var remaining = value;
        foreach (var (romanValue, symbol) in RomanNumerals)
        {
            while (remaining >= romanValue)
            {
                builder.Append(symbol);
                remaining -= romanValue;
            }
        }

        var result = builder.ToString();
        return upper ? result : result.ToLowerInvariant();
    }

    /// <summary>The standard subtractive-notation Roman numeral value/symbol table, in descending order.</summary>
    private static readonly (int Value, string Symbol)[] RomanNumerals =
    [
        (1000, "M"),
        (900, "CM"),
        (500, "D"),
        (400, "CD"),
        (100, "C"),
        (90, "XC"),
        (50, "L"),
        (40, "XL"),
        (10, "X"),
        (9, "IX"),
        (5, "V"),
        (4, "IV"),
        (1, "I"),
    ];
}
