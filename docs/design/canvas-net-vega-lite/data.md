## Data

![Data Structure](VegaLiteDataView.svg)

### Overview

The `Data` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Data`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Data/`) puts the rows of a chart into memory, safely. It is **internal**: its
types are visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the test assembly.
It reads **inline `values`**, **local data files** (JSON, CSV, TSV, and DSV) from a sandbox, **named datasets**, and
**generated sequences**, and applies the `parse` conversions; remote URLs are never fetched and are reported by the
`FeatureCatalog` unit. It contains four units:

- **DataTable** - the internal `DataTable`, `DataValue`, `DataValueKind`, `JsNumber`, and `FieldPath` types: the
  immutable typed value and table model with the deterministic comparison and JavaScript-style formatting that the
  scales and axes need; see _DataTable Unit Design_ (`data/data-table.md`)
- **DataLoader** - the internal `DataLoader`: chooses the source of a view and builds its table under the data limits;
  see _DataLoader Unit Design_ (`data/data-loader.md`)
- **DataFileResolver** - the internal `DataFileResolver` and `DataFile` types: finds and reads local data files
  inside the sandbox (or through the caller's callback), as strict bounded UTF-8, with a per-call cache and the file
  limits; see _DataFileResolver Unit Design_ (`data/data-file-resolver.md`)
- **DelimitedTextParser** - the internal `DelimitedTextParser`, `ParseDirectives`, `ParseFailures`, and the option and
  result records: CSV, TSV, and DSV text into rows, the `format.parse` conversions, and the per-column inference; see
  _DelimitedTextParser Unit Design_ (`data/delimited-text-parser.md`)

The subsystem's namespace documentation is in `Data/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Data-*` requirements in `docs/reqstream/canvas-net-vega-lite/data.yaml`.

### Interfaces

**Exposed** (internal only): the `DataLoader` constructor (source name, limits, the effective open options, the
top-level `datasets` node, the caller's datasets, a cancellation token) with `Load(SpecNode?)`, the `Warnings` and
`RowsLoaded` properties, and the static `DataLoader.Load(SpecNode?, string?, VegaLiteLimits?, CancellationToken)` for
inline data and sequences only; each of them returns or builds a `DataTable`. Also the table's factories
`DataTable.Create`, `DataTable.FromRecords`, and `DataTable.Empty` and its typed accessors; and the value helpers
`DataValue`, `JsNumber`, and `FieldPath`. The resolver and parser units are used by the loader (and, for two static
rules, by the `SpecModel` units, see below) and are not called by any other subsystem.

**Consumed**: `SpecNode` and `SpecNodeKind` from the `SpecParser` unit and `FeatureCatalog.IsSchemeBearingUrl` and
`FeatureReport` from the `FeatureCatalog` unit; the public `VegaLiteLimits`, `VegaLiteOpenOptions`,
`VegaLiteRenderOptions` datasets, `VegaLiteLocation`, and `VegaLiteRenderException` types of the `VegaLiteDocument`
unit. The .NET base class library supplies `System.IO` (files and streams), `System.Text` (strict UTF-8),
`System.Text.Json` (`JsonDocument`, for JSON data files), `System.Globalization`, `System.Numerics`, and
`System.Collections.Generic`.

**Consumers**: `VegaLiteDocument` (which creates one `DataLoader` per `Measure` or `Render` call for a single view with
a supported mark and reads its `Warnings`), `SpecParser` and `FeatureCatalog` (which use `DelimitedTextParser.IsValidDelimiter`
and `ParseDirectives.TryClassify` so that validation, reports, and loading agree), the `Scales` subsystem
(`ScaleResolver`, `ScaleModel`, and `AxisBuilder` read `DataValue` and `DataTable`), the `Guides` subsystem
(`AxisBuilder`), and the `Marks` subsystem (`MarkBuilder`).

### Design

**Choose the source, do the I/O once, never reach the network.** `DataLoader` picks the source of the `data` object by
precedence (inline `values`, then `url`, `sequence`, and `name`, the same precedence the feature catalog uses) and
returns an immutable table. Inline rows, named datasets, and sequences need no I/O. A `url` that has a scheme or is
absolute (remote data) is never given to the resolver and never opened: the table is empty and the catalog reports
`vl-data-remote-url`. A relative local `url` goes to `DataFileResolver`, which returns decoded text, and then to
`JsonDocument` or `DelimitedTextParser` depending on the format (`format.type`, else the extension). One loader, and
therefore one resolver cache, one file count, and one row budget, serves exactly one `Measure` or `Render` call.

**Local files are hard errors; unsupported sources are reports.** A local file that is missing, refused by the sandbox,
a link, not valid UTF-8, not valid JSON or delimited text, or over a limit, a `name` that no dataset defines, and a
sequence that is not valid are `VegaLiteRenderException`s with the location of the `url`, `name`, or `sequence`, in
lenient and strict mode alike, because an empty chart would hide a broken deployment. What is only recognized and not
supported (remote data, text values, GeoJSON and TopoJSON, no source) is an empty table that the feature catalog
reports.

**Sandbox.** The resolver confines every relative path to a root (the document folder, then each data path, in
order, or the logical root of the caller's callback), refuses a symbolic link or junction below a root, reads at most
`MaxDataBytes` bytes of at most `MaxDataFiles` distinct files, and decodes strict UTF-8; see _DataFileResolver Unit
Design_. With a callback, nothing touches the file system.

**Types by column, as Vega does.** `DelimitedTextParser` reads the cells as text and its `ParseDirectives`
convert them: delimited text is inferred per column by default (boolean, then number, then ISO 8601 date, only when
every non-empty text of the column fits), JSON keeps its types unless `parse` is `"auto"` or names fields, and an
explicit conversion never invents a value (a text that cannot be converted is null and counted into one
`vl-data-parse-failed` warning per field). Custom `date:'%fmt'` formats are recognized and not applied.

**A small typed value model.** `DataValue` is a `null`, boolean, number, date, or string with value equality
(JavaScript's SameValueZero, so it can be a dictionary key), a total order (null, boolean, number, date, string) used for
discrete domains, a quantitative reading (`TryGetQuantitative`: numbers, dates as milliseconds, booleans as 1 and 0, and
numeric strings), and a JavaScript-style text form (`ToText`) so that category labels and axis labels read as they do in
Vega. A date is an instant in UTC held as milliseconds since the epoch. `JsNumber` reproduces JavaScript's
number-to-string conversion (the shortest round-trip digits) and the other number helpers that d3 relies on.

**Rows are columnar and flat.** A table has an ordered list of canonical field names and typed rows; nested objects and
arrays are flattened to leaf fields named by their canonical path (`p.q`, `p.r.0`, a literal dot in a key escaped as
`\.`), and a field a row lacks is null. `FieldPath` parses the Vega field syntax (dotted and bracketed paths with
escapes) so that an encoding's `field` finds its column.

**Limits fail closed, before the memory is spent.** The row count is checked against `MaxDataRows` before any row is
converted or allocated (inline rows, a file, a dataset, and a sequence alike) and is a **total across all the tables
a call creates**; the field count is checked against `MaxFieldCount` per row and for the union of all rows, a text
against `MaxStringLength`, a file's bytes against `MaxDataBytes`, and the number of files against `MaxDataFiles`; every
failure is a `VegaLiteRenderException` with the location of the offending data. Cancellation is checked every 1,024
rows and between files and chunks. Recursion while flattening a row is bounded by the nesting depth that the parser
accepted (at most 256) or, for a JSON file, by `MaxJsonDepth`. The data is loaded before the scene budget is charged,
so a data failure never depends on `MaxSceneItems`.
