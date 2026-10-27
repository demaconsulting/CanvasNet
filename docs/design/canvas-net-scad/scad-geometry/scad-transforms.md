### ScadTransforms

<!-- cspell:ignore multmatrix newsize unioned underflows evaluator subtree aliceblue yellowgreen rebeccapurple -->

The `ScadTransforms` unit is the third software unit in the `ScadGeometry` subsystem. It comprises four source
files in the `DemaConsulting.CanvasNet.Scad.Geometry` namespace, all `internal`:

- `ScadTransforms.cs` — `ScadTransforms` (static partial class): `translate`, `rotate`, `scale`, `mirror`,
  `multmatrix`, the shared `Apply` step, the singularity rule, the argument readers, and the invalid-argument
  helper
- `ScadTransforms.Resize.cs` — the `resize` module and its per-axis factor computation
- `ScadColors.cs` — `ScadColors` (static class): the `color` module and the parsing of color values
- `ScadColorTable.cs` — `ScadColorTable` (static class): the immutable table of named colors

#### Purpose

`ScadTransforms` turns the transform modules of the language into scene geometry. Each module receives the
already built results of its children from the walk of `ScadGeometryBuilder` and returns the result for the
instance: a transform applies to the **implicit union** of its children, a solid is wrapped in a Render3D
`GroupNode` that carries the matrix (the identity adds no wrapper), and a 2D shape gets a planar transform node.
`color` follows the same pattern with a `NodeAppearance` instead of a matrix. The unit never walks the
instantiation tree itself, so it has no recursion and no stack requirement: every method does a bounded amount
of work on the results of the children. The unit is the only place that knows the argument forms, the
conventions (Euler order, column versus row vectors), and the degradation rules of the transform and color
modules.

The color table was copied from the named-color table of the repository's own SVG codec (`SvgCodec.Paint.cs`)
and is a public W3C table (see _CanvasNetScad System Design_, Clean-Room Provenance); no OpenSCAD source or
manual text was consulted for any behavior of this unit.

The unit's behavior is specified by the `CanvasNetScad-ScadGeometry-ScadTransforms-*` requirements (`Translate`,
`Rotate`, `Scale`, `Mirror`, `Multmatrix`, `MultmatrixProjective`, `Identity`, `Composition`,
`InvalidArguments`, `Singular`, `FlatTransforms`, `Resize`, `ResizeBounds`, `ResizeDegenerate`, `ColorForms`,
`ColorInvalid`, `ColorNesting`, and `ColorAlpha`) in
`docs/reqstream/canvas-net-scad/scad-geometry/scad-transforms.yaml`.

#### Data Model

**Module signature.** Every module method has the shape of the builder's container delegate:
`ScadGeo Method(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)`. `results` holds one
`ScadGeo` per child in order (empty ones included). `ScadGeo` (see _ScadGeometry Subsystem Design_) is a
union of Empty, Solid (a scene node), and Flat (a `ScadShape2D` tree), with a bounding box and two flags:
`IsTight` (the box is exactly the extent of the vertices) and `IsApproximate` (a difference or intersection is
inside, so the box is an upper bound only).

**Parameter tables.** Each module binds its arguments through `ScadArguments.Bind` with a fixed positional
parameter list: `v` (`translate`, `scale`, `mirror`), `a`, `v` (`rotate`), `m` (`multmatrix`), `newsize`, `auto`,
`convexity` (`resize`), and `c`, `alpha` (`color`). Names that start with `$` are skipped by the binder (the
evaluator already resolved special variables), unknown names and surplus positional values warn, and a
parameter given twice keeps its last value with a warning.

**`ReadState`** (internal enum): `Absent` (not supplied or `undef`), `Valid`, `Invalid`. It lets a module tell
"no argument" (the identity) from "a bad argument" (a warning).

**Matrices.** Render3D uses the row-vector convention (`Matrix4x4D`, a point is multiplied from the left, the
translation sits in the fourth row). The language uses column vectors. The unit builds every matrix in the
Render3D convention (`CreateTranslation`, `CreateScale`, `CreateRotationX/Y/Z`, `CreateFromAxisAngle`,
`CreateMirror`) and transposes the one matrix that comes from the user (`multmatrix`).

**`SingularTolerance`** is `1e-12`: a determinant whose magnitude is at most this fraction of the product of the
row lengths of the linear part is treated as zero.

**`ScadColorTable.Colors`** is a `FrozenDictionary<string, Rgba32>` with an ordinal case-insensitive comparer
holding the 148 CSS named colors (the extended color keywords, including both `gray` and `grey` spellings) plus
`transparent` (0, 0, 0, 0): 149 entries. It is immutable and thread-safe.

