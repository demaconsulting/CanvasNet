// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <summary>
///     An action box with rounded corners (PlantUML's <c>FtileBox</c>): the text padded by 10 pixels on every side.
/// </summary>
/// <param name="layout">The layout.</param>
/// <param name="text">The text.</param>
/// <param name="fill">The fill, or null for the theme's.</param>
/// <param name="lane">The swimlane.</param>
/// <param name="inLink">The arrow that leads into it.</param>
/// <param name="location">The location.</param>
internal sealed class BoxTile(ActivityLayout layout, string text, Rgba32? fill, ActivityLane? lane, ActivityLink inLink, PlantUmlLocation location) : Tile(layout)
{
    /// <summary>The text, laid out on first use.</summary>
    private TextBlock? _block;

    /// <summary>
    ///     Gets the laid-out text.
    /// </summary>
    private TextBlock Block => _block ??= Layout.Text(text, SkinElement.Activity, location);

    /// <inheritdoc/>
    public override ActivityLink InLink => inLink;

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => lane;

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        var style = Layout.Theme[SkinElement.Activity];
        var geometry = Geometry;
        canvas.Rect("activity", x, y, geometry.Width, geometry.Height, style.RoundCorner / 2, fill ?? style.Fill, ActivityLayout.StrokeOf(style), location);
        canvas.Text(Block, x + style.Padding, y + style.Padding, TextAlign.Left, "activity-text", location);
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var padding = Layout.Theme[SkinElement.Activity].Padding;
        var width = Block.Width + (2 * padding);
        var height = Block.Height + (2 * padding);
        return new TileGeometry(width, height, width / 2, 0, height);
    }
}

/// <summary>
///     The kinds of circles of an activity diagram.
/// </summary>
internal enum CircleKind
{
    /// <summary>The start node: a filled circle of 20 pixels.</summary>
    Start,

    /// <summary>The stop node: a ring of 22 pixels around a filled circle of 12.</summary>
    Stop,

    /// <summary>The end node: a circle of 20 pixels with a cross.</summary>
    End,

    /// <summary>A connector: a circle of 20 pixels with one character.</summary>
    Spot,
}

/// <summary>
///     A start, stop, end or connector circle (PlantUML's <c>FtileCircleStart</c>, <c>FtileCircleStop</c>,
///     <c>FtileCircleEndCross</c> and <c>FtileCircleSpot</c>).
/// </summary>
/// <param name="layout">The layout.</param>
/// <param name="kind">The kind.</param>
/// <param name="lane">The swimlane.</param>
/// <param name="inLink">The arrow that leads into it.</param>
/// <param name="location">The location.</param>
internal sealed class CircleTile(ActivityLayout layout, CircleKind kind, ActivityLane? lane, ActivityLink inLink, PlantUmlLocation location) : Tile(layout)
{
    /// <summary>The size of a stop node.</summary>
    private const double StopSize = 22;

    /// <summary>The size of the other circles.</summary>
    private const double Size = 20;

    /// <summary>
    ///     Gets or sets the character of a connector.
    /// </summary>
    public string Spot { get; init; } = string.Empty;

    /// <summary>
    ///     Gets or sets the fill of a connector, or null for the theme's.
    /// </summary>
    public Rgba32? SpotFill { get; init; }

