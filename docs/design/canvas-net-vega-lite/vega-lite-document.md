## VegaLiteDocument Unit Design

![CanvasNetVegaLite Structure](CanvasNetVegaLiteView.svg)

### Purpose

`VegaLiteDocument` is the public entry point of `CanvasNetVegaLite`. It reads Vega-Lite specification text from a
string, a stream, or a file, drives the internal `SpecModel` subsystem to parse and describe it, and exposes the
validated result. A document proves that the text is well-formed JSON within the limits of `VegaLiteLimits`, that the
Vega-Lite properties the renderer depends on have the right shape, and describes the chart without drawing it (schema
version, title, mark types, data references, parameter names), so that tools can list what a chart needs before they
render it. **Opening never loads data, never opens another file, never evaluates a parameter or a
transform, and never uses the
network**; it does compile the expressions of the specification (so a broken expression fails when the document is
opened). A document can then be
**measured** (`Measure`) and **rendered** (`Render`, at its natural size times a scale or at an exact size): both run
the same internal pipeline, which drives the `SpecModel`, `Data`, `Expressions`, `Transforms`, `Scales`, `Guides`, `Layout`,
`Marks`, and
`Scenegraph` subsystems and delivers the warnings and unsupported-feature reports to the caller's handlers. **Only
`Measure` and `Render` read data and run the parameters and the row transforms over it:** inline rows, named datasets,
and sequences need no I/O, and a relative local
`data.url` is read from the sandbox that the open options define (see _Data Subsystem Design_); a file that cannot be
used is a `VegaLiteRenderException`, and a remote URL is reported and never fetched.

The unit covers the files `VegaLiteDocument.cs` (the entry points and the descriptive properties),
`VegaLiteDocument.Render.cs` (`Measure`, `Render`, and the pipeline), the options `VegaLiteOpenOptions.cs`,
`VegaLiteRenderOptions.cs`, `VegaLiteFontOptions.cs`, and `VegaLiteLimits.cs`, the value types `VegaLiteLocation.cs`,
`VegaLiteSize.cs`, `VegaLiteDataReference.cs`, and `VegaLiteDataKind.cs`, the records `VegaLiteDiagnostic.cs` and
`VegaLiteUnsupportedFeature.cs`, the exceptions `VegaLiteSpecException.cs`, `VegaLiteRenderException.cs`, and
`VegaLiteUnsupportedFeatureException.cs`, the internal `VegaLiteReportSink.cs`, and the namespace documentation
`NamespaceDoc.cs`.

The unit's behavior is specified by the `CanvasNetVegaLite-VegaLiteDocument-*` requirements in
`docs/reqstream/canvas-net-vega-lite/vega-lite-document.yaml` and, at system level, by the `CanvasNetVegaLite-*`
requirements in `docs/reqstream/canvas-net-vega-lite.yaml` (opening, schema versions, immutable options, measuring and
rendering, diagnostics, unsupported features, limits and cancellation, determinism, and bundled fonts).

### Data Model

- **`VegaLiteDocument`** (sealed partial class, immutable) - holds the internal `SpecTree` (the parsed
  specification), the `SpecMetadata` read from it once, the effective `VegaLiteOpenOptions`, the read-only
  `MarkTypes` list, and the internal `Reports` list (the parse warnings and the catalog reports, merged in document
  order once at construction). The constructor is private; the only ways to obtain a document are `Parse` and the two
  `Open` overloads. `Tree`, `Options`, and `Reports` are `internal` so that the pipeline consumes them without widening
  the public surface. The class is `partial` only so that `Measure`, `Render`, and the pipeline live in
  `VegaLiteDocument.Render.cs`. A document holds no unmanaged resources, is not disposable, and keeps no state between
  calls: it is safe for concurrent use, including concurrent `Measure` and `Render` calls. The public properties are
  `SourceName`, `SchemaUrl` (as written), `SchemaMajorVersion` (the version in the URL, or null), `Title` (plain text,
  lines joined by a line feed; null for none or an expression), `MarkTypes` (in document order, nested views included),
  `DataReferences` (a view without a `data` object contributes none), and `ParameterNames` (distinct, in document order).
