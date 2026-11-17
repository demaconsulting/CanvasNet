using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the style and composition guidance of the help cards: the cards exist and link to each other,
///     and the advice they give holds for the real renderer.
/// </summary>
public partial class HelpTopicsTests
{
    /// <summary>
    ///     The drawing formats, which each have a style card and a composition card.
    /// </summary>
    private static readonly string[] DrawingFormats = ["scad", "vega-lite", "mermaid", "svg"];

    /// <summary>
    ///     Test that every drawing format has a style card and a composition card, that each links to the hub of its
    ///     kind, and that its root card lists both.
    /// </summary>
    [Fact]
    public void HelpTopics_DrawingFormats_HaveStyleAndCompositionCards()
    {
        // Act / Assert: each format has both cards, below its root, linked to the hubs
        foreach (var format in DrawingFormats)
        {
            var root = HelpTopics.Get(format);
            foreach (var kind in new[] { "style", "composition" })
            {
                var card = HelpTopics.Get($"{format}/{kind}");
                Assert.Contains(card.Id, root.ChildIds);
                Assert.Contains(kind, card.SeeAlso);
                Assert.Contains(kind, root.SeeAlso);
            }

            Assert.Contains($"{format}/composition", HelpTopics.Get($"{format}/style").SeeAlso);
            Assert.Contains($"{format}/style", HelpTopics.Get($"{format}/composition").SeeAlso);
        }
    }

    /// <summary>
    ///     Test that the <c>style</c> and <c>composition</c> hubs link to the card of every drawing format and to each
    ///     other, that the format cards link back, and that a reader of a diagram card reaches the hubs in a few steps.
    /// </summary>
    [Fact]
    public void HelpTopics_StyleAndCompositionHubs_LinkBothWays()
    {
        // Arrange: the hubs
        var style = HelpTopics.Get("style");
        var composition = HelpTopics.Get("composition");

        // Assert: the hubs link to every format card and to each other
        Assert.All(DrawingFormats, format => Assert.Contains($"{format}/style", style.SeeAlso));
        Assert.All(DrawingFormats, format => Assert.Contains($"{format}/composition", composition.SeeAlso));
        Assert.Contains("composition", style.SeeAlso);
        Assert.Contains("style", composition.SeeAlso);
        Assert.Contains("canvasnet help style", HelpCardText.Flatten(CommandRegistry.CreateDefault().RenderHelp()));

        // Assert: from 'mermaid gantt' the See also links lead to the style card, then to the composition card and the hubs
        var gantt = HelpTopics.Get("mermaid/gantt");
        Assert.Contains("mermaid/style", gantt.SeeAlso);
        Assert.Contains("mermaid/composition", HelpTopics.Get("mermaid/style").SeeAlso);
        Assert.Contains("style", HelpTopics.Get("mermaid/style").SeeAlso);
        Assert.Contains("composition", HelpTopics.Get("mermaid/composition").SeeAlso);
    }