    /// <inheritdoc/>
    public override ActivityLink InLink => inLink;

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => lane;

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        var circle = Layout.Theme[SkinElement.ActivityCircle];
        var color = circle.Fill ?? SkinTheme.Black;
        switch (kind)
        {
            case CircleKind.Start:
                canvas.Ellipse("start", x, y, Size, Size, color, new SceneStroke(color, 1), location);
                break;
            case CircleKind.Stop:
                canvas.Ellipse("stop", x, y, StopSize, StopSize, null, new SceneStroke(color, 1), location);
                canvas.Ellipse("stop", x + 5, y + 5, StopSize - 10, StopSize - 10, color, new SceneStroke(color, 1), location);
                break;
            case CircleKind.End:
                DrawEnd(canvas, x, y, color);
                break;
            default:
                DrawSpot(canvas, x, y);
                break;
        }
    }

    /// <summary>
    ///     Draws an end node: a circle with a cross of thick lines.
    /// </summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="color">The color.</param>
    private void DrawEnd(TileCanvas canvas, double x, double y, Rgba32 color)
    {
        canvas.Ellipse("end", x, y, Size, Size, null, new SceneStroke(color, 1.5), location);
        const double thickness = 2.5;
        var size2 = (Size - thickness) / Math.Sqrt(2);
        var delta = (Size - size2) / 2;
        var stroke = new SceneStroke(color, thickness);
        canvas.Line("end", x + delta, y + delta, x + delta + size2, y + delta + size2, stroke, location);
        canvas.Line("end", x + delta + size2, y + delta, x + delta, y + delta + size2, stroke, location);
    }

    /// <summary>
    ///     Draws a connector: a circle with its character centered.
    /// </summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    private void DrawSpot(TileCanvas canvas, double x, double y)
    {
        var style = Layout.Theme[SkinElement.Activity];
        canvas.Ellipse("connector", x, y, Size, Size, SpotFill ?? style.Fill, ActivityLayout.StrokeOf(style), location);

        // The character is drawn in the 14-pixel activity font, centered on its glyph
        var font = new SceneFont(style.FontFamily, 14, false, false);
        var block = Layout.Context.Text.Layout(Spot, font, style.FontColor, 0, Layout.Context.Sink, location);
        if (block.Lines.Count == 0)
        {
            return;
        }

        var line = block.Lines[0];
        var baseline = y + (Size / 2) + (0.3036 * font.Size);
        var before = canvas.Shapes.Count;
        canvas.Text(block, x + ((Size - line.Width) / 2), baseline - line.Height + line.Descent, TextAlign.Left, "connector-text", location);

        // PlantUML draws the character as a centered glyph, which neither the extent nor the compression sees
        for (var i = before; i < canvas.Shapes.Count; i++)
        {
            canvas.Shapes[i].CountsForLimits = false;
            canvas.Shapes[i].CountsForSlots = false;
        }
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry() => kind switch
    {
        CircleKind.Start or CircleKind.Spot => new TileGeometry(Size, Size, Size / 2, 0, Size),
        CircleKind.Stop => TileGeometry.NoOut(StopSize, StopSize, StopSize / 2, 0),
        _ => TileGeometry.NoOut(Size, Size, Size / 2, 0),
    };
}

/// <summary>
///     A diamond of 24 pixels with texts around it (PlantUML's <c>FtileDiamond</c>): the merge of an if, the start of a
///     repeat loop, or the end of a merge.
/// </summary>
/// <param name="layout">The layout.</param>
/// <param name="fill">The fill, or null for the theme's.</param>
/// <param name="lane">The swimlane.</param>
internal sealed class DiamondTile(ActivityLayout layout, Rgba32? fill, ActivityLane? lane) : Tile(layout)
{
    /// <summary>
    ///     Gets the text above the diamond, or null.
    /// </summary>
    public TextBlock? North { get; init; }

    /// <summary>
    ///     Gets the text below the diamond, or null.
    /// </summary>
    public TextBlock? South { get; init; }

    /// <summary>
    ///     Gets the text left of the diamond, or null.
    /// </summary>
    public TextBlock? West { get; init; }

    /// <summary>
    ///     Gets the text right of the diamond, or null.
    /// </summary>
    public TextBlock? East { get; init; }

    /// <summary>
    ///     Gets the location the diamond draws.
    /// </summary>
    public PlantUmlLocation Location { get; init; }

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => lane;

    /// <summary>
    ///     Gets the height of the taller of the west and east texts.
    /// </summary>
    public double WestEastLabelHeight => Math.Max(West?.Height ?? 0, East?.Height ?? 0);

