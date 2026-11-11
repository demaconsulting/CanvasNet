using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Pie;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Pie;

/// <summary>
///     Unit tests for <c>PieRenderer</c>: d3's pie angles, the arc path text and centroid, <c>toFixed(0)</c>, the text as
///     the browser shows it, and the drawing of Mermaid's <c>draw</c> (the 1% filter, the ordinal colors, the legend
///     positions and the view box, the hole, the highlighted slice, an empty chart and a zero sum).
/// </summary>
public sealed class PieRendererTests
{
    /// <summary>
    ///     Validates d3's <c>pie()</c> with <c>sort(null)</c>: each value's share of a turn, in the data's order, with zero
    ///     and negative values taking no angle.
    /// </summary>
    [Fact]
    public void PieRenderer_Pie_Values_GiveSharesOfATurnInOrder()
    {
        // Act
        var arcs = PieRenderer.Pie([1, 0, 1, 2]);

        // Assert
        Assert.Equal(
            [(0, Math.PI / 2), (Math.PI / 2, Math.PI / 2), (Math.PI / 2, Math.PI), (Math.PI, 2 * Math.PI)],
            arcs.Select(a => (a.StartAngle, a.EndAngle)));
        Assert.All(PieRenderer.Pie([0, 0]), arc => Assert.Equal((0.0, 0.0), (arc.StartAngle, arc.EndAngle)));
    }

    /// <summary>
    ///     Validates the path text of d3's <c>arc()</c> with 3 digits: a sector, a whole circle and an annular sector.
    /// </summary>
    [Fact]
    public void PieRenderer_ArcPath_Sectors_MatchD3()
    {
        // Act
        var quarter = PieRenderer.ArcPath(0, 185, new PieArc(0, Math.PI / 2)).Data;
        var circle = PieRenderer.ArcPath(0, 185, new PieArc(0, 2 * Math.PI)).Data;
        var donut = PieRenderer.ArcPath(74, 185, new PieArc(0, Math.PI)).Data;
        var large = PieRenderer.ArcPath(0, 185, new PieArc(0, 5)).Data;

        // Assert
        Assert.Equal("M0,-185A185,185,0,0,1,185,0L0,0Z", quarter);
        Assert.Equal("M0,-185A185,185,0,1,1,0,185A185,185,0,1,1,0,-185Z", circle);
        Assert.Equal("M0,-185A185,185,0,1,1,0,185L0,74A74,74,0,1,0,0,-74Z", donut);
        Assert.Equal("M0,-185A185,185,0,1,1,-177.401,-52.478L0,0Z", large);
    }

    /// <summary>
    ///     Validates d3's <c>arc.centroid</c>: the middle radius at the middle angle.
    /// </summary>
    [Fact]
    public void PieRenderer_Centroid_IsAtTheMiddleRadiusAndAngle()
    {
        // Act
        var (x, y) = PieRenderer.Centroid(100, 100, new PieArc(0, Math.PI));

        // Assert
        Assert.Equal(100, x, 1e-9);
        Assert.Equal(0, y, 1e-9);
    }

    /// <summary>
    ///     Validates JavaScript's <c>toFixed(0)</c>: ties away from zero on the exact value, the sign of a negative
    ///     number that rounds to zero, and the special values.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="expected">The text.</param>
    [Theory]
    [InlineData(0.5, "1")]
    [InlineData(1.5, "2")]
    [InlineData(2.5, "3")]
    [InlineData(0.49999999999999994, "0")]
    [InlineData(79.36507936507937, "79")]
    [InlineData(-0.4, "-0")]
    [InlineData(-0.0, "0")]
    [InlineData(-2.5, "-3")]
    [InlineData(double.NaN, "NaN")]
    [InlineData(double.PositiveInfinity, "Infinity")]
    public void PieRenderer_ToFixed0_RoundsAsJavaScript(double value, string expected)
    {
        // Act
        var text = PieRenderer.ToFixed0(value);

        // Assert
        Assert.Equal(expected, text);
    }

    /// <summary>
    ///     Validates the text the browser shows: Mermaid's entity placeholders are decoded, a reference the user wrote is
    ///     shown as written, and the white space is collapsed.
    /// </summary>
    [Fact]
    public void PieRenderer_DisplayText_DecodesOnlyThePlaceholders()
    {
        // Act
        var text = PieRenderer.DisplayText("A \ufb02\u00b0amp\u00b6\u00df B &amp;\n\u00a0\ufb02\u00b0\u00b035\u00b6\u00df  ");

        // Assert
        Assert.Equal("A & B &amp; \u00a0#", text);
    }

