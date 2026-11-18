using DemaConsulting.CanvasNet.PlantUml.Activity;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Activity;

/// <summary>
///     Shared helpers of the activity tests: parsing a diagram body, laying it out, drawing it, and the fixtures.
/// </summary>
internal static class ActivityTestHelpers
{
    /// <summary>
    ///     Parses the body of an activity diagram (the statements between <c>@startuml</c> and <c>@enduml</c>).
    /// </summary>
    /// <param name="body">The statements, one per line.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The model.</returns>
    public static ActivityModel Parse(string body, PlantUmlLimits? limits = null)
    {
        var effective = limits ?? PlantUmlLimits.Default;
        var source = Preprocessor.Run("@startuml\n" + body + "\n@enduml\n", null, effective);
        var detected = DiagramDetector.Detect(source);
        return ActivityParser.Parse(source, detected, effective);
    }

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
    ///     Lays out the body of an activity diagram.
    /// </summary>
    /// <param name="body">The statements.</param>
    /// <returns>The model and its geometry.</returns>
    public static (ActivityModel Model, ActivityGeometry Geometry) Layout(string body)
    {
        var model = Parse(body);
        return (model, ActivityLayout.Run(model, Context()));
    }

    /// <summary>
    ///     Lays out and draws the body of an activity diagram.
    /// </summary>
    /// <param name="body">The statements.</param>
    /// <returns>The scene (with page texts).</returns>
    public static Scene Draw(string body) => Parse(body).Layout(Context());

    /// <summary>
    ///     Reads an activity fixture.
    /// </summary>
    /// <param name="name">The name without extension.</param>
    /// <returns>The text.</returns>
    public static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "activity", name + ".puml"));

    /// <summary>
    ///     Gets the shapes of a geometry with a role.
    /// </summary>
    /// <param name="geometry">The geometry.</param>
    /// <param name="role">The role.</param>
    /// <returns>The shapes in paint order.</returns>
    public static List<ActivityShape> Shapes(ActivityGeometry geometry, string role) =>
        geometry.Shapes.Where(s => s.Role == role).ToList();

    /// <summary>
    ///     Gets the bounds of a placed shape as drawn.
    /// </summary>
    /// <param name="shape">The shape.</param>
    /// <returns>The bounds.</returns>
    public static SceneRect Box(ActivityShape shape) => Scene.BoundsOf([ActivityRenderer.ToItem(shape)!])!.Value;

    /// <summary>
    ///     Gets the bounds of the paths of a scene with a role.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The bounds in paint order.</returns>
    public static List<SceneRect> Bounds(Scene scene, string role) =>
        scene.Items.OfType<ScenePath>()
            .Where(p => p.Role == role)
            .Select(p => Scene.BoundsOf([p])!.Value)
            .ToList();

    /// <summary>
    ///     Gets the texts of a scene with a role.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The texts in paint order.</returns>
    public static List<SceneText> Texts(Scene scene, string role) =>
        scene.Items.OfType<SceneText>().Where(t => t.Role == role).ToList();
}
