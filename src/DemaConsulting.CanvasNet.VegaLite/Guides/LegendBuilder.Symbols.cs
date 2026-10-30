// Portions derived from Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause): legend entries and symbols; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <content>
///     The symbol legend: the entry values and labels, the symbols and their grid.
/// </content>
internal sealed partial class LegendBuilder
{
    /// <summary>Half of the square root of three, the height factor of the triangle symbols.</summary>
    private static readonly double HalfSqrt3 = Math.Sqrt(3) / 2;

    /// <summary>The tangent of 30 degrees, which offsets the triangle and wedge symbols.</summary>
    private static readonly double Tan30 = Math.Tan(Math.PI / 6);

    /// <summary>
    ///     What the entries of a legend show: the values, and how a value becomes a label.
    /// </summary>
    /// <param name="Values">The values in entry order.</param>
    /// <param name="Range">Whether the labels are ranges (<c>10 – 20</c>) between a value and the next one.</param>
    /// <param name="Max">The upper end of the last range.</param>
    /// <param name="Number">Formats a number, or null to use the text of the value.</param>
    private sealed record ValueSet(List<DataValue> Values, bool Range, double Max, Func<double, string>? Number);

    /// <summary>
    ///     The look of one symbol of a legend.
    /// </summary>
    /// <param name="Shape">The shape name.</param>
    /// <param name="Size">The area of the symbol.</param>
    /// <param name="Fill">The fill, or null.</param>
    /// <param name="Stroke">The stroke, or null.</param>
    /// <param name="StrokeWidth">The stroke width.</param>
    /// <param name="Opacity">The opacity.</param>
    /// <param name="FillOpacity">The fill opacity.</param>
    /// <param name="StrokeOpacity">The stroke opacity.</param>
    /// <param name="Dash">The dash pattern, or null.</param>
    private sealed record SymbolLook(string Shape, double Size, string? Fill, string? Stroke, double StrokeWidth, double Opacity, double FillOpacity, double StrokeOpacity, IReadOnlyList<double>? Dash);

    /// <summary>
    ///     Gets the values of the entries of a legend and the way they are formatted.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <returns>The values.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the values are more than a limit allows.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown when the <c>format</c> is not a format specifier.</exception>
    private ValueSet DescribeValues(Context c)
    {
        var s = c.S;
        var model = c.Spec.Canonical.Model;
        var specifier = FormatSpecifier(s);
        var explicitValues = ExplicitValues(c);
        switch (model.Kind)
        {
            case ScaleKind.Linear or ScaleKind.Sequential or ScaleKind.Diverging:
                var tickSet = ContinuousTicks(c, model, s.Number("tickCount") ?? 5);

                // The entries of a symbol legend are all labeled, whatever the scale: d3's log filter only picks the ticks
                var format = ContinuousFormat(c, model, tickSet, specifier, true);
                return new ValueSet(explicitValues ?? [.. tickSet.Values.Select(DataValue.FromNumber)], false, double.PositiveInfinity, format);
            case ScaleKind.Quantize or ScaleKind.Quantile or ScaleKind.Threshold:
                return new ValueSet(explicitValues ?? ThresholdValues(model), true, double.PositiveInfinity, ThresholdFormat(c, model, specifier));
            case ScaleKind.BinOrdinal:
                var edges = model.Thresholds;
                var bins = explicitValues ?? [.. edges.Take(Math.Max(0, edges.Count - 1)).Select(DataValue.FromNumber)];
                return new ValueSet(bins, true, edges.Count > 0 ? edges[^1] : double.PositiveInfinity, PointFormat(c, specifier));
            default:
                return new ValueSet(explicitValues ?? [.. model.Domain], false, double.PositiveInfinity, specifier is null ? null : PointFormat(c, specifier));
        }
    }

    /// <summary>
    ///     Gets the values of the entries of a discretizing scale: minus infinity and the thresholds.
    /// </summary>
    /// <param name="model">The scale model.</param>
    /// <returns>The values.</returns>
    private static List<DataValue> ThresholdValues(ScaleModel model)
    {
        var values = new List<DataValue>(model.Thresholds.Count + 1) { DataValue.FromNumber(double.NegativeInfinity) };
        values.AddRange(model.Thresholds.Select(DataValue.FromNumber));
        return values;
    }

