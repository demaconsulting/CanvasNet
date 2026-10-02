### CornerRoundEffect Unit Verification Design

The `CornerRoundEffect` unit is verified by
`test/DemaConsulting.CanvasNet.Tests/Geometry/CornerRoundEffectTests.cs`.

### Verification Approach

- **Rounding polyline corners**: `CornerRoundEffect_Apply_SquareRectangle_ProducesCornerArcs`
  verifies polyline corners are replaced by tangent-radius arcs.
- **Wrap-around and closing corners**:
  `CornerRoundEffect_Apply_ClosedSquare_RoundsAllFourCornersIncludingWrapAroundCorner` verifies
  all four corners of a closed square are rounded — including the wrap-around corner at the
  subpath's start point and the corner at the last real vertex before the closing edge — by
  asserting exactly four `CubicBezierTo` commands are produced and that no `LineTo` command
  terminates exactly at any of the four original sharp corner points.
- **Curved-corner policy**: `CornerRoundEffect_Apply_LeavesCurvedCornersUnchanged` verifies
  that curved segments are left as-is.
  `CornerRoundEffect_Apply_CubicThenLineThenLine_LeavesCurveAdjacentVertexUnroundedButRoundsLineLineCorner`
  and `CornerRoundEffect_Apply_CubicThenLineThenClose_LeavesCurveAdjacentVertexUnrounded` extend
  this to a `CubicBezierTo -> LineTo -> LineTo` (and `-> Close`) sequence, confirming the
  curve-adjacent vertex stays sharp while a genuine line-to-line-to-line corner elsewhere in the
  same path is still rounded.
- **Radius clamping**: `CornerRoundEffect_Apply_ClampsRadiusToHalfShorterAdjacentSegment`
  verifies the per-corner clamp to half the shorter adjacent segment.
- **Zero radius**: `CornerRoundEffect_Apply_ZeroRadius_ReturnsSourcePath` verifies the no-op
  fast path.
- **Validation**: `CornerRoundEffect_Apply_NullSource_ThrowsArgumentNullException`,
  `CornerRoundEffect_Apply_NegativeRadius_ThrowsArgumentOutOfRangeException`, and
  `CornerRoundEffect_Apply_NonFiniteRadius_ThrowsArgumentOutOfRangeException` cover the
  validation contract.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; every test builds a `Path` through the public `PathBuilder` API
- **Isolation**: Each test method constructs its own `Path` fixture, with no shared state between
  tests

### Acceptance Criteria

A unit test run passes when every scenario below passes without error or unexpected exception,
and when every named test method listed for each requirement ID passes across every target
framework.

### Test Scenarios

#### CanvasNet-Geometry-CornerRoundEffect-Apply: Polyline Corners Are Replaced by Tangent-Radius Arcs

**Tests**: `CornerRoundEffect_Apply_SquareRectangle_ProducesCornerArcs`,
`CornerRoundEffect_Apply_ClosedSquare_RoundsAllFourCornersIncludingWrapAroundCorner`,
`CornerRoundEffect_Apply_ClampsRadiusToHalfShorterAdjacentSegment`,
`CornerRoundEffect_Apply_Size10SquareRadius5_AllFourCornersGetSameUnclampedRadius`

Verifies every polyline corner (a `LineTo` followed by a `LineTo`, including, for a closed
subpath, the wrap-around corner at the subpath's start point and the corner at the last real
vertex before the closing edge) is replaced by a tangent-radius arc, clamped per-corner to half
of the shorter adjacent segment, and that an unclamped radius applies identically to all four
corners of a square.

#### CanvasNet-Geometry-CornerRoundEffect-ZeroRadius: A Zero Radius Returns the Source Path Unchanged

**Test**: `CornerRoundEffect_Apply_ZeroRadius_ReturnsSourcePath`

Verifies the no-op fast path: a zero radius returns the exact source `Path` instance rather than
allocating a redundant copy.

#### CanvasNet-Geometry-CornerRoundEffect-CurvedCorners: Curved-Segment Corners Are Left Unchanged

**Tests**: `CornerRoundEffect_Apply_LeavesCurvedCornersUnchanged`,
`CornerRoundEffect_Apply_CubicThenLineThenLine_LeavesCurveAdjacentVertexUnroundedButRoundsLineLineCorner`,
`CornerRoundEffect_Apply_CubicThenLineThenClose_LeavesCurveAdjacentVertexUnrounded`

Verifies that corners involving a curved segment (cubic or quadratic Bezier, or an arc) are left
as-is, including a `CubicBezierTo -> LineTo -> LineTo` (and `-> Close`) sequence, confirming the
curve-adjacent vertex stays sharp while a genuine line-to-line-to-line corner elsewhere in the
same path is still rounded.

#### CanvasNet-Geometry-CornerRoundEffect-Validation: Invalid Arguments Are Rejected

**Tests**: `CornerRoundEffect_Apply_NullSource_ThrowsArgumentNullException`,
`CornerRoundEffect_Apply_NegativeRadius_ThrowsArgumentOutOfRangeException`,
`CornerRoundEffect_Apply_NonFiniteRadius_ThrowsArgumentOutOfRangeException`

Verifies a `null` source path is rejected with `ArgumentNullException`, and a negative or
non-finite radius is rejected with `ArgumentOutOfRangeException`.

### Traceability

Every requirement in `docs/reqstream/canvas-net/geometry/corner-round-effect.yaml` links to
one or more of these tests.
