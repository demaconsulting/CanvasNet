// Portions derived from d3-scale (ISC, Mike Bostock) and Vega (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

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

    /// <summary>A sequential scale: a number in a two-entry domain gives a color of an interpolator over 0 to 1 (d3's <c>scaleSequential</c>).</summary>
    Sequential,

    /// <summary>A diverging scale: a number in a three-entry domain (with a middle value) gives a color of an interpolator over 0 to 1 (d3's <c>scaleDiverging</c>).</summary>
    Diverging,

    /// <summary>A quantize scale: the domain is cut into equal-width intervals, each with one value of a discrete range (d3's <c>scaleQuantize</c>).</summary>
    Quantize,

    /// <summary>A quantile scale: the sorted domain values are cut at their quantiles, each interval has one value of a discrete range (d3's <c>scaleQuantile</c>).</summary>
    Quantile,

    /// <summary>A threshold scale: explicit thresholds cut the numbers into intervals, each with one value of a discrete range (d3's <c>scaleThreshold</c>).</summary>
    Threshold,

    /// <summary>A bin-ordinal scale: the number is placed in the bin between two edges and the bin has one value of a discrete range (Vega's <c>scaleBinOrdinal</c>).</summary>
    BinOrdinal,
}

/// <summary>
///     The output values of an ordinal or discretizing scale: texts (colors, shape names), numbers (sizes, opacities, angles) or
///     only a count (when the caller maps the index itself, as for dash patterns).
/// </summary>
/// <remarks>The values are copied on creation, so the range never changes after it is built.</remarks>
internal readonly struct ScaleRange
{
    /// <summary>The texts, or null.</summary>
    private readonly string[]? _texts;

    /// <summary>The numbers, or null.</summary>
    private readonly double[]? _numbers;

    /// <summary>
    ///     Initializes a range.
    /// </summary>
    /// <param name="texts">The texts, or null.</param>
    /// <param name="numbers">The numbers, or null.</param>
    /// <param name="count">The number of values.</param>
    private ScaleRange(string[]? texts, double[]? numbers, int count)
    {
        _texts = texts;
        _numbers = numbers;
        Count = count;
    }

    /// <summary>Gets the number of values of the range.</summary>
    public int Count { get; }

    /// <summary>Gets the texts; empty when the range holds numbers or only a count.</summary>
    public IReadOnlyList<string> Texts => _texts ?? [];

    /// <summary>Gets the numbers; empty when the range holds texts or only a count.</summary>
    public IReadOnlyList<double> Numbers => _numbers ?? [];

    /// <summary>
    ///     Creates a range of texts.
    /// </summary>
    /// <param name="texts">The values.</param>
    /// <returns>The range.</returns>
    public static ScaleRange OfTexts(IEnumerable<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var copy = texts.ToArray();
        return new ScaleRange(copy, null, copy.Length);
    }

    /// <summary>
    ///     Creates a range of numbers.
    /// </summary>
    /// <param name="numbers">The values.</param>
    /// <returns>The range.</returns>
    public static ScaleRange OfNumbers(IEnumerable<double> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        var copy = numbers.ToArray();
        return new ScaleRange(null, copy, copy.Length);
    }

    /// <summary>
    ///     Creates a range that has only a count: the scale gives the index of the value.
    /// </summary>
    /// <param name="count">The number of values; a negative count is treated as zero.</param>
    /// <returns>The range.</returns>
    public static ScaleRange OfCount(int count) => new(null, null, Math.Max(0, count));

    /// <summary>
    ///     Gets the text at an index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The text, or null when the range has no texts or the index is outside it.</returns>
    public string? TextAt(int index) => _texts is not null && index >= 0 && index < _texts.Length ? _texts[index] : null;

    /// <summary>
    ///     Gets the number at an index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The number, or null when the range has no numbers or the index is outside it.</returns>
    public double? NumberAt(int index) => _numbers is not null && index >= 0 && index < _numbers.Length ? _numbers[index] : null;
}

/// <summary>
///     The transform of a continuous scale: the function that is applied to the domain before the linear map, which makes the scale a
///     linear, a log, a pow (or sqrt) or a symlog scale.
/// </summary>
internal enum ScaleTransformKind
{
    /// <summary>No transform (a linear scale).</summary>
    Linear,