    /// <summary>
    ///     Creates the formatter of a number that is not on a continuous scale: the format specifier, else the JavaScript text.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="specifier">The format specifier, or null.</param>
    /// <returns>The formatter.</returns>
    private Func<double, string> PointFormat(Context c, string? specifier) =>
        specifier is null ? JsNumber.ToString : Formatter(c, () => NumberFormat.Create(specifier)).Format;

    /// <summary>
    ///     Creates the formatter of the thresholds of a discretizing scale (Vega's <c>thresholdFormat</c>): the span is the smallest
    ///     difference between two thresholds (quantile) or the width of the domain (quantize), with the precision of ten ticks per class.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="model">The scale model.</param>
    /// <param name="specifier">The format specifier, or null.</param>
    /// <returns>The formatter.</returns>
    private Func<double, string> ThresholdFormat(Context c, ScaleModel model, string? specifier)
    {
        if (model.Kind == ScaleKind.Threshold)
        {
            return PointFormat(c, specifier);
        }

        double[] edges = model.Kind == ScaleKind.Quantize ? [model.DomainStart, model.DomainEnd] : [.. model.Thresholds];
        var span = 1.0;
        if (edges.Length > 1)
        {
            span = edges[1] - edges[0];
        }
        else if (edges.Length == 1)
        {
            span = edges[0];
        }
        for (var i = 1; i < edges.Length; i++)
        {
            span = Math.Min(span, edges[i] - edges[i - 1]);
        }

        if (!(span > 0) || !double.IsFinite(span))
        {
            span = 1;
        }

        return Formatter(c, () => NumberFormat.TickFormat(0, span, 30, specifier)).Format;
    }

    /// <summary>
    ///     The ticks of a continuous scale for a legend.
    /// </summary>
    /// <param name="Values">The tick values (for a log scale only those that Vega labels: the filter of d3's log format is applied).</param>
    /// <param name="Count">The tick count after the minimum step.</param>
    /// <param name="LabelTest">The log filter that decides whether a tick of a gradient legend is labeled, or null for a scale that is not logarithmic.</param>
    private sealed record TickSet(double[] Values, double Count, Func<double, bool>? LabelTest);

    /// <summary>
    ///     Gets the ticks of a continuous scale for a tick count, after the minimum step of the legend (Vega's <c>labelValues</c>: for a log
    ///     scale only the ticks that d3's log format labels).
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="model">The scale model.</param>
    /// <param name="requested">The requested tick count.</param>
    /// <returns>The ticks.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the legend, when the ticks are more than <see cref="VegaLiteLimits.MaxTicks"/>.</exception>
    private TickSet ContinuousTicks(Context c, ScaleModel model, double requested)
    {
        var minStep = c.S.ExplicitNode("tickMinStep")?.AsNumber();
        var log = model.Transform.Kind == ScaleTransformKind.Log;
        var count = Ticks.ResolveTickCount(model.DomainStart, model.DomainEnd, requested, minStep, log);
        try
        {
            var ticks = Ticks.ModelTicks(model, count, _limits.MaxTicks, c.Token);
            if (!log)
            {
                return new TickSet(ticks, count, null);
            }

            var test = Ticks.LogLabelTest(model.Transform.Parameter, count, ticks.Length);
            return new TickSet([.. ticks.Where(test)], count, test);
        }
        catch (VegaLiteRenderException ex) when (!ex.Location.IsKnown)
        {
            throw new VegaLiteRenderException(ex.Message, c.Spec.Location);
        }
    }

