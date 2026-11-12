using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Rendering;

/// <summary>
///     Tests of <see cref="ShapeLibrary"/>: the intersect functions, the exact box of path geometry, the style split, the
///     paint cascade and the shapes whose behavior the upstream vectors do not show.
/// </summary>
public sealed class ShapeLibraryTests
{
    /// <summary>The node box the intersect tests use: 100 by 50 around the origin.</summary>
    private static readonly NodeBox Box = new(0, 0, 100, 50);

    /// <summary>
    ///     Checks <c>intersect-rect.js</c>: the side the line leaves through decides the point.
    /// </summary>
    /// <param name="px">The point's x.</param>
    /// <param name="py">The point's y.</param>
    /// <param name="x">The expected x.</param>
    /// <param name="y">The expected y.</param>
    [Theory]
    [InlineData(200, 0, 50, 0)]
    [InlineData(0, -200, 0, -25)]
    [InlineData(100, 100, 25, 25)]
    [InlineData(-200, 50, -50, 12.5)]
    public void ShapeLibrary_IntersectRect_Point_LeavesThroughTheRightSide(double px, double py, double x, double y)
    {
        // Act
        var result = ShapeLibrary.IntersectRect(Box, new ShapePoint(px, py));

        // Assert
        Assert.Equal(x, result.X, 9);
        Assert.Equal(y, result.Y, 9);
    }

    /// <summary>
    ///     Checks <c>intersect-ellipse.js</c> on the axes and a diagonal.
    /// </summary>
    [Fact]
    public void ShapeLibrary_IntersectEllipse_Points_LieOnTheEllipse()
    {
        // Act
        var right = ShapeLibrary.IntersectEllipse(Box, 50, 25, new ShapePoint(300, 0));
        var up = ShapeLibrary.IntersectEllipse(Box, 50, 25, new ShapePoint(0, -300));
        var diagonal = ShapeLibrary.IntersectEllipse(Box, 50, 25, new ShapePoint(-300, 300));

        // Assert
        Assert.Equal(new ShapePoint(50, 0), right);
        Assert.Equal(new ShapePoint(0, -25), up);
        Assert.Equal(1, ((diagonal.X * diagonal.X) / 2500) + ((diagonal.Y * diagonal.Y) / 625), 9);
        Assert.True(diagonal.X < 0 && diagonal.Y > 0);
    }

