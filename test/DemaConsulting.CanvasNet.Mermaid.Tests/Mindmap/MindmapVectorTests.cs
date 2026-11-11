using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Mindmap;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Mindmap;

/// <summary>
///     The upstream vectors of the mindmap parser: every case of <c>mindmap-parser.json</c>, recorded from the pinned
///     Mermaid (its jison parser, its mindmap database and <c>getData</c>) by <c>gen-diagram-vectors.mjs --type mindmap</c>,
///     must give exactly Mermaid's nodes, sections, classes, shapes and edges, or fail where Mermaid fails.
/// </summary>
public sealed class MindmapVectorTests
{
    /// <summary>
    ///     The limits of the vectors: the defaults, with the deepest nesting allowed (Mermaid has no depth limit, and one
    ///     case is a chain of 40 nodes).
    /// </summary>
    private static readonly MermaidLimits VectorLimits = new() { MaxNestingDepth = 256 };

    /// <summary>
    ///     Gets the indexes of the vector cases.
    /// </summary>
    public static TheoryData<int> Cases => UpstreamVectors.Indexes(UpstreamVectors.Mindmaps);

    /// <summary>
    ///     Checks that the file holds enough cases: at least 200, with the upstream specs, valid texts and errors.
    /// </summary>
    [Fact]
    public void MindmapParser_Vectors_FileHasEnoughCases_AtLeast200()
    {
        // Arrange
        var cases = UpstreamVectors.Mindmaps;

        // Act
        var errors = cases.Count(c => c!["expected"]!["error"] is not null);
        var upstream = cases.Count(c => ((string)c!["spec"]!).Contains("diagrams/mindmap/", StringComparison.Ordinal));

        // Assert
        Assert.True(cases.Count >= 200, $"only {cases.Count} cases");
        Assert.True(upstream >= 35, $"only {upstream} upstream cases");
        Assert.InRange(errors, 30, cases.Count - 150);
    }

    /// <summary>
    ///     Parses one vector case and compares the result with what Mermaid recorded.
    /// </summary>
    /// <param name="index">The case index.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void MindmapParser_Parse_UpstreamVector_MatchesMermaid(int index)
    {
        // Arrange
        var vector = UpstreamVectors.Mindmaps[index]!.AsObject();
        var name = (string)vector["name"]!;
        var text = (string)vector["text"]!;
        var expected = vector["expected"]!.AsObject();
        var source = Preprocessor.Run(text, null, MermaidLimits.Default);

        // Act
        Exception? failure = null;
        MindmapDiagram? diagram = null;
        try
        {
            var detected = DiagramDetector.Detect(source.Code, DetectorSettings.Default);
            diagram = detected.DiagramType == "mindmap"
                ? MindmapParser.Parse(source, detected, VectorLimits)
                : throw new MermaidSyntaxException(MermaidDiagnosticCodes.UnknownDiagram, "not a mindmap", detected.Location);
        }
        catch (MermaidSyntaxException exception)
        {
            failure = exception;
        }

        // Assert
        if (expected["error"] is JsonObject error)
        {
            AssertSameFailure(name, error, Assert.IsType<MermaidSyntaxException>(failure));
            return;
        }

        Assert.True(failure is null, $"{name}: {failure?.Message}");
        var difference = UpstreamVectors.Difference(WithoutConfiguration(expected), Facts(diagram!));
        Assert.True(difference is null, $"{name}: {difference}");
    }

