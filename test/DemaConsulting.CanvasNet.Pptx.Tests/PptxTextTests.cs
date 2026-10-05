using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx txbody bodypr lstyle lstStyle lnspc spcpct spcpts defrpr lststyle sng calibri arial
// cspell:ignore Srgb srgb EEECE BACC hlink Hlink folhlink

/// <summary>
///     Unit-level tests for the Phase 1d DrawingML text parser (<c>PptxDocument.Text.cs</c>) and
///     the attribute-level run/paragraph property-inheritance resolver
///     (<c>PptxDocument.TextInheritance.cs</c>), including the master <c>&lt;p:txStyles&gt;</c>
///     plumbing added to <c>PptxDocument.Masters.cs</c>/<c>PptxPlaceholder.cs</c>. Complements
///     <see cref="PptxTextLayoutTests"/> (word-wrap/alignment/anchor/autofit),
///     <see cref="PptxTextRenderTests"/> (glyph painting), and
///     <see cref="PptxSystemIntegrationTests"/> (end-to-end).
/// </summary>
public class PptxTextTests
{
    private static readonly XNamespace DrawingNs = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace PresentationNs = "http://schemas.openxmlformats.org/presentationml/2006/main";

    /// <summary>Builds an arbitrary, fully-populated test <see cref="PptxTheme"/> for parser/inheritance tests.</summary>
    private static PptxTheme BuildTestTheme() =>
        new(
            new PptxColorScheme(
                new Rgba32(10, 10, 10, 255), new Rgba32(20, 20, 20, 255), new Rgba32(30, 30, 30, 255), new Rgba32(40, 40, 40, 255),
                new Rgba32(50, 50, 50, 255), new Rgba32(60, 60, 60, 255), new Rgba32(70, 70, 70, 255), new Rgba32(80, 80, 80, 255),
                new Rgba32(90, 90, 90, 255), new Rgba32(100, 100, 100, 255), new Rgba32(110, 110, 110, 255), new Rgba32(120, 120, 120, 255)),
            new PptxFontScheme(
                new PptxFontCollection("ThemeMajorLatin", "MajorEA", "MajorCS"),
                new PptxFontCollection("ThemeMinorLatin", "MinorEA", "MinorCS")));

    private static PptxPlaceholderProperties EmptyPlaceholderProperties(PptxTheme theme, PptxMasterTextStyles? masterTextStyles = null) =>
        new(null, null, theme, masterTextStyles);

    #region ParseTextBody / ParseBodyProperties

    /// <summary>Proves an absent <c>&lt;a:bodyPr&gt;</c> resolves every documented OOXML schema default.</summary>
    [Fact]
    public void ParseBodyProperties_Absent_ResolvesSchemaDefaults()
    {
        var result = PptxDocument.ParseBodyProperties(null);

        Assert.Equal(PptxTextAnchor.Top, result.Anchor);
        Assert.Equal(PptxTextWrap.Square, result.Wrap);
        Assert.Equal(91440f, result.InsetLeftEmu);
        Assert.Equal(45720f, result.InsetTopEmu);
        Assert.Equal(91440f, result.InsetRightEmu);
        Assert.Equal(45720f, result.InsetBottomEmu);
        Assert.Null(result.AutofitElement);
    }

    /// <summary>Parse Body Properties Anchor Ctr Resolves Middle.</summary>
    [Fact]
    public void ParseBodyProperties_AnchorCtr_ResolvesMiddle()
    {
        var bodyPr = new XElement(DrawingNs + "bodyPr", new XAttribute("anchor", "ctr"));

        var result = PptxDocument.ParseBodyProperties(bodyPr);

        Assert.Equal(PptxTextAnchor.Middle, result.Anchor);
    }

    /// <summary>Parse Body Properties Anchor B Resolves Bottom.</summary>
    [Fact]
    public void ParseBodyProperties_AnchorB_ResolvesBottom()
    {
        var bodyPr = new XElement(DrawingNs + "bodyPr", new XAttribute("anchor", "b"));

        var result = PptxDocument.ParseBodyProperties(bodyPr);

        Assert.Equal(PptxTextAnchor.Bottom, result.Anchor);
    }

    /// <summary>Parse Body Properties Anchor T Resolves Top.</summary>
    [Fact]
    public void ParseBodyProperties_AnchorT_ResolvesTop()
    {
        var bodyPr = new XElement(DrawingNs + "bodyPr", new XAttribute("anchor", "t"));

        var result = PptxDocument.ParseBodyProperties(bodyPr);

        Assert.Equal(PptxTextAnchor.Top, result.Anchor);
    }

    /// <summary>Parse Body Properties Wrap None Resolves Wrap None.</summary>
    [Fact]
    public void ParseBodyProperties_WrapNone_ResolvesWrapNone()
    {
        var bodyPr = new XElement(DrawingNs + "bodyPr", new XAttribute("wrap", "none"));

        var result = PptxDocument.ParseBodyProperties(bodyPr);

        Assert.Equal(PptxTextWrap.None, result.Wrap);
    }

