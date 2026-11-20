using DemaConsulting.CanvasNet.PlantUml.Description;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using static DemaConsulting.CanvasNet.PlantUml.Tests.Description.DescriptionTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Description;

/// <summary>
///     Unit tests for the <c>DescriptionRenderer</c> unit: the images of the elements and containers of description
///     diagrams, with the sizes and offsets of PlantUML's pictures.
/// </summary>
public class DescriptionRendererTests
{
    /// <summary>The height of a line of 14 pixels (Liberation Sans, as PlantUML measures Arial).</summary>
    private const double LineHeight = 17.609;

    /// <summary>The offset of the baseline of a line of 14 pixels from its top.</summary>
    private const double Ascent = 14.533;

    /// <summary>
    ///     Validates the component: margins of 15, 25, 20, and 10 around the name, corners of 5, PlantUML's fill and
    ///     outline, and the icon of 15 by 10 with its two tabs 20 and 22 from the right.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_Image_Component_HasPlantUmlBoxAndIcon()
    {
        // Arrange
        var model = Parse("[A]");

        // Act
        var (image, items) = Draw(model, "A");

        // Assert
        var name = items.OfType<SceneText>().Single(t => t.Role == "description-name");
        Assert.Equal(name.Box.Width + 40, image.Width, 3);
        Assert.Equal(LineHeight + 30, image.Height, 3);
        Assert.Equal((15, 20 + Ascent), (name.X, Math.Round(name.Baseline, 3)));
        var outline = items.OfType<ScenePath>().Single(p => p.Role == "description");
        Assert.Equal((SkinTheme.CommonBackground, SkinTheme.LineColor, 0.5), (outline.Fill!.Value, outline.Stroke!.Color, outline.Stroke.Width));
        var icons = items.Where(i => i.Role == "description-icon").Select(i => Scene.BoundsOf([i])!.Value).ToList();
        Assert.Equal(
            [(image.Width - 20, 5.0, 15.0, 10.0), (image.Width - 22, 7.0, 4.0, 2.0), (image.Width - 22, 11.0, 4.0, 2.0)],
            icons.Select(b => (b.X, b.Y, b.Width, b.Height)));
        Assert.Equal(ClipShape.Rectangle, image.Clip.Shape);
    }

    /// <summary>
    ///     Validates the interface: a circle of radius 8 in a box of 18, the name 8 below the box and the stereotypes 8
    ///     above it, centered; links end on the circle's box, which is the center of the image.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_Image_Interface_IsACircleWithItsNameBelow()
    {
        // Arrange
        var model = Parse("interface Port <<api>>");

        // Act
        var (image, items) = Draw(model, "Port");

        // Assert
        var clip = image.Clip.Box;
        Assert.Equal((ClipShape.Ellipse, 18.0, 18.0), (image.Clip.Shape, clip.Width, clip.Height));
        Assert.Equal(image.Width / 2, clip.X + 9, 3);
        Assert.Equal(image.Height / 2, clip.Y + 9, 3);
        var circle = Scene.BoundsOf([items.Single(i => i.Role == "description")])!.Value;
        Assert.Equal((16.0, 16.0), (circle.Width, circle.Height));
        var name = items.OfType<SceneText>().Single(t => t.Role == "description-name");
        var stereo = items.OfType<SceneText>().Single(t => t.Role == "description-stereotype");
        Assert.Equal(clip.Y + 18 + 8, name.Box.Y, 3);
        Assert.Equal(clip.Y - 8, stereo.Box.Y + stereo.Box.Height, 3);
        Assert.Equal(image.Width / 2, name.Box.X + (name.Box.Width / 2), 3);
        Assert.True(stereo.Runs[0].Font.Italic);
    }

