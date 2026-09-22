## Fonts

<!-- cspell:ignore glyf sfnt cmap loca hmtx hhea codepoint codepoints subtables -->
![Fonts Structure](FontsView.svg)

<!-- cspell:ignore glyf SFNT Sfnt Cmap Glyf Loca Hmtx Hhea codepoints subtables -->

The `Fonts` subsystem is the fifth software subsystem in CanvasNet. It groups the single public
`TrueTypeFont` unit that loads glyph-based TrueType SFNT fonts, maps Unicode codepoints to glyph
indices, extracts glyph outlines as `Geometry.Path` geometry, and reports horizontal metrics and
basic kerning. Internally, `TrueTypeFont` fronts `SfntContainer`, `CmapTable`,
`GlyfLocaReader`, `HmtxHheaReader`, and `KernTable` exactly as `PathFiller` fronts
`EdgeFlattener` and `ScanlineRasterizer`: one public entry point coordinating several
independently testable helpers.

### Purpose

The `Fonts` subsystem provides dependency-free parsing and querying of TrueType (`glyf`-based)
SFNT font files. Its responsibility ends at vector geometry and scalar metrics: it loads font
structure, resolves codepoints to glyph indices, decodes glyph contours into
`DemaConsulting.CanvasNet.Geometry.Path`, reports advance widths, and returns pairwise kerning
adjustments from classic `kern` format-0 subtables when present. CFF/OpenType (`OTTO`) fonts,
text layout, shaping, hint execution, point-size scaling, and pixel rendering are all outside
this subsystem's boundary.

### Units

- **TrueTypeFont** — the sole public unit of the subsystem. It exposes `Load(Stream)` /
  `Load(string)` plus query methods for codepoint mapping, glyph outlines, advance widths, and
  kerning. Its unit design documents the internal helpers inline because none has an independent
  public contract beyond supporting `TrueTypeFont`; see _TrueTypeFont Unit Design_
  (`fonts/true-type-font.md`)

### Dependencies

The `Fonts` subsystem depends only on the `Geometry` subsystem, specifically its `Path`,
`PathBuilder`, and `PathCommand` abstractions. `TrueTypeFont.GetGlyphOutline` returns raw vector
geometry in font design units, and the internal glyph decoder issues only `MoveTo`, `LineTo`,
`QuadraticBezierTo`, and `Close` commands through `PathBuilder`. The subsystem does **not**
depend on `Canvas` or `Drawing` directly: it neither allocates surfaces nor rasterizes pixels.
The system-integration test that fills a glyph outline proves callers can combine `Fonts` output
with `Drawing.PathFiller` and `Canvas.Surface`, but that collaboration happens outside the
subsystem itself.

### Callers

`TrueTypeFont` is a public API entry point, called directly by consumers of the CanvasNet
package. Within this repository, the subsystem is exercised by dedicated `Fonts` unit tests and
by the two system-integration tests in `CanvasNetTests.cs` that load a synthetic font, query
metrics, obtain an outline, and render that outline through `PathFiller` onto a `Surface`.
