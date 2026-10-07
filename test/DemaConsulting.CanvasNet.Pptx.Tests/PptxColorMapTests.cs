using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx clrmap clrmapovr sldmaster sldlayout csld sppr nvpr nvsppr

/// <summary>
///     Unit-level tests for the <c>&lt;p:clrMap&gt;</c>/<c>&lt;p:clrMapOvr&gt;</c> color-map fix:
///     <see cref="PptxDocument.GetMaster"/>'s required <c>&lt;p:clrMap&gt;</c> parsing,
///     <see cref="PptxDocument.GetLayout"/>/<see cref="PptxDocument.GetSlide"/>'s optional
///     <c>&lt;p:clrMapOvr&gt;</c> parsing, and <see cref="PptxDocument.ResolveEffectiveColorMap"/>'s
///     slide &gt; layout &gt; master fallback chain. Complements <see cref="PptxPaintTests"/>'s
///     own <c>ResolveSchemeColor</c>/<c>ResolveColor</c> indirection tests and
///     <see cref="PptxSystemIntegrationTests"/>'s end-to-end rendered-pixel assertion.
/// </summary>
public class PptxColorMapTests
{
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    private const string DefaultContentTypesXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="xml" ContentType="application/xml" />
        </Types>
        """;

    private const string DefaultPackageRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml" />
        </Relationships>
        """;

    private const string DefaultPresentationXml =
        """
        <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <p:sldSz cx="9144000" cy="6858000"/>
          <p:sldIdLst><p:sldId id="256" r:id="rId2"/></p:sldIdLst>
        </p:presentation>
        """;

    private const string DefaultPresentationRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide1.xml" />
        </Relationships>
        """;

    private const string MasterRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="../theme/theme1.xml" />
        </Relationships>
        """;

    private const string LayoutRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="../slideMasters/slideMaster1.xml" />
        </Relationships>
        """;

    private const string SlideRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml" />
        </Relationships>
        """;

    private const string ThemeXml =
        """
        <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="TestTheme">
          <a:themeElements>
            <a:clrScheme name="Test">
              <a:dk1><a:srgbClr val="101010"/></a:dk1>
              <a:lt1><a:srgbClr val="F0F0F0"/></a:lt1>
              <a:dk2><a:srgbClr val="202020"/></a:dk2>
              <a:lt2><a:srgbClr val="E0E0E0"/></a:lt2>
              <a:accent1><a:srgbClr val="111111"/></a:accent1>
              <a:accent2><a:srgbClr val="222222"/></a:accent2>
              <a:accent3><a:srgbClr val="333333"/></a:accent3>
              <a:accent4><a:srgbClr val="444444"/></a:accent4>
              <a:accent5><a:srgbClr val="555555"/></a:accent5>
              <a:accent6><a:srgbClr val="666666"/></a:accent6>
              <a:hlink><a:srgbClr val="777777"/></a:hlink>
              <a:folHlink><a:srgbClr val="888888"/></a:folHlink>
            </a:clrScheme>
            <a:fontScheme name="Test">
              <a:majorFont><a:latin typeface="MajorLatin"/><a:ea typeface="MajorEA"/><a:cs typeface="MajorCS"/></a:majorFont>
              <a:minorFont><a:latin typeface="MinorLatin"/><a:ea typeface="MinorEA"/><a:cs typeface="MinorCS"/></a:minorFont>
            </a:fontScheme>
          </a:themeElements>
        </a:theme>
        """;

