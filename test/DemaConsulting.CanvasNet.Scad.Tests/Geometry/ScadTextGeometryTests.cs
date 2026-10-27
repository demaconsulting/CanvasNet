// cspell:ignore notdef Liberation kerned kerning ttb btt rtl ltr halign colour canvasnet AVTLEHI
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using DemaConsulting.CanvasNet.Scad.Tests.Evaluation;
using DemaConsulting.CanvasNet.Tests.TestSupport;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;
using PathCommandType = DemaConsulting.CanvasNet.Geometry.PathCommandType;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of <see cref="ScadTextGeometry"/> and <see cref="ScadFontResolver"/>: the <c>text()</c> module.
///     Every expectation is an analytic property computed in the test from the bundled Liberation Sans font through
///     the public <see cref="TrueTypeFont"/> API (glyph ink bounds, advances, kerning) and the documented size and
///     alignment formulas, never a recorded result. Glyphs with straight outlines only (<c>H</c>, <c>I</c>, <c>E</c>,
///     <c>L</c>, <c>T</c>, <c>A</c>, <c>V</c>) are used where exact bounds are asserted, because their flattened
///     outline is exactly their font outline.
/// </summary>
public class ScadTextGeometryTests
{
    /// <summary>The tolerance of an exact analytic comparison (font units are floats, model units doubles).</summary>
    private const double Tolerance = 1e-4;

    /// <summary>The options that make explicit font requests independent of the host.</summary>
    private static readonly ScadFontResolutionOptions NoSystemFonts = new() { UseSystemFonts = false };

    /// <summary>
    ///     The outcome of building one <c>text()</c> in a test.
    /// </summary>
    /// <param name="Geo">The geometry.</param>
    /// <param name="Warnings">The warnings, in order.</param>
    /// <param name="Unsupported">The unsupported-feature reports, in order.</param>
    private sealed record TextResult(ScadGeo Geo, IReadOnlyList<ScadDiagnostic> Warnings, IReadOnlyList<ScadUnsupportedFeature> Unsupported)
    {
        /// <summary>Gets the bounds of the geometry.</summary>
        public BoundingBox3D Bounds => Geo.Bounds;

        /// <summary>Gets the regions of the result, one per glyph, in drawing order.</summary>
        public IReadOnlyList<PolygonRegion> Regions =>
            Geo.Kind == ScadGeoKind.Empty ? [] : [.. Geo.Shape!.EnumerateLeaves().Select(l => l.Leaf.Region!)];
    }

    /// <summary>
    ///     The ink bounds of a glyph in font design units.
    /// </summary>
    /// <param name="MinX">The left.</param>
    /// <param name="MaxX">The right.</param>
    /// <param name="MinY">The bottom.</param>
    /// <param name="MaxY">The top.</param>
    private readonly record struct Ink(double MinX, double MaxX, double MinY, double MaxY);

    /// <summary>
    ///     Validates that an empty string, and a string with only spaces, build nothing and report nothing.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_EmptyString_ReturnsEmptyWithoutWarnings()
    {
        // Arrange / Act
        var empty = Run("text(\"\");");
        var spaces = Run("text(\"   \");");

        // Assert
        Assert.Equal(ScadGeoKind.Empty, empty.Geo.Kind);
        Assert.Equal(ScadGeoKind.Empty, spaces.Geo.Kind);
        Assert.Empty(empty.Warnings);
        Assert.Empty(empty.Unsupported);
        Assert.Empty(spaces.Warnings);
        Assert.Empty(spaces.Unsupported);
    }

    /// <summary>
    ///     Validates that text without a <c>font</c> is the bundled Liberation Sans Regular whatever the host has,
    ///     and that naming it explicitly gives the same outline.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_DefaultFontUsesBundledLiberationSansWhenSystemFontsDisabled()
    {
        // Arrange: the analytic ink of H in the bundled font at size 10
        var ink = InkOf('H');
        var scale = 10.0 / (ink.MaxY - ink.MinY);

        // Act: no font with system fonts on and off, and the explicit family with system fonts off
        var withSystem = Run("text(\"H\");");
        var withoutSystem = Run("text(\"H\");", NoSystemFonts);
        var named = Run("text(\"H\", font = \"Liberation Sans\");", NoSystemFonts);

        // Assert: an H is exactly size tall and as wide as its ink, flush with the origin
        Assert.Empty(withSystem.Warnings);
        AssertBox(withSystem.Bounds, 0, (ink.MaxX - ink.MinX) * scale, 0, 10);
        AssertSameVertices(withSystem, withoutSystem);
        AssertSameVertices(withSystem, named);
        Assert.Empty(named.Warnings);
    }

    /// <summary>
    ///     Validates that <c>size</c> is the height of the font's <c>H</c> in model units, for the selected font.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_SizeUsesSelectedFontCapHeight()
    {
        // Arrange: the serif font has different proportions than the sans font
        var serif = SystemFontCatalog.LoadBundledFallback(serif: true, fixedPitch: false, bold: false, italic: false);
        var serifInk = InkOf(serif, 'H');

        // Act
        var sans = Run("text(\"H\", size = 25);");
        var serifResult = Run("text(\"H\", size = 25, font = \"Liberation Serif\");", NoSystemFonts);
        var small = Run("text(\"H\", size = 0.5);");

        // Assert: both are exactly the requested height, with the width of their own font
        AssertBox(sans.Bounds, 0, WidthOf('H', 25), 0, 25);
        var serifScale = 25.0 / (serifInk.MaxY - serifInk.MinY);
        AssertBox(serifResult.Bounds, 0, (serifInk.MaxX - serifInk.MinX) * serifScale, 0, 25);
        Assert.NotEqual(sans.Bounds.Max.X, serifResult.Bounds.Max.X, 3);
        AssertBox(small.Bounds, 0, WidthOf('H', 0.5), 0, 0.5);
    }

    /// <summary>
    ///     Validates that the font argument is parsed as a family with an optional style and that the requested
    ///     style selects the matching bundled face.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_FontSpecParsesFamilyAndBoldItalicStyle()
    {
        // Arrange / Act: the parse of the documented forms
        var plain = ScadFontResolver.ParseSpec("Liberation Sans");
        var bold = ScadFontResolver.ParseSpec("Liberation Sans:style=Bold");
        var both = ScadFontResolver.ParseSpec(" Liberation Serif : STYLE = bold ITALIC ");
        var oblique = ScadFontResolver.ParseSpec("Liberation Mono:size=12:style=Regular,Oblique");
        var unknownKey = ScadFontResolver.ParseSpec("Liberation Mono:weight=Bold");
        var none = ScadFontResolver.ParseSpec(string.Empty);

        // Assert
        Assert.Equal(new ScadFontSpec("Liberation Sans", false, false), plain);
        Assert.Equal(new ScadFontSpec("Liberation Sans", true, false), bold);
        Assert.Equal(new ScadFontSpec("Liberation Serif", true, true), both);
        Assert.Equal(new ScadFontSpec("Liberation Mono", false, true), oblique);
        Assert.Equal(new ScadFontSpec("Liberation Mono", false, false), unknownKey);
        Assert.Equal(new ScadFontSpec(string.Empty, false, false), none);

        // And the style reaches the outline: the bold bold-italic faces are wider than the regular H
        var boldFont = SystemFontCatalog.LoadBundledFallback(false, false, bold: true, italic: false);
        var boldInk = InkOf(boldFont, 'H');
        var boldResult = Run("text(\"H\", font = \"Liberation Sans:style=Bold\");", NoSystemFonts);
        var scale = 10.0 / (boldInk.MaxY - boldInk.MinY);
        AssertBox(boldResult.Bounds, 0, (boldInk.MaxX - boldInk.MinX) * scale, 0, 10);
        Assert.Empty(boldResult.Warnings);
        Assert.NotEqual(WidthOf('H', 10), boldResult.Bounds.Max.X, 3);
    }

