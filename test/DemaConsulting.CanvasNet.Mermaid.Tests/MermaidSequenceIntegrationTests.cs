using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of sequence diagrams through the public API: every fixture renders strictly, the pixels carry the
///     theme's colors, the scale and the look behave, and rendering is deterministic and can be canceled.
/// </summary>
public class MermaidSequenceIntegrationTests
{
    /// <summary>A small diagram: two participants, an activation, a note and a reply.</summary>
    private const string Simple = "sequenceDiagram\n    participant Alice\n    participant Bob\n    Alice->>+Bob: Hello\n    Note over Bob: Thinking\n    Bob-->>-Alice: Hi\n";

    /// <summary>
    ///     Gets the names of the sequence fixtures.
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence"), "*.mmd").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    /// <summary>
    ///     Validates that every sequence fixture renders in strict mode, draws something, gives no report, and measures as
    ///     it renders.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMermaid_Sequence_Fixture_RendersStrictly(string name)
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence", name + ".mmd"));
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates the default theme's colors in the pixels: participant fill <c>#ECECFF</c> and border <c>#9370DB</c>,
    ///     note fill <c>#fff5ad</c>, activation fill <c>#f4f4f4</c> and the signal color <c>#333333</c> of the arrowhead.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Sequence_DefaultTheme_PaintsMermaidsColors()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var items = SceneQuery.SequenceItems(SceneQuery.Layout(Simple));
        var alice = Box(items.Find("actor-top", "Alice")!);
        var note = Box(items.Find("note", "i2")!);
        var activation = Box(items.WithRole("activation").Single());
        var head = Box(items.WithRole("marker-end").First());

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 4 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), Pixel(surface, alice.X + 4, alice.Y + 4, 4));
        Assert.Equal(new Rgba32(0x93, 0x70, 0xDB, 255), Pixel(surface, alice.X + 20, alice.Y + 0.05, 4));
        Assert.Equal(new Rgba32(0xFF, 0xF5, 0xAD, 255), Pixel(surface, note.X + 3, note.Y + 3, 4));
        Assert.Equal(new Rgba32(0xF4, 0xF4, 0xF4, 255), Pixel(surface, activation.X + 2, activation.Bottom - 3, 4));
        Assert.Equal(new Rgba32(0x33, 0x33, 0x33, 255), Pixel(surface, head.X + (head.Width / 2), head.Y + (head.Height / 2), 4));
    }

    /// <summary>
    ///     Validates that the theme changes the participant fill (the theme's <c>actorBkg</c>).
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="r">The expected red.</param>
    /// <param name="g">The expected green.</param>
    /// <param name="b">The expected blue.</param>
    [Theory]
    [InlineData("default", 0xEC, 0xEC, 0xFF)]
    [InlineData("dark", 0x1F, 0x20, 0x20)]
    [InlineData("forest", 0xCD, 0xE4, 0x98)]
    [InlineData("neutral", 0xEE, 0xEE, 0xEE)]
    public void CanvasNetMermaid_Sequence_Theme_ChangesTheParticipantFill(string theme, byte r, byte g, byte b)
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var options = new MermaidRenderOptions { Scale = 2, Theme = theme };
        var alice = Box(SceneQuery.SequenceItems(SceneQuery.Layout(Simple, options)).Find("actor-top", "Alice")!);

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(r, g, b, 255), Pixel(surface, alice.X + 4, alice.Y + 4, 2));
    }

    /// <summary>
    ///     Validates that a look other than classic is reported once and drawn classic, and that strict mode refuses it.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Sequence_NeoLook_IsReportedAndDrawnClassic()
    {
        // Arrange
        var document = MermaidDocument.Parse("---\nconfig:\n  look: neo\n---\n" + Simple);
        var classic = MermaidDocument.Parse(Simple);
        var (options, _, unsupported) = MermaidTestHelpers.Collecting();
        var (strict, _, _) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        using var expected = classic.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var report = Assert.Single(unsupported);
        Assert.Equal(MermaidDiagnosticCodes.LookUnsupported, report.Feature);
        Assert.True(MermaidTestHelpers.PixelsEqual(expected, surface));
        Assert.Throws<MermaidUnsupportedFeatureException>(() => document.Render(strict, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that the scale multiplies the natural size, which is the view box's.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Sequence_Scale_MultipliesTheNaturalSize()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var viewBox = SceneQuery.Layout(Simple).ViewBox;

        // Act
        var natural = document.Measure(cancellationToken: TestContext.Current.CancellationToken);
        var doubled = document.Measure(new MermaidRenderOptions { Scale = 2 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(((int)Math.Ceiling(viewBox.Width), (int)Math.Ceiling(viewBox.Height)), (natural.Width, natural.Height));
        Assert.Equal((natural.Width * 2, natural.Height * 2), (doubled.Width, doubled.Height));
    }

    /// <summary>
    ///     Validates that repeated and concurrent renders of a document give identical pixels.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Sequence_RepeatedAndConcurrentRenders_AreIdentical()
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence", "17-autonumber.mmd"));
        using var first = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var second = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var concurrent = Enumerable.Range(0, 6).AsParallel().Select(_ =>
        {
            using var surface = document.Render();
            return MermaidTestHelpers.PixelsEqual(first, surface);
        }).ToList();

        // Assert
        Assert.True(MermaidTestHelpers.PixelsEqual(first, second));
        Assert.All(concurrent, Assert.True);
    }

    /// <summary>
    ///     Validates that a canceled token stops a sequence render.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Sequence_CanceledToken_Throws()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that a layout that needs more work than allowed fails with the limit code instead of running on.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Sequence_WorkLimit_FailsWithLimitExceeded()
    {
        // Arrange: many messages and a small work limit
        var text = "sequenceDiagram\n" + string.Concat(Enumerable.Range(0, 200).Select(i => $"    A->>B: message {i}\n"));
        var document = MermaidDocument.Parse(text);
        var small = new MermaidRenderOptions { Limits = new MermaidLimits { MaxLayoutWork = 2_000 } };

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(small, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Contains("MaxLayoutWork", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Gets the bounds of a scene item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The bounds.</returns>
    private static SceneRect Box(SceneItem item) => Scene.BoundsOf([item])!.Value;

    /// <summary>
    ///     Reads the pixel under a point of the scene (whose view box starts 50 left and 10 above the origin).
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="x">The scene x.</param>
    /// <param name="y">The scene y.</param>
    /// <param name="scale">The scale of the render.</param>
    /// <returns>The pixel.</returns>
    private static Rgba32 Pixel(Surface surface, double x, double y, double scale) =>
        surface.GetRowSpan((int)Math.Floor((y + 10) * scale))[(int)Math.Floor((x + 50) * scale)];
}
