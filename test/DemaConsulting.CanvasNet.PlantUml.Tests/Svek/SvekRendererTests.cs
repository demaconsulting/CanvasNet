using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using static DemaConsulting.CanvasNet.PlantUml.Tests.Svek.SvekTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Svek;

/// <summary>
///     Unit tests of the SvekRenderer unit: <see cref="SvekRenderer"/> (the scene, the links with their styles,
///     decorations, and labels, the notes with their pointers) and <see cref="SvekClusterImage"/> (the container
///     symbols).
/// </summary>
public class SvekRendererTests
{
    /// <summary>The text layout of the tests.</summary>
    private static readonly TextLayout Text = new(TextLayout.CreateFontResolver());

    /// <summary>
    ///     Validates the scene of a placed diagram: the margins around the placement, the theme's background, and the
    ///     paint order (clusters, entities, notes, links) with the entity images drawn at their places.
    /// </summary>
    [Fact]
    public void PlantUmlSvekRenderer_Render_MarginsBackgroundAndOrder()
    {
        // Arrange
        var package = new SvekCluster("P") { Image = SvekRenderer.ClusterImage(ClusterSymbol.Rectangle, "P", "P", [], SkinTheme.Default, Text, null, default) };
        var (a, b) = (Entity("A", parent: package), Entity("B"));
        var note = new SvekNote("N", "note") { Target = b };
        var diagram = new SvekDiagram { Entities = [a, b], Clusters = [package], Notes = [note], Links = [new SvekLink("l", a, b)] };
        var placement = Place(diagram);

        // Act
        var scene = SvekRenderer.Render(diagram, placement);

        // Assert
        Assert.Equal(new SceneRect(0, 0, placement.Width + 21, placement.Height + 28), scene.ViewBox);
        Assert.Equal(SkinColors.White, scene.Background);
        var roles = scene.Items.Select(i => i.Role).ToList();
        Assert.True(roles.IndexOf("cluster") < roles.IndexOf("entity"));
        Assert.True(roles.LastIndexOf("entity") < roles.IndexOf("note"));
        Assert.True(roles.LastIndexOf("note-text") < roles.IndexOf("link"));
        var drawnA = Scene.BoundsOf(scene.Items.Where(i => i.Role == "entity" && i.Id == "A"))!.Value;
        var placedA = BoxOf(placement, "A");
        Assert.Equal((placedA.X + 6, placedA.Y + 12), (drawnA.X, drawnA.Y));
    }

    /// <summary>
    ///     Validates the strokes of the line styles: solid 1, dashed (7, 7), dotted (1, 3), bold 2, a thickness, and a
    ///     color.
    /// </summary>
    [Fact]
    public void PlantUmlSvekRenderer_DrawLink_LineStyles()
    {
        // Arrange
        var red = new Rgba32(255, 0, 0, 255);
        SceneStroke StrokeOf(SvekLink link) =>
            ((ScenePath)SvekRenderer.DrawLink(new PlacedLink(link, [new(0, 0), new(0, 100)]), SkinTheme.Default, 0, 0).Single(i => i.Role == "link")).Stroke!;
        var (a, b) = (Entity("A"), Entity("B"));

        // Act
        var solid = StrokeOf(new SvekLink("s", a, b));
        var dashed = StrokeOf(new SvekLink("d", a, b) { Style = LinkStyle.Dashed });
        var dotted = StrokeOf(new SvekLink("o", a, b) { Style = LinkStyle.Dotted });
        var bold = StrokeOf(new SvekLink("b", a, b) { Style = LinkStyle.Bold });
        var thick = StrokeOf(new SvekLink("t", a, b) { Thickness = 3, Color = red });

        // Assert
        Assert.Equal((SkinTheme.LineColor, 1.0), (solid.Color, solid.Width));
        Assert.Null(solid.Dash);
        Assert.Equal([7, 7], dashed.Dash!);
        Assert.Equal([1, 3], dotted.Dash!);
        Assert.Equal(2, bold.Width);
        Assert.Equal((red, 3.0), (thick.Color, thick.Width));
    }

    /// <summary>
    ///     Validates the decorations of a link: drawn at each end with their fills, and the line stopping at their
    ///     contact length.
    /// </summary>
    [Fact]
    public void PlantUmlSvekRenderer_DrawLink_DecorationsAndShortenedLine()
    {
        // Arrange
        var link = new SvekLink("l", Entity("A"), Entity("B")) { TailDecor = LinkDecor.Composition, HeadDecor = LinkDecor.Arrow };

        // Act
        var items = SvekRenderer.DrawLink(new PlacedLink(link, [new(0, 0), new(0, 100)]), SkinTheme.Default, 0, 0);

        // Assert
        var line = (ScenePath)items.Single(i => i.Role == "link");
        Assert.Equal((0, 12), (line.Segments[0].X, line.Segments[0].Y));
        Assert.Equal(95, line.Segments[^1].Y, 9);
        var tail = (ScenePath)items.Single(i => i.Role == "link-tail");
        var head = (ScenePath)items.Single(i => i.Role == "link-head");
        Assert.Equal(SkinTheme.LineColor, tail.Fill);
        Assert.Equal(SkinTheme.LineColor, head.Fill);
        Assert.Equal(100, Scene.BoundsOf([head])!.Value.Bottom, 9);
        Assert.Equal(0, Scene.BoundsOf([tail])!.Value.Y, 9);
        Assert.All(items, i => Assert.Equal("l", i.Id));
    }

