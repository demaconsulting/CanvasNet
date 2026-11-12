# Introduction

<!-- cspell:ignore glyf sfnt cmap loca hmtx hhea codepoint Zapf Noto Visio visio rasterizer tessellator -->
<!-- cspell:ignore multmatrix Minkowski canvasnet deskew deskews Deskew Otsu -->

This document provides the detailed design for CanvasNet, a .NET library
providing a canvas-based drawing and rendering API.

## Purpose

The purpose of this document is to serve as the design entry point and provide detailed design
specifications for the CanvasNet system. This documentation enables formal code
review by providing implementation specifications, supports compliance auditing by maintaining
clear traceability from requirements through design to code, aids maintenance by documenting
system structure and interactions, and ensures quality assurance through detailed technical
specifications.

This document is intended for:

- Software developers implementing and maintaining the system
- Code reviewers validating implementation against design
- Compliance auditors tracing requirements through design to implementation
- Quality assurance teams validating system behavior

## Scope

This document covers the detailed design of the CanvasNet system and its constituent
software items, specifically:

- **CanvasNet (System)** — The complete .NET library system
- **Canvas (Subsystem)** — Pixel-buffer primitives: the `Surface` unit (mutable, in-memory
  32-bit RGBA pixel buffer with span-based row access) with its `SurfaceAdjustments` (tone and color adjustments),
  `SurfaceTransforms` (flip, EXIF orientation, pad, trim, fit, and flatten), `SurfaceGeometry` (rotation by any angle
  and skew estimation), `SurfaceComparison` (metrics, grid, and difference pictures), and `SurfaceAnalysis` (dominant
  colors, luma statistics, and pixel samples) parts, the `Rgba32` unit, and the `CssColor` unit
  (the shared CSS color parser)
- **Codecs (Subsystem)** — Image format codecs: `BmpCodec`, `PngCodec`, `TiffCodec`, and
  `JpegCodec`, each converting to and from a `Surface` pixel buffer; `GifCodec`, a decode-only
  unit that loads a `Surface` from the first frame of a GIF file; and `Jpeg2000Codec`, a decode-only
  JPEG 2000 Part 1 decoder (JP2 and raw codestreams) also used for PDF `/JPXDecode` images; and
  `ImageDecoder`, a format-neutral facade (`ImageFormat`, `ImageFormats`, `ImageDecoder`) that
  recognizes an image's format from its signature or media type and decodes it through the
  matching codec
- **Geometry (Subsystem)** — Vector-geometry primitives, distinct from the `Drawing`
  subsystem (which covers rasterization built on top of these primitives): the `Rect` unit
  (axis-aligned bounding rectangle), the `Path` unit (immutable vector path and its
  `PathBuilder`, covering the supporting `Subpath`, `PathCommand`, and `PathCommandType` types
  inline), the `BezierFlattening` unit (adaptive Bezier curve flattening), the
  `SvgArcConverter` unit (SVG-style elliptical arc to Bezier conversion), and the
  `CornerRoundEffect` unit (path-level pre-processing that replaces polyline corners with
  tangent-radius circular arcs)
- **Drawing (Subsystem)** — An antialiased scanline-coverage fill rasterizer for closed
  `Geometry.Path` geometry with solid-color, gradient, or tiled-pattern paint: the `PathFiller`
  unit (a public static `Fill` entry point, covering the supporting `FillRule` enum, the internal
  `EdgeFlattener`/`ScanlineRasterizer` helpers, and the internal `ClipMask` clip-coverage helper
  inline), the `PathStroker` unit (a public
  static `Stroke` entry point, covering the supporting `LineCap`/`LineJoin`/`StrokeStyle` types
  and the internal `StrokePathFlattener`/`DashSplitter`/`StrokeOutliner` helpers inline), the
  `GradientPaint` unit (the public `Gradient`/`LinearGradient`/`RadialGradient`/`GradientStop`/
  `GradientSpread` types and the internal `GradientEvaluator` helper), and (added alongside
  `CanvasNetPdf`'s `/Pattern` color-space support) the `TilePaint` unit (the public `TilePaint`
  type — a pre-rendered tile `Surface` plus a device-space `Transform` and pattern-space
  `XStep`/`YStep` pitch — and the internal `TilePaintEvaluator` helper, mirroring
  `GradientPaint`'s own public-type-plus-internal-evaluator shape)
- **Fonts (Subsystem)** — TrueType (`glyf`-based) and CFF/OpenType (`OTTO`-flavored, Type 2
  charstring-based) SFNT font support, including standalone classic PostScript Type 1/Type 1C
  font-program loading: the `TrueTypeFont` unit and its internal `SfntContainer`/`CmapTable`/
  `GlyfLocaReader`/`IGlyphOutlineSource`/`CffTable`/`CffCharstringInterpreter`/
  `CffStandardEncoding`/`Type1Table`/`Type1CharstringInterpreter`/`Type1CharstringDecryption`/
  `Type1PfbReader`/`Type1PfaReader`/`Type1StandardGlyphNames`/`HmtxHheaReader`/`KernTable`/
  `NameTable`/`StyleTable` helpers,
  producing `Geometry.Path` glyph outlines plus metrics and kerning; and (added Phase 6 of the
  `CanvasNetPdf` roadmap) the `SystemFontCatalog` unit, providing directory-scan-only discovery
  of fonts installed on the host operating system, best-effort family-name/style matching against
  that catalog, and a bundled, always-available Liberation Sans/Serif/Mono last-resort fallback
  font shipped as an embedded resource of this assembly
- **Rendering (Subsystem)** — higher-level rendering primitives composing `Canvas`, `Geometry`,
  `Drawing`, and `Fonts`: the transform-aware `Canvas` wrapper, `TextRenderer` (measure and draw
  TrueType text with alignment and kerning), `Shapes` (rectangle, rounded rectangle, and
  circle helpers), and `WordWrap` (a font- and unit-independent greedy word-wrap line packer)
- **Files (Subsystem)** — Sandboxed file access, with no dependency on any other subsystem (only the
  `ImageSources` subsystem of the core package depends on it): the
  `SandboxedFileResolver` unit (the public resolver plus its `SandboxOptions`, `SandboxedFile`,
  `SandboxException`, and `SandboxFailureKind` types) that turns an untrusted relative path into the bytes of
  a file inside caller-chosen directories (or a caller-supplied virtual file source), refusing escapes,
  hostile path text, and symbolic links or junctions, and used by `ImageSources`, `CanvasNetVegaLite`, and
  `CanvasNetScad` for every local file
- **ImageSources (Subsystem)** — Linked-picture support for documents that link to other pictures (an SVG
  `image` element, for example), depending on `Files`, `Canvas`, and `Codecs` (no core subsystem depends on it): the
  `ImageSourceContract` unit (the public `IImageSource`, `ImageSourceRequest`, `ImageSourceResult`,
  `ImageSourceDiagnostic`, `ImageDiagnosticSeverity`, `IImageResolver`, `EmbeddedImage`, `ImageSourceException`, and
  `ImageSourceBudgetException`
  types that anyone can implement or use, and the `ImageSourceSet` that validates what sources claim and lets the
  later source win), the `FolderImageResolver` unit (with its `FolderImageResolverOptions`: confines links to a folder,
  picks the source by file name extension or URI scheme, bounds depth, cycles, image count, and the pixels the sources
  allocate, forwards notes, and
  never caches a picture), and the `RasterImageSource` unit (the source for the raster formats). Each format package
  ships its own source (`SvgImageSource`, `ScadImageSource`, `VegaLiteImageSource`, `MermaidImageSource`,
  `PdfImageSource`, `PptxImageSource`, `VsdxImageSource`), folded into the direct unit of that package
- **Packaging (Subsystem)** — hardened building blocks for reading untrusted Open Packaging
  Conventions (OPC) packages and untrusted XML, depending only on the .NET base class library: the
  `OpcPackage` unit (a read-only, size-bounded view of an OPC ZIP package, such as a `.pptx` or
  `.vsdx` file, with content-type and relationship resolution and bounded part reads, and its
  `OpcFormatException`) and the `SafeXml` unit (XML loading with no DTD processing, no external
  resolution, and a character bound)
- **CanvasNetSvg (System)** — A separate, independently-distributed software system providing
  SVG (Scalable Vector Graphics) rasterization, containing a single unit, `SvgCodec`, which
  decodes/rasterizes a subset of SVG vector documents into a `CanvasNet.Canvas.Surface`.
  `CanvasNetSvg` depends on this `CanvasNet` system's `Canvas`, `Geometry`, `Drawing`, `Fonts`
  (including `SystemFontCatalog`, which resolves the font of SVG text when the caller registers
  none), `Codecs`, and `Packaging` subsystems — see _CanvasNetSvg System Design_ (`canvas-net-svg.md`)
- **CanvasNetPdf (System)** — A separate, independently-distributed software system providing
  PDF page-rendering support, containing a single unit, `PdfDocument`, which parses a PDF
  document's structure (cross-references, trailer, page tree), optionally decrypting a document
  encrypted with the PDF `/Standard` security handler (RC4, AES-128, or AES-256), and reports its
  page count/size/rotation, then renders each page's content stream end to end: path
  construction/painting with real device color (including axial/radial shading and tiling
  `/Pattern` fills), image XObjects (decoded through a `FlateDecode`/`LZWDecode`/
  `ASCII85Decode`/`ASCIIHexDecode`/`RunLengthDecode`/`DCTDecode`/`CCITTFaxDecode` filter
  pipeline) and nested `/Subtype /Form` XObjects, and text drawn with an embedded TrueType,
  Type 1, Type 1C, Type 3, or Type 0/CID-keyed font, or an automatically substituted
  system/bundled fallback font when none is embedded. `CanvasNetPdf` depends on this
  `CanvasNet` system's `Canvas`, `Geometry`, `Drawing`, `Fonts`, and `Codecs` subsystems — see
  _CanvasNetPdf System Design_ (`canvas-net-pdf.md`)
- **CanvasNetPptx (System)** — A separate, independently-distributed software system providing
  PowerPoint (`.pptx`) presentation-rendering support, containing a single unit, `PptxDocument`.
  This feature was delivered incrementally across Phases 1a–1f, each additive to the last: Phase
  1a implemented the underlying OOXML (Office Open XML) package layer — opening a `.pptx` file as
  a ZIP archive, resolving `[Content_Types].xml`'s default and part-specific override content-type
  mappings, and resolving package-level and per-part relationships (including relative-target
  traversal). Phase 1b added the presentation/theme/master/layout/slide model and a placeholder
  property-inheritance resolver — parsing `ppt/presentation.xml`'s declared slide size and ordered
  slide list (exposed as public `SlideCount`/`SlideSize` members), resolving each slide master's
  theme (color/font scheme), structurally parsing each master/layout/slide's placeholder shapes,
  and implementing the verified ECMA-376 placeholder-matching algorithm. Phase 1c added DrawingML
  shape geometry (position/rotation/flip transform resolution, group child-coordinate-space
  composition, preset and custom geometry resolution) and paint resolution (solid/gradient fills,
  line styles). Phase 1d added DrawingML text layout and rendering — structural text parsing, an
  attribute-level run/paragraph property-inheritance resolver, word-wrap/alignment/vertical-
  anchor/autofit layout, and glyph-ink text rendering. Phase 1e added `<p:pic>` picture-shape
  decoding/cropping/compositing, `<a:tbl>` table structure/cell-rect/paint resolution, and
  recursive, full `<p:spTree>` shape-tree parsing (including nested `<p:grpSp>` enumeration).
  Phase 1f (the current release) added the public, slide-level `Render` API — a document-order
  walk of a slide's full shape tree dispatching each node to the already-verified Phase 1c/1d/1e
  resolvers and painters — followed by subsequent Phase 2 Follow-Ups adding slide/layout/master
  `<p:bg>` background-fill resolution ahead of that walk, `<a:buChar>`/`<a:buAutoNum>`
  bullet/numbering rendering, and `<p:cxnSp>` connector-shape rendering. Radial/path gradients,
  full text justification, `spAutoFit` shape-resize behavior, kerning, text clipping on overflow,
  nested tables remain explicitly deferred. Pattern fill (a
  documented subset of 30 of the 54 named ECMA-376 `ST_PresetPatternVal` preset names, including
  background fills; the remaining 24 are deferred) and picture fill are both implemented — see
  _CanvasNetPptx System Design_ (`canvas-net-pptx.md`) for the full supported/deferred boundary.
  `CanvasNetPptx` depends on the `CanvasNet` system's `Canvas`, `Geometry`,
  `Drawing`, `Fonts`, `Codecs`, `Packaging`, and `Rendering` subsystems (for the `Rgba32` color type, path geometry/
  stroking, font/glyph resolution, and image decoding used to resolve and render shape/text/
  picture content), and on the `CanvasNetCharts` system (for parsing and rendering embedded
  `<p:graphicFrame>` charts — see `OpenXmlChartParser`/`ChartRenderer` below) — see
  _CanvasNetPptx System Design_ (`canvas-net-pptx.md`)
- **CanvasNetCharts (System)** — A separate, independently-distributed software system providing
  chart support, containing two subsystems: `ChartModel`, containing two units, `ChartDocument` —
  the public, immutable, validating chart data model (`Chart`/`ChartSeries`/
  `ChartAxis`/`ChartLegend`/`ChartTitle`/`ChartType`), the `ChartBuilder` fluent construction API,
  and a `ChartRenderer` unit that paints a `Chart` onto a core `Surface`; and `OpenXmlChart`,
  containing a single unit, `OpenXmlChartParser`, which parses an OOXML `chart1.xml` part into
  the `ChartModel` data model. The `CanvasNetPptx` system integrates `CanvasNetCharts` into its
  own slide rendering, automatically parsing and rendering embedded `<p:graphicFrame>` charts —
  see _CanvasNetPptx System Design_ (`canvas-net-pptx.md`) for that integration's detail. Bar,
  column, line, pie, doughnut, and area OOXML chart types are supported; radar, scatter, bubble,
  stock, surface, 3-D, "of pie", and multi-chart-type ("combo") OOXML charts remain explicitly
  deferred (see _OpenXmlChartParser Unit Design_ (`open-xml-chart-parser.md`) for the complete
  supported/deferred boundary). `CanvasNetCharts` depends on this `CanvasNet`
  system's `Canvas`, `Drawing`, `Geometry`, `Fonts`, and `Rendering` subsystems (for the `Rgba32`
  color type and pixel buffer, path filling/stroking and tile-paint primitives, transforms and
  rectangles, TrueType text layout/metrics, and the bundled Liberation Sans fallback font), and
  must never reference `CanvasNetSvg`, `CanvasNetPdf`, `CanvasNetPptx`, or `CanvasNetVsdx` — see
  _CanvasNetCharts System Design_ (`canvas-net-charts.md`)
- **CanvasNetVsdx (System)** — A separate, independently-distributed software system providing
  Microsoft Visio (`.vsdx`) diagram-rendering support, containing a single unit, `VsdxDocument`.
  Unlike `CanvasNetPptx`'s own incrementally-delivered phase history, this feature is delivered as
  a single pull request describing its complete, final feature set: an OPC (Open Packaging
  Conventions) package layer opening a `.vsdx` file and resolving its `[Content_Types].xml` and
  relationship graph (`_rels/.rels` through `visio/document.xml` to `masters.xml`/`pages.xml`/an
  optional `theme1.xml`, exclusively through relationship references, never by filename-number
  convention); a page/shape model parsing `visio/pages/pages.xml`'s page index and each page's
  shape tree; shape geometry and transform resolution (the VisioML geometry row vocabulary and the
  shape-local-to-page affine transform, including the 1-D connector begin/end-derived transform
  special case); Master/MasterShape cell-and-geometry-row inheritance; StyleSheet chain resolution
  for line/fill/text style; text rendering; connector/glue-point routing (trusting a connector's
  own pre-baked, already-resolved endpoint coordinates rather than live glue-point tracking);
  color/fill resolution; arbitrarily nested group/child-shape handling; and the public, page-level
  `Render` API. No distinct `Type`-based handling exists for an embedded-image/`Foreign` shape -
  it renders exactly like any other shape (its own geometry/paint/text, when present, render
  normally; decoding/rasterizing the embedded image payload itself remains deferred). Non-trivial
  theme-variation resolution, non-solid
  fill-pattern combinations beyond solid, the full arrowhead style-index table, and several
  documented-but-unobserved geometry row types remain explicitly deferred — see
  _CanvasNetVsdx System Design_ (`canvas-net-vsdx.md`) for the full supported/deferred boundary and
  its Implementation Phase Plan. `CanvasNetVsdx` depends on the `CanvasNet` system's `Canvas`,
  `Geometry`, `Drawing`, and `Fonts` subsystems, and must never reference `CanvasNetPptx`,
  `CanvasNetPdf`, or `CanvasNetCharts` — a future, explicitly out-of-scope dependency on the
  sibling `CanvasNetSvg` system (to rasterize an embedded SVG foreign object) is anticipated but
  not yet modeled — see _CanvasNetVsdx System Design_ (`canvas-net-vsdx.md`)
- **CanvasNetRender3D (System)** — A separate, independently-distributed, format-agnostic software
  system providing a pure-managed 3D mesh/scene rasterizer, containing three subsystems:
  `SceneModel` (units `DoublePrecisionMath` — `Vector2D`/`Vector3D`/`Matrix4x4D`/`BoundingBox3D`;
  `Mesh` — immutable triangle meshes, `MeshBuilder` and the internal `MeshTopology`; and
  `SceneGraph` — an immutable scene graph of `MeshNode`/`GroupNode`/`CsgNode` with transforms and
  per-node colors, and `Scene`), `Tessellation` (units `PrimitiveTessellator` — closed, outward-oriented box,
  sphere, cylinder and cone meshes, validated polyhedra, and 2D profile regions; `PolygonTriangulator` —
  validated even-odd `PolygonRegion` and an exact ear-clipping triangulator with hole bridging; and
  `Extruder` — linear extrusion with twist, scale and apex, and rotation about the Z axis, producing
  closed outward-oriented meshes; `ConvexHull` — exact `Hull3D`/`Hull2D` of point sets with the internal
  `HullBuilder3D` and `SolidPredicates`; and `MinkowskiSum` — `ConvexSum` of convex solids that rejects
  non-convex operands), and `SceneRendering` (units `Camera` —
  perspective/orthographic projection and `FitToBounds` framing; `Lighting` — ambient/directional
  lights with flat Lambert and optional Blinn-Phong shading; `TriangleRasterizer` — homogeneous
  clipping and a fixed-point 24.8 top-left-rule rasterizer; `FragmentBuffer` — a per-worker,
  budgeted, exact-sized store of all fragments of a tile; `CsgResolver` — the boolean-tree compiler
  and the per-sample fragment-list resolver; and `SceneRenderer` — a tiled driver with a z-buffer
  path and a fragment-list constructive-solid-geometry path, supersampling antialiasing,
  deterministic tile parallelism, a per-tile fragment budget, closed-operand reporting, and
  cooperative cancellation). It renders a `Scene` onto a new core `Surface`. Constructive solid
  geometry (union, difference, intersection) is delivered through per-pixel fragment lists, and
  scenes without a difference or intersection keep the z-buffer path; transparency and any 3D
  file-format front end remain out of scope — see _CanvasNetRender3D System Design_
  (`canvas-net-render3d.md`) for the full supported/deferred boundary and its Implementation Phase
  Plan. `CanvasNetRender3D` depends on the `CanvasNet` system's `Canvas`
  subsystem only (for `Surface` and `Rgba32`), and must never reference `CanvasNetSvg`,
  `CanvasNetPdf`, `CanvasNetPptx`, `CanvasNetVsdx`, or `CanvasNetCharts`
