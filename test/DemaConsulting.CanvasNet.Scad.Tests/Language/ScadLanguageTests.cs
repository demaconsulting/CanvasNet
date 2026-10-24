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
