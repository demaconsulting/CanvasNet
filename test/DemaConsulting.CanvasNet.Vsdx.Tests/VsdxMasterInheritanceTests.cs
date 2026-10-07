// cspell:ignore vsdx Visio davehoward Foregnd

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for <c>VsdxDocument.Masters.cs</c>/<c>VsdxDocument.CellMerge.cs</c>'s
///     Master/MasterShape cell and geometry-row override-merge algorithm, including the
///     geometry-row-delete pattern, per the format reference's own Master geometry-row-delete
///     example (confirmed directly against <c>davehoward-test9-rect-and-line.vsdx</c>'s Shape
///     ID='3' and <c>davehoward-test5-master.vsdx</c>'s own full-cell-set inheritance).
/// </summary>
public class VsdxMasterInheritanceTests
{
    /// <summary>A Master shape with PinX/PinY/Width/Height/Angle/FillForegnd all set, and a two-row rectangle-ish Geometry section (MoveTo, LineTo).</summary>
    private const string MasterShapeXml =
        """
        <Shape ID="1" Type="Shape">
          <Cell N="PinX" V="2"/><Cell N="PinY" V="2"/><Cell N="Width" V="2"/><Cell N="Height" V="2"/>
          <Cell N="LocPinX" V="1"/><Cell N="LocPinY" V="1"/><Cell N="Angle" V="0"/>
          <Cell N="FillForegnd" V="#112233"/><Cell N="FillPattern" V="1"/>
          <Section N="Geometry" IX="0">
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="LineTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/></Row>
            <Row T="LineTo" IX="3"><Cell N="X" V="2"/><Cell N="Y" V="2"/></Row>
          </Section>
        </Shape>
        """;

    /// <summary>
    ///     Proves a page instance shape that omits <c>PinY</c>/<c>Width</c>/<c>Height</c>/
    ///     <c>Angle</c> entirely (not even marked <c>F="Inh"</c> - simply absent) still resolves
    ///     them from its Master shape's own cells, while its own literal <c>PinX</c> override
    ///     wins.
    /// </summary>
    [Fact]
    public void MasterInheritance_InstanceOmitsCells_FallsThroughToMasterCells()
    {
        // Arrange: the instance overrides only PinX; every other transform cell is absent.
        var instanceShapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1">
              <Cell N="PinX" V="9"/>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(instanceShapeXml, mastersXml: MasterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert: PinX from the instance; PinY/Width/Height/Angle/LocPin from the Master.
        Assert.Equal(9.0, shape.EffectiveCells!.GetDouble("PinX"));
        Assert.Equal(2.0, shape.EffectiveCells.GetDouble("PinY"));
        Assert.Equal(2.0, shape.EffectiveCells.GetDouble("Width"));
        Assert.Equal(2.0, shape.EffectiveCells.GetDouble("Height"));
    }

    /// <summary>
    ///     Proves an instance's own literal cell value takes precedence over the Master's same-
    ///     named cell, even when both are present.
    /// </summary>
    [Fact]
    public void MasterInheritance_InstanceLiteralCell_OverridesMasterCell()
    {
        // Arrange
        var instanceShapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1">
              <Cell N="FillForegnd" V="#ff0000"/>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(instanceShapeXml, mastersXml: MasterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert
        Assert.Equal("#ff0000", shape.EffectiveCells!.GetString("FillForegnd"));
    }

    /// <summary>
    ///     Proves the geometry-row-delete pattern: an instance row at the same <c>IX</c> as a
    ///     Master row, marked <c>Del="1"</c>, removes that row from the merged geometry entirely
    ///     rather than overriding it - confirmed directly against
    ///     <c>davehoward-test9-rect-and-line.vsdx</c>'s Shape ID='3'.
    /// </summary>
    [Fact]
    public void MasterInheritance_GeometryRowDelete_RemovesMasterRowFromMergedGeometry()
    {
        // Arrange: the instance deletes the Master's row IX=3 (LineTo (2,2)), leaving only the
        // inherited MoveTo(0,0) and LineTo(2,0) rows.
        var instanceShapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1">
              <Section N="Geometry" IX="0">
                <Row T="LineTo" IX="3" Del="1"/>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(instanceShapeXml, mastersXml: MasterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert: only two commands remain (the inherited MoveTo start, then one LineTo) - the
        // deleted LineTo(2,2) row must not appear.
        var subpath = Assert.Single(shape.Geometries![0].Path.Subpaths);
        var command = Assert.Single(subpath.Commands);
        Assert.Equal(2f, command.EndPoint.X);
        Assert.Equal(0f, command.EndPoint.Y);
    }

    /// <summary>
    ///     Proves a shape with no <c>Master=</c> attribute at all resolves using only its own
    ///     cells, with no Master merge attempted.
    /// </summary>
    [Fact]
    public void MasterInheritance_NoMasterAttribute_ResolvesFromOwnCellsOnly()
    {
        // Arrange
        var shapeXml =
            """
            <Shape ID="10" Type="Shape">
              <Cell N="PinX" V="4"/><Cell N="PinY" V="4"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, mastersXml: MasterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert
        Assert.Equal(4.0, shape.EffectiveCells!.GetDouble("PinX"));
        Assert.Equal(1.0, shape.EffectiveCells.GetDouble("Width"));
    }
}
