## PdfDocument

![CanvasNetPdf Structure](CanvasNetPdfView.svg)

<!-- cspell:ignore xref startxref endobj endstream ObjStm MediaBox unresolvable -->

`PdfDocument` is distributed as the separate `DemaConsulting.CanvasNet.Pdf` NuGet package
(namespace `DemaConsulting.CanvasNet.Pdf`), which references the core
`DemaConsulting.CanvasNet` package.

The `PdfDocument` class is the sole software unit of the `CanvasNetPdf` system. Its dependencies
are limited to `CanvasNet`'s `Canvas` subsystem (`Surface`, `Rgba32`), `Codecs` subsystem
(`UnsupportedImageFeatureException`), `Geometry` subsystem (`PathBuilder`, `Path`), and `Drawing`
subsystem (`PathFiller`, `PathStroker`, `StrokeStyle`, `FillRule`, `LineCap`, `LineJoin`) — see
the Dependencies section of _CanvasNetPdf System Design_ (`../canvas-net-pdf.md`). It provides
hand-rolled parsing of a PDF document's structure (cross-references, trailer, page tree) and, as
of Phase 2, a content-stream interpreter: `Render` tokenizes and executes a page's `/Contents`
path-construction/painting and graphics-state operators, painting real path geometry onto the
returned `Surface`. **Phase 2 limitation**: every filled/stroked path paints in solid opaque
black — no color space or color-setting operator (`rg`/`g`/`k`/`sc`/`scn` and their stroking
counterparts) is implemented yet, text/font operators, image XObjects, additional stream filters,
and clipping-path operators (`W`/`W*`) are likewise out of scope for this phase and are silently
skipped like any other unrecognized keyword; a later phase is expected to add real color, text,
image, and clipping support.

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
- **`_pageDetails` (`IReadOnlyList<(PdfObject Node, double X0, double Y0, double BoxWidth, double BoxHeight)>`)**
  — the raw (non-rotated, non-swapped) leaf page node and its inherited `/MediaBox`'s origin and
  size, one entry per page in the same document order as `_pages`, populated in lock-step by
  `TraversePageTree` — `Render` needs the raw `/MediaBox` origin (`_pages`/`PdfPageInfo` only
  exposes the already-rotation-swapped display width/height) to correctly derive each page's base
  CTM (see `BuildBaseCtm` below).
- **`_gsStack` (`Stack<GraphicsState>`, nested `GraphicsState` class, `PdfDocument.GraphicsState.cs`)**
  — the `q`/`Q` graphics-state stack, and `_gs` (`GraphicsState`) — the current graphics state
  (current transformation matrix plus line width/cap/join/miter-limit/dash pattern), both reset at
  the start of every `ExecuteContentStream` call. `GraphicsState.Clone()` performs a member-wise
  copy (the dash array reference is shared, never mutated in place, so sharing it across a clone
  is safe).
- **`_pathBuilder` (`Geometry.PathBuilder`), `_currentPoint`/`_subpathStart` (`Vector2`,
  untransformed user-space), `_hasOpenSubpath` (`bool`)** (`PdfDocument.PathOps.cs`) — the path
  currently under construction by the path-construction operators, and the bookkeeping the `v`/
  `y` curve shorthands and `h`'s closepath need in the same untransformed user-space coordinates
  the next operator's own operands arrive in; `_hasOpenSubpath` mirrors `PathBuilder`'s own
  internal "can draw" state purely so a malformed "draw before move" content stream can be
  rejected with this class's own `InvalidDataException` convention rather than letting
  `PathBuilder`'s `InvalidOperationException` escape unwrapped. All three are reset at the start
  of every `ExecuteContentStream` call and cleared after every path-painting operator.
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
  page's leaf node and raw `/MediaBox` origin via `ResolvePageNodeAndMediaBox`, derives the page's
  base CTM via `BuildBaseCtm` (see _Content-Stream Interpreter_ below), resolves the page's
  `/Contents` bytes via `ResolvePageContentBytes`, executes them via `ExecuteContentStream`, and
  returns the painted surface. `Surface`'s own constructor supplies the `width`/`height`
  `ArgumentOutOfRangeException`/`MaxDimension` contract; this is deliberately not duplicated here,
  and the caller-specified `width`/`height` is used exactly as given — it is never clamped to, or
  derived from, the page's own `/MediaBox` size.
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
  correctly translate a `/MediaBox` that does not start at `(0, 0)`.
  `ResolvePageNodeAndMediaBox(int pageIndex)` is `Render`'s own entry point into this data.

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
- **`ExecuteContentStream(byte[] contentBytes, Surface surface, Matrix3x2 baseCtm)`
  (`PdfDocument.ContentStream.cs`)** — resets `_surface`/`_gsStack`/`_gs`/`_pathBuilder`/
  `_currentPoint`/`_subpathStart`/`_hasOpenSubpath`, then tokenizes `contentBytes` exactly like
  the rest of the document, accumulating operands (numbers, names, strings, arrays) until a
  `Keyword` token is reached, at which point `DispatchOperator` is called and the operand list is
  cleared — content-stream operators are always postfix (operands first, operator keyword last).
