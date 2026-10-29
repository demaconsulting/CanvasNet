### DataLoader

![Data Structure](VegaLiteDataView.svg)

The `DataLoader` unit is the second software unit in the `Data` subsystem. It is one source file, `DataLoader.cs`, in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace, and defines one `internal` type, the sealed class `DataLoader`.

#### Purpose

`DataLoader` reads the data of a view from the `data` property of its specification: inline `values` rows, a local data
file, a named dataset, or a generated sequence, under the data limits and the sandbox of one measure or render call.
It exists so that the rest of the pipeline sees every kind of data source as a `DataTable` and so that no other unit
needs to know how the rows were written. It never uses the network; all file access goes through `DataFileResolver`.

The unit's behavior is specified by the `CanvasNetVegaLite-Data-DataLoader-*` requirements in
`docs/reqstream/canvas-net-vega-lite/data/data-loader.yaml`.

#### Data Model

One instance serves one call and is not thread-safe. It holds the source name, the limits, the cancellation token, its
`DataFileResolver`, the top-level `datasets` node, the caller's datasets, the list of warnings, and the running count
of rows created (`RowsLoaded`). It defines the constants `PrimitiveField` (`"data"`, the field that holds the value of
a row when the values are primitives, and the default field of a sequence) and the cancellation interval (1,024
rows). Its input is the `data` node of a view (`SpecNode?`); its output is a `DataTable` and the `Warnings`
(`vl-data-parse-failed` reports, one per field and type).

**Source precedence.** The source is chosen by precedence, not by the order of the keys: inline `values`, then `url`,
then `sequence`, then `name`. Anything else (no `data`, no source, or `values` that is not an array) is the empty table.

**Row rules.** An array of objects gives one row per object. An array of primitives (or of arrays) gives one row per
value with the value in the field `data`. The JSON types are kept (number, string, boolean, null). A nested object or
array is flattened to one field per leaf, named by the canonical path of the leaf (`FieldPath.Join`, which escapes a
literal dot), and a field a row does not have is null. The same rules apply to a JSON data file, a top-level dataset, and
a dataset of the caller. A caller's `DateTime` (local converted to UTC, unspecified taken as UTC) and `DateTimeOffset`
values become dates in UTC; `DataLoader.FromDateTime` clamps a value past the last supported whole millisecond of the
year 9999 - which is exactly `DateTime.MaxValue` and `DateTimeOffset.MaxValue`, the usual "no end" sentinels, whose
sub-millisecond ticks the whole-millisecond date model cannot hold - to that last millisecond (`9999-12-31T23:59:59.999Z`)
instead of failing, and reports any other unexpected failure to represent a date as a `VegaLiteRenderException`
located at the data object (`FromObject` receives the location for that purpose).

**Format.** For a local `url`, `format.type` decides (`json`, `csv`, `tsv`, `dsv`); without it the extension of the
path does (`.csv`, `.tsv`, in any case; anything else is JSON). A DSV uses the single-character `format.delimiter` (a
comma when omitted). `format.property` selects the array of rows inside a JSON document by a dotted field path. The
`format.type` `topojson` is the empty table (reported by the catalog).

**Sequence.** `start` and `stop` are required finite numbers, `step` is 1 when it is absent and otherwise must be a
finite number other than zero (a present step that is not a number is an error, never silently 1), and `as` names the
field (`data` by default). The values are `start + i * step` for `i` from 0 up to, excluding, `stop` (a negative step
counts down); each is computed from its index, so rounding does not drift. The length is `ceil((stop - start) / step)`
in floating point: a quotient that is not positive (a step that points away from `stop`, including a positive step
with `stop` below `start`) is an empty table, and a difference or quotient that overflows to infinity, or any length
above the remaining `MaxDataRows` budget, fails the row limit before anything is allocated (the message describes an
unrepresentable count instead of printing it); the count is converted to an integer only after that check.

**Parse default.** As in Vega, no data-based type inference happens unless the specification asks for it. For CSV, TSV,
and DSV an absent `format.parse` and `parse: null` keep every cell as a string (so `01234`, `2024-03-05`, and `true`
stay text); `parse: "auto"` is the explicit opt-in to per-column inference of booleans, numbers, and ISO dates; a
`parse` object converts only the fields it names. The loader therefore always reads the directives with
`defaultAuto: false`. A quantitative encoding still coerces numeric text to a number when it is drawn (`DataValue`
parses it lazily for linear domains, positions, size, and opacity), which is a property of the encodings and not a
parse mode; a quantitative color encoding is not drawn by the current subset. Date parsing of text for temporal
encodings and time support arrive in Phase 8.

**Named data.** A `name` is looked up in the caller's datasets (`VegaLiteRenderOptions.Datasets`) first and then in
the top-level `datasets` of the specification; the caller wins; neither defining it is an error.

**Remote data.** A `url` for which `FeatureCatalog.IsSchemeBearingUrl` is true (a scheme, an absolute or rooted path, a
drive path, or a network path) is the empty table and is never given to the resolver.

#### Key Methods

