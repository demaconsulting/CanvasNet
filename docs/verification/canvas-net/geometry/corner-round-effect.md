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
- **Radius clamping**: `CornerRoundEffect_Apply_ClampsRadiusToHalfShorterAdjacentSegment`
  verifies the per-corner clamp to half the shorter adjacent segment.
- **Zero radius**: `CornerRoundEffect_Apply_ZeroRadius_ReturnsSourcePath` verifies the no-op
  fast path.
- **Validation**: `CornerRoundEffect_Apply_NullSource_ThrowsArgumentNullException`,
  `CornerRoundEffect_Apply_NegativeRadius_ThrowsArgumentOutOfRangeException`, and
  `CornerRoundEffect_Apply_NonFiniteRadius_ThrowsArgumentOutOfRangeException` cover the
  validation contract.

### Traceability

Every requirement in `docs/reqstream/canvas-net/geometry/corner-round-effect.yaml` links to
one or more of these tests.
