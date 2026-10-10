## PdfDocument

![CanvasNetPdf Structure](CanvasNetPdfView.svg)

<!-- cspell:ignore xref startxref endobj endstream ObjStm MediaBox unresolvable Trise -->
<!-- cspell:ignore CCITT reimplementation diffability bitstream Zapf Nonsymbolic cidfonttype fontfile -->
<!-- cspell:ignore Noto -->
<!-- cspell:ignore Segoe Dejavu Nimbus Consolas ttcf dogfooding LOCALAPPDATA -->
<!-- cspell:ignore beginbfchar endbfchar beginbfrange endbfrange codepoints tounicode bfrange -->
<!-- cspell:ignore begincodespacerange endcodespacerange findresource defineresource currentdict -->
<!-- cspell:ignore begincmap endcmap bfchar usecmap cidrange cidchar codespacerange -->
<!-- cspell:ignore functiontype bitspersample multiinput hival EOFB -->
<!-- cspell:ignore PDFium -->

<!-- cspell:ignore charsets -->
<!-- cspell:ignore bchar achar -->
<!-- cspell:ignore SASLprep -->

`PdfDocument` is distributed as the separate `DemaConsulting.CanvasNet.Pdf` NuGet package
(namespace `DemaConsulting.CanvasNet.Pdf`), which references the core
`DemaConsulting.CanvasNet` package.

The `PdfDocument` class is the sole software unit of the `CanvasNetPdf` system. Its dependencies
are limited to `CanvasNet`'s `Canvas` subsystem (`Surface`, `Rgba32`), `Codecs` subsystem
(`UnsupportedImageFeatureException`, and, as of Phase 3, `JpegCodec`), `Geometry` subsystem
(`PathBuilder`, `Path`), `Drawing` subsystem (`PathFiller`, `PathStroker`, `StrokeStyle`,
`FillRule`, `LineCap`, `LineJoin`), and, as of Phase 4, `Fonts` subsystem (`TrueTypeFont`, and, as
of Phase 6, `SystemFontCatalog`) — see the Dependencies section of _CanvasNetPdf System Design_
(`../canvas-net-pdf.md`). It provides
hand-rolled parsing of a PDF document's structure (cross-references, trailer, page tree), a
content-stream interpreter (`Render` tokenizes and executes a page's `/Contents` path-
construction/painting and graphics-state operators), real device color, a generalized stream-
filter pipeline, image XObjects (Phase 3), and, as of Phase 4, real embedded-TrueType-font text
rendering: `g`/`G`/`rg`/`RG`/`k`/`K`/`cs`/`CS`/`sc`/`SC`/`scn`/`SCN` set the actual fill/stroke
color a path paints with; a generalized `/Filter`/`/DecodeParms` pipeline (`FlateDecode` plus
PNG/TIFF predictor reversal) decodes any stream, not only a page's own `/Contents`; `Do` decodes
and composites a `/Subtype /Image` XObject (`DCTDecode` via `Codecs.JpegCodec`, `JPXDecode` via
`Codecs.Jpeg2000Codec`, or raw `DeviceGray`/`DeviceRGB`/`DeviceCMYK`/`Indexed` samples of
1/2/4/8/16 bits) through the current transformation matrix;
and `BT`/`ET`/`Tc`/`Tw`/`Tz`/`TL`/`Tf`/`Tr`/`Ts`/`Td`/`TD`/`Tm`/`T*`/`Tj`/`'`/`"`/`TJ` resolve a
simple (non-composite) TrueType font declared in the current page's `/Resources/Font`
dictionary, map each shown byte through that font's `/Encoding` to a Unicode codepoint, and paint
the resulting glyph outline (scaled/positioned by the composed text-rendering matrix) via
`Drawing.PathFiller.Fill` with the current fill color, exactly like any other filled path. As of
Phase 6 (this phase), a `/Subtype /TrueType` simple font with an embedded
`/FontDescriptor/FontFile2` still always wins, but a font with no embedded `/FontFile2` is no
longer an unconditional failure: it is instead substituted with the closest-matching font
actually installed on the host operating system, or - when nothing matches - a bundled Liberation
Sans/Serif/Mono font, fully automatically and silently (see _Font Resolution_ and _Font Fallback
Resolution_ below, and `Fonts.SystemFontCatalog`'s own unit design,
`../canvas-net/fonts/system-font-catalog.md`). As of Phase 9, a composite
`/Subtype /Type0`/`/Encoding /Identity-H` font naming a single `/CIDFontType2` descendant font
(with its own embedded `/FontDescriptor/FontFile2`) is also resolved and rendered end to end,
decoding each shown string as 2-byte-per-code (CID) values rather than 1-byte-per-code; as of
Phase 12 (this phase), a `/CIDFontType0` descendant font (with its own embedded, non-CID-keyed-
CFF, `/OpenType`-wrapped `/FontDescriptor/FontFile3`) is resolved identically (see _Composite Font
Resolution_ below). As of Phase B, a `/Subtype /Type1` simple font with an embedded
`/FontDescriptor/FontFile` (a classic PostScript Type 1 program) is also resolved and rendered,
exactly like a `/FontFile2`-embedded `/TrueType` font; as of Phase C (this phase), a `/Subtype
/Type1` simple font with an embedded `/FontDescriptor/FontFile3` whose own `/Subtype` is `Type1C`
(a bare, un-wrapped CFF program) is resolved identically (see _Type 1C Font Resolution_ below); as
of Phase D (this phase), a `/Subtype /Type3` font (whose glyphs are arbitrary content-stream
procedures rather than an outline/CFF program) is also resolved and rendered, via its own
dedicated resolution and glyph-painting path (see _Type 3 Font Resolution_/_Type 3 Glyph Painting_
below). As of Phase 16 (this phase), a document encrypted with the PDF "Standard" security
handler (`/Filter /Standard`) using RC4 (40 to 128-bit), AES-128 (`/CFM /AESV2`), or AES-256 using
the R5 or R6 (hardened hash) key derivation (`/CFM /AESV3`/`/R 5` or `/R 6`) and an empty user password is also opened and
rendered transparently, with every indirect object's strings and every stream's raw bytes
decrypted before any other parsing logic observes them (see _Encryption (Standard Security
Handler)_ below); every other encrypted-document shape (a non-`/Standard` security handler,
an `/R` other than 5/6, a non-`/StdCF` crypt filter, or a document that genuinely
requires a non-empty password) still fails closed exactly as before. As of Phase 18 (this phase),
both `LoadType1CFont` (simple `/Type1` fonts) and `LoadCidFontType0Font` (composite `CIDFontType0`
descendant fonts) resolve a `/FontFile3` stream by sniffing the stream's own decoded bytes for a
recognized SFNT container (`'OTTO'`/`'true'`/`1.0`/`'ttcf'`) or a structurally plausible bare CFF
header, rather than gating on the stream's declared `/Subtype` name: a bare, non-SFNT-wrapped CFF
program is now accepted on a _composite_ `/Type0` font regardless of whether its `/Subtype` reads
`/CIDFontType0C`, `/Type1C`, `/OpenType`, or is absent entirely, and an SFNT-wrapped (`'OTTO'`) CFF
program is likewise now accepted on a _simple_ `/Type1` font regardless of its declared `/Subtype`
— matching PDF 32000-1's own leniency around `/Subtype` versus the stream's actual byte container
shape, and real-world producers that emit one without exactly matching the other. Only a stream
whose bytes match neither recognized shape still fails closed with
`Codecs.UnsupportedImageFeatureException` (see _Type 1C Font Resolution_/_Composite Font
Resolution_ below).
**Phase 4 limitations (narrowed by Phase 6/9/12/B/C/D/18, see
above)**: CID-keyed CFF (`ROS`/`FDArray`/`FDSelect`), non-`/Identity-H` composite `/Encoding`s
(including `/Identity-V` and predefined CJK encodings), and `/MMType1` fonts remain
entirely unsupported and fail closed with `Codecs.UnsupportedImageFeatureException` (CID-keyed CFF
instead surfaces as `InvalidDataException` via `Fonts.CffTable.Parse`'s own existing rejection);
only the `/WinAnsiEncoding` and `/MacRomanEncoding` base encodings (plus `/Differences`) are
supported (an unrecognized base encoding also fails closed); text-rendering modes `0` (fill), `1`
(stroke), `2` (fill, then stroke), `3` (invisible), and the clip modes `4`-`7` are supported (a Type 3
glyph under a clip mode fails closed); and no additional stream filters were added for any of these phases.
**Phase 3 limitations** (narrowed by Phase 7/13 and the `/Pattern` color-space phase, see below):
there are no transparency groups, `/Mask` (stencil/color-key) image masks and `/Matte` are not
consulted, and the Phase 3 operator subset is otherwise limited as documented below. These remain
out of scope and are silently skipped (any other undefined keyword) or explicitly rejected
(unsupported color spaces/filters/fonts/encodings/render modes), per the operator/exception
taxonomy documented below. Since then the following have been added: shading/tiling pattern fills
(`/ShadingType 2`/`3`, `/PatternType 1`/`2` — see _`/Pattern` Color Space (Shading and Tiling
Patterns)_ below, reusing the `/FunctionType 0` sampled-function evaluator originally landed as
Phase 1 groundwork, alongside new `/FunctionType 2`/`3` support); the `LZWDecode`/`ASCII85Decode`/
`ASCIIHexDecode`/`RunLengthDecode` filters (Phase 7); `CCITTFaxDecode` — Group 4 (T.6 MMR) only —
(Phase 15, see below); and `JPXDecode` images and explicit `/SMask` soft masks (see _Image
XObjects_ below). As of Phase 13 (see below), `Do` on a `/Subtype /Form` XObject is no longer one
of the rejected constructs: it decodes and executes the Form's content stream, subject to its own
documented Phase 13 limitations (no `/BBox` clipping, no `/Group` transparency-group handling, and
a hard recursion-depth limit of 12).

### Purpose

`PdfDocument` lets callers open a PDF document exactly once (from a stream or a file path),
inspect its page count and each page's display size/rotation, and request a render of any given
page into a `Surface` of a caller-chosen pixel size. Unlike the static, no-instance-state codecs
in `CanvasNet.Codecs` and `SvgCodec`, `PdfDocument` is an instantiable, disposable class: parsing
a PDF document's cross-reference data and page tree is comparatively expensive, and a caller
typically wants to call `GetPageInfo`/`Render` for several pages of the _same_ document without
re-parsing it each time — a property a purely static API could not express.

### Data Model

- **`_buffer` (`byte[]`)** — the document's entire input, fully buffered in memory at `Open` time
  (regardless of whether the original `Stream` was seekable), since resolving an indirect object
  requires random access back into arbitrary earlier byte offsets.
- **`_xref` (`Dictionary<int, XrefEntry>`, private nested `XrefEntry`)** — the fully resolved
  cross-reference table (object number → either a direct byte offset, or a compressed
  stream-number/index-within-stream pair), merged across every classic/stream/hybrid section and
  `/Prev` link discovered during `Open`, or rebuilt via the linear-scan fallback. Object number
  alone is used as the lookup key (matching by generation number is not implemented — a
  documented Phase 1 simplification: real-world and fixture files overwhelmingly use generation
  0 for every object).
- **`_objectCache` (`Dictionary<int, PdfObject>`)** — memoizes each already-resolved indirect
  object, so a page referenced from multiple ancestors (or looked up repeatedly across several
  `GetPageInfo`/`Render` calls) is only ever parsed/decompressed once per `PdfDocument` instance.
- **`_pages` (`IReadOnlyList<PdfPageInfo>`)** — every page's already-rotated
  `PdfPageInfo`, computed once during `Open`'s page-tree traversal (see
  _PageTree Inheritance_ below), so `GetPageInfo` is an O(1) list lookup rather than a
  re-traversal.
- **`_disposed` (`bool`)** — mirrors `Canvas.Surface`'s own disposal field exactly: `false` until
  `Dispose()` is called, then permanently `true`; every other public member checks this first via
  `ObjectDisposedException.ThrowIf(_disposed, this)`.
- **`_pageDetails`**
  (`IReadOnlyList<(PdfObject Node, double X0, double Y0, double BoxWidth, double BoxHeight, PdfObject? Resources)>`)
  — the raw (non-rotated, non-swapped) leaf page node, its inherited `/MediaBox`'s origin and
  size, and its inherited `/Resources` dictionary (`PdfObject?`, `null` when no ancestor declares
  one — added in Phase 3, inherited exactly like `/MediaBox`/`/Rotate`: nearest declaring
  ancestor, never merged across ancestors), one entry per page in the same document order as
  `_pages`, populated in lock-step by `TraversePageTree` — `Render` needs the raw `/MediaBox`
  origin (`_pages`/`PdfPageInfo` only exposes the already-rotation-swapped display width/height)
  to correctly derive each page's base CTM (see `BuildBaseCtm` below), and needs `/Resources` to
  resolve a `cs`/`CS`/`Do` operator's named color-space/XObject resource.
- **`_gsStack` (`Stack<GraphicsState>`, nested `GraphicsState` class, `PdfDocument.GraphicsState.cs`)**
  — the `q`/`Q` graphics-state stack, and `_gs` (`GraphicsState`) — the current graphics state
  (current transformation matrix, line width/cap/join/miter-limit/dash pattern, `FillColor`/
  `StrokeColor`/`FillColorSpace`/`StrokeColorSpace` (Phase 3), and, as of Phase 4, persistent
  text state — `Font` (the currently selected `IResolvedFont?`), `FontSize`, `CharSpacing`,
  `WordSpacing`, `HorizontalScaling`, `Leading`, `RenderMode`, `TextRise`), both reset at the
  start of every `ExecuteContentStream` call. `GraphicsState.Clone()` performs a member-wise copy
  (the dash array reference and the resolved `Font` reference are shared, never mutated in
  place, so sharing either across a clone is safe) — text state is deliberately part of
  `GraphicsState`, not a separate field, since `q`/`Q` must save/restore it exactly like every
  other graphics-state parameter (the PDF specification's own rule).
- **`_resources` (`PdfObject?`, `PdfDocument.ContentStream.cs`, added in Phase 3)** — the current
  page's resolved `/Resources` dictionary (from `_pageDetails`), reset at the start of every
  `ExecuteContentStream` call; consulted by `cs`/`CS` (`/Resources/ColorSpace`) and `Do`
  (`/Resources/XObject`) to resolve a named resource.
- **`_pathBuilder` (`Geometry.PathBuilder`), `_currentPoint`/`_subpathStart` (`Vector2`,
  untransformed user-space), `_hasOpenSubpath` (`bool`)** (`PdfDocument.PathOps.cs`) — the path
  currently under construction by the path-construction operators, and the bookkeeping the `v`/
  `y` curve shorthands and `h`'s closepath need in the same untransformed user-space coordinates
  the next operator's own operands arrive in; `_hasOpenSubpath` mirrors `PathBuilder`'s own
  internal "can draw" state purely so a malformed "draw before move" content stream can be
  rejected with this class's own `InvalidDataException` convention rather than letting
  `PathBuilder`'s `InvalidOperationException` escape unwrapped. All three are reset at the start
  of every `ExecuteContentStream` call and cleared after every path-painting operator.
- **`_textMatrix`/`_lineMatrix` (`Matrix3x2`, `PdfDocument.Text.cs`, added in Phase 4)** — the
  current text-space-to-user-space matrix (`Tm`) and the line matrix `Tj`/`T*`/`TD` measure the
  next line's `Td` displacement from. Deliberately fields of `PdfDocument` itself, not
  `GraphicsState`: the PDF specification resets both to the identity matrix only at `BT`, and
  `q`/`Q` never save/restore them (unlike every other text-state parameter, which does live on
  `GraphicsState` — see `_gsStack` above) — a `q`/`Q` pair nested inside a `BT`/`ET` text object
  must not perturb the running text position. Both are reset once per `ExecuteContentStream`
  call and again by every `BT`.
- **`_fontCache` (`Dictionary<PdfObject, IResolvedFont>`, `PdfDocument.Fonts.cs`, added in Phase
  4, generalized to the `IResolvedFont` abstraction in Phase 9)** — memoizes each font
  dictionary's resolved `IResolvedFont` (either a `ResolvedSimpleFont` — embedded
  `Fonts.TrueTypeFont`, 256-entry code-to-Unicode-codepoint encoding map, per-code `/Widths` map,
  `/MissingWidth` — or, as of Phase 9, a `ResolvedCompositeFont` — descendant-font
    `Fonts.TrueTypeFont`, CID-to-glyph-index map, per-CID `/W` map, `/DW` default width, and, as of
    Phase 10, a `ToUnicode` code-to-Unicode-codepoint map resolved from the Type0 font dictionary's
    own `/ToUnicode` CMap stream when present (`null` otherwise) — resolved-but-unconsumed
    groundwork this phase, not yet read anywhere at rendering time) by the font
    dictionary `PdfObject`'s own reference identity, so `Tf` re-selecting the same font resource
  repeatedly within one `Render` call never re-decodes/re-parses the embedded `FontFile2` bytes
  more than once. Reset (cleared) at the start of every `ExecuteContentStream` call — scoped to a
  single `Render` call only, per this phase's documented caching contract (a later `Render` call
  always re-resolves every font from scratch, trading a small amount of redundant work across
  separate calls for never risking a stale reference into a different document's object graph).
- **`_surface` (`Canvas.Surface`, `PdfDocument.ContentStream.cs`)** — the destination surface
  every path-painting operator draws onto for the content stream currently being executed.
- **`PdfObject`/`PdfKind`** (internal, `PdfDocument.ObjectModel.cs`) — a small tagged-union
  representation of every PDF object kind (`Null`, `Boolean`, `Number`, `LiteralString`/
  `HexString`, `Name`, `Array`, `Dictionary`, `Stream`, `Reference`); not part of the public API
  surface, but exposed as `internal` (with `InternalsVisibleTo` the test assembly) so the
  tokenizer/object-model parsing logic can be unit tested directly against hand-written token
  sequences, not only indirectly through full fixture files. As of Phase 16, `PdfObject` also
  carries `ObjectNumber`/`Generation` (`int`, settable, default `-1`/`0`) — stamped onto a
  top-level indirect object's already-parsed value by `ParseIndirectObjectAt` after the fact,
  since they are not known until the surrounding `N G obj` header has been read — and `Bytes`
  (`byte[]`) changed from `private init` to settable, so an encrypted stream/string's ciphertext
  can be overwritten in place with its decrypted plaintext without rebuilding the object tree.
  **Design decision**: both deliberately break this class's otherwise-uniform `private init`
  immutability idiom; this is scoped intentionally to only ever be written by
  `ParseIndirectObjectAt`/`DecryptStringsInPlace` (see _Encryption (Standard Security Handler)_
  below), since `_objectCache` shares a single `PdfObject` instance across every caller and
  mutating it from anywhere else would alias-corrupt every other holder of the same reference.
- **`_encryptionKey` (`byte[]?`) / `_encryptionCipher` (`EncryptionCipher`, nested enum: `None`/
  `Rc4`/`Aes128`/`Aes256`)** (`PdfDocument.Encryption.cs`, added in Phase 16) — the resolved file
  encryption key and crypt method for an encrypted document whose empty user password
  successfully authenticated, or `null`/`None` for an unencrypted document (the common case,
  checked by `ParseIndirectObjectAt`/`GetStreamRawBytes` before ever attempting a decrypt). Set
  exactly once, by `InitializeEncryption`, during construction.
