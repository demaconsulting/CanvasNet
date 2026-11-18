// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <summary>
///     A connection drawn by a tile between two of its parts (PlantUML's <c>Connection</c>): an arrow from the out
///     point of one tile to the in point of another, or from a tile to a side of a diamond.
/// </summary>
/// <remarks>
///     A connection knows the tiles it joins so that, with swimlanes, it is drawn in the lane of both tiles or, when
///     they are in two lanes, by the crossing pass with the translations of both lanes.
/// </remarks>
/// <param name="tile1">The tile the arrow leaves, or null.</param>
/// <param name="tile2">The tile the arrow reaches, or null.</param>
internal abstract class TileConnection(Tile? tile1, Tile? tile2)
{
    /// <summary>
    ///     Gets the tile the arrow leaves, or null.
    /// </summary>
    public Tile? Tile1 { get; } = tile1;

    /// <summary>
    ///     Gets the tile the arrow reaches, or null.
    /// </summary>
    public Tile? Tile2 { get; } = tile2;

    /// <summary>
    ///     Gets a value indicating whether the connection can be drawn between two swimlanes.
    /// </summary>
    public virtual bool Translatable => false;

    /// <summary>
    ///     Draws the connection in the coordinates of the tile that owns it.
    /// </summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="x">The x of the owner's origin.</param>
    /// <param name="y">The y of the owner's origin.</param>
    public abstract void Draw(TileCanvas canvas, double x, double y);

    /// <summary>
    ///     Draws the connection between two swimlanes.
    /// </summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="x">The x of the owner's origin.</param>
    /// <param name="y">The y of the owner's origin.</param>
    /// <param name="dx1">The translation of the lane of the first tile.</param>
    /// <param name="dx2">The translation of the lane of the second tile.</param>
    public virtual void DrawTranslate(TileCanvas canvas, double x, double y, double dx1, double dx2)
    {
    }
}

/// <summary>
///     A connection whose drawing is given as delegates.
/// </summary>
/// <param name="tile1">The tile the arrow leaves, or null.</param>
/// <param name="tile2">The tile the arrow reaches, or null.</param>
/// <param name="draw">Draws the connection in the coordinates of its owner.</param>
/// <param name="translate">Draws the connection between two swimlanes, or null when it cannot be.</param>
internal sealed class DelegateConnection(
    Tile? tile1,
    Tile? tile2,
    Action<TileCanvas, double, double> draw,
    Action<TileCanvas, double, double, double, double>? translate = null) : TileConnection(tile1, tile2)
{
    /// <inheritdoc/>
    public override bool Translatable => translate is not null;

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y) => draw(canvas, x, y);

    /// <inheritdoc/>
    public override void DrawTranslate(TileCanvas canvas, double x, double y, double dx1, double dx2) =>
        translate?.Invoke(canvas, x, y, dx1, dx2);
}

/// <summary>
///     How a <see cref="TileCanvas"/> treats what is drawn on it.
/// </summary>
internal enum CanvasMode
{
    /// <summary>Everything is drawn.</summary>
    All,

    /// <summary>Only the tiles of one swimlane and the connections inside it are drawn.</summary>
    Lane,

    /// <summary>Only the connections between two swimlanes are drawn, with the translations of the lanes.</summary>
    Cross,
}

/// <summary>
///     The drawing surface of the activity layout (PlantUML's <c>UGraphic</c> with its swimlane interceptors and its
///     arrow collector): it records shapes in absolute coordinates and the arrows to draw after them.
/// </summary>
/// <remarks>
///     An instance is used by one layout on one thread.
/// </remarks>
internal sealed class TileCanvas
{
    /// <summary>The recorded shapes, shared by the canvases of one drawing.</summary>
    private readonly List<ActivityShape> _shapes;

    /// <summary>The recorded arrows, shared by the canvases of one drawing.</summary>
    private readonly List<(Snake Snake, ActivityLane? Lane)> _snakes;

    /// <summary>The swimlane drawn in <see cref="CanvasMode.Lane"/>.</summary>
    private readonly ActivityLane? _lane;

    /// <summary>The translation of every swimlane, for <see cref="CanvasMode.Cross"/>.</summary>
    private readonly Func<ActivityLane, double>? _laneTranslate;

    /// <summary>The canvas the crossing connections are drawn on.</summary>
    private readonly TileCanvas? _output;

    /// <summary>
    ///     Initializes a canvas that draws everything.
    /// </summary>
    /// <param name="layout">The layout.</param>
    public TileCanvas(ActivityLayout layout)
        : this(layout, CanvasMode.All, [], [], null, null, null)
    {
    }

