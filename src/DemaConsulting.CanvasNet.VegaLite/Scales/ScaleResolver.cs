// Portions derived from Vega-Lite and Vega (BSD-3-Clause), d3-scale (ISC); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <summary>
///     One end of an explicit scale range: a number, or the keyword <c>width</c> or <c>height</c>.
/// </summary>
/// <param name="Value">The number (ignored for a keyword).</param>
/// <param name="IsExtent">Whether the end is the keyword <c>width</c> or <c>height</c>, which stands for the size of the data rectangle.</param>
internal readonly record struct RangeEnd(double Value, bool IsExtent);

/// <summary>
///     A scale as Vega-Lite defines it for one encoding channel, before the size of the data rectangle is known.
/// </summary>
/// <remarks>
///     <para>
///         Everything that follows from the specification and the data is decided: the scale kind, the domain (after
///         the <c>zero</c> rule), the padding, <c>nice</c>, the range of colors, sizes and opacities, and, for a
///         discrete position scale, the step that sizes the view. What depends on the size of the data rectangle (the
///         range of a position scale, the pixel padding of a continuous scale, <c>nice</c> after that padding) is applied
///         by <see cref="CreateModel"/> once the layout knows it.
///     </para>
///     <para>
///         The class is immutable and safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class ResolvedScale
{
    /// <summary>Gets the channel: <c>x</c>, <c>y</c>, <c>color</c>, <c>fill</c>, <c>stroke</c>, <c>size</c>, <c>opacity</c>, <c>shape</c>, <c>strokeWidth</c>, <c>strokeDash</c>, <c>fillOpacity</c>, <c>strokeOpacity</c> or <c>angle</c>.</summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>Gets the kind of scale.</summary>
    public ScaleKind Kind { get; init; }

    /// <summary>Gets the field path of the channel, or null when the channel is driven by a <c>datum</c>. For a binned, aggregated or stacked channel it is the derived field the marks read (the start of a bin, <c>sum_x</c>, the upper end of a stack).</summary>
    public string? FieldPath { get; init; }

    /// <summary>Gets the field path of the second end of the channel (the lower end of a stack, the end of a bin, the field of <c>x2</c> or <c>y2</c>), or null when it has none.</summary>
    public string? Field2Path { get; init; }

    /// <summary>Gets the default title of the channel (Vega-Lite's <c>Sum of x</c>, <c>x (binned)</c>, ...), or null when the channel reads no field.</summary>
    public string? Title { get; init; }

    /// <summary>Gets how the channel is binned.</summary>
    public BinKind Bin { get; init; }

    /// <summary>Gets the bins of a channel that the encoding bins (the domain of its continuous scale), or null.</summary>
    public BinSpec? BinSpec { get; init; }

    /// <summary>Gets the offset of the stack the channel carries, or null when its marks are not stacked.</summary>
    public StackOffset? Stack { get; init; }

    /// <summary>Gets the field type: <c>quantitative</c>, <c>temporal</c>, <c>nominal</c> or <c>ordinal</c>.</summary>
    public string DataType { get; init; } = string.Empty;

    /// <summary>Gets the location of the channel definition in the specification.</summary>
    public VegaLiteLocation Location { get; init; }

    /// <summary>Gets the start of the domain of a continuous scale after the zero rule, before pixel padding and <c>nice</c>.</summary>
    public double DomainStart { get; init; }

    /// <summary>Gets the end of the domain of a continuous scale after the zero rule, before pixel padding and <c>nice</c> (the last entry of <see cref="Domain"/>).</summary>
    public double DomainEnd { get; init; }

    /// <summary>
    ///     Gets the complete domain of a continuous scale after the zero rule, or null when the domain is the two ends
    ///     <see cref="DomainStart"/> and <see cref="DomainEnd"/>. For a linear scale it is the explicit domain of more than two numbers (a
    ///     piecewise scale, see <see cref="ScaleModel.CreateLinear(double[], double[], bool, bool, ScaleTransform)"/>); for a diverging scale the three
    ///     entries with the middle value; for a quantile scale all the valid values of the field in ascending order; for a threshold scale
    ///     the thresholds; for a bin-ordinal scale the bin edges.
    /// </summary>
    public IReadOnlyList<double>? Domain { get; init; }

    /// <summary>Gets the tick count <c>nice</c> uses (10 for <c>nice: true</c>), or null when the domain is not extended to round values.</summary>
    public double? NiceCount { get; init; }

    /// <summary>Gets the padding of a linear position scale in pixels (the domain is widened so the extreme values keep this distance from the edge).</summary>
    public double PaddingPixels { get; init; }

    /// <summary>Gets a value indicating whether a linear scale clamps values to its domain.</summary>
    public bool Clamp { get; init; }

    /// <summary>
    ///     Gets the transform of a continuous scale: the identity for a linear scale, the logarithm, signed power (sqrt is the power 0.5)
    ///     or symmetric log of a <c>log</c>, <c>pow</c>, <c>sqrt</c> or <c>symlog</c> scale (with its <c>base</c>, <c>exponent</c> or <c>constant</c>).
    /// </summary>
    public ScaleTransform Transform { get; init; } = ScaleTransform.Linear;

    /// <summary>Gets a value indicating whether positions are rounded to whole numbers.</summary>
    public bool Round { get; init; }

    /// <summary>Gets a value indicating whether the range runs backwards.</summary>
    public bool Reverse { get; init; }

    /// <summary>Gets the categories of a discrete scale, in domain order.</summary>
    public IReadOnlyList<DataValue> DiscreteDomain { get; init; } = [];

    /// <summary>Gets the inner padding of a band scale in steps; 1 for a point scale.</summary>
    public double PaddingInner { get; init; }

    /// <summary>Gets the outer padding of a band or point scale in steps.</summary>
    public double PaddingOuter { get; init; }

    /// <summary>Gets the alignment of a band or point scale.</summary>
    public double Align { get; init; } = 0.5;

    /// <summary>Gets the step of a discrete position scale whose size comes from <c>step</c>; null when the size is fixed.</summary>
    public double? Step { get; init; }

    /// <summary>Gets the explicit range of the specification, or null for the default range.</summary>
    public IReadOnlyList<RangeEnd>? ExplicitRange { get; init; }

    /// <summary>Gets the replacement of the first entry of a default two-entry range (<c>rangeMin</c>), or null.</summary>
    public double? RangeMin { get; init; }

    /// <summary>Gets the replacement of the second entry of a default two-entry range (<c>rangeMax</c>), or null.</summary>
    public double? RangeMax { get; init; }

    /// <summary>Gets the numeric range of a size, opacity, stroke width or angle scale (start and end).</summary>
    public (double Start, double End)? ValueRange { get; init; }

    /// <summary>Gets the numbers a discretizing or ordinal scale of a size, opacity, stroke width or angle gives (one per interval or category, cycling for an ordinal scale).</summary>
    public IReadOnlyList<double> NumberRange { get; init; } = [];

    /// <summary>Gets the colors of an ordinal color scale, and of a quantize, quantile, threshold or bin-ordinal color scale (one per interval).</summary>
    public IReadOnlyList<string> ColorRange { get; init; } = [];

    /// <summary>Gets the shapes (symbol names or SVG path strings) of a <c>shape</c> scale, one per category or interval.</summary>
    public IReadOnlyList<string> ShapeRange { get; init; } = [];

    /// <summary>Gets the dash patterns of a <c>strokeDash</c> scale, one per category or interval.</summary>
    public IReadOnlyList<IReadOnlyList<double>> DashRange { get; init; } = [];

    /// <summary>Gets the interpolator of a sequential or diverging color scale (a color for a position from 0 to 1, after the scheme extent and the reversal), or null.</summary>
    public Func<double, RgbColor>? Interpolator { get; init; }

    /// <summary>
    ///     Gets the calendar of the field's instants (UTC for a UTC scale or a UTC time unit, otherwise the time zone of the chart), or null when the field is neither temporal nor has a time unit.
    /// </summary>
    public TimeCalendar? Calendar { get; init; }

    /// <summary>Gets the time unit of the field (the resolved unit for the <c>maxbins</c> form), or null.</summary>
    public TimeUnitSpec? TimeUnit { get; init; }

    /// <summary>Gets a value indicating whether the values of the field are instants: the field is temporal or has a time unit.</summary>
    public bool IsTemporalField { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the channel spans its time unit: <see cref="Field2Path"/> is the end of the unit (Vega-Lite's <c>hasBandEnd</c>), so a bar, rect or tick
    ///     covers the unit and a point can sit anywhere between its start and its end. It is false for a range that the encoding gives (<c>x2</c>, <c>y2</c>).
    /// </summary>
    public bool HasBandEnd { get; init; }

    /// <summary>Gets the interval (a name with an optional step) that <c>nice</c> extends a time scale to, or null when <c>nice</c> uses <see cref="NiceCount"/> or is off.</summary>
    public TimeTickRequest? NiceInterval { get; init; }

    /// <summary>Gets a value indicating whether the channel is a position channel (<c>x</c> or <c>y</c>).</summary>
    public bool IsPosition => Channel is "x" or "y";

    /// <summary>
    ///     Gets the extent of the data rectangle that a discrete position scale with a <c>step</c> needs: the step times
    ///     the number of steps (Vega's <c>bandspace</c>).
    /// </summary>
    /// <returns>The width (for x) or height (for y) in pixels; null when the size does not come from a step.</returns>
    public double? NaturalExtent
    {
        get
        {
            if (Step is not { } step || Kind is not (ScaleKind.Band or ScaleKind.Point))
            {
                return null;
            }

            var inner = Kind == ScaleKind.Point ? 1 : PaddingInner;
            return step * ScaleModel.BandSpace(DiscreteDomain.Count, inner, PaddingOuter);
        }
    }

    /// <summary>
    ///     Creates the scale model for a data rectangle of a given size.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The range of an x scale is <c>[0, extent]</c>; that of a continuous y scale is <c>[extent, 0]</c> (the
    ///         origin of a view is at the top) and that of a discrete y scale <c>[0, extent]</c>; an explicit range
    ///         replaces them (the keywords <c>width</c> and <c>height</c> stand for <paramref name="extent"/>) and <c>rangeMin</c> and
    ///         <c>rangeMax</c> replace the first and the second entry of a default range; a
    ///         reversed scale swaps the two ends. Size and opacity scales use their own ranges and ignore the extent. The scales
    ///         that give colors, shapes, dash patterns or the numbers of discrete intervals (sequential, diverging, quantize, quantile,
    ///         threshold, bin-ordinal and ordinal) do not depend on the extent.
    ///     </para>
    ///     <para>
    ///         A continuous position scale applies its pixel padding first (the domain is widened around its middle so
    ///         that the end values keep that distance from the edge) and then <c>nice</c> (the domain is extended to
    ///         round tick values); if <c>nice</c> cannot find a finite domain the padded domain stays.
    ///     </para>
    /// </remarks>
    /// <param name="extent">The width (x) or height (y) of the data rectangle in pixels.</param>
    /// <returns>The scale model.</returns>
    public ScaleModel CreateModel(double extent)
    {
        switch (Kind)
        {
            case ScaleKind.Linear:
                var range = ResolveRange(extent);
                return ScaleModel.CreateLinear(ExtendedDomain(Math.Abs(range[^1] - range[0])), range, Clamp, Round, Transform);
            case ScaleKind.Time or ScaleKind.Utc:
                var timeRange = ResolveRange(extent);
                return ScaleModel.CreateTime(ExtendedDomain(Math.Abs(timeRange[^1] - timeRange[0])), timeRange, Clamp, Round, Calendar ?? TimeCalendar.Utc, Kind == ScaleKind.Utc);
            case ScaleKind.Sequential or ScaleKind.Diverging:
                return ScaleModel.CreateSequential(ExtendedDomain(0), Interpolator ?? (t => default), Clamp, Transform);
            case ScaleKind.Quantize:
                var quantizeDomain = ExtendedDomain(0);
                return ScaleModel.CreateQuantize(quantizeDomain[0], quantizeDomain[^1], OutputRange());
            case ScaleKind.Quantile:
                return ScaleModel.CreateQuantile(DomainArray(), OutputRange());
            case ScaleKind.Threshold:
                return ScaleModel.CreateThreshold(DomainArray(), OutputRange());
            case ScaleKind.BinOrdinal:
                return ScaleModel.CreateBinOrdinal(DomainArray(), OutputRange());

            // A band or point scale takes the first two entries of its range, as d3's scaleBand does
            case ScaleKind.Band:
                var bandRange = ResolveRange(extent);
                return ScaleModel.CreateBand(DiscreteDomain, bandRange[0], bandRange[1], PaddingInner, PaddingOuter, Align, Round);
            case ScaleKind.Point:
                var pointRange = ResolveRange(extent);
                return ScaleModel.CreatePoint(DiscreteDomain, pointRange[0], pointRange[1], PaddingOuter, Align, Round);
            default:
                return ScaleModel.CreateOrdinal(DiscreteDomain, OutputRange());
        }
    }

    /// <summary>
    ///     Gets the output values of an ordinal or discretizing scale: the shapes of a shape scale, the number of dash patterns, the
    ///     numbers of a numeric channel or the colors.
    /// </summary>
    /// <returns>The range.</returns>
    private ScaleRange OutputRange()
    {
        if (DashRange.Count > 0)
        {
            return ScaleRange.OfCount(DashRange.Count);
        }

        if (Channel == "shape")
        {
            return ScaleRange.OfTexts(ShapeRange);
        }

        return NumberRange.Count > 0 ? ScaleRange.OfNumbers(NumberRange) : ScaleRange.OfTexts(ColorRange);
    }

    /// <summary>
    ///     Gets the domain of a quantile, threshold or bin-ordinal scale as an array.
    /// </summary>
    /// <returns>The domain (the array itself when it is one, so a long quantile domain is not copied).</returns>
    private double[] DomainArray() => Domain is double[] array ? array : [.. Domain ?? [DomainStart, DomainEnd]];

    /// <summary>
    ///     Applies the pixel padding and <c>nice</c> to the first and the last entry of the domain of a continuous scale.
    /// </summary>
    /// <remarks>
    ///     Vega applies padding, zero and nice to the first and the last entry of a piecewise domain and leaves the others as they are.
    /// </remarks>
    /// <param name="span">The extent of the range in pixels, which the padding is taken from.</param>
    /// <returns>The domain (at least two entries).</returns>
    private double[] ExtendedDomain(double span)
    {
        double[] domain = Domain is { Count: > 2 } complete ? [.. complete] : [DomainStart, DomainEnd];
        var start = domain[0];
        var end = domain[^1];

        // Pixel padding widens the domain around its middle (in the transformed domain of a log, pow or symlog scale, as Vega does)
        if (PaddingPixels > 0 && start.CompareTo(end) != 0 && span - (2 * PaddingPixels) > 0)
        {
            var factor = span / (span - (2 * PaddingPixels));
            if (Transform.IsLinear)
            {
                var middle = (start + end) / 2;
                start = middle + ((start - middle) * factor);
                end = middle + ((end - middle) * factor);
            }
            else
            {
                var reflect = Transform.Kind == ScaleTransformKind.Log && start < 0;
                var from = Transform.Forward(start, reflect);
                var to = Transform.Forward(end, reflect);
                var middle = (from + to) / 2;
                var paddedStart = Transform.Inverse(middle + ((from - middle) * factor), reflect);
                var paddedEnd = Transform.Inverse(middle + ((to - middle) * factor), reflect);
                if (double.IsFinite(paddedStart) && double.IsFinite(paddedEnd))
                {
                    start = paddedStart;
                    end = paddedEnd;
                }
            }
        }

        // Then the domain is extended to round values, if that gives a usable domain (a log scale to powers of its base, a time scale to the boundaries of a calendar interval)
        if (Kind is ScaleKind.Time or ScaleKind.Utc)
        {
            var request = NiceInterval ?? (NiceCount is { } niceCount ? new TimeTickRequest(niceCount) : (TimeTickRequest?)null);
            if (request is { } timeRequest)
            {
                (start, end) = Ticks.TimeNice(start, end, timeRequest, Calendar ?? TimeCalendar.Utc, Kind == ScaleKind.Utc);
            }
        }
        else if (NiceCount is { } count)
        {
            var (niceStart, niceEnd) = Transform.Kind == ScaleTransformKind.Log
                ? Ticks.LogNice(start, end, Transform.Parameter)
                : Ticks.Nice(start, end, count);
            if (double.IsFinite(niceStart) && double.IsFinite(niceEnd))
            {
                start = niceStart;
                end = niceEnd;
            }
        }

        domain[0] = start;
        domain[^1] = end;
        return domain;
    }

    /// <summary>
    ///     Works out the range: the entries of an explicit range, or the two ends of the default one.
    /// </summary>
    /// <param name="extent">The size of the data rectangle along the axis.</param>
    /// <returns>The range entries (at least two); a reversed scale gets them in reverse order, as Vega reverses the whole array.</returns>
    private double[] ResolveRange(double extent)
    {
        double[] range;
        if (ExplicitRange is { Count: >= 2 } explicitRange)
        {
            range = [.. explicitRange.Select(entry => entry.IsExtent ? extent : entry.Value)];
        }
        else
        {
            if (IsPosition)
            {
                // The origin of a view is its top left corner, so a continuous y scale runs upward
                range = Channel == "y" && Kind is ScaleKind.Linear or ScaleKind.Time or ScaleKind.Utc ? [extent, 0.0] : [0.0, extent];
            }
            else if (ValueRange is { } values)
            {
                range = [values.Start, values.End];
            }
            else
            {
                range = [0.0, 1.0];
            }

            // rangeMin and rangeMax replace the first and the second entry of a default range (not of a range that a step sizes)
            if (Step is null)
            {
                range[0] = RangeMin ?? range[0];
                range[1] = RangeMax ?? range[1];
            }
        }

        if (Reverse)
        {
            Array.Reverse(range);
        }

        return range;
    }
}

/// <summary>
///     The scales of one view and the facts about the view they were resolved against.
/// </summary>
/// <remarks>
///     The class is immutable. It holds one <see cref="ResolvedScale"/> per channel that has a scale; channels with a
///     <c>value</c> or without a field have none, and neither do channels whose scale is not drawn in this package
///     (a <c>size</c> on a mark without sizes, a <c>shape</c> on a mark other than <c>point</c>).
/// </remarks>
internal sealed class ResolvedScales
{
    /// <summary>The scales by channel name.</summary>
    private readonly Dictionary<string, ResolvedScale> _scales;

    /// <summary>
    ///     Initializes the result.
    /// </summary>
    /// <param name="markType">The mark type.</param>
    /// <param name="orient">The orientation of the mark.</param>
    /// <param name="scales">The resolved scales.</param>
    /// <param name="plan">The encoding plan the scales were resolved with, or null.</param>
    internal ResolvedScales(string markType, string? orient, Dictionary<string, ResolvedScale> scales, EncodingPlan? plan = null)
    {
        MarkType = markType;
        Orient = orient;
        _scales = scales;
        Plan = plan;
    }

    /// <summary>Gets the plan of the encoding the scales were resolved with (field names, titles, stack), or null.</summary>
    public EncodingPlan? Plan { get; }

    /// <summary>Gets the mark type of the view (<c>bar</c>, <c>point</c> and so on); empty when the view has no mark.</summary>
    public string MarkType { get; }

    /// <summary>Gets the orientation of the mark as Vega-Lite infers it (<c>horizontal</c> or <c>vertical</c>), or null when the mark has none.</summary>
    public string? Orient { get; }

    /// <summary>Gets the channels that have a scale, in the order x, y, color, fill, stroke, size, opacity, shape, strokeWidth, strokeDash, fillOpacity, strokeOpacity, angle.</summary>
    public IEnumerable<ResolvedScale> Scales => _scales.Values;

    /// <summary>Gets the width the data rectangle needs when the x scale is discrete and sized by a step; otherwise null.</summary>
    public double? DiscreteWidth => this["x"]?.NaturalExtent;

    /// <summary>Gets the height the data rectangle needs when the y scale is discrete and sized by a step; otherwise null.</summary>
    public double? DiscreteHeight => this["y"]?.NaturalExtent;

    /// <summary>
    ///     Gets the scale of a channel.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <returns>The scale, or <see langword="null"/> when the channel has none.</returns>
    public ResolvedScale? this[string channel] => _scales.GetValueOrDefault(channel);
}

/// <summary>
///     Vega-Lite's scale inference for the first charts: for every encoding channel it decides the kind of scale, the
///     domain, the padding and the range from the specification, the data and the configuration.
/// </summary>
/// <remarks>
///     <para>
///         The rules are those of Vega-Lite 6 (<c>compile/scale/type.ts</c>, <c>domain.ts</c>, <c>properties.ts</c>,
///         <c>range.ts</c>) and of the Vega scale transform that applies them:
///     </para>
///     <list type="bullet">
///         <item><description>
///             <b>Kind.</b> A quantitative field gets a linear scale, which on <c>color</c>, <c>fill</c> and <c>stroke</c> is a sequential
///             scale (a diverging scale when the domain has three entries, which <c>domainMid</c> makes of two). A nominal or ordinal field
///             on <c>x</c> or <c>y</c> gets a band scale for the marks <c>bar</c>, <c>rect</c>, <c>rule</c> and <c>tick</c> (they must
///             fit into a band) and a point scale for the others; on <c>color</c>, <c>fill</c>, <c>stroke</c>, <c>shape</c> and
///             <c>strokeDash</c> an ordinal scale (so does a quantitative field on <c>shape</c> and <c>strokeDash</c>, which accept
///             only discrete outputs); on <c>size</c>, <c>opacity</c>, <c>strokeWidth</c>, <c>fillOpacity</c>, <c>strokeOpacity</c> and
///             <c>angle</c> a point scale. A temporal field has no scale. An explicit <c>scale.type</c> is used when the channel and the field
///             type accept it (Vega-Lite's <c>channelSupportScaleType</c> and <c>scaleTypeSupportDataType</c>: linear, sequential (colors
///             only), quantile, quantize, threshold, band, point and ordinal; the others are not drawn yet), otherwise the default applies.
///         </description></item>
///         <item><description>
///             <b>Domain.</b> A linear domain is the extent of the valid values (numbers, booleans as 1/0, numeric
///             strings; null and NaN are ignored; an empty extent is [0, 1]); then <c>zero</c> (true for a quantitative
///             size that is not discretized; for x and y true for <c>bar</c> and <c>area</c> marks and otherwise the configured default true,
///             except on the discrete axis of a bar; with a custom domain only when it already spans zero) pulls the end
///             values to zero, <c>domainMin</c> and <c>domainMax</c> replace the first and the last entry and <c>domainMid</c> (colors) inserts the middle
///             value (Vega's <c>configureDomain</c>). A quantize scale has the same extent; a quantile scale all the valid values in ascending
///             order; a threshold scale the explicit thresholds, or the extent. A discrete domain is the distinct values in order of first appearance, sorted ascending
///             by default (<c>sort</c> may be ascending, descending, null for data order, an array, an object with an aggregate operation and a
///             field, an <c>encoding</c> channel, or a channel shorthand such as <c>"-y"</c>, see <c>ScaleResolver.Sort.cs</c>); an explicit
///             <c>scale.domain</c> is used as given. The fields a channel reads and its default title come from the <see cref="EncodingPlan"/>: the
///             domain of a stacked channel spans the lower and the upper ends of the stack (<c>[0, 1]</c> when normalized), that of a range the
///             fields of both channels, that of a binned field the start of the first bin and the stop of the last (no zero, no nice), and
///             <c>scale.domain: "unaggregated"</c> reads the rows before the aggregate; a discrete channel that is binned has the text of its bins as
///             categories, ordered by where they start.
///         </description></item>
///         <item><description>
///             <b>Padding and nice.</b> A band scale has inner padding from the configuration by mark (bar 0.1, tick
///             0.25, others 0) and outer padding half of it; a point scale 0.5; <c>scale.padding</c> sets both. A
///             continuous x or y scale gets <c>nice</c> with a count of 10 unless a domain is given, and a bar on its continuous
///             axis gets 5 pixels of padding. An explicit <c>nice</c> also applies to sequential, diverging and quantize scales.
///         </description></item>
///         <item><description>
///             <b>Range.</b> x is [0, width], y is [height, 0] (continuous) or [0, height] (discrete). Size uses
///             [minimum, maximum] by mark (point-like marks: [4, (0.95 step)^2]; bar and tick: [2, step - 1]; rule: [1,
///             4]); <c>strokeWidth</c> [1, 4]; opacities [0.3, 0.8]; <c>angle</c> [0, 360]; a discretizing size scale
///             (quantile, quantize, threshold) spreads the numbers evenly over [minimum, maximum] with one value per
///             interval (<c>quantileCount</c> 4, <c>quantizeCount</c> 4, one more than the thresholds). Colors: an ordinal scale takes the
///             named range <c>category</c> (nominal) or <c>ordinal</c> (ordinal) of <c>config.range</c>, a continuous scale <c>diverging</c> (with
///             <c>domainMid</c>), <c>heatmap</c> (a <c>rect</c> mark) or <c>ramp</c>; a named range is a list of colors or a scheme (<c>scheme</c>,
///             <c>count</c>, <c>extent</c>); the scale's own <c>range</c> (an array of colors, or the name of a configured range) and then its
///             <c>scheme</c> (a name, a list of colors or an object with <c>name</c>, <c>count</c>, <c>extent</c>) win over the default (see
///             <see cref="ColorSchemes.Configure"/> for the colors a scheme gives); <c>interpolate</c> chooses the color space of a list of colors;
///             <c>reverse</c> reverses the colors. <c>shape</c> takes the <c>symbol</c> range of the configuration and <c>strokeDash</c> five fixed
///             patterns. A discrete position
///             scale without an explicit range or a fixed size takes its size from the <c>step</c> (20 pixels by
///             default): see <see cref="ResolvedScale.NaturalExtent"/>. An explicit linear domain and range keep all
///             their entries and map piecewise like d3's <c>polymap</c> (<c>min(domain.length, range.length)</c> entries
///             are used, <c>zero</c>, pixel padding and <c>nice</c> change only the first and the last domain entry); a band
///             or point scale uses the first two entries of its range, as <c>scaleBand</c> does. <c>rangeMin</c> and <c>rangeMax</c> replace the
///             first and the second entry of a default two-entry range.
///         </description></item>
///         <item><description>
///             <b>Limits and cancellation.</b> A domain, range, scheme count or color list of more than
///             <see cref="VegaLiteLimits.MaxScaleDomainSize"/> entries fails with a located <see cref="VegaLiteRenderException"/> before it is built;
///             the values of a quantile scale are counted against <see cref="TableLayout.MaxCells"/> before they are read, charged to the
///             evaluation-step budget (one step per 64 values read, and per 64 comparisons of the sort) and the token is polled while they are read
///             and sorted.
///         </description></item>
///     </list>
///     <para>
///         Deviations from Vega-Lite: values of different kinds in one discrete domain are ordered by kind instead of
///         by JavaScript's coercions (see <see cref="DataValue.Compare"/>); <c>scale.domain</c> forms other than an array are
///         ignored; a property that the scale type or the channel does not support (for example <c>scheme</c> on a size channel) is ignored
///         without Vega-Lite's console warning; a color scale with an explicit domain of four or more entries (a piecewise color scale) is
///         a specification error; an array of colors given as <c>scheme</c> of a discrete scale is used as the list of colors, whole.
///         The class is stateless and thread-safe.
///     </para>
/// </remarks>
internal static partial class ScaleResolver
{
    /// <summary>The channels that are resolved, in the order of the result.</summary>
    private static readonly string[] Channels =
        ["x", "y", "color", "fill", "stroke", "size", "opacity", "shape", "strokeWidth", "strokeDash", "fillOpacity", "strokeOpacity", "angle"];

    /// <summary>The marks whose discrete positions are bands, so that the mark fits into the band.</summary>
    private static readonly HashSet<string> BandMarks = ["rect", "bar", "image", "rule", "tick"];

    /// <summary>The aggregate operations that an <c>unaggregated</c> domain accepts (Vega-Lite's shared domain operations).</summary>
    private static readonly HashSet<AggregateOp> SharedDomainOperations =
        [AggregateOp.Mean, AggregateOp.Median, AggregateOp.Q1, AggregateOp.Q3, AggregateOp.Min, AggregateOp.Max];

    /// <summary>The ratio of the step that a point-like mark's largest size may span (Vega-Lite's MAX_SIZE_RANGE_STEP_RATIO).</summary>
    private const double MaxSizeRangeStepRatio = 0.95;

    /// <summary>The step Vega-Lite uses when the configuration has none.</summary>
    private const double DefaultStep = 20;

    /// <summary>The number of rows between two cancellation checks.</summary>
    private const int CancellationInterval = 4096;

    /// <summary>
    ///     Resolves the scales of a single view whose data needs no implicit transform, or runs the implicit transforms with a
    ///     context of its own first.
    /// </summary>
    /// <remarks>
    ///     This is the convenience entry for callers that have only the rows after the explicit transforms (the tests and tools);
    ///     the document pipeline runs <see cref="EncodingTransforms"/> in its own call context and uses
    ///     <see cref="Resolve(SpecNode, EncodingPlan, EncodingData, ResolvedConfig, string?, VegaLiteLimits?, CancellationToken)"/>.
    ///     The row budget of that private context starts empty.
    /// </remarks>
    /// <param name="view">The view object of the specification (its <c>mark</c>, <c>encoding</c>, <c>width</c> and <c>height</c> are read).</param>
    /// <param name="table">The data of the view.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="sourceName">The source name used in the locations of errors, or null.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="cancellationToken">A token that stops the resolution.</param>
    /// <returns>The scales; empty when the view has no mark or no encoding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="view"/>, <paramref name="table"/> or <paramref name="config"/> is null.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown when <c>scale.domain</c> or <c>scale.range</c> has the wrong shape; carries the location.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when a discrete domain exceeds <see cref="VegaLiteLimits.MaxScaleDomainSize"/> or an implicit transform exceeds a limit; carries the location.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static ResolvedScales Resolve(
        SpecNode view,
        DataTable table,
        ResolvedConfig config,
        string? sourceName = null,
        VegaLiteLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(config);
        var plan = EncodingPlan.Create(view, config);
        var data = EncodingTransforms.ApplyStandalone(plan, table, view, sourceName, limits, cancellationToken);
        return Resolve(view, plan, data, config, sourceName, limits, cancellationToken);
    }

    /// <summary>
    ///     Resolves the scales of a single view.
    /// </summary>
    /// <param name="view">The view object of the specification (its <c>mark</c>, <c>encoding</c>, <c>width</c> and <c>height</c> are read).</param>
    /// <param name="plan">The plan of the encoding (field names, titles, bins, stack).</param>
    /// <param name="data">The tables of the implicit pipeline and the bins it computed.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="sourceName">The source name used in the locations of errors, or null.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="cancellationToken">A token that stops the resolution.</param>
    /// <returns>The scales; empty when the view has no mark or no encoding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than the optional ones is null.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown when <c>scale.domain</c> or <c>scale.range</c> has the wrong shape; carries the location.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when a discrete domain exceeds <see cref="VegaLiteLimits.MaxScaleDomainSize"/>; carries the location.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static ResolvedScales Resolve(
        SpecNode view,
        EncodingPlan plan,
        EncodingData data,
        ResolvedConfig config,
        string? sourceName = null,
        VegaLiteLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(config);

        // The mark decides the scale kinds, the zero rule, the paddings and the size range
        var markType = plan.MarkType;
        var encoding = view["encoding"] is { IsObject: true } e ? e : null;
        var orient = plan.Orient;
        var scales = new Dictionary<string, ResolvedScale>(StringComparer.Ordinal);
        if (markType.Length == 0 || encoding is null)
        {
            return new ResolvedScales(markType, orient, scales, plan);
        }

        var context = new Context
        {
            Plan = plan,
            Data = data,
            Config = config,
            MarkType = markType,
            Orient = orient,
            Encoding = encoding,
            SourceName = sourceName,
            Limits = limits ?? VegaLiteLimits.Default,
            CancellationToken = cancellationToken,
            WidthStep = StepOf(view["width"], config, "width"),
            HeightStep = StepOf(view["height"], config, "height"),
            MinStep = Math.Min(MinStepOf(view["width"], config, "width"), MinStepOf(view["height"], config, "height")),
        };

        foreach (var channel in Channels)
        {
            if (encoding[channel] is { IsObject: true } definition && ResolveChannel(channel, definition, context) is { } scale)
            {
                scales[channel] = scale;
            }
        }

        return new ResolvedScales(markType, orient, scales, plan);
    }

    /// <summary>
    ///     Infers the orientation of a mark the way Vega-Lite does.
    /// </summary>
    /// <remarks>
    ///     Point, circle, square and rect marks have none. A bar is horizontal when only x is a measure (an unbinned
    ///     quantitative field) and vertical when only y is, a tick the other way round; with two measures all three
    ///     are vertical; a rule is vertical when it has only x and horizontal when it has only y. An explicit orient
    ///     of the mark wins where Vega-Lite lets it.
    /// </remarks>
    /// <param name="markType">The mark type.</param>
    /// <param name="specified">The orient given in the mark definition, or null.</param>
    /// <param name="encoding">The encoding object, or null.</param>
    /// <returns><c>horizontal</c>, <c>vertical</c> or null.</returns>
    public static string? InferOrient(string markType, string? specified, SpecNode? encoding)
    {
        if (markType is "point" or "circle" or "square" or "rect" or "image" or "")
        {
            return null;
        }

        var x = encoding?["x"] is { IsObject: true } xd ? xd : null;
        var y = encoding?["y"] is { IsObject: true } yd ? yd : null;
        if (markType == "bar")
        {
            // An aggregated measure next to a plain dimension decides the orientation
            if (IsAggregated(y) && !IsAggregated(x))
            {
                return "vertical";
            }

            if (IsAggregated(x) && !IsAggregated(y))
            {
                return "horizontal";
            }
        }

        if (markType == "rule")
        {
            if (x is not null && y is null)
            {
                return "vertical";
            }

            if (y is not null && x is null)
            {
                return "horizontal";
            }
        }

        var xIsMeasure = IsUnbinnedQuantitative(x);
        var yIsMeasure = IsUnbinnedQuantitative(y);
        if (specified is "horizontal" or "vertical")
        {
            return specified;
        }

        if (xIsMeasure && !yIsMeasure)
        {
            return markType != "tick" ? "horizontal" : "vertical";
        }

        if (!xIsMeasure && yIsMeasure)
        {
            return markType != "tick" ? "vertical" : "horizontal";
        }

        return xIsMeasure && yIsMeasure ? "vertical" : null;
    }

    /// <summary>
    ///     Tells whether a channel definition is an unbinned quantitative field or datum.
    /// </summary>
    /// <param name="definition">The channel definition, or null.</param>
    /// <returns><see langword="true"/> for a measure.</returns>
    private static bool IsUnbinnedQuantitative(SpecNode? definition) =>
        definition is not null
        && (definition["field"] is not null || definition["datum"] is not null || definition["aggregate"]?.AsString() == "count")
        && ReadType(definition, definition["datum"]) == "quantitative"
        && !IsEnabled(definition["bin"]);

    /// <summary>
    ///     Tells whether a channel definition aggregates its field.
    /// </summary>
    /// <param name="definition">The channel definition, or null.</param>
    /// <returns><see langword="true"/> when the definition has an aggregate.</returns>
    private static bool IsAggregated(SpecNode? definition) => definition?["aggregate"] is { IsNull: false };

    /// <summary>
    ///     Tells whether a switch property is on (anything but null and false).
    /// </summary>
    /// <param name="node">The property value, or null when absent.</param>
    /// <returns><see langword="true"/> when the feature is switched on.</returns>
    private static bool IsEnabled(SpecNode? node) => node is { IsNull: false } && !(node.IsBoolean && !node.BoolValue);

    /// <summary>
    ///     Reads the type of a channel definition, defaulting like Vega-Lite does.
    /// </summary>
    /// <param name="definition">The channel definition.</param>
    /// <param name="datum">The datum of the definition, or null.</param>
    /// <returns>The type text (<c>quantitative</c>, <c>nominal</c>, <c>ordinal</c>, <c>temporal</c>, <c>geojson</c>).</returns>
    private static string ReadType(SpecNode definition, SpecNode? datum)
    {
        if (definition["type"]?.AsString() is { } type)
        {
            return type;
        }

        // Vega-Lite infers a datum's type from its value and assumes a quantitative field when no type is given
        return datum switch
        {
            { IsNumber: true } => "quantitative",
            { IsString: true } or { IsBoolean: true } => "nominal",
            _ => "quantitative",
        };
    }

    /// <summary>
    ///     Gets the step that sizes a discrete position scale: the specification's <c>{step}</c> or the configured one.
    /// </summary>
    /// <param name="size">The <c>width</c> or <c>height</c> node of the view, or null.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="name">The property name: <c>width</c> or <c>height</c>.</param>
    /// <returns>The step; null when the specification (or the configuration) fixes the size instead.</returns>
    private static double? StepOf(SpecNode? size, ResolvedConfig config, string name)
    {
        // The specification decides when it says anything: a step object or a fixed size
        if (size is not null)
        {
            return size.IsObject && size["step"] is { IsNumber: true } step && step.NumberValue > 0 ? step.NumberValue : null;
        }

        // Otherwise the configured discrete size: a step object, a fixed size, or the shared step
        var configured = config.Get("view." + name) ?? config.Get("view.discrete" + (name == "width" ? "Width" : "Height"));
        if (configured is null)
        {
            return config.GetNumber("view.step") is { } shared && shared > 0 ? shared : DefaultStep;
        }

        return configured.IsObject && configured["step"]?.AsNumber() is { } configuredStep && configuredStep > 0 ? configuredStep : null;
    }

    /// <summary>
    ///     Gets the step that limits the size of marks: the specification's <c>{step}</c> or the configured discrete step.
    /// </summary>
    /// <param name="size">The <c>width</c> or <c>height</c> node of the view, or null.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="name">The property name: <c>width</c> or <c>height</c>.</param>
    /// <returns>The step.</returns>
    private static double MinStepOf(SpecNode? size, ResolvedConfig config, string name)
    {
        if (size is { IsObject: true } && size["step"] is { IsNumber: true } step)
        {
            return step.NumberValue;
        }

        var configured = config.Get("view." + name) ?? config.Get("view.discrete" + (name == "width" ? "Width" : "Height"));
        if (configured is null)
        {
            return config.GetNumber("view.step") ?? DefaultStep;
        }

        return configured.IsObject && configured["step"]?.AsNumber() is { } configuredStep ? configuredStep : DefaultStep;
    }

    /// <summary>
    ///     Resolves the scale of one channel.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <param name="channelDefinition">The channel definition object (its field condition, when it has one, carries the field).</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The scale, or <see langword="null"/> when the channel has none.</returns>
    private static ResolvedScale? ResolveChannel(string channel, SpecNode channelDefinition, Context context)
    {
        // The scale belongs to the field definition of the channel: the definition itself, or the condition that holds the field
        var plan = context.Plan.Channels.FirstOrDefault(c => c.Definition == channelDefinition);
        var definition = plan?.FieldDef ?? channelDefinition;

        // A channel without a field or datum has no scale (a value is used as it is), and so has "scale": null
        var scaleNode = definition["scale"];
        if (scaleNode is { IsNull: true })
        {
            return null;
        }

        var scaleDef = scaleNode is { IsObject: true } ? scaleNode : null;
        var fieldPath = plan?.FieldPath;
        var datum = definition["datum"] is { IsString: true } or { IsNumber: true } or { IsBoolean: true } ? definition["datum"] : null;
        if (fieldPath is null && datum is null)
        {
            return null;
        }

        var type = ReadType(definition, datum);
        if (type is not ("quantitative" or "temporal" or "nominal" or "ordinal"))
        {
            return null;
        }

        if (!ChannelIsDrawn(channel, context.MarkType))
        {
            return null;
        }

        var binning = plan is { Bin: BinKind.Binning } && type == "quantitative";
        var (unit, isTemporal) = ReadTimeUnit(plan, type, context);
        var kind = ChooseKind(channel, type, scaleDef?["type"]?.AsString(), context.MarkType, binning, unit is { Utc: true }, out var transformKind);
        var location = definition.GetLocation(context.SourceName);
        var time = MakeTimeFacts(unit, isTemporal, kind, context);
        var common = new CommonInputs(channel, type, fieldPath, datum, definition, scaleDef, location, plan, transformKind, time);
        return kind is ScaleKind.Band or ScaleKind.Point or ScaleKind.Ordinal or ScaleKind.BinOrdinal
            ? BuildDiscrete(common, kind, context)
            : BuildContinuous(common, kind, context);
    }

    /// <summary>
    ///     Tells whether Vega-Lite draws a channel for a mark (its <c>supportMark</c>): <c>shape</c> only on <c>point</c>, <c>angle</c> on
    ///     <c>point</c>, <c>circle</c> and <c>square</c> (a circle looks the same turned), <c>strokeDash</c> and <c>size</c> on every mark but <c>rect</c>; the
    ///     other channels on every mark this package draws (a <c>size</c> on a mark without sizes, such as a rect, has no range, which the scale builders check too).
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="markType">The mark type.</param>
    /// <returns><see langword="true"/> when the mark uses the channel.</returns>
    internal static bool ChannelIsDrawn(string channel, string markType) => channel switch
    {
        "shape" => markType == "point",
        "angle" => markType is "point" or "circle" or "square",
        "strokeDash" or "size" => markType != "rect",
        _ => true,
    };

    /// <summary>The groups of channels that Vega-Lite treats alike when it picks and checks a scale type.</summary>
    private enum ChannelGroup
    {
        /// <summary><c>x</c> and <c>y</c>.</summary>
        Position,

        /// <summary><c>color</c>, <c>fill</c> and <c>stroke</c>: a continuous or a discrete range.</summary>
        Color,

        /// <summary><c>shape</c> and <c>strokeDash</c>: a discrete range only.</summary>
        Discrete,

        /// <summary><c>size</c>, <c>opacity</c>, <c>strokeWidth</c>, <c>fillOpacity</c>, <c>strokeOpacity</c> and <c>angle</c>: a numeric range.</summary>
        Numeric,
    }

    /// <summary>
    ///     Gets the group of a channel.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <returns>The group.</returns>
    private static ChannelGroup GroupOf(string channel) => channel switch
    {
        "x" or "y" => ChannelGroup.Position,
        "color" or "fill" or "stroke" => ChannelGroup.Color,
        "shape" or "strokeDash" => ChannelGroup.Discrete,
        _ => ChannelGroup.Numeric,
    };

    /// <summary>
    ///     Chooses the kind of scale for a channel and a field type.
    /// </summary>
    /// <remarks>
    ///     The default is Vega-Lite's <c>defaultType</c>; an explicit type counts when the channel accepts it
    ///     (<c>channelSupportScaleType</c>) and the field type accepts it (<c>scaleTypeSupportDataType</c>). The linear scale of a color is a
    ///     sequential scale (<see cref="ScaleKind.Sequential"/>; the resolver makes a diverging one of it when the domain has a middle value).
    /// </remarks>
    /// <param name="channel">The channel.</param>
    /// <param name="type">The field type.</param>
    /// <param name="specified">The explicit <c>scale.type</c>, or null.</param>
    /// <param name="markType">The mark type.</param>
    /// <param name="binning">Whether the encoding bins the quantitative field (a binned color gets the bin-ordinal scale by default).</param>
    /// <param name="utcUnit">Whether the field has a UTC time unit (its temporal default scale is a UTC scale).</param>
    /// <param name="transformKind">Receives the transform of the scale: log, pow (also for sqrt) or symlog for the matching explicit type, else linear.</param>
    /// <returns>The kind.</returns>
    private static ScaleKind ChooseKind(string channel, string type, string? specified, string markType, bool binning, bool utcUnit, out ScaleTransformKind transformKind)
    {
        var group = GroupOf(channel);
        transformKind = ScaleTransformKind.Linear;

        // The default kind of Vega-Lite's defaultType
        ScaleKind defaultKind;
        if (type == "quantitative")
        {
            defaultKind = group switch
            {
                ChannelGroup.Color => binning ? ScaleKind.BinOrdinal : ScaleKind.Sequential,
                ChannelGroup.Discrete => ScaleKind.Ordinal,
                _ => ScaleKind.Linear,
            };
        }
        else if (type == "temporal")
        {
            // A temporal color is sequential, shape and dash are discrete, everything else is a time scale (UTC for a UTC unit)
            defaultKind = group switch
            {
                ChannelGroup.Color => ScaleKind.Sequential,
                ChannelGroup.Discrete => ScaleKind.Ordinal,
                _ => utcUnit ? ScaleKind.Utc : ScaleKind.Time,
            };
        }
        else if (group is ChannelGroup.Color or ChannelGroup.Discrete)
        {
            defaultKind = ScaleKind.Ordinal;
        }
        else if (group == ChannelGroup.Position && BandMarks.Contains(markType))
        {
            defaultKind = ScaleKind.Band;
        }
        else
        {
            defaultKind = ScaleKind.Point;
        }

        if (!TryParseKind(specified, group, out var requested, out var requestedTransform, out var timeName) || !Accepts(group, type, requested, binning, timeName))
        {
            return defaultKind;
        }

        transformKind = requestedTransform;
        return requested;
    }

    /// <summary>
    ///     Reads an explicit <c>scale.type</c> as the kind this package draws.
    /// </summary>
    /// <param name="specified">The type name, or null.</param>
    /// <param name="group">The group of the channel (a color channel's <c>linear</c> is a sequential scale).</param>
    /// <param name="kind">Receives the kind.</param>
    /// <param name="transform">Receives the transform: log, pow (for <c>pow</c> and <c>sqrt</c>) or symlog for those types, else linear.</param>
    /// <param name="timeName">Receives whether the name is <c>time</c> or <c>utc</c>, which only a temporal field accepts.</param>
    /// <returns><see langword="false"/> for null and for a type this package does not draw (identity).</returns>
    private static bool TryParseKind(string? specified, ChannelGroup group, out ScaleKind kind, out ScaleTransformKind transform, out bool timeName)
    {
        transform = ScaleTransformKind.Linear;
        timeName = false;
        switch (specified)
        {
            case "time" or "utc":
                // On a color channel a time scale is the sequential scale over the instants, as linear is for numbers
                timeName = true;
                if (group == ChannelGroup.Color)
                {
                    kind = ScaleKind.Sequential;
                }
                else
                {
                    kind = specified == "time" ? ScaleKind.Time : ScaleKind.Utc;
                }

                return true;
            case "linear":
                kind = group == ChannelGroup.Color ? ScaleKind.Sequential : ScaleKind.Linear;
                return true;
            case "log" or "pow" or "sqrt" or "symlog":
                // Vega-Lite's continuous-to-continuous scales; on a color channel they are the sequential scales of d3 with the transform
                kind = group == ChannelGroup.Color ? ScaleKind.Sequential : ScaleKind.Linear;
                transform = specified switch
                {
                    "log" => ScaleTransformKind.Log,
                    "symlog" => ScaleTransformKind.Symlog,
                    _ => ScaleTransformKind.Pow,
                };
                return true;
            case "sequential":
                kind = ScaleKind.Sequential;
                return true;
            case "band":
                kind = ScaleKind.Band;
                return true;
            case "point":
                kind = ScaleKind.Point;
                return true;
            case "ordinal":
                kind = ScaleKind.Ordinal;
                return true;
            case "quantile":
                kind = ScaleKind.Quantile;
                return true;
            case "quantize":
                kind = ScaleKind.Quantize;
                return true;
            case "threshold":
                kind = ScaleKind.Threshold;
                return true;
            case "bin-ordinal":
                kind = ScaleKind.BinOrdinal;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    /// <summary>
    ///     Applies Vega-Lite's <c>channelSupportScaleType</c> and <c>scaleTypeSupportDataType</c> to an explicit scale kind.
    /// </summary>
    /// <param name="group">The group of the channel.</param>
    /// <param name="type">The field type.</param>
    /// <param name="kind">The requested kind.</param>
    /// <param name="binning">Whether the encoding bins the field: the bin-ordinal scale needs bins to place the values in.</param>
    /// <param name="timeName">Whether the explicit type was <c>time</c> or <c>utc</c>: Vega-Lite accepts only those for a temporal field, and only a temporal field accepts them.</param>
    /// <returns><see langword="true"/> when both the channel and the field type accept the kind.</returns>
    /// <remarks>
    ///     The bin-ordinal scale is accepted for a binned quantitative field of a color channel only. Vega-Lite also lets a nominal or
    ///     ordinal field ask for it, but then the field is the text of the bin, which no bin-ordinal scale can place; the default scale is used for that
    ///     (and for a field that is not binned, which has no bins to place values in).
    /// </remarks>
    private static bool Accepts(ChannelGroup group, string type, ScaleKind kind, bool binning, bool timeName = false)
    {
        var discretizing = kind is ScaleKind.Quantile or ScaleKind.Quantize or ScaleKind.Threshold;
        var channelAccepts = group switch
        {
            ChannelGroup.Position => kind is ScaleKind.Linear or ScaleKind.Band or ScaleKind.Point or ScaleKind.Time or ScaleKind.Utc,
            ChannelGroup.Color => kind is ScaleKind.Sequential or ScaleKind.Ordinal or ScaleKind.BinOrdinal || discretizing,
            ChannelGroup.Discrete => kind == ScaleKind.Ordinal || discretizing,
            _ => kind is ScaleKind.Linear or ScaleKind.Band or ScaleKind.Point or ScaleKind.Ordinal or ScaleKind.Time or ScaleKind.Utc || discretizing,
        };

        // A temporal field takes the time and utc types only; a quantitative field the continuous and the discretizing scales, a nominal or ordinal field the discrete ones
        bool typeAccepts;
        if (type == "temporal")
        {
            typeAccepts = timeName;
        }
        else if (timeName)
        {
            typeAccepts = false;
        }
        else
        {
            typeAccepts = type == "quantitative"
                ? kind is ScaleKind.Linear or ScaleKind.Sequential || discretizing || (kind == ScaleKind.BinOrdinal && binning)
                : kind is ScaleKind.Band or ScaleKind.Point or ScaleKind.Ordinal;
        }

        return channelAccepts && typeAccepts;
    }

    /// <summary>
    ///     Tells whether Vega-Lite accepts an explicit <c>scale.type</c> for a channel and a field type; the feature catalog uses it to warn that
    ///     an explicit type is replaced by the default one.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <param name="fieldType">The field type (<c>quantitative</c>, <c>temporal</c>, <c>nominal</c> or <c>ordinal</c>).</param>
    /// <param name="scaleType">The scale type name.</param>
    /// <param name="binned">Whether the encoding bins the field (<c>bin</c> is true or an object, not <c>"binned"</c>).</param>
    /// <returns><see langword="true"/> when the type is one this package draws and both the channel and the field type accept it; <see langword="false"/> for any other type, whether Vega-Lite rejects it or this package does not draw it yet.</returns>
    public static bool AcceptsScaleType(string channel, string fieldType, string scaleType, bool binned = false)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(fieldType);
        ArgumentNullException.ThrowIfNull(scaleType);
        var group = GroupOf(channel);
        return TryParseKind(scaleType, group, out var kind, out _, out var timeName) && Accepts(group, fieldType, kind, binned && EncodingPlan.CanBin(channel), timeName);
    }

    /// <summary>
    ///     Builds a continuous or discretizing scale: linear, sequential, diverging, quantize, quantile or threshold.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The domain is the explicit <c>scale.domain</c> or the extent of the valid values (quantile: all of them, sorted); then, as in Vega's
    ///         <c>configureDomain</c>, <c>zero</c> pulls the end values to zero, <c>domainMin</c> and <c>domainMax</c> replace the first and the
    ///         last entry, and <c>domainMid</c> (colors) inserts the middle value, which makes a color scale diverging. The three entries of a
    ///         diverging scale are the most a color scale takes: a longer explicit domain is a specification error.
    ///     </para>
    /// </remarks>
    /// <param name="input">The channel facts.</param>
    /// <param name="kind">The kind: linear, sequential, quantize, quantile or threshold.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The scale, or null when the channel cannot have one (a size on a mark without sizes).</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the domain, the range or the color scheme is wrong; carries the location.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when a limit is exceeded; carries the location.</exception>
    private static ResolvedScale? BuildContinuous(CommonInputs input, ScaleKind kind, Context context)
    {
        var group = GroupOf(input.Channel);
        var scaleDef = input.ScaleDef;
        var time = input.Time;
        var isTimeKind = kind is ScaleKind.Time or ScaleKind.Utc;
        var continuousToContinuous = kind is ScaleKind.Linear or ScaleKind.Sequential or ScaleKind.Time or ScaleKind.Utc;

        // The domain: explicit, or the extent of the valid values; an explicit domain of more than two numbers is piecewise
        double start;
        double end;
        double[]? entries = null;
        BinSpec? binSpec = null;
        var domainNode = scaleDef?["domain"] is { IsArray: true } array ? array : null;
        if (kind == ScaleKind.Quantile)
        {
            entries = QuantileDomain(input, domainNode, context);
            (start, end) = entries.Length > 0 ? (entries[0], entries[^1]) : (0d, 1d);
        }
        else if (domainNode is not null)
        {
            CheckSpecArray(domainNode, input, context, "domain");
            var minimum = kind == ScaleKind.Threshold ? 1 : 2;
            var explicitDomain = time is not null ? ReadTemporalDomain(domainNode, time, context, minimum) : ReadNumericDomain(domainNode, context, minimum);
            (start, end) = (explicitDomain[0], explicitDomain[^1]);
            entries = explicitDomain.Length > 2 || kind == ScaleKind.Threshold ? explicitDomain : null;
        }
        else if (time is { IsTemporal: true } && input.Datum is not null)
        {
            // A temporal datum is one instant (a text is a date text)
            var instant = ReadTemporalDatum(input.Datum, time);
            (start, end) = instant is { } at ? (at, at) : (0d, 1d);
        }
        else
        {
            (start, end) = LinearExtent(input, context, out binSpec, out var found);
            if (!found && input.TransformKind == ScaleTransformKind.Log)
            {
                // Without a valid value a log scale gets one decade (d3's default domain 0 to 1 contains zero, which a log scale cannot map)
                (start, end) = (1, 10);
            }

            if (kind == ScaleKind.Threshold)
            {
                // A threshold scale without a domain cuts the extent of the data in three
                entries = [start, end];
            }
        }

        // The zero rule pulls the end values to zero; the quantile and threshold scales have none (Vega-Lite's scaleTypeSupportProperty),
        // and neither has a log scale, whose domain cannot reach zero, or a time scale, for which zero is not meaningful
        var zero = kind is not (ScaleKind.Quantile or ScaleKind.Threshold)
            && !isTimeKind
            && input.TransformKind != ScaleTransformKind.Log
            && (scaleDef?["zero"]?.AsBool() ?? ZeroRule(input, domainNode, kind, context));
        if (zero)
        {
            if (start > 0)
            {
                start = 0;
            }

            if (end < 0)
            {
                end = 0;
            }

            if (entries is not null)
            {
                entries[0] = start;
                entries[^1] = end;
            }
        }

        // domainMin, domainMax and domainMid belong to the continuous-to-continuous scales (domainMid to colors only)
        if (continuousToContinuous)
        {
            if (ReadDomainBound(scaleDef?["domainMin"], time) is { } domainMin)
            {
                start = domainMin;
                if (entries is not null)
                {
                    entries[0] = start;
                }
            }

            if (ReadDomainBound(scaleDef?["domainMax"], time) is { } domainMax)
            {
                end = domainMax;
                if (entries is not null)
                {
                    entries[^1] = end;
                }
            }

            if (group == ChannelGroup.Color && ReadDomainBound(scaleDef?["domainMid"], time) is { } mid)
            {
                // Vega puts the middle value before the first entry, after the last, or between the ends (the last entry is moved up for two entries)
                var all = (entries ?? [start, end]).ToList();
                var position = all.Count - 1;
                if (mid > all[^1])
                {
                    position = all.Count;
                }
                else if (mid < all[0])
                {
                    position = 0;
                }

                all.Insert(position, mid);
                entries = [.. all];
                (start, end) = (entries[0], entries[^1]);
            }
        }

        // A color scale is sequential with two domain entries and diverging with three; a longer domain would be piecewise
        if (group == ChannelGroup.Color && kind == ScaleKind.Sequential)
        {
            if (entries is { Length: > 3 })
            {
                throw new VegaLiteSpecException(
                    "A color scale takes a domain of two entries, or of three with a middle value (a diverging scale); a longer domain is not supported.",
                    (domainNode ?? scaleDef)?.GetLocation(context.SourceName) ?? input.Location);
            }

            if (entries is { Length: 3 })
            {
                kind = ScaleKind.Diverging;
            }
        }

        // The output values, which also decide whether the channel has a scale at all
        var transform = MakeTransform(input);
        if (transform.Kind == ScaleTransformKind.Log)
        {
            CheckLogDomain(entries ?? [start, end], domainNode, input, context);
        }
        var domainSize = kind == ScaleKind.Threshold ? entries!.Length : 0;
        if (ResolveOutputs(input, kind, domainSize, context) is not { } outputs)
        {
            return null;
        }

        var isPosition = group == ChannelGroup.Position;
        var reverse = ReverseOf(input, kind);
        var supportsRangeLimits = kind is ScaleKind.Linear or ScaleKind.Time or ScaleKind.Utc;
        var timeNice = isTimeKind ? TimeNiceRequest(scaleDef) : null;
        double? niceCount;
        if (isTimeKind)
        {
            niceCount = timeNice is { Interval: null } counted ? counted.Count : null;
        }
        else
        {
            niceCount = NiceRule(input, kind, domainNode is not null, isPosition);
        }

        return new ResolvedScale
        {
            Channel = input.Channel,
            Kind = kind,
            FieldPath = input.FieldPath,
            Field2Path = input.Plan?.Field2Path,
            Title = input.Plan?.Title,
            Bin = input.Plan?.Bin ?? BinKind.None,
            BinSpec = binSpec,
            Stack = input.Plan is { IsStacked: true } ? context.Plan.Stack?.Offset : null,
            DataType = input.Type,
            Location = input.Location,
            DomainStart = start,
            DomainEnd = end,
            Domain = entries,
            NiceCount = niceCount,
            NiceInterval = timeNice is { Interval: not null } ? timeNice : null,
            PaddingPixels = isPosition ? ContinuousPadding(input, context) : 0,
            Clamp = continuousToContinuous && (scaleDef?["clamp"]?.AsBool() ?? false),
            Transform = transform,
            Round = supportsRangeLimits && (scaleDef?["round"]?.AsBool() ?? false),
            Reverse = reverse,
            ExplicitRange = outputs.ExplicitRange,
            RangeMin = supportsRangeLimits ? scaleDef?["rangeMin"]?.AsNumber() : null,
            RangeMax = supportsRangeLimits ? scaleDef?["rangeMax"]?.AsNumber() : null,
            ValueRange = outputs.ValueRange,
            NumberRange = outputs.Numbers,
            ColorRange = outputs.Colors,
            ShapeRange = outputs.Shapes,
            DashRange = outputs.Dashes,
            Interpolator = outputs.Interpolator,
            Calendar = time?.Calendar,
            TimeUnit = time?.Unit,
            IsTemporalField = time is not null,
            HasBandEnd = input.Plan?.HasBandEnd ?? false,
        };
    }

    /// <summary>
    ///     Creates the transform of a continuous scale from its type and its <c>base</c>, <c>exponent</c> or <c>constant</c>.
    /// </summary>
    /// <remarks>
    ///     As in Vega-Lite, <c>base</c> (default 10) belongs to a log scale, <c>exponent</c> (default 1) to a pow scale, <c>constant</c> (default 1) to a
    ///     symlog scale, and a sqrt scale is a pow scale with the exponent 0.5 whatever <c>exponent</c> says; the others ignore them.
    /// </remarks>
    /// <param name="input">The channel facts.</param>
    /// <returns>The transform; the identity for a linear scale.</returns>
    private static ScaleTransform MakeTransform(CommonInputs input)
    {
        var scaleDef = input.ScaleDef;
        switch (input.TransformKind)
        {
            case ScaleTransformKind.Log:
                return ScaleTransform.Log(scaleDef?["base"]?.AsNumber() is { } logBase and > 0 && logBase.CompareTo(1) != 0 ? logBase : 10);
            case ScaleTransformKind.Symlog:
                return ScaleTransform.Symlog(scaleDef?["constant"]?.AsNumber() is { } constant and > 0 ? constant : 1);
            case ScaleTransformKind.Pow:
                var exponent = scaleDef?["exponent"]?.AsNumber();
                if (scaleDef?["type"]?.AsString() == "sqrt")
                {
                    return ScaleTransform.Pow(0.5);
                }

                return ScaleTransform.Pow(exponent is { } e && double.IsFinite(e) ? e : 1);
            default:
                return ScaleTransform.Linear;
        }
    }

    /// <summary>
    ///     Checks that the domain of a log scale can be mapped: every entry is finite, none is zero and all are on the same side of zero.
    /// </summary>
    /// <remarks>
    ///     Vega only warns about a log domain that includes zero and then draws nothing (every position is NaN); here the domain is refused with
    ///     a located error that says what to do. A domain given in the specification is a <see cref="VegaLiteSpecException"/> at the domain; one
    ///     computed from the data is a <see cref="VegaLiteRenderException"/> at the channel.
    /// </remarks>
    /// <param name="entries">The domain entries.</param>
    /// <param name="domainNode">The explicit domain array, or null when the domain comes from the data.</param>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the explicit domain is not valid for a log scale.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the domain of the data is not valid for a log scale.</exception>
    private static void CheckLogDomain(double[] entries, SpecNode? domainNode, CommonInputs input, Context context)
    {
        if (entries.All(entry => Ticks.IsLogDomain(entries[0], entry)))
        {
            return;
        }

        var shown = string.Join(", ", entries.Take(4).Select(JsNumber.ToString)) + (entries.Length > 4 ? ", ..." : string.Empty);
        var message = $"A log scale cannot map a domain that includes zero or crosses it ([{shown}]); the domain of the {input.Channel} scale must lie wholly above or wholly below zero. Use a symlog scale, or leave the zero and negative values out of the data.";
        if (domainNode is not null)
        {
            throw new VegaLiteSpecException(message, domainNode.GetLocation(context.SourceName));
        }

        throw new VegaLiteRenderException(message, input.Location);
    }

    /// <summary>
    ///     Builds a band, point, ordinal or bin-ordinal scale.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="kind">The kind of scale.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The scale, or null when the channel cannot have one (a size on a mark without sizes).</returns>
    private static ResolvedScale? BuildDiscrete(CommonInputs input, ScaleKind kind, Context context)
    {
        var isPosition = input.Channel is "x" or "y";
        if (kind == ScaleKind.BinOrdinal)
        {
            return BuildBinOrdinal(input, context);
        }

        var domain = DiscreteDomain(input, context);

        // The output values (which also decide whether the channel has a scale at all)
        if (ResolveOutputs(input, kind, domain.Count, context) is not { } outputs)
        {
            return null;
        }

        var (inner, outer) = kind == ScaleKind.Ordinal ? (0.0, 0.0) : Paddings(input, kind, context, isPosition);

        // A discrete position without an explicit range is sized by its step, unless the view size is fixed
        double? step = null;
        if (isPosition && kind != ScaleKind.Ordinal && outputs.ExplicitRange is null)
        {
            step = input.Channel == "x" ? context.WidthStep : context.HeightStep;
        }

        return new ResolvedScale
        {
            Channel = input.Channel,
            Kind = kind,
            FieldPath = input.FieldPath,
            Field2Path = input.Plan?.Field2Path,
            Title = input.Plan?.Title,
            Bin = input.Plan?.Bin ?? BinKind.None,
            DataType = input.Type,
            Location = input.Location,
            DiscreteDomain = domain,
            PaddingInner = inner,
            PaddingOuter = outer,
            Align = Math.Clamp(input.ScaleDef?["align"]?.AsNumber() ?? 0.5, 0, 1),
            Round = input.ScaleDef?["round"]?.AsBool() ?? false,
            Reverse = input.ScaleDef?["reverse"]?.AsBool() ?? false,
            Step = step,
            ExplicitRange = outputs.ExplicitRange,
            RangeMin = kind is ScaleKind.Band or ScaleKind.Point ? input.ScaleDef?["rangeMin"]?.AsNumber() : null,
            RangeMax = kind is ScaleKind.Band or ScaleKind.Point ? input.ScaleDef?["rangeMax"]?.AsNumber() : null,
            ValueRange = outputs.ValueRange,
            NumberRange = outputs.Numbers,
            ColorRange = outputs.Colors,
            ShapeRange = outputs.Shapes,
            DashRange = outputs.Dashes,
            Calendar = input.Time?.Calendar,
            TimeUnit = input.Time?.Unit,
            IsTemporalField = input.Time is not null,
        };
    }

    /// <summary>
    ///     Builds a bin-ordinal scale: the colors of the bins of a binned quantitative field (Vega's <c>scaleBinOrdinal</c>).
    /// </summary>
    /// <remarks>
    ///     The domain is the edges of the bins the encoding computed (Vega-Lite hands the scale the <c>bins</c> of the bin transform; the scale builds
    ///     <c>sequence(start, stop + step / 2, step)</c> from them), so there is one more edge than there are bins and one color per bin. A field without a
    ///     finite value has no bins, so the scale has no edge and no color, and no mark gets a color from it. An explicit <c>scale.domain</c> is not used, as in
    ///     Vega-Lite, where the domain comes from the bins.
    /// </remarks>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The scale.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the bins are more than the scale domain limit; carries the location.</exception>
    private static ResolvedScale? BuildBinOrdinal(CommonInputs input, Context context)
    {
        var edges = new List<double>();
        BinSpec? binSpec = null;
        if (input.Plan is { BinPlan: { } bins } && context.Data.Bins.TryGetValue(bins.StartName, out var spec))
        {
            binSpec = spec;
            var step = spec.Step;
            var limit = spec.Stop + (step / 2);
            for (var edge = spec.Start; edge < limit; edge = spec.Start + (edges.Count * step))
            {
                if (edges.Count % CancellationInterval == 0)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                }

                edges.Add(edge);
                // One more edge than there are bins; the bins were bounded by the limit already, so this only guards the arithmetic of the loop
                if (edges.Count > context.Limits.MaxScaleDomainSize + 1)
                {
                    throw new VegaLiteRenderException(
                        $"The {input.Channel} scale has more than {context.Limits.MaxScaleDomainSize} bins, the limit (MaxScaleDomainSize).",
                        input.Location);
                }
            }
        }

        if (ResolveOutputs(input, ScaleKind.BinOrdinal, edges.Count, context) is not { } outputs)
        {
            return null;
        }

        return new ResolvedScale
        {
            Channel = input.Channel,
            Kind = ScaleKind.BinOrdinal,
            FieldPath = input.FieldPath,
            Title = input.Plan?.Title,
            Bin = input.Plan?.Bin ?? BinKind.None,
            BinSpec = binSpec,
            DataType = input.Type,
            Location = input.Location,
            DomainStart = edges.Count > 0 ? edges[0] : 0,
            DomainEnd = edges.Count > 0 ? edges[^1] : 0,
            Domain = [.. edges],
            Reverse = input.ScaleDef?["reverse"]?.AsBool() ?? false,
            ExplicitRange = outputs.ExplicitRange,
            ValueRange = outputs.ValueRange,
            NumberRange = outputs.Numbers,
            ColorRange = outputs.Colors,
            ShapeRange = outputs.Shapes,
            DashRange = outputs.Dashes,
        };
    }

    /// <summary>
    ///     Applies Vega-Lite's <c>zero</c> rule.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="domainNode">The explicit domain array, or null.</param>
    /// <param name="kind">The kind of scale (a discretizing size scale does not start at zero).</param>
    /// <param name="context">The view facts.</param>
    /// <returns>Whether the domain is extended to include zero.</returns>
    private static bool ZeroRule(CommonInputs input, SpecNode? domainNode, ScaleKind kind, Context context)
    {
        // An explicit domain is not augmented, except that one that already spans zero keeps zero
        if (domainNode is not null)
        {
            return domainNode.Items.Count > 0
                && domainNode.Items[0] is { IsNumber: true } first && first.NumberValue <= 0
                && domainNode.Items[^1] is { IsNumber: true } last && last.NumberValue >= 0;
        }

        // A quantitative size starts at zero, unless its scale cuts the domain into intervals
        if (input.Channel == "size")
        {
            return kind != ScaleKind.Quantize;
        }

        // Only unbinned x and y scales are subject to the position rules; every other channel does not include zero
        if (input.Channel is not ("x" or "y") || IsEnabled(input.Definition["bin"]))
        {
            return false;
        }

        // The dimension axis of a bar, area, line or trail does not start at zero by rule
        var type = context.MarkType;
        if (type is "bar" or "area" or "line" or "trail"
            && ((context.Orient == "horizontal" && input.Channel == "y") || (context.Orient == "vertical" && input.Channel == "x")))
        {
            return false;
        }

        // The measure axis of a bar or area always includes zero (unless it is a range); everything else follows the configuration
        var hasSecondary = context.Encoding?[input.Channel + "2"] is not null;
        if (type is "bar" or "area" && !hasSecondary)
        {
            return true;
        }

        return context.Config.GetBool("scale.zero") ?? true;
    }

    /// <summary>
    ///     Decides <c>nice</c> for a continuous scale.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="kind">The kind of scale (the quantile and threshold scales have no <c>nice</c>).</param>
    /// <param name="hasDomain">Whether the specification gives the domain.</param>
    /// <param name="isPosition">Whether the channel is x or y.</param>
    /// <returns>The tick count to nice with, or null for no nice.</returns>
    private static double? NiceRule(CommonInputs input, ScaleKind kind, bool hasDomain, bool isPosition)
    {
        if (kind is not (ScaleKind.Linear or ScaleKind.Sequential or ScaleKind.Diverging or ScaleKind.Quantize) || input.Time is not null)
        {
            // The ms of a temporal color scale are not "nice" numbers; time scales have their own rule (TimeNiceRequest)
            return null;
        }
        // Explicit nice: true uses the d3 default count of 10, a number is the count, false switches nice off
        var nice = input.ScaleDef?["nice"];
        if (nice is { IsBoolean: true })
        {
            return nice.BoolValue ? 10 : null;
        }

        if (nice is { IsNumber: true })
        {
            return nice.NumberValue > 0 ? nice.NumberValue : null;
        }

        // By default x and y get nice unless a domain is given; the bins of a binned field already end on round values, so they get none
        return isPosition && !hasDomain && input.Plan is not { Bin: not BinKind.None } ? 10 : null;
    }

    /// <summary>
    ///     Decides the pixel padding of a continuous position scale.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The padding in pixels.</returns>
    private static double ContinuousPadding(CommonInputs input, Context context)
    {
        if (input.ScaleDef?["padding"]?.AsNumber() is { } padding)
        {
            return Math.Max(0, padding);
        }

        if (context.Config.GetNumber("scale.continuousPadding") is { } configured)
        {
            return Math.Max(0, configured);
        }

        // A bar on the axis that carries its width gets room for half the bar, unless its field is binned or has a time unit (the bar then spans the bin or the unit)
        var carriesBandWidth = (context.Orient == "vertical" && input.Channel == "x") || (context.Orient == "horizontal" && input.Channel == "y");
        if (context.MarkType == "bar" && carriesBandWidth && !IsEnabled(input.Definition["bin"]) && input.Definition["timeUnit"] is not { IsNull: false })
        {
            return Math.Max(0, context.Config.GetNumber("bar.continuousBandSize") ?? 5);
        }

        return 0;
    }

    /// <summary>
    ///     Decides the inner and outer padding of a band or point scale.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="kind">Band or point.</param>
    /// <param name="context">The view facts.</param>
    /// <param name="isPosition">Whether the channel is x or y.</param>
    /// <returns>The inner and outer padding.</returns>
    private static (double Inner, double Outer) Paddings(CommonInputs input, ScaleKind kind, Context context, bool isPosition)
    {
        var scaleDef = input.ScaleDef;
        var padding = scaleDef?["padding"]?.AsNumber();
        double inner;
        double outer;
        if (kind == ScaleKind.Point)
        {
            // A point scale only has outer padding: the scale's, or half a step by configuration on x and y
            inner = 1;
            outer = scaleDef?["paddingOuter"]?.AsNumber() ?? padding ?? (isPosition ? context.Config.GetNumber("scale.pointPadding") ?? 0.5 : 0);
            return (inner, Math.Max(0, outer));
        }

        if (padding is { } p)
        {
            // "padding" sets both paddings; the specific properties refine it
            inner = Math.Min(1, p);
            outer = p;
            inner = scaleDef?["paddingInner"]?.AsNumber() is { } explicitInner ? explicitInner : inner;
            outer = scaleDef?["paddingOuter"]?.AsNumber() is { } explicitOuter ? explicitOuter : outer;
        }
        else if (isPosition)
        {
            // Defaults by mark; the outer padding is half the inner padding so that the size is a whole number of steps
            var markDefault = context.MarkType switch
            {
                "bar" => context.Config.GetNumber("scale.barBandPaddingInner") ?? 0.1,
                "tick" => context.Config.GetNumber("scale.tickBandPaddingInner") ?? 0.25,
                _ => context.Config.GetNumber("scale.rectBandPaddingInner") ?? 0,
            };
            inner = scaleDef?["paddingInner"]?.AsNumber() ?? context.Config.GetNumber("scale.bandPaddingInner") ?? markDefault;
            outer = scaleDef?["paddingOuter"]?.AsNumber() ?? context.Config.GetNumber("scale.bandPaddingOuter") ?? (inner / 2);
        }
        else
        {
            inner = scaleDef?["paddingInner"]?.AsNumber() ?? 0;
            outer = scaleDef?["paddingOuter"]?.AsNumber() ?? 0;
        }

        return (Math.Clamp(inner, 0, 1), Math.Max(0, outer));
    }

    /// <summary>
    ///     Gets the numeric range of a size scale for the mark.
    /// </summary>
    /// <param name="context">The view facts.</param>
    /// <returns>The minimum and maximum size; null when the mark has no size scale.</returns>
    private static (double Min, double Max)? SizeRange(Context context)
    {
        var scale = context.Config;
        switch (context.MarkType)
        {
            case "bar" or "tick":
                return (scale.GetNumber("scale.minBandSize") ?? 2, scale.GetNumber("scale.maxBandSize") ?? (context.MinStep - 1));
            case "line" or "trail" or "rule":
                return (scale.GetNumber("scale.minStrokeWidth") ?? 1, scale.GetNumber("scale.maxStrokeWidth") ?? 4);
            case "text":
                return (scale.GetNumber("scale.minFontSize") ?? 8, scale.GetNumber("scale.maxFontSize") ?? 40);
            case "point" or "square" or "circle":
                var maximum = scale.GetNumber("scale.maxSize") is { } configured and > 0
                    ? configured
                    : Math.Pow(MaxSizeRangeStepRatio * context.MinStep, 2);
                return (scale.GetNumber("scale.minSize") ?? 4, maximum);
            default:
                return null;
        }
    }

    /// <summary>
    ///     Reads an explicit linear domain: an array of numbers, all of which are kept (more than two make a piecewise scale).
    /// </summary>
    /// <param name="domain">The <c>scale.domain</c> array.</param>
    /// <param name="context">The view facts (for the source name).</param>
    /// <param name="minimum">The fewest entries the scale needs: two, or one for the thresholds of a threshold scale.</param>
    /// <returns>The entries.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the array has too few entries or a non-numeric entry.</exception>
    private static double[] ReadNumericDomain(SpecNode domain, Context context, int minimum = 2)
    {
        if (domain.Items.Count < minimum)
        {
            throw new VegaLiteSpecException(
                minimum == 1
                    ? "The scale domain of a threshold scale needs at least one number."
                    : "The scale domain of a quantitative field needs at least two numbers.",
                domain.GetLocation(context.SourceName));
        }

        var entries = new double[domain.Items.Count];
        for (var i = 0; i < entries.Length; i++)
        {
            if (!domain.Items[i].IsNumber)
            {
                throw new VegaLiteSpecException("The scale domain of a quantitative field must contain numbers.", domain.Items[i].GetLocation(context.SourceName));
            }

            entries[i] = domain.Items[i].NumberValue;
        }

        return entries;
    }

    /// <summary>
    ///     Builds the domain of a discrete scale: explicit, or the distinct values in the requested order.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The categories.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the domain exceeds the limit.</exception>
    private static List<DataValue> DiscreteDomain(CommonInputs input, Context context)
    {
        // An explicit domain is used as it is
        var domain = new List<DataValue>();
        var seen = new HashSet<DataValue>();
        if (input.ScaleDef?["domain"] is { IsArray: true } explicitDomain)
        {
            CheckSpecArray(explicitDomain, input, context, "domain");
            foreach (var item in explicitDomain.Items)
            {
                var value = ToDataValue(item, input.Time);
                if (seen.Add(value))
                {
                    domain.Add(value);
                }
            }

            return CheckDomainSize(domain, input, context);
        }

        // Otherwise the distinct values in order of first appearance...
        var table = context.Data.Main;
        if (input.FieldPath is not null)
        {
            var field = table.FindField(input.FieldPath);
            var rows = table.RowCount;
            for (var row = 0; row < rows; row++)
            {
                if (row % CancellationInterval == 0)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                }

                var value = field >= 0 ? table.GetValue(row, field) : default;
                if (seen.Add(value))
                {
                    domain.Add(value);
                    if (domain.Count > context.Limits.MaxScaleDomainSize)
                    {
                        CheckDomainSize(domain, input, context);
                    }
                }
            }
        }
        else
        {
            domain.Add(ToDataValue(input.Datum!));
        }

        CheckDomainSize(domain, input, context);

        // ... sorted as the field's sort asks (ascending by default); a stable sort keeps data order for ties
        return SortDomain(domain, input, context);
    }

    /// <summary>
    ///     Checks the size of a discrete domain against the limit.
    /// </summary>
    /// <param name="domain">The domain.</param>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The domain.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the domain is too large.</exception>
    private static List<DataValue> CheckDomainSize(List<DataValue> domain, CommonInputs input, Context context)
    {
        if (domain.Count > context.Limits.MaxScaleDomainSize)
        {
            throw new VegaLiteRenderException(
                $"The {input.Channel} scale has more than {context.Limits.MaxScaleDomainSize} categories, the limit (MaxScaleDomainSize).",
                input.Location);
        }

        return domain;
    }

    /// <summary>
    ///     Converts a JSON node to a data value.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="time">The time facts of the field: for a temporal field or a field with a time unit a date or a date-time object is the date it names, as the field's values are dates; null otherwise.</param>
    /// <returns>The value; null for anything but a string, number or boolean (or a date when the field is temporal), and for a date out of range.</returns>
    private static DataValue ToDataValue(SpecNode node, TimeFacts? time = null)
    {
        if (time is not null && node.Kind is SpecNodeKind.Number or SpecNodeKind.String or SpecNodeKind.Object)
        {
            var ms = ToMilliseconds(node, time);
            return double.IsNaN(ms) ? DataValue.Null : DataValue.FromDate(ms);
        }

        return node.Kind switch
        {
            SpecNodeKind.String => DataValue.FromString(node.StringValue),
            SpecNodeKind.Number => DataValue.FromNumber(node.NumberValue),
            SpecNodeKind.Boolean => DataValue.FromBoolean(node.BoolValue),
            _ => DataValue.Null,
        };
    }

    /// <summary>The facts about the view that every channel needs.</summary>
    private sealed class Context
    {
        /// <summary>Gets the plan of the encoding.</summary>
        public required EncodingPlan Plan { get; init; }

        /// <summary>Gets the tables of the implicit pipeline.</summary>
        public required EncodingData Data { get; init; }

        /// <summary>Gets the configuration.</summary>
        public required ResolvedConfig Config { get; init; }

        /// <summary>Gets the mark type.</summary>
        public required string MarkType { get; init; }

        /// <summary>Gets the inferred orientation of the mark.</summary>
        public string? Orient { get; init; }

        /// <summary>Gets the encoding object, or null.</summary>
        public SpecNode? Encoding { get; init; }

        /// <summary>Gets the source name for locations.</summary>
        public string? SourceName { get; init; }

        /// <summary>Gets the limits.</summary>
        public required VegaLiteLimits Limits { get; init; }

        /// <summary>Gets the cancellation token.</summary>
        public CancellationToken CancellationToken { get; init; }

        /// <summary>Gets the step that sizes a discrete x scale, or null.</summary>
        public double? WidthStep { get; init; }

        /// <summary>Gets the step that sizes a discrete y scale, or null.</summary>
        public double? HeightStep { get; init; }

        /// <summary>Gets the smaller of the two steps, which limits the size of marks.</summary>
        public double MinStep { get; init; } = DefaultStep;
    }

    /// <summary>The facts about one channel that the builders share.</summary>
    /// <param name="Channel">The channel name.</param>
    /// <param name="Type">The field type.</param>
    /// <param name="FieldPath">The field path, or null for a datum.</param>
    /// <param name="Datum">The datum node, or null.</param>
    /// <param name="Definition">The channel definition.</param>
    /// <param name="ScaleDef">The scale object, or null.</param>
    /// <param name="Location">The location of the channel definition.</param>
    /// <param name="Plan">The plan of the channel definition, or null.</param>
    /// <param name="TransformKind">The transform of a continuous scale: <see cref="ScaleTransformKind.Linear"/> unless the scale type is <c>log</c>, <c>pow</c>, <c>sqrt</c> or <c>symlog</c> and the channel accepts it.</param>
    /// <param name="Time">The calendar, the time unit and the type facts of a temporal field or a field with a time unit, or null.</param>
    private sealed record CommonInputs(
        string Channel,
        string Type,
        string? FieldPath,
        SpecNode? Datum,
        SpecNode Definition,
        SpecNode? ScaleDef,
        VegaLiteLocation Location,
        ChannelPlan? Plan,
        ScaleTransformKind TransformKind,
        TimeFacts? Time = null);
}
