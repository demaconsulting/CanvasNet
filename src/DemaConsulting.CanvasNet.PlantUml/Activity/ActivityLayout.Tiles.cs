// Portions derived from PlantUML (MIT); see third-party/README.md
namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <summary>
///     The geometry of a tile (PlantUML's <c>FtileGeometry</c>): its size, the x of its in and out points, the y of
///     its in point, and the y of its out point (not a number when the flow does not leave the tile).
/// </summary>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="Left">The x of the in and out points.</param>
/// <param name="InY">The y of the in point.</param>
/// <param name="OutY">The y of the out point, or <see cref="double.NaN"/> when the tile has none.</param>
internal readonly record struct TileGeometry(double Width, double Height, double Left, double InY, double OutY)
{
    /// <summary>
    ///     Gets a value indicating whether the flow leaves the tile.
    /// </summary>
    public bool HasOut => !double.IsNaN(OutY);

    /// <summary>
    ///     Gets the in point.
    /// </summary>
    public (double X, double Y) PointIn => (Left, InY);

    /// <summary>
    ///     Gets the out point.
    /// </summary>
    public (double X, double Y) PointOut => (Left, OutY);

    /// <summary>
    ///     Gets the middle of the right side (PlantUML's point B).
    /// </summary>
    public (double X, double Y) PointB => (Width, (InY + OutY) / 2);

    /// <summary>
    ///     Gets the middle of the left side (PlantUML's point D).
    /// </summary>
    public (double X, double Y) PointD => (0, (InY + OutY) / 2);

    /// <summary>
    ///     Creates a geometry without an out point.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="left">The x of the in point.</param>
    /// <param name="inY">The y of the in point.</param>
    /// <returns>The geometry.</returns>
    public static TileGeometry NoOut(double width, double height, double left, double inY) => new(width, height, left, inY, double.NaN);

    /// <summary>
    ///     Returns the geometry without its out point.
    /// </summary>
    /// <returns>The geometry.</returns>
    public TileGeometry WithoutOut() => this with { OutY = double.NaN };

    /// <summary>
    ///     Stacks another geometry below this one, aligned on the in and out points (PlantUML's
    ///     <c>FtileGeometryMerger</c>).
    /// </summary>
    /// <param name="other">The geometry below.</param>
    /// <returns>The geometry of both.</returns>
    public TileGeometry AppendBottom(TileGeometry other)
    {
        var left = Math.Max(Left, other.Left);
        var dx1 = left - Left;
        var dx2 = left - other.Left;
        var width = Math.Max(Width + dx1, other.Width + dx2);
        var height = Height + other.Height;
        return new TileGeometry(width, height, left, InY, other.HasOut ? other.OutY + Height : double.NaN);
    }

    /// <summary>
    ///     Adds margins on the left and the right.
    /// </summary>
    /// <param name="margin1">The left margin.</param>
    /// <param name="margin2">The right margin.</param>
    /// <returns>The geometry.</returns>
    public TileGeometry AddMarginX(double margin1, double margin2) => this with { Width = Width + margin1 + margin2, Left = Left + margin1 };

    /// <summary>
    ///     Adds space above and below, moving the in and out points.
    /// </summary>
    /// <param name="top">The space above.</param>
    /// <param name="bottom">The space below.</param>
    /// <returns>The geometry.</returns>
    public TileGeometry IncVertically(double top, double bottom) =>
        new(Width, Height + top + bottom, Left, InY + top, HasOut ? OutY + top : OutY);

    /// <summary>
    ///     Adds to the size; the out point moves down by the added height (PlantUML's <c>addDim</c>).
    /// </summary>
    /// <param name="width">The added width.</param>
    /// <param name="height">The added height.</param>
    /// <returns>The geometry.</returns>
    public TileGeometry AddDim(double width, double height) => new(Width + width, Height + height, Left, InY, OutY + height);

    /// <summary>
    ///     Moves the in and out points.
    /// </summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    /// <returns>The geometry.</returns>
    public TileGeometry Translate(double dx, double dy) => new(Width, Height, Left + dx, InY + dy, OutY + dy);
}

