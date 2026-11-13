// The link-check test vectors: string literals that spell unsafe URL schemes in encoded, split or truncated forms
// (such as "&#106;avascript:" or "java\tscript:"), which are deliberately not words.
// cspell:ignoreRegExp /"(?:[^"\\\n]|\\.)*"/g
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Syntax;

/// <summary>
///     Unit tests for <see cref="SourceScanner"/>, <see cref="TokenCursor"/>, <see cref="ScanPatterns"/> and
///     <see cref="UrlSanitizer"/>: jison's lexer semantics (first matching rule, start conditions, <c>less</c>, the
///     end-of-input rule), the located errors, and the link check ported from sanitize-url.
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


    /// <summary>
    ///     Gets links that sanitize-url refuses: the attack vectors of its own test file (numeric character references with
    ///     and without semicolons, decimal and hexadecimal, control characters and white space inside the scheme, escaped
    ///     white space, nested percent-encoding, leading control characters, <c>&amp;colon;</c>, mixed case) and further
    ///     encoded forms (<c>0x</c>, <c>0o</c> and <c>0b</c> references, references beyond 65535, nested references,
    ///     percent-encoded schemes, line separators, characters outside ASCII before the scheme). The bundled
    ///     sanitize-url gives <c>about:blank</c> for every one of them.
    /// </summary>
    public static TheoryData<string> UnsafeUrls =>
    [
        "&#0000106&#0000097&#0000118&#0000097&#0000115&#0000099&#0000114&#0000105&#0000112&#0000116&#0000058&#0000097&#0000108&#0000101&#0000114&#0000116&#0000040&#0000039&#0000088&#0000083&#0000083&#0000039&#0000041",
        "&#106;&#97;&#118;&#97;&#115;&#99;&#114;&#105;&#112;&#116;&#58;&#97;&#108;&#101;&#114;&#116;&#40;&#39;&#88;&#83;&#83;&#39;&#41;",
        "&#x6A&#x61&#x76&#x61&#x73&#x63&#x72&#x69&#x70&#x74&#x3A&#x61&#x6C&#x65&#x72&#x74&#x28&#x27&#x58&#x53&#x53&#x27&#x29",
        "jav&#x09;ascript:alert('XSS');",
        " &#14; javascript:alert('XSS');",
        "javasc&Tab;ript: alert('XSS');",
        "javasc&#\u0000x09;ript:alert(1)",
        "java&#38;&#38;&#35;78&#59;ewLine&#38;newline&#59;&#59;script&#58;alert&#40;&#39;XSS&#39;&#41;",
        "java&&#78;ewLine&newline;;script:alert('XSS')",
        "javascri\u000Apt:alert('xss')",
        "javascri\u000Dpt:alert('xss')",
        "javascri\u0009pt:alert('xss')",
        "javascrip\\%74t:alert('XSS')",
        "javascrip%5c%72t:alert()",
        "javascrip%5Ctt:alert()",
        "javascrip%255Ctt:alert()",
        "javascrip%25%35Ctt:alert()",
        "javascrip%25%35%43tt:alert()",
        "javascrip%25%32%35%25%33%35%25%34%33rt:alert()",
        "javascrip%255Crt:alert('%25xss')",
        "\u000Cjavascript:alert()",
        "\u000Bjavascript:alert()",
        "\u0009javascript:alert()",
        "\u000Ajavascript:alert()",
        "\u000Djavascript:alert()",
        "\u0000javascript:alert()",
        "\u0001javascript:alert()",
        "javascript:alert(document.domain)",
        "data:alert(document.domain)",
        "vbscript:alert(document.domain)",
        "&!*javascript:alert(document.domain)",
        "&!*data:alert(document.domain)",
        "&!*vbscript:alert(document.domain)",
        "javascript&colon;alert(document.domain)",
        "data&COLON;alert(document.domain)",
        "vbscript&colon;alert(document.domain)",
        "JaVaScRiPt:alert(document.domain)",
        "DaTa:alert(document.domain)",
        "VbScRiPt:alert(document.domain)",
        "j\uFEFF\uFEFFa\u200Bvascript:alert(document.domain)",
        "    javascript:alert(document.domain)",
        "    data:alert(document.domain)",
        "&#106;avascript:alert(1)",
        "&#0106;avascript:alert(1)",
        "&#0x6A;avascript:alert(1)",
        "&#0X6a;avascript:alert(1)",
        "&#0o152;avascript:alert(1)",
        "&#0b1101010;avascript:alert(1)",
        "&#65642;avascript:alert(1)",
        "&#4294967402;avascript:alert(1)",
        "&#38;#106;avascript:alert(1)",
        "%26%23106%3Bavascript:alert(1)",
        "&#37;6Aavascript:alert(1)",
        "java&Tab;script:alert(1)",
        "java&NewLine;script:alert(1)",
        "java&TAB;script:alert(1)",
        "java\u0009script:alert(1)",
        "java&#9;script:alert(1)",
        "java&#x9;script:alert(1)",
        "java&#10;script:alert(1)",
        "java&#8203;script:alert(1)",
        "java&#65279;script:alert(1)",
        "java&#127;script:alert(1)",
        "%6Aavascript:alert(1)",
        "%6A%61%76%61%73%63%72%69%70%74%3Aalert(1)",
        "javascript%3Aalert(1)",
        "java%09script:alert(1)",
        "java%0Ascript:alert(1)",
        "java%00script:alert(1)",
        "java%E2%80%8Bscript:alert(1)",
        "java%EF%BB%BFscript:alert(1)",
        "java\\tscript:alert(1)",
        "java\\nscript:alert(1)",
        "java%5Cnscript:alert(1)",
        "java\\%6Escript:alert(1)",
        "%2520javascript:alert(1)",
        " javascript:alert(1)",
        "\u3000javascript:alert(1)",
        "\u00A0javascript:alert(1)",
        "\u0085javascript:alert(1)",
        "\u2028javascript:alert(1)",
        "x\u2028javascript:alert(1)",
        "x\u2029 javascript:alert(1)",
        "%E2%80%A8javascript:alert(1)",
        "\u00E9javascript:alert(1)",
        "javascript\u0000:alert(1)",
        "javascript:alert('%')",
        "javascript://%0Aalert(1)",
        "data:text/html;base64,PHNjcmlwdD4=",
        "DATA:text/html,x",
        "vbscript:msgbox(1)",
        "&#\u2028javascript:alert(1)",
        "\"&#106;avascript:alert(1)",
    ];

    /// <summary>
    ///     Gets safe links and what sanitize-url makes of them (the cases of its own test file and common forms): relative
    ///     and protocol-less links kept, control characters removed, backslashes as slashes, <c>http</c> links normalized.
    ///     The expected values are the bundled sanitize-url's results in Node.
    /// </summary>
    public static TheoryData<string, string> SafeUrls => new()
    {
        { "http://example.com/path/to:something", "http://example.com/path/to:something" },
        { "http://example.com:4567/path/to:something", "http://example.com:4567/path/to:something" },
        { "https://example.com", "https://example.com/" },
        { "https://example.com:4567/path/to:something", "https://example.com:4567/path/to:something" },
        { "./path/to/my.json", "./path/to/my.json" },
        { "/path/to/my.json", "/path/to/my.json" },
        { "//google.com/robots.txt", "//google.com/robots.txt" },
        { "www.example.com", "www.example.com" },
        { "com.braintreepayments.demo://example", "com.braintreepayments.demo://example" },
        { "mailto:test@example.com?subject=hello+world", "mailto:test@example.com?subject=hello+world" },
        { "www.example.com/with-\u00E1cc\u00EAnt\u0161", "www.example.com/with-\u00E1cc\u00EAnt\u0161" },
        { "www.example.com/\u043B\u043E\u0442.\u0440\u0444\u0448\u0438\u0448\u043A\u0438\u00FC\u2013", "www.example.com/\u043B\u043E\u0442.\u0440\u0444\u0448\u0438\u0448\u043A\u0438\u00FC\u2013" },
        { "www.example.com/\u200D\u0000\u001F\u0000\u001F\uFEFFfoo", "www.example.com/foo" },
        { "   http://example.com/path/to:something    ", "http://example.com/path/to:something" },
        { "https://example.com&NewLine;&NewLine;/something", "https://example.com/something" },
        { "&#104;&#116;&#116;&#112;&#115;&#0000058//&#101;&#120;&#97;&#109;&#112;&#108;&#101;&#46;&#99;&#111;&#109;/&#0000106&#0000097&#0000118&#0000097&#0000115&#0000099&#0000114&#0000105&#0000112&#0000116&#0000058&#0000097&#0000108&#0000101&#0000114&#0000116&#0000040&#0000039&#0000088&#0000083&#0000083&#0000039&#0000041", "https://example.com/javascript:alert('XSS')" },
        { "\\j\\av\\a\\s\\cript:alert()", "/j/av/a/s/cript:alert()" },
        { "not_javascript:alert(document.domain)", "not_javascript:alert(document.domain)" },
        { "not_data:alert(document.domain)", "not_data:alert(document.domain)" },
        { "not_vbscript:alert(document.domain)", "not_vbscript:alert(document.domain)" },
        { "http://example.com#javascript:foo", "http://example.com#javascript:foo" },
        { "http://example.com#data:foo", "http://example.com#data:foo" },
        { "http://example.com#vbscript:foo", "http://example.com#vbscript:foo" },
        { "google.com", "google.com" },
        { "/relative/page", "/relative/page" },
        { "mailto:someone@example.com", "mailto:someone@example.com" },
        { "MAILTO:Test@Example.com", "MAILTO:Test@Example.com" },
        { "tel:+1-555", "tel:+1-555" },
        { "ftp://example.com/x", "ftp://example.com/x" },
        { "foo\\bar:baz", "foo/bar:baz" },
        { "./a", "./a" },
        { "../a", "../a" },
        { "?q=1", "?q=1" },
        { "#frag", "#frag" },
        { "x", "x" },
        { "a:b:c", "a:b:c" },
    };

    /// <summary>
    ///     Gets encoded links whose decoding follows JavaScript exactly, with sanitize-url's results in Node: a reference
    ///     word that is not a number gives U+0000 (and is removed), numbers wrap at 65536, malformed or invalid UTF-8
    ///     percent escapes keep the text, <c>toLowerCase</c> and <c>\w</c> are those of JavaScript.
    /// </summary>
    public static TheoryData<string, string> EncodedUrls => new()
    {
        { "&#1.06e2;x", ".06e2;x" },
        { "java&#09script:alert(1)", "java:alert(1)" },
        { "javascript&#58alert(1)", "javascript(1)" },
        { "javascript&#x3A;alert(1)", "javascriptalert(1)" },
        { "&#x6A;avascript:alert(1)", "avascript:alert(1)" },
        { "&#106avascript:alert(1)", ":alert(1)" },
        { "&#106_;avascript:alert(1)", "avascript:alert(1)" },
        { "&#1e2;", "d" },
        { "&#1E400;x", "x" },
        { "&#00x6A;avascript:alert(1)", "avascript:alert(1)" },
        { "&#Infinity;x", "x" },
        { "&#99999999999999999999999;x", "x" },
        { "&#0x1000000000000006A;avascript:alert(1)", "avascript:alert(1)" },
        { "&#0x20000000000006A;x", "`x" },
        { "&#0x;x", "x" },
        { "&#;javascript:alert(1)", "about:blank" },
        { "%256Aavascript:alert(1)", "about:blank" },
        { "&amp;#106;avascript:alert(1)", "&amp;#106;avascript:alert(1)" },
        { "java\u00ADscript:alert(1)", "java\u00ADscript:alert(1)" },
        { "\u0130javascript:alert(1)", "\u0130javascript:alert(1)" },
        { "JAVASCR\u0130PT:alert(1)", "JAVASCR\u0130PT:alert(1)" },
        { "\u212Aavascript:x", "\u212Aavascript:x" },
        { "%E0%A4%A", "%E0%A4%A" },
        { "%C0%AFjavascript:alert(1)", "%C0%AFjavascript:alert(1)" },
        { "%ED%A0%80x:y", "%ED%A0%80x:y" },
        { "%F4%90%80%80x:y", "%F4%90%80%80x:y" },
        { "%E0%80%AFx:y", "%E0%80%AFx:y" },
        { "%F0%9F%98%80x:y", "\uD83D\uDE00x:y" },
        { "%e2%82%acx:y", "\u20ACx:y" },
        { "&#55357;&#56832;x:y", "\uD83D\uDE00x:y" },
        { "\uD800x:y", "\uD800x:y" },
        { "java script:alert(1)", "java script:alert(1)" },
        { "javascript :alert(1)", "about:blank" },
        { "javascript&colon alert(1)", "javascript&colon alert(1)" },
        { "'javascript:alert(1)'", "about:blank" },
        { "<javascript:alert(1)>", "about:blank" },
    };

    /// <summary>
    ///     Gets <c>http</c> and <c>https</c> links and how sanitize-url writes them back through the WHATWG URL parser
    ///     (sanitize-url's results in Node, except where noted): host case, IDNA, IPv4 number forms, IPv6 compression, user
    ///     information, default ports, dot segments, percent-encode sets, and the links that do not parse.
    /// </summary>
    public static TheoryData<string, string> HttpUrls => new()
    {
        { "https://Example.com", "https://example.com/" },
        { "HTTP://EXAMPLE.COM/A?B#C", "http://example.com/A?B#C" },
        { "http://example.com/a b", "http://example.com/a%20b" },
        { "http://example.com/\u00E4", "http://example.com/%C3%A4" },
        { "https://b\u00FCcher.de/", "https://xn--bcher-kva.de/" },
        { "http://xn--BCHER-KVA.de/", "http://xn--bcher-kva.de/" },
        { "http://0x7f.1/", "http://127.0.0.1/" },
        { "http://127.1/", "http://127.0.0.1/" },
        { "http://0300.0250.0.1/", "http://192.168.0.1/" },
        { "http://4294967295/", "http://255.255.255.255/" },
        { "http://example.com/./a/../b", "http://example.com/b" },

        // sanitize-url's greedy scheme pattern /^.+(:|&colon;)/ stops at the LAST colon on the line, so a port makes the
        // "scheme" contain "://" and the text is returned as written, not parsed (sanitize-url 7.1.2, checked with node)
        { "http://EXAMPLE.COM:80/a b", "http://EXAMPLE.COM:80/a b" },
        { "https://Example.com:8443/x", "https://Example.com:8443/x" },
        { "http://x.com:99999/", "http://x.com:99999/" },
        { "http://example.com/%2e%2E/c", "http://example.com/c" },
        { "http:example.com/x", "http://example.com/x" },
        { "http:/example.com", "http://example.com/" },
        { "http:///example.com", "http://example.com/" },
        { "http://", "about:blank" },
        { "http:", "about:blank" },
        { "http://a@b@c/", "http://a%40b@c/" },
        { "http://@host/", "http://host/" },
        { "http://user@/", "about:blank" },
        { "http://us er@host/", "http://us%20er@host/" },
        { "http://u^@a/", "http://u%5E@a/" },
        { "http://exa mple.com", "about:blank" },
        { "http://example.com/?q='x'", "http://example.com/?q=%27x%27" },
        { "http://example.com/#frag `x`", "http://example.com/#frag%20%60x%60" },
        { "http://example.com/^{}|", "http://example.com/^%7B%7D|" },
        { "http://example.com/?a=<b>&c=\"d\"", "http://example.com/?a=%3Cb%3E&c=%22d%22" },
        { "http://example.com/path\u2028:x", "http://example.com/path%E2%80%A8:x" },
        { "http://ex%ample.com/%", "about:blank" },
        { "http://ex%41mple.com/%", "http://example.com/%" },
        { "http://xn--a.com/", "about:blank" },
        { "http://256.256.256.256/", "about:blank" },
        { "http://1.2.3.4.5/", "about:blank" },
        { "http://0x100000000/", "about:blank" },
        { "http://1.2.65536/", "about:blank" },
        { "http://foo.09/", "about:blank" },
        { "http://0x/", "http://0.0.0.0/" },
        { "http://example.com./", "http://example.com./" },
        { "http://a..b/", "http://a..b/" },
        { "http://a%00b.com/", "http://ab.com/" },
        { "http://\uD83D\uDE00.com/", "http://xn--e28h.com/" },
        { "https://example.com/\uD800", "https://example.com/%EF%BF%BD" },
        { "http://example.com\\path\\x", "http://example.com/path/x" },
        { "http://example.com/a/b/..", "http://example.com/a/" },
        { "http://example.com/a/%2e", "http://example.com/a/" },
        { "http://example.com?x", "http://example.com/?x" },
        { "http://example.com#y", "http://example.com/#y" },
        { "http://a\u2028@[::1]/", "http://a%E2%80%A8@[::1]/" },
        { "http://a\u2028b:c@host/x", "http://a%E2%80%A8b:c@host/x" },
        { "http://h\u2028@h:8080/", "http://h%E2%80%A8@h:8080/" },
        { "http://h\u2028@h:80/", "http://h%E2%80%A8@h/" },
        { "https://h\u2028@h:443/x", "https://h%E2%80%A8@h/x" },
        { "http://h\u2028@[1:0:0:0:0:0:0:1]/", "http://h%E2%80%A8@[1::1]/" },
        { "http://h\u2028@[::ffff:1.2.3.4]/", "http://h%E2%80%A8@[::ffff:102:304]/" },
        { "http://h\u2028@[0:0:1::]/", "http://h%E2%80%A8@[0:0:1::]/" },
        { "http://h\u2028@[::]/", "http://h%E2%80%A8@[::]/" },
        { "http://h\u2028@[1::2:0:0:3]/", "http://h%E2%80%A8@[1::2:0:0:3]/" },
        { "http://h\u2028@[::1.2.3]/", "about:blank" },
        { "http://h\u2028@[1:2:3:4:5:6:7:8:9]/", "about:blank" },
        { "http://h\u2028@h:99999/", "about:blank" },
        { "http://h\u2028@h:/x", "http://h%E2%80%A8@h/x" },
        { "http://h\u2028@h:0080/", "http://h%E2%80%A8@h/" },
        { "http://h\u2028@h:8a/", "about:blank" },
        { "http://[::1]/", "http://[::1]/" },

        // Node's URL (ada) keeps this final "/." (a shortcut of its path parser); the WHATWG standard and Chromium resolve it
        { "http://a/y/.x/.", "http://a/y/.x/" },
    };


    /// <summary>
    ///     Validates that every encoded form of a <c>javascript</c>, <c>data</c> or <c>vbscript</c> link that sanitize-url
    ///     refuses is refused: the result is <c>about:blank</c>.
    /// </summary>
    /// <param name="url">The link.</param>
    [Theory]
    [MemberData(nameof(UnsafeUrls))]
    public void UrlSanitizer_Sanitize_UnsafeScheme_IsBlank(string url)
    {
        // Act
        var actual = UrlSanitizer.Sanitize(url);

        // Assert
        Assert.Equal(UrlSanitizer.BlankUrl, actual);
    }

    /// <summary>
    ///     Validates that a safe link, an encoded link and an <c>http</c> or <c>https</c> link give sanitize-url's exact
    ///     result: JavaScript's decoding, and the WHATWG URL parser's writing (or <c>about:blank</c> when it does not
    ///     parse).
    /// </summary>
    /// <param name="url">The link.</param>
    /// <param name="expected">sanitize-url's result.</param>
    [Theory]
    [MemberData(nameof(SafeUrls))]
    [MemberData(nameof(EncodedUrls))]
    [MemberData(nameof(HttpUrls))]
    public void UrlSanitizer_Sanitize_Url_GivesSanitizeUrlResult(string url, string expected)
    {
        // Act
        var actual = UrlSanitizer.Sanitize(url);

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates that no link (null or empty) is <c>about:blank</c>, as in sanitize-url, and that Mermaid's
    ///     <c>formatUrl</c> gives no link for an empty or blank one and checks any other after trimming it.
    /// </summary>
    [Fact]
    public void UrlSanitizer_FormatUrl_EmptyAndUnsafeLinks_FollowMermaid()
    {
        // Act
        var none = UrlSanitizer.Sanitize(null);
        var empty = UrlSanitizer.Sanitize(string.Empty);
        var blank = UrlSanitizer.FormatUrl(" \t ");
        var unsafeLink = UrlSanitizer.FormatUrl("  java&#9;script:alert(1)  ");
        var safeLink = UrlSanitizer.FormatUrl("  https://Example.com  ");

        // Assert
        Assert.Equal((UrlSanitizer.BlankUrl, UrlSanitizer.BlankUrl), (none, empty));
        Assert.Null(blank);
        Assert.Equal((UrlSanitizer.BlankUrl, "https://example.com/"), (unsafeLink, safeLink));
    }

    /// <summary>
    ///     Validates that a deeply nested reference (each pass of the decoding loop removes one level) is decoded to the
    ///     end and refused, in bounded time.
    /// </summary>
    [Fact]
    public void UrlSanitizer_Sanitize_DeeplyNestedReference_IsDecodedAndRefused()
    {
        // Arrange: &#38; decodes to &, so each level hides the next one until &#106; (j) appears
        var url = "&#38;" + string.Concat(Enumerable.Repeat("#38;", 1000)) + "#106;avascript:alert(1)";

        // Act
        var actual = UrlSanitizer.Sanitize(url);

        // Assert
        Assert.Equal(UrlSanitizer.BlankUrl, actual);
    }
}
