using DemaConsulting.CanvasNet.Viewer.Help;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Help;

/// <summary>
///     Unit tests for the HelpFrontMatter unit, the front-matter parser of the help cards: the supported YAML subset, the body, and a clear error
///     with the file and the line for everything outside the subset.
/// </summary>
public class HelpFrontMatterTests
{
    /// <summary>
    ///     Test that the parser reads plain, single-quoted, and double-quoted scalars, a list of scalars, a list of maps,
    ///     comments, and blank lines, and that the body follows the closing fence.
    /// </summary>
    [Fact]
    public void HelpFrontMatter_Parse_SupportedSubset_ReadsEveryConstruct()
    {
        // Arrange: a card that uses every construct of the subset, with CRLF line endings
        var text = string.Join(
            "\r\n",
            "---",
            "# A comment",
            "title: Gantt charts",
            "summary: 'It''s a summary: with a colon'",
            "",
            "aliases:",
            "  - gantt-chart",
            "  - \"quoted \\\"alias\\\" \\\\ here\"",
            "examples:",
            "  - name: gantt",
            "    kind: source",
            "  # A comment inside a list",
            "  - name: render",
            "    content: 'canvasnet render gantt.mmd -o gantt.png'",
            "---",
            "",
            "Body line one.",
            "",
            "- a bullet",
            "",
            "");

        // Act
        var document = HelpFrontMatter.Parse(text, "mermaid/gantt.md");

        // Assert: the keys in order, with their values and lines
        Assert.Equal(["title", "summary", "aliases", "examples"], document.Properties.Select(p => p.Key));
        var properties = document.Properties.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("Gantt charts", properties["title"].Scalar);
        Assert.Equal(3, properties["title"].Line);
        Assert.Equal("It's a summary: with a colon", properties["summary"].Scalar);
        Assert.Equal(["gantt-chart", "quoted \"alias\" \\ here"], properties["aliases"].Items!.Select(i => i.Scalar));
        var examples = properties["examples"].Items!;
        Assert.Equal(2, examples.Count);
        Assert.Equal(10, examples[0].Line);
        Assert.Equal(["name", "kind"], examples[0].Map!.Select(p => p.Key));
        Assert.Equal("source", examples[0].Map![1].Value.Scalar);
        Assert.Equal("canvasnet render gantt.mmd -o gantt.png", examples[1].Map![1].Value.Scalar);
        Assert.Equal("Body line one.\n\n- a bullet", document.Body);
        Assert.Equal(17, document.BodyLine);
    }

    /// <summary>
    ///     Test that every construct outside the subset is an error whose message names the file and the line.
    /// </summary>
    /// <param name="frontMatter">The front-matter lines, separated by <c>~</c>.</param>
    /// <param name="line">The line the error must name.</param>
    /// <param name="reason">A part of the reason the error must give.</param>
    [Theory]
    [InlineData("title:\tx", 2, "tab")]
    [InlineData("aliases: [a, b]", 2, "flow sequence")]
    [InlineData("aliases: {a: b}", 2, "flow mapping")]
    [InlineData("summary: |", 2, "block scalar")]
    [InlineData("summary: >", 2, "block scalar")]
    [InlineData("title: &x anchor", 2, "anchor")]
    [InlineData("title: *x", 2, "alias")]
    [InlineData("title: !tag x", 2, "tag")]
    [InlineData("title: x # comment", 2, "trailing comment")]
    [InlineData("title: 'x' # comment", 2, "closing quote")]
    [InlineData("title: 'unterminated", 2, "not closed")]
    [InlineData("title: \"bad \\n escape\"", 2, "escapes")]
    [InlineData("title: key: value", 2, "': '")]
    [InlineData("title: `code`", 2, "quote it")]
    [InlineData("title: x~title: y", 3, "repeated")]
    [InlineData("aliases:~ - a", 3, "indentation")]
    [InlineData("aliases:~    - a", 3, "indentation")]
    [InlineData("title: x~  - a", 3, "indentation")]
    [InlineData("examples:~  - name: a~      kind: deep", 4, "indentation")]
    [InlineData("examples:~  - name: a~    name: b", 4, "repeated")]
    [InlineData("examples:~  - name:", 3, "needs a value")]
    [InlineData("aliases:~title: x", 2, "neither a value nor list items")]
    [InlineData("Title: x", 2, "lower case")]
    public void HelpFrontMatter_Parse_OutsideTheSubset_ThrowsWithFileAndLine(string frontMatter, int line, string reason)
    {
        // Arrange: a card whose front matter holds the construct
        var text = $"---\n{frontMatter.Replace('~', '\n')}\n---\nBody.";

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => HelpFrontMatter.Parse(text, "mermaid/gantt.md"));

        // Assert: the message names the file, the line, and the reason
        Assert.StartsWith($"Help card 'mermaid/gantt.md', line {line}: ", exception.Message);
        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a card without an opening or a closing fence is an error that names the file and the line.
    /// </summary>
    [Fact]
    public void HelpFrontMatter_Parse_MissingFence_ThrowsWithFileAndLine()
    {
        // Act
        var opening = Assert.Throws<InvalidOperationException>(() => HelpFrontMatter.Parse("title: x\n---\nBody.", "a.md"));
        var closing = Assert.Throws<InvalidOperationException>(() => HelpFrontMatter.Parse("---\ntitle: x\nBody.", "a.md"));

        // Assert
        Assert.StartsWith("Help card 'a.md', line 1: ", opening.Message);
        Assert.Contains("opens the front matter", opening.Message, StringComparison.Ordinal);
        Assert.StartsWith("Help card 'a.md', line 3: ", closing.Message);
        Assert.Contains("no closing '---'", closing.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the body keeps the trailing spaces of its last line (a Markdown hard break or code-block text) and
    ///     only loses the trailing line feeds.
    /// </summary>
    [Fact]
    public void HelpFrontMatter_Parse_BodyEndingInSpaces_KeepsTheSpaces()
    {
        // Act
        var document = HelpFrontMatter.Parse("---\ntitle: x\n---\n\nFirst line  \nlast line  \n\n", "a.md");

        // Assert
        Assert.Equal("First line  \nlast line  ", document.Body);
    }
}
