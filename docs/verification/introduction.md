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
  32-bit RGBA pixel buffer with span-based row access) and the `Rgba32` unit
- **Codecs (Subsystem)** — Image format codecs: `BmpCodec`, `PngCodec`, `TiffCodec`,
  `JpegCodec`, `GifCodec`, and `Jpeg2000Codec`; the first four convert to and from a `Surface`
  pixel buffer, while `GifCodec` and `Jpeg2000Codec` are decode-only loaders
- **Geometry (Subsystem)** — Vector-geometry primitives: `Rect`, `Path`, `BezierFlattening`,
  `SvgArcConverter`, and `CornerRoundEffect`
- **Drawing (Subsystem)** — Vector rasterization and stroke conversion: `PathFiller`,
  `PathStroker`, `GradientPaint`, and `TilePaint`
- **Fonts (Subsystem)** — TrueType (`glyf`-based) SFNT font loading and glyph-outline extraction:
  `TrueTypeFont` and `SystemFontCatalog`
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
  `Data` (units `DataTable`, `DataLoader`, `DataFileResolver`, and `DelimitedTextParser`), `Expressions` (units
  `ExpressionParser`, `ExpressionEvaluator`, and `ExpressionFunctions`, the supported subset of the Vega expression
  language), `Transforms` (units `RowTransforms`, the row transforms `calculate`, `filter`, `fold`, `flatten`, `sample`,
  `pivot`, and `lookup` and the static parameters; `AggregateTransforms`, the grouped transforms `aggregate`,
  `joinaggregate`, `window`, `bin`, `stack`, and `impute`; and `EncodingPlan`, the implicit bin, aggregate, and stack of
  the encoding), `Scales` (units `ScaleModel`, `ScaleResolver`, `Ticks`,
  `NumberFormat`, and `ColorSchemes`), `Guides` (units `TextMeasurer`, `TitleBuilder`, and `AxisBuilder`), `Layout` (unit
  `ViewLayout`), `Marks` (unit `MarkBuilder`), and `Scenegraph` (units `SceneModel`, `ScenePainter`, and `CssColor`) —
  see _CanvasNetVegaLite System Verification_ (`canvas-net-vega-lite.md`)
- **CanvasNetTool (System)** — A separate software system shipped as the `DemaConsulting.CanvasNet.Tool` dotnet
  tool package (command `canvasnet`), containing six direct units, `Program`, `Context`, `ArgumentReader`,
  `ResultWriter`, `Validation`, and `PathHelpers`, a `Commands` subsystem with the `CommandRegistry`,
  `DiscoveryCommands`, `RenderCommand`, `CompareCommand`, `MontageCommand`, and `InspectCommand` units, and an
  `Imaging` subsystem with the `SourceLoader`, `ImagePipeline`, `ImageWriter`, `ImageComparison`, `ContactSheet`,
  `ImageAdjustments`, `ImageGeometry`, `GridOverlay`, and `ImageAnalysis` units — see _CanvasNetTool System
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
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ScaleResolverTests.cs`. The system-level tests of that system are in
`VegaLiteSystemIntegrationTests.cs` and `VegaLiteChartIntegrationTests.cs` in the root of its test project, and its
original-work specification fixtures (including the data-file chart, its CSV file, and the nested JSON rows), with their
provenance statement, are in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Fixtures/`. The file-backed data tests of the `Data` subsystem use the
helpers in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataTestSupport.cs` (per-test temporary directories, an
in-memory file set behind the file callback, and an endless stream). The units of the `Expressions` and `Transforms`
subsystems follow the same pattern, for example the verification design
`docs/verification/canvas-net-vega-lite/expressions/expression-parser.md` and
`docs/verification/canvas-net-vega-lite/transforms/row-transforms.md`, the requirements
`docs/reqstream/canvas-net-vega-lite/expressions/expression-parser.yaml` and
`docs/reqstream/canvas-net-vega-lite/transforms/row-transforms.yaml`, the sources
`src/DemaConsulting.CanvasNet.VegaLite/Expressions/ExpressionParser.cs` and
`src/DemaConsulting.CanvasNet.VegaLite/Transforms/RowTransforms.cs`, and the tests
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionParserTests.cs` and
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/RowTransformsTests.cs`; the units `AggregateTransforms` and
`EncodingPlan` of the same subsystem have the verification designs `transforms/aggregate-transforms.md` and
`transforms/encoding-plan.md` and the tests `Transforms/AggregateTransformsTests.cs` (with its companion test classes for
the measures, the bin algorithm, and the window, stack, impute, and joinaggregate transforms) and
`Transforms/EncodingPlanTests.cs` (with `Transforms/EncodingTransformsTests.cs`); the expression tests share the helpers
in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionTestSupport.cs`, and the composite values of the
expression engine are tested in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataCompositeValueTests.cs`. Only
original fixtures are used: no Vega-Lite example, gallery chart, documentation snippet, or `vega-datasets` data set is
copied.

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)

Review-sets: defined in `.reviewmark.yaml`

## References

- CanvasNet User Guide — the compiled User Guide document for this repository.
- CanvasNet Repository — the CanvasNet source repository hosted on
  GitHub.
