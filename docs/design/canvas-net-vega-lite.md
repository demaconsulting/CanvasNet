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
size. Rendering is static (no interaction), deterministic (byte-identical output), and uses no network, no
JavaScript runtime, and no code outside the .NET base class library and the core `CanvasNet` package. The package
implements the Vega-Lite 6 defaults and rules; specifications that declare version 5 or 6 in `$schema` are interpreted
with the version 6 defaults. Everything that the renderer recognizes but cannot draw yet is **reported with a stable
`vl-*` identifier and the exact location, never skipped silently** (see _Supported And Deferred Features_).

The first delivery (the first pull request, phases 1 to 3 of the roadmap) renders **a single view with inline data**:
the marks `bar`, `rect`, `point`, `circle`, `square`, `rule`, and `tick`, the channels `x`, `y`, `color`/`fill`/
`stroke`, `size`, and `opacity`, linear, band, and point scales, axes with d3-style tick selection and number
formatting, titles with subtitles, the Vega-Lite default theme and configuration, and a view layout with padding and
`autosize` of `pad` or `none`. Phase 4 adds the **data sources** of that single view: **local CSV, TSV, DSV, and JSON
files** read from a sandbox (the folder of the specification, extra data directories, or a caller callback), **named
datasets** (the top-level `datasets` of the specification and the caller's `Datasets` option), **generated
sequences**, `format.parse` conversions (no data-based inference unless `parse` is `"auto"`, as in Vega), and
`format.property`. Remote URLs are still
never fetched. Phase 5 adds **expressions and row transforms**: a bounded, closed-world subset of the Vega expression
language (57 pure functions, no reflection, no clock, no random source), the **static variable parameters** of the view
(a `value` or an `expr`, replaced by the caller's `Parameters`), and the `calculate`, `filter`, `fold`, `flatten`,
`sample`, `pivot`, and `lookup` transforms, which run between the data loader and the
scales; a sort by the aggregate of a
field and the row-bound `{"expr": ...}` paint values of the marks are evaluated against the transformed rows.
Phase 6 adds **aggregation**: the `aggregate`, `joinaggregate`, `window`, `bin`, `stack`, and `impute` transforms
(every Vega-Lite 6 aggregate operation, with a deterministic bootstrap for `ci0` and `ci1`), and the **implicit pipeline
of the encoding** - the `aggregate`, `bin`, `stack`, and `sort` properties of a channel (a bar chart now stacks by default,
as Vega-Lite does), the secondary channels `x2` and `y2` of bars, rects, and rules, the stacked, binned, and ranged
geometry of the marks, and the derived titles, tick counts, and domains of binned and stacked axes. Compositions, the other
transforms, selection parameters, and time are later phases; conditional encodings (with a `test`) and the legends are
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
  everything about the specification text, with three units. `SpecParser` reads the UTF-8 JSON into an immutable
  `SpecNode` tree with RFC 6901 pointers and one-based line and column, validates the shape of the known properties
  (including the supported transforms, predicates, and parameters), compiles the expressions of the supported sites,
  and warns about duplicate and unknown keys. `FeatureCatalog` lists every recognized-but-unsupported construct with a
  stable identifier. `ConfigResolver` merges the Vega and Vega-Lite default configuration, the configuration of the
  specification, and the caller's configuration. See _SpecModel Subsystem Design_
  (`canvas-net-vega-lite/spec-model.md`).
- **Data** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Data`, folder `Data/`): rows into
  memory, with four units. `DataTable` is the immutable typed value and table model (with the array, object, and regular
  expression values that expressions produce). `DataLoader` chooses the source
  of a view (inline `values`, a local file, a named dataset, or a sequence) and builds the table under the data
  limits; it also owns the row budget of the call, which the transforms share, and loads the source of a `lookup`.
  `DataFileResolver` finds and reads local data files inside the sandbox. `DelimitedTextParser` reads CSV,
  TSV, and DSV text and applies the `parse` conversions (and the opt-in type inference). See _Data Subsystem Design_
  (`canvas-net-vega-lite/data.md`).
- **Expressions** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Expressions`, folder
  `Expressions/`): the expression engine, with three units. `ExpressionParser` compiles the text of an expression in one
  bounded pass into an immutable postfix program. `ExpressionEvaluator` runs it with JavaScript coercion under a step
  budget. `ExpressionFunctions` is the closed table of 57 pure functions with the regular expression translation. See
  _Expressions Subsystem Design_ (`canvas-net-vega-lite/expressions.md`).
- **Transforms** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Transforms`, folder `Transforms/`): three
  units. `RowTransforms` computes the static parameter environment and applies the row transforms (`calculate`,
  `filter`, `fold`, `flatten`, `sample`, `pivot`, `lookup`) to the loaded table. `AggregateTransforms` computes the
  aggregating transforms (`aggregate`, `joinaggregate`, `window`, `bin`, `stack`, `impute`) and holds the operations,
  the bin algorithm, and the stack rules. `EncodingPlan` plans and runs the implicit bin, aggregate, and stack of a view's
  encoding and names the fields and titles every channel reads. See _Transforms Subsystem Design_
  (`canvas-net-vega-lite/transforms.md`).
- **Scales** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Scales`, folder `Scales/`): the numeric
  heart, with six units. `ScaleModel` maps values to pixels (linear, band, point, ordinal). `ScaleResolver` infers
  scale types, domains, ranges, and paddings from the specification and the order of discrete domains. `Ticks` is the
  d3-array tick algorithm.
  `NumberFormat` is the d3-format number formatter. `ColorSchemes` holds the 68 Vega color schemes (the `tableau10`
  category palette is the Vega-Lite default) and configures the color range of a scale from a scheme.
  `ColorInterpolator` interpolates colors in sRGB, HSL, CIELAB, and HCL. See
  _Scales Subsystem Design_ (`canvas-net-vega-lite/scales.md`).
- **Guides** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Guides`, folder `Guides/`): text and
  guide construction, with five units. `TextMeasurer` resolves fonts and measures and truncates text. `TitleBuilder`
  builds the title and subtitle. `AxisBuilder` builds axes (grid, ticks, labels, domain, title) and their extents.
  `LabelOverlap` removes overlapping labels (parity and greedy) for the axes and the legends. `LegendBuilder` plans and
  builds the symbol, gradient, and discrete gradient legends. See
  _Guides Subsystem Design_ (`canvas-net-vega-lite/guides.md`).
- **Layout** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Layout`, folder `Layout/`): two units.
  `ViewLayout` computes the data rectangle, the padding, the autosize canvas, the title placement, and the view
  background and border of a single view. `LegendLayout` places the legends around the data rectangle and the axes and
  adds their room to the canvas. See _Layout Subsystem Design_ (`canvas-net-vega-lite/layout.md`).
- **Marks** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Marks`, folder `Marks/`): the
  `MarkBuilder` unit, which turns rows, scales, and mark properties into scene items. See _Marks Subsystem Design_
  (`canvas-net-vega-lite/marks.md`).
- **Scenegraph** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Scenegraph`, folder
  `Scenegraph/`): the assertion surface and the painter, with three units. `SceneModel` is the immutable scene
  (groups, rectangles, rules, paths, symbols, text, bounds, and the item budget). `ScenePainter` draws a scene onto a
  `Surface`. `CssColor` parses CSS colors. See _Scenegraph Subsystem Design_
  (`canvas-net-vega-lite/scenegraph.md`).

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
remote URL (`FeatureCatalog.IsSchemeBearingUrl`). `FeatureCatalog` and `ConfigResolver` read the `SpecTree` and
`SpecNode` types of `SpecParser`. `DataLoader` reads `SpecNode`, asks `DataFileResolver` for the text of a data file,
hands delimited text to `DelimitedTextParser`, and builds `DataTable`; `DataFileResolver` depends only on the public
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
`Transforms` subsystem, so that validation, reports, and execution agree; `Scales` and `Transforms` therefore refer to each
other at the unit level (`EncodingPlan` uses `ScaleResolver.InferOrient`; `ScaleResolver` uses `EncodingPlan`), as the
orientation rule is one function used by both. `Expressions` never
refers to `Transforms`, and `Data` never refers to `Expressions` or `Transforms`. `SceneModel` depends on nothing else.
`TextMeasurer` reads the `SceneModel` text and bounds types; `ScenePainter` calls `TextMeasurer` and `CssColor`.
`TitleBuilder`, `AxisBuilder`, and `LegendBuilder` build `SceneModel` items using `TextMeasurer`; `AxisBuilder` and
`LegendBuilder` share `LabelOverlap`, `AxisBuilder` uses `ScaleModel`, `Ticks`, and `NumberFormat`, and `LegendBuilder`
uses `ResolvedScale`, `ScaleModel`, `Ticks`, and `NumberFormat`. `ViewLayout` builds the root of the scene around the
title block and the content that the builders produced; `LegendLayout` places the built legends of `LegendBuilder` (it
calls the grid layout of `LegendBuilder`) against the bounds of the axes and tells `ViewLayout` how far they reach.
`MarkBuilder` builds `SceneModel` items from the `DataTable` and the scales. The
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
  `SourceName`, and `Limits`. Parsing consumes `SourceName` and `Limits`; `BaseDirectory` (the folder of a file opened
  by path unless set), `DataPaths` (extra data directories, searched in order after the base directory), and
  `FileResolver` (a callback that supplies the bytes of a normalized logical path and replaces the file system
  entirely) define the file sandbox that `Measure` and `Render` use for a local `data.url`.
