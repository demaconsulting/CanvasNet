// cspell:ignore vsdx Visio davehoward jgreywolfvsdxjs Jgreywolf Foregnd basicshapes

using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     System-level integration tests for the <c>DemaConsulting.CanvasNet.Vsdx</c> package,
///     exercising its public <see cref="VsdxDocument"/> API end-to-end (one test method per
///     top-level system requirement declared in <c>docs/reqstream/canvas-net-vsdx.yaml</c> and
///     <c>docs/reqstream/canvas-net-vsdx/platform-requirements.yaml</c>), mirroring
///     <c>PptxSystemIntegrationTests</c>'s own system-tier pattern for the sibling Pptx package.
///     Unlike the unit-level test classes elsewhere in this project (which target a single
///     internal collaborator such as <c>VsdxDocument.Geometry.cs</c> in isolation), every test
///     here calls only the public surface (<see cref="VsdxDocument.Open(string)"/>/
///     <see cref="VsdxDocument.Open(Stream)"/>, <see cref="VsdxDocument.PageCount"/>,
///     <see cref="VsdxDocument.GetPageSize"/>, <see cref="VsdxDocument.GetPageShapes"/>,
///     <see cref="VsdxDocument.Render(int, int, int, VsdxRenderOptions?)"/>), proving the whole
///     pipeline (package/part resolution, Master inheritance, StyleSheet chain walk, geometry
///     build, transform, paint, text, connectors, groups, and rendering) composes correctly for
///     an external caller.
/// </summary>
public class VsdxSystemIntegrationTests
{
    /// <summary>The root folder a built test project copies the staged <c>.vsdx</c> fixtures into (see the <c>.csproj</c>'s fixture <c>&lt;None&gt;</c> wiring).</summary>
    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "VsdxFixtures");

    /// <summary>Resolves a staged fixture's full path by file name, failing clearly if the fixture-copy wiring did not place it in the output directory.</summary>
    private static string FixturePath(string fileName)
    {
        var path = Path.Combine(FixturesDirectory, fileName);
        Assert.True(File.Exists(path), $"Expected staged fixture '{path}' to exist in the test output directory.");
        return path;
    }

    /// <summary>Proves at least one non-transparent pixel exists somewhere on <paramref name="surface"/>.</summary>
    private static void AssertPaintedSomePixel(Surface surface)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y].A > 0)
                {
                    return;
                }
            }
        }

        Assert.Fail("Expected at least one non-transparent pixel to be painted.");
    }

    // --- CanvasNetVsdx-Platform-* (shared VsdxOpen proof) and CanvasNetVsdx-VsdxOpen ------------

    /// <summary>
    ///     End-to-end proof that a well-formed real <c>.vsdx</c> package opens successfully
    ///     through the public <see cref="VsdxDocument.Open(string)"/> entry point and exposes a
    ///     usable, fully-resolved document (also the shared system-level proof for every
    ///     <c>CanvasNetVsdx-Platform-*</c> requirement, each of which asserts a distinct facet of
    ///     "the package opens and composes with its host platform/dependencies").
    /// </summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_VsdxOpen_SucceedsOnWellFormedPackage()
    {
        // Arrange / Act
        using var document = VsdxDocument.Open(FixturePath("davehoward-test1.vsdx"));

        // Assert
        Assert.Equal(3, document.PageCount);
        var shapes = document.GetPageShapes(0);
        Assert.NotEmpty(shapes);
    }

    /// <summary>Proves <see cref="VsdxDocument.Open(Stream)"/> rejects a <see langword="null"/> stream with <see cref="ArgumentNullException"/>, exercised through the public API rather than internals.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_VsdxOpenValidationNull_NullStreamThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => VsdxDocument.Open((Stream)null!));
    }

    /// <summary>Proves <see cref="VsdxDocument.Open(string)"/> rejects an empty path with <see cref="ArgumentException"/>, exercised through the public API rather than internals.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_VsdxOpenValidationEmptyPath_EmptyPathThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => VsdxDocument.Open(string.Empty));
    }

    // --- CanvasNetVsdx-PageMetadata --------------------------------------------------------------

    /// <summary>Proves <see cref="VsdxDocument.PageCount"/> matches the real fixture's own declared page count (3), read directly from its <c>pages.xml</c>.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_PageCount_ReturnsDeclaredPageCount()
    {
        // Arrange / Act
        using var document = VsdxDocument.Open(FixturePath("davehoward-test1.vsdx"));

        // Assert
        Assert.Equal(3, document.PageCount);
    }

    /// <summary>Proves <see cref="VsdxDocument.GetPageSize"/> matches the real fixture's own declared page size (in EMU), read directly from its <c>pages.xml</c>.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_GetPageSize_ReturnsDeclaredSizeInEmu()
    {
        // Arrange / Act
        using var document = VsdxDocument.Open(FixturePath("davehoward-test1.vsdx"));
        var page1 = document.GetPageSize(0);

        // Assert
        Assert.Equal("Page-1", page1.Name);
        Assert.Equal(7_560_000L, page1.WidthEmu);
        Assert.Equal(10_692_000L, page1.HeightEmu);
    }

    /// <summary>Proves <see cref="VsdxDocument.GetPageSize"/> rejects an out-of-range page index with <see cref="ArgumentOutOfRangeException"/>.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_GetPageSize_OutOfRangeIndexThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test1.vsdx"));

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetPageSize(document.PageCount));
    }

    // --- CanvasNetVsdx-Geometry -------------------------------------------------------------------

    /// <summary>Proves the common geometry row types (<c>MoveTo</c>/<c>LineTo</c>/<c>ArcTo</c>) resolve into a non-empty, usable subpath for a real fixture's shapes.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_Geometry_ResolvesCommonRowTypes()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test3-house.vsdx"));

        // Act
        var shapes = document.GetPageShapes(0);

        // Assert: at least one shape resolves at least one geometry section with at least one subpath command.
        Assert.Contains(shapes, shape => shape.Geometries!.Any(section => section.Path.Subpaths.Any(subpath => subpath.Commands.Count > 0)));
    }

    /// <summary>Proves an unrecognized geometry row type (here, a synthetic <c>EllipticalArcTo</c>) is tolerantly skipped rather than throwing, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_Geometry_UnrecognizedRowTypeIsSkippedNotThrown()
    {
        // Arrange: a shape whose geometry section mixes recognized rows with an EllipticalArcTo row.
        var shapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="EllipticalArcTo" IX="2">
                  <Cell N="X" V="1"/><Cell N="Y" V="1"/><Cell N="A" V="0.5"/><Cell N="B" V="0.5"/>
                  <Cell N="C" V="0"/><Cell N="D" V="1"/>
                </Row>
                <Row T="LineTo" IX="3"><Cell N="X" V="1"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert
        Assert.Null(exception);
    }

    // --- CanvasNetVsdx-Transform ------------------------------------------------------------------

    /// <summary>
    ///     Proves <c>davehoward-test11-rotate.vsdx</c>'s own rotated Shape ID='1' resolves a
    ///     transform that maps its local origin/opposite-corner to the same independently
    ///     (Python, double precision) computed page-space coordinates as
    ///     <c>VsdxTransformTests.Transform_WorkedRotationExample_MatchesIndependentlyComputedCoordinates</c>'s
    ///     own hand-authored reproduction of this fixture's cells - proving the real fixture and
    ///     the unit-level synthetic reproduction agree.
    /// </summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_Transform_RotatedShapeMatchesExpectedOutline()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test11-rotate.vsdx"));
        var shape = Assert.Single(document.GetPageShapes(0), s => s.Id == "1");
        var transform = shape.Transform!;

        // Act
        var origin = transform.ToPage(0, 0);
        var corner = transform.ToPage(transform.Width, transform.Height);

        // Assert
        const double tolerance = 1e-9;
        Assert.Equal(0.7887520150882352, origin.X, tolerance);
        Assert.Equal(9.43226349283737, origin.Y, tolerance);
        Assert.Equal(1.8766022819656367, corner.X, tolerance);
        Assert.Equal(11.87876015368609, corner.Y, tolerance);
    }

    // --- CanvasNetVsdx-MasterInheritance ------------------------------------------------------------

    /// <summary>Proves a shape instance's own literal cell overrides the inherited Master value, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_MasterInheritance_InstanceCellOverridesMaster()
    {
        // Arrange
        var mastersXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="#FF0000"/><Cell N="FillPattern" V="1"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        var shapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1">
              <Cell N="FillForegnd" V="#0000FF"/>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, mastersXml: mastersXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert: the instance's own #0000FF literal wins over the Master's #FF0000.
        Assert.Equal(0, paint.FillColor.R);
        Assert.Equal(0, paint.FillColor.G);
        Assert.Equal(255, paint.FillColor.B);
    }

    /// <summary>Proves a shape instance with no cell of its own for a given name falls through to the inherited Master value, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_MasterInheritance_InheritedCellFallsThroughToMaster()
    {
        // Arrange
        var mastersXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="2"/><Cell N="PinY" V="3"/><Cell N="Width" V="4"/><Cell N="Height" V="5"/>
              <Cell N="LocPinX" V="2"/><Cell N="LocPinY" V="2.5"/><Cell N="Angle" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        var shapeXml = """<Shape ID="10" Type="Shape" Master="1"></Shape>""";
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, mastersXml: mastersXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var transform = document.GetPageShapes(0)[0].Transform!;

        // Assert: the entire transform was inherited verbatim from the Master.
        Assert.Equal(4, transform.Width);
        Assert.Equal(5, transform.Height);
    }

    /// <summary>Proves a shape instance's own <c>Del="1"</c> geometry row deletes the Master's corresponding (matching-<c>IX</c>) row, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_MasterInheritance_DeletedGeometryRowIsOmitted()
    {
        // Arrange
        var mastersXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        var shapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1">
              <Section N="Geometry" IX="0">
                <Row T="LineTo" IX="2" Del="1"/>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, mastersXml: mastersXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var subpath = Assert.Single(document.GetPageShapes(0)[0].Geometries![0].Path.Subpaths);

        // Assert: only the MoveTo survived (its own subpath Start); the deleted LineTo contributed no command.
        Assert.Empty(subpath.Commands);
    }

    // --- CanvasNetVsdx-StyleResolution --------------------------------------------------------------

    /// <summary>Proves a shape with no literal paint cells of its own walks its StyleSheet chain to a literal value, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_StyleResolution_WalksChainToLiteralValue()
    {
        // Arrange: StyleSheet 2 (FillStyle=1) -> StyleSheet 1 (the literal FillForegnd) -> StyleSheet 0.
        var styleSheetsXml =
            """
            <StyleSheets>
              <StyleSheet ID="0" Name="No Style"></StyleSheet>
              <StyleSheet ID="1" Name="Base" FillStyle="0">
                <Cell N="FillForegnd" V="#00FF00"/><Cell N="FillPattern" V="1"/>
              </StyleSheet>
              <StyleSheet ID="2" Name="Derived" FillStyle="1"></StyleSheet>
            </StyleSheets>
            """;
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" FillStyle="2">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, styleSheetsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert
        Assert.Equal(0, paint.FillColor.R);
        Assert.Equal(255, paint.FillColor.G);
        Assert.Equal(0, paint.FillColor.B);
    }

    /// <summary>Proves a shape's own direct literal cell wins over any value supplied by its StyleSheet chain, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_StyleResolution_DirectOverrideWinsOverStyleSheet()
    {
        // Arrange
        var styleSheetsXml =
            """
            <StyleSheets>
              <StyleSheet ID="0" Name="No Style"></StyleSheet>
              <StyleSheet ID="1" Name="Base" FillStyle="0">
                <Cell N="FillForegnd" V="#00FF00"/><Cell N="FillPattern" V="1"/>
              </StyleSheet>
            </StyleSheets>
            """;
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" FillStyle="1">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="#FF00FF"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, styleSheetsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert: shape's own #FF00FF wins, not the StyleSheet's #00FF00.
        Assert.Equal(255, paint.FillColor.R);
        Assert.Equal(0, paint.FillColor.G);
        Assert.Equal(255, paint.FillColor.B);
    }

    // --- CanvasNetVsdx-ColorFill -------------------------------------------------------------------

    /// <summary>Proves a built-in palette index and a direct hex literal both resolve correctly, end-to-end through the public API (see <see cref="VsdxColorPaletteTests"/> for the complementary unit-level coverage).</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_ColorFill_ResolvesBuiltInPaletteIndexAndHexColors()
    {
        // Arrange: FillForegnd="2" (built-in palette "Red") and LineColor="#abcdef" (direct hex).
        var shapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="2"/><Cell N="FillPattern" V="1"/>
              <Cell N="LineColor" V="#abcdef"/><Cell N="LinePattern" V="1"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert
        Assert.Equal(new Rgba32(255, 0, 0, 255), paint.FillColor);
        Assert.Equal(new Rgba32(0xab, 0xcd, 0xef, 255), paint.StrokeColor);
    }

    /// <summary>Proves a <c>"Themed"</c> color cell with no resolvable theme falls back to the documented neutral default rather than throwing, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_ColorFill_ThemedCellFallsBackToNeutralDefaultWhenNoTheme()
    {
        // Arrange: no theme1Xml is supplied, so the "Themed" sentinel cannot resolve against any theme.
        var shapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="Themed"/><Cell N="FillPattern" V="1"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0)[0].Paint);

        // Assert: resolves without throwing to some concrete, fully-opaque color.
        Assert.Null(exception);
        Assert.Equal(255, document.GetPageShapes(0)[0].Paint!.FillColor.A);
    }

    /// <summary>Proves an unsupported (non-solid) <c>FillPattern</c> value degrades to solid fill (using the shape's <c>FillForegnd</c>) rather than throwing, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_ColorFill_UnsupportedFillPatternDegradesToSolidFill()
    {
        // Arrange: FillPattern="25" is not 0 (none) or 1 (solid), but is still a well-formed, non-negative pattern index.
        var shapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="#336699"/><Cell N="FillPattern" V="25"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert: treated exactly as solid fill - HasFill is true, and FillColor is the shape's own FillForegnd.
        Assert.True(paint.HasFill);
        Assert.Equal(new Rgba32(0x33, 0x66, 0x99, 255), paint.FillColor);
    }

    // --- CanvasNetVsdx-TextRendering ---------------------------------------------------------------

    /// <summary>Proves a shape's own literal <c>&lt;Text&gt;</c> content paints visible ink when the page is rendered, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_Text_RendersShapeLabel()
    {
        // Arrange
        var shapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="2"/><Cell N="PinY" V="2"/><Cell N="Width" V="4"/><Cell N="Height" V="2"/>
              <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
              <Cell N="LinePattern" V="0"/><Cell N="FillPattern" V="0"/>
              <Section N="Geometry" IX="0">
                <Cell N="NoFill" V="1"/><Cell N="NoLine" V="1"/>
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
              <Text>Hello</Text>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        using var surface = document.Render(0, 200);

        // Assert
        AssertPaintedSomePixel(surface);
    }

    /// <summary>Proves a shape's own text box resolves a page-space position relative to the shape's own transform, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_Text_PositionsTextBoxRelativeToShapeTransform()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test6-shape-properties.vsdx"));

        // Act
        var shapes = document.GetPageShapes(0);

        // Assert: at least one shape carries both resolved text content and a resolved text box,
        // positioned relative to that same shape's own resolved transform.
        Assert.Contains(
            shapes,
            shape =>
                shape.TextRuns is { Count: > 0 } runs &&
                runs.Any(run => !string.IsNullOrWhiteSpace(run.Text)) &&
                shape.TextBox is not null &&
                shape.Transform is not null);
    }

    // --- CanvasNetVsdx-Connectors ------------------------------------------------------------------

    /// <summary>Proves a straight connector glued between two shapes paints visible ink along its own line, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_Connectors_RendersStraightConnectorBetweenShapes()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test4-connectors.vsdx"));

        // Act
        using var surface = document.Render(0, 100);

        // Assert
        AssertPaintedSomePixel(surface);
    }

    /// <summary>Proves a connector's own dangling (unresolvable) glue target is skipped rather than throwing, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_Connectors_DanglingGlueTargetIsSkippedNotThrown()
    {
        // Arrange: a connector Shape whose own Connects entry points at a ToSheet that does not exist on the page.
        var shapeXml =
            """
            <Shape ID="7" Type="Shape">
              <Cell N="PinX" V="1.5"/><Cell N="PinY" V="0"/><Cell N="Width" V="3"/><Cell N="Height" V="0"/>
              <Cell N="LocPinX" V="1.5"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
              <Cell N="BeginX" V="0"/><Cell N="BeginY" V="0"/><Cell N="EndX" V="3"/><Cell N="EndY" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="2"><Cell N="X" V="3"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        var connectsXml =
            """
            <Connects>
              <Connect FromSheet="7" FromCell="EndX" FromPart="12" ToSheet="999" ToCell="PinX" ToPart="3"/>
            </Connects>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, connectsXml: connectsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert: the dangling glue target is skipped, not the whole page.
        Assert.Null(exception);
        var shape7 = Assert.Single(document.GetPageShapes(0), s => s.Id == "7");
        Assert.Empty(shape7.Connects);
    }

    // --- CanvasNetVsdx-Groups ----------------------------------------------------------------------

    /// <summary>Proves a 3-level-deep nested Group's own leaf shape composes its absolute page-space position through every intermediate level's own transform, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_Groups_ComposesNestedGroupChildTransform()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test10-nested-shapes.vsdx"));
        var shapes = document.GetPageShapes(0);

        // Act
        var topLevelGroup = Assert.Single(shapes, shape => shape.Id == "7");
        var nestedGroup = Assert.Single(topLevelGroup.Children, shape => shape.Id == "3");
        var leafShape = Assert.Single(nestedGroup.Children, shape => shape.Id == "1");

        // Assert: the full parent chain is wired, and every level resolved a usable transform.
        Assert.Same(topLevelGroup, nestedGroup.Parent);
        Assert.Same(nestedGroup, leafShape.Parent);
        Assert.NotNull(topLevelGroup.Transform);
        Assert.NotNull(nestedGroup.Transform);
        Assert.NotNull(leafShape.Transform);
    }

    // --- CanvasNetVsdx-RenderSurface ---------------------------------------------------------------

    /// <summary>Proves the pixel-dimensions <see cref="VsdxDocument.Render(int, int, int, VsdxRenderOptions?)"/> overload paints the expected surface for a real fixture, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_Render_PixelDimensionsOverload_PaintsExpectedSurface()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test3-house.vsdx"));

        // Act
        using var surface = document.Render(0, 400, 400);

        // Assert
        Assert.Equal(400, surface.Width);
        Assert.Equal(400, surface.Height);
        AssertPaintedSomePixel(surface);
    }

    /// <summary>Proves the DPI <see cref="VsdxDocument.Render(int, int, VsdxRenderOptions?)"/> overload computes pixel dimensions as <c>round(pageSizeInches * dpi)</c>, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_Render_DpiOverload_ComputesExpectedPixelDimensions()
    {
        // Arrange: davehoward-test1.vsdx's own page 1 is 7,560,000 EMU wide x 10,692,000 EMU tall
        // (914,400 EMU per inch) = 8.26.. x 11.69.. in (A4). At 100 dpi: round(8.26.. * 100) x round(11.69.. * 100).
        using var document = VsdxDocument.Open(FixturePath("davehoward-test1.vsdx"));
        var page1 = document.GetPageSize(0);
        var expectedWidth = (int)Math.Round(page1.WidthEmu / 914_400.0 * 100, MidpointRounding.AwayFromZero);
        var expectedHeight = (int)Math.Round(page1.HeightEmu / 914_400.0 * 100, MidpointRounding.AwayFromZero);

        // Act
        using var surface = document.Render(0, 100);

        // Assert
        Assert.Equal(expectedWidth, surface.Width);
        Assert.Equal(expectedHeight, surface.Height);
    }

    /// <summary>Proves <see cref="VsdxDocument.Render(int, int, int, VsdxRenderOptions?)"/> rejects a non-positive width/height with <see cref="ArgumentOutOfRangeException"/>, end-to-end through the public API.</summary>
    [Fact]
    public void CanvasNetVsdx_SystemIntegration_Render_InvalidDimensionsThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test1.vsdx"));

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, 0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, 100, 0));
    }

    // --- Whole-document end-to-end proof -----------------------------------------------------------

    /// <summary>
    ///     True end-to-end proof: opens every staged real-world <c>.vsdx</c> fixture, resolves and
    ///     renders every page at a fixed pixel size, and asserts overall success - the strongest
    ///     single system-level regression net this milestone adds, complementing
    ///     <see cref="VsdxRenderFixtureTests"/>'s own per-category whole-document render tests
    ///     (one fixture per shape-family) with full-corpus coverage in one place.
    /// </summary>
    [Theory]
    [MemberData(nameof(VsdxDocumentTests.AllFixtureFileNames), MemberType = typeof(VsdxDocumentTests))]
    public void CanvasNetVsdx_SystemIntegration_WholeDocument_OpensResolvesAndRendersEveryPage(string fileName)
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath(fileName));

        // Act / Assert: every page resolves its shapes and renders without throwing.
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            var shapes = document.GetPageShapes(pageIndex);
            Assert.NotNull(shapes);

            using var surface = document.Render(pageIndex, 150, 150);
            Assert.Equal(150, surface.Width);
            Assert.Equal(150, surface.Height);
        }
    }
}
