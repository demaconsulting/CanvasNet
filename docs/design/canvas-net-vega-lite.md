# System Design

This document provides the system-level design for CanvasNetVegaLite.

![CanvasNetVegaLite Structure](CanvasNetVegaLiteView.svg)

## Architecture

CanvasNetVegaLite is a static renderer for the public Vega-Lite specification language, delivered incrementally as
its own NuGet package (`DemaConsulting.CanvasNet.VegaLite`, root namespace `DemaConsulting.CanvasNet.VegaLite`),
independent of the other document systems in this repository. It reads Vega-Lite specification text (JSON) and
**parses** it into a validated, immutable document, **describes** the chart without drawing it (schema version,
title, mark types, data references, parameter names), **measures** the size a chart renders at, and **renders** the
chart onto a `Surface` (the core `CanvasNet` pixel buffer) at its natural size multiplied by a scale or at an exact
size. Rendering is static (no interaction), deterministic (pixel-identical surfaces), and uses no network, no
JavaScript runtime, and no code outside the .NET base class library and the core `CanvasNet` package. The package
implements the Vega-Lite 6 defaults and rules; specifications that declare version 5 or 6 in `$schema` are interpreted
with the version 6 defaults. Everything that the renderer recognizes but cannot draw yet is **reported with a stable
`vl-*` identifier and the exact location, never skipped silently** (see _Supported And Deferred Features_).

The first delivery (the first pull request, phases 1 to 3 of the roadmap) renders **a single view with inline data**:
the marks `bar`, `rect`, `point`, `circle`, `square`, `rule`, and `tick`, the channels `x`, `y`, `color`/`fill`/
`stroke`, `size`, and `opacity`, linear, band, and point scales, axes with d3-style tick selection and number
formatting, titles with subtitles, the Vega-Lite default theme and configuration, and a view layout with padding and
`autosize` of `pad` or `none`. Phase 4 adds the **data sources** of that single view: **local CSV, TSV, DSV, and JSON
files** read under the caller's file-access policy (relative to the folder of the specification or extra data
directories, or through a caller callback), **named
datasets** (the top-level `datasets` of the specification and the caller's `Datasets` option), **generated
sequences**, `format.parse` conversions (no data-based inference unless `parse` is `"auto"`, as in Vega), and
`format.property`. Remote URLs are still
never fetched. Phase 5 adds **expressions and row transforms**: a bounded, closed-world subset of the Vega expression
language (57 pure functions, 99 since the date and time functions of phase 8, 100 with the number `format`, and 113 since
the nine distribution functions of phase 14; no
reflection, no clock, no random
source), the **static variable parameters** of the view
(a `value` or an `expr`, replaced by the caller's `Parameters`), and the `calculate`, `filter`, `fold`, `flatten`,
`sample`, `pivot`, and `lookup` transforms, which run between the data loader and the
scales; a sort by the aggregate of a
field and the row-bound `{"expr": ...}` paint values of the marks are evaluated against the transformed rows.
Phase 6 adds **aggregation**: the `aggregate`, `joinaggregate`, `window`, `bin`, `stack`, and `impute` transforms
(every Vega-Lite 6 aggregate operation, with a deterministic bootstrap for `ci0` and `ci1`), and the **implicit pipeline
of the encoding** - the `aggregate`, `bin`, `stack`, and `sort` properties of a channel (a bar chart now stacks by
default,
as Vega-Lite does), the secondary channels `x2` and `y2` of bars, rects, and rules, the stacked, binned, and ranged
geometry of the marks, and the derived titles, tick counts, and domains of binned and stacked axes. Phase 8 adds
**time**: temporal fields and time units on a deterministic **calendar** (the `TimeZone` option, UTC by default; no host
zone, clock, or culture is ever read), `time` and `utc` scales with the d3-time tick intervals and `nice`, axes and
legends of dates (d3-time-format labels, Vega's multi-format, the unit specifiers), the `timeUnit` transform and the
`timeUnit` of a channel (bars and rects that span their unit, points at its start), the date-time objects of
predicates, domains, sorts, and axis values, the `parse` date formats (`date:'%fmt'`), the implicit date parsing of
temporal fields, and the date and time expression functions. Phase 9 adds the **line-like and polar marks**: the `line`,
`area`, `trail`, `text`, and `arc` marks, drawn from the curves of d3-shape (the fifteen `interpolate` values with
`tension`), the point and line overlays of the path marks, the `invalid` data modes, the `order` channel (the order of a
stack, the sort of a path, the draw order of the other marks), the `detail` channel, the `impute` of an encoding and the
implicit impute of a stacked path mark, the polar `theta` and `radius` channels with their scales, stacks, and the
`theta2` and `radius2` ends, pie, donut, and radial charts with the geometry of the d3-shape arc, and labels that are
formatted with the number and time formats and placed by the polar channels. Phase 10 adds **layers and composite
marks**: the `layer` composition (to any depth within `MaxCompositionDepth`) with the inheritance of `data`,
`transform`,
`encoding`, and size, the shared scales over the union of the domains of the layers and the `resolve` of scales, axes,
and
legends (independent scales give two axes, the second on the opposite side), the merged axes and legends with their
titles
joined, a step through layers, and the composite marks `boxplot`, `errorbar`, and `errorband`, which are expanded into
the primitive layers that Vega-Lite normalizes them to; a layered chart with a top-level `transform` is drawn, and its
transform is applied. Phase 11 adds **composition**: the `concat`, `hconcat`, and `vconcat` of any views (with
`columns`,
`spacing`, `align`, `center`, and `bounds`), the `facet` operator and the `row`, `column`, and `facet` channels (cells
for the values of a field, with sort, headers, footers, titles, and shared or independent axes), and `repeat` (a list, a
row and column matrix, and layers, with `{"repeat": ...}` references), each with its `resolve` and with shared legends
merged beside the whole grid, all within `MaxViews` and `MaxCompositionDepth`; and the `autosize` types `fit`, `fit-x`,
and `fit-y` with the `"container"` sizes of a single or layered view. Phase 13 adds the **statistical transforms**
`regression`, `loess`, `density`, `quantile`, and `extent` (with the numerics of the Vega operators, the `extent`
parameter, and the pivot of every aggregate operation), so every transform of Vega-Lite is computed, and the **`image`
mark**
(pictures that the host supplies through `VegaLiteRenderOptions.ImageResolver`, never read by the library itself);
selection
parameters are drawn in their initial state since phase 12; conditional encodings (with a
`test`) and the legends are
drawn since phase 7.

`CanvasNetVegaLite` consists of one direct unit and nine internal subsystems:

- **VegaLiteDocument** (unit; namespace `DemaConsulting.CanvasNet.VegaLite`, folder
  `src/DemaConsulting.CanvasNet.VegaLite/`): the public API. `VegaLiteDocument` offers the entry points
  `Parse(string)`, `Open(string)` (a file path), and `Open(Stream)`, exposes `SourceName`, `SchemaUrl`,
  `SchemaMajorVersion`, `Title`, `MarkTypes`, `DataReferences`, and `ParameterNames`, and offers `Measure` and two
  `Render` overloads. The supporting public types are `VegaLiteOpenOptions`, `VegaLiteRenderOptions`,
  `VegaLiteLimits`, `VegaLiteFontOptions`, `VegaLiteLocation`, `VegaLiteDiagnostic`, `VegaLiteUnsupportedFeature`,
  `VegaLiteSize`, `VegaLiteDataReference`, `VegaLiteDataKind`, `VegaLiteSpecException`, `VegaLiteRenderException`,
  and `VegaLiteUnsupportedFeatureException`, plus the internal `VegaLiteReportSink`. See _VegaLiteDocument Unit
  Design_ (`canvas-net-vega-lite/vega-lite-document.md`).
- **SpecModel** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.SpecModel`, folder `SpecModel/`):
  everything about the specification text, with five units. `SpecParser` reads the UTF-8 JSON into an immutable
  `SpecNode` tree with RFC 6901 pointers and one-based line and column, validates the shape of the known properties
  (including the supported transforms, predicates, parameters, `resolve`, and the composite marks), compiles the
  expressions of the supported sites and the expression references of the static sites,
  and warns about duplicate and unknown keys. `FeatureCatalog` lists every recognized-but-unsupported construct with a
  stable identifier. `ConfigResolver` merges the Vega and Vega-Lite default configuration, the configuration of the
  specification, and the caller's configuration. `SpecNormalizer` reduces the views of a specification to the tree of
  views that are drawn (the layers, concatenations, facets, and repeats with their inherited data, transform, encoding,
  and size, the facet channels of a unit view normalized to a facet, and the repeats expanded into copies) and expands
  the composite marks into layers. `ExpressionReferenceFolder` evaluates, once per call, the `{"expr": ...}` references
  of the static sites with the parameters at their initial state and rewrites the tree so that every later stage reads
  values. See _SpecModel Subsystem Design_
  (`canvas-net-vega-lite/spec-model.md`).
- **Data** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Data`, folder `Data/`): rows into
  memory and the **calendar**, with six units. `DataTable` is the immutable typed value and table model (with the
  array, object, and regular
  expression values that expressions produce). `DataLoader` chooses the source
  of a view (inline `values`, a local file, a named dataset, or a sequence) and builds the table under the data
  limits; it also owns the row budget of the call, which the transforms share, and loads the source of a `lookup`.
  `DataFileResolver` finds and reads local data files under the file-access policy. `DelimitedTextParser` reads CSV,
  TSV, and DSV text and applies the `parse` conversions (and the opt-in type inference, and the date conversions).
  `TimeCalendar` is the calendar: the time zone, instants and their parts, the d3-time intervals, the time units, and
  the
  date-time objects. `TimeFormat` is the d3-time-format formatter and parser with the Vega multi-format, and the
  `Date.parse` rules. The two time units are in `Data` rather than in `Transforms` or `Scales` (deviation P8-D1, see
  _Deviations From Vega-Lite_). See _Data Subsystem Design_
  (`canvas-net-vega-lite/data.md`).
- **Expressions** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Expressions`, folder
  `Expressions/`): the expression engine, with three units. `ExpressionParser` compiles the text of an expression in one
  bounded pass into an immutable postfix program. `ExpressionEvaluator` runs it with JavaScript coercion under a step
  budget. `ExpressionFunctions` is the closed table of 113 pure functions (the date and time functions, the number
  `format`, and the probability distribution functions included, on the
  calendar of the call) with the regular expression translation. See
  _Expressions Subsystem Design_ (`canvas-net-vega-lite/expressions.md`).
- **Transforms** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Transforms`, folder `Transforms/`):
  six
  units. `RowTransforms` computes the static parameter environment and applies the row transforms (`calculate`,
  `filter`, `fold`, `flatten`, `sample`, `pivot` (every aggregate operation), `lookup`, `timeUnit`) to the loaded
  table. `AggregateTransforms`
  computes the
  aggregating transforms (`aggregate`, `joinaggregate`, `window`, `bin`, `stack`, `impute`) and holds the operations,
  the bin algorithm, and the stack rules. `EncodingPlan` plans and runs the implicit bin, time unit, aggregate, and
  stack of a view's
  encoding (and its `impute` and the implicit impute of a stacked path mark, the grouping fields of the paths, and the
  order fields) and names the fields and titles every channel reads; `EncodingTransforms.Facet` orders the cells of a
  facet and splits the rows into one table for each cell. `TimeUnitTransform` adds the start and end fields
  of a
  time unit and collects the temporal fields that the loader date-parses implicitly (`TemporalFields`). `StatTransforms`
  computes the statistical transforms (`regression`, `loess`, `density`, `quantile`, `extent`) with the numerics of the
  Vega operators (the regression models, the curve sampling, the locally weighted regression, and the kernel
  density). See
  _Transforms Subsystem Design_
  (`canvas-net-vega-lite/transforms.md`).
- **Scales** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Scales`, folder `Scales/`): the numeric
  heart, with six units. `ScaleModel` maps values to pixels (linear, time, UTC, band, point, ordinal). `ScaleResolver`
  infers
  scale types, domains, ranges, and paddings from the specification (including the polar `theta` and `radius` scales
  of the arc and text marks) and the order of discrete domains, decides which channels a mark draws, and merges the
  scales
  that the layers of a layered view (or the charts and cells of a composition) share or keeps them apart (`resolve`),
  and sizes the nested offset scales of `xOffset` and `yOffset` inside the band of their position.
  `Ticks` is the
  d3-array tick algorithm and the d3-time calendar ticks and `nice`.
  `NumberFormat` is the d3-format number formatter. `ColorSchemes` holds the 68 Vega color schemes (the `tableau10`
  category palette is the Vega-Lite default) and configures the color range of a scale from a scheme.
  `ColorInterpolator` interpolates colors in sRGB, HSL, CIELAB, and HCL. See
  _Scales Subsystem Design_ (`canvas-net-vega-lite/scales.md`).
- **Guides** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Guides`, folder `Guides/`): text and
  guide construction, with seven units. `TextMeasurer` resolves fonts and measures and truncates text. `TitleBuilder`
  builds the title and subtitle and the header labels and titles of a facet (`HeaderSettings` reads their properties).
  `AxisBuilder` builds axes (grid, ticks, labels, domain, title) and their extents,
  including time axes. `LabelOverlap` removes overlapping labels (parity and greedy) for the axes and the legends.
  `LegendBuilder` plans and
  builds the symbol, gradient, and discrete gradient legends, including the legends of dates. `TimeLabels` resolves the
  label format of dates and the time tick requests that axes and legends share. `LabelExpressions` evaluates the
  `labelExpr` of axes, legends, and facet headers on the shared expression evaluator of the call. See
  _Guides Subsystem Design_ (`canvas-net-vega-lite/guides.md`).
- **Layout** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Layout`, folder `Layout/`): three units.
  `ViewLayout` computes the data rectangle, the padding, the autosize canvas (`pad`, `none`, and the fitting types), the
  title placement, and the view background and border of a single chart (a single view, or a layered view, which is
  one view with one data rectangle; also the cell of a composition, whose bounds it reports).
  `LegendLayout` places the legends around the data rectangle (or around a whole composition) and
  adds their room to the canvas. `CompositionLayout` places the charts of a concatenation, facet, or repeat in a grid
  (the Vega grid layout) with the headers, footers, and titles of a facet. See _Layout Subsystem Design_
  (`canvas-net-vega-lite/layout.md`).
- **Marks** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Marks`, folder `Marks/`): two units.
  `MarkBuilder` turns rows, scales, and mark properties into scene items (the bars, rects, symbols, rules, ticks,
  lines, areas, trails, text, and arcs, the overlays, the invalid data modes, and the draw order; it is one class in
  several partial files). `CurveBuilder` is the d3-shape curve and line and area generator that turns the defined
  points of a path into path segments. See _Marks Subsystem Design_ (`canvas-net-vega-lite/marks.md`).