    /// <summary>
    ///     Validates that the stereotypes (italic, one per line, with a margin of 1 on each side) are centered over the
    ///     name, as PlantUML's picture places <c>«S1»</c>, <c>«S2»</c>, and <c>C4</c>.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_Image_Stereotypes_AreCenteredOverTheName()
    {
        // Arrange
        var model = Parse("component C4 <<S1>> <<S2>>");

        // Act
        var (image, items) = Draw(model, "C4");

        // Assert
        var texts = items.OfType<SceneText>().ToDictionary(TextOf, t => t);
        var stereoWidth = texts["\u00ABS1\u00BB"].Box.Width;
        Assert.Equal(stereoWidth + 2 + 40, image.Width, 2);
        Assert.Equal((3 * LineHeight) + 30, image.Height, 2);
        Assert.Equal(16, texts["\u00ABS1\u00BB"].X, 2);
        Assert.Equal(20 + LineHeight + Ascent, texts["\u00ABS2\u00BB"].Baseline, 2);
        Assert.Equal(15 + ((stereoWidth + 2 - texts["C4"].Box.Width) / 2), texts["C4"].X, 2);
        Assert.Equal(20 + (2 * LineHeight) + Ascent, texts["C4"].Baseline, 2);
    }

    /// <summary>
    ///     Validates the actor: the stereotypes over the stick figure (a head of radius 8 filled like the other
    ///     elements, arms 26 wide, 60 high with its line of half a pixel) over the name, all centered, as PlantUML draws
    ///     <c>«human»</c> and <c>Customer</c>; links end on the box.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_Image_Actor_IsAStickFigureWithItsNameBelow()
    {
        // Arrange
        var model = Parse("actor Customer <<human>>");

        // Act
        var (image, items) = Draw(model, "Customer");

        // Assert
        var name = items.OfType<SceneText>().Single(t => t.Role == "description-name");
        var stereo = items.OfType<SceneText>().Single(t => t.Role == "description-stereotype");
        var w = Math.Max(Math.Max(name.Box.Width, stereo.Box.Width + 2), 27);
        Assert.Equal(w, image.Width, 3);
        Assert.Equal((2 * LineHeight) + 60, image.Height, 0.002);
        Assert.Equal(ClipShape.Rectangle, image.Clip.Shape);
        var head = items.OfType<ScenePath>().Single(p => p.Role == "description");
        var headBox = Scene.BoundsOf([head])!.Value;
        Assert.Equal((w / 2) - 8, headBox.X, 3);
        Assert.Equal((LineHeight + 0.5, 16.0, 16.0), (Math.Round(headBox.Y, 3), Math.Round(headBox.Width, 3), Math.Round(headBox.Height, 3)));
        Assert.Equal((SkinTheme.CommonBackground, SkinTheme.LineColor, 0.5), (head.Fill!.Value, head.Stroke!.Color, head.Stroke.Width));
        var body = Scene.BoundsOf([items.Single(i => i.Role == "description-detail")])!.Value;
        Assert.Equal((w / 2) - 13, body.X, 0.002);
        Assert.Equal((LineHeight + 16.5, 26.0, 42.0), (Math.Round(body.Y, 3), Math.Round(body.Width, 3), Math.Round(body.Height, 3)));
        Assert.Equal((w - name.Box.Width) / 2, name.X, 3);
        Assert.Equal(LineHeight + 60 + Ascent, name.Baseline, 0.002);
        Assert.Equal(((w - stereo.Box.Width - 2) / 2) + 1, stereo.X, 3);
        Assert.Equal(Ascent, stereo.Baseline, 3);
        Assert.True(stereo.Runs[0].Font.Italic);
    }

    /// <summary>
    ///     Validates the business actor: the stick figure with the slash across its head that PlantUML draws, from
    ///     (-1.944, 7.760) to (7.761, -1.944) of the head's center.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_Image_BusinessActor_HasTheSlash()
    {
        // Arrange
        var model = Parse("actor/ Clerk");

        // Act
        var (image, items) = Draw(model, "Clerk");

        // Assert
        Assert.Equal(3, items.OfType<ScenePath>().Count());
        var slash = items.OfType<ScenePath>().Where(p => p.Role == "description-detail").First(p => p.Segments.Count == 2);
        var (cx, cy) = (image.Width / 2, 8.5);
        Assert.Equal(-1.944, slash.Segments[0].X - cx, 0.002);
        Assert.Equal(7.760, slash.Segments[0].Y - cy, 0.002);
        Assert.Equal(7.761, slash.Segments[1].X - cx, 0.002);
        Assert.Equal(-1.944, slash.Segments[1].Y - cy, 0.002);
        Assert.Null(slash.Fill);
    }

