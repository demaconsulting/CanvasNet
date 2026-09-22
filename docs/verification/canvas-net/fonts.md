## Fonts Subsystem Verification Design

<!-- cspell:ignore codepoint codepoints renderable -->

<!-- cspell:ignore Codepoints codepoint renderable -->
This document describes the subsystem-level verification strategy for the `Fonts` subsystem (the
`TrueTypeFont` unit).

### Verification Approach

The `Fonts` subsystem is verified through the `TrueTypeFont` unit tests under
`test/DemaConsulting.CanvasNet.Tests/Fonts/` and through the two system-integration tests in
`CanvasNetTests.cs`. No dedicated subsystem test file exists. Instead, subsystem-level
requirements reuse the unit tests for parser and decoding behavior and reuse the system tests for
the integrated collaboration between `Fonts`, `Geometry`, `Drawing`, and `Canvas`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the subsystem uses only in-house byte-array fixtures and in-process
  calls to other CanvasNet public APIs
- **Fixtures**: Synthetic fonts are constructed in memory for all `TrueTypeFont` unit tests. One
  additional integration test,
  `TrueTypeFontRealFontIntegrationTests.TrueTypeFont_RealOpenSansFont_RendersGlyphOutlineAsVisibleInk`,
  loads the real, licensed (SIL OFL 1.1) "Open Sans" production font fixture
  (`test/DemaConsulting.CanvasNet.Tests/FontFixtures/OpenSans-Regular.ttf`, attributed per the
  accompanying `OpenSans.LICENSE`) specifically to prove end-to-end composition between `Fonts`
  and the `Drawing` pipeline on real-world glyph data; every other `Fonts` test remains
  synthetic-fixture-based

### Acceptance Criteria

The `Fonts` subsystem's verification passes when every `Fonts` unit test named in the
`TrueTypeFont` unit verification design passes, and when both `CanvasNet_SystemIntegration_*`
font scenarios pass without error or unexpected exception.

### Test Scenarios

#### CanvasNet-Fonts-Load: Loading a TrueType Font Exposes Metrics and Enforces the Declared Exception Contract

**Test**: `CanvasNet_SystemIntegration_LoadFontAndQueryMetrics_ReturnsExpectedValues`

Loads a synthetic font through `TrueTypeFont.Load`, asserts the top-level metrics exposed by the
public API, and proves the subsystem's load/query path is operational end to end.

#### CanvasNet-Fonts-GlyphMapping: Unicode Codepoints Resolve to Glyph Indices with Predictable Fallback

**Test**: `CanvasNet_SystemIntegration_LoadFontAndFillGlyphOutline_ReturnsExpectedPixels`

Loads a synthetic font, maps codepoint `'A'` to a glyph index, and proves the mapped glyph can
be used immediately for outline extraction and rendering.

#### CanvasNet-Fonts-GlyphOutlineExtraction: Decoded Glyph Geometry Interoperates with Geometry and Drawing

**Test**: `CanvasNet_SystemIntegration_LoadFontAndFillGlyphOutline_ReturnsExpectedPixels`

Extracts a glyph outline as `Geometry.Path`, scales and flips that path into canvas coordinates,
and fills it through `PathFiller` onto a `Surface`, confirming the subsystem returns renderable
vector geometry rather than opaque internal data.

#### CanvasNet-Fonts-Metrics: Advance Width and Kerning Remain Queryable Through the Public API

**Test**: `CanvasNet_SystemIntegration_LoadFontAndQueryMetrics_ReturnsExpectedValues`

After loading the same synthetic font, queries `GetAdvanceWidth` and `GetKerning` and asserts
the returned values match the font's declared metric data.
