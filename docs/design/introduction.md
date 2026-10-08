# Introduction

<!-- cspell:ignore glyf sfnt cmap loca hmtx hhea codepoint Zapf Noto Visio visio -->

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
  32-bit RGBA pixel buffer with span-based row access) and the `Rgba32` unit
- **Codecs (Subsystem)** — Image format codecs: `BmpCodec`, `PngCodec`, `TiffCodec`, and
  `JpegCodec`, each converting to and from a `Surface` pixel buffer; and `GifCodec`, a decode-only
  unit that loads a `Surface` from the first frame of a GIF file
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
  unit (a public static `Fill` entry point, covering the supporting `FillRule` enum and the
  internal `EdgeFlattener`/`ScanlineRasterizer` helpers inline), the `PathStroker` unit (a public
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
  TrueType text with alignment and kerning), and `Shapes` (rectangle, rounded rectangle, and
  circle helpers)
- **CanvasNetSvg (System)** — A separate, independently-distributed software system providing
  SVG (Scalable Vector Graphics) rasterization, containing a single unit, `SvgCodec`, which
  decodes/rasterizes a subset of SVG vector documents into a `CanvasNet.Canvas.Surface`.
  `CanvasNetSvg` depends on this `CanvasNet` system's `Canvas`, `Geometry`, `Drawing`, `Fonts`,
  and `Codecs` subsystems — see _CanvasNetSvg System Design_ (`canvas-net-svg.md`)
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
  nested tables, and table auto-sizing/banding remain explicitly deferred. Pattern fill (a
  documented subset of 30 of the 54 named ECMA-376 `ST_PresetPatternVal` preset names, including
  background fills; the remaining 24 are deferred) and picture fill are both implemented — see
  _CanvasNetPptx System Design_ (`canvas-net-pptx.md`) for the full supported/deferred boundary.
  `CanvasNetPptx` depends on the `CanvasNet` system's `Canvas`, `Geometry`,
  `Drawing`, `Fonts`, and `Codecs` subsystems (for the `Rgba32` color type, path geometry/
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
  `Render` API. Embedded images/`Foreign` shapes, non-trivial theme-variation resolution, non-solid
  fill-pattern combinations beyond solid, the full arrowhead style-index table, and several
  documented-but-unobserved geometry row types remain explicitly deferred — see
  _CanvasNetVsdx System Design_ (`canvas-net-vsdx.md`) for the full supported/deferred boundary and
  its Implementation Phase Plan. `CanvasNetVsdx` depends on the `CanvasNet` system's `Canvas`,
  `Geometry`, `Drawing`, and `Fonts` subsystems, and must never reference `CanvasNetPptx`,
  `CanvasNetPdf`, or `CanvasNetCharts` — a future, explicitly out-of-scope dependency on the
  sibling `CanvasNetSvg` system (to rasterize an embedded SVG foreign object) is anticipated but
  not yet modeled — see _CanvasNetVsdx System Design_ (`canvas-net-vsdx.md`)

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

CanvasNet is organized into six subsystems under the system level: the `Canvas` subsystem
(the `Surface` and `Rgba32` units, namespace `DemaConsulting.CanvasNet.Canvas`), the `Codecs`
subsystem (the `BmpCodec`, `PngCodec`, `TiffCodec`, `JpegCodec`, and `GifCodec` units,
namespace `DemaConsulting.CanvasNet.Codecs`, flat — no further nesting), the
`Geometry` subsystem (the
`Rect`, `Path`, `BezierFlattening`, and `SvgArcConverter` units, namespace
`DemaConsulting.CanvasNet.Geometry`, flat — no further nesting), the `Drawing` subsystem (the
`PathFiller` unit, covering the supporting `FillRule` enum and the internal
`EdgeFlattener`/`ScanlineRasterizer` helpers inline, the `PathStroker` unit, covering the
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
the `CanvasNetPdf` roadmap, with no internal helpers of its own, namespace
`DemaConsulting.CanvasNet.Fonts`, flat — no further nesting), and the `Rendering` subsystem (the
transform-aware `Canvas` wrapper unit,
the `TextRenderer` unit, covering the supporting `TextAlign` and `TextMetrics` types inline, and
the `Shapes` extension-method unit, namespace `DemaConsulting.CanvasNet.Rendering`, flat — no
further nesting). As additional functionality is added, further subsystems and nested
subsystems would organize related units and provide architectural boundaries with well-defined
interfaces and responsibilities.

A sibling top-level system, `CanvasNetSvg`, lives in this same repository alongside `CanvasNet`
(rather than as one of its subsystems): its sole unit, `SvgCodec`, namespace
`DemaConsulting.CanvasNet.Svg`, is distributed as its own separate NuGet package and depends on
the `CanvasNet` system's `Canvas`, `Geometry`, `Drawing`, `Fonts`, and `Codecs` subsystems — see
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

A third sibling top-level system (within this section's narrative; `CanvasNetPptx` and
`CanvasNetCharts` are additional sibling systems not yet narrated here — a pre-existing gap this
branch did not introduce), `CanvasNetVsdx`, likewise lives in this same repository
alongside `CanvasNet`: its sole unit, `VsdxDocument`, namespace `DemaConsulting.CanvasNet.Vsdx`,
is distributed as its own separate NuGet package and depends on the `CanvasNet` system's
`Canvas`, `Geometry`, `Drawing`, and `Fonts` subsystems (`Render` opens a `.vsdx` package's OPC
structure, resolves the Master/MasterShape cell-and-geometry-row inheritance chain and the
StyleSheet line/fill/text style chain, resolves shape/connector transforms and geometry rows to
paintable paths — including Milestone 11's real `EllipticalArcTo`/`ArcTo`-to-Bezier conversion —
composes arbitrarily nested group/child-shape transforms, and paints a page's full shape tree,
including text, in document order) — see the Folder Layout section below and
_CanvasNetVsdx System Design_ (`canvas-net-vsdx.md`).

## Folder Layout

The source code folder structure mirrors the software structure organization, with file paths
and descriptions as follows:

```text
src/DemaConsulting.CanvasNet/
├── Canvas/
│   ├── Surface.cs               — Mutable, in-memory 32-bit RGBA pixel buffer
│   ├── Rgba32.cs                — Single 32-bit RGBA pixel value type
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
│   │                                scan decoding, encoder, Huffman, DCT)
│   ├── GifCodec.cs               — Decode-only, first-frame-only GIF loader; public API entry
│   │                                point, partial-class implementation continues under `Gif/`
│   ├── Gif/                      — `GifCodec` partial-class implementation files (decode, LZW)
│   ├── ImageInfo.cs              — Shared `GetInfo` return type (dimensions, channels, alpha,
│   │                                `CanDecode`, `FrameCount`) common to all five codecs
│   ├── UnsupportedImageFeatureException.cs — Thrown by `Load` for a well-formed but
│   │                                unsupported file feature (for example PNG Adam7
│   │                                interlacing), distinct from `InvalidDataException`
│   └── NamespaceDoc.cs           — Namespace-level XML documentation
├── Drawing/
│   ├── FillRule.cs                — Nonzero/even-odd fill-rule enumeration
│   ├── EdgeFlattener.cs           — Converts a Path's subpaths into closed polygons
│   ├── ScanlineRasterizer.cs      — Analytic coverage-accumulation scanline rasterizer
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
│   ├── SystemFontCatalog.cs             — OS font discovery, best-effort matching, bundled fallback
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
└── Rendering/
    ├── Canvas.cs                   — Transform-aware Save/Restore/Translate/RotateDegrees wrapper
    ├── TextAlign.cs                — Text horizontal-alignment enumeration
    ├── TextMetrics.cs              — Measured width/ascent/descent result for a text run
    ├── TextRenderer.cs             — Measures and draws TrueType text with alignment and kerning
    ├── Shapes.cs                   — Fill/stroke extension helpers for rectangles, rounded
    │                                 rectangles, and circles
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
│                                 data/render, patterns, preserve-aspect-ratio, shapes, text)
├── SvgFontFace.cs              — Registered font-face (family/weight/style) data carrier
├── SvgFontStyle.cs             — Font-style enumeration used by `SvgFontFace`
└── NamespaceDoc.cs             — Namespace-level XML documentation
```

This six-subsystem folder structure reflects the small number of subsystems in the system
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
│                                        (`g`/`G`/`rg`/`RG`/`k`/`K`/`cs`/`CS`/`sc`/`SC`/`scn`/`SCN`)
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
(`canvas-net-vsdx.md`) for exactly which file introduced each dependency. `CanvasNetPptx` and
`CanvasNetCharts` also have their own sibling project folders
(`src/DemaConsulting.CanvasNet.Pptx/`, `src/DemaConsulting.CanvasNet.Charts/`); their folder-layout
trees are not yet documented in this section — a pre-existing gap predating this branch, tracked
separately and not addressed here.

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

## References

- CanvasNet User Guide — the compiled User Guide document for this repository.
- CanvasNet Repository — the CanvasNet source repository hosted on
  GitHub.
<!-- cspell:ignore Outliner -->
