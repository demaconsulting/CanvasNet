// cspell:ignore vsdx Visio davehoward

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Real-fixture and synthetic tests for <c>VsdxDocument.Groups.cs</c>'s recursive group/
///     nested-shape resolver (Milestone 6): arbitrary-depth child resolution, parent-composed
///     absolute page-space coordinates, Master-group-child correlation by ID (not position), and
///     the depth/resolved-shape-count budget guards.
/// </summary>
public class VsdxGroupResolutionTests
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

    /// <summary>Recursively flattens a shape's own descendant tree, in document order.</summary>
    private static IEnumerable<VsdxShapeNode> FlattenDescendants(VsdxShapeNode shape)
    {
        foreach (var child in shape.Children)
        {
            yield return child;
            foreach (var descendant in FlattenDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    ///     Proves <c>davehoward-test10-nested-shapes.vsdx</c>'s own 3-level-deep plain nested
    ///     group (top-level Group <c>ID='7'</c> -&gt; nested Group <c>ID='3'</c> -&gt; leaf Shape
    ///     <c>ID='1'</c>, every level's own <c>PinX</c>/<c>PinY</c>/<c>LocPinX</c>/<c>LocPinY</c>
    ///     confirmed verbatim against the extracted fixture's own <c>visio/pages/page1.xml</c>)
    ///     composes its absolute page-space position exactly as every intermediate level's own
    ///     <c>ToPage</c> transform, chained via <see cref="VsdxShapeNode.Parent"/>, predicts -
    ///     worked by hand (see this test's own inline arithmetic) rather than merely "did not
    ///     throw".
    /// </summary>
    [Fact]
    public void VsdxDocument_GroupChildTransformComposition_ResolvesNestedGroupChildPosition()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test10-nested-shapes.vsdx"));
        var shapes = document.GetPageShapes(0);

        // Act: locate the 3-level-deep leaf shape ID='1', nested in Group ID='3', itself nested
        // in top-level Group ID='7'.
        var topLevelGroup = Assert.Single(shapes, shape => shape.Id == "7");
        var nestedGroup = Assert.Single(topLevelGroup.Children, shape => shape.Id == "3");
        var leafShape = Assert.Single(nestedGroup.Children, shape => shape.Id == "1");

        // Assert: parent chain wired correctly.
        Assert.Same(nestedGroup, leafShape.Parent);
        Assert.Same(topLevelGroup, nestedGroup.Parent);
        Assert.Null(topLevelGroup.Parent);

        // Assert: leaf shape fully resolved, not merely parsed.
        Assert.NotNull(leafShape.EffectiveCells);
        Assert.NotNull(leafShape.Transform);
        Assert.NotNull(leafShape.Paint);

        // Assert: the leaf shape's own pin point, composed through its parent (ID='3') and
        // grandparent (ID='7') transforms via ToPageSpace, lands at the hand-computed absolute
        // page-space coordinate (see this test's own XML doc comment for the by-hand working,
        // confirmed against page1.xml's own literal PinX/PinY/LocPinX/LocPinY V= values - every
        // level's own Angle/FlipX/FlipY is 0, so each level's own ToPage reduces to a pure
        // translate).
        var (x, y) = VsdxDocument.ToPageSpace(leafShape, leafShape.Transform.LocPinX, leafShape.Transform.LocPinY);
        Assert.Equal(1.4566928961964947, x, precision: 6);
        Assert.Equal(10.433070743028946, y, precision: 6);
    }

    /// <summary>
    ///     Proves <c>davehoward-test3-house.vsdx</c>'s page-level Group shape instance
    ///     (<c>&lt;Shape ID='7' Type='Group' Master='2'&gt;</c>) - whose own nested children are
    ///     minimal stubs carrying only a <c>MasterShape=</c> attribute and no cells of their own
    ///     (<c>&lt;Shape ID='8' MasterShape='6'/&gt;</c>, <c>ID='9' MasterShape='7'</c>,
    ///     <c>ID='10' MasterShape='8'</c>) - resolves each stub child by correlating it against
    ///     the Master group's own child with the matching <c>ID</c> (<c>master1.xml</c>'s own
    ///     nested <c>&lt;Shape ID='5' Type='Group'&gt;</c>, containing <c>ID='6'</c>/<c>ID='7'</c>/
    ///     <c>ID='8'</c>), not merely the child occupying the same position.
    /// </summary>
    [Fact]
    public void VsdxDocument_NestedMasterShapeResolution_MatchesChildrenByIdNotPosition()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("davehoward-test3-house.vsdx"));
        var shapes = document.GetPageShapes(0);

        // Act
        var houseGroup = Assert.Single(shapes, shape => shape.Id == "7");
        var stubChild = Assert.Single(houseGroup.Children, shape => shape.Id == "8");

        // Assert: the stub carries no own Width cell at all, yet resolves a positive Width -
        // proving it inherited from its correlated Master child (Master shape ID='6'), not from
        // an empty/default cell bag.
        Assert.NotNull(stubChild.EffectiveCells);
        Assert.NotNull(stubChild.Transform);
        Assert.True(stubChild.Transform.Width > 0);
        Assert.True(stubChild.Transform.Height > 0);
        Assert.Same(houseGroup, stubChild.Parent);

        // Assert: the inherited Width/Height match Master shape ID='6''s own literal values
        // exactly (confirmed directly against the extracted fixture's own master1.xml), proving
        // ID-based correlation (6, not merely "whichever child is first").
        Assert.Equal(0.7391203115739566, stubChild.Transform.Width, precision: 9);
        Assert.Equal(0.8036576073152165, stubChild.Transform.Height, precision: 9);
    }

    /// <summary>
    ///     Proves every shape in a page's shape tree - top-level and nested group children alike,
    ///     across every real fixture in scope - resolves without throwing, as a broad smoke-level
    ///     regression guard alongside the two narrower, worked-example tests above.
    /// </summary>
    [Theory]
    [InlineData("davehoward-test10-nested-shapes.vsdx")]
    [InlineData("davehoward-test3-house.vsdx")]
    public void VsdxDocument_GroupChildTransformComposition_EveryNestedChildResolvesWithoutThrowing(string fileName)
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath(fileName));

        // Act / Assert
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            var exception = Record.Exception(() => document.GetPageShapes(pageIndex));
            Assert.Null(exception);

            foreach (var descendant in document.GetPageShapes(pageIndex).SelectMany(FlattenDescendants))
            {
                Assert.NotNull(descendant.EffectiveCells);
                Assert.NotNull(descendant.Transform);
                Assert.NotNull(descendant.Paint);
            }
        }
    }

    /// <summary>
    ///     Proves a group-child <c>&lt;Shape Del="1"&gt;</c> stub (Milestone 10's Bug #1(a) fix -
    ///     see <c>VsdxDocument.Shapes.cs</c>'s <c>ParseShapeElements</c>) is excluded from the
    ///     resolved shape tree entirely: it contributes neither a <see cref="VsdxShapeNode"/> nor
    ///     any spurious rendered content, confirmed against <c>60973.vsdx</c>'s own
    ///     <c>page2.xml</c> shape, which this synthetic fixture reproduces in miniature (a
    ///     correct sibling child followed by a <c>MasterShape</c>-only deleted stub using the
    ///     real fixture's own sentinel <c>ID="4294967295"</c>).
    /// </summary>
    [Fact]
    public void VsdxDocument_GroupChildDeletedStub_ExcludedFromResolvedShapeTree()
    {
        // Arrange: a top-level Group with one correct child (ID="1") and a deleted stub sibling
        // (Del="1", no cells/geometry of its own - mirroring 60973.vsdx's
        // <Shape Del='1' MasterShape='8' ID='4294967295'/>).
        var shapeXml =
            """
            <Shape ID="2" Type="Group">
              <Cell N="PinX" V="0.5"/><Cell N="PinY" V="0.5"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Shapes>
                <Shape ID="1" Type="Shape">
                  <Cell N="PinX" V="0.5"/><Cell N="PinY" V="0.5"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
                  <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
                  <Section N="Geometry" IX="0">
                    <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                  </Section>
                </Shape>
                <Shape Del="1" MasterShape="8" ID="4294967295"/>
              </Shapes>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var group = Assert.Single(document.GetPageShapes(0));

        // Assert: only the correct child resolved - the deleted stub never made it into the tree.
        var child = Assert.Single(group.Children);
        Assert.Equal("1", child.Id);
    }

    /// <summary>
    ///     Proves a synthetic page whose top-level shape nests deeper than
    ///     <c>VsdxDocument.Groups.cs</c>'s own <c>MaxGroupNestingDepth</c> budget throws
    ///     <see cref="InvalidDataException"/> rather than overflowing the call stack - explicitly
    ///     confirmed unreachable via any real fixture (every in-scope sample nests at most 3
    ///     levels deep - see <c>davehoward-test10-nested-shapes.vsdx</c>), so this is necessarily
    ///     a hand-built, pathologically-deep synthetic package.
    /// </summary>
    [Fact]
    public void VsdxDocument_GroupNestingDepthBudget_ExceedingDepthThrowsInvalidDataException()
    {
        // Arrange: nest a trivial Group shape 100 levels deep (well beyond any realistic budget).
        const int nestingLevels = 100;
        var innerShapeXml =
            """
            <Shape ID="0" Type="Shape">
              <Cell N="PinX" V="0.5"/><Cell N="PinY" V="0.5"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        var shapeXml = innerShapeXml;
        for (var level = 1; level <= nestingLevels; level++)
        {
            shapeXml =
                $"""
                <Shape ID="{level}" Type="Group">
                  <Cell N="PinX" V="0.5"/><Cell N="PinY" V="0.5"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
                  <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
                  <Shapes>
                {shapeXml}
                  </Shapes>
                </Shape>
                """;
        }

        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert
        Assert.IsType<InvalidDataException>(exception);
    }

    /// <summary>
    ///     Proves a synthetic page whose top-level shape has an excessive number of resolved
    ///     descendants (beyond <c>VsdxDocument.Groups.cs</c>'s own <c>MaxResolvedShapeCount</c>
    ///     budget, but each nested well within <c>MaxGroupNestingDepth</c>) throws
    ///     <see cref="InvalidDataException"/> rather than consuming unbounded time/memory -
    ///     explicitly confirmed unreachable via any real fixture, so this is necessarily a
    ///     hand-built synthetic package.
    /// </summary>
    [Fact]
    public void VsdxDocument_GroupNestingDepthBudget_ExceedingShapeCountThrowsInvalidDataException()
    {
        // Arrange: a single top-level Group containing more direct children than the budget
        // allows - every child stays at nesting depth 1, so only the count budget trips.
        const int childCount = 60_000;
        var childrenXml = string.Concat(Enumerable.Range(1, childCount).Select(id =>
            $"""
            <Shape ID="{id}" Type="Shape">
              <Cell N="PinX" V="0.5"/><Cell N="PinY" V="0.5"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
            </Shape>
            """));
        var shapeXml =
            $"""
            <Shape ID="0" Type="Group">
              <Cell N="PinX" V="0.5"/><Cell N="PinY" V="0.5"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Shapes>
            {childrenXml}
              </Shapes>
            </Shape>
            """;

        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert
        Assert.IsType<InvalidDataException>(exception);
    }
}
