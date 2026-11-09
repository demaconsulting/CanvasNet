using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Rendering;

/// <summary>
///     Subsystem tests of the Rendering subsystem: flowcharts laid out and drawn end to end (layout data, shapes, labels,
///     the cluster layout, edges and subgraphs), checked against what the pinned Mermaid draws for the same text in
///     headless Chrome with the bundled Liberation Sans, and the pictures, the picture limit and the reports of a layout.
/// </summary>
public sealed class RenderingTests
{
    /// <summary>
    ///     Checks two subgraphs linked to each other: the node and subgraph boxes, the edge routes and the view box are
    ///     Mermaid's.
    /// </summary>
    [Fact]
    public void Rendering_Flowchart_LinkedSubgraphs_MatchMermaid()
    {
        // Act
        var scene = SceneQuery.Layout(Fixture("25-subgraph-basic"));

        // Assert
        AssertBox(new SceneRect(0, 0, 163.797, 482), scene.ViewBox);
        var nodes = SceneQuery.Nodes(scene);
        AssertBox(new SceneRect(43, 33, 77.797, 54), nodes["flowchart-a1-0"].Box);
        AssertBox(new SceneRect(43, 137, 77.797, 54), nodes["flowchart-a2-1"].Box);
        AssertBox(new SceneRect(43, 291, 77.797, 54), nodes["flowchart-b1-2"].Box);
        AssertBox(new SceneRect(43, 395, 77.797, 54), nodes["flowchart-b2-3"].Box);
        var clusters = SceneQuery.Clusters(scene);
        AssertBox(new SceneRect(8, 8, 147.797, 208), clusters["one"]);
        AssertBox(new SceneRect(8, 266, 147.797, 208), clusters["two"]);
        AssertEnds(SceneQuery.Edges(scene)["L_a2_b1_0"], [(81.898, 191), (81.898, 195.167), (81.898, 216), (81.898, 241), (81.898, 265.333), (81.898, 283.5), (81.898, 287)]);
    }

    /// <summary>
    ///     Checks a subgraph laid out on its own in its declared direction, linked by its id: positions in the scene and
    ///     the routes inside the subgraph are Mermaid's.
    /// </summary>
    [Fact]
    public void Rendering_Flowchart_SubgraphWithDirection_MatchesMermaid()
    {
        // Act
        var scene = SceneQuery.Layout(Fixture("27-subgraph-direction"));

        // Assert
        AssertBox(new SceneRect(0, 0, 463.703, 348), scene.ViewBox);
        var nodes = SceneQuery.Nodes(scene);
        AssertBox(new SceneRect(45.5, 147, 74.234, 54), nodes["flowchart-r1-0"].Box);
        AssertBox(new SceneRect(343.969, 147, 74.234, 54), nodes["flowchart-r3-2"].Box);
        AssertBox(new SceneRect(177.391, 286, 108.922, 54), nodes["flowchart-bottom-6"].Box);
        AssertBox(new SceneRect(8, 112, 447.703, 124), SceneQuery.Clusters(scene)["row"]);
        AssertEnds(SceneQuery.Edges(scene)["L_r1_r2_0"], [(119.734, 174), (125.984, 174), (156.568, 174), (185.151, 174), (190.734, 174)]);
    }

    /// <summary>
    ///     Checks a self-loop and a bent edge: the compact loop and its route are Mermaid's.
    /// </summary>
    [Fact]
    public void Rendering_Flowchart_SelfLoop_MatchesMermaid()
    {
        // Act
        var scene = SceneQuery.Layout(Fixture("23-self-loop"));

        // Assert
        AssertBox(new SceneRect(0, 0, 312.634, 147.05), scene.ViewBox);
        var edges = SceneQuery.Edges(scene);
        AssertEnds(edges["L_Retry_Retry_0"], [(106.234, 100.008), (110.284, 99.015), (126.484, 101.043), (127.132, 123.216), (113.522, 125.879), (110.119, 125.045)]);
        AssertEnds(edges["L_Retry_Done_0"], [(83.089, 85.05), (91.114, 76.708), (126.71, 43.342), (148.734, 35), (152.234, 35)]);
    }

    /// <summary>
    ///     Checks edge labels: the spacing they add between ranks and their centers are Mermaid's.
    /// </summary>
    [Fact]
    public void Rendering_Flowchart_EdgeLabels_MatchMermaid()
    {
        // Act
        var scene = SceneQuery.Layout(Fixture("20-link-labels"));

        // Assert
        AssertBox(new SceneRect(0, 0, 713.531, 70), scene.ViewBox);
        var labels = SceneQuery.EdgeLabels(scene);
        Assert.Equal(120.586, labels["L_a_b_0"].X, 3);
        Assert.Equal(277.75, labels["L_b_c_0"].X, 3);
        Assert.Equal(437.57, labels["L_c_d_0"].X, 3);
        Assert.Equal(595.172, labels["L_d_e_0"].X, 3);
        Assert.All(labels.Values, label => Assert.Equal(35, label.Y, 3));
    }

    /// <summary>
    ///     Checks a diagram title: drawn centered above the drawing, inside the view box.
    /// </summary>
    [Fact]
    public void Rendering_Flowchart_Title_IsCenteredAboveTheDrawing()
    {
        // Act
        var scene = SceneQuery.Layout("---\ntitle: My Diagram Title\n---\nflowchart LR\n  a --> b\n");

        // Assert
        var title = Assert.IsType<SceneText>(Assert.Single(SceneQuery.OfRole(scene, "title")));
        Assert.Equal(SceneTextAnchor.Middle, title.Anchor);
        Assert.Equal(-25, title.Baseline, 9);
        Assert.True(scene.ViewBox.Encloses(title.Box));
        Assert.Equal(18, title.Runs[0].Font.Size, 9);
    }

