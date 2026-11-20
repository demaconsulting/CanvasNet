using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.State;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using static DemaConsulting.CanvasNet.PlantUml.Tests.State.StateTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.State;

/// <summary>
///     Unit tests for the <c>StateRenderer</c> unit: the sizes, outlines, and items of the states, pseudo-states, and
///     composite states against PlantUML's constants.
/// </summary>
public class StateRendererTests
{
    /// <summary>The fill of states (<c>#F1F1F1</c>).</summary>
    private static readonly Rgba32 StateFill = new(0xF1, 0xF1, 0xF1, 255);

    /// <summary>The fill of the start and end circles (<c>#222222</c>).</summary>
    private static readonly Rgba32 CircleFill = new(0x22, 0x22, 0x22, 255);

    /// <summary>
    ///     Validates a state without descriptions: at least 50 by 50, rounded with a radius of 12.5, the name centered 5
    ///     below the top, and the line 5 below the name.
    /// </summary>
    [Fact]
    public void PlantUmlStateRenderer_Image_SimpleState_HasPlantUmlBox()
    {
        // Arrange
        var model = Parse("state Idle\nstate \"A much longer name\" as Long");

        // Act
        var (small, items) = Draw(model, "Idle");
        var (wide, wideItems) = Draw(model, "Long");

        // Assert
        Assert.Equal((50.0, 50.0), (small.Width, small.Height));
        Assert.Equal(ClipShape.Rectangle, small.Clip.Shape);
        var box = items.OfType<ScenePath>().Single(p => p.Role == "state");
        Assert.Equal((StateFill, SkinTheme.LineColor, 0.5), (box.Fill!.Value, box.Stroke!.Color, box.Stroke.Width));
        var name = items.OfType<SceneText>().Single(t => t.Role == "state-name");
        Assert.Equal((25.0, 19.533), (name.X + (name.Box.Width / 2), Math.Round(name.Baseline, 3)));
        var line = Scene.BoundsOf([items.Single(i => i.Role == "state-separator")])!.Value;
        Assert.Equal(27.609, Math.Round(line.Y, 3));
        Assert.Equal(wideItems.OfType<SceneText>().Single().Box.Width + 20, wide.Width, 6);
    }

    /// <summary>
    ///     Validates a state with descriptions: the lines 5 from the left and 5 below the line, the box as tall as the
    ///     name and the lines with margins of 10, and a rule of four dashes drawn as a line 10 high.
    /// </summary>
    [Fact]
    public void PlantUmlStateRenderer_Image_Descriptions_AreDrawnBelowTheLine()
    {
        // Arrange
        var model = Parse("state Heating : the element is on\nHeating : keeps 21 degrees\nstate Ruled : first\nRuled : ----\nRuled : second");

        // Act
        var (image, items) = Draw(model, "Heating");
        var (ruled, ruledItems) = Draw(model, "Ruled");

        // Assert
        Assert.Equal(72.828, Math.Round(image.Height, 3));
        var lines = items.OfType<SceneText>().Where(t => t.Role == "state-description").ToList();
        Assert.Equal(["the element is on", "keeps 21 degrees"], lines.Select(TextOf));
        Assert.Equal((5.0, 47.143), (lines[0].X, Math.Round(lines[0].Baseline, 3)));
        Assert.Single(ruledItems, i => i.Role == "state-rule");
        Assert.Equal(Math.Round(image.Height + StateDescription.RuleHeight, 3), Math.Round(ruled.Height, 3));
    }

    /// <summary>
    ///     Validates a state drawn after <c>hide empty description</c>: at least 50 by 40, its name centered, without the
    ///     line; a state with descriptions keeps its line.
    /// </summary>
    [Fact]
    public void PlantUmlStateRenderer_Image_HideEmptyDescription_DropsTheLine()
    {
        // Arrange
        var model = Parse("hide empty description\nstate Off\nstate On : lit");

        // Act
        var (off, offItems) = Draw(model, "Off");
        var (_, onItems) = Draw(model, "On");

        // Assert
        Assert.Equal((50.0, 40.0), (off.Width, off.Height));
        Assert.DoesNotContain(offItems, i => i.Role == "state-separator");
        Assert.Equal(25.729, Math.Round(offItems.OfType<SceneText>().Single().Baseline, 3));
        Assert.Contains(onItems, i => i.Role == "state-separator");
    }