    /// <summary>
    ///     Validates the default drawing: the pie at (225, 225), the outer circle a stroke's half beyond the radius, the
    ///     slices and labels of the sections of 1% or more, a legend entry for every section at 216 to the right, and the
    ///     view box widened by the legend.
    /// </summary>
    [Fact]
    public void PieRenderer_Draw_RightLegend_ListsEverySectionAndWidensTheViewBox()
    {
        // Act
        var scene = SceneQuery.Layout("pie\n\"Big\" : 1000\n\"Tiny\" : 5\n\"Zero\" : 0\n\"Small\" : 20\n");
        var items = Walk(scene);

        // Assert
        var root = Assert.IsType<SceneGroup>(Assert.Single(scene.Items));
        Assert.Equal((225.0, 225.0), (root.OffsetX, root.OffsetY));
        var outer = Scene.BoundsOf([items.Single(i => i.Item.Role == "pie-outer-circle").Item])!.Value;
        Assert.Equal(372, outer.Width, 1e-6);
        Assert.Equal(["Big", "Small"], items.Where(i => i.Item.Role == "pie-slice").Select(i => i.Item.Id));
        Assert.Equal(["98%", "2%"], items.Where(i => i.Item is SceneText { Role: "pie-slice-label" }).Select(i => Text(i.Item)));
        var legend = items.Where(i => i.Item.Role == "pie-legend").ToList();
        Assert.Equal(["Big", "Tiny", "Zero", "Small"], legend.Select(i => i.Item.Id));
        Assert.Equal((441.0, 181.0), Position(legend[0]));
        Assert.Equal((441.0, 247.0), Position(legend[3]));
        var widest = items.Where(i => i.Item.Role == "pie-legend-text").Max(i => ((SceneText)i.Item).Box.Width);
        Assert.Equal(new SceneRect(0, 0, 490 + 22 + widest, 450), scene.ViewBox);
    }

