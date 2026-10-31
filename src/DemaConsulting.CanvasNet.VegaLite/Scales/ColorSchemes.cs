// Portions derived from Vega (BSD-3-Clause), d3-scale-chromatic (ISC; ColorBrewer colors Apache-2.0), matplotlib colormaps (CC0), cividis (Battelle license) and Turbo (Apache-2.0); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <summary>
///     What a color range is used for; it decides how many colors a scheme produces and whether they are discrete.
/// </summary>
internal enum SchemeUse
{
    /// <summary>A continuous (sequential or diverging) scale: the range is an interpolator over 0 to 1.</summary>
    Continuous,

    /// <summary>An ordinal scale: one color per category (a categorical scheme is used whole and cycles).</summary>
    Ordinal,

    /// <summary>A quantile scale: <c>schemeCount</c> colors, 5 by default.</summary>
    Quantile,

    /// <summary>A quantize scale: <c>schemeCount</c> colors, 5 by default.</summary>
    Quantize,

    /// <summary>A threshold scale: one more color than there are thresholds.</summary>
    Threshold,

    /// <summary>A bin-ordinal scale: one color per bin, one less than there are bin edges.</summary>
    BinOrdinal,
}

/// <summary>
///     A named color scheme: a list of colors that is either categorical or the stops of a continuous ramp.
/// </summary>
/// <remarks>
///     The colors of a scheme from the catalog are decoded from the compact table the first time they are needed
///     (thread-safe, once); a scheme built from user colors is complete at creation. A scheme is immutable.
/// </remarks>
internal sealed class ColorScheme
{
    /// <summary>The number of colors of the scheme.</summary>
    private readonly int _count;

    /// <summary>The colors, decoded on first use.</summary>
    private readonly Lazy<RgbColor[]> _colors;

    /// <summary>The CSS texts of the colors, built on first use.</summary>
    private readonly Lazy<string[]> _css;

    /// <summary>The ramp through the colors, built on first use.</summary>
    private readonly Lazy<ColorRamp> _ramp;