    /// <summary>Builds an in-memory, OPC-shaped <c>.pptx</c> ZIP package from name/content entry pairs.</summary>
    private static Stream BuildPackage(params (string Name, string Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>Builds the minimal set of entries needed for <see cref="PptxDocument.Open(Stream)"/> to succeed as a navigable, single-slide presentation.</summary>
    private static (string Name, string Content)[] MinimalOpenableEntries() =>
    [
        ("[Content_Types].xml", DefaultContentTypesXml),
        ("_rels/.rels", DefaultPackageRelsXml),
        ("ppt/presentation.xml", DefaultPresentationXml),
        ("ppt/_rels/presentation.xml.rels", DefaultPresentationRelsXml),
    ];

    /// <summary>Builds a <c>&lt;p:sldMaster&gt;</c> part's content with the given required <c>&lt;p:clrMap&gt;</c> attributes.</summary>
    private static string BuildMasterXml(string bg1 = "lt1", string tx1 = "dk1", string bg2 = "lt2", string tx2 = "dk2") =>
        new XElement(
            P + "sldMaster",
            new XElement(P + "cSld", new XElement(P + "spTree")),
            new XElement(P + "clrMap", new XAttribute("bg1", bg1), new XAttribute("tx1", tx1), new XAttribute("bg2", bg2), new XAttribute("tx2", tx2))).ToString();

    /// <summary>Builds a <c>&lt;p:sldMaster&gt;</c> part's content with no <c>&lt;p:clrMap&gt;</c> element at all.</summary>
    private static string BuildMasterXmlMissingClrMap() =>
        new XElement(P + "sldMaster", new XElement(P + "cSld", new XElement(P + "spTree"))).ToString();

    /// <summary>Builds a <c>&lt;p:sldLayout&gt;</c> part's content, with an optional raw <c>&lt;p:clrMapOvr&gt;</c> child.</summary>
    private static string BuildLayoutXml(XElement? clrMapOvr = null)
    {
        var root = new XElement(P + "sldLayout", new XElement(P + "cSld", new XElement(P + "spTree")));
        if (clrMapOvr is not null)
        {
            root.Add(clrMapOvr);
        }

        return root.ToString();
    }

    /// <summary>Builds a <c>&lt;p:sld&gt;</c> part's content, with an optional raw <c>&lt;p:clrMapOvr&gt;</c> child.</summary>
    private static string BuildSlideXml(XElement? clrMapOvr = null)
    {
        var root = new XElement(P + "sld", new XElement(P + "cSld", new XElement(P + "spTree")));
        if (clrMapOvr is not null)
        {
            root.Add(clrMapOvr);
        }

        return root.ToString();
    }

    /// <summary>Builds a <c>&lt;p:clrMapOvr&gt;</c> element wrapping <c>&lt;a:overrideClrMapping&gt;</c> with the given attributes.</summary>
    private static XElement OverrideClrMapOvr(string bg1, string tx1, string bg2, string tx2) =>
        new(
            P + "clrMapOvr",
            new XElement(A + "overrideClrMapping", new XAttribute("bg1", bg1), new XAttribute("tx1", tx1), new XAttribute("bg2", bg2), new XAttribute("tx2", tx2)));

    /// <summary>Builds a <c>&lt;p:clrMapOvr&gt;</c> element wrapping <c>&lt;a:masterClrMapping/&gt;</c> (the "no override at this tier" sentinel).</summary>
    private static XElement MasterClrMapOvr() => new(P + "clrMapOvr", new XElement(A + "masterClrMapping"));

    // --- GetMaster: required <p:clrMap> parsing ----------------------------------------------

    /// <summary>Proves <see cref="PptxDocument.GetMaster"/> parses a well-formed <c>&lt;p:clrMap&gt;</c> into <see cref="PptxMaster.ColorMap"/>.</summary>
    [Fact]
    public void GetMaster_ParsesRequiredClrMap()
    {
        using var stream = BuildPackage(
            [
                .. MinimalOpenableEntries(),
                ("ppt/slideMasters/slideMaster1.xml", BuildMasterXml(bg1: "dk1", tx1: "lt1", bg2: "dk2", tx2: "lt2")),
                ("ppt/slideMasters/_rels/slideMaster1.xml.rels", MasterRelsXml),
                ("ppt/theme/theme1.xml", ThemeXml),
            ]);
        using var document = PptxDocument.Open(stream);

        var master = document.GetMaster("ppt/slideMasters/slideMaster1.xml");

        Assert.Equal("dk1", master.ColorMap.Bg1);
        Assert.Equal("lt1", master.ColorMap.Tx1);
        Assert.Equal("dk2", master.ColorMap.Bg2);
        Assert.Equal("lt2", master.ColorMap.Tx2);
    }

    /// <summary>Proves <see cref="PptxDocument.GetMaster"/> throws <see cref="InvalidDataException"/> when <c>&lt;p:clrMap&gt;</c> is missing entirely.</summary>
    [Fact]
    public void GetMaster_MissingClrMap_ThrowsInvalidDataException()
    {
        using var stream = BuildPackage(
            [
                .. MinimalOpenableEntries(),
                ("ppt/slideMasters/slideMaster1.xml", BuildMasterXmlMissingClrMap()),
                ("ppt/slideMasters/_rels/slideMaster1.xml.rels", MasterRelsXml),
                ("ppt/theme/theme1.xml", ThemeXml),
            ]);
        using var document = PptxDocument.Open(stream);

        Assert.Throws<InvalidDataException>(() => document.GetMaster("ppt/slideMasters/slideMaster1.xml"));
    }

    // --- GetLayout/GetSlide: optional <p:clrMapOvr> parsing -----------------------------------

    /// <summary>Proves <see cref="PptxDocument.GetLayout"/> leaves <see cref="PptxLayout.ClrMapOvr"/> null when the layout declares no <c>&lt;p:clrMapOvr&gt;</c>.</summary>
    [Fact]
    public void GetLayout_ParsesOptionalClrMapOvr_DefaultsToNullWhenAbsent()
    {
        using var stream = BuildPackage(
            [
                .. MinimalOpenableEntries(),
                ("ppt/slideLayouts/slideLayout1.xml", BuildLayoutXml()),
                ("ppt/slideLayouts/_rels/slideLayout1.xml.rels", LayoutRelsXml),
                ("ppt/slideMasters/slideMaster1.xml", BuildMasterXml()),
                ("ppt/slideMasters/_rels/slideMaster1.xml.rels", MasterRelsXml),
                ("ppt/theme/theme1.xml", ThemeXml),
            ]);
        using var document = PptxDocument.Open(stream);

        var layout = document.GetLayout("ppt/slideLayouts/slideLayout1.xml");

        Assert.Null(layout.ClrMapOvr);
    }

    /// <summary>Proves <see cref="PptxDocument.GetLayout"/> retains a present <c>&lt;p:clrMapOvr&gt;</c> element, unparsed, on <see cref="PptxLayout.ClrMapOvr"/>.</summary>
    [Fact]
    public void GetLayout_ParsesOptionalClrMapOvr_RetainsElementWhenPresent()
    {
        using var stream = BuildPackage(
            [
                .. MinimalOpenableEntries(),
                ("ppt/slideLayouts/slideLayout1.xml", BuildLayoutXml(OverrideClrMapOvr("dk1", "lt1", "dk2", "lt2"))),
                ("ppt/slideLayouts/_rels/slideLayout1.xml.rels", LayoutRelsXml),
                ("ppt/slideMasters/slideMaster1.xml", BuildMasterXml()),
                ("ppt/slideMasters/_rels/slideMaster1.xml.rels", MasterRelsXml),
                ("ppt/theme/theme1.xml", ThemeXml),
            ]);
        using var document = PptxDocument.Open(stream);

        var layout = document.GetLayout("ppt/slideLayouts/slideLayout1.xml");

        Assert.NotNull(layout.ClrMapOvr);
        Assert.Equal(P + "clrMapOvr", layout.ClrMapOvr.Name);
    }

    /// <summary>Proves <see cref="PptxDocument.GetSlide"/> leaves <see cref="PptxSlide.ClrMapOvr"/> null when the slide declares no <c>&lt;p:clrMapOvr&gt;</c>.</summary>
    [Fact]
    public void GetSlide_ParsesOptionalClrMapOvr_DefaultsToNullWhenAbsent()
    {
        using var stream = BuildPackage(
            [
                .. MinimalOpenableEntries(),
                ("ppt/slides/slide1.xml", BuildSlideXml()),
                ("ppt/slides/_rels/slide1.xml.rels", SlideRelsXml),
                ("ppt/slideLayouts/slideLayout1.xml", BuildLayoutXml()),
                ("ppt/slideLayouts/_rels/slideLayout1.xml.rels", LayoutRelsXml),
                ("ppt/slideMasters/slideMaster1.xml", BuildMasterXml()),
                ("ppt/slideMasters/_rels/slideMaster1.xml.rels", MasterRelsXml),
                ("ppt/theme/theme1.xml", ThemeXml),
            ]);
        using var document = PptxDocument.Open(stream);

        var slide = document.GetSlide(0);

        Assert.Null(slide.ClrMapOvr);
    }

    // --- ResolveEffectiveColorMap: slide > layout > master fallback ---------------------------

    /// <summary>Resolve Effective Color Map - No Overrides Anywhere - Returns Master Color Map.</summary>
    [Fact]
    public void ResolveEffectiveColorMap_NoOverridesAnywhere_ReturnsMasterColorMap()
    {
        var masterColorMap = new PptxColorMap("dk1", "lt1", "dk2", "lt2");

        var effective = PptxDocument.ResolveEffectiveColorMap(null, null, masterColorMap);

        Assert.Equal(masterColorMap, effective);
    }

    /// <summary>Resolve Effective Color Map - Layout Override Clr Mapping - Returns Layout Override.</summary>
    [Fact]
    public void ResolveEffectiveColorMap_LayoutOverrideClrMapping_ReturnsLayoutOverride()
    {
        var masterColorMap = PptxColorMap.Default;
        var layoutClrMapOvr = OverrideClrMapOvr("dk1", "lt1", "dk2", "lt2");

        var effective = PptxDocument.ResolveEffectiveColorMap(null, layoutClrMapOvr, masterColorMap);

        Assert.Equal(new PptxColorMap("dk1", "lt1", "dk2", "lt2"), effective);
    }

    /// <summary>Resolve Effective Color Map - Slide Override Clr Mapping - Returns Slide Override Even When Layout Also Overrides.</summary>
    [Fact]
    public void ResolveEffectiveColorMap_SlideOverrideClrMapping_ReturnsSlideOverrideEvenWhenLayoutAlsoOverrides()
    {
        var masterColorMap = PptxColorMap.Default;
        var layoutClrMapOvr = OverrideClrMapOvr("accent1", "accent2", "accent3", "accent4");
        var slideClrMapOvr = OverrideClrMapOvr("dk1", "lt1", "dk2", "lt2");

        var effective = PptxDocument.ResolveEffectiveColorMap(slideClrMapOvr, layoutClrMapOvr, masterColorMap);

        Assert.Equal(new PptxColorMap("dk1", "lt1", "dk2", "lt2"), effective);
    }

    /// <summary>Resolve Effective Color Map - Slide Master Clr Mapping - Falls Through To Layout Override.</summary>
    [Fact]
    public void ResolveEffectiveColorMap_SlideMasterClrMapping_FallsThroughToLayoutOverride()
    {
        var masterColorMap = PptxColorMap.Default;
        var layoutClrMapOvr = OverrideClrMapOvr("dk1", "lt1", "dk2", "lt2");
        var slideClrMapOvr = MasterClrMapOvr();

        var effective = PptxDocument.ResolveEffectiveColorMap(slideClrMapOvr, layoutClrMapOvr, masterColorMap);

        Assert.Equal(new PptxColorMap("dk1", "lt1", "dk2", "lt2"), effective);
    }

    /// <summary>Resolve Effective Color Map - Slide And Layout Master Clr Mapping - Falls Through To Master Color Map.</summary>
    [Fact]
    public void ResolveEffectiveColorMap_SlideAndLayoutMasterClrMapping_FallsThroughToMasterColorMap()
    {
        var masterColorMap = new PptxColorMap("dk1", "lt1", "dk2", "lt2");
        var layoutClrMapOvr = MasterClrMapOvr();
        var slideClrMapOvr = MasterClrMapOvr();

        var effective = PptxDocument.ResolveEffectiveColorMap(slideClrMapOvr, layoutClrMapOvr, masterColorMap);

        Assert.Equal(masterColorMap, effective);
    }

    /// <summary>Resolve Effective Color Map - Override Clr Mapping Missing Required Attribute - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveEffectiveColorMap_OverrideClrMappingMissingRequiredAttribute_ThrowsInvalidDataException()
    {
        var malformed = new XElement(P + "clrMapOvr", new XElement(A + "overrideClrMapping", new XAttribute("bg1", "dk1")));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveEffectiveColorMap(malformed, null, PptxColorMap.Default));
    }

    // --- GetSlide: colorMapResolver threading into table cell fill resolution -----------------

    /// <summary>Builds a single-cell <c>&lt;p:graphicFrame&gt;</c> table whose cell's own fill is <c>&lt;a:solidFill&gt;&lt;a:schemeClr val="bg1"/&gt;&lt;/a:solidFill&gt;</c>.</summary>
    private static XElement BuildSchemeColorTableGraphicFrame() =>
        new(
            P + "graphicFrame",
            new XElement(
                A + "graphic",
                new XElement(
                    A + "graphicData",
                    new XAttribute("uri", "http://schemas.openxmlformats.org/drawingml/2006/table"),
                    new XElement(
                        A + "tbl",
                        new XElement(A + "tblGrid", new XElement(A + "gridCol", new XAttribute("w", 1000))),
                        new XElement(
                            A + "tr",
                            new XAttribute("h", 1000),
                            new XElement(
                                A + "tc",
                                new XElement(
                                    A + "tcPr",
                                    new XElement(A + "solidFill", new XElement(A + "schemeClr", new XAttribute("val", "bg1"))))))))));

    /// <summary>Builds a <c>&lt;p:sld&gt;</c> part's content whose <c>&lt;p:spTree&gt;</c> contains a single scheme-color-filled table, with an optional raw <c>&lt;p:clrMapOvr&gt;</c> child.</summary>
    private static string BuildSlideXmlWithSchemeColorTable(XElement? clrMapOvr = null)
    {
        var root = new XElement(
            P + "sld",
            new XElement(P + "cSld", new XElement(P + "spTree", BuildSchemeColorTableGraphicFrame())));
        if (clrMapOvr is not null)
        {
            root.Add(clrMapOvr);
        }

        return root.ToString();
    }

    /// <summary>
    ///     Proves <see cref="PptxDocument.GetSlide"/> threads this slide's own real effective
    ///     color map (see <see cref="PptxDocument.ResolveEffectiveColorMap"/>) into
    ///     <see cref="PptxDocument.ParseTable"/>, instead of silently defaulting to
    ///     <see cref="PptxColorMap.Default"/> as before this fix: a table cell's own
    ///     <c>&lt;a:schemeClr val="bg1"/&gt;</c> fill resolves against the theme color the
    ///     slide's own <c>&lt;p:clrMapOvr&gt;</c> override maps <c>bg1</c> to (<c>dk1</c>, the
    ///     theme's <c>101010</c> color), not the one <see cref="PptxColorMap.Default"/>'s
    ///     unoverridden <c>bg1</c>-&gt;<c>lt1</c> mapping would have produced (the theme's
    ///     <c>F0F0F0</c> color).
    /// </summary>
    [Fact]
    public void GetSlide_TableCellSchemeColorFillWithSlideClrMapOvr_UsesEffectiveColorMapNotDefault()
    {
        using var stream = BuildPackage(
            [
                .. MinimalOpenableEntries(),
                ("ppt/slides/slide1.xml", BuildSlideXmlWithSchemeColorTable(OverrideClrMapOvr("dk1", "lt1", "dk2", "lt2"))),
                ("ppt/slides/_rels/slide1.xml.rels", SlideRelsXml),
                ("ppt/slideLayouts/slideLayout1.xml", BuildLayoutXml()),
                ("ppt/slideLayouts/_rels/slideLayout1.xml.rels", LayoutRelsXml),
                ("ppt/slideMasters/slideMaster1.xml", BuildMasterXml()),
                ("ppt/slideMasters/_rels/slideMaster1.xml.rels", MasterRelsXml),
                ("ppt/theme/theme1.xml", ThemeXml),
            ]);
        using var document = PptxDocument.Open(stream);

        var slide = document.GetSlide(0);

        var graphicFrameNode = Assert.IsType<PptxGraphicFrameShapeNode>(Assert.Single(slide.ShapeTree));
        Assert.NotNull(graphicFrameNode.Table);
        var cellFill = Assert.IsType<PptxSolidFill>(graphicFrameNode.Table.Rows[0].Cells[0].Fill);
        Assert.Equal(new Rgba32(0x10, 0x10, 0x10, 255), cellFill.Color);
    }
}
