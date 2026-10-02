<!-- cspell:ignore Rgba lerp precomputation precomputes unmutated -->

## TilePaint Unit Verification Design

This document describes the unit-level verification strategy for the `TilePaint` unit: the public
`TilePaint` type and the internal `TilePaintEvaluator` helper, plus the tile-paint-specific
scenarios of `PathFiller.Fill` and `ScanlineRasterizer.Fill` that consume them.

### Verification Approach

The unit is verified through a layered mix of narrow constructor/validation tests, direct
algorithmic tests of the internal `TilePaintEvaluator`, and end-to-end rendering tests that fill a
path with a tile paint through the production `PathFiller` pipeline and inspect the resulting
`Surface` pixels:

- **Constructor validation** (`TilePaintTests.cs`) verifies rejection of a null surface, a
  non-finite transform component, and a non-finite/zero `XStep`/`YStep`, plus round-trip
  preservation of valid values (including negative steps) and `WithTransform`'s transform
  composition.
- **Direct evaluator tests** (`TilePaintEvaluatorTests.cs`) call the internal
  `TilePaintEvaluator.CreatePlan`/`EvaluateRow` methods directly (accessible to the test project
  via the existing `InternalsVisibleTo` configuration) to verify identity-transform sampling,
  wraparound beyond one tile repetition, negative-step wraparound, and the degenerate-transform
  fully-transparent policy, independently of the rasterizer.
- **End-to-end rendering tests** (`PathFillerTests.cs`, `ScanlineRasterizerTests.cs`) fill actual
  path geometry with a tile paint through `PathFiller.Fill`/`ScanlineRasterizer.Fill` and assert on
  rendered `Surface` pixels, proving the tile-paint overloads share coverage computation and
  validation with the pre-existing solid-color/gradient overloads rather than merely resembling
  them.

No mocks are required. `TilePaintEvaluator` has no injectable dependencies.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required

### Acceptance Criteria

The unit passes verification when every scenario below passes without unexpected exception and
every expected pixel color, alpha, or thrown exception type matches exactly.

### Test Scenarios

#### TilePaint Construction

- `TilePaint_Constructor_NullSurface_ThrowsArgumentNullException`
- `TilePaint_Constructor_TransformWithNaNComponent_ThrowsArgumentOutOfRangeException`
- `TilePaint_Constructor_TransformWithInfinityComponent_ThrowsArgumentOutOfRangeException`
- `TilePaint_Constructor_ZeroXStep_ThrowsArgumentOutOfRangeException`
- `TilePaint_Constructor_ZeroYStep_ThrowsArgumentOutOfRangeException`
- `TilePaint_Constructor_NonFiniteStep_ThrowsArgumentOutOfRangeException`
- `TilePaint_Constructor_NegativeSteps_Succeeds`
- `TilePaint_Constructor_ValidValues_PropertiesRoundTrip`
- `TilePaint_WithTransform_ComposesTransformPreservingOtherProperties`

These tests verify that the constructor rejects a null surface, a non-finite transform component,
and a non-finite or zero `XStep`/`YStep`, while accepting a negative step value (a legitimate PDF
tiling-pattern value meaning the tile repeats in the negative pattern-space axis direction); that
valid values round-trip exactly through the public properties; and that `WithTransform` returns a
new instance whose `Transform` is the original transform composed with the supplied transform,
leaving `Surface`/`XStep`/`YStep` unchanged and the original instance unmutated.

#### TilePaintEvaluator: Sampling and Wraparound

- `CreatePlan_NullTile_ThrowsArgumentNullException`
- `EvaluateRow_NonInvertibleTransform_WritesFullyTransparentRow`
- `EvaluateRow_IdentityTransform_SamplesExpectedTilePixels`
- `EvaluateRow_WrapAroundBeyondOneTile_MatchesFirstTileRepetition`
- `EvaluateRow_NegativeSteps_SamplesUsingAbsoluteValueWraparound`

These tests verify the core per-row sampling pipeline directly: an identity-transform tile paint
samples the expected tile pixel at each evaluated device-space point; a point more than one tile
repetition away from the origin wraps around (via floor-mod) to match the color sampled at the
equivalent point within the first tile repetition, proving the repeating-pattern behavior; a tile
paint with negative `XStep`/`YStep` still wraps correctly using the absolute value of each step;
and a non-invertible (singular) `Transform` causes `EvaluateRow` to write a fully transparent
(alpha zero) pixel for the entire row - the deliberate "Degenerate Transform" policy divergence
from `GradientEvaluator`'s own flat-fill-with-last-stop-color policy (see `tile-paint.md`'s Key
Methods section for the rationale).

#### PathFiller/ScanlineRasterizer: Tile Paint Fill Shares Behavior with Solid-Color/Gradient Fill

- `PathFiller_Fill_TilePaint_CheckerboardTile_FillsWithBothTileColors`
- `PathFiller_Fill_TilePaint_NullSurface_ThrowsArgumentNullException`
- `PathFiller_Fill_TilePaint_NullPath_ThrowsArgumentNullException`
- `PathFiller_Fill_TilePaint_NullPaint_ThrowsArgumentNullException`
- `PathFiller_Fill_TilePaint_EmptyPath_NoOpLeavesSurfaceUnchanged`
- `ScanlineRasterizer_Fill_TilePaint_FullCoverageRow_RepeatsAcrossWidth`
- `ScanlineRasterizer_Fill_TilePaint_NoPolygons_NoOp`
- `ScanlineRasterizer_Fill_TilePaint_NullPaint_ThrowsArgumentNullException`

These tests prove the tile-paint `Fill` overloads on `PathFiller` and `ScanlineRasterizer` produce
a genuinely repeating tile pattern: filling a region larger than a single tile with a 2-color
checkerboard tile produces both of the tile's own colors in the filled result; the overloads
reject a null surface, path, or tile paint argument; and they honor the same empty-input/
no-polygons no-op behavior already established for the solid-color and gradient overloads.

### Complexity Verification Policy

No algorithmic complexity properties are newly established by this unit beyond those already
documented for `ScanlineRasterizer` (see _PathFiller Unit Design_, `path-filler.md`);
`TilePaintEvaluator` itself performs a fixed, small amount of arithmetic per pixel with no loops
over unbounded input. `TilePaintEvaluator` precomputes the only per-fill-invariant quantity (the
inverted transform) once per fill operation, via `CreatePlan`, rather than once per pixel or once
per row; this is verified functionally, by the identity-transform and wraparound tests producing
exact expected pixel colors across a multi-pixel row, and by the degenerate-transform test
producing a fully transparent row in a single precomputed check rather than per pixel - not by any
timing measurement. There are intentionally **no** timing-based tests, elapsed-time assertions, or
`Stopwatch`-based guards in this unit's automated verification, consistent with every other unit in
this library.