- **CanvasNetScad (System)** — A separate software system (packable as `DemaConsulting.CanvasNet.Scad`)
  providing a clean-room front end for the OpenSCAD language that parses, evaluates, builds the geometry of,
  and renders models. It contains one direct unit, `ScadDocument` (the public `Parse`/`Open`/`Evaluate`/
  `BuildScene`/`Render` API with `ScadOpenOptions`, `ScadEvaluationOptions`, `ScadEvaluationResult`,
  `ScadRenderOptions`, `ScadResolvedView`, `ScadViewSource`, `ScadLimits`, `ScadSourceLocation`,
  `ScadSyntaxException`, `ScadEvaluationException`,
  and the recorded `include`/`use` directives, module names, and function names), and three internal
  subsystems. The `ScadLanguage` subsystem has two units:
  `ScadLexer` (tokens with one-based locations) and `ScadParser` (recursive-descent statements and
  precedence-climbing expressions into an immutable syntax tree, with fail-fast single-error reporting and
  bounded nesting and source size). The `ScadEvaluation` subsystem has four units: `ScadValue` (the value
  model and the deterministic number format), `ScadEvaluator` (scoping, special variables, functions,
  modules and children, control flow, comprehensions, and a geometry-agnostic instantiation tree, with
  deterministic, stack-independent resource limits), `ScadBuiltinFunctions` (the built-in function library
  and a platform-independent random generator), and `ScadFileResolver` (`include`/`use`
  resolution inside a sandbox). The `ScadGeometry` subsystem has five units: `ScadGeometryBuilder` (the
  iterative, recursion-free walk that combines the instantiation tree into a Render3D scene, with modifiers,
  booleans, the hull and minkowski convex combinations, the projection of solids, the triangle budget, reporting,
  and the
  node-to-location map), `ScadPrimitives` (cube, sphere,
  cylinder, polyhedron, square, circle, polygon, and the fragment rule), `ScadTransforms` (translate, rotate,
  scale, mirror, multmatrix, resize, and color), `ScadExtrusions` (linear and rotational extrusion over a
  symbolic 2D shape tree), and `ScadTextGeometry` (the text module: font resolution, glyph layout, and flat 2D
  glyph regions). The 2D Minkowski sum and a few other constructs are deferred and reported as
  unsupported features — see _CanvasNetScad System Design_ (`canvas-net-scad.md`) for the full
  supported/deferred boundary and its Implementation Phase Plan. `CanvasNetScad` depends on the `CanvasNet`
  system's `Canvas` subsystem (`Surface`, `Rgba32`, `CssColor`), its `Files` subsystem (`SandboxedFileResolver`, for
  `include` and `use`), and, for the `text` module, on its `Fonts` (`TrueTypeFont`,
  `SystemFontCatalog`, and the bundled Liberation fonts) and `Geometry` (`Path`, the glyph outlines) subsystems,
  and on `CanvasNetRender3D` (the scene, tessellation, and rendering API), all through their public API, and must
  never reference `CanvasNetSvg`, `CanvasNetPdf`, `CanvasNetPptx`,
  `CanvasNetVsdx`, or `CanvasNetCharts`
- **CanvasNetVegaLite (System)** — A separate software system (packable as `DemaConsulting.CanvasNet.VegaLite`)
  providing a static renderer for the public Vega-Lite specification language that parses, describes, measures, and
  renders charts, derived with attribution from Vega, Vega-Lite, and d3 (ISC and BSD-3-Clause license texts ship in
  the package; not affiliated with those projects). It contains one direct unit, `VegaLiteDocument` (the public
  `Parse`/`Open`/`Measure`/`Render` API with `VegaLiteOpenOptions`, `VegaLiteRenderOptions`, `VegaLiteLimits`,
  `VegaLiteFontOptions`, `VegaLiteLocation`, `VegaLiteDiagnostic`, `VegaLiteUnsupportedFeature`, `VegaLiteSize`,
  `VegaLiteDataReference`, `VegaLiteSpecException`, `VegaLiteRenderException`, and
  `VegaLiteUnsupportedFeatureException`), and nine internal subsystems. The `SpecModel` subsystem has five units:
  `SpecParser` (UTF-8 JSON into an immutable tree with JSON pointers and line and column under size and depth limits,
  structural validation, the compilation of expressions, and duplicate and unknown key warnings), `FeatureCatalog` (the
  stable `vl-*` reports of every recognized-but-unsupported construct), `ConfigResolver` (the Vega and Vega-Lite default
  configuration merged with the configuration of the specification and of the caller), and `SpecNormalizer` (the view
  tree of a specification - layers, concatenations, facets, and the copies of repeats - with the inheritance, `resolve`,
  and layout settings of its views, and the expansion of the composite marks `boxplot`, `errorbar`, and `errorband` into
  layers, under the `MaxViews` and `MaxCompositionDepth` limits), and `ExpressionReferenceFolder` (the evaluation, once
  per call, of the `{"expr": ...}` references of the static sites of a specification - the title, the padding and
  background, the properties of the guides, scales, and marks that no row reads, and the configuration - and the rewrite
  of the tree so that every later stage reads values). The `Data` subsystem has six units: `DataTable` (typed
  values and tables), `DataLoader` (the source of a view - inline rows, a local file, a named dataset, or a sequence -
  under data limits), `DataFileResolver` (sandboxed, bounded, strict-UTF-8 reading of local data files, or through the
  caller's callback), `DelimitedTextParser` (CSV, TSV, and DSV, the `parse` conversions, and the per-column type
  inference), `TimeCalendar` (ECMAScript time arithmetic over the years 1 to 9999 in UTC or the caller's time zone, the
  d3-time intervals, the Vega-Lite time units, and the `DateTime` object), and `TimeFormat` (d3-time-format formatting
  and parsing with the English locale, Vega's multi-format, and JavaScript's `Date.parse`). The `Expressions` subsystem
  has three units: `ExpressionParser` (a bounded compiler of the Vega expression language into an immutable postfix
  program), `ExpressionEvaluator` (a culture-invariant interpreter with JavaScript coercion under a step budget), and
  `ExpressionFunctions` (the closed table of 113 pure functions, including the date and time functions, the number
  `format`, the selection functions, and the probability distribution functions, with the safe
  regular expression translation). The `Transforms` subsystem has five units: `RowTransforms` (the static parameter
  environment and the `calculate`, `filter`, `fold`, `flatten`, `sample`, `pivot`, `lookup`, and `timeUnit` transforms
  between the loader and the scales), `SelectionModel` (the initial state of the selection parameters: their stores and
  resolved values), `AggregateTransforms` (the `aggregate`, `joinaggregate`, `window`, `bin`, `stack`,
  and `impute` transforms with the aggregate operations, the bin algorithm, and the stack rules), `EncodingPlan` (the
  implicit bin, time unit, aggregate, impute, and stack of a view's encoding, with the path grouping and order fields
  and the field names and titles that every channel reads), `TimeUnitTransform` (the start and the end of a time
  unit for every row, and the fields that are read as dates), and `StatTransforms` (the `regression`, `loess`,
  `density`, `quantile`, and `extent` transforms, with the regression models, the curve sampling, the locally weighted
  regression, and the kernel density). The `Scales` subsystem has six units: `ScaleModel` (the
  linear, time, UTC, logarithmic, power, symlog, band, point, ordinal, sequential, diverging, and discretizing
  mappings), `ScaleResolver` (the Vega-Lite inference of scale types, domains, ranges, and paddings, including the polar
  `theta` and `radius` scales, and the channels that each mark draws), `Ticks` (the d3-array and d3-time tick
  algorithms), `NumberFormat` (the d3-format number formatter), `ColorSchemes` (the 68 Vega color schemes and the
  configuration of a color range), and `ColorInterpolator` (the sRGB, HSL, CIELAB, and HCL color interpolation). The
  `Guides` subsystem has seven units: `TextMeasurer` (bundled-font resolution, measurement, and truncation),
  `TitleBuilder` (title and subtitle), `AxisBuilder` (grid, ticks, labels, domain, and title of an axis), `LabelOverlap`
  (parity and greedy removal of overlapping labels), `LegendBuilder` (symbol, gradient, and discrete gradient legends),
  `TimeLabels` (the label format, interval tick count, and default tick count of time axes and legends), and
  `LabelExpressions` (the `labelExpr` of axes, legends, and facet headers). The
  `Layout` subsystem has three units, `ViewLayout` (data rectangle, padding, autosize including fit and container sizes,
  title placement, view border, and
  the helpers of composed charts), `LegendLayout` (the placement of the legends around the data rectangle and the axes,
  and their extents), and `CompositionLayout` (the grid of the cells of a concatenation or a facet with its headers,
  footers, and titles). The `Marks` subsystem has two units: `MarkBuilder` (bar, rect, point, circle, square, rule,
  tick, line, area, trail, text, and arc geometry and paint, with the color, size, shape, opacity, stroke, and angle
  channels, the point and line overlays, the invalid data modes, the draw order, the polar and text rules, time-unit
  bands, and conditional encodings) and `CurveBuilder` (the d3-shape curves and the line and area generators that turn
  the defined points of a path into segments). The `Scenegraph` subsystem has five units: `SceneModel` (the immutable
  scene), `ScenePainter` (drawing a scene onto a `Surface`), `ArcGeometry` (the d3-shape arc and the d3-path emulation
  behind the arc and trail items), `CssPaint` (the chart paint rules on top of the core `CssColor` parser), and
  `SymbolPathParser` (the parser, drawing, and bounds of the SVG path data of a custom symbol shape).
  Selection parameters are drawn in their initial state since phase 12, and the parts that are not modeled are reported
  with stable identifiers
  (local CSV, TSV, DSV, and JSON data files, named datasets, and sequences, a bounded subset of the Vega expression
  language, static variable parameters (their expression references at the static sites included), the row transforms
  `calculate`, `filter`, `fold`, `flatten`, `sample`, `pivot`,
  `lookup`, and `timeUnit`, the aggregating transforms `aggregate`, `joinaggregate`, `window`, `bin`, `stack`, and
  `impute`, the aggregate, bin, stack, and sort properties of a channel, time (temporal fields, time units, time scales
  and axes, date formats, and the `TimeZone` option, with the calendar in UTC by default), the line, area, trail,
  text, and arc marks with curve interpolation, overlays, `impute`, and polar channels, layers, concatenations
  (`concat`, `hconcat`, `vconcat`), facets (the `facet` operator and the `row`/`column` channels), and repeats, with
  their headers and titles, `autosize` of `fit`, `fit-x`, and `fit-y`, and container sizes are supported; remote data
  is never fetched) — see _CanvasNetVegaLite System Design_ (`canvas-net-vega-lite.md`) for the full supported/deferred
  boundary and its Implementation Phase Plan. `CanvasNetVegaLite` depends on the `CanvasNet` system's `Canvas`,
  `Geometry`, `Drawing`, `Fonts`, `Files`, and `Rendering` subsystems through their public API, and must never reference
  `CanvasNetCharts`, `CanvasNetSvg`, `CanvasNetPdf`, `CanvasNetPptx`, `CanvasNetVsdx`, `CanvasNetRender3D`, or
  `CanvasNetScad`
- **CanvasNetMermaid (System)** — A separate software system (packable as `DemaConsulting.CanvasNet.Mermaid`)
  providing a static, offline renderer for the Mermaid diagram language that parses, describes, measures, and renders
  diagrams with Mermaid's own layout, shapes, labels, edges, and themes, derived with attribution from Mermaid,
  dagre-d3-es (dagre and graphlib), d3, khroma, js-yaml, lodash, and marked (their license texts ship in the package;
  not affiliated with those projects). It contains one direct unit, `MermaidDocument` (the public
  `Parse`/`Open`/`Measure`/`Render` API with `MermaidOpenOptions`, `MermaidRenderOptions`, `MermaidLimits`,
  `MermaidLocation`, `MermaidDiagnostic`, `MermaidUnsupportedFeature`, `MermaidSize`, `MermaidDiagnosticCodes`,
  `MermaidSyntaxException`, `MermaidRenderException`, `MermaidUnsupportedFeatureException`, the internal report sink,
  and `MermaidImageSource`), and fifteen internal subsystems: `Syntax` (`Preprocessor`, `DiagramDetector`,
  `YamlSubsetParser`, `SourceScanner`, and `HtmlSanitizer`), `Configuration` (`MermaidConfigResolver`,
  `ThemeResolver`, `ColorMath`, and `StyleDeclarations`), `Graphlib` (`Graph` and `GraphAlgorithms`), `Dagre`
  (`DagreLayout`, `DagreAcyclic`, `DagreRanker`, `DagreNesting`, `DagreNormalizer`, `DagreOrdering`,
  `DagrePositioning`, `DagreCoordinateSystem`, and `LayoutWorkMeter`), `Rendering` (`ClusterLayout`, `ShapeLibrary`,
  `EdgeBuilder`, `LabelBuilder`, and `NodeImages`), `Scenegraph` (`SceneModel` and `ScenePainter`), `Flowchart`
  (`FlowchartParser` and `FlowchartModel`), `Sequence` (`SequenceParser`, `SequenceModel`, `SequenceLayout`, and
  `SequenceRenderer`), `Class` (`ClassParser` and `ClassModel`), `State` (`StateParser` and `StateModel`), `Er`
  (`ErParser` and `ErModel`), `Pie` (`PieParser` and `PieRenderer`), `Gantt` (`GanttParser`, `GanttModel`,
  `GanttRenderer`, and `TimeAxis`), `Mindmap` (`MindmapParser` and `MindmapRenderer`), `Cose` (`CoseLayout`), and
  `Packet` (`PacketParser` and `PacketRenderer`). This version draws flowcharts, sequence diagrams, class diagrams,
  state diagrams, entity relationship diagrams, pie charts, gantt charts, mindmaps, and packet diagrams; every other
  Mermaid diagram type is recognized and reported as unsupported — see _CanvasNetMermaid System Design_
  (`canvas-net-mermaid.md`) for the known differences from Mermaid and its Phase Plan. `CanvasNetMermaid` depends on the
  `CanvasNet` system's `Canvas`, `Geometry`, `Drawing`, `Fonts`, `Rendering`, and `ImageSources` subsystems through
  their public API only
- **CanvasNetTool (System)** — A separate software system, shipped as the `DemaConsulting.CanvasNet.Tool` dotnet
  tool package (command `canvasnet`), that lets people, scripts, and AI agents inspect, render, transform, compare,
  and compose
  images and documents with the CanvasNet libraries, clean up photographs of sketches, and measure their colors, and
  offers a small, stable error contract (exit codes 0 and 1, concise error text, stable error codes, and a versioned
  JSON result contract). It is a thin command-line shell over the `Imaging` subsystem of the `CanvasNetViewer` system.
  It follows the DEMA
  Consulting tool template and contains six
  direct units - `Program` (entry point, error policy, and the `ToolException` supporting type, which derives from the
  `ViewerException` of the Viewer), `Context`
  (command line, command capture, output channels), `ArgumentReader` (command-line grammar and value parsers),
  `ResultWriter` (the JSON result documents), `Validation` (the `--validate` self-test and results
  output), and `PathHelpers` (safe path combination) - and one subsystem, `Commands` (the command contract, the
  `CommandRegistry` unit, the `HelpTopics` unit with the help topics and their examples, the `DiscoveryCommands` unit
  with the `formats` and `info` commands, and the
  `RenderCommand` unit with the `render` command, its alias `convert`, and the verb commands `crop`, `resize`,
  `rotate`, `flip`, and `pad` (and the `ImagePipelineParser` that reads the pipeline operations from the command line),
  the `CompareCommand` unit with the `compare` command, the `MontageCommand` unit
  with the `montage` command, the `InspectCommand` unit with the `inspect colors` and `inspect pixel` commands, and
  the `FlattenCommand` unit with the `flatten` command).
  It uses
  the other systems of this repository through private project references, so that their assemblies are bundled
  into the tool package — see _CanvasNetTool System Design_ (`canvas-net-tool.md`)

