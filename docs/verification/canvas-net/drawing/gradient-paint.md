<!-- cspell:ignore Rgba lerp precomputation precomputes -->

## GradientPaint Unit Verification Design

This document describes the unit-level verification strategy for the `GradientPaint` unit: the
public `Gradient`/`LinearGradient`/`RadialGradient`/`GradientStop`/`GradientSpread` types and the
internal `GradientEvaluator` helper, plus the gradient-specific scenarios of `PathFiller.Fill`,
`ScanlineRasterizer.Fill`, and `Surface.CompositeOverSpan` that consume them.

### Verification Approach

The unit is verified through a layered mix of narrow constructor/validation tests, direct
algorithmic tests of the internal `GradientEvaluator`, and end-to-end rendering tests that fill a
path with a gradient through the production `PathFiller` pipeline and inspect the resulting
`Surface` pixels:

- **Stop and gradient constructor validation** (`GradientStopTests.cs`, `LinearGradientTests.cs`,
  `RadialGradientTests.cs`) verify rejection of invalid offsets, empty/null stops, undefined
  spread values, non-finite transforms, non-finite points, and negative/non-finite radii, plus
  round-trip preservation of valid values and defensive stable-sorting of out-of-order stops.
- **Direct evaluator tests** (`GradientEvaluatorTests.cs`) call the internal
  `GradientEvaluator.EvaluatePoint`/`EvaluateRow` methods directly (accessible to the test project
  via the existing `InternalsVisibleTo` configuration) to verify the linear/radial parameter math,
  every spread mode, hard-stop resolution, premultiplied-alpha interpolation, every degenerate
  case, and double-precision extreme-coordinate robustness, independently of the rasterizer.
- **End-to-end rendering tests** (`PathFillerTests.cs`, `ScanlineRasterizerTests.cs`) fill actual
  path geometry with a gradient through `PathFiller.Fill`/`ScanlineRasterizer.Fill` and assert on
  rendered `Surface` pixels, proving the gradient overloads share coverage computation and
  validation with the pre-existing solid-color overloads rather than merely resembling them.
- **Surface composite equivalence** (`SurfaceTests.cs`) verifies the per-pixel-color
  `Surface.CompositeOverSpan` overload added to support gradient rendering produces
  byte-for-byte identical output to the pre-existing constant-color overload whenever every color
  in the per-pixel span happens to be equal, and that its internal workspace-reusing counterpart
  matches its public form.

No mocks are required. `GradientEvaluator` has no injectable dependencies.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required

### Acceptance Criteria

The unit passes verification when every scenario below passes without unexpected exception and
every expected pixel color, alpha, or thrown exception type matches exactly.

### Test Scenarios

#### Gradient Stop and Gradient/Subtype Construction

- `GradientStop_Constructor_OffsetOutsideZeroToOne_ThrowsArgumentOutOfRangeException`
- `GradientStop_Constructor_NonFiniteOffset_ThrowsArgumentOutOfRangeException`
- `GradientStop_Constructor_BoundaryOffsetsZeroAndOne_Succeeds`
- `Gradient_Constructor_IsPrivateProtected`
- `LinearGradient_Constructor_NullStops_ThrowsArgumentNullException`
- `LinearGradient_Constructor_EmptyStops_ThrowsArgumentException`
- `LinearGradient_Constructor_UndefinedSpread_ThrowsArgumentOutOfRangeException`
- `LinearGradient_Constructor_TransformWithNaNComponent_ThrowsArgumentOutOfRangeException`
- `LinearGradient_Constructor_TransformWithInfinityComponent_ThrowsArgumentOutOfRangeException`
- `LinearGradient_Constructor_DefaultTransform_IsIdentity`
- `LinearGradient_Constructor_ExplicitAllZeroTransform_IsPreservedNotReplacedWithIdentity`
- `LinearGradient_Constructor_ValidValues_PropertiesRoundTrip`
- `LinearGradient_Constructor_UnsortedStops_AreSortedAscendingByOffset`
- `LinearGradient_Constructor_DuplicateOffsetStops_PreservesInputOrderAmongTies`
- `LinearGradient_Constructor_StartEqualsEnd_Succeeds`
- `LinearGradient_Constructor_NonFiniteStart_ThrowsArgumentOutOfRangeException`
- `LinearGradient_Constructor_NonFiniteEnd_ThrowsArgumentOutOfRangeException`
- `RadialGradient_Constructor_ValidValues_PropertiesRoundTrip`
- `RadialGradient_Constructor_SingleCircleEquivalentConfiguration_Succeeds`
- `RadialGradient_Constructor_NegativeStartRadius_ThrowsArgumentOutOfRangeException`
- `RadialGradient_Constructor_NegativeEndRadius_ThrowsArgumentOutOfRangeException`
- `RadialGradient_Constructor_NonFiniteStartRadius_ThrowsArgumentOutOfRangeException`
- `RadialGradient_Constructor_NonFiniteStartCenter_ThrowsArgumentOutOfRangeException`
- `RadialGradient_Constructor_NonFiniteEndCenter_ThrowsArgumentOutOfRangeException`
- `RadialGradient_Constructor_EmptyStops_ThrowsArgumentException`
- `RadialGradient_Constructor_BothRadiiZeroCoincidentCenters_Succeeds`
- `RadialGradient_Constructor_DefaultTransform_IsIdentity`
- `RadialGradient_Constructor_ExplicitAllZeroTransform_IsPreservedNotReplacedWithIdentity`

