// cspell:ignore scenegraph
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Tests of the title and subtitle builder: the Vega-Lite 6 defaults, the orients and anchors, and the extent.
/// </summary>
public class TitleBuilderTests
{
    /// <summary>The measurer shared by the tests (bundled fonts).</summary>
    private static readonly TextMeasurer Measurer = new();

    /// <summary>
    ///     Builds the title of a specification with the default configuration.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="unitOrLayer">Whether the view is single or layered.</param>
    /// <returns>The block, or null when there is no title.</returns>
    private static TitleBlock? Build(string json, bool unitOrLayer = true)
    {
        var tree = SpecParser.Parse(json);
        return new TitleBuilder(Measurer).Build(tree.Root["title"], ConfigResolver.Resolve(tree, null), unitOrLayer);
    }

    /// <summary>
    ///     Measures a string with the bundled font the way the layout does, independently of the unit under test.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="size">The font size.</param>
    /// <param name="bold">Whether the bold face is used.</param>
    /// <returns>The advance width.</returns>
    private static double Width(string text, float size, bool bold) =>
        TextRenderer.MeasureText(text, SystemFontCatalog.LoadBundledFallback(false, false, bold, false), size).Width;

    /// <summary>
    ///     Validates the defaults, then each orient and anchor, the subtitle and the extent that results.
    /// </summary>
    [Fact]
    public void TitleBuilder_Build_OrientAnchorAndSubtitleProduceExpectedExtent()
    {
        // Arrange: the title width is measured with the font directly (13 pixel bold; the subtitle is 12 pixel regular)
        var titleWidth = Width("Main title", 13, bold: true);
        var subtitleWidth = Width("A longer subtitle line", 12, bold: false);
        var wide = Math.Max(titleWidth, subtitleWidth);

        // Act: the Vega-Lite 6 defaults: orient top, anchor middle, offset 4, subtitle padding 3, frame group
        var plain = Build("{\"title\":\"Main title\"}")!;
        var both = Build("{\"title\":{\"text\":\"Main title\",\"subtitle\":\"A longer subtitle line\"}}")!;
        var title = Assert.IsType<SceneText>(plain.Children[0]);

        // Assert: the title text is the group-title style: 13 pixel bold black sans-serif, centered, top baseline
        Assert.Equal(("top", "middle", "group", 4.0), (plain.Orient, plain.Anchor, plain.Frame, plain.Offset));
        Assert.Equal("title-text", title.Role);
        Assert.Equal(["Main title"], title.Lines);
        Assert.Equal((13.0, "bold", "#000", "sans-serif"), (title.FontSize, title.FontWeight, title.Fill, title.FontFamily));
        Assert.Equal(("center", "top", 0.0, 0.0), (title.Align, title.Baseline, title.Angle, title.Limit));

        // Assert: the extent is the text: as wide as the measured title, 13 high (the baseline offset 10 less 80% of 13 is 0), centered on x = 0
        Assert.Equal(titleWidth, plain.Extent.Width, 4);
        Assert.Equal(-titleWidth / 2, plain.Extent.X1, 4);
        Assert.Equal((0.0, 13.0), (plain.Extent.Y1, plain.Extent.Y2));

        // Assert: the subtitle (12 pixel, regular) sits below the title: 13 + 3 padding = 16; its box starts one pixel higher (Vega
        // rounds 0.79 * 12 to 9 and 0.8 * 12 to 10), so the extent ends at 16 - 1 + 12 = 27
        var subtitle = Assert.IsType<SceneText>(both.Children[1]);
        Assert.Equal("title-subtitle", subtitle.Role);
        Assert.Equal((0.0, 16.0), (subtitle.X, subtitle.Y));
        Assert.Equal(12.0, subtitle.FontSize);
        Assert.Null(subtitle.FontWeight);
        Assert.Equal("#000", subtitle.Fill);
        Assert.Equal(27, both.Extent.Height, 6);
        Assert.Equal(wide, both.Extent.Width, 4);

        // Assert: placed above a 300 by 200 plot: x at the center, y = 0 - 27 - 4
        var (group, bounds) = both.Place(300, 200, new SceneBounds(0, 0, 300, 200));
        Assert.Equal("title", group.Role);
        Assert.Equal((150.0, -31.0), (group.X, group.Y));
        Assert.Equal(-31, bounds.Y1, 6);
        Assert.Equal(-4, bounds.Y2, 6);
        Assert.Equal(150 - (wide / 2), bounds.X1, 4);

        // Assert: bottom puts the title 4 below the plot; start and end anchor at the left and right edge of the plot
        var bottom = Build("{\"title\":{\"text\":\"Main title\",\"orient\":\"bottom\"}}")!;
        Assert.Equal((150.0, 204.0), Position(bottom, 300, 200));
        var start = Build("{\"title\":{\"text\":\"Main title\",\"anchor\":\"start\"}}")!;
        var startText = Assert.IsType<SceneText>(start.Children[0]);
        Assert.Equal("left", startText.Align);
        Assert.Equal((0.0, -17.0), Position(start, 300, 200));
        var end = Build("{\"title\":{\"text\":\"Main title\",\"anchor\":\"end\"}}")!;
        Assert.Equal("right", Assert.IsType<SceneText>(end.Children[0]).Align);
        Assert.Equal((300.0, -17.0), Position(end, 300, 200));
    }