- **CanvasNetViewer (System)** — A separate software system (packable as `DemaConsulting.CanvasNet.Viewer`) that
  aggregates the image sources of every format and is the library form of the `canvasnet` tool. It contains two direct
  units and one subsystem. The direct unit `CanvasNetViewer` (with `ViewerRenderOptions` and `ViewerImage`) lists the
  built-in image sources (raster, SVG, PDF, PowerPoint, Visio, OpenSCAD, Vega-Lite, and Mermaid) and the extensions
  they claim, creates the `FolderImageResolver` of the core `ImageSources` subsystem for one render so that an SVG
  document can embed every format, renders any file by path or from a stream, and lets a library user add image sources
  of their own (later sources win; nothing is cached). The direct unit `ViewerException` is the exception of every
  expected failure, with the stable `ErrorCodes`. The `Imaging` subsystem holds the `SourceLoader` unit, which
  recognizes, probes, and loads PNG, JPEG, BMP, TIFF, GIF, JPEG 2000, and SVG files, the pages of PDF, PPTX, and VSDX
  documents, views of OpenSCAD models, Vega-Lite charts, and Mermaid diagrams, the `ImagePipeline` unit, which crops,
  resizes, fits, rotates by any angle, flips, pads, adjusts, deskews, trims, and grids pixels in the order the
  operations are given, the `GridOverlay` unit, which does the grid work for it (the flip, pad, trim, tone, rotation,
  skew, and analysis arithmetic is the core library's `Surface`), the `ImageWriter` unit, which writes PNG, JPEG, BMP,
  and TIFF files, the `ImageComparison` unit, which checks how two images compare (measured by the core library's
  `Surface`) against thresholds, the `ContactSheet` unit, which composes images into a labeled sheet, and the five
  operation units that chain them: `ImageRenderer` (render the pages of a source), `ImageInspector` (colors and pixels),
  `ImageComparer` (compare two images), `MontageBuilder` (a contact sheet), and `SvgFlattener` (a copy of an SVG drawing
  that other SVG readers can show). It references the core package and the
  Svg, Pdf, Pptx, Vsdx, Scad, Render3D, VegaLite, and Mermaid packages, is referenced only by the tool, and has no
  plug-in mechanism of its own — see _CanvasNetViewer System Design_ (`canvas-net-viewer.md`)

The following OTS items are also covered:

- **BuildMark** — build-notes documentation tool
- **FileAssert** — document assertion tool
- **Pandoc** — Markdown-to-HTML conversion tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SonarMark** — SonarCloud quality report tool
- **SysML2Tools** — architecture model lint and diagram rendering tool
- **System.Numerics.Tensors** — vectorized bulk pixel-arithmetic runtime library
- **DemaConsulting.TestResults** — self-validation result recording and TRX/JUnit serialization library of the
  tool package
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework

Version applicability: This design applies to all versions of CanvasNet.

The following topics are explicitly excluded from this design documentation:

- External library internals and third-party OTS components
- Build pipeline configuration and CI/CD processes
- Deployment, packaging, and distribution mechanisms
- Infrastructure and hosting environment details
- Test projects and test infrastructure

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` and rendered to the
diagram below by SysML2Tools as part of the build pipeline. AI agents should query the
SysML2 model directly (see the `sysml2tools-query` skill) rather than parsing this
diagram or the prose below.

![Software Structure](SoftwareStructureView.svg)

CanvasNet is organized into eight subsystems under the system level: the `Canvas` subsystem
(the `Surface` unit with its `SurfaceAdjustments`, `SurfaceTransforms`, `SurfaceGeometry`, `SurfaceComparison`, and
`SurfaceAnalysis` parts, and the `Rgba32` and `CssColor` units,
namespace `DemaConsulting.CanvasNet.Canvas`), the `Codecs`
subsystem (the `BmpCodec`, `PngCodec`, `TiffCodec`, `JpegCodec`, `GifCodec`, `Jpeg2000Codec`, and `ImageDecoder` units,
namespace `DemaConsulting.CanvasNet.Codecs`, flat — no further nesting), the
`Geometry` subsystem (the
`Rect`, `Path`, `BezierFlattening`, and `SvgArcConverter` units, namespace
`DemaConsulting.CanvasNet.Geometry`, flat — no further nesting), the `Drawing` subsystem (the
`PathFiller` unit, covering the supporting `FillRule` enum, the internal
`EdgeFlattener`/`ScanlineRasterizer` helpers, and the internal `ClipMask` clip-coverage helper
inline, the `PathStroker` unit, covering the
supporting `LineCap`/`LineJoin`/`StrokeStyle` types and the internal
`StrokePathFlattener`/`DashSplitter`/`StrokeOutliner` helpers inline, the `GradientPaint`
unit, covering the public `Gradient`/`LinearGradient`/`RadialGradient`/`GradientStop`/
`GradientSpread` types and the internal `GradientEvaluator` helper inline, and the `TilePaint`
unit, added alongside `CanvasNetPdf`'s `/Pattern` color-space support, covering the public
`TilePaint` type and the internal `TilePaintEvaluator` helper inline, namespace
`DemaConsulting.CanvasNet.Drawing`, flat — no further nesting), the `Fonts` subsystem (the
`TrueTypeFont` unit, covering the internal `SfntContainer`/`CmapTable`/`GlyfLocaReader`/
`IGlyphOutlineSource`/`CffTable`/`CffCharstringInterpreter`/`CffStandardEncoding`/`Type1Table`/
`Type1CharstringInterpreter`/`Type1CharstringDecryption`/`Type1PfbReader`/`Type1PfaReader`/
`Type1StandardGlyphNames`/`HmtxHheaReader`/`KernTable`/`NameTable`/`StyleTable` helpers inline,
and the `SystemFontCatalog` unit, added Phase 6 of
the `CanvasNetPdf` roadmap, with no internal helpers of its own, the `FontDirectoryScanner`,
`GenericFontFamilies`, `FontResolver`, and `GlyphFallback` units (the last: per-character fallback to the
bundled Noto fonts), namespace
`DemaConsulting.CanvasNet.Fonts`, flat — no further nesting), the `Rendering` subsystem (the
transform-aware `Canvas` wrapper unit,
the `TextRenderer` unit, covering the supporting `TextAlign` and `TextMetrics` types inline,
the `Shapes` extension-method unit, and the `WordWrap` line-packer unit, covering the supporting
`WordWrapTokenKind` enum inline, namespace `DemaConsulting.CanvasNet.Rendering`, flat — no
further nesting), the `Files` subsystem (the `SandboxedFileResolver` unit, covering the public
`SandboxOptions`, `SandboxedFile`, `SandboxException`, and `SandboxFailureKind` types inline, namespace
`DemaConsulting.CanvasNet.Files`, flat — no further nesting), the `ImageSources` subsystem (the
`ImageSourceContract`, `FolderImageResolver`, and `RasterImageSource` units, namespace
`DemaConsulting.CanvasNet.ImageSources`, flat — no further nesting), and the `Packaging` subsystem (the
`OpcPackage` unit, covering its
`OpcFormatException` inline, and the `SafeXml` unit, namespace
`DemaConsulting.CanvasNet.Packaging`, flat — no further nesting; the `CanvasNetPptx`,
`CanvasNetVsdx`, and `CanvasNetSvg` systems use it). As additional functionality is added, further
subsystems and nested
subsystems would organize related units and provide architectural boundaries with well-defined
interfaces and responsibilities.

A sibling top-level system, `CanvasNetSvg`, lives in this same repository alongside `CanvasNet`
(rather than as one of its subsystems): its sole unit, `SvgCodec`, namespace
`DemaConsulting.CanvasNet.Svg`, is distributed as its own separate NuGet package and depends on
the `CanvasNet` system's `Canvas`, `Geometry`, `Drawing`, `Fonts`, `Codecs`, and `Packaging` subsystems — see
the Folder Layout section below and _CanvasNetSvg System Design_ (`canvas-net-svg.md`).

A second sibling top-level system, `CanvasNetPdf`, likewise lives in this same repository
alongside `CanvasNet`: its sole unit, `PdfDocument`, namespace `DemaConsulting.CanvasNet.Pdf`, is
distributed as its own separate NuGet package and depends on the `CanvasNet` system's `Canvas`,
`Geometry`, `Drawing`, `Fonts`, and `Codecs` subsystems (`Render` rasterizes real page-content
geometry, device color (including shading/tiling pattern fills), image XObjects and nested Form
XObjects, and text — TrueType, Type 1, Type 1C, Type 3, or Type 0/CID-keyed — with automatic
font-fallback substitution, optionally decrypting an encrypted document first) —
see the Folder Layout section below and _CanvasNetPdf
System Design_ (`canvas-net-pdf.md`).

A third sibling top-level system, `CanvasNetPptx`, likewise lives in this same repository
alongside `CanvasNet`: its sole unit, `PptxDocument`, namespace `DemaConsulting.CanvasNet.Pptx`,
is distributed as its own separate NuGet package and depends on the `CanvasNet` system's `Canvas`,
`Geometry`, `Drawing`, `Fonts`, `Codecs`, `Packaging`, and `Rendering` subsystems, on `CanvasNetSvg`
(for SVG pictures), and
on `CanvasNetCharts` (for chart graphic frames) (`Render` opens a `.pptx` package's OOXML
structure, resolves the slide/layout/master/theme chain and placeholder property inheritance,
resolves DrawingML shape geometry, paint, connectors, pictures, tables, and chart graphic frames,
lays out and paints text including bullets and numbering, and paints a slide's full shape tree in
document order) — see the Folder Layout section below and _CanvasNetPptx System Design_
(`canvas-net-pptx.md`).

A fourth sibling top-level system, `CanvasNetVsdx`, likewise lives in this same repository
alongside `CanvasNet`: its sole unit, `VsdxDocument`, namespace `DemaConsulting.CanvasNet.Vsdx`,
is distributed as its own separate NuGet package and depends on the `CanvasNet` system's
`Canvas`, `Geometry`, `Drawing`, `Fonts`, `Packaging`, and `Rendering` subsystems (`Render` opens a `.vsdx` package's
OPC
structure, resolves the Master/MasterShape cell-and-geometry-row inheritance chain and the
StyleSheet line/fill/text style chain, resolves shape/connector transforms and geometry rows to
paintable paths — including Milestone 11's real `EllipticalArcTo`/`ArcTo`-to-Bezier conversion —
composes arbitrarily nested group/child-shape transforms, and paints a page's full shape tree,
including text, in document order) — see the Folder Layout section below and
_CanvasNetVsdx System Design_ (`canvas-net-vsdx.md`).

A fifth sibling top-level system, `CanvasNetCharts`, likewise lives in this same repository
alongside `CanvasNet`, distributed as its own separate NuGet package
(`DemaConsulting.CanvasNet.Charts`) and depending on the `CanvasNet` system's `Canvas`, `Geometry`,
`Drawing`, `Fonts`, and `Rendering` subsystems. It has two subsystems of its own — `ChartModel`
(the `ChartDocument` and `ChartRenderer` units: the immutable, validating chart data model and
`ChartBuilder`, plus the pixel-rendering engine; namespace `DemaConsulting.CanvasNet.Charts`, flat)
and `OpenXmlChart` (the `OpenXmlChartParser` unit: a format-agnostic parser from a raw ECMA-376
DrawingML-Charts element to a `Chart`; namespace `DemaConsulting.CanvasNet.Charts.OpenXml`) — so
that the untrusted-input XML parser sits behind its own architectural boundary. `CanvasNetPptx`
consumes both subsystems; `CanvasNetCharts` never references `CanvasNetPptx`. See the Folder
Layout section below and _CanvasNetCharts System Design_ (`canvas-net-charts.md`).

A further sibling top-level system, `CanvasNetRender3D`, likewise lives in this same repository
alongside `CanvasNet`, distributed as its own separate NuGet package
(`DemaConsulting.CanvasNet.Render3D`) and depending only on the `CanvasNet` system's `Canvas`
subsystem. Unlike the single-unit siblings it has three subsystems of its own — `SceneModel`
(`DoublePrecisionMath`, `Mesh`, `SceneGraph`), `Tessellation` (`PrimitiveTessellator`,
`PolygonTriangulator`, `Extruder`, `ConvexHull`, `MinkowskiSum`), and
`SceneRendering` (`Camera`, `Lighting`, `TriangleRasterizer`, `FragmentBuffer`, `CsgResolver`,
`SceneRenderer`) — one per sub-namespace (`DemaConsulting.CanvasNet.Render3D.SceneModel`,
`.Tessellation`, and `.SceneRendering`), so that the 3D math and scene model, the mesh generators,
and the rasterization pipeline can each evolve (the constructive-solid-geometry phase, for
example, added units to `SceneRendering` without touching the other two) behind their own
architectural boundary. The root namespace contains no types. See the Folder Layout section below
and _CanvasNetRender3D System Design_ (`canvas-net-render3d.md`).

A further sibling top-level system, `CanvasNetScad`, likewise lives in this same repository
alongside `CanvasNet` and is distributed as its own NuGet package. It is a clean-room front end for the OpenSCAD
language that parses, evaluates, builds the geometry of, and renders models: its direct unit, `ScadDocument`
(namespace `DemaConsulting.CanvasNet.Scad`), is the public API, its internal `ScadLanguage` subsystem
(namespace `DemaConsulting.CanvasNet.Scad.Language`) holds the `ScadLexer` and `ScadParser` units, its internal
`ScadEvaluation` subsystem (namespace `DemaConsulting.CanvasNet.Scad.Evaluation`) holds the `ScadValue`,
`ScadEvaluator`, `ScadBuiltinFunctions`, and `ScadFileResolver` units, and its internal `ScadGeometry`
subsystem (namespace `DemaConsulting.CanvasNet.Scad.Geometry`) holds the `ScadGeometryBuilder`,
`ScadPrimitives`, `ScadTransforms`, `ScadExtrusions`, and `ScadTextGeometry` units. It depends on the `CanvasNet`
system's `Canvas` (`Surface`, `Rgba32`), `Fonts` (for `text`: `TrueTypeFont` and `SystemFontCatalog`), and
`Geometry` (for `text`: the `Path` of a glyph outline) subsystems, and on `CanvasNetRender3D` (the scene model,
tessellation, and renderer), through their public API. See
the Folder Layout section below and _CanvasNetScad System Design_ (`canvas-net-scad.md`).

A further sibling top-level system, `CanvasNetVegaLite`, likewise lives in this same repository alongside `CanvasNet`
and is distributed as its own NuGet package. It is a static renderer for the public Vega-Lite specification language
that parses, describes, measures, and renders charts, derived with attribution from Vega, Vega-Lite, and d3 (the
licenses ship in the package): its direct unit, `VegaLiteDocument` (namespace `DemaConsulting.CanvasNet.VegaLite`), is
the public API, its internal `SpecModel` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.SpecModel`) holds the
`SpecParser`, `FeatureCatalog`, `ConfigResolver`, `SpecNormalizer`, and `ExpressionReferenceFolder` units, its internal
`Data` subsystem holds the `DataTable`, `DataLoader`, `DataFileResolver`, `DelimitedTextParser`, `TimeCalendar`, and
`TimeFormat` units, its internal `Expressions` subsystem holds the `ExpressionParser`, `ExpressionEvaluator`, and
`ExpressionFunctions` units, its internal `Transforms` subsystem holds the `RowTransforms`, `SelectionModel`,
`AggregateTransforms`, `EncodingPlan`, `TimeUnitTransform`, and `StatTransforms` units, its internal `Scales` subsystem
holds the `ScaleModel`, `ScaleResolver`, `Ticks`, `NumberFormat`, `ColorSchemes`, and `ColorInterpolator` units, its
internal `Guides` subsystem holds the `TextMeasurer`, `TitleBuilder`, `AxisBuilder`, `LabelOverlap`, `LegendBuilder`,
`TimeLabels`, and `LabelExpressions` units, its internal `Layout` subsystem holds the `ViewLayout`, `LegendLayout`, and
`CompositionLayout` units, its internal `Marks` subsystem holds the `MarkBuilder` and `CurveBuilder` units, and its
internal `Scenegraph` subsystem holds the `SceneModel`, `ScenePainter`, `ArcGeometry`, `CssPaint`, and
`SymbolPathParser` units (each
subsystem lives in the sub-namespace and the sub-folder of its own name). In the SysML2 model the subsystem `part def`
names carry the prefix `VegaLite` (`VegaLiteSpecModel`, `VegaLiteExpressions`, `VegaLiteTransforms`,
`VegaLiteScenegraph`, and so on) and the scene unit is `VegaLiteSceneModel`, because the model package is shared by
every system and `SceneModel` already names a `CanvasNetRender3D` subsystem. It depends on the `CanvasNet` system's
`Canvas` (`Surface`, `Rgba32`), `Geometry`, `Drawing`, `Fonts`, and `Rendering` subsystems through their public API. See
the Folder Layout section below and _CanvasNetVegaLite System Design_ (`canvas-net-vega-lite.md`).

A further sibling top-level system, `CanvasNetMermaid`, likewise lives in this same repository alongside `CanvasNet`
and is distributed as its own NuGet package. It is a static, offline renderer for the Mermaid diagram language that
parses, describes, measures, and renders diagrams with Mermaid's own layout and look, derived with attribution from
Mermaid, dagre-d3-es, d3, khroma, js-yaml, lodash, and marked (the licenses ship in the package): its direct unit,
`MermaidDocument` (namespace `DemaConsulting.CanvasNet.Mermaid`), is the public API and the image source; its internal
`Syntax` subsystem holds the `Preprocessor`, `DiagramDetector`, `YamlSubsetParser`, `SourceScanner`, and
`HtmlSanitizer` units, its internal `Configuration` subsystem holds the `MermaidConfigResolver`, `ThemeResolver`,
`ColorMath`, and `StyleDeclarations` units, its internal `Graphlib` subsystem holds the `Graph` and `GraphAlgorithms`
units, its internal `Dagre` subsystem holds the `DagreLayout`, `DagreAcyclic`, `DagreRanker`, `DagreNesting`,
`DagreNormalizer`, `DagreOrdering`, `DagrePositioning`, `DagreCoordinateSystem`, and `LayoutWorkMeter` units, its
internal `Rendering` subsystem holds the `ClusterLayout`, `ShapeLibrary`, `EdgeBuilder`, `LabelBuilder`, and
`NodeImages` units, its internal `Scenegraph` subsystem holds the `SceneModel` and `ScenePainter` units, its internal
`Flowchart` subsystem holds the `FlowchartParser` and `FlowchartModel` units, its internal `Sequence` subsystem
holds the `SequenceParser`, `SequenceModel`, `SequenceLayout`, and `SequenceRenderer` units, its internal `Class`
subsystem holds the `ClassParser` and `ClassModel` units, its internal `State` subsystem holds the `StateParser` and
`StateModel` units, its internal `Er` subsystem holds the `ErParser` and `ErModel` units, its internal `Pie` subsystem
holds the `PieParser` and `PieRenderer` units, its internal `Gantt` subsystem holds the `GanttParser`,
`GanttModel`, `GanttRenderer`, and `TimeAxis` units, its internal `Mindmap` subsystem holds the `MindmapParser` and
`MindmapRenderer` units, its internal `Cose` subsystem holds the `CoseLayout` unit, and its internal `Packet` subsystem
holds the `PacketParser` and `PacketRenderer` units (each subsystem lives in
the sub-namespace and the sub-folder of its own name). In the SysML2 model the subsystem `part def` names carry the
prefix `Mermaid` (`MermaidSyntax`, `MermaidConfiguration`, `MermaidGraphlib`, `MermaidDagre`, `MermaidRendering`,
`MermaidScenegraph`, `MermaidFlowchart`, `MermaidSequence`, `MermaidClass`, `MermaidState`, `MermaidEr`, `MermaidPie`,
`MermaidGantt`, `MermaidMindmap`, `MermaidCose`, and `MermaidPacket`) and the
scene units are
`MermaidSceneModel` and `MermaidScenePainter`,
because the model package is shared by every system and `Rendering`, `SceneModel`, and `ScenePainter` are already
taken. It depends on the `CanvasNet` system's `Canvas`, `Geometry`, `Drawing`, `Fonts`, `Rendering`, and
`ImageSources` subsystems through their public API. See the Folder Layout section below and _CanvasNetMermaid System
Design_ (`canvas-net-mermaid.md`).
A further sibling top-level system, `CanvasNetViewer`, likewise lives in this same repository and is distributed as its
own NuGet package (`DemaConsulting.CanvasNet.Viewer`). It aggregates the image sources of every format and is the
library form of the `canvasnet` tool: its direct unit `CanvasNetViewer` (namespace `DemaConsulting.CanvasNet.Viewer`)
lists the built-in sources (raster, SVG, PDF, PPTX, VSDX, OpenSCAD, Vega-Lite, and Mermaid), creates the
`FolderImageResolver` of
the core `ImageSources` subsystem for one render so that an SVG document can embed all of them, and renders any file as
a picture; its direct unit `ViewerException` is the exception of every expected failure with the stable `ErrorCodes`;
and its `Imaging` subsystem (namespace `DemaConsulting.CanvasNet.Viewer.Imaging`) holds the `SourceLoader`,
`ImagePipeline`, `ImageWriter`, `ImageComparison`, `ContactSheet`, and `GridOverlay` units that turn files into pixels,
transform pixels (grid, and the ordered operations over the core library's flip, pad, trim, tone, rotation, and skew
operations), turn pixels into files, check the comparison of two images against thresholds, and compose a contact sheet,
and the `ImageRenderer`, `ImageInspector`, `ImageComparer`, `MontageBuilder`, and `SvgFlattener` units that chain them
into the render, inspect, compare, montage, and flatten operations. It references the core package and the
`CanvasNetSvg`, `CanvasNetPdf`,
`CanvasNetPptx`, `CanvasNetVsdx`, `CanvasNetScad`, `CanvasNetRender3D`, `CanvasNetVegaLite`, and `CanvasNetMermaid`
packages; the tool references it. See the Folder Layout section below and _CanvasNetViewer System Design_
(`canvas-net-viewer.md`).

A final sibling top-level system, `CanvasNetTool`, likewise lives in this same repository and is distributed as
its own dotnet tool package (`DemaConsulting.CanvasNet.Tool`, command `canvasnet`) rather than as a library.
Unlike the library systems it is a command-line application following the DEMA Consulting tool template: its
direct units `Program`, `Context`, `ArgumentReader`, `ResultWriter`, `Validation`, and `PathHelpers` (namespace
`DemaConsulting.CanvasNet.Tool`) are the entry point, the command line and output channels, the command-line
grammar, the JSON result documents, the self-validation, and the safe path helper, and its `Commands` subsystem
(namespace `DemaConsulting.CanvasNet.Tool.Commands`) holds the `CommandRegistry`, `HelpTopics`, `DiscoveryCommands`, and
`RenderCommand`, `CompareCommand`, `MontageCommand`, `InspectCommand`, and `FlattenCommand` units (the `formats`,
`info`, `render`,
`convert`, `crop`,
`resize`, `rotate`, `flip`, `pad`, `compare`, `montage`, `inspect`, and `flatten`
commands). It is a thin shell: the commands call the operations of the `Imaging` subsystem of the `CanvasNetViewer`
system with request records built from the command line, and print the results. It uses the `CanvasNet`, `CanvasNetSvg`,
`CanvasNetPdf`, `CanvasNetPptx`, `CanvasNetVsdx`, `CanvasNetRender3D`, `CanvasNetScad`, `CanvasNetVegaLite`,
`CanvasNetMermaid`, and `CanvasNetViewer` systems
through their public API and bundles their assemblies in its package. No library system references the tool. See the
Folder Layout section below and _CanvasNetTool System Design_ (`canvas-net-tool.md`).

## Folder Layout

The source code folder structure mirrors the software structure organization, with file paths
and descriptions as follows:

```text
src/DemaConsulting.CanvasNet/
├── Canvas/
│   ├── Surface.cs               — Mutable, in-memory 32-bit RGBA pixel buffer
│   ├── Surface.Adjustments.cs   — SurfaceAdjustments: luma, grayscale, invert, thresholds, levels, brightness,
│   │                                contrast, and auto-levels (partial class of Surface)
│   ├── Surface.Transforms.cs    — SurfaceTransforms: flip, EXIF orientation, pad, trim, fit into a box, flatten,
│   │                                and the transparency test (partial class of Surface)
│   ├── Surface.Geometry.cs      — SurfaceGeometry: rotation by any angle and the rotated size (partial class of
│   │                                Surface)
│   ├── Surface.Skew.cs          — SurfaceGeometry: skew estimation (partial class of Surface)
│   ├── SkewEstimate.cs          — Public record of the estimated skew, its thresholds, and its correction
│   ├── Surface.Comparison.cs    — SurfaceComparison: metrics, structural similarity, and grid (partial class of
│   │                                Surface)
│   ├── Surface.ComparisonImages.cs — SurfaceComparison: the difference, overlay, and side-by-side pictures
│   ├── ComparisonResult.cs      — Public comparison result records (metrics, cells, result)
│   ├── DiffStyle.cs             — Public enum of the difference picture styles
│   ├── Surface.Analysis.cs      — SurfaceAnalysis: dominant colors, luma statistics, and pixel samples (partial
│   │                                class of Surface)
│   ├── SurfaceStatistics.cs     — Public analysis result records (colors, luma, samples)
│   ├── FitMode.cs               — Public enum of the box-fit modes of Surface.FitInto
│   ├── ResamplingMode.cs        — Public enum of resampling filters for Resize/ResizeToFit
│   ├── SurfaceResampler.cs      — Internal box-reduction prefilter and bilinear samplers
│   ├── SurfaceResampler.Engine.cs — Internal separable resampling engine, device-window and
│   │                                tile helpers
│   ├── Rgba32.cs                — Single 32-bit RGBA pixel value type
│   ├── CssColor.cs              — Shared CSS color parser and named-color table
│   └── NamespaceDoc.cs          — Namespace-level XML documentation
├── Codecs/
│   ├── BmpCodec.cs               — Uncompressed 24-bit/32-bit Windows BMP loader/saver
│   ├── PngCodec.cs               — PNG loader (any spec-valid, non-interlaced color type/bit
│   │                                depth) and saver (8-bit Truecolor/Truecolor-with-alpha);
│   │                                public API entry point, partial-class implementation
│   │                                continues under `Png/`
│   ├── Png/                      — `PngCodec` partial-class implementation files (chunk
│   │                                parsing, decode, filtering, zlib)
│   ├── TiffCodec.cs              — 8-bit RGB/RGBA/Grayscale, strip-based TIFF loader/saver;
│   │                                public API entry point, partial-class implementation
│   │                                continues under `Tiff/`
│   ├── Tiff/                     — `TiffCodec` partial-class implementation files (IFD, data
│   │                                source, decode, compression, utilities)
│   ├── JpegCodec.cs              — Baseline/progressive JPEG loader and baseline JPEG saver;
│   │                                public API entry point, partial-class implementation
│   │                                continues under `Jpeg/`
│   ├── Jpeg/                     — `JpegCodec` partial-class implementation files (decoder,
│   │                                scan decoding, encoder, Huffman, DCT, EXIF orientation)
│   ├── GifCodec.cs               — Decode-only, first-frame-only GIF loader; public API entry
│   │                                point, partial-class implementation continues under `Gif/`
│   ├── Gif/                      — `GifCodec` partial-class implementation files (decode, LZW)
│   ├── Jpeg2000Codec.cs          — Decode-only JPEG 2000 Part 1 decoder; public API entry
│   │                                point, partial-class implementation continues under `Jpeg2000/`
│   ├── Jpeg2000/                 — `Jpeg2000Codec` partial-class implementation files (JP2 boxes,
│   │                                codestream, packets, MQ, tier-1, DWT, decoder)
│   ├── Jpeg2000Image.cs          — Decoded JPEG 2000 image (samples, color space, alpha)
│   ├── Jpeg2000ColorSpace.cs     — Color space reported by `Jpeg2000Image`
│   ├── ImageInfo.cs              — Shared `GetInfo` return type (dimensions, channels, alpha,
│   │                                `CanDecode`, `FrameCount`) common to all six codecs
│   ├── UnsupportedImageFeatureException.cs — Thrown by `Load` for a well-formed but
│   │                                unsupported file feature (for example PNG Adam7
│   │                                interlacing), distinct from `InvalidDataException`
│   ├── ImageFormat.cs            — Public enum of the supported raster formats
│   ├── ImageFormats.cs           — Public signature and media-type recognizer
│   ├── ImageDecoder.cs           — Public decode-any-supported-format dispatcher
│   ├── ZlibHelper.cs             — Internal bounded zlib helper shared by `PngCodec`/`TiffCodec`
│   ├── PngFilter.cs              — Internal PNG scanline-filter reversal shared by `PngCodec`
│   │                                and the PDF package's PNG predictors
│   ├── LzwDecoder.cs             — Internal bounded LZW decoder shared by `TiffCodec` and the
│   │                                PDF package's `LZWDecode`
│   └── NamespaceDoc.cs           — Namespace-level XML documentation
├── Drawing/
│   ├── FillRule.cs                — Nonzero/even-odd fill-rule enumeration
│   ├── EdgeFlattener.cs           — Converts a Path's subpaths into closed polygons
│   ├── ScanlineRasterizer.cs      — Analytic coverage-accumulation scanline rasterizer
│   ├── ClipMask.cs                — Analytic-rectangle/dense clip coverage for clip-aware fills
│   ├── PathFiller.cs              — Public entry point: fills a Path onto a Surface
│   ├── LineCap.cs                 — Stroke end-cap enumeration
│   ├── LineJoin.cs                — Stroke corner-join enumeration
│   ├── StrokeStyle.cs             — Immutable stroke-style configuration snapshot
│   ├── StrokePathFlattener.cs     — Flattens subpaths while preserving open/closed state
│   ├── DashSplitter.cs            — Applies dash-array and dash-offset semantics
│   ├── StrokeOutliner.cs          — Converts stroked polylines into outline polygons
│   ├── PathStroker.cs             — Public entry point: strokes a Path into outline geometry
│   ├── GradientSpread.cs          — Gradient repeat-beyond-extent mode enumeration
│   ├── GradientStop.cs            — Single offset/color stop within a gradient
│   ├── Gradient.cs                — Abstract base for gradient paint definitions
│   ├── LinearGradient.cs          — Gradient paint that varies along a straight axis
│   ├── RadialGradient.cs          — Gradient paint that varies radially from a center point
│   ├── GradientEvaluator.cs       — Resolves a gradient definition to a color at a point
│   ├── TilePaint.cs               — Public tiled-pattern paint: a pre-rendered tile plus transform
│   ├── TilePaintEvaluator.cs      — Resolves a tile-paint definition to a color at a point
│   └── NamespaceDoc.cs            — Namespace-level XML documentation
├── Fonts/
│   ├── TrueTypeFont.cs                  — Public TrueType/CFF/Type 1 font loader/query entry point
│   ├── SfntContainer.cs                 — SFNT offset-table and directory parser
│   ├── CmapTable.cs                     — Unicode codepoint-to-glyph-index lookup
│   ├── GlyfLocaReader.cs                — Glyph location parsing and outline decoding
│   ├── IGlyphOutlineSource.cs           — Common glyph-outline-decoding contract dispatched by TrueTypeFont
│   ├── CffTable.cs                      — CFF structural parsing (Header/INDEXes/Private DICT/charset) plus lazy Type
│   │                                       2 charstring decoding
│   ├── CffCharstringInterpreter.cs      — Decodes a Type 2 charstring into glyph outline geometry
│   ├── CffStandardEncoding.cs           — Adobe StandardEncoding code-to-glyph-name table for seac-style accent
│   │                                       composition
│   ├── Type1Table.cs                    — Classic PostScript Type 1 font-program structural parsing plus lazy Type 1
│   │                                       charstring decoding
│   ├── Type1CharstringInterpreter.cs    — Decodes a Type 1 charstring into glyph outline geometry
│   ├── Type1CharstringDecryption.cs     — Type 1 eexec/charstring stream-cipher decryption
│   ├── Type1PfbReader.cs                — Reassembles a binary-framed (.pfb) Type 1 font program cleartext/encrypted
│   │                                       segments
│   ├── Type1PfaReader.cs                — Reassembles an ASCII-framed (.pfa) Type 1 font program cleartext/encrypted
│   │                                       segments
│   ├── Type1StandardGlyphNames.cs       — Default codepoint-to-glyph-name table for auto-detected standalone Type 1
│   │                                       fonts
│   ├── HmtxHheaReader.cs                — Horizontal metrics parsing and advance-width lookup
│   ├── KernTable.cs                     — Format-0 horizontal kerning lookup
│   ├── NameTable.cs                     — Parses the name table and resolves family/subfamily/full/PostScript name
│   │                                       strings
│   ├── StyleTable.cs                    — Derives bold/italic/fixed-pitch style metadata from OS/2/post/head.macStyle
│   ├── SystemFontCatalog.cs             — OS font discovery, best-effort matching, cached font load, bundled fallback
│   ├── FontDirectoryScanner.cs          — Deterministic, bounded, link-safe header-only font directory scan
│   ├── FontScanLimits.cs                — Bounds (directories, entries, font files, faces) of a font directory scan
│   ├── GenericFontFamilies.cs           — Generic-family keyword/alias classification and CSS font-family list splitting
│   ├── FontResolver.cs                  — Resolves a CSS font-family list to a loaded font (plus ResolvedFont, FontSource)
│   ├── FontResolverOptions.cs           — Directories, installed-font and alias options of a FontResolver
│   ├── GlyphFallback.cs                 — Per-character fallback to bundled Noto fonts (and the still-missing list)
│   ├── BundledFonts/                    — Embedded Liberation Sans/Serif/Mono plus Noto Symbol/ZapfDingbats substitute
│   │                                       .ttf files and OFL.txt license(s)
│   └── NamespaceDoc.cs                  — Namespace-level XML documentation
├── Geometry/
│   ├── Rect.cs                    — Axis-aligned bounding rectangle (position plus size)
│   ├── PathCommandType.cs         — Enumeration of path drawing command kinds
│   ├── PathCommand.cs             — Tagged-union path drawing command value
│   ├── Subpath.cs                 — One independent contour of a path
│   ├── Path.cs                    — Immutable vector path (ordered collection of subpaths)
│   ├── PathBuilder.cs             — Mutable, fluent builder that produces a Path
│   ├── BezierFlattening.cs        — Adaptive quadratic/cubic Bezier curve flattening
│   ├── SvgArcConverter.cs         — SVG-style elliptical arc to cubic Bezier conversion
│   ├── CornerRoundEffect.cs       — Replaces polyline corners with tangent circular arcs
│   └── NamespaceDoc.cs            — Namespace-level XML documentation
├── Files/
│   ├── SandboxedFileResolver.cs       — Public sandboxed resolver: search order, bounded read, callback mode, cache
│   ├── SandboxedFileResolver.Paths.cs — Path-text rules, root confinement, and the link walk
│   ├── SandboxOptions.cs              — Public options: base directory, search paths, callback, byte limit
│   ├── SandboxedFile.cs               — Public result: key, directory, root, and bytes of a resolved file
│   ├── SandboxException.cs            — Public exception carrying the failure kind, path, and detail
│   ├── SandboxFailureKind.cs          — Public enumeration of the reasons a path or file is refused
│   └── NamespaceDoc.cs                — Namespace-level XML documentation
├── ImageSources/
│   ├── IImageSource.cs                — Public contract of a source: name, claimed extensions and schemes, Render
│   ├── ImageSourceRequest.cs          — Public request: link, file, size, scoped resolver, token, budget contract
│   ├── ImageBudget.cs                 — Public live pixel counter shared by every request of one render
│   ├── ImageSourceResult.cs           — Public result: picture, notes, printed text, format (lists copied read-only)
│   ├── ImageSourceDiagnostic.cs       — Public note (and ImageDiagnosticSeverity.cs, its severity enumeration)
│   ├── IImageResolver.cs              — Public contract a renderer calls to turn a link into a picture
│   ├── EmbeddedImage.cs               — Public record of one loaded image
│   ├── ImageSourceException.cs        — Public exception of a source that failed on a found file
│   ├── ImageSourceBudgetException.cs  — Public refusal of a source for a picture over the pixel budget
│   ├── ImageSourceSet.cs              — Public ordered, immutable list of sources; later source wins
│   ├── FolderImageResolver.cs         — Public folder-confined resolver: dispatch, limits, scopes, direct render
│   ├── FolderImageResolver.Href.cs    — Link parsing: query/fragment, file: URIs, percent-encoding, sandbox refusals
│   ├── FolderImageResolverOptions.cs  — Public options: depth, image count, pixel budget, strictness, token
│   ├── RasterImageSource.cs           — Public source for the raster formats
│   └── NamespaceDoc.cs                — Namespace-level XML documentation
├── Rendering/
│   ├── Canvas.cs                   — Transform-aware Save/Restore/Translate/RotateDegrees wrapper
│   ├── TextAlign.cs                — Text horizontal-alignment enumeration
│   ├── TextMetrics.cs              — Measured width/ascent/descent result for a text run
│   ├── TextRenderer.cs             — Measures and draws TrueType text with alignment and kerning
│   ├── Shapes.cs                   — Fill/stroke extension helpers for rectangles, rounded
│   │                                 rectangles, and circles
│   ├── WordWrap.cs                 — Greedy word-wrap line packer (single and double precision)
│   │                                 and the WordWrapTokenKind enum
│   └── NamespaceDoc.cs             — Namespace-level XML documentation
└── Packaging/
    ├── OpcPackage.cs               — Read-only, size-bounded OPC ZIP package: content types,
    │                                 relationships, XML and byte part reads
    ├── OpcFormatException.cs       — Exception thrown for a malformed or oversized package
    ├── SafeXml.cs                  — Hardened, bounded XML loading (no DTD, no external resolver)
    └── NamespaceDoc.cs             — Namespace-level XML documentation
```

`SvgCodec` lives in the sibling `src/DemaConsulting.CanvasNet.Svg/` project folder (namespace
`DemaConsulting.CanvasNet.Svg`, distributed as the separate `DemaConsulting.CanvasNet.Svg` NuGet
package). This is not a subsystem-mirroring structure of `CanvasNet` — it is the source folder of
the separate, sibling `CanvasNetSvg` system's sole unit (see _CanvasNetSvg System Design_,
`canvas-net-svg.md`), a flat structure with no further nesting:

```text
src/DemaConsulting.CanvasNet.Svg/
├── SvgCodec.cs                — Decode/rasterize-only public API entry point; partial-class
│                                implementation continues in the remaining sibling files below
├── SvgCodec.*.cs               — `SvgCodec` partial-class implementation files (attributes,
│                                 clipping/masking, CSS cascade/parser/selectors, elements,
│                                 filters, gradients, image, markers, paint, parsing, path
│                                 data/render, patterns, preserve-aspect-ratio, shapes, text,
│                                 default font resolution)
├── SvgFontFace.cs              — Registered font-face (family/weight/style) data carrier
├── SvgFontStyle.cs             — Font-style enumeration used by `SvgFontFace`
├── SvgImageSource.cs           — `IImageSource` for `.svg` files, so an SVG file can be embedded in another document
├── SvgLoadOptions.cs           — Optional load settings (installed-font lookup on/off, `IImageResolver` for external images)
└── NamespaceDoc.cs             — Namespace-level XML documentation
```

This eight-subsystem folder structure reflects the small number of subsystems in the system
today. As the system grows with additional subsystems and units, the folder structure will
expand further to mirror the software architecture. `Canvas/Surface.cs` also gained a
`CompositeOverSpan` method used internally by `Drawing/PathFiller.cs`, and the `Drawing`
subsystem now includes the additional stroking files listed above, along with the gradient
paint files added for linear/radial gradient support in `PathFiller`. The sibling `CanvasNetSvg`
system's sole unit, `SvgCodec`, depends on the `CanvasNet` system's `Geometry`, `Drawing`,
`Fonts`, and `Codecs` subsystems (not only `Canvas`) to build and rasterize the vector paths and
text it decodes from SVG documents — see _CanvasNetSvg System Design_ (`canvas-net-svg.md`).

`PdfDocument` lives in the second sibling `src/DemaConsulting.CanvasNet.Pdf/` project folder
(namespace `DemaConsulting.CanvasNet.Pdf`, distributed as the separate
`DemaConsulting.CanvasNet.Pdf` NuGet package). Like `SvgCodec`'s own folder, this is the source
folder of a separate, sibling system's sole unit (see _CanvasNetPdf System Design_,
`canvas-net-pdf.md`), a flat structure with no further nesting:

```text
src/DemaConsulting.CanvasNet.Pdf/
├── PdfDocument.cs                    — Public API entry point: Open/PageCount/GetPageInfo/
│                                        Render/Dispose; partial-class implementation continues
│                                        in every file below
├── PdfDocument.Tokenizer.cs          — Low-level lexer (numbers/strings/names/delimiters/keywords)
├── PdfDocument.ObjectModel.cs        — Internal `PdfObject` tagged union + recursive-descent parser
├── PdfDocument.Xref.cs               — Classic xref+trailer, xref stream, object stream, hybrid `/XRefStm`,
│                                        linear-scan fallback; decrypts bytes via the key `PdfDocument.Encryption.cs`
│                                        derives
├── PdfDocument.PageTree.cs           — Catalog→Pages→Kids traversal, MediaBox/Rotate inheritance, cycle rejection,
│                                        `PdfPageInfo` production
├── PdfDocument.ContentStream.cs      — Content-stream tokenizer/dispatch loop, `/Contents` resolution (single stream
│                                        or space-joined array)
├── PdfDocument.GraphicsState.cs      — Graphics-state stack (`q`/`Q`/`cm`/`w`/`J`/`j`/`M`/`d`), CTM derivation from
│                                        `/MediaBox`/`/Rotate`
├── PdfDocument.PathOps.cs            — Path-construction (`m`/`l`/`c`/`v`/`y`/`h`/`re`) and path-painting
│                                        (`f`/`F`/`f*`/`S`/`s`/`B`/`B*`/`b`/`b*`/`n`)
├── PdfDocument.Color.cs              — Device color operators
│                                        (`g`/`G`/`rg`/`RG`/`k`/`K`/`cs`/`CS`/`sc`/`SC`/`scn`/`SCN`), calibrated
│                                        (CalGray/CalRGB/Lab), ICC-fallback, Indexed and Separation/DeviceN color spaces
├── PdfDocument.ColorKernel.cs        — CIE XYZ/Lab to sRGB conversion kernel and tint-transform conversion
├── PdfDocument.Functions.PostScript.cs — `/FunctionType 4` PostScript calculator parser and bounded executor
├── PdfDocument.Filters.cs            — Generalized `/Filter`/`/DecodeParms` pipeline dispatch, `FlateDecode` plus
│                                        PNG/TIFF predictor reversal
├── PdfDocument.Filters.Lzw.cs        — `LZWDecode` filter (PDF-variant early-change LZW)
├── PdfDocument.Filters.Ascii.cs      — `ASCII85Decode`/`ASCIIHexDecode` filters
├── PdfDocument.Filters.RunLength.cs  — `RunLengthDecode` filter (PackBits-style)
├── PdfDocument.Images.cs             — Image XObjects (`Do`: `DCTDecode` via `Codecs.JpegCodec`, `CCITTFaxDecode` via
│                                        `PdfDocument.CcittFax.cs`, or raw `DeviceGray`/`DeviceRGB`/`DeviceCMYK`
│                                        samples) and `/Subtype /Form` XObject nested content-stream execution
├── PdfDocument.Fonts.cs              — Simple `/Subtype /TrueType` font resolution (`/Resources/Font`), `/Encoding`
│                                        mapping, `/Widths`
├── PdfDocument.Fonts.ToUnicode.cs    — Resolves a font's optional `/ToUnicode` CMap stream into a
│                                        code-to-Unicode-codepoint map
├── PdfDocument.Fonts.Type0.cs        — Composite `/Subtype /Type0` (`Identity-H` CID-keyed) font resolution and
│                                        CID-to-glyph-index mapping
├── PdfDocument.Fonts.Type1.cs        — `/Subtype /Type1` classic and bare Type1C `/FontFile3` font resolution
├── PdfDocument.Fonts.Type3.cs        — `/Subtype /Type3` procedure-font resolution and glyph-procedure painting
├── PdfDocument.FontFallback.cs       — Standard-14/system-font substitution when no embedded font program is present,
│                                        plus a bundled Noto substitute-font union for `Symbol`/`ZapfDingbats` (any
│                                        other symbolic font still fails closed)
├── PdfDocument.Text.cs               — Text-object/text-state operators (`BT`/`ET`/`Tc`/`Tw`/
│                                        `Tz`/`TL`/`Tf`/`Tr`/`Ts`/`Td`/`TD`/`Tm`/`T*`), text-showing
│                                        (`Tj`/`'`/`"`/`TJ`) and glyph painting
├── PdfDocument.CcittFax.cs           — `CCITTFaxDecode` (Group 4/T.6 MMR) image-XObject decoding
├── PdfDocument.Encryption.cs         — `/Encrypt` detection, RC4/AES-128/AES-256 key derivation, stream/string
│                                        decryption
├── PdfDocument.Functions.cs          — Sampled/exponential/stitching `/FunctionType` evaluation for shading patterns
├── PdfDocument.Patterns.cs           — Resolved `/Pattern` resource: dispatches to a shading or tiling pattern
├── PdfDocument.Patterns.Shading.cs   — Axial/radial (`/ShadingType` `2`/`3`) shading-pattern construction and painting
├── PdfDocument.Patterns.Shading.Mesh.cs — Mesh (`/ShadingType` `4`-`7`) decoding, tessellation and rasterization
├── PdfDocument.Patterns.Tiling.cs    — Colored/uncolored tiling-pattern (`/PatternType 1`) cell rendering
├── PdfPageInfo.cs                    — Standalone supporting record struct (resolved page size/rotation)
└── NamespaceDoc.cs                   — Namespace-level XML documentation
```

`PdfDocument`'s dependencies span the `CanvasNet` system's `Canvas` (`Surface`/`Rgba32`),
`Geometry` (`PathBuilder`/`Path`), `Drawing` (`PathFiller`/`PathStroker`/`StrokeStyle`/
`Gradient`/`LinearGradient`/`RadialGradient`/`GradientStop`/`GradientSpread`/`TilePaint`),
`Fonts` (`TrueTypeFont`, `SystemFontCatalog`), and `Codecs` (`UnsupportedImageFeatureException`,
`JpegCodec`) subsystems — see _CanvasNetPdf System Design_ (`canvas-net-pdf.md`) for exactly
which file introduced each dependency.

`VsdxDocument` lives in the `src/DemaConsulting.CanvasNet.Vsdx/` project folder (namespace
`DemaConsulting.CanvasNet.Vsdx`, distributed as the separate `DemaConsulting.CanvasNet.Vsdx`
NuGet package). Like `SvgCodec`'s and `PdfDocument`'s own folders, this is the source folder of
a separate, sibling system's sole unit (see _CanvasNetVsdx System Design_,
`canvas-net-vsdx.md`), a flat structure with no further nesting:

```text
src/DemaConsulting.CanvasNet.Vsdx/
├── VsdxDocument.cs                — Public API entry point: Open/PageCount/GetPageSize/Render/
│                                     Dispose; partial-class implementation continues below
├── VsdxDocument.Package.cs        — OPC package layer: ZIP opening, Content-Types/relationship
│                                     resolution
├── VsdxDocument.Pages.cs          — Page-index parser (visio/pages/pages.xml)
├── VsdxDocument.Shapes.cs         — Page shape-tree parser/resolver
├── VsdxDocument.Masters.cs        — Master/Stencil resolver (masters.xml)
├── VsdxDocument.CellMerge.cs      — Master/MasterShape cell and geometry-row merge algorithm
│                                     (instance-wins, else-Master, including the 1-D/group-child
│                                     transform-cell and arrowhead-cell exceptions)
├── VsdxDocument.Geometry.cs       — Geometry-row resolver (MoveTo/LineTo/ArcTo/EllipticalArcTo
│                                     to path commands, tolerant skip of unrecognized rows)
├── VsdxDocument.Transform.cs      — Shape-local-to-page-space affine transform resolver
├── VsdxDocument.Styles.cs         — Parsed StyleSheet element/index
├── VsdxDocument.TextStyle.cs      — Text StyleSheet-chain resolver
├── VsdxDocument.Paint.cs          — Line/fill paint resolver (color, transparency, stroke-width
│                                     floor, non-printing/hide-text suppression)
├── VsdxDocument.Text.cs           — `<Text>` element parser
├── VsdxDocument.TextBox.cs        — Text-box transform resolver
├── VsdxDocument.TextLayout.cs     — Text layout engine (word-wrap/alignment)
├── VsdxDocument.TextRender.cs     — Text-painting primitive
├── VsdxDocument.Connects.cs       — `<Connects>` section parser
├── VsdxDocument.Arrowheads.cs     — Arrowhead style-index and geometry resolver
├── VsdxDocument.Groups.cs         — Recursive group/nested-shape resolver
├── VsdxDocument.Theme.cs          — Theme loader (theme1.xml)
├── VsdxDocument.Render.cs         — Public, page-level rendering API
├── VsdxCell.cs                    — Single parsed VisioML `<Cell>` element
├── VsdxCellBag.cs                 — Immutable, name-keyed lookup of `VsdxCell` entries
├── VsdxGeometry.cs                — Single, as-parsed (pre-merge) `<Row>` element
├── VsdxShapeNode.cs               — Single parsed VisioML `<Shape>` element
├── VsdxShapeTransform.cs          — Resolved 2-D affine transform for a shape
├── VsdxTextBoxTransform.cs        — Resolved text-box placement
├── VsdxTextRun.cs                 — As-parsed, marker-delimited text segment
├── VsdxTextLayout.cs              — Fully-resolved, laid-out glyph stream for a shape's text
├── VsdxResolvedPaint.cs           — Resolved stroke/fill paint after cell-merge and StyleSheet
│                                     chain resolution
├── VsdxColorPalette.cs            — Resolves a color cell's raw value to an `Rgba32`
├── VsdxConnect.cs                 — Identifies which end of a connector a `<Connect>` describes
├── VsdxConnectorEndpoints.cs      — Resolved, page-space begin/end connector endpoints
├── VsdxArrowhead.cs               — Recognized `BeginArrow`/`EndArrow` index subset
├── VsdxArrowheadGeometry.cs       — Local-space `Path` geometry for a resolved `VsdxArrowhead`
├── VsdxTheme.cs                   — Parsed document theme (color scheme)
├── VsdxPageInfo.cs                — Reports a page's declared name/size (EMU)
├── VsdxRenderOptions.cs           — Page-rendering configuration (for example background color)
├── VsdxUnsupportedFeatureException.cs — Thrown when a resolver refuses an unsupported-but-
│                                     recognized construct
└── NamespaceDoc.cs                — Namespace-level XML documentation
```

`VsdxDocument`'s dependencies span the `CanvasNet` system's `Canvas` (`Surface`/`Rgba32`),
`Geometry` (`PathBuilder`/`Path`), `Drawing` (`PathFiller`/`PathStroker`/`StrokeStyle`), and
`Fonts` (`TrueTypeFont`, `SystemFontCatalog`) subsystems — see _CanvasNetVsdx System Design_
(`canvas-net-vsdx.md`) for exactly which file introduced each dependency.

`PptxDocument` lives in the `src/DemaConsulting.CanvasNet.Pptx/` project folder (namespace
`DemaConsulting.CanvasNet.Pptx`, distributed as the separate `DemaConsulting.CanvasNet.Pptx`
NuGet package). Like `SvgCodec`'s, `PdfDocument`'s, and `VsdxDocument`'s own folders, this is the
source folder of a separate, sibling system's sole unit (see _CanvasNetPptx System Design_,
`canvas-net-pptx.md`), a flat structure with no further nesting:

```text
src/DemaConsulting.CanvasNet.Pptx/
├── PptxDocument.cs                    — Public API entry point: Open/SlideCount/SlideSize/
│                                         GetSlideSizeInPixels/Render/Dispose; partial-class
│                                         implementation continues below
├── PptxDocument.Package.cs            — OOXML package layer: ZIP opening, Content-Types and
│                                         relationship resolution
├── PptxDocument.Presentation.cs       — Presentation-root parser (slide size, ordered slide list)
├── PptxDocument.Slides.cs             — Slide parser
├── PptxDocument.Layouts.cs            — Slide layout parser
├── PptxDocument.Masters.cs            — Slide master parser (including text style buckets)
├── PptxDocument.Theme.cs              — Theme parser (color scheme and font scheme)
├── PptxDocument.Inheritance.cs        — Placeholder property-inheritance resolver
├── PptxDocument.Background.cs         — Slide/layout/master background-fill resolver
├── PptxDocument.Geometry.cs           — Shape transform, group-transform composition, preset and
│                                         custom geometry resolvers
├── PptxDocument.Paint.cs              — Fill, color (with color-transform pipeline), and line-style
│                                         resolvers
├── PptxDocument.Text.cs               — Text-body/paragraph/run parser
├── PptxDocument.TextInheritance.cs    — Run/paragraph property-inheritance resolver
├── PptxDocument.Bullets.cs            — Auto-numbered bullet string formatter
├── PptxDocument.TextLayout.cs         — Text layout engine (word-wrap, alignment, anchor, autofit)
├── PptxDocument.TextRender.cs         — Text-painting primitive and font resolution
├── PptxDocument.ShapeTree.cs          — Recursive shape-tree walker
├── PptxDocument.Connectors.cs         — Connector-shape and arrowhead resolvers
├── PptxDocument.Images.cs             — Picture-shape resolvers and painting primitive
├── PptxDocument.Tables.cs             — Table resolvers and painting primitive
├── PptxDocument.TableStyles.cs        — Table-style and banding resolver
├── PptxDocument.Charts.cs             — Chart graphic-frame resolver (integration with
│                                         `CanvasNetCharts`)
├── PptxDocument.Render.cs             — Public, slide-level rendering API
├── PptxArrowheadGeometry.cs           — Local-space `Path` geometry for a resolved arrowhead
├── PptxArrowheadStyle.cs              — Recognized arrowhead type enumeration
├── PptxEffectiveTextProperties.cs     — Fully-resolved effective run character properties
├── PptxLineStyle.cs                   — Resolved line style (width, paint, dash pattern)
├── PptxPaint.cs                       — Resolved fill or line paint
├── PptxPatternTileRenderer.cs         — Synthesizes a one-cell tile surface for a preset pattern fill
├── PptxPicture.cs                     — Resolved picture source-rectangle crop
├── PptxPlaceholder.cs                 — Single parsed placeholder shape
├── PptxPlaceholderProperties.cs       — Effective, resolved placeholder property fragments
├── PptxPresetGeometry.cs              — Core `Path` geometry for supported DrawingML preset shapes
├── PptxRenderOptions.cs               — Slide-rendering configuration (for example background color)
├── PptxShapeFrame.cs                  — Resolved placement transform of a single shape
├── PptxShapeTree.cs                   — Node hierarchy of a slide's parsed shape tree
├── PptxSlideSize.cs                   — Reports a presentation's slide size (EMU)
├── PptxTable.cs                       — Parsed table (column widths and rows)
├── PptxTextBody.cs                    — Parsed text body (body properties and paragraphs)
├── PptxTextLayout.cs                  — Fully-resolved, laid-out glyph stream for a text body
├── PptxTheme.cs                       — Parsed theme (color scheme and font scheme)
├── PptxUnsupportedFeatureException.cs — Thrown when a resolver refuses an unsupported-but-
│                                         recognized construct
└── NamespaceDoc.cs                    — Namespace-level XML documentation
```

`PptxDocument`'s dependencies span the `CanvasNet` system's `Canvas` (`Surface`/`Rgba32`),
`Geometry` (`PathBuilder`/`Path`), `Drawing` (`PathFiller`/`PathStroker`/`StrokeStyle`/
`LinearGradient`), `Fonts` (`TrueTypeFont`, `SystemFontCatalog`), and `Codecs` subsystems, plus the
sibling `CanvasNetSvg` system (SVG pictures) and the sibling `CanvasNetCharts` system
(`OpenXmlChartParser`, `ChartRenderer`) — see _CanvasNetPptx System Design_ (`canvas-net-pptx.md`)
for exactly which phase introduced each dependency.

`CanvasNetCharts` lives in the `src/DemaConsulting.CanvasNet.Charts/` project folder (distributed
as the separate `DemaConsulting.CanvasNet.Charts` NuGet package). Its two subsystems occupy
different folders: `ChartModel` occupies the project's root folder (flat — no further nesting),
and `OpenXmlChart` occupies the `OpenXml/` sub-folder, whose name equals its sub-namespace
`DemaConsulting.CanvasNet.Charts.OpenXml` (see _CanvasNetCharts System Design_,
`canvas-net-charts.md`):

```text
src/DemaConsulting.CanvasNet.Charts/
├── DemaConsulting.CanvasNet.Charts.csproj — Package definition (net8.0/net9.0/net10.0)
├── Chart.cs                       — ChartModel: immutable, validated chart description
├── ChartSeries.cs                 — ChartModel: immutable named series of numeric values
├── ChartAxis.cs                   — ChartModel: immutable category/value axis description
├── ChartLegend.cs                 — ChartModel: legend description and `ChartLegendPosition` enum
├── ChartTitle.cs                  — ChartModel: title text and optional font-size hint
├── ChartType.cs                   — ChartModel: chart-kind enumeration
├── ChartBuilder.cs                — ChartModel: fluent chart construction API
├── ChartColorPalette.cs           — ChartModel: built-in default categorical color palette
├── ChartRenderOptions.cs          — ChartModel: rendering configuration
├── ChartRenderer.cs               — ChartModel: public rendering entry point; partial-class
│                                     implementation continues below
├── ChartRenderer.Layout.cs        — ChartModel: title/legend/plot-area layout
├── ChartRenderer.Title.cs         — ChartModel: title painting
├── ChartRenderer.Legend.cs        — ChartModel: legend painting
├── ChartRenderer.CategoryChart.cs — ChartModel: Bar/Column/Line/Area painting
├── ChartRenderer.PieChart.cs      — ChartModel: Pie/Doughnut painting
├── NamespaceDoc.cs                — Namespace-level XML documentation
└── OpenXml/                       — OpenXmlChart subsystem
    ├── NamespaceDoc.cs            — Namespace-level XML documentation
    ├── OpenXmlChartParser.cs      — OpenXmlChartParser: `c:chartSpace`/`c:chart` to `Chart`
    └── ChartUnsupportedFeatureException.cs — OpenXmlChartParser: recognized-but-unimplemented
                                              chart kind or data shape
```

`CanvasNetCharts` depends on the `CanvasNet` system's `Canvas`, `Geometry`, `Drawing`, `Fonts`, and
`Rendering` subsystems; `CanvasNetPptx` consumes both of its subsystems and never the reverse.

The `CanvasNetRender3D` system lives in the `src/DemaConsulting.CanvasNet.Render3D/` project folder
(distributed as the separate `DemaConsulting.CanvasNet.Render3D` NuGet package). Its three
subsystems each occupy one sub-folder whose name equals the sub-namespace; no unit has a
nested sub-folder of its own (see _CanvasNetRender3D System Design_, `canvas-net-render3d.md`):

```text
src/DemaConsulting.CanvasNet.Render3D/
├── DemaConsulting.CanvasNet.Render3D.csproj — Package definition (net8.0/net9.0/net10.0)
├── SceneModel/                    — SceneModel subsystem
│   ├── NamespaceDoc.cs            — Namespace-level XML documentation
│   ├── Vector2D.cs                — DoublePrecisionMath: 2D double-precision vector
│   ├── Vector3D.cs                — DoublePrecisionMath: 3D double-precision vector
│   ├── Matrix4x4D.cs              — DoublePrecisionMath: 4x4 double-precision matrix (row-vector convention)
│   ├── BoundingBox3D.cs           — DoublePrecisionMath: axis-aligned bounding box
│   ├── Mesh.cs                    — Mesh: immutable indexed triangle mesh
│   ├── MeshBuilder.cs             — Mesh: vertex-welding mesh construction helper
│   ├── MeshTopology.cs            — Mesh: internal closedness, defect classification, volume and area computations
│   ├── MeshTopologyDefects.cs     — Mesh: internal topology-defect flags
│   ├── SceneNode.cs               — SceneGraph: abstract node (transform, appearance)
│   ├── MeshNode.cs                — SceneGraph: leaf node referencing a Mesh
│   ├── GroupNode.cs               — SceneGraph: node grouping child nodes (union)
│   ├── CsgNode.cs                 — SceneGraph: node combining child nodes with a boolean operation
│   ├── CsgOperation.cs            — SceneGraph: union/difference/intersection enumeration
│   ├── NodeAppearance.cs          — SceneGraph: per-node surface color
│   ├── Scene.cs                   — SceneGraph: scene root, bounds, instance enumeration
│   └── SceneInstance.cs           — SceneGraph: internal flattened mesh instance
├── Tessellation/                  — Tessellation subsystem
│   ├── NamespaceDoc.cs            — Namespace-level XML documentation
│   ├── PrimitiveTessellator.cs    — PrimitiveTessellator: box/sphere/cylinder/cone/polyhedron meshes
│   ├── PrimitiveTessellator.Shapes2D.cs — PrimitiveTessellator: rectangle/ellipse/circle/polygon regions
│   ├── PolygonRegion.cs           — PolygonTriangulator: validated even-odd 2D polygon region
│   ├── ContourInfo.cs             — PolygonTriangulator: internal contour table entry
│   ├── PolygonPredicates.cs       — PolygonTriangulator: internal exact orientation predicates
│   ├── PolygonTriangulator.cs     — PolygonTriangulator: hole bridging and ear-clipping triangulation
│   ├── Extruder.cs                — Extruder: shared helpers and uniform-scale overload
│   ├── Extruder.Linear.cs         — Extruder: linear extrusion with twist, scale, and apex
│   ├── Extruder.Rotate.cs         — Extruder: rotational extrusion about the Z axis
│   ├── ConvexHull.cs              — ConvexHull: Hull3D and Hull2D entry points
│   ├── HullBuilder3D.cs           — ConvexHull: internal incremental 3D hull engine
│   ├── SolidPredicates.cs         — ConvexHull: internal exact 3D orientation predicate
│   └── MinkowskiSum.cs            — MinkowskiSum: ConvexSum with the convexity test
└── SceneRendering/                — SceneRendering subsystem
    ├── NamespaceDoc.cs            — Namespace-level XML documentation
    ├── Camera.cs                  — Camera: perspective/orthographic camera, LookAt, FitToBounds
    ├── CameraProjection.cs        — Camera: projection enumeration
    ├── ViewTransform.cs           — Camera: internal camera-relative view transform
    ├── Light.cs                   — Lighting: abstract light and default rig
    ├── AmbientLight.cs            — Lighting: ambient light
    ├── DirectionalLight.cs        — Lighting: directional light
    ├── LightingModel.cs           — Lighting: internal flat Lambert/Blinn-Phong shading
    ├── TriangleRasterizer.cs      — TriangleRasterizer: setup and fixed-point rasterization
    ├── TriangleClipper.cs         — TriangleRasterizer: homogeneous polygon clipping
    ├── ClipVertex.cs              — TriangleRasterizer: internal clip-space vertex
    ├── ClipPlane.cs               — TriangleRasterizer: internal clip plane
    ├── SampleProjection.cs        — TriangleRasterizer: internal view-to-sample projection, guard band
    ├── ViewTriangle.cs            — TriangleRasterizer: internal shaded view-space triangle
    ├── ScreenTriangle.cs          — TriangleRasterizer: internal fixed-point screen triangle
    ├── TileRect.cs                — TriangleRasterizer: internal sample rectangle
    ├── IFragmentVisitor.cs        — TriangleRasterizer: internal fragment callback contract
    ├── DepthTestVisitor.cs        — TriangleRasterizer: internal z-buffer visitor with tie rule
    ├── FragmentBuffer.cs          — FragmentBuffer: internal per-tile fragment store with budget
    ├── CsgProgram.cs              — CsgResolver: internal postfix CSG program and evaluator
    ├── CsgCompiler.cs             — CsgResolver: internal scene tree to program compiler
    ├── CsgResolver.cs             — CsgResolver: internal per-sample visible-surface resolver
    ├── SceneRenderer.cs           — SceneRenderer: Render/RenderFitted entry points
    ├── SceneRenderOptions.cs      — SceneRenderer: immutable, validated render options
    ├── CsgOperandValidator.cs     — SceneRenderer: internal closed-operand validation and reporting
    ├── RenderBudgetExceededException.cs — SceneRenderer: per-tile fragment budget exception
    ├── SceneRenderUnsupportedFeature.cs — SceneRenderer: unsupported-feature report record
    ├── SceneRenderUnsupportedFeatureException.cs — SceneRenderer: strict-mode unsupported-feature exception
    ├── TileBinner.cs              — SceneRenderer: internal triangle-to-tile binning
    ├── SampleResolver.cs          — SceneRenderer: internal antialiasing resolve
    └── TileWorkspace.cs           — SceneRenderer: internal per-worker sample buffers
