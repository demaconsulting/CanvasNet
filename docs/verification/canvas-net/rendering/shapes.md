### Shapes Unit Verification Design

The `Shapes` unit is verified by
`test/DemaConsulting.CanvasNet.Tests/Rendering/ShapesTests.cs`.

### Verification Approach

- **Rect**: `Shapes_FillRect_WithSolidColor_PaintsRegion` and
  `Shapes_StrokeRect_WithSolidStyle_PaintsPixels` cover fill and stroke output;
  `Shapes_FillRect_RespectsCanvasCurrentTransform` verifies the Canvas transform propagates.
- **RoundRect**: `Shapes_FillRoundRect_RadiusClampedAndPaintsPixels` verifies the clamp
  contract and non-empty output for fill;
  `Shapes_StrokeRoundRect_WithSolidStyle_PaintsPixels` verifies the corresponding stroke output,
  and `Shapes_StrokeRoundRect_RadiusLargerThanHalfShorterSide_ClampsAndPaintsPixels` verifies
  the clamped-radius code path when the requested radius exceeds half of the shorter side.
- **Circle**: `Shapes_FillCircle_ProducesRoundRegion` and `Shapes_StrokeCircle_ProducesRing`
  cover fill and stroke.
- **Validation**: `Shapes_NullCanvas_ThrowsArgumentNullException` covers the null-Canvas
  contract across all helpers.

### Traceability

Every requirement in `docs/reqstream/canvas-net/rendering/shapes.yaml` links to one or more of
these tests.
