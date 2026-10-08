// cspell:ignore vsdx Visio davehoward Foregnd Tahoma

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for <c>VsdxDocument.TextStyle.cs</c>'s <c>TextStyle</c> StyleSheet-chain
///     resolver: the third axis alongside <see cref="VsdxStyleResolutionTests"/>'s own
///     <c>LineStyle</c>/<c>FillStyle</c> coverage - row-indexed <c>Section N="Character"</c>/
///     <c>"Paragraph"</c> cell resolution, the Master/instance row merge, and the flat
///     <c>VerticalAlign</c>/margin cell chain.
/// </summary>
public class VsdxTextStyleTests
{
    /// <summary>
    ///     A built-in-style-shaped <c>&lt;StyleSheets&gt;</c> block extending
    ///     <see cref="VsdxStyleResolutionTests"/>'s own <c>StyleSheetsXml</c> with
    ///     <c>Section N="Character"</c>/<c>"Paragraph"</c> on ID <c>0</c> and a flat
    ///     <c>VerticalAlign</c>/<c>LeftMargin</c> cell - mirroring
    ///     <c>davehoward-test5-master.vsdx</c>'s real document.xml shape.
    /// </summary>
    private const string StyleSheetsXml =
        """
        <StyleSheets>
          <StyleSheet ID="0" Name="No Style">
            <Cell N="VerticalAlign" V="1"/><Cell N="LeftMargin" V="0.05"/>
            <Section N="Character"><Row IX="0"><Cell N="Font" V="Arial"/><Cell N="Color" V="#000000"/><Cell N="Size" V="0.1667"/></Row></Section>
            <Section N="Paragraph"><Row IX="0"><Cell N="HorzAlign" V="0"/></Row></Section>
          </StyleSheet>
          <StyleSheet ID="1" Name="Text Only" LineStyle="0" FillStyle="0" TextStyle="0">
          </StyleSheet>
          <StyleSheet ID="3" Name="Normal" LineStyle="1" FillStyle="1" TextStyle="1">
          </StyleSheet>
        </StyleSheets>
        """;

