# Introduction

<!-- cspell:ignore glyf sfnt -->
<!-- cspell:ignore Visio -->
<!-- cspell:ignore rasterizer tessellator -->
<!-- cspell:ignore canvasnet -->

This document provides the verification design for CanvasNet, a .NET library
providing a canvas-based drawing and rendering API.

## Purpose

The purpose of this document is to serve as the verification design entry point and document how
requirements will be tested across all software items in the CanvasNet system. This
documentation enables formal review by mapping every requirement to named test scenarios, supports
compliance auditing by providing clear traceability from requirements through verification design
to tests, and ensures test completeness can be assessed without reading implementation code.

This document is intended for:

- Software developers implementing and maintaining tests
- Code reviewers validating test completeness against requirements
- Compliance auditors tracing requirements through verification design to tests
- Quality assurance teams validating test coverage and scenario adequacy

## Scope

This document covers the verification design for the CanvasNet system and its
constituent software items, specifically:

- **CanvasNet (System)** — The complete .NET library system
- **Canvas (Subsystem)** — Pixel-buffer primitives: the `Surface` unit (mutable, in-memory
  32-bit RGBA pixel buffer with span-based row access) with its `SurfaceAdjustments` and `SurfaceTransforms` parts, the
  `Rgba32` unit, and the `CssColor` unit
  (the shared CSS color parser)
- **Codecs (Subsystem)** — Image format codecs: `BmpCodec`, `PngCodec`, `TiffCodec`,
  `JpegCodec`, `GifCodec`, `Jpeg2000Codec`, and `ImageDecoder`; the first four convert to and from a `Surface`
  pixel buffer, `GifCodec` and `Jpeg2000Codec` are decode-only loaders, and `ImageDecoder` is the
  format-recognition and dispatch facade over them
- **Geometry (Subsystem)** — Vector-geometry primitives: `Rect`, `Path`, `BezierFlattening`,
  `SvgArcConverter`, and `CornerRoundEffect`
- **Drawing (Subsystem)** — Vector rasterization and stroke conversion: `PathFiller`,
  `PathStroker`, `GradientPaint`, and `TilePaint`
- **Fonts (Subsystem)** — TrueType (`glyf`-based) SFNT font loading and glyph-outline extraction:
  `TrueTypeFont` and `SystemFontCatalog`
- **Files (Subsystem)** — Sandboxed file access: the `SandboxedFileResolver` unit and its supporting
  `SandboxOptions`, `SandboxedFile`, `SandboxException`, and `SandboxFailureKind` types
- **ImageSources (Subsystem)** — Linked-picture support: the `ImageSourceContract`, `FolderImageResolver`, and
  `RasterImageSource` units and their supporting types
- **CanvasNetSvg (System)** — A separate, independently-distributed software system providing
  SVG (Scalable Vector Graphics) rasterization, containing a single unit, `SvgCodec` — see
  _CanvasNetSvg System Verification_ (`canvas-net-svg.md`)
- **CanvasNetPdf (System)** — A separate, independently-distributed software system providing
  PDF page-rendering support, containing a single unit, `PdfDocument` — see
  _CanvasNetPdf System Verification_ (`canvas-net-pdf.md`)
- **CanvasNetCharts (System)** — A separate, independently-distributed software system providing
  chart support, being delivered incrementally. The current release ships two subsystems:
  `ChartModel`, containing the `ChartDocument` and `ChartRenderer` units, and `OpenXmlChart`,
  containing the `OpenXmlChartParser` unit — see _CanvasNetCharts System Verification_
  (`canvas-net-charts.md`)
- **CanvasNetPptx (System)** — A separate, independently-distributed software system providing
  PowerPoint (`.pptx`) presentation-rendering support, containing a single unit,
  `PptxDocument` — see _CanvasNetPptx System Verification_ (`canvas-net-pptx.md`)
- **CanvasNetVsdx (System)** — A separate, independently-distributed software system providing
  Microsoft Visio (`.vsdx`) diagram-rendering support, containing a single unit,
  `VsdxDocument` — see _CanvasNetVsdx System Verification_ (`canvas-net-vsdx.md`)
- **CanvasNetRender3D (System)** — A separate, independently-distributed, format-agnostic software
  system providing a pure-managed 3D mesh/scene rasterizer, containing three subsystems:
  `SceneModel` (units `DoublePrecisionMath`, `Mesh`, and `SceneGraph`), `Tessellation` (units
  `PrimitiveTessellator`, `PolygonTriangulator`, `Extruder`, `ConvexHull`, and `MinkowskiSum`), and
  `SceneRendering` (units `Camera`, `Lighting`, `TriangleRasterizer`,
  `FragmentBuffer`, `CsgResolver`, and `SceneRenderer`), including constructive solid geometry
  (union, difference, intersection) through per-pixel fragment lists and polygon extrusion — see _CanvasNetRender3D
  System Verification_ (`canvas-net-render3d.md`)
