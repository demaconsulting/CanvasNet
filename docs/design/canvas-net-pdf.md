# System Design

<!-- cspell:ignore renderable -->

This document provides the system-level design for CanvasNetPdf.

![CanvasNetPdf Structure](CanvasNetPdfView.svg)

<!-- cspell:ignore xref startxref CCITT bitstream Zapf unembedded bitdepth xobject Annots cidfonttype -->
<!-- cspell:ignore AcroForms ccittfax Noto functiontype multiinput bitspersample fontfile tounicode bfrange -->

## Architecture

CanvasNetPdf is a .NET library providing PDF page-rendering support, distributed as its own
NuGet package (`DemaConsulting.CanvasNet.Pdf`, namespace `DemaConsulting.CanvasNet.Pdf`),
independent of, but depending on, the core `CanvasNet` system (its own separate package,
`DemaConsulting.CanvasNet`) — see the Dependencies section below. The system consists of a
single implemented unit:

- **PdfDocument** (namespace `DemaConsulting.CanvasNet.Pdf`, folder
  `src/DemaConsulting.CanvasNet.Pdf/`, flat — no further nesting): a hand-rolled PDF document
  parser and page-rendering codec, opening a PDF document once and reporting its page
  count/size/rotation, then rasterizing a requested page into a
  `DemaConsulting.CanvasNet.Canvas.Surface` pixel buffer. See _PdfDocument Unit Design_
  (`canvas-net-pdf/pdf-document.md`).

`CanvasNetPdf` is modeled as its own top-level software system (rather than as a further unit of
the `CanvasNet` system's `Codecs` subsystem) because it is distributed as its own
independently-versioned NuGet package — per `software-items.md`'s rule that a software package
contains exactly one software system, a system whose entire content ships in a distinct package
must itself be modeled as a distinct system, not folded into the package it depends on.
`CanvasNetPdf` contains exactly one unit, so no Subsystem tier is interposed between the system
and `PdfDocument`: inserting one here would not separate anything (there is nothing else in the
system to separate it from), mirroring `CanvasNetSvg`'s own single-unit precedent (see
_CanvasNetSvg System Design_, `docs/design/canvas-net-svg.md`).

