using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore xfrm flipH flipV prstGeom pptx prst lnref prstdash headend tailend cxnsp stealth sysdash sysdot pythonpptx

/// <summary>
///     Unit-level tests for the Phase 2 Follow-Up connector-shape-rendering building blocks:
///     <see cref="PptxPresetGeometry"/>'s connector-only preset builders (<c>line</c>/
///     <c>straightConnector1</c>/<c>bentConnectorN</c>/<c>curvedConnectorN</c>), the connector
///     line-style merge resolver (<see cref="PptxDocument.ResolveConnectorLineStyle"/>), arrowhead
///     resolution (<see cref="PptxDocument.ResolveArrowhead"/>), arrowhead geometry
///     (<see cref="PptxArrowheadGeometry"/>), and the endpoint/tangent helper
///     (<see cref="PptxDocument.ComputeEndpointsAndTangents"/>). End-to-end, render-based proof
///     that a connector actually paints visible pixels lives in <see cref="PptxRenderTests"/> and
///     <see cref="PptxFixturesCorpusTests"/>.
/// </summary>
public class PptxConnectorTests
{
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    /// <summary>Asserts two <see cref="Vector2"/> values are equal within a small tolerance.</summary>
    private static void AssertVectorsClose(Vector2 expected, Vector2 actual, float tolerance = 0.01f)
    {
        Assert.True(
            MathF.Abs(expected.X - actual.X) <= tolerance && MathF.Abs(expected.Y - actual.Y) <= tolerance,
            $"Expected {expected} but was {actual}.");
    }

    // --- PptxPresetGeometry: straight connector -------------------------------------------------

    /// <summary>Straight connector preset builds an open diagonal from (0,0) to (w,h).</summary>
    [Theory]
    [InlineData("line")]
    [InlineData("straightConnector1")]
    public void PptxPresetGeometry_Build_StraightConnectorPresets_ProducesOpenDiagonal(string prst)
    {
        var path = PptxPresetGeometry.Build(prst, 100, 50);

        var subpath = Assert.Single(path.Subpaths);
        Assert.False(subpath.IsClosed);
        AssertVectorsClose(Vector2.Zero, subpath.Start);
        var onlyCommand = Assert.Single(subpath.Commands);
        Assert.Equal(PathCommandType.LineTo, onlyCommand.Type);
        AssertVectorsClose(new Vector2(100, 50), onlyCommand.EndPoint);
    }

    /// <summary>
    ///     Proves the critical zero-height/zero-width connector bug fix: unlike every ordinary
    ///     (non-connector) preset, a connector preset still builds a real, non-empty path when
    ///     its own width or height is zero - confirmed necessary by the real
    ///     <c>pythonpptx-shp-connector-props.pptx</c> fixture's own second slide connector
    ///     (<c>&lt;a:ext cx="1440160" cy="0"/&gt;</c>).
    /// </summary>
    [Theory]
    [InlineData(100f, 0f)]
    [InlineData(0f, 100f)]
    [InlineData(0f, 0f)]
    public void PptxPresetGeometry_Build_StraightConnectorWithZeroWidthOrHeight_StillProducesLine(float w, float h)
    {
        var path = PptxPresetGeometry.Build("straightConnector1", w, h);

        var subpath = Assert.Single(path.Subpaths);
        AssertVectorsClose(new Vector2(w, h), subpath.Commands[0].EndPoint);
    }

    /// <summary>Confirms an ordinary (non-connector) preset is unaffected by the connector zero-size bypass - it still degrades to <see cref="Path.Empty"/>.</summary>
    [Fact]
    public void PptxPresetGeometry_Build_OrdinaryPresetWithZeroHeight_StillDegradesToEmpty()
    {
        Assert.Same(Path.Empty, PptxPresetGeometry.Build("rect", 100, 0));
    }

    // --- PptxPresetGeometry: bent/curved connectors ---------------------------------------------

    /// <summary>Every supported bent-connector preset reaches exactly (w,h) as its own final vertex, as an open polyline.</summary>
    [Theory]
    [InlineData("bentConnector2")]
    [InlineData("bentConnector3")]
    [InlineData("bentConnector4")]
    [InlineData("bentConnector5")]
    public void PptxPresetGeometry_Build_BentConnectorPresets_ReachesEndpointAsOpenPolyline(string prst)
    {
        var path = PptxPresetGeometry.Build(prst, 200, 120);

        var subpath = Assert.Single(path.Subpaths);
        Assert.False(subpath.IsClosed);
        Assert.NotEmpty(subpath.Commands);
        AssertVectorsClose(new Vector2(200, 120), subpath.Commands[^1].EndPoint);
    }

