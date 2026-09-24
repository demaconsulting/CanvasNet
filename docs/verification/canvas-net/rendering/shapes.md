### Shapes Unit Verification Design

The `Shapes` unit is verified by
`test/DemaConsulting.CanvasNet.Tests/Rendering/ShapesTests.cs`.

### Verification Approach

- **Rect**: `Shapes_FillRect_WithSolidColor_PaintsRegion` and
  `Shapes_StrokeRect_WithSolidStyle_PaintsPixels` cover fill and stroke output;
  `Shapes_FillRect_RespectsCanvasCurrentTransform` verifies the Canvas transform propagates.
- **RoundRect**: `Shapes_FillRoundRect_RadiusClampedAndPaintsPixels` verifies the clamp
  contract and non-empty output.
- **Circle**: `Shapes_FillCircle_ProducesRoundRegion` and `Shapes_StrokeCircle_ProducesRing`
  cover fill and stroke.
- **Validation**: `Shapes_NullCanvas_ThrowsArgumentNullException` covers the null-Canvas
  contract across all helpers.

### Traceability

Every requirement in `docs/reqstream/canvas-net/rendering/shapes.yaml` links to one or more of
these tests.