    /// <summary>
    ///     Checks that <c>intersect-line.js</c> keeps the half-denominator rounding of its integer original: the crossing
    ///     of two lines at the origin comes out half a unit off.
    /// </summary>
    [Fact]
    public void ShapeLibrary_IntersectLine_Crossing_KeepsTheRoundingOffset()
    {
        // Act
        var result = ShapeLibrary.IntersectLine(new ShapePoint(-10, 0), new ShapePoint(10, 0), new ShapePoint(0, -10), new ShapePoint(0, 10));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0.5, Math.Abs(result.Value.X), 9);
        Assert.Equal(0.5, Math.Abs(result.Value.Y), 9);
    }

    /// <summary>
    ///     Checks that segments on one side of each other, and parallel segments, do not intersect.
    /// </summary>
    [Fact]
    public void ShapeLibrary_IntersectLine_NoCrossing_GivesNull()
    {
        // Act
        var apart = ShapeLibrary.IntersectLine(new ShapePoint(0, 0), new ShapePoint(10, 0), new ShapePoint(0, 5), new ShapePoint(10, 6));
        var parallel = ShapeLibrary.IntersectLine(new ShapePoint(0, 0), new ShapePoint(10, 0), new ShapePoint(0, 0), new ShapePoint(5, 0));

        // Assert
        Assert.Null(apart);
        Assert.Null(parallel);
    }

    /// <summary>
    ///     Checks that the polygon intersection places the polygon in the node's box and returns the crossing nearest
    ///     the point; Mermaid's line test never rejects a crossing beyond the point, so a point inside still gives the
    ///     crossing in its direction.
    /// </summary>
    [Fact]
    public void ShapeLibrary_IntersectPolygon_Square_GivesNearestCrossing()
    {
        // Arrange: a square of 100 by 50 written from 0, placed by its smallest x and y
        List<ShapePoint> square = [new(0, 0), new(100, 0), new(100, 50), new(0, 50)];

        // Act
        var right = ShapeLibrary.IntersectPolygon(Box, square, new ShapePoint(200, 0));
        var inside = ShapeLibrary.IntersectPolygon(Box, square, new ShapePoint(-10, 0));

        // Assert
        Assert.Equal(50.5, right.X, 9);
        Assert.Equal(-50.5, inside.X, 9);
        Assert.Equal(0.5, inside.Y, 9);
    }

    /// <summary>
    ///     Checks that the box of an arc includes its extremes, as <c>getBBox</c> does, and a radius too small for the
    ///     arc is scaled up as SVG requires.
    /// </summary>
    [Fact]
    public void ShapeLibrary_SegmentBounds_Arcs_IncludeTheirExtremes()
    {
        // Arrange: a half circle from (-10, 0) to (10, 0) over the top, and an arc whose radius is too small
        IReadOnlyList<SceneSegment> half = [SceneSegment.Move(-10, 0), SceneSegment.Arc(10, 10, 0, false, true, 10, 0)];
        IReadOnlyList<SceneSegment> small = [SceneSegment.Move(0, 0), SceneSegment.Arc(1, 1, 0, false, false, 20, 0)];

        // Act
        var top = ShapeLibrary.SegmentBounds(half);
        var scaled = ShapeLibrary.SegmentBounds(small);

        // Assert
        Assert.NotNull(top);
        Assert.Equal(-10, top.Value.Y, 9);
        Assert.Equal(10, top.Value.Height, 9);
        Assert.NotNull(scaled);
        Assert.Equal(10, scaled.Value.Height, 9);
    }

    /// <summary>
    ///     Checks <c>styles2String</c>'s split: class styles, then inline styles, a later key wins in its first place, and
    ///     text properties go to the label.
    /// </summary>
    [Fact]
    public void ShapeLibrary_SplitStyles_Styles_SplitsLabelFromNode()
    {
        // Arrange
        var node = new LayoutNode
        {
            Id = "A",
            Shape = "rect",
            CssCompiledStyles = ["fill:#f00", "color:blue"],
            CssStyles = [" stroke: #333", "fill:#0f0"],
        };

        // Act
        var (label, nodeStyles) = ShapeLibrary.SplitStyles(node);

        // Assert
        Assert.Equal(["color:blue"], label);
        Assert.Equal(["fill:#0f0", "stroke:#333"], nodeStyles);
    }

    /// <summary>
    ///     Checks that the node's own styles win over the style sheet on the shape.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_UserStyles_PaintTheShape()
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = "rect", Label = "A", Padding = 15, CssStyles = ["fill:#ff0000", "stroke-width:4px", "stroke-dasharray:5 5"] };

        // Act
        var result = ShapeLibrary.Draw(node, Context());

        // Assert
        var path = Assert.IsType<ScenePath>(result.Items[0]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), path.Fill);
        Assert.Equal(4, path.Stroke!.Width);
        Assert.Equal([5.0, 5.0], path.Stroke.Dash);
        Assert.Equal(new Rgba32(147, 112, 219, 255), path.Stroke.Color);
    }

    /// <summary>
    ///     Checks the documented choice for the legacy <c>ellipse</c> (<c>A(-text-)</c>): an ellipse around the padded
    ///     label with an elliptical intersect function.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_LegacyEllipse_IsAnEllipse()
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = "ellipse", Padding = 15 };

        // Act
        var result = ShapeLibrary.Draw(node, Context(fixedLabel: new SceneRect(0, 0, 40, 24)));

        // Assert
        Assert.Equal("ellipse", result.Handler);
        Assert.Equal(70, result.Width, 9);
        Assert.Equal(39, result.Height, 9);
        Assert.Equal(new ShapePoint(35, 0), result.Intersect(new NodeBox(0, 0, 70, 39), new ShapePoint(300, 0)));
    }

    /// <summary>
    ///     Checks that the <c>state</c> shape, whose rectangle Mermaid sizes as not a number, draws and measures only its
    ///     label.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_State_SizesToTheLabel()
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = "state", Padding = 15 };

        // Act
        var result = ShapeLibrary.Draw(node, Context(fixedLabel: new SceneRect(0, 0, 40, 24)));

        // Assert
        Assert.Equal(new SceneRect(-20, -12, 40, 24), result.Bounds);
        Assert.DoesNotContain(result.Items, item => item is ScenePath);
    }

    /// <summary>
    ///     Checks that a fork turns upright in a subgraph of direction <c>LR</c>.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_ForkInLeftToRight_IsUpright()
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = "fork", Dir = "LR" };

        // Act
        var result = ShapeLibrary.Draw(node, Context());

        // Assert
        Assert.Equal(10, result.Width, 9);
        Assert.Equal(70, result.Height, 9);
    }

    /// <summary>
    ///     Checks that an image node without an image resolver is reported, keeps its frame and draws no picture.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_ImageWithoutResolver_ReportsAndDrawsNoPicture()
    {
        // Arrange
        var sink = new MermaidReportSink(100, false);
        var node = new LayoutNode { Id = "A", Shape = "imageSquare", Img = "pic.png", Label = string.Empty, AssetWidth = 60, AssetHeight = 40, Location = new MermaidLocation(null, 2, 5) };
        var context = Context(sink: sink, images: new NodeImages(null, 10, sink));
        var warnings = new List<MermaidDiagnostic>();

        // Act
        var result = ShapeLibrary.Draw(node, context);
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.DoesNotContain(result.Items, item => item is SceneImage);
        Assert.Equal(60, result.Width, 9);
        Assert.Equal(40, result.Height, 9);
        var warning = Assert.Single(warnings);
        Assert.Equal(MermaidDiagnosticCodes.ImageUnresolved, warning.Code);
        Assert.Equal(2, warning.Location.Line);
    }

    /// <summary>
    ///     Checks that a shape this version does not draw on a node - a name that is no shape, or the entity box on a
    ///     node without the data of an entity - fails, located, with a hint that suggests a documented shape.
    /// </summary>
    /// <param name="shape">The shape name.</param>
    [Theory]
    [InlineData("notAShape")]
    [InlineData("erBox")]
    public void ShapeLibrary_Draw_UndrawnShape_ThrowsRenderFailed(string shape)
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = shape, Location = new MermaidLocation(null, 3, 1) };

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => ShapeLibrary.Draw(node, Context()));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, exception.Code);
        Assert.Equal(3, exception.Location.Line);
        Assert.Contains($"'{shape}'", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.Hint);
        var suggestion = exception.Hint.Split('\'')[1];
        Assert.True(ShapeNames.IsKnown(suggestion), $"the hint suggests '{suggestion}', which is not a shape");
    }

    /// <summary>
    ///     Checks that every shape name a flowchart accepts is drawn.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_EveryDocumentedName_IsDrawn()
    {
        // Arrange
        var context = Context(fixedLabel: new SceneRect(0, 0, 40, 24));
        var names = ShapeNames.ShortNames.Concat(["state", "choice", "note", "composite", "anchor", "icon", "imageSquare", "iconSquare", "iconCircle", "iconRounded", "collapsedGroup", "ellipse"]);

        // Act
        var results = names.Select(name => ShapeLibrary.Draw(new LayoutNode { Id = "A", Shape = name, Label = "x", Padding = 15 }, context)).ToList();

        // Assert
        Assert.All(results, result => Assert.True(result.Width > 0 && result.Height > 0, result.Handler));
    }

    /// <summary>
    ///     Checks the class box of a class with attributes and methods: the groups stacked under the name, the rectangle
    ///     around them with the padding, and the two dividers under the name and under the attributes.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_ClassBoxWithMembers_StacksGroupsAndDividers()
    {
        // Arrange
        var member = new LayoutClassMember("+int x", string.Empty);
        var node = new LayoutNode { Id = "A", Shape = "classBox", ClassBox = new LayoutClassBox("A", [], [member, member], [member], []) };

        // Act
        var result = ShapeLibrary.Draw(node, ClassContext("classDiagram\n  class A\n", new SceneRect(0, 0, 40, 24)));

        // Assert: the name centered, the members from the left, 24 pixels apart
        Assert.Equal("classBox", result.Handler);
        Assert.Equal(new SceneRect(-42, -84, 84, 168), result.Bounds);
        Assert.Equal([-36.0, 36.0], Dividers(result).Select(box => box.Y));
        var members = result.Items.OfType<SceneGroup>().Where(g => g.Role == "class-member").ToList();
        Assert.Equal([-30.0, -30.0], members.Select(g => g.OffsetX));
        Assert.Equal(24, members[1].OffsetY - members[0].OffsetY, 9);
        Assert.Equal(new ShapePoint(42, 0), result.Intersect(new NodeBox(0, 0, 84, 168), new ShapePoint(300, 0)));
    }

    /// <summary>
    ///     Checks the class box of an empty class: the extra empty compartment below the name, with both dividers.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_EmptyClassBox_DrawsTheExtraCompartment()
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = "classBox", ClassBox = new LayoutClassBox("A", [], [], [], []) };

        // Act
        var result = ShapeLibrary.Draw(node, ClassContext("classDiagram\n  class A\n", new SceneRect(0, 0, 40, 24)));

        // Assert
        Assert.Equal(new SceneRect(-32, -42, 64, 84), result.Bounds);
        Assert.Equal([6.0, 24.0], Dividers(result).Select(box => box.Y));
    }

    /// <summary>
    ///     Checks that <c>class.hideEmptyMembersBox</c> draws an empty class as its name alone, without dividers.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_EmptyClassBoxHidden_DrawsTheNameAlone()
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = "classBox", ClassBox = new LayoutClassBox("A", [], [], [], []) };
        const string Text = "---\nconfig:\n  class:\n    hideEmptyMembersBox: true\n---\nclassDiagram\n  class A\n";

        // Act
        var result = ShapeLibrary.Draw(node, ClassContext(Text, new SceneRect(0, 0, 40, 24)));

        // Assert
        Assert.Equal(new SceneRect(-32, -24, 64, 48), result.Bounds);
        Assert.Empty(Dividers(result));
    }

    /// <summary>
    ///     Checks that SVG labels (a top-level <c>htmlLabels</c> of <c>false</c>) space the members by their height and
    ///     Mermaid's text padding of 3.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_ClassBoxSvgLabels_SpacesTheMembersByTheTextPadding()
    {
        // Arrange
        var member = new LayoutClassMember("+int x", string.Empty);
        var node = new LayoutNode { Id = "A", Shape = "classBox", ClassBox = new LayoutClassBox("A", [], [member, member], [], []) };
        const string Text = "---\nconfig:\n  htmlLabels: false\n---\nclassDiagram\n  class A\n";

        // Act
        var result = ShapeLibrary.Draw(node, ClassContext(Text, new SceneRect(0, 0, 40, 24)));

        // Assert
        var members = result.Items.OfType<SceneGroup>().Where(g => g.Role == "class-member").ToList();
        Assert.Equal(27, members[1].OffsetY - members[0].OffsetY, 9);
    }

    /// <summary>
    ///     Checks that only the top-level <c>htmlLabels</c> turns the class box's HTML labels off (Mermaid's
    ///     <c>evaluate(config.htmlLabels)</c>, which is true when the key is not set); the flowchart's does not.
    /// </summary>
    /// <param name="setting">The frontmatter line, or empty for none.</param>
    /// <param name="expected">Whether HTML labels are used.</param>
    [Theory]
    [InlineData("", true)]
    [InlineData("  htmlLabels: true\n", true)]
    [InlineData("  htmlLabels: false\n", false)]
    [InlineData("  flowchart:\n    htmlLabels: false\n", true)]
    public void ShapeLibrary_ClassHtmlLabels_FollowsMermaidsEvaluate(string setting, bool expected)
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "---\nconfig:\n" + setting + "  theme: default\n---\nclassDiagram\n");

        // Act
        var html = ShapeLibrary.ClassHtmlLabels(config);

        // Assert
        Assert.Equal(expected, html);
    }

    /// <summary>
    ///     Checks that the styles a diagram writes itself (a class diagram's note) are applied without reports, while the
    ///     same styles from the user report the properties this version does not draw; the fill applies either way.
    /// </summary>
    /// <param name="trusted">Whether the node's styles are the diagram's own.</param>
    /// <param name="expectedReports">The expected number of <c>mermaid-style-ignored</c> reports.</param>
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 2)]
    public void ShapeLibrary_Draw_TrustedNodeStyles_ApplyWithoutReports(bool trusted, int expectedReports)
    {
        // Arrange
        var sink = new MermaidReportSink(100, false);
        var node = new LayoutNode
        {
            Id = "note0",
            Shape = "note",
            Label = "n",
            CssStyles = ["text-align: left", "white-space: nowrap", "fill: #fff5ad", "stroke: #aaaa33"],
            TrustedStyles = trusted,
            Location = new MermaidLocation(null, 3, 5),
        };
        var warnings = new List<MermaidDiagnostic>();

        // Act
        var result = ShapeLibrary.Draw(node, Context(new SceneRect(0, 0, 20, 10), sink));
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Equal(expectedReports, warnings.Count(w => w.Code == MermaidDiagnosticCodes.StyleIgnored));
        Assert.Contains(result.Items, item => item is ScenePath { Fill: { } fill } && fill == new Rgba32(0xff, 0xf5, 0xad, 255));
    }

    /// <summary>
    ///     Checks that a state (<c>rect</c> with Mermaid's radii of 10) is drawn as <c>insertNode</c> draws it: a
    ///     <c>roundedRect</c> with the padding of 8 around the label, its corners rounded by the sheet's CSS radius of 5.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_StateRect_IsARoundedRectWithTheCssRadius()
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = "rect", Rx = 10, Ry = 10, Padding = 8, CenterLabel = true, CssClasses = " statediagram-state" };

        // Act
        var result = ShapeLibrary.Draw(node, StateContext(_ => new SceneRect(0, 0, 40, 24)));

        // Assert
        Assert.Equal("roundedRect", result.Handler);
        Assert.Equal(new SceneRect(-28, -20, 56, 40), result.Bounds);
        var arc = Assert.IsType<ScenePath>(result.Items[0]).Segments.First(s => s.Kind == SceneSegmentKind.ArcTo);
        Assert.Equal((5.0, 5.0), (arc.X1, arc.Y1));
        Assert.Equal(new ShapePoint(-20, -12), result.LabelOffset);
    }

    /// <summary>
    ///     Checks that a node's label is measured with its SVG text centered on its anchor only when the diagram's sheet
    ///     centers it (a flowchart's <c>.node .label text</c> rule), and starting at its anchor for a state diagram, whose
    ///     sheet has no such rule.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_SvgNodeText_IsCenteredOnlyWhenTheSheetCentersIt()
    {
        // Arrange
        var requests = new List<LabelRequest>();
        var state = StateContext(request =>
        {
            requests.Add(request);
            return new SceneRect(0, 0, 40, 24);
        });
        var flowchartRequests = new List<LabelRequest>();
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "flowchart TD\n  A\n");
        var flowchart = new ShapeContext
        {
            Config = config,
            Styles = FlowchartStyles.Create(config, null),
            MeasureLabel = request =>
            {
                flowchartRequests.Add(request);
                return LabelLayout.Of(request.HtmlLabels, new SceneRect(0, 0, 40, 24));
            },
        };

        // Act
        ShapeLibrary.Draw(new LayoutNode { Id = "A", Shape = "rect", Rx = 10, Ry = 10, Padding = 8, Label = "A", CenterLabel = true }, state);
        ShapeLibrary.Draw(new LayoutNode { Id = "A", Shape = "rect", Padding = 8, Label = "A" }, flowchart);

        // Assert
        Assert.False(Assert.Single(requests).CenterSvgText);
        Assert.True(Assert.Single(flowchartRequests).CenterSvgText);
    }

    /// <summary>
    ///     Checks that the state shapes are painted as a state diagram's sheet and Mermaid's attributes give them: the
    ///     start circle by <c>.node circle.state-start</c>; the end circles, the fork bar, the choice diamond and the note
    ///     by the rough.js attributes, which the sheet has no <c>.node path</c> rule to override.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_StateShapes_PaintThroughTheStateSheet()
    {
        // Arrange
        var context = StateContext(_ => new SceneRect(0, 0, 20, 10));
        var config = context.Config;
        Rgba32 Var(string name) => Color(config.ThemeVariable(name)!);
        ScenePath Part(string shape, int index = 0) =>
            Assert.IsType<ScenePath>(ShapeLibrary.Draw(new LayoutNode { Id = "A", Shape = shape, Label = "n", Padding = 15 }, context).Items[index]);

        // Act
        var start = Part("stateStart");
        var outer = Part("stateEnd");
        var inner = Part("stateEnd", 1);
        var fork = Part("fork");
        var choice = Part("choice");
        var note = Part("note");

        // Assert
        Assert.Equal((Var("specialStateColor"), Var("specialStateColor"), 1.0), (start.Fill, start.Stroke!.Color, start.Stroke.Width));
        Assert.Equal((Var("mainBkg"), Var("lineColor"), 2.0), (outer.Fill, outer.Stroke!.Color, outer.Stroke.Width));
        Assert.Equal((Var("nodeBorder"), Var("nodeBorder"), 2.0), (inner.Fill, inner.Stroke!.Color, inner.Stroke.Width));
        Assert.Equal((Var("lineColor"), Var("lineColor"), 1.3), (fork.Fill, fork.Stroke!.Color, fork.Stroke.Width));
        Assert.Equal((Var("mainBkg"), Var("nodeBorder"), 1.3), (choice.Fill, choice.Stroke!.Color, choice.Stroke.Width));
        Assert.Equal((Var("noteBkgColor"), Var("noteBorderColor"), 1.3), (note.Fill, note.Stroke!.Color, note.Stroke.Width));
    }

    /// <summary>
    ///     Checks that an end state's circles are drawn as rough.js draws them without roughness: the outline's last
    ///     curve doubles back past its start at angle 0, so the outer ring is 14.013 pixels wide from x -7 (as Chrome's
    ///     box of Mermaid's path), the inner circle 5.005, and the node is the box of the outline.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_StateEnd_IsTheRoughJsOutline()
    {
        // Arrange
        var context = StateContext(_ => new SceneRect(0, 0, 20, 10));

        // Act
        var end = ShapeLibrary.Draw(new LayoutNode { Id = "A", Shape = "stateEnd", Label = string.Empty, Padding = 8 }, context);
        var outer = Scene.BoundsOf([end.Items[0]])!.Value;
        var inner = Scene.BoundsOf([end.Items[1]])!.Value;

        // Assert
        Assert.Equal((-7.0, 14.013, 14.0), (Math.Round(outer.X, 3), Math.Round(outer.Width, 3), Math.Round(outer.Height, 3)));
        Assert.Equal((5.005, 5.0), (Math.Round(inner.Width, 3), Math.Round(inner.Height, 3)));
        Assert.Equal(outer.Width, end.Width, 3);
    }

    /// <summary>
    ///     Checks that a flowchart's sheet, whose node rule covers circles and paths, still paints the same shapes in the
    ///     node colors, and that a node's own styles win over both.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_StateShapesInAFlowchart_TakeTheNodeRule()
    {
        // Arrange
        var context = Context(new SceneRect(0, 0, 20, 10));
        var node = context.Styles.Node;

        // Act
        var shapes = new[] { "stateStart", "stateEnd", "fork", "choice", "note" }
            .Select(shape => Assert.IsType<ScenePath>(ShapeLibrary.Draw(new LayoutNode { Id = "A", Shape = shape, Label = "n", Padding = 15 }, context).Items[0]))
            .ToList();
        var styled = Assert.IsType<ScenePath>(ShapeLibrary.Draw(new LayoutNode { Id = "A", Shape = "fork", CssStyles = ["fill:#ff0000"] }, context).Items[0]);

        // Assert
        Assert.All(shapes, path => Assert.Equal((node.Fill, node.Stroke, node.StrokeWidth), (path.Fill, path.Stroke!.Color, path.Stroke.Width)));
        Assert.Equal(new Rgba32(255, 0, 0, 255), styled.Fill);
    }

    /// <summary>
    ///     Checks that a fork in a state diagram is the bar alone: the half of <c>state.padding</c> that Mermaid's
    ///     <c>forkJoin</c> adds to the node is replaced by the drawn box when the layout measures it.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_StateFork_IsTheBarWithoutThePadding()
    {
        // Arrange
        var node = new LayoutNode { Id = "F", Shape = "fork", Padding = 8, CssClasses = " statediagram-state" };

        // Act
        var result = ShapeLibrary.Draw(node, StateContext(_ => new SceneRect(0, 0, 20, 10)));

        // Assert
        Assert.Equal(new SceneRect(-35, -5, 70, 10), result.Bounds);
        Assert.Equal(new ShapePoint(0, -5), result.Intersect(new NodeBox(0, 0, 70, 10), new ShapePoint(0, -100)));
    }

    /// <summary>
    ///     Checks a state with descriptions (<c>rectWithTitle</c>) against Mermaid's drawing of a 61.09 pixel title over
    ///     an 80.94 pixel description: the narrower title centered, the description 33 pixels down, the rectangle with the
    ///     padding around and the CSS radius of 5, and the divider under the title.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_RectWithTitle_PlacesTitleDescriptionAndDivider()
    {
        // Arrange
        var node = new LayoutNode
        {
            Id = "D",
            Shape = "rectWithTitle",
            Label = "first line",
            Descriptions = ["second line"],
            Padding = 8,
            CssClasses = " statediagram-state",
        };
        var context = StateContext(request => new SceneRect(0, 0, request.Text == "first line" ? 61.09375 : 80.9375, 24));

        // Act
        var result = ShapeLibrary.Draw(node, context);

        // Assert: the boxes Mermaid's SVG has (rect x -44.47, y -32.5, 88.94 by 65; line y -4.5; label at -40.47, -29.5)
        Assert.Equal("rectWithTitle", result.Handler);
        Assert.Equal(new SceneRect(-44.46875, -32.5, 88.9375, 65), result.Bounds);
        var rect = Assert.IsType<ScenePath>(result.Items[0]);
        Assert.Equal(5, rect.Segments.First(s => s.Kind == SceneSegmentKind.ArcTo).X1);
        var divider = Assert.IsType<ScenePath>(Assert.Single(result.Items, item => item.Role == "state-divider"));
        Assert.Equal(new SceneRect(-44.46875, -4.5, 88.9375, 0), Scene.BoundsOf([divider]));
        Assert.Equal(Color(context.Config.ThemeVariable("nodeBorder")!), divider.Stroke!.Color);
        Assert.Equal(new ShapePoint(-40.46875 + 9.921875, -29.5), result.LabelOffset);
    }

    /// <summary>
    ///     Checks a composite state (<c>roundedWithTitle</c>): the outer rectangle with the CSS radius of 5 in the
    ///     composite title background, the title a pixel under its top, and the inner rectangle under the title in the
    ///     composite background, or the alternate background at an alternate depth.
    /// </summary>
    /// <param name="classes">The cluster's classes.</param>
    /// <param name="innerFill">The expected inner fill.</param>
    [Theory]
    [InlineData(" statediagram-state statediagram-cluster ", "#ffffff")]
    [InlineData(" statediagram-state statediagram-cluster statediagram-cluster-alt", "#f0f0f0")]
    public void ShapeLibrary_DrawCluster_RoundedWithTitle_DrawsOuterTitleAndInner(string classes, string innerFill)
    {
        // Arrange
        var node = new LayoutNode { Id = "C", Shape = "roundedWithTitle", Label = "C", Padding = 8, IsGroup = true, CssClasses = classes };
        var context = StateContext(_ => new SceneRect(0, 0, 30, 24));

        // Act
        var cluster = ShapeLibrary.DrawCluster(node, 100, 100, 200, 150, context);

        // Assert
        Assert.Equal((200.0, 150.0, -8.0), (cluster.Width, cluster.Height, cluster.Diff));
        var outer = Assert.IsType<ScenePath>(cluster.Shape.Items[0]);
        var inner = Assert.IsType<ScenePath>(cluster.Shape.Items[^1]);
        Assert.Equal(new SceneRect(0, 25, 200, 150), Scene.BoundsOf([outer]));
        Assert.Equal(5, outer.Segments.First(s => s.Kind == SceneSegmentKind.ArcTo).X1);
        Assert.Equal(Color(context.Config.ThemeVariable("compositeTitleBackground")!), outer.Fill);
        Assert.Equal(new SceneRect(0, 51, 200, 120), Scene.BoundsOf([inner]));
        Assert.DoesNotContain(inner.Segments, s => s.Kind == SceneSegmentKind.ArcTo);
        Assert.Equal(Color(innerFill), inner.Fill);
        Assert.Equal(new ShapePoint(85, 26), cluster.Shape.LabelOffset);
    }

    /// <summary>
    ///     Checks that a composite state narrower than its title is widened to the title and the padding, with Mermaid's
    ///     <c>diff</c>.
    /// </summary>
    [Fact]
    public void ShapeLibrary_DrawCluster_RoundedWithTitleNarrow_IsWidenedToTheTitle()
    {
        // Arrange
        var node = new LayoutNode { Id = "C", Shape = "roundedWithTitle", Label = "C", Padding = 8, IsGroup = true, CssClasses = " statediagram-state statediagram-cluster " };

        // Act
        var cluster = ShapeLibrary.DrawCluster(node, 0, 0, 20, 100, StateContext(_ => new SceneRect(0, 0, 30, 24)));

        // Assert
        Assert.Equal((38.0, 1.0), (cluster.Width, cluster.Diff));
        Assert.Equal(new SceneRect(-19, -50, 38, 100), cluster.Shape.Bounds);
    }

    /// <summary>
    ///     Checks the clusters without a title: a concurrency region (<c>divider</c>), a dashed rectangle in the alternate
    ///     background; and a note's group (<c>noteGroup</c>), an unpainted rectangle (kept as the cluster's outline,
    ///     which draws nothing).
    /// </summary>
    [Fact]
    public void ShapeLibrary_DrawCluster_DividerAndNoteGroup_AreDashedAndInvisible()
    {
        // Arrange
        var context = StateContext(_ => new SceneRect(0, 0, 30, 24));
        var divider = new LayoutNode { Id = "divider-id-1", Shape = "divider", Padding = 8, IsGroup = true, CssClasses = " statediagram-state statediagram-cluster statediagram-cluster-alt" };
        var group = new LayoutNode { Id = "A----parent", Shape = "noteGroup", Padding = 16, IsGroup = true, CssClasses = " statediagram-state" };

        // Act
        var region = ShapeLibrary.DrawCluster(divider, 50, 40, 100, 80, context);
        var notes = ShapeLibrary.DrawCluster(group, 50, 40, 100, 80, context);

        // Assert
        var rect = Assert.IsType<ScenePath>(Assert.Single(region.Shape.Items));
        Assert.Equal((Color("#f0f0f0"), Color(context.Config.ThemeVariable("nodeBorder")!)), (rect.Fill, rect.Stroke!.Color));
        Assert.Equal([10.0, 10.0], rect.Stroke.Dash);
        Assert.Equal((100.0, 80.0, -8.0), (region.Width, region.Height, region.Diff));
        var outline = Assert.IsType<ScenePath>(Assert.Single(notes.Shape.Items));
        Assert.Equal(("cluster", null, null), (outline.Role, outline.Fill, outline.Stroke));
        Assert.Equal((new SceneRect(0, 0, 100, 80), 0.0), (notes.Shape.Bounds, notes.Diff));
    }

    /// <summary>
    ///     Checks that the dividers of a class box take the class's styles as its outline does (Mermaid writes the
    ///     class's styles on each divider path): the stroke, its width and its dashes, and the theme's paint without
    ///     styles.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_ClassBoxStyles_PaintTheDividersAsTheOutline()
    {
        // Arrange
        var member = new LayoutClassMember("+int x", string.Empty);
        LayoutNode Node(string[] styles) =>
            new() { Id = "A", Shape = "classBox", ClassBox = new LayoutClassBox("A", [], [member], [member], styles) };
        var context = ClassContext("classDiagram\n  class A\n", new SceneRect(0, 0, 40, 24));

        // Act
        var styled = ShapeLibrary.Draw(Node(["stroke:#f00", "stroke-width:3px", "stroke-dasharray:5 5", "fill:#ff0"]), context);
        var plain = ShapeLibrary.Draw(Node([]), context);

        // Assert: both dividers stroked as the outline, never filled
        foreach (var (result, color, width) in new[] { (styled, new Rgba32(0xff, 0, 0, 255), 3.0), (plain, new Rgba32(0x93, 0x70, 0xdb, 255), 1.0) })
        {
            var outline = result.Items.OfType<ScenePath>().First(path => path.Role != "class-divider" && path.Stroke is not null);
            var dividers = result.Items.OfType<ScenePath>().Where(path => path.Role == "class-divider").ToList();
            Assert.Equal(2, dividers.Count);
            Assert.Equal((color, width), (outline.Stroke!.Color, outline.Stroke.Width));
            Assert.All(dividers, divider =>
            {
                Assert.Null(divider.Fill);
                Assert.Equal((outline.Stroke.Color, outline.Stroke.Width), (divider.Stroke!.Color, divider.Stroke.Width));
                Assert.Equal(outline.Stroke.Dash, divider.Stroke.Dash);
            });
        }

        Assert.Equal([5.0, 5.0], styled.Items.OfType<ScenePath>().First(path => path.Role == "class-divider").Stroke!.Dash!);
    }

    /// <summary>
    ///     Gets the boxes of the dividers of a drawn class box.
    /// </summary>
    /// <param name="result">The drawn node.</param>
    /// <returns>The boxes.</returns>
    private static List<SceneRect> Dividers(ShapeResult result) =>
        [.. result.Items.Where(item => item.Role == "class-divider").Select(item => Scene.BoundsOf([item])!.Value)];

    /// <summary>
    ///     Parses a CSS color.
    /// </summary>
    /// <param name="text">The color text.</param>
    /// <returns>The color.</returns>
    private static Rgba32 Color(string text) => StyleDeclarations.TryParsePaint(text, out var color) && color is { } value
        ? value
        : throw new ArgumentException($"not a color: {text}", nameof(text));

    /// <summary>
    ///     Checks <c>defaultMindmapNode</c> on a 40 by 24 label with a padding of 10: the path 80 by 34 (four times the
    ///     padding beside the label, half of it above and below) with four quadratic corners, filled by the mindmap
    ///     sheet's section rule and not stroked; the line along its bottom edge in the section's inverse color, 3 wide;
    ///     the label centered; and the rectangle's intersection.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_DefaultMindmapNode_DrawsPathLineAndCenteredLabel()
    {
        // Arrange
        var node = new LayoutNode { Id = "1", Shape = "defaultMindmapNode", Label = "a", Padding = 10, CssClasses = "mindmap-node section-0" };
        var context = MindmapContext(_ => new SceneRect(0, 0, 40, 24));

        // Act
        var result = ShapeLibrary.Draw(node, context);

        // Assert
        Assert.Equal("defaultMindmapNode", result.Handler);
        Assert.Equal(new SceneRect(-40, -17, 80, 34), result.Bounds);
        var body = Assert.IsType<ScenePath>(result.Items[0]);
        Assert.Equal(4, body.Segments.Count(s => s.Kind == SceneSegmentKind.QuadraticTo));
        Assert.Equal(new SceneRect(-40, -17, 80, 34), Scene.BoundsOf([body]));
        Assert.Equal(Color("#ffff78"), body.Fill);
        Assert.Null(body.Stroke);
        var line = Assert.IsType<ScenePath>(result.Items[1]);
        Assert.Equal(new SceneRect(-40, 17, 80, 0), Scene.BoundsOf([line]));
        Assert.Equal((Color("#ababff"), 3.0), (line.Stroke!.Color, line.Stroke.Width));
        Assert.Equal(new ShapePoint(-20, -12), result.LabelOffset);
        Assert.Equal(new ShapePoint(40, 0), result.Intersect(new NodeBox(0, 0, 80, 34), new ShapePoint(200, 0)));
    }

    /// <summary>
    ///     Checks that in a flowchart's sheet, which has no rule for a line, <c>defaultMindmapNode</c> draws its body in
    ///     the node colors and no line, at the same size.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_DefaultMindmapNodeInAFlowchart_DrawsNoLine()
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = "defaultMindmapNode", Label = "a", Padding = 10, CssClasses = "default" };
        var context = Context(new SceneRect(0, 0, 40, 24));

        // Act
        var result = ShapeLibrary.Draw(node, context);

        // Assert
        Assert.Equal(new SceneRect(-40, -17, 80, 34), result.Bounds);
        var body = Assert.IsType<ScenePath>(Assert.Single(result.Items));
        Assert.Equal((context.Styles.Node.Fill, context.Styles.Node.Stroke), (body.Fill, body.Stroke!.Color));
    }

    /// <summary>
    ///     Checks that <c>mindmapCircle</c> pads the label by the whole node padding, where <c>circle</c> pads it by half.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_MindmapCircle_TakesTheWholePadding()
    {
        // Arrange
        var context = MindmapContext(_ => new SceneRect(0, 0, 40, 24));
        LayoutNode Node(string shape) => new() { Id = "1", Shape = shape, Label = "a", Padding = 10, CssClasses = "mindmap-node section-0" };

        // Act
        var mindmap = ShapeLibrary.Draw(Node("mindmapCircle"), context);
        var circle = ShapeLibrary.Draw(Node("circle"), context);

        // Assert
        Assert.Equal("mindmapCircle", mindmap.Handler);
        Assert.Equal(new SceneRect(-30, -30, 60, 60), mindmap.Bounds);
        Assert.Equal(new SceneRect(-25, -25, 50, 50), circle.Bounds);
        Assert.Equal(30, mindmap.Intersect(new NodeBox(0, 0, 60, 60), new ShapePoint(300, 0)).X, 9);
    }

    /// <summary>
    ///     Checks that a mindmap's sheet paints each shape by the element it draws and the node's classes: a
    ///     <c>themeCSS</c> rule for <c>.section-1 circle</c> fills the circle only, the rectangle, hexagon, cloud and bang
    ///     keep the section's color, the root takes <c>git0</c>, and no shape is stroked; the label's color is the
    ///     section's span rule.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_MindmapSheet_PaintsByElementAndClasses()
    {
        // Arrange
        var site = new JsonObject { ["themeCSS"] = ".section-1 circle { fill: #ff0000 }" };
        var requests = new List<LabelRequest>();
        var context = MindmapContext(
            request =>
            {
                requests.Add(request);
                return new SceneRect(0, 0, 40, 24);
            },
            site);
        var section = Color(context.Config.ThemeVariable("cScale2")!);
        ScenePath Body(string shape, string classes = "mindmap-node section-1") =>
            Assert.IsType<ScenePath>(ShapeLibrary.Draw(new LayoutNode { Id = "1", Shape = shape, Label = "a", Padding = 10, CssClasses = classes }, context).Items[0]);

        // Act
        var bodies = new[] { "mindmapCircle", "rect", "rounded", "hexagon", "cloud", "bang" }.Select(shape => Body(shape)).ToList();
        var root = Body("defaultMindmapNode", "mindmap-node section-root section--1");

        // Assert
        Assert.Equal(Color("#ff0000"), bodies[0].Fill);
        Assert.All(bodies.Skip(1), body => Assert.Equal(section, body.Fill));
        Assert.All(bodies, body => Assert.Null(body.Stroke));
        Assert.Equal(Color(context.Config.ThemeVariable("git0")!), root.Fill);
        Assert.Equal(Color(context.Config.ThemeVariable("cScaleLabel2")!), requests[0].Font.Color);
        Assert.Equal(Color(context.Config.ThemeVariable("gitBranchLabel0")!), requests[^1].Font.Color);
    }

    /// <summary>
    ///     Checks <c>labelHelper</c>'s wrapping width: the node's width when it is truthy (a mindmap node's
    ///     <c>maxNodeWidth</c>), else <c>flowchart.wrappingWidth</c> (for a zero, NaN or unset width).
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_NodeWidth_IsTheWrappingWidthWhenTruthy()
    {
        // Arrange
        var requests = new List<LabelRequest>();
        var site = new JsonObject { ["flowchart"] = new JsonObject { ["wrappingWidth"] = 150 } };
        var context = MindmapContext(
            request =>
            {
                requests.Add(request);
                return new SceneRect(0, 0, 40, 24);
            },
            site);

        // Act
        foreach (var width in new double?[] { 100, 0, double.NaN, null })
        {
            ShapeLibrary.Draw(new LayoutNode { Id = "1", Shape = "defaultMindmapNode", Label = "a", Width = width, CssClasses = "mindmap-node section-0" }, context);
        }

        // Assert
        Assert.Equal([100.0, 150.0, 150.0, 150.0], requests.Select(request => request.Width));
    }

    /// <summary>
    ///     Checks <c>labelHelper</c>'s background rule: an SVG label gets its background when the node has an icon or an
    ///     image (a non-empty one, as JavaScript's <c>!!</c> tests it), whatever the shape.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_IconOrImage_GivesTheLabelItsBackground()
    {
        // Arrange
        var requests = new List<LabelRequest>();
        var context = MindmapContext(request =>
        {
            requests.Add(request);
            return new SceneRect(0, 0, 40, 24);
        });
        LayoutNode Node(string? icon, string? img) => new() { Id = "1", Shape = "defaultMindmapNode", Label = "a", Icon = icon, Img = img, CssClasses = "mindmap-node section-0" };

        // Act
        foreach (var node in new[] { Node("fa fa-book", null), Node(null, "a.png"), Node(string.Empty, null), Node(null, null) })
        {
            ShapeLibrary.Draw(node, context);
        }

        // Assert
        Assert.Equal([true, true, false, false], requests.Select(request => request.AddBackground));
    }

    /// <summary>
    ///     Creates the context of a mindmap whose labels all measure a given box.
    /// </summary>
    /// <param name="box">The box of a label request.</param>
    /// <param name="site">The site configuration, or null for none.</param>
    /// <returns>The context.</returns>
    private static ShapeContext MindmapContext(Func<LabelRequest, SceneRect> box, JsonObject? site = null)
    {
        var reports = new MermaidReportSink(100, false);
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(site ?? new JsonObject(), "mindmap\n  root\n");
        return new ShapeContext
        {
            Config = config,
            Styles = MindmapStyles.Create(config, reports),
            MeasureLabel = request => LabelLayout.Of(request.HtmlLabels, box(request)),
            Sink = reports,
        };
    }

    /// <summary>
    ///     Creates the context of a default state diagram whose labels measure a box chosen by their text.
    /// </summary>
    /// <param name="box">The box of a label request.</param>
    /// <returns>The context.</returns>
    private static ShapeContext StateContext(Func<LabelRequest, SceneRect> box)
    {
        var reports = new MermaidReportSink(100, false);
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "stateDiagram-v2\n  A --> B\n");
        return new ShapeContext
        {
            Config = config,
            Styles = StateStyles.Create(config, reports),
            MeasureLabel = request => LabelLayout.Of(request.HtmlLabels, box(request)),
            Sink = reports,
        };
    }

    /// <summary>
    ///     Creates the context of a class diagram whose labels all measure a fixed box.
    /// </summary>
    /// <param name="text">The class diagram text (its frontmatter configures the rendering).</param>
    /// <param name="fixedLabel">The box of every label.</param>
    /// <returns>The context.</returns>
    private static ShapeContext ClassContext(string text, SceneRect fixedLabel)
    {
        var reports = new MermaidReportSink(100, false);
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), text);
        return new ShapeContext
        {
            Config = config,
            Styles = ClassStyles.Create(config, reports),
            MeasureLabel = request => LabelLayout.Of(request.HtmlLabels, fixedLabel),
            Sink = reports,
        };
    }

    /// <summary>
    ///     Checks the table of an entity with attributes (the default <c>htmlLabels</c>, so the paddings of 20 and 15
    ///     grow to 25 and 18.75): the columns as wide as their widest text and the padding, the empty comment column
    ///     dropped, the rows as high as their texts and the text padding, the texts placed in their columns, the rows
    ///     filled in turn, and the dividers under the name (twice) and at the column boundaries, each a fill and a stroke
    ///     path.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_ErBoxWithAttributes_LaysOutTheTable()
    {
        // Arrange
        var node = new LayoutNode
        {
            Id = "entity-A-0",
            Label = "A",
            Shape = "erBox",
            ErBox = new LayoutErBox(string.Empty, [new LayoutErAttribute("int", "x", ["PK"], string.Empty), new LayoutErAttribute("int", "y", [], string.Empty)]),
        };

        // Act
        var result = ShapeLibrary.Draw(node, ErContext("erDiagram\n  A\n"));

        // Assert: three columns of 65, a name row and two rows of 42.75
        Assert.Equal("erBox", result.Handler);
        Assert.Equal(new SceneRect(-97.5, -64.125, 195, 128.25), result.Bounds);
        var groups = result.Items.OfType<SceneGroup>().ToList();
        var name = Assert.Single(groups, g => g.Role == "er-name");
        Assert.Equal((-20, -54.75), (name.OffsetX, name.OffsetY));
        var types = groups.Where(g => g.Role == "er-attribute-type").ToList();
        Assert.Equal([(-85.0, -12.0), (-85.0, 30.75)], types.Select(g => (g.OffsetX, g.OffsetY)));
        Assert.Equal([-20.0, -20.0], groups.Where(g => g.Role == "er-attribute-name").Select(g => g.OffsetX));
        Assert.Equal(8, result.Items.Count(item => item.Role == "er-divider"));
        var fills = result.Items.OfType<ScenePath>().Where(path => path.Role == "node" && path.Fill is not null).Select(path => path.Fill).ToList();
        Assert.Equal([Color("#ECECFF"), Color("#FFFFFF"), Color("#F1F1FF")], fills);
        Assert.Equal(new ShapePoint(97.5, 0), result.Intersect(new NodeBox(0, 0, 195, 128.25), new ShapePoint(300, 0)));
    }

    /// <summary>
    ///     Checks an entity without attributes: a rectangle with the diagram padding left and right and one and a half
    ///     times it above and below, widened to <c>er.minEntityWidth</c> when Mermaid's text width and the padding are
    ///     narrower.
    /// </summary>
    /// <param name="textWidth">The width Mermaid's <c>calculateTextWidth</c> gives the name.</param>
    /// <param name="width">The expected width.</param>
    [Theory]
    [InlineData(10, 100)]
    [InlineData(70, 80)]
    public void ShapeLibrary_Draw_ErBoxWithoutAttributes_IsAtLeastTheMinimumWidth(double textWidth, double width)
    {
        // Arrange
        var node = new LayoutNode { Id = "entity-A-0", Label = "A", Shape = "erBox", ErBox = new LayoutErBox(string.Empty, []) };
        var context = ErContext("erDiagram\n  A\n");
        context = new ShapeContext
        {
            Config = context.Config,
            Styles = context.Styles,
            MeasureLabel = context.MeasureLabel,
            Sink = context.Sink,
            TextWidth = _ => textWidth,
        };

        // Act
        var result = ShapeLibrary.Draw(node, context);

        // Assert: the label of 40 by 24, centered
        Assert.Equal(new SceneRect(-width / 2, -42, width, 84), result.Bounds);
        Assert.Equal(new SceneRect(-20, -12, 40, 24), result.LabelBox);
    }

    /// <summary>
    ///     Checks that with SVG text an empty cell counts in the node's box where it is placed (Chrome's <c>getBBox</c>
    ///     includes an empty text element), so an empty comment cell after the keys column widens the node past its
    ///     table; the paddings grow by a quarter.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_ErBoxSvgEmptyCell_WidensTheBox()
    {
        // Arrange
        var node = new LayoutNode
        {
            Id = "entity-A-0",
            Label = "A",
            Shape = "erBox",
            ErBox = new LayoutErBox(string.Empty, [new LayoutErAttribute("int", "x", ["PK"], string.Empty)]),
        };

        // Act
        var result = ShapeLibrary.Draw(node, ErContext("---\nconfig:\n  htmlLabels: false\n---\nerDiagram\n  A\n"));

        // Assert: the table is 195 wide from -97.5; the comment cell is at -97.5 + 12.5 + 195
        var table = Assert.IsType<ScenePath>(result.Items[0]);
        Assert.Equal(new SceneRect(-97.5, -42.75, 195, 85.5), Scene.BoundsOf([table]));
        Assert.Equal(-97.5, result.Bounds.X, 9);
        Assert.Equal(110, result.Bounds.Right, 9);
    }

    /// <summary>
    ///     Checks how the entity box reads the top-level <c>htmlLabels</c>: unset, the table's texts are HTML and the
    ///     paddings grow; true, they are HTML and the paddings do not grow; false, they are SVG text and the paddings grow.
    /// </summary>
    /// <param name="setting">The frontmatter line, or empty for none.</param>
    /// <param name="cells">Whether the table's texts are HTML.</param>
    /// <param name="scaled">Whether the paddings grow.</param>
    /// <param name="plain">Whether the name of an entity without attributes is HTML.</param>
    [Theory]
    [InlineData("", true, true, true)]
    [InlineData("  htmlLabels: true\n", true, false, true)]
    [InlineData("  htmlLabels: false\n", false, true, false)]
    public void ShapeLibrary_ErHtmlLabels_FollowsTheTopLevelSetting(string setting, bool cells, bool scaled, bool plain)
    {
        // Arrange
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "---\nconfig:\n" + setting + "  theme: default\n---\nerDiagram\n  A\n");

        // Act
        var result = ShapeLibrary.ErHtmlLabels(config);

        // Assert
        Assert.Equal((cells, scaled, plain), result);
    }

    /// <summary>
    ///     Creates the context of an ER diagram whose labels measure 40 by 24, or nothing for an empty text.
    /// </summary>
    /// <param name="text">The diagram text.</param>
    /// <returns>The context.</returns>
    private static ShapeContext ErContext(string text)
    {
        var reports = new MermaidReportSink(100, false);
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), text);
        return new ShapeContext
        {
            Config = config,
            Styles = ErStyles.Create(config, reports),
            MeasureLabel = request => LabelLayout.Of(request.HtmlLabels, request.Text.Length == 0 ? new SceneRect(0, 0, 0, 0) : new SceneRect(0, 0, 40, 24)),
            Sink = reports,
        };
    }

    /// <summary>
    ///     Creates the context of a default flowchart.
    /// </summary>
    /// <param name="fixedLabel">A fixed label box instead of a measured one, or null.</param>
    /// <param name="sink">The sink, or null for a new one.</param>
    /// <param name="images">The pictures, or null.</param>
    /// <returns>The context.</returns>
    private static ShapeContext Context(SceneRect? fixedLabel = null, MermaidReportSink? sink = null, NodeImages? images = null)
    {
        var reports = sink ?? new MermaidReportSink(100, false);
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "flowchart TD\n  A\n");
        var labels = new LabelBuilder(ScenePainter.CreateFontResolver(), reports, images);
        return new ShapeContext
        {
            Config = config,
            Styles = FlowchartStyles.Create(config, reports),
            MeasureLabel = fixedLabel is { } box ? request => LabelLayout.Of(request.HtmlLabels, box) : labels.Build,
            Images = images,
            Sink = reports,
        };
    }
}
