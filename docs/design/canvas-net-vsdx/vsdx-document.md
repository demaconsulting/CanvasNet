## VsdxDocument Unit Design

![CanvasNetVsdx Structure](CanvasNetVsdxView.svg)

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
  content-type/relationship part graph, the parsed `<StyleSheets>` model from
  `visio/document.xml` (an earlier revision of this design also claimed a parsed `<Colors>`/
  `<FaceNames>` model here; Milestone 8 confirmed via repository-wide search that neither element
  is parsed anywhere in this package's source and corrected this passage accordingly), the page
  index parsed from `visio/pages/pages.xml`, a lazily-resolved optional theme parsed from
  `visio/theme/theme1.xml`, and a lazily-resolved, per-master-file cache
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
  geometry rows — `MoveTo`/`LineTo`/`RelMoveTo`/`RelLineTo`/`EllipticalArcTo`/`ArcTo` (Milestone 11
  added `EllipticalArcTo`/`ArcTo` conversion to an `ArcTo` path command, see `ResolveGeometry`
  below) are converted to path segments, and every other row type including `NURBSTo`/
  `InfiniteLine` is tolerantly skipped
  (see Risk Control Measures in `canvas-net-vsdx.md`) — plus the section-level `NoFill`/`NoLine`/
  `NoShow` flags), its resolved line/
  fill style (`LineColor`/`LineWeight`/`LinePattern`/`BeginArrow`/`EndArrow`/`FillForegnd`/
  `FillBkgnd`/`FillPattern`/`HideText` — Milestone 11 added `HideText` resolution, see `Render`
  below; Milestone 11 also added recognition of `BeginArrow`/`EndArrow` index `4` (a solid, filled
  triangle, mapped to the same `VsdxArrowheadStyle.Arrow` style as index `2`) and index `254` (an
  open/unfilled triangle outline resolved through a `USE("Navigable")` named-cell formula, mapped
  to the new `VsdxArrowheadStyle.HollowTriangle` style) — see `VsdxArrowhead.cs`'s own remarks
  below), its resolved text (runs with character/paragraph formatting, including a `<fld>`
  Field-reference element's own nested text content treated as a literal run - Milestone 11, see
  `BuildRawRuns`/`CanvasNetVsdx-VsdxDocument-TextRunParsing` - and the
  text box's own `TxtPinX`/`TxtPinY`/`TxtLocPinX`/`TxtLocPinY`/`TxtWidth`/`TxtHeight`/`TxtAngle`),
  and (for a `Group`) its child `VsdxShapeNode` list.
- **`VsdxConnect`** — a resolved `<Connect>` entry: the connector shape's ID and which endpoint
  (`Begin`/`End`), the target shape's ID, and whether the glue is whole-shape-pin or connection-
  point-indexed (and if so, which connection-point index).
- **`VsdxRenderOptions`** — render-time options: `BackgroundColor` (default opaque white,
  mirroring `PptxRenderOptions`'s own precedent).
- **Supporting exception type**: `VsdxUnsupportedFeatureException` — defined (mirroring
  `PptxUnsupportedFeatureException`'s own precedent, see _PptxDocument Unit Design_,
  `canvas-net-pptx/pptx-document.md`) for a future recognized-but-deferred VisioML construct
  requiring a hard failure, carrying a stable feature-token string. Not currently thrown by any
  resolver in this delivery: a non-solid `FillPattern` beyond the documented subset, an
  unrecognized `BeginArrow`/`EndArrow` index, an unrecognized geometry row
  type, and a dangling `Connect` target all instead degrade to a tolerant, no-effect skip — per
  the Risk Control Measures in _CanvasNetVsdx System Design_ (`canvas-net-vsdx.md`).

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
  `<StyleSheets>` and `visio/pages/pages.xml`'s page index (not `<Colors>`/`<FaceNames>` — see
  the Data Model section's own correction note above). Throws
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
  geometry row present in the instance (matched by `IX`) is merged with the master's row of the
  same `IX` **cell-by-cell**: for every cell name the instance row itself actually carries (literal
  or marked `F="Inh"` — Milestone 11's quality-retry cycle (retry 1) confirmed a geometry-row
  coordinate cell is itself formula-derived from the shape's own `Width`/`Height` and so is
  typically cached as `Inh`, never literal, unlike an ordinary authored cell — see below), the
  instance's own cell always wins; a cell name genuinely absent from the instance row (never
  declared at all, not merely `Inh`-cached) falls through to the master row's same-named cell —
  Milestone 11 corrected an earlier, since-superseded whole-row-replacement implementation, see
  below); a row with `Del="1"` marks the master's corresponding row deleted; a
  row in the master with no matching instance `IX` and no `Del` is inherited verbatim. For a
  `Group`-typed master
  shape, recurses per-child via the child's own `MasterShape` attribute (matched against the
  master's nested `<Shapes>` by `ID`), not by position. A group child `<Shape Del="1">` stub
  (shape-level, not the row-level `Del="1"` above) is excluded from the resolved shape tree
  entirely — Milestone 10 confirmed against `60973.vsdx` that an unhandled shape-level `Del="1"`
  stub was rendering its own stale, superseded placeholder text (`VsdxDocument.Shapes.cs`'s
  `ParseShapeElements`) — specifically the `"OOB: N/A"`/`"[B] Lo1: N/A"`/`"TS: N/A"` stale-stub
  clutter text in that same document; this fix does **not** address the separate, deeper
  `AIRttt`/`AIRuuu` duplicate-text defect in the same document, which is a distinct, pre-existing
  issue unrelated to any shape-level `Del="1"` stub — see `canvas-net-vsdx.md`'s Design
  Constraints section for that defect's own documented, deferred-limitation entry. Milestone 10
  also narrows the generic "instance `F="Inh"` cell falls through to the master" rule above for
  exactly the nine transform cells `PinX`/`PinY`/`Width`/
  `Height`/`LocPinX`/`LocPinY`/`Angle`/`FlipX`/`FlipY`: an instance's own cached cell for one of
  these nine is always preferred over the master's same-named cell, even when `F="Inh"` — but
  **only when the shape is identified as 1-D** (both a `BeginX` and an `EndX` cell present on the
  merged result — the same detection convention `VsdxDocument.Groups.cs`'s own
  `ConnectorEndpoints` resolution uses) **or when the shape is itself a Group child** (Milestone 12,
  Finding #3 — see below). For a 1-D shape, these nine cells are themselves baked
  per-instance, pre-derived from that instance's own `BeginX`/`BeginY`/`EndX`/`EndY` endpoints, and
  the master's own same-named cell is only that master's unrelated template-local default
  position/size, never a value any instance should adopt. A **top-level** (non-Group-child) 2-D
  shape (no `BeginX`/`EndX` cell) is deliberately **excluded** from this overlay: for a top-level
  2-D shape, the master's own same-named cell
  genuinely can be the shared, legitimately-inherited value (a shape never locally moved/resized by
  the author), so the generic "master's cell wins over an inherited instance cell" rule above
  remains correct and is not overridden merely because the instance happens to carry its own
  stale/cached cell for one of these nine names — confirmed by a dedicated unit test
  (`MasterInheritance_2DShapeInhMarkedTransformCell_DefersToMasterValue`) proving a 2-D shape's
  legitimate full Master-inheritance-of-position is unaffected by this overlay. This refinement
  (`VsdxDocument.CellMerge.cs`'s `TransformCellNames`/`PreferInstanceCells`, generalized by
  Milestone 11's quality-retry cycle, retry 1, from an original `PreferInstanceTransformCells`
  taking a fixed cell-name list to a reusable helper taking any cell-name array — see below) was
  discovered during Milestone 10's own root-cause investigation of its Bug 2 (missing connector
  lines): the stroke-width floor described under `Render(...)` below was, on its own,
  insufficient against `60973.vsdx`'s connector shape `802`, because the generic merge rule was
  also collapsing that connector's resolved position to the master's own small, unrelated
  template-local corner — a deviation beyond this milestone's originating plan report's own
  stroke-width-only diagnosis, confirmed via the milestone's smoke-test visual-comparison
  procedure. Milestone 10 retry 1 (quality Finding #3) corrected an initial, unscoped
  implementation that applied this overlay to every shape regardless of dimensionality — narrowed
  to the 1-D-only scope documented here, matching this paragraph's own rationale, which was always
  1-D-specific. Milestone 11's own real-world-corpus validation traced two independent findings
  (`60973.vsdx`'s rack-mount frame container, `44501e.vsdx`'s connector-label shape `ID='45'`
  "end1_name") to this same 2-D-exclusion rule discarding a legitimately-cached, genuinely
  different instance position in favor of the Master's unrelated template-local position, landing
  each shape overlapping/inside a sibling instead of at its own correct position — see
  `canvas-net-vsdx.md`'s Design Constraints section for this confirmed-but-deferred limitation's
  own entry (no safe, narrowly-scoped disambiguation from the overlay's own original, still-locked
  legitimate-2-D-inheritance case was identified within this milestone's scope).

  Milestone 11 also corrected the geometry-row-merge algorithm described above from a
  whole-row-replacement to a cell-by-cell merge (`VsdxDocument.CellMerge.cs`'s
  `MergeGeometryRows`): confirmed necessary against several real-world fixtures whose matched
  instance row overrode only one or two of its own cells (for example only `X`, leaving `Y` to
  inherit from the master row) — the prior whole-row-replacement behavior was silently discarding
  the master row's other, legitimately-inherited cell values instead of merging them, collapsing
  the un-overridden coordinate to its CLR default (`0`) rather than the master's own intended
  value, producing a visibly wrong outline point; see
  `MasterInheritance_GeometryRowPartialOverride_MergesCellByCellNotWholesale` for the locking test
  added.

  Milestone 11's quality-retry cycle (retry 1) then uncovered, and fixed, a deeper regression in
  that same cell-by-cell merge: because a geometry-row coordinate cell is formula-derived from the
  shape's own `Width`/`Height` and so is typically cached as `F="Inh"` rather than literal, the
  cell-by-cell merge's own "instance's own literal cell wins, else the master row's own cell" rule
  (unchanged from the paragraph above) was treating an `Inh`-marked instance row cell identically
  to a genuinely absent one — discarding the instance's own, correctly-sized coordinate in favor
  of the master row's own, differently-scaled cached template value whenever the instance had no
  literal override for that cell, which is the common case for geometry coordinates. Confirmed
  against `60489.vsdx`'s Shape `ID='114'` (an ellipse instance glued to a Master via a distinct,
  larger instance `Width`/`Height`, every one of its own geometry-row cells cached as `Inh`): the
  pre-fix rule rendered the Master's own smaller cached radius for every row, fusing Shape 114 and
  its sibling Shape 122 (distinctly-sized instances of the same Master) into a single,
  wrongly-proportioned blob instead of two independently-sized ellipses. `MergeGeometryRow` now
  overlays every cell name an instance row itself carries (literal or `Inh`) unconditionally over
  the matched master row's own cell — reusing the same `PreferInstanceCells(merged, instanceCells,
  names)` helper the transform-cell overlay above now shares (generalized from the original
  `PreferInstanceTransformCells`, which took a fixed cell-name list), applied here with the
  instance row's own full cell-name set rather than a fixed array, and with no 1-D/2-D gate (a
  geometry row's own coordinates are per-instance-derived for every shape, 1-D or 2-D alike,
  unlike the nine transform cells above, which are only per-instance-derived for a 1-D shape) — a
  cell name genuinely absent from the instance row (never declared at all, not merely `Inh`-cached)
  still falls through to the master row's own value exactly as before, confirmed by a dedicated
  companion test
  (`MasterInheritance_GeometryRowCellGenuinelyAbsentOnInstance_StillFallsThroughToMaster`). This
  same cycle also generalized `PreferInstanceCells` itself to accept the four arrowhead-decoration
  cells `BeginArrow`/`EndArrow`/`BeginArrowSize`/`EndArrowSize` (a new `ArrowCellNames` array,
  applied through the existing 1-D-only gate alongside `TransformCellNames`) — a defensive
  extension mirroring the transform cells' own rationale, planned alongside this fix rather than
  because any fixture in this unit's corpus was confirmed to require it (this cycle's own
  investigation of a related arrowhead-rendering quality finding traced both of that finding's
  observed symptoms to causes other than a missing arrowhead-cell overlay — see
  `canvas-net-vsdx.md`'s Design Constraints section for the full evidence trail). Overlaying an
  arrowhead cell required writing the overlaid cell back as a literal (dropping any `"Inh"`
  marking), unlike the transform-cell overlay: the arrowhead cells are resolved through the same
  StyleSheet-chain-aware `Line*`-category precedence every other `Line*` cell uses
  (`ResolveLineCellValue`'s own `TryGetLiteral` check), which treats any `Inh`-marked cell as "not
  a genuine override, defer to the StyleSheet chain instead" — an overlay that preserved the `Inh`
  marking verbatim would have had no observable effect on the resolved arrowhead at all, confirmed
  by this cycle's own locking tests
  (`ArrowheadResolution_InstanceInhMarkedArrowCell_PreferredOverMasterValue`,
  `ArrowheadResolution_2DShapeInhMarkedArrowCell_DefersToMasterValue`).

  Milestone 12 (Finding #3) extended the `TransformCellNames` overlay above to apply
  unconditionally to a 2-D shape that is itself a **Group child** (`VsdxShapeNode.Parent` is not
  `null`, passed into `MergeCells(instanceCells, masterCells, isGroupChild)` as its new
  `isGroupChild` parameter, wired from `VsdxDocument.Groups.cs`'s `ResolveShapeRecursive` via
  `parent is not null`), independent of the 1-D/`BeginX`/`EndX` test: a Group child's own
  `PinX`/`PinY`/`Width`/`Height`/`LocPinX`/`LocPinY` cells can be baked, per-instance, from the
  enclosing Group's own resize — the same way a 1-D connector's transform cells are baked from its
  own endpoints — confirmed by `Test_Visio-Some_Random_Text.vsdx`'s "View" Group (Shape `ID='5'`,
  children `ID='6'`/`'7'`): the page instance resizes the Group larger than its Master's own
  template default, and both children's own `Width`/`Height`/`PinY` cells are marked `F="Inh"`
  (baked from formulas referencing the enclosing Group's own resized dimension). Before this fix,
  the generic rule let the Master's stale default win, corrupting the resolved transform and, via
  `VsdxDocument.TextBox.cs`'s `TxtWidth`/`TxtHeight` fallback, the resolved text box's available
  width/height — causing severe premature word-wrap. This is the identical gap Milestone 11's own
  Design Constraints deferred-limitation entry previously documented as having "no safe
  narrowly-scoped disambiguating heuristic"; the Group-child `Parent is not null` signal is that
  disambiguating condition. The **top-level** (non-Group-child) 2-D shape exclusion described
  above remains unaffected and is still locked by
  `MasterInheritance_2DShapeInhMarkedTransformCell_DefersToMasterValue`. The `ArrowCellNames`
  overlay is deliberately **not** extended to Group children (no evidence an arrowhead cell has
  the same Group-child problem) — it remains 1-D-only, exactly as described above.
- **`ResolveStyleChain(string styleSheetId, string cellName)`** (internal): Walks a shape's
  `LineStyle`/`FillStyle`/`TextStyle` StyleSheet-ID reference up the StyleSheet chain (each
  StyleSheet's own `LineStyle`/`FillStyle`/`TextStyle` attributes identify its own parent for that
  facet) until a literal, non-`"Inh"` value is found — guaranteed to terminate at StyleSheet ID
  `0` ("No Style"), which the format research confirms has no `F="Inh"` cells of its own. A
  literal value of `"Themed"` resolves through the optional parsed theme instead, falling back to
  a neutral default when no theme part is present or the referenced scheme entry is absent (see
  the Design Constraints deferral in _CanvasNetVsdx System Design_, `canvas-net-vsdx.md`).
  Milestone 10 additionally resolves `FillForegndTrans`/`LineColorTrans` through this same
  `FillStyle`/`LineStyle` chain (a literal `"0..1"` transparency fraction, `0` fully opaque, `1`
  fully transparent) and modulates the resolved `FillForegnd`/`LineColor` color's own alpha
  channel accordingly (`VsdxDocument.Paint.cs`'s `ApplyTransparency`) — previously unconsulted
  anywhere in this resolver, confirmed against `60973.vsdx`'s "Virtual Devices" container shape,
  whose literal 40%-transparent fill was rendering fully opaque and obscuring its own children.
  Milestone 10 retry 1 (quality Finding #1) independently re-verified a claimed regression on
  `github260.vsdx`'s literal `LineColorTrans="1"` shapes (a Lucidchart export convention, this
  fixture carries no `theme1.xml` part at all) by directly pixel-scanning the actual
  Visio-reference PNG at the exact coordinates of every such shape: none of them carries a visible
  border in real Visio, confirming the unconditional alpha-zero interpretation for `V="1"` is
  spec-correct, not a regression — no code change was warranted; see
  `VsdxStyleResolutionTests.cs`'s `StyleResolution_LineColorTrans_FullyTransparent_HasNoVisibleStroke`/
  `StyleResolution_FillForegndTrans_FullyTransparent_HasNoVisibleFill` for the locking tests added.
- **`ResolveTransform(VsdxShapeNode shape, Point2 local)`** (internal): Applies the shape-local-
  to-parent affine transform — translate by `-LocPinX/-LocPinY`, apply `FlipX`/`FlipY` before
  rotation, rotate by `Angle` (radians, counter-clockwise), translate by `PinX/PinY` — identical
  for a 2-D shape and a 1-D (connector) shape, since a 1-D shape's `PinX/PinY/Width/Height/Angle`
  are themselves pre-derived from its `BeginX/Y`/`EndX/Y` endpoints by Visio at save time.
- **`ResolveGeometry(VsdxShapeNode shape)`** (internal): Converts each resolved geometry section's
  `MoveTo`/`LineTo`/`RelMoveTo`/`RelLineTo` rows into a `Geometry.Path`, scaling `RelMoveTo`/
  `RelLineTo`'s normalized `[0,1]` coordinates by `Width`/`Height`. Milestone 11 additionally
  converts an `EllipticalArcTo` row (a general ellipse/arc segment, cells `X`/`Y`/`A`/`B`/`C`/`D`)
  and an `ArcTo` row (a circular, bow-height-derived arc segment, cells `X`/`Y`/`A`) to an `ArcTo`
  path command reaching the row's own destination point (`AppendEllipticalArcTo`/
  `AppendArcTo`/`TryResolveEllipticalArc`) — a zero-bow `ArcTo` row (`A=0`) degrades to a plain
  `LineTo` to its destination point rather than throwing, since a true zero-radius arc has no
  well-defined curvature. Every other row type, including `NURBSTo`/`InfiniteLine`, is still
  skipped (not throwing on) rather than converted.
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
  connector line/arrowheads, and text. Returns the painted `Surface`. Milestone 10 adds two
  rendering-fidelity refinements: (1) a shape whose effective `NonPrinting` cell resolves `true`
  skips its own self-paint (fill, stroke, text, arrowheads) entirely, but recursion into its
  children still proceeds unaffected — confirmed against `44501b.vsdx`'s Watermark Title shape,
  whose `NonPrinting` cell was previously unconsulted and so painted a spurious "Activity"
  heading never shown by Visio itself; and (2) `PaintShapeGeometry`'s stroke paint applies a
  `MinStrokeWidthPixels = 1f` floor to the resolved `LineWeight`-in-pixels value before stroking,
  so a sub-pixel line weight (confirmed against `60973.vsdx`'s connector shapes, whose resolved
  `LineWeight` rasterizes to roughly half a device pixel at the smoke-test's 150 DPI) still paints
  a visible hairline instead of rasterizing to nothing. Milestone 11 adds a third refinement:
  (3) a shape whose effective `HideText` cell (`VsdxDocument.Paint.cs`'s `ResolveHideText`)
  resolves `true` skips only its own text-run paint call, leaving its own fill/stroke/arrowheads
  and its children's painting unaffected — confirmed against `60489.vsdx`'s actor-label shapes,
  whose `HideText` cell was previously unconsulted and so painted a duplicate copy of a sibling
  shape's already-correctly-positioned label text.
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
  colors, including built-in palette index, literal hex, and `"Themed"`-sentinel resolution — not
  a document-declared `<Colors>` table, which is never parsed; see the Data Model section's own
  correction note above)
- **`CanvasNet.Geometry`** — `Path`/`PathBuilder` (resolved shape/connector outlines), `Rect`
  (resolved bounding boxes)
- **`CanvasNet.Drawing`** — `PathFiller`/`PathStroker` (rasterizing resolved paths/strokes/
  arrowheads). Correction note (Milestone 8): an earlier revision of this design also listed
  `Gradient`/`LinearGradient`/`GradientStop` here for a best-effort linear fill-pattern
  approximation; this was never implemented, and no such type is referenced anywhere in this
  package's source — every non-solid `FillPattern` value degrades to solid fill instead (see
  `FillPatternDeferral` above).
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
