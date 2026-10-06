using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore srgbclr schemeclr lummod lumoff pptx gsLst prstDash lgDash sysDash sysDot dashDot hlink folhlink Srgb patt

/// <summary>
///     Unit-level tests for the Phase 1c DrawingML paint resolvers (<see cref="PptxDocument.ResolveFill"/>,
///     <see cref="PptxDocument.ResolveColor"/>, <see cref="PptxDocument.ResolveGradientFill"/>,
///     <see cref="PptxDocument.ResolveLineStyle"/>, <see cref="PptxDocument.ResolveStrokeOutline"/>).
///     Every resolver under test is a plain static method operating on directly-constructed
///     <see cref="XElement"/> fragments and a directly-constructed <see cref="PptxTheme"/>, so
///     these tests do not need a full in-memory <c>.pptx</c> package (reserved for
///     <see cref="PptxSystemIntegrationTests"/>'s end-to-end scenarios).
/// </summary>
public class PptxPaintTests
{
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>Builds an arbitrary, fully-populated test <see cref="PptxTheme"/>, with each color-scheme slot a distinct, recognizable value.</summary>
    private static PptxTheme BuildTestTheme(
        IReadOnlyList<XElement>? fillStyleList = null, IReadOnlyList<XElement>? lnStyleList = null) =>
        new(
            new PptxColorScheme(
                Dark1: new Rgba32(0x10, 0x10, 0x10, 255),
                Light1: new Rgba32(0xF0, 0xF0, 0xF0, 255),
                Dark2: new Rgba32(0x20, 0x20, 0x20, 255),
                Light2: new Rgba32(0xE0, 0xE0, 0xE0, 255),
                Accent1: new Rgba32(0x11, 0x22, 0x33, 255),
                Accent2: new Rgba32(0x44, 0x55, 0x66, 255),
                Accent3: new Rgba32(0x77, 0x88, 0x99, 255),
                Accent4: new Rgba32(0xAA, 0xBB, 0xCC, 255),
                Accent5: new Rgba32(0x01, 0x02, 0x03, 255),
                Accent6: new Rgba32(0x04, 0x05, 0x06, 255),
                Hyperlink: new Rgba32(0x07, 0x08, 0x09, 255),
                FollowedHyperlink: new Rgba32(0x0A, 0x0B, 0x0C, 255)),
            new PptxFontScheme(
                new PptxFontCollection("MajorLatin", "MajorEA", "MajorCS"),
                new PptxFontCollection("MinorLatin", "MinorEA", "MinorCS")),
            FillStyleList: fillStyleList,
            LnStyleList: lnStyleList);

    /// <summary>Srgb Clr.</summary>
    private static XElement SrgbClr(string hex, params XElement[] transforms) =>
        new(A + "srgbClr", new XAttribute("val", hex), transforms);

    /// <summary>Scheme Clr.</summary>
    private static XElement SchemeClr(string val, params XElement[] transforms) =>
        new(A + "schemeClr", new XAttribute("val", val), transforms);

    /// <summary>Prst Clr.</summary>
    private static XElement PrstClr(string val, params XElement[] transforms) =>
        new(A + "prstClr", new XAttribute("val", val), transforms);

    /// <summary>Transform.</summary>
    private static XElement Transform(string name, int val100000ths) =>
        new(A + name, new XAttribute("val", val100000ths));

    // --- ResolveFill: fill-kind dispatch --------------------------------------------------------

    /// <summary>Resolve Fill - Null Parent - Returns No Fill.</summary>
    [Fact]
    public void ResolveFill_NullParent_ReturnsNoFill()
    {
        var paint = PptxDocument.ResolveFill(null, BuildTestTheme(), 100, 100);

        Assert.Same(PptxNoFill.Instance, paint);
    }

    /// <summary>Resolve Fill - Explicit No Fill - Returns No Fill.</summary>
    [Fact]
    public void ResolveFill_ExplicitNoFill_ReturnsNoFill()
    {
        var spPr = new XElement(A + "spPr", new XElement(A + "noFill"));

        var paint = PptxDocument.ResolveFill(spPr, BuildTestTheme(), 100, 100);

        Assert.Same(PptxNoFill.Instance, paint);
    }

    /// <summary>Resolve Fill - No Recognized Fill Child - Returns No Fill.</summary>
    [Fact]
    public void ResolveFill_NoRecognizedFillChild_ReturnsNoFill()
    {
        var spPr = new XElement(A + "spPr");

        var paint = PptxDocument.ResolveFill(spPr, BuildTestTheme(), 100, 100);

        Assert.Same(PptxNoFill.Instance, paint);
    }

    /// <summary>Resolve Fill - Solid Fill Srgb Clr - Returns Resolved Solid Fill.</summary>
    [Fact]
    public void ResolveFill_SolidFillSrgbClr_ReturnsResolvedSolidFill()
    {
        var spPr = new XElement(A + "spPr", new XElement(A + "solidFill", SrgbClr("112233")));

        var paint = PptxDocument.ResolveFill(spPr, BuildTestTheme(), 100, 100);

        var solid = Assert.IsType<PptxSolidFill>(paint);
        Assert.Equal(new Rgba32(0x11, 0x22, 0x33, 255), solid.Color);
    }

    /// <summary>Resolve Fill - Solid Fill Scheme Clr - Resolves Through Theme.</summary>
    [Fact]
    public void ResolveFill_SolidFillSchemeClr_ResolvesThroughTheme()
    {
        var theme = BuildTestTheme();
        var spPr = new XElement(A + "spPr", new XElement(A + "solidFill", SchemeClr("accent1")));

        var paint = PptxDocument.ResolveFill(spPr, theme, 100, 100);

        var solid = Assert.IsType<PptxSolidFill>(paint);
        Assert.Equal(theme.ColorScheme.Accent1, solid.Color);
    }

    /// <summary>Resolve Fill - Grad Fill Linear - Returns Resolved Gradient Fill.</summary>
    [Fact]
    public void ResolveFill_GradFillLinear_ReturnsResolvedGradientFill()
    {
        var gradFill = new XElement(
            A + "gradFill",
            new XElement(
                A + "gsLst",
                new XElement(A + "gs", new XAttribute("pos", 0), SrgbClr("FF0000")),
                new XElement(A + "gs", new XAttribute("pos", 100000), SrgbClr("0000FF"))),
            new XElement(A + "lin", new XAttribute("ang", 0)));
        var spPr = new XElement(A + "spPr", gradFill);

        var paint = PptxDocument.ResolveFill(spPr, BuildTestTheme(), 200, 100);

        var gradient = Assert.IsType<PptxGradientFill>(paint);
        var linear = Assert.IsType<LinearGradient>(gradient.Gradient);
        Assert.Equal(2, linear.Stops.Count);
    }

    /// <summary>Resolve Fill - Pattern Fill - Throws Pptx Unsupported Feature Exception.</summary>
    /// <summary>
    ///     Resolve Fill - Bare Pattern Fill With No Prst Attribute - Returns No Fill.
    /// </summary>
    /// <remarks>
    ///     A non-conformant, attribute-less <c>&lt;a:pattFill/&gt;</c> (no <c>prst</c> attribute)
    ///     - observed in the project's own real <c>pythonpptx-dml-fill.pptx</c> fixture - is
    ///     treated as "no override", not a named preset; see <see cref="PptxDocument.ResolveFill"/>'s
    ///     own remarks.
    /// </remarks>
    [Fact]
    public void ResolveFill_BarePatternFillWithNoPrstAttribute_ReturnsNoFill()
    {
        var spPr = new XElement(A + "spPr", new XElement(A + "pattFill"));

        var paint = PptxDocument.ResolveFill(spPr, BuildTestTheme(), 100, 100);

        Assert.Same(PptxNoFill.Instance, paint);
    }

    /// <summary>Resolve Fill - Pattern Fill Unsupported Preset - Throws Pptx Unsupported Feature Exception.</summary>
    [Fact]
    public void ResolveFill_PatternFillUnsupportedPreset_ThrowsPptxUnsupportedFeatureException()
    {
        var spPr = new XElement(A + "spPr", new XElement(A + "pattFill", new XAttribute("prst", "zigZag")));

        var ex = Assert.Throws<PptxUnsupportedFeatureException>(() => PptxDocument.ResolveFill(spPr, BuildTestTheme(), 100, 100));
        Assert.Equal("pptx-pattern-fill", ex.Feature);
        Assert.Contains("zigZag", ex.Message);
    }

    /// <summary>Resolve Fill - Pattern Fill Covered Preset - Returns Resolved Pattern Fill.</summary>
    [Fact]
    public void ResolveFill_PatternFillCoveredPreset_ReturnsPptxPatternFill()
    {
        var pattFill = new XElement(
            A + "pattFill",
            new XAttribute("prst", "divot"),
            new XElement(A + "fgClr", SrgbClr("2CB731")),
            new XElement(A + "bgClr", PrstClr("white")));
        var spPr = new XElement(A + "spPr", pattFill);

        var paint = PptxDocument.ResolveFill(spPr, BuildTestTheme(), 100, 100);

        var pattern = Assert.IsType<PptxPatternFill>(paint);
        Assert.Equal(PptxPresetPattern.Divot, pattern.Preset);
        Assert.Equal(new Rgba32(0x2C, 0xB7, 0x31, 255), pattern.Foreground);
        Assert.Equal(new Rgba32(255, 255, 255, 255), pattern.Background);
    }

