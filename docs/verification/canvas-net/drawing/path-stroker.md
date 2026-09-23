## PathStroker Unit Verification Design

This document describes the unit-level verification strategy for the `PathStroker` class and the
supporting `StrokeStyle`, `LineCap`, `LineJoin`, `StrokePathFlattener`, `DashSplitter`, and
`StrokeOutliner` types it covers inline.

### Verification Approach

The `PathStroker` unit is verified through a mix of end-to-end rendering assertions and narrower
internal geometry tests:

- **End-to-end stroke rendering** (`PathStrokerTests.cs`) converts an input path to outline
  geometry via `PathStroker.Stroke`, fills that returned path through the production
  `PathFiller.Fill` pipeline, and asserts byte-exact pixel alpha values on the resulting
  `Surface`. This proves the public API's observable behavior, not merely the intermediate outline
  coordinates.
- **Style validation tests** (`StrokeStyleTests.cs`) verify constructor rejection of invalid
  widths, miter limits, and dash arrays, plus round-trip preservation of valid values.
- **Flattening, dash, and outline helper tests** (`StrokePathFlattenerTests.cs`,
  `DashSplitterTests.cs`, and `StrokeOutlinerTests.cs`) exercise the internal contracts that are
  difficult to prove solely from rasterized pixels, such as seam stitching for a dashed closed
  contour or opposite winding for shell rings.

No mocks are required. The helpers are accessible to the test project through the existing
`InternalsVisibleTo` configuration already used elsewhere in the repository.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the unit has no injectable dependencies

### Acceptance Criteria

The unit passes verification when every scenario below passes without unexpected exception and
every expected pixel alpha or geometry assertion matches exactly.

### Test Scenarios

#### Stroke Conversion and Cap Shapes

- `PathStroker_Stroke_HorizontalLineButtCap_FillsExactRectangleNoExtension`
- `PathStroker_Stroke_HorizontalLineRoundCap_FillsRectanglePlusSemicircularEnds`
- `PathStroker_Stroke_HorizontalLineSquareCap_FillsRectanglePlusHalfWidthExtension`

These tests stroke a simple horizontal line and then fill the returned outline geometry. They
verify that butt caps stop exactly at the endpoints, square caps extend by half the width, and
round caps contribute the expected partial-coverage end pixels rather than a rectangular
extension.

#### Join Shapes and Miter Limit

- `PathStroker_Stroke_RightAngleCornerMiterJoin_FillsSharpMiteredCorner`
- `PathStroker_Stroke_RightAngleCornerRoundJoin_FillsRoundedCorner`
- `PathStroker_Stroke_RightAngleCornerBevelJoin_FillsFlatBeveledCorner`
- `PathStroker_Stroke_SharpAngleExceedingMiterLimit_FallsBackToBevel`

These tests use the same right-angle polyline with width 4 so the corner-only pixels differ
clearly between join styles. The miter-limit scenario uses the same path with a deliberately small
limit, proving that the rendered result matches the bevel case rather than the unrestricted miter.

#### Closed Contours, Dashing, and Degenerate Subpaths

- `PathStroker_Stroke_ClosedRectangle_FillsRingLeavingInteriorAndExteriorUnfilled`
- `PathStroker_Stroke_OverlappingLineAndPointCapOutlines_FillsOverlapRegionSolid`
- `PathStroker_Stroke_DashedLine_FillsOnlyOnSegmentsLeavingGapsUnfilled`
- `PathStroker_Stroke_ZeroLengthSubpathRoundCap_FillsCircleOfRadiusHalfWidth`
- `PathStroker_Stroke_ZeroLengthSubpathSquareCap_FillsSquareOfSideWidth`
- `PathStroker_Stroke_ZeroLengthSubpathButtCap_ProducesNoFilledPixels`
- `PathStroker_Stroke_SinglePointSubpath_MatchesZeroLengthSubpathBehavior`
- `PathStroker_Stroke_EmptyPath_ReturnsEmptyPathNoOp`

These scenarios verify the boundary conditions most likely to produce either missing geometry or
wrong winding: closed contours must leave their interior hole unfilled; two independently-emitted
outer outlines (an open-line outline and a point-cap circle) that overlap must render the overlap
region solidly filled rather than as a winding-cancellation hole; dashed lines must paint only
visible runs; degenerate subpaths must follow cap semantics exactly; and an empty path must remain
a no-op.