- **`VegaLiteOpenOptions`** (sealed class, init-only) - `BaseDirectory` (`string?`), `DataPaths`
  (`IReadOnlyList<string>`, empty by default, copied), `FileResolver` (`Func<string, Stream?>?`), `SourceName`
  (`string?`), and `Limits` (`VegaLiteLimits?`; the defaults when null). Every property is validated in its `init`
  accessor: null is allowed for the nullable properties, but empty or whitespace text throws `ArgumentException`; a
  null `DataPaths` list or entry throws `ArgumentNullException`, and a whitespace entry throws `ArgumentException`.
  Opening consumes only `SourceName` and `Limits`; `BaseDirectory` (the base directory of the sandbox: the folder of an
  opened file unless set; for a document from a string or stream, none), `DataPaths` (further directories, searched in
  order after the base directory), and `FileResolver` (a callback that receives a normalized logical path and returns
  a stream or null; it replaces the file system and is never called for a remote URL) are consumed by `Measure` and
  `Render` through the data stage. The internal `WithEffective(baseDirectory, sourceName)` creates a copy
  with those two values filled in, and `EffectiveLimits` is the limits or `VegaLiteLimits.Default`.
- **`VegaLiteRenderOptions`** (sealed class, init-only) - `Scale` (default 1; finite, greater than zero, at most
  `Surface.MaxDimension`, else `ArgumentOutOfRangeException`), `Background` (`Rgba32?`), `Config` (`string?`; a JSON
  object of at most 4,194,304 characters and 64 levels, else `ArgumentException`), `Parameters`
  (`IReadOnlyDictionary<string, object?>?`; keys non-blank; values null, `bool`, `string`, a finite number of any
  built-in numeric type, `DateTime`, or `DateTimeOffset`; copied; the value of a **variable parameter** of the root
  view, which replaces the definition of that parameter, `value` or `expr`, and is read by the expressions of the
  transforms and the row-bound mark values; a selection parameter name is accepted and ignored), `Datasets` (named
  lists of rows of the same value
  kinds, deep-copied; a caller dataset replaces a top-level dataset of the same name, and a `DateTime` or
  `DateTimeOffset` becomes a date in UTC), `TimeZone` (`TimeZoneInfo?`; **null is UTC**; the one zone in which the chart
  works, see _Time zone and calendar_ below), `ContainerWidth` (default 400) and
  `ContainerHeight` (default 300) (each 1 to `Surface.MaxDimension`), `Fonts` (`VegaLiteFontOptions?`), `Limits`
  (default `VegaLiteLimits.Default`; null throws `ArgumentNullException`), `WarningHandler`
  (`Action<VegaLiteDiagnostic>?`), `UnsupportedFeatureHandler` (`Action<VegaLiteUnsupportedFeature>?`), and
  `ThrowOnUnsupportedFeature` (default false). The internal `EffectiveFonts` is `Fonts` or a default
  `VegaLiteFontOptions`.
- **`VegaLiteFontOptions`** (sealed class) - `UseSystemFonts` (default false) and `AdditionalDirectories`
  (`IReadOnlyList<string>`, empty by default; a null list throws `ArgumentNullException`, a null, empty, or whitespace
  entry throws `ArgumentException`; copied into a read-only array).
