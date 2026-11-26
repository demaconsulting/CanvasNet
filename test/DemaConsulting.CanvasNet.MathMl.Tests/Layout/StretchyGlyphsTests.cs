using DemaConsulting.CanvasNet.MathMl.Layout;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="StretchyGlyphs"/> with the bundled Noto Sans Math: the base glyph, the size variants
///     and the glyph assemblies of MathML Core 5.3, the Unicode-based assemblies of B.4, the rule of a line character,
///     the large variant, the preferred inline size and the assembly limit.
/// </summary>
public class StretchyGlyphsTests
{
    /// <summary>The font size of the tests: 100 px, so a design unit of the 1000-unit font is 0.1 px.</summary>
    private const double Size = 100;

    /// <summary>The location the limit errors carry.</summary>
    private static readonly MathMlLocation Location = new("test.mml", 3, 7);

    /// <summary>The math font.</summary>
    private static MathFont Font => MathFont.Default;

    /// <summary>
    ///     Gets targets that the vertical assembly of the parenthesis covers, from just above its largest variant to many
    ///     extenders.
    /// </summary>
    /// <returns>The targets in CSS pixels.</returns>
    public static TheoryData<double> AssemblyTargets() => [377, 400, 450.5, 500, 744.4, 1000, 1234.5, 2000, 5000, 9000];

    /// <summary>
    ///     Validates that a target the base glyph covers keeps the base glyph.
    /// </summary>
    [Fact]
    public void StretchyGlyphs_Stretch_SmallTarget_UsesBaseGlyph()
    {
        // Arrange
        var (_, glyph) = Font.Resolve('(');

        // Act
        var result = StretchyGlyphs.Stretch(Font, '(', StretchAxis.Block, 10, Size, 1000, Location)!;

        // Assert
        Assert.Equal(StretchKind.Base, result.Kind);
        Assert.Equal(glyph, Assert.Single(result.Glyphs).Glyph);
    }

    /// <summary>
    ///     Validates that a medium target takes the first size variant whose advance measurement covers it: the 13
    ///     variants of the parenthesis grow from 942 to 3768 design units.
    /// </summary>
    [Fact]
    public void StretchyGlyphs_Stretch_MediumTarget_UsesFirstCoveringVariant()
    {
        // Arrange
        var (font, glyph) = Font.Resolve('(');
        Assert.True(Font.TryGetConstruction(font, glyph, StretchAxis.Block, Size, out var construction));

        // Act
        var result = StretchyGlyphs.Stretch(Font, '(', StretchAxis.Block, 150, Size, 1000, Location)!;

        // Assert
        Assert.Equal(13, construction.Variants.Count);
        Assert.Equal(94.2, construction.Variants[0].Advance, 9);
        Assert.Equal(376.8, construction.Variants[^1].Advance, 9);
        Assert.Equal(StretchKind.Variant, result.Kind);
        Assert.Equal(construction.Variants.First(variant => variant.Advance >= 150).Glyph, Assert.Single(result.Glyphs).Glyph);
        Assert.True(result.Height >= 150);
    }

    /// <summary>
    ///     Validates that a target beyond the largest variant is an assembly with the fewest repetitions of the extender:
    ///     for 744.4 px the parts of 106.9 px each with a minimum overlap of 10 px need six extenders.
    /// </summary>
    [Fact]
    public void StretchyGlyphs_Stretch_LargeTarget_AssemblesWithMinimalRepetitions()
    {
        // Act
        var result = StretchyGlyphs.Stretch(Font, '(', StretchAxis.Block, 744.4, Size, 1000, Location)!;

        // Assert: bottom, six extenders, top; exactly the target; standing on the baseline
        Assert.Equal(StretchKind.Assembly, result.Kind);
        Assert.Equal(8, result.Glyphs.Count);
        Assert.Equal(744.4, result.Height, 6);
        Assert.Equal(0, result.Descent);
        Assert.True(result.Glyphs.Zip(result.Glyphs.Skip(1)).All(pair => pair.Second.Baseline < pair.First.Baseline));
    }

