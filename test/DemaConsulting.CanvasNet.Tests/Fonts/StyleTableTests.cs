// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
// cspell:ignore macStyle fsSelection usWeightClass isFixedPitch italicAngle
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="StyleTable"/>.
/// </summary>
public class StyleTableTests
{
    /// <summary>
    ///     Proves that StyleTable Parse Os2FsSelectionBoldBit IsBoldTrue.
    /// </summary>
    [Fact]
    public void StyleTable_Parse_Os2FsSelectionBoldBit_IsBoldTrue()
    {
        // Arrange: build an OS/2 table with only the BOLD fsSelection bit set
        var os2 = SyntheticFontBuilder.Os2(usWeightClass: 400, fsSelection: 0x20);

        // Act: parse the style table
        var style = ParseOs2Only(os2);

        // Assert: IsBold is true, and italic is unaffected
        Assert.True(style.IsBold);
        Assert.False(style.IsItalic);
    }

    /// <summary>
    ///     Proves that StyleTable Parse Os2WeightClassHigh IsBoldTrue.
    /// </summary>
    [Fact]
    public void StyleTable_Parse_Os2WeightClassHigh_IsBoldTrue()
    {
        // Arrange: build an OS/2 table with a heavy usWeightClass (700) and no fsSelection bits
        var os2 = SyntheticFontBuilder.Os2(usWeightClass: 700, fsSelection: 0);

        // Act: parse the style table
        var style = ParseOs2Only(os2);

        // Assert: IsBold is true purely from the weight class threshold
        Assert.True(style.IsBold);
    }

    /// <summary>
    ///     Proves that StyleTable Parse Os2WeightClassBelowThreshold IsBoldFalse.
    /// </summary>
    [Fact]
    public void StyleTable_Parse_Os2WeightClassBelowThreshold_IsBoldFalse()
    {
        // Arrange: build an OS/2 table with a regular usWeightClass (400) and no fsSelection bits
        var os2 = SyntheticFontBuilder.Os2(usWeightClass: 400, fsSelection: 0);

        // Act: parse the style table
        var style = ParseOs2Only(os2);

        // Assert: IsBold is false
        Assert.False(style.IsBold);
    }

    /// <summary>
    ///     Proves that StyleTable Parse Os2FsSelectionItalicBit IsItalicTrue.
    /// </summary>
    [Fact]
    public void StyleTable_Parse_Os2FsSelectionItalicBit_IsItalicTrue()
    {
        // Arrange: build an OS/2 table with only the ITALIC fsSelection bit set
        var os2 = SyntheticFontBuilder.Os2(usWeightClass: 400, fsSelection: 0x1);

        // Act: parse the style table
        var style = ParseOs2Only(os2);

        // Assert: IsItalic is true, and bold is unaffected
        Assert.True(style.IsItalic);
        Assert.False(style.IsBold);
    }

    /// <summary>
    ///     Proves that StyleTable Parse PostItalicAngleNonZero IsItalicTrue.
    /// </summary>
    [Fact]
    public void StyleTable_Parse_PostItalicAngleNonZero_IsItalicTrue()
    {
        // Arrange: build a post table with a nonzero italicAngle and no OS/2 table at all
        var post = SyntheticFontBuilder.Post(isFixedPitch: 0, italicAngle: -12.0);

        // Act: parse the style table (no OS/2, only post)
        var style = StyleTable.Parse(post, macStyle: 0, os2: null, post: (0, post.Length));

        // Assert: IsItalic is true purely from the nonzero italic angle
        Assert.True(style.IsItalic);
    }

    /// <summary>
    ///     Proves that StyleTable Parse Os2Absent FallsBackToMacStyleBits.
    /// </summary>
    [Fact]
    public void StyleTable_Parse_Os2Absent_FallsBackToMacStyleBits()
    {
        // Arrange: no OS/2 or post table at all, only head.macStyle bits set
        const int macStyle = 0x1 | 0x2; // Bold | Italic

        // Act: parse the style table
        var style = StyleTable.Parse([], macStyle, os2: null, post: null);

        // Assert: both IsBold and IsItalic are true, derived purely from macStyle
        Assert.True(style.IsBold);
        Assert.True(style.IsItalic);
    }

