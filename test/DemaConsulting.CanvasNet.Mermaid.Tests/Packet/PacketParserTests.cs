using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Packet;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Packet;

/// <summary>
///     Unit tests for <c>PacketParser</c> and the database it fills (<c>PacketDiagram</c>): the tokens, the grammar and its
///     located errors, the value converters, Mermaid's database validation with its messages, the metadata, the limits,
///     and the row split of <c>PacketDiagram.Rows</c> (Mermaid's <c>getNextFittingBlock</c>).
/// </summary>
public sealed class PacketParserTests
{
    /// <summary>The limits of a row split without a part limit (Mermaid's 10,000-row cap still applies).</summary>
    private static readonly MermaidLimits Unbounded = new() { MaxNodes = int.MaxValue };

    /// <summary>
    ///     Validates the three block forms: a range, a single bit and a bit count, each filled in as Mermaid's database
    ///     fills it (a missing start is the next bit, a missing end the start plus the count less one).
    /// </summary>
    [Fact]
    public void PacketParser_Parse_BlockForms_FillStartEndAndBits()
    {
        // Act
        var diagram = Parse("packet-beta\n0-3: \"a\"\n4: 'b'\n+8: \"c\"\n13 - 13 : \"d\"\n");

        // Assert
        Assert.Equal(
            [(0.0, 3.0, 4.0, "a"), (4.0, 4.0, 1.0, "b"), (5.0, 12.0, 8.0, "c"), (13.0, 13.0, 1.0, "d")],
            diagram.Blocks.Select(block => (block.Start, block.End, block.Bits, block.Label)));
        Assert.Equal([2, 3, 4, 5], diagram.Blocks.Select(block => block.Location.Line));
    }

