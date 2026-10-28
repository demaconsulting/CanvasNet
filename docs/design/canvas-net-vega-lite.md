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
`autosize` of `pad` or `none`. Compositions, transforms, expressions, parameters, legends, time, and local data files
are later phases.

`CanvasNetVegaLite` consists of one direct unit and seven internal subsystems:

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
  `SpecNode` tree with RFC 6901 pointers and one-based line and column, validates the shape of the known properties,
  and warns about duplicate and unknown keys. `FeatureCatalog` lists every recognized-but-unsupported construct with a
  stable identifier. `ConfigResolver` merges the Vega and Vega-Lite default configuration, the configuration of the
  specification, and the caller's configuration. See _SpecModel Subsystem Design_
  (`canvas-net-vega-lite/spec-model.md`).
- **Data** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Data`, folder `Data/`): rows into
  memory, with two units. `DataTable` is the immutable typed value and table model. `DataLoader` reads inline
  `values` under the data limits. See _Data Subsystem Design_ (`canvas-net-vega-lite/data.md`).
- **Scales** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Scales`, folder `Scales/`): the numeric
  heart, with five units. `ScaleModel` maps values to pixels (linear, band, point, ordinal). `ScaleResolver` infers
  scale types, domains, ranges, and paddings from the specification. `Ticks` is the d3-array tick algorithm.
  `NumberFormat` is the d3-format number formatter. `ColorSchemes` holds the `tableau10` category palette. See
  _Scales Subsystem Design_ (`canvas-net-vega-lite/scales.md`).
- **Guides** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Guides`, folder `Guides/`): text and
  guide construction, with three units. `TextMeasurer` resolves fonts and measures and truncates text. `TitleBuilder`
  builds the title and subtitle. `AxisBuilder` builds axes (grid, ticks, labels, domain, title) and their extents. See
  _Guides Subsystem Design_ (`canvas-net-vega-lite/guides.md`).
- **Layout** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Layout`, folder `Layout/`): the
  `ViewLayout` unit, which computes the data rectangle, the padding, the autosize canvas, the title placement, and the
  view background and border of a single view. See _Layout Subsystem Design_ (`canvas-net-vega-lite/layout.md`).
