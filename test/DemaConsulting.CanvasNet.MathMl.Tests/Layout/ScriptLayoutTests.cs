using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="ScriptLayout"/>: the shifts of subscripts and superscripts (MathML Core 3.4.1) from the
///     MATH constants of the bundled font, the cramped shift, the italic corrections, the gap between both scripts,
///     SpaceAfterScript, and the pairs of <c>mmultiscripts</c> (3.4.3). The children are synthetic boxes; the font is the
///     bundled Noto Sans Math (1000 units per em, so a constant of n units is n/10 CSS pixels at 100 px).
/// </summary>
public class ScriptLayoutTests
{
    /// <summary>Opaque black.</summary>
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>The compact style of the tests at 100 px.</summary>
    private static readonly MathStyle Compact = new(100, false, 0, Black, null);

    /// <summary>SpaceAfterScript (40) at 100 px.</summary>
    private const double Space = 4;

    /// <summary>
    ///     Validates the shift of a subscript: SubscriptShiftDown (210) for a small script, its ascent - SubscriptTopMax
    ///     (430) for a tall one, and SubscriptBaselineDropMin (140) + the descent of the base for a deep base.
    /// </summary>
    [Fact]
    public void ScriptLayout_LayoutSub_ShiftFromConstantsAndScriptAscent()
    {
        // Arrange
        var layout = new ScriptLayout(MathFont.Default);

        // Act
        var small = layout.Layout(new MathBox(50, 50, 0), new MathBox(20, 10, 0), null, Compact);
        var tall = layout.Layout(new MathBox(50, 50, 0), new MathBox(20, 70, 0), null, Compact);
        var deep = layout.Layout(new MathBox(50, 50, 30), new MathBox(20, 10, 0), null, Compact);

        // Assert
        Assert.Equal(21, small.Children[1].Y, 9);
        Assert.Equal(70 - 43, tall.Children[1].Y, 9);
        Assert.Equal(14 + 30, deep.Children[1].Y, 9);
        Assert.Equal((50.0, 21.0), (small.Ascent, small.Descent));
    }

    /// <summary>
    ///     Validates that a superscript uses SuperscriptShiftUpCramped (280) instead of SuperscriptShiftUp (390) when
    ///     <c>math-shift</c> is compact, and is raised by SuperscriptBottomMin (134) + its descent and by the ascent of the
    ///     base - SuperscriptBaselineDropMax (240) when they are larger.
    /// </summary>
    [Fact]
    public void ScriptLayout_LayoutSup_CrampedUsesCrampedShift()
    {
        // Arrange
        var layout = new ScriptLayout(MathFont.Default);
        var script = new MathBox(20, 10, 0);

        // Act
        var normal = layout.Layout(new MathBox(50, 30, 0), null, script, Compact);
        var cramped = layout.Layout(new MathBox(50, 30, 0), null, script, Compact with { MathShiftCompact = true });
        var deepScript = layout.Layout(new MathBox(50, 30, 0), null, new MathBox(20, 10, 40), Compact);
        var tallBase = layout.Layout(new MathBox(50, 90, 0), null, script, Compact);

        // Assert
        Assert.Equal(-39, normal.Children[1].Y, 9);
        Assert.Equal(-28, cramped.Children[1].Y, 9);
        Assert.Equal(-(13.4 + 40), deepScript.Children[1].Y, 9);
        Assert.Equal(-(90 - 24), tallBase.Children[1].Y, 9);
        Assert.Equal(49, normal.Ascent, 9);
    }

    /// <summary>
    ///     Validates that the superscript of an operator drawn with a size variant or an assembly follows its italic
    ///     correction and the element ends after it as placed, while a base that is not an operator and paints nothing
    ///     moves no script, as in Chrome.
    /// </summary>
    [Fact]
    public void ScriptLayout_LayoutSup_ItalicCorrectionMovesSuperscript()
    {
        // Arrange
        var layout = new ScriptLayout(MathFont.Default);
        var stretched = new MathBox(50, 50, 0) { ItalicCorrection = 6, Operator = new EmbellishedOperator(0, 0) };
        var letter = new MathBox(50, 50, 0) { ItalicCorrection = 6 };

        // Act
        var operatorBox = layout.Layout(stretched, new MathBox(20, 10, 0), new MathBox(20, 10, 0), Compact);
        var letterBox = layout.Layout(letter, null, new MathBox(20, 10, 0), Compact);

        // Assert: the superscript after the correction, the subscript at the width, the box ending after the superscript
        Assert.Equal(56, operatorBox.Children[2].X, 9);
        Assert.Equal(50, operatorBox.Children[1].X, 9);
        Assert.Equal(56 + 20 + Space, operatorBox.Width, 9);
        Assert.Equal(50, letterBox.Children[1].X, 9);
        Assert.Equal((0.0, 0.0), ScriptLayout.ItalicCorrections(letter));
    }

