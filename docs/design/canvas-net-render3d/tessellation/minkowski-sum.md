### MinkowskiSum

<!-- cspell:ignore Minkowski convexity convex coplanar polytope welded octahedron pentagram chord -->

The `MinkowskiSum` unit computes the Minkowski sum of two convex solids. It is the public static class
`MinkowskiSum` (`MinkowskiSum.cs`: `ConvexSum`, `MaxPairwiseSums`) in the
`DemaConsulting.CanvasNet.Render3D.Tessellation` namespace.

#### Purpose

`ConvexSum` returns the Minkowski sum of two convex solid meshes as the exact convex hull of all pairwise vertex
sums. The hull of the sums is only the Minkowski sum when both operands are convex, so each operand is verified;
an operand that is not provably a closed convex solid is **rejected** with a `NotSupportedException` and never
approximated. The result is a valid constructive-solid-geometry operand. The unit supports the OpenSCAD
`minkowski` module for convex operands.

#### Data Model

`MinkowskiSum` is stateless and thread-safe. `MaxPairwiseSums` (2^21 = 2,097,152, public) is the documented bound
of the product of the operands' distinct vertex counts; it is public so that callers can pre-check without a second
source of truth. `ConvexityTolerance` (internal, `1e-9`) is the relative tolerance of the convexity test.

#### Key Methods

`ConvexSum(Mesh a, Mesh b, CancellationToken cancellationToken = default)`:

1. Validate the arguments; collect each operand's distinct referenced vertex positions (vertices at exactly the
   same position are welded with `MeshTopology.Weld`); reject an operand without triangles; check
   `Va * Vb <= MaxPairwiseSums` in `long` arithmetic **before** any large allocation or hull computation.
2. Verify each operand (see below).
3. Generate the points `a_i + b_j` (one double addition each, polling the token every few thousand sums) and
   compute the hull of them with `ConvexHull.Hull3DInPlace`, which takes the array over (no second copy). The
   result is the exact hull of the *rounded* sums (an error of at most
   one unit in the last place per coordinate). The hull depends only on the point set and IEEE addition is
   commutative, so `ConvexSum(a, b)` and `ConvexSum(b, a)` are byte-identical, and so are results for operands
   with permuted, repeated or unwelded vertices.

**Convexity test (soundness and tolerance).** An operand `M` is accepted only if all of these hold:

- *Exact structural preconditions*: the mesh is closed, edge-manifold, consistently wound and has no degenerate
  triangle (`OperandDefects` is empty), is not inverted (`IsInverted`), forms one connected shell (union-find over
  the welded indices), and has four affinely independent vertices (the hull of its vertices exists) with a positive
  volume above the rounding bound.
- *Geometric test*: `H = Hull3D(distinct vertices of M)` (exact). `M`'s vertices are `H`'s points, so `M` lies
  inside `H`; `M` is convex exactly when it fills `H`. It is accepted when
  `|Vol(M) - Vol(H)| <= tau * Vol(H) + e(M) + e(H)` and `|Area(M) - Area(H)| <= tau * Area(H)` with
  `tau = 1e-9` and `e` the rigorous rounding-error bound of `MeshTopology.SignedVolume`.

**Tolerance statement.** A mesh is accepted only if its solid differs from its convex hull by at most `1e-9` of
the hull volume and surface area. No macroscopic non-convexity can pass: every dent, notch, second shell, chord
triangulation of vertices in convex position and multiply wrapped surface changes the volume or the area by far
more than that. Rounding-level non-planarity (rotated cubes, cylinder caps and sphere quads, about `1e-16`
relative) passes, which is what makes the common rounded-box model (a cube plus a sphere) work. The result is
computed from the hull of the operand's vertices, so for an accepted operand the sum is exactly the sum of the
hull. Models with a huge offset relative to their size (more than about `1e6` times the feature size) can fail
the test through coordinate rounding; they are reported, never approximated.

**Non-finite measurements.** For coordinates beyond about `1e102` the volume and area overflow, and every comparison
with NaN is false, so a tolerance test written as "reject if the difference is above the limit" would accept the
operand. The volume, area and error bounds of the operand and of its hull are therefore checked to be finite first
(otherwise `NotSupportedException`, "too large to measure"), and the positivity and tolerance tests are written as
negated acceptance conditions, so that any remaining NaN fails the check instead of passing it.

**Why a local (per-edge) tolerance was rejected.** A local test (every edge's dihedral angle or height deviation
below a tolerance) is unsound: a gently concave bowl built from many facets passes any per-edge tolerance while
the total deviation accumulates (a thousand edges each bending by `1e-3` radian deviate by a full radian), and
tiny facets pass any angle test. The chosen test is global, so it cannot be fooled by many small local steps.

**Complexity.** O(`Va * Vb` log(`Va * Vb`)) time and O(`Va * Vb`) memory. The array of sums is the only copy of the
point set: `ConvexHull.Hull3DInPlace` sorts and de-duplicates it in place instead of copying it. At the bound
(2^21 points of 24 bytes) the array is about 50 MB, and the peak is about **70 MB**: that array plus about 9 bytes
per point of construction tables (about 20 MB; the hull builder's scratch map, and the used-flag and index map of
the mesh extraction), plus face tables of about 70 bytes per hull vertex, which for typical operands is far
smaller than the point set. Each operand's convexity test is one hull of its own vertices (at most `2^19`
vertices, since the other operand has at least four) and is a small fraction of that peak.

**Internal members for callers that sum many pairs.** `RequireConvexSolid(Mesh, string, CancellationToken)`
(internal, visible to the Scad assembly) performs the exact verification of `ConvexSum` for one operand, and
`ConvexSumOfVerified(Mesh, Mesh, CancellationToken)` (internal) is `ConvexSum` without the verification, for
operands that have passed `RequireConvexSolid` or are outputs of `ConvexHull.Hull3D` or of an earlier sum. A fold of
`k` operands therefore verifies each member once instead of re-verifying the accumulated hull at every pair. The
work bound and the argument checks are unchanged; the result is identical to `ConvexSum`.

#### Error Handling

`ArgumentNullException` for a null operand. `ArgumentException` for an operand without triangles, an operand
without volume (the vertices are collinear, coplanar or too few; for example a flat closed double-sided
triangle), and a vertex-count product above `MaxPairwiseSums` (the message names the limit). `NotSupportedException`
for an operand that is not provably a closed convex solid; the message names the operand and the reason (open,
non-manifold, inconsistently wound, degenerate triangles, inside out, several shells, no positive volume, or not
convex within the tolerance). `OperationCanceledException` when the token is canceled (checked on entry, while the
pairs are generated and inside the hull). Structural defects take precedence over the no-volume check, so an open
flat operand is reported as `NotSupportedException`.

#### Dependencies

`ConvexHull` (`Hull3D`) of this subsystem, and the `Mesh`, `MeshTopology` (internal: `Weld`, `SignedVolume` with
error bound, `SurfaceArea`) and `Vector3D` types of the `SceneModel` subsystem. No dependency on `SceneRendering`;
the unit tests use `Render3DTestHelpers.AssertValidCsgOperand` (test-only) through `InternalsVisibleTo`.

#### Callers

Callers that need the sum of two convex solids; the Scad geometry builder (`minkowski` module) verifies every member
once with `RequireConvexSolid`, sums with `ConvexSumOfVerified`, and reports a `NotSupportedException` as an
unsupported feature.
