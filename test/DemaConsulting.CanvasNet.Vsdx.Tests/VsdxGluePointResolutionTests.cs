// cspell:ignore vsdx davehoward jgreywolfvsdxjs Visio WALKGLUE

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for <c>VsdxDocument.Shapes.cs</c>'s connector-endpoint resolution
///     (<see cref="VsdxShapeNode.ConnectorEndpoints"/>), including exact-value worked-example
///     regression tests mirroring <c>VsdxTransformTests</c>'s own worked-rotation-example
///     pattern, independently read (not computed) from three real-world <c>.vsdx</c> fixtures'
///     own XML: <c>davehoward-test9-rect-and-line.vsdx</c> (a plain, unmastered 1-D "Line A"
///     shape), and both of this milestone's worked connector examples,
///     <c>davehoward-test4-connectors.vsdx</c> (whole-shape-pin glue, <c>_WALKGLUE</c> formula)
///     and <c>jgreywolfvsdxjs-connectors.vsdx</c> (connection-point-indexed glue, <c>PAR(PNT(...))</c>
///     formula).
/// </summary>
public class VsdxGluePointResolutionTests
{
    /// <summary>The root folder a built test project copies the staged <c>.vsdx</c> fixtures into.</summary>
    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "VsdxFixtures");

    /// <summary>Resolves a staged fixture's full path by file name, failing clearly if the fixture-copy wiring did not place it in the output directory.</summary>
    private static string FixturePath(string fileName)
    {
        var path = Path.Combine(FixturesDirectory, fileName);
        Assert.True(File.Exists(path), $"Expected staged fixture '{path}' to exist in the test output directory.");
        return path;
    }

    /// <summary>
    ///     Proves <c>davehoward-test9-rect-and-line.vsdx</c>'s page1.xml Shape ID='2' ("Line A") -
    ///     a plain, unmastered 1-D shape whose <c>BeginX/Y</c>/<c>EndX/Y</c> cells carry a
    ///     <c>F=</c> formula referencing <em>other</em> cells on the same shape (not
    ///     <c>_WALKGLUE</c>/<c>PAR(PNT(...))</c> glue formulas) - still resolves
    ///     <see cref="VsdxShapeNode.ConnectorEndpoints"/> to the exact <c>V</c> values read
    ///     directly from the fixture's own XML, confirming the resolver trusts <c>V</c>
    ///     unconditionally rather than special-casing which formula produced it.
    /// </summary>
    [Fact]
    public void GluePointResolution_PlainLineFixture_MatchesExactBeginEndCellValues()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test9-rect-and-line.vsdx"));

        // Act
        var lineA = Assert.Single(document.GetPageShapes(0), shape => shape.Id == "2");
        var endpoints = lineA.ConnectorEndpoints;

        // Assert: exact values read directly from davehoward-test9-rect-and-line.vsdx's page1.xml.
        Assert.NotNull(endpoints);
        Assert.Equal(0.3937007827558093, endpoints.BeginX);
        Assert.Equal(9.448818786139423, endpoints.BeginY);
        Assert.Equal(3.937007827558093, endpoints.EndX);
        Assert.Equal(10.23622035165104, endpoints.EndY);
    }

    /// <summary>
    ///     Proves <c>davehoward-test4-connectors.vsdx</c>'s page1.xml Shape ID='6' (a Dynamic
    ///     connector instance whose <c>BeginX/Y</c>/<c>EndX/Y</c> cells carry a <c>_WALKGLUE(...)</c>
    ///     whole-shape-pin glue formula) resolves <see cref="VsdxShapeNode.ConnectorEndpoints"/>
    ///     to the exact <c>V</c> values read directly from the fixture's own XML - the format
    ///     reference's §7.1/§7.2 first worked example.
    /// </summary>
    [Fact]
    public void GluePointResolution_WholeShapePinGlueFixture_MatchesExactPreBakedCoordinates()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test4-connectors.vsdx"));

        // Act
        var shape6 = Assert.Single(document.GetPageShapes(0), shape => shape.Id == "6");
        var endpoints = shape6.ConnectorEndpoints;

        // Assert: exact values read directly from davehoward-test4-connectors.vsdx's page1.xml.
        Assert.NotNull(endpoints);
        Assert.Equal(2.415354251861572, endpoints.BeginX);
        Assert.Equal(10.65551182326173, endpoints.BeginY);
        Assert.Equal(3.051180987260419, endpoints.EndX);
        Assert.Equal(10.65551182326173, endpoints.EndY);
    }

    /// <summary>
    ///     Proves <c>jgreywolfvsdxjs-connectors.vsdx</c>'s page1.xml Shape ID='42' (a connector
    ///     whose <c>BeginX/Y</c>/<c>EndX/Y</c> cells carry a <c>PAR(PNT(Sheet.N!Connections.X«i»,
    ///     Sheet.N!Connections.Y«i»))</c> connection-point-indexed glue formula) resolves
    ///     <see cref="VsdxShapeNode.ConnectorEndpoints"/> to the exact <c>V</c> values read
    ///     directly from the fixture's own XML - the format reference's §7.1/§7.2 second worked
    ///     example.
    /// </summary>
    [Fact]
    public void GluePointResolution_ConnectionPointIndexedGlueFixture_MatchesExactPreBakedCoordinates()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("jgreywolfvsdxjs-connectors.vsdx"));

        // Act
        var connector = Assert.Single(document.GetPageShapes(0), shape => shape.Id == "42");
        var endpoints = connector.ConnectorEndpoints;

        // Assert: exact values read directly from jgreywolfvsdxjs-connectors.vsdx's page1.xml.
        Assert.NotNull(endpoints);
        Assert.Equal(3.7014, endpoints.BeginX);
        Assert.Equal(7.4999, endpoints.BeginY);
        Assert.Equal(4.8207, endpoints.EndX);
        Assert.Equal(7.4999, endpoints.EndY);
    }

    /// <summary>
    ///     Proves a 2-D shape (no <c>BeginX</c>/<c>EndX</c> cell pair) resolves
    ///     <see cref="VsdxShapeNode.ConnectorEndpoints"/> to <see langword="null"/>, not a
    ///     zero-filled instance - distinguishing "not a connector" from "a connector pinned at the
    ///     origin".
    /// </summary>
    [Fact]
    public void GluePointResolution_TwoDimensionalShape_ConnectorEndpointsIsNull()
    {
        // Arrange
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
        var shape = Assert.Single(document.GetPageShapes(0));

        // Assert
        Assert.Null(shape.ConnectorEndpoints);
    }
}