    /// <summary>Parse Body Properties Custom Insets Resolves Declared Values.</summary>
    [Fact]
    public void ParseBodyProperties_CustomInsets_ResolvesDeclaredValues()
    {
        var bodyPr = new XElement(
            DrawingNs + "bodyPr",
            new XAttribute("lIns", "10000"),
            new XAttribute("tIns", "20000"),
            new XAttribute("rIns", "30000"),
            new XAttribute("bIns", "40000"));

        var result = PptxDocument.ParseBodyProperties(bodyPr);

        Assert.Equal(10000f, result.InsetLeftEmu);
        Assert.Equal(20000f, result.InsetTopEmu);
        Assert.Equal(30000f, result.InsetRightEmu);
        Assert.Equal(40000f, result.InsetBottomEmu);
    }

    /// <summary>Proves a non-numeric <c>bIns</c> attribute throws <see cref="InvalidDataException"/> (with the original <see cref="FormatException"/> preserved as <see cref="Exception.InnerException"/>) rather than letting the raw <see cref="FormatException"/> escape uncaught.</summary>
    [Fact]
    public void ParseBodyProperties_NonNumericBIns_ThrowsInvalidDataException()
    {
        var bodyPr = new XElement(DrawingNs + "bodyPr", new XAttribute("bIns", "not-a-number"));

        var ex = Assert.Throws<InvalidDataException>(() => PptxDocument.ParseBodyProperties(bodyPr));

        Assert.IsType<FormatException>(ex.InnerException);
    }

    /// <summary>Proves a non-numeric <c>lIns</c> attribute throws <see cref="InvalidDataException"/> (with the original <see cref="FormatException"/> preserved as <see cref="Exception.InnerException"/>) rather than letting the raw <see cref="FormatException"/> escape uncaught.</summary>
    [Fact]
    public void ParseBodyProperties_NonNumericLIns_ThrowsInvalidDataException()
    {
        var bodyPr = new XElement(DrawingNs + "bodyPr", new XAttribute("lIns", "not-a-number"));

        var ex = Assert.Throws<InvalidDataException>(() => PptxDocument.ParseBodyProperties(bodyPr));

        Assert.IsType<FormatException>(ex.InnerException);
    }

    /// <summary>Proves a non-numeric <c>tIns</c> attribute throws <see cref="InvalidDataException"/> (with the original <see cref="FormatException"/> preserved as <see cref="Exception.InnerException"/>) rather than letting the raw <see cref="FormatException"/> escape uncaught.</summary>
    [Fact]
    public void ParseBodyProperties_NonNumericTIns_ThrowsInvalidDataException()
    {
        var bodyPr = new XElement(DrawingNs + "bodyPr", new XAttribute("tIns", "not-a-number"));

        var ex = Assert.Throws<InvalidDataException>(() => PptxDocument.ParseBodyProperties(bodyPr));

        Assert.IsType<FormatException>(ex.InnerException);
    }

    /// <summary>Proves a non-numeric <c>rIns</c> attribute throws <see cref="InvalidDataException"/> (with the original <see cref="FormatException"/> preserved as <see cref="Exception.InnerException"/>) rather than letting the raw <see cref="FormatException"/> escape uncaught.</summary>
    [Fact]
    public void ParseBodyProperties_NonNumericRIns_ThrowsInvalidDataException()
    {
        var bodyPr = new XElement(DrawingNs + "bodyPr", new XAttribute("rIns", "not-a-number"));

        var ex = Assert.Throws<InvalidDataException>(() => PptxDocument.ParseBodyProperties(bodyPr));

        Assert.IsType<FormatException>(ex.InnerException);
    }

    /// <summary>Parse Body Properties Autofit Element Is Retained Unparsed.</summary>
    [Theory]
    [InlineData("noAutofit")]
    [InlineData("normAutofit")]
    [InlineData("spAutoFit")]
    public void ParseBodyProperties_AutofitElement_IsRetainedUnparsed(string autofitElementName)
    {
        var bodyPr = new XElement(DrawingNs + "bodyPr", new XElement(DrawingNs + autofitElementName));

        var result = PptxDocument.ParseBodyProperties(bodyPr);

        Assert.NotNull(result.AutofitElement);
        Assert.Equal(autofitElementName, result.AutofitElement.Name.LocalName);
    }

    /// <summary>Proves a <c>&lt;p:txBody&gt;</c> with no paragraphs still parses to an empty, non-null paragraph list.</summary>
    [Fact]
    public void ParseTextBody_NoParagraphs_ResolvesEmptyParagraphList()
    {
        var txBody = new XElement(PresentationNs + "txBody", new XElement(DrawingNs + "bodyPr"));

        var result = PptxDocument.ParseTextBody(txBody);

        Assert.Empty(result.Paragraphs);
    }

