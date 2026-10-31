using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tests.Canvas;

/// <summary>
///     Unit tests for the <see cref="Rgba32"/> parse/tryparse behavior and the shared internal
///     single-pixel <see cref="Rgba32.CompositeOver"/> Porter-Duff "over" alpha-blending helper
///     (reused by <c>DemaConsulting.CanvasNet.Pptx</c>'s <c>PaintPicture</c> and
///     <c>DemaConsulting.CanvasNet.Pdf</c>'s <c>CompositeImageOntoSurface</c>).
/// </summary>
public class Rgba32Tests
{
    /// <summary>Rgba32_Parse_6HexUppercase_ReturnsExpectedRgbaWithAlpha255.</summary>
    [Fact]
    public void Rgba32_Parse_6HexUppercase_ReturnsExpectedRgbaWithAlpha255()
    {
        var color = Rgba32.Parse("#ABCDEF");
        Assert.Equal(new Rgba32(0xAB, 0xCD, 0xEF, 255), color);
    }

    /// <summary>Rgba32_Parse_6HexLowercase_ReturnsExpectedRgbaWithAlpha255.</summary>
    [Fact]
    public void Rgba32_Parse_6HexLowercase_ReturnsExpectedRgbaWithAlpha255()
    {
        var color = Rgba32.Parse("#abcdef");
        Assert.Equal(new Rgba32(0xAB, 0xCD, 0xEF, 255), color);
    }

    /// <summary>Rgba32_Parse_6HexMixedCase_ReturnsExpectedRgba.</summary>
    [Fact]
    public void Rgba32_Parse_6HexMixedCase_ReturnsExpectedRgba()
    {
        Assert.Equal(new Rgba32(0xA1, 0xB2, 0xC3, 255), Rgba32.Parse("#a1B2c3"));
    }

    /// <summary>Rgba32_Parse_8HexUppercase_ReturnsExpectedArgb.</summary>
    [Fact]
    public void Rgba32_Parse_8HexUppercase_ReturnsExpectedArgb()
    {
        var color = Rgba32.Parse("#80112233");
        Assert.Equal(new Rgba32(0x11, 0x22, 0x33, 0x80), color);
    }

    /// <summary>Rgba32_Parse_8HexLowercase_ReturnsExpectedArgb.</summary>
    [Fact]
    public void Rgba32_Parse_8HexLowercase_ReturnsExpectedArgb()
    {
        var color = Rgba32.Parse("#80aabbcc");
        Assert.Equal(new Rgba32(0xAA, 0xBB, 0xCC, 0x80), color);
    }

