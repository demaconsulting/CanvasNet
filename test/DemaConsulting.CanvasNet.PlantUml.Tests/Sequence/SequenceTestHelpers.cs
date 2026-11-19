using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Sequence;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Sequence;

/// <summary>
///     Shared helpers of the sequence tests: parsing a diagram body, the context of a layout, and the fixtures.
/// </summary>
internal static class SequenceTestHelpers
{
    /// <summary>
    ///     Parses the body of a sequence diagram (the statements between <c>@startuml</c> and <c>@enduml</c>).
    /// </summary>
    /// <param name="body">The statements, one per line.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The model.</returns>
    public static SequenceModel Parse(string body, PlantUmlLimits? limits = null)
    {
        var effective = limits ?? PlantUmlLimits.Default;
        var source = Preprocessor.Run("@startuml\n" + body + "\n@enduml\n", null, effective);
        var detected = DiagramDetector.Detect(source);
        return SequenceParser.Parse(source, detected, effective);
    }

    /// <summary>
    ///     Creates the context of a layout call with the test's cancellation token.
    /// </summary>
    /// <param name="options">The options, or null for the defaults.</param>
    /// <returns>The context.</returns>
    public static DiagramRenderContext Context(PlantUmlRenderOptions? options = null) =>
        Canceling(options, TestContext.Current.CancellationToken);

    /// <summary>
    ///     Creates the context of a layout call with a cancellation token.
    /// </summary>
    /// <param name="options">The options, or null for the defaults.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The context.</returns>
    public static DiagramRenderContext Canceling(PlantUmlRenderOptions? options, CancellationToken cancellationToken) =>
        new(
            options ?? new PlantUmlRenderOptions(),
            new PlantUmlReportSink(100, false),
            new TextLayout(TextLayout.CreateFontResolver()),
            cancellationToken);

    /// <summary>
    ///     Lays out the body of a sequence diagram.
    /// </summary>
    /// <param name="body">The statements.</param>
    /// <returns>The model and its geometry.</returns>
    public static (SequenceModel Model, SequenceGeometry Geometry) Layout(string body)
    {
        var model = Parse(body);
        return (model, SequenceLayout.Run(model, Context()));
    }

    /// <summary>
    ///     Lays out and draws the body of a sequence diagram.
    /// </summary>
    /// <param name="body">The statements.</param>
    /// <returns>The scene (with page texts).</returns>
    public static Scene Draw(string body) => Parse(body).Layout(Context());

    /// <summary>
    ///     Reads a sequence fixture.
    /// </summary>
    /// <param name="name">The name without extension.</param>
    /// <returns>The text.</returns>
    public static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence", name + ".puml"));

    /// <summary>
    ///     Gets the bounds of the paths of a scene with a role (and an id).
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <param name="id">The id, or null for any.</param>
    /// <returns>The bounds in paint order.</returns>
    public static List<SceneRect> Bounds(Scene scene, string role, string? id = null) =>
        scene.Items.OfType<ScenePath>()
            .Where(p => p.Role == role && (id is null || p.Id == id))
            .Select(p => Scene.BoundsOf([p])!.Value)
            .ToList();

    /// <summary>
    ///     Gets the texts of a scene with a role.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The text items in paint order.</returns>
    public static List<SceneText> Texts(Scene scene, string role) =>
        scene.Items.OfType<SceneText>().Where(t => t.Role == role).ToList();
}