    /// <summary>Parse Text Body Single Paragraph Single Run Parses Body And Paragraph.</summary>
    [Fact]
    public void ParseTextBody_SingleParagraphSingleRun_ParsesBodyAndParagraph()
    {
        var txBody = new XElement(
            PresentationNs + "txBody",
            new XElement(DrawingNs + "bodyPr", new XAttribute("anchor", "ctr")),
            new XElement(
                DrawingNs + "p",
                new XElement(
                    DrawingNs + "r",
                    new XElement(DrawingNs + "rPr", new XAttribute("b", "1")),
                    new XElement(DrawingNs + "t", "Hello"))));

        var result = PptxDocument.ParseTextBody(txBody);

        Assert.Equal(PptxTextAnchor.Middle, result.Properties.Anchor);
        var paragraph = Assert.Single(result.Paragraphs);
        var run = Assert.Single(paragraph.Runs);
        Assert.Equal("Hello", run.Text);
        Assert.NotNull(run.RawRPr);
    }

    #endregion

    #region ParseParagraph / ParseParagraphProperties

    /// <summary>Parse Paragraph Properties Absent Resolves Level Zero And Null Optionals.</summary>
    [Fact]
    public void ParseParagraphProperties_Absent_ResolvesLevelZeroAndNullOptionals()
    {
        var result = PptxDocument.ParseParagraphProperties(null);

        Assert.Equal(0, result.Level);
        Assert.Null(result.Algn);
        Assert.Null(result.MarLEmu);
        Assert.Null(result.IndentEmu);
        Assert.Null(result.LnSpcElement);
        Assert.Null(result.SpcBeforeElement);
        Assert.Null(result.SpcAfterElement);
        Assert.Null(result.DefRPrElement);
    }

    /// <summary>Parse Paragraph Properties Level Attribute Clamps To Zero To Eight.</summary>
    [Theory]
    [InlineData("0", 0)]
    [InlineData("8", 8)]
    [InlineData("20", 8)]
    public void ParseParagraphProperties_LevelAttribute_ClampsToZeroToEight(string lvl, int expected)
    {
        var pPr = new XElement(DrawingNs + "pPr", new XAttribute("lvl", lvl));

        var result = PptxDocument.ParseParagraphProperties(pPr);

        Assert.Equal(expected, result.Level);
    }

    /// <summary>Parse Paragraph Properties Fully Populated Resolves Every Field.</summary>
    [Fact]
    public void ParseParagraphProperties_FullyPopulated_ResolvesEveryField()
    {
        var pPr = new XElement(
            DrawingNs + "pPr",
            new XAttribute("algn", "ctr"),
            new XAttribute("marL", "123"),
            new XAttribute("indent", "-456"),
            new XElement(DrawingNs + "lnSpc", new XElement(DrawingNs + "spcPct", new XAttribute("val", "150000"))),
            new XElement(DrawingNs + "spcBef", new XElement(DrawingNs + "spcPts", new XAttribute("val", "600"))),
            new XElement(DrawingNs + "spcAft", new XElement(DrawingNs + "spcPts", new XAttribute("val", "600"))),
            new XElement(DrawingNs + "defRPr", new XAttribute("sz", "2400")));

        var result = PptxDocument.ParseParagraphProperties(pPr);

        Assert.Equal("ctr", result.Algn);
        Assert.Equal(123f, result.MarLEmu);
        Assert.Equal(-456f, result.IndentEmu);
        Assert.NotNull(result.LnSpcElement);
        Assert.NotNull(result.SpcBeforeElement);
        Assert.NotNull(result.SpcAfterElement);
        Assert.NotNull(result.DefRPrElement);
    }

    /// <summary>Proves an <c>&lt;a:p&gt;</c> with no recognized child is a valid, empty paragraph - not an error.</summary>
    [Fact]
    public void ParseParagraph_NoChildren_ResolvesEmptyRunList()
    {
        var p = new XElement(DrawingNs + "p");

        var result = PptxDocument.ParseParagraph(p);

        Assert.Empty(result.Runs);
        Assert.Equal(0, result.RawProperties.Level);
    }

    /// <summary>
    ///     Proves an interleaved <c>&lt;a:br/&gt;</c> is preserved as an explicit
    ///     <see cref="PptxLineBreakItem"/> in document order alongside its surrounding runs -
    ///     not silently dropped - so the layout stage can later honor it as a forced line
    ///     boundary.
    /// </summary>
    [Fact]
    public void ParseParagraph_RunBreakRun_PreservesBreakInDocumentOrder()
    {
        var p = new XElement(
            DrawingNs + "p",
            new XElement(DrawingNs + "r", new XElement(DrawingNs + "t", "A")),
            new XElement(DrawingNs + "br"),
            new XElement(DrawingNs + "r", new XElement(DrawingNs + "t", "B")));

        var result = PptxDocument.ParseParagraph(p);

        Assert.Equal(3, result.Items.Count);
        var firstRun = Assert.IsType<PptxRunItem>(result.Items[0]);
        Assert.Equal("A", firstRun.Run.Text);
        Assert.IsType<PptxLineBreakItem>(result.Items[1]);
        var secondRun = Assert.IsType<PptxRunItem>(result.Items[2]);
        Assert.Equal("B", secondRun.Run.Text);

        // The run-only convenience accessor still reflects just the two runs, in order.
        Assert.Equal(2, result.Runs.Count);
        Assert.Equal("A", result.Runs[0].Text);
        Assert.Equal("B", result.Runs[1].Text);
    }