    /// <summary>
    ///     Validates that the superscript of a slanted letter whose ink would collide with the ink of the letter (the hook
    ///     of a zeta reaching past its advance) follows the italic correction of the letter, as MathML Core 3.4.1.3 places
    ///     it, and widens the element by it, while a superscript clear of the ink of the letter starts at its advance, as
    ///     in Chrome.
    /// </summary>
    [Fact]
    public void ScriptLayout_LayoutSup_CollidingInkFollowsItalicCorrection()
    {
        // Arrange: a letter with an italic correction of 13, its ink reaching 11 past its advance of 40 at its top
        var layout = new ScriptLayout(MathFont.Default);
        var hooked = new MathBox(40, 76, 21, null, [new RuleItem(new MathRect(2, -76, 49, 97), Black)]) { ItalicCorrection = 13 };
        var upright = new MathBox(40, 76, 21, null, [new RuleItem(new MathRect(2, -76, 36, 97), Black)]) { ItalicCorrection = 13 };
        var two = new MathBox(34, 44, 0, null, [new RuleItem(new MathRect(3, -43, 28, 43), Black)]);

        // Act
        var collided = layout.Layout(hooked, null, two, Compact);
        var clear = layout.Layout(upright, null, two, Compact);

        // Assert: the superscript moves by the italic correction, its ink clear of the ink of the letter
        var sup = collided.Children[1];
        Assert.Equal(40 + 13, sup.X, 9);
        Assert.True(sup.X + 3 > 2 + 49);
        Assert.Equal(40 + 13 + 34 + Space, collided.Width, 9);
        Assert.Equal(40, clear.Children[1].X, 9);
        Assert.Equal(40 + 34 + Space, clear.Width, 9);
    }

    /// <summary>
    ///     Validates that the subscript of a single character is shifted by the constants and its own ascent only, whatever
    ///     the descender of the character (as TeX), while the subscript of another base (two glyphs, a row) is also kept
    ///     SubscriptBaselineDropMin below the descent of the base (MathML Core 3.4.1.2).
    /// </summary>
    [Fact]
    public void ScriptLayout_LayoutSub_CharacterBase_IgnoresItsDescent()
    {
        // Arrange: a deep character (descent 40 px) and the same extents drawn with two glyphs
        var layout = new ScriptLayout(MathFont.Default);
        var (font, glyph) = MathFont.Default.Resolve(0x1D6FE);
        GlyphItem Glyph(double x) => new(font, glyph, 100, x, 0, Black, new MathRect(x, -50, 40, 90));
        var character = new MathBox(45, 50, 40, null, [Glyph(0)]);
        var pair = new MathBox(90, 50, 40, null, [Glyph(0), Glyph(45)]);
        var sub = new MathBox(20, 10, 0);
        var shiftDown = MathFont.Default.Constant(MathConstant.SubscriptShiftDown, 100);
        var dropMin = MathFont.Default.Constant(MathConstant.SubscriptBaselineDropMin, 100);

        // Act
        var single = layout.Layout(character, sub, null, Compact);
        var twoGlyphs = layout.Layout(pair, sub, null, Compact);

        // Assert
        Assert.True(ScriptLayout.IsCharacter(character));
        Assert.False(ScriptLayout.IsCharacter(pair));
        Assert.Equal(shiftDown, single.Children[1].Y, 9);
        Assert.Equal(dropMin + 40, twoGlyphs.Children[1].Y, 9);
        Assert.True(dropMin + 40 > shiftDown);
    }

    /// <summary>
    ///     Validates that the subscript of a large operator is pulled back by its italic correction
    ///     (LargeOpItalicCorrection) and its superscript is not moved, and that the element ends SpaceAfterScript after the
    ///     script that ends last as placed: a subscript wider than the superscript ends the correction earlier than the
    ///     base and the subscript side by side would (Chrome counts the whole subscript after the base).
    /// </summary>
    [Fact]
    public void ScriptLayout_LayoutSub_LargeOpItalicCorrectionPullsSubscript()
    {
        // Arrange
        var integral = new MathBox(100, 120, 110) { ItalicCorrection = 50, Operator = new EmbellishedOperator(0, 0) { LargeOp = true } };

        // Act
        var box = new ScriptLayout(MathFont.Default).Layout(integral, new MathBox(30, 30, 0), new MathBox(35, 30, 0), Compact, integral.Operator);
        var wideLimit = new ScriptLayout(MathFont.Default).Layout(integral, new MathBox(90, 30, 0), new MathBox(35, 30, 0), Compact, integral.Operator);

        // Assert
        Assert.Equal(50, box.Children[1].X, 9);
        Assert.Equal(100, box.Children[2].X, 9);
        Assert.Equal(100 + 35 + Space, box.Width, 9);
        Assert.Equal(100 - 50 + 90 + Space, wideLimit.Width, 9);
        Assert.Same(integral.Operator, box.Operator);
    }

