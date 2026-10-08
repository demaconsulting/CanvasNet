// cspell:ignore vsdx Visio

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for <c>VsdxDocument.Arrowheads.cs</c>'s arrowhead resolver, exercising
///     the same three precedence/degradation concerns <c>VsdxStyleResolutionTests</c> already
///     covers for <c>Line*</c>/<c>Fill*</c> cells (StyleSheet chain walk, direct-cell override,
///     and graceful degradation), applied specifically to <c>BeginArrow</c>/<c>EndArrow</c>/
///     <c>BeginArrowSize</c>/<c>EndArrowSize</c>.
/// </summary>
public class VsdxArrowheadResolutionTests
{
    /// <summary>
    ///     A minimal built-in-style-shaped <c>&lt;StyleSheets&gt;</c> block whose ID <c>0</c>
    ///     ("No Style") declares a non-zero <c>EndArrow="2"</c> (Triangle/<see cref="VsdxArrowheadStyle.Arrow"/>)
    ///     with <c>EndArrowSize="1"</c>, and ID <c>1</c> chains to ID <c>0</c> for <c>LineStyle</c>
    ///     with no paint cells of its own - mirroring <c>VsdxStyleResolutionTests</c>'s own chain
    ///     depth.
    /// </summary>
    private const string StyleSheetsXml =
        """
        <StyleSheets>
          <StyleSheet ID="0" Name="No Style">
            <Cell N="LineColor" V="0"/><Cell N="LinePattern" V="1"/>
            <Cell N="BeginArrow" V="0"/><Cell N="BeginArrowSize" V="2"/>
            <Cell N="EndArrow" V="2"/><Cell N="EndArrowSize" V="1"/>
          </StyleSheet>
          <StyleSheet ID="1" Name="Text Only" LineStyle="0">
          </StyleSheet>
        </StyleSheets>
        """;

    /// <summary>Builds a minimal 1-D (connector-shaped) shape's literal markup with the given <c>LineStyle</c> attribute and extra own-cell markup.</summary>
    private static string BuildConnectorShapeXml(string lineStyleAttribute, string extraCellsXml = "") =>
        $"""
        <Shape ID="1" Type="Shape" {lineStyleAttribute}>
          <Cell N="PinX" V="1"/><Cell N="PinY" V="0"/><Cell N="Width" V="2"/><Cell N="Height" V="0"/>
          <Cell N="LocPinX" V="1"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
          <Cell N="BeginX" V="0"/><Cell N="BeginY" V="0"/><Cell N="EndX" V="2"/><Cell N="EndY" V="0"/>
          {extraCellsXml}
          <Section N="Geometry" IX="0">
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="LineTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/></Row>
          </Section>
        </Shape>
        """;

    /// <summary>
    ///     Proves a connector shape with no own <c>BeginArrow</c>/<c>EndArrow</c> cells resolves
    ///     its arrowheads by walking the StyleSheet chain via <c>LineStyle="1"</c> (1 -&gt; 0),
    ///     landing on ID 0's literal <c>BeginArrow="0"</c> (degrades to
    ///     <see cref="VsdxArrowheadStyle.None"/>, hence <see cref="VsdxArrowhead.NoArrowhead"/>)
    ///     and <c>EndArrow="2"</c>/<c>EndArrowSize="1"</c> (<see cref="VsdxArrowheadStyle.Arrow"/>).
    /// </summary>
    [Fact]
    public void ArrowheadResolution_StyleSheetChainWalk_ResolvesThroughParent()
    {
        // Arrange
        var shapeXml = BuildConnectorShapeXml(lineStyleAttribute: """LineStyle="1" """);
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, StyleSheetsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert
        Assert.Equal(VsdxArrowhead.NoArrowhead, paint.BeginArrowhead);
        Assert.Equal(VsdxArrowheadStyle.Arrow, paint.EndArrowhead.Style);
        Assert.Equal(1, paint.EndArrowhead.SizeIndex);
    }

    /// <summary>
    ///     Proves a shape's own direct, literal <c>BeginArrow</c>/<c>BeginArrowSize</c> cells take
    ///     final precedence over the entire StyleSheet chain, even when the chain's own terminal
    ///     StyleSheet declares a different literal <c>BeginArrow</c>.
    /// </summary>
    [Fact]
    public void ArrowheadResolution_DirectShapeOverride_TakesPrecedenceOverStyleSheetChain()
    {
        // Arrange: the shape's own BeginArrow="10" (Circle) overrides StyleSheet ID 0's BeginArrow="0".
        var shapeXml = BuildConnectorShapeXml(
            lineStyleAttribute: """LineStyle="1" """,
            extraCellsXml: """<Cell N="BeginArrow" V="10"/><Cell N="BeginArrowSize" V="3"/>""");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, StyleSheetsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert
        Assert.Equal(VsdxArrowheadStyle.Circle, paint.BeginArrowhead.Style);
        Assert.Equal(3, paint.BeginArrowhead.SizeIndex);
    }

