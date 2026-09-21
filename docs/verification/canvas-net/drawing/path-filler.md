## PathFiller Unit Verification Design

This document describes the unit-level verification strategy for the `PathFiller` class (and the
supporting `FillRule` enum and internal `EdgeFlattener`/`ScanlineRasterizer` helpers).

### Verification Approach

The `PathFiller` unit is verified through unit tests that exercise `PathFiller.Fill` end to end
(constructing a `Path`, filling it onto a `Surface`, and inspecting resulting pixels), plus
dedicated unit tests for the internal `EdgeFlattener` and `ScanlineRasterizer` helpers (accessible
to the test project via `InternalsVisibleTo`) that verify their narrower contracts in isolation.
Expected antialiased coverage values throughout are **hand-computed analytically** from the
geometry under test - the exact fraction of each pixel's unit cell (`[x, x+1) x [y, y+1)`, per
the coordinate convention documented in _PathFiller Unit Design_, `../../design/canvas-net/
drawing/path-filler.md`) lying inside the filled region - independently of the implementation
under test, not by re-deriving the same rasterization formula. Byte-level expected alpha values
account for `Surface`'s `MidpointRounding.AwayFromZero` blend-pipeline convention where relevant
(for example, `255 * 0.5 = 127.5` rounds to `128` under round-half-away-from-zero).

Unit tests reside in `PathFillerTests.cs`, `EdgeFlattenerTests.cs`, and
`ScanlineRasterizerTests.cs` within the `DemaConsulting.CanvasNet.Tests.Drawing` project
namespace.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `PathFiller` has no injectable dependencies

### Hand-Computed Reference Values

The following geometric constructions and their exact hand-computed coverage results are used
across the scenarios below:

- **Axis-aligned rectangle with a fractional-pixel edge**: a rectangle from `(0.5, 0.5)` to
  `(3.5, 2.5)` on a 4x4 surface. Interior columns 1 and 2 (at row 1) are fully covered (alpha
  `255`); edge columns 0 and 3 are covered at exactly half (raw coverage `0.5`, alpha
  `255 * 0.5 = 127.5`, which rounds to `128` under round-half-away-from-zero).
- **Right triangle with a diagonal hypotenuse**: the triangle `(0,0)`, `(2,0)`, `(0,2)` on a 2x2
  surface. Cell `(0,0)` lies entirely inside the triangle (coverage `1.0`, alpha `255`); cells
  `(1,0)` and `(0,1)` are each split exactly in half by the diagonal hypotenuse (coverage `0.5`,
  alpha `128`); cell `(1,1)` lies entirely outside (coverage `0`, alpha `0`).
- **Overlapping same-wound rectangles (bowtie-equivalent self-overlap)**: two identically wound,
  integer-aligned 4x4 rectangles offset so they overlap by a 2x2 region, on an 8x8 surface. Every
  covered pixel has an exact (non-antialiased) integer winding count: singly-covered pixels have
  winding magnitude 1 (alpha `255` under both fill rules), while the doubly-covered overlap region
  has raw winding magnitude 2 - `NonZero` clamps this to fully opaque (`min(1, abs(2)) = 1`, alpha
  `255`), while `EvenOdd` folds it to fully transparent (`2 mod 2 = 0`, alpha `0`), directly
  demonstrating the two fill rules' divergence on self-overlapping geometry.
- **Nested, counter-wound subpaths forming a hole**: an outer, clockwise-wound 6x6 square and an
  inner, counter-wound (reverse vertex order) 2x2 square from `(2,2)` to `(4,4)`, both
  integer-aligned. The ring between the two subpaths has winding magnitude 1 (filled, alpha
  `255`); the inner subpath's own interior has winding magnitude 0 (the outer subpath's `+1`
  contribution and the inner subpath's `-1` contribution cancel), so it is left unfilled (alpha
  `0`) - the standard nested-subpath "hole" construction used, for example, by the letter "O" in
  vector font outlines.

### Unit-Level Test Scenarios

