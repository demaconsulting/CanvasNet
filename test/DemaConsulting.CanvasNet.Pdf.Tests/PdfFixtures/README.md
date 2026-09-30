# PDF Test Fixtures

<!-- cspell:ignore xobject devicergb -->

Every PDF file in this folder is a small, hand-authored document created specifically for this
repository to exercise `PdfDocument`'s parsing internals (tokenizer, object model,
cross-reference resolution in all three forms, the linear-scan fallback, page-tree traversal with
inheritance, and `/Encrypt` detection - Phase 1), its content-stream interpreter (path
construction/painting operators, the graphics-state stack, CTM/rotation derivation, and
`/Contents` resolution - Phase 2), and its device color/image-XObject support (`rg`/`cs`/`scn`
device color operators and `Do`-placed image XObjects - Phase 3) end to end via real files on
disk. There is no third-party source corpus behind any of them (unlike, for example, `PngSuite` in
the core test project): each was constructed byte-by-byte from scratch for CanvasNet and is
licensed under the same MIT license as the rest of this repository.

| File | Exercises |
| ------ | ----------- |
| `classic-xref-single-page.pdf` | Classic `xref` table + `trailer` dictionary, one page |
| `xref-stream-single-page.pdf` | A `/Type /XRef` cross-reference stream, no classic table |
| `object-stream.pdf` | A `/Type /ObjStm` compressed-object stream holding the page dictionary |
| `hybrid-xref.pdf` | Classic trailer + `/XRefStm` hybrid link to a supplementary xref stream |
| `multi-page-mixed-mediabox-rotate.pdf` | Three pages, differing/inherited `/MediaBox` and `/Rotate` |
| `malformed-startxref.pdf` | No `startxref`/`xref`/`trailer` at all - exercises the linear-scan fallback |
| `encrypted-trailer.pdf` | A trailer containing an `/Encrypt` key - exercises `/Encrypt` detection |
| `cyclic-page-tree.pdf` | A `/Kids` entry referencing an ancestor - exercises page-tree cycle rejection |
| `path-construction-rect-and-line.pdf` | Filled rectangle (`re f`) plus a stroked line (`m`/`l`/`S`) |
| `path-construction-rotated-page.pdf` | `/Rotate 90` with an asymmetric filled rectangle - CTM/rotation-sign check |
| `contents-array-two-streams.pdf` | `/Contents` as an array - proves space-separator stream concatenation |
| `no-contents-page.pdf` | A page with no `/Contents` key - proves it still renders as a blank surface |
| `color-rgb-rectangle-fill.pdf` | `rg` device color (Phase 3) - a rectangle filled opaque red |
| `image-xobject-devicergb-flate.pdf` | `/Subtype /Image` XObject (2x2 `DeviceRGB`/`FlateDecode`) placed via `cm`/`Do` |
| `text-embedded-truetype-font.pdf` | `/Subtype /TrueType` font, embedded `/FontFile2` (Phase 4) - `Tf`/`Td`/`Tj` |
| `content-stream-lzw.pdf` | `/Contents` compressed with `LZWDecode` (Phase 7) - a filled rectangle |
| `content-stream-ascii85.pdf` | `/Contents` armored with `ASCII85Decode` (Phase 7) - the same filled rectangle |
| `content-stream-asciihex.pdf` | `/Contents` armored with `ASCIIHexDecode` (Phase 7) - the same filled rectangle |
| `content-stream-runlength.pdf` | `/Contents` compressed with `RunLengthDecode` (Phase 7) - the same filled rectangle |

`text-embedded-truetype-font.pdf` is the one exception to the "no third-party source corpus"
statement above: its `/FontFile2` stream is a real, unmodified, `FlateDecode`-compressed copy of
the same "Open Sans" TrueType font every other CanvasNet test project shares (see
`DemaConsulting.CanvasNet.Tests\FontFixtures\README.md` for its provenance and SIL Open Font
License 1.1 text, not repeated here) - everything else about the file (its object structure,
page/font/descriptor dictionaries, and content stream) was still hand-authored from scratch for
this repository, exactly like every other fixture in this folder.
