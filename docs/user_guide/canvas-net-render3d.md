# CanvasNet Render3D Package

## API Reference

### 3D Rendering Overview

The `DemaConsulting.CanvasNet.Render3D` package is a separate, optional NuGet package containing a
pure-managed software 3D rasterizer. It depends only on the core `CanvasNet` package (for `Surface`
and `Rgba32`) and has no knowledge of any 3D file format. Its types live in three namespaces:

- `DemaConsulting.CanvasNet.Render3D.SceneModel` - double-precision math, meshes, and the scene graph
- `DemaConsulting.CanvasNet.Render3D.Tessellation` - primitive mesh factories, 2D polygon regions,
  polygon triangulation, and linear and rotational extrusion
- `DemaConsulting.CanvasNet.Render3D.SceneRendering` - camera, lights, options, and the renderer

Scenes can combine solids with constructive solid geometry (union, difference, and intersection of
closed meshes) through `CsgNode`; see *Constructive Solid Geometry*. A scene can also hold translucent
surfaces and independent overlay bodies (ghosts and highlights; see *Transparency and Overlays*), and the
tessellation namespace builds solids by linear or rotational polygon extrusion (see *Extruder*), exact convex
hulls (see *ConvexHull*), and Minkowski sums of convex solids (see *MinkowskiSum*).

The conventions used throughout: world space is right-handed with +Z up; the camera looks down -Z
in view space; a triangle whose vertices are counter-clockwise as seen from outside faces outward;
matrices use the same row-vector convention as `System.Numerics.Matrix4x4` (a point transforms as
`v * M`, and `a * b` applies `a` first); and angles are in degrees. All model-space math is done in
`double` precision, so scenes positioned far from the origin render without precision loss.

### Vector3D, Matrix4x4D, and BoundingBox3D

`Vector2D`, `Vector3D`, `Matrix4x4D`, and `BoundingBox3D` are immutable double-precision value types.

```csharp
public readonly record struct Vector3D(double X, double Y, double Z)
{
    public static Vector3D Zero { get; }
    public static Vector3D One { get; }
    public static Vector3D UnitX { get; }
    public static Vector3D UnitY { get; }
    public static Vector3D UnitZ { get; }
    public double Length { get; }
    public bool IsFinite { get; }
    public static double Dot(Vector3D left, Vector3D right);
    public static Vector3D Cross(Vector3D left, Vector3D right);
    public Vector3D Normalize(); // a zero-length vector returns Vector3D.Zero
    public Vector3 ToVector3(); // explicit narrowing to System.Numerics.Vector3
    // plus +, -, unary -, * and / by a scalar
}

public readonly struct Matrix4x4D : IEquatable<Matrix4x4D>
{
    public static Matrix4x4D Identity { get; } // default(Matrix4x4D) is the ZERO matrix
    public static Matrix4x4D CreateTranslation(Vector3D offset);
    public static Matrix4x4D CreateScale(Vector3D scale);
    public static Matrix4x4D CreateScale(double scale);
    public static Matrix4x4D CreateRotationX(double degrees); // also Y and Z
    public static Matrix4x4D CreateFromAxisAngle(Vector3D axis, double degrees);
    public static Matrix4x4D CreateMirror(Vector3D planeNormal);
    public static Matrix4x4D FromRowMajor(ReadOnlySpan<double> elements); // exactly 16 elements
    public static Matrix4x4D operator *(Matrix4x4D left, Matrix4x4D right); // left is applied first
    public double Determinant { get; }
    public bool IsFinite { get; }
    public bool TryInvert(out Matrix4x4D inverse);
    public Vector3D TransformPoint(Vector3D point);
    public Vector3D TransformDirection(Vector3D direction); // ignores translation
}

public readonly record struct BoundingBox3D(Vector3D Min, Vector3D Max)
{
    public static BoundingBox3D Empty { get; }
    public bool IsEmpty { get; }
    public Vector3D Size { get; }
    public Vector3D Center { get; }
    public double Diagonal { get; }
    public BoundingBox3D Union(BoundingBox3D other);
    public BoundingBox3D Union(Vector3D point);
    public BoundingBox3D Transform(Matrix4x4D matrix); // axis-aligned box of the transformed corners
}
```

Rotations by multiples of 90 degrees are exact (no rounding error). A mirror matrix has a negative
determinant; the library compensates by flipping triangle winding, so a mirrored mesh is shaded
like the original.

```csharp
using DemaConsulting.CanvasNet.Render3D.SceneModel;

// Rotate 90 degrees about Z, then move 10 units along X (the left operand is applied first)
var matrix = Matrix4x4D.CreateRotationZ(90) * Matrix4x4D.CreateTranslation(new Vector3D(10, 0, 0));
Console.WriteLine(matrix.TransformPoint(new Vector3D(1, 0, 0))); // X = 10, Y = 1, Z = 0
```

**Exceptions:**

- `ArgumentException`: Thrown by `FromRowMajor` when the span does not hold exactly 16 elements, and by
  `CreateFromAxisAngle`/`CreateMirror` when the axis or normal is zero-length or not finite.

### Mesh and MeshBuilder

`Mesh` is an immutable, validated, indexed triangle mesh: a list of `Vector3D` vertices and a flat
list of triangle indices (three per triangle). The winding contract is counter-clockwise as seen
from outside. `MeshBuilder` assembles a mesh and welds identical vertices.

```csharp
public sealed class Mesh
{
    public Mesh(IEnumerable<Vector3D> vertices, IEnumerable<int> triangleIndices);
    public IReadOnlyList<Vector3D> Vertices { get; }
    public IReadOnlyList<int> TriangleIndices { get; }
    public int VertexCount { get; }
    public int TriangleCount { get; }
    public BoundingBox3D Bounds { get; }
    public bool IsClosed { get; } // every edge shared by exactly two consistently wound triangles
    public double SignedVolume { get; } // positive for a closed, outward-oriented mesh
    public double SurfaceArea { get; }
    public Mesh Transform(Matrix4x4D matrix); // reverses winding when the determinant is negative
    public Mesh Reverse(); // flips every triangle (repairs an inside-out mesh)
}

public sealed class MeshBuilder
{
    public int VertexCount { get; }
    public int TriangleCount { get; }
    public int AddVertex(Vector3D position); // returns the index of an identical existing vertex
    public void AddTriangle(int a, int b, int c);
    public void AddPolygon(ReadOnlySpan<int> polygon); // convex, counter-clockwise; fan-triangulated
    public Mesh Build();
}
```

The `Mesh` constructor copies its inputs, so later changes to the caller's collections have no
effect. A mesh with zero triangles is allowed (it has empty bounds and is not closed).