    /// <summary>Resolve Fill - Pattern Fill Missing Fg Bg Clr - Defaults To Black On White.</summary>
    [Fact]
    public void ResolveFill_PatternFillMissingFgBgClr_DefaultsToBlackOnWhite()
    {
        var spPr = new XElement(A + "spPr", new XElement(A + "pattFill", new XAttribute("prst", "horz")));

        var paint = PptxDocument.ResolveFill(spPr, BuildTestTheme(), 100, 100);

        var pattern = Assert.IsType<PptxPatternFill>(paint);
        Assert.Equal(PptxPresetPattern.Horz, pattern.Preset);
        Assert.Equal(new Rgba32(0, 0, 0, 255), pattern.Foreground);
        Assert.Equal(new Rgba32(255, 255, 255, 255), pattern.Background);
    }

    /// <summary>Resolve Fill - Picture Fill - Throws Pptx Unsupported Feature Exception.</summary>
    [Fact]
    public void ResolveFill_PictureFill_ThrowsPptxUnsupportedFeatureException()
    {
        var spPr = new XElement(A + "spPr", new XElement(A + "blipFill"));

        var ex = Assert.Throws<PptxUnsupportedFeatureException>(() => PptxDocument.ResolveFill(spPr, BuildTestTheme(), 100, 100));
        Assert.Equal("pptx-picture-fill", ex.Feature);
    }

    /// <summary>
    ///     Proves that, once a <c>resolveBlipImage</c>-shaped resolver is supplied (see
    ///     <see cref="PptxDocument.ResolveFill"/>'s own remarks), a minimal
    ///     <c>&lt;a:blipFill&gt;&lt;a:blip r:embed="rId1"/&gt;&lt;a:stretch/&gt;&lt;/a:blipFill&gt;</c>
    ///     no longer throws and instead resolves to a <see cref="PptxImageFill"/> wrapping the
    ///     resolver's own decoded <see cref="Surface"/> - the regression this whole feature exists
    ///     to fix (see <see cref="ResolveFill_PictureFill_ThrowsPptxUnsupportedFeatureException"/>
    ///     for the still-unchanged "no resolver supplied" fallback behavior).
    /// </summary>
    [Fact]
    public void ResolveFill_BlipFillWithResolver_ReturnsPptxImageFill()
    {
        using var image = new Surface(4, 4);
        var blip = new XElement(A + "blip", new XAttribute(R + "embed", "rId1"));
        var spPr = new XElement(
            A + "spPr",
            new XElement(A + "blipFill", blip, new XElement(A + "stretch")));

        var paint = PptxDocument.ResolveFill(
            spPr, BuildTestTheme(), 100, 100, resolveBlipImage: resolvedBlip =>
            {
                Assert.Same(blip, resolvedBlip.Element(A + "blip"));
                return image;
            });

        var imageFill = Assert.IsType<PptxImageFill>(paint);
        Assert.Same(image, imageFill.Image);
    }

    // --- ResolveImageFillTransform: <a:tile>/<a:stretch> transform derivations ------------------

    /// <summary>
    ///     Pins down <see cref="PptxDocument.ResolveImageFillTransform"/>'s own <c>&lt;a:tile&gt;</c>
    ///     derivation with a non-trivial (non-default, non-100%) <c>tx</c>/<c>ty</c>/<c>sx</c>/<c>sy</c>
    ///     combination - the mandatory case for this package's own <c>pythonpptx-dml-fill.pptx</c>
    ///     fixture (see the design document) - independent of that real fixture's own trivial
    ///     (<c>tx="0" ty="0" sx="100000" sy="100000"</c>, i.e. unscaled/untranslated) values.
    /// </summary>
    [Fact]
    public void ResolveImageFillTransform_TileWithNonTrivialOffsetAndScale_AppliesBothToEachAxis()
    {
        using var image = new Surface(10, 20);
        var tile = new XElement(
            A + "tile",
            new XAttribute("tx", "914400"), // 1 inch
            new XAttribute("ty", "457200"), // 0.5 inch
            new XAttribute("sx", "50000"), // 50%
            new XAttribute("sy", "200000")); // 200%
        var blipFill = new XElement(A + "blipFill", tile);

        var transform = PptxDocument.ResolveImageFillTransform(blipFill, image, widthEmu: 1000, heightEmu: 1000);

        // 96-DPI EMU-per-pixel is 9525; sx/sy scale that per-axis, tx/ty translate directly.
        Assert.Equal(0.5f * 9525f, transform.M11, 2);
        Assert.Equal(0f, transform.M12, 2);
        Assert.Equal(0f, transform.M21, 2);
        Assert.Equal(2f * 9525f, transform.M22, 2);
        Assert.Equal(914400f, transform.M31, 2);
        Assert.Equal(457200f, transform.M32, 2);
    }

    /// <summary>
    ///     Proves a <c>&lt;a:stretch&gt;</c>'s nested <c>&lt;a:fillRect&gt;</c> insets crop the
    ///     image before the remaining span is stretched to fill the owning shape, by pinning the
    ///     exact resulting scale/translation for a non-trivial (non-zero, non-degenerate) set of
    ///     edges.
    /// </summary>
    [Fact]
    public void ResolveImageFillTransform_StretchWithFillRectInsets_CropsBeforeStretching()
    {
        using var image = new Surface(100, 50);
        var fillRect = new XElement(
            A + "fillRect", new XAttribute("l", "10000"), new XAttribute("r", "10000")); // 10% each side
        var blipFill = new XElement(A + "blipFill", new XElement(A + "stretch", fillRect));

        var transform = PptxDocument.ResolveImageFillTransform(blipFill, image, widthEmu: 200, heightEmu: 100);

        // hSpan = 1 - 0.1 - 0.1 = 0.8; stretchScaleX = 200 / (0.8 * 100) = 2.5; translateX = -0.1*200/0.8 = -25.
        Assert.Equal(2.5f, transform.M11, 3);
        Assert.Equal(-25f, transform.M31, 3);
        // No top/bottom inset: vSpan = 1, stretchScaleY = 100 / (1 * 50) = 2; translateY = 0.
        Assert.Equal(2f, transform.M22, 3);
        Assert.Equal(0f, transform.M32, 3);
    }

    /// <summary>
    ///     Proves a degenerate <c>&lt;a:fillRect&gt;</c> (left+right edges summing to <c>100%</c>
    ///     or more) falls back to an uncropped full stretch on that axis, rather than producing a
    ///     non-finite or divide-by-zero transform - see <see cref="PptxDocument.ResolveImageFillTransform"/>'s
    ///     own remarks.
    /// </summary>
    [Fact]
    public void ResolveImageFillTransform_DegenerateFillRect_FallsBackToFullStretch()
    {
        using var image = new Surface(100, 50);
        var fillRect = new XElement(
            A + "fillRect", new XAttribute("l", "60000"), new XAttribute("r", "50000")); // sums to 110%
        var blipFill = new XElement(A + "blipFill", new XElement(A + "stretch", fillRect));

        var transform = PptxDocument.ResolveImageFillTransform(blipFill, image, widthEmu: 200, heightEmu: 100);

        // Falls back to left=0, hSpan=1: stretchScaleX = 200 / (1 * 100) = 2; translateX = 0.
        Assert.Equal(2f, transform.M11, 3);
        Assert.Equal(0f, transform.M31, 3);
        Assert.True(float.IsFinite(transform.M11));
        Assert.True(float.IsFinite(transform.M31));
    }

    // --- ResolveColor: base color kinds ----------------------------------------------------------

    /// <summary>Resolve Color - Srgb Clr - Parses Hex Directly.</summary>
    [Fact]
    public void ResolveColor_SrgbClr_ParsesHexDirectly()
    {
        var color = PptxDocument.ResolveColor(SrgbClr("ABCDEF"), BuildTestTheme());

        Assert.Equal(new Rgba32(0xAB, 0xCD, 0xEF, 255), color);
    }

    /// <summary>Resolve Color - Sys Clr - Uses Last Clr Attribute.</summary>
    [Fact]
    public void ResolveColor_SysClr_UsesLastClrAttribute()
    {
        var element = new XElement(A + "sysClr", new XAttribute("val", "windowText"), new XAttribute("lastClr", "123456"));

        var color = PptxDocument.ResolveColor(element, BuildTestTheme());

        Assert.Equal(new Rgba32(0x12, 0x34, 0x56, 255), color);
    }