- **CanvasNetScad (System)** — A separate software system providing a clean-room front end for the
  OpenSCAD language that parses, evaluates, builds the geometry of, and renders models, containing a single
  direct unit, `ScadDocument` (the public parse, evaluate, build, and render API, options, limits, locations, and
  exceptions), an internal `ScadLanguage` subsystem with the units `ScadLexer` and `ScadParser`, an internal
  `ScadEvaluation` subsystem with the units `ScadValue`, `ScadEvaluator`, `ScadBuiltinFunctions`, and
  `ScadFileResolver`, and an internal `ScadGeometry` subsystem with the units `ScadGeometryBuilder`,
  `ScadPrimitives`, `ScadTransforms`, `ScadExtrusions`, and `ScadTextGeometry` —
  see _CanvasNetScad System Verification_ (`canvas-net-scad.md`)
- **CanvasNetVegaLite (System)** — A separate software system providing a Vega-Lite chart renderer that parses a
  Vega-Lite JSON specification and measures and renders the supported chart subset to a `Surface`, containing a single
  direct unit, `VegaLiteDocument` (the public parse, open, measure, and render API, options, limits, locations, and
  exceptions), and nine internal subsystems: `SpecModel` (units `SpecParser`, `FeatureCatalog`, and `ConfigResolver`),
  `Data` (units `DataTable`, `DataLoader`, `DataFileResolver`, `DelimitedTextParser`, `TimeCalendar`, the calendar, the
  d3-time intervals, the Vega-Lite time units, and the datetime objects, and `TimeFormat`, the d3-time-format formatter
  and parser and the `Date.parse` rules), `Expressions` (units `ExpressionParser`, `ExpressionEvaluator`, and
  `ExpressionFunctions`, the supported subset of the Vega expression language), `Transforms` (units `RowTransforms`, the
  row transforms `calculate`, `filter`, `fold`, `flatten`, `sample`, `pivot` (with every aggregate operation), and
  `lookup` and the static parameters; `AggregateTransforms`, the grouped transforms `aggregate`, `joinaggregate`,
  `window`, `bin`, `stack`, and `impute`; `EncodingPlan`, the implicit bin, time unit, aggregate, impute, and stack of
  the encoding; `TimeUnitTransform`, the `timeUnit` transform and the start and end of the time unit of a date; and
  `StatTransforms`, the `regression`, `loess`, `density`, `quantile`, and `extent` transforms), `Scales` (units
  `ScaleModel`, `ScaleResolver`, `Ticks`, `NumberFormat`, `ColorSchemes`, and `ColorInterpolator`), `Guides` (units
  `TextMeasurer`, `TitleBuilder`, `AxisBuilder`, `LabelOverlap`, `LegendBuilder`, and `TimeLabels`), `Layout` (units
  `ViewLayout` and `LegendLayout`), `Marks` (units `MarkBuilder`, the bar, rect, symbol, rule, tick, line, area, trail,
  text, and arc marks, and `CurveBuilder`, the d3-shape curves), and `Scenegraph` (units `SceneModel`, `ScenePainter`,
  `ArcGeometry`, the d3-shape arc, `CssPaint`, and `SymbolPathParser`, the custom symbol paths) — see
  _CanvasNetVegaLite System Verification_
  (`canvas-net-vega-lite.md`)
- **CanvasNetMermaid (System)** — A separate software system providing a Mermaid diagram renderer that parses Mermaid
  text, describes, measures, and renders flowcharts, sequence diagrams, and class diagrams with Mermaid's own layout
  and look, and
  reports the other diagram types as unsupported, containing a single direct unit, `MermaidDocument` (the public parse,
  open, measure, and render API, options, limits, locations, report codes, exceptions, and the image source), and nine
  internal subsystems:
  `Syntax` (units `Preprocessor`, `DiagramDetector`, `YamlSubsetParser`, `SourceScanner`, and `HtmlSanitizer`),
  `Configuration` (units `MermaidConfigResolver`, `ThemeResolver`, `ColorMath`, and `StyleDeclarations`), `Graphlib`
  (units `Graph` and `GraphAlgorithms`), `Dagre` (units `DagreLayout`, `DagreAcyclic`, `DagreRanker`, `DagreNesting`,
  `DagreNormalizer`, `DagreOrdering`, `DagrePositioning`, `DagreCoordinateSystem`, and `LayoutWorkMeter`),
  `Rendering` (units `ClusterLayout`, `ShapeLibrary`, `EdgeBuilder`, `LabelBuilder`, and `NodeImages`), `Scenegraph`
  (units `SceneModel` and `ScenePainter`), `Flowchart` (units `FlowchartParser` and `FlowchartModel`), `Sequence`
  (units `SequenceParser`, `SequenceModel`, `SequenceLayout`, and `SequenceRenderer`), and `Class` (units `ClassParser`
  and `ClassModel`) — see
  _CanvasNetMermaid System Verification_ (`canvas-net-mermaid.md`)
