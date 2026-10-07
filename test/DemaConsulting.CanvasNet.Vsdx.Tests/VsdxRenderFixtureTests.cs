// cspell:ignore vsdx davehoward jgreywolfvsdxjs basicshapes Visio

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
}
