## ChartModel Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `ChartModel`
subsystem, as of this release containing two units: `ChartDocument` and `ChartRenderer`.

### Verification Approach

The `ChartModel` subsystem is verified entirely through its two units' own unit tests (see
_ChartDocument Unit Verification Design_, `chart-model/chart-document.md`, and
_ChartRenderer Unit Verification Design_, `chart-model/chart-renderer.md`). No separate
subsystem-level tests otherwise exist, since every `ChartRenderer` test already builds its input
`Chart` via `ChartDocument`'s own public API (`ChartBuilder` or the model constructors directly),
providing the cross-unit coverage a dedicated subsystem-level test would otherwise exist to
provide; the subsystem-level requirements reuse both units' own test evidence directly.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `ChartDocument`'s only dependency is the core `CanvasNet` system's
  `Canvas` subsystem's `Rgba32` unit, and `ChartRenderer`'s dependencies are the core `CanvasNet`
  system's `Canvas`/`Drawing`/`Geometry`/`Fonts`/`Rendering` subsystems — all in-house,
  already-verified units

### Acceptance Criteria

The `ChartModel` subsystem's verification passes when every unit test scenario described in
_ChartDocument Unit Verification Design_ (`chart-model/chart-document.md`) and
_ChartRenderer Unit Verification Design_ (`chart-model/chart-renderer.md`) passes without error
or unexpected exception.

### Test Scenarios

The `ChartModel` subsystem's test scenarios are those named in
_ChartDocument Unit Verification Design_ (`chart-model/chart-document.md`) and
_ChartRenderer Unit Verification Design_ (`chart-model/chart-renderer.md`); see those documents
for the complete list.