    #endregion

    #region ParseRun

    /// <summary>Parse Run No Text Resolves Empty String.</summary>
    [Fact]
    public void ParseRun_NoText_ResolvesEmptyString()
    {
        var r = new XElement(DrawingNs + "r");

        var result = PptxDocument.ParseRun(r);

        Assert.Equal(string.Empty, result.Text);
        Assert.Null(result.RawRPr);
    }

    /// <summary>Parse Run With Text And R Pr Resolves Both.</summary>
    [Fact]
    public void ParseRun_WithTextAndRPr_ResolvesBoth()
    {
        var r = new XElement(
            DrawingNs + "r",
            new XElement(DrawingNs + "rPr", new XAttribute("i", "1")),
            new XElement(DrawingNs + "t", "World"));

        var result = PptxDocument.ParseRun(r);

        Assert.Equal("World", result.Text);
        Assert.NotNull(result.RawRPr);
    }

    #endregion

    #region ResolveEffectiveRunProperties - attribute-level inheritance, each tier in isolation

    private static PptxTextRun Run(XElement? rPr, string text = "x") => new(rPr, text);

    private static PptxParagraph Paragraph(XElement? pPr, params PptxTextRun[] runs) =>
        new(PptxDocument.ParseParagraphProperties(pPr), runs.Select(run => (PptxParagraphItem)new PptxRunItem(run)).ToList());

    /// <summary>Resolve Effective Run Properties Run Override Wins Over Every Other Tier.</summary>
    [Fact]
    public void ResolveEffectiveRunProperties_RunOverride_WinsOverEveryOtherTier()
    {
        var theme = BuildTestTheme();
        var run = Run(new XElement(
            DrawingNs + "rPr",
            new XAttribute("sz", "3600"),
            new XAttribute("b", "1"),
            new XElement(DrawingNs + "latin", new XAttribute("typeface", "RunFont"))));
        var paragraph = Paragraph(null, run);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "body");

