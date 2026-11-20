using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer.Help;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Help;

/// <summary>
///     Unit tests for the style and composition guidance of the library help cards: the cards exist and link to each
///     other, and the advice they give holds for the real renderer.
/// </summary>
public partial class HelpCatalogTests
{
    /// <summary>
    ///     The drawing formats, which each have a style card and a composition card.
    /// </summary>
    private static readonly string[] DrawingFormats = ["scad", "vega-lite", "mermaid", "plantuml", "svg"];

    /// <summary>
    ///     The white background a picture is flattened over, as an image written by a host usually is.
    /// </summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     Test that every drawing format has a style card and a composition card, that each links to the hub of its
    ///     kind, and that its root card lists both.
    /// </summary>
    [Fact]
    public void HelpCatalog_DrawingFormats_HaveStyleAndCompositionCards()
    {
        // Act / Assert: each format has both cards, below its root, linked to the hubs
        foreach (var format in DrawingFormats)
        {
            var root = HelpCardText.Card(format);
            foreach (var kind in new[] { "style", "composition" })
            {
                var card = HelpCardText.Card($"{format}/{kind}");
                Assert.Contains(card.Id, root.ChildIds);
                Assert.Contains(kind, card.SeeAlsoIds);
                Assert.Contains(kind, root.SeeAlsoIds);
            }

            Assert.Contains($"{format}/composition", HelpCardText.Card($"{format}/style").SeeAlsoIds);
            Assert.Contains($"{format}/style", HelpCardText.Card($"{format}/composition").SeeAlsoIds);
        }
    }

    /// <summary>
    ///     Test that the <c>style</c> and <c>composition</c> hubs link to the card of every drawing format and to each
    ///     other, that the format cards link back, and that a reader of a diagram card reaches the hubs in a few steps.
    /// </summary>
    [Fact]
    public void HelpCatalog_StyleAndCompositionHubs_LinkBothWays()
    {
        // Arrange: the hubs
        var style = HelpCardText.Card("style");
        var composition = HelpCardText.Card("composition");

        // Assert: the hubs link to every format card and to each other
        Assert.All(DrawingFormats, format => Assert.Contains($"{format}/style", style.SeeAlsoIds));
        Assert.All(DrawingFormats, format => Assert.Contains($"{format}/composition", composition.SeeAlsoIds));
        Assert.Contains("composition", style.SeeAlsoIds);
        Assert.Contains("style", composition.SeeAlsoIds);

        // Assert: from 'mermaid/gantt' the See also links lead to the style card, then to the composition card and the hubs
        Assert.Contains("mermaid/style", HelpCardText.Card("mermaid/gantt").SeeAlsoIds);
        Assert.Contains("mermaid/composition", HelpCardText.Card("mermaid/style").SeeAlsoIds);
        Assert.Contains("style", HelpCardText.Card("mermaid/style").SeeAlsoIds);
        Assert.Contains("composition", HelpCardText.Card("mermaid/composition").SeeAlsoIds);
    }

    /// <summary>
    ///     Test that the <c>mermaid/style</c> card lists exactly the five themes and that each one draws the flowchart
    ///     example in strict mode without a diagnostic, and that the styled example draws under every theme too (its own
    ///     theme wins).
    /// </summary>
    /// <param name="theme">The theme.</param>
    [Theory]
    [InlineData("default")]
    [InlineData("base")]
    [InlineData("dark")]
    [InlineData("forest")]
    [InlineData("neutral")]
    public void HelpCatalog_MermaidStyleCard_EveryListedThemeRenders(string theme)
    {
        // Arrange: the card's list of themes, and the two examples
        var themes = HelpCardText.Bullets("mermaid/style", "Themes")[0];
        Assert.StartsWith("The themes are `default`, `base`, `dark`, `forest`, and `neutral`.", themes);
        using var directory = new TempDirectory();
        var flowchart = HelpCardText.SaveExample(directory, "mermaid", "flowchart", "flowchart.mmd");
        var styled = HelpCardText.SaveExample(directory, "mermaid", "styled", "styled-flow.mmd");
        var options = new SourceOptions { Strict = true, Mermaid = new DiagramOptions { Theme = theme } };

        // Act / Assert: each draws without a diagnostic
        foreach (var input in new[] { flowchart, styled })
        {
            using var loaded = SourceLoader.Load(input, options);
            Assert.Empty(loaded.Diagnostics);
        }
    }