- **Marks** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Marks`, folder `Marks/`): the
  `MarkBuilder` unit, which turns rows, scales, and mark properties into scene items. See _Marks Subsystem Design_
  (`canvas-net-vega-lite/marks.md`).
- **Scenegraph** (internal subsystem; namespace `DemaConsulting.CanvasNet.VegaLite.Scenegraph`, folder
  `Scenegraph/`): the assertion surface and the painter, with three units. `SceneModel` is the immutable scene
  (groups, rectangles, rules, paths, symbols, text, bounds, and the item budget). `ScenePainter` draws a scene onto a
  `Surface`. `CssColor` parses CSS colors. See _Scenegraph Subsystem Design_
  (`canvas-net-vega-lite/scenegraph.md`).

The names of the subsystems in this document are the namespace and folder names; in the SysML2 model their `part def`
names carry the prefix `VegaLite` (`VegaLiteSpecModel`, `VegaLiteData`, `VegaLiteScales`, `VegaLiteGuides`,
`VegaLiteLayout`, `VegaLiteMarks`, and `VegaLiteScenegraph`), and the `SceneModel` unit is `VegaLiteSceneModel`,
because the `CanvasNet` model package is shared by every system of the repository and `SceneModel` and `SceneGraph`
already name items of `CanvasNetRender3D`.

Dependencies inside the system are one-directional at the unit level. `VegaLiteDocument` calls every subsystem.
`SpecParser` has no dependency on another unit; `FeatureCatalog` and `ConfigResolver` read its `SpecTree` and
`SpecNode` types. `DataLoader` reads `SpecNode` and builds `DataTable`. `ScaleResolver` reads the `SpecNode` tree, the
`DataTable`, and the `ResolvedConfig`, and builds `ScaleModel` instances, using `Ticks` and `ColorSchemes`;
`NumberFormat` and `Ticks` depend on nothing else in the system. `SceneModel` depends on nothing else.
`TextMeasurer` reads the `SceneModel` text and bounds types; `ScenePainter` calls `TextMeasurer` and `CssColor`.
`TitleBuilder` and `AxisBuilder` build `SceneModel` items using `TextMeasurer`, and `AxisBuilder` also uses
`ScaleModel`, `Ticks`, and `NumberFormat`. `ViewLayout` builds the root of the scene around the title block and the
content that the builders produced. `MarkBuilder` builds `SceneModel` items from the `DataTable` and the scales. The
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
  `SourceName`, and `Limits`. Parsing consumes `SourceName` and `Limits`; the other three define the file sandbox of
  the later data-file phase and are stored but not yet consumed (local files report `vl-data-file`).
- **`VegaLiteRenderOptions`** (sealed, init-only, validated): `Scale` (finite, greater than zero, at most
  `Surface.MaxDimension`), `Background` (an `Rgba32` that replaces the specification and configuration background),
  `Config` (a Vega-Lite configuration as JSON object text, merged over the defaults and the specification's
  `config`), `Parameters` and `Datasets` (copied and validated; reserved for the parameter and data phases),
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
| `VegaLiteDocument` properties | Outbound | Text, numbers, read-only lists | Describe the text only: no data is loaded, no file is opened, nothing is evaluated |
| `VegaLiteDocument.Measure(...)` | Inbound and outbound | `VegaLiteRenderOptions`, a `CancellationToken`; a `VegaLiteSize` | Same checks, reports, and order as `Render`, nothing is drawn; always equals the size `Render` produces |
| `VegaLiteDocument.Render(...)` (natural size) | Inbound and outbound | `VegaLiteRenderOptions`, a `CancellationToken`; a `Surface` | Natural size times `Scale`, rounded half away from zero, each side from 1 to `Surface.MaxDimension`, validated before any report is delivered; caller owns the surface; byte-identical output for identical input |
| `VegaLiteDocument.Render(int, int, ...)` (exact size) | Inbound and outbound | Width, height, options, a token; a `Surface` | Width and height from 1 to `Surface.MaxDimension`, validated first; the natural chart is scaled uniformly to fit and centered, margins take the background; `Scale` is not used |
| `VegaLiteRenderOptions.WarningHandler`, `UnsupportedFeatureHandler` | Outbound | Delegates receiving `VegaLiteDiagnostic` and `VegaLiteUnsupportedFeature` | Called once per distinct report, in document order, on the calling thread, after the work finished and before the method returns or throws |
| `VegaLiteSpecException` | Outbound | Exception with `Location` | Thrown for malformed JSON, nesting deeper than `MaxJsonDepth`, a known property of the wrong type or value, a string longer than `MaxStringLength`, plain Vega, and, at render time, an invalid axis orientation, axis format, or scale domain or range |
| `VegaLiteRenderException` | Outbound | Exception with `Location` | Thrown when a data, scale, axis, or scene limit is exceeded (`MaxDataRows`, `MaxFieldCount`, `MaxScaleDomainSize`, `MaxTicks`, `MaxSceneItems`) |
| `VegaLiteUnsupportedFeatureException` | Outbound | Exception with `Feature` and `Location` | Thrown only when `ThrowOnUnsupportedFeature` is set, at the first unsupported feature in document order; the report is delivered to the handler first |
| `ArgumentOutOfRangeException`, `ArgumentException` | Outbound | Exception | Thrown for an invalid option value when it is set, for a scaled or explicit size outside 1 to `Surface.MaxDimension`, and for a `Parameters` name the document does not declare |
| `OperationCanceledException` | Outbound | Exception | Thrown, never wrapped, when the token is canceled before or during the work |
| `InvalidDataException` | Outbound | Exception | Thrown, not as a `VegaLiteSpecException`, when the source exceeds `MaxSourceLength` (no location is meaningful) |
<!-- markdownlint-enable MD013 -->

Opening a document never loads data, never opens another file, and never uses the network. The types of the
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
`System.Text.RegularExpressions` (a generated regular expression for the d3 format specifier),
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
  rendering would be a guess; an unknown property is only a `vl-unknown-property` warning, because it may come from a
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
  | `MaxStringLength` | 1,048,576 characters | `SpecParser` | `VegaLiteSpecException` |
  | `MaxDataRows` | 1,000,000 (total) | `DataTable`, `DataLoader` | `VegaLiteRenderException` before any row is converted |
  | `MaxFieldCount` | 1,024 | `DataTable`, `DataLoader` | `VegaLiteRenderException` |
  | `MaxScaleDomainSize` | 100,000 values | `ScaleResolver` | `VegaLiteRenderException` |
  | `MaxTicks` | 1,000 per axis | `Ticks`, `AxisBuilder` | `VegaLiteRenderException` (also for categories) |
  | `MaxSceneItems` | 2,000,000 | `MarkBuilder`, `SceneBudget`, `VegaLiteDocument` | `VegaLiteRenderException`, counted in 1,024-row batches before memory is spent |
  | `MaxDiagnostics` | 1,000 | `VegaLiteReportSink` | Further reports collapse into one `vl-diagnostics-suppressed` notice; strict mode is unaffected |
  | `MaxDataFiles`, `MaxDataBytes`, `MaxViews`, `MaxCompositionDepth`, `MaxExpressionLength`, `MaxExpressionDepth`, `MaxEvaluationSteps` | 64, 64 MiB, 1,000, 16 (1 to 64), 4,096, 64 (1 to 256), 50,000,000 | Reserved for the data-file, composition, and expression phases | Not yet enforced: the corresponding constructs are reported as unsupported |
  <!-- markdownlint-enable MD013 -->

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
- **No file or network access.** Parsing and rendering read only the text they are given. A data `url` is never
  opened or fetched: a URL with a scheme, a drive letter, or a network path reports `vl-data-remote-url`, and any other
  `url` reports `vl-data-file`; both use an empty dataset. No code in the specification is ever executed: expressions,
  transforms, and parameters are reported, not evaluated.
- **Cancellation.** A `CancellationToken` is honored before any work, in the data and scale stages, per tick of an
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
   Vega-Lite 6 schema, and records `vl-unknown-property` warnings.
4. **Describe**: `SpecMetadata` reads the schema URL and major version, the title, the mark types, the data references,
   and the parameter names from the tree; `FeatureCatalog.Visit` lists the recognized-but-unsupported constructs. The
   parse warnings and the catalog reports are merged once, sorted by line and column (stable), and kept as the
   document's reports.
5. **Output**: an immutable `VegaLiteDocument`, safe to share between threads.

**Measure and render path (`Measure`, `Render(options)`):**

1. **Choose options**: the given options, or the defaults.
2. **Prepare the chart** (pure, delivers nothing): `ConfigResolver` merges the defaults, the specification's `config`,
   and the caller's `Config`; a `TextMeasurer` is created for the font options. When the root is a single view
   (no `layer`, `concat`, `hconcat`, `vconcat`, `facet`, or `repeat`) with a supported mark, `DataLoader` reads the
   inline rows and `ScaleResolver` infers the scales. `ViewLayout` resolves the view request (size, padding,
   autosize, background, view style) with the discrete-step hints of the scales; `TitleBuilder` builds the title. If
   there is no drawable single view the layout is computed around the title alone (an empty view, as for a
   composition or an unsupported mark). Otherwise the chart is built: the axes are built for the current data
   rectangle, their extents measured, and the layout re-measured, at most three times, until the data rectangle is
   stable (it is stable at once for fixed sizes); the `MarkBuilder` then builds the marks once for the final rectangle,
   the item budget being charged for the axes and counted per batch of rows; and `ViewLayout.Build` assembles the
   scene (root, view group, grids, axes behind the marks, the marks group, axes in front, title) and the natural
   size. When this fails with a specification or render error, the reports found so far are still delivered before
   the error is rethrown.
3. **Validate**: the natural size times the scale is validated (each side from 1 to `Surface.MaxDimension`), then the
   `Parameters` names, then the token.
4. **Record**: the reports of the document (warnings, then catalog reports, in document order) are recorded in the
   `VegaLiteReportSink`, which de-duplicates, caps, and, in strict mode, throws at the first unsupported feature.
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
  `key`) or, when it changes what is drawn (parameters and selections), reported
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
| Vega-Lite | BSD-3-Clause | `ConfigResolver`, `ScaleResolver`, `ColorSchemes`, `AxisBuilder`, `MarkBuilder` | Default configuration values (background, padding, view size, step, mark, bar, rect, tick, rule, scale, header, and spacing configuration), the `tableau10` default category colors, the precedence of mark, view, title, and style properties, the scale inference rules (type by field type, channel, and mark; domain order; zero, nice, padding, and reverse rules; default size and opacity ranges; step sizing), the default axis tick count and axis defaults and rules, and the inferred orientation, band size, opacity, color, and invalid-data rules of the supported marks |
| Vega | BSD-3-Clause | `ConfigResolver`, `ViewLayout`, `TextMeasurer`, `TitleBuilder`, `AxisBuilder`, `SceneModel`, `ScenePainter`, `Ticks`, `ScaleModel`, `ScaleResolver` | Default Vega configuration values, the guide, group, and mark styles, the title and axis layout constants and offsets, the axis geometry (tick and label positions, band offset, rounding, half-pixel translate, extent clamping, label overlap removal), the tick-count rules, the `bandspace` rule, the text baseline offsets and line-height and truncation rules, the group stroke offset, the symbol shape geometry, and the view-layout rules for `autosize` and title placement |
| d3-array | ISC | `Ticks` | The `ticks`, `tickIncrement`, and `tickStep` algorithms |
| d3-format | ISC | `NumberFormat` | The format specifier grammar and the `format`, `formatPrefix`, `precisionFixed`, `precisionPrefix`, and `precisionRound` behavior for the numeric types |
| d3-scale | ISC | `ScaleModel`, `Ticks`, `NumberFormat` | The arithmetic of the linear, band, and point scales, the linear `nice` method, and the precision rule of `tickFormat` |
| d3-scale-chromatic | ISC | `ColorSchemes` | Only the ten `tableau10` colors; no ColorBrewer scheme is used |
<!-- markdownlint-enable MD013 -->

The obligations of the licenses are met as follows:

- The license texts ship with the package: `src/DemaConsulting.CanvasNet.VegaLite/third-party/LICENSE-vega.txt`
  (BSD-3-Clause) and `LICENSE-d3.txt` (ISC) are packed under `licenses/`, and the `CanvasNetTool` package, which bundles
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

This table is the exact boundary of the **first delivery**. The renderer does not imply general Vega-Lite support: a
recognized construct outside the table is reported with the identifier shown, never skipped silently, and strict mode
throws at the first report. The reports carry the RFC 6901 pointer and the one-based line and column of the construct.

<!-- markdownlint-disable MD013 -->
| Area | Supported | Reported (identifier) when outside the supported subset |
| ---- | --------- | ------------------------------------------------------- |
| Schema | `$schema` of Vega-Lite version 5 or 6, interpreted with the version 6 defaults; no `$schema` | Another major version, or a URL naming no Vega-Lite version: warning `vl-schema-version` (still rendered with the version 6 defaults); a malformed value is a `VegaLiteSpecException`; plain Vega is rejected |
| Views | One single view | `layer`, `concat`, `hconcat`, `vconcat`, `facet`, `repeat`: `vl-composition-<name>` (only the title, background, and empty view are drawn; nested views are examined too); `projection`: `vl-projection` |
| Data | Inline `data.values`: an array of objects (one row per object, nested values flattened to path fields) or of primitives (one row `{data: value}`) | A missing or empty `values` array or a `data` object without a source: warning `vl-data-empty` (empty chart); `url` with a scheme, drive letter, or network path: `vl-data-remote-url` (never fetched, empty dataset); other `url`: `vl-data-file`; `name` only: `vl-data-named`; `sequence`: `vl-data-sequence`; `format.parse`: `vl-data-parse`; `format.type` csv, tsv, or dsv: `vl-data-format-csv`, `-tsv`, `-dsv`; `format.property`: `vl-data-property`; `values` as text: `vl-data-values-text`; `sphere`, `graticule`, topojson, or `values` as an object: `vl-data-geo` |
| Marks | `bar`, `rect`, `point`, `circle`, `square`, `rule`, `tick` (a mark given as a string or as a definition object with `type`) | Every other recognized mark type: `vl-mark-<type>`; the chart is drawn without marks and without axes |
| Channels | `x`, `y`, `color`, `fill`, `stroke`, `size`, `opacity` as `field`, literal `value`, or literal `datum` where the channel permits; `tooltip`, `href`, `description`, and `key` are accepted and ignored without a report | Every other recognized channel: `vl-channel-<name>`; `longitude`, `latitude`, `longitude2`, `latitude2`: `vl-channel-geo` |
| Field types | `quantitative`, `nominal`, `ordinal` | `temporal`: `vl-type-temporal`; `geojson`: `vl-type-geojson` |
| Encoding properties | `field`, `type`, `value`, `datum`, `title`, `scale`, `axis`, `legend` (as null only), `sort` (ascending, descending, null, or an array of values), `bin` and `stack` (as false or null only) | `aggregate`, `bin`, `timeUnit`, `stack` (an implicit bar stack is reported at the measure channel), `sort` by a field or channel, `condition`, `impute`, `bandPosition`: `vl-encoding-<name>`; a quantitative color field: `vl-scale-color-ramp` |
| Scales | Types linear, band, point, and ordinal; the properties `type`, `domain`, `range`, `zero`, `nice`, `padding`, `reverse`, `paddingInner`, `paddingOuter`, `align`, `round`, `clamp`, and `scheme` of value `tableau10` | Every other scale type: `vl-scale-<type>`; every other scale property, and a scheme other than `tableau10`: `vl-scale-property-<name>`; an unknown scale type value is a `VegaLiteSpecException` |
| Axes | Grid, ticks, labels, domain, title, orient, offset, minimum and maximum extent, `tickCount`, `tickMinStep`, `values`, d3 `format`, `labelAngle`, `labelAlign`, `labelBaseline`, `labelPadding`, `labelOffset`, `labelLimit`, `labelFlush`, `labelFlushOffset`, `labelOverlap`, `labelSeparation`, the font, color, opacity, dash, width, and cap properties of the parts, `titleAnchor`, `titleAngle`, `titlePadding`, `zindex`, and the configuration precedence of Vega-Lite | `axis.encode`: `vl-guide-encode`; `labelBound`, `tickBand`, `tickExtra`, `position`, `translate`, `bandPosition`, `titleX`, `titleY`: `vl-guide-axis-<name>`; `formatType`: `vl-format-type-custom`; `labelExpr`: `vl-expr-label` |
| Legends | None | A color, fill, stroke, size, or opacity channel driven by a field, unless `legend` or `scale` is null: `vl-guide-legend`; `legend.encode`: `vl-guide-encode` |
| Titles | `text` (a string or an array of lines), `subtitle`, `anchor`, `orient`, `offset`, `frame`, the font, color, and alignment properties, `limit`, `angle`, `dx`, `dy` | `{"expr": ...}`: `vl-expr-ref` |
| Layout | `width` and `height` as numbers, `{step}`, or `"container"` (the container size is an option); `padding` as a number or object; `autosize` of `pad` or `none` (with `contains`); `background`; the `view` configuration (size, fill, stroke) | `autosize` of `fit`, `fit-x`, `fit-y`: `vl-layout-autosize-<type>` (laid out as `pad`); `{"expr": ...}` anywhere in a view: `vl-expr-ref` |
| Configuration | Vega-Lite 6 and Vega defaults, the specification's `config`, and the caller's `Config`, merged in that order; the `style` mechanism; the default categorical palette is `tableau10` | N/A (unknown keys are ignored) |
| Transforms | None | Every entry of `transform`: `vl-transform-<name>`; `calculate` expressions: `vl-expr-calculate`; string filters: `vl-expr-filter` |
| Parameters | None (the default state is not applied) | `params` entries: `vl-param-variable`, `vl-param-selection`; the legacy `selection`: `vl-param-selection`; `bind` is ignored |
| Time | None; the `TimeZone` option is accepted and reserved (UTC by default) | `vl-type-temporal`, `vl-encoding-timeunit`, `vl-transform-timeunit` |
| Fonts | The bundled Liberation Sans, Serif, and Mono for generic and Arial-like families; named families through `VegaLiteFontOptions` (`UseSystemFonts` is `false` by default) | N/A (an unresolvable family falls back to the bundled sans font) |
<!-- markdownlint-enable MD013 -->

The **stable report identifiers** are part of the public contract of the package and are listed in the XML
documentation of the internal `FeatureCatalog` type. Identifiers are lower case, of the form `vl-<area>-<detail>`; a
name taken from the specification is lower-cased without separators (`timeUnit` gives `timeunit`):

<!-- markdownlint-disable MD013 -->
| Identifier | Kind | Reported at, and when |
| ---------- | ---- | --------------------- |
| `vl-schema-version` | Warning | `$schema`, when the major version is not 5 or 6 or the URL names no Vega-Lite version |
| `vl-data-empty` | Warning | The `data` object, when it has an empty `values` array or no source key |
| `vl-data-remote-url` | Unsupported | The `data` object, when `url` has a scheme, a drive letter, or is a network or UNC path |
| `vl-data-file` | Unsupported | The `data` object, when `url` is a relative local path |
| `vl-data-named` | Unsupported | The `data` object that has only a `name` |
| `vl-data-sequence` | Unsupported | `data/sequence` |
| `vl-data-parse` | Unsupported | `data/format/parse`, when not null |
| `vl-data-format-csv`, `vl-data-format-tsv`, `vl-data-format-dsv` | Unsupported | `data/format/type` naming a delimited text format |
| `vl-data-property` | Unsupported | `data/format/property` |
| `vl-data-values-text` | Unsupported | `data/values` given as a string |
| `vl-data-geo` | Unsupported | `sphere`, `graticule`, a topojson format, or `values` given as an object |
| `vl-mark-<type>` | Unsupported | `mark`, for every type except bar, rect, point, circle, square, rule, and tick |
| `vl-channel-<name>` | Unsupported | A channel other than x, y, color, fill, stroke, size, and opacity (tooltip, href, description, and key are ignored) |
| `vl-channel-geo` | Unsupported | `longitude`, `latitude`, `longitude2`, `latitude2` |
| `vl-type-temporal`, `vl-type-geojson` | Unsupported | The `type` of a channel |
| `vl-scale-color-ramp` | Unsupported | A quantitative `color`, `fill`, or `stroke` field (the default mark color is used) |
| `vl-encoding-aggregate`, `-bin`, `-timeunit`, `-stack`, `-sort`, `-condition`, `-impute`, `-bandposition` | Unsupported | That property of a supported channel (`bin` and `stack` unless false or null; `sort` only for a shorthand or an object) |
| `vl-scale-<type>` | Unsupported | `scale/type`, for every type except linear, band, point, and ordinal |
| `vl-scale-property-<name>` | Unsupported | A scale property outside the supported list; `scheme` unless `tableau10` |
| `vl-guide-legend` | Unsupported | The legend, or the channel, of a field-driven color, fill, stroke, size, or opacity channel |
| `vl-guide-encode` | Unsupported | `axis.encode`, `legend.encode` |
| `vl-guide-axis-<name>` | Unsupported | `labelBound`, `tickBand`, `tickExtra`, `position`, `translate`, `bandPosition`, `titleX`, `titleY` of an axis |
| `vl-format-type-custom` | Unsupported | `axis.formatType` |
| `vl-expr-ref`, `vl-expr-calculate`, `vl-expr-filter`, `vl-expr-label` | Unsupported | An `{"expr": ...}` value in a title, size, padding, background, view, mark, or encoding; a calculate expression; a string filter; `axis.labelExpr` |
| `vl-transform-<name>` | Unsupported | Every entry of `transform` |
| `vl-composition-layer`, `-concat`, `-hconcat`, `-vconcat`, `-facet`, `-repeat` | Unsupported | That property; the nested views are examined too |
| `vl-param-variable`, `vl-param-selection` | Unsupported | Every entry of `params`; the legacy top-level `selection` |
| `vl-projection` | Unsupported | `projection` |
| `vl-layout-autosize-<type>` | Unsupported | `autosize` types other than pad and none |
| `vl-duplicate-key` | Warning | The later of two equal keys of one object (the last value wins) |
| `vl-unknown-property` | Warning | A key that Vega-Lite does not define for a view, mark definition, encoding, or channel definition |
| `vl-diagnostics-suppressed` | Warning | The first report beyond `MaxDiagnostics` |
<!-- markdownlint-enable MD013 -->

**Deferred phases.** The roadmap that follows the first delivery adds, each in its own pull request with the full
companion artifacts, the following capabilities; until then the constructs are reported with the identifiers above:

- **Data files**: local CSV, TSV, DSV, and JSON files from a sandbox (the document folder, `DataPaths`, or a
  `FileResolver`), `parse` and type inference, `sequence`, and `datasets`; remote URLs stay reported
- **Expressions and row transforms**: the Vega expression language (a closed, bounded interpreter), `calculate`,
  `filter`, `fold`, `flatten`, `sample`, `pivot`, `lookup`
- **Aggregation**: `aggregate`, `joinaggregate`, `window`, `bin`, `stack`, `impute`, and `count`
- **Legends and mark-property channels**: legends, the remaining color schemes (with their notices), the other scale
  types (log, pow, sqrt, symlog, quantize, quantile, threshold, sequential, diverging), and the `shape`, `strokeDash`,
  `strokeWidth`, `angle`, and `fillOpacity` channels
- **Time**: calendar and time-zone handling, time and UTC scales, `timeUnit`, `datetime`, and time formatting (the
  `TimeZone` option, UTC by default)
- **Line-like and other marks**: `line`, `area`, `trail`, `text`, `arc`, and curve interpolation
- **Layers and composite marks**: `layer` with `resolve`, `boxplot`, `errorbar`, `errorband`
- **Composition**: `concat`, `hconcat`, `vconcat`, `facet`, `repeat`, `autosize` of `fit`, and `"container"` sizes in
  compositions
- **Parameters**: variable and selection parameters in their default state and the caller's `Parameters` overrides
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
6. **Clip**: marks are clipped only with `"clip": true`, as in Vega-Lite 6, never to the view.
7. **Title lines**: a line feed inside title text splits the line, which Vega does not do.
8. **Painting**: clips are composited with fractional coverage; text is filled only; an unknown symbol shape is drawn
   as a circle; fills are flattened at a tolerance of 0.02 pixel so that small circles stay round.
9. **Number formatting**: the d3 `en-US` locale with the minus sign U+2212, culture-invariant; widths above 1,000 are
   clamped as a denial-of-service guard.

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
3. **Phase 3 - Tool wiring (delivered with this pull request).** The `canvasnet` tool recognizes `.vl.json` and `.vl`
   files and `$schema`-identified JSON, probes and renders Vega-Lite charts, reports the chart facts, and maps the
   library exceptions to its stable error codes; the tool package bundles the assembly and the license texts. The
   tool's own design, verification, and requirements are documented in the `CanvasNetTool` artifacts.
4. **Phase 4 - Data files (planned).** The data-file sandbox, delimited text parsing, `parse` and inference,
   `sequence`, and `datasets`.
5. **Phase 5 - Expressions and row transforms (planned).**
6. **Phase 6 - Aggregation (planned).**
7. **Phase 7 - Legends and mark-property channels (planned).**
8. **Phase 8 - Time (planned).**
9. **Phase 9 - Line-like and other marks (planned).**
10. **Phase 10 - Layers and composite marks (planned).**
11. **Phase 11 - Composition (planned).** The recommended release gate of the package.
12. **Phase 12 - Parameters and default state (planned).**
13. **Phase 13 - Advanced features and polish (planned).**
14. **Phase 14 - Hardening (planned).**

Every later phase adds members and types rather than changing the signatures delivered here.