    /// <summary>
    ///     Validates the rotated orients: the text turns, the extent swaps width and height, and the subtitle goes beside the title.
    /// </summary>
    [Fact]
    public void TitleBuilder_Build_LeftAndRightTitlesAreRotated()
    {
        // Arrange
        var titleWidth = Width("Side", 13, bold: true);

        // Act
        var left = Build("{\"title\":{\"text\":\"Side\",\"orient\":\"left\"}}")!;
        var right = Build("{\"title\":{\"text\":\"Side\",\"orient\":\"right\"}}")!;
        var leftBoth = Build("{\"title\":{\"text\":\"Side\",\"subtitle\":\"Sub\",\"orient\":\"left\"}}")!;
        var rightBoth = Build("{\"title\":{\"text\":\"Side\",\"subtitle\":\"Sub\",\"orient\":\"right\"}}")!;

        // Assert: a left title reads upwards (-90), a right title downwards (90); the extent is 13 wide
        Assert.Equal(-90, Assert.IsType<SceneText>(left.Children[0]).Angle);
        Assert.Equal(90, Assert.IsType<SceneText>(right.Children[0]).Angle);
        Assert.Equal((0.0, 13.0), (left.Extent.X1, left.Extent.X2));
        Assert.Equal(-titleWidth / 2, left.Extent.Y1, 4);
        Assert.Equal((-13.0, 0.0), (right.Extent.X1, right.Extent.X2));

        // Assert: placed beside a 300 by 200 plot with the 4 pixel offset: x = 0 - 13 - 4, centered on y = 100 (frame group)
        Assert.Equal((-17.0, 100.0), Position(left, 300, 200));
        Assert.Equal((317.0, 100.0), Position(right, 300, 200));

        // Assert: the subtitle is beside the title: 13 + 3 to the right of a left title, 13 + 3 to the left of a right title;
        // its box is 12 wide in x (one pixel off the title's, see the subtitle above), so both extents are 27 wide
        var leftSub = Assert.IsType<SceneText>(leftBoth.Children[1]);
        var rightSub = Assert.IsType<SceneText>(rightBoth.Children[1]);
        Assert.Equal((16.0, 0.0), (leftSub.X, leftSub.Y));
        Assert.Equal((-16.0, 0.0), (rightSub.X, rightSub.Y));
        Assert.Equal(27, leftBoth.Extent.Width, 6);
        Assert.Equal(27, rightBoth.Extent.Width, 6);
        Assert.Equal((-31.0, 100.0), Position(leftBoth, 300, 200));
        Assert.Equal((331.0, 100.0), Position(rightBoth, 300, 200));

        // Assert: anchors on a left title run bottom to top: start is the bottom edge, end the top edge
        var start = Build("{\"title\":{\"text\":\"Side\",\"orient\":\"left\",\"anchor\":\"start\"}}")!;
        var end = Build("{\"title\":{\"text\":\"Side\",\"orient\":\"left\",\"anchor\":\"end\"}}")!;
        Assert.Equal(200, Position(start, 300, 200).Y);
        Assert.Equal(0, Position(end, 300, 200).Y);
    }