    /// <summary>
    ///     Validates that a link that is not drawn (hidden, a note pointer, without a route) draws nothing.
    /// </summary>
    [Fact]
    public void PlantUmlSvekRenderer_DrawLink_NotDrawn_DrawsNothing()
    {
        // Arrange
        var link = new SvekLink("l", Entity("A"), Entity("B"));

        // Act
        var hidden = SvekRenderer.DrawLink(new PlacedLink(link, [new(0, 0), new(0, 10)]) { Drawn = false }, SkinTheme.Default, 0, 0);
        var unrouted = SvekRenderer.DrawLink(new PlacedLink(link, []), SkinTheme.Default, 0, 0);

        // Assert
        Assert.Empty(hidden);
        Assert.Empty(unrouted);
    }

    /// <summary>
    ///     Validates the labels of a link: the middle label after its direction triangle, and the end labels, in the
    ///     font of the link labels.
    /// </summary>
    [Fact]
    public void PlantUmlSvekRenderer_DrawLink_LabelsAndTriangle()
    {
        // Arrange
        var (a, b) = (Entity("A"), Entity("B"));
        var link = new SvekLink("l", a, b) { Label = "uses", LabelArrow = LinkLabelArrow.Forward, TailLabel = "1", HeadLabel = "*" };
        var placed = LinkOf(Place(new SvekDiagram { Entities = [a, b], Links = [link] }), "l");

        // Act
        var items = SvekRenderer.DrawLink(placed, SkinTheme.Default, 0, 0);

        // Assert
        var label = (SceneText)items.Single(i => i.Role == "link-label");
        Assert.Equal(("uses", 13.0), (label.Text, label.Runs[0].Font.Size));
        Assert.Equal(placed.Label!.Box.X + 1 + 13, label.X, 9);
        var triangle = (ScenePath)items.Single(i => i.Role == "link-label-arrow");
        var bounds = Scene.BoundsOf([triangle])!.Value;
        Assert.True(bounds.Bottom > bounds.Y + 4);
        Assert.True(bounds.Right <= label.X);
        Assert.Equal("1", ((SceneText)items.Single(i => i.Role == "link-tail-label")).Text);
        Assert.Equal("*", ((SceneText)items.Single(i => i.Role == "link-head-label")).Text);
    }

    /// <summary>
    ///     Validates the folder of a package: the header band of its title, the tab as wide as the title plus 6 with its
    ///     slope of 7, and the bold title 4 from the left.
    /// </summary>
    [Fact]
    public void PlantUmlSvekRenderer_ClusterImage_Folder_MatchesPlantUml()
    {
        // Arrange
        var image = SvekRenderer.ClusterImage(ClusterSymbol.Folder, "geo", "geo", [], SkinTheme.Default, Text, null, default);
        var box = new SceneRect(123.5, 50.609, 110, 108);

        // Act
        var items = image.Draw(box);

        // Assert: PlantUML's geo package of the class probe has its tab line at 74.219 and its title at 127.5
        var title = (SceneText)items.Single(i => i.Role == "cluster-title");
        Assert.Equal((127.5, true), (title.X, title.Runs[0].Font.Bold));
        Assert.Equal(67.143, title.Baseline, 1);
        var tabLine = (ScenePath)items.Where(i => i.Role == "cluster").ElementAt(1);
        Assert.Equal(74.219, tabLine.Segments[0].Y, 1);
        Assert.Equal(123.5 + title.Box.Width + 6 + 7, tabLine.Segments[1].X, 9);
        Assert.Equal((SkinTheme.Black, 1.5), (((ScenePath)items[0]).Stroke!.Color, ((ScenePath)items[0]).Stroke!.Width));
        Assert.Equal((Math.Floor(title.Box.Height) + 11, 16.0), (image.HeaderHeight, image.Padding));
    }

