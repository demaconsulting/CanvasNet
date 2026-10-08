# System Design

<!-- cspell:ignore vsdx Visio VisioML xfrm stencil stencils glueable NURBS nurbs -->
<!-- cspell:ignore shapesheet ShapeSheet rrggbb slnx Foregnd THEMEVAL Nwwww Neeeee -->

This document provides the system-level design for CanvasNetVsdx.

![CanvasNetVsdx Structure](CanvasNetVsdxView.svg)

## Architecture

CanvasNetVsdx is a .NET library providing Microsoft Visio (`.vsdx`) diagram-rendering support,
distributed as its own NuGet package (`DemaConsulting.CanvasNet.Vsdx`, namespace
`DemaConsulting.CanvasNet.Vsdx`), independent of, but depending on, the core `CanvasNet` system
(its own separate package, `DemaConsulting.CanvasNet`) — see the Dependencies section below. The
system consists of a single implemented unit:

- **VsdxDocument** (namespace `DemaConsulting.CanvasNet.Vsdx`, folder
  `src/DemaConsulting.CanvasNet.Vsdx/`, flat — no further nesting): an OPC (Open Packaging
  Conventions) package parser and VisioML (the `http://schemas.microsoft.com/office/visio/2012/main`
  cell/shapesheet-based XML vocabulary Visio uses, unrelated to DrawingML) rendering codec. It
  opens a `.vsdx` package, resolves its content-type and relationship graph, enumerates pages and
  their masters/theme, resolves each page's full shape tree — including master/stencil cell and
  geometry-row inheritance, the StyleSheet chain, shape geometry and transform, text layout, and
  1-D connector glue-point resolution — and renders a requested page onto a core
  `CanvasNet.Canvas.Surface`. See _VsdxDocument Unit Design_ (`canvas-net-vsdx/vsdx-document.md`).

`CanvasNetVsdx` is modeled as its own top-level software system (rather than as a further unit of
the `CanvasNet` system, or as a subsystem of a different document-rendering system) because it is
distributed as its own independently-versioned NuGet package — per `software-items.md`'s rule
that a software package contains exactly one software system, a system whose entire content ships
in a distinct package must itself be modeled as a distinct system. This mirrors `CanvasNetSvg`'s
and `CanvasNetPdf`'s own precedent (see _CanvasNetSvg System Design_, `canvas-net-svg.md`, and
_CanvasNetPdf System Design_, `canvas-net-pdf.md`). `CanvasNetVsdx` contains exactly one unit, so
no Subsystem tier is interposed between the system and `VsdxDocument`: inserting one here would
not separate anything, since there is nothing else in the system to separate it from — identical
reasoning to `CanvasNetSvg`'s own single-unit shape. Unlike `CanvasNetPptx`'s own
`PptxDocument` (whose single-unit shape was decided only after several delivered phases already
existed), `VsdxDocument` is planned as a single unit from the outset: the format's two
inheritance axes (Master/MasterShape cell-and-geometry-row inheritance, and the independent
StyleSheet chain) are tightly coupled to the same per-shape resolution pipeline, with no natural
internal boundary that would justify a second unit or an interposed subsystem.

This design document describes `CanvasNetVsdx`'s complete, final feature set, delivered as a
single pull request rather than the phased, additive-release pattern `CanvasNetPptx` followed.
The Implementation Phase Plan section at the end of this document breaks that single delivery
down into an ordered sequence of internal engineering milestones for implementation agents to
follow — each milestone is a checkpoint within one continuous development effort, not a
separately-shippable increment.

