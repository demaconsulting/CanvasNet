using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneModel;

/// <summary>
///     Subsystem-level tests for the SceneModel subsystem, exercising the math, mesh and
///     scene-graph units together.
/// </summary>
public class SceneModelTests
{
    /// <summary>
    ///     Validates that a rotate-translate-scale chain across nested nodes composes in double
    ///     precision and keeps sub-millimetre detail at a million-unit offset.
    /// </summary>
    [Fact]
    public void SceneModel_TransformChain_ComposesInDouble()
    {
        // Arrange: a 0.001-unit cube rotated 90 degrees about Z, then moved 1e6 units away
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(0.001, 0.001, 0.001));
        var node = new MeshNode(cube) { Transform = Matrix4x4D.CreateRotationZ(90.0) };
        var group = new GroupNode([node]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(1e6, 0, 0)) };

        // Act
        var bounds = new Scene(group).ComputeBounds();

        // Assert: rotation by 90 degrees maps x in [0,0.001] to y in [0,0.001] and y to x in [-0.001,0]
        Assert.Equal(1e6 - 0.001, bounds.Min.X, 1e-9);
        Assert.Equal(1e6, bounds.Max.X, 1e-9);
        Assert.Equal(0.001, bounds.Size.Y, 1e-9);
    }

    /// <summary>
    ///     Validates that a mirrored mesh inside a group, once flattened, still has a positive
    ///     volume and a closed surface.
    /// </summary>
    [Fact]
    public void SceneModel_MirroredMeshInGroup_KeepsPositiveVolumeAndClosed()
    {
        // Arrange: a cube under a group that mirrors X
        var cube = Render3DTestHelpers.CreateBoxMesh(new Vector3D(1, 1, 1), Vector3D.One);
        var scene = new Scene(new GroupNode([new MeshNode(cube)]) { Transform = Matrix4x4D.CreateMirror(Vector3D.UnitX) });

        // Act
        var instance = Assert.Single(scene.EnumerateInstances());
        var flattened = instance.Mesh.Transform(instance.World);

        // Assert
        Assert.True(instance.World.Determinant < 0.0);
        Assert.Equal(1.0, flattened.SignedVolume, 1e-12);
        Assert.True(flattened.IsClosed);
    }

    /// <summary>
    ///     Validates that nested groups produce analytically correct scene bounds.
    /// </summary>
    [Fact]
    public void SceneModel_NestedGroups_BoundsMatchAnalytic()
    {
        // Arrange: two unit cubes, one shifted by (3,0,0) inside an inner group shifted by (0,4,0)
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var inner = new GroupNode([new MeshNode(cube) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(3, 0, 0)) }])
        {
            Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 4, 0)),
        };
        var scene = new Scene(new GroupNode([new MeshNode(cube), inner]));

        // Act
        var bounds = scene.ComputeBounds();

        // Assert
        Assert.Equal(Vector3D.Zero, bounds.Min);
        Assert.Equal(new Vector3D(4, 5, 1), bounds.Max);
    }
}