    /// <summary>
    ///     Test that the <c>mermaid style</c> card lists exactly the five themes and that each one draws the flowchart
    ///     example under <c>--strict</c> without a diagnostic, and that the styled example draws under every theme too
    ///     (its own theme wins).
    /// </summary>
    /// <param name="theme">The theme.</param>
    [Theory]
    [InlineData("default")]
    [InlineData("base")]
    [InlineData("dark")]
    [InlineData("forest")]
    [InlineData("neutral")]
    public void HelpTopics_MermaidStyleCard_EveryListedThemeRenders(string theme)
    {
        // Arrange: the card's list of themes, and the two examples
        var themes = HelpCardText.Bullets("mermaid/style", "Themes")[0];
        Assert.StartsWith("The themes are `default`, `base`, `dark`, `forest`, and `neutral`.", themes);
        using var directory = new TempDirectory();
        var flowchart = directory.Combine("flowchart.mmd");
        var styled = directory.Combine("styled-flow.mmd");
        File.WriteAllText(flowchart, HelpTopics.RenderExample(HelpTopics.Get("mermaid"), "flowchart"));
        File.WriteAllText(styled, HelpTopics.RenderExample(HelpTopics.Get("mermaid"), "styled"));

        // Act / Assert: each draws without a diagnostic
        foreach (var input in new[] { flowchart, styled })
        {
            var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "--theme", theme, "--strict", "--json", "-o", directory.Combine("out.png"));
            Assert.True(exitCode == 0, stdErr + stdOut);
            using var document = JsonDocument.Parse(stdOut);
            Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        }
    }

    /// <summary>
    ///     Test that every key of the <c>config</c> of the styled Vega-Lite example, each of which the <c>vega-lite
    ///     style</c> card recommends, visibly changes the picture: the chart drawn without that key differs from the
    ///     chart drawn with it.
    /// </summary>
    [Fact]
    public void HelpTopics_VegaLiteStyledExample_ConfigChangesThePicture()
    {
        // Arrange: the example, its config keys, and the card text
        var source = HelpTopics.RenderExample(HelpTopics.Get("vega-lite"), "styled");
        var config = JsonNode.Parse(source)!["config"]!.AsObject();
        var keys = config.Select(p => p.Key).ToList();
        var card = HelpCardText.TextOf("vega-lite/style");
        using var directory = new TempDirectory();
        var full = Render(directory, "full", source);

        // Act / Assert: the card names each key, and leaving each out changes the picture
        Assert.True(keys.Count >= 6, $"The example sets only {keys.Count} config keys.");
        foreach (var key in keys)
        {
            Assert.Contains($"`{key}", card, StringComparison.Ordinal);
            var without = JsonNode.Parse(source)!;
            without["config"]!.AsObject().Remove(key);
            var changed = Render(directory, key, without.ToJsonString());
            Assert.False(full.AsSpan().SequenceEqual(changed), $"Leaving out config.{key} does not change the picture.");
        }

        // Renders a chart and returns the bytes of the picture
        static byte[] Render(TempDirectory directory, string name, string chart)
        {
            var input = directory.Combine($"{name}.vl.json");
            var output = directory.Combine($"{name}.png");
            File.WriteAllText(input, chart);
            var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "--strict", "--json", "-o", output);
            Assert.True(exitCode == 0, stdErr + stdOut);
            return File.ReadAllBytes(output);
        }
    }

    /// <summary>
    ///     Test that a chart that sets <c>"background": "transparent"</c>, as the style cards advise, stays transparent
    ///     inside an SVG figure, while a chart without it keeps its white background there.
    /// </summary>
    [Fact]
    public void HelpTopics_StyleAdvice_TransparentChartInsideSvg_ShowsThePage()
    {
        // Arrange: a yellow page that places the styled chart, once as it is and once transparent
        var source = HelpTopics.RenderExample(HelpTopics.Get("vega-lite"), "styled");
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
        string PixelAt(string name)
        {
            var (exitCode, stdOut, stdErr) = ToolHost.Invoke("inspect", "pixel", directory.Combine($"{name}.svg"), "300,8", "--json");
            Assert.True(exitCode == 0, stdErr + stdOut);
            using var document = JsonDocument.Parse(stdOut);
            return document.RootElement.GetProperty("pixel").GetProperty("hex").GetString()!;
        }

        // Assert: the transparent chart shows the page, the other its own white
        Assert.StartsWith("#FFFF00", PixelAt("clear"), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("#FFFFFF", PixelAt("white"), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Test that the <c>svg framed-figure</c> card is a child of <c>svg</c> and is linked both ways with the
    ///     <c>composition</c> hub, the <c>svg composition</c> and <c>svg style</c> cards, the <c>scad anchors</c>
    ///     and <c>scad composition</c> cards, and the <c>positions</c> topic.
    /// </summary>
    [Fact]
    public void HelpTopics_FramedFigureCard_IsLinkedFromTheCompositionAndAnchorCards()
    {
        // Arrange: the card and the cards that teach composition, style, and anchors
        var card = HelpTopics.Get("svg/framed-figure");
        string[] linked = ["composition", "svg/composition", "svg/style", "scad/anchors", "positions"];

        // Assert: it is listed under svg, links to each, and each links back
        Assert.Contains(card.Id, HelpTopics.Get("svg").ChildIds);
        Assert.All(linked, id => Assert.Contains(id, card.SeeAlso));
        Assert.All(linked.Append("scad/composition"), id => Assert.Contains(card.Id, HelpTopics.Get(id).SeeAlso));
    }

    /// <summary>
    ///     Test that every callout arrow of the <c>framed</c> example of the <c>svg framed-figure</c> card ends on an
    ///     anchor of the model it embeds: the pixel of the anchor when the model is rendered alone at the size of its
    ///     image, plus the image x and y; that at least two different anchors are hit; and that the arrows use a marker.
    /// </summary>
    [Fact]
    public void HelpTopics_FramedFigureExample_CalloutsEndOnTheModelAnchors()
    {
        // Arrange: the figure, the model image, and the callout lines
        var figure = XDocument.Parse(HelpTopics.RenderExample(HelpTopics.Get("svg"), "framed"));
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
        var model = directory.Combine("anchors.scad");
        File.WriteAllText(model, HelpTopics.RenderExample(HelpTopics.Get("scad"), "anchors"));
        var size = string.Create(CultureInfo.InvariantCulture, $"{width}x{height}");
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", model, "--size", size, "--json", "-o", directory.Combine("slot.png"));
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var anchors = document.RootElement.GetProperty("anchors").EnumerateArray()
            .Select(a => (
                Name: a.GetProperty("name").GetString()!,
                X: a.GetProperty("pixel")[0].GetSingle() + x,
                Y: a.GetProperty("pixel")[1].GetSingle() + y))
            .ToList();

        // Assert: every tip is within a quarter unit of an anchor, and at least two different anchors are hit
        var hit = tips.Select(t => anchors.FirstOrDefault(a => MathF.Abs(a.X - t.X) <= 0.25f && MathF.Abs(a.Y - t.Y) <= 0.25f).Name).ToList();
        Assert.All(hit, Assert.NotNull);
        Assert.True(hit.Distinct(StringComparer.Ordinal).Count() >= 2, "The callouts hit fewer than two anchors.");
    }

    /// <summary>
    ///     Test that the <c>svg arrows</c> card is a child of <c>svg</c>, resolves by its aliases, is linked both ways
    ///     with the <c>svg style</c>, <c>svg framed-figure</c>, and <c>composition</c> cards, and teaches the marker that
    ///     follows the connector: <c>orient="auto"</c>, <c>auto-start-reverse</c> for a start head, and a tip-anchored
    ///     <c>refX="10"</c>.
    /// </summary>
    [Fact]
    public void HelpTopics_ArrowsCard_IsLinkedBothWaysAndTeachesMarkers()
    {
        // Arrange: the card and the cards it is linked with
        var card = HelpTopics.Get("svg/arrows");
        string[] linked = ["svg/style", "svg/framed-figure", "composition"];
        var text = HelpCardText.Flatten(card.Body);

        // Assert: listed under svg, found by its aliases, and linked both ways
        Assert.Contains(card.Id, HelpTopics.Get("svg").ChildIds);
        Assert.Equal(card.Id, HelpTopics.Resolve(["svg", "markers"])?.Id);
        Assert.Equal(card.Id, HelpTopics.Resolve(["svg", "arrowheads"])?.Id);
        Assert.All(linked, id => Assert.Contains(id, card.SeeAlso));
        Assert.All(linked, id => Assert.Contains(card.Id, HelpTopics.Get(id).SeeAlso));

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
    public void HelpTopics_ArrowsExample_TipsLandOnTheTargetEdges()
    {
        // Arrange: the example, its marker, and its connectors
        var source = HelpTopics.RenderExample(HelpTopics.Get("svg"), "arrows");
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
        var output = directory.Combine("arrows.png");
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", directory.Combine("arrows.svg"), "--width", "1200", "--strict", "-o", output);
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var picture = PngCodec.Load(output);

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
            Assert.Equal(new Rgba32(255, 255, 255, 255), target);
        }
    }
    /// <summary>
    ///     Test the advice of the <c>svg framed-figure</c> card for finding a feature: a diagram, a chart, and a model
    ///     rendered alone with <c>--size</c> equal to the box of an image are the picture an SVG figure embeds in that
    ///     box, so a position found in the one is the position in the other.
    /// </summary>
    /// <param name="root">The root card of the example.</param>
    /// <param name="example">The name of the example.</param>
    /// <param name="file">The file name to save the example as.</param>
    /// <param name="box">The size of the image box, WxH.</param>
    [Theory]
    [InlineData("mermaid", "flowchart", "flow.mmd", "430x280")]
    [InlineData("vega-lite", "bar", "bar.vl.json", "400x300")]
    [InlineData("scad", "anchors", "anchors.scad", "440x300")]
    public void HelpTopics_FramedFigureAdvice_PictureAloneAtTheBoxSize_IsThePictureTheFigureEmbeds(string root, string example, string file, string box)
    {
        // Arrange: the picture, and a figure that embeds it in a box of that size at the origin
        using var directory = new TempDirectory();
        File.WriteAllText(directory.Combine(file), HelpTopics.RenderExample(HelpTopics.Get(root), example));
        var sides = box.Split('x');
        File.WriteAllText(
            directory.Combine("figure.svg"),
            $"<svg xmlns='http://www.w3.org/2000/svg' width='{sides[0]}' height='{sides[1]}'><image href='{file}' x='0' y='0' width='{sides[0]}' height='{sides[1]}'/></svg>");

        // Act: render the figure, render the picture alone at the box size, and compare
        var figure = ToolHost.Invoke("render", directory.Combine("figure.svg"), "--strict", "-o", directory.Combine("figure.png"));
        var alone = ToolHost.Invoke("render", directory.Combine(file), "--size", box, "--strict", "-o", directory.Combine("alone.png"));
        Assert.True(figure.ExitCode == 0, figure.StdErr + figure.StdOut);
        Assert.True(alone.ExitCode == 0, alone.StdErr + alone.StdOut);
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("compare", directory.Combine("figure.png"), directory.Combine("alone.png"), "--json");

        // Assert: no pixel differs beyond the default tolerance
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        Assert.Equal(0, document.RootElement.GetProperty("metrics").GetProperty("diffPixels").GetInt64());
    }

    /// <summary>
    ///     Test the advice of the <c>svg framed-figure</c> card for a diagram or chart: the commands of its fenced block
    ///     draw the picture at the box size with a labeled grid, and <c>inspect pixel</c> on the source at that size reads
    ///     the same pixel as the picture rendered alone, on the node the card names.
    /// </summary>
    [Fact]
    public void HelpTopics_FramedFigureAdvice_GridAndInspectPixel_ReadThePictureAtTheBoxSize()
    {
        // Arrange: the two command lines of the card, and the flowchart example
        var card = HelpTopics.Get("svg/framed-figure");
        var lines = card.Body.Split('\n').Where(l => l.StartsWith("canvasnet ", StringComparison.Ordinal)).ToList();
        var gridLine = Assert.Single(lines, l => l.Contains("--grid", StringComparison.Ordinal));
        var pixelLine = Assert.Single(lines, l => l.StartsWith("canvasnet inspect pixel ", StringComparison.Ordinal));
        Assert.Contains("canvasnet render --help", HelpCardText.Flatten(card.Body));
        using var directory = new TempDirectory();
        File.WriteAllText(directory.Combine("flowchart.mmd"), HelpTopics.RenderExample(HelpTopics.Get("mermaid"), "flowchart"));

        // Arguments of a card line, with the file names in the temporary directory
        string[] ArgumentsOf(string line) => line.Split(' ')[1..]
            .Select(a => a.EndsWith(".mmd", StringComparison.Ordinal) || a.EndsWith(".png", StringComparison.Ordinal) ? directory.Combine(a) : a)
            .ToArray();
        var gridArguments = ArgumentsOf(gridLine);
        var pixelArguments = ArgumentsOf(pixelLine);
        var size = gridArguments[Array.IndexOf(gridArguments, "--size") + 1];
        var point = pixelArguments[3];

        // Act: run the card's commands, render the picture alone, and read the same point of the PNG
        var grid = ToolHost.Invoke(gridArguments);
        var alone = ToolHost.Invoke("render", directory.Combine("flowchart.mmd"), "--size", size, "--strict", "-o", directory.Combine("alone.png"));
        var fromSource = ToolHost.Invoke([.. pixelArguments, "--json"]);
        var fromPicture = ToolHost.Invoke("inspect", "pixel", directory.Combine("alone.png"), point, "--json");
        var compare = ToolHost.Invoke("compare", directory.Combine("grid.png"), directory.Combine("alone.png"), "--json");

        // Assert: the grid changes the picture, and the source and the picture agree on the point, a node of the diagram
        Assert.True(grid.ExitCode == 0, grid.StdErr + grid.StdOut);
        Assert.True(alone.ExitCode == 0, alone.StdErr + alone.StdOut);
        Assert.True(fromSource.ExitCode == 0, fromSource.StdErr + fromSource.StdOut);
        Assert.True(fromPicture.ExitCode == 0, fromPicture.StdErr + fromPicture.StdOut);
        using var compareDocument = JsonDocument.Parse(compare.StdOut);
        Assert.False(compareDocument.RootElement.GetProperty("metrics").GetProperty("identical").GetBoolean());
        using var sourceDocument = JsonDocument.Parse(fromSource.StdOut);
        using var pictureDocument = JsonDocument.Parse(fromPicture.StdOut);
        var hex = sourceDocument.RootElement.GetProperty("pixel").GetProperty("hex").GetString();
        Assert.Equal(pictureDocument.RootElement.GetProperty("pixel").GetProperty("hex").GetString(), hex);
        Assert.Equal("#CCFFCC", hex);
    }

    /// <summary>
    ///     Test the advice of the <c>scad views</c> and <c>svg fragments</c> cards for the view angles of a fragment:
    ///     <c>def.$vpr=[70,0,45]</c> draws exactly what <c>--azimuth 45 --elevation 20</c> draws, reports those angles,
    ///     and works in an SVG link without escaping.
    /// </summary>
    [Fact]
    public void HelpTopics_ScadViewsAdvice_VprFragment_EqualsAzimuthAndElevation()
    {
        // Arrange: the cards, the plate example, and a figure that links the model with the fragment
        Assert.Contains("'part.scad#def.$vpr=[70,0,45]' is --azimuth 45 --elevation 20", HelpCardText.TextOf("scad/views"), StringComparison.Ordinal);
        Assert.Contains("def.$vpr=[70,0,45] is azimuth 45, elevation 20", HelpCardText.TextOf("svg/fragments"), StringComparison.Ordinal);
        using var directory = new TempDirectory();
        File.WriteAllText(directory.Combine("plate.scad"), HelpTopics.RenderExample(HelpTopics.Get("scad"), "plate"));
        File.WriteAllText(
            directory.Combine("figure.svg"),
            "<svg xmlns='http://www.w3.org/2000/svg' width='300' height='300'><image href='plate.scad#def.$vpr=[70,0,45]' x='0' y='0' width='300' height='300'/></svg>");

        // Act: the fragment, the options, and the link
        var fragment = ToolHost.Invoke("render", directory.Combine("plate.scad") + "#def.$vpr=[70,0,45]", "--size", "300x300", "--strict", "--json", "-o", directory.Combine("vpr.png"));
        var options = ToolHost.Invoke("render", directory.Combine("plate.scad"), "--azimuth", "45", "--elevation", "20", "--size", "300x300", "-o", directory.Combine("angles.png"));
        var link = ToolHost.Invoke("render", directory.Combine("figure.svg"), "--strict", "--json", "-o", directory.Combine("figure.png"));
        var compare = ToolHost.Invoke("compare", directory.Combine("vpr.png"), directory.Combine("angles.png"), "--json");

        // Assert: the fragment reports the angles and draws the same picture; the link draws without a diagnostic
        Assert.True(fragment.ExitCode == 0, fragment.StdErr + fragment.StdOut);
        Assert.True(options.ExitCode == 0, options.StdErr + options.StdOut);
        Assert.True(link.ExitCode == 0, link.StdErr + link.StdOut);
        using var fragmentDocument = JsonDocument.Parse(fragment.StdOut);
        var operation = fragmentDocument.RootElement.GetProperty("operations")[0];
        Assert.Equal(45, operation.GetProperty("azimuth").GetDouble(), 6);
        Assert.Equal(20, operation.GetProperty("elevation").GetDouble(), 6);
        using var compareDocument = JsonDocument.Parse(compare.StdOut);
        Assert.True(compareDocument.RootElement.GetProperty("metrics").GetProperty("identical").GetBoolean());
        using var linkDocument = JsonDocument.Parse(link.StdOut);
        Assert.Empty(linkDocument.RootElement.GetProperty("diagnostics").EnumerateArray());
    }

    /// <summary>
    ///     Test the advice of the <c>mermaid gantt</c> card for the grid: the theme variable <c>gridColor</c> changes no
    ///     pixel, the card's <c>themeCSS</c> rule for <c>.grid .tick</c> recolors the grid lines, and its rule for
    ///     <c>.grid .tick text</c> resizes the tick labels, both without a diagnostic.
    /// </summary>
    [Fact]
    public void HelpTopics_MermaidGanttAdvice_ThemeCssStylesTheGridNotGridColor()
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
        var source = HelpTopics.RenderExample(HelpTopics.Get("mermaid"), "gantt");
        using var directory = new TempDirectory();

        // Act: the plain chart, gridColor red, and each themeCSS rule
        var plain = Render("plain", source);
        var gridColor = Render("grid-color", "%%{init: {\"themeVariables\": {\"gridColor\": \"#ff0000\"}}}%%\n" + source);
        var lines = Render("lines", $"%%{{init: {{\"themeCSS\": \"{lineRule}\"}}}}%%\n" + source);
        var labels = Render("labels", $"%%{{init: {{\"themeCSS\": \"{labelRule}\"}}}}%%\n" + source);

        // Assert: gridColor changes nothing; the label rule changes the picture
        Assert.True(Identical(plain, gridColor), "gridColor changed the picture.");
        Assert.False(Identical(plain, labels), "The font-size rule did not change the tick labels.");

        // Assert: a grid line (the tick of Jun 5) is dark gray, and lighter gray with the color rule
        var plainLine = PixelAt(plain, "120,160");
        var styledLine = PixelAt(lines, "120,160");
        Assert.True(plainLine.R == plainLine.G && plainLine.G == plainLine.B && plainLine.R < 128, $"No black grid line at 120,160: {plainLine}.");
        Assert.True(styledLine.R == styledLine.G && styledLine.G == styledLine.B, $"The styled grid line is not gray: {styledLine}.");
        Assert.InRange(styledLine.R, plainLine.R + 40, 250);

        // Renders a chart under --strict without a diagnostic and returns the picture
        string Render(string name, string chart)
        {
            var input = directory.Combine($"{name}.mmd");
            var output = directory.Combine($"{name}.png");
            File.WriteAllText(input, chart);
            var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "--strict", "--json", "-o", output);
            Assert.True(exitCode == 0, stdErr + stdOut);
            using var document = JsonDocument.Parse(stdOut);
            Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
            return output;
        }

        // Whether two pictures are identical
        static bool Identical(string a, string b)
        {
            var (exitCode, stdOut, stdErr) = ToolHost.Invoke("compare", a, b, "--json");
            Assert.True(exitCode == 0, stdErr + stdOut);
            using var document = JsonDocument.Parse(stdOut);
            return document.RootElement.GetProperty("metrics").GetProperty("identical").GetBoolean();
        }

        // The color of one pixel of a picture
        static (int R, int G, int B) PixelAt(string picture, string point)
        {
            var (exitCode, stdOut, stdErr) = ToolHost.Invoke("inspect", "pixel", picture, point, "--json");
            Assert.True(exitCode == 0, stdErr + stdOut);
            using var document = JsonDocument.Parse(stdOut);
            var pixel = document.RootElement.GetProperty("pixel");
            return (pixel.GetProperty("r").GetInt32(), pixel.GetProperty("g").GetInt32(), pixel.GetProperty("b").GetInt32());
        }
    }
}
