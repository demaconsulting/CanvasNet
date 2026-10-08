// cspell:ignore vsdx davehoward jgreywolfvsdxjs basicshapes Visio Foregnd

using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Fixture-conformance tests that exercise <see cref="VsdxDocument"/>'s full public
///     <see cref="VsdxDocument.Render(int, int, VsdxRenderOptions?)"/> API against four
///     real-world <c>.vsdx</c> fixtures, one from each of this milestone's declared test
///     categories: a plain shapes-only fixture, a fixture with text, a fixture with connectors,
///     and a fixture with nested groups. Asserts a broad "this real file renders without an
///     ungraceful failure, painting real content where content is expected" result - the same
///     tier and assertion convention <c>DemaConsulting.CanvasNet.Pptx.Tests.PptxFixturesCorpusTests</c>
///     already establishes for its own sibling package - distinct from (and complementing, not
///     duplicating) the finer-grained, single-construct unit tests already made against
///     hand-authored synthetic packages in <see cref="VsdxRenderTests"/>.
/// </summary>
public class VsdxRenderFixtureTests
{
    /// <summary>The root folder a built test project copies the staged <c>.vsdx</c> fixtures into.</summary>
    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "VsdxFixtures");

    /// <summary>The rendering resolution (dots per inch) used for every fixture render in this file.</summary>
    private const int Dpi = 96;

    /// <summary>
    ///     A fully transparent <see cref="VsdxRenderOptions.BackgroundColor"/>, used so
    ///     <see cref="AssertPaintedSomePixel"/>'s "any painted pixel" check keeps proving real
    ///     content was rendered rather than becoming vacuously true against an opaque-white
    ///     default background.
    /// </summary>
    private static readonly VsdxRenderOptions Transparent = new() { BackgroundColor = new Rgba32(0, 0, 0, 0) };

    /// <summary>Resolves a staged fixture's full path by file name, failing clearly if the fixture-copy wiring did not place it in the output directory.</summary>
    private static string FixturePath(string fileName)
    {
        var path = Path.Combine(FixturesDirectory, fileName);
        Assert.True(File.Exists(path), $"Expected staged fixture '{path}' to exist in the test output directory.");
        return path;
    }

    /// <summary>
    ///     Proves at least one non-transparent pixel was painted somewhere on
    ///     <paramref name="surface"/> - the broad "real content was rendered, not merely a blank
    ///     surface" assertion this fixture-conformance tier favors over per-pixel checks.
    /// </summary>
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

    /// <summary>
    ///     Proves <c>jgreywolfvsdxjs-basicshapes.vsdx</c> (a plain, shapes-only fixture, no
    ///     connectors/groups/notable text) renders every one of its pages without throwing,
    ///     painting visible shape-fill/stroke ink on each.
    /// </summary>
    [Fact]
    public void Render_BasicShapesFixture_RendersEveryPageWithVisibleInkWithoutThrowing()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("jgreywolfvsdxjs-basicshapes.vsdx"));

        // Act & Assert
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            using var surface = document.Render(pageIndex, Dpi, Transparent);
            AssertPaintedSomePixel(surface);
        }
    }

    /// <summary>
    ///     Proves <c>jgreywolfvsdxjs-drawing.vsdx</c> (a fixture whose shapes carry text content)
    ///     renders every one of its pages without throwing, painting visible glyph/shape ink on
    ///     each - a real-fixture complement to <see cref="VsdxRenderTests.Render_TextShape_PaintsVisibleGlyphInk"/>'s
    ///     synthetic smoke test.
    /// </summary>
    [Fact]
    public void Render_DrawingFixtureWithText_RendersEveryPageWithVisibleInkWithoutThrowing()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("jgreywolfvsdxjs-drawing.vsdx"));

        // Act & Assert
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            using var surface = document.Render(pageIndex, Dpi, Transparent);
            AssertPaintedSomePixel(surface);
        }
    }

    /// <summary>
    ///     Proves <c>jgreywolfvsdxjs-connectors.vsdx</c> (a fixture whose page declares a
    ///     <c>&lt;Connects&gt;</c> section and glued connector shapes) renders every one of its
    ///     pages without throwing, painting visible connector-line/arrowhead ink on each - a
    ///     real-fixture complement to <see cref="VsdxRenderTests"/>'s own synthetic connector
    ///     arrowhead tests.
    /// </summary>
    [Fact]
    public void Render_ConnectorsFixture_RendersEveryPageWithVisibleInkWithoutThrowing()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("jgreywolfvsdxjs-connectors.vsdx"));

        // Act & Assert
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            using var surface = document.Render(pageIndex, Dpi, Transparent);
            AssertPaintedSomePixel(surface);
        }
    }

    /// <summary>
    ///     Proves <c>davehoward-test10-nested-shapes.vsdx</c> (a fixture whose page declares
    ///     multi-level nested <c>Type="Group"</c> shapes) renders every one of its pages without
    ///     throwing, painting visible ink from the resolved, flattened descendant shape tree -
    ///     proving the recursive <c>RenderShapeRecursive</c> group walk (Milestone 6's
    ///     group-resolution helpers composed with this milestone's own per-level
    ///     <see cref="VsdxShapeTransform.ToPageMatrix"/> accumulation) reaches and paints every
    ///     nested descendant, not just top-level shapes.
    /// </summary>
    [Fact]
    public void Render_NestedShapesFixture_RendersEveryPageWithVisibleInkWithoutThrowing()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test10-nested-shapes.vsdx"));

        // Act & Assert
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            using var surface = document.Render(pageIndex, Dpi, Transparent);
            AssertPaintedSomePixel(surface);
        }
    }

    /// <summary>
    ///     Proves Milestone 10's <c>NonPrinting</c> suppression (Bug #1(b) - see
    ///     <c>VsdxDocument.Render.cs</c>'s <c>RenderShapeRecursive</c>): a <c>Type="Group"</c>
    ///     shape with its own resolved fill and a <c>NonPrinting="1"</c> cell paints none of its
    ///     own fill ink, yet its child shape - nested entirely within the parent's own box -
    ///     still paints its own, distinctly-colored fill, proving self-paint suppression does not
    ///     also suppress recursion into children. Uses a small, hand-authored synthetic package
    ///     (not the external <c>poi-fixtures</c> corpus), modeled on this milestone's own
    ///     confirmed <c>44501b.vsdx</c> "Activity" heading regression (see the milestone's own
    ///     completion report for the external smoke-test visual evidence).
    /// </summary>
    [Fact]
    public void Render_NonPrintingShape_SkipsOwnFillButStillRendersChildren()
    {
        // Arrange: a red-filled Group spanning page-space [1,3]x[1,3], marked NonPrinting="1",
        // containing one blue-filled child spanning the Group's own local-space [0.5,1.5]x[0.5,1.5]
        // (entirely inside the parent's own box, but not covering all of it).
        var shapeXml =
            """
            <Shape ID="1" Type="Group">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="2"/><Cell N="Height" V="2"/>
              <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
              <Cell N="NonPrinting" V="1"/>
              <Cell N="FillForegnd" V="#FF0000"/><Cell N="FillPattern" V="1"/><Cell N="LinePattern" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="3"><Cell N="X" V="2"/><Cell N="Y" V="2"/></Row>
                <Row T="LineTo" IX="4"><Cell N="X" V="0"/><Cell N="Y" V="2"/></Row>
              </Section>
              <Shapes>
                <Shape ID="2" Type="Shape">
                  <Cell N="PinX" V="0.5"/><Cell N="PinY" V="0.5"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
                  <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
                  <Cell N="FillForegnd" V="#0000FF"/><Cell N="FillPattern" V="1"/><Cell N="LinePattern" V="0"/>
                  <Section N="Geometry" IX="0">
                    <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                    <Row T="LineTo" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="0"/></Row>
                    <Row T="LineTo" IX="3"><Cell N="X" V="1"/><Cell N="Y" V="1"/></Row>
                    <Row T="LineTo" IX="4"><Cell N="X" V="0"/><Cell N="Y" V="1"/></Row>
                  </Section>
                </Shape>
              </Shapes>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act: render at 100 DPI against the default 8.5x11in page (850x1100px) with a
        // transparent background, so an unpainted pixel is unambiguously detectable.
        using var surface = document.Render(0, 100, Transparent);

        // Assert: a pixel inside the parent's own box but outside the child's box (page-space
        // (1.2, 2.8), i.e. pixel (120, 820)) stayed fully transparent - the parent's own red
        // fill was never painted.
        Assert.Equal(0, surface[120, 820].A);

        // Assert: a pixel inside the child's own box (page-space (2, 2), i.e. pixel (200, 900))
        // painted the child's own blue fill - recursion into children still happened.
        var childPixel = surface[200, 900];
        Assert.Equal(0, childPixel.R);
        Assert.Equal(0, childPixel.G);
        Assert.Equal(255, childPixel.B);
        Assert.True(childPixel.A > 0);
    }

    /// <summary>
    ///     Proves Milestone 10's <c>MinStrokeWidthPixels</c> hairline floor (Bug #2 - see
    ///     <c>VsdxDocument.Render.cs</c>'s <c>PaintShapeGeometry</c>): a shape whose resolved
    ///     <c>LineWeight</c> rasterizes to well under one physical pixel at the chosen render
    ///     resolution still paints visible stroke ink, rather than vanishing entirely. Uses a
    ///     small, hand-authored synthetic package with a deliberately sub-pixel
    ///     <c>LineWeight="0.002"</c> (0.2px at 100 DPI) and no fill of its own, modeled on this
    ///     milestone's own confirmed <c>60973.vsdx</c> missing-connector-line regression (see the
    ///     milestone's own completion report for the external smoke-test visual evidence).
    /// </summary>
    [Fact]
    public void Render_SubPixelLineWeight_StillPaintsVisibleHairlineStroke()
    {
        // Arrange: a plain, unfilled horizontal line spanning page-space (0.25,5.5)-(4.25,5.5),
        // with LineWeight="0.002" (0.2px at 100 DPI - well under one physical pixel).
        var shapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="2.25"/><Cell N="PinY" V="5.5"/>
              <Cell N="Width" V="4"/><Cell N="Height" V="0"/>
              <Cell N="LocPinX" V="2"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
              <Cell N="LineColor" V="#000000"/><Cell N="LineWeight" V="0.002"/><Cell N="LinePattern" V="1"/>
              <Cell N="FillPattern" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="2"><Cell N="X" V="4"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act: render at 100 DPI against the default 8.5x11in page, transparent background.
        using var surface = document.Render(0, 100, Transparent);

        // Assert: despite the sub-pixel LineWeight, at least one visible (non-transparent) pixel
        // was painted along the line - without the hairline floor, this would be a blank surface.
        AssertPaintedSomePixel(surface);
    }
}
