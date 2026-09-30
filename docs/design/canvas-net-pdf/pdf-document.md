## PdfDocument

![CanvasNetPdf Structure](CanvasNetPdfView.svg)

<!-- cspell:ignore xref startxref endobj endstream ObjStm MediaBox unresolvable Trise -->
<!-- cspell:ignore CCITT reimplementation diffability bitstream Zapf Nonsymbolic -->
<!-- cspell:ignore Segoe Dejavu Nimbus Consolas ttcf dogfooding LOCALAPPDATA -->

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
and composites a `/Subtype /Image` XObject (`DCTDecode` via `Codecs.JpegCodec`, or raw
`DeviceGray`/`DeviceRGB`/`DeviceCMYK` 8-bit samples) through the current transformation matrix;
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
`../canvas-net/fonts/system-font-catalog.md`). **Phase 4 limitations (narrowed by Phase 6, see
above)**: `/Type0` (composite/CID-keyed), `/Type1`, `/MMType1`, and `/Type3` fonts remain entirely
unsupported and fail closed with `Codecs.UnsupportedImageFeatureException`; only the
`/WinAnsiEncoding` and `/MacRomanEncoding` base encodings (plus `/Differences`) are supported (an
unrecognized base encoding also fails closed); only text-rendering modes `0` (fill) and `3`
(invisible) are supported (stroke/clip modes `1`/`2`/`4`-`7` fail closed); and no additional
stream filters were added for either phase.
**Phase 3 limitations** (narrowed by Phase 7, see below): no Form XObject rendering (`Do` on a
`/Subtype /Form` XObject fails closed with `UnsupportedImageFeatureException`, rather than being
silently skipped), no shading/patterns/transparency groups, no `CCITTFax`/`JPX` filter decoding
(fails closed; `LZWDecode`/`ASCII85Decode`/`ASCIIHexDecode`/`RunLengthDecode` are supported as of
Phase 7, see below), and no `/SMask`/alpha compositing (every decoded image is treated as fully
opaque) — these remain out of scope for this phase and are silently skipped (any other undefined
keyword) or explicitly rejected (Form XObjects, unsupported color spaces/filters/fonts/encodings/
render modes), per the operator/exception taxonomy documented below; a later phase is expected to
add Form XObject and transparency support.

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
  text state — `Font` (the currently selected `ResolvedFont?`), `FontSize`, `CharSpacing`,
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
- **`_fontCache` (`Dictionary<PdfObject, ResolvedFont>`, `PdfDocument.Fonts.cs`, added in Phase
  4)** — memoizes each font dictionary's resolved `ResolvedFont` (embedded `Fonts.TrueTypeFont`,
  256-entry code-to-Unicode-codepoint encoding map, per-code `/Widths` map, `/MissingWidth`) by
  the font dictionary `PdfObject`'s own reference identity, so `Tf` re-selecting the same font
  resource repeatedly within one `Render` call never re-decodes/re-parses the embedded
  `FontFile2` bytes more than once. Reset (cleared) at the start of every `ExecuteContentStream`
  call — scoped to a single `Render` call only, per this phase's documented caching contract (a
  later `Render` call always re-resolves every font from scratch, trading a small amount of
  redundant work across separate calls for never risking a stale reference into a different
  document's object graph).
- **`_surface` (`Canvas.Surface`, `PdfDocument.ContentStream.cs`)** — the destination surface
  every path-painting operator draws onto for the content stream currently being executed.
- **`PdfObject`/`PdfKind`** (internal, `PdfDocument.ObjectModel.cs`) — a small tagged-union
  representation of every PDF object kind (`Null`, `Boolean`, `Number`, `LiteralString`/
  `HexString`, `Name`, `Array`, `Dictionary`, `Stream`, `Reference`); not part of the public API
  surface, but exposed as `internal` (with `InternalsVisibleTo` the test assembly) so the
  tokenizer/object-model parsing logic can be unit tested directly against hand-written token
  sequences, not only indirectly through full fixture files.
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