    /// <summary>
    ///     Validates the use case ellipses against PlantUML's pictures: the size of the smallest ellipse of the texts'
    ///     proportions around every line, grown by 6, and the offset of each line, for a name (<c>Browse</c>), a business
    ///     use case (<c>Audit</c>, margins of 7), stereotypes over a name (<c>«main»</c>, <c>U1</c>), and lines of
    ///     different widths, centered.
    /// </summary>
    /// <param name="body">The declaration.</param>
    /// <param name="id">The element id.</param>
    /// <param name="width">PlantUML's width.</param>
    /// <param name="height">PlantUML's height.</param>
    /// <param name="text">The text of the first line.</param>
    /// <param name="x">PlantUML's offset of the first line from the left.</param>
    /// <param name="baseline">PlantUML's offset of the first line's baseline from the top.</param>
    [Theory]
    [InlineData("usecase Browse", "Browse", 72.020, 30.904, "Browse", 12.668, 20.756)]
    [InlineData("usecase/ Audit", "Audit", 70.926, 30.904, "Audit", 19.508, 19.180)]
    [InlineData("usecase U1 <<main>>", "U1", 65.978, 50.084, "\u00ABmain\u00BB", 10.031, 24.970)]
    [InlineData("usecase \"line one\\nsecond line is longer\\n3\" as M", "M", 141.100, 61.918, "line one", 46.030, 19.016)]
    public void PlantUmlDescriptionRenderer_Image_UseCase_HasPlantUmlEllipse(string body, string id, double width, double height, string text, double x, double baseline)
    {
        // Arrange
        var model = Parse(body);

        // Act
        var (image, items) = Draw(model, id);

        // Assert
        Assert.Equal(width, image.Width, 1);
        Assert.Equal(height, image.Height, 1);
        Assert.Equal((ClipShape.Ellipse, image.Width, image.Height), (image.Clip.Shape, image.Clip.Box.Width, image.Clip.Box.Height));
        var outline = Scene.BoundsOf([items.Single(i => i.Role == "description")])!.Value;
        Assert.Equal(image.Width, outline.Width, 0.001);
        Assert.Equal(image.Height, outline.Height, 0.001);
        var first = items.OfType<SceneText>().First(t => TextOf(t) == text);
        Assert.Equal(x, first.X, 1);
        Assert.Equal(baseline, first.Baseline, 1);
    }

    /// <summary>
    ///     Validates the slash of a business use case: the chord PlantUML draws across the lower right of
    ///     <c>Audit</c>'s ellipse, and none on a use case.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_Image_BusinessUseCase_HasTheSlash()
    {
        // Arrange
        var model = Parse("usecase/ Audit\nusecase Plain");

        // Act
        var (_, items) = Draw(model, "Audit");
        var (_, plain) = Draw(model, "Plain");

        // Assert
        var slash = items.OfType<ScenePath>().Single(p => p.Role == "description-detail");
        Assert.Equal((68.1, 10.07), (Math.Round(slash.Segments[0].X, 1), Math.Round(slash.Segments[0].Y, 2)));
        Assert.Equal((48.78, 29.39), (Math.Round(slash.Segments[1].X, 2), Math.Round(slash.Segments[1].Y, 2)));
        Assert.DoesNotContain(plain, i => i.Role == "description-detail");
        Assert.Equal(SkinElement.UseCase, DescriptionRenderer.StyleOf(DescriptionSymbol.BusinessUseCase));
        Assert.Equal(SkinElement.Actor, DescriptionRenderer.StyleOf(DescriptionSymbol.BusinessActor));
        Assert.True(DescriptionRenderer.IsDrawn(DescriptionSymbol.UseCase, container: false));
    }

