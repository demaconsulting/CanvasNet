### Canvas Unit Verification Design

The `Canvas` unit is verified by `test/DemaConsulting.CanvasNet.Tests/Rendering/CanvasTests.cs`.

### Verification Approach

- **Stack semantics**: `Canvas_Save_ThenRestore_RestoresPreviousTransform` and
  `Canvas_Save_NestedSaveRestore_UnwindsInLifoOrder` verify Save/Restore behavior;
  `Canvas_Restore_OnEmptyStack_ThrowsInvalidOperationException` verifies the empty-stack throw.
- **Composition order**: `Canvas_Translate_ThenTranslate_ComposesAdditively`,
  `Canvas_RotateDegrees_ThenTranslate_AppliesTranslationInRotatedFrame`, and
  `Canvas_Translate_ThenRotateDegrees_AppliesRotationInTranslatedFrame` verify prepend
  composition.
- **Byte-identical no-transform**: `Canvas_FillPath_NoTransform_ProducesByteIdenticalOutputToPathFillerFill`
  and `Canvas_StrokePath_NoTransform_ProducesByteIdenticalOutputToPathStrokerPlusPathFiller`
  compare the surface byte-for-byte against direct `PathFiller`/`PathStroker` output at identity.
- **Observable transform**: `Canvas_FillPath_WithTranslate_ShiftsOutputByExpectedPixels`
  verifies pixels shift by the translation applied.
- **Validation**: null-surface, null-path, null-style, and invalid fill-rule tests cover the
  validation contract.

### Traceability

Every requirement in `docs/reqstream/canvas-net/rendering/canvas.yaml` links to one or more of
these tests.