    /// <summary>
    ///     Validates that every container symbol draws its outline inside its box with its title, and the padding and
    ///     extra header of the node and database symbols.
    /// </summary>
    /// <param name="symbol">The symbol (a <see cref="ClusterSymbol"/> name).</param>
    [Theory]
    [InlineData("Rectangle")]
    [InlineData("Frame")]
    [InlineData("Node")]
    [InlineData("Database")]
    [InlineData("Cloud")]
    [InlineData("Card")]
    public void PlantUmlSvekRenderer_ClusterImage_EverySymbol_DrawsInsideItsBox(string symbol)
    {
        // Arrange
        var kind = Enum.Parse<ClusterSymbol>(symbol);
        var image = SvekRenderer.ClusterImage(kind, "C", "Title", ["stereo"], SkinTheme.Default, Text, null, default);
        var box = new SceneRect(10, 20, 160, 120);

        // Act
        var items = image.Draw(box);

        // Assert
        var outline = Scene.BoundsOf(items.Where(i => i.Role == "cluster"))!.Value;
        var slack = kind == ClusterSymbol.Cloud ? 15 : 1e-6;
        Assert.True(Contains(new SceneRect(box.X - slack, box.Y - slack, box.Width + (2 * slack), box.Height + (2 * slack)), outline));
        Assert.Contains(items, i => i is SceneText { Role: "cluster-title", Text: "Title" });
        Assert.Contains(items, i => i is SceneText { Role: "cluster-stereotype", Text: "\u00ABstereo\u00BB" });
        Assert.Equal(kind == ClusterSymbol.Node ? 20 : 16, image.Padding);
        var titleHeight = items.OfType<SceneText>().Sum(t => t.Box.Height);
        var extra = kind switch { ClusterSymbol.Node => 5, ClusterSymbol.Database => 15, _ => 0 };
        Assert.Equal(Math.Floor(titleHeight + extra) + 11, image.HeaderHeight);
    }

    /// <summary>
    ///     Validates that a cluster image without an outline draws nothing and reserves no space, and that a cloud is
    ///     the same for the same box.
    /// </summary>
    [Fact]
    public void PlantUmlSvekRenderer_ClusterImage_NoneAndCloud()
    {
        // Arrange
        var none = SvekRenderer.ClusterImage(ClusterSymbol.None, "R", "Region", [], SkinTheme.Default, Text, null, default);

        // Act
        var first = SvekRenderer.CloudOutline(0, 0, 150, 120);
        var second = SvekRenderer.CloudOutline(0, 0, 150, 120);
        var small = SvekRenderer.CloudOutline(5, 5, 60, 40);

        // Assert
        Assert.Empty(none.Draw(new SceneRect(0, 0, 50, 50)));
        Assert.Equal((0.0, 0.0, 0.0), (none.HeaderHeight, none.Padding, none.MinWidth));
        Assert.Equal(first, second);
        Assert.True(first.Count > 20);
        Assert.Equal(SceneSegmentKind.Close, small[^1].Kind);
    }

    /// <summary>
    ///     Validates the notes: the folded corner, the text 6 from the left and 5 from the top, and the pointer from each
    ///     side touching its tip.
    /// </summary>
    /// <param name="side">The side of the pointer (a <see cref="NotePosition"/> name), or empty for none.</param>
    [Theory]
    [InlineData("")]
    [InlineData("Left")]
    [InlineData("Right")]
    [InlineData("Top")]
    [InlineData("Bottom")]
    public void PlantUmlSvekRenderer_DrawNote_PointerFromEachSide(string side)
    {
        // Arrange
        var box = new SceneRect(100, 100, 60, 30);
        NotePosition? position = side.Length == 0 ? null : Enum.Parse<NotePosition>(side);
        var (pointerBase, tip) = position switch
        {
            NotePosition.Left => (new SvekPoint(100, 115), new SvekPoint(80, 115)),
            NotePosition.Right => (new SvekPoint(160, 115), new SvekPoint(180, 115)),
            NotePosition.Top => (new SvekPoint(130, 100), new SvekPoint(130, 80)),
            NotePosition.Bottom => (new SvekPoint(130, 130), new SvekPoint(130, 150)),
            _ => (default, default),
        };
        var text = Text.Layout("note", new SceneFont("SansSerif", 13, false, false), SkinColors.Black);
        var placed = new PlacedNote(new SvekNote("N", "note"), text, box) { PointerSide = position, PointerBase = pointerBase, PointerTip = tip };

        // Act
        var items = SvekRenderer.DrawNote(placed, SkinTheme.Default, 0, 0);

        // Assert
        var outline = (ScenePath)items[0];
        Assert.Equal(SkinColors.Note, outline.Fill);
        Assert.Equal(position is null ? 5 : 8, outline.Segments.Count(s => s.Kind != SceneSegmentKind.Close));
        if (position is not null)
        {
            Assert.Contains(outline.Segments, s => Math.Abs(s.X - tip.X) < 1e-9 && Math.Abs(s.Y - tip.Y) < 1e-9);
        }

        var fold = Scene.BoundsOf([items[1]])!.Value;
        Assert.Equal(new SceneRect(150, 100, 10, 10), fold);
        var noteText = (SceneText)items.Single(i => i.Role == "note-text");
        Assert.Equal(106, noteText.X);
    }
}
