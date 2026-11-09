using System.Text;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     Hardening tests through the public API: hostile and very large inputs end in a located limit failure or a
///     render, and a seeded mutation fuzzer over the fixtures only ever sees the documented exceptions.
/// </summary>
public class MermaidHardeningTests
{
    /// <summary>Options that measure at a tenth of the natural size, so very large diagrams fit a surface.</summary>
    private static readonly MermaidRenderOptions Tenth = new() { Scale = 0.1 };

    /// <summary>
    ///     Gets the hostile corpus: diagrams that once escaped the documented exceptions (found by review), each with
    ///     the outcome now expected: null when it renders, else the code of the located render failure and its line.
    /// </summary>
    public static TheoryData<string, string?, int> HostileCorpus => new()
    {
        // A subgraph used as a node of another subgraph, and a self-loop on a subgraph (Mermaid 11.17.2 fails on both)
        { "flowchart TB\nsubgraph s2\n s1\nend\nsubgraph s1\n subgraph s4\n  n5\n end\n s4 --> n2\nend\n", MermaidDiagnosticCodes.RenderFailed, 9 },
        { "flowchart TB\nsubgraph s0\n n0\nend\nsubgraph s1\n n3 --> s2\nend\nsubgraph s2\n s0 --> s0\nend\n", MermaidDiagnosticCodes.RenderFailed, 9 },

        // Font sizes that are zero or infinite in single precision, and dash patterns beyond single precision
        { "flowchart TD\nA\nstyle A font-size:1e-300px\n", null, 0 },
        { "flowchart TD\nA\nstyle A font-size:1e300px\n", null, 0 },
        { "flowchart TD\nA:::k\nclassDef k font-size:1e300px\n", null, 0 },
        { "flowchart TD\nA --> B\nlinkStyle 0 font-size:1e-300px\n", null, 0 },
        { "%%{init: {'themeVariables': {'fontSize': 1e-308}}}%%\nflowchart TD\nA --> B\n", null, 0 },
        { "flowchart TD\nA[\"<span style='font-size:1e300px'>x</span>\"]\n", null, 0 },
        { "flowchart TD\nA\nstyle A stroke-dasharray:1e300\n", null, 0 },
        { "flowchart TD\nA\nstyle A stroke-dasharray:1e39 1\n", null, 0 },
        { "flowchart TD\nA:::k\nclassDef k stroke-dasharray:4e38\n", null, 0 },
    };