    /// <summary>
    ///     Validates the smallest enclosing circle: the circle of a rectangle's corners, of points on a line, of one
    ///     point, of no point, and of many points (every point inside, the same circle in any order).
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_EnclosingCircle_IsTheSmallest()
    {
        // Arrange
        var ring = Enumerable.Range(0, 20_000).Select(i => (X: 50 + (40 * Math.Cos(i * 0.001)), Y: 20 + (40 * Math.Sin(i * 0.001)))).ToList();
        var reversed = Enumerable.Reverse(ring).ToList();

        // Act
        var square = DescriptionRenderer.EnclosingCircle([(0, 0), (6, 0), (0, 8), (6, 8), (3, 4)]);
        var line = DescriptionRenderer.EnclosingCircle([(0, 0), (1, 1), (4, 4)]);
        var single = DescriptionRenderer.EnclosingCircle([(2, 3)]);
        var none = DescriptionRenderer.EnclosingCircle([]);
        var many = DescriptionRenderer.EnclosingCircle(ring);
        var again = DescriptionRenderer.EnclosingCircle(reversed);

        // Assert
        Assert.Equal((3.0, 4.0, 5.0), (Math.Round(square.X, 9), Math.Round(square.Y, 9), Math.Round(square.Radius, 9)));
        Assert.Equal((2.0, 2.0, Math.Round(Math.Sqrt(8), 9)), (Math.Round(line.X, 9), Math.Round(line.Y, 9), Math.Round(line.Radius, 9)));
        Assert.Equal((2.0, 3.0, 0.0), single);
        Assert.Equal((0.0, 0.0, 0.0), none);
        Assert.All(ring, p => Assert.True(Math.Sqrt(((p.X - many.X) * (p.X - many.X)) + ((p.Y - many.Y) * (p.Y - many.Y))) <= many.Radius + 1e-6));
        Assert.True(many.Radius <= 40 + 1e-6);
        Assert.Equal(Math.Round(many.Radius, 6), Math.Round(again.Radius, 6));
        Assert.Equal((0.2, 0.8, 1.0, 0.8), (DescriptionRenderer.EllipseRatio(100, 1), DescriptionRenderer.EllipseRatio(10, 100), DescriptionRenderer.EllipseRatio(0, 0), DescriptionRenderer.EllipseRatio(0, 5)));
    }
    /// <summary>
    ///     Validates the margins of the leaf symbols around their name (PlantUML's <c>USymbol</c> margins) and the
    ///     number of shapes of their outline.
    /// </summary>
    /// <param name="keyword">The keyword of the symbol.</param>
    /// <param name="extraWidth">The width beyond the name.</param>
    /// <param name="extraHeight">The height beyond the name.</param>
    /// <param name="left">The offset of the name from the left.</param>
    /// <param name="top">The offset of the name from the top.</param>
    /// <param name="shapes">The number of shapes of the outline.</param>
    [Theory]
    [InlineData("node", 40, 30, 15, 20, 4)]
    [InlineData("rectangle", 20, 20, 10, 10, 1)]
    [InlineData("frame", 40, 30, 15, 20, 2)]
    [InlineData("cloud", 30, 30, 15, 15, 1)]
    [InlineData("database", 20, 29, 10, 24, 2)]
    [InlineData("card", 20, 6, 10, 3, 1)]
    [InlineData("artifact", 30, 23, 10, 13, 4)]
    [InlineData("storage", 20, 20, 10, 10, 1)]
    [InlineData("file", 20, 20, 10, 10, 2)]
    [InlineData("queue", 20, 10, 5, 5, 2)]
    [InlineData("stack", 50, 20, 25, 10, 2)]
    [InlineData("agent", 20, 20, 10, 10, 1)]
    [InlineData("collections", 20, 20, 8, 8, 2)]
    [InlineData("label", 20, 20, 10, 10, 0)]
    public void PlantUmlDescriptionRenderer_Image_Symbols_HavePlantUmlMargins(string keyword, double extraWidth, double extraHeight, double left, double top, int shapes)
    {
        // Arrange
        var model = Parse(keyword + " \"A long element name\" as N");

        // Act
        var (image, items) = Draw(model, "N");

        // Assert
        var name = items.OfType<SceneText>().Single();
        Assert.Equal(name.Box.Width + extraWidth, image.Width, 3);
        Assert.Equal(LineHeight + extraHeight, image.Height, 3);
        Assert.Equal((left, top), (name.X, name.Box.Y));
        Assert.Equal(shapes, items.Count(i => i is ScenePath));
    }

