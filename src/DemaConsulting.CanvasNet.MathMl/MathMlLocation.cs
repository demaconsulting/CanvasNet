using System.Globalization;

namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     A position in MathML text, used to point diagnostics, unsupported-feature reports and exceptions at the
///     construct they describe.
/// </summary>
/// <remarks>
///     <para>
///         The position is always one in the <b>original</b> text as the caller supplied it: the named character
///         references that the reader rewrites before the XML is parsed do not shift it, so an author or an agent can go
///         straight to the character in an editor.
///     </para>
///     <para>
///         Lines and columns are one-based. A column counts UTF-16 code units and a tab counts as one column, as the XML
///         reader of .NET counts them, so locations are deterministic and independent of the platform and of editor tab
///         width. A line ends at <c>\n</c>, at <c>\r\n</c> or at a lone <c>\r</c>.
///     </para>
///     <para>
///         <c>default(MathMlLocation)</c> has <see cref="Line"/> equal to zero and means "unknown". The type is an
///         immutable value type with value equality and is safe for concurrent use.
///     </para>
/// </remarks>
/// <param name="SourceName">The source name (file path) as supplied by the caller, or <see langword="null"/> when the source is unnamed.</param>
/// <param name="Line">The one-based line number, or zero when the position is unknown.</param>
/// <param name="Column">The one-based column number in UTF-16 code units, or zero when the position is unknown.</param>
public readonly record struct MathMlLocation(string? SourceName, int Line, int Column)
{
    /// <summary>
    ///     Gets a value indicating whether the location carries a text position.
    /// </summary>
    /// <remarks>
    ///     Messages of exceptions and diagnostics only prefix a location when it is known, because an empty prefix
    ///     (<c>?</c>) would be noise.
    /// </remarks>
    public bool IsKnown => Line != 0;

    /// <summary>
    ///     Formats the location for use as a diagnostic prefix.
    /// </summary>
    /// <returns>
    ///     <c>{SourceName}:{Line}:{Column}</c>, <c>{Line}:{Column}</c> when <see cref="SourceName"/> is
    ///     <see langword="null"/>, or <c>?</c> in place of the coordinates when <see cref="Line"/> is zero (for example
    ///     <c>quadratic.mml:3:7</c>). Numbers use the invariant culture.
    /// </returns>
    public override string ToString()
    {
        // The file:line:column form is the one editors and build tools recognize
        var position = Line == 0 ? "?" : string.Create(CultureInfo.InvariantCulture, $"{Line}:{Column}");
        return SourceName is null ? position : $"{SourceName}:{position}";
    }
}
