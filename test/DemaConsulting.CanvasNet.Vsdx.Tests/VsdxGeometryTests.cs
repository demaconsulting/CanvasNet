// cspell:ignore vsdx Visio RelLineTo RelMoveTo NURBSTo

using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for <c>VsdxDocument.Geometry.cs</c>'s geometry-row resolver: recognizing
///     <c>MoveTo</c>/<c>LineTo</c>/<c>RelMoveTo</c>/<c>RelLineTo</c> rows and gracefully skipping
///     every other row type (for example <c>NURBSTo</c>), per <c>canvas-net-vsdx.md</c>'s Risk
///     Control Measures.
/// </summary>
public class VsdxGeometryTests
{
    /// <summary>A single 2x1-inch shape with no rotation/flip, for isolating geometry-row behavior from transform math.</summary>
    private const string ShapeCellsXml =
        """
        <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="2"/><Cell N="Height" V="1"/>
        <Cell N="LocPinX" V="1"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
        """;

    /// <summary>Proves absolute <c>MoveTo</c>/<c>LineTo</c> rows are parsed at their literal <c>X</c>/<c>Y</c> values.</summary>
    [Fact]
    public void Geometry_MoveToLineTo_ProducesShapeLocalPoints()
    {
        // Arrange
        var shapeXml =
            $"""
            <Shape ID="1" Type="Shape">
              {ShapeCellsXml}
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="1"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert
        var subpath = Assert.Single(shape.Geometries![0].Path.Subpaths);
        Assert.Equal(0f, subpath.Start.X);
        Assert.Equal(0f, subpath.Start.Y);
        var command = Assert.Single(subpath.Commands);
        Assert.Equal(PathCommandType.LineTo, command.Type);
        Assert.Equal(2f, command.EndPoint.X);
        Assert.Equal(1f, command.EndPoint.Y);
    }

    /// <summary>Proves <c>RelMoveTo</c>/<c>RelLineTo</c> rows are normalized by the shape's own <c>Width</c>/<c>Height</c>.</summary>
    [Fact]
    public void Geometry_RelMoveToRelLineTo_NormalizedByWidthAndHeight()
    {
        // Arrange: RelMoveTo(0,0) -> (0,0); RelLineTo(1,1) -> (Width, Height) = (2, 1)
        var shapeXml =
            $"""
            <Shape ID="1" Type="Shape">
              {ShapeCellsXml}
              <Section N="Geometry" IX="0">
                <Row T="RelMoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="RelLineTo" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert
        var subpath = Assert.Single(shape.Geometries![0].Path.Subpaths);
        Assert.Equal(0f, subpath.Start.X);
        Assert.Equal(0f, subpath.Start.Y);
        var command = Assert.Single(subpath.Commands);
        Assert.Equal(2f, command.EndPoint.X);
        Assert.Equal(1f, command.EndPoint.Y);
    }

    /// <summary>
    ///     Proves an unrecognized row type (for example <c>NURBSTo</c>) between two recognized
    ///     rows is silently skipped: the pen position is left exactly where the previous
    ///     recognized row left it, and resolution never throws.
    /// </summary>
    [Fact]
    public void Geometry_UnrecognizedRowType_SkippedWithoutThrowing()
    {
        // Arrange: MoveTo(0,0), an unrecognized NURBSTo row, then LineTo(2,1) - the NURBSTo row
        // must be skipped entirely rather than contributing a command or throwing.
        var shapeXml =
            $"""
            <Shape ID="1" Type="Shape">
              {ShapeCellsXml}
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="NURBSTo" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/><Cell N="A" V="0"/><Cell N="B" V="0"/><Cell N="C" V="0"/><Cell N="D" V="0"/><Cell N="E" V="1"/></Row>
                <Row T="LineTo" IX="3"><Cell N="X" V="2"/><Cell N="Y" V="1"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert
        Assert.Null(exception);
        var shape = document.GetPageShapes(0)[0];
        var subpath = Assert.Single(shape.Geometries![0].Path.Subpaths);
        var command = Assert.Single(subpath.Commands);
        Assert.Equal(PathCommandType.LineTo, command.Type);
        Assert.Equal(2f, command.EndPoint.X);
        Assert.Equal(1f, command.EndPoint.Y);
    }

    /// <summary>
    ///     Proves an <c>EllipticalArcTo</c> row is converted to an <see cref="PathCommandType.ArcTo"/>
    ///     command (not skipped) and reaches the documented destination point <c>(X, Y)</c> - this
    ///     milestone's Finding #1 fix (<c>VsdxDocument.Geometry.cs</c>'s <c>AppendEllipticalArcTo</c>/
    ///     <c>TryResolveEllipticalArc</c>). A quarter-circle arc (equal <c>A</c>/<c>B</c> radii,
    ///     <c>90</c>-degree sweep) is used so the destination point is unambiguous.
    /// </summary>
    [Fact]
    public void Geometry_EllipticalArcToRow_ConvertsToArcReachingDestination()
    {
        // Arrange: MoveTo(1,0), then a quarter-circle EllipticalArcTo ending at (0,1) with its
        // own third point (cells A/B) at (0.7071, 0.7071) - the 45-degree point on the unit
        // circle - angle C=0 (unrotated) and ratio D=1 (no eccentricity, a true circle): a
        // standard quarter-circle arc from the 0-degree point to the 90-degree point.
        var shapeXml =
            $"""
            <Shape ID="1" Type="Shape">
              {ShapeCellsXml}
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="1"/><Cell N="Y" V="0"/></Row>
                <Row T="EllipticalArcTo" IX="2"><Cell N="X" V="0"/><Cell N="Y" V="1"/><Cell N="A" V="0.7071067811865476"/><Cell N="B" V="0.7071067811865476"/><Cell N="C" V="0"/><Cell N="D" V="1"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert
        Assert.Null(exception);
        var shape = document.GetPageShapes(0)[0];
        var subpath = Assert.Single(shape.Geometries![0].Path.Subpaths);
        var command = Assert.Single(subpath.Commands);
        Assert.Equal(PathCommandType.ArcTo, command.Type);
        Assert.Equal(0f, command.EndPoint.X, 3);
        Assert.Equal(1f, command.EndPoint.Y, 3);
        Assert.Equal(1f, command.Radius.X, 2);
        Assert.Equal(1f, command.Radius.Y, 2);
    }