- **CanvasNetViewer (System)** — A separate software system, distributed as the `DemaConsulting.CanvasNet.Viewer`
  package, that lists the image sources of every format, creates the image resolver that lets an SVG document embed
  them, renders any file as a picture, and is the library form of the `canvasnet` tool, containing two direct units,
  `CanvasNetViewer` (with `ViewerRenderOptions` and `ViewerImage`) and `ViewerException` (with `ErrorCodes`), and an
  `Imaging` subsystem with the `SourceLoader`, `ImagePipeline`, `ImageWriter`, `ImageComparison`, `ContactSheet`,
  `GridOverlay`, `ImageRenderer`, `ImageInspector`, `ImageComparer`, and `MontageBuilder` units — see _CanvasNetViewer
  System Verification_ (`canvas-net-viewer.md`)
- **CanvasNetTool (System)** — A separate software system shipped as the `DemaConsulting.CanvasNet.Tool` dotnet
  tool package (command `canvasnet`), containing six direct units, `Program`, `Context`, `ArgumentReader`,
  `ResultWriter`, `Validation`, and `PathHelpers`, and a `Commands` subsystem with the `CommandRegistry`, `HelpTopics`,
  `DiscoveryCommands`, `RenderCommand`, `CompareCommand`, `MontageCommand`, and `InspectCommand` units; the tool is a
  thin shell over the `Imaging` subsystem of the Viewer — see _CanvasNetTool System
  Verification_ (`canvas-net-tool.md`)

The following OTS items are also covered:

- **BuildMark** — build-notes documentation tool
- **FileAssert** — document assertion tool
- **Pandoc** — Markdown-to-HTML conversion tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SonarMark** — SonarCloud quality report tool
- **SysML2Tools** — SysML v2 model-to-documentation tool
- **System.Numerics.Tensors** — vectorized bulk pixel-arithmetic runtime library
- **DemaConsulting.TestResults** — self-validation result recording and TRX/JUnit serialization library of the
  tool package
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework

This verification documentation covers the same software items as the design documentation.

Version applicability: This verification design applies to all versions of CanvasNet.

The following topics are explicitly excluded from this verification documentation:

- Build pipeline and CI/CD process testing
- Infrastructure and hosting environment testing

## Companion Artifact Structure

Each software item covered by this document has corresponding artifacts in parallel directory
trees. In-house items have artifacts in these parallel locations:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

