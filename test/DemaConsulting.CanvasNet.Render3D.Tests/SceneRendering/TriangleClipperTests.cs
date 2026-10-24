using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Unit tests for the internal <c>TriangleClipper</c> (TriangleRasterizer unit).
/// </summary>
public class TriangleClipperTests
{
    /// <summary>
    ///     Validates that a triangle crossing the near plane is clipped to a quad lying entirely in front of it.
    /// </summary>
    [Fact]
    public void TriangleClipper_NearPlane_TriangleCrossingPlane_ClipsToFrontPortion()
    {
        // Arrange: A is 1 unit from the eye (behind a near plane at 2); B and C are 3 units away
        ClipVertex[] triangle = [new(0, 0, -1), new(1, 0, -3), new(0, 1, -3)];
        var output = new ClipVertex[TriangleClipper.MaxVertices];

        // Act
        var count = TriangleClipper.Clip(triangle, output, TriangleClipper.NearPlane(2.0));

        // Assert: a quad whose vertices are all at depth >= near, with the cut edges at depth exactly 2
        Assert.Equal(4, count);
        for (var i = 0; i < count; i++)
        {
            Assert.True(output[i].Depth >= 2.0 - 1e-12);
        }

        Assert.Contains(output.Take(count), v => Math.Abs(v.Depth - 2.0) < 1e-12 && Math.Abs(v.X - 0.5) < 1e-12 && Math.Abs(v.Y) < 1e-12);
        Assert.Contains(output.Take(count), v => Math.Abs(v.Depth - 2.0) < 1e-12 && Math.Abs(v.X) < 1e-12 && Math.Abs(v.Y - 0.5) < 1e-12);
    }

    /// <summary>
    ///     Validates that a triangle fully behind the near plane produces nothing, and one fully in front is unchanged.
    /// </summary>
    [Fact]
    public void TriangleClipper_NearPlane_FullyBehind_ProducesNothing()
    {
        // Arrange
        ClipVertex[] behind = [new(0, 0, -1), new(1, 0, -1.5), new(0, 1, -1.9)];
        ClipVertex[] front = [new(0, 0, -5), new(1, 0, -5), new(0, 1, -5)];
        var output = new ClipVertex[TriangleClipper.MaxVertices];

        // Act / Assert
        Assert.Equal(0, TriangleClipper.Clip(behind, output, TriangleClipper.NearPlane(2.0)));
        Assert.Equal(3, TriangleClipper.Clip(front, output, TriangleClipper.NearPlane(2.0)));
        Assert.Equal(front[0], output[0]);
    }

    /// <summary>
    ///     Validates that two triangles sharing an edge (traversed in opposite directions) compute a
    ///     bit-identical intersection point on it.
    /// </summary>
    [Fact]
    public void TriangleClipper_SharedEdgeIntersection_IsBitIdenticalForBothNeighbors()
    {
        // Arrange: edge A-B crosses the near plane; each triangle has its own third vertex in front
        var a = new ClipVertex(0.3, 0.7, -5.0);
        var b = new ClipVertex(1.1, -0.4, -0.5);
        var c = new ClipVertex(-0.9, 0.2, -4.0);
        var d = new ClipVertex(1.7, 0.9, -6.0);
        ClipVertex[] first = [a, b, c];
        ClipVertex[] second = [b, a, d];
        var out1 = new ClipVertex[TriangleClipper.MaxVertices];
        var out2 = new ClipVertex[TriangleClipper.MaxVertices];
        var plane = TriangleClipper.NearPlane(1.0);

        // Act
        var n1 = TriangleClipper.Clip(first, out1, plane);
        var n2 = TriangleClipper.Clip(second, out2, plane);

        // Assert: the vertices on the near plane that lie on edge AB are exactly equal in both outputs
        var shared = out1.Take(n1).Intersect(out2.Take(n2)).Where(v => Math.Abs(v.Depth - 1.0) < 1e-9).ToList();
        Assert.Single(shared);
    }

    /// <summary>
    ///     Validates that a convex polygon fully inside every plane is returned unchanged by a side plane clip.
    /// </summary>
    [Fact]
    public void TriangleClipper_Clip_PolygonInsidePlane_IsUnchanged()
    {
        // Arrange: a plane x >= -10 keeps the whole triangle
        ClipVertex[] triangle = [new(0, 0, -1), new(1, 0, -1), new(0, 1, -1)];
        var output = new ClipVertex[TriangleClipper.MaxVertices];

        // Act
        var count = TriangleClipper.Clip(triangle, output, new ClipPlane(1.0, 0.0, 0.0, 10.0));

        // Assert
        Assert.Equal(3, count);
        Assert.Equal(triangle[0], output[0]);
        Assert.Equal(triangle[1], output[1]);
        Assert.Equal(triangle[2], output[2]);
    }
}