    /// <summary>
    ///     Validates the folder and the package: a tab at the top left with margins of 10, 20, 13, and 10; an empty tab
    ///     of 40 by 15 for a folder with its name below it, and the name in bold in the tab of a package.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_Image_FolderAndPackage_HaveTheirTab()
    {
        // Arrange
        var model = Parse("folder F\npackage P");

        // Act
        var (folder, folderItems) = Draw(model, "F");
        var (package, packageItems) = Draw(model, "P");

        // Assert
        Assert.Equal((70, 15 + LineHeight + 23), (folder.Width, Math.Round(folder.Height, 3)));
        var folderName = folderItems.OfType<SceneText>().Single();
        Assert.Equal((10, 28.0), (folderName.X, folderName.Box.Y));
        var title = packageItems.OfType<SceneText>().Single();
        Assert.True(title.Runs[0].Font.Bold);
        Assert.Equal((10, 3.0), (title.X, title.Box.Y));
        Assert.Equal(title.Box.Width + 12 + 30, package.Width, 3);
        Assert.Equal(LineHeight + 23, package.Height, 3);
    }

    /// <summary>
    ///     Validates that a symbol without its own drawing is drawn as a rectangle with margins of 10, in the style of
    ///     the other description elements.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_Image_SymbolWithoutDrawing_IsARectangle()
    {
        // Arrange
        var model = Parse("action Act");

        // Act
        var (image, items) = Draw(model, "Act");

        // Assert
        Assert.False(DescriptionRenderer.IsDrawn(DescriptionSymbol.Action, container: false));
        Assert.False(DescriptionRenderer.IsDrawn(DescriptionSymbol.Process, container: true));
        Assert.True(DescriptionRenderer.IsDrawn(DescriptionSymbol.Component, container: true));
        Assert.Equal(SkinElement.Description, DescriptionRenderer.StyleOf(DescriptionSymbol.Action));
        var name = items.OfType<SceneText>().Single();
        Assert.Equal(name.Box.Width + 20, image.Width, 3);
        Assert.Single(items.OfType<ScenePath>());
    }

    /// <summary>
    ///     Validates that the colors of a declaration replace the theme's: the fill and outline of the box and of the
    ///     icon, and the text color.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_Image_Colors_ReplaceTheTheme()
    {
        // Arrange
        var model = Parse("component A #back:white;line:blue;text:green");

        // Act
        var (_, items) = Draw(model, "A");

        // Assert
        Assert.All(items.OfType<ScenePath>(), p => Assert.Equal((SkinTheme.White, new Canvas.Rgba32(0, 0, 0xFF, 255)), (p.Fill!.Value, p.Stroke!.Color)));
        Assert.Equal(new Canvas.Rgba32(0, 0x80, 0, 255), items.OfType<SceneText>().Single().Runs[0].Color);
    }

