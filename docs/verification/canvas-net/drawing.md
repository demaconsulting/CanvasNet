## Drawing Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Drawing` subsystem
(the `PathFiller` unit, covering the supporting `FillRule` enum and the internal
`EdgeFlattener`/`ScanlineRasterizer` helpers; the `PathStroker` unit, covering the
supporting `LineCap`/`LineJoin`/`StrokeStyle` types and the internal
`StrokePathFlattener`/`DashSplitter`/`StrokeOutliner` helpers; and the `GradientPaint` unit,
covering the public `Gradient`/`LinearGradient`/`RadialGradient`/`GradientStop`/`GradientSpread`
types and the internal `GradientEvaluator` helper).

### Verification Approach

The `Drawing` subsystem is verified primarily through its constituent units' own tests:
_PathFiller Unit Verification Design_ (`drawing/path-filler.md`),
_PathStroker Unit Verification Design_ (`drawing/path-stroker.md`), and
_GradientPaint Unit Verification Design_ (`drawing/gradient-paint.md`). Those tests exercise both
public APIs directly, and the internal helper types (`EdgeFlattener`/`ScanlineRasterizer`,
`StrokePathFlattener`/`DashSplitter`/`StrokeOutliner`, and `GradientEvaluator`, respectively) in
isolation where that provides clearer evidence than end-to-end pixel checks alone. Two
system-level integration tests additionally exercise the `Geometry`, `Drawing`, and `Canvas`
subsystems together end to end (see the system verification design, `../canvas-net.md`):

- Building a triangular `Path` via `PathBuilder`, filling it onto a `Surface` via
  `PathFiller.Fill`, and asserting fully-covered interior pixels, untouched exterior pixels, and
  antialiased (partially covered) edge pixels - confirming the subsystem's rasterizer, the
  `Geometry` subsystem's path type, and the `Canvas` subsystem's `Surface.CompositeOverSpan`
  collaborate correctly.
- Filling an empty `Path`, and separately a closed `Path` whose bounds fall entirely outside a
  `Surface`, via `PathFiller.Fill`, and asserting every pixel of the surface remains at its
  initial, fully transparent state - confirming the same subsystem collaboration correctly
  no-ops for the empty/out-of-bounds boundary condition rather than throwing or writing pixels.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the `Drawing` subsystem has no injectable dependencies

### Acceptance Criteria

The `Drawing` subsystem's verification passes when every unit test scenario described in
_PathFiller Unit Verification Design_ (`drawing/path-filler.md`),
_PathStroker Unit Verification Design_ (`drawing/path-stroker.md`), and
_GradientPaint Unit Verification Design_ (`drawing/gradient-paint.md`) passes without error or
unexpected exception, and both system-level integration tests described above pass.
<!-- cspell:ignore Outliner -->