- **`VegaLiteLimits`** (sealed class, init-only) - the sixteen limits `MaxSourceLength`, `MaxJsonDepth` (1 to 256),
  `MaxDataFiles`, `MaxDataBytes`, `MaxDataRows`, `MaxFieldCount`, `MaxViews`, `MaxCompositionDepth` (1 to 64),
  `MaxSceneItems`, `MaxScaleDomainSize`, `MaxTicks`, `MaxExpressionLength`, `MaxExpressionDepth` (1 to 256),
  `MaxEvaluationSteps`, `MaxStringLength`, and `MaxDiagnostics`, with the `Default` instance; a value outside its range
  throws `ArgumentOutOfRangeException` when set. The defaults and the stage that enforces each are listed in
  _CanvasNetVegaLite System Design_. The three expression limits (`MaxExpressionLength`, `MaxExpressionDepth`, and
  `MaxEvaluationSteps`) are active since phase 5, and `MaxDataRows` and `MaxFieldCount` also bound the tables that the row
  and aggregating transforms and the implicit pipeline derive; since phase 6 `MaxEvaluationSteps` also bounds the work of
  the aggregating transforms and `MaxScaleDomainSize` the number of bins. No limit was added. Phase 8 (time) added no
  limit either: time ticks are bounded by `MaxTicks`, formatted dates by `MaxStringLength`, date sequences, discrete date
  domains, and the `maxbins` of a time unit by `MaxScaleDomainSize`, the work of the time-unit transform and of the date
  functions by `MaxEvaluationSteps`, and the number of derived fields and rows by `MaxFieldCount` and `MaxDataRows`.
- **`VegaLiteLocation`** (`readonly record struct (string? File, string Pointer, int Line, int Column)`) - a position
  in the specification. A column counts UTF-16 code units, a tab counts as one column, and a line ends at `\n`.
  `default` (line zero, empty pointer) means "unknown"; `IsKnown` is true when a line or a pointer is present.
  `ToString()` returns `{File}:{Line}:{Column}` (without the file when null, with `?` for an unknown line), followed by
  a space and the pointer when it is not empty.
- **`VegaLiteDiagnostic`** (`sealed record (string Message, VegaLiteLocation Location)`) and
  **`VegaLiteUnsupportedFeature`** (`sealed record (string Feature, string Message, VegaLiteLocation Location)`) - a
  warning (the message starts with its stable identifier and a colon) and an unsupported-feature report. `ToString()`
  prefixes the location when it is known.
- **`VegaLiteSize`** (`readonly record struct (int Width, int Height)`), **`VegaLiteDataReference`**
  (`sealed record (VegaLiteDataKind Kind, string? Url, long? RowCount, VegaLiteLocation Location)`), and
  **`VegaLiteDataKind`** (`Inline`, `Url`, `Name`, `Sequence`) - small immutable value types.
- **`VegaLiteSpecException`** (sealed, derives from `FormatException`), **`VegaLiteRenderException`** (sealed, derives
  from `InvalidOperationException`), and **`VegaLiteUnsupportedFeatureException`** (sealed, derives from
  `IOException`) - each carries a `Location` (the last also the `Feature`); the constructor taking
  `(description, location)` builds the message `{location}: {description}`, or just the description when the location
  is unknown; the standard parameterless, message, and message-plus-inner-exception constructors leave the location
  unknown. `VegaLiteSpecException` derives from `FormatException` because `InvalidDataException` is sealed.
- **`VegaLiteReportSink`** (internal sealed class) - the buffer of one call: recorded warnings and unsupported
  features in order, de-duplicated by value, capped at `MaxDiagnostics`, with the strict-mode switch (strict mode always
  records the first unsupported feature, even beyond the cap, so the handler receives it before the exception is thrown).

### Key Methods

- `Parse(string json, VegaLiteOpenOptions? options)` - requires a non-null string, uses the default options when none are
  given, and delegates to the internal `Create`.
- `Open(string path, VegaLiteOpenOptions? options)` - requires a non-blank path, fills in the effective options (the
  path names the source and its folder is the base directory, unless overridden), opens the file, reads it with
  `ReadBounded`, and delegates to `Create`; a missing file or directory surfaces the natural .NET exception.
- `Open(Stream stream, VegaLiteOpenOptions? options)` - requires a non-null readable stream and reads it from its
  current position with `ReadBounded`; the stream is left open.