```csharp
using DemaConsulting.CanvasNet.Render3D.SceneModel;

// A tetrahedron with outward-facing (counter-clockwise) triangles
var builder = new MeshBuilder();
var a = builder.AddVertex(new Vector3D(0, 0, 0));
var b = builder.AddVertex(new Vector3D(1, 0, 0));
var c = builder.AddVertex(new Vector3D(0, 1, 0));
var d = builder.AddVertex(new Vector3D(0, 0, 1));
builder.AddTriangle(a, c, b);
builder.AddTriangle(a, d, c);
builder.AddTriangle(a, b, d);
builder.AddTriangle(b, c, d);
Mesh tetrahedron = builder.Build();
Console.WriteLine($"{tetrahedron.IsClosed} {tetrahedron.SignedVolume}"); // Output: True 0.16666666666666666
```

**Exceptions:**

- `ArgumentNullException`: Thrown by the `Mesh` constructor for a null sequence.
- `ArgumentException`: Thrown by the `Mesh` constructor for an index count that is not a multiple of
  three or a non-finite vertex; by `Transform` for a non-finite matrix or result; by
  `AddVertex` for a non-finite position; by `AddTriangle` for repeated indices; and by `AddPolygon`
  for fewer than three indices or a repeated index.
- `ArgumentOutOfRangeException`: Thrown by the `Mesh` constructor and by `AddTriangle` and
  `AddPolygon` for an index that does not address a vertex.

### PrimitiveTessellator

`PrimitiveTessellator` (namespace `DemaConsulting.CanvasNet.Render3D.Tessellation`) creates welded
meshes for common solids: `Box`, `Sphere`, and `Cylinder` results are closed and outward-oriented,
while `Polyhedron` builds its mesh from the points and faces you supply and preserves them as given.

```csharp
public static class PrimitiveTessellator
{
    public static Mesh Box(Vector3D size, bool center = false);
    public static Mesh Sphere(double radius, int segments);
    public static Mesh Cylinder(double height, double radius1, double radius2, int segments, bool center = false);
    public static Mesh Polyhedron(IReadOnlyList<Vector3D> points, IReadOnlyList<IReadOnlyList<int>> facesCcw);

    // 2D profiles for the Extruder (see PolygonRegion)
    public static PolygonRegion Rectangle(Vector2D size, bool center = false);
    public static PolygonRegion Ellipse(double radiusX, double radiusY, int segments);
    public static PolygonRegion Circle(double radius, int segments);
    public static PolygonRegion Polygon(IReadOnlyList<Vector2D> points, IReadOnlyList<IReadOnlyList<int>>? paths = null);
}
```

- `Box` spans `[0, size]` on each axis, or is centered on the origin when `center` is true.
- `Sphere` is a UV sphere centered on the origin with its poles on the Z axis; `segments` (at
  least 3) is the number of divisions around the Z axis.
- `Cylinder` extends along +Z from 0 to `height` (or symmetrically about 0 when `center` is true).
  `radius1` is the bottom radius and `radius2` the top radius, so unequal radii make a frustum and a
  radius of 0 makes a cone (not both 0).
- `Polyhedron` welds the points and fan-triangulates each face, preserving the faces and winding you
  supply exactly as given: it neither closes the surface nor corrects its orientation, so closure and
  outward orientation are your responsibility. Faces must be **convex** and should be listed
  **counter-clockwise as seen from outside** for an outward-facing result; neither is checked, so
  inspect `Mesh.IsClosed` and `Mesh.SignedVolume`, and for an inside-out polyhedron (negative
  `SignedVolume`) call `Mesh.Reverse()`.
- `Rectangle` is a counter-clockwise rectangle from the origin to `size` (or centered on the origin
  when `center` is true). `Ellipse` and `Circle` are inscribed polygons centered on the origin whose
  vertex `j` is at the angle `2 pi j / segments` counter-clockwise from +X (the same ring as
  `Cylinder`, so extruding `Circle(r, n)` gives the same solid as `Cylinder(h, r, r, n)`); their
  area is slightly below the true ellipse. `Polygon` builds a region from a point list: with no
  `paths` the points form one contour in order, otherwise each path is a list of indices into
  `points` forming one contour (a nested contour is a hole). All four return a `PolygonRegion`.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown for a non-positive or non-finite size, radius, or height,
  fewer than three segments, both cylinder radii zero, a face index outside the point list, or a
  `Polygon` path index outside the point list.
- `ArgumentException`: Thrown by `Polyhedron` for a face with fewer than three indices, a repeated
  index, or two indices that address identical (welded) points, and by the 2D factories for an
  invalid polygon (see *PolygonRegion*).
- `ArgumentNullException`: Thrown by `Polyhedron` for a null point list, face list, or face, and by
  `Polygon` for a null point list or path.

### PolygonRegion

`PolygonRegion` (namespace `DemaConsulting.CanvasNet.Render3D.Tessellation`) is an immutable,
validated 2D region bounded by simple polygon contours, with holes expressed by nesting. It is the
input of `Extruder` and `PolygonTriangulator`.

```csharp
public sealed class PolygonRegion
{
    public PolygonRegion(IEnumerable<IReadOnlyList<Vector2D>> contours);
    public static PolygonRegion Empty { get; }
    public IReadOnlyList<IReadOnlyList<Vector2D>> Contours { get; }
    public IReadOnlyList<Vector2D> Vertices { get; }
    public int VertexCount { get; }
    public bool IsEmpty { get; }
    public double Area { get; }
}
```

- Each contour is a list of at least three distinct points that is closed implicitly. The even-odd
  rule decides what is solid: a contour inside no other contour is an outer boundary, a contour
  inside one is a hole, and a contour inside a hole is an island, whatever winding you supply.
  `Contours` returns the normalized contours (outer boundaries counter-clockwise, holes clockwise).
- Closing duplicates (a last point equal to the first) and consecutive duplicate points are
  dropped; collinear points are kept and become vertices of the extruded mesh.
- `Area` is the area of the region (outer boundaries minus holes plus islands). `Empty` has no
  contours; extruding it gives an empty mesh. `Vertices` is all contour vertices in order, the index
  space of `PolygonTriangulator.Triangulate`.