```

`CanvasNetRender3D`'s only dependency on the rest of this repository is the `CanvasNet` system's
`Canvas` subsystem (`Surface`, `Rgba32`) — see _CanvasNetRender3D System Design_
(`canvas-net-render3d.md`).

The `CanvasNetScad` system lives in the `src/DemaConsulting.CanvasNet.Scad/` project folder (the separate
`DemaConsulting.CanvasNet.Scad` package). The `ScadDocument` unit
occupies the project root; the internal `ScadLanguage`, `ScadEvaluation`, and `ScadGeometry` subsystems occupy the
`Language/`, `Evaluation/`, and `Geometry/` sub-folders, whose names equal their sub-namespaces; no unit has a
nested sub-folder of its own (see _CanvasNetScad System Design_, `canvas-net-scad.md`):

```text
src/DemaConsulting.CanvasNet.Scad/
├── DemaConsulting.CanvasNet.Scad.csproj — Package definition (net8.0/net9.0/net10.0; packable)
├── NamespaceDoc.cs                — Namespace-level XML documentation (incl. clean-room statement)
├── ScadDocument.cs                — ScadDocument: Parse/Open entry points and file-scope facts
├── ScadDocument.Evaluate.cs       — ScadDocument: Evaluate (headless evaluation, handler delivery)
├── ScadDocument.Render.cs         — ScadDocument: BuildScene and Render (geometry, rendering, delivery)
├── ScadRenderOptions.cs           — ScadDocument: immutable, validated render options
├── ScadViewport.cs                — ScadDocument: viewport plan, camera mapping, and camera choice
├── ScadResolvedView.cs            — ScadDocument: the view a render used (public record)
├── ScadViewSource.cs              — ScadDocument: where the view came from (public enumeration)
├── ScadFontResolutionOptions.cs   — ScadDocument: immutable font resolution options of the text module
├── ScadOpenOptions.cs             — ScadDocument: immutable, validated open options
├── ScadSourceLocation.cs          — ScadDocument: file, line, and column value
├── ScadSyntaxException.cs         — ScadDocument: syntax error exception with location
├── ScadDirective.cs               — ScadDocument: recorded include/use directive
├── ScadDirectiveKind.cs           — ScadDocument: include/use enumeration
├── ScadEvaluationOptions.cs       — ScadDocument: immutable, validated evaluation options
├── ScadEvaluationResult.cs        — ScadDocument: echo, diagnostics, unsupported features, instance count
├── ScadLimits.cs                  — ScadDocument: resource limits
├── ScadDiagnostic.cs              — ScadDocument: warning record
├── ScadUnsupportedFeature.cs      — ScadDocument: unsupported-feature report record
├── ScadUnsupportedFeatureException.cs — ScadDocument: strict-mode unsupported-feature exception
├── ScadEvaluationException.cs     — ScadDocument: evaluation error exception with location
├── Language/                      — ScadLanguage subsystem (internal)
│   ├── NamespaceDoc.cs            — Namespace-level XML documentation
│   ├── ScadTokenKind.cs           — ScadLexer: token kind enumeration
│   ├── ScadToken.cs               — ScadLexer: token record struct
│   ├── ScadLexer.cs               — ScadLexer: tokenizer, operators, identifiers, comments
│   ├── ScadLexer.Literals.cs      — ScadLexer: number, string, and directive literals
│   ├── ScadSyntax.cs              — ScadParser: syntax-tree base types, parts, and enumerations
│   ├── ScadStatements.cs          — ScadParser: statement records
│   ├── ScadExpressions.cs         — ScadParser: expression records
│   ├── ScadParser.cs              — ScadParser: entry points, token cursor, errors, limits
│   ├── ScadParser.Statements.cs   — ScadParser: statement productions
│   ├── ScadParser.Expressions.cs  — ScadParser: expression productions
│   └── ScadTreeDepth.cs           — ScadParser: iterative syntax-tree height verification
├── Geometry/                      — ScadGeometry subsystem (internal)
│   ├── NamespaceDoc.cs            — ScadGeometry: namespace-level XML documentation
│   ├── ScadGeometryBuilder.cs     — ScadGeometryBuilder: Build entry point and scene root
│   ├── ScadGeometryBuilder.Walk.cs — ScadGeometryBuilder: iterative post-order engine
│   ├── ScadGeometryBuilder.Modifiers.cs — ScadGeometryBuilder: show-only pre-pass
│   ├── ScadGeometryBuilder.Unsupported.cs — ScadGeometryBuilder: dispatch and unsupported-module tables
│   ├── ScadGeo.cs                 — ScadGeometryBuilder: result of building one subtree, with bounds
│   ├── ScadGeometryContext.cs     — ScadGeometryBuilder: per-build services, settings, built scene
│   ├── ScadSceneMap.cs            — ScadGeometryBuilder: mesh node to source location map
│   ├── ScadReporter.cs            — ScadGeometryBuilder: single reporting channel and strict mode
│   ├── ScadSceneOptions.cs        — ScadGeometryBuilder: renderer options copy and report mapping
│   ├── ScadFeatureIds.cs          — ScadGeometryBuilder: stable unsupported-feature identifiers
│   ├── ScadBooleans.cs            — ScadGeometryBuilder: union, difference, and intersection combiners
│   ├── ScadHulls.cs               — ScadGeometryBuilder: hull and minkowski combiners
│   ├── ScadProjections.cs         — ScadGeometryBuilder: projection combiner, arguments, and budget
│   ├── ScadProjections.Tree.cs    — ScadGeometryBuilder: projection solid tree and shared state
│   ├── ScadProjections.Faces.cs   — ScadGeometryBuilder: projection face groups and footprints
│   ├── ScadProjections.Slice.cs   — ScadGeometryBuilder: projection cross-section of a mesh
│   ├── ScadProjections.Classify.cs — ScadGeometryBuilder: projection silhouette, classification, and cut
│   ├── ScadArguments.cs           — ScadPrimitives: argument binder and typed readers
│   ├── ScadFragmentRule.cs        — ScadPrimitives: fragment count from $fn, $fa, and $fs
│   ├── ScadPrimitives.cs          — ScadPrimitives: cube, sphere, and cylinder
│   ├── ScadPrimitives.Shapes2D.cs — ScadPrimitives: square, circle, and polygon
│   ├── ScadPrimitives.Polyhedron.cs — ScadPrimitives: polyhedron and face triangulation
│   ├── ScadTransforms.cs          — ScadTransforms: translate, rotate, scale, mirror, and multmatrix
│   ├── ScadTransforms.Resize.cs   — ScadTransforms: resize
│   ├── ScadColors.cs              — ScadTransforms: color
│   ├── ScadShape2D.cs             — ScadExtrusions: symbolic 2D shape tree
│   ├── ScadExtrusions.cs          — ScadExtrusions: distribution over the 2D tree and slabs
│   ├── ScadExtrusions.Linear.cs   — ScadExtrusions: linear_extrude
│   ├── ScadExtrusions.Rotate.cs   — ScadExtrusions: rotate_extrude
│   ├── ScadTextGeometry.cs        — ScadTextGeometry: text module, layout, alignment, and regions
│   ├── ScadTextGeometry.Flattening.cs — ScadTextGeometry: reference height and outline flattening
│   └── ScadFontResolver.cs        — ScadTextGeometry: font argument parsing and font resolution
└── Evaluation/                    — ScadEvaluation subsystem (internal)
    ├── NamespaceDoc.cs            — Namespace-level XML documentation
    ├── ScadValueKind.cs           — ScadValue: value kind enumeration
    ├── ScadValue.cs               — ScadValue: value struct, factories, truthiness, equality
    ├── ScadValue.Compare.cs       — ScadValue: ordering
    ├── ScadValue.Arithmetic.cs    — ScadValue: operators, indexing, members
    ├── ScadValue.Matrix.cs        — ScadValue: dot, matrix, and vector products
    ├── ScadValue.Format.cs        — ScadValue: string and echo text forms
    ├── ScadValue.Convert.cs       — ScadValue: conversion from caller-supplied objects
    ├── ScadVector.cs              — ScadValue: immutable vector payload
    ├── ScadRange.cs               — ScadValue: lazy range payload
    ├── IScadFunctionValue.cs      — ScadValue: function-value interface
    ├── ScadNumberFormatter.cs     — ScadValue: deterministic number format
    ├── ScadNumbers.cs             — ScadValue: IEEE equality helper
    ├── ScadText.cs                — ScadValue: Unicode-scalar text helpers
    ├── ScadEvaluator.cs           — ScadEvaluator: entry points, restart, accounting
    ├── ScadEvaluator.Scopes.cs    — ScadEvaluator: scopes, lazy cells, lookup
    ├── ScadEvaluator.Expressions.cs — ScadEvaluator: expressions and operators
    ├── ScadEvaluator.Calls.cs     — ScadEvaluator: argument binding, calls, tail calls
    ├── ScadEvaluator.Statements.cs — ScadEvaluator: statements, echo, assert
    ├── ScadEvaluator.Modules.cs   — ScadEvaluator: modules and children
    ├── ScadEvaluator.Comprehensions.cs — ScadEvaluator: comprehensions and iteration
    ├── ScadEvaluator.Host.cs      — ScadEvaluator: built-in function host
    ├── ScadEvaluatorElementCounter.cs — ScadEvaluator: capped, memoized count of nested vector elements
    ├── ScadEvaluatorFallbackSlots.cs — ScadEvaluator: bounded slots for fallback threads, with counters
    ├── ScadScope.cs               — ScadEvaluator: scopes, cells, special frames, closures
    ├── ScadInstance.cs            — ScadEvaluator: instantiation tree nodes
    ├── ScadBuiltinModules.cs      — ScadEvaluator: recognized built-in module names
    ├── ScadEvaluationSettings.cs  — ScadEvaluator: evaluation settings
    ├── ScadEvaluationOutput.cs    — ScadEvaluator: evaluation result
    ├── ScadViewportValues.cs      — ScadEvaluator: viewport variable values and the document's viewport
    ├── ScadEvaluationSink.cs      — ScadEvaluator: buffered echo, warnings, unsupported features
    ├── IScadBuiltinHost.cs        — ScadBuiltinFunctions: evaluator services for functions
    ├── ScadBuiltinFunctions.cs    — ScadBuiltinFunctions: registry and argument binding
    ├── ScadBuiltinFunctions.Math.cs    — ScadBuiltinFunctions: mathematics and trigonometry
    ├── ScadBuiltinFunctions.Lists.cs   — ScadBuiltinFunctions: list functions and lookup
    ├── ScadBuiltinFunctions.Search.cs  — ScadBuiltinFunctions: search
    ├── ScadBuiltinFunctions.Strings.cs — ScadBuiltinFunctions: string functions
    ├── ScadBuiltinFunctions.Types.cs   — ScadBuiltinFunctions: type tests and version
    ├── ScadBuiltinFunctions.Random.cs  — ScadBuiltinFunctions: rands
    ├── ScadRandom.cs              — ScadBuiltinFunctions: SplitMix64 generator
    ├── ScadSourceUnit.cs          — ScadFileResolver: include-expanded source unit
    ├── ScadFileResolver.cs        — ScadFileResolver: expansion, libraries, limits
    └── ScadFileResolver.Paths.cs  — ScadFileResolver: path normalization and sandbox
```

`CanvasNetScad` uses the `CanvasNet` system (`Surface`, `Rgba32`, and `CssColor` of `Canvas`; for `text`,
`TrueTypeFont` and
`SystemFontCatalog` of `Fonts` and `Path` of `Geometry`) and `CanvasNetRender3D` (scene model,
tessellation, rendering) through their public API from the `ScadGeometry` subsystem (the `ScadTextGeometry` unit
for `Fonts` and `Geometry`) and the `BuildScene` and
`Render` methods of `ScadDocument`, and declares both packages as its dependencies — see
_CanvasNetScad System Design_ (`canvas-net-scad.md`).

The `CanvasNetVegaLite` system lives in the `src/DemaConsulting.CanvasNet.VegaLite/` project folder (the separate
`DemaConsulting.CanvasNet.VegaLite` package). The `VegaLiteDocument` unit occupies the project root; the internal
`SpecModel`, `Data`, `Expressions`, `Transforms`, `Scales`, `Guides`, `Layout`, `Marks`, and `Scenegraph` subsystems
occupy the sub-folders of the same
names, whose names equal their sub-namespaces; no unit has a nested sub-folder of its own (see _CanvasNetVegaLite System
Design_, `canvas-net-vega-lite.md`):

```text
src/DemaConsulting.CanvasNet.VegaLite/
├── DemaConsulting.CanvasNet.VegaLite.csproj — Package definition (net8.0/net9.0/net10.0; packable)
├── NamespaceDoc.cs                — VegaLiteDocument: namespace-level XML documentation
├── VegaLiteDocument.cs            — VegaLiteDocument: Parse/Open entry points and descriptive properties
├── VegaLiteDocument.Render.cs     — VegaLiteDocument: Measure, Render, and the pipeline
├── VegaLiteDocument.Layers.cs     — VegaLiteDocument: the layers of a view (data walk, axes, legends, and marks per layer)
├── VegaLiteDocument.Composition.cs — VegaLiteDocument: the cell and the composition (concat, repeat)
├── VegaLiteOpenOptions.cs         — VegaLiteDocument: immutable, validated open options
├── VegaLiteRenderOptions.cs       — VegaLiteDocument: immutable, validated render options
├── VegaLiteFontOptions.cs         — VegaLiteDocument: font resolution options
├── VegaLiteLimits.cs              — VegaLiteDocument: resource limits
├── VegaLiteLocation.cs            — VegaLiteDocument: file, JSON pointer, line, and column value
├── VegaLiteSize.cs                — VegaLiteDocument: size in pixels
├── VegaLiteDataReference.cs       — VegaLiteDocument: data source as written
├── VegaLiteDataKind.cs            — VegaLiteDocument: data source kind enumeration
├── VegaLiteDiagnostic.cs          — VegaLiteDocument: warning record
├── VegaLiteUnsupportedFeature.cs  — VegaLiteDocument: unsupported-feature report record
├── VegaLiteSpecException.cs       — VegaLiteDocument: specification error exception with location
├── VegaLiteRenderException.cs     — VegaLiteDocument: render error exception with location
├── VegaLiteUnsupportedFeatureException.cs — VegaLiteDocument: strict-mode unsupported-feature exception
├── VegaLiteReportSink.cs          — VegaLiteDocument: buffered, de-duplicated, capped report delivery
├── third-party/                   — Attribution: README.md provenance record and the LICENSE-*.txt notices
├── SpecModel/                     — SpecModel subsystem (internal)
│   ├── NamespaceDoc.cs            — SpecModel: namespace-level XML documentation
│   ├── SpecParser.cs              — SpecParser: entry points, single-pass reader, limits, duplicate keys
│   ├── SpecParser.Validation.cs   — SpecParser: structural validation and unknown properties
│   ├── SpecParser.Transforms.cs   — SpecParser: expression compilation, parameter, transform, and predicate validation
│   ├── SpecParser.Aggregates.cs   — SpecParser: aggregating transform and aggregate, bin, stack, and sort validation
│   ├── SpecParser.Time.cs         — SpecParser: time unit, date-time object, tick interval, and timeUnit transform validation
│   ├── SpecParser.Layers.cs       — SpecParser: resolve, composite mark properties, and composition validation
│   ├── SpecParser.LabelExpressions.cs — SpecParser: the compilation of the labelExpr of axes, legends, and headers
│   ├── SpecExpressions.cs         — SpecParser: the compiled expressions and the static references of a specification
│   ├── SpecNode.cs                — SpecParser: SpecNode, SpecNodeKind, NodePosition, and SpecTree
│   ├── SelectionDeclarations.cs   — SpecParser: the reading of selection parameters
│   ├── SelectionDeclarations.Assignment.cs — SpecParser: the assignment of selections to unit views
│   ├── SpecMetadata.cs            — SpecParser: descriptive metadata of a specification
│   ├── ChannelDefinitions.cs      — SpecParser: which object of a channel definition carries the field, and its conditions
│   ├── PredicateSource.cs         — SpecParser: a predicate of a condition written as expression source text
│   ├── FeatureCatalog.cs          — FeatureCatalog: stable vl- reports of unsupported constructs
│   ├── ConfigResolver.cs          — ConfigResolver: defaults, configuration merge, precedence chains
│   ├── SpecNormalizer.cs          — SpecNormalizer: view tree, inheritance, resolve, limits
│   ├── SpecNormalizer.Composition.cs — SpecNormalizer: concatenations, facets, faceted unit views, and layout settings
│   ├── SpecNormalizer.Repeat.cs   — SpecNormalizer: repeat expansion and repeat references
│   ├── SpecNormalizer.Composite.cs — SpecNormalizer: Expand and the composite-mark expansion into layers
│   ├── SpecNormalizer.BoxPlot.cs  — SpecNormalizer: the boxplot expansion
│   ├── SpecNormalizer.ErrorBar.cs — SpecNormalizer: the errorbar and errorband expansion
│   ├── SpecNormalizer.PathOverlay.cs — SpecNormalizer: the point overlay decision of the path marks
│   ├── ExpressionReferenceFolder.cs — ExpressionReferenceFolder: the static references and the tree rewrite
│   └── ExpressionReferenceSites.cs — ExpressionReferenceFolder: the table of sites and the walk that finds them
├── Data/                          — Data subsystem (internal)
│   ├── NamespaceDoc.cs            — Data: namespace-level XML documentation
│   ├── DataTable.cs               — DataTable: DataValue, JsNumber, FieldPath, and DataTable
│   ├── DataObject.cs              — DataTable: DataObject and DataArray, the payloads of composite values
│   ├── DataLoader.cs              — DataLoader: inline rows, files, named datasets, sequences, and the row budget
│   ├── DataFileResolver.cs        — DataFileResolver: sandboxed bounded reads, cache, strict UTF-8, and data-file limits
│   ├── DataFileResolver.Paths.cs  — DataFileResolver: path rules, candidates, root confinement, and the link walk
│   ├── DelimitedTextParser.cs     — DelimitedTextParser: CSV, TSV, and DSV, parse directives, and type inference
│   ├── TimeCalendar.cs            — TimeCalendar: DateParts, instants and wall-clock parts, and the integer civil arithmetic
│   ├── TimeCalendar.Zone.cs       — TimeCalendar: the zone offset lookup and the gap and overlap rule
│   ├── TimeCalendar.Intervals.cs  — TimeCalendar: the d3-time intervals, TimeIntervalKind, and TimeRangeLimitException
│   ├── TimeUnits.cs               — TimeCalendar: TimeUnitSpec, the Vega-Lite time units
│   ├── TimeDateObject.cs          — TimeCalendar: DateTimeObject, the Vega-Lite date-time object
│   ├── TimeFormat.cs              — TimeFormat: the English locale tables and shared helpers
│   ├── TimeFormat.Format.cs       — TimeFormat: TimeFormatter, the specifier compiler, and the length limit
│   ├── TimeFormat.Parse.cs        — TimeFormat: the d3 time parser
│   ├── TimeFormat.Multi.cs        — TimeFormat: Vega's multi-format
│   ├── JsDateParser.cs            — TimeFormat: JavaScript Date.parse, the ECMAScript date-time string format
│   └── JsDateParser.Legacy.cs     — TimeFormat: the documented subset of the legacy Date.parse forms
├── Expressions/                   — Expressions subsystem (internal)
│   ├── NamespaceDoc.cs            — ExpressionParser: namespace-level XML documentation
│   ├── ExpressionParser.cs        — ExpressionParser: entry point, length and depth limits, precedence climbing
│   ├── ExpressionParser.Primary.cs — ExpressionParser: literals, access, calls, and collected constructs
│   ├── ExpressionParser.Tokenizer.cs — ExpressionParser: tokens and the tokenizer
│   ├── CompiledExpression.cs      — ExpressionParser: the immutable postfix program and its tables
│   ├── ExpressionConstruct.cs     — ExpressionParser: unsupported constructs, parameter references, runtime exception
│   ├── ExpressionEvaluator.cs     — ExpressionEvaluator: the interpreter loop and IExpressionDatum
│   ├── ExpressionEvaluator.Coercion.cs — ExpressionEvaluator: JavaScript coercion, equality, and relational helpers
│   ├── ExpressionEvaluator.Datum.cs — ExpressionEvaluator: reading the datum from a row, a value, or a caller object
│   ├── ExpressionFunctions.cs     — ExpressionFunctions: the function table and the math functions
│   ├── ExpressionFunctions.Text.cs — ExpressionFunctions: string and array functions
│   ├── ExpressionFunctions.Types.cs — ExpressionFunctions: type and conversion functions
│   ├── ExpressionFunctions.Regex.cs — ExpressionFunctions: regexp, test, and the regular expression forms
│   ├── ExpressionFunctions.Casing.cs — ExpressionFunctions: the case conversion of lower and upper
│   ├── ExpressionFunctions.Distributions.cs — ExpressionFunctions: the density, cumulative, and quantile functions
│   ├── ExpressionFunctions.Time.cs — ExpressionFunctions: the date and time functions
│   ├── ExpressionFunctions.Selection.cs — ExpressionFunctions: data and the selection functions
│   ├── SelectionStore.cs          — ExpressionFunctions: the tuples and stores of selections
│   ├── SelectionTestSemantics.cs  — ExpressionFunctions: the test and resolution of a selection
│   ├── ExpressionFunctionContext.cs — ExpressionFunctions: step budget, string limit, and regex cache
│   └── ExpressionRegex.cs         — ExpressionFunctions: JavaScript regular expressions as safe .NET ones
├── Transforms/                    — Transforms subsystem (internal)
│   ├── NamespaceDoc.cs            — RowTransforms: namespace-level XML documentation
│   ├── RowTransforms.cs           — RowTransforms: Apply, calculate, and the row selection of filter
│   ├── RowTransforms.Predicates.cs — RowTransforms: the predicates of filter
│   ├── RowTransforms.Shape.cs     — RowTransforms: fold, flatten, sample, and pivot
│   ├── RowTransforms.Lookup.cs    — RowTransforms: lookup
│   ├── RowSampler.cs              — RowTransforms: deterministic reservoir sampling
│   ├── TableLayout.cs             — RowTransforms: derived table layout, bounds, and field reader
│   ├── ParameterEnvironment.cs    — RowTransforms: static parameter values
│   ├── TransformContext.cs        — RowTransforms: the shared context and the row expressions
│   ├── SelectionModel.cs          — SelectionModel: the stores and values of the selections
│   ├── SelectionModel.Projection.cs — SelectionModel: Create, the projection, and the initial value
│   ├── SelectionModel.Queries.cs  — SelectionModel: BindUnits, the components of a unit, and the extent of a selection
│   ├── SelectionTimeUnits.cs      — SelectionTimeUnits: the time-unit fields that a filter or a condition reads
│   ├── AggregateTransforms.cs     — AggregateTransforms: Apply, grouping keys, and shared helpers
│   ├── AggregateTransforms.Aggregate.cs — AggregateTransforms: aggregate, joinaggregate, and the grouping passes
│   ├── AggregateTransforms.Bin.cs — AggregateTransforms: the bin transform
│   ├── AggregateTransforms.Window.cs — AggregateTransforms: the window transform and engine
│   ├── AggregateTransforms.Stack.cs — AggregateTransforms: the stack transform and engine
│   ├── AggregateTransforms.Impute.cs — AggregateTransforms: the impute transform
│   ├── AggregateMeasures.cs       — AggregateTransforms: operation names and the operation registry
│   ├── AggregateMeasures.Cell.cs  — AggregateTransforms: the accumulators of the aggregate operations
│   ├── BinAlgorithm.cs            — AggregateTransforms: the Vega bin algorithm and bin assignment
│   ├── StackRules.cs              — AggregateTransforms: Vega-Lite's decision that a view stacks
│   ├── WorkMeter.cs               — AggregateTransforms: step-budget charging of proportional work
│   ├── StableSorter.cs            — AggregateTransforms: stable, cancelable merge sort
│   ├── EncodingPlan.cs            — EncodingPlan: the plan of the implicit bin, time unit, aggregate, impute, and stack
│   ├── EncodingTransforms.cs      — EncodingPlan: running the plan (with the impute stages), and the Raw and Main tables
│   ├── EncodingTransforms.Facet.cs — EncodingPlan: the cells of a facet (ordered values, grid, partition of the rows)
│   ├── TimeUnitTransform.cs       — TimeUnitTransform: the start and end of a time unit for every row
│   ├── TemporalFields.cs          — TimeUnitTransform: the fields that are read as dates
│   ├── StatTransforms.cs          — StatTransforms: Apply, the groups of Vega's partition, and the output table
│   ├── StatTransforms.Regression.cs — StatTransforms: the regression transform
│   ├── StatTransforms.Loess.cs    — StatTransforms: the loess transform
│   ├── StatTransforms.Density.cs  — StatTransforms: the density transform
│   ├── StatTransforms.Quantile.cs — StatTransforms: the quantile transform
│   ├── StatTransforms.Extent.cs   — StatTransforms: the extent transform and its parameter
│   ├── RegressionFit.cs           — StatTransforms: the regression models (linear, log, exp, pow, quad, poly)
│   ├── CurveSampler.cs            — StatTransforms: the adaptive sampling of a curve
│   ├── LoessFit.cs                — StatTransforms: the locally weighted regression
│   ├── KernelDensity.cs           — StatTransforms: the Gaussian kernel density and Scott's rule
│   └── StatisticsPrimitives.cs    — StatTransforms: number conversion, quantiles, deviation, extent, range length
├── Scales/                        — Scales subsystem (internal)
│   ├── NamespaceDoc.cs            — Scales: namespace-level XML documentation
│   ├── ScaleModel.cs              — ScaleModel: linear, transformed, band, point, ordinal, and color mappings
│   ├── ScaleResolver.cs           — ScaleResolver: scale inference, ResolvedScales, and ResolvedScale
│   ├── ScaleResolver.Plan.cs      — ScaleResolver: binned, stacked, ranged, and unaggregated domains
│   ├── ScaleResolver.Sort.cs      — ScaleResolver: the order of discrete domains, including sort by an aggregate
│   ├── ScaleResolver.Color.cs     — ScaleResolver: the colors of color, fill, and stroke scales and quantile domains
│   ├── ScaleResolver.Range.cs     — ScaleResolver: the ranges of the non-position channels and named ranges
│   ├── ScaleResolver.Time.cs      — ScaleResolver: temporal domains, bounds, nice, and the time-unit scales
│   ├── ScaleResolver.Layers.cs    — ScaleResolver: the scales shared by the layers of a view (merge, conflicts, resolve)
│   ├── ScaleResolver.Offsets.cs   — ScaleResolver: the nested offset scales of xOffset and yOffset and the position step
│   ├── ScaleResolver.Raw.cs       — ScaleResolver: the raw domain and the extent of a scale (domainRaw, parameters)
│   ├── Ticks.cs                   — Ticks: d3-array ticks, nice, and Vega tick counts
│   ├── Ticks.Time.cs              — Ticks: d3-time ticks, tick intervals, and nice for time scales
│   ├── NumberFormat.cs            — NumberFormat: d3-format number formatting and tickFormat
│   ├── ColorSchemes.cs            — ColorSchemes: ColorScheme, SchemeRange, SchemeUse, the catalog lookup, and range configuration
│   ├── ColorSchemeData.cs         — ColorSchemes: the palette tables of the 68 color schemes (data only)
│   └── ColorInterpolator.cs       — ColorInterpolator: RgbColor, color spaces, ColorRamp, range and quantize helpers
├── Guides/                        — Guides subsystem (internal)
│   ├── NamespaceDoc.cs            — Guides: namespace-level XML documentation
│   ├── TextMeasurer.cs            — TextMeasurer: font resolution, measurement, and truncation
│   ├── TitleBuilder.cs            — TitleBuilder: title, subtitle, and TitleBlock placement
│   ├── TitleBuilder.Header.cs     — TitleBuilder: the label and title blocks and the header groups of a facet
│   ├── HeaderSettings.cs          — TitleBuilder: the header properties of a facet channel and the labels of its cells
│   ├── AxisBuilder.cs             — AxisBuilder: axis grid, ticks, labels, domain, title, and extents
│   ├── LabelOverlap.cs            — LabelOverlap: parity and greedy removal of overlapping labels
│   ├── LegendPlan.cs              — LegendBuilder: which channels have legends, their kind, orient, title, and merging
│   ├── LegendBuilder.cs           — LegendBuilder: the legend group, title, box, and result records
│   ├── LegendBuilder.Symbols.cs   — LegendBuilder: symbol legend entries, labels, symbols, and bounds
│   ├── LegendBuilder.Gradient.cs  — LegendBuilder: gradient and discrete gradient legends
│   ├── LegendBuilder.Time.cs      — LegendBuilder: the ticks, labels, and values of legends of temporal fields
│   ├── LegendBuilder.Grid.cs      — LegendBuilder: the grid layout of Vega's layout transform
│   ├── LegendDimming.cs           — LegendBuilder: the dimming of the entries of a legend bound to a selection
│   ├── TimeLabels.cs              — TimeLabels: date label format, interval tick count, default count, and values reader
│   └── LabelExpressions.cs        — LabelExpressions: the labelExpr of axes, legends, and headers and the LabelText result
├── Layout/                        — Layout subsystem (internal)
│   ├── NamespaceDoc.cs            — Layout: namespace-level XML documentation
│   ├── CompositionLayout.cs       — CompositionLayout: the cells, headers, and titles of a composition (types and the grid)
│   ├── CompositionLayout.Headers.cs — CompositionLayout: the trellis headers, footers, and titles
│   ├── LegendLayout.cs            — LegendLayout: placement of the legends and their extents
│   └── ViewLayout.cs              — ViewLayout: data rectangle, padding, autosize (pad, none, fit), and scene assembly
├── Marks/                         — Marks subsystem (internal)
│   ├── NamespaceDoc.cs            — Marks: namespace-level XML documentation
│   ├── MarkBuilder.cs             — MarkBuilder: mark items, with the color, size, shape, stroke, and angle channels
│   ├── MarkBuilder.Order.cs       — MarkBuilder: the draw order of the rows (the order channel)
│   ├── MarkBuilder.Paths.cs       — MarkBuilder: the line, area, and trail marks (grouping, sorting, invalid data)
│   ├── MarkBuilder.Overlays.cs    — MarkBuilder: the point and line overlays of the path marks
│   ├── MarkBuilder.Text.cs        — MarkBuilder: the text mark (labels, formats, polar offset)
│   ├── MarkBuilder.Arcs.cs        — MarkBuilder: the arc mark (pie, donut, and radial slices)
│   ├── MarkBuilder.Offsets.cs     — MarkBuilder: the xOffset and yOffset channels (marks inside the band of a position)
│   ├── MarkBuilder.Brush.cs       — MarkBuilder: the brush marks of an interval selection with an initial value
│   ├── CurveBuilder.cs            — CurveBuilder: the curve kinds, the segment bound, and the line and area entry points
│   ├── CurveBuilder.Generators.cs — CurveBuilder: the line and area generators with defined gaps
│   └── CurveBuilder.Splines.cs    — CurveBuilder: the cardinal, catmull-rom, monotone, and natural curves
└── Scenegraph/                    — Scenegraph subsystem (internal)
    ├── NamespaceDoc.cs            — Scenegraph: namespace-level XML documentation
    ├── SceneModel.cs              — SceneModel: Scene, scene items (paths, arcs, trails), and value types
    ├── SceneBounds.cs             — SceneModel: bounding boxes
    ├── SceneBudget.cs             — SceneModel: scene-item budget
    ├── ScenePainter.cs            — ScenePainter: drawing a scene onto a Surface and placement
    ├── ScenePainter.Paths.cs      — ScenePainter: segment paths, arcs, trails, and the miter limit
    ├── ArcGeometry.cs             — ArcGeometry: the d3-shape arc and the d3-path emulation
    ├── SymbolPathParser.cs        — SymbolPathParser: SVG path data of a custom symbol shape (parse, draw, bounds)
    └── CssPaint.cs                — CssPaint: chart paint rules (none, opacity) on the core CssColor parser