- **`DispatchOperator(string operatorName, List<PdfObject> operands)`
  (`PdfDocument.ContentStream.cs`)** — a single `switch` over every operator this phase
  implements (graphics-state, path-construction, path-painting), silently doing nothing for any
  other keyword (color, text, image, clipping, and any other operator not yet implemented).
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
  (`f`/`F`/`f*`/`S`/`s`/`B`/`B*`/`b`/`b*`/`n`): fills via `Drawing.PathFiller.Fill` with the fixed
  `OpaqueBlack` color (this phase's only color) and the operator's documented fill rule; strokes
  via `Drawing.PathStroker.Stroke` then fills the resulting outline with the same `OpaqueBlack`
  color, using a `Drawing.StrokeStyle` built from the current graphics state's line width/cap/
  join/miter-limit/dash pattern — every `StrokeStyle` field is passed explicitly (the PDF
  specification's own default miter limit is `10`, not `StrokeStyle`'s own C# default of `4`).
  `DeviceScale` (`sqrt(|det(CTM.Linear)|)`, the CTM with its translation zeroed) converts a
  user-space line width/dash length into device pixel-space, with a `MinimumDeviceLineWidth` of
  one device pixel implementing the specification's "a line width of 0 renders as the thinnest
  renderable line" rule. Every path-painting operator, including `n`, always clears the current
  path afterward (`_pathBuilder.Clear()`); the surrounding graphics state is entirely unaffected
  by that clear, so a subsequent path in the same content stream still sees the same CTM/stroke
  style.

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
- **Any public member called after `Dispose()`** — `ObjectDisposedException`, thrown first via
  `ObjectDisposedException.ThrowIf(_disposed, this)`, before any other validation.

### Dependencies

- `Canvas.Surface` (from the core `CanvasNet` system) — the `Render` return type and every
  path-painting operator's paint destination
- `Canvas.Rgba32` (from the core `CanvasNet` system) — the fixed `OpaqueBlack` fill/stroke color
- `Codecs.UnsupportedImageFeatureException` (from the core `CanvasNet` system) — reused,
  unmodified, for `/Encrypt` detection
- `Geometry.PathBuilder`/`Path` (from the core `CanvasNet` system) — accumulates the current
  path's subpaths/commands as path-construction operators are dispatched
- `Drawing.PathFiller`/`PathStroker`/`StrokeStyle`/`FillRule`/`LineCap`/`LineJoin` (from the core
  `CanvasNet` system) — rasterize the current path onto `_surface` for every path-painting
  operator
- BCL `System.IO.Compression.DeflateStream` — `FlateDecode` decompression of object streams
- BCL `System.Numerics.Matrix3x2`/`Vector2` — the current transformation matrix and every
  transformed path point

### Callers

None yet - `PdfDocument` is `CanvasNetPdf`'s sole unit and its system's only public entry point;
no other unit or subsystem in this repository calls into it.
