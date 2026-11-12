using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Packet;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Packet;

/// <summary>
///     The upstream vectors of the packet parser: every case of <c>packet-parser.json</c>, recorded from the pinned Mermaid
///     (its Langium parser and packet database) by <c>gen-diagram-vectors.mjs --type packet</c>, must give exactly
///     Mermaid's database facts (the texts, the effective packet configuration and every row), or fail where Mermaid fails
///     first, with the same kind of error, at the same place, expecting the same tokens.
/// </summary>
public sealed class PacketVectorTests
{
    /// <summary>The limits of the row split: Mermaid has no part limit, only its 10,000-row cap.</summary>
    private static readonly MermaidLimits Unbounded = new() { MaxNodes = int.MaxValue };

    /// <summary>
    ///     Gets the indexes of the vector cases.
    /// </summary>
    public static TheoryData<int> Cases => UpstreamVectors.Indexes(UpstreamVectors.Packets);

    /// <summary>
    ///     Checks that the file holds enough cases: at least 120, with the upstream specs, valid and invalid texts.
    /// </summary>
    [Fact]
    public void PacketParser_Vectors_FileHasEnoughCases_AtLeast120()
    {
        // Arrange
        var cases = UpstreamVectors.Packets;

        // Act
        var errors = cases.Count(c => c!["expected"]!["error"] is not null);
        var upstream = cases.Count(c => ((string)c!["spec"]!).Contains(".ts:", StringComparison.Ordinal));

        // Assert
        Assert.True(cases.Count >= 120, $"only {cases.Count} cases");
        Assert.True(upstream >= 20, $"only {upstream} upstream cases");
        Assert.InRange(errors, 30, cases.Count - 80);
    }

    /// <summary>
    ///     Parses one vector case, splits its rows under the case's site configuration, and compares the result with what
    ///     Mermaid recorded.
    /// </summary>
    /// <param name="index">The case index.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void PacketParser_Parse_UpstreamVector_MatchesMermaid(int index)
    {
        // Arrange: getDiagramFromText applies the site configuration only (not the frontmatter's)
        var vector = UpstreamVectors.Packets[index]!.AsObject();
        var name = (string)vector["name"]!;
        var text = (string)vector["text"]!;
        var expected = vector["expected"]!.AsObject();
        var source = Preprocessor.Run(text, null, MermaidLimits.Default);
        var options = new MermaidRenderOptions { Config = (vector["site"] as JsonObject)?.ToJsonString() };
        var config = MermaidConfigResolver.Resolve(Preprocessor.Run("packet\n", null, MermaidLimits.Default), "packet", options, null);

        // Act
        Exception? failure = null;
        JsonObject? actual = null;
        try
        {
            var diagram = PacketParser.Parse(source, DiagramDetector.Detect(source.Code, DetectorSettings.Default), MermaidLimits.Default);
            var rows = diagram.Rows(config.Packet.BitsPerRow, Unbounded, new LayoutWorkMeter(long.MaxValue, diagram.KeywordLocation, CancellationToken.None));
            actual = Facts(diagram, config.Packet, rows);
        }
        catch (MermaidSyntaxException exception)
        {
            failure = exception;
        }
        catch (MermaidRenderException exception)
        {
            failure = exception;
        }

        // Assert
        if (expected["error"] is JsonObject error)
        {
            var message = (string)error["message"]!;
            if (failure is MermaidRenderException render)
            {
                // The row split's error, which Mermaid throws from the same populate
                Assert.Equal(MermaidDiagnosticCodes.RenderFailed, render.Code);
                Assert.Contains(message, render.Message, StringComparison.Ordinal);
                Assert.True(render.Location.IsKnown, $"{name}: {render.Message}");
                return;
            }

            AssertSameFailure(name, message, Assert.IsType<MermaidSyntaxException>(failure), source);
            return;
        }

        Assert.True(failure is null, $"{name}: {failure?.Message}");
        var difference = UpstreamVectors.Difference(expected, actual);
        Assert.True(difference is null, $"{name}: {difference}");
    }

    /// <summary>
    ///     Writes the facts of a diagram as the generator records them from Mermaid's packet database (the generator's
    ///     <c>getDiagramFromText</c> passes no frontmatter title, which only a render sets).
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <param name="config">The packet configuration.</param>
    /// <param name="rows">The rows.</param>
    /// <returns>The facts.</returns>
    private static JsonObject Facts(PacketDiagram diagram, PacketConfig config, IReadOnlyList<IReadOnlyList<PacketBlock>> rows)
    {
        // A diagram of more than 100 rows keeps its first and last 50
        var kept = rows.Count > 100 ? [.. rows.Take(50), .. rows.Skip(rows.Count - 50)] : rows;
        var written = new JsonArray();
        foreach (var row in kept)
        {
            var parts = new JsonArray();
            foreach (var part in row)
            {
                parts.Add(new JsonObject
                {
                    ["start"] = Number(part.Start),
                    ["end"] = Number(part.End),
                    ["bits"] = Number(part.Bits),
                    ["label"] = part.Label,
                });
            }

            written.Add(parts);
        }

        var facts = new JsonObject
        {
            ["type"] = "packet",
            ["title"] = diagram.DiagramTitle,
            ["accTitle"] = diagram.AccessibleTitle,
            ["accDescr"] = diagram.AccessibleDescription,
            ["config"] = new JsonObject
            {
                ["useMaxWidth"] = config.UseMaxWidth,
                ["rowHeight"] = config.RowHeight,
                ["bitWidth"] = config.BitWidth,
                ["bitsPerRow"] = config.BitsPerRow,
                ["showBits"] = config.ShowBits,
                ["paddingX"] = config.PaddingX,
                ["paddingY"] = config.EffectivePaddingY,
            },
            ["rowCount"] = (double)rows.Count,
        };
        if (rows.Count > 100)
        {
            facts["truncated"] = true;
        }

        facts["rows"] = written;
        return facts;
    }

    /// <summary>
    ///     Writes a number as the generator does: a finite number as JSON writes it (negative zero as zero), any other as
    ///     JavaScript's <c>String</c>.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The JSON value.</returns>
    private static JsonNode Number(double value) =>
        double.IsFinite(value) ? JsonValue.Create(value + 0.0) : JsonValue.Create(YamlSubsetParser.FormatJsNumber(value));

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
