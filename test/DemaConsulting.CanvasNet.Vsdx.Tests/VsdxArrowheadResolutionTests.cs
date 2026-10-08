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
    ///     A Master connector shape (1-D: carries its own <c>BeginX</c>/<c>EndX</c>) whose own
    ///     <c>EndArrow</c>/<c>EndArrowSize</c> are literal <c>0</c>/<c>2</c> (no arrowhead) -
    ///     mirroring <c>44501e.vsdx</c>'s own <c>master25.xml</c> "Binary Association" Group
    ///     shape's literal <c>EndArrow V='0' F='GUARD(0)'</c> cell.
    /// </summary>
    private const string ArrowMasterShapeXml =
        """
        <Shape ID="1" Type="Shape">
          <Cell N="PinX" V="1"/><Cell N="PinY" V="0"/><Cell N="Width" V="2"/><Cell N="Height" V="0"/>
          <Cell N="LocPinX" V="1"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
          <Cell N="BeginX" V="0"/><Cell N="BeginY" V="0"/><Cell N="EndX" V="2"/><Cell N="EndY" V="0"/>
          <Cell N="EndArrow" V="0"/><Cell N="EndArrowSize" V="2"/>
          <Section N="Geometry" IX="0">
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="LineTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/></Row>
          </Section>
        </Shape>
        """;

    /// <summary>
    ///     Proves this milestone's <c>ArrowCellNames</c> extension of
    ///     <c>PreferInstanceCells</c> (see <c>VsdxDocument.CellMerge.cs</c>'s own remarks): a 1-D
    ///     (connector) instance's own <c>EndArrow</c>/<c>EndArrowSize</c> cells, cached as
    ///     <c>F="Inh"</c> (not literal), still win over the Master's own distinct literal
    ///     <c>EndArrow="0"</c>/<c>EndArrowSize="2"</c> (see <see cref="ArrowMasterShapeXml"/>) -
    ///     mirroring a per-instance "toggle navigability" authoring choice on one specific UML
    ///     association instance of a shared Master, analogous to <c>44501e.vsdx</c>'s own
    ///     <c>Binary Association</c> connector pattern.
    /// </summary>
    [Fact]
    public void ArrowheadResolution_InstanceInhMarkedArrowCell_PreferredOverMasterValue()
    {
        // Arrange: the instance is a genuine 1-D connector (own BeginX/BeginY/EndX/EndY cells)
        // whose own EndArrow/EndArrowSize are cached as Inh, not literal, yet still the
        // instance's own correct, per-instance authoring choice (Arrow, size index 1).
        var instanceShapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1">
              <Cell N="BeginX" V="0"/><Cell N="BeginY" V="0"/><Cell N="EndX" V="2"/><Cell N="EndY" V="0"/>
              <Cell N="EndArrow" V="4" F="Inh"/><Cell N="EndArrowSize" V="1" F="Inh"/>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(instanceShapeXml, mastersXml: ArrowMasterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert: the instance's own Inh-marked EndArrow (4/Arrow) wins, not the Master's literal
        // EndArrow="0" (None).
        Assert.Equal(VsdxArrowheadStyle.Arrow, paint.EndArrowhead.Style);
        Assert.Equal(1, paint.EndArrowhead.SizeIndex);
    }

    /// <summary>
    ///     A Master <em>2-D</em> shape (no <c>BeginX</c>/<c>EndX</c> cell anywhere - unlike
    ///     <see cref="ArrowMasterShapeXml"/>) whose own <c>EndArrow</c>/<c>EndArrowSize</c> are
    ///     literal <c>0</c>/<c>2</c> (no arrowhead).
    /// </summary>
    private const string TwoDimensionalArrowMasterShapeXml =
        """
        <Shape ID="1" Type="Shape">
          <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="2"/><Cell N="Height" V="2"/>
          <Cell N="LocPinX" V="1"/><Cell N="LocPinY" V="1"/><Cell N="Angle" V="0"/>
          <Cell N="EndArrow" V="0"/><Cell N="EndArrowSize" V="2"/>
          <Section N="Geometry" IX="0">
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="LineTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/></Row>
          </Section>
        </Shape>
        """;

    /// <summary>
    ///     Proves the <c>ArrowCellNames</c> overlay's narrowing to 1-D shapes only, mirroring
    ///     <c>TransformCellNames</c>'s own narrowing: a 2-D shape (no <c>BeginX</c>/<c>EndX</c>
    ///     cell) whose own <c>EndArrow</c> cell is marked <c>F="Inh"</c> still defers entirely to
    ///     the Master's own literal <c>EndArrow</c> value, exactly as the generic merge rule
    ///     already resolved it before this milestone - proving the new overlay does not affect an
    ///     ordinary 2-D shape that legitimately inherits its arrowhead cells from its Master (a
    ///     2-D shape's own arrowhead cell, when present at all, genuinely can be the Master's own
    ///     shared default).
    /// </summary>
    [Fact]
    public void ArrowheadResolution_2DShapeInhMarkedArrowCell_DefersToMasterValue()
    {
        // Arrange: a 2-D shape (no BeginX/EndX) whose own EndArrow is marked Inh with a stale
        // cached value (4/Arrow); the Master's own literal EndArrow is 0 (None) - see
        // TwoDimensionalArrowMasterShapeXml.
        var instanceShapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1">
              <Cell N="EndArrow" V="4" F="Inh"/>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(instanceShapeXml, mastersXml: TwoDimensionalArrowMasterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert: the Master's own literal EndArrow (None) wins, not the instance's stale
        // Inh-marked EndArrow (Arrow) - unaffected by the (1-D-only) arrow-cell overlay.
        Assert.Equal(VsdxArrowheadStyle.None, paint.EndArrowhead.Style);
    }

    /// <summary>
    ///     A Master Group (<c>ID="5"</c>) whose own single child (<c>ID="6"</c>) is a 2-D shape
    ///     (no <c>BeginX</c>/<c>EndX</c> cell) carrying the Master's own template-default
    ///     <c>EndArrow</c>/<c>EndArrowSize</c> (<c>0</c>/<c>2</c>, no arrowhead) - mirroring
    ///     <see cref="TwoDimensionalArrowMasterShapeXml"/>, but nested one level inside a Group.
    /// </summary>
    private const string TwoDimensionalArrowGroupMasterShapeXml =
        """
        <Shape ID="5" Type="Group">
          <Cell N="PinX" V="0.5"/><Cell N="PinY" V="0.5"/><Cell N="Width" V="0.5"/><Cell N="Height" V="0.5"/>
          <Cell N="LocPinX" V="0.25"/><Cell N="LocPinY" V="0.25"/><Cell N="Angle" V="0"/>
          <Shapes>
            <Shape ID="6" Type="Shape">
              <Cell N="PinX" V="0.25"/><Cell N="PinY" V="0.25"/><Cell N="Width" V="0.5"/><Cell N="Height" V="0.5"/>
              <Cell N="LocPinX" V="0.25"/><Cell N="LocPinY" V="0.25"/><Cell N="Angle" V="0"/>
              <Cell N="EndArrow" V="0"/><Cell N="EndArrowSize" V="2"/>
            </Shape>
          </Shapes>
        </Shape>
        """;

    /// <summary>
    ///     Proves Milestone 12's (Finding #3) Group-child carve-out is deliberately scoped to the
    ///     <c>TransformCellNames</c> overlay only, and does <em>not</em> extend to
    ///     <c>ArrowCellNames</c>: a 2-D shape that is itself a Group child (<see
    ///     cref="VsdxShapeNode.Parent"/> not <see langword="null"/>) whose own <c>EndArrow</c>
    ///     cell is marked <c>F="Inh"</c> still defers entirely to the Master child's own literal
    ///     <c>EndArrow</c> value, exactly like an ordinary top-level 2-D shape (see
    ///     <see cref="ArrowheadResolution_2DShapeInhMarkedArrowCell_DefersToMasterValue"/>) -
    ///     proving arrowhead cells remain 1-D-only regardless of Group nesting, per
    ///     <c>VsdxDocument.CellMerge.cs</c>'s own remarks that <c>ArrowCellNames</c> was
    ///     deliberately left out of the <c>isGroupChild</c> carve-out.
    /// </summary>
    [Fact]
    public void ArrowheadResolution_2DGroupChildInhMarkedArrowCell_DefersToMasterValue()
    {
        // Arrange: a Group child (2-D shape, no BeginX/EndX) whose own EndArrow is marked Inh
        // with a stale cached value (4/Arrow); the Master child's own literal EndArrow is 0
        // (None) - see TwoDimensionalArrowGroupMasterShapeXml.
        var instanceShapeXml =
            """
            <Shape ID="5" Type="Group" Master="1">
              <Cell N="PinX" V="2"/><Cell N="PinY" V="2"/><Cell N="Width" V="0.5"/><Cell N="Height" V="0.5"/>
              <Cell N="LocPinX" V="0.25"/><Cell N="LocPinY" V="0.25"/><Cell N="Angle" V="0"/>
              <Shapes>
                <Shape ID="6" MasterShape="6">
                  <Cell N="EndArrow" V="4" F="Inh"/>
                </Shape>
              </Shapes>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(instanceShapeXml, mastersXml: TwoDimensionalArrowGroupMasterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var group = document.GetPageShapes(0)[0];
        var child = Assert.Single(group.Children);

        // Assert: the Master child's own literal EndArrow (None) wins, not the instance child's
        // stale Inh-marked EndArrow (Arrow) - the (1-D-only) arrow-cell overlay is unaffected by
        // Group nesting.
        Assert.Same(group, child.Parent);
        Assert.Equal(VsdxArrowheadStyle.None, child.Paint!.EndArrowhead.Style);
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
