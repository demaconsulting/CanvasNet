using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Syntax;

/// <summary>
///     Unit tests for <see cref="Preprocessor"/>: Mermaid's preprocessing steps and the location map back to the original
///     text.
/// </summary>
public class PreprocessorTests
{
    /// <summary>The default limits.</summary>
    private static readonly MermaidLimits Limits = MermaidLimits.Default;

    /// <summary>
    ///     Validates that CRLF and CR become LF while positions keep pointing at the original lines.
    /// </summary>
    [Fact]
    public void Preprocessor_Run_LineEndings_NormalizedAndMapped()
    {
        // Arrange
        const string Text = "graph TD\r\n  A-->B\r  B-->C";

        // Act
        var result = Preprocessor.Run(Text, "x.mmd", Limits);
        var c = result.Code.Text.IndexOf('C', StringComparison.Ordinal);

        // Assert
        Assert.Equal("graph TD\n  A-->B\n  B-->C", result.Code.Text);
        Assert.Equal(new MermaidLocation("x.mmd", 3, 7), result.Code.Locate(c));
    }

    /// <summary>
    ///     Validates that the double quotes of HTML attributes become single quotes, as in Mermaid's <c>cleanupText</c>.
    /// </summary>
    [Fact]
    public void Preprocessor_CleanupText_HtmlAttributes_UseSingleQuotes()
    {
        // Arrange
        var text = MermaidTestHelpers.Mapped("A[<a href=\"x\" title=\"t\">link</a> \"quoted\"]");

        // Act
        var cleaned = Preprocessor.CleanupText(text);

        // Assert
        Assert.Equal("A[<a href='x' title='t'>link</a> \"quoted\"]", cleaned.Text);
    }

    /// <summary>
    ///     Validates that the frontmatter is removed and its title, display mode and configuration are kept.
    /// </summary>
    [Fact]
    public void Preprocessor_Run_Frontmatter_IsReadAndRemoved()
    {
        // Arrange
        const string Text = "---\ntitle: Orders\ndisplayMode: compact\nconfig:\n  theme: dark\n---\nflowchart LR\n A-->B";

        // Act
        var result = Preprocessor.Run(Text, null, Limits);

        // Assert
        Assert.Equal("flowchart LR\n A-->B", result.Code.Text);
        var frontmatter = Assert.IsType<FrontmatterData>(result.Frontmatter);
        Assert.Equal(("Orders", "compact"), (frontmatter.Title, frontmatter.DisplayMode));
        var config = Assert.IsType<YamlMapping>(frontmatter.Config);
        Assert.Equal("dark", ((YamlScalar)config.Get("theme")!).Value);
        Assert.Equal(new MermaidLocation(null, 1, 1), frontmatter.Location);
        Assert.Equal(new MermaidLocation(null, 7, 1), result.Code.Locate(0));
    }

    /// <summary>
    ///     Validates that the title is converted as JavaScript's <c>String(value)</c> and dropped when falsy.
    /// </summary>
    /// <param name="yaml">The title line.</param>
    /// <param name="expected">The expected title, or null.</param>
    [Theory]
    [InlineData("title: 1.50", "1.5")]
    [InlineData("title: true", "true")]
    [InlineData("title: 0", null)]
    [InlineData("title: ''", null)]
    [InlineData("title: [a, b]", "a,b")]
    [InlineData("title: \"x: y\"", "x: y")]
    public void Preprocessor_Run_FrontmatterTitle_FollowsJavaScriptConversion(string yaml, string? expected)
    {
        // Arrange / Act
        var result = Preprocessor.Run($"---\n{yaml}\n---\npie", null, Limits);

        // Assert
        Assert.Equal(expected, result.Frontmatter!.Title);
    }

    /// <summary>
    ///     Validates the indented frontmatter: the closing line must have the same indent, and the indent is removed from the body.
    /// </summary>
    [Fact]
    public void Preprocessor_MatchFrontmatter_Indented_RemovesIndentFromBody()
    {
        // Arrange / Act
        var result = Preprocessor.Run("  ---\n  title: Indented\n  config:\n    theme: base\n  ---\npie", null, Limits);

        // Assert
        Assert.Equal("Indented", result.Frontmatter!.Title);
        Assert.Equal("pie", result.Code.Text);
    }