    /// <summary>
    ///     Validates that an unknown family falls back to the bundled sans font with one located warning, that the
    ///     generic names resolve to the bundled families silently, and that an additional directory is searched first.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_MissingFontFallsBackToBundledWithWarning()
    {
        // Arrange: a directory with a copy of a bundled font, to prove directories are searched
        var directory = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "canvasnet-test-scad-fonts-" + Guid.NewGuid())).FullName;
        try
        {
            var assembly = typeof(SystemFontCatalog).Assembly;
            using (var source = assembly.GetManifestResourceStream("DemaConsulting.CanvasNet.Fonts.BundledFonts.LiberationSerif-Bold.ttf")!)
            using (var target = File.Create(Path.Join(directory, "SerifBold.ttf")))
            {
                source.CopyTo(target);
            }

            // Act
            var missing = Run("\n  text(\"H\", font = \"No Such Family\");", NoSystemFonts);
            var generic = Run("text(\"H\", font = \"serif\");", NoSystemFonts);
            var fromDirectory = new ScadFontResolver(new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [directory] })
                .Resolve(new ScadFontSpec("liberation serif", true, false));
            var absent = new ScadFontResolver(new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [directory] })
                .Resolve(new ScadFontSpec("Liberation Serif", false, false));

            // Assert: the fallback is the sans outline, with one warning naming the font at the instance
            var warning = Assert.Single(missing.Warnings);
            Assert.Equal(new ScadSourceLocation(null, 2, 3), warning.Location);
            Assert.Contains("No Such Family", warning.Message, StringComparison.Ordinal);
            AssertBox(missing.Bounds, 0, WidthOf('H', 10), 0, 10);
            Assert.Empty(generic.Warnings);
            var serifInk = InkOf(SystemFontCatalog.LoadBundledFallback(true, false, false, false), 'H');
            AssertBox(generic.Bounds, 0, (serifInk.MaxX - serifInk.MinX) * 10.0 / (serifInk.MaxY - serifInk.MinY), 0, 10);

            // The additional directory won for the family it holds (closest style, found by case-insensitive name)
            Assert.Equal(ScadFontSource.AdditionalDirectory, fromDirectory.Source);
            Assert.Null(fromDirectory.Warning);
            Assert.True(fromDirectory.Font.IsBold);

            // A different style of a family that only has one other face still resolves (closest style) from the directory
            Assert.Equal(ScadFontSource.AdditionalDirectory, absent.Source);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that an additional directory is searched recursively, that the face is chosen by exact family
    ///     (ignoring case) and the closest bold and italic style, and that the choice is the same on every run.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_FontResolver_AdditionalDirectory_MatchesFamilyAndClosestStyle()
    {
        // Arrange: a bold and an italic serif face (the italic one in a subdirectory, with an upper case extension),
        // and a face of another family
        var directory = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "canvasnet-test-scad-styles-" + Guid.NewGuid())).FullName;
        try
        {
            CopyBundledFont("LiberationSerif-Bold.ttf", Path.Join(directory, "b.ttf"));
            CopyBundledFont("LiberationSerif-Italic.ttf", Path.Join(directory, "sub", "a.TTF"));
            CopyBundledFont("LiberationMono-Regular.ttf", Path.Join(directory, "mono.ttf"));
            var options = new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [directory] };

            // Act
            var bold = new ScadFontResolver(options).Resolve(new ScadFontSpec("LIBERATION SERIF", true, false));
            var italic = new ScadFontResolver(options).Resolve(new ScadFontSpec("Liberation Serif", false, true));
            var mono = new ScadFontResolver(options).Resolve(new ScadFontSpec("Liberation Mono", false, false));
            var tie = new ScadFontResolver(options).Resolve(new ScadFontSpec("Liberation Serif", true, true));
            var again = new ScadFontResolver(options).Resolve(new ScadFontSpec("Liberation Serif", true, true));

            // Assert: the exact family and style, from the directory, silently
            Assert.All([bold, italic, mono, tie], r => Assert.Equal(ScadFontSource.AdditionalDirectory, r.Source));
            Assert.All([bold, italic, mono, tie], r => Assert.Null(r.Warning));
            Assert.True(bold.Font.IsBold);
            Assert.False(bold.Font.IsItalic);
            Assert.True(italic.Font.IsItalic);
            Assert.False(italic.Font.IsBold);
            Assert.True(mono.Font.IsFixedPitch);

            // A style that no face has exactly takes the first of the closest faces in ordinal path order
            // (the path of b.ttf sorts before the path of sub/a.TTF)
            Assert.True(tie.Font.IsBold);
            Assert.Equal(tie.Font.IsBold, again.Font.IsBold);
            Assert.Equal(tie.Font.IsItalic, again.Font.IsItalic);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that the directories are searched in the order given, so the first directory that holds the
    ///     family wins when the styles are equally close.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_FontResolver_AdditionalDirectories_AreSearchedInOrder()
    {
        // Arrange: one family spread over two directories
        var root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "canvasnet-test-scad-order-" + Guid.NewGuid())).FullName;
        try
        {
            var first = Path.Join(root, "z-first");
            var second = Path.Join(root, "a-second");
            CopyBundledFont("LiberationSerif-Bold.ttf", Path.Join(first, "f.ttf"));
            CopyBundledFont("LiberationSerif-Italic.ttf", Path.Join(second, "f.ttf"));

            // Act: bold italic is one flag away from both faces
            var firstWins = new ScadFontResolver(new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [first, second] })
                .Resolve(new ScadFontSpec("Liberation Serif", true, true));
            var secondWins = new ScadFontResolver(new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [second, first] })
                .Resolve(new ScadFontSpec("Liberation Serif", true, true));

            // Assert
            Assert.True(firstWins.Font.IsBold);
            Assert.True(secondWins.Font.IsItalic);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that a missing directory, a file that is not a font, a damaged font, and a file with another
    ///     extension are skipped without failing, and that the family of a skipped file is then not found.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_FontResolver_AdditionalDirectory_SkipsMissingAndUnparseableFiles()
    {
        // Arrange: a good serif face next to garbage files, and a missing directory listed first
        var directory = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "canvasnet-test-scad-skip-" + Guid.NewGuid())).FullName;
        try
        {
            var missing = Path.Join(directory, "missing");
            CopyBundledFont("LiberationSerif-Bold.ttf", Path.Join(directory, "good.ttf"));
            File.WriteAllText(Path.Join(directory, "garbage.ttf"), "this is not a font");
            File.WriteAllBytes(Path.Join(directory, "empty.otf"), []);
            CopyBundledFont("LiberationSans-Regular.ttf", Path.Join(directory, "ignored.txt"));
            var options = new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [missing, directory] };

            // Act
            var good = new ScadFontResolver(options).Resolve(new ScadFontSpec("Liberation Serif", true, false));
            var damaged = new ScadFontResolver(options).Resolve(new ScadFontSpec("garbage", false, false));
            var wrongExtension = new ScadFontResolver(options).Resolve(new ScadFontSpec("Liberation Sans", false, false));
            var onlyMissing = new ScadFontResolver(new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [missing] })
                .Resolve(new ScadFontSpec("Nonexistent Family", false, false));

            // Assert: the good face is found, the damaged ones are not offered, nothing throws
            Assert.Equal(ScadFontSource.AdditionalDirectory, good.Source);
            Assert.True(good.Font.IsBold);
            Assert.Equal(ScadFontSource.Bundled, damaged.Source);
            Assert.Contains("garbage", damaged.Warning, StringComparison.Ordinal);
            Assert.Equal(ScadFontSource.Bundled, wrongExtension.Source);
            Assert.Null(wrongExtension.Warning);
            Assert.Equal(ScadFontSource.Bundled, onlyMissing.Source);
            Assert.Contains("Nonexistent Family", onlyMissing.Warning, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    ///     Copies one of the bundled fonts of the core package to a file, creating its directory.
    /// </summary>
    /// <param name="bundledName">The bundled file name, for example <c>LiberationSerif-Bold.ttf</c>.</param>
    /// <param name="target">The path of the copy.</param>
    private static void CopyBundledFont(string bundledName, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using var source = typeof(SystemFontCatalog).Assembly.GetManifestResourceStream("DemaConsulting.CanvasNet.Fonts.BundledFonts." + bundledName)!;
        using var file = File.Create(target);
        source.CopyTo(file);
    }

    /// <summary>
    ///     Validates that kerning moves the pen by the font's pair adjustment (not scaled by spacing) and that
    ///     spacing multiplies the advance only, with analytic placement of the second glyph.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_KerningAndSpacing_AffectMeasuredPlacement()
    {
        // Arrange: find a pair of straight-outline letters that the font kerns
        var pair = FindKernedPair();
        var (first, second) = pair;
        var scale = 10.0 / HeightOfH();
        var inkFirst = InkOf(first);
        var inkSecond = InkOf(second);
        var kerning = Font.GetKerning(Font.GetGlyphIndex(first), Font.GetGlyphIndex(second));
        Assert.NotEqual(0, kerning);

        // Act
        var normal = Run($"text(\"{first}{second}\");");
        var wide = Run($"text(\"{first}{second}\", spacing = 2);");
        var none = Run($"text(\"{first}{second}\", spacing = 0);");

        // Assert: the second glyph starts at advance * spacing + kerning; the whole is shifted so the ink starts at 0
        foreach (var (result, spacing) in new[] { (normal, 1.0), (wide, 2.0), (none, 0.0) })
        {
            var origin = ((Font.GetAdvanceWidth(Font.GetGlyphIndex(first)) * spacing) + kerning) * scale;
            var minX = Math.Min(inkFirst.MinX * scale, origin + (inkSecond.MinX * scale));
            var maxX = Math.Max(inkFirst.MaxX * scale, origin + (inkSecond.MaxX * scale));
            AssertBox(result.Bounds, 0, maxX - minX, 0, 10);
            Assert.Equal(2, result.Regions.Count);
        }

        Assert.Empty(none.Warnings);
    }

    /// <summary>
    ///     Validates that <c>halign</c> anchors the measured ink extents (not the advance box): the left edge,
    ///     the middle or the right edge of the ink sits at X = 0.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_HorizontalAlignment_AnchorsMeasuredInkBounds()
    {
        // Arrange / Act
        var left = Run("text(\"HI\");");
        var explicitLeft = Run("text(\"HI\", halign = \"left\");");
        var center = Run("text(\"HI\", halign = \"center\");");
        var right = Run("text(\"HI\", halign = \"RIGHT\");");

        // Assert: the width is the same, the anchor differs
        var width = left.Bounds.Max.X;
        Assert.Equal(0.0, left.Bounds.Min.X, Tolerance);
        AssertBox(explicitLeft.Bounds, 0, width, 0, 10);
        AssertBox(center.Bounds, -width / 2.0, width / 2.0, 0, 10);
        AssertBox(right.Bounds, -width, 0, 0, 10);
        Assert.Empty(right.Warnings);
    }

    /// <summary>
    ///     Validates that <c>valign</c> anchors the measured ink extents: the baseline stays, the top, the middle or
    ///     the bottom of the ink sits at Y = 0 (the descender of <c>g</c> makes the difference visible).
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_VerticalAlignment_AnchorsMeasuredInkBounds()
    {
        // Arrange / Act
        var baseline = Run("text(\"Hg\");");
        var top = Run("text(\"Hg\", valign = \"top\");");
        var center = Run("text(\"Hg\", valign = \"center\");");
        var bottom = Run("text(\"Hg\", valign = \"bottom\");");
        var explicitBaseline = Run("text(\"Hg\", valign = \"baseline\");");

        // Assert: the descender hangs below the baseline; every other anchor is the same ink moved
        var minY = baseline.Bounds.Min.Y;
        var maxY = baseline.Bounds.Max.Y;
        Assert.True(minY < -1.0);
        Assert.Equal(10.0, maxY, Tolerance);
        Assert.Equal(minY, explicitBaseline.Bounds.Min.Y, Tolerance);
        Assert.Equal(0.0, top.Bounds.Max.Y, Tolerance);
        Assert.Equal(minY - maxY, top.Bounds.Min.Y, Tolerance);
        Assert.Equal((maxY - minY) / 2.0, center.Bounds.Max.Y, Tolerance);
        Assert.Equal(-(maxY - minY) / 2.0, center.Bounds.Min.Y, Tolerance);
        Assert.Equal(0.0, bottom.Bounds.Min.Y, Tolerance);
        Assert.Equal(maxY - minY, bottom.Bounds.Max.Y, Tolerance);
    }

    /// <summary>
    ///     Validates that <c>rtl</c> lays the reversed sequence out left to right without any shaping: the glyphs
    ///     of "HI" appear in the order I, H with the kerning of that pair.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_RtlReversesGlyphOrderWithoutShaping()
    {
        // Arrange
        var scale = 10.0 / HeightOfH();
        var inkH = InkOf('H');
        var inkI = InkOf('I');
        var glyphI = Font.GetGlyphIndex('I');
        var glyphH = Font.GetGlyphIndex('H');

        // Act
        var rtl = Run("text(\"HI\", direction = \"rtl\");");
        var ltrOfReversed = Run("text(\"IH\");");

        // Assert: the layout is that of "IH" left to right (I first, then H after advance(I) and the I-H kerning)
        var originH = ((Font.GetAdvanceWidth(glyphI) * 1.0) + Font.GetKerning(glyphI, glyphH)) * scale;
        var minX = Math.Min(inkI.MinX * scale, originH + (inkH.MinX * scale));
        var maxX = Math.Max(inkI.MaxX * scale, originH + (inkH.MaxX * scale));
        AssertBox(rtl.Bounds, 0, maxX - minX, 0, 10);
        AssertSameVertices(rtl, ltrOfReversed);
    }

    /// <summary>
    ///     Validates that <c>ttb</c> and <c>btt</c> stack upright glyphs along Y by the scaled advance, with no
    ///     kerning, the first glyph on the baseline.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_TtbAndBttStackUprightGlyphs()
    {
        // Arrange: H stands on the baseline; the next glyph is one advance of H lower (ttb) or higher (btt)
        var scale = 10.0 / HeightOfH();
        var step = Font.GetAdvanceWidth(Font.GetGlyphIndex('H')) * scale;
        var inkH = InkOf('H');
        var inkI = InkOf('I');
        var minX = Math.Min(inkH.MinX, inkI.MinX) * scale;
        var maxX = Math.Max(inkH.MaxX, inkI.MaxX) * scale;

        // Act
        var down = Run("text(\"HI\", direction = \"ttb\");");
        var up = Run("text(\"HI\", direction = \"btt\");");

        // Assert: the extents of H at the baseline and I one step below (or above), the X extent left-aligned
        AssertBox(down.Bounds, 0, maxX - minX, -step, 10);
        AssertBox(up.Bounds, 0, maxX - minX, 0, step + (10.0 * (inkI.MaxY - inkI.MinY) / (inkH.MaxY - inkH.MinY)));
        Assert.Equal(2, down.Regions.Count);
    }

    /// <summary>
    ///     Validates that <c>language</c> and <c>script</c> are accepted without a warning and change nothing.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_LanguageAndScript_AreAcceptedAndIgnored()
    {
        // Arrange / Act
        var plain = Run("text(\"Hi\");");
        var tagged = Run("text(\"Hi\", language = \"ar\", script = \"arabic\");");
        var odd = Run("text(\"Hi\", language = 5, script = [1, 2]);");

        // Assert
        Assert.Empty(tagged.Warnings);
        Assert.Empty(odd.Warnings);
        AssertSameVertices(plain, tagged);
        AssertSameVertices(plain, odd);
    }

    /// <summary>
    ///     Validates that a character the font does not map is drawn with the font's <c>.notdef</c> glyph, that
    ///     control characters are skipped silently, and that a non-string value is converted like <c>str()</c>.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_MissingGlyph_UsesNotdefOrWarnsWhenNotdefIsEmpty()
    {
        // Arrange: a CJK ideograph that Liberation Sans does not map, so it is drawn as .notdef
        Assert.Equal(0, Font.GetGlyphIndex(0x4E2D));
        var notdef = Font.GetGlyphOutline(0);
        Assert.NotEmpty(notdef.Subpaths);

        // Act
        var missing = Run("text(\"\u4E2D\");");
        var withControl = Run("text(\"H\\nI\\tH\");");
        var plain = Run("text(\"HIH\");");
        var number = Run("text(12);");
        var asString = Run("text(\"12\");");

        // Assert: .notdef is drawn silently, the controls vanish without a trace, the number is its digits
        Assert.Empty(missing.Warnings);
        Assert.Single(missing.Regions);
        Assert.NotEmpty(missing.Regions[0].Contours);
        AssertSameVertices(plain, withControl);
        Assert.Empty(withControl.Warnings);
        AssertSameVertices(asString, number);
    }

    /// <summary>
    ///     Validates the contour policy: holes and islands are normalized whatever winding the outline had (the
    ///     <c>O</c> of the font has an outer contour and a clockwise hole), nested contours are valid, and contours
    ///     that cross each other or themselves are rejected with the matching feature identifier.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_GlyphContours_NormalizeHolesAndRejectOverlaps()
    {
        // Arrange: nested squares drawn with the same winding, overlapping solids, a hole that crosses its solid and a bow tie
        Vector2D[] outer = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];
        Vector2D[] inner = [new(3, 3), new(7, 3), new(7, 7), new(3, 7)];
        Vector2D[] overlapA = [new(0, 0), new(6, 0), new(6, 6), new(0, 6)];
        Vector2D[] overlapB = [new(3, 3), new(9, 3), new(9, 9), new(3, 9)];
        Vector2D[] crossingHole = [new(8, 2), new(8, 8), new(12, 8), new(12, 2)];
        Vector2D[] bowTie = [new(0, 0), new(4, 4), new(4, 0), new(0, 4)];

        // Act
        var nested = ScadTextGeometry.TryMakeRegions([outer, inner], out var nestedFeature);
        var overlapping = ScadTextGeometry.TryMakeRegions([overlapA, overlapB], out var overlapFeature);
        var holeCrossing = ScadTextGeometry.TryMakeRegions([outer, crossingHole], out var holeFeature);
        var crossing = ScadTextGeometry.TryMakeRegions([outer, bowTie], out var crossingFeature);
        var glyph = Run("text(\"O\");");
        var ring = Run("text(\"\u00C5\");");

        // Assert: the hole is clockwise and its area is subtracted
        var region = Assert.Single(nested!);
        Assert.Null(nestedFeature);
        Assert.Equal(100.0 - 16.0, region.Area, 1e-9);
        Assert.True(SignedArea(region.Contours[0]) > 0.0);
        Assert.True(SignedArea(region.Contours[1]) < 0.0);

        // Overlapping solids are the union of two regions; a hole that crosses its solid and a bow tie are rejected, told apart
        Assert.Equal(2, overlapping!.Count);
        Assert.Null(overlapFeature);
        Assert.Null(holeCrossing);
        Assert.Equal(ScadFeatureIds.TextOverlappingContours, holeFeature);
        Assert.Null(crossing);
        Assert.Equal(ScadFeatureIds.TextInvalidOutline, crossingFeature);

        // The real letter O: one solid outline and one hole, counter-clockwise and clockwise
        var o = Assert.Single(glyph.Regions);
        Assert.Equal(2, o.Contours.Count);
        Assert.True(SignedArea(o.Contours[0]) > 0.0);
        Assert.True(SignedArea(o.Contours[1]) < 0.0);
        Assert.Equal(SignedArea(o.Contours[0]) + SignedArea(o.Contours[1]), o.Area, 1e-6);
        Assert.Empty(glyph.Unsupported);

        // The composite A with a ring has overlapping solids: it is built as a union, not rejected
        Assert.True(ring.Regions.Count > 1);
        Assert.Empty(ring.Unsupported);
        Assert.Empty(ring.Warnings);
    }

    /// <summary>
    ///     Validates that every printable character of Basic Latin and Latin-1 in all twelve bundled faces builds a
    ///     valid region, so the policy that rejects an overlapping glyph never discards ordinary text.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_AllBundledLatinGlyphs_BuildValidRegions()
    {
        // Arrange: every printable character of 0x20..0x7E and 0xA0..0xFF
        var characters = string.Concat(Enumerable.Range(0x20, 0x5F).Concat(Enumerable.Range(0xA1, 0x5F)).Select(c => (char)c));
        var families = new[] { "Liberation Sans", "Liberation Serif", "Liberation Mono" };
        var styles = new[] { "Regular", "Bold", "Italic", "Bold Italic" };
        var failures = new List<string>();

        // Act
        foreach (var family in families)
        {
            foreach (var style in styles)
            {
                foreach (var character in characters)
                {
                    var literal = character is '"' or '\\' ? "\\" + character : character.ToString();
                    var result = Run($"text(\"{literal}\", font = \"{family}:style={style}\");", NoSystemFonts);
                    if (result.Unsupported.Count > 0 || result.Warnings.Count > 0)
                    {
                        failures.Add($"{family}:{style} U+{(int)character:X4} {string.Join(';', result.Unsupported.Select(u => u.Feature))}{string.Join(';', result.Warnings.Select(w => w.Message))}");
                    }
                }
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    ///     Validates that every invalid argument is one located warning that yields nothing, and that an unknown
    ///     argument only warns while the text is still built.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_InvalidArguments_WarnAndReturnEmpty()
    {
        // Arrange
        string[] invalid =
        [
            "text();",
            "text(undef);",
            "text(\"H\", size = 0);",
            "text(\"H\", size = -3);",
            "text(\"H\", size = \"big\");",
            "text(\"H\", spacing = -1);",
            "text(\"H\", halign = \"middle\");",
            "text(\"H\", halign = 1);",
            "text(\"H\", valign = \"under\");",
            "text(\"H\", direction = \"up\");",
            "text(\"H\", font = 5);",
            "text(function(a) a);",
            "text(\"H\", size = 1e300);",
        ];

        // Act / Assert: one warning, nothing built, nothing unsupported
        foreach (var source in invalid)
        {
            var result = Run(source);
            Assert.Equal(ScadGeoKind.Empty, result.Geo.Kind);
            Assert.Single(result.Warnings);
            Assert.Empty(result.Unsupported);
        }

        // An unknown argument is a warning, an extra child too, and the text is still built
        var unknown = Run("text(\"H\", colour = 3) cube(1);");
        Assert.Equal(2, unknown.Warnings.Count);
        Assert.Equal(ScadGeoKind.Flat, unknown.Geo.Kind);
    }

    /// <summary>
    ///     Validates that the flat result is charged like any other 2D shape: nothing until its slab is built, then
    ///     the exact slab triangles, and a budget that cannot hold the slab is refused with the instance's location
    ///     before the regions are built.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_FlatShapeBudgetUsesExistingSlabAccounting()
    {
        // Arrange / Act: the full build of a top-level text charges exactly the triangles of its meshes
        var built = Build("text(\"Hello\");");
        var tooSmall = Assert.Throws<ScadEvaluationException>(() => Build("\n text(\"Hello\");", Settings(maxTriangles: 20)));

        // Assert
        Assert.Equal(5, built.Meshes.Count);
        Assert.Equal(built.MeshTriangles, built.TrianglesCharged);
        Assert.Contains("Triangle limit", tooSmall.Message, StringComparison.Ordinal);
        Assert.Equal(new ScadSourceLocation(null, 2, 2), tooSmall.Location);
    }

    /// <summary>
    ///     Validates that cancellation is polled before any glyph is flattened: a canceled token stops the build and
    ///     nothing is reported.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_Cancellation_IsPolledBeforeFlattening()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var output = ScadEvaluatorTestSupport.Run("text(\"Hello World\");", new ScadEvaluationSettings(), null, sink);
        var context = new ScadGeometryContext(Settings(cancellation: cts.Token), new ScadReporter(sink, false));

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => ScadTextGeometry.Build(output.Roots[0], context));
        Assert.Empty(sink.UnsupportedFeatures);
        Assert.Empty(sink.Diagnostics);
    }

    /// <summary>
    ///     Validates that the flattening of the reference glyph (the <c>H</c> that <c>size</c> is measured against)
    ///     polls cancellation: a canceled context stops the reference-height measurement itself.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_Cancellation_IsPolledDuringReferenceHeightFlattening()
    {
        // Arrange: a canceled context, and a font whose H is flattened for the measurement
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var context = new ScadGeometryContext(Settings(cancellation: cts.Token), new ScadReporter(sink, false));

        // Act / Assert: the measurement is stopped, and the same measurement completes with a live context
        Assert.Throws<OperationCanceledException>(() => ScadTextGeometry.ReferenceHeight(Font, context));
        var live = new ScadGeometryContext(Settings(cancellation: TestContext.Current.CancellationToken), new ScadReporter(sink, false));
        var height = ScadTextGeometry.ReferenceHeight(Font, live);
        var ink = InkOf('H');
        Assert.Equal(ink.MaxY - ink.MinY, height, Tolerance);
    }

    /// <summary>
    ///     Validates that the reference glyph <c>H</c> is drawn with straight lines only in all twelve bundled faces,
    ///     so the fine-tolerance flattening of the reference measurement and the fragment-derived flattening of the
    ///     output give the same cap height and an <c>H</c> is exactly <c>size</c> tall at any fragment setting.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_ReferenceHeight_BundledFaces_HaveStraightLinedH()
    {
        // Arrange / Act / Assert: every bundled face
        foreach (var serif in new[] { false, true })
        {
            foreach (var fixedPitch in new[] { false, true })
            {
                foreach (var bold in new[] { false, true })
                {
                    foreach (var italic in new[] { false, true })
                    {
                        var font = SystemFontCatalog.LoadBundledFallback(serif, fixedPitch, bold, italic);
                        var outline = font.GetGlyphOutline(font.GetGlyphIndex('H'));
                        Assert.NotEmpty(outline.Subpaths);
                        Assert.All(
                            outline.Subpaths.SelectMany(s => s.Commands),
                            command => Assert.True(command.Type is PathCommandType.LineTo or PathCommandType.Close));
                    }
                }
            }
        }

        // A coarse $fn does not change the height of an H either
        var coarse = Run("$fn = 5; text(\"H\", size = 14.09);");
        AssertBox(coarse.Bounds, coarse.Bounds.Min.X, coarse.Bounds.Max.X, 0, 14.09);
    }

    /// <summary>
    ///     Validates that the flattening measures the deviation of the control points from the chord segment, not
    ///     from the infinite line, so a cubic whose control points lie on the chord line beyond its end point (an
    ///     overshoot that comes back) is subdivided and keeps its overshoot.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_FlattenCubic_CollinearOvershoot_IsKept()
    {
        // Arrange: (0,0) -> (2,0) -> (2,0) -> (1,0) reaches x = 1.472 at t = 0.8 before it returns to x = 1
        Vector2D a = new(0, 0);
        Vector2D b = new(2, 0);
        Vector2D d = new(1, 0);
        var output = new List<Vector2D>();

        // Act
        var distance = ScadTextGeometry.ControlDistance(a, b, b, d);
        ScadTextGeometry.FlattenCubic(a, b, b, d, 0.05, output);

        // Assert: the control points are one unit from the chord segment, and the polyline reaches past the end point
        Assert.Equal(1.0, distance, 1e-12);
        Assert.Equal(d, output[^1]);
        Assert.True(output.Max(p => p.X) > 1.4, "the overshoot of the curve was lost");
        Assert.All(output, p => Assert.Equal(0.0, p.Y, 1e-12));
    }

    /// <summary>
    ///     Validates the documented limit of the composite-glyph split: solids that only touch (zero-area contact) are
    ///     accepted as separate regions, exactly like touching leaves of a union, and the contact is left to the
    ///     union to report; solids that cross are accepted too, and contours that cross a hole are still rejected.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_TryMakeRegions_TouchingSolids_AreSplitAndLeftToTheUnion()
    {
        // Arrange: two squares that share one corner, and two that share an edge segment
        Vector2D[] first = [new(0, 0), new(5, 0), new(5, 5), new(0, 5)];
        Vector2D[] corner = [new(5, 5), new(10, 5), new(10, 10), new(5, 10)];
        Vector2D[] edge = [new(5, 1), new(10, 1), new(10, 4), new(5, 4)];

        // Act
        var touchingCorner = ScadTextGeometry.TryMakeRegions([first, corner], out var cornerFeature);
        var touchingEdge = ScadTextGeometry.TryMakeRegions([first, edge], out var edgeFeature);

        // Assert: each solid is its own region and nothing is reported by this unit
        Assert.Equal(2, touchingCorner!.Count);
        Assert.Null(cornerFeature);
        Assert.Equal(2, touchingEdge!.Count);
        Assert.Null(edgeFeature);
    }

    /// <summary>
    ///     Validates that a mapped glyph whose only subpath has no area (a collinear contour) is reported as an
    ///     invalid outline and omits the whole text, whereas a glyph with a real outline in the same font builds.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_DegenerateMappedGlyph_ReportsInvalidOutline()
    {
        // Arrange: a font with a usable H (a 600 x 700 box), a mapped D with a collinear contour and an empty space
        using var directory = new ScadTempDirectory();
        var family = "Synthetic Degenerate";
        WriteSyntheticFont(directory.Combine("synthetic.ttf"), family);
        var options = new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [directory.Path] };
        var resolved = new ScadFontResolver(options).Resolve(new ScadFontSpec(family, false, false));
        Assert.Equal(ScadFontSource.AdditionalDirectory, resolved.Source);
        Assert.NotEqual(0, resolved.Font.GetGlyphIndex('H'));
        Assert.NotEqual(0, resolved.Font.GetGlyphIndex('D'));

        // Act
        var usable = Run($"text(\"HH\", font = \"{family}\");", options);
        var degenerate = Run($"\n  text(\"HD\", font = \"{family}\");", options);

        // Assert: the box glyphs build, the degenerate glyph is reported and omits everything
        Assert.Empty(usable.Warnings);
        Assert.Empty(usable.Unsupported);
        Assert.Equal(2, usable.Regions.Count);
        AssertBox(usable.Bounds, 0, 10.0 + (600.0 / 700.0 * 10.0), 0, 10);
        Assert.Equal(ScadGeoKind.Empty, degenerate.Geo.Kind);
        var feature = Assert.Single(degenerate.Unsupported);
        Assert.Equal(ScadFeatureIds.TextInvalidOutline, feature.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), feature.Location);
        Assert.Empty(degenerate.Warnings);
    }

    /// <summary>
    ///     Validates that a directory that is itself a link (a reparse point) is not scanned, while a real
    ///     directory listed beside it is.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_FontResolver_AdditionalDirectory_ReparsePointRootIsIgnored()
    {
        // Arrange: a real directory with a font, and a link to it (skipped where links cannot be created)
        using var directory = new ScadTempDirectory();
        CopyBundledFont("LiberationSerif-Bold.ttf", directory.Combine("real/serif.ttf"));
        var link = directory.Combine("link");
        if (!ScadResolverTestSupport.TryCreateDirectoryLink(link, directory.Combine("real"), out var reason))
        {
            Assert.Skip("Directory links cannot be created in this environment: " + reason);
        }

        var spec = new ScadFontSpec("Liberation Serif", true, false);

        // Act
        var throughLink = new ScadFontResolver(new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [link] }).Resolve(spec);
        var throughBoth = new ScadFontResolver(new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [link, directory.Combine("real")] }).Resolve(spec);

        // Assert: the link contributes nothing (the bundled family answers), the real directory is found
        Assert.Equal(ScadFontSource.Bundled, throughLink.Source);
        Assert.Equal(ScadFontSource.AdditionalDirectory, throughBoth.Source);
    }

    /// <summary>
    ///     Validates that a font file that is a symbolic link (a reparse point) is not read, even though it is named
    ///     like a font and points at a real one, while a real font file in a directory listed beside it is.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_FontResolver_AdditionalDirectory_ReparsePointFileIsIgnored()
    {
        // Arrange: a real font outside the scanned folder, and a link to it inside (skipped where links cannot be created)
        using var directory = new ScadTempDirectory();
        CopyBundledFont("LiberationSerif-Bold.ttf", directory.Combine("outside/serif.ttf"));
        Directory.CreateDirectory(directory.Combine("scanned"));
        var link = directory.Combine("scanned/linked.ttf");
        if (!ScadResolverTestSupport.TryCreateFileLink(link, directory.Combine("outside/serif.ttf"), out var reason))
        {
            Assert.Skip("File links cannot be created in this environment: " + reason);
        }

        var spec = new ScadFontSpec("Liberation Serif", true, false);

        // Act
        var throughLink = new ScadFontResolver(new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [directory.Combine("scanned")] }).Resolve(spec);
        var throughBoth = new ScadFontResolver(new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [directory.Combine("scanned"), directory.Combine("outside")] }).Resolve(spec);

        // Assert: the linked file contributes nothing (the bundled family answers), the real file is found
        Assert.Equal(ScadFontSource.Bundled, throughLink.Source);
        Assert.Equal(ScadFontSource.AdditionalDirectory, throughBoth.Source);
    }

    /// <summary>
    ///     Validates that the scan of the additional directories polls cancellation and stops with an exception
    ///     instead of completing silently.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_FontResolver_AdditionalDirectory_CancellationIsPolled()
    {
        // Arrange: a directory with a font, and a canceled token
        using var directory = new ScadTempDirectory();
        CopyBundledFont("LiberationSerif-Bold.ttf", directory.Combine("serif.ttf"));
        var options = new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [directory.Path] };
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var spec = new ScadFontSpec("Liberation Serif", true, false);

        // Act / Assert: the canceled resolver throws, the same scan with a live token finds the font
        Assert.Throws<OperationCanceledException>(() => new ScadFontResolver(options, cts.Token).Resolve(spec));
        var live = new ScadFontResolver(options, TestContext.Current.CancellationToken).Resolve(spec);
        Assert.Equal(ScadFontSource.AdditionalDirectory, live.Source);
    }

    /// <summary>
    ///     Validates that the scan stops deterministically at its bounds: with one font file allowed only the first
    ///     in ordinal order is offered, and with two directories allowed the second subdirectory is not visited.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_FontResolver_AdditionalDirectory_ScanStopsAtResourceBound()
    {
        // Arrange: two fonts in one directory, and one font in each of two subdirectories
        using var files = new ScadTempDirectory();
        CopyBundledFont("LiberationSerif-Bold.ttf", files.Combine("a.ttf"));
        CopyBundledFont("LiberationSerif-Italic.ttf", files.Combine("b.ttf"));
        using var tree = new ScadTempDirectory();
        CopyBundledFont("LiberationSerif-Bold.ttf", tree.Combine("a/bold.ttf"));
        CopyBundledFont("LiberationSerif-Italic.ttf", tree.Combine("b/italic.ttf"));
        var italic = new ScadFontSpec("Liberation Serif", false, true);
        var filesOptions = new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [files.Path] };
        var treeOptions = new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = [tree.Path] };
        var generous = ScadFontScanLimits.Default;

        // Act
        var unbounded = new ScadFontResolver(filesOptions, default, generous).Resolve(italic);
        var oneFile = new ScadFontResolver(filesOptions, default, generous with { FontFiles = 1 }).Resolve(italic);
        var oneFace = new ScadFontResolver(filesOptions, default, generous with { Faces = 1 }).Resolve(italic);
        var fullTree = new ScadFontResolver(treeOptions, default, generous).Resolve(italic);
        var twoDirectories = new ScadFontResolver(treeOptions, default, generous with { Directories = 2 }).Resolve(italic);
        var fewEntries = new ScadFontResolver(treeOptions, default, generous with { Entries = 1 }).Resolve(italic);

        // Assert: the italic face is found when everything is read, and only the first in ordinal order otherwise
        Assert.True(unbounded.Font.IsItalic);
        Assert.Equal(ScadFontSource.AdditionalDirectory, oneFile.Source);
        Assert.False(oneFile.Font.IsItalic);
        Assert.True(oneFile.Font.IsBold);
        Assert.False(oneFace.Font.IsItalic);
        Assert.True(fullTree.Font.IsItalic);
        Assert.Equal(ScadFontSource.AdditionalDirectory, twoDirectories.Source);
        Assert.False(twoDirectories.Font.IsItalic);

        // With one entry allowed nothing usable is read, so the bundled family answers
        Assert.Equal(ScadFontSource.Bundled, fewEntries.Source);
    }

    /// <summary>
    ///     Validates that malformed, over-long, missing and non-directory paths are skipped without failing, and
    ///     that a valid directory listed after them is still scanned.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_FontResolver_AdditionalDirectory_InvalidPathsAreSkipped()
    {
        // Arrange: bad entries first, then a file that is not a directory, then a real directory with a font
        using var directory = new ScadTempDirectory();
        CopyBundledFont("LiberationSerif-Bold.ttf", directory.Combine("good/serif.ttf"));
        var notADirectory = directory.Write("file.txt", "not a directory");
        string[] paths =
        [
            "bad\0path",
            new string('x', 40_000),
            Path.Join(directory.Path, "missing", "..", "missing"),
            notADirectory,
            directory.Combine("good"),
        ];
        var options = new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = paths };

        // Act
        var resolved = new ScadFontResolver(options).Resolve(new ScadFontSpec("Liberation Serif", true, false));

        // Assert: nothing threw and the valid sibling was found
        Assert.Equal(ScadFontSource.AdditionalDirectory, resolved.Source);
        Assert.True(resolved.Font.IsBold);
    }

    /// <summary>
    ///     Writes a small, clean-room TrueType font to a file: a box <c>H</c> (600 by 700 units, advance 700), a
    ///     mapped <c>D</c> whose only contour is collinear (no area, advance 700) and an unmapped, empty .notdef.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="family">The family name of the font.</param>
    private static void WriteSyntheticFont(string path, string family)
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var box = SyntheticFontBuilder.SimpleGlyph([(0, 0, true), (600, 0, true), (600, 700, true), (0, 700, true)]);
        var line = SyntheticFontBuilder.SimpleGlyph([(0, 0, true), (300, 0, true), (600, 0, true)]);
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(3))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, 3))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 700, 700]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length, box.Length, line.Length], longFormat: false))
            .AddTable("glyf", [.. notdef, .. box, .. line])
            .AddTable("cmap", SyntheticFontBuilder.CmapFormat4(3, 1, [('H', 1), ('D', 2)]))
            .AddTable("name", SyntheticFontBuilder.Name([new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 1, family), new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 2, "Regular")]))
            .Build();
        File.WriteAllBytes(path, data);
    }

    /// <summary>
    ///     Validates that building the same text twice gives exactly the same vertices, curves included.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_RepeatedCallsAreDeterministic()
    {
        // Arrange / Act: letters with curves, a hole and a kerned pair
        var first = Run("text(\"Roboto Quag, AV 8%\", size = 7.5);");
        var second = Run("text(\"Roboto Quag, AV 8%\", size = 7.5);");

        // Assert
        Assert.NotEmpty(first.Regions);
        AssertSameVertices(first, second);
    }

    /// <summary>
    ///     Validates that the flat result is consumed like any other 2D shape: a transformed text, a 2D hull of text,
    ///     a difference and both extrusions build closed solids without warnings or unsupported reports.
    /// </summary>
    [Fact]
    public void ScadTextGeometry_Build_TransformsBooleansAndExtrusionsConsumeTheFlatRegion()
    {
        // Arrange / Act
        var transformed = Build("translate([5, 2, 0]) rotate(90) linear_extrude(height = 3) text(\"H\");");
        var hull = Build("linear_extrude(height = 1) hull() text(\"HI\");");
        var difference = Build("linear_extrude(height = 2) difference() { text(\"HI\"); translate([1, 1]) square(2); }");
        var rotated = Build("rotate_extrude($fn = 16) translate([10, 0]) text(\"I\", size = 5);");
        var colored = Build("color(\"red\") linear_extrude(height = 1) text(\"Hi\");");
        var flat = Build("text(\"Hi\");");

        // Assert
        foreach (var result in new[] { transformed, hull, difference, rotated, colored, flat })
        {
            Assert.Empty(result.Warnings);
            Assert.Empty(result.Features);
            Assert.All(result.Leaves, leaf => Assert.True(leaf.WorldMesh.IsClosed));
        }

        // The extruded H is its glyph area times the height, rotated a quarter turn and moved
        var area = Run("text(\"H\");").Regions[0].Area;
        Assert.Equal(area * 3.0, transformed.WorldMesh.SignedVolume, area * 3.0 * 1e-6);
        AssertBox(transformed.WorldMesh.Bounds, 5 - 10, 5, 2, 2 + (WidthOf('H', 10)), 0, 3);

        // The hull of two glyphs is one convex prism; the difference is a boolean of two extrusions
        Assert.Single(hull.Meshes);
        Assert.Equal(3, difference.Leaves.Count);
        Assert.NotNull(FindCsg(difference.Scene));
        Assert.Single(rotated.Meshes);
        Assert.Equal(2, flat.Meshes.Count);
    }

    /// <summary>
    ///     Gets the bundled Liberation Sans Regular font that the tests measure against.
    /// </summary>
    private static TrueTypeFont Font => SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false, italic: false);

    /// <summary>
    ///     Builds the first top-level instance of a program with the geometry builder of <c>text()</c>.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="fonts">The font resolution options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The geometry and the reports.</returns>
    private static TextResult Run(string source, ScadFontResolutionOptions? fonts = null)
    {
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var output = ScadEvaluatorTestSupport.Run(source, new ScadEvaluationSettings(), null, sink);
        var settings = new ScadGeometrySettings(5_000_000, 0.1, false, TestContext.Current.CancellationToken, FontResolution: fonts);
        var context = new ScadGeometryContext(settings, new ScadReporter(sink, false));
        var geo = ScadTextGeometry.Build(output.Roots[0], context);
        return new TextResult(geo, [.. sink.Diagnostics], [.. sink.UnsupportedFeatures]);
    }

    /// <summary>
    ///     Gets the ink bounds of a glyph of the bundled sans font.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>The ink bounds in font units.</returns>
    private static Ink InkOf(char character) => InkOf(Font, character);

    /// <summary>
    ///     Gets the ink bounds of a glyph of a font, from the control points of its outline (exact for straight outlines).
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="character">The character.</param>
    /// <returns>The ink bounds in font units.</returns>
    private static Ink InkOf(TrueTypeFont font, char character)
    {
        var bounds = font.GetGlyphOutline(font.GetGlyphIndex(character)).GetBounds();
        return new Ink(bounds.Left, bounds.Right, bounds.Top, bounds.Bottom);
    }

    /// <summary>
    ///     Gets the ink height of the reference glyph <c>H</c> in font units.
    /// </summary>
    /// <returns>The height.</returns>
    private static double HeightOfH()
    {
        var ink = InkOf('H');
        return ink.MaxY - ink.MinY;
    }

    /// <summary>
    ///     Gets the ink width of a glyph at a size, the width of the text of that one character left-aligned.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <param name="size">The size.</param>
    /// <returns>The width in model units.</returns>
    private static double WidthOf(char character, double size)
    {
        var ink = InkOf(character);
        return (ink.MaxX - ink.MinX) * size / HeightOfH();
    }

    /// <summary>
    ///     Finds a pair of uppercase straight-outline letters that the bundled font kerns.
    /// </summary>
    /// <returns>The pair.</returns>
    private static (char First, char Second) FindKernedPair()
    {
        const string Letters = "AVTLEHI";
        foreach (var a in Letters)
        {
            foreach (var b in Letters)
            {
                if (Font.GetKerning(Font.GetGlyphIndex(a), Font.GetGlyphIndex(b)) != 0)
                {
                    return (a, b);
                }
            }
        }

        Assert.Fail("The bundled font has no kerning pair among the straight-outline capitals.");
        return default;
    }

    /// <summary>
    ///     Asserts the X and Y extents of a box.
    /// </summary>
    /// <param name="bounds">The bounds.</param>
    /// <param name="minX">The minimum X.</param>
    /// <param name="maxX">The maximum X.</param>
    /// <param name="minY">The minimum Y.</param>
    /// <param name="maxY">The maximum Y.</param>
    private static void AssertBox(BoundingBox3D bounds, double minX, double maxX, double minY, double maxY)
    {
        Assert.Equal(minX, bounds.Min.X, Tolerance);
        Assert.Equal(maxX, bounds.Max.X, Tolerance);
        Assert.Equal(minY, bounds.Min.Y, Tolerance);
        Assert.Equal(maxY, bounds.Max.Y, Tolerance);
    }

    /// <summary>
    ///     Asserts the extents of a box in all three axes.
    /// </summary>
    /// <param name="bounds">The bounds.</param>
    /// <param name="minX">The minimum X.</param>
    /// <param name="maxX">The maximum X.</param>
    /// <param name="minY">The minimum Y.</param>
    /// <param name="maxY">The maximum Y.</param>
    /// <param name="minZ">The minimum Z.</param>
    /// <param name="maxZ">The maximum Z.</param>
    private static void AssertBox(BoundingBox3D bounds, double minX, double maxX, double minY, double maxY, double minZ, double maxZ)
    {
        AssertBox(bounds, minX, maxX, minY, maxY);
        Assert.Equal(minZ, bounds.Min.Z, Tolerance);
        Assert.Equal(maxZ, bounds.Max.Z, Tolerance);
    }

    /// <summary>
    ///     Asserts that two results have exactly the same regions, vertex for vertex.
    /// </summary>
    /// <param name="expected">The expected result.</param>
    /// <param name="actual">The actual result.</param>
    private static void AssertSameVertices(TextResult expected, TextResult actual)
    {
        Assert.Equal(expected.Regions.Count, actual.Regions.Count);
        for (var i = 0; i < expected.Regions.Count; i++)
        {
            Assert.True(expected.Regions[i].Vertices.SequenceEqual(actual.Regions[i].Vertices), $"Region {i} differs.");
        }
    }

    /// <summary>
    ///     Computes the signed area of a contour (positive for counter-clockwise).
    /// </summary>
    /// <param name="contour">The contour.</param>
    /// <returns>The signed area.</returns>
    private static double SignedArea(IReadOnlyList<Vector2D> contour)
    {
        var sum = 0.0;
        for (var i = 0; i < contour.Count; i++)
        {
            sum += Vector2D.Cross(contour[i], contour[(i + 1) % contour.Count]);
        }

        return sum / 2.0;
    }

    /// <summary>
    ///     Finds the first boolean node of a scene, walking iteratively.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The node, or <see langword="null"/>.</returns>
    private static CsgNode? FindCsg(Scene scene)
    {
        var stack = new Stack<SceneNode>();
        stack.Push(scene.Root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is CsgNode csg)
            {
                return csg;
            }

            if (node is GroupNode group)
            {
                foreach (var child in group.Children)
                {
                    stack.Push(child);
                }
            }
        }

        return null;
    }
}
