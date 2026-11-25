using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;

namespace DemaConsulting.CanvasNet.MathMl.Tests;

/// <summary>
///     Unit tests for <see cref="MathMlPainter"/>: glyphs, backgrounds, borders, phantoms, cancellation and the glyph
///     limit, on boxes built directly.
/// </summary>
public class MathMlPainterTests
{
    /// <summary>The color of the glyphs of the tests.</summary>
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>
    ///     Validates that a glyph is painted where its pen and baseline place it, in its color.
    /// </summary>
    [Fact]
    public void MathMlPainter_Paint_Glyph_InksItsBox()
    {
        // Arrange: an upright l, a vertical stroke from the baseline up
        var box = new MathBox(20, 20, 0, null, [Glyph('l', 40, 0)]);
        using var surface = new Surface(30, 40);

        // Act
        MathMlPainter.Paint(surface, box, new MathPlacement(1, 2, 35), null, 10, TestContext.Current.CancellationToken);

        // Assert: ink just above the baseline inside the advance, none below it
        Assert.Contains(Enumerable.Range(0, 30), x => surface[x, 30] == Black);
        Assert.All(Enumerable.Range(0, 30), x => Assert.Equal(0, surface[x, 37].A));
    }

    /// <summary>
    ///     Validates that a background is painted under the glyphs and that the surface background fills everything.
    /// </summary>
    [Fact]
    public void MathMlPainter_Paint_Background_IsUnderGlyphs()
    {
        // Arrange: a background item after the glyph in the list is still painted first
        var blue = new Rgba32(0, 0, 255, 255);
        var white = new Rgba32(255, 255, 255, 255);
        var box = new MathBox(20, 30, 5, null, [Glyph('l', 40, 0), new BackgroundItem(new MathRect(0, -30, 20, 35), blue)]);
        using var surface = new Surface(30, 50);

        // Act
        MathMlPainter.Paint(surface, box, new MathPlacement(1, 0, 40), white, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(white, surface[25, 5]);
        Assert.Equal(blue, surface[1, 12]);
        Assert.Contains(Enumerable.Range(0, 20), x => surface[x, 35] == Black);
    }

    /// <summary>
    ///     Validates that a border paints its four sides and leaves its inside alone.
    /// </summary>
    [Fact]
    public void MathMlPainter_Paint_Border_PaintsFourSides()
    {
        // Arrange
        var red = new Rgba32(255, 0, 0, 255);
        var box = new MathBox(10, 10, 0, null, [new BorderItem(new MathRect(0, -10, 10, 10), 1, red)]);
        using var surface = new Surface(10, 10);

        // Act
        MathMlPainter.Paint(surface, box, new MathPlacement(1, 0, 10), null, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((red, red, red, red), (surface[5, 0], surface[5, 9], surface[0, 5], surface[9, 5]));
        Assert.Equal(0, surface[5, 5].A);
    }

    /// <summary>
    ///     Validates that a phantom subtree keeps nothing on the surface.
    /// </summary>
    [Fact]
    public void MathMlPainter_Paint_Phantom_PaintsNothing()
    {
        // Arrange
        var phantom = new MathBox(20, 30, 0, null, [Glyph('x', 40, 0), new BackgroundItem(new MathRect(0, -30, 20, 30), Black)]) { IsPhantom = true };
        var root = new MathBox(20, 30, 0, [new PlacedBox(phantom, 0, 0)]);
        using var surface = new Surface(30, 40);

        // Act
        MathMlPainter.Paint(surface, root, new MathPlacement(1, 0, 35), null, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(MathMlTestHelpers.IsFullyTransparent(surface));
    }

    /// <summary>
    ///     Validates that a canceled token stops the painting before anything is painted.
    /// </summary>
    [Fact]
    public void MathMlPainter_Paint_CanceledToken_PaintsNothing()
    {
        // Arrange
        var items = Enumerable.Range(0, 3 * MathMlPainter.CancellationInterval).Select(i => (IPaintItem)Glyph('x', 10, i)).ToList();
        var box = new MathBox(100, 10, 0, null, items);
        using var surface = new Surface(100, 20);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act
        Assert.ThrowsAny<OperationCanceledException>(() => MathMlPainter.Paint(surface, box, new MathPlacement(1, 0, 15), null, 1_000, source.Token));

        // Assert
        Assert.True(MathMlTestHelpers.IsFullyTransparent(surface));
    }

    /// <summary>
    ///     Validates the glyph limit: more glyphs than allowed fail before any is painted, located at their element.
    /// </summary>
    [Fact]
    public void MathMlPainter_Paint_TooManyGlyphs_ThrowsBeforePainting()
    {
        // Arrange
        var element = MathMlTestHelpers.Tree("<math>\n<mi>xyz</mi></math>").ElementChildren.Single();
        var box = new MathBox(30, 10, 0, null, [Glyph('x', 10, 0), Glyph('y', 10, 10), Glyph('z', 10, 20)]) { Element = element };
        using var surface = new Surface(40, 20);

        // Act
        var ex = Assert.Throws<MathMlRenderException>(() => MathMlPainter.Paint(surface, box, new MathPlacement(1, 0, 15), Black, 2, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Contains("MathMlLimits.MaxPaintedGlyphs", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new MathMlLocation(null, 2, 2), ex.Location);
        Assert.True(MathMlTestHelpers.IsFullyTransparent(surface));
    }

    /// <summary>
    ///     Creates a glyph item of the math font.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <param name="size">The font size.</param>
    /// <param name="x">The pen position.</param>
    /// <returns>The item.</returns>
    private static GlyphItem Glyph(char character, double size, double x)
    {
        var (font, glyph) = MathFont.Default.Resolve(character);
        return new GlyphItem(font, glyph, size, x, 0, Black, MathFont.Default.InkBounds(font, glyph, size).Offset(x, 0));
    }
}