    /// <summary>
    ///     Validates the colors: the theme's <c>pie1</c> to <c>pie12</c> by each section's index, the thirteenth section
    ///     taking <c>pie1</c> again, and the slices drawn at the theme's opacity of 0.7.
    /// </summary>
    [Fact]
    public void PieRenderer_Draw_Colors_WrapAfterTwelve()
    {
        // Arrange
        var text = "pie\n" + string.Concat(Enumerable.Range(1, 13).Select(i => $"\"S{i}\" : 10\n"));

        // Act
        var items = Walk(SceneQuery.Layout(text));

        // Assert
        var rects = items.Where(i => i.Item.Role == "pie-legend-rect").Select(i => ((ScenePath)i.Item).Fill).ToList();
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), rects[0]);
        Assert.Equal(rects[0], rects[12]);
        Assert.NotEqual(rects[0], rects[1]);
        var slice = (ScenePath)items.First(i => i.Item.Role == "pie-slice").Item;
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 179), slice.Fill);
    }

    /// <summary>
    ///     Validates the legend positions and the pie's move: a top legend above the pie, which moves down by the legend
    ///     and grows the height; a bottom legend below it; a left legend, which moves the pie right; and a centered legend.
    /// </summary>
    /// <param name="position">The legend position.</param>
    /// <param name="pieMovesRight">Whether the pie moves right.</param>
    /// <param name="height">The height of the view box.</param>
    [Theory]
    [InlineData("top", false, 494)]
    [InlineData("bottom", false, 494)]
    [InlineData("left", true, 450)]
    [InlineData("center", false, 450)]
    public void PieRenderer_Draw_LegendPosition_PlacesTheLegendAndThePie(string position, bool pieMovesRight, double height)
    {
        // Act
        var scene = SceneQuery.Layout($"---\nconfig:\n  pie:\n    legendPosition: {position}\n---\npie\n\"a\" : 1\n\"b\" : 1\n");
        var items = Walk(scene);

        // Assert
        var chart = (SceneGroup)items.Single(i => i.Item.Role == "pie-chart").Item;
        var legend = items.Where(i => i.Item.Role == "pie-legend").Select(Position).ToList();
        Assert.Equal(height, scene.ViewBox.Height);
        Assert.Equal(pieMovesRight, chart.OffsetX > 0);
        Assert.Equal(position == "top" ? 66 : 0, chart.OffsetY);
        Assert.Equal(22, legend[1].Y - legend[0].Y);
        var expectedY = position switch
        {
            "top" => 225 - 185.0,
            "bottom" => 225 + 185.0 + 22,
            _ => 225 - 22.0,
        };
        Assert.Equal(expectedY, legend[0].Y);
    }

    /// <summary>
    ///     Validates the hole: a <c>donutHole</c> above 0 and at most 0.9 cuts an inner arc, and any other value none.
    /// </summary>
    [Fact]
    public void PieRenderer_Draw_DonutHole_CutsTheInnerArcInRange()
    {
        // Act
        var donut = Slice(SceneQuery.Layout("---\nconfig:\n  pie:\n    donutHole: 0.5\n---\npie\n\"a\" : 1\n\"b\" : 3\n"));
        var plain = Slice(SceneQuery.Layout("---\nconfig:\n  pie:\n    donutHole: 0.95\n---\npie\n\"a\" : 1\n\"b\" : 3\n"));

        // Assert
        Assert.Equal(2, donut.Segments.Count(s => s.Kind == SceneSegmentKind.ArcTo));
        Assert.Contains(donut.Segments, s => s.Kind == SceneSegmentKind.ArcTo && Math.Abs(s.X1 - 92.5) < 1e-9);
        Assert.Single(plain.Segments, s => s.Kind == SceneSegmentKind.ArcTo);
    }

    /// <summary>
    ///     Validates the highlighted slice: drawn 1.05 times larger around the pie's center and opaque, while the others
    ///     keep the theme's opacity.
    /// </summary>
    [Fact]
    public void PieRenderer_Draw_HighlightSlice_EnlargesTheSlice()
    {
        // Act
        var items = Walk(SceneQuery.Layout("---\nconfig:\n  pie:\n    highlightSlice: b\n---\npie\n\"a\" : 1\n\"b\" : 1\n"));

        // Assert
        var slices = items.Where(i => i.Item.Role == "pie-slice").Select(i => (ScenePath)i.Item).ToList();
        Assert.Equal(185 * 1.05, slices[1].Segments[1].X1, 1e-9);
        Assert.Equal(185, slices[0].Segments[1].X1);
        Assert.Equal(255, slices[1].Fill!.Value.A);
        Assert.Equal(179, slices[0].Fill!.Value.A);
    }

    /// <summary>
    ///     Validates an empty chart: no slice and no legend; the legend width is minus infinity, so the view box is the
    ///     left half of the pie, as in Mermaid.
    /// </summary>
    [Fact]
    public void PieRenderer_Draw_EmptyChart_ViewBoxIsTheLeftHalf()
    {
        // Act
        var scene = SceneQuery.Layout("pie\n");
        var items = Walk(scene);

        // Assert
        Assert.DoesNotContain(items, i => i.Item.Role is "pie-slice" or "pie-legend");
        Assert.Equal(new SceneRect(0, 0, 225, 450), scene.ViewBox);
    }

    /// <summary>
    ///     Validates a chart whose values are all zero: the legend lists them, and no slice is drawn.
    /// </summary>
    [Fact]
    public void PieRenderer_Draw_ZeroSum_DrawsTheLegendOnly()
    {
        // Act
        var items = Walk(SceneQuery.Layout("pie showData\n\"a\" : 0\n\"b\" : 0\n"));

        // Assert
        Assert.DoesNotContain(items, i => i.Item.Role is "pie-slice" or "pie-slice-label");
        Assert.Equal(["a [0]", "b [0]"], items.Where(i => i.Item.Role == "pie-legend-text").Select(i => Text(i.Item)));
    }

    /// <summary>
    ///     Validates the title: centered 200 above the pie's center, and a title wider than the chart widening the view
    ///     box to the left; the frontmatter title is drawn when the text has none.
    /// </summary>
    [Fact]
    public void PieRenderer_Draw_LongTitle_WidensTheViewBoxToTheLeft()
    {
        // Act
        var scene = SceneQuery.Layout("pie title " + string.Concat(Enumerable.Repeat("wide ", 40)) + "\n\"a\" : 1\n");
        var fallback = Walk(SceneQuery.Layout("---\ntitle: From the frontmatter\n---\npie\n\"a\" : 1\n"));

        // Assert
        var title = (SceneText)Walk(scene).Single(i => i.Item.Role == "title").Item;
        Assert.Equal((0.0, -200.0, SceneTextAnchor.Middle), (title.X, title.Baseline, title.Anchor));
        Assert.True(scene.ViewBox.X < 0);
        Assert.Equal(225 - (scene.ViewBox.Width / 2), scene.ViewBox.X, 1e-9);
        Assert.Equal("From the frontmatter", Text(fallback.Single(i => i.Item.Role == "title").Item));
    }

    /// <summary>
    ///     Gets every item of a scene with its absolute offset.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The items.</returns>
    private static List<(SceneItem Item, double X, double Y)> Walk(Scene scene) =>
        [.. scene.Walk().Select(entry => (entry.Item, entry.OffsetX, entry.OffsetY))];

    /// <summary>
    ///     Gets the absolute position of a group.
    /// </summary>
    /// <param name="entry">The group and its offset.</param>
    /// <returns>The position.</returns>
    private static (double X, double Y) Position((SceneItem Item, double X, double Y) entry) =>
        (entry.X + ((SceneGroup)entry.Item).OffsetX, entry.Y + ((SceneGroup)entry.Item).OffsetY);

    /// <summary>
    ///     Gets the text of a text item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The text.</returns>
    private static string Text(SceneItem item) => string.Concat(((SceneText)item).Runs.Select(run => run.Text));

    /// <summary>
    ///     Gets the first slice of a scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The slice.</returns>
    private static ScenePath Slice(Scene scene) => (ScenePath)scene.Walk().First(entry => entry.Item.Role == "pie-slice").Item;
}
