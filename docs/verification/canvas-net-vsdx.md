# System Verification Design

<!-- cspell:ignore vsdx -->

> **STATUS: TODO.** This is a skeleton verification document created alongside the
> `CanvasNetVsdx` requirements/design skeleton, before any `CanvasNetVsdx` source or test code
> exists. It establishes the section structure a later agent must complete once the
> Implementation Phase Plan in `canvas-net-vsdx.md` reaches milestone (h) (comprehensive test
> suite). No section below should be treated as verified until that later pass replaces this
> notice and fills in real content against real, passing tests.

## Verification Approach

TODO: Describe the overall verification strategy for `CanvasNetVsdx` once its test suite exists,
following the same approach already established by _CanvasNetPptx System Verification_
(`canvas-net-pptx.md`) and _CanvasNetSvg System Verification_ (`canvas-net-svg.md`): system-level
integration tests exercising `VsdxDocument`'s public API end-to-end against real `.vsdx` fixture
files, plus unit-level tests covering individual resolvers (Master/MasterShape inheritance,
StyleSheet chain resolution, geometry/transform, text, connectors, color/fill, groups) in
isolation. The staged fixture corpus at
`test/DemaConsulting.CanvasNet.Vsdx.Tests.Fixtures-STAGING/` is expected to become this system's
primary source of real-world verification evidence, mirroring `CanvasNetPptx`'s own
fixtures-corpus test precedent (`PptxFixturesCorpusTests.cs`).

## Test Environment

TODO: Describe the test environment (target frameworks, operating systems, and any fixture-file
provenance/licensing notes) once the test project exists, mirroring the Test Environment section
of _CanvasNetPptx System Verification_ (`canvas-net-pptx.md`).

## External Interface Simulation

TODO: Describe how this unit's external dependencies (the core `CanvasNet.Canvas`/`Geometry`/
`Drawing`/`Fonts` subsystems) are exercised directly rather than simulated, mirroring the External
Interface Simulation section of _CanvasNetPptx System Verification_ (`canvas-net-pptx.md`), once
real tests exist to describe.

## System-Level Test Scenarios

TODO: Enumerate each system-level integration test scenario here, one `###` subsection per test
method, once `VsdxSystemIntegrationTests.cs` and its sibling test classes exist (see the
Implementation Phase Plan in `canvas-net-vsdx.md`). Each subsection must describe the scenario, the
expected outcome, and reference the real, passing test method that proves it - following the exact
per-scenario subsection format already established by _CanvasNetPptx System Verification_
(`canvas-net-pptx.md`).

## Acceptance Criteria

TODO: State the acceptance criteria for `CanvasNetVsdx` once its test suite exists, mirroring the
Acceptance Criteria section of _CanvasNetPptx System Verification_ (`canvas-net-pptx.md`) - for
example, that every requirement in `docs/reqstream/canvas-net-vsdx.yaml` and
`docs/reqstream/canvas-net-vsdx/vsdx-document.yaml` links to at least one passing test, and that
the fixtures-corpus suite renders every staged real-world sample without an unhandled exception.