These tests verify that every public gradient type's constructor validates its own arguments
(non-finite/negative/undefined values are rejected), that valid values round-trip exactly, that
stops are defensively stable-sorted ascending by offset (preserving caller-supplied relative order
among ties), that geometric configurations later resolved as evaluation-time degenerate cases
(a zero-length linear vector, a single-circle-equivalent radial configuration, both radii zero
with coincident centers) are still accepted at construction rather than rejected, that
`Gradient`'s shared constructor is `private protected` (a closed hierarchy - only `LinearGradient`
and `RadialGradient`, both declared in this assembly, may derive from it), and that an omitted
transform argument resolves to the identity transform while an explicitly-supplied transform -
including the all-zero matrix - is preserved exactly as given.

#### GradientEvaluator: Linear and Radial Parameter Math

- `EvaluatePoint_NullGradient_ThrowsArgumentNullException`
- `EvaluatePoint_SingleStop_ResolvesToThatColorEverywhere`
- `EvaluatePoint_Linear_EndpointsMatchFirstAndLastStopColors`
- `EvaluatePoint_Radial_SingleCircleEquivalent_CenterAndEdgeMatchEndpointColors`
- `EvaluatePoint_Radial_TwoCircle_DistinctCentersAndRadii_ResolvesAlongSweptFamily`
- `EvaluatePoint_Radial_TwoCircle_PointOnStartCircleWithSpuriousSecondRoot_ResolvesToFirstStopColor`
- `EvaluatePoint_Radial_TwoCircle_PointOnEndCircleWithSpuriousSecondRoot_ResolvesToLastStopColor`
- `EvaluateRow_MultiPixelRun_MatchesPerPixelEvaluatePoint`
- `EvaluateRow_Radial_MultiPixelRun_MatchesPerPixelEvaluatePoint`
- `EvaluateRow_SingularTransform_MatchesPerPixelEvaluatePoint`
- `EvaluateRow_NullGradient_ThrowsArgumentNullException`

These tests verify the core per-point/per-row evaluation math directly: a single-stop gradient
resolves to that one color everywhere; a linear gradient reproduces its first/last stop's color
exactly at its `Start`/`End` points; a radial gradient (both the single-circle-equivalent
configuration and a genuinely distinct-center/distinct-radius two-circle configuration) resolves
the first stop's color on the start circle and the last stop's color on the end circle; and
`EvaluateRow` matches independent per-pixel `EvaluatePoint` calls across a multi-pixel run, for
both a linear gradient, a radial gradient wide enough to cross both circles (proving the
once-per-row precomputation of the matrix inverse and two-circle coefficients does not change
per-pixel output), and a singular-transform gradient (proving the whole-row degenerate-transform
fast path matches the per-pixel path exactly). The two "spurious second root" tests reproduce the
exact intersecting-circle scenario reported against the previous "always pick the larger root"
logic (start circle `(0,0)` radius `5`, end circle `(20,0)` radius `6`, point `(0,5)`, and its
radius-shrinking mirror image) and assert the point resolves to the correct boundary stop color
rather than a spurious interpolated color.

#### GradientEvaluator: Spread Modes

- `EvaluatePoint_Linear_PadSpread_ClampsBeyondEndpoints`
- `EvaluatePoint_Linear_RepeatSpread_WrapsNegativeAndPositiveOutOfRange`
- `EvaluatePoint_Linear_ReflectSpread_FoldsIntoTriangleWave`

