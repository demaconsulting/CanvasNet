// cspell:ignore vsdx davehoward jgreywolfvsdxjs Jgreywolf Visio

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for <c>VsdxDocument.Connects.cs</c>'s <c>&lt;Connects&gt;</c> parser and
///     connector-attachment logic, including exact attribute assertions against both of this
///     milestone's worked real-world fixtures (<c>davehoward-test4-connectors.vsdx</c> - whole-
///     shape-pin glue - and <c>jgreywolfvsdxjs-connectors.vsdx</c> - connection-point-indexed
///     glue) and synthetic-package dangling-target tolerance coverage.
/// </summary>
public class VsdxConnectsParsingTests
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
    ///     Proves every <c>&lt;Connect&gt;</c> entry in <c>davehoward-test4-connectors.vsdx</c>'s
    ///     page1.xml (whole-shape-pin glue) is parsed with the exact
    ///     <c>FromSheet</c>/<c>FromCell</c>/<c>FromPart</c>/<c>ToSheet</c>/<c>ToCell</c>/
    ///     <c>ToPart</c> attribute values read directly from the fixture's own XML - see the
    ///     format reference's §7.1 worked example.
    /// </summary>
    [Fact]
    public void ConnectsParsing_DaveHowardConnectorsFixture_ResolvesExactConnectEntryAttributes()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test4-connectors.vsdx"));

        // Act
        var shapes = document.GetPageShapes(0);

        // Assert: Shape ID='7' is glued at both ends (EndX -> Shape 5's PinX, BeginX -> Shape 2's PinX).
        var shape7 = Assert.Single(shapes, shape => shape.Id == "7");
        Assert.Equal(2, shape7.Connects.Count);

        var shape7End = Assert.Single(shape7.Connects, c => c.Endpoint == VsdxConnectEndpoint.End);
        Assert.Equal("7", shape7End.ConnectorShapeId);
        Assert.Equal("12", shape7End.FromPart);
        Assert.Equal("5", shape7End.TargetShapeId);
        Assert.Equal("PinX", shape7End.ToCell);
        Assert.Equal("3", shape7End.ToPart);
        Assert.True(shape7End.IsWholeShapePin);
        Assert.Null(shape7End.ConnectionPointIndex);

        var shape7Begin = Assert.Single(shape7.Connects, c => c.Endpoint == VsdxConnectEndpoint.Begin);
        Assert.Equal("7", shape7Begin.ConnectorShapeId);
        Assert.Equal("9", shape7Begin.FromPart);
        Assert.Equal("2", shape7Begin.TargetShapeId);
        Assert.Equal("PinX", shape7Begin.ToCell);
        Assert.Equal("3", shape7Begin.ToPart);
        Assert.True(shape7Begin.IsWholeShapePin);

        // Assert: Shape ID='6' is glued similarly (EndX -> Shape 2's PinX, BeginX -> Shape 1's PinX).
        var shape6 = Assert.Single(shapes, shape => shape.Id == "6");
        Assert.Equal(2, shape6.Connects.Count);
        var shape6End = Assert.Single(shape6.Connects, c => c.Endpoint == VsdxConnectEndpoint.End);
        Assert.Equal("2", shape6End.TargetShapeId);
        var shape6Begin = Assert.Single(shape6.Connects, c => c.Endpoint == VsdxConnectEndpoint.Begin);
        Assert.Equal("1", shape6Begin.TargetShapeId);

        // Assert: non-connector shapes carry no Connect entries at all.
        var shape1 = Assert.Single(shapes, shape => shape.Id == "1");
        Assert.Empty(shape1.Connects);
    }

    /// <summary>
    ///     Proves <c>jgreywolfvsdxjs-connectors.vsdx</c>'s page1.xml connection-point-indexed glue
    ///     (<c>ToCell="Connections.X1"</c>/<c>"Connections.X2"</c>) is parsed with
    ///     <see cref="VsdxConnect.IsWholeShapePin"/> <see langword="false"/> and the exact 1-based
    ///     <see cref="VsdxConnect.ConnectionPointIndex"/> extracted from each <c>ToCell</c> - see
    ///     the format reference's §7.1 second worked example.
    /// </summary>
    [Fact]
    public void ConnectsParsing_JgreywolfConnectorsFixture_ResolvesConnectionPointIndexedGlue()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("jgreywolfvsdxjs-connectors.vsdx"));

        // Act
        var shapes = document.GetPageShapes(0);

        // Assert
        var connector = Assert.Single(shapes, shape => shape.Id == "42");
        Assert.Equal(2, connector.Connects.Count);

        var endConnect = Assert.Single(connector.Connects, c => c.Endpoint == VsdxConnectEndpoint.End);
        Assert.Equal("12", endConnect.FromPart);
        Assert.Equal("41", endConnect.TargetShapeId);
        Assert.Equal("Connections.X1", endConnect.ToCell);
        Assert.Equal("100", endConnect.ToPart);
        Assert.False(endConnect.IsWholeShapePin);
        Assert.Equal(1, endConnect.ConnectionPointIndex);

        var beginConnect = Assert.Single(connector.Connects, c => c.Endpoint == VsdxConnectEndpoint.Begin);
        Assert.Equal("9", beginConnect.FromPart);
        Assert.Equal("40", beginConnect.TargetShapeId);
        Assert.Equal("Connections.X2", beginConnect.ToCell);
        Assert.Equal("101", beginConnect.ToPart);
        Assert.False(beginConnect.IsWholeShapePin);
        Assert.Equal(2, beginConnect.ConnectionPointIndex);
    }

    /// <summary>
    ///     Proves a <c>&lt;Connect&gt;</c> entry whose <c>ToSheet</c> references a shape ID absent
    ///     from the page is tolerated: the whole page's shape resolution still succeeds without
    ///     throwing, the dangling <see cref="VsdxConnect"/> is still attached to its connector
    ///     shape (since this milestone never dereferences <c>TargetShapeId</c> to compute a
    ///     rendered endpoint), and the connector's own <see cref="VsdxShapeNode.ConnectorEndpoints"/>
    ///     still resolves from its own <c>BeginX/Y</c>/<c>EndX/Y</c> cells, unaffected.
    /// </summary>
    [Fact]
    public void ConnectsParsing_DanglingGlueTarget_SkipsGlueResolutionNotWholePage()
    {
        // Arrange: a 1-D connector shape glued (per its own <Connect> entry) to a target shape ID
        // ("999") that does not exist anywhere on the page.
        var shapeXml =
            """
            <Shape ID="10" Type="Shape">
              <Cell N="PinX" V="1.5"/><Cell N="PinY" V="0"/><Cell N="Width" V="3"/><Cell N="Height" V="0"/>
              <Cell N="LocPinX" V="1.5"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
              <Cell N="BeginX" V="0"/><Cell N="BeginY" V="0"/><Cell N="EndX" V="3"/><Cell N="EndY" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="2"><Cell N="X" V="3"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        var connectsXml = """<Connect FromSheet="10" FromCell="EndX" FromPart="12" ToSheet="999" ToCell="PinX" ToPart="3"/>""";
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, connectsXml: connectsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert
        Assert.Null(exception);
        var shape = Assert.Single(document.GetPageShapes(0));
        Assert.Single(shape.Connects);
        Assert.Equal("999", shape.Connects[0].TargetShapeId);
        Assert.NotNull(shape.ConnectorEndpoints);
        Assert.Equal(0.0, shape.ConnectorEndpoints.BeginX);
        Assert.Equal(3.0, shape.ConnectorEndpoints.EndX);
    }

    /// <summary>
    ///     Proves a page with no <c>&lt;Connects&gt;</c> section at all (confirmed optional/page-
    ///     dependent by the format reference's §13 inventory) resolves every shape with an empty
    ///     <see cref="VsdxShapeNode.Connects"/> list rather than throwing.
    /// </summary>
    [Fact]
    public void ConnectsParsing_NoConnectsSection_EveryShapeHasEmptyConnectsList()
    {
        // Arrange
        var shapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
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
        Assert.Empty(shape.Connects);
        Assert.Null(shape.ConnectorEndpoints);
    }
}
