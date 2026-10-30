// Portions derived from Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause): legend guides; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <summary>
///     The paint and the border of the box of a legend: Vega's <c>fillColor</c>, <c>strokeColor</c> and <c>cornerRadius</c>.
/// </summary>
/// <param name="Fill">The background fill as a CSS color, or null for none.</param>
/// <param name="Stroke">The border color, or null for none.</param>
/// <param name="StrokeWidth">The border width, or null for the default of 1.</param>
/// <param name="StrokeDash">The dash pattern of the border, or null for a solid line.</param>
/// <param name="CornerRadius">The corner radius.</param>
internal sealed record LegendFrame(string? Fill, string? Stroke, double? StrokeWidth, IReadOnlyList<double>? StrokeDash, double CornerRadius);

/// <summary>
///     A legend that was built: its parts relative to its own origin, its size and how it asks to be placed.
/// </summary>
/// <remarks>
///     The legend group itself is created by <see cref="CreateGroup"/> once the layout knows where it goes (a scene group has a fixed
///     position), so the result holds its children and the facts the layout needs.
/// </remarks>
/// <param name="Kind">The kind of the legend.</param>
/// <param name="Orient">The orient: <c>right</c>, <c>left</c>, <c>top</c>, <c>bottom</c>, <c>top-left</c>, <c>top-right</c>, <c>bottom-left</c>, <c>bottom-right</c> or <c>none</c>.</param>
/// <param name="Offset">The distance to the data rectangle the legend asks for (<c>legend.offset</c>), or null for the configured layout offset.</param>
/// <param name="X">The x position of a legend with the orient <c>none</c> (<c>legendX</c>).</param>
/// <param name="Y">The y position of a legend with the orient <c>none</c> (<c>legendY</c>).</param>
/// <param name="Width">The width of the legend box (its content and padding, rounded up).</param>
/// <param name="Height">The height of the legend box.</param>
/// <param name="Bounds">The bounding box of the legend relative to its origin: the box grown by half the border width.</param>
/// <param name="Children">The items inside the legend group: the entry group, then the title.</param>
/// <param name="Frame">The paint of the box.</param>
/// <param name="ItemCount">The number of scene items of the legend, the legend group itself included.</param>
internal sealed record LegendResult(
    LegendKind Kind,
    string Orient,
    double? Offset,
    double X,
    double Y,
    double Width,
    double Height,
    SceneBounds Bounds,
    IReadOnlyList<SceneItem> Children,
    LegendFrame Frame,
    int ItemCount)
{
    /// <summary>
    ///     Creates the legend group at a position.
    /// </summary>
    /// <param name="x">The x position in the coordinates of the view.</param>
    /// <param name="y">The y position in the coordinates of the view.</param>
    /// <returns>The group (role <c>legend</c>).</returns>
    public SceneGroup CreateGroup(double x, double y) => new("legend", x, y)
    {
        Width = Width,
        Height = Height,
        Fill = Frame.Fill,
        Stroke = Frame.Stroke,
        StrokeWidth = Frame.StrokeWidth,
        StrokeDash = Frame.StrokeDash,
        CornerRadius = Frame.CornerRadius,
        Children = Children,
    };
}

