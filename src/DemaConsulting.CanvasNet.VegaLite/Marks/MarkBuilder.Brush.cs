// Portions derived from Vega-Lite (BSD-3-Clause): the brush marks of an interval selection (compile/selection/interval.ts); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Marks;

/// <summary>
///     The two groups that draw the brush of an interval selection: the shade behind the marks and the outline in front of them.
/// </summary>
/// <param name="Background">The group of the shade (role <c>selection-brush-background</c>), drawn before the marks.</param>
/// <param name="Outline">The group of the outline (role <c>selection-brush</c>), drawn after the marks.</param>
internal sealed record BrushGroups(SceneGroup Background, SceneGroup Outline);

/// <content>
///     The brush of an interval selection (a partial file of the mark builder unit).
/// </content>
/// <remarks>
///     <para>
///         <b>What is drawn.</b> An interval selection that has an initial value draws two rectangle marks, both clipped to the data rectangle (Vega-Lite's <c>interval.ts</c> <c>marks</c>):
///         <c>name_brush_bg</c> before the marks, with the fill and the fill opacity of the <c>mark</c> of the selection (by default <c>#333</c> and 0.125), and <c>name_brush</c> after them, with a
///         transparent fill and the other properties of the <c>mark</c> as the stroke (by default white; they apply only when the brush has an extent on every axis it is projected over). The
///         rectangle runs from the first to the second end of the first initial value, each end mapped through the scale of its channel (a band scale gives the start of the band of the value,
///         a point scale its position); the ends may come in either order. An axis the selection is not projected over spans the data rectangle. Without an initial value, or when an end has no
///         position, the rectangle has no extent and nothing is drawn, so no item is made.
///     </para>
///     <para>
///         <b>Where.</b> A selection that is resolved globally keeps one interval in one store, the one of the unit that declared it first; the other units draw nothing (Vega-Lite hides their brush
///         with a test of the unit of the store), and no unit of a facet draws a brush, because the name of its unit in the store never equals the name of the cell. A selection bound to the scales of
///         its view draws no brush: it sets the domain of the scales.
///     </para>
/// </remarks>
internal sealed partial class MarkBuilder
{
    /// <summary>The keys of the <c>mark</c> of a brush that are not the stroke of the outline: the others are its stroke (Vega-Lite's <c>{fill, fillOpacity, cursor, ...stroke}</c>).</summary>
    private static readonly HashSet<string> NonStrokeKeys = new(["fill", "fillOpacity", "cursor"], StringComparer.Ordinal);

    /// <summary>
    ///     Builds the brush of an interval selection of a unit.
    /// </summary>
    /// <param name="component">The selection as the unit declares it.</param>
    /// <param name="model">The selections of the specification, which hold the store.</param>
    /// <param name="scales">The scales of the unit.</param>
    /// <param name="unit">The unit view that is drawn (a cell's copy of a facet's template draws no brush of a globally resolved selection).</param>
    /// <param name="width">The width of the data rectangle in pixels.</param>
    /// <param name="height">The height of the data rectangle in pixels.</param>
    /// <returns>The two groups; null when the selection draws no brush in the unit.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the selection, when the chart has more scene items than <see cref="VegaLiteLimits.MaxSceneItems"/>.</exception>
    public BrushGroups? BuildBrush(SelectionComponent component, SelectionModel model, ResolvedScales scales, LayerNode unit, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scales);
        ArgumentNullException.ThrowIfNull(unit);
        if (component.Type != "interval" || component.IsScaleBound || component.IsDropped || component.Init.Count == 0)
        {
            return null;
        }

        // A global selection keeps one interval: only the unit that holds the first tuple of the store shows it, and no cell of a facet does
        if (component.Resolve == "global"
            && (unit.Cell is not null
                || !model.Stores.TryGetValue(component.VariableName + "_store", out var store)
                || store.Entries.Count == 0
                || store.Entries[0].Unit != component.UnitName))
        {
            return null;
        }

        var first = component.Init[0];
        var projectionX = IndexOfChannel(component, "x");
        var projectionY = IndexOfChannel(component, "y");
        if (projectionX < 0 && projectionY < 0)
        {
            return null;
        }

        if (!TryEnds(projectionX, first, scales["x"], width, out var left, out var right)
            || !TryEnds(projectionY, first, scales["y"], height, out var top, out var bottom))
        {
            return null;
        }

        // An axis that the selection is not projected over spans the data rectangle
        var (x0, x1) = projectionX < 0 ? (0d, width) : (left, right);
        var (y0, y1) = projectionY < 0 ? (0d, height) : (top, bottom);
        var rectangleX = Math.Min(x0, x1);
        var rectangleY = Math.Min(y0, y1);
        var rectangleWidth = Math.Abs(x1 - x0);
        var rectangleHeight = Math.Abs(y1 - y0);

        // Without an extent nothing is painted: the shade has no area and the outline is not stroked
        if (!(rectangleWidth > 0) || !(rectangleHeight > 0))
        {
            return null;
        }