/// <summary>
///     A tile of the activity layout (PlantUML's <c>Ftile</c>): a rectangle of the drawing with an in point at its top
///     and, unless the flow ends inside it, an out point at its bottom. Tiles are composed into bigger tiles, each
///     placing its children and drawing the connections between them.
/// </summary>
/// <param name="layout">The layout the tile belongs to.</param>
internal abstract class Tile(ActivityLayout layout)
{
    /// <summary>The geometry, computed on first use.</summary>
    private TileGeometry? _geometry;

    /// <summary>The swimlanes, computed on first use.</summary>
    private IReadOnlySet<ActivityLane>? _lanes;

    /// <summary>
    ///     Gets the geometry.
    /// </summary>
    public TileGeometry Geometry => _geometry ??= ComputeGeometry();

    /// <summary>
    ///     Gets the layout.
    /// </summary>
    public ActivityLayout Layout { get; } = layout;

    /// <summary>
    ///     Gets the arrow that leads into the tile.
    /// </summary>
    public virtual ActivityLink InLink => ActivityLink.None;

    /// <summary>
    ///     Gets the arrow that leaves the tile.
    /// </summary>
    public virtual ActivityLink OutLink => ActivityLink.None;

    /// <summary>
    ///     Gets the swimlane the flow enters the tile in, or null.
    /// </summary>
    public virtual ActivityLane? LaneIn => null;

    /// <summary>
    ///     Gets the swimlane the flow leaves the tile in, or null.
    /// </summary>
    public virtual ActivityLane? LaneOut => LaneIn;

    /// <summary>
    ///     Gets the swimlanes the tile draws in, computed on first use.
    /// </summary>
    public IReadOnlySet<ActivityLane> Lanes => _lanes ??= ComputeLanes();

    /// <summary>
    ///     Gets an empty set of swimlanes.
    /// </summary>
    protected static IReadOnlySet<ActivityLane> EmptyLanes { get; } = new HashSet<ActivityLane>();

    /// <summary>
    ///     Draws the tile with its origin at a point.
    /// </summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="x">The x of the origin.</param>
    /// <param name="y">The y of the origin.</param>
    public abstract void Draw(TileCanvas canvas, double x, double y);

    /// <summary>
    ///     Computes the geometry.
    /// </summary>
    /// <returns>The geometry.</returns>
    protected abstract TileGeometry ComputeGeometry();

    /// <summary>
    ///     Computes the swimlanes the tile draws in.
    /// </summary>
    /// <returns>The swimlanes.</returns>
    protected virtual IReadOnlySet<ActivityLane> ComputeLanes() => LaneIn is { } lane ? new HashSet<ActivityLane> { lane } : EmptyLanes;

    /// <summary>
    ///     Merges the swimlanes of tiles.
    /// </summary>
    /// <param name="tiles">The tiles.</param>
    /// <returns>The union of their swimlanes.</returns>
    protected static IReadOnlySet<ActivityLane> MergeLanes(params Tile?[] tiles)
    {
        var result = new HashSet<ActivityLane>();
        foreach (var tile in tiles.OfType<Tile>())
        {
            result.UnionWith(tile.Lanes);
        }

        return result;
    }
}

/// <summary>
///     A tile that delegates to another one (PlantUML's <c>FtileDecorate</c>), optionally replacing the arrows into or
///     out of it.
/// </summary>
/// <param name="inner">The decorated tile.</param>
/// <param name="inLink">The replacement of the arrow into it, or null.</param>
/// <param name="outLink">The replacement of the arrow out of it, or null.</param>
internal class DecoratedTile(Tile inner, ActivityLink? inLink = null, ActivityLink? outLink = null) : Tile(inner.Layout)
{
    /// <summary>
    ///     Gets the decorated tile.
    /// </summary>
    public Tile Inner { get; } = inner;

    /// <inheritdoc/>
    public override ActivityLink InLink => inLink ?? Inner.InLink;

