### Canvas Unit Verification Design

The `Canvas` unit is verified by `test/DemaConsulting.CanvasNet.Tests/Rendering/CanvasTests.cs`.

### Verification Approach

- **Stack semantics**: `Canvas_Save_ThenRestore_RestoresPreviousTransform` and
  `Canvas_Save_NestedSaveRestore_UnwindsInLifoOrder` verify Save/Restore behavior;
  `Canvas_Restore_OnEmptyStack_ThrowsInvalidOperationException` verifies the empty-stack throw.
- **Transform-independent Clear**: `Canvas_Clear_DelegatesToSurfaceClear_ProducesByteIdenticalOutput`
  compares `Canvas.Clear`'s output byte-for-byte against calling `Surface.Clear` directly;
  `Canvas_Clear_WithNonIdentityTransform_StillFillsWholeSurfaceIdentically` proves a non-identity
  `CurrentTransform` has no effect on `Clear`'s result, unlike every fill/stroke member.
- **Composition order**: `Canvas_Translate_ThenTranslate_ComposesAdditively`,
  `Canvas_RotateDegrees_ThenTranslate_AppliesTranslationInRotatedFrame`, and
  `Canvas_Translate_ThenRotateDegrees_AppliesRotationInTranslatedFrame` verify prepend
  composition.
- **Byte-identical no-transform**: `Canvas_FillPath_NoTransform_ProducesByteIdenticalOutputToPathFillerFill`
  and `Canvas_StrokePath_NoTransform_ProducesByteIdenticalOutputToPathStrokerPlusPathFiller`
  compare the surface byte-for-byte against direct `PathFiller`/`PathStroker` output at identity.
- **Observable transform**: `Canvas_FillPath_WithTranslate_ShiftsOutputByExpectedPixels`
  verifies pixels shift by the translation applied.
- **Validation**: null-surface, null-path, null-gradient, null-style, and invalid fill-rule
  tests cover the validation contract.
- **Surface ownership (no requirement link)**:
  `Canvas_Surface_DisposedExternally_CanvasDoesNotThrowFromAccessingSurfaceProperty` is a
  design-intent regression test confirming `Canvas` holds a non-owning reference to its wrapped
  `Surface` — it disposes the `Surface` externally, then confirms accessing `Canvas.Surface`
  itself does not throw, proving `Canvas` adds no ownership check of its own.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; every test uses only in-process `Surface`/`Path`/`Gradient`/
  `StrokeStyle` public APIs
- **Isolation**: Each test constructs its own `Surface` and `Canvas` fixture, with no shared
  state between tests

### Acceptance Criteria

A unit test run passes when every scenario below passes without error or unexpected exception,
and when every named test method listed for each requirement ID passes across every target
framework.

### Test Scenarios

#### CanvasNet-Rendering-Canvas-Save: Save Pushes the Current Transform

**Test**: `Canvas_Save_ThenRestore_RestoresPreviousTransform`

Verifies `Save` pushes the current transform onto the transform stack.

#### CanvasNet-Rendering-Canvas-Clear: Clear Fills the Whole Surface Independent of the Current Transform

**Tests**: `Canvas_Clear_DelegatesToSurfaceClear_ProducesByteIdenticalOutput`,
`Canvas_Clear_WithNonIdentityTransform_StillFillsWholeSurfaceIdentically`

Compares `Canvas.Clear`'s output byte-for-byte against calling `Surface.Clear` directly, and
proves a non-identity `CurrentTransform` has no effect on `Clear`'s result, unlike every
fill/stroke member.

#### CanvasNet-Rendering-Canvas-Restore: Restore Pops the Transform Stack

**Tests**: `Canvas_Save_ThenRestore_RestoresPreviousTransform`,
`Canvas_Save_NestedSaveRestore_UnwindsInLifoOrder`

Verifies `Restore` pops the transform stack and replaces the current transform with the popped
value, including LIFO ordering across nested `Save`/`Restore` pairs.

#### CanvasNet-Rendering-Canvas-RestoreEmpty: Restore on an Empty Stack Throws