    /// <summary>
    ///     Gets the width of the east text.
    /// </summary>
    public double EastLabelWidth => East?.Width ?? 0;

    /// <summary>
    ///     Gets the height of the south text.
    /// </summary>
    public double SouthLabelHeight => South?.Height ?? 0;

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        const double half = ActivityLayout.HexagonHalfSize;
        var style = Layout.Theme[SkinElement.ActivityDiamond];
        var top = y + (North?.Height ?? 0);
        canvas.Polygon("diamond", x, top, [(half, 0), (half * 2, half), (half, half * 2), (0, half), (half, 0)], fill ?? style.Fill, ActivityLayout.StrokeOf(style), Location);
        if (North is { } north)
        {
            canvas.Text(north, x + (half * 1.5), top - north.Height, TextAlign.Left, "diamond-text", Location);
        }

        if (South is { } south)
        {
            canvas.Text(south, x + (half * 1.5), top + (2 * half), TextAlign.Left, "arrow-text", Location);
        }

        if (West is { } west)
        {
            canvas.Text(west, x - west.Width, top - west.Height + half, TextAlign.Left, "arrow-text", Location);
        }

        if (East is { } east)
        {
            canvas.Text(east, x + (half * 2), top - east.Height + half, TextAlign.Left, "arrow-text", Location);
        }
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var north = North?.Height ?? 0;
        const double size = ActivityLayout.HexagonHalfSize * 2;
        return new TileGeometry(size, size + north, size / 2, north, size + north);
    }
}

/// <summary>
///     A hexagon around a test with texts around it (PlantUML's <c>FtileDiamondInside</c> and, for the elseif chain,
///     <c>FtileDiamondInside2</c>).
/// </summary>
/// <param name="layout">The layout.</param>
/// <param name="label">The test, laid out (empty for none).</param>
/// <param name="fill">The fill, or null for the theme's.</param>
/// <param name="lane">The swimlane.</param>
internal sealed class HexagonTile(ActivityLayout layout, TextBlock label, Rgba32? fill, ActivityLane? lane) : Tile(layout)
{
    /// <summary>
    ///     Gets the text drawn below-right of the hexagon (PlantUML's "north", which is drawn at the bottom), or null.
    /// </summary>
    public TextBlock? North { get; init; }

    /// <summary>
    ///     Gets the text drawn below-right of the hexagon (PlantUML's "south"), or null.
    /// </summary>
    public TextBlock? South { get; init; }

    /// <summary>
    ///     Gets or sets the text left of the hexagon, or null.
    /// </summary>
    public TextBlock? West { get; set; }