- **`PdfPageInfo`** (public, `readonly record struct`, `PdfPageInfo.cs`) — the resolved,
  already-rotated per-page result: `Width`/`Height` (already swapped when the effective
  `Rotation` is 90/270) and `Rotation` (the normalized `0`/`90`/`180`/`270` effective rotation).
  Documented inline here (per `sysml2-modeling.md`'s "documented inline" convention for small
  supporting value types), exactly as `Codecs.ImageInfo` is documented within its owning codecs'
  design docs rather than as its own separate unit. **Design decision**: `Rotation` is reported
  separately from the already-swapped `Width`/`Height`, even though `Width`/`Height` alone are
  sufficient to size a `Render` call, because a later phase's content-stream-to-device
  transformation matrix construction needs the effective rotation value itself, not just its
  width/height side effect.
- **`PdfRenderOptions`** (public, `sealed class`, `PdfRenderOptions.cs`) — a small, deliberately
  growable bag of page-rendering configuration, documented inline here for the same reason as
  `PdfPageInfo` above. Currently exposes a single `init`-only `BackgroundColor` property
  (`Canvas.Rgba32`, defaulting to opaque white) plus a `static readonly Default` instance used
  whenever a `Render` caller passes `null`. **Design decision**: a `sealed class` with `init`
  properties was chosen over a `record`/`record struct` specifically so future properties can be
  added without a breaking positional-argument/constructor change for any caller already using
  `new PdfRenderOptions { ... }` object-initializer syntax - no specific future property is
  planned or implied by this shape choice.

### Key Methods

- **`Open(Stream stream, string? password = null)`** / **`Open(string path, string? password =
  null)`** — `ArgumentNullException.ThrowIfNull` the `stream`/`path` first; `Open(string, ...)`
  additionally rejects an empty or whitespace-only `path` with `ArgumentException` (mirroring
  `PngCodec.Load(string)`'s exact null-then-empty check order, but checking
  `string.IsNullOrWhiteSpace` rather than only length, per this system's request-specific hard
  constraint). `Open(Stream, ...)` reads the stream fully into an in-memory buffer without ever
  calling `Dispose`/`Close` on the caller's stream. `Open(string, ...)` opens its own internal
  `FileStream`, reads it fully, and closes it (via `using`) before parsing — mirroring
  `PngCodec.Load(string)`'s open/consume/close pattern exactly — then calls the same shared
  private parse implementation the stream overload uses. Parsing runs the tokenizer, object
  model, cross-reference resolution (classic/stream/ObjStm/hybrid, with the linear-scan
  fallback), `/Encrypt` detection/decryption (see _Encryption (Standard Security Handler)_
  below, Phases 16/17), and page-tree traversal exactly once, producing a fully
  resolved `PdfDocument` instance. The optional `password` parameter (added in Phase 17) is
  consulted only when the resolved trailer declares an `/Encrypt` key: when `null` (the
  default), only the empty user password is authenticated, matching Phase 16's original
  encrypted-document support; when supplied, it is tried first as the user password and, if that
  fails, as the owner password, so a caller need not know in advance which of the two passwords
  it holds — see _Encryption (Standard Security Handler)_ below for the full authentication and
  fail-closed detail.
- **`PageCount` (get)** — disposed-check, then returns `_pages.Count`.
- **`GetPageInfo(int pageIndex)`** — disposed-check, then `ArgumentOutOfRangeException` for
  `pageIndex < 0 || pageIndex >= PageCount`, then returns `_pages[pageIndex]` (already computed
  during `Open`).
- **`Render(int pageIndex, int width, int height, PdfRenderOptions? options = null)`** —
  disposed-check and `pageIndex` range-check identical to `GetPageInfo`, then builds `new
  Surface(width, height)` and immediately clears it via `surface.Clear((options ??
  PdfRenderOptions.Default).BackgroundColor)` (opaque white when `options` is `null` or leaves
  `BackgroundColor` at its own default), resolves the page's leaf node, raw `/MediaBox` origin,
  and inherited `/Resources` via `ResolvePageDetails`, derives the page's base CTM via
  `BuildBaseCtm` (see _Content-Stream Interpreter_ below), resolves the page's `/Contents` bytes
  via `ResolvePageContentBytes`, executes them via `ExecuteContentStream` (passing the resolved
  `/Resources`), and returns the painted surface. `Surface`'s own constructor supplies the
  `width`/`height` `ArgumentOutOfRangeException`/`MaxDimension` contract; this is deliberately not
  duplicated here, and the caller-specified `width`/`height` is used exactly as given — it is
  never clamped to, or derived from, the page's own `/MediaBox` size. A caller that needs the
  previous fully transparent background back (for example to composite the result over
  something else itself) passes `new PdfRenderOptions { BackgroundColor = new(0, 0, 0, 0) }`.
- **`Render(int pageIndex, float dpi, PdfRenderOptions? options = null)`** — disposed-check and
  `pageIndex` range-check identical to `GetPageInfo`, then `ArgumentOutOfRangeException` for a
  non-positive or non-finite `dpi`, computes `width`/`height` by scaling `GetPageInfo(pageIndex)`'s
  own point-sized dimensions by `dpi / 72.0` (rounding away from zero), and forwards unchanged to
  `Render(int, int, int, PdfRenderOptions?)` above — `options` (and its `BackgroundColor`
  default/override semantics) is simply passed through as-is.
- **`Dispose()`** — idempotent (mirrors `Surface`'s exact pattern): `if (_disposed) return;` then
  `_disposed = true;`. No finalizer (only managed memory — the buffered bytes and parsed object
  model — is held).
- **Tokenizer (`PdfDocument.Tokenizer.cs`)** — a stateless-per-call lexer producing numbers,
  literal/hex strings, names, array/dictionary delimiters, comments (skipped), and keyword
  tokens, following the PDF specification's own whitespace/delimiter/regular character classes.
  Reused unmodified by the content-stream interpreter (a content stream tokenizes with exactly
  the same lexical rules as the rest of the document).
- **Object model parser (`PdfDocument.ObjectModel.cs`)** — a recursive-descent parser turning the
  tokenizer's output into a `PdfObject` tree (dictionaries, arrays, indirect references,
  streams), distinguishing a bare number followed by an object-definition header (`N G obj`)
  from an indirect reference (`N G R`) by one token of lookahead. Its `ParseArray`/`ParseValue`
  helpers are reused, unmodified, by `ExecuteContentStream` to parse an operand token that begins
  an array (content streams never legitimately need a content-stream-specific array parser).
- **Cross-reference resolution (`PdfDocument.Xref.cs`)** — parses a classic `xref`/`trailer`
  section, a `/Type /XRef` cross-reference stream (`/W`-width-driven binary field decoding), and
  a `/Type /ObjStm` compressed object stream (`FlateDecode`, `System.IO.Compression.DeflateStream`
  — mirroring `PngCodec.Zlib.cs`'s established zlib-header/DEFLATE-payload decompression pattern,
  except that the trailing Adler-32 checksum is deliberately not validated — several real-world
  PDF producers emit a wrong/placeholder checksum over an otherwise well-formed DEFLATE payload,
  and other mainstream PDF readers tolerate this), merges a hybrid `/XRefStm` link's entries with its paired classic
  section's own entries (a hybrid entry fills in an object number the classic section can only
  mark free; classic entries elsewhere are untouched), and walks a `/Prev` incremental-update
  chain (an earlier-processed, more recent section's entry always wins). Falls back to a linear
  scan of the buffered document for `N G obj` markers when `startxref` is absent/unreadable or
  the resolved trailer does not describe a valid `/Type /Catalog` root. **Documented
  simplification**: a cross-reference stream's own `/Length` must be a direct integer, not an
  indirect reference — resolving an indirect `/Length` would require the very xref table that
  stream is helping to build; regular content/object streams parsed after xref resolution
  completes may use an indirect `/Length` freely. `GetStreamDecodedBytes` (also defined here) is
  reused, unmodified, by `ResolvePageContentBytes` to decode a `/Contents` stream's bytes.
- **Page-tree traversal (`PdfDocument.PageTree.cs`)** — walks `trailer["/Root"]` → catalog
  `/Pages` → recursively nested `/Kids` arrays, producing an ordered, flattened page list. Each
  page's `/MediaBox`/`/Rotate` is inherited from the nearest ancestor that declares one
  (defaulting to US Letter `[0 0 612 792]` and rotation `0` when wholly undeclared anywhere in
  the ancestry), normalized (`/Rotate` modulo 360, rejecting a non-multiple-of-90 value with
  `InvalidDataException`), and used to compute each page's already-rotated `PdfPageInfo`
  (swapping `Width`/`Height` at 90/270). A visited-node set (keyed by object number) detects and
  rejects an unbounded `/Kids` reference cycle with `InvalidDataException`. `ResolveMediaBox`
  additionally returns the box's raw `(X0, Y0)` origin (not just its `Width`/`Height`), threaded
  through `TraversePageTree` into `_pageDetails`, since `BuildBaseCtm` needs that origin to
  correctly translate a `/MediaBox` that does not start at `(0, 0)`. As of Phase 3, each node's
  own `/Resources` (if declared) or the inherited value is likewise threaded through
  `TraversePageTree` into `_pageDetails` (nearest-declaring-ancestor inheritance, matching
  `/MediaBox`/`/Rotate`'s own pattern, per the PDF specification's own `/Resources` inheritance
  rule — never merged across ancestors).
  `ResolvePageDetails(int pageIndex)` is `Render`'s own entry point into this data.

### Encryption (Standard Security Handler)

Added in Phase 16, `PdfDocument.Encryption.cs` detects an encrypted document and derives its file
encryption key, so every subsequent indirect-object string and stream read can transparently
decrypt its bytes before any of this class's other parsing logic ever sees them. Phase 17 extends
this to accept an optional caller-supplied `password`, threaded from both `Open` overloads and the
private constructor into `InitializeEncryption`: when `password` is `null` (the default), behavior
is byte-for-byte unchanged from Phase 16 (empty-user-password authentication only). When a
non-`null` password is supplied, it is first tried as the **user password** (the same Algorithm
2/4/5 or 2.A authentication path, now parameterized on the real password's encoded/padded bytes
instead of always the empty-password padding constant); if that does not authenticate, the same
supplied string is tried as the **owner password** — ISO 32000-1 Algorithm 3 (R2-R4: recovers the
padded user password from `/O`, then re-derives and re-authenticates a candidate file key) or the
owner-password variant of ISO 32000-2 Algorithm 2.A (R5/R6: recovers the file key directly from
`/OE`). If neither attempt authenticates, `Codecs.UnsupportedImageFeatureException` is thrown with
the new `pdf-encrypted-incorrect-password` feature token (distinguishable from the null-password
`pdf-encrypted-password-required` token, which is unchanged). **Encoding scope boundary**: R2-R4
passwords are encoded via Latin-1 (≈ PDFDocEncoding for the ASCII range) — any character outside
ASCII 0-127 throws `pdf-encrypted-password-non-ascii` rather than silently deriving wrong key
material; R5 passwords are encoded via UTF-8 with no SASLprep/Unicode normalization (an
intentional, documented scope boundary that does not affect ordinary ASCII passwords). Both
encodings truncate to a maximum of 127 bytes before any hashing, per ISO 32000-1/2's own password
truncation rule.

- **`InitializeEncryption(PdfObject trailer, string? password = null)`** — replaces the previous,
  unconditional-throw `CheckForEncryption`. Returns immediately (leaving `_encryptionKey` `null`)
  when the trailer has no `/Encrypt` entry. Otherwise resolves (and thereby caches) the
  `/Encrypt` dictionary's own object, validates `/Filter` is `/Standard` (else
  `Codecs.UnsupportedImageFeatureException`, feature `pdf-encrypted-filter-{name}`), reads
  `/V`/`/R`/`/Length`/`/O`/`/U`/`/P`/`/EncryptMetadata` and the trailer's `/ID` first element,
  then dispatches on `/V`: `1`/`2` → RC4 via `InitializeRc4OrAesV2Encryption(…, password)`; `4` →
  validates `/CF/StdCF/CFM` is `/AESV2` (else feature `pdf-encrypted-cfm-{name}`) then the same
  RC4/AESV2 initialization path; `5` → accepts `/R 5` and `/R 6` (hardened hash) and rejects any
  other `/R` (feature `pdf-encrypted-r-{revision}`), validates `/CF/StdCF/CFM` is
  `/AESV3`, computes `passwordBytes` (empty, or `EncodeR5PasswordBytes(password)`), passes `revision` to
  `TryComputeFileKeyAlgorithm2A`, tried first and — when `password is not null` and that returns `null`
  — falls back to `TryComputeFileKeyAlgorithm2AOwnerPassword` (reading `/OE`), throwing the
  appropriate token (`pdf-encrypted-password-required` or `pdf-encrypted-incorrect-password`) if
  both fail; any other `/V` fails with feature `pdf-encrypted-v-{version}`. **Ordering invariant
  and cache invalidation**: resolving the
  `/Encrypt` dictionary happens, and is cached by `GetObject`, before `_encryptionKey` is ever
  set, so its own `/O`/`/U`/`/OE`/`/UE` strings are never mistakenly decrypted — this part is
  still relied on deliberately, instead of also carrying a redundant "is this the Encrypt
  dictionary's own object number" guard field, to avoid dead/speculative code (see
  `PdfDocument_Open_Encrypted_EncryptDictionaryStringsAreNeverDecrypted` for the regression test).
  However, by the time `InitializeEncryption` runs, _other_ objects may already be cached pre-key
  too: the trailer's own `/Root` Catalog (resolved by the constructor's own `IsValidCatalogRoot`
  check before `InitializeEncryption` ever runs) and, on the linear-scan fallback path, _every_
  object in the document (resolved by `BuildLinearScanFallback`'s own scan looking for a
  `/Type /Catalog` object) — none of those pre-key cache entries' own strings were decrypted
  either. Once `_encryptionKey` is established by whichever `/V` branch succeeds, this method
  calls its own `InvalidateObjectCacheExceptEncryptDictionary(encryptEntry)` helper, which clears
  `_objectCache` entirely except for the `/Encrypt` dictionary's own already-cached entry (kept
  exactly as originally cached, pre-key, preserving the invariant above), so every other
  previously-cached object is correctly re-resolved — and, this time, decrypted — the next time
  anything asks for it (`_xref`, the offset/compressed-entry table itself, is never cleared, only
  `_objectCache`, the parsed-value cache, so re-resolution is always possible). See
  `PdfDocument_Open_Encrypted_CatalogOwnStringIsDecrypted` for the regression test covering the
  Catalog case.
- **`PadPasswordBytes(byte[] passwordBytes)`** — pads/truncates arbitrary password bytes to
  exactly 32 bytes using `PasswordPadding`'s own bytes for the remainder; for an empty input this
  reproduces `PasswordPadding` verbatim, preserving byte-for-byte null-password behavior.
  **`EncodeR2R4PasswordBytes(string password)`** — Latin-1-encodes the password (≈ PDFDocEncoding
  for ASCII), throwing `Codecs.UnsupportedImageFeatureException` (feature
  `pdf-encrypted-password-non-ascii`) if any character is outside ASCII 0-127, then truncates the
  encoded bytes to 127. **`EncodeR5PasswordBytes(string password)`** — UTF-8-encodes the password
  (no SASLprep/Unicode normalization — an intentional scope boundary) then truncates to 127 bytes.
- **`ComputeFileKeyAlgorithm2(byte[] paddedPasswordBytes, …)`** (ISO 32000-1 Algorithm 2) — takes
  an already-padded 32-byte password (`PadPasswordBytes`'s own output for the empty/user-password
  case, or Algorithm 3's recovered bytes directly for the owner-password case, which must **not**
  be re-padded), MD5-hashes it with `/O`, `/P` (4-byte little-endian signed integer), the document
  ID, and (revision ≥ 4 with `/EncryptMetadata` explicitly `false`) four trailing `0xFF` bytes,
  then — for revision ≥ 3 — re-hashes the first `keyLengthBytes` of the digest 50 more times via
  the shared `Rehash50RoundsIfRevisionAtLeast3` helper; the file key is the first `keyLengthBytes`
  of the final digest (`/Length` in bits ÷ 8, defaulting to 5 bytes/40 bits, except `/V 1` which is
  always exactly 5 bytes regardless of `/Length`).
- **`TryAuthenticateUserPasswordAlgorithm45`** (ISO 32000-1 Algorithm 4 for revision 2, Algorithm
  5 for revision 3/4) — recomputes the expected `/U` value from the file key (Algorithm 2 alone
  never fails; only this comparison can detect a wrong password) and compares it against the
  document's actual `/U` (only the first 16 of 32 bytes for revision 3/4, since the trailing 16
  are producer-defined padding), returning `bool` instead of throwing (Algorithm 4/5 always
  hashes the literal 32-byte `PasswordPadding` constant, never the actual supplied password, per
  ISO 32000-1 §7.6.3.4/7.6.3.3 — this comparison is identical regardless of which password role is
  being tested).
- **`RecoverPaddedUserPasswordAlgorithm3(byte[] paddedOwnerPasswordBytes, byte[] oBytes, …)`** (ISO
  32000-1 Algorithm 3, decrypt direction, R2-R4 owner-password path only) — MD5-hashes the padded
  owner-password guess, re-hashes 50 rounds for revision ≥ 3 via the shared helper, then RC4-
  decrypts `/O`: a single pass for revision 2, or 20 rounds from round 19 down to round 0 (round 0
  using the unmodified owner key, rounds 1-19 using the key XORed with the round number) for
  revision 3/4 — returning the recovered 32-byte padded user password, which `InitializeRc4OrAesV2Encryption`
  feeds directly (without re-padding) into `ComputeFileKeyAlgorithm2` to derive a candidate file
  key, re-verified via `TryAuthenticateUserPasswordAlgorithm45`.
- **`ComputeObjectKeyAlgorithm1`** (ISO 32000-1 Algorithm 1, RC4/AESV2 only) — MD5-hashes the file
  key plus the object's 3-byte little-endian object number and 2-byte little-endian generation
  number (plus the 4 literal ASCII bytes `sAlT` for AESV2); the per-object key is the first
  `min(fileKeyLength + 5, 16)` bytes of that digest.
- **`ComputeHashAlgorithm2B(byte[] password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> udata, int revision)`**
  — the password hash used for both the validation hash and the intermediate key. R5: a single
  `SHA-256(password ‖ salt ‖ udata)`. R6 (ISO 32000-2 Algorithm 2.B): `K = SHA-256(input)`, then
  rounds of `K1 = (password ‖ K ‖ udata) × 64`, `E = AES-128-CBC(key = K[0..16], iv = K[16..32], K1)`
  with no padding, `K = SHA-256/384/512(E)` selected by the sum of `E`'s first 16 bytes mod 3; at
  least 64 rounds, then stop once the last byte of `E` is `<= round - 32`; the result is the
  first 32 bytes of `K`. The loop is capped (64 + 256 rounds) and throws `InvalidDataException`
  beyond that, failing closed.
- **`TryComputeFileKeyAlgorithm2A(byte[] passwordBytes, …, int revision)`** (ISO 32000-2 Algorithm 2.A, R5/R6 AESV3
  only, user-password path) — authenticates by comparing `ComputeHashAlgorithm2B(passwordBytes, /U`'s 8-byte
  validation salt`)` against `/U`'s own embedded 32-byte hash, returning `null` (instead of
  throwing) on a mismatch, otherwise AES-256-CBC-decrypts `/UE` (zero IV, no padding) using
  `ComputeHashAlgorithm2B(passwordBytes, /U`'s 8-byte key salt`)` as the key, yielding the 32-byte file
  encryption key directly — used as-is for every string/stream, with no further per-object
  derivation (unlike RC4/AESV2).
