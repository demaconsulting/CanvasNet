using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Subsystem tests for the internal <c>ScadLanguage</c> subsystem: the lexer and parser working together
///     through <see cref="ScadParser"/> on realistic programs and hostile input.
/// </summary>
public class ScadLanguageTests
{
    /// <summary>A small program used to compare line-ending variants.</summary>
    private static readonly string[] ProgramLines =
    [
        "// header",
        "module bracket(w = 10, h = 20) {",
        "  difference() {",
        "    cube([w, h, 3]);",
        "    translate([w / 2, h / 2, -1]) cylinder(r = 2, h = 5);",
        "  }",
        "}",
        "/* block",
        "   comment */",
        "bracket(w = 12);",
    ];

    /// <summary>
    ///     Validates that the same program with different line endings yields identical trees and locations.
    /// </summary>
    [Fact]
    public void ScadLanguage_Parse_LineEndingVariants_ProduceIdenticalTrees()
    {
        // Arrange / Act: parse the program with each line-ending convention
        var variants = new[] { "\n", "\r\n", "\r" }
            .Select(newline => ScadParser.Parse(string.Join(newline, ProgramLines), "p.scad"))
            .ToList();

        // Assert: identical dumps and identical statement locations
        var dumps = variants.Select(ScadAstDumper.Dump).Distinct().ToList();
        var locations = variants.Select(p => string.Join(';', p.Statements.Select(s => s.Location.ToString()))).Distinct().ToList();
        Assert.Single(dumps);
        Assert.Single(locations);
        Assert.Equal("p.scad:2:1;p.scad:10:1", locations[0]);
    }

    /// <summary>
    ///     Validates that a lexical error inside a statement surfaces as a located syntax exception.
    /// </summary>
    [Fact]
    public void ScadLanguage_Parse_LexicalErrorInsideStatement_SurfacesAsSyntaxExceptionWithLocation()
    {
        // Arrange / Act / Assert
        ScadTestHelpers.AssertSyntaxError(
            () => ScadParser.Parse("x = 1;\ny = 2 @ 3;", "s.scad"),
            2,
            7,
            "Unexpected character '@'",
            "s.scad");
    }

    /// <summary>
    ///     Validates the documented consequence of tokenizing first: a lexical error later in the file is
    ///     reported before a grammatical error earlier in the file.
    /// </summary>
    [Fact]
    public void ScadLanguage_Parse_LexicalErrorAfterGrammarError_IsReportedFirst()
    {
        // Arrange: the grammar error is on line 1, the lexical error on line 2
        const string Source = "x = ;\ny = @;";

        // Act / Assert
        ScadTestHelpers.AssertSyntaxError(() => ScadParser.Parse(Source, null), 2, 5, "Unexpected character '@'");
    }

    /// <summary>
    ///     Validates that tokenizing and then parsing is equivalent to parsing the source directly.
    /// </summary>
    [Fact]
    public void ScadLanguage_TokenizeThenParse_Program_MatchesParseOfSource()
    {
        // Arrange
        var source = string.Join('\n', ProgramLines);

        // Act
        var direct = ScadParser.Parse(source, "p.scad");
        var staged = ScadParser.Parse(ScadLexer.Tokenize(source, "p.scad"));

        // Assert
        Assert.Equal(ScadAstDumper.Dump(direct), ScadAstDumper.Dump(staged));
        Assert.Equal(direct.Location, staged.Location);
    }

    /// <summary>
    ///     Validates that every authored fixture parses.
    /// </summary>
    [Fact]
    public void ScadLanguage_Parse_AllAuthoredFixtures_Succeed()
    {
        // Arrange
        var fixtures = ScadFixtureHelpers.ValidFixtures();

        // Act: parse each fixture with its path as the source name
        var programs = fixtures.Select(path => ScadParser.Parse(File.ReadAllText(path), path)).ToList();

        // Assert: all valid fixtures (ten parse fixtures and sixteen golden evaluation fixtures) parse into non-empty programs
        Assert.Equal(26, fixtures.Count);
        Assert.All(programs, p => Assert.NotEmpty(p.Statements));
    }

