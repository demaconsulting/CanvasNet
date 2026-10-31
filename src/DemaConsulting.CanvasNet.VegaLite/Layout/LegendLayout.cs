// Portions derived from Vega (BSD-3-Clause): the legend layout of vega-view-transforms (ViewLayout and layout/legend.js); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Layout;

/// <summary>
///     The legends of a view after they were placed.
/// </summary>
/// <param name="Groups">The legend groups in the order of the legends that were given, positioned in the coordinates of the view.</param>
/// <param name="Bounds">The union of the bounds of all legends in the coordinates of the view (empty when there is none).</param>
internal sealed record LegendPlacement(IReadOnlyList<SceneGroup> Groups, SceneBounds Bounds);

/// <summary>
///     Places the legends of a view next to its data rectangle and its axes, and tells how far they reach.
/// </summary>
/// <remarks>
///     <para>
///         <b>Placement.</b> A port of Vega's <c>legendParams</c> and <c>gridLayout</c> for the legends of one view. The legends are grouped
///         by orient (in the order of the first legend of each); each group is one grid (alignment <c>each</c>, flush boxes) whose cells are
///         the legend boxes: a group of <c>left</c> or <c>right</c> legends is one column and one of <c>top</c> or <c>bottom</c> legends one
///         row (<c>config.legend.layout.direction</c>), with <c>margin</c> (8) between the boxes. The grid is anchored outside the data
///         rectangle and the axes: a <c>right</c> grid starts at the right edge of the data rectangle and the right axes (rounded up) plus the
///         offset, a <c>left</c> grid ends at the left edge (rounded down) minus the offset, <c>top</c> and <c>bottom</c> likewise above
///         and below; the offset is the largest <c>offset</c> that a legend asks for, else <c>config.legend.layout.offset</c> (18). The
///         position along the side comes from the <c>anchor</c> (start, middle, end) and the <c>frame</c> (the group, which is the data
///         rectangle, or the bounds, which include the axes). The inner orients (<c>top-left</c>, <c>top-right</c>, <c>bottom-left</c>,
///         <c>bottom-right</c>) are anchored at the offset from the corners of the data rectangle; the orient <c>none</c> keeps the
///         <c>legendX</c> and <c>legendY</c> of the legend. All anchors are rounded half up.
///     </para>
///     <para>
///         <b>Extents.</b> The union of the legend bounds joins the bounds of the view with those of the axes before the title is laid out,
///         so a legend outside the data rectangle makes the canvas larger (with <c>autosize: pad</c>) and is cut off or overflows
///         otherwise, which <see cref="ViewLayout"/> decides. The properties of <c>config.legend.layout</c> are read with Vega's lookup:
///         the object of the orient, then the layout object, then the default.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use; it loops over the legends only (at most eight channels), so it needs no
///         limit or cancellation point.
///     </para>
/// </remarks>
internal static class LegendLayout
{
    /// <summary>
    ///     Gets the bounds of the data rectangle united with the axes on its sides: the x bounds with the y axes (left and right) and the
    ///     y bounds with the x axes (top and bottom), which is where Vega measures the legends from.
    /// </summary>
    /// <param name="axes">The axes of the view.</param>
    /// <param name="dataWidth">The width of the data rectangle.</param>
    /// <param name="dataHeight">The height of the data rectangle.</param>
    /// <returns>The bounds that the left and right legends and the bounds that the top and bottom legends are placed against.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="axes"/> is null.</exception>
    public static (SceneBounds Horizontal, SceneBounds Vertical) AxisBounds(IEnumerable<AxisResult> axes, double dataWidth, double dataHeight)
    {
        ArgumentNullException.ThrowIfNull(axes);
        var horizontal = new SceneBounds(0, 0, dataWidth, dataHeight);
        var vertical = horizontal;
        foreach (var axis in axes)
        {
            if (axis.Orient is "left" or "right")
            {
                horizontal = horizontal.Union(axis.Bounds);
            }
            else
            {
                vertical = vertical.Union(axis.Bounds);
            }
        }

        return (horizontal, vertical);
    }

    /// <summary>
    ///     Combines the reach of the axes with the bounds of the legends.
    /// </summary>
    /// <param name="axes">How far the axes reach beyond the data rectangle.</param>
    /// <param name="legends">The bounds of the legends in the coordinates of the view; empty for none.</param>
    /// <param name="dataWidth">The width of the data rectangle.</param>
    /// <param name="dataHeight">The height of the data rectangle.</param>
    /// <returns>The larger reach on each side.</returns>
    public static ViewExtents Combine(ViewExtents axes, SceneBounds legends, double dataWidth, double dataHeight)
    {
        if (legends.IsEmpty)
        {
            return axes;
        }

        return new ViewExtents(
            Math.Max(axes.Left, -legends.X1),
            Math.Max(axes.Top, -legends.Y1),
            Math.Max(axes.Right, legends.X2 - dataWidth),
            Math.Max(axes.Bottom, legends.Y2 - dataHeight));
    }

