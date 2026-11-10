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
    ///     Gets diagrams of every type that has links (class <c>link</c> and <c>click ... href</c>, flowchart <c>click</c>
    ///     and <c>href</c>, sequence <c>link</c> and <c>links</c>, state <c>click</c> and <c>click ... href</c>), each with
    ///     encoded forms of unsafe links that a partial check lets through: a tab inside the scheme, a percent-encoded
    ///     letter, an escaped white space, a character reference, and mixed case.
    /// </summary>
    public static TheoryData<string> UnsafeLinkDiagrams =>
    [
        "classDiagram\nclass A\nclass B\nclass C\nlink A \"java\tscript:alert(1)\" \"tip\"\nclick B href \"%6Aavascript:alert(2)\" _self\n" +
        "link C \"JaVa&Tab;ScRiPt&colon;alert(3)\"\n",
        "flowchart TD\nA --> B\nclick A href \"java\tscript:alert(1)\" \"tip\" _blank\nclick B \"%6Aavascript:alert(2)\"\n",
        "sequenceDiagram\nparticipant A\nlink A: Docs @ %6Aavascript:alert(1)\nlinks A: {\"Run\": \"javascrip\\\\%74t:alert(2)\", \"Go\": \"JaVaScRiPt:alert(3)\"}\nA->>A: hi\n",
        "stateDiagram-v2\nA --> B\nB --> C\nC --> D\nclick A \"java\tscript:alert(1)\" \"tip\"\nclick B href \"%6Aavascript:alert(2)\"\n" +
        "click C href \"JaVa&Tab;ScRiPt&colon;alert(3)\"\nclick D \"&#106;avascript:alert(4)\" \"tip\"\n",
    ];

    /// <summary>
    ///     Validates through the public API that an encoded unsafe link appears in no output of a document: the diagram
    ///     renders, no report, hint, title or accessible title carries the link, and the only links the parsed diagram
    ///     keeps are the checked ones (<c>about:blank</c>; a flowchart keeps none).
    /// </summary>
    /// <param name="text">The diagram.</param>
    [Theory]
    [MemberData(nameof(UnsafeLinkDiagrams))]
    public void CanvasNetMermaid_Hardening_EncodedUnsafeLinks_AppearInNoOutput(string text)
    {
        // Arrange
        var outputs = new List<string>();
        var options = new MermaidRenderOptions
        {
            WarningHandler = report => outputs.AddRange([report.ToString(), report.Hint ?? string.Empty]),
            UnsupportedFeatureHandler = report => outputs.AddRange([report.ToString(), report.Hint ?? string.Empty]),
        };

        // Act
        var document = MermaidDocument.Parse(text);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        outputs.AddRange([document.Title ?? string.Empty, document.AccTitle ?? string.Empty]);

        // Assert
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.DoesNotContain(outputs, output => output.Contains("alert(", StringComparison.Ordinal));
        Assert.All(KeptLinks(document), link => Assert.Equal("about:blank", link));
    }

    /// <summary>
    ///     Gets the links a parsed diagram keeps: the links of the classes of a class diagram, the link texts of the
    ///     participants of a sequence diagram, and the URLs of the links and click statements of a state diagram (a
    ///     flowchart keeps none).
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The kept links.</returns>
    private static List<string?> KeptLinks(MermaidDocument document) => document.Diagram switch
    {
        global::DemaConsulting.CanvasNet.Mermaid.Class.ClassModel model => [.. model.Classes.Where(c => c.Link is not null).Select(c => c.Link)],
        global::DemaConsulting.CanvasNet.Mermaid.Sequence.SequenceModel model => [.. model.Actors.SelectMany(a => a.Links.Keys.Select(key => a.Links.TryGetValue(key, out var value) ? (string?)value : null))],
        global::DemaConsulting.CanvasNet.Mermaid.State.StateModel model =>
            [.. model.Links.Select(l => l.Url), .. model.RootDocument.Where(s => s.Stmt == "click").Select(s => s.Url)],
        _ => [],
    };

    /// <summary>
    ///     Validates that class namespaces count against <see cref="MermaidLimits.MaxNodes"/> with the classes: once 1,000
    ///     namespaces of one class each reach the limit, the next namespace fails at open, located at the namespace.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_ClassTooManyNamespaces_IsALocatedLimit()
    {
        // Arrange: 1,000 namespaces and their 1,000 classes reach the limit; the next namespace is one too many
        var text = "classDiagram\n" + string.Concat(Enumerable.Range(0, 1_001).Select(i => $"namespace n{i} {{\nclass c{i}\n}}\n"));

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));

        // Assert: namespace 1,000 (counted from zero) is on line 2 + 3 * 1,000
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(3_002, exception.Location.Line);
        Assert.Contains("MermaidLimits.MaxNodes", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that nested class namespaces whose names each fit <see cref="MermaidLimits.MaxLabelLength"/> but
    ///     whose qualified id (the drawn label when <c>class.hierarchicalNamespaces</c> is false) does not fail at open,
    ///     located at the inner namespace.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_ClassNestedNamespaceIds_AreALocatedLimit()
    {
        // Arrange: two 3,000-character names give a 6,001-character qualified id
        var text = $"classDiagram\nnamespace {new string('a', 3_000)} {{\nnamespace {new string('b', 3_000)} {{\nclass C\n}}\n}}\n";

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(3, exception.Location.Line);
        Assert.Contains("MermaidLimits.MaxLabelLength", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Gets class diagrams whose namespaces nest deeper than <see cref="MermaidLimits.MaxNestingDepth"/> (32) through
    ///     the dots in their names, alone or with nested <c>namespace</c> keywords, each with the line of the namespace
    ///     that is too deep.
    /// </summary>
    public static TheoryData<string, int> ClassDottedNamespacesTooDeep => new()
    {
        // 33 dotted parts, one level too many
        { "classDiagram\nnamespace " + string.Join('.', Enumerable.Range(0, 33).Select(i => $"n{i}")) + " {\nclass X\n}\n", 2 },

        // 1,000 dotted parts, which once overflowed the stack laying out the nested clusters
        { "classDiagram\nnamespace " + string.Join('.', Enumerable.Repeat("a", 1_000)) + " {\nclass X\n}\n", 2 },

        // 17 nested keywords of two parts each: the 17th reaches 34 levels, on line 18
        { "classDiagram\n" + string.Concat(Enumerable.Range(0, 17).Select(i => $"namespace a{i}.b{i} {{\n")) + "class X\n" + string.Concat(Enumerable.Repeat("}\n", 17)), 18 },
    };

    /// <summary>
    ///     Validates that class namespaces nested deeper than <see cref="MermaidLimits.MaxNestingDepth"/> through dotted
    ///     names fail at parse with a located limit, instead of nesting one cluster per part.
    /// </summary>
    /// <param name="text">The diagram.</param>
    /// <param name="line">The line of the namespace that is too deep.</param>
    [Theory]
    [MemberData(nameof(ClassDottedNamespacesTooDeep))]
    public void CanvasNetMermaid_Hardening_ClassDottedNamespacesTooDeep_AreALocatedLimit(string text, int line)
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(line, exception.Location.Line);
        Assert.Contains("MermaidLimits.MaxNestingDepth", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that class namespaces nested exactly <see cref="MermaidLimits.MaxNestingDepth"/> (32) levels deep,
    ///     through a dotted name alone or with nested <c>namespace</c> keywords, still render.
    /// </summary>
    /// <param name="keywords">The number of nested <c>namespace</c> keywords.</param>
    /// <param name="parts">The number of dotted parts in each name.</param>
    [Theory]
    [InlineData(1, 32)]
    [InlineData(16, 2)]
    public void CanvasNetMermaid_Hardening_ClassDottedNamespacesAtTheLimit_Render(int keywords, int parts)
    {
        // Arrange
        var text = "classDiagram\n" +
            string.Concat(Enumerable.Range(0, keywords).Select(k => "namespace " + string.Join('.', Enumerable.Range(0, parts).Select(p => $"n{k}x{p}")) + " {\n")) +
            "class X\n" + string.Concat(Enumerable.Repeat("}\n", keywords));
        var document = MermaidDocument.Parse(text);

        // Act
        using var surface = document.Render(400, 300, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(32, document.SubgraphCount);
    }

    /// <summary>
    ///     Validates that note theme variables a directive or the frontmatter sets to a number or a boolean, which Mermaid
    ///     stringifies into the note's style, render instead of failing.
    /// </summary>
    /// <param name="text">The diagram.</param>
    [Theory]
    [InlineData("%%{init: {'themeVariables': {'noteBkgColor': 5, 'noteBorderColor': true}}}%%\nclassDiagram\nnote \"x\"\n")]
    [InlineData("---\nconfig:\n  themeVariables:\n    noteBkgColor: false\n    noteBorderColor: 1.5\n---\nclassDiagram\nnote for A \"x\"\nclass A\n")]
    public void CanvasNetMermaid_Hardening_ClassNonStringNoteThemeVariables_Render(string text)
    {
        // Arrange
        var document = MermaidDocument.Parse(text);

        // Act
        var exception = Record.Exception(() =>
        {
            using var surface = document.Render(400, 300, cancellationToken: TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Validates that a labeled generic class whose name and label each fit <see cref="MermaidLimits.MaxLabelLength"/>
    ///     but whose drawn name (the label with the type) does not fails at open, located at the class.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_ClassLabelWithGenericType_IsALocatedLimit()
    {
        // Arrange: a 3,000-character type and a 3,000-character label give a 6,002-character drawn name
        var text = $"classDiagram\nclass A~{new string('T', 3_000)}~[\"{new string('L', 3_000)}\"]\n";

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(2, exception.Location.Line);
        Assert.Contains("MermaidLimits.MaxLabelLength", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Gets class diagrams that repeat a CSS class or styles on one class, as Mermaid does for a name listed again,
    ///     until the joined text passes <see cref="MermaidLimits.MaxLabelLength"/>, each with the line of the statement
    ///     that makes it too long.
    /// </summary>
    public static TheoryData<string, int> ClassRepeatedClassesAndStyles => new()
    {
        // cssClass with the class listed 1,000 times, three times: the CSS classes pass 4,096 characters on line 5
        { "classDiagram\nclass A\n" + string.Concat(Enumerable.Repeat($"cssClass \"{string.Join(',', Enumerable.Repeat("A", 1_000))}\" c\n", 3)), 5 },

        // classDef with the style class listed 2,000 times: its styles are added once per name
        { "classDiagram\nclass A:::c\nclassDef " + string.Join(',', Enumerable.Repeat("c", 2_000)) + " " + string.Join(',', Enumerable.Repeat("a:b", 1_000)) + "\n", 3 },

        // Two style classes of 2,099 characters on one class: its styles pass 4,096 characters at the second
        { "classDiagram\nclass A:::c\ncssClass \"A\" d\n" + string.Concat(new[] { "c", "d" }.Select(name => $"classDef {name} {string.Join(',', Enumerable.Repeat("a:b", 525))}\n")), 5 },

        // Two style statements of 2,099 characters on one class
        { "classDiagram\nclass A\n" + string.Concat(Enumerable.Repeat($"style A {string.Join(',', Enumerable.Repeat("a:b", 525))}\n", 2)), 4 },
    };

    /// <summary>
    ///     Validates that CSS classes and styles a class gets again and again (a class listed many times in a
    ///     <c>cssClass</c> statement, a style class listed many times in a <c>classDef</c> statement, several style classes
    ///     or style statements) fail at open, located at the statement that makes them too long, instead of multiplying
    ///     the styles without bound.
    /// </summary>
    /// <param name="text">The diagram.</param>
    /// <param name="line">The line of the statement that makes the text too long.</param>
    [Theory]
    [MemberData(nameof(ClassRepeatedClassesAndStyles))]
    public void CanvasNetMermaid_Hardening_ClassRepeatedClassesAndStyles_AreALocatedLimit(string text, int line)
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(line, exception.Location.Line);
        Assert.Contains("MermaidLimits.MaxLabelLength", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that namespaces chained by class names (a namespace holds a class named like the next namespace,
    ///     which nests that namespace in it, though no namespace is nested in the text) count against
    ///     <see cref="MermaidLimits.MaxNestingDepth"/>: a chain of 32 renders, and one of 40 fails when it is laid out,
    ///     located at the class that nests the 33rd namespace.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_ClassNamespacesChainedByClassNames_AreALocatedLimit()
    {
        // Arrange: namespace Ni holds class N(i+1), on line 3 + 3i
        static string Chain(int count) =>
            "classDiagram\n" + string.Concat(Enumerable.Range(0, count).Select(i => $"namespace N{i} {{\nclass N{i + 1}\n}}\n"));
        var accepted = MermaidDocument.Parse(Chain(32));
        var refused = MermaidDocument.Parse(Chain(40));

        // Act
        var size = accepted.Measure(Tenth, TestContext.Current.CancellationToken);
        var exception = Assert.Throws<MermaidRenderException>(() => refused.Measure(Tenth, TestContext.Current.CancellationToken));

        // Assert: class N33 in namespace N32 nests namespace N33 33 levels deep
        Assert.True(size.Width > 0 && size.Height > 0);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(99, exception.Location.Line);
        Assert.Contains("namespaces are nested deeper than 32 levels (MermaidLimits.MaxNestingDepth)", exception.Message, StringComparison.Ordinal);
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
    ///     Validates that every state fixture renders the same pixels twice.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_StateFixtures_RenderDeterministically()
    {
        // Arrange
        var documents = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state"), "*.mmd")
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
    ///     Validates hostile state diagrams through the public API, fitted to 400 by 300 pixels: huge spacings and font
    ///     sizes, huge dashes, self-loops on states and composite states, notes on composite states, markup in nested
    ///     notes, composite states inside each other, and a note on a state that appears nowhere else each render, or end
    ///     in a located render failure.
    /// </summary>
    /// <param name="text">The diagram.</param>
    [Theory]
    [InlineData("---\nconfig:\n  state:\n    nodeSpacing: 1e300\n    rankSpacing: 1e300\n---\nstateDiagram-v2\nA --> B\nA --> C\n")]
    [InlineData("%%{init: {'themeVariables': {'fontSize': 1e300}}}%%\nstateDiagram-v2\nA --> B : go\n")]
    [InlineData("%%{init: {'themeVariables': {'fontSize': 1e-308}}}%%\nstateDiagram-v2\nA --> B : go\nB : one\nB : two\n")]
    [InlineData("stateDiagram-v2\nA\nstyle A stroke:#f00,stroke-dasharray:1e300\n")]
    [InlineData("stateDiagram-v2\nA --> A : self\nstate C {\n  x\n}\nC --> C\n")]
    [InlineData("stateDiagram-v2\nstate C {\n  a --> b\n  note right of a : <b>x</b><script>y</script><img src='x'>\n}\nnote left of C : outer\n")]
    [InlineData("stateDiagram-v2\nstate A {\n  B\n}\nstate B {\n  A\n}\n")]
    [InlineData("stateDiagram-v2\nnote left of Ghost : boo\n")]
    [InlineData("stateDiagram-v2\nstate F <<fork>>\nstate J <<join>>\nstate C <<choice>>\nF --> J\nJ --> C\nC --> F\n")]
    public void CanvasNetMermaid_Hardening_HostileStateDiagram_RendersOrFailsLocated(string text)
    {
        // Arrange
        var document = MermaidDocument.Parse(text);

        // Act
        var exception = Record.Exception(() =>
        {
            using var surface = document.Render(400, 300, cancellationToken: TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.Equal("state", document.DiagramType);
        if (exception is not null)
        {
            var failure = Assert.IsType<MermaidRenderException>(exception);
            Assert.True(failure.Location.Line > 0, failure.Message);
        }
    }

    /// <summary>
    ///     Validates that notes and their groups count against <see cref="MermaidLimits.MaxNodes"/>: 700 states with a
    ///     note each are 2,100 nodes, which fail at open at the note that makes the 2,001st, located at its line.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_StateManyNotes_IsALocatedNodeLimit()
    {
        // Arrange
        var text = "stateDiagram-v2\n" + string.Concat(Enumerable.Range(0, 700).Select(i => $"s{i}\nnote left of s{i} : n\n"));

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(1_335, exception.Location.Line);
        Assert.Contains("MermaidLimits.MaxNodes", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that concurrent regions count as nesting levels: 16 composite states that each hold the next in a
    ///     region are 32 levels and render, and 17 fail at open at the composite state of the 33rd level, though the
    ///     grammar allows 17 nested composite states.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_StateNestedRegions_IsALocatedNestingLimit()
    {
        // Arrange
        var accepted = MermaidDocument.Parse(NestedRegions(16));

        // Act
        var size = accepted.Measure(Tenth, TestContext.Current.CancellationToken);
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(NestedRegions(17)));

        // Assert
        Assert.Equal(16, accepted.SubgraphCount);
        Assert.True(size.Width > 0 && size.Height > 0);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(18, exception.Location.Line);
        Assert.Contains("MermaidLimits.MaxNestingDepth", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that composite states nested through names (composite state <c>Si</c> names <c>S(i+1)</c>, declared
    ///     again at the top with its own state, keeping the parent it was first given) count against
    ///     <see cref="MermaidLimits.MaxNestingDepth"/>, though the text nests them one level deep: a chain of 32 renders,
    ///     and chains of 40 and 1,700 fail when laid out, located at the state of the 33rd level.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_StateNestedThroughNames_IsALocatedNestingLimit()
    {
        // Arrange: state Si { S(i+1) } on lines 2 + 3i to 4 + 3i
        static string Chain(int count) =>
            "stateDiagram-v2\n" + string.Concat(Enumerable.Range(0, count).Select(i => $"state S{i} {{\nS{i + 1}\n}}\n"));
        var accepted = MermaidDocument.Parse(Chain(32));

        // Act
        var size = accepted.Measure(Tenth, TestContext.Current.CancellationToken);
        var exceptions = new[] { 40, 1_700 }
            .Select(count => Assert.Throws<MermaidRenderException>(() => MermaidDocument.Parse(Chain(count)).Measure(Tenth, TestContext.Current.CancellationToken)))
            .ToList();

        // Assert: S33, declared on line 101, is the first state inside 33 composite states
        Assert.True(size.Width > 0 && size.Height > 0);
        Assert.All(exceptions, exception =>
        {
            Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
            Assert.Equal(101, exception.Location.Line);
            Assert.Contains("MermaidLimits.MaxNestingDepth", exception.Message, StringComparison.Ordinal);
        });
    }

    /// <summary>
    ///     Gets state and ER diagrams that repeat CSS classes or styles on one state, entity or style class, as Mermaid
    ///     does for an id listed or a class defined again, until a joined text passes
    ///     <see cref="MermaidLimits.MaxLabelLength"/>, each with the line of the statement that makes it too long and the
    ///     length it reaches there.
    /// </summary>
    public static TheoryData<string, int, int> StateAndErRepeatedClassesAndStyles => new()
    {
        // The review's state shape: four definitions of 1,000 styles of one class, two class statements listing the
        // state 2,000 times, and 300 mentions; the styles of the class pass 4,096 characters at the second definition
        {
            "stateDiagram-v2\n" + string.Concat(Enumerable.Repeat($"classDef c {string.Join(',', Enumerable.Repeat("a:b", 1_000))}\n", 4)) +
                string.Concat(Enumerable.Repeat($"class {string.Join(',', Enumerable.Repeat("A", 2_000))} c\n", 2)) + string.Concat(Enumerable.Repeat("A\n", 300)),
            3,
            4_099
        },

        // Two class statements listing a state 2,000 times: its CSS classes (with statediagram-state) pass 4,096
        // characters at the second
        { "stateDiagram-v2\n" + string.Concat(Enumerable.Repeat($"class {string.Join(',', Enumerable.Repeat("A", 2_000))} c\n", 2)) + "A\n", 3, 4_098 },

        // Two style classes of 2,099 characters on one state: its compiled styles pass 4,096 characters, located at the
        // statement that gave it the second class
        {
            "stateDiagram-v2\n" + string.Concat(new[] { "c", "d" }.Select(name => $"classDef {name} {string.Join(',', Enumerable.Repeat("a:b", 525))}\n")) +
                "class A c\nclass A d\n" + string.Concat(Enumerable.Repeat("A\n", 300)),
            5,
            4_099
        },

        // The review's ER shape: a style statement listing the entity 2,000 times with 450 styles
        { "erDiagram\nA\nstyle " + string.Join(',', Enumerable.Repeat("A", 2_000)) + " " + string.Join(',', Enumerable.Repeat("fill:red", 450)) + "\n", 3, 4_103 },

        // An ER classDef statement listing the class 2,000 times with 1,000 styles
        { "erDiagram\nA:::c\nclassDef " + string.Join(',', Enumerable.Repeat("c", 2_000)) + " " + string.Join(',', Enumerable.Repeat("a:b", 1_000)) + "\n", 3, 4_099 },
    };

    /// <summary>
    ///     Validates that CSS classes and styles a state, an entity or a style class gets again and again (an id listed
    ///     many times in a <c>class</c>, <c>style</c> or <c>classDef</c> statement, a style class defined many times,
    ///     several style classes on one state) fail at open, located at the statement that makes them too long, instead of
    ///     multiplying the styles without bound (for a state, with every mention of it).
    /// </summary>
    /// <param name="text">The diagram.</param>
    /// <param name="line">The line of the statement that makes the text too long.</param>
    /// <param name="length">The length the joined text reaches there.</param>
    [Theory]
    [MemberData(nameof(StateAndErRepeatedClassesAndStyles))]
    public void CanvasNetMermaid_Hardening_StateAndErRepeatedClassesAndStyles_AreALocatedLimit(string text, int line, int length)
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(line, exception.Location.Line);
        Assert.Contains($"a label is {length} characters long", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MermaidLimits.MaxLabelLength", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the descriptions drawn under a state's title are checked joined: 457 short descriptions of one
    ///     state make a block of 4,099 characters, which fails at open at the line that makes it too long.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_StateManyDescriptions_IsALocatedLabelLimit()
    {
        // Arrange
        var text = "stateDiagram-v2\n" + string.Concat(Enumerable.Repeat("S : abcd\n", 500));

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(458, exception.Location.Line);
        Assert.Contains("4099 characters", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a state diagram with very many transitions renders or hits the work limit, and that the work is
    ///     bounded.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_StateLongChain_RendersOrHitsTheWorkLimit()
    {
        // Arrange
        var text = "stateDiagram-v2\n" + string.Concat(Enumerable.Range(0, 300).Select(i => $"s{i} --> s{i + 1} : t{i}\n"));
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
    ///     Validates with a seeded mutation fuzzer over the state fixtures that opening and measuring only ever throw the
    ///     documented exceptions.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_MutatedStateFixtures_ThrowOnlyDocumentedExceptions()
    {
        // Arrange
        string[] tokens =
        [
            "-->", "--", "[*]", ":", " : label", "state X", "state Y {", "}", "{", "<<fork>>", "<<join>>", "<<choice>>",
            "[[fork]]", "state \"T\" as Z", "note left of X : n", "note right of Y\n  n\nend note", "end note", "note \"f\" as N",
            "direction LR", "direction BT", "classDef c fill:#f00", "class X c", ":::c", "style X fill:#0f0",
            "click X \"https://example.com\" \"t\"", "hide empty description", "scale 100 width", "accTitle: t", "accDescr: d",
            "%%", "\n", "  ", "<br>", "\\", "#59;",
        ];
        var random = new Random(20261110);
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state"), "*.mmd")
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
    ///     Validates that every ER fixture renders the same pixels twice.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_ErFixtures_RenderDeterministically()
    {
        // Arrange
        var documents = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "er"), "*.mmd")
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
    ///     Validates hostile ER diagrams through the public API, fitted to 400 by 300 pixels: huge spacings, paddings and
    ///     font sizes, huge dashes, self-relationships, the <c>md_parent</c> cardinality, a generic type that takes most
    ///     of its line, comments over several lines, markup in texts, duplicate subgraph ids, relationships to subgraphs,
    ///     an entity named like a later subgraph and a subgraph that holds a relationship to itself each render, or end in
    ///     a located render failure.
    /// </summary>
    /// <param name="text">The diagram.</param>
    [Theory]
    [InlineData("---\nconfig:\n  er:\n    nodeSpacing: 1e300\n    rankSpacing: 1e300\n---\nerDiagram\nA ||--o{ B : r\nA ||--o{ C : r\n")]
    [InlineData("---\nconfig:\n  er:\n    diagramPadding: 1e300\n    entityPadding: 1e300\n    minEntityWidth: 1e300\n---\nerDiagram\nA ||--o{ B : r\nB {\n  int x\n}\n")]
    [InlineData("%%{init: {'themeVariables': {'fontSize': 1e300}}}%%\nerDiagram\nA ||--o{ B : go\nB {\n  int x PK \"c\"\n}\n")]
    [InlineData("%%{init: {'themeVariables': {'fontSize': 1e-308}}}%%\nerDiagram\nA ||--o{ B : go\nB {\n  int x PK \"c\"\n}\n")]
    [InlineData("erDiagram\nA {\n  int x\n  int y\n}\nstyle A stroke:#f00,stroke-width:1e300,stroke-dasharray:1e300\n")]
    [InlineData("erDiagram\nA ||--o{ A : self\nA u--|| B : parent\nB }|..|{ B : again\n")]
    [InlineData("erDiagram\nR {\n  List~int~ a, Map~x~ b\n  string c \"first line\n  second line\"\n  string d \"<b>x</b><script>y</script>\"\n}\n")]
    [InlineData("erDiagram\nsubgraph S\n  A\nend\nsubgraph S\n  B\nend\nC ||--o{ S : r\nS ||--|| A : s\n")]
    [InlineData("erDiagram\nA ||--|| B : r\nsubgraph A\n  C\nend\n")]
    [InlineData("erDiagram\nsubgraph S\n  A ||--o{ S : r\nend\n")]
    public void CanvasNetMermaid_Hardening_HostileErDiagram_RendersOrFailsLocated(string text)
    {
        // Arrange
        var document = MermaidDocument.Parse(text);

        // Act
        var exception = Record.Exception(() =>
        {
            using var surface = document.Render(400, 300, cancellationToken: TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.Equal("er", document.DiagramType);
        if (exception is not null)
        {
            var failure = Assert.IsType<MermaidRenderException>(exception);
            Assert.True(failure.Location.Line > 0, failure.Message);
        }
    }

    /// <summary>
    ///     Validates that more entities than the default <see cref="MermaidLimits.MaxNodes"/> fail at open, located at the
    ///     line of the first entity over the limit.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_ErManyEntities_IsALocatedNodeLimit()
    {
        // Arrange
        var text = "erDiagram\n" + string.Join("\n", Enumerable.Range(0, 2_001).Select(i => $"E{i}")) + "\n";

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(2_002, exception.Location.Line);
        Assert.Contains("MermaidLimits.MaxNodes", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that subgraphs nested to the default <see cref="MermaidLimits.MaxNestingDepth"/> render, and that one
    ///     level more fails at open, located at the subgraph that is one too deep.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_ErNestedSubgraphs_IsALocatedNestingLimit()
    {
        // Arrange
        static string Nested(int depth) =>
            "erDiagram\n" + string.Concat(Enumerable.Range(0, depth).Select(i => $"subgraph S{i}\n")) + "A\n" +
            string.Concat(Enumerable.Repeat("end\n", depth));
        var accepted = MermaidDocument.Parse(Nested(32));

        // Act
        var size = accepted.Measure(Tenth, TestContext.Current.CancellationToken);
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(Nested(33)));

        // Assert
        Assert.Equal(32, accepted.SubgraphCount);
        Assert.True(size.Width > 0 && size.Height > 0);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(34, exception.Location.Line);
        Assert.Contains("MermaidLimits.MaxNestingDepth", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that subgraphs nested through names (subgraph <c>Si</c> names <c>S(i+1)</c>, declared after it)
    ///     count against <see cref="MermaidLimits.MaxNestingDepth"/>, though the text nests none: a chain of 32 renders,
    ///     and a chain of 40 fails when laid out, located at the subgraph of the 33rd level.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_ErNestedThroughNames_IsALocatedNestingLimit()
    {
        // Arrange: subgraph Si with S(i+1) on lines 2 + 3i to 4 + 3i
        static string Chain(int count) =>
            "erDiagram\n" + string.Concat(Enumerable.Range(0, count).Select(i => $"subgraph S{i}\nS{i + 1}\nend\n"));
        var accepted = MermaidDocument.Parse(Chain(32));
        var refused = MermaidDocument.Parse(Chain(40));

        // Act
        var size = accepted.Measure(Tenth, TestContext.Current.CancellationToken);
        var exception = Assert.Throws<MermaidRenderException>(() => refused.Measure(Tenth, TestContext.Current.CancellationToken));

        // Assert: subgraph S33, on line 101, is the first inside 33 subgraphs
        Assert.True(size.Width > 0 && size.Height > 0);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(101, exception.Location.Line);
        Assert.Contains("subgraphs are nested deeper than 32 levels (MermaidLimits.MaxNestingDepth)", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an attribute comment longer than the default <see cref="MermaidLimits.MaxLabelLength"/> fails at
    ///     open, located at its line.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_ErLongComment_IsALocatedLabelLimit()
    {
        // Arrange
        var comment = new string('x', 4_097);

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse($"erDiagram\nT {{\n  int a\n  int b \"{comment}\"\n}}\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(4, exception.Location.Line);
        Assert.Contains("MermaidLimits.MaxLabelLength", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an entity with very many attributes renders, measured and drawn in bounded work.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_ErManyAttributes_Render()
    {
        // Arrange
        var text = "erDiagram\nT {\n" + string.Concat(Enumerable.Range(0, 1_000).Select(i => $"  int a{i} PK \"c{i}\"\n")) + "}\nT ||--o{ U : r\n";
        var document = MermaidDocument.Parse(text);

        // Act
        var size = document.Measure(Tenth, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(size.Height > size.Width);
    }

    /// <summary>
    ///     Validates with a seeded mutation fuzzer over the ER fixtures that opening and measuring only ever throw the
    ///     documented exceptions.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Hardening_MutatedErFixtures_ThrowOnlyDocumentedExceptions()
    {
        // Arrange
        string[] tokens =
        [
            "||--o{", "}|..|{", "|o--o|", "u--||", "one or more", "to", "optionally to", ":", " : role", " : \"two words\"",
            "X {", "}", "{", "int a PK", "string b FK, UK \"c\"", "List~int~", "`b.c`", "\"Quoted Name\"", "[\"Alias\"]",
            "subgraph S", "subgraph S2[\"T\"]", "end", "direction LR", "direction BT", "classDef c fill:#f00", "class X c",
            ":::c", "style X fill:#0f0", "accTitle: t", "accDescr: d", "%%", "\n", "  ", "<br>", "\\", "#59;",
        ];
        var random = new Random(20261111);
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "er"), "*.mmd")
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
    ///     Builds composite states nested through their concurrent regions: each holds the next one, then a divider and a
    ///     state, so each adds two nesting levels.
    /// </summary>
    /// <param name="count">The number of composite states.</param>
    /// <returns>The text, with composite state <c>i</c> on line <c>i + 2</c>.</returns>
    private static string NestedRegions(int count) =>
        "stateDiagram-v2\n" +
        string.Concat(Enumerable.Range(0, count).Select(i => $"state C{i} {{\n")) +
        "x\n" +
        string.Concat(Enumerable.Range(0, count).Reverse().Select(i => $"--\nz{i}\n}}\n"));

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
