# CanvasNet Vega-Lite Package

## API Reference

### VegaLiteDocument

`VegaLiteDocument` (namespace `DemaConsulting.CanvasNet.VegaLite`) is distributed via the separate
`DemaConsulting.CanvasNet.VegaLite` NuGet package, whose only dependency is the core `DemaConsulting.CanvasNet`
package - see the Installation section of the project README. It is the single entry point for Vega-Lite
specifications and draws them as **static pictures**: a chart is a still image, so interaction (tooltips, links,
selections, bindings) has no meaning. The package implements a **defined subset** of the public Vega-Lite language
(see *Supported Vega-Lite Subset*); it does not imply general Vega-Lite support, and everything outside the subset is
reported, never skipped silently. It is derived with attribution from the Vega-Lite, Vega, and d3 projects and is not
affiliated with or endorsed by them (see *Vega-Lite Third-Party Attribution*). A document is **immutable and
thread-safe**: it is created from specification text (JSON), validated once, and can then be measured and rendered any
number of times, from several threads at once, each call giving the same result for the same options.

```csharp
public sealed partial class VegaLiteDocument
{
    public static VegaLiteDocument Parse(string json, VegaLiteOpenOptions? options = null);
    public static VegaLiteDocument Open(string path, VegaLiteOpenOptions? options = null);
    public static VegaLiteDocument Open(Stream stream, VegaLiteOpenOptions? options = null);

    public string? SourceName { get; }
    public string? SchemaUrl { get; }
    public int? SchemaMajorVersion { get; }
    public string? Title { get; }
    public IReadOnlyList<string> MarkTypes { get; }
    public IReadOnlyList<VegaLiteDataReference> DataReferences { get; }
    public IReadOnlyList<string> ParameterNames { get; }

    public VegaLiteSize Measure(
        VegaLiteRenderOptions? options = null,
        CancellationToken cancellationToken = default);

    public Surface Render(
        VegaLiteRenderOptions? options = null,
        CancellationToken cancellationToken = default);

    public Surface Render(
        int width,
        int height,
        VegaLiteRenderOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

- `Parse` and the two `Open` overloads read UTF-8 text (at most 4,194,304 UTF-16 characters by default; a leading byte
  order mark is ignored; invalid UTF-8, or a string with an unpaired surrogate, is rejected rather than replaced) and
  throw a `VegaLiteSpecException` with the location of the first problem when the text is not valid JSON, is nested too
  deeply, has a known property of the wrong type or value, or holds an invalid expression or parameter (see
  *Vega-Lite Expressions* and *Vega-Lite Parameters*). Plain **Vega** specifications (a `$schema` under
  `vega.github.io/schema/vega/`) are rejected. `Open(string)` names the source by the path; `Open(Stream)` reads from
  the current position and leaves the stream open. Opening never loads data, never opens another file, never uses
  the network, and never evaluates an expression (it only compiles them); only `Measure` and `Render` read data, as
  described in *Vega-Lite Data*, and run expressions.
- The descriptive properties (`SchemaUrl`, `SchemaMajorVersion`, `Title`, `MarkTypes`, `DataReferences`,
  `ParameterNames`) describe the text only: nothing is loaded or evaluated, so they are cheap and safe to read for a
  file you do not trust. A `VegaLiteDataReference` records the `Kind` of a data source as written (`Inline`, `Url`,
  `Name`, or `Sequence`), its `Url`, the `RowCount` when the text gives it, and its `Location`.
- `Measure` computes the size in pixels that `Render` produces for the same options, without drawing anything. It
  runs the same checks and delivers the same reports, so it is the cheap way to learn the size of a chart.
- `Render()` draws the chart onto a new `Surface` at its **natural size** (its `width` and `height` plus its axes,
  title, and padding) multiplied by `VegaLiteRenderOptions.Scale`. The chart is drawn sharply at the final size, not
  enlarged afterwards. The caller owns, and must dispose, the returned surface.
- `Render(width, height)` draws the chart onto a new surface of exactly `width` by `height` pixels: the natural chart
  is scaled uniformly to fit and centered, and the margins that remain take the background. `Scale` is not used,
  because the size decides the scale.

**Exceptions** (`Measure` and both `Render` overloads):

- `ArgumentOutOfRangeException`: Thrown, before any work, when the explicit size or the scaled natural size is outside
  1 to `Surface.MaxDimension` on a side.
- `ArgumentException`: Thrown when `VegaLiteRenderOptions.Parameters` names a parameter that the document does not
  declare.
- `VegaLiteSpecException`: Thrown, with the location of the construct, when an axis, scale, or format property has an
  invalid value that can only be found while laying out (for example an unknown axis orientation or a malformed d3
  format).
- `VegaLiteRenderException`: Thrown, with the location of the construct, when the data, scale, axis, or scene cannot
  be built or a limit of `VegaLiteLimits` is exceeded. An expression or transform that fails while it runs (the
  evaluation budget, a string limit, a regular expression, a row, field, or cell limit of a derived table) is this
  exception, located at the expression or the transform (see *Vega-Lite Expressions* and *Vega-Lite Transforms*). A
  local data file that is missing, refused by the sandbox,
  behind a link, not valid UTF-8, or not valid JSON or delimited text, a `data.name` that nobody defines, and a
  document with no place to look for a local data file are all this exception, in lenient and strict mode alike (see
  *Vega-Lite Data*).
- `VegaLiteUnsupportedFeatureException`: Thrown at the first unsupported feature when
  `ThrowOnUnsupportedFeature` is set (see *Vega-Lite Unsupported Features and Strict Mode*).
- `OperationCanceledException`: Thrown, never wrapped, when the token is canceled before or during the work.
- `InvalidDataException`: Thrown by `Parse` and `Open`, without a location, when the text is longer than
  `MaxSourceLength`.

The locations carried by the exceptions and reports are `VegaLiteLocation` values: the `File` (the source name), the
RFC 6901 JSON `Pointer` of the construct, and its one-based `Line` and `Column` (counted in UTF-16 code units, with
`\n` as the only line terminator, so they are the same on every platform). `ToString()` gives
`file:line:column /pointer`, the form editors and build tools recognize.

### VegaLiteOpenOptions and VegaLiteRenderOptions

Both classes are immutable; every property is init-only and validated when set (fail-closed), collections are copied
and exposed read-only, so one instance can be shared between threads and calls.

<!-- markdownlint-disable MD013 -->
| `VegaLiteOpenOptions` property | Default | Meaning |
| ------------------------------ | ------- | ------- |
| `SourceName` | `null` (the path for `Open(string)`) | The name shown in locations and messages |
| `Limits` | `null` (`VegaLiteLimits.Default`) | The resource limits of parsing (the source length, the JSON depth, and the length and nesting depth of every expression) |
| `BaseDirectory` | `null` (the folder of the file for `Open(string)`) | The folder that a relative data `url` is looked up in first. A document opened from a string or a stream has none unless you set it. The current directory is never used |
| `DataPaths` | empty | More folders that a relative data `url` is looked up in, in order, after `BaseDirectory`; copied when set |
| `FileResolver` | `null` | A `Func<string, Stream?>` callback that replaces the file system: it receives a normalized logical path (forward slashes, no leading slash) and returns a readable stream of UTF-8 text, or `null` when the file does not exist. When it is set the file system is never touched. The library disposes the stream |
<!-- markdownlint-enable MD013 -->

<!-- markdownlint-disable MD013 -->
| `VegaLiteRenderOptions` property | Default | Meaning |
| -------------------------------- | ------- | ------- |
| `Scale` | 1 | The factor by which the natural size is multiplied (text included, so a larger scale gives a larger, equally sharp image); finite, greater than zero. The scaled size must lie within 1 and `Surface.MaxDimension` |
| `Background` | `null` | An `Rgba32` that replaces the background of the specification and of the configuration; `null` uses them (white when neither defines one). `new Rgba32(0, 0, 0, 0)` gives a transparent picture |
| `Config` | `null` | A Vega-Lite configuration as JSON object text, merged over the built-in defaults and the `config` of the specification (the caller wins). This is how a host applies a theme without editing the specification |
| `ContainerWidth`, `ContainerHeight` | 400, 300 | The size in pixels of a chart whose `width` or `height` is `"container"`, which is fitted to that size (see *Vega-Lite Autosize and Container Sizes*) |
| `Fonts` | `null` | A `VegaLiteFontOptions`: `UseSystemFonts` (default `false`) and `AdditionalDirectories` (default empty; copied and validated when set). See *Time Zone, Fonts, and Determinism* |
| `TimeZone` | `null` (UTC) | The only time zone the chart ever uses: a date-time without an offset in the data, a custom `date:` parse format, the local time units, the local date functions of expressions, and the ticks and labels of time scales are read in it. `null` is UTC, never the zone of the machine; the `utc` time units, scales, and functions always use UTC. See *Vega-Lite Time and Time Zones* |
| `Parameters` | `null` | Caller-supplied values that **replace the definition** of the named parameters of the specification, validated and copied when set (see *Vega-Lite Parameters*). A name that the document does not declare (`ParameterNames`) is an `ArgumentException` when the chart is measured or rendered. Values are `null`, `bool`, `string`, finite numbers, `DateTime`, `DateTimeOffset`, or a **structured value**: a sequence (an array or a list) or a string-keyed dictionary (`IReadOnlyDictionary<string, object?>` or `IDictionary<string, object?>`) of such values, nested up to 32 levels and holding at most 65,536 values; a structured value is how the host gives a parameter that holds an array, such as the domain of a scale |
| `Datasets` | `null` | Named tables of rows for a `data.name`, copied when set; a caller dataset **replaces** a dataset of the same name in the specification's top-level `datasets`. Each row is a dictionary of field name to a value that is `null`, `bool`, `string`, a finite number, a `DateTime` (UTC; an unspecified kind is taken as UTC), or a `DateTimeOffset` (see *Vega-Lite Data*) |
| `Limits` | `VegaLiteLimits.Default` | The resource limits of the call (see below) |
| `WarningHandler` | `null` | Receives each `VegaLiteDiagnostic` (a warning); `null` drops them |
| `UnsupportedFeatureHandler` | `null` | Receives each `VegaLiteUnsupportedFeature`; `null` drops them |
| `ThrowOnUnsupportedFeature` | `false` | Turns the first unsupported feature into a `VegaLiteUnsupportedFeatureException` (strict mode) |
<!-- markdownlint-enable MD013 -->

`VegaLiteLimits` bounds every resource of a call. Its sixteen properties are init-only, each at least 1 (the depth
limits have an upper ceiling too), and the defaults are `MaxSourceLength` 4,194,304 characters, `MaxJsonDepth` 64,
`MaxDataFiles` 64, `MaxDataBytes` 64 MiB, `MaxDataRows` 1,000,000, `MaxFieldCount` 1,024, `MaxViews` 1,000,
`MaxCompositionDepth` 16, `MaxSceneItems` 2,000,000, `MaxScaleDomainSize` 100,000, `MaxTicks` 1,000,
`MaxExpressionLength` 4,096 (the expressions that you write), `MaxExpressionDepth` 64, `MaxEvaluationSteps` 50,000,000,
`MaxStringLength` 1,048,576, and
`MaxDiagnostics` 1,000 (the first report beyond it becomes a single `vl-diagnostics-suppressed` warning). `MaxViews`
and `MaxCompositionDepth` are checked when the views of a specification are normalized, before any data is read:
`MaxViews` counts the top-level view, every layer (nested ones too), every child of a concatenation, every copy of a
repeat (checked before the copies are made), every cell of a facet (checked before the cells are made: the number of
cells
times the views of one cell), and the views that a composite mark stands for (a default `boxplot` is 9 views, a default
`errorbar` 3; see *Vega-Lite Layers and Composite Marks* and *Vega-Lite Composition*), and `MaxCompositionDepth` the
levels of `layer`, `concat`, `hconcat`, `vconcat`, `facet`, and `repeat` inside one another (the outermost one is level
1, a `vconcat` of an `hconcat` of charts is 2; a chart itself and the views of a composite mark are not levels).
Exceeding either is a `VegaLiteRenderException` located at the offending view, `layer`, `repeat`, or facet field. The
data,
scale, tick, and scene limits are checked **before** the memory is allocated, so a hostile specification fails with a
located `VegaLiteRenderException` instead of exhausting memory. `MaxSceneItems` counts every item of the scene, groups
included (the root and view groups, the title and its texts, each axis with its grid, ticks, labels, domain and title,
the marks group, and each mark), and an item is counted before it is created, so a limit of N allows a chart of exactly
N items and fails on the next one; tick labels that are dropped because they overlap are not items and cost nothing. The
data limits apply to each `Measure` or `Render` call: `MaxDataRows` is a **total** across every table that the call
creates (inline rows, files, datasets, and sequences), `MaxDataFiles` counts the distinct files read, `MaxDataBytes` is
the size of each file, and `MaxFieldCount` is the number of fields of a row (the columns of a file). The data is read
and checked before any scene item is counted. `MaxExpressionLength` (for the expressions that you write: `calculate`, a
`filter` text, a `labelExpr`, an `{"expr": ...}`) and `MaxExpressionDepth` are checked when the
document is opened (with `VegaLiteOpenOptions.Limits`); `MaxEvaluationSteps` is **one budget per measure or render
call** that the parameters, the transforms, and the row-bound mark values share, and `MaxStringLength` also bounds every
text that an expression builds. The fields and rows that the row transforms derive count against `MaxFieldCount` and
`MaxDataRows` as well (see *Vega-Lite Expressions* and *Vega-Lite Transforms* for the details).

### Vega-Lite Data

A chart's rows can come from five sources. The source is chosen by precedence, not by the order of the keys: inline
`values`, then `url`, then `sequence`, then `name`. Everything is read while a chart is **measured or rendered**;
opening a document never reads a data file.

<!-- markdownlint-disable MD013 -->
| Source | Written as | Notes |
| ------ | ---------- | ----- |
| Inline rows | `"data": {"values": [{...}, ...]}` | An array of objects (one row each) or of primitives (one row `{"data": value}`); nested objects and arrays are flattened to fields named by their path (`a.b`, `p.r.0`) |
| A local file | `"data": {"url": "rows.csv"}` | A **relative** path only; see *Data files* and *The sandbox* below |
| A named dataset | `"data": {"name": "rows"}` | The caller's `VegaLiteRenderOptions.Datasets` first, then the top-level `datasets` of the specification; the caller wins. A name that nobody defines is an error |
| A sequence | `"data": {"sequence": {"start": 0, "stop": 5, "step": 1, "as": "x"}}` | The numbers from `start` up to, **not including**, `stop` in steps of `step` (1 by default; negative counts down) in the field `as` (`data` by default). `start` and `stop` must be finite numbers; a step that is present must be a finite number other than zero (an absent step is 1), and a step that points away from `stop` gives no rows. A missing, non-finite, or zero value fails the render, and so does a sequence longer than `MaxDataRows` |
<!-- markdownlint-enable MD013 -->

**Data files.** The format is `format.type` (`json`, `csv`, `tsv`, or `dsv`) when it is given; otherwise a path ending
in
`.csv` or `.tsv` (in any case) is CSV or TSV, and **everything else is JSON**, as in Vega-Lite. A DSV names its
delimiter with `format.delimiter` (one character, a comma when omitted). `format.header` is ignored: the first line of
delimited text is always the header.

- *JSON*: the file must be an array of rows (primitives become `{"data": value}` rows), or an object whose rows are
  selected by `format.property`, a dotted path such as `"report.rows"` (or `"a[0].rows"`). JSON values keep their JSON
  types.
- *Delimited text (CSV, TSV, DSV)*: UTF-8 with or without a byte-order mark; lines end at LF, CRLF, or a lone CR; empty
  lines are skipped. A field is plain (a quote inside it is an ordinary character) or quoted with `"` (it may hold
  delimiters and line breaks, and `""` is one quote). Text after a closing quote, or a quote that is never closed, is
  an error. A row with **fewer** entries than the header has `null` in the missing fields, and **extra** entries are
  ignored. Repeated header names, and more columns than `MaxFieldCount`, are errors. A header such as `a.b` is
  addressed in an encoding as `a\.b`, like a JSON key that contains a dot.

**Types: `format.parse`.** Delimited text is read as text and, **as in Vega, nothing is converted unless you ask**: with
no `parse` property (or `"parse": null`) every cell of a CSV, TSV, or DSV file stays a **string**, so identifiers such
as
`01234` and text such as `2024-03-05` or `true` are kept exactly as written, and there is no data-based inference of
column types. JSON files, inline rows, and datasets keep their JSON types. You ask for conversion in one of four ways:

- `"parse": "auto"` is the explicit opt-in to inference. It looks at the whole column and picks, in this order,
  **boolean** when every non-empty value is `true` or `false` (any case), **number** when every non-empty value is a
  number (`12`, `-2.5`, `+4`, `.5`, `1e3`, `007`), **date** when every non-empty value is a date text (an ISO 8601
  date or
  date-time, or one of the forms of JavaScript's `Date.parse` that *Vega-Lite Time and Time Zones* lists, such as
  `Jan 2, 2006`), and otherwise leaves the column as text. One value that does not fit keeps the **whole** column text.
  An empty value is `null` in a typed column. (On JSON data `"auto"` applies the same rule to columns that hold text.)
- `"parse": null` (or no `parse`) keeps every value as read (text stays text).
- `"parse": {"price": "number", "ok": "boolean", "day": "date", "id": "string"}` converts only the **named** fields
  (the keys are field paths) and leaves every other field as read: `number` reads a finite number (a boolean is 1 or 0,
  a date is its milliseconds since the
  epoch); `boolean` reads `true` or `false` (any case) or `1` or `0`, and a number is true unless it is zero; `date`
  reads
  a date text by the rules of JavaScript's `Date.parse` (see *Vega-Lite Time and Time Zones*), or a number as
  milliseconds since the epoch, and `date:'%d/%m/%Y'` and `utc:'%d/%m/%Y'` read the text with a d3 time format, in the
  zone of the chart or in UTC; `string` is the text of the value (a number as
  JavaScript would write it, a date as `2024-03-05T00:00:00.000Z`). An empty value is `null` (except for `string`).
- A value that an explicit conversion **cannot read becomes `null`**, never a made-up number or date, and the chart is
  drawn: one warning `vl-data-parse-failed` per field and type tells the author how many values were affected and the
  first one (`3 value(s) of the field 'n' could not be read as number and are null (the first is in row 2: 'abc')`).
- A **temporal field is read as a date without being named**, as in Vega-Lite (the implicit `parse`): the field of a
  channel of the type `temporal`, with a `timeUnit`, or with a `formatType` of `time`, the field of a `timeUnit`
  transform, and the field of a filter or condition predicate that has a `timeUnit` or compares with a date-time object.
  A `parse` entry for the field wins over this, and `"parse": null` switches it off. See *Vega-Lite Time and Time
  Zones*.
- A date is an instant: its numeric value is the milliseconds since 1970-01-01T00:00:00Z, and its text is
  `yyyy-MM-ddTHH:mm:ss.fffZ`. Dates are supported from the year 1 to the **last millisecond of the year 9999**; text
  beyond that (for example `9999-12-31T23:59:59.9995`, a fraction of a millisecond past it) is **not a date**: under
  `"auto"` its column stays text, and under an explicit `date` conversion it becomes `null` with the usual
  `vl-data-parse-failed` warning. A date-time **without** an offset is read in the time zone of the chart
  (`VegaLiteRenderOptions.TimeZone`, UTC by default, never the zone of the machine), and a date without a time is UTC;
  a `Z` or `+HH:mm` offset is honored (see *Vega-Lite Time and Time Zones* for every form).

