## VsdxDocument Unit Design

<!-- cspell:ignore vsdx Visio VisioML xfrm stencil stencils glueable NURBS nurbs -->
<!-- cspell:ignore shapesheet ShapeSheet rrggbb PinX PinY LocPinX LocPinY FlipX FlipY BeginX BeginY -->
<!-- cspell:ignore EndX EndY MasterShape FillForegnd FillBkgnd LineColor LineWeight LinePattern -->
<!-- cspell:ignore FillPattern ShapeSheet TxtPinX TxtPinY TxtLocPinX TxtLocPinY TxtWidth TxtHeight -->

### Purpose

`VsdxDocument` is the sole unit of `CanvasNetVsdx`. It is an OPC (Open Packaging Conventions)
package parser and VisioML rendering codec for Microsoft Visio `.vsdx` diagram packages,
responsible for: opening a `.vsdx` package and resolving its content-type/relationship graph;
enumerating pages and reporting their declared size; resolving a page's full shape tree,
including Master/MasterShape cell-and-geometry-row inheritance, the StyleSheet chain, shape
geometry/transform, text layout, and 1-D connector glue-point resolution; and rendering a
requested page onto a core `CanvasNet.Canvas.Surface`. See _CanvasNetVsdx System Design_
(`canvas-net-vsdx.md`) for the system-level architecture and the Implementation Phase Plan this
unit's own delivery follows.

### Data Model

- **`VsdxDocument`** (the public entry point) — holds the opened `ZipArchive`, the resolved
  content-type/relationship part graph, the parsed `<StyleSheets>`/`<Colors>`/`<FaceNames>` model
  from `visio/document.xml`, the page index parsed from `visio/pages/pages.xml`, a lazily-resolved
  optional theme parsed from `visio/theme/theme1.xml`, and a lazily-resolved, per-master-file cache
  of parsed `<MasterContents>` trees (a page's shape tree is not resolved until `Render` is first
  called for that page, mirroring `PptxDocument`'s own lazy-resolution precedent — see
  _PptxDocument Unit Design_, `canvas-net-pptx/pptx-document.md`).
- **`VsdxPageInfo`** — a resolved page's declared `PageWidth`/`PageHeight` (converted from the raw
  inches value to the document's internal unit, EMU — `914400` EMU per inch, the same constant and
  internal unit already used by `PptxSlideSize`, so the existing EMU-to-pixel/DPI conversion
  pipeline is reused unchanged rather than inventing a separate inches-to-pixels path) and the
  page's declared name.
