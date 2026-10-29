using System.Globalization;

namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     A position in a Vega-Lite specification, used to point diagnostics, data references and exceptions at the
///     construct they describe.
/// </summary>
/// <remarks>
///     <para>
///         A location carries two complementary coordinates because JSON has both a text position and a
///         structural position: the one-based <see cref="Line"/> and <see cref="Column"/> help an author find the
///         text in an editor, and the RFC 6901 JSON <see cref="Pointer"/> (for example <c>/layer/1/encoding/x</c>)
///         identifies the construct independently of formatting, which is what tools and tests should compare.
///     </para>
///     <para>
///         Lines and columns are one-based. A column counts UTF-16 code units and a tab counts as one column, so
///         locations are deterministic and independent of the platform and of editor tab width. A line ends at
///         <c>\n</c> (a preceding <c>\r</c> belongs to the previous line); a lone <c>\r</c> is JSON white space and
///         does not end a line.
///     </para>
///     <para>
///         <c>default(VegaLiteLocation)</c> has <see cref="Line"/> equal to zero and an empty pointer and means
///         "unknown". The type is an immutable value type with value equality and is safe for concurrent use.
///     </para>
/// </remarks>
/// <param name="File">The source name (file path) as supplied by the caller, or <see langword="null"/> when the source is unnamed.</param>
/// <param name="Pointer">
///     The RFC 6901 JSON pointer of the construct; the empty string denotes the whole specification (and is
///     also used when the construct has no structural position, such as malformed JSON).
/// </param>
/// <param name="Line">The one-based line number, or zero when the text position is unknown.</param>
/// <param name="Column">The one-based column number in UTF-16 code units, or zero when the text position is unknown.</param>
public readonly record struct VegaLiteLocation(string? File, string Pointer, int Line, int Column)
{
    /// <summary>
    ///     Gets a value indicating whether the location carries any information (a text position or a pointer).
    /// </summary>
    /// <remarks>
    ///     Messages of exceptions and diagnostics only prefix a location when it is known, because an empty prefix
    ///     (<c>?</c>) would be noise.
    /// </remarks>
    public bool IsKnown => Line != 0 || !string.IsNullOrEmpty(Pointer);

    /// <summary>
    ///     Formats the location for use as a diagnostic prefix.
    /// </summary>
    /// <returns>
    ///     <c>{File}:{Line}:{Column}</c> (or <c>{Line}:{Column}</c> when <see cref="File"/> is
    ///     <see langword="null"/>, or <c>?</c> in place of the coordinates when <see cref="Line"/> is zero),
    ///     followed by a space and the <see cref="Pointer"/> when the pointer is not empty, for example
    ///     <c>spec.vl.json:3:7 /layer/1/encoding/x</c>. Numbers use the invariant culture.
    /// </returns>
    public override string ToString()
    {
        // The text position comes first because editors and build tools recognize the file:line:column form
        var position = Line == 0 ? "?" : string.Create(CultureInfo.InvariantCulture, $"{Line}:{Column}");
        var text = File is null ? position : $"{File}:{position}";

        // The pointer follows, separated by a space, when the construct has a structural position
        return string.IsNullOrEmpty(Pointer) ? text : $"{text} {Pointer}";
    }
}
