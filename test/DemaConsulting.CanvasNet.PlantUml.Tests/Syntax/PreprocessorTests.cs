using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Syntax;

/// <summary>
///     Unit tests for the <c>Preprocessor</c> unit: framing, comments, continued lines, the supported directives, the
///     reports of the unsupported ones, the limits and the location map.
/// </summary>
public class PreprocessorTests
{
    /// <summary>
    ///     Runs the preprocessor with the default limits.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The preprocessed source.</returns>
    private static PreprocessedSource Run(string text, PlantUmlLimits? limits = null) =>
        Preprocessor.Run(text, "test.puml", limits ?? PlantUmlLimits.Default);

    /// <summary>
    ///     Gets the non-empty statement texts of a run.
    /// </summary>
    /// <param name="source">The preprocessed source.</param>
    /// <returns>The texts.</returns>
    private static List<string> Texts(PreprocessedSource source) =>
        [.. source.Block.Statements.Select(s => s.Text).Where(t => t.Length > 0)];

    /// <summary>
    ///     Validates that a text without a tag is one diagram with no tag.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Run_BareText_IsOneDiagram()
    {
        // Arrange / Act
        var source = Run("A -> B\nB -> C\n");

        // Assert
        Assert.Null(source.Block.Tag);
        Assert.Equal(["A -> B", "B -> C"], Texts(source));
    }

    /// <summary>
    ///     Validates that the first block is the diagram: text before it is ignored, its tag is read in lowercase and
    ///     located.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Run_StartUmlBlock_TakesItsContent()
    {
        // Arrange / Act
        var source = Run("intro text\n  @StartUml name\nA -> B\n@enduml\ntrailing\n");

        // Assert
        Assert.Equal("uml", source.Block.Tag);
        Assert.Equal(new PlantUmlLocation("test.puml", 2, 3), source.Block.Location);
        Assert.Equal(["A -> B"], Texts(source));
        Assert.Empty(source.Reports);
    }