    /// <summary>Rgba32_Parse_MissingHash_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_MissingHash_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse("ABCDEF"));
    }

    /// <summary>Rgba32_Parse_3HexShortForm_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_3HexShortForm_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse("#ABC"));
    }

    /// <summary>Rgba32_Parse_4HexArgbShortForm_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_4HexArgbShortForm_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse("#ABCD"));
    }

    /// <summary>Rgba32_Parse_7Hex_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_7Hex_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse("#ABCDEFA"));
    }

    /// <summary>Rgba32_Parse_9Hex_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_9Hex_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse("#123456789"));
    }

    /// <summary>Rgba32_Parse_NonHexCharacter_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_NonHexCharacter_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse("#GGGGGG"));
    }

    /// <summary>Rgba32_Parse_Null_ThrowsArgumentNullException.</summary>
    [Fact]
    public void Rgba32_Parse_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Rgba32.Parse(null!));
    }

    /// <summary>Rgba32_Parse_EmptyString_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_EmptyString_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse(""));
    }

    /// <summary>Rgba32_TryParse_ValidInput_ReturnsTrueAndSetsResult.</summary>
    [Fact]
    public void Rgba32_TryParse_ValidInput_ReturnsTrueAndSetsResult()
    {
        Assert.True(Rgba32.TryParse("#010203", out var c));
        Assert.Equal(new Rgba32(1, 2, 3, 255), c);
    }

    /// <summary>Rgba32_TryParse_InvalidInput_ReturnsFalseAndSetsResultToDefault.</summary>
    [Fact]
    public void Rgba32_TryParse_InvalidInput_ReturnsFalseAndSetsResultToDefault()
    {
        Assert.False(Rgba32.TryParse("#ZZZ", out var c));
        Assert.Equal(default, c);
    }

    /// <summary>Rgba32_TryParse_Null_ReturnsFalse.</summary>
    [Fact]
    public void Rgba32_TryParse_Null_ReturnsFalse()
    {
        Assert.False(Rgba32.TryParse(null, out var c));
        Assert.Equal(default, c);
    }

    /// <summary>Rgba32_Parse_ExceptionMessage_ContainsFormatGuidance.</summary>
    [Fact]
    public void Rgba32_Parse_ExceptionMessage_ContainsFormatGuidance()
    {
        var ex = Assert.Throws<FormatException>(() => Rgba32.Parse("XYZ"));
        Assert.Contains("#RRGGBB", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Rgba32_Parse_MissingHashPrefixAtValidLength_MessageIdentifiesMissingHash.</summary>
    [Fact]
    public void Rgba32_Parse_MissingHashPrefixAtValidLength_MessageIdentifiesMissingHash()
    {
        // "ABCDEF0" is 7 characters (a valid #RRGGBB length) but has no leading '#', so this
        // must reach the dedicated missing-'#' branch rather than the length-check branch.
        var ex = Assert.Throws<FormatException>(() => Rgba32.Parse("ABCDEF0"));
        Assert.Contains("'#'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Rgba32_Parse_NonHexCharacter_MessageIncludesOffendingCharacter.</summary>
    [Fact]
    public void Rgba32_Parse_NonHexCharacter_MessageIncludesOffendingCharacter()
    {
        var ex = Assert.Throws<FormatException>(() => Rgba32.Parse("#GGGGGG"));
        Assert.Contains("'G'", ex.Message, StringComparison.Ordinal);
    }

    // --- CompositeOver (shared single-pixel Porter-Duff "over" helper) -------------------------

    /// <summary>
    ///     Proves a fully transparent foreground (<c>alpha == 0</c>) leaves the background pixel
    ///     completely unchanged, regardless of the (irrelevant, since fully transparent) RGB
    ///     stored alongside that zero alpha.
    /// </summary>
    [Fact]
    public void Rgba32_CompositeOver_FullyTransparentForeground_ReturnsBackgroundUnchanged()
    {
        var background = new Rgba32(10, 20, 30, 255);
        var foreground = new Rgba32(255, 255, 255, 0);

        var result = Rgba32.CompositeOver(background, foreground);

        Assert.Equal(background, result);
    }

    /// <summary>
    ///     Proves a fully opaque foreground (<c>alpha == 255</c>) exactly replaces the background
    ///     pixel, regardless of the background's own color/alpha.
    /// </summary>
    [Fact]
    public void Rgba32_CompositeOver_FullyOpaqueForeground_ReturnsForegroundExactly()
    {
        var background = new Rgba32(10, 20, 30, 255);
        var foreground = new Rgba32(7, 8, 9, 255);

        var result = Rgba32.CompositeOver(background, foreground);

        Assert.Equal(foreground, result);
    }

    /// <summary>
    ///     Proves a partially transparent foreground composited over a fully opaque background
    ///     blends per the documented Porter-Duff "over" formula, asserting the exact expected
    ///     bytes (computed independently via the documented formula with round-half-away-from-zero).
    /// </summary>
    [Fact]
    public void Rgba32_CompositeOver_PartiallyTransparentForegroundOverOpaqueBackground_BlendsExactly()
    {
        var background = new Rgba32(0, 255, 0, 255);
        var foreground = new Rgba32(200, 100, 50, 128);

        var result = Rgba32.CompositeOver(background, foreground);

        Assert.Equal(new Rgba32(100, 177, 25, 255), result);
    }

    /// <summary>
    ///     Proves two partially transparent pixels (both background and foreground with
    ///     non-0/non-255 alpha) composite to the correctly premultiplied-then-unpremultiplied
    ///     color, with the composited alpha channel computed per <c>outA = fgA + bgA * (1 - fgA)</c>.
    /// </summary>
    [Fact]
    public void Rgba32_CompositeOver_BothBackgroundAndForegroundPartiallyTransparent_BlendsExactly()
    {
        var background = new Rgba32(100, 150, 200, 100);
        var foreground = new Rgba32(50, 60, 70, 90);

        var result = Rgba32.CompositeOver(background, foreground);

        Assert.Equal(new Rgba32(71, 98, 124, 155), result);
    }

    /// <summary>
    ///     Proves two fully transparent pixels (both background and foreground with
    ///     <c>alpha == 0</c>) composite to the fully transparent, zeroed-color degenerate result,
    ///     rather than a division-by-zero <c>NaN</c>.
    /// </summary>
    [Fact]
    public void Rgba32_CompositeOver_BothBackgroundAndForegroundFullyTransparent_ReturnsZeroedResult()
    {
        var background = new Rgba32(123, 45, 67, 0);
        var foreground = new Rgba32(89, 10, 11, 0);

        var result = Rgba32.CompositeOver(background, foreground);

        Assert.Equal(default, result);
    }

    /// <summary>
    ///     Proves the single-pixel helper agrees with the row-based <see cref="Surface.CompositeOver(Rgba32)"/>
    ///     for every combination of a grid of background and foreground alphas and colors, so the public
    ///     helper and the surface operation are interchangeable.
    /// </summary>
    [Fact]
    public void Rgba32_CompositeOver_MatchesSurfaceCompositeOverColor_AcrossAlphaGrid()
    {
        // Arrange: a grid of alphas (including 0, 255 and rounding-sensitive values) crossed with colors
        byte[] alphas = [0, 1, 64, 127, 128, 200, 254, 255];
        (byte R, byte G, byte B)[] colors = [(0, 0, 0), (255, 255, 255), (200, 50, 100), (13, 240, 77)];
        var pixels = new List<Rgba32>();
        foreach (var alpha in alphas)
        {
            foreach (var (r, g, b) in colors)
            {
                pixels.Add(new Rgba32(r, g, b, alpha));
            }
        }

        // Act / Assert: one surface row of every background, composited with each foreground in turn
        foreach (var foreground in pixels)
        {
            using var surface = new Surface(pixels.Count, 1);
            for (var x = 0; x < pixels.Count; x++)
            {
                surface[x, 0] = pixels[x];
            }

            surface.CompositeOver(foreground);

            for (var x = 0; x < pixels.Count; x++)
            {
                Assert.Equal(surface[x, 0], Rgba32.CompositeOver(pixels[x], foreground));
            }
        }
    }

    /// <summary>
    ///     Proves the helper is part of the public API: a public static method of the public struct.
    /// </summary>
    [Fact]
    public void Rgba32_CompositeOver_IsPublicStaticMethod()
    {
        // Arrange / Act: look the method up by its public signature
        var method = typeof(Rgba32).GetMethod(
            nameof(Rgba32.CompositeOver),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            [typeof(Rgba32), typeof(Rgba32)]);

        // Assert
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
        Assert.True(typeof(Rgba32).IsPublic);
        Assert.Equal(typeof(Rgba32), method.ReturnType);
    }

    /// <summary>
    ///     Proves <see cref="Rgba32.ToString"/> renders the documented <c>Rgba32(R, G, B, A)</c>
    ///     form with decimal channel values, independent of the current culture.
    /// </summary>
    [Fact]
    public void Rgba32_ToString_RepresentativeChannels_ReturnsInvariantFormattedString()
    {
        var color = new Rgba32(255, 0, 7, 128);
        var originalCulture = CultureInfo.CurrentCulture;
        string result;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            result = color.ToString();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        Assert.Equal("Rgba32(255, 0, 7, 128)", result);
    }
}