/// <summary>
///     Builds the legend guide of a channel or of several channels that share a scale: the entry symbols or the color gradient, their
///     labels and the title, and measures the box they need.
/// </summary>
/// <remarks>
///     <para>
///         <b>Where the rules come from.</b> The properties and their defaults are those of Vega-Lite 6 (<c>legend/parse.ts</c>,
///         <c>legend/properties.ts</c>, <c>legend/encode.ts</c>) over Vega's legend parsers (<c>legend.js</c>, <c>legend-symbol-groups.js</c>,
///         <c>legend-gradient.js</c>, <c>legend-gradient-discrete.js</c>, <c>legend-gradient-labels.js</c>, <c>legend-title.js</c>), the entry
///         generator (<c>LegendEntries</c>) and the legend layout of <c>ViewLayout</c>. A property is looked up in the <c>legend</c> object of
///         the channel (the first channel of a merged legend that has it), then <c>config.legend</c> with the Vega-Lite and Vega defaults,
///         then the <c>guide-label</c> and <c>guide-title</c> styles.
///     </para>
///     <para>
///         <b>Symbol legends.</b> The entries are the domain of a discrete scale, the ticks of a continuous one (5 by default), or the
///         classes of a discretizing one (labeled with ranges such as <c>0 – 10</c>, <c>&lt; 10</c> and <c>≥ 20</c>); more than
///         <c>symbolLimit</c> entries (30) end with an entry <c>…N entries</c>. An entry is a symbol (the scaled fill, stroke, shape, size,
///         opacity and dash of its value, the fixed paints of the mark otherwise, or the base paints of the legend configuration) and a label,
///         laid out in a grid. <b>Gradient legends.</b> A continuous color scale draws a bar (16 by 200 pixels, shorter for a small view) filled with the
///         color ramp, sampled at the ends of the domain and the 15 ticks between them as Vega's <c>scaleGradient</c> does, with labels at the
///         ticks; a discretizing scale draws one band per class. <b>Layout.</b> The content sits inside the padding below the title (the title
///         is above, anchored at its start), and the box is as large as the content and the padding, rounded up.
///     </para>
///     <para>
///         <b>Limits.</b> The number of entries is bounded by <see cref="VegaLiteLimits.MaxScaleDomainSize"/> (a domain, an explicit
///         <c>values</c> list) and <see cref="VegaLiteLimits.MaxTicks"/> (the ticks of a continuous scale) before anything is created, and by
///         <c>symbolLimit</c> after; labels are only made for the entries that are kept, and each label is measured against
///         <see cref="VegaLiteLimits.MaxStringLength"/> and charged to a private evaluation (<see cref="VegaLiteLimits.MaxEvaluationSteps"/>)
///         before it is measured. Every scene item is charged to the budget before it is created; loops poll the cancellation token. A
///         failure is a located <see cref="VegaLiteRenderException"/> whose message names the limit. An invalid <c>format</c> is a
///         <see cref="VegaLiteSpecException"/>. The builder is stateless between calls and safe for concurrent use.
///     </para>
///     <para>
///         <b>Not drawn.</b> <c>titleOrient</c> other than <c>top</c>, <c>clipHeight</c>, <c>encode</c>, <c>labelExpr</c> and custom format types
///         (the feature catalog reports them); the symbols of the <c>arrow</c>, <c>wedge</c> and <c>triangle</c> shapes use the box of their path.
///     </para>
/// </remarks>
internal sealed partial class LegendBuilder
{
    /// <summary>The number of entries between two cancellation checks.</summary>
    private const int CancellationInterval = 1024;

    /// <summary>The text measurer.</summary>
    private readonly TextMeasurer _measurer;

    /// <summary>The limits.</summary>
    private readonly VegaLiteLimits _limits;

    /// <summary>The source name for the locations of failures.</summary>
    private readonly string? _sourceName;

    /// <summary>
    ///     Initializes a legend builder.
    /// </summary>
    /// <param name="measurer">The text measurer that sizes labels and titles.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="sourceName">The source name for the locations of failures, or null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="measurer"/> is null.</exception>
    public LegendBuilder(TextMeasurer measurer, VegaLiteLimits? limits = null, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(measurer);
        _measurer = measurer;
        _limits = limits ?? VegaLiteLimits.Default;
        _sourceName = sourceName;
    }

    /// <summary>
    ///     Builds one legend.
    /// </summary>
    /// <param name="spec">The planned legend.</param>
    /// <param name="dataWidth">The width of the data rectangle (the length of a horizontal gradient).</param>
    /// <param name="dataHeight">The height of the data rectangle (the length of a vertical gradient).</param>
    /// <param name="budget">The scene budget; every item is charged before it is created.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>The legend relative to its own origin.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="spec"/> or <paramref name="budget"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the channel, when the legend has more entries than a limit allows or a label is too long.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown, with the location of the property, when <c>format</c> is not a d3 format specifier.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public LegendResult Build(LegendSpec spec, double dataWidth, double dataHeight, SceneBudget budget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(budget);
        cancellationToken.ThrowIfCancellationRequested();
        var context = new Context(spec, budget, cancellationToken, new ExpressionFunctionContext(_limits, cancellationToken), dataWidth, dataHeight, _sourceName);
        var s = spec.Settings;
        var location = spec.Location;
        var padding = s.Number("padding") ?? 0;

