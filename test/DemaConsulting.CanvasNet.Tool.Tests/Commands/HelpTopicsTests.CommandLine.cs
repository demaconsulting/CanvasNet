using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Tests that the command-line advice the tool adds to the help cards holds when the tool runs it: the grid and
///     <c>inspect pixel</c> commands and the <c>read-positions</c> command of the framed-figure recipe, the view angles
///     of a command-line input fragment, and the quoting of a selector in each shell.
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