    /// <summary>
    ///     Validates the pseudo-states: their sizes, outlines, roles, and colors.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    /// <param name="id">The id of the element.</param>
    /// <param name="size">The width and height.</param>
    /// <param name="shape">The clip shape (a <see cref="ClipShape"/> name).</param>
    /// <param name="role">The role of its shape.</param>
    [Theory]
    [InlineData("[*] --> A", ".start.", 20, "Ellipse", "state-start")]
    [InlineData("A --> [*]", ".end.", 22, "Ellipse", "state-end")]
    [InlineData("state J <<junction>>", "J", 12, "Ellipse", "state-junction")]
    [InlineData("state C <<choice>>", "C", 24, "Diamond", "state-choice")]
    [InlineData("A --> [H]", ".historical.", 22, "Ellipse", "state-history")]
    [InlineData("A --> [H*]", ".deephistory.", 22, "Ellipse", "state-history")]
    public void PlantUmlStateRenderer_Image_PseudoStates_HavePlantUmlShapes(string declaration, string id, double size, string shape, string role)
    {
        // Arrange
        var model = Parse(declaration);

        // Act
        var (image, items) = Draw(model, id);

        // Assert
        Assert.Equal((size, size), (image.Width, image.Height));
        Assert.Equal(Enum.Parse<ClipShape>(shape), image.Clip.Shape);
        Assert.Contains(items, i => i.Role == role);
    }

    /// <summary>
    ///     Validates the start and end circles: the start filled and outlined <c>#222222</c>, the end a ring with a filled
    ///     circle of radius 6 inside.
    /// </summary>
    [Fact]
    public void PlantUmlStateRenderer_Image_StartAndEnd_HavePlantUmlColors()
    {
        // Arrange
        var model = Parse("[*] --> A\nA --> [*]");

        // Act
        var (_, start) = Draw(model, ".start.");
        var (_, end) = Draw(model, ".end.");

        // Assert
        var circle = Assert.IsType<ScenePath>(Assert.Single(start));
        Assert.Equal((CircleFill, CircleFill, 1.0), (circle.Fill!.Value, circle.Stroke!.Color, circle.Stroke.Width));
        var (ring, inner) = (Assert.IsType<ScenePath>(end[0]), Assert.IsType<ScenePath>(end[1]));
        Assert.Null(ring.Fill);
        Assert.Equal(CircleFill, inner.Fill);
        Assert.Equal(12.0, Math.Round(Scene.BoundsOf([inner])!.Value.Width, 6));
    }

    /// <summary>
    ///     Validates the fork and join bar: 80 by 8 filled <c>#555555</c> without an outline, 8 by 80 in a left to right
    ///     diagram.
    /// </summary>
    [Fact]
    public void PlantUmlStateRenderer_Image_ForkBar_LiesAcrossTheRanks()
    {
        // Arrange
        var down = Parse("state F <<fork>>");
        var across = Parse("left to right direction\nstate F <<fork>>");

        // Act
        var (bar, items) = Draw(down, "F");
        var (sideways, _) = Draw(across, "F");

        // Assert
        Assert.Equal((80.0, 8.0), (bar.Width, bar.Height));
        Assert.Equal((8.0, 80.0), (sideways.Width, sideways.Height));
        var path = Assert.IsType<ScenePath>(Assert.Single(items));
        Assert.Equal((new Rgba32(0x55, 0x55, 0x55, 255), (SceneStroke?)null), (path.Fill!.Value, path.Stroke));
    }

    /// <summary>
    ///     Validates the history circles: <c>H</c> and <c>H*</c> centered in a circle of 22 filled <c>#F1F1F1</c>.
    /// </summary>
    [Fact]
    public void PlantUmlStateRenderer_Image_History_HasItsMark()
    {
        // Arrange
        var model = Parse("A --> [H]\nA --> [H*]");

        // Act
        var (_, shallow) = Draw(model, ".historical.");
        var (_, deep) = Draw(model, ".deephistory.");

        // Assert
        Assert.Equal("H", TextOf(shallow.OfType<SceneText>().Single()));
        Assert.Equal("H*", TextOf(deep.OfType<SceneText>().Single()));
        Assert.Equal(16.729, Math.Round(shallow.OfType<SceneText>().Single().Baseline, 3));
        Assert.Equal(StateFill, shallow.OfType<ScenePath>().Single().Fill);
    }