- **Invalid polygons are rejected, never repaired**: the constructor throws `ArgumentException`
  (naming the contour) for a non-finite point, a contour with fewer than three distinct vertices or
  with zero area (all points collinear, or an area too small for a normal double, such as a triangle with
  legs of `1e-200`), a contour that crosses or touches itself (a bow tie, a
  spike, or a repeated non-consecutive point), and contours that cross or touch each other (a shared
  point, or a vertex on another contour's edge). Move touching contours apart or merge them
  yourself. `ArgumentNullException` is thrown for a null contour list or contour. Finite but
  extreme coordinates (beyond about 1e154, where the area overflows) are also rejected with
  `ArgumentException`, so `Area` is always a finite number.

```csharp
// A 10 x 10 square plate with a 4 x 4 hole (winding does not matter)
var plate = new PolygonRegion(
[
    new Vector2D[] { new(0, 0), new(10, 0), new(10, 10), new(0, 10) },
    new Vector2D[] { new(3, 3), new(3, 7), new(7, 7), new(7, 3) },
]);
Console.WriteLine(plate.Area); // Output: 84
```

### PolygonTriangulator

`PolygonTriangulator` triangulates a `PolygonRegion` into counter-clockwise triangles that use only
the region's own vertices (no points are added, and every collinear vertex is kept).

```csharp
public static class PolygonTriangulator
{
    public static IReadOnlyList<int> Triangulate(PolygonRegion region);
}
```

- The result lists three indices per triangle into `region.Vertices`. A region with `V` vertices,
  `H` holes and `P` outer boundaries gives exactly `V + 2H - 2P` triangles, which tile the region
  exactly and leave the holes uncovered. The decisions use exact arithmetic (no tolerances), and the
  result is deterministic.
- Holes are connected to their outer boundary by zero-width bridges before the ears are clipped.
  A convex polygon is linear. A concave polygon costs up to the number of vertices times the number
  of reflex (inward-pointing) vertices, so the worst case is quadratic in the number of vertices:
  fine for typical CAD-sized profiles, but a comb with 8,000 vertices and many deep teeth takes about
  two seconds, and the time keeps growing quadratically beyond that. There is no cancellation, so
  split very large concave profiles.
- `ArgumentNullException` is thrown for a null region.

```csharp
var triangles = PolygonTriangulator.Triangulate(plate);
Console.WriteLine(triangles.Count / 3); // Output: 8 (8 vertices + 2 * 1 hole - 2 * 1 boundary)
```

### Extruder

`Extruder` turns a `PolygonRegion` into a solid `Mesh` by extruding it along +Z or by revolving it
about the Z axis.

```csharp
public static class Extruder
{
    public static Mesh Linear(PolygonRegion region, double height, bool center = false,
        double twistDegrees = 0.0, int slices = 1, Vector2D? scale = null);
    public static Mesh Linear(PolygonRegion region, double height, bool center,
        double twistDegrees, int slices, double uniformScale);
    public static Mesh Rotate(PolygonRegion region, double angleDegrees, int segments);
}
```

Every result is a welded, closed, outward-oriented mesh with positive `SignedVolume` and no
degenerate triangles, so it is a valid operand for constructive solid geometry (`CsgNode`) without
any operand report. An empty region gives an empty mesh. These guarantees hold for profiles and
parameters of ordinary magnitude and are subject to two caveats:

- **Finite precision.** With extreme values (a tiny non-zero scale such as `1e-17`, a height near
  `double.MaxValue`, or coordinates of about `1e200`) rounding can make a triangle degenerate; the
  operand validator then reports `render3d-csg-degenerate-triangle`. Use scales, heights, and
  coordinates of moderate magnitude.
- **Scale and twist are about the origin.** For an apex (scale `(0, 0)`), a large scale change, or a
  per-axis scale combined with a twist, the profile should be **star-shaped about the origin** (every
  point of the profile is visible from the origin). Otherwise the solid can intersect itself (it may
  even come out inverted) and is not a valid operand.

The `Linear` method:

- The solid spans `z = 0` to `z = height`, or `+/- height/2` when `center` is true.
- `twistDegrees` rotates the top relative to the bottom about +Z: a **positive** angle turns the top
  **counter-clockwise** as seen from +Z (right-hand rule). If you are porting from a language with
  the opposite sign, negate the angle.
- `scale` is the scale of the top relative to the bottom about the origin (per axis, interpolated
  linearly; applied before the twist). Each component must be finite and not negative. A scale of
  `(0, 0)` makes the top a single **apex** point (no top cap), giving a pyramid or cone shape; the apex
  needs a region with a **single contour**, because a hole or a second island would meet the outer
  shell at one pinched (non-manifold) vertex, so such a region throws `ArgumentException`. A
  scale that is zero in only one axis, or negative in either, is rejected: it would flatten the top
  to a line or make the solid pass through itself. The `uniformScale` overload scales both axes.
- `slices` is the number of slices along the height (at least 1). A straight extrusion without
  taper or twist needs only one. Twisted or non-uniformly scaled walls are not planar, so the mesh
  approximates the smooth solid and the approximation improves as `slices` grows: use enough slices
  that the twist per slice is small (the volume error is about 1.7% for 90 degrees in 32 slices, and
  shrinks as slices increase). The slice count is never refined automatically.
- A region with a hole becomes **one connected solid with a through-hole**: the outer wall, the
  hole wall (facing into the hole), and both caps share their vertices. An island inside a hole is
  a separate solid.

The `Rotate` method:

- The profile's X is the radius and its Y is the height; it is revolved about the +Z axis, turning
  from azimuth 0 toward `+angleDegrees` (counter-clockwise as seen from +Z).
- `angleDegrees` must be in `(0, 360]`, and not so small that its radian value rounds to zero
  (`double.Epsilon` is rejected). Exactly 360 is a full revolution (`segments` at least 3, no
  end caps); a smaller angle is a partial sweep (`segments` at least 1) with end caps at both ends. In
  every case each angular step, `angleDegrees / segments`, must be at most **120 degrees** (the step of
  the coarsest full revolution); wider steps give flat or inside-out facets and are rejected. For
  example `(90, 1)`, `(180, 2)`, and `(359, 3)` are valid, while `(180, 1)` and `(359, 2)` are not.
- The whole profile must lie on **one side of the axis**: every vertex with `x >= 0`, or every
  vertex with `x <= 0`. A profile that **crosses the axis** (vertices with both signs) throws
  `ArgumentException`, as does a profile lying only on the axis. There is no tolerance: `x == 0`
  exactly means on the axis. Vertices on the axis are welded into single points, so a profile
  touching the axis along an edge gives a cone, cylinder, or lens without degenerate triangles. A
  profile that touches the axis only at an **isolated vertex** (both neighboring edges leave the axis)
  would pinch the solid at that point and throws `ArgumentException`.
- The mesh is faceted: its volume is `segments * sin(angle / segments) * integral(|x| dA)` over the
  profile, which approaches the exact `angle * x_centroid * area` as `segments` grows (for a
  full revolution, `angle` is 2 pi radians).
- A profile with a hole gives a hollow solid (its cavity is a second, inward-facing shell).

**Exceptions:**

- `ArgumentNullException`: Thrown for a null region.
- `ArgumentOutOfRangeException`: Thrown for a height that is not finite and positive, fewer than one
  slice, an invalid scale, an angle outside `(0, 360]` (or one whose radian value is zero), too few
  segments, or a sweep step (`angleDegrees / segments`) wider than 120 degrees.
- `ArgumentException`: Thrown for a non-finite twist, an apex (zero scale) over a region with a hole or
  several contours, a profile that crosses or lies on the rotation axis or touches it at an isolated
  vertex, a sweep step too small to resolve at a profile vertex's radius, or a result too
  large for a mesh.

```csharp
var plate3D = Extruder.Linear(plate, 2.0, center: true);
Console.WriteLine(plate3D.SignedVolume); // Output: 168 (84 * 2)

// A cone: revolve a right triangle that touches the axis
var cone = Extruder.Rotate(new PolygonRegion([new Vector2D[] { new(0, 0), new(2, 0), new(0, 5) }]), 360.0, 32);
Console.WriteLine(cone.IsClosed); // Output: True
```

### ConvexHull

`ConvexHull` computes the exact convex hull of a point set: a closed solid mesh for 3D points and a polygon for
2D points. It is what the OpenSCAD `hull` module is built on.

```csharp
public static class ConvexHull
{
    public static Mesh Hull3D(IEnumerable<Vector3D> points, CancellationToken cancellationToken = default);
    public static IReadOnlyList<Vector2D> Hull2D(IEnumerable<Vector2D> points, CancellationToken cancellationToken = default);
}
```

- `Hull3D` returns a welded, closed, outward-oriented mesh with positive `SignedVolume` whose vertices are
  exactly the **strictly extreme** points of the input (points inside the hull, on a face, or on an edge do
  not appear), so the result is a valid operand for constructive solid geometry (`CsgNode`). It has exactly
  `2 * V - 4` triangles for `V` vertices; coplanar triangles of one facet are merged and fan-triangulated.
- `Hull2D` returns the strictly convex polygon (no collinear points), counter-clockwise, starting at the
  lexicographically smallest point. A degenerate set does not throw: it returns no point, the single point,
  or the two end points of the segment, and the caller decides what that means.
- Every decision is exact (no tolerance), and the result depends only on the point **set**: any order and any
  repetition of the points (and `-0.0` versus `0.0`) gives a byte-identical result.
- `Hull2D` is O(n log n) time in every case (the sort dominates; the monotone chain after it is O(n)) and O(n)
  memory.
- `Hull3D` is O(n log n) for the copy, the sort and the removal of duplicates, plus the incremental insertion:
  each insertion costs the faces it replaces and the faces around the previously inserted vertex, so the
  total is O(n log n) when the hull stays small (random points in a cube or a ball, lattices) and O(n * h) in the
  worst case, which is O(n^2) when almost every point is a hull vertex (h = number of hull vertices; points
  on a moment curve are the classic adversarial input). Merging the coplanar triangles into facets is
  O(F log F) for F ≤ 2n - 4 triangles. Memory is O(n). The cancellation token is polled every few thousand steps
  (the initial sort cannot be interrupted).

**Exceptions:** `ArgumentNullException` for a null sequence; `ArgumentException` for a non-finite coordinate, and
for `Hull3D` when the points are not **affinely independent** enough to span a volume (fewer than four distinct
points, all collinear, or all coplanar; the message says which); `OperationCanceledException` when canceled.

```csharp
Vector3D[] points =
[
    new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0),
    new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1),
    new(0.5, 0.5, 0.5), // inside: not a vertex of the hull
];
var hull = ConvexHull.Hull3D(points);
Console.WriteLine($"{hull.VertexCount} {hull.TriangleCount} {hull.SignedVolume}"); // Output: 8 12 1
```

### MinkowskiSum

`MinkowskiSum.ConvexSum` computes the Minkowski sum of two **convex** solid meshes (every point `a + b`, the
shape of one solid swept over the other): rounding a block with a sphere, or offsetting a part by another. It
is what the OpenSCAD `minkowski` module is built on.

```csharp
public static class MinkowskiSum
{
    public const int MaxPairwiseSums = 2_097_152;
    public static Mesh ConvexSum(Mesh a, Mesh b, CancellationToken cancellationToken = default);
}
```

- The result is the exact `ConvexHull.Hull3D` of all pairwise sums of the operands' vertices (each a single
  `double` addition, so the sum has an error of at most one unit in the last place per coordinate): a closed,
  outward-oriented mesh and a valid CSG operand, byte-identical for `(a, b)` and `(b, a)`.
