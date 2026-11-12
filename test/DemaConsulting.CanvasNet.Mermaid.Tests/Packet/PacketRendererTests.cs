using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Packet;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Packet;

/// <summary>
///     Unit tests for <c>PacketRenderer</c>: the geometry of Mermaid's <c>draw</c> and <c>drawWord</c> (the view box,
///     the blocks, the bit numbers and the title row), the texts as the browser shows them, the paint of the themes, the
///     blocks too narrow to draw, and the work limit and cancellation.
/// </summary>
public sealed class PacketRendererTests
{
    /// <summary>
    ///     Validates the text the browser shows: Mermaid's entity placeholders are decoded, a reference the user wrote is
    ///     shown as written, and the white space is collapsed.
    /// </summary>
    [Fact]
    public void PacketRenderer_DisplayText_DecodesOnlyThePlaceholders()
    {
        // Act
        var text = PacketRenderer.DisplayText("A \ufb02\u00b0amp\u00b6\u00df B &amp;\n\u00a0\ufb02\u00b0\u00b035\u00b6\u00df  ");

        // Assert
        Assert.Equal("A & B &amp; \u00a0#", text);
    }

    /// <summary>
    ///     Validates the default geometry: rows 47 pixels apart from y 15 (the padding grown for the bit numbers), blocks
    ///     32 pixels per bit less the padding of 5, a block's first and last bit numbers 2 pixels above its corners (one
    ///     centered number for a single bit), the label centered, and the view box of 1026 pixels by the rows without a
    ///     title row.
    /// </summary>
    [Fact]
    public void PacketRenderer_Draw_Default_PlacesBlocksAndBitNumbersAsMermaid()
    {
        // Act
        var scene = SceneQuery.Layout("packet\n0-15: \"a\"\n16: \"b\"\n17-40: \"c\"\n");
        var items = scene.Walk().ToList();

        // Assert
        Assert.Equal(new SceneRect(0, 0, 1026, 109), scene.ViewBox);
        Assert.Equal(2, items.Count(entry => entry.Item.Role == "packet-row"));
        var rects = items.Where(entry => entry.Item.Role == "packet-block").Select(entry => Scene.BoundsOf([entry.Item])!.Value).ToList();
        Assert.Equal(
            [new SceneRect(1, 15, 507, 32), new SceneRect(513, 15, 27, 32), new SceneRect(545, 15, 475, 32), new SceneRect(1, 62, 283, 32)],
            rects);
        var texts = items.Select(entry => entry.Item).OfType<SceneText>().Select(text => (text.Role, Text(text), text.X, text.Anchor)).ToList();
        Assert.Equal(
            [
                ("packet-label", "a", 254.5, SceneTextAnchor.Middle),
                ("packet-bit-start", "0", 1.0, SceneTextAnchor.Start),
                ("packet-bit-end", "15", 508.0, SceneTextAnchor.End),
                ("packet-label", "b", 526.5, SceneTextAnchor.Middle),
                ("packet-bit-start", "16", 526.5, SceneTextAnchor.Middle),
                ("packet-label", "c", 782.5, SceneTextAnchor.Middle),
                ("packet-bit-start", "17", 545.0, SceneTextAnchor.Start),
                ("packet-bit-end", "31", 1020.0, SceneTextAnchor.End),
                ("packet-label", "c", 142.5, SceneTextAnchor.Middle),
                ("packet-bit-start", "32", 1.0, SceneTextAnchor.Start),
                ("packet-bit-end", "40", 284.0, SceneTextAnchor.End),
            ],
            texts);
        Assert.All(items.Select(entry => entry.Item).OfType<SceneText>().Where(text => text.Role == "packet-bit-start" && Text(text) == "32"), text => Assert.Equal(60, text.Baseline));
    }

    /// <summary>
    ///     Validates the title row: a title adds a row's height to the view box and is centered in the extra row, its
    ///     middle baseline below the row's middle; the frontmatter title is drawn when the text has none.
    /// </summary>
    [Fact]
    public void PacketRenderer_Draw_Title_IsCenteredInAnExtraRow()
    {
        // Act
        var titled = SceneQuery.Layout("packet\ntitle Header\n0-31: \"a\"\n");
        var frontmatter = SceneQuery.Layout("---\ntitle: From front\n---\npacket\n0-31: \"a\"\n");

        // Assert
        Assert.Equal(new SceneRect(0, 0, 1026, 94), titled.ViewBox);
        var title = Assert.IsType<SceneText>(Assert.Single(SceneQuery.OfRole(titled, "title")));
        Assert.Equal(("Header", 513.0, SceneTextAnchor.Middle), (Text(title), title.X, title.Anchor));
        Assert.InRange(title.Baseline, 70.5, 75);
        Assert.Equal("From front", Text(Assert.IsType<SceneText>(Assert.Single(SceneQuery.OfRole(frontmatter, "title")))));
    }

    /// <summary>
    ///     Validates that a title statement that sanitizes to empty replaces the frontmatter title, so no title row is
    ///     added, as in Mermaid.
    /// </summary>
    [Fact]
    public void PacketRenderer_Draw_TitleSanitizedToEmpty_HasNoTitleRow()
    {
        // Act
        var scene = SceneQuery.Layout("---\ntitle: FM\n---\npacket\ntitle <script>x</script>\n0: \"a\"\n");

        // Assert
        Assert.Equal(new SceneRect(0, 0, 1026, 62), scene.ViewBox);
        Assert.Empty(SceneQuery.OfRole(scene, "title"));
    }

