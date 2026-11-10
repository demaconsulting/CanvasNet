using DemaConsulting.CanvasNet.Mermaid.Pie;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Pie;

/// <summary>
///     Unit tests for <c>PieParser</c> (with <c>PieDiagram</c>): the tokens tried in chevrotain's order, the grammar, the
///     value converters, the database's negative values and repeated labels, the located errors with the expected tokens,
///     the metadata, and the limits on every kept and derived text.
/// </summary>
public sealed class PieParserTests
{
    /// <summary>
    ///     Validates that the sections are kept in order with their converted labels and numbers, and that the metadata
    ///     counts them.
    /// </summary>
    [Fact]
    public void PieParser_Parse_Sections_KeepsOrderLabelsAndValues()
    {
        // Arrange
        const string Text = "pie\n    \"Dogs\" : 386\n    'Cats' : 85.25\n    \"Rats\" : 0\n";

        // Act
        var diagram = Parse(Text);

        // Assert
        Assert.Equal([("Dogs", 386.0), ("Cats", 85.25), ("Rats", 0.0)], diagram.Sections.Select(s => (s.Label, s.Value)));
        Assert.Equal((3, 0, 0), (diagram.NodeCount, diagram.EdgeCount, diagram.SubgraphCount));
        Assert.Null(diagram.Direction);
        Assert.False(diagram.ShowData);
    }

    /// <summary>
    ///     Validates that a label seen before is ignored (the first value wins), as Mermaid's <c>addSection</c> does.
    /// </summary>
    [Fact]
    public void PieParser_Parse_RepeatedLabel_FirstValueWins()
    {
        // Act
        var diagram = Parse("pie\n\"x\" : 5\n\"y\" : 1\n\"x\" : 7\n");

        // Assert
        Assert.Equal([("x", 5.0), ("y", 1.0)], diagram.Sections.Select(s => (s.Label, s.Value)));
    }

    /// <summary>
    ///     Validates that a negative value fails with Mermaid's message, located at its section, even for a label seen
    ///     before.
    /// </summary>
    [Fact]
    public void PieParser_Parse_NegativeValue_FailsLocatedWithMermaidsMessage()
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("pie\n\"x\" : 5\n  \"x\" : -7.5\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
        Assert.Equal((3, 3), (exception.Location.Line, exception.Location.Column));
        Assert.Contains("\"x\" has invalid value: -7.5. Negative values are not allowed in pie charts. All slice values must be >= 0.", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the header: <c>showData</c>, and a title on the <c>pie</c> line whose spaces are collapsed and whose
    ///     <c>%%</c> comment is left out.
    /// </summary>
    [Fact]
    public void PieParser_Parse_ShowDataAndTitle_AreRead()
    {
        // Act
        var diagram = Parse("pie showData title   A   pie  %% not this\n\"a\" : 1\n");

        // Assert
        Assert.True(diagram.ShowData);
        Assert.Equal("A pie", diagram.Title);
        Assert.Equal("a [1]", diagram.Sections[0].LegendText(diagram.ShowData));
    }

    /// <summary>
    ///     Validates the accessibility texts: the title trimmed, the single-line description, and the multi-line
    ///     description with its lines trimmed and its blank lines removed.
    /// </summary>
    [Fact]
    public void PieParser_Parse_AccessibilityTexts_AreConverted()
    {
        // Act
        var single = Parse("pie\naccTitle:   The   title  \naccDescr: one   two\n");
        var multi = Parse("pie\naccDescr {\n    first   line   \n\n\n    second line\n  }\n");

        // Assert
        Assert.Equal(("The title", "one two"), (single.AccTitle, single.AccessibleDescription));
        Assert.Equal("first line\nsecond line", multi.AccessibleDescription);
        Assert.Null(multi.AccTitle);
    }

    /// <summary>
    ///     Validates Langium's string conversion: the quotes removed, the known escapes replaced and any other escaped
    ///     character kept.
    /// </summary>
    [Fact]
    public void PieParser_ConvertString_Escapes_FollowLangium()
    {
        // Act
        var value = PieParser.ConvertString("\"a\\nb\\tc\\q\\\\d\\\"e\\0\"");

        // Assert
        Assert.Equal("a\nb\tcq\\d\"e\0", value);
    }

    /// <summary>
    ///     Validates the conversion of a multi-line description, line by line.
    /// </summary>
    /// <param name="input">The text in the braces.</param>
    /// <param name="expected">The value.</param>
    [Theory]
    [InlineData("\n  x\n  ", "x")]
    [InlineData("  a  \n\n  b\t\tc  \n", "a\nb c")]
    [InlineData("one line", "one line")]
    [InlineData("a\r\n\r\nb", "a\rb")]
    public void PieParser_ConvertMultiLine_Lines_AreTrimmedAndJoined(string input, string expected)
    {
        // Act
        var value = PieParser.ConvertMultiLine(input);

        // Assert
        Assert.Equal(expected, value);
    }

    /// <summary>
    ///     Validates the grammar errors: the token the grammar does not accept, located, with the token names chevrotain
    ///     lists.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="line">The line of the error.</param>
    /// <param name="column">The column of the error.</param>
    /// <param name="expected">The expected tokens, separated by spaces.</param>
    [Theory]
    [InlineData("pie\n\"a\" 1\n", 2, 5, ":")]
    [InlineData("pie\n\"a\" :\n", 2, 6, "NUMBER_PIE")]
    [InlineData("pie\n\"a\" : 1 \"b\" : 2\n", 2, 9, "NEWLINE EOF")]
    [InlineData("pie\n: 5\n", 2, 1, "EOF")]
    [InlineData("pie showData showData\n", 1, 14, "EOF")]
    public void PieParser_Parse_GrammarError_IsLocatedWithTheExpectedTokens(string text, int line, int column, string expected)
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
        Assert.Equal((line, column), (exception.Location.Line, exception.Location.Column));
        Assert.Equal(expected.Split(' '), exception.Expected);
        Assert.NotNull(exception.Hint);
    }

    /// <summary>
    ///     Validates the lexical errors, which come before any grammar error: a number the terminals do not match, a word
    ///     that is no keyword, and an unquoted label.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="line">The line of the error.</param>
    /// <param name="column">The column of the error.</param>
    [Theory]
    [InlineData("pie\n\"a\" : 1e3\n", 2, 8)]
    [InlineData("pie\n\"a\" : .5\n", 2, 7)]
    [InlineData("pie showdata\n\"a\" : 1\n", 1, 5)]
    [InlineData("pie\nlabel : 1\n", 2, 1)]
    public void PieParser_Parse_LexicalError_IsLocatedAtTheFirstUnmatchedCharacter(string text, int line, int column)
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse(text));