- **`TryComputeFileKeyAlgorithm2AOwnerPassword(byte[] passwordBytes, byte[] oBytes, byte[] oeBytes, byte[] uBytes, int revision)`**
  (owner-password variant of ISO 32000-2 Algorithm 2.A, R5/R6 AESV3 only) — hashes/encrypts over
  `passwordBytes ‖ salt ‖ U` where `U` is the **full 48-byte** `/U` value (not a sub-slice):
  compares `ComputeHashAlgorithm2B(passwordBytes, /O`'s 8-byte validation salt, fullU`)` against `/O`'s own
  32-byte hash, returning `null` on a mismatch, otherwise AES-256-CBC-decrypts `/OE` (not `/UE`,
  zero IV, no padding) using `ComputeHashAlgorithm2B(passwordBytes, /O`'s 8-byte key salt, fullU`)` as the key,
  yielding the 32-byte file encryption key directly.
- **`DecryptStreamBytes`/`DecryptStringsInPlace`** — dispatch on `_encryptionCipher`: RC4
  re-derives the per-object key and XORs; AES-128 derives the per-object key with the `sAlT`
  suffix, then AES-128-CBC/PKCS7-decrypts a leading-16-byte-IV-prefixed ciphertext; AES-256
  (R5/R6) uses `_encryptionKey` directly against the same IV-prefixed wire format. Called,
  respectively, by `GetStreamRawBytes` (before the generic `/Filter`/`/DecodeParms` pipeline
  runs) and `ParseIndirectObjectAt` (recursing every `PdfKind.String` found anywhere within a
  freshly-parsed top-level indirect object's value, never recursing into `PdfKind.Reference`
  nodes). A compressed (`/Type /ObjStm`-contained) object is never decrypted a second time: it is
  reached exclusively via `LoadCompressedObject`, which never calls `DecryptStringsInPlace` —
  its strings were already decrypted once, as part of its containing `/Type /ObjStm` stream's own
  raw bytes being decrypted by `GetStreamRawBytes` before decompression.
- **`Rc4Transform`** — a from-scratch, hand-rolled RC4 stream cipher (classic KSA/PRGA), since
  RC4 is not a built-in .NET primitive; symmetric, so the same implementation both encrypts and
  decrypts.

**Scope boundary**: only the `/Filter /Standard` security handler is supported, and only RC4
(`/V 1`/`/V 2`), AES-128 (`/V 4`/`/CFM /AESV2`), and AES-256 using the R5 or R6
(hardened hash) key derivation (`/V 5`/`/R 5` or `/R 6`/`/CFM /AESV3`) are supported. A caller-supplied password
(R2-R4: Latin-1/ASCII only;
R5/R6: UTF-8, no SASLprep normalization; both: 127-byte truncation) is tried as the user password
then the owner password, as described above. Every other shape (a non-`/Standard` filter, an `/R` other
than 5/6, a crypt filter other than the standard `/StdCF`, a document whose
user/owner password does not match the supplied (or default empty) password, or an R2-4 password
containing a non-ASCII character) fails closed with `Codecs.UnsupportedImageFeatureException` and
its own distinguishable `Feature` token.

### Content-Stream Interpreter

