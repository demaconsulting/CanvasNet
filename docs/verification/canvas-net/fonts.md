## Fonts Subsystem Verification Design

<!-- cspell:ignore codepoint codepoints renderable -->

<!-- cspell:ignore Codepoints codepoint renderable charstring charstrings ttcf -->
This document describes the subsystem-level verification strategy for the `Fonts` subsystem (the
`TrueTypeFont` unit, including its CFF/OpenType outline decoding and TrueType Collection support).

### Verification Approach

The `Fonts` subsystem is verified through the `TrueTypeFont` unit tests under
`test/DemaConsulting.CanvasNet.Tests/Fonts/`, through the real-font integration tests in
`TrueTypeFontRealFontIntegrationTests.cs`, and through the two system-integration tests in
`CanvasNetTests.cs`. No dedicated subsystem test file exists. Instead, subsystem-level
requirements reuse the unit tests for parser and decoding behavior, reuse the real-font
integration tests for end-to-end behavior against genuine production font files (`.ttf`, `.otf`,
and `.ttc`), and reuse the system tests for the integrated collaboration between `Fonts`,
`Geometry`, `Drawing`, and `Canvas`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the subsystem uses only in-house byte-array fixtures and in-process
  calls to other CanvasNet public APIs
- **Fixtures**: Synthetic fonts (including synthetic CFF/OTTO fonts and a synthetic multi-face
  `ttcf` container, built by `TestSupport/SyntheticFontBuilder`) are constructed in memory for
  every `TrueTypeFont`/`CffTable`/`CffCharstringInterpreter`/`SfntContainer` unit test. Three
  additional real-fixture integration tests in `TrueTypeFontRealFontIntegrationTests.cs` prove
  end-to-end composition between `Fonts` and the `Drawing` pipeline on real-world glyph data:
  - `TrueTypeFont_RealOpenSansFont_RendersGlyphOutlineAsVisibleInk` loads the real, licensed (SIL
    OFL 1.1) "Open Sans" glyf-flavored production font fixture
    (`test/DemaConsulting.CanvasNet.Tests/FontFixtures/OpenSans-Regular.ttf`, attributed per the
    accompanying `OpenSans.LICENSE`)
  - `TrueTypeFont_RealSourceSans3OtfFont_RendersCffGlyphOutlineAsVisibleInk` loads the real,
    licensed (SIL OFL 1.1) "Source Sans 3" CFF/OpenType production font fixture
    (`test/DemaConsulting.CanvasNet.Tests/FontFixtures/SourceSans3-Regular.otf`, attributed per
    the accompanying `SourceSans3.LICENSE`)
  - `TrueTypeFont_RealTtcContainer_FaceOne_RendersCffGlyphOutlineAsVisibleInk` and
    `TrueTypeFont_RealTtcContainer_FaceZero_RendersGlyfGlyphOutlineAsVisibleInk` load the two
    faces of the locally-assembled `OpenSans-SourceSans3.ttc` TrueType Collection fixture (see
    `FontFixtures/README.md` for its assembly provenance)

  Every other `Fonts` test remains synthetic-fixture-based

### Acceptance Criteria

The `Fonts` subsystem's verification passes when every `Fonts` unit test named in the
`TrueTypeFont` unit verification design passes, when all four real-fixture integration tests in
`TrueTypeFontRealFontIntegrationTests.cs` pass, and when both `CanvasNet_SystemIntegration_*`
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

#### CanvasNet-Fonts-CffOutlineDecoding: CFF/OpenType Fonts Decode Through the Same Public API

**Test**: `TrueTypeFont_RealSourceSans3OtfFont_RendersCffGlyphOutlineAsVisibleInk`

Loads the real "Source Sans 3" CFF/OpenType production font fixture, resolves a flex-operator-free
glyph (capital `H`), decodes its outline through the CFF/Type 2 charstring backend, and fills it
through `PathFiller` onto a `Surface`, confirming actual visible ink is produced from real-world
CFF outline data end to end (not merely that structural parsing succeeded without exception).

#### CanvasNet-Fonts-TtcSupport: TrueType Collection Faces Are Independently Selectable and Renderable

**Tests**: `TrueTypeFont_RealTtcContainer_FaceOne_RendersCffGlyphOutlineAsVisibleInk`,
`TrueTypeFont_RealTtcContainer_FaceZero_RendersGlyfGlyphOutlineAsVisibleInk`

Loads both faces of the locally-assembled `OpenSans-SourceSans3.ttc` fixture via
`TrueTypeFont.Load(string, int)`, confirms `GetFaceCount` reports the container's true face
count, and decodes/renders a glyph from each face, proving both the glyf-flavored face-0 font and
the CFF-flavored face-1 font remain independently usable once selected out of the shared
container.
