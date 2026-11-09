using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Flowchart;
using DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Flowchart;

/// <summary>
///     The upstream vectors of the flowchart parser and model: every case of <c>flowchart-parser.json</c>, recorded from
///     the pinned Mermaid by <c>gen-flowchart-vectors.mjs</c>, must give exactly Mermaid's database facts and layout
///     data, or fail where Mermaid fails.
/// </summary>
public sealed class FlowchartVectorTests
{
    /// <summary>
    ///     Gets the indexes of the vector cases.
    /// </summary>
    public static TheoryData<int> Cases => UpstreamVectors.Indexes(UpstreamVectors.Flowcharts);

    /// <summary>
    ///     Checks that the file holds the cases the plan asks for: at least 150, with valid and invalid texts.
    /// </summary>
    [Fact]
    public void FlowchartParser_Vectors_FileHasEnoughCases_AtLeast150()
    {
        // Arrange
        var cases = UpstreamVectors.Flowcharts;

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
    public void FlowchartParser_Parse_UpstreamVector_MatchesMermaid(int index)
    {
        // Arrange
        var vector = UpstreamVectors.Flowcharts[index]!.AsObject();
        var name = (string)vector["name"]!;
        var text = (string)vector["text"]!;
        var site = vector["site"]?.AsObject() ?? [];
        var expected = vector["expected"]!.AsObject();

        // Act
        Exception? failure = null;
        JsonObject? actual = null;
        try
        {
            var document = MermaidDocument.Parse(text);
            var model = Assert.IsType<FlowchartModel>(document.Diagram);
            var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(site, text);
            actual = FlowchartFacts.Of(model, config, document.Detected.MermaidId);
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
    ///     Asserts that a parse error is the error Mermaid reports: for a grammar error, the same unexpected token.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <param name="error">Mermaid's error.</param>
    /// <param name="exception">The exception.</param>
    private static void AssertSameFailure(string name, JsonObject error, MermaidSyntaxException exception)
    {
        var message = (string)error["message"]!;
        if (message.StartsWith("Lexical error", StringComparison.Ordinal))
        {
            Assert.Contains("unrecognized text", exception.Message, StringComparison.Ordinal);
            return;
        }

        if (error["token"] is not JsonValue token)
        {
            // An error of Mermaid's database (an unknown shape, a linkStyle index, YAML): located, with a code
            Assert.True(exception.Location.IsKnown, $"{name}: {exception.Message}");
            return;
        }

        // A grammar error: the same unexpected token (jison's '1' is the end of the tokens)
        var expectedToken = token.GetValue<string>() switch
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