    /// <summary>The logarithm to a base (d3's <c>scaleLog</c>); a domain below zero is reflected.</summary>
    Log,

    /// <summary>The signed power to an exponent (d3's <c>scalePow</c>; the exponent 0.5 is d3's <c>scaleSqrt</c>).</summary>
    Pow,

    /// <summary>The symmetric log with a constant (d3's <c>scaleSymlog</c>).</summary>
    Symlog,
}

/// <summary>
///     An immutable transform of a continuous scale and its parameter (the base of a log scale, the exponent of a pow scale, the constant
///     of a symlog scale), with the functions d3-scale 4 applies.
/// </summary>
/// <remarks>
///     <para>
///         The transforms are those of d3's <c>log.js</c>, <c>pow.js</c> and <c>symlog.js</c>: <c>Math.log</c> (the natural logarithm: a
///         linear map does not depend on the base, which only matters for the ticks and <c>nice</c>), the sign-preserving power
///         <c>x &lt; 0 ? -(-x)^e : x^e</c> (with <c>Math.sqrt</c> and its square for the exponent 0.5), and
///         <c>sign(x) * log1p(|x| / c)</c> with the inverse <c>sign(y) * expm1(|y|) * c</c>. A log scale whose domain starts below zero is
///         reflected: <c>-log(-x)</c> and <c>-exp(-y)</c>. A transform never changes after it is created.
///     </para>
/// </remarks>
internal sealed class ScaleTransform
{
    /// <summary>The identity transform of a linear scale.</summary>
    public static readonly ScaleTransform Linear = new(ScaleTransformKind.Linear, 1);

    /// <summary>
    ///     Initializes a transform.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="parameter">The base, the exponent or the constant.</param>
    private ScaleTransform(ScaleTransformKind kind, double parameter)
    {
        Kind = kind;
        Parameter = parameter;
    }

    /// <summary>Gets the kind of transform.</summary>
    public ScaleTransformKind Kind { get; }

    /// <summary>Gets the base of a log transform, the exponent of a pow transform or the constant of a symlog transform (1 for a linear one).</summary>
    public double Parameter { get; }

    /// <summary>Gets a value indicating whether the transform changes nothing (a linear scale, or a pow scale with the exponent 1).</summary>
    public bool IsLinear => Kind == ScaleTransformKind.Linear || (Kind == ScaleTransformKind.Pow && Parameter.CompareTo(1) == 0);

    /// <summary>
    ///     Creates a log transform.
    /// </summary>
    /// <param name="logBase">The base (d3's default is 10).</param>
    /// <returns>The transform.</returns>
    public static ScaleTransform Log(double logBase) => new(ScaleTransformKind.Log, logBase);

    /// <summary>
    ///     Creates a pow transform.
    /// </summary>
    /// <param name="exponent">The exponent (1 gives a linear scale, 0.5 a sqrt scale).</param>
    /// <returns>The transform.</returns>
    public static ScaleTransform Pow(double exponent) => new(ScaleTransformKind.Pow, exponent);

    /// <summary>
    ///     Creates a symlog transform.
    /// </summary>
    /// <param name="constant">The constant (d3's default is 1): the size of the linear region around zero.</param>
    /// <returns>The transform.</returns>
    public static ScaleTransform Symlog(double constant) => new(ScaleTransformKind.Symlog, constant);

    /// <summary>
    ///     Applies the transform to a domain value.
    /// </summary>
    /// <param name="x">The value.</param>
    /// <param name="reflect">Whether a log transform is reflected (the domain starts below zero).</param>
    /// <returns>The transformed value; NaN or an infinity where the transform is not defined (the logarithm of a number that is not above zero).</returns>
    public double Forward(double x, bool reflect = false)
    {
        switch (Kind)
        {
            case ScaleTransformKind.Log:
                return reflect ? -Math.Log(-x) : Math.Log(x);
            case ScaleTransformKind.Pow:
                if (Parameter.CompareTo(1) == 0)
                {
                    return x;
                }

                if (Parameter.CompareTo(0.5) == 0)
                {
                    return x < 0 ? -Math.Sqrt(-x) : Math.Sqrt(x);
                }

                return x < 0 ? -Math.Pow(-x, Parameter) : Math.Pow(x, Parameter);
            case ScaleTransformKind.Symlog:
                return double.IsNaN(x) ? double.NaN : Math.Sign(x) * Log1P(Math.Abs(x / Parameter));
            default:
                return x;
        }
    }