        // Assert
        Assert.Equal((line, column), (exception.Location.Line, exception.Location.Column));
        Assert.Contains("unrecognized text", exception.Message, StringComparison.Ordinal);
        Assert.Empty(exception.Expected);
    }

    /// <summary>
    ///     Validates the number terminals: a decimal, a zero, a leading-zero number split in two, and a lexer that does
    ///     not take a trailing dot.
    /// </summary>
    [Fact]
    public void PieParser_Parse_Numbers_FollowTheTerminals()
    {
        // Act
        var diagram = Parse("pie\n\"a\" : 10.500\n\"b\" : 0\n\"c\" : -0\n");
        var split = Assert.Throws<MermaidSyntaxException>(() => Parse("pie\n\"a\" : 007\n"));

        // Assert: "007" is 0 then 07, which the line end does not accept
        Assert.Equal([10.5, 0.0, 0.0], diagram.Sections.Select(s => s.Value));
        Assert.True(double.IsNegative(diagram.Sections[2].Value));
        Assert.Equal(["NEWLINE", "EOF"], split.Expected);
    }

    /// <summary>
    ///     Validates that every section statement counts against <see cref="MermaidLimits.MaxNodes"/>, repeated labels
    ///     included.
    /// </summary>
    [Fact]
    public void PieParser_Parse_TooManySections_IsALocatedLimit()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNodes = 3 };

        // Act
        var accepted = Parse("pie\n\"a\" : 1\n\"a\" : 1\n\"b\" : 1\n", limits);
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("pie\n\"a\" : 1\n\"a\" : 1\n\"b\" : 1\n\"c\" : 1\n", limits));

        // Assert
        Assert.Equal(2, accepted.NodeCount);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(5, exception.Location.Line);
    }

    /// <summary>
    ///     Validates that the texts the sanitizer lengthens are checked against <see cref="MermaidLimits.MaxLabelLength"/>
    ///     after sanitizing, as derived texts.
    /// </summary>
    [Fact]
    public void PieParser_Parse_SanitizedTitleTooLong_IsALocatedLimit()
    {
        // Arrange: 30 characters that the sanitizer writes as 120
        var limits = new MermaidLimits { MaxLabelLength = 60 };

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("pie title " + new string('<', 30) + "\n", limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Contains("MermaidLimits.MaxLabelLength", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Parses a pie chart as the document does: preprocessing, detection, then the parser.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The diagram.</returns>
    private static PieDiagram Parse(string text, MermaidLimits? limits = null)
    {
        var effective = limits ?? MermaidLimits.Default;
        var source = Preprocessor.Run(text, null, effective);
        return PieParser.Parse(source, DiagramDetector.Detect(source.Code, DetectorSettings.Default), effective);
    }
}
