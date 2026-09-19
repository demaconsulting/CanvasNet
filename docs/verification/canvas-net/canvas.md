## Canvas Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Canvas` subsystem
(the `Surface` unit and its supporting `Rgba32` value type).

### Verification Approach

The `Canvas` subsystem is verified entirely through its constituent unit's tests: the `Surface`
unit's tests (see _Surface Unit Verification Design_, `canvas/surface.md`) exercise every public
constructor, property, indexer, span accessor, and the `Crop` method in isolation. No separate
subsystem-level tests exist; the subsystem-level requirements reuse the same `Surface` unit tests
as verification evidence, since the `Canvas` subsystem currently contains exactly one unit with
externally observable behavior.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the `Canvas` subsystem has no injectable dependencies

### Acceptance Criteria

The `Canvas` subsystem's verification passes when every `Surface` unit test scenario described in
_Surface Unit Verification Design_ (`canvas/surface.md`) passes without error or unexpected
exception.