    /// <summary>Resolve Color - Scheme Clr Ordinary Slots - Resolve To Matching Theme Slot.</summary>
    [Theory]
    [InlineData("dk1")]
    [InlineData("lt1")]
    [InlineData("dk2")]
    [InlineData("lt2")]
    [InlineData("accent1")]
    [InlineData("accent2")]
    [InlineData("accent3")]
    [InlineData("accent4")]
    [InlineData("accent5")]
    [InlineData("accent6")]
    [InlineData("hlink")]
    [InlineData("folHlink")]
    public void ResolveColor_SchemeClrOrdinarySlots_ResolveToMatchingThemeSlot(string slot)
    {
        var theme = BuildTestTheme();
        var expected = slot switch
        {
            "dk1" => theme.ColorScheme.Dark1,
            "lt1" => theme.ColorScheme.Light1,
            "dk2" => theme.ColorScheme.Dark2,
            "lt2" => theme.ColorScheme.Light2,
            "accent1" => theme.ColorScheme.Accent1,
            "accent2" => theme.ColorScheme.Accent2,
            "accent3" => theme.ColorScheme.Accent3,
            "accent4" => theme.ColorScheme.Accent4,
            "accent5" => theme.ColorScheme.Accent5,
            "accent6" => theme.ColorScheme.Accent6,
            "hlink" => theme.ColorScheme.Hyperlink,
            _ => theme.ColorScheme.FollowedHyperlink,
        };

        var color = PptxDocument.ResolveColor(SchemeClr(slot), theme);

        Assert.Equal(expected, color);
    }

    /// <summary>Resolve Color - Scheme Clr Background Text Aliases - Map To Expected Slot.</summary>
    [Theory]
    [InlineData("bg1")]
    [InlineData("tx1")]
    [InlineData("bg2")]
    [InlineData("tx2")]
    public void ResolveColor_SchemeClrBackgroundTextAliases_MapToExpectedSlot(string alias)
    {
        var theme = BuildTestTheme();
        var expected = alias switch
        {
            "bg1" => theme.ColorScheme.Light1,
            "tx1" => theme.ColorScheme.Dark1,
            "bg2" => theme.ColorScheme.Light2,
            _ => theme.ColorScheme.Dark2,
        };

        var color = PptxDocument.ResolveColor(SchemeClr(alias), theme);

        Assert.Equal(expected, color);
    }

