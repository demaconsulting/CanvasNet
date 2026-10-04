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

    /// <summary>Builds an arbitrary, fully-populated test <see cref="PptxTheme"/>, with each color-scheme slot a distinct, recognizable value.</summary>
    private static PptxTheme BuildTestTheme() =>
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
                new PptxFontCollection("MinorLatin", "MinorEA", "MinorCS")));

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

    /// <summary>Resolve Color - Unsupported Color Kind - Throws Pptx Unsupported Feature Exception.</summary>
    [Fact]
    public void ResolveColor_UnsupportedColorKind_ThrowsPptxUnsupportedFeatureException()
    {
        var element = new XElement(A + "hslClr", new XAttribute("hue", 0), new XAttribute("sat", "0%"), new XAttribute("lum", "0%"));

        var ex = Assert.Throws<PptxUnsupportedFeatureException>(() => PptxDocument.ResolveColor(element, BuildTestTheme()));
        Assert.Equal("pptx-color-kind", ex.Feature);
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
