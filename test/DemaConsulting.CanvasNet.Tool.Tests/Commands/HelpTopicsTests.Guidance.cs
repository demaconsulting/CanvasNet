using System.Text.Json;
using System.Text.Json.Nodes;
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
}
