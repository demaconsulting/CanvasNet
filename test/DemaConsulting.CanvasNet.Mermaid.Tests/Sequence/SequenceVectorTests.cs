using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Sequence;
using DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Sequence;

/// <summary>
///     The upstream vectors of the sequence parser and model: every case of <c>sequence-parser.json</c>, recorded from
///     the pinned Mermaid by <c>gen-sequence-vectors.mjs</c>, must give exactly Mermaid's database facts, or fail where
///     Mermaid fails.
/// </summary>
public sealed class SequenceVectorTests
{
    /// <summary>The beginnings of the messages of the errors that Mermaid's sequence database throws.</summary>
    private static readonly string[] DatabaseErrors =
    [
        "A same participant ", "It is not possible ", "The created participant ", "The destroyed participant ", "Trying to inactivate ",
    ];

    /// <summary>
    ///     Gets the indexes of the vector cases.
    /// </summary>
    public static TheoryData<int> Cases => UpstreamVectors.Indexes(UpstreamVectors.Sequences);

    /// <summary>
    ///     Checks that the file holds the cases the plan asks for: at least 150, with valid and invalid texts.
    /// </summary>
    [Fact]
    public void SequenceParser_Vectors_FileHasEnoughCases_AtLeast150()
    {
        // Arrange
        var cases = UpstreamVectors.Sequences;

        // Act
        var errors = cases.Count(c => c!["expected"]!["error"] is not null);

        // Assert
        Assert.True(cases.Count >= 150, $"only {cases.Count} cases");
        Assert.InRange(errors, 20, cases.Count - 100);
    }

    /// <summary>
    ///     Parses one vector case and compares the result with what Mermaid recorded.
    /// </summary>
    /// <param name="index">The case index.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void SequenceParser_Parse_UpstreamVector_MatchesMermaid(int index)
    {
        // Arrange
        var vector = UpstreamVectors.Sequences[index]!.AsObject();
        var name = (string)vector["name"]!;
        var text = (string)vector["text"]!;
        var site = vector["site"]?.AsObject() ?? [];
        var expected = vector["expected"]!.AsObject();

        // Act
        Exception? failure = null;
        JsonObject? actual = null;
        try
        {
            var model = SequenceFacts.Parse(text);
            var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(site, text);
            actual = SequenceFacts.Of(model, config.Sequence);
        }
        catch (MermaidSyntaxException exception)
        {
            failure = exception;
        }

        // Assert
        if (expected["error"] is JsonObject error)
        {
            var exception = Assert.IsType<MermaidSyntaxException>(failure);
            AssertSameFailure(name, error, exception);
            return;
        }

        Assert.True(failure is null, $"{name}: {failure?.Message}");
        var difference = UpstreamVectors.Difference(expected, actual);
        Assert.True(difference is null, $"{name}: {difference}");
    }

    /// <summary>
    ///     Asserts that a parse error is the error Mermaid reports: for a grammar error, the same unexpected token and the
    ///     same expected tokens; for an error of Mermaid's database, the same message.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <param name="error">Mermaid's error.</param>
    /// <param name="exception">The exception.</param>
    private static void AssertSameFailure(string name, JsonObject error, MermaidSyntaxException exception)
    {
        var message = (string)error["message"]!;
        Assert.True(exception.Location.IsKnown, $"{name}: {exception.Message}");
        if (message.StartsWith("Lexical error", StringComparison.Ordinal))
        {
            Assert.Contains("unrecognized text", exception.Message, StringComparison.Ordinal);
            return;
        }

        // An error of Mermaid's database has Mermaid's own message
        if (DatabaseErrors.Any(prefix => message.StartsWith(prefix, StringComparison.Ordinal)))
        {
            Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
            Assert.EndsWith(": " + message, exception.Message, StringComparison.Ordinal);
            return;
        }

        // Any other error without a line is js-yaml's, for invalid participant metadata
        if (error["line"] is null)
        {
            Assert.Equal(MermaidDiagnosticCodes.YamlInvalid, exception.Code);
            return;
        }

        // A grammar error: the same unexpected token (jison's '1' is the end of the tokens)
        var expectedToken = (string?)error["token"] switch
        {
            "1" => "the end of the text",
            var kind => $"'{kind}'",
        };
        Assert.True(
            exception.Message.Contains("found " + expectedToken, StringComparison.Ordinal),
            $"{name}: expected the token {expectedToken}, got: {exception.Message}");

        // And the same expected tokens, in jison's order
        var jison = error["expected"]!.AsArray().Select(e => (string)e!).ToList();
        Assert.Equal(jison, exception.Expected);
    }
}
