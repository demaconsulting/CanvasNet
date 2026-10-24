### DoublePrecisionMath

<!-- cspell:ignore Rodrigues -->

The `DoublePrecisionMath` unit is the first software unit in the `SceneModel` subsystem. It comprises
four public types in the `DemaConsulting.CanvasNet.Render3D.SceneModel` namespace, all immutable
value types holding 64-bit floating-point components:

- `Vector2D` (`readonly record struct`): a 2D vector
- `Vector3D` (`readonly record struct`): a 3D vector
- `Matrix4x4D` (`readonly struct`): a 4x4 matrix
- `BoundingBox3D` (`readonly record struct`): an axis-aligned bounding box

#### Purpose

`DoublePrecisionMath` provides the double-precision vector, matrix, and bounding-box arithmetic the
rest of `CanvasNetRender3D` is built on. Keeping all model-space math in 64-bit floating point is
what lets the renderer place geometry far from the origin without visible precision loss (see
_CanvasNetRender3D System Design_, `../../canvas-net-render3d.md`, Coordinate and Precision
Conventions). The unit exists in this repository rather than reusing `System.Numerics` because the
BCL's vector and matrix types are single precision.

#### Data Model

- **`Vector2D(X, Y)`** and **`Vector3D(X, Y, Z)`**: positional records with `double` components,
  value equality, and the static members `Zero`, `UnitX`, `UnitY` (and `One`, `UnitZ` for
  `Vector3D`). `IsFinite` is true when every component is finite.
- **`Matrix4x4D`**: sixteen read-only `double` properties `M11` to `M44`. It uses the same
  **row-vector convention** as `System.Numerics.Matrix4x4`: a point transforms as `v' = v * M`,
  translation lives in `M41` to `M43`, and `a * b` applies `a` first and then `b`. The
  `default(Matrix4x4D)` value is the **zero** matrix (a struct cannot have a non-trivial default
  constructor); `Matrix4x4D.Identity` is the identity, and every property of another type that holds
  a `Matrix4x4D` is initialized with `Identity`. Construction is through the factory methods or
  `FromRowMajor`; the 16-argument constructor is private.
- **`BoundingBox3D(Min, Max)`**: a positional record. `BoundingBox3D.Empty` has `Min` at positive
  infinity and `Max` at negative infinity, so it is the identity of `Union`. `IsEmpty` is true when
  any `Min` component exceeds the corresponding `Max` component. `Size`, `Center`, and `Diagonal`
  return zero for an empty box.

Invariants: no type validates finiteness on construction (the value types are plain data); callers
that need a guarantee test `IsFinite`, and the types that consume them (`Mesh`, `SceneNode`)
validate it.

#### Key Methods

##### Vector2D and Vector3D

- Operators `+`, `-` (binary and unary), `*` (vector times scalar, in both operand orders), and `/`
  (vector divided by scalar), all component-wise.
- `Dot(a, b)`; `Cross(a, b)` (a vector for `Vector3D`; the scalar z component for `Vector2D`).
- `LengthSquared`, `Length`. For `Vector2D` and `Vector3D`, `Length` and `Normalize` divide by the
  largest absolute component before squaring, so a finite vector never overflows or flushes to zero
  (for example `(1e200, 0, 0)` has length `1e200` and `(1e-200, 0, 0)` has length `1e-200`).
  `LengthSquared` remains the plain sum of squares and can overflow or underflow, so zero and
  degeneracy checks on caller-supplied vectors use `Length` (or an exact zero test), never
  `LengthSquared`.
- `Normalize()`: returns the unit-length vector, or `Zero` when the length is zero or a component is
  NaN (documented, so callers can test the result's `Length`).
- `Vector3D.Min(a, b)` / `Max(a, b)`: component-wise minimum / maximum.
- `Vector3D.ToVector3()`: the explicit single-precision narrowing to `System.Numerics.Vector3`, used
  only at the documented narrowing step of the renderer.

##### Matrix4x4D

- `FromRowMajor(ReadOnlySpan<double>)`: builds a matrix from exactly 16 elements in row-major order;
  any other length throws `ArgumentException`.
- `CreateTranslation(Vector3D)`, `CreateScale(Vector3D)`, `CreateScale(double)`.
- `CreateRotationX/Y/Z(degrees)` and `CreateFromAxisAngle(axis, degrees)` (Rodrigues' rotation
  formula; right-handed, so `CreateRotationZ(90)` maps +X to +Y). Sine and cosine come from a helper
  that returns exact 0 and plus or minus 1 for any angle within 1e-12 quarter turns of a multiple of
  90 degrees, so axis-aligned CAD rotations carry no rounding error. A zero or non-finite axis
  throws `ArgumentException`; any finite non-zero axis (including components of 1e-200 or 1e200) is
  accepted and normalized.
- `CreateMirror(Vector3D planeNormal)`: the reflection `I - 2 n n^T` across the plane through the
  origin with the given normal (the normal is normalized first); a zero or non-finite normal throws
  `ArgumentException`. The result has a negative determinant.
- `operator *`: the matrix product, applying the left operand first.
- `Determinant`: the 4x4 determinant.
- `TryInvert(out inverse)`: general 4x4 inverse by Gauss-Jordan elimination with partial pivoting;
  returns `false` (and the zero matrix) when the absolute determinant is below 1e-300 or the result
  is not finite.
- `TransformPoint(Vector3D)`: transforms a point, including translation. `TransformDirection`:
  transforms a direction, ignoring translation.
- `IsFinite`, `Equals`, `GetHashCode`, `==`, `!=`, `ToString`: element-wise equality semantics.

##### BoundingBox3D

- `Union(BoundingBox3D)` and `Union(Vector3D)`: the smallest box containing both operands; unioning
  with an empty box returns the other operand.
- `Transform(Matrix4x4D)`: the axis-aligned box of the eight transformed corners (possibly larger
  than the tight bounds of the transformed content when the matrix rotates by a non-multiple of 90
  degrees); an empty box stays empty.

#### Error Handling

The unit throws `ArgumentException` for a wrong-length `FromRowMajor` input and for a zero or
non-finite rotation axis or mirror normal. All other operations are total: `Normalize` of a zero
vector returns `Zero`, `TryInvert` reports failure through its return value, and arithmetic follows
IEEE 754 semantics (non-finite results propagate and are detected by `IsFinite`). Nothing is caught
locally.

#### Dependencies

The .NET base class library only (`System.Numerics.Vector3` for `ToVector3`, `System.Math`). No
other unit or subsystem.

#### Callers

`Mesh`, `MeshBuilder`, `SceneNode`, `Scene`, and `PrimitiveTessellator` use the vector, matrix, and
bounding-box types; `Camera`, `ViewTransform`, `LightingModel`, `SceneRenderer`, and the clipping
and projection helpers of `TriangleRasterizer` use `Vector3D` and `Matrix4x4D` for the
double-precision view transform. The types are also public API for callers building scenes.