#### Key Methods

##### Transform modules

Each module reads its arguments, builds a `Matrix4x4D`, and hands it to `Apply`:

- **`Translate`** — `v` is a vector of one to three finite numbers; missing components are 0. An absent `v` is
  the identity.
- **`Rotate`** — a numeric `a` rotates about Z; with a numeric `a` and a `v` of exactly three finite numbers it
  is an axis-angle rotation by the right-hand rule (`CreateFromAxisAngle`; an axis of any length is
  normalized by the Render3D factory). A vector `a` of one to three angles in degrees is the Euler form: the
  matrix is `Rx * Ry * Rz` in row-vector order, so **X is applied first, then Y, then Z**. A `v` given with a
  vector `a` is ignored with the warning `rotate: v is ignored when a is a vector`. An axis whose components are
  all zero warns `rotate: the axis v is zero; the children are not rotated` and uses the identity, so the
  children are kept; the test is on the components, not on the length, so an axis whose length overflows (such
  as `[1.7e308, 1.7e308, 1.7e308]`) or whose square underflows is a perfectly good direction. An angle of huge
  magnitude (1e15 degrees or more) is reduced modulo 360 (exact for doubles) before the matrix is built, so the
  rotation is the same on every platform. An absent `a` is the identity. A `v` that is not a vector of three
  numbers, and an angle that is not a finite number, is an invalid argument.
- **`Scale`** — a number is a uniform factor; a vector of one to three numbers gives the per-axis factors,
  missing components are 1. A negative factor is a reflection: the wrapper's matrix has a negative determinant
  and Render3D flips the winding of such an instance itself, so the world mesh stays closed and outward
  oriented.
- **`Mirror`** — `CreateMirror(v)`, a reflection through the plane through the origin with normal `v` (any
  length); missing components are 0. A normal whose components are all zero warns `mirror: the normal v is zero;
  the children are not mirrored` and uses the identity, so the children are kept (a normal whose length merely
  overflows is a direction, as for `rotate`).
- **`Multmatrix`** — `m` is a vector of three or four rows of exactly four finite numbers. A fourth row other
  than `[0, 0, 0, 1]` (tested exactly for the first three entries and with `ScadNumbers.AreEqual` for the last)
  is **projective**: it cannot be rendered, so it is reported as the unsupported feature
  `scad-multmatrix-projective` at the instance location and the children are omitted (no warning). Otherwise the
  matrix is transposed into row-vector convention (the fourth column becomes the translation row) with an
  implicit last column `[0, 0, 0, 1]` and passed to `Apply`.

##### `Apply(node, results, context, matrix)`

The shared step of every transform: it forms the implicit union of the children with `ScadBooleans.Union`
(which also reports a 2D/3D mix) and returns it unchanged when it is empty or the matrix equals the identity
(**no wrapper node**). Otherwise a solid goes to `ApplySolid` and a 2D shape to `ApplyFlat`.

- **`ApplySolid`** rejects a matrix that is not finite or whose linear part is singular (see below), then wraps
  the node in a `GroupNode` with `Transform = matrix`. Render3D composes a chain of such groups child-first.
  The bounds are transformed with the matrix; the result stays tight only when the matrix maps boxes to boxes
  exactly (`IsAxisAligned`: every row and column of the linear part has exactly one non-zero entry — scaled
  permutations, such as translations, scales, mirrors, and rotations by multiples of 90 degrees); the
  approximate flag is carried over.
- **`ApplyFlat`** rejects a non-finite matrix, then applies only the **in-plane part** (`M11 M12 M21 M22` and
  the XY translation) as a planar `ScadShape2D` transform node. When the matrix couples Z (`M13`, `M23`,
  `M31`, `M32`, or the Z translation `M43` is not zero) the warning `<module>: the Z part of the transformation
  is ignored for 2D objects` is reported at the instance; a Z scale alone (`M33`) is ignored silently. A
  singular in-plane part is rejected. A hole follows its outline because the transform applies to the whole
    shape tree, not to individual contours.

**Singularity rule.** A linear part is singular when its determinant is not finite, is exactly zero, or has a
magnitude of at most `1e-12` times the product of the three row lengths (for a 2D shape, the 2x2 determinant
against the product of the two row lengths). The relative bound keeps a legitimately tiny uniform scale valid
while rejecting a nearly collapsed matrix, whose determinant could underflow in the renderer.

##### `Resize`