    /// <summary>
    ///     Undoes the transform.
    /// </summary>
    /// <param name="y">The transformed value.</param>
    /// <param name="reflect">Whether a log transform is reflected.</param>
    /// <returns>The domain value.</returns>
    public double Inverse(double y, bool reflect = false)
    {
        switch (Kind)
        {
            case ScaleTransformKind.Log:
                return reflect ? -Math.Exp(-y) : Math.Exp(y);
            case ScaleTransformKind.Pow:
                if (Parameter.CompareTo(1) == 0)
                {
                    return y;
                }

                if (Parameter.CompareTo(0.5) == 0)
                {
                    return y < 0 ? -(y * y) : y * y;
                }

                return y < 0 ? -Math.Pow(-y, 1 / Parameter) : Math.Pow(y, 1 / Parameter);
            case ScaleTransformKind.Symlog:
                return double.IsNaN(y) ? double.NaN : Math.Sign(y) * ExpM1(Math.Abs(y)) * Parameter;
            default:
                return y;
        }
    }

    /// <summary>
    ///     Computes <c>ln(1 + x)</c> without losing the small digits of <paramref name="x"/> (JavaScript's <c>Math.log1p</c>).
    /// </summary>
    /// <param name="x">The number, above -1.</param>
    /// <returns>The natural logarithm of one plus the number.</returns>
    private static double Log1P(double x)
    {
        var u = 1 + x;
        if (double.IsInfinity(u) || double.IsNaN(u))
        {
            return Math.Log(u);
        }

        // Kahan's correction: the rounding error of 1 + x is divided out
        return u.CompareTo(1) == 0 ? x : Math.Log(u) * x / (u - 1);
    }

    /// <summary>
    ///     Computes <c>e^x - 1</c> without losing the small digits of <paramref name="x"/> (JavaScript's <c>Math.expm1</c>).
    /// </summary>
    /// <param name="x">The number.</param>
    /// <returns>The exponential of the number minus one.</returns>
    private static double ExpM1(double x)
    {
        var u = Math.Exp(x);
        if (u.CompareTo(1) == 0)
        {
            return x;
        }

        var um1 = u - 1;
        if (um1.CompareTo(-1) == 0)
        {
            return -1;
        }

        return double.IsInfinity(u) ? u : um1 * x / Math.Log(u);
    }
}