    /// <summary>
    ///     Validates the overlap of the assembly over many targets: at least the minimum connector overlap, at most the
    ///     shortest connector, the size covers the target, and one repetition fewer would not.
    /// </summary>
    /// <param name="target">The target size.</param>
    [Theory]
    [MemberData(nameof(AssemblyTargets))]
    public void StretchyGlyphs_Stretch_Assembly_OverlapWithinConnectorBounds(double target)
    {
        // Arrange
        var parts = ParenthesisParts();
        var minOverlap = Font.MinConnectorOverlap(Size);
        var connectors = parts.Select((part, i) => (part, i))
            .SelectMany(entry => new[] { entry.part.IsExtender || entry.i > 0 ? entry.part.StartConnector : double.MaxValue, entry.part.IsExtender || entry.i < parts.Count - 1 ? entry.part.EndConnector : double.MaxValue })
            .Min();

        // Act
        var (repetitions, overlap, size) = StretchyGlyphs.SolveAssembly(parts, minOverlap, target, 1000, Location)!.Value;

        // Assert
        Assert.Equal(10, minOverlap, 9);
        Assert.InRange(overlap, minOverlap - 1e-9, connectors + 1e-9);
        Assert.True(size >= target - 1e-6);
        var fewer = SizeAt(parts, repetitions - 1, minOverlap);
        Assert.True(repetitions == 0 || fewer < target);
    }

    /// <summary>
    ///     Validates that the assembly is exactly the target when the even overlap fits the connectors.
    /// </summary>
    [Fact]
    public void StretchyGlyphs_Stretch_Assembly_SizeCoversTargetExactlyWhenPossible()
    {
        // Arrange
        var parts = ParenthesisParts();
        var minOverlap = Font.MinConnectorOverlap(Size);

        // Act / Assert: every target of the parenthesis is reached exactly (its connectors are long)
        foreach (var target in new[] { 400.0, 744.4, 1000.0, 2500.0 })
        {
            var (_, _, size) = StretchyGlyphs.SolveAssembly(parts, minOverlap, target, 1000, Location)!.Value;
            Assert.Equal(target, size, 6);
        }

        // An assembly without an extender, or whose extenders do not grow it, is invalid
        Assert.Null(StretchyGlyphs.SolveAssembly([parts[0] with { IsExtender = false }], minOverlap, 500, 1000, Location));
        Assert.Null(StretchyGlyphs.SolveAssembly([parts[1] with { FullAdvance = 5 }], minOverlap, 500, 1000, Location));
    }

    /// <summary>
    ///     Validates that a horizontal arrow, which has a horizontal construction in the math font, assembles to a width.
    /// </summary>
    [Fact]
    public void StretchyGlyphs_Stretch_HorizontalArrow_Assembles()
    {
        // Act
        var result = StretchyGlyphs.Stretch(Font, 0x2192, StretchAxis.Inline, 600, Size, 1000, Location)!;

        // Assert: the arrow covers the width (its short connectors may keep it wider than the target)
        Assert.Equal(StretchKind.Assembly, result.Kind);
        Assert.True(result.Width >= 600);
        Assert.True(result.Glyphs.Count >= 2);
        Assert.True(result.Glyphs.Zip(result.Glyphs.Skip(1)).All(pair => pair.Second.X > pair.First.X));
    }

    /// <summary>
    ///     Validates the ascent and the descent of a horizontal assembly: the ink of its parts rounded out to whole pixels,
    ///     negative when the ink is above the baseline (the over brace), as the ink of a glyph.
    /// </summary>
    [Fact]
    public void StretchyGlyphs_Stretch_HorizontalAssembly_HasInkExtents()
    {
        // Act: the over brace, whose ink is above the baseline
        var result = StretchyGlyphs.Stretch(Font, 0x23DE, StretchAxis.Inline, 1000, Size, 1000, Location)!;

        // Assert
        Assert.Equal(StretchKind.Assembly, result.Kind);
        Assert.True(result.Descent < 0);
        Assert.Equal(System.Math.Round(result.Ascent), result.Ascent);
        Assert.Equal(System.Math.Round(result.Descent), result.Descent);
        Assert.True(result.Ascent > 0 && result.Ascent + result.Descent > 0);
    }