    /// <summary>
    ///     Creates the label formatter of a continuous scale (Vega's <c>tickFormat</c>): d3's precision from the tick step, or for a log
    ///     scale the variable-precision format, which for a gradient legend leaves out the labels that d3's log rule hides.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="model">The scale model.</param>
    /// <param name="ticks">The ticks.</param>
    /// <param name="specifier">The format specifier, or null.</param>
    /// <param name="noSkip">Whether every label is shown (a symbol legend, and explicit <c>values</c>).</param>
    /// <returns>The formatter.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the specifier is not a format specifier.</exception>
    private Func<double, string> ContinuousFormat(Context c, ScaleModel model, TickSet ticks, string? specifier, bool noSkip)
    {
        if (model.Transform.Kind != ScaleTransformKind.Log)
        {
            return Formatter(c, () => NumberFormat.TickFormat(model.DomainStart, model.DomainEnd, ticks.Count, specifier)).Format;
        }

        var plain = Formatter(c, () => NumberFormat.FormatFloat(specifier));
        return noSkip || ticks.LabelTest is not { } test ? plain : value => test(value) ? plain(value) : string.Empty;
    }

    /// <summary>
    ///     Builds the label of an entry.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="set">The values of the legend.</param>
    /// <param name="items">The entries that are shown.</param>
    /// <param name="index">The position of the entry.</param>
    /// <param name="sliced">Whether the list was cut (the upper end of the last range is then open, as in Vega where the cut list has lost it).</param>
    /// <returns>The label.</returns>
    private string LabelOf(Context c, ValueSet set, List<DataValue> items, int index, bool sliced)
    {
        var value = items[index];
        if (!set.Range)
        {
            return set.Number is { } format && value.IsNumber ? format(value.Number) : TextOf(c, value);
        }

        var format2 = set.Number ?? JsNumber.ToString;
        double upper;
        if (index + 1 < items.Count && items[index + 1].IsNumber)
        {
            upper = items[index + 1].Number;
        }
        else
        {
            upper = sliced ? double.PositiveInfinity : set.Max;
        }

        var lo = value.IsNumber && double.IsFinite(value.Number) ? format2(value.Number) : null;
        var hi = double.IsFinite(upper) ? format2(upper) : null;
        c.Evaluation.ChargeSize((lo?.Length ?? 0) + (hi?.Length ?? 0));
        if (!string.IsNullOrEmpty(lo) && !string.IsNullOrEmpty(hi))
        {
            return lo + " \u2013 " + hi;
        }

        return !string.IsNullOrEmpty(hi) ? "< " + hi : "\u2265 " + (lo ?? "null");
    }

    /// <summary>
    ///     Builds the content of a symbol legend.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <returns>The entries, relative to the entry group.</returns>
    private EntryContent BuildSymbols(Context c)
    {
        var s = c.S;
        var spec = c.Spec;
        var set = DescribeValues(c);
        var values = set.Values;
        var limit = (int)Math.Min(int.MaxValue, Math.Max(0, s.Number("symbolLimit") ?? 0));
        var items = values;
        var sliced = false;
        var ellipsis = false;
        if (limit > 0 && values.Count > limit)
        {
            items = values.GetRange(0, limit - 1);
            sliced = true;
            ellipsis = true;
        }

        // A first value that the scale maps to zero (a size of nothing) is not shown, unless the values are explicit
        if (s.ExplicitNode("values") is null && items.Count > 0 && spec.Canonical.Model.Map(Mappable(items[0])) == 0)
        {
            items = items.GetRange(1, items.Count - 1);
            sliced = true;
        }

        // Labels are made only for the entries that are kept
        var fontSize = LabelFontSize(s);
        var entries = new List<(DataValue Value, string Label)>(items.Count + 1);
        for (var i = 0; i < items.Count; i++)
        {
            if (i % CancellationInterval == 0)
            {
                c.Token.ThrowIfCancellationRequested();
            }

            entries.Add((items[i], LabelOf(c, set, items, i, sliced)));
        }

        var shown = entries.Count;
        if (ellipsis && items.Count < values.Count)
        {
            var remaining = values.Count - items.Count;
            c.Evaluation.ChargeSize(24);
            entries.Add((values[items.Count], "\u2026" + remaining.ToString(System.Globalization.CultureInfo.InvariantCulture) + " entries"));
        }

        // Every item is charged before it is created: a symbol and a label per entry
        c.Budget.Add(2L * entries.Count, spec.Location);

        var looks = new List<SymbolLook>(entries.Count);
        var sizes = new double[entries.Count];
        double offset = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var look = LookOf(c, entries[i].Value);
            looks.Add(look);
            sizes[i] = Math.Max(Math.Ceiling(Math.Sqrt(Math.Max(0, look.Size)) + look.StrokeWidth), fontSize);
            if (i < shown)
            {
                offset = Math.Max(offset, sizes[i]);
            }
        }