    /// <summary>
    ///     Test that every key of the <c>config</c> of the styled Vega-Lite example, each of which the
    ///     <c>vega-lite/style</c> card recommends, visibly changes the picture: the chart drawn without that key differs
    ///     from the chart drawn with it.
    /// </summary>
    [Fact]
    public void HelpCatalog_VegaLiteStyledExample_ConfigChangesThePicture()
    {
        // Arrange: the example, its config keys, and the card text
        var source = HelpCardText.Example("vega-lite", "styled");
        var keys = JsonNode.Parse(source)!["config"]!.AsObject().Select(p => p.Key).ToList();
        var card = HelpCardText.TextOf("vega-lite/style");
        using var directory = new TempDirectory();
        using var full = RenderChart(directory, "full", source);

        // Act / Assert: the card names each key, and leaving each out changes the picture
        Assert.True(keys.Count >= 6, $"The example sets only {keys.Count} config keys.");
        foreach (var key in keys)
        {
            Assert.Contains($"`{key}", card, StringComparison.Ordinal);
            var without = JsonNode.Parse(source)!;
            without["config"]!.AsObject().Remove(key);
            using var changed = RenderChart(directory, key, without.ToJsonString());
            Assert.False(SamePixels(full.Surface, changed.Surface), $"Leaving out config.{key} does not change the picture.");
        }

        // Renders a chart in strict mode over white
        static ViewerImage RenderChart(TempDirectory directory, string name, string chart)
        {
            var input = directory.Combine($"{name}.vl.json");
            File.WriteAllText(input, chart);
            return RenderStrict(input);
        }
    }

    /// <summary>
    ///     Test that a chart that sets <c>"background": "transparent"</c>, as the style cards advise, stays transparent
    ///     inside an SVG figure, while a chart without it keeps its white background there.
    /// </summary>
    [Fact]
    public void HelpCatalog_StyleAdvice_TransparentChartInsideSvg_ShowsThePage()
    {
        // Arrange: a yellow page that places the styled chart, once as it is and once transparent
        var source = HelpCardText.Example("vega-lite", "styled");
        var transparent = JsonNode.Parse(source)!;
        transparent["background"] = "transparent";
        using var directory = new TempDirectory();
        File.WriteAllText(directory.Combine("white.vl.json"), source);
        File.WriteAllText(directory.Combine("clear.vl.json"), transparent.ToJsonString());
        foreach (var name in new[] { "white", "clear" })
        {
            File.WriteAllText(
                directory.Combine($"{name}.svg"),
                $"<svg xmlns='http://www.w3.org/2000/svg' width='600' height='400'><rect width='600' height='400' fill='#ffff00'/><image href='{name}.vl.json' x='0' y='0' width='600' height='400'/></svg>");
        }

        // Act: the color of a pixel inside the chart's padding
        Rgba32 PixelAt(string name)
        {
            using var image = CanvasNetViewer.Render(directory.Combine($"{name}.svg"));
            return image.Surface[300, 8];
        }

        // Assert: the transparent chart shows the page, the other its own white
        Assert.Equal(new Rgba32(255, 255, 0, 255), PixelAt("clear"));
        Assert.Equal(White, PixelAt("white"));
    }

    /// <summary>
    ///     Test that the <c>svg/framed-figure</c> card is a child of <c>svg</c> and is linked both ways with the
    ///     <c>composition</c> hub, the <c>svg/composition</c> and <c>svg/style</c> cards, the <c>scad/anchors</c> and
    ///     <c>scad/composition</c> cards, and the <c>positions</c> card.
    /// </summary>
    [Fact]
    public void HelpCatalog_FramedFigureCard_IsLinkedFromTheCompositionAndAnchorCards()
    {
        // Arrange: the card and the cards that teach composition, style, and anchors
        var card = HelpCardText.Card("svg/framed-figure");
        string[] linked = ["composition", "svg/composition", "svg/style", "scad/anchors", "positions"];

        // Assert: it is listed under svg, links to each, and each links back
        Assert.Contains(card.Id, HelpCardText.Card("svg").ChildIds);
        Assert.All(linked, id => Assert.Contains(id, card.SeeAlsoIds));
        Assert.All(linked.Append("scad/composition"), id => Assert.Contains(card.Id, HelpCardText.Card(id).SeeAlsoIds));
    }