    /// <summary>Every supported curved-connector preset reaches exactly (w,h) as its own final vertex (corner-rounding never moves the true endpoints).</summary>
    [Theory]
    [InlineData("curvedConnector2")]
    [InlineData("curvedConnector3")]
    [InlineData("curvedConnector4")]
    [InlineData("curvedConnector5")]
    public void PptxPresetGeometry_Build_CurvedConnectorPresets_ReachesEndpoint(string prst)
    {
        var path = PptxPresetGeometry.Build(prst, 200, 120);

        var subpath = Assert.Single(path.Subpaths);
        Assert.False(subpath.IsClosed);
        Assert.NotEmpty(subpath.Commands);
        AssertVectorsClose(new Vector2(0, 0), subpath.Start);
        AssertVectorsClose(new Vector2(200, 120), subpath.Commands[^1].EndPoint);
    }

    // --- Flip combinations (straight connector + ResolveShapeFrame) -----------------------------

    /// <summary>
    ///     A straight connector's own absolute endpoints, after composing
    ///     <see cref="PptxDocument.ResolveShapeFrame"/>'s own transform (which bakes in
    ///     <c>flipH</c>/<c>flipV</c>) with the connector's local geometry, land where PowerPoint's
    ///     own documented flip-about-center semantics place them - covering every flip combination
    ///     (neither/flipH only/flipV only/both).
    /// </summary>
    [Theory]
    [InlineData(false, false, 0, 0, 100, 50)]
    [InlineData(true, false, 100, 0, 0, 50)]
    [InlineData(false, true, 0, 50, 100, 0)]
    [InlineData(true, true, 100, 50, 0, 0)]
    public void ResolveShapeFrame_StraightConnectorWithFlipCombination_MapsEndpointsCorrectly(
        bool flipH, bool flipV, float expectedStartX, float expectedStartY, float expectedEndX, float expectedEndY)
    {
        var xfrm = new XElement(
            A + "xfrm",
            new XAttribute("flipH", flipH ? "1" : "0"),
            new XAttribute("flipV", flipV ? "1" : "0"),
            new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
            new XElement(A + "ext", new XAttribute("cx", 100), new XAttribute("cy", 50)));

        var frame = PptxDocument.ResolveShapeFrame(xfrm);
        var localPath = PptxPresetGeometry.Build("straightConnector1", frame.WidthEmu, frame.HeightEmu);
        var transformed = localPath.Transform(frame.Transform);

        var subpath = Assert.Single(transformed.Subpaths);
        AssertVectorsClose(new Vector2(expectedStartX, expectedStartY), subpath.Start);
        AssertVectorsClose(new Vector2(expectedEndX, expectedEndY), subpath.Commands[0].EndPoint);
    }

    // --- ResolveConnectorLineStyle: merge-resolver precedence -----------------------------------

    /// <summary>Builds a minimal <c>&lt;p:spPr&gt;</c> element with the given raw <c>&lt;a:ln&gt;</c> inner XML (or none).</summary>
    private static XElement BuildSpPr(string lnInnerXml = "")
    {
        var spPr = XElement.Parse(
            $"""<p:spPr xmlns:p="{P.NamespaceName}" xmlns:a="{A.NamespaceName}">{lnInnerXml}</p:spPr>""");
        return spPr;
    }

    /// <summary>Builds an arbitrary, fully-populated test <see cref="PptxTheme"/> with the given <c>&lt;a:ln&gt;</c> entries as its own <c>&lt;a:lnStyleLst&gt;</c> (see <see cref="PptxPaintTests.BuildTestTheme"/> for the matching, pre-existing pattern this mirrors).</summary>
    private static PptxTheme BuildThemeWithLnStyleList(IReadOnlyList<XElement> lnStyleList) =>
        new(
            new PptxColorScheme(
                Dark1: new Rgba32(0x10, 0x10, 0x10, 255),
                Light1: new Rgba32(0xF0, 0xF0, 0xF0, 255),
                Dark2: new Rgba32(0x20, 0x20, 0x20, 255),
                Light2: new Rgba32(0xE0, 0xE0, 0xE0, 255),
                Accent1: new Rgba32(0x4F, 0x81, 0xBD, 255),
                Accent2: new Rgba32(0xC0, 0x50, 0x4D, 255),
                Accent3: new Rgba32(0x9B, 0xBB, 0x59, 255),
                Accent4: new Rgba32(0x80, 0x64, 0xA2, 255),
                Accent5: new Rgba32(0x4B, 0xAC, 0xC6, 255),
                Accent6: new Rgba32(0xF7, 0x96, 0x46, 255),
                Hyperlink: new Rgba32(0x00, 0x00, 0xFF, 255),
                FollowedHyperlink: new Rgba32(0x80, 0x00, 0x80, 255)),
            new PptxFontScheme(
                new PptxFontCollection("MajorLatin", "MajorEA", "MajorCS"),
                new PptxFontCollection("MinorLatin", "MinorEA", "MinorCS")),
            LnStyleList: lnStyleList);

