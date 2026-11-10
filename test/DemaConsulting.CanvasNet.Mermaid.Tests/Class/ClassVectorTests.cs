using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Class;

/// <summary>
///     The upstream vectors of the class parser and model: every case of <c>class-parser.json</c>, recorded from the
///     pinned Mermaid by <c>gen-diagram-vectors.mjs --type class</c>, must give exactly Mermaid's database facts and the
///     facts of its <c>getData()</c>, or fail where Mermaid fails.
/// </summary>
public sealed class ClassVectorTests
{
    /// <summary>
    ///     Gets the indexes of the vector cases.
    /// </summary>
    public static TheoryData<int> Cases => UpstreamVectors.Indexes(UpstreamVectors.Classes);

    /// <summary>
    ///     Checks that the file holds the cases the plan asks for: at least 120, with valid and invalid texts.
    /// </summary>
    [Fact]
    public void ClassParser_Vectors_FileHasEnoughCases_AtLeast120()
    {
        // Arrange
        var cases = UpstreamVectors.Classes;

        // Act
        var errors = cases.Count(c => c!["expected"]!["error"] is not null);

        // Assert
        Assert.True(cases.Count >= 120, $"only {cases.Count} cases");
        Assert.InRange(errors, 10, cases.Count - 100);
    }

    /// <summary>
    ///     Parses one vector case and compares the result with what Mermaid recorded.
    /// </summary>
    /// <param name="index">The case index.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ClassParser_Parse_UpstreamVector_MatchesMermaid(int index)
    {
        // Arrange
        var vector = UpstreamVectors.Classes[index]!.AsObject();
        var name = (string)vector["name"]!;
        var text = (string)vector["text"]!;
        var site = vector["site"]?.AsObject() ?? [];
        var expected = vector["expected"]!.AsObject();

        // Act
        Exception? failure = null;
        JsonObject? actual = null;
        try
        {
            var model = ClassFacts.Parse(text);
            var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(site, text);
            actual = ClassFacts.Of(model, config);
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
    ///     Asserts that a parse error is the error Mermaid reports: for a lexical error, unrecognized text; for an error of
    ///     Mermaid's database (a JavaScript error without a token), a located syntax error with Mermaid's message; for a
    ///     grammar error, the same unexpected token and the same expected tokens.
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

        if (error["token"] is null)
        {
            Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
            Assert.Contains(message, exception.Message, StringComparison.Ordinal);
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