#### CanvasNet-Drawing-PathFiller-Fill: End-to-End Fill Composites the Requested Color

**Test**: `PathFiller_Fill_FullyCoveredInteriorPixel_ColorAndAlphaMatchOverOracle`

Fills a fully covered interior pixel of an axis-aligned square with a semi-transparent color onto
an opaque background, and separately composites the same color directly via
`Surface.CompositeOver(Rgba32)` onto an independent oracle surface pre-seeded with the same
background pixel. Asserts the two results are byte-identical, confirming `PathFiller.Fill`
performs the exact same Porter-Duff "over" blend as the existing `CompositeOver` overload for a
fully covered pixel, not merely "some" blend.

#### CanvasNet-Drawing-PathFiller-Antialiasing: Antialiased Coverage Matches Hand-Computed Reference Values

**Tests**: `PathFiller_Fill_AxisAlignedRectangleFractionalEdges_InteriorOpaqueEdgesAntialiased`,
`PathFiller_Fill_DiagonalTriangle_MatchesHandComputedCoverageGradient`,
`PathFiller_Fill_QuadraticCurveShape_TotalCoverageMatchesAnalyticBezierBulgeArea`

Fills the fractional-edge rectangle described above and asserts interior columns are alpha `255`,
edge columns are alpha `128` (exactly half coverage), and a pixel entirely outside the rectangle's
bounding box remains untouched (alpha `0`). Separately, fills the diagonal-hypotenuse triangle
described above and asserts the fully interior cell is alpha `255`, the two half-covered cells
straddling the hypotenuse are each alpha `128`, and the fully exterior cell is alpha `0` -
together confirming the analytic rasterizer produces genuine, correctly graded fractional
coverage along both an axis-aligned edge and a diagonal edge, not a hard-edged approximation.
Separately, fills a shape with a genuinely curved edge (a quadratic Bezier from `(0, 0)` to
`(4, 4)` with control point `(4, 0)`, implicitly closed by a straight chord) and asserts the sum
of every rendered pixel's fractional coverage across the whole surface matches the curve's own
analytic enclosed area (`2/3` of the control-point triangle's area, a standard Green's-theorem
identity for a quadratic Bezier's bulge area, evaluating to `16/3`) - this exercises actual
rendered pixel coverage end to end through `PathFiller` + `EdgeFlattener` + `ScanlineRasterizer`
for a curved edge, which the straight-edged rectangle/triangle scenarios above do not, and which
the `EdgeFlattener`-only tests (see below) do not either since they check flattened vertex
positions in isolation rather than integrated fill coverage.

#### CanvasNet-Drawing-PathFiller-FillRule: NonZero and EvenOdd Diverge on Self-Overlapping Geometry

**Test**: `PathFiller_Fill_OverlappingSameWoundRectangles_NonZeroVsEvenOddDiverge`

Fills the overlapping same-wound rectangles described above once with `FillRule.NonZero` and once
with `FillRule.EvenOdd`. Asserts the singly-covered pixel is fully opaque under both rules, while
the doubly-covered overlap pixel is fully opaque under `NonZero` but fully transparent under
`EvenOdd`, directly proving the two fill rules resolve overlapping winding differently.

#### CanvasNet-Drawing-PathFiller-Holes: Nested Counter-Wound Subpaths Render a Hole

**Test**: `PathFiller_Fill_NestedCounterWoundSubpaths_RendersHole`

Fills the nested counter-wound square-with-hole `Path` described above and asserts the ring
between the outer square and the inner hole is fully opaque at multiple sample points, while the
hole's own interior is fully transparent at multiple sample points, confirming winding-based hole
resolution across two subpaths within a single `Path`.

#### CanvasNet-Drawing-PathFiller-ImplicitClose: An Explicitly Open Subpath Fills Identically to a Closed One

**Test**: `PathFiller_Fill_ExplicitlyOpenSubpath_FillsIdenticallyToClosed`

Builds the same triangle twice - once left explicitly open (no `Close` call, `IsClosed` is
`false`) and once explicitly closed (`IsClosed` is `true`) - fills each onto its own surface, and
asserts every pixel matches exactly between the two surfaces, confirming the implicit-close rule
makes the `Close` call observably irrelevant to fill output.

#### CanvasNet-Drawing-PathFiller-EmptyOrOutOfBoundsNoOp: Empty or Out-of-Bounds Paths Are a No-Op

**Tests**: `PathFiller_Fill_EmptyPath_NoOpLeavesSurfaceUnchanged`,
`PathFiller_Fill_PathFullyOutsideSurfaceBounds_NoOpLeavesSurfaceUnchanged`

Fills `Path.Empty` onto a surface with a pre-existing pixel value and asserts the surface is
completely unmodified. Separately, fills a well-formed rectangle whose bounding box lies entirely
outside the surface's pixel extent and asserts the surface is likewise completely unmodified.
Neither case throws.

#### CanvasNet-Drawing-PathFiller-NullArguments: Fill Rejects Null Surface/Path

**Tests**: `PathFiller_Fill_NullSurface_ThrowsArgumentNullException`,
`PathFiller_Fill_NullPath_ThrowsArgumentNullException`

Calls `PathFiller.Fill` with a `null` surface, and separately with a `null` path, and asserts
`ArgumentNullException` is thrown in both cases.

#### CanvasNet-Drawing-PathFiller-NonPositiveTolerance: Fill Rejects a Non-Positive flattenTolerance

**Test**: `PathFiller_Fill_NonPositiveFlattenTolerance_ThrowsArgumentOutOfRangeException`

Calls `PathFiller.Fill` with `flattenTolerance` equal to `0`, and separately a negative value, and
separately each of `float.NaN`, `float.PositiveInfinity`, and `float.NegativeInfinity`, and
asserts `ArgumentOutOfRangeException` is thrown in every case (`[Theory]`-driven).

#### CanvasNet-Drawing-PathFiller-InvalidFillRule: Fill Rejects an Undefined FillRule Value

**Test**: `PathFiller_Fill_UndefinedFillRule_ThrowsArgumentOutOfRangeException`

Calls `PathFiller.Fill` with `fillRule` cast from an out-of-range integer value (`(FillRule)42`)
and asserts `ArgumentOutOfRangeException` is thrown, confirming an undefined `FillRule` value is
rejected at the public API boundary rather than silently falling through
`ScanlineRasterizer.ResolveCoverage`'s winding-resolution `else` branch and being treated as
`FillRule.EvenOdd`.

#### CanvasNet-Drawing-PathFiller-EdgeFlattenerConversion: EdgeFlattener Converts Each Command Type Correctly

**Tests**: `EdgeFlattener_Flatten_LineTo_ConvertsToExpectedPoint`,
`EdgeFlattener_Flatten_QuadraticBezierTo_DelegatesToBezierFlattening`,
`EdgeFlattener_Flatten_CubicBezierTo_DelegatesToBezierFlattening`,
`EdgeFlattener_Flatten_ArcTo_DelegatesToSvgArcConverterAndBezierFlattening`,
`EdgeFlattener_Flatten_MultipleSubpaths_RemainSeparatePolygons`,
`EdgeFlattener_Flatten_EmptyPath_ReturnsEmptyList`

Builds a single-subpath `Path` using one command type at a time (`LineTo`,
`QuadraticBezierTo`, `CubicBezierTo`, `ArcTo`) and asserts `EdgeFlattener.Flatten`'s output matches
the same points independently computed via direct calls to `BezierFlattening`/`SvgArcConverter`
(for the curve/arc cases) or the literal end point (for `LineTo`), confirming each command type is
converted correctly and that curves/arcs genuinely delegate to the existing Geometry primitives
rather than an independent, potentially divergent implementation. Separately, builds a
multi-subpath `Path` and asserts the result contains one separate polygon per subpath. Separately,
flattens `Path.Empty` and asserts an empty list is returned.

#### CanvasNet-Drawing-PathFiller-EdgeFlattenerImplicitClose: Implicit Close Is Applied Exactly Once

**Tests**: `EdgeFlattener_Flatten_OpenSubpath_AppendsImplicitClosingPoint`,
`EdgeFlattener_Flatten_AlreadyClosedSubpath_DoesNotDuplicateClosingPoint`

Flattens an explicitly open subpath whose last point does not coincide with its start, and
asserts the resulting polygon's final point equals the subpath's start (the implicit closing
point was appended). Separately, flattens a subpath whose last command's end point already equals
its start, and asserts no duplicate closing point was appended (the polygon's point count matches
the walked command count exactly, not one more).

#### CanvasNet-Drawing-PathFiller-ScanlineCoverageMath: Coverage Math Matches a Hand-Computed Sub-Pixel-Offset Reference

**Test**: `ScanlineRasterizer_Fill_SubPixelOffsetSquare_MatchesHandComputedCoverage`

Rasterizes a unit square offset by a known sub-pixel amount (for example `(0.5, 0.5)` to
`(1.5, 1.5)` on a 2x2 surface, where every one of the four pixels is covered by exactly one
quarter of its unit cell) directly via `ScanlineRasterizer.Fill`, and asserts every pixel's
resulting alpha exactly equals the hand-computed expected value (`255 * 0.25 = 63.75`, which
rounds to `64` under round-half-away-from-zero).

#### CanvasNet-Drawing-PathFiller-ScanlineFillRuleResolution: Fill-Rule Resolution Formulas Are Verified Directly

**Tests**: `ScanlineRasterizer_Fill_RawWindingOfTwo_NonZeroClampsEvenOddFoldsToZero`,
`ScanlineRasterizer_Fill_SubPixelOffsetDuplicatePolygons_SumsCoincidentContributionsPerCellAlgorithm`,
`ScanlineRasterizer_Fill_OverlappingSubPixelSquares_SumsPerCellContributionsPerCellAlgorithm`

Constructs geometry that produces an exact raw winding count of 2 at a given pixel (via
overlapping same-wound, pixel-aligned polygons) and rasterizes it once under each fill rule,
asserting `NonZero` resolves to fully opaque and `EvenOdd` resolves to fully transparent, directly
exercising both resolution formulas (`ResolveCoverage`) against a known raw value rather than only
the aggregate fill result. Separately, two further tests target the cell-based signed area/cover
accumulation algorithm's known, accepted trade-off for edges that fall within the same pixel
column: filling two exactly coincident, sub-pixel-offset duplicate rectangles, and separately
filling two overlapping (not coincident) sub-pixel-offset squares whose boundaries both land in
the same column, each assert the alpha values that independently verified hand-calculation (see
the type-level remarks on `ScanlineRasterizer`) predicts for this algorithm - both edges'
raw signed contributions summing linearly within the shared cell before `ResolveCoverage` folds
the total into `[0, 1]`. This is the documented, industry-standard (AGG/FreeType) trade-off
accepted in exchange for the crossing-edge correctness fix below, and both tests exist to detect
any accidental regression in that specific, intentional behavior.

#### CanvasNet-Drawing-PathFiller-ScanlineCrossingEdges: Self-Intersecting Polygons Produce Correct Partial Coverage

**Test**: `ScanlineRasterizer_Fill_BowtieSelfIntersectingPolygon_ProducesCorrectPartialCoverageNotFullFill`

Rasterizes a self-intersecting "bowtie" polygon (`(0,0)->(4,3)->(4,0)->(0,3)->close`) whose two
diagonal edges cross each other strictly inside a row (not at a shared vertex or row boundary),
and asserts the row containing the crossing point resolves to the analytically correct partial
coverage (`255, 170, 170, 255` across the four columns) under both fill rules, matching an
independently computed dense-supersampling ray-casting ground truth (not merely the value
reported in the originating code review). This is a regression test for the critical bug the
cell-based rewrite fixes: the prior sub-interval/sort-by-x algorithm produced ~100% over-fill for
this exact case, because it assumed edges spanning a sub-interval never change their relative
x-order within it - an assumption that crossing/self-intersecting edges violate by construction.

#### CanvasNet-Drawing-PathFiller-ScanlinePerformanceScaling: Many Overlapping Edges Scale Roughly Linearly

**Test**: `ScanlineRasterizer_Fill_ManyOverlappingRectangles_ScalesRoughlyLinearlyWithEdgeCount`

Fills two batches of many overlapping full-width rectangles in the same rows - one 8x larger than
the other - and asserts the larger batch's elapsed wall-clock time is no more than roughly 8x the
smaller batch's (with a generous tolerance to absorb CI scheduling noise), directly detecting the
medium-severity performance bug the cell-based rewrite fixes: the prior algorithm re-swept full
row-width buffers once per qualifying "inside" gap per sub-interval, producing measured
super-linear (~`O(edges^2 x width)`) scaling instead of the intended `O(edges + width)` per row.

#### CanvasNet-Drawing-PathFiller-ScanlineActiveEdgeList: Active-Edge-List Add/Remove Occurs at the Correct Rows

**Test**: `ScanlineRasterizer_Fill_EdgeStartingAndEndingMidSweep_StopsContributingAtCorrectRows`

Rasterizes a rectangle whose vertical extent spans only a subset of a taller surface's rows (for
example rows 1-3 of a 6-row surface), and asserts rows before the rectangle's top edge and rows
after its bottom edge remain completely untouched, while the rectangle's own rows are fully
filled - confirming each edge is added to, and removed from, the active edge list at exactly its
own `TopY`/`BottomY`, not the surface's full height.