```

`CanvasNetVegaLite` uses only the `CanvasNet` system (`Surface`, `Rgba32`, and `CssColor` of `Canvas`, `Path` of
`Geometry`,
`PathFiller` and `PathStroker` of `Drawing`, `TrueTypeFont` and `SystemFontCatalog` of `Fonts`, and `TextRenderer` of
`Rendering`) through its public API, and declares it as its only package dependency; the `ScenePainter`, `ArcGeometry`,
`SymbolPathParser`, and `TextMeasurer` units use it — see _CanvasNetVegaLite System Design_ (`canvas-net-vega-lite.md`).

The `CanvasNetMermaid` system lives in the `src/DemaConsulting.CanvasNet.Mermaid/` project folder (the separate
`DemaConsulting.CanvasNet.Mermaid` package). The `MermaidDocument` unit occupies the project root; the internal
subsystems occupy the sub-folders `Syntax/`, `Configuration/`, `Graphlib/`, `Dagre/`, `Rendering/`, `Scenegraph/`,
`Flowchart/`, `Sequence/`, `Class/`, `State/`, `Er/`, `Pie/`, `Gantt/`, `Mindmap/`, `Cose/`, and `Packet/`, whose
names equal
their sub-namespaces; partial files and small helper types
belong to the unit named before the dash (see _CanvasNetMermaid System Design_, `canvas-net-mermaid.md`):

```text
src/DemaConsulting.CanvasNet.Mermaid/
├── DemaConsulting.CanvasNet.Mermaid.csproj — Package definition (net8.0/net9.0/net10.0; packable; ships third-party/)
├── NamespaceDoc.cs                — MermaidDocument: namespace-level XML documentation
├── MermaidDocument.cs             — MermaidDocument: Parse/Open entry points and the metadata properties
├── MermaidDocument.Render.cs      — MermaidDocument: Measure, Render, natural size, and report delivery
├── MermaidDocument.Pipeline.cs    — MermaidDocument: the internal diagram seam and registry
├── MermaidOpenOptions.cs          — MermaidDocument: validated open options
├── MermaidRenderOptions.cs        — MermaidDocument: validated render options and the theme names
├── MermaidLimits.cs               — MermaidDocument: resource limits
├── MermaidLocation.cs             — MermaidDocument: file, line, and column value
├── MermaidDiagnostic.cs           — MermaidDocument: warning record
├── MermaidUnsupportedFeature.cs   — MermaidDocument: unsupported-feature report record
├── MermaidSize.cs                 — MermaidDocument: size in pixels
├── MermaidDiagnosticCodes.cs      — MermaidDocument: the stable mermaid-* codes
├── MermaidSyntaxException.cs      — MermaidDocument: located syntax exception with the expected tokens
├── MermaidRenderException.cs      — MermaidDocument: located render exception
├── MermaidUnsupportedFeatureException.cs — MermaidDocument: strict-mode exception
├── MermaidReportSink.cs           — MermaidDocument: ordered, de-duplicated, capped report delivery
├── MermaidImageSource.cs          — MermaidDocument: the image source for .mmd and .mermaid files
├── third-party/                   — License texts and the provenance table of the ported projects
├── Syntax/                        — Syntax subsystem
│   ├── Preprocessor.cs, Preprocessor.MappedText.cs — Preprocessor: clean-up, frontmatter, directives, location map
│   ├── DiagramDetector.cs         — DiagramDetector: Mermaid's detectors in Mermaid's order
│   ├── YamlSubsetParser.cs, YamlSubsetParser.Values.cs — YamlSubsetParser: the js-yaml subset
│   ├── SourceScanner.cs, SourceScanner.Patterns.cs, SourceScanner.Tokens.cs — SourceScanner: jison lexer semantics
│   └── HtmlSanitizer.cs, HtmlEntities.cs — HtmlSanitizer: label HTML clean-up and named references
├── Configuration/                 — Configuration subsystem
│   ├── MermaidConfigResolver*.cs, MermaidConfig.cs — MermaidConfigResolver: defaults, precedence, typed view
│   ├── ThemeResolver*.cs          — ThemeResolver: the five themes and the style sheet of each drawn type (pie and
│   │                                gantt and packet in ThemeResolver.ChartStyles.cs, mindmap in
│   │                                ThemeResolver.MindmapStyles.cs)
│   ├── ColorMath.cs, ColorMath.Parsers.cs — ColorMath: the khroma subset
│   └── StyleDeclarations.cs       — StyleDeclarations: the CSS declaration subset
├── Graphlib/                      — Graphlib subsystem
│   ├── Graph.cs, GraphEdge.cs, ObjectKeyMap.cs — Graph: graphlib's graph with JavaScript key order
│   └── GraphAlgorithms.cs         — GraphAlgorithms: depth-first preorder and postorder
├── Dagre/                         — Dagre subsystem
│   ├── DagreLayout.cs, DagreLayout.Util.cs, DagreGraph.cs, LayoutModel.cs, Lodash.cs — DagreLayout: the pipeline
│   ├── DagreAcyclic.cs            — DagreAcyclic: cycle breaking
│   ├── DagreRanker*.cs            — DagreRanker: longest path, feasible tree, network simplex
│   ├── DagreNesting.cs            — DagreNesting: nesting graph, border segments, dummy chains
│   ├── DagreNormalizer.cs         — DagreNormalizer: long-edge dummies
│   ├── DagreOrdering*.cs          — DagreOrdering: crossing reduction
│   ├── DagrePositioning*.cs       — DagrePositioning: Brandes-Koepf coordinates
│   ├── DagreCoordinateSystem.cs   — DagreCoordinateSystem: rank direction transforms
│   └── LayoutWorkMeter.cs         — LayoutWorkMeter: work budget, cancellation, unique ids
├── Rendering/                     — Rendering subsystem
│   ├── ClusterLayout*.cs, LayoutData.cs — ClusterLayout: subgraphs, recursive layout, placement, diagram frame
│   ├── ShapeLibrary*.cs, ShapeNames.cs — ShapeLibrary: shapes, class and entity boxes, state and mindmap shapes
│   ├── EdgeBuilder*.cs            — EdgeBuilder: routes, curves, markers, edge labels, terminal labels
│   ├── LabelBuilder*.cs           — LabelBuilder: HTML, markdown, and SVG labels; the text utilities of sequence text
│   └── NodeImages.cs              — NodeImages: pictures through the caller's resolver
├── Scenegraph/                    — Scenegraph subsystem
│   ├── SceneModel.cs              — SceneModel: scene records, bounds, placement, drop shadows
│   └── ScenePainter*.cs           — ScenePainter: paths, shadows, text, and pictures to pixels
├── Flowchart/                     — Flowchart subsystem
│   ├── FlowchartParser*.cs        — FlowchartParser: the flow.jison lexer and grammar
│   └── FlowchartModel*.cs         — FlowchartModel: the flowDb rules, layout data, and drawing
├── Sequence/                      — Sequence subsystem
│   ├── SequenceParser*.cs         — SequenceParser: the sequenceDiagram.jison lexer and grammar
│   ├── SequenceModel*.cs          — SequenceModel: the sequenceDb rules, metadata, and reports
│   ├── SequenceLayout*.cs         — SequenceLayout: the sequenceRenderer.ts layout and view box
│   └── SequenceRenderer*.cs       — SequenceRenderer: the svgDraw.js drawing into scene items
├── Class/                         — Class subsystem
│   ├── ClassParser*.cs            — ClassParser: the classDiagram.jison lexer and grammar
│   └── ClassModel*.cs             — ClassModel: the classDb rules, members, URL check, metadata, and layout data
├── State/                         — State subsystem
│   ├── StateParser*.cs            — StateParser: the stateDiagram.jison lexer and grammar
│   └── StateModel*.cs             — StateModel: the stateDb rules, data fetcher, metadata, and layout data
├── Er/                            — Er subsystem
│   ├── ErParser*.cs               — ErParser: the erDiagram.jison lexer and grammar
│   └── ErModel*.cs                — ErModel: the erDb rules, subgraphs, styles, metadata, and layout data
├── Pie/                           — Pie subsystem
│   ├── PieParser*.cs              — PieParser: the Langium pie grammar, the pieDb rules, and PieDiagram
│   └── PieRenderer*.cs            — PieRenderer: the pieRenderer.ts drawing with d3's pie and arc
├── Gantt/                         — Gantt subsystem
│   ├── GanttParser*.cs            — GanttParser: the gantt.jison lexer and grammar
│   ├── GanttModel*.cs             — GanttModel: the ganttDb rules, the task dates, the clock, and the dayjs subset
│   ├── GanttRenderer*.cs          — GanttRenderer: the ganttRenderer.js drawing with d3-axis
│   └── TimeAxis*.cs               — TimeAxis: JavaScript dates in UTC and the d3 time functions
├── Mindmap/                       — Mindmap subsystem
│   ├── MindmapParser*.cs          — MindmapParser: the mindmap.jison lexer and grammar, mindmapDb, MindmapDiagram
│   └── MindmapRenderer.cs         — MindmapRenderer: the mindmapRenderer.ts drawing with the cose-bilkent renderer
├── Cose/                          — Cose subsystem
│   └── CoseLayout*.cs             — CoseLayout: the cose-bilkent layout, cytoscape's edge points, correctly rounded math
└── Packet/                        — Packet subsystem
    ├── PacketParser*.cs           — PacketParser: the Langium packet grammar, the packet database, the row split
    └── PacketRenderer.cs          — PacketRenderer: the packet renderer.ts drawing
