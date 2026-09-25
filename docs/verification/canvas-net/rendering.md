## Rendering Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Rendering` subsystem
(the `Canvas`, `TextRenderer`, and `Shapes` units).

### Verification Approach

The `Rendering` subsystem is verified through unit tests under
`test/DemaConsulting.CanvasNet.Tests/Rendering/`: `CanvasTests`, `TextRendererTests`, and
`ShapesTests`. No dedicated subsystem test file exists; subsystem-level requirements reuse the
unit tests, particularly the byte-identical no-transform regression in `CanvasTests` that
establishes composition with `Drawing.PathFiller` and `Drawing.PathStroker`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Mocking**: None required; the subsystem uses only in-process CanvasNet public APIs.
- **Fixtures**: Synthetic fonts constructed via `TestSupport/SyntheticFontBuilder` are used
  for `TextRenderer` tests; `Canvas` and `Shapes` tests use in-memory `Surface` instances.

### Coverage Summary

- **Canvas transform stack**: Save/Restore round-trip and LIFO nesting, empty-stack throw,
  Translate/RotateDegrees prepend composition, and byte-identical output against direct
  PathFiller/PathStroker at identity are all covered in `CanvasTests`.
- **TextRenderer**: measurement advance-plus-kerning summation, ascent/descent from font
  metrics, all three alignments, and transform composition under a translated canvas are
  covered in `TextRendererTests`.
- **Shapes**: fill and stroke output, radius clamping in `FillRoundRect`, and Canvas
  current-transform propagation are covered in `ShapesTests`.

### Traceability

Every requirement in `docs/reqstream/canvas-net/rendering.yaml` and in the three unit
requirement documents `rendering/canvas.yaml`, `rendering/text-renderer.yaml`, and
`rendering/shapes.yaml` links to one or more of these tests.