        // The legend group, the title and the entry group are charged before they are created, then the content
        budget.Add(1, location);
        var titleLines = spec.Title is { Count: > 0 } lines ? lines : null;
        if (titleLines is not null)
        {
            budget.Add(1, location);
        }

        budget.Add(1, location);
        var content = spec.Kind switch
        {
            LegendKind.Gradient => BuildGradient(context),
            LegendKind.DiscreteGradient => BuildDiscreteGradient(context),
            _ => BuildSymbols(context),
        };

        // Vega's legendGroupLayout: the entries sit inside the padding, below the title
        double entryX = padding;
        double entryY = padding;
        SceneText? title = null;
        var titleBounds = SceneBounds.Empty;
        if (titleLines is not null)
        {
            var anchor = s.Text("titleAnchor") ?? "start";
            var probe = MakeTitle(context, titleLines, 0, 0, anchor);
            var probeBounds = _measurer.GetBounds(probe);
            entryY += probeBounds.Height + (s.Number("titlePadding") ?? 0);

            // A centered or end-anchored title is positioned against the width of the entries (the bar alone for a horizontal gradient)
            var reference = spec.Kind != LegendKind.Symbol && !spec.Vertical ? content.MarkBounds : content.Bounds;
            var width = (reference.IsEmpty ? 0 : reference.X2) + entryX - padding;
            var shift = anchor switch
            {
                "end" => TextMeasurer.RoundHalfUp(width),
                "middle" => TextMeasurer.RoundHalfUp(0.5 * width),
                _ => 0.0,
            };
            var titleX = padding + shift;
            var placed = probeBounds.Translate(titleX, padding);

            // A title that reaches past the left padding pushes everything to the right
            var overhang = TextMeasurer.RoundHalfUp(placed.X1 - padding);
            if (overhang < 0)
            {
                entryX -= overhang;
                titleX -= overhang;
                placed = placed.Translate(-overhang, 0);
            }

            title = MakeTitle(context, titleLines, titleX, padding, anchor);
            titleBounds = placed;
        }

        var entryBounds = content.Bounds.IsEmpty ? SceneBounds.Empty : content.Bounds.Translate(entryX, entryY);
        var all = entryBounds.Union(titleBounds);
        double right = padding;
        double bottom = padding;
        if (!all.IsEmpty)
        {
            right = all.X2;
            bottom = all.Y2;
        }

        var boxWidth = Math.Ceiling(right - padding + (2 * padding));
        var boxHeight = Math.Ceiling(bottom - padding + (2 * padding));
        var entryGroup = new SceneGroup("legend-entry", entryX, entryY) { Children = content.Items };
        List<SceneItem> children = [entryGroup];
        if (title is not null)
        {
            children.Add(title);
        }