**Phased delivery.** `PdfDocument` is being delivered incrementally. Phase 1 implemented document
structure parsing only: cross-reference resolution (in all of PDF's common forms), the
trailer/catalog/page tree, and page count/size/rotation reporting; `Render` exposed its final
call shape (a page index plus a caller-chosen output size) but returned only a correctly sized,
fully transparent `Surface`. Phase 2 added a content-stream interpreter: `Render` tokenizes and
executes a page's `/Contents` path-construction (`m`/`l`/`c`/`v`/`y`/`h`/`re`) and path-painting
(`f`/`F`/`f*`/`S`/`s`/`B`/`B*`/`b`/`b*`/`n`) operators, plus the graphics-state stack
(`q`/`Q`/`cm`/`w`/`J`/`j`/`M`/`d`), painting real path geometry in the correct device-pixel
position for the page's `/MediaBox` origin, effective `/Rotate`, and the caller's requested render
size — but every filled/stroked path painted in solid opaque black, since no color operator was
implemented yet. Phase 3 added real device color (`g`/`G`/`rg`/`RG`/`k`/`K`/`cs`/
`CS`/`sc`/`SC`/`scn`/`SCN`), a generalized `/Filter`/`/DecodeParms` stream-decoding pipeline
(`FlateDecode` plus PNG/TIFF predictor reversal), and image XObjects (`Do`: `DCTDecode` via the
`CanvasNet` system's `Codecs.JpegCodec`, or raw `DeviceGray`/`DeviceRGB`/`DeviceCMYK` 8-bit
samples, composited through the current transformation matrix). Phase 4 added real text/font
rendering: `BT`/`ET`/`Tc`/`Tw`/`Tz`/`TL`/`Tf`/`Tr`/`Ts`/`Td`/`TD`/`Tm`/`T*`/`Tj`/`'`/`"`/
`TJ` resolve a simple `/Subtype /TrueType` font from the current page's `/Resources/Font`
dictionary (requiring an embedded `/FontDescriptor/FontFile2`, loaded via the `CanvasNet`
system's `Fonts.TrueTypeFont`), map each shown byte through its `/WinAnsiEncoding`/
`/MacRomanEncoding` (plus `/Differences`) encoding to a Unicode codepoint, and paint the
resulting glyph outline through the composed text-rendering matrix exactly like any other filled
path. Phase 6 narrowed Phase 4's font-resolution boundary: a `/Subtype /TrueType` font with no
embedded `/FontFile2` is no longer an unconditional failure — it is instead automatically
substituted with the closest-matching font actually installed on the host operating system, or,
when nothing matches, a bundled Liberation Sans/Serif/Mono font (via the `CanvasNet` system's
`Fonts.SystemFontCatalog`), except that a `/BaseFont` of `Symbol`/`ZapfDingbats` (or any font
whose `/FontDescriptor/Flags` declares `Symbolic` without also declaring `Nonsymbolic`) still
fails closed with `Codecs.UnsupportedImageFeatureException`, since a symbol/dingbat glyph set has
no meaningful generic-family equivalent. Phase 7 narrowed Phase 3's stream-filter boundary: a
page's `/Contents` or an image XObject's data may now also be filtered with `LZWDecode`,
`ASCII85Decode`, `ASCIIHexDecode`, or `RunLengthDecode` (individually or composed with
`FlateDecode`'s predictor reversal), in addition to `FlateDecode` itself. Phase 9 added composite
font support: a `/Subtype /Type0` font whose `/Encoding` is the name `/Identity-H` and whose
single-element `/DescendantFonts` array names a `/Subtype /CIDFontType2` font with an embedded
`/FontDescriptor/FontFile2` is now resolved by decoding each shown 2-byte code directly as a CID,
mapping it to a glyph index via the descendant's `/CIDToGIDMap`, and resolving its advance width
from the descendant's `/DW`/`/W` entries — composite fonts have no fallback substitution path, so
a missing embedded font still fails closed. Later phases substantially widened this scope. Phase
12 resolves a `/CIDFontType0` descendant font (a non-CID-keyed CFF program, `/OpenType`-wrapped or
bare) identically to `/CIDFontType2`. Phases B/C/D added simple-font subtypes beyond
`/TrueType`: a `/Subtype /Type1` font with an embedded classic `/FontFile` program or a bare
Type1C `/FontFile3` program is resolved and rendered exactly like an embedded `/TrueType` font,
and a `/Subtype /Type3` font (whose glyphs are themselves small content-stream procedures rather
than an outline font program) is resolved via its own dedicated glyph-painting path that re-enters
the same content-stream interpreter for each glyph procedure. Phase 18 widened `/FontFile3`
dispatch for both `/Type1` and composite `/CIDFontType0` fonts to sniff the stream's own decoded
bytes for a recognized container shape rather than trusting the stream's declared `/Subtype`
name. Phase 13 added `/Subtype /Form` XObject rendering: `Do` decodes and executes a Form's
content stream as a nested, implicitly `q`/`Q`-bracketed execution of the same interpreter. Phase
14 replaced the color-space model to add `/CalRGB`, `/ICCBased` (resolved via its `/N` or
`/Alternate`), and `/Indexed` (a palette lookup over any supported base color space), and
introduced the `/Pattern` color space: axial/radial shading patterns (`/PatternType 2`,
`/ShadingType 2`-`7`) reusing a widened Function evaluator that now also supports
`/FunctionType 2` (exponential interpolation) and `/FunctionType 3` (stitching) functions in
addition to `/FunctionType 0` (sampled); and colored/uncolored tiling patterns (`/PatternType 1`,
each cell rendered through the same nested-execution machinery `/Subtype /Form` XObjects use).
Phase 15 added `CCITTFaxDecode` (Group 4/T.6 MMR only) image-XObject decoding. Phases 16/17 added
decryption of a document encrypted with the PDF `/Filter /Standard` security handler using RC4
(`/V 1`/`/V 2`), AES-128 (`/V 4`/`/CFM /AESV2`), or AES-256 using the R5 or R6 (hardened hash) key derivation
(`/V 5`/`/R 5` or `/R 6`/`/CFM /AESV3`), authenticating either the empty user password (the default) or an
optional caller-supplied password tried as both the user and the owner password. **Current
limitations**: `/MMType1` (Multiple Master Type 1) fonts remain entirely unsupported and fail
closed; `/Encoding` values other than `/Identity-H` (including `/Identity-V` and predefined CJK
encodings) and a CID-keyed CFF program fail closed too; only the `/WinAnsiEncoding`/
`/MacRomanEncoding`/`/StandardEncoding` base encodings (plus `/Differences`) are supported (an
unrecognized base encoding fails closed); fill (`Tr 0`), stroke (`Tr 1`), fill+stroke (`Tr 2`),
and invisible (`Tr 3`) text-rendering modes and the clip modes (`Tr 4`-`7`) are supported (a Type 3
glyph under a clip mode fails closed); a
Form XObject's `/BBox` is never used to clip its content and its `/Group` (transparency group)
entry is never consulted, though the Form itself renders; `/ShadingType` values outside `2`-`7`
and `/FunctionType 4` (PostScript calculator) functions fail closed, as does the `sh` operator and
general path clipping (`W`/`W*`), both of which are silently skipped rather than rejected; no
`/SMask`/alpha compositing or transparency groups (every decoded image is treated as fully
opaque); no `JPXDecode` filter decoding (fails closed; `CCITTFaxDecode` - Group 4 (T.6 MMR) only -
is supported); `/Separation`/`/DeviceN`/`/CalGray`/`/Lab` color spaces remain unsupported and fail
closed; and an encrypted document using any security handler, crypt-filter method, `/V`/`/R`
combination other than the ones listed above, or whose correct password is not supplied, fails
closed rather than being decrypted. These remain out of scope and are planned for later phases
(see Risk Control Measures below for the complete, currently-thrown `Feature` string taxonomy).

## External Interfaces

The system exposes the following public API to external consumers, all on the sealed
`PdfDocument` class:

- **PdfDocument.Open(Stream stream, string? password = null)** / **PdfDocument.Open(string path,
  string? password = null)**: Opens and parses a PDF document exactly once, from a stream or a
  file path, buffering the input fully in memory. `Open(Stream, ...)` never takes ownership of
  (or disposes) the caller's stream. `Open(string, ...)` opens, reads, and closes its own
  internal `FileStream` before returning. The optional `password` is consulted only for an
  encrypted (`/Encrypt` present in the trailer) document: when `null` (the default), only the
  empty user password is authenticated; when supplied, it is tried first as the user password,
  then as the owner password, and the document is opened/decrypted transparently the moment
  either authenticates. Throws `ArgumentNullException` for a null `stream`/`path`,
  `ArgumentException` for an empty/whitespace-only `path`, `InvalidDataException` for
  malformed/unresolvable document structure, and `Codecs.UnsupportedImageFeatureException` for an
  encrypted document using an unsupported security handler/crypt-filter method/`/V`/`/R`
  combination, or whose correct password was not supplied (see _PdfDocument Unit Design_ for the
  complete, distinguishable `Feature` token set).
- **PdfDocument.PageCount**: Reports the document's true resolved page count. Throws
  `ObjectDisposedException` once the document has been disposed.
- **PdfDocument.GetPageInfo(int pageIndex)**: Reports the given page's display
  (rotation-adjusted) width/height and normalized effective rotation as a `PdfPageInfo`. Throws
  `ArgumentOutOfRangeException` for `pageIndex < 0 || pageIndex >= PageCount`, and
  `ObjectDisposedException` once disposed.
- **PdfDocument.Render(int pageIndex, int width, int height)**: Returns a `Surface` of the
  caller-specified `width`x`height` for the given page, painted with the page's interpreted
  content-stream geometry (path construction/painting with real device color, solid fills or
  shading/tiling pattern fills, any placed image XObjects decoded through the full `/Filter`
  chain — `FlateDecode`, `LZWDecode`, `ASCII85Decode`, `ASCIIHexDecode`, `RunLengthDecode`,
  `DCTDecode`, `CCITTFaxDecode` (Group 4 only), individually or composed — any nested
  `/Subtype /Form` XObject executed as a nested content stream, and any shown text painted with a
  resolved embedded TrueType/Type1/Type1C/Type3/composite font, or an automatically substituted
  system/bundled fallback font when a simple TrueType font has none embedded — see _PdfDocument
  Unit Design_ for the full operator set and its documented fail-closed boundaries), or a fully
  transparent surface when the page declares no `/Contents`. Validates
  `pageIndex` the same way as `GetPageInfo`, propagates `Surface`'s own `width`/`height`
  validation unwrapped, throws `InvalidDataException` for malformed `/Contents` or a malformed
  recognized operator, throws `Codecs.UnsupportedImageFeatureException` for a well-formed but
  unsupported color space/stream filter/pattern or shading shape/font subtype or encoding/
  Type 3 glyph under a clip text-rendering mode (`pdf-text-render-mode-type3-clip`)/
  symbolic-font-without-embedded-data, and throws `ObjectDisposedException`
  once disposed.
- **PdfDocument.Render(int pageIndex, float dpi)**: Convenience overload preserving the page's
  own aspect ratio: reads `GetPageInfo(pageIndex)`'s rotation-adjusted point-space width/height,
  scales both by `dpi / 72`, rounds to the nearest pixel, and delegates to
  `Render(int, int, int)`. Throws `ArgumentOutOfRangeException` for a non-positive/non-finite
  `dpi`, in addition to every exception `Render(int, int, int)` itself can throw.
- **PdfDocument.Dispose()**: Idempotent; releases the buffered/parsed document state. No other
  public member may be called afterward without throwing `ObjectDisposedException`.

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| -------------------------------------- | ---------------- | ------------------------------------ | ------------------------------- |
| `PdfDocument.Open(...)` | Inbound/Outbound | Method call / `PdfDocument` return | Valid PDF stream or path; optional `password` for an encrypted document |
| `PdfDocument.PageCount` | Outbound | Property read / `int` return | Not disposed |
| `PdfDocument.GetPageInfo(...)` | Inbound/Outbound | Method call / `PdfPageInfo` return | `0 <= pageIndex < PageCount`; not disposed |
| `PdfDocument.Render(...)` | Inbound/Outbound | Method call / `Surface` return | `0 <= pageIndex < PageCount`; `0 < width, height <= 8192`; not disposed |
| `PdfDocument.Render(int, float)` | Inbound/Outbound | Method call / `Surface` return | `0 <= pageIndex < PageCount`; `dpi` positive and finite; not disposed |
<!-- markdownlint-enable MD013 -->

See _PdfDocument Unit Design_ (`canvas-net-pdf/pdf-document.md`) for the complete parsing
internals (tokenizer, object model, all three cross-reference forms, the linear-scan fallback,
and page-tree traversal/inheritance) and every method's full parameter and exception detail.

## Dependencies

`CanvasNetPdf` depends on the separate `CanvasNet` system (its own package,
`DemaConsulting.CanvasNet`, referenced via a project reference from
`src/DemaConsulting.CanvasNet.Pdf/DemaConsulting.CanvasNet.Pdf.csproj`), specifically:

- The `Canvas` subsystem's `Surface` unit — the content-rendered destination raster `Render`
  returns, and `Rgba32` — the fill/stroke color and decoded image-pixel representation
- The `Codecs` subsystem's shared `UnsupportedImageFeatureException` type — reused, unmodified,
  to signal a well-formed-but-unsupported encrypted-document shape, color space, stream filter,
  pattern/shading shape, function type, or font subtype/encoding (see Risk Control Measures
  below)
- The `Codecs` subsystem's `JpegCodec` unit (new as of Phase 3) — decodes an image XObject's raw
  `DCTDecode` (JPEG) bitstream via `JpegCodec.Load(Stream)` directly, without requiring
  APP0/JFIF framing
- The `Geometry` subsystem's `PathBuilder`/`Path` — accumulates each content stream's current
  path as its path-construction operators are dispatched
- The `Drawing` subsystem's `PathFiller`/`PathStroker`/`StrokeStyle`/`FillRule`/`LineCap`/
  `LineJoin` — rasterizes each finished path onto the destination `Surface` for every
  path-painting operator, and, as of Phase 4, every filled glyph outline
- The `Drawing` subsystem's `Gradient`/`LinearGradient`/`RadialGradient`/`GradientStop` and
  `TilePaint` types (new as of the `/Pattern` color-space phase) — `PathFiller`'s `Gradient`
  fill overload paints an axial/radial shading pattern built from the resolved `/Shading`
  function(s); its `TilePaint` fill overload paints a colored/uncolored tiling pattern's
  pre-rendered repeating tile cell, sampled per destination pixel through the pattern-to-device
  transform
- The `Fonts` subsystem's `TrueTypeFont` unit (new as of Phase 4) — loads an embedded
  `/FontFile2` byte stream and resolves each shown codepoint to a glyph index/outline/advance
  width, exactly as `CanvasNetSvg`'s own `SvgCodec.Text.cs` already uses it for SVG `<text>`
  rendering; the same type's `LoadType1`/`LoadType1C` factory methods (new as of the Type1/Type1C
  font phases) load, respectively, an embedded classic PostScript Type 1 `/FontFile` program and
  a bare Type1C/CFF `/FontFile3` program, reusing the same glyph-outline representation rather
  than introducing a separate font type
- The `Fonts` subsystem's `SystemFontCatalog` unit (new as of Phase 6) — locates the
  closest-matching font actually installed on the host operating system (or, when nothing
  matches, loads a bundled Liberation Sans/Serif/Mono fallback, or - for `Symbol`/`ZapfDingbats` -
  a bundled Noto substitute) when a simple font declares no embedded font program, so that
  documents referencing an unembedded standard font still render recognizable glyph shapes
  rather than failing closed
- BCL `System.Security.Cryptography.MD5`/`SHA256`/`Aes` (new as of Phase 16, the encryption
  phase) — implement the PDF Standard Security Handler's own mandated password-hashing
  (ISO 32000-1 Algorithms 2/4/5, ISO 32000-2 Algorithm 2.A) and AES-128/AES-256 stream/string
  decryption; RC4 has no BCL equivalent and is hand-rolled, consistent with this codebase's
  existing convention for other standard algorithms (e.g. CRC-32/Adler-32/CCITT tables)

This dependency on `Geometry`/`Drawing` is new as of Phase 2 (Phase 1 introduced no such
dependency — no Phase 1 file constructed a `Path`, rasterized a fill/stroke, or looked up a
glyph). The dependency on `Fonts.TrueTypeFont` is new as of Phase 4, and the dependency on
`Fonts.SystemFontCatalog` is new as of Phase 6: Phases 1-3 implemented no text/font operator and
introduced no such dependency.

This is an ordinary, same-repository, system-to-system dependency: both `CanvasNet` and
`CanvasNetPdf` are produced by this repository, so it is neither an OTS Software Item (not a
third-party/external-program dependency) nor a Shared Package (that category is scoped to a
package produced by a _different_ repository within the same program). Beyond `CanvasNet`,
`PdfDocument` uses only the .NET base class library's `System.IO.Compression.DeflateStream`
(decompressing `FlateDecode`-filtered object streams), available on every one of CanvasNetPdf's
target frameworks with no new runtime NuGet dependency. `CanvasNetPdf` introduces no new OTS
Software Item beyond those already used to build and verify the `CanvasNet` system (BuildMark,
FileAssert, Pandoc, ReqStream, ReviewMark, SarifMark, SonarMark, VersionMark, WeasyPrint, xUnit)
— see _OTS Integration Design_ (`docs/design/ots.md`).

## Risk Control Measures

`PdfDocument.Open` parses an externally-supplied, potentially untrusted PDF document (a binary
format whose cross-reference tables, object offsets, and page-tree references may be malformed,
inconsistent, or adversarially constructed). Risk control for this untrusted-input parsing is
segregated entirely within the single `PdfDocument` unit and falls into two distinct categories,
each with its own consistently-applied behavior:

- **Tolerated recovery** — a document whose normal cross-reference parsing fails, or resolves to
  a trailer that does not describe a valid `/Type /Catalog` root, falls back to a linear scan of
  the buffered document for `N G obj` markers to reconstruct an object-offset table directly,
  rather than immediately rejecting an otherwise-recoverable document.
- **Rejected reference cycles and malformed structure** — an unbounded page-tree reference cycle
  (a `/Kids` entry referencing one of its own ancestors) is explicitly detected and rejected with
  a thrown `InvalidDataException`, rather than being tolerated: recursing into a genuine cycle
  would otherwise never terminate. Any other unresolvable/malformed structure (an indirect object
  that cannot be located even after the linear-scan fallback, an invalid `/Rotate` value that is
  not a multiple of 90, and so on) is likewise rejected with `InvalidDataException`.

An `/Encrypt` key present in the trailer is detected explicitly: `PdfDocument` decrypts every
indirect object's strings and every stream's raw bytes before any other parsing logic observes
them, for a document using the PDF `/Filter /Standard` security handler with RC4 (`/V 1`/`/V 2`),
AES-128 (`/V 4`/`/CFM /AESV2`), or AES-256 using the R5 or R6 (hardened hash) key derivation (`/V 5`/`/R 5` or `/R 6`/
`/CFM /AESV3`), authenticating either the empty user password (the default) or an optional
caller-supplied password tried as both the user and the owner password. `PdfDocument` never
attempts to interpret the (still-encrypted) bytes of an encrypted document as plaintext content
when authentication fails, or when the document uses a security handler/crypt-filter
method/`/V`/`/R` combination outside this supported set: each such shape fails closed with its
own distinguishable `Codecs.UnsupportedImageFeatureException` immediately upon detection, never
falling back to any tolerant/best-effort decoding. Phase 3 extended this same fail-closed posture
to every well-formed but out-of-scope construct it could then encounter: an unsupported color
space, an unsupported stream filter, and an unsupported image `/BitsPerComponent` are all
rejected with `Codecs.UnsupportedImageFeatureException` rather than being silently skipped or
mis-rendered. Phase 4 extended the same posture to text/font constructs: a font dictionary's
`/MMType1` subtype, an `/Encoding` naming an unrecognized base encoding, and (as of the text-clip
change) a Type 3 glyph shown under a clip text-rendering mode (`Tr 4`-`7`) are all likewise rejected with
`Codecs.UnsupportedImageFeatureException`. Phase 6 narrowed (but did not remove) the font-subtype
fail-closed boundary: a `/Subtype /TrueType` font lacking an embedded `/FontFile2` is now resolved
via automatic system/bundled-font substitution rather than rejected outright, except that a
`/BaseFont` of `Symbol`/`ZapfDingbats` (or any font whose `/FontDescriptor/Flags` declares
`Symbolic` without also declaring `Nonsymbolic`) still fails closed with
`Codecs.UnsupportedImageFeatureException` (feature `"pdf-font-symbolic-not-embedded"`), since a
symbol/dingbat glyph set has no meaningful generic-family equivalent and is never substituted
with an unrelated font. Phase 7 extended the supported stream-filter set from `FlateDecode` alone
to also include `LZWDecode`, `ASCII85Decode`, `ASCIIHexDecode`, and `RunLengthDecode`; any filter
other than these five (plus `DCTDecode`/`CCITTFaxDecode` for image XObjects) remains rejected
with `Codecs.UnsupportedImageFeatureException` (`"pdf-filter-{name}"`), and malformed bytes for
any of the supported filters are rejected with `InvalidDataException` (malformed, not merely
unsupported). Phase 9 narrowed the font-subtype fail-closed boundary again: a `/Subtype /Type0`
font is no longer unconditionally rejected — an `/Encoding` other than `/Identity-H` (feature
`"pdf-font-type0-encoding-{name}"`) or a descendant `/Subtype` other than `/CIDFontType2`/
`/CIDFontType0` (Phase 12 added `/CIDFontType0`, feature `"pdf-font-cidfonttype-{subtype}"`) still
fails closed, and a composite font has no fallback substitution path, so a missing/non-embedded
descendant font program fails closed with `InvalidDataException` rather than
`Codecs.UnsupportedImageFeatureException`. Phases B/C/D narrowed the font-subtype fail-closed
boundary a third time: `/MMType1` is now the only remaining unconditionally rejected font
`/Subtype` — `/Type1` (embedded classic `/FontFile` or bare Type1C `/FontFile3`) and `/Type3`
(procedure-painted glyphs) are both now resolved and rendered. Phase 13 narrowed the Phase 3
color-space/XObject boundary further: `Do` on a `/Subtype /Form` XObject is no longer rejected —
it decodes and executes the Form's content stream as a nested execution of the same interpreter,
subject to its own documented, non-exception-raising limitation (the Form's `/BBox` is never used
to clip its content and its `/Group` transparency-group entry is never consulted — the Form's
content simply paints unclipped). Phase 14 replaced the color-space model: `/CalRGB`, `/ICCBased`
(resolved via its `/N` or `/Alternate`, feature `"pdf-colorspace-ICCBased"` for an unsupported
shape), and `/Indexed` (a palette lookup over any supported base space) are all now supported,
narrowing the unsupported-color-space set to `Separation`/`DeviceN`/`CalGray`/`Lab` (feature
`"pdf-colorspace-{name}"`); the same phase introduced the `/Pattern` color space and `scn`/`SCN`
pattern operands (an undeclared pattern name, feature `"pdf-pattern-not-declared"`; an
unsupported `/PatternType`, feature `"pdf-pattern-type-{n}"`; a malformed `/Pattern` array shape,
feature `"pdf-colorspace-Pattern"`), axial/radial shading patterns (an unsupported
`/ShadingType`, feature `"pdf-shading-type-{n}"`; an unsupported shading `/ColorSpace`, feature
`"pdf-shading-colorspace-{family}"`), tiling patterns (an oversized rendered tile, feature
`"pdf-pattern-tile-too-large"`), and a Function evaluator widened from `/FunctionType 0`
(sampled) alone to also resolve `/FunctionType 2` (exponential) and `/FunctionType 3`
(stitching); any other `/FunctionType` (including `4`, PostScript calculator) still fails closed
(feature `"pdf-functiontype-{n}"`), as does a multi-input `/FunctionType 0` function (feature
`"pdf-function-multiinput"`) and an unsupported `/BitsPerSample` (feature
`"pdf-function-bitspersample-{n}"`). Phase 15 added `CCITTFaxDecode` (Group 4/T.6 MMR only) image
decoding: a non-negative `/K` (Group 3, feature `"pdf-ccittfax-group3"`), `/EndOfLine true`
(feature `"pdf-ccittfax-endofline"`), or a resolved `/ColorSpace` with more than 1 component
(feature `"pdf-ccittfax-colorspace"`) each still fail closed. Phase 18 widened `/FontFile3`
dispatch for `/Type1` and composite `/CIDFontType0` fonts to sniff the stream's own decoded bytes
for a recognized container shape; only bytes matching neither a recognized SFNT nor bare-CFF
shape still fail closed (feature `"pdf-font-fontfile3-unrecognized-shape"`). Each
currently-thrown `Codecs.UnsupportedImageFeatureException` carries a distinct,
descriptive `Feature` string so a caller (or this repository's own tests) can distinguish exactly
which unsupported construct was encountered — the complete current set is:
`pdf-encrypted-filter-{name}` (a non-`/Standard` security handler), `pdf-encrypted-cfm-{name}`
(an unsupported `/CF/StdCF/CFM`), `pdf-encrypted-crypt-filter-{name}` (an unsupported named
crypt filter), `pdf-encrypted-r-{revision}`,
`pdf-encrypted-v-{version}`, `pdf-encrypted-password-required` (a `null` password when a
non-empty one is genuinely required), `pdf-encrypted-incorrect-password`,
`pdf-encrypted-password-non-ascii` (an R2-R4 password outside ASCII 0-127),
`pdf-colorspace-{name}` (`Separation`/`DeviceN`/`CalGray`/`Lab`), `pdf-colorspace-Pattern`,
`pdf-colorspace-ICCBased`, `pdf-filter-{name}` (`JPXDecode` and any other unrecognized filter),
`pdf-tiff-predictor-bitdepth-{n}`, `pdf-image-bitdepth-{n}`, `pdf-pattern-not-declared`,
`pdf-pattern-type-{n}`, `pdf-pattern-tile-too-large`, `pdf-shading-type-{n}`,
`pdf-shading-colorspace-{family}`, `pdf-functiontype-{n}`, `pdf-function-multiinput`,
`pdf-function-bitspersample-{n}`, `pdf-font-subtype-{subtype}` (`MMType1` only),
`pdf-font-symbolic-not-embedded` (`Symbol`/`ZapfDingbats` descriptor flags without an embedded
font program), `pdf-font-encoding-{name}`, `pdf-font-type0-encoding-{name}` (a `/Type0`
`/Encoding` other than `/Identity-H`), `pdf-font-cidfonttype-{subtype}` (a descendant `/Subtype`
other than `/CIDFontType2`/`/CIDFontType0`), `pdf-font-fontfile3-unrecognized-shape`,
`pdf-font-tounicode-{operator}` and `pdf-font-tounicode-bfrange-array-destination` (an
unsupported `/ToUnicode` CMap construct), `pdf-text-render-mode-type3-clip` (a Type 3 glyph under clip mode),
`pdf-ccittfax-group3`, `pdf-ccittfax-endofline`, and `pdf-ccittfax-colorspace`. `/Annots`
(annotations) and AcroForms are simply not
processed at all — page rendering silently ignores `/Annots` rather than throwing — since this is
an unimplemented feature, not a fail-closed scope boundary. No other segregation is required at
the system level:
`CanvasNetPdf` contains exactly one unit, so this risk control is inherently contained within it
(IEC 62304 §5.3.3).

Beyond rejecting out-of-scope constructs, several decoders and interpreter loops also bound a
resource (decoded-output size, stack depth, or expansion factor) that an otherwise well-formed
but adversarially crafted document could otherwise drive unboundedly large, closing a class of
decompression-bomb/resource-exhaustion risks without rejecting any legitimate document; see
_Resource and Input Bounds_ in `PdfDocument`'s own unit design for the complete set of caps and
guards.

## Data Flow

**PDF open/parse path:**

1. **Input**: A PDF document (stream or file path)
2. **Validation**: `Open` rejects a null `stream`/`path` with `ArgumentNullException` and an
   empty/whitespace `path` with `ArgumentException`
3. **Processing**: Fully buffers the input into memory, tokenizes and parses its cross-reference
   data (classic table, cross-reference stream, object stream, and hybrid combinations, with a
   linear-scan fallback), checks the resolved trailer for an `/Encrypt` key, then traverses the
   catalog's page tree (`/Pages` → recursive `/Kids` → `/Type /Page` leaves), inheriting
   `/MediaBox`/`/Rotate` from the nearest ancestor that declares one and computing each page's
   already-rotated `PdfPageInfo`
4. **Output**: A new `PdfDocument` instance whose `PageCount`/`GetPageInfo` reflect the fully
   resolved page tree — parsed once, reused across every subsequent call on that instance

**PDF render path:**

1. **Input**: A `pageIndex` and a caller-requested output `width`/`height`
2. **Validation**: Rejects an out-of-range `pageIndex` with `ArgumentOutOfRangeException`
   (identically to `GetPageInfo`); a non-positive `width`/`height`, or a `width`/`height`
   exceeding `Surface.MaxDimension` (8192), propagates, unwrapped, as `Surface`'s own
   `ArgumentOutOfRangeException`
3. **Processing**: Builds the page's base current transformation matrix from its raw `/MediaBox`
   origin, effective `/Rotate`, and the requested `width`/`height`; resolves `/Contents` to fully
   decoded bytes (concatenating a multi-stream array with a space separator, and decoding through
   its full `/Filter` chain — `FlateDecode`, `LZWDecode`, `ASCII85Decode`, `ASCIIHexDecode`,
   `RunLengthDecode`, individually or composed); tokenizes and
   dispatches every recognized path-construction/painting, graphics-state, device-color,
   image-XObject, and text operator, silently skipping any other keyword — a text-showing
   operator (`Tj`/`'`/`"`/`TJ`) resolves each shown byte through the currently selected font's
   `/Encoding` to a Unicode codepoint, looks up its glyph outline/advance width via the resolved
   font (an embedded `Fonts.TrueTypeFont`, or, when none is embedded, an automatically
   substituted system/bundled `Fonts.SystemFontCatalog` fallback), and paints it through the
   composed text-rendering matrix
   exactly like any other filled path; throws `InvalidDataException` for malformed `/Contents` or
   a malformed recognized operator's operand count/type, and throws
   `Codecs.UnsupportedImageFeatureException` for a well-formed but unsupported color space,
   stream filter, pattern/shading shape, function type, or
   font subtype/encoding/symbolic-font-without-embedded-data/Type 3 glyph under a clip
   text-rendering mode (`pdf-text-render-mode-type3-clip`)
4. **Output**: A new `Canvas.Surface` of exactly the requested size, painted with the page's
   interpreted path geometry, any placed image XObjects and nested Form XObjects, any
   pattern/shading-filled paths, and any shown text (or fully transparent, when the page declares
   no `/Contents` at all)

## Design Constraints

- **Simplicity**: Minimal functionality kept easy to understand and extend
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms
- **One-time parse, reused across calls**: `Open` performs the full parse exactly once;
  `PageCount`/`GetPageInfo`/`Render` all read from that already-resolved state rather than
  re-parsing on every call — a design property a stateless (static-method-only) API could not
  express, and the direct motivation for `PdfDocument` being an instantiable, disposable class
  rather than a set of static methods like `SvgCodec`
- **Decode-only**: `PdfDocument` provides no encode/`Save` direction, matching every other
  decode-only codec in this repository

### Platform Support

The library targets the following frameworks, identical to the `CanvasNet` system it depends on,
enabling compatibility across modern, currently supported .NET runtimes:

| Target Framework | Runtime / Environment |
| ---------------- | --------------------- |
| `net8.0`         | .NET 8 LTS            |
| `net9.0`         | .NET 9                |
| `net10.0`        | .NET 10               |

The library is supported on the following operating systems:

- **Windows** — primary developer and CI platform
- **Linux** — CI/CD and containerized environments
- **macOS** — developer workstations using Apple platforms

Portability is achieved by restricting the implementation exclusively to Base Class Library (BCL)
APIs and the `CanvasNet` system's own public API, available across all target frameworks. No
platform-specific native interop, OS-specific APIs, or framework-version-specific features are
used.

### Integration Patterns

- **NuGet Packaging**: Standard .NET library packaging and distribution, referencing the
  `CanvasNet` package as an ordinary NuGet dependency
- **CI/CD Integration**: Automated build, test, and quality validation
- **Requirements Traceability**: All features linked to passing tests
