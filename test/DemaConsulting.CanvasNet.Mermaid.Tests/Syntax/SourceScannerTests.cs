using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Syntax;

/// <summary>
///     Unit tests for <see cref="SourceScanner"/>, <see cref="TokenCursor"/> and <see cref="ScanPatterns"/>: jison's lexer
///     semantics (first matching rule, start conditions, <c>less</c>, the end-of-input rule) and the located errors.
/// </summary>
public class SourceScannerTests
{
    /// <summary>
    ///     Builds a small grammar: white space is skipped, <c>-</c> comes before <c>--&gt;</c> (so the first rule wins),
    ///     a quote enters a string state, words are tokens, and an end-of-input rule returns <c>EOF_RULE</c>.
    /// </summary>
    /// <param name="text">The input.</param>
    /// <returns>The scanner.</returns>
    private static SourceScanner Scanner(string text)
    {
        var rules = new List<ScanRule>
        {
            new("SPACE", ScanPatterns.Run(c => c == ' '), _ => null),
            new("MINUS", ScanPatterns.Literal("-")),
            new("LINK", ScanPatterns.Literal("-->")),
            new("QUOTE", ScanPatterns.Literal("\""), s =>
            {
                s.Begin("string");
                return null;
            }),
            new("STR", ScanPatterns.Run(c => c != '"')),
            new("END_QUOTE", ScanPatterns.Literal("\""), s =>
            {
                s.PopState();
                return null;
            }),
            new("WORD", ScanPatterns.Run(ScanPatterns.IsJsWordChar)),
            new("ARROW", ScanPatterns.Literal("=>"), s =>
            {
                s.Less(1);
                return "EQ";
            }),
            new("EOF_RULE", ScanPatterns.EndOfInput),
        };
        var conditions = new Dictionary<string, IReadOnlyList<int>>
        {
            [SourceScanner.InitialState] = [0, 1, 2, 3, 6, 7, 8],
            ["string"] = [5, 4],
        };
        return new SourceScanner(MermaidTestHelpers.Mapped(text, "s.mmd"), rules, conditions);
    }

    /// <summary>
    ///     Lexes every token kind of an input.
    /// </summary>
    /// <param name="scanner">The scanner.</param>
    /// <returns>The kinds, up to and including the first end of input.</returns>
    private static List<string> Kinds(SourceScanner scanner)
    {
        var kinds = new List<string>();
        while (true)
        {
            var token = scanner.Next();
            kinds.Add(token.Kind);
            if (token.Kind == SourceScanner.EndOfInput)
            {
                return kinds;
            }
        }
    }

    /// <summary>
    ///     Validates the first-match rule: the earlier rule wins even when a later one matches more.
    /// </summary>
    [Fact]
    public void SourceScanner_Next_FirstMatchingRuleWins()
    {
        // Arrange / Act
        var kinds = Kinds(Scanner("a -- b"));

        // Assert: "-" comes before "-->" in the rule list, so the longer rule never wins
        Assert.Equal(["WORD", "MINUS", "MINUS", "WORD", "EOF_RULE", "EOF"], kinds);
    }

    /// <summary>
    ///     Validates start conditions: a quote enters the string state, whose rules lex the content.
    /// </summary>
    [Fact]
    public void SourceScanner_Next_StartConditions_SwitchRules()
    {
        // Arrange
        var scanner = Scanner("a \"x y\" b");

        // Act
        var first = scanner.Next();
        var str = scanner.Next();
        var depth = scanner.StateDepth;
        var last = scanner.Next();

        // Assert
        Assert.Equal(("WORD", "a"), (first.Kind, first.Text));
        Assert.Equal(("STR", "x y", 3, 6), (str.Kind, str.Text, str.Start, str.End));
        Assert.Equal(2, depth);
        Assert.Equal(("WORD", "b"), (last.Kind, last.Text));
        Assert.Equal(SourceScanner.InitialState, scanner.State);
    }

    /// <summary>
    ///     Validates <c>less</c>: the rest of a match goes back to the input.
    /// </summary>
    [Fact]
    public void SourceScanner_Less_ReturnsRestOfMatchToInput()
    {
        // Arrange
        var scanner = Scanner("=>");

        // Act
        var eq = scanner.Next();

        // Assert
        Assert.Equal(("EQ", "=", 0, 1), (eq.Kind, eq.Text, eq.Start, eq.End));
        Assert.Equal(1, scanner.Position);
    }

    /// <summary>
    ///     Validates the end of input: the end-of-input rule runs once, then the scanner returns the end repeatedly.
    /// </summary>
    [Fact]
    public void SourceScanner_Next_EndOfInputRule_RunsOnce()
    {
        // Arrange
        var scanner = Scanner("a");

        // Act
        var kinds = new[] { scanner.Next().Kind, scanner.Next().Kind, scanner.Next().Kind, scanner.Next().Kind };

        // Assert
        Assert.Equal(["WORD", "EOF_RULE", "EOF", "EOF"], kinds);
    }

