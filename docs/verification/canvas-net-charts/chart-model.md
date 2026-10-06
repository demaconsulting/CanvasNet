## ChartModel Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `ChartModel` subsystem
(as of this release, its single constituent unit, `ChartDocument`).

### Verification Approach

The `ChartModel` subsystem is verified entirely through `ChartDocument`'s own unit tests (see
_ChartDocument Unit Verification Design_, `chart-model/chart-document.md`). No separate
subsystem-level tests otherwise exist, since `ChartModel` contains exactly one unit as of this
release; the subsystem-level requirements reuse that unit's own test evidence directly.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `ChartDocument`'s only dependency is the core `CanvasNet` system's
  `Canvas` subsystem's `Rgba32` unit, an in-house, already-verified value type

### Acceptance Criteria

The `ChartModel` subsystem's verification passes when every unit test scenario described in
_ChartDocument Unit Verification Design_ (`chart-model/chart-document.md`) passes without error
or unexpected exception.

### Test Scenarios

The `ChartModel` subsystem's test scenarios are those named in
_ChartDocument Unit Verification Design_ (`chart-model/chart-document.md`); see that document
for the complete list.
