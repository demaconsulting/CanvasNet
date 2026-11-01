## Canvas Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Canvas` subsystem
(the `Surface` unit, the `Rgba32` value-type unit, and the `CssColor` parser unit).

### Verification Approach

The `Canvas` subsystem is verified entirely through its constituent units' tests: the `Surface`
unit's tests (see _Surface Unit Verification Design_, `canvas/surface.md`) exercise every public
constructor, property, indexer, span accessor, the `Crop` method, and the `Resize`/`ResizeToFit`
resampling API (requirements `CanvasNet-Canvas-Resize`, `-ResizeAlpha`, `-ResizeValidation` and
`-ResizeToFit`) in isolation. No separate
subsystem-level tests exist; the subsystem-level requirements reuse the same `Surface` unit tests
as verification evidence, alongside the `Rgba32` unit's own tests (see _Rgba32 Unit Verification
Design_, `canvas/rgba32.md`) and the `CssColor` unit's own tests (see _CssColor Unit Verification Design_,
`canvas/css-color.md`); the `Canvas` subsystem contains exactly these three units.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the `Canvas` subsystem has no injectable dependencies

### Acceptance Criteria

The `Canvas` subsystem's verification passes when every `Surface` unit test scenario described in
_Surface Unit Verification Design_ (`canvas/surface.md`) passes without error or unexpected
exception.

### Test Scenarios

The `Canvas` subsystem's test scenarios are those named in _Rgba32 Unit Verification Design_
(`canvas/rgba32.md`), those named in _CssColor Unit Verification Design_ (`canvas/css-color.md`:
hexadecimal and named colors, `rgb()`/`hsl()` functions, rounding and clamping, invalid text, and
formatting), plus those named in _Surface Unit Verification Design_
(`canvas/surface.md`): construction and invariants, indexer and span-accessor boundary conditions,
`Crop` normal/boundary/error paths, `Resize`/`ResizeToFit` size, filter, premultiplied-alpha (no color
fringe) and argument-validation behavior (`CanvasNet-Canvas-Resize*`),
`PremultiplyAlpha`/`UnpremultiplyAlpha`/`CompositeOver`
compositing correctness, and `Dispose` idempotency. No subsystem-level test exists beyond
these units' own coverage.
