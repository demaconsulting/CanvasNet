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
`VegaLiteDocument.Render.cs` (`Measure`, `Render`, and the pipeline), `VegaLiteDocument.Layers.cs` (the layers of a view:
the data walk, the axis planning, and the legend scopes), `VegaLiteDocument.Composition.cs` (the cell: the guides and
the marks of one unit or layered view, built independently of the view that holds it, and the `Composer` that draws a
concatenation, a repeat or a facet from its cells), the options `VegaLiteOpenOptions.cs`,
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
  Phase 9 (the line, area, trail, text, and arc marks) added none either: the segments of a path, the overlay points,
  the arcs, and the lines of a label are charged to `MaxSceneItems`, a label and the labels of a mark to
  `MaxStringLength`, the rows that an impute adds to `MaxDataRows`, and the work of the path marks to
  `MaxEvaluationSteps`. Phase 10 (layers and composite marks) activates the two composition limits: `MaxViews` bounds the
  views of the specification (the layers, and the views that a composite mark expands to) and `MaxCompositionDepth` the
  nesting of `layer`, both enforced by `SpecNormalizer` before any data is read; the data, the tables of the
  expansion, and the marks of the layers are charged to the existing limits. Phase 11 (concatenation and repeat) counts
  the children of a concatenation and the copies of a repeat in `MaxViews` and every nesting level of a composition in
  `MaxCompositionDepth`, in the same place and before any data is read (the copies of a repeat are counted before any is
  made); the cell, the scope group, the title, the axes, the legends and the marks of a composition are charged to
  `MaxSceneItems` before they are created.
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
  features in order, de-duplicated by value (a warning also by an optional key, so that a message that lists only a
  prefix of what identifies it, such as the missing-glyph warning, does not hide a different set), capped at
  `MaxDiagnostics`, with the strict-mode switch (strict mode always
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
- `PrepareChart(options, token)` - the layout described in the system design (configuration, measurer, the layer tree,
  data, scales, view request, title, and either the empty layout or `BuildChart`). It first expands the views
  (`SpecNormalizer.Expand`: the layers of a layered view, the composite marks replaced by their layers, `MaxViews` and
  `MaxCompositionDepth` enforced, so a limit failure is delivered with the reports like any other), and forwards the
  warnings of that one tree (`LayerTree.Warnings`: `vl-concat-axis-shared`, `vl-composition-columns-ignored`,
  `vl-repeat-ref-unresolved`) with the findings of the data stage, so each is delivered once; the chart is drawn: a tree
  whose root is a unit or a layered view goes through `BuildChart` (the single view that iterates its guides with the
  layout),
  a tree whose root is a concatenation or a facet through
  `BuildComposition`, and the layers that are drawn are those with a supported mark (a facet is instantiated by the data
  stage first, so the layers of its cells are drawn). For a view that is drawn (a single view is one layer, the root
  itself; see the next bullet) it first creates the **calendar** of the call once,
  `TimeCalendar.Create(options.TimeZone)` (null gives the UTC singleton), then creates one `DataLoader` for the call
  (passing the effective open options, the top-level `datasets` node, the caller's `Datasets`, the limits, the token,
  and the calendar), loads the data **before** the scene budget is created and before the scales are resolved, passing
  `TemporalFields.Collect(root)` (for a layered view `TemporalFields.CollectAll` over the views that the table serves;
  the canonical names of the fields that the encoding, the `timeUnit` transforms, and the filter and condition
  predicates use as dates) so that the loader date-parses them implicitly in the calendar unless `format.parse` names
  them (or is null), and then, on the same loader: creates one `ExpressionEvaluator` for the call (the limits, the
  token, the dictionary of parameter values, and the **same calendar**, which reaches every expression function through
  `ExpressionFunctionContext.Calendar`), calls `ParameterEnvironment.Populate` (the static parameters, with the caller's
  `Parameters`), and calls `RowTransforms.Apply` over the loaded table with a `TransformContext` (which also runs the
  aggregating transforms, the `timeUnit` transform, and the time-aware predicates); it then creates the `EncodingPlan`
  of the view (`EncodingPlan.Create`, a pure function of the specification and the configuration) and runs
  `EncodingTransforms.Apply` on the same context, which performs the implicit bin, **time unit**, aggregate, and stack
  of the encoding in Vega-Lite's order and returns the `EncodingData` (the **Raw** and **Main** tables, the bins, and
  the resolved time units). The scales are resolved from the plan and the data (a sort by an aggregate and an
  `unaggregated` domain read Raw, everything else Main; the resolver reads the calendar from the evaluation facts of the
  `EncodingData`, so its signature did not change), and the marks draw Main, so the field names, the titles, and the
  tables agree by construction; the axes and legends read the calendar and the time unit from each `ResolvedScale`
  (`BuildChart` also gives the `AxisBuilder` the calendar of the chart, `EncodingData.Evaluation.Calendar`, for the
  labels of a field that is not temporal but has `formatType: "time"`), and the legend builder passes the calendar of
  its canonical scale to its own `ExpressionFunctionContext`; the `RowExpressions` (the compiled expressions and the
  evaluator) go to `MarkBuilder`. `ApplyStandalone` (the entry the tests use for the implicit pipeline alone) passes
  `TimeCalendar.Utc` explicitly. The findings of the data stage are kept in the plan in this order: the loader's
  warnings (`vl-data-parse-failed`), the warnings of a lookup source (the loader's warnings that arose while the
  transforms ran), and then the transform warnings (`vl-expr-runtime`, `vl-transform-runtime`); they are recorded in the
  report sink as soon as they exist (a `finally` block around the parameters and transforms), so a later failure still
  delivers them, and the `BuildPlan` step records them after the reports of the specification, followed by the
  missing-glyph warnings of the scene (`ReportMissingGlyphs`: the scene is walked in drawing order; for every
  `SceneText` that the painter draws (`ScenePainter.IsDrawable`, the painter's own rule at the scale 1, because the
  placement scale is not known at plan time: a visible fill, a positive font size that is still a positive single, and
  a bounded position, offset, angle, and line height), `TextMeasurer.FindMissingGlyphs` lists the code points that
  neither the face nor the core `GlyphFallback` covers, and one `vl-text-missing-glyph` warning goes to the sink at
  `SceneText.Source` (the text
  mark), listing the first eight; the sink de-duplicates identical message, location, and key, and the key is the
  complete set of code points, so two different sets that share the listed eight are both reported, and a set is
  reported once per mark location; text that the chart builds itself, such as axis labels, legends, and titles, has
  no source and
  is reported at the unknown location; a warning is never an unsupported feature, so strict mode does not throw).
  The only I/O of the
  pipeline happens here, on the calling thread. `BuildChart` iterates the guides of the **cell** (`BuildCellGuides`: the
  axes, then the legends) and the layout at most three passes (each pass builds the axes and the legends again for the
  data rectangle, the legends are placed by `LegendLayout` against the axes and their reach joins the extents of the
  axes before the title is laid out; when the third pass still changed the data rectangle the guides are built once
  more for the final one, so they never belong to an earlier rectangle), then builds the content of the cell once
  (`BuildCell`, passing the guides of the
  pass it kept: grids, back axes, marks, front axes, then the legends) and assembles the scene (the content, then the
  title). `PrepareChartOrReport` wraps it so that a `VegaLiteSpecException` or `VegaLiteRenderException` still delivers
  the document's reports before it is rethrown (in strict mode the first unsupported feature wins). **Fit and container
  sizes** (Phase 11): the loop above is the one that a `"container"` size always needed, and it also serves
  `autosize` `fit`, `fit-x`, and `fit-y` (the data rectangle depends on the reach of the axes, the legends, and the
  title, which depend on the data rectangle): `LegendLayout.Combine` is called with the request's `IsFit`, so for a
  fit only the extent of a legend across its side counts, and `ViewLayout.Measure` makes the canvas the fitted size.
  After `ViewLayout.Resolve`, `FitDroppedWarning` turns the `DroppedFit` of the request (a step size that dropped the
  fit of a dimension) into the warning `vl-layout-fit-step`, added to the data warnings after those of the scales
  (and to the sink for a failing call in the same order). A composition takes none of this: `BuildComposition` always
  pads, and the catalog warns (`vl-composition-property-ignored`).
