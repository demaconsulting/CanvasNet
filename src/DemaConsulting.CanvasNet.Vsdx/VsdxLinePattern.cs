namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     Maps a Visio <c>LinePattern</c> cell value to a dash array expressed in multiples of the
///     line weight (alternating dash/gap lengths), so the dash geometry scales with the stroke
///     width exactly as Visio's own patterns do.
/// </summary>
internal static class VsdxLinePattern
{
    /// <summary>
    ///     Built-in pattern table indexed by <c>LinePattern</c> value (index 0 and 1 are none/solid
    ///     and unused). Patterns 2-5 (dash, dot, dash-dot, dash-dot-dot) are the canonical Visio
    ///     shapes; the longer patterns (6-22) follow Visio's published ordering (longer dashes
    ///     and dash/dot combinations) but their exact segment lengths are an approximation.
    /// </summary>
    private static readonly float[]?[] Table =
    [
        null,
        null,
        [4f, 3f],
        [1f, 3f],
        [4f, 3f, 1f, 3f],
        [4f, 3f, 1f, 3f, 1f, 3f],
        [9f, 3f, 1f, 3f],
        [9f, 3f, 1f, 3f, 1f, 3f],
        [9f, 3f, 4f, 3f],
        [9f, 3f, 4f, 3f, 4f, 3f],
        [4f, 3f, 4f, 3f, 1f, 3f],
        [1f, 2f],
        [2f, 2f],
        [6f, 3f],
        [6f, 3f, 1f, 3f],
        [9f, 3f],
        [9f, 5f],
        [12f, 3f],
        [12f, 3f, 1f, 3f],
        [12f, 3f, 1f, 3f, 1f, 3f],
        [12f, 3f, 4f, 3f],
        [12f, 3f, 4f, 3f, 4f, 3f],
        [12f, 6f]
    ];

    /// <summary>Highest built-in pattern index with a table entry.</summary>
    private const int MaxBuiltIn = 22;

    /// <summary>Resolves a raw <c>LinePattern</c> value to its dash array.</summary>
    /// <param name="raw">The raw cell value (may be <see langword="null"/> or non-numeric, e.g. <c>Themed</c>).</param>
    /// <param name="feature">
    ///     When the pattern is approximated or unsupported (and therefore worth reporting), the
    ///     stable feature token; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>Dash/gap lengths in multiples of line weight, or <see langword="null"/> for a solid line.</returns>
    internal static float[]? Resolve(string? raw, out string? feature)
    {
        feature = null;
        if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var pattern) ||
            pattern <= 1)
        {
            return null;
        }

        if (pattern > MaxBuiltIn)
        {
            // 23 is the custom (StripeSizes) pattern; anything above is undefined.
            feature = $"vsdx-linepattern-{pattern}";
            return null;
        }

        if (pattern > 5)
        {
            feature = $"vsdx-linepattern-{pattern}-approximated";
        }

        return Table[pattern];
    }
}