/// <summary>
///     An immutable scale: the mapping from data values to positions, sizes, opacities or colors.
/// </summary>
/// <remarks>
///     <para>
///         The model is a C# version of the d3-scale 4 scales Vega-Lite uses for its first charts: <c>scaleLinear</c>
///         (<see cref="ScaleKind.Linear"/>, with <c>clamp</c> and <c>round</c>, a reversed domain or range and d3's piecewise
///         (polylinear) mapping for a domain and a range of more than two entries, see
///         <see cref="CreateLinear(double[], double[], bool, bool, ScaleTransform)"/>), <c>scaleBand</c>
///         (<see cref="ScaleKind.Band"/>, with inner and outer padding, alignment, rounding and a reversed range),
///         <c>scalePoint</c> (<see cref="ScaleKind.Point"/>) and <c>scaleOrdinal</c> (<see cref="ScaleKind.Ordinal"/>). The arithmetic of
///         d3 is kept, so for example the band step is
///         <c>(stop - start) / max(1, n - paddingInner + 2 * paddingOuter)</c> and the first band starts at
///         <c>start + (stop - start - step * (n - paddingInner)) * align</c>. The color and discretizing scales of Vega-Lite's
///         legends and color encodings are <c>scaleSequential</c> and <c>scaleDiverging</c> (<see cref="ScaleKind.Sequential"/>,
///         <see cref="ScaleKind.Diverging"/>: a number gives a color of an interpolator), <c>scaleQuantize</c>,
///         <c>scaleQuantile</c> and <c>scaleThreshold</c> (<see cref="ScaleKind.Quantize"/>, <see cref="ScaleKind.Quantile"/>,
///         <see cref="ScaleKind.Threshold"/>: a number gives the value of its interval, whose boundaries are
///         <see cref="Thresholds"/>) and Vega's <c>scaleBinOrdinal</c> (<see cref="ScaleKind.BinOrdinal"/>: a number gives the value of its
///         bin). Their outputs are a <see cref="ScaleRange"/> of texts (colors, shape names) or numbers (sizes, opacities), or the
///         index of the value (<see cref="MapIndex"/>) for outputs that are neither, such as dash patterns.
///     </para>
///     <para>
///         A linear, sequential or diverging scale can carry a <see cref="ScaleTransform"/>: with the logarithm (<c>scaleLog</c>,
///         <c>scaleSequentialLog</c>), the signed power (<c>scalePow</c>, <c>scaleSqrt</c>) or the symmetric log (<c>scaleSymlog</c>) the map
///         from the domain to the range is linear over the transformed domain, as in d3's <c>transformer</c>; <see cref="Fraction"/> gives
///         the position of a value in that transformed domain (what a gradient legend and its labels need).
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

    /// <summary>The output values of an ordinal or discretizing scale.</summary>
    private readonly ScaleRange _range;

    /// <summary>The thresholds of a quantize, quantile or threshold scale, or the bin edges of a bin-ordinal scale.</summary>
    private double[] _thresholds = [];

    /// <summary>The slope of a sequential scale from the domain to 0..1, or the slope of the lower half of a diverging scale.</summary>
    private double _k10;

    /// <summary>The slope of the upper half of a diverging scale.</summary>
    private double _k21;

    /// <summary>The direction of a diverging scale: 1, or -1 when the domain runs downward.</summary>
    private double _sign = 1;

    /// <summary>The transformed first domain entry of a sequential scale, or the transformed middle entry of a diverging scale.</summary>
    private double _x0;

    /// <summary>
    ///     Initializes a model.
    /// </summary>
    /// <param name="kind">The kind of scale.</param>
    /// <param name="domain">The categories of a discrete domain (deduplicated here).</param>
    /// <param name="range">The output values of an ordinal or discretizing scale.</param>
    /// <param name="positions">The band starts of a band or point scale.</param>
    private ScaleModel(ScaleKind kind, IEnumerable<DataValue> domain, ScaleRange range, double[]? positions = null)
    {
        Kind = kind;
        _range = range;
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

    /// <summary>Gets the middle value of a diverging scale (the second domain entry).</summary>
    public double DomainMid { get; private init; }

    /// <summary>Gets the interpolator of a sequential or diverging scale (a color for a position from 0 to 1), or null.</summary>
    public Func<double, RgbColor>? Interpolator { get; private init; }

    /// <summary>
    ///     Gets the thresholds of a quantize, quantile or threshold scale (the values that separate the intervals, in ascending
    ///     order), or the bin edges of a bin-ordinal scale; empty for the other kinds.
    /// </summary>
    public IReadOnlyList<double> Thresholds => _thresholds;

    /// <summary>Gets the output values of an ordinal or discretizing scale.</summary>
    public ScaleRange Range => _range;

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

    /// <summary>
    ///     Gets the transform of a linear, sequential or diverging scale: the identity for a linear scale, else the log, pow (or sqrt) or
    ///     symlog transform the domain goes through before the linear map.
    /// </summary>
    public ScaleTransform Transform { get; private init; } = ScaleTransform.Linear;

    /// <summary>Gets a value indicating whether a log scale is reflected (its domain starts below zero, so <c>-log(-x)</c> is used).</summary>
    private bool Reflect { get; init; }

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

    /// <summary>Gets the range of an ordinal scale as texts (empty when the range holds numbers).</summary>
    public IReadOnlyList<string> OrdinalRange => _range.Texts;

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
    /// <param name="transform">The transform of a log, pow, sqrt or symlog scale; null for a linear scale.</param>
    /// <returns>The scale.</returns>
    public static ScaleModel CreateLinear(double domainStart, double domainEnd, double rangeStart, double rangeEnd, bool clamp = false, bool round = false, ScaleTransform? transform = null) =>
        CreateLinear([domainStart, domainEnd], [rangeStart, rangeEnd], clamp, round, transform);

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
    ///     <para>
    ///         A <paramref name="transform"/> makes the scale a log, pow, sqrt or symlog scale exactly as in d3's <c>transformer</c>: the
    ///         value is clamped (in the domain), then transformed, and the piecewise map runs over the transformed domain; the inverse
    ///         undoes the transform of the result. A value that the transform cannot take (the logarithm of a number that is not above
    ///         zero) has no position: <see cref="Map(DataValue)"/> gives <see langword="null"/> for it.
    ///     </para>
    /// </remarks>
    /// <param name="domain">The domain: at least two numbers.</param>
    /// <param name="range">The range: at least two numbers.</param>
    /// <param name="clamp">Whether values outside the domain are clamped to its ends.</param>
    /// <param name="round">Whether results are rounded to whole numbers.</param>
    /// <param name="transform">The transform of a log, pow, sqrt or symlog scale; null for a linear scale.</param>
    /// <returns>The scale.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="domain"/> or <paramref name="range"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the domain or the range has fewer than two entries.</exception>
    public static ScaleModel CreateLinear(double[] domain, double[] range, bool clamp = false, bool round = false, ScaleTransform? transform = null)
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
        transform ??= ScaleTransform.Linear;

        // The piecewise maps work on the transformed domain; a log scale that starts below zero is reflected (d3's rescale)
        var reflect = transform.Kind == ScaleTransformKind.Log && domain[0] < 0;
        var transformed = transform.IsLinear ? domain : [.. domain.Select(d => transform.Forward(d, reflect))];
        return new ScaleModel(ScaleKind.Linear, [], default)
        {
            DomainStart = domain[0],
            DomainEnd = domain[^1],
            RangeStart = range[0],
            RangeEnd = range[^1],
            Clamp = clamp,
            Round = round,
            Transform = transform,
            Reflect = reflect,
            Forward = Piecewise.Create(transformed, range),
            Inverse = Piecewise.Create(range, transformed),
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
        return new ScaleModel(ScaleKind.Ordinal, domain, ScaleRange.OfTexts(range));
    }

    /// <summary>
    ///     Creates an ordinal scale with any kind of range: texts, numbers or only a count (the caller maps the index).
    /// </summary>
    /// <param name="domain">The categories (duplicates are ignored).</param>
    /// <param name="range">The range; the n-th category gets the n-th value and the range repeats when it is shorter than the domain.</param>
    /// <returns>The scale.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="domain"/> is null.</exception>
    public static ScaleModel CreateOrdinal(IEnumerable<DataValue> domain, ScaleRange range)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return new ScaleModel(ScaleKind.Ordinal, domain, range);
    }

    /// <summary>
    ///     Creates a sequential scale (two domain entries) or a diverging scale (three), whose output is a color of an interpolator.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The sequential map is d3's: <c>t = (x - x0) / (x1 - x0)</c>, clamped to 0 to 1 when <paramref name="clamp"/> is set, and a
    ///         domain of zero width gives the middle of the interpolator (0.5). The diverging map is d3's
    ///         <c>t = 0.5 + (x - x1) * k</c> with <c>k = 0.5 / (x1 - x0)</c> on the side of <c>x0</c> and <c>0.5 / (x2 - x1)</c> on the
    ///         side of <c>x2</c> (zero when the two entries are equal), the sides told apart by the direction of the domain.
    ///     </para>
    /// </remarks>
    /// <param name="domain">The domain: two entries (sequential) or three, the middle one being the value the middle color stands for (diverging).</param>
    /// <param name="interpolator">The interpolator over 0 to 1.</param>
    /// <param name="clamp">Whether the position is limited to 0 to 1.</param>
    /// <param name="transform">The transform of a log, pow, sqrt or symlog color scale (d3's <c>scaleSequentialLog</c> and friends: the position is computed from the transformed numbers); null for none.</param>
    /// <returns>The scale.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="domain"/> or <paramref name="interpolator"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the domain does not have two or three entries.</exception>
    public static ScaleModel CreateSequential(double[] domain, Func<double, RgbColor> interpolator, bool clamp = false, ScaleTransform? transform = null)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(interpolator);
        if (domain.Length is not (2 or 3))
        {
            throw new ArgumentException("A sequential scale needs two domain entries and a diverging scale three.", nameof(domain));
        }

        // The position is computed from the transformed domain (a log scale that starts below zero is reflected)
        transform ??= ScaleTransform.Linear;
        var reflect = transform.Kind == ScaleTransformKind.Log && domain[0] < 0;
        var t = transform.IsLinear ? domain : [.. domain.Select(d => transform.Forward(d, reflect))];
        if (domain.Length == 2)
        {
            return new ScaleModel(ScaleKind.Sequential, [], default)
            {
                DomainStart = domain[0],
                DomainEnd = domain[1],
                Interpolator = interpolator,
                Clamp = clamp,
                Transform = transform,
                Reflect = reflect,
                _x0 = t[0],
                _k10 = t[0].CompareTo(t[1]) == 0 ? 0 : 1 / (t[1] - t[0]),
            };
        }

        return new ScaleModel(ScaleKind.Diverging, [], default)
        {
            DomainStart = domain[0],
            DomainMid = domain[1],
            DomainEnd = domain[2],
            Interpolator = interpolator,
            Clamp = clamp,
            Transform = transform,
            Reflect = reflect,
            _x0 = t[1],
            _k10 = t[0].CompareTo(t[1]) == 0 ? 0 : 0.5 / (t[1] - t[0]),
            _k21 = t[1].CompareTo(t[2]) == 0 ? 0 : 0.5 / (t[2] - t[1]),
            _sign = t[1] < t[0] ? -1 : 1,
        };
    }

    /// <summary>
    ///     Creates a quantize scale: the domain is divided into as many equal-width intervals as the range has values.
    /// </summary>
    /// <remarks>
    ///     The thresholds are d3's <c>((i + 1) * x1 - (i - n) * x0) / (n + 1)</c> for <c>n = range.Count - 1</c>, and a number is
    ///     mapped to the value of the interval it falls into (<c>bisectRight</c>), so a number outside the domain gets the first
    ///     or the last value and a number on a threshold the value above it.
    /// </remarks>
    /// <param name="domainStart">The start of the domain.</param>
    /// <param name="domainEnd">The end of the domain.</param>
    /// <param name="range">The output values.</param>
    /// <returns>The scale.</returns>
    public static ScaleModel CreateQuantize(double domainStart, double domainEnd, ScaleRange range)
    {
        var n = range.Count - 1;
        var thresholds = new double[Math.Max(0, n)];
        for (var i = 0; i < thresholds.Length; i++)
        {
            thresholds[i] = (((i + 1) * domainEnd) - ((i - n) * domainStart)) / (n + 1);
        }

        return new ScaleModel(ScaleKind.Quantize, [], range)
        {
            DomainStart = domainStart,
            DomainEnd = domainEnd,
            _thresholds = thresholds,
        };
    }

    /// <summary>
    ///     Creates a quantile scale: the domain values are divided into as many groups of equal size as the range has values.
    /// </summary>
    /// <remarks>
    ///     The thresholds are the quantiles <c>i / n</c> of the sorted values by linear interpolation between the closest ranks (the R-7
    ///     method of d3's <c>quantileSorted</c>), for <c>n = max(1, range.Count)</c> and <c>i = 1 .. n - 1</c>.
    /// </remarks>
    /// <param name="sortedValues">The domain values in ascending order; none is NaN.</param>
    /// <param name="range">The output values.</param>
    /// <returns>The scale.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sortedValues"/> is null.</exception>
    public static ScaleModel CreateQuantile(double[] sortedValues, ScaleRange range)
    {
        ArgumentNullException.ThrowIfNull(sortedValues);
        var n = Math.Max(1, range.Count);
        var thresholds = new double[n - 1];
        for (var i = 1; i < n; i++)
        {
            thresholds[i - 1] = AggregateMeasures.Quantile(sortedValues, (double)i / n);
        }

        return new ScaleModel(ScaleKind.Quantile, [], range)
        {
            DomainStart = sortedValues.Length > 0 ? sortedValues[0] : double.NaN,
            DomainEnd = sortedValues.Length > 0 ? sortedValues[^1] : double.NaN,
            _thresholds = thresholds,
        };
    }

    /// <summary>
    ///     Creates a threshold scale: the thresholds divide the numbers into intervals, the interval a number falls into gives the value.
    /// </summary>
    /// <remarks>
    ///     As in d3, the thresholds are expected in ascending order and only the first <c>min(thresholds, range.Count - 1)</c> of them are used.
    /// </remarks>
    /// <param name="thresholds">The thresholds in ascending order.</param>
    /// <param name="range">The output values.</param>
    /// <returns>The scale.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="thresholds"/> is null.</exception>
    public static ScaleModel CreateThreshold(double[] thresholds, ScaleRange range)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        var used = Math.Max(0, Math.Min(thresholds.Length, range.Count - 1));
        return new ScaleModel(ScaleKind.Threshold, [], range)
        {
            DomainStart = thresholds.Length > 0 ? thresholds[0] : double.NaN,
            DomainEnd = thresholds.Length > 0 ? thresholds[^1] : double.NaN,
            _thresholds = [.. thresholds.Take(used)],
        };
    }

    /// <summary>
    ///     Creates a bin-ordinal scale: a number is placed in the bin between two neighboring edges and the bin gives the value.
    /// </summary>
    /// <remarks>
    ///     As in Vega's <c>scaleBinOrdinal</c> the value is <c>range[(bisectRight(edges, x) - 1) % range.Count]</c>: the bin index
    ///     cycles through the range, a number below the first edge has no value, and a number at or above the last edge gets the value
    ///     that follows the last bin.
    /// </remarks>
    /// <param name="edges">The bin edges in ascending order (one more than there are bins).</param>
    /// <param name="range">The output values.</param>
    /// <returns>The scale.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="edges"/> is null.</exception>
    public static ScaleModel CreateBinOrdinal(double[] edges, ScaleRange range)
    {
        ArgumentNullException.ThrowIfNull(edges);
        return new ScaleModel(ScaleKind.BinOrdinal, [], range)
        {
            DomainStart = edges.Length > 0 ? edges[0] : double.NaN,
            DomainEnd = edges.Length > 0 ? edges[^1] : double.NaN,
            _thresholds = [.. edges],
        };
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

        var result = Forward!.Evaluate(Transform.IsLinear ? value : Transform.Forward(value, Reflect));
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
                if (!value.TryGetQuantitative(out var number))
                {
                    return null;
                }

                // A value the transform cannot take (the logarithm of a number that is not above zero) has no position
                var mapped = Map(number);
                return Transform.IsLinear || double.IsFinite(mapped) ? mapped : null;
            case ScaleKind.Band or ScaleKind.Point:
                return _index.TryGetValue(value, out var index) ? _positions[index] : null;
            case ScaleKind.Ordinal or ScaleKind.Quantize or ScaleKind.Quantile or ScaleKind.Threshold or ScaleKind.BinOrdinal:
                return _range.NumberAt(MapIndex(value));
            default:
                return null;
        }
    }

    /// <summary>
    ///     Maps a data value through any scale whose output is a text (a color or a shape name) to that text.
    /// </summary>
    /// <remarks>
    ///     An ordinal scale maps a category, a quantize, quantile, threshold or bin-ordinal scale the number to the value of its
    ///     interval, and a sequential or diverging scale the number to a color of the interpolator as <c>#rrggbb</c> (or
    ///     <c>#rrggbbaa</c>). A value that is not a number (or not a category) has no text, as d3's <c>unknown</c>.
    /// </remarks>
    /// <param name="value">The data value.</param>
    /// <returns>The text, or <see langword="null"/> when the value has none or the scale does not output texts.</returns>
    public string? MapText(DataValue value)
    {
        switch (Kind)
        {
            case ScaleKind.Sequential or ScaleKind.Diverging:
                return value.TryGetQuantitative(out var number) ? MapColor(number)?.ToCss() : null;
            case ScaleKind.Ordinal or ScaleKind.Quantize or ScaleKind.Quantile or ScaleKind.Threshold or ScaleKind.BinOrdinal:
                return _range.TextAt(MapIndex(value));
            default:
                return null;
        }
    }

    /// <summary>
    ///     Maps a number through a sequential or diverging scale to a color.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The color of the interpolator; <see langword="null"/> for NaN.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the scale is not sequential or diverging.</exception>
    public RgbColor? MapColor(double value)
    {
        if (Kind is not (ScaleKind.Sequential or ScaleKind.Diverging))
        {
            throw new InvalidOperationException($"The operation needs a {ScaleKind.Sequential} or {ScaleKind.Diverging} scale, but this is a {Kind} scale.");
        }

        if (double.IsNaN(value))
        {
            return null;
        }

        double t;
        var x = Transform.IsLinear ? value : Transform.Forward(value, Reflect);
        if (Kind == ScaleKind.Sequential)
        {
            // d3: a domain of zero width gives the middle of the interpolator, otherwise the position in the (transformed) domain
            t = _k10.CompareTo(0) == 0 ? 0.5 : (x - _x0) * _k10;
            if (_k10.CompareTo(0) != 0 && Clamp)
            {
                t = Math.Max(0, Math.Min(1, t));
            }
        }
        else
        {
            t = 0.5 + ((x - _x0) * (_sign * x < _sign * _x0 ? _k10 : _k21));
            if (Clamp)
            {
                t = Math.Max(0, Math.Min(1, t));
            }
        }

        return double.IsFinite(t) ? Interpolator!(t) : null;
    }

    /// <summary>
    ///     Finds the position in the range that an ordinal or discretizing scale gives a data value.
    /// </summary>
    /// <remarks>
    ///     An ordinal scale gives the position of the category in the domain modulo the size of the range. The discretizing scales
    ///     read the value as a number (see <see cref="DataValue.TryGetQuantitative"/>) and find the interval with
    ///     <c>bisectRight</c> over the thresholds, as d3 does.
    /// </remarks>
    /// <param name="value">The data value.</param>
    /// <returns>The zero-based position in the range, or -1 when the value has no output (not in the domain, not a number, an empty range, or, for a bin-ordinal scale, below the first edge).</returns>
    public int MapIndex(DataValue value)
    {
        var count = _range.Count;
        if (count == 0)
        {
            return -1;
        }

        switch (Kind)
        {
            case ScaleKind.Ordinal:
                return _index.TryGetValue(value, out var category) ? category % count : -1;
            case ScaleKind.Quantize or ScaleKind.Quantile or ScaleKind.Threshold:
                return value.TryGetQuantitative(out var number) ? Math.Min(count - 1, BisectRight(_thresholds, number)) : -1;
            case ScaleKind.BinOrdinal:
                if (!value.TryGetQuantitative(out var edge))
                {
                    return -1;
                }

                var bin = BisectRight(_thresholds, edge) - 1;
                return bin < 0 ? -1 : bin % count;
            default:
                return -1;
        }
    }

    /// <summary>
    ///     Finds the number of thresholds that are not above a number (d3's <c>bisectRight</c>): the position where the number would be
    ///     inserted after its equals.
    /// </summary>
    /// <param name="thresholds">The thresholds, in ascending order.</param>
    /// <param name="x">The number.</param>
    /// <returns>The insertion position, from 0 to the number of thresholds.</returns>
    private static int BisectRight(double[] thresholds, double x)
    {
        var low = 0;
        var high = thresholds.Length;
        while (low < high)
        {
            var middle = (low + high) >>> 1;
            if (thresholds[middle] > x)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return low;
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
        return _range.TextAt(MapIndex(value));
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

        var result = Transform.Inverse(Inverse!.Evaluate(position), Reflect);
        return Clamp ? Math.Max(ClampLow, Math.Min(ClampHigh, result)) : result;
    }

    /// <summary>
    ///     Gets the position of a number between the ends of the domain of a linear, sequential or diverging scale, from 0 at the start to
    ///     1 at the end, along the transformed domain (Vega's <c>scaleFraction</c>: a log scale gives <c>log(v / start) / log(end / start)</c>).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The fraction; 0.5 when the domain has no span (or the span is not finite).</returns>
    public double Fraction(double value)
    {
        var from = Transform.Forward(DomainStart, Reflect);
        var to = Transform.Forward(DomainEnd, Reflect);
        var delta = to - from;
        if (delta.CompareTo(0) == 0 || !double.IsFinite(delta))
        {
            return 0.5;
        }

        return (Transform.Forward(value, Reflect) - from) / delta;
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

        return new ScaleModel(kind, categories, default, positions)
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