    /// <summary>
    ///     Validates that a character without a construction in the math font uses its Unicode-based assembly (B.4): the
    ///     left-right arrow is a left arrow, extenders and a right arrow, joined without overlap.
    /// </summary>
    [Fact]
    public void StretchyGlyphs_Stretch_NoMathConstruction_UsesUnicodeFallback()
    {
        // Arrange
        var (_, left) = Font.Resolve(0x2190);
        var (_, right) = Font.Resolve(0x2192);
        var (_, extender) = Font.Resolve(0x23AF);

        // Act
        var result = StretchyGlyphs.Stretch(Font, 0x2194, StretchAxis.Inline, 500, Size, 1000, Location)!;

        // Assert
        Assert.Equal(StretchKind.UnicodeAssembly, result.Kind);
        Assert.Equal(left, result.Glyphs[0].Glyph);
        Assert.Equal(right, result.Glyphs[^1].Glyph);
        Assert.All(result.Glyphs.Skip(1).SkipLast(1), glyph => Assert.Equal(extender, glyph.Glyph));
        Assert.True(result.Width >= 500);
        Assert.Equal(41, StretchyGlyphs.UnicodeAssemblyCount);
    }

    /// <summary>
    ///     Validates that a line character the math font cannot stretch (the macron, which it has no glyph for) is a rule
    ///     of the target length, as thick as the ink of its glyph.
    /// </summary>
    [Fact]
    public void StretchyGlyphs_Stretch_MissingGlyphLine_UsesRuleFallback()
    {
        // Arrange
        var (font, glyph) = Font.Resolve(0x00AF);
        var ink = Font.InkBounds(font, glyph, Size);

        // Act
        var result = StretchyGlyphs.Stretch(Font, 0x00AF, StretchAxis.Inline, 300, Size, 1000, Location)!;
        var vertical = StretchyGlyphs.Stretch(Font, 0x2015, StretchAxis.Block, 300, Size, 1000, Location)!;

        // Assert
        Assert.NotSame(Font.Primary, font);
        Assert.Equal(StretchKind.Rule, result.Kind);
        Assert.Empty(result.Glyphs);
        Assert.Equal(new MathRect(0, ink.Y, 300, ink.Height), result.Rule);
        Assert.Equal(300, result.Width);
        Assert.True(StretchyGlyphs.IsRuleCharacter(0x203E));
        Assert.Equal((StretchKind.Rule, 300.0), (vertical.Kind, vertical.Height));
    }

    /// <summary>
    ///     Validates that a character nothing can stretch fails, so that its caller keeps the glyph.
    /// </summary>
    [Fact]
    public void StretchyGlyphs_Stretch_NothingAvailable_Fails()
    {
        // Act / Assert
        Assert.Null(StretchyGlyphs.Stretch(Font, 'x', StretchAxis.Block, 300, Size, 1000, Location));
        Assert.Null(StretchyGlyphs.Stretch(Font, 'x', StretchAxis.Inline, 300, Size, 1000, Location));
        Assert.Null(StretchyGlyphs.LargeVariant(Font, 'x', 230, Size));
        Assert.Equal(StretchKind.Base, StretchyGlyphs.Glyph(Font, 'x', Size).Kind);
    }