    /// <summary>
    ///     Validates the gap between both scripts (SubSuperscriptGapMin 160): the superscript is raised until its bottom
    ///     reaches SuperscriptBottomMaxWithSubscript (430), then the subscript is lowered.
    /// </summary>
    [Fact]
    public void ScriptLayout_LayoutSubSup_GapIsEnforced()
    {
        // Arrange: a tall subscript and a deep superscript, 20 px apart the wrong way at the standard shifts (21, 39)
        var layout = new ScriptLayout(MathFont.Default);
        var sub = new MathBox(20, 60, 0);
        var sup = new MathBox(20, 10, 20);

        // Act
        var box = layout.Layout(new MathBox(50, 20, 0), sub, sup, Compact);

        // Assert: SuperShift is raised until the bottom of the superscript is at 43, SubShift lowered by the rest
        var subShift = box.Children[1].Y;
        var supShift = -box.Children[2].Y;
        Assert.Equal(43 + 20, supShift, 9);
        Assert.Equal(16, (subShift - sub.Ascent) + (supShift - sup.Descent), 9);
        Assert.Equal(33, subShift, 9);
    }

    /// <summary>
    ///     Validates that SpaceAfterScript follows the wider script, and that the ascent and the descent are at least zero.
    /// </summary>
    [Fact]
    public void ScriptLayout_Layout_SpaceAfterScriptIsAdded()
    {
        // Arrange
        var layout = new ScriptLayout(MathFont.Default);

        // Act
        var sub = layout.Layout(new MathBox(50, 50, 0), new MathBox(30, 10, 0), null, Compact);
        var both = layout.Layout(new MathBox(50, 50, 0), new MathBox(30, 10, 0), new MathBox(45, 10, 0), Compact);
        var above = layout.Layout(new MathBox(50, 60, -40), null, new MathBox(10, 20, -5), Compact);

        // Assert
        Assert.Equal(50 + 30 + Space, sub.Width, 9);
        Assert.Equal(50 + 45 + Space, both.Width, 9);
        Assert.Equal(0, above.Descent);
        Assert.Throws<ArgumentException>(() => layout.Layout(new MathBox(1, 1, 1), null, null, Compact));
    }

    /// <summary>
    ///     Validates the placement of <c>mmultiscripts</c>: each prescript pair after SpaceAfterScript and aligned right in
    ///     its column, the base and the <c>mprescripts</c> box after them, the postscript pairs after the base, each
    ///     followed by SpaceAfterScript, and the shifts the largest over the pairs.
    /// </summary>
    [Fact]
    public void ScriptLayout_LayoutMultiscripts_PrescriptsAndPostscriptsArePlaced()
    {
        // Arrange
        var empty = new MathBox(0, 0, 0);
        var post = new ScriptPair[] { new(new MathBox(10, 10, 0), empty), new(empty, new MathBox(25, 10, 0)) };
        var pre = new ScriptPair[] { new(new MathBox(30, 10, 0), new MathBox(20, 10, 0)) };
        var separator = new MathBox(0, 0, 0);
        var @base = new MathBox(50, 50, 0);

        // Act
        var box = new ScriptLayout(MathFont.Default).LayoutMultiscripts(@base, post, pre, separator, Compact);

        // Assert: x positions (prescripts: sub, sup; base; mprescripts; postscripts: sub, sup, sub, sup)
        Assert.Equal([4.0, 14.0, 34.0, 34.0, 84.0, 84.0, 98.0, 98.0], box.Children.Select(child => Math.Round(child.X, 9)));
        Assert.Equal(34 + 50 + 10 + Space + 25 + Space, box.Width, 9);
        Assert.Equal(21, box.Children[0].Y, 9);
        Assert.Equal(-39, box.Children[1].Y, 9);
    }

    /// <summary>
    ///     Validates that an <c>mmultiscripts</c> with one postscript pair is laid out as <c>msubsup</c> with the same
    ///     children.
    /// </summary>
    [Fact]
    public void ScriptLayout_LayoutMultiscripts_OnePair_MatchesSubSup()
    {
        // Arrange
        var layout = new ScriptLayout(MathFont.Default);
        var @base = new MathBox(50, 50, 10);
        var sub = new MathBox(20, 40, 5);
        var sup = new MathBox(30, 10, 20);

        // Act
        var multi = layout.LayoutMultiscripts(@base, [new ScriptPair(sub, sup)], [], null, Compact);
        var subsup = layout.Layout(@base, sub, sup, Compact);

        // Assert
        Assert.Equal((subsup.Width, subsup.Ascent, subsup.Descent), (multi.Width, multi.Ascent, multi.Descent));
        Assert.Equal(subsup.Children.Select(child => (child.X, child.Y)), multi.Children.Select(child => (child.X, child.Y)));
    }
}
