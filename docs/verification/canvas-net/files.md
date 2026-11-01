## Files Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Files` subsystem (the
`SandboxedFileResolver` unit and its supporting `SandboxOptions`, `SandboxedFile`, `SandboxException`, and
`SandboxFailureKind` types).

### Verification Approach

The `Files` subsystem is verified entirely through its single unit's tests: the `SandboxedFileResolver` unit's tests
(see _SandboxedFileResolver Unit Verification Design_, `files/sandboxed-file-resolver.md`) exercise every public
constructor, property, and method in isolation. No separate subsystem-level tests exist; the subsystem-level
requirements reuse the same unit tests as verification evidence. The subsystem contains exactly this one unit, so there
is no inter-unit interaction to verify.

The consumers of the subsystem - the `DataFileResolver` unit of `CanvasNetVegaLite` and the `ScadFileResolver` unit of
`CanvasNetScad` - verify, in their own projects, that every `SandboxException` kind they receive is translated into
their own located error with their own wording; they do not repeat the sandbox mechanics.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the unit's seams (a link probe and a file opener) are real implementations supplied by the
  tests through an internal constructor
- **Files**: per-test temporary directories, deleted by the test; no checked-in file is read
- **Capabilities**: two scenarios create real symbolic links or junctions and skip themselves where the host cannot;
  nothing else depends on the host

### Acceptance Criteria

The `Files` subsystem's verification passes when every `SandboxedFileResolver` unit test scenario described in
_SandboxedFileResolver Unit Verification Design_ (`files/sandboxed-file-resolver.md`) passes without error or unexpected
exception.

### Test Scenarios

The `Files` subsystem's test scenarios are those named in _SandboxedFileResolver Unit Verification Design_
(`files/sandboxed-file-resolver.md`): search order and normalization, missing files, hostile path text, confinement,
link refusal, resolver-callback mode, opening and reading, the size limit, the per-instance cache, cancellation, and
failure reporting. No subsystem-level test exists beyond this unit's own coverage.