#### Concave/Collinear Closed Contours

- `PathStroker_Stroke_ClosedRectangleBevelJoin_InnerCornerMatchesExactIntersectionNotBevel`
- `PathStroker_Stroke_ClosedRectangleRoundJoin_InnerCornerMatchesExactIntersectionNotArc`
- `PathStroker_Stroke_ClosedRectangleMiterJoin_InnerCornerMatchesExactIntersection`
- `PathStroker_Stroke_ConcaveClosedContourBevelJoin_AppliesJoinsByLocalVertexConvexity`
- `PathStroker_Stroke_ClosedTwoPointSubpath_FillsStrokedSegmentAreaNotEmpty`
- `PathStroker_Stroke_ClosedThreePointCollinearSubpath_FillsStrokedSegmentAreaNotEmpty`
- `StrokeOutliner_Outline_ConcaveClosedContourBevelJoin_ResolvesJoinPerVertexFromLocalTurn`
- `StrokeOutliner_Outline_ThreePointCollinearClosedContour_ProducesVisibleStroke`
- `StrokeOutliner_Outline_CollinearClosedContourWithDuplicatePoints_ProducesVisibleStroke`

These tests verify that a closed contour's forced-exact-intersection inner corner is used in place
of its styled join (Miter, Round, or Bevel) regardless of which join style is configured; that join
resolution for a concave polygon is decided per-vertex from local convexity/turn direction rather
than by any single global winding decision, so styling stays correct even when convexity flips
along the contour; and that a degenerate closed contour - a 2-point subpath, a 3+ point subpath
whose distinct points are all collinear, or a collinear subpath containing duplicate points - still
renders a visible stroke rather than vanishing to nothing.

#### Robustness Against Extreme and Degenerate Numeric Input

- `PathStroker_Stroke_OverflowProneDashArrayWithNegativeOffset_CompletesWithoutHanging`
- `DashSplitter_Split_OverflowProneDashArrayWithNegativeOffset_CompletesWithoutHanging`
- `DashSplitter_Split_TinyNegativeOffsetAgainstHugePattern_ProducesCorrectPhase`
- `DashSplitter_Split_TinyNegativeOffsetAgainstAsymmetricHugePattern_ProducesCorrectPhase`
- `DashSplitter_Split_ZeroLengthLeadingDashEntryOnZeroLengthPath_StartsInFollowingOffEntry`
- `DashSplitter_Split_EdgeSpanningExtremeFloat32Coordinates_CompletesWithFiniteSegments`
- `DashSplitter_Split_HugeFiniteTotalLengthWithFineDashSpan_FallsBackToSolidStroke`
- `PathStroker_Stroke_HugeFiniteCoordinatesWithFineDashPattern_CompletesWithoutHanging`
- `StrokeOutliner_Outline_RoundJoinAtExtremeScale_ProducesCurvedNotStraightJoin`
- `StrokeOutliner_Outline_RoundCapAtTypicalScale_MatchesExpectedSegmentCount`
- `StrokeOutliner_Outline_RoundJoinSmallSweepAtExtremeScale_ProducesMultiSegmentCurve`
- `StrokeOutliner_Outline_ClosedContourWithExtremeFloat32NonCollinearCoordinates_ProducesValidShellOutline`

These tests prove that dash-offset phase computation, dash extraction, round join/cap
tessellation, and closed-contour collinearity/winding classification all remain correct and
terminate promptly even when intermediate arithmetic would naively overflow or underflow float32 -
covering a huge dash pattern combined with a tiny negative offset (symmetric and asymmetric
patterns), an edge or closed contour spanning near-extreme float32 coordinate magnitudes, a
zero-length leading dash-array entry evaluated against a zero-length path, a huge-but-finite total
path length combined with a fine dash span (where double precision's ULP at that magnitude
otherwise forces the dash-interval traversal loop toward an impractical iteration count, now capped
and falling back to a solid stroke instead of consuming disproportionate CPU time), and a round
join/cap tessellated at extreme geometric scale - rather than hanging, misclassifying the contour
as degenerate, or producing `NaN`/`Infinity` coordinates.

#### Public API Validation

