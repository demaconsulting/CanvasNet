## Drawing Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Drawing` subsystem
(the `PathFiller` unit, covering the supporting `FillRule` enum and the internal
`EdgeFlattener`/`ScanlineRasterizer` helpers).

### Verification Approach

The `Drawing` subsystem is verified primarily through its single constituent unit's own tests
(see _PathFiller Unit Verification Design_, `drawing/path-filler.md`), exercising `PathFiller`'s
public API, and the internal `EdgeFlattener`/`ScanlineRasterizer` helpers (accessible to the test
project via `InternalsVisibleTo`), in isolation. A single system-level integration test
additionally exercises the `Geometry`, `Drawing`, and `Canvas` subsystems together end to end
(see the system verification design, `../canvas-net.md`): building a triangular `Path` via
`PathBuilder`, filling it onto a `Surface` via `PathFiller.Fill`, and asserting fully-covered
interior pixels, untouched exterior pixels, and antialiased (partially covered) edge pixels -
confirming the subsystem's rasterizer, the `Geometry` subsystem's path type, and the `Canvas`
subsystem's `Surface.CompositeOverSpan` collaborate correctly.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the `Drawing` subsystem has no injectable dependencies

### Acceptance Criteria

The `Drawing` subsystem's verification passes when every unit test scenario described in
_PathFiller Unit Verification Design_ (`drawing/path-filler.md`) passes without error or
unexpected exception, and the system-level integration test described above passes.