        var location = component.Declaration.GetLocation(_sourceName);
        _budget.Add(4, location);
        var mark = component.Mark ?? ConfigNode.EmptyObject;
        var shade = new SceneRect("selection-brush-background-rect", rectangleX, rectangleY, rectangleWidth, rectangleHeight)
        {
            Fill = mark["fill"]?.AsString(),
            FillOpacity = mark["fillOpacity"]?.AsNumber() ?? 1,
        };
        var outline = StrokeOf(new SceneRect("selection-brush-rect", rectangleX, rectangleY, rectangleWidth, rectangleHeight) { Fill = "transparent" }, mark);
        var clip = new SceneClip(0, 0, width, height);
        return new BrushGroups(
            new SceneGroup("selection-brush-background", 0, 0) { Children = [shade], Clip = clip },
            new SceneGroup("selection-brush", 0, 0) { Children = [outline], Clip = clip });
    }

    /// <summary>
    ///     Finds the position of the projected field of a channel in the tuple of a selection.
    /// </summary>
    /// <param name="component">The selection.</param>
    /// <param name="channel">The channel.</param>
    /// <returns>The index; -1 when the selection is not projected over the channel.</returns>
    private static int IndexOfChannel(SelectionComponent component, string channel)
    {
        for (var i = 0; i < component.Projection.Count; i++)
        {
            if (component.Projection[i].Channel == channel)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Maps the two ends of the interval of a channel through its scale (Vega's <c>scale()</c>).
    /// </summary>
    /// <param name="index">The position of the field of the channel in the tuple; negative when the selection is not projected over the channel (the ends are then not used).</param>
    /// <param name="tuple">The values of the first tuple of the selection.</param>
    /// <param name="scale">The scale of the channel.</param>
    /// <param name="extent">The size of the data rectangle along the channel.</param>
    /// <param name="first">Receives the position of the first end.</param>
    /// <param name="second">Receives the position of the second end.</param>
    /// <returns><see langword="false"/> when the selection is projected over the channel and an end has no position (the tuple has no pair of values, or the scale gives none); <see langword="true"/> otherwise.</returns>
    private static bool TryEnds(int index, IReadOnlyList<DataValue> tuple, ResolvedScale? scale, double extent, out double first, out double second)
    {
        first = second = 0;
        if (index < 0)
        {
            return true;
        }

        if (scale is null || tuple[index] is not { IsArray: true } pair || pair.Elements.Length < 2)
        {
            return false;
        }

        var model = scale.CreateModel(extent);
        var low = model.Map(pair.Elements[0]);
        var high = model.Map(pair.Elements[1]);
        if (low is not { } a || high is not { } b || double.IsNaN(a) || double.IsNaN(b))
        {
            return false;
        }

        (first, second) = (a, b);
        return true;
    }

    /// <summary>
    ///     Gives the outline the stroke of the <c>mark</c> of the selection: every key but the fill, the fill opacity and the cursor. Vega-Lite copies every such key to the rectangle (its schema names only
    ///     <c>stroke</c>, <c>strokeOpacity</c>, <c>strokeWidth</c>, <c>strokeDash</c> and <c>strokeDashOffset</c>, but the compiler does not filter them), so the other stroke properties that a rectangle has
    ///     (<c>strokeCap</c>, <c>strokeJoin</c>, <c>strokeMiterLimit</c>) and its <c>opacity</c> are applied too; a key that is not a rectangle property that the scene models is ignored.
    /// </summary>
    /// <param name="outline">The outline.</param>
    /// <param name="mark">The <c>mark</c> of the selection.</param>
    /// <returns>The outline with its stroke.</returns>
    private static SceneRect StrokeOf(SceneRect outline, ConfigNode mark)
    {
        var result = outline;
        foreach (var (key, value) in mark.Properties.Where(p => !NonStrokeKeys.Contains(p.Key)))
        {
            result = key switch
            {
                "stroke" => value.Kind == ConfigNodeKind.String ? result with { Stroke = value.StringValue } : result,
                "strokeOpacity" when value.Kind == ConfigNodeKind.Number => result with { StrokeOpacity = value.NumberValue },
                "strokeWidth" when value.Kind == ConfigNodeKind.Number => result with { StrokeWidth = value.NumberValue },
                "strokeDashOffset" when value.Kind == ConfigNodeKind.Number => result with { StrokeDashOffset = value.NumberValue },
                "strokeDash" when value.Kind == ConfigNodeKind.Array => result with { StrokeDash = [.. value.Items.Where(i => i.Kind == ConfigNodeKind.Number).Select(i => i.NumberValue)] },
                "strokeCap" when value.Kind == ConfigNodeKind.String => result with { StrokeCap = value.StringValue },
                "strokeJoin" when value.Kind == ConfigNodeKind.String => result with { StrokeJoin = value.StringValue },
                "strokeMiterLimit" when value.Kind == ConfigNodeKind.Number => result with { StrokeMiterLimit = value.NumberValue },
                "opacity" when value.Kind == ConfigNodeKind.Number => result with { Opacity = value.NumberValue },
                _ => result,
            };
        }

        return result;
    }
}