        var columns = (int)Math.Min(int.MaxValue, Math.Max(0, s.Number("columns") ?? (spec.Vertical ? 1 : 0)));
        var symbolOffset = s.Number("symbolOffset") ?? 0;
        var labelOffset = s.Number("labelOffset") ?? 0;
        var align = s.Text("labelAlign") ?? "left";
        var baseline = s.Text("labelBaseline") ?? "middle";
        var limitWidth = s.Number("labelLimit") ?? 0;
        var symbols = new List<SceneSymbol>(entries.Count);
        var labels = new List<SceneText>(entries.Count);
        var boxes = new List<SceneBounds>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            if (i % CancellationInterval == 0)
            {
                c.Token.ThrowIfCancellationRequested();
            }

            var reference = columns != 0 ? offset : sizes[i];
            var y = sizes[i] * 0.5;
            var look = looks[i];
            var symbol = new SceneSymbol("legend-symbol", (reference * 0.5) + symbolOffset, y)
            {
                Shape = look.Shape,
                Size = look.Size,
                Fill = look.Fill,
                Stroke = look.Stroke,
                StrokeWidth = look.StrokeWidth,
                Opacity = look.Opacity,
                FillOpacity = look.FillOpacity,
                StrokeOpacity = look.StrokeOpacity,
                StrokeDash = look.Dash,
            };
            var label = MakeLabel(c, reference + symbolOffset + labelOffset, y, entries[i].Label, align, baseline, limitWidth);
            symbols.Add(symbol);
            labels.Add(label);
            boxes.Add(SymbolBounds(symbol).Union(_measurer.GetBounds(label)));
        }

        // The grid: a vertical legend with several columns fills them from top to bottom
        var count = entries.Count;
        var order = new int[count];
        for (var i = 0; i < count; i++)
        {
            order[i] = i;
        }

        if (spec.Vertical && columns > 1)
        {
            var rows = (int)Math.Ceiling(count / (double)columns);
            Array.Sort(order, (a, b) => (a % rows).CompareTo(b % rows) is var byRow and not 0 ? byRow : a.CompareTo(b));
        }

        var slots = order.Select(i => boxes[i]).ToList();
        var grid = GridLayout(
            slots,
            new GridOptions(
                columns,
                ParseAlign(s.Text("gridAlign")),
                ParseAlign(s.Text("gridAlign")),
                s.Number("columnPadding") ?? 0,
                s.Number("rowPadding") ?? 0,
                CenterColumns: false,
                CenterRows: true));
        var children = new List<SceneItem>(2 * count);
        for (var slot = 0; slot < count; slot++)
        {
            var entry = order[slot];
            var dx = grid.Dx[slot];
            var dy = grid.Dy[slot];
            children.Add(MoveSymbol(symbols[entry], dx, dy));
            children.Add(MoveLabel(labels[entry], dx, dy));
        }

        return new EntryContent(children, grid.Bounds, grid.Bounds);
    }

    /// <summary>
    ///     Replaces minus infinity (the first value of a discretizing scale) by the smallest finite number, which the scale models accept and
    ///     which falls in the first class, as Vega's scale gives it.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value to map.</returns>
    private static DataValue Mappable(DataValue value) =>
        value.IsNumber && double.IsNegativeInfinity(value.Number) ? DataValue.FromNumber(double.MinValue) : value;

    /// <summary>
    ///     Reads a grid alignment: <c>all</c>, <c>each</c> or <c>none</c>.
    /// </summary>
    /// <param name="text">The text, or null.</param>
    /// <returns>The alignment; <see cref="GridAlign.Each"/> for anything unknown.</returns>
    private static GridAlign ParseAlign(string? text) => text switch
    {
        "all" => GridAlign.All,
        "none" => GridAlign.None,
        _ => GridAlign.Each,
    };

    /// <summary>
    ///     Copies a symbol shifted by the position its entry got in the grid.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="dx">The horizontal shift.</param>
    /// <param name="dy">The vertical shift.</param>
    /// <returns>The shifted symbol.</returns>
    private static SceneSymbol MoveSymbol(SceneSymbol symbol, double dx, double dy) =>
        new(symbol.Role, symbol.X + dx, symbol.Y + dy)
        {
            Shape = symbol.Shape,
            Size = symbol.Size,
            Angle = symbol.Angle,
            Fill = symbol.Fill,
            Stroke = symbol.Stroke,
            StrokeWidth = symbol.StrokeWidth,
            Opacity = symbol.Opacity,
            FillOpacity = symbol.FillOpacity,
            StrokeOpacity = symbol.StrokeOpacity,
            StrokeDash = symbol.StrokeDash,
            StrokeDashOffset = symbol.StrokeDashOffset,
        };

    /// <summary>
    ///     Copies a label shifted by the position its entry got in the grid.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="dx">The horizontal shift.</param>
    /// <param name="dy">The vertical shift.</param>
    /// <returns>The shifted label.</returns>
    private static SceneText MoveLabel(SceneText label, double dx, double dy) =>
        new(label.Role, label.X + dx, label.Y + dy, label.Lines)
        {
            Fill = label.Fill,
            FillOpacity = label.FillOpacity,
            FontFamily = label.FontFamily,
            FontSize = label.FontSize,
            FontWeight = label.FontWeight,
            FontStyle = label.FontStyle,
            Limit = label.Limit,
            Align = label.Align,
            Baseline = label.Baseline,
            LineHeight = label.LineHeight,
        };

    /// <summary>
    ///     Works out the look of the symbol of an entry (Vega-Lite's <c>legend/encode.ts</c> for symbols over Vega's symbol encoders).
    /// </summary>
    /// <remarks>
    ///     The channels of the legend give their scaled value (a color, a shape, a size, a stroke width, a dash pattern, an opacity). What
    ///     they do not give comes from the fixed paints of the mark; a paint that a field of the encoding drives (and that this legend does not
    ///     explain) and a paint the mark does not have fall back to the legend configuration (<c>symbolBaseFillColor</c> and
    ///     <c>symbolBaseStrokeColor</c> unless the legend has a fill scale).
    /// </remarks>
    /// <param name="c">The build state.</param>
    /// <param name="entryValue">The value of the entry.</param>
    /// <returns>The look.</returns>
    private static SymbolLook LookOf(Context c, DataValue entryValue)
    {
        var value = Mappable(entryValue);
        var s = c.S;
        var spec = c.Spec;
        var mark = spec.Mark;
        var fillMember = spec.Get("fill") ?? (mark.Filled ? spec.Get("color") : null);
        var strokeMember = spec.Get("stroke") ?? (!mark.Filled ? spec.Get("color") : null);

        // The paints: the scale of the legend, else the fixed paint of the mark, else the base paint
        var baseFill = fillMember is null ? s.Text("symbolBaseFillColor") : null;
        var baseStroke = fillMember is null ? s.Text("symbolBaseStrokeColor") : null;
        string? fill;
        if (fillMember is not null)
        {
            fill = fillMember.Model.MapText(value);
        }
        else if (mark.FillIsField)
        {
            fill = s.Text("symbolFillColor") ?? baseFill;
        }
        else
        {
            fill = mark.Fill ?? s.Text("symbolFillColor") ?? baseFill;
        }

        string? stroke;
        if (strokeMember is not null)
        {
            stroke = strokeMember.Model.MapText(value);
        }
        else
        {
            stroke = s.Text("symbolStrokeColor") ?? (mark.StrokeIsField ? null : mark.Stroke) ?? baseStroke;
        }

        var size = s.Number("symbolSize") ?? 100;
        if (spec.Get("size") is { } sizeMember && sizeMember.Model.Map(value) is { } scaled)
        {
            size = scaled;
        }

        var strokeWidth = s.Explicit("symbolStrokeWidth")?.AsNumber() ?? mark.StrokeWidth ?? s.Number("symbolStrokeWidth") ?? 1.5;
        if (spec.Get("strokeWidth") is { } widthMember && widthMember.Model.Map(value) is { } width)
        {
            strokeWidth = width;
        }

        var shape = ShapeDefault(c);
        if (spec.Get("shape") is { } shapeMember && shapeMember.Model.MapText(value) is { } named)
        {
            shape = named;
        }

        var opacity = s.Number("symbolOpacity") ?? mark.Opacity ?? 1;
        if (spec.Get("opacity") is { } opacityMember && opacityMember.Model.Map(value) is { } alpha)
        {
            opacity = alpha;
        }

        var dash = s.NumberArray("symbolDash") ?? mark.StrokeDash;
        if (spec.Get("strokeDash") is { } dashMember)
        {
            var index = dashMember.Model.MapIndex(value);
            if (index >= 0 && index < dashMember.Scale.DashRange.Count)
            {
                dash = dashMember.Scale.DashRange[index];
            }
        }

        return new SymbolLook(shape, size, fill, stroke, strokeWidth, opacity, mark.FillOpacity ?? 1, mark.StrokeOpacity ?? 1, dash);
    }

    /// <summary>
    ///     Gets the shape of the symbols of a legend that has no shape scale: <c>symbolType</c>, the fixed shape of the mark, or the shape
    ///     that stands for the mark (a square for bars, rects and squares, a line for lines, trails and rules, a circle otherwise).
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <returns>The shape name.</returns>
    private static string ShapeDefault(Context c)
    {
        var s = c.S;
        if (s.Explicit("symbolType")?.AsString() is { } explicitType)
        {
            return explicitType;
        }

        if (c.Spec.Mark.Shape is { } fixedShape)
        {
            return fixedShape;
        }

        return c.Spec.Mark.MarkType switch
        {
            "bar" or "rect" or "image" or "square" => "square",
            "line" or "trail" or "rule" => "stroke",
            _ => s.Text("symbolType") ?? "circle",
        };
    }

    /// <summary>
    ///     Gets the bounding box of a symbol the way Vega does: the box of the shape, grown by the stroke (a stroked symbol with a miter
    ///     join reaches up to two stroke widths, because Vega allows for the longest miter).
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The box in the coordinates of the symbol's group.</returns>
    internal static SceneBounds SymbolBounds(SceneSymbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        var r = Math.Sqrt(Math.Max(0, symbol.Size)) / 2;
        double left = r;
        double top = r;
        double right = r;
        double bottom = r;
        switch (symbol.Shape)
        {
            case "triangle-up" or "triangle-down":
                top = bottom = HalfSqrt3 * r;
                break;
            case "triangle-left" or "triangle-right":
                left = right = HalfSqrt3 * r;
                break;
            case "triangle":
                top = (HalfSqrt3 * r) + ((HalfSqrt3 * r) - (r * Tan30));
                bottom = (HalfSqrt3 * r) - ((HalfSqrt3 * r) - (r * Tan30));
                break;
            case "wedge":
                var height = HalfSqrt3 * r;
                var shift = height - (r * Tan30);
                top = height + shift;
                bottom = height - shift;
                left = right = r / 4;
                break;
            case "arrow":
                left = right = r / 2.5;
                break;
            case "stroke":
                top = bottom = 0;
                break;
            default:
                break;
        }

        // boundStroke with a miter: the larger of half the width and two widths (the miter limit of 4 over two)
        double reach = 0;
        if (!string.IsNullOrEmpty(symbol.Stroke) && symbol.Opacity != 0 && symbol.StrokeOpacity != 0)
        {
            var width = symbol.StrokeWidth ?? 1;
            reach = Math.Max(width / 2, 4 * width / 2);
        }

        return new SceneBounds(symbol.X - left - reach, symbol.Y - top - reach, symbol.X + right + reach, symbol.Y + bottom + reach);
    }
}