    /// <summary>Builds a one-shape package resolving <c>TextStyle="3"</c> with the given <c>&lt;Text&gt;</c>/own-section markup.</summary>
    private static VsdxDocument OpenShape(string textXml, string ownSectionsXml = "", string masterXml = "")
    {
        var shapeXml =
            $"""
            <Shape ID="1" Type="Shape" TextStyle="3" {(masterXml.Length > 0 ? "Master=\"1\"" : string.Empty)}>
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
              {textXml}
              {ownSectionsXml}
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, StyleSheetsXml, masterXml.Length > 0 ? masterXml : null);
        return VsdxDocument.Open(stream);
    }

    /// <summary>
    ///     Proves a shape with <c>TextStyle="3"</c> and no own <c>Section N="Character"</c>
    ///     resolves <c>Font</c>/<c>Color</c>/<c>Size</c> by walking the chain 3 -&gt; 1 -&gt; 0.
    /// </summary>
    [Fact]
    public void TextStyle_ChainWalk_ResolvesCharacterRowThroughMultipleParents()
    {
        // Arrange / Act
        using var document = OpenShape("<Text>Hello</Text>");
        var run = document.GetPageShapes(0)[0].TextRuns![0];

        // Assert
        Assert.Equal("Arial", run.FontFamily);
        Assert.Equal(0, run.Color.R);
        Assert.Equal(0, run.Color.G);
        Assert.Equal(0, run.Color.B);
        Assert.Equal(0.1667, run.SizeInches, 4);
    }

    /// <summary>
    ///     Proves a shape's own direct <c>Section N="Character"</c> overrides only the cells it
    ///     declares (<c>Color</c>), while <c>Font</c>/<c>Size</c> still resolve through the
    ///     <c>TextStyle</c> chain - mirrors <c>davehoward-test12-colors.vsdx</c> Shape 2 exactly.
    /// </summary>
    [Fact]
    public void TextStyle_ShapeOwnCharacterRowOverride_TakesPrecedenceOverChainForThatCellOnly()
    {
        // Arrange / Act
        using var document = OpenShape(
            "<Text><cp IX=\"0\"/>Text Color</Text>",
            """<Section N="Character"><Row IX="0"><Cell N="Color" V="#00ff00"/></Row></Section>""");
        var run = document.GetPageShapes(0)[0].TextRuns![0];

        // Assert: Color overridden, Font/Size still from the chain.
        Assert.Equal(0, run.Color.R);
        Assert.Equal(255, run.Color.G);
        Assert.Equal(0, run.Color.B);
        Assert.Equal("Arial", run.FontFamily);
        Assert.Equal(0.1667, run.SizeInches, 4);
    }

    /// <summary>
    ///     Proves <c>MergeTextSectionRows</c> merges an instance's partial row (one cell) over a
    ///     Master's full row (every cell) cell-by-cell, not whole-row replacement.
    /// </summary>
    [Fact]
    public void TextStyle_MasterInstanceRowMerge_InstanceRowMergesOverMasterRow()
    {
        // Arrange: Master's row 0 declares Font+Color+Size; instance's row 0 declares only Color.
        const string masterXml =
            """
            <Shape ID="2" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
              <Section N="Character"><Row IX="0"><Cell N="Font" V="Tahoma"/><Cell N="Color" V="#0000ff"/><Cell N="Size" V="0.25"/></Row></Section>
            </Shape>
            """;

        using var document = OpenShape(
            "<Text><cp IX=\"0\"/>Hi</Text>",
            """<Section N="Character"><Row IX="0"><Cell N="Color" V="#ff0000"/></Row></Section>""",
            masterXml);
        var run = document.GetPageShapes(0)[0].TextRuns![0];

        // Assert: instance's own Color wins; Master's Font/Size are inherited, not lost.
        Assert.Equal(255, run.Color.R);
        Assert.Equal(0, run.Color.G);
        Assert.Equal(0, run.Color.B);
        Assert.Equal("Tahoma", run.FontFamily);
        Assert.Equal(0.25, run.SizeInches, 4);
    }

    /// <summary>
    ///     Proves <c>VerticalAlign</c>/<c>LeftMargin</c> (flat, non-row-indexed cells) resolve
    ///     through the <c>TextStyle</c> StyleSheet chain via the existing
    ///     <c>ResolveStyleCellValue</c> helper, exactly like <c>LineColor</c>/<c>FillForegnd</c>,
    ///     and that the resolved values actually move the shape's laid-out glyph - not merely
    ///     that <c>TextLayout</c> is non-null, which would also pass if <c>VerticalAlign</c>/
    ///     margins were silently ignored. Builds a tall (3in), wide (3in) shape so the
    ///     <c>StyleSheet</c>-inherited <c>VerticalAlign="1"</c> (Middle)/<c>LeftMargin="0.05"</c>
    ///     defaults and a shape with its own overriding flat <c>VerticalAlign="0"</c> (Top)/
    ///     <c>LeftMargin="0.4"</c> cells produce measurably different glyph origins.
    /// </summary>
    [Fact]
    public void TextStyle_FlatTextStyleCells_ResolveThroughTextStyleParentChainAndAffectGlyphPosition()
    {
        // Arrange: a 3x3in shape so a 2.9in-tall/2.9in-wide content box leaves plenty of room
        // for Top vs Middle vertical anchoring, and a 0.05in vs 0.4in LeftMargin, to diverge
        // measurably rather than rounding away to the same pixel.
        static string ShapeXml(string ownCellsXml) =>
            $"""
            <Shape ID="1" Type="Shape" TextStyle="3">
              <Cell N="PinX" V="2"/><Cell N="PinY" V="2"/><Cell N="Width" V="3"/><Cell N="Height" V="3"/>
              <Cell N="LocPinX" V="1.5"/><Cell N="LocPinY" V="1.5"/><Cell N="Angle" V="0"/>
              {ownCellsXml}
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
              <Text>Hi</Text>
            </Shape>
            """;

        using var defaultDocument = VsdxDocument.Open(VsdxTestPackages.BuildPackage(ShapeXml(string.Empty), StyleSheetsXml));
        using var overriddenDocument = VsdxDocument.Open(
            VsdxTestPackages.BuildPackage(
                ShapeXml("""<Cell N="VerticalAlign" V="0"/><Cell N="LeftMargin" V="0.4"/>"""),
                StyleSheetsXml));

        // Act
        var defaultGlyph = defaultDocument.GetPageShapes(0)[0].TextLayout!.Glyphs[0];
        var overriddenGlyph = overriddenDocument.GetPageShapes(0)[0].TextLayout!.Glyphs[0];

        // Assert: the default shape inherits VerticalAlign="1" (Middle)/LeftMargin="0.05" from
        // the TextStyle="3" -> "1" -> "0" StyleSheet chain; the overridden shape's own
        // VerticalAlign="0" (Top)/LeftMargin="0.4" cells win over that chain for this shape only.
        // Top-anchored text sits higher (larger shape-local, y-up Y) than Middle-anchored text in
        // the same tall box, and the larger LeftMargin pushes the glyph further right - so if
        // either flat cell were silently ignored, these two shapes would resolve to the same
        // glyph origin and this assertion would fail.
        Assert.True(
            overriddenGlyph.OriginYInches > defaultGlyph.OriginYInches,
            $"Expected Top-anchored glyph Y ({overriddenGlyph.OriginYInches}) to exceed Middle-anchored glyph Y ({defaultGlyph.OriginYInches}).");
        Assert.True(
            overriddenGlyph.OriginXInches > defaultGlyph.OriginXInches,
            $"Expected LeftMargin=0.4in glyph X ({overriddenGlyph.OriginXInches}) to exceed LeftMargin=0.05in glyph X ({defaultGlyph.OriginXInches}).");
        Assert.Equal(0.4 - 0.05, overriddenGlyph.OriginXInches - defaultGlyph.OriginXInches, 6);
    }

    /// <summary>
    ///     Proves an unrecognized/undocumented <c>HorzAlign</c> value (<c>"2"</c>/<c>"3"</c>)
    ///     degrades to <see cref="VsdxHorizontalAlign.Left"/> rather than throwing.
    /// </summary>
    [Theory]
    [InlineData("2")]
    [InlineData("3")]
    public void TextStyle_HorzAlignUnrecognizedValue_DegradesToLeftWithoutThrowing(string horzAlignValue)
    {
        // Arrange / Act
        using var document = OpenShape(
            "<Text><pp IX=\"0\"/>Hi</Text>",
            $"""<Section N="Paragraph"><Row IX="0"><Cell N="HorzAlign" V="{horzAlignValue}"/></Row></Section>""");
        var run = document.GetPageShapes(0)[0].TextRuns![0];

        // Assert
        Assert.Equal(VsdxHorizontalAlign.Left, run.Paragraph.Align);
    }
}