    /// <summary>
    ///     Validates the two header keywords: <c>packet</c> must be followed by white space or <c>%%</c>, while
    ///     <c>packet-beta</c> matches before any character, and a statement may follow the header on its line.
    /// </summary>
    [Fact]
    public void PacketParser_Parse_Keywords_FollowTheTokenBuilder()
    {
        // Act
        var comment = Parse("packet%% c\n0: \"a\"\n");
        var beta = Parse("packet-beta%% c\n0: \"a\"\n");
        var sameLine = Parse("packet 0-3: \"a\"\n");
        var glued = Assert.Throws<MermaidSyntaxException>(() => Parse("packets\n0: \"a\"\n"));
        var betaGlued = Assert.Throws<MermaidSyntaxException>(() => Parse("packet-betas\n0: \"a\"\n"));

        // Assert
        Assert.Equal((1, 1, 1), (comment.Blocks.Count, beta.Blocks.Count, sameLine.Blocks.Count));
        Assert.Equal((1, 1), (glued.Location.Line, glued.Location.Column));
        Assert.Equal((1, 12), (betaGlued.Location.Line, betaGlued.Location.Column));
        Assert.Contains("unrecognized text", betaGlued.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the <c>INT</c> terminal: a zero alone, no leading zero, and the digits of a longer number giving back
    ///     their last digit before a dot, so that the dot is the first character no token matches.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="line">The line of the error.</param>
    /// <param name="column">The column of the error.</param>
    /// <param name="expected">The expected token, or null for a lexical error.</param>
    [Theory]
    [InlineData("packet\n05: \"a\"\n", 2, 2, ":")]
    [InlineData("packet\n00: \"a\"\n", 2, 2, ":")]
    [InlineData("packet\n0-10.5: \"a\"\n", 2, 5, null)]
    [InlineData("packet\n0-1.0: \"a\"\n", 2, 3, null)]
    [InlineData("packet\n0.5: \"a\"\n", 2, 2, null)]
    public void PacketParser_Parse_IntTerminal_BacktracksAsTheRegularExpression(string text, int line, int column, string? expected)
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse(text));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, line, column), (exception.Code, exception.Location.Line, exception.Location.Column));
        if (expected is null)
        {
            Assert.Contains("unrecognized text", exception.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal([expected], exception.Expected);
        }
    }

    /// <summary>
    ///     Validates that a grammar error is located at the token the grammar does not accept, with the tokens chevrotain
    ///     lists and a hint.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="line">The line of the error.</param>
    /// <param name="column">The column of the error.</param>
    /// <param name="expected">The expected tokens, separated by commas.</param>
    [Theory]
    [InlineData("packet\n0 \"a\"\n", 2, 3, ":")]
    [InlineData("packet\n0:\n", 2, 3, "STRING")]
    [InlineData("packet\n0-: \"a\"\n", 2, 3, "INT")]
    [InlineData("packet\n+-1: \"a\"\n", 2, 2, "INT")]
    [InlineData("packet\n0: \"a\" 1: \"b\"\n", 2, 8, "NEWLINE,EOF")]
    [InlineData("packet\n: \"a\"\n", 2, 1, "EOF")]
    [InlineData("packet\npacket\n", 2, 1, "EOF")]
    public void PacketParser_Parse_GrammarError_IsLocatedWithTheExpectedTokens(string text, int line, int column, string expected)
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse(text));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, line, column), (exception.Code, exception.Location.Line, exception.Location.Column));
        Assert.Equal(expected.Split(','), exception.Expected);
        Assert.False(string.IsNullOrEmpty(exception.Hint));
    }

    /// <summary>
    ///     Validates Mermaid's database errors, in its order of checks: an end before the start (before contiguity), a
    ///     block that does not start at the next bit (the end of a single bit written as its start), and a zero bit count;
    ///     each located at its statement with Mermaid's message and a hint.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="line">The line of the failing statement.</param>
    /// <param name="message">Mermaid's message.</param>
    [Theory]
    [InlineData("packet\n0-3: \"a\"\n9-6: \"b\"\n", 3, "Packet block 9 - 6 is invalid. End must be greater than start.")]
    [InlineData("packet\n0-16: \"a\"\n18-20: \"b\"\n", 3, "Packet block 18 - 20 is not contiguous. It should start from 17.")]
    [InlineData("packet\n0-16: \"a\"\n18: \"b\"\n", 3, "Packet block 18 - 18 is not contiguous. It should start from 17.")]
    [InlineData("packet\n1-3: \"a\"\n", 2, "Packet block 1 - 3 is not contiguous. It should start from 0.")]
    [InlineData("packet\n0: \"a\"\n0: \"b\"\n", 3, "Packet block 0 - 0 is not contiguous. It should start from 1.")]
    [InlineData("packet\n0-7: \"a\"\n+0: \"b\"\n", 3, "Packet block 8 is invalid. Cannot have a zero bit field.")]
    [InlineData("packet\n0: \"a\"\n1234567890123456789012345: \"b\"\n", 3, "Packet block 1.2345678901234568e+24 - 1.2345678901234568e+24 is not contiguous. It should start from 1.")]
    public void PacketParser_Parse_InvalidBlock_FailsLocatedWithMermaidsMessage(string text, int line, string message)
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse(text));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, line, 1), (exception.Code, exception.Location.Line, exception.Location.Column));
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(exception.Hint));
    }

    /// <summary>
    ///     Validates the title and accessibility texts: converted (trimmed, spaces collapsed, the lines of a braced
    ///     description trimmed), the last of each winning, and sanitized as <c>commonDb</c> sanitizes them; and the
    ///     metadata of the document (the blocks as nodes, no edges, subgraphs or direction).
    /// </summary>
    [Fact]
    public void PacketParser_Parse_TitleAccessibilityAndMetadata_AreRead()
    {
        // Act
        var diagram = Parse("packet title First\n  title   A   <b>B</b>  <script>x</script>\naccTitle:   T  \naccDescr {\n   one\n     two\n}\n0: \"a\"\n+4: \"b\"\n");

        // Assert
        Assert.Equal(("A <b>B</b> ", "T", "one\ntwo"), (diagram.DiagramTitle, diagram.AccessibleTitle, diagram.AccessibleDescription));
        Assert.Equal(("A <b>B</b> ", "T", (string?)null), (diagram.Title, diagram.AccTitle, diagram.Direction));
        Assert.Equal((2, 0, 0), (diagram.NodeCount, diagram.EdgeCount, diagram.SubgraphCount));
    }

    /// <summary>
    ///     Validates the title Mermaid draws: the frontmatter title (sanitized) until a non-empty title statement replaces
    ///     it; the diagram's own title stays empty without one.
    /// </summary>
    [Fact]
    public void PacketParser_Parse_FrontmatterTitle_IsTheDrawnTitleFallback()
    {
        // Act
        var fallback = Parse("---\ntitle: From <i>front</i>\n---\npacket\n0: \"a\"\n");
        var own = Parse("---\ntitle: From front\n---\npacket title Own\n0: \"a\"\n");

        // Assert
        Assert.Equal((string.Empty, "From <i>front</i>", (string?)null), (fallback.DiagramTitle, fallback.DrawnTitle, fallback.Title));
        Assert.Equal(("Own", "Own"), (own.DiagramTitle, own.DrawnTitle));
    }

    /// <summary>
    ///     Validates that a title statement with a value replaces the frontmatter title even when sanitizing empties it,
    ///     while a bare <c>title</c> keeps the frontmatter title, as Mermaid's <c>populateCommonDb</c> tests the value
    ///     before sanitizing.
    /// </summary>
    [Fact]
    public void PacketParser_Parse_TitleSanitizedToEmpty_ReplacesTheFrontmatterTitle()
    {
        // Act
        var emptied = Parse("---\ntitle: FM\n---\npacket\ntitle <script>x</script>\n0: \"a\"\n");
        var bare = Parse("---\ntitle: FM\n---\npacket\ntitle\n0: \"a\"\n");

        // Assert
        Assert.Equal((string.Empty, string.Empty, (string?)null, true), (emptied.DiagramTitle, emptied.DrawnTitle, emptied.Title, emptied.IsTitleSet));
        Assert.Equal(("FM", false), (bare.DrawnTitle, bare.IsTitleSet));
    }

    /// <summary>
    ///     Validates that labels are kept as Langium's string converter gives them (escapes, quotes, a label over two
    ///     lines), without sanitizing them.
    /// </summary>
    [Fact]
    public void PacketParser_Parse_Labels_FollowConvertString()
    {
        // Act
        var diagram = Parse("packet\n0: \"a\\nb\\tc\\q\"\n1: 'it\\'s <b>x</b>'\n2: \"first\nsecond\"\n3: \"\"\n");

        // Assert
        Assert.Equal(["a\nb\tcq", "it's <b>x</b>", "first\nsecond", string.Empty], diagram.Blocks.Select(block => block.Label));
    }

    /// <summary>
    ///     Validates that a block statement over <see cref="MermaidLimits.MaxNodes"/> fails at that statement.
    /// </summary>
    [Fact]
    public void PacketParser_Parse_TooManyBlocks_IsALocatedLimit()
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("packet\n0: \"a\"\n1: \"b\"\n2: \"c\"\n", new MermaidLimits { MaxNodes = 2 }));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 4), (exception.Code, exception.Location.Line));
        Assert.Contains("MaxNodes", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that every text path is bounded by <see cref="MermaidLimits.MaxLabelLength"/>: a label, a title and
    ///     the frontmatter title (located at the frontmatter).
    /// </summary>
    [Fact]
    public void PacketParser_Parse_LongTexts_AreLocatedLimits()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 8 };

        // Act
        var label = Assert.Throws<MermaidSyntaxException>(() => Parse("packet\n0: \"a long label\"\n", limits));
        var title = Assert.Throws<MermaidSyntaxException>(() => Parse("packet\ntitle A long title\n0: \"a\"\n", limits));
        var frontmatter = Assert.Throws<MermaidSyntaxException>(() => Parse("---\ntitle: A long title\n---\npacket\n0: \"a\"\n", limits));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 2), (label.Code, label.Location.Line));
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 2), (title.Code, title.Location.Line));
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, true), (frontmatter.Code, frontmatter.Location.IsKnown));
    }

    /// <summary>
    ///     Validates the row split of Mermaid's <c>getNextFittingBlock</c>: a block that passes a row end is cut there, its
    ///     parts keep the label and Mermaid's bit counts (one less than their bits), and a part that ends at a row's last
    ///     bit completes the row.
    /// </summary>
    [Fact]
    public void PacketDiagram_Rows_SplitBlocksAsMermaid()
    {
        // Arrange
        var diagram = Parse("packet\n0-10: \"test\"\n11-90: \"multiple\"\n91-95: \"cut\"\n96: \"bit\"\n");

        // Act
        var rows = diagram.Rows(32, MermaidLimits.Default, Meter());

        // Assert
        Assert.Equal(
            [
                [(0.0, 10.0, 11.0, "test"), (11.0, 31.0, 20.0, "multiple")],
                [(32.0, 63.0, 31.0, "multiple")],
                [(64.0, 90.0, 26.0, "multiple"), (91.0, 95.0, 5.0, "cut")],
                [(96.0, 96.0, 1.0, "bit")],
            ],
            rows.Select(row => row.Select(part => (part.Start, part.End, part.Bits, part.Label)).ToList()).ToList());
        Assert.All(rows[1], part => Assert.Equal(3, part.Location.Line));
    }

    /// <summary>
    ///     Validates the split for other row sizes: one bit per row (each part a row), zero bits per row (Mermaid's
    ///     10,000-row cap of empty parts), a negative size (no part at all), and a block much longer than the cap.
    /// </summary>
    [Fact]
    public void PacketDiagram_Rows_OtherRowSizes_SplitAsMermaid()
    {
        // Arrange
        var diagram = Parse("packet\n0-2: \"a\"\n3: \"b\"\n");
        var huge = Parse("packet\n0-99999999: \"huge\"\n");

        // Act
        var one = diagram.Rows(1, Unbounded, Meter());
        var zero = diagram.Rows(0, Unbounded, Meter());
        var negative = diagram.Rows(-3, Unbounded, Meter());
        var capped = huge.Rows(32, Unbounded, Meter());

        // Assert
        Assert.Equal([(0.0, 0.0, 0.0), (1.0, 1.0, 0.0), (2.0, 2.0, 0.0), (3.0, 3.0, 1.0)], one.Select(row => (row[0].Start, row[0].End, row[0].Bits)));
        Assert.Equal(PacketDiagram.MaxPacketSize, zero.Count);
        Assert.All(zero, row => Assert.Equal((0.0, -1.0, -1.0), (row[0].Start, row[0].End, row[0].Bits)));
        Assert.Empty(negative);
        Assert.Equal(PacketDiagram.MaxPacketSize, capped.Count);
        Assert.Equal((319968.0, 319999.0), (capped[^1][0].Start, capped[^1][0].End));
    }

    /// <summary>
    ///     Validates that a fractional row size that makes a part start after its end fails as Mermaid's
    ///     <c>getNextFittingBlock</c> does, located at the block statement.
    /// </summary>
    [Fact]
    public void PacketDiagram_Rows_PartStartAfterEnd_IsALocatedRenderFailure()
    {
        // Arrange
        var diagram = Parse("packet\n0: \"a\"\n1: \"b\"\n2-3: \"c\"\n");

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => diagram.Rows(1.5, Unbounded, Meter()));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.RenderFailed, 3), (exception.Code, exception.Location.Line));
        Assert.Contains("Block start 1.5 is greater than block end 1.", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the parts of the split count against <see cref="MermaidLimits.MaxNodes"/>, so a long range fails
    ///     at its statement long before Mermaid's 10,000 rows.
    /// </summary>
    [Fact]
    public void PacketDiagram_Rows_TooManyParts_IsALocatedLimit()
    {
        // Arrange
        var diagram = Parse("packet\n0: \"a\"\n1-99999999: \"huge\"\n");

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => diagram.Rows(32, new MermaidLimits { MaxNodes = 10 }, Meter()));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 3), (exception.Code, exception.Location.Line));
        Assert.Contains("MaxNodes", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the work the split charges: one unit per block and one per part, and the limit of the meter.
    /// </summary>
    [Fact]
    public void PacketDiagram_Rows_ChargesOnePerBlockAndPart()
    {
        // Arrange
        var diagram = Parse("packet\n0-40: \"a\"\n41: \"b\"\n");
        var meter = Meter();

        // Act
        diagram.Rows(32, MermaidLimits.Default, meter);
        var exception = Assert.Throws<MermaidRenderException>(() => diagram.Rows(32, MermaidLimits.Default, new LayoutWorkMeter(4, diagram.KeywordLocation, CancellationToken.None)));

        // Assert
        Assert.Equal(5, meter.Used);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
    }

    /// <summary>
    ///     Gets a work meter without a practical limit.
    /// </summary>
    /// <returns>The meter.</returns>
    private static LayoutWorkMeter Meter() => new(long.MaxValue, default, CancellationToken.None);

    /// <summary>
    ///     Parses a packet text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="limits">The limits, or the defaults.</param>
    /// <returns>The diagram.</returns>
    private static PacketDiagram Parse(string text, MermaidLimits? limits = null)
    {
        var effective = limits ?? MermaidLimits.Default;
        var source = Preprocessor.Run(text, null, effective);
        return PacketParser.Parse(source, DiagramDetector.Detect(source.Code, DetectorSettings.Default), effective);
    }
}
