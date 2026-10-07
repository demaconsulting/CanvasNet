// cspell:ignore vsdx davehoward jgreywolfvsdxjs Visio

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Smoke-level tests proving this milestone's full shape-resolution pipeline (Master/cell
///     merge, StyleSheet chain walk, geometry-row build, transform, paint) runs to completion,
///     without throwing, against every real-world <c>.vsdx</c> fixture this milestone's own
///     planning report singled out: <c>davehoward-test3-house.vsdx</c>,
///     <c>davehoward-test5-master.vsdx</c>, <c>davehoward-test9-rect-and-line.vsdx</c>,
///     <c>davehoward-test11-rotate.vsdx</c>, <c>davehoward-test12-colors.vsdx</c>, and
///     <c>davehoward-test10-nested-shapes.vsdx</c>.
/// </summary>
public class VsdxFixtureShapeResolutionTests
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

    /// <summary>Every fixture this milestone's own plan report calls out by name, each exercising a distinct resolution concern (master, nested shapes, rotation, colors, plain rect/line).</summary>
    public static TheoryData<string> PlanReportFixtureFileNames =>
    [
        "davehoward-test3-house.vsdx",
        "davehoward-test5-master.vsdx",
        "davehoward-test9-rect-and-line.vsdx",
        "davehoward-test10-nested-shapes.vsdx",
        "davehoward-test11-rotate.vsdx",
        "davehoward-test12-colors.vsdx"
    ];

    /// <summary>
    ///     Proves every page's top-level shapes resolve (Master merge, geometry, transform,
    ///     paint) without throwing for every fixture singled out by the plan report, and that
    ///     every resolved shape carries a non-null <see cref="VsdxShapeNode.EffectiveCells"/>/
    ///     <see cref="VsdxShapeNode.Transform"/>/<see cref="VsdxShapeNode.Paint"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(PlanReportFixtureFileNames))]
    public void FixtureShapeResolution_RealFixture_ResolvesEveryPageWithoutThrowing(string fileName)
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath(fileName));

        // Act / Assert: resolving every page's shapes must not throw, for any page.
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            var exception = Record.Exception(() => document.GetPageShapes(pageIndex));
            Assert.Null(exception);

            var shapes = document.GetPageShapes(pageIndex);
            foreach (var shape in shapes)
            {
                Assert.NotNull(shape.EffectiveCells);
                Assert.NotNull(shape.Geometries);
                Assert.NotNull(shape.Transform);
                Assert.NotNull(shape.Paint);
            }
        }
    }

    /// <summary>
    ///     Proves <c>davehoward-test5-master.vsdx</c>'s page-level Group shape instance - which
    ///     carries almost no cells of its own (no <c>PinY</c>/<c>Width</c>/<c>Height</c>/
    ///     <c>Angle</c> at all) - still resolves a complete transform by inheriting every missing
    ///     cell from its Master shape.
    /// </summary>
    [Fact]
    public void FixtureShapeResolution_MasterFixture_GroupInstanceInheritsFullTransformFromMaster()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test5-master.vsdx"));

        // Act
        var shapes = document.GetPageShapes(0);

        // Assert: at least one shape resolved with a non-degenerate (positive) Width/Height,
        // proving the Master-inherited cells were actually picked up rather than defaulting to 0.
        Assert.Contains(shapes, shape => shape.Transform is { Width: > 0, Height: > 0 });
    }

    /// <summary>
    ///     Proves <c>davehoward-test10-nested-shapes.vsdx</c>'s top-level shapes resolve, and that
    ///     every nested child shape - at every nesting depth - is now also fully resolved (Master
    ///     merge, geometry, transform, paint), each with its <see cref="VsdxShapeNode.Parent"/>
    ///     correctly set to its immediately-enclosing shape - see <c>VsdxDocument.Groups.cs</c>'s
    ///     <c>ResolveShapeRecursive</c> (Milestone 6), which supersedes this fixture's own earlier,
    ///     children-left-unresolved behavior.
    /// </summary>
    [Fact]
    public void FixtureShapeResolution_NestedShapesFixture_EveryNestedChildResolvesWithParentSet()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test10-nested-shapes.vsdx"));

        // Act
        var shapes = document.GetPageShapes(0);

        // Assert
        Assert.NotEmpty(shapes);
        var allDescendants = shapes.SelectMany(FlattenDescendants).ToList();
        Assert.NotEmpty(allDescendants); // the fixture is known to nest at least one level deep.
        foreach (var (child, parent) in allDescendants)
        {
            Assert.NotNull(child.EffectiveCells);
            Assert.NotNull(child.Transform);
            Assert.NotNull(child.Paint);
            Assert.Same(parent, child.Parent);
        }
    }

    /// <summary>Recursively flattens a shape's own descendant tree into (child, parent) pairs, at every nesting depth.</summary>
    private static IEnumerable<(VsdxShapeNode Child, VsdxShapeNode Parent)> FlattenDescendants(VsdxShapeNode shape)
    {
        foreach (var child in shape.Children)
        {
            yield return (child, shape);
            foreach (var descendant in FlattenDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}
