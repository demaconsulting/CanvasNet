using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Tests that the claims of the <c>positions</c> help cards and of the framed-figure recipe hold when the tool runs
///     them: the selector examples, the quoting of each shell, the kinds of each diagram type, the view paths, the limits,
///     the links, and the callouts of the framed flowchart example.
/// </summary>
public partial class HelpTopicsTests
{
    /// <summary>The selector the quoting section of the selectors card quotes for every shell.</summary>
    private const string OddSelector = "node:\"my*odd id\"";

    /// <summary>
    ///     Saves a source example of a root card into a directory.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="root">The root card.</param>
    /// <param name="example">The example name.</param>
    /// <param name="file">The file name.</param>
    /// <returns>The path.</returns>
    private static string SaveExample(TempDirectory directory, string root, string example, string file)
    {
        var path = directory.Combine(file);
        File.WriteAllText(path, HelpTopics.RenderExample(HelpTopics.Get(root), example));
        return path;
    }

    /// <summary>
    ///     Renders an input with selectors and returns the parsed result.
    /// </summary>
    /// <param name="directory">The directory of the output.</param>
    /// <param name="input">The input.</param>
    /// <param name="selectors">The selectors, one per <c>--positions</c>.</param>
    /// <returns>The JSON document; the caller disposes it.</returns>
    private static JsonDocument RenderPositions(TempDirectory directory, string input, params string[] selectors)
    {
        var arguments = new List<string> { "render", input };
        foreach (var selector in selectors)
        {
            arguments.AddRange(["--positions", selector]);
        }

        arguments.AddRange(["-o", directory.Combine("out.png"), "--json"]);
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke([.. arguments]);
        Assert.True(exitCode == 0, stdErr + stdOut);
        return JsonDocument.Parse(stdOut);
    }

    /// <summary>
    ///     Gets the ids of the positions of a result.
    /// </summary>
    /// <param name="document">The result.</param>
    /// <returns>The ids, in order.</returns>
    private static List<string> IdsOf(JsonDocument document) =>
        [.. document.RootElement.GetProperty("positions").EnumerateArray().Select(p => p.GetProperty("id").GetString()!)];

    /// <summary>
    ///     Test that <c>positions</c> is a topic after <c>composition</c> with its four cards, and that it is linked both
    ///     ways with the framed-figure, anchors, and composition cards.
    /// </summary>
    [Fact]
    public void HelpTopics_PositionsTopic_IsInTheTreeAndLinked()
    {
        // Arrange
        var root = HelpTopics.Get("positions");

        // Assert: the topic, its cards, and the links
        Assert.Equal(HelpTopics.TopicNames.ToList().IndexOf("composition") + 1, HelpTopics.TopicNames.ToList().IndexOf("positions"));
        Assert.Equal(["selectors", "mermaid", "vega-lite", "geometry"], root.ChildIds.Select(id => id[(id.IndexOf('/') + 1)..]));
        Assert.Contains("svg/framed-figure", root.SeeAlso);
        Assert.Contains("scad/anchors", root.SeeAlso);
        Assert.Contains("positions", HelpTopics.Get("svg/framed-figure").SeeAlso);
        Assert.Contains("positions", HelpTopics.Get("scad/anchors").SeeAlso);
        Assert.Contains("positions", HelpTopics.Get("composition").SeeAlso);
        Assert.Contains("positions/mermaid", HelpTopics.Get("mermaid/composition").SeeAlso);
        Assert.Contains("positions/vega-lite", HelpTopics.Get("vega-lite/composition").SeeAlso);
    }