        var frame = new LegendFrame(
            s.Text("fillColor"),
            s.Text("strokeColor"),
            s.Number("strokeWidth"),
            s.NumberArray("strokeDash"),
            s.Number("cornerRadius") ?? 0);
        var reach = frame.Stroke is null ? 0 : (frame.StrokeWidth ?? 1) / 2;
        return new LegendResult(
            spec.Kind,
            spec.Orient,
            s.Number("offset"),
            s.Number("legendX") ?? 0,
            s.Number("legendY") ?? 0,
            boxWidth,
            boxHeight,
            new SceneBounds(-reach, -reach, boxWidth + reach, boxHeight + reach),
            children,
            frame,
            2 + (title is null ? 0 : 1) + content.Items.Count);
    }

    /// <summary>
    ///     Builds the title text of a legend.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="lines">The title lines.</param>
    /// <param name="x">The x position.</param>
    /// <param name="y">The y position.</param>
    /// <param name="anchor">The anchor: <c>start</c>, <c>middle</c> or <c>end</c>.</param>
    /// <returns>The title (role <c>legend-title</c>).</returns>
    private static SceneText MakeTitle(Context c, IReadOnlyList<string> lines, double x, double y, string anchor)
    {
        var s = c.S;
        var align = anchor switch
        {
            "end" => "right",
            "middle" => "center",
            _ => "left",
        };
        return new SceneText("legend-title", x, y, lines)
        {
            Fill = s.Text("titleColor") ?? s.StyleText("guide-title", "fill"),
            FillOpacity = s.Number("titleOpacity") ?? 1,
            FontFamily = s.Text("titleFont") ?? s.StyleText("guide-title", "font"),
            FontSize = s.Number("titleFontSize") ?? s.StyleNumber("guide-title", "fontSize") ?? 11,
            FontWeight = s.Text("titleFontWeight") ?? s.StyleText("guide-title", "fontWeight"),
            FontStyle = s.Text("titleFontStyle") ?? s.StyleText("guide-title", "fontStyle"),
            LineHeight = s.Number("titleLineHeight"),
            Limit = s.Number("titleLimit") ?? 0,
            Align = s.Text("titleAlign") ?? align,
            Baseline = s.Text("titleBaseline") ?? "top",
        };
    }

    /// <summary>
    ///     Gets the font size of the labels: the legend's <c>labelFontSize</c>, else that of the <c>guide-label</c> style.
    /// </summary>
    /// <param name="s">The settings.</param>
    /// <returns>The font size.</returns>
    private static double LabelFontSize(LegendSettings s) => s.Number("labelFontSize") ?? s.StyleNumber("guide-label", "fontSize") ?? 10;

    /// <summary>
    ///     Builds a label with the label font properties of the legend.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="x">The x position.</param>
    /// <param name="y">The y position.</param>
    /// <param name="text">The label text.</param>
    /// <param name="align">The horizontal alignment.</param>
    /// <param name="baseline">The vertical alignment.</param>
    /// <param name="limit">The width limit; 0 for none.</param>
    /// <returns>The label (role <c>legend-label</c>).</returns>
    private static SceneText MakeLabel(Context c, double x, double y, string text, string align, string baseline, double limit)
    {
        var s = c.S;
        return new SceneText("legend-label", x, y, [text])
        {
            Fill = s.Text("labelColor") ?? s.StyleText("guide-label", "fill"),
            FillOpacity = s.Number("labelOpacity") ?? 1,
            FontFamily = s.Text("labelFont") ?? s.StyleText("guide-label", "font"),
            FontSize = LabelFontSize(s),
            FontWeight = s.Text("labelFontWeight") ?? s.StyleText("guide-label", "fontWeight"),
            FontStyle = s.Text("labelFontStyle") ?? s.StyleText("guide-label", "fontStyle"),
            Limit = limit,
            Align = align,
            Baseline = baseline,
        };
    }

    /// <summary>
    ///     Converts a domain value to label text, bounded by <see cref="VegaLiteLimits.MaxStringLength"/> and charged to the evaluation of the build.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the legend, when the text is too long or the evaluation budget is exhausted.</exception>
    private string TextOf(Context c, DataValue value)
    {
        try
        {
            var text = ExpressionEvaluator.ToJsString(value, _limits.MaxStringLength);
            c.Evaluation.ChargeSize(text.Length);
            return text;
        }
        catch (ExpressionRuntimeException ex)
        {
            throw new VegaLiteRenderException(ex.Message, c.Spec.Location);
        }
    }

    /// <summary>
    ///     Reads the explicit <c>values</c> of the legend.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <returns>The values, or null when the legend has none.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the property, when there are more values than <see cref="VegaLiteLimits.MaxScaleDomainSize"/>.</exception>
    private List<DataValue>? ExplicitValues(Context c)
    {
        if (c.S.ExplicitNode("values") is not { IsArray: true } node)
        {
            return null;
        }

        if (node.Items.Count > _limits.MaxScaleDomainSize)
        {
            throw new VegaLiteRenderException(
                $"The legend values have {node.Items.Count} entries, more than the limit of {_limits.MaxScaleDomainSize} (VegaLiteLimits.MaxScaleDomainSize).",
                node.GetLocation(_sourceName));
        }

        var values = new List<DataValue>(node.Items.Count);
        foreach (var item in node.Items)
        {
            c.Token.ThrowIfCancellationRequested();
            var value = item.Kind switch
            {
                SpecNodeKind.Number => DataValue.FromNumber(item.NumberValue),
                SpecNodeKind.String => DataValue.FromString(item.StringValue),
                SpecNodeKind.Boolean => DataValue.FromBoolean(item.BoolValue),
                SpecNodeKind.Null => DataValue.Null,
                _ => (DataValue?)null,
            };
            if (value is { } v)
            {
                values.Add(v);
            }
        }

        return values;
    }

    /// <summary>
    ///     Creates the number formatter of a format specifier of the legend.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="create">Creates the formatter.</param>
    /// <returns>The formatter.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown, with the location of the <c>format</c>, when the specifier is not valid.</exception>
    private NumberFormat Formatter(Context c, Func<NumberFormat> create)
    {
        try
        {
            return create();
        }
        catch (FormatException ex)
        {
            var location = c.S.ExplicitNode("format")?.GetLocation(_sourceName) ?? c.Spec.Location;
            throw new VegaLiteSpecException($"The legend format is not a valid d3 format specifier: {ex.Message}", location, ex);
        }
    }

    /// <summary>
    ///     Gets the <c>format</c> specifier of the legend.
    /// </summary>
    /// <param name="s">The settings.</param>
    /// <returns>The specifier, or null when the legend has none (or it is not a text).</returns>
    private static string? FormatSpecifier(LegendSettings s) => s.Effective("format")?.AsString();

    /// <summary>
    ///     The state of building one legend.
    /// </summary>
    private sealed class Context
    {
        /// <summary>
        ///     Initializes the state.
        /// </summary>
        /// <param name="spec">The planned legend.</param>
        /// <param name="budget">The scene budget.</param>
        /// <param name="token">The cancellation token.</param>
        /// <param name="evaluation">The evaluation that label text is charged to.</param>
        /// <param name="dataWidth">The width of the data rectangle.</param>
        /// <param name="dataHeight">The height of the data rectangle.</param>
        /// <param name="sourceName">The source name.</param>
        public Context(LegendSpec spec, SceneBudget budget, CancellationToken token, ExpressionFunctionContext evaluation, double dataWidth, double dataHeight, string? sourceName)
        {
            Spec = spec;
            Budget = budget;
            Token = token;
            Evaluation = evaluation;
            DataWidth = dataWidth;
            DataHeight = dataHeight;
            SourceName = sourceName;
        }

        /// <summary>Gets the planned legend.</summary>
        public LegendSpec Spec { get; }

        /// <summary>Gets the settings of the legend.</summary>
        public LegendSettings S => Spec.Settings;

        /// <summary>Gets the scene budget.</summary>
        public SceneBudget Budget { get; }

        /// <summary>Gets the cancellation token.</summary>
        public CancellationToken Token { get; }

        /// <summary>Gets the evaluation that label text is charged to.</summary>
        public ExpressionFunctionContext Evaluation { get; }

        /// <summary>Gets the width of the data rectangle.</summary>
        public double DataWidth { get; }

        /// <summary>Gets the height of the data rectangle.</summary>
        public double DataHeight { get; }

        /// <summary>Gets the source name.</summary>
        public string? SourceName { get; }
    }

    /// <summary>
    ///     The parts of a legend inside its entry group.
    /// </summary>
    /// <param name="Items">The scene items in drawing order, relative to the entry group.</param>
    /// <param name="Bounds">The bounds of everything, relative to the entry group.</param>
    /// <param name="MarkBounds">The bounds of the first mark (the gradient bar, the bands, or everything for a symbol legend).</param>
    private sealed record EntryContent(List<SceneItem> Items, SceneBounds Bounds, SceneBounds MarkBounds);
}
