### CornerRoundEffect Unit Verification Design

The `CornerRoundEffect` unit is verified by
`test/DemaConsulting.CanvasNet.Tests/Geometry/CornerRoundEffectTests.cs`.

### Verification Approach

- **Rounding polyline corners**: `CornerRoundEffect_Apply_SquareRectangle_ProducesCornerArcs`
  verifies polyline corners are replaced by tangent-radius arcs.
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
