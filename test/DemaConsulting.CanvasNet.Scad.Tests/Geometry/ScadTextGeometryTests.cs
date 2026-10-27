// cspell:ignore notdef Liberation kerned kerning ttb btt rtl ltr
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using DemaConsulting.CanvasNet.Scad.Tests.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

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
