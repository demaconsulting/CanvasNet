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
/// <remarks>
///     The expected <see cref="TrueTypeFont.GetNameInfo"/>/<see cref="TrueTypeFont.IsBold"/>/
///     <see cref="TrueTypeFont.IsItalic"/>/<see cref="TrueTypeFont.IsFixedPitch"/> values asserted
///     by the name/style metadata tests below were confirmed directly against these exact fixture
///     files using a throwaway <c>fonttools</c> (Python) inspection script - not guessed - which
///     read <c>name.getName(nameID, platformID, encodingID, languageID)</c> for IDs 1/2/4/6/16/17
///     under both <c>(3, 1, 0x409)</c> and <c>(1, 0, 0)</c>, and printed
///     <c>OS/2.fsSelection</c>/<c>usWeightClass</c>, <c>head.macStyle</c>, and
///     <c>post.isFixedPitch</c>/<c>italicAngle</c>. All three fixtures (and both faces of the
///     <c>.ttc</c>) were confirmed to have no <c>nameID</c> 16/17 records, and to be
///     Regular/non-bold/non-italic/non-fixed-pitch.
/// </remarks>
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
    ///     The path to the real "Source Sans 3" CFF/OpenType (<c>.otf</c>) font (see
    ///     <c>FontFixtures\README.md</c> for provenance and SIL OFL 1.1 licensing).
    /// </summary>
    private static string OtfFontPath => System.IO.Path.Join(AppContext.BaseDirectory, "FontFixtures", "SourceSans3-Regular.otf");

    /// <summary>
    ///     The path to the synthetic <c>ttcf</c> container locally assembled from
    ///     <see cref="FontPath"/> (face 0) and <see cref="OtfFontPath"/> (face 1) - see
    ///     <c>FontFixtures\README.md</c> for how it was built and its licensing basis.
    /// </summary>
    private static string TtcFontPath => System.IO.Path.Join(AppContext.BaseDirectory, "FontFixtures", "OpenSans-SourceSans3.ttc");

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

        AssertGlyphRendersAsVisibleInk(font, glyphIndex);
    }

    /// <summary>
    ///     Proves that a real glyph decoded from a real CFF/OpenType (<c>.otf</c>) production
    ///     font's Type 2 charstring bytecode, when transformed into pixel space and filled onto a
    ///     <see cref="Surface"/>, produces actual visible ink - proving <see cref="CffTable"/>/
    ///     <see cref="CffCharstringInterpreter"/> genuinely decode real-world CFF outline data,
    ///     not just synthetic fixtures. The glyph ('H') is deliberately chosen from among a set of
    ///     straight-line-only Latin letters verified not to require the Type 2 flex escape
    ///     operators, which are outside this library's supported operator set (see
    ///     <c>FontFixtures\README.md</c> for the verification detail).
    /// </summary>
    [Fact]
    public void TrueTypeFont_RealSourceSans3OtfFont_RendersCffGlyphOutlineAsVisibleInk()
    {
        // Arrange: load the real CFF/OpenType font from disk and resolve capital 'H'
        var font = TrueTypeFont.Load(OtfFontPath);
        var glyphIndex = font.GetGlyphIndex('H');
        Assert.NotEqual(0, glyphIndex); // must be a real mapped glyph, not .notdef

        AssertGlyphRendersAsVisibleInk(font, glyphIndex);
    }

    /// <summary>
    ///     Proves that a locally-assembled <c>ttcf</c> TrueType Collection fixture's second face
    ///     (the CFF/OpenType "Source Sans 3" font) is independently loadable and decodable via
    ///     <see cref="TrueTypeFont.Load(string, int)"/>, with its glyph outline rendering as real
    ///     visible ink exactly as it does when loaded as a standalone <c>.otf</c> file.
    /// </summary>
    [Fact]
    public void TrueTypeFont_RealTtcContainer_FaceOne_RendersCffGlyphOutlineAsVisibleInk()
    {
        // Arrange: the ttcf container's face count and explicit face-1 selection
        var faceCount = TrueTypeFont.GetFaceCount(TtcFontPath);
        Assert.Equal(2, faceCount);

        var font = TrueTypeFont.Load(TtcFontPath, 1);
        var glyphIndex = font.GetGlyphIndex('H');
        Assert.NotEqual(0, glyphIndex);

        AssertGlyphRendersAsVisibleInk(font, glyphIndex);
    }

    /// <summary>
    ///     Proves that the locally-assembled <c>ttcf</c> container's first face (the glyf-flavored
    ///     "Open Sans" font) is independently loadable and decodable via
    ///     <see cref="TrueTypeFont.Load(string, int)"/> with an explicit face index of
    ///     <c>0</c>, with its glyph outline rendering as real visible ink exactly as it does when
    ///     loaded as a standalone <c>.ttf</c> file - complementing
    ///     <see cref="TrueTypeFont_RealTtcContainer_FaceOne_RendersCffGlyphOutlineAsVisibleInk"/>'s
    ///     coverage of the container's second (CFF-flavored) face, so both faces of the real
    ///     <c>.ttc</c> fixture are proven independently renderable through the explicit
    ///     face-index API, not merely the default/no-index overload.
    /// </summary>
    [Fact]
    public void TrueTypeFont_RealTtcContainer_FaceZero_RendersGlyfGlyphOutlineAsVisibleInk()
    {
        // Arrange: the ttcf container's face count and explicit face-0 selection
        var faceCount = TrueTypeFont.GetFaceCount(TtcFontPath);
        Assert.Equal(2, faceCount);

        var font = TrueTypeFont.Load(TtcFontPath, 0);
        var glyphIndex = font.GetGlyphIndex('A');
        Assert.NotEqual(0, glyphIndex);

        AssertGlyphRendersAsVisibleInk(font, glyphIndex);
    }

    /// <summary>
    ///     Proves that the real "Open Sans" production font exposes the exact family/subfamily/
    ///     full/PostScript name and bold/italic/fixed-pitch style metadata confirmed directly
    ///     against the fixture via <c>fonttools</c> (see this class's remarks): a font with no
    ///     <c>nameID</c> 16/17 records (so the 1/2 fallback path resolves), non-bold, non-italic,
    ///     non-fixed-pitch.
    /// </summary>
    [Fact]
    public void TrueTypeFont_RealOpenSansFont_ExposesExpectedNameAndStyleMetadata()
    {
        // Act: load the real font and resolve its name/style metadata
        var font = TrueTypeFont.Load(FontPath);
        var info = font.GetNameInfo();

        // Assert: the exact values confirmed via fonttools inspection of this fixture
        Assert.Equal("Open Sans", info.FamilyName);
        Assert.Equal("Regular", info.SubfamilyName);
        Assert.Equal("Open Sans Regular", info.FullName);
        Assert.Equal("OpenSans-Regular", info.PostScriptName);
        Assert.False(font.IsBold);
        Assert.False(font.IsItalic);
        Assert.False(font.IsFixedPitch);
    }

    /// <summary>
    ///     Proves that the real "Source Sans 3" CFF/OpenType production font exposes the exact
    ///     name/style metadata confirmed directly against the fixture via <c>fonttools</c> (see
    ///     this class's remarks).
    /// </summary>
    [Fact]
    public void TrueTypeFont_RealSourceSans3OtfFont_ExposesExpectedNameAndStyleMetadata()
    {
        // Act: load the real CFF/OpenType font and resolve its name/style metadata
        var font = TrueTypeFont.Load(OtfFontPath);
        var info = font.GetNameInfo();

        // Assert: the exact values confirmed via fonttools inspection of this fixture
        Assert.Equal("Source Sans 3", info.FamilyName);
        Assert.Equal("Regular", info.SubfamilyName);
        Assert.Equal("Source Sans 3", info.FullName);
        Assert.Equal("SourceSans3-Regular", info.PostScriptName);
        Assert.False(font.IsBold);
        Assert.False(font.IsItalic);
        Assert.False(font.IsFixedPitch);
    }

    /// <summary>
    ///     Proves that both faces of the locally-assembled <c>ttcf</c> container independently
    ///     expose their own real name/style metadata - face 0's "Open Sans" metadata and face 1's
    ///     "Source Sans 3" metadata are never cross-contaminated, confirming per-face table
    ///     resolution is correct for <c>name</c>/<c>OS/2</c>/<c>post</c> exactly as it already is
    ///     for every other table this unit reads.
    /// </summary>
    [Fact]
    public void TrueTypeFont_RealTtcContainer_BothFaces_ExposeExpectedNameAndStyleMetadataIndependently()
    {
        // Act: load both faces and resolve each one's own name/style metadata
        var faceZero = TrueTypeFont.Load(TtcFontPath, 0);
        var faceZeroInfo = faceZero.GetNameInfo();

        var faceOne = TrueTypeFont.Load(TtcFontPath, 1);
        var faceOneInfo = faceOne.GetNameInfo();

        // Assert: face 0 matches the standalone Open Sans fixture's metadata
        Assert.Equal("Open Sans", faceZeroInfo.FamilyName);
        Assert.Equal("OpenSans-Regular", faceZeroInfo.PostScriptName);
        Assert.False(faceZero.IsBold);
        Assert.False(faceZero.IsItalic);
        Assert.False(faceZero.IsFixedPitch);

        // Assert: face 1 matches the standalone Source Sans 3 fixture's metadata, not face 0's
        Assert.Equal("Source Sans 3", faceOneInfo.FamilyName);
        Assert.Equal("SourceSans3-Regular", faceOneInfo.PostScriptName);
        Assert.False(faceOne.IsBold);
        Assert.False(faceOne.IsItalic);
        Assert.False(faceOne.IsFixedPitch);
    }

    /// <summary>
    ///     Loads the bundled Noto Sans Math font (the shared, cached instance) - the font whose
    ///     <c>MATH</c> table MathML layout reads. The expected values asserted below were read from
    ///     this exact font file with a throwaway <c>fonttools</c> (Python) inspection script, not
    ///     guessed.
    /// </summary>
    private static TrueTypeFont LoadNotoSansMath() => SystemFontCatalog.LoadBundledFallbackCore("NotoSansMath-Regular.ttf");

    /// <summary>
    ///     Gets the <c>MATH</c> table of a font that must have one.
    /// </summary>
    private static MathTable RequireMathTable(TrueTypeFont font)
    {
        Assert.True(font.TryGetMathTable(out var table));
        return table;
    }

    /// <summary>
    ///     Proves that the real Noto Sans Math <c>MATH</c> table parses and exposes its
    ///     MathConstants and MinConnectorOverlap as confirmed with <c>fonttools</c>.
    /// </summary>
    [Fact]
    public void TrueTypeFont_RealNotoSansMath_ReadsMathConstants()
    {
        // Act: parse the real MATH table
        var table = RequireMathTable(LoadNotoSansMath());
        var constants = table.Constants;

        // Assert: the fonttools-confirmed values
        Assert.Equal(278, constants.AxisHeight);
        Assert.Equal(60, constants.ScriptPercentScaleDown);
        Assert.Equal(50, constants.ScriptScriptPercentScaleDown);
        Assert.Equal(2300, constants.DelimitedSubFormulaMinHeight);
        Assert.Equal(2300, constants.DisplayOperatorMinHeight);
        Assert.Equal(150, constants.MathLeading);
        Assert.Equal(71, constants.FractionRuleThickness);
        Assert.Equal(-450, constants.RadicalKernAfterDegree);
        Assert.Equal(56, constants.RadicalDegreeBottomRaisePercent);
        Assert.Equal(100, table.MinConnectorOverlap);
    }

    /// <summary>
    ///     Proves that the real vertical constructions of the left parenthesis and the square root
    ///     sign are read with every variant and every assembly part, including connector lengths
    ///     and extender flags, as confirmed with <c>fonttools</c>.
    /// </summary>
    [Fact]
    public void TrueTypeFont_RealNotoSansMath_ReadsParenthesisConstruction()
    {
        // Arrange
        var font = LoadNotoSansMath();
        var table = RequireMathTable(font);
        var parenthesis = font.GetGlyphIndex('(');
        var radical = font.GetGlyphIndex(0x221A);

        // Act
        var parenthesisConstruction = table.GetVerticalConstruction(parenthesis);
        var radicalConstruction = table.GetVerticalConstruction(radical);

        // Assert: '(' has 13 variants from its own glyph up, and a bottom/extender/top assembly
        Assert.Equal(1864, parenthesis);
        Assert.NotNull(parenthesisConstruction);
        Assert.Equal(13, parenthesisConstruction.Variants.Count);
        Assert.Equal(new MathGlyphVariant(1864, 942), parenthesisConstruction.Variants[0]);
        Assert.Equal(new MathGlyphVariant(2606, 3768), parenthesisConstruction.Variants[^1]);
        Assert.NotNull(parenthesisConstruction.Assembly);
        Assert.Equal(
            [
                new MathGlyphPart(1959, 0, 627, 1069, false),
                new MathGlyphPart(1961, 1069, 1069, 1069, true),
                new MathGlyphPart(1960, 627, 0, 1069, false)
            ],
            parenthesisConstruction.Assembly.Parts);

        // Assert: U+221A has 6 variants and a 3-part assembly
        Assert.Equal(3196, radical);
        Assert.NotNull(radicalConstruction);
        Assert.Equal(6, radicalConstruction.Variants.Count);
        Assert.Equal(new MathGlyphVariant(3219, 2867), radicalConstruction.Variants[^1]);
        Assert.NotNull(radicalConstruction.Assembly);
        Assert.Equal(
            [
                new MathGlyphPart(3200, 0, 1333, 1538, false),
                new MathGlyphPart(3223, 1070, 1070, 1070, true),
                new MathGlyphPart(3222, 998, 0, 1069, false)
            ],
            radicalConstruction.Assembly.Parts);

        // Assert: the integral's assembly carries its italic correction
        Assert.Equal(90, table.GetVerticalConstruction(font.GetGlyphIndex(0x222B))!.Assembly!.ItalicsCorrection);
    }

    /// <summary>
    ///     Proves that the real horizontal constructions are read: the right arrow stretches with
    ///     a two-part assembly and no variants, and the left-right arrow has no construction.
    /// </summary>
    [Fact]
    public void TrueTypeFont_RealNotoSansMath_ReadsHorizontalConstruction()
    {
        // Arrange
        var font = LoadNotoSansMath();
        var table = RequireMathTable(font);

        // Act
        var rightArrow = table.GetHorizontalConstruction(font.GetGlyphIndex(0x2192));
        var leftRightArrow = table.GetHorizontalConstruction(font.GetGlyphIndex(0x2194));

        // Assert
        Assert.NotNull(rightArrow);
        Assert.Empty(rightArrow.Variants);
        Assert.Equal(
            [new MathGlyphPart(4656, 0, 777, 658, true), new MathGlyphPart(4646, 500, 0, 910, false)],
            rightArrow.Assembly!.Parts);
        Assert.Null(leftRightArrow);
    }

    /// <summary>
    ///     Proves that the real glyph information is read: an italic correction, a top-accent
    ///     attachment, extended shapes, and the absence of cut-in kerns (the font has no
    ///     MathKernInfo), as confirmed with <c>fonttools</c>.
    /// </summary>
    [Fact]
    public void TrueTypeFont_RealNotoSansMath_ReadsItalicCorrectionAndTopAccent()
    {
        // Arrange
        var font = LoadNotoSansMath();
        var table = RequireMathTable(font);
        var italicF = font.GetGlyphIndex(0x1D453);
        var a = font.GetGlyphIndex('a');

        // Act
        var hasItalic = table.TryGetItalicsCorrection(italicF, out var italic);
        var hasAccent = table.TryGetTopAccentAttachment(a, out var accent);

        // Assert
        Assert.Equal(1036, italicF);
        Assert.True(hasItalic);
        Assert.Equal(145, italic);
        Assert.Equal(82, a);
        Assert.True(hasAccent);
        Assert.Equal(281, accent);
        Assert.True(table.IsExtendedShape(font.GetGlyphIndex(0x222B)));
        Assert.False(table.IsExtendedShape(a));
        Assert.False(table.TryGetMathKern(italicF, MathKernCorner.TopRight, out _));
    }

    /// <summary>
    ///     Proves that a real text font without a <c>MATH</c> table reports none.
    /// </summary>
    [Fact]
    public void TrueTypeFont_RealOpenSans_HasNoMathTable()
    {
        // Act
        var found = TrueTypeFont.Load(FontPath).TryGetMathTable(out var table);

        // Assert
        Assert.False(found);
        Assert.Null(table);
    }

    /// <summary>
    ///     Extracts <paramref name="glyphIndex"/>'s real outline from <paramref name="font"/>,
    ///     confirms its metrics are sane for a real font, transforms it from font design-unit
    ///     space into a small pixel-space canvas, fills it via <see cref="PathFiller"/>, and
    ///     asserts that real visible ink was painted within the glyph's own bounding box while the
    ///     canvas's far corners remain fully transparent background.
    /// </summary>
    private static void AssertGlyphRendersAsVisibleInk(TrueTypeFont font, int glyphIndex)
    {
        // Act: extract the glyph's real outline (font design units, y-axis up), query its
        // advance width, and confirm a self-kerning lookup does not throw
        var outline = font.GetGlyphOutline(glyphIndex);
        var advance = font.GetAdvanceWidth(glyphIndex);
        var kerning = font.GetKerning(glyphIndex, glyphIndex);

        // Assert: the outline is real contour data, and metrics are sane for a real font
        Assert.NotEmpty(outline.Subpaths);
        Assert.True(advance > 0, "A real glyph must have a positive advance width.");
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

                    case PathCommandType.CubicBezierTo:
                        builder.CubicBezierTo(Map(command.Control1), Map(command.Control2), Map(command.EndPoint));
                        break;

                    case PathCommandType.Close:
                        builder.Close();
                        break;

                    default:
                        // TrueTypeFont glyph outlines only ever contain LineTo,
                        // QuadraticBezierTo, CubicBezierTo, and Close commands (TrueType glyf
                        // outlines never emit CubicBezierTo; CFF outlines never emit
                        // QuadraticBezierTo) - matching GlyfLocaReader's own AppendTransformed
                        // helper, extended for CFF's cubic segments.
                        throw new InvalidOperationException("Unexpected path command in a glyph outline.");
                }
            }
        }

        return builder.Build();
    }
}