*Numbers in unparsed text.* Because the cells stay text, a chart still works with an unparsed CSV: as in Vega, a
**quantitative** encoding coerces numeric text to a number when it draws it, so quantitative `x`, `y`, `size`, and
`opacity` fields (and a linear scale's domain) read `"12.5"` as 12.5 without any `parse`. This coercion is not a parse
mode, so the values stay text in every other use (a nominal field or a label). A **temporal** field needs no `parse`:
it is read as a date, as described above.

*Where this differs from Vega.* The conversion rules follow Vega's, with these deliberate
differences: a number is a finite decimal or exponent number (hexadecimal, thousands separators, `NaN`, and infinities
are not numbers, where JavaScript's conversion would accept some of them); a date text is read by the documented subset
of the forms of `Date.parse` (ISO 8601 and the legacy forms listed in *Vega-Lite Time and Time Zones*), where Vega
accepts every text that the JavaScript engine of the browser reads; a date outside the years 1 to 9999 is not a date;
and a date that cannot be read is `null`, never JavaScript's `Invalid Date`. Where a quantitative value is needed a date
reads as its number of milliseconds, and as a category it reads as its ISO text.

**Named datasets from code.** `VegaLiteRenderOptions.Datasets` maps a name to a list of rows, each row a dictionary
from field name to a value (`null`, `bool`, `string`, a finite number, a `DateTime`, or a `DateTimeOffset`; a
`DateTime` of unspecified kind is taken as UTC, so the result does not depend on the host; `DateTime.MaxValue` and
`DateTimeOffset.MaxValue`, which are often used to mean "no end", are represented by the last supported millisecond of
the year 9999 (`9999-12-31T23:59:59.999Z`) instead of failing). A name in `Datasets`
replaces a dataset of the same name in the specification. This is the way to chart data that you hold in memory without
writing a file. The same `format.parse` rules apply to a named dataset.

**The sandbox.** A relative `url` is looked for in the **base directory** first (the folder of a file opened with
`Open(path)`, or `VegaLiteOpenOptions.BaseDirectory`) and then in each `DataPaths` folder, in order; the first file
that exists wins. The rules are the same on every platform:

- Only **relative** paths are read. A path that is empty, starts with a slash, has a drive letter or a colon, is a UNC
  or network path, or contains a control character (including the C1 range) is refused. `.` and `..` are resolved,
  and a path that would leave
  its folder (for example `../secret.csv`) is refused even if the file exists. A backslash is the same as a slash.
- A **symbolic link or junction** inside a folder is never followed, whatever it points to: a file that is a link, or
  that lies below a directory that is a link, is refused before it is opened. The folder you name is trusted, including
  when you reach it through a link; to use linked content, name the real folder as a data path. A hard link cannot be
  recognized, and a file that is replaced by a link after it was checked (a race with a writer) is not noticed, so point
  `BaseDirectory` and `DataPaths` only at folders that untrusted parties cannot write to.
- With a **`FileResolver`** callback the file system is never touched: the callback is called on the calling thread
  with the normalized logical path (first as written, then once per valid `DataPaths` entry used as a prefix), at most
  once per path and call, and returns a stream (disposed by the library) or `null`. It is never called for a remote URL.
- A document parsed from a string or opened from a stream has **no folder**: a local `url` then needs
  `BaseDirectory`, `DataPaths`, or a `FileResolver`, or the render fails.
- A file is read as UTF-8 (a byte-order mark is removed); text that is not valid UTF-8, including UTF-16, is an error
  and
  is never repaired. Each file is read **once per call**, however often the chart names it, and again by the next call.

**Remote URLs.** A `url` with a scheme (`https:`, `file:`, `data:`, ...), a drive letter, an absolute path, or a network
path is never fetched, never opened, and never passed to a `FileResolver`: it is reported as `vl-data-remote-url`, and
the chart is drawn with an empty dataset (strict mode throws `VegaLiteUnsupportedFeatureException`).

**Errors.** A local file that cannot be used is **not** an empty chart. These fail the render with a
`VegaLiteRenderException` located at the `url` (or `name`) in the specification, in lenient and strict mode alike: no
place to look, a path the sandbox refuses, a link, a missing file, a directory where a file is expected, an existing
file
that cannot be opened (for example for lack of permission, which is reported as a read failure and **never** as a
missing file), a file or a
`FileResolver` stream that cannot be read (including a callback that throws, which is reported as a read failure and
never as the callback's own exception), text that is not UTF-8, JSON that is invalid, nested too deeply, or not an
array,
JSON text that is not valid Unicode (such as an escaped lone surrogate, `"\ud800"`, in a string or a property name),
a missing `format.property`, malformed delimited text, a name that nobody defines, a sequence with a missing,
non-finite, or zero value, and any data limit. A cancellation is never an error of this kind: it is always an
`OperationCanceledException`. Files are opened read-only and tolerate other programs reading, writing, renaming, or
deleting them at the same time.
`MaxDataFiles` (64 distinct files per call), `MaxDataBytes` (64 MiB per file; reading stops one chunk past the limit,
so a
stream that never ends cannot exhaust memory), `MaxDataRows` (1,000,000 rows in total per call, checked before the rows
are allocated, also for a sequence), `MaxFieldCount`, and `MaxStringLength` (a text longer than this) name themselves in
the message. Data is read before the scene is built, so a data failure never depends on `MaxSceneItems`.

**What is not read.** Geographic data (`topojson`, `sphere`, `graticule`, GeoJSON values), `values` given as text, and
remote URLs are reported (`vl-data-geo`, `vl-data-values-text`, `vl-data-remote-url`) and give an empty dataset. The
rows that are read can be reshaped by the row transforms and summarized by the aggregating, binning, and stacking
transforms (see *Vega-Lite Transforms* and *Vega-Lite Aggregation, Binning, and Stacking*).

### Supported Vega-Lite Subset

The following is exactly what the package draws today. Anything else that Vega-Lite defines is **recognized and
reported** with a stable identifier (see *Vega-Lite Unsupported Features and Strict Mode*); the chart is still drawn
without it. A key that Vega-Lite does not define for a view, mark, encoding, or channel is a warning
(`vl-unknown-property`); unknown keys of the configuration are ignored.

<!-- markdownlint-disable MD013 -->
| Area | Supported |
| ---- | --------- |
| Schema | A `$schema` of Vega-Lite version 5 or 6, interpreted with the **version 6 defaults** (not the version 5 defaults, for example a view is 300 by 300 pixels by default); no `$schema` at all |
| Views | A single view, a `layer` of views (layers may be nested and carry a `resolve` of the scales, axes, and legends; see *Vega-Lite Layers and Composite Marks*), and the compositions `concat`, `hconcat`, `vconcat` (with `columns`, `spacing`, `align`, `center`, and `bounds`), `facet` (a `row` and `column` mapping, or one field with `columns`, over a `spec` that is a unit or a layered view; the `row`, `column`, and `facet` channels of a unit view), and `repeat` (a list, a `row` and `column` mapping, a `layer` list, and `{"repeat": ...}` field references), nested within `MaxCompositionDepth` levels and `MaxViews` views, with a `resolve` of the scales, axes, and legends of each composition; see *Vega-Lite Composition* |
| Data | Inline `data.values` (an array of objects, one row per object with nested values flattened to path fields such as `a.b`, or of primitives, one row `{"data": value}`); a relative local `data.url` read as JSON, CSV, TSV, or DSV; `format.type`, `delimiter`, `property`, and `parse` (see *Vega-Lite Data*); named data (`data.name`, from the caller's `Datasets` or the top-level `datasets`); and `data.sequence`. Remote URLs, geographic data, and `values` given as text are not read |
| Marks | `bar`, `rect`, `point`, `circle`, `square`, `rule`, `tick`, `line`, `area`, `trail`, `text`, and `arc`, given as a string or as a definition object with `type`; see *Vega-Lite Line, Area, Text, and Arc Marks*. The composite marks `boxplot`, `errorbar`, and `errorband`, which are drawn as layers of those marks; see *Vega-Lite Layers and Composite Marks* |
| Channels | `x`, `y`, `xOffset` and `yOffset` (grouped bars and offset points inside a band; see *Vega-Lite Offset Channels (Grouped Bars)*), `theta`, `radius`, `color`, `fill`, `stroke`, `size`, `opacity`, `fillOpacity`, `strokeOpacity`, `strokeWidth`, `strokeDash`, `shape`, `angle`, and `text`, each as a `field`, a literal `value`, or a literal `datum` where the channel permits it (`shape` is drawn by a `point` mark and by the overlay points of a `line`, `area`, `trail`, or `rule` (see *Overlays* under *Vega-Lite Line, Area, Text, and Arc Marks*), `angle` by a `point`, `circle`, `square`, or `text`, `text` by a `text` mark, `theta` and `radius` by an `arc` or `text` mark, `size` by every mark but a `rect`, `area`, and `arc`, and `strokeDash` by every mark but a `rect`, `area`, `trail`, `text`, and `arc`; see *Vega-Lite Legends, Color Schemes, and Scales*). `x2` and `y2` give the end of a range on a `bar`, `rect`, `rule`, or `area` (and the ends of the range of an `errorbar` or `errorband`), `xError`, `xError2`, `yError`, and `yError2` give the errors of an `errorbar` or `errorband` (they are reported on any other mark), `theta2` and `radius2` the end of the span of an `arc`, and `order` orders the layers of a stack, the points of a path, or the drawing order of the marks. `detail` groups the points of a path. `tooltip`, `href`, `description`, and `key` are accepted and ignored without a report, because they have no meaning in a still picture. `row`, `column`, and `facet` on a unit view facet it, as the `facet` operator does (see *Vega-Lite Composition*); inside a layer or the `spec` of a facet they are reported |
| Field types | `quantitative`, `temporal`, `nominal`, and `ordinal` (`geojson` is reported) |
| Encoding properties | `field`, `type`, `value`, `datum`, `title`, `scale`, `axis`, `legend` (an object of legend properties, or `null`), `aggregate` (all 24 operations, including `count` without a field), `bin` (on `x` and `y`: `true`, the bin parameters, or `"binned"`; on `color`, `fill`, `stroke`, `size`, the opacities, `strokeWidth`, `strokeDash`, `shape`, and `angle`: `true` or the bin parameters, with 6 bins by default, 4 for `strokeDash`, and 10 for `angle`), `timeUnit` (the 40 Vega-Lite time units, their `utc` forms, and the object form; see *Vega-Lite Time and Time Zones*), `stack` (`zero`, `center`, `normalize`, `true`, `false`, or `null`; also on `theta` and `radius`), `impute` (on `x` and `y`: an object of `keyvals`, `method`, `value`, and `frame`, or `null` to switch off the implicit impute of a stacked path mark), and `sort` (ascending, descending, `null`, an array of values, a channel such as `"-y"`, or an object that sorts by an aggregate of a field), and `condition` on the `color`, `fill`, `stroke`, `opacity`, `fillOpacity`, `strokeOpacity`, `strokeWidth`, `strokeDash`, `size`, `shape`, and `angle` channels (a `test` that is an expression or a predicate; the first condition that holds gives the mark its `value` or the scale of its field; see *Vega-Lite Conditional Encodings*); see *Vega-Lite Aggregation, Binning, and Stacking* |
| Scales | The types linear, log, pow, sqrt, symlog, sequential, time, utc, band, point, ordinal, quantile, quantize, threshold, and bin-ordinal, as Vega-Lite accepts them for a channel and a field type (a scale type that the channel or the field cannot use is replaced by the default one with the warning `vl-scale-type-ignored`); a diverging color scale is a sequential one with a `domainMid`. A temporal field gets a `time` scale (a `utc` scale for a `utc` time unit), see *Vega-Lite Time and Time Zones*. The polar channels `theta` and `radius` have the ranges 0 to a full turn and 0 to half of the smaller side of the view, see *Vega-Lite Line, Area, Text, and Arc Marks*. A binned quantitative color gets the bin-ordinal scale (one color per bin, and a legend of the ranges of the bins). A log scale takes `base`, a pow scale `exponent`, and a symlog scale `constant`; a log scale has no zero, is rounded out to powers of its base, and refuses a domain that includes zero or crosses it with a located error, where a symlog scale takes such data. The properties are `type`, `domain` (an array, or `{"param": ...}` of a variable or a selection parameter), `domainRaw`, `domainMid`, `domainMin`, `domainMax`, `range` (a list, or the name of an entry of `config.range`), `rangeMin`, `rangeMax`, `scheme`, `interpolate`, `zero`, `nice` (also a time interval for a time scale), `padding`, `reverse`, `paddingInner`, `paddingOuter`, `align`, `round`, `clamp`, `base`, `exponent`, and `constant`; see *Vega-Lite Legends, Color Schemes, and Scales*. A quantitative `domain` and `range` of more than two entries map piecewise exactly as in d3-scale (`min(domain length, range length)` entries are used, and `zero`, `nice` and padding change only the first and last entry), while a band or point scale uses the first two entries of its `range` |
| Axes | Grid, ticks, labels, domain, title, `orient`, `offset`, `minExtent`, `maxExtent`, `tickCount` (a number, and for a time scale a time interval), `tickMinStep`, `values`, a d3 number or time `format` (a text, or for dates an object of multi-format levels), `formatType` of `number`, `time`, or `utc`, the label properties (`labelAngle`, `labelAlign`, `labelBaseline`, `labelPadding`, `labelOffset`, `labelLimit`, `labelFlush`, `labelFlushOffset`, `labelOverlap`, `labelSeparation`), the font, color, opacity, dash, width, and cap properties of the parts, `titleAnchor`, `titleAngle`, `titlePadding`, and `zindex`, and `labelExpr` (see *Vega-Lite Label Expressions*), with the configuration precedence of Vega-Lite (including `config.axisTemporal`) |
| Legends | Symbol, gradient, and discrete gradient legends for the color, fill, stroke, shape, size, opacity, stroke width, and stroke dash channels, merged when channels share a field, placed by `orient` (right by default), titled like the axes, with the `legend` properties and `config.legend`; `legend: null` switches one off, and the legend of a chart makes the picture larger (see *Vega-Lite Legends, Color Schemes, and Scales*). A temporal field gets date labels (see *Vega-Lite Time and Time Zones*). The `labelExpr` of a legend is evaluated (see *Vega-Lite Label Expressions*). `legend.encode`, a `formatType` other than `number`, `time`, or `utc`, a `titleOrient` other than `top`, and `clipHeight` are reported and not drawn |
| Color schemes | The 68 color schemes of Vega by name (`scale.scheme` and `config.range`, lower or upper case), a list of colors, or `{"name", "count", "extent"}`, with `interpolate` (`rgb`, `hsl`, `hsl-long`, `lab`, `hcl`, `hcl-long`, and an sRGB `gamma`) for a list of colors; an unknown scheme name is an error. `cubehelix` interpolation is reported |
| Shapes | The symbol names `circle`, `square`, `cross`, `diamond`, `triangle-up`, `triangle-down`, `triangle-right`, `triangle-left`, `stroke`, `arrow`, `wedge`, and `triangle` for the `shape` channel, a `shape` property, and `legend.symbolType`; a custom SVG path is reported and a circle is drawn |
| Titles | `text` (a string or an array of lines), `subtitle`, `anchor`, `orient`, `offset`, `frame`, the font, color, and alignment properties, `limit`, `angle`, `dx`, and `dy` |
| Layout | `width` and `height` as numbers, `{"step": n}`, or `"container"`; `padding` as a number or an object; `autosize` of `pad`, `none`, `fit`, `fit-x`, and `fit-y` (with `contains`), also implied by a `"container"` size, for a single or a layered view (see *Vega-Lite Autosize and Container Sizes*); `background`; the `view` configuration (size, fill, and stroke). Of a layered view only the top-level `width`, `height`, `padding`, `autosize`, and `background` count, and a `step` is applied to the merged discrete scale of the layers (the union of their categories times the step). A composition is laid out as a grid with `spacing` (20 pixels by default), `columns`, `align`, `center`, and `bounds`, and takes the size of the views inside it (see *Vega-Lite Composition*) |
| Configuration | The Vega-Lite 6 and Vega defaults, the `config` of the specification, and `VegaLiteRenderOptions.Config`, merged in that order; the `style` mechanism; the `boxplot`, `errorbar`, and `errorband` entries of the composite marks (see *Vega-Lite Layers and Composite Marks*); the `concat` and `facet` entries (`spacing`, `columns`) and the `header`, `headerRow`, `headerColumn`, and `headerFacet` entries of the facet headers (see *Vega-Lite Composition*). Only the top-level `config` counts in a layered view or a composition |
| Expressions | A subset of the Vega expression language with 104 functions (math, string, array, type, regular expression, number format, date and time, and the selection functions `data`, `vlSelectionTest`, `vlSelectionIdTest`, and `vlSelectionResolve`), in the `calculate` and `filter` transforms, the `expr` of a parameter, the `{"expr": ...}` color, fill, stroke, opacity, stroke width, and size values of a mark that are evaluated for every row, and the `labelExpr` of axes, legends, and facet headers; see *Vega-Lite Expressions* and *Vega-Lite Label Expressions* |
| Parameters | Static **variable** parameters of the single or top-level view (`value` or `expr`, with a caller override through `VegaLiteRenderOptions.Parameters`), read by expressions, by expression references (`{"expr": ...}`, evaluated once), by the `param` filter predicate, and by a scale `domain` of the form `{"param": ...}`; see *Vega-Lite Parameters*. **Selection parameters** (`select` of `point` or `interval`, and the legacy `selection`) are drawn in their **initial state** (the `value`, or the caller's override) and read by the `condition` of a channel, by the `filter` transform, by a scale `domain` or a `bin` `extent` of the form `{"param": ...}`, and by `"bind": "scales"`, with `empty`; an interval selection with a `value` also draws its **brush**, and a legend that a selection is bound to (`"bind": "legend"`) **dims** the entries the selection does not hold, as Vega-Lite does; the events, the input widgets, and the other interaction properties have no meaning in a still picture. A variable parameter declared inside a layer or a composition is ignored, as Vega-Lite itself declares no signal for it, and is reported (`vl-param-variable`) |
| Transforms | `calculate`, `filter`, `fold`, `flatten`, `sample`, `pivot`, `lookup`, `timeUnit`, `aggregate`, `joinaggregate`, `window`, `bin`, `stack`, and `impute`, run in the order of the specification on the rows before the scales are built, where the `transform` of a layered view or a composition is applied first and then the `transform` of each layer or child (see *Vega-Lite Layers and Composite Marks* and *Vega-Lite Composition*); see *Vega-Lite Transforms*, *Vega-Lite Time and Time Zones*, and *Vega-Lite Aggregation, Binning, and Stacking* |
| Time | Temporal fields with implicit date parsing, the `parse` directives `date`, `date:'...'`, and `utc:'...'`, the time units, date-time objects, time and UTC scales with calendar ticks and `nice` intervals, date labels on axes and legends, the date and time expression functions, and the render option `TimeZone`; see *Vega-Lite Time and Time Zones* |
| Other transforms | **None** (`regression`, `loess`, `density`, `quantile`, and `extent` are reported; see the identifiers below) |
<!-- markdownlint-enable MD013 -->

**Field types without a `type`.** The `type` of a field definition is optional, as in Vega-Lite 5 and later. A
definition
without one gets the type Vega-Lite infers (its `defaultType`), the same in every stage that reads it (scales, legends,
axes, labels, stacks, bins, time parsing, composite marks); a missing type is valid and is never reported, also in
strict
mode. The rules apply in this order, and a written `type` always wins:

<!-- markdownlint-disable MD013 -->
| Rule | Inferred type |
| ---- | ------------- |
| The channel is `latitude` or `longitude` | `quantitative` |
| The channel is `row`, `column`, `facet`, `shape`, or `strokeDash` | `nominal` |
| The channel is `order`, or `sort` is an array of values | `ordinal` |
| There is a `timeUnit` | `temporal` |
| There is a `bin`, or an `aggregate` other than `argmin` and `argmax` (so `count`, `valid`, `missing`, and `distinct` too) | `quantitative` |
| The `scale.type` is a numeric or discretizing type (`linear`, `log`, `pow`, `sqrt`, `symlog`, `identity`, `sequential`, `quantile`, `quantize`, `threshold`), or `time` or `utc` | `quantitative`, or `temporal` (any other scale type, such as `ordinal`, `point`, `band`, or `bin-ordinal`, leaves the field `nominal`, as in Vega-Lite) |
| Anything else | `nominal` |
<!-- markdownlint-enable MD013 -->

The channel matters only for the first three rules, so a field on `x`, `y`, `color`, `size`, or `text` without a `type`
(and without a `bin`, an `aggregate`, a `timeUnit`, or a `scale.type`) is **nominal**: `"text": {"field": "status"}`
shows the strings of the field, a `color` field gets the categorical colors and a symbol legend, and a field on `x`
gets a band or point scale. Write `"type": "quantitative"` on a measure that is not aggregated or binned. The secondary
channels `x2`, `y2`, `theta2`, and `radius2` have the type of their main channel. A `datum` takes its type from its
value (a number is `quantitative`, a string or a boolean `nominal`, a date-time object `temporal`).

**Behavior change.** Earlier versions of this package read a field without a `type` as `quantitative` on every
channel. That was not Vega-Lite's behavior: an `x` or `y` field **without a type is now nominal**, as in Vega-Lite
(a band or point scale, not a linear one), and a `text` or `color` field without a type no longer draws `NaN` or
a gradient. A specification that relied on the old default for a measure that is neither aggregated nor binned needs
`"type": "quantitative"` on that field.

The default colors, sizes, orientations, and layout constants follow Vega-Lite 6 and Vega; positions are derived from
the same rules, so a chart is laid out as Vega-Lite would lay it out up to the width of the text. Marks that are not
paths with a missing, `null`, NaN, or non-numeric value on a continuous `x`, `y`, `size`, or `opacity` channel are
dropped (the Vega-Lite 6 behavior for marks that are not lines), and so are values outside an explicit discrete
`domain`; a `line`, `area`, or `trail` breaks or connects its path at such a row (see *Vega-Lite Line, Area, Text, and
Arc Marks*).
Discrete domains are ascending by default, with mixed kinds ordered null, boolean, number, and string. Marks are clipped
only with `"clip": true`. A line feed inside title text splits the title into lines, which Vega itself does not do.

### Vega-Lite Expressions

An expression is a string in a **subset of the Vega expression language** (itself a subset of JavaScript). The package
parses it with its own bounded parser and runs it with its own interpreter: there is no `eval`, no reflection, no
user-defined function, no file, network, clock, or random source, so an expression is a pure, deterministic function
of its row and its parameters. Expressions are accepted in these places, and only these:

- the `calculate` of a `calculate` transform, and a `filter` that is a string (also inside the `and`, `or`, and `not`
  of a filter predicate): the expression runs once for every row, with `datum` bound to the row;
- the `expr` of a variable parameter: it runs once per call, without a row (see *Vega-Lite Parameters*);
- an `{"expr": ...}` value that is evaluated for every mark row: the mark properties `color`, `fill`, `stroke`,
  `opacity`, and `strokeWidth`, `size` for a `point`, `circle`, or `square`, and the constant `value` of the `color`,
  `fill`, `stroke`, `opacity`, and (for those marks) `size` channels. A result of the wrong kind (a color that is not
  text, a number that is not finite) leaves the default of a mark property in place; the `opacity` or `size` of a
  channel `value` that is not a finite number leaves that row out of the picture, like a missing value of a continuous
  channel;
- an `{"expr": ...}` value at a **static site**: a property that has one value for the whole chart, which Vega-Lite's
  schema allows an expression at. The expression runs **once per call**, without a row, after the parameters are
  evaluated and before anything else reads the specification, so the title, the padding, the background, the
  configuration, the scales, the guides, and the marks see the value, never the expression (see *Expression
  references* in *Vega-Lite Parameters*);
- the `labelExpr` of an axis, a legend, and a facet header, and of the axis and header entries of `config`: the
  expression runs once for every label, with `datum` bound to the label (see *Vega-Lite Label Expressions*).

An expression anywhere else (`width`, `height`, a `facet` field definition, an `{"expr": ...}` that Vega-Lite's schema
does not allow, and every one inside a selection: the brush `mark`, `on`, `fields`, the initial `value`, in `params` or
in `config.selection`) is not evaluated and is reported (`vl-expr-ref`), and so is a static site that reads `datum`.
Conditional encodings (`condition` with a `test` or a `param`) are applied, as described in *Vega-Lite Conditional Encodings*.

Every expression of every view is **compiled when the document is opened**, once, so a mistake is a
`VegaLiteSpecException` at the string value of the expression, with the pointer, line, and column of the value and the
zero-based offset inside the expression text in the message (`... Invalid expression: <what> at expression offset N.`),
and a parsed document can be rendered any number of times and from several threads.

**The language.**

<!-- markdownlint-disable MD013 -->
| Element | Supported |
| ------- | --------- |
| Literals | `null`, `true`, `false`; numbers (decimal with fraction and exponent, and hexadecimal `0x1F`); strings in `'...'` or `"..."` with the JavaScript escapes (`\n`, `\t`, `\xHH`, `\uHHHH`, `\u{...}`; no octal escapes, no template strings); array literals `[1, 2]`; object literals `{a: 1, 'b c': 2}`; regular expression literals `/ab+c/gi` (flags `g`, `i`, `m`, `s`; `u`, `v`, `y`, and `d` are not supported and are an error) |
| Constants | `NaN`, `Infinity`, `PI`, `E`. The other Vega constants (`SQRT2`, `LN2`, ...) are not available |
| `datum` | The row: `datum.name`, `datum['a b']`, `datum.a.b`, and `datum[expression]`. A missing field is `null`; a nested field is read from the data as a path (`a.b`) or rebuilt as an object or array when the row has `a.b` or `a.0` fields (names `0` to n-1 make an array; reading the same path twice gives the same value, so `datum.a === datum.a`) |
| Member access | `value.name`, `value[key]`, `array.length`, `string.length`, `array[0]`, `string[0]`, and the `source`, `flags`, and `global` of a regular expression. The member of `null` or of a scalar is `null`; there are no method calls (`a.b()` is a syntax error) |
| Operators, from the lowest to the highest precedence | `? :` (right to left); `\|\|`; `&&`; `==` `!=` `===` `!==`; `<` `<=` `>` `>=`; `+` `-`; `*` `/` `%`; the unary `+` `-` `!`; member access, indexing, and calls. Parentheses group |
| Short circuit | `&&`, `\|\|`, and `? :` run only the operand they need, and `&&` and `\|\|` **return an operand**, not a boolean, as in JavaScript |
| Parameters | Any other identifier is the value of the parameter of that name (see *Vega-Lite Parameters*); a name that is not a parameter is a `VegaLiteSpecException` when the document is opened |
<!-- markdownlint-enable MD013 -->

The evaluation follows JavaScript with the invariant culture: `+` concatenates when either side is text and adds
numbers otherwise, `==` and the relational operators convert their operands as JavaScript does, `===` compares the
kind and the value, division by zero is an infinity or `NaN`, and `%` is the remainder of JavaScript. Arrays and
objects are values that compare **by identity** (`[1] == [1]` is false, as in JavaScript). Two differences are
deliberate: the data model has one `null` for both JavaScript's `null` and `undefined`, so `null + 1` is 1 (JavaScript
gives `NaN` for `undefined + 1`), and `isNaN` and `isFinite` do not convert their argument (see below). A date is a
value
that reads as its milliseconds since the epoch in arithmetic and as its ISO text as a string; the date functions create
and read dates (see *Vega-Lite Time and Time Zones*).

**What is a syntax error.** Assignments (`=`, `+=`, ...), `++` and `--`, `new`, `function` and `=>`, `typeof`, `void`,
and `delete`, the comma operator and `;`, `**`, `??`, `?.`, spread (`...`), template strings, method calls, and every
other JavaScript reserved word are errors that fail when the document is opened.

**What parses but does not run.** A construct that the language has but this release does not run is **reported** and
evaluates to `null`, so one unsupported call does not hide the rest of the chart: a call of a function outside the
list below (`vl-expr-function-<name>`), `signal`, `event`, `item`, and the signals of the view that no parameter
declares (`width`, `height`, `padding`, `autosize`, `background`, `cursor`: `vl-expr-view-state`), and the operators
`|`, `^`, `&`, `<<`, `>>`, `>>>`, and `in` (`vl-expr-construct-<name>`: `bitwise-or`, `bitwise-xor`, `bitwise-and`,
`bitwise-not` (the unary `~`),
`shift-left`, `shift-right`, `shift-right-unsigned`, `in`). In strict mode the first report throws a
`VegaLiteUnsupportedFeatureException` (see *Vega-Lite Unsupported Features and Strict Mode*); the report is located at
the string value of the expression and its message names the offset of the construct.

**The functions.** There are exactly **100** functions; names are case-sensitive and an argument that is not given is
`undefined` (`NaN` as a number, the text `undefined` as a string). Number arguments are converted as in JavaScript.

<!-- markdownlint-disable MD013 -->
| Math (27) | Meaning |
| --------- | ------- |
| `abs(x)`, `ceil(x)`, `floor(x)`, `trunc(x)`, `sign(x)`, `sqrt(x)` | The absolute value; the smallest integer not below `x`; the largest integer not above `x`; `x` without its fraction; -1, 0 (a zero keeps its sign), 1, or `NaN`; the square root |
| `round(x)` | The nearest integer; halves go toward positive infinity, as JavaScript's `Math.round` |
| `exp(x)`, `log(x)`, `log10(x)`, `pow(x, y)` | e to the power `x`; the natural logarithm; the base-10 logarithm; `x` to the power `y` with JavaScript's special cases (`NaN` for a `NaN` exponent) |
| `sin(x)`, `cos(x)`, `tan(x)`, `asin(x)`, `acos(x)`, `atan(x)` | The circular functions, in radians |
| `sinh(x)`, `cosh(x)`, `tanh(x)`, `asinh(x)`, `acosh(x)`, `atanh(x)` | The hyperbolic functions |
| `atan2(y, x)` | The angle of the point (`x`, `y`) in radians |
| `clamp(value, min, max)` | `value` limited to the range from `min` to `max` |
| `max(a, b, ...)`, `min(a, b, ...)` | The largest or smallest argument; any number of arguments (negative or positive infinity for none); `NaN` when an argument is `NaN` |

| String (9) | Meaning |
| ---------- | ------- |
| `lower(s)`, `upper(s)` | The text in lower or upper case with the full JavaScript mappings, independent of the culture (`upper('ß')` is `SS`, `lower('İ')` is `i` and a combining dot, and a capital sigma at the end of a word becomes `ς`); the result is at most `MaxStringLength` characters |
| `trim(s)` | The text without JavaScript white space at both ends |
| `substring(s, start[, end])` | The part from `start` to `end` (the end of the text when omitted); positions are clamped to the text and swapped when `start` is larger; counted in UTF-16 code units |
| `startswith(s, search[, position])`, `endswith(s, search[, endPosition])` | Whether the text has `search` at `position` (0 by default), or ends with it when cut at `endPosition` |
| `pad(s, length[, character[, align]])` | The text padded with repeated `character` (a space by default) up to `length`: at the end by default, at the start for an `align` of `"left"`, on both sides for `"center"` |
| `replace(s, pattern, replacement)` | The text with the first occurrence of a text `pattern`, or the matches of a regular expression (all of them with the `g` flag), replaced; the replacement may use `$&`, `` $` ``, `$'`, `$$`, `$1`, and `$<name>` |
| `split(s, separator[, limit])` | The array of the parts between the matches of a text or regular expression `separator` (captured groups are included, as in JavaScript), at most `limit` parts |

| Array and text (6) | Meaning |
| ------------------ | ------- |
| `length(x)` | The number of UTF-16 code units of a text or of elements of an array; `null` for anything else |
| `slice(x, start[, end])` | A part of a text or array (a new array); a negative position counts from the end |
| `indexof(x, value)`, `lastindexof(x, value)` | The first or last position of a text in a text, or of an element strictly equal (`===`) to `value` in an array; -1 when it does not occur |
| `includes(x, value)` | Whether the text contains the text `value`, or the array has an element that is the same value (`NaN` equals `NaN`) |
| `join(array[, separator])` | The elements as text joined by `separator` (`,` by default; `null` elements are empty); `null` when `x` is not an array |

| Type and conversion (14) | Meaning |
| ------------------------ | ------- |
| `isArray(x)`, `isBoolean(x)`, `isDate(x)`, `isNumber(x)`, `isString(x)`, `isRegExp(x)` | Whether the value is of that kind; `isNumber` is true for `NaN` and the infinities |
| `isObject(x)` | True for objects, arrays, dates, and regular expressions (not for `null`) |
| `isFinite(x)`, `isNaN(x)` | True for a number that is finite, or is `NaN`; false for any other kind, **without conversion** (like `Number.isFinite` and `Number.isNaN`) |
| `isValid(x)` | True unless the value is `null` or `NaN` |
| `toBoolean(x)`, `toNumber(x)`, `toString(x)` | The value converted as JavaScript does (the texts `false` and `0` are false for `toBoolean`); `null` and the empty text give `null`, so a missing value stays missing |
| `toDate(x)` | A date from a date, a number of milliseconds, or a date text (read in the zone of the chart as a `Date.parse` text is); `null` for `null`, the empty text, and anything that is not a valid date |

| Regular expression (2) | Meaning |
| ---------------------- | ------- |
| `regexp(pattern[, flags])` | A regular expression value; an invalid pattern or an unknown, unsupported (`u`, `v`, `y`, `d`) or repeated flag fails the render |
| `test(regexp, s)` | Whether the text matches; a text instead of a regular expression is used as the pattern |

| Number format (1) | Meaning |
| ----------------- | ------- |
| `format(value, specifier)` | The number as text with a d3 number format (d3-format) in the `en-US` locale: the minus sign is U+2212 and the thousands separator is a comma, whatever the culture of the machine. `value` is converted as JavaScript's unary plus does (`'12'` is 12, `true` is 1, a date is its milliseconds), so a value that cannot be read gives the text `NaN`, and `null` gives the text `null` without the specifier being checked (`format(null, 'nonsense')` is `null`, as in Vega). A `specifier` that d3 does not accept is a `VegaLiteRenderException` located at the expression (it is not a warning). The type `c` is the exception, as in d3: it prints the original value as JavaScript text (`format('foo', 'c')` is `foo` and `format(true, 'c')` is `true`), and the prefix, suffix, width, and padding of the specifier still apply |

| Date and time (41) | Meaning |
| ------------------ | ------- |
| `datetime()`, `datetime(x)`, `datetime(year, month[, day, hours, minutes, seconds, milliseconds])` | A date. With no argument it is `null` (an expression has no clock); with one argument it is the instant `x` (a date, a number of milliseconds, or a date text); with two or more it is the wall-clock time in the zone of the chart, with the month counted from 0 and the day 1 by default. Parts that are too large or too small roll over as in JavaScript (month 12 is January of the next year, day 0 is the last day of the previous month), and a year from 0 to 99 means 1900 to 1999. A result outside the years 1 to 9999 is `null` |
| `utc(year[, month, day, hours, minutes, seconds, milliseconds])` | The same wall-clock rules in UTC, as the **number** of milliseconds (the month is 0 by default) |
| `time(d)`, `timezoneoffset(d)` | The milliseconds of a date (a number, `NaN` for a value that is not a date); the offset of the zone from UTC at that instant **in minutes with the sign of JavaScript** (`300` for UTC-05:00, `-330` for UTC+05:30) |
| `year`, `quarter`, `month`, `week`, `date`, `day`, `dayofyear`, `hours`, `minutes`, `seconds`, `milliseconds`, `isoweek` `(d)`, and the same names with a `utc` prefix (`utcyear`, `utcmonth`, ...) | The part of a date: the year, the quarter 1 to 4, the month 0 to 11, the week of the year counted in Sundays (0 until the first Sunday), the day of the month 1 to 31, the weekday 0 (Sunday) to 6, the day of the year 1 to 366, the time of day, and the ISO 8601 week number. The names without the prefix read the zone of the chart; a value that is not a date gives `NaN` |
| `timeOffset(unit, d[, step])`, `utcOffset(unit, d[, step])` | The date moved by `step` units (1 by default; negative goes back) with the calendar rules of d3-time (a month moves to the same day of the month, rolling over when it has none). `unit` is `year`, `quarter`, `month`, `week`, `date`, `day`, `dayofyear`, `hours`, `minutes`, `seconds`, or `milliseconds` in lower case (the singular d3 names such as `hour` and `day` also work); any other value gives `null` |
| `timeSequence(unit, start, stop[, step])`, `utcSequence(...)` | The array of dates from `start` up to, **not including**, `stop` in steps of that unit, aligned to the calendar as the ticks of d3 are; at most `MaxScaleDomainSize` dates, and one evaluation step is charged for each |
| `timeUnitSpecifier(units[, specifiers])` | The d3 time format that labels the given Vega time unit names (a text or an array of them) with Vega's defaults, for example `%Y-%m` for `["year", "month"]`; `specifiers` replaces the format of a unit. `null` for an invalid list |
| `timeFormat(value, specifier)`, `utcFormat(value, specifier)` | The date as text with a d3 time format (see *Vega-Lite Time and Time Zones*), or with an object of multi-format levels; `value` is read as a number of milliseconds like d3 does, so a text that is not a number gives `Invalid Date`, and `null` gives the text `null` |
| `timeParse(text, specifier)`, `utcParse(text, specifier)` | The date that a d3 time format reads from the text, or `null` when the text does not match it |
| `monthFormat(m)`, `monthAbbrevFormat(m)`, `dayFormat(d)`, `dayAbbrevFormat(d)` | The English name of month `m` (0 is January) or weekday `d` (0 is Sunday), full or abbreviated; the empty text for anything that is not a whole number; other numbers roll over |
<!-- markdownlint-enable MD013 -->

The selection functions are the ones that the expressions of a compiled selection call; they read the **stores of the
selections** of the call (see *Selection parameters* in *Vega-Lite Parameters*):

<!-- markdownlint-disable MD013 -->
| Selection (4) | Meaning |
| ------------- | ------- |
| `data(name)` | The array of the tuples of the selection store `name` (a selection `brush` has the store `brush_store`: each tuple is an object with `unit`, `fields`, and `values`); an empty array for any other name, as no other data set can be read |
| `vlSelectionTest(store, datum[, op])` | Whether the datum is selected by the tuples of the store, with the rules of `vega-selections`: a point value must be strictly equal (or a member of a list), a range includes both ends (a binned range excludes its end), a datum without the field is not selected; `op` is `"union"` (any unit selects it) or `"intersect"` (every unit does). A store without tuples gives the number `0` |
| `vlSelectionIdTest(store, datum[, op])` | The same test of the identity of the row (`_vgsid_`) |
| `vlSelectionResolve(store[, op, isPoint, vegaLite5])` | The value of the selection: an object with the values or the range of each field (and `vlPoint`, the query of the tuples, for a point selection); an empty object for an empty store |
<!-- markdownlint-enable MD013 -->

The functions of the color (`rgb`, `hsl`, ...), scale (`scale`), and
geographic families, and the other functions of Vega, are reported as unsupported (`vl-expr-function-<name>`, for
example `vl-expr-function-geoarea`) and give `null`. `now` and `random` are not available by design: an expression has
no clock and no random source, so `now()` is reported as `vl-expr-function-now`, and `datetime()` gives `null`.

**Regular expressions** are JavaScript regular expressions run by the .NET engine, so a few details follow .NET: `\d`,
`\D`, `\w`, and `\W` are ASCII classes (also inside a character class), `\s` and `\S` are the JavaScript white space,
`$` without the `m` flag matches only at the very end, `[^]` matches any
character, `[]` matches nothing, and `.` and the numbering of mixed named and unnamed groups follow .NET. The
linear-time matching engine is used (so a pattern cannot cause catastrophic backtracking); look-around and
back-references need the backtracking engine and are stopped after 100 ms, which fails the render with a located error.
A pattern is at most 1,024 characters; an invalid pattern in a literal is an error when the document is opened, and one
built by `regexp()` fails when it runs. Matching works on UTF-16 code units, so the flags `u` and `v` (Unicode
matching),
`y` (sticky), and `d` (match indices) are rejected, in a literal when the document is opened and in `regexp()` when it
runs, instead of being accepted and ignored.

**Limits and errors.**

<!-- markdownlint-disable MD013 -->
| Limit | Default | Checked | Failure |
| ----- | ------- | ------- | ------- |
| `MaxExpressionLength` | 4,096 characters | When the document is opened (`VegaLiteOpenOptions.Limits`); applies to the expressions that you write (also an `{"expr": ...}` value inside a predicate), not to the text that the renderer writes from a predicate (see the `oneOf` rule under the time predicates), which is bound by eight times `MaxSourceLength` | `VegaLiteSpecException` (the offset is the limit) |
| `MaxExpressionDepth` | 64 (at most 256) | When the document is opened | `VegaLiteSpecException`; the nesting of parentheses, brackets, call arguments, array and object literals, conditional branches, and unary operators counts, a long chain such as `a + b + c + ...` does not |
| `MaxEvaluationSteps` | 50,000,000 | At every instruction; **one budget per call** shared by the parameters, the transforms, and the row-bound mark values | `VegaLiteRenderException` located at the expression, with the offset of the instruction that ran out. A step is one executed instruction, plus one per 64 characters or elements of the work of a function such as `split` or `replace`, or of an operator that reads a long string or array as a number or text |
| `MaxStringLength` | 1,048,576 | For every text an expression builds (`+`, `join`, `pad`, `replace`, ...) | `VegaLiteRenderException` |
<!-- markdownlint-enable MD013 -->

A failure that depends on the data (a limit, a regular expression) is a `VegaLiteRenderException`; everything else in
the data is a value, never an exception: a missing field, a member of `null`, or a call with the wrong kind of argument
gives `null`, `NaN`, or the JavaScript result. A canceled token is an `OperationCanceledException`, never wrapped, and
is checked when an evaluation starts and whenever the instruction counter passes a multiple of 1,024.

Examples of expressions (each is the value of a `calculate`, a `filter`, or an `expr`):

```json
{ "calculate": "datum.crates - datum.returned", "as": "sold" }
{ "filter": "datum.sold > 20 && lower(datum.stall) != 'east'" }
{ "calculate": "join(split(datum.tags, ';'), ' / ')", "as": "label" }
{ "calculate": "test(/^n/i, datum.stall) ? 'north' : 'other'", "as": "region" }
{ "calculate": "clamp(round(datum.crates / 10) * 10, 20, 80)", "as": "bucket" }
```

### Vega-Lite Parameters

A **variable parameter** is a named value that expressions read. The package supports the **static** form: the
parameter has the value that its definition gives, because a still picture has no widget that could change it.

```json
{
  "params": [
    { "name": "cut", "value": 3 },
    { "name": "limit", "expr": "cut + 1" },
    { "name": "show", "value": true }
  ],
  "transform": [ { "filter": "datum.x > limit" }, { "filter": { "param": "show" } } ]
}
```

- A parameter is an entry of the `params` array of the (single, drawn) view with a text `name` and **no `select`**. Its
  value is the `expr` when it has one (an expression, evaluated once per call) and otherwise the `value` (any JSON
  value: text, number, boolean, `null`, an array, or an object; arrays and objects work with `length`, `slice`,
  member access, and so on). A parameter with neither is `null`. A `bind` is accepted, ignored, and not reported, and
  `empty` is ignored: the parameter keeps its initial value.
- The names must be unique in a view, and `datum`, `event`, `item`, and `parent` are reserved. A repeated or reserved
  name is a `VegaLiteSpecException` at the `name`.
- An expression reads a parameter by writing its name. A name that no enclosing view declares is a
  `VegaLiteSpecException` at the
  expression when the document is opened (except the view signals, which are reported as `vl-expr-view-state`). The
  parameters are evaluated in **dependency order** (a parameter after the parameters its expression reads, otherwise in
  the order of the text), so the order in the text does not matter. A parameter that depends on itself, directly or
  through others, is a cycle and a `VegaLiteSpecException` at the `expr` of the first parameter of the text that is on
  the cycle or depends on it.
- A `filter` predicate `{"param": "name"}` keeps every row when the parameter is truthy (JavaScript truthiness) and no
  row otherwise.
- `VegaLiteRenderOptions.Parameters` **replaces the definition** of a parameter by a value of the host: the `value` and
  the `expr` of the specification are not used and the expression is **not evaluated**, and the expressions that read
  the parameter see the override. A date (`DateTime`, `DateTimeOffset`) becomes a date value, and a **structured value**
  (an array or list, or a string-keyed dictionary, nested) becomes an array or an object, so a host can give the
  domain of a scale (`new object[] { 2, 6 }`) or an object. The names that may be
  overridden are `VegaLiteDocument.ParameterNames`; any other name is an `ArgumentException` when the chart is measured
  or rendered. A text of an override (or of a
  `value`) longer than `MaxStringLength` fails the render with a located `VegaLiteRenderException`. The override of a
  **selection** parameter replaces its initial value (see *Selection parameters* below). The `canvasnet` tool does not
  pass parameter values to a Vega-Lite chart yet (its `-D` option sets OpenSCAD variables only); a chart is drawn with
  the values of its specification there.
- **Only the variable parameters of the top-level view exist.** Vega-Lite compiles the `params` of the top-level
  specification into the signals of the chart and none for a view inside a layer or a composition, so a variable
  parameter declared there is ignored and reported (`vl-param-variable`); an expression that reads one fails in Vega
  and evaluates to `null` here. Declare a parameter that several views read at the top level.

**Selection parameters.** A parameter with `select` (`"point"`, `"interval"`, or an object such as `{"type":
"interval", "encodings": ["x"]}`), and an entry of the legacy top-level `selection` (`single`, `multi`, `interval`),
is a **selection**. A still picture has no mouse, so a selection is drawn in its **initial state**: nothing is selected,
or what its `value` (the legacy `init`) selects, or what the host gives in `VegaLiteRenderOptions.Parameters`. The
state is the one that Vega-Lite's compiled chart holds before any event, and the tests are the ones of Vega-Lite and
`vega-selections`. Example 49 draws a brush, a point selection, and a legend bound to a selection.

```json
{
  "params": [ { "name": "brush", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [1, 4] } } ],
  "data": { "values": [ { "a": 1, "b": 1 }, { "a": 4, "b": 2 }, { "a": 7, "b": 3 } ] },
  "mark": "circle",
  "encoding": {
    "x": { "field": "a", "type": "quantitative" },
    "y": { "field": "b", "type": "quantitative" },
    "fill": { "condition": { "param": "brush", "value": "#d73027" }, "value": "#4575b4" }
  }
}
```

The circles at `a` = 1 and 4 are red (the range includes both ends) and the one at 7 is blue.

- **Where a selection applies.** A selection of a single or a layered unit view applies to that unit; a selection of a
  `concat`, `facet`, or `repeat` at the top level applies to every unit view inside (or to those that its `views` names,
  by unit `name`) and is **one selection** for all of them: the store is the one of the first unit, so a field it projects
  is tested in every view. A selection that two layers of one layered view both receive is a duplicate signal that Vega
  rejects: it is applied as empty and reported (`vl-param-selection-layer`); name one layer in `views`, or declare the
  selection in that layer. The name of a selection is visible from every view (a condition in one view may name the
  selection of another); a `param` or `selection` that no view declares is a `VegaLiteSpecException` at the name.
- **The projection** says which fields a selection tests, as in Vega-Lite: the `fields` and `encodings` of the `select`
  object; else the keys of the `value` (a key that is a channel name such as `x` is an encoding, another key is a field
  of a point selection); else the defaults of `config.selection` (an interval projects `x` and `y`; a point selection
  projects the identity of the rows, `_vgsid_`). An explicit list counts when it is present, even when it is empty:
  `"fields": []` is an empty projection (every row matches a tuple), not the default. A projected channel that has no
  field, or an aggregate, is skipped
  and reported as the warning `vl-param-selection-projection`; so is an interval initialized with a key that is not a
  channel (the default encodings are used).
- **The initial value.** A point selection takes an array of objects (`[{"a": 4}, {"a": 7}]`, one tuple each), one object
  (`{"a": 4}`), or, for a selection of one field, a scalar or an array of scalars (`4`, `[4, 7]`: one tuple per value).
  An interval selection takes a map of one range per encoding or field (`{"x": [1, 4], "y": [2, 3]}`); a reversed range
  is read sorted. A date is a date-time object (`{"year": 2020, "month": 1, "date": 15}`, read in the calendar of the
  chart; see *Vega-Lite Time and Time Zones*). Over a discrete (band, point, or ordinal) axis the range holds the
  values at its two ends and **only those categories** are in the selection; over a binned field the range includes its
  start and excludes its end. A point selection that projects the identity of the rows (no `fields`, no `encodings`,
  no key in the `value`) has no tuples in a still chart, since the rows have no identity: with a `value` it is applied as
  empty and reported (`vl-param-selection-id`).
- **What is selected.** A datum is in a point selection when its field equals the value with strict equality (the number
  `1` is not the text `"1"`; a datum without the field is not selected); in an interval selection when its value is
  between the two ends, both included. A selection with **no tuples is empty**: the `empty` property of a condition or
  filter (`true` by default) says whether an empty selection selects every row or none (`{"param": "brush", "empty":
  false}` selects none). The legacy `empty: "none"` is `empty: false`. With several selections in several units, a
  selection of `resolve` `"global"` (the default) or `"union"` selects what any unit selects, and `"intersect"` what
  every unit selects.
- **Where it is used.** A selection is read by a channel `condition` (`{"param": "brush", "value": ...}`, a field
  condition, or a `test` that holds it: `{"test": {"and": [{"param": "brush"}, {"field": "a", "gt": 1}]}}`, also in
  `not`, `and`, and `or`; the legacy `{"selection": "brush"}` is the same), by a `filter` transform
  (`{"filter": {"param": "brush"}}`, or `{"filter": {"selection": "brush"}}`), by the domain of a scale and the extent
  of a bin (below), and by `"bind": "scales"` (below). An expression can read it by its name: it
  has the value that Vega-Lite computes from the selection (an object with one array per field, as `brush.a[0]`
  and `brush.a[1]` for the range of the field `a`). A selection that is projected over a field with a `timeUnit`
  tests the derived field (`month_date`, whose year is 2012 when the unit has none, as everywhere in Vega-Lite): the
  `filter` and the units whose conditions name the selection get that field computed first, as Vega-Lite does.
- **The host can set it.** `Parameters["brush"]` takes the same shapes as the `value` (a dictionary of arrays for an
  interval; a list of dictionaries or a dictionary for a point selection; a scalar or a list of scalars; a `DateTime`
  for a date) and **replaces** the `value` of the specification; `null` clears the selection. A date-time object as a
  member of a dictionary is not supported (give a `DateTime`). The value of a row-identity selection is reported.
- **The brush.** An interval selection that has an initial value draws the two rectangles that Vega-Lite draws (named
  `brush_bg` and `brush` there): a shade **before** the marks, with the `fill` and `fillOpacity` of the selection's
  `mark` (`#333` and 0.125 by default), and an outline **after** them, with a transparent fill and every other key
  of the `mark` as its stroke (`stroke` white by default, `strokeWidth`, `strokeOpacity`, `strokeDash`,
  `strokeDashOffset`, and also `strokeCap`, `strokeJoin`, `strokeMiterLimit`, and `opacity`, which Vega-Lite's compiler
  copies to the rectangle although its schema does not list them; the `cursor` has no meaning). Both are clipped to
  the data rectangle. The `mark` of the selection
  is merged over `config.selection.interval.mark` (the specification's and the caller's `Config`), property by
  property. Vega-Lite's schema gives the `mark` plain values only, so an `{"expr": ...}` there (a fill from a
  parameter, say) is reported (`vl-expr-ref`) and not evaluated: the brush keeps the default of that property (or the
  value of the configuration, for a reference in the selection's own `mark`). The rectangle runs from the first end of
  the range to the second of the **first** value, each end mapped
  through the scale of its channel (the ends may come in either order; a band scale maps a value to the **start** of its
  band, so a brush from `"a"` to `"c"` stops where the band of `"c"` starts), and an axis the selection is not projected
  over spans the data rectangle. A brush that has no extent on an axis it is projected over, a selection with no value,
  and a value that has no position on its scale draw nothing (no item is made). In a **layered** view the shades of all
  the selections come before the marks of every layer and the outlines after them (the last layer's shade first, the
  first layer's outline first), and the selections of one view wrap each other in the order they are declared. A
  selection of `resolve` `"global"` (the default) has one interval, the one of the unit that declared it first: of
  several views, only that one draws the brush, and **no cell of a facet draws one** (Vega-Lite's test of the unit of the
  store never matches the name of a cell); with `"resolve": "union"` or `"intersect"` every view that has the
  selection draws its own, through its own scales. A point selection draws no brush.
- **`"bind": "scales"`.** An interval selection bound to the scales (zoom and pan in a browser) **sets the domain**
  of the continuous scales it projects over to the range of its `value`, and the marks of the view are clipped to the
  data rectangle; no brush is drawn. It applies only to a view that is the **whole view** (a single view, or the layers
  of a layered view at the top level): in a `concat`, a `facet`, or a `repeat` Vega-Lite pushes the state of the scale to
  the top level through signals, which hold nothing in a still picture, so the domains stay those of the data (the marks
  are clipped all the same, because the scale has the extent of a selection) and the selection reads as nothing. A
  scale that is not continuous cannot be bound and is reported as the warning `vl-param-selection-projection`. The
  `resolve` has to be `"global"`: any other makes the selection draw its brush instead.
- **A selection as the domain of a scale or the extent of a bin.** `{"scale": {"domain": {"param": "brush"}}}`, and
  `{"bin": {"extent": {"param": "brush"}}}` in an encoding or in a `bin` transform, follow the selection. The
  object may name a `field` or an `encoding` (the field the selection projects over that channel); with neither, or
  with an `encoding` that is not projected exactly once, the first projected field is used (reported as the warning
  `vl-param-selection-projection` when the selection projects several). A **domain** is the range (or the list of
  values) of that field, taken as the raw domain of the scale (see *A parameter as the domain of a scale* below). A
  **bin extent** only sets the **span** that chooses the bin width (Vega's `span` of the `bin` transform: the last end
  minus the first of the range; zero, or no value, is no span): the bins still start and stop at the extent of the
  data, as in Vega-Lite, and an encoding whose `bin` has such an extent also takes the range as the domain of its
  scale and clips its marks. A selection that holds nothing changes nothing, but the marks are still clipped. The
  selection may be declared in another view (a `vconcat` of a view with the selection and a histogram that bins by it);
  a **variable** parameter holding `[min, max]` works the same way.
- **Ignored without a report**: `on`, `clear`, `toggle`, `nearest`, `translate`, `zoom`, and the input widgets of
  `bind`. An animation selection (an `on` with a timer, or the `time` encoding) is reported
  (`vl-param-selection-animation`).
- **A legend bound to a selection.** `"bind": "legend"` (on a selection of `resolve` `"global"` that projects exactly
  one field that is not the row identity) makes the legend of the channel an interactive one in a browser. In its
  initial state the legend **dims the entries the selection does not hold**: with a selection that has tuples, the
  symbols and the labels of an entry whose value is not among the values the selection holds for the field get the
  opacity `config.legend.unselectedOpacity` (0.35 by default); a selected entry keeps its own (a symbol the opacity of
  the mark, a label 1). A selection with no tuples dims nothing. The test is Vega-Lite's: the value of the entry
  must be **strictly equal** to a held value (an ordinal entry `2` is not selected by the text `"2"`). A selection stores
  a date as a number of milliseconds: the entry of a discrete legend over a field of dates is that number, so the date
  selects it, while a tick of a time scale (a temporal color shown as symbols, or its gradient labels) is a date and is
  never selected. The field it reads is the `field` of the channel definition (not the projected field, so a selection
  over a field with a `timeUnit`, which stores its values under the derived field such as `month_d`, dims every entry
  while it has a tuple, as in Vega-Lite), and the
  projection must be that field or the channel. The labels of a gradient legend are dimmed by their tick value too.
  When the legends of several channels or layers merge, the first one decides; a legend that also explains `opacity`
  keeps its symbols (Vega-Lite drops the rule from them) and dims its labels. A legend binding without exactly one
  projected field is reported as the warning `vl-param-selection-projection` and is not applied.
- **Known differences.** The `resolve` of a predicate (`vlSelectionTest`'s third argument) is read from the `config` of
  the specification, not from the `Config` text of the render options (a static chart has the tuples of one unit, so
  the answer is the same); the same holds for the choice between `vlSelectionTest` and `vlSelectionIdTest`, which is
  made from the projection that the specification and its `config` give (the model applies a default projection of the
  host's `Config`, or a value of the host, and the identity test of a store that is projected over fields tests those
  fields, so the answer is the same); the finding `vl-param-selection-id` reads the effective configuration (the host's
  `Config` too), like the brush and `bind: "scales"`. The ends of a date-time
  object are read in the time zone of the chart (`VegaLiteRenderOptions.TimeZone`, UTC by default), where a browser
  reads them in its own. A brush that paints nothing is not made, where Vega-Lite makes a zero-size rectangle.

**Expression references.** An `{"expr": ...}` object (Vega-Lite's `ExprRef`) is a value that an expression gives. At
a **static site** (a property with one value for the whole chart) the expression runs **once per call**, with the
parameters at their initial state (or the override of the host), and its value is what the property holds: the
chart is the one that the literal value would give. The sites are the ones that Vega-Lite's schema allows an
`ExprRef` at, and the renderer reads:

<!-- markdownlint-disable MD013 -->
| Where | Properties |
| ----- | ---------- |
| The top-level view | `padding` and `background` |
| `title` | `text`, `anchor`, `angle`, `aria`, `color`, `dx`, `dy`, `font`, `fontSize`, `fontStyle`, `fontWeight`, `frame`, `limit`, `lineHeight`, `offset`, `orient`, `zindex`, and the subtitle properties `subtitleColor`, `subtitleFont`, `subtitleFontSize`, `subtitleFontStyle`, `subtitleFontWeight`, `subtitleLineHeight`, and `subtitlePadding` |
| The `mark` definition, a `point` or `line` overlay, the parts of a composite mark | The properties of a mark (`align`, `angle`, `baseline`, `cornerRadius`, `dx`, `dy`, `fillOpacity`, `font`, `fontSize`, `interpolate`, `limit`, `shape`, `size` of a mark that does not evaluate it for every row, `strokeCap`, `strokeDash`, `text`, `theta`, `radius`, `x`, `y`, ... 75 in all) except `color`, `fill`, `stroke`, `opacity`, `strokeWidth`, and the `size` of a symbol, line, trail, or text mark, which the marks evaluate **for every row** (see above) |
| `encoding`: a channel definition | `value` (of a channel other than `color`, `fill`, `stroke`, `opacity`, and the `size` of such a mark) and `datum` |
| An `axis` (and `config.axis*`) | The 65 properties of an axis that Vega-Lite allows (`labelAngle`, `labelFontSize`, `tickCount`, `values`, `domainColor`, `gridOpacity`, `offset`, `orient`, `titleAnchor`, ...) |
| A `legend` (and `config.legend`) | The 68 properties of a legend (`columns`, `symbolSize`, `labelFontSize`, `titleOrient`, `values`, `offset`, `padding`, ...) |
| A `header` of a facet (and `config.header*`) | The label and title properties (`labelFontSize`, `labelAngle`..., `titleFontSize`, `titlePadding`, ...), 20 in all |
| A `scale` | `align`, `base`, `clamp`, `constant`, `domain` (the whole array, or each entry), `domainMax`, `domainMid`, `domainMin`, `domainRaw`, `exponent`, `interpolate`, `nice`, `padding`, `paddingInner`, `paddingOuter`, `range` (each entry), `rangeMax`, `rangeMin`, `reverse`, `round`, `scheme`, `zero` |
| `view` (and `config.view`) | The stroke, fill, opacity, and corner radius of the plot background |
| `config` | `background`, `lineBreak`, `padding`, and the properties of `title`, `axis*`, `legend`, `header*`, `view`, `scale`, `mark`, the mark types, and named `style`s |
<!-- markdownlint-enable MD013 -->

- The value is whatever the expression gives: a text, a number, a boolean, `null`, an array, or an object. A number
  that is not finite (JSON has none) leaves the property unset. The `text` of a title or a mark takes the JavaScript
  text of a number or a boolean. A **date** (`datetime(...)`) is its number of milliseconds only in a scale domain
  (`domain`, `domainMin`, `domainMax`, `domainMid`, `domainRaw`); anywhere else it has no value a property can read,
  so the reference is left and reported as the warning `vl-expr-ref`. A result longer than `MaxStringLength` is a
  located `VegaLiteRenderException`.
- An expression is compiled when the document is opened, like every other (a syntax error, or a name that no view
  declares as a parameter, is a `VegaLiteSpecException`). It reads parameters, never `datum`: a static site has no row,
  so a reference that reads `datum` is **not evaluated**, the property keeps its default, and the reference is reported
  (`vl-expr-ref`). Each evaluation counts against `MaxEvaluationSteps`.
- `width` and `height` are not expression references in Vega-Lite (a size is a number, `"container"`, or a step), so a
  chart cannot be sized by a parameter; an `{"expr": ...}` there is reported (`vl-expr-ref`). The `labelExpr` of a
  guide is a text, not a reference (see *Vega-Lite Label Expressions*).
- An `{"expr": ...}` in the `Config` text of the render options is **not evaluated** (the text is merged as it is) and is
  reported (`vl-expr-ref`, with the pointer inside the text in the message); the `config` of the specification is.

**A parameter as the domain of a scale.** A scale `domain` of the form `{"param": "name"}` takes the **value of the
parameter** `name` as the scale's **raw domain**: for a variable parameter its value (an array), for a selection
parameter the range (or list of values) of one of its fields (see *A selection as the domain of a scale or the extent of
a bin* above); the `extent` of an encoding's `bin` and an interval selection bound to the scales do the same. The raw
domain replaces the domain and **nothing adjusts it
afterwards** (no `zero`, `domainMin`, `domainMax`, `domainMid`, `padding`, or `nice`), as in Vega. A `domainRaw` that
the scale writes itself works the same way and wins over a parameter. The raw domain of a continuous scale (linear,
log, pow, sqrt, symlog, time, utc, and the sequential scale of a color) is two numbers or dates (more than two are a
piecewise domain; a date is its instant); that of a band, point, or ordinal scale is the list of categories in order.
The discretizing scales (`quantize`, `quantile`, `threshold`, `bin-ordinal`) keep their own domain. A parameter
without a value, and a selection that holds nothing for the field, leave the domain of the scale as it is; a value that
cannot be a domain (not
an array, fewer than two numbers, a non-numeric entry, a date object of the specification) is reported as the warning
`vl-scale-domain-raw` and the scale keeps its own domain (Vega would draw nothing). When the `domain` of the x or y
scale is a parameter (or its bin has an `extent` that is one, or a selection is bound to it), the marks of the view are
**clipped to the data rectangle**, as Vega-Lite does for the extent of
a parameter, so the marks that the domain leaves out are not drawn outside the view (this holds also when the parameter
has no value). A layered view that shares the scale takes the raw domain of a layer for all of them.

### Vega-Lite Transforms

The `transform` array of the view is run **in the order of the text**, once per measure or render call, on the rows
that the data source gave and **before** the scales are built, so every scale, axis, and mark sees the transformed rows.
Each transform is applied to the whole table that the previous one produced. An entry that names a transform that is
not supported (`regression`, `loess`, `density`, `quantile`, `extent`) is reported (`vl-transform-<name>`)
and **skipped**; the later transforms still run on the rows without it, so the chart is only approximate. The
`transform` of
a layered view, a concatenation, a facet, or a repeat runs on the rows first, and then the `transform` of each layer or
child (see *Vega-Lite Layers and Composite Marks* and *Vega-Lite Composition*). The aggregating transforms
(`aggregate`, `joinaggregate`, `window`, `bin`, `stack`, and `impute`) are described in *Vega-Lite Aggregation,
Binning, and Stacking*, and the `timeUnit` transform in *Vega-Lite Time and Time Zones*.

The shape of an entry is checked when the document is opened: an entry that is not an object, or has none of the
transform properties, or a transform of the wrong shape (a `fold` that is not an array, a `calculate` without `as`, a
predicate that is neither an expression nor an object, a pivot without `value`, a lookup without `from`) is a
`VegaLiteSpecException` at the offending value; the first transform property of an entry names its transform. A field
name is a Vega-Lite field path (`a`, `a.b`, `a\.b`).

<!-- markdownlint-disable MD013 -->
| Transform | Properties | Behavior and differences from Vega-Lite |
| --------- | ---------- | --------------------------------------- |
| `calculate` | `calculate` (an expression), `as` (required, a non-empty text) | Stores the result in the field `as` for every row, replacing a field of that name. `as` is a literal name (a dot is part of the name, not a path). The result may be a number, text, boolean, `null`, array, or object; a regular expression cannot be stored and gives `null` with the warning `vl-expr-runtime` |
| `filter` | `filter`: an expression text or a predicate (below) | Keeps the rows for which an expression is truthy (JavaScript truthiness: not `null`, `false`, `0`, `NaN`, or the empty text) or a predicate holds |
| `fold` | `fold` (an array of fields), `as` (one or two names; `["key", "value"]` by default) | Turns each row into one row per folded field, in the order of the rows and then of the fields: the key is the field name as written and the value the cell (`null` for a missing field). Every field of the row stays, the folded ones included |
| `flatten` | `flatten` (an array of fields), `as` (at most as many names as fields; the field itself by default) | Turns each row into as many rows as its longest array. The arrays of several fields are expanded in parallel (shorter ones are padded with `null`); a value that is not an array counts as an array of one element; `null`, an empty array, and a missing field count as none, and a row in which every field is empty disappears. A trailing `null` element of an array in the data is not kept |
| `sample` | `sample` (a number of rows, not negative) | Keeps at most that many rows, in the order of the data. See *Determinism* below |
| `pivot` | `pivot` (the field whose values become columns), `value` (required), `groupby` (fields), `limit` (0 or absent for no limit), `op` (`sum` or `count`) | One row per distinct `groupby` combination (in the order of first appearance; all rows form one group without `groupby`), with the `groupby` fields and one column per distinct pivot value, named by the text of the value and in ordinal order. A cell is the sum of the numbers of `value` of the group (the default; numeric text and booleans count as numbers) or the number of rows (`count`); a combination without rows is 0. Any other field is dropped. A pivot `op` other than `sum` or `count` is reported (`vl-transform-pivot-op`) and `sum` is used |
| `lookup` | `lookup` (the field of the data), `from.data` (a data source), `from.key`, `from.fields`, `as`, `default` | Copies the fields `from.fields` of the **first** row of the other data whose `from.key` has the same value into the fields `as` (the field names when `as` is absent). Without `from.fields` the whole row goes into one field, `data` by default, as an object. A row with no match gets `default` (a text, number, boolean, or `null`; `null` by default). Vega-Lite takes the last row for a repeated key; this package takes the first |
| `timeUnit` | `timeUnit` (a time unit name or object), `field` (required), `as` (required, a non-empty text) | Adds two date fields to every row: `as`, the start of the time unit that contains the date in `field`, and `as_end`, the exclusive end of that unit (for example `month` and `month_end`). The field is read as a date (it is date-parsed implicitly, and a number is milliseconds); a row whose value is not a date, or is outside the years 1 to 9999, gets `null` in both fields. See *Vega-Lite Time and Time Zones* |
<!-- markdownlint-enable MD013 -->

**Filter predicates.** A field predicate is an object with a `field` and one comparison; `and`, `or`, and `not`
combine predicates, and `param` names a parameter. Comparisons follow JavaScript, so a value is compared by kind:

<!-- markdownlint-disable MD013 -->
| Predicate | Holds when |
| --------- | ---------- |
| `{"field": f, "equal": v}` | The value is strictly equal (`===`) to `v`: the number 1 does not equal the text `"1"` |
| `{"field": f, "lt": v}`, `"lte"`, `"gt"`, `"gte"` | The relational comparison (`<`, `<=`, `>`, `>=`) of JavaScript is true, so a missing value compares as 0 with a number |
| `{"field": f, "range": [low, high]}` | The value is between the two values, **inclusive**; a `null` endpoint is open and endpoints in the wrong order are swapped |
| `{"field": f, "oneOf": [v, ...]}` | The value is strictly equal to one of the values |
| `{"field": f, "valid": true}` | The value is not `null` and not `NaN` (`false` selects the others) |
| `{"and": [p, ...]}`, `{"or": [p, ...]}`, `{"not": p}` | All, any, or none of the predicates hold; each part is a predicate or an expression text |
| `{"param": "name"}` | The variable parameter is truthy (see *Vega-Lite Parameters*), or the selection parameter selects the row (see *Selection parameters* in *Vega-Lite Parameters*; `"empty": false` makes an empty selection select no row) |
| `{"selection": "name"}` (also with `and`, `or`, `not`) | The legacy form of the selection predicate |
<!-- markdownlint-enable MD013 -->

When an object has several comparisons, the first of `equal`, `lt`, `lte`, `gt`, `gte`, `range`, `oneOf`, `valid` is
used. A predicate may carry a `timeUnit`, and its value may be a date-time object (`{"year": 2020}`): the comparison is
then made on the date, or on the parts that the unit keeps (see *Vega-Lite Time and Time Zones*). The `timeUnit` of a
predicate whose unit has a `week` or `dayofyear` part does not apply those parts, as in Vega-Lite, and is reported as
the warning `vl-predicate-timeunit-part`.

**Sorting by a field.** The `sort` of a discrete channel takes these forms:

<!-- markdownlint-disable MD013 -->
| `sort` | Order of the categories |
| ------ | ----------------------- |
| absent or `"ascending"`, `"descending"` | By the values of the channel's field |
| `null` | In the order of first appearance in the (transformed) rows |
| An array of values | In the order of the array; values that it does not list come first |
| `"y"`, `"-y"` (a channel name, a leading minus for descending), or `{"encoding": "y", "order": ...}` | By the values of another channel of the same encoding: its aggregate, or `sum` when it is the stacked channel, or else `min`, of its field over the rows of each category |
| `{"op": ..., "field": ..., "order": ...}` | By an aggregate of a field over the rows of each category |
<!-- markdownlint-enable MD013 -->

In the object form `op` is any aggregate operation that gives an orderable result (every one in *Vega-Lite Aggregation,
Binning, and Stacking* except `argmin`, `argmax`, `values`, `exponential`, and `exponentialb`) and is `min` by default,
`field` is the field to aggregate (the channel's own field by default), and `order` is `"ascending"` (the default) or
`"descending"`. The aggregate is computed over the rows **before** the encoding's own aggregate and stack, as Vega-Lite
does, with the rules of the aggregate operations (only valid values of the field count, and a category with no valid
value has a `null` result, which sorts first in either direction). Categories with the same value keep their order of
first appearance. A sort by a channel that is not in the encoding or has no field, or by an unsupported `op`, is
reported (`vl-encoding-sort`, with a message that names the channel) and the categories are sorted ascending. A `sort`
array may hold only strings, numbers, booleans, and `null`; an object or an array in it is a specification error at that
item (the one exception is a date-time object such as `{"year": 2024}` on a field of type `temporal`, which is read as a
date; see *Vega-Lite Time and Time Zones*). An array that mixes kinds of values (`[1, "b"]`) still works, because values
are matched by kind,
and is reported as the warning `vl-encoding-sort-mixed-types`. A `sort` array, channel, or object on a quantitative
field has no categories to order on its continuous scale and is ignored with the warning `vl-encoding-sort-ignored`. A
key of a `sort` object that Vega-Lite does not define is the warning `vl-unknown-property`. A `sort` array may list at
most `MaxScaleDomainSize` values.

**Signed stacks.** The negative values of a stacked measure stack **downward** from zero (leftward for a horizontal
chart) separately from the positive values, which stack upward from zero, so that a chart of takings and outgoings hangs
the outgoings below the zero line (`center` and `normalize` use the absolute values). The axis covers both ends.

**Determinism and limits.** `sample` is a reservoir sample whose generator is seeded from the **JSON pointer of the
transform** (for example `/transform/0`), so the same chart always keeps the same rows, on every machine, and two
`sample` transforms of one chart draw independent samples; where Vega-Lite samples at random, a chart here does not
change between renders. A size of at least the number of rows keeps every row. The rows of every table that a transform
derives count against `MaxDataRows` (a `fold` or `flatten` needs room for the input and the output together, and a
`filter`, `sample`, or `pivot` gives the rows it drops back), the lookup source counts like any data source (and a
local file in it is read under the same sandbox, file count, and size limits), the fields against `MaxFieldCount`, and a
derived table may hold at most 2^25 (33,554,432) cells, rows times fields. The counts are checked **before** the memory
is allocated, and a failure is a `VegaLiteRenderException` located at the transform. A `pivot` that would output more
than `MaxFieldCount` fields fails the same way; the check counts the columns **after** `limit` keeps the first `limit`
distinct values in ordinal order, so a pivot with many distinct values and a small `limit` succeeds, and only a pivot
without a `limit` (or with one above the bound) and more distinct values than the bound allows fails. Recoverable
findings of a pivot are warnings (`vl-transform-runtime`): rows whose pivot value is `null` are skipped, the columns
beyond `limit` are cut, and a pivot value that has the name of a `groupby` field replaces that field. Cancellation is
checked between the transforms and every 1,024 rows.

Examples:

```json
{ "fold": ["north", "south"], "as": ["region", "crates"] }
{ "flatten": ["tags"], "as": ["tag"] }
{ "pivot": "region", "value": "crates", "groupby": ["week"] }
{ "lookup": "stall", "from": { "data": { "name": "owners" }, "key": "stall", "fields": ["owner"] }, "default": "-" }
{ "filter": { "and": [ { "field": "crates", "range": [20, 80] }, { "not": { "field": "stall", "equal": "East" } } ] } }
{ "sample": 100 }
```

Examples 33 and 34 draw charts with parameters, expressions, filters, calculates, a sort by a field, and the
aggregating transforms. Example 48 draws charts whose title, padding, and axis read parameters through expression
references, and whose scale takes its domain from a parameter.

### Vega-Lite Aggregation, Binning, and Stacking

The transforms `aggregate`, `joinaggregate`, `window`, `bin`, `stack`, and `impute` compute over **groups of rows**, and
the encoding properties `aggregate`, `bin`, and `stack` ask for the same computations without a `transform`. Both use
one engine, so the rules below hold for both. Every one of them is deterministic and is bounded by the limits of the
call (see *Limits* below).

**Groups.** A group is the set of rows that have the same values in the `groupby` fields (one group of all rows when
there is no `groupby`). Unlike Vega, which joins the text of the values, a group is identified by the **structure** of
the values, so the number 1 and the text `"1"` are two groups, an array or object compares by its content, and two
`NaN` values are the same key. The groups are in the order in which they first appear, so the rows that an
`aggregate` outputs never depend on the order of the keys of an object. An empty input has no groups, also without a
`groupby`.

**`aggregate`.** `{"aggregate": [{"op": ..., "field": ..., "as": ...}], "groupby": [...]}` outputs **one row per group**
with the `groupby` fields (a nested field keeps its path) followed by one field for every operation. `as` is a
literal name; when it is absent the name is the operation, an underscore, and the field (`sum_units`, `mean_x`;
`average` is `mean`), or just `count` for a `count` without a field. A later field of the same name replaces an earlier
one. `joinaggregate` takes the same arguments but keeps **every row** in its place and adds the result of its group to
each row (the `values` array and the row of an `argmin` or `argmax` are shared by the rows of a group).

All 24 aggregate operations are available. A value is **missing** when it is `null` or the empty text, **valid** when it
is neither missing nor `NaN`, and `NaN` is neither:

<!-- markdownlint-disable MD013 -->
| Operation | Result |
| --------- | ------ |
| `count` | The number of rows of the group (needs no `field`; every other operation does) |
| `valid`, `missing` | The number of valid values, and of missing values |
| `distinct` | The number of distinct `String(value)` texts of the field over all rows (`null` counts as the text `null`) |
| `sum`, `product`, `mean` (or `average`) | Over the valid values; `null` when the group has none |
| `variance`, `variancep`, `stdev`, `stdevp` | Sample and population variance and standard deviation, computed incrementally; `null` unless the group has more than one valid value (`variancep` and `stdevp` need only one and are `0` for a single value) |
| `stderr` | The standard error of the mean, the square root of `variance / valid` |
| `median`, `q1`, `q3` | The 0.5, 0.25, and 0.75 quantiles of the valid numbers, interpolated as in d3-array (the R-7 method: position `(n - 1) * p`) |
| `ci0`, `ci1` | The lower and upper bound of the 95% bootstrap confidence interval of the mean (1,000 resamples; see the deviations below) |
| `min`, `max` | The smallest and largest valid value by the relational operators of JavaScript, so texts and dates are compared as in Vega; the first wins a tie |
| `argmin`, `argmax` | The **whole row** (an object) that holds the smallest or largest value of the field; the first wins a tie. The default name is `argmax_field` |
| `values` | An array of the values of the field of all rows |
| `exponential`, `exponentialb` | The exponentially weighted mean `exp * (1 - r) / (1 - r^n)` and moving average `exp * (1 - r)` with the weight `r` given by `param` (a number above 0 and at most 1; **0.5** when `param` is absent) |
<!-- markdownlint-enable MD013 -->

An operation that is not in the table, a missing `field`, or an `as` that is not a non-empty text is a
`VegaLiteSpecException` at the offending value.

**`window`.** `{"window": [{"op": ..., "field": ..., "param": ..., "as": ...}], "groupby": [...], "sort": [...],
"frame": [start, end], "ignorePeers": false}` adds one field per operation to **every row** and keeps the rows in their
input order. Within a group the rows are ordered by `sort` (a list of `{"field": ..., "order": ...}` with `order` of
`"ascending"` or `"descending"`; null, `NaN`, and then numbers and text; a stable order, so rows that compare equal keep
the order of the data). Every aggregate operation is also a window operation over the **frame**, and these 13
operations only exist as window operations. The default name is the operation, an underscore, and the field
(`rank`, `sum_orders`, `lag_x`):

<!-- markdownlint-disable MD013 -->
| Operation | Needs | Result for a row |
| --------- | ----- | ---------------- |
| `row_number` | | The position in the group, from 1 |
| `rank`, `dense_rank` | | The position of the first row with the same sort values, counting rows (`rank`) or distinct sort values (`dense_rank`), from 1 |
| `percent_rank` | | `(rank - 1) / (rows - 1)`; `NaN` for a group of one row, as in Vega |
| `cume_dist` | | The share of the group up to and including the last peer of the row |
| `ntile` | `param` | The number of the bucket, from 1 to `param`, that the row falls in |
| `lag`, `lead` | `field`; `param` (the offset, 1 by default) | The value of the row `param` rows before or after, or `null` beyond the group |
| `first_value`, `last_value` | `field` | The value in the first or last row of the frame; `null` for an empty frame |
| `nth_value` | `field`, `param` | The value in row `param` (from 1) of the frame; `null` if the frame is shorter |
| `prev_value`, `next_value` | `field` | The value of the row when it is not `null`, else the closest earlier or later value that is not `null` |
<!-- markdownlint-enable MD013 -->

The **frame** is two row offsets from the current row, either of them `null` for no limit: `[null, 0]` (the default)
runs
from the start of the group to the current row, `[null, null]` is the whole group, and `[-1, 1]` is the row, its
predecessor, and its successor. An offset is clamped to the group, and a frame that is empty gives the empty result of
the operation (`count` 0, `sum` `null`). When a `sort` is given and `ignorePeers` is not `true`, the ends of the frame
are widened to include the **peers** of the rows they land on (rows with the same sort values), so a running `sum` over
rows with equal sort values gives the same total to all of them; with `ignorePeers` set to `true` it does not. The
parameters of `ntile`, `lag`, `lead`, and `nth_value` are integers from 1 up (`ntile` and `nth_value` need one). The
frames that start at the beginning of the group are computed incrementally.

**`bin`.** `{"bin": true | {...}, "field": ..., "as": ...}` writes the lower and the upper boundary of the bin of every
row into two new fields and keeps all rows. The names are `bin0` and `bin1` when `as` is absent, `name` and `name_end`
when `as` is one text, and the two names of an array of two. The bin parameters are:

<!-- markdownlint-disable MD013 -->
| Parameter | Meaning | Default |
| --------- | ------- | ------- |
| `maxbins` | The most bins (a number of at least 2) | 10 (the bins of an encoding on `x` and `y` also default to 10) |
| `step` | The exact bin width (greater than 0); `maxbins` then plays no part | Computed from the extent |
| `steps`, `minstep` | The allowed widths (an array of up to 1,024 numbers) and the smallest width; `steps` takes the largest allowed width below `span / maxbins` | None |
| `base`, `divide` | The base of the step (greater than 1) and the divisors tried to refine a step (up to 16 numbers) | `10` and `[5, 2]` |
| `nice` | Whether the extent is rounded out to whole steps | `true` |
| `anchor` | A number that a bin boundary is aligned to; values below the shifted first bin go to the infinite bin | None |
| `extent` | The `[min, max]` that decides the bins (two numbers in ascending order), or `{"param": ...}` of a selection or a variable parameter: only the **span** of the parameter's range chooses the bin width and the bins still cover the data (see *Selection parameters* in *Vega-Lite Parameters*) | The extent of the finite numbers of the field |
<!-- markdownlint-enable MD013 -->

The step is the largest "nice" width that gives at most `maxbins` bins, found by the algorithm of Vega: over the data
`[0, 100]` ten bins are 0 to 100 by 10 and twenty bins are by 5, and `[3.7, 98.2]` also gives 0 to 100 by 10. A value
`v` goes to the bin that holds it, with the **upper boundary of the last bin included**; a value below the extent is
bin `-Infinity` and a value above it `+Infinity`, and a value that is `null`, not a number, or not finite gets `null`
in both fields. A field without a finite value gives `null` everywhere, and an extent that cannot be divided into
finite steps, or more bins than `VegaLiteLimits.MaxScaleDomainSize` (100,000), fails the render with a located
`VegaLiteRenderException`.

**`stack`.** `{"stack": "units", "groupby": [...], "sort": [...], "offset": "zero", "as": [start, end]}` adds the
lower and upper end of each row's segment and keeps all rows in their place. Within a group the rows are stacked in the
order of `sort` (the table order when it is absent), and the first row sits at the bottom. The names are
`units_start` and `units_end` by default (`as` of one text gives `name` and `name_end`, an array of two gives both);
this
differs from Vega, which writes `y0` and `y1`. The value of a row is read as a number (`null` is 0, numeric text is
read, other text is `NaN`).

<!-- markdownlint-disable MD013 -->
| `offset` | Segments |
| -------- | -------- |
| `zero` (default) | Positive values stack up from 0, in order, and negative values stack down from 0 separately; `NaN` joins the positive stack and spoils its sum |
| `center` | The absolute values are stacked and each group is centered on the middle of the group with the largest total |
| `normalize` | The absolute values are scaled so that every group totals 1; a group that totals 0 gets `NaN` ends |
<!-- markdownlint-enable MD013 -->

**`impute`.** `{"impute": "units", "key": "week", "groupby": ["stall"], "keyvals": [...], "method": "value",
"value": 0, "frame": [null, null]}` fills the gaps: for every group (one group without `groupby`) and every value of the
**key domain** that the group lacks, a new row is **appended after the input rows** (the input order is kept, then the
groups in order, then the keys in order) with the group fields, the key, and the imputed field, and `null` in every
other field. The key domain is `keyvals` (an array, or a sequence `{"start": 0, "stop": n, "step": 1}` of
`ceil((stop - start) / step)` numbers, bounded by `MaxDataRows` before it is created) followed by the keys seen in the
data in order of appearance; a repeated key counts once. The `method` is `value` (the default; the imputed field takes
`value`, which is `0` when absent and `null` when written `null`) or `mean`, `median`, `max`, or `min`, which replace
**every** `null` of the field (the rows that already had one too) with that aggregate of the group over `frame`
(`[null, null]`, the whole group, by default) in the order of the rows, as Vega-Lite does with a window and a formula. A
field that no row has is added.

**Encoding properties.** The same computations run from an `encoding` when a channel asks for them, in the order of
Vega-Lite: the explicit `transform` array, then the **bins** of the encoding, then (for the scales that need the
unaggregated rows, see `sort` and `scale.domain: "unaggregated"`) a copy of the table, then the implicit
**aggregate**, then the **stack**, and then the marks.

- **`aggregate`** on a channel definition (all 24 operations; `count` needs no `field`, and an object `{"argmax": ...}`
  or `{"exponential": r}` is the same as the transform) turns the chart into a summary: the rows are grouped by the
  fields of **every other** channel definition that is not itself aggregated (including `detail` and `tooltip`, the
  field of a binned channel as its two boundaries, and every definition of an array channel), and each aggregated
  definition becomes a measure. Without any grouping field the chart is one group, and without rows it is empty.
- **`bin`** on an `x` or `y` definition (`true` or the parameters above; an extent as `{"param": ...}` sets the span that
  chooses the bin width and the domain of the scale, see *Vega-Lite Parameters*) bins the field of the channel. A
  quantitative channel is a continuous axis from the
  first to the last boundary of the bins (no zero, no nice rounding of the domain), a bar or rect fills the bin less the
  `binSpacing` (1 pixel for a bar and 0 for a rect or tick), and a point or rule sits in the middle of the bin; a bar
  that would be thinner than `minBandSize` (0.25) is widened to it. An `ordinal` or `nominal` binned channel is
  a band scale of the texts `start – end` (with an en dash, formatted like the axis labels). The default
  axis of a binned channel has no grid and about one tick per 10 pixels.
- **`"binned"`** (`bin: "binned"`) says that the data is **already** binned: the field is the start of a bin and the
  secondary channel `x2` or `y2` its end, or `bin.step` gives the width; the domain is the extent of both. A bar, rect,
  or rule with `x2` or `y2` is drawn as a **range** between the two values on every range channel, for any field,
  datum, or pixel `value`; a row whose end is invalid is not drawn.
- **`stack`** is on by default for bars (`zero`) and available with `stack: "zero"`, `"center"`, `"normalize"`, or
  `true` (zero) for the point-like marks, ticks, and rules; `false` or `null` turns it off. The stacked channel is the
  quantitative `x` or `y` (the aggregated one when both are), the groups are the values of the other position channel
  (the two boundaries of a binned one), and the layers are ordered by the fields of the other channels, such as
  `color` (descending when the stacked channel is `y`, ascending otherwise), or by the `order` channel when there is
  one, so the first layer is at the bottom. A chart with one row per category is unchanged (the segment is from 0 to
  the value). No stack is made when the mark has a range (`x2` or `y2`), when the stacked channel has an aggregate and
  no other channel groups it, or for a mark that does not stack. The domain of the stacked axis is from the lowest start
  to the highest end (0 to 1 for `normalize`, with the format `config.normalizedNumberFormat`, `.0%` by default). A
  stack on a log, pow, sqrt, or symlog scale is not applied (Vega-Lite does not stack on a non-linear scale) and is
  reported
  (`vl-encoding-stack-nonlinear`); the marks start at the baseline.

The derived fields use the names of Vega-Lite: `sum_units`, `mean_x`, `__count` for a count, `bin_maxbins_10_weight`
and `bin_maxbins_10_weight_end` for a bin (the parameters are in the name, so two channels with the same bin share
it), and `<field>_start` and `<field>_end` of a stack. The default **titles** follow Vega-Lite as well: `Sum of units`,
`Q1 of units`, `Count of Records` (`config.countTitle`), `weight (binned)`, `a for max b` for an `argmax`, and the two
titles of a range joined as `from, to`; an explicit `title` always wins, and `config.fieldTitle` selects `plain` or
`functional` titles (`SUM(units)`).

**Limits, determinism, and failures.** Every table that these transforms derive counts against `MaxDataRows` (a group
counts as a row while it is built), the fields against `MaxFieldCount`, and the cells (rows times fields, and the
working state of every group and operation) against the limit of 2^25 values; the work of grouping, sorting, windows,
and the bootstrap is charged to `MaxEvaluationSteps`, text is bounded by `MaxStringLength`, and cancellation is checked
every 1,024 rows. A bin with too many bins, a stack that is too large, or a sort of a scale over too many categories
(`MaxScaleDomainSize`) fails the render with a `VegaLiteRenderException` located at the transform or the encoding, and
the message names the limit. The result never depends on the machine, the culture, or the order of the keys.

**Deviations from Vega and Vega-Lite.** The package differs in these documented ways:

- **Group keys** are structural and the groups are in order of first appearance (see *Groups*), where Vega joins the
  text of the values and uses the order of the keys of an object.
- **`ci0` and `ci1`** use a **deterministic** bootstrap: a generator seeded from the JSON pointer of the entry and the
  group, with 1,000 resamples; the same chart always gives the same interval, where Vega uses `Math.random`.
- **`stack` names** default to `field_start` and `field_end` (Vega writes `y0` and `y1`).
- **`exponential` and `exponentialb`** take the weight 0.5 when `param` is absent (Vega-Lite does not require one).
- **Window frames** that are empty give `null` for the positional operations (Vega reads past the group), and
  `cume_dist` and `ntile` count all the peers of the first rows (Vega under-counts them).
- **Bin** growth is bounded, `bin` needs no `as` (the names are `bin0` and `bin1`), and an anchor shifts the bins
  exactly as Vega does.
- A `bin: "binned"` on a channel other than `x` and `y` and an encoding `impute` are reported (see the identifiers
  below). A field with a `timeUnit` is grouped by the start of its unit (and its end, when the mark spans the unit; see
  *Vega-Lite Time and Time Zones*), and a `bin` on a field wins over its `timeUnit`.

### Vega-Lite Legends, Color Schemes, and Scales

**Legends.** A channel that a field (or a `datum`) drives and that has a scale gets a **legend** that explains it, as
in Vega-Lite: the channels `color`, `size`, `shape`, `fill`, `stroke`, `strokeDash`, `strokeWidth`, and `opacity`
(`fillOpacity` and `strokeOpacity` are drawn, but have no legend). A channel with a literal `value`, or with `legend`
or `scale` set to `null`, has none, and `config.legend.disable` switches every legend off. Which kind is drawn follows
the scale:

<!-- markdownlint-disable MD013 -->
| Scale | Legend |
| ----- | ------ |
| A discrete scale (ordinal, band, point), and a continuous `size`, `opacity`, `strokeWidth`, or `angle` scale | A **symbol legend**: one symbol and one label per value (the values of the domain, or the ticks of a continuous scale, about five); `legend.values` lists them explicitly. More than `symbolLimit` (30) entries keep the first ones and end with an entry such as `…3 entries` |
| A sequential or diverging color scale (a quantitative or temporal color) | A **gradient legend**: a bar of the colors of the scale, 16 pixels thick and by default 200 long (the chart height, limited to 64 to 200 pixels, for a vertical bar), with labels at the ticks (for a temporal field the ticks and labels are dates, see *Vega-Lite Time and Time Zones*). `legend.type: "symbol"` gives a symbol legend of ticks instead |
| A quantize, quantile, or threshold color scale | A **discrete gradient legend**: one band per class, with the class limits as labels |
| A bin-ordinal color scale (a binned quantitative color) | A symbol legend with one entry per bin, labeled with the range of the bin (`0 – 10`, with a `legend.format` or the number text) |
<!-- markdownlint-enable MD013 -->

Channels that use the same field with the same domain (the same `scale.domain` and `sort`, and the same binning) are
**merged** into one legend, whose title merges the titles of the channels (a written title wins over a derived one;
derived titles join with a comma) and whose symbols combine the channels
(for example the color and the shape of one field); channels that ask for different `orient` values stay apart, and a
merge of different kinds is a symbol legend. The title is the legend's `title`, else the title of the channel, else the
default title of the field; `null` or an empty text gives none. The **placement** is the `orient`: `right` (the
default), `left`, `top`, `bottom`, the corners `top-left`, `top-right`, `bottom-left`, and `bottom-right`, or `none`
(at `legendX` and `legendY`). Legends of one side form a column (or, for `top` and `bottom`, a row) with 8 pixels
between them, placed at the `offset` (18 pixels by default, `config.legend.layout.offset`) from the data rectangle and
its axes. A legend is **part of the picture**: with the default `autosize: pad` its width (or height) is added to the
chart, so `Measure` and `Render` give a larger picture, and with `autosize: none` it can stick out of the canvas as in
Vega-Lite.

<!-- markdownlint-disable MD013 -->
| Legend properties | Meaning |
| ----------------- | ------- |
| `type`, `orient`, `direction`, `title`, `values`, `format`, `formatType`, `tickCount`, `tickMinStep`, `zindex`, `offset`, `padding`, `legendX`, `legendY` | What and where; `format` is a d3 number format of the labels (a d3 time format for dates, see *Vega-Lite Time and Time Zones*) |
| `columns`, `columnPadding`, `rowPadding`, `gridAlign`, `symbolLimit` | The grid of symbol entries (`columns` splits a long vertical legend) and the entry limit |
| `symbolType`, `symbolSize`, `symbolOffset`, `symbolStrokeWidth`, `symbolOpacity`, `symbolFillColor`, `symbolStrokeColor`, `symbolDash`, `symbolDashOffset` | The symbols (a symbol name; see the shapes below) |
| `gradientLength`, `gradientThickness`, `gradientOpacity`, `gradientStrokeColor`, `gradientStrokeWidth` | The gradient bar |
| `labelAlign`, `labelBaseline`, `labelColor`, `labelFont`, `labelFontSize`, `labelFontStyle`, `labelFontWeight`, `labelLimit`, `labelOffset`, `labelOpacity`, `labelOverlap`, `labelSeparation` | The labels (overlapping gradient labels are thinned out as on an axis) |
| `titleAlign`, `titleAnchor`, `titleBaseline`, `titleColor`, `titleFont`, `titleFontSize`, `titleFontStyle`, `titleFontWeight`, `titleLimit`, `titleLineHeight`, `titleOpacity`, `titlePadding` | The title |
| `fillColor`, `strokeColor`, `cornerRadius`, `aria`, `description` | The box around the legend, and accessibility text that a still picture does not use |
<!-- markdownlint-enable MD013 -->

Every property can also be set for all legends in `config.legend` (with the `config.legend.layout` object for the
placement of the groups of legends, and the defaults of Vega-Lite and Vega). `legend.labelExpr` is evaluated (see
*Vega-Lite Label Expressions*; there is no `config.legend.labelExpr`, as in Vega-Lite 6). `legend.encode`, a
`legend.formatType` other than `number`, `time`, and `utc`, a `titleOrient` other than `top`, and `clipHeight` are
accepted, **reported**
(`vl-guide-encode`, `vl-format-type-custom`, `vl-guide-legend-titleorient`,
`vl-guide-legend-clipheight`), and not drawn.

**Scale types.** An explicit `scale.type` is used when Vega-Lite accepts it for the channel and the field type; any
other is replaced by the default type with the warning `vl-scale-type-ignored`:

<!-- markdownlint-disable MD013 -->
| Channels | Default scale | Other types that are accepted |
| -------- | ------------- | ----------------------------- |
| `x`, `y` | `linear` for a quantitative field; `time` for a temporal one (`utc` when its time unit is a `utc` unit); `band` (bar, rect, tick) or `point` for a discrete one | `log`, `pow`, `sqrt`, `symlog`; `band` and `point` for a discrete field; a temporal field accepts only `time` and `utc` |
| `color`, `fill`, `stroke` | `sequential` for a quantitative or temporal field (a gradient); `bin-ordinal` when the field is binned; `ordinal` for a nominal or ordinal one | `linear`, `log`, `pow`, `sqrt`, `symlog` (a sequential scale with that transform), `quantize`, `quantile`, `threshold` for a quantitative field; a temporal field accepts only `time` and `utc`, which are drawn as a sequential scale over the dates |
| `size`, `opacity`, `fillOpacity`, `strokeOpacity`, `strokeWidth`, `angle` | `linear` for a quantitative field; `time` (or `utc`) for a temporal one; `point` for a discrete one | `log`, `pow`, `sqrt`, `symlog`, `quantize`, `quantile`, `threshold`, `bin-ordinal`, `ordinal`, `band`, `point` for a quantitative field; `time` and `utc` for a temporal one |
| `shape`, `strokeDash` | `ordinal` (a quantitative or temporal field is accepted with the warning `vl-scale-type-ignored`, because the channel shows categories) | `quantize`, `quantile`, `threshold` for a quantitative field |
<!-- markdownlint-enable MD013 -->

A **diverging** color scale is a sequential scale with a `domainMid` (the value at the middle of the scheme). The
**discretizing** scales cut a quantitative domain into classes that each get one value of the range: `quantize` into
classes of equal width, `quantile` into classes that hold equal numbers of values (the quantiles are those of
d3-array), and `threshold` at the cut points of an explicit `domain` (one more class than cut points). The number of
classes is the length of an explicit `range`, else the `count` of a scheme object, else 4 (`config.scale.quantizeCount`
and `quantileCount`). A `log` scale takes `base` (10), `pow` an `exponent` (1), `sqrt` is `pow` of 0.5, and `symlog` a
`constant` (1); `domainMin`, `domainMax`, `rangeMin`, and `rangeMax` replace an end of the domain or the range, `zero`
and `nice` apply to the continuous types, and a `reverse` reverses the range. The `time` and `utc` scales of a temporal
field are described in *Vega-Lite Time and Time Zones*. A `domain` that is a parameter (`{"param": ...}`) or a
`domainRaw` is the domain as it is, with no `zero`, `nice`, or padding, and clips the marks (see *A parameter as the
domain of a scale* in *Vega-Lite Parameters*). The scale property `bins` and the type `identity` are reported
(`vl-scale-property-bins`, `vl-scale-identity`).

**Where the colors come from.** In the order of Vega-Lite: the `range` of the scale (a list of colors, or the name of
an entry of `config.range`), then its `scheme`, then the default `config.range` entry of the channel: `category`
(the `tableau10` scheme) for a nominal field, `ordinal` for an ordinal one, `ramp` for a continuous color (`blues`),
`heatmap` for a `rect` mark (`yellowgreenblue`), and `diverging` when there is a `domainMid` (`blueorange`). A `scheme`
is a name, a list of colors, or an object `{"name": ..., "count": ..., "extent": [from, to]}` (the `extent` is the part
of the scheme to use, from 0 to 1, and `[1, 0]` runs it backwards). A continuous scheme is interpolated **piecewise
in sRGB** between its colors, as in Vega. A list of colors takes the color space of `interpolate` (`rgb`, `hsl`,
`hsl-long`, `lab`, `hcl`, `hcl-long`, or `{"type": ..., "gamma": ...}` for sRGB); `cubehelix` is reported
(`vl-scale-interpolate-cubehelix`) and RGB is used. A discrete range takes a categorical scheme whole, and from any
other scheme the first colors, or samples of a continuous one. A scheme name is not case sensitive, and a name that is
not in the list below is a `VegaLiteSpecException` at the scheme (a misspelled name must not quietly change the colors).

The **68 schemes** are those of Vega, with their colors as published in the palette table of Vega:

<!-- markdownlint-disable MD013 -->
| Schemes | Kind | Source and license |
| ------- | ---- | ------------------ |
| `tableau10`, `tableau20` | Categorical | Vega (BSD-3-Clause); the Vega-Lite default palette |
| `category10`, `category20`, `category20b`, `category20c`, `observable10` | Categorical | Vega and d3-scale-chromatic (ISC) |
| `accent`, `dark2`, `paired`, `pastel1`, `pastel2`, `set1`, `set2`, `set3` | Categorical | ColorBrewer (Apache-2.0; Cynthia Brewer, Mark Harrower, The Pennsylvania State University), via d3-scale-chromatic (ISC) |
| `blues`, `greens`, `greys`, `oranges`, `purples`, `reds`, `bluegreen`, `bluepurple`, `greenblue`, `orangered`, `purpleblue`, `purplebluegreen`, `purplered`, `redpurple`, `yellowgreen`, `yellowgreenblue`, `yelloworangebrown`, `yelloworangered` | Sequential | Vega (BSD-3-Clause), the ramps that ColorBrewer (Apache-2.0) originated |
| `brownbluegreen`, `purplegreen`, `purpleorange`, `pinkyellowgreen`, `redblue`, `redgrey`, `redyellowblue`, `redyellowgreen`, `spectral` | Diverging | Vega (BSD-3-Clause), the ramps that ColorBrewer (Apache-2.0) originated |
| `blueorange`, `browns`, `tealblues`, `teals`, `warmgreys`, `goldgreen`, `goldorange`, `goldred`, `lightgreyred`, `lightgreyteal`, `lightmulti`, `lightorange`, `lighttealblue`, `darkblue`, `darkgold`, `darkgreen`, `darkmulti`, `darkred` | Sequential and diverging | Vega (BSD-3-Clause); Vega credits Maureen Stone of Tableau Software for part of them |
| `viridis`, `magma`, `inferno`, `plasma` | Sequential | matplotlib colormaps by Nathaniel J. Smith, Stefan van der Walt, and Eric Firing (CC0) |
| `cividis` | Sequential | Jamie R. Nunez, Christopher R. Anderton, and Ryan S. Renslow, Pacific Northwest National Laboratory (Battelle Memorial Institute license) |
| `turbo` | Sequential | Anton Mikhailov, Google LLC (Apache-2.0) |
| `rainbow`, `sinebow` | Cyclical | d3-scale-chromatic (ISC) |
<!-- markdownlint-enable MD013 -->

The ramps that Vega names after ColorBrewer are Vega's own 9 to 11 color tables, not the 3 to 12 class tables of
ColorBrewer, so a discrete range taken from them with a `count` is sampled from the ramp and does not equal the
ColorBrewer table of that size. The notices of all these sources ship with the packages (see *Vega-Lite Third-Party
Attribution*).

**Shapes and other channels.** The `shape` channel (a `point` mark), the `shape` property of a mark, a scale `range`
of shapes, and `legend.symbolType` take the 12 symbol names `circle`, `square`, `cross`, `diamond`, `triangle-up`,
`triangle-down`, `triangle-right`, `triangle-left`, `stroke`, `arrow`, `wedge`, and `triangle`; the default range is
`config.range.symbol` (circle, square, triangle-up, cross, diamond, triangle-right, triangle-down, triangle-left, in
that
order). A shape that is not a name (a custom SVG
path) is reported (`vl-shape-path`) and a circle is drawn. The other ranges are those of Vega-Lite: `strokeWidth` from
`config.scale.minStrokeWidth` to `maxStrokeWidth` (1 to 4), the opacities from `minOpacity` to `maxOpacity` (0.3 to
0.8), `angle` from 0 to 360 degrees, a `strokeDash` scale five patterns (solid, `4 2`, `2 1`, `1 1`, and `1 2 4 2`),
and a `size` by the mark. A `bin` on one of these channels bins the field (6 bins by default, 4 for `strokeDash`, 10 for
`angle`) and, for a color, gives the bin-ordinal scale above.

**Limits, determinism, and failures.** The entries of a legend are bounded before anything is created (the domain and
`legend.values` by `MaxScaleDomainSize`, the ticks by `MaxTicks`, the entries drawn by `symbolLimit`), every item of a
legend counts against `MaxSceneItems`, and the work of the labels against `MaxEvaluationSteps`, so a hostile
specification fails with a located `VegaLiteRenderException` that names the limit, and the cancellation token is
polled while the entries are built. A `format` that is not a valid d3 format is a `VegaLiteSpecException` at the
property. The result never depends on the machine: the colors are computed with deterministic 8-bit rounding, the
text is measured with the bundled fonts, and the legend labels use the d3 `en-US` number format.

**Deviations from Vega-Lite.** The package differs in these documented ways: the colors of the schemes are Vega's
palette table interpolated piecewise in sRGB (not the per-size tables or the B-spline interpolators of
d3-scale-chromatic);
the canonical scale of a legend that merges several continuous channels is the first channel in the order of Vega-Lite;
`fillOpacity` and `strokeOpacity` have no legend; the legend features listed above are reported and not drawn; and the
`bins` scale property is not supported.

### Vega-Lite Label Expressions

The `labelExpr` of an axis, a legend, and a facet header replaces the text of every label with the result of an
expression, as in Vega-Lite 6. The expression runs on the same engine as every other expression (see *Vega-Lite
Expressions*: the same 104 functions and parameters, and the same `MaxEvaluationSteps`, `MaxStringLength`, and
cancellation), once for every label and **before the labels are measured, laid out, and thinned**, so a longer text
makes the axis or the legend wider, and a blank text takes no room and no part in the removal of overlapping labels.
The `labelExpr` of the configuration applies to every guide that has none of its own: `config.axis` and the other axis
entries (`axisX`, `axisBand`, `axisTemporal`, and so on, in the order of the other axis properties) and `config.header`,
`headerRow`, `headerColumn`, and `headerFacet` (the header, then the entry of its channel, then `config.header`).
Vega-Lite has no `config.legend.labelExpr`, so there is none here either.

<!-- markdownlint-disable MD013 -->
| Guide | `datum` has |
| ----- | ----------- |
| Axis | `value` (the value of the tick: a number, a category, or a date for a time scale and for a field with a time unit), `label` (the text the label would have), `index` (the position among the ticks as a **fraction** from 0 to 1, `i / (n - 1)`, as in Vega), and `tickIndex` (the position as a whole number from 0) |
| Symbol legend | `value` (the value of the entry), `label`, and `index` (the whole-number position of the entry); the entry that stands for the entries beyond `symbolLimit` has the next index. `datum.size` and `datum.offset` are `null` |
| Gradient legend | `value` (a date for a temporal field), `label`, `index`, and `perc` (the position along the bar from 0 to 1); the discrete gradient of a quantize, quantile, or threshold scale also has `perc2` (the end of the class) |
| Header | `value` (the value of the cell) and `label` (its formatted label), and nothing else |
<!-- markdownlint-enable MD013 -->

**The result** becomes the text the way Vega's text mark does it: `null` (and a member that does not exist) is the empty
text, an array is several lines (every element is trimmed, and a `null` element is empty), and anything else is
converted to text as JavaScript does and trimmed. A date is its ISO text. A text is **not** split at a line feed; only
an
array makes lines (also in a facet header: a column or row header, its footer, and the title of a cell of a wrapped
facet, so `[datum.label, 'n=' + datum.value]` is a header of two lines, and the layout measures them, which makes the
header taller). A function that the engine does not have (`luminance`, `geoarea`, `now`, ...) evaluates to `null`, so
the label is empty, and it is reported (`vl-expr-function-<name>`). A view signal is reported too
(`vl-expr-view-state`).

`format(value, specifier)` writes a number with a d3 number format, and `timeFormat(value, specifier)` a date with a d3
time format in the time zone of the chart (`utcFormat` writes it in UTC), as in Vega.

Every label expression is **compiled when the document is opened**, also the ones in the `config` of the specification,
so a syntax error or a reference to a parameter that does not exist is a `VegaLiteSpecException` at the text. An
expression in the configuration that the caller passes to `VegaLiteRenderOptions.Config` is compiled when its guide is
built (the same exception). An expression that runs too long or builds too long a text is a `VegaLiteRenderException` at
the expression. The guides of a chart are laid out up to three times and every pass runs the expressions again, so the
cost is bounded by `MaxTicks` times `MaxEvaluationSteps`; labels that are switched off (`labels: false`) never run one.

```json
{ "x": { "field": "year", "type": "ordinal",
         "axis": { "labelExpr": "datum.value % 2 == 0 ? 'FY' + datum.label : ''" } },
  "y": { "field": "share", "type": "quantitative",
         "axis": { "labelExpr": "datum.label + '%'" } } }
```

A y axis of ratios written as percent with `format`, and a temporal x axis written as month and year with `timeFormat`:

```json
{ "mark": "line",
  "data": { "values": [ { "month": "2024-01-01", "share": 0.28 }, { "month": "2024-04-01", "share": 0.41 },
                        { "month": "2024-07-01", "share": 0.62 } ] },
  "encoding": {
    "x": { "field": "month", "type": "temporal",
           "axis": { "labelExpr": "timeFormat(datum.value, '%b %Y')" } },
    "y": { "field": "share", "type": "quantitative",
           "axis": { "labelExpr": "format(datum.value, '.0%')" } } } }
```

**Not supported.** The functions outside the 100, `datum.size` and `datum.offset` of a symbol legend
(they are `null`), the replacement of `datum.label` by the text of a custom `formatType` (a custom format type is
reported
and not evaluated), and the extra tick of a binned axis. Example 47 shows label expressions.

### Vega-Lite Offset Channels (Grouped Bars)

`xOffset` and `yOffset` place a mark **inside the band** of its `x` or `y` position, so a category can show several
groups side by side (a grouped bar chart) or points that spread inside their band, as in Vega-Lite 6:

```json
{ "mark": "bar",
  "encoding": {
    "x": { "field": "stall", "type": "nominal" },
    "xOffset": { "field": "quarter", "type": "nominal" },
    "y": { "field": "crates", "type": "quantitative" },
    "color": { "field": "quarter", "type": "nominal" } } }
```

The rules are those of Vega-Lite:

- **The position.** The `x` (or `y`) channel must be a nominal or ordinal field (or datum) without a `timeUnit`, or a
  temporal field with a time unit (see *Time-unit positions* below). Its
  scale is then a **band scale for every mark** (a `point` mark too, and an explicit `scale.type` of `point` is replaced
  with `vl-scale-type-ignored`), with the paddings `config.scale.bandWithNestedOffsetPaddingInner` and `...Outer` (0.2
  each by default) instead of the paddings of the mark, unless `scale.padding`, `paddingInner`, or `paddingOuter` says
  otherwise.
- **The offset scale.** A discrete offset (nominal or ordinal) is a **band** scale for `bar`, `rect`, `rule`, and `tick`
  and a **point** scale for the other marks (with an outer padding of half a step, so the points are at the centers of
  the bands); a quantitative offset is a linear scale and a temporal one a time scale. The offset scale spans the
  bandwidth of the band of its position (a continuous `yOffset` runs upward, like `y`). Its `scale` takes the usual
  properties (`type`, `domain`, `sort`, `paddingInner`, `paddingOuter`, `padding`, `reverse`, `round`, `range`, and so
  on). A band offset has the paddings `config.scale.offsetBandPaddingInner` and `...Outer` (none by default); an offset
  has no `zero`, no `nice`, and no pixel padding. The domain is the sorted values of the whole field (ascending by
  default), so a group that a category lacks keeps its empty slot.
- **The size of the view.** With a step size (the default, 20 pixels) the step of the position scale is the step of the
  offset times the room of the offset domain over the free part of the band, that is the step of the offset times
  `n - paddingInner + 2 * paddingOuter` (the offset values `n` and the paddings of the offset), divided by
  `1 - paddingInner` of the position (`for: "offset"`, the default for a discrete offset). Three categories with two
  groups give the step 20 \* 2 / 0.8 = 50, a view of 50 \* (3 - 0.2 + 0.4) = 160 pixels, a band of 40, and **bars of 20
  pixels**. `"width": {"step": 20, "for": "position"}` keeps the step on the position scale instead, and a number as the
  width fills the bands with that width (200 pixels give a step of 62.5 and bars of 25).
- **The marks.** A bar, rect, tick, or rule spans the offset band (so a grouped bar has the width of the offset band,
  and
  there is no gap between the bars of a group unless the offset has a `paddingInner`); with an explicit `size` the mark
  is centered in the offset band. A `point`, `circle`, `square`, `text`, `line`, and `trail` sit at the start of the
  band
  plus the center of the offset: a discrete offset gives the side-by-side positions, a **continuous** offset spreads
  the points inside the band (a strip plot). A row whose offset value is not valid is not drawn. A `boxplot`,
  `errorbar`, and `errorband` keep the offset channel.
- **Stacks.** The offset field of the dimension splits the stacks as the dimension does: with a color that is not the
  offset field the segments stack inside each group, and with the same field as the color (or an aggregate) nothing is
  stacked. A continuous offset on a chart without a dimension is a strip, so the default stack of a bar is off.
- **Layers and compositions.** The layers of a layered chart that share the position scale share the offset scale (the
  domains are merged, and the step comes from the merged domain); `resolve.scale.xOffset: "independent"` gives each
  layer
  its own. The charts of a `concat` or `repeat` keep independent offset scales by default and `"shared"` shares one; the
  cells of a `facet` share the offset domain.

**Time-unit positions.** The `x` (or `y`) channel of a `temporal` field with a `timeUnit` (`yearmonth`, `year`,
`yearmonthdate`, `hours`, ...) can have an offset too. The position then **stays a time scale**, and only the offset
gets
a scale of its own (a band scale for `bar`, `rect`, `image`, `rule`, and `tick` with no paddings, a point scale for the
other marks; a quantitative or temporal offset is a linear or time scale, and a continuous offset of a `bar` or an
`area`
stays `vl-offset-ranged`). The marks read the position at the **start** of the unit and add the offset. The range of the
offset scale is a share of the length of **one reference unit** on the time scale, as Vega-Lite computes it: the length
is the distance between 2001-01-01 and one step of the smallest part of the unit later, measured on the position scale
(the same for every unit of the chart, so a month is the 31 days of January 2001 and a year is 365 days, and the length
is negative on a `y` scale, so that a `yOffset` runs upward). With the padding
`config.scale.bandWithNestedOffsetPaddingInner` (0.2 by default) the range is from `(padding / 2 + p) * length` to
`(1 - padding / 2 + p) * length`, where `p` is the band position minus 0.5 (the band position is the `bandPosition` of
the position channel, else the
`timeUnitBandPosition` of the mark, else 0.5); a padding of 0 gives the range from 0 to `length` and ignores the
band position.
A bar spans a band of the offset scale and is narrowed by `timeUnitBandSize` (1 by default, centered) unless it has a
`size`. A chart of 59 days over 300 pixels with two groups per month draws bands of 0.4 of a month, starting at 0.1 and
0.5 of a month from the start of each month:

```json
{ "data": { "values": [
    { "date": "2024-01-01", "kind": "A", "n": 4 }, { "date": "2024-01-01", "kind": "B", "n": 7 },
    { "date": "2024-02-01", "kind": "A", "n": 6 }, { "date": "2024-02-01", "kind": "B", "n": 3 } ] },
  "mark": { "type": "bar", "timeUnitBandSize": 1 },
  "encoding": {
    "x": { "field": "date", "type": "temporal", "timeUnit": "yearmonth" },
    "xOffset": { "field": "kind", "type": "nominal" },
    "y": { "field": "n", "type": "quantitative" },
    "color": { "field": "kind", "type": "nominal" } } }
```

These details differ from Vega-Lite: a mark that has no `timeUnitBandPosition` or `bandPosition` (a `point`, `line`,
`text`, ...) uses the middle of the unit where Vega-Lite computes `NaN`; a `utc` unit measures its reference duration in
the calendar of the scale (UTC) where Vega-Lite uses the zone of the chart (up to an hour apart for a quarter in a zone
with daylight saving time); a `bandPosition` other than 0.5 on a `bar` does not move the domain of the scale
(`vl-encoding-bandposition-rect` is still reported); and `minBandSize` clamps around the start of the band instead of
its
center. The unit `month` without a year puts every date into the leap year 2012, while `yearmonth` uses the real year.

**Not drawn, and reported.** An offset without a supported position (`vl-offset-position`) and a continuous offset of a
`bar` or an `area` (a ranged offset, `vl-offset-ranged`) are ignored and the marks are drawn at their position. The
position is not supported when the `x` (or `y`) channel is missing, is quantitative, or is temporal without a
`timeUnit`; when its unit is `day`, `weekday`, or `yearweekday` (the smallest part is the day of the week), a `maxbins`
unit, or a binned one; when the mark is a `rect` with a temporal time unit. A nominal or ordinal field with a
`timeUnit` is an
ordinary band parent and is drawn like one without. A temporal field with any other time unit is no longer reported. The
`bin` of an offset channel is not applied (`vl-encoding-bin`), and the `xOffset` and `yOffset` **properties** of a mark
(a pixel offset) are not drawn, as before. Example 47 draws a grouped bar chart.

### Vega-Lite Time and Time Zones

A field of the type `temporal` holds **dates**. A date is an instant: the number of milliseconds since
1970-01-01T00:00:00Z. The package reads, groups, scales, and labels dates in one **calendar** per `Measure` or `Render`
call: UTC by default, or the zone of `VegaLiteRenderOptions.TimeZone`. It never uses the zone, the clock, or the
culture of the machine, so the same document and options give the same picture everywhere (see *The time zone*
below). Write the `type` of a time channel, or a `timeUnit`: as in Vega-Lite, a channel without a `type` is typed by
Vega-Lite's
default rules, and a `timeUnit` makes the type `temporal` (see *Field types without a `type`* in *Supported Vega-Lite
Subset*).
The package does not infer `temporal` from the values. Example 36 draws a time series in a time zone.

```json
{
  "data": { "url": "visits.csv", "format": { "parse": { "day": "date:'%d/%m/%Y'" } } },
  "mark": "bar",
  "encoding": {
    "x": {
      "field": "day", "type": "temporal", "timeUnit": "yearmonth",
      "axis": { "format": "%b %Y", "tickCount": "month" }
    },
    "y": { "aggregate": "sum", "field": "tickets", "type": "quantitative" }
  }
}
```

**Temporal fields and parsing.** Text is turned into dates in the places that *Vega-Lite Data* lists: an explicit
`format.parse` entry, `"parse": "auto"`, and the **implicit parse** of every field that a time construct reads (a
channel of the type `temporal`, with a `timeUnit`, or with a `formatType` of `time`; the field of a `timeUnit`
transform; the field of a predicate that has a `timeUnit` or a date-time object). The implicit parse applies to the main
data of the view (inline rows, files, and datasets, not the source of a `lookup`); a `parse` entry for the field wins
over it, and `"parse": null` switches it off. A value that cannot be read becomes `null` and is counted in the warning
`vl-data-parse-failed`, never an invalid date. A number is read as **milliseconds since the epoch**, and a `DateTime` or
`DateTimeOffset` in a caller dataset is already an instant (the zone of the chart does not move it).

<!-- markdownlint-disable MD013 -->
| `format.parse` entry | What is read |
| -------------------- | ------------ |
| `"date"` | A date text by the rules of `Date.parse` (below), a number as milliseconds, a date unchanged; a boolean is `null` |
| `"date:'%d/%m/%Y'"` | The text with a d3 time format (see *Axis and legend labels* for the directives), in the **zone of the chart**; the format must match the whole text, and a number is read through its text |
| `"utc:'%d/%m/%Y'"` | The same format, always read as **UTC** |
<!-- markdownlint-enable MD013 -->

The format of `date:` and `utc:` must be quoted with matching single or double quotes; an unquoted or empty format is a
`VegaLiteSpecException` at the entry when the document is opened. A text longer than 256 characters is never a date
under a custom format.

*ISO 8601 text* follows JavaScript's mixed rule, which is the one thing that depends on the zone:

<!-- markdownlint-disable MD013 -->
| Text | Is read as |
| ---- | ---------- |
| `2024`, `2024-03`, `2024-03-05`, and expanded years such as `+002024-03-05` | Midnight **UTC**, whatever the zone of the chart |
| `2024-03-05T13:04`, `2024-03-05T13:04:09`, `2024-03-05T13:04:09.007`, or a space instead of the `T` | That wall-clock time in the **zone of the chart** (UTC by default); a fraction may have any number of digits and is cut to milliseconds |
| Any of these time forms with `Z`, `+02:00`, or `+0200` | The instant with that offset, whatever the zone of the chart |
| `24:00` | The end of the day. Other hours above 23, and a day that does not exist (`2023-02-29`), are not dates |
<!-- markdownlint-enable MD013 -->

*Legacy forms.* A subset of the other texts that the `Date.parse` of a browser reads is read too, in the zone of the
chart unless the text names one (month and weekday names in any case, in full or with at least three letters; a
parenthesized comment is ignored):

<!-- markdownlint-disable MD013 -->
| Form | Examples |
| ---- | -------- |
| Month name, day, year, optional time | `Jan 2, 2006`, `January 2, 2006 10:00` |
| Day, month name, year | `2 Jan 2006` |
| With a weekday | `Mon Jan 02 2006 15:04:05 GMT+0200 (CEST)`, `Tue, 02 Jan 2006 15:04:05 GMT` |
| Numeric | `2006/01/02` (year, month, day), `1/2/2006` (**month**, day, year) |
| Time of day | `h:mm`, `h:mm:ss`, `h:mm:ss.fff`, optionally followed by `AM` or `PM` |
| Zone | `UTC`, `GMT`, `UT`, `Z`, optionally followed by an offset, or just an offset such as `+0200` or `-05:30` |
<!-- markdownlint-enable MD013 -->

Years in the legacy forms have four digits. **Where this differs from V8** (the engine of Chrome and Node): anything
that is not an ISO 8601 text or one of the forms above is not a date here, even when V8 reads it (a two-digit year, a
day that does not exist and that V8 rolls over, and other variants), a text of more than 128 characters is not a date
(it is rejected before it is scanned, so a huge cell costs no more than a short one), and a result outside the years 1
to 9999 is not a date. Under `"parse": "auto"` a column in these forms (for example `Jan 2, 2006`) becomes a date
column, as it does in Vega.

**Time units.** A `timeUnit` floors a date to the start of a calendar unit, so that the dates of one month, day, or hour
share one value. It is a property of a channel definition (also in a condition, a predicate, and a `timeUnit`
transform) and is a name or an object. There are **11 single units** and **29 multi units** (the 40 local units of
Vega-Lite), and every one of them has a **UTC form** with the prefix `utc` (`utcmonth`, `utcyearmonthdate`) or with
`"utc": true` in the object form. A local unit uses the calendar of the chart, and a `utc` unit always uses UTC.

<!-- markdownlint-disable MD013 -->
| Single unit | Keeps | Label specifier |
| ----------- | ----- | --------------- |
| `year` | The year | `%Y` |
| `quarter` | The quarter, 1 to 4 | `Q%q` |
| `month` | The month | `%b` |
| `week` | The week of the year, counted in Sundays: week 0 is before the first Sunday, and a week starts on a Sunday | `W%U` |
| `day` | The day of the week | `%a` |
| `dayofyear` | The day of the year, 1 to 366 | `%j` |
| `date` | The day of the month | `%d` |
| `hours` | The hour | `%H:00` |
| `minutes` | The minute | `00:%M` |
| `seconds` | The second | `:%S` |
| `milliseconds` | The millisecond | `.%L` |
<!-- markdownlint-enable MD013 -->

<!-- markdownlint-disable MD013 -->
| Multi units | Label specifier |
| ----------- | --------------- |
| `yearquarter`, `yearquartermonth` | `%Y Q%q`, `%Y Q%q %b` |
| `yearmonth`, `yearmonthdate`, `yearmonthdatehours`, `yearmonthdatehoursminutes`, `yearmonthdatehoursminutesseconds` | `%b %Y`, `%b %d, %Y`, `%b %d, %Y %H:00`, `%b %d, %Y %H:%M`, `%b %d, %Y %H:%M:%S` |
| `yearweek`, `yearweekday`, `yearweekdayhours`, `yearweekdayhoursminutes`, `yearweekdayhoursminutesseconds` | `%Y W%U`, `%Y W%U %a`, `%Y W%U %a %H:00`, `%Y W%U %a %H:%M`, `%Y W%U %a %H:%M:%S` |
| `yeardayofyear`, `quartermonth` | `%Y %j`, `Q%q %b` |
| `monthdate`, `monthdatehours`, `monthdatehoursminutes`, `monthdatehoursminutesseconds` | `%b %d`, `%b %d %H:00`, `%b %d %H:%M`, `%b %d %H:%M:%S` |
| `weekday`, `weekdayhours`, `weekdayhoursminutes`, `weekdayhoursminutesseconds` | `W%U %a`, `W%U %a %H:00`, `W%U %a %H:%M`, `W%U %a %H:%M:%S` |
| `dayhours`, `dayhoursminutes`, `dayhoursminutesseconds` | `%a %H:00`, `%a %H:%M`, `%a %H:%M:%S` |
| `hoursminutes`, `hoursminutesseconds`, `minutesseconds`, `secondsmilliseconds` | `%H:%M`, `%H:%M:%S`, `00:%M:%S`, `:%S.%L` |
<!-- markdownlint-enable MD013 -->

The units that name no year, month, or day take the **reference values** of Vega-Lite: the year 2012 (a leap year that
starts on a Sunday), January, and the first of the month, so `month` floors the 17th of March 2024 to 2012-03-01 and
`day` floors a Tuesday to the Tuesday of the first week of January 2012. A unit that keeps the `week` is floored to the
Sunday that starts the week (which can lie in the previous December). The **object form** is
`{"unit": "yearmonth", "step": 3, "utc": false, "maxbins": 10}` with the members `unit`, `step` (a number above 0,
applied
to the smallest part of the unit: `{"unit": "month", "step": 3}` floors March to January and April to April), `utc`,
and `maxbins` (a number above 0). An unknown unit name, a member that is not one of these, or a value of the wrong
kind is a `VegaLiteSpecException` at the value when the document is opened.

The object `{"maxbins": 10}` without a `unit` lets the data choose the unit: it takes, for the extent of the field, the
step of this list whose length is nearest to the extent divided by `maxbins`: 1, 5, 15, or 30 seconds; 1, 5, 15, or 30
minutes; 1, 3, 6, or 12 hours; 1 day; 1 week; 1 or 3 months; 1 year; milliseconds with a round step for a shorter
extent and years with a round step for a longer one. A `maxbins` above `MaxScaleDomainSize` fails the render with a
located error. The `binned*` units (`binnedyearmonth`, ...) and `"binned": true` are **not supported**: they are
reported (`vl-timeunit-binned`), the unit is ignored, and the field is used as it is.

*What a time unit adds to the rows.* A channel with a `timeUnit` reads a **derived field** that holds the start of the
unit as a date, and a channel that spans its unit also reads the end. The names are those of Vega-Lite: `yearmonth_date`
and `yearmonth_date_end` for the unit `yearmonth` on the field `date`, `month_step_2_date` for `{"unit": "month",
"step": 2}`, `utcmonth_date` for a UTC unit, and `timeunit_maxbins_10_date` for the form that names no unit. The
**title**
of the channel follows `config.fieldTitle`: `date (year-month)` (verbal, the default; the parts of the unit, and just
`date` for the `maxbins` form), `YEARMONTH(date)` (functional; `UTCYEARMONTH(date)` for a UTC unit and
`TIMEUNIT(date)` for `maxbins`), or `date` (plain); an explicit `title` wins. The unit is computed before an `aggregate`
of the encoding, which then groups by the start of the unit (and by its end when the mark spans it). A `bin` on the
same field wins over its `timeUnit`. The `timeUnit` **transform** `{"timeUnit": "yearmonth", "field": "date", "as":
"month"}` adds the fields `month` and `month_end` to every row in the order of the `transform` array.

*Bands and positions.* A mark that **spans** its unit draws it from the start to the end: the channel is `x` or `y`, of
the type `temporal`, with no `x2` or `y2`, and a band position is defined. That is the case for a `bar`, a `rect`, and a
`tick` (the configuration `timeUnitBandPosition` is 0.5 for them), and for any mark whose channel definition
(`bandPosition`), mark definition, or configuration (`timeUnitBandPosition`) gives one. The scale then covers the start
of the first unit to the end of the last. A `bar` or `rect` fills the part of the unit that is `timeUnitBandSize` wide
(1 by default, or the `band` of a relative mark `width` such as `{"band": 0.5}`), less the `binSpacing` of a bar
(1 pixel, as for bins; 0 for a rect), and a bar that would be thinner than `minBandSize` is widened to it. A `tick` or a
point with a band position sits at that fraction of the unit (0.5 is the middle). A mark that does not span, such as a
point with no band position, sits at the **start** of its unit. A `bandPosition` or `timeUnitBandPosition` other than
0.5 on a `bar` or `rect` is not drawn: 0.5 is used and
`vl-encoding-bandposition-rect` is reported. A channel of the type `ordinal` or `nominal` with a `timeUnit` has a band
or point scale over the dates of its units (sorted by date), labeled by the unit specifier.

**Date-time objects.** A Vega-Lite `DateTime` object gives one point in time by calendar parts, for example
`{"year": 2024, "month": "Mar", "date": 5}`. A missing part takes its default; a part that is too large or too small
rolls over as in the JavaScript `Date` constructor (month 13 is January of the next year, date 0 is the last day of
the previous month). The object is read in the zone of the chart, or in UTC with `"utc": true`.

<!-- markdownlint-disable MD013 -->
| Key | Value | Default |
| --- | ----- | ------- |
| `year` | A number (0 to 99 mean 1900 to 1999, as in JavaScript) | 2012 |
| `quarter` | 1 to 4, or numeric text; it stands for the first month of the quarter, and `month` wins when both are given | January |
| `month` | 1 (January) to 12, numeric text, or a month name in full or with its first three letters, in any case (`"March"`, `"mar"`) | January |
| `date` | The day of the month | 1 |
| `day` | 1 (Monday) to 7, or 0 (Sunday), or a weekday name; it stands for that day of the first week of January 2012, and like Vega-Lite the object **drops its `day`** when it has any other key, `utc` included | none |
| `hours`, `minutes`, `seconds`, `milliseconds` | Numbers | 0 |
| `utc` | `true` or `false` | `false` |
<!-- markdownlint-enable MD013 -->

An unknown key, a value of the wrong kind, an unknown month or weekday name, an empty object, and a value that is not a
number (such as `"quarter": "Q"`) are a `VegaLiteSpecException` at the offending member when the document is opened.
Date-time objects are accepted in `scale.domain` entries, `scale.domainMin` and `domainMax`, the `values` of an axis
or a legend, the values of a predicate (`equal`, `lt`, `lte`, `gt`, `gte`, `range`, and `oneOf`), and the `sort` array
of a temporal field. As the `datum` of a channel they are reported and the channel is ignored
(`vl-encoding-datum-datetime`).

**Time in predicates.** A filter or condition predicate compares dates when its field has a `timeUnit` or its value is
a date-time object. With a unit, the comparison is made on the **parts that the unit keeps**:
`{"field": "when", "timeUnit": "month", "oneOf": ["Feb", "Mar"]}` keeps the rows of February and March of any year,
and `{"field": "when", "range": [{"year": 2024, "month": 1, "date": 1}, {"year": 2024, "month": 12, "date": 31}]}`
keeps the dates of 2024. A range is inclusive, accepts its ends in either order, and a `null` end is open. A number that
is compared with a field that has a **single** local unit and is below 10,000 stands for that part (`3` against `month`
is March, `5` against `hours` is 05:00), any other number is milliseconds, and a text is read as a date or, for a
single unit, as a month or weekday name (`"Mar"`). A `utc` unit compares in UTC (Vega-Lite ignores its `utc` flag in a
predicate). The `week` and `dayofyear` parts are **not applied** by a predicate, as in Vega-Lite, and the warning
`vl-predicate-timeunit-part` says so; the `timeUnit` transform and the encoding do apply them. A `binned` unit (or the
`maxbins` form without a unit) in a predicate is reported and ignored. A time-aware `oneOf` list, and a predicate that
is the `test` of a condition, is compiled into an
expression that the renderer writes (not one that you write), so it is not bound by `MaxExpressionLength`: a list may
have `MaxScaleDomainSize` entries (100,000), a `oneOf` of a thousand values or an
`or` of a thousand comparisons is accepted, and the text that is written is bound by eight times `MaxSourceLength` (a
longer one is a located `VegaLiteSpecException` that names `MaxSourceLength`; the text is counted as it is written and
refused as soon as it passes the bound, so a very long field name with a large `oneOf` list fails at once and uses no
memory for the text). An expression that you write as the value of a predicate (`{"expr": "..."}` as the comparison or
as an item of a `range` or `oneOf`) is an expression that you write: it is bound by `MaxExpressionLength` like any
other, and a failure is located at the `expr` text.

**Time and UTC scales.** A temporal field on `x`, `y`, `size`, `opacity`, `strokeWidth`, or `angle` gets a `time` scale
(a `utc` scale when its time unit is a `utc` unit): a linear scale over milliseconds whose ticks are calendar
boundaries. A temporal `color`, `fill`, or `stroke` gets a sequential color scale over the same milliseconds. A
temporal field accepts only the scale types `time` and `utc`; any other type is replaced by the default with the warning
`vl-scale-type-ignored`, and so is `time` or `utc` on a quantitative, nominal, or ordinal field. A `time` scale uses the
zone of the chart for its ticks and its `nice` boundaries and a `utc` scale uses UTC (also on a color channel, where the
type `utc` keeps the legend of the dates in UTC whatever the zone of the chart is). The properties `domain` (timestamps,
date texts, or date-time objects), `domainMin`, `domainMax`, `domainMid` (a timestamp, for a color), `range`,
`rangeMin`, `rangeMax`, `reverse`, `clamp`, `round`, `padding`, and `nice` apply as for a linear scale; there is no
zero rule, and `scale.zero` is ignored (also on a temporal color) with the warning `vl-scale-zero-ignored`. A temporal
field without a valid date gets the default domain 0 to 1, as a quantitative field does.

- **Ticks.** The default tick count is one tick for every 40 pixels (`ceil(length / 40)`); a field whose unit is exactly
  `month`, `hours`, `day`, or `quarter` uses 10 (Vega's default). The count is lowered so that no tick is closer than
  `axis.tickMinStep`, which is by default the length of one step of the smallest part of the time unit (31 days for a
  `month`, 365 days for a `year`; it is measured in UTC, so it does not depend on the zone, and there is none for a
  `day` unit that means the weekday). The ticks themselves are those of d3: the calendar step of this list whose length
  is nearest to the span divided by the count: 1, 5, 15, or 30 seconds; 1, 5, 15, or 30 minutes; 1, 3, 6, or 12 hours;
  1 or 2 days; 1 week; 1 or 3 months; 1 year; milliseconds or years with a round step beyond the ends of the list. The
  first tick is the first boundary at or after the start of the domain, and the stop is included. The count is a hint,
  as for numbers.
- **`tickCount` as an interval.** An axis or legend `tickCount` of `"day"` (one of `millisecond`, `second`, `minute`,
  `hour`, `day`, `week`, `month`, or `year`) or `{"interval": "day", "step": 2}` places a tick at every boundary of that
  calendar interval, or at every second boundary with a `step` of 2. A `step` that is not a positive number is a
  `VegaLiteSpecException`; on a scale that is not a time scale the interval is ignored with the warning
  `vl-guide-axis-tickcount-interval`. The `values` of an axis or legend may be timestamps, date texts, or date-time
  objects.
- **`nice`.** `"nice": true` (or a count) extends the domain to the boundaries of the tick interval of that count (10 by
  default), and `"nice": "month"` or `"nice": {"interval": "month", "step": 3}` extends it to the boundaries of that
  interval, so a domain from 10 January to 20 March becomes 1 January to 1 April. The boundaries are those of the
  calendar of the scale. A time scale is **not** nice by default. An interval as `nice` of a scale that is not a time
  scale is ignored with the warning `vl-scale-nice-interval`.

**Axis and legend labels.** An axis labels a time scale with these rules, in order:

1. `axis.format` as a **text** is a d3 time format (read in the zone of the scale; a `utc` scale uses UTC).
2. `axis.format` as an **object** is Vega's multi-format with your texts as the formats of its levels: the members
   `milliseconds`, `seconds`, `minutes`, `hours`, `date` (or `day`), `week`, `month`, `quarter`, and `year`; a level
   that is missing or empty keeps its default below.
3. With no `format` and a `timeUnit`, the **label specifier of the unit** (the tables above): `Jan 2024` for
   `yearmonth`.
4. Otherwise the default **multi-format**, which labels each tick by the largest calendar boundary it sits on:

<!-- markdownlint-disable MD013 -->
| The tick is | Default specifier | Example |
| ----------- | ----------------- | ------- |
| Not on a whole second | `.%L` | `.250` |
| A second that is not a whole minute | `:%S` | `:30` |
| A minute that is not a whole hour | `%I:%M` | `01:45` |
| An hour that is not midnight | `%I %p` | `01 PM` |
| A day that is not the start of a week | `%a %d` | `Tue 05` |
| The start of a week (a Sunday) that is not the start of a month | `%b %d` | `Mar 03` |
| The start of a month or a quarter that is not the start of a year | `%B` | `March` |
| The start of a year | `%Y` | `2024` |
<!-- markdownlint-enable MD013 -->

The d3 time format directives, shown for 2024-03-05T13:04:09.007 (a Tuesday), are the following. A padding flag may
follow the `%` (`-` for none, `_` for a space, `0` for zeros), and any other letter after a `%` is copied without the
`%`:

<!-- markdownlint-disable MD013 -->
| Directive | Meaning | Example |
| --------- | ------- | ------- |
| `%a`, `%A` | The weekday, abbreviated and in full | `Tue`, `Tuesday` |
| `%b`, `%B` | The month, abbreviated and in full | `Mar`, `March` |
| `%d`, `%e` | The day of the month, padded with a zero and padded with a space | `05`, a space and `5` |
| `%H`, `%I`, `%p` | The hour from 00 to 23, the hour from 01 to 12, and `AM` or `PM` | `13`, `01`, `PM` |
| `%M`, `%S`, `%L`, `%f` | The minute, the second, the millisecond, and the microsecond (six digits) | `04`, `09`, `007`, `007000` |
| `%j` | The day of the year | `065` |
| `%m`, `%q` | The month number and the quarter | `03`, `1` |
| `%u`, `%w` | The weekday from 1 (Monday) to 7, and from 0 (Sunday) to 6 | `2`, `2` |
| `%U`, `%W`, `%V` | The week of the year counted in Sundays, in Mondays, and the ISO 8601 week | `09`, `10`, `10` |
| `%g`, `%G` | The ISO 8601 week-based year without and with the century | `24`, `2024` |
| `%y`, `%Y` | The year without and with the century | `24`, `2024` |
| `%Z` | The offset from UTC of the instant | `+0000` (`-0500` in a zone five hours behind) |
| `%Q`, `%s` | The milliseconds and the seconds since the epoch | `1709643849007`, `1709643849` |
| `%c`, `%x`, `%X` | The date and time, the date, and the time of the English locale | `3/5/2024, 1:04:09 PM`, `3/5/2024`, `1:04:09 PM` |
| `%%` | A percent sign | `%` |
<!-- markdownlint-enable MD013 -->

`axis.formatType` selects how a label is made: `number` (the default for a quantitative field) formats the milliseconds
as a number with the d3 number format of `axis.format`, `time` makes the labels of **any** axis dates (in the time zone
of the chart: a number is read as milliseconds since the epoch), and `utc` makes
them UTC dates; any other name is a custom format type, which is not evaluated and is reported
(`vl-format-type-custom`). Axis properties can be set for the time axes in `config.axisTemporal` (and `axisXTemporal`
and `axisYTemporal`) as in Vega-Lite. A temporal axis has a grid by default, and the first and last labels of a
temporal `x` axis are flushed to the ends of the axis; the labels of a time unit field are not turned by default, and
labels that overlap are removed by parity (every other one).

A **legend** of a temporal field labels the ticks of its gradient (a temporal color) or its symbols (a size, an
opacity, or a time unit field) with `legend.format` (a d3 time format), else the specifier of the time unit, else
`config.timeFormat` (`%b %d, %Y` by default, for example `Jan 01, 2024`); the ticks follow the rules above, with
`legend.tickCount` and `legend.tickMinStep`, and the gradient is sampled at those ticks. Unlike an axis, a legend does
not use the multi-format.

**Expression functions.** The date and time functions of the expression language (see the table in *Vega-Lite
Expressions*) read the calendar of the chart, except the `utc` forms. They follow JavaScript: the month counts from 0
and a part that is too large rolls over (`datetime(2024, 1, 31)` is 2 March 2024, because February 2024 has 29 days),
and the years 0 to 99 mean 1900 to 1999. They differ in these ways: `now()` is not available (the clock
is not an input; it is reported as `vl-expr-function-now` and gives `null`), `datetime()` with no argument is `null`,
a result outside the years 1 to 9999 is `null` for a date and `NaN` for a number where JavaScript has `Invalid Date`, a
`null` or boolean argument is not an instant (JavaScript reads `null` as the epoch), and `isoweek` and `utcisoweek` are
included, though Vega has no such unit. `timeFormat` and `timeParse` read the calendar of the chart and `utcFormat` and
`utcParse` UTC.

**The time zone.** `VegaLiteRenderOptions.TimeZone` is a `TimeZoneInfo`; `null` (the default) is **UTC**, and a chart
never uses the zone of the machine (`TimeZoneInfo.Local`, the clock, and the culture are not consulted), so a chart that
you render in Tokyo and in Oslo is the same picture. The zone is the one calendar of the call (`Measure` and `Render`
alike), and it decides:

- how a date-time **without an offset** is read (in the data, in a date-time object, in a legacy date text, and with a
  `date:` parse format);
- where a **local time unit** begins and ends (the day of a UTC date can differ from its day in the zone), and what the
  local date functions of expressions return;
- where the ticks and the `nice` boundaries of a `time` scale are, and what its labels say.

A `utc` time unit, a `utc` scale, `utc:` parse format, a date-time object with `"utc": true`, and the `utc` functions
(`utcFormat`, `utcyear`, ...) always use UTC. An instant itself never moves: a date with a `Z` or an offset is the same
instant in every zone, and a `DateTime` or `DateTimeOffset` of a caller dataset is the same instant too.

- **Zone ids.** `TimeZoneInfo.FindSystemTimeZoneById` finds a zone by the id of the platform: IANA ids
  (`"America/New_York"`) on Linux and macOS, and Windows ids (`"Eastern Standard Time"`) on Windows. .NET 6 and later
  can
  also convert between the two kinds when ICU is available (an IANA id on Windows, a Windows id on Linux and macOS), and
  without ICU only the native kind works; the call throws a `TimeZoneNotFoundException` for an id it does not know.
- **Reproducibility.** The rules of a system zone come from the **operating system database** (the Windows registry or
  the IANA time zone database files), and the historical rules of the two databases differ (the years before the
  current rules, and the zones whose rules were changed after your OS was released), so the same id can give a
  different hour for an old date on two machines. For a picture that must be identical everywhere, build the zone with
  `TimeZoneInfo.CreateCustomTimeZone` (a fixed offset, or an offset with your own adjustment rules, as in Example 36) or
  pass a fixed-offset zone.
- **Daylight saving time.** A wall-clock time that does not occur (the clock jumps forward) is read with the offset from
  before the change, so `02:30` on the day of a spring change is the instant that the clock shows as `03:30`. A
  wall-clock time that occurs twice (the clock goes back) is the **earlier** of the two instants. This is the rule of
  ECMAScript, and it assumes at most one change of offset in 48 hours, which holds for every real zone. A local `day`
  unit that contains the change is 23 or 25 hours long, and its `bar` is narrower or wider than the others.
- **Range and invalid dates.** Dates are supported from the year 1 to the last millisecond of the year 9999 (in UTC; a
  wall-clock time near the ends that falls outside the range because of the zone offset is out of range too). Anything
  outside the range, every text that is not a date, and every non-finite number is **invalid**: a **`null`** in the
  data and in a date function, and **`NaN`** in a number function (never JavaScript's `Invalid Date`, which a label
  would show as the text `Invalid Date`). Nothing throws for a date value.
- **Culture.** The names are those of the English d3 locale (`January`, `Tue`, `AM`), the digits are ASCII, the first
  day of a week is a Sunday (`%U`, the `week` unit, and the `week` interval), and nothing depends on
  `CultureInfo.CurrentCulture` or on the language of the machine; a locale other than English is not supported.

**Limits and determinism.** The time constructs are bounded by the limits of the call, and a limit that is exceeded is a
located `VegaLiteRenderException` that names it:

<!-- markdownlint-disable MD013 -->
| Limit | What it bounds in the time constructs |
| ----- | ------------------------------------- |
| `MaxTicks` (1,000) | The ticks of a time axis or legend, counted before any label is made (a `tickCount` of `"millisecond"` over a year fails at once) |
| `MaxStringLength` (1,048,576) | A label, the labels of one axis or one legend together, a `timeFormat` result, and the name of a derived field; the formatter stops as soon as it would write more |
| 1,024 characters and 4,096 pieces | A d3 time format (in an axis or legend `format`, in a member of a format object, in `config.timeFormat`, in a `date:` parse entry, and in `timeFormat`, `utcFormat`, `timeParse`, and `utcParse`); `%c` counts as 13 pieces. A longer or larger one is a located error when the document is opened or the guide is built, or an expression error |
| `MaxScaleDomainSize` (100,000) | A `timeSequence` result, the `maxbins` of a time unit, the dates of a discrete date scale, and the lists of a scale, a sort, and a time `oneOf` |
| `MaxDataRows`, `MaxFieldCount`, and the cell limit | The rows and fields of the table, which the two fields of a `timeUnit` count against |
| `MaxEvaluationSteps` (50,000,000) | The date functions (one step for each date of a sequence, and a charge by the length of a format and of the text it reads) and the `timeUnit` transform (a charge for every row, higher in a zone than in UTC, because the offset of a zone is looked up) |
| 128 characters | A date text; a longer one is not a date and costs no more than a short one (256 characters for a custom `date:` format) |
<!-- markdownlint-enable MD013 -->

The cancellation token is checked while ticks, rows, and sequences are made. Everything is deterministic: the same
document, the same zone, and the same options give the same picture on every machine and in every culture, and a zone
built with `CreateCustomTimeZone` gives the same picture on every operating system.

**Supported and not supported.** The following summarizes the time support:

<!-- markdownlint-disable MD013 -->
| Supported | Not supported (reported unless noted) |
| --------- | ------------------------------------- |
| Temporal fields with implicit parsing; `parse` of `date`, `date:'...'`, and `utc:'...'`; ISO 8601 text and the documented legacy forms | The rest of the forms of `Date.parse`; a locale other than English (not reported) |
| The 40 local time units, their `utc` forms, the object form with `step`, `utc`, and `maxbins`, and the `timeUnit` transform | `binned*` units and `"binned": true` (`vl-timeunit-binned`); a `maxbins` unit in a predicate (`vl-timeunit-maxbins`) |
| Date-time objects in domains, `values`, predicates, and sort arrays | A date-time object as a `datum` (`vl-encoding-datum-datetime`) |
| Time and UTC scales, calendar ticks, `nice` and `tickCount` intervals, bands of bars, rects, and ticks | A `bandPosition` other than 0.5 on a bar or rect (`vl-encoding-bandposition-rect`); `tickBand` and the axis `bandPosition` (`vl-guide-axis-<name>`) |
| Date labels: `format` text and object, unit specifiers, the multi-format, `formatType` of `number`, `time`, and `utc`, `config.axisTemporal`, and `config.timeFormat` in legends | Custom format types (`vl-format-type-custom`); `config.customFormatTypes` and `timeFormatType` (not reported) |
| The date and time expression functions, `toDate`, and the render option `TimeZone` | `now()` (`vl-expr-function-now`); the `week` and `dayofyear` parts of a predicate (`vl-predicate-timeunit-part`, as in Vega-Lite) |
<!-- markdownlint-enable MD013 -->

Example 36 draws the same time series in UTC and in a time zone.

### Vega-Lite Line, Area, Text, and Arc Marks

The marks `line`, `area`, `trail`, `text`, and `arc` complete the set of marks that a single view can use (with the
bar-like and point-like marks above): the first three draw **paths** through the rows of a group, `text` draws a label
for every row, and `arc` draws a wedge of a circle for every row. They share the channels, the scales, the legends, and
the limits of the other marks; what is specific to them is described here. Example 37 draws a line chart with points,
a stacked area, a donut chart, and a table of labels.

**Channels per mark.** A channel that a mark does not draw is reported and ignored (`vl-channel-<name>`), except for the
channels that are accepted by every mark.

<!-- markdownlint-disable MD013 -->
| Channel | `line` | `area` | `trail` | `text` | `arc` |
| ------- | ------ | ------ | ------- | ------ | ----- |
| `x`, `y` | The point of each row | The position along the axis and the end of the range (the value, a stack, or `y2`/`x2`) | The point of each row | The anchor of the label | The center of the circle (the middle of the view when absent) |
| `x2`, `y2` | Reported | The other end of the range | Reported | Reported | Reported |
| `theta`, `radius` | Reported | Reported | Reported | A polar offset of the anchor | The angular and radial span |
| `theta2`, `radius2` | Reported | Reported | Reported | Reported | The other end of the span |
| `color`, `fill`, `stroke`, `opacity`, `fillOpacity`, `strokeOpacity`, `strokeWidth` | The paint of the path (they also **group** the rows into paths) | The paint of the path (they group) | The paint of the path (they group) | The paint of each label | The paint of each wedge |
| `size` | The stroke width of the path (it groups) | Reported (the width of its line overlay, when it has one) | The width of the trail at each point | The font size | Reported |
| `strokeDash` | The dash of the path (it groups) | Reported (the dash of its line overlay, when it has one) | Reported | Reported | Reported |
| `shape` | The shape of the overlay points when the mark has a point overlay (see *Overlays*; it does not group the paths), else reported | The shape of the overlay points when the mark has a point overlay, else reported | The shape of the overlay points when the mark has a point overlay, else reported | Reported | Reported |
| `text` | Reported | Reported | Reported | The label | Reported |
| `detail`, `key` | Group the rows into paths | Group | Group | Accepted | Accepted |
| `order` | The order of the points of a path (or of the layers of a stack) | The order of the layers of a stack, and the paths | The order of the points | The drawing order of the labels | The drawing order of the wedges |
| `tooltip`, `href`, `description` | Ignored without a report | Ignored | Ignored | Ignored | Ignored |
<!-- markdownlint-enable MD013 -->

**Paths: line, area, and trail.** The rows of the chart are **grouped** by the values of the fields of the channels
that group (`color`, `fill`, `stroke`, the opacities, `strokeWidth`, `strokeDash`, `detail`, `key`, and, for a line,
`size`; a channel with an aggregate does not group), and every group is **one path**, in the order in which the groups
first appear. The points of a path are sorted by their position along the dimension (`x`, or `y` for a horizontal
orient), keeping the data order of equal positions; an `order` channel that does not order a stack sorts them by its
fields instead (ascending or descending), and an `order` channel whose `value` is `null` or `false` (or, without an
`order` channel, a mark property `order` of `null` or `false`) keeps the order of the data. A path takes its color,
stroke width, and dash from its **first** row
(as Vega does), so two rows with different colors are two paths, never one path of two colors. A group with fewer than
two valid points (a trail: no run of two points) draws nothing.

- **`line`** strokes the curve through the points, with the stroke width 2 (the `size` channel, the `size` or
  `strokeWidth` of the mark, or `config.line.strokeWidth` give another) and no fill. The orientation is the one of
  Vega-Lite: a measure on `y` makes a vertical line, a measure on `x` a horizontal one, and `orient` overrides it.
- **`area`** fills the region between the value and a baseline. A vertical area has the dimension on `x`: its range
  runs from the **stack** (`stack` is true by default for an area with a color group, so the layers are stacked
  upward), from `y` to `y2`, or from `y` to zero (to the lowest value when the scale has no zero). A horizontal
  area mirrors this. The fill is the color of the group, with the opacity 0.7 when the area has a `line` or `point`
  overlay and no opacity of its own, and the outline is stroked only when a stroke is given. An area **without a
  measure channel draws nothing** (Vega-Lite draws a flat area at zero).
- **`trail`** is a line whose width follows the `size` channel at every point: the shape is the hull of the circles
  of the points (Vega's trail), and the size without a channel is 2. A row without a valid size ends the trail's run.
  The `size` of a trail does not group. A trail is one shape painted once: a translucent trail (`opacity`,
  `fillOpacity`) shows no
  dots where its capsules overlap, however many points it has, as in Vega (a very large trail is painted in several
  steps internally
  and composited once, so the steps leave no seams; the memory for that is bounded, and only the translucent parts
  need it: a translucent fill gets one layer, and an outline gets one when it is translucent or when the fill is
  translucent (an opaque fill needs none), so a translucent trail so large that
  its layers would need more than 32 Mi pixels (two layers, a translucent fill with an outline, over a box larger than
  about 4,096 by 4,096 pixels, or one layer, a translucent fill alone or a translucent outline over an opaque fill,
  over a box larger than about 5,800 by 5,800) is painted in steps without it and
  may show a darker dot where two steps meet). A trail is not clipped at the edge of the data rectangle (Vega-Lite
  clips only for
  `"clip": true`); what the image cuts is the part that lies beyond the chart padding, because the image does not grow
  with the overflow of a mark (see the first deviation under
  *Deviations from Vega-Lite* in *Vega-Lite Composition*).

**Interpolation.** The `interpolate` property of a line or area (in the mark definition or in `config`) takes the 15
names of Vega-Lite, and the `tension` property sets the shape parameter of three of them:

<!-- markdownlint-disable MD013 -->
| `interpolate` | Curve | `tension` |
| ------------- | ----- | --------- |
| `linear` (default), `linear-closed` | Straight segments (and the closing segment) | Ignored |
| `step`, `step-before`, `step-after` | A step in the middle of the interval, at its start, or at its end | Ignored |
| `basis`, `basis-open`, `basis-closed` | A uniform cubic B-spline (it passes through the first and last point only) | Ignored |
| `bundle` | A B-spline straightened by `tension` (the `beta` of d3-shape; **a line only**) | 0.85 by default; 1 gives `basis` |
| `cardinal`, `cardinal-open`, `cardinal-closed` | A cardinal spline through the points | 0 by default |
| `catmull-rom` | A Catmull-Rom spline (the `alpha` of the parameterization) | 0.5 by default; 0 gives `cardinal` |
| `monotone` | A monotone cubic interpolation (`monotone-y` for a horizontal area, `monotone-x` otherwise) that never overshoots the data | Ignored |
| `natural` | A natural cubic spline | Ignored |
<!-- markdownlint-enable MD013 -->

A name that is not one of these is a located error when the document is opened (`catmull-rom-open` and
`catmull-rom-closed` belong to Vega, not to Vega-Lite); in the configuration, which is not checked when the document
is opened, such a name draws a `linear` curve and is reported (`vl-mark-interpolate-unknown`). The `bundle` curve
cannot draw an area: the area is drawn
with `linear` and the specification is reported (`vl-mark-interpolate-bundle-area`), also when the name comes from the
configuration. A trail has no curve. Any finite `tension` is accepted, as in Vega-Lite; a tension that makes the control
points of a spline unreasonably long (more than 32,768 pixels from the chart) draws the affected pieces as straight
lines
between the points, and a point that lies more than 32,768 pixels from the chart (which only an explicit scale domain
can cause) is treated like an invalid row and breaks the path.

**Invalid data.** A row of a path mark is **invalid** when a channel with a continuous scale (`x`, `y`, `size`, an
opacity, or a continuous color) has no finite number. The `invalid` property of the mark definition (or
`config.<mark>.invalid`, or `config.mark.invalid`) decides what the path does: the modes that start with
`break-paths-` (`break-paths-show-path-domains`, the default, `break-paths-show-domains`, and
`break-paths-filter-domains`) **break the path** at the row, so a gap appears and the next valid row starts a new part,
and `filter` **drops** the row, so the path connects its neighbors. The modes `show` and `null` (draw the invalid
value at a fallback position) are reported (`vl-mark-invalid-show`, also when the mode comes from the configuration)
and the default applies. The scale domains of
this renderer **always include the rows of invalid values**, so `filter` and `break-paths-filter-domains` are reported
with the warning `vl-mark-invalid-domains`. A mark that is not a path drops the rows that have an invalid value, as
before. **A stacked channel** (the default for a `bar`, an `area`, and an `arc`) works as in Vega-Lite: the stack counts
a null value as 0, so the other
slices keep their places, and the row is then dropped from the marks. A bar, an arc, a point, or a label of a null slice
is **not drawn** (it is not
a zero-width slice), and a stacked `area` breaks its path at the null (a layer that has no value there draws no area at
it).

**Overlays.** A `line`, `area`, `trail`, or `rule` can draw **points** over its marks with `"point": true`, an object of
point mark properties (`{"size": 100, "fill": "white"}`), or `"transparent"` (points with the opacity 0), and an `area`
can stroke its top edge with `"line": true` or an object of line properties. `"point": false` or `null` switches off
what `config.<mark>.point` enables. Only the `point` of the mark itself can be `"transparent"` (Vega-Lite's
`getPointOverlay` reads a configuration value that is not an object as the default overlay, so
`config.line.point: "transparent"` draws opaque points). The overlays use the scales and the stack of the mark, and a
stacked area's line
is drawn at the top of its layer. The overlay points draw the channels of a `point` mark that the path itself does not,
as Vega-Lite's path overlay does: the `shape` channel gives them their shapes (a circle, a square, and so on, by group),
and an `encoding.shape` also **turns the points on** when the mark has no `point` property and `config.<mark>.point`
does not say otherwise, so `{"mark": "line", "encoding": {"shape": {"field": "g"}}}` draws the points; `"point": false`
or `null` keeps them off and the `shape` is then reported (`vl-channel-shape`: the mark does not draw it). The `shape`
is
dropped from the path, so it does not split a line into more paths and raises no report on the line; a shape and a color
of the same field share one legend (the shapes with the colors), and another field gets its own. The symbols of that
legend are filled when the points are filled (the color is their fill, not the stroke of the
line). The `angle` and `strokeDash` channels and the `size` of an area turn, dash, and size the points too, with the
scale ranges of a point mark; the line overlay of an area (`"line": true`) draws the `strokeDash` as the dash of its
line
and the
`size` as its width (the range of the stroke widths, 1 to 4 by default). The path mark itself does not draw these
channels,
so they are still reported (`vl-channel-angle`, `vl-channel-strokedash`, `vl-channel-size`) with a message that names
the
overlay that draws them. The overlays follow the configuration that is in effect, including the `config` of the render
options (`config.line.point`, `config.area.line`), for the reports as for the drawing. A `size` channel of a line
reaches
the overlay points through the size scale of the line (so the points of a line take their area from the range of its
stroke widths).

**Impute.** A stacked `line`, `area`, or `trail` with several layers must have a value for each layer at every position,
or the stack leaves holes: the pipeline therefore **imputes 0** for every missing combination of a position and a layer
before it stacks (set `"impute": null` on the stacked channel to switch it off). A **binned** dimension is not imputed
and is reported (the warning `vl-encoding-stack-impute-bin`). The `impute` property of the `x` or `y` channel
(`{"keyvals": [1, 2, 3], "method": "mean", "value": 0, "frame": [-2, 2]}`) fills the missing positions of every path
before the marks are drawn, as the `impute` transform does, and needs both an `x` and a `y` field definition.
The `impute` of another channel is reported (`vl-encoding-impute`).

**Polar channels and arcs.** An `arc` mark is a rectangle in polar coordinates: `theta` is the angle (a full turn is
`2π`, clockwise from twelve o'clock) and `radius` the distance from the center, and each has a second end
(`theta2`, `radius2`, or the mark properties `theta`, `theta2`, `radius`, `radius2`, `innerRadius`, and `outerRadius`).
The scales are the usual ones with these ranges: `theta` from 0 to a full turn, and `radius` from 0 to half of the
smaller side of the view (`rangeMin` and `rangeMax` change an end, as for `x` and `y`); a quantitative field gets a
**linear** scale (a `sqrt` scale is an explicit `scale.type`, and gives areas that grow with the value), a nominal or
ordinal one a band scale for an `arc` and a point scale for a `text`, and there is no padding and no `nice`. `theta` and
`radius` stack like `x` and `y` (the stack of `theta` is a **pie chart**, the radius of the same field a radial
chart), and a field that stacks gives each row the span from the start to the end of its slice. The mark properties
`padAngle` (the gap between neighbors, in radians, constant in pixels), `cornerRadius` (the rounding of the four
corners, limited to half the width of the ring and to what fits in the wedge), `innerRadius` (a donut), and
`outerRadius` are constants, not channels. The center is the middle of the view unless `x` and `y` are given, and the
legend symbol of an arc is a filled circle. `startAngle`, `endAngle`, and `padRadius` are not properties of Vega-Lite
(they are the unknown-property warnings), `theta2` and `radius2` without `theta` and `radius` are ignored, and a `bin`
on `theta` or `radius` is reported (`vl-encoding-bin`).

**Text.** A `text` mark draws one label for every row, anchored at `x` and `y` (the middle of the view when absent).
The label is the `text` channel, else the `text` property of the mark (a string, or an array of lines); a field is
formatted with its `format` and `formatType` (`number`, `time`, or `utc`; another `formatType` is reported with
`vl-format-type-custom` and the default format is used): a quantitative field with the d3 number format (the empty
format by default, so `1234.5` is `1234.5`, and `",.1f"` gives `1,234.5`; a null is `0`, as in Vega), a temporal field
or a field with a `timeUnit` with the time format (the format, else the specifier of the time unit, else
`config.timeFormat`), a binned field as `start – end` (with an en dash), and everything else as its JavaScript text (a
null is `null`). A label is split into lines at the array elements of the text, or at the literal `lineBreak` text of
the mark. The properties are `align` (`center` by default), `baseline` (`middle` by default), `dx`, `dy`, `angle`
(also a channel), `font`, `fontSize` (11 by default; the `size` channel, through the size scale, wins over it),
`fontStyle`, `fontWeight`, `limit` (the width at which a label is cut with the `ellipsis`), `lineHeight`, and the
paint. With a `theta` and a `radius` (channels or mark properties) the anchor is moved by the radius along the angle,
as in Vega: a **stacked** `theta` of a text (`"stack": true`) puts its label at the middle of its slice (the
`bandPosition` of the channel, 0.5 by default), which is how a pie chart gets its labels (an `arc` and a `text` are two
layers, see *Vega-Lite Layers and Composite Marks* and Example 43).

**Legends.** The symbols follow the mark: a `line`, `rule`, or `trail` shows a short stroked line in the color of the
group (a trail's symbol is stroked and not filled, although the trail is drawn filled; its stroke width is the legend
default), and an `area`, `arc`, or `text` shows a filled circle. The `size` legend of a **trail** shows its **width**:
as in Vega-Lite, the
symbols keep the symbol size (100, or the `symbolSize` of the legend) and take the stroke width of the size scale (1 to
4 by default), so a
thick part of the trail has a thick symbol; the `size` legend of a `line` or a `text` still scales the size of the symbol.

**Limits and determinism.** The limits of the call bound the new marks too, and a limit that is exceeded is a located
`VegaLiteRenderException` that names it, raised **before** the memory is used:

<!-- markdownlint-disable MD013 -->
| Limit | What it bounds in these marks |
| ----- | ----------------------------- |
| `MaxSceneItems` (2,000,000) | The scene items: each path counts one item and **an upper bound of its segments** (so a curve through a million points fails before its segments are made), each trail point, each text line, and each arc |
| `MaxStringLength` (1,048,576) | A label, and **the labels of one text mark together** (16 times the limit); the formatter stops as soon as a label would exceed it |
| `MaxEvaluationSteps` (50,000,000) | The rows of every path, text, and arc (one step per row), the grouping and sorting of the rows of a path (`n` log `n`), the points of the curves, the characters of a label, and an `{"expr": ...}` size |
| `MaxDataRows`, `MaxFieldCount`, and the cell limit | The rows and fields of the table, which the rows that an impute adds count against, and the number of `keyvals` of an `impute` (at most `MaxScaleDomainSize`) |
<!-- markdownlint-enable MD013 -->

The cancellation token is checked every 1,024 rows, per group, while the points of a curve are made, for every item
that is painted, and (for a trail of very large points, which is painted in chunks) between the chunks. A value that
no chart can show is not an error: a position, size, radius, font size, offset, or stroke width that is not finite,
or that is more than 32,768 pixels (four times the largest image) away or large, makes the point undefined, skips
the label, or draws nothing for the item, so a font size of `1e300` draws no label instead of failing and no single
value can make the painter spend seconds on one item. The output does not depend on the machine, the culture, or the
time: the curves and the arcs use
`Math.Sin`, `Math.Cos`, and the other math functions of the host, which can differ in the last bit between platforms,
and the tests compare with a tolerance.

**Supported and not supported.**

<!-- markdownlint-disable MD013 -->
| Supported | Not supported (reported unless noted) |
| --------- | ------------------------------------- |
| `line`, `area`, `trail`, `text`, and `arc` marks, grouped by the grouping channels, with the `order` channel | `image` and `geoshape` (`vl-mark-<type>`) |
| The 15 `interpolate` names and `tension`; `point` and `line` overlays | `bundle` for an area (`vl-mark-interpolate-bundle-area`); `catmull-rom-open` and `-closed` (an error) |
| The invalid modes `break-paths-*` and `filter`; the null slice of a stack is not drawn (a bar, an arc, a point, a label) and breaks a stacked area | The modes `show` and `null` (`vl-mark-invalid-show`); domains that leave the invalid rows out (`vl-mark-invalid-domains`), so a category that occurs only in null rows of a stacked chart stays on the axis and in the legend (Vega-Lite drops it) |
| Stack, impute, and `impute` on `x` and `y` | A binned dimension of a stacked path (`vl-encoding-stack-impute-bin`); `impute` on another channel (`vl-encoding-impute`) |
| Polar `theta`, `radius`, `theta2`, and `radius2` of an arc, and `theta` and `radius` of a text | A `bin` of `theta` or `radius` (`vl-encoding-bin`); a custom `formatType` of a text (`vl-format-type-custom`) |
| `padAngle`, `cornerRadius`, `innerRadius`, and `outerRadius` of an arc | `startAngle`, `endAngle`, and `padRadius` (`vl-unknown-property`, a warning) |
<!-- markdownlint-enable MD013 -->

Example 37 draws these marks.

### Vega-Lite Conditional Encodings

A channel can take a **`condition`** (one object or an array of objects) on `color`, `fill`, `stroke`, `opacity`,
`fillOpacity`, `strokeOpacity`, `strokeWidth`, `strokeDash`, `size`, `shape`, and `angle`. Each condition has a `test`
and either a `value` or the properties of a field definition. The `test` is an expression (`"datum.moisture < 30"`) or a
predicate object (`{"field": "day", "range": [3, 5]}`, with `equal`, `lt`, `lte`, `gt`, `gte`, `range`, `oneOf`,
`valid`, `and`, `or`, `not`, a variable `param`, and a selection `param` or the legacy `selection` with `empty`); it
runs for every row with the expression engine, under the
`MaxEvaluationSteps` budget and the cancellation token. The first condition that holds gives the mark its value (or the
value of the scale of its field); a row for which none holds gets what the channel gives without the conditions - its
own field and scale, else its `value`, else the default of the mark. Conditions that hold only values create no scale
and no legend; a condition that holds a field draws the legend of that field, and the symbols of another legend show
the first condition value (the opacity is the largest of the values). A predicate `test` may use a `timeUnit` or a
date-time object, as a filter does (see *Vega-Lite Time and Time Zones*). A condition may name a selection instead of
a `test`: `{"param": "brush", "value": "red"}`, `{"param": "brush", "empty": false, "value": "red"}`, or a field
definition; it holds for the rows that the selection selects (see *Selection parameters* in *Vega-Lite Parameters*), in
the order written with the other conditions. A condition on a channel that takes none is reported as
`vl-encoding-condition`.

### Vega-Lite Layers and Composite Marks

A view with a **`layer`** array draws each of its views one above the other in a single chart, with the scales, the
axes, and the legends of the layers shared as Vega-Lite shares them. A layer is a unit view (a `mark` and an
`encoding`) or another layered view, nested up to `MaxCompositionDepth` levels. The layers are drawn in the order of the
array, so the first layer is at the bottom, and the whole chart has the one title, background, padding, and size of the
top-level view.

**What a layer inherits.** The top-level view is the parent of its layers, and a layer is the parent of its own layers:

<!-- markdownlint-disable MD013 -->
| Property | Rule |
| -------- | ---- |
| `data` | A layer without `data` (or with `"data": null`, which Vega-Lite treats like an absent one in a layer) reads the table of its nearest ancestor (the data of a table is read and counted once, however many layers share it); a layer with its own `data` replaces it for itself and for its own layers; a top-level `"data": null` is an empty table |
| `transform` | The `transform` of the top-level view runs first, on the table of the data, then the `transform` of each layer on the result, so a top-level `calculate`, `filter`, or `aggregate` applies to every layer, and a layer can then reshape the rows for itself only. A nested layered view chains the same way |
| `encoding` | A channel of a parent is inherited by its layers. A layer that writes the same channel replaces the inherited one (in the position the parent gave it), and a channel that only the layer writes is added; each layer is checked against its own mark, so an inherited `x2` or `shape` is reported only for the layers whose mark cannot draw it |
| `width`, `height` | Only the top-level values count; they give the size of the plot, and a `{"step": n}` is applied to the merged discrete scale of the layers (the number of categories of the union times the step) |
| `title`, `config`, `background`, `padding`, `autosize`, `datasets`, `params` | Only the top-level values count. A layer that writes `width`, `height`, `autosize`, `padding`, `background`, `config`, `datasets`, or `title` is warned (`vl-layer-property-ignored`) and the value is not used; a variable parameter inside a layer is reported (`vl-param-variable`) |
<!-- markdownlint-enable MD013 -->

A `mark` that is written on a layered view itself is not drawn (only the marks of the layers are). A layer whose mark is
not supported (`image`, `geoshape`) is reported (`vl-mark-<type>`) and left out, and the other layers are drawn. A
`layer` that is empty draws a chart without marks.

**Shared scales and axes.** By default every channel that two or more layers use has **one scale**: the continuous
domain is the union of the domains of the layers (after each layer's own `zero` rule), the domain of a
discrete scale is the union of the categories in the order of the layers (sorted when every layer sorts them the same
way), and when a band scale and a point scale meet, the band scale wins. Explicit `scale.domain`s win over the domains
derived from the data and are merged, as Vega-Lite does: a continuous scale takes the extent of the union of the
explicit domains of the layers (equal domains count once, so a piecewise domain that every layer gives is kept), and a
discrete scale takes their categories once each in order of first appearance. The first domain of a quantize, quantile,
or threshold scale is used for all the layers. The axis of a position channel is also
one axis. Its title is the merge of the titles of the layers, as in Vega-Lite: a **written** title (the `title` of a
channel definition, or an `axis.title`) wins over a title **derived** from the field (`crates`, `Mean of crates`), so
a `bar` with `"title": "Crates"` and a `rule` of the mean of `crates` gives the axis the title `Crates`. Derived titles
join as `a, b` (the distinct titles in layer order: a `bar` of `crates` with a `rule` of the mean of `crates` gives
`crates, Mean of crates`), and so do different written titles (`Crates, Mean`), while equal ones are one. A written
`"title": null` removes the title. A `boxplot`, `errorbar`, or `errorband` writes the title of its measure (the
`title` you gave, else the field name), so its title wins over the one derived for the aggregate of another layer. A
channel definition that switches the axis off (`"axis": null`) removes it. Layers that cannot share a scale (a
continuous scale with a discrete one, a linear scale with a log, pow, or time scale, or different discretizing scales)
keep a scale of their own for the channel, and the warning `vl-layer-scale-conflict` names the layer that was left out.

A legend that several layers share is one legend: its properties are those of the `legend` objects of all the layers (a
property that one layer gives wins over the silence of the others, and when several give it differently the first layer
wins), its title merges the titles of the layers in the same way (a written `legend.title` or `title` wins over the
titles derived from the fields, which join as `a, b`), and its symbols are those of the first layer's mark. Layers
that give different `legend.orient` values cannot share a legend, as in Vega-Lite: each has a legend of its own.

**`resolve`.** A layered view (the top-level one, or a nested one for its own layers) can give each channel a mode,
`"shared"` or `"independent"`, for the scale, the axis, and the legend:

```json
"resolve": {
  "scale": { "y": "independent", "color": "shared" },
  "axis": { "x": "independent" },
  "legend": { "color": "independent" }
}
```

- `scale` accepts the position channels, `color`, `fill`, `stroke`, the opacities, `size`, `shape`, `strokeWidth`,
  `strokeDash`, `angle`, `theta`, `radius`, and the offsets. An **independent scale** gives each layer its own scale of
  the channel, and the axis or the legend of that channel is independent as well (one guide cannot describe two
  scales).
- `axis` accepts `x` and `y`. An **independent axis** with a **shared** scale draws one axis of the same scale for each
  layer.
- `legend` accepts the channels that have a legend. An **independent legend** draws a legend for each layer.
- With independent axes of one channel, the first axis is on the usual side (`x` at the bottom, `y` at the left), the
  next
  one on the opposite side (`x` at the top, `y` at the right) unless its `axis.orient` says otherwise, and only the
  first axis draws the grid (`axis.grid` still wins). Two layers whose explicit `axis.orient` differ do not share an
  axis.
- A channel that `resolve` does not name is shared. The value of `resolve` must be an object, its `scale`, `axis`, and
  `legend` objects must map channels to `"shared"` or `"independent"`, and a channel that the kind cannot take is a
  `VegaLiteSpecException` located at the entry (an unknown key of `resolve` itself is the warning
  `vl-unknown-property`). The `resolve` of a `concat`, `facet`, or `repeat` is described in *Vega-Lite Composition*.

**Composite marks.** `boxplot`, `errorbar`, and `errorband` are **composite marks**: Vega-Lite stands for each by a few
layers of primitive marks, and so does this package (a view that uses one is expanded into the layers when the chart is
built). The data of the layers is computed from the rows of the view by the transforms of Vega-Lite (`joinaggregate`,
`aggregate`, `filter`, and `calculate`), after the `transform` of the specification. A composite mark can be
the mark of a unit view or of a layer, and it can be layered with other marks (the errorband of Example 43 lies under a
line). The part marks do not get a tooltip, because a still picture has none, and the `tooltip` channel is ignored. The
views of the expansion count against `MaxViews` (see the limits above), and the specification errors of a composite mark
are located at its `mark`.

For all three marks, **one channel is the measure** (the quantitative or temporal `x` or `y` that is summarized) and the
other channels group the rows. The orientation is the `orient` of the mark when it has one (`vertical` puts the measure
on `y`); otherwise the continuous channel is the measure (a continuous `y` next to a discrete `x` is vertical, a
continuous `x` next to a discrete `y` is horizontal), and when both are continuous the mark is vertical unless only `y`
is temporal. A mark that has no continuous channel is a `VegaLiteSpecException`. The other channels of the encoding
(`x` or `y` of the groups, `color`, `fill`, `stroke`, `opacity`, `shape`, `detail`, and so on) are shared by all the
parts, so a `color` field draws one box or bar per group and a legend. A grouping channel with a `bin`, a `timeUnit`,
or an `aggregate` is reported (`vl-composite-grouping`): the rows are grouped by the field as it is and a channel that
aggregates is left out. The shorthand `"aggregate": "boxplot"` (and `"errorbar"`, `"errorband"`) of Vega-Lite is not
accepted; write the composite mark as the `mark`. The `title`, `scale`, and `axis` that are written on the measure are
used for its axis, and the field name is the default title.

Each part of a composite mark is **switched on or off** by the property of the mark that carries its name: `true` or an
object of mark properties switches it on (the object styles the part, for example `"box": {"fill": "gold"}`), and
`false` switches it off. A part that the mark does not mention follows the configuration (`config.boxplot`,
`config.errorbar`, or `config.errorband`), and these are the defaults:

<!-- markdownlint-disable MD013 -->
| Mark | Parts and their defaults | Other properties |
| ---- | ------------------------ | ---------------- |
| `boxplot` | `box` (on), `median` (on, `"color": "white"`), `rule` (the whiskers, on), `outliers` (on), `ticks` (the end ticks of the whiskers, off) | `extent` (a number, the multiple of the interquartile range at which the whiskers end, 1.5 by default; or `"min-max"`, the whiskers reach the minimum and the maximum and there are no outliers), `size` (the width of the box, `config.boxplot.size` of 14 by default), `orient`, `color`, `opacity`, `clip` |
| `errorbar` | `rule` (on) and `ticks` (off) | `extent` (`stderr`, the default for a mean, `stdev`, `ci`, or `iqr`, the default for a median), `center` (`mean`, the default, or `median`), `thickness` (the width of the rule and of the ticks), `size` (the length of a tick), `orient`, `color`, `opacity`, `clip` |
| `errorband` | `band` (on, opacity 0.3) and `borders` (off) | `extent`, `center`, `interpolate` and `tension` (of the band and the borders when the band is two-dimensional), `orient`, `color`, `opacity`, `clip` |
<!-- markdownlint-enable MD013 -->

**`boxplot`.** The rows of each group give the quartiles. With the default extent a whisker reaches the most extreme
row within `extent` times the interquartile range of the box, and a row beyond it is an **outlier**, drawn as a hollow
point (the `shape`, `angle`, and `strokeDash` channels reach the outliers). The box is a `bar`, the median is a `tick`
across it (white by default, `config.boxplot.median`; it takes the color of the box where the box has no height), the
whiskers are `rule` marks, and the whisker ends are `tick` marks (black). `"extent": "min-max"` ends the
whiskers at the minimum and the maximum and draws no outliers. `size` (or `config.boxplot.size`, else
`config.mark.size`)
is the width of the box and of the median. The `color` of the mark and the `color` channel color the box and the
outliers, and the whiskers keep their own color and size.

**`errorbar`.** An error bar draws the interval of a value as a `rule` between the lower and the upper end, with a
`tick` at each end when `ticks` is on. The interval comes from one of three kinds of input:

- **Observations** (a continuous measure and no range or errors): the rows of each group are aggregated. `stderr` is the
  mean plus and minus the standard error, `stdev` the mean plus and minus the standard deviation, `ci` the 95%
  confidence
  interval of the mean (deterministic, like the `ci0` and `ci1` aggregates), and `iqr` the interquartile range around
  the
  median. The center is the mark's `center`, else the median for an `iqr` extent and the mean for any other, and when
  neither the `center` nor the `extent` is written, `config.errorbar.center`.
- **A range** (`x2` or `y2` next to the measure): the measure is the lower end and the second channel is the upper end.
  The mark's `extent` and `center` are not used.
- **Errors** (`xError` and optionally `xError2`, or `yError` and optionally `yError2`): the upper end is the value plus
  the error, and the lower end is the value minus the error, or the value **plus** `xError2` when that channel is
  written
  (so `xError2` is a signed offset, usually negative). A range together with errors, `x2` together with `y2`,
  `xError2` without `xError`, and `xError` together with `yError` are a `VegaLiteSpecException`.

A `size` channel on an `errorbar` is reported (`vl-channel-size`, Vega-Lite drops it). Use the `size` property of the
mark for the length of the ticks.

**`errorband`.** An error band draws the same interval as a filled band, and optionally its borders. When the encoding
has both `x` and `y` the band is **two-dimensional**: an `area` between the lower and the upper values along the
other axis (`interpolate` and `tension` shape it and the `line` borders), as Example 43 shows. When only the measure
is encoded the band is **one-dimensional**: a `rect` across the whole plot, with `rule` borders. The band is drawn with
the opacity 0.3 by default (`"band": {"opacity": 0.5}` changes it) and `"borders": true` draws the two borders as `line`
marks (as `rule` marks for a one-dimensional band).

**Deviations from Vega-Lite.** The package differs in these documented ways:

- **Tooltips** of the parts are omitted, and the `tooltip` channel is ignored without a report.
- **`bin`, `timeUnit`, and `aggregate` on a grouping channel** are reported (`vl-composite-grouping`) and not applied;
  Vega-Lite would bin or aggregate first.
- **Log messages** of Vega-Lite that only warn (a `center` of `median` with an extent other than `iqr`, a `center` or
  `extent` next to a range or errors, an `interpolate` on a one-dimensional band, an aggregate on the measure that the
  composite mark replaces) are not reproduced.
- **Shared legends** show the symbols of the first layer's mark (Vega-Lite also merges the symbol encodings of the
  layers).
- **A layer-level `config`, `width`, `height`, `title`** and the other properties of the table above are ignored with a
  warning, as the top-level view decides them.
- **The `extent` of an `errorbar` or `errorband`** accepts only `ci`, `iqr`, `stderr`, and `stdev` (Vega-Lite's schema);
  a number or `"min-max"` is a `VegaLiteSpecException`, and the `extent` of a `boxplot` is a number that is not
  negative or `"min-max"`.
- **A `config.axisX` or `config.axisY` `orient`** is not part of the choice of the sides of independent axes; only the
  `orient` of the axes of the specification is.

Limits and determinism are those of the other marks: the work of every layer is charged to `MaxEvaluationSteps`, the
items to `MaxSceneItems`, the rows to `MaxDataRows`, and the same chart always gives the same picture. Example 43 draws
a layered chart, a box plot, and an error band.

### Vega-Lite Composition

A view can hold other views and lay them out as a grid: **`hconcat`**, **`vconcat`**, and **`concat`** place a list of
views side by side, one above the other, or in rows of `columns`; **`facet`** draws one copy of a view for each value of
a field (the `row`, `column`, and `facet` channels of a unit view do the same); and **`repeat`** draws one copy for each
value of a list of fields, with a field that the copy names by a reference. A child is a unit view, a layered view, or
another composition, nested up to `MaxCompositionDepth` levels and `MaxViews` views in all (the `spec` of a facet is a
unit or a layered view only; a composition in it, or a composition as a `layer`, is a `VegaLiteSpecException`). Examples
44 to 46 draw a concatenation, a facet, and a repeat.

**What a child inherits.** The `data` and the `transform` of a composition are those of its children, as for layers: a
child without `data` reads the table of its nearest ancestor (read and counted once), a child with its own `data` reads
that, and the `transform` of the composition runs first and the `transform` of a child after it. Each child keeps its
own
title, axes, legends (unless they are shared, see below), `view` background, and size, and is built as a chart of its
own.

<!-- markdownlint-disable MD013 -->
| Property | Rule |
| -------- | ---- |
| `width`, `height` | Of a **child chart**: its own size (a number, a `{"step": n}`, or the default of 300 and the step of 20 for a discrete scale). Of a **composition**, and `"container"` anywhere in it: ignored, because a composition has the size of the views inside it (`vl-composition-property-ignored`) |
| `autosize` | A composition is always `pad`: a `fit`, `fit-x`, `fit-y`, or `none` is warned about and ignored (`vl-composition-property-ignored`); the same warning for the `autosize` of a nested view |
| `padding`, `background`, `config`, `datasets`, `params` | Only the top-level values count. A nested view that writes `padding`, `background`, `config`, `datasets`, or `autosize` is warned about (`vl-composition-property-ignored`) and the value is not used; a variable parameter inside a composition is reported (`vl-param-variable`). The `view` property of a child chart counts for that chart |
| `title` | The title of a composition is placed at the **start** of the whole grid (above it by default, `anchor` and `orient` apply to the grid); the `title` of a child sits inside its own cell, above its chart |
<!-- markdownlint-enable MD013 -->

**The grid.** The cells are placed in rows of `columns` (an `hconcat` is one row, a `vconcat` one column, and a `concat`
without `columns` and without `config.concat.columns` one row). A cell is as large as everything that belongs to its
chart: the data rectangle with its axes, its legends, its title, and half the stroke of its border. A column starts
after the widest cell of the column before it plus the spacing, a row after the tallest cell of the row above it:

- `spacing` is the space between the cells, **20 pixels** by default (`config.concat.spacing`, `config.facet.spacing`);
  a number sets both directions and `{"row": 10, "column": 40}` each.
- `align` is `"each"` (a column is as wide as the widest cell of that column; the default of a concatenation),
  `"all"` (every column is as wide as the widest cell of the grid; the default of a facet and a repeat), or `"none"`
  (each cell follows the one before it); `{"row": ..., "column": ...}` gives the two directions separately.
- `center` (`true`, or `{"row": ..., "column": ...}`) centers a smaller cell in its column or row, as Vega does: it only
  moves the cells that start a column or a row.
- `bounds` is `"full"` (the default: a cell counts with its axes and legends) or `"flush"` (a cell counts with its width
  and height only, so the axes and the legends stick out of the cell and the neighbors' axes may overlap them).

**Shared and independent scales, axes, and legends.** A composition's `resolve` (the same shape as for layers, see
*Vega-Lite Layers and Composite Marks*) gives each channel a mode. The defaults are Vega-Lite's:

<!-- markdownlint-disable MD013 -->
| Composition | Scales | Axes | Legends |
| ----------- | ------ | ---- | ------- |
| `concat`, `hconcat`, `vconcat`, `repeat` | The position channels (`x`, `y`, `xOffset`, `yOffset`, `theta`, `radius`) are **independent**: every chart has its own. Every other channel (`color`, `size`, `shape`, the opacities, ...) is **shared**: the domain is the union of the domains of the charts | Never shared: every chart draws its own axes (`resolve.axis.x: "shared"` is warned about, `vl-concat-axis-shared`, and ignored) | A legend of a shared channel is **one legend**, merged at the highest composition that shares it and placed beside the whole grid (right by default; `legend.orient` moves it); an independent channel keeps a legend in each chart |
| `facet` | Shared (`theta` independent): all the cells use the union of their domains | Shared axes are drawn **once**, on the outer edge: the y axis at the left of every row, the x axis under every column, and the cells keep only their grid lines; an independent scale draws its axis in each cell | As above |
<!-- markdownlint-enable MD013 -->

A shared scale over several charts is built as for layers (the union of the domains; explicit domains merged; a conflict
that cannot be merged is the warning `vl-layer-scale-conflict`). A `resolve.scale.x: "shared"` of a concatenation gives
the charts one x domain but each chart still draws its own axis. A discrete `{"step": n}` size over a shared scale
is the step times the union of the categories of all the charts or cells, so every cell has the same size; an
independent scale sizes each cell by its own categories and then the columns (or rows) are not aligned unless `align`
says so.

**`facet`.** A facet draws the view of its `spec` once for each cell:

```json
"facet": { "row": { "field": "region", "type": "nominal" }, "column": { "field": "quarter", "type": "nominal" } },
"spec": { "mark": "bar", "encoding": { ... } }
```

- With a **`row` and `column` mapping** the cells are the product of the values of the two fields, row by row; a
  combination without rows is an **empty cell** (its frame and grid lines are drawn without marks). With only one of
  them the
  cells are one column or one row. With **one field** (`"facet": {"field": "g", "type": "nominal"}`, wrapped) the cells
  wrap after `columns` cells (one row without `columns`) and each cell has its label as its title. A `columns` next to a
  `row` and `column` mapping is ignored with the warning `vl-composition-columns-ignored`.
- A facet field is a field definition: `field`, `type`, `title`, `sort`, and `header`. The cells are in **ascending**
  order of the field by default; `"sort": "descending"` reverses it, and `"sort": null` is the default order again, as
  in
  Vega-Lite. A `sort` array puts the listed values first in that order and the other values after them in order of first
  appearance (no value is dropped). A missing or `null` value is a cell of its own, labeled `null` (first when
  ascending,
  last when descending).
- The rows that a cell draws are the rows of its value: every transform of the encoding (an `aggregate`, a `stack`, a
  `bin`) runs on the rows of the cell, and a shared scale is the union of the cells (see the deviations). A `spec` with
  a `data` of its own is not partitioned: every cell draws that table.
- A **unit view with a `row`, `column`, or `facet` channel** is the same as a facet over that view without those
  channels
  (its `mark`, `width`, `height`, and the rest of its encoding stay in the cell, and its `title` is the title of the
  whole). The channels are reported (`vl-channel-row`, `-column`, `-facet`) only where Vega-Lite cannot facet either: on
  a layer, on a layered view, or in the `spec` of a facet.

**Headers.** The values of a `row` or `column` field label the rows and columns, and the field names title them, as in
Vega-Lite: a **row header** is at the left of its row (the label, rotated, next to the shared y axis, and the title of
all the rows beyond them) and a **column header** above its column (the label with the title above all the labels). The
labels are 10 pixels and the titles bold 11 pixels (`config.header.labelFontSize` and `titleFontSize` change them). The
`header` object of the field definition sets (a property of the field definition wins over `config.headerRow`,
`config.headerColumn`, or `config.headerFacet` of its channel, and that over `config.header`):

- `title` (a text, an array of lines, or `null` for none), `labels` (`false` hides the labels), `format` and
  `formatType`
  (`number`, `time`, or `utc`) of the label text, and `"header": null` to hide the labels and the title;
- the label properties `labelAlign`, `labelAnchor`, `labelAngle`, `labelBaseline`, `labelColor`, `labelFont`,
  `labelFontSize`, `labelFontStyle`, `labelFontWeight`, `labelLimit`, `labelLineHeight`, and `labelPadding`, and the
  title properties of the same names (`titleAlign` and so on);
- `labelOrient` and `titleOrient`: the labels of a row may be at the `right` (the **footer** of the row; the shared y
  axis stays at the left) and those of a column at the `bottom` (the footer of the column, below the cells), and a title
  may follow them to that side; another side is reported (`vl-header-orient`) and the default side is used. The
  `orient` shortcut of `header` is not read, as in Vega-Lite.

A wrapped facet has no row or column headers: the label of a cell is a title above the cell (`header.labelOrient` and
the other label properties style it; `labels: false` removes it and the cell keeps its chart's own title), and the title
of the field is one title above the whole grid. The `labelExpr` of a header is evaluated over the value of the cell
(`datum.value`) and its formatted label (`datum.label`; see *Vega-Lite Label Expressions*), and a custom `formatType` is
reported (`vl-format-type-custom`); a `bin`, `timeUnit`, or `aggregate` on the facet field is not applied and the raw
value is the cell (`vl-facet-field-transform`), and a `sort` by `{"field": ..., "op": ...}` orders the cells by the
field itself (`vl-facet-sort`).

**`repeat`.** A repeat draws its `spec` once for each value of a list, and a reference `{"repeat": "row"}`,
`{"repeat": "column"}`, `{"repeat": "repeat"}`, or `{"repeat": "layer"}` in the spec is replaced by the value of the
copy:

- `"repeat": ["a", "b", "c"]` is a concatenation of three copies in rows of `columns` (one row by default; the reference
  is `"repeat"`);
- `"repeat": {"row": [...], "column": [...]}` is the matrix of the two lists, row by row (the references are `"row"` and
  `"column"`; `columns` is ignored with `vl-composition-columns-ignored`, and with only one of the lists the cells are
  one column or one row);
- `"repeat": {"layer": ["a", "b"]}` is one **layered** chart with a layer for each value (the reference is `"layer"`),
  and `row` or `column` together with `layer` repeats such a layered chart in every cell.

A reference may stand for the `field` or the `datum` of a channel, the field of a `sort`, and the `field` of a
condition.
A reference that the enclosing repeat does not define is warned about (`vl-repeat-ref-unresolved`) and the channel is
dropped, as in Vega-Lite (a `facet` that is left with no field is dropped and its `spec` is drawn without a
facet). The copies are normalized before anything is read, so the product of the lists is checked against `MaxViews`
first. A repeat draws **no header**: the repeated field is the title of each axis. The `spec` of a
layer repeat gives the size of the layered chart, and the `spec` of any other repeat is the chart of each copy.

**Sizes, order, and cost.** Every cell is built and measured before it is placed, so a composition costs what its charts
cost: the work and the items are charged to the same limits (`MaxSceneItems` counts every header, footer, title, cell,
and legend before it is created), the cells and headers are checked for cancellation, and `Measure` gives exactly the
size of the surface that `Render` draws. The groups of the scene are in the order of Vega-Lite (the row and column
titles, the row headers and footers, the column headers and footers, and then the cells).

**Deviations from Vega-Lite.** The package differs in these documented ways:

- **The overflow of a mark is not part of the bounds** of a cell: a point or a bar at the edge of the domain, or the
  stroke of a mark, can stick out of the cell, and the next cell may start a few pixels nearer than in a browser (the
  same rule as for the single view; only the axes, legends, title, and the border count).
- **A shared scale of a facet** is the union of the domains of the cells **after** their own transforms (Vega-Lite takes
  it from the data before an aggregate of the cell). A category sort of the shared scale by an aggregate over the cells
  keeps the order of first appearance.
- **A `spec` with its own `data`** is not partitioned by the facet (Vega-Lite's rule too); every cell draws that table,
  and the cells are those of the table of the facet.
- **The sizes of the text** (labels, titles) come from the bundled fonts, not from Vega's text estimator, so a facet
  header or a legend can differ from a browser's by a few pixels (see *Time Zone, Fonts, and Determinism*).
- **A facet field transform and a sort by an aggregate** are reported and not applied (see above), and the `header`
  properties a custom `formatType` and the sides that the header does not draw on are reported (the `labelExpr` is
  evaluated).
- **A composition ignores more than Vega-Lite does**: Vega-Lite drops only `fit` of a composition and keeps `fit-x` and
  `fit-y`; here every `autosize` but `pad`, and every `width`, `height`, and `"container"` of a composition, is warned
  about and a composition is always padded.
- **`headerBand` and `footerBand`** of the layout are not used (every cell here has a size, so a header is at the origin
  of its cell); a legend that is owned by a composition is laid out against a 0 by 0 data rectangle, so a gradient
  legend has its minimum length of 64 pixels there, as in Vega.
- **A layer of a layered chart** that writes a `width` or `height` is still ignored with `vl-layer-property-ignored`
  (Phase 10), while the `spec` of a **layer repeat** gives the size of the repeated chart, as in Vega-Lite.

### Vega-Lite Autosize and Container Sizes

For a **single or layered view** the `autosize` types are `pad` (the default: the size is the data rectangle and the
padding, axes, legends, and title are added to it), `none` (the size is the data rectangle plus the padding, and the
guides may stick out), and the fitting types `fit`, `fit-x`, and `fit-y`, which keep the **whole chart** at the size
that `width` and `height` give: the data rectangle becomes what is left after the axes, legends, and title, so a fitted
chart has a known canvas whatever its labels need. `fit` fits both directions, `fit-x` only the width and `fit-y` only
the height (the other direction is `pad`). `autosize` is a type name or an object `{"type": ..., "contains": ...}`:
with `"contains": "padding"` the size includes the padding (a 400 by 300 chart is a 400 by 300 picture), and with
`"contains": "content"`, the default of a written `autosize`, the padding is added to the size (a 400 by 300 chart is
410 by 310 with the default padding of 5). A title that overflows the fitted view is accommodated by the data rectangle
(it is iterated at most three times), and a legend counts only across its side (a legend on the right takes away width
and none of the height).

`"width": "container"` and `"height": "container"` fit that direction to the **container size** of the render options
(`ContainerWidth` and `ContainerHeight`, 400 by 300 by default): they mean `fit-x`, `fit-y`, or `fit` with `contains:
padding`, unless the specification or `config.autosize` writes an `autosize` of its own (the built-in `pad` default does
not count, a written one does). So a container chart fills the container exactly, title and legend included. An explicit
`pad` or `none` with a container size makes the container size the data rectangle.

A fit does not apply to a dimension that a **step** sizes (`{"step": n}`, or a discrete scale without a size): the
dimension is dropped from the fit with the warning `vl-layout-fit-step`, `fit` becomes `fit-y` or `fit-x`, and both
dimensions stepped make the view `pad`; `fit-x` with a step width becomes `fit-y`, as in Vega-Lite. The `resize` of an
`autosize` object is ignored (the picture is static). A composition ignores `autosize` and `container`, as described in
*Vega-Lite Composition*.

### Time Zone, Fonts, and Determinism

Rendering is **deterministic**: two renders of one document with the same options are byte-identical, for every
degree of parallelism, on every machine and in every culture. This holds because the package consults no wall clock,
environment variable, or locale (numbers are formatted with the d3 `en-US` locale and the minus sign U+2212,
culture-invariant), the time zone defaults to UTC, and only the **bundled fonts** are used unless you say otherwise.

- **Fonts.** Text is measured and drawn with the bundled Liberation Sans, Liberation Serif, and Liberation Mono, which
  are metric-compatible with Arial, Times, and Courier and are used for the generic and Arial-like families a chart
  names. A browser draws with other fonts, so the widths of labels can differ from a
  browser's by the difference of the advance widths. `VegaLiteFontOptions.UseSystemFonts` is **`false` by default**
  (unlike `ScadFontResolutionOptions` and the core catalog) so that the output does not depend on the host; set it to
  `true`, and/or give `AdditionalDirectories`, to let a **named** family (`"font": "Verdana"`) be found in your
  directories first and then among the installed fonts. A family that cannot be found falls back to the bundled sans
  font.
- **Characters a font lacks.** A character that the chosen font has no glyph for - `∝` (U+221D), `≫` (U+226B), and most
  arrows and mathematical operators are not in the Liberation fonts - is measured and drawn, per character, with the
  bundled Noto Sans, Noto Sans Math, or Noto Sans Symbols 2 (the fallback of the core `GlyphFallback`), so that text
  such as
  `"a ∝ b ≫ c"` in a text mark shows the real signs. Only the bundled Noto fonts are used, never an installed one, so
  the picture is the same on every machine (a fallback glyph is the regular style, even in a bold or italic text). A
  character that no bundled font covers (a private-use or CJK character, for example) is drawn as the font's
  missing-glyph box and reported as the warning `vl-text-missing-glyph`, once for each text mark location and distinct
  set of characters; the chart still renders, and `ThrowOnUnsupportedFeature` does not throw for it.
- **Time.** Dates, time units, time scales, and the date functions of expressions are computed in one calendar: the
  zone of `VegaLiteRenderOptions.TimeZone`, which is **UTC when `null`**, never the zone, the clock, or the culture of
  the
  machine (the names are the English ones of d3, and the digits are ASCII). So the same document and options give the
  same picture everywhere. The only input that can differ between machines is the **time zone database** that
  `TimeZoneInfo.FindSystemTimeZoneById` reads; to get exact reproducibility build the zone yourself. See *Vega-Lite Time
  and Time Zones*.
- **Expressions and transforms.** An expression is a pure function of its row and its parameters, with no clock, random
  source, or locale, and the `sample` transform is seeded from the JSON pointer of the transform, so rows and results
  are the same on every call. The one limit of exactness: the math functions come from the host's math library, so a
  transcendental result (`sin`, `exp`, `log`, ...) can differ in the last bit between platforms or from a browser. A
  regular expression that only a backtracking engine can run (look-around, back-references) is stopped after 100 ms and
  fails the render, which is the only wall-clock bound; patterns that the linear-time engine runs have none.
- **No network.** The package never opens a socket. A `url` that names a scheme, a drive letter, an absolute path, or a
  network path is reported as `vl-data-remote-url` and is **never fetched, opened, or given to your `FileResolver`**;
  the chart is drawn with an empty dataset (strict mode throws). Download such a file next to the specification and
  refer to it by a relative path.

### Vega-Lite Unsupported Features and Strict Mode

A construct that Vega-Lite defines but the package cannot draw is reported as a `VegaLiteUnsupportedFeature` with a
stable, lower-case `Feature` identifier of the form `vl-<area>-<detail>`, a message, and the `VegaLiteLocation` of the
construct, and the rest of the chart is still drawn. A name taken from the specification is lower-cased without
separators (`timeUnit` gives `timeunit`), and so is the name of an unsupported expression function
(`geoArea` gives `vl-expr-function-geoarea`); the message keeps the function name as written, so that it can be
found in the text. The identifiers are part of the documented
behavior of the package and are
never renamed once released. A **warning** (`VegaLiteDiagnostic`) is a questionable construct that still renders: its
message starts with its `vl-*` identifier and a colon. Reports are delivered once, in document order, on the calling
thread, after the work finished and before the method returns or throws.

<!-- markdownlint-disable MD013 -->
| Identifier | Kind | Reported at, and when |
| ---------- | ---- | --------------------- |
| `vl-schema-version` | Warning | `$schema`, when the major version is not 5 or 6 or the URL names no Vega-Lite version (the chart is rendered with the version 6 defaults) |
| `vl-data-empty` | Warning | The `data` object, when it has an empty `values` array or no source key |
| `vl-data-remote-url` | Unsupported | The `data` object, when `url` has a scheme (including `file:`), a drive letter, or is an absolute path (`/etc/hosts`, a leading backslash), or a network or UNC path (never fetched; empty dataset). A relative local path is supported and is not reported |
| `vl-data-parse-failed` | Warning | The `data` object, at render time: values that an explicit `format.parse` conversion (or the implicit date parse of a temporal field) could not read (one warning per field and type; those values are `null`) |
| `vl-data-values-text` | Unsupported | `data/values` given as a string, or a top-level dataset that `data.name` refers to given as a string |
| `vl-data-geo` | Unsupported | `sphere`, `graticule`, a topojson format, or `values` (or a referenced dataset) given as an object |
| `vl-mark-<type>` | Unsupported | `mark`, for every type except bar, rect, point, circle, square, rule, tick, line, area, trail, text, arc, boxplot, errorbar, and errorband (`image` and `geoshape`); the view, layer, or chart is drawn without marks and without axes |
| `vl-mark-invalid-show` | Unsupported | An `invalid` of `show` or `null` in a mark definition, or in `config.<mark>`, `config.mark`, or the `config.style` of the mark (reported at the value, in the configuration when it is written there; invalid values are not drawn at a fallback position; the default mode applies) |
| `vl-mark-invalid-domains` | Warning | An `invalid` of `filter` or `break-paths-filter-domains`, in the mark definition or the configuration: the scale domains of this renderer always include the rows of invalid values |
| `vl-mark-interpolate-bundle-area` | Unsupported | An `interpolate` of `bundle` on an `area`, in the mark definition or the configuration, which has no outline in d3-shape (the area is drawn with `linear`) |
| `vl-mark-interpolate-unknown` | Unsupported | An `interpolate` of a `line` or `area` that is not one of the 15 names of Vega-Lite; only the configuration can give one (a mark definition with such a name is an error); the line is drawn with `linear` |
| `vl-channel-<name>` | Unsupported | A channel other than x, y, xOffset, yOffset, theta, radius, theta2, radius2, color, fill, stroke, size, opacity, shape, strokeWidth, strokeDash, fillOpacity, strokeOpacity, angle, text, order, and detail (tooltip, href, description, and key are ignored); `shape` is drawn by a point mark and by the overlay points of a line, area, trail, or rule that has a point overlay (an `encoding.shape` switches the overlay on unless `point` is `false` or `null`, so a `shape` is reported only on a path mark whose overlay is off), `angle` by a point, circle, square, or text, `text` by a text mark, `theta` and `radius` by an arc or text mark, `theta2` and `radius2` by an arc, `size` by every mark but a rect, area, and arc, and `strokeDash` by every mark but a rect, area, trail, text, and arc, and the channel is reported and ignored on any other mark (an `angle`, `strokeDash`, or `size` that only the overlay points of a path mark, or the line overlay of an area, draw is reported with a message that names the overlay) (a `boxplot` draws `shape`, `angle`, and `strokeDash` with its outliers, and an `errorbar` draws `strokeDash` with its rule). `x2` and `y2` are supported on a bar, rect, rule, or area that has the `x` or `y` they extend, and on an errorbar or errorband, and are reported (`vl-channel-x2`, `vl-channel-y2`) on any other mark or without it. `xError`, `xError2`, `yError`, and `yError2` are supported on an errorbar or errorband and are reported (`vl-channel-xerror`, `vl-channel-xerror2`, `vl-channel-yerror`, `vl-channel-yerror2`) on any other mark; the `size` channel of an errorbar is reported (`vl-channel-size`). `order` and `detail` are accepted by every mark |
| `vl-channel-geo` | Unsupported | `longitude`, `latitude`, `longitude2`, `latitude2` |
| `vl-channel-row`, `-column`, `-facet` | Unsupported | A `row`, `column`, or `facet` channel of a view that cannot be faceted: a layer of a layered view, a layered view whose encoding has the channel, or the `spec` of a `facet` (the channel is ignored; use the `facet` operator). The channels of a unit view that is faceted are drawn and are not reported |
| `vl-facet-field-transform` | Unsupported | A `bin`, `timeUnit`, or `aggregate` of a facet field (the `facet` operator, or the `row`, `column`, and `facet` channels): the raw value of the field is the cell |
| `vl-facet-sort` | Unsupported | A facet `sort` by `{"field": ..., "op": ...}`: the cells are in ascending order of the facet field |
| `vl-header-orient` | Unsupported | A `header.labelOrient` or `titleOrient` on a side that the header does not draw on (a row draws at the `left` and `right`, a column at the `top` and `bottom`, the label of a wrapped facet on any side and its title `top` and `bottom`): the default side is used |
| `vl-type-geojson` | Unsupported | The `type` of a channel |
| `vl-shape-path` | Unsupported | A shape that is not a symbol name (a custom SVG path): the `shape` of a mark, the `value` of a `shape` channel, or an entry of its `scale.range`; a circle is drawn |
| `vl-scale-interpolate-cubehelix` | Unsupported | A `scale.interpolate` of `cubehelix` or `cubehelix-long`; RGB interpolation is used |
| `vl-scale-type-ignored` | Warning | A `scale.type` that the channel or the field type cannot use (the default type is used, as Vega-Lite does): for a temporal field every type but `time` and `utc`, and `time` or `utc` on a quantitative, nominal, or ordinal field; and a quantitative or temporal field on a `shape` or `strokeDash` channel without a discretizing scale type (the scale is ordinal) |
| `vl-scale-zero-ignored` | Warning | A `scale.zero` on a temporal field with a time or UTC scale or a color scale, where zero has no meaning (it is ignored, as in Vega-Lite) |
| `vl-scale-nice-interval` | Warning | A time interval (`"month"` or `{"interval": "month", "step": 3}`) as the `scale.nice` of a scale that is not a time scale; it is ignored |
| `vl-encoding-impute`, `-bandposition`, `-bandposition-rect`, `-datum-datetime` | Unsupported | That property of a supported channel: `-impute` is the `impute` of a channel other than `x` and `y` (the impute of a position channel is run; the `impute` transform is supported); `-bandposition` is a `bandPosition` on a field that has neither a `timeUnit` nor a `bin`, or has a `bin` (not the `bandPosition` of a stacked `theta` or `radius` of a text mark, which places the label in its slice); `-bandposition-rect` is a `bandPosition` or `timeUnitBandPosition` other than 0.5 on a `bar` or `rect` whose field has a `timeUnit` (0.5 is used); `-datum-datetime` is a date-time object as the `datum` of a channel (the channel is ignored) |
| `vl-encoding-stack-impute-bin` | Warning | The binned dimension of a stacked `line`, `area`, or `trail` with a color or another group (and without `impute: null`): a layer that lacks a bin has no point there (no zero is imputed) |
| `vl-timeunit-binned` | Unsupported | A `binned` time unit (a name that starts with `binned`, or `"binned": true`) in an encoding, a condition, a predicate, or the `timeUnit` transform; the unit is ignored and the field is used as it is |
| `vl-timeunit-maxbins` | Unsupported | The `{"maxbins": n}` time unit form without a `unit` in a filter or condition predicate; the unit is ignored and the field is compared as it is (an encoding and the transform apply it) |
| `vl-predicate-timeunit-part` | Warning | The `timeUnit` of a filter or condition predicate whose unit has a `week` or `dayofyear` part: like Vega-Lite, the predicate does not apply those parts |
| `vl-encoding-condition` | Unsupported | A `condition` on a channel that takes none (`x`, `y`, the secondary channels, `order`); the condition is ignored |
| `vl-encoding-bin` | Unsupported | The `bin` of a channel that cannot bin (`order`, the secondary channels `x2` and `y2`, and `theta` and `radius`), and `bin: "binned"` (data that is binned already) on a channel other than `x` and `y`, unless it is false or null; the channel is not binned |
| `vl-encoding-sort` | Unsupported | A `sort` that is not applied: a channel (`"-y"`, `{"encoding": ...}`) that the encoding does not have or that has neither a `field` nor an `aggregate` (the message names the channel), or an `op` that cannot order categories (`argmin`, `argmax`, `values`, `exponential`, `exponentialb`); the categories are sorted ascending |
| `vl-encoding-sort-ignored` | Warning | A `sort` array, channel (`"-y"`, `{"encoding": ...}`), or object with a `field` or `op` on a quantitative field whose scale is continuous: there are no categories to order, so the sort is ignored (`"ascending"`, `"descending"`, and `null` are not reported) |
| `vl-encoding-sort-mixed-types` | Warning | A `sort` array that mixes strings, numbers, booleans, or `null`, reported at the array; the values are matched by kind (`1` does not position the text `"1"`) |
| `vl-encoding-stack-nonlinear` | Unsupported | An explicit `stack` (not false or null) of an `x` or `y` whose `scale.type` is not linear; the marks of a log, pow, sqrt, or symlog scale are not stacked (Vega-Lite does not stack on them), and any other type is stacked on the linear scale |
| `vl-scale-<type>` | Unsupported | `scale/type`, for every type except linear, log, pow, sqrt, symlog, sequential, time, utc, band, point, ordinal, quantile, quantize, threshold, and bin-ordinal (that is `identity`) |
| `vl-scale-property-<name>` | Unsupported | A scale property outside the supported list (see the Scales row of *Supported Vega-Lite Subset*; `bins` is an example that is not supported). An unknown color scheme name is not reported but is an error |
| `vl-guide-encode` | Unsupported | `axis.encode`, `legend.encode` |
| `vl-guide-legend-titleorient`, `vl-guide-legend-clipheight` | Unsupported | A `legend.titleOrient` other than `top` (the title stays above the entries), a `legend.clipHeight` (the entries are not clipped) |
| `vl-guide-axis-<name>` | Unsupported | `labelBound`, `tickBand`, `tickExtra`, `position`, `translate`, `bandPosition`, `titleX`, `titleY` of an axis |
| `vl-guide-axis-tickcount-interval` | Warning | A time interval (`"week"` or `{"interval": "week", "step": 2}`) as the `tickCount` of an axis or legend whose scale is not a time scale; the default tick count is used |
| `vl-format-type-custom` | Unsupported | An `axis.formatType`, `legend.formatType`, `header.formatType`, or the `formatType` of a `text` channel other than `number`, `time`, and `utc` (a custom format type is not evaluated; the default format is used) |
| `vl-scale-domain-raw` | Warning | The value of the parameter that a scale `domain` names, or a `domainRaw`, that cannot be a domain: not an array, fewer than two numbers or dates for a continuous scale (none for a band, point, or ordinal scale), or an entry that is not a number or a date (a text, an object, `NaN`); the scale keeps its own domain (see *A parameter as the domain of a scale* in *Vega-Lite Parameters*) |
| `vl-expr-ref` | Unsupported or warning | An `{"expr": ...}` value that is not evaluated: at a property that Vega-Lite's schema does not allow one at (`width`, `height`, a `facet` field definition, and the like), at a static site that reads `datum` (a static site has no row; the property keeps its default), in a selection (its brush `mark`, events, projection or initial value, where the schema allows none), or in the `Config` of the render options (unsupported); and, as a **warning**, an expression at a static site whose value is a date (or a regular expression) where the property takes none. The value is not used |
| `vl-offset-position` | Unsupported | An `xOffset` or `yOffset` that has no position to nest in: the `x` (or `y`) channel is missing, is quantitative, or is temporal without a `timeUnit`; or its time unit is `day`, `weekday`, `yearweekday`, binned, or `maxbins`; or the mark is a `rect` with a temporal time unit. A nominal or ordinal field with a `timeUnit` is an ordinary band parent and is drawn (not reported). A temporal field with another time unit is nested in and not reported. The offset is ignored and the marks are drawn at their position (Vega-Lite throws for a continuous position) |
| `vl-offset-ranged` | Unsupported | A continuous (quantitative or temporal) `xOffset` or `yOffset` of a `bar` or an `area`, which Vega-Lite draws as a range from the start of the band (a ranged offset). The offset is ignored and the marks are drawn at their position; the continuous offset of a point-like mark is drawn |
| `vl-expr-function-<name>` | Unsupported | The string value of an expression (also a `labelExpr`) that calls a function outside the 104 supported ones, once per call, with `<name>` lower-cased (`vl-expr-function-now` for `now`, `vl-expr-function-geoarea` for `geoArea`; `vl-expr-function-luminance` for `luminance`; the message names the function **as written**). The call evaluates to null |
| `vl-expr-view-state` | Unsupported | The string value of an expression that uses `signal`, `event`, or `item`, or the name `width`, `height`, `padding`, `autosize`, `background`, or `cursor` without declaring a parameter of that name. It evaluates to null |
| `vl-expr-construct-<name>` | Unsupported | The string value of an expression that uses an operator that is parsed but not run: `bitwise-or`, `bitwise-xor`, `bitwise-and`, `bitwise-not` (the unary `~`), `shift-left`, `shift-right`, `shift-right-unsigned`, or `in`. It evaluates to null |
| `vl-expr-runtime` | Warning | The string value of a `calculate` expression whose result is a regular expression for some rows; null is stored (one warning per transform, with the number of rows and the first row index) |
| `vl-transform-<name>` | Unsupported | An entry of `transform` for `regression`, `loess`, `density`, `quantile`, or `extent` (the entry is skipped). The transforms of a layered view, of its layers, and of the views of a `concat`, `hconcat`, `vconcat`, `facet`, or `repeat` are applied and are not reported for being there |
| `vl-transform-pivot-op` | Unsupported | A pivot `op` other than `sum` or `count`; `sum` is used |
| `vl-transform-lookup-param` | Unsupported | A lookup whose `from` is a `param` (the data of a selection); the transform is skipped |
| `vl-transform-runtime` | Warning | A pivot that skipped rows whose pivot value is null, cut its columns at `limit`, or let a pivot value replace a group field of the same name |
| `vl-composition-property-ignored` | Warning | A `width`, `height` (`"container"` included), or `autosize` other than `pad` of a `concat`, `hconcat`, `vconcat`, `facet`, or `repeat` of rows or columns (only the views inside have a size, and a composition is laid out as `pad`), the `"container"` size of a view inside one, and the `padding`, `background`, `config`, `datasets`, or `autosize` of a view inside one; only the top-level view decides them and the value is not used |
| `vl-concat-axis-shared` | Warning | A `resolve.axis.x` or `y` of `shared` on a `concat`, `hconcat`, `vconcat`, or `repeat`: the charts keep their own axes, as in Vega-Lite |
| `vl-composition-columns-ignored` | Warning | A `columns` next to a `row` and `column` mapping of a `facet` or a `repeat` (the cells follow the fields or lists) |
| `vl-repeat-ref-unresolved` | Warning | A `{"repeat": ...}` reference that the enclosing repeat does not define; the channel is dropped, as in Vega-Lite |
| `vl-layer-property-ignored` | Warning | A `width`, `height`, `autosize`, `padding`, `background`, `config`, `datasets`, or `title` of a view inside a layered view (a layer, or a nested layered view); only the top-level view decides them and the value is not used |
| `vl-layer-scale-conflict` | Warning | Layers whose scales of one channel cannot be shared (a continuous scale with a discrete one, a linear scale with a log, pow, or time scale, or different discretizing scales), reported at the channel of the layer that is left out; that layer keeps a scale of its own |
| `vl-composite-grouping` | Unsupported | A grouping channel of a `boxplot`, `errorbar`, or `errorband` (every channel but the measure, its second end or errors, and the tooltip) that has a `bin`, a `timeUnit`, or an `aggregate`: the rows are grouped by the field as it is, and a channel that aggregates is left out |
| `vl-channel-xerror`, `-xerror2`, `-yerror`, `-yerror2` | Unsupported | An `xError`, `xError2`, `yError`, or `yError2` channel on a mark other than `errorbar` and `errorband`, which use them (the channel is ignored) |
| `vl-param-selection-layer`, `-id`, `-animation` | Unsupported | A selection that is not drawn as Vega-Lite does: `-layer` is a selection that two or more layers of one layered view receive (a top-level selection of a layered view without `views`, or with `views` that name both), which Vega rejects as a duplicate signal; it is applied as an empty selection (name one layer in `views`); `-id` is a point selection over the identity of the rows (no `fields`, no `encodings`, no key in the `value`) that has an initial `value`, or a value of the host: a still chart has no row identity, so it is applied as empty; `-animation` is a selection with a timer in `on`, or with the `time` encoding: it is drawn in its initial state |
| `vl-param-selection-projection` | Warning | A selection projection or use that cannot be built: a channel in `encodings` that has no field or an aggregate is skipped, an interval initialized with a key that is not a channel uses the default encodings, a selection used as the `domain` of a scale or the `extent` of a bin that names no field (or an encoding that is not projected exactly once) while it projects several uses the first field, and a selection bound to the scales that projects a channel without a continuous scale does not bind it, and a selection bound to a legend that does not project exactly one field (or encoding) does not bind it (see *Selection parameters* in *Vega-Lite Parameters*) |
| `vl-param-variable` | Unsupported | A variable parameter of a nested view (a view inside a layer or a composition): Vega-Lite compiles only the `params` of the top-level view into signals, so the parameter is ignored (an expression that reads it fails in Vega and evaluates to `null` here); the variable parameters of the single or top-level view are supported |
| `vl-projection` | Unsupported | `projection` |
| `vl-layout-fit-step` | Warning | An `autosize` of `fit`, `fit-x`, or `fit-y` (also the one that a `"container"` size implies) whose width or height is sized by a step (`{"step": n}`, or no size on a discrete scale): the fit is dropped for that dimension, as in Vega-Lite (reported at the `autosize`, else the size property, else the view) |
| `vl-duplicate-key` | Warning | The later of two equal keys of one object (the last value wins) |
| `vl-unknown-property` | Warning | A key that Vega-Lite does not define for a view, mark definition, encoding, channel definition, or sort object |
| `vl-diagnostics-suppressed` | Warning | The first report beyond `MaxDiagnostics` |
| `vl-text-missing-glyph` | Warning | The `mark` of a `text` mark (the unknown location for axis labels, legend labels, and titles), when the text that is drawn (after truncation at `limit`, with a visible fill) has characters that neither the font nor the bundled fallback fonts have a glyph for (a private-use or CJK character, for example); the font's missing-glyph box is drawn. One warning for each mark location and distinct set of characters, listing the first eight code points as `U+XXXX`. Characters that a bundled Noto font covers (`∝`, `≫`, arrows, math signs) are drawn and are not reported. It never throws, also with `ThrowOnUnsupportedFeature` |
<!-- markdownlint-enable MD013 -->

Pass handlers to receive the reports as they are delivered, or set `ThrowOnUnsupportedFeature` to refuse an
approximate chart: the first unsupported feature, after the earlier reports were delivered to the handlers, is thrown
as a `VegaLiteUnsupportedFeatureException` that carries the `Feature` and the `Location`. See Examples 30 and 31.
The identifiers `vl-data-file`, `vl-data-named`, `vl-data-sequence`, `vl-data-parse`, `vl-data-format-csv`,
`vl-data-format-tsv`, `vl-data-format-dsv`, and `vl-data-property` were reported by earlier releases and are no longer
produced, because local files, named data, sequences, `parse`, the delimited formats, and `format.property` are now
supported (see *Vega-Lite Data*); a build that matched one of them should stop doing so.
The identifiers `vl-expr-calculate` and `vl-expr-filter`, `vl-transform-calculate`, `-filter`, `-fold`, `-flatten`,
`-sample`, `-pivot`, and `-lookup` (for the drawn view), and `vl-param-variable` (for the single drawn view) were
reported by earlier releases and are no longer produced, because expressions, those transforms, and static variable
parameters are now supported (see *Vega-Lite Expressions*, *Vega-Lite Transforms*, and *Vega-Lite Parameters*); a build
that matched one of them should stop doing so. An expression that uses something that is still deferred is reported
with the more precise identifiers above (`vl-expr-function-<name>`, `vl-expr-view-state`, `vl-expr-construct-<name>`).
The identifier `vl-scale-property-domainRaw` was reported by earlier releases and is no longer produced, because a
`domainRaw` is now the domain of its scale; `vl-expr-ref` is no longer produced for a title, padding, background,
`view`, a mark property, an axis, legend, header, or scale property, or the `value` of a channel, because an expression
reference at such a static site is now evaluated once (see *Vega-Lite Parameters*), and `vl-param-variable` is
unchanged but explains that Vega-Lite itself declares no signal for the variable parameter of a nested view.
The identifiers `vl-encoding-aggregate`, `vl-encoding-stack`, `vl-transform-aggregate`, `-joinaggregate`, `-window`,
`-bin`, `-stack`, and `-impute` (for the drawn view) were reported by earlier releases and are no longer produced,
because the aggregating, binning, and stacking transforms and encodings are now supported (see *Vega-Lite Aggregation,
Binning, and Stacking*); `vl-channel-x2` and `vl-channel-y2` are no longer produced for a bar, rect, or rule, nor
`vl-channel-order` for an `order` that orders a stack; `vl-encoding-bin` is now produced only for `bin: "binned"` on a
channel other than `x` and `y` and for a bin of `order` or a secondary channel, `vl-scale-bin-ordinal` is no longer
produced (the bin-ordinal scale is drawn), `vl-encoding-condition` is now produced only for a channel that takes no
condition, and `vl-encoding-sort` only for a form that cannot be applied. The identifiers `vl-guide-legend` (every
legend was reported), `vl-scale-color-ramp` (a quantitative color), `vl-channel-strokewidth`, `-fillopacity`, and
`-strokeopacity`, and `vl-scale-property-scheme` for a scheme other than `tableau10` were reported by earlier releases
and are no longer produced, because legends, the color schemes, the continuous and discretizing color scales, and those
channels are now supported (see *Vega-Lite Legends, Color Schemes, and Scales*); `vl-channel-shape`, `-strokedash`, and
`-angle` are now produced only for a mark that does not draw the channel, and `vl-scale-<type>` only for the type
`identity`. A build that matched one of them should stop doing so.
The identifiers `vl-type-temporal`, `vl-encoding-timeunit`, `vl-transform-filter-timeunit`,
`vl-transform-filter-datetime`, `vl-encoding-condition-test`, `vl-data-parse-date-format`, `vl-scale-time`,
`vl-scale-utc`, and the `vl-expr-function-<name>` of the date and time functions (for example
`vl-expr-function-datetime` and `vl-expr-function-timeformat`) were reported by earlier releases and are no longer
produced, because temporal fields, time units, date predicates, custom date parse formats, time and UTC scales, and the
date and time functions are now supported (see *Vega-Lite Time and Time Zones*); `vl-transform-timeunit` was produced
only for a view that was not drawn and is no longer produced at all (every view is drawn), `vl-encoding-bandposition`
only for a field without a `timeUnit`, and
`vl-format-type-custom` only for a format type other than `number`, `time`, and `utc`. A build that matched one of them
should stop doing so.
The identifiers `vl-mark-line`, `vl-mark-area`, `vl-mark-trail`, `vl-mark-text`, `vl-mark-arc`, `vl-channel-order`, and
`vl-channel-detail` were reported by earlier releases and are no longer produced, because those marks, the `order` and
`detail` channels, and the polar channels are now supported (see *Vega-Lite Line, Area, Text, and Arc Marks*);
`vl-mark-<type>` is now produced for `image` and `geoshape` only,
`vl-channel-theta`, `-radius`, `-theta2`, `-radius2`, `-text`, `-x2`, `-y2`, `-size`, `-strokedash`, `-angle`, and
`-shape`
only for a mark that does not draw the channel, and `vl-encoding-impute` only for a channel other than `x` and `y`.
A build that matched one of them should stop doing so.
The identifiers `vl-composition-layer`, `vl-mark-boxplot`, `vl-mark-errorbar`, and `vl-mark-errorband` were reported by
earlier releases and are no longer produced, because layers and the composite marks are now supported (see *Vega-Lite
Layers and Composite Marks*); the identifiers are retired and will not be reused. A `transform` of a layered view and of
its layers is now applied and is not reported, and `vl-transform-<name>` is produced only for the transforms that are
not supported. The identifiers
`vl-layer-property-ignored`, `vl-layer-scale-conflict`, `vl-composite-grouping`, `vl-channel-xerror`, `-xerror2`,
`-yerror`, and `-yerror2` are new. A build that matched one of the retired identifiers should stop doing so.
The identifiers `vl-composition-concat`, `-hconcat`, `-vconcat`, `-facet`, and `-repeat` (the whole family
`vl-composition-<name>` of the unsupported compositions) and `vl-layout-autosize-fit`, `-fit-x`, and `-fit-y` (the
family
`vl-layout-autosize-<type>`) were reported by earlier releases and are no longer produced, because the compositions and
the `fit` autosize types are now supported (see *Vega-Lite Composition* and *Vega-Lite Autosize and Container Sizes*);
they are retired and will not be reused. The message of `vl-transform-<name>` that read "the transform is not applied:
the view is a composition" is retired with them, because the views of every composition are drawn and their transforms
applied, and `vl-channel-row`, `-column`, and `-facet` are now produced only for a view that cannot be faceted. The
identifiers `vl-composition-property-ignored`, `vl-concat-axis-shared`, `vl-composition-columns-ignored`,
`vl-repeat-ref-unresolved`, `vl-layout-fit-step`, `vl-facet-field-transform`, `vl-facet-sort`, and `vl-header-orient`
are new (`vl-composition-property-ignored` is not one of the retired `vl-composition-<name>` identifiers: it names a
property that is ignored in a drawn composition). A build that matched one of the retired identifiers should stop
doing so.
The identifier `vl-expr-label` (the `labelExpr` of an axis, a legend, or a header) was reported by earlier releases
and is retired: label expressions are evaluated now (see *Vega-Lite Label Expressions*), what an expression cannot do
is reported with `vl-expr-function-<name>`, `vl-expr-view-state`, and `vl-expr-construct-<name>` at the text of the
expression, and `vl-expr-label` is never produced again and will not be reused. The identifiers `vl-channel-xoffset`
and `-yoffset` are no longer produced because the offset channels are drawn (see *Vega-Lite Offset Channels (Grouped
Bars)*); they are retired with it, and `vl-offset-position` and `vl-offset-ranged` are new (`vl-offset-position` is no
longer reported for a temporal position with a supported time unit, which is nested in now). The identifier
`vl-expr-function-format` is no longer reported either, because `format()` is supported (see *Vega-Lite Expressions*).
The identifiers `vl-param-selection`, `vl-encoding-condition-param`, `vl-transform-filter-param`,
`vl-encoding-bin-extent-param`, and `vl-transform-bin-extent-param` are retired in
Phase 12: selections are drawn in their initial state, and a bin `extent` that is a parameter sets the span of the bins,
so they are no longer reported (see *Selection parameters* in
*Vega-Lite Parameters*); `vl-param-selection-layer`, `-id`, `-animation`, and `-projection` are new.
A build that matched one of
the retired identifiers should stop doing so.
Planned later releases add, among other things, the other transforms (`regression`, `loess`, `density`, `quantile`,
`extent`), the color functions of the expression language, expressions in more
places, and `image`; until then the constructs are
reported with the identifiers above. Geographic constructs (`projection`, `geoshape`, `longitude` and `latitude`,
topojson) are not planned and remain reported.

### Vega-Lite Third-Party Attribution

`DemaConsulting.CanvasNet.VegaLite` is **derived with attribution**; it is not a clean-room implementation. The
specification model, normalization, layout structure, and marks are original code written from the public
documentation, but a small number of values and algorithms were ported from the following projects, and no source
file, test, example, data set, or documentation text of the upstream projects was copied:

- **Vega-Lite** and **Vega** (BSD-3-Clause, Copyright (c) University of Washington Interactive Data Lab): default
  configuration values, scale inference rules, the title, axis, legend, and view layout constants, and the axis and
  legend geometry; the bin algorithm and the behavior rules of the aggregating, window, stack, and impute transforms and
  of the encoding rules that compile to them (field names, titles, stacking, bin geometry); the time unit rules (the
  floor, the end, the bin choice for `maxbins`, the label specifiers, the multi-format of the axis labels, and the field
  names and titles), the date-time objects, the time predicates, and the geometry of time unit bands; the rules of the
  line, area, trail, text, and arc marks (the grouping and sorting of paths, the overlays, the invalid-data modes, the
  implicit impute of a stack, the polar stack and scales, the text formats, and the trail, arc, and text shapes); the
  compositions of Phase 11 (the grid layout of concatenations, facets, and repeats with its spacing, columns,
  alignment, centering, and bounds, the order of the cells of a facet and the properties and placement of its headers,
  the defaults of `resolve` of each composition, the expansion of a `repeat` and its field references, the facet
  channels of a unit view, and the `fit` autosize and the container sizes); the datum and the text rules of the label
  expressions of axes, legends, and headers, and the rules of the offset channels (the nested offset scale, its
  paddings and step, and the placement of the marks); the rules of the selections of Phase 12 (the tests and the
  resolution of a selection of `vega-selections`, the predicate text, the projection, and the legend binding of a
  selection of Vega-Lite, and
  the normalization of a top-level selection); and
  the palette table of the 68 color schemes, the color interpolation rules, and the rules that give a scheme its number
  of colors.
- **d3-array**, **d3-format**, **d3-scale**, **d3-scale-chromatic**, **d3-interpolate**, **d3-color**, **d3-time**,
  **d3-time-format**, **d3-shape**, and **d3-path** (ISC, Copyright Mike Bostock): the tick algorithms (the calendar
  ticks and `nice` of time scales among them), the quantile interpolation of the aggregate operations, the number format
  grammar, the calendar intervals (floor, ceil, offset, count, and ranges) and the time format and parse directives with
  the English locale, the arithmetic of the linear, band, point, logarithmic, power, symlog, time, and discretizing
  scales, the color conversions, the colors of the `tableau10`, `category10`, `observable10`, `rainbow`, and `sinebow`
  schemes, and the curves, the line and area generators, and the arc generator of the line, area, trail, and arc marks.
- **ColorBrewer** (Apache-2.0, Copyright 2002 Cynthia Brewer, Mark Harrower, and The Pennsylvania State University): the
  colors of the qualitative sets `accent`, `dark2`, `paired`, `pastel1`, `pastel2`, `set1`, `set2`, and `set3`, and the
  ramps that Vega names after it. The **Turbo** color map (Apache-2.0, Copyright 2019 Google LLC), the **cividis** color
  map (Battelle Memorial Institute license), and the **matplotlib** colormaps `viridis`, `magma`, `inferno`, and
  `plasma` (CC0, by Nathaniel J. Smith, Stefan van der Walt, and Eric Firing) supply the colors of their schemes.

The expression language and the row transforms (`calculate`, `filter`, `fold`, `flatten`, `sample`, `pivot`, and
`lookup`) are original code written from the public documentation of the Vega expression language, ECMAScript, and the
Vega-Lite transforms: no value, source file, test, or example of the upstream projects was ported or copied for them
(the `timeUnit` transform, the date and time functions, and the selection functions use the rules described above).
The functions follow the behavior that the Vega documentation describes; the
`sample` transform does not reproduce Vega's random generator but uses published algorithms (FNV-1a, SplitMix64, and
reservoir sampling) implemented from their public descriptions, and so does the bootstrap of `ci0` and `ci1`.

The license texts ship in the NuGet packages under `licenses/` (also for the `canvasnet` tool, which bundles the
assembly), and `src/DemaConsulting.CanvasNet.VegaLite/third-party/README.md` records what was ported, from which
version, and by which unit. The package is **not affiliated with, endorsed by, or promoted by** the Vega project, the
Vega-Lite project, the d3 project, or the University of Washington Interactive Data Lab; the names are used only to
describe the language that is implemented.

### Using As An Embedded Image

`VegaLiteImageSource` is the image source of this package for `.vl` and `.vl.json` files (not `.json`). With it a
document that links to a chart draws the chart through a `FolderImageResolver` of the core package (or
`CanvasNetViewer.CreateImageResolver` of the Viewer package). The chart is laid out afresh at the size of its placement,
deterministically (UTC and the bundled fonts only), and keeps **its own background** (white unless the specification
declares another), because a chart carries its own paper; its data files are looked up in the folder of the file and
nowhere else. Warnings and unsupported features are returned as notes of the source `vega-lite`.

```csharp
// An SVG document that links to files of this kind, drawn through the resolver
var resolver = new FolderImageResolver("assets", [new VegaLiteImageSource()]);
var options = new SvgLoadOptions { ImageResolver = resolver };
using var surface = SvgCodec.Load("assets/diagram.svg", 800, 600, fonts: null, options);
```

## Examples

### Example 29: Rendering a Vega-Lite Chart

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

// A bar chart with inline data. VegaLiteDocument.Open("chart.vl.json") would read the same text from a file.
var chart = VegaLiteDocument.Parse("""
    {
      "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
      "title": "Crates sold per stall",
      "width": 240,
      "height": 200,
      "data": { "values": [
        { "stall": "North", "crates": 28 },
        { "stall": "South", "crates": 55 },
        { "stall": "East", "crates": 43 },
        { "stall": "West", "crates": 91 }
      ] },
      "mark": "bar",
      "encoding": {
        "x": { "field": "stall", "type": "nominal", "axis": { "labelAngle": 0 } },
        "y": { "field": "crates", "type": "quantitative", "axis": { "title": "Crates" } }
      }
    }
    """);

// The document describes the text without loading anything
Console.WriteLine(chart.Title);                     // Output: Crates sold per stall
Console.WriteLine(string.Join(", ", chart.MarkTypes)); // Output: bar
Console.WriteLine(chart.DataReferences[0].Kind);    // Output: Inline

// Measure gives the size Render produces (the data rectangle plus the axes, title, and padding) without drawing
VegaLiteSize natural = chart.Measure();

// Render at the natural size, then at twice the size (everything, text included, is drawn sharply at the final size)
using var normal = chart.Render();
using var doubled = chart.Render(new VegaLiteRenderOptions { Scale = 2 });
Console.WriteLine($"{normal.Width}x{normal.Height} = {natural.Width}x{natural.Height}");
PngCodec.Save(doubled, "crates.png");

// Fit the chart into a fixed box: it is scaled uniformly and centered, and the margins take the background
var gray = new VegaLiteRenderOptions { Background = new Rgba32(244, 244, 244, 255) };
using var thumbnail = chart.Render(320, 240, gray);
PngCodec.Save(thumbnail, "crates-thumbnail.png");
```

### Example 30: Vega-Lite Unsupported Features and Strict Mode

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite;

// A geoshape mark is recognized but not drawn (geographic marks are not planned)
var chart = VegaLiteDocument.Parse("""
    {
      "data": { "values": [ { "week": 1, "visits": 120 }, { "week": 2, "visits": 150 } ] },
      "mark": "geoshape"
    }
    """, new VegaLiteOpenOptions { SourceName = "visits.vl.json" });

// Lenient: the chart is drawn without what is not supported, and every report is delivered once, in order, on
// this thread, before Render returns
var warnings = new List<VegaLiteDiagnostic>();
var unsupported = new List<VegaLiteUnsupportedFeature>();
var lenient = new VegaLiteRenderOptions
{
    WarningHandler = warnings.Add,
    UnsupportedFeatureHandler = unsupported.Add,
};
using var picture = chart.Render(lenient);

foreach (var feature in unsupported)
{
    // The location is file:line:column and the RFC 6901 pointer of the construct
    Console.WriteLine($"{feature.Location}: {feature.Feature}"); // Output: visits.vl.json:3:11 /mark: vl-mark-geoshape
}

// Strict: refuse an approximate chart. The first unsupported feature throws, after it was delivered to the handler
var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };

try
{
    using var never = chart.Render(strict);
}
catch (VegaLiteUnsupportedFeatureException ex)
{
    Console.WriteLine($"{ex.Location}: {ex.Feature}"); // Output: visits.vl.json:3:11 /mark: vl-mark-geoshape
}
```

### Example 31: Themes, Fonts, and Limits for a Vega-Lite Chart

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

var chart = VegaLiteDocument.Open("chart.vl.json");

var options = new VegaLiteRenderOptions
{
    // 1. A host theme: a configuration that is merged over the specification's own (the caller wins)
    Config = """{ "bar": { "fill": "steelblue" }, "title": { "fontSize": 16 } }""",

    // 2. Reproducible text: only the bundled Liberation fonts and ./fonts are consulted for a named family
    Fonts = new VegaLiteFontOptions { UseSystemFonts = false, AdditionalDirectories = ["./fonts"] },

    // 3. Bound the work for specifications you do not control: at most 10,000 data rows and 5,000 scene items
    Limits = new VegaLiteLimits { MaxDataRows = 10_000, MaxSceneItems = 5_000 },
};

// 4. Give up after 10 seconds
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

try
{
    using var picture = chart.Render(options, timeout.Token);
    PngCodec.Save(picture, "themed.png");
}
catch (VegaLiteRenderException ex)
{
    // A limit was exceeded or the data cannot be drawn; the location names the construct
    Console.WriteLine($"{ex.Location}: {ex.Message}");
}
```

### Example 32: Vega-Lite Charts from Data Files, Named Datasets, and Sequences

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

// 1. A chart whose rows are in a CSV file next to the specification. Opening it by path makes its folder the base
//    directory, so "stalls.csv" is read from there (never from the current directory) when the chart is rendered.
//    stalls.csv:   stall,crates,fresh        chart.vl.json: "data": { "url": "stalls.csv" }, "mark": "bar", ...
//                  North,28,true
//                  South,55,false
var fromFile = VegaLiteDocument.Open("charts/chart.vl.json");
using var picture = fromFile.Render();
PngCodec.Save(picture, "from-file.png");

// 2. The same text parsed from a string has no folder: say where its data lives, or serve it yourself
var text = File.ReadAllText("charts/chart.vl.json");
var inFolder = VegaLiteDocument.Parse(
    text,
    new VegaLiteOpenOptions { BaseDirectory = "charts", DataPaths = ["shared-data"] });
var inMemory = VegaLiteDocument.Parse(
    text,
    new VegaLiteOpenOptions
    {
        // The callback replaces the file system: it gets normalized logical paths and returns UTF-8 text (or null)
        FileResolver = path => path == "stalls.csv"
            ? new MemoryStream("stall,crates\nNorth,28\nSouth,55\n"u8.ToArray())
            : null,
    });

// 3. Rows that you hold in memory, for a chart that says "data": { "name": "stalls" }
var rows = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>
{
    ["stalls"] =
    [
        new Dictionary<string, object?> { ["stall"] = "North", ["crates"] = 28 },
        new Dictionary<string, object?> { ["stall"] = "South", ["crates"] = 55 },
    ],
};
var named = VegaLiteDocument.Parse("""
    {
      "data": { "name": "stalls" },
      "mark": "bar",
      "encoding": {
        "x": { "field": "stall", "type": "nominal" },
        "y": { "field": "crates", "type": "quantitative" }
      }
    }
    """);
using var fromMemory = named.Render(new VegaLiteRenderOptions { Datasets = rows });

// 4. A generated sequence needs no data at all: x = 0, 1, 2 (the stop is not included)
var sequence = VegaLiteDocument.Parse("""
    {
      "data": { "sequence": { "start": 0, "stop": 3, "as": "x" } },
      "mark": "point",
      "encoding": { "x": { "field": "x", "type": "quantitative" } }
    }
    """);
using var dots = sequence.Render();

// 5. A local file that cannot be used is an error, not an empty chart; a remote URL is reported and never fetched
try
{
    using var broken = VegaLiteDocument.Parse(text, new VegaLiteOpenOptions { BaseDirectory = "elsewhere" }).Render();
}
catch (VegaLiteRenderException ex)
{
    Console.WriteLine($"{ex.Location}: {ex.Message}"); // names the url and says that the file was not found
}
```

### Example 33: Vega-Lite Expressions, Parameters, and Row Transforms

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

// 1. Two static parameters (the second is an expression of the first), a calculate and a filter transform that read
//    them, a bar sorted by an aggregate, and a row-bound fill: a bar is red above "alert" and blue otherwise
var chart = VegaLiteDocument.Parse("""
    {
      "params": [
        { "name": "threshold", "value": 40 },
        { "name": "alert", "expr": "threshold * 1.5" }
      ],
      "data": { "values": [
        { "stall": "North", "crates": 28, "returned": 3 },
        { "stall": "South", "crates": 55, "returned": 9 },
        { "stall": "East", "crates": 43, "returned": 2 },
        { "stall": "West", "crates": 91, "returned": 12 }
      ] },
      "transform": [
        { "calculate": "datum.crates - datum.returned", "as": "sold" },
        { "filter": "datum.sold > threshold" }
      ],
      "mark": { "type": "bar", "fill": { "expr": "datum.sold > alert ? '#b22222' : '#4682b4'" } },
      "encoding": {
        "x": { "field": "stall", "type": "nominal",
               "sort": { "op": "sum", "field": "sold", "order": "descending" } },
        "y": { "field": "sold", "type": "quantitative" }
      }
    }
    """);
Console.WriteLine(string.Join(", ", chart.ParameterNames)); // Output: threshold, alert

// By default threshold is 40 and alert is 60: South (46) and East (41) are blue, West (79) is red, North (25) is
// filtered out, and the bars are ordered West, South, East by the sold total
using var normal = chart.Render();

// 2. The host replaces the definition of a parameter. threshold = 20 also moves alert (30, still computed from it), so
//    North (25) stays; alert = 100 replaces the expression, which is then not evaluated, so no bar is red
using var lower = chart.Render(new VegaLiteRenderOptions
{
    Parameters = new Dictionary<string, object?> { ["threshold"] = 20 },
});
using var calm = chart.Render(new VegaLiteRenderOptions
{
    Parameters = new Dictionary<string, object?> { ["alert"] = 100 },
});
PngCodec.Save(lower, "stalls-lower.png");

// 3. A function that is not in the supported list (here the clock, which an expression does not have) is reported,
//    evaluates to null, and the chart is still drawn; strict mode refuses the approximate chart at the first report
var dated = VegaLiteDocument.Parse("""
    {
      "data": { "values": [ { "d": 1 } ] },
      "transform": [ { "calculate": "now()", "as": "stamp" } ],
      "mark": "point",
      "encoding": { "x": { "field": "d", "type": "quantitative" } }
    }
    """);

try
{
    using var never = dated.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true });
}
catch (VegaLiteUnsupportedFeatureException ex)
{
    Console.WriteLine($"{ex.Feature} {ex.Location.Pointer}"); // Output: vl-expr-function-now /transform/0/calculate
}

// 4. A mistake in an expression is found when the document is opened, with the location of the text and the offset
//    inside it, never while rendering
try
{
    VegaLiteDocument.Parse("""{ "transform": [ { "calculate": "datum.a +", "as": "b" } ], "mark": "point" }""");
}
catch (VegaLiteSpecException ex)
{
    Console.WriteLine(ex.Location.Pointer); // Output: /transform/0/calculate
}
```

### Example 34: Vega-Lite Aggregation, Binning, and Stacking

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

// 1. A histogram: the encoding bins the weights (bin) and counts the rows of every bin (aggregate). With 10 bins asked
//    for, the extent 1.5 to 31 gets the step 5, so the bins are 0 to 35 by 5 and the counts are 3, 2, 3, 1, 1, 1, 1.
//    The y axis is titled "Count of Records" and the x axis "weight (binned)", as in Vega-Lite.
var histogram = VegaLiteDocument.Parse("""
    {
      "title": "Parcel weights",
      "width": 280,
      "height": 100,
      "data": { "values": [
        { "weight": 1.5 }, { "weight": 2.5 }, { "weight": 3 }, { "weight": 7 }, { "weight": 8.5 }, { "weight": 12 },
        { "weight": 14 }, { "weight": 14.5 }, { "weight": 18 }, { "weight": 22 }, { "weight": 25 }, { "weight": 31 }
      ] },
      "mark": "bar",
      "encoding": {
        "x": { "field": "weight", "type": "quantitative", "bin": true },
        "y": { "aggregate": "count", "type": "quantitative" }
      }
    }
    """);
using var histogramPicture = histogram.Render();
PngCodec.Save(histogramPicture, "parcel-weights.png");

// 2. A stacked bar chart: the y channel sums the units of every region and product (aggregate), and the bars stack
//    (the default for a bar) in the order of the product: north is washers 0 to 5, nuts 5 to 7, and bolts 7 to 10, and
//    south is nuts 0 to 1 and bolts 1 to 5. The y axis reaches the largest total, 10. The color legend is drawn at the
//    right of the chart, so the lenient render reports nothing.
var shipped = VegaLiteDocument.Parse("""
    {
      "title": "Units shipped per region and product",
      "width": 240,
      "height": 100,
      "data": { "values": [
        { "region": "north", "product": "bolts", "units": 3 },
        { "region": "north", "product": "nuts", "units": 2 },
        { "region": "north", "product": "washers", "units": 5 },
        { "region": "south", "product": "bolts", "units": 4 },
        { "region": "south", "product": "nuts", "units": 1 }
      ] },
      "mark": "bar",
      "encoding": {
        "x": { "field": "region", "type": "nominal", "axis": { "labelAngle": 0 } },
        "y": { "field": "units", "type": "quantitative", "aggregate": "sum" },
        "color": { "field": "product", "type": "nominal" }
      }
    }
    """);
var reports = new List<VegaLiteUnsupportedFeature>();
using var shippedPicture = shipped.Render(new VegaLiteRenderOptions { UnsupportedFeatureHandler = reports.Add });
Console.WriteLine(reports.Count); // Output: 0
PngCodec.Save(shippedPicture, "units-shipped.png");

// 3. A running total with the window transform: the default frame [null, 0] runs from the first row to the current
//    one, so the bars are 4, 7, 12, and 14 high. Unlike an aggregate, a window keeps every row and adds a result
//    computed over its neighbors.
var orders = VegaLiteDocument.Parse("""
    {
      "title": "Cumulative orders per quarter",
      "width": 160,
      "height": 140,
      "data": { "values": [
        { "quarter": "q1", "orders": 4 }, { "quarter": "q2", "orders": 3 },
        { "quarter": "q3", "orders": 5 }, { "quarter": "q4", "orders": 2 }
      ] },
      "transform": [
        { "window": [ { "op": "sum", "field": "orders", "as": "running" } ], "frame": [null, 0] }
      ],
      "mark": "bar",
      "encoding": {
        "x": { "field": "quarter", "type": "ordinal", "axis": { "labelAngle": 0 } },
        "y": { "field": "running", "type": "quantitative", "title": "Orders so far" }
      }
    }
    """);
using var ordersPicture = orders.Render();
PngCodec.Save(ordersPicture, "orders-running.png");

// 4. Data that is already binned: bin "binned" says that "from" is the start of a bin and x2 its end, so the three
//    bars of different widths fill 0 to 10, 10 to 25, and 25 to 30 (less a pixel of spacing)
var tickets = VegaLiteDocument.Parse("""
    {
      "title": "Tickets per age range",
      "width": 150,
      "height": 100,
      "data": { "values": [
        { "from": 0, "to": 10, "tickets": 4 },
        { "from": 10, "to": 25, "tickets": 7 },
        { "from": 25, "to": 30, "tickets": 2 }
      ] },
      "mark": "bar",
      "encoding": {
        "x": { "field": "from", "type": "quantitative", "bin": "binned" },
        "x2": { "field": "to" },
        "y": { "field": "tickets", "type": "quantitative" }
      }
    }
    """);
using var ticketsPicture = tickets.Render();

// A bin or a stack that needs more than the limits allow fails the render with a located exception instead of
// exhausting memory
try
{
    using var tooMany = VegaLiteDocument.Parse("""
        {
          "data": { "values": [ { "x": 0 }, { "x": 1000000 } ] },
          "transform": [ { "bin": { "step": 0.001 }, "field": "x", "as": "b" } ],
          "mark": "point",
          "encoding": { "x": { "field": "b", "type": "quantitative" } }
        }
        """).Render();
}
catch (VegaLiteRenderException ex)
{
    Console.WriteLine($"{ex.Location.Pointer}: {ex.Message}"); // names the transform and the limit that was exceeded
}
```

### Example 35: Vega-Lite Legends and Color Schemes

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

// 1. Legends are drawn for every channel that a field drives. Here "crop" drives both the color (the Set2 scheme) and the
//    shape, so the two share one legend titled "Crop", and "area" drives the size, which gets a second legend. The
//    legends sit at the right of the chart and make the picture wider by their width and the offset of 18 pixels
var beds = VegaLiteDocument.Parse("""
    {
      "title": "Garden beds",
      "width": 220,
      "height": 150,
      "data": { "values": [
        { "sun": 4, "yield": 6, "crop": "beet", "area": 2 },
        { "sun": 6, "yield": 9, "crop": "beet", "area": 4 },
        { "sun": 7, "yield": 12, "crop": "kale", "area": 6 },
        { "sun": 9, "yield": 14, "crop": "kale", "area": 8 },
        { "sun": 11, "yield": 17, "crop": "leek", "area": 10 },
        { "sun": 13, "yield": 21, "crop": "leek", "area": 12 }
      ] },
      "mark": { "type": "point", "filled": true },
      "encoding": {
        "x": { "field": "sun", "type": "quantitative", "scale": { "zero": false }, "title": "Hours of sun" },
        "y": { "field": "yield", "type": "quantitative", "title": "Yield (kg)" },
        "color": { "field": "crop", "type": "nominal", "scale": { "scheme": "set2" }, "legend": { "title": "Crop" } },
        "shape": { "field": "crop", "type": "nominal" },
        "size": { "field": "area", "type": "quantitative", "legend": { "title": "Area (m2)" } }
      }
    }
    """);
var reports = new List<VegaLiteUnsupportedFeature>();
var withLegends = beds.Measure(new VegaLiteRenderOptions { UnsupportedFeatureHandler = reports.Add });
var withoutLegends = beds.Measure(new VegaLiteRenderOptions { Config = """{ "legend": { "disable": true } }""" });
Console.WriteLine(withLegends.Width > withoutLegends.Width); // Output: True
Console.WriteLine(reports.Count); // Output: 0
using var bedsPicture = beds.Render();
PngCodec.Save(bedsPicture, "garden-beds.png");

// 2. A quantitative color gets a gradient legend: the colors are the viridis scheme (interpolated in sRGB), the legend
//    goes to the bottom, and the bar is 160 pixels long. The scale is a log scale, so the labels are at powers of ten
var rainfall = VegaLiteDocument.Parse("""
    {
      "width": 220,
      "height": 120,
      "data": { "values": [
        { "plot": 1, "depth": 10, "rain": 1 }, { "plot": 2, "depth": 14, "rain": 4 },
        { "plot": 3, "depth": 9, "rain": 20 }, { "plot": 4, "depth": 18, "rain": 100 },
        { "plot": 5, "depth": 12, "rain": 450 }, { "plot": 6, "depth": 20, "rain": 1000 }
      ] },
      "mark": "point",
      "encoding": {
        "x": { "field": "plot", "type": "quantitative", "scale": { "zero": false } },
        "y": { "field": "depth", "type": "quantitative" },
        "color": {
          "field": "rain", "type": "quantitative",
          "scale": { "type": "log", "scheme": "viridis" },
          "legend": { "title": "Rain (mm)", "orient": "bottom", "gradientLength": 160 }
        }
      }
    }
    """);
using var rainfallPicture = rainfall.Render();
PngCodec.Save(rainfallPicture, "rainfall.png");

// 3. A discretizing scale: quantize cuts the domain into five equal classes of the Oranges scheme, and the discrete
//    gradient legend shows one band per class with the class limits as labels. A scheme object gives the count
var classes = VegaLiteDocument.Parse("""
    {
      "width": 220,
      "height": 120,
      "data": { "values": [
        { "bed": "a", "week": 1, "pests": 3 }, { "bed": "a", "week": 2, "pests": 12 },
        { "bed": "b", "week": 1, "pests": 25 }, { "bed": "b", "week": 2, "pests": 40 },
        { "bed": "c", "week": 1, "pests": 18 }, { "bed": "c", "week": 2, "pests": 33 }
      ] },
      "mark": "rect",
      "encoding": {
        "x": { "field": "week", "type": "ordinal", "axis": { "labelAngle": 0 } },
        "y": { "field": "bed", "type": "nominal" },
        "color": {
          "field": "pests", "type": "quantitative",
          "scale": { "type": "quantize", "scheme": { "name": "oranges", "count": 5 } },
          "legend": { "title": "Pests" }
        }
      }
    }
    """);
using var classesPicture = classes.Render();
PngCodec.Save(classesPicture, "pests.png");

// 4. A legend property that is not drawn is reported once, at the property, and strict mode refuses it
var left = VegaLiteDocument.Parse("""
    {
      "data": { "values": [ { "a": 1, "b": "x" }, { "a": 2, "b": "y" } ] },
      "mark": "point",
      "encoding": {
        "x": { "field": "a", "type": "quantitative" },
        "color": { "field": "b", "type": "nominal", "legend": { "titleOrient": "left" } }
      }
    }
    """);

try
{
    using var never = left.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true });
}
catch (VegaLiteUnsupportedFeatureException ex)
{
    Console.WriteLine($"{ex.Feature} {ex.Location.Pointer}"); // Output: vl-guide-legend-titleorient /encoding/color/legend/titleOrient
}

// 5. A scheme name that does not exist is an error (as in Vega), not a silent change of colors
var misspelled = VegaLiteDocument.Parse("""
    {
      "data": { "values": [ { "a": 1, "b": "x" } ] },
      "mark": "point",
      "encoding": {
        "x": { "field": "a", "type": "quantitative" },
        "color": { "field": "b", "type": "nominal", "scale": { "scheme": "blue-orange" } }
      }
    }
    """);

try
{
    using var never = misspelled.Render();
}
catch (VegaLiteSpecException ex)
{
    Console.WriteLine(ex.Location.Pointer); // Output: /encoding/color/scale/scheme
}
```

### Example 36: Time Series with a Time Zone

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

// 1. Door alarms with their times in UTC (the Z), counted per day: the encoding floors every time to its day (timeUnit
//    yearmonthdate) and counts the alarms of each day (aggregate count). The type of the field must be written
var alarms = VegaLiteDocument.Parse("""
    {
      "title": "Door alarms per day",
      "width": 200,
      "height": 120,
      "data": { "values": [
        { "raised": "2024-03-09T20:15:00Z" },
        { "raised": "2024-03-10T03:30:00Z" },
        { "raised": "2024-03-10T04:45:00Z" },
        { "raised": "2024-03-10T09:10:00Z" },
        { "raised": "2024-03-10T22:00:00Z" }
      ] },
      "mark": "bar",
      "encoding": {
        "x": { "field": "raised", "type": "temporal", "timeUnit": "yearmonthdate", "title": "Day" },
        "y": { "aggregate": "count", "type": "quantitative", "title": "Alarms" }
      }
    }
    """);

// By default the chart is in UTC, never in the zone of this machine: March 9 has 1 alarm and March 10 has 4
using var inUtc = alarms.Render();
PngCodec.Save(inUtc, "alarms-utc.png");

// 2. The same data in the time zone of the building. A zone that you build yourself is the same on every operating
//    system: standard time is UTC-05:00, and daylight saving time (+1 hour) runs from the second Sunday of March at
//    02:00 to the first Sunday of November at 02:00
var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday);
var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday);
var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
    DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1), start, end);
var building = TimeZoneInfo.CreateCustomTimeZone(
    "Example/Building", TimeSpan.FromHours(-5), "Building time", "Building standard", "Building daylight", [rule]);

// The change of 2024 happens at 07:00 UTC on March 10
Console.WriteLine(building.GetUtcOffset(new DateTime(2024, 3, 10, 6, 59, 0, DateTimeKind.Utc))); // Output: -05:00:00
Console.WriteLine(building.GetUtcOffset(new DateTime(2024, 3, 10, 7, 0, 0, DateTimeKind.Utc)));  // Output: -04:00:00

// In that zone three alarms fall on March 9 (15:15, 22:30, and 23:45 local time) and two on March 10: the day of every
// local time unit starts at local midnight, and the day of the change has 23 hours
using var inZone = alarms.Render(new VegaLiteRenderOptions { TimeZone = building });
PngCodec.Save(inZone, "alarms-building.png");

// 3. A zone of the system is found by its id, which is not the same on every platform: IANA ids such as
//    "America/New_York" on Linux and macOS, Windows ids such as "Eastern Standard Time" on Windows (.NET 6 and later can
//    convert between them when ICU is available). The rules come from the operating system, so an old date can differ
//    between machines; use CreateCustomTimeZone, as above, when the picture must be exactly reproducible
try
{
    var system = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    using var inSystemZone = alarms.Render(new VegaLiteRenderOptions { TimeZone = system });
}
catch (TimeZoneNotFoundException)
{
    Console.WriteLine("This platform does not know the id");
}

// 4. A date column that is not ISO 8601 is read with a d3 time format: the quoted format of date:'...' is read in the
//    zone of the chart, and utc:'...' in UTC. The type temporal needs no parse entry of its own, and a date that does
//    not match its format becomes null with the warning vl-data-parse-failed. The ticks are at the start of every month
var visits = VegaLiteDocument.Parse("""
    {
      "width": 220,
      "height": 100,
      "data": {
        "values": [
          { "day": "05/03/2024", "tickets": 4 },
          { "day": "15/03/2024", "tickets": 7 },
          { "day": "25/04/2024", "tickets": 2 }
        ],
        "format": { "parse": { "day": "date:'%d/%m/%Y'" } }
      },
      "mark": "point",
      "encoding": {
        "x": { "field": "day", "type": "temporal", "axis": { "format": "%b %d", "tickCount": "month" } },
        "y": { "field": "tickets", "type": "quantitative" }
      }
    }
    """);
var warnings = new List<VegaLiteDiagnostic>();
using var visitsPicture = visits.Render(new VegaLiteRenderOptions { WarningHandler = warnings.Add });
Console.WriteLine(warnings.Count); // Output: 0
PngCodec.Save(visitsPicture, "visits.png");

// 5. A construct that is not supported is reported, as everywhere in the package: a binned time unit (the data is
//    already floored) is ignored, and strict mode refuses it
var binned = VegaLiteDocument.Parse("""
    {
      "data": { "values": [ { "month": "2024-03-01", "n": 3 } ] },
      "mark": "bar",
      "encoding": {
        "x": { "field": "month", "type": "temporal", "timeUnit": "binnedyearmonth" },
        "y": { "field": "n", "type": "quantitative" }
      }
    }
    """);

try
{
    using var never = binned.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true });
}
catch (VegaLiteUnsupportedFeatureException ex)
{
    Console.WriteLine($"{ex.Feature} {ex.Location.Pointer}"); // Output: vl-timeunit-binned /encoding/x/timeUnit
}
```

### Example 37: Lines, Areas, a Donut Chart, and Labels

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

// Strict mode refuses an approximate chart: every chart below is drawn completely, so nothing is thrown
var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };

// 1. A line chart with one smooth line for each site (the color groups the rows into paths), and a point on every
//    value. The rows of a path are sorted by their week, and "monotone" never overshoots the data
var lines = VegaLiteDocument.Parse("""
    {
      "title": "Visits per week",
      "width": 240,
      "height": 120,
      "data": { "values": [
        { "week": 1, "site": "north", "visits": 120 },
        { "week": 2, "site": "north", "visits": 150 },
        { "week": 3, "site": "north", "visits": 90 },
        { "week": 4, "site": "north", "visits": 180 },
        { "week": 1, "site": "south", "visits": 60 },
        { "week": 2, "site": "south", "visits": 80 },
        { "week": 3, "site": "south", "visits": 110 },
        { "week": 4, "site": "south", "visits": 100 }
      ] },
      "mark": { "type": "line", "interpolate": "monotone", "point": true },
      "encoding": {
        "x": { "field": "week", "type": "quantitative", "axis": { "tickMinStep": 1 } },
        "y": { "field": "visits", "type": "quantitative" },
        "color": { "field": "site", "type": "nominal" }
      }
    }
    """);
using var linePicture = lines.Render(strict);
PngCodec.Save(linePicture, "visits-lines.png");

// 2. The same measure as stacked areas with a line on top of each layer. The south site has no value in week 2: the
//    stack imputes 0 there, so the two layers meet instead of leaving a hole
var areas = VegaLiteDocument.Parse("""
    {
      "title": "Visits per week, stacked",
      "width": 240,
      "height": 120,
      "data": { "values": [
        { "week": 1, "site": "north", "visits": 120 },
        { "week": 2, "site": "north", "visits": 150 },
        { "week": 3, "site": "north", "visits": 90 },
        { "week": 4, "site": "north", "visits": 180 },
        { "week": 1, "site": "south", "visits": 60 },
        { "week": 3, "site": "south", "visits": 110 },
        { "week": 4, "site": "south", "visits": 100 }
      ] },
      "mark": { "type": "area", "line": true },
      "encoding": {
        "x": { "field": "week", "type": "quantitative", "axis": { "tickMinStep": 1 } },
        "y": { "field": "visits", "type": "quantitative" },
        "color": { "field": "site", "type": "nominal" }
      }
    }
    """);
using var areaPicture = areas.Render(strict);
PngCodec.Save(areaPicture, "visits-areas.png");

// 3. A donut chart: theta is stacked, so each row is a slice of the full circle, in the order of the colors; the
//    pad angle leaves a gap between the slices and the corners are rounded
var donut = VegaLiteDocument.Parse("""
    {
      "title": "Visits by site",
      "width": 160,
      "height": 160,
      "data": { "values": [
        { "site": "north", "visits": 540 },
        { "site": "south", "visits": 350 },
        { "site": "east", "visits": 210 }
      ] },
      "mark": { "type": "arc", "innerRadius": 40, "padAngle": 0.03, "cornerRadius": 4 },
      "encoding": {
        "theta": { "field": "visits", "type": "quantitative" },
        "color": { "field": "site", "type": "nominal" }
      }
    }
    """);
using var donutPicture = donut.Render(strict);
PngCodec.Save(donutPicture, "visits-donut.png");

// 4. Labels: the text of a row is its field formatted with a d3 number format ("1,234.5"), anchored at the position
//    of the row and moved up by dy
var labels = VegaLiteDocument.Parse("""
    {
      "title": "Totals",
      "width": 200,
      "height": 100,
      "data": { "values": [
        { "site": "north", "total": 1234.5 },
        { "site": "south", "total": 872.25 },
        { "site": "east", "total": 410 }
      ] },
      "mark": { "type": "text", "fontSize": 12, "dy": -8 },
      "encoding": {
        "x": { "field": "site", "type": "nominal", "sort": null },
        "y": { "field": "total", "type": "quantitative", "scale": { "domain": [0, 1400] } },
        "text": { "field": "total", "type": "quantitative", "format": ",.1f" }
      }
    }
    """);
using var labelPicture = labels.Render(strict);
PngCodec.Save(labelPicture, "visits-labels.png");

// 5. A row without a value breaks a line (the default), and "invalid": "filter" connects its neighbors instead. The
//    scale domains of this renderer still include the invalid row, which is reported as a warning
var gap = VegaLiteDocument.Parse("""
    {
      "width": 200,
      "height": 100,
      "data": { "values": [
        { "week": 1, "visits": 120 },
        { "week": 2, "visits": null },
        { "week": 3, "visits": 90 }
      ] },
      "mark": { "type": "line", "invalid": "filter" },
      "encoding": {
        "x": { "field": "week", "type": "quantitative" },
        "y": { "field": "visits", "type": "quantitative" }
      }
    }
    """);
var warnings = new List<VegaLiteDiagnostic>();
using var gapPicture = gap.Render(new VegaLiteRenderOptions { WarningHandler = warnings.Add });
Console.WriteLine(warnings.Count);                                                  // Output: 1
Console.WriteLine(warnings[0].Message.StartsWith("vl-mark-invalid-domains:"));      // Output: True
```

### Example 39: Drawing a Vega-Lite Chart in a Pandoc Document

The Pandoc filter `docs/filters/canvasnet.lua` draws a picture whose file is a `.vl.json` (or `.vl`) specification.
`scale` makes the picture sharper, and `width` stays on the picture. The chart below reads its values from the data
file `examples/stall-sales.csv`, which the specification names with a relative `data.url`. The filter hashes that
file with the specification, so changing the data draws the chart again, and the tool reads it only from the
specification's folder.

```markdown
![Crates sold per stall](examples/stall-sales.vl.json){scale=2 width=60%}
```

![Crates sold per stall](examples/stall-sales.vl.json){scale=2 width=60%}

### Example 43: Layers, a Box Plot, and an Error Band

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

// Strict mode refuses an approximate chart: every chart below is drawn completely, so nothing is thrown
var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };

// 1. Bars and the mean as a dashed rule. The layers share the data and the y scale, so the rule is drawn at the mean of
//    the bars; the y axis is titled "Crates": the bars write that title, and a written title wins over the one derived
//    for the mean ("Mean of crates"), which would join it only if the bars wrote none
var bars = VegaLiteDocument.Parse("""
    {
      "title": "Crates sold per stall and the mean",
      "width": 320,
      "height": 200,
      "data": { "values": [
        { "stall": "Apples", "crates": 28 },
        { "stall": "Pears", "crates": 55 },
        { "stall": "Plums", "crates": 43 },
        { "stall": "Cherries", "crates": 91 }
      ] },
      "layer": [
        {
          "mark": "bar",
          "encoding": {
            "x": { "field": "stall", "type": "nominal", "sort": null },
            "y": { "field": "crates", "type": "quantitative", "title": "Crates" }
          }
        },
        {
          "mark": { "type": "rule", "color": "firebrick", "strokeDash": [4, 3] },
          "encoding": { "y": { "field": "crates", "type": "quantitative", "aggregate": "mean" } }
        }
      ]
    }
    """);
using var barPicture = bars.Render(strict);
PngCodec.Save(barPicture, "stalls-mean.png");

// 2. A line with a point on every value, one color per stall. The top-level encoding is inherited by both layers, and
//    the layers share one legend
var lines = VegaLiteDocument.Parse("""
    {
      "width": 320,
      "height": 200,
      "data": { "values": [
        { "month": 1, "crates": 12, "stall": "North" },
        { "month": 2, "crates": 18, "stall": "North" },
        { "month": 3, "crates": 15, "stall": "North" },
        { "month": 1, "crates": 8, "stall": "South" },
        { "month": 2, "crates": 11, "stall": "South" },
        { "month": 3, "crates": 19, "stall": "South" }
      ] },
      "encoding": {
        "x": { "field": "month", "type": "quantitative" },
        "y": { "field": "crates", "type": "quantitative" },
        "color": { "field": "stall", "type": "nominal" }
      },
      "layer": [ { "mark": "line" }, { "mark": { "type": "point", "filled": true } } ]
    }
    """);
using var linePicture = lines.Render(strict);
PngCodec.Save(linePicture, "stalls-lines.png");

// 3. Two measures with very different ranges: each layer gets its own y scale, so there are two axes, the second one on
//    the right
var dual = VegaLiteDocument.Parse("""
    {
      "width": 240,
      "height": 140,
      "data": { "values": [
        { "month": 1, "crates": 10, "revenue": 1000 },
        { "month": 2, "crates": 14, "revenue": 1800 },
        { "month": 3, "crates": 9, "revenue": 2600 }
      ] },
      "resolve": { "scale": { "y": "independent" } },
      "layer": [
        {
          "mark": { "type": "line", "color": "steelblue" },
          "encoding": {
            "x": { "field": "month", "type": "ordinal" },
            "y": { "field": "crates", "type": "quantitative", "axis": { "title": "Crates", "titleColor": "steelblue" } }
          }
        },
        {
          "mark": { "type": "line", "color": "firebrick" },
          "encoding": {
            "x": { "field": "month", "type": "ordinal" },
            "y": { "field": "revenue", "type": "quantitative",
                   "axis": { "title": "Revenue", "titleColor": "firebrick" } }
          }
        }
      ]
    }
    """);
using var dualPicture = dual.Render(strict);
PngCodec.Save(dualPicture, "stalls-dual.png");

// 4. A box plot per orchard: the box is the quartiles with a white median, the whiskers reach the furthest value within
//    1.5 interquartile ranges, and the value beyond them is an outlier
var box = VegaLiteDocument.Parse("""
    {
      "title": "Crate weights by orchard",
      "width": 160,
      "height": 200,
      "data": { "values": [
        { "orchard": "A", "kg": 3.1 }, { "orchard": "A", "kg": 3.5 }, { "orchard": "A", "kg": 4.0 },
        { "orchard": "A", "kg": 4.4 }, { "orchard": "A", "kg": 4.6 }, { "orchard": "A", "kg": 5.2 },
        { "orchard": "A", "kg": 5.5 }, { "orchard": "A", "kg": 9.8 },
        { "orchard": "B", "kg": 6.0 }, { "orchard": "B", "kg": 6.3 }, { "orchard": "B", "kg": 6.9 },
        { "orchard": "B", "kg": 7.2 }, { "orchard": "B", "kg": 7.4 }, { "orchard": "B", "kg": 7.9 },
        { "orchard": "B", "kg": 8.3 }
      ] },
      "mark": { "type": "boxplot", "extent": 1.5 },
      "encoding": {
        "x": { "field": "orchard", "type": "nominal" },
        "y": { "field": "kg", "type": "quantitative", "title": "Weight (kg)" },
        "color": { "field": "orchard", "type": "nominal", "legend": null }
      }
    }
    """);
using var boxPicture = box.Render(strict);
PngCodec.Save(boxPicture, "orchards-box.png");

// 5. An error band of one standard deviation around the weekly mean, under a line of the mean. The band is made from
//    the raw observations; the line aggregates them again for itself
var band = VegaLiteDocument.Parse("""
    {
      "title": "Mean yield with a standard-deviation band",
      "width": 320,
      "height": 200,
      "data": { "values": [
        { "week": 1, "yield": 10 }, { "week": 1, "yield": 12 }, { "week": 1, "yield": 14 },
        { "week": 2, "yield": 13 }, { "week": 2, "yield": 16 }, { "week": 2, "yield": 19 },
        { "week": 3, "yield": 15 }, { "week": 3, "yield": 21 }, { "week": 3, "yield": 24 }
      ] },
      "layer": [
        {
          "mark": { "type": "errorband", "extent": "stdev", "borders": true },
          "encoding": {
            "x": { "field": "week", "type": "quantitative", "title": "Week" },
            "y": { "field": "yield", "type": "quantitative", "title": "Yield" }
          }
        },
        {
          "mark": { "type": "line", "color": "black" },
          "encoding": {
            "x": { "field": "week", "type": "quantitative" },
            "y": { "field": "yield", "type": "quantitative", "aggregate": "mean" }
          }
        }
      ]
    }
    """);
using var bandPicture = band.Render(strict);
PngCodec.Save(bandPicture, "yield-band.png");

// 6. A layer that sets its own width is warned about, and the width of the top-level view is used
var ignored = VegaLiteDocument.Parse("""
    {
      "data": { "values": [ { "a": "x", "b": 1 }, { "a": "y", "b": 2 } ] },
      "layer": [
        { "width": 100, "mark": "bar",
          "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "b", "type": "quantitative" } } },
        { "mark": "rule", "encoding": { "y": { "field": "b", "type": "quantitative", "aggregate": "mean" } } }
      ]
    }
    """);
var warnings = new List<VegaLiteDiagnostic>();
using var ignoredPicture = ignored.Render(new VegaLiteRenderOptions { WarningHandler = warnings.Add });
Console.WriteLine(warnings.Count);                                                  // Output: 1
Console.WriteLine(warnings[0].Message.StartsWith("vl-layer-property-ignored:"));    // Output: True

// 7. Binning the grouping channel of a box plot is not supported: strict mode throws, and without it the rows are
//    grouped by the field as it is
var grouped = VegaLiteDocument.Parse("""
    {
      "data": { "values": [ { "g": 1, "v": 1 }, { "g": 1, "v": 3 }, { "g": 2, "v": 2 }, { "g": 2, "v": 5 } ] },
      "mark": "boxplot",
      "encoding": {
        "x": { "field": "g", "type": "ordinal", "bin": true },
        "y": { "field": "v", "type": "quantitative" }
      }
    }
    """);
try
{
    using var groupedPicture = grouped.Render(strict);
}
catch (VegaLiteUnsupportedFeatureException ex)
{
    Console.WriteLine($"{ex.Feature} {ex.Location.Pointer}"); // Output: vl-composite-grouping /encoding/x
}
```

### Example 44: Concatenated Charts with a Shared Legend

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

// Strict mode refuses an approximate chart: every composition below that is drawn with it is drawn completely
var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };

// 1. Two charts side by side. The data is the composition's, so both charts read it. The color channel is shared, so
//    the stalls have one legend, beside the whole grid; the position scales are independent, so each chart has its
//    own axes; the second chart has a title of its own, inside its cell, and the composition's title is at the start
var stalls = VegaLiteDocument.Parse("""
    {
      "title": "Crates sold per stall and month",
      "data": { "values": [
        { "stall": "Apples", "month": 1, "crates": 28 }, { "stall": "Apples", "month": 2, "crates": 35 },
        { "stall": "Apples", "month": 3, "crates": 31 }, { "stall": "Pears", "month": 1, "crates": 55 },
        { "stall": "Pears", "month": 2, "crates": 48 }, { "stall": "Pears", "month": 3, "crates": 62 },
        { "stall": "Plums", "month": 1, "crates": 43 }, { "stall": "Plums", "month": 2, "crates": 51 },
        { "stall": "Plums", "month": 3, "crates": 40 }
      ] },
      "hconcat": [
        {
          "width": 160,
          "height": 160,
          "mark": "bar",
          "encoding": {
            "x": { "field": "stall", "type": "nominal" },
            "y": { "field": "crates", "type": "quantitative", "aggregate": "sum" },
            "color": { "field": "stall", "type": "nominal" }
          }
        },
        {
          "width": 160,
          "height": 160,
          "title": "By month",
          "mark": { "type": "line", "point": true },
          "encoding": {
            "x": { "field": "month", "type": "ordinal" },
            "y": { "field": "crates", "type": "quantitative" },
            "color": { "field": "stall", "type": "nominal" }
          }
        }
      ]
    }
    """);
var size = stalls.Measure();
Console.WriteLine($"{size.Width}x{size.Height}"); // Output: 491x259
using var picture = stalls.Render(strict);
PngCodec.Save(picture, "stalls-concat.png");

// 2. Four charts in two columns, 40 pixels between the columns and 10 between the rows (a concat without columns is
//    one row, an hconcat is always one row and a vconcat one column)
var grid = VegaLiteDocument.Parse("""
    {
      "data": { "values": [
        { "stall": "Apples", "crates": 28 }, { "stall": "Pears", "crates": 55 }, { "stall": "Plums", "crates": 43 }
      ] },
      "columns": 2,
      "spacing": { "row": 10, "column": 40 },
      "concat": [
        { "width": 100, "height": 80, "mark": "bar",
          "encoding": {
            "x": { "field": "stall", "type": "nominal" },
            "y": { "field": "crates", "type": "quantitative" } } },
        { "width": 100, "height": 80, "mark": "area",
          "encoding": {
            "x": { "field": "crates", "type": "quantitative" },
            "y": { "field": "crates", "type": "quantitative" } } },
        { "width": 100, "height": 80, "mark": "tick",
          "encoding": { "x": { "field": "crates", "type": "quantitative" } } },
        { "width": 100, "height": 80, "mark": "point",
          "encoding": {
            "x": { "field": "crates", "type": "quantitative" },
            "y": { "field": "stall", "type": "nominal" } } }
      ]
    }
    """);
var gridSize = grid.Measure();
Console.WriteLine($"{gridSize.Width}x{gridSize.Height}"); // Output: 340x269

// 3. The width of a composition and shared axes of a concatenation are not supported by Vega-Lite either: both are
//    warned about, and the charts keep their own size and axes
var ignored = VegaLiteDocument.Parse("""
    {
      "data": { "values": [ { "a": "x", "b": 1 }, { "a": "y", "b": 2 } ] },
      "width": 300,
      "resolve": { "axis": { "x": "shared" } },
      "hconcat": [
        {
          "mark": "bar",
          "encoding": {
            "x": { "field": "a", "type": "nominal" },
            "y": { "field": "b", "type": "quantitative" }
          }
        },
        {
          "mark": "point",
          "encoding": {
            "x": { "field": "b", "type": "quantitative" },
            "y": { "field": "b", "type": "quantitative" }
          }
        }
      ]
    }
    """);
var warnings = new List<VegaLiteDiagnostic>();
using var ignoredPicture = ignored.Render(new VegaLiteRenderOptions { WarningHandler = warnings.Add });
Console.WriteLine(warnings.Count);                                                      // Output: 2
Console.WriteLine(warnings[0].Message.StartsWith("vl-composition-property-ignored:"));  // Output: True
Console.WriteLine(warnings[1].Message.StartsWith("vl-concat-axis-shared:"));            // Output: True
```

### Example 45: Faceted Charts with Headers

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };

// 1. One cell for each region (a row) and quarter (a column). The cells share their scales, so the y axis is drawn
//    once at the left of each row and the x axis once under each column; the row header is titled "Region" with bold
//    labels, and the column header has labels but no title
var regions = VegaLiteDocument.Parse("""
    {
      "data": { "values": [
        { "region": "North", "quarter": "Q1", "stall": "Apples", "crates": 28 },
        { "region": "North", "quarter": "Q1", "stall": "Pears", "crates": 55 },
        { "region": "North", "quarter": "Q2", "stall": "Apples", "crates": 35 },
        { "region": "North", "quarter": "Q2", "stall": "Pears", "crates": 48 },
        { "region": "South", "quarter": "Q1", "stall": "Apples", "crates": 31 },
        { "region": "South", "quarter": "Q1", "stall": "Pears", "crates": 22 },
        { "region": "South", "quarter": "Q2", "stall": "Apples", "crates": 40 },
        { "region": "South", "quarter": "Q2", "stall": "Pears", "crates": 37 }
      ] },
      "facet": {
        "row": { "field": "region", "type": "nominal", "header": { "title": "Region", "labelFontWeight": "bold" } },
        "column": { "field": "quarter", "type": "nominal", "header": { "title": null } }
      },
      "spec": {
        "width": 120,
        "height": 80,
        "mark": "bar",
        "encoding": {
          "x": { "field": "stall", "type": "nominal", "title": null },
          "y": { "field": "crates", "type": "quantitative" },
          "color": { "field": "stall", "type": "nominal" }
        }
      }
    }
    """);
var size = regions.Measure();
Console.WriteLine($"{size.Width}x{size.Height}"); // Output: 424x252
using var picture = regions.Render(strict);
PngCodec.Save(picture, "regions-facet.png");

// 2. A facet of one field wraps after `columns` cells, and each cell is titled with its value. The `facet` channel of a
//    unit view is the same as the facet operator over that view; the cells are in descending order of the field
var wrapped = VegaLiteDocument.Parse("""
    {
      "data": { "values": [
        { "region": "North", "stall": "Apples", "crates": 28 }, { "region": "North", "stall": "Pears", "crates": 55 },
        { "region": "South", "stall": "Apples", "crates": 31 }, { "region": "South", "stall": "Pears", "crates": 22 },
        { "region": "East", "stall": "Apples", "crates": 40 }, { "region": "East", "stall": "Pears", "crates": 37 }
      ] },
      "width": 100,
      "height": 80,
      "mark": "bar",
      "encoding": {
        "facet": { "field": "region", "type": "nominal", "sort": "descending", "columns": 2 },
        "x": { "field": "stall", "type": "nominal" },
        "y": { "field": "crates", "type": "quantitative" }
      }
    }
    """);
var wrappedSize = wrapped.Measure();
Console.WriteLine($"{wrappedSize.Width}x{wrappedSize.Height}"); // Output: 268x308

// 3. Binning the facet field is not supported: the raw value of the field is the cell, the feature is reported at the
//    property, and strict mode refuses it
var binned = VegaLiteDocument.Parse("""
    {
      "data": { "values": [ { "g": 1, "v": 1 }, { "g": 2, "v": 3 }, { "g": 3, "v": 2 } ] },
      "facet": { "field": "g", "type": "quantitative", "bin": true },
      "columns": 2,
      "spec": {
        "mark": "bar",
        "encoding": {
          "x": { "field": "g", "type": "ordinal" },
          "y": { "field": "v", "type": "quantitative" }
        }
      }
    }
    """);
try
{
    using var binnedPicture = binned.Render(strict);
}
catch (VegaLiteUnsupportedFeatureException ex)
{
    Console.WriteLine($"{ex.Feature} {ex.Location.Pointer}"); // Output: vl-facet-field-transform /facet/bin
}
```

### Example 46: Repeated Charts

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };

// 1. A matrix of scatter plots: the row list gives the y field and the column list the x field of each chart, row by
//    row. The color legend is shared, so there is one for the whole matrix, and there are no headers: the field of each
//    axis is its title
var matrix = VegaLiteDocument.Parse("""
    {
      "data": { "values": [
        { "price": 3.1, "weight": 12, "crates": 28, "stall": "Apples" },
        { "price": 4.2, "weight": 18, "crates": 55, "stall": "Pears" },
        { "price": 2.8, "weight": 9, "crates": 43, "stall": "Plums" },
        { "price": 5.5, "weight": 21, "crates": 91, "stall": "Cherries" },
        { "price": 3.9, "weight": 15, "crates": 62, "stall": "Apples" },
        { "price": 4.8, "weight": 11, "crates": 37, "stall": "Pears" }
      ] },
      "repeat": {
        "row": ["price", "weight"],
        "column": ["weight", "crates"]
      },
      "spec": {
        "width": 100,
        "height": 100,
        "mark": "point",
        "encoding": {
          "x": { "field": { "repeat": "column" }, "type": "quantitative" },
          "y": { "field": { "repeat": "row" }, "type": "quantitative" },
          "color": { "field": "stall", "type": "nominal" }
        }
      }
    }
    """);
var size = matrix.Measure();
Console.WriteLine($"{size.Width}x{size.Height}"); // Output: 372x298
using var picture = matrix.Render(strict);
PngCodec.Save(picture, "stalls-matrix.png");

// 2. A repeat of layers is one chart with a line for each field; the `datum` of the color channel names the line in the
//    legend. The size of the repeated chart is the size of the `spec`
var layers = VegaLiteDocument.Parse("""
    {
      "data": { "values": [
        { "month": 1, "crates": 28, "revenue": 90 }, { "month": 2, "crates": 35, "revenue": 120 },
        { "month": 3, "crates": 31, "revenue": 100 }
      ] },
      "repeat": { "layer": ["crates", "revenue"] },
      "spec": {
        "width": 240,
        "height": 120,
        "mark": "line",
        "encoding": {
          "x": { "field": "month", "type": "ordinal" },
          "y": { "field": { "repeat": "layer" }, "type": "quantitative", "title": "Value" },
          "color": { "datum": { "repeat": "layer" }, "type": "nominal", "title": "Measure" }
        }
      }
    }
    """);
var layersSize = layers.Measure();
Console.WriteLine($"{layersSize.Width}x{layersSize.Height}"); // Output: 360x158

// 3. A reference that the repeat does not define (there is no "layer" list here) is warned about and the channel that
//    holds it is dropped, as in Vega-Lite
var unresolved = VegaLiteDocument.Parse("""
    {
      "data": { "values": [ { "p": 1, "q": 2 }, { "p": 2, "q": 5 } ] },
      "repeat": { "row": ["p", "q"] },
      "spec": {
        "mark": "point",
        "encoding": {
          "x": { "field": { "repeat": "row" }, "type": "quantitative" },
          "y": { "field": { "repeat": "layer" }, "type": "quantitative" }
        }
      }
    }
    """);
var warnings = new List<VegaLiteDiagnostic>();
using var unresolvedPicture = unresolved.Render(new VegaLiteRenderOptions { WarningHandler = warnings.Add });
Console.WriteLine(warnings.Count);                                                      // Output: 1
Console.WriteLine(warnings[0].Message.StartsWith("vl-repeat-ref-unresolved:"));         // Output: True
```

### Example 47: Grouped Bars and Label Expressions

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };

// 1. A grouped bar chart: the quarter is the xOffset of each stall, so the bars of a stall stand side by side. With
//    the default step of 20 pixels each bar is 20 pixels wide, and the view is 50 * (3 - 0.2 + 0.4) = 160 pixels wide
//    (the band of a stall is 40 wide with 0.2 of a step of room around it). Both are supported, so strict mode has
//    nothing to throw for
var grouped = VegaLiteDocument.Parse("""
    {
      "data": { "values": [
        { "stall": "Apples", "quarter": "Q1", "crates": 28 }, { "stall": "Apples", "quarter": "Q2", "crates": 35 },
        { "stall": "Pears", "quarter": "Q1", "crates": 55 }, { "stall": "Pears", "quarter": "Q2", "crates": 41 },
        { "stall": "Plums", "quarter": "Q1", "crates": 43 }, { "stall": "Plums", "quarter": "Q2", "crates": 62 }
      ] },
      "mark": "bar",
      "encoding": {
        "x": { "field": "stall", "type": "nominal" },
        "xOffset": { "field": "quarter", "type": "nominal" },
        "y": { "field": "crates", "type": "quantitative" },
        "color": { "field": "quarter", "type": "nominal" }
      }
    }
    """);
var size = grouped.Measure();
Console.WriteLine($"{size.Width}x{size.Height}"); // Output: 260x363
using var picture = grouped.Render(strict);
PngCodec.Save(picture, "stalls-grouped.png");

// 2. A label expression rewrites the labels of an axis before they are measured: datum.label is the text the label
//    would have and datum.value the value of the tick (a date for a time scale). The labels of the y axis here are
//    percent values, and the labels of the x axis are in capitals
var share = VegaLiteDocument.Parse("""
    {
      "data": { "values": [
        { "stall": "Apples", "share": 0.28 }, { "stall": "Pears", "share": 0.41 }, { "stall": "Plums", "share": 0.62 }
      ] },
      "mark": "bar",
      "encoding": {
        "x": { "field": "stall", "type": "nominal", "axis": { "labelExpr": "upper(datum.label)" } },
        "y": { "field": "share", "type": "quantitative", "axis": { "labelExpr": "round(datum.value * 100) + '%'" } }
      }
    }
    """);
var shareSize = share.Measure();
Console.WriteLine($"{shareSize.Width}x{shareSize.Height}"); // Output: 113x371
using var sharePicture = share.Render(strict);
PngCodec.Save(sharePicture, "stalls-share.png");

// 3. An offset without a discrete position to nest in (here x is quantitative) is reported and ignored: the marks are
//    drawn at their position, and strict mode would refuse the chart
var misplaced = VegaLiteDocument.Parse("""
    {
      "data": { "values": [ { "stall": "Apples", "crates": 28 }, { "stall": "Pears", "crates": 55 } ] },
      "mark": "point",
      "encoding": {
        "x": { "field": "crates", "type": "quantitative" },
        "xOffset": { "field": "stall", "type": "nominal" },
        "y": { "field": "stall", "type": "nominal" }
      }
    }
    """);
var features = new List<VegaLiteUnsupportedFeature>();
using var misplacedPicture = misplaced.Render(new VegaLiteRenderOptions { UnsupportedFeatureHandler = features.Add });
Console.WriteLine(features[0].Feature);  // Output: vl-offset-position
Console.WriteLine(features[0].Location.Pointer); // Output: /encoding/xOffset
```

### Example 48: Expression References and Parameters as Scale Domains

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };

// 1. Expression references ({"expr": ...}) are evaluated once with the parameters, wherever Vega-Lite allows one and
//    no row is involved: here the text, the size, and the color of the title, the padding, and the label size of the y
//    axis. "accent" is an expression of "base", so the title is 14 pixels in firebrick, with the padding of 12. All
//    are supported, so strict mode has nothing to throw for
var sales = VegaLiteDocument.Parse("""
    {
      "params": [
        { "name": "base", "value": 12 },
        { "name": "accent", "expr": "base > 10 ? '#b22222' : '#4682b4'" }
      ],
      "title": {
        "text": { "expr": "'Sales, base size ' + base" },
        "fontSize": { "expr": "base + 2" },
        "color": { "expr": "accent" }
      },
      "padding": { "expr": "base" },
      "data": { "values": [
        { "stall": "North", "crates": 28 }, { "stall": "South", "crates": 55 }, { "stall": "East", "crates": 43 }
      ] },
      "mark": "bar",
      "encoding": {
        "x": { "field": "stall", "type": "nominal" },
        "y": { "field": "crates", "type": "quantitative", "axis": { "labelFontSize": { "expr": "base - 1" } } }
      }
    }
    """);
var salesSize = sales.Measure(strict);
Console.WriteLine($"{salesSize.Width}x{salesSize.Height}"); // Output: 153x397
using var salesPicture = sales.Render(strict);
PngCodec.Save(salesPicture, "sales-references.png");

// The host replaces a parameter, and every reference that reads it follows: a base of 8 gives a smaller title and
// padding, and the color of the title is now the blue of the other branch
using var small = sales.Render(new VegaLiteRenderOptions
{
    Parameters = new Dictionary<string, object?> { ["base"] = 8 },
});

// 2. A parameter can be the domain of a scale. The value of "window" is the domain as it is (no zero, nice, or
//    padding), and the marks that it leaves out are clipped to the view. The host gives an array to replace it
var window = VegaLiteDocument.Parse("""
    {
      "params": [ { "name": "window", "value": [20, 60] } ],
      "width": 300, "height": 150,
      "data": { "values": [ { "x": 10, "y": 1 }, { "x": 30, "y": 2 }, { "x": 50, "y": 3 }, { "x": 70, "y": 4 } ] },
      "mark": "circle",
      "encoding": {
        "x": { "field": "x", "type": "quantitative", "scale": { "domain": { "param": "window" } } },
        "y": { "field": "y", "type": "quantitative" }
      }
    }
    """);
var windowSize = window.Measure(strict);
Console.WriteLine($"{windowSize.Width}x{windowSize.Height}"); // Output: 339x197
using var zoomed = window.Render(new VegaLiteRenderOptions
{
    Parameters = new Dictionary<string, object?> { ["window"] = new object[] { 0, 80 } },
});
PngCodec.Save(zoomed, "window-zoomed.png");

// 3. An expression at a property with one value for the whole chart has no row, so it cannot read datum: the
//    reference is reported (and ignored), and strict mode refuses the chart
var reads = VegaLiteDocument.Parse("""
    {
      "title": { "text": "T", "fontSize": { "expr": "datum.size" } },
      "data": { "values": [ { "x": 1 } ] },
      "mark": "point",
      "encoding": { "x": { "field": "x", "type": "quantitative" } }
    }
    """);
var features = new List<VegaLiteUnsupportedFeature>();
using var readsPicture = reads.Render(new VegaLiteRenderOptions { UnsupportedFeatureHandler = features.Add });
Console.WriteLine(features[0].Feature); // Output: vl-expr-ref
Console.WriteLine(features[0].Location.Pointer); // Output: /title/fontSize
```

### Example 49: Selections in Their Initial State

A selection is drawn the way Vega-Lite's chart looks before anyone touches it: the `value` of the selection (or the one
the host gives) is what is selected. The first chart brushes a range of a scatter plot, so the points inside the
rectangle keep their color; the second selects two bars; the third dims the legend entries of a selection that is
bound to the legend.

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.VegaLite;

var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };

// A point selection of the field "c" with the initial value B and D. The bars that it holds are blue and the others
// gray; the selection is a parameter, so the host can replace its value
var bars = VegaLiteDocument.Parse("""
    {
      "params": [
        { "name": "pick", "select": { "type": "point", "fields": ["c"] },
          "value": [ { "c": "B" }, { "c": "D" } ] }
      ],
      "data": { "values": [
        { "c": "A", "n": 28 }, { "c": "B", "n": 55 }, { "c": "C", "n": 43 }, { "c": "D", "n": 91 }
      ] },
      "mark": "bar",
      "encoding": {
        "x": { "field": "c", "type": "nominal" },
        "y": { "field": "n", "type": "quantitative" },
        "color": { "condition": { "param": "pick", "value": "steelblue" }, "value": "lightgray" }
      }
    }
    """);
Console.WriteLine(string.Join(", ", bars.ParameterNames)); // Output: pick
using var initial = bars.Render(strict);
PngCodec.Save(initial, "bars-b-and-d.png");

// The host selects A instead: a list of objects, one for each tuple of the selection
using var other = bars.Render(new VegaLiteRenderOptions
{
    Parameters = new Dictionary<string, object?>
    {
        ["pick"] = new List<Dictionary<string, object?>> { new() { ["c"] = "A" } },
    },
});
PngCodec.Save(other, "bars-a.png");
```

An interval selection with a `value` also draws the **brush**: a shade behind the marks and an outline over them
(`#333` at 0.125 and a white stroke by default; see *Selection parameters*). The points inside the range, both ends
included, take the color of their group:

```markdown
![A brush with the points inside it colored](examples/brush-selection.vl.json){scale=2 width=60%}
```

![A brush with the points inside it colored](examples/brush-selection.vl.json){scale=2 width=60%}

A selection with the value that the host or the specification gives selects the bars in the same way:

```markdown
![Two bars selected](examples/bar-selection.vl.json){scale=2 width=60%}
```

![Two bars selected](examples/bar-selection.vl.json){scale=2 width=60%}

A selection that is bound to the legend (`"bind": "legend"`) dims the entries it does not hold, the symbols and the
labels, to `config.legend.unselectedOpacity` (0.35 by default); here the marks follow the same selection through an
`opacity` condition:

```markdown
![Legend entries p and r dimmed](examples/legend-selection.vl.json){scale=2 width=60%}
```

![Legend entries p and r dimmed](examples/legend-selection.vl.json){scale=2 width=60%}