- **`VegaLiteRenderOptions`** (sealed, init-only, validated): `Scale` (finite, greater than zero, at most
  `Surface.MaxDimension`), `Background` (an `Rgba32` that replaces the specification and configuration background),
  `Config` (a Vega-Lite configuration as JSON object text, merged over the defaults and the specification's
  `config`), `Parameters` (copied and validated; the value of a variable parameter of the view, which replaces its `value`
  or `expr`) and `Datasets` (named row tables
  that replace a top-level dataset of the same name; copied and validated),
  `TimeZone` (`null` is UTC; reserved for the time phase), `ContainerWidth` and `ContainerHeight` (the size of a
  `"container"` chart; 400 by 300 by default), `Fonts`, `Limits`, the handlers `WarningHandler` and
  `UnsupportedFeatureHandler`, and `ThrowOnUnsupportedFeature`.
- **`VegaLiteFontOptions`** (sealed, immutable): `UseSystemFonts` (whether installed fonts are searched for an
  explicitly named family; `false` by default) and `AdditionalDirectories` (extra directories scanned first; empty by
  default, copied and validated when set).
- **`VegaLiteLimits`** (sealed, init-only): the sixteen resource limits listed in _Risk Control Measures_, with the
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
| Local data files (`data.url`) | Inbound | UTF-8 CSV, TSV, DSV, or JSON text in a file under the base directory or a data path | Relative paths only; confined to the roots; no symbolic link or junction below a root; at most `MaxDataFiles` files and `MaxDataBytes` bytes each; read only by `Measure` and `Render`; never from the network |
| `VegaLiteDocument` properties | Outbound | Text, numbers, read-only lists | Describe the text only: no data is loaded, no file is opened, no transform or parameter is evaluated (the expressions are compiled and checked) |
| `VegaLiteDocument.Measure(...)` | Inbound and outbound | `VegaLiteRenderOptions`, a `CancellationToken`; a `VegaLiteSize` | Same checks, reports, and order as `Render`, nothing is drawn; always equals the size `Render` produces |
| `VegaLiteDocument.Render(...)` (natural size) | Inbound and outbound | `VegaLiteRenderOptions`, a `CancellationToken`; a `Surface` | Natural size times `Scale`, rounded half away from zero, each side from 1 to `Surface.MaxDimension`, validated before any report is delivered; caller owns the surface; byte-identical output for identical input |
| `VegaLiteDocument.Render(int, int, ...)` (exact size) | Inbound and outbound | Width, height, options, a token; a `Surface` | Width and height from 1 to `Surface.MaxDimension`, validated first; the natural chart is scaled uniformly to fit and centered, margins take the background; `Scale` is not used |
| `VegaLiteRenderOptions.WarningHandler`, `UnsupportedFeatureHandler` | Outbound | Delegates receiving `VegaLiteDiagnostic` and `VegaLiteUnsupportedFeature` | Called once per distinct report, in document order, on the calling thread, after the work finished and before the method returns or throws |
| `VegaLiteSpecException` | Outbound | Exception with `Location` | Thrown for malformed JSON, nesting deeper than `MaxJsonDepth`, a known property of the wrong type or value, a string longer than `MaxStringLength`, plain Vega, an invalid expression (a syntax error, a text longer than `MaxExpressionLength`, nesting deeper than `MaxExpressionDepth`, or a reference to an undeclared parameter; the message names the expression offset), a malformed transform, predicate, or parameter (a cycle, a repeated or reserved name), and, at render time, an invalid axis orientation, axis format, or scale domain or range |
| `VegaLiteRenderException` | Outbound | Exception with `Location` | Thrown when a data, transform, expression, scale, axis, or scene limit is exceeded (`MaxDataRows`, `MaxFieldCount`, `MaxStringLength`, `MaxEvaluationSteps`, `MaxDataFiles`, `MaxDataBytes`, `MaxScaleDomainSize`, `MaxTicks`, `MaxSceneItems`) and when local data cannot be used: no file source for a local `url`, a path the sandbox refuses, a link, a missing or unreadable file, invalid UTF-8, invalid JSON or delimited text, an undefined named dataset, or an invalid sequence; also for an invalid or too slow regular expression of an expression; the location is the `url`, `name`, `sequence`, transform, or expression that caused it |
| `VegaLiteUnsupportedFeatureException` | Outbound | Exception with `Feature` and `Location` | Thrown only when `ThrowOnUnsupportedFeature` is set, at the first unsupported feature in document order; the report is delivered to the handler first |
| `ArgumentOutOfRangeException`, `ArgumentException` | Outbound | Exception | Thrown for an invalid option value when it is set, for a scaled or explicit size outside 1 to `Surface.MaxDimension`, and for a `Parameters` name the document does not declare |
| `OperationCanceledException` | Outbound | Exception | Thrown, never wrapped, when the token is canceled before or during the work |
| `InvalidDataException` | Outbound | Exception | Thrown, not as a `VegaLiteSpecException`, when the source exceeds `MaxSourceLength` (no location is meaningful) |
<!-- markdownlint-enable MD013 -->

Opening a document never loads data, never opens another file, never evaluates anything, and never uses the network (it
compiles expressions and checks their shape); only `Measure` and
`Render` read data, and only from the sandbox described in _Risk Control Measures_. The types of the
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
  and the **`Rendering`** subsystem (`TextRenderer`, for text measurement and drawing)

The dependency is declared as the package dependency of `DemaConsulting.CanvasNet.VegaLite`; the public API exposes
the core `Surface` and `Rgba32`. The dependency is modeled in the SysML2 model as the `usesCanvas`, `usesDrawing`,
`usesFonts`, `usesGeometry`, and `usesRendering` dependencies of the `CanvasNetVegaLite` system (the model records
dependencies at the system level only, as for every other satellite system). No core API and no `InternalsVisibleTo`
entry was added for the system.

**`CanvasNetVegaLite` must never reference `DemaConsulting.CanvasNet.Charts`, `DemaConsulting.CanvasNet.Svg`,
`DemaConsulting.CanvasNet.Pdf`, `DemaConsulting.CanvasNet.Pptx`, `DemaConsulting.CanvasNet.Vsdx`,
`DemaConsulting.CanvasNet.Render3D`, or `DemaConsulting.CanvasNet.Scad`.** It is a chart-specification front end
unrelated to those systems, and the consumers that host it (such as `CanvasNetTool`) depend on it, never the reverse.

Beyond the project reference, `CanvasNetVegaLite` uses only the .NET base class library: `System.Text.Json`
(`Utf8JsonReader`, for the specification parser, and `JsonDocument`, for checking a configuration text),
`System.Text.RegularExpressions` (a generated regular expression for the d3 format specifier, and the safe .NET translation
of the regular expressions of an expression),
`System.Numerics` (`BigInteger`, for exact decimal expansion in number formatting, and `Matrix3x2` in the painter),
`System.IO`, `System.Text`, `System.Globalization` (invariant-culture number parsing and formatting), and
`System.Collections.ObjectModel`. These are platform types available on every target framework, not OTS Software
Items, and require no runtime NuGet dependency. The d3 and Vega algorithms and default values that the package uses
are **local ported source with attribution**, not a consumed third-party component (see _Third-Party Provenance And
Attribution_), so the system introduces no OTS Software Item and no Shared Package (OTS: N/A - no OTS items).

This is an ordinary, same-repository, system-to-system dependency: it is neither an OTS Software Item nor a Shared
Package (that category is scoped to a package produced by a _different_ repository).

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
  | `MaxStringLength` | 1,048,576 characters | `SpecParser`, `ExpressionEvaluator`, `RowTransforms`, `AggregateTransforms` | `VegaLiteSpecException` (specification text); `VegaLiteRenderException` for a string an expression or a transform would produce (also a group value, a `distinct` key, and a name or value that a transform copies) |
  | `MaxDataRows` | 1,000,000 (total per call) | `DataTable`, `DataLoader`, `DelimitedTextParser`, `RowTransforms`, `AggregateTransforms`, `EncodingTransforms` | `VegaLiteRenderException` before any row is converted or allocated (also for a sequence, a lookup source, the output of a fold or flatten, the groups of an aggregate, the rows of an impute, and the `keyvals` of an impute) |
  | `MaxFieldCount` | 1,024 | `DataTable`, `DataLoader`, `DelimitedTextParser`, `RowTransforms`, `AggregateTransforms` | `VegaLiteRenderException` (also for the fields a transform adds) |
  | `MaxDataFiles` | 64 (distinct files per call) | `DataFileResolver` | `VegaLiteRenderException`; the file that would exceed the limit is not read |
  | `MaxDataBytes` | 64 MiB (per file) | `DataFileResolver` | `VegaLiteRenderException`; reading stops one chunk past the limit, so an endless stream cannot exhaust memory |
  | `MaxScaleDomainSize` | 100,000 values | `ScaleResolver`, `BinAlgorithm` | `VegaLiteRenderException` (also for the number of bins of a `bin`, and for the categories of a sort by an aggregate) |
  | `MaxTicks` | 1,000 per axis | `Ticks`, `AxisBuilder` | `VegaLiteRenderException` (also for categories) |
  | `MaxSceneItems` | 2,000,000 | `MarkBuilder`, `SceneBudget`, `VegaLiteDocument` | `VegaLiteRenderException`, counted as each item is created, before it is kept |
  | `MaxDiagnostics` | 1,000 | `VegaLiteReportSink` | Further reports collapse into one `vl-diagnostics-suppressed` notice; strict mode is unaffected |
  | `MaxExpressionLength` | 4,096 characters | `ExpressionParser` (when the document is opened) | `VegaLiteSpecException` located at the expression, offset in the message; checked before tokenizing |
  | `MaxExpressionDepth` | 64 (1 to 256) | `ExpressionParser` (when the document is opened) | `VegaLiteSpecException`; counts nesting (groups, brackets, arguments, literals, branches, unary operands), not length |
  | `MaxEvaluationSteps` | 50,000,000 (total per call) | `ExpressionEvaluator`, `ExpressionFunctionContext`, `WorkMeter` | `VegaLiteRenderException` located at the expression or the transform; one step per executed instruction plus one per 64 characters or elements of function work, shared by the parameters, the transforms (also the comparisons of a filter predicate and the rows, sorts, window frames, and bootstrap resamples of the aggregating transforms), the implicit pipeline, the sort of a scale, and the mark values of the call |
  | `MaxViews`, `MaxCompositionDepth` | 1,000, 16 (1 to 64) | Reserved for the composition phase | Not yet enforced: the corresponding constructs are reported as unsupported |
  <!-- markdownlint-enable MD013 -->