    /// <summary>
    ///     Places the legends of a view.
    /// </summary>
    /// <param name="legends">The built legends.</param>
    /// <param name="config">The effective configuration (<c>legend.layout</c>).</param>
    /// <param name="horizontal">The data rectangle united with the left and right axes (see <see cref="AxisBounds"/>).</param>
    /// <param name="vertical">The data rectangle united with the top and bottom axes.</param>
    /// <param name="dataWidth">The width of the data rectangle.</param>
    /// <param name="dataHeight">The height of the data rectangle.</param>
    /// <returns>The positioned legend groups and the union of their bounds.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="legends"/> or <paramref name="config"/> is null.</exception>
    public static LegendPlacement Place(IReadOnlyList<LegendResult> legends, ResolvedConfig config, SceneBounds horizontal, SceneBounds vertical, double dataWidth, double dataHeight)
    {
        ArgumentNullException.ThrowIfNull(legends);
        ArgumentNullException.ThrowIfNull(config);
        var groups = new SceneGroup?[legends.Count];
        var bounds = SceneBounds.Empty;
        var layout = config.Root.Get("legend.layout");

        // A legend without an orient is placed by its own position
        for (var i = 0; i < legends.Count; i++)
        {
            if (legends[i].Orient == "none")
            {
                groups[i] = legends[i].CreateGroup(legends[i].X, legends[i].Y);
                bounds = bounds.Union(legends[i].Bounds.Translate(legends[i].X, legends[i].Y));
            }
        }

        // The others form one grid for each orient, in the order in which the orients first appear
        var orients = legends.Select(l => l.Orient).Where(o => o != "none").Distinct().ToList();

        foreach (var orient in orients)
        {
            var indexes = Enumerable.Range(0, legends.Count).Where(i => legends[i].Orient == orient).ToList();
            var settings = new Lookup(layout, orient);
            var full = settings.Text("bounds", "flush") == "full";
            var boxes = indexes.Select(i => full ? legends[i].Bounds : new SceneBounds(0, 0, legends[i].Width, legends[i].Height)).ToList();
            var options = Options(settings, orient, indexes.Select(i => legends[i]).ToList(), horizontal, vertical, dataWidth, dataHeight);
            var grid = LegendBuilder.GridLayout(boxes, options);
            for (var k = 0; k < indexes.Count; k++)
            {
                var legend = legends[indexes[k]];
                groups[indexes[k]] = legend.CreateGroup(grid.Dx[k], grid.Dy[k]);
                bounds = bounds.Union(legend.Bounds.Translate(grid.Dx[k], grid.Dy[k]));
            }
        }

        return new LegendPlacement([.. groups.Select(g => g!)], bounds);
    }

    /// <summary>
    ///     Creates the grid options of the legends of one orient (Vega's <c>legendParams</c>).
    /// </summary>
    /// <param name="settings">The layout settings of the orient.</param>
    /// <param name="orient">The orient.</param>
    /// <param name="group">The legends of the orient.</param>
    /// <param name="horizontal">The data rectangle united with the left and right axes.</param>
    /// <param name="vertical">The data rectangle united with the top and bottom axes.</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <returns>The options.</returns>
    private static GridOptions Options(Lookup settings, string orient, List<LegendResult> group, SceneBounds horizontal, SceneBounds vertical, double width, double height)
    {
        // The offset that a legend asks for wins (the largest one), else the layout's
        var asked = group.Where(l => l.Offset is not null).Select(l => l.Offset!.Value).DefaultIfEmpty(double.NegativeInfinity).Max();
        var offset = double.IsNegativeInfinity(asked) ? settings.Number("offset", 0) : asked;
        var anchorName = settings.Text("anchor", "start");
        var anchor = ToAnchor(anchorName);
        var fraction = anchor switch
        {
            GridAnchor.End => 1.0,
            GridAnchor.Middle => 0.5,
            _ => 0.0,
        };
        var byBounds = settings.Text("frame", "group") == "bounds";
        var (ax, ay) = byBounds
            ? (horizontal.X1 + (fraction * horizontal.Width), vertical.Y1 + (fraction * vertical.Height))
            : (fraction * AlongGroup(width, vertical.Width + (2 * vertical.X1)), fraction * AlongGroup(height, horizontal.Height + (2 * horizontal.Y1)));
        var columns = settings.Text("direction", "horizontal") == "vertical" ? 1 : group.Count;
        var margin = settings.Number("margin", 8);
        var (centerColumns, centerRows) = settings.Center();
        var options = new GridOptions(columns, GridAlign.Each, GridAlign.Each, margin, margin, centerColumns, centerRows);
        return orient switch
        {
            "left" => options with { X = Math.Floor(horizontal.X1) - offset, ColumnAnchor = GridAnchor.End, Y = ay, RowAnchor = anchor },
            "right" => options with { X = Math.Ceiling(horizontal.X2) + offset, Y = ay, RowAnchor = anchor },
            "top" => options with { Y = Math.Floor(vertical.Y1) - offset, RowAnchor = GridAnchor.End, X = ax, ColumnAnchor = anchor },
            "bottom" => options with { Y = Math.Ceiling(vertical.Y2) + offset, X = ax, ColumnAnchor = anchor },
            "top-left" => options with { X = offset, Y = offset },
            "top-right" => options with { X = width - offset, Y = offset, ColumnAnchor = GridAnchor.End },
            "bottom-left" => options with { X = offset, Y = height - offset, RowAnchor = GridAnchor.End },
            _ => options with { X = width - offset, Y = height - offset, ColumnAnchor = GridAnchor.End, RowAnchor = GridAnchor.End },
        };
    }

