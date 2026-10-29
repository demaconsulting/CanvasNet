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
///         (<see cref="ScaleKind.Linear"/>, with <c>clamp</c> and <c>round</c>, a reversed domain or range and d3's piecewise
///         (polylinear) mapping for a domain and a range of more than two entries, see
///         <see cref="CreateLinear(double[], double[], bool, bool)"/>), <c>scaleBand</c>
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

    /// <summary>Gets the start of the numeric domain of a linear scale (the first entry of the complete domain).</summary>
    public double DomainStart { get; private init; }

    /// <summary>Gets the end of the numeric domain of a linear scale (the last entry of the complete domain).</summary>
    public double DomainEnd { get; private init; }

    /// <summary>Gets the start of the numeric range of a linear, band or point scale (the first entry of the complete range).</summary>
    public double RangeStart { get; private init; }

    /// <summary>Gets the end of the numeric range of a linear, band or point scale (the last entry of the complete range for a linear scale).</summary>
    public double RangeEnd { get; private init; }

    /// <summary>Gets the lower clamp limit of a linear scale.</summary>
    private double ClampLow { get; init; }

    /// <summary>Gets the upper clamp limit of a linear scale.</summary>
    private double ClampHigh { get; init; }

    /// <summary>Gets the piecewise map from domain to range of a linear scale.</summary>
    private Piecewise? Forward { get; init; }

    /// <summary>Gets the piecewise map from range to domain of a linear scale.</summary>
    private Piecewise? Inverse { get; init; }

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
        CreateLinear([domainStart, domainEnd], [rangeStart, rangeEnd], clamp, round);

    /// <summary>
    ///     Creates a linear scale from a domain and a range of any length, with d3's piecewise (polylinear) semantics.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         As in d3-scale's <c>continuous.js</c>, <c>min(domain.length, range.length)</c> entries are used: with two the
    ///         scale is a single segment from the first two domain entries to the first two range entries (so an
    ///         entry beyond them is ignored, whichever array is longer); with more, one segment lies between each pair of
    ///         neighbors, and a value is mapped by the segment that <c>bisectRight(domain, x, 1, n - 1)</c> picks, so a value
    ///         beyond the last entry continues the last segment and one before the first continues the first.
    ///     </para>
    ///     <para>
    ///         A descending domain (the last used entry below the first) reverses both complete arrays before they are
    ///         cut to that length, exactly as d3 does. <paramref name="clamp"/> limits a value to the first and the last
    ///         used domain entry. <see cref="DomainStart"/> and <see cref="DomainEnd"/> are the first and the last entry of
    ///         the complete domain and <see cref="RangeStart"/> and <see cref="RangeEnd"/> those of the complete range,
    ///         which is what d3's <c>nice</c>, <c>ticks</c> and an axis use.
    ///     </para>
    /// </remarks>
    /// <param name="domain">The domain: at least two numbers.</param>
    /// <param name="range">The range: at least two numbers.</param>
    /// <param name="clamp">Whether values outside the domain are clamped to its ends.</param>
    /// <param name="round">Whether results are rounded to whole numbers.</param>
    /// <returns>The scale.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="domain"/> or <paramref name="range"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the domain or the range has fewer than two entries.</exception>
    public static ScaleModel CreateLinear(double[] domain, double[] range, bool clamp = false, bool round = false)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(range);
        if (domain.Length < 2)
        {
            throw new ArgumentException("A linear scale needs at least two domain entries.", nameof(domain));
        }

        if (range.Length < 2)
        {
            throw new ArgumentException("A linear scale needs at least two range entries.", nameof(range));
        }

        var used = Math.Min(domain.Length, range.Length);
        return new ScaleModel(ScaleKind.Linear, [], [])
        {
            DomainStart = domain[0],
            DomainEnd = domain[^1],
            RangeStart = range[0],
            RangeEnd = range[^1],
            Clamp = clamp,
            Round = round,
            Forward = Piecewise.Create(domain, range),
            Inverse = Piecewise.Create(range, domain),
            ClampLow = Math.Min(domain[0], domain[used - 1]),
            ClampHigh = Math.Max(domain[0], domain[used - 1]),
        };
    }

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
            value = Math.Max(ClampLow, Math.Min(ClampHigh, value));
        }

        var result = Forward!.Evaluate(value);
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

        var result = Inverse!.Evaluate(position);
        return Clamp ? Math.Max(ClampLow, Math.Min(ClampHigh, result)) : result;
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
    ///     One piecewise-linear map between two arrays of numbers: d3-scale's <c>bimap</c> for two entries and
    ///     <c>polymap</c> for more (with <c>interpolateNumber</c> and <c>normalize</c>).
    /// </summary>
    private sealed class Piecewise
    {
        /// <summary>The used entries of the input side, ascending (after the reversal of a descending domain).</summary>
        private readonly double[] _input;

        /// <summary>The width of every segment of the input side.</summary>
        private readonly double[] _width;

        /// <summary>The output value at the start of every segment.</summary>
        private readonly double[] _from;

        /// <summary>The output value at the end of every segment.</summary>
        private readonly double[] _to;

        /// <summary>
        ///     Initializes a map.
        /// </summary>
        /// <param name="input">The used input entries.</param>
        /// <param name="output">The used output entries, as many as the input entries.</param>
        private Piecewise(double[] input, double[] output)
        {
            _input = input;
            var segments = input.Length - 1;
            _width = new double[segments];
            _from = new double[segments];
            _to = new double[segments];
            for (var i = 0; i < segments; i++)
            {
                _width[i] = input[i + 1] - input[i];
                _from[i] = output[i];
                _to[i] = output[i + 1];
            }
        }

        /// <summary>
        ///     Builds the map the way d3's <c>piecewise</c> does.
        /// </summary>
        /// <param name="input">The complete input array (the domain for the forward map, the range for the inverse).</param>
        /// <param name="output">The complete output array.</param>
        /// <returns>The map over the first <c>min(input.Length, output.Length)</c> entries.</returns>
        public static Piecewise Create(double[] input, double[] output)
        {
            var used = Math.Min(input.Length, output.Length);
            if (used == 2)
            {
                // bimap: a descending pair is swapped, and nothing beyond the first two entries counts
                return input[1] < input[0]
                    ? new Piecewise([input[1], input[0]], [output[1], output[0]])
                    : new Piecewise([input[0], input[1]], [output[0], output[1]]);
            }

            // polymap: a descending input reverses both complete arrays before the first entries are used
            if (input[used - 1] < input[0])
            {
                var reversedInput = new double[input.Length];
                var reversedOutput = new double[output.Length];
                Array.Copy(input, reversedInput, input.Length);
                Array.Copy(output, reversedOutput, output.Length);
                Array.Reverse(reversedInput);
                Array.Reverse(reversedOutput);
                input = reversedInput;
                output = reversedOutput;
            }

            return new Piecewise(input[..used], output[..used]);
        }

        /// <summary>
        ///     Maps a number.
        /// </summary>
        /// <param name="x">The number, not NaN.</param>
        /// <returns>The mapped number; NaN when the segment has NaN ends.</returns>
        public double Evaluate(double x)
        {
            // bisectRight(input, x, 1, n - 1) - 1 picks the segment; the last or first segment extends beyond the ends
            var segment = 0;
            if (_input.Length > 2)
            {
                var low = 1;
                var high = _input.Length - 1;
                while (low < high)
                {
                    var middle = (low + high) >>> 1;
                    if (_input[middle] > x)
                    {
                        high = middle;
                    }
                    else
                    {
                        low = middle + 1;
                    }
                }

                segment = low - 1;
            }

            // normalize: a segment of zero width maps to the middle of the output segment
            var width = _width[segment];
            double t;
            if (width != 0)
            {
                t = (x - _input[segment]) / width;
            }
            else
            {
                t = double.IsNaN(width) ? double.NaN : 0.5;
            }

            return (_from[segment] * (1 - t)) + (_to[segment] * t);
        }
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