- **Data loaded before the scene budget.** The data stage runs before any scene item is charged and before any scale
  exists, so a data failure or a data limit never depends on `MaxSceneItems`; the marks that the loaded rows produce
  are then charged to the scene budget as before.
- **Pixel budget.** The size of the surface is bounded by `Surface.MaxDimension` on each side: the scale and the
  explicit width and height are validated before any pixel memory is allocated, and a clip layer in the painter is only
  as large as the clip's pixel box.
- **Finite arithmetic and bounded stroking.** A number of the specification is any finite `double`, but the geometry is
  `float` and the layout is built from sums and spans that can overflow. The tick generator returns no ticks for a span
  that overflows the double range or has an infinite end and drops any generated tick that is not finite, so a NaN
  never reaches the layout; the painter validates a stroke width, dash pattern, and dash offset after their conversion
  to `float` (an unusable width draws nothing, an overflowing offset counts as zero, an unusable pattern is solid) and
  draws a dash pattern solid when its period is below 0.25 surface pixels or the path would have more than 50,000
  dashes, so a specification cannot make the stroker do unbounded work or throw an argument exception.
- **Error classification.** The library reports a limit of the parser as a `VegaLiteSpecException` (depth, string
  length), a source longer than `MaxSourceLength` as an `InvalidDataException`, a limit of the layout or the render as a
  `VegaLiteRenderException` whose message names the limit (`the limit of N` or `VegaLiteLimits.<Name>`), and a size of
  the chart outside the surface limits as an `ArgumentOutOfRangeException` for the parameter `options`. The CanvasNetTool
  maps them to `vega-lite-error`, `corrupt-input` (as for an overlong SCAD model), `budget-exceeded`, and `size-limit`
  respectively; no other argument failure is a size or a usage problem.
- **Sandboxed local data, no network.** Opening reads only the text it is given. A data `url` is read only by
  `Measure` and `Render`, and only when it is a relative local path: a URL with a scheme, a drive letter, an absolute
  path, or a network path reports `vl-data-remote-url`, is **never fetched and never handed to the file resolver or
  the callback**, and uses an empty dataset (strict mode throws). A relative path is confined to a root (the base
  directory, then each data path, in order) by normalizing `.` and `..` and refusing a path that leaves its root;
  rooted, drive, UNC, colon, empty, and control-character paths are refused outright; every component below a root
  is checked before the file is opened and a symbolic link or junction is refused, and the check fails closed when
  it cannot answer. With a `FileResolver` callback no file-system access happens at all. A file is read as bytes under
  `MaxDataBytes` and decoded as strict UTF-8, and each call reads each distinct file at most once. A local file that
  cannot be used (including an existing directory in place of a file, an open that fails, and a callback or callback
  stream that throws) is a `VegaLiteRenderException`, in lenient and strict mode alike, never an empty dataset. The
  residual risk is a concurrent writer that replaces a checked path with a link before it is opened; the resolver
  does not defend against it, and a deployment that renders files an attacker can modify must not share the
  directory with them. No code in the specification is ever executed: an expression is text that a bounded interpreter
  runs over data values (see _Closed-world expressions_ below), never host code.
- **Closed-world expressions.** The expression language is a subset of JavaScript that an interpreter written for this
  package runs: there is no reflection, `eval`, assignment, `new`, function expression, file or network access, clock, or
  random source, and a function exists only if it is in the frozen table of 57 pure functions. An expression is compiled
  when the document is opened (length and nesting limits, no recursion in proportion to the text, located syntax errors),
  then run as a loop over a postfix program under an exact step budget (`MaxEvaluationSteps`,
  shared by every expression of
  the call and charged for work proportional to string and array sizes), a string limit, and cancellation checks every
  1,024 steps; a regular expression runs on the linear-time engine or, where the pattern needs look-around or
  back-references, on the backtracking engine under a 100 ms timeout that fails closed, with a pattern of at most 1,024
  characters. A result is a pure function of the datum and the parameters, so it is deterministic and culture-independent.
- **Bounded transforms.** Every derived table is bounded before it is allocated by the shared
  row budget (`MaxDataRows`, with
    the peak of an input and an output counted for fold, flatten, aggregate, and impute), `MaxFieldCount`, and 2^25 cells
    (which also bound the per-group state and the values that an aggregate operation keeps); the number of bins is bounded
    by
    `MaxScaleDomainSize`; the work of every row, sort, window frame, and bootstrap is charged to `MaxEvaluationSteps`,
    so a
    hostile specification fails closed and located; a lookup source
  goes through the same sandboxed loader and file cache as the main data; `sample` is seeded from the JSON pointer, never
  from a clock, and so is the bootstrap of the confidence interval (`ci0`, `ci1`); and the transforms that
  the renderer does
  not compute are skipped and reported, never approximated.
  - **Cancellation.** A `CancellationToken` is honored before any work, in the data and transform stages (at the start of
    every transform and at least every 1,024 rows, also inside the sorts, the bootstrap, and the implicit pipeline), in
    every expression evaluation (at the start and every 1,024 steps), in
    the scale stage, per tick of an
  axis, per 1,024 rows of the marks, and while painting. The exception is never wrapped.
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
- **Deterministic and culture-independent.** Numbers are parsed and formatted with the invariant culture, text is
  measured with the bundled fonts, and no wall clock or environment variable is consulted, so results do not depend on
  the host's locale or installed fonts.

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
   `ExpressionParser`, checks the transforms, predicates, and parameters, and records `vl-unknown-property` warnings.
4. **Describe**: `SpecMetadata` reads the schema URL and major version, the title, the mark types, the data references,
   and the parameter names from the tree; `FeatureCatalog.Visit` lists the recognized-but-unsupported constructs (the
   functions, view state, and operators that the compiled expressions collected among them). The
   parse warnings and the catalog reports are merged once, sorted by line and column (stable), and kept as the
   document's reports.
5. **Output**: an immutable `VegaLiteDocument`, safe to share between threads.

**Measure and render path (`Measure`, `Render(options)`):**

