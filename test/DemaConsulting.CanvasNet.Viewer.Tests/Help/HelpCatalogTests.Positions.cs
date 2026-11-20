using System.Globalization;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Viewer.Help;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Help;

/// <summary>
///     Tests that the claims of the <c>positions</c> library cards and of the framed-figure recipe hold when the library
///     runs them: the selector examples, the kinds of each diagram type, the view paths, the limits, the links, and the
///     callouts of the framed flowchart example.
/// </summary>
public partial class HelpCatalogTests
{
    /// <summary>
    ///     Renders a file with selectors and returns the picture with its positions.
    /// </summary>
    /// <param name="input">The file.</param>
    /// <param name="selectors">The selectors.</param>
    /// <returns>The picture; the caller disposes it.</returns>
    private static ViewerImage RenderPositions(string input, params string[] selectors)
    {
        return CanvasNetViewer.Render(input, new ViewerRenderOptions { Positions = PositionSelection.Parse(selectors) });
    }

    /// <summary>
    ///     Gets the ids of the positions of a picture.
    /// </summary>
    /// <param name="image">The picture.</param>
    /// <returns>The ids, in order.</returns>
    private static List<string> IdsOf(ViewerImage image) => [.. image.Positions.Select(p => p.Id)];

    /// <summary>
    ///     Test that <c>positions</c> is a root after <c>composition</c> with its four cards, and that it is linked both
    ///     ways with the framed-figure, anchors, and composition cards.
    /// </summary>
    [Fact]
    public void HelpCatalog_PositionsCard_IsInTheTreeAndLinked()
    {
        // Arrange
        var root = HelpCardText.Card("positions");
        var roots = HelpCatalog.LibraryRootOrder.ToList();

        // Assert: the root, its cards, and the links
        Assert.Equal(roots.IndexOf("composition") + 1, roots.IndexOf("positions"));
        Assert.Equal(["selectors", "mermaid", "vega-lite", "geometry"], root.ChildIds.Select(id => id[(id.IndexOf('/') + 1)..]));
        Assert.Contains("svg/framed-figure", root.SeeAlsoIds);
        Assert.Contains("scad/anchors", root.SeeAlsoIds);
        Assert.Contains("positions", HelpCardText.Card("svg/framed-figure").SeeAlsoIds);
        Assert.Contains("positions", HelpCardText.Card("scad/anchors").SeeAlsoIds);
        Assert.Contains("positions", HelpCardText.Card("composition").SeeAlsoIds);
        Assert.Contains("positions/mermaid", HelpCardText.Card("mermaid/composition").SeeAlsoIds);
        Assert.Contains("positions/vega-lite", HelpCardText.Card("vega-lite/composition").SeeAlsoIds);
    }

