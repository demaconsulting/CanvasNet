// Portions derived from Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause): gradient legends; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <content>
///     The gradient legends: the color ramp of a continuous color scale and the bands of a discretizing one.
/// </content>
internal sealed partial class LegendBuilder
{
    /// <summary>The number of ticks that the gradient samples between the ends of the domain (Vega's <c>scaleGradient</c> default).</summary>
    private const int GradientTickCount = 15;

    /// <summary>
    ///     Builds the content of a gradient legend: a bar filled with the color ramp and the labels at the ticks of the scale.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <returns>The bar and labels, relative to the entry group.</returns>
    private EntryContent BuildGradient(Context c)
    {
        var s = c.S;
        var spec = c.Spec;
        var model = spec.Canonical.Model;
        var vertical = spec.Vertical;
        var thickness = s.Number("gradientThickness") ?? 16;
        var length = GradientLength(c);
        var start = model.DomainStart;
        var end = model.DomainEnd;

        // The default tick count follows the length of the bar; labels are the ticks, or the two ends when there are fewer than three
        var explicitValues = ExplicitValues(c);
        var requestedCount = s.Number("tickCount") ?? Math.Max(2, 2 * Math.Floor(length / 100));
        var temporal = IsTemporal(c);
        var tickSet = temporal ? TimeTickSet(c, model, requestedCount) : ContinuousTicks(c, model, requestedCount);
        var specifier = FormatSpecifier(s);
        var format = temporal ? TimeLabelFunction(c) : ContinuousFormat(c, model, tickSet, specifier, explicitValues is not null);
        List<DataValue> values = explicitValues ?? [.. tickSet.Values.Select(DataValue.FromNumber)];
        if (explicitValues is null && values.Count < 3 && start.CompareTo(end) != 0)
        {
            values = [DataValue.FromNumber(start), DataValue.FromNumber(end)];
        }

        // The bar: the stops are the ends and the ticks between them, as Vega's scaleGradient samples the scale
        var gradient = Gradient(c, model, vertical);
        var barWidth = vertical ? thickness : length;
        var barHeight = vertical ? length : thickness;
        c.Budget.Add(1, spec.Location);
        var bar = new SceneRect("legend-gradient", 0, 0, barWidth, barHeight)
        {
            FillGradient = gradient,
            Stroke = s.Text("gradientStrokeColor"),
            StrokeWidth = s.Number("gradientStrokeWidth"),
            Opacity = s.Number("gradientOpacity") ?? 1,
        };
        var barBounds = RectBounds(bar);

        // The labels sit outside the bar, at the position of their value; those that overlap are not created and cost nothing
        var offset = s.Explicit("labelOffset")?.AsNumber() ?? s.Number("gradientLabelOffset") ?? 0;
        var limit = s.Explicit("labelLimit")?.AsNumber() ?? s.Number("gradientLabelLimit") ?? 0;
        var candidates = new List<SceneText>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            if (i % CancellationInterval == 0)
            {
                c.Token.ThrowIfCancellationRequested();
            }

            var value = values[i];
            if (!value.IsNumber)
            {
                continue;
            }

            var along = model.Fraction(value.Number);
            var text = format(value.Number);
            c.Evaluation.ChargeSize(text.Length);
            candidates.Add(GradientLabel(c, text, along, vertical, thickness, offset, length, limit));
        }