- **Convex operands only, and checked.** An operand is verified to be a closed, consistently wound, single-shell
  convex solid; it is accepted only if it differs from the convex hull of its own vertices by at most **1e-9
  of the hull's volume and surface area** (plus the rounding error of the volume computation), so the rounding
  noise of rotated cubes, cylinder caps, and sphere quads passes and no real dent, notch, hole, or second shell
  does. The sum is then computed from the operand's vertices, which is exact for the operand's hull. A
  non-convex operand is **never approximated**: it throws. A per-edge tolerance would be unsound (a gently
  concave bowl of many facets passes any per-edge height tolerance), which is why the test is global. Models
  whose offset is huge compared with their size (more than about a million times the feature size) can fail
  the test through coordinate rounding and are rejected as well.
- The work is O(`Va * Vb` * log(`Va * Vb`)) with O(`Va * Vb`) memory (the array of sums, 24 bytes per point and about
  50 MB at the bound, is sorted in place, so the peak is about 70 MB), where `Va` and
  `Vb` are the numbers of distinct vertices; the product is limited to `MaxPairwiseSums` (2^21).

**Exceptions:** `ArgumentNullException` for a null operand; `ArgumentException` for an operand without
triangles or without volume, or when `Va * Vb` exceeds `MaxPairwiseSums`; `NotSupportedException` for an
operand that is not provably a closed convex solid (open, non-manifold, inconsistently wound, inverted, several
shells, or not convex within the tolerance; the message gives the reason); `OperationCanceledException` when
canceled.

```csharp
var block = PrimitiveTessellator.Box(new Vector3D(2, 2, 2));
var other = PrimitiveTessellator.Box(new Vector3D(1, 1, 1));
var sum = MinkowskiSum.ConvexSum(block, other);
Console.WriteLine(sum.SignedVolume); // Output: 27 (a 3 x 3 x 3 box)

try
{
    var corners = new Vector2D[] { new(0, 0), new(3, 0), new(3, 1), new(1, 1), new(1, 3), new(0, 3) };
    var notch = Extruder.Linear(new PolygonRegion([corners]), 1.0);
    MinkowskiSum.ConvexSum(notch, other);
}
catch (NotSupportedException ex)
{
    Console.WriteLine("not convex: " + ex.Message);
}
```

### Scene

A `Scene` is an immutable tree of nodes. Each node has a `Transform` (a `Matrix4x4D`, default
identity) applied to the node and everything below it, and an optional `Appearance`. A node's
effective color is that of the innermost node that has an appearance; the alpha of a node color is
honored (see *Transparency and Overlays* below). A group is the union of its children; a `CsgNode`
combines its children by union, difference, or intersection (see *Constructive Solid Geometry*
below), and an `OverlayNode` is an independent translucent body (see *Transparency and Overlays*).

