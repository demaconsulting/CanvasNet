// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using GlyphPath = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Real-world integration test that loads the actual "Open Sans" TrueType font (see
///     <c>FontFixtures\README.md</c> for provenance and SIL OFL 1.1 licensing) and renders one of
///     its glyphs end-to-end through <see cref="TrueTypeFont"/> and the <c>Drawing</c> pipeline
///     (<see cref="PathFiller"/>). This proves the two subsystems genuinely compose against
///     production font data, complementing the hand-rolled synthetic fixtures every other
///     <c>Fonts</c> test builds via <see cref="TestSupport.SyntheticFontBuilder"/>.
/// </summary>
public class TrueTypeFontRealFontIntegrationTests
{
    /// <summary>
    ///     The path to the real "Open Sans" TrueType font, copied to the test output directory by
    ///     the test project's <c>FontFixtures\**</c> content item. <see cref="System.IO.Path.Join(string, string, string)"/>
    ///     is used instead of <see cref="System.IO.Path.Combine(string, string, string)"/> purely
    ///     to avoid CodeQL's <c>cs/path-combine</c> rule, since <c>Path.Join</c> does not discard
    ///     preceding segments when a later segment looks rooted.
    /// </summary>
    private static string FontPath => System.IO.Path.Join(AppContext.BaseDirectory, "FontFixtures", "OpenSans-Regular.ttf");

    /// <summary>
    ///     Proves that a real glyph loaded from a real production font, when transformed into
    ///     pixel space and filled onto a <see cref="Surface"/> via <see cref="PathFiller"/>,
    ///     produces actual visible ink within its expected bounding-box region while leaving the
    ///     canvas's far corners fully transparent - not merely "no exception was thrown".
    /// </summary>
    [Fact]
    public void TrueTypeFont_RealOpenSansFont_RendersGlyphOutlineAsVisibleInk()
    {
        // Arrange: load the real font from disk and resolve capital 'A' to a real glyph index
        var font = TrueTypeFont.Load(FontPath);
        var glyphIndex = font.GetGlyphIndex('A');
        Assert.NotEqual(0, glyphIndex); // must be a real mapped glyph, not .notdef

        // Act: extract the glyph's real outline (font design units, y-axis up), query its
        // advance width, and confirm a self-kerning lookup does not throw
        var outline = font.GetGlyphOutline(glyphIndex);
        var advance = font.GetAdvanceWidth(glyphIndex);
        var kerning = font.GetKerning(glyphIndex, glyphIndex);

        // Assert: the outline is real contour data, and metrics are sane for a real font
        Assert.NotEmpty(outline.Subpaths);
        Assert.True(advance > 0, "A real 'A' glyph must have a positive advance width.");
        _ = kerning; // GetKerning never throws; this call itself is the assertion

        // Arrange: transform the glyph outline from font design units (y-up) into pixel space
        // (y-down), scaled to fit within a small square canvas with a comfortable margin
        const int canvasSize = 128;
        const float margin = 16f;

        var rawBounds = outline.GetBounds(0.25f);
        Assert.False(rawBounds.IsEmpty);

        var maxExtent = Math.Max(rawBounds.Width, rawBounds.Height);
        var scale = (canvasSize - 2 * margin) / maxExtent;
        var transformed = TransformGlyphToPixelSpace(outline, rawBounds, scale, margin);

        // Act: fill the transformed glyph outline onto a fresh (fully transparent) surface
        var surface = new Surface(canvasSize, canvasSize);
        PathFiller.Fill(surface, transformed, new Rgba32(0, 0, 0, 255));

        // Assert: at least one pixel within the transformed glyph's own bounding box was
        // actually painted (non-transparent) - proving real ink was drawn, not just that no
        // exception occurred
        var pixelBounds = transformed.GetBounds(0.25f);
        Assert.False(pixelBounds.IsEmpty);

        var minX = Math.Max(0, (int)Math.Floor(pixelBounds.Left));
        var maxX = Math.Min(canvasSize - 1, (int)Math.Ceiling(pixelBounds.Right));
        var minY = Math.Max(0, (int)Math.Floor(pixelBounds.Top));
        var maxY = Math.Min(canvasSize - 1, (int)Math.Ceiling(pixelBounds.Bottom));

        var foundInk = false;
        for (var y = minY; y <= maxY && !foundInk; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (surface[x, y].A > 0)
                {
                    foundInk = true;
                    break;
                }
            }
        }

        Assert.True(foundInk, "Expected at least one filled (non-transparent) pixel within the glyph's transformed bounding box.");

        // Assert: the far corners of the canvas, well outside the margin-inset glyph bounding
        // box, remain fully transparent background
        Assert.Equal(0, surface[0, 0].A);
        Assert.Equal(0, surface[canvasSize - 1, 0].A);
        Assert.Equal(0, surface[0, canvasSize - 1].A);
        Assert.Equal(0, surface[canvasSize - 1, canvasSize - 1].A);
    }

    /// <summary>
    ///     Re-issues every subpath of <paramref name="outline"/> into a new <see cref="GlyphPath"/>,
    ///     mapping each point from font design-unit space (y-axis up, origin at <paramref name="bounds"/>'s
    ///     top-left in that space) into pixel space (y-axis down), scaled by <paramref name="scale"/>
    ///     and inset by <paramref name="margin"/> pixels on every side.
    /// </summary>
    private static GlyphPath TransformGlyphToPixelSpace(GlyphPath outline, Rect bounds, float scale, float margin)
    {
        Vector2 Map(Vector2 p) => new(
            (p.X - bounds.Left) * scale + margin,
            (bounds.Bottom - p.Y) * scale + margin);

        var builder = new PathBuilder();
        foreach (var subpath in outline.Subpaths)
        {
            builder.MoveTo(Map(subpath.Start));
            foreach (var command in subpath.Commands)
            {
                switch (command.Type)
                {
                    case PathCommandType.LineTo:
                        builder.LineTo(Map(command.EndPoint));
                        break;

                    case PathCommandType.QuadraticBezierTo:
                        builder.QuadraticBezierTo(Map(command.Control1), Map(command.EndPoint));
                        break;

                    case PathCommandType.Close:
                        builder.Close();
                        break;

                    default:
                        // TrueTypeFont glyph outlines only ever contain LineTo, QuadraticBezierTo,
                        // and Close commands (see GlyfLocaReader) - matching that reader's own
                        // AppendTransformed helper.
                        throw new InvalidOperationException("Unexpected path command in a glyph outline.");
                }
            }
        }

        return builder.Build();
    }
}