    /// <summary>
    ///     Validates the frame: the group frame anchors to the plot, the bounds frame to everything in the view.
    /// </summary>
    [Fact]
    public void TitleBuilder_Place_FrameBoundsCentersOverTheGuides()
    {
        // Arrange: a view whose axes reach 40 to the left, 20 above, 10 to the right and 30 below the 300 by 200 plot
        var guides = new SceneBounds(-40, -20, 310, 230);
        var group = Build("{\"title\":\"T\"}")!;
        var bounds = Build("{\"title\":{\"text\":\"T\",\"frame\":\"bounds\"}}")!;
        var startBounds = Build("{\"title\":{\"text\":\"T\",\"frame\":\"bounds\",\"anchor\":\"start\"}}")!;
        var endBounds = Build("{\"title\":{\"text\":\"T\",\"frame\":\"bounds\",\"anchor\":\"end\",\"orient\":\"bottom\"}}")!;
        var leftBounds = Build("{\"title\":{\"text\":\"T\",\"frame\":\"bounds\",\"orient\":\"left\"}}")!;

        // Act / Assert: the group frame centers on the plot (150), the bounds frame on the guides ((-40 + 310) / 2 = 135)
        Assert.Equal(150, group.Place(300, 200, guides).Group.X);
        Assert.Equal(135, bounds.Place(300, 200, guides).Group.X);
        Assert.Equal(-40, startBounds.Place(300, 200, guides).Group.X);
        Assert.Equal(310, endBounds.Place(300, 200, guides).Group.X);

        // Assert: above the guides, not the plot: y = -20 - 13 - 4 = -37; below the guides: 230 + 4
        Assert.Equal(-37, bounds.Place(300, 200, guides).Group.Y);
        Assert.Equal(234, endBounds.Place(300, 200, guides).Group.Y);

        // Assert: a left title sits left of the guides: x = -40 - 13 - 4, centered on the guides' height ((230 + -20) / 2 = 105)
        var left = leftBounds.Place(300, 200, guides).Group;
        Assert.Equal((-57.0, 105.0), (left.X, left.Y));
    }

    /// <summary>
    ///     Validates title text given as lines, properties given in the title and in the configuration, and the limit.
    /// </summary>
    [Fact]
    public void TitleBuilder_Build_PropertiesLinesConfigurationAndLimit()
    {
        // Act: an array of lines, a string with a line feed, and a title that sets its own font properties
        var lines = Build("{\"title\":[\"First\",\"Second\"]}")!;
        var splitting = Build("{\"title\":\"One\\nTwo\\nThree\"}")!;
        var styled = Build(
            """
            {"title": {"text": "Styled", "subtitle": ["s1", "s2"], "fontSize": 20, "font": "serif", "fontWeight": "normal",
             "fontStyle": "italic", "color": "red", "offset": 10, "subtitlePadding": 6, "lineHeight": 30, "subtitleFontSize": 9,
             "subtitleColor": "#123456", "subtitleFontWeight": 700, "subtitleLineHeight": 11, "dx": 2, "dy": 3, "limit": 40}}
            """)!;

        // Assert: two lines are 13 + (13 + 2) high, and a line feed splits like an array does
        Assert.Equal(["First", "Second"], Assert.IsType<SceneText>(lines.Children[0]).Lines);
        Assert.Equal(28, lines.Extent.Height, 6);
        Assert.Equal(["One", "Two", "Three"], Assert.IsType<SceneText>(splitting.Children[0]).Lines);
        Assert.Equal(13 + (2 * 15), splitting.Extent.Height, 6);

        // Assert: the title's own properties win over the style defaults
        var text = Assert.IsType<SceneText>(styled.Children[0]);
        var sub = Assert.IsType<SceneText>(styled.Children[1]);
        Assert.Equal(20.0, text.FontSize);
        Assert.Equal("serif", text.FontFamily);
        Assert.Equal("normal", text.FontWeight);
        Assert.Equal("italic", text.FontStyle);
        Assert.Equal("red", text.Fill);
        Assert.Equal(30.0, text.LineHeight);
        Assert.Equal((2.0, 3.0, 40.0), (text.Dx, text.Dy, text.Limit));
        Assert.Equal(9.0, sub.FontSize);
        Assert.Equal("700", sub.FontWeight);
        Assert.Equal("#123456", sub.Fill);
        Assert.Equal(11.0, sub.LineHeight);
        Assert.Equal(10, styled.Offset);

        // Assert: the subtitle is one title box (20 high) and the 6 padding below the anchor
        Assert.Equal(26, sub.Y, 6);

        // Assert: the limit applies to the measured text: "Styled" in 20 pixel serif is wider than 40, so the extent is narrower
        Assert.True(styled.Extent.Width < 40);
        var limited = Build("{\"title\":{\"text\":\"A rather long title for a narrow limit\",\"limit\":50}}")!;
        Assert.True(limited.Extent.Width < 50);
        Assert.Equal(["A rather long title for a narrow limit"], Assert.IsType<SceneText>(limited.Children[0]).Lines);

        // Arrange: a configuration that restyles the title and the subtitle
        const string json = """
            {"config": {"title": {"fontSize": 18, "color": "#336699", "anchor": "start", "offset": 7, "orient": "bottom",
             "subtitleFontSize": 10, "subtitleColor": "gray", "subtitlePadding": 2}},
             "title": {"text": "Configured", "subtitle": "Detail"}}
            """;
        var tree = SpecParser.Parse(json);

        // Act
        var configured = new TitleBuilder(Measurer).Build(tree.Root["title"], ConfigResolver.Resolve(tree, null))!;

        // Assert: config.title applies where the title is silent
        var configuredText = Assert.IsType<SceneText>(configured.Children[0]);
        var configuredSub = Assert.IsType<SceneText>(configured.Children[1]);
        Assert.Equal((18.0, "#336699", "left"), (configuredText.FontSize, configuredText.Fill, configuredText.Align));
        Assert.Equal(("bottom", "start", 7.0), (configured.Orient, configured.Anchor, configured.Offset));
        Assert.Equal((10.0, "gray", 20.0), (configuredSub.FontSize, configuredSub.Fill, configuredSub.Y));
    }

