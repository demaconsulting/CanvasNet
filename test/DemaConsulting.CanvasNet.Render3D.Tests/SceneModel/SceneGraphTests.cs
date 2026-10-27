using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneModel;

/// <summary>
///     Unit tests for the SceneGraph unit: <see cref="SceneNode"/>, <see cref="MeshNode"/>,
///     <see cref="GroupNode"/>, <see cref="NodeAppearance"/> and <see cref="Scene"/>.
/// </summary>
public class SceneGraphTests
{
    /// <summary>A shared unit-cube mesh for node tests.</summary>
    private static readonly Mesh UnitCube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);

    /// <summary>
    ///     Validates that a null mesh is rejected by <see cref="MeshNode"/>.
    /// </summary>
    [Fact]
    public void MeshNode_Constructor_NullMesh_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new MeshNode(null!));
    }

    /// <summary>
    ///     Validates that a null child collection is rejected by <see cref="GroupNode"/>.
    /// </summary>
    [Fact]
    public void GroupNode_Constructor_NullChildren_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new GroupNode(null!));
    }

    /// <summary>
    ///     Validates that a null child element is rejected.
    /// </summary>
    [Fact]
    public void GroupNode_Constructor_NullChildElement_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new GroupNode([new MeshNode(UnitCube), null!]));
    }

    /// <summary>
    ///     Validates that the group copies its children so later source changes do not affect it.
    /// </summary>
    [Fact]
    public void GroupNode_Constructor_CopiesChildren()
    {
        // Arrange
        var source = new List<SceneNode> { new MeshNode(UnitCube) };
        var group = new GroupNode(source);

        // Act
        source.Add(new MeshNode(UnitCube));

        // Assert
        Assert.Single(group.Children);
    }

    /// <summary>
    ///     Validates that an empty group is allowed.
    /// </summary>
    [Fact]
    public void GroupNode_Constructor_Empty_Allowed()
    {
        // Arrange / Act
        var group = new GroupNode([]);

        // Assert
        Assert.Empty(group.Children);
    }

    /// <summary>
    ///     Validates that a null root is rejected by <see cref="Scene"/>.
    /// </summary>
    [Fact]
    public void Scene_Constructor_NullRoot_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new Scene(null!));
    }

    /// <summary>
    ///     Validates that a non-finite transform is rejected at initialization.
    /// </summary>
    [Fact]
    public void SceneNode_Transform_NonFiniteMatrix_ThrowsArgumentException()
    {
        // Arrange
        var nan = Matrix4x4D.CreateTranslation(new Vector3D(double.NaN, 0, 0));

        // Act / Assert
        Assert.Throws<ArgumentException>(() => new MeshNode(UnitCube) { Transform = nan });
        Assert.Throws<ArgumentException>(() => new GroupNode([]) { Transform = nan });
    }

    /// <summary>
    ///     Validates that the default node transform is the identity (not the zero matrix).
    /// </summary>
    [Fact]
    public void SceneNode_Transform_DefaultIsIdentity()
    {
        // Arrange / Act
        var node = new MeshNode(UnitCube);

        // Assert
        Assert.Equal(Matrix4x4D.Identity, node.Transform);
        Assert.Null(node.Appearance);
    }

    /// <summary>
    ///     Validates that nested transforms compose child first, then parent.
    /// </summary>
    [Fact]
    public void Scene_EnumerateInstances_NestedTransforms_ComposeChildThenParent()
    {
        // Arrange: child scales by 2, parent translates by (10,0,0) => unit corner (1,1,1) -> (2,2,2) -> (12,2,2)
        var child = new MeshNode(UnitCube) { Transform = Matrix4x4D.CreateScale(2.0) };
        var parent = new GroupNode([child]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(10, 0, 0)) };
        var scene = new Scene(parent);

        // Act
        var instance = Assert.Single(scene.EnumerateInstances());

        // Assert
        Render3DTestHelpers.AssertVector(new Vector3D(12, 2, 2), instance.World.TransformPoint(Vector3D.One));
        Assert.Same(UnitCube, instance.Mesh);
    }

    /// <summary>
    ///     Validates that instance order follows a stable depth-first pre-order.
    /// </summary>
    [Fact]
    public void Scene_EnumerateInstances_PreOrderIndexIsStable()
    {
        // Arrange: root[ a, group[ b, c ], d ] => order a, b, c, d
        var a = new MeshNode(UnitCube) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(1, 0, 0)) };
        var b = new MeshNode(UnitCube) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(2, 0, 0)) };
        var c = new MeshNode(UnitCube) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(3, 0, 0)) };
        var d = new MeshNode(UnitCube) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(4, 0, 0)) };
        var scene = new Scene(new GroupNode([a, new GroupNode([b, c]), d]));

        // Act
        var first = scene.EnumerateInstances().ToList();
        var second = scene.EnumerateInstances().ToList();

        // Assert
        Assert.Equal([0, 1, 2, 3], first.Select(i => i.Order));
        Assert.Equal([1.0, 2.0, 3.0, 4.0], first.Select(i => i.World.M41));
        Assert.Equal(first.Select(i => i.World.M41), second.Select(i => i.World.M41));
    }

    /// <summary>
    ///     Validates that a child shared by two parents is enumerated once per path.
    /// </summary>
    [Fact]
    public void Scene_EnumerateInstances_SharedChild_EnumeratedPerPath()
    {
        // Arrange: the same node under two differently translated groups
        var shared = new MeshNode(UnitCube);
        var left = new GroupNode([shared]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(-5, 0, 0)) };
        var right = new GroupNode([shared]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(5, 0, 0)) };
        var scene = new Scene(new GroupNode([left, right]));

        // Act
        var instances = scene.EnumerateInstances().ToList();

        // Assert
        Assert.Equal(2, instances.Count);
        Assert.Equal(-5.0, instances[0].World.M41);
        Assert.Equal(5.0, instances[1].World.M41);
    }

    /// <summary>
    ///     Validates that the innermost appearance wins over outer ones.
    /// </summary>
    [Fact]
    public void Scene_EnumerateInstances_InnermostAppearanceWins()
    {
        // Arrange: outer red, inner group blue, one mesh green, one inheriting
        var red = new NodeAppearance(new Rgba32(255, 0, 0, 255));
        var blue = new NodeAppearance(new Rgba32(0, 0, 255, 255));
        var green = new NodeAppearance(new Rgba32(0, 255, 0, 255));
        var inner = new GroupNode([new MeshNode(UnitCube) { Appearance = green }, new MeshNode(UnitCube)]) { Appearance = blue };
        var outer = new GroupNode([inner, new MeshNode(UnitCube)]) { Appearance = red };

        // Act
        var instances = new Scene(outer).EnumerateInstances().ToList();

        // Assert: green (own), blue (parent), red (grandparent's direct child)
        Assert.Equal(green, instances[0].Appearance);
        Assert.Equal(blue, instances[1].Appearance);
        Assert.Equal(red, instances[2].Appearance);
    }

    /// <summary>
    ///     Validates that without any appearance the instance reports null (renderer default color applies).
    /// </summary>
    [Fact]
    public void Scene_EnumerateInstances_NoAppearance_ReportsNull()
    {
        // Arrange
        var scene = new Scene(new GroupNode([new MeshNode(UnitCube)]));

        // Act
        var instance = Assert.Single(scene.EnumerateInstances());

        // Assert
        Assert.Null(instance.Appearance);
    }

    /// <summary>
    ///     Validates that bounds of a transformed group match the analytic transformed corners.
    /// </summary>
    [Fact]
    public void Scene_ComputeBounds_TransformedGroup_MatchesAnalytic()
    {
        // Arrange: unit cube scaled by (2,3,4) then moved by (10,20,30)
        var node = new MeshNode(UnitCube) { Transform = Matrix4x4D.CreateScale(new Vector3D(2, 3, 4)) };
        var group = new GroupNode([node]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(10, 20, 30)) };

        // Act
        var bounds = new Scene(group).ComputeBounds();

        // Assert
        Assert.Equal(new Vector3D(10, 20, 30), bounds.Min);
        Assert.Equal(new Vector3D(12, 23, 34), bounds.Max);
    }

    /// <summary>
    ///     Validates that an empty group has empty bounds.
    /// </summary>
    [Fact]
    public void Scene_ComputeBounds_EmptyGroup_IsEmpty()
    {
        // Arrange / Act
        var bounds = new Scene(new GroupNode([])).ComputeBounds();

        // Assert
        Assert.True(bounds.IsEmpty);
    }

    /// <summary>
    ///     Validates that repeated bounds computation returns the same value.
    /// </summary>
    [Fact]
    public void Scene_ComputeBounds_CalledTwice_ReturnsSame()
    {
        // Arrange
        var scene = new Scene(new MeshNode(UnitCube) { Transform = Matrix4x4D.CreateRotationZ(33.0) });

        // Act
        var first = scene.ComputeBounds();
        var second = scene.ComputeBounds();

        // Assert
        Assert.Equal(first, second);
        Assert.False(first.IsEmpty);
    }

    /// <summary>
    ///     Validates that very deep nesting is enumerated without recursion-related stack overflow.
    /// </summary>
    [Fact]
    public void Scene_EnumerateInstances_TenThousandNestedGroups_DoesNotOverflowStack()
    {
        // Arrange: 10 000 nested groups each translating by +1 in X around one mesh
        SceneNode node = new MeshNode(UnitCube);
        for (var i = 0; i < 10_000; i++)
        {
            node = new GroupNode([node]) { Transform = Matrix4x4D.CreateTranslation(Vector3D.UnitX) };
        }

        var scene = new Scene(node);

        // Act
        var instance = Assert.Single(scene.EnumerateInstances());
        var bounds = scene.ComputeBounds();

        // Assert
        Assert.Equal(10_000.0, instance.World.M41);
        Assert.Equal(10_000.0, bounds.Min.X);
    }

    /// <summary>
    ///     Validates that a null child collection is rejected by <see cref="CsgNode"/>.
    /// </summary>
    [Fact]
    public void CsgNode_Constructor_NullChildren_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new CsgNode(CsgOperation.Union, null!));
    }

    /// <summary>
    ///     Validates that a null child element is rejected by <see cref="CsgNode"/>.
    /// </summary>
    [Fact]
    public void CsgNode_Constructor_NullChildElement_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new CsgNode(CsgOperation.Difference, [new MeshNode(UnitCube), null!]));
    }

    /// <summary>
    ///     Validates that an undefined operation value is rejected by <see cref="CsgNode"/>.
    /// </summary>
    [Fact]
    public void CsgNode_Constructor_UndefinedOperation_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsgNode((CsgOperation)3, [new MeshNode(UnitCube)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsgNode((CsgOperation)(-1), []));
    }

    /// <summary>
    ///     Validates that <see cref="CsgNode"/> keeps its operation and copies its children so later source changes do not affect it.
    /// </summary>
    [Theory]
    [InlineData(CsgOperation.Union)]
    [InlineData(CsgOperation.Difference)]
    [InlineData(CsgOperation.Intersection)]
    public void CsgNode_Constructor_KeepsOperationAndCopiesChildren(CsgOperation operation)
    {
        // Arrange
        var first = new MeshNode(UnitCube);
        var second = new MeshNode(UnitCube);
        var source = new List<SceneNode> { first, second };
        var node = new CsgNode(operation, source);

        // Act
        source.Clear();
        source.Add(new MeshNode(UnitCube));

        // Assert
        Assert.Equal(operation, node.Operation);
        Assert.Equal(2, node.Children.Count);
        Assert.Same(first, node.Children[0]);
        Assert.Same(second, node.Children[1]);
    }

    /// <summary>
    ///     Validates that a childless <see cref="CsgNode"/> is allowed for every operation, is empty and has empty bounds.
    /// </summary>
    [Theory]
    [InlineData(CsgOperation.Union)]
    [InlineData(CsgOperation.Difference)]
    [InlineData(CsgOperation.Intersection)]
    public void CsgNode_Constructor_Empty_AllowedAndEmptyForEveryOperation(CsgOperation operation)
    {
        // Arrange / Act
        var node = new CsgNode(operation, []);
        var scene = new Scene(node);

        // Assert
        Assert.Empty(node.Children);
        Assert.Empty(scene.EnumerateInstances());
        Assert.True(scene.ComputeBounds().IsEmpty);
    }

    /// <summary>
    ///     Validates that a single-child <see cref="CsgNode"/> enumerates exactly that child's instances for every operation.
    /// </summary>
    [Theory]
    [InlineData(CsgOperation.Union)]
    [InlineData(CsgOperation.Difference)]
    [InlineData(CsgOperation.Intersection)]
    public void CsgNode_SingleChild_EnumeratesThatChild(CsgOperation operation)
    {
        // Arrange
        var child = new MeshNode(UnitCube);
        var scene = new Scene(new CsgNode(operation, [child]));

        // Act
        var instance = Assert.Single(scene.EnumerateInstances());

        // Assert
        Assert.Same(child, instance.Node);
        Assert.Same(UnitCube, instance.Mesh);
    }

    /// <summary>
    ///     Validates that a non-finite transform is rejected on a <see cref="CsgNode"/> like on every other node.
    /// </summary>
    [Fact]
    public void CsgNode_Transform_NonFiniteMatrix_ThrowsArgumentException()
    {
        // Arrange
        var nan = Matrix4x4D.CreateTranslation(new Vector3D(double.NaN, 0, 0));

        // Act / Assert
        Assert.Throws<ArgumentException>(() => new CsgNode(CsgOperation.Union, []) { Transform = nan });
        Assert.Equal(Matrix4x4D.Identity, new CsgNode(CsgOperation.Union, []).Transform);
    }

    /// <summary>
    ///     Validates that the traversal descends into a <see cref="CsgNode"/> (operands are never silently dropped), in pre-order, mixed with groups.
    /// </summary>
    [Fact]
    public void Scene_EnumerateInstances_CsgNode_DescendsIntoOperandsInPreOrder()
    {
        // Arrange: root group[ a, csg-difference[ b, group[ c, csg-intersection[ d, e ] ] ], f ]
        var nodes = Enumerable.Range(1, 6)
            .Select(i => new MeshNode(UnitCube) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(i, 0, 0)) })
            .ToArray();
        var intersection = new CsgNode(CsgOperation.Intersection, [nodes[3], nodes[4]]);
        var difference = new CsgNode(CsgOperation.Difference, [nodes[1], new GroupNode([nodes[2], intersection])]);
        var scene = new Scene(new GroupNode([nodes[0], difference, nodes[5]]));

        // Act
        var instances = scene.EnumerateInstances().ToList();

        // Assert: all six are present, in pre-order, and each reports its source node
        Assert.Equal([0, 1, 2, 3, 4, 5], instances.Select(i => i.Order));
        Assert.Equal([1.0, 2.0, 3.0, 4.0, 5.0, 6.0], instances.Select(i => i.World.M41));
        Assert.Equal(nodes, instances.Select(i => i.Node));
    }

    /// <summary>
    ///     Validates that a child shared by two CSG parents is enumerated once per path with each path's transform.
    /// </summary>
    [Fact]
    public void Scene_EnumerateInstances_CsgNode_SharedChild_EnumeratedPerPath()
    {
        // Arrange: one shared node under a union and a difference node at different offsets
        var shared = new MeshNode(UnitCube);
        var left = new CsgNode(CsgOperation.Union, [shared]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(-5, 0, 0)) };
        var right = new CsgNode(CsgOperation.Difference, [shared, new MeshNode(UnitCube)]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(5, 0, 0)) };

        // Act
        var instances = new Scene(new GroupNode([left, right])).EnumerateInstances().ToList();

        // Assert
        Assert.Equal(3, instances.Count);
        Assert.Same(shared, instances[0].Node);
        Assert.Same(shared, instances[1].Node);
        Assert.Equal(-5.0, instances[0].World.M41);
        Assert.Equal(5.0, instances[1].World.M41);
    }

    /// <summary>
    ///     Validates that a <see cref="CsgNode"/> composes its transform child-then-parent and passes its appearance to operands without one.
    /// </summary>
    [Fact]
    public void Scene_EnumerateInstances_CsgNode_ComposesTransformAndInheritsAppearance()
    {
        // Arrange: operand scales by 2, CSG node translates by (10,0,0), grandparent group is blue
        var red = new NodeAppearance(new Rgba32(255, 0, 0, 255));
        var blue = new NodeAppearance(new Rgba32(0, 0, 255, 255));
        var scaled = new MeshNode(UnitCube) { Transform = Matrix4x4D.CreateScale(2.0) };
        var own = new MeshNode(UnitCube) { Appearance = red };
        var csg = new CsgNode(CsgOperation.Difference, [scaled, own]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(10, 0, 0)) };
        var scene = new Scene(new GroupNode([csg]) { Appearance = blue });

        // Act
        var instances = scene.EnumerateInstances().ToList();

        // Assert: unit corner (1,1,1) -> (2,2,2) -> (12,2,2); appearance blue inherited, red own
        Render3DTestHelpers.AssertVector(new Vector3D(12, 2, 2), instances[0].World.TransformPoint(Vector3D.One));
        Assert.Equal(blue, instances[0].Appearance);
        Assert.Equal(red, instances[1].Appearance);

        // A CSG node's own appearance applies to its operands as well
        var tinted = new Scene(new CsgNode(CsgOperation.Union, [new MeshNode(UnitCube)]) { Appearance = red });
        Assert.Equal(red, Assert.Single(tinted.EnumerateInstances()).Appearance);
    }

    /// <summary>
    ///     Validates that scene bounds include every operand of a difference or intersection (conservative bounds).
    /// </summary>
    [Fact]
    public void Scene_ComputeBounds_CsgNode_IncludesAllOperands()
    {
        // Arrange: a unit cube minus a cube far away still reports both operands in the bounds
        var far = new MeshNode(UnitCube) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(9, 0, 0)) };
        var difference = new Scene(new CsgNode(CsgOperation.Difference, [new MeshNode(UnitCube), far]));
        var intersection = new Scene(new CsgNode(CsgOperation.Intersection, [new MeshNode(UnitCube), far]));

        // Act
        var differenceBounds = difference.ComputeBounds();
        var intersectionBounds = intersection.ComputeBounds();

        // Assert
        Assert.Equal(new Vector3D(0, 0, 0), differenceBounds.Min);
        Assert.Equal(new Vector3D(10, 1, 1), differenceBounds.Max);
        Assert.Equal(differenceBounds, intersectionBounds);
    }

    /// <summary>
    ///     Validates that a scene nested ten thousand CSG nodes deep is enumerated without stack overflow.
    /// </summary>
    [Fact]
    public void Scene_EnumerateInstances_TenThousandNestedCsgNodes_DoesNotOverflowStack()
    {
        // Arrange: alternate operations around one mesh, translating +1 in X each level
        SceneNode node = new MeshNode(UnitCube);
        for (var i = 0; i < 10_000; i++)
        {
            var operation = (CsgOperation)(i % 3);
            node = new CsgNode(operation, [node]) { Transform = Matrix4x4D.CreateTranslation(Vector3D.UnitX) };
        }

        var scene = new Scene(node);

        // Act
        var instance = Assert.Single(scene.EnumerateInstances());
        var bounds = scene.ComputeBounds();

        // Assert
        Assert.Equal(10_000.0, instance.World.M41);
        Assert.Equal(10_000.0, bounds.Min.X);
    }

    /// <summary>
    ///     Validates that <see cref="OverlayNode"/> copies its children and rejects a null enumerable or child.
    /// </summary>
    [Fact]
    public void OverlayNode_Constructor_CopiesChildren_RejectsNullChild_AndNullEnumerable()
    {
        // Arrange
        var color = new Rgba32(10, 20, 30, 40);
        var source = new List<SceneNode> { new MeshNode(UnitCube) };

        // Act
        var overlay = new OverlayNode(color, source);
        source.Add(new MeshNode(UnitCube));

        // Assert
        Assert.Equal(color, overlay.Color);
        Assert.Single(overlay.Children);
        Assert.Empty(new OverlayNode(color, []).Children);
        Assert.Throws<ArgumentNullException>(() => new OverlayNode(color, null!));
        Assert.Throws<ArgumentException>(() => new OverlayNode(color, [new MeshNode(UnitCube), null!]));
    }

    /// <summary>
    ///     Validates that an overlay forces its color over every inner appearance and marks its instances.
    /// </summary>
    [Fact]
    public void OverlayNode_EnumerateInstances_ForcesOverlayColorOverInnerAppearances_AndMarksInOverlay()
    {
        // Arrange: a plain red mesh beside an overlay whose children carry their own appearances
        var red = new NodeAppearance(new Rgba32(255, 0, 0, 255));
        var green = new NodeAppearance(new Rgba32(0, 255, 0, 255));
        var overlayColor = new Rgba32(1, 2, 3, 77);
        var overlay = new OverlayNode(
            overlayColor,
            [new MeshNode(UnitCube) { Appearance = green }, new GroupNode([new MeshNode(UnitCube)]) { Appearance = green }])
        {
            Appearance = red,
        };
        var scene = new Scene(new GroupNode([new MeshNode(UnitCube) { Appearance = red }, overlay]));

        // Act
        var instances = scene.EnumerateInstances().ToList();

        // Assert: the first is outside the overlay, the other two are forced to the overlay color
        Assert.Equal(3, instances.Count);
        Assert.False(instances[0].InOverlay);
        Assert.Equal(red, instances[0].Appearance);
        Assert.All(instances.Skip(1), i => Assert.True(i.InOverlay));
        Assert.All(instances.Skip(1), i => Assert.Equal(overlayColor, i.Appearance!.Color));
        Assert.Equal([0, 1, 2], instances.Select(i => i.Order));
    }

    /// <summary>
    ///     Validates that an overlay nested inside an overlay is a plain group and the outer color wins.
    /// </summary>
    [Fact]
    public void OverlayNode_NestedOverlay_ActsAsGroupWithOuterColor()
    {
        // Arrange
        var outerColor = new Rgba32(200, 0, 0, 90);
        var innerColor = new Rgba32(0, 0, 200, 200);
        var inner = new OverlayNode(innerColor, [new MeshNode(UnitCube)]);
        var scene = new Scene(new OverlayNode(outerColor, [inner, new MeshNode(UnitCube)]));

        // Act
        var instances = scene.EnumerateInstances().ToList();

        // Assert
        Assert.Equal(2, instances.Count);
        Assert.All(instances, i => Assert.True(i.InOverlay));
        Assert.All(instances, i => Assert.Equal(outerColor, i.Appearance!.Color));
    }

    /// <summary>
    ///     Validates that an overlay's transform composes like a group's.
    /// </summary>
    [Fact]
    public void OverlayNode_Transform_ComposesLikeGroup()
    {
        // Arrange
        var mesh = new MeshNode(UnitCube) { Transform = Matrix4x4D.CreateScale(2.0) };
        var overlay = new OverlayNode(new Rgba32(1, 1, 1, 255), [mesh]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(10, 0, 0)) };
        var scene = new Scene(new GroupNode([overlay]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 5, 0)) });

        // Act
        var instance = Assert.Single(scene.EnumerateInstances());

        // Assert: (1,1,1) -> (2,2,2) -> (12,2,2) -> (12,7,2)
        Assert.Equal(new Vector3D(12, 7, 2), instance.World.TransformPoint(Vector3D.One));
    }

    /// <summary>
    ///     Validates that bounds without overlays skip overlay instances.
    /// </summary>
    [Fact]
    public void Scene_ComputeBounds_WithoutOverlays_SkipsOverlayInstances()
    {
        // Arrange: a unit cube at the origin and an overlay cube far away
        var far = new OverlayNode(new Rgba32(1, 1, 1, 255), [new MeshNode(UnitCube) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(100, 0, 0)) }]);
        var scene = new Scene(new GroupNode([new MeshNode(UnitCube), far]));

        // Act
        var without = scene.ComputeBounds(false);
        var withOverlays = scene.ComputeBounds(true);
        var onlyOverlays = new Scene(far).ComputeBounds(false);

        // Assert (repeated calls hit both caches)
        Assert.Equal(Vector3D.One, without.Max);
        Assert.Equal(101.0, withOverlays.Max.X);
        Assert.True(onlyOverlays.IsEmpty);
        Assert.Equal(without, scene.ComputeBounds(false));
        Assert.Equal(withOverlays, scene.ComputeBounds(true));
    }

    /// <summary>
    ///     Validates that the default bounds include overlays, equal to <c>ComputeBounds(true)</c>.
    /// </summary>
    [Fact]
    public void Scene_ComputeBounds_Default_IncludesOverlays()
    {
        // Arrange
        var far = new OverlayNode(new Rgba32(1, 1, 1, 255), [new MeshNode(UnitCube) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(100, 0, 0)) }]);
        var scene = new Scene(new GroupNode([new MeshNode(UnitCube), far]));

        // Act / Assert
        Assert.Equal(scene.ComputeBounds(true), scene.ComputeBounds());
        Assert.Equal(101.0, scene.ComputeBounds().Max.X);
    }

    /// <summary>
    ///     Validates that very deep overlay nesting is enumerated without recursion-related stack overflow.
    /// </summary>
    [Fact]
    public void Scene_EnumerateInstances_DeepOverlayNesting_IsIterative()
    {
        // Arrange: 10 000 nested overlays, each translating by +1 in X around one mesh
        SceneNode node = new MeshNode(UnitCube);
        for (var i = 0; i < 10_000; i++)
        {
            node = new OverlayNode(new Rgba32(9, 9, 9, 100), [node]) { Transform = Matrix4x4D.CreateTranslation(Vector3D.UnitX) };
        }

        var scene = new Scene(node);

        // Act
        var instance = Assert.Single(scene.EnumerateInstances());

        // Assert
        Assert.Equal(10_000.0, instance.World.M41);
        Assert.True(instance.InOverlay);
        Assert.Equal(10_000.0, scene.ComputeBounds(true).Min.X);
        Assert.True(scene.ComputeBounds(false).IsEmpty);
    }
}