    /// <inheritdoc/>
    public override ActivityLink OutLink => outLink ?? Inner.OutLink;

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => Inner.LaneIn;

    /// <inheritdoc/>
    public override ActivityLane? LaneOut => Inner.LaneOut;

    /// <inheritdoc/>
    protected override IReadOnlySet<ActivityLane> ComputeLanes() => Inner.Lanes;

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y) => Inner.Draw(canvas, x, y);

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry() => Inner.Geometry;
}

/// <summary>
///     A tile with connections drawn after it (PlantUML's <c>FtileWithConnection</c>).
/// </summary>
/// <param name="inner">The tile.</param>
/// <param name="connections">The connections, in the coordinates of the tile.</param>
internal sealed class ConnectedTile(Tile inner, IReadOnlyList<TileConnection> connections) : DecoratedTile(inner)
{
    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        Inner.Draw(canvas, x, y);
        foreach (var connection in connections)
        {
            canvas.Connection(connection, x, y);
        }
    }
}

/// <summary>
///     A tile whose flow ends inside it (PlantUML's <c>FtileKilled</c>).
/// </summary>
/// <param name="inner">The tile.</param>
internal sealed class KilledTile(Tile inner) : DecoratedTile(inner)
{
    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y) => canvas.Tile(Inner, x, y);

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry() => Inner.Geometry.WithoutOut();
}

/// <summary>
///     A tile with horizontal margins (PlantUML's <c>FtileMarged</c>).
/// </summary>
/// <param name="inner">The tile.</param>
/// <param name="margin1">The left margin.</param>
/// <param name="margin2">The right margin.</param>
internal sealed class MarginTile(Tile inner, double margin1, double margin2) : DecoratedTile(inner)
{
    /// <summary>
    ///     Gets the left margin.
    /// </summary>
    public double Margin1 { get; } = margin1;

    /// <summary>
    ///     Adds horizontal margins to a tile (none when both are zero).
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <param name="margin1">The left margin.</param>
    /// <param name="margin2">The right margin.</param>
    /// <returns>The tile with its margins.</returns>
    public static Tile Of(Tile tile, double margin1, double margin2) => margin1 == 0 && margin2 == 0 ? tile : new MarginTile(tile, margin1, margin2);

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y) => canvas.Tile(Inner, x + Margin1, y);

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry() => Inner.Geometry.AddMarginX(Margin1, margin2);
}

/// <summary>
///     A tile with space above and below (PlantUML's <c>FtileMargedVertically</c>).
/// </summary>
/// <param name="inner">The tile.</param>
/// <param name="top">The space above.</param>
/// <param name="bottom">The space below.</param>
internal sealed class VerticalMarginTile(Tile inner, double top, double bottom) : DecoratedTile(inner)
{
    /// <summary>
    ///     Adds space above and below a tile (none when both are zero).
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <param name="top">The space above.</param>
    /// <param name="bottom">The space below.</param>
    /// <returns>The tile with its margins.</returns>
    public static Tile Of(Tile tile, double top, double bottom) => top == 0 && bottom == 0 ? tile : new VerticalMarginTile(tile, top, bottom);

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y) => canvas.Tile(Inner, x, y + Math.Max(0, top));

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry() => Inner.Geometry.IncVertically(top, bottom);
}

/// <summary>
///     A tile at least as wide as a minimum, centered in it (PlantUML's <c>FtileMinWidthCentered</c>).
/// </summary>
/// <param name="inner">The tile.</param>
/// <param name="minWidth">The minimum width.</param>
internal sealed class MinWidthTile(Tile inner, double minWidth) : DecoratedTile(inner)
{
    /// <summary>
    ///     Gets the horizontal offset of the inner tile.
    /// </summary>
    private double Shift => Inner.Geometry.Width < minWidth ? (minWidth - Inner.Geometry.Width) / 2 : 0;

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y) => Inner.Draw(canvas, x + Shift, y);

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var geometry = Inner.Geometry;
        return geometry with { Width = Math.Max(geometry.Width, minWidth), Left = geometry.Left + Shift };
    }
}