    /// <summary>
    ///     Writes the facts of a diagram as the generator records them from Mermaid's database and <c>getData</c>, without
    ///     what the configuration decides (padding, width, height and look).
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <returns>The facts.</returns>
    internal static JsonObject Facts(MindmapDiagram diagram)
    {
        var nodes = new JsonArray();
        var dataNodes = new JsonArray();
        foreach (var node in diagram.Nodes)
        {
            var facts = new JsonObject
            {
                ["id"] = (double)node.Id,
                ["nodeId"] = node.NodeId,
                ["level"] = (double)node.Level,
                ["descr"] = node.Description,
                ["type"] = (double)node.Type,
            };
            var data = new JsonObject
            {
                ["id"] = node.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["domId"] = "node_" + node.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["label"] = node.Description,
                ["labelType"] = "markdown",
                ["shape"] = node.Shape,
                ["cssClasses"] = node.CssClasses,
            };
            if (node.Icon is not null)
            {
                facts["icon"] = node.Icon;
                data["icon"] = node.Icon;
            }

            if (node.Class is not null)
            {
                facts["class"] = node.Class;
            }

            data["level"] = (double)node.Level;
            data["nodeId"] = node.NodeId;
            data["type"] = (double)node.Type;
            if (node.Section is { } section)
            {
                facts["section"] = (double)section;
                data["section"] = (double)section;
            }

            facts["isRoot"] = node.IsRoot;
            facts["children"] = new JsonArray([.. node.Children.Select(c => (JsonNode)(double)c.Id)]);
            nodes.Add(facts);
            dataNodes.Add(data);
        }

        var edges = new JsonArray();
        foreach (var edge in diagram.Edges)
        {
            edges.Add(new JsonObject
            {
                ["id"] = edge.Id,
                ["start"] = edge.Start.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["end"] = edge.End.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["type"] = "normal",
                ["curve"] = "basis",
                ["thickness"] = "normal",
                ["classes"] = edge.Classes,
                ["depth"] = (double)edge.Start.Level,
                ["section"] = (double?)edge.End.Section,
            });
        }

        return new JsonObject
        {
            ["type"] = "mindmap",
            ["nodes"] = nodes,
            ["data"] = new JsonObject { ["nodes"] = dataNodes, ["edges"] = edges },
        };
    }

    /// <summary>
    ///     Gets Mermaid's facts without what the configuration decides (the renderer's business).
    /// </summary>
    /// <param name="expected">The facts.</param>
    /// <returns>The facts of the database and the parser alone.</returns>
    private static JsonObject WithoutConfiguration(JsonObject expected)
    {
        var copy = expected.DeepClone().AsObject();
        foreach (var node in copy["nodes"]!.AsArray().Concat(copy["data"]!["nodes"]!.AsArray()))
        {
            foreach (var key in new[] { "padding", "width", "height", "look" })
            {
                node!.AsObject().Remove(key);
            }
        }

        foreach (var edge in copy["data"]!["edges"]!.AsArray())
        {
            edge!.AsObject().Remove("look");
        }

        return copy;
    }

    /// <summary>
    ///     Asserts that an error is the error Mermaid reports: a text Mermaid does not detect as a mindmap, a database or
    ///     JavaScript error with Mermaid's message, a lexical error, or a jison parse error with the same unexpected token
    ///     and the same expected tokens.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <param name="error">Mermaid's error.</param>
    /// <param name="exception">The exception.</param>
    private static void AssertSameFailure(string name, JsonObject error, MermaidSyntaxException exception)
    {
        var message = (string)error["message"]!;
        if (message.StartsWith("No diagram type detected", StringComparison.Ordinal))
        {
            Assert.Equal(MermaidDiagnosticCodes.UnknownDiagram, exception.Code);
            return;
        }

        Assert.True(exception.Location.IsKnown, $"{name}: {exception.Message}");
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
        if (message.StartsWith("Lexical error", StringComparison.Ordinal))
        {
            Assert.Contains("unrecognized text", exception.Message, StringComparison.Ordinal);
            return;
        }

        if (error["token"] is null)
        {
            // The database's error, or the JavaScript error of a decoration before any node: Mermaid's own message
            Assert.Empty(exception.Expected);
            Assert.Contains(message, exception.Message, StringComparison.Ordinal);
            return;
        }

        // The same unexpected token: jison names the end of the tokens '1'
        var token = (string)error["token"]!;
        var found = token == "1" ? "the end of the text" : $"'{token}'";
        Assert.True(exception.Message.Contains("found " + found, StringComparison.Ordinal), $"{name}: expected the token {token}, got: {exception.Message}");

        // And the same expected tokens, in jison's order
        var jison = error["expected"]!.AsArray().Select(e => (string)e!).ToList();
        Assert.Equal(jison, exception.Expected);
    }
}