- **`VsdxShapeNode`** — a resolved node in a page's shape tree: its kind (`Shape`/`Group`/`Guide`),
  its effective (post-Master/MasterShape-merge, post-StyleSheet-chain-resolved) cell set
  (`PinX`/`PinY`/`Width`/`Height`/`LocPinX`/`LocPinY`/`Angle`/`FlipX`/`FlipY`, and for a 1-D shape
  `BeginX`/`BeginY`/`EndX`/`EndY`), its resolved geometry sections (each an ordered list of
  geometry rows — `MoveTo`/`LineTo`/`RelMoveTo`/`RelLineTo`/`EllipticalArcTo`/`NURBSTo`/
  `InfiniteLine` — plus the section-level `NoFill`/`NoLine`/`NoShow` flags), its resolved line/
  fill style (`LineColor`/`LineWeight`/`LinePattern`/`BeginArrow`/`EndArrow`/`FillForegnd`/
  `FillBkgnd`/`FillPattern`), its resolved text (runs with character/paragraph formatting, and the
  text box's own `TxtPinX`/`TxtPinY`/`TxtLocPinX`/`TxtLocPinY`/`TxtWidth`/`TxtHeight`/`TxtAngle`),
  and (for a `Group`) its child `VsdxShapeNode` list.
- **`VsdxConnect`** — a resolved `<Connect>` entry: the connector shape's ID and which endpoint
  (`Begin`/`End`), the target shape's ID, and whether the glue is whole-shape-pin or connection-
  point-indexed (and if so, which connection-point index).
- **`VsdxRenderOptions`** — render-time options: `BackgroundColor` (default opaque white,
  mirroring `PptxRenderOptions`'s own precedent).
- **Supporting exception type**: `VsdxUnsupportedFeatureException` — thrown for a recognized but
  explicitly deferred VisioML construct (an embedded `Foreign` shape, a non-solid `FillPattern`
  beyond the documented subset, or an unrecognized `BeginArrow`/`EndArrow` index), carrying a
  stable feature-token string, mirroring `PptxUnsupportedFeatureException`'s own precedent (see
  _PptxDocument Unit Design_, `canvas-net-pptx/pptx-document.md`). An unrecognized geometry row
  type or a dangling `Connect` target is **not** an exception — per the Risk Control Measures in
  _CanvasNetVsdx System Design_ (`canvas-net-vsdx.md`), these degrade to a tolerant, no-effect
  skip instead.

**Invariants**: `PageCount` and `GetPageSize` are available immediately after `Open` succeeds,
with no page shape tree resolved yet. A `VsdxShapeNode`'s effective cell set is always fully
resolved (no `F="Inh"` cached-inherited cell is ever exposed to a caller unresolved) before it is
handed to geometry/paint/text resolution. A `Group` node's children's `PinX/PinY/.../Angle` cells
are expressed in the group's own local box, per §10.1 of the format research underlying this
design, not a separately-scaled child coordinate system — composing a child's transform into the
page therefore means repeatedly applying each ancestor's own local-to-parent transform, walking
from the leaf up to the page, with no separate `chOff`/`chExt`-style remap step.

### Key Methods

- **`Open(Stream stream)`** / **`Open(string path)`**: Validates the argument (`ArgumentNullException`
  for null, `ArgumentException` for an empty/whitespace path), opens the ZIP as a read-only
  `ZipArchive`, resolves `[Content_Types].xml` and the `_rels/.rels` → `visio/document.xml` →
  `masters.xml`/`pages.xml`/`theme1.xml` relationship chain, parses `visio/document.xml`'s
  `<Colors>`/`<FaceNames>`/`<StyleSheets>` and `visio/pages/pages.xml`'s page index. Throws
  `InvalidDataException` for an unreadable/corrupt ZIP or a package missing a required part.
  Postcondition: `PageCount`/`GetPageSize` are resolvable; no page's shape tree is parsed yet.
- **`PageCount`** (property): Returns the number of entries in the parsed page index.
- **`GetPageSize(int pageIndex)`**: Returns the requested page's `VsdxPageInfo`. Precondition:
  `Open` has succeeded. Throws `ArgumentOutOfRangeException` for `pageIndex` outside
  `[0, PageCount)`.
- **`ResolveMasterShape(string masterId)`** (internal): Resolves a `Master="N"` reference through
  `masters.xml`'s `<Master ID="N">` element, its child `<Rel r:id="...">`, and
  `masters/_rels/masters.xml.rels`, to the target `masterN.xml` part — always through this
  relationship chain, never by filename-number convention (Master IDs are sparse/non-contiguous
  and never align 1:1 with the physical `masterN.xml` filename). Caches the parsed
  `<MasterContents>` tree per resolved master file.
- **`ResolveEffectiveShape(XElement instanceShape, XElement? masterShape)`** (internal): Implements
  the cell/geometry-row merge algorithm — an instance cell with a non-`"Inh"` `F` wins; an
  instance cell with `F="Inh"` or no cell at all falls through to the master's resolved value; a
  geometry row present in the instance (matched by `IX`) replaces the master's row of the same
  `IX`; a row with `Del="1"` marks the master's corresponding row deleted; a row in the master
  with no matching instance `IX` and no `Del` is inherited verbatim. For a `Group`-typed master
  shape, recurses per-child via the child's own `MasterShape` attribute (matched against the
  master's nested `<Shapes>` by `ID`), not by position.
- **`ResolveStyleChain(string styleSheetId, string cellName)`** (internal): Walks a shape's
  `LineStyle`/`FillStyle`/`TextStyle` StyleSheet-ID reference up the StyleSheet chain (each
  StyleSheet's own `LineStyle`/`FillStyle`/`TextStyle` attributes identify its own parent for that
  facet) until a literal, non-`"Inh"` value is found — guaranteed to terminate at StyleSheet ID
  `0` ("No Style"), which the format research confirms has no `F="Inh"` cells of its own. A
  literal value of `"Themed"` resolves through the optional parsed theme instead, falling back to
  a neutral default when no theme part is present or the referenced scheme entry is absent (see
  the Design Constraints deferral in _CanvasNetVsdx System Design_, `canvas-net-vsdx.md`).
- **`ResolveTransform(VsdxShapeNode shape, Point2 local)`** (internal): Applies the shape-local-
  to-parent affine transform — translate by `-LocPinX/-LocPinY`, apply `FlipX`/`FlipY` before
  rotation, rotate by `Angle` (radians, counter-clockwise), translate by `PinX/PinY` — identical
  for a 2-D shape and a 1-D (connector) shape, since a 1-D shape's `PinX/PinY/Width/Height/Angle`
  are themselves pre-derived from its `BeginX/Y`/`EndX/Y` endpoints by Visio at save time.
- **`ResolveGeometry(VsdxShapeNode shape)`** (internal): Converts each resolved geometry section's
  rows into a `Geometry.Path`, scaling `RelMoveTo`/`RelLineTo`'s normalized `[0,1]` coordinates by
  `Width`/`Height`, converting `EllipticalArcTo`/`NURBSTo` rows to Bezier segments, and skipping
  (not throwing on) an unrecognized row type.
- **`ResolveConnectors(VsdxDocument document, IReadOnlyList<XElement> connects)`** (internal):
  Resolves each `<Connect>` entry's connector shape and target shape; since a connector's own
  `BeginX/BeginY`/`EndX/EndY` cells are already pre-baked, resolved page-space coordinates (by
  Visio at save time) for both whole-shape-pin and connection-point-indexed glue, this is a direct
  lookup with no live glue-tracking math required for a static render.
- **`Render(int pageIndex, int width, int height, VsdxRenderOptions? options)`**: Validates
  `pageIndex`/`width`/`height` (`ArgumentOutOfRangeException` on violation), resolves the
  requested page's shape tree (first call for that page; cached thereafter), clears a new
  `width`x`height` `Surface` to `options.BackgroundColor`, then walks the shape tree in document
  order — document order is z-order, first shape drawn first/bottom, the same convention already
  established by `PptxDocument`/`PdfDocument` — painting each node's resolved fill, stroke,
  connector line/arrowheads, and text. Returns the painted `Surface`.
- **`Render(int pageIndex, int dpi, VsdxRenderOptions? options)`**: Resolves the page's
  `VsdxPageInfo` size, converts it to pixel dimensions at the requested `dpi`, and delegates to
  the pixel-dimension overload above.

### Error Handling

`ArgumentNullException` and `ArgumentException` are thrown synchronously from `Open` for a
programming-error argument (null stream/path, empty/whitespace path) — these are never caught or
converted internally. `ArgumentOutOfRangeException` is thrown synchronously from `GetPageSize`/
`Render` for an out-of-range `pageIndex` or a non-positive/over-`Surface.MaxDimension` pixel size.
`InvalidDataException` is thrown for malformed package/XML structure that prevents correct
resolution at all (an unreadable ZIP, a missing required part, a Master/MasterShape or group-
nesting cycle, or a nesting-depth/shape-count budget exceeded) — these represent input the unit
cannot safely process at all, as distinct from `VsdxUnsupportedFeatureException`, thrown for a
recognized-but-deferred construct (see the Data Model section above), which is caught and
degraded to a tolerant skip at the shape/row level wherever the format research's Risk Control
Measures call for tolerance, and only propagates to the caller when no safe default exists.

### Dependencies

- **`CanvasNet.Canvas`** — `Surface` (destination raster), `Rgba32` (resolved line/fill/text
  colors, including built-in palette index and document `<Colors>` table resolution)
- **`CanvasNet.Geometry`** — `Path`/`PathBuilder` (resolved shape/connector outlines), `Rect`
  (resolved bounding boxes)
- **`CanvasNet.Drawing`** — `PathFiller`/`PathStroker` (rasterizing resolved paths/strokes/
  arrowheads), `Gradient`/`LinearGradient`/`GradientStop` (best-effort linear fill-pattern
  approximation)
- **`CanvasNet.Fonts`** — `TrueTypeFont`/`SystemFontCatalog` (resolving a `Section N="Character"`
  row's `Font` name hint and extracting glyph outlines for shape/connector text)
- **`CanvasNet.Codecs`** — reserved for the deferred embedded-image/`Foreign`-shape capability;
  not exercised by this unit's in-scope feature set (see _CanvasNetVsdx System Design_'s Design
  Constraints section, `canvas-net-vsdx.md`)
- **PPTX theme-parsing infrastructure** — `VsdxDocument`'s optional `visio/theme/theme1.xml`
  parsing reuses `CanvasNetPptx`'s existing OOXML DrawingML `<a:theme>`/`<a:clrScheme>` parsing
  logic where structurally identical (both are the same OOXML theme part format); this is a
  source-level reuse of parsing logic, not a project/package dependency on `CanvasNetPptx` itself
  — see _CanvasNetVsdx System Design_'s Dependencies section (`canvas-net-vsdx.md`) for why
  `CanvasNetVsdx` must never take an actual package dependency on `CanvasNetPptx`.

### Callers

`VsdxDocument` is the sole public entry point of `CanvasNetVsdx`; it is called directly by
consuming applications (there is no other unit or subsystem within this repository that calls
it, identical to `SvgCodec`'s and `PdfDocument`'s own single-unit-system precedent). System-level
integration tests in `test/DemaConsulting.CanvasNet.Vsdx.Tests/VsdxSystemIntegrationTests.cs`
exercise it purely through its public API, mirroring `PptxSystemIntegrationTests.cs`'s own
established pattern.
