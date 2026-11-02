## Data

![Data Structure](VegaLiteDataView.svg)

### Overview

The `Data` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Data`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Data/`) puts the rows of a chart into memory, safely. It is **internal**: its
types are visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the test assembly.
It reads **inline `values`**, **local data files** (JSON, CSV, TSV, and DSV) from a sandbox, **named datasets**, and
**generated sequences**, and applies the `parse` conversions; remote URLs are never fetched and are reported by the
`FeatureCatalog` unit. It also owns the **time model** of the system: the calendar, the time units, the date-time
objects, and the date formatting and parsing (a deviation from the master plan, which placed them in `Transforms` and
`Scales`: the parse directives need them here, and the `Expressions` subsystem, which must not depend on `Scales` or
`Transforms`, needs them too). It contains six units:

- **DataTable** - the internal `DataTable`, `DataValue`, `DataValueKind`, `DataObject`, `DataArray`, `JsNumber`, and
  `FieldPath` types: the immutable typed value and table model (with the array, object, and regular expression values that
  expressions and transforms produce) and the deterministic comparison and JavaScript-style formatting that the
  scales and axes need; see _DataTable Unit Design_ (`data/data-table.md`)
- **DataLoader** - the internal `DataLoader`: chooses the source of a view and builds its table under the data limits; it
  also keeps the row budget of the call for the derived tables of the row transforms and loads the sources of a `lookup`;
  see _DataLoader Unit Design_ (`data/data-loader.md`)