    /// <summary>
    ///     Test that the limits the cards state are the limits of the code.
    /// </summary>
    [Fact]
    public void HelpCatalog_PositionsLimits_AreTheLimitsOfTheCode()
    {
        // Arrange
        var limits = HelpCardText.TextOf("limits");
        var selectors = HelpCardText.TextOf("positions/selectors");
        static string Of(int value) => value.ToString(CultureInfo.InvariantCulture);

        // Assert
        Assert.Contains($"at most {Of(PositionCollector.MaxPositions)} items", limits, StringComparison.Ordinal);
        Assert.Contains($"At most {Of(PositionCollector.MaxPositions)} items", HelpCardText.TextOf("positions"), StringComparison.Ordinal);
        Assert.Contains($"At most {Of(PositionSelection.MaxSelectors)} selectors", selectors, StringComparison.Ordinal);
        Assert.Contains($"{Of(PositionSelector.MaxSelectorLength)} characters", selectors, StringComparison.Ordinal);
        Assert.Contains($"{Of(PositionSelector.MaxPredicates)} predicates", selectors, StringComparison.Ordinal);
        Assert.Contains($"at most {Of(ImagePosition.MaxDataFields)} fields", HelpCardText.TextOf("positions/vega-lite"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test the data-field example of the cards: both cite the selector and the 2012 base year of a month time unit,
    ///     and on the chart the <c>positions/vega-lite</c> card describes the selector picks the point while the channel
    ///     selector with the source date matches nothing.
    /// </summary>
    [Fact]
    public void HelpCatalog_PositionsVegaLite_DataFieldExample_SelectsThePoint()
    {
        // Arrange
        using var directory = new TempDirectory();
        var chart = directory.Combine("months.vl.json");
        File.WriteAllText(
            chart,
            "{\"data\":{\"values\":[{\"month\":\"2025-10-01\",\"region\":\"Americas\",\"revenue\":6.1},{\"month\":\"2025-11-01\",\"region\":\"Americas\",\"revenue\":6.8}]}," +
            "\"mark\":\"line\",\"encoding\":{\"x\":{\"field\":\"month\",\"timeUnit\":\"month\",\"type\":\"temporal\"}," +
            "\"y\":{\"field\":\"revenue\",\"type\":\"quantitative\"},\"color\":{\"field\":\"region\"}}}");
        var vegaLite = HelpCardText.TextOf("positions/vega-lite");
        var selectors = HelpCardText.TextOf("positions/selectors");
        Assert.All([vegaLite, selectors], text => Assert.Contains("mark:month=2025-11-01+region=Americas", text, StringComparison.Ordinal));
        Assert.All([vegaLite, selectors], text => Assert.Contains("2012", text, StringComparison.Ordinal));
        Assert.Contains("\"2012-11-01T00:00:00.000Z\"", vegaLite, StringComparison.Ordinal);
        Assert.Contains("mark:x=2025-11-01", vegaLite, StringComparison.Ordinal);

        // Act
        using var byData = RenderPositions(chart, "mark:month=2025-11-01+region=Americas");
        using var byChannel = RenderPositions(chart, "mark:x=2025-11-01");

        // Assert
        var item = Assert.Single(byData.Positions);
        Assert.True(item.TryGetValue("x", out var x));
        Assert.Equal("2012-11-01T00:00:00.000Z", x.StringValue);
        Assert.True(item.TryGetData("month", out var month));
        Assert.Equal("2025-11-01", month.StringValue);
        Assert.Empty(IdsOf(byChannel));
    }

    /// <summary>
    ///     Test the selector examples of the cards by running them on the help examples: what each matches.
    /// </summary>
    [Fact]
    public void HelpCatalog_PositionsSelectors_Examples_MatchAsTheCardsSay()
    {
        // Arrange: the flowchart, the git graph, and the bar chart examples
        using var directory = new TempDirectory();
        var flow = HelpCardText.SaveExample(directory, "mermaid", "flowchart", "flowchart.mmd");
        var git = HelpCardText.SaveExample(directory, "mermaid", "gitgraph", "gitgraph.mmd");
        var bar = HelpCardText.SaveExample(directory, "vega-lite", "bar", "bar.vl.json");
        var text = HelpCardText.TextOf("positions/selectors") + HelpCardText.TextOf("positions");
        string[] cited = ["node:send", "edge:@from=pack", "mark:x=B", "'commit:*fix*'", "node:se?d", "node:s*", "mark:x=3", "mark:x=\"3\""];
        Assert.All(cited, selector => Assert.Contains(selector, text, StringComparison.Ordinal));

        // Act
        using var send = RenderPositions(flow, "node:send");
        using var glob = RenderPositions(flow, "node:se?d", "node:s*");
        using var edges = RenderPositions(flow, "edge:@from=pack");
        using var fix = RenderPositions(git, "commit:*fix*");
        using var number = RenderPositions(bar, "mark:y=55");
        using var textValue = RenderPositions(bar, "mark:y=\"55\"");
        using var bars = RenderPositions(bar, "mark:x=B", "axis");

        // Assert
        Assert.Equal(["send"], IdsOf(send));
        Assert.Equal(["start", "stock", "send"], IdsOf(glob));
        Assert.Equal(["L_pack_send_0"], IdsOf(edges));
        Assert.Equal(["hotfix"], IdsOf(fix));
        Assert.Equal(["bar-1"], IdsOf(number));
        Assert.Empty(IdsOf(textValue));
        Assert.Equal(["x", "y", "bar-1"], IdsOf(bars));
    }

    /// <summary>
    ///     Test the metadata examples of the selectors card: the parallel edge index and the axis of one view of a
    ///     concatenation.
    /// </summary>
    [Fact]
    public void HelpCatalog_PositionsSelectors_MetadataExamples_SelectOneItem()
    {
        // Arrange: two parallel edges, and two charts side by side
        using var directory = new TempDirectory();
        var flow = directory.Combine("pair.mmd");
        File.WriteAllText(flow, "flowchart LR\n    A --> B\n    A --> B\n");
        var chart = directory.Combine("pair.vl.json");
        const string Unit = "{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}";
        File.WriteAllText(chart, "{\"data\":{\"values\":[{\"a\":\"A\",\"b\":3}]},\"hconcat\":[" + Unit + "," + Unit + "]}");
        var text = HelpCardText.TextOf("positions/selectors");
        Assert.Contains("edge:@from=A+@to=B+@index=0", text, StringComparison.Ordinal);
        Assert.Contains("axis:@id=x+@view=\"hconcat[1]\"", text, StringComparison.Ordinal);

        // Act
        using var edge = RenderPositions(flow, "edge:@from=A+@to=B+@index=0");
        using var axis = RenderPositions(chart, "axis:@id=x+@view=\"hconcat[1]\"");

        // Assert
        Assert.Equal(0, Assert.Single(edge.Positions).Index);
        Assert.Equal("hconcat[1]", Assert.Single(axis.Positions).View);
    }

    /// <summary>
    ///     Test the kinds the <c>positions/mermaid</c> card lists for each diagram type by rendering the help example of
    ///     the type.
    /// </summary>
    /// <param name="example">The example of the <c>mermaid</c> tree.</param>
    /// <param name="kinds">The kinds the example reports, comma separated.</param>
    [Theory]
    [InlineData("flowchart", "title,group,node,edge")]
    [InlineData("class", "title,node,edge,note")]
    [InlineData("state", "title,group,node,edge,note")]
    [InlineData("er", "title,node,edge")]
    [InlineData("sequence", "title,participant,message,note")]
    [InlineData("gantt", "title,task,milestone,section")]
    [InlineData("pie", "title,slice")]
    [InlineData("mindmap", "node")]
    [InlineData("packet", "title,block")]
    [InlineData("gitgraph", "title,commit,branch,tag")]
    public void HelpCatalog_PositionsMermaid_KindsPerType_AreTheKindsReported(string example, string kinds)
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = HelpCardText.SaveExample(directory, "mermaid", example, example + ".mmd");
        var card = HelpCardText.TextOf("positions/mermaid");

        // Act
        using var image = RenderPositions(input, "all");

        // Assert: the reported kinds, each named by the card
        var reported = image.Positions.Select(p => p.Kind).Distinct().ToList();
        Assert.Equal(kinds.Split(',').Order(StringComparer.Ordinal), reported.Order(StringComparer.Ordinal));
        Assert.All(reported, kind => Assert.Contains(kind, card, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that every callout arrow of the <c>framed-flow</c> example of the <c>svg/framed-figure</c> card is bound
    ///     with <c>cn:x2y2</c> to a selector of the diagram it embeds; that its fallback numbers are within a quarter unit,
    ///     and the numbers <see cref="SvgFlattener"/> bakes within 1e-3, of the position computed independently (the
    ///     pixel of the selector when the diagram is rendered alone at the size of its image, plus the image x and y);
    ///     and that at least two different positions are hit.
    /// </summary>
    [Fact]
    public void HelpCatalog_FramedFlowExample_CalloutsEndOnPositions()
    {
        // Arrange: the figure, the diagram image, and the bound callout lines
        XNamespace cn = "https://demaconsulting.com/canvasnet";
        var source = HelpCardText.Example("svg", "framed-flow");
        var figure = XDocument.Parse(source);
        var elements = figure.Descendants().ToList();
        var image = Assert.Single(elements, e => e.Name.LocalName == "image");
        Assert.Equal("flowchart.mmd", (string?)image.Attribute("href"));
        double Number(XElement element, string name) => double.Parse((string)element.Attribute(name)!, CultureInfo.InvariantCulture);
        var (x, y, width, height) = (Number(image, "x"), Number(image, "y"), Number(image, "width"), Number(image, "height"));
        var callouts = elements.Where(e => e.Name.LocalName == "line" && (string?)e.Attribute("class") == "callout").ToList();
        Assert.NotEmpty(callouts);
        Assert.All(callouts, line => Assert.StartsWith($"#{(string?)image.Attribute("id")} ", (string?)line.Attribute(cn + "x2y2")));
        var selectors = callouts.Select(line => ((string)line.Attribute(cn + "x2y2")!).Split(' ', 2)[1]).ToList();

        // Act: render the diagram alone at the size of its image once per selector, and flatten the figure
        using var directory = new TempDirectory();
        var input = HelpCardText.SaveExample(directory, "mermaid", "flowchart", "flowchart.mmd");
        var expected = selectors.Select(selector =>
        {
            using var slot = CanvasNetViewer.Render(input, new ViewerRenderOptions { Width = (int)width, Height = (int)height, Positions = PositionSelection.Parse([selector]) });
            var pixel = Assert.Single(slot.Positions).Pixel!.Value;
            return (X: pixel.X + x, Y: pixel.Y + y);
        }).ToList();
        File.WriteAllText(directory.Combine("framed-flow.svg"), source);
        var result = SvgFlattener.Execute(SourceLoader.Probe(directory.Combine("framed-flow.svg")), new FlattenRequest(directory.Combine("flat.svg")) { Strict = true });
        var baked = XDocument.Load(directory.Combine("flat.svg")).Descendants()
            .Where(e => e.Name.LocalName == "line" && (string?)e.Attribute("class") == "callout")
            .ToList();

        // Assert: the fallbacks are close, the baked tips are exact, the copy has no binding left, and two positions are hit
        Assert.Equal(callouts.Count, result.ResolvedBindings);
        Assert.Equal(callouts.Count, baked.Count);
        for (var index = 0; index < callouts.Count; index++)
        {
            Assert.InRange(Number(callouts[index], "x2") - expected[index].X, -0.25, 0.25);
            Assert.InRange(Number(callouts[index], "y2") - expected[index].Y, -0.25, 0.25);
            Assert.Equal(expected[index].X, Number(baked[index], "x2"), 3);
            Assert.Equal(expected[index].Y, Number(baked[index], "y2"), 3);
            Assert.Null(baked[index].Attribute(cn + "x2y2"));
        }

        Assert.True(expected.Distinct().Count() >= 2, "The callouts hit fewer than two positions.");
    }
}