    /// <summary>
    ///     Gets the size that the group frame of an anchor refers to: the size of the data rectangle, or, when that is zero, the size of the
    ///     bounds of the axes on the other side (Vega's <c>w || ...</c>).
    /// </summary>
    /// <param name="size">The size of the data rectangle.</param>
    /// <param name="fallback">The size to use when the data rectangle has none.</param>
    /// <returns>The size.</returns>
    private static double AlongGroup(double size, double fallback) => size.CompareTo(0) != 0 ? size : fallback;

    /// <summary>
    ///     Reads an anchor name.
    /// </summary>
    /// <param name="name">The name: <c>start</c>, <c>middle</c> or <c>end</c>.</param>
    /// <returns>The anchor; the start for anything else.</returns>
    private static GridAnchor ToAnchor(string name) => name switch
    {
        "end" => GridAnchor.End,
        "middle" => GridAnchor.Middle,
        _ => GridAnchor.Start,
    };

    /// <summary>
    ///     Reads the properties of <c>config.legend.layout</c> the way Vega does: the object of the orient first, then the layout, then a default.
    /// </summary>
    private sealed class Lookup
    {
        /// <summary>The layout object, or null.</summary>
        private readonly ConfigNode? _layout;

        /// <summary>The object of the orient, or null.</summary>
        private readonly ConfigNode? _orient;

        /// <summary>
        ///     Initializes the lookup.
        /// </summary>
        /// <param name="layout">The <c>legend.layout</c> object, or null.</param>
        /// <param name="orient">The orient.</param>
        public Lookup(ConfigNode? layout, string orient)
        {
            _layout = layout;
            _orient = layout?[orient];
        }

        /// <summary>
        ///     Reads a number.
        /// </summary>
        /// <param name="key">The property.</param>
        /// <param name="fallback">The value when no level has the property.</param>
        /// <returns>The number.</returns>
        public double Number(string key, double fallback) => Find(key)?.AsNumber() ?? fallback;

        /// <summary>
        ///     Reads a text.
        /// </summary>
        /// <param name="key">The property.</param>
        /// <param name="fallback">The value when no level has the property.</param>
        /// <returns>The text.</returns>
        public string Text(string key, string fallback) => Find(key)?.AsString() ?? fallback;

        /// <summary>
        ///     Reads <c>center</c>: a boolean for both directions, or an object with <c>row</c> and <c>column</c>.
        /// </summary>
        /// <returns>Whether the columns and whether the rows are centered.</returns>
        public (bool Columns, bool Rows) Center()
        {
            var node = Find("center");
            if (node is { IsObject: true })
            {
                return (node["column"]?.AsBool() ?? false, node["row"]?.AsBool() ?? false);
            }

            var both = node?.AsBool() ?? false;
            return (both, both);
        }

        /// <summary>
        ///     Finds a property in the object of the orient, then the layout object.
        /// </summary>
        /// <param name="key">The property.</param>
        /// <returns>The value, or null.</returns>
        private ConfigNode? Find(string key) => _orient is { IsObject: true } && _orient[key] is { } own ? own : _layout?[key];
    }
}
