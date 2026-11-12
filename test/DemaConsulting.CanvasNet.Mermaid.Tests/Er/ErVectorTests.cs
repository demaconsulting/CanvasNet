using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Er;

/// <summary>
///     The upstream vectors of the ER parser and model: every case of <c>er-parser.json</c>, recorded from the pinned
///     Mermaid by <c>gen-diagram-vectors.mjs --type er</c>, must give exactly Mermaid's database facts and the facts of its
///     <c>getData()</c>, or fail where Mermaid fails.
/// </summary>
public sealed class ErVectorTests
{
    /// <summary>
    ///     Gets the indexes of the vector cases.
    /// </summary>
    public static TheoryData<int> Cases => UpstreamVectors.Indexes(UpstreamVectors.Ers);

    /// <summary>
    ///     Checks that the file holds the cases the plan asks for: at least 110, with valid and invalid texts.
    /// </summary>
    [Fact]
    public void ErParser_Vectors_FileHasEnoughCases_AtLeast110()
    {
        // Arrange
        var cases = UpstreamVectors.Ers;

        // Act
        var errors = cases.Count(c => c!["expected"]!["error"] is not null);

        // Assert
        Assert.True(cases.Count >= 110, $"only {cases.Count} cases");
        Assert.InRange(errors, 10, cases.Count - 100);
    }

    /// <summary>
    ///     Parses one vector case and compares the result with what Mermaid recorded.
    /// </summary>
    /// <param name="index">The case index.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ErParser_Parse_UpstreamVector_MatchesMermaid(int index)
    {
        // Arrange
        var vector = UpstreamVectors.Ers[index]!.AsObject();
        var name = (string)vector["name"]!;
        var text = (string)vector["text"]!;
        var site = vector["site"]?.AsObject() ?? [];
        var expected = vector["expected"]!.AsObject();
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(site, text);

        // Act
        Exception? failure = null;
        JsonObject? actual = null;
        try
        {
            var model = ErFacts.Parse(text, settings: config.DetectorSettings);
            actual = ErFacts.Of(model, config);
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
    ///     Asserts that a parse error is the error Mermaid reports: for a lexical error, unrecognized text; for a grammar
    ///     error, the same unexpected token and the same expected tokens.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <param name="error">Mermaid's error.</param>
    /// <param name="exception">The exception.</param>
    private static void AssertSameFailure(string name, JsonObject error, MermaidSyntaxException exception)
    {
        var message = (string)error["message"]!;
        Assert.True(exception.Location.IsKnown, $"{name}: {exception.Message}");
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
        if (message.StartsWith("Lexical error", StringComparison.Ordinal))
        {
            Assert.Contains("unrecognized text", exception.Message, StringComparison.Ordinal);
            return;
        }

        // A grammar error: the same unexpected token. jison names the end of the tokens '1', which is also the name of
        // the character 1 that an attribute block returns as itself, so either reading of '1' is the same token
        var token = (string)error["token"]!;
        var found = token == "1" ? ["the end of the text", "'1'"] : new[] { $"'{token}'" };
        Assert.True(
            found.Any(f => exception.Message.Contains("found " + f, StringComparison.Ordinal)),
            $"{name}: expected the token {token}, got: {exception.Message}");

        // And the same expected tokens, in jison's order
        var jison = error["expected"]!.AsArray().Select(e => (string)e!).ToList();
        Assert.Equal(jison, exception.Expected);
    }
}