These tests verify each of the three spread modes independently: `Pad` clamps an out-of-range raw
parameter to the nearest endpoint color; `Repeat` floor-mods a raw parameter (including a negative
one) back into `[0, 1]`; and `Reflect` folds a raw parameter into a period-2 triangle wave, so a
raw parameter of `1.5` resolves identically to `0.5`.

#### GradientEvaluator: Stop Ordering and Hard Stops

- `EvaluatePoint_Linear_UnsortedInputStops_ResolvesInSortedOrder`
- `EvaluatePoint_Linear_DuplicateStopOffsets_ProducesHardStepWithoutError`
- `EvaluatePoint_Linear_InteriorDuplicateStopOffsets_ExactTieResolvesToLaterSuppliedStop`
- `EvaluatePoint_Linear_DuplicateStopOffsetsAtGradientStart_ExactTieResolvesToLaterSuppliedStop`

These tests verify that out-of-order input stops still resolve correctly (proving the
constructor's defensive sort is what makes evaluation correct), and that two or more stops sharing
the same offset produce a sharp, well-defined color step rather than a division error or
inconsistent result. The last two tests verify the exact tie-break direction: evaluating precisely
at a shared offset always resolves to the later-supplied stop's color, both for an interior
duplicate pair and for a duplicate pair at the gradient's very first offset.

#### GradientEvaluator: Premultiplied-Alpha Interpolation

- `EvaluatePoint_Linear_TransparentAndOpaqueStops_InterpolatesInPremultipliedAlphaSpace`
- `EvaluatePoint_Linear_BothStopsFullyTransparent_ResolvesToAllZeroPixel`

The first test interpolates between an opaque red stop and a fully transparent white stop and
asserts the midpoint's color channels remain saturated red (not a washed-out pink/gray blend) at
half alpha, proving premultiplied-space interpolation. The second test interpolates between two
fully transparent stops with different RGB values and asserts the result is exactly
`(0, 0, 0, 0)`, matching `Surface.UnpremultiplyAlpha`'s documented "alpha == 0 implies R = G = B =
0" convention rather than propagating a divide-by-zero artifact.

#### GradientEvaluator: Degenerate-Case Policy

- `Constructor_NonFiniteTransform_ThrowsArgumentOutOfRangeException`
- `EvaluatePoint_SingularTransform_FlatFillsWithLastStopColor`
- `EvaluatePoint_ExplicitAllZeroTransform_FlatFillsWithLastStopColor`
- `EvaluatePoint_Linear_ZeroLengthVector_FlatFillsWithLastStopColor`
- `EvaluatePoint_Radial_BothRadiiZeroCoincidentCenters_FlatFillsWithLastStopColor`
- `EvaluatePoint_Radial_EqualNonZeroRadiiCoincidentCenters_FlatFillsWithLastStopColor`
- `EvaluatePoint_Radial_TwoCircle_NoValidRootRegion_IsFullyTransparent`

These tests verify the unifying degenerate-case policy directly: a non-finite transform component
is rejected at construction (not tolerated at evaluation time); a singular (non-invertible but
finite) transform - including the explicitly-supplied all-zero matrix, now distinguishable from an
omitted transform argument - a zero-length linear vector, and a radial gradient whose two circles
never change with `t` at all (coincident centers with equal radii, whether that shared radius is
zero or nonzero) all flat-fill with the last stop's color; and, distinctly, a two-circle radial
gradient's genuine "no valid root" region (a point outside every circle the gradient's family ever
sweeps through) resolves to fully transparent (alpha zero) rather than being flat-filled - proving
these two superficially similar "nothing to paint" outcomes are correctly distinguished.

#### GradientEvaluator: Extreme-Coordinate Robustness

- `EvaluatePoint_Linear_ExtremeMagnitudeCoordinates_ResolvesCorrectly`

This test constructs a linear gradient whose `Start`/`End` points are near the edges of `float`'s
representable magnitude and asserts the correct endpoint/spread-folded colors are still resolved,
proving the evaluator's internal `double`-precision projection math avoids the overflow a naive
`float` computation could suffer even though every individual coordinate and the true
mathematical result remain well within `float`'s range.

#### PathFiller/ScanlineRasterizer: Gradient Fill Shares Behavior with Solid-Color Fill

