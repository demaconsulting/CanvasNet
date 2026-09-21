## BezierFlattening Unit Verification Design

This document describes the unit-level verification strategy for the `BezierFlattening` class.

### Verification Approach

The `BezierFlattening` unit is verified through unit tests exercising the never-write-start /
always-write-end-last chaining convention, a tolerance-convergence property test (densely
sampling the true curve and checking every flattened point lies within a small multiple of the
requested tolerance), a monotonic segment-count property test, degenerate-control-point
termination, and the `ArgumentOutOfRangeException` guard - for both `FlattenCubic` and
`FlattenQuadratic`.

The tolerance-convergence tests are property-based rather than golden-value-based: rather than
asserting an exact, pre-computed set of output points (which would be brittle against
implementation-internal subdivision choices, such as exactly how many segments a given tolerance
produces), each test evaluates the true curve at 1000 densely spaced parameter values via an
independent cubic/quadratic Bezier evaluation formula, and asserts every one of those true-curve
points lies within a small multiple of `tolerance` of the nearest point on the flattened
polyline. This verifies the actual contract (the flattened polyline hugs the true curve within
tolerance) without over-specifying the exact flattening implementation.

A separate, deliberately distinct test verifies the `MaxRecursionDepth` safety valve itself: it
supplies a pathological curve and an extremely tight tolerance chosen so the flatness test can
never be satisfied at any practical depth, and asserts only that the call terminates with a
bounded output point count - it does not assert (and must not assert) that the result satisfies
the tolerance, because the recursion-depth guard is documented as a termination/resource
guarantee, distinct from the tolerance guarantee, following the well-established
`curve_recursion_limit` convention from Anti-Grain Geometry (AGG).

Unit tests reside in `BezierFlatteningTests.cs` within the
`DemaConsulting.CanvasNet.Tests.Geometry` project namespace.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `BezierFlattening` has no injectable dependencies

### Unit-Level Test Scenarios

#### CanvasNet-Geometry-BezierFlattening-FlattenCubic / CanvasNet-Geometry-BezierFlattening-FlattenQuadratic: Chaining Convention

**Tests**: `BezierFlattening_FlattenCubic_SimpleCurve_NeverWritesStartAndAlwaysWritesEndLast`,
`BezierFlattening_FlattenQuadratic_SimpleCurve_NeverWritesStartAndAlwaysWritesEndLast`

Flattens a simple, non-degenerate cubic (and, separately, quadratic) curve and asserts the
output list never contains the curve's start point and its final element exactly equals the
curve's end point.

#### CanvasNet-Geometry-BezierFlattening-ToleranceConvergence: Flattened Points Stay Within Tolerance

**Tests**: `BezierFlattening_FlattenCubic_VariousTolerances_SampledCurvePointsWithinTolerance`,
`BezierFlattening_FlattenQuadratic_VariousTolerances_SampledCurvePointsWithinTolerance`,
`BezierFlattening_FlattenCubic_ControlPointProjectsBeyondChordEnd_StaysWithinTolerance`

For a table of tolerances, flattens a representative curved cubic (and, separately, quadratic)
Bezier curve, densely samples the true curve at 1000 points via an independently implemented
evaluation formula, and asserts the distance from every sampled point to the nearest point on the
flattened polyline is within a small multiple of the requested tolerance. A further regression
test uses a cubic curve whose control point's projection onto the endpoint chord falls beyond the
chord's end - so its distance to the infinite line through the chord is small, but its distance to
the finite chord segment is not - and asserts the flattener still subdivides and stays within
tolerance, guarding against a bug where the flatness test measured distance to the infinite line
rather than the finite chord segment.

#### CanvasNet-Geometry-BezierFlattening-MonotonicSegmentCount: Segment Count Is Monotonic

**Test**: `BezierFlattening_FlattenCubic_IncreasingTolerance_SegmentCountIsMonotonicallyNonIncreasing`

Flattens the same cubic curve at a strictly increasing sequence of tolerances and asserts the
resulting output point count never increases as tolerance increases.

#### CanvasNet-Geometry-BezierFlattening-DegenerateControlPoints: Degenerate Curves Terminate

**Tests**: `BezierFlattening_FlattenCubic_CoincidentControlPoints_TerminatesWithSinglePoint`,
`BezierFlattening_FlattenCubic_CollinearControlPoints_TerminatesWithCollinearPoints`

Flattens a cubic curve whose control points are coincident with an endpoint, and separately, a
cubic curve whose control points are collinear with the chord between its endpoints, and asserts
both terminate promptly (without throwing or hanging) with a geometrically sensible result.

#### CanvasNet-Geometry-BezierFlattening-RecursionDepthSafetyValve: Pathological Input Still Terminates

**Test**: `BezierFlattening_FlattenCubic_PathologicalNonConvergingCurve_TerminatesWithBoundedOutput`

Flattens a curved cubic at an extremely tight tolerance (`1e-10`) that a curve of this scale can
never satisfy under float32 precision at any practical recursion depth, forcing the
`MaxRecursionDepth` safety valve to be exercised, and asserts the call still terminates promptly
with a bounded output point count (at most `2^MaxRecursionDepth`) ending at the curve's declared
end point. This test proves the documented safety-valve behavior itself (bounded termination) -
it deliberately does not assert that the tolerance is met, since `MaxRecursionDepth`'s remarks
document that the tolerance guarantee does not extend to input that hits this limit.

#### CanvasNet-Geometry-BezierFlattening-NonPositiveTolerance: Non-Positive Tolerance Is Rejected

**Test**: `BezierFlattening_Flatten_NonPositiveTolerance_ThrowsArgumentOutOfRangeException`

Calls both `FlattenCubic` and `FlattenQuadratic` with a zero and a negative tolerance and asserts
`ArgumentOutOfRangeException` is thrown in every case.

### Acceptance Criteria

A unit test run passes when every scenario above passes without error or unexpected exception.
