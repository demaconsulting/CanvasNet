using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="RadicalLayout"/>: the radical symbol stretched over the base, the gap and the overbar, the
///     metrics of a square root (MathML Core 3.3.3.2), the kerns and the raise of the index of a root (3.3.3.3), and the
///     assembly limit. The bases and indices are synthetic boxes; the font is the bundled Noto Sans Math (1000 units per
///     em, so a constant of n units is n/10 CSS pixels at 100 px).
/// </summary>
public class RadicalLayoutTests
{
    /// <summary>The compact style of the tests at 100 px.</summary>
    private static readonly MathStyle Compact = new(100, false, 0, new Rgba32(0, 0, 0, 255), null);

    /// <summary>RadicalRuleThickness and RadicalExtraAscender at 100 px (71 units each).</summary>
    private const double Rule = 7.1;

    /// <summary>
    ///     Validates a square root of a small base: the symbol (its base glyph) covers the base, RadicalVerticalGap (89) and
    ///     the overbar; the ascent adds the gap, the overbar and RadicalExtraAscender to the base's; the descent reaches the
    ///     bottom of the symbol; the base follows the advance of the symbol; and the box holds the widest construction.
    /// </summary>
    [Fact]
    public void RadicalLayout_LayoutSquareRoot_SurdCoversBaseGapAndRule()
    {
        // Arrange
        var radicand = new MathBox(50, 50, 10);
        var glyph = StretchyGlyphs.Glyph(MathFont.Default, RadicalLayout.RadicalSymbol, 100);

        // Act
        var box = Layout().LayoutSquareRoot(radicand, Compact, default);

        // Assert
        Assert.True(glyph.Height >= Rule + 8.9 + 60);
        Assert.Equal(50 + 8.9 + Rule + Rule, box.Ascent, 9);
        Assert.Equal(glyph.Height + Rule - box.Ascent, box.Descent, 9);
        var placed = Assert.Single(box.Children);
        Assert.Equal((glyph.Width, 0.0), (placed.X, placed.Y));
        Assert.Equal(StretchyGlyphs.PreferredInlineSize(MathFont.Default, RadicalLayout.RadicalSymbol, 100) + 50, box.Width, 9);
        Assert.Single(box.Items.OfType<GlyphItem>());
    }

    /// <summary>
    ///     Validates that a square root in display style uses RadicalDisplayStyleVerticalGap (190).
    /// </summary>
    [Fact]
    public void RadicalLayout_LayoutSquareRoot_DisplayStyle_UsesDisplayGap()
    {
        // Act
        var box = Layout().LayoutSquareRoot(new MathBox(50, 50, 10), Compact with { DisplayStyle = true }, default);

        // Assert
        Assert.Equal(50 + 19 + Rule + Rule, box.Ascent, 9);
    }

    /// <summary>
    ///     Validates that a tall base stretches the symbol into its glyph assembly, which reaches the bottom of the base.
    /// </summary>
    [Fact]
    public void RadicalLayout_LayoutSquareRoot_TallBase_StretchesTheSurd()
    {
        // Act
        var box = Layout().LayoutSquareRoot(new MathBox(20, 300, 100), Compact, default);

        // Assert
        var glyphs = box.Items.OfType<GlyphItem>().ToList();
        Assert.True(glyphs.Count >= 3);
        var ink = glyphs.Select(glyph => glyph.Ink).Aggregate((a, b) => a.Union(b));
        Assert.True(ink.Bottom >= 100 - 1e-6);
        Assert.Equal(300 + 8.9 + Rule + Rule, box.Ascent, 9);
        Assert.True(box.Descent >= 100);
    }

    /// <summary>
    ///     Validates the overbar: a rule of RadicalRuleThickness from the end of the symbol over the whole base, its top
    ///     RadicalExtraAscender below the top of the box, in the color of the radical.
    /// </summary>
    [Fact]
    public void RadicalLayout_LayoutSquareRoot_Overbar_SpansTheBase()
    {
        // Arrange
        var blue = Compact with { Color = new Rgba32(0, 0, 255, 255) };

        // Act
        var box = Layout().LayoutSquareRoot(new MathBox(50, 50, 10), blue, default);

        // Assert
        var bar = Assert.Single(box.Items.OfType<RuleItem>());
        Assert.Equal(new Rgba32(0, 0, 255, 255), bar.Color);
        Assert.Equal(box.Children[0].X, bar.Rect.X, 9);
        Assert.Equal(50, bar.Rect.Width, 9);
        Assert.Equal(Rule, bar.Rect.Height, 9);
        Assert.Equal(-box.Ascent + Rule, bar.Rect.Y, 9);
    }

    /// <summary>
    ///     Validates a root with an index wider than the kern after it: the index starts after RadicalKernBeforeDegree (51),
    ///     the square root follows after RadicalKernAfterDegree (-450), and the bottom of the index is raised by
    ///     RadicalDegreeBottomRaisePercent (56, at the 1/64 precision of Chrome) of the height of the symbol from the bottom
    ///     of the square root.
    /// </summary>
    [Fact]
    public void RadicalLayout_LayoutRoot_IndexIsKernedAndRaised()
    {
        // Arrange
        var radicand = new MathBox(50, 50, 10);
        var index = new MathBox(60, 20, 5);
        var square = Layout().LayoutSquareRoot(radicand, Compact, default);
        var symbol = StretchyGlyphs.Glyph(MathFont.Default, RadicalLayout.RadicalSymbol, 100);

        // Act
        var box = Layout().LayoutRoot(radicand, index, Compact, default);

        // Assert
        var raise = 35 / 64.0 * symbol.Height;
        Assert.Equal((5.1, square.Descent - raise - 5), (Math.Round(box.Children[0].X, 9), Math.Round(box.Children[0].Y, 9)));
        Assert.Equal(5.1 + 60 - 45, box.Children[1].X, 9);
        Assert.Equal(5.1 + 60 - 45 + square.Width, box.Width, 9);
        Assert.Equal(System.Math.Max(square.Ascent, -square.Descent + raise + 25), box.Ascent, 9);
    }

    /// <summary>
    ///     Validates that the kern after a narrow index is clamped to minus its width, so the square root never starts
    ///     before the index.
    /// </summary>
    [Fact]
    public void RadicalLayout_LayoutRoot_NarrowIndex_ClampsKernAfter()
    {
        // Act
        var box = Layout().LayoutRoot(new MathBox(50, 50, 10), new MathBox(30, 20, 5), Compact, default);

        // Assert
        Assert.Equal(5.1, box.Children[1].X, 9);
    }

    /// <summary>
    ///     Validates that a base too tall for <see cref="MathMlLimits.MaxAssemblyParts"/> fails with a located limit error.
    /// </summary>
    [Fact]
    public void RadicalLayout_LayoutSquareRoot_HugeBase_LimitedByMaxAssemblyParts()
    {
        // Arrange
        var layout = new RadicalLayout(MathFont.Default, new MathMlLimits { MaxAssemblyParts = 5 });
        var location = new MathMlLocation(null, 3, 4);

        // Act
        var ex = Assert.Throws<MathMlRenderException>(() => layout.LayoutSquareRoot(new MathBox(10, 10_000, 0), Compact, location));

        // Assert
        Assert.Equal((MathMlDiagnosticCodes.LimitExceeded, location), (ex.Code, ex.Location));
        Assert.Contains("MaxAssemblyParts", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Creates the radical layout of a call with the default limits.
    /// </summary>
    /// <returns>The layout.</returns>
    private static RadicalLayout Layout() => new(MathFont.Default, MathMlLimits.Default);
}