    /// <summary>
    ///     Test that every callout arrow of the <c>framed</c> example of the <c>svg/framed-figure</c> card ends on an
    ///     anchor of the model it embeds: the pixel of the anchor when the model is rendered alone at the size of its
    ///     image, plus the image x and y; that at least two different anchors are hit; and that the arrows use a marker.
    /// </summary>
    [Fact]
    public void HelpCatalog_FramedFigureExample_CalloutsEndOnTheModelAnchors()
    {
        // Arrange: the figure, the model image, and the callout lines
        var figure = XDocument.Parse(HelpCardText.Example("svg", "framed"));
        var elements = figure.Descendants().ToList();
        var image = Assert.Single(elements, e => e.Name.LocalName == "image");
        Assert.Equal("anchors.scad", (string?)image.Attribute("href"));
        float Number(XElement element, string name) => float.Parse((string)element.Attribute(name)!, CultureInfo.InvariantCulture);
        var (x, y, width, height) = (Number(image, "x"), Number(image, "y"), Number(image, "width"), Number(image, "height"));
        var tips = elements
            .Where(e => e.Name.LocalName == "polyline" && (string?)e.Attribute("class") == "callout")
            .Select(e => ((string)e.Attribute("points")!).Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1].Split(','))
            .Select(p => (X: float.Parse(p[0], CultureInfo.InvariantCulture), Y: float.Parse(p[1], CultureInfo.InvariantCulture)))
            .ToList();
        Assert.Contains(elements, e => e.Name.LocalName == "marker" && (string?)e.Attribute("id") == "arrow");
        Assert.Contains("marker-end: url(#arrow)", figure.ToString(), StringComparison.Ordinal);

        // Act: render the model alone at the size of its image and read its anchors
        using var directory = new TempDirectory();
        var model = HelpCardText.SaveExample(directory, "scad", "anchors", "anchors.scad");
        using var slot = CanvasNetViewer.Render(model, new ViewerRenderOptions { Width = (int)width, Height = (int)height });
        var anchors = slot.Anchors
            .Select(a => (a.Name, X: (float)a.PixelX!.Value + x, Y: (float)a.PixelY!.Value + y))
            .ToList();