    /// <summary>
    ///     Validates that an assembly that needs more glyphs than <see cref="MathMlLimits.MaxAssemblyParts"/> fails with a
    ///     located limit error before any glyph is placed.
    /// </summary>
    [Fact]
    public void StretchyGlyphs_Stretch_HugeTarget_ThrowsLimitExceeded()
    {
        // Act
        var error = Assert.Throws<MathMlRenderException>(() => StretchyGlyphs.Stretch(Font, '(', StretchAxis.Block, 1e7, Size, 1000, Location));
        var infinite = Assert.Throws<MathMlRenderException>(() => StretchyGlyphs.Stretch(Font, '(', StretchAxis.Block, double.PositiveInfinity, Size, 1000, Location));
        var withinLimit = StretchyGlyphs.Stretch(Font, '(', StretchAxis.Block, 5000, Size, 1000, Location)!;

        // Assert
        Assert.Equal((MathMlDiagnosticCodes.LimitExceeded, Location), (error.Code, error.Location));
        Assert.Contains("MathMlLimits.MaxAssemblyParts", error.Message, StringComparison.Ordinal);
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, infinite.Code);
        Assert.True(withinLimit.Glyphs.Count <= 1000);
    }

    /// <summary>
    ///     Validates the preferred inline size: the widest of the base glyph, the variants and the parts of the assembly.
    /// </summary>
    [Fact]
    public void StretchyGlyphs_PreferredInlineSize_IsMaximumOverConstructions()
    {
        // Arrange
        var (font, glyph) = Font.Resolve('(');
        Assert.True(Font.TryGetConstruction(font, glyph, StretchAxis.Block, Size, out var construction));
        var expected = construction.Variants.Select(variant => MathFont.Advance(font, variant.Glyph, Size))
            .Concat(construction.Parts!.Select(part => MathFont.Advance(part.Font, part.Glyph, Size)))
            .Append(MathFont.Advance(font, glyph, Size))
            .Max();
        var (xFont, xGlyph) = Font.Resolve('x');

        // Act
        var paren = StretchyGlyphs.PreferredInlineSize(Font, '(', Size);
        var x = StretchyGlyphs.PreferredInlineSize(Font, 'x', Size);

        // Assert
        Assert.Equal(expected, paren, 9);
        Assert.True(paren > MathFont.Advance(font, glyph, Size));
        Assert.Equal(MathFont.Advance(xFont, xGlyph, Size), x, 9);
    }

    /// <summary>
    ///     Validates the large variant of a summation: a size variant other than the base glyph, taller than it.
    /// </summary>
    [Fact]
    public void StretchyGlyphs_LargeVariant_Sum_IsTallerThanBase()
    {
        // Arrange
        var (_, glyph) = Font.Resolve(0x2211);

        // Act
        var large = StretchyGlyphs.LargeVariant(Font, 0x2211, Font.Constant(MathConstant.DisplayOperatorMinHeight, Size), Size)!;

        // Assert
        Assert.NotEqual(glyph, Assert.Single(large.Glyphs).Glyph);
        Assert.True(large.Height > StretchyGlyphs.Glyph(Font, 0x2211, Size).Height);
    }

    /// <summary>
    ///     Gets the parts of the vertical assembly of the parenthesis.
    /// </summary>
    /// <returns>The parts.</returns>
    private static IReadOnlyList<StretchPart> ParenthesisParts()
    {
        var (font, glyph) = Font.Resolve('(');
        Assert.True(Font.TryGetConstruction(font, glyph, StretchAxis.Block, Size, out var construction));
        return construction.Parts!;
    }

    /// <summary>
    ///     Computes the size of an assembly with a number of repetitions and an overlap.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <param name="repetitions">The repetitions of every extender.</param>
    /// <param name="overlap">The overlap.</param>
    /// <returns>The size.</returns>
    private static double SizeAt(IReadOnlyList<StretchPart> parts, int repetitions, double overlap)
    {
        var count = parts.Sum(part => part.IsExtender ? repetitions : 1);
        return parts.Sum(part => part.FullAdvance * (part.IsExtender ? repetitions : 1)) - (overlap * (count - 1));
    }
}