```csharp
public abstract class SceneNode
{
    public Matrix4x4D Transform { get; init; } // default Matrix4x4D.Identity; must be finite
    public NodeAppearance? Appearance { get; init; }
}

public sealed class MeshNode : SceneNode
{
    public MeshNode(Mesh mesh);
    public Mesh Mesh { get; }
}

public sealed class GroupNode : SceneNode
{
    public GroupNode(IEnumerable<SceneNode> children);
    public IReadOnlyList<SceneNode> Children { get; }
}

public enum CsgOperation { Union, Difference, Intersection }

public sealed class CsgNode : SceneNode
{
    public CsgNode(CsgOperation operation, IEnumerable<SceneNode> children);
    public CsgOperation Operation { get; }
    public IReadOnlyList<SceneNode> Children { get; } // for Difference the first is the minuend
}

public sealed class OverlayNode : SceneNode
{
    public OverlayNode(Rgba32 color, IEnumerable<SceneNode> children);
    public Rgba32 Color { get; } // replaces every appearance beneath the overlay; alpha is honored
    public IReadOnlyList<SceneNode> Children { get; } // their union is the overlay body
}

public sealed record NodeAppearance(Rgba32 Color); // alpha below 255 makes the surface translucent

public sealed class Scene
{
    public Scene(SceneNode root);
    public SceneNode Root { get; }
    public BoundingBox3D ComputeBounds(); // tight world-space bounds, overlay bodies included
    public BoundingBox3D ComputeBounds(bool includeOverlays); // false leaves the overlay bodies out
}
```

The node hierarchy is closed (you cannot derive your own node types). A node (including a mesh
node) may appear under several parents. Coincident faces of different meshes are resolved by scene
order: the node that comes first in a depth-first traversal wins.

**Exceptions:**

- `ArgumentNullException`: Thrown by `MeshNode` for a null mesh, by `GroupNode`, `CsgNode` and
  `OverlayNode` for a null child list, and by `Scene` for a null root.
- `ArgumentException`: Thrown by `GroupNode`, `CsgNode` and `OverlayNode` for a null child, and when a
  node `Transform` contains a non-finite element.
- `ArgumentOutOfRangeException`: Thrown by `CsgNode` for an undefined `CsgOperation` value.

### Transparency and Overlays

The alpha channel of a `NodeAppearance` color is honored. A color with alpha below 255 makes the
visible surfaces of the solid translucent: the renderer composites the surfaces along each view ray
from the nearest to the farthest, deterministically and independent of any submission order, until it
meets an opaque surface, and then composites the remainder over `SceneRenderOptions.BackgroundColor`
(whose own alpha is honored, so over a transparent background the pixel keeps its color with the
accumulated alpha). A translucent box therefore shows its far wall through its front, and a
translucent box in front of an opaque one shows a blend of the two. An alpha of 0 is invisible but the
body is still solid, so it still takes part in a difference or intersection. The alpha of
`SceneRenderOptions.DefaultColor`, `BackFaceColor` and of a light color remains ignored.

```csharp
// A translucent red cube in front of an opaque blue floor: the pixel blends red (twice: front and
// back wall of the cube) with the blue floor behind it
var cube = new MeshNode(PrimitiveTessellator.Box(new Vector3D(2, 2, 2), center: true))
{
    Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 0, 2)),
    Appearance = new NodeAppearance(new Rgba32(200, 40, 40, 128)),
};
var floor = new MeshNode(PrimitiveTessellator.Box(new Vector3D(8, 8, 1), center: true))
{
    Appearance = new NodeAppearance(new Rgba32(30, 60, 220, 255)),
};
var scene = new Scene(new GroupNode([floor, cube]));
```

Members of one union **merge**: a union of overlapping translucent bodies shows only the boundary of
the union, and geometry fully enclosed by a translucent solid of the same union is not visible (this
is the only meaning consistent with difference and intersection). The cut surface of a translucent
difference is drawn with the subtrahend's own color and alpha.

An `OverlayNode` expresses an **independent** see-through body (a ghost or a highlight). Its children
(a union) are drawn with the overlay's `Color` (alpha honored; it replaces every appearance beneath
the overlay, and the overlay node's own `Appearance` is ignored), and the overlay is **false** in every
enclosing boolean: it does not add to, subtract from, or intersect the solid. It is depth-composited
with the rest of the scene, so it tints what is behind it, is hidden behind opaque geometry, and can
sit anywhere, including inside a solid that is translucent. An overlay nested in an overlay is a plain
group (the outer color wins). To highlight a subtrahend, put the same node both in the difference and,
wrapped in an overlay, beside it:

```csharp
var cutter = new MeshNode(PrimitiveTessellator.Cylinder(6, 1.0, 1.0, 32, center: true));
var overlay = new OverlayNode(new Rgba32(255, 64, 128, 110), [cutter]);
var part = new CsgNode(CsgOperation.Difference, [cube, new GroupNode([cutter, overlay])]);
```

`Scene.ComputeBounds()` (and so `RenderFitted` and `Camera.FitToBounds`) includes the overlay bodies
so that a ghost stays in the picture; `Scene.ComputeBounds(false)` leaves them out, for example to size
only the solid.

**Requirements.** Like constructive solid geometry, translucency and overlays are resolved with signed
surface crossings, so every mesh in such a scene should be a closed, consistently wound, outward-oriented
solid (see *Operands must be solids* below); the renderer checks and reports them with the same
tokens and options. Such scenes are rendered per pixel from stored surface crossings and are subject to
`MaxFragmentsPerTile` (an overlay of the same size doubles the fragments of its tile); an opaque scene
without overlays is rendered exactly as before, with no extra memory.

### Constructive Solid Geometry

A `CsgNode` builds a solid from other solids. `Union` is inside where any child is inside,
`Difference` is the first child minus every other child, and `Intersection` is inside where every
child is inside. The operations are n-ary and nest freely (a `CsgNode` may be a child of a
`CsgNode` or a `GroupNode`), the node's `Transform` and `Appearance` apply to all of its children,
and a node without children is empty for every operation. The renderer never builds a boolean mesh:
each sample of the image is resolved from the surfaces its view ray crosses, so a result can be
rendered and re-rendered cheaply, and the operand meshes are never modified.

```csharp
// A 4 x 4 x 4 cube hollowed out by a centered sphere (a round window in every face)
var cube = new MeshNode(PrimitiveTessellator.Box(new Vector3D(4, 4, 4), center: true));
var bite = new MeshNode(PrimitiveTessellator.Sphere(2.6, 48));
var part = new CsgNode(CsgOperation.Difference, [cube, bite]);
```

**Operands must be solids.** Every mesh that takes part in a CSG scene should be a closed,
consistently wound, outward-oriented (counter-clockwise from outside) mesh: `Mesh.IsClosed` must
be `true` and `Mesh.SignedVolume` positive. Closedness is geometric: vertices at exactly the same
position count as one vertex, so a mesh that stores every triangle's corners separately (for
example one read from an STL file) is closed when its triangles form a closed surface. A hollow
solid is valid: an outward outer shell with inward-wound cavity shells (and outward solids nested
inside a cavity again); a shell whose orientation disagrees with how deeply it is nested - for
example an inside-out cube next to a valid one - is reported even when the total volume is
positive. The `PrimitiveTessellator` box, sphere, cylinder, and
cone meshes satisfy this; for a closed mesh with a negative `SignedVolume` (inside-out), use
`Mesh.Reverse`. Checking applies to every mesh in a scene that contains a difference or an
intersection (or a translucent appearance or an overlay, see *Transparency and Overlays*), not only
the direct operands, because an open mesh anywhere in the scene would vanish
from the result. By default the renderer is **tolerant**: it renders anyway, and the output is
deterministic but visibly wrong where an operand is not a solid. To find out about such operands,
set `SceneRenderOptions.UnsupportedFeatureHandler` (called once per problem, on the calling
thread, before any pixels are produced) or set `ThrowOnUnsupportedFeature` for **strict** behavior
(the first problem throws `SceneRenderUnsupportedFeatureException`). Without either option no
analysis is performed.