    /// <summary>
    ///     Validates that text no rule matches is a located lexical error.
    /// </summary>
    [Fact]
    public void SourceScanner_Next_UnrecognizedText_ThrowsLocatedError()
    {
        // Arrange
        var scanner = Scanner("a\n  #");

        // Act
        scanner.Next();
        var ex = Assert.Throws<MermaidSyntaxException>(() => Kinds(scanner));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new MermaidLocation("s.mmd", 1, 2), ex.Location);
        Assert.Contains("(a line break)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the state stack operations at their limits.
    /// </summary>
    [Fact]
    public void SourceScanner_StateStack_PopAtInitialAndTopStateBeyondDepth()
    {
        // Arrange
        var scanner = Scanner("a");

        // Act
        scanner.Begin("string");
        var top = scanner.TopState();
        var below = scanner.TopState(1);
        var beyond = scanner.TopState(5);
        scanner.PopState();
        var popInitial = scanner.PopState();

        // Assert
        Assert.Equal(("string", SourceScanner.InitialState, SourceScanner.InitialState), (top, below, beyond));
        Assert.Equal(SourceScanner.InitialState, popInitial);
        Assert.Throws<ArgumentException>(() => scanner.Begin("missing"));
    }

    /// <summary>
    ///     Validates that a rule set that makes no progress is reported as a defect instead of looping.
    /// </summary>
    [Fact]
    public void SourceScanner_Next_NoProgress_ThrowsInvalidOperation()
    {
        // Arrange: a rule that matches nothing and skips forever
        var rules = new List<ScanRule> { new("NOTHING", (_, _) => 0, _ => null) };
        var conditions = new Dictionary<string, IReadOnlyList<int>> { [SourceScanner.InitialState] = [0] };
        var scanner = new SourceScanner(MermaidTestHelpers.Mapped("x"), rules, conditions);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => scanner.Next());
    }

    /// <summary>
    ///     Validates the checks of the condition table.
    /// </summary>
    [Fact]
    public void SourceScanner_Constructor_InvalidConditions_Throw()
    {
        // Arrange
        var rules = new List<ScanRule> { new("A", ScanPatterns.Literal("a")) };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => new SourceScanner(MermaidTestHelpers.Mapped("a"), rules, new Dictionary<string, IReadOnlyList<int>>()));
        Assert.Throws<ArgumentException>(() => new SourceScanner(
            MermaidTestHelpers.Mapped("a"),
            rules,
            new Dictionary<string, IReadOnlyList<int>> { [SourceScanner.InitialState] = [1] }));
    }

    /// <summary>
    ///     Validates the cursor: one token of lookahead, and an error that lists the expected tokens like jison.
    /// </summary>
    [Fact]
    public void TokenCursor_Expect_WrongToken_ThrowsWithExpected()
    {
        // Arrange
        var cursor = new TokenCursor(Scanner("a - b"), kind => kind switch
        {
            "LINK" => "'-->'",
            "WORD" => "a node id",
            _ => $"'{kind}'",
        });

        // Act
        var word = cursor.Expect("WORD");
        var ex = Assert.Throws<MermaidSyntaxException>(() => cursor.Expect("LINK", "WORD", "LINK"));

        // Assert
        Assert.Equal("a", word.Text);
        Assert.True(cursor.Is("MINUS"));
        Assert.Equal(["'-->'", "a node id"], ex.Expected);
        Assert.Equal(new MermaidLocation("s.mmd", 1, 3), ex.Location);
        Assert.Contains("expected '-->' or a node id, found 'MINUS' ('-')", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the joining of alternatives in messages.
    /// </summary>
    [Fact]
    public void TokenCursor_JoinAlternatives_JoinsWithCommasAndOr()
    {
        // Arrange / Act / Assert
        Assert.Equal("nothing", TokenCursor.JoinAlternatives([]));
        Assert.Equal("a", TokenCursor.JoinAlternatives(["a"]));
        Assert.Equal("a, b or c", TokenCursor.JoinAlternatives(["a", "b", "c"]));
    }

    /// <summary>
    ///     Validates the matcher building blocks and the JavaScript character classes.
    /// </summary>
    [Fact]
    public void ScanPatterns_Matchers_BehaveAsRegexParts()
    {
        // Arrange
        var keyword = ScanPatterns.Sequence(ScanPatterns.Literal("graph", ignoreCase: true), ScanPatterns.Optional(ScanPatterns.Run(ScanPatterns.IsJsWhitespace)));
        var choice = ScanPatterns.Choice(ScanPatterns.Literal("--"), ScanPatterns.Literal("-"));
        var notArrow = ScanPatterns.NotFollowedBy(ScanPatterns.Literal("-"), ScanPatterns.Literal(">"));

        // Act / Assert
        Assert.Equal(6, keyword("GRAPH TD", 0));
        Assert.Equal(-1, keyword("gr", 0));
        Assert.Equal(2, choice("--x", 0));
        Assert.Equal(1, choice("-x", 0));
        Assert.Equal(-1, notArrow("->", 0));
        Assert.Equal(1, notArrow("-x", 0));
        Assert.Equal(-1, ScanPatterns.Run(char.IsAsciiDigit, 2)("1a", 0));
        Assert.True(ScanPatterns.IsJsWhitespace('\u00a0') && ScanPatterns.IsJsWhitespace('\ufeff') && ScanPatterns.IsJsWhitespace('\u2005'));
        Assert.False(ScanPatterns.IsJsWhitespace('\u200b'));
        Assert.True(ScanPatterns.IsJsLineTerminator('\u2028'));
        Assert.False(ScanPatterns.IsJsWordChar('\u00e9'));
    }

    /// <summary>
    ///     Validates the letters outside ASCII of the <c>UNICODE_TEXT</c> rule that the flowchart and class grammars share:
    ///     letters of every script are letters, while ASCII (matched by the grammars' own rules), punctuation, white space
    ///     and the multiplication and division signs inside the Latin-1 letters are not.
    /// </summary>
    [Fact]
    public void ScanPatterns_IsUnicodeLetter_FollowsTheGrammarLetters()
    {
        // Act / Assert
        Assert.All(['\u00aa', '\u00c0', '\u00e9', '\u0416', '\u4e2d'], c => Assert.True(ScanPatterns.IsUnicodeLetter(c), $"U+{(int)c:X4}"));
        Assert.All(['a', '1', '_', ' ', '\u00a0', '\u00d7', '\u00f7'], c => Assert.False(ScanPatterns.IsUnicodeLetter(c), $"U+{(int)c:X4}"));
    }
}
