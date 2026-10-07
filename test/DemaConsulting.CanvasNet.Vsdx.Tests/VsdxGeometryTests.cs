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