- `PathStroker_Stroke_NullPath_ThrowsArgumentNullException`
- `PathStroker_Stroke_NullStrokeStyle_ThrowsArgumentNullException`
- `PathStroker_Stroke_NonPositiveFlattenTolerance_ThrowsArgumentOutOfRangeException`

These tests prove that the public entry point rejects invalid reference and tolerance arguments
before attempting any geometry conversion.

#### StrokeStyle Constructor Validation

- `StrokeStyle_Constructor_NonPositiveWidth_ThrowsArgumentOutOfRangeException`
- `StrokeStyle_Constructor_MiterLimitBelowOne_ThrowsArgumentOutOfRangeException`
- `StrokeStyle_Constructor_DefaultMiterLimit_IsFour`
- `StrokeStyle_Constructor_NullOrEmptyDashArray_IsTreatedAsSolid`
- `StrokeStyle_Constructor_DashArrayWithNegativeEntry_ThrowsArgumentException`
- `StrokeStyle_Constructor_DashArrayAllZero_ThrowsArgumentException`
- `StrokeStyle_Constructor_ValidValues_PropertiesRoundTrip`

These tests verify that style validation is front-loaded at construction time and that valid
public styling values are preserved exactly.

#### Internal Helper Semantics

- `StrokePathFlattener_Flatten_OpenSubpath_PreservesIsClosedFalse`
- `StrokePathFlattener_Flatten_ClosedSubpath_PreservesIsClosedTrueWithNoImplicitDuplicatePoint`
- `StrokePathFlattener_Flatten_CurveCommands_DelegatesToBezierFlattening`
- `StrokePathFlattener_Flatten_MultipleSubpaths_RemainSeparate`
- `StrokePathFlattener_Flatten_EmptyPath_ReturnsEmptyList`
- `DashSplitter_Split_NullOrEmptyDashArray_ReturnsWholeSegmentUnchanged`
- `DashSplitter_Split_OddLengthDashArray_DuplicatesArrayConceptually`
- `DashSplitter_Split_WithDashOffset_ShiftsPhaseOfFirstSegment`
- `DashSplitter_Split_ClosedPolylineDashWrappingSeam_StitchesSegmentAcrossStartPoint`
- `DashSplitter_Split_PatternLongerThanPolyline_ReturnsSinglePartialOnSegment`
- `DashSplitter_Split_AllZeroDashArray_TreatedAsSolid`
- `DashSplitter_Split_FineDashPatternOnVeryLongPath_CompletesWithCorrectSegments`
- `StrokeOutliner_Outline_ClosedSubpath_ProducesTwoCounterWoundRings`
- `StrokeOutliner_Outline_OpenLineAndPointCapCircle_ShareSameOuterWinding`
- `StrokeOutliner_Outline_ClosedContourReversedSourceWinding_NormalizesOuterRingConsistently`
- `StrokeOutliner_Outline_SegmentSpanningExtremeFloat32Coordinates_ProducesValidNonDegenerateOutline`
- `StrokeOutliner_Outline_MiterJoinWithinLimit_ProducesSharpVertex`
- `StrokeOutliner_Outline_MiterJoinExceedingLimit_FallsBackToBevelVertex`
- `StrokeOutliner_Outline_ClosedSquareHalfWidthExceedsInradius_ProducesNoInvalidHole`
- `StrokeOutliner_Outline_ClosedSquareHalfWidthNearButBelowInradius_ProducesValidHole`

These tests verify the intermediate geometry contracts that feed the public API: preserving
open/closed state, applying SVG-style dash semantics, stitching seam-wrapping visible runs,
normalizing every independently-emitted outer outline (open-line outlines, point-cap circles, and
closed-contour outer rings) to a single consistent winding direction regardless of outline kind or
source authoring order, tolerating segments spanning near-extreme float32 coordinates without
overflowing to a degenerate outline, and producing shell rings with opposite winding for
`FillRule.NonZero`.

### Complexity Verification Policy

Complexity expectations for dash splitting and outline generation are established in
_PathStroker Unit Design_ (`../../../design/canvas-net/drawing/path-stroker.md`) through design
analysis and code review only. There are intentionally **no** timing-based tests, elapsed-time
assertions, or `Stopwatch`-based guards in this unit's automated verification because those are
not stable compliance evidence on heterogeneous CI hardware.
<!-- cspell:ignore Outliner inradius collinearity -->