    /// <summary>
    ///     Proves that StyleTable Parse MacStyleAlwaysContributes EvenWhenOs2Present.
    /// </summary>
    [Fact]
    public void StyleTable_Parse_MacStyleAlwaysContributes_EvenWhenOs2Present()
    {
        // Arrange: an OS/2 table that itself indicates neither bold nor italic, but macStyle's
        // Bold bit is set
        var os2 = SyntheticFontBuilder.Os2(usWeightClass: 400, fsSelection: 0);

        // Act: parse the style table with both OS/2 present and macStyle's Bold bit set
        var style = StyleTable.Parse(os2, macStyle: 0x1, os2: (0, os2.Length), post: null);

        // Assert: IsBold is true - macStyle contributes even though OS/2 is present
        Assert.True(style.IsBold);
    }

    /// <summary>
    ///     Proves that StyleTable Parse PostIsFixedPitchNonZero IsFixedPitchTrue.
    /// </summary>
    [Fact]
    public void StyleTable_Parse_PostIsFixedPitchNonZero_IsFixedPitchTrue()
    {
        // Arrange: build a post table with isFixedPitch set
        var post = SyntheticFontBuilder.Post(isFixedPitch: 1);

        // Act: parse the style table
        var style = StyleTable.Parse(post, macStyle: 0, os2: null, post: (0, post.Length));

        // Assert: IsFixedPitch is true
        Assert.True(style.IsFixedPitch);
    }

    /// <summary>
    ///     Proves that StyleTable Parse PostAbsent IsFixedPitchFalse.
    /// </summary>
    [Fact]
    public void StyleTable_Parse_PostAbsent_IsFixedPitchFalse()
    {
        // Arrange/Act: parse the style table with no post table at all
        var style = StyleTable.Parse([], macStyle: 0, os2: null, post: null);

        // Assert: IsFixedPitch is false when post is entirely absent
        Assert.False(style.IsFixedPitch);
    }

    /// <summary>
    ///     Proves that StyleTable Parse Os2TooShortForFsSelection IgnoresFsSelectionFields.
    /// </summary>
    [Fact]
    public void StyleTable_Parse_Os2TooShortForFsSelection_IgnoresFsSelectionFields()
    {
        // Arrange: a present OS/2 table truncated to only 6 bytes - long enough for
        // usWeightClass but too short to reach fsSelection at all
        var os2 = SyntheticFontBuilder.Os2(usWeightClass: 400, fsSelection: 0x21)[..6];

        // Act: parse the style table
        var style = StyleTable.Parse(os2, macStyle: 0, os2: (0, os2.Length), post: null);

        // Assert: the too-short table's fsSelection bits are never read (bold/italic both
        // false), even though a full-length table with this same fsSelection value would set
        // both
        Assert.False(style.IsBold);
        Assert.False(style.IsItalic);
    }

    /// <summary>
    ///     Proves that StyleTable Parse Os2TooShortForWeightClass IgnoresWeightClassField.
    /// </summary>
    [Fact]
    public void StyleTable_Parse_Os2TooShortForWeightClass_IgnoresWeightClassField()
    {
        // Arrange: a present OS/2 table truncated to fewer than 6 bytes - too short even for
        // usWeightClass
        var os2 = SyntheticFontBuilder.Os2(usWeightClass: 900, fsSelection: 0)[..4];

        // Act: parse the style table
        var style = StyleTable.Parse(os2, macStyle: 0, os2: (0, os2.Length), post: null);

        // Assert: the too-short table's usWeightClass is never read
        Assert.False(style.IsBold);
    }

    /// <summary>
    ///     Proves that StyleTable Parse PostTooShortForIsFixedPitch IsFixedPitchFalse.
    /// </summary>
    [Fact]
    public void StyleTable_Parse_PostTooShortForIsFixedPitch_IsFixedPitchFalse()
    {
        // Arrange: a present post table truncated before isFixedPitch (offset 12, needs 16 bytes)
        var post = SyntheticFontBuilder.Post(isFixedPitch: 1)[..10];

        // Act: parse the style table
        var style = StyleTable.Parse(post, macStyle: 0, os2: null, post: (0, post.Length));

        // Assert: the too-short table's isFixedPitch is never read
        Assert.False(style.IsFixedPitch);
    }

    /// <summary>
    ///     Parses a style table backed only by the given OS/2 table bytes at offset 0, with no
    ///     macStyle bits and no post table - a convenience shared by the OS/2-focused tests above.
    /// </summary>
    private static StyleTable ParseOs2Only(byte[] os2) =>
        StyleTable.Parse(os2, macStyle: 0, os2: (0, os2.Length), post: null);
}
