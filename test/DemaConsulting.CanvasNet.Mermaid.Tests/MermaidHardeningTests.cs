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
    ///     Gets sequence diagrams whose stroke widths round to zero in single precision when fitted to 400 by 300 pixels
    ///     (a tiny fit scale), found by review.
    /// </summary>
    public static TheoryData<string> SequenceTinyFitScales =>
    [
        "%%{init: {\"sequence\": {\"width\": 1e300}}}%%\nsequenceDiagram\nA->>B: hi\n",
        "%%{init: {\"sequence\": {\"textPlacement\": \"old\", \"actorMargin\": 1e60, \"mirrorActors\": false}}}%%\nsequenceDiagram\nparticipant A\nparticipant B\n",
    ];

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
    ///     Validates that a sequence diagram fitted to a size at a tiny scale renders without throwing: a stroke width
    ///     that rounds to zero in single precision draws no stroke.
    /// </summary>
    /// <param name="text">The diagram.</param>
    [Theory]
    [MemberData(nameof(SequenceTinyFitScales))]
    public void CanvasNetMermaid_Hardening_SequenceTinyFitScale_Renders(string text)
    {
        // Arrange
        var document = MermaidDocument.Parse(text);

        // Act
        using var surface = document.Render(400, 300, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((400, 300), (surface.Width, surface.Height));
    }

    /// <summary>
    ///     Validates that the shadowed boxes of a sequence diagram (a box around participants and a forced menu panel)
    ///     with a stroke width beyond any drawing (set by <c>themeCSS</c>, found by review) render without throwing,
    ///     their strokes not drawn and their shadows the shadows of their fills alone: the same pixels as with a stroke
    ///     width of zero.
    /// </summary>
    /// <param name="selector">The rule that sets the stroke width.</param>
    /// <param name="width">The stroke width.</param>
    [Theory]
    [InlineData("g rect.rect", "1e300px")]
    [InlineData("g rect.rect", "1e30px")]
    [InlineData("g rect.rect", "40000px")]
    [InlineData(".actorPopupMenuPanel", "1e300px")]
    [InlineData(".actorPopupMenuPanel", "1e30px")]
    [InlineData(".actorPopupMenuPanel", "40000px")]
    public void CanvasNetMermaid_Hardening_SequenceHugeStrokeWidth_ShadowIsTheFillsShadow(string selector, string width)
    {
        // Arrange
        MermaidDocument Diagram(string strokeWidth) => MermaidDocument.Parse(
            "---\nconfig:\n  sequence:\n    forceMenus: true\n" +
            $"  themeCSS: \"{selector} {{ stroke-width: {strokeWidth} }}\"\n---\n" +
            "sequenceDiagram\n    box Aqua Group\n    participant A\n    end\n    participant B\n" +
            "    link B: Home @ https://example.com/\n    A->>B: hi\n");
        var huge = Diagram(width);
        var none = Diagram("0");

        // Act
        using var surface = huge.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var expected = none.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(MermaidTestHelpers.PixelsEqual(expected, surface));
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
    ///     Validates that sequence blocks nested deeper than <see cref="MermaidLimits.MaxNestingDepth"/> fail at open, and
    ///     that 32 nested blocks (the limit) render.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_SequenceDeepBlocks_AreALocatedLimit()
    {
        // Arrange
        static string Nested(int depth) =>
            "sequenceDiagram\n" + string.Concat(Enumerable.Range(0, depth).Select(i => $"loop L{i}\n")) + "A->>B: x\n" + string.Concat(Enumerable.Repeat("end\n", depth));

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(Nested(33)));
        var size = MermaidDocument.Parse(Nested(32)).Measure(Tenth, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.True(exception.Location.Line > 1);
        Assert.True(size.Height > 50);
    }

    /// <summary>
    ///     Validates that more sequence participants than <see cref="MermaidLimits.MaxNodes"/> fail at open, located at
    ///     the extra participant.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_SequenceTooManyParticipants_IsALocatedLimit()
    {
        // Arrange
        var text = "sequenceDiagram\n" + string.Concat(Enumerable.Range(0, 2_001).Select(i => $"participant p{i}\n"));

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(2_002, exception.Location.Line);
    }

    /// <summary>
    ///     Validates that sequence boxes count against <see cref="MermaidLimits.MaxNodes"/> with the participants: once
    ///     1,000 boxes of one participant each reach the limit, the next box fails at open, located at the box.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_SequenceTooManyBoxes_IsALocatedLimit()
    {
        // Arrange: 1,000 boxes and their 1,000 participants reach the limit; the next box is one too many
        var text = "sequenceDiagram\n" + string.Concat(Enumerable.Range(0, 1_001).Select(i => $"box Aqua b{i}\nparticipant p{i}\nend\n"));

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));

        // Assert: box 1,000 (counted from zero) is on line 2 + 3 * 1,000
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(3_002, exception.Location.Line);
        Assert.Contains("MermaidLimits.MaxNodes", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a sequence participant metadata alias that is short as source but long once converted (a list
    ///     of 9,000 <c>1e20</c> numbers, about 45,000 characters, becomes a 197,999-character description) fails at
    ///     open with <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_SequenceMetadataAliasList_IsALocatedLimit()
    {
        // Arrange
        var text = "sequenceDiagram\n    participant A@{ \"alias\": [" + string.Join(",", Enumerable.Repeat("1e20", 9_000)) + "] }\n    A->>A: x\n";

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(2, exception.Location.Line);
        Assert.Contains("MermaidLimits.MaxLabelLength", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a sequence message longer than <see cref="MermaidLimits.MaxLabelLength"/> fails at open.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_SequenceLongMessage_IsALocatedLimit()
    {
        // Arrange
        var text = "sequenceDiagram\n    A->>B: " + string.Join(' ', Enumerable.Repeat("lorem", 1_000)) + "\n";

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(2, exception.Location.Line);
    }

    /// <summary>
    ///     Validates that wrapping long words (Mermaid's hyphenation, quadratic in the word) renders within the default
    ///     layout work and fails with a located limit under a small one.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_SequenceWrapBomb_RendersOrHitsTheWorkLimit()
    {
        // Arrange: wrapped messages and notes of one long word each
        var word = new string('w', 4_000);
        var text = "---\nconfig:\n  wrap: true\n---\nsequenceDiagram\n" + string.Concat(Enumerable.Range(0, 4).Select(_ => $"    A->>B: {word}\n    Note over A: {word}\n"));
        var document = MermaidDocument.Parse(text);
        var small = new MermaidRenderOptions { Limits = new MermaidLimits { MaxLayoutWork = 100_000 } };

        // Act
        var size = document.Measure(Tenth, TestContext.Current.CancellationToken);
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(small, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(size.Height > 100);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
    }

    /// <summary>
    ///     Validates that a storm of nested activations and blocks renders within the default layout work, fails with the
    ///     limit under a small one, and stops on a canceled token.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_SequenceActivationStorm_RendersHitsTheLimitOrCancels()
    {
        // Arrange
        var text = new StringBuilder("sequenceDiagram\n");
        for (var i = 0; i < 300; i++)
        {
            text.Append("    A->>+B: call ").Append(i).Append('\n');
        }

        for (var i = 0; i < 300; i++)
        {
            text.Append("    B-->>-A: return ").Append(i).Append('\n');
        }

        var document = MermaidDocument.Parse(text.ToString());
        var small = new MermaidRenderOptions { Limits = new MermaidLimits { MaxLayoutWork = 10_000 } };
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act
        var size = document.Measure(Tenth, TestContext.Current.CancellationToken);
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(small, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(size.Height > 1_000);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(1, exception.Location.Line);
        Assert.Throws<OperationCanceledException>(() => document.Measure(Tenth, canceled.Token));
    }

    /// <summary>
    ///     Validates that every sequence fixture renders the same pixels twice.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_SequenceFixtures_RenderDeterministically()
    {
        // Arrange
        var documents = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence"), "*.mmd")
            .Order(StringComparer.Ordinal)
            .Select(path => MermaidDocument.Open(path))
            .ToList();

        // Act
        var differing = documents.Where(document =>
        {
            using var first = document.Render(cancellationToken: TestContext.Current.CancellationToken);
            using var second = document.Render(cancellationToken: TestContext.Current.CancellationToken);
            return !MermaidTestHelpers.PixelsEqual(first, second);
        }).ToList();

        // Assert
        Assert.Empty(differing);
    }

    /// <summary>
    ///     Validates with a seeded mutation fuzzer over the sequence fixtures that opening and measuring only ever throw
    ///     the documented exceptions.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_MutatedSequenceFixtures_ThrowOnlyDocumentedExceptions()
    {
        // Arrange
        string[] tokens =
        [
            "->>", "-->>", "->", "-x", "--)", "<<->>", "-|\\", "//-", "+", "-", "()", ":", "loop L", "alt A", "else B", "end", "par P",
            "and Q", "par_over R", "rect rgb(1, 2, 3)", "rect", "opt O", "critical C", "option D", "break E", "box Aqua G", "participant X",
            "actor Y", "participant Z@{ \"type\": \"queue\" }", "participant W@{ \"type\": \"robot\" }", "create participant N",
            "destroy A", "activate A", "deactivate A", "Note over A,B: n", "note left of A: n", "autonumber", "autonumber 10 5",
            "links A: {\"x\": \"https://example.com\"}", "link A: L @ https://example.com", "properties A: {\"class\": \"c\"}",
            "title T", "%%", "\n", "  ", "<br>", "wrap:", "$$x$$", "#59;",
        ];
        var random = new Random(20261109);
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence"), "*.mmd")
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText)
            .ToList();
        var unexpected = new List<string>();

        // Act
        foreach (var fixture in fixtures)
        {
            for (var round = 0; round < 8; round++)
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
    ///     Validates that every class fixture renders the same pixels twice.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_ClassFixtures_RenderDeterministically()
    {
        // Arrange
        var documents = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "class"), "*.mmd")
            .Order(StringComparer.Ordinal)
            .Select(path => MermaidDocument.Open(path))
            .ToList();

        // Act
        var differing = documents.Where(document =>
        {
            using var first = document.Render(cancellationToken: TestContext.Current.CancellationToken);
            using var second = document.Render(cancellationToken: TestContext.Current.CancellationToken);
            return !MermaidTestHelpers.PixelsEqual(first, second);
        }).ToList();

        // Assert
        Assert.Empty(differing);
    }

    /// <summary>
    ///     Validates hostile class diagrams through the public API, fitted to 400 by 300 pixels: huge and tiny paddings,
    ///     spacings and font sizes, huge dashes, cardinalities on a self-loop and markup in a note each render, or end in a
    ///     located render failure.
    /// </summary>
    /// <param name="text">The diagram.</param>
    [Theory]
    [InlineData("---\nconfig:\n  class:\n    padding: 1e300\n---\nclassDiagram\nclass A {\n+int x\n}\nA --> B\n")]
    [InlineData("---\nconfig:\n  class:\n    padding: -1e300\n---\nclassDiagram\nclass A\nA --> B\n")]
    [InlineData("---\nconfig:\n  state:\n    nodeSpacing: 1e300\n    rankSpacing: 1e300\n---\nclassDiagram\nA --> B\nA --> C\n")]
    [InlineData("%%{init: {'themeVariables': {'fontSize': 1e300}}}%%\nclassDiagram\nclass A {\n+int x\n}\n")]
    [InlineData("%%{init: {'themeVariables': {'fontSize': 1e-308}}}%%\nclassDiagram\nclass A {\n+int x\n}\n")]
    [InlineData("classDiagram\nclass A\nstyle A stroke:#f00,stroke-dasharray:1e300\n")]
    [InlineData("classDiagram\nA \"1\" --> \"*\" A : self\n")]
    [InlineData("classDiagram\nnote \"<b>x</b><br><img src='x'>\"\nclass A {\n<<a>>\n}\n")]
    public void CanvasNetMermaid_Hardening_HostileClassDiagram_RendersOrFailsLocated(string text)
    {
        // Arrange
        var document = MermaidDocument.Parse(text);

        // Act
        var exception = Record.Exception(() =>
        {
            using var surface = document.Render(400, 300, cancellationToken: TestContext.Current.CancellationToken);
        });

        // Assert
        if (exception is not null)
        {
            var failure = Assert.IsType<MermaidRenderException>(exception);
            Assert.True(failure.Location.Line > 0, failure.Message);
        }
    }

    /// <summary>
    ///     Validates that a class with very many members renders or hits the work limit, and that the work is bounded.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_ClassWithManyMembers_RendersOrHitsTheWorkLimit()
    {
        // Arrange
        var text = "classDiagram\nclass Big {\n" + string.Concat(Enumerable.Range(0, 1_500).Select(i => $"+int field{i}\n")) + "}\n";
        var document = MermaidDocument.Parse(text);
        var small = new MermaidRenderOptions { Scale = 0.1, Limits = new MermaidLimits { MaxLayoutWork = 5_000 } };

        // Act
        var size = document.Measure(Tenth, TestContext.Current.CancellationToken);
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(small, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(size.Height > size.Width);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
    }

    /// <summary>
    ///     Validates with a seeded mutation fuzzer over the class fixtures that opening and measuring only ever throw the
    ///     documented exceptions.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_MutatedClassFixtures_ThrowOnlyDocumentedExceptions()
    {
        // Arrange
        string[] tokens =
        [
            "<|--", "*--", "o--", "-->", "--", "..>", "..|>", "..", "()--", "--()", "<|--|>", "\"1\"", "\"*\"", ":", " : label",
            "class X", "class Y {", "}", "{", "<<interface>>", "<<", ">>", "~T~", "~", "+", "-", "#", "$", "*", "()", "(a, b)",
            "namespace N {", "note \"n\"", "note for X \"n\"", "classDef c fill:#f00", "cssClass \"X\" c", ":::c", "style X fill:#0f0",
            "direction LR", "direction RL", "link X \"https://example.com\"", "click X call f()", "callback X \"f\"", "`B`", "[\"L\"]",
            "accTitle: t", "accDescr: d", "%%", "\n", "  ", "<br>", "\\", "#59;",
        ];
        var random = new Random(20261109);
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "class"), "*.mmd")
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText)
            .ToList();
        var unexpected = new List<string>();

        // Act
        foreach (var fixture in fixtures)
        {
            for (var round = 0; round < 8; round++)
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