        Assert.Equal("RunFont", result.FontFamily);
        Assert.Equal(3600f * 127f, result.SizeEmu);
        Assert.True(result.Bold);
    }

    /// <summary>Resolve Effective Run Properties Paragraph Def R Pr Only Wins When Run Declares Nothing.</summary>
    [Fact]
    public void ResolveEffectiveRunProperties_ParagraphDefRPrOnly_WinsWhenRunDeclaresNothing()
    {
        var theme = BuildTestTheme();
        var run = Run(null);
        var pPr = new XElement(
            DrawingNs + "pPr",
            new XElement(DrawingNs + "defRPr", new XAttribute("sz", "2000"), new XAttribute("i", "1")));
        var paragraph = Paragraph(pPr, run);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "body");

        Assert.Equal(2000f * 127f, result.SizeEmu);
        Assert.True(result.Italic);
    }

    /// <summary>Resolve Effective Run Properties Placeholder Level Style Only Wins When Run And Paragraph Declare Nothing.</summary>
    [Fact]
    public void ResolveEffectiveRunProperties_PlaceholderLevelStyleOnly_WinsWhenRunAndParagraphDeclareNothing()
    {
        var theme = BuildTestTheme();
        var run = Run(null);
        var paragraph = Paragraph(null, run); // level 0
        var lstStyle = new XElement(
            DrawingNs + "lstStyle",
            new XElement(DrawingNs + "lvl1pPr", new XElement(DrawingNs + "defRPr", new XAttribute("sz", "1400"))));
        var placeholderProperties = new PptxPlaceholderProperties(null, lstStyle, theme);

        var result = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "body");

        Assert.Equal(1400f * 127f, result.SizeEmu);
    }

    /// <summary>Resolve Effective Run Properties Master Tx Styles Only Wins When Everything Above Is Absent.</summary>
    [Fact]
    public void ResolveEffectiveRunProperties_MasterTxStylesOnly_WinsWhenEverythingAboveIsAbsent()
    {
        var theme = BuildTestTheme();
        var run = Run(null);
        var paragraph = Paragraph(null, run); // level 0
        var bodyStyle = new XElement(
            DrawingNs + "bodyStyle",
            new XElement(DrawingNs + "lvl1pPr", new XElement(DrawingNs + "defRPr", new XAttribute("sz", "3300"))));
        var masterTextStyles = new PptxMasterTextStyles(null, bodyStyle, null);
        var placeholderProperties = EmptyPlaceholderProperties(theme, masterTextStyles);

        var result = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "body");

        Assert.Equal(3300f * 127f, result.SizeEmu);
    }

    /// <summary>Resolve Effective Run Properties Master Title Style Selected For Title Placeholder Type.</summary>
    [Fact]
    public void ResolveEffectiveRunProperties_MasterTitleStyle_SelectedForTitlePlaceholderType()
    {
        var theme = BuildTestTheme();
        var run = Run(null);
        var paragraph = Paragraph(null, run);
        var titleStyle = new XElement(
            DrawingNs + "titleStyle",
            new XElement(DrawingNs + "lvl1pPr", new XElement(DrawingNs + "defRPr", new XAttribute("sz", "4400"))));
        var bodyStyle = new XElement(
            DrawingNs + "bodyStyle",
            new XElement(DrawingNs + "lvl1pPr", new XElement(DrawingNs + "defRPr", new XAttribute("sz", "1800"))));
        var masterTextStyles = new PptxMasterTextStyles(titleStyle, bodyStyle, null);
        var placeholderProperties = EmptyPlaceholderProperties(theme, masterTextStyles);

        var result = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "title");

        Assert.Equal(4400f * 127f, result.SizeEmu);
    }

    /// <summary>Resolve Effective Run Properties Master Other Style Selected For Non Placeholder Shape.</summary>
    [Fact]
    public void ResolveEffectiveRunProperties_MasterOtherStyle_SelectedForNonPlaceholderShape()
    {
        var theme = BuildTestTheme();
        var run = Run(null);
        var paragraph = Paragraph(null, run);
        var otherStyle = new XElement(
            DrawingNs + "otherStyle",
            new XElement(DrawingNs + "lvl1pPr", new XElement(DrawingNs + "defRPr", new XAttribute("sz", "1200"))));
        var masterTextStyles = new PptxMasterTextStyles(null, null, otherStyle);
        var placeholderProperties = EmptyPlaceholderProperties(theme, masterTextStyles);

        var result = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, string.Empty);

        Assert.Equal(1200f * 127f, result.SizeEmu);
    }

    /// <summary>Resolve Effective Run Properties None Declared Resolves Hard Coded Defaults.</summary>
    [Fact]
    public void ResolveEffectiveRunProperties_NoneDeclared_ResolvesHardCodedDefaults()
    {
        var theme = BuildTestTheme();
        var run = Run(null);
        var paragraph = Paragraph(null, run);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var bodyResult = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "body");
        var titleResult = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "title");

        Assert.Equal("ThemeMinorLatin", bodyResult.FontFamily);
        Assert.Equal("ThemeMajorLatin", titleResult.FontFamily);
        Assert.Equal(18f * 12700f, bodyResult.SizeEmu);
        Assert.False(bodyResult.Bold);
        Assert.False(bodyResult.Italic);
        Assert.Equal(PptxUnderlineStyle.None, bodyResult.UnderlineStyle);
        Assert.Equal(theme.ColorScheme.Dark1, bodyResult.Color);
    }

    /// <summary>Resolve Effective Run Properties Underline Attribute Resolves Expected Style.</summary>
    [Theory]
    [InlineData("sng", "Single")]
    [InlineData("none", "None")]
    [InlineData("dbl", "Double")]
    [InlineData("wavy", "Other")]
    [InlineData("heavy", "Other")]
    public void ResolveEffectiveRunProperties_UnderlineAttribute_ResolvesExpectedStyle(string u, string expectedStyleName)
    {
        var theme = BuildTestTheme();
        var run = Run(new XElement(DrawingNs + "rPr", new XAttribute("u", u)));
        var paragraph = Paragraph(null, run);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "body");

        Assert.Equal(expectedStyleName, result.UnderlineStyle.ToString());
    }

    /// <summary>Resolve Effective Run Properties - Explicit uFill Overrides The Run's Own Text Color.</summary>
    [Fact]
    public void ResolveEffectiveRunProperties_ExplicitUFill_OverridesTextColor()
    {
        var theme = BuildTestTheme();
        var run = Run(new XElement(
            DrawingNs + "rPr",
            new XAttribute("u", "sng"),
            new XElement(DrawingNs + "solidFill", new XElement(DrawingNs + "srgbClr", new XAttribute("val", "FF0000"))),
            new XElement(
                DrawingNs + "uFill",
                new XElement(DrawingNs + "solidFill", new XElement(DrawingNs + "srgbClr", new XAttribute("val", "00FF00"))))));
        var paragraph = Paragraph(null, run);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "body");

        Assert.Equal(new Rgba32(255, 0, 0, 255), result.Color);
        Assert.Equal(new Rgba32(0, 255, 0, 255), result.UnderlineColor);
    }

    /// <summary>Resolve Effective Run Properties - No uFill/uFillTx - Defaults Underline Color To The Run's Own Text Color.</summary>
    [Fact]
    public void ResolveEffectiveRunProperties_NoUnderlineFillMarkup_DefaultsUnderlineColorToTextColor()
    {
        var theme = BuildTestTheme();
        var run = Run(new XElement(
            DrawingNs + "rPr",
            new XAttribute("u", "sng"),
            new XElement(DrawingNs + "solidFill", new XElement(DrawingNs + "srgbClr", new XAttribute("val", "FF0000")))));
        var paragraph = Paragraph(null, run);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "body");

        Assert.Equal(new Rgba32(255, 0, 0, 255), result.UnderlineColor);
    }

    /// <summary>Resolve Effective Run Properties - Explicit uFillTx - Also Defaults Underline Color To The Run's Own Text Color (not "no color").</summary>
    [Fact]
    public void ResolveEffectiveRunProperties_ExplicitUFillTx_DefaultsUnderlineColorToTextColor()
    {
        var theme = BuildTestTheme();
        var run = Run(new XElement(
            DrawingNs + "rPr",
            new XAttribute("u", "sng"),
            new XElement(DrawingNs + "solidFill", new XElement(DrawingNs + "srgbClr", new XAttribute("val", "FF0000"))),
            new XElement(DrawingNs + "uFillTx")));
        var paragraph = Paragraph(null, run);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "body");

        Assert.Equal(new Rgba32(255, 0, 0, 255), result.UnderlineColor);
    }

    /// <summary>Resolve Effective Run Properties Run Solid Fill Srgb Clr Resolves Explicit Color.</summary>
    [Fact]
    public void ResolveEffectiveRunProperties_RunSolidFillSrgbClr_ResolvesExplicitColor()
    {
        var theme = BuildTestTheme();
        var run = Run(new XElement(
            DrawingNs + "rPr",
            new XElement(DrawingNs + "solidFill", new XElement(DrawingNs + "srgbClr", new XAttribute("val", "FF0000")))));
        var paragraph = Paragraph(null, run);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "body");

        Assert.Equal(new Rgba32(255, 0, 0, 255), result.Color);
    }

    /// <summary>
    ///     Resolve Effective Run Properties - Theme Font Tokens - Resolve Through The Theme's
    ///     FontScheme (rather than being passed through as literal, unresolvable family name
    ///     strings, which would miss the theme's actual font and fall back to the font
    ///     resolver's own default).
    /// </summary>
    [Theory]
    [InlineData("+mj-lt", "ThemeMajorLatin")]
    [InlineData("+mn-lt", "ThemeMinorLatin")]
    [InlineData("+mj-ea", "MajorEA")]
    [InlineData("+mn-ea", "MinorEA")]
    [InlineData("+mj-cs", "MajorCS")]
    [InlineData("+mn-cs", "MinorCS")]
    public void ResolveEffectiveRunProperties_ThemeFontToken_ResolvesThroughFontScheme(string token, string expectedFamily)
    {
        var theme = BuildTestTheme();
        var run = Run(new XElement(DrawingNs + "rPr", new XElement(DrawingNs + "latin", new XAttribute("typeface", token))));
        var paragraph = Paragraph(null, run);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "body");

        Assert.Equal(expectedFamily, result.FontFamily);
    }

    /// <summary>
    ///     Resolve Effective Run Properties - Literal Typeface - Passes Through Unchanged (only
    ///     the reserved <c>+mj-*</c>/<c>+mn-*</c> theme-font tokens are resolved through the
    ///     theme; every other typeface string is a literal family name).
    /// </summary>
    [Fact]
    public void ResolveEffectiveRunProperties_LiteralTypeface_PassesThroughUnchanged()
    {
        var theme = BuildTestTheme();
        var run = Run(new XElement(DrawingNs + "rPr", new XElement(DrawingNs + "latin", new XAttribute("typeface", "Calibri"))));
        var paragraph = Paragraph(null, run);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, "body");

        Assert.Equal("Calibri", result.FontFamily);
    }

    #endregion

    #region ResolveEffectiveParagraphProperties

    /// <summary>Resolve Effective Paragraph Properties None Declared Resolves Hard Coded Defaults.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_NoneDeclared_ResolvesHardCodedDefaults()
    {
        var theme = BuildTestTheme();
        var paragraph = Paragraph(null);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal("l", result.Alignment);
        Assert.Equal(0f, result.MarginLeftEmu);
        Assert.Equal(0f, result.IndentEmu);
        Assert.Equal(1.0f, result.LineSpacing.Percent);
        Assert.Null(result.LineSpacing.FixedEmu);
    }

    /// <summary>Resolve Effective Paragraph Properties Alignment Normalizes Justification To Left.</summary>
    [Theory]
    [InlineData("l", "l")]
    [InlineData("ctr", "ctr")]
    [InlineData("r", "r")]
    [InlineData("just", "l")]
    public void ResolveEffectiveParagraphProperties_Alignment_NormalizesJustificationToLeft(string algn, string expected)
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(DrawingNs + "pPr", new XAttribute("algn", algn));
        var paragraph = Paragraph(pPr);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal(expected, result.Alignment);
    }

    /// <summary>Resolve Effective Paragraph Properties Fixed Line Spacing Resolves Points To Emu.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_FixedLineSpacing_ResolvesPointsToEmu()
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(
            DrawingNs + "pPr",
            new XElement(DrawingNs + "lnSpc", new XElement(DrawingNs + "spcPts", new XAttribute("val", "2400"))));
        var paragraph = Paragraph(pPr);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Null(result.LineSpacing.Percent);
        Assert.Equal(2400f * 127f, result.LineSpacing.FixedEmu);
    }

    /// <summary>Resolve Effective Paragraph Properties Placeholder Level Style Only Wins When Paragraph Declares Nothing.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_PlaceholderLevelStyleOnly_WinsWhenParagraphDeclaresNothing()
    {
        var theme = BuildTestTheme();
        var paragraph = Paragraph(null);
        var lstStyle = new XElement(
            DrawingNs + "lstStyle",
            new XElement(DrawingNs + "lvl1pPr", new XAttribute("algn", "r"), new XAttribute("marL", "500")));
        var placeholderProperties = new PptxPlaceholderProperties(null, lstStyle, theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal("r", result.Alignment);
        Assert.Equal(500f, result.MarginLeftEmu);
    }

    /// <summary>Resolve Effective Paragraph Properties Master Tx Styles Only Wins When Everything Above Is Absent.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_MasterTxStylesOnly_WinsWhenEverythingAboveIsAbsent()
    {
        var theme = BuildTestTheme();
        var paragraph = Paragraph(null);
        var bodyStyle = new XElement(
            DrawingNs + "bodyStyle",
            new XElement(DrawingNs + "lvl1pPr", new XAttribute("algn", "ctr"), new XAttribute("indent", "-100")));
        var masterTextStyles = new PptxMasterTextStyles(null, bodyStyle, null);
        var placeholderProperties = EmptyPlaceholderProperties(theme, masterTextStyles);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal("ctr", result.Alignment);
        Assert.Equal(-100f, result.IndentEmu);
    }

    /// <summary>
    ///     Proves a paragraph's own <c>&lt;a:spcBef&gt;</c>/<c>&lt;a:spcAft&gt;</c> fixed
    ///     (<c>spcPts</c>) spacing resolves to the correct EMU value (Phase 2 Follow-Up:
    ///     Paragraph Spacing) - previously these elements were parsed into
    ///     <see cref="PptxRawParagraphProperties"/> but never resolved into the effective
    ///     paragraph properties at all.
    /// </summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_SpcBefSpcAftPoints_ResolvesFixedEmu()
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(
            DrawingNs + "pPr",
            new XElement(DrawingNs + "spcBef", new XElement(DrawingNs + "spcPts", new XAttribute("val", "600"))),
            new XElement(DrawingNs + "spcAft", new XElement(DrawingNs + "spcPts", new XAttribute("val", "600"))));
        var paragraph = Paragraph(pPr);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Null(result.EffectiveSpaceBefore.Percent);
        Assert.Equal(600f * 127f, result.EffectiveSpaceBefore.FixedEmu);
        Assert.Null(result.EffectiveSpaceAfter.Percent);
        Assert.Equal(600f * 127f, result.EffectiveSpaceAfter.FixedEmu);
    }

    /// <summary>
    ///     Proves a paragraph's own <c>&lt;a:spcBef&gt;</c>/<c>&lt;a:spcAft&gt;</c> percentage
    ///     (<c>spcPct</c>) spacing resolves to the correct fraction (Phase 2 Follow-Up: Paragraph
    ///     Spacing).
    /// </summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_SpcBefSpcAftPercent_ResolvesPercent()
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(
            DrawingNs + "pPr",
            new XElement(DrawingNs + "spcBef", new XElement(DrawingNs + "spcPct", new XAttribute("val", "50000"))),
            new XElement(DrawingNs + "spcAft", new XElement(DrawingNs + "spcPct", new XAttribute("val", "50000"))));
        var paragraph = Paragraph(pPr);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal(0.5f, result.EffectiveSpaceBefore.Percent);
        Assert.Null(result.EffectiveSpaceBefore.FixedEmu);
        Assert.Equal(0.5f, result.EffectiveSpaceAfter.Percent);
        Assert.Null(result.EffectiveSpaceAfter.FixedEmu);
    }

    /// <summary>
    ///     Proves a paragraph that never declares <c>&lt;a:spcBef&gt;</c>/<c>&lt;a:spcAft&gt;</c>
    ///     at any inheritance tier resolves to the "zero extra gap" sentinel, not
    ///     <c>LineSpacing</c>'s own "100% of line height" default - regression guard for every
    ///     pre-existing paragraph that never declared spacing.
    /// </summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_NoSpcBefSpcAft_ResolvesToNone()
    {
        var theme = BuildTestTheme();
        var paragraph = Paragraph(null);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal(0f, result.EffectiveSpaceBefore.Percent);
        Assert.Null(result.EffectiveSpaceBefore.FixedEmu);
        Assert.Equal(0f, result.EffectiveSpaceAfter.Percent);
        Assert.Null(result.EffectiveSpaceAfter.FixedEmu);
    }

    #endregion

    #region GetMaster <p:txStyles> parsing (Phase 1d plumbing)

    /// <summary>Proves <c>GetMaster</c> parses a master's own <c>&lt;p:txStyles&gt;</c> sibling of <c>&lt;p:cSld&gt;</c> into a <see cref="PptxMasterTextStyles"/>.</summary>
    [Fact]
    public void GetMaster_TxStylesSiblingOfCSld_ParsesAllThreeStyles()
    {
        using var stream = BuildMasterPackageWithTxStyles();
        using var document = PptxDocument.Open(stream);

        var master = document.GetMaster("ppt/slideMasters/slideMaster1.xml");

        Assert.NotNull(master.TxStyles.TitleStyle);
        Assert.NotNull(master.TxStyles.BodyStyle);
        Assert.NotNull(master.TxStyles.OtherStyle);
    }

    private static Stream BuildMasterPackageWithTxStyles()
    {
        const string contentTypesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
            </Types>
            """;

        const string packageRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml" />
            </Relationships>
            """;

        const string presentationXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:sldSz cx="9144000" cy="6858000" />
              <p:sldIdLst>
                <p:sldId id="256" r:id="rId2" />
              </p:sldIdLst>
            </p:presentation>
            """;

        const string presentationRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="slideMasters/slideMaster1.xml" />
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide1.xml" />
            </Relationships>
            """;

        const string masterXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <p:sldMaster xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:cSld>
                <p:spTree />
              </p:cSld>
              <p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2"/>
              <p:txStyles>
                <p:titleStyle><a:lvl1pPr/></p:titleStyle>
                <p:bodyStyle><a:lvl1pPr/></p:bodyStyle>
                <p:otherStyle><a:lvl1pPr/></p:otherStyle>
              </p:txStyles>
            </p:sldMaster>
            """;

        const string masterRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="../theme/theme1.xml" />
            </Relationships>
            """;

        const string themeXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Test">
              <a:themeElements>
                <a:clrScheme name="Test">
                  <a:dk1><a:sysClr val="windowText" lastClr="000000" /></a:dk1>
                  <a:lt1><a:sysClr val="window" lastClr="FFFFFF" /></a:lt1>
                  <a:dk2><a:srgbClr val="1F497D" /></a:dk2>
                  <a:lt2><a:srgbClr val="EEECE1" /></a:lt2>
                  <a:accent1><a:srgbClr val="4F81BD" /></a:accent1>
                  <a:accent2><a:srgbClr val="C0504D" /></a:accent2>
                  <a:accent3><a:srgbClr val="9BBB59" /></a:accent3>
                  <a:accent4><a:srgbClr val="8064A2" /></a:accent4>
                  <a:accent5><a:srgbClr val="4BACC6" /></a:accent5>
                  <a:accent6><a:srgbClr val="F79646" /></a:accent6>
                  <a:hlink><a:srgbClr val="0000FF" /></a:hlink>
                  <a:folHlink><a:srgbClr val="800080" /></a:folHlink>
                </a:clrScheme>
                <a:fontScheme name="Test">
                  <a:majorFont><a:latin typeface="Calibri Light" /><a:ea typeface="" /><a:cs typeface="" /></a:majorFont>
                  <a:minorFont><a:latin typeface="Calibri" /><a:ea typeface="" /><a:cs typeface="" /></a:minorFont>
                </a:fontScheme>
              </a:themeElements>
            </a:theme>
            """;

        const string slideXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
              <p:cSld>
                <p:spTree />
              </p:cSld>
            </p:sld>
            """;

        const string slideRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml" />
            </Relationships>
            """;

        const string layoutXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <p:sldLayout xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
              <p:cSld>
                <p:spTree />
              </p:cSld>
            </p:sldLayout>
            """;

        const string layoutRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="../slideMasters/slideMaster1.xml" />
            </Relationships>
            """;

        var memoryStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteEntry(archive, "_rels/.rels", packageRelsXml);
            WriteEntry(archive, "ppt/presentation.xml", presentationXml);
            WriteEntry(archive, "ppt/_rels/presentation.xml.rels", presentationRelsXml);
            WriteEntry(archive, "ppt/slideMasters/slideMaster1.xml", masterXml);
            WriteEntry(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", masterRelsXml);
            WriteEntry(archive, "ppt/theme/theme1.xml", themeXml);
            WriteEntry(archive, "ppt/slides/slide1.xml", slideXml);
            WriteEntry(archive, "ppt/slides/_rels/slide1.xml.rels", slideRelsXml);
            WriteEntry(archive, "ppt/slideLayouts/slideLayout1.xml", layoutXml);
            WriteEntry(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", layoutRelsXml);
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    private static void WriteEntry(System.IO.Compression.ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    #endregion
}
