using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.GitGraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.GitGraph;

/// <summary>
///     The upstream vectors of the git graph parser: every case of <c>gitgraph-parser.json</c>, recorded from the pinned
///     Mermaid (its Langium parser and git graph database, with the seeded id sequence) by
///     <c>gen-diagram-vectors.mjs --type gitgraph</c>, must give exactly Mermaid's database facts (the texts, the
///     effective gitGraph configuration, every commit with its generated id, the branches, their drawing order, the
///     checked-out branch and the head), or fail where Mermaid fails first, with the same kind of error, at the same place,
///     expecting the same tokens.
/// </summary>
public sealed class GitGraphVectorTests
{
    /// <summary>
    ///     Gets the indexes of the vector cases.
    /// </summary>
    public static TheoryData<int> Cases => UpstreamVectors.Indexes(UpstreamVectors.GitGraphs);

    /// <summary>
    ///     Checks that the file holds enough cases: at least 220, at least 90 of them from Mermaid's specs, with valid and
    ///     invalid texts.
    /// </summary>
    [Fact]
    public void GitGraphParser_Vectors_FileHasEnoughCases_AtLeast220()
    {
        // Arrange
        var cases = UpstreamVectors.GitGraphs;

        // Act
        var errors = cases.Count(c => c!["expected"]!["error"] is not null);
        var upstream = cases.Count(c => ((string)c!["spec"]!).Contains(".ts:", StringComparison.Ordinal));

        // Assert
        Assert.True(cases.Count >= 220, $"only {cases.Count} cases");
        Assert.True(upstream >= 90, $"only {upstream} upstream cases");
        Assert.InRange(errors, 50, cases.Count - 120);
    }

    /// <summary>
    ///     Parses one vector case under the case's site configuration (which Mermaid's <c>getDiagramFromText</c> applies,
    ///     unlike the text's frontmatter) and compares the database with what Mermaid recorded.
    /// </summary>
    /// <param name="index">The case index.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void GitGraphParser_Parse_UpstreamVector_MatchesMermaid(int index)
    {
        // Arrange
        var vector = UpstreamVectors.GitGraphs[index]!.AsObject();
        var name = (string)vector["name"]!;
        var text = (string)vector["text"]!;
        var expected = vector["expected"]!.AsObject();
        var source = Preprocessor.Run(text, null, MermaidLimits.Default);
        var options = new MermaidRenderOptions { Config = (vector["site"] as JsonObject)?.ToJsonString() };
        var config = MermaidConfigResolver.Resolve(Preprocessor.Run("gitGraph\n", null, MermaidLimits.Default), "gitGraph", options, null);

        // Act
        MermaidSyntaxException? failure = null;
        JsonObject? actual = null;
        try
        {
            var diagram = GitGraphParser.Parse(source, DiagramDetector.Detect(source.Code, DetectorSettings.Default), MermaidLimits.Default, config);
            actual = Facts(diagram, config);
        }
        catch (MermaidSyntaxException exception)
        {
            failure = exception;
        }

        // Assert
        if (expected["error"] is JsonObject error)
        {
            Assert.True(failure is not null, $"{name}: no error, expected '{(string)error["message"]!}'");
            AssertSameFailure(name, (string)error["message"]!, failure, source);
            return;
        }

        Assert.True(failure is null, $"{name}: {failure?.Message}");
        var difference = UpstreamVectors.Difference(expected, actual);
        Assert.True(difference is null, $"{name}: {difference}");
    }

    /// <summary>
    ///     Writes the facts of a diagram as the generator records them from Mermaid's git graph database (the generator's
    ///     <c>getDiagramFromText</c> passes no frontmatter title, which only a render sets).
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <param name="config">The configuration of the case.</param>
    /// <returns>The facts.</returns>
    internal static JsonObject Facts(GitGraphDiagram diagram, MermaidConfig config)
    {
        var database = diagram.OpenDatabase;
        var commits = new JsonArray();
        foreach (var commit in database.Commits)
        {
            var written = new JsonObject
            {
                ["id"] = commit.Id,
                ["message"] = commit.Message,
                ["seq"] = (double)commit.Seq,
                ["type"] = (double)commit.Type,
                ["tags"] = new JsonArray([.. commit.Tags.Select(tag => (JsonNode)tag)]),
                ["parents"] = new JsonArray([.. commit.Parents.Select(parent => (JsonNode)parent)]),
                ["branch"] = commit.Branch,
            };
            if (commit.CustomType is { } type)
            {
                written["customType"] = (double)type;
            }

            if (commit.CustomId is { } customId)
            {
                written["customId"] = customId;
            }

            commits.Add(written);
        }

        var branches = new JsonArray();
        foreach (var branch in database.Branches)
        {
            branches.Add(new JsonObject { ["name"] = branch.Name, ["head"] = database.HeadOf(branch.Name) });
        }

        return new JsonObject
        {
            ["type"] = "gitGraph",
            ["direction"] = diagram.Direction,
            ["title"] = diagram.DiagramTitle,
            ["accTitle"] = diagram.AccessibleTitle,
            ["accDescr"] = diagram.AccessibleDescription,
            ["config"] = config.Raw["gitGraph"]!.DeepClone(),
            ["commits"] = commits,
            ["branches"] = branches,
            ["branchOrder"] = new JsonArray([.. database.BranchOrder.Select(branch => (JsonNode)branch.Name)]),
            ["currentBranch"] = database.CurrentBranch,
            ["head"] = database.Head?.Id,
        };
    }

    /// <summary>
    ///     Asserts that an error is the first error Mermaid reports: a database error with Mermaid's message, or a lexer or
    ///     parse error at the same line and column of the parser text, a parse error expecting the same tokens (the first
    ///     token of each sequence chevrotain lists).
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

        // A parse error: the same expected tokens (of the first error only)
        var tokens = first[colon..];
        var found = tokens.IndexOf("but found", StringComparison.Ordinal);
        Assert.Equal(ExpectedTokens(found < 0 ? tokens : tokens[..found]), exception.Expected);
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
    ///     Reads the expected tokens of a chevrotain message: <c>Expecting token of type 'X'</c>, or the first token of each
    ///     sequence of <c>Expecting: one of these possible Token sequences</c>, each once.
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
            if (open < 0)
            {
                continue;
            }

            var sequence = row[(open + 3)..row.IndexOf(']', open)];
            var token = sequence.Split(", ")[0];
            if (!tokens.Contains(token))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }
}