    /// <summary>
    ///     Initializes a canvas.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="shapes">The shared shapes.</param>
    /// <param name="snakes">The shared arrows.</param>
    /// <param name="lane">The swimlane of <see cref="CanvasMode.Lane"/>.</param>
    /// <param name="laneTranslate">The translations of <see cref="CanvasMode.Cross"/>.</param>
    /// <param name="output">The canvas of <see cref="CanvasMode.Cross"/>.</param>
    private TileCanvas(
        ActivityLayout layout,
        CanvasMode mode,
        List<ActivityShape> shapes,
        List<(Snake, ActivityLane?)> snakes,
        ActivityLane? lane,
        Func<ActivityLane, double>? laneTranslate,
        TileCanvas? output)
    {
        Layout = layout;
        Mode = mode;
        _shapes = shapes;
        _snakes = snakes;
        _lane = lane;
        _laneTranslate = laneTranslate;
        _output = output;
    }

    /// <summary>
    ///     Gets the layout.
    /// </summary>
    public ActivityLayout Layout { get; }

    /// <summary>
    ///     Gets the mode.
    /// </summary>
    public CanvasMode Mode { get; }

    /// <summary>
    ///     Gets the swimlane drawn in <see cref="CanvasMode.Lane"/>, or null.
    /// </summary>
    public ActivityLane? Lane => _lane;

    /// <summary>
    ///     Gets the recorded shapes.
    /// </summary>
    public IReadOnlyList<ActivityShape> Shapes => _shapes;

    /// <summary>
    ///     Gets the recorded arrows with the lane they were drawn in.
    /// </summary>
    public IReadOnlyList<(Snake Snake, ActivityLane? Lane)> Snakes => _snakes;

    /// <summary>
    ///     Creates a canvas on the same records that draws one swimlane.
    /// </summary>
    /// <param name="lane">The swimlane.</param>
    /// <returns>The canvas.</returns>
    public TileCanvas ForLane(ActivityLane lane) => new(Layout, CanvasMode.Lane, _shapes, _snakes, lane, null, null);

    /// <summary>
    ///     Creates a canvas on the same records that draws the connections between swimlanes.
    /// </summary>
    /// <param name="laneTranslate">The translation of every swimlane.</param>
    /// <returns>The canvas.</returns>
    public TileCanvas ForCrossing(Func<ActivityLane, double> laneTranslate) =>
        new(Layout, CanvasMode.Cross, _shapes, _snakes, null, laneTranslate, this);

    /// <summary>
    ///     Draws a tile (filtered by the swimlane of the canvas).
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <param name="x">The x of its origin.</param>
    /// <param name="y">The y of its origin.</param>
    public void Tile(Tile tile, double x, double y)
    {
        Layout.Tick();
        if (Mode == CanvasMode.Lane && !tile.Lanes.Contains(_lane!))
        {
            return;
        }

        tile.Draw(this, x, y);
    }

    /// <summary>
    ///     Draws a connection (filtered by the swimlane of the canvas).
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="x">The x of its owner's origin.</param>
    /// <param name="y">The y of its owner's origin.</param>
    public void Connection(TileConnection connection, double x, double y)
    {
        Layout.Tick();
        switch (Mode)
        {
            case CanvasMode.All:
                connection.Draw(this, x, y);
                return;
            case CanvasMode.Lane:
                var inLane1 = connection.Tile1?.LaneOut is not { } out1 || out1 == _lane;
                var inLane2 = connection.Tile2?.LaneIn is not { } in2 || in2 == _lane;
                if (inLane1 && inLane2)
                {
                    connection.Draw(this, x, y);
                }

                return;
            default:
                if (connection.Tile1?.LaneOut is { } lane1 && connection.Tile2?.LaneIn is { } lane2 && lane1 != lane2 &&
                    connection.Translatable)
                {
                    connection.DrawTranslate(_output!, x, y, _laneTranslate!(lane1), _laneTranslate(lane2));
                }

                return;
        }
    }

    /// <summary>
    ///     Records a shape (nothing is recorded by the crossing pass, which only draws connections).
    /// </summary>
    /// <param name="shape">The shape.</param>
    public void Add(ActivityShape shape)
    {
        if (Mode != CanvasMode.Cross)
        {
            Layout.Tick();
            _shapes.Add(shape);
        }
    }