    /// <summary>
    ///     Validates the component container: a rectangle cluster's header and padding, an outline without fill one
    ///     pixel wide with the component icon, the stereotypes 13 from the top, and the bold title below them.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_ContainerImage_Component_HasIconAndTitle()
    {
        // Arrange
        var model = Parse("component K <<k>> {\n[A]\n}");
        var text = new TextLayout(TextLayout.CreateFontResolver());

        // Act
        var image = DescriptionRenderer.ContainerImage(ElementOf(model, "K"), model.Theme, text, null);
        var items = image.Draw(new SceneRect(0, 0, 100, 120));

        // Assert
        Assert.IsType<DescriptionContainerImage>(image);
        Assert.Equal((Math.Floor(2 * LineHeight) + 11, 16.0), (image.HeaderHeight, image.Padding));
        var outline = items.OfType<ScenePath>().First();
        Assert.Equal((null, 1.0), (outline.Fill, outline.Stroke!.Width));
        Assert.Equal(4, items.OfType<ScenePath>().Count());
        var stereo = items.OfType<SceneText>().Single(t => t.Role == "cluster-stereotype");
        var title = items.OfType<SceneText>().Single(t => t.Role == "cluster-title");
        Assert.Equal((13.0, 13 + LineHeight), (stereo.Box.Y, Math.Round(title.Box.Y, 3)));
        Assert.True(title.Runs[0].Font.Bold);
        Assert.Equal(50, title.Box.X + (title.Box.Width / 2), 3);
    }

    /// <summary>
    ///     Validates the other containers: the Svek cluster symbols with stereotypes of 14, and the colors of a
    ///     container: its own, else those a skin parameter of its symbol sets, else none.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_ContainerImage_Symbols_UseClusterOutlines()
    {
        // Arrange
        var model = Parse("skinparam nodeBackgroundColor #FFEEDD\nnode N <<vm>> {\n[A]\n}\ncomponent K #pink {\n[B]\n}\ncloud C {\n[D]\n}");
        var text = new TextLayout(TextLayout.CreateFontResolver());

        // Act
        var node = DescriptionRenderer.ContainerImage(ElementOf(model, "N"), model.Theme, text, null);
        var items = node.Draw(new SceneRect(0, 0, 100, 120));

        // Assert
        Assert.IsType<SvekClusterImage>(node);
        Assert.Equal(14, items.OfType<SceneText>().Single(t => t.Role == "cluster-stereotype").Runs[0].Font.Size);
        Assert.Equal(new Canvas.Rgba32(0xFF, 0xEE, 0xDD, 255), items.OfType<ScenePath>().First().Fill);
        Assert.Equal((new Canvas.Rgba32(0xFF, 0xC0, 0xCB, 255), (Canvas.Rgba32?)null), DescriptionRenderer.ContainerColors(ElementOf(model, "K"), model.Theme));
        Assert.Equal(((Canvas.Rgba32?)null, (Canvas.Rgba32?)null), DescriptionRenderer.ContainerColors(ElementOf(model, "C"), model.Theme));
    }

    /// <summary>
    ///     Validates the shapes of the deployment symbols that are more than a box: a storage's corners of 35, cut to
    ///     half the side (an ellipse when the storage is small); a stack's box filled without an outline between its
    ///     ledges; a collections symbol's back box 4 to the right and below; an artifact's document icon.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_Image_DeploymentOutlines_AreTheSymbols()
    {
        // Arrange
        var model = Parse("storage S\nstorage \"A very long storage name\" as L\nstack K\ncollections C\nartifact A");

        // Act
        var (small, smallItems) = Draw(model, "S");
        var (_, longItems) = Draw(model, "L");
        var (_, stackItems) = Draw(model, "K");
        var (collections, collectionsItems) = Draw(model, "C");
        var (artifact, artifactItems) = Draw(model, "A");

        // Assert
        var outline = smallItems.OfType<ScenePath>().Single();
        Assert.Equal((small.Width / 2, 0.0), (outline.Segments[0].X, outline.Segments[0].Y));
        Assert.Equal((35.0, 0.0), (longItems.OfType<ScenePath>().Single().Segments[0].X, longItems.OfType<ScenePath>().Single().Segments[0].Y));
        var box = stackItems.OfType<ScenePath>().Single(p => p.Role == "description");
        var ledges = stackItems.OfType<ScenePath>().Single(p => p.Role == "description-detail");
        Assert.Equal((SkinTheme.CommonBackground, (SceneStroke?)null), (box.Fill!.Value, box.Stroke));
        Assert.Equal(((Canvas.Rgba32?)null, 0.5), (ledges.Fill, ledges.Stroke!.Width));
        Assert.Equal((0.0, 0.0), (ledges.Segments[0].X, ledges.Segments[0].Y));
        var back = Scene.BoundsOf([collectionsItems.OfType<ScenePath>().Single(p => p.Role == "description")])!.Value;
        var front = Scene.BoundsOf([collectionsItems.OfType<ScenePath>().Single(p => p.Role == "description-detail")])!.Value;
        Assert.Equal((4.0, 4.0, collections.Width - 4, collections.Height - 4), (back.X, back.Y, back.Width, back.Height));
        Assert.Equal((0.0, 0.0), (front.X, front.Y));
        var sheet = Scene.BoundsOf([artifactItems.OfType<ScenePath>().First(p => p.Role == "description-icon")])!.Value;
        Assert.Equal((artifact.Width - 17, 5.0, 12.0, 14.0), (sheet.X, sheet.Y, sheet.Width, sheet.Height));
    }

