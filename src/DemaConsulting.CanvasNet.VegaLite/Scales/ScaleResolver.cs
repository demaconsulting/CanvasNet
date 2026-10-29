// Portions derived from Vega-Lite and Vega (BSD-3-Clause), d3-scale (ISC); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

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
    /// <summary>Gets the channel: <c>x</c>, <c>y</c>, <c>color</c>, <c>fill</c>, <c>stroke</c>, <c>size</c> or <c>opacity</c>.</summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>Gets the kind of scale.</summary>
    public ScaleKind Kind { get; init; }

    /// <summary>Gets the field path of the channel, or null when the channel is driven by a <c>datum</c>.</summary>
    public string? FieldPath { get; init; }

    /// <summary>Gets the field type: <c>quantitative</c>, <c>nominal</c> or <c>ordinal</c>.</summary>
    public string DataType { get; init; } = string.Empty;

    /// <summary>Gets the location of the channel definition in the specification.</summary>
    public VegaLiteLocation Location { get; init; }

    /// <summary>Gets the start of the domain of a linear scale after the zero rule, before pixel padding and <c>nice</c>.</summary>
    public double DomainStart { get; init; }

    /// <summary>Gets the end of the domain of a linear scale after the zero rule, before pixel padding and <c>nice</c> (the last entry of <see cref="Domain"/>).</summary>
    public double DomainEnd { get; init; }

    /// <summary>
    ///     Gets the complete explicit domain of a linear scale after the zero rule (at least two numbers), or null when the
    ///     domain is the two ends <see cref="DomainStart"/> and <see cref="DomainEnd"/>; a domain of more than two numbers
    ///     is piecewise (see <see cref="ScaleModel.CreateLinear(double[], double[], bool, bool)"/>).
    /// </summary>
    public IReadOnlyList<double>? Domain { get; init; }

    /// <summary>Gets the tick count <c>nice</c> uses (10 for <c>nice: true</c>), or null when the domain is not extended to round values.</summary>
    public double? NiceCount { get; init; }

    /// <summary>Gets the padding of a linear position scale in pixels (the domain is widened so the extreme values keep this distance from the edge).</summary>
    public double PaddingPixels { get; init; }

    /// <summary>Gets a value indicating whether a linear scale clamps values to its domain.</summary>
    public bool Clamp { get; init; }

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

    /// <summary>Gets the numeric range of a size or opacity scale (start and end).</summary>
    public (double Start, double End)? ValueRange { get; init; }

    /// <summary>Gets the colors of an ordinal color scale.</summary>
    public IReadOnlyList<string> ColorRange { get; init; } = [];

    /// <summary>Gets a value indicating whether the colors are the <c>tableau10</c> fallback because the requested scheme is not available.</summary>
    public bool ColorFallback { get; init; }

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
    ///         replaces them (the keywords <c>width</c> and <c>height</c> stand for <paramref name="extent"/>); a
    ///         reversed scale swaps the two ends. Size and opacity scales use their own ranges and ignore the extent.
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
        var range = ResolveRange(extent);
        switch (Kind)
        {
            case ScaleKind.Linear:
                // Vega applies padding, zero and nice to the first and the last entry of a piecewise domain and leaves the others as they are
                double[] domain = Domain is { Count: > 2 } complete ? [.. complete] : [DomainStart, DomainEnd];
                var start = domain[0];
                var end = domain[^1];

                // Pixel padding widens the domain around its middle
                var span = Math.Abs(range[^1] - range[0]);
                if (PaddingPixels > 0 && start.CompareTo(end) != 0 && span - (2 * PaddingPixels) > 0)
                {
                    var factor = span / (span - (2 * PaddingPixels));
                    var middle = (start + end) / 2;
                    start = middle + ((start - middle) * factor);
                    end = middle + ((end - middle) * factor);
                }

                // Then the domain is extended to round values, if that gives a usable domain
                if (NiceCount is { } count)
                {
                    var (niceStart, niceEnd) = Ticks.Nice(start, end, count);
                    if (double.IsFinite(niceStart) && double.IsFinite(niceEnd))
                    {
                        start = niceStart;
                        end = niceEnd;
                    }
                }

                domain[0] = start;
                domain[^1] = end;
                return ScaleModel.CreateLinear(domain, range, Clamp, Round);

            // A band or point scale takes the first two entries of its range, as d3's scaleBand does
            case ScaleKind.Band:
                return ScaleModel.CreateBand(DiscreteDomain, range[0], range[1], PaddingInner, PaddingOuter, Align, Round);
            case ScaleKind.Point:
                return ScaleModel.CreatePoint(DiscreteDomain, range[0], range[1], PaddingOuter, Align, Round);
            default:
                return ScaleModel.CreateOrdinal(DiscreteDomain, ColorRange);
        }
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
        else if (IsPosition)
        {
            // The origin of a view is its top left corner, so a continuous y scale runs upward
            range = Channel == "y" && Kind == ScaleKind.Linear ? [extent, 0.0] : [0.0, extent];
        }
        else if (ValueRange is { } values)
        {
            range = [values.Start, values.End];
        }
        else
        {
            range = [0.0, 1.0];
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
///     (quantitative color, temporal fields).
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
    internal ResolvedScales(string markType, string? orient, Dictionary<string, ResolvedScale> scales)
    {
        MarkType = markType;
        Orient = orient;
        _scales = scales;
    }

    /// <summary>Gets the mark type of the view (<c>bar</c>, <c>point</c> and so on); empty when the view has no mark.</summary>
    public string MarkType { get; }

    /// <summary>Gets the orientation of the mark as Vega-Lite infers it (<c>horizontal</c> or <c>vertical</c>), or null when the mark has none.</summary>
    public string? Orient { get; }

    /// <summary>Gets the channels that have a scale, in the order x, y, color, fill, stroke, size, opacity.</summary>
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
///             <b>Kind.</b> A quantitative field gets a linear scale. A nominal or ordinal field on <c>x</c> or
///             <c>y</c> gets a band scale for the marks <c>bar</c>, <c>rect</c>, <c>rule</c> and <c>tick</c> (they must
///             fit into a band) and a point scale for the others; on <c>color</c>, <c>fill</c> and <c>stroke</c> an ordinal
///             scale; on <c>size</c> and <c>opacity</c> a point scale. A quantitative color is not drawn here (no scale),
///             a temporal field has no scale. An explicit <c>scale.type</c> is used when the channel and the field
///             type accept it (linear, band, point), otherwise the default applies.
///         </description></item>
///         <item><description>
///             <b>Domain.</b> A linear domain is the extent of the valid values (numbers, booleans as 1/0, numeric
///             strings; null and NaN are ignored; an empty extent is [0, 1]); then <c>zero</c> (true for a quantitative
///             size; for x and y true for <c>bar</c> and <c>area</c> marks and otherwise the configured default true,
///             except on the discrete axis of a bar; with a custom domain only when it already spans zero) pulls the end
///             values to zero. A discrete domain is the distinct values in order of first appearance, sorted ascending
///             by default (<c>sort</c> may be ascending, descending, null for data order, or an array); an explicit
///             <c>scale.domain</c> is used as given.
///         </description></item>
///         <item><description>
///             <b>Padding and nice.</b> A band scale has inner padding from the configuration by mark (bar 0.1, tick
///             0.25, others 0) and outer padding half of it; a point scale 0.5; <c>scale.padding</c> sets both. A
///             continuous x or y scale gets <c>nice</c> with a count of 10 unless a domain is given, and a bar on its continuous
///             axis gets 5 pixels of padding.
///         </description></item>
///         <item><description>
///             <b>Range.</b> x is [0, width], y is [height, 0] (continuous) or [0, height] (discrete). Size uses
///             [minimum, maximum] by mark (point-like marks: [4, (0.95 step)^2]; bar and tick: [2, step - 1]; rule: [1,
///             4]); opacity [0.3, 0.8]; colors the configured category scheme (<c>tableau10</c>). A discrete position
///             scale without an explicit range or a fixed size takes its size from the <c>step</c> (20 pixels by
///             default): see <see cref="ResolvedScale.NaturalExtent"/>. An explicit linear domain and range keep all
///             their entries and map piecewise like d3's <c>polymap</c> (<c>min(domain.length, range.length)</c> entries
///             are used, <c>zero</c>, pixel padding and <c>nice</c> change only the first and the last domain entry); a band
///             or point scale uses the first two entries of its range, as <c>scaleBand</c> does.
///         </description></item>
///     </list>
///     <para>
///         Deviations from Vega-Lite: values of different kinds in one discrete domain are ordered by kind instead of
///         by JavaScript's coercions (see <see cref="DataValue.Compare"/>); a color scheme other than <c>tableau10</c>
///         falls back to it (see <see cref="ResolvedScale.ColorFallback"/>), and so does the <c>ordinal</c> range of an
///         ordinal color field (ColorBrewer blues in Vega-Lite); <c>scale.domain</c> forms other than an array are
///         ignored. The class is stateless and thread-safe.
///     </para>
/// </remarks>
internal static class ScaleResolver
{
    /// <summary>The channels that are resolved, in the order of the result.</summary>
    private static readonly string[] Channels = ["x", "y", "color", "fill", "stroke", "size", "opacity"];

    /// <summary>The marks whose discrete positions are bands, so that the mark fits into the band.</summary>
    private static readonly HashSet<string> BandMarks = ["rect", "bar", "image", "rule", "tick"];

    /// <summary>The ratio of the step that a point-like mark's largest size may span (Vega-Lite's MAX_SIZE_RANGE_STEP_RATIO).</summary>
    private const double MaxSizeRangeStepRatio = 0.95;

    /// <summary>The step Vega-Lite uses when the configuration has none.</summary>
    private const double DefaultStep = 20;

    /// <summary>The number of rows between two cancellation checks.</summary>
    private const int CancellationInterval = 4096;

    /// <summary>
    ///     Resolves the scales of a single view.
    /// </summary>
    /// <param name="view">The view object of the specification (its <c>mark</c>, <c>encoding</c>, <c>width</c> and <c>height</c> are read).</param>
    /// <param name="table">The data of the view.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="sourceName">The source name used in the locations of errors, or null.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="cancellationToken">A token that stops the resolution.</param>
    /// <returns>The scales; empty when the view has no mark or no encoding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="view"/>, <paramref name="table"/> or <paramref name="config"/> is null.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown when <c>scale.domain</c> or <c>scale.range</c> has the wrong shape; carries the location.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when a discrete domain exceeds <see cref="VegaLiteLimits.MaxScaleDomainSize"/>; carries the location.</exception>
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

        // The mark decides the scale kinds, the zero rule, the paddings and the size range
        var (markType, specifiedOrient) = ReadMark(view["mark"]);
        var encoding = view["encoding"] is { IsObject: true } e ? e : null;
        var orient = InferOrient(markType, specifiedOrient, encoding);
        var scales = new Dictionary<string, ResolvedScale>(StringComparer.Ordinal);
        if (markType.Length == 0 || encoding is null)
        {
            return new ResolvedScales(markType, orient, scales);
        }

        var context = new Context
        {
            Table = table,
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

        return new ResolvedScales(markType, orient, scales);
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
    ///     Reads the mark type and the orient from the <c>mark</c> property.
    /// </summary>
    /// <param name="mark">The <c>mark</c> node: a string or a mark definition.</param>
    /// <returns>The type (empty when there is none) and the specified orient.</returns>
    private static (string Type, string? Orient) ReadMark(SpecNode? mark) =>
        mark switch
        {
            { IsString: true } => (mark.StringValue!, null),
            { IsObject: true } => (mark["type"]?.AsString() ?? string.Empty, mark["orient"]?.AsString()),
            _ => (string.Empty, null),
        };

    /// <summary>
    ///     Tells whether a channel definition is an unbinned quantitative field or datum.
    /// </summary>
    /// <param name="definition">The channel definition, or null.</param>
    /// <returns><see langword="true"/> for a measure.</returns>
    private static bool IsUnbinnedQuantitative(SpecNode? definition) =>
        definition is not null
        && (definition["field"] is not null || definition["datum"] is not null)
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
    /// <param name="definition">The channel definition object.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The scale, or <see langword="null"/> when the channel has none.</returns>
    private static ResolvedScale? ResolveChannel(string channel, SpecNode definition, Context context)
    {
        // A channel without a field or datum has no scale (a value is used as it is), and so has "scale": null
        var scaleNode = definition["scale"];
        if (scaleNode is { IsNull: true })
        {
            return null;
        }

        var scaleDef = scaleNode is { IsObject: true } ? scaleNode : null;
        var fieldPath = definition["field"] is { IsString: true } fieldNode ? fieldNode.StringValue : null;
        var datum = definition["datum"] is { IsString: true } or { IsNumber: true } or { IsBoolean: true } ? definition["datum"] : null;
        if (fieldPath is null && datum is null)
        {
            return null;
        }

        var type = ReadType(definition, datum);
        if (type is not ("quantitative" or "nominal" or "ordinal"))
        {
            return null;
        }

        var kind = ChooseKind(channel, type, scaleDef?["type"]?.AsString(), context.MarkType);
        if (kind is null)
        {
            return null;
        }

        var location = definition.GetLocation(context.SourceName);
        var common = new CommonInputs(channel, type, fieldPath, datum, definition, scaleDef, location);
        return kind == ScaleKind.Linear
            ? BuildLinear(common, context)
            : BuildDiscrete(common, kind.Value, context);
    }

    /// <summary>
    ///     Chooses the kind of scale for a channel and a field type.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="type">The field type.</param>
    /// <param name="specified">The explicit <c>scale.type</c>, or null.</param>
    /// <param name="markType">The mark type.</param>
    /// <returns>The kind, or <see langword="null"/> when the channel gets no scale.</returns>
    private static ScaleKind? ChooseKind(string channel, string type, string? specified, string markType)
    {
        var isColor = channel is "color" or "fill" or "stroke";
        var isPosition = channel is "x" or "y";

        // The default kind of Vega-Lite's defaultType
        ScaleKind defaultKind;
        if (type == "quantitative")
        {
            defaultKind = ScaleKind.Linear;
        }
        else if (isColor)
        {
            defaultKind = ScaleKind.Ordinal;
        }
        else if (isPosition && BandMarks.Contains(markType))
        {
            defaultKind = ScaleKind.Band;
        }
        else
        {
            defaultKind = ScaleKind.Point;
        }

        // An explicit type counts when both the channel and the field type accept it
        var kind = defaultKind;
        if (specified is "linear" or "band" or "point" or "ordinal")
        {
            var requested = specified switch
            {
                "linear" => ScaleKind.Linear,
                "band" => ScaleKind.Band,
                "point" => ScaleKind.Point,
                _ => ScaleKind.Ordinal,
            };
            bool channelAccepts;
            if (isPosition)
            {
                channelAccepts = requested != ScaleKind.Ordinal;
            }
            else if (isColor)
            {
                channelAccepts = requested is ScaleKind.Linear or ScaleKind.Ordinal;
            }
            else
            {
                channelAccepts = requested != ScaleKind.Ordinal;
            }
            var typeAccepts = type == "quantitative" ? requested == ScaleKind.Linear : requested != ScaleKind.Linear;
            if (channelAccepts && typeAccepts)
            {
                kind = requested;
            }
        }

        // A quantitative color would need a color ramp, which is not drawn here
        return isColor && kind == ScaleKind.Linear ? null : kind;
    }

    /// <summary>
    ///     Builds a linear scale.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The scale, or null when the channel cannot have one (a size on a mark without sizes).</returns>
    private static ResolvedScale? BuildLinear(CommonInputs input, Context context)
    {
        // The domain: explicit, or the extent of the valid values; an explicit domain of more than two numbers is piecewise
        double start;
        double end;
        double[]? piecewise = null;
        var domainNode = input.ScaleDef?["domain"] is { IsArray: true } array ? array : null;
        if (domainNode is not null)
        {
            var explicitDomain = ReadNumericDomain(domainNode, context);
            (start, end) = (explicitDomain[0], explicitDomain[^1]);
            piecewise = explicitDomain.Length > 2 ? explicitDomain : null;
        }
        else
        {
            (start, end) = ExtentOf(input, context);
        }

        // The zero rule pulls the end values to zero
        var zero = input.ScaleDef?["zero"]?.AsBool() ?? ZeroRule(input, domainNode, context);
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

            if (piecewise is not null)
            {
                piecewise[0] = start;
                piecewise[^1] = end;
            }
        }

        // The value range of a size or opacity scale
        (double, double)? valueRange = null;
        if (input.Channel == "size")
        {
            if (SizeRange(context) is not { } sizeRange)
            {
                return null;
            }

            valueRange = sizeRange;
        }
        else if (input.Channel == "opacity")
        {
            valueRange = (context.Config.GetNumber("scale.minOpacity") ?? 0.3, context.Config.GetNumber("scale.maxOpacity") ?? 0.8);
        }

        var isPosition = input.Channel is "x" or "y";
        var sortDescending = input.Definition["sort"]?.AsString() == "descending";
        return new ResolvedScale
        {
            Channel = input.Channel,
            Kind = ScaleKind.Linear,
            FieldPath = input.FieldPath,
            DataType = input.Type,
            Location = input.Location,
            DomainStart = start,
            DomainEnd = end,
            Domain = piecewise,
            NiceCount = NiceRule(input, domainNode is not null, isPosition),
            PaddingPixels = isPosition ? ContinuousPadding(input, context) : 0,
            Clamp = input.ScaleDef?["clamp"]?.AsBool() ?? false,
            Round = input.ScaleDef?["round"]?.AsBool() ?? false,
            Reverse = input.ScaleDef?["reverse"]?.AsBool() ?? sortDescending,
            ExplicitRange = ReadRange(input, context),
            ValueRange = valueRange,
        };
    }

    /// <summary>
    ///     Builds a band, point or ordinal scale.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="kind">The kind of scale.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The scale, or null when the channel cannot have one (a size on a mark without sizes).</returns>
    private static ResolvedScale? BuildDiscrete(CommonInputs input, ScaleKind kind, Context context)
    {
        var isPosition = input.Channel is "x" or "y";
        var domain = DiscreteDomain(input, context);

        // Size and opacity categories are spread over the numeric range of the channel
        (double, double)? valueRange = null;
        if (input.Channel == "size")
        {
            if (SizeRange(context) is not { } sizeRange)
            {
                return null;
            }

            valueRange = sizeRange;
        }
        else if (input.Channel == "opacity")
        {
            valueRange = (context.Config.GetNumber("scale.minOpacity") ?? 0.3, context.Config.GetNumber("scale.maxOpacity") ?? 0.8);
        }

        var (inner, outer) = kind == ScaleKind.Ordinal ? (0.0, 0.0) : Paddings(input, kind, context, isPosition);
        var explicitRange = ReadRange(input, context);

        // Colors for an ordinal scale
        IReadOnlyList<string> colors = [];
        var fallback = false;
        if (kind == ScaleKind.Ordinal)
        {
            (colors, fallback) = ResolveColors(input, context);
        }

        // A discrete position without an explicit range is sized by its step, unless the view size is fixed
        double? step = null;
        if (isPosition && kind != ScaleKind.Ordinal && explicitRange is null)
        {
            step = input.Channel == "x" ? context.WidthStep : context.HeightStep;
        }

        return new ResolvedScale
        {
            Channel = input.Channel,
            Kind = kind,
            FieldPath = input.FieldPath,
            DataType = input.Type,
            Location = input.Location,
            DiscreteDomain = domain,
            PaddingInner = inner,
            PaddingOuter = outer,
            Align = Math.Clamp(input.ScaleDef?["align"]?.AsNumber() ?? 0.5, 0, 1),
            Round = input.ScaleDef?["round"]?.AsBool() ?? false,
            Reverse = input.ScaleDef?["reverse"]?.AsBool() ?? false,
            Step = step,
            ExplicitRange = explicitRange,
            ValueRange = valueRange,
            ColorRange = colors,
            ColorFallback = fallback,
        };
    }

    /// <summary>
    ///     Applies Vega-Lite's <c>zero</c> rule.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="domainNode">The explicit domain array, or null.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>Whether the domain is extended to include zero.</returns>
    private static bool ZeroRule(CommonInputs input, SpecNode? domainNode, Context context)
    {
        // An explicit domain is not augmented, except that one that already spans zero keeps zero
        if (domainNode is not null)
        {
            return domainNode.Items.Count > 0
                && domainNode.Items[0] is { IsNumber: true } first && first.NumberValue <= 0
                && domainNode.Items[^1] is { IsNumber: true } last && last.NumberValue >= 0;
        }

        // A quantitative size starts at zero
        if (input.Channel == "size")
        {
            return true;
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
    /// <param name="hasDomain">Whether the specification gives the domain.</param>
    /// <param name="isPosition">Whether the channel is x or y.</param>
    /// <returns>The tick count to nice with, or null for no nice.</returns>
    private static double? NiceRule(CommonInputs input, bool hasDomain, bool isPosition)
    {
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

        // By default x and y get nice unless a domain is given
        return isPosition && !hasDomain ? 10 : null;
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

        // A bar on the axis that carries its width gets room for half the bar
        var carriesBandWidth = (context.Orient == "vertical" && input.Channel == "x") || (context.Orient == "horizontal" && input.Channel == "y");
        if (context.MarkType == "bar" && carriesBandWidth && !IsEnabled(input.Definition["bin"]))
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
    ///     Reads an explicit <c>scale.range</c> of numbers and the keywords <c>width</c> and <c>height</c>.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts (for the source name).</param>
    /// <returns>The ends of the range; null when the range is absent or not an array of numbers (for example a color range).</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when a numeric range has fewer than two ends.</exception>
    private static IReadOnlyList<RangeEnd>? ReadRange(CommonInputs input, Context context)
    {
        if (input.ScaleDef?["range"] is not { IsArray: true } range || input.Channel is "color" or "fill" or "stroke")
        {
            return null;
        }

        var ends = new List<RangeEnd>();
        foreach (var item in range.Items)
        {
            if (item.IsNumber)
            {
                ends.Add(new RangeEnd(item.NumberValue, false));
            }
            else if (item is { IsString: true } && item.StringValue is "width" or "height")
            {
                ends.Add(new RangeEnd(0, true));
            }
            else
            {
                throw new VegaLiteSpecException("The scale range must be an array of numbers (or the keywords \"width\" and \"height\").", item.GetLocation(context.SourceName));
            }
        }

        if (ends.Count < 2)
        {
            throw new VegaLiteSpecException("The scale range needs at least two values.", range.GetLocation(context.SourceName));
        }

        return ends;
    }

    /// <summary>
    ///     Reads an explicit linear domain: an array of numbers, all of which are kept (more than two make a piecewise scale).
    /// </summary>
    /// <param name="domain">The <c>scale.domain</c> array.</param>
    /// <param name="context">The view facts (for the source name).</param>
    /// <returns>The entries.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the array has fewer than two entries or a non-numeric entry.</exception>
    private static double[] ReadNumericDomain(SpecNode domain, Context context)
    {
        if (domain.Items.Count < 2)
        {
            throw new VegaLiteSpecException("The scale domain of a quantitative field needs at least two numbers.", domain.GetLocation(context.SourceName));
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
    ///     Finds the extent of the valid numeric values of a channel.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The minimum and maximum; (0, 1) when there is no valid value.</returns>
    private static (double Min, double Max) ExtentOf(CommonInputs input, Context context)
    {
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        if (input.FieldPath is not null)
        {
            var field = context.Table.FindField(input.FieldPath);
            if (field >= 0)
            {
                var row = 0;
                foreach (var value in context.Table.Column(field))
                {
                    if (++row % CancellationInterval == 0)
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                    }

                    if (value.TryGetQuantitative(out var number))
                    {
                        min = Math.Min(min, number);
                        max = Math.Max(max, number);
                    }
                }
            }
        }
        else if (ToDataValue(input.Datum!).TryGetQuantitative(out var datumNumber))
        {
            min = max = datumNumber;
        }

        return double.IsFinite(min) ? (min, max) : (0, 1);
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
            foreach (var item in explicitDomain.Items)
            {
                var value = ToDataValue(item);
                if (seen.Add(value))
                {
                    domain.Add(value);
                }
            }

            return CheckDomainSize(domain, input, context);
        }

        // Otherwise the distinct values in order of first appearance...
        if (input.FieldPath is not null)
        {
            var field = context.Table.FindField(input.FieldPath);
            var rows = context.Table.RowCount;
            for (var row = 0; row < rows; row++)
            {
                if (row % CancellationInterval == 0)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                }

                var value = field >= 0 ? context.Table.GetValue(row, field) : default;
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
        var sort = input.Definition["sort"];
        if (sort is { IsNull: true })
        {
            return domain;
        }

        if (sort is { IsArray: true } order)
        {
            // Values missing from the sort array come first (index -1), then the array order; the first index of each
            // listed value is recorded once so ordering a domain stays linear in the domain size
            var firstIndex = new Dictionary<DataValue, int>();
            var position = 0;
            foreach (var item in order.Items)
            {
                firstIndex.TryAdd(ToDataValue(item), position++);
            }

            return [.. domain.OrderBy(value => firstIndex.TryGetValue(value, out var index) ? index : -1)];
        }

        var descending = sort?.AsString() == "descending";
        return descending
            ? [.. domain.OrderBy(v => v, Comparer<DataValue>.Create((x, y) => DataValue.Compare(y, x)))]
            : [.. domain.OrderBy(v => v, Comparer<DataValue>.Create(DataValue.Compare))];
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
    ///     Chooses the colors of an ordinal color scale.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The colors and whether they are the fallback palette for a scheme that is not available.</returns>
    private static (IReadOnlyList<string> Colors, bool Fallback) ResolveColors(CommonInputs input, Context context)
    {
        // An explicit range of colors
        if (input.ScaleDef?["range"] is { IsArray: true, Items.Count: > 0 } range && range.Items.All(i => i.IsString))
        {
            return ([.. range.Items.Select(i => i.StringValue!)], false);
        }

        // A named scheme of the scale
        if (input.ScaleDef?["scheme"] is { } scheme)
        {
            var name = scheme.IsString ? scheme.StringValue : scheme["name"]?.AsString();
            return ColorSchemes.TryGetScheme(name, out var colors) ? (colors, false) : (ColorSchemes.Tableau10, true);
        }

        // The configured palette: "category" for nominal fields, "ordinal" for ordinal fields
        var node = context.Config.Get(input.Type == "nominal" ? "range.category" : "range.ordinal");
        if (node is { Kind: ConfigNodeKind.Array } && node.Items.Count > 0 && node.Items.All(i => i.AsString() is not null))
        {
            return ([.. node.Items.Select(i => i.AsString()!)], false);
        }

        if (node?["scheme"]?.AsString() is { } schemeName && ColorSchemes.TryGetScheme(schemeName, out var configured))
        {
            return (configured, false);
        }

        return (ColorSchemes.Tableau10, true);
    }

    /// <summary>
    ///     Converts a JSON node to a data value.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The value; null for anything but a string, number or boolean.</returns>
    private static DataValue ToDataValue(SpecNode node) => node.Kind switch
    {
        SpecNodeKind.String => DataValue.FromString(node.StringValue),
        SpecNodeKind.Number => DataValue.FromNumber(node.NumberValue),
        SpecNodeKind.Boolean => DataValue.FromBoolean(node.BoolValue),
        _ => DataValue.Null,
    };

    /// <summary>The facts about the view that every channel needs.</summary>
    private sealed class Context
    {
        /// <summary>Gets the data of the view.</summary>
        public required DataTable Table { get; init; }

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
    private sealed record CommonInputs(
        string Channel,
        string Type,
        string? FieldPath,
        SpecNode? Datum,
        SpecNode Definition,
        SpecNode? ScaleDef,
        VegaLiteLocation Location);
}