#### CanvasNet-Drawing-PathFiller-ScanlineDegenerateInputNoOp: Degenerate Input Is a No-Op

**Tests**: `ScanlineRasterizer_Fill_NoPolygons_NoOp`, `ScanlineRasterizer_Fill_DegeneratePolygon_ContributesZeroCoverage`

Calls `ScanlineRasterizer.Fill` with an empty polygon list, and separately with a single polygon
reduced to two points (fewer than the edges needed to enclose any area), and asserts the surface
remains completely unmodified in both cases, without throwing.

### Floating-Point Tolerance

Every hand-computed coverage value used in most of these tests (`0.25`, `0.5`, `0.75`, and `1.0`
covered fractions) resolves, after compositing through `Surface`'s existing
round-half-away-from-zero byte rounding, to an exact expected byte value with no residual
floating-point error observable at the byte level - so the corresponding assertions in
`PathFillerTests`, `EdgeFlattenerTests`, and `ScanlineRasterizerTests` use exact equality
(`Assert.Equal` on `byte`/`Rgba32`/`Vector2` values), not an epsilon-based comparison. The one
exception is `PathFiller_Fill_QuadraticCurveShape_TotalCoverageMatchesAnalyticBezierBulgeArea`
(see _CanvasNet-Drawing-PathFiller-Antialiasing_ above), which sums fractional coverage across an
entire flattened curved region rather than comparing a single pixel's exact byte value - its
expected total (`16/3`) is compared using xUnit's `Assert.Equal(double, double, int precision)`
overload (the established convention for tolerance-bearing floating-point comparisons elsewhere in
this codebase, for example `SvgArcConverterTests`/`RectTests`), at a precision loose enough to
absorb the curve-flattening polygon's small, tolerance-bounded deviation from the true analytic
curve without masking any genuine coverage-computation defect. No other test in this unit
currently needs a floating-point tolerance; if a future test exercises a coverage fraction that
does not resolve to an exact byte value at the single-pixel level, the appropriate tolerance would
be the same `NearZeroDisplacement` epsilon (`1e-6f`) used internally by `ScanlineRasterizer` to
classify near-horizontal/near-vertical edges, since the analytic accumulation itself is exact
arithmetic up to ordinary floating-point rounding at that scale.

### Acceptance Criteria

A unit test run passes when every scenario above passes without error or unexpected exception.
