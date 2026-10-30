// Portions derived from Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Layout;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <summary>
///     What the builder needs to know to draw the axis of one position channel.
/// </summary>
/// <param name="Channel">The channel: <c>x</c> or <c>y</c>.</param>
/// <param name="ChannelDef">The channel definition of the encoding (its <c>axis</c>, <c>title</c> and <c>field</c> are read), or null.</param>
/// <param name="Scale">The resolved scale of the channel.</param>
/// <param name="Model">The scale model for the final size of the data rectangle.</param>
/// <param name="DataWidth">The width of the data rectangle in pixels.</param>
/// <param name="DataHeight">The height of the data rectangle in pixels.</param>
/// <param name="MarkType">The mark type of the view, or null; a <c>rect</c> mark with a discrete channel draws its axis in front of the mark.</param>
/// <param name="Budget">The scene budget the axis charges before it creates each item, or null for a budget of its own from <see cref="VegaLiteLimits.MaxSceneItems"/>.</param>
internal sealed record AxisRequest(string Channel, SpecNode? ChannelDef, ResolvedScale Scale, ScaleModel Model, double DataWidth, double DataHeight, string? MarkType = null, SceneBudget? Budget = null);

/// <summary>
///     One tick of an axis: the domain value, where it is on the axis and the label that names it.
/// </summary>
/// <param name="Value">The domain value (a number for a linear scale, a category for a band or point scale).</param>
/// <param name="Position">The position along the axis in pixels of the data rectangle, rounded like the tick mark (<c>tickRound</c>).</param>
/// <param name="Label">The label text before truncation; empty labels are not drawn.</param>
internal sealed record AxisTick(DataValue Value, double Position, string Label);

/// <summary>
///     The axis of one channel: its scene groups, how far it reaches and the ticks it has.
/// </summary>
/// <remarks>
///     Like Vega-Lite, which compiles the grid of an axis to an axis of its own, the grid is a separate group: the grids of all axes
///     are drawn first (behind everything), then the other parts of the axes, so a domain line is never covered by a grid line.
/// </remarks>
/// <param name="Group">The group (role <c>axis</c>), positioned in the coordinates of the data rectangle; its children are the ticks, labels, domain and title in drawing order.</param>
/// <param name="Grid">The group of the grid lines (role <c>axis-grid-group</c>) at the same position, or null when the axis has no grid.</param>
/// <param name="Bounds">The bounding box of the axis (ticks, labels, domain extent and title) in the coordinates of the data rectangle; the grid is not part of it.</param>
/// <param name="Orient">The side of the data rectangle the axis is on: <c>top</c>, <c>bottom</c>, <c>left</c> or <c>right</c>.</param>
/// <param name="Ticks">The ticks, including those whose labels were dropped because they overlap.</param>
/// <param name="ItemCount">The number of scene items in the groups, the groups themselves excluded.</param>
/// <param name="InFront">Whether the axis is drawn in front of the marks (<c>zindex</c> above 0) instead of behind them.</param>
internal sealed record AxisResult(SceneGroup Group, SceneGroup? Grid, SceneBounds Bounds, string Orient, IReadOnlyList<AxisTick> Ticks, int ItemCount, bool InFront);