        // Assert: every tip is within a quarter unit of an anchor, and at least two different anchors are hit
        var hit = tips.Select(t => anchors.FirstOrDefault(a => MathF.Abs(a.X - t.X) <= 0.25f && MathF.Abs(a.Y - t.Y) <= 0.25f).Name).ToList();
        Assert.All(hit, Assert.NotNull);
        Assert.True(hit.Distinct(StringComparer.Ordinal).Count() >= 2, "The callouts hit fewer than two anchors.");
    }

    /// <summary>
    ///     Test that the <c>svg/arrows</c> card is a child of <c>svg</c>, resolves by its aliases, is linked both ways
    ///     with the <c>svg/style</c>, <c>svg/framed-figure</c>, and <c>composition</c> cards, and teaches the marker that
    ///     follows the connector: <c>orient="auto"</c>, <c>auto-start-reverse</c> for a start head, and a tip-anchored
    ///     <c>refX="10"</c>.
    /// </summary>
    [Fact]
    public void HelpCatalog_ArrowsCard_IsLinkedBothWaysAndTeachesMarkers()
    {
        // Arrange: the card and the cards it is linked with
        var card = HelpCardText.Card("svg/arrows");
        string[] linked = ["svg/style", "svg/framed-figure", "composition"];
        var text = HelpCardText.Flatten(card.Markdown);

        // Assert: listed under svg, found by its aliases, and linked both ways
        Assert.Contains(card.Id, HelpCardText.Card("svg").ChildIds);
        Assert.Equal(card.Id, HelpCatalog.Default.Find("svg markers")?.Id);
        Assert.Equal(card.Id, HelpCatalog.Default.Find("svg/arrowheads")?.Id);
        Assert.All(linked, id => Assert.Contains(id, card.SeeAlsoIds));
        Assert.All(linked, id => Assert.Contains(card.Id, HelpCardText.Card(id).SeeAlsoIds));

        // Assert: the advice the card gives
        Assert.Contains("orient=\"auto\"", text, StringComparison.Ordinal);
        Assert.Contains("auto-start-reverse", text, StringComparison.Ordinal);
        Assert.Contains("refX=\"10\" refY=\"5\"", text, StringComparison.Ordinal);
        Assert.Contains("M x0,y0 C xm,y0 xm,y1 x1,y1", text, StringComparison.Ordinal);
        Assert.Contains("M x0,y0 C x0,ym x1,ym x1,y1", text, StringComparison.Ordinal);
        Assert.Contains("svg-arrowhead-shape", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that every connector of the <c>arrows</c> example ends with its last control point in line with its end
    ///     and a tip-anchored <c>orient="auto"</c> marker, and that the rendered head lands on the edge of the target: the
    ///     head color just before the end, beside the stroke, and the white of the target box just past it.
    /// </summary>
    [Fact]
    public void HelpCatalog_ArrowsExample_TipsLandOnTheTargetEdges()
    {
        // Arrange: the example, its marker, and its connectors
        var source = HelpCardText.Example("svg", "arrows");
        var figure = XDocument.Parse(source);
        var marker = Assert.Single(figure.Descendants(), e => e.Name.LocalName == "marker");
        Assert.Equal("10", (string?)marker.Attribute("refX"));
        Assert.Equal("auto", (string?)marker.Attribute("orient"));
        var connectors = figure.Descendants()
            .Where(e => e.Name.LocalName == "path" && (string?)e.Attribute("class") == "connector")
            .Select(e => ((string)e.Attribute("d")!).Split([' ', ',', 'M', 'C'], StringSplitOptions.RemoveEmptyEntries)
                .Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray())
            .ToList();
        Assert.Equal(2, connectors.Count);

        // Act: render the example at twice its size
        using var directory = new TempDirectory();
        File.WriteAllText(directory.Combine("arrows.svg"), source);
        using var image = RenderStrict(directory.Combine("arrows.svg"), width: 1200);
        var picture = image.Surface;

        // Assert: for each connector, the final tangent is level or vertical, the head is just before the end, and
        // the target box is just past it
        foreach (var d in connectors)
        {
            var (controlX, controlY, endX, endY) = (d[4], d[5], d[6], d[7]);
            Assert.True(MathF.Abs(controlX - endX) < 1e-3f || MathF.Abs(controlY - endY) < 1e-3f, "The last control point is not in line with the end.");
            var length = MathF.Sqrt(((endX - controlX) * (endX - controlX)) + ((endY - controlY) * (endY - controlY)));
            var (tx, ty) = ((endX - controlX) / length, (endY - controlY) / length);
            var head = picture[(int)((endX - (6 * tx) - (2 * ty)) * 2), (int)((endY - (6 * ty) + (2 * tx)) * 2)];
            var target = picture[(int)((endX + (3 * tx)) * 2), (int)((endY + (3 * ty)) * 2)];
            Assert.Equal(new Rgba32(0xea, 0x58, 0x0c, 255), head);
            Assert.Equal(White, target);
        }
    }

    /// <summary>
    ///     Test the advice of the <c>svg/framed-figure</c> card for finding a feature: a diagram, a chart, and a model
    ///     rendered alone at the size of the box of an image are the picture an SVG figure embeds in that box, so a
    ///     position found in the one is the position in the other.
    /// </summary>
    /// <param name="root">The root card of the example.</param>
    /// <param name="example">The name of the example.</param>
    /// <param name="file">The file name to save the example as.</param>
    /// <param name="width">The width of the image box.</param>
    /// <param name="height">The height of the image box.</param>
    [Theory]
    [InlineData("mermaid", "flowchart", "flow.mmd", 430, 280)]
    [InlineData("vega-lite", "bar", "bar.vl.json", 400, 300)]
    [InlineData("scad", "anchors", "anchors.scad", 440, 300)]
    public void HelpCatalog_FramedFigureAdvice_PictureAloneAtTheBoxSize_IsThePictureTheFigureEmbeds(string root, string example, string file, int width, int height)
    {
        // Arrange: the picture, and a figure that embeds it in a box of that size at the origin
        using var directory = new TempDirectory();
        HelpCardText.SaveExample(directory, root, example, file);
        File.WriteAllText(
            directory.Combine("figure.svg"),
            $"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'><image href='{file}' x='0' y='0' width='{width}' height='{height}'/></svg>");

        // Act: render the figure, and the picture alone at the box size
        using var figure = RenderStrict(directory.Combine("figure.svg"));
        using var alone = RenderStrict(directory.Combine(file), width, height);

        // Assert: no pixel differs beyond the default tolerance of a comparison
        Assert.Equal(0, figure.Surface.Compare(alone.Surface, ImageComparison.DefaultTolerance, 1, 1).Metrics.DifferingPixels);
    }

    /// <summary>
    ///     Test the advice of the <c>scad/views</c> and <c>svg/fragments</c> cards for the view angles of a fragment:
    ///     <c>def.$vpr=[70,0,45]</c> draws exactly what an azimuth of 45 and an elevation of 20 draw, and works in an SVG
    ///     link without escaping.
    /// </summary>
    [Fact]
    public void HelpCatalog_ScadViewsAdvice_VprDefine_EqualsAzimuthAndElevation()
    {
        // Arrange: the cards, the plate example, and a figure that links the model with the fragment
        Assert.Contains("part.scad#def.$vpr=[70,0,45] is azimuth 45, elevation 20", HelpCardText.TextOf("scad/views"), StringComparison.Ordinal);
        Assert.Contains("def.$vpr=[70,0,45] is azimuth 45, elevation 20", HelpCardText.TextOf("svg/fragments"), StringComparison.Ordinal);
        using var directory = new TempDirectory();
        var plate = HelpCardText.SaveExample(directory, "scad", "plate", "plate.scad");
        File.WriteAllText(
            directory.Combine("figure.svg"),
            "<svg xmlns='http://www.w3.org/2000/svg' width='300' height='300'><image href='plate.scad#def.$vpr=[70,0,45]' x='0' y='0' width='300' height='300'/></svg>");
        var vpr = new ScadViewOptions { Defines = new Dictionary<string, object?> { ["$vpr"] = new List<object?> { 70.0, 0.0, 45.0 } } };
        var angles = new ScadViewOptions { Azimuth = 45, Elevation = 20 };

        // Act: the define, the angles, and the link
        using var byDefine = CanvasNetViewer.Render(plate, new ViewerRenderOptions { Scad = vpr, Width = 300, Height = 300, ThrowOnUnsupportedFeature = true });
        using var byAngles = CanvasNetViewer.Render(plate, new ViewerRenderOptions { Scad = angles, Width = 300, Height = 300 });
        using var link = RenderStrict(directory.Combine("figure.svg"));

        // Assert: the define draws the same picture as the angles, and the link draws without a diagnostic
        Assert.True(SamePixels(byDefine.Surface, byAngles.Surface), "def.$vpr=[70,0,45] does not draw azimuth 45, elevation 20.");
        Assert.Empty(link.Diagnostics);
        Assert.Equal("plate.scad#def.$vpr=[70,0,45]", Assert.Single(link.Embedded).Href);
    }

    /// <summary>
    ///     Test the advice of the <c>mermaid/gantt</c> card for the grid: the theme variable <c>gridColor</c> changes no
    ///     pixel, the card's <c>themeCSS</c> rule for <c>.grid .tick</c> recolors the grid lines, and its rule for
    ///     <c>.grid .tick text</c> resizes the tick labels, all without a diagnostic.
    /// </summary>
    [Fact]
    public void HelpCatalog_MermaidGanttAdvice_ThemeCssStylesTheGridNotGridColor()
    {
        // Arrange: the card's two CSS rules, and the gantt example with each change
        var bullets = HelpCardText.Bullets("mermaid/gantt", "Syntax");
        var axis = Assert.Single(bullets, b => b.StartsWith("Axis: ", StringComparison.Ordinal));
        var colors = Assert.Single(bullets, b => b.StartsWith("Colors: ", StringComparison.Ordinal));
        Assert.Contains("`gridColor` is accepted and, as in Mermaid 11, colors no grid line", colors, StringComparison.Ordinal);
        Assert.Contains("black at 0.8 opacity and their labels 10 px", axis, StringComparison.Ordinal);
        const string lineRule = ".grid .tick { color: #999999; }";
        const string labelRule = ".grid .tick text { font-size: 14px; }";
        Assert.Contains($"`{lineRule}`", axis, StringComparison.Ordinal);
        Assert.Contains($"`{labelRule}`", axis, StringComparison.Ordinal);
        var source = HelpCardText.Example("mermaid", "gantt");
        using var directory = new TempDirectory();

        // Act: the plain chart, gridColor red, and each themeCSS rule
        using var plain = Render("plain", source);
        using var gridColor = Render("grid-color", "%%{init: {\"themeVariables\": {\"gridColor\": \"#ff0000\"}}}%%\n" + source);
        using var lines = Render("lines", $"%%{{init: {{\"themeCSS\": \"{lineRule}\"}}}}%%\n" + source);
        using var labels = Render("labels", $"%%{{init: {{\"themeCSS\": \"{labelRule}\"}}}}%%\n" + source);

        // Assert: gridColor changes nothing; the label rule changes the picture
        Assert.True(SamePixels(plain.Surface, gridColor.Surface), "gridColor changed the picture.");
        Assert.False(SamePixels(plain.Surface, labels.Surface), "The font-size rule did not change the tick labels.");

        // Assert: a grid line (the tick of Jun 5) is dark gray, and lighter gray with the color rule
        var plainLine = plain.Surface[120, 160];
        var styledLine = lines.Surface[120, 160];
        Assert.True(plainLine.R == plainLine.G && plainLine.G == plainLine.B && plainLine.R < 128, $"No black grid line at 120,160: {plainLine}.");
        Assert.True(styledLine.R == styledLine.G && styledLine.G == styledLine.B, $"The styled grid line is not gray: {styledLine}.");
        Assert.InRange(styledLine.R, plainLine.R + 40, 250);

        // Renders a chart in strict mode over white, without a diagnostic
        ViewerImage Render(string name, string chart)
        {
            var input = directory.Combine($"{name}.mmd");
            File.WriteAllText(input, chart);
            var image = RenderStrict(input);
            Assert.Empty(image.Diagnostics);
            return image;
        }
    }

    /// <summary>
    ///     Renders a file in strict mode over white, as a host that writes an image usually does.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="width">The width, or <see langword="null"/> for the natural width.</param>
    /// <param name="height">The height, or <see langword="null"/> for the natural height.</param>
    /// <returns>The picture; the caller disposes it.</returns>
    private static ViewerImage RenderStrict(string path, int? width = null, int? height = null)
    {
        return CanvasNetViewer.Render(path, new ViewerRenderOptions { ThrowOnUnsupportedFeature = true, Background = White, Width = width, Height = height });
    }

    /// <summary>
    ///     Gets whether two pictures have the same size and the same pixels, alpha included.
    /// </summary>
    /// <param name="a">The first picture.</param>
    /// <param name="b">The second picture.</param>
    /// <returns><see langword="true"/> when every pixel is equal.</returns>
    private static bool SamePixels(Surface a, Surface b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            return false;
        }

        for (var y = 0; y < a.Height; y++)
        {
            for (var x = 0; x < a.Width; x++)
            {
                if (a[x, y] != b[x, y])
                {
                    return false;
                }
            }
        }

        return true;
    }
}
