namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     The kind of a file-scope directive that references another source file.
/// </summary>
/// <remarks>
///     The members tolerate additions: consumers should handle unknown values defensively.
/// </remarks>
public enum ScadDirectiveKind
{
    /// <summary>An <c>include &lt;path&gt;</c> directive, which brings in the referenced file's statements.</summary>
    Include,

    /// <summary>A <c>use &lt;path&gt;</c> directive, which brings in only the referenced file's modules and functions.</summary>
    Use,
}
