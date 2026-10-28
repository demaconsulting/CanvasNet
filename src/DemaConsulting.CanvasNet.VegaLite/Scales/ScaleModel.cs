// Portions derived from d3-scale (ISC, Mike Bostock); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <summary>
///     The kind of a <see cref="ScaleModel"/>.
/// </summary>
internal enum ScaleKind
{
    /// <summary>A linear scale from a numeric domain to a numeric range.</summary>
    Linear,

    /// <summary>A band scale: each category of a discrete domain gets an equal-width band of the range.</summary>
    Band,

    /// <summary>A point scale: a band scale whose bands have no width, so each category is one position.</summary>
    Point,

    /// <summary>An ordinal scale: each category of a discrete domain gets the next value of a discrete range (cycling).</summary>
    Ordinal,
}

/// <summary>
///     An immutable scale: the mapping from data values to positions, sizes, opacities or colors.
/// </summary>
/// <remarks>
///     <para>
///         The model is a C# version of the d3-scale 4 scales Vega-Lite uses for its first charts: <c>scaleLinear</c>
///         (<see cref="ScaleKind.Linear"/>, with <c>clamp</c> and <c>round</c> and a reversed domain or range), <c>scaleBand</c>
///         (<see cref="ScaleKind.Band"/>, with inner and outer padding, alignment, rounding and a reversed range),
///         <c>scalePoint</c> (<see cref="ScaleKind.Point"/>) and <c>scaleOrdinal</c> (<see cref="ScaleKind.Ordinal"/>). The arithmetic of
///         d3 is kept, so for example the band step is
///         <c>(stop - start) / max(1, n - paddingInner + 2 * paddingOuter)</c> and the first band starts at
///         <c>start + (stop - start - step * (n - paddingInner)) * align</c>.
///     </para>
///     <para>
///         A model never changes after it is created, which also means that an ordinal model does not learn new
///         categories while mapping (d3's implicit domain): the domain is built beforehand, in the order of first
///         appearance or sorted, by <see cref="ScaleResolver"/>, and a value outside the domain maps to nothing.
///         Instances are safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class ScaleModel
{
    /// <summary>The distinct categories of a discrete domain in order.</summary>
    private readonly DataValue[] _domain;

    /// <summary>The position of every category of a discrete domain.</summary>
    private readonly Dictionary<DataValue, int> _index;

    /// <summary>The band start of every category of a band or point scale.</summary>
    private readonly double[] _positions;

    /// <summary>The range of an ordinal scale.</summary>
    private readonly string[] _ordinalRange;

    /// <summary>
    ///     Initializes a model.
    /// </summary>
    /// <param name="kind">The kind of scale.</param>
    /// <param name="domain">The categories of a discrete domain (deduplicated here).</param>
    /// <param name="ordinalRange">The range of an ordinal scale.</param>
    /// <param name="positions">The band starts of a band or point scale.</param>
    private ScaleModel(ScaleKind kind, IEnumerable<DataValue> domain, string[] ordinalRange, double[]? positions = null)
    {
        Kind = kind;
        _ordinalRange = ordinalRange;
        _index = [];
        var list = new List<DataValue>();
        foreach (var value in domain.Distinct())
        {
            _index[value] = list.Count;
            list.Add(value);
        }

        _domain = [.. list];
        _positions = positions ?? [];
    }

    /// <summary>Gets the kind of scale.</summary>
    public ScaleKind Kind { get; }

    /// <summary>Gets the start of the numeric domain of a linear scale.</summary>
    public double DomainStart { get; private init; }

    /// <summary>Gets the end of the numeric domain of a linear scale.</summary>
    public double DomainEnd { get; private init; }

    /// <summary>Gets the start of the numeric range of a linear, band or point scale.</summary>
    public double RangeStart { get; private init; }

    /// <summary>Gets the end of the numeric range of a linear, band or point scale.</summary>
    public double RangeEnd { get; private init; }

    /// <summary>Gets a value indicating whether a linear scale clamps values to its domain.</summary>
    public bool Clamp { get; private init; }

    /// <summary>Gets a value indicating whether a linear scale rounds its results to whole numbers, or a band scale rounds its step and positions.</summary>
    public bool Round { get; private init; }

    /// <summary>Gets the inner padding of a band scale (a fraction of the step between bands); 1 for a point scale.</summary>
    public double PaddingInner { get; private init; }

    /// <summary>Gets the outer padding of a band or point scale (in steps, before the first and after the last band).</summary>
    public double PaddingOuter { get; private init; }

    /// <summary>Gets the alignment of a band or point scale: how the unused space is split (0 puts the bands at the start, 1 at the end).</summary>
    public double Align { get; private init; } = 0.5;

    /// <summary>Gets the distance between the starts of two neighboring bands of a band or point scale.</summary>
    public double Step { get; private init; }

    /// <summary>Gets the width of a band of a band scale (zero for a point scale).</summary>
    public double Bandwidth { get; private init; }

    /// <summary>Gets the categories of a discrete domain, without duplicates, in order.</summary>
    public IReadOnlyList<DataValue> Domain => _domain;

    /// <summary>Gets the range of an ordinal scale.</summary>
    public IReadOnlyList<string> OrdinalRange => _ordinalRange;

    /// <summary>
    ///     Computes the width a band or point scale needs so that every band has the given step (Vega's
    ///     <c>bandspace</c>).
    /// </summary>
    /// <remarks>
    ///     The range of a scale with a fixed step is <c>step * bandSpace(count, paddingInner, paddingOuter)</c>; an empty
    ///     domain needs no space.
    /// </remarks>
    /// <param name="count">The number of categories.</param>
    /// <param name="paddingInner">The inner padding (1 for a point scale).</param>
    /// <param name="paddingOuter">The outer padding.</param>
    /// <returns>The number of steps the range spans.</returns>
    public static double BandSpace(int count, double paddingInner, double paddingOuter)
    {
        var space = count - paddingInner + (paddingOuter * 2);
        if (count <= 0)
        {
            return 0;
        }

        return space > 0 ? space : 1;
    }

    /// <summary>
    ///     Creates a linear scale.
    /// </summary>
    /// <param name="domainStart">The first end of the domain.</param>
    /// <param name="domainEnd">The second end of the domain.</param>
    /// <param name="rangeStart">The value that <paramref name="domainStart"/> maps to.</param>
    /// <param name="rangeEnd">The value that <paramref name="domainEnd"/> maps to.</param>
    /// <param name="clamp">Whether values outside the domain are clamped to its ends.</param>
    /// <param name="round">Whether results are rounded to whole numbers.</param>
    /// <returns>The scale.</returns>
    public static ScaleModel CreateLinear(double domainStart, double domainEnd, double rangeStart, double rangeEnd, bool clamp = false, bool round = false) =>
        new(ScaleKind.Linear, [], [])
        {
            DomainStart = domainStart,
            DomainEnd = domainEnd,
            RangeStart = rangeStart,
            RangeEnd = rangeEnd,
            Clamp = clamp,
            Round = round,
        };

    /// <summary>
    ///     Creates a band scale.
    /// </summary>
    /// <remarks>
    ///     The range may run backwards (<paramref name="rangeStart"/> above <paramref name="rangeEnd"/>): the bands are
    ///     laid out in the reversed order. The paddings are in units of the step: the inner padding (at most 1) is the
    ///     space between bands, the outer padding the space before the first and after the last.
    /// </remarks>
    /// <param name="domain">The categories (duplicates are ignored).</param>
    /// <param name="rangeStart">The first end of the range.</param>
    /// <param name="rangeEnd">The second end of the range.</param>
    /// <param name="paddingInner">The inner padding; limited to at most 1.</param>
    /// <param name="paddingOuter">The outer padding.</param>
    /// <param name="align">The alignment from 0 to 1 (limited to that range).</param>
    /// <param name="round">Whether the step, the start and the bandwidth are rounded to whole numbers.</param>
    /// <returns>The scale.</returns>
    public static ScaleModel CreateBand(
        IEnumerable<DataValue> domain,
        double rangeStart,
        double rangeEnd,
        double paddingInner = 0,
        double paddingOuter = 0,
        double align = 0.5,
        bool round = false)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return CreateBands(ScaleKind.Band, domain, rangeStart, rangeEnd, Math.Min(1, paddingInner), paddingOuter, align, round);
    }

    /// <summary>
    ///     Creates a point scale: a band scale with an inner padding of 1, so that every category is one position.
    /// </summary>
    /// <param name="domain">The categories (duplicates are ignored).</param>
    /// <param name="rangeStart">The first end of the range.</param>
    /// <param name="rangeEnd">The second end of the range.</param>
    /// <param name="padding">The outer padding in steps; 0.5 puts a half step before the first and after the last point.</param>
    /// <param name="align">The alignment from 0 to 1 (limited to that range).</param>
    /// <param name="round">Whether the step and the positions are rounded to whole numbers.</param>
    /// <returns>The scale.</returns>
    public static ScaleModel CreatePoint(
        IEnumerable<DataValue> domain,
        double rangeStart,
        double rangeEnd,
        double padding = 0,
        double align = 0.5,
        bool round = false)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return CreateBands(ScaleKind.Point, domain, rangeStart, rangeEnd, 1, padding, align, round);
    }

    /// <summary>
    ///     Creates an ordinal scale: the n-th category gets the n-th range value, and the range repeats when it is shorter
    ///     than the domain.
    /// </summary>
    /// <param name="domain">The categories (duplicates are ignored).</param>
    /// <param name="range">The range values, for example colors.</param>
    /// <returns>The scale.</returns>
    public static ScaleModel CreateOrdinal(IEnumerable<DataValue> domain, IEnumerable<string> range)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(range);
        return new ScaleModel(ScaleKind.Ordinal, domain, [.. range]);
    }

    /// <summary>
    ///     Maps a number through a linear scale.
    /// </summary>
    /// <remarks>
    ///     The value is mapped linearly from the domain to the range. A domain of zero width maps every value to the
    ///     middle of the range (as d3 does), NaN maps to NaN.
    /// </remarks>
    /// <param name="value">The number.</param>
    /// <returns>The mapped number.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the scale is not linear.</exception>
    public double Map(double value)
    {
        RequireKind(ScaleKind.Linear);
        if (double.IsNaN(value))
        {
            return double.NaN;
        }

        if (Clamp)
        {
            value = Math.Max(Math.Min(DomainStart, DomainEnd), Math.Min(Math.Max(DomainStart, DomainEnd), value));
        }

        // d3 swaps both pairs when the domain runs backwards, and so does this
        double t;
        double from;
        double to;
        if (DomainEnd < DomainStart)
        {
            t = Normalize(DomainEnd, DomainStart, value);
            from = RangeEnd;
            to = RangeStart;
        }
        else
        {
            t = Normalize(DomainStart, DomainEnd, value);
            from = RangeStart;
            to = RangeEnd;
        }

        var result = (from * (1 - t)) + (to * t);
        return Round ? JsNumber.Round(result) : result;
    }

    /// <summary>
    ///     Maps a data value through the scale to a position or size.
    /// </summary>
    /// <remarks>
    ///     For a linear scale the value must read as a number (see <see cref="DataValue.TryGetQuantitative"/>). For a band
    ///     scale the result is the start of the value's band, for a point scale its position. An ordinal scale maps to
    ///     a range value, not a number: use <see cref="MapOrdinal"/>.
    /// </remarks>
    /// <param name="value">The data value.</param>
    /// <returns>The mapped number, or <see langword="null"/> when the value is invalid or outside the domain.</returns>
    public double? Map(DataValue value)
    {
        switch (Kind)
        {
            case ScaleKind.Linear:
                return value.TryGetQuantitative(out var number) ? Map(number) : null;
            case ScaleKind.Band or ScaleKind.Point:
                return _index.TryGetValue(value, out var index) ? _positions[index] : null;
            default:
                return null;
        }
    }

    /// <summary>
    ///     Maps a data value to the center of its band (the position itself for a linear or point scale).
    /// </summary>
    /// <param name="value">The data value.</param>
    /// <returns>The center position, or <see langword="null"/> when the value is invalid or outside the domain.</returns>
    public double? MapCenter(DataValue value)
    {
        var start = Map(value);
        return start is { } position && Kind == ScaleKind.Band ? position + (Bandwidth / 2) : start;
    }

    /// <summary>
    ///     Maps a data value through an ordinal scale to its range value.
    /// </summary>
    /// <param name="value">The data value.</param>
    /// <returns>The range value (cycling through the range), or <see langword="null"/> when the value is outside the domain or the range is empty.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the scale is not ordinal.</exception>
    public string? MapOrdinal(DataValue value)
    {
        RequireKind(ScaleKind.Ordinal);
        return _ordinalRange.Length > 0 && _index.TryGetValue(value, out var index) ? _ordinalRange[index % _ordinalRange.Length] : null;
    }

    /// <summary>
    ///     Maps a position back through a linear scale to a domain value.
    /// </summary>
    /// <remarks>
    ///     The inverse of <see cref="Map(double)"/>; with <see cref="Clamp"/> the result is clamped to the domain. A
    ///     range of zero width gives the middle of the domain.
    /// </remarks>
    /// <param name="position">The number in the range.</param>
    /// <returns>The domain value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the scale is not linear.</exception>
    public double Invert(double position)
    {
        RequireKind(ScaleKind.Linear);
        if (double.IsNaN(position))
        {
            return double.NaN;
        }

        double t;
        double from;
        double to;
        if (RangeEnd < RangeStart)
        {
            t = Normalize(RangeEnd, RangeStart, position);
            from = DomainEnd;
            to = DomainStart;
        }
        else
        {
            t = Normalize(RangeStart, RangeEnd, position);
            from = DomainStart;
            to = DomainEnd;
        }

        var result = (from * (1 - t)) + (to * t);
        return Clamp
            ? Math.Max(Math.Min(DomainStart, DomainEnd), Math.Min(Math.Max(DomainStart, DomainEnd), result))
            : result;
    }

    /// <summary>
    ///     Finds the position of a category in the discrete domain.
    /// </summary>
    /// <param name="value">The data value.</param>
    /// <returns>The zero-based position, or -1 when the value is not in the domain.</returns>
    public int IndexOf(DataValue value) => _index.TryGetValue(value, out var index) ? index : -1;

    /// <summary>
    ///     Builds a band or point scale and computes the positions of its bands (d3's <c>rescale</c>).
    /// </summary>
    /// <param name="kind">Band or point.</param>
    /// <param name="domain">The categories.</param>
    /// <param name="rangeStart">The first end of the range.</param>
    /// <param name="rangeEnd">The second end of the range.</param>
    /// <param name="paddingInner">The inner padding (already limited to 1).</param>
    /// <param name="paddingOuter">The outer padding.</param>
    /// <param name="align">The alignment.</param>
    /// <param name="round">Whether to round.</param>
    /// <returns>The scale.</returns>
    private static ScaleModel CreateBands(
        ScaleKind kind,
        IEnumerable<DataValue> domain,
        double rangeStart,
        double rangeEnd,
        double paddingInner,
        double paddingOuter,
        double align,
        bool round)
    {
        // The categories (deduplicated) and the geometry: the range may run backwards
        var categories = domain.Distinct().ToArray();
        var n = categories.Length;
        var reverse = rangeEnd < rangeStart;
        var start = reverse ? rangeEnd : rangeStart;
        var stop = reverse ? rangeStart : rangeEnd;
        align = Math.Max(0, Math.Min(1, align));
        var step = (stop - start) / Math.Max(1, n - paddingInner + (paddingOuter * 2));
        if (round)
        {
            step = Math.Floor(step);
        }

        start += (stop - start - (step * (n - paddingInner))) * align;
        var bandwidth = step * (1 - paddingInner);
        if (round)
        {
            start = JsNumber.Round(start);
            bandwidth = JsNumber.Round(bandwidth);
        }

        // The band starts, first to last (or last to first when the range runs backwards)
        var positions = new double[n];
        for (var i = 0; i < n; i++)
        {
            positions[reverse ? n - 1 - i : i] = start + (step * i);
        }

        return new ScaleModel(kind, categories, [], positions)
        {
            RangeStart = rangeStart,
            RangeEnd = rangeEnd,
            PaddingInner = paddingInner,
            PaddingOuter = paddingOuter,
            Align = align,
            Round = round,
            Step = step,
            Bandwidth = bandwidth,
        };
    }

    /// <summary>
    ///     Normalizes a value between two ends of a domain to the unit interval.
    /// </summary>
    /// <param name="a">The first end.</param>
    /// <param name="b">The second end (not below the first).</param>
    /// <param name="x">The value.</param>
    /// <returns>The position of <paramref name="x"/> between the ends; 0.5 when the ends are equal, NaN when they are not numbers.</returns>
    private static double Normalize(double a, double b, double x)
    {
        var width = b - a;
        if (width != 0)
        {
            return (x - a) / width;
        }

        return double.IsNaN(width) ? double.NaN : 0.5;
    }

    /// <summary>
    ///     Checks that the scale is of the given kind.
    /// </summary>
    /// <param name="expected">The kind the caller needs.</param>
    /// <exception cref="InvalidOperationException">Thrown when the scale is of another kind.</exception>
    private void RequireKind(ScaleKind expected)
    {
        if (Kind != expected)
        {
            throw new InvalidOperationException($"The operation needs a {expected} scale, but this is a {Kind} scale.");
        }
    }
}