    /// <summary>
    ///     Checks an image node: its picture is asked for once, through the caller's resolver, to learn its natural size,
    ///     and the natural aspect ratio sizes the picture of a constrained node of a given height.
    /// </summary>
    [Fact]
    public void Rendering_Flowchart_ImageNode_SizedByThePictureOfTheResolver()
    {
        // Arrange: a resolver whose pictures are 40 by 20, a raster at its own size
        const string Text = "flowchart LR\n  A@{ img: \"pic.png\", label: \"Part\", h: 60, constraint: \"on\" } --> B[Next]\n";
        var resolver = new FixedSizeResolver(40, 20);

        // Act
        var scene = SceneQuery.Layout(Text, new MermaidRenderOptions { ImageResolver = resolver });

        // Assert: the picture keeps the aspect ratio 2 at the height of 60
        Assert.Equal([("pic.png", NodeImages.ProbePixels, NodeImages.ProbePixels)], resolver.Requests);
        var image = Assert.IsType<SceneImage>(Assert.Single(SceneQuery.OfRole(scene, "node-image")));
        Assert.Equal("pic.png", image.Href);
        Assert.Equal(120, image.Box.Width, 9);
        Assert.Equal(60, image.Box.Height, 9);
    }

    /// <summary>
    ///     Checks that a layout that would load more pictures than <see cref="MermaidLimits.MaxImages"/> fails at the node
    ///     whose picture passes the limit, without asking the resolver for it.
    /// </summary>
    [Fact]
    public void Rendering_Flowchart_PicturesBeyondMaxImages_FailAtTheNode()
    {
        // Arrange: three pictures against a limit of two
        const string Text = "flowchart LR\n  a@{ img: \"a.png\" }\n  b@{ img: \"b.png\" }\n  c@{ img: \"c.png\" }\n";
        var resolver = new FixedSizeResolver(10, 10);
        var options = new MermaidRenderOptions { ImageResolver = resolver, Limits = new MermaidLimits { MaxImages = 2 } };

        // Act
        var ex = Assert.Throws<MermaidRenderException>(() => SceneQuery.Layout(Text, options));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 4), (ex.Code, ex.Location.Line));
        Assert.Contains("MermaidLimits.MaxImages", ex.Message, StringComparison.Ordinal);
        Assert.Equal(["a.png", "b.png"], resolver.Requests.Select(r => r.Href));
    }

    /// <summary>
    ///     Checks that what the layout does not draw as Mermaid does is reported at the line of its node, in the order the
    ///     nodes are drawn: an HTML element that is not supported, a character that no bundled font has, and a picture
    ///     without a resolver.
    /// </summary>
    [Fact]
    public void Rendering_Flowchart_UndrawnConstructs_AreReportedAtTheirNodes()
    {
        // Arrange
        const string Text = "flowchart LR\n  a[\"<u>under</u>\"]\n  b[\"x \U000F0000\"]\n  c@{ img: \"pic.png\" }\n";
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        var scene = SceneQuery.Layout(Text, options);

        // Assert: the diagram is still laid out, and the three reports name their lines
        Assert.Equal(3, SceneQuery.Nodes(scene).Count);
        Assert.Empty(unsupported);
        Assert.Equal(
            [(MermaidDiagnosticCodes.HtmlTagIgnored, 2), (MermaidDiagnosticCodes.TextMissingGlyph, 3), (MermaidDiagnosticCodes.ImageUnresolved, 4)],
            warnings.Select(w => (w.Code, w.Location.Line)));
    }

    /// <summary>
    ///     Reads a flowchart fixture.
    /// </summary>
    /// <param name="name">The fixture name without extension.</param>
    /// <returns>The text.</returns>
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "flowchart", name + ".mmd"));

    /// <summary>
    ///     Asserts a box to the 3 decimals Mermaid's SVG reports.
    /// </summary>
    /// <param name="expected">The expected box.</param>
    /// <param name="actual">The actual box.</param>
    private static void AssertBox(SceneRect expected, SceneRect actual)
    {
        Assert.Equal(expected.X, actual.X, 0.002);
        Assert.Equal(expected.Y, actual.Y, 0.002);
        Assert.Equal(expected.Width, actual.Width, 0.002);
        Assert.Equal(expected.Height, actual.Height, 0.002);
    }

    /// <summary>
    ///     Asserts the end points of an edge's segments (Mermaid's path commands).
    /// </summary>
    /// <param name="path">The edge path.</param>
    /// <param name="expected">The expected end points.</param>
    private static void AssertEnds(ScenePath path, (double X, double Y)[] expected)
    {
        Assert.Equal(expected.Length, path.Segments.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].X, path.Segments[i].X, 0.002);
            Assert.Equal(expected[i].Y, path.Segments[i].Y, 0.002);
        }
    }

    /// <summary>
    ///     A resolver that gives pictures of one size, whatever size is asked for (a raster at its own size), and records
    ///     the requests.
    /// </summary>
    /// <param name="width">The width of the pictures.</param>
    /// <param name="height">The height of the pictures.</param>
    private sealed class FixedSizeResolver(int width, int height) : IImageResolver
    {
        /// <summary>Gets the requests made.</summary>
        public List<(string Href, int Width, int Height)> Requests { get; } = [];

        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            Requests.Add((href, pixelWidth, pixelHeight));
            var surface = new Surface(width, height);
            surface.Clear(new Rgba32(255, 0, 0, 255));
            return surface;
        }
    }
}