`CanvasNetVsdx` has no dependency, in either direction, on `CanvasNetPptx`, `CanvasNetPdf`, or
`CanvasNetCharts`. Nothing in the VSDX format research underlying this design found any construct
analogous to a PPTX `<p:graphicFrame>` chart reference, so `CanvasNetVsdx` does not depend on
`CanvasNetCharts` (unlike `CanvasNetPptx` — see _CanvasNetPptx System Design_,
`canvas-net-pptx.md`). A future `CanvasNetSvg` dependency (to rasterize an embedded SVG foreign
object, mirroring `CanvasNetPptx`'s own "Phase 2 Follow-Up: SVG-Only Picture Blip Rendering") is
an explicitly deferred, known gap — see the Design Constraints section below.

## External Interfaces

The system exposes the following public API, all on the `VsdxDocument` class:

- **`VsdxDocument.Open(Stream stream)`** / **`VsdxDocument.Open(string path)`**: Opens a `.vsdx`
  package from a stream or file path exactly once, parsing its OPC relationship/content-type
  graph, its page index (`visio/pages/pages.xml`), its master/stencil index
  (`visio/masters/masters.xml`), its StyleSheet chain (`visio/document.xml`), and its optional
  theme (`visio/theme/theme1.xml`), returning a `VsdxDocument` instance. Throws
  `ArgumentNullException` for a null `stream`/`path`, `ArgumentException` for an empty/
  whitespace-only `path`, and `InvalidDataException` for an unreadable/corrupt ZIP or a package
  missing a required part (`[Content_Types].xml`, `_rels/.rels`, `visio/document.xml`, or
  `visio/pages/pages.xml`).
- **`PageCount`**: Reports the number of pages declared in `visio/pages/pages.xml`.
- **`GetPageSize(int pageIndex)`**: Reports the requested page's declared `PageWidth`/
  `PageHeight` (inches, converted to the document's internal unit — see _VsdxDocument Unit
  Design_, `canvas-net-vsdx/vsdx-document.md`). Throws `ArgumentOutOfRangeException` for a
  `pageIndex` outside `[0, PageCount)`.
- **`Render(int pageIndex, int width, int height, VsdxRenderOptions? options = null)`**: Resolves
  the requested page's full shape tree (master/stencil inheritance, StyleSheet chain, geometry/
  transform, text, connectors) and paints it onto a new `width`x`height` `Canvas.Surface`,
  cleared to `VsdxRenderOptions.BackgroundColor` (default opaque white) beforehand. Throws
  `ArgumentOutOfRangeException` for a `pageIndex` outside `[0, PageCount)` or a non-positive/
  over-`Surface.MaxDimension` `width`/`height`.
- **`Render(int pageIndex, int dpi, VsdxRenderOptions? options = null)`**: A DPI-based convenience
  overload that converts the page's declared size to pixel dimensions at the requested resolution
  before delegating to the pixel-dimension overload above.

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| -------------------------------------- | ---------------- | ------------------------------ | ------------------------------- |
| `VsdxDocument.Open(...)` | Inbound | Method call / `VsdxDocument` return | Valid `.vsdx` stream or path |
| `PageCount` / `GetPageSize(...)` | Outbound | Property / method call | `0 <= pageIndex < PageCount` |
| `Render(pageIndex, width, height, ...)` | Inbound/Outbound | Method call / `Surface` return | `0 < width, height <= 8192` |
| `Render(pageIndex, dpi, ...)` | Inbound/Outbound | Method call / `Surface` return | `0 < dpi`; resolved pixel size `<= 8192` |
<!-- markdownlint-enable MD013 -->

See _VsdxDocument Unit Design_ (`canvas-net-vsdx/vsdx-document.md`) for the complete in-scope
VisioML feature subset (package/relationship resolution, geometry row vocabulary, master/stencil
inheritance, StyleSheet resolution, text, connectors/glue points, color/fill resolution, and group
nesting) and every method's full parameter and exception detail.

## Dependencies

`CanvasNetVsdx` depends on the separate `CanvasNet` system (its own package,
`DemaConsulting.CanvasNet`, referenced via a project reference from
`src/DemaConsulting.CanvasNet.Vsdx/DemaConsulting.CanvasNet.Vsdx.csproj`), specifically:

- The `Canvas` subsystem's `Surface` unit (constructing the destination raster and compositing
  filled/stroked/text pixels into it) and `Rgba32` unit (representing resolved line/fill/text
  colors, including the built-in 24-color Visio palette and literal hex colors). Correction note
  (Milestone 8): an earlier revision of this design also claimed resolution against the
  document's own `<Colors>` extension table (a custom, document-declared color palette distinct
  from the built-in 24-color one); a repository-wide search confirms no `<Colors>` element is
  parsed anywhere in this package's source — only the built-in palette, literal hex colors, and
  the `"Themed"` sentinel are resolved (see `VsdxColorPalette.Resolve`).
- The `Geometry` subsystem's `Path`/`PathBuilder` (assembling a shape's resolved `MoveTo`/
  `LineTo`/`RelMoveTo`/`RelLineTo` geometry-row subpaths) and `Rect` (resolved shape/page bounding
  boxes)
- The `Drawing` subsystem's `PathFiller`/`PathStroker` (rasterizing resolved shape/connector
  outlines, including connector arrowheads). Correction note (Milestone 8): an earlier revision of
  this design anticipated also depending on the `Gradient`/`LinearGradient`/`GradientStop` types
  for a best-effort fill-pattern resolution; this was never implemented, and no such type is
  referenced anywhere in this package's source — every non-solid `FillPattern` value degrades to
  solid fill instead (see Design Constraints below).
- The `Fonts` subsystem's `TrueTypeFont`/`SystemFontCatalog` (resolving a `Section N="Character"`
  row's `Font` name hint and extracting glyph outlines for shape text)
- The `Codecs` subsystem (reserved for the deferred embedded-image/foreign-object capability
  described in the Design Constraints section below; not used by the initial, in-scope feature
  set, since no inspected sample exercises an embedded picture)

This is an ordinary, same-repository, system-to-system dependency: both `CanvasNet` and
`CanvasNetVsdx` are produced by this repository, so it is neither an OTS Software Item nor a
Shared Package. Beyond `CanvasNet`, `VsdxDocument` uses only the .NET base class library's
`System.IO.Compression` (`ZipArchive`, for the OPC package layer) and `System.Xml.Linq`
(`XDocument`/`XElement`, for VisioML parsing) namespaces, available on every one of
`CanvasNetVsdx`'s target frameworks with no new runtime NuGet dependency. `CanvasNetVsdx`
introduces no new OTS Software Item beyond those already used to build and verify the `CanvasNet`
system (BuildMark, FileAssert, Pandoc, ReqStream, ReviewMark, SarifMark, SonarMark, VersionMark,
WeasyPrint, xUnit) — see _OTS Integration Design_ (`docs/design/ots.md`).

`CanvasNetVsdx` must never reference `CanvasNetPptx`, `CanvasNetPdf`, or `CanvasNetCharts`. A
future, explicitly out-of-scope dependency on `CanvasNetSvg` (to rasterize an embedded SVG foreign
object) is the single exception this constraint anticipates — see _CanvasNetSvg System Design_'s
own Dependencies section (`canvas-net-svg.md`), which already names `CanvasNetVsdx` as a future,
one-directional consumer, identical to how `CanvasNetPptx` depends on `CanvasNetSvg` today.

## Risk Control Measures

`VsdxDocument.Open`/`Render` parse an externally-supplied, potentially untrusted `.vsdx` package —
a ZIP archive containing XML markup that may declare deeply nested groups, self-referencing
Master/MasterShape chains, or malformed geometry/transform cells. Risk control for this
untrusted-input parsing is segregated entirely within the single `VsdxDocument` unit and falls
into two distinct categories, mirroring `CanvasNetSvg`'s own established pattern (see
_CanvasNetSvg System Design_, `canvas-net-svg.md`):

- **Tolerantly skipped, no-effect constructs** — an unrecognized geometry row type (for example
  `ArcTo`/`RelCubBezTo`/`SplineStart`/`PolylineTo`/`Ellipse`, documented but unobserved in the
  format research underlying this design — see the Design Constraints section), an unresolvable
  `Connect` target, or a well-formed but out-of-scope construct (a non-solid `FillPattern`, a
  non-default `Themed` color with no theme part present) degrades that one shape/row/connector to
  a safe default (skip the row, render with no fill/stroke, or fall back to a neutral color)
  rather than aborting the whole page's render.
