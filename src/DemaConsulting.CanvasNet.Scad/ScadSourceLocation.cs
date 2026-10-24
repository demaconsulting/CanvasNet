using System.Globalization;

namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     A position in OpenSCAD-language source text, used to point diagnostics and directives at the
///     construct they describe.
/// </summary>
/// <remarks>
///     <para>
///         Lines and columns are one-based. A column counts UTF-16 code units and a tab counts as a single
///         column, so locations are deterministic and independent of the platform and of editor tab width.
///         The sequences <c>\r\n</c>, <c>\n</c> and a lone <c>\r</c> each end exactly one line.
///     </para>
///     <para>
///         <c>default(ScadSourceLocation)</c> has <see cref="Line"/> equal to zero and means "unknown".
///         The type is an immutable value type with value equality and is safe for concurrent use.
///     </para>
/// </remarks>
/// <param name="File">The source name (file path) as supplied by the caller, or <see langword="null"/> when the source is unnamed.</param>
/// <param name="Line">The one-based line number, or zero when the location is unknown.</param>
/// <param name="Column">The one-based column number in UTF-16 code units, or zero when the location is unknown.</param>
public readonly record struct ScadSourceLocation(string? File, int Line, int Column)
{
    /// <summary>
    ///     Formats the location for use as a diagnostic prefix.
    /// </summary>
    /// <returns>
    ///     <c>{File}:{Line}:{Column}</c>, or <c>{Line}:{Column}</c> when <see cref="File"/> is
    ///     <see langword="null"/>, or <c>?</c> when the location is unknown (<see cref="Line"/> is zero).
    /// </returns>
    public override string ToString()
    {
        // An unknown location carries no usable coordinates, so it is rendered as a placeholder
        if (Line == 0)
        {
            return "?";
        }

        // Numbers are formatted with the invariant culture so that diagnostics are identical everywhere
        var position = string.Create(CultureInfo.InvariantCulture, $"{Line}:{Column}");
        return File is null ? position : $"{File}:{position}";
    }
}