    /// <summary>
    ///     Validates which titles are not built and how a composition anchors its title.
    /// </summary>
    [Fact]
    public void TitleBuilder_Build_EmptyTitlesAndCompositions()
    {
        // Act / Assert: nothing to build
        Assert.Null(Build("{}"));
        Assert.Null(Build("{\"title\":null}"));
        Assert.Null(Build("{\"title\":\"\"}"));
        Assert.Null(Build("{\"title\":\"   \"}"));
        Assert.Null(Build("{\"title\":[]}"));
        Assert.Null(Build("{\"title\":{}}"));
        Assert.Null(Build("{\"title\":{\"subtitle\":\"only a subtitle\"}}"));

        // Assert: a composition anchors its title at the start and frames it by the bounds
        var composition = Build("{\"title\":\"T\"}", unitOrLayer: false)!;
        Assert.Equal(("start", "bounds"), (composition.Anchor, composition.Frame));
        Assert.Equal("left", Assert.IsType<SceneText>(composition.Children[0]).Align);

        // Assert: invalid orient, anchor and frame values (the specification rejects them, the configuration is not checked) fall back
        var odd = Build("{\"config\":{\"title\":{\"orient\":\"sideways\",\"anchor\":\"nowhere\",\"frame\":\"odd\"}},\"title\":\"T\"}")!;
        Assert.Equal(("top", "middle", "bounds"), (odd.Orient, odd.Anchor, odd.Frame));
        Assert.Throws<ArgumentNullException>(() => new TitleBuilder(null!));
        Assert.Throws<ArgumentNullException>(() => new TitleBuilder(Measurer).Build(null, null!));
    }

    /// <summary>
    ///     Places a title in a plot of the given size with no guides and returns the position of its group.
    /// </summary>
    /// <param name="block">The title block.</param>
    /// <param name="width">The plot width.</param>
    /// <param name="height">The plot height.</param>
    /// <returns>The x and y of the title group.</returns>
    private static (double X, double Y) Position(TitleBlock block, double width, double height)
    {
        var (group, _) = block.Place(width, height, new SceneBounds(0, 0, width, height));
        return (group.X, group.Y);
    }
}