    /// <summary>
    ///     Proves an unrecognized <c>EndArrow</c> index (<c>999</c>, not one of this milestone's
    ///     conservative recognized subset) degrades to <see cref="VsdxArrowheadStyle.None"/> -
    ///     <see cref="VsdxArrowhead.NoArrowhead"/> - without throwing, per the amended
    ///     <c>ArrowheadRendering</c> requirement.
    /// </summary>
    [Fact]
    public void ArrowheadResolution_UnrecognizedIndex_DegradesToNoArrowheadWithoutThrowing()
    {
        // Arrange
        var shapeXml = BuildConnectorShapeXml(
            lineStyleAttribute: string.Empty,
            extraCellsXml: """<Cell N="EndArrow" V="999"/>""");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert
        Assert.Null(exception);
        var paint = document.GetPageShapes(0)[0].Paint!;
        Assert.Equal(VsdxArrowhead.NoArrowhead, paint.EndArrowhead);
    }

    /// <summary>
    ///     Proves a non-numeric <c>BeginArrow</c> cell value (e.g. an unresolved field-reference
    ///     sentinel) also degrades to <see cref="VsdxArrowheadStyle.None"/> without throwing,
    ///     rather than only tolerating out-of-range integers.
    /// </summary>
    [Fact]
    public void ArrowheadResolution_NonNumericValue_DegradesToNoArrowheadWithoutThrowing()
    {
        // Arrange
        var shapeXml = BuildConnectorShapeXml(
            lineStyleAttribute: string.Empty,
            extraCellsXml: """<Cell N="BeginArrow" V="#NAME?"/>""");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert
        Assert.Null(exception);
        var paint = document.GetPageShapes(0)[0].Paint!;
        Assert.Equal(VsdxArrowhead.NoArrowhead, paint.BeginArrowhead);
    }

    /// <summary>
    ///     Proves <c>EndArrow="4"</c> resolves to <see cref="VsdxArrowheadStyle.Arrow"/> - this
    ///     milestone's Finding #5 fix (<c>VsdxDocument.Arrowheads.cs</c>'s <c>ParseArrowheadStyle</c>),
    ///     confirmed in use against a real corpus connector (mirrors a <c>44501e.vsdx</c> cell).
    /// </summary>
    [Fact]
    public void ArrowheadResolution_EndArrowIndex4_ResolvesToArrowStyle()
    {
        // Arrange
        var shapeXml = BuildConnectorShapeXml(
            lineStyleAttribute: string.Empty,
            extraCellsXml: """<Cell N="EndArrow" V="4"/>""");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert
        Assert.Equal(VsdxArrowheadStyle.Arrow, paint.EndArrowhead.Style);
    }

    /// <summary>
    ///     Proves <c>EndArrow="254"</c> resolves to <see cref="VsdxArrowheadStyle.HollowTriangle"/> -
    ///     this milestone's Finding #5 fix (<c>VsdxDocument.Arrowheads.cs</c>'s
    ///     <c>ParseArrowheadStyle</c>), confirmed necessary against <c>44501e.vsdx</c>'s own
    ///     <c>EndArrow V='254' F='USE("Navigable")'</c> cell (a UML "navigable association" end).
    /// </summary>
    [Fact]
    public void ArrowheadResolution_EndArrowIndex254_ResolvesToHollowTriangleStyle()
    {
        // Arrange
        var shapeXml = BuildConnectorShapeXml(
            lineStyleAttribute: string.Empty,
            extraCellsXml: """<Cell N="EndArrow" V="254"/>""");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert
        Assert.Equal(VsdxArrowheadStyle.HollowTriangle, paint.EndArrowhead.Style);
    }

    /// <summary>
    ///     Proves a shape with no <c>BeginArrow</c>/<c>EndArrow</c> cells anywhere in its chain
    ///     (no <c>LineStyle</c> attribute, no <c>&lt;StyleSheets&gt;</c> override) resolves both
    ///     arrowheads to <see cref="VsdxArrowhead.NoArrowhead"/> by default.
    /// </summary>
    [Fact]
    public void ArrowheadResolution_NoArrowCellsAnywhere_DefaultsToNoArrowheadBothEnds()
    {
        // Arrange
        var shapeXml = BuildConnectorShapeXml(lineStyleAttribute: string.Empty);
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert
        Assert.Equal(VsdxArrowhead.NoArrowhead, paint.BeginArrowhead);
        Assert.Equal(VsdxArrowhead.NoArrowhead, paint.EndArrowhead);
    }
}