`resize(newsize, auto, convexity)` scales about the origin so that the bounding box of the children gets
the requested size:

1. `newsize` is a vector of one to three non-negative finite numbers (missing components 0 = not requested);
   `auto` is a flag for all axes (truthiness) or a vector of one to three flags (missing flags false). A bad
   `newsize` or `auto` is an invalid argument. An absent `newsize` is the identity (`Apply` with the identity).
   `convexity` is accepted and ignored without a report.
2. The children are unioned. An empty union is returned as is (silently).
3. **Bounds.** If the union is approximate (a difference or an intersection is inside), the propagated upper
   bound is used and the unsupported feature `scad-resize-approximate` is reported at the instance; the resize
   is still applied with that extent. Otherwise, if the box is not tight (a rotation that is not a multiple of 90
   degrees loosened it), the **exact vertex bounds** are recomputed on demand — `new Scene(node).ComputeBounds(
   includeOverlays: false)` for a solid (a `%` ghost is not part of the geometry whose size is requested),
   `ScadShape2D.ComputeVertexBounds()` for a 2D shape — so a rotated child ends up with exactly the
   requested size. A ghost also contributes no propagated bounds, so the tight-box path ignores it too (see
    _ScadGeometryBuilder Unit Design_); it remains part of the scene bounds that fit the default camera.
4. **Factors** (`ComputeFactors`). Per axis (three for a solid, only X and Y for a 2D shape), a positive
   `newsize` component gives `newsize / size`; the first such factor is the reference. An axis with a positive
   `newsize` but no extent warns `resize: the children have no extent along X; they are not resized along it`
   (and is skipped). A component that is zero keeps factor 1 unless `auto` is set for that axis and a reference
   exists, in which case it takes the reference factor (the proportions of the first requested axis).
5. The factors become a scale matrix that goes through `Apply` (so all-ones is the identity and a collapsed
   factor is a singular transform).

##### Color

`Color(node, results, context)` binds `c` and `alpha`:

- `c` goes through **`TryParse`**: a string is a name from `ScadColorTable.Colors` (case-insensitive) or, when
  it starts with `#`, a hexadecimal color with 3 (`#rgb`), 4 (`#rgba`), 6, or 8 digits (**`TryParseHex`**:
  short forms repeat each digit by multiplying with 17, only hexadecimal digits are accepted — no sign and no
  white space); a vector of three or four numbers is clamped to 0..1 per channel and rounded to the nearest of
  256 levels, rounding half away from zero (`ToByte`). Anything else is invalid.
  The parsed color is **always opaque** (`A = 255`); the alpha of the value (4th vector component, the last two
  hex digits, or the table entry — `transparent` has alpha 0) is returned separately.
- `alpha` must be a finite number and is clamped to 0..1; it **overrides** any alpha from `c`.
- An invalid `c` warns `color: c must be a color name, a hex string …; the color is ignored` and an invalid
  `alpha` warns `color: alpha must be a number from 0 to 1; it is ignored`; both keep the children.
- If the final alpha is below 1 it is carried into the appearance (see below) and nothing is reported (Phase 8;
  the former `scad-color-alpha` report no longer exists). An `alpha` **without** a `c` argument has nothing to
  apply to: it warns `color: alpha needs a color c; it is ignored` and colors nothing.
- The children are unioned; when the union is empty or no valid `c` was given (`color()`, `color(alpha=…)`, an
  invalid color) it is returned unchanged. Otherwise a solid is wrapped in a `GroupNode` with
  `Appearance = new NodeAppearance(new Rgba32(r, g, b, round(alpha * 255)))` (alpha 1 when none was given;
  rounding half away from zero), and a 2D shape in a `ScadShape2D` color node (so an extruded shape and a flat slab
  keep the alpha); the bounds and
  flags are carried over. Because Render3D resolves the appearance of the innermost group, the **innermost
  color wins**, alpha included, and siblings are unaffected. The renderer honors the alpha (see the Render3D
  transparency design): a surface with alpha below 255 is composited, alpha 0 is invisible but still solid, and
  a body that is translucent must be a closed solid.

##### Shared helpers

`Invalid(node, context, problem)` reports `<module>: <problem>; the instance is ignored` as a warning at the
instance location and returns `ScadGeo.Empty`. `ReadVector`/`TryReadVector` read a vector of one to three
finite numbers padded with a fill value; `ReadMatrixRows` reads three or four rows of four finite numbers.

#### Dialect Decisions And Reported Identifiers