- **Layers** (phase 10, `VegaLiteDocument.Layers.cs`). A view without `layer` is one layer, the view itself, and takes
  exactly the steps above. For a layered view, `RunDataStage` walks the layer tree depth first in document order (an
  explicit stack), visiting only the nodes on the way to a layer that is drawn: the **table of a node** is the table of
  the
  nearest `data` above it (the root's data is always loaded; a view's own `data` is loaded, once for each `data` object
  of the specification, with the temporal fields of every view that reads that table, `LayerTree.DataSites` and
  `TemporalFields.CollectAll(views, facets)`, and replaces the inherited table; a table is shared by the views that
  inherit it or declare the same `data` object, never copied), then the
  node's own `transform` array runs on it (`RowTransforms.Apply`) **before** the nodes below read the table, so a
  top-level `calculate` runs once and is seen by every layer; a **facet** then splits that table into its cells
  (`EncodingTransforms.Facet` with the view counts of `LayerTree.FacetViews`, then `LayerTree.InstantiateFacet`: the
  template below the facet is replaced by a copy for each cell) and the walk goes on into the copies, each with the rows
  of its cell as the inherited table (a copy whose view has `data` of its own reads that data instead: it is not
  partitioned; the cells are those of the facet's own table); a facet is visited and instantiated also when no mark
  below it is drawn (its cells are drawn empty), and the copies of the units that are drawn join the nodes that the walk
  builds a layer for; a layer then plans and runs its implicit bin, aggregate, and stack (`EncodingPlan.Create` over the
  layer's merged unit view and `EncodingTransforms.Apply`). One loader, one evaluator, one parameter set, and one
  `TransformContext` serve the whole call, and the findings keep the order (loader, then transforms); the composite
  marks' expansion supplies its derived specification tree to the data stage, so the expressions it wrote are evaluated
  like those of the specification. `ScaleResolver.ResolveLayers` then resolves the scales over all the layers; the
  discrete size hints come from the **merged** scales (the combined categories times the step), and a
  `vl-layer-scale-conflict` warning is delivered after the data warnings. `BuildChart` builds the legends with
  `LegendPlan.Create(LegendLayers(...))` (a `LegendLayer` for each layer, its scope function from the `resolve` of the
  legend and the scale groups, so independent legends and scales give one legend for each) and the axes from `PlanAxes`:
  for each position channel the layers are grouped by (scale group, highest ancestor not making the axis independent,
  explicit `axis.orient`), each group is one `AxisPlan` (the scale of its first member; the title merges the titles of
  the members with `GuideTitle.Merge`, as Vega-Lite's `mergeAxisComponent` does: a written title - `axis.title`, else
  the `title` of the channel definition - wins over one derived from the field, derived titles and different written
  titles join as `a, b`; a written result is set as the `title` of the chosen definition with its `axis.title` removed,
  a derived one is the title of the scale; and the definition that decides the axis properties: the first that switches
  the axis off, else the first with an `axis` object, else the first with a field or aggregate); several axes of one
  channel follow Vega-Lite's `parseLayerAxes` (`SeparateAxes`: a second axis moves to the opposite side when more axes
  are on its side than on the other, counting the side before the move; only the first axis keeps its grid; an explicit
  `axis.orient` or `axis.grid` wins). The marks are built once for each drawn layer into its own `marks` group, in layer
  order, between the axes behind and in front of the marks; each `MarkBuilder` call reads the layer's own scales and
  table. A layer with a mark that is not drawn (`geoshape`, `image`) is reported by the catalog and not drawn; the other
  layers are. A layered view with no layer draws its title and background only.
- **Cells** (phase 11, `VegaLiteDocument.Composition.cs`). A cell is the guides and the marks of one unit or layered view
  for a data rectangle of a given size, built independently of the view that holds it. `CreateCell` gathers a cell's
  inputs (its layers, the axes planned for them, the legends planned for them, and the axis and legend builders);
  `BuildCellGuides` builds the axes (`BuildAxes`) and places the legends outside them (`BuildLegends`); `BuildCell` builds
  the content in Vega-Lite's order (grids, back axes, one marks group for each layer, front axes, legends) from guides
  that the caller passes or, for a cell of a composition whose size is fixed, builds them for the same rectangle.
  `PlanAxes` plans over all the layers of the tree and records, for each `AxisPlan`, the layers that share the axis
  (`Members`) and the view that owns it (`Owner`: the highest view that shares it, so that a composition keeps the axes
  that its cell owns and moves those that a facet shares to its headers); the second axis of a channel (`SeparateAxes`)
  is decided chart by chart (the group of plans of one `ChartRoot`), so a chart next to a layered chart keeps its first
  axis on the usual side. `LegendLayers` passes each layer's node so that
  `LegendPlan.Owners` knows the owner of each legend, and `BuildLegends` places legends against any bounds (a chart's
  axes, or the grid of a concatenation or facet that owns them). The refactoring left the output of a single or layered
  view unchanged (the scenes of 12 specifications are pinned by `VegaLiteRefactorSnapshotTests`, captured before the
  change).
- **Compositions** (phase 11, `BuildComposition` and the `Composer` of `VegaLiteDocument.Composition.cs`). The root of a
  concatenation (a `concat`, `hconcat`, `vconcat`, or the concatenations that a `repeat` of values, rows or columns
  normalizes to) has no data rectangle: `ViewLayout.Resolve(..., composition: true)` reads only the padding and the
  background, and the canvas is the bounds of the content plus the padding (`ViewLayout.Measure` with the content bounds
  as the guides of a root of size zero). The `Composer` is built from the drawn layers with their scales and plans the
  guides of the whole tree once: the axes (`PlanAxes`, each given to the chart that owns it) and, for each owner in
  `LegendPlan.Owners`, the legends of that owner over the layers below it (a chart's own legends go to its cell, those of
  a concatenation to the concatenation). It then builds the views of the tree **bottom up with an explicit stack**
  (children before parents, so the nesting cannot overflow the call stack): a **chart** (a unit or layered view that is
  a child of a concatenation or the root) resolves its own size (`ViewLayout.ResolveSize` with the step hints of its own
  scales, `LayeredScales.HintsFor`), builds its guides and marks as a cell (`BuildCellGuides`, `BuildCell`), places its
  title inside the cell (`ViewLayout.CellBounds`, which also expands the bounds of everything in the cell by the half of
  the stroke of the cell's border, as Vega does for a stroked group) and makes a group of the role `view` with the style
  of its `view`; a chart whose marks are not supported is an empty cell of the default size. A **concatenation** places
  the cells of its children with `CompositionLayout.Place` (its `columns`, `spacing`, `align`, `center`, `bounds`), adds
  the legends that it owns with `BuildLegends` against the bounds of the grid (a data rectangle of zero by zero, so a
  right legend is at `ceil(x2) + offset` from the top) and its title (`unitOrLayer: false`: anchored at the start, over
  everything it holds), and makes a group of the role `scope`. A **facet** (the `facet` operator, or a unit view with a
  `row`, `column` or `facet` channel) places the charts of its cells (`ComposeFacet`; one cell for each value of a
  wrapped field, or for each row value and column value, row major) with `CompositionLayout.Place` and the
  `CompositionHeaders` it builds, then adds its legends and its title as a concatenation does. The **cells** are built
  by the same `ComposeChart`: the title of the cell is the label of its value in a wrapped facet
  (`TitleBuilder.BuildHeaderBlock`, which replaces the title of the chart; the body of a faceted unit view has no title
  of its own), and the axes that the cells share (the plans whose owner is the facet) are built for every cell with
  `AxisRequest.InHeader` (`CellInputs.HeaderAxisPlans`, `CellGuides.HeaderAxes`): the cell keeps their grids and does
  not reach into the room of the axes, the axis groups are taken from the first cell of each column (the x axes: top
  into the column header, bottom into the column footer) and of each row (the y axes: left into the row header, right
  into the row footer; a cell builds, and is charged to the scene item limit for, the main part of the axes of the sides
  that it supplies only (`AxisRequest.KeepInHeader`: the first column for left and right, the first row for top and
  bottom) and its grids otherwise). The headers and footers are built by `TitleBuilder.BuildHeader` (one for each row
  and column,
  with the label of its value and the shared axes;
  `HeaderSettings.Read(config, header, channel, honoredOrientsOnly: true)` reads the header properties, so a side that
  the renderer does not draw is read as not given and the default side is used), the row and column titles by
  `BuildHeader` with the `title` part (the title of the header, else that of the field definition, else the field name;
  `null` switches it off) and ten pixels outside the headers for a `row` and `column` facet (Vega-Lite's layout offset;
  none for a wrapped facet, whose title is at the edge of the cells); a null value is labeled `null`. The layout of a
  facet is `bounds: full`, the `columns` of the `column` field (one for a `row` facet, the `columns` of a wrapped
  facet), and `align: all`, or `none` when the scale of the direction without a facet field is independent (the `align`
  of the specification wins). Every group, title, axis, legend and mark is charged to the scene budget before it is
  created, and the token is polled for each view. The shared axes are built for every cell (their grids are needed
  there), so the budget counts the axes of the cells whose headers are not drawn too.
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
  `ConfigResolver`, `ResolvedConfig`, `SpecNormalizer`, `LayerTree`, `LayerNode`
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
- **`Scales` subsystem** - `ScaleResolver` (the pipeline entry that takes the plan and the data, and `ResolveLayers`),
  `ResolvedScales`, `LayeredScales`
- **`Guides` subsystem** - `TextMeasurer`, `TitleBuilder`, `TitleBlock`, `AxisBuilder`, `AxisRequest`, `AxisResult`,
  `LegendPlan`, `LegendBuilder`, `LegendResult`
- **`Layout` subsystem** - `ViewLayout` and its request and measure records, and `LegendLayout`
- **`Marks` subsystem** - `MarkBuilder`
- **`Scenegraph` subsystem** - `Scene`, `SceneItem`, `SceneBudget`, `ScenePainter`, `ScenePlacement`
- **Core `CanvasNet`** - `Surface`, `Rgba32`, and `CssColor` (`ToCss` turns the caller's `Background` option into the
  scene's string) (public API); see _CanvasNetVegaLite System Design_
- **.NET base class library** - `System.IO`, `System.Text`, `System.Text.Json` (the configuration check),
  `System.Collections.ObjectModel`, `System.Globalization`

### Callers

The consumers are applications and libraries that render charts. Within this repository the `CanvasNetTool` system
(the `SourceLoader` unit of its `Imaging` subsystem) calls `Open`, reads the descriptive properties for `info`, and
calls `Measure` and `Render`, mapping the exceptions to its stable error codes. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/` call the public API and, through `InternalsVisibleTo`, the internal
pipeline types.