    private static readonly IReadOnlyList<XElement> ThreeEntryLnStyleList =
    [
        new(A + "ln", new XAttribute("w", 9525), new XElement(A + "solidFill", new XElement(A + "schemeClr", new XAttribute("val", "phClr")))),
        new(A + "ln", new XAttribute("w", 25400), new XElement(A + "solidFill", new XElement(A + "schemeClr", new XAttribute("val", "accent1")))),
        new(A + "ln", new XAttribute("w", 38100), new XElement(A + "solidFill", new XElement(A + "schemeClr", new XAttribute("val", "accent2")))),
    ];

    /// <summary>An explicit <c>&lt;a:noFill/&gt;</c> directly on the connector's own <c>&lt;a:ln&gt;</c> always wins, even over a style that would otherwise resolve to a visible stroke.</summary>
    [Fact]
    public void ResolveConnectorLineStyle_OwnLnExplicitNoFill_AlwaysWinsOverStyle()
    {
        var spPr = BuildSpPr("""<a:ln w="50000"><a:noFill/></a:ln>""");
        var styleElement = XElement.Parse(
            $"""<p:style xmlns:p="{P.NamespaceName}" xmlns:a="{A.NamespaceName}"><a:lnRef idx="2"><a:schemeClr val="accent1"/></a:lnRef></p:style>""");
        var theme = BuildThemeWithLnStyleList(ThreeEntryLnStyleList);

        var lineStyle = PptxDocument.ResolveConnectorLineStyle(spPr, styleElement, theme, PptxColorMap.Default);

        Assert.Null(lineStyle);
    }

    /// <summary>When the connector's own <c>&lt;a:ln&gt;</c> declares no <c>w</c>, the merged width falls back to the style's own resolved width.</summary>
    [Fact]
    public void ResolveConnectorLineStyle_OwnLnHasNoWidth_FallsBackToStyleWidth()
    {
        var spPr = BuildSpPr("""<a:ln><a:tailEnd type="arrow"/></a:ln>""");
        var styleElement = XElement.Parse(
            $"""<p:style xmlns:p="{P.NamespaceName}" xmlns:a="{A.NamespaceName}"><a:lnRef idx="2"><a:schemeClr val="accent1"/></a:lnRef></p:style>""");
        var theme = BuildThemeWithLnStyleList(ThreeEntryLnStyleList);

        var lineStyle = PptxDocument.ResolveConnectorLineStyle(spPr, styleElement, theme, PptxColorMap.Default);

        Assert.NotNull(lineStyle);
        Assert.Equal(25400f, lineStyle.WidthEmu);
        var solid = Assert.IsType<PptxSolidFill>(lineStyle.Paint);
        Assert.Equal(new Rgba32(0x4F, 0x81, 0xBD, 255), solid.Color);
    }

    /// <summary>When the connector's own <c>&lt;a:ln&gt;</c> declares a width but no fill child, the merged paint falls back to the style's own resolved paint.</summary>
    [Fact]
    public void ResolveConnectorLineStyle_OwnLnHasWidthButNoFill_FallsBackToStylePaint()
    {
        var spPr = BuildSpPr("""<a:ln w="100000"/>""");
        var styleElement = XElement.Parse(
            $"""<p:style xmlns:p="{P.NamespaceName}" xmlns:a="{A.NamespaceName}"><a:lnRef idx="3"><a:schemeClr val="accent2"/></a:lnRef></p:style>""");
        var theme = BuildThemeWithLnStyleList(ThreeEntryLnStyleList);

        var lineStyle = PptxDocument.ResolveConnectorLineStyle(spPr, styleElement, theme, PptxColorMap.Default);

        Assert.NotNull(lineStyle);
        Assert.Equal(100000f, lineStyle.WidthEmu);
        var solid = Assert.IsType<PptxSolidFill>(lineStyle.Paint);
        Assert.Equal(new Rgba32(0xC0, 0x50, 0x4D, 255), solid.Color);
    }

