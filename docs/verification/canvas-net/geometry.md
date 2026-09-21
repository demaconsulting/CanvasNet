## Geometry Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Geometry` subsystem
(the `Rect`, `Path`/`PathBuilder`, `BezierFlattening`, and `SvgArcConverter` units).

### Verification Approach

The `Geometry` subsystem is verified primarily through each constituent unit's own tests (see
_Rect Unit Verification Design_ (`geometry/rect.md`), _Path Unit Verification Design_
(`geometry/path.md`), _BezierFlattening Unit Verification Design_
(`geometry/bezier-flattening.md`), and _SvgArcConverter Unit Verification Design_
(`geometry/svg-arc-converter.md`)), each exercising its unit's public API in isolation. A single
system-level integration test additionally exercises the units together end to end (see the
system verification design, `../canvas-net.md`): building a `Path` via `PathBuilder` using a
`MoveTo`/`CubicBezierTo`/`ArcTo`/`Close` sequence, flattening the cubic segment directly via
`BezierFlattening`, and computing the path's bounds via `Path.GetBounds` - confirming the four
units collaborate correctly (in particular, that `Path.GetBounds` correctly invokes
`SvgArcConverter` for the `ArcTo` command).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the `Geometry` subsystem has no injectable dependencies

### Acceptance Criteria

The `Geometry` subsystem's verification passes when every unit test scenario described in each
unit's own verification design document passes without error or unexpected exception, and the
system-level integration test described above passes.
