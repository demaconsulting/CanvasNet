using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Tests of the header part of the title builder: the label and title blocks of a facet header (the <c>guide-label</c> and <c>guide-title</c> styles, the
///     <c>label*</c> and <c>title*</c> header properties, the alignment that an angle implies) and the header groups that hold them with the shared axes, with the
///     numbers derived by hand from Vega's title layout and the measured width of the text.
/// </summary>
public class TitleBuilderHeaderTests
{
    /// <summary>The measurer shared by the tests (bundled fonts).</summary>
    private static readonly TextMeasurer Measurer = new();

    /// <summary>A scatter specification whose x domain is 0 to 100 and whose y domain is 0 to 50.</summary>
    private const string LinearSpec = """
        { "data": { "values": [ { "v": 0, "w": 0 }, { "v": 100, "w": 50 } ] },
          "mark": "point",
          "encoding": {
            "x": { "field": "v", "type": "quantitative" },
            "y": { "field": "w", "type": "quantitative" }
          } }
        """;

    /// <summary>
    ///     Reads the header settings of a facet channel.
    /// </summary>
    /// <param name="channel">The facet channel.</param>
    /// <param name="header">The JSON text of the <c>header</c> of the field definition, or null for none.</param>
    /// <param name="callerConfig">The caller's configuration text, or null.</param>
    /// <returns>The settings.</returns>
    private static HeaderSettings Settings(string channel, string? header = null, string? callerConfig = null)
    {
        var field = header is null
            ? "{ \"field\": \"a\", \"type\": \"nominal\" }"
            : "{ \"field\": \"a\", \"type\": \"nominal\", \"header\": " + header + " }";
        var tree = SpecParser.Parse("{ \"data\": { \"values\": [ { \"a\": 1 } ] }, \"facet\": " + field + ", \"spec\": { \"mark\": \"point\" } }");
        return HeaderSettings.Read(ConfigResolver.Resolve(tree, callerConfig), tree.Root["facet"]!["header"], channel);
    }

    /// <summary>
    ///     Builds a header block.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="settings">The header settings.</param>
    /// <param name="part">Whether the text is a label or a title.</param>
    /// <returns>The block.</returns>
    private static TitleBlock Block(string text, HeaderSettings settings, HeaderPart part = HeaderPart.Label) =>
        new TitleBuilder(Measurer).BuildHeaderBlock([text], settings, part)!;

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
    ///     Validates that a label is a 10 pixel <c>guide-label</c> text, centered on top of the cell, 10 pixels from it, framed by the group.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeaderBlock_LabelUsesGuideLabelStyleSizeTen()
    {
        // Arrange
        var width = Width("Alpha", 10, bold: false);

        // Act
        var block = Block("Alpha", Settings("column"));
        var text = Assert.IsType<SceneText>(Assert.Single(block.Children));

        // Assert: the guide-label style is 10 pixel, normal weight, black, sans-serif; the block is on top, centered, framed by the group, 10 pixels away
        Assert.Equal("title-text", text.Role);
        Assert.Equal(["Alpha"], text.Lines);
        Assert.Equal((10.0, null, "#000", "sans-serif"), (text.FontSize, text.FontWeight, text.Fill, text.FontFamily));
        Assert.Equal(("center", "top", 0.0, 0.0), (text.Align, text.Baseline, text.Angle, text.Limit));
        Assert.Equal(("top", "middle", "group", 10.0), (block.Orient, block.Anchor, block.Frame, block.Offset));
        Assert.True(block.PlacesByExtent);

        // Assert: the extent is the text: as wide as measured, 10 high, centered on x = 0
        Assert.Equal(width, block.Extent.Width, 4);
        Assert.Equal(-width / 2, block.Extent.X1, 4);
        Assert.Equal((0.0, 10.0), (block.Extent.Y1, block.Extent.Y2));
    }

    /// <summary>
    ///     Validates that the title of a channel is an 11 pixel bold <c>guide-title</c> text, and that the title of a row is on the left, rotated by -90 degrees.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeaderBlock_TitleUsesGuideTitleBoldElevenAndRowIsRotated()
    {
        // Arrange
        var width = Width("Region", 11, bold: true);

        // Act
        var row = Block("Region", Settings("row"), HeaderPart.Title);
        var column = Block("Region", Settings("column"), HeaderPart.Title);
        var rowText = Assert.IsType<SceneText>(row.Children[0]);
        var columnText = Assert.IsType<SceneText>(column.Children[0]);

        // Assert: guide-title is 11 pixel bold; the title has no frame setting, so Vega's bounds frame; the offset is titlePadding 10
        Assert.Equal((11.0, "bold", "#000"), (rowText.FontSize, rowText.FontWeight, rowText.Fill));
        Assert.Equal(("left", "middle", "bounds", 10.0), (row.Orient, row.Anchor, row.Frame, row.Offset));
        Assert.Equal((-90.0, "center", "top"), (rowText.Angle, rowText.Align, rowText.Baseline));
        Assert.Equal(("top", 0.0), (column.Orient, columnText.Angle));

        // Assert: the rotated title is 11 wide and as long as the text; the column title is as wide as the text and 11 high
        Assert.Equal((0.0, 11.0), (row.Extent.X1, row.Extent.X2));
        Assert.Equal(-width / 2, row.Extent.Y1, 4);
        Assert.Equal(width, column.Extent.Width, 4);
        Assert.Equal(11, column.Extent.Height, 6);
    }

    /// <summary>
    ///     Validates that a label of a column is placed over the middle of the width of its cell, with its box 10 pixels (the label padding) above it.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeaderBlock_LabelFrameIsGroupCenteredOverCellWidth()
    {
        // Arrange: a column header over a cell 120 wide; its group has no height (Vega-Lite gives the header the width of the cell only)
        var width = Width("Alpha", 10, bold: false);
        var block = Block("Alpha", Settings("column"));

        // Act
        var (group, bounds) = block.Place(120, 0, new SceneBounds(0, 0, 120, 0));

        // Assert: the label is centered at 60; y = top 0 less the box height 10 less the offset 10 = -20 (the label of a wrapped facet in Vega-Lite's output is at y -20)
        Assert.Equal(("title", 60.0, -20.0), (group.Role, group.X, group.Y));
        Assert.Equal(60 - (width / 2), bounds.X1, 4);
        Assert.Equal(60 + (width / 2), bounds.X2, 4);
        Assert.Equal((-20.0, -10.0), (bounds.Y1, bounds.Y2));
    }

    /// <summary>
    ///     Validates the distance between the label and the cells: it is <c>labelPadding</c> (10 by default), and for a row label it is the distance from the left edge.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeaderBlock_LabelOffsetIsLabelPadding()
    {
        // Arrange / Act: a row label (rotated by -90 degrees, 10 wide) beside a cell 80 high with the default padding and with a padding of 4
        var normal = Block("Beta", Settings("row"));
        var tight = Block("Beta", Settings("row", "{ \"labelPadding\": 4 }"));
        var (group, bounds) = normal.Place(0, 80, new SceneBounds(0, 0, 0, 80));
        var (tightGroup, tightBounds) = tight.Place(0, 80, new SceneBounds(0, 0, 0, 80));

        // Assert: the label box is 10 wide and ends at -10 (-4 for the tight one), centered on the middle of the cell (40)
        Assert.Equal(10.0, normal.Offset);
        Assert.Equal(4.0, tight.Offset);
        Assert.Equal((-20.0, 40.0), (group.X, group.Y));
        Assert.Equal((-20.0, -10.0), (bounds.X1, bounds.X2));
        Assert.Equal((-14.0, 40.0), (tightGroup.X, tightGroup.Y));
        Assert.Equal((-14.0, -4.0), (tightBounds.X1, tightBounds.X2));
    }

    /// <summary>
    ///     Validates that the properties of a header change the text: the font, size, weight, style, color, limit, line height and anchor, and that an alignment or baseline
    ///     of the header wins over the one the anchor and angle imply.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeaderBlock_LabelPropertiesApply()
    {
        // Arrange / Act
        var styled = Block("Beta", Settings("column", """
            { "labelFont": "Courier", "labelFontSize": 14, "labelFontWeight": "bold", "labelFontStyle": "italic", "labelColor": "#c00",
              "labelLimit": 50, "labelLineHeight": 20, "labelAnchor": "start", "labelPadding": 3 }
            """));
        var explicitAlign = Block("Beta", Settings("column", "{ \"labelAnchor\": \"end\", \"labelAlign\": \"center\", \"labelBaseline\": \"middle\" }"));
        var end = Block("Beta", Settings("column", "{ \"labelAnchor\": \"end\" }"));
        var text = Assert.IsType<SceneText>(styled.Children[0]);

        // Assert: the properties become the text's; start anchors the text at its left
        Assert.Equal(("Courier", 14.0, "bold", "italic", "#c00"), (text.FontFamily, text.FontSize, text.FontWeight, text.FontStyle, text.Fill));
        Assert.Equal((50.0, 20.0, "left"), (text.Limit, text.LineHeight, text.Align));
        Assert.Equal(("start", 3.0), (styled.Anchor, styled.Offset));

        // Assert: an end anchor aligns right; labelAlign and labelBaseline are the header's own and win
        Assert.Equal("right", Assert.IsType<SceneText>(end.Children[0]).Align);
        var own = Assert.IsType<SceneText>(explicitAlign.Children[0]);
        Assert.Equal(("center", "middle"), (own.Align, own.Baseline));
    }

    /// <summary>
    ///     Validates Vega-Lite's alignment and baseline for a label or title with an angle (<c>defaultHeaderGuideAlign</c> and <c>defaultHeaderGuideBaseline</c>).
    /// </summary>
    /// <param name="channel">The facet channel.</param>
    /// <param name="angle">The label angle in degrees.</param>
    /// <param name="align">The expected alignment.</param>
    /// <param name="baseline">The expected baseline.</param>
    [Theory]
    [InlineData("column", 0, "center", "bottom")]
    [InlineData("column", 45, "right", "bottom")]
    [InlineData("column", 90, "right", "middle")]
    [InlineData("column", 270, "left", "middle")]
    [InlineData("column", 315, "left", "bottom")]
    [InlineData("row", 0, "right", "middle")]
    [InlineData("row", 45, "right", "middle")]
    [InlineData("row", 90, "center", "top")]
    [InlineData("row", 180, "left", "middle")]
    [InlineData("row", 270, "center", "bottom")]
    public void TitleBuilder_BuildHeaderBlock_LabelAngleChoosesAlignmentAndBaseline(string channel, double angle, string align, string baseline)
    {
        // Arrange / Act: the rules of Vega-Lite (axis/properties.ts) with the header's orient (top for a column, left for a row), by hand:
        // x (column): angle 0 is "center"/bottom, (0 < angle < 180) === false gives right, else left; the middle baseline for 45 < angle < 135 or 225 < angle < 315
        // y (row): (angle + 90) % 180 === 0 is "center"; (90 < angle < 270) === true gives left, else right; baseline: middle unless 45 < angle < 135 (top) or 225 < angle < 315 (bottom)
        var block = Block("Beta", Settings(channel, "{ \"labelAngle\": " + angle.ToString(System.Globalization.CultureInfo.InvariantCulture) + " }"));
        var text = Assert.IsType<SceneText>(block.Children[0]);

        // Assert: the angle is the text's own
        Assert.Equal(angle, text.Angle);
        Assert.Equal((align, baseline), (text.Align, text.Baseline));
    }

    /// <summary>
    ///     Validates that a row label with <c>labelAngle: 0</c> (the common way to make the labels readable) ends at the padding from the cell, centered on its middle.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeaderBlock_UprightRowLabelEndsAtTheOffset()
    {
        // Arrange: right aligned at the group origin, with a middle baseline: its box spans the text's width to the left of the origin
        var width = Width("Beta", 10, bold: false);
        var block = Block("Beta", Settings("row", "{ \"labelAngle\": 0 }"));

        // Act: beside a cell 80 high
        var (group, bounds) = block.Place(0, 80, new SceneBounds(0, 0, 0, 80));

        // Assert: the box ends at x = -10 (the padding), however wide the text is, and is centered on y = 40
        Assert.Equal(("left", -10.0, 40.0), (block.Orient, group.X, group.Y));
        Assert.Equal(-10, bounds.X2, 6);
        Assert.Equal(-10 - width, bounds.X1, 4);
        Assert.Equal(40, (bounds.Y1 + bounds.Y2) / 2, 6);
    }

    /// <summary>
    ///     Validates that Vega-Lite normalizes the angle of a title into [0, 360) to choose its alignment but gives the text the angle as written.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeaderBlock_TitleAngleIsNormalizedForTheAlignmentOnly()
    {
        // Arrange / Act: -90 is 270: x channel baseline middle (225 < 270 < 315), align left ((0 < 270 < 180) is false)
        var title = Block("Region", Settings("column", "{ \"titleAngle\": -90 }"), HeaderPart.Title);
        var text = Assert.IsType<SceneText>(title.Children[0]);

        // Assert
        Assert.Equal((-90.0, "left", "middle"), (text.Angle, text.Align, text.Baseline));
    }

    /// <summary>
    ///     Validates the orients of the text: a label oriented to the bottom or right of the cells, an anchor of the title, and an invalid orient that falls back to the default side.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeaderBlock_OrientsAndAnchorsPlaceTheBlock()
    {
        // Arrange / Act
        var bottom = Block("Beta", Settings("column", "{ \"labelOrient\": \"bottom\" }"));
        var right = Block("Beta", Settings("row", "{ \"labelOrient\": \"right\" }"));
        var title = Block("Region", Settings("column", "{ \"titleOrient\": \"bottom\", \"titleAnchor\": \"end\" }"), HeaderPart.Title);
        var facet = Block("Beta", Settings("facet"));

        // Assert: bottom is not rotated; right is rotated by 90 degrees; the end anchor aligns the text to the right; the label of a wrapped facet is on top
        Assert.Equal(("bottom", 0.0), (bottom.Orient, Assert.IsType<SceneText>(bottom.Children[0]).Angle));
        Assert.Equal(("right", 90.0), (right.Orient, Assert.IsType<SceneText>(right.Children[0]).Angle));
        Assert.Equal(("bottom", "end", "right"), (title.Orient, title.Anchor, Assert.IsType<SceneText>(title.Children[0]).Align));
        Assert.Equal("top", facet.Orient);

        // Assert: placed 10 below a cell 100 wide and 80 high: the box (11 high for the title, 10 for the label) starts at 80 + 10
        Assert.Equal(90, bottom.Place(100, 0, new SceneBounds(0, 0, 100, 80)).Group.Y, 6);
        Assert.Equal(90, title.Place(100, 0, new SceneBounds(0, 0, 100, 80)).Group.Y, 6);
    }

    /// <summary>
    ///     Validates that a text without a visible line draws nothing, for the block and for a header group without axes.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeaderBlock_LabelsFalseAndTitleNullGiveNothing()
    {
        // Arrange
        var builder = new TitleBuilder(Measurer);
        var settings = Settings("column");

        // Act / Assert: no lines, empty lines and white space
        Assert.Null(builder.BuildHeaderBlock([], settings, HeaderPart.Label));
        Assert.Null(builder.BuildHeaderBlock(["", " "], settings, HeaderPart.Title));
        Assert.Null(builder.BuildHeader("column-header", [], settings, HeaderPart.Label, 100, 0));
        Assert.Null(builder.BuildHeader("column-header", [" "], settings, HeaderPart.Label, 100, 0, []));
        Assert.Throws<ArgumentNullException>(() => builder.BuildHeaderBlock(null!, settings, HeaderPart.Label));
        Assert.Throws<ArgumentNullException>(() => builder.BuildHeaderBlock(["a"], null!, HeaderPart.Label));
        Assert.Throws<ArgumentNullException>(() => builder.BuildHeader(null!, ["a"], settings, HeaderPart.Label, 1, 1));
    }

    /// <summary>
    ///     Validates the order of lookup of a text property: the header, then <c>config.title</c> (which Vega applies to these titles too), then the style.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeaderBlock_HeaderOverTitleConfigOverStyle()
    {
        // Arrange: config.title gives size 20 and a start anchor, the header only gives one of two label sizes
        const string config = "{ \"title\": { \"fontSize\": 20, \"anchor\": \"start\" } }";

        // Act
        var fromTitleConfig = Block("Beta", Settings("column", null, config));
        var fromHeader = Block("Beta", Settings("column", "{ \"labelFontSize\": 12 }", config));
        var fromStyle = Block("Beta", Settings("column", null, "{ \"style\": { \"guide-label\": { \"fontSize\": 8 } } }"));

        // Assert: Vega-Lite hands the properties to a Vega title, and a Vega title takes config.title before its style
        Assert.Equal(20.0, Assert.IsType<SceneText>(fromTitleConfig.Children[0]).FontSize);
        Assert.Equal("start", fromTitleConfig.Anchor);
        Assert.Equal(12.0, Assert.IsType<SceneText>(fromHeader.Children[0]).FontSize);
        Assert.Equal(8.0, Assert.IsType<SceneText>(fromStyle.Children[0]).FontSize);
    }

    /// <summary>
    ///     Validates that the scene budget is charged before the title group and its text are created.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeader_ChargesTheBudgetBeforeCreatingItems()
    {
        // Arrange
        var builder = new TitleBuilder(Measurer);
        var settings = Settings("column");

        // Act / Assert: the block needs the title group and the text (2), a header group one more (3)
        Assert.Throws<VegaLiteRenderException>(() => builder.BuildHeaderBlock(["a"], settings, HeaderPart.Label, new SceneBudget(1)));
        Assert.NotNull(builder.BuildHeaderBlock(["a"], settings, HeaderPart.Label, new SceneBudget(2)));
        Assert.Throws<VegaLiteRenderException>(() => builder.BuildHeader("column-header", ["a"], settings, HeaderPart.Label, 50, 0, null, new SceneBudget(2)));
        var budget = new SceneBudget(3);
        Assert.NotNull(builder.BuildHeader("column-header", ["a"], settings, HeaderPart.Label, 50, 0, null, budget));
        Assert.Equal(3, budget.Count);
    }

    /// <summary>
    ///     Validates the header group of a column: the size of a cell along the header and none across, the label over the middle of the cell, and bounds that are those of the text.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeader_ColumnLabelGroupHasCellWidthAndTextBounds()
    {
        // Arrange
        var width = Width("Alpha", 10, bold: false);

        // Act
        var header = new TitleBuilder(Measurer).BuildHeader("column-header", ["Alpha"], Settings("column"), HeaderPart.Label, 120, 0)!;

        // Assert: the group is at the origin and as wide as the cell; its only child is the label group at (60, -20)
        Assert.Equal(("column-header", 0.0, 0.0, 120.0, 0.0), (header.Group.Role, header.Group.X, header.Group.Y, header.Group.Width, header.Group.Height));
        var label = Assert.IsType<SceneGroup>(Assert.Single(header.Group.Children));
        Assert.Equal(("title", 60.0, -20.0), (label.Role, label.X, label.Y));
        Assert.Equal(60 - (width / 2), header.Bounds.X1, 4);
        Assert.Equal(60 + (width / 2), header.Bounds.X2, 4);
        Assert.Equal((-20.0, -10.0), (header.Bounds.Y1, header.Bounds.Y2));
    }

    /// <summary>
    ///     Validates the title of a channel as a header group of its own: the row title ends 10 pixels left of the origin, the column title 10 above it, centered on the origin.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeader_ChannelTitlesSitTenPixelsFromTheOrigin()
    {
        // Arrange
        var builder = new TitleBuilder(Measurer);
        var width = Width("Region", 11, bold: true);

        // Act: the group of a title has no size; the layout puts it at the middle of the cells
        var row = builder.BuildHeader("row-title", ["Region"], Settings("row"), HeaderPart.Title, 0, 0)!;
        var column = builder.BuildHeader("column-title", ["Region"], Settings("column"), HeaderPart.Title, 0, 0)!;

        // Assert: the 11 pixel text and the offset 10 give x = -21 for the rotated row title (box -21 to -10) and y = -21 for the column title (S2 of the Vega-Lite output: -21)
        var rowTitle = Assert.IsType<SceneGroup>(Assert.Single(row.Group.Children));
        var columnTitle = Assert.IsType<SceneGroup>(Assert.Single(column.Group.Children));
        Assert.Equal((-21.0, 0.0), (rowTitle.X, rowTitle.Y));
        Assert.Equal((-21.0, -10.0), (row.Bounds.X1, row.Bounds.X2));
        Assert.Equal(-width / 2, row.Bounds.Y1, 4);
        Assert.Equal((0.0, -21.0), (columnTitle.X, columnTitle.Y));
        Assert.Equal((-21.0, -10.0), (column.Bounds.Y1, column.Bounds.Y2));
        Assert.Equal(-width / 2, column.Bounds.X1, 4);
    }

    /// <summary>
    ///     Validates a footer that holds a shared x axis and, below it, the label: the label sits the padding below the axis, and the bounds cover both.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeader_FooterHoldsAxisAndLabelBelowIt()
    {
        // Arrange: the x axis of 300 pixels in a footer (at the origin), and a label oriented to the bottom
        var axis = ChartTestHelpers.Axis(LinearSpec, "x", 300, 200, inHeader: true)!;
        var settings = Settings("column", "{ \"labelOrient\": \"bottom\" }");

        // Act
        var footer = new TitleBuilder(Measurer).BuildHeader("column-footer", ["Alpha"], settings, HeaderPart.Label, 300, 0, [axis])!;

        // Assert: the axis group first, then the label group, centered at 150, 10 below the axis's bounds, which end where its labels end
        Assert.Equal(["axis", "title"], footer.Group.Children.Select(c => c.Role));
        Assert.Same(axis.Group, footer.Group.Children[0]);
        var label = Assert.IsType<SceneGroup>(footer.Group.Children[1]);
        Assert.Equal(150.0, label.X, 6);
        Assert.Equal(axis.Bounds.Y2 + 10, label.Y, 6);

        // Assert: the bounds are the axis's and the label's (the label is 10 high, below the axis)
        Assert.Equal(axis.Bounds.Y1, footer.Bounds.Y1, 6);
        Assert.Equal(axis.Bounds.Y2 + 20, footer.Bounds.Y2, 6);
        Assert.Equal(axis.Bounds.X1, footer.Bounds.X1, 6);
    }

    /// <summary>
    ///     Validates a row header that holds a shared y axis and the label left of it: the label is the padding to the left of the axis's bounds, centered on the cell's height.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeader_RowHeaderHoldsAxisAndLabelLeftOfIt()
    {
        // Arrange: the y axis of 200 pixels in a row header
        var axis = ChartTestHelpers.Axis(LinearSpec, "y", 300, 200, inHeader: true)!;

        // Act
        var header = new TitleBuilder(Measurer).BuildHeader("row-header", ["Beta"], Settings("row"), HeaderPart.Label, 0, 200, [axis])!;

        // Assert: the label (rotated, 10 wide) is 10 left of the axis's bounds and centered at y = 100
        var label = Assert.IsType<SceneGroup>(header.Group.Children[1]);
        Assert.Equal(axis.Bounds.X1 - 20, label.X, 6);
        Assert.Equal(100.0, label.Y, 6);
        Assert.Equal(axis.Bounds.X1 - 20, header.Bounds.X1, 6);
        Assert.Equal(axis.Bounds.X2, header.Bounds.X2, 6);
        Assert.Equal((0.0, 0.0, 0.0, 200.0), (header.Group.X, header.Group.Y, header.Group.Width, header.Group.Height));
    }

    /// <summary>
    ///     Validates a header that holds only an axis: its bounds are the axis's, and there is no label.
    /// </summary>
    [Fact]
    public void TitleBuilder_BuildHeader_AxisOnlyHeaderHasTheAxisBounds()
    {
        // Arrange
        var axis = ChartTestHelpers.Axis(LinearSpec, "x", 300, 200, inHeader: true)!;

        // Act
        var footer = new TitleBuilder(Measurer).BuildHeader("column-footer", [], Settings("column"), HeaderPart.Label, 300, 0, [axis])!;

        // Assert
        Assert.Same(axis.Group, Assert.Single(footer.Group.Children));
        Assert.Equal(axis.Bounds, footer.Bounds);
    }
}
