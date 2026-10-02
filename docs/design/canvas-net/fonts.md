## Fonts

<!-- cspell:ignore glyf sfnt cmap loca hmtx hhea codepoint codepoints subtables -->
![Fonts Structure](FontsView.svg)

<!-- cspell:ignore glyf SFNT Sfnt Cmap Glyf Loca Hmtx Hhea codepoints subtables -->
<!-- cspell:ignore charstring charstrings ttcf OTTO dogfooding Dejavu Nimbus Consolas -->
<!-- cspell:ignore Segoe LOCALAPPDATA -->

The `Fonts` subsystem is the fifth software subsystem in CanvasNet. It groups the `TrueTypeFont`
unit that loads glyph-based TrueType SFNT fonts as well as CFF/OpenType (`OTTO`-flavored) fonts
and individual faces of a TrueType Collection (`ttcf`) container, maps Unicode codepoints to
glyph indices, extracts glyph outlines as `Geometry.Path` geometry, reports horizontal metrics
and basic kerning, and exposes each font's name (`name` table) and derived bold/italic/
fixed-pitch style metadata. `TrueTypeFont` additionally loads standalone classic PostScript
Type 1 and Type 1C font programs (`LoadType1`/`LoadType1C`), decrypting and decoding their
`eexec`-encrypted Type 1 charstrings (PFB segmented binary or PFA hex-encoded ASCII source) the
same way it decodes `glyf`/CFF outlines, so a caller can load any of the three classic font
program flavors through one unit; and (added this phase, `TrueTypeFont` itself unchanged) the
`SystemFontCatalog` unit, which discovers fonts installed on the host operating system by
directory scan, best-effort matches a requested family name/style against that catalog, and
provides a bundled, always-available Liberation Sans/Serif/Mono last-resort fallback font shipped
as an embedded resource of this assembly. Internally, `TrueTypeFont` fronts `SfntContainer`,
`CmapTable`, `GlyfLocaReader`, `CffTable`, `CffCharstringInterpreter`, `HmtxHheaReader`,
`KernTable`, `NameTable`, and `StyleTable` exactly as `PathFiller` fronts `EdgeFlattener` and
`ScanlineRasterizer`: one public entry point coordinating several independently testable helpers.
`GlyfLocaReader` and `CffTable` both implement a small internal `IGlyphOutlineSource` abstraction
so `TrueTypeFont` dispatches `GetGlyphOutline` to whichever outline flavor the loaded font
actually uses without any type-checking of its own. `SystemFontCatalog` is a much shallower unit
by comparison: it calls `TrueTypeFont` as a client, not as an internal helper, and has no
supporting internal types of its own.

### Purpose

The `Fonts` subsystem provides dependency-free parsing and querying of TrueType (`glyf`-based)
and CFF/OpenType (`OTTO`-flavored, Type 2 charstring-based) SFNT font files, including selecting
an individual face out of a TrueType Collection (`ttcf`) container, as well as standalone classic
PostScript Type 1 and Type 1C font programs (PFB/PFA source, `eexec`-encrypted Type 1
charstrings). Its responsibility ends at
vector geometry and scalar metrics: it loads font structure, resolves codepoints to glyph
indices, decodes glyph contours into `DemaConsulting.CanvasNet.Geometry.Path`, reports advance
widths, returns pairwise kerning adjustments from classic `kern` format-0 subtables when present,
and resolves the font's own name (family/subfamily/full/PostScript name) and bold/italic/
fixed-pitch style classification from its `name`, `OS/2`, `head`, and `post` tables. Since this
phase, the subsystem also answers "what fonts does this process consider installed, and which
one is the best available substitute for an unavailable font" - `SystemFontCatalog`'s directory
scan, `FindBestMatch` scoring, and bundled-fallback loading - as a format-agnostic capability any
document-format consumer can call. Text layout, shaping, hint execution, point-size scaling, and
pixel rendering remain outside this subsystem's boundary.

### Units

- **TrueTypeFont** — the first public unit of the subsystem. It exposes `Load(Stream)` /
  `Load(string)`, the explicit-face-selection overloads `Load(Stream, int)` / `Load(string, int)`,
  `LoadType1`/`LoadType1C` for standalone classic PostScript Type 1/Type 1C font programs,
  `GetFaceCount(Stream)` / `GetFaceCount(string)`, query methods for codepoint mapping, glyph
  outlines, advance widths, and kerning, `GetNameInfo()` for the font's name-table strings, and
  `IsBold`/`IsItalic`/`IsFixedPitch` for its derived style classification. Its unit design
  documents the internal helpers inline because none has an independent public contract beyond
  supporting `TrueTypeFont`; see _TrueTypeFont Unit Design_ (`fonts/true-type-font.md`)
- **SystemFontCatalog** — the second public unit of the subsystem, added this phase. It exposes
  `Fonts` (the lazily-built, process-lifetime-cached OS font catalog), `FindBestMatch` (best-effort
  family-name/style matching against that catalog), and `LoadBundledFallback` (the bundled
  Liberation Sans/Serif/Mono last resort); see _SystemFontCatalog Unit Design_
  (`fonts/system-font-catalog.md`)

### Dependencies

The `Fonts` subsystem depends only on the `Geometry` subsystem, specifically its `Path`,
`PathBuilder`, and `PathCommand` abstractions (used by `TrueTypeFont`), and, for
`SystemFontCatalog`, on `System.Runtime.InteropServices.RuntimeInformation` to select this
process's per-operating-system font-directory scan roots, and on this assembly's own embedded
`Fonts/BundledFonts/*.ttf` resources. `TrueTypeFont.GetGlyphOutline` returns raw vector
geometry in font design units: the `glyf`-flavored glyph decoder issues only `MoveTo`, `LineTo`,
`QuadraticBezierTo`, and `Close` commands, while the CFF/Type 2 charstring decoder issues only
`MoveTo`, `LineTo`, `CubicBezierTo`, and `Close` commands, both through `PathBuilder`. The
subsystem does **not** depend on `Canvas` or `Drawing` directly: it neither allocates surfaces
nor rasterizes pixels. The system-integration test that fills a glyph outline proves callers can
combine `Fonts` output with `Drawing.PathFiller` and `Canvas.Surface`, but that collaboration
happens outside the subsystem itself.

### Callers

`TrueTypeFont` and `SystemFontCatalog` are both public API entry points, called directly by
consumers of the CanvasNet package. Within this repository, the subsystem is exercised by
dedicated `Fonts` unit tests, by real-font integration tests in
`TrueTypeFontRealFontIntegrationTests.cs` (covering real `.ttf`, `.otf`, and locally-assembled
`.ttc` fixtures), by `BundledLiberationFontsTests.cs` (dogfooding all 12 bundled Liberation
files through `TrueTypeFont.Load` itself), by `SystemFontCatalogRealDiscoveryIntegrationTests.cs`
(gated real-filesystem discovery), and by the two system-integration tests in `CanvasNetTests.cs`
that load a synthetic font, query metrics, obtain an outline, and render that outline through
`PathFiller` onto a `Surface`. `SystemFontCatalog` is additionally called by
`DemaConsulting.CanvasNet.Pdf`'s `PdfDocument` when resolving a non-embedded font.