```csharp
public sealed record SceneRenderUnsupportedFeature(string Feature, string Message, MeshNode? Node = null);

public sealed class SceneRenderUnsupportedFeatureException : InvalidOperationException
{
    public SceneRenderUnsupportedFeatureException(string feature, string message, MeshNode? node = null);
    public string Feature { get; } // the same token as SceneRenderUnsupportedFeature.Feature
    public MeshNode? Node { get; }
}
```

The `Feature` tokens are stable and machine-matchable:

| Token | Meaning |
| ----- | ------- |
| `render3d-csg-open-mesh` | The mesh has a boundary edge (it is not closed) |
| `render3d-csg-non-manifold-edge` | An edge is shared by three or more triangles |
| `render3d-csg-inconsistent-winding` | Neighboring triangles are wound in conflicting directions |
| `render3d-csg-degenerate-triangle` | The mesh has zero-area triangles (repeated index, or collinear vertices) |
| `render3d-csg-inverted-mesh` | A closed mesh has a shell wound against its nesting |
| `render3d-csg-singular-transform` | An instance's world transform is singular (zero or non-finite determinant) |

Mesh problems are reported once per `MeshNode` however many places in the scene share it; a
singular transform (or triangles that collapse under an extreme transform, reported as
`render3d-csg-degenerate-triangle`) is reported per instance, so a node shared by a valid and a
singular instance is reported for the singular one. Reports are delivered
in scene order, and `Node` identifies the offending mesh node so a front end can map it back to its
source.

Rendering behavior worth knowing:

- **Unions are free.** An opaque scene whose `CsgNode`s are all unions (or whose difference/intersection
  nodes have a single child) is rendered exactly as a scene of groups: the same z-buffer path, no
  extra memory, no operand analysis, and byte-identical output to a render without `CsgNode`.
- **Per-tile budget.** A scene with a difference or an intersection (or a translucent appearance or an
  overlay) stores every surface crossing
  of a 32 x 32 pixel tile. `SceneRenderOptions.MaxFragmentsPerTile` (default 4 Mi, about 32 MB per
  worker) bounds that memory; a tile that needs more throws `RenderBudgetExceededException`
  (with the `MaxFragmentsPerTile` that was exceeded), identically for every thread count. Lower
  `Supersampling`, the image size, or the scene complexity - or raise the budget - to avoid it.
- **Cut surfaces.** A surface exposed by a subtrahend is painted in that subtrahend's own color,
  lit as seen by the viewer; it is never painted with `BackFaceColor`.
- **Camera inside a solid.** An eye inside a solid starts inside it: the first exit is the visible
  surface and is painted with `BackFaceColor`, as for any back face. For an orthographic camera
  (which has no eye position along its rays) set an explicit `Camera.NearPlane` to place the
  viewpoint. An explicit `NearPlane` (including one in front of some geometry, or a camera inside
  an operand such as a hollow room) is supported and not reported: the result is the CSG solid
  clipped to the near plane of the view frustum, and no cap surfaces are drawn on that plane, so
  from inside a hollow room you see the room's walls.
- **Coincident faces.** Faces of different operands that coincide (for example a subtrahend whose
  face lies exactly on a face of the minuend) are resolved without speckle when their coordinates
  are identical. Meshes that merely coincide mathematically but are placed through different
  transforms are within a small depth tolerance of each other and are also resolved, but along the
  edge-on silhouette of such walls a few isolated pixels (the tests tolerate up to 12 in an
  80 x 80 image across many views) can still be wrong. Features thinner than the depth tolerance
  (one millionth of the scene size; up to about one thousand times that between different mesh
  instances seen at grazing angles) are treated as coincident. Where you can, give a subtrahend a
  small overlap instead of an exactly flush face.
- **Mirrored transforms.** An operand under a negative-determinant (mirroring) transform renders
  as if the mirror were baked into its vertices; the inside-out check is unaffected by mirroring.
- **Determinism.** At any given `Supersampling` setting, the output is byte-identical for every
  `MaxDegreeOfParallelism` and every run, as for scenes without CSG. (Different `Supersampling`
  factors naturally produce different antialiasing.)
- **Bounds.** `Scene.ComputeBounds()` includes every operand, even the removed parts (and every overlay
  body), so `RenderFitted` and `Camera.FitToBounds` frame everything and may leave extra margin.

### Camera

`Camera` describes the viewpoint and projection. A default-constructed camera is valid (the eye on
the -Y axis looking at the origin with +Z up, perspective, 22.5 degree vertical field of view).

```csharp
public enum CameraProjection { Perspective, Orthographic }

public sealed class Camera
{
    public const double DefaultFieldOfViewDegrees = 22.5;
    public const double DefaultAzimuthDegrees = 45.0;
    public static readonly double DefaultElevationDegrees; // about 35.26 (isometric)

    public Vector3D Eye { get; init; }
    public Vector3D Target { get; init; }
    public Vector3D Up { get; init; } // default +Z
    public CameraProjection Projection { get; init; }
    public double FieldOfViewDegrees { get; init; } // vertical; between 0 and 180 (exclusive)
    public double? OrthographicHeight { get; init; } // required for Orthographic
    public double? NearPlane { get; init; } // null = automatic
    public double? FarPlane { get; init; } // null = automatic

    public static Camera LookAt(Vector3D eye, Vector3D target, Vector3D up);
    public static Camera FitToBounds(
        BoundingBox3D bounds,
        double azimuthDegrees,
        double elevationDegrees,
        double aspectRatio,
        CameraProjection projection = CameraProjection.Perspective,
        double fieldOfViewDegrees = DefaultFieldOfViewDegrees,
        double marginFraction = 0.05);
    public Matrix4x4D GetViewMatrix();
}
```

`FitToBounds` places the camera so the bounding box fills the frame (with `marginFraction` of
margin) when viewed from the given orbit angles: azimuth 0 and elevation 0 look along +Y from the
-Y axis (a front view), positive elevation raises the eye toward +Z, and positive azimuth moves the
eye counter-clockwise as seen from above. An empty or zero-size bounding box falls back to a unit
cube at the origin. When the view direction is parallel to the up vector (for example, looking
straight down), an alternate up vector is substituted. With `NearPlane`/`FarPlane` left `null`, the
renderer chooses them from the scene's depth range; geometry that crosses the near plane is clipped,
so a camera inside or beside a model works.