    /// <summary>When the connector's own <c>&lt;a:ln&gt;</c> explicitly declares both width and fill, those own values win outright over the style.</summary>
    [Fact]
    public void ResolveConnectorLineStyle_OwnLnHasExplicitWidthAndFill_OwnValuesWin()
    {
        var spPr = BuildSpPr("""<a:ln w="12700"><a:solidFill><a:srgbClr val="00FF00"/></a:solidFill></a:ln>""");
        var styleElement = XElement.Parse(
            $"""<p:style xmlns:p="{P.NamespaceName}" xmlns:a="{A.NamespaceName}"><a:lnRef idx="2"><a:schemeClr val="accent1"/></a:lnRef></p:style>""");
        var theme = BuildThemeWithLnStyleList(ThreeEntryLnStyleList);

        var lineStyle = PptxDocument.ResolveConnectorLineStyle(spPr, styleElement, theme, PptxColorMap.Default);

        Assert.NotNull(lineStyle);
        Assert.Equal(12700f, lineStyle.WidthEmu);
        var solid = Assert.IsType<PptxSolidFill>(lineStyle.Paint);
        Assert.Equal(new Rgba32(0x00, 0xFF, 0x00, 255), solid.Color);
    }

    /// <summary>With neither an own <c>&lt;a:ln&gt;</c> width/fill nor any <c>&lt;p:style&gt;</c> at all, the connector resolves to "no stroke".</summary>
    [Fact]
    public void ResolveConnectorLineStyle_NoOwnWidthAndNoStyle_ResolvesToNull()
    {
        var spPr = BuildSpPr("""<a:ln/>""");
        var theme = BuildThemeWithLnStyleList(ThreeEntryLnStyleList);

        var lineStyle = PptxDocument.ResolveConnectorLineStyle(spPr, null, theme, PptxColorMap.Default);

        Assert.Null(lineStyle);
    }

    /// <summary>The connector's own explicit <c>&lt;a:prstDash&gt;</c> is used over the style's own dash pattern.</summary>
    [Fact]
    public void ResolveConnectorLineStyle_OwnLnHasExplicitDash_UsesOwnDashArray()
    {
        var spPr = BuildSpPr(
            """<a:ln w="12700"><a:solidFill><a:srgbClr val="000000"/></a:solidFill><a:prstDash val="dash"/></a:ln>""");

        var lineStyle = PptxDocument.ResolveConnectorLineStyle(spPr, null, BuildThemeWithLnStyleList(ThreeEntryLnStyleList), PptxColorMap.Default);

        Assert.NotNull(lineStyle);
        Assert.NotNull(lineStyle.DashArray);
        Assert.Equal([12700f * 4f, 12700f * 3f], lineStyle.DashArray);
    }

    // --- ResolveArrowhead --------------------------------------------------------------------

    /// <summary>A recognized <c>type</c> attribute value on a <c>&lt;a:tailEnd&gt;</c> element resolves to the matching <see cref="PptxArrowheadKind"/>, carrying through its <c>w</c>/<c>len</c> size keys.</summary>
    [Theory]
    [InlineData("triangle", "Triangle")]
    [InlineData("stealth", "Stealth")]
    [InlineData("diamond", "Diamond")]
    [InlineData("oval", "Oval")]
    [InlineData("arrow", "Arrow")]
    public void ResolveArrowhead_RecognizedTypeValue_ResolvesExpectedKind(string typeValue, string expectedKindName)
    {
        var ln = XElement.Parse(
            $"""<a:ln xmlns:a="{A.NamespaceName}"><a:tailEnd type="{typeValue}" w="lg" len="sm"/></a:ln>""");

        var style = PptxDocument.ResolveArrowhead(ln, "tailEnd");

        Assert.NotNull(style);
        Assert.Equal(expectedKindName, style.Kind.ToString());
        Assert.Equal("lg", style.WidthKey);
        Assert.Equal("sm", style.LengthKey);
    }