    /// <summary>
    ///     Proves an <c>ArcTo</c> row (circular arc, bow-height cell <c>A</c>) is converted to an
    ///     <see cref="PathCommandType.ArcTo"/> command reaching the documented destination point,
    ///     with a radius matching the bow/chord/radius relationship (<c>r = (4b² + c²) / (8|b|)</c>)
    ///     - this milestone's Finding #2 fix (<c>VsdxDocument.Geometry.cs</c>'s <c>AppendArcTo</c>).
    /// </summary>
    [Fact]
    public void Geometry_ArcToRow_ConvertsToArcWithBowDerivedRadius()
    {
        // Arrange: MoveTo(0,0), then an ArcTo row to (2,0) with bow A=1 - chord length 2, bow 1,
        // giving radius r = (4*1*1 + 2*2) / (8*1) = 8/8 = 1.
        var shapeXml =
            $"""
            <Shape ID="1" Type="Shape">
              {ShapeCellsXml}
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="ArcTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="1"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert
        Assert.Null(exception);
        var shape = document.GetPageShapes(0)[0];
        var subpath = Assert.Single(shape.Geometries![0].Path.Subpaths);
        var command = Assert.Single(subpath.Commands);
        Assert.Equal(PathCommandType.ArcTo, command.Type);
        Assert.Equal(2f, command.EndPoint.X, 3);
        Assert.Equal(0f, command.EndPoint.Y, 3);
        Assert.Equal(1f, command.Radius.X, 3);
        Assert.Equal(1f, command.Radius.Y, 3);
    }

    /// <summary>
    ///     Proves an <c>ArcTo</c> row with a zero bow (cell <c>A="0"</c>) - a straight chord,
    ///     per the documented "no bow" degenerate case - degrades to a plain
    ///     <see cref="PathCommandType.LineTo"/> rather than an arc command.
    /// </summary>
    [Fact]
    public void Geometry_ArcToRow_ZeroBow_DegradesToLineTo()
    {
        // Arrange
        var shapeXml =
            $"""
            <Shape ID="1" Type="Shape">
              {ShapeCellsXml}
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="ArcTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/><Cell N="A" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert
        var subpath = Assert.Single(shape.Geometries![0].Path.Subpaths);
        var command = Assert.Single(subpath.Commands);
        Assert.Equal(PathCommandType.LineTo, command.Type);
        Assert.Equal(2f, command.EndPoint.X);
        Assert.Equal(0f, command.EndPoint.Y);
    }

    /// <summary>Proves the <c>NoFill</c>/<c>NoLine</c>/<c>NoShow</c> section flags are resolved from the section's own cells.</summary>
    [Fact]
    public void Geometry_SectionFlags_ResolvedFromSectionCells()
    {
        // Arrange
        var shapeXml =
            $"""
            <Shape ID="1" Type="Shape">
              {ShapeCellsXml}
              <Section N="Geometry" IX="0">
                <Cell N="NoFill" V="1"/><Cell N="NoLine" V="1"/><Cell N="NoShow" V="0"/>
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var section = document.GetPageShapes(0)[0].Geometries![0];

        // Assert
        Assert.True(section.NoFill);
        Assert.True(section.NoLine);
        Assert.False(section.NoShow);
    }
}