- **Scenegraph** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Scenegraph`, folder
  `Scenegraph/`): the assertion surface and the painter, with five units. `SceneModel` is the immutable scene
  (groups, rectangles, rules, paths with line and cubic segments, symbols, text, arcs, trails, bounds, and the item
  budget). `ScenePainter` draws a scene onto a `Surface` (in several partial files). `ArcGeometry` is the d3-shape arc
  and the d3-path emulation that turn an arc item into a core `Path`. `CssPaint` resolves a chart paint and an opacity
  to a color on top of the core `CssColor` parser. `SymbolPathParser` parses the SVG path data of a custom symbol shape
  and draws it scaled by the symbol radius, with its bounds. See _Scenegraph
  Subsystem Design_ (`canvas-net-vega-lite/scenegraph.md`).

The names of the subsystems in this document are the namespace and folder names; in the SysML2 model their `part def`
names carry the prefix `VegaLite` (`VegaLiteSpecModel`, `VegaLiteData`, `VegaLiteExpressions`, `VegaLiteTransforms`,
`VegaLiteScales`, `VegaLiteGuides`,
`VegaLiteLayout`, `VegaLiteMarks`, and `VegaLiteScenegraph`), and the `SceneModel` unit is `VegaLiteSceneModel`,
because the `CanvasNet` model package is shared by every system of the repository and `SceneModel` and `SceneGraph`
already name items of `CanvasNetRender3D`.

Dependencies inside the system are one-directional at the unit level, apart from three shared static rules.
`VegaLiteDocument` calls every subsystem. `SpecParser` and `FeatureCatalog` use two stateless rules of the data units
so that the text of a specification, its report, and its loading agree by construction: the validation of a
delimiter (`DelimitedTextParser.IsValidDelimiter`) and the classification of a `format.parse` entry
(`ParseDirectives.TryClassify`, in the `DelimitedTextParser` unit), and `DataLoader` uses the one definition of a
remote URL (`FeatureCatalog.IsRemoteDataUrl`, with `FeatureCatalog.LocalPathOfFileUri` for a local `file:` URI).
`FeatureCatalog` and `ConfigResolver` read the `SpecTree` and
`SpecNode` types of `SpecParser`. `SpecNormalizer` reads the same types, the `ResolvedConfig` (for the composite mark
defaults), and `ExpressionParser` (it compiles the expressions that an expansion writes), and its `LayerTree` is read by
`FeatureCatalog` (the merged encoding of each layer), `VegaLiteDocument` (the data walk, the axes, and the legends), and
`ScaleResolver.ResolveLayers` (the unit view and the nested `resolve` of each layer). `DataLoader` reads `SpecNode`,
asks `DataFileResolver` for the text of a data file,
hands delimited text to `DelimitedTextParser`, and builds `DataTable`; `DataFileResolver` depends only on the core
`FileReferenceResolver` and `FileAccessPolicy` (see _FileReferenceResolver Unit Design_,
`canvas-net/files/file-reference-resolver.md`)
and the public
option, limit, and exception types, and `DelimitedTextParser` on those and the value type of `DataTable` (and the
`SpecNode` of the `format` object), never on `DataLoader`. `ScaleResolver` reads the `SpecNode` tree, the
`DataTable`, and the `ResolvedConfig`, and builds `ScaleModel` instances, using `Ticks` and `ColorSchemes`;
`NumberFormat` and `Ticks` depend on nothing else in the system. The `Expressions` subsystem sits above the value model
only: `ExpressionParser` depends on `ExpressionFunctions` (to decide whether a call is supported) and on `DataValue`,
`ExpressionEvaluator` on the compiled program, `ExpressionFunctions`, and the value model, and `ExpressionFunctions` on
`DataValue` and the coercion helpers of the evaluator; none of them refers to the other subsystems. `SpecParser` calls
`ExpressionParser` to compile every expression of a supported site when a document is opened, and `FeatureCatalog` reads
the constructs that the compiled expressions collected. `RowTransforms` reads the `SpecTree` and its compiled
expressions, evaluates them with the `ExpressionEvaluator`, and builds `DataTable` instances through the `DataLoader` of
the call (which owns the row budget and loads the sources of a lookup), and hands the aggregating transforms to
`AggregateTransforms`, which builds its tables the same way and charges its work to the step budget of the evaluator.
`EncodingPlan` reads the `SpecNode` tree and the `ResolvedConfig` (and `ScaleResolver.InferOrient`, `StackRules`) and
`EncodingTransforms` runs it with the engines of `AggregateTransforms`; `ScaleResolver` then reads the plan and the Raw
and
Main tables (and uses the accumulators of `AggregateTransforms` to sort a discrete domain by an aggregate), and
`MarkBuilder` reads the Main table and the scales and evaluates the row-bound paint expressions with the same evaluator.
`SpecParser` and `FeatureCatalog` read the static operation registry (`AggregateMeasures`) and `StackRules` of the
`Transforms` subsystem, so that validation, reports, and execution agree; `Scales` and `Transforms` therefore refer to
each
other at the unit level (`EncodingPlan` uses `ScaleResolver.InferOrient`; `ScaleResolver` uses `EncodingPlan`), as the
orientation rule is one function used by both. `Expressions` never
refers to `Transforms`, and `Data` never refers to `Expressions` or `Transforms`. The **calendar** units sit at the
bottom of the `Data` subsystem: `TimeCalendar` (with `TimeInterval`, `TimeUnitSpec`, and `DateTimeObject`) and
`TimeFormat`
(with `JsDateParser`) depend only on the value range of `DataValue` and on each other (`TimeFormat` uses
`TimeCalendar`), and everything that reads or writes a date uses them: `DelimitedTextParser` (`parse` dates and the
implicit date parsing, through the calendar of `DataLoader`), `ExpressionFunctions` (the date functions, through
`ExpressionFunctionContext.Calendar`), `SpecParser` and `FeatureCatalog` (the validation of time units and date-time
objects, which must agree with execution), `TimeUnitTransform`, `EncodingPlan`, and `PredicateSource` (the time-aware
predicates are written as expressions), `ScaleModel`, `Ticks`, and `ScaleResolver` (time scales, ticks, and `nice`),
`TimeLabels`, `AxisBuilder`, and `LegendBuilder` (the labels of dates), and `MarkBuilder` (the `datum` of a temporal
channel). This placement is why `Expressions` and `Data` need no dependency on `Scales` or `Transforms` for dates
(deviation P8-D1). `VegaLiteDocument` creates the one `TimeCalendar` of a call from `VegaLiteRenderOptions.TimeZone`
and hands it to the loader and the evaluator; the scales read it from the evaluation facts of the encoding data, and
the axes and legends from the `ResolvedScale`. `SceneModel` depends on nothing else.
`TextMeasurer` reads the `SceneModel` text and bounds types; `ScenePainter` calls `TextMeasurer` and `CssPaint`.
`TitleBuilder`, `AxisBuilder`, and `LegendBuilder` build `SceneModel` items using `TextMeasurer`; `AxisBuilder` and
`LegendBuilder` share `LabelOverlap`, `AxisBuilder` uses `ScaleModel`, `Ticks`, and `NumberFormat`, and `LegendBuilder`
uses `ResolvedScale`, `ScaleModel`, `Ticks`, and `NumberFormat`. `ViewLayout` builds the root of the scene around the
title block and the content that the builders produced; `LegendLayout` places the built legends of `LegendBuilder` (it
calls the grid layout of `LegendBuilder`) against the bounds of the axes and tells `ViewLayout` how far they reach.
`MarkBuilder` builds `SceneModel` items from the `DataTable` and the scales (the segment lists of the path marks
through `CurveBuilder`, which reads only the segment type of `SceneModel`; `MarkBuilder` also asks
`ScaleResolver.ChannelIsDrawn` which channels a mark draws, the one rule that the catalog uses too). `ScenePainter`
calls `ArcGeometry` for the arc and trail items (`ArcGeometry`, with its internal `ArcPathWriter`, depends only on
the core `Path` and `PathBuilder`). The
`Guides` and `Scenegraph` subsystems therefore refer to each other (the guide builders produce scene items; the painter
needs the measurer), but no unit refers back along the same path: `SceneModel` and `SceneBounds` are leaves,
`TextMeasurer` reads them, and `ScenePainter` reads `TextMeasurer`. Outside the system, the `Scenegraph` and `Guides`
subsystems and the public `Render` methods use the core `CanvasNet` (see _Dependencies_).

## External Interfaces

The system exposes the following public API in the `DemaConsulting.CanvasNet.VegaLite` namespace:

- **`VegaLiteDocument`** (sealed, immutable, thread-safe, not `IDisposable`): the static
  `Parse(string, VegaLiteOpenOptions?)`, `Open(string, VegaLiteOpenOptions?)`, and
  `Open(Stream, VegaLiteOpenOptions?)` entry points; the `SourceName`, `SchemaUrl`, `SchemaMajorVersion`, `Title`,
  `MarkTypes`, `DataReferences`, and `ParameterNames` properties;
  `Measure(VegaLiteRenderOptions?, CancellationToken)`, which returns a `VegaLiteSize`;
  `Render(VegaLiteRenderOptions?, CancellationToken)`, which returns a new `Surface` at the natural size times the
  scale; and `Render(int width, int height, VegaLiteRenderOptions?, CancellationToken)`, which returns a new
  `Surface` of exactly that size with the chart scaled uniformly to fit and centered. The caller owns and disposes
  every returned `Surface`.
- **`VegaLiteOpenOptions`** (sealed, init-only, validated): `BaseDirectory`, `DataPaths`, `FileResolver`,
  `SourceName`, `Limits`, and `FileAccess`. Parsing consumes `SourceName` and `Limits`; `BaseDirectory` (the folder of
  a file opened by path unless set), `DataPaths` (extra data directories, searched in order after the base directory),
  and `FileResolver` (a callback that supplies the bytes of a normalized logical path and replaces the file system
  entirely) say where `Measure` and `Render` look for a local `data.url` (search locations, not boundaries), and
  `FileAccess` (a `FileAccessPolicy`, `FileAccessPolicy.AllowAll` by default, `null` refused; kept by
  `WithEffective`; not consulted with a `FileResolver`) decides which of those files may be read.
- **`VegaLiteRenderOptions`** (sealed, init-only, validated): `Scale` (finite, greater than zero, at most
  `Surface.MaxDimension`), `Background` (an `Rgba32` that replaces the specification and configuration background),
  `Config` (a Vega-Lite configuration as JSON object text, merged over the defaults and the specification's
  `config`), `Parameters` (copied and validated; the value of a variable parameter of the view, which replaces its
  `value`
  or `expr`) and `Datasets` (named row tables
  that replace a top-level dataset of the same name; copied and validated),
  `TimeZone` (`null` is UTC; the only zone the renderer uses, for dates without an offset, `date:` formats, local time
  units, time scales, ticks and labels, and the local date functions; never the host's zone), `ContainerWidth` and
  `ContainerHeight` (the size of a
  `"container"` chart; 400 by 300 by default), `Fonts`, `Limits`, the handlers `WarningHandler` and
  `UnsupportedFeatureHandler`, and `ThrowOnUnsupportedFeature`.
- **`VegaLiteFontOptions`** (sealed, immutable): `UseSystemFonts` (whether installed fonts are searched for an
  explicitly named family; `false` by default) and `AdditionalDirectories` (extra directories scanned first; empty by
  default, copied and validated when set).
- **`VegaLiteLimits`** (sealed, init-only): the eighteen resource limits listed in _Risk Control Measures_, with the
  `Default` instance.
- **`VegaLiteLocation`** (readonly record struct): `File`, RFC 6901 `Pointer`, one-based `Line`, and one-based
  `Column`; `default` (line zero, empty pointer) means "unknown".
- **`VegaLiteDiagnostic`** and **`VegaLiteUnsupportedFeature`** (sealed records): a warning (message starting with
  its stable `vl-*` identifier and a colon) and an unsupported-feature report (stable `Feature` identifier), each with
  its location.
- **`VegaLiteSize`** (readonly record struct): `Width` and `Height` in pixels.
- **`VegaLiteDataReference`** (sealed record) and **`VegaLiteDataKind`** (`Inline`, `Url`, `Name`, `Sequence`): a data
  source as written, before any loading.
- **`VegaLiteSpecException`** (sealed, derives from `FormatException`), **`VegaLiteRenderException`** (sealed,
  derives from `InvalidOperationException`), and **`VegaLiteUnsupportedFeatureException`** (sealed, derives from
  `IOException`): each carries a `Location` (the last also the `Feature`).

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `VegaLiteDocument.Parse(string, ...)` | Inbound | Vega-Lite specification text (JSON) | Non-null; at most 4,194,304 UTF-16 characters by default; the root must be a JSON object; a leading U+FEFF is ignored; plain Vega (`$schema` of `vega.github.io/schema/vega/`) is rejected |
| `VegaLiteDocument.Open(string, ...)` | Inbound | File path | Non-null and not empty or whitespace; read as UTF-8 with byte-order-mark detection; a missing file or directory surfaces the natural .NET exception; the source name defaults to the path |
| `VegaLiteDocument.Open(Stream, ...)` | Inbound | Readable stream | Non-null and readable; read from the current position as UTF-8; left open; reading stops one 8 KiB chunk past the size limit |
| `VegaLiteOpenOptions` and `VegaLiteRenderOptions` properties | Inbound | Text, numbers, lists, dictionaries, delegates, `Rgba32` | Each value is validated when initialized, so an instance is always valid and immutable; collections are copied and exposed read-only |
| `VegaLiteOpenOptions.FileResolver` | Outbound (a callback the library invokes) and inbound (the stream it returns) | `Func<string, Stream?>`: a normalized logical path in, a readable UTF-8 stream or `null` out | Called only by `Measure` and `Render`, on the calling thread, at most once per candidate path per call, never for a remote URL; the library disposes the stream and reads at most `MaxDataBytes`; the file system is never touched while it is set |
| Local data files (`data.url`) | Inbound | UTF-8 CSV, TSV, DSV, or JSON text in a local file (relative to the base directory or a data path, absolute, or a `file:` URI without a host) | Read under `VegaLiteOpenOptions.FileAccess` (every regular local file by default; a restrictive policy is judged on the real path of the open file); network, device, and alternate-data-stream paths refused under every policy; at most `MaxDataFiles` files and `MaxDataBytes` bytes each; read only by `Measure` and `Render`; never from the network |
| `VegaLiteDocument` properties | Outbound | Text, numbers, read-only lists | Describe the text only: no data is loaded, no file is opened, no transform or parameter is evaluated (the expressions are compiled and checked) |
| `VegaLiteDocument.Measure(...)` | Inbound and outbound | `VegaLiteRenderOptions`, a `CancellationToken`; a `VegaLiteSize` | Same checks, reports, and order as `Render`, nothing is drawn; always equals the size `Render` produces |
| `VegaLiteDocument.Render(...)` (natural size) | Inbound and outbound | `VegaLiteRenderOptions`, a `CancellationToken`; a `Surface` | Natural size times `Scale`, rounded half away from zero, each side from 1 to `Surface.MaxDimension`, validated before any report is delivered; caller owns the surface; pixel-identical output for identical input |
| `VegaLiteDocument.Render(int, int, ...)` (exact size) | Inbound and outbound | Width, height, options, a token; a `Surface` | Width and height from 1 to `Surface.MaxDimension`, validated first; the natural chart is scaled uniformly to fit and centered, margins take the background; `Scale` is not used |
| `VegaLiteRenderOptions.WarningHandler`, `UnsupportedFeatureHandler` | Outbound | Delegates receiving `VegaLiteDiagnostic` and `VegaLiteUnsupportedFeature` | Called once per distinct report, in document order, on the calling thread, after the work finished and before the method returns or throws |
| `VegaLiteSpecException` | Outbound | Exception with `Location` | Thrown for malformed JSON, nesting deeper than `MaxJsonDepth`, a known property of the wrong type or value, a string longer than `MaxStringLength`, plain Vega, an invalid expression (a syntax error, a text longer than `MaxExpressionLength`, nesting deeper than `MaxExpressionDepth`, or a reference to an undeclared parameter; the message names the expression offset), a malformed transform, predicate, or parameter (a cycle, a repeated or reserved name), an unknown time unit or a malformed time-unit object, a date-time object with an unknown month or day name or a member of the wrong type, a `tickCount` or `nice` interval with an unknown name or a non-positive step, a `date:` or `utc:` parse entry without a quoted format, and, at render time, an invalid axis orientation, axis format, or scale domain or range (a temporal domain entry that is not a date) |
| `VegaLiteRenderException` | Outbound | Exception with `Location` | Thrown when a data, transform, expression, scale, axis, or scene limit is exceeded (`MaxDataRows`, `MaxFieldCount`, `MaxStringLength`, `MaxEvaluationSteps`, `MaxDataFiles`, `MaxDataBytes`, `MaxScaleDomainSize`, `MaxTicks`, `MaxSceneItems`) and when local data cannot be used: no file source for a local `url`, a path that is never allowed, a file the file-access policy denies (the core `FileReferenceException` is the inner exception), a missing or unreadable file, invalid UTF-8, invalid JSON or delimited text, an undefined named dataset, or an invalid sequence; also for an invalid or too slow regular expression of an expression; the location is the `url`, `name`, `sequence`, transform, or expression that caused it |
| `VegaLiteUnsupportedFeatureException` | Outbound | Exception with `Feature` and `Location` | Thrown only when `ThrowOnUnsupportedFeature` is set, at the first unsupported feature in document order; the report is delivered to the handler first |
| `ArgumentOutOfRangeException`, `ArgumentException` | Outbound | Exception | Thrown for an invalid option value when it is set, for a scaled or explicit size outside 1 to `Surface.MaxDimension`, and for a `Parameters` name the document does not declare |
| `OperationCanceledException` | Outbound | Exception | Thrown, never wrapped, when the token is canceled before or during the work |
| `InvalidDataException` | Outbound | Exception | Thrown, not as a `VegaLiteSpecException`, when the source exceeds `MaxSourceLength` (no location is meaningful) |
<!-- markdownlint-enable MD013 -->

Opening a document never loads data, never opens another file, never evaluates anything, and never uses the network (it
compiles expressions and checks their shape); only `Measure` and
`Render` read data, and only under the file-access policy described in _Risk Control Measures_. The types of the
subsystems are internal and visible only to the test assembly through `InternalsVisibleTo`. See the unit and
subsystem design documents for the complete constructor, property, and exception detail of every type.

## Dependencies

`CanvasNetVegaLite` references the separate `CanvasNet` system through a project reference in
`src/DemaConsulting.CanvasNet.VegaLite/DemaConsulting.CanvasNet.VegaLite.csproj`:

- `CanvasNet` (its own package, `DemaConsulting.CanvasNet`), through its public API only: the **`Canvas`** subsystem
  (`Surface`, the result of `Render`, and `Rgba32`, the color of the options and of every painted pixel); the
  **`Geometry`** subsystem (`Path` and `PathBuilder`, for the symbol shapes and the rectangles and strokes that are
  filled); the **`Drawing`** subsystem (`PathFiller` and `PathStroker`, which fill and stroke every shape); the
  **`Fonts`** subsystem (`TrueTypeFont`, `SystemFontCatalog`, and the bundled Liberation fonts, for font resolution);
  the **`Rendering`** subsystem (`TextRenderer`, for text measurement and drawing); and the **`Files`** subsystem
  (`FileReferenceResolver` with `FileReferenceOptions`, `FileReference`, `FileReferenceException`, and
  `FileAccessPolicy`, which the `DataFileResolver`
  unit uses for all local data-file access)

The dependency is declared as the package dependency of `DemaConsulting.CanvasNet.VegaLite`; the public API exposes
the core `Surface` and `Rgba32`. The dependency is modeled in the SysML2 model as the `usesCanvas`, `usesDrawing`,
`usesFiles`, `usesFonts`, `usesGeometry`, and `usesRendering` dependencies of the `CanvasNetVegaLite` system (the model
records dependencies at the system level only, as for every other satellite system). No `InternalsVisibleTo` entry was
added for the system: the `Files` subsystem is public core API.

**`CanvasNetVegaLite` must never reference `DemaConsulting.CanvasNet.Charts`, `DemaConsulting.CanvasNet.Svg`,
`DemaConsulting.CanvasNet.Pdf`, `DemaConsulting.CanvasNet.Pptx`, `DemaConsulting.CanvasNet.Vsdx`,
`DemaConsulting.CanvasNet.Render3D`, or `DemaConsulting.CanvasNet.Scad`.** It is a chart-specification front end
unrelated to those systems, and the consumers that host it (such as `CanvasNetTool`) depend on it, never the reverse.

Beyond the project reference, `CanvasNetVegaLite` uses only the .NET base class library: `System.Text.Json`
(`Utf8JsonReader`, for the specification parser, and `JsonDocument`, for checking a configuration text),
`System.Text.RegularExpressions` (a generated regular expression for the d3 format specifier, and the safe .NET
translation
of the regular expressions of an expression),
`System.Numerics` (`BigInteger`, for exact decimal expansion in number formatting, and `Matrix3x2` in the painter),
`System.IO`, `System.Text`, `System.Globalization` (invariant-culture number parsing and formatting), and
`System.Collections.ObjectModel`. These are platform types available on every target framework, not OTS Software
Items, and require no runtime NuGet dependency. The d3 and Vega algorithms and default values that the package uses
are **local ported source with attribution**, not a consumed third-party component (see _Third-Party Provenance And
Attribution_), so the system introduces no OTS Software Item and no Shared Package (OTS: N/A - no OTS items).

This is an ordinary, same-repository, system-to-system dependency: it is neither an OTS Software Item nor a Shared
Package (that category is scoped to a package produced by a _different_ repository).

**Image sources.** The package also ships `VegaLiteImageSource`, an implementation of the core `ImageSources`
subsystem's `IImageSource` for `.vl` and `.vl.json` files, so that a document can link to a chart through the shared
`FolderImageResolver` (see _ImageSources Subsystem Design_, `canvas-net/image-sources.md`). It uses only the public
contract types of the core package, so the package still references no other format package; the separate
`CanvasNetViewer` system references this package one-way, to list the source with the other built-in ones, and nothing
in this package refers to it.

## Risk Control Measures

`CanvasNetVegaLite` has no segregation requirement for safety. Because a Vega-Lite specification and its data may come
from an untrusted origin, its risk control measures are defensive-correctness measures against invalid and hostile
input:

- **Fail-closed validation.** Every entry point and every options property validates its argument eagerly and throws
  a specific exception (`ArgumentNullException`, `ArgumentException`, `ArgumentOutOfRangeException`) rather than
  coercing it; options and limits are immutable once constructed, so the pipeline trusts every instance it receives.
- **Fail-fast specification errors.** The first malformed construct aborts parsing with one `VegaLiteSpecException`
  carrying its file, JSON pointer, line, and column. A known property of the wrong type or value is an error, because
  rendering would be a guess, and so is an expression that cannot be compiled, a transform or predicate of the wrong
  shape, and a parameter cycle or an undeclared parameter reference (the message adds the offset inside the expression);
  an unknown property is only a `vl-unknown-property` warning, because it may come from a
  newer version. There is no error recovery and no partial specification.
- **Bounded recursion and memory while reading.** The parser is one forward pass over the bytes with an explicit
  stack of frames indexed by depth, so hostile nesting cannot overflow the call stack. Nesting beyond `MaxJsonDepth`
  (64 by default, at most 256) and strings longer than `MaxStringLength` are specification errors; source longer than
  `MaxSourceLength` (4,194,304 characters by default) is an `InvalidDataException`. `Open(Stream)` and `Open(string)`
  read in chunks and stop one chunk past the limit, so a stream that never ends cannot consume unbounded memory.
  Inline data rows are not walked while parsing, so the cost of opening does not depend on the data volume.
- **Resource limits, fail closed.** The limits below bound every resource a render can consume. Each is validated when
  set, enforced at its exact value by the stage that spends the resource, and reported with the location of the
  construct that exceeded it.

  <!-- markdownlint-disable MD013 -->
  | Limit | Default | Enforced by | Effect when exceeded |
  | ----- | ------- | ----------- | -------------------- |
  | `MaxSourceLength` | 4,194,304 characters | `VegaLiteDocument`, `SpecParser` | `InvalidDataException` |
  | `MaxJsonDepth` | 64 (1 to 256) | `SpecParser` | `VegaLiteSpecException` at the crossing |
  | `MaxStringLength` | 1,048,576 characters | `SpecParser`, `ExpressionEvaluator`, `RowTransforms`, `AggregateTransforms`, `TimeFormat`, `TimeUnitTransform`, `AxisBuilder`, `LegendBuilder`, `MarkBuilder` | `VegaLiteSpecException` (specification text); `VegaLiteRenderException` for a string an expression or a transform would produce (also a group value, a `distinct` key, a name or value that a transform copies, a formatted date or an axis or legend label of a date: the formatter stops as soon as it would write more) and for the label of a text mark: a single label above the limit, or all the labels of one text mark above sixteen times the limit |
  | `MaxDataRows` | 1,000,000 (total per call) | `DataTable`, `DataLoader`, `DelimitedTextParser`, `RowTransforms`, `AggregateTransforms`, `EncodingTransforms` | `VegaLiteRenderException` before any row is converted or allocated (also for a sequence, a lookup source, the output of a fold or flatten, the groups of an aggregate, the rows of an impute, and the `keyvals` of an impute) |
  | `MaxFieldCount` | 1,024 | `DataTable`, `DataLoader`, `DelimitedTextParser`, `RowTransforms`, `AggregateTransforms` | `VegaLiteRenderException` (also for the fields a transform adds) |
  | `MaxDataFiles` | 64 (distinct files per call) | `DataFileResolver` | `VegaLiteRenderException`; the file that would exceed the limit is refused before it is opened or requested |
  | `MaxDataBytes` | 64 MiB (per file) | `DataFileResolver` | `VegaLiteRenderException`; reading stops one chunk past the limit, so an endless stream cannot exhaust memory |
  | `MaxScaleDomainSize` | 100,000 values | `ScaleResolver`, `BinAlgorithm`, `TimeUnitTransform`, `ExpressionFunctions` | `VegaLiteRenderException` (also for the number of bins of a `bin`, the categories of a sort by an aggregate, the explicit domain and sort arrays of a temporal field, the `maxbins` of a time unit, and the elements of a `timeSequence` or `utcSequence`) |
  | `MaxTicks` | 1,000 per axis | `Ticks`, `AxisBuilder`, `LegendBuilder` | `VegaLiteRenderException` (also for categories, and for the ticks of a time axis or legend: the interval range stops at the first tick beyond the limit, before any label is made) |
  | `MaxSceneItems` | 2,000,000 | `MarkBuilder`, `SceneBudget`, `VegaLiteDocument` | `VegaLiteRenderException`, counted as each item is created, before it is kept; a line or area is charged one item plus an upper bound of its path segments, and a trail one item plus its points, **before** the segment list or the item exists; every overlay point, every arc, and every line of a text label is one item, charged before it is created |
  | `MaxDiagnostics` | 1,000 | `VegaLiteReportSink` | Further reports collapse into one `vl-diagnostics-suppressed` notice; strict mode is unaffected |
  | `MaxPaintedPixels` | 250,000,000 (device pixels per `Render`) | `ScenePainter` (`ScenePainter.Budget.cs`), `SceneImageSource` | `VegaLiteRenderException` that names `VegaLiteLimits.MaxPaintedPixels`, located at the item or the mark that owns it; the box of every fill and stroke, estimated text, picture, clip layer, and trail layer is charged **before** the item is painted (see _Pixel budget_); `Measure` paints nothing |
  | `MaxExpressionLength` | 4,096 characters | `ExpressionParser` (when the document is opened) | `VegaLiteSpecException` located at the expression, offset in the message; checked before tokenizing; applies to the expressions that a user writes (also an `{"expr": ...}` operand of a predicate), not to the text that the renderer writes from a predicate or a composite mark, which is bound by eight times `MaxSourceLength` (`ParseGenerated`; a predicate's text is counted as it is written and refused before it is allocated) |
  | `MaxExpressionDepth` | 64 (1 to 256) | `ExpressionParser` (when the document is opened) | `VegaLiteSpecException`; counts nesting (groups, brackets, arguments, literals, branches, unary operands), not length |
  | `MaxEvaluationSteps` | 50,000,000 (total per call) | `ExpressionEvaluator`, `ExpressionFunctionContext`, `WorkMeter` | `VegaLiteRenderException` located at the expression or the transform; one step per executed instruction plus one per 64 characters or elements of function work, shared by the parameters, the transforms (also the comparisons of a filter predicate and the rows, sorts, window frames, and bootstrap resamples of the aggregating transforms, and the rows of a `timeUnit` transform), the implicit pipeline, the sort of a scale, the label text of the legends, the date functions (one step per element of a sequence, and the lengths of a format specifier, its output, and a parsed text), and the mark values of the call (also the grouping, the sorts, the points, and the segment bounds of the path marks, the characters and lines of the labels of a text mark, and the slices of an arc mark, and the rows of an encoding or stack impute; the work of a transform is counted in work elements weighted to about three nanoseconds each, see _Calibrated work budget_) |
  | `MaxViews`, `MaxCompositionDepth` | 1,000, 16 (1 to 64) | `SpecNormalizer` (when the document renders, before any data is read), `EncodingTransforms.Facet` (the cells of a facet, before the partition is allocated) | `VegaLiteRenderException` located at the offending view (`MaxViews`: every view counts, the layers, the children of a concatenation, the copies of a repeat (computed before they are made), the cells of a facet (cells times the views of one cell), and the views that a composite mark expands to) or container (`MaxCompositionDepth`: the nesting of `layer`, `concat`, `hconcat`, `vconcat`, `facet`, and `repeat`, the outermost one being level one; a chart and the views of an expansion are not levels) |
  <!-- markdownlint-enable MD013 -->

- **Data loaded before the scene budget.** The data stage runs before any scene item is charged and before any scale
  exists, so a data failure or a data limit never depends on `MaxSceneItems`; the marks that the loaded rows produce
  are then charged to the scene budget as before.
- **Pixel budget.** The size of the surface is bounded by `Surface.MaxDimension` on each side: the scale and the
  explicit width and height are validated before any pixel memory is allocated, and a clip layer in the painter is only
  as large as the clip's pixel box. What the painter paints is bounded by `MaxPaintedPixels` (phase 14), because
  `MaxSceneItems` counts items, not the area they cover (measured on an 800 by 600 chart before the limit existed: a
  full-surface rectangle took 6.7 milliseconds, a symbol of a million square pixels 25, a stroke 3,000 pixels wide 30,
  and an `image` mark without a size drew a 256 by 256 picture for every row, 87 seconds for 20,000 rows). The painter
  charges one **paint meter** (one per `Paint` call, carried in its context) before the work of every item: the bounding
  box of a path in the item's units (`Path.GetBounds`: a curve lies inside the hull of its control points, and an arc is
  converted to the curves of its ellipse with the radii corrected as SVG does, so the whole arc is inside, not only its
  end point) mapped to surface pixels, widened to whole pixels, cut to the target (the surface, or the clip layer that
  the item is drawn on), once for a visible fill and once for a visible stroke, the latter grown by half the stroke
  width; for text, one estimated box for each non-empty line where the painter places it (the characters times 0.6 of
  the font size, at most the `limit` of the text, by a font size and a third, from the baseline plus the line number
  times the line height, aligned as the text is; a rotated item is one square around its anchor), cut to the target, so
  that text that is truncated, off the target or not drawn (no visible fill, a font size that is not positive) is
  charged only for what the painter draws, and a zero or negative line height still charges every line (measuring the
  text would be painting work); the
  box of a picture and the pixels of each resampled copy that is made; the pixels of a clip layer and of the layers of a
  translucent trail, whose fill chunks are then charged in the coordinates of the layer like its stroke chunks. An item
  outside the target costs nothing, an exact total at the limit is accepted, a box that cannot
  be computed is the whole target, and the failure is raised before the item is painted. It is located at the item's
  `SceneItem.Source` or, when it has none, at the nearest enclosing group that has one: the `marks` group of a mark
  carries the location of the mark. At about 40 nanoseconds a pixel in the worst measured case (a circle with a stroke
  thousands of pixels wide) the default stops the slowest painting in about ten seconds, and a realistic chart (a few
  full-image layers) uses a small share of it. The nearest-neighbor copy and the opacity copy of a picture are made once
  for the items that draw the same picture at the same size and opacity (`SceneImageSource.GetTile`), within a bound of
  8,388,608 kept pixels; a copy that does not fit is made for one item and disposed.
- **Finite arithmetic and bounded stroking.** A number of the specification is any finite `double`, but the geometry is
  `float` and the layout is built from sums and spans that can overflow. The tick generators (numeric and time) return
  no
  ticks for a span that overflows the double range or has an infinite end and drop any generated tick that is not
  finite, so a NaN
  never reaches the layout; **every instant is bounded to the years 1 to 9999**: the calendar returns NaN (a null date)
  for any result outside the range of a `DataValue` date, for a non-finite input, and for a failed parse, its civil
  arithmetic is integer based (no `DateTime` is built from an instant, so nothing overflows or throws), and the probe
  instant of a zone offset lookup is clamped to the `DateTime` range; the painter validates a stroke width, dash
  pattern, and dash offset after their conversion
  to `float` (an unusable width draws nothing, an overflowing offset counts as zero, an unusable pattern is solid) and
  draws a dash pattern solid when its period is below 0.25 surface pixels or the path would have more than 50,000
  dashes, so a specification cannot make the stroker do unbounded work or throw an argument exception. The path marks
  follow the same rule: the curve generators write only coordinates that can be drawn (a point that is not finite, or is
  beyond the one magnitude bound of the painter, 32,768 surface pixels, is a gap, and a cubic with a control point
  beyond it, which a huge `tension` makes, is a line), the
  number of segments of a path is bounded and charged before its list is created (by curve and mode, so that a line of
  isolated points is charged one point and one segment per run), `ArcGeometry` is total (a value that
  is not finite or is above 1e9 in magnitude gives an empty path and a negative radius, pad angle, or corner radius
  counts as zero), the painter draws nothing for a trail point, arc, path command, text, group offset, or stroke
  width that is not finite or is beyond the bound (the cost of an item is bounded, and the painter paints a trail in
  chunks with a cancellation poll between them), the text measurer measures a font size that does not fit a single
  as zero instead of letting the core text renderer throw, and the miter limit of a stroke is clamped (4 by default,
  at least 1, at most 1,000).
- **Defined numeric conversions (phase 14).** A C# cast of a `double` that is NaN or does not fit the integer is not
  defined by the language: .NET 8 on x64 gives the smallest value and .NET 9 and later saturate, so one specification
  could give two answers, or an unhandled `OverflowException` (a quantile step of `1e-300` made a negative count of
  probabilities on .NET 8). Every conversion of a number that comes from a specification or its data to a count, size,
  step or index goes through `NumericConversions` (`ToInt32Saturating`, `ToInt64Saturating`: truncate toward zero,
  saturate at the range, NaN is zero), and the callers bound the saturated value (the row budget, the cell limit) before
  they allocate: a quantile checks the row budget, then `TableLayout.MaxCells`, before it creates the array of
  probabilities, and the loader compares a row count with the row limit as an integer (a count of 2^63 equals a limit
  of `long.MaxValue` as a double, so it is refused first) and bounds a generated sequence and the points of a sampled
  curve by `MaxCells` as well. A canvas whose size is not a finite number (the guides of a `pow` scale with an
  exponent of `1e300`
  are NaN) is a located `VegaLiteRenderException` raised when the view is measured, never an unlocated argument
  exception from the scene. A build with `CheckForOverflowUnderflow` on the library passes every test: that is the
  oracle for a cast that was missed.
- **Calibrated work budget (phase 14).** One evaluation step pays for 64 work elements, and a transform charges its
  work in elements weighted so that an element stands for about three nanoseconds, which makes the default 50,000,000
  steps (3.2 billion elements) a bound of about ten seconds for every transform family. The weights are the measured
  cost of one element on a current desktop processor: a loess weight evaluation 3 (a loess of 20,000 rows with a
  bandwidth of 1 ran 11 seconds before and is refused now), a kernel evaluation of a density 4, an element of a
  polynomial fit 16 (a degree 20 fit of 900,000 rows ran 26 seconds), a draw of a bootstrap resample 5 (a `ci0` of
  900,000 rows ran 14 seconds), an element of a recomputed window frame 20 (a median of a frame of 4,001 rows over
  200,000 rows stopped after 19 seconds); the row-wise transforms charge one element a row and are bounded by
  `MaxDataRows`. Wall-clock time is otherwise bounded only by the limits and the cancellation token.
- **Structured fuzzing, a limit audit and source guards (phase 14).** The limits are only as good as their coverage, so
  three executable checks keep the whole input space under them (see _Resource Limits And Hardening_): the **fuzz
  suite** (`Hardening/VegaLiteFuzz*`) measures and renders seeded, schema-shaped specifications and mutations of the
  fixtures under tight limits, and passes only when every case succeeds or fails with a documented, located exception,
  never hangs, allocates a bounded amount, and gives the same outcome twice; the **limit audit**
  (`VegaLiteLimitAuditTests`) is the inventory below as a table of hostile specifications, each of which must be refused
  with the documented exception, located, naming its limit, before the memory it asked for is allocated, and must stop
  on cancellation while its stage runs; and the **source guards** (`VegaLiteSourceGuardTests`) fail on a narrowing cast
  of a double that is neither routed through `NumericConversions` nor audited with the reason that its value is in range,
  and on any read of the clock, the local time zone, the culture, the environment, a random source or a hash order.
- **Error classification.** The library reports a limit of the parser as a `VegaLiteSpecException` (depth, string
  length), a source longer than `MaxSourceLength` as an `InvalidDataException`, a limit of the layout or the render as a
  `VegaLiteRenderException` whose message names the limit (`the limit of N`, `the limit (Max<Name>)`, or
  `VegaLiteLimits.<Name>`), and a size of
  the chart outside the surface limits as an `ArgumentOutOfRangeException` for the parameter `options`. The
  CanvasNetTool
  maps them to `vega-lite-error`, `corrupt-input` (as for an overlong SCAD model), `budget-exceeded`, and `size-limit`
  respectively; no other argument failure is a size or a usage problem.
- **Policy-checked local data, no network.** Opening reads only the text it is given. A data `url` is read only by
  `Measure` and `Render`, and only when it is a local path (relative, absolute, a drive path, or a `file:` URI without a
  host, read at its local path): a URL that `FeatureCatalog.IsRemoteDataUrl` classifies as remote (a scheme of two or
  more characters other than `file`, a `file:` URI with a host, `localhost` included, or a malformed one, or a path
  that starts with `//` or `\\`) reports `vl-data-remote-url`, is **never fetched and never handed to the file resolver
  or the callback**, and uses an empty dataset (strict mode throws). A relative path is looked up in the base
  directory, then in each data path, in order (search locations, not boundaries); which file may be read is decided by
  `VegaLiteOpenOptions.FileAccess`, which the core `FileReferenceResolver` asks about the **real path of the opened
  file** before a byte is read: the default `FileAccessPolicy.AllowAll` reads every regular local file (`..`, absolute
  paths and symbolic links are followed, like Pandoc), and
  `FileAccessPolicy.Combine(FileAccessPolicy.UnderRoots(...), FileAccessPolicy.NoSymlinks)` restores the confinement
  of earlier versions. A chart drawn as a linked picture gets the policy of the render, anchored at the origin of its
  chain, so a nested chart cannot loosen it. Empty, control-character, network, device and alternate-data-stream paths
  are refused under every policy; on Linux and macOS a drive path is an invalid path (a located error), and with a
  `FileResolver` callback no file-system access and no policy apply at all (an absolute path is then a located error).
  A file is read as bytes under `MaxDataBytes` and decoded
  as strict UTF-8, and each call reads each distinct file at most once. A local file that cannot be used (including a
  denial of the policy, an existing directory in place of a file, an open that fails, and a callback or callback stream
  that throws) is a `VegaLiteRenderException` located at the `url`, with the core `FileReferenceException` as its inner
  exception for a file refusal, in lenient and strict mode alike, never an empty dataset; a dangling link is a missing
  file. Because the policy judges the open handle, a directory swapped for a link between a check and the open cannot
  redirect the read; the residual risk is a hard link (which is the file itself) to a file outside the roots of a
  confining policy, so a deployment that renders files an attacker can modify must not share the directory with them.
  No code in the specification is ever executed: an expression is text
  that a bounded interpreter runs over data values (see _Closed-world expressions_ below), never host code.
- **Closed-world expressions.** The expression language is a subset of JavaScript that an interpreter written for this
  package runs: there is no reflection, `eval`, assignment, `new`, function expression, file or network access, clock,
  or
  random source, and a function exists only if it is in the frozen table of 113 pure functions (the date and time
  functions work on the calendar of the call, never on a clock: `now` is not in the table and `datetime()` without an
  argument is null). An expression is compiled
  when the document is opened (length and nesting limits, no recursion in proportion to the text, located syntax
  errors),
  then run as a loop over a postfix program under an exact step budget (`MaxEvaluationSteps`,
  shared by every expression of
  the call and charged for work proportional to string and array sizes), a string limit, and cancellation checks every
  1,024 steps; a regular expression runs on the linear-time engine or, where the pattern needs look-around or
  back-references, on the backtracking engine under a 100 ms timeout that fails closed, with a pattern of at most 1,024
  characters. A result is a pure function of the datum and the parameters, so it is deterministic and
  culture-independent.
- **Bounded transforms.** Every derived table is bounded before it is allocated by the shared
  row budget (`MaxDataRows`, with
    the peak of an input and an output counted for fold, flatten, aggregate, and impute), `MaxFieldCount`, and 2^25
    cells
    (which also bound the per-group state and the values that an aggregate operation keeps); the number of bins is
    bounded
    by
    `MaxScaleDomainSize`; the work of every row, sort, window frame, and bootstrap is charged to `MaxEvaluationSteps`,
    so a
    hostile specification fails closed and located; a lookup source
  goes through the same policy-checked loader and file cache as the main data; `sample` is seeded from the JSON pointer,
  never
  from a clock, and so is the bootstrap of the confidence interval (`ci0`, `ci1`); and the transforms that
  the renderer does
  not compute are skipped and reported, never approximated.
  - **Cancellation.** A `CancellationToken` is honored before any work, in the data and transform stages (at the start
    of
    every transform and at least every 1,024 rows, also inside the sorts, the bootstrap, the date parsing of a column
  (every 4,096 rows), the `timeUnit` transform (every 1,024 rows), and the implicit pipeline), in
    every expression evaluation (at the start and every 1,024 steps), in
    the scale stage, per tick of an
  axis or a time legend (and per element of a `timeSequence`), per 1,024 rows of the marks, per group and per merge
  block
  of a sort of the path marks, per 1,024 points of a curve, per 1,024 points of a trail while painting, and while
  painting. The
  exception is never wrapped.
- **Buffered, ordered event delivery.** Warnings and unsupported-feature reports are recorded in a sink while the work
  runs and delivered to the caller's handlers afterwards, in document order, once, on the calling thread only (also
  when the work fails; a handler exception on a failure path cannot hide the failure, and after a success a handler
  exception propagates and the returned surface is disposed first). User code therefore never runs inside the
  pipeline.
- **Bounded diagnostics.** Reports are de-duplicated by message and location and capped at `MaxDiagnostics`, so a
  hostile specification cannot exhaust memory through its reports.
- **Nothing is silently skipped.** Every recognized construct that is not drawn produces a report with a stable
  identifier; strict mode (`ThrowOnUnsupportedFeature`) turns the first one into an exception for builds that must never
  accept an approximate chart.
- **Deterministic and culture-independent (the determinism rule of the calendar).** Numbers are parsed and formatted
  with the invariant culture, text is
  measured with the bundled fonts, and no wall clock or environment variable is consulted, so results do not depend on
  the host's locale or installed fonts. **Time follows the same rule: the only zone is
  `VegaLiteRenderOptions.TimeZone` (null is UTC), and no code of the package reads the host's zone, clock, or culture**
  (`TimeZoneInfo.Local`, `DateTime.Now`, `UtcNow`, `ToLocalTime`, and `CurrentCulture` are not used by the time code, a
  property that is checked by a search of the source; the two earlier `ToUniversalTime` conversions of caller-supplied
  `DateTime` values remain). Month, day, and period names are the English d3 locale, digits are ASCII, a local wall
  time is converted to an instant by one fixed rule (see _Deviations From Vega-Lite_), and an invalid date formats as
  `Invalid Date`, so the output is the same on every host, in every culture and zone, on every thread.

## Data Flow

**Open path (`VegaLiteDocument.Parse`, `Open(string)`, `Open(Stream)`):**

