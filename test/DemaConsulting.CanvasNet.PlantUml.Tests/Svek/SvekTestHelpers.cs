using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Svek;

/// <summary>
///     A fake entity image of the Svek tests: a filled rectangle of a given size with a given clip outline, drawn with
///     the role <c>entity</c> and the entity's id.
/// </summary>
/// <param name="id">The id the items carry.</param>
/// <param name="width">The width.</param>
/// <param name="height">The height.</param>
/// <param name="clip">The clip outline, or null for the whole box.</param>
internal sealed class FakeImage(string id, double width, double height, SvekClip? clip = null) : ISvekEntityImage
{
    /// <inheritdoc/>
    public double Width { get; } = width;

    /// <inheritdoc/>
    public double Height { get; } = height;

    /// <inheritdoc/>
    public SvekClip Clip { get; } = clip ?? new SvekClip(ClipShape.Rectangle, new SceneRect(0, 0, width, height));

    /// <inheritdoc/>
    public IReadOnlyList<SceneItem> Draw(double x, double y) =>
        [new ScenePath(SceneShapes.Rect(x, y, Width, Height)) { Role = "entity", Id = id, Fill = new Rgba32(0xF1, 0xF1, 0xF1, 255) }];
}

/// <summary>
///     Shared helpers of the Svek tests: entities with fake images, the context of a layout call, statements, and
///     geometry checks.
/// </summary>
internal static class SvekTestHelpers
{
    /// <summary>
    ///     Creates an entity with a fake image.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="parent">The parent cluster, or null.</param>
    /// <param name="clip">The clip outline, or null for the whole box.</param>
    /// <returns>The entity.</returns>
    public static SvekEntity Entity(string id, double width = 80, double height = 40, SvekCluster? parent = null, SvekClip? clip = null) =>
        new(id, new FakeImage(id, width, height, clip)) { Parent = parent };

    /// <summary>
    ///     Creates the context of a layout call with the test's cancellation token.
    /// </summary>
    /// <param name="options">The options, or null for the defaults.</param>
    /// <returns>The context.</returns>
    public static DiagramRenderContext Context(PlantUmlRenderOptions? options = null) =>
        new(
            options ?? new PlantUmlRenderOptions(),
            new PlantUmlReportSink(100, false),
            new TextLayout(TextLayout.CreateFontResolver()),
            TestContext.Current.CancellationToken);

    /// <summary>
    ///     Lays a diagram out with the default context.
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <returns>The placement.</returns>
    public static SvekPlacement Place(SvekDiagram diagram) => SvekLayout.Run(diagram, Context());

    /// <summary>
    ///     Reads the statements of a diagram body (the lines between <c>@startuml</c> and <c>@enduml</c>), without the
    ///     empty ones.
    /// </summary>
    /// <param name="body">The lines.</param>
    /// <returns>The statements.</returns>
    public static List<SourceStatement> Statements(string body) =>
        Preprocessor.Run("@startuml\n" + body + "\n@enduml\n", null, PlantUmlLimits.Default)
            .Block.Statements.Where(s => s.Text.Length > 0).ToList();

    /// <summary>
    ///     Reads one statement.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <returns>The statement.</returns>
    public static SourceStatement Statement(string text) => Statements(text).Single();

    /// <summary>
    ///     Gets the placed box of an entity.
    /// </summary>
    /// <param name="placement">The placement.</param>
    /// <param name="id">The id.</param>
    /// <returns>The box.</returns>
    public static SceneRect BoxOf(SvekPlacement placement, string id) => placement.Entities.Single(e => e.Entity.Id == id).Box;

    /// <summary>
    ///     Gets the placed link with an id.
    /// </summary>
    /// <param name="placement">The placement.</param>
    /// <param name="id">The id.</param>
    /// <returns>The link.</returns>
    public static PlacedLink LinkOf(SvekPlacement placement, string id) => placement.Links.Single(l => l.Link.Id == id);

    /// <summary>
    ///     Decides whether a point is on the border of a rectangle, within a tolerance.
    /// </summary>
    /// <param name="box">The rectangle.</param>
    /// <param name="point">The point.</param>
    /// <param name="tolerance">The tolerance.</param>
    /// <returns>True when it is.</returns>
    public static bool OnBorder(SceneRect box, SvekPoint point, double tolerance = 0.01)
    {
        var inside = point.X >= box.X - tolerance && point.X <= box.Right + tolerance && point.Y >= box.Y - tolerance && point.Y <= box.Bottom + tolerance;
        var near = Math.Abs(point.X - box.X) <= tolerance || Math.Abs(point.X - box.Right) <= tolerance
            || Math.Abs(point.Y - box.Y) <= tolerance || Math.Abs(point.Y - box.Bottom) <= tolerance;
        return inside && near;
    }

    /// <summary>
    ///     Decides whether two rectangles overlap with an area.
    /// </summary>
    /// <param name="a">The first.</param>
    /// <param name="b">The second.</param>
    /// <returns>True when they do.</returns>
    public static bool Overlap(SceneRect a, SceneRect b) => a.X < b.Right - 1e-6 && b.X < a.Right - 1e-6 && a.Y < b.Bottom - 1e-6 && b.Y < a.Bottom - 1e-6;

    /// <summary>
    ///     Decides whether a rectangle contains another.
    /// </summary>
    /// <param name="outer">The outer rectangle.</param>
    /// <param name="inner">The inner rectangle.</param>
    /// <returns>True when it does.</returns>
    public static bool Contains(SceneRect outer, SceneRect inner) =>
        inner.X >= outer.X - 1e-6 && inner.Y >= outer.Y - 1e-6 && inner.Right <= outer.Right + 1e-6 && inner.Bottom <= outer.Bottom + 1e-6;
}