1. **Choose options**: the given options, or the defaults.
2. **Prepare the chart** (reads only the data files and delivers nothing): `ConfigResolver` merges the defaults, the
   specification's `config`, and the caller's `Config`; a `TextMeasurer` is created for the font options. When the
   root is a single view (no `layer`, `concat`, `hconcat`, `vconcat`, `facet`, or `repeat`) with a supported mark, one
   `DataLoader` serves the call (with its own file cache, file count, and row budget) and reads the data first,
   before any scene item is charged: inline rows, the local file named by a relative `url` (through
   `DataFileResolver`, then `DelimitedTextParser` for CSV, TSV, or DSV, or `JsonDocument` for JSON), a named dataset
   (the caller's `Datasets` first, then the top-level `datasets`), or a generated sequence; the `parse` conversions
   are applied, and the values that an explicit conversion could not read are collected as `vl-data-parse-failed`
   warnings. The **static parameters** are then evaluated once in dependency order (the
   caller's `Parameters` replacing a
   definition), and the **transforms** of the view run in the written order (`RowTransforms`: calculate, filter,
   fold, flatten, sample, pivot, and lookup, whose source is read by the same `DataLoader`; and, through
   `AggregateTransforms`, aggregate, joinaggregate, window, bin, stack, and impute), each producing a new table
   within the shared row budget; the findings they can recover from are collected as `vl-expr-runtime` and
   `vl-transform-runtime` warnings. `EncodingPlan` then plans the **implicit pipeline** of the encoding (the field names,
   titles, bins, aggregate, and stack) and `EncodingTransforms` runs it in Vega-Lite's order: the bins of the binned
   fields, the **Raw** table, the aggregate of the aggregating channels, the stack, and the **Main** table.
   `ScaleResolver` then infers the scales from the plan over those tables (a domain reads Main; a sort by an
   aggregate and a
   `scale.domain` of `unaggregated` read Raw). `ViewLayout` resolves the view request (size, padding,
   autosize, background, view style) with the discrete-step hints of the scales; `TitleBuilder` builds the title. If
   there is no drawable single view the layout is computed around the title alone (an empty view, as for a
   composition or an unsupported mark). Otherwise the chart is built: the axes are built for the current data
   rectangle, their extents measured, and the layout re-measured, at most three times, until the data rectangle is
   stable (it is stable at once for fixed sizes); the `MarkBuilder` then builds the marks once for the final rectangle
   from the Main table
   (evaluating the row-bound paint expressions for each row with the evaluator of the call),
   the item budget being charged for the axes and for each mark item as it is created; and `ViewLayout.Build`
   assembles the
   scene (root, view group, grids, axes behind the marks, the marks group, axes in front, title) and the natural
   size. When this fails with a specification or render error, the reports found so far are still delivered before
   the error is rethrown.
3. **Validate**: the natural size times the scale is validated (each side from 1 to `Surface.MaxDimension`), then the
   `Parameters` names, then the token.
4. **Record**: the reports of the document (warnings, then catalog reports, in document order) are recorded in the
   `VegaLiteReportSink`, which de-duplicates, caps, and, in strict mode, throws at the first unsupported feature;
   the data warnings of this call follow them, in this order: the warnings of the loader (`vl-data-parse-failed`, also
   for a lookup source), then those of the transforms (`vl-expr-runtime`, `vl-transform-runtime`); they are also delivered
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
  `key`, and the `bind` and `empty` of a parameter) or, when it changes what is drawn (selections and conditions on a
  parameter), reported
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
| Vega-Lite | BSD-3-Clause | `ConfigResolver`, `ScaleResolver`, `ColorSchemes`, `AxisBuilder`, `MarkBuilder`, `EncodingPlan`, `EncodingTransforms`, `AggregateTransforms`, `StackRules` | Default configuration values (background, padding, view size, step, mark, bar, rect, tick, rule, scale, header, and spacing configuration), the `tableau10` default category colors, the precedence of mark, view, title, and style properties, the scale inference rules (type by field type, channel, and mark; domain order; zero, nice, padding, and reverse rules; default size and opacity ranges; step sizing), the default axis tick count and axis defaults and rules, and the inferred orientation, band size, opacity, color, and invalid-data rules of the supported marks; and, since phase 6, the compile rules that turn an encoding into transforms (the order bin, aggregate, stack; the group-by and stack-by fields; which marks stack, in which direction and with which default offset; the derived field names and default titles), the domain rules of stacked, binned, and aggregated fields, the geometry of a binned bar or rect, and the tick count of a binned axis |
| Vega | BSD-3-Clause | `ConfigResolver`, `ViewLayout`, `LegendLayout`, `TextMeasurer`, `TitleBuilder`, `AxisBuilder`, `LabelOverlap`, `LegendBuilder`, `SceneModel`, `ScenePainter`, `Ticks`, `ScaleModel`, `ScaleResolver`, `BinAlgorithm`, `AggregateMeasures`, `AggregateTransforms`, `StackRules`, `ColorSchemes`, `ColorInterpolator` | Default Vega configuration values, the guide, group, and mark styles, the title and axis layout constants and offsets, the axis geometry (tick and label positions, band offset, rounding, half-pixel translate, extent clamping, label overlap removal), the tick-count rules, the `bandspace` rule, the text baseline offsets and line-height and truncation rules, the group stroke offset, the symbol shape geometry, and the view-layout rules for `autosize` and title placement; and the legend rules (entry sizes and offsets, the gradient bar and its stops, the grid layout of entries, and the placement of legends by orient, offset, margin, and anchor); and, since phase 6, the bin algorithm and the assignment rules of the `bin` transform, the semantics of the aggregate operations, and the behavior rules of the `window`, `stack`, and `impute` transforms; and, since phase 7, the palette table of 68 color schemes, the piecewise sRGB ramp, the interpolation types, the range and quantize helpers, and the rules that give a scheme its number of colors |
| d3-array | ISC | `Ticks`, `AggregateMeasures` | The `ticks`, `tickIncrement`, and `tickStep` algorithms, and the `quantileSorted` interpolation (the R-7 method) behind the median, the quartiles, and the bootstrap interval |
| d3-format | ISC | `NumberFormat` | The format specifier grammar and the `format`, `formatPrefix`, `precisionFixed`, `precisionPrefix`, and `precisionRound` behavior for the numeric types |
| d3-scale | ISC | `ScaleModel`, `Ticks`, `NumberFormat` | The arithmetic of the linear, band, and point scales, the linear `nice` method, and the precision rule of `tickFormat` |
| d3-scale-chromatic | ISC (and Apache-2.0 for the ColorBrewer colors) | `ColorSchemes` | The colors of `category10`, `observable10`, `rainbow`, `sinebow`, and the ColorBrewer qualitative sets |
| d3-interpolate, d3-color | ISC | `ColorInterpolator` | The sRGB, HSL, CIELAB, and HCL color interpolation and the piecewise ramp |
| ColorBrewer | Apache-2.0 | `ColorSchemes` (`ColorSchemeData`) | The color values of the ColorBrewer qualitative sets and of the sequential and diverging ramps that Vega names after it |
| matplotlib colormaps, cividis, Turbo | CC0, a Battelle permissive license, Apache-2.0 | `ColorSchemes` (`ColorSchemeData`) | The color values of `viridis`, `magma`, `inferno`, and `plasma` (CC0); `cividis`; and `turbo` |
<!-- markdownlint-enable MD013 -->

The `Expressions` subsystem and the `RowTransforms` unit (with `RowSampler`, `TableLayout`, and `ParameterEnvironment`)
port **nothing**: they are original code written from the public documentation
of the Vega expression language, the Vega-Lite transforms and parameters, and ECMAScript, so they have no row in the table
and carry no `Portions derived from` header; the `sample` transform is built from the published FNV-1a and SplitMix64
descriptions and the classic reservoir algorithm. The `AggregateTransforms` and `EncodingPlan` units re-implement
the rules named in the table from their public description (the bin algorithm is a port of the Vega `bin`), carry the
`Portions derived from` header where they contain ported logic, and use the same published FNV-1a and SplitMix64
descriptions for the deterministic bootstrap. `src/DemaConsulting.CanvasNet.VegaLite/third-party/README.md` records
this, with the upstream files that were read.

The obligations of the licenses are met as follows:

- The license texts ship with the package: `src/DemaConsulting.CanvasNet.VegaLite/third-party/LICENSE-vega.txt`
  (BSD-3-Clause), `LICENSE-d3.txt` (ISC), `LICENSE-colorbrewer.txt` with `LICENSE-apache-2.0.txt` (the ColorBrewer
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

This table is the exact boundary of the **delivered phases** (1 to 7). The renderer does not imply general Vega-Lite
support: a recognized construct outside the table is reported with the identifier shown, never skipped silently, and
strict mode throws at the first report. The reports carry the RFC 6901 pointer and the one-based line and column of the
construct.

<!-- markdownlint-disable MD013 -->
| Area | Supported | Reported (identifier) when outside the supported subset |
| ---- | --------- | ------------------------------------------------------- |
| Schema | `$schema` of Vega-Lite version 5 or 6, interpreted with the version 6 defaults; no `$schema` | Another major version, or a URL naming no Vega-Lite version: warning `vl-schema-version` (still rendered with the version 6 defaults); a malformed value is a `VegaLiteSpecException`; plain Vega is rejected |
| Views | One single view | `layer`, `concat`, `hconcat`, `vconcat`, `facet`, `repeat`: `vl-composition-<name>` (only the title, background, and empty view are drawn; nested views are examined too); `projection`: `vl-projection` |
| Data | Inline `data.values` (an array of objects, one row per object with nested values flattened to path fields, or of primitives, one row `{data: value}`); a **relative local `data.url`** read as JSON (the default), CSV, TSV, or DSV (`format.type`, else the extension `.csv` or `.tsv`, else JSON; DSV uses `format.delimiter`, a comma when omitted); `format.property` (a dotted path to the array of rows in a JSON file); `format.parse` as `"auto"` (explicit per-column inference, never the default), `null`, or a field map of `number`, `boolean`, `date`, and `string` (named fields only; delimited text keeps its cells as strings otherwise), with ISO 8601 dates in UTC; **named datasets** (`data.name` found in the caller's `Datasets`, then in the top-level `datasets` arrays); `data.sequence` (`start`, exclusive `stop`, `step`, `as`) | A missing or empty `values` array or a `data` object without a source: warning `vl-data-empty` (empty chart); `url` with a scheme, drive letter, absolute path, or network path: `vl-data-remote-url` (never fetched, never given to a resolver, empty dataset); a custom `parse` date format (`date:'%Y'`, `utc:'%Y'`): `vl-data-parse-date-format` (the field is left as read); `values` (or a referenced dataset) as text: `vl-data-values-text`; `sphere`, `graticule`, topojson, or `values` (or a referenced dataset) as an object: `vl-data-geo`. Not a report but a `VegaLiteRenderException`: a local file that is missing, refused by the sandbox, a link, a directory in place of a file, unreadable (an open failure, or a failing callback or stream), invalid UTF-8, invalid JSON or delimited text, or over a limit; an undefined `name`; a sequence with a missing or non-finite `start` or `stop` or a zero, non-finite, or non-numeric `step`. Values that an explicit `parse` could not convert become null and give the warning `vl-data-parse-failed` |
| Marks | `bar`, `rect`, `point`, `circle`, `square`, `rule`, `tick` (a mark given as a string or as a definition object with `type`) | Every other recognized mark type: `vl-mark-<type>`; the chart is drawn without marks and without axes |
| Channels | `x`, `y`, `color`, `fill`, `stroke`, `size`, `opacity`, `fillOpacity`, `strokeOpacity`, `strokeWidth`, `strokeDash`, `shape`, `angle` as `field`, literal `value`, or literal `datum` where the channel permits (`shape` on a `point` mark, `angle` on a `point`, `circle`, or `square`, `strokeDash` on every mark but a `rect`); `x2` and `y2` (the range of a `bar`, `rect`, or `rule` that has the channel they extend; a field, a datum, or a pixel `value`); `order` when it orders a stack; `tooltip`, `href`, `description`, and `key` are accepted and ignored without a report | Every other recognized channel: `vl-channel-<name>`; a supported channel that the mark does not draw (`shape`, `angle`, `strokeDash`): `vl-channel-<name>` (ignored); `x2` and `y2` of another mark or without their primary channel: `vl-channel-x2`, `vl-channel-y2`; `order` that orders no stack: `vl-channel-order`; `longitude`, `latitude`, `longitude2`, `latitude2`: `vl-channel-geo` |
| Field types | `quantitative`, `nominal`, `ordinal` | `temporal`: `vl-type-temporal`; `geojson`: `vl-type-geojson` |
| Encoding properties | `field`, `type`, `value` (a constant, or `{"expr": ...}` for color, fill, stroke, opacity, and the size of a symbol mark), `datum`, `title`, `scale`, `axis`, `legend` (an object of legend properties, or null), `aggregate` (every operation, with `argmax` and `argmin` as `{"argmax": field}` and the exponential operations as `{"exponential": r}`; a count needs no field), `bin` on `x` and `y` (true, an object of `maxbins`, `step`, `steps`, `minstep`, `divide`, `base`, `anchor`, `nice`, and a numeric `extent`, or `"binned"` with `x2` or a `step`) and on the channels that have a scale (true or an object, with Vega-Lite's default of 6 bins, 4 for `strokeDash`, and 10 for `angle`), `stack` (`zero`, `center`, `normalize`, true, false, null; a bar stacks by default and the other supported marks only with an explicit `stack`), and `sort` (ascending, descending, null, an array of values, a channel shorthand such as `"-y"`, `{"encoding", "order"}`, or an object with any aggregate `op` but `argmin`, `argmax`, `values`, and the exponential ones, an optional `field`, and an optional `order`), and `condition` on the color, fill, stroke, opacity, fillOpacity, strokeOpacity, strokeWidth, strokeDash, size, shape, and angle channels (an object or an array of objects with a `test` - an expression, or a predicate of `equal`, `lt`, `lte`, `gt`, `gte`, `range`, `oneOf`, `valid`, `and`, `or`, `not`, or a variable parameter - and a `value`, or a field definition: the first condition that holds gives the row its value, else the channel's own value or scale, else the default of the mark; conditions alone create no legend) | `timeUnit`, `impute` of a channel, `bandPosition`: `vl-encoding-<name>`; `condition` on another channel: `vl-encoding-condition`; a condition on a `param`, or a test that names a selection or a parameter that is not a variable: `vl-encoding-condition-param` (the condition never holds); a time unit or a date object in a test: `vl-encoding-condition-test` (the condition never holds); `bin` of a channel that cannot bin (`order`, a secondary channel) and `"binned"` on a channel other than `x` and `y`: `vl-encoding-bin`; a `bin` whose `extent` is a parameter: `vl-encoding-bin-extent-param` (the extent of the data is used); an explicit `stack` on a scale that is not linear: `vl-encoding-stack-nonlinear` (the marks of a log, pow, sqrt, or symlog scale are not stacked); a `sort` that is not applied: `vl-encoding-sort` |
| Scales | Types linear, log, pow, sqrt, symlog, sequential, band, point, ordinal, quantile, quantize, threshold, and bin-ordinal, accepted for a channel and a field type as Vega-Lite's `channelSupportScaleType` and `scaleTypeSupportDataType` allow (a type that cannot be used is replaced by the default one with a warning; a diverging color scale is a sequential one with a `domainMid`); a log scale with the `base`, a pow scale with the `exponent`, a symlog scale with the `constant`; a log scale has no zero, is rounded out to powers of its base, and refuses a domain that includes zero; the bin-ordinal scale of a binned quantitative color, with one color per bin and the ranges of the bins in its legend; the properties `type`, `domain` (an array, or `"unaggregated"` for a channel that aggregates with `mean`, `median`, `q1`, `q3`, `min`, or `max`), `domainMid`, `domainMin`, `domainMax`, `range` (an array, or the name of an entry of `config.range`), `rangeMin`, `rangeMax`, `zero`, `nice`, `padding`, `reverse`, `paddingInner`, `paddingOuter`, `align`, `round`, `clamp`, `interpolate` (`rgb`, `hsl`, `hsl-long`, `lab`, `hcl`, `hcl-long`, with a `gamma` for sRGB), and `scheme` (any of the 68 Vega color schemes by name, a list of colors, or an object with `name`, `count`, and `extent`); the domain of a stacked, ranged, or binned field | `identity`, `time`, and `utc` scale types: `vl-scale-<type>`; every other scale property (`bins`, `domainRaw`, `interval`, and so on): `vl-scale-property-<name>`; a `cubehelix` interpolation: `vl-scale-interpolate-cubehelix` (RGB is used); a type that the channel or the field type cannot use: warning `vl-scale-type-ignored`; an unknown scale type value or an unknown color scheme name is a `VegaLiteSpecException` |
| Axes | Grid, ticks, labels, domain, title, orient, offset, minimum and maximum extent, `tickCount`, `tickMinStep`, `values`, d3 `format`, `labelAngle`, `labelAlign`, `labelBaseline`, `labelPadding`, `labelOffset`, `labelLimit`, `labelFlush`, `labelFlushOffset`, `labelOverlap`, `labelSeparation`, the font, color, opacity, dash, width, and cap properties of the parts, `titleAnchor`, `titleAngle`, `titlePadding`, `zindex`, and the configuration precedence of Vega-Lite | `axis.encode`: `vl-guide-encode`; `labelBound`, `tickBand`, `tickExtra`, `position`, `translate`, `bandPosition`, `titleX`, `titleY`: `vl-guide-axis-<name>`; `formatType`: `vl-format-type-custom`; `labelExpr`: `vl-expr-label` |
| Legends | Color, fill, stroke, shape, size, opacity, strokeWidth, and strokeDash legends: symbol legends (one entry per value of a discrete scale, per tick of a continuous one, or per class of a discretizing one), gradient legends for a continuous color scale and discrete gradients for a discretizing one, merged when channels share a field and a domain, laid out by orient (right, left, top, bottom, the four corners, none) with the `config.legend` and `config.legend.layout` properties, `legend: null` to switch one off | `legend.encode`: `vl-guide-encode`; `legend.labelExpr`: `vl-expr-label`; `legend.formatType`: `vl-format-type-custom`; a `titleOrient` other than `top`: `vl-guide-legend-titleorient`; `clipHeight`: `vl-guide-legend-clipheight`; a `symbolType` that is a path: `vl-shape-path` |
| Titles | `text` (a string or an array of lines), `subtitle`, `anchor`, `orient`, `offset`, `frame`, the font, color, and alignment properties, `limit`, `angle`, `dx`, `dy` | `{"expr": ...}`: `vl-expr-ref` |
| Layout | `width` and `height` as numbers, `{step}`, or `"container"` (the container size is an option); `padding` as a number or object; `autosize` of `pad` or `none` (with `contains`); `background`; the `view` configuration (size, fill, stroke) | `autosize` of `fit`, `fit-x`, `fit-y`: `vl-layout-autosize-<type>` (laid out as `pad`); `{"expr": ...}` in a size, padding, background, or view: `vl-expr-ref` |
| Configuration | Vega-Lite 6 and Vega defaults, the specification's `config`, and the caller's `Config`, merged in that order; the `style` mechanism; the default categorical palette is `tableau10`; `config.range` (the named ranges `category`, `ordinal`, `ramp`, `heatmap`, `diverging`, and `symbol`), `config.scale` (including `quantileCount` and `quantizeCount`), and `config.legend` with `config.legend.layout` | N/A (unknown keys are ignored) |
| Expressions | A closed subset of the Vega expression language in the supported sites: literals (null, booleans, numbers, strings, regular expressions, arrays, objects) and the constants `PI`, `E`, `NaN`, `Infinity`; `datum`, `datum.x`, `datum['a b']`, member and index access; the unary `+ - !`, the arithmetic, comparison, and equality operators, the short-circuit logical and-or operators, and `?:`; 57 whitelisted pure functions (math, text and array, type and conversion, regular expressions); parameter references. Supported sites: the `expr` of a variable parameter, `calculate`, a string `filter`, and the `{"expr": ...}` paint values of the drawn view (mark `color`, `fill`, `stroke`, `opacity`, `strokeWidth`, and the `size` of a symbol mark; the encoding `value` of `color`, `fill`, `stroke`, `opacity`, and the size of a symbol mark) | A function outside the table (date, time, format, color, geographic, scale, statistical, and data-flow functions): `vl-expr-function-<name>`; `signal`, `event`, `item`, and an undeclared `width`, `height`, `padding`, `autosize`, `background`, or `cursor`: `vl-expr-view-state`; the bitwise (including the unary `~`) and shift operators and `in`: `vl-expr-construct-<name>` (each evaluates to null, at the expression string with the offset in the message); an `{"expr": ...}` anywhere else (title, size, padding, background, view, other mark properties, other encoding values): `vl-expr-ref`; `axis.labelExpr`: `vl-expr-label`. A syntax error, an over-long or over-deep expression, or a reference to an undeclared parameter is a `VegaLiteSpecException` when the document is opened |
| Transforms | `calculate`, `filter` (an expression or the predicates `equal`, `lt`, `lte`, `gt`, `gte`, `range`, `oneOf`, and `valid`, composed with `and`, `or`, and `not`, and `param` of a variable parameter), `fold`, `flatten`, `sample`, `pivot` (`sum` and `count`), `lookup` (inline, named, or local-file data, by `from.key`, `fields`, `as`, and `default`), `aggregate` and `joinaggregate` (every operation: `count`, `valid`, `missing`, `distinct`, `sum`, `product`, `mean`, `average`, `variance`, `variancep`, `stdev`, `stdevp`, `stderr`, `median`, `q1`, `q3`, `ci0`, `ci1`, `min`, `max`, `argmin`, `argmax`, `values`, `exponential`, and `exponentialb`), `window` (`row_number`, `rank`, `dense_rank`, `percent_rank`, `cume_dist`, `ntile`, `lag`, `lead`, `first_value`, `last_value`, `nth_value`, `prev_value`, `next_value`, and every aggregate operation over a `frame`, with `sort`, `groupby`, and `ignorePeers`), `bin` (the Vega bin algorithm with its parameters), `stack` (`zero`, `center`, `normalize`), and `impute` (`value`, `mean`, `median`, `max`, `min`, with `keyvals` and `frame`), of the drawn view, in the written order | `timeUnit`, `regression`, `loess`, `density`, `quantile`, `extent`: `vl-transform-<name>` (the transform is skipped); every transform of a nested view of a composition: `vl-transform-<name>`; parts of a supported transform: `vl-transform-filter-param` (a selection predicate passes every row), `vl-transform-filter-timeunit` (ignored), `vl-transform-filter-datetime` (the predicate is ignored), `vl-transform-pivot-op` (sum is used), `vl-transform-lookup-param` (not applied), `vl-transform-bin-extent-param` (the extent of the data is used); recoverable findings at render time: warnings `vl-expr-runtime` and `vl-transform-runtime`. An operation name that does not exist and a malformed aggregating transform are a `VegaLiteSpecException` |
| Parameters | Variable parameters of the root view with a `value` or an `expr` (the expression wins), evaluated once per call in dependency order, overridden by `VegaLiteRenderOptions.Parameters`, read by expressions by name, and by a filter `{"param": name}`; `bind` and `empty` are ignored | Selection parameters and the legacy `selection`: `vl-param-selection`; a variable parameter of a nested view: `vl-param-variable`. A parameter cycle, a repeated or reserved name, and a reference to an undeclared parameter are a `VegaLiteSpecException` |
| Time | None; the `TimeZone` option is accepted and reserved (UTC by default) | `vl-type-temporal`, `vl-encoding-timeunit`, `vl-transform-timeunit` |
| Fonts | The bundled Liberation Sans, Serif, and Mono for generic and Arial-like families; named families through `VegaLiteFontOptions` (`UseSystemFonts` is `false` by default) | N/A (an unresolvable family falls back to the bundled sans font) |
<!-- markdownlint-enable MD013 -->

The **stable report identifiers** are part of the public contract of the package and are listed in the XML
documentation of the internal `FeatureCatalog` type. Identifiers are lower case, of the form `vl-<area>-<detail>`; a
name taken from the specification is lower-cased without separators (`timeUnit` gives `timeunit`), and so is the name
of an expression function (`timeFormat` gives `vl-expr-function-timeformat`; the message keeps the name as written):

<!-- markdownlint-disable MD013 -->
| Identifier | Kind | Reported at, and when |
| ---------- | ---- | --------------------- |
| `vl-schema-version` | Warning | `$schema`, when the major version is not 5 or 6 or the URL names no Vega-Lite version |
| `vl-data-empty` | Warning | The `data` object, when it has an empty `values` array or no source key |
| `vl-data-remote-url` | Unsupported | The `data` object, when `url` has a scheme (including `file:`), a drive letter, or is an absolute path (`/etc/hosts`, a leading backslash), or a network or UNC path; a relative local path is supported and not reported |
| `vl-data-parse-date-format` | Unsupported | The `format.parse` entry that names a date with a custom format (`date:'%d/%m/%Y'`, `utc:'...'`) |
| `vl-data-parse-failed` | Warning | The `data` object, at render time only (it needs the data): one per field and type, when an explicit `parse` could not convert some values; those values are null |
| `vl-data-values-text` | Unsupported | `data/values` given as a string, or a top-level dataset that `data.name` refers to given as a string |
| `vl-data-geo` | Unsupported | `sphere`, `graticule`, a topojson format, or `values` (or a referenced dataset) given as an object |
| `vl-mark-<type>` | Unsupported | `mark`, for every type except bar, rect, point, circle, square, rule, and tick |
| `vl-channel-<name>` | Unsupported | A channel other than x, y, color, fill, stroke, size, opacity, shape, strokeWidth, strokeDash, fillOpacity, strokeOpacity, and angle (tooltip, href, description, and key are ignored); `shape` is drawn by a point mark, `angle` by a point, circle, or square, and `strokeDash` by every mark but a rect, and the channel is reported and ignored on any other mark; `x2` and `y2` are accepted for a bar, rect, or rule with the channel they extend and report `vl-channel-x2` and `vl-channel-y2` otherwise; `order` is accepted when it orders a stack and reports `vl-channel-order` otherwise |
| `vl-channel-geo` | Unsupported | `longitude`, `latitude`, `longitude2`, `latitude2` |
| `vl-type-temporal`, `vl-type-geojson` | Unsupported | The `type` of a channel |
| `vl-shape-path` | Unsupported | A shape that is not a symbol name (a custom SVG path): the `shape` of a mark, the `value` of a `shape` channel, or an entry of its `scale.range`; a circle is drawn |
| `vl-scale-interpolate-cubehelix` | Unsupported | A `scale.interpolate` of `cubehelix` or `cubehelix-long`; RGB interpolation is used |
| `vl-scale-type-ignored` | Warning | A `scale.type` that the channel or the field type cannot use (the default type is used), and a quantitative field on a `shape` or `strokeDash` channel without a discretizing scale type (the scale is ordinal) |
| `vl-encoding-timeunit`, `-impute`, `-bandposition` | Unsupported | That property of a supported channel (the `impute` of a channel needs the grouping fields of a path mark; the `impute` transform is supported) |
| `vl-encoding-condition` | Unsupported | A `condition` on a channel that takes none (`x`, `y`, the secondary channels, `order`); the condition is ignored |
| `vl-encoding-condition-param` | Unsupported | A condition on a `param`, or a `test` that names a selection or a parameter that is not a variable; the condition never holds |
| `vl-encoding-condition-test` | Unsupported | A `timeUnit` or a date object in the predicate of a `test`; the condition never holds |
| `vl-encoding-bin` | Unsupported | The `bin` of a channel that cannot bin (`order` and the secondary channels), and `bin: "binned"` on a channel other than `x` and `y`, unless false or null; the channel is not binned |
| `vl-encoding-bin-extent-param`, `vl-transform-bin-extent-param` | Unsupported | A `bin` whose `extent` is a `{"param": ...}` object, in an encoding or in a transform; the extent of the data is used |
| `vl-encoding-stack-nonlinear` | Unsupported | An explicit `stack` of `x` or `y` whose `scale.type` is not linear; the marks of a log, pow, sqrt, or symlog scale are not stacked (Vega-Lite does not stack on them), and any other type is stacked on the linear scale |
| `vl-encoding-sort` | Unsupported | A `sort` that is not applied: a channel shorthand or an `encoding` that names a channel without a field or an aggregate (the message names the channel), or an `op` of `values` or an exponential operation |
| `vl-encoding-sort-ignored` | Warning | A `sort` array, channel shorthand, or object with a `field`, `op`, or `encoding` on a quantitative field with a continuous scale (there are no categories to order) |
| `vl-encoding-sort-mixed-types` | Warning | A `sort` array that mixes strings, numbers, booleans, or null (produced by the parser at the array; values are matched by kind) |
| `vl-scale-<type>` | Unsupported | `scale/type`, for every type except linear, log, pow, sqrt, symlog, sequential, band, point, ordinal, quantile, quantize, threshold, and bin-ordinal |
| `vl-scale-property-<name>` | Unsupported | A scale property outside the supported list (`domainMid`, `domainMin`, `domainMax`, `rangeMin`, `rangeMax`, `interpolate`, `base`, `exponent`, and `constant` are supported; `bins` and `domainRaw` are not; an unknown color scheme name is an error) |
| `vl-guide-encode` | Unsupported | `axis.encode`, `legend.encode` |
| `vl-guide-legend-titleorient`, `vl-guide-legend-clipheight` | Unsupported | A `legend.titleOrient` other than `top` (the title stays above the entries), a `legend.clipHeight` (the entries are not clipped) |
| `vl-guide-axis-<name>` | Unsupported | `labelBound`, `tickBand`, `tickExtra`, `position`, `translate`, `bandPosition`, `titleX`, `titleY` of an axis |
| `vl-format-type-custom` | Unsupported | `axis.formatType`, `legend.formatType` |
| `vl-expr-ref`, `vl-expr-label` | Unsupported | An `{"expr": ...}` value that the renderer does not evaluate (in a title, size, padding, background, view, a mark property other than the row-bound paint properties, or an encoding other than the `value` of a color, fill, stroke, opacity, or symbol size channel), and `axis.labelExpr`, `legend.labelExpr` |
| `vl-expr-function-<name>` | Unsupported | The expression text of a supported site, for a call of a function outside the table of 57 (the name lower-cased with the invariant culture, the message keeps it as written, for example `vl-expr-function-timeformat` for `timeFormat`) |
| `vl-expr-view-state` | Unsupported | The expression text of a supported site, for `signal`, `event`, `item`, or a view signal (`width`, `height`, `padding`, `autosize`, `background`, `cursor`) that no parameter declares |
| `vl-expr-construct-<name>` | Unsupported | The expression text of a supported site, for a deferred operator: `bitwise-and`, `bitwise-or`, `bitwise-xor`, `bitwise-not`, `shift-left`, `shift-right`, `shift-right-unsigned`, or `in` |
| `vl-expr-runtime` | Warning | A `calculate` expression, at render time, when it produced a regular expression, which cannot be stored in a field (null is stored; one report per transform with the count and the first row) |
| `vl-transform-<name>` | Unsupported | Every entry of `transform` that is not computed: `timeunit`, `regression`, `loess`, `density`, `quantile`, `extent`, and every transform of a nested view of a composition (aggregating transforms included) |
| `vl-transform-filter-param`, `-filter-timeunit`, `-filter-datetime`, `-pivot-op`, `-lookup-param`, `-bin-extent-param` | Unsupported | The part of a computed transform that is not supported: a filter on a selection (every row passes), a `timeUnit` in a predicate (ignored), a date object in a predicate (the predicate is ignored), a pivot `op` other than sum and count (sum is used), a lookup in the data of a parameter (not applied), and a bin extent that is a parameter (the extent of the data is used) |
| `vl-transform-runtime` | Warning | A pivot, at render time: rows with a null pivot value skipped, columns cut at `limit`, or a pivot value that replaces a group field |
| `vl-composition-layer`, `-concat`, `-hconcat`, `-vconcat`, `-facet`, `-repeat` | Unsupported | That property; the nested views are examined too |
| `vl-param-variable`, `vl-param-selection` | Unsupported | Every selection parameter and the legacy top-level `selection` (`vl-param-selection`); a variable parameter of a nested view (`vl-param-variable`); the variable parameters of the root view are computed and not reported |
| `vl-projection` | Unsupported | `projection` |
| `vl-layout-autosize-<type>` | Unsupported | `autosize` types other than pad and none |
| `vl-duplicate-key` | Warning | The later of two equal keys of one object (the last value wins) |
| `vl-unknown-property` | Warning | A key that Vega-Lite does not define for a view, mark definition, encoding, or channel definition |
| `vl-diagnostics-suppressed` | Warning | The first report beyond `MaxDiagnostics` |
<!-- markdownlint-enable MD013 -->

**Retired identifiers.** The identifiers `vl-data-file`, `vl-data-named`, `vl-data-sequence`, `vl-data-parse`,
`vl-data-format-csv`, `vl-data-format-tsv`, `vl-data-format-dsv`, and `vl-data-property` were reported before phase 4;
the constructs they named are now supported, so they are no longer produced. The identifiers `vl-expr-calculate` and
`vl-expr-filter` were reported before phase 5 for a calculate expression and a string filter, and `vl-transform-calculate`,
`-filter`, `-fold`, `-flatten`, `-sample`, `-pivot`, and `-lookup` for the transforms of the drawn view; those constructs
are now supported, so these reports are no longer produced for the drawn view (the transforms of a nested view are still
reported under their `vl-transform-<name>`). Likewise, `vl-encoding-aggregate` and `vl-encoding-stack` (and the report
of an
implicit bar stack) were reported before phase 6, `vl-transform-aggregate`, `-joinaggregate`, `-window`, `-bin`, `-stack`,
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
`vl-encoding-condition` for a channel that takes no condition).
They are never reused for another meaning.

**Deferred phases.** The roadmap that follows the delivered phases adds, each in its own pull request with the full
companion artifacts, the following capabilities; until then the constructs are reported with the identifiers above:

- **Data formats still deferred**: custom `parse` time formats (`date:'%fmt'`, with the time phase), data given as text
  in `values`, and the geographic formats; remote URLs are never fetched and stay reported
- **Expressions and row transforms (delivered in phase 5, with these parts deferred)**: the date, time, format, color,
  geographic, scale, statistical, and data-flow functions of the expression language, view state (`signal`, `event`,
  `item`), and the bitwise operators; expressions in titles, sizes, paddings,
  backgrounds, and `axis.labelExpr`; and the other transforms below
- **Aggregation (delivered in phase 6, with these parts deferred)**: a bin extent given by a parameter, the encoding
  `impute` of a channel
  (it needs the path marks of a later phase), `argmin` and `argmax` sorts, the `order`
  channel as the drawing order of marks, `timeUnit` in a bin or an aggregate, and the `extent` transform
- **Mark-property channels, legends, and scales (delivered in phase 7, with these parts deferred)**: the `identity`,
  `time`, and `utc` scale types (`time` and `utc` come with the time phase), the scale properties `bins` and `domainRaw`,
  `cubehelix` interpolation, a custom SVG path as a `shape`, `legend.encode`, `legend.labelExpr`, `legend.formatType`, a
  `titleOrient` other than `top`, `clipHeight`, and conditions on a `param` (with the selections)
- **Time**: calendar and time-zone handling, time and UTC scales, `timeUnit`, `datetime`, and time formatting (the
  `TimeZone` option, UTC by default)
- **Line-like and other marks**: `line`, `area`, `trail`, `text`, `arc`, and curve interpolation
- **Layers and composite marks**: `layer` with `resolve`, `boxplot`, `errorbar`, `errorband`
- **Composition**: `concat`, `hconcat`, `vconcat`, `facet`, `repeat`, `autosize` of `fit`, and `"container"` sizes in
  compositions
- **Parameters**: selection parameters and their default state, the interaction state of a variable parameter (`bind`,
  `empty`), and the remaining uses of a parameter in expression sites that are not evaluated yet (the static variable
  parameters and the caller's `Parameters` overrides are delivered)
- **Advanced and hardening**: `regression`, `loess`, `density`, `quantile`, label-overlap variants, a local `image`
  mark, the remaining configuration keys, and a fuzzing, determinism, and performance audit

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
   channel are dropped (the Vega-Lite 6 `filter` behavior for non-path marks); values outside an explicit discrete
   domain are dropped.
5. **Domain order**: discrete domains are ascending by Vega-Lite's default sort, with mixed value kinds ordered null,
   boolean, number, string (a total order), and values missing from a `sort` array first.
6. **Clip**: marks are clipped only with `"clip": true`, as in Vega-Lite 6, never otherwise to the view; an explicit
   `x` or `y` scale domain does not clip (Vega-Lite's `scaleClip` is for the extent of an interval selection, which is
   not supported).
7. **Title lines**: a line feed inside title text splits the line, which Vega does not do.
8. **Painting**: clips are composited with fractional coverage; text is filled only; an unknown symbol shape is drawn
   as a circle; fills are flattened at a tolerance of 0.02 pixel so that small circles stay round.
9. **Number formatting**: the d3 `en-US` locale with the minus sign U+2212, culture-invariant; widths above 1,000 are
   clamped as a denial-of-service guard.
10. **Data format by extension**: without `format.type` a `url` ending in `.csv` or `.tsv` (any case) is delimited text
    and everything else is JSON, as in Vega-Lite; `format.type` always wins. `format.header` and `format.property` on
    delimited data, and `format.delimiter` on CSV and TSV, are ignored, and a DSV without a delimiter uses a comma.
11. **Inference only on request**: as in Vega, no data-based type inference is applied unless `parse` is `"auto"`: delimited
    text without `parse` (or with `parse: null`) keeps every cell as a string (a leading zero or an ISO-looking date
    stays text), and a `parse` object converts only the fields it names. With `"auto"` a column is converted by the type
    of its whole column (boolean, then number, then ISO 8601 date, only when every non-empty text of the column fits;
    otherwise the column stays text), never per cell. Numbers are finite invariant integers, decimals, and exponents (no
    hex, no thousands separators, no `NaN` or infinity); a date without a zone is UTC, so the result never depends on
    the host. Quantitative encodings still coerce numeric text to numbers when they are drawn (as Vega does for its
    linear scales); quantitative color is drawn with the continuous color scales of phase 7, and temporal parsing of
    text arrives with the time
    support of Phase 8. Custom `date:'%fmt'` parsing is not implemented (`vl-data-parse-date-format`).
12. **Failed conversions**: a value that an explicit `parse` cannot convert becomes null, with a
    `vl-data-parse-failed` warning for the field, instead of NaN or an invalid date; an empty text becomes null.
13. **Local files are hard errors**: a data file that cannot be used fails the render with a located
    `VegaLiteRenderException` in lenient mode too, because an empty chart would hide a broken deployment; only remote
    data, which is never fetched, is reported and empty. This includes a directory where a file is expected, an existing
    file that cannot be opened (never reported as missing: only the open attempt decides that), JSON data text that is
    not valid Unicode, and a callback or callback stream that throws, which are located failures ("cannot be read", or a
    located data failure) rather than a raw exception.
    Caller dataset dates are UTC; `DateTime.MaxValue` and `DateTimeOffset.MaxValue` (the usual "no end" sentinels) are
    clamped to the last supported millisecond of the year 9999, and ISO text beyond the supported date range is not a date.
14. **Links**: a symbolic link or junction below a root is refused (a link target is never followed), whatever it
    points to; a link at or above the root, which the caller chose, is trusted, and other reparse points that redirect
    nothing (for example the placeholders of a synchronized folder) are not links in this sense.
15. **Invalid sequences fail the render**: a `sequence` without a finite numeric `start` and `stop`, or with a step that
    is zero, not finite, or not a number, fails the render with a located `VegaLiteRenderException` instead of being
    defaulted (only an absent step is 1); a step that points away from the stop is an empty table, and a length that
    overflows fails the row limit before anything is allocated. The specification metadata reports an unknown length for
    these.
16. **Provenance of the data rules**: the format, parse, and inference behavior follows the public Vega and Vega-Lite
    documentation of data loading; the parsers are original code and no upstream source, dataset, or example was copied.
17. **A closed expression subset**: the language is a subset of JavaScript run by an interpreter written for this package,
    with 57 whitelisted functions and no `random`, no date, time, format, or color function, no `signal`, `event`, or
    `item`, no bitwise operators, no assignment, and no method calls; the parts that parse but do not run evaluate to null
    and are reported. The value model has one null for `null` and `undefined`, so null converts to 0 in arithmetic.
    The math functions use the host math library, so a transcendental result can differ from a browser in the last bit.
18. **Compiled when the document is opened**: an expression with a syntax error, a length over `MaxExpressionLength`,
    nesting over `MaxExpressionDepth`, or a reference to an undeclared parameter is a `VegaLiteSpecException` at open time;
    Vega-Lite itself would fail when it builds the dataflow. `MaxExpressionDepth` counts nesting, not length.
19. **Parameters are static**: a variable parameter is evaluated once; the expression wins over the `value`; a caller
    `Parameters` entry replaces the whole definition (its expression is not evaluated); `bind` and `empty` are ignored;
    a selection parameter reads as null in an expression and passes every row of a filter.
20. **Lookup takes the first match**: the secondary rows are indexed by the text of the key and the first row of a repeated
    key wins, so the result does not depend on the index implementation; this differs from an
    index that keeps the last row.
21. **Deterministic sample**: `sample` is seeded from the JSON pointer of the transform (FNV-1a, SplitMix64, reservoir
    sampling, rows kept in source order), not from a random source, so a chart renders the same on every call; the rows
    differ from the ones a browser would pick.
22. **Pivot is limited to sum and count**, with columns in ordinal order and a missing combination 0; another `op` is
    reported and sum is used.
23. **Sort by field supports every orderable aggregate**: any aggregate operation except `argmin`, `argmax`, `values`, and
    the exponential ones, by field, by `op`, by channel shorthand (`"-y"`), or by `{"encoding"}`, over the **Raw** rows
    (before the implicit aggregate, as in Vega-Lite), stable for ties; a category without a valid value (a null result,
    also for `sum`, as in Vega) sorts first in either direction. Any other form is reported as
    `vl-encoding-sort`. A sort array is bounded by `MaxScaleDomainSize` and an object or array item in it is a
    specification error; a sort array that mixes value kinds warns (`vl-encoding-sort-mixed-types`) and a sort of
    categories on a continuous scale warns and is ignored (`vl-encoding-sort-ignored`).
24. **Warnings, not silent nulls**: a calculate that produces a regular expression stores null and warns
    (`vl-expr-runtime`); a pivot that skips or cuts data warns (`vl-transform-runtime`).
25. **Provenance of the expression and transform rules**: the semantics follow the public Vega
    expression documentation, the
    Vega-Lite transform documentation, and ECMAScript; the engine and the transforms are original code, no upstream source,
    test, or example was copied, and the sampler is built from the published FNV-1a and SplitMix64 descriptions (see
    `third-party/README.md`).
26. **Deterministic bootstrap**: `ci0` and `ci1` are the 2.5% and 97.5% quantiles of 1,000 bootstrap means drawn with
    SplitMix64, seeded from the FNV-1a hash of the JSON pointer of the transform and the group (Vega draws with
    `Math.random`); the two bounds of one field share their resamples. The result is identical on every call.
27. **Structural group keys**: the groups of `aggregate`, `joinaggregate`, `window`, `stack`, and `impute` (and of the
    implicit aggregate) are keyed by the structure of the values (a number and a text with the same spelling are different
    groups; an array or an object compares by its content) and listed in order of first appearance, where Vega keys by the
    joined text of the values and lists in the order of a JavaScript object.
28. **Defaults of the aggregating transforms**: the `stack` transform writes `field_start` and `field_end` when it has no
    `as` (Vega: `y0` and `y1`); the `exponential` operations use a weight of 0.5 without a `param`; an empty window frame
    gives null for `first_value`, `last_value`, and `nth_value` (Vega reads past the partition); `cume_dist` and `ntile`
    count the peers of the first rows of a partition (Vega under-reports them); the growth of the bin step is capped
    (Vega can loop for ever) and a bin count above `MaxScaleDomainSize` fails.
29. **A bar stacks by default**: as in Vega-Lite 6, rows of a bar chart that share a category are stacked from zero, not
    overdrawn. A chart with one row per category is drawn exactly as before; because such a bar starts at 0 even when an
    explicit domain excludes zero, it overflows the data rectangle as in Vega-Lite (there is no scale clip; see 6).
30. **The scope of the encoding pipeline**: `bin: "binned"` is read on `x` and `y` only (the other channels that have a
    scale bin
    from the data); `x2` and `y2` only for `bar`, `rect`,
    and `rule`; `order` only as the order of a stack; `scale.domain: "unaggregated"` only for `mean`, `median`, `q1`, `q3`,
    `min`, and `max`; a datum `x2` or `y2` is mapped through the primary scale
    and is not part of the domain; the title of a
    count is `config.countTitle` in every `fieldTitle` mode and the name of an aggregate keeps the operation as written
    (`average_x`); the axis of a normalized stack uses `config.normalizedNumberFormat` (`.0%` by default); the default tick
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
    and `strokeOpacity` are drawn but get no legend; `legend.encode`, `labelExpr`, a custom `formatType`, a
    `titleOrient` other than `top`, and `clipHeight` are reported and not drawn; legends make the chart larger (a
    legend on the right adds its width, plus the offset of 18 pixels, to the width of the picture) except with
    `autosize: none`, where they overflow, as in Vega-Lite. The entries are charged to the scene-item limit before
    they are created.
34. **Scale types follow Vega-Lite's tables**: an explicit `scale.type` that the channel or the field type cannot use is
    replaced by the default type with the warning `vl-scale-type-ignored`, as Vega-Lite does; the bin-ordinal scale is
    accepted for a binned quantitative color only; a color scale with an explicit domain of four or more entries (a
    piecewise color scale) is a specification error; a `scale.domain` that is not an array is ignored.

### Determinism And Immutability

Every public result type is immutable. A `VegaLiteDocument` and its internal specification tree can be shared between
threads, and parsing the same text any number of times, on any thread, in any culture, yields equal trees and
identical errors. `Measure` and `Render` hold no state between calls, so a document may be measured and rendered from
several threads at once, and each call gives the same result for the same options: a render is **byte-identical** for
every degree of parallelism and on every platform, because the only fonts used are the bundled fonts unless the options
say otherwise, the time zone defaults to UTC, no wall clock or environment variable is consulted, and the floating-
point arithmetic is specified by the ported algorithms. `Measure` always equals the size of the surface `Render`
produces, because both run the same pure layout.

## Implementation Phase Plan

`CanvasNetVegaLite` is delivered incrementally; each phase adds requirements, design, verification, and tests for its
own scope, and everything not yet implemented is reported with a stable identifier.

1. **Phase 1 - Foundation and document shell (delivered).** The project and test project, the public API contract
   (`VegaLiteDocument` and the options, limits, location, diagnostic, size, data reference, and exception types), the
   `SpecModel` subsystem (`SpecParser`, `FeatureCatalog`, `ConfigResolver`), the `Guides` units `TextMeasurer` and
   `TitleBuilder`, the `Layout` unit `ViewLayout`, the `Scenegraph` subsystem (`SceneModel`, `ScenePainter`,
   `CssColor`), the attribution scaffolding under `third-party/`, original fixtures with a provenance README, and the
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
   `ReleaseRows`) and the loading of lookup sources, the sort by the aggregate of a field in `ScaleResolver`, the row-bound
   paint expressions of `MarkBuilder`, the static variable parameters and the `Parameters` override, the active expression
   limits, the new `vl-expr-*` and `vl-transform-*` identifiers, and the original fixtures.
6. **Phase 6 - Aggregation (delivered).** The `Transforms` units `AggregateTransforms` (the
   `aggregate`, `joinaggregate`, `window`, `bin`, `stack`, and `impute` transforms, every aggregate operation with the
   deterministic bootstrap, the Vega bin algorithm, and the stack rules) and `EncodingPlan` (the implicit bin, aggregate,
   and
   stack of an encoding and its field names and titles), the charging of filter predicates to the step budget, the
   validation of the aggregation properties in `SpecParser`, the plan-driven domains, the Raw and Main tables, and the sort
   by
   any aggregate in `ScaleResolver` (split into three files), the stacked, ranged, and binned geometry and the clipping
   rule
   of `MarkBuilder`, the derived titles, binned tick counts, and normalized format of `AxisBuilder`, the secondary channels
   `x2` and `y2`, the changed and new `vl-encoding-*`, `vl-channel-*`, and `vl-transform-*` identifiers, the attribution
   record, and the original fixtures.
7. **Phase 7 - Legends, color schemes, and mark-property channels (delivered with this pull request).** The `Scales`
   units `ColorInterpolator` (the color spaces and ramps) and the rewritten `ColorSchemes` (the 68 Vega schemes, with
   their notices), the scale kinds sequential, diverging, quantize, quantile, threshold, bin-ordinal, log, pow, sqrt, and
   symlog in `ScaleModel` and `ScaleResolver` (split into partial files for colors and ranges), log ticks and labels in
   `Ticks` and `NumberFormat`, the `Guides` units `LabelOverlap` (extracted from `AxisBuilder`) and `LegendBuilder`
   (planning, symbol, gradient, and discrete gradient legends), the `Layout` unit `LegendLayout` (placement by orient and
   the legend extents of the view layout), the `SceneGradient` of the scene model and the gradient fill of the painter,
   the `shape`, `strokeWidth`, `strokeDash`, `fillOpacity`, `strokeOpacity`, and `angle` channels and the conditional
   encodings of `MarkBuilder`, `bin` on the channels that have a scale in `EncodingPlan`, the validation of the scale and
   legend properties in `SpecParser`, the retired and new `vl-*` identifiers, the notices of the color sources
   (ColorBrewer, Turbo, cividis, the matplotlib colormaps), and the original fixtures.
8. **Phase 8 - Time (planned).**
9. **Phase 9 - Line-like and other marks (planned).**
10. **Phase 10 - Layers and composite marks (planned).**
11. **Phase 11 - Composition (planned).** The recommended release gate of the package.
12. **Phase 12 - Parameters and default state (planned).** Selection parameters (the static variable parameters are
    delivered in phase 5).
13. **Phase 13 - Advanced features and polish (planned).**
14. **Phase 14 - Hardening (planned).**

Every later phase adds members and types rather than changing the signatures delivered here.
