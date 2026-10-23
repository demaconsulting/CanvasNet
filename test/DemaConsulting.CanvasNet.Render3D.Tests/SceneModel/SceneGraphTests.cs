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
    ///     Validates that without any appearance the instance reports null (renderer default colour applies).
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
}