        return Finish(c, bar, barBounds, candidates, LabelMethod(c));
    }

    /// <summary>
    ///     Builds the content of a discrete gradient legend: one band of color per class of a discretizing scale and the labels at the
    ///     boundaries of the classes.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <returns>The bands and labels, relative to the entry group.</returns>
    private EntryContent BuildDiscreteGradient(Context c)
    {
        var s = c.S;
        var spec = c.Spec;
        var model = spec.Canonical.Model;
        var vertical = spec.Vertical;
        var thickness = s.Number("gradientThickness") ?? 16;
        var length = GradientLength(c);
        var specifier = FormatSpecifier(s);
        var format = ThresholdFormat(c, model, specifier);
        var values = ExplicitValues(c) ?? ThresholdValues(model);
        var fraction = ClassFraction(model);

        // One band per value, charged before the bands are created
        c.Budget.Add(values.Count, spec.Location);
        var items = new List<SceneItem>(values.Count * 2);
        var bounds = SceneBounds.Empty;
        for (var i = 0; i < values.Count; i++)
        {
            if (i % CancellationInterval == 0)
            {
                c.Token.ThrowIfCancellationRequested();
            }

            var along = i == 0 ? 0 : fraction(NumberOrNaN(values[i]));
            var alongEnd = i == values.Count - 1 ? 1 : fraction(NumberOrNaN(values[i + 1]));
            var fill = model.MapText(Mappable(values[i]));
            var (x, y, width, height) = vertical
                ? (0.0, (1 - alongEnd) * length, thickness, (alongEnd - along) * length)
                : (along * length, 0.0, (alongEnd - along) * length, thickness);
            var band = new SceneRect("legend-band", x, y, width, height)
            {
                Fill = fill,
                Stroke = s.Text("gradientStrokeColor"),
                StrokeWidth = s.Number("gradientStrokeWidth"),
                Opacity = s.Number("gradientOpacity") ?? 1,
            };
            items.Add(band);
            bounds = bounds.Union(RectBounds(band));
        }

        // The labels are the boundaries between the classes: the first value (minus infinity) has none
        var offset = s.Explicit("labelOffset")?.AsNumber() ?? s.Number("gradientLabelOffset") ?? 0;
        var limit = s.Explicit("labelLimit")?.AsNumber() ?? s.Number("gradientLabelLimit") ?? 0;
        var candidates = new List<SceneText>(values.Count);
        for (var i = 1; i < values.Count; i++)
        {
            if (i % CancellationInterval == 0)
            {
                c.Token.ThrowIfCancellationRequested();
            }

            if (!values[i].IsNumber)
            {
                continue;
            }

            var text = format(values[i].Number);
            c.Evaluation.ChargeSize(text.Length);
            candidates.Add(GradientLabel(c, text, fraction(values[i].Number), vertical, thickness, offset, length, limit));
        }

        var shown = ShownLabels(c, candidates, LabelMethod(c));
        c.Budget.Add(shown.Count, spec.Location);
        foreach (var label in shown)
        {
            items.Add(label);
            bounds = bounds.Union(_measurer.GetBounds(label));
        }

        return new EntryContent(items, bounds, bounds.IsEmpty ? SceneBounds.Empty : RectUnion(items));
    }

    /// <summary>
    ///     Finishes a gradient legend: removes the overlapping labels, charges the ones that stay and gathers the parts.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="bar">The gradient bar.</param>
    /// <param name="barBounds">The bounds of the bar.</param>
    /// <param name="candidates">The labels before the overlap removal.</param>
    /// <param name="method">The overlap method, or null.</param>
    /// <returns>The content.</returns>
    private EntryContent Finish(Context c, SceneRect bar, SceneBounds barBounds, List<SceneText> candidates, string? method)
    {
        var shown = ShownLabels(c, candidates, method);
        c.Budget.Add(shown.Count, c.Spec.Location);
        var items = new List<SceneItem>(shown.Count + 1) { bar };
        var bounds = barBounds;
        foreach (var label in shown)
        {
            items.Add(label);
            bounds = bounds.Union(_measurer.GetBounds(label));
        }

        return new EntryContent(items, bounds, barBounds);
    }

    /// <summary>
    ///     Removes the labels that the overlap method hides.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="candidates">The labels in guide order.</param>
    /// <param name="method">The method, or null for none.</param>
    /// <returns>The labels that stay.</returns>
    private List<SceneText> ShownLabels(Context c, List<SceneText> candidates, string? method)
    {
        var withText = candidates.Where(l => l.Text.Length > 0).ToList();
        var hidden = LabelOverlap.Hidden(withText, _measurer, method, c.S.Number("labelSeparation") ?? 0, c.Token);
        return [.. candidates.Where(l => !hidden.Contains(l))];
    }

    /// <summary>
    ///     Gets the overlap method of the labels: the one the legend names, Vega-Lite's greedy method for quantile and threshold scales,
    ///     else the configuration's (parity).
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <returns>The method, or null for none.</returns>
    private static string? LabelMethod(Context c)
    {
        if (c.S.Explicit("labelOverlap") is { } node)
        {
            return LabelOverlap.FromSetting(node);
        }

        if (c.Spec.Canonical.Model.Kind is ScaleKind.Quantile or ScaleKind.Threshold)
        {
            return LabelOverlap.Greedy;
        }

        return LabelOverlap.FromSetting(c.S.Effective("labelOverlap"));
    }

    /// <summary>
    ///     Gets the length of the gradient bar: <c>gradientLength</c>, else Vega-Lite's size from the data rectangle for a continuous
    ///     scale, else the configured length.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <returns>The length in pixels.</returns>
    private static double GradientLength(Context c)
    {
        var s = c.S;
        if (s.Explicit("gradientLength")?.AsNumber() is { } explicitLength)
        {
            return explicitLength;
        }

        if (c.Spec.Canonical.Model.Kind is ScaleKind.Sequential or ScaleKind.Diverging or ScaleKind.Linear)
        {
            double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
            if (c.Spec.Vertical)
            {
                return Clamp(c.DataHeight, s.Number("gradientVerticalMinLength") ?? 64, s.Number("gradientVerticalMaxLength") ?? 200);
            }

            var minimum = s.Number("gradientHorizontalMinLength") ?? 100;
            return c.Spec.Orient is "top" or "bottom"
                ? Clamp(c.DataWidth, minimum, s.Number("gradientHorizontalMaxLength") ?? 200)
                : minimum;
        }

        return s.Number("gradientLength") ?? 200;
    }

    /// <summary>
    ///     Gets the position function of the classes of a discretizing scale (Vega's <c>labelFraction</c>): linear over the domain; a
    ///     threshold scale is widened by one class at each end.
    /// </summary>
    /// <param name="model">The scale model.</param>
    /// <returns>The function from a value to its position from 0 to 1.</returns>
    private static Func<double, double> ClassFraction(ScaleModel model)
    {
        var lo = model.DomainStart;
        var hi = model.DomainEnd;
        if (model.Kind == ScaleKind.Threshold)
        {
            var count = model.Thresholds.Count - 1;
            var adjust = count > 0 ? (hi - lo) / count : 0.1;
            lo -= adjust;
            hi += adjust;
        }

        var span = hi - lo;
        return value => (value - lo) / span;
    }

    /// <summary>
    ///     Builds one label of a gradient legend.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="text">The text.</param>
    /// <param name="along">The position along the bar from 0 to 1.</param>
    /// <param name="vertical">Whether the bar is vertical.</param>
    /// <param name="thickness">The thickness of the bar.</param>
    /// <param name="offset">The distance between the bar and the label.</param>
    /// <param name="length">The length of the bar.</param>
    /// <param name="limit">The width limit; 0 for none.</param>
    /// <returns>The label.</returns>
    private static SceneText GradientLabel(Context c, string text, double along, bool vertical, double thickness, double offset, double length, double limit)
    {
        if (vertical)
        {
            return MakeLabel(c, thickness + offset, (1 - along) * length, text, "left", Pick(along, "bottom", "top", "middle"), limit);
        }

        return MakeLabel(c, along * length, thickness + offset, text, Pick(along, "left", "right", "center"), "top", limit);
    }

    /// <summary>
    ///     Picks the alignment of a gradient label from its position: the first for a label at the start of the bar, the second at its end.
    /// </summary>
    /// <param name="along">The position along the bar from 0 to 1.</param>
    /// <param name="atStart">The value at or before the start.</param>
    /// <param name="atEnd">The value at or after the end.</param>
    /// <param name="between">The value in between.</param>
    /// <returns>The chosen value.</returns>
    private static string Pick(double along, string atStart, string atEnd, string between)
    {
        if (along <= 0)
        {
            return atStart;
        }

        return along >= 1 ? atEnd : between;
    }

    /// <summary>
    ///     Gets the number of a value, or NaN for a value that is not a number.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The number.</returns>
    private static double NumberOrNaN(DataValue value) => value.IsNumber ? value.Number : double.NaN;

    /// <summary>
    ///     Creates the gradient that fills the bar: the stops are the ends of the domain and the ticks between them.
    /// </summary>
    /// <remarks>
    ///     This is Vega's <c>scaleGradient</c>: the stops are the scale's ticks for a count of 15, with the two ends of the domain added
    ///     when the ticks do not reach them; the offset of a stop is its position in the domain and its color the scale's color there. A
    ///     domain without span is replaced by the interval from 0 to 1 with the same interpolator.
    /// </remarks>
    /// <param name="c">The build state.</param>
    /// <param name="model">The sequential or diverging scale.</param>
    /// <param name="vertical">Whether the bar is vertical (the gradient then runs from the bottom up).</param>
    /// <returns>The gradient.</returns>
    private SceneGradient Gradient(Context c, ScaleModel model, bool vertical)
    {
        var min = model.DomainStart;
        var max = model.DomainEnd;
        Func<double, double> fraction;
        Func<double, string?> color;
        var spanOfDomain = max - min;
        var expanded = spanOfDomain.CompareTo(0) == 0 || !double.IsFinite(spanOfDomain);
        if (expanded)
        {
            // Vega expands a domain without span to 0 to 1 with the interpolator of the scale
            min = 0;
            max = 1;
            fraction = v => v;
            color = v => model.Interpolator?.Invoke(v).ToCss();
        }
        else
        {
            // A log, pow or symlog scale places a stop along the transformed domain
            fraction = model.Fraction;
            color = v => model.MapColor(v)?.ToCss();
        }

        List<double> stops;
        try
        {
            if (expanded)
            {
                stops = [.. Ticks.GetTicks(min, max, GradientTickCount, _limits.MaxTicks)];
            }
            else if (IsTemporal(c))
            {
                // The color of a temporal field is sampled at the time ticks of its domain, as Vega samples a time scale
                stops = [.. TimeTicksOf(c, model, new TimeTickRequest(GradientTickCount))];
            }
            else
            {
                stops = [.. Ticks.ModelTicks(model, GradientTickCount, _limits.MaxTicks, c.Token)];
            }
        }
        catch (VegaLiteRenderException ex) when (!ex.Location.IsKnown)
        {
            throw new VegaLiteRenderException(ex.Message, c.Spec.Location);
        }

        if (stops.Count == 0 || stops[0].CompareTo(min) != 0)
        {
            stops.Insert(0, min);
        }

        if (stops[^1].CompareTo(max) != 0)
        {
            stops.Add(max);
        }

        var list = new List<SceneGradientStop>(stops.Count);
        foreach (var stop in stops)
        {
            if (color(stop) is { } css)
            {
                list.Add(new SceneGradientStop(fraction(stop), css));
            }
        }

        return vertical ? SceneGradient.Vertical(list) : SceneGradient.Horizontal(list);
    }

    /// <summary>
    ///     Gets the bounding box of a rectangle: its box grown by half the stroke width when it has a stroke (Vega's <c>boundStroke</c>).
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <returns>The box.</returns>
    private static SceneBounds RectBounds(SceneRect rect)
    {
        double reach = 0;
        if (!string.IsNullOrEmpty(rect.Stroke) && rect.Opacity != 0 && rect.StrokeOpacity != 0)
        {
            reach = (rect.StrokeWidth ?? 1) / 2;
        }

        return new SceneBounds(rect.X - reach, rect.Y - reach, rect.X + rect.Width + reach, rect.Y + rect.Height + reach);
    }

    /// <summary>
    ///     Gets the union of the bounds of the rectangles among some items.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>The union of the bounds of the rectangles.</returns>
    private static SceneBounds RectUnion(List<SceneItem> items)
    {
        var bounds = SceneBounds.Empty;
        foreach (var item in items.OfType<SceneRect>())
        {
            bounds = bounds.Union(RectBounds(item));
        }

        return bounds;
    }
}
