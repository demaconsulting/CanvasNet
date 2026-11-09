using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Syntax;

/// <summary>
///     Subsystem tests of the Syntax subsystem: the preprocessing, the YAML reader and the detector working together on a
///     whole diagram text, with locations in the original text.
/// </summary>
public class SyntaxTests
{
    /// <summary>
    ///     Validates a text with every part Mermaid removes before parsing: the frontmatter is read, the directive is found,
    ///     the comments go, the type is detected at its location, and the parser text maps back to the original lines.
    /// </summary>
    [Fact]
    public void Syntax_PreprocessAndDetect_FullText_KeepsOriginalLocations()
    {
        // Arrange
        const string Text = "---\r\ntitle: Pipeline\r\nconfig:\r\n  flowchart:\r\n    curve: linear\r\n---\r\n" +
                            "%%{init: {'theme':'dark'}}%%\r\n%% a comment\r\nflowchart LR\r\n  A[#quot;x#quot;] --> B\r\n";

        // Act
        var source = Preprocessor.Run(Text, "p.mmd", MermaidLimits.Default);
        var detected = DiagramDetector.Detect(source.Code, DetectorSettings.Default);
        var b = source.ParserText.Text.LastIndexOf('B');

        // Assert
        Assert.Equal("Pipeline", source.Frontmatter!.Title);
        var flowchart = Assert.IsType<YamlMapping>(Assert.IsType<YamlMapping>(source.Frontmatter.Config).Get("flowchart"));
        Assert.Equal("linear", ((YamlScalar)flowchart.Get("curve")!).Value);
        Assert.Equal(("init", "{\"theme\":\"dark\"}"), (source.Directives[0].Type, source.Directives[0].Arguments));
        Assert.Equal(("flowchart-v2", new MermaidLocation("p.mmd", 9, 1)), (detected.MermaidId, detected.Location));
        Assert.Equal(new MermaidLocation("p.mmd", 10, 24), source.ParserText.Locate(b));
    }

    /// <summary>
    ///     Validates that a refused YAML construct in the frontmatter stops the text with its location.
    /// </summary>
    [Fact]
    public void Syntax_Preprocess_FrontmatterAlias_IsRefusedAtLocation()
    {
        // Arrange
        const string Text = "---\nbase: &b {a: 1}\nother: *b\n---\npie";

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Preprocessor.Run(Text, null, MermaidLimits.Default));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.YamlUnsupported, ex.Code);
        Assert.Equal(new MermaidLocation(null, 2, 7), ex.Location);
    }

    /// <summary>
    ///     Validates that a lexer built on the scanner reads the prepared parser text: its tokens are located in the
    ///     original text after the removed frontmatter and comment line, and a character that no rule matches is a
    ///     located lexical error.
    /// </summary>
    [Fact]
    public void Syntax_ScanParserText_AfterFrontmatter_LocatesTokensAndLexicalError()
    {
        // Arrange: a text with a frontmatter and a comment line, lexed by a grammar of words and line feeds
        const string Text = "---\ntitle: T\n---\n%% note\nflowchart LR\n  a b\n  c ?\n";
        var source = Preprocessor.Run(Text, "s.mmd", MermaidLimits.Default);
        var scanner = new SourceScanner(source.ParserText, WordRules(), WordConditions());
        var tokens = new List<ScanToken>();

        // Act: lex until the scanner refuses the text (a bounded number of tokens)
        var ex = Assert.Throws<MermaidSyntaxException>(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                tokens.Add(scanner.Next());
            }
        });

        // Assert
        Assert.Equal(["WORD", "WORD", "NEWLINE", "WORD", "WORD", "NEWLINE", "WORD"], tokens.Select(t => t.Kind));
        Assert.Equal(["flowchart", "LR", "\n", "a", "b", "\n", "c"], tokens.Select(t => t.Text));
        Assert.Equal(new MermaidLocation("s.mmd", 5, 1), scanner.Locate(tokens[0].Start));
        Assert.Equal(new MermaidLocation("s.mmd", 6, 5), scanner.Locate(tokens[4].Start));
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, new MermaidLocation("s.mmd", 7, 5)), (ex.Code, ex.Location));
        Assert.Contains("'?'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a token the grammar does not accept, read from the prepared parser text, is refused at its
    ///     place in the original text with the accepted tokens in the grammar's words.
    /// </summary>
    [Fact]
    public void Syntax_ParseParserText_UnexpectedToken_ListsExpectedTokensAtOriginalLocation()
    {
        // Arrange: after the keyword and the direction the grammar accepts only a line feed
        const string Text = "---\ntitle: T\n---\nflowchart LR LR\n";
        var source = Preprocessor.Run(Text, "e.mmd", MermaidLimits.Default);
        var cursor = new TokenCursor(
            new SourceScanner(source.ParserText, WordRules(), WordConditions()),
            kind => kind == "NEWLINE" ? "a new line" : "a word");

        // Act
        cursor.Expect("WORD");
        cursor.Expect("WORD");
        var ex = Assert.Throws<MermaidSyntaxException>(() => cursor.Expect("NEWLINE"));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, new MermaidLocation("e.mmd", 4, 14)), (ex.Code, ex.Location));
        Assert.Equal(["a new line"], ex.Expected);
        Assert.Contains("found a word ('LR')", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the HTML of a label, prepared as Mermaid prepares the text (the double quotes of its attributes
    ///     turned into single quotes so the label can stay in a quoted string), is cleaned of the script, the event
    ///     handler and the unsafe link, and that its named character reference is decoded.
    /// </summary>
    [Fact]
    public void Syntax_PrepareAndSanitizeLabel_UnsafeHtml_IsCleaned()
    {
        // Arrange: a node whose label holds a link with double-quoted attributes, a script and a reference
        const string Text = "flowchart LR\n  A[\"<a href=\"javascript:alert(1)\" onclick=\"steal()\">go</a><script>bad()</script> caf&eacute; <b>ok</b>\"]\n";
        var source = Preprocessor.Run(Text, null, MermaidLimits.Default);
        var parserText = source.ParserText.Text;
        var start = parserText.IndexOf("[\"", StringComparison.Ordinal) + 2;
        var label = parserText[start..parserText.IndexOf("\"]", start, StringComparison.Ordinal)];

        // Act
        var cleaned = HtmlSanitizer.Sanitize(label);

        // Assert
        Assert.Equal("<a href='javascript:alert(1)' onclick='steal()'>go</a><script>bad()</script> caf&eacute; <b>ok</b>", label);
        Assert.Equal("<a>go</a> caf\u00e9 <b>ok</b>", cleaned);
    }

    /// <summary>
    ///     Gets the rules of a small grammar: spaces are skipped, and line feeds and words are tokens.
    /// </summary>
    /// <returns>The rules.</returns>
    private static List<ScanRule> WordRules() =>
    [
        new("SPACE", ScanPatterns.Run(c => c == ' '), _ => null),
        new("NEWLINE", ScanPatterns.Literal("\n")),
        new("WORD", ScanPatterns.Run(ScanPatterns.IsJsWordChar)),
    ];

    /// <summary>
    ///     Gets the start conditions of the small grammar: every rule in the initial condition.
    /// </summary>
    /// <returns>The conditions.</returns>
    private static Dictionary<string, IReadOnlyList<int>> WordConditions() =>
        new() { [SourceScanner.InitialState] = [0, 1, 2] };
}
