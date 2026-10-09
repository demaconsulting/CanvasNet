# PDF Test Fixtures

<!-- cspell:ignore xobject devicergb pdfhost flipperfile qwikpdf -->
<!-- cspell:ignore bfchar bfrange codepoints -->
<!-- cspell:ignore cidfonttype OTTO -->
<!-- cspell:ignore hsbw fontfile -->
<!-- cspell:ignore XUPVJI Gotham thinspace ligatures fontfile -->

Every PDF file in this folder, with three exceptions (`text-embedded-truetype-font.pdf` and
`text-composite-truetype-identity-h.pdf`, which embed a real third-party TrueType font, and
`text-type1c-differences-agl-ligatures.pdf`, a trimmed excerpt of a real-world document - see
each file's own entry below), is a small, hand-authored document created specifically for this
repository to exercise `PdfDocument`'s parsing internals
(tokenizer, object model,
cross-reference resolution in all three forms, the linear-scan fallback, page-tree traversal with
inheritance, and `/Encrypt` detection - Phase 1), its content-stream interpreter (path
construction/painting operators, the graphics-state stack, CTM/rotation derivation, and
`/Contents` resolution - Phase 2), its device color/image-XObject support (`rg`/`cs`/`scn`
device color operators and `Do`-placed image XObjects - Phase 3), its embedded-TrueType text
support (`Tf`/`Td`/`Tj` against a `/FontFile2`-embedded simple font - Phase 4), its Standard-14
font-fallback substitution (Phase 6), its `LZWDecode`/`ASCII85Decode`/`ASCIIHexDecode`/
`RunLengthDecode` content-stream filter set (Phase 7), its `/Type0`/`/Encoding /Identity-H`/
`/CIDFontType2` composite-font text support (2-byte-code `Tj` against a `/FontFile2`-embedded
descendant font, with a non-identity `/CIDToGIDMap` - Phase 9), its `/ToUnicode` CMap
resolution (`bfchar`/`bfrange` operators - Phase 10, resolved but unconsumed at rendering time),
and its `/Type0`/`/CIDFontType0` (CFF-outline) composite-font text support (a non-CID-keyed CFF
program embedded via an `/OpenType`-wrapped `/FontFile3` - Phase 12), and its classic PostScript
Type 1 simple-font support (`/Subtype /Type1`, either an embedded `/FontDescriptor/FontFile`
program or the free non-embedded fallback path, plus the `/StandardEncoding` base encoding -
Phase B)
end to end via real files on disk.
With the same three exceptions noted above (`text-embedded-truetype-font.pdf` and
`text-composite-truetype-identity-h.pdf`, which embed a real, third-party, SIL Open Font
License-licensed TrueType font, and `text-type1c-differences-agl-ligatures.pdf`, a trimmed
real-world excerpt), there is no third-party source corpus behind any of them (unlike, for
example, `PngSuite` in the core test project): each remaining file's object structure,
dictionaries, and content stream were constructed byte-by-byte from scratch for CanvasNet and
are licensed under the same MIT license as the rest of this repository; the embedded font bytes
in those three exception files retain their own original third-party licenses (see each file's
own entry below for details).

| File | Exercises |
| ------ | ----------- |
| `classic-xref-single-page.pdf` | Classic `xref` table + `trailer` dictionary, one page |
| `xref-stream-single-page.pdf` | A `/Type /XRef` cross-reference stream, no classic table |
| `object-stream.pdf` | A `/Type /ObjStm` compressed-object stream holding the page dictionary |
| `hybrid-xref.pdf` | Classic trailer + `/XRefStm` hybrid link to a supplementary xref stream |
| `multi-page-mixed-mediabox-rotate.pdf` | Three pages, differing/inherited `/MediaBox` and `/Rotate` |
| `malformed-startxref.pdf` | No `startxref`/`xref`/`trailer` at all - exercises the linear-scan fallback |
| `linear-scan-stream-noise.pdf` | Stray `<Z` byte pair - raw byte-pattern scan tolerates hex-string-like noise |
| `object-stream-linear-scan-fallback.pdf` | Page dict compressed in `/Type /ObjStm`, no top-level xref/trailer |
| `encrypted-trailer.pdf` | Non-`/Standard` handler (`/Filter /Adobe.PubSec`) - rejects unsupported handlers |
| `cyclic-page-tree.pdf` | A `/Kids` entry referencing an ancestor - exercises page-tree cycle rejection |
| `path-construction-rect-and-line.pdf` | Filled rectangle (`re f`) plus a stroked line (`m`/`l`/`S`) |
| `path-construction-rotated-page.pdf` | `/Rotate 90` with an asymmetric filled rectangle - CTM/rotation-sign check |
| `contents-array-two-streams.pdf` | `/Contents` as an array - proves space-separator stream concatenation |
| `no-contents-page.pdf` | A page with no `/Contents` key - proves it still renders as a blank surface |
| `color-rgb-rectangle-fill.pdf` | `rg` device color (Phase 3) - a rectangle filled opaque red |
| `image-xobject-devicergb-flate.pdf` | `/Subtype /Image` XObject (2x2 `DeviceRGB`/`FlateDecode`) placed via `cm`/`Do` |
| `text-embedded-truetype-font.pdf` | `/Subtype /TrueType` font, embedded `/FontFile2` (Phase 4) - `Tf`/`Td`/`Tj` |
| `text-composite-truetype-identity-h.pdf` | `/Type0`/`Identity-H`/`CIDFontType2` (Phase 9) + `/ToUnicode` (Phase 10) |
| `text-composite-cff-cidfonttype0-identity-h.pdf` | `/Type0`/`Identity-H`/`CIDFontType0` non-CID-keyed OpenType CFF |
| `content-stream-lzw.pdf` | `/Contents` compressed with `LZWDecode` (Phase 7) - a filled rectangle |
| `content-stream-ascii85.pdf` | `/Contents` armored with `ASCII85Decode` (Phase 7) - the same filled rectangle |
| `content-stream-asciihex.pdf` | `/Contents` armored with `ASCIIHexDecode` (Phase 7) - the same filled rectangle |
| `content-stream-runlength.pdf` | `/Contents` compressed with `RunLengthDecode` (Phase 7) - the same filled rectangle |
| `combined-vector-text-image.pdf` | Filled rect + stroked line + image XObject + font text (Phase 8) |
| `standard14-font-fallback.pdf` | `/BaseFont /Helvetica`, no `/FontFile2` - on-disk fallback (Phase 6) fixture |
| `malformed-content-stream.pdf` | `re` operator given only 2 of its 4 required operands (malformed) |
| `text-embedded-type1-font.pdf` | `/Subtype /Type1`, embedded PostScript `/FontFile` (Phase B) - `Tf`/`Td`/`Tj` |
| `text-standard14-type1-no-fontfile.pdf` | `/Subtype /Type1`, `/BaseFont /Helvetica`, no `/FontFile*` (Phase B) |
| `fill-evenodd-nested-rectangles-double-border.pdf` | Even-odd (`f*`) "double border" - four boundaries in one row |
| `text-type1c-differences-agl-ligatures.pdf` | Real-world `/Differences` names resolved via embedded font |

For this phase, a real-world third-party PDF sourcing pass was investigated (mirroring
`SvgFixtures`' Wikimedia Commons CC0 sourcing) to see whether a small, genuinely verifiable,
permissively-licensed sample corpus could be added alongside the hand-authored fixtures. No
suitable candidate was found: Wikimedia Commons no longer topically categorizes "PDF files" (that
category is deprecated/under discussion), and the "public domain sample PDF" sites that turned up
in a web search (`pdfhost.io`, `flipperfile.com`, `qwikpdf.com`) have no checkable authorship or
license text comparable to `WikimediaCommons.LICENSE`'s verifiable provenance. Rather than
fabricate a "real-world" provenance claim for an unverifiable source, the corpus remains entirely
hand-authored and honestly documented as such; a human maintainer can revisit real-world sourcing
in a future, non-blocking follow-up.

`text-embedded-truetype-font.pdf` is one exception to the "no third-party source corpus"
statement above: its `/FontFile2` stream is a real, unmodified, `FlateDecode`-compressed copy of
the same "Open Sans" TrueType font every other CanvasNet test project shares (see
`DemaConsulting.CanvasNet.Tests\FontFixtures\README.md` for its provenance and SIL Open Font
License 1.1 text, not repeated here) - everything else about the file (its object structure,
page/font/descriptor dictionaries, and content stream) was still hand-authored from scratch for
this repository, exactly like every other fixture in this folder. `text-composite-truetype-identity-h.pdf`
reuses the exact same real, unmodified Open Sans TrueType font bytes (independently
`FlateDecode`-recompressed for this second file, but decoding to identical font bytes) as its
`/CIDFontType2` descendant font's own `/FontFile2` - no new font asset was sourced for this
second fixture either. As of Phase 10, `text-composite-truetype-identity-h.pdf` also declares a
`/ToUnicode` CMap stream (object 10, added without disturbing any pre-existing object) mapping
CID 1 via `bfchar` and CID 2 via a single-entry `bfrange` to the Unicode codepoints `U+0048`/
`U+004F` respectively - the same two CIDs the existing content stream's `Tj` operator shows -
wrapped in the standard Adobe CMap/PostScript resource-management boilerplate; this map is
resolved by `ResolveToUnicodeMap` but not yet consumed anywhere at rendering time.

`text-composite-cff-cidfonttype0-identity-h.pdf` (Phase 12) exercises the `/CIDFontType0`
(CFF-outline) descendant-font path: unlike every other fixture with an embedded font, it is
**entirely hand-authored and synthetic, including its embedded font program** - built with the
same `SyntheticFontBuilder.Cff`/`WithSfntVersion(0x4F54544F)` ('OTTO') helper combination already
proven by `DemaConsulting.CanvasNet.Tests\Fonts\TrueTypeFontTests.cs`'s own OTTO/CFF coverage, not
derived from any real-world font. Its `/Type0`/`/Identity-H` font names a `/CIDFontType0`
descendant whose `/FontDescriptor/FontFile3` is a minimal, well-formed, non-CID-keyed CFF program
(no `ROS`/`FDArray`/`FDSelect`) wrapped in an `OTTO`-tagged SFNT container (`/Subtype /OpenType`),
with no `/CIDToGIDMap` declared (ignored for this subtype regardless - identity CID-to-glyph-index
is always used). Its content stream draws Identity-H code `0001` (CID 1, resolving directly to
GID 1 - a filled square spanning font-design-space `(100, 100)`-`(500, 500)` of a 1000-unit em) at
font size 20, text-space origin `(5, 5)`, on a 100x100 `/MediaBox`.

`text-embedded-type1-font.pdf` (Phase B) exercises the `/Subtype /Type1` embedded
`/FontDescriptor/FontFile` classic PostScript Type 1 font path: like the Phase 12 CFF fixture
above, it is **entirely hand-authored and synthetic, including its embedded font program** - built
with the same `SyntheticFontBuilder.Type1` helper already proven by
`DemaConsulting.CanvasNet.Tests\Fonts\Type1TableTests.cs`'s own Type 1 coverage, not derived from
any real-world font. Its font dictionary declares no `/Encoding` (so the default
`/WinAnsiEncoding` base encoding applies), and its embedded program declares three glyphs
(`.notdef`, `space`, and `A` - glyph `A` a filled square spanning font-design-space
`(100, 100)`-`(500, 500)` of a 1000-unit em, matching the Phase 12 CFF fixture's own glyph
design/placement so both fixtures' system-integration tests share the exact same pixel-assertion
convention). Its content stream draws `(A) Tj` at font size 20, text-space origin `(5, 5)`, on a
100x100 `/MediaBox`.

`text-standard14-type1-no-fontfile.pdf` (Phase B) mirrors `standard14-font-fallback.pdf` exactly,
except its font dictionary declares `/Subtype /Type1` instead of `/Subtype /TrueType` - proving
the free non-embedded fallback path (`ResolveFallbackFont`) is reachable for `/Type1` fonts too,
not only `/TrueType` fonts. Like `standard14-font-fallback.pdf`, it declares no
`/FontDescriptor/FontFile`/`/FontFile2`/`/FontFile3` at all.

`fill-evenodd-nested-rectangles-double-border.pdf` reproduces a real-world even-odd ("double
border") rendering defect: four nested rectangles sharing the same x-range (`96 624` wide) but
with slightly different y-ranges (bottom edges at user-space y `60`, `61.67`, `63.33`, `65`,
mirrored at the top), filled with a single `f*` (`FillRule.EvenOdd`) operator - the exact
operator sequence from the original bug report, `0.6 0.6 0.6 rg 96 65 624 930 re 96 61.67 624 934
re 96 63.33 624 932 re 96 60 624 936 re f*`. At full (1:1) resolution each boundary lands in its
own device-pixel row and renders correctly; at the downscaled resolution its system-integration
test renders at, all four bottom boundaries (and, symmetrically, all four top boundaries) land
strictly inside a single device-pixel row, reproducing the `ScanlineRasterizer` cell-accumulation
defect fixed alongside this fixture (see `docs/design/canvas-net/drawing/path-filler.md` and
`ScanlineRasterizerTests`'s own unit-level regressions for the underlying mechanism) - without the
fix, the affected row folds to materially the wrong coverage; with the fix, it matches an
independently hand-computed weighted-parity value exactly.

`text-type1c-differences-agl-ligatures.pdf` is, unlike every other fixture in this folder, **not**
hand-authored: it is a trimmed, single-page excerpt of a real-world document (page 4), kept
otherwise intact (including its two genuinely embedded `/Subtype /Type1`/`/FontFile3 /Type1C`
fonts, `XUPVJI+Gotham-Bold` (`/T1_0`) and `XUPVJI+Gotham-Book` (`/T1_1`)) because it reproduces a
regression that a synthetic fixture could not credibly demonstrate: both fonts declare an
`/Encoding/Differences` array naming glyphs by the exact spelling the embedded font's own CFF
charset actually uses - for example code 28/27 `/uni03BC`, a name the generic Adobe-Glyph-List
common-name subset (`StandardGlyphNames`) does not itself cover, resolved instead via the Adobe
Glyph List's generic `uniXXXX` hex-codepoint naming convention. Code 27 `/thinspace` is itself
now a direct `StandardGlyphNames` entry, not a missing-vocabulary case. Its content stream
genuinely exercises both codes against real body text (`"...50μL..."`, `"...+1 % compared..."`).

> **Note**: this fixture's content stream also genuinely contains a `/f_f` ligature-glyph name
> (an underscore-joined AGL ligature decomposition, naming the "ff" ligature glyph) against real
> body text - previously left unresolved by design, this name now resolves via
> `TryResolveLigatureUnderscoreName`'s underscore-decomposition fallback. However, this fixture's
> own test only asserts that some pixel paints somewhere on the page - a page that already
> painted ink before this fix, from the unrelated text surrounding the ligature - so it cannot by
> itself distinguish a resolved `/f_f` glyph from an unresolved tofu glyph. The isolated proof
> that the mechanism itself works is `PdfDocumentTests`'s own dedicated synthetic regression
> tests, which pixel-assert the specific glyph position against a synthetic embedded font built
> for exactly that purpose; this fixture merely shows the real-world name occurs and the document
> still opens and renders without error.
