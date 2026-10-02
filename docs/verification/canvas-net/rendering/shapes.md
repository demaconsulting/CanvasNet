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

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; every test uses only in-process `Canvas`/`Surface` public APIs
- **Isolation**: Each test constructs its own `Surface`/`Canvas` fixture, with no shared state
  between tests

### Acceptance Criteria

A unit test run passes when every scenario below passes without error or unexpected exception,
and when every named test method listed for each requirement ID passes across every target
framework.

### Test Scenarios

#### CanvasNet-Rendering-Shapes-Rect: FillRect/StrokeRect Draw an Axis-Aligned Rectangle

**Tests**: `Shapes_FillRect_WithSolidColor_PaintsRegion`,
`Shapes_StrokeRect_WithSolidStyle_PaintsPixels`, `Shapes_FillRect_RespectsCanvasCurrentTransform`

Verifies `FillRect` and `StrokeRect` paint the expected rectangular region using the `Canvas`
current transform.

#### CanvasNet-Rendering-Shapes-RoundRect: FillRoundRect/StrokeRoundRect Draw a Rounded Rectangle With a Clamped Radius

**Tests**: `Shapes_FillRoundRect_RadiusClampedAndPaintsPixels`,
`Shapes_StrokeRoundRect_WithSolidStyle_PaintsPixels`,
`Shapes_StrokeRoundRect_RadiusLargerThanHalfShorterSide_ClampsAndPaintsPixels`

Verifies `FillRoundRect` and `StrokeRoundRect` paint non-empty output and clamp the requested
corner radius to half of the shorter side whenever the requested radius exceeds that bound.

#### CanvasNet-Rendering-Shapes-Circle: FillCircle/StrokeCircle Draw a Circle at a Given Center and Radius

**Tests**: `Shapes_FillCircle_ProducesRoundRegion`, `Shapes_StrokeCircle_ProducesRing`

Verifies `FillCircle` and `StrokeCircle` paint a round filled region and a ring-shaped stroked
outline respectively.

#### CanvasNet-Rendering-Shapes-Validation: A Null Canvas Argument Is Rejected

**Test**: `Shapes_NullCanvas_ThrowsArgumentNullException`

Verifies every `Shapes` helper rejects a `null` `Canvas` argument with `ArgumentNullException`.

### Traceability

Every requirement in `docs/reqstream/canvas-net/rendering/shapes.yaml` links to one or more of
these tests.
