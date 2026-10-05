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
    [Fact]
    public void ResolveFill_PatternFill_ThrowsPptxUnsupportedFeatureException()
    {
        var spPr = new XElement(A + "spPr", new XElement(A + "pattFill"));

        var ex = Assert.Throws<PptxUnsupportedFeatureException>(() => PptxDocument.ResolveFill(spPr, BuildTestTheme(), 100, 100));
        Assert.Equal("pptx-pattern-fill", ex.Feature);
    }

    /// <summary>Resolve Fill - Picture Fill - Throws Pptx Unsupported Feature Exception.</summary>
    [Fact]
    public void ResolveFill_PictureFill_ThrowsPptxUnsupportedFeatureException()
    {
        var spPr = new XElement(A + "spPr", new XElement(A + "blipFill"));

        var ex = Assert.Throws<PptxUnsupportedFeatureException>(() => PptxDocument.ResolveFill(spPr, BuildTestTheme(), 100, 100));
        Assert.Equal("pptx-picture-fill", ex.Feature);
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

        var outline = PptxDocument.ResolveStrokeOutline(shapePath, lineStyle);

        Assert.NotEmpty(outline.Subpaths);
    }
}