    /// <summary>
    ///     Validates the backtracking cases of the frontmatter expression: an empty body closes, and a block without its
    ///     closing line is not frontmatter.
    /// </summary>
    [Fact]
    public void Preprocessor_MatchFrontmatter_EmptyAndUnclosed_FollowMermaidExpression()
    {
        // Arrange / Act
        var empty = Preprocessor.MatchFrontmatter("---\n---\npie");
        var blank = Preprocessor.MatchFrontmatter("---\n\n---\npie");
        var unclosed = Preprocessor.MatchFrontmatter("---\ntitle: x\npie");
        var dashes = Preprocessor.MatchFrontmatter("----\n---\npie");

        // Assert
        Assert.Null(empty);
        Assert.Equal(new FrontmatterMatch(0, 4, 4, 9), blank);
        Assert.Null(unclosed);
        Assert.Null(dashes);
    }

    /// <summary>
    ///     Validates that a frontmatter whose YAML is not a mapping keeps nothing.
    /// </summary>
    [Fact]
    public void Preprocessor_Run_FrontmatterNotMapping_KeepsNothing()
    {
        // Arrange / Act
        var result = Preprocessor.Run("---\n- a\n- b\n---\npie", null, Limits);

        // Assert
        Assert.Equal(new FrontmatterData(null, null, null, new MermaidLocation(null, 1, 1)), result.Frontmatter);
    }

