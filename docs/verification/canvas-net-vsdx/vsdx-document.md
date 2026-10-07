## VsdxDocument Unit Verification Design

<!-- cspell:ignore vsdx -->

> **STATUS: TODO.** This is a skeleton unit verification document created alongside the
> `CanvasNetVsdx` requirements/design skeleton, before any `VsdxDocument` source or test code
> exists. It establishes the section structure a later agent must complete once the
> Implementation Phase Plan in `canvas-net-vsdx.md` reaches milestone (h) (comprehensive test
> suite). No section below should be treated as verified until that later pass replaces this
> notice and fills in real content against real, passing tests.

### Verification Approach

TODO: Describe `VsdxDocument`'s own unit-level verification strategy once its test suite exists,
mirroring the Verification Approach section of _PptxDocument Unit Verification_
(`canvas-net-pptx/pptx-document.md`): dedicated test classes per resolver area (package/relationship
resolution, page model, geometry/transform, Master/MasterShape inheritance, StyleSheet chain
resolution, color/fill, text, connectors/glue-points, groups, and the public `Render` API), plus a
fixtures-corpus test class rendering every staged real-world sample.

### Test Environment

TODO: Describe the test environment once the test project exists, mirroring the Test Environment
section of _PptxDocument Unit Verification_ (`canvas-net-pptx/pptx-document.md`).

### Unit-Level Test Scenarios

TODO: Enumerate each unit-level test scenario here, one `####` subsection per test method, once
`VsdxDocumentTests.cs` and its sibling test classes exist (see the Implementation Phase Plan in
`canvas-net-vsdx.md`). Each subsection must describe the scenario, the expected outcome, and
reference the real, passing test method that proves it - following the exact per-scenario
subsection format already established by _PptxDocument Unit Verification_
(`canvas-net-pptx/pptx-document.md`).