    /// <summary>
    ///     Validates the hexagon: the texts 5 from the top and a quarter of the width from the left, in a box twice as
    ///     wide as them and 10 higher, outlined by a hexagon with corners in the middles of its sides.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_Image_Hexagon_IsTwiceAsWideAsItsTexts()
    {
        // Arrange
        var model = Parse("hexagon \"Port Adapter\" as H");

        // Act
        var (image, items) = Draw(model, "H");

        // Assert
        var name = items.OfType<SceneText>().Single();
        Assert.Equal((2 * name.Box.Width, LineHeight + 10), (image.Width, Math.Round(image.Height, 3)));
        Assert.Equal((image.Width / 4, 5.0), (name.X, name.Box.Y));
        var outline = items.OfType<ScenePath>().Single(p => p.Role == "description");
        Assert.Equal(7, outline.Segments.Count);
        Assert.Equal((0.0, image.Height / 2), (outline.Segments[0].X, outline.Segments[0].Y));
        Assert.Equal((image.Width / 4, 0.0), (outline.Segments[1].X, outline.Segments[1].Y));
    }

    /// <summary>
    ///     Validates the person: a head 0.42 times the square root of the body's area, centered over a body with margins
    ///     of 10 and corners as round as the head.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionRenderer_Image_Person_HasAHeadOverItsBody()
    {
        // Arrange
        var model = Parse("person Person");

        // Act
        var (image, items) = Draw(model, "Person");

        // Assert
        var name = items.OfType<SceneText>().Single();
        var (bodyWidth, bodyHeight) = (name.Box.Width + 20, name.Box.Height + 20);
        var head = Math.Sqrt(bodyWidth * bodyHeight) * 0.42;
        Assert.Equal(20.663, head, 3);
        Assert.Equal(bodyWidth, image.Width, 3);
        Assert.Equal(bodyHeight + head, image.Height, 3);
        var ring = Scene.BoundsOf([items.OfType<ScenePath>().Single(p => p.Role == "description")])!.Value;
        Assert.Equal((bodyWidth - head) / 2, ring.X, 3);
        Assert.Equal((0.0, Math.Round(head, 3)), (ring.Y, Math.Round(ring.Width, 3)));
        var body = Scene.BoundsOf([items.OfType<ScenePath>().Single(p => p.Role == "description-detail")])!.Value;
        Assert.Equal(head, body.Y, 3);
        Assert.Equal(10.0, name.X, 3);
        Assert.Equal(head + 10, name.Box.Y, 3);
    }