**Exceptions:**

- `ArgumentException`: Thrown by an initializer for a non-finite `Eye`, `Target`, or `Up`; by
  `LookAt` when the eye equals the target or `up` is zero; and by `SceneRenderer.Render` (not at
  initialization) when the eye equals the target, an orthographic camera has no `OrthographicHeight`,
  or `NearPlane` is not less than `FarPlane`.
- `ArgumentOutOfRangeException`: Thrown for a field of view outside (0, 180), a non-positive
  or non-finite height or clip distance, and by `FitToBounds` for a non-finite angle, a non-positive
  aspect ratio, or a negative margin.

### Lights

Lights are set through `SceneRenderOptions.Lights`. If it is `null`, a neutral default rig is used:
an ambient light of 0.35, a camera-relative headlight of 0.55, and a camera-relative key light of
0.25 from the upper left (also available as `Light.DefaultRig`). An explicit empty list renders
everything black; to get flat, unshaded color use a single `AmbientLight { Intensity = 1 }`.

```csharp
public abstract class Light
{
    public static IReadOnlyList<Light> DefaultRig { get; }
    public Rgba32 Color { get; init; } // default white; alpha is ignored
    public float Intensity { get; init; } // default 1; finite and not negative
}

public sealed class AmbientLight : Light
{
}

public sealed class DirectionalLight : Light
{
    public Vector3D Direction { get; init; } // direction the light TRAVELS; non-zero
    public bool IsCameraRelative { get; init; }
}
```

A `DirectionalLight.Direction` is the direction the light travels, normalized when set. When
`IsCameraRelative` is `true` the direction is expressed in view space (+X right, +Y up, -Z forward),
so the light moves with the camera; otherwise it is a world-space direction. Shading is flat
(one color per triangle) Lambertian shading plus an optional Blinn-Phong highlight controlled by
`SceneRenderOptions.SpecularStrength`/`SpecularPower`.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `Intensity` is negative or not finite.
- `ArgumentException`: Thrown when `Direction` is zero-length or not finite.

### SceneRenderOptions

`SceneRenderOptions` is an immutable options bag. Every property is validated when it is
initialized, so an options instance is always valid.

```csharp
public sealed class SceneRenderOptions
{
    public static SceneRenderOptions Default { get; }

    public Rgba32 BackgroundColor { get; init; } // default opaque white; alpha is honored
    public Rgba32 DefaultColor { get; init; } // default (180, 180, 180): nodes without an appearance; alpha is ignored
    public Rgba32 BackFaceColor { get; init; } // default (200, 70, 70): surfaces seen from behind; alpha is ignored
    public IReadOnlyList<Light>? Lights { get; init; } // null = Light.DefaultRig; copied
    public float SpecularStrength { get; init; } // default 0.10; 0 to 1
    public float SpecularPower { get; init; } // default 32; greater than 0
    public int Supersampling { get; init; } // default 3; 1 to 4 samples per axis
    public int MaxDegreeOfParallelism { get; init; } // default -1; 1 = sequential
    public long MaxFragmentsPerTile { get; init; } // default 4 Mi; 1 to Array.MaxLength; per-pixel scenes only
    public Action<SceneRenderUnsupportedFeature>? UnsupportedFeatureHandler { get; init; } // default null
    public bool ThrowOnUnsupportedFeature { get; init; } // default false (tolerant)
}
```

`Supersampling` of `k` shades `k x k` samples per pixel and resolves them with a box filter, so
cost grows with the square of `k`; a transparent `BackgroundColor` is preserved, and antialiased
edges keep their color at partial opacity. `MaxDegreeOfParallelism` of `-1` lets the scheduler
choose how many tiles to render at once, `1` renders sequentially, and the output is byte-identical
for every setting. A back-facing surface is not culled but painted with `BackFaceColor`, which makes
inside-out or open meshes easy to spot.

The last three properties apply to scenes that are resolved per pixel - a `CsgNode` difference or
intersection, a translucent appearance, or an `OverlayNode` (see *Constructive Solid Geometry* and
*Transparency and Overlays*): `MaxFragmentsPerTile` bounds the per-tile fragment storage and is
ignored for opaque scenes without such nodes, `UnsupportedFeatureHandler` receives a
`SceneRenderUnsupportedFeature` for each operand problem, and `ThrowOnUnsupportedFeature` turns the
first problem into a `SceneRenderUnsupportedFeatureException` instead.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown by an initializer for `Supersampling` outside 1 to 4,
  `SpecularStrength` outside 0 to 1, a non-positive or non-finite `SpecularPower`, a
  `MaxDegreeOfParallelism` of 0 or below -1, or a `MaxFragmentsPerTile` below 1 or above
  `Array.MaxLength`.
- `ArgumentException`: Thrown when `Lights` contains a null light.

### SceneRenderer

`SceneRenderer` is the static entry point that renders a `Scene` into a new `Surface`. The caller
owns, and must dispose, the returned surface.

