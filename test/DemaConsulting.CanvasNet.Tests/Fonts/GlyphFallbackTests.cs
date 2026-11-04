// cspell:ignore Noto
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="GlyphFallback"/>: the bundled per-character fallback chain.
/// </summary>
public class GlyphFallbackTests
{
    /// <summary>Gets the bundled Liberation Sans Regular font, which has no glyph for U+221D or U+226B.</summary>
    private static TrueTypeFont Liberation() =>
        SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false, italic: false);

    /// <summary>
    ///     Proves that the chain is the three bundled Noto fonts in the documented order.
    /// </summary>
    [Fact]
    public void GlyphFallback_BundledFonts_IsTheNotoTrioInOrder()
    {
        // Act
        var fonts = GlyphFallback.BundledFonts;

        // Assert: Noto Sans, Noto Sans Math, Noto Sans Symbols 2, the same instances the catalog caches
        Assert.Equal(3, fonts.Count);
        Assert.Same(SystemFontCatalog.LoadBundledFallbackCore("NotoSans-Regular.ttf"), fonts[0]);
        Assert.Same(SystemFontCatalog.LoadBundledFallbackCore("NotoSansMath-Regular.ttf"), fonts[1]);
        Assert.Same(SystemFontCatalog.LoadBundledFallbackCore("NotoSansSymbols2-Regular.ttf"), fonts[2]);
    }

    /// <summary>
    ///     Proves that a character Liberation lacks resolves to Noto Sans Math with a real glyph.
    /// </summary>
    [Theory]
    [InlineData(0x221D)]
    [InlineData(0x226B)]
    public void GlyphFallback_Resolve_CharacterMissingInPrimary_UsesNotoSansMath(int codePoint)
    {
        // Arrange
        var primary = Liberation();
        Assert.Equal(0, primary.GetGlyphIndex(codePoint));

        // Act
        var (font, glyph) = GlyphFallback.Resolve(primary, codePoint);

        // Assert: Noto Sans Math, a non-.notdef glyph
        Assert.Same(GlyphFallback.BundledFonts[1], font);
        Assert.NotEqual(0, glyph);
        Assert.NotEmpty(font.GetGlyphOutline(glyph).Subpaths);
    }

    /// <summary>
    ///     Proves that a character the primary font covers is returned untouched.
    /// </summary>
    [Fact]
    public void GlyphFallback_Resolve_CharacterCoveredByPrimary_ReturnsPrimaryGlyph()
    {
        // Arrange
        var primary = Liberation();

        // Act
        var (font, glyph) = GlyphFallback.Resolve(primary, 'A');

        // Assert
        Assert.Same(primary, font);
        Assert.Equal(primary.GetGlyphIndex('A'), glyph);
    }

    /// <summary>
    ///     Proves that a character no font covers resolves to the primary font's missing glyph.
    /// </summary>
    [Theory]
    [InlineData(0xE000)]
    [InlineData(0x4E2D)]
    public void GlyphFallback_Resolve_CharacterCoveredByNoFont_ReturnsPrimaryNotdef(int codePoint)
    {
        // Arrange
        var primary = Liberation();

        // Act
        var (font, glyph) = GlyphFallback.Resolve(primary, codePoint);

        // Assert
        Assert.Same(primary, font);
        Assert.Equal(0, glyph);
    }

    /// <summary>
    ///     Proves that a chain member used as the primary font is not consulted twice.
    /// </summary>
    [Fact]
    public void GlyphFallback_Resolve_PrimaryIsChainMember_SkipsItselfAndFindsNextFont()
    {
        // Arrange: Noto Sans has no U+221D, Noto Sans Math has it
        var notoSans = GlyphFallback.BundledFonts[0];
        Assert.Equal(0, notoSans.GetGlyphIndex(0x221D));

        // Act
        var (font, glyph) = GlyphFallback.Resolve(notoSans, 0x221D);

        // Assert
        Assert.Same(GlyphFallback.BundledFonts[1], font);
        Assert.NotEqual(0, glyph);
    }

    /// <summary>
    ///     Proves that characters without a visible glyph are never replaced and never reported.
    /// </summary>
    [Theory]
    [InlineData(0x0000)]
    [InlineData(0x0009)]
    [InlineData(0x0020)]
    [InlineData(0x200D)]
    [InlineData(0x200E)]
    [InlineData(0xFE0F)]
    [InlineData(0x2028)]
    [InlineData(0xFFFF)]
    [InlineData(0xE0100)]
    public void GlyphFallback_NeedsGlyph_InvisibleCharacter_ReturnsFalse(int codePoint)
    {
        // Act / Assert
        Assert.False(GlyphFallback.NeedsGlyph(codePoint));
    }

    /// <summary>
    ///     Proves that visible, private-use, and unassigned characters need a glyph, and invalid code points do not.
    /// </summary>
    [Fact]
    public void GlyphFallback_NeedsGlyph_VisibleAndPrivateUse_ReturnsTrueAndInvalidReturnsFalse()
    {
        // Act / Assert
        Assert.True(GlyphFallback.NeedsGlyph('A'));
        Assert.True(GlyphFallback.NeedsGlyph(0x221D));
        Assert.True(GlyphFallback.NeedsGlyph(0xE000));
        Assert.False(GlyphFallback.NeedsGlyph(-1));
        Assert.False(GlyphFallback.NeedsGlyph(0x110000));
        Assert.False(GlyphFallback.NeedsGlyph(0xD800));
    }

    /// <summary>
    ///     Proves that invisible, zero-width, and invalid code points are ignorable.
    /// </summary>
    [Theory]
    [InlineData(0x0000)]
    [InlineData(0x0007)]
    [InlineData(0x00AD)]
    [InlineData(0x200B)]
    [InlineData(0x200C)]
    [InlineData(0x200D)]
    [InlineData(0x200E)]
    [InlineData(0x2060)]
    [InlineData(0xFEFF)]
    [InlineData(0xFE00)]
    [InlineData(0xFE0F)]
    [InlineData(0xE0100)]
    [InlineData(0xE01EF)]
    [InlineData(0xFFFE)]
    [InlineData(0xFDD0)]
    [InlineData(0xD800)]
    [InlineData(-1)]
    [InlineData(0x110000)]
    public void GlyphFallback_IsIgnorable_InvisibleOrInvalidCharacter_ReturnsTrue(int codePoint)
    {
        // Act / Assert
        Assert.True(GlyphFallback.IsIgnorable(codePoint));
    }

    /// <summary>
    ///     Proves that visible characters and white space (which a layout handles itself) are not ignorable.
    /// </summary>
    [Theory]
    [InlineData('A')]
    [InlineData(0x0020)]
    [InlineData(0x0009)]
    [InlineData(0x000A)]
    [InlineData(0x00A0)]
    [InlineData(0x221D)]
    [InlineData(0xE000)]
    public void GlyphFallback_IsIgnorable_VisibleCharacterOrWhiteSpace_ReturnsFalse(int codePoint)
    {
        // Act / Assert
        Assert.False(GlyphFallback.IsIgnorable(codePoint));
    }

    /// <summary>
    ///     Proves that NeedsGlyph, now built on IsIgnorable, gives the same answer for every code point as
    ///     the Unicode-category definition it replaced (no control, format, separator, or surrogate; no
    ///     variation selector or non-character; inside the Unicode range).
    /// </summary>
    [Fact]
    public void GlyphFallback_NeedsGlyph_EveryCodePoint_MatchesUnicodeCategoryDefinition()
    {
        // Act / Assert: sweep the invalid ranges and the whole Unicode range
        for (var codePoint = -2; codePoint <= 0x110002; codePoint++)
        {
            Assert.True(
                ExpectedNeedsGlyph(codePoint) == GlyphFallback.NeedsGlyph(codePoint),
                $"U+{codePoint:X4}");
            if (GlyphFallback.IsIgnorable(codePoint))
            {
                Assert.False(GlyphFallback.NeedsGlyph(codePoint), $"U+{codePoint:X4}");
            }
        }
    }

    /// <summary>
    ///     The category-based definition of "needs a glyph" that predates IsIgnorable.
    /// </summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns><see langword="true"/> when the character is expected to draw.</returns>
    private static bool ExpectedNeedsGlyph(int codePoint)
    {
        if (codePoint < 0 || codePoint > 0x10FFFF || codePoint is >= 0xD800 and <= 0xDFFF)
        {
            return false;
        }

        if (codePoint is >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF
            || codePoint is >= 0xFDD0 and <= 0xFDEF || (codePoint & 0xFFFE) == 0xFFFE)
        {
            return false;
        }

        return System.Text.Rune.GetUnicodeCategory(new System.Text.Rune(codePoint)) is not (
            System.Globalization.UnicodeCategory.Control
            or System.Globalization.UnicodeCategory.Format
            or System.Globalization.UnicodeCategory.SpaceSeparator
            or System.Globalization.UnicodeCategory.LineSeparator
            or System.Globalization.UnicodeCategory.ParagraphSeparator);
    }

    /// <summary>
    ///     Proves that the published chain is a read-only wrapper that cannot be cast back to a mutable array.
    /// </summary>
    [Fact]
    public void GlyphFallback_BundledFonts_IsReadOnlyWrapper_CannotBeMutated()
    {
        // Act
        var fonts = GlyphFallback.BundledFonts;

        // Assert: not an array, rejects writes, and is the same instance on every access
        Assert.False(fonts is TrueTypeFont[]);
        var list = Assert.IsAssignableFrom<IList<TrueTypeFont>>(fonts);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = fonts[1]);
        Assert.Same(fonts, GlyphFallback.BundledFonts);
    }

    /// <summary>
    ///     Proves that an invisible character the primary font lacks is not looked up in the chain.
    /// </summary>
    [Fact]
    public void GlyphFallback_Resolve_InvisibleCharacterMissingInPrimary_ReturnsPrimaryNotdef()
    {
        // Arrange: U+E0100 (a variation selector) is not mapped by Liberation
        var primary = Liberation();

        // Act
        var (font, glyph) = GlyphFallback.Resolve(primary, 0xE0100);

        // Assert
        Assert.Same(primary, font);
        Assert.Equal(0, glyph);
    }

    /// <summary>
    ///     Proves that covered text reports nothing and uncovered characters are listed distinct and ascending.
    /// </summary>
    [Fact]
    public void GlyphFallback_FindMissing_ReportsOnlyUncoveredCharactersDistinctAndAscending()
    {
        // Arrange
        var primary = Liberation();

        // Act
        var covered = GlyphFallback.FindMissing(primary, "a \u221D b \u226B c \u00FC");
        var missing = GlyphFallback.FindMissing(primary, "a \u4E2D \uE000 \u200D \t \uE000");

        // Assert
        Assert.Empty(covered);
        Assert.Equal(new[] { 0x4E2D, 0xE000 }, missing);
    }

    /// <summary>
    ///     Proves that a lone surrogate is judged as U+FFFD, as it is when drawn.
    /// </summary>
    [Fact]
    public void GlyphFallback_FindMissing_LoneSurrogate_IsTreatedAsReplacementCharacter()
    {
        // Act
        var missing = GlyphFallback.FindMissing(Liberation(), "a\uD800b");

        // Assert: U+FFFD is mapped by Liberation or the chain, so it is not missing
        Assert.DoesNotContain(0xD800, missing);
    }

    /// <summary>
    ///     Proves that null arguments are rejected.
    /// </summary>
    [Fact]
    public void GlyphFallback_NullArguments_ThrowArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>("primary", () => GlyphFallback.Resolve(null!, 'A'));
        Assert.Throws<ArgumentNullException>("primary", () => GlyphFallback.FindMissing(null!, "a"));
        Assert.Throws<ArgumentNullException>("text", () => GlyphFallback.FindMissing(Liberation(), null!));
    }

    /// <summary>
    ///     Proves that concurrent resolution gives the same answer on every thread.
    /// </summary>
    [Fact]
    public void GlyphFallback_Resolve_ConcurrentCalls_AreDeterministic()
    {
        // Arrange
        var primary = Liberation();
        var results = new (TrueTypeFont, int)[64];

        // Act
        Parallel.For(0, results.Length, i => results[i] = GlyphFallback.Resolve(primary, 0x221D));

        // Assert
        Assert.All(results, r => Assert.Equal(results[0], r));
        Assert.NotEqual(0, results[0].Item2);
    }
}
