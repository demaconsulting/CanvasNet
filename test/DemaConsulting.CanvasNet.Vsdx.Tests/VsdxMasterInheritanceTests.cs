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
    ///     Proves <c>MergeGeometryRows</c> merges an instance row over its Master's own
    ///     same-indexed row <em>cell-by-cell</em>, not wholesale: an instance row carrying only an
    ///     <c>X</c> cell (relying on <c>F="Inh"</c> for its own <c>Y</c>) must still inherit the
    ///     Master row's own <c>Y</c> value, rather than that axis silently defaulting to <c>0</c>
    ///     (the pre-fix "whole-row replacement" bug - see <c>MergeGeometryRows</c>'s own remarks
    ///     and this milestone's Findings #2/#3 "diagonal zigzag" symptom).
    /// </summary>
    [Fact]
    public void MasterInheritance_GeometryRowPartialOverride_MergesCellByCellNotWholesale()
    {
        // Arrange: the instance's own row IX=3 overrides only X (to 5), omitting Y entirely -
        // the merged row must still carry the Master row's own Y=2, not default it to 0.
        var instanceShapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1">
              <Section N="Geometry" IX="0">
                <Row T="LineTo" IX="3"><Cell N="X" V="5"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(instanceShapeXml, mastersXml: MasterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert: two commands (LineTo(2,0), then LineTo(5,2) - X from the instance, Y inherited
        // from the Master row, not defaulted to 0).
        var subpath = Assert.Single(shape.Geometries![0].Path.Subpaths);
        Assert.Equal(2, subpath.Commands.Count);
        var lastCommand = subpath.Commands[^1];
        Assert.Equal(5f, lastCommand.EndPoint.X);
        Assert.Equal(2f, lastCommand.EndPoint.Y);
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

    /// <summary>
    ///     Proves <c>ResolveMasterShape</c> caches a Master's parsed content tree per resolved
    ///     master file rather than re-parsing <c>masterN.xml</c> on every shape that references
    ///     the same Master ID - two independent page shapes both referencing <c>Master="1"</c>
    ///     resolve the exact same <see cref="VsdxShapeNode"/> instance by reference, proving the
    ///     cache (not merely an equal-by-value re-parse) is being hit. Exercises the internal
    ///     <c>ResolveMasterShape</c> entry point directly (see its own remarks for why it is
    ///     <see langword="internal"/>, not <see langword="private"/>): the cache is an
    ///     implementation invariant with no distinguishing externally-visible side effect other
    ///     than performance, so it cannot be proven through the public API alone.
    /// </summary>
    [Fact]
    public void MasterInheritance_RepeatedResolution_ReusesCachedParsedMasterContentTree()
    {
        // Arrange: two page shapes both reference the same Master="1".
        var pageShapesXml =
            """
            <Shape ID="10" Type="Shape" Master="1"/>
            <Shape ID="11" Type="Shape" Master="1"/>
            """;
        using var stream = VsdxTestPackages.BuildPackage(pageShapesXml, mastersXml: MasterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act: resolve the same Master ID twice through the internal entry point.
        var first = document.ResolveMasterShape("1");
        var second = document.ResolveMasterShape("1");

        // Assert: the exact same parsed instance is returned both times (reference equality),
        // proving the second call served from the cache rather than re-parsing master1.xml.
        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    /// <summary>
    ///     Proves the geometry-row merge's "replace by matching IX" semantics: an instance row at
    ///     the same <c>IX</c> as a Master row, with no <c>Del</c> attribute, replaces that row's
    ///     own coordinates in the merged geometry (rather than being appended alongside it or
    ///     ignored), while a Master row at a different, unmatched <c>IX</c> is still inherited
    ///     verbatim in the same merge - proving both halves of
    ///     <c>CanvasNetVsdx-VsdxDocument-GeometryRowMerge</c>'s "replace by IX" / "unmatched rows
    ///     inherited verbatim" requirement text in a single shape, complementing the existing
    ///     <see cref="MasterInheritance_GeometryRowDelete_RemovesMasterRowFromMergedGeometry"/>
    ///     delete-case coverage.
    /// </summary>
    [Fact]
    public void MasterInheritance_InstanceGeometryRowWithoutDel_ReplacesMasterRowAtMatchingIndex()
    {
        // Arrange: the instance replaces the Master's row IX=2 (LineTo (2,0)) with LineTo (3,0),
        // while leaving the Master's row IX=3 (LineTo (2,2)) unmatched and thus inherited
        // verbatim.
        var instanceShapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1">
              <Section N="Geometry" IX="0">
                <Row T="LineTo" IX="2"><Cell N="X" V="3"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(instanceShapeXml, mastersXml: MasterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert: MoveTo(0,0) [start], then the replaced LineTo(3,0), then the unmatched,
        // verbatim-inherited LineTo(2,2).
        var subpath = Assert.Single(shape.Geometries![0].Path.Subpaths);
        Assert.Equal(0f, subpath.Start.X);
        Assert.Equal(0f, subpath.Start.Y);
        Assert.Equal(2, subpath.Commands.Count);
        Assert.Equal(3f, subpath.Commands[0].EndPoint.X);
        Assert.Equal(0f, subpath.Commands[0].EndPoint.Y);
        Assert.Equal(2f, subpath.Commands[1].EndPoint.X);
        Assert.Equal(2f, subpath.Commands[1].EndPoint.Y);
    }

    /// <summary>
    ///     Proves Milestone 10's <c>PreferInstanceTransformCells</c> overlay, narrowed (Milestone
    ///     10 retry 1, Finding #3) to apply only to a 1-D (connector) shape - one carrying both a
    ///     <c>BeginX</c> and an <c>EndX</c> cell, the same detection convention
    ///     <c>VsdxDocument.Groups.cs</c>'s own <c>ConnectorEndpoints</c> resolution uses: an
    ///     instance's own <c>PinX</c> cell marked <c>F="Inh"</c> (a cached, round-tripped formula
    ///     result, not a literal override) still wins over the Master's own distinct <c>PinX</c>
    ///     value - unlike every other (non-transform) cell, where an <c>Inh</c> marking defers
    ///     entirely to the Master. Confirmed necessary against <c>60973.vsdx</c>'s shape
    ///     <c>802</c> (a genuine 1-D connector instance, carrying its own <c>BeginX</c>/
    ///     <c>BeginY</c>/<c>EndX</c>/<c>EndY</c> cells exactly as this fixture now does): its own
    ///     cached, per-instance-computed <c>PinX</c>/<c>PinY</c> (Visio's already-baked connector
    ///     endpoint midpoint) were being discarded in favor of the Master's own small, unrelated
    ///     template-local default position, because the generic Master-wins merge rule treated the
    ///     instance's <c>Inh</c>-marked cell as "absent" - see
    ///     <c>VsdxDocument.CellMerge.cs</c>'s own <c>TransformCellNames</c> remarks for the full
    ///     root-cause account and why this is strictly more correct than the generic rule for
    ///     exactly these 9 per-instance geometry/position cells, and only for a 1-D shape.
    /// </summary>
    [Fact]
    public void MasterInheritance_InstanceInhMarkedTransformCell_PreferredOverMasterValue()
    {
        // Arrange: the Master's own PinX is 2 (its small, template-local default position); the
        // instance is a genuine 1-D connector (carries its own BeginX/BeginY/EndX/EndY cells, the
        // 1-D detection convention) with its own cached PinX="9", marked F="Inh" - a round-tripped
        // formula result, not a literal override, yet still the instance's own correct,
        // per-instance computed position derived from its own endpoints.
        var instanceShapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1">
              <Cell N="PinX" V="9" F="Inh"/>
              <Cell N="BeginX" V="8"/><Cell N="BeginY" V="1"/>
              <Cell N="EndX" V="10"/><Cell N="EndY" V="1"/>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(instanceShapeXml, mastersXml: MasterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert: the instance's own Inh-marked PinX (9) wins, not the Master's PinX (2).
        Assert.Equal(9.0, shape.EffectiveCells!.GetDouble("PinX"));
    }

    /// <summary>
    ///     Proves Milestone 10 retry 1's Finding #3 narrowing: a 2-D shape (no <c>BeginX</c>/
    ///     <c>EndX</c> cell) whose own <c>PinX</c> cell is marked <c>F="Inh"</c> (never locally
    ///     overridden by the author - a legitimate full Master-inheritance-of-position scenario)
    ///     still defers entirely to the Master's own <c>PinX</c> value, exactly as the original,
    ///     pre-Milestone-10 generic merge rule always resolved it - proving the narrowed
    ///     <c>PreferInstanceTransformCells</c> overlay does not affect a 2-D shape in either
    ///     direction (it neither regresses this legitimate inheritance case, which the original,
    ///     unscoped Milestone 10 implementation risked, nor was it ever needed for this case to
    ///     resolve correctly).
    /// </summary>
    [Fact]
    public void MasterInheritance_2DShapeInhMarkedTransformCell_DefersToMasterValue()
    {
        // Arrange: a 2-D shape (no BeginX/EndX) whose own PinX is marked Inh with a stale cached
        // value; the Master's own literal PinX is 2 (see MasterShapeXml).
        var instanceShapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1">
              <Cell N="PinX" V="9" F="Inh"/>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(instanceShapeXml, mastersXml: MasterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert: the Master's own PinX (2) wins, not the instance's stale Inh-marked PinX (9) -
        // unaffected by the (now 1-D-only) transform-cell overlay.
        Assert.Equal(2.0, shape.EffectiveCells!.GetDouble("PinX"));
    }

    /// <summary>
    ///     Proves <c>PreferInstanceTransformCells</c>' overlay is narrowly scoped to exactly the
    ///     9 transform cell names (<c>PinX</c>/<c>PinY</c>/<c>Width</c>/<c>Height</c>/
    ///     <c>LocPinX</c>/<c>LocPinY</c>/<c>Angle</c>/<c>FlipX</c>/<c>FlipY</c>): an
    ///     <c>Inh</c>-marked <em>non</em>-transform cell (<c>FillForegnd</c>) on the instance
    ///     still defers to the Master's own literal value, exactly as every style/paint cell
    ///     already did before this milestone - proving the new overlay did not widen to cells
    ///     where the generic Master-wins rule remains correct.
    /// </summary>
    [Fact]
    public void MasterInheritance_InstanceInhMarkedNonTransformCell_StillDefersToMasterValue()
    {
        // Arrange: the instance's own FillForegnd is marked Inh with a stale cached value;
        // the Master's own literal FillForegnd is "#112233" (see MasterShapeXml).
        var instanceShapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1">
              <Cell N="FillForegnd" V="#ff0000" F="Inh"/>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(instanceShapeXml, mastersXml: MasterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert: the Master's own literal FillForegnd wins, unaffected by the transform-cell
        // overlay.
        Assert.Equal("#112233", shape.EffectiveCells!.GetString("FillForegnd"));
    }
}