    /// <summary>
    ///     Gets or sets the text right of the hexagon, or null.
    /// </summary>
    public TextBlock? East { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the geometry is the elseif one (as wide as the hexagon and its north text).
    /// </summary>
    public bool ElseIfGeometry { get; init; }

    /// <summary>
    ///     Gets the location the hexagon draws.
    /// </summary>
    public PlantUmlLocation Location { get; init; }

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => lane;

    /// <summary>
    ///     Gets the width of the east text.
    /// </summary>
    public double EastLabelWidth => East?.Width ?? 0;

    /// <summary>
    ///     Gets the height of the south text.
    /// </summary>
    public double SouthLabelHeight => South?.Height ?? 0;

    /// <summary>
    ///     Swaps the west and east texts (when the else branch of an if goes around on the left).
    /// </summary>
    public void SwapEastWest() => (West, East) = (East, West);

    /// <summary>
    ///     Gets the size of the hexagon alone.
    /// </summary>
    private (double Width, double Height) Alone
    {
        get
        {
            const double size = ActivityLayout.HexagonHalfSize * 2;
            if (label.Width == 0 || label.Height == 0)
            {
                return (size, size);
            }

            return (Math.Max(label.Width, size) + size, Math.Max(label.Height, size));
        }
    }

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        const double half = ActivityLayout.HexagonHalfSize;
        var style = Layout.Theme[SkinElement.ActivityDiamond];
        var (width, height) = Alone;
        canvas.Polygon(
            "diamond",
            x,
            y,
            [(half, 0), (width - half, 0), (width, height / 2), (width - half, height), (half, height), (0, height / 2), (half, 0)],
            fill ?? style.Fill,
            ActivityLayout.StrokeOf(style),
            Location);
        if (North is { } north)
        {
            canvas.Text(north, x + 4 + (width / 2), y + height, TextAlign.Left, "arrow-text", Location);
        }

        if (South is { } south)
        {
            canvas.Text(south, x + 4 + (width / 2), y + height, TextAlign.Left, "arrow-text", Location);
        }

        canvas.Text(label, x + ((width - label.Width) / 2), y + ((height - label.Height) / 2), TextAlign.Left, "diamond-text", Location);
        if (West is { } west)
        {
            canvas.Text(west, x - west.Width, y - west.Height + (height / 2), TextAlign.Left, "arrow-text", Location);
        }

        if (East is { } east)
        {
            canvas.Text(east, x + width, y - east.Height + (height / 2), TextAlign.Left, "arrow-text", Location);
        }
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var (width, height) = Alone;
        var north = North?.Height ?? 0;
        if (ElseIfGeometry)
        {
            var left = width / 2;
            var total = (North?.Width ?? 0) > left ? left + North!.Width : width;
            return new TileGeometry(total, height + north, left, 0, height);
        }

        return new TileGeometry(width, height + north, width / 2, 0, height);
    }
}

/// <summary>
///     The bar of a fork (PlantUML's <c>FtileBlackBlock</c>): 6 pixels high, as wide as the fork, with an optional
///     label at its right.
/// </summary>
/// <param name="layout">The layout.</param>
/// <param name="lane">The swimlane.</param>
internal sealed class BarTile(ActivityLayout layout, ActivityLane? lane) : Tile(layout)
{
    /// <summary>
    ///     Gets or sets the width of the bar.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    ///     Gets or sets the label, or null.
    /// </summary>
    public TextBlock? Label { get; set; }

    /// <summary>
    ///     Gets or sets the location the bar draws.
    /// </summary>
    public PlantUmlLocation Location { get; set; }

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => lane;

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        var color = Layout.Theme[SkinElement.ActivityBar].Fill ?? SkinTheme.Black;
        var rect = canvas.Rect("bar", x, y, Width, ActivityLayout.BarHeight, 2.5, color, new SceneStroke(color, 1), Location);
        rect.IgnoreX = true;
        if (Label is { } label)
        {
            canvas.Text(label, x + Width + 5, y - (label.Height / 2), TextAlign.Left, "bar-text", Location);
        }
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var supp = Label is { } label && label.Width > 0 ? label.Width + 5 : 0;
        return new TileGeometry(Width + supp, ActivityLayout.BarHeight, Width / 2, 0, ActivityLayout.BarHeight);
    }
}

/// <summary>
///     The thin line of a split (PlantUML's <c>FtileThinSplit</c>).
/// </summary>
/// <param name="layout">The layout.</param>
/// <param name="color">The color, or null when the line is not drawn.</param>
/// <param name="lane">The swimlane.</param>
internal sealed class ThinSplitTile(ActivityLayout layout, Rgba32? color, ActivityLane? lane) : Tile(layout)
{
    /// <summary>The height of the tile.</summary>
    private const double Height = 1.5;

    /// <summary>
    ///     Gets or sets the x where the line starts.
    /// </summary>
    public double First { get; set; }

    /// <summary>
    ///     Gets or sets the x where the line ends.
    /// </summary>
    public double Last { get; set; }

    /// <summary>
    ///     Gets or sets the width of the tile.
    /// </summary>
    public double Width { get; set; }

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => lane;

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        if (color is { } line)
        {
            canvas.Line("split", x + First, y, x + Last, y, new SceneStroke(line, 1.5), default);
        }
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry() => new(Width, Height, Width / 2, 0, Height);
}
