using DemaConsulting.CanvasNet.PlantUml.Class;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using static DemaConsulting.CanvasNet.PlantUml.Tests.Class.ClassTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Class;

/// <summary>
///     Subsystem tests of the Class subsystem: a class diagram read by <c>ClassParser</c> into a <c>ClassModel</c>,
///     whose entities <c>ClassRenderer</c> draws, laid out by the Svek engine.
/// </summary>
public class ClassTests
{
    /// <summary>
    ///     Validates the whole subsystem on a small model: the kinds, members, packages, relationships, and notes are
    ///     read, lowered, laid out, and drawn with their roles and ids, and drawing twice gives the same scene.
    /// </summary>
    [Fact]
    public void PlantUmlClass_ReadLayoutRender_DrawsTheModel()
    {
        // Arrange
        const string Body = "title Shop\npackage sales {\n  abstract class Order {\n    - id : long\n    + total() : decimal\n  }\n  enum Status {\n    OPEN\n    PAID\n  }\n}\ninterface Payable\nOrder ..|> Payable\nOrder \"1\" *-- \"*\" Line : contains >\nOrder --> Status\nnote right of Payable : paid later\nobject cart\nmap prices {\n  apple => 1\n}\ncart ..> prices";
        var model = Parse(Body);

        // Act
        var first = model.Layout(Context());
        var second = model.Layout(Context());

        // Assert
        Assert.Equal(["sales.Order", "sales.Status", "Payable", "Line", "cart", "prices"], model.Entities.Select(e => e.Id));
        Assert.Equal(6, first.Items.Count(i => i.Role == "class"));
        Assert.Equal(4, first.Items.Count(i => i.Role == "class-spot"));
        Assert.Contains(first.Items, i => i is SceneText { Role: "class-name", Text: "Order" } t && t.Runs[0].Font.Italic);
        Assert.Contains(first.Items, i => i is SceneText { Role: "class-member", Text: "OPEN" });
        Assert.Equal(2, first.Items.Count(i => i.Role == "class-icon"));
        Assert.Contains(first.Items, i => i.Role == "cluster" && i.Id == "sales");
        Assert.Contains(first.Items, i => i is SceneText { Role: "map-key", Text: "apple" });
        Assert.Contains(first.Items, i => i is SceneText { Role: "note-text", Text: "paid later" });
        Assert.Contains(first.Items, i => i is SceneText { Role: "link-label", Text: "contains" });
        Assert.Contains(first.Items, i => i is SceneText { Role: "title", Text: "Shop" });
        Assert.Equal(first.ViewBox, second.ViewBox);
        Assert.Equal(first.Items.Count, second.Items.Count);
    }

    /// <summary>
    ///     Validates that the reports of the parser are kept by the model in text order, and that the theme of the
    ///     diagram's skin parameters reaches the drawing.
    /// </summary>
    [Fact]
    public void PlantUmlClass_SkinAndReports_ReachTheDrawing()
    {
        // Arrange
        var model = Parse("skinparam classBackgroundColor #FFEEDD\nskinparam classBorderColor #884422\nskinparam classAttributeFontColor #0000FF\nclass A {\n  x : int\n}\nA }o-- B\nremove B");

        // Act
        var scene = model.Layout(Context());

        // Assert
        var box = scene.Items.OfType<ScenePath>().First(p => p.Role == "class" && p.Id == "A");
        Assert.Equal((new Canvas.Rgba32(0xFF, 0xEE, 0xDD, 255), new Canvas.Rgba32(0x88, 0x44, 0x22, 255)), (box.Fill!.Value, box.Stroke!.Color));
        Assert.Equal(new Canvas.Rgba32(0, 0, 0xFF, 255), scene.Items.OfType<SceneText>().Single(t => t.Role == "class-member").Runs[0].Color);
        Assert.Equal([8, 9], model.Reports.Select(r => r.Location.Line));
    }
}