/// <summary>
///     Builds the axis guide of a position scale: grid lines, tick marks, tick labels, the domain line and the title, and
///     measures how much room they need.
/// </summary>
/// <remarks>
///     <para>
///         <b>Where the rules come from.</b> The properties and their defaults are those of Vega-Lite 6 (<c>axis/parse.ts</c>,
///         <c>axis/properties.ts</c>, <c>axis/config.ts</c>) over Vega's axis encoders and layout (<c>axis-grid</c>,
///         <c>axis-ticks</c>, <c>axis-labels</c>, <c>axis-domain</c>, <c>axis-title</c>, <c>axisLayout</c>, <c>Overlap</c>).
///         A property is looked up in the axis of the channel, then the Vega-Lite only configuration
///         (<c>axisXBand</c>, <c>axisBand</c>, <c>axisQuantitative</c> and so on), then the Vega configuration
///         (<c>axisBand</c>, <c>axisBottom</c> and the other orients, <c>axisX</c>/<c>axisY</c>, <c>axis</c>), then the
///         <c>guide-label</c> and <c>guide-title</c> styles. A few properties have a Vega-Lite rule that applies when no configuration
///         names them: <c>grid</c> (on for a continuous scale), <c>labelAngle</c> (270 for the labels of a discrete x axis),
///         <c>labelFlush</c> (on for a continuous x axis) and <c>labelOverlap</c> (parity for continuous scales, off for
///         discrete ones).
///     </para>
///     <para>
///         <b>Geometry.</b> The axis group sits at the edge of the data rectangle shifted by half a pixel (Vega's
///         <c>translate</c> of 0.5), so a one pixel line is crisp. Tick and grid positions are the scale position plus the
///         band offset (half a band for a band scale, with the <c>-0.5</c> correction of <c>axisBand</c>) rounded half up; labels
///         use the unrounded position. Ticks stand <c>tickSize</c> out from the data rectangle, labels <c>tickSize + labelPadding</c>,
///         the extent is the ticks and labels clamped to <c>minExtent</c> and <c>maxExtent</c>, and the title sits
///         <c>titlePadding</c> beyond it. Bounds follow Vega: a stroked rule grows by half its width, text by its measured box.
///     </para>
///     <para>
///         <b>Ticks.</b> A continuous scale uses the ported d3 tick algorithm with Vega-Lite's count (a tick for every 40
///         pixels, one for every 10 pixels for a field that the encoding bins) and the d3 tick format (the
///         <c>normalizedNumberFormat</c>, <c>.0%</c> by default, for a normalized stack); a band or point scale has a tick for each category. <c>values</c> selects the ticks
///         (kept in range order, thinned when more than the count). Labels that overlap are removed with Vega's parity method
///         (<c>labelOverlap: true</c> or <c>"parity"</c>, the default for continuous scales) or its greedy method.
///     </para>
///     <para>
///         <b>Not drawn.</b> <c>labelBound</c>, <c>tickBand</c>, <c>tickExtra</c>, <c>position</c>, <c>translate</c>,
///         <c>bandPosition</c>, <c>titleX</c>, <c>titleY</c>, label and axis <c>encode</c>, custom format types and label expressions
///         (the feature catalog reports them); a <c>values</c> array that is not made of numbers, strings or booleans is ignored. The
///         class is stateless between calls and safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class AxisBuilder
{
    /// <summary>The unit of the Vega axis translate: half a pixel keeps one pixel lines crisp.</summary>
    private const double Translate = 0.5;

    /// <summary>The text measurer.</summary>
    private readonly TextMeasurer _measurer;

    /// <summary>The effective configuration.</summary>
    private readonly ResolvedConfig _config;

    /// <summary>The limit on ticks per axis.</summary>
    private readonly int _maxTicks;

    /// <summary>The limit on scene items, for an axis built without a shared budget.</summary>
    private readonly long _maxSceneItems;

    /// <summary>The source name used in the locations of errors.</summary>
    private readonly string? _sourceName;

    /// <summary>
    ///     Initializes an axis builder.
    /// </summary>
    /// <param name="measurer">The text measurer that sizes labels and titles.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="limits">The limits to enforce (<see cref="VegaLiteLimits.MaxTicks"/>); the defaults when null.</param>
    /// <param name="sourceName">The source name for the locations of errors, or null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="measurer"/> or <paramref name="config"/> is null.</exception>
    public AxisBuilder(TextMeasurer measurer, ResolvedConfig config, VegaLiteLimits? limits = null, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(measurer);
        ArgumentNullException.ThrowIfNull(config);
        _measurer = measurer;
        _config = config;
        _maxTicks = (limits ?? VegaLiteLimits.Default).MaxTicks;
        _maxSceneItems = (limits ?? VegaLiteLimits.Default).MaxSceneItems;
        _sourceName = sourceName;
    }

    /// <summary>
    ///     Gets how far a set of axes reach beyond the data rectangle.
    /// </summary>
    /// <param name="axes">The axes.</param>
    /// <param name="dataWidth">The width of the data rectangle.</param>
    /// <param name="dataHeight">The height of the data rectangle.</param>
    /// <returns>The reach on each side, never negative.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="axes"/> is null.</exception>
    public static ViewExtents GetExtents(IEnumerable<AxisResult> axes, double dataWidth, double dataHeight)
    {
        ArgumentNullException.ThrowIfNull(axes);
        var bounds = new SceneBounds(0, 0, dataWidth, dataHeight);
        foreach (var axis in axes)
        {
            bounds = bounds.Union(axis.Bounds);
        }

        return new ViewExtents(-bounds.X1, -bounds.Y1, bounds.X2 - dataWidth, bounds.Y2 - dataHeight);
    }

    /// <summary>
    ///     Builds the axis of a position channel.
    /// </summary>
    /// <param name="request">The channel, its scale and the size of the data rectangle.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>The axis, or <see langword="null"/> when the channel's <c>axis</c> is <c>null</c> (no axis is drawn).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown, with the location of the construct, when <c>axis.orient</c> is not valid for the channel or <c>axis.format</c> is not a d3 format specifier.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the channel, when the axis would have more ticks than <see cref="VegaLiteLimits.MaxTicks"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public AxisResult? Build(AxisRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var axisNode = request.ChannelDef?["axis"];
        if (axisNode is { IsNull: true } or { IsBoolean: true, BoolValue: false })
        {
            return null;
        }

        var location = (request.ChannelDef ?? axisNode)?.GetLocation(_sourceName) ?? default;
        var axis = axisNode is { IsObject: true } ? axisNode : null;
        var settings = new Settings(_config, axis, request.Channel, request.Scale.Kind);
        var orient = ResolveOrient(settings, request.Channel, (axis?["orient"] ?? request.ChannelDef)?.GetLocation(_sourceName) ?? location);
        settings.Orient = orient;
        var isX = orient is "top" or "bottom";
        var extent = isX ? request.DataWidth : request.DataHeight;
        var ticks = BuildTicks(request, axis, settings, extent, location, cancellationToken);

        var layout = new Layout(settings, request, orient, isX, extent, ticks);

        // Every scene item is charged before it is created: the axis group, then the grid group and rules, the tick
        // rules, the labels, the domain line and the title (the grid group exists only when there is a tick)
        var budget = request.Budget ?? new SceneBudget(_maxSceneItems);
        budget.Add(1, location);
        var gridRules = new List<SceneItem>();
        if (layout.ShowGrid && ticks.Count > 0)
        {
            budget.Add(1, location);
            gridRules = GridRules(layout, budget, location).ToList();
        }

        var children = new List<SceneItem>();
        var tickRules = layout.ShowTicks ? TickRules(layout, budget, location) : [];
        children.AddRange(tickRules);
        var labels = layout.ShowLabels ? Labels(layout, budget, location, cancellationToken) : [];
        children.AddRange(labels);
        if (layout.ShowDomain)
        {
            budget.Add(1, location);
            children.Add(DomainRule(layout));
        }

        // The extent of the axis is what its ticks and labels need, kept between minExtent and maxExtent (Vega's axisLayout)
        var bounds = SceneBounds.Empty;
        foreach (var rule in tickRules)
        {
            bounds = bounds.Union(RuleBounds((SceneRule)rule));
        }

        foreach (var label in labels)
        {
            bounds = bounds.Union(_measurer.GetBounds((SceneText)label));
        }

        var title = ResolveTitle(request, axis, settings);
        return Finish(layout, new AxisParts(gridRules, children), bounds, title, ticks, budget, location);
    }

    /// <summary>
    ///     The scene items of an axis before it is positioned: the grid lines and the other parts.
    /// </summary>
    /// <param name="Grid">The grid lines.</param>
    /// <param name="Main">The ticks, labels and domain line; the title is added to it.</param>
    private sealed record AxisParts(List<SceneItem> Grid, List<SceneItem> Main);

    /// <summary>
    ///     Adds the extent, the title and the translation to the guide and builds its result.
    /// </summary>
    /// <param name="layout">The axis layout facts.</param>
    /// <param name="parts">The grid lines and the other items.</param>
    /// <param name="tickAndLabelBounds">The bounds of the ticks and labels.</param>
    /// <param name="title">The title lines, or null for no title.</param>
    /// <param name="ticks">The ticks.</param>
    /// <param name="budget">The scene budget; the title text is charged before it is created.</param>
    /// <param name="location">The location of the channel for a budget failure.</param>
    /// <returns>The axis.</returns>
    private AxisResult Finish(Layout layout, AxisParts parts, SceneBounds tickAndLabelBounds, IReadOnlyList<string>? title, IReadOnlyList<AxisTick> ticks, SceneBudget budget, VegaLiteLocation location)
    {
        var children = parts.Main;
        var settings = layout.Settings;
        var minExtent = settings.Number("minExtent") ?? 0;
        var maxExtent = settings.Number("maxExtent") ?? 200;
        var range = Math.Abs(layout.Model.RangeEnd - layout.Model.RangeStart);
        var bounds = tickAndLabelBounds;

        // Vega's axisLayout: the extent is the far edge of the ticks and labels, clamped; the axis box spans the range by the extent
        double reach = layout.Orient switch
        {
            "top" => -bounds.Y1,
            "left" => -bounds.X1,
            "right" => bounds.X2,
            _ => bounds.Y2,
        };
        var extent = Math.Max(minExtent, Math.Min(maxExtent, reach));
        bounds = layout.Orient switch
        {
            "top" => bounds.Union(Point(0, -extent)).Union(Point(range, 0)),
            "left" => bounds.Union(Point(-extent, 0)).Union(Point(0, range)),
            "right" => bounds.Union(Point(0, 0)).Union(Point(extent, range)),
            _ => bounds.Union(Point(0, 0)).Union(Point(range, extent)),
        };

        var itemCount = children.Count + parts.Grid.Count;
        if (title is { Count: > 0 })
        {
            budget.Add(1, location);
            var text = TitleText(layout, title, extent);
            children.Add(text);
            bounds = bounds.Union(_measurer.GetBounds(text));
            itemCount++;
        }

        // The group origin: the data rectangle edge plus the offset, shifted by the half pixel of Vega's translate
        var offset = settings.Number("offset") ?? 0;
        var (originX, originY) = layout.Orient switch
        {
            "top" => (0.0, -offset),
            "left" => (-offset, 0.0),
            "right" => (layout.Request.DataWidth + offset, 0.0),
            _ => (0.0, layout.Request.DataHeight + offset),
        };
        var group = new SceneGroup("axis", originX + Translate, originY + Translate) { Children = children };
        var grid = parts.Grid.Count > 0 ? new SceneGroup("axis-grid-group", originX + Translate, originY + Translate) { Children = parts.Grid } : null;
        return new AxisResult(group, grid, bounds.Translate(originX, originY), layout.Orient, ticks, itemCount, layout.InFront);
    }

    /// <summary>
    ///     Creates a degenerate box that stands for a point, to grow bounds with.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The box.</returns>
    private static SceneBounds Point(double x, double y) => new(x, y, x, y);

    /// <summary>
    ///     Gets the bounds of a rule: its end points grown by half the stroke width (Vega's <c>boundStroke</c>).
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The bounds.</returns>
    private static SceneBounds RuleBounds(SceneRule rule)
    {
        var half = rule.Stroke is null || rule.Opacity == 0 || rule.StrokeOpacity == 0 ? 0 : (rule.StrokeWidth ?? 1) / 2;
        return new SceneBounds(
            Math.Min(rule.X, rule.X2) - half,
            Math.Min(rule.Y, rule.Y2) - half,
            Math.Max(rule.X, rule.X2) + half,
            Math.Max(rule.Y, rule.Y2) + half);
    }

    /// <summary>
    ///     Resolves and validates the side of the data rectangle the axis is on.
    /// </summary>
    /// <param name="settings">The axis settings.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="location">The location for the error.</param>
    /// <returns><c>top</c>, <c>bottom</c>, <c>left</c> or <c>right</c>.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the orient is not valid for the channel.</exception>
    private static string ResolveOrient(Settings settings, string channel, VegaLiteLocation location)
    {
        var orient = settings.ExplicitOrVl("orient")?.AsString() ?? (channel == "x" ? "bottom" : "left");
        var valid = channel == "x" ? orient is "top" or "bottom" : orient is "left" or "right";
        if (!valid)
        {
            throw new VegaLiteSpecException(
                $"The axis orient '{orient}' is not valid for the {channel} channel; use {(channel == "x" ? "top or bottom" : "left or right")}.",
                location);
        }

        return orient;
    }

    /// <summary>
    ///     Builds the ticks of the axis.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="axis">The axis object, or null.</param>
    /// <param name="settings">The axis settings.</param>
    /// <param name="extent">The length of the data rectangle along the axis.</param>
    /// <param name="location">The location of the channel for errors.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The ticks in axis order.</returns>
    private List<AxisTick> BuildTicks(AxisRequest request, SpecNode? axis, Settings settings, double extent, VegaLiteLocation location, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var model = request.Model;
        var format = settings.Explicit("format")?.AsString() ?? settings.VlConfig("format")?.AsString();

        // A normalized stack is a share of the whole: its axis shows percentages unless a format is given (Vega-Lite's normalizedNumberFormat)
        if (format is null && request.Scale is { Kind: ScaleKind.Linear, Stack: StackOffset.Normalize })
        {
            format = _config.GetString("normalizedNumberFormat") ?? ".0%";
        }

        var formatLocation = (axis?["format"] ?? axis ?? request.ChannelDef)?.GetLocation(_sourceName) ?? location;
        var values = axis?["values"] is { IsArray: true } valuesNode ? valuesNode.Items : null;
        var tickOffset = settings.Number("tickOffset") ?? 0;
        var round = settings.Bool("tickRound") ?? true;
        var bandShift = (model.Bandwidth * (settings.Number("bandPosition") ?? 0.5)) + tickOffset;
        List<AxisTick> ticks;
        if (model.Kind == ScaleKind.Linear)
        {
            ticks = LinearTicks(model, settings, extent, values, format, formatLocation, location, bandShift, round, request.Scale.Bin == BinKind.Binning);
        }
        else
        {
            ticks = DiscreteTicks(model, values, format, formatLocation, location, bandShift, round);
        }

        if (ticks.Count > _maxTicks)
        {
            throw new VegaLiteRenderException($"The axis has {ticks.Count} ticks, more than VegaLiteLimits.MaxTicks ({_maxTicks}).", location);
        }

        return ticks;
    }

    /// <summary>
    ///     Builds the ticks of a continuous scale.
    /// </summary>
    /// <param name="model">The scale.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="extent">The length of the data rectangle along the axis.</param>
    /// <param name="values">The <c>values</c> of the axis, or null.</param>
    /// <param name="format">The format specifier, or null.</param>
    /// <param name="formatLocation">The location of the format for errors.</param>
    /// <param name="location">The location of the channel.</param>
    /// <param name="shift">The offset added to the scale position.</param>
    /// <param name="round">Whether tick positions are rounded.</param>
    /// <param name="binning">Whether the field is binned by the encoding: its default tick count is one per ten pixels (Vega-Lite's <c>ceil(size / 10)</c>).</param>
    /// <returns>The ticks.</returns>
    private List<AxisTick> LinearTicks(ScaleModel model, Settings settings, double extent, IReadOnlyList<SpecNode>? values, string? format, VegaLiteLocation formatLocation, VegaLiteLocation location, double shift, bool round, bool binning)
    {
        var minStep = settings.Number("tickMinStep") ?? Ticks.DefaultAxisTickMinStep(format);
        var defaultCount = binning ? Math.Ceiling(extent / 10) : Ticks.DefaultAxisTickCount(extent);
        var requested = settings.Number("tickCount") ?? (values is null ? defaultCount : values.Count);
        var count = Ticks.ResolveTickCount(model.DomainStart, model.DomainEnd, requested, minStep);
        double[] positions;
        try
        {
            positions = values is null
                ? Ticks.GetTicks(model.DomainStart, model.DomainEnd, count, _maxTicks)
                : ValidTicks(model, values.Where(v => v.IsNumber).Select(v => v.NumberValue).ToList(), count);
        }
        catch (VegaLiteRenderException ex) when (!ex.Location.IsKnown)
        {
            throw new VegaLiteRenderException(ex.Message, location);
        }

        NumberFormat formatter;
        try
        {
            formatter = NumberFormat.TickFormat(model.DomainStart, model.DomainEnd, count, format);
        }
        catch (FormatException ex)
        {
            throw new VegaLiteSpecException($"The axis format is not a valid d3 format specifier: {ex.Message}", formatLocation, ex);
        }

        var ticks = new List<AxisTick>(positions.Length);
        foreach (var value in positions)
        {
            ticks.Add(new AxisTick(DataValue.FromNumber(value), Place(model.Map(value) + shift, round), formatter.Format(value)));
        }

        return ticks;
    }

    /// <summary>
    ///     Keeps the explicit tick values that lie in the range of the scale, in range order, thinned to the tick count (Vega's <c>validTicks</c>).
    /// </summary>
    /// <param name="model">The scale.</param>
    /// <param name="candidates">The candidate values.</param>
    /// <param name="count">The tick count.</param>
    /// <returns>The accepted values.</returns>
    private static double[] ValidTicks(ScaleModel model, List<double> candidates, double count)
    {
        var lo = Math.Floor(Math.Min(model.RangeStart, model.RangeEnd));
        var hi = Math.Ceiling(Math.Max(model.RangeStart, model.RangeEnd));
        var descending = model.RangeStart > model.RangeEnd;
        var kept = candidates
            .Select(v => (Value: v, Position: model.Map(v)))
            .Where(p => lo <= p.Position && p.Position <= hi)
            .OrderBy(p => descending ? -p.Position : p.Position)
            .Select(p => p.Value)
            .ToList();
        return Thin(kept, count);
    }

    /// <summary>
    ///     Removes every other tick while there are more than the count (keeping the ends, as Vega does).
    /// </summary>
    /// <typeparam name="T">The kind of tick.</typeparam>
    /// <param name="ticks">The ticks in range order.</param>
    /// <param name="count">The tick count.</param>
    /// <returns>The thinned ticks.</returns>
    private static T[] Thin<T>(List<T> ticks, double count)
    {
        if (!(count > 0) || ticks.Count <= 1)
        {
            return [.. ticks];
        }

        var ends = new[] { ticks[0], ticks[^1] };
        while (ticks.Count > count && ticks.Count >= 3)
        {
            ticks = ticks.Where((_, i) => i % 2 == 0).ToList();
        }

        return ticks.Count < 3 ? ends : [.. ticks];
    }

    /// <summary>
    ///     Builds the ticks of a band or point scale: one per category, or the categories named by <c>values</c>.
    /// </summary>
    /// <param name="model">The scale.</param>
    /// <param name="values">The <c>values</c> of the axis, or null.</param>
    /// <param name="format">The format specifier for numeric categories, or null.</param>
    /// <param name="formatLocation">The location of the format for errors.</param>
    /// <param name="location">The location of the channel.</param>
    /// <param name="shift">The offset added to the scale position.</param>
    /// <param name="round">Whether tick positions are rounded.</param>
    /// <returns>The ticks.</returns>
    private static List<AxisTick> DiscreteTicks(ScaleModel model, IReadOnlyList<SpecNode>? values, string? format, VegaLiteLocation formatLocation, VegaLiteLocation location, double shift, bool round)
    {
        _ = location;
        NumberFormat? formatter = null;
        if (format is not null)
        {
            try
            {
                formatter = NumberFormat.Create(format);
            }
            catch (FormatException ex)
            {
                throw new VegaLiteSpecException($"The axis format is not a valid d3 format specifier: {ex.Message}", formatLocation, ex);
            }
        }

        IEnumerable<DataValue> domain = model.Domain;
        if (values is not null)
        {
            var wanted = values.Select(ToDataValue).Where(v => v is not null).Select(v => v!.Value).ToList();
            var inRange = wanted.Where(v => model.Map(v) is not null).Select(v => (Value: v, Position: model.Map(v)!.Value)).ToList();
            var descending = model.RangeStart > model.RangeEnd;
            domain = inRange.OrderBy(p => descending ? -p.Position : p.Position).Select(p => p.Value);
        }

        var ticks = new List<AxisTick>();
        foreach (var value in domain)
        {
            if (model.Map(value) is not { } start)
            {
                continue;
            }

            var label = formatter is not null && value.IsNumber ? formatter.Format(value.Number) : value.ToText();
            ticks.Add(new AxisTick(value, Place(start + shift, round), label));
        }

        return ticks;
    }

    /// <summary>
    ///     Converts a <c>values</c> entry to a data value.
    /// </summary>
    /// <param name="node">The entry.</param>
    /// <returns>The value, or null for an entry that is not a number, string, boolean or null.</returns>
    private static DataValue? ToDataValue(SpecNode node) => node.Kind switch
    {
        SpecNodeKind.Number => DataValue.FromNumber(node.NumberValue),
        SpecNodeKind.String => DataValue.FromString(node.StringValue),
        SpecNodeKind.Boolean => DataValue.FromBoolean(node.BoolValue),
        SpecNodeKind.Null => DataValue.Null,
        _ => null,
    };

    /// <summary>
    ///     Places a tick: the position, rounded half up when the axis rounds ticks.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <param name="round">Whether to round.</param>
    /// <returns>The tick position.</returns>
    private static double Place(double position, bool round) => round ? TextMeasurer.RoundHalfUp(position) : position;

    /// <summary>
    ///     Builds the grid lines: one rule per tick across the data rectangle.
    /// </summary>
    /// <param name="layout">The axis layout facts.</param>
    /// <param name="budget">The scene budget; each rule is charged before it is created.</param>
    /// <param name="location">The location for a budget failure.</param>
    /// <returns>The rules.</returns>
    private static IEnumerable<SceneItem> GridRules(Layout layout, SceneBudget budget, VegaLiteLocation location)
    {
        var s = layout.Settings;
        var offset = s.Number("offset") ?? 0;
        foreach (var position in layout.TickList.Select(t => t.Position))
        {
            budget.Add(1, location);

            // The rule starts on the axis side of the data rectangle and ends on the far side, in the coordinates of the axis group
            yield return layout.Orient switch
            {
                "top" => Line(s, "grid", position, layout.Request.DataHeight + offset, position, offset, "axis-grid"),
                "bottom" => Line(s, "grid", position, -offset, position, -offset - layout.Request.DataHeight, "axis-grid"),
                "left" => Line(s, "grid", offset, position, layout.Request.DataWidth + offset, position, "axis-grid"),
                _ => Line(s, "grid", -offset, position, -offset - layout.Request.DataWidth, position, "axis-grid"),
            };
        }
    }

    /// <summary>
    ///     Builds a rule styled by the <c>grid</c>, <c>tick</c> or <c>domain</c> properties.
    /// </summary>
    /// <param name="s">The settings.</param>
    /// <param name="part">The property prefix: <c>grid</c>, <c>tick</c> or <c>domain</c>.</param>
    /// <param name="x1">The start x.</param>
    /// <param name="y1">The start y.</param>
    /// <param name="x2">The end x.</param>
    /// <param name="y2">The end y.</param>
    /// <param name="role">The role of the item.</param>
    /// <returns>The rule.</returns>
    private static SceneRule Line(Settings s, string part, double x1, double y1, double x2, double y2, string role) =>
        new(role, x1, y1, x2, y2)
        {
            Stroke = s.Text(part + "Color"),
            StrokeWidth = s.Number(part + "Width"),
            StrokeOpacity = s.Number(part + "Opacity") ?? 1,
            StrokeDash = s.NumberArray(part + "Dash"),
            StrokeDashOffset = s.Number(part + "DashOffset") ?? 0,
            StrokeCap = s.Text(part + "Cap"),
        };

    /// <summary>
    ///     Builds the tick marks: a short rule at every tick, pointing away from the data rectangle.
    /// </summary>
    /// <param name="layout">The axis layout facts.</param>
    /// <param name="budget">The scene budget; each rule is charged before it is created.</param>
    /// <param name="location">The location for a budget failure.</param>
    /// <returns>The rules.</returns>
    private static List<SceneItem> TickRules(Layout layout, SceneBudget budget, VegaLiteLocation location)
    {
        var s = layout.Settings;
        var size = layout.TickSize;
        var sign = layout.Sign;
        var rules = new List<SceneItem>(layout.TickList.Count);
        foreach (var position in layout.TickList.Select(t => t.Position))
        {
            budget.Add(1, location);
            rules.Add(layout.IsX
                ? Line(s, "tick", position, 0, position, sign * size, "axis-tick")
                : Line(s, "tick", 0, position, sign * size, position, "axis-tick"));
        }

        return rules;
    }

    /// <summary>
    ///     Builds the domain line along the data rectangle.
    /// </summary>
    /// <param name="layout">The axis layout facts.</param>
    /// <returns>The rule.</returns>
    private static SceneRule DomainRule(Layout layout)
    {
        var model = layout.Model;
        return layout.IsX
            ? Line(layout.Settings, "domain", model.RangeStart, 0, model.RangeEnd, 0, "axis-domain")
            : Line(layout.Settings, "domain", 0, model.RangeStart, 0, model.RangeEnd, "axis-domain");
    }

    /// <summary>
    ///     Builds the tick labels and removes the overlapping ones.
    /// </summary>
    /// <remarks>
    ///     A label that overlaps a neighbor is not part of the scene and costs no budget; the candidates are bounded by
    ///     <see cref="VegaLiteLimits.MaxTicks"/>, which is enforced before any label is created, and the labels that
    ///     remain are charged before they are added to the axis.
    /// </remarks>
    /// <param name="layout">The axis layout facts.</param>
    /// <param name="budget">The scene budget.</param>
    /// <param name="location">The location for a budget failure.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The labels that are drawn, in axis order.</returns>
    private List<SceneItem> Labels(Layout layout, SceneBudget budget, VegaLiteLocation location, CancellationToken cancellationToken)
    {
        var s = layout.Settings;
        var model = layout.Model;
        var distance = (layout.ShowTicks ? layout.TickSize : 0) + (s.Number("labelPadding") ?? 0);
        var labelOffset = (s.Number("labelOffset") ?? 0) + (s.Number("tickOffset") ?? 0);
        var flush = layout.FlushThreshold;
        var flushOffset = s.Number("labelFlushOffset") ?? 0;
        var angle = layout.LabelAngle;
        var align = layout.LabelAlign;
        var baseline = layout.LabelBaseline;
        var items = new List<SceneText>(layout.TickList.Count);
        var visible = new List<SceneText>(layout.TickList.Count);
        foreach (var tick in layout.TickList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Labels use the unrounded position of the band center, plus the tick offset and the label offset
            var start = model.Map(tick.Value) ?? tick.Position;
            var along = start + (model.Bandwidth * 0.5) + labelOffset;
            var flushed = flush is { } threshold ? Flush(model.RangeStart, model.RangeEnd, start, threshold) : 0;
            var x = layout.IsX ? along : layout.Sign * distance;
            var y = layout.IsX ? layout.Sign * distance : along;
            var a = align ?? DefaultAlign(layout, flushed);
            var b = baseline ?? DefaultBaseline(layout, flushed);
            var shift = flush is not null && flushOffset != 0 ? flushed * flushOffset : 0;
            var text = new SceneText("axis-label", x, y, [tick.Label])
            {
                Fill = s.Text("labelColor") ?? s.StyleText("guide-label", "fill"),
                FillOpacity = s.Number("labelOpacity") ?? 1,
                FontFamily = s.Text("labelFont") ?? s.StyleText("guide-label", "font"),
                FontSize = s.Number("labelFontSize") ?? s.StyleNumber("guide-label", "fontSize") ?? 10,
                FontWeight = s.Text("labelFontWeight") ?? s.StyleText("guide-label", "fontWeight"),
                FontStyle = s.Text("labelFontStyle") ?? s.StyleText("guide-label", "fontStyle"),
                LineHeight = s.Number("labelLineHeight"),
                Limit = s.Number("labelLimit") ?? 0,
                Angle = angle,
                Align = a,
                Baseline = b,
                Dx = layout.IsX && align is null ? shift : 0,
                Dy = !layout.IsX && baseline is null ? shift : 0,
            };
            items.Add(text);
            if (tick.Label.Length > 0)
            {
                visible.Add(text);
            }
        }

        var hidden = HiddenByOverlap(layout, visible);
        List<SceneItem> shown = [.. items.Where(i => !hidden.Contains(i))];
        budget.Add(shown.Count, location);
        return shown;
    }

    /// <summary>
    ///     Gets the default alignment of a label: flush labels at the ends of a horizontal axis lean inward, the others are centered or
    ///     face the data rectangle.
    /// </summary>
    /// <param name="layout">The axis layout facts.</param>
    /// <param name="flushed">-1 or 1 for a label that is flush with an end of the axis, else 0.</param>
    /// <returns>The alignment.</returns>
    private static string DefaultAlign(Layout layout, int flushed)
    {
        if (layout.IsX)
        {
            return Pick(flushed, "left", "right", "center");
        }

        return layout.Orient == "right" ? "left" : "right";
    }

    /// <summary>
    ///     Gets the default baseline of a label: the labels of a horizontal axis hang away from the data rectangle, flush labels of a
    ///     vertical axis lean inward.
    /// </summary>
    /// <param name="layout">The axis layout facts.</param>
    /// <param name="flushed">-1 or 1 for a label that is flush with an end of the axis, else 0.</param>
    /// <returns>The baseline.</returns>
    private static string DefaultBaseline(Layout layout, int flushed)
    {
        if (layout.IsX)
        {
            return layout.Orient == "top" ? "bottom" : "top";
        }

        return Pick(flushed, "top", "bottom", "middle");
    }

    /// <summary>
    ///     Picks one of three values by the sign of a flush result.
    /// </summary>
    /// <param name="flushed">-1 near the start, 1 near the end, 0 otherwise.</param>
    /// <param name="low">The value near the start.</param>
    /// <param name="high">The value near the end.</param>
    /// <param name="middle">The value elsewhere.</param>
    /// <returns>The picked value.</returns>
    private static string Pick(int flushed, string low, string high, string middle)
    {
        if (flushed < 0)
        {
            return low;
        }

        return flushed > 0 ? high : middle;
    }

    /// <summary>
    ///     Vega's <c>flush</c>: tells whether a position is within a threshold of the start (-1) or the end (+1) of a range, else 0.
    /// </summary>
    /// <param name="rangeStart">The start of the range.</param>
    /// <param name="rangeEnd">The end of the range.</param>
    /// <param name="value">The position.</param>
    /// <param name="threshold">The distance from an end that counts as flush.</param>
    /// <returns>-1 near the lower end, 1 near the upper end, otherwise 0.</returns>
    private static int Flush(double rangeStart, double rangeEnd, double value, double threshold)
    {
        var a = Math.Min(rangeStart, rangeEnd);
        var b = Math.Max(rangeStart, rangeEnd);
        var l = Math.Abs(value - a);
        var r = Math.Abs(b - value);
        if (l < r && l <= threshold)
        {
            return -1;
        }

        return r <= threshold ? 1 : 0;
    }

    /// <summary>
    ///     Finds the labels that Vega's overlap removal hides.
    /// </summary>
    /// <remarks>
    ///     Only labels wider and taller than a pixel take part. With at least three labels that overlap, the method
    ///     (parity: hide every other label; greedy: hide each label that overlaps the last visible one) repeats until nothing
    ///     overlaps or fewer than three labels are left; the last label is then kept visible.
    /// </remarks>
    /// <param name="layout">The axis layout facts.</param>
    /// <param name="labels">The labels with text, in axis order.</param>
    /// <returns>The labels to hide.</returns>
    private HashSet<SceneText> HiddenByOverlap(Layout layout, List<SceneText> labels)
    {
        var hidden = new HashSet<SceneText>();
        if (layout.Overlap is null)
        {
            return hidden;
        }

        var source = labels.Where(l => HasBounds(_measurer.GetBounds(l))).ToList();
        var separation = layout.Settings.Number("labelSeparation") ?? 0;
        var items = source;
        if (items.Count < 3 || !HasOverlap(items, separation))
        {
            return hidden;
        }

        do
        {
            items = layout.Overlap == "greedy" ? Greedy(items, separation, hidden) : Parity(items, hidden);
        }
        while (items.Count >= 3 && HasOverlap(items, separation));

        if (items.Count < 3 && hidden.Contains(source[^1]))
        {
            if (items.Count > 1)
            {
                hidden.Add(items[^1]);
            }

            hidden.Remove(source[^1]);
        }

        return hidden;
    }

    /// <summary>
    ///     Tells whether a label box is larger than a pixel in both directions (Vega skips labels without content).
    /// </summary>
    /// <param name="bounds">The bounds.</param>
    /// <returns><see langword="true"/> when the label takes part in the overlap removal.</returns>
    private static bool HasBounds(SceneBounds bounds) => bounds.Width > 1 && bounds.Height > 1;

    /// <summary>
    ///     Tells whether any two neighbors overlap, with a separation in pixels.
    /// </summary>
    /// <param name="items">The labels in axis order.</param>
    /// <param name="separation">The separation.</param>
    /// <returns><see langword="true"/> when at least one pair overlaps.</returns>
    private bool HasOverlap(List<SceneText> items, double separation)
    {
        for (var i = 1; i < items.Count; i++)
        {
            if (Intersect(_measurer.GetBounds(items[i - 1]), _measurer.GetBounds(items[i]), separation))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tells whether two boxes are closer than a separation (Vega's <c>intersect</c>).
    /// </summary>
    /// <param name="a">The first box.</param>
    /// <param name="b">The second box.</param>
    /// <param name="separation">The separation.</param>
    /// <returns><see langword="true"/> when the gap between the boxes is smaller than the separation.</returns>
    private static bool Intersect(SceneBounds a, SceneBounds b, double separation) =>
        separation > Math.Max(Math.Max(b.X1 - a.X2, a.X1 - b.X2), Math.Max(b.Y1 - a.Y2, a.Y1 - b.Y2));

    /// <summary>
    ///     The parity method: keeps the labels at even positions and hides the others.
    /// </summary>
    /// <param name="items">The labels.</param>
    /// <param name="hidden">Receives the hidden labels.</param>
    /// <returns>The labels that stay.</returns>
    private static List<SceneText> Parity(List<SceneText> items, HashSet<SceneText> hidden)
    {
        var kept = new List<SceneText>();
        for (var i = 0; i < items.Count; i++)
        {
            if (i % 2 == 0)
            {
                kept.Add(items[i]);
            }
            else
            {
                hidden.Add(items[i]);
            }
        }

        return kept;
    }

    /// <summary>
    ///     The greedy method: hides every label that overlaps the last label that stays.
    /// </summary>
    /// <param name="items">The labels.</param>
    /// <param name="separation">The separation in pixels.</param>
    /// <param name="hidden">Receives the hidden labels.</param>
    /// <returns>The labels that stay.</returns>
    private List<SceneText> Greedy(List<SceneText> items, double separation, HashSet<SceneText> hidden)
    {
        var kept = new List<SceneText>();
        foreach (var item in items)
        {
            if (kept.Count == 0 || !Intersect(_measurer.GetBounds(kept[^1]), _measurer.GetBounds(item), separation))
            {
                kept.Add(item);
            }
            else
            {
                hidden.Add(item);
            }
        }

        return kept;
    }

    /// <summary>
    ///     Resolves the title lines of the axis.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="axis">The axis object, or null.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>The lines, or null for no title.</returns>
    private static IReadOnlyList<string>? ResolveTitle(AxisRequest request, SpecNode? axis, Settings settings)
    {
        _ = settings;
        var node = axis?["title"] ?? request.ChannelDef?["title"];
        if (node is not null)
        {
            return TitleLines(node);
        }

        // Without a title anywhere, the plan's default title (Vega-Lite's "Sum of x", "x (binned)") names the axis; a datum has no name to show
        if (request.Scale.Title is { Length: > 0 } title)
        {
            return [title];
        }

        return request.Scale.FieldPath is { Length: > 0 } field ? [field] : null;
    }

    /// <summary>
    ///     Reads title lines from a node: a string, or an array of strings; null and other values mean no title.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The lines, or null for no title.</returns>
    private static IReadOnlyList<string>? TitleLines(SpecNode node)
    {
        if (node.IsString)
        {
            return node.StringValue!.Length > 0 ? [.. TextMeasurer.SplitLines(node.StringValue)] : null;
        }

        if (node.IsArray)
        {
            var lines = node.Items.Where(i => i.IsString).Select(i => i.StringValue!).ToList();
            return lines.Count > 0 ? lines : null;
        }

        return null;
    }

    /// <summary>
    ///     Builds the title text, placed beyond the extent of the axis.
    /// </summary>
    /// <param name="layout">The axis layout facts.</param>
    /// <param name="lines">The title lines.</param>
    /// <param name="extent">The extent of the ticks and labels.</param>
    /// <returns>The title.</returns>
    private SceneText TitleText(Layout layout, IReadOnlyList<string> lines, double extent)
    {
        var s = layout.Settings;
        var model = layout.Model;
        var anchor = s.Text("titleAnchor");
        var fraction = 0.5;
        var defaultAlign = "center";
        if (anchor == "start")
        {
            fraction = 0;
            defaultAlign = "left";
        }
        else if (anchor == "end")
        {
            fraction = 1;
            defaultAlign = "right";
        }

        var along = model.RangeStart + ((model.RangeEnd - model.RangeStart) * fraction);
        var padding = s.Number("titlePadding") ?? 4;
        var fontSize = s.Number("titleFontSize") ?? s.StyleNumber("guide-title", "fontSize") ?? 11;
        var lineHeight = s.Number("titleLineHeight");

        // Vega: multi-line titles are pushed out by their extra lines, except below the data rectangle
        var multiLine = layout.Orient == "bottom" ? 0 : (lines.Count - 1) * TextMeasurer.LineHeight(fontSize, lineHeight);
        var distance = layout.Sign * (extent + multiLine + padding);
        var defaultAngle = layout.IsX ? 0 : layout.Sign * 90.0;
        var defaultBaseline = "bottom";
        if (layout.IsX && layout.Orient == "bottom")
        {
            defaultBaseline = "top";
        }
        return new SceneText("axis-title", layout.IsX ? along : distance, layout.IsX ? distance : along, lines)
        {
            Fill = s.Text("titleColor") ?? s.StyleText("guide-title", "fill"),
            FillOpacity = s.Number("titleOpacity") ?? 1,
            FontFamily = s.Text("titleFont") ?? s.StyleText("guide-title", "font"),
            FontSize = fontSize,
            FontWeight = s.Text("titleFontWeight") ?? s.StyleText("guide-title", "fontWeight"),
            FontStyle = s.Text("titleFontStyle") ?? s.StyleText("guide-title", "fontStyle"),
            LineHeight = lineHeight,
            Limit = s.Number("titleLimit") ?? 0,
            Align = s.Text("titleAlign") ?? defaultAlign,
            Angle = s.Number("titleAngle") ?? defaultAngle,
            Baseline = s.Text("titleBaseline") ?? defaultBaseline,
        };
    }

    /// <summary>
    ///     The facts about one axis that the item builders share: its orientation, its resolved properties and its ticks.
    /// </summary>
    private sealed class Layout
    {
        /// <summary>
        ///     Resolves the layout facts.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="request">The request.</param>
        /// <param name="orient">The orientation.</param>
        /// <param name="isX">Whether the axis is horizontal.</param>
        /// <param name="extent">The length of the data rectangle along the axis.</param>
        /// <param name="ticks">The ticks.</param>
        public Layout(Settings settings, AxisRequest request, string orient, bool isX, double extent, List<AxisTick> ticks)
        {
            Settings = settings;
            Request = request;
            Orient = orient;
            IsX = isX;
            TickList = ticks;
            Model = request.Model;
            Sign = orient is "left" or "top" ? -1 : 1;
            var rule = request.Scale.Kind == ScaleKind.Linear;
            var binned = request.ChannelDef?["bin"] is { } bin && !bin.IsNull && !(bin.IsBoolean && !bin.BoolValue);
            ShowGrid = settings.ExplicitOrVl("grid")?.AsBool() ?? (rule && (request.ChannelDef?["field"] is not null || request.ChannelDef?["aggregate"] is { IsNull: false }) && !binned);
            ShowTicks = settings.Bool("ticks") ?? true;
            ShowLabels = settings.Bool("labels") ?? true;
            ShowDomain = settings.Bool("domain") ?? true;
            TickSize = settings.Number("tickSize") ?? 5;
            _ = extent;
            var isDiscreteType = request.Scale.DataType is "nominal" or "ordinal";
            var ruleAngle = request.Channel == "x" && isDiscreteType ? 270.0 : (double?)null;
            var configured = settings.VlNumber("labelAngle") ?? ruleAngle;
            var angle = configured is { } a ? Normalize(a) : (double?)null;
            LabelAngle = angle ?? Normalize(settings.Number("labelAngle") ?? 0);
            LabelAlign = settings.ExplicitOrVl("labelAlign")?.AsString() ?? (angle is { } va ? DefaultLabelAlign(va, orient, isX) : null);
            LabelBaseline = settings.ExplicitOrVl("labelBaseline")?.AsString() ?? (angle is { } vb ? DefaultLabelBaseline(vb, orient, isX) : null);
            FlushThreshold = ResolveFlush(settings, request.Channel, request.Scale.DataType);
            Overlap = ResolveOverlap(settings, isDiscreteType);

            // Vega-Lite draws the axes of a rect mark with a discrete channel in front of it, all others behind the marks
            var defaultZindex = request.MarkType == "rect" && isDiscreteType ? 1 : 0;
            InFront = (settings.ExplicitOrVl("zindex")?.AsNumber() ?? defaultZindex) > 0;
        }

        /// <summary>Gets a value indicating whether the axis is drawn in front of the marks.</summary>
        public bool InFront { get; }

        /// <summary>Gets the settings.</summary>
        public Settings Settings { get; }

        /// <summary>Gets the request.</summary>
        public AxisRequest Request { get; }

        /// <summary>Gets the scale model.</summary>
        public ScaleModel Model { get; }

        /// <summary>Gets the orientation.</summary>
        public string Orient { get; }

        /// <summary>Gets a value indicating whether the axis is horizontal.</summary>
        public bool IsX { get; }

        /// <summary>Gets the sign of the direction away from the data rectangle: -1 for top and left, 1 for bottom and right.</summary>
        public int Sign { get; }

        /// <summary>Gets the ticks.</summary>
        public List<AxisTick> TickList { get; }

        /// <summary>Gets a value indicating whether grid lines are drawn.</summary>
        public bool ShowGrid { get; }

        /// <summary>Gets a value indicating whether tick marks are drawn.</summary>
        public bool ShowTicks { get; }

        /// <summary>Gets a value indicating whether labels are drawn.</summary>
        public bool ShowLabels { get; }

        /// <summary>Gets a value indicating whether the domain line is drawn.</summary>
        public bool ShowDomain { get; }

        /// <summary>Gets the length of the tick marks.</summary>
        public double TickSize { get; }

        /// <summary>Gets the rotation of the labels in degrees, from 0 to 360.</summary>
        public double LabelAngle { get; }

        /// <summary>Gets the label alignment that overrides the default one, or null.</summary>
        public string? LabelAlign { get; }

        /// <summary>Gets the label baseline that overrides the default one, or null.</summary>
        public string? LabelBaseline { get; }

        /// <summary>Gets the distance from the ends of the axis within which labels are flush, or null for no flushing.</summary>
        public double? FlushThreshold { get; }

        /// <summary>Gets the overlap removal method (<c>parity</c> or <c>greedy</c>), or null for none.</summary>
        public string? Overlap { get; }

        /// <summary>
        ///     Normalizes an angle to the range from 0 up to 360.
        /// </summary>
        /// <param name="angle">The angle in degrees.</param>
        /// <returns>The angle.</returns>
        private static double Normalize(double angle) => (((angle % 360) + 360) % 360);

        /// <summary>
        ///     Vega-Lite's default label alignment for a label angle.
        /// </summary>
        /// <param name="angle">The angle.</param>
        /// <param name="orient">The orientation.</param>
        /// <param name="isX">Whether the axis is horizontal.</param>
        /// <returns>The alignment, or null for the Vega default.</returns>
        private static string? DefaultLabelAlign(double angle, string orient, bool isX)
        {
            var start = isX ? 0 : 90;
            if ((angle + start) % 180 == 0)
            {
                return isX ? null : "center";
            }

            var main = isX ? "bottom" : "left";
            return (start < angle && angle < 180 + start) == (orient == main) ? "left" : "right";
        }

        /// <summary>
        ///     Vega-Lite's default label baseline for a label angle.
        /// </summary>
        /// <param name="angle">The angle.</param>
        /// <param name="orient">The orientation.</param>
        /// <param name="isX">Whether the axis is horizontal.</param>
        /// <returns>The baseline, or null for the Vega default.</returns>
        private static string? DefaultLabelBaseline(double angle, string orient, bool isX)
        {
            if (isX)
            {
                if ((angle is > 45 and < 135) || (angle is > 225 and < 315))
                {
                    return "middle";
                }

                return (angle <= 45 || angle >= 315) == (orient == "top") ? "bottom" : "top";
            }

            if (angle <= 45 || angle >= 315 || (angle >= 135 && angle <= 225))
            {
                return null;
            }

            return (angle is >= 45 and <= 135) == (orient == "left") ? "top" : "bottom";
        }

        /// <summary>
        ///     Resolves <c>labelFlush</c>: true counts as one pixel, a number as itself, off otherwise.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="channel">The channel.</param>
        /// <param name="dataType">The field type.</param>
        /// <returns>The threshold, or null for no flushing.</returns>
        private static double? ResolveFlush(Settings settings, string channel, string dataType)
        {
            var node = settings.ExplicitOrVl("labelFlush");
            if (node is null)
            {
                return channel == "x" && dataType == "quantitative" ? 1.0 : null;
            }

            return node.Kind switch
            {
                ConfigNodeKind.Boolean => node.BoolValue ? 1.0 : null,
                ConfigNodeKind.Number => node.NumberValue,
                _ => null,
            };
        }

        /// <summary>
        ///     Resolves <c>labelOverlap</c>: parity by default for continuous scales, none for discrete ones.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="isDiscreteType">Whether the field is nominal or ordinal.</param>
        /// <returns>The method, or null for none.</returns>
        private static string? ResolveOverlap(Settings settings, bool isDiscreteType)
        {
            var node = settings.ExplicitOrVl("labelOverlap");
            if (node is null)
            {
                return isDiscreteType ? null : "parity";
            }

            return node.Kind switch
            {
                ConfigNodeKind.Boolean => node.BoolValue ? "parity" : null,
                ConfigNodeKind.String when node.StringValue == "greedy" => "greedy",
                ConfigNodeKind.String when node.StringValue == "parity" => "parity",
                _ => null,
            };
        }
    }

    /// <summary>
    ///     Looks up axis properties in the order Vega-Lite and Vega define.
    /// </summary>
    private sealed class Settings
    {
        /// <summary>The effective configuration.</summary>
        private readonly ResolvedConfig _config;

        /// <summary>The <c>axis</c> object of the channel, or null.</summary>
        private readonly SpecNode? _axis;

        /// <summary>The names of the Vega-Lite only configuration objects, highest precedence first.</summary>
        private readonly string[] _vlOnly;

        /// <summary>The channel letter in upper case.</summary>
        private readonly string _channelName;

        /// <summary>
        ///     Initializes the settings.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <param name="axis">The axis object, or null.</param>
        /// <param name="channel">The channel.</param>
        /// <param name="kind">The kind of the scale.</param>
        public Settings(ResolvedConfig config, SpecNode? axis, string channel, ScaleKind kind)
        {
            _config = config;
            _axis = axis;
            _channelName = channel.ToUpperInvariant();
            _vlOnly = kind switch
            {
                ScaleKind.Band => [$"axis{_channelName}Band", $"axis{_channelName}Discrete", "axisBand", "axisDiscrete"],
                ScaleKind.Point => [$"axis{_channelName}Point", $"axis{_channelName}Discrete", "axisPoint", "axisDiscrete"],
                _ => [$"axis{_channelName}Quantitative", "axisQuantitative"],
            };
        }

        /// <summary>Gets or sets the orientation, once it is known (it selects the <c>axisBottom</c> style configuration).</summary>
        public string? Orient { get; set; }

        /// <summary>
        ///     Reads a property of the axis object.
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The value (a null node for JSON null), or null when the axis does not have it.</returns>
        public ConfigNode? Explicit(string name) => _axis?[name] is { } node ? ConfigNode.FromSpec(node) : null;

        /// <summary>
        ///     Reads a property from the Vega-Lite configuration alone (without the Vega defaults).
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The value, or null.</returns>
        public ConfigNode? VlConfig(string name) => FromLayer(_config.VegaLite, name);

        /// <summary>
        ///     Reads a property from the axis object, then the Vega-Lite configuration.
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The value, or null.</returns>
        public ConfigNode? ExplicitOrVl(string name) => Explicit(name) ?? VlConfig(name);

        /// <summary>
        ///     Reads a number property with the full precedence (axis, configuration, Vega defaults).
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The number, or null.</returns>
        public double? Number(string name) => Effective(name)?.AsNumber();

        /// <summary>
        ///     Reads a number property from the axis object and the Vega-Lite configuration only.
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The number, or null.</returns>
        public double? VlNumber(string name) => ExplicitOrVl(name)?.AsNumber();

        /// <summary>
        ///     Reads a boolean property with the full precedence.
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The value, or null.</returns>
        public bool? Bool(string name) => Effective(name)?.AsBool();

        /// <summary>
        ///     Reads a text property with the full precedence; a number is read as its text (a font weight of 700).
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The text, or null.</returns>
        public string? Text(string name) => TextOf(Effective(name));

        /// <summary>
        ///     Reads an array of numbers (a dash pattern) with the full precedence.
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The numbers, or null.</returns>
        public IReadOnlyList<double>? NumberArray(string name) =>
            Effective(name) is { Kind: ConfigNodeKind.Array } node ? node.Items.Select(i => i.AsNumber()).Where(n => n is not null).Select(n => n!.Value).ToList() : null;

        /// <summary>
        ///     Reads a text property of a named style (<c>guide-label</c> or <c>guide-title</c>).
        /// </summary>
        /// <param name="style">The style name.</param>
        /// <param name="property">The property.</param>
        /// <returns>The text, or null.</returns>
        public string? StyleText(string style, string property) => TextOf(_config.ResolveStyleProperty([style], property));

        /// <summary>
        ///     Reads a number property of a named style.
        /// </summary>
        /// <param name="style">The style name.</param>
        /// <param name="property">The property.</param>
        /// <returns>The number, or null.</returns>
        public double? StyleNumber(string style, string property) => _config.ResolveStyleProperty([style], property)?.AsNumber();

        /// <summary>
        ///     Reads a property with the full precedence: the axis, the Vega-Lite only configuration, then the Vega configuration
        ///     (<c>axisBand</c> for band scales, the orient, <c>axisX</c>/<c>axisY</c>, <c>axis</c>) with the Vega defaults.
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The value, or null.</returns>
        private ConfigNode? Effective(string name) => Explicit(name) ?? FromLayer(_config.Root, name);

        /// <summary>
        ///     Looks a property up in one configuration tree.
        /// </summary>
        /// <param name="layer">The tree.</param>
        /// <param name="name">The property.</param>
        /// <returns>The value, or null.</returns>
        private ConfigNode? FromLayer(ConfigNode layer, string name)
        {
            foreach (var key in _vlOnly)
            {
                if (layer[key]?[name] is { } vl)
                {
                    return vl;
                }
            }

            var orientKey = Orient is { Length: > 0 } o ? "axis" + char.ToUpperInvariant(o[0]) + o[1..] : null;
            return (orientKey is null ? null : layer[orientKey]?[name]) ?? layer["axis" + _channelName]?[name] ?? layer["axis"]?[name];
        }

        /// <summary>
        ///     Reads the text of a node: a string as is, a number in invariant culture.
        /// </summary>
        /// <param name="node">The node, or null.</param>
        /// <returns>The text, or null.</returns>
        private static string? TextOf(ConfigNode? node) => node?.Kind switch
        {
            ConfigNodeKind.String => node.StringValue,
            ConfigNodeKind.Number => node.NumberValue.ToString("R", CultureInfo.InvariantCulture),
            _ => null,
        };
    }
}