For example, the unit `ScaleResolver` of the `Scales` subsystem of the CanvasNetVegaLite system has the requirements
`docs/reqstream/canvas-net-vega-lite/scales/scale-resolver.yaml`, the design
`docs/design/canvas-net-vega-lite/scales/scale-resolver.md`, this verification design
`docs/verification/canvas-net-vega-lite/scales/scale-resolver.md`, the source
`src/DemaConsulting.CanvasNet.VegaLite/Scales/ScaleResolver.cs`, and the tests
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ScaleResolverTests.cs`. The system-level tests of that system are
in `VegaLiteSystemIntegrationTests.cs` and `VegaLiteChartIntegrationTests.cs` in the root of its test project (with
`VegaLiteAggregationIntegrationTests.cs`, `VegaLiteTimeIntegrationTests.cs`, `VegaLiteLineAreaIntegrationTests.cs`, and
`VegaLiteArcTextIntegrationTests.cs`), and its original-work specification fixtures (including the data-file chart, its
CSV file, and the nested JSON rows), with their provenance statement, are in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Fixtures/`. The file-backed data tests of the `Data` subsystem use the
helpers in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataTestSupport.cs` (per-test temporary directories, an
in-memory file set behind the file callback, and an endless stream); the time units of that subsystem (`TimeCalendar`
and `TimeFormat`) have the verification designs `data/time-calendar.md` and `data/time-format.md` and the tests
`Data/TimeCalendar*Tests.cs`, `Data/TimeUnitSpec*Tests.cs`, `Data/DateTimeObjectTests.cs`, `Data/TimeFormat*Tests.cs`,
and `Data/JsDateParserTests.cs`, which build every time zone with `TimeZoneInfo.CreateCustomTimeZone` in the helper
`Data/TimeTestSupport.cs` and never read the host's zone, clock, or culture. The units of the `Expressions` and
`Transforms` subsystems follow the same pattern, for example the verification design
`docs/verification/canvas-net-vega-lite/expressions/expression-parser.md` and
`docs/verification/canvas-net-vega-lite/transforms/row-transforms.md`, the requirements
`docs/reqstream/canvas-net-vega-lite/expressions/expression-parser.yaml` and
`docs/reqstream/canvas-net-vega-lite/transforms/row-transforms.yaml`, the sources
`src/DemaConsulting.CanvasNet.VegaLite/Expressions/ExpressionParser.cs` and
`src/DemaConsulting.CanvasNet.VegaLite/Transforms/RowTransforms.cs`, and the tests
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionParserTests.cs` and
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/RowTransformsTests.cs`; the units `AggregateTransforms` and
`EncodingPlan` of the same subsystem have the verification designs `transforms/aggregate-transforms.md` and
`transforms/encoding-plan.md` and the tests `Transforms/AggregateTransformsTests.cs` (with its companion test classes
for the measures, the bin algorithm, and the window, stack, impute, and joinaggregate transforms) and
`Transforms/EncodingPlanTests.cs` (with `Transforms/EncodingTransformsTests.cs`); the unit `TimeUnitTransform` has the
verification design `transforms/time-unit-transform.md` and the tests `Transforms/TimeUnitTransformTests.cs` and
`Transforms/TemporalFieldsTests.cs`; the unit `StatTransforms` has the verification design
`transforms/stat-transforms.md` and the tests `Transforms/StatTransforms*Tests.cs` (with the tests of `RegressionFit`,
`CurveSampler`, `LoessFit`, `KernelDensity`, and `StatisticsPrimitives`), and the unit `TimeLabels` of the `Guides`
subsystem has the verification design `guides/time-labels.md` and the tests `Guides/TimeLabelsTests.cs`; the units added
for the line, area, trail, text, and arc marks follow the same pattern: `CurveBuilder` of the `Marks` subsystem has the
verification design `marks/curve-builder.md` and the tests `Marks/CurveBuilderTests.cs`,
`Marks/CurveBuilderSplineTests.cs`, and `Marks/CurveBuilderAreaTests.cs`, `ArcGeometry` of the `Scenegraph` subsystem
has `scenegraph/arc-geometry.md` and `Scenegraph/ArcGeometryTests.cs`, `SymbolPathParser` has
`scenegraph/symbol-path-parser.md` and `Scenegraph/SymbolPathParserTests.cs`, and the existing units `MarkBuilder` and
`ScenePainter` have the new test classes `Marks/MarkBuilder{Path,Order,Overlay,Text,Arc,PolarText}Tests.cs` and
`Scenegraph/ScenePainterPathTests.cs`, with the expected values of all of them derived by hand and the helper
`Marks/PathText.cs`; the expression tests share the helpers in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionTestSupport.cs`, and the composite values of the
expression engine are tested in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataCompositeValueTests.cs`. Only
original fixtures are used: no Vega-Lite example, gallery chart, documentation snippet, or `vega-datasets` data set is
copied.

For example, the unit `DagreRanker` of the `Dagre` subsystem of the CanvasNetMermaid system has the requirements
`docs/reqstream/canvas-net-mermaid/dagre/dagre-ranker.yaml`, the design
`docs/design/canvas-net-mermaid/dagre/dagre-ranker.md`, this verification design
`docs/verification/canvas-net-mermaid/dagre/dagre-ranker.md`, the source
`src/DemaConsulting.CanvasNet.Mermaid/Dagre/DagreRanker.cs`, and the tests
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreRankerTests.cs`. Each subsystem of that system has a
subsystem test class `{Subsystem}Tests.cs` in its test folder; the system tests are `MermaidSystemIntegrationTests.cs`,
`MermaidFlowchartIntegrationTests.cs`, `MermaidReferenceLayoutTests.cs`, `MermaidSequenceIntegrationTests.cs`,
`MermaidSequenceReferenceLayoutTests.cs`, `MermaidClassIntegrationTests.cs`, `MermaidGalleryTests.cs`,
`MermaidHardeningTests.cs`, and `MermaidUnsupportedDiagramTests.cs` in the
root of its test project. Its original flowchart, sequence, and class fixtures are in `Fixtures/flowchart/`,
`Fixtures/sequence/`, and `Fixtures/class/`, the expected results of the pinned upstream packages for original inputs in
`Fixtures/upstream-vectors/`, and the mermaid-cli reference layouts with their provenance README in
`Fixtures/reference/`; they are generated by the developer scripts in `benchmarks/visual-diff/` and never in CI.

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)

Review-sets: defined in `.reviewmark.yaml`

## References

- CanvasNet User Guide — the compiled User Guide document for this repository.
- CanvasNet Repository — the CanvasNet source repository hosted on
  GitHub.