```csharp
public static class SceneRenderer
{
    public static Surface Render(
        Scene scene,
        Camera camera,
        int width,
        int height,
        SceneRenderOptions? options = null,
        CancellationToken cancellationToken = default);

    public static Surface RenderFitted(
        Scene scene,
        int width,
        int height,
        SceneRenderOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

`Render` draws the scene as seen by the camera. `RenderFitted` builds an isometric camera (azimuth
45 degrees, elevation about 35.26 degrees) that frames the whole scene with a 5 % margin, which is
a convenient way to preview a scene; an empty scene renders the background only. Rendering is
deterministic, and a scene, camera, or options instance may be shared between threads and renders.
Passing a `CancellationToken` lets you abandon a long render: cancellation throws
`OperationCanceledException` (never an `AggregateException`) and releases the partial surface.

**Exceptions:**

- `ArgumentNullException`: Thrown when `scene` or `camera` is null.
- `ArgumentOutOfRangeException`: Thrown when `width` or `height` is outside 1 to
  `Surface.MaxDimension`.
- `ArgumentException`: Thrown by `Render` when the camera is invalid (eye equals target, an
  orthographic camera without `OrthographicHeight`, or `NearPlane` not less than `FarPlane`).
- `OperationCanceledException`: Thrown when `cancellationToken` is cancelled.
- `RenderBudgetExceededException`: Thrown when a scene with a CSG difference or intersection, a
  translucent appearance or an overlay needs more than `MaxFragmentsPerTile` fragments in a tile.
- `SceneRenderUnsupportedFeatureException`: Thrown when `ThrowOnUnsupportedFeature` is set and a CSG,
  translucent or overlay mesh is not a closed, outward-oriented solid.

## Examples

### Example 20: Rendering a 3D Scene

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

// Build a scene: a gray base plate, a red sphere, and a blue cone. Nodes without an appearance
// use SceneRenderOptions.DefaultColor (neutral gray).
var plate = new MeshNode(PrimitiveTessellator.Box(new Vector3D(10, 10, 1), center: true))
{
    Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 0, -0.5)),
};
var ball = new MeshNode(PrimitiveTessellator.Sphere(2, 48))
{
    Transform = Matrix4x4D.CreateTranslation(new Vector3D(-2, 0, 2)),
    Appearance = new NodeAppearance(new Rgba32(200, 40, 40, 255)),
};
var cone = new MeshNode(PrimitiveTessellator.Cylinder(4, 1.5, 0, 48))
{
    Transform = Matrix4x4D.CreateTranslation(new Vector3D(2.5, 0, 0)),
    Appearance = new NodeAppearance(new Rgba32(40, 80, 200, 255)),
};
var scene = new Scene(new GroupNode([plate, ball, cone]));

// A quick preview: an isometric view that frames the whole scene automatically
using var preview = SceneRenderer.RenderFitted(scene, 640, 480);
PngCodec.Save(preview, "scene-preview.png");

// A full render: an explicit camera, custom lights, a transparent background, 4 x 4
// supersampling, and a 30-second cancellation timeout
var camera = Camera.FitToBounds(
    scene.ComputeBounds(),
    azimuthDegrees: 30,
    elevationDegrees: 25,
    aspectRatio: 640.0 / 480.0);
var options = new SceneRenderOptions
{
    BackgroundColor = new Rgba32(0, 0, 0, 0),
    Lights =
    [
        new AmbientLight { Intensity = 0.3f },
        new DirectionalLight { Direction = new Vector3D(-1, 1, -1), Intensity = 0.8f },
    ],
    Supersampling = 4,
    MaxDegreeOfParallelism = Environment.ProcessorCount,
};
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
using var render = SceneRenderer.Render(scene, camera, 640, 480, options, cts.Token);
PngCodec.Save(render, "scene.png");
Console.WriteLine(render[0, 0].A); // Output: 0 (the corner is untouched transparent background)
```

### Example 21: Cube Minus Sphere (Constructive Solid Geometry)

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

// A red 4 x 4 x 4 cube from which a blue sphere is subtracted. The sphere is centered in the cube and
// larger than the cube's inscribed sphere, so it hollows the cube out and cuts a round window in
// every face. The surface exposed by the subtraction takes the sphere's color. Both operands are
// closed, outward-oriented meshes, as constructive solid geometry requires.
var cube = new MeshNode(PrimitiveTessellator.Box(new Vector3D(4, 4, 4), center: true))
{
    Appearance = new NodeAppearance(new Rgba32(200, 60, 50, 255)),
};
var hollow = new MeshNode(PrimitiveTessellator.Sphere(2.6, 64))
{
    Appearance = new NodeAppearance(new Rgba32(50, 110, 210, 255)),
};
var scene = new Scene(new CsgNode(CsgOperation.Difference, [cube, hollow]));

// Collect operand problems instead of failing: a tolerant render still produces an image
var problems = new List<SceneRenderUnsupportedFeature>();
var options = new SceneRenderOptions
{
    UnsupportedFeatureHandler = problems.Add,
    MaxFragmentsPerTile = 8 * 1024 * 1024, // raise the default memory budget (4 Mi fragments per tile)
};

try
{
    using var image = SceneRenderer.RenderFitted(scene, 640, 480, options);
    PngCodec.Save(image, "cube-minus-sphere.png");
    Console.WriteLine(problems.Count); // Output: 0 (both operands are closed solids)
}
catch (RenderBudgetExceededException ex)
{
    // A tile needed more fragments than allowed: raise MaxFragmentsPerTile or lower Supersampling
    Console.WriteLine($"Scene too complex for a budget of {ex.MaxFragmentsPerTile} fragments per tile");
}

// Strict mode: fail fast when an operand is not a closed solid (here an open, single-triangle mesh)
var openMesh = new MeshBuilder();
openMesh.AddTriangle(
    openMesh.AddVertex(new Vector3D(0, 0, 0)),
    openMesh.AddVertex(new Vector3D(1, 0, 0)),
    openMesh.AddVertex(new Vector3D(0, 1, 0)));
var broken = new Scene(new CsgNode(CsgOperation.Difference, [cube, new MeshNode(openMesh.Build())]));
try
{
    using var failed = SceneRenderer.RenderFitted(
        broken, 640, 480, new SceneRenderOptions { ThrowOnUnsupportedFeature = true });
}
catch (SceneRenderUnsupportedFeatureException ex)
{
    Console.WriteLine(ex.Feature); // Output: render3d-csg-open-mesh
}
```

### Example 22: Extruding a Polygon With a Hole

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

// A 20 x 20 square plate with a round hole: the second contour lies inside the first, so it is a
// hole (the winding of the contours does not matter). Invalid polygons, such as a self-intersecting
// bow tie or contours that touch, throw ArgumentException here, when the region is created.
var plate = new PolygonRegion(
[
    PrimitiveTessellator.Rectangle(new Vector2D(20, 20), center: true).Contours[0],
    PrimitiveTessellator.Circle(5.0, 48).Contours[0],
]);

// Extrude 4 units along Z: one closed solid with a through-hole, outward-oriented
var plateMesh = Extruder.Linear(plate, 4.0, center: true);
Console.WriteLine(plateMesh.IsClosed); // Output: True

// A star twisted by 90 degrees (positive is counter-clockwise from above) and tapered to a point.
// Twisted solids are approximated by slices, so use enough of them.
var star = PrimitiveTessellator.Polygon(Enumerable.Range(0, 10)
    .Select(i => new Vector2D(
        (i % 2 == 0 ? 3.0 : 1.2) * Math.Cos(i * Math.PI / 5.0),
        (i % 2 == 0 ? 3.0 : 1.2) * Math.Sin(i * Math.PI / 5.0)))
    .ToArray());
var spire = Extruder.Linear(star, 8.0, twistDegrees: 90.0, slices: 32, scale: Vector2D.Zero);

// A vase: revolve a profile (X = radius, Y = height) that stays on one side of the Z axis
var vase = Extruder.Rotate(
    PrimitiveTessellator.Polygon([new(0, 0), new(3, 0), new(4, 3), new(2, 6), new(2.5, 8), new(0, 8)]),
    360.0,
    48);

var scene = new Scene(new GroupNode(
[
    new MeshNode(plateMesh) { Appearance = new NodeAppearance(new Rgba32(70, 130, 180, 255)) },
    new MeshNode(spire)
    {
        Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 0, 2)),
        Appearance = new NodeAppearance(new Rgba32(220, 170, 60, 255)),
    },
    new MeshNode(vase)
    {
        Transform = Matrix4x4D.CreateTranslation(new Vector3D(25, 0, -2)),
        Appearance = new NodeAppearance(new Rgba32(190, 90, 80, 255)),
    },
]));

using var image = SceneRenderer.RenderFitted(scene, 800, 400);
PngCodec.Save(image, "extrusions.png");
```