    /// <summary>
    ///     Validates an empty diagram (no rows, no title): a view box of 1026 by the padding of 15 and no items.
    /// </summary>
    [Fact]
    public void PacketRenderer_Draw_Empty_IsThePaddingOnly()
    {
        // Act
        var scene = SceneQuery.Layout("packet\n");

        // Assert
        Assert.Equal(new SceneRect(0, 0, 1026, 15), scene.ViewBox);
        Assert.Empty(Assert.IsType<SceneGroup>(Assert.Single(scene.Items)).Children);
    }

    /// <summary>
    ///     Validates the configuration: without bit numbers the padding is not grown and no number is drawn; the row
    ///     height, bit width, bits per row and paddings move every block.
    /// </summary>
    [Fact]
    public void PacketRenderer_Draw_Configuration_ChangesTheGeometry()
    {
        // Act
        var plain = SceneQuery.Layout("---\nconfig:\n  packet:\n    showBits: false\n---\npacket\n0-15: \"a\"\n");
        var sized = SceneQuery.Layout("---\nconfig:\n  packet:\n    rowHeight: 40\n    bitWidth: 20\n    bitsPerRow: 16\n    paddingX: 2\n    paddingY: 8\n---\npacket\n0-3: \"a\"\n4-20: \"b\"\n");

        // Assert
        Assert.Equal(new SceneRect(0, 0, 1026, 42), plain.ViewBox);
        Assert.DoesNotContain(plain.Walk(), entry => entry.Item.Role is "packet-bit-start" or "packet-bit-end");
        Assert.Equal(new SceneRect(1, 5, 507, 32), Scene.BoundsOf(SceneQuery.OfRole(plain, "packet-block"))!.Value);
        Assert.Equal(new SceneRect(0, 0, 322, 134), sized.ViewBox);
        Assert.Equal(
            [new SceneRect(1, 18, 78, 40), new SceneRect(81, 18, 238, 40), new SceneRect(1, 76, 98, 40)],
            SceneQuery.OfRole(sized, "packet-block").Select(item => Scene.BoundsOf([item])!.Value));
    }

    /// <summary>
    ///     Validates that a block narrower than the padding draws no rectangle (SVG draws no rectangle of negative width),
    ///     but its texts.
    /// </summary>
    [Fact]
    public void PacketRenderer_Draw_NarrowBlock_HasTextsButNoRectangle()
    {
        // Act
        var scene = SceneQuery.Layout("---\nconfig:\n  packet:\n    paddingX: 40\n---\npacket\n0: \"a\"\n1-2: \"b\"\n");

        // Assert
        Assert.Equal(["b"], SceneQuery.OfRole(scene, "packet-block").Select(item => item.Id));
        Assert.Equal(["a", "b"], SceneQuery.OfRole(scene, "packet-label").Select(item => item.Id));
    }

    /// <summary>
    ///     Validates the paint of a theme: the dark theme's packet colors on the blocks, labels and bit numbers, and the
    ///     sheet's font sizes.
    /// </summary>
    [Fact]
    public void PacketRenderer_Draw_DarkTheme_PaintsWithThePacketColors()
    {
        // Act
        var scene = SceneQuery.Layout("---\nconfig:\n  theme: dark\n---\npacket\n0-7: \"a\"\n");

        // Assert
        var text = new Rgba32(0xe0, 0xdf, 0xdf, 255);
        var block = Assert.IsType<ScenePath>(Assert.Single(SceneQuery.OfRole(scene, "packet-block")));
        Assert.Equal((new Rgba32(0x33, 0x33, 0x33, 255), text, 1.0), (block.Fill!.Value, block.Stroke!.Color, block.Stroke.Width));
        var label = Assert.IsType<SceneText>(Assert.Single(SceneQuery.OfRole(scene, "packet-label")));
        var start = Assert.IsType<SceneText>(Assert.Single(SceneQuery.OfRole(scene, "packet-bit-start")));
        Assert.Equal((text, 12.0), (label.Runs[0].Color, label.Runs[0].Font.Size));
        Assert.Equal((text, 10.0), (start.Runs[0].Color, start.Runs[0].Font.Size));
    }

    /// <summary>
    ///     Validates that the render is bounded: a work limit below the blocks, parts and texts fails located with
    ///     <see cref="MermaidDiagnosticCodes.LimitExceeded"/>, and a canceled render stops.
    /// </summary>
    [Fact]
    public void PacketRenderer_Draw_WorkLimitAndCancellation_StopTheRender()
    {
        // Arrange
        var document = MermaidDocument.Parse("packet\n0-15: \"Source Port\"\n16-31: \"Destination Port\"\n");
        var limited = new MermaidRenderOptions { Limits = new MermaidLimits { MaxLayoutWork = 20 } };
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.LayoutScene(limited, TestContext.Current.CancellationToken));
        var stopped = Record.Exception(() => document.LayoutScene(null, canceled.Token));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.True(exception.Location.IsKnown);
        Assert.IsAssignableFrom<OperationCanceledException>(stopped);
    }

    /// <summary>
    ///     Gets the text of a scene text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Its runs' text.</returns>
    private static string Text(SceneText text) => string.Concat(text.Runs.Select(run => run.Text));
}