    /// <summary>
    ///     Validates the hostile corpus through the public API: each diagram renders, or fails with a located
    ///     <see cref="MermaidRenderException"/> of the expected code at the expected line, and never with another
    ///     exception.
    /// </summary>
    /// <param name="text">The diagram.</param>
    /// <param name="code">The code of the expected failure, or null when the diagram renders.</param>
    /// <param name="line">The line of the expected failure.</param>
    [Theory]
    [MemberData(nameof(HostileCorpus))]
    public void CanvasNetMermaid_Hardening_HostileCorpus_RendersOrFailsLocated(string text, string? code, int line)
    {
        // Arrange
        var document = MermaidDocument.Parse(text);

        // Act
        var exception = Record.Exception(() =>
        {
            using var surface = document.Render(cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
        });

        // Assert
        if (code is null)
        {
            Assert.Null(exception);
        }
        else
        {
            var failure = Assert.IsType<MermaidRenderException>(exception);
            Assert.Equal(code, failure.Code);
            Assert.Equal(line, failure.Location.Line);
        }
    }

    /// <summary>
    ///     Validates that more edges than <see cref="MermaidLimits.MaxEdges"/> fail at open, located at the extra edge.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_TooManyEdges_IsALocatedLimit()
    {
        // Arrange
        var text = new StringBuilder("flowchart LR\n");
        for (var i = 0; i < 501; i++)
        {
            text.Append("  n").Append(i).Append(" --> n").Append(i + 1).Append('\n');
        }

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text.ToString()));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(502, exception.Location.Line);
    }

    /// <summary>
    ///     Validates that a text longer than <see cref="MermaidLimits.MaxTextSize"/> is refused before any parsing.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_TooLongText_IsRefused()
    {
        // Arrange
        var text = "flowchart LR\n  a --> b\n" + new string(' ', 50_001);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => MermaidDocument.Parse(text));
    }

    /// <summary>
    ///     Validates that subgraphs nested deeper than <see cref="MermaidLimits.MaxNestingDepth"/> fail at open.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_DeepNesting_IsALocatedLimit()
    {
        // Arrange
        var text = new StringBuilder("flowchart TB\n");
        for (var i = 0; i < 33; i++)
        {
            text.Append("subgraph s").Append(i).Append('\n');
        }

        text.Append("a\n");
        for (var i = 0; i < 33; i++)
        {
            text.Append("end\n");
        }

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text.ToString()));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.True(exception.Location.Line > 1);
    }

    /// <summary>
    ///     Validates that 32 nested subgraphs (the limit) still render.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_NestingAtTheLimit_Renders()
    {
        // Arrange
        var text = new StringBuilder("flowchart TB\n");
        for (var i = 0; i < 32; i++)
        {
            text.Append("subgraph s").Append(i).Append('\n');
        }

        text.Append("a --> b\n");
        for (var i = 0; i < 32; i++)
        {
            text.Append("end\n");
        }

        text.Append("c --> a\n");

        // Act
        var size = MermaidDocument.Parse(text.ToString()).Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(size.Width > 100 && size.Height > 100);
    }

    /// <summary>
    ///     Validates that a YAML alias bomb in the frontmatter is refused (anchors and aliases are not supported).
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_YamlAliasBomb_IsRefused()
    {
        // Arrange
        const string Text = "---\nconfig:\n  a: &a [x, x, x, x, x, x, x, x, x]\n  b: &b [*a, *a, *a, *a, *a, *a, *a, *a, *a]\n  c: &c [*b, *b, *b, *b, *b, *b, *b, *b, *b]\n  d: [*c, *c, *c, *c, *c, *c, *c, *c, *c]\n---\nflowchart LR\n  a --> b\n";

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(Text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.YamlUnsupported, exception.Code);
        Assert.Equal(3, exception.Location.Line);
    }

    /// <summary>
    ///     Validates that a 10,000-character label is over the default <see cref="MermaidLimits.MaxLabelLength"/>, and that
    ///     with the limit raised it renders, wrapped.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_LongLabel_IsALimitOrWraps()
    {
        // Arrange
        var words = string.Join(' ', Enumerable.Repeat("lorem", 1_666));
        var text = "flowchart LR\n  a[\"" + words + "\"] --> b\n";
        var raised = new MermaidOpenOptions { Limits = new MermaidLimits { MaxLabelLength = 20_000 } };

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));
        var size = MermaidDocument.Parse(text, raised).Measure(Tenth, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.True(size.Height > 100);
        Assert.True(size.Width < 60);
    }

    /// <summary>
    ///     Validates that the complete graph of 30 nodes (435 edges) renders within the default layout work, and fails
    ///     with a located limit under a small one.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_CompleteGraph_RendersOrHitsTheWorkLimit()
    {
        // Arrange
        var text = new StringBuilder("flowchart TB\n");
        for (var i = 0; i < 30; i++)
        {
            for (var j = i + 1; j < 30; j++)
            {
                text.Append("  k").Append(i).Append(" --> k").Append(j).Append('\n');
            }
        }

        var document = MermaidDocument.Parse(text.ToString());
        var small = new MermaidRenderOptions { Limits = new MermaidLimits { MaxLayoutWork = 1_000_000 } };

        // Act
        var size = document.Measure(Tenth, TestContext.Current.CancellationToken);
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(small, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(size.Width > 500);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(1, exception.Location.Line);
    }

    /// <summary>
    ///     Validates that a long chain and many subgraphs render.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_LongChainAndManySubgraphs_Render()
    {
        // Arrange
        var chain = new StringBuilder("flowchart TB\n");
        for (var i = 0; i < 400; i++)
        {
            chain.Append("  c").Append(i).Append(" --> c").Append(i + 1).Append('\n');
        }

        var clusters = new StringBuilder("flowchart LR\n");
        for (var i = 0; i < 60; i++)
        {
            clusters.Append("  subgraph g").Append(i).Append("\n    x").Append(i).Append(" --> y").Append(i).Append("\n  end\n");
            if (i > 0)
            {
                clusters.Append("  y").Append(i - 1).Append(" --> x").Append(i).Append('\n');
            }
        }

        // Act
        var chainSize = MermaidDocument.Parse(chain.ToString()).Measure(Tenth, TestContext.Current.CancellationToken);
        var clusterSize = MermaidDocument.Parse(clusters.ToString()).Measure(Tenth, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(chainSize.Height > 2_000);
        Assert.True(clusterSize.Width > 500);
    }

    /// <summary>
    ///     Validates with a seeded mutation fuzzer over the fixtures that opening and measuring only ever throw the
    ///     documented exceptions.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_MutatedFixtures_ThrowOnlyDocumentedExceptions()
    {
        // Arrange
        string[] tokens =
        [
            "-->", "---", "-.->", "==>", "~~~", "<-->", "o--o", "x--x", "|label|", "[", "]", "(", ")", "{", "}", "((", "))",
            "@{ shape: rect }", "@{ img: x.png }", ":::cls", "&", "subgraph S", "end", "direction LR", "classDef cls fill:#f00",
            "style a fill:#0f0", "linkStyle 0 stroke:#f00", "linkStyle default interpolate step", "%%", "%%{init: {}}%%", "\"", "`",
            "\n", "  ", "<br>", "<b>x</b>", "**bold**", "a --> a", "e1@-->", "e1@{ animate: true }", "click a call f()", "#59;",
        ];
        var random = new Random(20261108);
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "flowchart"), "*.mmd")
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText)
            .ToList();
        var unexpected = new List<string>();

        // Act
        foreach (var fixture in fixtures)
        {
            for (var round = 0; round < 6; round++)
            {
                var text = Mutate(fixture, tokens, random);
                try
                {
                    MermaidDocument.Parse(text).Measure(cancellationToken: TestContext.Current.CancellationToken);
                }
                catch (Exception ex) when (ex is MermaidSyntaxException or MermaidRenderException or InvalidDataException or ArgumentOutOfRangeException)
                {
                    // A documented failure
                }
                catch (Exception ex)
                {
                    unexpected.Add($"{ex.GetType().Name}: {ex.Message}\n{text}");
                }
            }
        }

        // Assert
        Assert.Empty(unexpected);
    }

    /// <summary>
    ///     Mutates a text: deletes a range, inserts a token, duplicates a line or swaps two characters, one to three times.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens to insert.</param>
    /// <param name="random">The seeded generator.</param>
    /// <returns>The mutated text.</returns>
    private static string Mutate(string text, string[] tokens, Random random)
    {
        var builder = new StringBuilder(text);
        var count = random.Next(1, 4);
        for (var i = 0; i < count; i++)
        {
            var position = random.Next(builder.Length + 1);
            switch (random.Next(4))
            {
                case 0 when builder.Length > 0:
                    var length = Math.Min(random.Next(1, 12), builder.Length - Math.Min(position, builder.Length - 1));
                    builder.Remove(Math.Min(position, builder.Length - 1), Math.Max(length, 0));
                    break;
                case 1:
                    builder.Insert(position, tokens[random.Next(tokens.Length)]);
                    break;
                case 2:
                    var lines = builder.ToString().Split('\n');
                    var line = lines[random.Next(lines.Length)];
                    builder.Insert(position, "\n" + line + "\n");
                    break;
                default:
                    if (builder.Length > 2)
                    {
                        var at = random.Next(builder.Length - 1);
                        (builder[at], builder[at + 1]) = (builder[at + 1], builder[at]);
                    }

                    break;
            }
        }

        return builder.ToString();
    }
}