    /// <summary>
    ///     Validates that a YAML error in the frontmatter is located on its line of the original text.
    /// </summary>
    [Fact]
    public void Preprocessor_Run_FrontmatterInvalidYaml_ThrowsLocated()
    {
        // Arrange / Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Preprocessor.Run("---\ntitle: a\ntitle: b\n---\npie", "f.mmd", Limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.YamlInvalid, ex.Code);
        Assert.Equal(new MermaidLocation("f.mmd", 3, 1), ex.Location);
        Assert.Contains("duplicated mapping key", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that directives are found with their type, arguments and location, and removed from the code.
    /// </summary>
    [Fact]
    public void Preprocessor_Run_Directives_AreDetectedAndRemoved()
    {
        // Arrange
        const string Text = "%%{init: {'theme': 'forest'}}%%\n%%{wrap}%%\nflowchart LR\n A-->B";

        // Act
        var result = Preprocessor.Run(Text, null, Limits);

        // Assert
        Assert.Equal(2, result.Directives.Count);
        Assert.Equal(new DirectiveText("init", "{\"theme\": \"forest\"}", true, new MermaidLocation(null, 1, 1)), result.Directives[0]);
        Assert.Equal(new DirectiveText("wrap", null, false, new MermaidLocation(null, 2, 1)), result.Directives[1]);
        Assert.Equal("flowchart LR\n A-->B", result.Code.Text);
    }

    /// <summary>
    ///     Validates the directive expression's forms: a word argument, a type without a colon, and a directive that is
    ///     not closed.
    /// </summary>
    [Fact]
    public void Preprocessor_TryMatchDirective_Forms_FollowMermaidExpression()
    {
        // Arrange / Act
        Assert.True(Preprocessor.TryMatchDirective("%%{ initialize : abc }%% x", 0, out var word));
        Assert.True(Preprocessor.TryMatchDirective("%%{init {\"a\":1}}%%", 0, out var noColon));
        Assert.True(Preprocessor.TryMatchDirective("%%{init: {\"a\":1}\nflowchart", 0, out var open));
        var none = Preprocessor.TryMatchDirective("%%{ }%%", 0, out _);

        // Assert
        Assert.Equal(new DirectiveMatch(24, "initialize", "abc", false), word);
        Assert.Equal(new DirectiveMatch(18, "init", "{\"a\":1}", true), noColon);
        Assert.Equal(("init", "{\"a\":1}\nflowchart"), (open.Type, open.Arguments));
        Assert.False(none);
    }

    /// <summary>
    ///     Validates that comment lines (with the blank lines before them) are removed, while <c>%%</c> alone and
    ///     <c>%%{</c> stay, and the start is trimmed.
    /// </summary>
    [Fact]
    public void Preprocessor_CleanupComments_RemovesCommentLinesOnly()
    {
        // Arrange
        var text = MermaidTestHelpers.Mapped("a\n\n  %% comment\nb %% inline\n%%\n%%{x}%%\nc");

        // Act
        var cleaned = Preprocessor.CleanupComments(text);

        // Assert
        Assert.Equal("a\nb %% inline\n%%\n%%{x}%%\nc", cleaned.Text);
    }

    /// <summary>
    ///     Validates Mermaid's entity encoding: named and numeric entities get the private markers, and the last
    ///     semicolon of a <c>style</c> or <c>classDef</c> line with a color is dropped.
    /// </summary>
    [Fact]
    public void Preprocessor_EncodeEntities_EncodesEntitiesAndStyleSemicolons()
    {
        // Arrange
        var text = MermaidTestHelpers.Mapped("A[\"#quot;hi#35;\"]\nstyle A fill:#f9f;\nclassDef c stroke:#333,color:#fff;\nB[a;b]");

        // Act
        var encoded = Preprocessor.EncodeEntities(text);

        // Assert
        Assert.Equal(
            "A[\"\uFB02\u00B0quot\u00B6\u00DFhi\uFB02\u00B0\u00B035\u00B6\u00DF\"]\nstyle A fill:#f9f\nclassDef c stroke:#333,color:#fff\nB[a;b]",
            encoded.Text);
    }

    /// <summary>
    ///     Validates that the parser text is the encoded code with a final line feed, mapped to the original text.
    /// </summary>
    [Fact]
    public void Preprocessor_Run_ParserText_EndsWithLineFeedAndMaps()
    {
        // Arrange / Act
        var result = Preprocessor.Run("%% c\nflowchart LR\n A[#amp;]", null, Limits);
        var marker = result.ParserText.Text.IndexOf('\uFB02', StringComparison.Ordinal);

        // Assert
        Assert.EndsWith("\n", result.ParserText.Text, StringComparison.Ordinal);
        Assert.Equal(new MermaidLocation(null, 3, 4), result.ParserText.Locate(marker));
    }

    /// <summary>
    ///     Validates that every step handles long runs of the patterns it looks for (the worst cases of the original
    ///     regular expressions) and gives the expected text; the steps are linear, so these complete promptly.
    /// </summary>
    /// <param name="unit">The repeated pattern.</param>
    [Theory]
    [InlineData("%%}%%")]
    [InlineData("%%")]
    [InlineData("%%{")]
    [InlineData("<a ")]
    [InlineData("style a:#f")]
    [InlineData("\n ")]
    [InlineData("#a")]
    [InlineData("---\n")]
    public void Preprocessor_Steps_LongRepeatedPattern_Complete(string unit)
    {
        // Arrange: a text of 200,000 characters without a final line feed
        var text = MermaidTestHelpers.Mapped("pie\n" + string.Concat(Enumerable.Repeat(unit, 200_000 / unit.Length)));

        // Act
        var cleaned = Preprocessor.CleanupText(text);
        var directives = Preprocessor.DetectDirectives(cleaned);
        var code = Preprocessor.CleanupComments(Preprocessor.RemoveDirectives(cleaned));
        var encoded = Preprocessor.EncodeEntities(code);
        var detection = DiagramDetector.ReplaceComments(code);
        var frontmatter = Preprocessor.MatchFrontmatter(cleaned.Text);

        // Assert: the diagram keyword survives every step, and the steps only remove or encode
        Assert.StartsWith("pie", code.Text, StringComparison.Ordinal);
        Assert.StartsWith("pie", encoded.Text, StringComparison.Ordinal);
        Assert.True(code.Length <= text.Length);
        Assert.True(detection.Length <= code.Length);
        Assert.StartsWith("pie", detection.Text, StringComparison.Ordinal);
        Assert.Null(frontmatter);
        Assert.All(directives, d => Assert.True(d.Location.IsKnown));
    }

    /// <summary>
    ///     Validates the two size checks: the code against <see cref="MermaidLimits.MaxTextSize"/>, and the whole text
    ///     against four times it.
    /// </summary>
    [Fact]
    public void Preprocessor_Run_SizeLimits_ThrowInvalidData()
    {
        // Arrange
        var limits = new MermaidLimits { MaxTextSize = 20 };
        var comments = string.Concat(Enumerable.Repeat("%% comment line\n", 4)) + "pie";

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => Preprocessor.Run("pie\n" + new string('x', 30), null, limits));
        Assert.Throws<InvalidDataException>(() => Preprocessor.Run(new string(' ', 81) + "pie", null, limits));
        Assert.Equal("pie", Preprocessor.Run(comments, null, limits).Code.Text);
    }
}
