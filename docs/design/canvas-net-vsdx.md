# System Design

<!-- cspell:ignore vsdx Visio VisioML xfrm stencil stencils glueable NURBS nurbs -->
<!-- cspell:ignore shapesheet ShapeSheet rrggbb slnx -->

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
  colors, including the built-in 24-color Visio palette, the document's own `<Colors>` extension
  table, and literal hex colors)
- The `Geometry` subsystem's `Path`/`PathBuilder` (assembling a shape's resolved geometry-row
  subpaths, including `EllipticalArcTo` and `NURBSTo` segments converted to Bezier curves) and
  `Rect` (resolved shape/page bounding boxes)
- The `Drawing` subsystem's `PathFiller`/`PathStroker` (rasterizing resolved shape/connector
  outlines, including connector arrowheads) and `Gradient`/`LinearGradient`/`GradientStop` types
  (best-effort fill-pattern resolution, mirroring `CanvasNetPptx`'s own linear-only boundary)
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
   `visio/document.xml`'s `<Colors>`/`<FaceNames>`/`<StyleSheets>` and `visio/pages/pages.xml`'s
   page index into an in-memory model, without yet resolving any individual page's shape tree
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
  `RelMoveTo`/`RelLineTo`/`EllipticalArcTo`/`NURBSTo`/`InfiniteLine` geometry rows; Master/
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
    resolvable scheme entry.
  - **Non-solid `FillPattern` values (built-in hatch/gradient combinations)** and the full
    `BeginArrow`/`EndArrow` arrowhead-style index table — neither is fully enumerable from the
    format research's sample corpus; `VsdxDocument` supports `FillPattern` 0 (none) and 1 (solid)
    plus a best-effort linear-gradient approximation, and a documented subset of common arrowhead
    styles, with an unrecognized value degrading to a flat fill or a plain, unadorned line end
    respectively, never throwing.
  - **`ArcTo`/`RelCubBezTo`/`SplineStart`/`SplineKnot`/`PolylineTo`/`Ellipse` geometry rows** —
    documented VisioML vocabulary not observed in any inspected sample; an unrecognized row type
    is skipped (see Risk Control Measures above) rather than implemented against an unverified
    cell layout.

  None of these gaps blocks the rest of the design: each is isolated to its own narrow code path,
  with a safe, non-throwing fallback, consistent with the Risk Control Measures above.

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
   built-in/document `<Colors>` palette). Scope: an ungrouped, unmastered-or-mastered shape's
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