    /// <summary>
    ///     Validates the colors of a declaration: the fill and the line color replace the theme's.
    /// </summary>
    [Fact]
    public void PlantUmlStateRenderer_Image_Colors_ReplaceTheTheme()
    {
        // Arrange
        var model = Parse("state A #FFDDDD ##blue");

        // Act
        var (_, items) = Draw(model, "A");

        // Assert
        var box = items.OfType<ScenePath>().Single(p => p.Role == "state");
        Assert.Equal((new Rgba32(0xFF, 0xDD, 0xDD, 255), new Rgba32(0, 0, 0xFF, 255)), (box.Fill!.Value, box.Stroke!.Color));
    }

    /// <summary>
    ///     Validates the image of a composite state: the header as tall as the name with margins of 5, at least as wide
    ///     as the name with margins of 10, padded 10, drawn as a filled header band, a rounded outline, a line under the
    ///     name, and the centered title; a composite state with its own color is filled whole.
    /// </summary>
    [Fact]
    public void PlantUmlStateRenderer_CompositeImage_HasHeaderAndOutline()
    {
        // Arrange
        var model = Parse("state Active {\nstate A\n}\nstate Colored #pink {\nstate B\n}\nstate Described {\nstate C\n}\nDescribed : text");
        var text = new TextLayout(TextLayout.CreateFontResolver());

        // Act
        var image = StateRenderer.CompositeImage(ElementOf(model, "Active"), model.Theme, text, null);
        var colored = StateRenderer.CompositeImage(ElementOf(model, "Colored"), model.Theme, text, null);
        var described = StateRenderer.CompositeImage(ElementOf(model, "Described"), model.Theme, text, null);
        var items = image.Draw(new SceneRect(0, 0, 200, 120));

        // Assert
        Assert.Equal(27.609, Math.Round(image.HeaderHeight, 3));
        Assert.Equal(StateRenderer.CompositePadding, image.Padding);
        Assert.True(image.MinWidth > 40);
        Assert.Equal(3, items.Count(i => i.Role == "cluster"));
        Assert.Equal(StateFill, items.OfType<ScenePath>().First().Fill);
        Assert.Null(items.OfType<ScenePath>().ElementAt(1).Fill);
        var title = items.OfType<SceneText>().Single(t => t.Role == "cluster-title");
        Assert.Equal((100.0, 19.533), (Math.Round(title.X + (title.Box.Width / 2), 3), Math.Round(title.Baseline, 3)));
        Assert.NotNull(colored.Draw(new SceneRect(0, 0, 100, 100)).OfType<ScenePath>().ElementAt(1).Fill);
        Assert.True(described.HeaderHeight > image.HeaderHeight + 17);
        Assert.Contains(described.Draw(new SceneRect(0, 0, 100, 100)), i => i.Role == "state-description");
    }

    /// <summary>
    ///     Validates the helpers: a rule is four or more dashes, and the header band has a rounded top and a square
    ///     bottom.
    /// </summary>
    [Fact]
    public void PlantUmlStateRenderer_Helpers_RuleAndRoundedTop()
    {
        // Act
        var band = StateRenderer.RoundedTop(0, 0, 100, 30, 12.5);

        // Assert
        Assert.True(StateRenderer.IsRule("----"));
        Assert.True(StateRenderer.IsRule("  ------ "));
        Assert.False(StateRenderer.IsRule("---"));
        Assert.False(StateRenderer.IsRule("--x--"));
        Assert.Equal(2, band.Count(s => s.Kind == SceneSegmentKind.CubicTo));
        Assert.Equal(new SceneRect(0, 0, 100, 30), Scene.BoundsOf([new ScenePath(band) { Role = "band" }])!.Value);
    }
}