    /// <summary>
    ///     Test that the limits the cards state are the limits of the code.
    /// </summary>
    [Fact]
    public void HelpTopics_PositionsLimits_AreTheLimitsOfTheCode()
    {
        // Arrange
        var limits = HelpCardText.Flatten(HelpCardText.TextOf("limits"));
        var selectors = HelpCardText.Flatten(HelpCardText.TextOf("positions/selectors"));
        string Of(int value) => value.ToString(CultureInfo.InvariantCulture);

        // Assert
        Assert.Contains($"at most {Of(PositionCollector.MaxPositions)} items", limits, StringComparison.Ordinal);
        Assert.Contains($"At most {Of(PositionCollector.MaxPositions)} items", HelpCardText.Flatten(HelpCardText.TextOf("positions")), StringComparison.Ordinal);
        Assert.Contains($"At most {Of(PositionSelection.MaxSelectors)} selectors", selectors, StringComparison.Ordinal);
        Assert.Contains($"{Of(PositionSelector.MaxSelectorLength)} characters", selectors, StringComparison.Ordinal);
        Assert.Contains($"{Of(PositionSelector.MaxPredicates)} predicates", selectors, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test the selector examples of the cards by running them on the help examples: what each matches.
    /// </summary>
    [Fact]
    public void HelpTopics_PositionsSelectors_Examples_MatchAsTheCardsSay()
    {
        // Arrange: the flowchart, the git graph, and the bar chart examples
        using var directory = new TempDirectory();
        var flow = SaveExample(directory, "mermaid", "flowchart", "flowchart.mmd");
        var git = SaveExample(directory, "mermaid", "gitgraph", "gitgraph.mmd");
        var bar = SaveExample(directory, "vega-lite", "bar", "bar.vl.json");
        var text = HelpCardText.TextOf("positions/selectors") + HelpCardText.TextOf("positions");
        string[] cited = ["node:send", "edge:@from=pack", "mark:x=B", "'commit:*fix*'", "node:se?d", "node:s*", "mark:x=3", "mark:x=\"3\""];
        Assert.All(cited, selector => Assert.Contains(selector, text, StringComparison.Ordinal));

        // Act
        using var send = RenderPositions(directory, flow, "node:send");
        using var glob = RenderPositions(directory, flow, "node:se?d", "node:s*");
        using var edges = RenderPositions(directory, flow, "edge:@from=pack");
        using var fix = RenderPositions(directory, git, "commit:*fix*");
        using var number = RenderPositions(directory, bar, "mark:y=55");
        using var textValue = RenderPositions(directory, bar, "mark:y=\"55\"");
        using var bars = RenderPositions(directory, bar, "mark:x=B", "axis");

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
    public void HelpTopics_PositionsSelectors_MetadataExamples_SelectOneItem()
    {
        // Arrange: two parallel edges, and two charts side by side
        using var directory = new TempDirectory();
        var flow = directory.Combine("pair.mmd");
        File.WriteAllText(flow, "flowchart LR\n    A --> B\n    A --> B\n");
        var chart = directory.Combine("pair.vl.json");
        const string Unit = "{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}";
        File.WriteAllText(chart, "{\"data\":{\"values\":[{\"a\":\"A\",\"b\":3}]},\"hconcat\":[" + Unit + "," + Unit + "]}");
        var text = HelpCardText.Flatten(HelpCardText.TextOf("positions/selectors"));
        Assert.Contains("edge:@from=A+@to=B+@index=0", text, StringComparison.Ordinal);
        Assert.Contains("axis:@id=x+@view=\"hconcat[1]\"", text, StringComparison.Ordinal);

        // Act
        using var edge = RenderPositions(directory, flow, "edge:@from=A+@to=B+@index=0");
        using var axis = RenderPositions(directory, chart, "axis:@id=x+@view=\"hconcat[1]\"");

        // Assert
        var item = Assert.Single(edge.RootElement.GetProperty("positions").EnumerateArray());
        Assert.Equal(0, item.GetProperty("index").GetInt32());
        var axisItem = Assert.Single(axis.RootElement.GetProperty("positions").EnumerateArray());
        Assert.Equal("hconcat[1]", axisItem.GetProperty("view").GetString());
    }

    /// <summary>
    ///     Test the quoting section of the selectors card: the quoted selector of each shell, split as that shell splits
    ///     a command line, reaches the tool as the selector itself (the warning that it matched nothing names it exactly).
    /// </summary>
    [Fact]
    public void HelpTopics_PositionsSelectors_ShellQuoting_ReachesTheToolIntact()
    {
        // Arrange: the quoted form each shell's line of the card shows
        var items = HelpCardText.Bullets("positions/selectors", "Quoting");
        var bash = Assert.Single(items, i => i.StartsWith("bash", StringComparison.Ordinal));
        var powerShell = Assert.Single(items, i => i.StartsWith("PowerShell 7.3", StringComparison.Ordinal));
        var cmd = Assert.Single(items, i => i.StartsWith("cmd", StringComparison.Ordinal));
        Assert.Contains("'node:\"my*odd id\"'", bash, StringComparison.Ordinal);
        Assert.Contains("as in bash", powerShell, StringComparison.Ordinal);
        Assert.Contains("\"node:\\\"my*odd id\\\"\"", cmd, StringComparison.Ordinal);
        using var directory = new TempDirectory();
        var flow = SaveExample(directory, "mermaid", "flowchart", "flowchart.mmd");

        // Act: split each quoted form as its shell does
        string[] arguments =
        [
            SplitPosix("'node:\"my*odd id\"'"),
            SplitPowerShell("'node:\"my*odd id\"'"),
            SplitWindows("\"node:\\\"my*odd id\\\"\""),
        ];

        // Assert: each is the selector, which the tool accepts and names in the warning
        foreach (var argument in arguments)
        {
            Assert.Equal(OddSelector, argument);
            using var document = RenderPositions(directory, flow, argument);
            var warning = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
            Assert.Equal($"The --positions selector '{OddSelector}' matched nothing.", warning.GetProperty("message").GetString());
        }
    }

    /// <summary>
    ///     Test the kinds the Mermaid card lists for each diagram type by rendering the help example of the type.
    /// </summary>
    /// <param name="example">The example of the <c>mermaid</c> topic.</param>
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
    public void HelpTopics_PositionsMermaid_KindsPerType_AreTheKindsReported(string example, string kinds)
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = SaveExample(directory, "mermaid", example, example + ".mmd");
        var card = HelpCardText.Flatten(HelpCardText.TextOf("positions/mermaid"));

        // Act
        using var document = RenderPositions(directory, input, "all");

        // Assert: the reported kinds, each named by the card
        var reported = document.RootElement.GetProperty("positions").EnumerateArray().Select(p => p.GetProperty("kind").GetString()!).Distinct().ToList();
        Assert.Equal(kinds.Split(',').Order(StringComparer.Ordinal), reported.Order(StringComparer.Ordinal));
        Assert.All(reported, kind => Assert.Contains(kind, card, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test the recipe of the <c>svg framed-figure</c> card for a diagram: its <c>read-positions</c> command reports
    ///     the Send node, whose pixel in the picture rendered at the box size is the node's fill.
    /// </summary>
    [Fact]
    public void HelpTopics_FramedFigureAdvice_ReadPositions_PixelIsOnTheNode()
    {
        // Arrange: the command of the card, with the file names in a temporary directory
        var command = Assert.Single(HelpTopics.Get("svg/framed-figure").Examples, e => e.Name == "read-positions").Content;
        Assert.Contains("--positions node:send", command, StringComparison.Ordinal);
        using var directory = new TempDirectory();
        SaveExample(directory, "mermaid", "flowchart", "flowchart.mmd");
        var arguments = command.Split(' ')[1..]
            .Select(a => a.EndsWith(".mmd", StringComparison.Ordinal) || a.EndsWith(".png", StringComparison.Ordinal) ? directory.Combine(a) : a)
            .ToArray();

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke(arguments);

        // Assert: the pixel of the node is on its fill (the light green of the example), at the size of the box
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var send = Assert.Single(document.RootElement.GetProperty("positions").EnumerateArray(), p => p.GetProperty("id").GetString() == "send");
        using var picture = PngCodec.Load(directory.Combine("slot.png"));
        var x = send.GetProperty("pixel")[0].GetDouble();
        var y = send.GetProperty("box").GetProperty("y").GetDouble() + (send.GetProperty("box").GetProperty("height").GetDouble() * 0.2);
        Assert.Equal(new DemaConsulting.CanvasNet.Canvas.Rgba32(0xCC, 0xFF, 0xCC, 255), picture[(int)x, (int)y]);
    }

    /// <summary>
    ///     Test that every callout arrow of the <c>framed-flow</c> example of the <c>svg framed-figure</c> card ends on a
    ///     position of the diagram it embeds (its pixel when the diagram is rendered alone at the size of its image, plus the
    ///     image x and y), and that at least two different positions are hit.
    /// </summary>
    [Fact]
    public void HelpTopics_FramedFlowExample_CalloutsEndOnPositions()
    {
        // Arrange: the figure, the diagram image, and the callout lines
        var figure = XDocument.Parse(HelpTopics.RenderExample(HelpTopics.Get("svg"), "framed-flow"));
        var elements = figure.Descendants().ToList();
        var image = Assert.Single(elements, e => e.Name.LocalName == "image");
        Assert.Equal("flowchart.mmd", (string?)image.Attribute("href"));
        double Number(XElement element, string name) => double.Parse((string)element.Attribute(name)!, CultureInfo.InvariantCulture);
        var (x, y, width, height) = (Number(image, "x"), Number(image, "y"), Number(image, "width"), Number(image, "height"));
        var tips = elements
            .Where(e => e.Name.LocalName == "polyline" && (string?)e.Attribute("class") == "callout")
            .Select(e => ((string)e.Attribute("points")!).Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1].Split(','))
            .Select(p => (X: double.Parse(p[0], CultureInfo.InvariantCulture), Y: double.Parse(p[1], CultureInfo.InvariantCulture)))
            .ToList();
        Assert.NotEmpty(tips);

        // Act: render the diagram alone at the size of its image and read every position
        using var directory = new TempDirectory();
        var input = SaveExample(directory, "mermaid", "flowchart", "flowchart.mmd");
        var size = string.Create(CultureInfo.InvariantCulture, $"{width}x{height}");
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "--size", size, "--positions", "all", "--json", "-o", directory.Combine("slot.png"));
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var positions = document.RootElement.GetProperty("positions").EnumerateArray()
            .Select(p => (
                Id: p.GetProperty("kind").GetString() + ":" + p.GetProperty("id").GetString(),
                X: p.GetProperty("pixel")[0].GetDouble() + x,
                Y: p.GetProperty("pixel")[1].GetDouble() + y))
            .ToList();

        // Assert: every tip is within a quarter unit of a position, and at least two different positions are hit
        var hit = tips.Select(t => positions.FirstOrDefault(p => Math.Abs(p.X - t.X) <= 0.25 && Math.Abs(p.Y - t.Y) <= 0.25).Id).ToList();
        Assert.All(hit, Assert.NotNull);
        Assert.True(hit.Distinct(StringComparer.Ordinal).Count() >= 2, "The callouts hit fewer than two positions.");
    }

    /// <summary>
    ///     Splits one word as a POSIX shell (bash, zsh) does: the text between single quotes is literal.
    /// </summary>
    /// <param name="word">The word as typed.</param>
    /// <returns>The argument the program receives.</returns>
    private static string SplitPosix(string word)
    {
        // Only the single-quoted form of the card is modeled: the quotes are removed and nothing inside is special
        Assert.True(word.Length >= 2 && word[0] == '\'' && word[^1] == '\'', $"'{word}' is not single-quoted.");
        Assert.DoesNotContain('\'', word[1..^1]);
        return word[1..^1];
    }

    /// <summary>
    ///     Splits one word as PowerShell 7.3 or later does for a native program: a single-quoted string is literal (a
    ///     doubled single quote is one), and the argument is passed to the program as it is.
    /// </summary>
    /// <param name="word">The word as typed.</param>
    /// <returns>The argument the program receives.</returns>
    private static string SplitPowerShell(string word)
    {
        Assert.True(word.Length >= 2 && word[0] == '\'' && word[^1] == '\'', $"'{word}' is not single-quoted.");
        return word[1..^1].Replace("''", "'", StringComparison.Ordinal);
    }

    /// <summary>
    ///     Splits one word of a cmd command line as a program's start-up code (CommandLineToArgvW) does: double quotes
    ///     group, and backslashes before a double quote escape it.
    /// </summary>
    /// <param name="word">The word as typed.</param>
    /// <returns>The argument the program receives.</returns>
    private static string SplitWindows(string word)
    {
        var result = new StringBuilder();
        var quoted = false;
        var index = 0;
        while (index < word.Length)
        {
            // Count a run of backslashes: before a quote, each pair is one backslash and an odd one escapes the quote
            var backslashes = 0;
            while (index < word.Length && word[index] == '\\')
            {
                backslashes++;
                index++;
            }

            if (index < word.Length && word[index] == '"')
            {
                result.Append('\\', backslashes / 2);
                if (backslashes % 2 == 1)
                {
                    result.Append('"');
                }
                else
                {
                    quoted = !quoted;
                }

                index++;
                continue;
            }

            result.Append('\\', backslashes);
            if (index < word.Length)
            {
                Assert.True(quoted || word[index] != ' ', "An unquoted space would split the word.");
                result.Append(word[index]);
                index++;
            }
        }

        Assert.False(quoted, "The word leaves a quote open.");
        return result.ToString();
    }
}