- **`BuildBaseCtm(x0, y0, boxWidth, boxHeight, rotation, width, height)`
  (`PdfDocument.PageTree.cs`)** — derives the page's base (pre-content-stream) current
  transformation matrix as `Translate(-x0, -y0) * RotationFlip(rotation, boxWidth, boxHeight) *
  Scale(scaleX, scaleY)` (row-vector composition: a user-space point is first translated by the
  raw `/MediaBox` origin, then rotated/flipped, then scaled to the requested render size), where:
  - `RotationFlip` is one of four literal matrices mapping PDF user-space (origin bottom-left,
    y-axis up) to un-scaled device pixel-space (origin top-left, y-axis down) for the page's
    effective rotation: `Rotate0 = (1,0,0,-1,0,boxHeight)`, `Rotate90 = (0,1,1,0,0,0)`,
    `Rotate180 = (-1,0,0,1,boxWidth,0)`, `Rotate270 = (0,-1,-1,0,boxHeight,boxWidth)` — confirmed
    by hand-checking each case maps `(0,0)`/`(boxWidth,boxHeight)` to the expected device corner.
  - `scaleX = width / displayWidth`, `scaleY = height / displayHeight`, where `displayWidth`/
    `displayHeight` are the already rotation-swapped dimensions (matching `PdfPageInfo.Width`/
    `Height`, and mirroring `SvgCodec.Load`'s own independent-X/Y requested-size scaling).
- **`ExecuteContentStream(byte[] contentBytes, Surface surface, Matrix3x2 baseCtm, PdfObject? resources)`
  (`PdfDocument.ContentStream.cs`)** — resets `_surface`/`_resources`/`_gsStack`/`_gs`/
  `_pathBuilder`/`_currentPoint`/`_subpathStart`/`_hasOpenSubpath`, then tokenizes
  `contentBytes` exactly like the rest of the document, accumulating operands (numbers, names,
  strings, arrays) until a `Keyword` token is reached, at which point `DispatchOperator` is
  called and the operand list is cleared — content-stream operators are always postfix (operands
  first, operator keyword last).
- **`DispatchOperator(string operatorName, List<PdfObject> operands)`
  (`PdfDocument.ContentStream.cs`)** — a single `switch` over every operator this phase
  implements (graphics-state, path-construction, path-painting, clipping-path, device-color,
  image-XObject, and the `sh` shading operator), silently doing nothing for any other keyword
  (text, `gs` (ExtGState), inline images, and any other operator not yet implemented). `sh` is
  dispatched to `OpPaintShading` (`PdfDocument.Patterns.Shading.cs`) — see _`/Pattern` Color Space
  (Shading and Tiling Patterns)_ below for what it paints and how it reuses the shading-pattern
  resolution/clip-mask mechanisms already documented there.
- **`ResolvePageContentBytes(PdfObject pageNode)` (`PdfDocument.ContentStream.cs`)** — resolves
  `/Contents`: a single stream is decoded directly via `GetStreamDecodedBytes`; an array of
  streams is decoded entry-by-entry and concatenated with a single space byte inserted between
  each entry (per the PDF specification's own requirement, so adjacent tokens from different
  streams can never merge); a page with no `/Contents` key returns an empty array (rendered as
  just its `PdfRenderOptions.BackgroundColor`-cleared surface, with no further geometry painted
  over it).
- **Graphics-state operators (`PdfDocument.GraphicsState.cs`)** — `OpPushGraphicsState`
  (`q`, pushes a clone of `_gs`), `OpPopGraphicsState` (`Q`, pops into `_gs`, tolerating an empty
  stack as a documented no-op leniency distinct from this class's strict operand-count/type
  checks), `OpConcatMatrix` (`cm`, right-multiplies the supplied matrix onto `_gs.CurrentTransform`
  per the specification: `CTM' = M × CTM`, so a point transforms as `P × M × CTM`, applying the
  newest `cm` first), and `OpSetLineWidth`/`OpSetLineCap`/`OpSetLineJoin`/`OpSetMiterLimit`/
  `OpSetDashPattern` (`w`/`J`/`j`/`M`/`d`, storing their operand(s) verbatim on `_gs`). Every
  handler validates its operand count/type via a shared `RequireNumbers` helper, throwing
  `InvalidDataException` on mismatch.
- **Path-construction operators (`PdfDocument.PathOps.cs`)** — `OpMoveTo`/`OpLineTo`/`OpCurveTo`/
  `OpCurveToV`/`OpCurveToY`/`OpClosePath`/`OpRectangle` (`m`/`l`/`c`/`v`/`y`/`h`/`re`), each
  transforming its own operands through the current CTM via a shared `Transform` helper before
  calling into `_pathBuilder` (matching `SvgCodec.PathRender.cs`'s established "bake the
  transform into each point before construction" pattern, rather than transforming the finished
  path afterward) — `v` reuses `_currentPoint` (transformed at draw time, not pre-transformed) as
  its first control point, and `y` reuses its own endpoint as its second control point, per the
  specification's documented shorthand semantics. `re` always ends its own subpath already closed
  (equivalent to `m l l l h`), so a subsequent draw operator without an intervening `m`/`re`
  throws `InvalidDataException` via a shared `RequireOpenSubpath` helper.
- **Path-painting operators (`PdfDocument.PathOps.cs`)** — a single shared `PaintCurrentPath`
  method parameterized by `fill`/`fillRule`/`stroke`/`closeFirst`, covering all ten operators
  (`f`/`F`/`f*`/`S`/`s`/`B`/`B*`/`b`/`b*`/`n`): fills via `Drawing.PathFiller.Fill` with the
  current graphics state's `FillColor` and the operator's documented fill rule; strokes via a
  shared `PaintStroke` helper (extracted so it is also reusable by `PdfDocument.Text.cs`'s
  `ShowGlyph`, for `Tr` modes `1`/`2` - see _Text Rendering_ below), which runs
  `Drawing.PathStroker.Stroke` then fills the resulting outline with `StrokeColor`, using a
  `Drawing.StrokeStyle` built from the current graphics state's line width/cap/join/miter-limit/
  dash pattern — every `StrokeStyle` field is passed explicitly (the PDF specification's own
  default miter limit is `10`, not `StrokeStyle`'s own C# default of `4`). `DeviceScale`
  (`sqrt(|det(CTM.Linear)|)`, the CTM with its translation zeroed) converts a user-space line
  width/dash length into device pixel-space, with a `MinimumDeviceLineWidth` of one device pixel
  implementing the specification's "a line width of 0 renders as the thinnest renderable line"
  rule. Every path-painting operator, including `n`, always clears the current path afterward
  (`_pathBuilder.Clear()`); the surrounding graphics state is entirely unaffected by that clear,
  so a subsequent path in the same content stream still sees the same CTM/stroke style/color.
  - **Hairline-stroke device-width clamp (regression fix note).** `DeviceLineWidth` originally
    applied the same `MinimumDeviceLineWidth` one-device-pixel floor unconditionally to every
    scaled line width, including a genuinely positive, small user-space width (e.g. `0.25 w`)
    that legitimately resolves to a sub-device-pixel stroke - forcing every such stroke to paint
    as a full-opacity, one-device-pixel-wide line indistinguishable from a literal `0 w` stroke,
    reported as "hairline strokes drawn heavier than PDFium". `DeviceLineWidth` now branches on
    whether the user-space `LineWidth` is itself (effectively, within a tiny
    `ZeroLineWidthTolerance` epsilon) zero: only that case still floors to the full-opacity
    `MinimumDeviceLineWidth` (the spec's "thinnest renderable line" rule, which has no magnitude
    of its own to preserve); a genuinely positive `LineWidth` instead floors only to
    `MinimumPositiveDeviceLineWidth` (a numerical-safety floor two orders of magnitude below one
    device pixel, confirmed sufficient because `Drawing.PathStroker`/`Drawing.PathFiller` require
    only a strictly-positive width to produce correct, non-degenerate outline/coverage geometry),
    letting `Drawing.PathFiller`'s antialiased scanline-coverage rasterizer paint the stroke's
    true, lighter sub-pixel coverage. Proven by three dedicated tests:
    `PdfDocumentTests.PdfDocument_PathOps_Stroke_ZeroWidth_RendersFullOpacityHairline` (the
    zero-width case is unaffected), `PdfDocument_PathOps_Stroke_ThinNonzeroWidth_RendersLighterThanWideStroke`
    (a small positive width now paints measurably lighter than a normal-width stroke, with a
    genuinely partially-transparent pixel), and `PdfDocument_PathOps_Stroke_SubEpsilonWidth_RendersVisibleLine`
    (a width below the numerical-safety floor still paints something visible rather than
    vanishing).
  - **Shared `StrokeOutliner` false-collapse investigation (regression fix note).** A separate
    investigation into a PPTX rendering regression (a `<a:noFill/>` shape's thin stroke outline
    rendering as a solid-filled interior instead of a thin ring) traced the root cause to the
    shared core `Drawing.StrokeOutliner`'s inner-ring collapse detector, which used a fixed
    absolute tolerance that false-positives when stroking a closed, curved contour tessellated
    finely relative to its own large coordinate magnitude (see
    `docs/design/canvas-net/drawing/path-stroker.md`'s "Inner-ring collapse" section). `PaintStroke`
    above shares this exact `Drawing.PathStroker.Stroke`/`StrokeOutliner` code path, so the
    investigation confirmed the same underlying defect is reproducible here too, with plain
    large-magnitude device-space coordinates independent of PPTX/EMU. In practice, `PaintStroke`
    strokes `path` _after_ it is already device-space-baked (unlike the PPTX caller that was
    stroking in native, pre-transform EMU space) and typical PDF device-space coordinates stay at
    a modest, page/pixel scale, so this defect was judged unlikely to manifest for ordinary PDF
    documents - no `PdfDocument` source change was required. The shared `StrokeOutliner` tolerance
    fix itself was still made (hardening every caller, including this one, against unusually large
    pages/zoom levels), and a dedicated regression test,
    `PdfDocumentTests.PdfDocument_PathOps_StrokeOnlyClosedBezierCircle_RendersThinRingNotSolidDisc`,
    confirms a stroke-only, unfilled closed Bezier-curve circle continues to render as a ring
    (not a solid disc) through this exact code path.
- **Clipping-path operators (`W`/`W*`, `PdfDocument.ContentStream.cs`/`PdfDocument.PathOps.cs`/
  `PdfDocument.GraphicsState.cs`, per PDF 32000-1 §8.5.4)** — `W`/`W*` do not themselves modify the
  active clip immediately; each only records a _pending_ clip fill rule
  (`_pendingClipFillRule`, nonzero for `W`, even-odd for `W*`) on a field scoped exactly like
  `_pathBuilder` (not part of `GraphicsState`, since it tracks in-progress-path state, not a
  graphics-state attribute that survives `q`/`Q`). `PaintCurrentPath` - the single shared dispatch
  point for every path-painting operator - applies the pending clip _after_ that operator's own
  fill/stroke painting (so, for example, `W f` still fills using the _previous_ clip, then installs
  the new one for whatever comes next; `W n` paints nothing but still installs the new clip): it
  rasterizes the just-painted path into a fresh `Drawing.ClipMask` (via `ClipMask.FromPath`, under
  the pending fill rule, bound to the destination surface's own pixel extent) and assigns
  `_gs.Clip = _gs.Clip is null ? newMask : _gs.Clip.Intersect(newMask)` - an _intersection_ with any
  already-active clip, never a wholesale replacement, per the specification's own wording ("the new
  clipping path ... shall be the intersection of the current clipping path and the newly
  constructed path"). `GraphicsState.Clip` (an `internal Drawing.ClipMask?`, `null` meaning "the
  entire output device", i.e. unclipped) is an ordinary graphics-state field, so `Clone()` copies
  it like any other - `ClipMask` is immutable and `Intersect` always returns a new instance, so
  sharing the reference across a `q`/`Q`/Form-XObject/Type3-glyph clone is always safe, with no
  extra save/restore logic required anywhere. The active clip is enforced by multiplying its own
  per-pixel antialiased coverage into every subsequent paint operation's own coverage: `_gs.Clip` is
  threaded as a `Drawing.ClipMask?` argument into every `Drawing.PathFiller.Fill` call site this
  class owns (ordinary fill/stroke/pattern fill in `PaintCurrentPath`/`PaintStroke`/
  `PaintPatternFill`, and glyph fill in `PdfDocument.Text.cs`'s `ShowGlyph`), and - since
  `PdfDocument.Images.cs`'s `CompositeImageOntoSurface` paints a decoded image XObject directly
  rather than through `Drawing.PathFiller` - that method instead multiplies each sampled source
  pixel's own alpha by `_gs.Clip?.GetCoverage(x, y)` before compositing it "over" the destination,
  the same technique applied at the one paint call site that does not go through `Drawing.PathFiller`.
  See `docs/design/canvas-net/drawing/path-filler.md`'s own _ClipMask_ subsection for the
  `Drawing`-layer rasterization/intersection mechanics.
- **Device color operators (`PdfDocument.Color.cs`, added in Phase 3; color-space model replaced
  in Phase 14)** — a private, immutable `PdfColorSpace` class (nested `Family` enum:
  `DeviceGray`/`DeviceRGB`/`DeviceCMYK`/`Indexed`, with shared `DeviceGray`/`DeviceRGB`/
  `DeviceCMYK` singleton instances and an `Indexed` factory carrying the base color space, the
  highest valid palette index (`Hival`), and the raw, un-normalized palette bytes) tracks
  `GraphicsState.FillColorSpace`/`StrokeColorSpace`. `OpSetGrayFill`/`OpSetGrayStroke` (`g`/`G`,
  1 operand), `OpSetRgbFill`/`OpSetRgbStroke` (`rg`/`RG`, 3 operands), and
  `OpSetCmykFill`/`OpSetCmykStroke` (`k`/`K`, 4 operands, `R = 255 * (1 - C) * (1 - K)` and the
  equivalent formulas for `G`/`B`) each clamp every component into `[0, 1]` (a documented
  leniency for a slightly out-of-spec producer's cosmetic overshoot — never rejected) before
  converting via a shared `ColorFromComponents` helper (also reused, unmodified, by
  `PdfDocument.Images.cs` for raw image samples). `OpSetColorSpaceFill`/`OpSetColorSpaceStroke`
  (`cs`/`CS`, exactly 1 name operand) resolve `DeviceGray`/`DeviceRGB`/`DeviceCMYK` directly, or
  else look the name up in the current page's `/Resources/ColorSpace` dictionary
  (`ResolveColorSpaceByName`), resetting color to opaque black (the PDF specification's own
  documented `cs`/`CS` reset rule) — an array-shaped color space is dispatched by
  `ResolveColorSpaceValue` to `ResolveIccBasedColorSpace` (`/ICCBased`) or
  `ResolveIndexedColorSpace` (`/Indexed`, Phase 14 — see below), or resolved directly to
  `DeviceRGB` for `[/CalRGB dict]` (ignoring its own `/WhitePoint`/`/Gamma`/`/Matrix` entries); any
  other resolved color space (`Separation`/`DeviceN`/`CalGray`/`Lab`, or an undeclared name) throws
  `Codecs.UnsupportedImageFeatureException`, naming the unsupported space; a `/Pattern` array
  form is dispatched instead to `PdfColorSpace.Pattern` (see _`/Pattern` Color Space (Shading and
  Tiling Patterns)_ below). `OpSetColorFill`/`OpSetColorStroke` (`sc`/`SC`/`scn`/`SCN`) require
  exactly `ComponentCount` numeric operands for the current color space when it is not
  `/Pattern`; a `/Pattern` color space instead dispatches `scn`/`SCN` (never `sc`/`SC`, which the
  specification never pairs with a pattern name) to the dedicated pattern-operand path described
  below. Every malformed operand count/type throws `InvalidDataException`, matching every other
  operator in this class.
  - **`/ICCBased`/`/Indexed` color-space resolution (`PdfDocument.Color.cs`, Phase 14 — this
    phase)** — `ResolveIccBasedColorSpace` validates a 2-element `[/ICCBased streamRef]` array
    whose 2nd element resolves to a stream (else `Codecs.UnsupportedImageFeatureException` — a
    malformed-but-still-rejected-as-unsupported case, not `InvalidDataException`, matching this
    color space's own "well-formed PDF, out-of-scope feature" posture), then prefers the stream's
    `/Alternate` entry when present and itself resolves (via a recursive `ResolveColorSpaceValue`
    call) to a supported color space, deliberately catching only
    `Codecs.UnsupportedImageFeatureException` from that recursive call to fall back to `/N`-based
    resolution (an `InvalidDataException` from a structurally malformed `/Alternate` is allowed to
    propagate unmodified — a different, non-swallowed failure class) — falling back maps the
    stream's `/N` (`1`/`3`/`4`) to `DeviceGray`/`DeviceRGB`/`DeviceCMYK`, else throws
    `Codecs.UnsupportedImageFeatureException`. `ResolveIndexedColorSpace` validates a 4-element
    `[/Indexed baseSpace hival lookup]` array (else `InvalidDataException` — a wrong array shape
    is structurally malformed PDF, unlike `/ICCBased`'s own convention), resolves `baseSpace`
    recursively via `ResolveColorSpaceValue` (propagating an unsupported base space's own
    `Codecs.UnsupportedImageFeatureException` unmodified), validates `/Hival` is a non-negative
    number, resolves `lookup` (a PDF string's raw bytes, or a stream decoded via
    `GetStreamDecodedBytes`), and validates the resolved palette has at least
    `(Hival + 1) * ComponentCount(baseSpace)` bytes (else `InvalidDataException` in each case).
    `ComponentCount` reports `1` for `Indexed` (the palette index itself); `ColorFromComponents`
    dispatches an `Indexed` color's single raw (not `[0, 1]`-normalized) index component to
    `IndexedToColor`, which rounds/clamps the index into `[0, Hival]` (an out-of-range index
    clamps to the nearest valid entry rather than being rejected — a documented leniency mirroring
    this phase's existing color-component clamping precedent), looks up that palette entry's raw
    bytes, normalizes each byte to `[0, 1]`, and recurses into the base color space's own
    `ColorFromComponents` — shared identically by both the `sc`/`scn` color operators and an image
    XObject's own `/ColorSpace` decoding (see below).
- **Stream filter pipeline (`PdfDocument.Filters.cs`, added in Phase 3; extended in Phase 7)** —
  generalizes the Phase 1/2 single-filter `GetStreamDecodedBytes` into an ordered
  `/Filter`/`/DecodeParms` pipeline (`ResolveFilterPipeline`), preserving the exact existing
  behavior (bare `FlateDecode`, no predictor) for cross-reference streams, object streams, and
  page `/Contents` — none of which regress. Each `FlateDecode` step decompresses via the existing
  `ZlibDecompress`; each `LZWDecode` step decompresses via `PdfDocument.Filters.Lzw.cs`'s
  `DecodeLzw` — a from-scratch PDF-variant LZW decoder (fixed 258-entry initial table, MSB-first
  variable 9-12 bit code packing, `/DecodeParms /EarlyChange`-controlled growth timing, mid-stream
  Clear-code reinitialization), deliberately reimplemented rather than reused from either
  `Codecs/Gif/GifCodec.Lzw.cs` (LSB-first bit packing, no `EarlyChange` concept, GIF-specific
  Clear/EOI code values) or `Codecs/Tiff/TiffCodec.Compression.cs` (MSB-first and a fixed
  early-change timing, but no `/EarlyChange` toggle) — both were read only as structural
  references, per the same "independent reimplementation, cited by name for diffability" pattern
  already used for the PNG predictor below. `ASCII85Decode`/`ASCIIHexDecode` decode via
  `PdfDocument.Filters.Ascii.cs` (base-85/hex-digit ASCII armoring, per ISO 32000-1/2
  §7.4.3/§7.4.2), and `RunLengthDecode` decodes via `PdfDocument.Filters.RunLength.cs` (the
  PackBits-style scheme of §7.4.5). After a `FlateDecode` or `LZWDecode` step only (never after
  `ASCII85Decode`/`ASCIIHexDecode`/`RunLengthDecode`, which the PDF specification never pairs with
  a predictor), when `/DecodeParms` declares `/Predictor > 1`, the pipeline reverses either a TIFF
  predictor (`/Predictor 2` — per-row horizontal-difference reversal, requiring
  `/BitsPerComponent 8`) or a PNG predictor (`/Predictor 10`-`15` — an independent
  reimplementation, named identically for diffability, of
  `Codecs/Png/PngCodec.Filtering.cs`'s `DefilterRow`/`Sub`/`Up`/`Average`/`Paeth`/Paeth-predictor
  algorithm, since those methods are `private` in a different assembly and cannot be reused
  directly).   Any other filter name (including `DCTDecode`/`CCITTFaxDecode`, which `PdfDocument.Images.cs`
  always detects and bypasses before calling this pipeline; `JPXDecode` (handled by the dedicated JPX
  path described below), and
  `JBIG2Decode`/`Crypt`, which remain out of scope) throws
  `Codecs.UnsupportedImageFeatureException`; a malformed `/Filter`/`/DecodeParms` shape, an
  unrecognized `/Predictor` value, or malformed bytes for any of the five supported filters
  (missing EOD marker, invalid/out-of-range code or character, truncated run, or an out-of-range
  ASCII85 group value) throws `InvalidDataException`.
- **Image XObjects (`PdfDocument.Images.cs`, added in Phase 3; extended in Phase 15)** —
  `OpDrawXObject` (`Do`)
  resolves a named XObject from the current page's `/Resources/XObject` dictionary; a
  `/Subtype /Form` XObject is dispatched to `OpDrawFormXObject` (see below, added in Phase 13); a
  `/Subtype /Image` XObject is decoded (`DecodeImageXObject`) and composited onto `_surface`
  through `_gs.CurrentTransform` (`CompositeImageOntoSurface`). `DecodeImageXObject` detects a
  bare `/Filter /DCTDecode` image and decodes its raw bytes directly via
  `Codecs.JpegCodec.Load(Stream)` (bypassing the general Flate+predictor pipeline entirely,
  trusting `JpegCodec`'s own decoded width/height over the PDF `/Width`/`/Height` as a documented
  leniency); a bare `/Filter /CCITTFaxDecode` image is likewise detected and dispatched to
  `DecodeCcittFaxImageXObject` (new in Phase 15), which resolves `/K`/`/Columns`/`/Rows`/
  `/BlackIs1`/`/EncodedByteAlign`/`/EndOfLine` from the (possibly absent) sole `/DecodeParms`
  entry, decodes via `PdfDocument.CcittFax.cs`'s `DecodeCcittFax` (see below), and — mirroring
  `DCTDecode`'s own leniency — trusts `/Columns`/the resolved `/Rows` (not `/Width`/`/Height`) as
  the authoritative surface dimensions; `CCITTFaxDecode` combined with any other filter throws
  `InvalidDataException`, mirroring the equivalent `DCTDecode` check; any other supported case
  decodes via `GetStreamDecodedBytes` and interprets the raw
  samples per `/ColorSpace` (device spaces, `/ICCBased`, and `/Indexed` — see above, reusing
  `ColorFromComponents`; an `/Indexed` sample is passed through `SamplesToColor` as a raw,
  un-normalized palette index rather than divided by `255.0` like every other color space) and
  `/BitsPerComponent` (`1`, `2`, `4`, `8` or `16` — `UnpackSamples` normalizes every depth to one
  byte per component: rows are padded to whole bytes, non-`/Indexed` samples are scaled to 0-255,
  16-bit samples keep their high byte, `/Indexed` samples stay raw palette indices and may not be
  16-bit; a short sample buffer throws `InvalidDataException`; any other depth throws
  `Codecs.UnsupportedImageFeatureException`). A bare `/Filter /JPXDecode` image is detected and
  dispatched to `DecodeJpxImageXObject`, which decodes the stream via
  `Codecs.Jpeg2000Codec.Decode(byte[], Jpeg2000DecoderLimits)` with explicit limits of
  `Surface.MaxDimension` in each dimension, checked by the codec while it parses the headers and so
  before any plane is allocated (trusting the decoder's dimensions over `/Width`/`/Height`, like
  `DCTDecode`; every `Jpeg2000Codec` failure, including a limit violation, surfaces as the
  existing `InvalidDataException`/`UnsupportedImageFeatureException` conventions);
  `JPXDecode` combined with any other filter
  throws `InvalidDataException`. When `/ColorSpace` is absent the JP2's own color space is used
  (gray, sRGB, CMYK, or by channel count; five or more channels need an explicit `/ColorSpace`
  and otherwise throw `UnsupportedImageFeatureException`); when present, `/ColorSpace`
  (`DeviceGray`/`DeviceRGB`/`DeviceCMYK`, `/Indexed`, `/ICCBased` by component count; `/Pattern`
  throws `InvalidDataException`) overrides it
  and its component count must equal the decoded color channel count
  (`InvalidDataException` otherwise); an `/Indexed` override uses the decoded 8-bit samples as
  indices (non-8-bit source samples and JP2 data with its own palette throw
  `UnsupportedImageFeatureException`). An optional `/Decode` array is applied for **every**
  encoding (raw, `CCITTFaxDecode`, `JPXDecode`, and `/SMask` images) by the shared
  `ResolveDecodeArray` (validation; an identity array resolves to none) and `ApplyDecodeArray`
  (per-component linear remap of the samples before the color-space conversion; for `/Indexed`
  the range is in palette-index units over `0..2^bits-1`); a
  wrong-length or non-numeric array throws `InvalidDataException`. A `DCTDecode` image is decoded
  by `JpegCodec` to exactly its 1 (gray) or 3 (RGB) component samples, so `ApplyDecodeToSurface`
  applies the same `ApplyDecodeArray` remap to them exactly; 4-component (CMYK/YCCK) JPEGs are
  rejected outright by `JpegCodec` (`InvalidDataException`), so a `/Decode` is never silently ignored.
  `/SMaskInData` selects how the codestream's opacity channel is used: `0` ignores it, `1` applies
  it as straight alpha, `2` un-premultiplies the component samples (`UnpremultiplySamples`,
  before `/Decode` and before the color-space conversion, since e.g. CMYK to RGB is not linear in
  the premultiplied values; an `/Indexed` space throws `UnsupportedImageFeatureException`,
  `pdf-jpx-smaskindata-indexed`) and then applies it; other values throw
  `InvalidDataException`. An explicit `/SMask` image (any filter, including `JPXDecode`; decoded
  through the same `DecodeImageSamples` path without nested masks, and required to be a
  single-component non-`/Indexed` image, else `InvalidDataException`) is nearest-neighbor resampled
  to the base image and its luminance multiplies the base alpha in place on the base surface
  (`ApplySoftMask`), taking
  precedence over `/SMaskInData`; `/Mask` and `/Matte` are not consulted. Inline images (`BI`) are
  not supported at all, which satisfies the PDF rule that `JPXDecode` is not permitted inline.
  `CompositeImageOntoSurface` inverts the CTM (a non-invertible/degenerate CTM silently paints
  nothing), computes the device-space bounding box of the transformed unit square, and for every
  destination pixel in that box nearest-neighbor-samples the source image (`row = floor((1 - v) *
  image.Height)`, since image sample row `0` is the _top_ of the unit square per the PDF
  specification's image-space convention, the opposite of user-space's y-up convention) — no
  bilinear interpolation, a documented Phase 3 simplification consistent with Phase 2's own
  stroke-width simplification precedent. Each sampled source pixel is alpha-blended "over" the
  existing destination pixel via the shared internal `Canvas.Rgba32.CompositeOver(Rgba32, Rgba32)`
  helper (standard Porter-Duff "over" alpha compositing — straight/unassociated alpha in and out,
  `outA = fgA + bgA * (1 - fgA)`, each color channel `outC = (fgC * fgA + bgC * bgA * (1 - fgA)) /
  outA` when `outA != 0` else `0`, round-half-away-from-zero, clamped to `[0, 255]`) rather than
  overwritten outright — a documented fix (an image XObject is opaque unless it carries an `/SMask`
  or a JPX `/SMaskInData` alpha channel, so this matters for those images and for this same
  helper's shared reuse by
  `DemaConsulting.CanvasNet.Pptx`'s own `PaintPicture`, which does sample genuinely non-opaque
  source pixels today — see that package's own design documentation).
- **CCITT Group 4 fax decoding (`PdfDocument.CcittFax.cs`, added in Phase 15)** — a from-scratch
  ITU-T T.6 decoder, implementing two-dimensional MMR coding only (`/K` must be negative;
  non-negative `/K`, i.e. Group 3, and `/EndOfLine true` are rejected up front by the top-level
  `DecodeCcittFax` entry point with `Codecs.UnsupportedImageFeatureException`, before any bits are
  read). `CcittBitReader` (nested, `internal` rather than `private` specifically so the test
  project's `InternalsVisibleTo` can exercise it directly for a table self-consistency test — see
  verification) is an MSB-first bit reader with an `AlignToByte` method for `/EncodedByteAlign`.
  `ModeCodeTable` holds the nine two-dimensional mode codes (Pass `0001`, Horizontal `001`,
  vertical `V0` `1`/`VR1` `011`/`VL1` `010`/`VR2` `000011`/`VL2` `000010`/`VR3` `0000011`/`VL3`
  `0000010`) as a prefix-free `(bit length, code value) → CcittMode` lookup, read bit-by-bit via
  `ReadMode` (safe because no valid mode code is a bit-prefix of another, so the first table match
  at the shortest accumulated length is always correct — the same reasoning applies to the
  run-length tables below). `ModeCodeTable`/`CcittMode`/`ReadMode` are themselves `internal`
  rather than `private`, for the same cross-assembly table self-consistency test reason as
  `CcittBitReader` above. `WhiteCodeTable`/`BlackCodeTable` (`internal`, for the same
  cross-assembly test reason) are built by `BuildRunLengthCodeTable` from literal
  (run length, code bits, code length) triples transcribed from the ITU-T T.4 Modified Huffman
  terminating-code (`0`-`63`) and makeup-code (`64`-`1728`, plus the extended makeup codes
  `1792`-`2560` shared identically between the White and Black tables) tables, cross-checked
  against libtiff's long-trusted, independently maintained `t4.h` table values (used only as
  transcribed numeric facts from the published ITU-T recommendation, not as copied code);
  `ReadVariableLengthCode` (`internal`) reads one such code bit-by-bit, and `ReadRun` chains zero
  or more makeup codes (run `≥ 64`) followed by exactly one terminating code (run `< 64`) to
  assemble a full run length, since a single run can exceed the largest single code's value.
  `DecodeCcittRow` implements the per-row two-dimensional reference-line decode loop: starting
  from an imaginary all-white line for row `0` (so the first row's changing-element lookups
  default to `Columns`), it tracks the coding position `a0` (`-1` before the first element) and
  current color, reads one mode code per coding element via `ReadMode`, computes the reference
  line's changing elements `(b1, b2)` via `FindB1B2` (the first element on the reference line past
  `a0` with color opposite the current coding color, and the one after it), and branches: Pass
  mode advances `a0` to `b2` without recording a changing element; Horizontal mode reads two
  run-length codes (current color, then the opposite) via `ReadRun`/`ReadVariableLengthCode` and
  records two changing elements; each vertical mode (`V0`/`VR1`-`VR3`/`VL1`-`VL3`) computes the
  new changing element as `b1 + delta` (`VerticalDelta`, `delta` in `-3`..`+3`), records one
  changing element, and toggles color. `PackRow`/`SetBitRange` assemble each row's changing
  elements into packed-1-bit-per-pixel bytes of exactly `(Columns + 7) / 8` bytes — the same
  `rowBytes` convention `ApplyTiffPredictor`/`ApplyPngPredictor` already use — honoring
  `/BlackIs1` (default `false`: packed bit `1` means black, per ISO 32000-1/2 Table 11, the
  opposite of this library's black`=0`/white`=max` `DeviceGray` convention, so the packed-bit
  polarity is deliberately inverted from `DeviceGray` and only normalized back during expansion);
  `ExpandPackedBitsToGrayBytes` then expands each packed row into one 8-bit gray byte (`0` or
  `255`) per pixel for the shared `/ColorSpace` pipeline (see above) to consume identically to
  every other image XObject's raw samples. A malformed/truncated stream (an unrecognized mode
  code, a changing-element search that runs past `Columns`, or running out of bits mid-row) throws
  `InvalidDataException` rather than producing a corrupt or out-of-bounds row.
- **Form XObjects (`PdfDocument.Images.cs`, added in Phase 13)** — `OpDrawFormXObject` checks
  `_formNestingDepth` against `MaxFormNestingDepth` (`12`, throwing `InvalidDataException` when
  reached), reads the Form's optional `/Matrix` via `ReadFormMatrix` (identity when absent,
  `InvalidDataException` for a present-but-malformed array) and concatenates it into the CTM
  using exactly the same left-multiply convention `OpConcatMatrix` uses for `cm`
  (`formMatrix * _gs.CurrentTransform`), resolves the Form's own `/Resources` when present (else
  reuses the invoking stream's current `_resources` unchanged), decodes the Form's stream bytes
  via the same `GetStreamDecodedBytes` pipeline every other stream uses, and re-enters
  `ExecuteOperators` directly (not `ExecuteContentStream`) against a cloned graphics state and a
  fresh, empty `_gsStack` — so an unbalanced `q`/`Q` inside the Form can never touch the invoking
  stream's own saved states. A `try`/`finally` around the nested call unconditionally restores the
  invoking stream's own `_resources`/`_gs`/`_gsStack` (success or exception), implementing an
  implicit `q` ... `Q` bracketing around the Form's own CTM/color/font-selection mutations, while
  deliberately leaving `_pathBuilder`/`_currentPoint`/`_fontCache` (not part of the PDF graphics-
  state stack) untouched, so path-painting/surface side effects performed by the Form's content
  persist exactly like any other painting operator's. No `/BBox` clipping and no `/Group`
  (transparency group) handling is performed — a documented Phase 13 limitation.
- **Font resolution (`PdfDocument.Fonts.cs`, added in Phase 4, fallback branch rewritten in
  Phase 6, dispatch generalized to `IResolvedFont` in Phase 9, widened to `/Type1` in Phase B,
  widened to `/Type1` + bare `/FontFile3` Type1C/CFF in Phase C, widened to `/Type3` in Phase D)**
  — `ResolveFont(PdfObject fontResource)` looks up (and caches, via `_fontCache`) an
  `IResolvedFont`. `BuildResolvedFont` dispatches on `/Subtype`: `/TrueType` and `/Type1` both
  build a `ResolvedSimpleFont` via `BuildResolvedSimpleFont`; `/Type0` builds a
  `ResolvedCompositeFont` via `BuildResolvedCompositeFont` (see _Composite Font Resolution_
  below); `/Type3` builds a `ResolvedType3Font` via `BuildResolvedType3Font` (see _Type 3 Font
  Resolution_ below); any other `/Subtype` (`/MMType1`) throws
  `Codecs.UnsupportedImageFeatureException` naming the rejected subtype. `IResolvedFont` exposes
  `Font` (the underlying `Fonts.TrueTypeFont`, nullable as of Phase D since a `ResolvedType3Font`
  has no such outline-glyph font program at all - `ShowGlyph` branches on the concrete resolved-
  font type before ever consulting this property, so the nullability is invisible to every other
  resolved-font kind's own non-nullable explicit interface implementation), `CodeByteWidth` (`1`
  for a simple font, `2` for a composite `/Identity-H` font - consulted by `ShowText`'s code-decoding loop), and
  `Resolve(int code)` (returning the code's glyph index and text-space advance width in one call
  - the single entry point `ShowGlyph` uses regardless of which concrete implementation is
  active). `BuildResolvedSimpleFont(PdfObject fontDict, string? subtype)` requires
  `/FontDescriptor`, then checks embedded-font keys in priority order: when `/FontFile2` resolves
  to a stream, that (TrueType-outline) embedded font always wins - decoded via the same
  `GetStreamDecodedBytes` every other stream in this class uses, then loaded via
  `Fonts.TrueTypeFont.Load(new MemoryStream(decodedBytes))`; else, when `/FontFile` resolves to a
  stream, `LoadType1Font` loads the embedded classic PostScript Type 1 program (see _Type 1 Font
  Resolution_ below); else, when `subtype == "Type1"` and `/FontFile3` is present (with neither
  `/FontFile` nor `/FontFile2`), `LoadType1CFont` loads the embedded bare Type1C/CFF program (see
  _Type 1C Font Resolution_ below); only when none of those apply does `BuildResolvedSimpleFont`
  call `ResolveFallbackFont` (see _Font Fallback Resolution_ immediately below) instead of failing
  closed - reachable for `/Type1` fonts exactly as it already was for `/TrueType` fonts. Before
  dispatching on those embedded-font keys, `BuildResolvedSimpleFont` calls
  `BuildEmbeddedFontGlyphNameMap(fontDict.Get("Encoding"))` - deliberately ahead of (not after,
  as in every phase before this one) loading any embedded font program - to build a
  per-font-dictionary enriched codepoint-to-glyph-name map: it starts from the generic
  `CodepointToStandardGlyphName` reverse map (below), then, for this font dictionary's own
  `/Encoding/Differences` array (if any), overwrites the entry for each `(code, name)` pair's
  resolved codepoint with that pair's own literal declared name, whenever `name` itself resolves
  to a codepoint via `TryResolveGlyphNameToCodepoint` (below) - the document's own literal
  declared glyph name wins over the generic guess for that specific codepoint, since an
  embedded/subsetted font's own charset is more likely to spell a glyph the way its own
  `/Differences` array names it than the way a generic, document-independent reverse-name guess
  does (and some codepoints, for example U+03BC via `/uni03BC`, have no entry in the generic map
  at all). This enriched map (not the raw `CodepointToStandardGlyphName` map) is what
  `LoadType1Font`/`LoadType1CFont` now receive, below.
  `ResolveEncoding` builds a full
  256-entry `int[]` code-to-Unicode-codepoint map: `ApplyBaseEncoding` seeds it from one of three
  hand-transcribed 256-entry tables (`WinAnsiEncodingTable`/`MacRomanEncodingTable`/
  `StandardEncodingTable`, the PDF specification's own Appendix D tables), defaulting to
  `/WinAnsiEncoding` when `/Encoding` is absent entirely and throwing
  `Codecs.UnsupportedImageFeatureException` for any other named base encoding
  (`/PDFDocEncoding`/anything else); `ApplyDifferences` then applies
  an `/Encoding/Differences` array's `code1 name1 name2 ... code2 name1 ...` run-length overrides,
  resolving each glyph name via `TryResolveGlyphNameToCodepoint` - first consulting
  `StandardGlyphNames` (a ~240-entry Adobe Glyph List subset
  covering common ASCII/Latin-1 names), then, for a name that subset does not itself cover,
  falling back to the Adobe Glyph List's own generic `uniXXXX` (exactly four uppercase hex
  digits)/`uXXXX`/`uXXXXX`/`uXXXXXX` (four to six uppercase hex digits) hex-codepoint naming
  convention - an unrecognized glyph name (one neither resolution reaches) is tolerated (that one
  code is
  simply left at whatever the base encoding already assigned it, rather than rejecting the whole
  document), while a `/Differences` array beginning with a glyph name before any starting code
  number still throws `InvalidDataException`. A reverse of `StandardGlyphNames`
  (`CodepointToStandardGlyphName`, first-wins on
  collision) is built once and used by `BuildEmbeddedFontGlyphNameMap` (above) as the generic
  starting point for the enriched, per-font-dictionary map both `LoadType1Font` (as
  `Fonts.TrueTypeFont.LoadType1`'s
  `codepointToGlyphName` argument) and `LoadType1CFont` (as `Fonts.TrueTypeFont.LoadType1C`'s
  identical argument) now receive - a document's own `/Differences`-declared literal glyph names
  can therefore influence which name wins for a given codepoint, per font dictionary, rather than
  every embedded font always receiving the exact same generic vocabulary regardless of its own
  `/Encoding`.
  `ResolveWidths` builds a sparse `code -> width`
  (`/1000`-scaled) map from `/FirstChar`/`/Widths` (missing/malformed entries silently omitted,
  not rejected), plus `/FontDescriptor/MissingWidth` (defaulting to `0`, the specification's own
  documented default) as the fallback for any code absent from that map.
- **Type 1 font resolution (`PdfDocument.Fonts.Type1.cs`, added in Phase B)** —
  `LoadType1Font(PdfObject descriptor, IReadOnlyDictionary<int, string> codepointToGlyphName)`
  is `BuildResolvedSimpleFont`'s `/FontFile` loader
  counterpart to `LoadCidFontType2Font`/`LoadCidFontType0Font` (see _Composite Font Resolution_
  below): it requires `/FontDescriptor/FontFile` to resolve to a stream
  (`InvalidDataException` otherwise), then reads that stream's own `/Length1`
  (cleartext-segment byte count) and `/Length2` (`eexec`-encrypted-segment byte count) entries -
  deliberately read from the `/FontFile` stream dictionary itself, never from the descriptor that
  references it, per PDF 32000-1 §9.9 - throwing `InvalidDataException` when either is missing or
  does not resolve to a number. The stream is decoded via the same `GetStreamDecodedBytes` every
  other embedded font stream in this class uses, then loaded via
  `Fonts.TrueTypeFont.LoadType1(new MemoryStream(decodedBytes), length1, length2,
  codepointToGlyphName)` - `codepointToGlyphName` being `BuildResolvedSimpleFont`'s own
  per-font-dictionary enriched map (above), not necessarily `CodepointToStandardGlyphName`
  itself - any exception the core `Fonts` layer itself throws for a
  malformed `eexec`-encrypted segment, an unparsable `/CharStrings`/`/Subrs` dictionary, or a
  rejected `seac` charstring propagates uncaught, consistent with this class's "embedded fonts
  fail closed on any embedded-font problem, no fallback" convention (matching
  `LoadCidFontType0Font`'s own documented precedent). **Non-Goals**: a `seac`-based
  accented-composite charstring is rejected by the core `Fonts` layer itself (not
  re-implemented or caught here); a non-1000-unit-em `/FontMatrix` is not read or honored at all
  (a 1000-unit em is assumed, matching every other font format this class resolves); and the
  Type 1 program's own built-in `/Encoding` array (if any) embedded in the font program itself is
  never consulted - only the PDF font dictionary's own `/Encoding` entry, resolved via
  `ResolveEncoding` above, determines which glyph a shown code selects.
- **Type 1C font resolution (`PdfDocument.Fonts.Type1.cs`, added in Phase C; shape-sniffing
  dispatch added in Phase 18)** —
  `LoadType1CFont(PdfObject descriptor, IReadOnlyDictionary<int, string> codepointToGlyphName)`
  is
  `BuildResolvedSimpleFont`'s `/FontFile3` loader counterpart (reached only when `/FontFile` is
  absent, per the priority order above), mirroring `LoadCidFontType0Font`'s own shape-sniffing
  precedent (see _Composite Font Resolution_ below): it requires `/FontDescriptor/FontFile3` to
  resolve to a stream (`InvalidDataException` otherwise), decodes it via the same
  `GetStreamDecodedBytes` every other embedded font stream in this class uses, then sniffs the
  decoded bytes themselves - via the shared `SniffFontFile3Shape` helper
  (`PdfDocument.Fonts.cs`) - for a recognized SFNT container or a structurally plausible bare CFF
  header, never consulting the stream's own declared `/Subtype` name for this decision at all (it
  is read only for inclusion in the exception message below). An SFNT-wrapped (for example
  `'OTTO'`) CFF program is loaded via `Fonts.TrueTypeFont.Load(new MemoryStream(decodedBytes))`; a
  bare, non-SFNT-wrapped CFF program is loaded via
  `Fonts.TrueTypeFont.LoadType1C(new MemoryStream(decodedBytes), codepointToGlyphName)` -
  `codepointToGlyphName` being the same per-font-dictionary enriched map `LoadType1Font` receives
  (above), not necessarily `CodepointToStandardGlyphName` itself - either way, a declared
  `/Subtype` of `Type1C`, `OpenType`, some other name, or no `/Subtype` key
  at all is accepted identically, as long as the bytes themselves match one of the two recognized
  shapes. Only bytes matching neither shape are rejected, with
  `Codecs.UnsupportedImageFeatureException` naming the stream's declared `/Subtype` (or "none") in
  its message. Any exception the core `Fonts` layer itself throws for malformed CFF table data,
  including a CID-keyed (`ROS`-declaring) CFF program, propagates uncaught, consistent with this
  class's "embedded fonts fail closed on any embedded-font problem, no fallback" convention.
  **Non-Goals**:
  unlike `LoadType1Font`'s classic Type 1 `seac` operator (still rejected, see above), the CFF/Type
  2 charstring format's own deprecated seac-style 4-operand `endchar` composition form (`adx ady
  bchar achar endchar`) _is_ supported by the core `Fonts` layer (`Fonts.CffCharstringInterpreter`)
  and therefore renders correctly here too; otherwise identical to `LoadType1Font`'s own Non-Goals
  above (the non-1000-unit-em `/FontMatrix` and built-in `/Encoding` scope boundaries), plus the
  predefined Expert/ExpertSubset CFF charsets are recognized by the core `Fonts` layer but not
  resolved to glyph names (that layer's own documented scope boundary, not re-implemented or
  worked around here) - a font relying on either predefined charset to name its glyphs resolves no
  glyph for any code via `ResolveEncoding` and therefore paints no visible ink for that code,
  without throwing.
- **Type 3 font resolution (`PdfDocument.Fonts.Type3.cs`, added in Phase D)** —
  `BuildResolvedType3Font(PdfObject fontDict)` is `BuildResolvedFont`'s `/Type3` dispatch target.
  It requires and parses `/FontMatrix` as six numbers (`ReadFontMatrix`; `InvalidDataException` if
  absent, non-array, wrong length, or any element non-numeric) and requires `/CharProcs` to
  resolve to a dictionary (`InvalidDataException` otherwise) - each entry an indirect reference to
  a glyph procedure's own content stream, resolved lazily (at paint time, by
  _Type 3 Glyph Painting_ below) rather than eagerly here. `/Encoding` is resolved via
  `ResolveType3Encoding`, reusing the same `ParseDifferences` run-length-decoding helper
  `ApplyDifferences` itself now delegates to (extracted in this phase so both call sites share one
  implementation): an absent, non-dictionary, or `/Differences`-less `/Encoding` leniently
  resolves to an empty code-to-glyph-name map (every code paints nothing, not an error) rather
  than the simple/composite-font path's own base-encoding-table convention, since a Type 3 font
  has no base encoding concept at all - only explicit `/Differences` entries name its glyphs.
  `/Widths`/`/FirstChar`/`/LastChar`/`/FontDescriptor/MissingWidth` are resolved with the same
  missing-entry leniency `ResolveWidths` already established for simple/composite fonts.
  `ResolvedType3Font.Resolve(int code)` is the one place this font kind's width convention
  differs from every other resolved font: the `/Widths` entry (glyph-space units) is transformed
  through `/FontMatrix` via `Vector2.TransformNormal` (the matrix's linear part only, correctly
  ignoring any translation component for a displacement vector) to obtain the text-space advance
  width - never divided by `1000`, since a Type 3 font's own `/FontMatrix` is the sole authority
  on its glyph-space-to-text-space scale (which need not be `0.001` at all - see
  _Type 3 Glyph Painting_'s own highest-risk geometry proof). `/Resources` is read but not
  otherwise processed here (optional; its paint-time fallback behavior is also documented in
  _Type 3 Glyph Painting_ below).
- **Type 3 glyph painting (`PdfDocument.Fonts.Type3.cs`, added in Phase D)** —
  `ShowGlyph` branches early, via a `font is ResolvedType3Font type3Font` pattern-matched check -
  performed before ever consulting `IResolvedFont.Font` (which is `null` for this font kind) - to
  `PaintType3Glyph(type3Font, code)`, skipping it entirely (while still advancing, via the shared
  trailing displacement logic every render mode uses) under render mode `3` (invisible), exactly
  like the outline-glyph path's own fill step. `PaintType3Glyph` looks up `code`'s glyph name via
  the font's resolved `/Encoding` map, then that name's content-stream reference via
  `/CharProcs`; either lookup failing (code unmapped, or glyph name absent from `/CharProcs`)
  paints nothing, with no exception - the code still advances by its declared width, since
  `Resolve`'s width computation is wholly independent of whether a glyph procedure exists. When
  both lookups succeed, the glyph procedure's content stream is decoded via the same
  `GetStreamDecodedBytes` every other stream in this class uses, and executed recursively via
  `ExecuteOperators` - the exact same re-entrance pattern `OpDrawFormXObject` already established
  for Form XObjects (see _Form XObjects_ above): `_resources`/`_gs`/`_gsStack` are saved, `_resources`
  is set to the Type 3 font's own `/Resources` when present else left as the invoking content
  stream's own (falling back exactly like a `/Subtype /Form` XObject with no `/Resources` of its
  own), a cloned `GraphicsState` (inheriting the invoking state's fill color/font/etc.) has its
  `CurrentTransform` overridden to the computed glyph matrix, and everything is restored in a
  `finally` block regardless of how the glyph procedure's execution completes - so a glyph
  procedure's own `cm`/color-operator mutations never leak back into the invoking content
  stream's graphics state. The glyph matrix is `FontMatrix * ComputeTextRenderingMatrix()` -
  the same left-multiply composition convention used throughout this class - entirely replacing
  the outline-glyph path's `1/UnitsPerEm`-scale convention for this font kind (there is no
  `UnitsPerEm` at all for a Type 3 font). Recursion is bounded by a dedicated
  `_type3NestingDepth`/`MaxType3NestingDepth` (`12`, reset to `0` at the start of each
  `ExecuteContentStream` call) - independent of `OpDrawFormXObject`'s own
  `_formNestingDepth`/`MaxFormNestingDepth` counter, so a pathological (self-referencing) glyph
  procedure and a pathological (self-referencing) Form XObject each fail closed against their own
  budget rather than one silently exhausting the other's. The `d0`/`d1` glyph-description
  operators are recognized (`OpType3SetWidth`/`OpType3SetWidthAndBBox`, dispatched from
  `DispatchOperator` exactly like every other operator) and their operand count/type validated via
  the same `RequireNumbers` helper every other fixed-arity operator uses, but their result is
  otherwise discarded: this class's authoritative advance width remains the font's own `/Widths`
  entry (see _Type 3 Font Resolution_ above), consulted independently of whichever glyph procedure
  happens to be painted - a stray `d0`/`d1` outside a glyph procedure is therefore a harmless,
  validated no-op. **Non-Goal (deliberate Phase D scope boundary)**: `d1`'s optional
  color-operator-suppression behavior (PDF 32000-1 §9.6.5.2 permits, but does not require, a
  conforming reader to ignore a `d1` glyph procedure's own color-setting operators and always
  paint with the invoking text's current fill color instead) is not implemented - a `d1` glyph
  procedure's own `rg`/`g`/`k`/`sc`/`scn` operators are honored normally, exactly as they would be
  inside any other content stream, rather than suppressed.
- **Composite font resolution (`PdfDocument.Fonts.Type0.cs`, added in Phase 9, extended in
  Phase 12, shape-sniffing dispatch added in Phase 18)** — `BuildResolvedCompositeFont` is
  `BuildResolvedFont`'s `/Type0` dispatch target. It
  requires `/Encoding` to resolve to the name `Identity-H`; any other name (including
  `Identity-V`) or non-name kind throws `Codecs.UnsupportedImageFeatureException` (feature
  `pdf-font-type0-encoding-{name}`) - no CMap-based or vertical-writing encoding is supported.
  `/DescendantFonts` must resolve to a single-element array whose element resolves to a
  dictionary (`InvalidDataException` for a missing/malformed array, matching this class's own
  "malformed required field" convention); that descendant's `/Subtype` must be `CIDFontType2` or
  `CIDFontType0` (`Codecs.UnsupportedImageFeatureException`, feature
  `pdf-font-cidfonttype-{subtype}`, for any other value or a missing `/Subtype`). Neither
  descendant subtype is ever embedded on a fallback/substitute basis - unlike
  `BuildResolvedSimpleFont`, no fallback substitution is ever attempted for a composite font (a
  deliberate Non-Goal: composite fonts must embed their descendant font). The descendant's
  `/FontDescriptor` is resolved once and dispatched, by subtype, to one of two loader helpers
  producing a `(TrueTypeFont Font, Func<int,int> CidToGid)` pair:
  - `LoadCidFontType2Font` (the pre-Phase-12 path, unchanged): requires
    `/FontDescriptor/FontFile2` (`InvalidDataException` if missing or non-stream), decodes it via
    `GetStreamDecodedBytes`, loads it via `Fonts.TrueTypeFont.Load`, and pairs it with
    `ResolveCidToGidMap`'s resolved `/CIDToGIDMap` function (see below).
  - `LoadCidFontType0Font` (new in Phase 12; shape-sniffing dispatch added in Phase 18): requires
    `/FontDescriptor/FontFile3` (`InvalidDataException` if missing or non-stream); decodes it via
    the same `GetStreamDecodedBytes` every other embedded font stream in this class uses, then
    sniffs the decoded bytes themselves - via the shared `SniffFontFile3Shape` helper
    (`PdfDocument.Fonts.cs`) - for a recognized SFNT container or a structurally plausible bare
    CFF header, never consulting the stream's own declared `/Subtype` name for this decision at
    all (it is read only for inclusion in the exception message below). An SFNT-wrapped (for
    example `'OTTO'`) CFF program is loaded via
    `Fonts.TrueTypeFont.Load(new MemoryStream(decodedBytes))`, exactly like the `CIDFontType2`
    path - the same `TrueTypeFont` type, no new `Fonts`-subsystem code; a bare, non-SFNT-wrapped
    CFF program (for example a `/CIDFontType0C`-declared stream with no SFNT wrapper) is loaded
    via `Fonts.TrueTypeFont.LoadType1C(new MemoryStream(decodedBytes),
    EmptyCodepointToGlyphName)` (an empty, never-populated codepoint-to-glyph-name map, since a
    composite font's CID-to-glyph-index mapping below never consults the font's own cmap or
    charset-derived glyph names at all). Either way, a declared `/Subtype` of `CIDFontType0C`,
    `OpenType`, `Type1C`, some other name, or no `/Subtype` key at all is accepted identically, as
    long as the bytes themselves match one of the two recognized shapes; only bytes matching
    neither shape are rejected, with `Codecs.UnsupportedImageFeatureException` (feature
    `pdf-font-fontfile3-unrecognized-shape`) naming the stream's declared `/Subtype` (or "none")
    in its message. Per PDF 32000-1 §9.7.4.2, a non-CID-keyed CFF program uses identity
    CID-to-glyph-index, so this path
    is paired with `cid => cid` directly (not `ResolveCidToGidMap` - `/CIDToGIDMap` is a
    `CIDFontType2`-only key per the specification, and any non-standard occurrence on a
    `CIDFontType0` descendant is deliberately ignored, never consulted). If the embedded CFF
    program is CID-keyed (`ROS` present in its Top DICT), `Fonts.CffTable.Parse` (invoked
    transitively by both `TrueTypeFont.Load` and `TrueTypeFont.LoadType1C`, regardless of
    container shape) already rejects it with `InvalidDataException`, which
    propagates uncaught here - no new translation code, consistent with this class's "composite
    fonts fail closed on any embedded-font problem" convention. CID-keyed CFF support itself
    (`FDArray`/`FDSelect`-aware charstring dispatch, a CID-keyed `charset` parser) is a
    separately-scoped, not-yet-implemented future phase.

  Regardless of descendant subtype, `ResolveCompositeWidths`
  resolves `/DW` (defaulting to `1000`, the specification's own documented default - not `0`, this
  is the one place a `TrueType`-family font descriptor's own default differs between the simple-
  and composite-font paths) and `/W` (a CID-to-width map, supporting both the `c [w1 w2 ... wn]`
  individual-width sub-form and the `cFirst cLast w` range sub-form, disambiguated by the resolved
  `PdfKind` - `Array` vs. `Number` - of the element immediately following the leading CID number;
  any other shape, or a range form with `cLast < cFirst`, throws `InvalidDataException`) - this
  logic has no subtype-specific branch at all. `ResolveCidToGidMap` (called only for a
  `CIDFontType2` descendant) resolves `/CIDToGIDMap`: absent or the name `/Identity`
  yields the identity function; a stream is decoded (via the same `GetStreamDecodedBytes` every
  other stream uses) into a big-endian `uint16`-per-CID lookup table (an odd byte count throws
  `InvalidDataException`), with an out-of-range/negative CID mapping to glyph `0`/`.notdef` per
  the specification; any other resolved kind throws `InvalidDataException`.
  `ResolvedCompositeFont.Resolve(code)` treats `code` directly as the CID (per `/Identity-H`'s own
  "code equals CID" identity), maps it to a glyph index via `CidToGid`, and resolves its width from
  `CidWidths` (falling back to `DefaultWidth`) - the same `(GlyphIndex, Width)` tuple shape
  `ResolvedSimpleFont.Resolve` returns, so `ShowGlyph` never needs a type check;
  `ResolvedCompositeFont` itself has no descendant-subtype-specific field or branch - the same
  shape serves both `CIDFontType2` and `CIDFontType0` descendants.
- **`/ToUnicode` CMap resolution (`PdfDocument.Fonts.ToUnicode.cs`, added in Phase 10)** —
  `ResolveToUnicodeMap(fontDict)` is called from `BuildResolvedCompositeFont` (on the Type0 font
  dictionary itself, not the descendant font dictionary) and stores its result on
  `ResolvedCompositeFont.ToUnicode`; it is unconsumed groundwork this phase - nothing reads it at
  rendering time. It returns `null` when `/ToUnicode` is absent or doesn't resolve to a stream
  (the same leniency `ResolveWidths` applies to a missing `/Widths` array), otherwise decodes the
  stream (via the same `GetStreamDecodedBytes` every other stream uses) and tokenizes it with a
  fresh `PdfTokenizer`, reusing `ParseValue`'s string/hex-string/array parsing primitives rather
  than a second, purpose-built CMap tokenizer. `beginbfchar`/`endbfchar` pairs map a single hex-
  string source code to a hex-string or literal-text (UTF-16BE) destination's codepoint.
  `beginbfrange`/`endbfrange` triples (`srcLo srcHi dst`) support two destination forms: a single
  hex/literal-string destination maps consecutive codes across `[srcLo, srcHi]` to consecutive
  incrementing codepoints starting at that destination's own codepoint, and an array destination
  maps each code individually to its own corresponding array element - a nested array element
  (the CIDSystemInfo-style "array of arrays" destination sub-form) is out of scope and throws
  `Codecs.UnsupportedImageFeatureException` (feature `pdf-font-tounicode-bfrange-array-
  destination`). A destination decoding to more than one UTF-16 code unit (e.g. a ligature) keeps
  only its first codepoint - a documented simplification, not a fidelity goal this phase.
  `begincodespacerange`/`endcodespacerange` and the CMap's own PostScript resource-management
  wrapper keywords (`begin`/`end`/`dict`/`def`/`findresource`/`defineresource`/`pop`/
  `currentdict`/`begincmap`/`endcmap`, and any bare number/name operand appearing alongside them)
  are silently skipped - mirroring `ExecuteContentStream`'s own "silently ignore any other
  operator" precedent - since none of them affect bfchar/bfrange mapping semantics. A
  `usecmap`/`cidrange`/`cidchar` operator, by contrast, is explicitly out of this phase's scope
  and throws `Codecs.UnsupportedImageFeatureException` (feature `pdf-font-tounicode-{operator}`)
  rather than being silently (and incorrectly) ignored.
- **Font fallback resolution (`PdfDocument.FontFallback.cs`, added in Phase 6; revised to add the
  Symbol/ZapfDingbats Noto substitution path)** —
  `ResolveFallbackFont(baseFontName, descriptor)` is `BuildResolvedSimpleFont`'s sole entry point into
  this file, and returns an ordered, non-empty `IReadOnlyList<Fonts.TrueTypeFont>` rather than a
  single font (see `BuildResolvedSimpleFont`'s own entry below for why). A `/BaseFont` of exactly
  `Symbol` or `ZapfDingbats` resolves via `ResolveSymbolicNotoFallback`: a bundled Noto substitute
  font union, reusing `Fonts.SystemFontCatalog.LoadBundledFallbackCore` (made callable
  cross-assembly via a new `InternalsVisibleTo` grant from `DemaConsulting.CanvasNet` to
  `DemaConsulting.CanvasNet.Pdf`, rather than duplicating its embedded-resource-loading/caching
  logic) - `Symbol` returns a 3-element priority list (`NotoSans-Regular.ttf` for Greek letters and
  general symbols, then `NotoSansMath-Regular.ttf` for mathematical operators, then
  `NotoSansSymbols2-Regular.ttf` for Private-Use-Area-adjacent/rare symbols), while `ZapfDingbats`
  returns a single-element list (`NotoSansSymbols2-Regular.ttf` alone). Any other font whose
  `/FontDescriptor/Flags` declares the `Symbolic` bit (bit 3) without also declaring the
  `Nonsymbolic` bit (bit 6) still fails closed with `Codecs.UnsupportedImageFeatureException`
  (feature `pdf-font-symbolic-not-embedded`) exactly as before - a symbol/dingbat glyph set has no
  meaningful generic-family equivalent, so it is never substituted rather than being silently
  mis-rendered, and this is now the sole remaining fail-closed case (`Symbol`/`ZapfDingbats`
  themselves are checked, and diverted to the Noto substitution path, before this check is ever
  reached). Otherwise,
  `ResolveFallbackFlavor` classifies the requested serif/fixed-pitch/bold/italic flavor: a
  recognized Standard-14 name (the fixed 12-entry `Standard14Flavors` table, covering
  Helvetica/Times/Courier's four style variants each - `Symbol`/`ZapfDingbats` deliberately
  excluded, since they now resolve via `ResolveSymbolicNotoFallback` before this table is ever
  consulted) takes priority and
  bypasses the descriptor entirely; otherwise the flavor is derived from `/FontDescriptor/Flags`
  bit 1 (`FixedPitch`)/bit 2 (`Serif`), `/FontWeight >= 600` (else a case-insensitive `"Bold"`
  substring in `/BaseFont`) for bold, and `/FontDescriptor/Flags` bit 7 (`Italic`) OR a non-zero
  `/ItalicAngle` OR a case-insensitive `"Italic"`/`"Oblique"` substring in `/BaseFont` for italic.
  `StripFontNameDecoration` then reduces `/BaseFont` to a plain family-name hint by removing a
  leading six-uppercase-letter-plus-`+` PDF subset tag and any trailing style-name suffix (for
  example `"Arial,BoldItalic"` and `"ABCDEF+Arial-BoldItalic"` both become `"Arial"`), and that
  hint plus the resolved flavor are passed to `Fonts.SystemFontCatalog.FindBestMatch`; a match is
  loaded (and process-lifetime-cached by `(FilePath, FaceIndex)`, via `LoadFallbackFontFromDisk`)
  from disk, while no match falls through to `Fonts.SystemFontCatalog.LoadBundledFallback`'s own
  bundled Liberation Sans/Serif/Mono font (which is itself already process-lifetime-cached by
  `SystemFontCatalog`). This process-lifetime cache is deliberately broader-scoped than
  `_fontCache`'s per-`Render` call scope, since a system or bundled font file's bytes never
  change between calls or between documents. **This ordinary (non-Symbol/non-ZapfDingbats) branch
  now returns a 2-element candidate list** - `[primaryMatch, Fonts.SystemFontCatalog.
  LoadBundledFallback(flavor)]`, where `primaryMatch` is whichever of the matched-system-font or
  already-bundled-fallback result the paragraph above resolved - reusing the same composite
  glyph-lookup mechanism the Symbol/ZapfDingbats path already established (see
  `ResolvedSimpleFont.Resolve` immediately below) rather than introducing a second, parallel
  mechanism: a codepoint the primary match itself does not cover now falls through to the bundled
  Liberation fallback's own glyph for that codepoint (closing the "tofu box"/missing-glyph gap for
  an otherwise-fully-resolved ordinary font whose own coverage is merely incomplete), instead of
  painting `.notdef` outright. Primary-font-first ordering means a font with full coverage is
  completely unaffected - the bundled fallback is only ever consulted on an actual per-codepoint
  glyph-0 miss.

  `BuildResolvedSimpleFont`'s `ResolvedSimpleFont.Fonts` property (plural, renamed from the
  earlier single `Font` property) is an ordered, non-empty `IReadOnlyList<Fonts.TrueTypeFont>` -
  a single-element list only for the embedded `/FontFile2` resolution path now, and multi-element
  for every fallback-substitution path (the ordinary 2-element primary/bundled-fallback list
  above, and the Symbol Noto-substitute union below). `ResolvedSimpleFont.Resolve(code)` (the sole
  `IResolvedFont.Resolve` implementation this concerns) tries each font in `Fonts` in priority
  order, returning the first one whose `GetGlyphIndex` for the code's mapped codepoint is
  non-zero (its advance width is derived from that winning font), falling back to `Fonts[0]` and
  glyph `0`/`.notdef` if none of them cover the codepoint - the composite/union glyph-lookup
  mechanism this feature introduces. `BuildResolvedSimpleFont` additionally selects, for a
  `/BaseFont` of exactly `Symbol` or `ZapfDingbats` resolved via this fallback path, the matching
  `SymbolEncodingTable`/`ZapfDingbatsEncodingTable` as `ResolveEncoding`'s new optional
  `defaultBaseTable` parameter (in place of its `WinAnsiEncodingTable` default for every other
  font) - an explicit `/Encoding/Differences` array still applies on top, exactly as for any
  other font.

- **Text rendering (`PdfDocument.Text.cs`, added in Phase 4)** — `OpBeginText`/`OpEndText`
  (`BT`/`ET`) reset only `_textMatrix`/`_lineMatrix` to the identity matrix (every other text-
  state parameter lives on `GraphicsState` and is untouched, per this phase's documented `q`/`Q`-
  interaction design — see `_textMatrix`/`_lineMatrix` above). `OpSetCharSpacing`/
  `OpSetWordSpacing`/`OpSetHorizontalScaling`/`OpSetLeading`/`OpSetTextRise` (`Tc`/`Tw`/`Tz`/`TL`/
  `Ts`) store their one operand verbatim; `OpSetFont` (`Tf`, a name then a number) resolves the
  named font resource via `ResolveFont` and stores it alongside the requested size;
  `OpSetTextRenderMode` (`Tr`) accepts modes `0` (fill, the default), `1` (stroke), `2`
  (fill, then stroke), and `3` (invisible — painted with zero-area geometry, i.e. skipped
  entirely), and `4`-`7` (the clip modes: fill/stroke/fill+stroke/invisible respectively, each
  additionally accumulating the glyph's device-space outline into `_textClipBuilder` and setting
  `_textClipPending`, even for outline-less glyphs such as spaces). `OpEndText` (`ET`) then, if a
  clip is pending, builds the accumulated path, converts it via `ClipMask.FromPath` (non-zero
  winding) and intersects it into `_gs.Clip`; the clip is therefore not applied until `ET`, and is
  restored by `Q`. Space-only clip text yields an empty region that clips everything (spec-
  conformant). `OpBeginText`, `ExecuteContentStream`, and the Form XObject/Type 3/tiling re-entrant
  executors reset or save/restore the accumulator so nested content cannot clobber it. A Type 3
  glyph shown under a clip mode throws `Codecs.UnsupportedImageFeatureException` (feature
  `pdf-text-render-mode-type3-clip`) from `ShowGlyph` since it has no outline; any other numeric
  value throws `InvalidDataException`. Modes `4`/`6` use the same flat fill color as modes `0`/`2`
  (the pre-existing no-Pattern-fill simplification). `OpTextMoveTo`/
  `OpTextMoveToSetLeading`/`OpTextNextLine` (`Td`/`TD`/`T*`) and `OpSetTextMatrix` (`Tm`)
  manipulate `_textMatrix`/`_lineMatrix` per the specification's own line-matrix-relative-
  displacement (`Td`/`TD`, `TD` additionally setting `Leading = -ty`) versus direct-replacement
  (`Tm`) semantics; `T*` is exactly `0 -TL Td`. `OpShowText`/`OpShowTextNextLine`/
  `OpShowTextNextLineWithSpacing`/`OpShowTextArray` (`Tj`/`'`/`"`/`TJ`) each ultimately call
  `ShowText`, which throws `InvalidDataException` if no font is currently selected (`Tf` was
  never called), then decodes the shown byte string into a sequence of character codes using the
  selected font's `IResolvedFont.CodeByteWidth` - one byte per code for a simple font (unchanged
  since Phase 4), or, as of Phase 9, two bytes (big-endian) per code for a composite
  `/Identity-H` font (`InvalidDataException` if the byte string's length is not a multiple of 2)
  - showing each decoded code via `ShowGlyph`: `ShowGlyph` calls the selected font's
  `IResolvedFont.Resolve(code)` once (folding in the encoding/glyph-index/advance-width lookup
  each concrete `IResolvedFont` implementation documents its own priority for - see _Font
  Resolution_/_Composite Font Resolution_ above), looks up the resolved glyph index's outline via
  `Fonts.TrueTypeFont.GetGlyphOutline`, computes the text-rendering matrix `Trm = [Tfs·Th, 0, 0,
  Tfs, 0, Trise] × Tm × CTM` (row-vector composition, matching `OpConcatMatrix`'s own convention),
  combines it with a `1/UnitsPerEm` glyph-space scale, transforms every glyph outline point
  through the result (`AppendTransformedGlyphOutline`, reimplementing - since it is `private` in
  a different assembly - the exact glyph-outline-to-`Geometry.Path` re-issuing pattern
  `SvgCodec.Text.cs` established), then paints the transformed outline per the current render
  mode (skipped entirely for render mode `3`): fills via `Drawing.PathFiller.Fill` with
  `_gs.FillColor` for modes `0`/`2` (a known, documented pre-existing simplification: unlike the
  stroke step below, this fill does not mirror a `/Pattern` fill color space - out of this
  change's scope), then, for modes `1`/`2`, strokes via the same `PaintStroke` helper
  `PdfDocument.PathOps.cs`'s path-painting operators use (see below), painting a resolved
  `/Pattern` stroke color space's tile colors when one is set. Mode `2`'s fill-then-stroke order
  matches the path-painting operators' own `B`/`b` order. `ShowGlyph` then advances `Tm.x` by
  `((w0 - Tj/1000) × Tfs + Tc + (code == 32 && CodeByteWidth == 1 ? Tw : 0)) × Th` per the
  specification, where `w0` is `IResolvedFont.Resolve(code)`'s returned advance width (the
  `Tj/1000` term only applies within `TJ`'s array form, via `ApplyTextSpaceAdjustment`) - word
  spacing is gated on `CodeByteWidth == 1` (as of Phase 9) since the specification's word-spacing
  rule addresses only the single-byte code `32` of a simple font, never a composite font's 2-byte
  code even when it numerically equals `32`. Every operator validates its operand
  count/type via the same `RequireNumbers`/`RequireOperandCount` helpers every other operator
  family uses, throwing `InvalidDataException` on mismatch.

- **Sampled Function Evaluation (`PdfDocument.Functions.cs`, added in Phase 1 of the `/Pattern`
  color-space roadmap)** — `ResolveFunction` resolves a `/Function` entry restricted to
  `/FunctionType 0` (sampled function) with exactly 1 input (a 2-element `/Domain`) into a
  `SampledFunction`, parsing `/Size` (a single element, since this is a 1-input function),
  `/BitsPerSample` (`8` or `16` only), `/Encode` (defaulting to `[0, Size[0] - 1]` when absent),
  and `/Decode` (defaulting to `/Range` when absent), and decoding the function stream's own bytes
  via `GetStreamDecodedBytes` (already filter-aware — no new filter code needed, since every
  sampled function this evaluator's motivating real-world PDF declares uses `/FlateDecode`).
  `SampledFunction.Evaluate(input)` clamps `input` into `/Domain`, linearly maps it into
  sample-index space via `/Encode`, clips the result into `[0, Size - 1]`, linearly interpolates
  between the two nearest samples (this evaluator never consults `/Order` — it always behaves as
  though `/Order` were `1`, matching every sampled function this evaluator's motivating PDF
  declares), reads each output's raw sample value via a generic, bit-width-agnostic big-endian bit
  reader (`ReadBits`, shared by both the 8-bit and 16-bit cases), and maps each interpolated raw
  value through `/Decode`, clipped to `/Range`. A `/FunctionType` other than `0` throws
  `Codecs.UnsupportedImageFeatureException` (feature `pdf-functiontype-{n}`); a multi-input
  `/FunctionType 0` function (a `/Domain` with more than 2 elements — the `/DeviceN`/`/Separation`
  tint-transform shape, itself already out of scope, see _Content-Stream Interpreter_'s color-space
  discussion above) throws the same exception type (feature `pdf-function-multiinput`); a
  `/BitsPerSample` other than `8`/`16` likewise throws (feature `pdf-function-bitspersample-{n}`).
  This was originally landed as resolved-but-unconsumed groundwork (Phase 1 of the `/Pattern`
  color-space roadmap); it is now consumed directly by shading-pattern gradient construction (see
  _`/Pattern` Color Space (Shading and Tiling Patterns)_ below), mirroring
  `PdfDocument.Fonts.ToUnicode.cs`'s own precedent of landing a narrowly-scoped parser ahead of
  the feature that consumes it.
- **Exponential and Stitching Function Evaluation (`PdfDocument.Functions.cs`, added alongside
  `/Pattern` color-space support)** — `ResolveFunctionGeneric` is the new, uniform entry point
  every pattern/shading caller uses (`ResolveFunction` itself remains, unchanged, as the
  `/FunctionType 0`-only path every pre-existing caller still uses): it dispatches `/FunctionType`
  `0` to the existing `SampledFunction` (through a shared `IPdfFunction` interface exposing
  `Evaluate(double)`/`OutputCount`), `2` to a new `ExponentialFunction` (`/Domain`, `/C0`/`/C1`
  defaulting to `[0.0]`/`[1.0]` per spec when absent, and `/N` — always a plain PDF number, never
  an array — computing the literal spec formula
  `C0[i] + (Math.Pow(clampedInput, N) * (C1[i] - C0[i]))` component-wise), and `3` to a new
  `StitchingFunction` (`/Domain`, `/Functions` — each sub-function independently resolved,
  recursively, through the same `ResolveFunctionGeneric` dispatcher, so a stitching function
  nesting another stitching function is supported automatically rather than specially — bounded by
  a new `MaxFunctionRecursionDepth` (`32`) guard on `ResolveFunctionGeneric` itself (checked/
  incremented/decremented exactly like `MaxColorSpaceRecursionDepth`), throwing
  `InvalidDataException` when a self-referencing or excessively deep `/Functions` chain would
  otherwise recurse until the process' call stack is exhausted — `/Bounds`
  — the `k - 1` partition points for `k` sub-functions — and `/Encode` — `2k` elements remapping
  each selected sub-domain into its own sub-function's `/Domain` before delegating). `/FunctionType
  4` (PostScript calculator functions) continues to throw
  `Codecs.UnsupportedImageFeatureException` (feature `pdf-functiontype-4`) — the exception
  message now reads "only /FunctionType 0, 2, and 3 are supported".

### `/Pattern` Color Space (Shading and Tiling Patterns)

- **`/Pattern` color-space resolution and `scn`/`SCN` pattern operands (`PdfDocument.Color.cs`,
  added alongside `/Pattern` color-space support)** — `PdfColorSpace.Family` gains a `Pattern`
  case, with an internal `PatternBase` property holding the optional underlying color space from
  the 2-element `[/Pattern baseSpace]` array form (`null` for the bare `/Pattern` name form).
  `cs`/`CS` resolve `/Pattern` as a fixed device-style identifier (never looked up as a
  `/Resources/ColorSpace` name, matching `/DeviceGray`/`/DeviceRGB`/`/DeviceCMYK`'s own
  resolution precedent); an array form with any element count other than 1 or 2 throws
  `Codecs.UnsupportedImageFeatureException` (feature `pdf-colorspace-Pattern`).
  `OpSetColorFill`/`OpSetColorStroke` (`scn`/`SCN` — the plain `sc`/`SC` operators never carry a
  pattern name per spec) branch on the active color space's `Kind == Family.Pattern` _before_
  reaching the existing numeric-component path: the trailing operand must be a `Name` (else
  `InvalidDataException`); every operand before it must be numeric, with a count driven _solely_
  by whether the active color space declared a `PatternBase` — exactly `ComponentCount
  (PatternBase)` numbers when one was declared (regardless of whether the pattern the name
  resolves to actually turns out to be a shading or a tiling pattern), or exactly `0` numbers
  otherwise (either shape violated throws `InvalidDataException`). The leading numeric operands,
  when present, convert via the existing `ColorFromComponents(PatternBase, ...)` helper into an
  "uncolored tint" color, stored alongside the resolved pattern. The resolved pattern name is
  looked up in the current page's `/Resources/Pattern` dictionary (`ResolvePattern`, mirroring
  `ResolveColorSpaceByName`'s own `/Resources/ColorSpace` lookup precedent exactly), throwing
  `Codecs.UnsupportedImageFeatureException` (feature `pdf-pattern-not-declared`) when absent, and
  dispatching on `/PatternType` to a tiling-pattern builder (`1`) or shading-pattern builder
  (`2`, per PDF-spec numbering), any other value throwing
  `Codecs.UnsupportedImageFeatureException` (feature `pdf-pattern-type-{n}`).
  `GraphicsState.FillPattern`/`StrokePattern` (new, nullable, shared-not-deep-copied on `Clone()`
  exactly like the existing `Font` field) carry the resolved pattern; `FillColor`/`StrokeColor`
  are set to an opaque-black placeholder that is documented as never actually painted with, since
  `PaintCurrentPath` always branches on `FillColorSpace.Kind == Family.Pattern` before reading
  either color field.
- **Shading patterns (`PdfDocument.Patterns.cs`/`PdfDocument.Patterns.Shading.cs`, added
  alongside `/Pattern` color-space support)** — a `/PatternType 2` dictionary's `/Shading`
  resolves `/ShadingType 2` (axial), `3` (radial) or `4`-`7` (mesh, see _Mesh shadings_ below);
  any other `/ShadingType` throws
  `Codecs.UnsupportedImageFeatureException` (feature `pdf-shading-type-{n}`). `/ColorSpace` must
  resolve to `DeviceGray`/`DeviceRGB`/`DeviceCMYK` (else feature
  `pdf-shading-colorspace-{family}`); `/Function` accepts either a single function or an array of
  1-output-component functions (`ResolveFunctionOrFunctionArray`), each resolved through
  `ResolveFunctionGeneric` above. The resolved shading samples its function(s) at 32 evenly spaced
  points across `/Domain` (a fixed constant balancing visual smoothness against per-fill
  allocation/compute cost — no caller needs more precision than this, mirroring
  `SampledFunction`'s own posture), converts each sample through `ColorFromComponents`, and builds
  one `Drawing.GradientStop` per sample to construct a `Drawing.LinearGradient` (`ShadingType 2`)
  or `Drawing.RadialGradient` (`ShadingType 3`) whose `Transform` is the pattern's own `/Matrix`
  composed with the page's initial (pre-`cm`) CTM (`_pageInitialCtm`, captured once per
  `ExecuteContentStream` call, per PDF 32000-1 §8.7.3.1's default-coordinate-system rule — this is
  the one documented limitation when a pattern is resolved from inside a nested Form XObject's own
  `/Resources/Pattern`: it still composes against the page's own initial CTM, not the Form's own
  default space). **`/Extend` is approximated as `Drawing.GradientSpread.Pad`** regardless of its
  actual `[false false]`/`[true true]`/mixed value — a documented, narrower-than-spec
  simplification: no existing `GradientSpread` value expresses the spec's true
  "paint nothing outside the defining geometry" default, and implementing that exactly would
  require clipping the shading's own paint to its defining geometry automatically - distinct from
  the general-purpose `W`/`W*` clipping-path operators (see _Clipping Paths_ below), which a
  content stream must invoke explicitly and which this phase does not wire into `/Extend`
  resolution itself.
- **Shading operator (`sh`, `PdfDocument.ContentStream.cs`/`PdfDocument.Patterns.Shading.cs`, per
  PDF 32000-1 §8.7.4.2)** — paints a named `/Resources/Shading` dictionary's gradient directly
  onto the destination surface, with no path construction/consumption and no `/Pattern`
  color-space selection at all, distinct from the `scn`/`SCN` + `/Pattern` + `/PatternType 2` path
  above. The shared `/ShadingType`/`/ColorSpace`/`/Function`/`/Domain`/`/Coords` resolution body
  previously inline in `BuildShadingPattern` was extracted into `ResolveShadingDescriptor`
  (returning a `ShadingDescriptor`, which also resolves an optional `/BBox` — see below);
  `BuildShadingPattern` now simply wraps it, behaviorally identical to before. `OpPaintShading`
  resolves the named shading (`Codecs.UnsupportedImageFeatureException`, feature
  `pdf-shading-not-declared`, when undeclared — mirroring `ResolvePattern`'s own
  `pdf-pattern-not-declared` precedent exactly), calls `ResolveShadingDescriptor` (propagating the
  exact same `pdf-shading-type-{n}`/`pdf-shading-colorspace-{family}` exceptions the shading-
  pattern path already throws, since it is the same method), and builds a transient
  `ResolvedPattern` with `Matrix = Matrix3x2.Identity` — unlike a shading _pattern_'s own
  `/Matrix` (anchored against the page's initial CTM via `PatternToDeviceTransform`), `sh` has no
  pattern wrapper of its own, so `BuildShadingGradient` is instead composed directly against
  `GraphicsState.CurrentTransform`, the CTM in effect when `sh` executes, per spec. The painted
  region is whichever is bounded: when the shading declares a `/BBox`, its four corners
  (transformed by the current CTM) form the region; otherwise a rectangle spanning the full
  destination surface is used — either way the region is passed to the same clip-aware
  `Drawing.PathFiller.Fill(Surface, Path, Gradient, ClipMask?, FillRule, float)` overload
  `PaintPatternFill`/`PaintCurrentPath`/`PaintStroke` already call (see _Clipping-path operators_
  above), so `sh` composes with an active `W`/`W*` clip with zero new clip-related code, and can
  never paint beyond the destination surface regardless of whether a `/BBox` was declared (that
  overload's own existing `TryFlattenForFill` step always intersects the fill's path bounds
  against the surface's own bounds before painting a single pixel).
- **Tiling patterns (`PdfDocument.Patterns.cs`/`PdfDocument.Patterns.Tiling.cs`, added alongside
  `/Pattern` color-space support)** — a `/PatternType 1` stream's `/BBox` (4 numbers), `/XStep`/
  `/YStep` (each required finite and non-zero — `InvalidDataException` otherwise, a malformed, not
  merely unsupported, value per spec), optional `/Matrix`, required `/PaintType` (`1` colored /
  `2` uncolored — any other value is `InvalidDataException`), own `/Resources` (falling back to
  the invoking stream's resources, exactly like `OpDrawFormXObject`), and decoded content bytes
  are all resolved up front. Rendering one pattern cell (`RenderTilingPatternCell`) computes the
  device-pixel tile-surface size from `|XStep|`/`|YStep|` scaled by the pattern-to-device
  transform's own determinant-derived scale factor (`MatrixScale`, the same
  `sqrt(|det(matrix.Linear)|)` formula `DeviceScale()` already uses for stroke width, extracted to
  a shared static helper so both call sites share one implementation), rejecting a resulting
  surface wider or taller than `MaxTileSurfaceDimension` (2048 device pixels) with
  `Codecs.UnsupportedImageFeatureException` (feature `pdf-pattern-tile-too-large` — a fail-closed
  guard against a pathological/adversarial `/XStep`/`/YStep` combined with an extreme CTM scale).
  The cell then renders through the exact same "swap `_surface`/`_resources`/`_gs`/`_gsStack`,
  increment `_formNestingDepth` (reusing the existing `MaxFormNestingDepth` guard — not a new,
  parallel counter, so combined Form-XObject and tiling-pattern-cell nesting is bounded by the one
  existing limit), `ExecuteOperators`, restore everything in a `finally` block" shape
  `OpDrawFormXObject` already established — with one necessary addition: the cell's own nested
  content-stream execution also saves, resets to a fresh value, and restores the path-building
  scalar fields (`_pathBuilder`/`_currentPoint`/`_subpathStart`/`_hasOpenSubpath`), since (unlike
  `Do`, which is never invoked mid-path-paint) a tiling-pattern cell can be rendered _from inside_
  an in-progress fill/stroke paint operation, after the host's own path has already been captured
  but before it has been cleared — without this extra save/reset/restore, the cell's own path data
  would silently accumulate onto the host's still-live path and corrupt both. For `/PaintType 2`
  (uncolored) patterns, every color the cell's content stream itself sets is overridden by the
  caller-supplied tint before compositing, preserving each pixel's own painted alpha exactly (so a
  partially transparent cell stays partially transparent, just recolored). The rendered cell
  becomes a `Drawing.TilePaint` (new `Drawing` subsystem unit — see _Introduction_ and
  `Drawing/TilePaint.cs`/`Drawing/TilePaintEvaluator.cs`), sampled nearest-neighbor per destination
  pixel by `Drawing.PathFiller`'s new `TilePaint` fill overload, mirroring the existing `Gradient`
  overload's structure exactly.
- **Mesh shadings (`PdfDocument.Patterns.Shading.Mesh.cs`, `/ShadingType` 4-7)** — `ResolveMeshShading`
  requires the shading to be a stream and validates `/BitsPerCoordinate` (1/2/4/8/12/16/24/32),
  `/BitsPerComponent` (1/2/4/8/12/16), `/BitsPerFlag` (2/4/8; not used by type 5), `/Decode`
  (4 + 2n finite entries) and `/VerticesPerRow` (type 5, at least 2), plus an optional `/Function`
  (one parametric component, sampled into a 256-entry lookup table across the `/Decode` t range;
  `/FunctionType 4` stays unsupported) and `/Background`. Type 4 reads free-form triangles
  (flag 0 starts a triangle, 1 reuses the previous edge vb-vc, 2 reuses va-vc); type 5 reads a lattice
  of `/VerticesPerRow` columns; types 6/7 read Coons/tensor patches whose 12/16 control points
  are mapped into a 4x4 net, with flags 1-3 inheriting the previous patch's edge and two corner
  colors (types 6 derives the four interior points from the Coons formula). **Every type 4/5 vertex
  and every patch starts on a byte boundary** (the alignment the Poppler and PDFium renderers use).
  Patches are
  evaluated as bicubic Bezier surfaces tessellated at a fixed 16x16 subdivision (a deliberate
  choice balancing cost and smoothness), triangles are rasterized with barycentric interpolation,
  no anti-aliasing and pixel-centre sampling, into an offscreen bitmap painted through the
  pattern's fill/stroke path or the `sh` clip/`/BBox` region. Colors are interpolated in RGB
  even for `DeviceCMYK` (a documented approximation). `/Background` paints beneath the mesh for
  pattern use only (not for `sh`). Malformed data (missing/illegal entries, truncated records,
  bad flags, bad lattice) throws `InvalidDataException`; unsupported color spaces/functions and DoS
  limits (1,048,576 vertices, 65,536 patches, 2^28 raster-work units) throw
  `Codecs.UnsupportedImageFeatureException` (`pdf-shading-mesh-too-many-vertices`/
  `-too-many-patches`/`-too-complex`).
- **Scope boundaries (deliberately not implemented this phase)** — `ShadingType` `1`
  (function-based shadings) and `/FunctionType 4` (PostScript calculator functions)
  remain unsupported/unchanged, reachable through two paths — `scn`/`SCN` + `/Pattern` and
  `sh` — both throwing the identical `Codecs.UnsupportedImageFeatureException` (shared code, not
  a reimplementation); a `/Pattern` color space nested inside another `/Pattern`'s own `PatternBase` is out of scope (the
  `ComponentCount` arm for `Family.Pattern` throws `InvalidOperationException` as a fail-closed
  backstop, since no caller is expected to reach it); deep nested-tiling-pattern recursion is
  bounded only by the shared, reused `MaxFormNestingDepth` guard (no dedicated correctness test of
  deep nested rendering itself, only that the guard still fires through this new call path).

### Resource and Input Bounds

`PdfDocument` parses an externally-supplied, potentially adversarial PDF document, so several of
its decoders and interpreter loops cap a resource (decoded-output size, stack depth, or expansion
factor) that an otherwise well-formed but maliciously crafted input could otherwise drive
unboundedly large, closing a class of decompression-bomb/resource-exhaustion denial-of-service
risks without rejecting any legitimate, real-world PDF document:

- **`FlateDecode` output cap** — `DecompressFlateStream`'s chunked read loop rejects a decoded
  stream once its accumulated output exceeds `FlateMaxOutputBytes` (64 MiB) with
  `InvalidDataException`, rather than continuing to inflate an unbounded (or deliberately
  self-referential/highly-compressible) `zlib` stream into memory. 64 MiB is generous for any
  legitimate PDF image or content stream while still bounding a single decode's worst-case
  allocation.
- **`RunLengthDecode` output cap** — each repeat/literal run is checked against the remaining
  budget under `RunLengthMaxOutputBytes` (64 MiB) _before_ the run's bytes are appended, throwing
  `InvalidDataException` rather than only detecting the overrun after the full (already expanded)
  output has been materialized; a single 2-byte repeat run can expand to up to 128 output bytes (a
  64x amplification), so checking the budget first closes the window where a tiny crafted stream
  could otherwise force a large allocation before the cap is ever observed.
- **`ASCII85Decode` output cap** — mirrors the `RunLengthDecode` convention exactly:
  `Ascii85MaxOutputBytes` (64 MiB) is checked before each decoded group is appended, throwing
  `InvalidDataException` once exceeded, bounding a filter whose encoded form is otherwise only
  mildly larger than its decoded output but still unbounded in principle.
- **Graphics-state stack depth cap** — `OpPushGraphicsState` (`q`) throws `InvalidDataException`
  once the graphics-state stack already holds `MaxGraphicsStateStackDepth` (256) entries, rather
  than allowing a content stream with deeply/unboundedly nested `q` operators (never matched by a
  corresponding `Q`) to grow the stack without limit. 256 comfortably exceeds any nesting depth a
  legitimate content stream's own `q`/`Q`-balanced structure is expected to reach.
- **`/ToUnicode` `bfrange` span cap** — `ParseBfRangeBlock`'s hex-string destination form throws
  `InvalidDataException` when a single `bfrange` entry's code span (`srcHi - srcLo + 1`) exceeds
  `ToUnicodeMaxBfRangeSpan` (65536 codes), rather than materializing one map entry per code for an
  entry whose declared source range is enormous (or malformed to appear so).
- **Compressed object-stream cycle guard** — `GetObject`/`LoadCompressedObject` track the set of
  compressed-object numbers currently being resolved in `_compressedObjectResolutionStack`,
  throwing `InvalidDataException` when a compressed object's own resolution would re-enter itself
  (directly or transitively via another compressed object naming it as its own containing object
  stream), rather than recursing indefinitely into a genuine cycle and eventually raising an
  `StackOverflowException` that cannot be caught - mirroring the page-tree reference-cycle guard
  `TraversePageTree` already applies to `/Kids`.
- **Non-finite numeric operand rejection** — `RequireNumbers` (the shared operand-parsing helper
  used by every numeric content-stream operator, including `cm`, path-construction operators, and
  color operators) throws `InvalidDataException` when any parsed operand is `NaN` or an infinity,
  rejecting a non-finite value before it can propagate into a transform matrix or path coordinate
  and corrupt unrelated, subsequently-painted geometry.
- **CCITT dimension/bounds validation ordering** — `DecodeCcittFaxImageXObject` validates a
  non-positive (zero or negative) `/Columns`/`/Rows` value, and performs its row/column bounds
  check, _before_ invoking the Group 4 (T.6 MMR) decoder, rather than after: validating first
  prevents an oversized allocation from being attempted ahead of the existing bounds check ever
  running.
- **Xref stream-data-length overflow fix** — `GetStreamRawBytes` computes a compressed object
  stream's `StreamDataStart + length` bound using `long` arithmetic rather than `int`, preventing
  a maliciously large declared `/Length` from wrapping an `int` sum around to a small or negative
  value that would otherwise defeat the subsequent bounds check entirely.
- **Unterminated literal-string rejection** — the tokenizer throws `InvalidDataException` when a
  PDF literal string (`(...)`) reaches end-of-file while its paren-nesting depth is still nonzero,
  rather than silently treating the truncated remainder of the file as the string's own content.

Each of these limits is an approximate, deliberately generous bound chosen to comfortably exceed
any legitimate, real-world PDF document's own requirements while still closing the unbounded-
resource attack surface a malicious or malformed document could otherwise exploit; none of them
change behavior for any document within normal real-world limits.

### Error Handling

- **Null `stream`/`path` argument to `Open`** — `ArgumentNullException`, thrown directly with a
  `nameof(...)` parameter name.
- **Empty/whitespace-only `path` to `Open(string)`** — `ArgumentException`, mirroring
  `PngCodec.Load(string)`'s message wording.
- **Malformed/unresolvable document structure** (tokenizer, object model, xref, or page tree,
  after the linear-scan fallback is exhausted) — `InvalidDataException`, mirroring `PngCodec`'s
  exact convention for malformed data.
- **`/Encrypt` key present in the trailer, but not the `/Standard` security handler** (feature
  `pdf-encrypted-filter-{name}`), **a `/CF/StdCF/CFM` other than `/AESV2`/`/AESV3`** (feature
  `pdf-encrypted-cfm-{name}`), **`/V 5` with an unsupported `/R` (not 5 or 6)** (feature
  `pdf-encrypted-r-{revision}`), **an unsupported `/V`** (feature `pdf-encrypted-v-{version}`),
  **a `null` password that fails `/U` (R2-R4) or `/U`'s embedded validation hash (R5)
  authentication, i.e. the document genuinely requires a non-empty password** (feature
  `pdf-encrypted-password-required`), **a non-`null` password that authenticates as neither the
  user nor the owner password** (feature `pdf-encrypted-incorrect-password`), or **a non-`null`
  password containing a character outside ASCII 0-127 for an R2-R4 document** (feature
  `pdf-encrypted-password-non-ascii`) — `Codecs.UnsupportedImageFeatureException` for each,
  thrown directly, each with its own distinguishable `Feature` token (added in Phase 16/17, see
  _Encryption (Standard Security Handler)_ above; narrows the previous Phase 1 blanket
  `"pdf-encrypted"` rejection of every encrypted document regardless of shape).
- **Out-of-range `pageIndex` to `GetPageInfo`/`Render`** — `ArgumentOutOfRangeException`, thrown
  directly.
- **Non-positive/too-large `width`/`height` to `Render`** — `ArgumentOutOfRangeException`,
  propagated unwrapped from `Surface`'s own constructor, not duplicated.
- **Malformed `/Contents`** (neither a stream nor an array of streams, or an array entry that
  does not itself resolve to a stream) — `InvalidDataException`.
- **Malformed content-stream syntax, or a malformed operand count/type for a recognized
  operator** — `InvalidDataException`, matching `PdfDocument.Xref.cs`'s own established
  malformed-input convention. An unrecognized operator is never an error (see _Content-Stream
  Interpreter_ above); an unbalanced `Q` with no matching prior `q` is likewise tolerated as a
  documented no-op, not an error.
- **An unsupported color space** (`cs`/`CS`, or an image XObject's `/ColorSpace`:
  `Separation`/`DeviceN`/`CalGray`/`Lab`, an undeclared `/Resources/ColorSpace` name, an
  `/ICCBased` stream whose `/N` is not `1`/`3`/`4` with no usable `/Alternate`, an `/Indexed` color
  space whose base color space is itself unsupported, a `/Pattern` array form with an element
  count other than 1 or 2 (feature `pdf-colorspace-Pattern`), or any other unrecognized value) —
  `Codecs.UnsupportedImageFeatureException`.
- **`scn`/`SCN` pattern operands** — a missing trailing `Name` operand, or a leading numeric
  operand count that does not exactly match the active color space's declared `PatternBase`
  component count (`0` when none was declared) — `InvalidDataException`; an undeclared pattern
  name (feature `pdf-pattern-not-declared`) or an unsupported `/PatternType` (anything other than
  `1`/`2`, feature `pdf-pattern-type-{n}`) — `Codecs.UnsupportedImageFeatureException`.
- **An unsupported shading pattern** — a `/ShadingType` outside `2`–`7` (feature
  `pdf-shading-type-{n}`), or a `/ColorSpace` other than `DeviceGray`/`DeviceRGB`/`DeviceCMYK`
  (feature `pdf-shading-colorspace-{family}`) — `Codecs.UnsupportedImageFeatureException`. The
  `sh` operator reaching an undeclared shading name throws the same shape, feature
  `pdf-shading-not-declared` (mirroring the existing `pdf-pattern-not-declared` feature exactly);
  `sh` reaching an unsupported `/ShadingType`/`/ColorSpace` throws the identical
  `pdf-shading-type-{n}`/`pdf-shading-colorspace-{family}` features the shading-pattern-fill path
  already throws, since `ResolveShadingDescriptor` is the one shared implementation both paths
  call.
- **A pathological tiling pattern** — a `/XStep`/`/YStep` that is zero or non-finite, or a
  `/PaintType` other than `1`/`2` — `InvalidDataException`; a resolved device-pixel tile surface
  exceeding `MaxTileSurfaceDimension` — `Codecs.UnsupportedImageFeatureException` (feature
  `pdf-pattern-tile-too-large`).
- **`ResolveFunction`/`ResolveFunctionGeneric`'s `/Function` entry** — a `/FunctionType` other
  than `0`/`2`/`3`, a multi-input `/FunctionType 0` function (a `/Domain` with more than 2
  elements), a `/BitsPerSample` other than `8`/`16`, or a `/FunctionType 4` (PostScript
  calculator) function (feature `pdf-functiontype-4`) — `Codecs.UnsupportedImageFeatureException`;
  a `/Function` that does not resolve to a stream/dictionary, or a missing/malformed
  `/Domain`/`/Range`/`/Size`/`/Encode`/`/Decode`/`/C0`/`/C1`/`/N`/`/Functions`/`/Bounds` entry —
  `InvalidDataException` instead (malformed, not merely unsupported).
- **An unsupported stream filter** (anything other than `FlateDecode`, `LZWDecode`,
  `ASCII85Decode`, `ASCIIHexDecode`, `RunLengthDecode`, or `DCTDecode`/`CCITTFaxDecode` combined
  with another filter) — `Codecs.UnsupportedImageFeatureException`; an unrecognized `/Predictor`
  value, a malformed `/Filter`/`/DecodeParms` shape, or malformed bytes for any of the five
  supported filters is `InvalidDataException` instead (malformed, not merely unsupported).
- **An unsupported image `/BitsPerComponent`** (anything other than `1`, `2`, `4`, `8` or `16`, or
  16 on an `/Indexed` image), or a TIFF predictor
  combined with a non-`8` `/BitsPerComponent` — `Codecs.UnsupportedImageFeatureException`.
- **`CCITTFaxDecode` with a non-negative `/K` (Group 3), or with `/EndOfLine true`** —
  `Codecs.UnsupportedImageFeatureException` (added in Phase 15; this decoder implements Group 4
  (T.6 MMR) only and never scans for EOL/EOFB/RTC bit patterns); **`CCITTFaxDecode` whose
  resolved `/ColorSpace` has more than 1 component** — `Codecs.UnsupportedImageFeatureException`;
  a malformed/truncated CCITT bit stream (an unrecognized mode code, a changing-element search
  that runs past `/Columns`, or running out of bits mid-row) — `InvalidDataException` instead.
- **`Do` on a `/Subtype /Form` XObject whose nesting depth already equals
  `MaxFormNestingDepth` (`12`), or whose own `/Matrix` is present but is not an array of exactly
  6 numbers** — `InvalidDataException` (added in Phase 13; see _Image XObjects_/_Form XObjects_
  above).
- **`Do` with a name undeclared in `/Resources/XObject` (or no `/Resources` at all), a
  non-stream/missing-`/Subtype` resolved value, or a malformed operand count/type** —
  `InvalidDataException` (a malformed content stream, not merely unsupported).
- **A font dictionary whose `/Subtype` is `/MMType1`** —
  `Codecs.UnsupportedImageFeatureException` (Multiple Master Type 1 fonts remain
  entirely out of scope by design). A `/Subtype /TrueType` or `/Subtype /Type1` font lacking an
  embedded `/FontDescriptor/FontFile2`/`/FontFile`/`/FontFile3` no longer reaches this list at all
  as of Phase 6 (widened to `/Type1` in Phase B) - it is resolved
  via fallback substitution instead (see below). A `/BaseFont` of exactly `Symbol` or
  `ZapfDingbats` resolves via a bundled Noto substitute font union instead of reaching this list
  either (see _Font fallback resolution_ above); any other font whose `/FontDescriptor/Flags`
  declares `Symbolic` without also
  declaring `Nonsymbolic` still throws `Codecs.UnsupportedImageFeatureException` (feature
  `pdf-font-symbolic-not-embedded`) - a symbol/dingbat glyph set with no bundled substitute has no
  meaningful generic-family
  equivalent and is never substituted with an unrelated system or bundled font. As of Phase C, a
  `/Subtype /Type1` descriptor declaring only `/FontFile3` (neither `/FontFile` nor `/FontFile2`)
  is resolved by `LoadType1CFont`: as of Phase 18, the stream's declared `/Subtype` is never
  consulted for dispatch at all - only the decoded bytes' own shape is sniffed (an SFNT container
  or a bare CFF header, via the shared `SniffFontFile3Shape` helper); only bytes matching neither
  recognized shape are rejected with `Codecs.UnsupportedImageFeatureException` (feature
  `pdf-font-fontfile3-unrecognized-shape`, reusing the exact feature-key pattern
  `LoadCidFontType0Font`'s own shape-sniffing dispatch below already establishes) - an
  SFNT-wrapped (`/OpenType`-shaped) CFF stream on a simple `/Type1` font is now supported here,
  exactly like the composite `CIDFontType0` path below. A `/FontFile` stream's own
  missing or non-numeric `/Length1`/`/Length2` entry, or any exception `Fonts.TrueTypeFont.LoadType1`
  itself throws for a malformed embedded Type 1 program, is `InvalidDataException` (no fallback
  substitution is ever attempted once an embedded Type 1 program is present, matching the
  `CIDFontType2`/`CIDFontType0` "embedded fonts fail closed" precedent); likewise, any exception
  `Fonts.TrueTypeFont.Load`/`Fonts.TrueTypeFont.LoadType1C` itself throws for a malformed or
  CID-keyed (`ROS`-bearing) embedded CFF program is `InvalidDataException`, propagated uncaught
  under the same convention.
- **A `/Subtype /Type3` font dictionary missing (or non-array-of-6-numbers) `/FontMatrix`, or
  missing (or non-dictionary) `/CharProcs`** — `InvalidDataException` (added in Phase D; a
  malformed/missing required field, not merely unsupported - unlike every other resolved font
  kind, a Type 3 font has no fallback-substitution path at all, so either condition fails
  closed unconditionally). A Type 3 glyph procedure's recursion exceeding
  `MaxType3NestingDepth` (`12`, tracked independently of `MaxFormNestingDepth` - see
  _Type 3 Glyph Painting_ above) is `InvalidDataException` as well, mirroring
  `OpDrawFormXObject`'s own nesting-depth precedent. An unmapped character code, or a mapped
  glyph name absent from `/CharProcs`, is never an exception for a Type 3 font - it paints
  nothing but still advances (a deliberate leniency, matching `ResolveWidths`'s own established
  "missing optional field" convention, not this class's "malformed required field" convention).
- **A `/Subtype /Type0` font's `/Encoding`, when not the name `Identity-H`** (for example
  `Identity-V` or a predefined CJK encoding name) — `Codecs.UnsupportedImageFeatureException`
  (feature `pdf-font-type0-encoding-{name}`), as of Phase 9. A `/Type0` font's `/DescendantFonts`
  entry, when missing or not a single-element array whose element resolves to a dictionary, is
  `InvalidDataException` instead (a malformed required field, not merely unsupported). A
  descendant font's `/Subtype`, when neither `CIDFontType2` nor `CIDFontType0` —
  `Codecs.UnsupportedImageFeatureException` (feature `pdf-font-cidfonttype-{subtype}`). A
  `CIDFontType2` descendant font's missing/non-embedded `/FontDescriptor/FontFile2`, or a
  `CIDFontType0` descendant font's missing/non-embedded `/FontDescriptor/FontFile3`, is
  `InvalidDataException` (no fallback substitution is ever attempted for a composite font - a
  deliberate Phase 9 Non-Goal, unchanged by Phase 12/18). As of Phase 18, a `CIDFontType0`
  descendant's `/FontFile3` stream's declared `/Subtype` is never consulted for dispatch at all -
  only the decoded bytes' own shape is sniffed (an SFNT container or a bare CFF header, via the
  shared `SniffFontFile3Shape` helper); only bytes matching neither recognized shape are rejected
  with `Codecs.UnsupportedImageFeatureException` (feature
  `pdf-font-fontfile3-unrecognized-shape`); a bare, non-SFNT-wrapped CFF stream (for example
  declared `/CIDFontType0C`) whose embedded CFF program is CID-keyed (`ROS` present) is
  `InvalidDataException`, surfaced uncaught from `Fonts.CffTable.Parse`'s own existing rejection -
  the same rejection an SFNT-wrapped CID-keyed CFF program already triggered before Phase 18.
  A `CIDFontType2` descendant font's `/CIDToGIDMap`, when resolving to anything other than the
  name `Identity` or a stream (or a stream with an odd byte count), is `InvalidDataException`
  (this key is never consulted, and any non-standard occurrence never validated, for a
  `CIDFontType0` descendant); a malformed `/W` array shape (not matching either the
  `c [w1 w2 ... wn]` or `cFirst cLast w` sub-form, or a range form with `cLast < cFirst`) is
  `InvalidDataException` too, regardless of descendant subtype. A composite
  font's shown byte string with an odd byte length is `InvalidDataException` (a 2-byte-per-code
  string must have an even byte count).
- **A `bfrange` destination array containing a nested array** (the CIDSystemInfo-style "array of
  arrays" destination sub-form), as of Phase 10 — `Codecs.UnsupportedImageFeatureException`
  (feature `pdf-font-tounicode-bfrange-array-destination`); a `usecmap`/`cidrange`/`cidchar`
  operator appearing in a `/ToUnicode` CMap stream is likewise
  `Codecs.UnsupportedImageFeatureException` (feature `pdf-font-tounicode-{operator}`). By
  contrast, `begincodespacerange`/`endcodespacerange` and the CMap's own PostScript resource-
  management wrapper keywords (`begin`/`end`/`dict`/`def`/`findresource`/`defineresource`/`pop`/
  `currentdict`/`begincmap`/`endcmap`) are silently ignored, not rejected - they carry no
  bfchar/bfrange mapping semantics of their own. An absent `/ToUnicode` entry resolves to a null
  map, matching `ResolveWidths`'s own "missing optional field" leniency.
- **An `/Encoding` naming an unrecognized base encoding** (anything other than
  `/WinAnsiEncoding`/`/MacRomanEncoding`/`/StandardEncoding` (the last added in Phase B), or their
  dictionary form's `/BaseEncoding`) — `Codecs.UnsupportedImageFeatureException`.
- **A Type 3 glyph shown under a clipping `Tr` mode (`4`-`7`)** —
  `Codecs.UnsupportedImageFeatureException` (feature `pdf-text-render-mode-type3-clip`), raised at
  show time; `Tr` itself accepts `0`-`7`, and any other numeric value is
  `InvalidDataException`.
- **A text-showing operator (`Tj`/`'`/`"`/`TJ`) with no font currently selected** (`Tf` was never
  called), or a malformed operand count/type for any text operator — `InvalidDataException`,
  matching every other operator family's own convention.
- **Any public member called after `Dispose()`** — `ObjectDisposedException`, thrown first via
  `ObjectDisposedException.ThrowIf(_disposed, this)`, before any other validation.

### Dependencies

- `Canvas.Surface` (from the core `CanvasNet` system) — the `Render` return type, every
  path-painting operator's paint destination, and a decoded image XObject's own pixel buffer
- `Canvas.Rgba32` (from the core `CanvasNet` system) — the current fill/stroke color and a
  decoded image sample's color representation
- `Codecs.UnsupportedImageFeatureException` (from the core `CanvasNet` system) — reused,
  unmodified, for `/Encrypt` detection and, as of Phase 3, an unsupported color space/stream
  filter/image bit depth; as of Phase 14, also an unsupported pattern type/shading
  type/shading color space/function type; as of Phase 16, also each of the five distinct
  narrowed encryption rejection reasons documented under _Error Handling_ above
- `Codecs.JpegCodec` (from the core `CanvasNet` system, new as of Phase 3) — `Load(Stream)`
  decodes an image XObject's bare `DCTDecode` (JPEG) bitstream directly, without requiring
  APP0/JFIF framing
- `Geometry.PathBuilder`/`Path` (from the core `CanvasNet` system) — accumulates the current
  path's subpaths/commands as path-construction operators are dispatched
- `Drawing.PathFiller`/`PathStroker`/`StrokeStyle`/`FillRule`/`LineCap`/`LineJoin` (from the core
  `CanvasNet` system) — rasterize the current path onto `_surface` for every path-painting
  operator, and, as of Phase 4, every filled glyph outline
- `Drawing.Gradient`/`LinearGradient`/`RadialGradient`/`GradientStop`/`GradientSpread` and
  `Drawing.TilePaint` (from the core `CanvasNet` system, new as of the Phase 14 `/Pattern`
  color-space work) — `PathFiller`'s `Gradient`-typed `Fill` overload paints an axial
  (`LinearGradient`) or radial (`RadialGradient`) shading pattern built from the resolved
  `/Shading` dictionary's evaluated function and `/Coords`; its `TilePaint`-typed `Fill` overload
  paints a colored/uncolored tiling pattern's own pre-rendered repeating tile cell through the
  pattern-to-device transform — see _`/Pattern` Color Space (Shading and Tiling Patterns)_ above
  for the full construction detail
- `Fonts.TrueTypeFont` (from the core `CanvasNet` system, new as of Phase 4, `LoadType1` added in
  Phase A) — loads an embedded `/FontFile2` byte stream and resolves each shown codepoint to a
  glyph index/outline/advance width, exactly as `SvgCodec.Text.cs` already uses it for SVG
  `<text>` rendering; `LoadType1(Stream, int length1, int length2, IReadOnlyDictionary<int,
  string> codepointToGlyphName)` is used as of Phase B by `LoadType1Font` to load an embedded
  classic PostScript Type 1 `/FontFile` program, reusing the same `TrueTypeFont` type rather than
  a separate font representation; `LoadType1C(Stream stream, IReadOnlyDictionary<int, string>
  codepointToGlyphName)` is used as of Phase C by `LoadType1CFont` to load an embedded bare
  Type1C/CFF `/FontFile3` program, again reusing the same `TrueTypeFont` type
- BCL `System.IO.Compression.DeflateStream` — `FlateDecode` decompression of object streams and,
  as of Phase 3, any other `FlateDecode`-filtered stream (image XObjects included)
- BCL `System.Numerics.Matrix3x2`/`Vector2` — the current transformation matrix, every
  transformed path point, and (as of Phase 3) an image XObject's unit-square-to-device-space
  compositing math
- `PdfTokenizer`/`PdfObject`/`ParseValue` (internal, `PdfDocument.Tokenizer.cs`/
  `PdfDocument.ObjectModel.cs`) — reused as of Phase 10 by `ResolveToUnicodeMap` to tokenize and
  parse a `/ToUnicode` CMap stream's `bfchar`/`bfrange` operands (hex strings, literal strings,
  and arrays); no second, purpose-built CMap tokenizer was introduced.
- BCL `System.Security.Cryptography.MD5`/`SHA256`/`Aes` (new as of Phase 16,
  `PdfDocument.Encryption.cs`) — `MD5.HashData`/`SHA256.HashData` implement ISO 32000-1
  Algorithm 2/4/5's and ISO 32000-2 Algorithm 2.A's hash steps exactly as the PDF specification
  itself mandates (not a free security choice — see the file's own `S4790` suppression
  justification comment); `Aes.Create()` (`CipherMode.CBC`, `PaddingMode.PKCS7`/`None`) decrypts
  AESV2/AESV3 strings and streams. RC4 has no BCL equivalent and is hand-rolled (`Rc4Transform`),
  mirroring this codebase's existing convention of hand-rolled standard algorithms elsewhere
  (e.g. CRC-32/Adler-32/CCITT tables).

### Callers

None yet - `PdfDocument` is `CanvasNetPdf`'s sole unit and its system's only public entry point;
no other unit or subsystem in this repository calls into it.
