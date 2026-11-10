using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.State;
using DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.State;

/// <summary>
///     The upstream vectors of the state parser and model: every case of <c>state-parser.json</c>, recorded from the
///     pinned Mermaid by <c>gen-diagram-vectors.mjs --type state</c>, must give exactly Mermaid's database facts, its
///     translated statement tree and the facts of its <c>getData()</c> (with the URL of a click compared as the library
///     checks it), or fail where Mermaid fails.
/// </summary>
public sealed class StateVectorTests
{
    /// <summary>
    ///     Gets the indexes of the vector cases.
    /// </summary>
    public static TheoryData<int> Cases => UpstreamVectors.Indexes(UpstreamVectors.States);

    /// <summary>
    ///     Checks that the file holds the cases the plan asks for: at least 100, with valid and invalid texts.
    /// </summary>
    [Fact]
    public void StateParser_Vectors_FileHasEnoughCases_AtLeast100()
    {
        // Arrange
        var cases = UpstreamVectors.States;

        // Act
        var errors = cases.Count(c => c!["expected"]!["error"] is not null);

        // Assert
        Assert.True(cases.Count >= 100, $"only {cases.Count} cases");
        Assert.InRange(errors, 10, cases.Count - 90);
    }

    /// <summary>
    ///     Parses one vector case and compares the result with what Mermaid recorded.
    /// </summary>
    /// <param name="index">The case index.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void StateParser_Parse_UpstreamVector_MatchesMermaid(int index)
    {
        // Arrange
        var vector = UpstreamVectors.States[index]!.AsObject();
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
            var model = StateFacts.Parse(text, settings: config.DetectorSettings);
            actual = StateFacts.Of(model, config);
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
        var difference = UpstreamVectors.Difference(WithCheckedUrls(expected), actual);
        Assert.True(difference is null, $"{name}: {difference}");
    }

    /// <summary>
    ///     Gets Mermaid's facts with the URL of every click (in <c>links</c> and in the click statements of
    ///     <c>rootDoc</c>) as the library keeps it. Mermaid keeps the URL as written, quotes included, and never checks it
    ///     with sanitize-url; the library keeps only <see cref="StateModel.CheckLink(string)"/> of it, so the recorded URL
    ///     is compared as that check gives it (sanitize-url of the URL Mermaid's picture would carry). Every other fact,
    ///     the tooltip included, is compared as recorded.
    /// </summary>
    /// <param name="expected">Mermaid's facts; not changed.</param>
    /// <returns>A copy of the facts with the checked URLs.</returns>
    private static JsonObject WithCheckedUrls(JsonObject expected)
    {
        var result = expected.DeepClone().AsObject();
        foreach (var link in result["links"]?.AsArray() ?? [])
        {
            CheckUrl(link);
        }

        foreach (var statement in result["rootDoc"]?.AsArray() ?? [])
        {
            CheckClicks(statement);
        }

        return result;
    }

    /// <summary>Replaces the URL of every click statement of a statement and its documents with the checked URL.</summary>
    /// <param name="statement">The statement facts.</param>
    private static void CheckClicks(JsonNode? statement)
    {
        if (statement is not JsonObject facts)
        {
            return;
        }

        if ((string?)facts["stmt"] == "click")
        {
            CheckUrl(facts);
        }

        foreach (var child in facts["doc"]?.AsArray() ?? [])
        {
            CheckClicks(child);
        }
    }

    /// <summary>Replaces a recorded URL with the checked URL.</summary>
    /// <param name="facts">The facts that hold the URL.</param>
    private static void CheckUrl(JsonNode? facts)
    {
        if (facts is JsonObject holder && holder["url"] is JsonValue value && value.TryGetValue<string>(out var url))
        {
            holder["url"] = StateModel.CheckLink(url);
        }
    }

    /// <summary>
    ///     Asserts that a parse error is the error Mermaid reports: for a lexical error, unrecognized text; for an error
    ///     Mermaid throws without a token (the lexer's single-word error or the database's group error), a located syntax
    ///     error with Mermaid's message; for a grammar error, the same unexpected token and the same expected tokens.
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

        if (error["token"] is null)
        {
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