    /// <summary>A <see langword="null"/> <c>&lt;a:ln&gt;</c> element (no line properties at all) has no arrowhead to resolve.</summary>
    [Fact]
    public void ResolveArrowhead_NoLnElement_ReturnsNull()
    {
        Assert.Null(PptxDocument.ResolveArrowhead(null, "tailEnd"));
    }

    /// <summary>Resolving the <c>tailEnd</c> element name on an <c>&lt;a:ln&gt;</c> that only declares a <c>&lt;a:headEnd&gt;</c> yields no arrowhead.</summary>
    [Fact]
    public void ResolveArrowhead_NoMatchingEndElement_ReturnsNull()
    {
        var ln = XElement.Parse($"""<a:ln xmlns:a="{A.NamespaceName}"><a:headEnd type="triangle"/></a:ln>""");

        Assert.Null(PptxDocument.ResolveArrowhead(ln, "tailEnd"));
    }

    /// <summary>Both the explicit <c>none</c> type value, and any unrecognized/exotic type value, resolve to no arrowhead at all.</summary>
    [Theory]
    [InlineData("none")]
    [InlineData("unknownExoticType")]
    public void ResolveArrowhead_NoneOrUnrecognizedType_ReturnsNull(string typeValue)
    {
        var ln = XElement.Parse($"""<a:ln xmlns:a="{A.NamespaceName}"><a:tailEnd type="{typeValue}"/></a:ln>""");

        Assert.Null(PptxDocument.ResolveArrowhead(ln, "tailEnd"));
    }

    /// <summary>When an arrowhead's <c>w</c>/<c>len</c> attributes are both absent, both size keys default to <c>"med"</c>.</summary>
    [Fact]
    public void ResolveArrowhead_NoWOrLenAttributes_DefaultsToMed()
    {
        var ln = XElement.Parse($"""<a:ln xmlns:a="{A.NamespaceName}"><a:tailEnd type="triangle"/></a:ln>""");

        var style = PptxDocument.ResolveArrowhead(ln, "tailEnd");

        Assert.NotNull(style);
        Assert.Equal("med", style.WidthKey);
        Assert.Equal("med", style.LengthKey);
    }

    // --- PptxArrowheadGeometry: shape/sizing ----------------------------------------------------

    /// <summary>Every filled arrowhead kind's tip sits at the local origin, with its body extending toward local -x (the caller orients this toward the connector's own direction later).</summary>
    [Theory]
    [InlineData("Triangle")]
    [InlineData("Stealth")]
    [InlineData("Diamond")]
    public void PptxArrowheadGeometry_Build_FilledKinds_TipAtOriginBodyExtendsBackward(string kindName)
    {
        var kind = Enum.Parse<PptxArrowheadKind>(kindName);
        var style = new PptxArrowheadStyle(kind, "med", "med");

        var path = PptxArrowheadGeometry.Build(style, 100000f);

        var subpath = Assert.Single(path.Subpaths);
        Assert.True(subpath.IsClosed);
        var bounds = path.GetBounds();
        Assert.True(bounds.X <= 0.01f, $"{kind}: left bound should be <= 0, was {bounds.X}");
        Assert.True(bounds.X + bounds.Width >= -0.01f, $"{kind}: right bound should reach back to ~0, was {bounds.X + bounds.Width}");
    }

    /// <summary>The "oval" arrowhead kind is also closed and centered around the origin.</summary>
    [Fact]
    public void PptxArrowheadGeometry_Build_Oval_IsClosedAndCenteredNearOrigin()
    {
        var style = new PptxArrowheadStyle(PptxArrowheadKind.Oval, "med", "med");

        var path = PptxArrowheadGeometry.Build(style, 100000f);

        var subpath = Assert.Single(path.Subpaths);
        Assert.True(subpath.IsClosed);
    }

    /// <summary>The "arrow" open-chevron kind is an open (unclosed) two-segment path, meant to be stroked rather than filled.</summary>
    [Fact]
    public void PptxArrowheadGeometry_Build_Arrow_IsOpenChevron()
    {
        var style = new PptxArrowheadStyle(PptxArrowheadKind.Arrow, "med", "med");

        var path = PptxArrowheadGeometry.Build(style, 100000f);

        var subpath = Assert.Single(path.Subpaths);
        Assert.False(subpath.IsClosed);
        Assert.Equal(2, subpath.Commands.Count);
        AssertVectorsClose(Vector2.Zero, subpath.Commands[0].EndPoint);
    }

