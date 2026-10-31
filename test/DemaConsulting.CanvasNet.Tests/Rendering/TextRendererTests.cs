using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;
using DemaConsulting.CanvasNet.Tests.TestSupport;
using GeoPath = DemaConsulting.CanvasNet.Geometry.Path;
using RenderCanvas = DemaConsulting.CanvasNet.Rendering.Canvas;

namespace DemaConsulting.CanvasNet.Tests.Rendering;

/// <summary>Unit tests for <see cref="TextRenderer"/>.</summary>
public class TextRendererTests
{
    // Synthetic font: two glyphs.
    //   .notdef (index 0) with advance 0
    //   'A' (codepoint 65, index 1) with advance 500 units, outline is a triangle.
    // Ascender 800, descender -200. UnitsPerEm 1000.
    // Kerning pair (1, 1): -50.
    private static TrueTypeFont NewFont()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph(); // zero contours
        var glyphA = SyntheticFontBuilder.SimpleGlyph(
            [(0, 0, true), (500, 0, true), (250, 800, true)]);

        var kern = SyntheticFontBuilder.KernFormat0([(1, 1, -50)]);
        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length, glyphA.Length], longFormat: false))
            .AddTable("glyf", [.. notdef, .. glyphA])
            .AddTable("cmap", cmap)
            .AddTable("kern", kern)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    /// <summary>TextRenderer_MeasureText_EmptyString_ReturnsZeroWidth.</summary>
    [Fact]
    public void TextRenderer_MeasureText_EmptyString_ReturnsZeroWidth()
    {
        var font = NewFont();
        Assert.Equal(0f, TextRenderer.MeasureText("", font, 16f).Width);
    }

    /// <summary>TextRenderer_MeasureText_SingleGlyph_ReturnsAdvanceWidthScaledBySize.</summary>
    [Fact]
    public void TextRenderer_MeasureText_SingleGlyph_ReturnsAdvanceWidthScaledBySize()
    {
        var font = NewFont();
        var metrics = TextRenderer.MeasureText("A", font, 20f);
        // Advance 500 units * (20 / 1000) = 10.
        Assert.Equal(10f, metrics.Width, 4);
    }

    /// <summary>TextRenderer_MeasureText_MultipleGlyphs_SumsAdvancesAndAppliesKerning.</summary>
    [Fact]
    public void TextRenderer_MeasureText_MultipleGlyphs_SumsAdvancesAndAppliesKerning()
    {
        var font = NewFont();
        var metrics = TextRenderer.MeasureText("AA", font, 100f);
        // Two glyphs: 500 + kern(1,1) = -50 + 500 = 950 units * (100/1000) = 95.
        Assert.Equal(95f, metrics.Width, 3);
    }

    /// <summary>TextRenderer_MeasureText_ReturnsAscentAndDescentFromFontMetrics.</summary>
    [Fact]
    public void TextRenderer_MeasureText_ReturnsAscentAndDescentFromFontMetrics()
    {
        var font = NewFont();
        var metrics = TextRenderer.MeasureText("A", font, 100f);
        Assert.Equal(80f, metrics.Ascent, 3);
        Assert.Equal(20f, metrics.Descent, 3);
    }

    /// <summary>
    ///     A synthetic font whose <c>hhea.descender</c> is stored as a POSITIVE value, unlike
    ///     the normal (and every other test's) negative-descender convention. The font loader
    ///     preserves whatever sign the font file happens to use, without enforcing negativity, so
    ///     this exercises the documented "Descent is the absolute value of the descender"
    ///     contract against a font that violates the usual sign convention.
    /// </summary>
    private static TrueTypeFont NewFontWithPositiveDescender()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var glyphA = SyntheticFontBuilder.SimpleGlyph(
            [(0, 0, true), (500, 0, true), (250, 800, true)]);

        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, 200, 0, 2)) // descender is POSITIVE 200
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length, glyphA.Length], longFormat: false))
            .AddTable("glyf", [.. notdef, .. glyphA])
            .AddTable("cmap", cmap)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    /// <summary>
    ///     TextRenderer_MeasureText_PositiveRawDescender_ReturnsPositiveAbsoluteDescent.
    /// </summary>
    /// <remarks>
    ///     Regression test: <see cref="TextMetrics.Descent"/> is documented as the absolute value
    ///     of the font descender. A bare negation of a POSITIVE raw <c>hhea.descender</c> (as
    ///     produced by a font that doesn't follow the usual negative-descender convention) would
    ///     incorrectly yield a negative Descent; taking the absolute value keeps it positive
    ///     regardless of the font's sign convention. Distinct from
    ///     <see cref="TextRenderer_MeasureText_ReturnsAscentAndDescentFromFontMetrics"/>, which
    ///     covers the normal negative-descender case.
    /// </remarks>
    [Fact]
    public void TextRenderer_MeasureText_PositiveRawDescender_ReturnsPositiveAbsoluteDescent()
    {
        var font = NewFontWithPositiveDescender();
        var metrics = TextRenderer.MeasureText("A", font, 100f);

        // Raw descender is +200 units * (100/1000) = 20; the absolute value is still +20, not
        // -20 (which a bare `-font.Descender * scale` negation would have produced).
        Assert.Equal(20f, metrics.Descent, 3);
        Assert.True(metrics.Descent >= 0f, "Descent must be non-negative regardless of the font's descender sign convention.");
    }

    /// <summary>TextRenderer_MeasureText_NonFiniteSize_ThrowsArgumentOutOfRangeException.</summary>
    [Fact]
    public void TextRenderer_MeasureText_NonFiniteSize_ThrowsArgumentOutOfRangeException()
    {
        var font = NewFont();
        Assert.Throws<ArgumentOutOfRangeException>(() => TextRenderer.MeasureText("A", font, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextRenderer.MeasureText("A", font, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextRenderer.MeasureText("A", font, -1f));
    }

    /// <summary>TextRenderer_MeasureText_NullText_ThrowsArgumentNullException.</summary>
    [Fact]
    public void TextRenderer_MeasureText_NullText_ThrowsArgumentNullException()
    {
        var font = NewFont();
        Assert.Throws<ArgumentNullException>(() => TextRenderer.MeasureText(null!, font, 16f));
    }

    /// <summary>TextRenderer_MeasureText_NullFont_ThrowsArgumentNullException.</summary>
    [Fact]
    public void TextRenderer_MeasureText_NullFont_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TextRenderer.MeasureText("A", null!, 16f));
    }

    /// <summary>TextRenderer_DrawText_NullText_ThrowsArgumentNullException.</summary>
    [Fact]
    public void TextRenderer_DrawText_NullText_ThrowsArgumentNullException()
    {
        var canvas = new RenderCanvas(new Surface(64, 64));
        Assert.Throws<ArgumentNullException>(() => canvas.DrawText(null!, 0, 0, TextAlign.Left, NewFont(), 16f, new Rgba32(0, 0, 0, 255)));
    }

    /// <summary>TextRenderer_DrawText_NullFont_ThrowsArgumentNullException.</summary>
    [Fact]
    public void TextRenderer_DrawText_NullFont_ThrowsArgumentNullException()
    {
        var canvas = new RenderCanvas(new Surface(64, 64));
        Assert.Throws<ArgumentNullException>(() => canvas.DrawText("A", 0, 0, TextAlign.Left, null!, 16f, new Rgba32(0, 0, 0, 255)));
    }

    /// <summary>TextRenderer_DrawText_InvalidAlign_ThrowsArgumentOutOfRangeException.</summary>
    [Fact]
    public void TextRenderer_DrawText_InvalidAlign_ThrowsArgumentOutOfRangeException()
    {
        var canvas = new RenderCanvas(new Surface(64, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.DrawText("A", 0, 0, (TextAlign)99, NewFont(), 16f, new Rgba32(0, 0, 0, 255)));
    }

    /// <summary>TextRenderer_DrawText_LeftAlign_RendersGlyphsWithoutThrowing.</summary>
    [Fact]
    public void TextRenderer_DrawText_LeftAlign_RendersGlyphsWithoutThrowing()
    {
        var canvas = new RenderCanvas(new Surface(128, 64));
        canvas.DrawText("A", 10, 40, TextAlign.Left, NewFont(), 24f, new Rgba32(255, 0, 0, 255));

        // Verify some pixels were painted.
        var count = 0;
        for (var y = 0; y < canvas.Surface.Height; y++)
        {
            foreach (var px in canvas.Surface.GetRowSpan(y))
            {
                if (px.A > 0)
                {
                    count++;
                }
            }
        }

        Assert.True(count > 0, "DrawText produced no pixels");
    }

    /// <summary>TextRenderer_DrawText_CenterAlign_CentersRunAroundGivenX.</summary>
    [Fact]
    public void TextRenderer_DrawText_CenterAlign_CentersRunAroundGivenX()
    {
        // With Center alignment and text width W, the run left edge lands at x - W/2. Rendering
        // then produces pixels centered around x. We assert consistency by comparing to Left
        // alignment at x - W/2.
        var font = NewFont();
        var metrics = TextRenderer.MeasureText("A", font, 24f);
        var color = new Rgba32(0, 128, 0, 255);

        var s1 = new Surface(128, 64);
        new RenderCanvas(s1).DrawText("A", 64, 40, TextAlign.Center, font, 24f, color);

        var s2 = new Surface(128, 64);
        new RenderCanvas(s2).DrawText("A", 64 - metrics.Width / 2f, 40, TextAlign.Left, font, 24f, color);

        // Byte-identical.
        for (var y = 0; y < s1.Height; y++)
        {
            var r1 = s1.GetRowSpanBytes(y);
            var r2 = s2.GetRowSpanBytes(y);
            Assert.True(r1.SequenceEqual(r2), $"Row {y} mismatch");
        }
    }

    /// <summary>TextRenderer_DrawText_RightAlign_PlacesLastGlyphEndAtGivenX.</summary>
    [Fact]
    public void TextRenderer_DrawText_RightAlign_PlacesLastGlyphEndAtGivenX()
    {
        var font = NewFont();
        var metrics = TextRenderer.MeasureText("A", font, 24f);
        var color = new Rgba32(0, 128, 0, 255);

        var s1 = new Surface(128, 64);
        new RenderCanvas(s1).DrawText("A", 100, 40, TextAlign.Right, font, 24f, color);

        var s2 = new Surface(128, 64);
        new RenderCanvas(s2).DrawText("A", 100 - metrics.Width, 40, TextAlign.Left, font, 24f, color);

        for (var y = 0; y < s1.Height; y++)
        {
            Assert.True(s1.GetRowSpanBytes(y).SequenceEqual(s2.GetRowSpanBytes(y)));
        }
    }

    /// <summary>TextRenderer_DrawText_UnderTranslatedCanvas_ShiftsGlyphsByTranslation.</summary>
    [Fact]
    public void TextRenderer_DrawText_UnderTranslatedCanvas_ShiftsGlyphsByTranslation()
    {
        var font = NewFont();
        var color = new Rgba32(0, 0, 0, 255);

        var s1 = new Surface(128, 64);
        var c1 = new RenderCanvas(s1);
        c1.Translate(10, 0);
        c1.DrawText("A", 5, 40, TextAlign.Left, font, 24f, color);

        var s2 = new Surface(128, 64);
        new RenderCanvas(s2).DrawText("A", 15, 40, TextAlign.Left, font, 24f, color);

        for (var y = 0; y < s1.Height; y++)
        {
            Assert.True(s1.GetRowSpanBytes(y).SequenceEqual(s2.GetRowSpanBytes(y)));
        }
    }

    /// <summary>TextRenderer_DrawText_UnderRotatedCanvas_MatchesManuallyPreTransformedGlyphFill.</summary>
    /// <remarks>
    ///     Regression test: translation alone cannot catch an incorrect transform-composition
    ///     order or a sign error in the glyph's y-flip-vs-rotation interaction, since rotation and
    ///     y-flip don't commute. Draws text via <see cref="RenderCanvas"/> + <see cref="TextRenderer.DrawText"/>
    ///     inside a Save/Translate/RotateDegrees/Restore block for a 90-degree rotation (re-centered
    ///     onto the surface, since rotating strictly about the origin would carry the glyph
    ///     off-canvas), then independently re-derives the same glyph-space transform (scale +
    ///     y-flip + baseline translation, composed with the same rotation-then-recenter current
    ///     transform) and fills the raw glyph outline directly through the static
    ///     <see cref="PathFiller"/> - matching the "compare against manual pre-transform"
    ///     technique used for the gradient-transform fix elsewhere in this PR cycle. The two
    ///     surfaces must be byte-identical.
    /// </remarks>
    [Fact]
    public void TextRenderer_DrawText_UnderRotatedCanvas_MatchesManuallyPreTransformedGlyphFill()
    {
        var font = NewFont();
        var color = new Rgba32(0, 200, 100, 255);
        const float x = 0f;
        const float y = 0f;
        const float size = 24f;
        const float angleDegrees = 90f;
        const float centerX = 48f;
        const float centerY = 48f;

        // Act: render "A" via the public Canvas + TextRenderer API inside a rotated Save/Restore
        // block. Rotating strictly around the origin would carry the glyph off-canvas (its
        // baseline anchor sits at positive x/y), so a translate-to-center is composed after the
        // rotation: per Canvas' documented row-vector prepend order, calling Translate then
        // RotateDegrees means "rotate first (around the origin), then translate" - i.e. the
        // rotated glyph is re-centered onto the surface.
        var rotatedSurface = new Surface(96, 96);
        var rotatedCanvas = new RenderCanvas(rotatedSurface);
        rotatedCanvas.Save();
        rotatedCanvas.Translate(centerX, centerY);
        rotatedCanvas.RotateDegrees(angleDegrees);
        rotatedCanvas.DrawText("A", x, y, TextAlign.Left, font, size, color);
        rotatedCanvas.Restore();

        // Assert (manual re-derivation): independently reconstruct the same glyph-space
        // transform TextRenderer.DrawText bakes internally (scale + y-flip, baseline
        // translation, then the canvas' current transform composed on the outside) and fill the
        // raw glyph outline directly via the static PathFiller, entirely independently of
        // TextRenderer's own implementation.
        var glyphIndex = font.GetGlyphIndex('A');
        var outline = font.GetGlyphOutline(glyphIndex);
        var scale = size / font.UnitsPerEm;
        var rotation = Matrix3x2.CreateRotation(angleDegrees * (MathF.PI / 180f));
        var recenter = Matrix3x2.CreateTranslation(centerX, centerY);
        var currentTransform = rotation * recenter; // matches Canvas' Translate-then-RotateDegrees prepend order
        var glyphMatrix = Matrix3x2.CreateScale(scale, -scale) * Matrix3x2.CreateTranslation(x, y) * currentTransform;
        var manualSurface = new Surface(96, 96);
        PathFiller.Fill(manualSurface, outline.Transform(glyphMatrix), color);

        // Assert: both surfaces are painted identically, pixel for pixel, and the render actually
        // produced some non-trivial coverage (guards against a degenerate transform collapsing
        // the glyph to nothing).
        var paintedPixelCount = 0;
        for (var py = 0; py < rotatedSurface.Height; py++)
        {
            var rotatedRow = rotatedSurface.GetRowSpanBytes(py);
            var manualRow = manualSurface.GetRowSpanBytes(py);
            Assert.True(rotatedRow.SequenceEqual(manualRow), $"Row {py} mismatch");

            foreach (var px in rotatedSurface.GetRowSpan(py))
            {
                if (px.A > 0)
                {
                    paintedPixelCount++;
                }
            }
        }

        Assert.True(paintedPixelCount > 0, "DrawText under a rotated canvas produced no pixels");
    }

    /// <summary>
    ///     Describes a path as one token per start point and per command, in the round-trip text
    ///     format, so two paths can be compared bit for bit.
    /// </summary>
    /// <param name="path">The path to describe.</param>
    /// <returns>The tokens in order, with <c>!closed</c> on a closed subpath's start token.</returns>
    private static List<string> Describe(GeoPath path)
    {
        static string P(Vector2 v) => $"{v.X:R},{v.Y:R}";

        var tokens = new List<string>();
        foreach (var subpath in path.Subpaths)
        {
            tokens.Add($"M{P(subpath.Start)}{(subpath.IsClosed ? "!closed" : string.Empty)}");
            foreach (var c in subpath.Commands)
            {
                tokens.Add(c.Type switch
                {
                    PathCommandType.LineTo => $"L{P(c.EndPoint)}",
                    PathCommandType.QuadraticBezierTo => $"Q{P(c.Control1)} {P(c.EndPoint)}",
                    PathCommandType.CubicBezierTo => $"C{P(c.Control1)} {P(c.Control2)} {P(c.EndPoint)}",
                    _ => "Z",
                });
            }
        }

        return tokens;
    }

    /// <summary>
    ///     Test-local copy of the glyph-run algorithm the SVG codec used before
    ///     <see cref="TextRenderer.CreateTextPath"/> existed (its own measurement pass, anchor offset,
    ///     and per-point re-issue of each glyph outline); the independent oracle for the equivalence test.
    /// </summary>
    /// <param name="text">The text to lay out.</param>
    /// <param name="font">The font.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="origin">The anchor position.</param>
    /// <param name="anchor">0 = start, 1 = middle, 2 = end.</param>
    /// <returns>The combined path.</returns>
    private static GeoPath LegacyGlyphRun(string text, TrueTypeFont font, float fontSize, Vector2 origin, int anchor)
    {
        var scale = fontSize / font.UnitsPerEm;

        // Measurement pass: total advance including kerning
        var total = 0f;
        int? previous = null;
        foreach (var rune in text.EnumerateRunes())
        {
            var glyph = font.GetGlyphIndex(rune.Value);
            if (previous.HasValue)
            {
                total += font.GetKerning(previous.Value, glyph) * scale;
            }

            total += font.GetAdvanceWidth(glyph) * scale;
            previous = glyph;
        }

        var offset = anchor switch { 1 => total / 2f, 2 => total, _ => 0f };
        var builder = new PathBuilder();
        var pen = origin.X - offset;
        previous = null;
        foreach (var rune in text.EnumerateRunes())
        {
            var glyph = font.GetGlyphIndex(rune.Value);
            if (previous.HasValue)
            {
                pen += font.GetKerning(previous.Value, glyph) * scale;
            }

            var outline = font.GetGlyphOutline(glyph);
            if (outline.Subpaths.Count > 0)
            {
                var m = Matrix3x2.CreateScale(scale, -scale) * Matrix3x2.CreateTranslation(pen, origin.Y);
                foreach (var subpath in outline.Subpaths)
                {
                    builder.MoveTo(Vector2.Transform(subpath.Start, m));
                    foreach (var c in subpath.Commands)
                    {
                        switch (c.Type)
                        {
                            case PathCommandType.LineTo:
                                builder.LineTo(Vector2.Transform(c.EndPoint, m));
                                break;
                            case PathCommandType.QuadraticBezierTo:
                                builder.QuadraticBezierTo(Vector2.Transform(c.Control1, m), Vector2.Transform(c.EndPoint, m));
                                break;
                            case PathCommandType.CubicBezierTo:
                                builder.CubicBezierTo(Vector2.Transform(c.Control1, m), Vector2.Transform(c.Control2, m), Vector2.Transform(c.EndPoint, m));
                                break;
                            case PathCommandType.Close:
                                builder.Close();
                                break;
                        }
                    }
                }
            }

            pen += font.GetAdvanceWidth(glyph) * scale;
            previous = glyph;
        }

        return builder.Build();
    }

    /// <summary>
    ///     Proves that a left-aligned run places each glyph outline at the pen position (previous advance plus
    ///     kerning), y-flipped about the baseline and scaled to the requested size.
    /// </summary>
    [Fact]
    public void TextRenderer_CreateTextPath_LeftAlign_MatchesGlyphOutlinesPlacedAtPenWithKerning()
    {
        // Arrange: "AA" at size 100 (scale 0.1): the glyph is a triangle (0,0) (500,0) (250,800); the second
        // glyph's pen is 10 + 50 (advance) - 5 (kern -50 units) = 55
        var font = NewFont();

        // Act
        var path = TextRenderer.CreateTextPath("AA", font, 100f, 10f, 200f);

        // Assert: two triangles with baseline y = 200 and apex 80 units above it
        Assert.Equal(2, path.Subpaths.Count);
        var expectedPens = new[] { 10f, 55f };
        for (var i = 0; i < 2; i++)
        {
            var subpath = path.Subpaths[i];
            Assert.Equal(expectedPens[i], subpath.Start.X, 3);
            Assert.Equal(200f, subpath.Start.Y, 3);
            Assert.Equal(expectedPens[i] + 50f, subpath.Commands[0].EndPoint.X, 3);
            Assert.Equal(200f, subpath.Commands[0].EndPoint.Y, 3);
            Assert.Equal(expectedPens[i] + 25f, subpath.Commands[1].EndPoint.X, 3);
            Assert.Equal(120f, subpath.Commands[1].EndPoint.Y, 3);
        }
    }

    /// <summary>
    ///     Proves that center alignment shifts the run left by half of its measured width.
    /// </summary>
    [Fact]
    public void TextRenderer_CreateTextPath_CenterAlign_ShiftsRunByHalfMeasuredWidth()
    {
        // Arrange: "AA" measures 95 at size 100
        var font = NewFont();

        // Act
        var path = TextRenderer.CreateTextPath("AA", font, 100f, 100f, 0f, TextAlign.Center);

        // Assert: the first glyph starts at 100 - 47.5
        Assert.Equal(52.5f, path.Subpaths[0].Start.X, 3);
    }

    /// <summary>
    ///     Proves that right alignment shifts the run left by its full measured width.
    /// </summary>
    [Fact]
    public void TextRenderer_CreateTextPath_RightAlign_ShiftsRunByFullMeasuredWidth()
    {
        // Arrange: "AA" measures 95 at size 100
        var font = NewFont();

        // Act
        var path = TextRenderer.CreateTextPath("AA", font, 100f, 100f, 0f, TextAlign.Right);

        // Assert: the first glyph starts at 100 - 95
        Assert.Equal(5f, path.Subpaths[0].Start.X, 3);
    }

    /// <summary>
    ///     Proves that a run of glyphs without contours (spaces) yields the empty path.
    /// </summary>
    [Fact]
    public void TextRenderer_CreateTextPath_WhitespaceOnlyText_ReturnsEmptyPath()
    {
        // Arrange: a real font whose space glyph has an advance but no contours
        var font = SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false, italic: false);

        // Act
        var path = TextRenderer.CreateTextPath("   ", font, 16f, 5f, 20f);

        // Assert
        Assert.Empty(path.Subpaths);
    }

    /// <summary>
    ///     Proves that empty text yields the empty path.
    /// </summary>
    [Fact]
    public void TextRenderer_CreateTextPath_EmptyText_ReturnsEmptyPath()
    {
        // Arrange
        var font = NewFont();

        // Act
        var path = TextRenderer.CreateTextPath(string.Empty, font, 16f, 5f, 20f);

        // Assert
        Assert.Same(GeoPath.Empty, path);
    }

    /// <summary>
    ///     Proves that the result is bit-identical to the glyph-run algorithm the SVG codec used before the
    ///     method existed, across texts (kerning pairs, a surrogate pair, a missing glyph), sizes, anchors and
    ///     positions.
    /// </summary>
    [Fact]
    public void TextRenderer_CreateTextPath_MatchesLegacyGlyphRunOracle()
    {
        // Arrange: a bundled real font with kerning and the synthetic font
        var real = SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false, italic: false);
        var fonts = new[] { real, NewFont() };
        var texts = new[] { "Hello, World!", "AVATAR To Wa fi", "A\U0001F600 AA ü", "AAAA", " " };
        var aligns = new[] { TextAlign.Left, TextAlign.Center, TextAlign.Right };

        foreach (var font in fonts)
        {
            foreach (var text in texts)
            {
                foreach (var size in new[] { 7.5f, 16f, 33.3f })
                {
                    for (var a = 0; a < aligns.Length; a++)
                    {
                        // Act
                        var actual = TextRenderer.CreateTextPath(text, font, size, 12.25f, 40.5f, aligns[a]);
                        var oracle = LegacyGlyphRun(text, font, size, new Vector2(12.25f, 40.5f), a);

                        // Assert: identical structure and coordinates
                        Assert.Equal(Describe(oracle), Describe(actual));
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Proves that a null text is rejected.
    /// </summary>
    [Fact]
    public void TextRenderer_CreateTextPath_NullText_ThrowsArgumentNullException()
    {
        // Arrange
        var font = NewFont();

        // Act / Assert
        Assert.Throws<ArgumentNullException>("text", () => TextRenderer.CreateTextPath(null!, font, 16f, 0f, 0f));
    }

    /// <summary>
    ///     Proves that a null font is rejected.
    /// </summary>
    [Fact]
    public void TextRenderer_CreateTextPath_NullFont_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>("font", () => TextRenderer.CreateTextPath("A", null!, 16f, 0f, 0f));
    }

    /// <summary>
    ///     Proves that a size that is not finite and positive is rejected.
    /// </summary>
    [Fact]
    public void TextRenderer_CreateTextPath_NonPositiveOrNonFiniteSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var font = NewFont();

        // Act / Assert: zero, negative, NaN, and infinity
        foreach (var size in new[] { 0f, -16f, float.NaN, float.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>("size", () => TextRenderer.CreateTextPath("A", font, size, 0f, 0f));
        }
    }

    /// <summary>
    ///     Proves that an undefined alignment value is rejected.
    /// </summary>
    [Fact]
    public void TextRenderer_CreateTextPath_InvalidAlign_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var font = NewFont();

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>("align", () => TextRenderer.CreateTextPath("A", font, 16f, 0f, 0f, (TextAlign)99));
    }

    /// <summary>
    ///     Loads the bundled Liberation Sans font, a real font with kerning, used by the truncation tests.
    /// </summary>
    /// <returns>The font.</returns>
    private static TrueTypeFont RealFont() =>
        SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false, italic: false);

    /// <summary>
    ///     Test-local copy of the truncation algorithm the chart renderer used before
    ///     <see cref="TextRenderer.TruncateToWidth"/> existed (cutting by UTF-16 unit, ASCII ellipsis,
    ///     empty result when nothing fits); the independent oracle for ASCII text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="fontSize">The size in pixels.</param>
    /// <param name="maxWidth">The width budget.</param>
    /// <returns>The truncated text.</returns>
    private static string LegacyTruncateToFit(string text, TrueTypeFont font, float fontSize, float maxWidth)
    {
        if (maxWidth <= 0f)
        {
            return string.Empty;
        }

        if (TextRenderer.MeasureText(text, font, fontSize).Width <= maxWidth)
        {
            return text;
        }

        for (var length = text.Length - 1; length >= 0; length--)
        {
            var candidate = text[..length] + "...";
            if (TextRenderer.MeasureText(candidate, font, fontSize).Width <= maxWidth)
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    /// <summary>
    ///     Proves that a text that fits (including exactly at the limit) is returned as the same instance.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_TextFits_ReturnsTextUnchanged()
    {
        // Arrange: a text and a width budget equal to its measured width
        var font = RealFont();
        var text = new string('A', 3) + "bc";
        var width = TextRenderer.MeasureText(text, font, 14f).Width;

        // Act
        var exact = TextRenderer.TruncateToWidth(text, font, 14f, width, "...");
        var roomy = TextRenderer.TruncateToWidth(text, font, 14f, width + 100f, "...");

        // Assert
        Assert.Same(text, exact);
        Assert.Same(text, roomy);
    }

    /// <summary>
    ///     Proves that a too-wide text is cut to the longest beginning that, with the ellipsis, fits.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_TextTooWide_ReturnsLongestPrefixPlusEllipsisThatFits()
    {
        // Arrange
        var font = RealFont();
        var text = "The quick brown fox jumps over the lazy dog";
        const float maxWidth = 120f;

        // Act
        var fitted = TextRenderer.TruncateToWidth(text, font, 14f, maxWidth, "...");

        // Assert: it fits, is a beginning of the text plus the ellipsis, and one more character would not fit
        Assert.EndsWith("...", fitted);
        var kept = fitted[..^3];
        Assert.StartsWith(kept, text, StringComparison.Ordinal);
        Assert.True(kept.Length > 0 && kept.Length < text.Length);
        Assert.True(TextRenderer.MeasureText(fitted, font, 14f).Width <= maxWidth);
        Assert.True(TextRenderer.MeasureText(text[..(kept.Length + 1)] + "...", font, 14f).Width > maxWidth);
    }

    /// <summary>
    ///     Proves that for ASCII text and the ASCII ellipsis the result equals the algorithm the chart
    ///     renderer used before the method existed, over a sweep of widths (including zero and negative).
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_AsciiEllipsis_MatchesLegacyChartsAlgorithmAcrossWidthSweep()
    {
        // Arrange: kerning-prone texts, an empty one, and a sweep of widths
        var font = RealFont();
        var texts = new[] { "AVATAR To Wa fi ffl Category label 2024", "Quarterly revenue (USD)", "W", string.Empty };

        foreach (var text in texts)
        {
            for (var maxWidth = -5f; maxWidth <= 400f; maxWidth += 3.7f)
            {
                // Act
                var actual = TextRenderer.TruncateToWidth(text, font, 12f, maxWidth, "...");
                var expected = LegacyTruncateToFit(text, font, 12f, maxWidth);

                // Assert
                Assert.Equal(expected, actual);
            }
        }
    }

    /// <summary>
    ///     Proves that an empty result is returned when not even the ellipsis fits.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_EllipsisAloneDoesNotFit_ReturnsEmptyString()
    {
        // Arrange
        var font = RealFont();

        // Act
        var fitted = TextRenderer.TruncateToWidth("A long label", font, 14f, 1f, "...");

        // Assert
        Assert.Equal(string.Empty, fitted);
    }

    /// <summary>
    ///     Proves that an empty ellipsis gives a plain cut at the longest beginning that fits.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_EmptyEllipsis_CutsWithoutSuffix()
    {
        // Arrange
        var font = RealFont();
        var text = "abcdefghijklmnopqrstuvwxyz";
        const float maxWidth = 60f;

        // Act
        var fitted = TextRenderer.TruncateToWidth(text, font, 14f, maxWidth, string.Empty);

        // Assert: a beginning of the text that fits, while one more character does not
        Assert.StartsWith(fitted, text, StringComparison.Ordinal);
        Assert.True(fitted.Length > 0 && fitted.Length < text.Length);
        Assert.True(TextRenderer.MeasureText(fitted, font, 14f).Width <= maxWidth);
        Assert.True(TextRenderer.MeasureText(text[..(fitted.Length + 1)], font, 14f).Width > maxWidth);
    }

    /// <summary>
    ///     Proves that a surrogate pair is never split: every shortened result is well-formed UTF-16.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_SurrogatePair_IsNeverSplit()
    {
        // Arrange: astral characters (two UTF-16 units each) and a strict encoder that rejects a lone surrogate
        var font = RealFont();
        var text = string.Concat(Enumerable.Repeat("a\U0001F600", 20));
        var strict = new System.Text.UTF8Encoding(false, true);
        var shortened = 0;

        for (var maxWidth = 1f; maxWidth <= 300f; maxWidth += 2.3f)
        {
            // Act
            var fitted = TextRenderer.TruncateToWidth(text, font, 14f, maxWidth, "...");

            // Assert: encodes strictly; the kept beginning is a whole-character beginning of the text
            Assert.Equal(fitted, strict.GetString(strict.GetBytes(fitted)));
            if (fitted.EndsWith("...", StringComparison.Ordinal) && fitted.Length > 3)
            {
                Assert.StartsWith(fitted[..^3], text, StringComparison.Ordinal);
                shortened++;
            }
        }

        Assert.True(shortened > 10, "The sweep should have shortened the text for many widths.");
    }

    /// <summary>
    ///     Proves that a lone surrogate in the input becomes U+FFFD in a shortened result.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_LoneSurrogate_IsNormalizedToReplacementCharacter()
    {
        // Arrange: a lone high surrogate at index 3, and a budget that exactly fits "abc" + U+FFFD + the ellipsis
        var font = RealFont();
        const string text = "abc\uD83Ddefghijklmnop";
        const string expected = "abc\uFFFD...";
        var maxWidth = TextRenderer.MeasureText(expected, font, 14f).Width;

        // Act
        var fitted = TextRenderer.TruncateToWidth(text, font, 14f, maxWidth, "...");

        // Assert
        Assert.Equal(expected, fitted);
    }

    /// <summary>
    ///     Proves that white space at the end of the kept beginning is not trimmed.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_TrailingWhitespace_IsKeptInPrefix()
    {
        // Arrange: a budget that exactly fits "ab " + the ellipsis
        var font = RealFont();
        const string text = "ab cd ef gh ij kl";
        const string expected = "ab ...";
        var maxWidth = TextRenderer.MeasureText(expected, font, 14f).Width;

        // Act
        var fitted = TextRenderer.TruncateToWidth(text, font, 14f, maxWidth, "...");

        // Assert
        Assert.Equal(expected, fitted);
    }

    /// <summary>
    ///     Proves that the default ellipsis is the single character U+2026.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_DefaultEllipsis_IsHorizontalEllipsisCharacter()
    {
        // Arrange
        var font = RealFont();
        const string text = "The quick brown fox jumps over the lazy dog";

        // Act
        var fitted = TextRenderer.TruncateToWidth(text, font, 14f, 100f);

        // Assert
        Assert.EndsWith("\u2026", fitted);
        Assert.True(TextRenderer.MeasureText(fitted, font, 14f).Width <= 100f);
    }

    /// <summary>
    ///     Proves that a NaN width budget never fits, so the result is empty.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_NaNMaxWidth_ReturnsEmptyString()
    {
        // Arrange
        var font = RealFont();

        // Act
        var fitted = TextRenderer.TruncateToWidth("Some label", font, 14f, float.NaN, "...");

        // Assert
        Assert.Equal(string.Empty, fitted);
    }

    /// <summary>
    ///     Proves that a negative width budget never fits a non-empty text, so the result is empty.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_NegativeMaxWidth_ReturnsEmptyString()
    {
        // Arrange
        var font = RealFont();

        // Act
        var fitted = TextRenderer.TruncateToWidth("Some label", font, 14f, -10f, "...");

        // Assert
        Assert.Equal(string.Empty, fitted);
    }

    /// <summary>
    ///     Proves that empty text is returned as empty text when the budget is not negative.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_EmptyText_ReturnsEmptyString()
    {
        // Arrange
        var font = RealFont();

        // Act
        var atZero = TextRenderer.TruncateToWidth(string.Empty, font, 14f, 0f, "...");
        var wide = TextRenderer.TruncateToWidth(string.Empty, font, 14f, 100f);

        // Assert
        Assert.Equal(string.Empty, atZero);
        Assert.Equal(string.Empty, wide);
    }

    /// <summary>
    ///     Proves that a null text is rejected.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_NullText_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>("text", () => TextRenderer.TruncateToWidth(null!, RealFont(), 14f, 50f));
    }

    /// <summary>
    ///     Proves that a null font is rejected.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_NullFont_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>("font", () => TextRenderer.TruncateToWidth("abc", null!, 14f, 50f));
    }

    /// <summary>
    ///     Proves that a null ellipsis is rejected.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_NullEllipsis_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>("ellipsis", () => TextRenderer.TruncateToWidth("abc", RealFont(), 14f, 50f, null!));
    }

    /// <summary>
    ///     Proves that a size that is not finite and positive is rejected even when the text would fit.
    /// </summary>
    [Fact]
    public void TextRenderer_TruncateToWidth_NonPositiveSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var font = RealFont();

        // Act / Assert
        foreach (var size in new[] { 0f, -14f, float.NaN, float.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>("size", () => TextRenderer.TruncateToWidth("abc", font, size, 1000f));
        }
    }
}