    /// <summary>
    ///     Validates that tokenizing and then parsing a fixture equals parsing its source directly.
    /// </summary>
    [Fact]
    public void ScadLanguage_TokenizeThenParse_Fixture_MatchesParseOfSource()
    {
        // Arrange / Act / Assert: for every valid fixture
        foreach (var path in ScadFixtureHelpers.ValidFixtures())
        {
            var source = File.ReadAllText(path);
            var direct = ScadParser.Parse(source, path);
            var staged = ScadParser.Parse(ScadLexer.Tokenize(source, path));
            Assert.Equal(ScadAstDumper.Dump(direct), ScadAstDumper.Dump(staged));
        }
    }

    /// <summary>
    ///     Validates that converting every fixture to CRLF or CR line endings leaves the tree unchanged.
    /// </summary>
    [Fact]
    public void ScadLanguage_Parse_FixtureLineEndingConversion_ProducesIdenticalTrees()
    {
        // Arrange / Act / Assert: normalize to LF first (the working copy may have either convention)
        foreach (var path in ScadFixtureHelpers.ValidFixtures())
        {
            var lf = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var expected = ScadAstDumper.Dump(ScadParser.Parse(lf, null));
            Assert.Equal(expected, ScadAstDumper.Dump(ScadParser.Parse(lf.Replace("\n", "\r\n", StringComparison.Ordinal), null)));
            Assert.Equal(expected, ScadAstDumper.Dump(ScadParser.Parse(lf.Replace('\n', '\r'), null)));
        }
    }

    /// <summary>
    ///     Validates that the include and use fixture records its directives in source order.
    /// </summary>
    [Fact]
    public void ScadLanguage_Parse_IncludeAndUseFixture_RecordsDirectivesInOrder()
    {
        // Arrange
        var path = ScadFixtureHelpers.PathOf("include-and-use.scad");

        // Act
        var directives = ScadParser.Parse(File.ReadAllText(path), path).Statements.OfType<ScadDirectiveStatement>().ToList();

        // Assert: kinds, paths and locations in source order
        Assert.Equal(
            ["Include parts/frames.scad 3", "Use lib/gears.scad 4", "Include ./relative/../nested/deep/path/box.scad 5", "Use fasteners.scad 6"],
            directives.Select(d => $"{d.Kind} {d.Path} {d.Location.Line}"));
    }

    /// <summary>
    ///     Validates that excessive nesting fails closed with the typed exception.
    /// </summary>
    [Fact]
    public void ScadLanguage_Parse_DeepNesting_FailsClosedWithTypedException()
    {
        // Arrange: nesting far beyond the limit in several shapes
        var inputs = new[]
        {
            "x = " + ScadParserTestSupport.Repeat("(", 5000) + "1" + ScadParserTestSupport.Repeat(")", 5000) + ";",
            ScadParserTestSupport.Repeat("a() { ", 5000) + ScadParserTestSupport.Repeat("}", 5000),
            "x = 1" + ScadParserTestSupport.Repeat("+1", 5000) + ";",
        };

        // Act / Assert: each is a ScadSyntaxException with the limit in its message
        foreach (var source in inputs)
        {
            var ex = Assert.Throws<ScadSyntaxException>(() => ScadParser.Parse(source, null));
            Assert.Contains("Nesting too deep", ex.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Validates that hostile nesting on a 1 MiB stack neither overflows nor escapes as another exception type.
    /// </summary>
    [Fact]
    public void ScadLanguage_Parse_DeepNesting_OnSmallStackThread_DoesNotOverflow()
    {
        // Arrange: nesting 400x beyond the limit
        var source = "x = " + ScadParserTestSupport.Repeat("[(", 50000) + "1" + ScadParserTestSupport.Repeat(")]", 50000) + ";";
        Exception? error = null;

        // Act
        var thread = new Thread(() => error = Record.Exception(() => ScadParser.Parse(source, null)), 1024 * 1024);
        thread.Start();
        thread.Join();

        // Assert
        Assert.IsType<ScadSyntaxException>(error);
    }
}