    /// <summary>
    ///     Records an arrow, drawn after every shape.
    /// </summary>
    /// <param name="snake">The arrow.</param>
    public void Add(Snake snake)
    {
        if (Mode != CanvasMode.Cross && snake.Points.Count >= 2)
        {
            Layout.Tick(snake.Points.Count);
            _snakes.Add((snake, _lane));
        }
    }

    /// <summary>
    ///     Records a rectangle.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="radius">The corner radius.</param>
    /// <param name="fill">The fill, or null.</param>
    /// <param name="stroke">The stroke, or null.</param>
    /// <param name="location">The location.</param>
    /// <returns>The shape, for further settings.</returns>
    public ActivityShape Rect(string role, double x, double y, double width, double height, double radius, Rgba32? fill, SceneStroke? stroke, PlantUmlLocation location)
    {
        var shape = new ActivityShape(ActivityShapeKind.Rect, role)
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Radius = radius,
            Fill = fill,
            Stroke = stroke,
            Location = location,
        };
        Add(shape);
        return shape;
    }

    /// <summary>
    ///     Records an ellipse.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="fill">The fill, or null.</param>
    /// <param name="stroke">The stroke, or null.</param>
    /// <param name="location">The location.</param>
    public void Ellipse(string role, double x, double y, double width, double height, Rgba32? fill, SceneStroke? stroke, PlantUmlLocation location) =>
        Add(new ActivityShape(ActivityShapeKind.Ellipse, role)
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Fill = fill,
            Stroke = stroke,
            Location = location,
        });

    /// <summary>
    ///     Records a closed outline.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="x">The x of its anchor.</param>
    /// <param name="y">The y of its anchor.</param>
    /// <param name="points">The corners, relative to the anchor.</param>
    /// <param name="fill">The fill, or null.</param>
    /// <param name="stroke">The stroke, or null.</param>
    /// <param name="location">The location.</param>
    /// <returns>The shape, for further settings.</returns>
    public ActivityShape Polygon(string role, double x, double y, (double X, double Y)[] points, Rgba32? fill, SceneStroke? stroke, PlantUmlLocation location)
    {
        var shape = new ActivityShape(ActivityShapeKind.Polygon, role)
        {
            X = x,
            Y = y,
            Segments = SceneShapes.Polygon(points),
            Fill = fill,
            Stroke = stroke,
            Location = location,
        };
        Add(shape);
        return shape;
    }

    /// <summary>
    ///     Records an open outline.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="x">The x of its anchor.</param>
    /// <param name="y">The y of its anchor.</param>
    /// <param name="points">The points, relative to the anchor.</param>
    /// <param name="stroke">The stroke.</param>
    /// <param name="location">The location.</param>
    /// <returns>The shape, for further settings.</returns>
    public ActivityShape Path(string role, double x, double y, (double X, double Y)[] points, SceneStroke? stroke, PlantUmlLocation location)
    {
        var shape = new ActivityShape(ActivityShapeKind.Path, role)
        {
            X = x,
            Y = y,
            Segments = SceneShapes.Polyline(points),
            Stroke = stroke,
            Location = location,
        };
        Add(shape);
        return shape;
    }

    /// <summary>
    ///     Records a straight line.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="x1">The start x.</param>
    /// <param name="y1">The start y.</param>
    /// <param name="x2">The end x.</param>
    /// <param name="y2">The end y.</param>
    /// <param name="stroke">The stroke.</param>
    /// <param name="location">The location.</param>
    public void Line(string role, double x1, double y1, double x2, double y2, SceneStroke stroke, PlantUmlLocation location) =>
        Add(new ActivityShape(ActivityShapeKind.Line, role) { X = x1, Y = y1, X2 = x2, Y2 = y2, Stroke = stroke, Location = location });

    /// <summary>
    ///     Records space that is kept free.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void Empty(double x, double y, double width, double height) =>
        Add(new ActivityShape(ActivityShapeKind.Empty, "empty") { X = x, Y = y, Width = width, Height = height });

    /// <summary>
    ///     Records the lines of a text block.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="x">The left of the block.</param>
    /// <param name="top">The top of the block.</param>
    /// <param name="align">The alignment of its lines.</param>
    /// <param name="role">The role.</param>
    /// <param name="location">The location.</param>
    public void Text(TextBlock block, double x, double top, TextAlign align, string role, PlantUmlLocation location)
    {
        foreach (var line in block.Draw(x, top, align, role, location))
        {
            Add(new ActivityShape(ActivityShapeKind.Text, role)
            {
                X = line.X,
                Y = line.Baseline,
                Width = line.Box.Width,
                Height = line.Box.Height,
                Text = line,
                Location = location,
            });
        }
    }
}
