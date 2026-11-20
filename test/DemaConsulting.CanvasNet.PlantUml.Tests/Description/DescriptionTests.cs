using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using static DemaConsulting.CanvasNet.PlantUml.Tests.Description.DescriptionTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Description;

/// <summary>
///     Subsystem tests of the Description subsystem: a description diagram read by <c>DescriptionParser</c> into a
///     <c>DescriptionModel</c>, whose elements and containers <c>DescriptionRenderer</c> draws, laid out by the Svek
///     engine.
/// </summary>
public class DescriptionTests
{
    /// <summary>
    ///     Validates the whole subsystem on a small model: components, interfaces, symbols, nested containers, notes,
    ///     and labeled links are read, lowered, laid out, and drawn with their roles and ids, and drawing twice gives the
    ///     same scene.
    /// </summary>
    [Fact]
    public void PlantUmlDescription_ReadLayoutRender_DrawsTheModel()
    {
        // Arrange
        const string Body = "title Shop\npackage Front {\n[Web] <<ui>>\n}\nnode Server {\ncomponent API {\n[Orders]\n}\ndatabase Store\n}\ncloud CDN\n() HTTP\n[Web] --> HTTP\nHTTP - Orders\nOrders ..> Store : sql\nWeb --> CDN\nnote right of Store : main\n[Orders] --( Payments";
        var model = Parse(Body);

        // Act
        var first = model.Layout(Context());
        var second = model.Layout(Context());

        // Assert
        Assert.Equal((6, 5, 3), (model.NodeCount, model.EdgeCount, model.SubgraphCount));
        Assert.Contains(first.Items, i => i is SceneText { Role: "description-name", Text: "Web" });
        Assert.Contains(first.Items, i => i is SceneText { Role: "description-stereotype", Text: "\u00ABui\u00BB" });
        Assert.Contains(first.Items, i => i.Role == "description-icon" && i.Id == "Front.Web");
        Assert.Contains(first.Items, i => i.Role == "description" && i.Id == "HTTP");
        Assert.Contains(first.Items, i => i.Role == "description-detail" && i.Id == "Server.Store");
        Assert.Contains(first.Items, i => i is SceneText { Role: "cluster-title", Text: "API" });
        Assert.Contains(first.Items, i => i is SceneText { Role: "note-text", Text: "main" });
        Assert.Contains(first.Items, i => i is SceneText { Role: "link-label", Text: "sql" });
        Assert.Contains(first.Items, i => i is SceneText { Role: "title", Text: "Shop" });
        Assert.Equal(first.ViewBox, second.ViewBox);
        Assert.Equal(first.Items.Count, second.Items.Count);
        Assert.Empty(model.Reports);
    }

    /// <summary>
    ///     Validates that the reports of the parser are kept by the model in text order, and that the theme of the
    ///     diagram's skin parameters reaches the drawing.
    /// </summary>
    [Fact]
    public void PlantUmlDescription_SkinAndReports_ReachTheDrawing()
    {
        // Arrange
        var model = Parse("skinparam componentBackgroundColor #FFEEDD\nskinparam componentBorderColor #884422\nskinparam interfaceFontColor #0000FF\n[A] --> I\naction X\n[A] -(0)- [B]");

        // Act
        var scene = model.Layout(Context());

        // Assert
        var box = scene.Items.OfType<ScenePath>().First(p => p.Role == "description" && p.Id == "A");
        Assert.Equal((new Canvas.Rgba32(0xFF, 0xEE, 0xDD, 255), new Canvas.Rgba32(0x88, 0x44, 0x22, 255)), (box.Fill!.Value, box.Stroke!.Color));
        Assert.Equal(new Canvas.Rgba32(0, 0, 0xFF, 255), scene.Items.OfType<SceneText>().Single(t => t.Role == "description-name" && t.Id == "I").Runs[0].Color);
        Assert.Equal([6, 7], model.Reports.Select(r => r.Location.Line));
    }

    /// <summary>
    ///     Validates the whole subsystem on a use case diagram: actors (a name only written in a link among them) and use
    ///     cases in a boundary are read, laid out with links that end on the ellipses, and drawn as stick figures and
    ///     ellipses with an «include» label and the actor and use case skin parameters.
    /// </summary>
    [Fact]
    public void PlantUmlDescription_UseCase_DrawsActorsAndUseCases()
    {
        // Arrange
        const string Body = "skinparam usecaseBackgroundColor #DDEEFF\nskinparam actorBorderColor #885500\nactor Customer\nrectangle Shop {\nusecase Checkout\n(Pay)/\n}\nCustomer --> Checkout\nClerk --> Pay\nCheckout ..> Pay : <<include>>";
        var model = Parse(Body);

        // Act
        var scene = model.Layout(Context());

        // Assert
        Assert.Equal((4, 3, 1), (model.NodeCount, model.EdgeCount, model.SubgraphCount));
        var head = scene.Items.OfType<ScenePath>().Single(p => p.Role == "description" && p.Id == "Clerk");
        Assert.Equal(new Canvas.Rgba32(0x88, 0x55, 0x00, 255), head.Stroke!.Color);
        var ellipse = scene.Items.OfType<ScenePath>().Single(p => p.Role == "description" && p.Id == "Shop.Checkout");
        Assert.Equal(new Canvas.Rgba32(0xDD, 0xEE, 0xFF, 255), ellipse.Fill);
        Assert.Contains(scene.Items, i => i.Role == "description-detail" && i.Id == "Shop.Pay");
        Assert.Contains(scene.Items, i => i is SceneText { Role: "link-label", Text: "\u00ABinclude\u00BB" });
        Assert.Contains(scene.Items, i => i is SceneText { Role: "cluster-title", Text: "Shop" });
        Assert.Empty(model.Reports);
    }
}
