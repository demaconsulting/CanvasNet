// cspell:ignore vsdx Visio LocPin davehoward

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for <c>VsdxDocument.Transform.cs</c>/<see cref="VsdxShapeTransform"/>'s
///     unified shape-local-to-page-space affine transform, including a worked rotation-transform
///     regression test independently verified (via a separate Python double-precision
///     computation, not copied from any planning document) against
///     <c>davehoward-test11-rotate.vsdx</c>'s own Shape ID='1' cells.
/// </summary>
public class VsdxTransformTests
{
    /// <summary>The tight tolerance used by the exact-coordinate rotation regression test, reflecting full double precision at page-space magnitudes around 10 inches.</summary>
    private const double RotationTolerance = 1e-9;

    /// <summary>Proves an unrotated, unflipped shape's transform maps a local corner directly onto its page-space Pin, offset by the local-to-pin vector.</summary>
    [Fact]
    public void Transform_NoRotationNoFlip_MapsLocalOriginRelativeToPin()
    {
        // Arrange: PinX=5, PinY=3, Width=2, Height=1, LocPinX=1 (Width/2), LocPinY=0.5 (Height/2), Angle=0
        // Local (0,0) is (LocPinX, LocPinY) away from the pin -> page (5-1, 3-0.5) = (4, 2.5)
        var shapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="5"/><Cell N="PinY" V="3"/><Cell N="Width" V="2"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="1"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var transform = document.GetPageShapes(0)[0].Transform!;
        var (x, y) = transform.ToPage(0, 0);

        // Assert
        Assert.Equal(4.0, x, RotationTolerance);
        Assert.Equal(2.5, y, RotationTolerance);
    }

    /// <summary>
    ///     Proves the exact worked rotation example independently derived from
    ///     <c>davehoward-test11-rotate.vsdx</c>'s page1.xml Shape ID='1' (Angle=30 degrees =
    ///     0.5235987755983 rad): mapping both the shape-local origin and the opposite
    ///     (<c>Width</c>, <c>Height</c>) corner into page space must match the independently
    ///     (Python, double precision) computed expected coordinates to within
    ///     <see cref="RotationTolerance"/>.
    /// </summary>
    [Fact]
    public void Transform_WorkedRotationExample_MatchesIndependentlyComputedCoordinates()
    {
        // Arrange: exact cell values read directly from davehoward-test11-rotate.vsdx's page1.xml
        var shapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1.332677148526936"/><Cell N="PinY" V="10.65551182326173"/>
              <Cell N="Width" V="2.165354297053872"/><Cell N="Height" V="1.574803125130089"/>
              <Cell N="LocPinX" V="1.082677148526936"/><Cell N="LocPinY" V="0.7874015625650443"/>
              <Cell N="Angle" V="0.5235987755983"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var transform = document.GetPageShapes(0)[0].Transform!;
        var origin = transform.ToPage(0, 0);
        var corner = transform.ToPage(transform.Width, transform.Height);

        // Assert
        Assert.Equal(0.7887520150882352, origin.X, RotationTolerance);
        Assert.Equal(9.43226349283737, origin.Y, RotationTolerance);
        Assert.Equal(1.8766022819656367, corner.X, RotationTolerance);
        Assert.Equal(11.87876015368609, corner.Y, RotationTolerance);
    }

    /// <summary>Proves <see cref="VsdxShapeTransform.FlipX"/>/<see cref="VsdxShapeTransform.FlipY"/> mirror the local point about the pin before rotation.</summary>
    [Fact]
    public void Transform_FlipXFlipY_MirrorsAboutLocPinBeforeRotation()
    {
        // Arrange: PinX=0, PinY=0, LocPinX=0, LocPinY=0, Angle=0, FlipX and FlipY both set ->
        // local (1, 1) becomes (-1, -1) relative to the pin before translating to PinX/PinY.
        var shapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="0"/><Cell N="PinY" V="0"/><Cell N="Width" V="2"/><Cell N="Height" V="2"/>
              <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
              <Cell N="FlipX" V="1"/><Cell N="FlipY" V="1"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var transform = document.GetPageShapes(0)[0].Transform!;
        var (x, y) = transform.ToPage(1, 1);

        // Assert
        Assert.Equal(-1.0, x, RotationTolerance);
        Assert.Equal(-1.0, y, RotationTolerance);
    }

    /// <summary>
    ///     Proves a 1-D shape (connector/line) resolves and applies its transform identically to
    ///     a 2-D shape, since Visio pre-bakes its <c>PinX</c>/<c>PinY</c>/<c>Width</c>/
    ///     <c>Height</c>/<c>Angle</c> cells from its endpoints.
    /// </summary>
    [Fact]
    public void Transform_OneDimensionalShape_UsesSameUnifiedFormula()
    {
        // Arrange: a horizontal 1-D line from (0,0) to (3,0) bakes to PinX=1.5, PinY=0, Width=3,
        // Height=0, Angle=0, LocPinX=1.5 (default Visio 1-D LocPin is the midpoint), LocPinY=0.
        var shapeXml =
            """
            <Shape ID="2" Type="Shape" TwoD="0">
              <Cell N="PinX" V="1.5"/><Cell N="PinY" V="0"/><Cell N="Width" V="3"/><Cell N="Height" V="0"/>
              <Cell N="LocPinX" V="1.5"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
              <Cell N="BeginX" V="0"/><Cell N="BeginY" V="0"/><Cell N="EndX" V="3"/><Cell N="EndY" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="2"><Cell N="X" V="3"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var transform = document.GetPageShapes(0)[0].Transform!;
        var begin = transform.ToPage(0, 0);
        var end = transform.ToPage(3, 0);

        // Assert
        Assert.Equal(0.0, begin.X, RotationTolerance);
        Assert.Equal(0.0, begin.Y, RotationTolerance);
        Assert.Equal(3.0, end.X, RotationTolerance);
        Assert.Equal(0.0, end.Y, RotationTolerance);
    }
}