    /// <summary>Resolve Color - Scheme Clr Unrecognized Slot - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveColor_SchemeClrUnrecognizedSlot_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveColor(SchemeClr("notARealSlot"), BuildTestTheme()));
    }

    // --- ResolveSchemeColor / ResolveColor: <p:clrMap>/<p:clrMapOvr> indirection ----------------

    /// <summary>Resolve Scheme Color - Default Color Map - Tx1 Resolves To Dark1.</summary>
    [Fact]
    public void ResolveSchemeColor_DefaultColorMap_Tx1ResolvesToDark1()
    {
        var theme = BuildTestTheme();

        var color = PptxDocument.ResolveSchemeColor("tx1", theme.ColorScheme, PptxColorMap.Default);

        Assert.Equal(theme.ColorScheme.Dark1, color);
    }

    /// <summary>Resolve Scheme Color - Non Identity Color Map - Tx1 Resolves To Overridden Slot.</summary>
    [Fact]
    public void ResolveSchemeColor_NonIdentityColorMap_Tx1ResolvesToOverriddenSlot()
    {
        var theme = BuildTestTheme();
        var colorMap = new PptxColorMap(Bg1: "dk1", Tx1: "lt1", Bg2: "dk2", Tx2: "lt2");

        var color = PptxDocument.ResolveSchemeColor("tx1", theme.ColorScheme, colorMap);

        Assert.Equal(theme.ColorScheme.Light1, color);
    }

    /// <summary>Resolve Color - Scheme Clr With Non Identity Color Map - Applies Indirection Before Transforms.</summary>
    [Fact]
    public void ResolveColor_SchemeClrWithNonIdentityColorMap_AppliesIndirectionBeforeTransforms()
    {
        var theme = BuildTestTheme();
        var colorMap = new PptxColorMap(Bg1: "dk1", Tx1: "lt1", Bg2: "dk2", Tx2: "lt2");

        var color = PptxDocument.ResolveColor(SchemeClr("tx1", Transform("shade", 50000)), theme, colorMap: colorMap);

        var expectedBase = theme.ColorScheme.Light1;
        var expectedShaded = PptxDocument.ResolveColor(SrgbClr($"{expectedBase.R:X2}{expectedBase.G:X2}{expectedBase.B:X2}", Transform("shade", 50000)), theme);

        Assert.Equal(expectedShaded, color);
    }

    /// <summary>Resolve Color - Unsupported Color Kind - Throws Pptx Unsupported Feature Exception.</summary>
    [Fact]
    public void ResolveColor_UnsupportedColorKind_ThrowsPptxUnsupportedFeatureException()
    {
        var element = new XElement(A + "hslClr", new XAttribute("hue", 0), new XAttribute("sat", "0%"), new XAttribute("lum", "0%"));

        var ex = Assert.Throws<PptxUnsupportedFeatureException>(() => PptxDocument.ResolveColor(element, BuildTestTheme()));
        Assert.Equal("pptx-color-kind", ex.Feature);
    }

    /// <summary>Resolve Color - Prst Clr White - Returns White.</summary>
    [Fact]
    public void ResolveColor_PrstClrWhite_ReturnsWhite()
    {
        var color = PptxDocument.ResolveColor(PrstClr("white"), BuildTestTheme());

        Assert.Equal(new Rgba32(255, 255, 255, 255), color);
    }

    /// <summary>Resolve Color - Prst Clr Black - Returns Black.</summary>
    [Fact]
    public void ResolveColor_PrstClrBlack_ReturnsBlack()
    {
        var color = PptxDocument.ResolveColor(PrstClr("black"), BuildTestTheme());

        Assert.Equal(new Rgba32(0, 0, 0, 255), color);
    }

    /// <summary>Resolve Color - Prst Clr Unsupported Name - Throws Pptx Unsupported Feature Exception.</summary>
    [Fact]
    public void ResolveColor_PrstClrUnsupportedName_ThrowsPptxUnsupportedFeatureException()
    {
        var ex = Assert.Throws<PptxUnsupportedFeatureException>(() => PptxDocument.ResolveColor(PrstClr("aliceBlue"), BuildTestTheme()));
        Assert.Equal("pptx-color-kind", ex.Feature);
        Assert.Contains("aliceBlue", ex.Message);
    }

    /// <summary>Resolve Color - Prst Clr Missing Val Attribute - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveColor_PrstClrMissingValAttribute_ThrowsInvalidDataException()
    {
        var element = new XElement(A + "prstClr");

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveColor(element, BuildTestTheme()));
    }

    /// <summary>
    ///     Resolve Color - Srgb Clr Eight Digit Value - Throws Invalid Data Exception (rather than
    ///     silently accepting an <c>AARRGGBB</c> value and misinterpreting its leading byte as
    ///     alpha, since OOXML's <c>srgbClr/@val</c> is always exactly six hex digits).
    /// </summary>
    [Fact]
    public void ResolveColor_SrgbClrEightDigitValue_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveColor(SrgbClr("80AABBCC"), BuildTestTheme()));
    }

    /// <summary>Resolve Color - Sys Clr Eight Digit Last Clr Value - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveColor_SysClrEightDigitLastClrValue_ThrowsInvalidDataException()
    {
        var element = new XElement(A + "sysClr", new XAttribute("val", "windowText"), new XAttribute("lastClr", "80123456"));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveColor(element, BuildTestTheme()));
    }

    // --- ResolveColor: color-transform chain -----------------------------------------------------

    /// <summary>Resolve Color - Alpha - Sets Alpha Channel.</summary>
    [Fact]
    public void ResolveColor_Alpha_SetsAlphaChannel()
    {
        var color = PptxDocument.ResolveColor(SrgbClr("112233", Transform("alpha", 50000)), BuildTestTheme());

        Assert.Equal(0x11, color.R);
        Assert.Equal(0x22, color.G);
        Assert.Equal(0x33, color.B);
        Assert.Equal((byte)127, color.A);
    }

    /// <summary>Resolve Color - Shade - Darkens Proportionally.</summary>
    [Fact]
    public void ResolveColor_Shade_DarkensProportionally()
    {
        var color = PptxDocument.ResolveColor(SrgbClr("FF0000", Transform("shade", 50000)), BuildTestTheme());

        Assert.Equal((byte)127, color.R);
        Assert.Equal((byte)0, color.G);
        Assert.Equal((byte)0, color.B);
    }

    /// <summary>Resolve Color - Tint - Lightens Proportionally.</summary>
    [Fact]
    public void ResolveColor_Tint_LightensProportionally()
    {
        var color = PptxDocument.ResolveColor(SrgbClr("000000", Transform("tint", 50000)), BuildTestTheme());

        // c' = c*0.5 + 255*0.5 = 127.5 (rounds within clamp)
        Assert.InRange(color.R, (byte)127, (byte)128);
        Assert.InRange(color.G, (byte)127, (byte)128);
        Assert.InRange(color.B, (byte)127, (byte)128);
    }

    /// <summary>Resolve Color - Lum Mod - Decreases Luminance.</summary>
    [Fact]
    public void ResolveColor_LumMod_DecreasesLuminance()
    {
        var baseColor = PptxDocument.ResolveColor(SrgbClr("808080"), BuildTestTheme());
        var color = PptxDocument.ResolveColor(SrgbClr("808080", Transform("lumMod", 50000)), BuildTestTheme());

        Assert.True(color.R < baseColor.R, $"Expected darker luminance, base={baseColor.R}, result={color.R}");
    }

    /// <summary>Resolve Color - Lum Off - Increases Luminance.</summary>
    [Fact]
    public void ResolveColor_LumOff_IncreasesLuminance()
    {
        var baseColor = PptxDocument.ResolveColor(SrgbClr("202020"), BuildTestTheme());
        var color = PptxDocument.ResolveColor(SrgbClr("202020", Transform("lumOff", 50000)), BuildTestTheme());

        Assert.True(color.R > baseColor.R, $"Expected brighter luminance, base={baseColor.R}, result={color.R}");
    }

    /// <summary>Resolve Color - Combined Lum Mod Lum Off Shade Tint Alpha - Applies Fixed Pipeline Order.</summary>
    [Fact]
    public void ResolveColor_CombinedLumModLumOffShadeTintAlpha_AppliesFixedPipelineOrder()
    {
        // Mainly proves this does not throw and alpha is applied last/exactly, regardless of the
        // combined effect of the earlier RGB-affecting transforms.
        var color = PptxDocument.ResolveColor(
            SrgbClr("336699", Transform("lumMod", 80000), Transform("lumOff", 10000), Transform("shade", 90000), Transform("tint", 90000), Transform("alpha", 60000)),
            BuildTestTheme());

        Assert.Equal((byte)153, color.A);
    }

    // --- ResolveGradientFill: direction/stop resolution ------------------------------------------

    /// <summary>Resolve Gradient Fill - Linear Zero Degrees - Points Along Positive X.</summary>
    [Fact]
    public void ResolveGradientFill_LinearZeroDegrees_PointsAlongPositiveX()
    {
        var gradFill = new XElement(
            A + "gradFill",
            new XElement(
                A + "gsLst",
                new XElement(A + "gs", new XAttribute("pos", 0), SrgbClr("FF0000")),
                new XElement(A + "gs", new XAttribute("pos", 100000), SrgbClr("0000FF"))),
            new XElement(A + "lin", new XAttribute("ang", 0)));

        var result = PptxDocument.ResolveGradientFill(gradFill, BuildTestTheme(), 200, 100);

        var linear = Assert.IsType<LinearGradient>(result.Gradient);
        Assert.True(linear.End.X > linear.Start.X, "Expected the gradient to run left-to-right at 0 degrees.");
        Assert.InRange(MathF.Abs(linear.End.Y - linear.Start.Y), 0f, 0.01f);
    }

    /// <summary>Resolve Gradient Fill - Missing Lin - Throws Pptx Unsupported Feature Exception.</summary>
    [Fact]
    public void ResolveGradientFill_MissingLin_ThrowsPptxUnsupportedFeatureException()
    {
        var gradFill = new XElement(
            A + "gradFill",
            new XElement(A + "gsLst", new XElement(A + "gs", new XAttribute("pos", 0), SrgbClr("FF0000"))),
            new XElement(A + "path"));

        var ex = Assert.Throws<PptxUnsupportedFeatureException>(
            () => PptxDocument.ResolveGradientFill(gradFill, BuildTestTheme(), 200, 100));
        Assert.Equal("pptx-gradient-path", ex.Feature);
    }

    /// <summary>Resolve Gradient Fill - Missing Gs Lst - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveGradientFill_MissingGsLst_ThrowsInvalidDataException()
    {
        var gradFill = new XElement(A + "gradFill", new XElement(A + "lin", new XAttribute("ang", 0)));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveGradientFill(gradFill, BuildTestTheme(), 200, 100));
    }

    /// <summary>
    ///     Resolve Gradient Fill - Non Numeric Gs Pos - Throws Invalid Data Exception (not the
    ///     raw conversion-failure exception an explicit <c>(float?)</c> cast would otherwise let
    ///     escape).
    /// </summary>
    [Fact]
    public void ResolveGradientFill_NonNumericGsPos_ThrowsInvalidDataException()
    {
        var gradFill = new XElement(
            A + "gradFill",
            new XElement(A + "gsLst", new XElement(A + "gs", new XAttribute("pos", "not-a-number"), SrgbClr("FF0000"))),
            new XElement(A + "lin", new XAttribute("ang", 0)));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveGradientFill(gradFill, BuildTestTheme(), 200, 100));
    }

    /// <summary>
    ///     Resolve Gradient Fill - Non Finite Gs Pos - Throws Invalid Data Exception (a
    ///     <c>NaN</c>/<c>Infinity</c>/<c>-Infinity</c> <c>pos</c> value survives
    ///     <see cref="float.TryParse(string, System.Globalization.NumberStyles, System.IFormatProvider?, out float)"/>
    ///     but must still be rejected, since <see cref="System.Math.Clamp(float, float, float)"/>
    ///     would otherwise propagate it unchanged).
    /// </summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void ResolveGradientFill_NonFiniteGsPos_ThrowsInvalidDataException(string nonFiniteValue)
    {
        var gradFill = new XElement(
            A + "gradFill",
            new XElement(A + "gsLst", new XElement(A + "gs", new XAttribute("pos", nonFiniteValue), SrgbClr("FF0000"))),
            new XElement(A + "lin", new XAttribute("ang", 0)));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveGradientFill(gradFill, BuildTestTheme(), 200, 100));
    }

    /// <summary>
    ///     Proves an <c>&lt;a:lin&gt;</c>'s non-numeric <c>ang</c> attribute is rejected with
    ///     <see cref="InvalidDataException"/> rather than letting the explicit <c>(int?)</c>
    ///     cast's raw <see cref="FormatException"/> propagate uncaught.
    /// </summary>
    [Fact]
    public void ResolveGradientFill_NonNumericLinAng_ThrowsInvalidDataException()
    {
        var gradFill = new XElement(
            A + "gradFill",
            new XElement(A + "gsLst", new XElement(A + "gs", new XAttribute("pos", 0), SrgbClr("FF0000"))),
            new XElement(A + "lin", new XAttribute("ang", "not-a-number")));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveGradientFill(gradFill, BuildTestTheme(), 200, 100));
    }

    /// <summary>
    ///     Proves an <c>&lt;a:lin&gt;</c>'s overflowing (out-of-<see cref="int"/>-range)
    ///     <c>ang</c> attribute is rejected with <see cref="InvalidDataException"/> rather than
    ///     letting the explicit <c>(int?)</c> cast's raw <see cref="OverflowException"/>
    ///     propagate uncaught.
    /// </summary>
    [Fact]
    public void ResolveGradientFill_OverflowingLinAng_ThrowsInvalidDataException()
    {
        var gradFill = new XElement(
            A + "gradFill",
            new XElement(A + "gsLst", new XElement(A + "gs", new XAttribute("pos", 0), SrgbClr("FF0000"))),
            new XElement(A + "lin", new XAttribute("ang", "99999999999")));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveGradientFill(gradFill, BuildTestTheme(), 200, 100));
    }

    // --- ResolveLineStyle --------------------------------------------------------------------------

    /// <summary>Resolve Line Style - Null Element - Returns Null.</summary>
    [Fact]
    public void ResolveLineStyle_NullElement_ReturnsNull()
    {
        var lineStyle = PptxDocument.ResolveLineStyle(null, BuildTestTheme());

        Assert.Null(lineStyle);
    }

    /// <summary>Resolve Line Style - Zero Width - Returns Null.</summary>
    [Fact]
    public void ResolveLineStyle_ZeroWidth_ReturnsNull()
    {
        var ln = new XElement(A + "ln", new XAttribute("w", 0), new XElement(A + "solidFill", SrgbClr("112233")));

        Assert.Null(PptxDocument.ResolveLineStyle(ln, BuildTestTheme()));
    }

    /// <summary>Resolve Line Style - Explicit Zero Width - Returns Null (regression guard).</summary>
    [Fact]
    public void ResolveLineStyle_ExplicitZeroWidth_ReturnsNull()
    {
        var ln = new XElement(A + "ln", new XAttribute("w", 0), new XElement(A + "solidFill", SrgbClr("FF0000")));

        Assert.Null(PptxDocument.ResolveLineStyle(ln, BuildTestTheme()));
    }

    /// <summary>Resolve Line Style - Explicit Negative Width - Returns Null (regression guard).</summary>
    [Fact]
    public void ResolveLineStyle_ExplicitNegativeWidth_ReturnsNull()
    {
        var ln = new XElement(A + "ln", new XAttribute("w", -100), new XElement(A + "solidFill", SrgbClr("FF0000")));

        Assert.Null(PptxDocument.ResolveLineStyle(ln, BuildTestTheme()));
    }

    /// <summary>
    ///     Resolve Line Style - Missing Width Attribute - Resolves Default Width. A genuinely
    ///     absent <c>w</c> attribute (as opposed to an explicit <c>w="0"</c>) must resolve to
    ///     PowerPoint's own observed default stroke width (9525 EMU / 0.75pt) rather than "no
    ///     stroke", matching real-world documents whose <c>&lt;a:ln&gt;</c> declares only a color.
    /// </summary>
    [Fact]
    public void ResolveLineStyle_MissingWidthAttribute_ResolvesDefaultWidth()
    {
        var ln = new XElement(A + "ln", new XElement(A + "solidFill", SrgbClr("FF0000")));

        var lineStyle = PptxDocument.ResolveLineStyle(ln, BuildTestTheme());

        Assert.NotNull(lineStyle);
        Assert.Equal(9525f, lineStyle.WidthEmu);
        var solid = Assert.IsType<PptxSolidFill>(lineStyle.Paint);
        Assert.Equal(new Rgba32(0xFF, 0x00, 0x00, 255), solid.Color);
    }

    /// <summary>Resolve Line Style - No Fill Line - Returns Null.</summary>
    [Fact]
    public void ResolveLineStyle_NoFillLine_ReturnsNull()
    {
        var ln = new XElement(A + "ln", new XAttribute("w", 12700), new XElement(A + "noFill"));

        Assert.Null(PptxDocument.ResolveLineStyle(ln, BuildTestTheme()));
    }

    /// <summary>Resolve Line Style - Width And Solid Fill - Resolves Width And Color.</summary>
    [Fact]
    public void ResolveLineStyle_WidthAndSolidFill_ResolvesWidthAndColor()
    {
        var ln = new XElement(A + "ln", new XAttribute("w", 25400), new XElement(A + "solidFill", SrgbClr("00FF00")));

        var lineStyle = PptxDocument.ResolveLineStyle(ln, BuildTestTheme());

        Assert.NotNull(lineStyle);
        Assert.Equal(25400f, lineStyle.WidthEmu);
        var solid = Assert.IsType<PptxSolidFill>(lineStyle.Paint);
        Assert.Equal(new Rgba32(0x00, 0xFF, 0x00, 255), solid.Color);
        Assert.Null(lineStyle.DashArray);
    }

    /// <summary>
    ///     Resolve Line Style - Pattern Fill Covered Preset - Returns Line Style With Pptx Pattern
    ///     Fill. Proves <see cref="PptxDocument.ResolveLineStyle"/> resolves an
    ///     <c>&lt;a:ln&gt;&lt;a:pattFill&gt;...&lt;/a:pattFill&gt;&lt;/a:ln&gt;</c> (a line/stroke
    ///     fill, as legitimately permitted by ECMA-376's <c>CT_LineProperties</c>) to a
    ///     <see cref="PptxLineStyle"/> wrapping a <see cref="PptxPatternFill"/> with the expected
    ///     preset/colors - the same shared <see cref="PptxDocument.ResolveFill"/> resolver used for
    ///     shape fills (see <see cref="ResolveFill_PatternFillCoveredPreset_ReturnsPptxPatternFill"/>)
    ///     is reached for a line's own fill-definition child too, so pattern fill is equally
    ///     resolvable (and, after the companion render-level transform-composition fix, equally
    ///     renderable) in a line/stroke context - it is not "not applicable" there.
    /// </summary>
    [Fact]
    public void ResolveLineStyle_PatternFillCoveredPreset_ReturnsLineStyleWithPptxPatternFill()
    {
        var pattFill = new XElement(
            A + "pattFill",
            new XAttribute("prst", "horz"),
            new XElement(A + "fgClr", SrgbClr("2CB731")),
            new XElement(A + "bgClr", SrgbClr("C0504D")));
        var ln = new XElement(A + "ln", new XAttribute("w", 28575), pattFill);

        var lineStyle = PptxDocument.ResolveLineStyle(ln, BuildTestTheme());

        Assert.NotNull(lineStyle);
        Assert.Equal(28575f, lineStyle.WidthEmu);
        var pattern = Assert.IsType<PptxPatternFill>(lineStyle.Paint);
        Assert.Equal(PptxPresetPattern.Horz, pattern.Preset);
        Assert.Equal(new Rgba32(0x2C, 0xB7, 0x31, 255), pattern.Foreground);
        Assert.Equal(new Rgba32(0xC0, 0x50, 0x4D, 255), pattern.Background);
    }

    /// <summary>Proves a non-numeric <c>w</c> attribute throws <see cref="InvalidDataException"/> rather than letting a raw <see cref="FormatException"/> escape uncaught.</summary>
    [Fact]
    public void ResolveLineStyle_NonNumericWidth_ThrowsInvalidDataException()
    {
        var ln = new XElement(A + "ln", new XAttribute("w", "not-a-number"), new XElement(A + "solidFill", SrgbClr("FF0000")));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveLineStyle(ln, BuildTestTheme()));
    }

    /// <summary>Proves a non-finite (<c>NaN</c>/<c>Infinity</c>/<c>-Infinity</c>) <c>w</c> attribute throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void ResolveLineStyle_NonFiniteWidth_ThrowsInvalidDataException(string nonFiniteValue)
    {
        var ln = new XElement(A + "ln", new XAttribute("w", nonFiniteValue), new XElement(A + "solidFill", SrgbClr("FF0000")));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveLineStyle(ln, BuildTestTheme()));
    }

    /// <summary>Resolve Line Style - Dash Presets - Produce Non Null Dash Array.</summary>
    [Theory]
    [InlineData("dash")]
    [InlineData("dashDot")]
    [InlineData("dot")]
    [InlineData("lgDash")]
    [InlineData("lgDashDot")]
    [InlineData("sysDash")]
    [InlineData("sysDot")]
    public void ResolveLineStyle_DashPresets_ProduceNonNullDashArray(string presetName)
    {
        var ln = new XElement(
            A + "ln",
            new XAttribute("w", 12700),
            new XElement(A + "solidFill", SrgbClr("000000")),
            new XElement(A + "prstDash", new XAttribute("val", presetName)));

        var lineStyle = PptxDocument.ResolveLineStyle(ln, BuildTestTheme());

        Assert.NotNull(lineStyle);
        Assert.NotNull(lineStyle.DashArray);
        Assert.NotEmpty(lineStyle.DashArray);
    }

    /// <summary>Resolve Line Style - Solid Preset - Produces Null Dash Array.</summary>
    [Fact]
    public void ResolveLineStyle_SolidPreset_ProducesNullDashArray()
    {
        var ln = new XElement(
            A + "ln",
            new XAttribute("w", 12700),
            new XElement(A + "solidFill", SrgbClr("000000")),
            new XElement(A + "prstDash", new XAttribute("val", "solid")));

        var lineStyle = PptxDocument.ResolveLineStyle(ln, BuildTestTheme());

        Assert.NotNull(lineStyle);
        Assert.Null(lineStyle.DashArray);
    }

    // --- ResolveShapeStyleFill: <p:style>/<a:fillRef> shape-style reference (Phase 2 Follow-Up) ---

    /// <summary>Resolve Shape Style Fill - Null Style Element - Returns No Fill.</summary>
    [Fact]
    public void ResolveShapeStyleFill_NullStyleElement_ReturnsNoFill()
    {
        var paint = PptxDocument.ResolveShapeStyleFill(null, BuildTestTheme(), 100, 100);

        Assert.Same(PptxNoFill.Instance, paint);
    }

    /// <summary>Resolve Shape Style Fill - No Fill Ref - Returns No Fill.</summary>
    [Fact]
    public void ResolveShapeStyleFill_NoFillRef_ReturnsNoFill()
    {
        var style = new XElement(P + "style");

        var paint = PptxDocument.ResolveShapeStyleFill(style, BuildTestTheme(), 100, 100);

        Assert.Same(PptxNoFill.Instance, paint);
    }

    /// <summary>Resolve Shape Style Fill - Idx Zero - Returns No Fill.</summary>
    [Fact]
    public void ResolveShapeStyleFill_IdxZero_ReturnsNoFill()
    {
        var style = new XElement(P + "style", new XElement(A + "fillRef", new XAttribute("idx", "0"), SrgbClr("FF00FF")));

        var paint = PptxDocument.ResolveShapeStyleFill(style, BuildTestTheme(), 100, 100);

        Assert.Same(PptxNoFill.Instance, paint);
    }

    /// <summary>
    ///     Pins the no-offset behavior directly: <c>idx="1"</c> must resolve
    ///     <see cref="PptxTheme.FillStyleList"/>'s entry 0 (not entry 1, and not a
    ///     <c>&lt;p:bgRef&gt;</c>-style 1000-offset entry).
    /// </summary>
    [Fact]
    public void ResolveShapeStyleFill_FillRefIdxOne_ResolvesFillStyleListEntryZeroNoOffset()
    {
        var fillStyleList = new List<XElement>
        {
            new(A + "solidFill", SrgbClr("111111")),
            new(A + "solidFill", SrgbClr("222222")),
            new(A + "solidFill", SrgbClr("333333")),
        };
        var theme = BuildTestTheme(fillStyleList: fillStyleList);
        var style = new XElement(P + "style", new XElement(A + "fillRef", new XAttribute("idx", "1")));

        var paint = PptxDocument.ResolveShapeStyleFill(style, theme, 100, 100);

        var solid = Assert.IsType<PptxSolidFill>(paint);
        Assert.Equal(new Rgba32(0x11, 0x11, 0x11, 255), solid.Color);
    }

    /// <summary>Resolve Shape Style Fill - Fill Ref With Scheme Clr Ph Clr - Substitutes Fill Ref Own Color.</summary>
    [Fact]
    public void ResolveShapeStyleFill_FillRefWithSchemeClrPhClr_SubstitutesFillRefOwnColor()
    {
        var fillStyleList = new List<XElement> { new(A + "solidFill", SchemeClr("phClr")) };
        var theme = BuildTestTheme(fillStyleList: fillStyleList);
        var style = new XElement(P + "style", new XElement(A + "fillRef", new XAttribute("idx", "1"), SrgbClr("AA00AA")));

        var paint = PptxDocument.ResolveShapeStyleFill(style, theme, 100, 100);

        var solid = Assert.IsType<PptxSolidFill>(paint);
        Assert.Equal(new Rgba32(0xAA, 0x00, 0xAA, 255), solid.Color);
    }

    /// <summary>Resolve Shape Style Fill - Idx Out Of Range - Throws Invalid Data Exception.</summary>
    [Theory]
    [InlineData("4")]
    [InlineData("1000")]
    public void ResolveShapeStyleFill_IdxOutOfRange_ThrowsInvalidDataException(string idx)
    {
        var fillStyleList = new List<XElement>
        {
            new(A + "solidFill", SrgbClr("111111")),
            new(A + "solidFill", SrgbClr("222222")),
            new(A + "solidFill", SrgbClr("333333")),
        };
        var theme = BuildTestTheme(fillStyleList: fillStyleList);
        var style = new XElement(P + "style", new XElement(A + "fillRef", new XAttribute("idx", idx)));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeStyleFill(style, theme, 100, 100));
    }

    /// <summary>Resolve Shape Style Fill - Idx Past End Of Empty Fill Style List - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveShapeStyleFill_IdxPastEndOfEmptyFillStyleList_ThrowsInvalidDataException()
    {
        var style = new XElement(P + "style", new XElement(A + "fillRef", new XAttribute("idx", "1")));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeStyleFill(style, BuildTestTheme(), 100, 100));
    }

    /// <summary>Resolve Shape Style Fill - Fill Ref With No Idx Attribute - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveShapeStyleFill_FillRefWithNoIdxAttribute_ThrowsInvalidDataException()
    {
        var style = new XElement(P + "style", new XElement(A + "fillRef", SrgbClr("FF00FF")));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeStyleFill(style, BuildTestTheme(), 100, 100));
    }

    // --- ResolveShapeStyleLineStyle: <p:style>/<a:lnRef> shape-style reference (Phase 2 Follow-Up)

    /// <summary>Resolve Shape Style Line Style - Null Style Element - Returns Null.</summary>
    [Fact]
    public void ResolveShapeStyleLineStyle_NullStyleElement_ReturnsNull()
    {
        var lineStyle = PptxDocument.ResolveShapeStyleLineStyle(null, BuildTestTheme());

        Assert.Null(lineStyle);
    }

    /// <summary>Resolve Shape Style Line Style - No Ln Ref - Returns Null.</summary>
    [Fact]
    public void ResolveShapeStyleLineStyle_NoLnRef_ReturnsNull()
    {
        var style = new XElement(P + "style");

        Assert.Null(PptxDocument.ResolveShapeStyleLineStyle(style, BuildTestTheme()));
    }

    /// <summary>Resolve Shape Style Line Style - Idx Zero - Returns Null.</summary>
    [Fact]
    public void ResolveShapeStyleLineStyle_IdxZero_ReturnsNull()
    {
        var style = new XElement(P + "style", new XElement(A + "lnRef", new XAttribute("idx", "0"), SrgbClr("FF00FF")));

        Assert.Null(PptxDocument.ResolveShapeStyleLineStyle(style, BuildTestTheme()));
    }

    /// <summary>Resolve Shape Style Line Style - Ln Ref Idx One - Resolves Ln Style List Entry Zero No Offset.</summary>
    [Fact]
    public void ResolveShapeStyleLineStyle_LnRefIdxOne_ResolvesLnStyleListEntryZeroNoOffset()
    {
        var lnStyleList = new List<XElement>
        {
            new(A + "ln", new XAttribute("w", 12700), new XElement(A + "solidFill", SrgbClr("111111"))),
            new(A + "ln", new XAttribute("w", 25400), new XElement(A + "solidFill", SrgbClr("222222"))),
            new(A + "ln", new XAttribute("w", 38100), new XElement(A + "solidFill", SrgbClr("333333"))),
        };
        var theme = BuildTestTheme(lnStyleList: lnStyleList);
        var style = new XElement(P + "style", new XElement(A + "lnRef", new XAttribute("idx", "1")));

        var lineStyle = PptxDocument.ResolveShapeStyleLineStyle(style, theme);

        Assert.NotNull(lineStyle);
        Assert.Equal(12700f, lineStyle.WidthEmu);
        var solid = Assert.IsType<PptxSolidFill>(lineStyle.Paint);
        Assert.Equal(new Rgba32(0x11, 0x11, 0x11, 255), solid.Color);
    }

    /// <summary>Resolve Shape Style Line Style - Ln Ref With Scheme Clr Ph Clr - Substitutes Ln Ref Own Color.</summary>
    [Fact]
    public void ResolveShapeStyleLineStyle_LnRefWithSchemeClrPhClr_SubstitutesLnRefOwnColor()
    {
        var lnStyleList = new List<XElement>
        {
            new(A + "ln", new XAttribute("w", 12700), new XElement(A + "solidFill", SchemeClr("phClr"))),
        };
        var theme = BuildTestTheme(lnStyleList: lnStyleList);
        var style = new XElement(P + "style", new XElement(A + "lnRef", new XAttribute("idx", "1"), SrgbClr("AA00AA")));

        var lineStyle = PptxDocument.ResolveShapeStyleLineStyle(style, theme);

        Assert.NotNull(lineStyle);
        var solid = Assert.IsType<PptxSolidFill>(lineStyle.Paint);
        Assert.Equal(new Rgba32(0xAA, 0x00, 0xAA, 255), solid.Color);
    }

    /// <summary>Resolve Shape Style Line Style - Idx Out Of Range - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveShapeStyleLineStyle_IdxOutOfRange_ThrowsInvalidDataException()
    {
        var style = new XElement(P + "style", new XElement(A + "lnRef", new XAttribute("idx", "4")));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeStyleLineStyle(style, BuildTestTheme()));
    }

    /// <summary>Resolve Shape Style Line Style - Ln Ref With No Idx Attribute - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveShapeStyleLineStyle_LnRefWithNoIdxAttribute_ThrowsInvalidDataException()
    {
        var style = new XElement(P + "style", new XElement(A + "lnRef", SrgbClr("FF00FF")));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeStyleLineStyle(style, BuildTestTheme()));
    }

    // --- ResolveShapeLineStyle: merges a shape's own <a:ln> with its <p:style>/<a:lnRef>
    // fallback per the corrected 4-case precedence (Phase 2 Follow-Up: Shape Style References) ---

    /// <summary>Resolve Shape Line Style - Ln Element Null - Defers To Style Line Style.</summary>
    [Fact]
    public void ResolveShapeLineStyle_LnElementNull_DefersToStyleLineStyle()
    {
        var lnStyleList = new List<XElement>
        {
            new(A + "ln", new XAttribute("w", 12700), new XElement(A + "solidFill", SrgbClr("111111"))),
        };
        var theme = BuildTestTheme(lnStyleList: lnStyleList);
        var style = new XElement(P + "style", new XElement(A + "lnRef", new XAttribute("idx", "1")));

        var viaNewMethod = PptxDocument.ResolveShapeLineStyle(null, style, theme);
        var viaStyleOnly = PptxDocument.ResolveShapeStyleLineStyle(style, theme);

        Assert.NotNull(viaNewMethod);
        Assert.NotNull(viaStyleOnly);
        Assert.Equal(viaStyleOnly.WidthEmu, viaNewMethod.WidthEmu);
        Assert.Equal(
            Assert.IsType<PptxSolidFill>(viaStyleOnly.Paint).Color,
            Assert.IsType<PptxSolidFill>(viaNewMethod.Paint).Color);
    }

    /// <summary>Resolve Shape Line Style - Ln Has Explicit Solid Fill - Own Fill Wins Over Style.</summary>
    [Fact]
    public void ResolveShapeLineStyle_LnHasExplicitSolidFill_OwnFillWinsOverStyle()
    {
        var lnStyleList = new List<XElement>
        {
            new(A + "ln", new XAttribute("w", 999999), new XElement(A + "solidFill", SrgbClr("00FF00"))),
        };
        var theme = BuildTestTheme(lnStyleList: lnStyleList);
        var style = new XElement(P + "style", new XElement(A + "lnRef", new XAttribute("idx", "1")));
        var lnElement = new XElement(A + "ln", new XAttribute("w", 12700), new XElement(A + "solidFill", SrgbClr("FF0000")));

        var lineStyle = PptxDocument.ResolveShapeLineStyle(lnElement, style, theme);

        Assert.NotNull(lineStyle);
        Assert.Equal(12700f, lineStyle.WidthEmu);
        var solid = Assert.IsType<PptxSolidFill>(lineStyle.Paint);
        Assert.Equal(new Rgba32(0xFF, 0x00, 0x00, 255), solid.Color);
    }

    /// <summary>Resolve Shape Line Style - Ln Has Explicit No Fill - Resolves Null Regardless Of Style.</summary>
    [Fact]
    public void ResolveShapeLineStyle_LnHasExplicitNoFill_ResolvesNullRegardlessOfStyle()
    {
        var lnStyleList = new List<XElement>
        {
            new(A + "ln", new XAttribute("w", 999999), new XElement(A + "solidFill", SrgbClr("00FF00"))),
        };
        var theme = BuildTestTheme(lnStyleList: lnStyleList);
        var style = new XElement(P + "style", new XElement(A + "lnRef", new XAttribute("idx", "1")));
        var lnElement = new XElement(A + "ln", new XAttribute("w", 12700), new XElement(A + "noFill"));

        var lineStyle = PptxDocument.ResolveShapeLineStyle(lnElement, style, theme);

        Assert.Null(lineStyle);
    }

    /// <summary>Resolve Shape Line Style - Ln Has Width But No Fill Child - Keeps Own Width Uses Style Color.</summary>
    [Fact]
    public void ResolveShapeLineStyle_LnHasWidthButNoFillChild_KeepsOwnWidthUsesStyleColor()
    {
        var lnStyleList = new List<XElement>
        {
            new(A + "ln", new XAttribute("w", 999999), new XElement(A + "solidFill", SrgbClr("00FF00"))),
        };
        var theme = BuildTestTheme(lnStyleList: lnStyleList);
        var style = new XElement(P + "style", new XElement(A + "lnRef", new XAttribute("idx", "1")));
        var lnElement = new XElement(A + "ln", new XAttribute("w", 76200));

        var lineStyle = PptxDocument.ResolveShapeLineStyle(lnElement, style, theme);

        Assert.NotNull(lineStyle);
        Assert.Equal(76200f, lineStyle.WidthEmu);
        var solid = Assert.IsType<PptxSolidFill>(lineStyle.Paint);
        Assert.Equal(new Rgba32(0x00, 0xFF, 0x00, 255), solid.Color);
    }

    /// <summary>Proves a non-numeric <c>w</c> attribute on a case-3 (no fill child) <c>&lt;a:ln&gt;</c> throws <see cref="InvalidDataException"/> rather than letting a raw <see cref="FormatException"/> escape uncaught.</summary>
    [Fact]
    public void ResolveShapeLineStyle_LnHasNonNumericWidth_ThrowsInvalidDataException()
    {
        var style = new XElement(P + "style", new XElement(A + "lnRef", new XAttribute("idx", "1")));
        var lnElement = new XElement(A + "ln", new XAttribute("w", "not-a-number"));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeLineStyle(lnElement, style, BuildTestTheme()));
    }

    /// <summary>Proves a non-finite (<c>NaN</c>/<c>Infinity</c>/<c>-Infinity</c>) <c>w</c> attribute on a case-3 (no fill child) <c>&lt;a:ln&gt;</c> throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void ResolveShapeLineStyle_LnHasNonFiniteWidth_ThrowsInvalidDataException(string nonFiniteValue)
    {
        var style = new XElement(P + "style", new XElement(A + "lnRef", new XAttribute("idx", "1")));
        var lnElement = new XElement(A + "ln", new XAttribute("w", nonFiniteValue));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeLineStyle(lnElement, style, BuildTestTheme()));
    }

    /// <summary>Resolve Shape Line Style - Ln Has Width And Own Dash But No Fill Child - Keeps Own Dash Array.</summary>
    [Fact]
    public void ResolveShapeLineStyle_LnHasWidthAndOwnDashButNoFillChild_KeepsOwnDashArray()
    {
        var lnStyleList = new List<XElement>
        {
            new(A + "ln", new XAttribute("w", 999999), new XElement(A + "solidFill", SrgbClr("00FF00"))),
        };
        var theme = BuildTestTheme(lnStyleList: lnStyleList);
        var style = new XElement(P + "style", new XElement(A + "lnRef", new XAttribute("idx", "1")));
        var lnElement = new XElement(
            A + "ln", new XAttribute("w", 76200), new XElement(A + "prstDash", new XAttribute("val", "dash")));

        var lineStyle = PptxDocument.ResolveShapeLineStyle(lnElement, style, theme);

        Assert.NotNull(lineStyle);
        Assert.NotNull(lineStyle.DashArray);
        Assert.Equal([76200f * 4f, 76200f * 3f], lineStyle.DashArray);
    }

    /// <summary>
    ///     Resolve Shape Line Style - Ln Has Width But No Fill Child And No Style Element -
    ///     Resolves Null Default.
    /// </summary>
    [Fact]
    public void ResolveShapeLineStyle_LnHasWidthButNoFillChildAndNoStyleElement_ResolvesNullDefault()
    {
        var lnElement = new XElement(A + "ln", new XAttribute("w", 76200));

        var lineStyle = PptxDocument.ResolveShapeLineStyle(lnElement, null, BuildTestTheme());

        Assert.Null(lineStyle);
    }

    /// <summary>
    ///     Resolve Shape Line Style - Ln Has Width But No Fill Child And Style Ln Ref Idx Zero -
    ///     Resolves Null Default.
    /// </summary>
    [Fact]
    public void ResolveShapeLineStyle_LnHasWidthButNoFillChildAndStyleLnRefIdxZero_ResolvesNullDefault()
    {
        var style = new XElement(P + "style", new XElement(A + "lnRef", new XAttribute("idx", "0")));
        var lnElement = new XElement(A + "ln", new XAttribute("w", 76200));

        var lineStyle = PptxDocument.ResolveShapeLineStyle(lnElement, style, BuildTestTheme());

        Assert.Null(lineStyle);
    }

    /// <summary>Resolve Shape Line Style - Ln Has No Width And No Fill Child - Resolves Null Regardless Of Style.</summary>
    [Fact]
    public void ResolveShapeLineStyle_LnHasNoWidthAndNoFillChild_ResolvesNullRegardlessOfStyle()
    {
        var lnStyleList = new List<XElement>
        {
            new(A + "ln", new XAttribute("w", 999999), new XElement(A + "solidFill", SrgbClr("00FF00"))),
        };
        var theme = BuildTestTheme(lnStyleList: lnStyleList);
        var style = new XElement(P + "style", new XElement(A + "lnRef", new XAttribute("idx", "1")));
        var lnElement = new XElement(A + "ln");

        var lineStyle = PptxDocument.ResolveShapeLineStyle(lnElement, style, theme);

        Assert.Null(lineStyle);
    }

    // --- ResolveStrokeOutline: mirrors the PDF renderer's StrokeStyle/PathStroker call pattern ---

    /// <summary>Resolve Stroke Outline - Rectangle With Solid Line - Produces Non Empty Outline.</summary>
    [Fact]
    public void ResolveStrokeOutline_RectangleWithSolidLine_ProducesNonEmptyOutline()
    {
        var shapePath = Path.Rectangle(0, 0, 100, 50);
        var lineStyle = new PptxLineStyle(10f, new PptxSolidFill(new Rgba32(0, 0, 0, 255)), null);

        var outline = PptxDocument.ResolveStrokeOutline(shapePath, lineStyle, System.Numerics.Matrix3x2.Identity);

        Assert.NotEmpty(outline.Subpaths);
    }

    // --- PptxPatternTileRenderer.RenderTile: per-preset tile-synthesis unit tests ----------------

    /// <summary>An arbitrary, distinct foreground test color.</summary>
    private static readonly Rgba32 Fg = new(0x2C, 0xB7, 0x31, 255);

    /// <summary>An arbitrary, distinct background test color.</summary>
    private static readonly Rgba32 Bg = new(0xFF, 0xFF, 0xFF, 255);

    /// <summary>Asserts every pixel of <paramref name="surface"/> is either <see cref="Fg"/> or <see cref="Bg"/>, and that both colors actually appear.</summary>
    private static void AssertOnlyFgAndBgPresent(Surface surface)
    {
        var sawFg = false;
        var sawBg = false;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var pixel = surface[x, y];
                if (pixel == Fg)
                {
                    sawFg = true;
                }
                else if (pixel == Bg)
                {
                    sawBg = true;
                }
                else
                {
                    Assert.Fail($"Unexpected pixel color {pixel} at ({x},{y}); expected only Fg/Bg.");
                }
            }
        }

        Assert.True(sawFg, "Expected at least one foreground-colored pixel.");
        Assert.True(sawBg, "Expected at least one background-colored pixel.");
    }

    /// <summary>Render Tile - Horz - Alternates Full Rows.</summary>
    [Fact]
    public void RenderTile_Horz_AlternatesFullRows()
    {
        using var tile = PptxPatternTileRenderer.RenderTile(PptxPresetPattern.Horz, Fg, Bg);

        for (var y = 0; y < tile.Height; y++)
        {
            var expected = y % 2 == 0 ? Fg : Bg;
            for (var x = 0; x < tile.Width; x++)
            {
                Assert.Equal(expected, tile[x, y]);
            }
        }

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Vert - Alternates Full Columns.</summary>
    [Fact]
    public void RenderTile_Vert_AlternatesFullColumns()
    {
        using var tile = PptxPatternTileRenderer.RenderTile(PptxPresetPattern.Vert, Fg, Bg);

        for (var x = 0; x < tile.Width; x++)
        {
            var expected = x % 2 == 0 ? Fg : Bg;
            for (var y = 0; y < tile.Height; y++)
            {
                Assert.Equal(expected, tile[x, y]);
            }
        }

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Lt Horz - Sparse Thin Rows.</summary>
    [Fact]
    public void RenderTile_LtHorz_SparseThinRows()
    {
        using var tile = PptxPatternTileRenderer.RenderTile(PptxPresetPattern.LtHorz, Fg, Bg);

        for (var y = 0; y < tile.Height; y++)
        {
            var expected = y % 4 == 0 ? Fg : Bg;
            Assert.Equal(expected, tile[0, y]);
        }

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Dk Horz - Dominant Rows.</summary>
    [Fact]
    public void RenderTile_DkHorz_DominantRows()
    {
        using var tile = PptxPatternTileRenderer.RenderTile(PptxPresetPattern.DkHorz, Fg, Bg);

        for (var y = 0; y < tile.Height; y++)
        {
            var expected = y % 4 != 0 ? Fg : Bg;
            Assert.Equal(expected, tile[0, y]);
        }

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Lt Vert - Sparse Thin Columns.</summary>
    [Fact]
    public void RenderTile_LtVert_SparseThinColumns()
    {
        using var tile = PptxPatternTileRenderer.RenderTile(PptxPresetPattern.LtVert, Fg, Bg);

        for (var x = 0; x < tile.Width; x++)
        {
            var expected = x % 4 == 0 ? Fg : Bg;
            Assert.Equal(expected, tile[x, 0]);
        }

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Dk Vert - Dominant Columns.</summary>
    [Fact]
    public void RenderTile_DkVert_DominantColumns()
    {
        using var tile = PptxPatternTileRenderer.RenderTile(PptxPresetPattern.DkVert, Fg, Bg);

        for (var x = 0; x < tile.Width; x++)
        {
            var expected = x % 4 != 0 ? Fg : Bg;
            Assert.Equal(expected, tile[x, 0]);
        }

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Dn Diag - Follows X Plus Y Modulo Rule.</summary>
    [Fact]
    public void RenderTile_DnDiag_FollowsXPlusYModuloRule()
    {
        using var tile = PptxPatternTileRenderer.RenderTile(PptxPresetPattern.DnDiag, Fg, Bg);

        for (var y = 0; y < tile.Height; y++)
        {
            for (var x = 0; x < tile.Width; x++)
            {
                var expected = (x + y) % 4 == 0 ? Fg : Bg;
                Assert.Equal(expected, tile[x, y]);
            }
        }

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Up Diag - Follows X Minus Y Modulo Rule.</summary>
    [Fact]
    public void RenderTile_UpDiag_FollowsXMinusYModuloRule()
    {
        using var tile = PptxPatternTileRenderer.RenderTile(PptxPresetPattern.UpDiag, Fg, Bg);

        for (var y = 0; y < tile.Height; y++)
        {
            for (var x = 0; x < tile.Width; x++)
            {
                var expected = (x - y + PptxPatternTileRenderer.TileSize) % 4 == 0 ? Fg : Bg;
                Assert.Equal(expected, tile[x, y]);
            }
        }

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Wd Dn Diag - Produces Wide Bands.</summary>
    [Fact]
    public void RenderTile_WdDnDiag_ProducesWideBands()
    {
        using var tile = PptxPatternTileRenderer.RenderTile(PptxPresetPattern.WdDnDiag, Fg, Bg);

        for (var y = 0; y < tile.Height; y++)
        {
            for (var x = 0; x < tile.Width; x++)
            {
                var expected = (x + y) % 8 < 4 ? Fg : Bg;
                Assert.Equal(expected, tile[x, y]);
            }
        }

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Cross - Fg At Horizontal Or Vertical Lines.</summary>
    [Fact]
    public void RenderTile_Cross_FgAtHorizontalOrVerticalLines()
    {
        using var tile = PptxPatternTileRenderer.RenderTile(PptxPresetPattern.Cross, Fg, Bg);

        for (var y = 0; y < tile.Height; y++)
        {
            for (var x = 0; x < tile.Width; x++)
            {
                var expected = y % 4 == 0 || x % 4 == 0 ? Fg : Bg;
                Assert.Equal(expected, tile[x, y]);
            }
        }

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Diag Cross - Fg At Either Diagonal.</summary>
    [Fact]
    public void RenderTile_DiagCross_FgAtEitherDiagonal()
    {
        using var tile = PptxPatternTileRenderer.RenderTile(PptxPresetPattern.DiagCross, Fg, Bg);

        for (var y = 0; y < tile.Height; y++)
        {
            for (var x = 0; x < tile.Width; x++)
            {
                var expected = (x + y) % 4 == 0 || (x - y + PptxPatternTileRenderer.TileSize) % 4 == 0 ? Fg : Bg;
                Assert.Equal(expected, tile[x, y]);
            }
        }

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Percentage Family - Density Increases Monotonically With Percent.</summary>
    [Theory]
    [InlineData("pct5")]
    [InlineData("pct10")]
    [InlineData("pct20")]
    [InlineData("pct25")]
    [InlineData("pct30")]
    [InlineData("pct40")]
    [InlineData("pct50")]
    [InlineData("pct60")]
    [InlineData("pct70")]
    [InlineData("pct75")]
    [InlineData("pct80")]
    [InlineData("pct90")]
    public void RenderTile_PercentageFamily_BothColorsPresent(string prst)
    {
        using var tile = PptxPatternTileRenderer.RenderTile(ParsePresetName(prst), Fg, Bg);

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Percentage Family - Fg Pixel Count Increases With Percent.</summary>
    [Fact]
    public void RenderTile_PercentageFamily_FgPixelCountIncreasesWithPercent()
    {
        int CountFg(PptxPresetPattern preset)
        {
            using var tile = PptxPatternTileRenderer.RenderTile(preset, Fg, Bg);
            var count = 0;
            for (var y = 0; y < tile.Height; y++)
            {
                for (var x = 0; x < tile.Width; x++)
                {
                    if (tile[x, y] == Fg)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        var counts = new[]
        {
            CountFg(PptxPresetPattern.Pct5), CountFg(PptxPresetPattern.Pct10), CountFg(PptxPresetPattern.Pct25),
            CountFg(PptxPresetPattern.Pct50), CountFg(PptxPresetPattern.Pct75), CountFg(PptxPresetPattern.Pct90),
        };

        for (var i = 1; i < counts.Length; i++)
        {
            Assert.True(
                counts[i] >= counts[i - 1],
                $"Expected non-decreasing foreground pixel density; counts were [{string.Join(", ", counts)}].");
        }
    }

    /// <summary>Render Tile - Divot - Both Colors Present.</summary>
    [Fact]
    public void RenderTile_Divot_BothColorsPresent()
    {
        using var tile = PptxPatternTileRenderer.RenderTile(PptxPresetPattern.Divot, Fg, Bg);

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Wave - Both Colors Present.</summary>
    [Fact]
    public void RenderTile_Wave_BothColorsPresent()
    {
        using var tile = PptxPatternTileRenderer.RenderTile(PptxPresetPattern.Wave, Fg, Bg);

        AssertOnlyFgAndBgPresent(tile);
    }

    /// <summary>Render Tile - Every Covered Preset - Produces Exactly Tile Size Square Surface.</summary>
    [Theory]
    [InlineData("horz")]
    [InlineData("vert")]
    [InlineData("ltHorz")]
    [InlineData("ltVert")]
    [InlineData("dkHorz")]
    [InlineData("dkVert")]
    [InlineData("dnDiag")]
    [InlineData("upDiag")]
    [InlineData("ltDnDiag")]
    [InlineData("ltUpDiag")]
    [InlineData("dkDnDiag")]
    [InlineData("dkUpDiag")]
    [InlineData("wdDnDiag")]
    [InlineData("wdUpDiag")]
    [InlineData("cross")]
    [InlineData("diagCross")]
    [InlineData("divot")]
    [InlineData("wave")]
    public void RenderTile_EveryCoveredPreset_ProducesTileSizeSquareSurface(string prst)
    {
        using var tile = PptxPatternTileRenderer.RenderTile(ParsePresetName(prst), Fg, Bg);

        Assert.Equal(PptxPatternTileRenderer.TileSize, tile.Width);
        Assert.Equal(PptxPatternTileRenderer.TileSize, tile.Height);
    }

    /// <summary>
    ///     Maps an <c>ST_PresetPatternVal</c> <c>prst</c> name to its <see cref="PptxPresetPattern"/>
    ///     via a real, minimal <c>&lt;a:pattFill&gt;</c>/<see cref="PptxDocument.ResolveFill"/>
    ///     round trip - avoiding a second, independent (and possibly divergent) string-to-enum
    ///     mapping table duplicated purely for test parameterization.
    /// </summary>
    private static PptxPresetPattern ParsePresetName(string prst)
    {
        var spPr = new XElement(A + "spPr", new XElement(A + "pattFill", new XAttribute("prst", prst)));
        var paint = PptxDocument.ResolveFill(spPr, BuildTestTheme(), 100, 100);
        return Assert.IsType<PptxPatternFill>(paint).Preset;
    }
}