    /// <summary>
    ///     Initializes a scheme of the catalog.
    /// </summary>
    /// <param name="name">The name as Vega documents it.</param>
    /// <param name="isContinuous"><see langword="true"/> for a continuous ramp, <see langword="false"/> for a categorical scheme.</param>
    /// <param name="hex">The colors as one run of six-digit hexadecimal colors.</param>
    internal ColorScheme(string name, bool isContinuous, string hex)
    {
        Name = name;
        IsContinuous = isContinuous;
        _count = hex.Length / 6;
        _colors = new Lazy<RgbColor[]>(() => Decode(name, hex), LazyThreadSafetyMode.ExecutionAndPublication);
        _css = new Lazy<string[]>(() => Array.ConvertAll(_colors.Value, color => color.ToCss()), LazyThreadSafetyMode.ExecutionAndPublication);
        _ramp = new Lazy<ColorRamp>(() => new ColorRamp(InterpolationSpace.Rgb, _colors.Value), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    ///     Initializes a continuous scheme from user colors.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="colors">The stops, at least one.</param>
    /// <param name="ramp">The ramp through the stops.</param>
    private ColorScheme(string name, RgbColor[] colors, ColorRamp ramp)
    {
        Name = name;
        IsContinuous = true;
        _count = colors.Length;
        _colors = new Lazy<RgbColor[]>(colors);
        _css = new Lazy<string[]>(() => Array.ConvertAll(colors, color => color.ToCss()), LazyThreadSafetyMode.ExecutionAndPublication);
        _ramp = new Lazy<ColorRamp>(ramp);
    }

    /// <summary>Gets the name of the scheme (<c>custom</c> for a scheme built from colors).</summary>
    public string Name { get; }

    /// <summary>Gets a value indicating whether the colors are the stops of a continuous ramp (otherwise they are categories).</summary>
    public bool IsContinuous { get; }

    /// <summary>Gets the number of colors (categories or stops).</summary>
    public int Count => _count;

    /// <summary>Gets the colors in order.</summary>
    public IReadOnlyList<RgbColor> Colors => _colors.Value;

    /// <summary>Gets the colors in order as <c>#rrggbb</c> CSS texts.</summary>
    public IReadOnlyList<string> CssColors => _css.Value;

    /// <summary>
    ///     Gets the ramp that interpolates piecewise through the colors; for a categorical scheme this is linear sRGB
    ///     interpolation through the categories.
    /// </summary>
    public ColorRamp Ramp => _ramp.Value;

    /// <summary>
    ///     Creates a continuous scheme from colors, interpolated piecewise (a <c>scale.range</c> or <c>scheme</c> that is an array of colors).
    /// </summary>
    /// <param name="colors">The stops, at least one.</param>
    /// <param name="space">The interpolation space; sRGB by default.</param>
    /// <param name="gamma">The gamma of the sRGB space.</param>
    /// <returns>The scheme.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="colors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="colors"/> is empty.</exception>
    public static ColorScheme FromColors(IReadOnlyList<RgbColor> colors, InterpolationSpace space = InterpolationSpace.Rgb, double gamma = 1)
    {
        var ramp = new ColorRamp(space, colors, gamma);
        return new ColorScheme("custom", [.. colors], ramp);
    }

    /// <summary>
    ///     Decodes a run of six-digit hexadecimal colors.
    /// </summary>
    /// <param name="name">The scheme name, for the message of a corrupt table.</param>
    /// <param name="hex">The run.</param>
    /// <returns>The colors.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the table is not a run of six-digit colors (a defect in the table).</exception>
    private static RgbColor[] Decode(string name, string hex)
    {
        if (hex.Length == 0 || hex.Length % 6 != 0)
        {
            throw new InvalidOperationException($"The table of the color scheme '{name}' is not a run of six-digit colors.");
        }

        var colors = new RgbColor[hex.Length / 6];
        for (var i = 0; i < colors.Length; i++)
        {
            if (!RgbColor.TryParseHex(hex.AsSpan(i * 6, 6), out colors[i]))
            {
                throw new InvalidOperationException($"The table of the color scheme '{name}' has an invalid color at position {i}.");
            }
        }

        return colors;
    }
}

/// <summary>
///     The colors a scheme gives to a scale: an interpolator for a continuous scale or a list for a discrete one.
/// </summary>
internal sealed class SchemeRange
{
    /// <summary>
    ///     Initializes a continuous range.
    /// </summary>
    /// <param name="interpolator">The interpolator over 0 to 1.</param>
    internal SchemeRange(Func<double, RgbColor> interpolator)
    {
        Interpolator = interpolator;
    }

    /// <summary>
    ///     Initializes a discrete range.
    /// </summary>
    /// <param name="colors">The colors.</param>
    internal SchemeRange(IReadOnlyList<RgbColor> colors)
    {
        Colors = colors;
    }

    /// <summary>Gets the interpolator over 0 to 1, or <see langword="null"/> for a discrete range.</summary>
    public Func<double, RgbColor>? Interpolator { get; }

    /// <summary>Gets the colors, or <see langword="null"/> for a continuous range.</summary>
    public IReadOnlyList<RgbColor>? Colors { get; }

    /// <summary>Gets a value indicating whether the range is an interpolator.</summary>
    public bool IsContinuous => Interpolator is not null;

    /// <summary>
    ///     Gets the colors of a discrete range as CSS texts.
    /// </summary>
    /// <returns>The <c>#rrggbb</c> texts; empty for a continuous range.</returns>
    public IReadOnlyList<string> ToCssList() => Colors is null ? [] : [.. Colors.Select(color => color.ToCss())];
}

/// <summary>
///     The Vega color schemes (categorical lists and continuous ramps), the lookup of a scheme by name and the
///     configuration of a scale range from a scheme.
/// </summary>
/// <remarks>
///     <para>
///         The catalog has 68 schemes, listed in <c>ColorSchemeData.cs</c>: 15 categorical (<c>tableau10</c>, the
///         default category palette of Vega-Lite, among them) and 53 continuous. The names are Vega's and the lookup
///         ignores the letter case, as Vega's <c>scheme</c> function does. A name that is not in the catalog is not
///         found; a caller that reads it from a specification decides how to fail (Vega raises an error).
///     </para>
///     <para>
///         <see cref="Configure"/> ports the way Vega's <c>scale</c> transform turns a scheme into a range
///         (<c>configureScheme</c>): a continuous scheme is an interpolator, optionally over an <c>extent</c> (a
///         sub-range of 0 to 1, in either direction) and reversed; a discrete range takes the whole categorical list
///         (ordinal scales), the first <c>count</c> colors of it, or <c>count</c> samples of the interpolator taken at
///         <c>i / (count + 1)</c> (<see cref="ColorInterpolator.Quantize"/>, so the two end colors are not used). The
///         number of colors is the <c>schemeCount</c> or its default for quantile and quantize scales (5), one more
///         than the thresholds for a threshold scale, one less than the bin edges for a bin-ordinal scale, and for an
///         ordinal scale the <c>schemeCount</c>, else the size of the domain, else 5.
///     </para>
///     <para>
///         <b>Limits.</b> A discrete range never holds more colors than the <c>maxColors</c> argument (a
///         <see cref="VegaLiteRenderException"/> otherwise, whose message names <c>VegaLiteLimits.MaxScaleDomainSize</c>
///         for the tool's limit mapping; the caller adds the location; a categorical scheme used whole for an ordinal
///         scale has at most 20 colors and is exempt); a long list polls the cancellation token every
///         4,096 colors. The decoded tables are static data of at most 64 colors. The type is stateless and
///         thread-safe; results depend on nothing but the arguments, so they are deterministic.
///     </para>
/// </remarks>
internal static partial class ColorSchemes
{
    /// <summary>The name of the default category scheme.</summary>
    public const string Tableau10Name = "tableau10";

    /// <summary>The number of colors of a quantile or quantize range that has no explicit count (Vega's default).</summary>
    public const int DefaultCount = 5;

    /// <summary>The catalog in order, and the lookup by lower-case name.</summary>
    private static readonly Lazy<(ColorScheme[] Schemes, Dictionary<string, ColorScheme> ByName)> Catalog =
        new(BuildLookup, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Gets the 68 schemes of the catalog: the categorical ones in alphabetical order, then the continuous ones.</summary>
    public static IReadOnlyList<ColorScheme> All => Catalog.Value.Schemes;

    /// <summary>Gets the colors of <c>tableau10</c>, as CSS hex colors, in category order.</summary>
    public static IReadOnlyList<string> Tableau10 => Catalog.Value.ByName[Tableau10Name].CssColors;

    /// <summary>
    ///     Looks up a scheme of the catalog by name.
    /// </summary>
    /// <param name="name">The scheme name; the comparison ignores case, as Vega does.</param>
    /// <param name="scheme">Receives the scheme.</param>
    /// <returns><see langword="true"/> when the catalog has the scheme.</returns>
    public static bool TryGetColorScheme(string? name, out ColorScheme scheme)
    {
        if (name is not null && Catalog.Value.ByName.TryGetValue(name.ToLowerInvariant(), out var found))
        {
            scheme = found;
            return true;
        }

        scheme = null!;
        return false;
    }

    /// <summary>
    ///     Looks up a categorical scheme by name and gives its colors (the form the scale resolver uses for ordinal color ranges).
    /// </summary>
    /// <param name="name">The scheme name; the comparison ignores case.</param>
    /// <param name="colors">Receives the colors as CSS hex colors, in category order; empty when the result is <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the scheme exists and is categorical; a continuous scheme (for example <c>blues</c>) and an unknown name give <see langword="false"/>.</returns>
    public static bool TryGetScheme(string? name, out IReadOnlyList<string> colors)
    {
        if (TryGetColorScheme(name, out var scheme) && !scheme.IsContinuous)
        {
            colors = scheme.CssColors;
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
    public static string Tableau10Color(int index) => CategoryColor(Tableau10, index);

    /// <summary>
    ///     Turns a scheme into the range of a scale.
    /// </summary>
    /// <param name="scheme">The scheme.</param>
    /// <param name="use">The kind of scale the range is for.</param>
    /// <param name="schemeCount">The explicit number of colors (<c>scheme.count</c>), or <see langword="null"/>; a value below 1 counts as absent.</param>
    /// <param name="domainSize">The size of the scale domain (the categories of an ordinal scale, the thresholds of a threshold scale, the bin edges of a bin-ordinal scale).</param>
    /// <param name="extent">The part of a continuous scheme to use (<c>scheme.extent</c>), <see langword="null"/> for all of it; a start above the end reverses the colors. Ignored for a categorical scheme.</param>
    /// <param name="reverse">Reverse the colors (<c>scale.reverse</c>).</param>
    /// <param name="maxColors">The largest number of colors accepted in a discrete range.</param>
    /// <param name="cancellationToken">A token that stops a long list.</param>
    /// <returns>An interpolator for <see cref="SchemeUse.Continuous"/>, otherwise the list of colors (possibly empty).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scheme"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the discrete range would have more than <paramref name="maxColors"/> colors.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled.</exception>
    public static SchemeRange Configure(
        ColorScheme scheme,
        SchemeUse use,
        int? schemeCount = null,
        int domainSize = 0,
        (double Start, double End)? extent = null,
        bool reverse = false,
        int maxColors = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        // The part of a categorical scheme that is not a ramp ignores the extent
        var (start, end) = scheme.IsContinuous && extent is { } e ? e : (0d, 1d);
        var ramp = scheme.Ramp;
        if (use == SchemeUse.Continuous)
        {
            // Reversing a continuous range swaps the two ends of the extent
            var (first, last) = reverse ? (end, start) : (start, end);
            return new SchemeRange(ColorInterpolator.Range(ramp.At, first, last));
        }

        var count = Math.Max(0, ColorCount(use, schemeCount, domainSize));
        var wholeList = !scheme.IsContinuous && use == SchemeUse.Ordinal;
        if (!wholeList && count > maxColors)
        {
            throw new VegaLiteRenderException(
                $"The color scheme '{scheme.Name}' would give {count} colors, more than the limit of {maxColors} (VegaLiteLimits.MaxScaleDomainSize).");
        }

        RgbColor[] colors;
        if (scheme.IsContinuous)
        {
            // Sampled over the extent; the reversal is applied to the list below
            colors = ColorInterpolator.Quantize(ColorInterpolator.Range(ramp.At, start, end), count, cancellationToken);
        }
        else if (wholeList)
        {
            colors = [.. scheme.Colors];
        }
        else
        {
            colors = [.. scheme.Colors.Take(count)];
        }

        if (reverse)
        {
            Array.Reverse(colors);
        }

        return new SchemeRange(colors);
    }

    /// <summary>
    ///     Computes the number of colors of a discrete range.
    /// </summary>
    /// <param name="use">The kind of scale.</param>
    /// <param name="schemeCount">The explicit count, or <see langword="null"/>.</param>
    /// <param name="domainSize">The size of the domain.</param>
    /// <returns>The number of colors; a negative value is clamped by the caller.</returns>
    private static int ColorCount(SchemeUse use, int? schemeCount, int domainSize)
    {
        var explicitCount = schemeCount is > 0 ? schemeCount.Value : 0;
        switch (use)
        {
            case SchemeUse.Threshold:
                return domainSize == int.MaxValue ? int.MaxValue : domainSize + 1;
            case SchemeUse.BinOrdinal:
                return domainSize - 1;
            case SchemeUse.Quantile:
            case SchemeUse.Quantize:
                return explicitCount > 0 ? explicitCount : DefaultCount;
            default:
                if (explicitCount > 0)
                {
                    return explicitCount;
                }

                return domainSize > 0 ? domainSize : DefaultCount;
        }
    }

    /// <summary>
    ///     Builds the catalog and its lookup table.
    /// </summary>
    /// <returns>The schemes in catalog order and the schemes by lower-case name.</returns>
    private static (ColorScheme[] Schemes, Dictionary<string, ColorScheme> ByName) BuildLookup()
    {
        var schemes = BuildCatalog();
        var byName = new Dictionary<string, ColorScheme>(schemes.Length, StringComparer.Ordinal);
        foreach (var scheme in schemes)
        {
            byName[scheme.Name.ToLowerInvariant()] = scheme;
        }

        return (schemes, byName);
    }
}