### Key Methods

- **`Open(Stream stream)`** / **`Open(string path)`** — `ArgumentNullException.ThrowIfNull` the
  `stream`/`path` first; `Open(string)` additionally rejects an empty or whitespace-only `path`
  with `ArgumentException` (mirroring `PngCodec.Load(string)`'s exact null-then-empty check
  order, but checking `string.IsNullOrWhiteSpace` rather than only length, per this system's
  request-specific hard constraint). `Open(Stream)` reads the stream fully into an in-memory
  buffer without ever calling `Dispose`/`Close` on the caller's stream. `Open(string)` opens its
  own internal `FileStream`, reads it fully, and closes it (via `using`) before parsing —
  mirroring `PngCodec.Load(string)`'s open/consume/close pattern exactly — then calls the same
  shared private parse implementation the stream overload uses. Parsing runs the tokenizer,
  object model, cross-reference resolution (classic/stream/ObjStm/hybrid, with the linear-scan
  fallback), `/Encrypt` detection, and page-tree traversal exactly once, producing a fully
  resolved `PdfDocument` instance.
- **`PageCount` (get)** — disposed-check, then returns `_pages.Count`.
- **`GetPageInfo(int pageIndex)`** — disposed-check, then `ArgumentOutOfRangeException` for
  `pageIndex < 0 || pageIndex >= PageCount`, then returns `_pages[pageIndex]` (already computed
  during `Open`).
- **`Render(int pageIndex, int width, int height)`** — disposed-check and `pageIndex`
  range-check identical to `GetPageInfo`, then builds `new Surface(width, height)`, resolves the
  page's leaf node, raw `/MediaBox` origin, and inherited `/Resources` via `ResolvePageDetails`,
  derives the page's base CTM via `BuildBaseCtm` (see _Content-Stream Interpreter_ below),
  resolves the page's `/Contents` bytes via `ResolvePageContentBytes`, executes them via
  `ExecuteContentStream` (passing the resolved `/Resources`), and returns the painted surface.
  `Surface`'s own constructor supplies the `width`/`height` `ArgumentOutOfRangeException`/
  `MaxDimension` contract; this is deliberately not duplicated here, and the caller-specified
  `width`/`height` is used exactly as given — it is never clamped to, or derived from, the page's
  own `/MediaBox` size.
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
  — mirroring `PngCodec.Zlib.cs`'s established zlib-header/Adler-32-trailer/DEFLATE-payload
  decompression pattern), merges a hybrid `/XRefStm` link's entries with its paired classic
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
  implements (graphics-state, path-construction, path-painting, device-color, and
  image-XObject), silently doing nothing for any other keyword (text, clipping, `gs`
  (ExtGState), shading, inline images, and any other operator not yet implemented).
- **`ResolvePageContentBytes(PdfObject pageNode)` (`PdfDocument.ContentStream.cs`)** — resolves
  `/Contents`: a single stream is decoded directly via `GetStreamDecodedBytes`; an array of
  streams is decoded entry-by-entry and concatenated with a single space byte inserted between
  each entry (per the PDF specification's own requirement, so adjacent tokens from different
  streams can never merge); a page with no `/Contents` key returns an empty array (rendered as a
  blank page, exactly as every page did in Phase 1).
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
  current graphics state's `FillColor` and the operator's documented fill rule; strokes via
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
- **Device color operators (`PdfDocument.Color.cs`, added in Phase 3)** — a private
  `PdfColorSpaceKind` enum (`DeviceGray`/`DeviceRGB`/`DeviceCMYK`) tracks
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
  documented `cs`/`CS` reset rule) — any other resolved color space (`Indexed`/`Separation`/
  `DeviceN`/`ICCBased`/`CalRGB`/`CalGray`/`Lab`, or an undeclared name) throws
  `Codecs.UnsupportedImageFeatureException`, naming the unsupported space. `OpSetColorFill`/
  `OpSetColorStroke` (`sc`/`SC`/`scn`/`SCN`) require exactly `ComponentCount` numeric operands for
  the current color space; a trailing `Name` operand (the `/Pattern` form) throws
  `Codecs.UnsupportedImageFeatureException` rather than being interpreted as a component. Every
  malformed operand count/type throws `InvalidDataException`, matching every other operator in
  this class.
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
  directly). Any other filter name (including `DCTDecode`, which `PdfDocument.Images.cs` always
  detects and bypasses before calling this pipeline; and `CCITTFaxDecode`/`JPXDecode`/
  `JBIG2Decode`/`Crypt`, which remain out of scope) throws
  `Codecs.UnsupportedImageFeatureException`; a malformed `/Filter`/`/DecodeParms` shape, an
  unrecognized `/Predictor` value, or malformed bytes for any of the five supported filters
  (missing EOD marker, invalid/out-of-range code or character, truncated run, or an out-of-range
  ASCII85 group value) throws `InvalidDataException`.