- **DataFileResolver** - the internal `DataFileResolver` and `DataFile` types: finds and reads local data files
  through the core `SandboxedFileResolver` (inside the sandbox, or through the caller's callback), and adds the search
  order, strict UTF-8 decoding, the file limit and the Vega-Lite wording of every refusal; see
  _DataFileResolver Unit Design_ (`data/data-file-resolver.md`)
- **DelimitedTextParser** - the internal `DelimitedTextParser`, `ParseDirectives`, `ParseFailures`, and the option and
  result records: CSV, TSV, and DSV text into rows, the `format.parse` conversions, and the per-column inference; see
  _DelimitedTextParser Unit Design_ (`data/delimited-text-parser.md`)
- **TimeCalendar** - the internal `TimeCalendar`, `DateParts`, `TimeInterval`, `TimeIntervalKind`,
  `TimeRangeLimitException`, `TimeUnitSpec`, and `DateTimeObject` types: ECMAScript time arithmetic over the years 1 to
  9999 (anything else is NaN), the caller's time zone (UTC by default, never the host zone) with the ECMAScript gap
  and overlap rule, the d3-time intervals, the Vega-Lite time units, and the `DateTime` object; see _TimeCalendar Unit
  Design_ (`data/time-calendar.md`)
- **TimeFormat** - the internal `TimeFormat`, `TimeFormatter`, `TimeFormatTooLongException`, and `JsDateParser` types:
  d3-time-format formatting and parsing with the English locale, Vega's multi-format, and JavaScript's `Date.parse`
  (the ISO format and a documented legacy subset), bounded and independent of the culture; see _TimeFormat Unit
  Design_ (`data/time-format.md`)

The subsystem's namespace documentation is in `Data/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Data-*` requirements in `docs/reqstream/canvas-net-vega-lite/data.yaml`.

### Interfaces

**Exposed** (internal only): the `DataLoader` constructor (source name, limits, the effective open options, the
top-level `datasets` node, the caller's datasets, a cancellation token, and the `TimeCalendar` that reads dates) with
`Load(SpecNode?, IReadOnlySet<string>?)` (the optional set is the implicit date fields of the view), the `Warnings` and
`RowsLoaded` properties, and the static `DataLoader.Load(SpecNode?, string?, VegaLiteLimits?, CancellationToken)` for
inline data and sequences only; each of them returns or builds a `DataTable`. Also the table's factories
`DataTable.Create`, `DataTable.FromRecords`, and `DataTable.Empty` and its typed accessors; the internal
`DataTable.FromSortedCells` (a table from a finished block of cells, used by the row transforms); the loader's
`ReserveDerivedRows` and `ReleaseRows` (the shared row budget) and `DataLoader.FromObject` (the conversion of a caller
value); and the value helpers
`DataValue`, `JsNumber`, and `FieldPath`. The time model is exposed as `TimeCalendar.Create(TimeZoneInfo?)` and
`TimeCalendar.Utc` with `ToParts` and `FromParts`, the `TimeInterval` objects of `TimeCalendar.Interval`, `TimeUnitSpec`
(`TryParse`, `Floor`, `End`, `Specifier`, `FieldName`, `ResolveMaxBins`), and `DateTimeObject` (`TryRead`,
`ToMilliseconds`, `FromScalar`); and the date text model as `TimeFormat.Compile`, `Multi`, and `TryParse`,
`TimeFormatter.Format`, and `JsDateParser.TryParse`. The resolver and parser units are used by the loader (and, for two
static rules, by the `SpecModel` units, see below) and are not called by any other subsystem.

**Consumed**: `SpecNode` and `SpecNodeKind` from the `SpecParser` unit (the time units and date-time objects read
their specification nodes) and `FeatureCatalog.IsSchemeBearingUrl` and
`FeatureReport` from the `FeatureCatalog` unit; the public `VegaLiteLimits`, `VegaLiteOpenOptions`,
`VegaLiteRenderOptions` datasets, `VegaLiteLocation`, and `VegaLiteRenderException` types of the `VegaLiteDocument`
unit. The .NET base class library supplies `System.IO` (files and streams), `System.Text` (strict UTF-8),
`System.Text.Json` (`JsonDocument`, for JSON data files), `System.Globalization`, `System.Numerics`,
`System.TimeZoneInfo` (the offsets of the caller's zone, and nothing of the host's zone), and
`System.Collections.Generic`.

**Consumers**: `VegaLiteDocument` (which creates one `DataLoader` per `Measure` or `Render` call for a single view with
a supported mark and reads its `Warnings`, and creates the `TimeCalendar` of the call from the `TimeZone` option), the
`Transforms` subsystem (`RowTransforms`, `AggregateTransforms`, and `EncodingTransforms` reserve and release rows in the
loader's budget, `RowTransforms` loads the source of a `lookup` through the same loader, and all of them build derived
tables; `ParameterEnvironment`
converts caller values with `DataLoader.FromObject`; `TimeUnitTransform` and `EncodingPlan` use `TimeUnitSpec` and the
calendar), the `Expressions` subsystem (which reads and produces `DataValue`
and `DataObject`, and whose date and time functions use the calendar, `TimeFormat`, and `JsDateParser`), `SpecParser`
and `FeatureCatalog` (which use `DelimitedTextParser.IsValidDelimiter`
and `ParseDirectives.TryClassify` so that validation, reports, and loading agree, and `TimeUnitSpec` and `DateTimeObject`
to validate time units and date-time objects), the `Scales` subsystem (`ScaleResolver`, `ScaleModel`, and `Ticks` read
`DataValue`, `DataTable`, and the calendar and its intervals), the `Guides` subsystem (`AxisBuilder`, `LegendBuilder`,
and `TimeLabels` read `DataValue` and format dates with `TimeFormat`), and the `Marks` subsystem (`MarkBuilder`).

### Design

**Choose the source, do the I/O once, never reach the network.** `DataLoader` picks the source of the `data` object by
precedence (inline `values`, then `url`, `sequence`, and `name`, the same precedence the feature catalog uses) and
returns an immutable table. Inline rows, named datasets, and sequences need no I/O. A `url` that has a scheme or is
absolute (remote data) is never given to the resolver and never opened: the table is empty and the catalog reports
`vl-data-remote-url`. A relative local `url` goes to `DataFileResolver`, which returns decoded text, and then to
`JsonDocument` or `DelimitedTextParser` depending on the format (`format.type`, else the extension). One loader, and
therefore one resolver cache, one file count, and one row budget, serves exactly one `Measure` or `Render`
call, including the sources of its
`lookup` transforms (which are read through `Load` of the same instance).

**Local files are hard errors; unsupported sources are reports.** A local file that is missing, refused by the sandbox,
a link, a directory where a file is expected, unreadable (an existing file whose open fails, which is never reported as
missing because only the open attempt itself decides that, or a callback or stream that fails), not
valid UTF-8, not valid JSON (including JSON text that is not valid Unicode, such as an escaped lone surrogate) or
delimited text, or over a limit, a `name` that no dataset defines, and a sequence that is
not valid (a missing or non-finite endpoint, or a zero, non-finite, or non-numeric step) are `VegaLiteRenderException`s
with the location of the `url`, `name`, or `sequence`, in
lenient and strict mode alike, because an empty chart would hide a broken deployment. What is only recognized and not
supported (remote data, text values, GeoJSON and TopoJSON, no source) is an empty table that the feature catalog
reports.

**Sandbox.** The resolver asks the core `SandboxedFileResolver` to confine every relative path to a root (the document
folder, then each data path, in order, or the logical root of the caller's callback), to refuse a symbolic link or
junction below a root (a dangling link included), and to read at most `MaxDataBytes` bytes; the resolver itself reads at
most `MaxDataFiles` distinct files, decodes strict UTF-8 and words each refusal; see _DataFileResolver Unit
Design_. With a callback, nothing touches the file system, and a callback that throws or returns a failing stream is the
same located read failure.

**No implicit type inference, as in Vega.** `DelimitedTextParser` reads the cells as text and its `ParseDirectives`
convert them only when the specification asks: delimited text (and JSON) is not inferred by default, so a leading zero
or an ISO-looking date stays text; `parse: "auto"` is the explicit opt-in to per-column inference (boolean, then number,
then date by the JavaScript `Date.parse` rules of `JsDateParser`, only when every non-empty text of the column fits); a
`parse` object converts only the named
fields; and an
explicit conversion never invents a value (a text that cannot be converted is null and counted into one
`vl-data-parse-failed` warning per field). A custom `date:'%fmt'` format is applied with `TimeFormat.TryParse` in the
calendar of the chart (`utc:'%fmt'` in UTC), and a plain `date` with `JsDateParser`. The one implicit exception is the
**temporal fields of the view** (`TemporalFields`, see _TimeUnitTransform_): the main data of a view parses them as dates
unless an explicit `parse` entry says otherwise (`parse: null` suppresses it), as Vega-Lite does, and a lookup source is
never implicitly parsed. Quantitative
encodings still coerce numeric text when they are drawn (`DataValue.TryGetQuantitative`), which is why an unparsed
CSV column still scales as numbers.

**One calendar, owned by the Data subsystem.** The time model is in this subsystem (plan decision P8-D1, a deviation
from the master plan) so that the parse directives, the expression functions, the transforms, the scales, and the
guides all read dates the same way. An instant is a number of milliseconds since the Unix epoch (a `DataValue` date), the
valid range is the years 1 to 9999, and anything else is NaN at the calendar level and null as a value. The zone is the
`TimeZone` render option only (UTC when it is null): the host's zone, clock, and culture are never read, a wall time
that the zone repeats or skips is resolved like ECMAScript, and the names and digits of every formatted date are the
English d3 locale and invariant ASCII; see _TimeCalendar Unit Design_ and _TimeFormat Unit Design_. The date text
parsers are bounded before any scan (128 characters for `Date.parse`, 256 for a format), so a 1 MiB cell costs no more
than a short one, and the parse directives poll the cancellation token every 4,096 rows.

**A small typed value model.** `DataValue` is a `null`, boolean, number, date, or string, or (as the result of an
expression or a transform, never of a data loader) an **array**, an **object**, or a **regular expression**, with value
equality (JavaScript's SameValueZero for the scalars and identity for the composites, so it can still be a dictionary
key), a total order (null, boolean, number, date, string, with the composites after them) used for
discrete domains, a quantitative reading (`TryGetQuantitative`: numbers, dates as milliseconds, booleans as 1 and 0, and
numeric strings), and a JavaScript-style text form (`ToText`) so that category labels and axis labels read as they do in
Vega. The composite payloads are immutable (`DataArray`, `DataObject` with ordered string keys, and an opaque regular
expression payload), and the struct stays 24 bytes. A date is an instant in UTC held as milliseconds since the epoch,
within the years 1 to 9999: ISO text beyond that
range is not a date (text under inference, null with a warning under an explicit conversion), and a caller's
`DateTime.MaxValue` or `DateTimeOffset.MaxValue` is clamped to the last supported millisecond. `JsNumber` reproduces JavaScript's
number-to-string conversion (the shortest round-trip digits) and the other number helpers that d3 relies on.

**Rows are columnar and flat.** A table has an ordered list of canonical field names and typed rows; nested objects and
arrays are flattened to leaf fields named by their canonical path (`p.q`, `p.r.0`, a literal dot in a key escaped as
`\.`), and a field a row lacks is null. `FieldPath` parses the Vega field syntax (dotted and bracketed paths with
escapes) so that an encoding's `field` finds its column. The loaders only ever produce scalar cells; the row transforms
store composite cells in derived tables (a calculated array or object, a looked-up row), and an expression that reads
`datum.name` rebuilds an array or an object from the flattened fields below `name` when the table has no such cell.

**Limits fail closed, before the memory is spent.** The row count is checked against `MaxDataRows` before any row is
converted or allocated (inline rows, a file, a dataset, a sequence, and a lookup source alike) and is a **total across
all the tables a call creates**: the loader's counter is the single budget of the call, the
output of a fold, a flatten, an aggregate, an impute, or an implicit aggregate is
reserved in it as it is built (while its input still counts), and the rows of a table that is no longer needed
(the input of a transform once its output is complete, the rows a filter or a sample drops) are given back; the field
count is checked against `MaxFieldCount` per row and for the union of all rows, a text
against `MaxStringLength`, a file's bytes against `MaxDataBytes`, and the number of files against `MaxDataFiles`; every
failure is a `VegaLiteRenderException` with the location of the offending data. Cancellation is checked every 1,024
rows and between files and chunks. Recursion while flattening a row is bounded by the nesting depth that the parser
accepted (at most 256) or, for a JSON file, by `MaxJsonDepth`. The data is loaded before the scene budget is charged,
so a data failure never depends on `MaxSceneItems`.
