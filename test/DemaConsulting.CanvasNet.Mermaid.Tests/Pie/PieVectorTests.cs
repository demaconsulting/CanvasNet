using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Pie;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Pie;

/// <summary>
///     The upstream vectors of the pie parser: every case of <c>pie-parser.json</c>, recorded from the pinned Mermaid (its
///     Langium parser and pie database) by <c>gen-diagram-vectors.mjs --type pie</c>, must give exactly Mermaid's database
///     facts, or fail where Mermaid fails first, with the same kind of error, at the same place, expecting the same tokens.
/// </summary>
public sealed class PieVectorTests
{
    /// <summary>
    ///     Gets the indexes of the vector cases.
    /// </summary>
    public static TheoryData<int> Cases => UpstreamVectors.Indexes(UpstreamVectors.Pies);

    /// <summary>
    ///     Checks that the file holds enough cases: at least 120, with the upstream specs, valid and invalid texts.
    /// </summary>
    [Fact]
    public void PieParser_Vectors_FileHasEnoughCases_AtLeast120()
    {
        // Arrange
        var cases = UpstreamVectors.Pies;

        // Act
        var errors = cases.Count(c => c!["expected"]!["error"] is not null);
        var upstream = cases.Count(c => ((string)c!["spec"]!).Contains(".ts:", StringComparison.Ordinal));

        // Assert
        Assert.True(cases.Count >= 120, $"only {cases.Count} cases");
        Assert.True(upstream >= 30, $"only {upstream} upstream cases");
        Assert.InRange(errors, 30, cases.Count - 80);
    }

    /// <summary>
    ///     Parses one vector case and compares the result with what Mermaid recorded.
    /// </summary>
    /// <param name="index">The case index.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void PieParser_Parse_UpstreamVector_MatchesMermaid(int index)
    {
        // Arrange
        var vector = UpstreamVectors.Pies[index]!.AsObject();
        var name = (string)vector["name"]!;
        var text = (string)vector["text"]!;
        var expected = vector["expected"]!.AsObject();
        var source = Preprocessor.Run(text, null, MermaidLimits.Default);

        // Act
        Exception? failure = null;
        JsonObject? actual = null;
        try
        {
            actual = Facts(PieParser.Parse(source, DiagramDetector.Detect(source.Code, DetectorSettings.Default), MermaidLimits.Default));
        }
        catch (MermaidSyntaxException exception)
        {
            failure = exception;
        }

        // Assert
        if (expected["error"] is JsonObject error)
        {
            var exception = Assert.IsType<MermaidSyntaxException>(failure);
            AssertSameFailure(name, (string)error["message"]!, exception, source);
            return;
        }

        Assert.True(failure is null, $"{name}: {failure?.Message}");
        var difference = UpstreamVectors.Difference(expected, actual);
        Assert.True(difference is null, $"{name}: {difference}");
    }

    /// <summary>
    ///     Writes the facts of a diagram as the generator records them from Mermaid's pie database (the generator's
    ///     <c>getDiagramFromText</c> passes no frontmatter title, which only a render sets).
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <returns>The facts.</returns>
    private static JsonObject Facts(PieDiagram diagram)
    {
        var sections = new JsonArray();
        foreach (var section in diagram.Sections)
        {
            sections.Add(new JsonObject
            {
                ["label"] = section.Label,
                ["value"] = YamlSubsetParser.FormatJsNumber(section.Value),
            });
        }

        return new JsonObject
        {
            ["type"] = "pie",
            ["title"] = diagram.DiagramTitle,
            ["accTitle"] = diagram.AccessibleTitle,
            ["accDescr"] = diagram.AccessibleDescription,
            ["showData"] = diagram.ShowData,
            ["sections"] = sections,
        };
    }

    /// <summary>
    ///     Asserts that an error is the first error Mermaid reports: a database error with Mermaid's message, or a lexer or
    ///     parse error at the same line and column of the parser text, a parse error with the same expected tokens.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <param name="message">Mermaid's error message.</param>
    /// <param name="exception">The exception.</param>
    /// <param name="source">The preprocessed source.</param>
    private static void AssertSameFailure(string name, string message, MermaidSyntaxException exception, PreprocessedSource source)
    {
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
        Assert.True(exception.Location.IsKnown, $"{name}: {exception.Message}");
        const string Prefix = "Parsing failed:";
        if (!message.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // A database error: Mermaid's own message
            Assert.Contains(message, exception.Message, StringComparison.Ordinal);
            return;
        }

        // The first error: Mermaid lists the lexer's errors before the parser's
        var first = message[Prefix.Length..].TrimStart();
        var at = first.IndexOf(" on line ", StringComparison.Ordinal);
        var comma = first.IndexOf(", column ", at, StringComparison.Ordinal);
        var colon = first.IndexOf(':', comma);
        var line = int.Parse(first[(at + " on line ".Length)..comma], CultureInfo.InvariantCulture);
        var column = int.Parse(first[(comma + ", column ".Length)..colon], CultureInfo.InvariantCulture);
        var expectedLocation = source.ParserText.Locate(OffsetOf(source.ParserText.Text, line, column));
        Assert.True(
            (expectedLocation.Line, expectedLocation.Column) == (exception.Location.Line, exception.Location.Column),
            $"{name}: expected the error at {expectedLocation.Line}:{expectedLocation.Column}, got {exception.Location.Line}:{exception.Location.Column}: {exception.Message}");
        if (first.StartsWith("Lexer error", StringComparison.Ordinal))
        {
            Assert.Contains("unrecognized text", exception.Message, StringComparison.Ordinal);
            return;
        }

        // A parse error: the same expected tokens
        Assert.Equal(ExpectedTokens(first[colon..]), exception.Expected);
    }

    /// <summary>
    ///     Gets the offset of a 1-based line and column in a text whose lines end with line feeds.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="line">The line.</param>
    /// <param name="column">The column.</param>
    /// <returns>The offset.</returns>
    private static int OffsetOf(string text, int line, int column)
    {
        var offset = 0;
        for (var i = 1; i < line; i++)
        {
            offset = text.IndexOf('\n', offset) + 1;
        }

        return offset + column - 1;
    }

    /// <summary>
    ///     Reads the expected tokens of a chevrotain message: <c>Expecting token of type 'X'</c>, or the single-token
    ///     sequences of <c>Expecting: one of these possible Token sequences</c>.
    /// </summary>
    /// <param name="message">The message after the location.</param>
    /// <returns>The token names.</returns>
    private static List<string> ExpectedTokens(string message)
    {
        const string Single = "Expecting token of type '";
        var single = message.IndexOf(Single, StringComparison.Ordinal);
        if (single >= 0)
        {
            var start = single + Single.Length;
            return [message[start..message.IndexOf('\'', start)]];
        }

        var tokens = new List<string>();
        foreach (var row in message.Split('\n'))
        {
            var open = row.IndexOf(". [", StringComparison.Ordinal);
            if (open >= 0)
            {
                tokens.Add(row[(open + 3)..row.IndexOf(']', open)]);
            }
        }

        return tokens;
    }
}