    /// <summary>
    ///     Validates that a block without an end tag is a located syntax error.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Run_MissingEnd_ThrowsLocated()
    {
        // Arrange / Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => Run("\n@startuml\nA -> B\n"));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(2, ex.Location.Line);
        Assert.Contains("@enduml", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a second block is reported once and not read.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Run_SecondBlock_ReportsMultipleDiagrams()
    {
        // Arrange / Act
        var source = Run("@startuml\nA -> B\n@enduml\n@startuml\nC -> D\n@enduml\n@startuml\n@enduml\n");

        // Assert
        Assert.Equal(["A -> B"], Texts(source));
        var report = Assert.Single(source.Reports);
        Assert.Equal((false, PlantUmlDiagnosticCodes.MultipleDiagrams, 4), (report.IsWarning, report.Code, report.Location.Line));
    }

    /// <summary>
    ///     Validates that line comments, block comments over several lines, and inline block comments are removed.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Run_Comments_AreRemoved()
    {
        // Arrange / Act
        var source = Run("' a comment\n  ' indented comment\nA -> B /' inline '/ : hi\n/' block\nstill\n'/\nB -> C\n");

        // Assert
        Assert.Equal(["A -> B  : hi", "B -> C"], Texts(source));
    }

    /// <summary>
    ///     Validates that a trailing backslash joins a line with the next one, keeping the location of every character.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Run_Continuation_JoinsLinesWithLocations()
    {
        // Arrange / Act
        var source = Run("A -> B : first \\\n  second\n");
        var statement = source.Block.Statements.Single(s => s.Text.Length > 0);

        // Assert
        Assert.Equal("A -> B : first   second", statement.Text);
        var second = statement.Text.IndexOf("second", StringComparison.Ordinal);
        Assert.Equal(new PlantUmlLocation("test.puml", 2, 3), statement.Locate(second));
        Assert.Equal(new PlantUmlLocation("test.puml", 1, 1), statement.Location);
    }

    /// <summary>
    ///     Validates that a macro replaces whole words only, once, and stops after <c>!undef</c>.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Run_Define_SubstitutesWholeWords()
    {
        // Arrange / Act
        var source = Run("!define Web Website\nClient -> Web : Webmaster\n!undef Web\nClient -> Web\n");

        // Assert
        Assert.Equal(["Client -> Website : Webmaster", "Client -> Web"], Texts(source));
        Assert.Empty(source.Reports);
    }

    /// <summary>
    ///     Validates the variables: quoted texts, numbers, other variables, and the conditional assignment.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Run_Variables_AreSubstituted()
    {
        // Arrange / Act
        var source = Run("!$who = \"Alice\"\n!$n = 3\n!$copy = $who\n!$who ?= \"Bob\"\n!$new ?= 'Carol'\n$who -> $new : $n $copy $unknown\n");

        // Assert
        Assert.Equal(["Alice -> Carol : 3 Alice $unknown"], Texts(source));
        Assert.Empty(source.Reports);
    }

    /// <summary>
    ///     Validates that a substituted character is located at the name it replaces.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Run_Substitution_LocatesInsertedText()
    {
        // Arrange / Act
        var source = Run("!$who = \"Alice\"\nX -> $who\n");
        var statement = source.Block.Statements.Single(s => s.Text.Length > 0);

        // Assert
        Assert.Equal("X -> Alice", statement.Text);
        Assert.Equal(new PlantUmlLocation("test.puml", 2, 6), statement.Locate(7));
    }

    /// <summary>
    ///     Validates the reports of the unsupported directives: a macro with parameters, an expression, a dropped block,
    ///     includes that are never followed (the standard library, a URL), a theme, a pragma (a warning), a dropped function
    ///     and a built-in function.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Run_UnsupportedDirectives_AreReportedAndDropped()
    {
        // Arrange
        var text = string.Join('\n',
            "!define F(x) x",
            "!$sum = 1 + 2",
            "!if %true()",
            "A -> B : inside",
            "!endif",
            "!include <C4/C4_Container>",
            "!includeurl https://example.com/x.puml",
            "!theme cerulean",
            "!pragma teoz true",
            "!function $f()",
            "!return 1",
            "!endfunction",
            "B -> C : %date()",
            string.Empty);

        // Act
        var source = Run(text);

        // Assert
        Assert.Equal(["B -> C : %date()"], Texts(source));
        Assert.Equal(
            [
                PlantUmlDiagnosticCodes.PreprocessorUnsupported,
                PlantUmlDiagnosticCodes.PreprocessorUnsupported,
                PlantUmlDiagnosticCodes.PreprocessorUnsupported,
                PlantUmlDiagnosticCodes.IncludeUnsupported,
                PlantUmlDiagnosticCodes.IncludeUnsupported,
                PlantUmlDiagnosticCodes.ThemeUnsupported,
                PlantUmlDiagnosticCodes.PragmaIgnored,
                PlantUmlDiagnosticCodes.PreprocessorUnsupported,
                PlantUmlDiagnosticCodes.PreprocessorUnsupported,
            ],
            source.Reports.Select(r => r.Code));
        Assert.True(source.Reports.Single(r => r.Code == PlantUmlDiagnosticCodes.PragmaIgnored).IsWarning);
        Assert.Equal([1, 2, 3, 6, 7, 8, 9, 10, 13], source.Reports.Select(r => r.Location.Line));
    }

    /// <summary>
    ///     Validates that dropped directive blocks nest only up to the nesting limit.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Run_DirectiveNestingOverLimit_Throws()
    {
        // Arrange
        var text = string.Concat(Enumerable.Repeat("!if 1\n", 4)) + string.Concat(Enumerable.Repeat("!endif\n", 4));

        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => Run(text, new PlantUmlLimits { MaxNestingDepth = 3 }));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Contains("MaxNestingDepth", ex.Message, StringComparison.Ordinal);
        Assert.Equal(4, ex.Location.Line);
    }

    /// <summary>
    ///     Validates the size limits: the original text (four times the limit) and the statements after substitution.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Run_SizeOverLimit_ThrowsInvalidData()
    {
        // Arrange
        var limits = new PlantUmlLimits { MaxTextSize = 40 };
        var comments = string.Concat(Enumerable.Repeat("'c\n", 10));
        var growing = "!define X 0123456789012345678901234567890123456789\nA -> B : X\n";

        // Act / Assert: comments do not count, a text over four times the limit is refused, growth is counted
        Assert.Equal(["A -> B"], Texts(Run(comments + "A -> B\n", limits)));
        Assert.Throws<InvalidDataException>(() => Run(new string(' ', 161), limits));
        Assert.Throws<InvalidDataException>(() => Run(growing, limits));
    }

    /// <summary>
    ///     Validates the line table: every line ending, and columns in UTF-16 code units.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_SourceLines_LocatesEveryLineEnding()
    {
        // Arrange
        var lines = new SourceLines("a\r\nb\rc\nd", "f");

        // Act / Assert
        Assert.Equal(4, lines.Count);
        Assert.Equal(new PlantUmlLocation("f", 2, 1), lines.Locate(3));
        Assert.Equal(new PlantUmlLocation("f", 3, 1), lines.Locate(5));
        Assert.Equal(new PlantUmlLocation("f", 4, 1), lines.Locate(7));
        Assert.Equal("b", lines.LineText(1));
    }
}