- `DataLoader(sourceName, limits, openOptions, specDatasets, callerDatasets, cancellationToken, isLink)` - creates the
  loader and its `DataFileResolver` (the last argument is a test seam for the link probe).
- `Load(SpecNode? data)` - returns `DataTable.Empty` unless `data` is an object; selects the source by precedence and
  calls the loader of that source. The static `Load(data, sourceName, limits, token)` is the inline-only convenience
  (a source that needs a file or a named dataset fails there).
- `LoadSpecRows` - checks the row count against the total budget (`Charge`) before any row is converted, flattens each
  row into a record while checking the field count of the row and the token every 1,024 rows, applies the `parse`
  directives, and builds the table with `DataTable.FromRecords`.
- `LoadUrl` - classifies remote data, finds the type, asks the resolver for the file, and routes to `LoadDelimited`
  (the parser unit) or `LoadJson` (`JsonDocument` under `MaxJsonDepth`, then `ReadJsonRecords`: `format.property`,
  `Charge`, flatten; then parse). `ReadJsonRecords` is the one boundary around every element access of the rows: the
  JSON parser accepts an escaped lone surrogate (such as `\ud800`) and only the later read of a string, a property
  name, or a property lookup throws a raw `InvalidOperationException`, so that exception (unless it already is a
  located `VegaLiteRenderException`, which derives from it) becomes a located failure saying that the data contains
  text that is not valid Unicode. `JsonException` keeps its own line and position message. Caller datasets are managed
  objects, not JSON, and need no such path.
- `LoadNamed`, `LoadCallerRows`, and `LoadSequence` - the named-dataset and sequence sources; `LoadSequence` validates
  the endpoints and the step, computes the length with overflow-safe arithmetic, charges it before anything is
  allocated, and generates the values lazily.
- `InferFormatType(path)` - `csv`, `tsv`, or `json` by extension.
- `Complete` - builds the table, adds its rows to `RowsLoaded`, and converts the parse failures into warnings.
- `Charge`, `Add`, and `CheckFieldCount` - enforce `MaxDataRows` as a total, `MaxFieldCount` per row, and the string
  length of JSON values at the leaf's location.

**Preconditions and postconditions**: the `data` node, when present, was validated by `SpecParser`; on return the table
is complete and within the limits, the files it read are counted and cached for the rest of the call, and the warnings
list is complete for this call. Root-level data inheritance for layered views is the caller's job (it passes the right
`data` node); only the root view's data is loaded so far.

#### Error Handling

Every failure of the data is a `VegaLiteRenderException` located at the construct that caused it, in lenient and strict
mode alike: a file that the resolver refuses or cannot find or read (the resolver's exceptions are propagated, and the
resolver also reports a failing callback or stream as the located read failure), invalid
JSON or JSON nested deeper than `MaxJsonDepth`, a JSON root that is not an array or a `format.property` that is not
found, JSON text that is not valid Unicode (an escaped lone surrogate in a string or a property name), a delimiter that
is not one valid character, delimited text that is malformed (located by the parser), a
`name` that is not defined, a sequence without a finite numeric `start` and `stop`, with a zero, non-finite, or
non-numeric `step`, or with a value outside the range of a number, more rows than `MaxDataRows` (in total, also for an
overflowing sequence length), more fields in a row than `MaxFieldCount`, a
string longer than `MaxStringLength`, and a number outside the range of a double. A canceled token is an
`OperationCanceledException`, never wrapped. Values that an explicit `parse` could not convert are not errors: they are
null and reported as `vl-data-parse-failed` warnings. Sources that are only recognized and not supported (remote data, `values`
given as text or as an object, a topojson format, no source) are not errors: the table is empty and the `FeatureCatalog`
reports the reason (`vl-data-empty`, `vl-data-remote-url`, `vl-data-values-text`, `vl-data-geo`), so the rest of the
chart is still drawn.

#### Dependencies

- **`DataFileResolver`** and **`DelimitedTextParser`** (with `ParseDirectives` and `ParseFailures`) - the other units of
  the `Data` subsystem
- **`DataTable`**, **`DataValue`**, **`FieldPath`** - the value and table model of the `DataTable` unit
- **`SpecNode`**, **`SpecNodeKind`**, **`FeatureReport`**, and **`FeatureCatalog.IsSchemeBearingUrl`** - the tree of the
  `SpecParser` unit and the report type and remote-URL rule of the `FeatureCatalog` unit
- **`VegaLiteLimits`**, **`VegaLiteOpenOptions`**, **`VegaLiteLocation`**, and **`VegaLiteRenderException`** - the
  public types of the `VegaLiteDocument` unit
- **.NET base class library** - `System.Text.Json` (JSON data files), `System.Globalization` (invariant-culture array
  indices in field names and messages)

#### Callers

`VegaLiteDocument` creates one loader per `Measure` or `Render` call for a single view with a supported mark, calls
`Load`, and delivers the `Warnings` after the reports of the specification. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataLoaderTests.cs` call it directly through `InternalsVisibleTo`.