- **Rejected structural cycles** — a Master/MasterShape or group-nesting chain that resolves back
  to itself is explicitly detected and rejected with a thrown `InvalidDataException`, rather than
  tolerated: recursing into a genuine cycle would otherwise never terminate. A bounded
  group-nesting-depth and total-shape-count budget is likewise enforced, rejecting the offending
  package once exceeded.

No other segregation is required at the system level: `CanvasNetVsdx` contains exactly one unit,
so this risk control is inherently contained within it (IEC 62304 §5.3.3).

## Data Flow

**Package open path:**

1. **Input**: A `.vsdx` package (stream or file path)
2. **Validation**: `Open` rejects a null `stream`/`path` with `ArgumentNullException`, an empty/
   whitespace `path` with `ArgumentException`, and an unreadable/corrupt ZIP or a package missing
   a required part with `InvalidDataException`
3. **Processing**: Resolves `[Content_Types].xml` and the `_rels/.rels` → `visio/document.xml` →
   `masters.xml`/`pages.xml`/`theme1.xml` relationship chain into a navigable part graph; parses
   `visio/document.xml`'s `<StyleSheets>` and `visio/pages/pages.xml`'s page index into an
   in-memory model, without yet resolving any individual page's shape tree. Correction note
   (Milestone 8): an earlier revision of this design also claimed `<Colors>`/`<FaceNames>`
   parsing at this step; a repository-wide search confirms neither element is parsed anywhere in
   this package's source (see the Dependencies section's own correction note above for the
   `<Colors>` case specifically) - `<FaceNames>` parsing was never implemented at all, and no
   in-scope requirement or test currently depends on it.
4. **Output**: A `VsdxDocument` instance exposing `PageCount`/`GetPageSize`/`Render`

**Page render path:**

1. **Input**: A page index, requested pixel dimensions (or DPI), and optional render options
2. **Validation**: Rejects a `pageIndex` outside `[0, PageCount)` and a non-positive/over-
   `Surface.MaxDimension` `width`/`height` (or resolved DPI-derived size) with
   `ArgumentOutOfRangeException`
3. **Processing**: Parses the requested `pageN.xml` (and any `<Connects>` section) into a shape
   tree; resolves each shape's effective cell set via the Master/MasterShape merge algorithm,
   its effective line/fill/text style via the StyleSheet chain, its geometry rows into a
   `Geometry.Path`, its shape-local-to-page affine transform, its text layout, and (for 1-D
   shapes) its glue-resolved begin/end endpoints; walks the resolved shape tree in document
   order (document order is z-order, identical convention to the existing `.pptx`/`.pdf`
   renderers) painting each shape's fill, stroke, connector line/arrowheads, and text onto the
   destination `Surface`
4. **Output**: A new `Canvas.Surface` containing the rendered page raster

## Design Constraints

