### GlyphFallback

<!-- cspell:ignore Liberation Noto -->

`GlyphFallback` is a public static software unit in the `Fonts` subsystem. It gives a character that
the font a caller chose has no glyph for a real glyph from a fixed chain of bundled fonts, and it
names the characters that even the chain does not cover.

#### Purpose

The bundled Liberation fonts cover Latin, Greek, Cyrillic, and common punctuation, but not
mathematical operators, most arrows, or many symbols. A character such as U+221D (proportional to)
or U+226B (much greater than) was drawn as the font's missing-glyph box (`.notdef`) with no
diagnostic. The unit supplies the per-character fallback that every text path of the library shares,
so that the box becomes a real glyph where a bundled font has one, and a caller can report the
remaining cases.

#### Font Chain

The chain is the three Noto fonts that are already embedded in the assembly (SIL OFL 1.1, see
`Fonts/BundledFonts/README.md`), in this order:

1. `NotoSans-Regular.ttf` - Greek, Latin extensions, general punctuation.
2. `NotoSansMath-Regular.ttf` - mathematical operators, arrows, supplemental mathematical symbols.
3. `NotoSansSymbols2-Regular.ttf` - geometric shapes, emoji, and rare symbols.

They are used by the PDF `Symbol`/`ZapfDingbats` substitute already, so the unit adds nothing to the
package size and nothing to the license notices. Only these fonts are consulted: an installed font
is never used, so a result does not depend on the machine. The chain is the regular style; a bold or
italic request is not synthesized for a fallback glyph. The Noto fonts have 1000 units per em
(Liberation: 2048), so a caller must scale a fallback glyph with the units per em of the font that
supplies it, which `Resolve` makes possible by returning that font.

The fonts have no box-drawing coverage beyond 10 characters, no block elements beyond 8, and no CJK or
private-use coverage; those characters stay missing (and are reported where the host has a
diagnostics channel).

#### Key Methods

- **`BundledFonts`** - the chain as a read-only list; built lazily on first access with
  `SystemFontCatalog.LoadBundledFallbackCore` (a `Lazy<T>` with `ExecutionAndPublication`, over the
  existing locked, process-wide cache of bundled fonts, so each file is loaded once per process). It
  is published through `Array.AsReadOnly`, so the array never escapes and a caller can neither
  replace a font nor cast the list back to an array.
- **`IsIgnorable(codePoint)`** - `true` for a character with no visible form and no width: a value
  outside the Unicode range, a surrogate, a non-character, a variation selector (U+FE00-U+FE0F,
  U+E0100-U+E01EF), a format character (zero-width joiners, bidirectional marks, the soft hyphen
  U+00AD, U+FEFF), and a control character that is not white space. White space (the tab and the
  line-break controls included) is not ignorable: a layout handles it itself. Every text layout
  (`TextRenderer`, `ScadTextGeometry`, Pptx, Vsdx) skips an ignorable character before it resolves a
  glyph and before it touches the pen or the previous-glyph state, so such a character takes no
  glyph, no advance, and does not separate a kerning pair. A soft hyphen is therefore invisible
  (none of these layouts break lines at it).
- **`NeedsGlyph(codePoint)`** - `!IsIgnorable(codePoint)` and not white space: `false` for values
  outside the Unicode range, surrogates, non-characters, variation selectors, and the Unicode
  categories control, format, space separator, line separator, and paragraph separator; `true`
  otherwise (private-use and unassigned code points included). It decides whether a missing glyph is
  looked up in the chain and reported.
- **`Resolve(primary, codePoint)`** - the primary font and its glyph when it maps the code point
  (the fast path: nothing is loaded and no category lookup is made, so text the primary font covers
  is unaffected); otherwise, when `NeedsGlyph` accepts the code point, the first chain font, skipping
  the primary by reference, that maps it, with its glyph; otherwise the primary font and glyph 0.
- **`FindMissing(primary, text)`** - the distinct code points of the text, in ascending order, for
  which `Resolve` ends at glyph 0 and `NeedsGlyph` is true. A lone surrogate is judged as U+FFFD,
  because the text is enumerated as Unicode scalars exactly as the renderers do.

#### Error Handling

`Resolve` and `FindMissing` throw `ArgumentNullException` for a `null` font or text. A missing
embedded resource would be a packaging defect and surfaces as the `InvalidOperationException` of
`LoadBundledFallbackCore`.

#### Dependencies

`SystemFontCatalog` (the internal `LoadBundledFallbackCore`), `TrueTypeFont`, and
`System.Text.Rune`/`System.Globalization.UnicodeCategory`.

#### Callers

- `TextRenderer` (`MeasureText`, `DrawText`, `CreateTextPath`, `TruncateToWidth`) - and through it
  Svg, Charts, Vega-Lite, and the tool's contact sheet and grid overlay.
- `ScadTextGeometry` (OpenSCAD `text()`), which warns for the characters that stay missing.
- Vega-Lite's `TextMeasurer.FindMissingGlyphs`, behind the `vl-text-missing-glyph` warning.
- The Pdf, Pptx, and Vsdx renderers. Pdf consults the chain lazily, only for a character that neither
  its primary substitute nor the plain Liberation Sans covers; Pptx and Vsdx append it to their own
  candidate lists.

#### Thread Safety

Stateless apart from the lazily built, read-only chain; safe for concurrent use.