- `ReadBounded(stream, maxLength)` - reads the raw bytes in 8,192-byte chunks (it does not decode them) and throws an
  `InvalidDataException` as soon as the running byte count exceeds three bytes per permitted character plus a
  byte-order mark (no input that long can fit the limit), so a stream that never ends cannot consume unbounded memory.
  The exact character count and the strict UTF-8 decoding are done by `SpecParser.Parse(byte[], ...)`, so a malformed
  byte sequence is a located `VegaLiteSpecException` and is never replaced by U+FFFD.
- `Create(source, options)` - calls `SpecParser.Parse(source, sourceName, limits)` (for a string or for the bytes of a file
  or stream) and wraps the tree in a document
  whose constructor reads the metadata and merges the reports.
- `Measure(options, token)` - prepares the chart (`PrepareChartOrReport`), validates the scaled size
  (`ValidateScaledSize`: each side of the natural size times the scale, rounded half away from zero, must be 1 to
  `Surface.MaxDimension`), validates the parameter names, checks the token, and runs the pipeline with a finish step
  that returns the size.
- `Render(options, token)` - the same checks, with a finish step that paints the scene at `ScenePlacement.Uniform(scale)`
  onto a new surface of the scaled size.
- `Render(width, height, options, token)` - validates the width and height first (`ArgumentOutOfRangeException`), then
  the parameter names and the token, prepares the chart, and paints at `ScenePlacement.Fit` onto a surface of exactly
  that size.
- `Run(options, token, plan, finish)` - creates the sink, records the reports (`BuildPlan` ->
  `RecordReports`: warnings through `Warn`, unsupported features through `Unsupported`, which throws in strict mode),
  runs the finish step, and delivers the sink. On a failure the reports recorded so far are delivered with handler
  exceptions ignored; on success a handler exception propagates after the result is disposed.