- **Image XObjects (`PdfDocument.Images.cs`, added in Phase 3)** — `OpDrawXObject` (`Do`)
  resolves a named XObject from the current page's `/Resources/XObject` dictionary; a
  `/Subtype /Form` XObject throws `Codecs.UnsupportedImageFeatureException` (explicit fail-closed,
  not silently skipped — Form XObject rendering is out of this phase's scope); a
  `/Subtype /Image` XObject is decoded (`DecodeImageXObject`) and composited onto `_surface`
  through `_gs.CurrentTransform` (`CompositeImageOntoSurface`). `DecodeImageXObject` detects a
  bare `/Filter /DCTDecode` image and decodes its raw bytes directly via
  `Codecs.JpegCodec.Load(Stream)` (bypassing the general Flate+predictor pipeline entirely,
  trusting `JpegCodec`'s own decoded width/height over the PDF `/Width`/`/Height` as a documented
  leniency); any other supported case decodes via `GetStreamDecodedBytes` and interprets the raw
  samples per `/ColorSpace` (device spaces only, reusing `ColorFromComponents`) and
  `/BitsPerComponent` (`8` only — anything else throws
  `Codecs.UnsupportedImageFeatureException`); `/SMask`/`/Mask` are never consulted (every decoded
  image is treated as fully opaque, a documented Phase 3 limitation).
  `CompositeImageOntoSurface` inverts the CTM (a non-invertible/degenerate CTM silently paints
  nothing), computes the device-space bounding box of the transformed unit square, and for every
  destination pixel in that box nearest-neighbor-samples the source image (`row = floor((1 - v) *
  image.Height)`, since image sample row `0` is the _top_ of the unit square per the PDF
  specification's image-space convention, the opposite of user-space's y-up convention) — no
  bilinear interpolation, a documented Phase 3 simplification consistent with Phase 2's own
  stroke-width simplification precedent.
- **Font resolution (`PdfDocument.Fonts.cs`, added in Phase 4, fallback branch rewritten in
  Phase 6)** — `ResolveFont(PdfObject fontResource)` looks up (and caches, via `_fontCache`) a
  font dictionary's `Fonts.TrueTypeFont`, `/Encoding`, and `/Widths`/`/MissingWidth`. Only
  `/Subtype /TrueType` is supported; `/Type0`, `/Type1`, `/MMType1`, and `/Type3` each throw
  `Codecs.UnsupportedImageFeatureException` naming the rejected subtype. `BuildResolvedFont`
  requires `/FontDescriptor`; when its `FontFile2` entry resolves to a stream, that embedded font
  always wins - decoded via the same `GetStreamDecodedBytes` every other stream in this class
  uses, then loaded via `Fonts.TrueTypeFont.Load(new MemoryStream(decodedBytes))`. Only when
  `FontFile2` is absent does `BuildResolvedFont` call `ResolveFallbackFont` (see _Font Fallback
  Resolution_ immediately below) instead of failing closed. `ResolveEncoding` builds a full
  256-entry `int[]` code-to-Unicode-codepoint map: `ApplyBaseEncoding` seeds it from one of two
  hand-transcribed 256-entry tables (`WinAnsiEncodingTable`/`MacRomanEncodingTable`, the PDF
  specification's own Appendix D tables), defaulting to `/WinAnsiEncoding` when `/Encoding` is
  absent entirely and throwing `Codecs.UnsupportedImageFeatureException` for any other named base
  encoding (`/StandardEncoding`/`/PDFDocEncoding`/anything else); `ApplyDifferences` then applies
  an `/Encoding/Differences` array's `code1 name1 name2 ... code2 name1 ...` run-length overrides,
  resolving each glyph name via `StandardGlyphNames` (a ~240-entry Adobe Glyph List subset
  covering common ASCII/Latin-1 names) - an unrecognized glyph name throws
  `InvalidDataException` (a fail-closed policy, not a silent mis-mapping to codepoint `0`/
  `.notdef`), as does a `/Differences` array beginning with a glyph name before any starting code
  number. `ResolveWidths` builds a sparse `code -> width`
  (`/1000`-scaled) map from `/FirstChar`/`/Widths` (missing/malformed entries silently omitted,
  not rejected), plus `/FontDescriptor/MissingWidth` (defaulting to `0`, the specification's own
  documented default) as the fallback for any code absent from that map.
- **Font fallback resolution (`PdfDocument.FontFallback.cs`, added in Phase 6)** —
  `ResolveFallbackFont(baseFontName, descriptor)` is `BuildResolvedFont`'s sole entry point into
  this file. It first fails closed with `Codecs.UnsupportedImageFeatureException` (feature
  `pdf-font-symbolic-not-embedded`) for `/BaseFont` `Symbol` or `ZapfDingbats`, or for any font
  whose `/FontDescriptor/Flags` declares the `Symbolic` bit (bit 3) without also declaring the
  `Nonsymbolic` bit (bit 6) - a symbol/dingbat glyph set has no meaningful generic-family
  equivalent, so it is never substituted rather than being silently mis-rendered. Otherwise,
  `ResolveFallbackFlavor` classifies the requested serif/fixed-pitch/bold/italic flavor: a
  recognized Standard-14 name (the fixed 12-entry `Standard14Flavors` table, covering
  Helvetica/Times/Courier's four style variants each - `Symbol`/`ZapfDingbats` deliberately
  excluded, since they are rejected above before this table is ever consulted) takes priority and
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
  change between calls or between documents.
- **Text rendering (`PdfDocument.Text.cs`, added in Phase 4)** — `OpBeginText`/`OpEndText`
  (`BT`/`ET`) reset only `_textMatrix`/`_lineMatrix` to the identity matrix (every other text-
  state parameter lives on `GraphicsState` and is untouched, per this phase's documented `q`/`Q`-
  interaction design — see `_textMatrix`/`_lineMatrix` above). `OpSetCharSpacing`/
  `OpSetWordSpacing`/`OpSetHorizontalScaling`/`OpSetLeading`/`OpSetTextRise` (`Tc`/`Tw`/`Tz`/`TL`/
  `Ts`) store their one operand verbatim; `OpSetFont` (`Tf`, a name then a number) resolves the
  named font resource via `ResolveFont` and stores it alongside the requested size;
  `OpSetTextRenderMode` (`Tr`) accepts only mode `0` (fill, the default) and `3` (invisible —
  painted with zero-area geometry, i.e. skipped entirely), throwing
  `Codecs.UnsupportedImageFeatureException` for stroke/clip modes `1`/`2`/`4`-`7` (out of this
  phase's scope) or `InvalidDataException` for any other numeric value. `OpTextMoveTo`/
  `OpTextMoveToSetLeading`/`OpTextNextLine` (`Td`/`TD`/`T*`) and `OpSetTextMatrix` (`Tm`)
  manipulate `_textMatrix`/`_lineMatrix` per the specification's own line-matrix-relative-
  displacement (`Td`/`TD`, `TD` additionally setting `Leading = -ty`) versus direct-replacement
  (`Tm`) semantics; `T*` is exactly `0 -TL Td`. `OpShowText`/`OpShowTextNextLine`/
  `OpShowTextNextLineWithSpacing`/`OpShowTextArray` (`Tj`/`'`/`"`/`TJ`) each ultimately call
  `ShowText`, which throws `InvalidDataException` if no font is currently selected (`Tf` was
  never called), then iterates the string byte-by-byte (composite/multi-byte fonts are out of
  this phase's scope, matching the Type0 rejection above): `ShowGlyph` resolves each byte through
  the selected font's encoding map to a Unicode codepoint, looks up its glyph index/outline/
  advance width via `Fonts.TrueTypeFont`, computes the text-rendering matrix `Trm = [Tfs·Th, 0, 0,
  Tfs, 0, Trise] × Tm × CTM` (row-vector composition, matching `OpConcatMatrix`'s own convention),
  combines it with a `1/UnitsPerEm` glyph-space scale, transforms every glyph outline point
  through the result (`AppendTransformedGlyphOutline`, reimplementing - since it is `private` in
  a different assembly - the exact glyph-outline-to-`Geometry.Path` re-issuing pattern
  `SvgCodec.Text.cs` established), and fills the transformed outline via `Drawing.PathFiller.Fill`
  with `_gs.FillColor` (skipped entirely for render mode `3`). `ResolveGlyphWidth` determines each
  glyph's advance in text space with a documented priority: an explicit `/Widths` entry for that
  code first, else `/FontDescriptor/MissingWidth`, else (only when the font declares neither -
  i.e. `ResolvedFont.Widths` has no entry and `MissingWidth` was never declared) the font's own
  `GetAdvanceWidth`/`UnitsPerEm` metric - then advances `Tm.x` by
  `((w0 - Tj/1000) × Tfs + Tc + (code == 32 ? Tw : 0)) × Th` per the specification (the `Tj/1000`
  term only applies within `TJ`'s array form, via `ApplyTextSpaceAdjustment`; `Tw` only applies to
  the single-byte code `32`, per the specification's own restriction, never a multi-byte code
  that merely decodes to codepoint 32). Every operator validates its operand count/type via the
  same `RequireNumbers`/`RequireOperandCount` helpers every other operator family uses, throwing
  `InvalidDataException` on mismatch.

### Error Handling

- **Null `stream`/`path` argument to `Open`** — `ArgumentNullException`, thrown directly with a
  `nameof(...)` parameter name.
- **Empty/whitespace-only `path` to `Open(string)`** — `ArgumentException`, mirroring
  `PngCodec.Load(string)`'s message wording.
- **Malformed/unresolvable document structure** (tokenizer, object model, xref, or page tree,
  after the linear-scan fallback is exhausted) — `InvalidDataException`, mirroring `PngCodec`'s
  exact convention for malformed data.
- **`/Encrypt` key present in the trailer** — `Codecs.UnsupportedImageFeatureException` (feature
  `"pdf-encrypted"`), thrown directly; the referenced encryption dictionary is never resolved or
  decrypted.
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
- **An unsupported color space** (`cs`/`CS`, or an image XObject's `/ColorSpace`: `Indexed`/
  `Separation`/`DeviceN`/`ICCBased`/`CalRGB`/`CalGray`/`Lab`, an undeclared
  `/Resources/ColorSpace` name, or any other unrecognized value) — `Codecs.UnsupportedImageFeatureException`.
- **`scn`/`SCN` with a trailing pattern name** — `Codecs.UnsupportedImageFeatureException`
  (`/Pattern` color is out of this phase's scope).
- **An unsupported stream filter** (anything other than `FlateDecode`, `LZWDecode`,
  `ASCII85Decode`, `ASCIIHexDecode`, `RunLengthDecode`, or `DCTDecode` combined with another
  filter) — `Codecs.UnsupportedImageFeatureException`; an unrecognized `/Predictor` value, a
  malformed `/Filter`/`/DecodeParms` shape, or malformed bytes for any of the five supported
  filters is `InvalidDataException` instead (malformed, not merely unsupported).
- **An unsupported image `/BitsPerComponent`** (anything other than `8`), or a TIFF predictor
  combined with a non-`8` `/BitsPerComponent` — `Codecs.UnsupportedImageFeatureException`.
- **`Do` on a `/Subtype /Form` XObject** — `Codecs.UnsupportedImageFeatureException`, thrown
  explicitly (not silently skipped, per this phase's hard scope boundary).
- **`Do` with a name undeclared in `/Resources/XObject` (or no `/Resources` at all), a
  non-stream/missing-`/Subtype` resolved value, or a malformed operand count/type** —
  `InvalidDataException` (a malformed content stream, not merely unsupported).
- **A font dictionary whose `/Subtype` is `/Type0`, `/Type1`, `/MMType1`, or `/Type3`** —
  `Codecs.UnsupportedImageFeatureException` (composite/CID-keyed, Type 1/CFF, and Type 3 fonts
  remain entirely out of scope by design). A `/Subtype /TrueType` font lacking an embedded
  `/FontDescriptor/FontFile2` no longer reaches this list at all as of Phase 6 - it is resolved
  via fallback substitution instead (see below), except that a `/BaseFont` of `Symbol` or
  `ZapfDingbats`, or a font whose `/FontDescriptor/Flags` declares `Symbolic` without also
  declaring `Nonsymbolic`, still throws `Codecs.UnsupportedImageFeatureException` (feature
  `pdf-font-symbolic-not-embedded`) - a symbol/dingbat glyph set has no meaningful generic-family
  equivalent and is never substituted with an unrelated system or bundled font.
- **An `/Encoding` naming an unrecognized base encoding** (anything other than
  `/WinAnsiEncoding`/`/MacRomanEncoding`, or their dictionary form's `/BaseEncoding`) —
  `Codecs.UnsupportedImageFeatureException`.
- **`Tr` (text-rendering mode) set to `1`, `2`, or `4`-`7`** (stroke/clip modes) —
  `Codecs.UnsupportedImageFeatureException`; any other numeric value outside `0`-`7` is
  `InvalidDataException` instead.
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
  filter/image bit depth/Form XObject
- `Codecs.JpegCodec` (from the core `CanvasNet` system, new as of Phase 3) — `Load(Stream)`
  decodes an image XObject's bare `DCTDecode` (JPEG) bitstream directly, without requiring
  APP0/JFIF framing
- `Geometry.PathBuilder`/`Path` (from the core `CanvasNet` system) — accumulates the current
  path's subpaths/commands as path-construction operators are dispatched
- `Drawing.PathFiller`/`PathStroker`/`StrokeStyle`/`FillRule`/`LineCap`/`LineJoin` (from the core
  `CanvasNet` system) — rasterize the current path onto `_surface` for every path-painting
  operator, and, as of Phase 4, every filled glyph outline
- `Fonts.TrueTypeFont` (from the core `CanvasNet` system, new as of Phase 4) — loads an embedded
  `/FontFile2` byte stream and resolves each shown codepoint to a glyph index/outline/advance
  width, exactly as `SvgCodec.Text.cs` already uses it for SVG `<text>` rendering
- BCL `System.IO.Compression.DeflateStream` — `FlateDecode` decompression of object streams and,
  as of Phase 3, any other `FlateDecode`-filtered stream (image XObjects included)
- BCL `System.Numerics.Matrix3x2`/`Vector2` — the current transformation matrix, every
  transformed path point, and (as of Phase 3) an image XObject's unit-square-to-device-space
  compositing math

### Callers

None yet - `PdfDocument` is `CanvasNetPdf`'s sole unit and its system's only public entry point;
no other unit or subsystem in this repository calls into it.