/// <summary>
///     A tile centered vertically in a fixed height (PlantUML's <c>FtileHeightFixedCentered</c>).
/// </summary>
/// <param name="inner">The tile.</param>
/// <param name="height">The fixed height.</param>
internal sealed class FixedHeightTile(Tile inner, double height) : DecoratedTile(inner)
{
    /// <summary>
    ///     Gets the vertical offset of the inner tile.
    /// </summary>
    private double Shift => (height - Inner.Geometry.Height) / 2;

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y) => canvas.Tile(Inner, x, y + Shift);

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry() => Inner.Geometry.Translate(0, Shift) with { Height = height };
}

/// <summary>
///     A tile with fixed space above and below (PlantUML's <c>FtileHeightFixedMarged</c>).
/// </summary>
/// <param name="inner">The tile.</param>
/// <param name="top">The space above.</param>
/// <param name="bottom">The space below.</param>
internal sealed class FixedMarginTile(Tile inner, double top, double bottom) : DecoratedTile(inner)
{
    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y) => canvas.Tile(Inner, x, y + top);

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var geometry = Inner.Geometry;
        return geometry.Translate(0, top) with { Height = top + geometry.Height + bottom };
    }
}

/// <summary>
///     A tile widened on the right to a total width (PlantUML's <c>FtileMargedRight</c>).
/// </summary>
/// <param name="inner">The tile.</param>
/// <param name="width">The total width.</param>
internal sealed class RightMarginTile(Tile inner, double width) : DecoratedTile(inner)
{
    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y) => canvas.Tile(Inner, x, y);

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry() => Inner.Geometry with { Width = Math.Max(width, Inner.Geometry.Width) };
}

/// <summary>
///     Two tiles one below the other, aligned on their in and out points (PlantUML's <c>FtileAssemblySimple</c>).
/// </summary>
/// <param name="layout">The layout.</param>
/// <param name="tile1">The upper tile.</param>
/// <param name="tile2">The lower tile.</param>
internal sealed class AssemblyTile(ActivityLayout layout, Tile tile1, Tile tile2) : Tile(layout)
{
    /// <summary>
    ///     Gets the upper tile.
    /// </summary>
    public Tile Tile1 { get; } = tile1;

    /// <summary>
    ///     Gets the lower tile.
    /// </summary>
    public Tile Tile2 { get; } = tile2;

    /// <inheritdoc/>
    public override ActivityLink InLink => Tile1.InLink;

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => Tile1.LaneIn;

    /// <inheritdoc/>
    public override ActivityLane? LaneOut => Tile2.LaneOut;

    /// <inheritdoc/>
    protected override IReadOnlySet<ActivityLane> ComputeLanes() => MergeLanes(Tile1, Tile2);

    /// <summary>
    ///     Gets the offset of the upper tile.
    /// </summary>
    public (double X, double Y) Offset1 => (Geometry.Left - Tile1.Geometry.Left, 0);

    /// <summary>
    ///     Gets the offset of the lower tile.
    /// </summary>
    public (double X, double Y) Offset2 => (Geometry.Left - Tile2.Geometry.Left, Tile1.Geometry.Height);

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        canvas.Tile(Tile1, x + Offset1.X, y + Offset1.Y);
        canvas.Tile(Tile2, x + Offset2.X, y + Offset2.Y);
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry() => Tile1.Geometry.AppendBottom(Tile2.Geometry);
}

/// <summary>
///     A tile that draws nothing (PlantUML's <c>FtileEmpty</c>).
/// </summary>
/// <param name="layout">The layout.</param>
/// <param name="width">The width.</param>
/// <param name="height">The height.</param>
/// <param name="lane">The swimlane, or null.</param>
internal sealed class EmptyTile(ActivityLayout layout, double width, double height, ActivityLane? lane) : Tile(layout)
{
    /// <inheritdoc/>
    public override ActivityLane? LaneIn => lane;

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry() => new(width, height, width / 2, 0, height);
}
