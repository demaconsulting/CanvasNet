// Portions derived from Vega-Lite and Vega (BSD-3-Clause), d3-scale-chromatic (ISC); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <summary>
///     The category colors of the Vega-Lite default configuration, and lookup of a color scheme by name.
/// </summary>
/// <remarks>
///     <para>
///         The only scheme in this package is <c>tableau10</c>, the default category palette of Vega-Lite (the ten
///         colors of its <c>config.range.category</c>). Other scheme names (ColorBrewer, matplotlib and so on) are not
///         available yet; <see cref="TryGetScheme"/> reports that and the scale resolver falls back to
///         <c>tableau10</c>, so a chart is still drawn with distinct colors.
///     </para>
///     <para>
///         Lookup by category is deterministic: category number <c>i</c> gets color <c>i mod 10</c>. The type is
///         stateless and thread-safe.
///     </para>
/// </remarks>
internal static class ColorSchemes
{
    /// <summary>The name of the default category scheme.</summary>
    public const string Tableau10Name = "tableau10";

    /// <summary>The colors of <c>tableau10</c>, as CSS hex colors, in category order.</summary>
    private static readonly string[] Tableau10Colors =
    [
        "#4c78a8",
        "#f58518",
        "#e45756",
        "#72b7b2",
        "#54a24b",
        "#eeca3b",
        "#b279a2",
        "#ff9da6",
        "#9d755d",
        "#bab0ac",
    ];

    /// <summary>Gets the colors of <c>tableau10</c>, in category order.</summary>
    public static IReadOnlyList<string> Tableau10 => Tableau10Colors;

    /// <summary>
    ///     Looks up a color scheme by name.
    /// </summary>
    /// <param name="name">The scheme name; the comparison ignores case, as Vega does.</param>
    /// <param name="colors">Receives the colors of the scheme.</param>
    /// <returns><see langword="true"/> when the scheme is available (only <c>tableau10</c> is).</returns>
    public static bool TryGetScheme(string? name, out IReadOnlyList<string> colors)
    {
        if (string.Equals(name, Tableau10Name, StringComparison.OrdinalIgnoreCase))
        {
            colors = Tableau10Colors;
            return true;
        }

        colors = [];
        return false;
    }

    /// <summary>
    ///     Gets the color of a category by its position, cycling through the colors of the scheme.
    /// </summary>
    /// <param name="scheme">The colors; must not be empty.</param>
    /// <param name="index">The zero-based category position; must not be negative.</param>
    /// <returns>The color <c>index mod count</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scheme"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="scheme"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative.</exception>
    public static string CategoryColor(IReadOnlyList<string> scheme, int index)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (scheme.Count == 0)
        {
            throw new ArgumentException("The scheme must have at least one color.", nameof(scheme));
        }

        return scheme[index % scheme.Count];
    }

    /// <summary>
    ///     Gets the <c>tableau10</c> color of a category by its position.
    /// </summary>
    /// <param name="index">The zero-based category position; must not be negative.</param>
    /// <returns>The color <c>index mod 10</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative.</exception>
    public static string Tableau10Color(int index) => CategoryColor(Tableau10Colors, index);
}