- `PathFiller_Fill_Gradient_HorizontalRectangleWithHorizontalLinearGradient_VariesLeftToRight`
- `PathFiller_Fill_Gradient_SingleStop_MatchesSolidColorFill`
- `PathFiller_Fill_Gradient_OverlappingSameWoundRectangles_NonZeroVsEvenOddDiverge`
- `PathFiller_Fill_Gradient_EmptyPath_NoOpLeavesSurfaceUnchanged`
- `ScanlineRasterizer_Fill_Gradient_SingleStop_MatchesSolidColorOverloadPerPixelCoverage`
- `ScanlineRasterizer_Fill_Gradient_FullCoverageRow_VariesAcrossWidthPerGradient`
- `ScanlineRasterizer_Fill_Gradient_NoPolygons_NoOp`

These tests prove the gradient `Fill` overloads on `PathFiller` and `ScanlineRasterizer` produce
genuinely varying per-pixel color for a multi-stop gradient, produce byte-for-byte identical
output to the solid-color overload for a single-stop gradient (including at the coverage-math
level using a sub-pixel-offset shape, proving coverage computation is genuinely shared rather than
duplicated), and honor the same `FillRule` divergence and empty-input no-op behavior already
established for solid-color fills.

#### PathFiller: Gradient Fill Argument Validation

- `PathFiller_Fill_Gradient_NullSurface_ThrowsArgumentNullException`
- `PathFiller_Fill_Gradient_NullPath_ThrowsArgumentNullException`
- `PathFiller_Fill_Gradient_NullPaint_ThrowsArgumentNullException`
- `PathFiller_Fill_Gradient_NonPositiveFlattenTolerance_ThrowsArgumentOutOfRangeException`
- `PathFiller_Fill_Gradient_UndefinedFillRule_ThrowsArgumentOutOfRangeException`

These tests verify the gradient overload rejects a null surface, path, or gradient paint
argument, and a non-positive/non-finite `flattenTolerance` or undefined `fillRule` argument,
matching the solid-color overload's own validation exactly.

#### Surface: Per-Pixel-Color CompositeOverSpan Overload

- `Surface_CompositeOverSpan_PerPixelColors_MatchesConstantColorOverload_WhenAllColorsEqual`
- `Surface_CompositeOverSpan_PerPixelColors_FullCoverage_AppliesEachPixelsOwnColor`
- `Surface_CompositeOverSpan_PerPixelColors_ZeroCoverage_LeavesBackgroundUnchanged`
- `Surface_CompositeOverSpan_PerPixelColors_LengthMismatch_ThrowsArgumentException`
- `Surface_CompositeOverSpanWithWorkspace_PerPixelColors_MatchesPublicOverload`

These tests verify the per-pixel-color `CompositeOverSpan` overload added to support gradient
rendering: it matches the pre-existing constant-color overload byte-for-byte whenever every color
in the span happens to be equal (an equivalence proof that the shared blend math introduced by
this unit's `Surface` refactor is genuinely unchanged for the pre-existing overload, per _Surface
Unit Design_, `../canvas/surface.md`); it applies each pixel's own color scaled by that pixel's
own coverage; it rejects a coverage/colors length mismatch; and its internal workspace-reusing
counterpart matches its public form exactly.

### Complexity Verification Policy

No algorithmic complexity properties are newly established by this unit beyond those already
documented for `ScanlineRasterizer`/`Surface.CompositeOverSpan` (see _PathFiller Unit Design_,
`path-filler.md`, and _Surface Unit Design_, `../canvas/surface.md`); `GradientEvaluator` itself
performs a fixed, small amount of arithmetic per point/pixel with no loops over unbounded input.
`GradientEvaluator` precomputes every per-fill-invariant quantity (the inverted transform, and the
linear/radial gradient's own geometric coefficients) once per `EvaluatePoint`/`EvaluateRow` call
rather than once per pixel; this is verified functionally, by asserting `EvaluateRow` produces
pixel-for-pixel identical output to independent `EvaluatePoint` calls both for a plain gradient and
for one requiring the whole-row degenerate-transform fast path (see
`EvaluateRow_Radial_MultiPixelRun_MatchesPerPixelEvaluatePoint` and
`EvaluateRow_SingularTransform_MatchesPerPixelEvaluatePoint` above), not by any timing measurement.
There are intentionally **no** timing-based tests, elapsed-time assertions, or `Stopwatch`-based
guards in this unit's automated verification, consistent with every other unit in this library.