**Test**: `Canvas_Restore_OnEmptyStack_ThrowsInvalidOperationException`

Verifies `Restore` throws `InvalidOperationException` when called with an empty transform stack.

#### CanvasNet-Rendering-Canvas-Translate: Translate Prepends a Translation to the Current Transform

**Tests**: `Canvas_Translate_ThenTranslate_ComposesAdditively`,
`Canvas_FillPath_WithTranslate_ShiftsOutputByExpectedPixels`

Verifies repeated `Translate` calls compose additively, and that a translated `Canvas` shifts
filled output by the expected number of pixels.

#### CanvasNet-Rendering-Canvas-RotateDegrees: RotateDegrees Prepends a Degree-Specified Rotation

**Tests**: `Canvas_RotateDegrees_ThenTranslate_AppliesTranslationInRotatedFrame`,
`Canvas_Translate_ThenRotateDegrees_AppliesRotationInTranslatedFrame`

Verifies `RotateDegrees` composes with `Translate` in prepend order, matching HTML5 Canvas/Skia
composition semantics regardless of call order.

#### CanvasNet-Rendering-Canvas-FillPath: FillPath Bakes the Current Transform Into the Filled Path

**Tests**: `Canvas_FillPath_NoTransform_ProducesByteIdenticalOutputToPathFillerFill`,
`Canvas_FillPath_WithTranslate_ShiftsOutputByExpectedPixels`

Verifies the solid-color `FillPath` overload bakes the current transform into the path before
delegating to `Drawing.PathFiller.Fill`.

#### CanvasNet-Rendering-Canvas-FillPathGradient: Gradient FillPath Bakes the Transform Into Path and Gradient Mapping

**Test**: `Canvas_FillPath_GradientWithRotateAndTranslate_MatchesPreTransformedGradientFill`

Verifies the gradient-paint `FillPath` overload bakes the current transform into both the path
and the gradient's own coordinate mapping (via `Gradient.WithTransform`), so the gradient
continues to visually follow the filled shape under a translated or rotated `Canvas`.

#### CanvasNet-Rendering-Canvas-StrokePath: StrokePath Bakes the Current Transform Into the Stroked Path

**Test**: `Canvas_StrokePath_NoTransform_ProducesByteIdenticalOutputToPathStrokerPlusPathFiller`

Verifies `StrokePath` bakes the current transform into the path before delegating to
`Drawing.PathStroker.Stroke` plus `Drawing.PathFiller.Fill`.

#### CanvasNet-Rendering-Canvas-Identity: Identity Transform Matches Direct PathFiller/Stroker Calls Byte-for-Byte

**Tests**: `Canvas_FillPath_NoTransform_ProducesByteIdenticalOutputToPathFillerFill`,
`Canvas_StrokePath_NoTransform_ProducesByteIdenticalOutputToPathStrokerPlusPathFiller`,
`Canvas_Constructor_WithSurface_HasIdentityTransform`

Verifies a newly constructed `Canvas` starts at the identity transform, and that filling/stroking
at identity produces byte-identical output to calling `PathFiller`/`PathStroker` directly.

#### CanvasNet-Rendering-Canvas-Validation: Invalid Arguments Are Rejected

**Tests**: `Canvas_Constructor_WithNullSurface_ThrowsArgumentNullException`,
`Canvas_FillPath_NullPath_ThrowsArgumentNullException`,
`Canvas_FillPath_NullGradient_ThrowsArgumentNullException`,
`Canvas_StrokePath_NullStyle_ThrowsArgumentNullException`,
`Canvas_FillPath_InvalidFillRule_ThrowsArgumentOutOfRangeException`

Verifies a `null` `Surface` constructor argument, a `null` `Path` argument to fill or stroke, a
`null` `Gradient` argument to the gradient-paint `FillPath` overload, and a `null` `StrokeStyle`
argument to stroke are all rejected with `ArgumentNullException`, and an invalid fill rule is
rejected with `ArgumentOutOfRangeException`.

### Traceability

Every requirement in `docs/reqstream/canvas-net/rendering/canvas.yaml` links to one or more of
these tests.