```

Each sub-folder also holds a `NamespaceDoc.cs` with the namespace documentation. `CanvasNetMermaid` uses only the
`CanvasNet` system through its public API and declares it as its only package dependency.
The `CanvasNetViewer` system lives in the `src/DemaConsulting.CanvasNet.Viewer/` project folder (the separate
`DemaConsulting.CanvasNet.Viewer` package). Its two direct units, `CanvasNetViewer` and `ViewerException`, occupy the
project root, and its `Imaging` subsystem occupies the `Imaging/` sub-folder, whose name equals its sub-namespace (see
_CanvasNetViewer System Design_, `canvas-net-viewer.md`):

```text
src/DemaConsulting.CanvasNet.Viewer/
├── DemaConsulting.CanvasNet.Viewer.csproj — Package definition (net8.0/net9.0/net10.0; packable)
├── NamespaceDoc.cs                — CanvasNetViewer: namespace-level XML documentation
├── CanvasNetViewer.cs             — CanvasNetViewer: the built-in sources, CreateImageResolver, and Render
├── ViewerRenderOptions.cs         — CanvasNetViewer: the render options (size, folder, strictness, extra sources, token)
├── ViewerImage.cs                 — CanvasNetViewer: the rendered picture with its format, notes, text, and embedded images
├── ViewerException.cs             — ViewerException: the exception of every expected failure, and the stable ErrorCodes
└── Imaging/                       — Imaging subsystem
    ├── NamespaceDoc.cs            — Imaging: namespace-level XML documentation
    ├── SourceModels.cs            — SourceLoader: source kinds, output formats, and the load model types
    ├── SourceLoader.cs            — SourceLoader: probe, load, size policy, and the exception mapping
    ├── SourceLoader.Detect.cs     — SourceLoader: format detection
    ├── SourceLoader.Raster.cs     — SourceLoader: PNG, JPEG, BMP, TIFF, GIF, and JPEG 2000 sources
    ├── SourceLoader.Svg.cs        — SourceLoader: SVG sources
    ├── SourceLoader.SvgImages.cs  — SourceLoader: the wiring of the viewer's image resolver for the files an SVG embeds
    ├── SourceLoader.Documents.cs  — SourceLoader: the page-based sources' shared sizing and rendering
    ├── SourceLoader.Pdf.cs        — SourceLoader: PDF documents
    ├── SourceLoader.Pptx.cs       — SourceLoader: PowerPoint presentations
    ├── SourceLoader.Vsdx.cs       — SourceLoader: Visio drawings
    ├── SourceLoader.Scad.cs       — SourceLoader: OpenSCAD models drawn from a named view
    ├── SourceLoader.VegaLite.cs   — SourceLoader: Vega-Lite charts
    ├── SourceLoader.Mermaid.cs    — SourceLoader: Mermaid diagrams
    ├── ImageOperations.cs         — ImagePipeline: the operation records (crop, resize, fit, rotate, flip, pad, photo)
    ├── ImagePipeline.cs           — ImagePipeline: applying the ordered operations to a surface
    ├── ImagePipeline.Anchors.cs   — ImagePipeline: the mapping of model anchors through the applied steps
    ├── GridOverlay.cs             — GridOverlay: the labeled coordinate grid, drawn with the bundled font
    ├── ImageComparison.cs         — ImageComparison: the thresholds of a comparison and the defaults of its options
    ├── ContactSheet.cs            — ContactSheet: layout and composition of a contact sheet, with bundled-font labels
    ├── ImageWriter.cs             — ImageWriter: PNG, JPEG, BMP, and TIFF output, atomic and with the overwrite policy
    ├── ImageRenderer.cs           — ImageRenderer: the render operation (load, transform, and write the pages of a source)
    ├── ImageInspector.cs          — ImageInspector: the inspect operation (colors and pixels after the pipeline)
    ├── ImageComparer.cs           — ImageComparer: the compare operation (sizes, measures, thresholds, and pictures)
    ├── MontageBuilder.cs          — MontageBuilder: the montage operation (a contact sheet of many sources)
    ├── SvgFlattener.cs            — SvgFlattener: the flatten operation (records and orchestration)
    ├── SvgFlattener.Links.cs      — SvgFlattener: the link scanner, the rewrite, and the rebasing of links
    └── SvgFlattener.Recorder.cs   — SvgFlattener: the recording resolver that draws the links to replace
```

`CanvasNetViewer` references the `CanvasNet` core package (its `ImageSources` subsystem) and the `CanvasNetSvg`,
`CanvasNetPdf`, `CanvasNetPptx`, `CanvasNetVsdx`, `CanvasNetScad`, `CanvasNetRender3D`, `CanvasNetVegaLite`, and
`CanvasNetMermaid` packages for the image source each one supplies and the operations of `Imaging`; no package it except
the tool.

The `CanvasNetTool` system lives in the `src/DemaConsulting.CanvasNet.Tool/` project folder (the separate
`DemaConsulting.CanvasNet.Tool` dotnet tool package). The six direct units occupy the project root, and the
`Commands` subsystem occupies the `Commands/` sub-folder, whose name equals its sub-namespace
(see _CanvasNetTool System Design_, `canvas-net-tool.md`):

```text
src/DemaConsulting.CanvasNet.Tool/
├── DemaConsulting.CanvasNet.Tool.csproj — Tool package definition (net8.0/net9.0/net10.0; packable as a dotnet tool)
├── Program.cs                     — Program: entry point, dispatch, and the error policy
├── ToolException.cs               — Program: ToolException (a ViewerException with the command and result fields)
├── Context.cs                     — Context: global options, command capture, and output channels
├── ArgumentReader.cs              — ArgumentReader: option reading
├── ArgumentReader.Values.cs       — ArgumentReader: the value grammars (size, geometry, color, and others)
├── ResultWriter.cs                — ResultWriter: deterministic JSON success and failure documents
├── Validation.cs                  — Validation: --validate self-test and results output
├── PathHelpers.cs                 — PathHelpers: safe path combination
└── Commands/                      — Commands subsystem
    ├── ICommand.cs                — CommandRegistry: ICommand contract and OptionSpec
    ├── CommandRegistry.cs         — CommandRegistry: command table, dispatch, and the unknown-command report
    ├── CommandRegistry.Help.cs    — CommandRegistry: the help command, its subjects, examples, and JSON documents
    ├── HelpTopics.cs              — HelpTopics: the topic model, the embedded catalog, tokens, and lookup
    ├── HelpTopics.Render.cs       — HelpTopics: the text and JSON renderings and the wrapping
    ├── Help/                      — HelpTopics: the embedded topic files (scad, vega-lite, mermaid, svg, pdf, pptx,
    │   │                            vsdx, raster, json, errors, and limits, one JSON file each)
    │   └── Examples/              — HelpTopics: the embedded example sources (SCAD, Vega-Lite, Mermaid, and SVG)
    ├── DiscoveryCommands.cs       — DiscoveryCommands: the formats and info commands and the format tables
    ├── RenderCommand.cs           — RenderCommand: the render command (alias convert), run and result
    ├── RenderCommand.Options.cs   — RenderCommand: the option table and the reading of the settings
    ├── RenderCommand.Verbs.cs     — RenderCommand: the verb commands crop, resize, rotate, flip, and pad
    ├── ImagePipelineParser.cs     — RenderCommand: the reading of the pipeline operations from the command line
    ├── CompareCommand.cs          — CompareCommand: the compare command, run, thresholds, and result
    ├── CompareCommand.Options.cs  — CompareCommand: the option table and the reading of the settings
    ├── MontageCommand.cs          — MontageCommand: the montage command, run and result
    ├── MontageCommand.Options.cs  — MontageCommand: the option table and the reading of the settings
    ├── InspectCommand.cs          — InspectCommand: the inspect colors and inspect pixel commands, run and result
    ├── InspectCommand.Options.cs  — InspectCommand: the option table and the reading of the settings and the point
    └── FlattenCommand.cs          — FlattenCommand: the flatten command, options, run, and result
```

The top-level `tools/` folder is a separate tree of support tools that are not software items: unlike
`docs/reqstream/`, `docs/design/`, `docs/verification/`, `src/`, and `test/`, it does not follow the software-item
naming convention, has no requirements, design, or verification artifacts, is not shipped, and is run by hand, never
by CI (see `tools/README.md`):

```text
tools/
├── README.md                    — What each tool is for, when to run it, its prerequisites, and adding a tool
├── DemaConsulting.CanvasNet.VegaLite.Benchmarks/ — Profiling: Stopwatch Vega-Lite benchmarks (built by the solution)
└── visual-diff/                 — Fidelity: visual diff against Vega, Vega-Lite, and mermaid-cli
    └── mermaid/                 — Generators of the Mermaid test goldens and upstream vectors
```

## Document Conventions

Throughout this document:

- Class names, method names, property names, and file names appear in `monospace` font.
- The word **shall** denotes a design constraint that the implementation must satisfy.
- Section headings within each unit chapter follow a consistent structure: overview, data model,
  methods/algorithms, and interactions with other units.
- Text tables are used in preference to diagrams, which may not render in all PDF viewers.

## Companion Artifact Structure

Each software item has corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)
- SysML2 model: `docs/sysml2/model/{system}/.../{item}.sysml` (kebab-case)
- Review-sets: defined in `.reviewmark.yaml`

For the `CanvasNetVegaLite` system the parallel paths are, for example for the `ViewLayout` unit in the `Layout`
subsystem: requirements `docs/reqstream/canvas-net-vega-lite/layout/view-layout.yaml`, design
`docs/design/canvas-net-vega-lite/layout/view-layout.md`, verification
`docs/verification/canvas-net-vega-lite/layout/view-layout.md`, source
`src/DemaConsulting.CanvasNet.VegaLite/Layout/ViewLayout.cs`, tests
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Layout/ViewLayoutTests.cs`, and SysML2 model
`docs/sysml2/model/canvas-net-vega-lite/layout/view-layout.sysml`. The direct unit `VegaLiteDocument` has no subsystem
folder (`docs/design/canvas-net-vega-lite/vega-lite-document.md`), the system itself is
`docs/design/canvas-net-vega-lite.md` with `docs/sysml2/model/canvas-net-vega-lite.sysml`, and the original chart
fixtures (including the data-file chart, its CSV, and the nested JSON rows) and their provenance record are in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Fixtures/`. The two units added to the `Data` subsystem for local data
files follow the same pattern: for `DataFileResolver`, requirements
`docs/reqstream/canvas-net-vega-lite/data/data-file-resolver.yaml`, design
`docs/design/canvas-net-vega-lite/data/data-file-resolver.md`, verification
`docs/verification/canvas-net-vega-lite/data/data-file-resolver.md`, source
`src/DemaConsulting.CanvasNet.VegaLite/Data/DataFileResolver.cs` (with `DataFileResolver.Paths.cs`), tests
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataFileResolverTests.cs` and `DataFileResolverLinkTests.cs` (with
the helpers in `DataTestSupport.cs`), and SysML2 model
`docs/sysml2/model/canvas-net-vega-lite/data/data-file-resolver.sysml`; for `DelimitedTextParser`, the same paths with
`delimited-text-parser`, `DelimitedTextParser.cs`, and `DelimitedTextParserTests.cs`. The two subsystems added for
expressions and row transforms follow the same pattern with their own subsystem files: for the `Expressions` subsystem,
requirements `docs/reqstream/canvas-net-vega-lite/expressions.yaml`, design
`docs/design/canvas-net-vega-lite/expressions.md`, verification `docs/verification/canvas-net-vega-lite/expressions.md`,
and SysML2 model `docs/sysml2/model/canvas-net-vega-lite/expressions.sysml`, and for its `ExpressionParser` unit
`expressions/expression-parser.yaml`, `expressions/expression-parser.md` (design and verification), source
`src/DemaConsulting.CanvasNet.VegaLite/Expressions/ExpressionParser.cs` (with its partial files and
`CompiledExpression.cs`), tests `test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionParserTests.cs`, and
`expressions/expression-parser.sysml`; the `ExpressionEvaluator` and `ExpressionFunctions` units have the same paths
with `expression-evaluator` and `expression-functions`. For the `Transforms` subsystem the files are `transforms.yaml`,
`transforms.md`, and `transforms.sysml` and, for its `RowTransforms` unit, `transforms/row-transforms.*`, source
`src/DemaConsulting.CanvasNet.VegaLite/Transforms/RowTransforms.cs` (with its partial files, `RowSampler.cs`,
`TableLayout.cs`, `ParameterEnvironment.cs`, and `TransformContext.cs`), and tests
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/RowTransformsTests.cs`. The two units added to the `Transforms`
subsystem for aggregation follow the same pattern: for `AggregateTransforms`, requirements
`docs/reqstream/canvas-net-vega-lite/transforms/aggregate-transforms.yaml`, design
`docs/design/canvas-net-vega-lite/transforms/aggregate-transforms.md`, verification
`docs/verification/canvas-net-vega-lite/transforms/aggregate-transforms.md`, SysML2 model
`docs/sysml2/model/canvas-net-vega-lite/transforms/aggregate-transforms.sysml`, source
`src/DemaConsulting.CanvasNet.VegaLite/Transforms/AggregateTransforms.cs` (with its partial files,
`AggregateMeasures.cs` and `AggregateMeasures.Cell.cs`, `BinAlgorithm.cs`, `StackRules.cs`, `WorkMeter.cs`, and
`StableSorter.cs`), and tests `test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/AggregateTransformsTests.cs`
(with the window, stack, impute, and joinaggregate test files, `AggregateMeasuresTests.cs`, `BinAlgorithmTests.cs`,
`StackRulesTests.cs`, and `StableSorterTests.cs`); for `EncodingPlan`, the same paths with `encoding-plan`,
`EncodingPlan.cs` and `EncodingTransforms.cs`, and `EncodingPlanTests.cs` and `EncodingTransformsTests.cs`. The units
added for time follow the same pattern: for `TimeCalendar` in the `Data` subsystem, requirements
`docs/reqstream/canvas-net-vega-lite/data/time-calendar.yaml`, design
`docs/design/canvas-net-vega-lite/data/time-calendar.md`, verification
`docs/verification/canvas-net-vega-lite/data/time-calendar.md`, SysML2 model
`docs/sysml2/model/canvas-net-vega-lite/data/time-calendar.sysml`, source
`src/DemaConsulting.CanvasNet.VegaLite/Data/TimeCalendar.cs` (with `TimeCalendar.Zone.cs`, `TimeCalendar.Intervals.cs`,
`TimeUnits.cs`, and `TimeDateObject.cs`), and tests
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/TimeCalendarTests.cs` (with the zone, interval, epoch-day, time unit,
and date-time object test files); for `TimeFormat`, the same paths with `time-format`, `TimeFormat.cs` (with its partial
files and `JsDateParser.cs` and `JsDateParser.Legacy.cs`), and `TimeFormatTests.cs` (with the parse, multi-format, and
`Date.parse` test files); for `TimeUnitTransform` in the `Transforms` subsystem, the same paths with
`transforms/time-unit-transform`, `TimeUnitTransform.cs` and `TemporalFields.cs`, and `TimeUnitTransformTests.cs` and
`TemporalFieldsTests.cs`; for `StatTransforms` in the `Transforms` subsystem, the same paths with
`transforms/stat-transforms`, `StatTransforms*.cs` (with `RegressionFit.cs`, `CurveSampler.cs`, `LoessFit.cs`,
`KernelDensity.cs`, and `StatisticsPrimitives.cs`), and `StatTransforms*Tests.cs` (with the test files of the numeric
helpers and `StatTestSupport.cs`); and for `TimeLabels` in the `Guides` subsystem, the same paths with
`guides/time-labels`, `TimeLabels.cs` (the temporal legends are in `LegendBuilder.Time.cs`, a partial file of the
`LegendBuilder` unit), and `TimeLabelsTests.cs`; for `LabelExpressions` in the `Guides` subsystem, the same paths with
`guides/label-expressions`, `LabelExpressions.cs`, and `LabelExpressionsTests.cs` (with `AxisBuilderLabelExprTests.cs`
and `LegendBuilderLabelExprTests.cs` for the guides that use it, and `LegendBuilderSelectionTests.cs` for the legend of
a selection). The two units added for the line, area, trail, text, and arc marks follow the same pattern: for
`CurveBuilder` in the `Marks` subsystem, requirements `docs/reqstream/canvas-net-vega-lite/marks/curve-builder.yaml`,
design `docs/design/canvas-net-vega-lite/marks/curve-builder.md`, verification
`docs/verification/canvas-net-vega-lite/marks/curve-builder.md`, SysML2 model
`docs/sysml2/model/canvas-net-vega-lite/marks/curve-builder.sysml`, source
`src/DemaConsulting.CanvasNet.VegaLite/Marks/CurveBuilder.cs` (with `CurveBuilder.Generators.cs` and
`CurveBuilder.Splines.cs`), and tests `test/DemaConsulting.CanvasNet.VegaLite.Tests/Marks/CurveBuilderTests.cs` (with
`CurveBuilderSplineTests.cs` and `CurveBuilderAreaTests.cs`); for `ArcGeometry` in the `Scenegraph` subsystem, the same
paths with `scenegraph/arc-geometry`, `Scenegraph/ArcGeometry.cs` (which also holds the internal `ArcPathWriter`), and
`ArcGeometryTests.cs`; and for `SymbolPathParser`, `scenegraph/symbol-path-parser`, `Scenegraph/SymbolPathParser.cs`, and
`SymbolPathParserTests.cs`. The path, text, and arc marks themselves are in the partial files of the existing `MarkBuilder`
unit (`MarkBuilder.Paths.cs`, `.Order.cs`, `.Overlays.cs`, `.Text.cs`, and `.Arcs.cs`, with the tests
`MarkBuilderPathTests.cs`, `MarkBuilderOrderTests.cs`, `MarkBuilderOverlayTests.cs`, `MarkBuilderTextTests.cs`,
`MarkBuilderArcTests.cs`, and `MarkBuilderPolarTextTests.cs`), and the painting of the segment paths, arcs, and trails
is in `ScenePainter.Paths.cs`, a partial file of the `ScenePainter` unit (with `ScenePainterPathTests.cs`). The
attribution record and license texts of the ported algorithms are in
`src/DemaConsulting.CanvasNet.VegaLite/third-party/`. The developer artifacts of Phase 14 are not software items and
have no companion artifacts of their own: the original gallery specifications and their two small SVG pictures are in
`docs/user_guide/examples/gallery/` (drawn by the Pandoc filter, rendered strictly by
`test/DemaConsulting.CanvasNet.VegaLite.Tests/VegaLiteGalleryTests.cs`, and shown in the montage
`docs/images/vegalite-gallery.png`, which `docs/images/render-images.ps1` draws and checks), the Stopwatch benchmark
harness is the project `tools/DemaConsulting.CanvasNet.VegaLite.Benchmarks/` (compiled by the solution build, run
by hand), and the visual-diff scripts against Vega and Vega-Lite are in `tools/visual-diff/` (run by hand, never in
CI).

For the `CanvasNetMermaid` system the parallel paths are, for example for the `DagreRanker` unit in the `Dagre`
subsystem: requirements `docs/reqstream/canvas-net-mermaid/dagre/dagre-ranker.yaml`, design
`docs/design/canvas-net-mermaid/dagre/dagre-ranker.md`, verification
`docs/verification/canvas-net-mermaid/dagre/dagre-ranker.md`, SysML2 model
`docs/sysml2/model/canvas-net-mermaid/dagre/dagre-ranker.sysml`, source
`src/DemaConsulting.CanvasNet.Mermaid/Dagre/DagreRanker.cs` (with `DagreRanker.FeasibleTree.cs` and
`DagreRanker.NetworkSimplex.cs`), and tests `test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreRankerTests.cs`.
The subsystem tests are `{Subsystem}Tests.cs` in each test folder (for example `Dagre/DagreTests.cs` with the dagre
stage goldens), the direct unit `MermaidDocument` has no subsystem folder
(`docs/design/canvas-net-mermaid/mermaid-document.md`), and the system itself is `docs/design/canvas-net-mermaid.md`
with `docs/sysml2/model/canvas-net-mermaid.sysml`; its system tests are `MermaidSystemIntegrationTests.cs`,
`MermaidFlowchartIntegrationTests.cs`, `MermaidReferenceLayoutTests.cs`, `MermaidSequenceIntegrationTests.cs`,
`MermaidSequenceReferenceLayoutTests.cs`, `MermaidClassIntegrationTests.cs`, `MermaidStateIntegrationTests.cs`,
`MermaidErIntegrationTests.cs`, `MermaidPieIntegrationTests.cs`, `MermaidGanttIntegrationTests.cs`
(with `MermaidChartReferenceLayoutTests.cs`), `MermaidMindmapIntegrationTests.cs` (with
`MermaidMindmapReferenceLayoutTests.cs`), `MermaidPacketIntegrationTests.cs`, `MermaidGalleryTests.cs`,
`MermaidHardeningTests.cs`, and `MermaidUnsupportedDiagramTests.cs` in the root of its test project. Units whose helper
types live in their own files list those files in their design and SysML2 model (for example `ObjectKeyMap.cs` belongs
to `Graph`, `ShapeNames.cs` to `ShapeLibrary`, `LayoutData.cs` to `ClusterLayout`, and `HtmlEntities.cs` to
`HtmlSanitizer`). The original flowchart, sequence, class, state, entity relationship, pie, gantt, mindmap, and packet
fixtures are in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/flowchart/`, `Fixtures/sequence/`,
`Fixtures/class/`, `Fixtures/state/`, `Fixtures/er/`, `Fixtures/pie/`, `Fixtures/gantt/`, `Fixtures/mindmap/`, and
`Fixtures/packet/`, the upstream vectors in
`Fixtures/upstream-vectors/`, and the reference layout goldens with their provenance README in `Fixtures/reference/`
(the sequence, class, state, entity relationship, pie, gantt, mindmap, and packet goldens in
`Fixtures/reference/sequence/`,
`Fixtures/reference/class/`, `Fixtures/reference/state/`, `Fixtures/reference/er/`, `Fixtures/reference/pie/`,
`Fixtures/reference/gantt/`, `Fixtures/reference/mindmap/`, and `Fixtures/reference/packet/`). The attribution record
and license texts are in `src/DemaConsulting.CanvasNet.Mermaid/third-party/`. The developer scripts that generate the
vectors and goldens, and the visual-diff harness against mermaid-cli, are in `tools/visual-diff/` and
`tools/visual-diff/mermaid/` (run by hand, never in CI); they are not software items.

## References

- CanvasNet User Guide — the compiled User Guide document for this repository.
- CanvasNet Repository — the CanvasNet source repository hosted on
  GitHub.
<!-- cspell:ignore Outliner -->