| Behavior | Rule as built |
| --- | --- |
| Euler `rotate([x, y, z])` | `Rx * Ry * Rz`, row-vector order: X first, then Y, then Z |
| `rotate(a)` | rotation about Z; `rotate(a, v)` axis-angle, right-hand rule |
| `multmatrix(m)` | column-vector input transposed; 4x4 or the first three rows |
| Identity transform | returns the union of the children, no wrapper node |
| Singular or non-finite transform | warning at the instance; empty result (never a Render3D report) |
| Invalid argument | warning at the instance; empty result |
| Documented exceptions | Zero `rotate` axis, zero `mirror` normal, invalid color or alpha: warning, children kept |
| Absent argument | identity |
| 2D transform | in-plane part only; warning when Z is coupled |
| `resize` bounds | exact; recomputed from vertices after a non-axis-aligned rotation |
| `resize` over `difference`/`intersection` | `scad-resize-approximate`; still resized with the estimated extent |
| Projective `multmatrix` | `scad-multmatrix-projective`; children omitted |
| Alpha below 1, `transparent` | carried into the appearance alpha and rendered; no report |
| `convexity`, `$`-names | accepted and ignored silently |

Identifiers reported by this unit (`ScadFeatureIds`): `scad-multmatrix-projective` and `scad-resize-approximate`.
All other problems are warnings.

#### Error Handling

- **Warning** (located at the instance, the instance yields nothing unless noted): a supplied argument of the
  wrong type or length, a non-finite number, a negative `newsize`, a malformed `multmatrix`, a singular or
  non-finite transform, and unknown or surplus arguments (the transform is still applied). Exceptions that keep
  the children: a zero `rotate` axis, a zero `mirror` normal, `v` ignored for a vector `rotate`, an invalid
  color or alpha, and the Z part of a transform of a 2D shape. A `resize` axis without extent is skipped with a
  warning.
- **Unsupported feature**: `scad-multmatrix-projective` and `scad-resize-approximate`.
  In strict mode (`ThrowOnUnsupportedFeature`) the reporter throws `ScadUnsupportedFeatureException` at the
  first report of a feature at a location (repeats at the same location are silent); the unit does not catch
  it.
- **`ScadEvaluationException`** — never thrown by this unit. A transform whose world product later
  underflows is reported by the render stage (`scad-csg-singular-transform`, see _ScadGeometryBuilder Unit
  Design_), not here.
- **Cancellation** is polled by the builder's walk, not by this unit; no module method loops over the scene.

#### Dependencies

- **`ScadInstance`**, **`ScadValue`**, **`ScadValueKind`**, **`ScadNumbers`** (`Evaluation` subsystem) — the
  instance with its name, arguments, and source location, and the argument values.
- **`ScadArguments`** and **`ScadReporter`** (`ScadPrimitives` and `ScadGeometryBuilder` units) — argument
  binding with the unknown/surplus/`$` rules, and the warning and unsupported-feature channel.
- **`ScadGeo`**, **`ScadGeoKind`**, **`ScadGeometryContext`**, **`ScadFeatureIds`** (the builder unit) — the
  result type with bounds, the context carrying the reporter, and the identifiers.
- **`ScadBooleans.Union`** (`ScadGeometryBuilder` unit) — the implicit union of the children.
- **`ScadShape2D`** (`ScadExtrusions` unit) — the planar transform and color nodes and the exact vertex bounds
  of a 2D shape.
- **Render3D public types** — `Matrix4x4D`, `Vector3D`, `BoundingBox3D`, `GroupNode`, `NodeAppearance`,
  `Scene`.
- **Core types** — `Rgba32` (`DemaConsulting.CanvasNet.Canvas`).
- **.NET base class library** — `System.Collections.Frozen`, `System.Globalization` (invariant parsing).

#### Callers

`ScadGeometryBuilder` is the only production caller. Its module table registers `ScadTransforms.Translate`,
`Rotate`, `Scale`, `Mirror`, `Multmatrix`, `Resize`, and `ScadColors.Color` as container builders; the
iterative walk calls each once per instance, after the children are built. The unit tests in
`test/DemaConsulting.CanvasNet.Scad.Tests/Geometry/ScadTransformsTests.cs`, `ScadResizeTests.cs`, and
`ScadColorsTests.cs` evaluate small programs through `ScadGeometryTestSupport.Build` (the evaluator and the
builder through `InternalsVisibleTo`) and call `ScadColorTable.Colors` directly. The approximate-bounds report
of `resize` is additionally exercised by `ScadGeometryBuilderBooleanTests` and the deep transform chains by
`ScadGeometryBuilderTransformDepthTests` and `ScadGeometryBuilderStackTests`.
