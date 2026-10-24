namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     A file-scope <c>include</c> or <c>use</c> directive found in a parsed document.
/// </summary>
/// <remarks>
///     Directives are only recorded: parsing never opens, resolves or validates the referenced file, so a
///     directive may name a file that does not exist. The path is the exact text between the angle brackets,
///     without trimming.
/// </remarks>
/// <param name="Kind">Whether the directive is an <c>include</c> or a <c>use</c>.</param>
/// <param name="Path">The text between the angle brackets (never empty).</param>
/// <param name="Location">The location of the directive keyword.</param>
public sealed record ScadDirective(ScadDirectiveKind Kind, string Path, ScadSourceLocation Location);
