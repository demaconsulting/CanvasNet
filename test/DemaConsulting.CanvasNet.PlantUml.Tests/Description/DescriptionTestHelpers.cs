using DemaConsulting.CanvasNet.PlantUml.Description;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Description;

/// <summary>
///     Shared helpers of the Description tests: parsing a diagram body, the context of a layout call, and the texts of
///     scene items.
/// </summary>
internal static class DescriptionTestHelpers
{
    /// <summary>
    ///     Parses the statements of a description diagram body (the lines between <c>@startuml</c> and
    ///     <c>@enduml</c>) as a component diagram.
    /// </summary>
    /// <param name="body">The lines.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The model.</returns>
    public static DescriptionModel Parse(string body, PlantUmlLimits? limits = null)
    {
        var source = Preprocessor.Run("@startuml\n" + body + "\n@enduml\n", null, limits ?? PlantUmlLimits.Default);
        var first = source.Block.Statements.First(s => s.Text.Length > 0);
        return DescriptionParser.Parse(source, new DetectedDiagram("component", true, "component", first.Location), limits ?? PlantUmlLimits.Default);
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
    ///     Gets the element of a model with an id.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="id">The id.</param>
    /// <returns>The element.</returns>
    public static DescriptionElement ElementOf(DescriptionModel model, string id) => model.Elements.Single(e => e.Id == id);

    /// <summary>
    ///     Draws the image of a leaf element of a model at the origin.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="id">The element id.</param>
    /// <returns>The image and its items.</returns>
    public static (DescriptionImage Image, IReadOnlyList<SceneItem> Items) Draw(DescriptionModel model, string id)
    {
        var image = DescriptionRenderer.Image(ElementOf(model, id), model.Theme, new TextLayout(TextLayout.CreateFontResolver()), null);
        return (image, image.Draw(0, 0));
    }

    /// <summary>
    ///     Gets the text of a scene line.
    /// </summary>
    /// <param name="text">The line.</param>
    /// <returns>The text.</returns>
    public static string TextOf(SceneText text) => string.Concat(text.Runs.Select(r => r.Text));
}