- `PrepareChart(options, token)` - the layout described in the system design (configuration, measurer, data,
  scales, view request, title, and either the empty layout or `BuildChart`). For a single view with a supported mark it
  first creates the **calendar** of the call once, `TimeCalendar.Create(options.TimeZone)` (null gives the UTC
  singleton), then creates one `DataLoader` for the call (passing the effective open options, the top-level `datasets`
  node, the
  caller's `Datasets`, the limits, the token, and the calendar), loads the data **before** the scene budget is created
  and before the
  scales are resolved, passing `TemporalFields.Collect(root)` (the canonical names of the fields that the encoding,
  the `timeUnit` transforms, and the filter and condition predicates use as dates) so that the loader date-parses them
  implicitly in the calendar unless `format.parse` names them (or is null), and then, on the same loader: creates one
  `ExpressionEvaluator` for the call (the limits, the
  token, the dictionary of parameter values, and the **same calendar**, which reaches every expression function
  through `ExpressionFunctionContext.Calendar`), calls `ParameterEnvironment.Populate` (the static parameters, with the
  caller's `Parameters`), and calls `RowTransforms.Apply` over the loaded table with a `TransformContext` (which also
  runs the aggregating transforms, the `timeUnit` transform, and the time-aware predicates); it then creates the
  `EncodingPlan` of the view (`EncodingPlan.Create`, a pure function
  of the specification and the configuration) and runs `EncodingTransforms.Apply` on the same context, which performs the
  implicit bin, **time unit**, aggregate, and stack of the encoding in Vega-Lite's order and returns the `EncodingData`
  (the **Raw** and
  **Main** tables, the bins, and the resolved time units). The scales are resolved from the plan and the data (a sort by
  an aggregate and an
  `unaggregated` domain read Raw, everything else Main; the resolver reads the calendar from the evaluation facts of the
  `EncodingData`, so its signature did not change), and the marks draw Main, so the field names, the titles, and the
  tables agree by construction; the axes and legends read the calendar and the time unit from each `ResolvedScale`, and
  the legend builder passes the calendar of its canonical scale to its own `ExpressionFunctionContext`; the
  `RowExpressions` (the compiled
  expressions and the evaluator) go to
  `MarkBuilder`. `ApplyStandalone` (the entry the tests use for the implicit pipeline alone) passes `TimeCalendar.Utc`
  explicitly. The findings of the data stage are kept in the plan in this order: the loader's warnings
  (`vl-data-parse-failed`), the warnings of a lookup source (the loader's warnings that arose while the transforms ran),
  and then the transform warnings (`vl-expr-runtime`, `vl-transform-runtime`); they are recorded
  in the report sink as soon as
  they exist (a `finally` block around the parameters and transforms), so a later failure still delivers them, and the
  `BuildPlan` step records them after the reports of the specification. The only I/O of the pipeline
  happens here, on the calling thread.
  `BuildChart` iterates the axes, the legends, and the
  layout at most three passes (each pass builds the axes and the legends again for the data rectangle, the legends are
  placed by `LegendLayout` against the axes and their reach joins the extents of the axes before the title is laid out),
  then builds the marks once and assembles the scene (grids, back axes, marks, front axes, then the legends, then the
  title). `PrepareChartOrReport` wraps it so
  that a `VegaLiteSpecException` or `VegaLiteRenderException` still delivers the document's reports before it is
  rethrown (in strict mode the first unsupported feature wins).
- **Time zone and calendar** (the pipeline step "calendar", Phase 8). `VegaLiteRenderOptions.TimeZone` is the **only**
  zone the renderer ever uses: no code of the pipeline reads `TimeZoneInfo.Local`, the host clock, or the host culture,
  so a render depends only on the specification, the data, and the options. The XML documentation of the property reads
  "the time zone in which dates are interpreted and time units are computed, or null for UTC", with the remark that UTC
  is the default so that output does not depend on the host's local time zone (the code of Phase 8 did not change that
  text; this section is the full statement). The zone is applied to: a date-time **without an offset** in the data
  (`2024-03-05T13:00:00` and the legacy forms of `Date.parse`; a date-only ISO text and a text with an offset or `Z` are
  not shifted), the `date:'%fmt'` parse directives (`utc:'%fmt'` always reads UTC), the **local** time units (`month`,
  `yearmonthdate`, `hours`, and so on: the day boundaries are local midnights, so a day of a zone with daylight saving
  time can be 23 or 25 hours long), the ticks and `nice` boundaries of a `time` scale, the labels of its axes and
  legends, the local date and time functions of the expressions (`datetime`, `month`, `timeFormat`, ...), and the
  dates of a date-time object that does not say `utc`. It is **not** applied to the `utc*` time units, `utc` scales,
  `utcFormat`, `utcParse`, and `utc` date-time objects, which always use UTC, nor to a `DateTime` or `DateTimeOffset`
  that the caller supplies (those are converted to UTC instants, as before). The offset of a zone at an instant comes
  from `TimeZoneInfo.GetUtcOffset(DateTime utc)`; a fixed-offset zone and UTC take a fast path; a local wall time is
  converted to an instant by the ECMAScript rule (the offset before the transition: in a spring-forward gap 02:30 is
  03:30, and in a fall-back overlap the earlier instant wins; this assumes at most one transition in 48 hours, which is
  true of every real zone). The public API did not change: `TimeZone` already existed and was documented as the
  zone of the dates.
- `ValidateParameters(options)` - checks every `Parameters` key against `ParameterNames` and reports the first unknown
  name in ordinal order, so the message does not depend on the caller's dictionary order.

**Preconditions and postconditions**: a document always holds a fully validated tree; `Measure` and `Render` never
mutate it; every returned surface is new and owned by the caller; `Measure` returns exactly the size of the surface
`Render(options)` produces for the same options.

### Error Handling

Errors are typed and fail closed. Null arguments and invalid options throw `ArgumentNullException`,
`ArgumentException`, or `ArgumentOutOfRangeException` at the point of the call or the initialization. Text longer than
`MaxSourceLength` is an `InvalidDataException`. Malformed JSON, nesting beyond `MaxJsonDepth`, a known property with a
wrong value, and plain Vega are `VegaLiteSpecException`s with the location of the first problem (detected by
`SpecParser`; the unit only delegates). That includes an expression that cannot be compiled
and a transform, predicate, or
parameter of the wrong shape, an unknown time unit, a date-time object with an unknown month or day name, and a
`tickCount` or `nice` interval with an unknown name or a non-positive step: they fail **when the document is opened**,
not when it is rendered. Failures while preparing
the chart are `VegaLiteSpecException` (an invalid axis
orientation, d3 format, scale domain or range, including a temporal domain entry that is not a date) or
`VegaLiteRenderException` (a data,
transform, expression, scale, axis, or
scene limit (including more time ticks than `MaxTicks` and a formatted date longer than `MaxStringLength`), an exhausted
evaluation step budget, an invalid or too slow regular expression, and a
local data file or named dataset that cannot be used: no file source, a path the sandbox refuses, a link, a missing or
unreadable file, invalid UTF-8, invalid JSON or delimited text, an undefined name; in lenient and strict mode alike),
each with a location and thrown from the stage that detected them; the unit does not catch and convert them,
except that it
delivers the already-found reports first. A scaled size outside 1 to `Surface.MaxDimension` and an unknown parameter
name are argument exceptions. A cancellation is an `OperationCanceledException`, never wrapped. Strict mode throws a
`VegaLiteUnsupportedFeatureException` at the first unsupported feature after delivering that report. A handler that
throws after a successful call propagates its exception (the surface is disposed); on a failure path it is ignored.

### Dependencies

- **`SpecModel` subsystem** - `SpecParser`, `SpecTree`, `SpecMetadata`, `FeatureCatalog`, `FeatureReport`,
  `ConfigResolver`, `ResolvedConfig`
- **`Data` subsystem** - `DataLoader` (one instance per call; reads local files through `DataFileResolver` and
  delimited text through `DelimitedTextParser`, and carries the row budget of the call), `DataTable`, `DataFileResolver`,
  `DelimitedTextParser`, and `TimeCalendar` (created once per call from `VegaLiteRenderOptions.TimeZone` and passed to
  the loader and the evaluator; see _Time Calendar Design_)
- **`Expressions` subsystem** - `ExpressionEvaluator` (one instance per call, with the parameter dictionary and the
  calendar)
- **`Transforms` subsystem** - `ParameterEnvironment`, `RowTransforms` (which dispatches to `AggregateTransforms` and
  `TimeUnitTransform`),
  `EncodingPlan`, `EncodingTransforms`, `EncodingData`, `TransformContext`, `RowExpressions`, and `TemporalFields` (the
  implicit date-parse fields)
- **`Scales` subsystem** - `ScaleResolver` (the pipeline entry that takes the plan and the data), `ResolvedScales`
- **`Guides` subsystem** - `TextMeasurer`, `TitleBuilder`, `TitleBlock`, `AxisBuilder`, `AxisRequest`, `AxisResult`,
  `LegendPlan`, `LegendBuilder`, `LegendResult`
- **`Layout` subsystem** - `ViewLayout` and its request and measure records, and `LegendLayout`
- **`Marks` subsystem** - `MarkBuilder`
- **`Scenegraph` subsystem** - `Scene`, `SceneItem`, `SceneBudget`, `ScenePainter`, `ScenePlacement`, `CssColor`
- **Core `CanvasNet`** - `Surface` and `Rgba32` (public API); see _CanvasNetVegaLite System Design_
- **.NET base class library** - `System.IO`, `System.Text`, `System.Text.Json` (the configuration check),
  `System.Collections.ObjectModel`, `System.Globalization`

### Callers

The consumers are applications and libraries that render charts. Within this repository the `CanvasNetTool` system
(the `SourceLoader` unit of its `Imaging` subsystem) calls `Open`, reads the descriptive properties for `info`, and
calls `Measure` and `Render`, mapping the exceptions to its stable error codes. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/` call the public API and, through `InternalsVisibleTo`, the internal
pipeline types.
