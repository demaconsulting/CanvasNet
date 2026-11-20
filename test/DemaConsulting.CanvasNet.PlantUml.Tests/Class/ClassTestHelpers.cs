using DemaConsulting.CanvasNet.PlantUml.Class;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Class;

/// <summary>
///     Shared helpers of the Class tests: parsing a diagram body, the context of a layout call, and the images of the
///     entities.
/// </summary>
internal static class ClassTestHelpers
{
    /// <summary>
    ///     Parses the statements of a class diagram body (the lines between <c>@startuml</c> and <c>@enduml</c>).
    /// </summary>
    /// <param name="body">The lines.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The model.</returns>
    public static ClassModel Parse(string body, PlantUmlLimits? limits = null)
    {
        var source = Preprocessor.Run("@startuml\n" + body + "\n@enduml\n", null, limits ?? PlantUmlLimits.Default);
        return ClassParser.Parse(source, DiagramDetector.Detect(source), limits ?? PlantUmlLimits.Default);
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
    ///     Builds the image of an entity of a model, with the portions its rules give it.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="id">The entity id.</param>
    /// <returns>The image.</returns>
    public static ClassImage Image(ClassModel model, string id)
    {
        var entity = model.Entities.Single(e => e.Id == id);
        return ClassRenderer.Image(entity, model.PortionsOf(entity), model.Theme, new TextLayout(TextLayout.CreateFontResolver()), null);
    }

    /// <summary>
    ///     Draws the image of an entity at the origin.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="id">The entity id.</param>
    /// <returns>The items.</returns>
    public static IReadOnlyList<SceneItem> Draw(ClassModel model, string id) => Image(model, id).Draw(0, 0);

    /// <summary>
    ///     Gets the text of a scene line.
    /// </summary>
    /// <param name="text">The line.</param>
    /// <returns>The text.</returns>
    public static string TextOf(SceneText text) => string.Concat(text.Runs.Select(r => r.Text));
}
