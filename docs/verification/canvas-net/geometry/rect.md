## Rect Unit Verification Design

This document describes the unit-level verification strategy for the `Rect` struct.

### Verification Approach

The `Rect` unit is verified through unit tests that exercise its constructor, computed
edge/corner properties, `IsEmpty`, `Contains`, `Union`, `Intersect`, `Transform`, value equality,
and `ToString`, in isolation. Expected values for `Transform` under rotation are hand-computed
independently of the implementation, not by re-deriving the same rotation formula under test.

Unit tests reside in `RectTests.cs` within the `DemaConsulting.CanvasNet.Tests.Geometry` project
namespace.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `Rect` has no injectable dependencies

### Unit-Level Test Scenarios

#### CanvasNet-Geometry-Rect-Construction: Constructor Sets Properties and Computed Edges

**Test**: `Rect_Constructor_ValidValues_SetsPropertiesAndComputedEdges`

Constructs a `Rect` with known `X`, `Y`, `Width`, `Height` and asserts every computed
property (`Left`, `Top`, `Right`, `Bottom`, `Location`, `Size`, `TopLeft`, `BottomRight`) equals
its hand-computed expected value.

#### CanvasNet-Geometry-Rect-Empty: Union With Empty Is an Identity

**Test**: `Rect_Union_WithEmpty_ReturnsOtherRectangleUnchanged`

Unions `Rect.Empty` with a real rectangle (in both argument orders) and asserts the result
exactly equals the real rectangle unchanged, confirming `Empty` is a true union identity.

#### CanvasNet-Geometry-Rect-IsEmpty: IsEmpty Distinguishes Empty From Real Rectangles

**Test**: `Rect_IsEmpty_EmptySentinelVersusRealRectangle_ReturnsExpectedValue`

Asserts `Rect.Empty.IsEmpty` is `true`, and that a real rectangle (including one with a zero
width or height, but not negative) reports `IsEmpty` as `false`.

#### CanvasNet-Geometry-Rect-Contains: Contains Uses a Half-Open Interval

**Test**: `Rect_Contains_BoundaryPoints_ReturnsExpectedValue`

Tests points at each corner and edge of a rectangle and asserts the half-open containment
convention: left/top edges included, right/bottom edges excluded.

#### CanvasNet-Geometry-Rect-Union: Union of Two Overlapping Rectangles

**Test**: `Rect_Union_TwoOverlappingRectangles_ReturnsSmallestEnclosingRectangle`

Unions two overlapping rectangles (via both the instance and static overloads) and asserts the
result exactly equals the hand-computed smallest enclosing rectangle.

#### CanvasNet-Geometry-Rect-Intersect: Intersect of Overlapping and Disjoint Rectangles

**Tests**: `Rect_Intersect_TwoOverlappingRectangles_ReturnsOverlapRegion`,
`Rect_Intersect_DisjointRectangles_ReturnsEmpty`,
`Rect_Intersect_RectanglesTouchingAlongVerticalEdge_ReturnsEmpty`,
`Rect_Intersect_RectanglesTouchingAlongHorizontalEdge_ReturnsEmpty`

Intersects two overlapping rectangles and asserts the result exactly equals the hand-computed
overlap region. Separately, intersects two disjoint rectangles and asserts the result exactly
equals `Rect.Empty`. Two further regression tests intersect rectangles that merely touch along a
vertical or horizontal edge (zero-width/zero-height overlap) and assert `Empty` is returned rather
than a non-`Empty` zero-extent `Rect`, since edge-only contact contains no points.

#### CanvasNet-Geometry-Rect-Transform: Transform Considers All Four Corners

**Tests**: `Rect_Transform_Translation_OffsetsPosition`,
`Rect_Transform_Rotation90DegreesAboutOrigin_ReturnsHandComputedAabb`

Transforms a rectangle by a pure translation matrix and asserts the result is offset by exactly
that translation with unchanged size. Separately, transforms a rectangle by a 90-degree rotation
matrix about the origin and asserts the result exactly equals the hand-computed axis-aligned
bounding box of the four rotated corners - a scenario that would fail under a naive
two-corner-only transform implementation.

#### CanvasNet-Geometry-Rect-Equality: Value Equality and ToString

**Tests**: `Rect_Equals_SameAndDifferentValues_ReturnsExpectedResult`,
`Rect_ToString_ReturnsStringContainingFieldValues`

Compares pairs of `Rect` values with identical and with differing field values via `Equals`,
`GetHashCode`, `==`, and `!=`, asserting the expected result for each. Separately, calls
`ToString()` on a `Rect` with known field values and asserts the returned string contains each
field's value.

### Acceptance Criteria

A unit test run passes when every scenario above passes without error or unexpected exception.