1. **Validate** the arguments and options (null text, null or whitespace path, unreadable stream). `Open(string)`
   fixes the effective source name (the path as supplied, unless overridden) and the effective base directory (the
   folder of the path's full name, unless overridden).
2. **Read**: `Open` reads the file or stream as UTF-8 in chunks, detecting and removing a byte-order mark, and fails
   with `InvalidDataException` as soon as the running length exceeds the limit. `Parse` checks the length of the string.
3. **Parse**: `SpecParser` converts the text into UTF-8 bytes, reads them in one pass with `Utf8JsonReader` into an
   immutable `SpecNode` tree (pointers computed lazily, line and column per node), applies the duplicate-key policy
   (last value wins, one warning), validates the shape of the known properties against the rules taken from the
   Vega-Lite 6 schema, **compiles the expression of every supported site** (the `expr` of a variable parameter, a
   `calculate`, a string `filter`, and the row-bound `{"expr": ...}` paint values of the drawn view) with
   `ExpressionParser`, **compiles the `{"expr": ...}` references of the static sites** that Vega-Lite's schema allows
   (the
   title, the padding and background, the other mark properties, the guides, scales, and view background, and the
   `config`
   of the top-level view) and lists them as static references, checks the transforms, predicates, and parameters (and
   the
   time units, date-time objects, and
   tick and `nice` intervals, whose predicates are also compiled to expressions), and records `vl-unknown-property`
   warnings.
4. **Describe**: `SpecMetadata` reads the schema URL and major version, the title, the mark types, the data references,
   and the parameter names from the tree; `FeatureCatalog.Visit` lists the recognized-but-unsupported constructs (the
   functions, view state, and operators that the compiled expressions collected among them). The
   parse warnings and the catalog reports are merged once, sorted by line and column (stable), and kept as the
   document's reports. A render with a caller's `Config` visits the catalog again with the effective configuration
   (`VegaLiteDocument.ReportsFor`), so that the reports that depend on the configuration (the overlays of a path mark,
   which say whether the points or the line of an area draw a channel) are those of what is drawn.
5. **Output**: an immutable `VegaLiteDocument`, safe to share between threads.

**Measure and render path (`Measure`, `Render(options)`):**

1. **Choose options**: the given options, or the defaults.
2. **Prepare the chart** (reads only the data files and delivers nothing): the **calendar** of the call is created once
   from `VegaLiteRenderOptions.TimeZone` (`TimeCalendar.Create`; null is UTC) and one `ExpressionEvaluator` with the
   one dictionary of parameter values serves the call; the **static parameters** are evaluated first, once in
   dependency order (`ParameterEnvironment`, the caller's `Parameters`, which can be arrays and objects, replacing a
   definition), and `ExpressionReferenceFolder` then evaluates the **expression references of the static sites** once
   with those values and replaces them in a copy of the specification tree (the pipeline order of a call is parameters,
   fold, configuration, normalization, data, transforms, scales); `ConfigResolver` merges the defaults, the
   specification's `config`, and the caller's `Config` over that tree; a `TextMeasurer` is created for the font options;
   `SpecNormalizer.Expand` reduces the views to the tree of views that are drawn (a single view is one chart; a
   composite mark is replaced by its layers; a `repeat` is expanded into its copies; a unit view with a `row`, `column`,
   or `facet` channel becomes a facet; `MaxViews` and `MaxCompositionDepth` are enforced here, and the warnings of the
   normalization, `vl-concat-axis-shared`, `vl-composition-columns-ignored`, and `vl-repeat-ref-unresolved`, are
   kept). When the root has at least one chart with a supported mark (a single view, a layered view, or any
   composition),
   one
   `DataLoader` serves the call (with its own file cache, file count, row budget, and the calendar) and reads the data
   first,
   before any scene item is charged: inline rows, the local file named by a relative `url` (through
   `DataFileResolver`, then `DelimitedTextParser` for CSV, TSV, or DSV, or `JsonDocument` for JSON), a named dataset
   (the caller's `Datasets` first, then the top-level `datasets`), or a generated sequence; the `parse` conversions
   are applied (a `date:'%fmt'` entry through `TimeFormat` in the calendar, `utc:'%fmt'` in UTC), the temporal fields of
   the encoding (`TemporalFields.Collect`) are **date-parsed implicitly** unless a `parse` entry names them, and the
   values that an explicit or implicit conversion could not read are collected as `vl-data-parse-failed`
   warnings. The **transforms** of the view then run in the written order (`RowTransforms`: calculate, filter,
   fold, flatten, sample, pivot, lookup (whose source is read by the same `DataLoader`), and timeUnit; and, through
   `AggregateTransforms`, aggregate, joinaggregate, window, bin, stack, and impute), each producing a new table
   within the shared row budget; the expressions run with the same calendar, and a predicate with a time unit or a
   date-time object is evaluated as an expression over the calendar functions; the findings they can recover from are
   collected as `vl-expr-runtime` and
   `vl-transform-runtime` warnings. `EncodingPlan` then plans the **implicit pipeline** of the encoding (the field
   names,
   titles, bins, time units, aggregate, and stack) and `EncodingTransforms` runs it in Vega-Lite's order: the bins of
   the
   binned fields, the **time units** of the fields that carry one (the start and, for a channel that spans its unit, the
   end), the **Raw** table, the aggregate of the aggregating channels, the **encoding impute** (the `impute` of a
   position channel), the **stack impute** (the implicit zeros of the layers of a stacked line, area, or trail), the
   stack (also of the polar `theta` and `radius`), and the **Main** table.
   `ScaleResolver` then infers the scales from the plan over those tables (a domain reads Main; a sort by an
   aggregate and a
   `scale.domain` of `unaggregated` read Raw; a temporal field gets a time or UTC scale carrying the calendar; the
   `theta` and `radius` of an arc or text mark get the polar scales, whose range is the full circle and the half of the
   smaller side of the data rectangle). **For a layered view or a composition** the data stage is a depth-first walk of
   the view tree: the
   table of a view is that of the nearest `data` above it, the `transform` of each view runs on it before the views
   below
   read it, and each unit then plans and runs its own implicit pipeline (a **facet** is instantiated at its node:
   `EncodingTransforms.Facet` orders the cells and splits the table into one table for each cell, checking `MaxViews`
   first, and the cells are walked with their rows); `ScaleResolver.ResolveLayers` resolves each
   unit's scales and merges the scales of the channels that the layers, or the charts and cells of a composition, share
   (the union of their domains), or keeps them apart
   as `resolve` says, and the discrete-step hints come from the merged scales.
   `ViewLayout` resolves the view request (size, padding,
   autosize, background, view style) with the discrete-step hints of the scales; `TitleBuilder` builds the title. If
   there is no drawable chart (a single view, the layers of a layered view, or the charts of a composition with a
   supported mark) the layout is
   computed around the title alone (an empty view, as for an unsupported mark). Otherwise a **single or layered**
   chart is built: the axes are built for the
   current data
   rectangle (one for each channel, or two when a layered view keeps its axes apart), their extents measured, and the
   layout re-measured, at most three times, until the data rectangle is
   stable (it is stable at once for fixed sizes); the `MarkBuilder` then builds the marks once for the final rectangle
   from the Main table of each layer (one `marks` group for each layer, in layer order, each with its own scales and
   table)
   (evaluating the row-bound paint expressions for each row with the evaluator of the call; the rows are first put in
   the draw order of the `order` channel unless it orders a stack, and a bar, rect, point, rule, tick, text, or arc is
   one item for each valid
   row, while a line or area is one path for each group of rows that share the grouping fields, sorted, cut at the
   rows that are not defined, turned into segments by `CurveBuilder`, and followed by its point and line overlays, and
   a trail is one item of runs of sized points),
   the item budget being charged for the axes and for each mark item as it is created (a path before its segment
   list); and `ViewLayout.Build`
   assembles the
   scene (root, view group, grids, axes behind the marks, the marks group, axes in front, title) and the natural
   size. A **composition** is built bottom-up by the `Composer` of `VegaLiteDocument`: every chart of the tree
   (the unit or layered views that are children of a concatenation, the cells of a facet, the copies of a repeat) is
   built as a cell with the same code (size from `ViewLayout.ResolveSize`, bounds from `ViewLayout.CellBounds`, the
   axes and legends that the cell owns, its marks, its title), then `CompositionLayout.Place` positions the cells of
   each concatenation and facet with the headers, footers, and titles of a facet (`TitleBuilder.BuildHeader`, and the
   shared axes of a facet drawn once in the outer headers and footers with `AxisRequest.InHeader`), the legends that a
   composition owns (the shared legends merged at the highest view that shares them) are built and placed beside its
   grid by `LegendLayout`, and the title is anchored at the start of the grid; the natural size is
   `ViewLayout.Measure` of the bounds of the root, so `Measure` equals the size of the surface. The scene budget is
   charged for every header, footer, title, cell group, and legend before it is created. When this fails with a
   specification or render error, the reports found so far are still delivered before
   the error is rethrown.
3. **Validate**: the natural size times the scale is validated (each side from 1 to `Surface.MaxDimension`), then the
   `Parameters` names, then the token.
4. **Record**: the reports of the document (warnings, then catalog reports, in document order) are recorded in the
   `VegaLiteReportSink`, which de-duplicates, caps, and, in strict mode, throws at the first unsupported feature;
   the data warnings of this call follow them, in this order: the warnings of the normalization of the compositions
   (`vl-concat-axis-shared`, `vl-composition-columns-ignored`, `vl-repeat-ref-unresolved`), then those of the loader
   (`vl-data-parse-failed`, also
   for a lookup source), then those of the transforms (`vl-expr-runtime`, `vl-transform-runtime`), then the scale
   conflicts
   of the layers (`vl-layer-scale-conflict`), then `vl-layout-fit-step` (which needs the scales); they are also
   delivered
   when a later stage fails.
5. **Paint** (`Render` only): `ScenePainter` fills the background and draws the scene onto a new `Surface` of the
   scaled size.
6. **Deliver**: the sink replays its reports to the caller's handlers in order on the calling thread; on a failure the
   reports are delivered with handler exceptions ignored so that the failure wins.
7. **Output**: the size or the surface, or the first error as a typed exception.

**Exact-size render path (`Render(width, height, options)`):** the size and the `Parameters` names are validated
first, then the token; the chart is prepared as above; the placement is a uniform scale that fits the natural chart
into the surface, centered (a chart without area is not scaled); and the painting and delivery steps are as above. The
`Scale` option is not used.

## Design Constraints

- **Simplicity**: Minimal functionality kept easy to understand and extend
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms
- **Dependency direction**: `CanvasNetVegaLite` must never reference `CanvasNetCharts`, `CanvasNetSvg`,
  `CanvasNetPdf`, `CanvasNetPptx`, `CanvasNetVsdx`, `CanvasNetRender3D`, or `CanvasNetScad` (see Dependencies above)
- **Static rendering**: A chart is a still picture. Interaction (events, bindings, tooltips, links, cursors,
  accessibility annotations) has no meaning and is either ignored without a report (`tooltip`, `href`, `description`,
  `key`, and the `bind`, `on`, `clear`, `toggle`, `nearest`, `translate`, and `zoom` of a parameter) or, when it changes
  what
  is drawn (the initial value of a selection), modeled in its initial state (the animation of a selection is reported)
- **Deterministic time (no host zone, clock, or culture)**: every date and time operation runs on the one
  `TimeCalendar` of the call, built from `VegaLiteRenderOptions.TimeZone` (null is UTC). No code reads
  `TimeZoneInfo.Local`, `DateTime.Now`, `ToLocalTime`, or the current culture; names are the English d3 locale and
  digits are ASCII; every instant is bounded to the years 1 to 9999 (NaN, hence null, outside); the time ticks,
  labels, and sequences are bounded by `MaxTicks`, `MaxStringLength`, and `MaxScaleDomainSize`. The calendar and the
  time formatter live in `Data` so that no unit above them needs the host's time (see deviation 35).
- **Deterministic arithmetic of the path marks**: the curves are literal ports of the d3-shape curve classes with fixed
  epsilons, the groups of a path mark are kept in first-seen order and sorted with a stable sort, the text of a label
  uses the invariant culture, and nothing reads a clock; the trigonometry of an arc and a polar offset is the host's
  `Math` library, so a coordinate can differ in the last bit between platforms (deviation 54), which is why the tests
  compare geometry with a tolerance and never compare pixels across operating systems
- **Bounded path, text, and arc marks**: a path, a label, or an arc is charged to `MaxSceneItems`,
  `MaxStringLength`, and `MaxEvaluationSteps` before it is created, so no chart can spend memory in proportion to
  anything but its data
- **Fail-visible**: Nothing the specification asks for may disappear without a report; a warning is a questionable
  construct that still renders, an unsupported-feature report is a recognized construct that is not drawn, and an
  exception is a problem that makes the result meaningless or dangerous
- **Additive evolution**: The public types are sealed, options are init-only, results are read-only lists, and records
  and enumerations tolerate added members, so later phases extend the API without breaking it; a stable identifier is
  never renamed once released
- **No ported test material**: Vega-Lite's tests and examples and the `vega-datasets` collection are not used;
  fixtures and test data are original works written for this repository, and expectations are derived by hand

### Platform Support

The library targets the following frameworks, identical to the `CanvasNet` system it references, enabling
compatibility across modern, currently supported .NET runtimes:

| Target Framework | Runtime / Environment |
| ---------------- | --------------------- |
| `net8.0`         | .NET 8 LTS            |
| `net9.0`         | .NET 9                |
| `net10.0`        | .NET 10               |

The library is supported on the following operating systems:

- **Windows** - primary developer and CI platform
- **Linux** - CI/CD and containerized environments
- **macOS** - developer workstations using Apple platforms

Portability is achieved by restricting the implementation to Base Class Library (BCL) APIs and the public API of the
core package. No platform-specific native interop, OS-specific APIs, or framework-version-specific features are used.
Locations count UTF-16 code units and treat `\n` as the only line terminator (a preceding `\r` belongs to the previous
line), so results are identical on every platform regardless of the line-ending style of the checkout. Text uses the
bundled Liberation fonts by default, so measurement and rendering do not depend on the fonts installed on the host.

### Integration Patterns

- **NuGet Packaging**: Standard .NET library packaging and distribution; the package is packable, declares
  `DemaConsulting.CanvasNet` as its only dependency, and packs the `third-party` license texts under `licenses/`
- **CI/CD Integration**: Automated build, test, and quality validation
- **Requirements Traceability**: All features linked to passing tests
- **Front-end role**: `VegaLiteDocument.Render` draws onto a core `Surface`; the host (for example the `canvasnet`
  tool) decides what to do with the surface. The renderer never writes a file and never calls into a codec

### Third-Party Provenance and Attribution

`CanvasNetVegaLite` is **derived with attribution** from the public Vega-Lite language and a small number of ported
algorithms and values; it is **not a clean-room implementation**, and it is **not affiliated with, endorsed by, or
promoted by** the Vega project, the Vega-Lite project, the D3 project, or the University of Washington Interactive
Data Lab. The names are used only to describe the language that is implemented. The system model, normalization,
layout structure, and marks are original code written from the public documentation and the behavior rules noted
below; **no source file, test, example, dataset, or documentation text of the upstream projects was copied**, and
the documentation is paraphrased.

The ported material, its licenses, and the units that contain it are:

<!-- markdownlint-disable MD013 -->
| Upstream project | License | Units that contain ported material | What is ported |
| ---------------- | ------- | ---------------------------------- | -------------- |
| Vega-Lite | BSD-3-Clause | `ConfigResolver`, `ScaleResolver`, `ColorSchemes`, `AxisBuilder`, `TitleBuilder`, `ViewLayout`, `MarkBuilder`, `EncodingPlan`, `EncodingTransforms`, `AggregateTransforms`, `StackRules`, `SpecNormalizer`, `VegaLiteDocument` | Default configuration values (background, padding, view size, step, mark, bar, rect, tick, rule, scale, header, and spacing configuration), the `tableau10` default category colors, the precedence of mark, view, title, and style properties, the scale inference rules (type by field type, channel, and mark; domain order; zero, nice, padding, and reverse rules; default size and opacity ranges; step sizing), the default axis tick count and axis defaults and rules, and the inferred orientation, band size, opacity, color, and invalid-data rules of the supported marks; and, since phase 6, the compile rules that turn an encoding into transforms (the order bin, aggregate, stack; the group-by and stack-by fields; which marks stack, in which direction and with which default offset; the derived field names and default titles), the domain rules of stacked, binned, and aggregated fields, the geometry of a binned bar or rect, and the tick count of a binned axis; and, since phase 8, the rules for time units (the unit names and parts, the derived field names and titles, the band end, the band position and size of a unit, the predicate expressions of a time unit or a date-time object, the date-time objects, and the default time-axis tick count, minimum step, label flush, and overlap rules); and, since phase 9, the compile rules of the line, area, trail, text, and arc marks (the encoding of a path, the orientation and the baseline of an area, the point and line overlays, the grouping and sorting rules of the paths, the invalid data modes, the polar position and range rules of an arc and a text mark, the formatting of a label, and which marks and channels may stack), the stack and impute rules of a path mark (the implicit zeros of the layers, the polar stack), the scale type, zero, and range rules of the polar channels, and the channel support of each mark; and, since phase 10, the normalization of the composite marks `boxplot`, `errorbar`, and `errorband` into layers (the aggregates, the fences, the center and extent, the part defaults and their order of precedence, the orientation, and the defaults of the composite mark configuration), the merging of the domains and the precedence of the scale types of layered scales, the default sharing and the `resolve` of scales, axes, and legends, and the merging of the titles and the placement of independent axes (`SpecNormalizer`, `ScaleResolver`, `VegaLiteDocument`); and, since phase 11, the composition rules (the normalization of a unit view with a `row`, `column`, or `facet` channel into a facet, the expansion of a `repeat` and the replacement of its field references, the layout settings `spacing`, `columns`, `align`, `center`, and `bounds`, the default `resolve` of a concatenation and of a facet, the order of the cells of a facet and its sort by an array, the properties and defaults of the facet headers, the sizing of the charts of a composition, the merging of the legends at the owner view, and the `autosize` fit normalization and the dropping of a fit for a step) (`SpecNormalizer`, `EncodingTransforms`, `ScaleResolver`, `ViewLayout`, `TitleBuilder`, `VegaLiteDocument`); and, since phase 12, the raw domain of a scale (the `domainRaw` and the domain that a parameter gives, which nothing adjusts, and the clipping of the marks of a scale whose domain is a parameter) and the properties of the schema that take an expression reference (`ScaleResolver`, `ExpressionReferenceFolder`); and, since phase 13, the compile rules of the `regression`, `loess`, `density`, `quantile`, `extent`, and `pivot` transforms (the default output fields, the shared resolve of a density, and the `isExtent` precedence) (`StatTransforms`, `SpecParser`), and the compile rules of the `image` mark (`compile/mark/image.ts`, the image case of `rectPosition` in `encode/position-rect.ts`, `rangePosition` in `encode/position-range.ts`, `vgAlignedPositionChannel` in `encode/position-align.ts`, and `text(model, 'url')` in `encode/text.ts`) (`MarkBuilder.Images.cs`) |
| Vega | BSD-3-Clause | `ConfigResolver`, `ViewLayout`, `LegendLayout`, `CompositionLayout`, `TextMeasurer`, `TitleBuilder`, `AxisBuilder`, `LabelOverlap`, `LegendBuilder`, `SceneModel`, `ScenePainter`, `Ticks`, `ScaleModel`, `ScaleResolver`, `BinAlgorithm`, `AggregateMeasures`, `AggregateTransforms`, `StackRules`, `ColorSchemes`, `ColorInterpolator`, `MarkBuilder`, `CurveBuilder`, `ArcGeometry`, `SymbolPathParser`, `StatTransforms`, `KernelDensity`, `LoessFit`, `CurveSampler` | Default Vega configuration values, the guide, group, and mark styles, the title and axis layout constants and offsets, the axis geometry (tick and label positions, band offset, rounding, half-pixel translate, extent clamping, label overlap removal), the tick-count rules, the `bandspace` rule, the text baseline offsets and line-height and truncation rules, the group stroke offset, the symbol shape geometry, and the view-layout rules for `autosize` and title placement; and the legend rules (entry sizes and offsets, the gradient bar and its stops, the grid layout of entries, and the placement of legends by orient, offset, margin, and anchor); and, since phase 6, the bin algorithm and the assignment rules of the `bin` transform, the semantics of the aggregate operations, and the behavior rules of the `window`, `stack`, and `impute` transforms; and, since phase 7, the palette table of 68 color schemes, the piecewise sRGB ramp, the interpolation types, the range and quantize helpers, and the rules that give a scheme its number of colors; and, since phase 8, the time-unit floor, bin, and specifier rules, the multi-format of dates, the date and time expression functions, and the time-scale tick rules; and, since phase 9, the path geometry of the scenegraph (the segment-list path, the capsules of the trail, the arc item and its stroke and miter limit), the text mark (the alignment and baseline defaults, the font size and style properties, the line breaks, the polar offset of a label, and the limit and ellipsis), the symbol and line mark styles; and, since phase 11, the grid layout of a composition (`gridLayout` of `vega-view-transforms`, which the legend layout shares, with the alignment, centering, bounds, and anchor rules), the trellis layout of the headers, footers, and titles of a facet (`trellisLayout`), and the view size layout of `autosize` `fit` (`viewSizeLayout` with its legend rule); and, since phase 13, the statistical operators (the `Regression` and `Loess` transforms of `vega-regression`, the `KDE`, `Quantile`, `Extent`, and `Pivot` transforms of `vega-transforms`, and the `regression`, `loess`, `kde`, `bandwidth`, `normal`, `quantiles`, `numbers`, and `sampleCurve` functions of `vega-statistics`), and the geometry of the `image` mark (`imageWidth`, `imageHeight`, the anchors, and the fit-inside rule of `vega-scenegraph/src/marks/image.js`; the `x2`, `xc`, and `width` channels of `vega-runtime/src/util.js`) and, since phase 13, the custom symbol paths (the path parser, the canvas path renderer with its arc conversion, the bounds of a path, and the `pathSymbol` scaling of `vega-scenegraph`: `src/path/parse.js`, `render.js`, `arc.js`, and `symbols.js`, and `src/bound/boundContext.js`), the legend layout of the title orients (`legendGroupLayout` and `legendTitleOffset` of `vega-view-transforms` and `legend-title.js` of `vega-parser`), and the axis band, extra tick, translate, position, title position, and label bound rules (`guide-util.js`, `axis-title.js`, `axis-labels.js` of `vega-parser` and `Overlap.js` of `vega-view-transforms`) |
| d3-shape | ISC | `CurveBuilder`, `ArcGeometry` | The curve classes (linear, linear-closed, step, basis, basis-open, basis-closed, bundle, cardinal, cardinal-open, cardinal-closed, catmull-rom, monotone-x, monotone-y, and natural), the `line` and `area` generators with their `defined` handling and area baseline pass, and the `arc` generator (the sector and annulus, the pad angle, the corner rounding, and the full circle) |
| d3-path | ISC | `CurveBuilder`, `ArcGeometry` (`ArcPathWriter`) | The path context (`moveTo`, `lineTo`, `bezierCurveTo`, `closePath`, and `arc` with its epsilon rules), emulated over the segment list and the core path |
| d3-array | ISC | `Ticks`, `AggregateMeasures`, `TimeUnitSpec`, `StatisticsPrimitives` | The `ticks`, `tickIncrement`, and `tickStep` algorithms (`TimeUnitSpec` keeps a private copy of `tickStep`), the `quantileSorted` interpolation (the R-7 method) behind the median, the quartiles, and the bootstrap interval, and, since phase 13, the `deviation` and the `range` behind the quantile transform and Scott's rule |
| d3-regression | BSD-3-Clause | `RegressionFit` | The linear, logarithmic, exponential, power, quadratic, and polynomial regression models and the coefficient of determination, as `vega-statistics` adapted them (phase 13) |
| regression-js | MIT | `RegressionFit` | The polynomial least-squares fit that d3-regression adapted from it (phase 13) |
| science.js | BSD-3-Clause | `LoessFit` | The locally weighted regression (tricube weights, window, robustness iterations) as `vega-statistics` adapted it (phase 13) |
| d3-format | ISC | `NumberFormat` | The format specifier grammar and the `format`, `formatPrefix`, `precisionFixed`, `precisionPrefix`, and `precisionRound` behavior for the numeric types |
| d3-scale | ISC | `ScaleModel`, `Ticks`, `NumberFormat` | The arithmetic of the linear, band, and point scales (and of the time scale, which is the same map over milliseconds), the linear and the time `nice` methods, and the precision rule of `tickFormat` |
| d3-time | ISC | `TimeCalendar` (`TimeInterval`), `Ticks` | The interval objects (`floor`, `ceil`, `offset`, `count`, `every`, `range`) of millisecond to year in local and UTC time, and the `tickInterval` table and `ticks` of the time scale |
| d3-time-format | ISC | `TimeFormat` | The formatter and parser directives, padding flags, week-number rules, and the English locale |
| d3-scale-chromatic | ISC (and Apache-2.0 for the ColorBrewer colors) | `ColorSchemes` | The colors of `category10`, `observable10`, `rainbow`, `sinebow`, and the ColorBrewer qualitative sets |
| d3-interpolate, d3-color | ISC | `ColorInterpolator` | The sRGB, HSL, CIELAB, HCL, and (since phase 13) cubehelix color interpolation (`cubehelix` and `cubehelixLong`, with the cubehelix conversion constants of d3-color) and the piecewise ramp |
| ColorBrewer | Apache-2.0 | `ColorSchemes` (`ColorSchemeData`) | The color values of the ColorBrewer qualitative sets and of the sequential and diverging ramps that Vega names after it |
| matplotlib colormaps, cividis, Turbo | CC0, a Battelle permissive license, Apache-2.0 | `ColorSchemes` (`ColorSchemeData`) | The color values of `viridis`, `magma`, `inferno`, and `plasma` (CC0); `cividis`; and `turbo` |
<!-- markdownlint-enable MD013 -->

The `Expressions` subsystem (apart from the date and time functions) and the `RowTransforms` unit (with `RowSampler`,
`TableLayout`, and `ParameterEnvironment`)
port **nothing**: they are original code written from the public documentation
of the Vega expression language, the Vega-Lite transforms and parameters, and ECMAScript, so they have no row in the
table
and carry no `Portions derived from` header; the date and time functions (`ExpressionFunctions.Time.cs`) re-implement
the Vega functions over the calendar and carry the header; the `sample` transform is built from the published FNV-1a
and SplitMix64
descriptions and the classic reservoir algorithm. The `AggregateTransforms` and `EncodingPlan` units re-implement
the rules named in the table from their public description (the bin algorithm is a port of the Vega `bin`), carry the
`Portions derived from` header where they contain ported logic, and use the same published FNV-1a and SplitMix64
descriptions for the deterministic bootstrap. `src/DemaConsulting.CanvasNet.VegaLite/third-party/README.md` records
this, with the upstream files that were read.

The obligations of the licenses are met as follows:

- The license texts ship with the package: `src/DemaConsulting.CanvasNet.VegaLite/third-party/LICENSE-vega.txt`
  (BSD-3-Clause), `LICENSE-d3.txt` (ISC, one section for each d3 module that is ported, d3-shape and d3-path
  included), `LICENSE-colorbrewer.txt` with `LICENSE-apache-2.0.txt` (the ColorBrewer
  Apache notice and the Apache License), `LICENSE-turbo.txt` (Apache-2.0), and `LICENSE-cividis.txt` (the cividis
  license) are packed under `licenses/` by the `third-party\LICENSE-*.txt` glob, and the `CanvasNetTool` package,
  which bundles
  the assembly without declaring a dependency, carries the same notices.
- `src/DemaConsulting.CanvasNet.VegaLite/third-party/README.md` is the provenance record: for each upstream project it
  lists the version studied, the license and copyright notice, the unit that ports it, and exactly what was ported,
  and it lists the upstream files that were read. It is part of the reviewed documentation.
- Every source file that contains ported logic carries the header comment
  `Portions derived from <project> (<license>); see third-party/README.md`.
- No endorsement is implied: the documentation says that the package implements the Vega-Lite specification language
  and never that it is produced or approved by the upstream projects.
- Ported code is **local source, not an OTS Software Item** (the upstream packages are not consumed), so no OTS design
  document exists for them. If a later decision is to consume an actual third-party package, that triggers the OTS
  artifacts.

### Supported And Deferred Features

This table is the exact boundary of the **delivered phases** (1 to 11). The renderer does not imply general Vega-Lite
support: a recognized construct outside the table is reported with the identifier shown, never skipped silently, and
strict mode throws at the first report. The reports carry the RFC 6901 pointer and the one-based line and column of the
construct.

<!-- markdownlint-disable MD013 -->
| Area | Supported | Reported (identifier) when outside the supported subset |
| ---- | --------- | ------------------------------------------------------- |
| Schema | `$schema` of Vega-Lite version 5 or 6, interpreted with the version 6 defaults; no `$schema` | Another major version, or a URL naming no Vega-Lite version: warning `vl-schema-version` (still rendered with the version 6 defaults); a malformed value is a `VegaLiteSpecException`; plain Vega is rejected |
| Views | One single view; a **layered view** (`layer`, at most `MaxViews` views) of unit views and layered views, with the `data`, `transform`, `encoding`, and size that a layer inherits and the `resolve` of its scales, axes, and legends; the **compositions** `concat`, `hconcat`, and `vconcat` (children of any kind, `columns`, `spacing`, `align`, `center`, `bounds`), `facet` (a `row` and `column` mapping or one wrapped field over a `spec` that is a unit or a layered view, with `sort` and `header`; and the `row`, `column`, and `facet` channels of a unit view, normalized to a facet), and `repeat` (a list, a `row` and `column` matrix, `layer`, and `{"repeat": ...}` references in the `field`, `datum`, `sort`, and conditions of a channel), nested to `MaxCompositionDepth` levels, each with the `data` and `transform` that its children inherit and the `resolve` of its scales, axes, and legends (a concatenation: position scales independent, axes never shared, the other scales and the legends shared and merged beside the grid; a facet: shared scales and axes drawn once in the outer headers and footers) | `projection`: `vl-projection`; a `width`, `height`, `autosize`, `padding`, `background`, `config`, `datasets`, or `title` of a view inside a layered view: warning `vl-layer-property-ignored`; a `width`, `height`, or `autosize` of a composition, a `"container"` child, and the `padding`, `background`, `config`, `datasets`, or `autosize` of a view inside a composition: warning `vl-composition-property-ignored` (the composition is drawn as `pad`); `resolve.axis` of `shared` on a concatenation: warning `vl-concat-axis-shared`; `columns` with a `row` and `column` mapping: warning `vl-composition-columns-ignored`; a `{"repeat": ...}` reference with no value: warning `vl-repeat-ref-unresolved` (the channel is dropped); a `header` `labelOrient` or `titleOrient` on a side that is not drawn: `vl-header-orient`; a `row`, `column`, or `facet` channel of a view that cannot be faceted: `vl-channel-<name>`; scales of a shared channel that cannot be merged: warning `vl-layer-scale-conflict` (each keeps its own scale). The identifiers `vl-composition-layer` (Phase 10) and `vl-composition-concat`, `-hconcat`, `-vconcat`, `-facet`, and `-repeat` (Phase 11) are retired; a composition in a `layer` or in the `spec` of a facet, and a facet or repeat without a `spec`, are a `VegaLiteSpecException` |
| Data | Inline `data.values` (an array of objects, one row per object with nested values flattened to path fields, or of primitives, one row `{data: value}`); a **local `data.url`** (relative, absolute, a drive path, or a `file:` URI without a host, read at its local path under the file-access policy) read as JSON (the default), CSV, TSV, or DSV (`format.type`, else the extension `.csv` or `.tsv`, else JSON; DSV uses `format.delimiter`, a comma when omitted); `format.property` (a dotted path to the array of rows in a JSON file); `format.parse` as `"auto"` (explicit per-column inference, never the default), `null`, or a field map of `number`, `boolean`, `date`, and `string` (named fields only; delimited text keeps its cells as strings otherwise), where `date` is read by the `Date.parse` rules in the calendar (a date-only ISO text is UTC, a date-time without an offset is in the chart's time zone) and `date:'%fmt'` and `utc:'%fmt'` by the d3 time-format parser (in the calendar and in UTC respectively); the fields that the encoding, a `timeUnit` transform, or a filter or condition predicate uses as dates are **date-parsed implicitly** unless `parse` names them or is null; **named datasets** (`data.name` found in the caller's `Datasets`, then in the top-level `datasets` arrays); `data.sequence` (`start`, exclusive `stop`, `step`, `as`) | A missing or empty `values` array or a `data` object without a source: warning `vl-data-empty` (empty chart); `url` that names remote data (a scheme other than `file`, a `file:` URI with a host, or a path starting with `//` or `\\`): `vl-data-remote-url` (never fetched, never given to a resolver, empty dataset); `values` (or a referenced dataset) as text: `vl-data-values-text`; `sphere`, `graticule`, topojson, or `values` (or a referenced dataset) as an object: `vl-data-geo`. Not a report but a `VegaLiteRenderException`: a local file that is missing (a dangling link included), refused as never allowed, denied by the file-access policy, a directory in place of a file, unreadable (an open failure, or a failing callback or stream), invalid UTF-8, invalid JSON or delimited text, or over a limit; an undefined `name`; a sequence with a missing or non-finite `start` or `stop` or a zero, non-finite, or non-numeric `step`. Values that an explicit or implicit `parse` could not convert (a date that does not parse is null) become null and give the warning `vl-data-parse-failed` |
| Marks | `bar`, `rect`, `point`, `circle`, `square`, `rule`, `tick`, `line`, `area`, `trail`, `text`, `arc`, `image` (a mark given as a string or as a definition object with `type`; the `image` through the host's image resolver: the `url` channel or property, the position, `width`, `height`, `align`, `baseline`, `aspect`, `smooth`, and `opacity` of Vega-Lite 6.5 and Vega's image mark); the composite marks `boxplot` (the Tukey form with an `extent` that is a multiple of the interquartile range, or `min-max`; the parts `box`, `median`, `rule`, `ticks`, and `outliers`), `errorbar`, and `errorband` (the `extent` `stderr`, `stdev`, `ci`, or `iqr`, the `center` `mean` or `median`, the parts `rule`, `ticks`, `band`, and `borders`; from raw observations, a pre-aggregated range, or error channels), expanded into the primitive layers that Vega-Lite normalizes them to; the `interpolate` (the fifteen Vega-Lite names, with `tension`), `point` and `line` overlays, `invalid` modes, `order`, `strokeMiterLimit`, `padAngle`, `cornerRadius`, `innerRadius`, `outerRadius`, `radius`, `radius2`, `theta`, `theta2`, and the text properties (`text`, `align`, `baseline`, `dx`, `dy`, `angle`, `font`, `fontSize`, `fontStyle`, `fontWeight`, `limit`, `ellipsis`, `lineBreak`, `lineHeight`) of the path, text, and arc marks | Every other recognized mark type (`geoshape`): `vl-mark-<type>`; the `image` mark without an image resolver: `vl-mark-image` (laid out, no picture drawn; strict mode fails); a picture that the resolver does not give: warning `vl-mark-image-unresolved`; a `url` channel on a mark other than `image`: `vl-channel-url`; the chart is drawn without that mark (and, for a single view, without axes); the identifiers `vl-mark-boxplot`, `vl-mark-errorbar`, and `vl-mark-errorband` are retired; `aggregate: "boxplot"` and the like are not accepted (a specification error, as the aggregate name is unknown); `invalid` of `"show"` or null is drawn since Phase 13 (`vl-mark-invalid-show` is retired); an entry of `config.scale.invalid` (per scale channel: `"zero-or-min"`, or `{"value": number or text}` as the output of the channel) is drawn since Phase 14 and puts its channel in the show mode whatever the mark's mode, and an entry that is neither: `vl-scale-invalid-value`; `invalid` of `"filter"` or `"break-paths-filter-domains"`: warning `vl-mark-invalid-domains` (the scale domains keep the invalid rows); `interpolate: "bundle"` on an `area`: `vl-mark-interpolate-bundle-area` (drawn linear) |
| Channels | `x`, `y`, `xOffset`, `yOffset`, `color`, `fill`, `stroke`, `size`, `opacity`, `fillOpacity`, `strokeOpacity`, `strokeWidth`, `strokeDash`, `shape`, `angle`, `text`, `theta`, `radius`, `theta2`, `radius2` as `field`, literal `value`, or literal `datum` where the channel permits (`shape` on a `point` mark, `angle` on a `point`, `circle`, `square`, or `text`, `strokeDash` on every mark but a `rect`, an `area`, a `trail`, a `text`, and an `arc`, `size` on every mark but an `area` and an `arc` (a `rect` ignores it without a report), `text` on a `text` mark, `theta` and `radius` on an `arc` and a `text` mark, `theta2` and `radius2` on an `arc`); `xOffset` and `yOffset` (a **nested offset scale** inside the band of a nominal or ordinal `x` or `y` (with or without a time unit), or inside the time unit of a temporal `x` or `y` with a time unit other than `day` and `weekday` (not on a `rect`, not binned or `maxbins`), drawn by every mark: a discrete offset by a band scale for a bar, rect, rule, or tick and a point scale for the others, a quantitative or temporal offset by a linear or time scale of the other marks, as a grouped bar chart, a strip, or a jitter plot; since Phase 14 the **ranged offset**: an unbinned quantitative offset of a `bar` or `area` over a nominal or ordinal position without `x2` or `y2`, which spans inside the band from the zero of a linear offset scale to the value, with the ticks and labels of the position at the baseline); `x2` and `y2` (the range of a `bar`, `rect`, `rule`, `area`, `errorbar`, or `errorband` that has the channel they extend; a field, a datum, or a pixel `value`); `xError`, `xError2`, `yError`, and `yError2` (the error of an `errorbar` or `errorband`); `row`, `column`, and `facet` of a unit view that can be faceted (the facet of a view: `field`, `type`, `title`, `sort`, `header`, `columns`, and the layout properties); `order` (the order of a stack, the sort of the points of a path, the draw order of the marks) and `detail` (the grouping of the points of a path) on every mark; `tooltip`, `href`, `description`, and `key` are accepted and ignored without a report | Every other recognized channel: `vl-channel-<name>`; a supported channel that the mark does not draw (`shape`, `angle`, `strokeDash`, `size`, `text`, `theta`, `radius`, `theta2`, `radius2`): `vl-channel-<name>` (ignored); `row`, `column`, and `facet` of a layer, of a layered view, or in the `spec` of a facet: `vl-channel-row`, `-column`, `-facet` (ignored; the `facet` operator is the alternative); `x2` and `y2` of another mark or without their primary channel: `vl-channel-x2`, `vl-channel-y2`; `xError`, `xError2`, `yError`, `yError2` on a mark other than `errorbar` and `errorband`: `vl-channel-xerror`, `vl-channel-xerror2`, `vl-channel-yerror`, `vl-channel-yerror2` (ignored); an `xOffset` or `yOffset` whose position channel is missing, is quantitative, is temporal without a `timeUnit`, or is temporal with a `day`, `weekday`, binned, or `maxbins` unit (or the mark is a `rect`): `vl-offset-position`, and a continuous (quantitative or temporal) offset of a `bar` or `area` that is not a ranged offset (a temporal or binned offset, a position with `x2` or `y2` or with a time unit): `vl-offset-ranged` (both ignored: the marks are drawn at the position); the `size` channel of an `errorbar`: `vl-channel-size` (Vega-Lite drops it); `longitude`, `latitude`, `longitude2`, `latitude2`: `vl-channel-geo` |
| Field types | `quantitative`, `temporal`, `nominal`, `ordinal` (a temporal field is date-parsed, has a time scale, and is labeled as dates) | `geojson`: `vl-type-geojson` |
| Encoding properties | `field`, `type`, `value` (a constant, or `{"expr": ...}` for color, fill, stroke, opacity, and the size of a symbol, line, trail, or text mark), `datum` (a number or text, a date text on a temporal channel), `timeUnit` (all 40 local unit names, from `year` to `milliseconds` and their combinations, and the `utc` forms, as a name or as the object `{unit, maxbins, step, utc, binned}`, on a channel, a condition, a predicate, and the `timeUnit` transform: the channel then reads the start of the unit, and a bar, rect, or tick that has no secondary channel also its end; a `binned` unit reads the field itself, whose data is floored already, and adds only the end), `title`, `scale`, `axis`, `legend` (an object of legend properties, or null), `aggregate` (every operation, with `argmax` and `argmin` as `{"argmax": field}` and the exponential operations as `{"exponential": r}`; a count needs no field), `bin` on `x` and `y` (true, an object of `maxbins`, `step`, `steps`, `minstep`, `divide`, `base`, `anchor`, `nice`, and an `extent` (two numbers, or `{"param": name}`, which sets the span that chooses the step), or `"binned"` with `x2` or a `step`) and on the channels that have a scale (true or an object, with Vega-Lite's default of 6 bins, 4 for `strokeDash`, and 10 for `angle`; also the `text` channel, whose label is then `start – end`), `stack` (`zero`, `center`, `normalize`, true, false, null, also on the polar `theta` and `radius`; a bar, an area, and an arc stack by default and the other stackable marks only with an explicit `stack`), and `sort` (ascending, descending, null, an array of values, a channel shorthand such as `"-y"`, `{"encoding", "order"}`, or an object with any aggregate `op` but `argmin`, `argmax`, `values`, and the exponential ones, an optional `field`, and an optional `order`), `impute` (the `value`, `method`, `frame`, and `keyvals` of an `x` or `y` channel, or null to switch off the implicit zeros of the layers of a stacked line, area, or trail), `format` and `formatType` (`number`, `time`, `utc`) of a `text` channel, and `condition` on the color, fill, stroke, opacity, fillOpacity, strokeOpacity, strokeWidth, strokeDash, size, shape, and angle channels (an object or an array of objects with a `test` - an expression, or a predicate of `equal`, `lt`, `lte`, `gt`, `gte`, `range`, `oneOf`, `valid`, `and`, `or`, `not`, or a variable parameter or a selection parameter, `param` or the legacy `selection`, with `empty`) - and a `value`, or a field definition: the first condition that holds gives the row its value, else the channel's own value or scale, else the default of the mark; conditions alone create no legend) | `impute` of a channel other than `x` and `y` is ignored without a report, as Vega-Lite reads `impute` from `x` and `y` only (`vl-encoding-impute` is retired in Phase 13); a stacked line, area, or trail whose dimension is binned: warning `vl-encoding-stack-impute-bin` (the layers are not imputed); a `bandPosition` on a field with neither a time unit nor a bin, or with a bin: `vl-encoding-bandposition` (ignored; not the `bandPosition` of a stacked `theta` or `radius` of a text mark); a `formatType` of a `text` channel other than `number`, `time`, and `utc`: `vl-format-type-custom` (the default format is used); a `bandPosition` or `timeUnitBandPosition` other than 0.5 on a bar or rect with a time unit: `vl-encoding-bandposition-rect` (0.5 is used); a date-time object as the `datum` of a channel: `vl-encoding-datum-datetime` (the channel is ignored); `condition` on another channel: `vl-encoding-condition`; `bin` of a channel that cannot bin (`order`, a secondary channel, `theta`, `radius`) and `"binned"` on a channel other than `x` and `y`: `vl-encoding-bin`; a `bin` whose `extent` is a parameter is supported (`vl-encoding-bin-extent-param` is retired); an explicit `stack` on a scale that is not linear: `vl-encoding-stack-nonlinear` (the marks of a log, pow, sqrt, or symlog scale are not stacked); a `sort` that is not applied: `vl-encoding-sort` |
| Scales | Types linear, log, pow, sqrt, symlog, time, utc, sequential, band, point, ordinal, quantile, quantize, threshold, and bin-ordinal, accepted for a channel and a field type as Vega-Lite's `channelSupportScaleType` and `scaleTypeSupportDataType` allow (a type that cannot be used is replaced by the default one with a warning; a temporal field accepts only `time` and `utc`; a diverging color scale is a sequential one with a `domainMid`); a temporal field has a `time` scale (a `utc` scale for a `utc` unit; on a color channel the sequential scale over its instants) with the d3-time tick intervals and `nice` in the calendar, and an ordinal or nominal field with a time unit a band or point scale over its dates; a log scale with the `base`, a pow scale with the `exponent`, a symlog scale with the `constant`; a log scale has no zero, is rounded out to powers of its base, and refuses a domain that includes zero; a time scale has no zero and no default `nice`; the bin-ordinal scale of a binned quantitative color, with one color per bin and the ranges of the bins in its legend; the properties `type`, `domain` (an array, of dates for a temporal field: timestamps, date texts, or date-time objects; `"unaggregated"` for a channel that aggregates with `mean`, `median`, `q1`, `q3`, `min`, or `max`; or, since Phase 12, `{"param": name}`: the array that a variable parameter holds, or the range of a field of a selection (`field` or `encoding` name it; else the first projected field), is the domain as it is, with no zero rule, bounds, padding, or `nice`, and the marks are clipped; the `extent` of the `bin` of a channel and an interval selection with `"bind": "scales"` set the domain the same way), `domainRaw` (an array, which is the domain as it is, for a continuous scale of at least two numbers or dates and for a band, point, or ordinal scale), `domainMid`, `domainMin`, `domainMax`, `range` (an array, or the name of an entry of `config.range`), `rangeMin`, `rangeMax`, `zero`, `nice` (also a number, an interval name, or `{interval, step}` on a time scale), `padding`, `reverse`, `paddingInner`, `paddingOuter`, `align`, `round`, `clamp`, `interpolate` (`rgb`, `hsl`, `hsl-long`, `lab`, `hcl`, `hcl-long`, `cubehelix`, `cubehelix-long`, with a `gamma` for sRGB and cubehelix), and `scheme` (any of the 68 Vega color schemes by name, a list of colors, or an object with `name`, `count`, and `extent`); the polar scales of `theta` (the full circle) and `radius` (from zero to half of the smaller side of the data rectangle) of an arc and a text mark, with the types of a position (the band scale of an arc and the point scale of a text mark for a discrete field), `rangeMin`, `rangeMax`, `reverse`, and an explicit range, and with no padding, no `nice`, and no axis; the domain of a stacked, ranged, binned, or time-unit field; the nested offset scales of `xOffset` and `yOffset` (a position that holds an offset is a band scale whatever the mark is, with `config.scale.bandWithNestedOffsetPaddingInner` and `...Outer`; the offset scale spans its bandwidth, with `config.scale.offsetBandPaddingInner` and `...Outer`; the `for` of a `width` or `height` step) | `identity` scale type: `vl-scale-identity`; every other scale property (`bins`, `interval`, and so on): `vl-scale-property-<name>`; a raw domain that no scale can use (not an array, fewer than two numbers or dates for a continuous scale, a composite entry), from `domainRaw` or from the value of a parameter: warning `vl-scale-domain-raw` (the scale keeps its own domain; `vl-scale-property-domainRaw` is retired, as `domainRaw` is supported); the `cubehelix` and `cubehelix-long` interpolations are drawn since Phase 13 (`vl-scale-interpolate-cubehelix` is retired); a type that the channel or the field type cannot use (also an explicit `point` scale on a position that holds an offset): warning `vl-scale-type-ignored`; a `scale.zero` on a temporal field with a time scale: warning `vl-scale-zero-ignored`; an interval as `nice` on a scale that is not a time scale: warning `vl-scale-nice-interval`; an unknown scale type value or an unknown color scheme name is a `VegaLiteSpecException` |
| Axes | Grid, ticks, labels, domain, title, orient, offset, minimum and maximum extent, `tickCount` (also an interval name or `{interval, step}` on a time axis), `tickMinStep`, `values` (also dates), `format` (a d3 number format, a d3 time format for dates, or an object of multi-format levels), `formatType` of `number`, `time`, or `utc`, `labelAngle`, `labelAlign`, `labelBaseline`, `labelPadding`, `labelOffset`, `labelLimit`, `labelFlush`, `labelFlushOffset`, `labelOverlap`, `labelSeparation`, `labelExpr` (an expression of the label that runs before the labels are laid out), the font, color, opacity, dash, width, and cap properties of the parts, `titleAnchor`, `titleAngle`, `titlePadding`, `zindex`, and the configuration precedence of Vega-Lite (with `axisTemporal` for time axes); time axes with the d3 time ticks at local midnights and month starts of the zone, default labels from Vega's multi-format or the specifier of the time unit | `axis.encode`: `vl-guide-encode`; `labelBound`, `tickBand`, `tickExtra`, `position`, `translate`, `bandPosition`, `titleX`, and `titleY` are drawn since Phase 13 (`vl-guide-axis-<name>` is retired); a `formatType` other than `number`, `time`, and `utc`: `vl-format-type-custom`; an interval as `tickCount` on a scale that is not a time scale: warning `vl-guide-axis-tickcount-interval`; the unsupported functions of a `labelExpr`: `vl-expr-function-<name>` (see _Expressions_) |
| Legends | Color, fill, stroke, shape, size, opacity, strokeWidth, and strokeDash legends: symbol legends (one entry per value of a discrete scale, per tick of a continuous one, or per class of a discretizing one; the symbol of a line, rule, or trail is a stroke shape and the symbol of an area, arc, or text mark a filled circle), gradient legends for a continuous color scale (a temporal color has the time ticks of its domain and `config.timeFormat` labels) and discrete gradients for a discretizing one, symbol legends of dates (a size or opacity over a time scale, the dates of a time unit), merged when channels share a field and a domain, laid out by orient (right, left, top, bottom, the four corners, none) with the `config.legend` and `config.legend.layout` properties, `legend: null` to switch one off, `clipHeight` of a symbol legend (since Phase 14 the entries sit at half of it and each symbol is clipped to its entry; a gradient legend ignores it, as Vega does), `legend.labelExpr` (the datum has `index`, `label`, and `value`, and `perc` and `perc2` for gradients; `config.legend.labelExpr` is not read, as Vega-Lite has none); the legends of a shared channel of a composition merged into one at the highest view that shares them and placed beside its whole grid | `legend.encode`: `vl-guide-encode`; the unsupported functions of a `legend.labelExpr`: `vl-expr-function-<name>`; a `legend.formatType` other than `number`, `time`, and `utc`: `vl-format-type-custom`; a `symbolType` that is neither a symbol name nor SVG path data that parses: warning `vl-shape-path-invalid` (a circle is drawn; a path is drawn since Phase 13) |
| Titles | `text` (a string or an array of lines), `subtitle`, `anchor`, `orient`, `offset`, `frame`, the font, color, and alignment properties, `limit`, `angle`, `dx`, `dy`; the title of a composition (anchored at the start of its grid) and of each chart in it; the labels and titles of the facet headers (`header` of a facet field: `title`, `labels`, `format`, `formatType` of `number`, `time`, or `utc`, the `label*` and `title*` properties, `labelOrient`, `titleOrient`, and `labelExpr` (with `datum.value` and `datum.label`); with `config.header`, `headerRow`, `headerColumn`, and `headerFacet`) | `{"expr": ...}`: `vl-expr-ref` (also in a facet definition or a `header`); the unsupported functions of a `header.labelExpr`: `vl-expr-function-<name>`; a custom `header.formatType`: `vl-format-type-custom`; a header side that is not drawn: `vl-header-orient` |
| Layout | `width` and `height` as numbers, `{step}` (for a layered view applied to the combined categories of the layers), or `"container"` (the container size is an option; Vega-Lite's `fit` to it with `contains: padding`); `padding` as a number or object; `autosize` of `pad`, `none`, `fit`, `fit-x`, and `fit-y` (with `contains`) for a single or layered view, a fit dropped with `vl-layout-fit-step` for a step size; `background`; the `view` configuration (size, fill, stroke); and, for a composition, `spacing` (20 by default), `columns`, `align`, `center`, and `bounds`, with the size of the charts inside it (a `width`, `height`, or `autosize` of the composition itself is not used) | `{"expr": ...}` in a size, padding, background, or view: `vl-expr-ref`; `width`, `height`, or `autosize` of a composition and `"container"` of a child: `vl-composition-property-ignored`; a step-sized dimension of a fit: `vl-layout-fit-step` |
| Configuration | Vega-Lite 6 and Vega defaults, the specification's `config`, and the caller's `Config`, merged in that order; the `style` mechanism; the default categorical palette is `tableau10`; `config.range` (the named ranges `category`, `ordinal`, `ramp`, `heatmap`, `diverging`, and `symbol`), `config.scale` (including `quantileCount` and `quantizeCount`), `config.legend` with `config.legend.layout`, `config.concat` and `config.facet` (`spacing`, `columns`), and `config.header`, `headerRow`, `headerColumn`, and `headerFacet` | N/A (unknown keys are ignored) |
| Expressions | A closed subset of the Vega expression language in the supported sites: literals (null, booleans, numbers, strings, regular expressions, arrays, objects) and the constants `PI`, `E`, `NaN`, `Infinity`; `datum`, `datum.x`, `datum['a b']`, member and index access; the unary `+ - !`, the arithmetic, comparison, and equality operators, the short-circuit logical and-or operators, and `?:`; 113 whitelisted pure functions (math, text and array, type and conversion, regular expressions, the selection functions, the number `format` (d3-format, en-US), the nine probability distribution functions `densityNormal`, `cumulativeNormal`, `quantileNormal`, `densityLogNormal`, `cumulativeLogNormal`, `quantileLogNormal`, `densityUniform`, `cumulativeUniform`, and `quantileUniform` (since Phase 14, with the JavaScript defaults and coercions of `vega-statistics`), and the date and time functions: `datetime`, `utc`, `time`, `timezoneoffset`, the local and `utc` getters including `isoweek`, `timeOffset`, `timeSequence`, `timeUnitSpecifier`, `timeFormat`, `timeParse`, the name formatters, and `toDate`, all on the calendar of the call); parameter references. Supported sites: the `expr` of a variable parameter, `calculate`, a string `filter`, and the `{"expr": ...}` paint values of the drawn view (mark `color`, `fill`, `stroke`, `opacity`, `strokeWidth`, and the `size` of a symbol, line, trail, or text mark; the encoding `value` of `color`, `fill`, `stroke`, `opacity`, and the size of a symbol, line, trail, or text mark), which are evaluated for every row, and (since Phase 12) every `{"expr": ...}` reference (`ExprRef`) at the **static sites** that Vega-Lite's schema allows - the title, the `padding` and `background` of the top-level view, the other mark properties (also of the overlays and the parts of a composite mark), the properties of an axis, a legend, a header, a scale, and the view background, the `value` and `datum` of the other channels, and the same properties in the `config` of the top-level view - which are evaluated **once per call** with the parameters at their initial state (no row), and the `labelExpr` of axes, legends, and facet headers and of `config.axis*` and `config.header*` (evaluated on a datum with `value` and `label` and, by guide, `index`, `tickIndex`, `perc`, and `perc2`; an array result is a multi-line label (several lines in an axis, a legend, and a facet header); run on the evaluator of the call, so on the same step budget, string limit, and parameters) | A function outside the table (`now`, `luminance`, the sampling functions `random`, `sampleNormal`, `sampleLogNormal`, and `sampleUniform`, color, geographic, scale, and data-flow functions): `vl-expr-function-<name>` (`now()` is never a clock and `random()` never a random source: they are reported and null); `signal`, `event`, `item`, and an undeclared `width`, `height`, `padding`, `autosize`, `background`, or `cursor`: `vl-expr-view-state`; the bitwise (including the unary `~`) and shift operators and `in`: `vl-expr-construct-<name>` (each evaluates to null, at the expression string with the offset in the message); an `{"expr": ...}` where it is not evaluated: at a site that Vega-Lite's schema does not allow (a `width`, a `height`, or an expression outside the compiled sites), or at a static site whose expression reads `datum`, which has no row there (the property keeps its default): `vl-expr-ref`, and, at render time, a static reference whose value no property can hold (a date anywhere but in a scale domain, a regular expression): warning `vl-expr-ref` (the reference is ignored); the functions and constructs of a `labelExpr` are reported like those of any other expression, at its text (`vl-expr-label` is retired and never produced again; the number `format()` is evaluated like `timeFormat`, and `vl-expr-function-format` is retired). A syntax error, an over-long or over-deep expression, or a reference to an undeclared parameter is a `VegaLiteSpecException` when the document is opened, at a static site as well |
| Transforms | `calculate`, `filter` (an expression or the predicates `equal`, `lt`, `lte`, `gt`, `gte`, `range`, `oneOf`, and `valid`, composed with `and`, `or`, and `not`, with a `timeUnit` or a date-time object as the value, and `param` of a variable parameter or a selection parameter, with `empty`, and the legacy `selection`), `fold`, `flatten`, `sample`, `pivot` (every aggregate operation), `lookup` (inline, named, or local-file data, by `from.key`, `fields`, `as`, and `default`), `aggregate` and `joinaggregate` (every operation: `count`, `valid`, `missing`, `distinct`, `sum`, `product`, `mean`, `average`, `variance`, `variancep`, `stdev`, `stdevp`, `stderr`, `median`, `q1`, `q3`, `ci0`, `ci1`, `min`, `max`, `argmin`, `argmax`, `values`, `exponential`, and `exponentialb`), `window` (`row_number`, `rank`, `dense_rank`, `percent_rank`, `cume_dist`, `ntile`, `lag`, `lead`, `first_value`, `last_value`, `nth_value`, `prev_value`, `next_value`, and every aggregate operation over a `frame`, with `sort`, `groupby`, and `ignorePeers`), `bin` (the Vega bin algorithm with its parameters), `stack` (`zero`, `center`, `normalize`), and `impute` (`value`, `mean`, `median`, `max`, `min`, with `keyvals` and `frame`), and `timeUnit` (`{timeUnit, field, as: [start, end]}`, with the object form and `utc` units), and, since Phase 13, `regression` (`linear`, `log`, `exp`, `pow`, `quad`, `poly`, with `order`, `extent`, `params`, `groupby`, and `as`), `loess`, `density`, `quantile`, and `extent` (which sets a parameter), of every drawn view (the single view, a layered view, each layer, and each child of a composition: the transform of a layered view or a composition applies first), in the written order | `vl-param-extent-static` (warning: a static site that reads the parameter of an `extent` transform sees null); the transforms of the views of a `concat`, `hconcat`, `vconcat`, `facet`, or `repeat` are applied like those of any drawn view (the Phase 10 message "not applied: the view is a '<composition>' composition" is retired in Phase 11); parts of a supported transform: `vl-predicate-timeunit-part` (a `week` or `dayofyear` part of a predicate time unit is not applied, as in Vega-Lite), `vl-transform-lookup-param` (not applied; `vl-transform-pivot-op` is retired in Phase 13: a pivot computes every aggregate operation, and the identifiers `vl-transform-regression`, `-loess`, `-density`, `-quantile`, and `-extent` are retired); a `bin` transform whose `extent` is a parameter is supported (`vl-transform-bin-extent-param` is retired); recoverable findings at render time: warnings `vl-expr-runtime` and `vl-transform-runtime`. An operation name that does not exist and a malformed aggregating transform are a `VegaLiteSpecException` |
| Parameters | Variable parameters of the root view with a `value` or an `expr` (the expression wins), evaluated once per call in dependency order **before anything else reads them**, overridden by `VegaLiteRenderOptions.Parameters` (a value of the caller can be a scalar, an array, or an object, and replaces the whole definition), read by expressions by name, by the expression references of the static sites, by a filter `{"param": name}`, and, as the domain of a scale (`scale.domain` of the form `{"param": name}`, an array value), by the scale resolver; `bind` is ignored. **Selection parameters** (`select` of `point` or `interval`, and the legacy `selection`) are drawn in their initial state (`SelectionModel`): the `value`, or the caller's `Parameters` entry, is the initial tuple of the selection, the conditions and filters on the selection test each row as Vega-Lite does, and `empty` and `resolve` are honored; `bind: "scales"` sets the domain of the scales and `bind: "legend"` dims the entries of the legend that the selection does not hold; the input-widget `bind`, `on`, `clear`, `toggle`, `nearest`, `translate`, and `zoom` are ignored | A selection with an animation: `vl-param-selection-animation`; a point selection over the identity of the rows with a value: `vl-param-selection-id`; a selection that two layers of one layered view receive: `vl-param-selection-layer`; a projection that cannot be built (a channel without a field, an aggregate, a value key that is not a channel for an interval): warning `vl-param-selection-projection`; a variable parameter of a nested view (Vega-Lite compiles only those of the top-level view, so it declares no signal and is ignored, and an expression that reads it evaluates to null): `vl-param-variable`. A parameter cycle, a repeated or reserved name, a reference to an undeclared parameter, and a `param` or `selection` predicate that names no declared selection are a `VegaLiteSpecException` |
| Time | The calendar of `VegaLiteRenderOptions.TimeZone` (UTC by default; the host's zone and clock are never read); temporal fields with implicit date parsing and the `date:'%fmt'` and `utc:'%fmt'` parse formats; every local time unit and its `utc` form (name or object, `maxbins` included, and the `binned*` units) in an encoding, a `timeUnit` transform, a predicate, a sort array, and a domain; date-time objects in domains, sorts, predicates, axis and legend `values`, and domain bounds; time and UTC scales with ticks and `nice` (an interval name, a number, or `{interval, step}`); d3-time-format labels in the English locale with Vega's multi-format and the unit specifiers; bars and rects that span their unit, points at its start, and the `timeUnitBandSize` and `timeUnitBandPosition` of the mark; the date and time expression functions; the labels of a text mark that formats a date (the `format`, else the unit specifier, else `config.timeFormat`) | `bandPosition` other than 0.5 on a bar or rect with a time unit: `vl-encoding-bandposition-rect`; a date-time object as `datum`: `vl-encoding-datum-datetime`; `week` and `dayofyear` parts of a predicate unit: `vl-predicate-timeunit-part`; `now`: `vl-expr-function-now`; a custom `formatType`; locales other than English; the `time` (animation) channel |
| Fonts | The bundled Liberation Sans, Serif, and Mono for generic and Arial-like families; named families through `VegaLiteFontOptions` (`UseSystemFonts` is `false` by default) | N/A (an unresolvable family falls back to the bundled sans font) |
<!-- markdownlint-enable MD013 -->

The **stable report identifiers** are part of the public contract of the package and are listed in the XML
documentation of the internal `FeatureCatalog` type. Identifiers are lower case, of the form `vl-<area>-<detail>`; a
name taken from the specification is lower-cased without separators (`timeUnit` gives `timeunit`), and so is the name
of an expression function (`geoArea` gives `vl-expr-function-geoarea`; the message keeps the name as written):

<!-- markdownlint-disable MD013 -->
| Identifier | Kind | Reported at, and when |
| ---------- | ---- | --------------------- |
| `vl-schema-version` | Warning | `$schema`, when the major version is not 5 or 6 or the URL names no Vega-Lite version |
| `vl-data-empty` | Warning | The `data` object, when it has an empty `values` array or no source key |
| `vl-data-remote-url` | Unsupported | The `data` object, when `url` has a scheme of two or more characters other than `file`, is a `file:` URI with a host (`localhost` included) or a malformed one, or is a network or UNC path (a leading `//` or `\\`); a local path (relative, absolute such as `/etc/hosts`, a drive path, or a `file:` URI without a host) is supported and not reported |
| `vl-data-parse-failed` | Warning | The `data` object, at render time only (it needs the data): one per field and type, when an explicit or implicit `parse` could not convert some values (a date that does not parse included); those values are null |
| `vl-data-values-text` | Unsupported | `data/values` given as a string, or a top-level dataset that `data.name` refers to given as a string |
| `vl-data-geo` | Unsupported | `sphere`, `graticule`, a topojson format, or `values` (or a referenced dataset) given as an object |
| `vl-mark-<type>` | Unsupported | `mark`, for every type except bar, rect, point, circle, square, rule, tick, line, area, trail, text, arc, image, boxplot, errorbar, and errorband (that is, `geoshape`) |
| `vl-mark-image` | Unsupported | The `image` mark when the caller supplied no `VegaLiteRenderOptions.ImageResolver`: the mark is laid out and draws no picture (the library never reads a file or a network resource on its own) |
| `vl-mark-image-unresolved` | Warning | A picture that the resolver did not give (it returned `null`); produced by the painter at the mark, once for each link, never an unsupported feature |
| `vl-channel-url` | Unsupported | The `url` channel of a mark other than `image` (ignored, as Vega-Lite ignores it) |
| `vl-mark-invalid-show` | Retired | Reported until Phase 13; never produced again and never reused. The `invalid` of a mark definition of `"show"` or null is drawn: a row whose continuous channel value is not a number is drawn at the scale output of zero (when the domain contains zero and the scale is not a time, UTC, or log scale) or of the first value of the domain |
| `vl-mark-invalid-domains` | Warning | The `invalid` of a mark definition when it is `"filter"` or `"break-paths-filter-domains"`: the scale domains still include the invalid rows |
| `vl-mark-interpolate-bundle-area` | Unsupported | The `interpolate` of an `area` when it is `"bundle"`; the area is drawn with the `linear` interpolation |
| `vl-channel-<name>` | Unsupported | A channel other than x, y, xOffset, yOffset, color, fill, stroke, size, opacity, shape, strokeWidth, strokeDash, fillOpacity, strokeOpacity, angle, text, theta, radius, theta2, radius2, order, and detail (tooltip, href, description, and key are ignored); `shape` is drawn by a point mark, `text` by a text mark, `angle` by a point, circle, square, or text mark, `theta` and `radius` by an arc or text mark, `theta2` and `radius2` by an arc mark, `strokeDash` by every mark but a rect, area, trail, text, and arc, and `size` by every mark but an area and an arc, and the channel is reported and ignored on any other mark; `x2` and `y2` are accepted for a bar, rect, rule, area, errorbar, or errorband with the channel they extend and report `vl-channel-x2` and `vl-channel-y2` otherwise; `xError`, `xError2`, `yError`, and `yError2` are drawn by an errorbar and an errorband and reported on any other mark (`vl-channel-xerror`, `-xerror2`, `-yerror`, `-yerror2`); `order` and `detail` are accepted by every mark and no longer reported |
| `vl-composite-grouping` | Retired | Reported until Phase 13; never produced again and never reused. A channel of a boxplot, errorbar, or errorband that groups the rows is transformed as Vega-Lite's `extractTransformsFromEncoding` does: a `bin` becomes a `bin` transform before the aggregate (the start and end group the rows and the channel reads the start as binned data, with `x2` or `y2` for the end), a `timeUnit` (also the `maxbins` form) a `timeUnit` transform, and an `aggregate` a measure of the aggregate of the mark that the channel reads (`mean_age`, `__count`, `argmax_by.field`) |
| `vl-offset-position`, `vl-offset-ranged` | Unsupported | An `xOffset` or `yOffset` that is ignored (the marks are drawn at the position of `x` or `y`): `vl-offset-position`: the position channel is missing, is quantitative, is temporal without a `timeUnit`, or is temporal with a `day`, `weekday`, binned, or `maxbins` unit, or the mark is a `rect` (a temporal position with another time unit nests the offset in its unit and is not reported), `vl-offset-ranged` (narrowed in Phase 14: `OffsetDecision.UnsupportedContinuous`) for a continuous (quantitative or temporal) offset of a `bar` or `area` that is not a ranged offset: a temporal or binned offset, a position with `x2` or `y2`, a temporal position with a time unit (an unbinned quantitative offset of a bar or area over a nominal or ordinal position without a secondary channel is Vega-Lite's ranged offset and is drawn since Phase 14, not reported); reported at the offset channel definition |
| `vl-layer-property-ignored` | Warning | A `width`, `height`, `autosize`, `padding`, `background`, `config`, `datasets`, or `title` of a view inside a layered view: only the top-level view decides them |
| `vl-composition-property-ignored` | Warning | A `width`, `height` (`"container"` included), or `autosize` other than `pad` of a concatenation, a facet, or a repeat of rows or columns; the `"container"` size of a view inside one; and the `padding`, `background`, `config`, `datasets`, or `autosize` of a view inside one (produced by the catalog; the composition is drawn as `pad`). It is not one of the retired `vl-composition-<name>` identifiers |
| `vl-concat-axis-shared`, `vl-composition-columns-ignored`, `vl-repeat-ref-unresolved` | Warning | Produced by the normalization of the compositions and delivered by the renderer (once, from the drawn tree): `resolve.axis.x` or `y` of `shared` on a concatenation or repeat (the axes stay independent), `columns` next to a `row` and `column` mapping of a facet or repeat, and a `{"repeat": ...}` reference that the enclosing repeat does not define (the channel is dropped) |
| `vl-header-orient` | Unsupported | A `header.labelOrient` or `titleOrient` of a facet field on a side that the header does not draw on (the default side is used) |
| `vl-facet-field-transform` | Retired | Reported until Phase 13; never produced again and never reused. The `bin`, the `aggregate`, and the `timeUnit` of a facet field (a unit, a binned unit, and the `maxbins` form) are applied as Vega-Lite's facet model applies them: a `bin` is the cell (its start, labeled `start – end`), an `aggregate` reads the field of the name `vgField` gives (`sum_v`, `__count`; Vega-Lite's facet model has no aggregate node), a `timeUnit` is the start of the unit (a binned unit leaves the raw value as the cell); the title of the header is the default title of the definition. `vl-facet-sort` is retired (a `sort` by `{field, op}` orders the cells by that aggregate) |
| `vl-layout-fit-step` | Warning | An `autosize` of fit, fit-x, or fit-y whose width or height is sized by a step (a `{"step": n}`, or no size on a discrete scale): Vega-Lite drops the fit for that dimension, and so does the renderer; produced by the layout, not by the catalog. `vl-layout-autosize-<type>` (fit, fit-x, fit-y) is retired in Phase 11 and never produced |
| `vl-layer-scale-conflict` | Warning | Delivered at render time, at the channel of a layer whose scale of a shared channel cannot be merged with the others (a discrete with a continuous scale, a linear with a logarithmic or time scale, different discretizing scales): the layer keeps its own scale |
| `vl-channel-geo` | Unsupported | `longitude`, `latitude`, `longitude2`, `latitude2` |
| `vl-channel-row`, `-column`, `-facet` | Unsupported | A `row`, `column`, or `facet` channel of a view that cannot be faceted: a layer of a layered view, a layered view whose encoding has the channel, or the `spec` of a facet (the channel is ignored). Narrowed in Phase 11: the channels of a unit view that is faceted are drawn and not reported |
| `vl-type-geojson` | Unsupported | The `type` of a channel |
| `vl-shape-path-invalid` | Warning | A shape that is neither a symbol name nor SVG path data that parses: the `shape` of a mark, the `value` of a `shape` channel, an entry of its `scale.range`, or the `symbolType` of a legend; a circle is drawn (Vega throws for such a path), and a path with no command draws nothing and is reported too. Custom SVG path shapes are drawn since Phase 13; the identifier `vl-shape-path` is retired and never produced again |
| `vl-scale-interpolate-cubehelix` | Retired | Reported until Phase 13; never produced again and never reused. A `scale.interpolate` of `cubehelix` or `cubehelix-long` is interpolated in the cubehelix color space |
| `vl-scale-type-ignored` | Warning | A `scale.type` that the channel or the field type cannot use (the default type is used), including any type but `time` and `utc` on a temporal field, `time` and `utc` on a field that is not temporal, and a temporal field on a `shape` or `strokeDash` channel; and a quantitative field on a `shape` or `strokeDash` channel without a discretizing scale type (the scale is ordinal); and an explicit `point` scale type on a position that holds an `xOffset` or `yOffset` (the band scale is used) |
| `vl-scale-zero-ignored` | Warning | A `scale.zero` on a temporal field with a time or UTC scale or a color scale (zero is not meaningful; it is ignored, as Vega-Lite does) |
| `vl-scale-nice-interval`, `vl-guide-axis-tickcount-interval` | Warning | A time interval (a name or `{interval, step}`) as `scale.nice`, or as the `tickCount` of an axis or a legend, on a scale that is not a time scale; it is ignored |
| `-bandposition`, `-bandposition-rect`, `-datum-datetime` (`vl-encoding-...`) | Unsupported | That property of a supported channel (`vl-encoding-impute` is retired in Phase 13: the `impute` of a channel other than `x` and `y` is ignored without a report, as Vega-Lite reads it from `x` and `y` only; the `impute` of a position channel and the `impute` transform are supported). `-bandposition` is a `bandPosition` on a field without a time unit (or with a bin; ignored; the `bandPosition` of a stacked `theta` or `radius` of a text mark places the label in its slice and is not reported); `-bandposition-rect` a `bandPosition` or `timeUnitBandPosition` other than 0.5 on a bar or rect whose field has a time unit (0.5 is used); `-datum-datetime` a date-time object as the `datum` of a channel (the channel is ignored) |
| `vl-encoding-stack-impute-bin` | Warning | The dimension channel of a stacked line, area, or trail that is binned and has stack-by fields, unless the stacked channel says `impute: null`: the layers are not filled with zeros for the bins that they lack |
| `vl-timeunit-binned`, `vl-timeunit-maxbins` | Retired | Reported until Phase 13; never produced again and never reused. A binned unit (a name that starts with `binned`, or `binned: true`) in an encoding reads the field as it is (the data is floored already) and adds the end of the unit only where a channel spans it, in a predicate compares `time(field)`, and in the `timeUnit` transform floors like any other unit (Vega's transform has no `binned` parameter); the `{maxbins}` form without a unit in a predicate compares the field as it is, as Vega-Lite's `fieldFilterExpression` does |
| `vl-predicate-timeunit-part` | Warning | The `timeUnit` of a predicate (a filter or a condition test) whose unit has a `week` or `dayofyear` part; as in Vega-Lite a predicate does not apply those parts (the transform and the encoding do) |
| `vl-encoding-condition` | Unsupported | A `condition` on a channel that takes none (`x`, `y`, the secondary channels, `theta`, `radius`, `order`, and a conditional `text`); the condition is ignored |
| `vl-encoding-bin` | Unsupported | The `bin` of a channel that cannot bin (`order`, the secondary channels, `theta`, and `radius`), and `bin: "binned"` on a channel other than `x` and `y`, unless false or null; the channel is not binned |
| `vl-encoding-stack-nonlinear` | Unsupported | An explicit `stack` of `x`, `y`, `theta`, or `radius` whose `scale.type` is not linear; the marks of a log, pow, sqrt, or symlog scale are not stacked (Vega-Lite does not stack on them), and any other type is stacked on the linear scale |
| `vl-encoding-sort` | Unsupported | A `sort` that is not applied: a channel shorthand or an `encoding` that names a channel without a field or an aggregate (the message names the channel), or an `op` of `values` or an exponential operation |
| `vl-encoding-sort-ignored` | Warning | A `sort` array, channel shorthand, or object with a `field`, `op`, or `encoding` on a quantitative field with a continuous scale (there are no categories to order) |
| `vl-encoding-sort-mixed-types` | Warning | A `sort` array that mixes strings, numbers, booleans, or null (produced by the parser at the array; values are matched by kind) |
| `vl-scale-<type>` | Unsupported | `scale/type`, for every type except linear, log, pow, sqrt, symlog, sequential, time, utc, band, point, ordinal, quantile, quantize, threshold, and bin-ordinal (that is, `identity`) |
| `vl-scale-property-<name>` | Unsupported | A scale property outside the supported list (`domainMid`, `domainMin`, `domainMax`, `domainRaw`, `rangeMin`, `rangeMax`, `interpolate`, `base`, `exponent`, and `constant` are supported; `bins` is not; an unknown color scheme name is an error); the identifier `vl-scale-property-domainRaw` is retired and never produced again |
| `vl-scale-domain-raw` | Warning | A raw domain that no scale can use: the `scale.domainRaw`, or the value of the variable parameter that `scale.domain` names (`{"param": name}`), is not an array, or is an array of fewer than two numbers or dates for a continuous scale (or of fewer than one scalar for a band, point, or ordinal scale), or has a composite or non-finite entry; the scale keeps the domain of its data (Vega would draw every position undefined) |
| `vl-scale-invalid-value` | Unsupported | An entry of the specification's `config.scale.invalid` (Phase 14: a scale channel mapped to `"zero-or-min"` or to `{"value": number or text}` is drawn) that is neither: `null`, a boolean, or a list as the `value`, or an entry of another shape (an expression reference as the `value` is reported by `vl-expr-ref` at the value instead); reported once for each entry, at the entry. The channel is treated as having no entry, so the `invalid` mode of the mark handles its invalid rows. The entries of the render-options `Config` are not reported |
| `vl-guide-encode` | Unsupported | `axis.encode`, `legend.encode` |
| `vl-guide-legend-clipheight` | Retired | Reported until Phase 14; never produced again and never reused. A `legend.clipHeight` (or `config.legend.clipHeight`) greater than 0 on a symbol legend is drawn: the entries sit at half of it and each symbol is clipped to its entry; a gradient legend ignores it, as Vega does. `vl-guide-legend-titleorient` is retired in Phase 13: every `legend.titleOrient` is drawn |
| `vl-guide-axis-<name>` | Retired | Reported until Phase 13 for `labelBound`, `tickBand`, `tickExtra`, `position`, `translate`, `bandPosition`, `titleX`, and `titleY` of an axis; never produced again and never reused: all eight are drawn |
| `vl-format-type-custom` | Unsupported | `axis.formatType`, `legend.formatType`, `header.formatType`, and the `formatType` of a `text` channel other than `number`, `time`, and `utc` (a custom format type is not evaluated; the default format is used) |
| `vl-expr-ref` | Unsupported, and Warning | An `{"expr": ...}` value that is not evaluated: at a site that Vega-Lite's schema does not allow (a `width` or a `height`, a facet definition or `header` outside the compiled sites, an expression in a predicate) or at a static site (the title, the padding, the background, the view, the properties of the guides, the scales, and the marks, and the `config` of the top-level view) whose expression reads `datum`, which has no row there (Unsupported; the property keeps its default). An `{"expr": ...}` anywhere in a selection (the brush `mark`, `on`, the projection, the initial value; `config.selection` included) is reported too, because the schema allows none there (the brush keeps its default or configured value). The references at the other static sites are evaluated once per call and are not reported. At render time, a static reference whose value no property can hold (a date anywhere but in a scale domain, a regular expression) is the Warning `vl-expr-ref` at the reference (it is ignored). The `labelExpr` of axes, legends, and headers is evaluated and reports only the constructs below |
| `vl-expr-label` | Retired | Reported until the label expressions were implemented for `axis.labelExpr`, `legend.labelExpr`, and `header.labelExpr`; never produced again and never reused. A label expression reports only `vl-expr-function-<name>`, `vl-expr-view-state`, and `vl-expr-construct-<name>`, at the text of the expression (also for the `labelExpr` of `config.axis*` and `config.header*`); a `config.legend.labelExpr` is not read and not reported |
| `vl-expr-function-<name>` | Unsupported | The expression text of a supported site (a `labelExpr` included), for a call of a function outside the table of 113 (the name lower-cased with the invariant culture, the message keeps it as written, for example `vl-expr-function-now` for `now`, `vl-expr-function-luminance` for `luminance`, and `vl-expr-function-random` for `random` or `vl-expr-function-samplenormal` for `sampleNormal`, which evaluate to null: an empty label; `vl-expr-function-format` is retired: the number `format` is evaluated) |
| `vl-expr-view-state` | Unsupported | The expression text of a supported site (a `labelExpr` included), for `signal`, `event`, `item`, or a view signal (`width`, `height`, `padding`, `autosize`, `background`, `cursor`) that no parameter declares |
| `vl-expr-construct-<name>` | Unsupported | The expression text of a supported site (a `labelExpr` included), for a deferred operator: `bitwise-and`, `bitwise-or`, `bitwise-xor`, `bitwise-not`, `shift-left`, `shift-right`, `shift-right-unsigned`, or `in` |
| `vl-expr-runtime` | Warning | A `calculate` expression, at render time, when it produced a regular expression, which cannot be stored in a field (null is stored; one report per transform with the count and the first row) |
| `vl-transform-<name>` | (retired) | Every transform of Vega-Lite is computed, so this family is not produced any more (`regression`, `loess`, `density`, `quantile`, and `extent` were its last members and are retired in Phase 13, never reused); the transforms of every drawn view, including the children of a composition, are applied and not reported (the Phase 10 message "not applied: the view is a '<composition>' composition" is retired) |
| `vl-transform-lookup-param` | Unsupported | The part of a computed transform that is not supported: a lookup in the data of a parameter (not applied). `vl-transform-pivot-op` (a pivot `op` other than sum and count) is retired in Phase 13: a pivot computes every aggregate operation. `vl-encoding-bin-extent-param` and `vl-transform-bin-extent-param` (a bin extent that is a parameter) are retired in Phase 12: the extent sets the span that chooses the bin step |
| `vl-transform-runtime` | Warning | A pivot, a regression, a loess, a density, a quantile, or an extent at render time: a pivot's rows with a null pivot value skipped, columns cut at `limit`, or a pivot value that replaces a group field; a regression group skipped for too few rows, a log regression whose extent is not positive, or an extent without a finite value |
| `vl-param-extent-static` | Warning | A static site (a title, a mark or scale property, a size, ...) whose expression reads the parameter of an `extent` transform: the properties of a static site are fixed before the data is read, so the parameter is null there |
| `vl-composition-<name>` | Retired | `vl-composition-concat`, `-hconcat`, `-vconcat`, `-facet`, and `-repeat` (and `vl-composition-layer` since Phase 10) are never produced: every composition is drawn. They are not reused (`vl-composition-property-ignored` and `vl-composition-columns-ignored` are other, new identifiers) |
| `vl-param-variable`, `vl-param-selection-layer`, `-id`, `-animation`, `-projection` | Unsupported | A variable parameter of a nested view (`vl-param-variable`: Vega-Lite compiles only the variable parameters of the top-level view, so a nested one declares no signal and is ignored, and an expression that reads it evaluates to null); the variable parameters of the root view are computed and not reported. A selection (`select`, or the legacy `selection`) is drawn in its initial state and is not reported as a whole; the parts that are not modeled are: `-layer` (two layers of one layered view receive the selection, which Vega rejects: it is applied as empty), `-id` (a point selection over the identity of the rows has no value in a static chart: a `value`, or a value of the caller, is ignored), `-animation` (a timer or the `time` encoding), and `-projection` (a warning: a projection channel without a field or with an aggregate is skipped, an interval initialized with a key that is not a channel uses the default encodings). `vl-param-selection`, `vl-encoding-condition-param`, and `vl-transform-filter-param` are retired |
| `vl-projection` | Unsupported | `projection` |
| `vl-duplicate-key` | Warning | The later of two equal keys of one object (the last value wins) |
| `vl-unknown-property` | Warning | A key that Vega-Lite does not define for a view, mark definition, encoding, or channel definition |
| `vl-diagnostics-suppressed` | Warning | The first report beyond `MaxDiagnostics` |
<!-- markdownlint-enable MD013 -->

**Retired identifiers.** The identifiers `vl-data-file`, `vl-data-named`, `vl-data-sequence`, `vl-data-parse`,
`vl-data-format-csv`, `vl-data-format-tsv`, `vl-data-format-dsv`, and `vl-data-property` were reported before phase 4;
the constructs they named are now supported, so they are no longer produced. The identifiers `vl-expr-calculate` and
`vl-expr-filter` were reported before phase 5 for a calculate expression and a string filter, and
`vl-transform-calculate`,
`-filter`, `-fold`, `-flatten`, `-sample`, `-pivot`, and `-lookup` for the transforms of the drawn view; those
constructs
are now supported, so these reports are no longer produced for the drawn view (the transforms of a view of a composition
are applied too since phase 11). Likewise, `vl-encoding-aggregate` and `vl-encoding-stack` (and the report
of an
implicit bar stack) were reported before phase 6, `vl-transform-aggregate`, `-joinaggregate`, `-window`, `-bin`,
`-stack`,
and `-impute` for the transforms of the drawn view, and `vl-encoding-bin` for every channel; those constructs are now
supported (`vl-encoding-bin` remains for `bin: "binned"` on the channels other than `x` and `y`, and for a bin of
`order` and of the
secondary channels). `vl-scale-bin-ordinal` was reported until phase 7 and is no longer produced. Likewise, before
phase 7 `vl-guide-legend` (every legend), `vl-scale-color-ramp` (a quantitative color), `vl-channel-shape`,
`vl-channel-strokewidth`, `vl-channel-strokedash`, `vl-channel-angle`, `vl-channel-fillopacity`, and
`vl-channel-strokeopacity`, `vl-scale-<type>` for
the types that are now drawn, `vl-scale-property-scheme` (any scheme other than `tableau10`), and the
`vl-encoding-condition`
of a supported channel were reported; those constructs are now supported, so these reports are no longer produced
(`vl-channel-shape`, `-angle`, and `-strokedash` remain for a mark that does not draw the channel, and
`vl-encoding-condition` for a channel that takes no condition). Finally, in phase 8 `vl-type-temporal` (a temporal
channel), `vl-encoding-timeunit` (a time unit in an encoding), `vl-transform-timeunit` (the `timeUnit` transform of the
drawn view; a nested view reported `vl-transform-timeunit` until phase 11), `vl-transform-filter-timeunit` and
`vl-transform-filter-datetime` (a time unit or a date-time object in a filter), `vl-encoding-condition-test` (a time
unit
or a date-time object in a condition test), `vl-data-parse-date-format` (a custom `parse` date format),
`vl-scale-time` and `vl-scale-utc` (the `time` and `utc` scale types), and the `vl-expr-function-<name>` reports of the
date and time functions (`vl-expr-function-datetime`, `-timeformat`, and the others, except `-now`) were reported; those
constructs are now supported, so these reports are no longer produced, and `vl-encoding-bandposition` is narrowed to a
`bandPosition` on a field without a time unit (or with a bin). The `scale.type` of `time` or `utc` on a field that is
not
temporal is reported as `vl-scale-type-ignored`, as Vega-Lite refuses it. In phase 9 `vl-mark-line`, `vl-mark-area`,
`vl-mark-trail`, `vl-mark-text`, and `vl-mark-arc` (the marks are drawn), `vl-channel-order` and `vl-channel-detail`
(every
mark accepts them) were reported; those constructs are now supported, so these reports are no longer produced.
`vl-channel-text`, `-theta`, `-radius`, `-theta2`, `-radius2`, `-x2`, `-y2`, `-size`, `-strokedash`, `-angle`, and
`-shape`
are narrowed to a mark that does not draw the channel, `vl-encoding-impute` to a channel other than `x` and `y`, and
`vl-format-type-custom` is extended to the `text` channel; the ids `vl-mark-invalid-show`, `vl-mark-invalid-domains`,
`vl-mark-interpolate-bundle-area`, and `vl-encoding-stack-impute-bin` are new. `vl-mark-geoshape` and the
`vl-channel-geo` of a `longitude` channel are the unsupported mark and channel that the tests and the documentation use
as examples since phase 9 (they were `vl-mark-arc` and `vl-channel-theta`). In phase 10 `vl-composition-layer` (the
layer
composition is drawn) and `vl-mark-boxplot`, `vl-mark-errorbar`, and `vl-mark-errorband` (the composite marks are drawn)
were reported; those constructs are now supported, so these reports are no longer produced, and the ids
`vl-layer-property-ignored`,
`vl-layer-scale-conflict`, `vl-composite-grouping`, `vl-channel-xerror`, `vl-channel-xerror2`, `vl-channel-yerror`, and
`vl-channel-yerror2` are new. In phase 11 `vl-composition-concat`, `-hconcat`, `-vconcat`, `-facet`, and `-repeat` (the
compositions are drawn: all of the `vl-composition-<name>` family is retired) and `vl-layout-autosize-fit`, `-fit-x`,
and `-fit-y` (the family `vl-layout-autosize-<type>`: the fitting types are drawn) were reported; those constructs
are now supported, so these reports are no longer produced; the message of `vl-transform-<name>` "not applied: the view
is a '<composition>' composition" is retired too (the views of every composition are drawn, so nothing is left undrawn
by a composition and the documentation's sentinel for an unsupported view is a `projection` or a `geoshape`
mark); `vl-channel-row`, `-column`, and `-facet` are narrowed to a view that cannot be faceted; and the ids
`vl-composition-property-ignored`, `vl-concat-axis-shared`, `vl-composition-columns-ignored`,
`vl-repeat-ref-unresolved`, `vl-layout-fit-step`, `vl-facet-field-transform` (retired in phase 14), `vl-facet-sort`,
and `vl-header-orient`
are
new (`vl-format-type-custom` has a new site in the header of a facet; the retired `vl-expr-label` had one too).
They are never reused for another meaning.

In phase 13 (the remaining gaps) `vl-shape-path` (a custom SVG path as a `shape` is drawn), `vl-scale-interpolate-cubehelix`
(the `cubehelix` and `cubehelix-long` interpolations are implemented), `vl-encoding-impute` (the `impute` of a channel
other than `x` and `y` is ignored without a report, as Vega-Lite reads `impute` from `x` and `y` only),
`vl-mark-invalid-show` (the `show` and null `invalid` modes are drawn), `vl-guide-legend-titleorient` (every legend
`titleOrient` is drawn), `vl-facet-sort` (a facet `sort` by an aggregate is applied), and all eight
`vl-guide-axis-<name>` identifiers (`labelBound`, `tickBand`, `tickExtra`, `position`, `translate`, `bandPosition`,
`titleX`, and `titleY` of an axis are drawn) were reported; those constructs are now supported, so these reports are no
longer produced and the identifiers are never reused. `vl-shape-path-invalid` (a warning) is new: a shape that is neither
a symbol name nor SVG path data that parses is drawn as a circle, where Vega would throw. `vl-facet-field-transform` was
narrowed to a `bin`, an `aggregate`, or a binned or `maxbins` `timeUnit` of a facet field, and `vl-composite-grouping` to
a `bin`, an `aggregate`, or a binned or `maxbins` `timeUnit` of a grouping channel (a `timeUnit` with parts was applied
in
both).

In phase 14 (hardening) `vl-timeunit-binned` (the `binned*` units and `binned: true` are applied: an encoding reads the
field as it is and adds the end of the unit where a channel spans it, a predicate compares `time(field)`),
`vl-timeunit-maxbins` (the `{maxbins}` form in a predicate compares the field as it is, as Vega-Lite does),
`vl-facet-field-transform` (the `bin`, `aggregate`, and `timeUnit` of a facet field are applied as Vega-Lite's facet model
applies them), and `vl-composite-grouping` (the `bin`, `aggregate`, and `timeUnit` of a grouping channel of a composite
mark are transformed as Vega-Lite's `extractTransformsFromEncoding` does) were reported; those constructs are now
supported, so these reports are no longer produced and the identifiers are never reused.

**Deferred phases.** The roadmap that follows the delivered phases adds, each in its own pull request with the full
companion artifacts, the following capabilities; until then the constructs are reported with the identifiers above:

- **Data formats still deferred**: data given as text
  in `values`, and the geographic formats; remote URLs are never fetched and stay reported
- **Expressions and row transforms (delivered in phase 5, with these parts deferred)**: the color,
  geographic, scale, and data-flow functions of the expression language and the clock function `now`, view
  state (`signal`, `event`, `item`), and the bitwise operators (the number `format` and the date and time functions were
  delivered in phase 5 and 8, and the nine distribution functions in phase 14; the sampling functions `random`,
  `sampleNormal`, `sampleLogNormal`, and `sampleUniform` are not planned, see 95); and the other transforms below (the
  expressions in
  titles, sizes, paddings, and backgrounds were delivered in phase 12, milestone 1, as the static references; the
  `labelExpr` of axes, legends, and headers was delivered later)
- **Aggregation (delivered in phase 6, with these parts deferred)**: `argmin` and
  `argmax` sorts (the `extent` transform and a bin extent given by a parameter were delivered in phases 13 and 12;
  the encoding `impute` of a position channel and the `order` channel as
  the drawing order of marks were delivered in phase 9)
- **Mark-property channels, legends, and scales (delivered in phase 7, with these parts deferred)**: the `identity`
  scale type, the scale property `bins` (`domainRaw` was delivered in phase 12, milestone 1),
  `legend.encode`, a custom
  `legend.formatType` (`clipHeight` was delivered in phase 14; the brush of an interval selection and `bind: "scales"` were
  delivered in phase 12; the `cubehelix` interpolation, a custom SVG path as a `shape`, and the legend `titleOrient`
  were delivered in phase 13)
- **Time (delivered in phase 8, with these parts deferred)**: a `bandPosition` other than 0.5 on a rect-based mark
  (`vl-encoding-bandposition-rect`), a
  date-time object as a `datum` (`vl-encoding-datum-datetime`), custom format types and
  `config.customFormatTypes`, the unit `isoweek` as an interval name, a `unionWith` domain of dates, the zoom of time
  scales, the `time` (animation) channel, the tooltip `timeFormat` (the labels of a text mark that formats dates are
  drawn since phase 9), and locales other than `en-US` (the axis `labelBound` was delivered in phase 13, and the `binned`
  time units and the `maxbins` form of a predicate in phase 14)
- **Line-like and polar marks (delivered in phase 9, with these parts deferred)**: the filtering of the scale domains
  by the invalid modes
  (`vl-mark-invalid-domains`; the `show` and null modes were delivered in phase 13, and `config.scale.invalid` in
  phase 14), the area of a `bundle` curve
  (`vl-mark-interpolate-bundle-area`), the point overlay
  that an `encoding.shape` switches on, the implicit impute of a
  binned dimension (`vl-encoding-stack-impute-bin`), the `bin` of `theta` and `radius`, a custom `formatType` and a
  `condition` of a `text` channel, and the `x2` and `y2` of a line, trail, text, or arc mark
- **Layers and composite marks (delivered in phase 10, with these parts deferred)**: the tooltips of the composite
  marks, the shorthand
  aggregate `"boxplot"`, `"errorbar"`, and `"errorband"` of the measure, the log-only warnings of Vega-Lite about a
  composite mark, and the properties of the layers that only the top-level view decides
  (`vl-layer-property-ignored`) and a variable parameter declared in a layer (`vl-param-variable`) (the `bin`, `aggregate`,
  and `timeUnit` of a grouping channel were delivered in phases 13 and 14)
- **Composition (delivered in phase 11, with these parts deferred)**: a custom `header.formatType`
  (`vl-format-type-custom`), a `header.labelOrient` or `titleOrient` on
  a side that the header does not draw on (`vl-header-orient`), the `fit` and `"container"` sizes and the `autosize` of
  a composition and of its children (`vl-composition-property-ignored`: a composition is always `pad`), the
  `headerBand` and `footerBand` of the grid layout, the shared axes of a concatenation (`vl-concat-axis-shared`: the
  axes stay independent), the `autosize.resize` of a static picture, and a facet inside a facet or a composition
  inside a layer (a specification error)
- **Label expressions and offset channels (delivered after phase 11, with these parts deferred)**: `datum.size`
  and `datum.offset` of a symbol legend, a custom `formatType` with a label expression, the extra tick of a binned
  axis, `config.legend.labelExpr` (Vega-Lite has none), a continuous offset of a bar or area that is not a ranged offset
  (`vl-offset-ranged`; the ranged offset was delivered in phase 14), the
  offset of a temporal position with a `day` or `weekday` time unit, a binned or `maxbins` unit, or a `rect` mark
  (`vl-offset-position`), a `bin` on an offset channel, and the pixel
  `xOffset` and `yOffset` properties of a mark
- **Parameters**: the lookup source given by a parameter (`vl-transform-lookup-param`), the interaction
  state of a variable parameter (`bind`), the Tool's `-D` pass-through of parameter values, and the remaining uses of a
  parameter in expression sites that are not evaluated yet (the static variable parameters, the caller's `Parameters`
  overrides, the initial state of selections, the brush mark, `bind: "scales"`, `bind: "legend"` (the dimming of a
  legend), a selection as a scale domain, and the bin extent given by a parameter are delivered)
- **Advanced and hardening**: label-overlap variants, the remaining configuration keys, and a fuzzing, determinism, and
  performance audit (the `regression`, `loess`,
  `density`, `quantile`, and `extent` transforms and every `pivot` operation are delivered in phase 13)

Geographic constructs (`projection`, `geoshape`, `longitude` and `latitude` channels, topojson) are not planned and
remain reported permanently.

### Deliberate Strictness And Deviations

The following decisions differ from, or are stricter than, the Vega-Lite reference behavior; each is pinned by a test:

1. **Version 6 defaults for version 5**: a `$schema` of version 5 is interpreted with the version 6 defaults (the
   default view is 300 by 300 pixels), not the version 5 defaults.
2. **Configuration merge order**: the defaults are merged first, then the specification's `config`, then the caller's
   `Config` (the caller wins), as a true deep merge in which arrays are replaced. Vega-Lite's own embedding merges the
   specification over the option; the caller-wins order is the documented CanvasNet choice for applying a host theme.
3. **Fonts**: text is measured with the bundled Liberation fonts, which are metric-compatible with Arial, Times, and
   Courier but are not the fonts of a browser, so label widths can differ from Vega's by the difference of the
   advance widths; the layout rules themselves do not depend on font metrics. `UseSystemFonts` is `false` by default
   (unlike the core and Scad defaults) so that output does not depend on the host.
4. **Invalid data**: rows with a null, NaN, or non-numeric value on a continuous `x`, `y`, `size`, or `opacity`
   channel are dropped (the Vega-Lite 6 `filter` behavior for non-path marks) unless the mark's `invalid` is `"show"` or
   null (see 89); values outside an explicit discrete
   domain are dropped. The line, area, and trail marks break their path at such a row instead (see 50).
5. **Domain order**: discrete domains are ascending by Vega-Lite's default sort (the date values of a time-unit field
   ascending by instant), with mixed value kinds ordered null,
   boolean, number, string (a total order), and values missing from a `sort` array first.
6. **Clip**: marks are clipped only with `"clip": true`, as in Vega-Lite 6, never otherwise to the view; an explicit
   `x` or `y` scale domain does not clip (Vega-Lite's `scaleClip` is for the extent of an interval selection, which is
   not supported).
7. **Title lines**: a line feed inside title text splits the line, which Vega does not do.
8. **Painting**: clips are composited with fractional coverage; text is filled only; a shape that is neither a symbol
   name nor SVG path data that parses is drawn
   as a circle (see 90); fills are flattened at a tolerance of 0.02 pixel so that small circles stay round.
9. **Number formatting**: the d3 `en-US` locale with the minus sign U+2212, culture-invariant; widths above 1,000 are
   clamped as a denial-of-service guard.
10. **Data format by extension**: without `format.type` a `url` ending in `.csv` or `.tsv` (any case) is delimited text
    and everything else is JSON, as in Vega-Lite; `format.type` always wins. `format.header` and `format.property` on
    delimited data, and `format.delimiter` on CSV and TSV, are ignored, and a DSV without a delimiter uses a comma.
11. **Inference only on request**: as in Vega, no data-based type inference is applied unless `parse` is `"auto"`:
    delimited
    text without `parse` (or with `parse: null`) keeps every cell as a string (a leading zero or an ISO-looking date
    stays text), and a `parse` object converts only the fields it names. With `"auto"` a column is converted by the type
    of its whole column (boolean, then number, then date by the `Date.parse` rules, only when every non-empty text of
    the
    column fits;
    otherwise the column stays text), never per cell. Numbers are finite invariant integers, decimals, and exponents (no
    hex, no thousands separators, no `NaN` or infinity); a date-only ISO text is UTC and a date-time without an offset
    is
    in the chart's time zone (UTC by default), so the result never depends on
    the host. Quantitative encodings still coerce numeric text to numbers when they are drawn (as Vega does for its
    linear scales); quantitative color is drawn with the continuous color scales of phase 7, and the **temporal fields
    of
    the encoding** (and the fields of a `timeUnit` transform and of date predicates) are date-parsed implicitly without
    `parse: "auto"`, unless `parse` names them or is null (phase 8). A custom `date:'%fmt'` or `utc:'%fmt'` parse entry
    is applied with the d3 time-format parser.
12. **Failed conversions**: a value that an explicit or implicit `parse` cannot convert becomes null, with a
    `vl-data-parse-failed` warning for the field, instead of NaN or an invalid date; an empty text becomes null.
13. **Local files are hard errors**: a data file that cannot be used fails the render with a located
    `VegaLiteRenderException` in lenient mode too, because an empty chart would hide a broken deployment; only remote
    data, which is never fetched, is reported and empty. This includes a directory where a file is expected, an existing
    file that cannot be opened (never reported as missing: only the open attempt decides that), JSON data text that is
    not valid Unicode, and a callback or callback stream that throws, which are located failures ("cannot be read", or a
    located data failure) rather than a raw exception.
    Caller dataset dates are UTC; `DateTime.MaxValue` and `DateTimeOffset.MaxValue` (the usual "no end" sentinels) are
    clamped to the last supported millisecond of the year 9999, and ISO text beyond the supported date range is not a
    date.
14. **Links**: a symbolic link or junction is followed under the default `FileAccessPolicy.AllowAll`, like Pandoc;
    the policy is asked about the real path of the opened file, so `FileAccessPolicy.NoSymlinks` refuses a link below
    the host-chosen origin and `FileAccessPolicy.UnderRoots(...)` a link whose target leaves the roots, and a dangling
    link is a missing file. A folder at or above the origin, which the host chose, is trusted.
15. **Invalid sequences fail the render**: a `sequence` without a finite numeric `start` and `stop`, or with a step that
    is zero, not finite, or not a number, fails the render with a located `VegaLiteRenderException` instead of being
    defaulted (only an absent step is 1); a step that points away from the stop is an empty table, and a length that
    overflows fails the row limit before anything is allocated. The specification metadata reports an unknown length for
    these.
16. **Provenance of the data rules**: the format, parse, and inference behavior follows the public Vega and Vega-Lite
    documentation of data loading; the parsers are original code and no upstream source, dataset, or example was copied.
17. **A closed expression subset**: the language is a subset of JavaScript run by an interpreter written for this
    package,
    with 113 whitelisted functions (the number `format`, the date and time functions, and the distribution functions
    among them) and no `random`, no
    `now` or other clock, no color function, no
    `signal`, `event`, or
    `item`, no bitwise operators, no assignment, and no method calls; the parts that parse but do not run evaluate to
    null
    and are reported. The value model has one null for `null` and `undefined`, so null converts to 0 in arithmetic.
    The math functions use the host math library, so a transcendental result can differ from a browser in the last bit.
18. **Compiled when the document is opened**: an expression with a syntax error, a length over `MaxExpressionLength`
    (for a text that the user wrote; the expression that the renderer writes from a predicate is bound by eight times
    `MaxSourceLength`, item 41),
    nesting over `MaxExpressionDepth`, or a reference to an undeclared parameter is a `VegaLiteSpecException` at open
    time;
    Vega-Lite itself would fail when it builds the dataflow. `MaxExpressionDepth` counts nesting, not length.
19. **Parameters are static**: a variable parameter is evaluated once; the expression wins over the `value`; a caller
    `Parameters` entry replaces the whole definition (its expression is not evaluated); `bind` is ignored;
    a selection parameter reads as its resolved value in an expression (see the deviation 78) and is drawn in its
    initial state. Since Phase 12 (milestone 1)
    the parameters are evaluated first in every call, before the expression references of the static sites are folded
    and the data is read, and a caller's value can be an array or an object (see the deviation 77).
20. **Lookup takes the first match**: the secondary rows are indexed by the text of the key and the first row of a
    repeated
    key wins, so the result does not depend on the index implementation; this differs from an
    index that keeps the last row.
21. **Deterministic sample**: `sample` is seeded from the JSON pointer of the transform (FNV-1a, SplitMix64, reservoir
    sampling, rows kept in source order), not from a random source, so a chart renders the same on every call; the rows
    differ from the ones a browser would pick.
22. **Pivot computes every aggregate operation (Phase 13; it was limited to sum and count)**, with columns in ordinal
    order,
    each cell an aggregate of the rows of the pivot value with Vega's semantics (see deviation 83), a missing
    combination being null for the operations that need a value and 0 for the counts (the former sum of 0 is a
    deliberate fidelity change); an `op` that is not an aggregate operation is a specification error.
23. **Sort by field supports every orderable aggregate**: any aggregate operation except `argmin`, `argmax`, `values`,
    and
    the exponential ones, by field, by `op`, by channel shorthand (`"-y"`), or by `{"encoding"}`, over the **Raw** rows
    (before the implicit aggregate, as in Vega-Lite), stable for ties; a category without a valid value (a null result,
    also for `sum`, as in Vega) sorts first in either direction. Any other form is reported as
    `vl-encoding-sort`. A sort array is bounded by `MaxScaleDomainSize` and an object or array item in it is a
    specification error; a sort array that mixes value kinds warns (`vl-encoding-sort-mixed-types`) and a sort of
    categories on a continuous scale warns and is ignored (`vl-encoding-sort-ignored`).
24. **Warnings, not silent nulls**: a calculate that produces a regular expression stores null and warns
    (`vl-expr-runtime`); a pivot that skips or cuts data, a regression group that is skipped, and an extent without a
    finite
    value warn (`vl-transform-runtime`).
25. **Provenance of the expression and transform rules**: the semantics follow the public Vega
    expression documentation, the
    Vega-Lite transform documentation, and ECMAScript; the engine and the transforms are original code, no upstream
    source,
    test, or example was copied, and the sampler is built from the published FNV-1a and SplitMix64 descriptions (see
    `third-party/README.md`).
26. **Deterministic bootstrap**: `ci0` and `ci1` are the 2.5% and 97.5% quantiles of 1,000 bootstrap means drawn with
    SplitMix64, seeded from the FNV-1a hash of the JSON pointer of the transform and the group (Vega draws with
    `Math.random`); the two bounds of one field share their resamples. The result is identical on every call.
27. **Structural group keys**: the groups of `aggregate`, `joinaggregate`, `window`, `stack`, and `impute` (and of the
    implicit aggregate) are keyed by the structure of the values (a number and a text with the same spelling are
    different
    groups; an array or an object compares by its content) and listed in order of first appearance, where Vega keys by
    the
    joined text of the values and lists in the order of a JavaScript object.
28. **Defaults of the aggregating transforms**: the `stack` transform writes `field_start` and `field_end` when it has
    no
    `as` (Vega: `y0` and `y1`); the `exponential` operations use a weight of 0.5 without a `param`; an empty window
    frame
    gives null for `first_value`, `last_value`, and `nth_value` (Vega reads past the partition); `cume_dist` and `ntile`
    count the peers of the first rows of a partition (Vega under-reports them); the growth of the bin step is capped
    (Vega can loop for ever) and a bin count above `MaxScaleDomainSize` fails.
29. **A bar stacks by default**: as in Vega-Lite 6, rows of a bar chart that share a category are stacked from zero, not
    overdrawn (an area and an arc stack by default too, since phase 9; see 53). A chart with one row per category is
    drawn exactly as before; because such a bar starts at 0 even when an
    explicit domain excludes zero, it overflows the data rectangle as in Vega-Lite (there is no scale clip; see 6).
30. **The scope of the encoding pipeline**: `bin: "binned"` is read on `x` and `y` only (the other channels that have a
    scale bin
    from the data); `x2` and `y2` only for `bar`, `rect`,
    `rule`, and (since phase 9) `area`; `order` as the order of a stack, the sort of the points of a path, or the draw
    order of the other marks (phase 9); `scale.domain: "unaggregated"` only for `mean`, `median`, `q1`, `q3`,
    `min`, and `max`; a datum `x2` or `y2` is mapped through the primary scale
    and is not part of the domain; the title of a
    count is `config.countTitle` in every `fieldTitle` mode and the name of an aggregate keeps the operation as written
    (`average_x`); the axis of a normalized stack uses `config.normalizedNumberFormat` (`.0%` by default); the default
    tick
    count of a binned axis is `ceil(size / 10)`.
31. **Provenance of the aggregation rules**: the semantics follow the public Vega and Vega-Lite documentation and the
    behavior of the published algorithms (see `third-party/README.md`); the units are original C# code,
    the bin algorithm is
    a port, and the tests use values derived by hand.
32. **Color schemes are Vega's**: the 68 scheme names and colors are those of Vega's palette table, interpolated
    piecewise in sRGB as Vega does; the ramps that Vega names after ColorBrewer are Vega's own 9 to 11 color tables,
    so a discrete scheme with a `count` is sampled from the ramp and is not the ColorBrewer class table of that size.
    An unknown scheme name is a `VegaLiteSpecException`, as it is an error in Vega. The notices of the sources ship
    with the packages (see `third-party/README.md`).
33. **Legends**: a legend is built from the rules of Vega-Lite 6 and Vega, with these differences: the canonical
    scale of a merged continuous legend is the first channel in Vega-Lite's channel order; the channels `fillOpacity`
    and `strokeOpacity` are drawn but get no legend; `legend.encode` and a custom `formatType` are
    reported and not drawn (the `titleOrient` of a legend is
    drawn since Phase 13, see 93, and the `clipHeight` of a symbol legend since Phase 14, see 96); legends make the
    chart larger (a
    legend on the right adds its width, plus the offset of 18 pixels, to the width of the picture) except with
    `autosize: none`, where they overflow, as in Vega-Lite. The entries are charged to the scene-item limit before
    they are created. The symbol of a legend entry follows Vega-Lite's legend rules for the path marks: a line, a rule,
    and a trail (which Vega-Lite makes unfilled in its legend) have a stroke-shaped symbol painted with the stroke
    color, at the legend's default stroke width of 1.5, and an area, an arc, and a text mark have a filled circle.
34. **Scale types follow Vega-Lite's tables**: an explicit `scale.type` that the channel or the field type cannot use is
    replaced by the default type with the warning `vl-scale-type-ignored`, as Vega-Lite does; the bin-ordinal scale is
    accepted for a binned quantitative color only; a color scale with an explicit domain of four or more entries (a
    piecewise color scale) is a specification error; a `scale.domain` that is not an array is ignored; a temporal field
    accepts only the `time` and `utc` types (Vega-Lite's `scaleTypeSupportDataType`), so any other explicit type on it
    is
    replaced by the default.
35. **Placement of the calendar and the formatter (P8-D1; items 35 to 47 are the deviations of phase 8, time, whose
    decision numbers P8-D1 to P8-D10 are those of its plan)**: the calendar (`TimeCalendar`, with the intervals, the
    time
    units, and the date-time objects) and the d3-time-format formatter and parser with the `Date.parse` rules
    (`TimeFormat`) are units of the **`Data`** subsystem, not of `Transforms` or `Scales` as the master plan sketched,
    because the `parse` directives of the data stage must read dates and the `Expressions` subsystem must format, parse,
    and offset them, and `Expressions` must not depend on `Scales` or `Transforms`. The time-unit row work
    (`TimeUnitTransform`) is in `Transforms`, the time scales and ticks are in `Scales`, and the label resolution that
    axes and legends share (`TimeLabels`) is in `Guides`.
36. **Instants and the range 1 to 9999**: an instant is a `double` of milliseconds since the epoch, but the calendar
    works only on the years **1 to 9999** (the range of `DataValue.FromDate`), where JavaScript allows plus or minus
    8.64e15 milliseconds. Any result outside it, any non-finite input, and any failed parse is NaN at the calendar level
    and **null** as a value (a date field that does not parse is null with `vl-data-parse-failed`, and a date expression
    such as `datetime(10000, 0, 1)` is null), so nothing overflows or throws, and a formatted invalid date is the text
    `Invalid Date`.
37. **Zone semantics**: `TimeZone` (null is UTC) is the only zone; a local wall time becomes an instant by the
    ECMAScript
    rule (the offset before the transition): of the two candidates of a fall-back overlap the **earlier** instant is
    used, and a time in a spring-forward gap is shifted forward (02:30 becomes 03:30). The rule assumes at most one
    transition in 48 hours, which is true of every real zone. A day of a zone with daylight saving time can be 23 or 25
    hours long, and the `utc` units, `utc` scales, `utcFormat`, `utcParse`, and `utc` date-time objects ignore the zone.
38. **A documented subset of `Date.parse`**: a date text is read by the ECMAScript date-time string format (a date-only
    text is UTC, a date-time without an offset is in the zone, `Z` and offsets are honored, `+YYYYYY` years) plus a
    **legacy subset** of what V8 accepts (`Jan 2, 2006`, `2 Jan 2006`, `Mon Jan 02 2006 15:04:05 GMT+0200`, RFC 1123,
    `2006/01/02`, `1/2/2006` as month, day, year, an optional `h:mm[:ss] [AM|PM]`, and `UTC`, `GMT`, `Z`, or `+hhmm`).
    Anything else is not a date (V8 accepts more); a day that does not exist in its month (`2023-02-29`) is rejected
    where
    V8 rolls some of them over; a text longer than 128 characters is rejected before any scan. Because `parse: "auto"`
    now tests dates with this reader, a column of text such as `Jan 2, 2006` becomes a column of dates.
39. **d3-time-format details**: the English (`en-US`) locale only; an unknown directive is copied like d3 (so a format
    string never fails); `%L` reads one to three digits and the number directives read a window as d3 does; `%Z` shows
    whole minutes; a parse whose result leaves the years 1 to 9999 fails (d3 would give an invalid date); the
    multi-format `TimeFormatter` is bound to the calendar given to `Multi`. The `minutes` plus `seconds` unit specifier
    is `00:%M:%S` (no space).
40. **Expression functions**: all the date and time functions of Vega except `now`, which stays unlisted
    (`vl-expr-function-now`, a null), so no function reads a clock; `datetime()` without an argument is null; the
    `isoweek` and `utcisoweek` getters are added (a superset of Vega-Lite, which has no ISO week unit), but `isoweek` is
    not
    an interval name of `timeOffset` and `timeSequence` (null); `timeUnitSpecifier` returns null for invalid units where
    Vega throws, and a date sequence is bounded by `MaxScaleDomainSize` and charged one step per element.
41. **Predicates with a time unit or a date-time object** are compiled to expressions over the calendar functions and
    run by the expression evaluator. A `range` is `min(a, b) <= left <= max(a, b)` (reversed ends also match), a `oneOf`
    uses `indexof` over the list, **`utc` units use the `utc*` functions** (Vega-Lite marks this "TODO" and ignores the
    flag), and a text that is neither a date nor a unit part never matches. The `week` and `dayofyear` parts are **not
    applied** in a predicate, exactly like Vega-Lite, and are reported by the warning `vl-predicate-timeunit-part` (the
    transform and the encoding apply them). The expression is written by the renderer, so the `MaxExpressionLength` of
    the expressions that a user writes does not apply to it: a date list is
    bound by `MaxScaleDomainSize` values and the written text by eight times `MaxSourceLength` (counted as it is
    written and refused before it is allocated, so a long field name with a large `oneOf` list fails fast), and either
    failure is at the predicate; an `{"expr": ...}` that the user writes as an operand of a predicate is bound by
    `MaxExpressionLength` like any user-written expression, at the expression (the same holds for a `oneOf` or an `or`
    of a condition, so a list of a thousand values is accepted).
42. **Time units**: all 40 local unit names of Vega-Lite (and their `utc` forms) and the object form, including the
    `maxbins` form with Vega's `timeBin`; the `binned*` units (and `binned: true`) are applied in Phase 14 as Vega-Lite
    6.5 does (`compile/data/timeunit.ts`, `timeunit.ts`, `predicate.ts`): an encoding with a binned unit reads the field
    as it is (the data is floored already; `vgField` leaves the unit out of the name, so the title is the field alone)
    and has the end `field_end` (`timeOffset(smallestPart, field, step)` of the value as it is, so the 15th of March ends
    on the 15th of April) only where a channel spans the unit, which is the rule of a unit that is not binned (a bar or
    rect, or a band position); a predicate compares `time(field)` with the values read for the unit; the `timeUnit`
    transform floors a binned unit like any other (Vega's `timeunit` has no `binned` parameter, which Vega-Lite passes
    through and Vega ignores; Phase 13 had kept the value as the start and the end one step after it); a rect-based mark
    (`bar`, `rect`) whose band position is not 0.5 is drawn at 0.5 and reported
    (`vl-encoding-bandposition-rect`; Vega-Lite's offset rect fields are not produced); `bandPosition` on a field
    without
    a time unit keeps `vl-encoding-bandposition`. A binned unit without a `unit` (`{"maxbins": 4, "binned": true}`) is a
    specification error, where Vega-Lite fails in the compiler. The group-by of an aggregate on a channel that spans its
    unit includes
    the end field (`yearmonth_date_end`; it follows from the start, so it adds no group), where the plan said "not by
    the end". A `datum` that is a date-time object is not implemented (`vl-encoding-datum-datetime`).
43. **Time scales and ticks**: the default type of a temporal color is a `time` scale in Vega-Lite and is drawn as the
    sequential scale over the instants (the same colors, with date ticks and labels); a temporal field accepts only
    `time` and `utc`; zero and a default `nice` do not apply; `domainMid` takes a timestamp; the explicit domain
    entries and a `datum` are read in the scale's calendar. The two-day tick of a **UTC** scale counts the days since
    the
    epoch (d3's `unixDay`: January 2, 4, and so on from January 1, 2024, which is the odd day 19,723), while the two-day
    tick of a time scale counts the day of the month (1, 3, 5), and the ticks of a two-week step count from the epoch
    week.
    A step below one or negative falls back to about ten ticks. The tick count default is `ceil(size / 40)`, except
    Vega's ten for a field whose unit is exactly a month, an hour, a day, or a quarter, and is limited by the duration
    of the smallest part of the unit (`tickMinStep`).
44. **Labels**: an axis label is the `format` (string or multi-format object), else the unit specifier, else Vega's
    multi-format; a legend label is the `format`, else the unit specifier, else `config.timeFormat` (`%b %d, %Y`); axes
    ignore `config.timeFormat`. `formatType` `number`, `time`, and `utc` are evaluated (`utc` is a superset of the
    plan),
    and a `time` format type on an axis that is not temporal uses UTC (a quantitative scale has no calendar).
    **`labelBound` is applied only with a `labelOverlap` method** (see 92; the `labelExpr` of axes and legends is: see 75).
45. **Marks**: a bar or rect on a time-unit channel spans its unit exactly as a bin (spacing, half pixel, `minBandSize`
    against the whole unit, `timeUnitBandSize` as the share of the unit), points, circles, squares, and rules sit at the
    **start** of their unit (a tick at the middle) unless a band position is given, and a bar on a time scale without a
    unit is `continuousBandSize` thick, as on a linear scale.
46. **Date-time objects** follow Vega-Lite's rules: a `day` is dropped when the object has any other key (`utc` too),
    numeric texts are accepted for the month, quarter, and day, a name matches by its first three letters, years 0 to 99
    mean 1900 to 1999, and an overflowing component rolls over as `MakeDay` and `MakeTime` do.
47. **Corrections of the phase plan** found while building and verified by arithmetic: Vega-Lite has **40** local
    time-unit
    names (and their `utc` forms), not 44; 1900-01-01T13:45Z is **-2,208,939,300,000** milliseconds (25,567 days before
    the
    epoch at 86,400,000 is 2,208,988,800,000, less 49,500,000 for 13:45); the bar of the month example is 31/91 of 300
    pixels wide, **102.198**; `DateTimeObject.ToMilliseconds` has no unit parameter (Vega-Lite ignores the unit there);
    in the `nice` example the tick span is 6.97 days (the result, the week, is unchanged); `HasBinEnds` of the mark
    builder is now `HasBandEnds`; and `vl-scale-zero-ignored` is a new warning that the plan did not list.
48. **Placement of the curves and the arc (P9-D1; items 48 to 59 are the deviations of phase 9, line, area, trail,
    text, and arc, whose decision numbers P9-D1 to P9-D16 are those of its plan)**: the curve, line, and area
    generators (`CurveBuilder`) are a unit of `Marks`, and the arc geometry (`ArcGeometry`, with the internal d3-path
    emulation `ArcPathWriter`) is a unit of `Scenegraph`, because the painter needs it for the arc and trail items.
    `MarkBuilder` and `ScenePainter` became partial classes (`MarkBuilder.Paths.cs`, `.Order.cs`, `.Overlays.cs`,
    `.Text.cs`, and `.Arcs.cs`; `ScenePainter.Paths.cs`). Phase 9 adds no public type, no option, and no limit, and the
    point and line overlays are built inside `MarkBuilder`, not by a normalization of the specification into layers
    (that belongs to the layer phase).
49. **Curves**: the fifteen Vega-Lite names of `interpolate` are accepted and drawn as the literal ports of the
    d3-shape curve classes; `catmull-rom-open` and `catmull-rom-closed` of Vega are not Vega-Lite values and are a
    specification error like any other unknown name. `monotone` is the x-monotone curve for a line and for an area that
    is not horizontal, and the y-monotone curve for a horizontal area. `tension` is the beta of `bundle`, the tension of
    the cardinal curves, and the alpha of `catmull-rom`; the other curves ignore it, and a `tension` that is not
    finite counts as absent. d3-shape gives `bundle` no area outline, so an area with it is drawn with the `linear`
    interpolation and reported as `vl-mark-interpolate-bundle-area`. The closed basis and cardinal curves end back at
    their start without a closing command (as d3-shape does; only `linear-closed` closes), so the area of such a curve
    is two closed loops. The upper bound of the segments of a path (the charge made before the list exists) is one
    segment for each defined point (two for the step curves, and doubled for an area, which writes every point twice)
    plus `6 * max(runs, 1)`, which is the allowance that the baseline of an area of basis curves needs.
50. **Invalid data on the path marks (P9-D4)**: the `invalid` property of the mark definition, else
    `config.<mark>.invalid`,
    else `config.mark.invalid`, else `break-paths-show-path-domains`; a text of the configuration that is not one of
    the modes is ignored. A row is invalid when a channel with a continuous scale (`x`, `y`, `theta`, `radius`, `size`,
    and the opacity channels) has no finite number. The `break-paths-*` modes (the default) cut the path at such a row
    and the `filter` mode drops the row, so the path connects its neighbors; a trail drops a run of one point. The
    modes `show` and null draw such a row at a fallback position since Phase 13 (see 89; `vl-mark-invalid-show` is
    retired). **The scale domains of every mode include the rows that the mode leaves out of the marks** (the
    scale resolver does not filter a domain by the validity of another channel), so the explicit `filter` and
    `break-paths-filter-domains` modes give the warning `vl-mark-invalid-domains`. The other marks keep dropping the
    invalid rows (item 4). **A stacked channel is invalid when the value that was stacked is not a number**: the stack
    counts a null as 0
    (so the other slices keep their positions), and the row is then removed from the marks as Vega-Lite's filter, which
    runs after the stack
    (`compile/data/parse.ts` and its `FilterInvalidNode`), does: a bar, an arc, a symbol, or a label of that row is not
    drawn and a stacked area breaks
    its path there. The domains still hold the row (a category that occurs only in null rows stays on the axis and in
    the legend, which Vega-Lite drops: the deviation above).
51. **Overlays (P9-D6)**: the `point` overlay of a line, area, trail, or rule and the `line` overlay of an area follow
    the
    overlay mark definitions of Vega-Lite (`true`, an object, `"transparent"` for invisible points, and the
    configuration of the mark; an area with an overlay and without an opacity gets the opacity 0.7), with these
    differences: an `encoding.shape` **switches the point overlay on** when the mark has no `point` and the
    configuration
    does not decide (Vega-Lite's `getPointOverlay`; the shape is then drawn by the overlay points and is not reported),
    the overlay marks draw the channels of their mark that the path does not (the points a `shape`, `angle`,
    `strokeDash`,
    and `size`, the line overlay of an area its `strokeDash` and its `size` as the width; the legend of a line with
    points shows filled symbols), the overlay line of an area groups as a line, and an
    `{"expr": ...}` inside an overlay object is a static site, evaluated once like the one of the mark (Phase 12). The
    `size` channel of a line is applied to the overlay points through the
    scale of the line, its range of stroke widths read as the area of a symbol: that is **Vega-Lite's own behavior**
    (the point layer of
    the path overlay normalizer of Vega-Lite gets the whole encoding and shares the scale), not a difference.
52. **Impute (P9-D7, P9-D8)**: the `impute` of `x` or `y` runs the impute engine after the aggregate and before the
    stack, with the key taken from the other position channel (it needs both channels), the grouping fields of the path,
    and the `value`, `method`, `frame`, and `keyvals` of the definition (the `keyvals` array is bounded by
    `MaxScaleDomainSize`); an aggregated channel is imputed through its derived name, and the `impute` of any other
    channel is ignored without a report (`vl-encoding-impute` is retired: Vega-Lite reads `impute` from `x` and `y`
    only). The implicit impute of a stacked line, area, or trail (zeros for the
    keys that a layer lacks, switched off by `impute: null`) is **not done for a binned dimension** (Vega-Lite uses a
    computed mid-point field for it): such a chart is stacked as it is and gets the warning
    `vl-encoding-stack-impute-bin`.
    It is not done either when there is no stack-by field (a single layer has every key) or when a time-unit dimension
    has a band end.
53. **Grouping, sorting, stacking, and orientation of the path marks**: a path takes its paint, width, and dash from the
    first row of its group, and the `size` of a line is its stroke width and groups the lines (a trail's `size` is per
    point). The points of a group are sorted by the pixel position on the dimension (the pixel `y` of a horizontal
    orientation) when that channel is a field, by the `order` fields when the channel is not stacked, and not at all
    for `order: null` or `false` (as a channel or as a mark property); a row whose other channel is invalid keeps its
    position in the sort and cuts the path there. A group field that holds an array or an object compares by
    identity. An `area` without a measure channel draws nothing (Vega-Lite draws a zero-height area at zero). A bar,
    an area, and an arc stack by default, and **one without stack-by fields is still stacked** (as in Vega-Lite),
    so an `order` on it orders the stack and a null `y` of an area counts 0 in the stack but breaks the path of the
    area; `stack: null` gives the
    unstacked chart. A stack is not grouped by the stacked field itself (a radial chart reads `theta` and `radius`
    from one field). The orientation of a mark with no `orient` follows Vega-Lite's rules (an area with `y2` is
    vertical, or horizontal when `y` is `"binned"`, and an area with `x2` is horizontal, or vertical when `x` is
    `"binned"`; a line, area, or tick with one temporal
    position and one that is not follows the temporal one), and a trail and an arc are always vertical; the temporal
    rule is not applied to a bar, rule, or text mark, which Vega-Lite does apply it to (that would shift the continuous
    bands of a bar on a temporal axis).
    A trail ends its run at a point whose coordinates or size are above 1e9 in magnitude, and its coincident points
    start their circle with a move (the filled shape is the capsule union of Vega).
54. **Trigonometry**: the geometry of an arc, the polar offset of a label, and the capsules of a trail use the sine,
    cosine, arcsine, arccosine, and arctangent of the host's math library, and the `catmull-rom` curve a power, which
    can differ in the last bit between platforms and runtimes; the tests therefore compare geometry with a tolerance
    of 1e-9 and never compare pixels across operating systems, while repeated and concurrent renders in one process
    are identical.
55. **Polar scales and arcs (P9-D10, P9-D11)**: `theta` has the range of the full circle and `radius` the range from
    zero
    to half of the smaller side of the data rectangle, linear by default for a quantitative field, with no `nice`, no
    padding, and no axis (zero is `config.scale.zero`, true by default, unless the domain or `scale.zero` says
    otherwise); a nominal or ordinal
    `theta` or `radius` is a band scale for an arc and a point scale for a text mark, so **discrete polar scales are
    drawn** and the identifier `vl-scale-polar-discrete` that the plan kept as a fallback does not exist. The
    `discreteBandSize` of a band `theta` still applies (Vega-Lite reads it for `x` and `y` only). A `bin` on `theta` or
    `radius` is not drawn and stays reported (`vl-encoding-bin`), and a time unit on them is read at its start. The
    `theta2` and `radius2` channels of a mark without the main channel are ignored; `innerRadius` and `outerRadius` are
    the aliases of `radius2` and `radius` and the explicit properties win; `padAngle` and `cornerRadius` are constants
    of the mark definition or the configuration. `startAngle`, `endAngle`, and `padRadius` are not Vega-Lite properties
    and are reported as unknown properties. The two ends of a slice or ring are ordered, so a reversed scale still
    makes a sector. A stacked arc row whose value is null is not drawn (the stack counts it as 0, so the other
    slices keep their angles, and Vega-Lite's filter removes the row after the stack), and an unstacked null is not
    drawn either.
56. **The text mark (P9-D9)**: the text of a row is the `text` channel, else the `text` property. A field is formatted
    by
    its `format` and `formatType` (a custom format type is reported as `vl-format-type-custom` and the default format is
    used, and the `format` is ignored); a time, a temporal field, or a time unit uses the time formatter with the
    specifier of the unit or `config.timeFormat`; a binned field is `start – end` (with the en dash) of the number
    format; a quantitative field or any `format` goes through the d3 number format of the JavaScript number of the
    value, so **a null is `0`**, as `format(null, "")` is in Vega; any other value is its JavaScript text (a null is
    `null`, and an array is one line for each element). A line is only made by an array or by the literal text of
    `lineBreak` (never by a line feed in the text: the title of the view splits at a line feed, item 7, the label of a
    text mark does not), empty text draws nothing, and `dx` and `dy` stay on the item and are not added to its
    position. The size of the font is the `size` channel, else `fontSize`, else the `size` property. **A label is
    limited by `MaxStringLength`, and all the labels of one mark by sixteen times that limit**, and a condition on
    `text` stays reported (`vl-encoding-condition`).
57. **The order channel (P9-D5, P9-D12)**: `order` is accepted by every mark (`vl-channel-order` and `vl-channel-detail`
    are retired): it orders a stack, the points of a path, and otherwise the draw order of the marks, as a stable sort
    of the rows charged to the step budget; `detail` groups the points of a path and has no effect elsewhere, and `key`
    still groups a path and is otherwise ignored.
58. **Limits of the new marks (P9-D13)**: no new limit property exists. The charges are in the limits table: a path is
    charged up front with an upper bound of its segments (the bound is never refunded, so a chart can be refused
    although its exact count would fit), an overlay point, an arc, and each line of a label are one item, and the label
    characters are charged to the step budget as they are produced. A single path of up to `MaxSceneItems` segments is
    painted as one item, so the stroker and the filler cannot be interrupted inside one path; the cancellation is
    polled between items, groups, merge blocks of a sort, and every 1,024 curve and trail points.
59. **Provenance of the line, area, trail, text, and arc rules**: the semantics follow the public Vega, Vega-Lite, and
    d3-shape documentation and the behavior of the published algorithms, read for the curve classes, the line and area
    generators, the arc, the path emulation, the trail and text marks, and the compile rules of the marks (see
    `third-party/README.md`); the units are C# code that ports the d3-shape and d3-path algorithms with attribution,
    and the tests use values derived by hand.
60. **Layers: one normalized tree, no rewritten specification (P10-D1)**: the views of a specification are reduced to
    unit
    views by `SpecNormalizer` over the original nodes (the merged encoding of a layer is a node that is not adopted by
    its
    parent, so every pointer, line, and column is that of the text the author wrote), and the inherited `data`,
    `transform`,
    and `encoding` follow Vega-Lite: a layer's own `data` replaces the inherited one, the `transform` of a layered view
    applies first and each layer's own after it, and a layer's channel replaces the inherited channel in place. The
    `width`, `height`, `autosize`, `padding`, `background`, `config`, `datasets`, and `title` of a layer are ignored
    with a
    warning (`vl-layer-property-ignored`); a layered root's own `mark` is ignored. A `layer` of a layer is nested to
    `MaxCompositionDepth` levels, and the number of views, the views of a composite mark expansion included, is bounded
    by
    `MaxViews`; both are enforced before any data is read (the limits were reserved until this phase).
61. **Shared scales (P10-D2)**: a channel that several layers carry has one scale: the owner is the layer with the scale
    of the highest precedence (band, then point, then the rest), the domain is the union of the layers' domains
    (continuous:
    from the smallest start to the largest end, each after the layer's own zero rule; discrete: every category once in
    layer
    order, sorted when all the layers sort the same way), and the owner's scale is built again over that domain so that
    everything that depends on the domain agrees. Explicit domains follow Vega-Lite's `mergeValuesWithExplicit` with
    `domainsTieBreaker`: an explicit domain beats an implicit one (which is ignored), equal explicit domains count once,
    and different ones are all kept, which Vega unions (continuous: the extent of all the values; discrete: every
    category once in order of first appearance). The legend of a merged channel takes the symbol facts and the
    properties of the **first**
    layer that has it (Vega-Lite merges them); the layer scales that cannot be merged keep their own with the warning
    `vl-layer-scale-conflict`. `resolve` can make a scale, an axis, or a legend independent, in nested layered views
    too.
62. **Guides of layers (P10-D3)**: the layers that share a position scale and an axis have one axis, whose title merges
    the titles of the layers as Vega-Lite's `mergeAxisComponent` does (a written title wins over a derived one, derived
    titles and different written titles join as `a, b`, equal ones are one); the legend titles merge the same way;
    independent axes follow Vega-Lite's `parseLayerAxes` (the second axis moves to the opposite side unless the
    axis says an `orient`, and only the first axis has a grid); the `config` orient of an axis does not count as
    explicit for
    that rule. A step (`{"step": n}` or the default) applies to the union of the categories of the layers.
63. **Composite marks are expanded, not drawn natively (P10-D4)**: `boxplot`, `errorbar`, and `errorband` become the
    layered
    views Vega-Lite normalizes them to (aggregate, joinaggregate, calculate, and filter transforms with the rule, tick,
    bar,
    point, rect, area, and line marks), with the defaults of `config.boxplot`, `config.errorbar`, and `config.errorband`
    (the band opacity is 0.3). The tooltip parts are omitted; the `bin`, the `aggregate`, and the `timeUnit` of a channel
    that groups the rows are transformed as Vega-Lite's `extractTransformsFromEncoding` does (see 91; the identifier
    `vl-composite-grouping` is retired in Phase 14), the shorthand aggregate of the
    measure (`"boxplot"` and the like) is
    not
    accepted, as it is not in the schema of Vega-Lite 6; the warnings that Vega-Lite only logs (a mismatched `center`
    and
    `extent`, ignored properties of a one-dimensional band) are not reported. A **transform that the top-level view
    holds** is applied before the expansion's own transforms, so a composite mark sees the transformed rows.
64. **Provenance of the layer and composite rules**: the semantics follow the public Vega-Lite documentation and the
    behavior of the published compile rules for layers (`compile/axis/parse.ts`, `compile/scale/domain.ts`, and
    `compile/resolve.ts`) and composite marks (`compositemark/boxplot.ts`, `errorbar.ts`, `errorband.ts`, `common.ts`,
    and the
    `config.ts` defaults); the units are own C# code with attribution (see `third-party/README.md`), and the tests use
    values
    derived by hand.
65. **Composition: the overflow of a mark is not part of a cell's bounds (P11-D1)**: the bounds of a chart that the grid
    layout uses are the data rectangle, the axes, the legends, the title, and half of the stroke of the view border; a
    point or a bar at the edge of its domain, or a mark stroke, that sticks out of the data rectangle is not counted
    (the same rule as for the single view, whose canvas also ignores it), so the next cell can start up to a few pixels
    nearer than in a browser, where the bounds include the marks. `bounds: "flush"` lays the cells out by their width
    and height only, as Vega.
66. **Facet scales and sorts (P11-D2)**: a scale that the cells of a facet share is the union of the domains of the
    cells **after** each cell's own transforms (an aggregate, a stack, a bin of the encoding), where Vega-Lite derives a
    shared domain from the data before the aggregates of the cell; a category sort of the shared scale by an aggregate
    over the cells keeps the order of first appearance. The order of the cells itself follows Vega-Lite: ascending of
    the field by default and for `sort: null`, descending, a `sort` array listing those values first and the others
    after them in order of first appearance, and a null or missing value as a cell of its own (labeled `null`, first
    when ascending and last when descending).
67. **A facet body with its own data is not partitioned (P11-D3)**: Vega-Lite does not facet a `spec` with a `data` of
    its own either, so every cell draws that table; the cells are those of the facet's own table and the documentation
    says so.
68. **Text sizes come from the bundled fonts (P11-D4)**: the widths and heights of the header labels and titles, of the
    legends, and of the axes, which decide the bounds of the cells and so the positions of the grid, are those of the
    bundled fonts and not Vega's text estimator, so the picture of a composition differs from a browser's by the
    difference of the text sizes (the structure and every number that does not depend on text agree).
69. **Facet features that are reported, not drawn**: a custom `header.formatType`
    (`vl-format-type-custom`), and a
    `header.labelOrient` or `titleOrient` on a side that the header does not draw on (the default side is used;
    `vl-header-orient`: a row honors left and right, a column top and bottom, the label of a wrapped facet any side and
    its title top and bottom). The `orient` shortcut of a `header` is not read, as Vega-Lite's compiler does not read
    it either. A header of a row or a column is taken from the first cell of that row or column; the footer with the x
    axis is drawn for every column, also under an empty cell of a wrapped facet, as in Vega-Lite.
70. **A composition ignores more than Vega-Lite does**: Vega-Lite drops only a `fit` of a composition and keeps `fit-x`
    and `fit-y`; here every `autosize` other than `pad`, every `width` and `height` (`"container"` included) of a
    composition, and the `"container"` size of its children are warned about (`vl-composition-property-ignored`) and a
    composition is laid out as `pad`; the `padding`, `background`, `config`, `datasets`, and `autosize` of a nested view
    are ignored with the same warning. The warning `vl-composition-property-ignored` is produced by the catalog (it
    is static); `vl-concat-axis-shared`, `vl-composition-columns-ignored`, and `vl-repeat-ref-unresolved` are produced
    by the normalization and delivered once from the drawn tree.
71. **The fit autosize and container sizes (P11, `ViewLayout`)**: `autosize` is normalized as Vega-Lite does
    (`normalizeAutoSize`: a `"container"` width, height, or both is `fit-x`, `fit-y`, or `fit` with `contains: padding`
    unless the specification or a `config.autosize` that the configuration really sets says otherwise; the built-in
    `pad` default is not an explicit one) and a fit is dropped for a dimension that a step sizes
    (`vl-layout-fit-step`, `getTopLevelProperties`: `fit-x` with a step width becomes `fit-y`, both dimensions
    stepped become `pad`). The layout is Vega's `viewSizeLayout` (the guide reaches are rounded up, a title is part of
    the fit, a legend counts only across its side), so a `"container"` chart now fills the container **exactly** and
    leaves room for its title where it grew by its guides before (the scene snapshot `single-container-fit` changed
    from 421.3 by 252.8 to 420 by 252 for a 420 by 252 container). An explicit `pad` or `none` with a container size
    takes the container size as the data rectangle (Vega-Lite's `width` signal); the warning
    `containerSizeNotCompatibleWithAutosize` is not reproduced (no identifier); a title that overflows a fitted view is
    iterated at most three times where Vega iterates until the layout is stable; `autosize.resize` is ignored (the
    picture is static).
72. **Repeat and layer sizes**: a `repeat` of `layer` takes the size and the `view` of its `spec` when the repeat has
    none (Vega-Lite takes the size of a layered view from its layers), a pure layer repeat is a layered view (its title
    is middle-anchored), and the size of a layer of a plain `layer` that writes a `width` stays ignored with
    `vl-layer-property-ignored` (Phase 10). The product of the lists of a repeat is checked against `MaxViews` before
    a copy is made.
73. **Legend ownership and the grid**: the legends of a shared channel are merged at the highest view that shares the
    channel (the owner) and a legend owned by a concatenation or a facet is built against a data rectangle of 0 by 0,
    so a gradient legend there has its minimum length of 64 pixels, as Vega's zero-size group; the specs owned by a
    chart stay in its cell. `headerBand` and `footerBand` of the grid layout are not ported (every cell has a size, so
    the headers are at the origin of their cell), a header that has no cell to align to is placed at 0 where Vega
    fails, and the quirks of the Vega layout are kept (`center` moves only the cells that start a column or row,
    `align: none` still adds the previous extent of the column, headers beyond the rows are dropped).
74. **Provenance of the composition rules**: the semantics follow the public Vega-Lite documentation and the behavior of
    the published compile rules (`spec/base.ts`, `normalize/core.ts`, `normalize/repeater.ts`, `compile/concat.ts`,
    `compile/facet.ts`, `compile/header/*`, the size rules of `compile/`, `compile/resolve.ts`, `compile/compile.ts`,
    and the
    `config.ts` defaults) and of the Vega layouts (`vega-view-transforms`: `grid.js`, `ViewLayout.js`); the units are
    own C# code with attribution (see `third-party/README.md`), and the tests use values derived by hand and the
    structure that the Vega-Lite and Vega reference gives, never pasted output.
75. **Label expressions (`labelExpr`, unit `LabelExpressions`)**: the `labelExpr` of an axis, a legend, and a facet
    header, and of `config.axis*` and `config.header*`, follows Vega-Lite 6 and Vega (`compile/axis/assemble.ts`,
    `compile/legend/assemble.ts`, `compile/header/assemble.ts`, `AxisTicks.js`, `LegendEntries.js`, and the text mark of
    `vega-scenegraph`), with these differences. The expression runs on the **shared evaluator of the call** (the same
    `MaxEvaluationSteps`, `MaxStringLength`, cancellation token, and parameters as every other expression) **before the
    labels are measured, laid out, and thinned**, so the extent of an axis, `labelLimit`, the overlap removal, and a
    legend box see the replaced text; the three layout passes evaluate an axis again, so the cost is bounded by
    `MaxTicks` times the steps of the expression, and an exhausted budget is a located `VegaLiteRenderException`. A
    syntax error or an unknown parameter is a located `VegaLiteSpecException` when the document is opened. The datum
    has `value` and `label` and, by guide, `index` (an axis: the fraction `i / (n - 1 or 1)`; a legend: a whole
    number), `tickIndex` (an axis), `perc` (gradient legends), and `perc2` (discrete gradients); a member a guide
    does not give does not exist. The result is coerced like the text mark: null is empty, an array is lines (each
    trimmed), anything else its JavaScript text, trimmed. `config.legend.labelExpr` is **not read** (Vega-Lite 6 has
    none). The diagnostic `vl-expr-label` is **retired** (never produced again, never reused); a label expression
    reports only `vl-expr-function-<name>`, `vl-expr-view-state`, and `vl-expr-construct-<name>` at its text. The
    number `format(value, specifier)` is one of the 113 functions (Vega's d3-format in the en-US locale: an invalid
    specifier is a located `VegaLiteRenderException`) and `vl-expr-function-format` is **retired**; `luminance`,
    `geoarea`, and `now` are examples of the functions still reported. An array result is several lines in an axis, a
    legend, and a facet header (the header, footer, or wrapped-facet title is taller; a string result stays one line).
    Not modeled: `datum.size` and `datum.offset` of a symbol legend (null), a date result is its ISO text (not
    the JavaScript date text), the interplay with a custom `formatType` (the `datum.label` text replacement), and the
    extra tick of a binned axis.
76. **Offset channels (`xOffset`, `yOffset`)**: the channels place marks inside the band of a nominal or
    ordinal `x` or `y` with a **nested offset scale**, as in Vega-Lite 6 (`channelHasNestedOffsetScale`,
    `scale/{type,properties,range,domain}.ts`, `mark/encode/{offset,position-point,position-rect}.ts`,
    `stack.ts`, and `getStepFor` of `spec/base.ts`). A position that holds a discrete offset is **always a band
    scale** (paddings `config.scale.bandWithNestedOffsetPaddingInner` and `...Outer`, 0.2 each, unless `scale.padding`
    or an explicit padding says otherwise); the offset scale is a band scale for `bar`, `rect`, `image`, `rule`, and
    `tick` and a point scale for the other marks (outer padding 0.5), or a linear or time scale for a quantitative or
    temporal offset (no zero, no `nice`, no pixel padding; a strip or jitter plot), with the band paddings
    `config.scale.offsetBandPaddingInner` and `...Outer` (0 by default); its domain is the distinct values of the
    whole field, sorted by its `sort`. The offset scale spans the **bandwidth** of the position band, and a step size
    is applied to the position scale (`position step = offset step x bandspace(offset count, offset paddings) / (1 -
    position paddingInner)` for `for: "offset"`, the default; `for: "position"` keeps the step on the position scale).
    Worked default: three categories and two groups with the step 20 give a position step of 50, a width of 160, a
    bandwidth of 40, and bars of 20 pixels (with a width of 200: 62.5, 50, and 25). A point-like mark sits at the start
    of its band plus the center of its offset; a bar, rect, tick, or rule spans the offset band (an explicit size is
    centered in it); a row with an invalid offset is not drawn. The offset field is a stack group-by (not a stack-by),
    the aggregate groups by it, and a quantitative offset on a view with no dimension disables the default stack.
    Layers merge shared offset domains (`resolve.scale.xOffset: "independent"` keeps each layer's own) and compute the
    position step from the merged domain; a concatenation keeps offset scales independent unless
    `resolve.scale.xOffset` is `shared`; the cells of a facet share them. **A temporal position with a time unit**
    (`year`, `yearmonth`, `yearmonthdate`, `hours`, ... whose smallest part is not the day of the week; not a `rect`
    mark) nests the offset in the unit: the position stays a time scale, the offset scale spans `[(padding / 2 + bpo) x
    length, (1 - padding / 2 + bpo) x length]` pixels from the start of the unit, where `length` is the distance on the
    time scale of one reference unit (Vega-Lite's `durationExpr`: from 2001-01-01 one step of the smallest part later;
    a month is 31 days, a year 365; negative on a y scale), `padding` is
    `config.scale.bandWithNestedOffsetPaddingInner` (0.2), and `bpo` is the band position (`bandPosition`, else the
    mark's `timeUnitBandPosition`, else 0.5) less 0.5; a bar spans a band of the offset scale times `timeUnitBandSize`.
    Example: a monthly grouped bar chart of two groups over a domain of 59 days and 300 pixels has a month of 31 x 300 /
    59 pixels, bars 0.4 of a month wide at 0.1 and 0.5 of the month. A nominal or ordinal position with a time unit is
    an ordinary band parent (a band scale with the nested paddings), whatever the unit and the mark. Deviations: a mark
    without a band position uses
    0.5 (Vega-Lite computes NaN), a UTC unit is measured in the UTC calendar (Vega-Lite: the zone of the chart), a
    `bandPosition` other than 0.5 on a bar does not shift the domain (`vl-encoding-bandposition-rect`), and
    `minBandSize` clamps around the band start. **The ranged offset** (Phase 14, see 97) is drawn; not ported: a
    continuous offset of a bar or area that is not a ranged offset
    (`vl-offset-ranged`), the offset of a temporal position with a `day` or `weekday` unit, a binned or `maxbins` unit,
    or on a
    `rect` mark (`vl-offset-position`), a `bin` on
    an offset channel (`vl-encoding-bin`), and the pixel `xOffset` and `yOffset` properties of a mark, which are still
    not drawn; an explicit `point` scale type on a position that holds an offset is replaced by the band scale
    (`vl-scale-type-ignored`).
77. **Expression references, raw domains, and structured parameter values (Phase 12, milestone 1)**: Vega-Lite compiles
    an `ExprRef` to a Vega signal; in a static render its value is the expression evaluated once, with the parameters at
    their initial state (a caller's `Parameters` entry replacing the definition), and the renderer does exactly that:
    `PrepareChart` evaluates the parameters first, then folds the references of the static sites into a copy of the
    tree (`ExpressionReferenceFolder`), and every later stage reads values. Deviations and rules: the references at the
    sites that the marks evaluate for every row (the paint properties of the mark and the `value` of a color, fill,
    stroke, opacity, or size channel) stay per-row expressions; a reference that reads `datum` at a static site has no
    row and is reported (`vl-expr-ref`, the property keeps its default); a number that is not finite folds to null; a
    date folds to its milliseconds only in a scale domain and is otherwise a warning (`vl-expr-ref`, the reference is
    ignored); the `text` of a title or a mark takes the JavaScript text of a number or a boolean; the `width`, the
    `height`, and a predicate are not sites (Vega-Lite's size is a number, `"container"`, or a step). A `scale.domain`
    of the form `{"param": name}` and a `scale.domainRaw` give the domain as it is, as Vega's `configureDomain` does
    for a
    raw domain (no zero rule, `domainMin`, `domainMax`, `domainMid`, pixel padding, or `nice`); `domainRaw` wins over
    the
    parameter; a continuous scale needs at least two numbers or dates and a band, point, or ordinal scale at least one
    scalar, else the warning `vl-scale-domain-raw` is delivered and the scale keeps its own domain (Vega would draw
    every
    position undefined); the discretizing scales keep their own domain; a parameter that has no value or is a selection
    leaves the domain of the data; a scale whose domain is a `{"param": ...}` clips the marks of the view (Vega-Lite's
    `scaleClip`), whether or not the parameter has a value. The layers that share a scale take the raw domain of any
    member as the domain of the shared scale. Only the variable parameters of the top-level view exist (Vega-Lite
    compiles no signal for those of a nested view), so one of a nested view is reported (`vl-param-variable`) and read
    as
    null. The `Parameters` option takes arrays and string-keyed dictionaries of values (nested up to 32 levels, at most
    65,536 values for each parameter, deep-copied when set); the dataset rows still take scalars only. A selection as a
    scale domain, a filter on a selection, the conditions on a parameter, and the bin extent and lookup source given by
    a
    parameter are described under the deviations 78 and 79 and in the later milestones.

78. **Selection parameters in their initial state (Phase 12, milestone 2a)**: a selection parameter (`select` of
    `point` or `interval`, as a text or an object, and the legacy top-level `selection`) is the state that Vega-Lite's
    compiled specification holds before any event: its `value` (or nothing) as a store of tuples, and the signal that
    Vega-Lite derives from the store. `SelectionModel` computes it once per call, and the predicates that Vega-Lite
    writes
    for a condition or a filter on a selection (`!length(data("name_store")) || vlSelectionTest("name_store", datum)`,
    with
    the `empty` and `resolve` properties) are evaluated by the expression engine with the functions `data`,
    `vlSelectionTest`, `vlSelectionIdTest`, and `vlSelectionResolve` (ported from `vega-selections`: inclusive bounds
    for an
    `R` tuple, start-inclusive end-exclusive bounds for a binned `R-RE` tuple, strict equality for an `E` tuple, a datum
    without the field is a miss, union and intersect resolution). The projection follows `project.ts` (explicit `fields`
    and `encodings`, else the keys of the `value`, else `config.selection` defaults, else `x` and `y` for an interval
    and
    `_vgsid_` for a point; the presence of an explicit list decides, so `fields: []` is an empty projection that does
    not
    fall back to the defaults; the predicate names `vlSelectionIdTest` from that projection as the specification and its
    `config` give it, and the identity test of a store that is projected over fields tests its fields, so a default
    projection or a value of the caller makes no difference; a channel without a field or with an aggregate is skipped
    with a warning), and the assignment
    of
    a top-level selection to the unit views follows the top-level selection normalizer (`views`, and a selection in a
    layer
    or a facet). Deviations and rules: an unselected chart (an empty store) passes every row and holds every condition
    with `empty: true` (the default); a **point selection over the identity of the rows** (`_vgsid_`) is evaluated by
    `vlSelectionIdTest` but has no value in a static chart because no row identity exists (an initial `value` is
    reported,
    `vl-param-selection-id`); a selection that two layers of one layered view both receive is a duplicate signal that
    Vega
    rejects, so it is applied as empty and reported (`vl-param-selection-layer`); an animation selection is reported
    (`vl-param-selection-animation`); an interval initialized with a key that is not a channel warns and uses the
    default
    encodings (`vl-param-selection-projection`); `bind` (other than `scales` and `legend`), `on`,
    `clear`, `toggle`, `nearest`, `translate`, and `zoom` are ignored without a report; the name of a selection is
    visible from every view; a `param` or `selection` that no view declares in a condition or a filter is a located
    `VegaLiteSpecException` (Vega-Lite fails the same way); the initial value of a selection can be replaced by the
    caller
    (`Parameters`: a map, a list of maps, a scalar or a list of scalars for a single projection, `DateTime` values for
    dates, null to clear it), dates in a selection value are read in the calendar of the chart; a time unit field of a
    projection is the field `unit_field` (a transform-level filter and the conditions of an encoding derive it from the
    time unit of the projection, in the calendar of the chart; Vega-Lite does the same), and
    the `resolve` argument of the predicate is read from the `config` of the specification, not the `Config` render
    option
    (the `resolve` that the brush and `bind: "scales"` read is the merged one). `bind: "legend"` (the dimming of a
    legend) is deviation 80.

79. **Brush marks, `bind: "scales"`, and selections as domains and bin extents (Phase 12, milestone 2b)**: an interval
    selection that has an initial value draws the marks that Vega-Lite compiles for it (`selection-brush-background`
    before and `selection-brush` after the marks of the view, `clip` on, the background `#333` at the fill opacity
    0.125, the brush transparent with a white stroke, overridden by `config.selection.interval.mark`), through the first
    tuple of the store and the scales of the unit (a band scale gives the start of the band); under `resolve: "global"`
    only the unit that owns the selection draws them, never a cell of a facet; the marks of the units of a layered
    view are wrapped around all the layers, in the order that Vega-Lite's `assembleLayerSelectionMarks` gives. A brush
    whose rectangle has no area is not drawn (Vega-Lite draws an empty rectangle: no visible difference). A selection
    with
    `bind: "scales"` (and `resolve: "global"`) draws no brush: its initial value becomes the domain of the scale of
    each projected continuous channel when the unit is the whole view or a layer of the top-level layers (in a
    concatenation, facet, or repeat the top-level signal holds nothing and the domain stays the data's), and the marks
    are
    clipped in every case. `scale.domain: {"param": selection}` reads the range of the selection's field (`field`,
    else `encoding`, else the first projected field) as the domain and clips the marks; a `bin` whose `extent` is
    `{"param": ...}` (an encoding or the transform) takes the span of that extent for the step of `BinAlgorithm` (the
    bins still cover the data) and, for an encoding, gives the scale the extent as its domain and clips the marks. A
    negative span is a
    render error where Vega yields NaN. The selection defaults of `config.selection` are part of `ConfigResolver`.

80. **Legends bound to a selection (Phase 12, milestone 3)**: a legend that a global selection is bound to
    (`"bind": "legend"`, one projected field that is not the row identity, the field of the first channel's definition
    or the channel itself) dims the entries that the selection does not hold, as the first paint of Vega-Lite does: the
    symbols of a symbol legend that does not explain `opacity` and the labels of every legend kind get
    `config.legend.unselectedOpacity` (0.35) when the value of the entry is not among the values the resolved
    selection holds for the field (strict equality, as `indexof`), and nothing is dimmed while the store of the
    selection is empty. A selected symbol has the opacity of the mark, or 1 when the legend has a `symbolOpacity`
    (Vega-Lite's `opacity ?? 1`); a selected label has 1. When the channels or layers of a legend merge, the first one
    decides. The input widgets and the click and clear events of the binding are ignored, as every interaction. A legend
    binding without exactly one projected field is reported as `vl-param-selection-projection` and not applied. The
    value of an entry is what Vega holds in `datum.value`: the number of milliseconds for the domain value of a discrete
    scale over a field of dates (a date selects it), a `Date` for a tick of a time scale (never selected by a number, as
    in Vega: strict equality). The rule reads the field of the channel definition, so a selection over a field with a
    `timeUnit` (its values are stored under the derived field `month_d`) dims every entry while its store has a tuple,
    as Vega-Lite does.

81. **Statistical transforms are ported literally (Phase 13)**: `regression`, `loess`, `density`, `quantile`, and
    `extent` compute the numbers of the Vega operators they compile to (`vega-regression`, `vega-transforms`, and
    `vega-statistics`, see _Third-Party Provenance and Attribution_), including the JavaScript details: the groups are
    those of Vega's `partition` (the key of a row is the text of the array of its group values, so the number 1 and the
    text `"1"` are one group, and null and a missing field are one group; first-seen order; the group fields before the
    output fields), a value is a number as `+value` makes it (null, the empty text, and NaN are skipped by the quantile
    and the extent, and a text that is not a number makes a density NaN), and the order of the floating-point operations
    is that of the reference. The unit is `StatTransforms` (see _StatTransforms Unit Design_). The defaults are those of
    Vega-Lite: the output fields `x`/`y` (the `on` and the regressed field), `value`/`density`, and `prob`/`value`, the
    `shared` resolve of a density, the bandwidth of Scott's rule when none is given, and the 0.3 bandwidth of a loess.
82. **Limits and rejected options of the statistical transforms (Phase 13)**: the rows a statistical transform generates
    are counted in the row budget before they exist (a curve as it is sampled, a density grid, and a quantile list), the
    work is charged to the evaluation-step budget (a fit by the points and the coefficients, a loess by the points and
    the window, a density by the rows for every sample point), and the cancellation token is polled in the long loops;
    the order of a polynomial is limited to an integer from 0 to 64 (`RegressionFit.MaxPolynomialOrder`, because the fit
    is cubic in the order), the `bandwidth` of a loess to 0 to 1, and a negative `bandwidth`, `steps`, `minsteps`,
    `maxsteps`, or `step` is a specification error (Vega would yield NaN or loop); a regression group with no valid x
    (where Vega throws) gives two points with a null x and a NaN y, and a date is the number of milliseconds on a curve,
    as in Vega.
83. **The cells of a pivot are Vega's aggregate cells (Phase 13)**: a cell is one aggregate operator fed only with the
    rows of its pivot value (`MeasureCell`), so `count` counts the rows whose `value` is not NaN (null and the empty
    text count), a cell without rows is null for `sum`, `mean`, `min`, `max`, `median`, and the other operations that
    need a value and 0 for `count`, `valid`, and `missing`, `distinct` counts the rows of the group that carry another
    pivot value as one more value (Vega feeds them as NaN), `values` is the array of all the rows of the group, a text
    that is not a number poisons `sum` and `mean` with NaN, and `exponential` and `exponentialb`, which need a weight,
    give NaN. The state of all the cells is bounded by the cell limit and checked before it is allocated.
84. **The extent parameter (Phase 13)**: an `extent` transform sets the parameter that `param` names when it runs, after
    the data is read, so the transforms that follow it, `bin.extent` (in the encoding or in a `bin` transform), and
    `scale.domain` as `{"param": ...}` read it, and the expressions of its view and of the views below it may reference
    it (a parameter of the same name as a declared one is a specification error). The properties that are folded before
    the data is read (a title, a size, a mark or scale property that is an expression reference) see the parameter as
    null and the renderer reports the warning `vl-param-extent-static`. An extent without a finite value is
    `[null, null]` with a `vl-transform-runtime` warning. An `extent` property next to a `regression` or a `density` is
    the option of that transform and not a transform of its own (Vega-Lite's `isExtent` rule).
85. **The image mark takes its pictures from the host (Phase 13)**: Vega loads an image by its URL (the browser's
    loader, or the file system and the network of Node); this library never does. The `url` of an `image` mark is
    handed,
    as text and untouched (a remote URL, an absolute path, a fragment), to `VegaLiteRenderOptions.ImageResolver`, which
    decides what it may name (`FolderImageResolver` resolves it relative to the chart under its file-access policy and
    understands every CanvasNet format: a
    raster,
    SVG, a `.scad` model, a PDF page as `report.pdf#page=2`, another chart). Without a resolver the mark is laid out and
    draws
    nothing and the feature catalog reports `vl-mark-image`; `Measure` never asks the resolver, so the size of a chart
    never depends on a picture (Vega's own layout does not either: a picture is drawn when it is loaded).
86. **The size of a vector picture and the units of a raster (Phase 13)**: Vega draws a raster at its natural pixel size
    in scene units, which this renderer does (so at `scale` 2 it covers twice the pixels). A model, an SVG file, a PDF
    page, or a chart has no natural pixel size: with no `width` and `height` of the mark it is drawn 256 units wide and
    high (the resolver is asked for 256 device pixels at a scale of one and the result is divided by the scale), with
    one dimension the other follows the aspect ratio of what the resolver returned. The kind of a picture is not
    inferred from the size of the answer: `ImageSourceResult.IsNaturalSize` (set by `RasterImageSource`) is carried by
    `IImageNaturalSizeResolver`, which `FolderImageResolver` and the resolver it gives to embedded files implement, so a
    raster that has exactly the device size of the request is still drawn at its natural size. Only a host resolver that
    implements just `IImageResolver` leaves the judgment to the size of the answer (a picture that has exactly the size
    of the request is taken as drawn for it).
87. **One answer for a link and a size in a render (Phase 13)**: the resolver contract allows no cache, but a chart that
    shows
    the same icon for every point must not ask once for each point, so the answer for a link at a device size is kept
    and
    shared for the rest of the render (and every surface is released when the render ends). `VegaLiteLimits.MaxImages`
    (256) bounds the distinct questions, and the question beyond it fails the render at the mark.
88. **Vega-Lite's `rectPosition` for the image mark is ported including its oddities (Phase 13)**: an axis with a
    position
    and no size is a point anchored by the `align` (`baseline`) of the mark; a `size` channel or property counts as a
    size
    without giving a length, so the axis is the range from the value to the zero of the scale; an absent channel spans
    the whole
    data rectangle (or, with a `width`, anchors at the right edge) - what Vega-Lite compiles. The conditions of the
    `url`
    channel are reported (`vl-encoding-condition`) and not applied, and `smooth: false` resamples with nearest-neighbor
    to the
    rounded device size instead of the browser's `imageSmoothingEnabled`.
89. **The `show` invalid mode draws the row at zero or at the minimum (Phase 13, `MarkBuilder`)**: with an `invalid`
    of `"show"` or null (of the mark, else of `config.<mark>`, else of `config.mark`), a row whose value on a continuous
    channel (`x`, `y`, `size`, the opacity channels, and the color-like channels on a continuous scale) is not a number
    is drawn: the channel reads as a fallback data value, which is the scale output of zero when the domain contains
    zero (never on a time, UTC, or log scale) and else of the first value of the domain - Vega-Lite's
    `scaledZeroOrMinOrMax` (`compile/mark/encode/invalid.ts`). It is a data value on the channel, so the text
    channels are not touched. **Deviations**: a stacked row of a null is drawn at its position in the stack. The
    `config.scale.invalid` of a value per channel is read since Phase 14 (see 98). The `filter` and `break-paths-*`
    modes are as before
    (50), and `vl-mark-invalid-show` is retired.
90. **Custom SVG path shapes are ported from vega-scenegraph (Phase 13, `SymbolPathParser`)**: a `shape` that is not a
    symbol name (the `shape` of a point mark, a `shape` channel value or `scale.range` entry, and the `symbolType`
    of a legend) is SVG path data. The unit `SymbolPathParser` ports `parse.js`, `render.js`, `arc.js`, and the
    bounds of `boundContext.js`, with the scaling of `symbols.js` (the coordinates of the path are scaled by
    `sqrt(size) / 2`, the symbol radius), including the quirks of Vega: `s` and `S` reflect the persisted control
    point whatever the previous command was; a scaled `t` always sees `t` as the previous command, so its control
    point is NaN, the canvas skips the curve, and the bounds still add the end point; text before the first command
    letter is skipped; text with no command draws nothing; a non-finite number skips the draw call. **Deviation**:
    Vega throws for a path that does not parse (and for a `t` as the first command); the renderer reports the warning
    `vl-shape-path-invalid` and draws a circle (a path with no command is reported too and draws nothing). A path has
    at most 4,096 commands (`MaxCommands`), and 256 parsed paths are cached. `vl-shape-path` is retired.
91. **Facet and composite grouping time units, and the facet sort (Phase 13; extended in Phase 14)**: a `sort` of a
    facet field by
    `{field, op}` (the operation is `min` by default, and `count` when there is no field) orders the cells by that
    aggregate of the cell's rows, stably, descending with `order: "descending"`; `argmin` and `argmax` are ignored.
    A `timeUnit` with parts on a facet field is applied with the `timeUnit` transform into a derived field: the cell is
    the start date of the unit and the header label is formatted with the time format of the unit. The same holds for a
    non-binned `timeUnit` of a channel that groups the rows of a boxplot, errorbar, or errorband: it becomes a
    `timeUnit` transform plus a derived field (prepended to the transforms of the top-level view), its default title is
    that of a time unit, and the format type is `time` for a channel that is not temporal.
    **Phase 14** applies the rest as Vega-Lite 6.5 does. A facet field (the `facet` operator and the `row`, `column`, and
    `facet` channels of a unit view; `compile/facet.ts`, `compile/data/facet.ts`, `compile/data/parse.ts`) names its
    field with `vgField`, where a `bin` wins over an `aggregate`, which wins over a time unit: a `bin` is computed by
    the `bin` of the encoding (6 bins by default for `row` and `column`, 10 for `facet`: `autoMaxBins`), the cell is
    the start of the bin and the cells are ordered by it, and the header label is `start – end` formatted by the
    `format` of the header, else `config.numberFormat` (`binFormatExpression`), the text `null` for a row without a bin;
    an `aggregate` is **not computed** (Vega-Lite's `AggregateNode.makeFromEncoding` takes a unit model only), so the
    cell reads the field that `vgField` names (`sum_v`, `__count`, `argmax_by.field`) from the data, which an
    `aggregate` or `window` transform may have written, and is one null cell when there is none (the title is
    `Sum of v`); a binned unit adds no field (`TimeUnitNode.makeFromEncoding` produces a component only for a unit
    model), so the raw value of the field is the cell and the header label uses the specifier of the unit; the
    `maxbins` form is resolved against the data by the `timeUnit` transform. The title of a header is the default
    title of the definition (Vega-Lite's `title`: `c (binned)`, `d (year-month)`), where Phase 13 wrote the field name.
    A channel of a boxplot, errorbar, or errorband (`extractTransformsFromEncoding`, `encoding.ts`) is transformed by its
    definition: a `bin` is a `bin` transform before the aggregate with the bins of the encoding (`autoMaxBins` of the
    channel), whose start and end group the rows and which the channel reads as binned data, with `x2` or `y2` for a
    position channel, the type quantitative and a title; an `aggregate` is a measure of every aggregate transform of the
    mark (the whisker, box, and min-max aggregates of a boxplot, the aggregate of an errorbar from raw observations, and
    for aggregated input the `{aggregate, groupby}` that Vega-Lite adds when a channel aggregates), which the channel reads
    (`mean_age`, `__count`, `argmax_by.field`, the exponential operations with their `param`) with the title of its
    definition, and which does not group the rows; a `bin` that is `"binned"` or false and a binned unit leave the
    field as it is. `vl-facet-sort` is retired. Two consequences of Vega-Lite's own rules are deliberate: a rule whose
    `x` is data that is binned already (`bin: "binned"` with its end) is a point at the middle of the bin and not a
    range (`compile/mark/init.ts` ignores the binned channel in the orient of a rule), which is what makes the whiskers
    of a box plot over a bin vertical; and the box of a box plot over a bin spans the whole bin here, where
    Vega-Lite 6.5 draws it from the middle of the bin to its end (`rangePosition` of the bar with the size of the box
    reads the middle of the binned `x`; reproducing a bar that starts in the middle of its bin was not attempted).
92. **Static axis properties (Phase 13, `AxisBuilder`)**: `tickBand` of `"extent"` puts the ticks and grid at the end of
    the band (band 1, an extra tick, offset 0), any other value at its middle (0.5, no extra tick); without `tickBand`
    the band is `bandPosition` (0.5 by default) and the extra tick is `tickExtra`. The extra tick is appended after the
    label expression at the position of the first band's start when the band is not 0, and the labels skip it;
    `bandPosition` moves the ticks and the grid but not the labels. `translate` (0.5 by default) replaces the half pixel
    of the origin of the axis group and does not feed the shift of the bins of a bar; `position` moves the axis along
    itself; `titleX` and `titleY` replace the position of the title in the pixels of the axis group. `labelBound`
    (true is a tolerance of 1 pixel, a number is the tolerance, 0 and false none) hides a label that sticks out of the
    range along the axis, but only when a label overlap method is set, as the Overlap transform of Vega returns before
    the test otherwise, and boxes of at most 1 pixel are skipped. The eight `vl-guide-axis-<name>` identifiers are
    retired.
93. **Legend `titleOrient` is a port of the legend layout of Vega (Phase 13, `LegendBuilder`)**: `top`, `bottom`,
    `left`, and `right` follow `legendGroupLayout` and `legendTitleOffset` (`vega-view-transforms`,
    `layout/legend.js`) and `legend-title.js` (`vega-parser`): the entries move right by the width of a left title and
    down by the height of a top title, a right title goes past the entries and a bottom title below them, the title
    anchor defaults to `middle` for `left` and `right` and `start` otherwise, the title of a vertical gradient is
    turned at the sides, and `titleAlign` and `titleBaseline` win. `vl-guide-legend-titleorient` is retired;
    `clipHeight` was still reported then and is drawn since Phase 14 (see 96).
94. **Cubehelix interpolation is a port of d3 (Phase 13, `ColorInterpolator`)**: `scale.interpolate` of `cubehelix` and
    `cubehelix-long` (also `{"type": "cubehelix", "gamma": g}`) follows d3-color's cubehelix conversion and
    d3-interpolate's `cubehelix` and `cubehelixLong`: the hue takes the shorter way (the long way for the long form), a
    grey takes the hue of the other color, and the gamma is applied as `t^gamma` to the lightness. The oracle values of
    d3 (red to blue, gamma 1, the midpoint `#ee00d1`; gamma 2, `#ff00f6`) are asserted by the tests.
    `vl-scale-interpolate-cubehelix` is retired.
95. **Distribution functions are pure; sampling is not in the table (Phase 14, `ExpressionFunctions.Distributions.cs`)**:
    the nine functions `densityNormal`, `cumulativeNormal`, `quantileNormal`, `densityLogNormal`,
    `cumulativeLogNormal`, `quantileLogNormal`, `densityUniform`, `cumulativeUniform`, and `quantileUniform` are ports of
    `normal.js`, `lognormal.js`, and `uniform.js` of `vega-statistics` 2.0.0 (the table grows from 104 to 113
    functions): the cumulative normal is West's (2009) approximation shared with the kernel density
    (`KernelDensity.NormalCdf`) and the normal quantile uses the inverse error function of Giles in the form of Apache
    Commons Math that Vega ports. The JavaScript defaults and coercions are reproduced (`mean || 0`,
    `stdev == null ? 1 : stdev`, a uniform without a maximum is the interval from 0 to the minimum, JavaScript's
    relational comparison, the `+` concatenation when a text mean or minimum is given to a quantile) and a quantile
    outside 0 to 1 is NaN. **Decision**: `random`, `sampleNormal`, `sampleLogNormal`, and `sampleUniform` **stay
    unsupported**. An expression must be a pure, deterministic function of its datum and its parameters, and Vega's
    `random` is the unseeded `Math.random`, so a sampling function would make a picture differ from one call to the
    next (the determinism contract of _Determinism And Immutability_); a call is reported as `vl-expr-function-random`
    (`-samplenormal`, ...) and evaluates to null, like `now`. The numbers of the unit tests are those of
    `vega-statistics` 2.0.0 itself (relative tolerance 1e-12); the notices are in `third-party/README.md`.
96. **`legend.clipHeight` is a port of the symbol legend of Vega (Phase 14, `LegendBuilder.Symbols.cs`)**: a
    `clipHeight` (of the legend or `config.legend`) greater than 0 on a **symbol** legend puts the symbol and the label
    of each entry at
    half of `clipHeight` instead of half of the entry size, makes the box of the entry group the label box united with
    the rectangle from (0, 0) to (0, `clipHeight`), and wraps each symbol in a clipping group (scene role
    `legend-symbol-clip`) at the origin of its entry with a clip rectangle as wide as the widest entry of its column and
    as high as the bottom edge of the entry; the label is not clipped. The port follows `legend-symbol-groups.js` of
    `vega-parser`, `group.js` and `boundClip.js` of `vega-scenegraph`, and `legendEntryLayout` of
    `vega-view-transforms` (`layout/legend.js`) and was verified against the output of Vega 6.4 (the pitch of the
    entries, the positions, and the clip rectangles). A **gradient legend ignores** `clipHeight`, as Vega does (Vega-Lite
    passes 20 for a gradient, which Vega never reads). `vl-guide-legend-clipheight` is retired.
97. **Continuous offsets of a bar or area are drawn as ranges (Phase 14, `ScaleResolver.Offsets.cs`,
    `MarkBuilder.Offsets.cs`, `AxisBuilder`)**: Vega-Lite 6.5.0's ranged offset (`isRangedOffset` of `compile/unit.ts`,
    `rangePosition`, `area.ts`, and `zero()`, `getOffsetRange`, `defaultBandPosition`, and `labelsPositionSpec` of the
    scale and axis compilers). A `bar` or `area` whose `x` or `y` is a nominal or ordinal field or datum (a band scale
    with the nested paddings 0.2 and 0.2) without `x2` or `y2` and whose `xOffset` or `yOffset` is an unbinned
    quantitative field or numeric datum spans, inside the band, from the zero of the offset scale to the offset value (a
    bar is the rectangle between them, and a negative value hangs on the other side of the baseline; an area has `y` and
    `y2` at those ends, its points along the other axis, and its measure axis follows the ranged offset). The offset
    scale is a linear scale whose range is the bandwidth of the position scale (running upward for `yOffset`) with
    `zero: true`; the other offset scales never include zero and have no `nice`. The position axis defaults its
    `bandPosition` to 0 for `x` and to 1 for `y` (ticks and grid at the baseline) and its labels sit at that band
    position; an explicit `axis.bandPosition` wins, and 0.5 leaves the labels in the middle. `DecideOffset` returns
    `Ranged` for this case and the new `UnsupportedContinuous` for the continuous offsets of a bar or area that are not
    ranged (a temporal or binned offset, a position with `x2` or `y2`, a temporal position with a time unit), so
    `vl-offset-ranged` is **narrowed**, not retired, to exactly `UnsupportedContinuous`; `vl-offset-position` is unchanged
    (a ranged offset without a position channel is not drawn: Vega-Lite spans the bars from the top of the view and from
    half of the height then, a layout this renderer does not reproduce). The numbers of the tests are those of the Vega
    6.4 output of Vega-Lite 6.5.0.
98. **`config.scale.invalid` is read (Phase 14, `MarkBuilder.ApplyShowInvalid`)**: Vega-Lite 6.5.0's `invalid.ts`,
    `compile/invalid/ScaleInvalidDataMode.ts`, and `compile/mark/encode/invalid.ts` map a scale channel to `"zero-or-min"`
    or `{"value": ...}`. A channel with an entry (a continuous-domain scale, field-bound, not stacked) is in the `show`
    mode whatever the `invalid` mode of the mark is: its rows with a null, NaN, or non-finite value are not filtered and
    do not break a path. `"zero-or-min"` places them where the scale maps zero (when the domain has zero; never for a time
    or log scale), else at the first value of the domain; `{"value": v}` (a number or a text) is the output of the mark
    property as it is (a pixel position, a size area, an opacity, a color text), not passed through the scale
    (`ScaleModel.WithInvalidOutput`). The other channels keep the mode of the mark. **Deviations**: a stacked channel
    keeps filtering (its entry is ignored); the scale domains still include the invalid rows, as with the other modes in
    this renderer; an entry that is neither form (null, a boolean, a list, another shape) is ignored and
    reported as the new `vl-scale-invalid-value`, at the entry of the specification's own `config.scale.invalid`
    (an expression reference as the value is reported by `vl-expr-ref` at the value instead, not twice; the entries of
    the `VegaLiteRenderOptions.Config` are honored and not reported).
99. **Known fidelity gaps recorded at the end of Phase 14**: (a) next to a field-driven fill on a filled mark, a size
    legend draws hollow rings where Vega-Lite draws filled black symbols; (b) the axis of a binned scale places its ticks
    by the tick algorithm and not at the bin boundaries that the `bins` of a Vega scale give; (c) a `text` layer that sets
    both `fill` and `color` keeps the `color` field as a stack key, where Vega-Lite drops it. The gallery specs avoid
    all three cases. Changing (a) moves legend layout and the `single-point-legends` and `scatter-size-legend` pins,
    so it is left as a deliberate follow-up.

### Resource Limits And Hardening

The limits of the _Risk Control Measures_ are kept honest by three executable checks in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Hardening`. None of them is a software item; they are system-level tests
of the `CanvasNetVegaLite-Hardening` requirement.

**Audit inventory.** `VegaLiteLimitAuditTests` holds one row for every input-controlled cost. A row is a hostile
specification, the stage that refuses it, the exception type, the text that the message must contain (the limit), and
the JSON pointer that the location must start with. A refused row must be located, must name its limit, and must not
allocate what the specification asked for (less than 96 MiB on its thread: the cost is charged _before_ the
allocation); every row also gets an already canceled token, and the stages that a budget alone does not stop (density,
sliding window, aggregate, impute, facet cells) are canceled 150 ms after they start with their budgets lifted. A test
fails when a `VegaLiteLimits` property has no row.

<!-- markdownlint-disable MD013 -->
| Input-controlled cost | Bound | Audit row |
| --------------------- | ----- | --------- |
| Source text length, JSON nesting | `MaxSourceLength` (`InvalidDataException`), `MaxJsonDepth` (`VegaLiteSpecException`, located at the crossing) | `open/source-length`, `open/json-depth` |
| Inline rows, generated sequences (count, step, unrepresentable count, cells), fields of a row | `MaxDataRows` (compared as integers), `TableLayout.MaxCells` for a sequence, `MaxFieldCount`, charged before any row is converted | `data/rows-inline`, `data/rows-sequence`, `data/rows-sequence-step`, `data/rows-sequence-unrepresentable`, `data/rows-sequence-cells`, `data/fields` |
| Data file size and number | `MaxDataBytes`, `MaxDataFiles`; a file over the limit is refused before it is opened | `data/bytes`, `data/files` |
| Rows made by `fold`, `flatten`, `impute` (groups times keys), `quantile`, `density` | `MaxDataRows` with the clamped count, `TableLayout.MaxCells` for cells | `transform/fold`, `transform/flatten`, `transform/impute`, `transform/quantile-step`, `transform/density-steps` |
| Work of `loess`, window frames, aggregates, bootstrap, polynomial fits | `MaxEvaluationSteps` (calibrated weights), cancellation | `transform/loess`, `transform/window-frame`, stage cancellation tests |
| Regression order, window parameters | parser: a regression order is an integer within the documented range; window frame bounds and parameters are any integer (the engine clamps them) | `transform/regression-order`, `transform/window-frame` (completes) |
| Expression length, depth, steps, strings, arrays | `MaxExpressionLength`, `MaxExpressionDepth`, `MaxEvaluationSteps`, `MaxStringLength`; arrays are cut at the `MaxScaleDomainSize` cap | `expression/length`, `expression/depth`, `expression/steps`, `expression/string`, `expression/array` |
| Scale domain, bins | `MaxScaleDomainSize` (also for the bins of a bin step of `1e-9`) | `scale/domain`, `scale/bins` |
| Ticks | `MaxTicks`, checked before the labels are made | `guide/ticks` |
| Facet cells, repeat copies, concatenated and layered views, nesting | `MaxViews`, `MaxCompositionDepth`, counted before the views are made | `composition/facet-cells`, `composition/repeat-copies`, `composition/concat-children`, `composition/layers`, `composition/depth` |
| Scene items, pictures, painted pixels | `MaxSceneItems`, `MaxImages`, `MaxPaintedPixels` (charged for the clipped, stroke-inflated box of each item before it is painted) | `scene/items`, `scene/images`, `paint/pixels` |
| Canvas size | a size that is not finite is a located `VegaLiteRenderException`; a natural size beyond `Surface.MaxDimension` is an `ArgumentOutOfRangeException` for the options | `layout/non-finite`, `layout/size` |
| Text of titles and labels | `MaxStringLength` | `text/title` |
| Reports | `MaxDiagnostics` | `data/diagnostics-flood` |
| Number format width, dash period | clamped (`NumberFormat.MaxWidth`, `MaxDashSegments`): the rendering completes | `format/width`, `paint/dash` |
| Regular expression | the linear engine first; the backtracking engine has a 100 ms timeout, the one wall-clock-dependent outcome of the package | documented; not a row |
<!-- markdownlint-enable MD013 -->

**Fuzz suite.** `VegaLiteFuzzGenerator` builds a specification that has the shape of the schema (data, a single, layered,
concatenated, faceted or repeated view, a mark of the twenty types, one to six channels with aggregate, bin, time unit,
stack, sort, scale, axis, legend and condition, up to three transforms of twenty-three templates, parameters and
selections) from a seeded SplitMix64 source (`FuzzRandom`, not `System.Random`, so that a seed gives the same
specification on every runtime). A specification is _clean_ (ordinary values only), a little wild, or fully wild (a third
of the numbers from a table of boundary values: zero, one, `1e-300`, `5e-324`, 2^31, 2^32, 9.3e18, `1e300`, the largest
double, hostile texts: empty, a NUL, a surrogate pair, an unterminated expression, a 3,000 character text).
`VegaLiteFuzzMutator` applies one to three mutations (replace a leaf by a boundary value or a hostile text, scale a
number, delete a key, insert a key of a pool of 90, multiply an array, swap a subtree with another specification, flip a
type name) to every `Fixtures/*.vl.json` and to clean generated specifications. The oracle (`VegaLiteFuzzHarness`) runs
each case under tight limits (20,000 rows, 50,000 scene items, 5,000,000 steps, 200 ticks, 5,000 domain values, 60 views,
8 images, 50,000,000 painted pixels) and requires: (1) opening throws only `VegaLiteSpecException` with a location or
`InvalidDataException`; (2) measuring and rendering throw only `VegaLiteRenderException` or `VegaLiteSpecException`
with a
location, `VegaLiteUnsupportedFeatureException` in strict mode, or the documented `ArgumentOutOfRangeException` for the
options; (3) the case does not reach the hang guard (a cancellation by the guard's token is reported as a hang with the
message "Hang guard"): the guard is 120 seconds, orders of magnitude above any case under these limits, and
`CANVASNET_VL_HANG_GUARD_SECONDS` sets another number of seconds for a very slow machine; the tests assert outcomes and
never elapsed time, because the machines that run them can be heavily loaded (a calibration of the work meter against
seconds is measured by the benchmark harness, not asserted); (4) the thread allocates less
than 4 GiB gross over the parse and the four passes (a runaway detector; a rendering allocates about thirty kilobytes of
short-lived memory per painted symbol); (5) two runs give the same pixels and the same ordered reports, or the same
error, and `Measure` equals the size of `Render`. The default run is 300 cases with a fixed seed and takes about ten
seconds on each target framework. A larger run is opt-in through `CANVASNET_VL_FUZZ_CASES` (the total, split evenly
over the three kinds), `CANVASNET_VL_FUZZ_SEED` and `CANVASNET_VL_FUZZ_DUMP_DIR` (a folder that receives every failing
specification and a summary of the outcomes). The discovery oracle for a missed cast is to run a large fuzz with
`<CheckForOverflowUnderflow>true</CheckForOverflowUnderflow>` set temporarily in the library project (after a clean
rebuild): every out-of-range double conversion becomes an exception on every runtime. Every defect that a run finds is
a row of `Fuzz_FoundDefectsStayFixed`, with its seed and index.

**Source guards.** `VegaLiteSourceGuardTests` scans the text of the package (comments and string literals masked). A
narrowing cast (`(int)`, `(long)`, `(byte)`, ...) whose operand calls `Math`, names `double`, or uses an identifier
that the file declares as a `double` (or a `var` initialized from one) must be in `AuditedCasts` with the reason that its
value cannot be NaN or out of range; an entry that no longer matches fails the test. The scan is a heuristic that
cannot see every double; the checked-mode fuzz run is the exhaustive check. A second guard forbids reading the wall
clock, the local time zone, the culture of the thread, the environment, a random source, parallel or concurrent
collections, string-hash order and string-keyed collections without a comparer, except in the files that are listed
with the reason (the hash codes of the key structs of dictionaries that are enumerated in insertion order).

### Determinism And Immutability

Every public result type is immutable. A `VegaLiteDocument` and its internal specification tree can be shared between
threads, and parsing the same text any number of times, on any thread, in any culture, yields equal trees and
identical errors. `Measure` and `Render` hold no state between calls, so a document may be measured and rendered from
several threads at once, and each call gives the same result for the same options: a render is **pixel-identical** (the
`Surface` is equal pixel for pixel; an _encoded PNG_ is not byte-stable across .NET versions, because the deflate streams
of .NET 8 and of .NET 9 and later differ for the same pixels, so determinism is never stated or tested for encoded
bytes) for every degree of parallelism, on every .NET version the package targets, and in every culture, because the
only fonts used are the bundled fonts unless the options say otherwise, the time zone defaults to UTC and is the only
zone ever used (the host's zone, culture, and clock are never read; a source guard test forbids the calls), no
environment variable is consulted, the caller's dictionaries are looked up by name and never enumerated, the
dictionaries of the library are enumerated in insertion order, and the floating-point arithmetic is specified by the
ported algorithms. `Measure` always equals the size of the surface `Render` produces, because both run the same pure
layout. Three things are not pure: a regular expression that needs the backtracking engine has a wall-clock timeout of
100 milliseconds (the linear engine is tried first), wall-clock time is bounded only by the limits and the cancellation
token, and the floating point library (`Math.Sin`, `Math.Pow`, `Math.Log10`) may differ in the last bit between
Windows, Linux and macOS, which moves an anti-aliased pixel.

The determinism tests (`VegaLiteDeterminismTests`) therefore have two tiers. The **exact tier** pins the SHA-256 of the
pixels (`Fixtures/Determinism/pixel-pins.txt`) of six charts made of rectangles, lines, text and linear or band axes,
which have the same pixels on every target framework and operating system. The **tolerant tier** compares the pixels of
six charts that go through a library function that may differ in the last bit (the circle of a symbol, curves, arcs,
logarithmic scales, density and regression) with a committed picture (`Fixtures/Determinism/golden`) within the
tolerance that the repository uses for its committed images (`docs/images/render-images.ps1`: a root-mean-square
difference of one color level, at most 0.1% of the pixels more than eight levels apart). The tests also render every
chart twice and from eight threads, with the culture of the thread set to six cultures, with the datasets and the
parameters supplied in two insertion orders, and check that no time zone means UTC; a corpus of fourteen specifications
that steer a number beyond the range of an integer pins the outcome (the exception type and the location) that the
tests of every target framework must give.

## Implementation Phase Plan

`CanvasNetVegaLite` is delivered incrementally; each phase adds requirements, design, verification, and tests for its
own scope, and everything not yet implemented is reported with a stable identifier.

1. **Phase 1 - Foundation and document shell (delivered).** The project and test project, the public API contract
   (`VegaLiteDocument` and the options, limits, location, diagnostic, size, data reference, and exception types), the
   `SpecModel` subsystem (`SpecParser`, `FeatureCatalog`, `ConfigResolver`), the `Guides` units `TextMeasurer` and
   `TitleBuilder`, the `Layout` unit `ViewLayout`, the `Scenegraph` subsystem (`SceneModel`, `ScenePainter`,
   `CssPaint`), the attribution scaffolding under `third-party/`, original fixtures with a provenance README, and the
   complete companion documentation.
2. **Phase 2 - First charts (delivered).** The `Data` subsystem (`DataTable`, `DataLoader`), the `Scales` subsystem
   (`ScaleModel`, `ScaleResolver`, `Ticks`, `NumberFormat`, `ColorSchemes`), the `Guides` unit `AxisBuilder`, the
   `Marks` unit `MarkBuilder`, the end-to-end pipeline, the first ported d3 and Vega algorithms, and the extension of
   the attribution record.
3. **Phase 3 - Tool wiring (delivered).** The `canvasnet` tool recognizes `.vl.json` and `.vl`
   files and `$schema`-identified JSON, probes and renders Vega-Lite charts, reports the chart facts, and maps the
   library exceptions to its stable error codes; the tool package bundles the assembly and the license texts. The
   tool's own design, verification, and requirements are documented in the `CanvasNetTool` artifacts.
4. **Phase 4 - Data files (delivered).** The `Data` units `DataFileResolver` (the sandboxed,
   bounded, cached reading of local data files and of the caller's callback) and `DelimitedTextParser` (CSV, TSV, and
   DSV, `parse`, and opt-in per-column inference), the extended `DataLoader` (files, `format.property`, named datasets,
   `sequence`) and `DataTable` (a date value), the active `BaseDirectory`, `DataPaths`, `FileResolver`, `Datasets`,
   `MaxDataFiles`, and `MaxDataBytes` options, the retired and new `vl-data-*` identifiers, the data folder behavior of
   the `canvasnet` tool, and the original data fixtures.
5. **Phase 5 - Expressions and row transforms (delivered).** The `Expressions` subsystem
   (`ExpressionParser`, `ExpressionEvaluator`, `ExpressionFunctions`) and the `Transforms`
   subsystem (`RowTransforms`), the
   composite (array, object, and regular expression) values of `DataTable`, the compilation of expressions and the
   validation of transforms and parameters in `SpecParser`, the shared row budget of
   `DataLoader` (`ReserveDerivedRows` and
   `ReleaseRows`) and the loading of lookup sources, the sort by the aggregate of a field in `ScaleResolver`, the
   row-bound
   paint expressions of `MarkBuilder`, the static variable parameters and the `Parameters` override, the active
   expression
   limits, the new `vl-expr-*` and `vl-transform-*` identifiers, and the original fixtures.
6. **Phase 6 - Aggregation (delivered).** The `Transforms` units `AggregateTransforms` (the
   `aggregate`, `joinaggregate`, `window`, `bin`, `stack`, and `impute` transforms, every aggregate operation with the
   deterministic bootstrap, the Vega bin algorithm, and the stack rules) and `EncodingPlan` (the implicit bin,
   aggregate,
   and
   stack of an encoding and its field names and titles), the charging of filter predicates to the step budget, the
   validation of the aggregation properties in `SpecParser`, the plan-driven domains, the Raw and Main tables, and the
   sort
   by
   any aggregate in `ScaleResolver` (split into three files), the stacked, ranged, and binned geometry and the clipping
   rule
   of `MarkBuilder`, the derived titles, binned tick counts, and normalized format of `AxisBuilder`, the secondary
   channels
   `x2` and `y2`, the changed and new `vl-encoding-*`, `vl-channel-*`, and `vl-transform-*` identifiers, the attribution
   record, and the original fixtures.
7. **Phase 7 - Legends, color schemes, and mark-property channels (delivered).** The `Scales`
   units `ColorInterpolator` (the color spaces and ramps) and the rewritten `ColorSchemes` (the 68 Vega schemes, with
   their notices), the scale kinds sequential, diverging, quantize, quantile, threshold, bin-ordinal, log, pow, sqrt,
   and
   symlog in `ScaleModel` and `ScaleResolver` (split into partial files for colors and ranges), log ticks and labels in
   `Ticks` and `NumberFormat`, the `Guides` units `LabelOverlap` (extracted from `AxisBuilder`) and `LegendBuilder`
   (planning, symbol, gradient, and discrete gradient legends), the `Layout` unit `LegendLayout` (placement by orient
   and
   the legend extents of the view layout), the `SceneGradient` of the scene model and the gradient fill of the painter,
   the `shape`, `strokeWidth`, `strokeDash`, `fillOpacity`, `strokeOpacity`, and `angle` channels and the conditional
   encodings of `MarkBuilder`, `bin` on the channels that have a scale in `EncodingPlan`, the validation of the scale
   and
   legend properties in `SpecParser`, the retired and new `vl-*` identifiers, the notices of the color sources
   (ColorBrewer, Turbo, cividis, the matplotlib colormaps), and the original fixtures.
8. **Phase 8 - Time (delivered).** The `Data` units `TimeCalendar` (the zone, instants and
   their parts, the d3-time intervals, the Vega time units, and the date-time objects; deterministic, years 1 to 9999)
   and `TimeFormat` (the d3-time-format formatter and parser, Vega's multi-format, and the `Date.parse` rules in
   `JsDateParser`), the calendar parameter of `DataLoader` and `ParseDirectives` (the `date:` and `utc:` formats, the
   implicit date parsing of temporal fields, and the date test of `parse: "auto"`), the date and time functions of
   `ExpressionFunctions` on `ExpressionFunctionContext.Calendar` (the date and time functions; 100 functions in all with
the number `format`), the `Transforms` unit
   `TimeUnitTransform` (with `TemporalFields`), the time-unit plans, band ends, and group-by of `EncodingPlan` and
   `EncodingTransforms`, the time-aware predicates of `RowTransforms` and `PredicateSource`, the validation of time
   units, date-time objects, and tick and nice intervals in `SpecParser`, `ScaleKind.Time` and `ScaleKind.Utc` in
   `ScaleModel`, `ScaleResolver` (the new partial `ScaleResolver.Time.cs`), and `Ticks` (the new partial
   `Ticks.Time.cs`),
   the `Guides` unit `TimeLabels` with the time axes of `AxisBuilder` and the temporal legends of `LegendBuilder`, the
   time-unit bands and positions of `MarkBuilder`, the calendar wiring of `VegaLiteDocument` (the public API did not
   change), the retired and new `vl-*` identifiers (see the deviations 35 to 47), the notices of d3-time,
   d3-time-format, and the Vega time sources, and the original fixtures. The calendar and the formatter sit in `Data`
   rather than in `Transforms` and `Scales` (deviation 35).
9. **Phase 9 - Line-like and other marks (delivered).** The `Marks` unit `CurveBuilder` (the
   fifteen interpolations as ports of the d3-shape curves, the line and area generators with gaps, and the segment
   bound), the `Scenegraph` unit `ArcGeometry` (the d3-shape arc and the d3-path emulation) and `ScenePainter.Paths.cs`
   (the segment paths, the arc and trail items, and the miter limit), the path, arc, and trail items and the
   `StrokeMiterLimit` of `SceneModel`, the line, area, trail, text, and arc marks of `MarkBuilder` (the partial files
   for the paths, the overlays, the draw order, the text, and the arcs, with the `invalid` modes), the polar stack and
   the path grouping, order, and impute plans of `EncodingPlan` and `StackRules`, the encoding impute and the
   stack impute stage of `EncodingTransforms`, the polar `theta` and `radius` scales, the orientation rule, and the
   channel support table of `ScaleResolver`, the mark, overlay, impute, and text rules of `SpecParser`, the new and
   narrowed `vl-*` identifiers of `FeatureCatalog` (geoshape is the example of an unsupported mark since this phase),
   the stroke legend symbol of a trail in `LegendBuilder`, `TimeLabels.CreateBounded`, the notices of d3-shape and
   d3-path,
   and the original system tests (the deviations 48 to 59).
10. **Phase 10 - Layers and composite marks (delivered).** The `SpecModel` unit `SpecNormalizer`
    (the layer tree with the inheritance of `data`, `transform`, `encoding`, and size, the `resolve` of a layered view,
    the
    expansion of `boxplot`, `errorbar`, and `errorband` into primitive layers, and the enforcement of `MaxViews` and
    `MaxCompositionDepth`), the non-adopting `SpecNode.Overlay` and `SpecNode.Synthetic` nodes, `SpecExpressions.With`,
    the
    `resolve` and composite mark rules of `SpecParser.Layers.cs` and the widened `IsDrawnView`, the `boxplot`,
    `errorbar`,
    and `errorband` defaults of `ConfigResolver`, the retired and new `vl-*` identifiers of `FeatureCatalog` (the merged
    encoding of each layer is visited, and the reworded transform report), `ScaleResolver.Layers.cs` (the shared and
    independent scales and the conflicts, with `ResolvedScale` as a record), the layered `LegendPlan.Create`, the
    `DefaultOrient` and `DefaultGrid` of `AxisRequest`, `VegaLiteDocument.Layers.cs` (the data walk, the axis planning,
    and the
    legend scopes), `TemporalFields.CollectAll`, the provenance rows of the layer and composite mark rules, and the
    original
    system tests (the deviations 60 to 64). A layered chart with a top-level `transform` is drawn and its transform
    applied
    (it was reported as `vl-transform-<name>` and not applied before).
11. **Phase 11 - Composition (delivered with this pull request).** The `Layout` unit `CompositionLayout` (the Vega grid
    layout and trellis layout of the cells, headers, footers, and titles, in two files), the composition tree of
    `SpecNormalizer` (`SpecNormalizer.Composition.cs` and `SpecNormalizer.Repeat.cs`: `concat`, `hconcat`, `vconcat`,
    `facet`, the facet channels of a unit view, `repeat` and its references, the layout settings, the default `resolve`
    of each composition, and the `MaxViews` and `MaxCompositionDepth` of every nesting level), the composition and
    facet rules of `SpecParser` and the header properties, `EncodingTransforms.Facet` (the order of the cells, the
    cross product, the partition into one table for each cell, and the `MaxViews` check before the allocation),
    `TemporalFields.CollectAll` over the facet fields, the per-chart size hints and the composition conflicts of
    `ScaleResolver.Layers.cs`, `HeaderSettings` and `TitleBuilder.Header.cs` (the header blocks), `AxisRequest.InHeader`
    and the legend owners of `LegendPlan`, `ViewLayout` (`ResolveSize`, `CellBounds`, the `fit` autosize types and
    `"container"` sizes) and the fit rule of `LegendLayout`, the `Composer` of `VegaLiteDocument`
    (`VegaLiteDocument.Composition.cs`) with the generalized data walk and the cell pipeline shared with the single and
    layered chart (bit-identical output, pinned by the scene snapshots), the retired and new `vl-*` identifiers of
    `FeatureCatalog`, the provenance rows of the composition rules, the user guide section _Vega-Lite Composition_ with
    Examples 44 to 46, and the original system tests (the deviations 65 to 74). The carry-over wording about explicit
    domains of shared scales is corrected: explicit domains are merged and no text says that the first one wins.
12. **Phase 12 - Parameters and default state (delivered, in three milestones: ExprRef folding and the
    parameter hoist, selection semantics, and the brush, `bind: "scales"`, domain and bin-extent selections, the dimming
    of legends bound to a selection, and the demos).**
    Milestone 3 delivers the dimming of a legend that a selection is bound to (`LegendDimming.cs`, deviation 80), the
    demos and Example 49 of the user guide, and the reconciliation of the documentation. Milestone 2b delivers the brush
    marks (`MarkBuilder.Brush.cs`), the scale extent of a selection and of a bin
    (`ScaleResolver.Raw.cs`), the bin span (`BinSettings.Span`), the queries of the selection model, the time-unit
    fields of projections, and the `config.selection` defaults (deviation 79), and retires
    `vl-encoding-bin-extent-param`
    and `vl-transform-bin-extent-param`. Milestone 2a delivers the
    `SelectionModel` unit of the `Transforms` subsystem, the selection functions of the expression engine, the selection
    declarations of the `SpecModel`, the conditions and filters on a `param` or a legacy `selection`, and the caller's
    initial value of a selection (deviation 78). Milestone 1 delivers the `ExpressionReferenceFolder` unit of the
    `SpecModel`
    subsystem (the `{"expr": ...}` references of the static sites are compiled when the document is opened, evaluated
    once per call, and replaced by their values before the configuration and every later stage read the tree), the
    hoist of the parameter evaluation out of the data stage into `PrepareChart` (parameters, then the fold, then the
    configuration), the structured values (arrays and objects) of the `Parameters` option, the raw domain of a scale
    (`domainRaw` and `scale.domain: {"param": ...}`, with the clipping of the marks and the shared scale of layers, in
    `ScaleResolver.Raw.cs`), the narrowing of `vl-expr-ref`, the retirement of `vl-scale-property-domainRaw`, the
    rewording of `vl-param-variable`, and the new warning `vl-scale-domain-raw`. The static variable parameters were
    delivered in phase 5.
13. **Phase 13 - Advanced features and polish (delivered).**
    Delivered: the `StatTransforms` unit of the `Transforms` subsystem (`regression`, `loess`, `density`,
    `quantile`, and `extent`, with the numerics of Vega: `RegressionFit`, `CurveSampler`, `LoessFit`, `KernelDensity`,
    and `StatisticsPrimitives`), the pivot of every aggregate operation, the extent parameter and the warning
    `vl-param-extent-static`, the retirement of `vl-transform-regression`, `-loess`, `-density`, `-quantile`, `-extent`,
    and `-pivot-op`, the notices of d3-regression, regression-js, and science.js, and the deviations 81 to 84; the
    **`image` mark**: `SceneImage`, `MarkBuilder.Images.cs`, `ScenePainter.Images.cs` and `SceneImageSource` (the
    pictures are loaded at paint time through `VegaLiteRenderOptions.ImageResolver`, bounded by
    `VegaLiteLimits.MaxImages`),
    `VegaLiteImageSource` passing the scoped resolver of its request, the `vl-mark-image` report only without a
    resolver,
    the warning `vl-mark-image-unresolved`, `vl-channel-url`, the Viewer wiring of the folder resolver for a chart, and
    the
    deviations 85 to 88; the **remaining gaps** (the work packages WP10 to WP12): the `Scenegraph` unit
    `SymbolPathParser` (custom SVG path shapes and the warning `vl-shape-path-invalid`), the cubehelix interpolations of
    `ColorInterpolator`, the `show` and null `invalid` modes of `MarkBuilder`, the static axis properties of
    `AxisBuilder` (`tickBand`, `tickExtra`, `bandPosition`, `translate`, `position`, `titleX`, `titleY`, and
    `labelBound`), the legend `titleOrient` of `LegendBuilder`, the facet `sort` by an aggregate and the time units of
    facet fields and composite grouping channels, the retirement of `vl-shape-path`,
    `vl-scale-interpolate-cubehelix`, `vl-encoding-impute`, `vl-mark-invalid-show`, `vl-guide-legend-titleorient`,
    `vl-facet-sort`, and `vl-guide-axis-<name>`, and the deviations 89 to 94. Delivered in Phase 14: the binned
    and `maxbins` time units (`vl-timeunit-binned`, `vl-timeunit-maxbins`), a `bin`, `aggregate` or `timeUnit` of a facet
    field (`vl-facet-field-transform`), and of a composite grouping channel (`vl-composite-grouping`), the nine
    distribution functions of the expression language, `legend.clipHeight` (`vl-guide-legend-clipheight`), the ranged
    offset of a bar or area (`vl-offset-ranged` is narrowed to the continuous offsets that are not ranged), and
    `config.scale.invalid`. Deferred to the backlog:
    `axis.encode` and `legend.encode` (`vl-guide-encode`), a custom `formatType` (`vl-format-type-custom`), `geoshape`,
    and the sampling functions of the expression language (`random`, `sampleNormal`, `sampleLogNormal`, and
    `sampleUniform`, which stay unsupported, see 95). The `--param` option of the Tool, the `def.` and `param.`
    fragment parameters of an SVG
    image link, and the fragments on the input paths of the Tool are described in the Viewer and Tool designs.
14. **Phase 14 - Hardening (delivered).**
    Delivered by the fuzz, audit and determinism package: the **structured fuzz suite**, the **limit audit** table and
    the **source guards** of _Resource Limits And Hardening_, and the **determinism tiers** (exact pixel pins, tolerant
    pictures, culture, thread and insertion-order independence). The defects that they found and that are fixed with a
    regression test: the infinite padding of a chart without a data stage (an `ArgumentOutOfRangeException` from the scene
    without a location), the infinite tick index of a denormal step (`GetTicks` cast NaN to an array length: an
    `OverflowException` on .NET 8), the count of the `keyvals` of an impute beyond the range of an int (wrapped on
    .NET 8,
    saturated later), an empty field name on the channel of a boxplot, an error bar or an error band (a null group-by
    name: `ArgumentNullException`), and the message of a JSON nesting error that did not name `MaxJsonDepth`.
    Delivered by the numeric-safety and paint-budget package: the **`VegaLiteLimits.MaxPaintedPixels`** limit
    (250,000,000 device pixels per render, charged by `ScenePainter` before each item is painted and located at the
    mark; the shared resampled copies of pictures in `SceneImageSource`), the **defined numeric conversions**
    (`NumericConversions`, replacing the casts of numbers from specifications and data, and the fixes of the defects
    that they caused: the quantile step overflow on .NET 8 and the `limit` of a pivot), the located failure for a
    canvas whose size is not a finite number, and the **calibrated work budget** (the weights of the loess, density,
    polynomial regression, bootstrap, and window-frame work). Delivered by the time unit and grouping package (the
    Phase 13
    deferrals about time units and grouping, see 42 and 91): the **binned time units** (`binned*` and `binned: true`) and
    the `maxbins` form of a predicate, the `bin`, `aggregate`, and `timeUnit` of **facet fields**, and the same
    transforms of
    the **grouping channels of the composite marks**, with the retirement of `vl-timeunit-binned`,
    `vl-timeunit-maxbins`,
    `vl-facet-field-transform`, and `vl-composite-grouping`; the defect they found and fixed is the diagonal whisker of
    a
    rule over data that is binned already (the rule treated `x` and `x2` of a pre-binned channel as a range).
    Delivered by the expression, legend, offset, and invalid-data package: the nine **distribution functions** of the
    expression language (the table grows from 104 to 113 functions; `random` and the `sample*` functions stay
    unsupported, see 95), the **`legend.clipHeight`** of symbol legends (see 96, with the retirement of
    `vl-guide-legend-clipheight`), the **ranged offsets** of bars and areas (see 97, with the narrowing of
    `vl-offset-ranged` to `OffsetDecision.UnsupportedContinuous`), and **`config.scale.invalid`** (see 98, with the new
    identifier `vl-scale-invalid-value`). Delivered by the gallery and tooling package: the **documentation gallery**
    (18 original specifications under `docs/user_guide/examples/gallery`, drawn by the Pandoc filter, rendered strictly
    by `VegaLiteGalleryTests`, and shown in one montage image that `docs/images/render-images.ps1 -Check` keeps
    current), the **benchmark harness** (`tools/DemaConsulting.CanvasNet.VegaLite.Benchmarks`, a Stopwatch console
    project that the solution build compiles and that nothing runs automatically; it also checks how long the default
    limits take to stop a runaway specification), and the **visual-diff harness** (`tools/visual-diff`, developer
    tooling that draws a specification with CanvasNet and with Vega and Vega-Lite in Node and reports the RMSE). None of
    the three is a software item: they add no unit and no requirement of their own. What is deferred is listed in the
    deviations and in the user guide's list of unsupported features.

Every later phase adds members and types rather than changing the signatures delivered here.