    /// <summary>
    ///     Validates the boundary, control, and entity: the sequence diagram's symbols (a circle of radius 12 with its
    ///     mark, 4 from the sides of a box of 49 or 32), the name below the box, centered; a control's arrow is outlined
    ///     one pixel wide.
    /// </summary>
    /// <param name="keyword">The keyword.</param>
    /// <param name="symbolWidth">The width of the symbol's box.</param>
    /// <param name="circleX">The center of the circle from the middle of the element.</param>
    [Theory]
    [InlineData("boundary", 49, 8.5)]
    [InlineData("control", 32, 0)]
    [InlineData("entity", 32, 0)]
    public void PlantUmlDescriptionRenderer_Image_SequenceSymbols_AreThoseOfSequenceDiagrams(string keyword, double symbolWidth, double circleX)
    {
        // Arrange
        var model = Parse(keyword + " X\n" + keyword + " \"A long symbol name\" as L");

        // Act
        var (small, smallItems) = Draw(model, "X");
        var (large, largeItems) = Draw(model, "L");

        // Assert
        Assert.Equal((symbolWidth, 32 + LineHeight), (small.Width, Math.Round(small.Height, 3)));
        Assert.Equal(largeItems.OfType<SceneText>().Single().Box.Width, large.Width, 3);
        var circle = Scene.BoundsOf([largeItems.OfType<ScenePath>().Single(p => p.Role == "description")])!.Value;
        Assert.Equal((24.0, 4.0), (circle.Width, circle.Y));
        Assert.Equal((large.Width / 2) + circleX, circle.X + 12, 3);
        Assert.Equal(2, smallItems.Count(i => i is ScenePath));
        Assert.Equal(32.0, largeItems.OfType<SceneText>().Single().Box.Y, 3);
        var mark = smallItems.OfType<ScenePath>().Single(p => p.Role == "description-detail");
        Assert.Equal(keyword == "control" ? 1.0 : 0.5, mark.Stroke!.Width);
    }

    /// <summary>
    ///     Validates the containers of the deployment symbols: a rectangle cluster's header and padding, the symbol's
    ///     outline unfilled and one pixel wide (a stack's box without an outline), and the stereotypes and the bold
    ///     title centered at the symbol's offsets.
    /// </summary>
    /// <param name="keyword">The keyword.</param>
    /// <param name="stereoTop">The top of the stereotypes.</param>
    /// <param name="gap">The space between the stereotypes and the title.</param>
    /// <param name="shapes">The number of shapes of the outline.</param>
    [Theory]
    [InlineData("artifact", 2, 0, 4)]
    [InlineData("storage", 5, 2, 1)]
    [InlineData("file", 2, 0, 2)]
    [InlineData("queue", 2, 0, 2)]
    [InlineData("stack", 13, 0, 2)]
    [InlineData("hexagon", 2, 0, 1)]
    public void PlantUmlDescriptionRenderer_ContainerImage_DeploymentSymbols_DrawTheirOutline(string keyword, double stereoTop, double gap, int shapes)
    {
        // Arrange
        var model = Parse(keyword + " K <<k>> {\nnode A\n}");
        var text = new TextLayout(TextLayout.CreateFontResolver());

        // Act
        var image = DescriptionRenderer.ContainerImage(ElementOf(model, "K"), model.Theme, text, null);
        var items = image.Draw(new SceneRect(0, 0, 100, 120));

        // Assert
        Assert.IsType<DescriptionContainerImage>(image);
        Assert.True(DescriptionRenderer.IsDrawn(ElementOf(model, "K").Symbol, container: true));
        Assert.Equal((Math.Floor(2 * LineHeight) + 11, 16.0), (image.HeaderHeight, image.Padding));
        var paths = items.OfType<ScenePath>().ToList();
        Assert.Equal(shapes, paths.Count);
        Assert.All(paths, p => Assert.Null(p.Fill));
        Assert.Equal(keyword == "stack" ? null : 1.0, paths[0].Stroke?.Width);
        var stereo = items.OfType<SceneText>().Single(t => t.Role == "cluster-stereotype");
        var title = items.OfType<SceneText>().Single(t => t.Role == "cluster-title");
        Assert.Equal((stereoTop, stereoTop + gap + LineHeight), (stereo.Box.Y, Math.Round(title.Box.Y, 3)));
        Assert.True(title.Runs[0].Font.Bold);
        Assert.Equal(50, title.Box.X + (title.Box.Width / 2), 3);
    }
}