    /// <summary>A non-positive line width yields an empty arrowhead (nothing meaningful to scale from).</summary>
    [Fact]
    public void PptxArrowheadGeometry_Build_NonPositiveLineWidth_ReturnsEmpty()
    {
        var style = new PptxArrowheadStyle(PptxArrowheadKind.Triangle, "med", "med");

        Assert.Same(Path.Empty, PptxArrowheadGeometry.Build(style, 0f));
        Assert.Same(Path.Empty, PptxArrowheadGeometry.Build(style, -5f));
    }

    /// <summary>A "lg" arrowhead is strictly larger (wider bounding box) than a "sm" one built from the same line width.</summary>
    [Fact]
    public void PptxArrowheadGeometry_Build_SizeKeys_LargeIsBiggerThanSmall()
    {
        var small = PptxArrowheadGeometry.Build(new PptxArrowheadStyle(PptxArrowheadKind.Triangle, "sm", "sm"), 50000f);
        var large = PptxArrowheadGeometry.Build(new PptxArrowheadStyle(PptxArrowheadKind.Triangle, "lg", "lg"), 50000f);

        Assert.True(large.GetBounds().Width > small.GetBounds().Width);
        Assert.True(large.GetBounds().Height > small.GetBounds().Height);
    }

    // --- ComputeEndpointsAndTangents --------------------------------------------------------------

    /// <summary>A horizontal straight connector's start/end points sit at its local corners, and both tangents point along local +X.</summary>
    [Fact]
    public void ComputeEndpointsAndTangents_HorizontalStraightConnector_TangentsPointAlongPositiveX()
    {
        var path = PptxPresetGeometry.Build("straightConnector1", 100f, 0f);

        var (startPoint, startTangent, endPoint, endTangent) = PptxDocument.ComputeEndpointsAndTangents(path);

        AssertVectorsClose(Vector2.Zero, startPoint);
        AssertVectorsClose(new Vector2(100, 0), endPoint);
        AssertVectorsClose(Vector2.UnitX, startTangent);
        AssertVectorsClose(Vector2.UnitX, endTangent);
    }

    /// <summary>A vertical straight connector's start/end points sit at its local corners, and both tangents point along local +Y.</summary>
    [Fact]
    public void ComputeEndpointsAndTangents_VerticalStraightConnector_TangentsPointAlongPositiveY()
    {
        var path = PptxPresetGeometry.Build("straightConnector1", 0f, 80f);

        var (startPoint, startTangent, endPoint, endTangent) = PptxDocument.ComputeEndpointsAndTangents(path);

        AssertVectorsClose(Vector2.Zero, startPoint);
        AssertVectorsClose(new Vector2(0, 80), endPoint);
        AssertVectorsClose(Vector2.UnitY, startTangent);
        AssertVectorsClose(Vector2.UnitY, endTangent);
    }

    /// <summary>A square diagonal straight connector's tangents point at exactly 45 degrees (normalized (1,1)) at both ends.</summary>
    [Fact]
    public void ComputeEndpointsAndTangents_DiagonalStraightConnector_TangentsPointAtFortyFiveDegrees()
    {
        var path = PptxPresetGeometry.Build("straightConnector1", 100f, 100f);

        var (startPoint, startTangent, endPoint, endTangent) = PptxDocument.ComputeEndpointsAndTangents(path);

        AssertVectorsClose(Vector2.Zero, startPoint);
        AssertVectorsClose(new Vector2(100, 100), endPoint);
        var expected = Vector2.Normalize(new Vector2(1, 1));
        AssertVectorsClose(expected, startTangent);
        AssertVectorsClose(expected, endTangent);
    }

    /// <summary>A two-segment bent connector's final segment is vertical, so its end tangent must point along +Y rather than the overall diagonal direction.</summary>
    [Fact]
    public void ComputeEndpointsAndTangents_BentConnector_FinalSegmentTangentPointsAlongLastDirection()
    {
        // bentConnector2 is a single horizontal-then-vertical staircase (2 segments): the final
        // segment is vertical (downward), so the end tangent should point along +Y.
        var path = PptxPresetGeometry.Build("bentConnector2", 100f, 100f);

        var (_, _, endPoint, endTangent) = PptxDocument.ComputeEndpointsAndTangents(path);

        AssertVectorsClose(new Vector2(100, 100), endPoint);
        AssertVectorsClose(Vector2.UnitY, endTangent);
    }
}