- **Simplicity**: Minimal functionality kept easy to understand and extend
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms
- **Format subset**: `VsdxDocument` implements the VisioML construct set confirmed by real-world
  sample packages (per the format research underlying this design) — `MoveTo`/`LineTo`/
  `RelMoveTo`/`RelLineTo` geometry rows; Master/
  MasterShape cell-and-row inheritance; the StyleSheet chain (including direct per-shape
  overrides); explicit hex and built-in-palette-index colors; character/paragraph text formatting
  and run markers; whole-shape-pin and connection-point-indexed glue; and arbitrarily nested
  groups. The following are **explicitly deferred**, each a documented gap in the format research:
  - **Embedded images / `Foreign` shapes** — no inspected sample contains a `<Shape
    Type="Foreign">`/image relationship; the expected OOXML-convention shape is inferred, not
    observed, and must be verified against a real image-bearing `.vsdx` sample before
    implementation. `VsdxDocument` recognizes but does not render a `Foreign`-typed shape in this
    delivery.
  - **`Themed` color resolution against a non-trivial `theme1.xml` variation** — no inspected
    sample exercises a shape whose resolved color is genuinely theme-driven (every sample with a
    theme part uses explicit hex colors instead); `VsdxDocument` parses a present `theme1.xml`
    (reusing the existing PPTX DrawingML theme-parsing infrastructure, since both are OOXML
    `<a:theme>` parts) but falls back to a neutral default color for a `Themed` cell with no
    resolvable scheme entry. Milestone 10's own stress-testing against the apache/poi test-data
    corpus confirmed a concrete, real-world sample of this exact limitation: `test.vsdx`'s header
    bar and star shape resolve a bare `THEMEVAL()` formula cell that genuinely depends on the
    shape's own Quick-Style variation index (a full variation-matrix engine, not merely scheme
    lookup, would be required to resolve it), so they render in the neutral fallback color rather
    than Visio's own blue — confirmed, by direct comparison against a Visio COM reference PNG, to
    be this already-documented limitation working exactly as intended, compounded at the time by
    the (since-resolved, see below) `EllipticalArcTo`-skip deferral rounding the header bar's
    corners squarely instead; Milestone 11's `EllipticalArcTo`/`ArcTo` fix (below) corrects the
    corner-rounding compounding factor, leaving the color itself still in the documented neutral
    fallback, exactly as this limitation describes. No code change was made for this sample; it is
    recorded here purely as confirmed evidence this documented gap is real and already correctly
    tolerated, not a newly discovered defect. Milestone 11 confirmed a second, independent
    real-world instance of this same limitation: `60973.vsdx`'s `Nwwww`/`Neeeee` rack-slot bars
    (`LineStyle="3"`/`FillStyle="3"`, no own literal color cells) resolve a bare `THEMEVAL()`
    formula through the same unresolvable-scheme path, rendering in the neutral fallback gray
    rather than Visio's own orange/blue fill — confirmed via direct comparison against the
    Visio-reference PNG, and left untouched (documentation-only confirmation, no code change),
    consistent with the first instance above.
  - **Non-solid `FillPattern` values (built-in hatch/gradient combinations)** and the full
    `BeginArrow`/`EndArrow` arrowhead-style index table — neither is fully enumerable from the
    format research's sample corpus; `VsdxDocument` supports `FillPattern` 0 (none) and 1 (solid)
    directly, and degrades any other numeric `FillPattern` value to the same solid-fill treatment
    (using the resolved `FillForegnd` color), and supports a documented subset of common arrowhead
    styles, with an unrecognized arrowhead index degrading to a plain, unadorned line end, never
    throwing. An earlier revision of this design anticipated a best-effort linear-gradient
    approximation for a non-solid `FillPattern`; this was never implemented — direct inspection of
    `VsdxDocument.Paint.cs` confirms every non-zero, non-one `FillPattern` value has always
    degraded to solid fill, and no `Gradient`/`LinearGradient` type from the core `Drawing`
    subsystem is referenced anywhere in this package's source.
  - **`NURBSTo`/`InfiniteLine`/`RelCubBezTo`/`SplineStart`/`SplineKnot`/`PolylineTo`/`Ellipse`
    geometry rows** — documented VisioML vocabulary observed (`NURBSTo`) or not observed (the
    remainder) in the inspected sample corpus; every one of these row types is skipped (see Risk
    Control Measures above) rather than implemented against an unverified cell layout or
    curve-approximation algorithm. An earlier revision of this design anticipated converting
    `EllipticalArcTo`/`NURBSTo` rows to Bezier curve approximations; this was never implemented
    for either row type as of Milestone 10, and the tolerant-skip treatment already applied to
    every other unrecognized row type was confirmed sufficient at that time (no inspected
    fixture's rendered output depended on either row type contributing a path segment).
    **Milestone 11 superseded this deferral for `EllipticalArcTo` and the documented-but-
    previously-unobserved `ArcTo` row type specifically**: `VsdxDocument.Geometry.cs` now
    converts both to an `ArcTo` path command reaching the row's own destination point (a general
    ellipse/arc for `EllipticalArcTo`'s `X`/`Y`/`A`/`B`/`C`/`D` cell set; a circular,
    bow-height-derived arc for `ArcTo`'s `X`/`Y`/`A` cell set, degrading a zero-bow row to a plain
    `LineTo`) — confirmed necessary against `test.vsdx`'s header-bar shape, whose own
    `EllipticalArcTo`-shaped rounded corner was rendering as a sharp wedge/triangle instead of a
    smoothly rounded bar while the row was tolerantly skipped. `NURBSTo` remains skipped; no
    inspected fixture's rendered output depends on it contributing a path segment. Milestone 10
    retry 1's own independent re-verification of a disputed transparency-regression claim
    (quality Finding #1) confirmed a second, real-world instance of the (still-current, `NURBSTo`-
    only) limitation:
    `github260.vsdx`'s "start state" circle (Master `8`'s `MasterShape="6"`, page Shape `ID="1"`)
    has a `Section N="Geometry"` consisting of a single `MoveTo` followed entirely by `NURBSTo`
    rows with no `LineTo` fallback — skipping every `NURBSTo` row leaves a single bare point with
    no closed path to paint, so the shape renders with neither fill nor stroke regardless of its
    own `LineColorTrans`/`FillForegndTrans` values; confirmed via direct pixel-scanning of the
    actual Visio-reference PNG that the shape's genuine appearance there is a solid fill with no
    visible border (not the fully-bordered shape an initial, since-corrected quality finding
    claimed), and confirmed byte-identical across the pre-Bug-#3-fix and post-Bug-#3-fix commits,
    ruling out any Milestone 10 transparency-resolution change as the cause.

  None of these gaps blocks the rest of the design: each is isolated to its own narrow code path,
  with a safe, non-throwing fallback, consistent with the Risk Control Measures above.

  Beyond the format-subset gaps above, Milestone 10 retry 1 (quality Finding #2) confirmed and
  documents one further, narrowly-scoped, pre-existing rendering defect, deferred rather than
  fixed in this milestone:

  - **Text/geometry rendering for a 100%-Master-inherited 2-D shape nested 2+ group levels deep
    whose own instance carries zero geometry/transform cells** — confirmed against
    `60973.vsdx`'s `AIRttt`/`AIRuuu` "Wireless Controller" instances (page shapes `791`/`854`,
    `Master="21"` resolving to `master19.xml`): both instances carry only a `LayerMember`, a
    `Character/Size` override, and their own literal `<Text>` — zero geometry, fill, or transform
    cells of their own — yet the resolved text overflows unclipped with no visible box fill,
    unlike sibling boxes using other Masters in the same document, which render correctly. This is
    a distinct and deeper defect from the Del="1"-stub-exclusion and connector-line fixes
    Milestone 10 did deliver for this same document (see `CanvasNetVsdx-VsdxDocument-DeletedShapeExclusion`'s
    own justification note distinguishing the two). Confirmed via git-history bisection to render
    identically broken before Milestone 9, before Milestone 10, and after all 4 Milestone 10
    commits — not introduced or fixable by this milestone's scope; deferred pending dedicated
    root-cause investigation of the Master-shape-lookup/`TextBox.cs` `TxtWidth`-sizing interaction
    for deeply nested, fully-inherited 2-D shapes.

  Milestone 11's own real-world-corpus validation pass confirmed, and documents, three further
  rendering limitations deferred rather than fixed this milestone:

  - **2-D shape transform-cell-merge exclusion discarding a legitimately-cached instance position
    in favor of an unrelated Master design-time position** — `VsdxDocument.CellMerge.cs`'s
    `TransformCellNames`/`PreferInstanceTransformCells` overlay (see
    `CanvasNetVsdx-VsdxDocument-CellMerge`'s own justification, and `canvas-net-vsdx/vsdx-document.md`)
    deliberately excludes a 2-D shape (no `BeginX`/`EndX` cell pair) from the instance-cached-
    transform-cell preference applied to a 1-D connector, on the documented assumption that a 2-D
    shape's own `F="Inh"`-marked transform cell is always a legitimate, intentional Master
    inheritance. Milestone 11 traced two independent real-world findings to the same counter-
    example of that assumption: a 2-D `Shape` that is itself a child of a group/connector (a frame
    container in `60973.vsdx`'s rack-mount page, and a connector-label shape `ID='45'`
    ("end1_name") in `44501e.vsdx`'s Binary Association group) whose own `PinX`/`PinY`/`Width`/
    `Height`/`LocPinX`/`LocPinY` are all marked `F="Inh"` yet do carry a genuinely different,
    correctly-cached instance-specific position — discarded in favor of the Master's unrelated
    template-local position, landing the frame/label in the wrong place (overlapping/inside a
    sibling shape rather than at its own correct position). No safe, narrowly-scoped
    disambiguating heuristic between this counter-example and the overlay's own original
    legitimate-2-D-inheritance justification (locked by
    `MasterInheritance_2DShapeInhMarkedTransformCell_DefersToMasterValue`) was identified within
    this milestone's scope; broadening the existing 1-D-only overlay to cover this 2-D case risks
    silently breaking that locked, intentional case. Deferred pending a dedicated root-cause
    investigation of a safe disambiguation signal (for example, a group-child-specific heuristic
    distinct from a top-level 2-D shape). This is the same limitation underlying both
    `60973.vsdx`'s still-missing rack-frame border/"15 U" field label positioning and
    `44501e.vsdx`'s "-has" connector label rendering glued inside a sibling box's title
    compartment rather than at its own external connector-label position — not two independent
    defects, but one shared root cause observed at two real-world sites.
  - **Standalone top-level 2-D shape mis-position unrelated to any Master/geometry inheritance
    path** — `test.vsdx`'s "This is a test." text shape (page Shape `ID=1`, a top-level sibling of
    the "Classic" group, not nested within it, carrying fully literal, non-`"Inh"` `PinX`/`PinY`/
    `Width`/`Height` cells of its own) renders overlapping the header bar in both a pre-Milestone-11
    and post-Milestone-11 build (confirmed via an explicit `git stash`/`git stash pop` baseline
    A/B render comparison) — this shape's own transform cells are never inherited from anything,
    so none of this milestone's Group A/B/C/D/E fixes can affect its resolved position. Milestone
    11's `EllipticalArcTo`/`ArcTo` fix (Group B) did resolve this same fixture's header-bar/star
    shape from a sharp wedge/triangle to the correctly rounded bar Visio itself renders, so this
    finding is **partially**, not fully, resolved: the triangle-shaped-outline defect is fixed, the
    unrelated text-overlap-position defect persists, deferred pending a dedicated root-cause
    investigation of this specific top-level shape's own intended position.
  - **Arrowhead rendered size proportional to a connector's own stroke width** — confirmed in
    `CanvasNetVsdx-VsdxDocument-ArrowheadRendering`'s own justification above: a thin-stroked
    connector (common throughout the UML-diagram fixtures) renders a correctly-styled but visually
    tiny arrowhead, since `VsdxArrowheadGeometry`'s half-width/length factors scale with the
    connector's own resolved stroke width rather than any fixed/absolute size — a pre-existing,
    unchanged design approximation (the same philosophy already applied to `PptxArrowheadGeometry`),
    not a regression or a defect in Milestone 11's own style-index-mapping fix.

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

## Implementation Phase Plan

`CanvasNetVsdx` ships as a single pull request, but the engineering work proceeds through the
following ordered milestones. Each milestone builds strictly on the previous one's resolved
output; an implementation agent should complete and validate one milestone (including its own
unit tests) before starting the next, since later milestones assume earlier resolvers are
already correct.

1. **Project scaffolding.** Create `src/DemaConsulting.CanvasNet.Vsdx/` and
   `test/DemaConsulting.CanvasNet.Vsdx.Tests/` projects following the existing sibling systems'
   `.csproj` conventions (target frameworks, analyzers, `InternalsVisibleTo` grant to the test
   project, project reference to `DemaConsulting.CanvasNet`), wire both into `CanvasNet.slnx`,
   and add the empty `VsdxDocument`/`NamespaceDoc` skeleton with no behavior yet.
2. **OPC package layer + page enumeration.** Implement `Open(Stream)`/`Open(string)`:
   `[Content_Types].xml` and relationship-chain resolution (`_rels/.rels` →
   `visio/document.xml` → `masters.xml`/`pages.xml`/`theme1.xml`, per-page/per-master `.rels`
   indirection), `visio/pages/pages.xml` page-index parsing (`PageWidth`/`PageHeight`,
   inches-to-internal-unit conversion), and the documented argument/structural validation
   contract. Scope: `PageCount`/`GetPageSize` resolve correctly; no shape content parsed yet.
3. **Shape/geometry model + master/stencil inheritance + style resolution.** Parse a page's
   `<Shapes>`/`<Shape>` tree into a generic per-shape cell/section model; implement the geometry
   row vocabulary and the shape-local-to-page affine transform (including the 1-D begin/end-
   derived transform special case); implement the Master/MasterShape cell-and-geometry-row merge
   algorithm and the StyleSheet chain resolution (including direct per-shape overrides and the
   built-in color palette). Scope: an ungrouped, unmastered-or-mastered shape's
   outline, fill, and stroke resolve and paint correctly.
4. **Text rendering.** Implement `<Text>` run parsing (`<cp>`/`<pp>` markers against
   `Section N="Character"`/`"Paragraph"` rows), the implicit text-box-equals-shape-box default,
   explicit `Txt*` cell positioning for connectors, and glyph-ink painting via the core `Fonts`/
   `Drawing.PathFiller` primitives.
5. **Connector/glue-point routing.** Implement `<Connects>` parsing, the static glue-point
   resolution algorithm (trusting pre-baked `BeginX/Y`/`EndX/Y` values), the geometry-row-delete
   pattern needed for dynamic-connector master overrides, and arrowhead rendering.
6. **Color/theme resolution + group nesting.** Implement `visio/theme/theme1.xml` parsing for
   `Themed` cell resolution (reusing the existing PPTX theme-parsing infrastructure where
   structurally identical), and recursive group/nested-shape transform composition (arbitrary
   nesting depth, with the cycle/depth-budget rejection from Risk Control Measures).
7. **Public Render API integration + DPI/page-size handling.** Implement the public
   `Render(pageIndex, width, height, options)` and DPI-overload entry points: background-color
   clearing, document-order shape-tree walk, and dispatch to the already-verified milestone
   3–6 resolvers/painters.
8. **Comprehensive test suite using staged real-world fixtures.** Exercise every resolver/painter
   above against the real-world `.vsdx` fixtures staged (in parallel with this design work) into
   `test/DemaConsulting.CanvasNet.Vsdx.Tests.Fixtures-STAGING/`, promoting that staging folder to
   its permanent, versioned location as part of this milestone; add the system-integration test
   class (`VsdxSystemIntegrationTests.cs`) and the per-concern unit test classes mirroring the
   `.reviewmark.yaml` file list in this change; fill in `docs/verification/canvas-net-vsdx.md` and
   `docs/verification/canvas-net-vsdx/vsdx-document.md`'s TODO scenario sections against the
   resulting test names; update every `docs/reqstream/canvas-net-vsdx/**/*.yaml` requirement's
   `tests:` list to reference the real, passing test method names.
9. **Local code review + documentation finalization.** Run `lint.ps1`/`build.ps1`, resolve any
   SonarMark/SarifMark findings, confirm every requirement in `docs/reqstream/canvas-net-vsdx/`
   links to a passing test, confirm `sysml2tools lint`/`reviewmark --plan --enforce` both pass,
   and finalize the README/User Guide updates for the new package.
