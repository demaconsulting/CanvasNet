### DataLoader Unit Verification Design

This document describes the unit-level verification strategy for the `DataLoader` unit in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace: the internal `DataLoader` class (its constructor, `Load`,
`InferFormatType`, `Warnings`, `RowsLoaded`, the `ReserveDerivedRows` and `ReleaseRows` methods that let a transform
count the rows of a table it derives against the same row total, and the `PrimitiveField` constant). The loader turns
the `data` node of a parsed specification into a `DataTable`: inline `values` arrays, a relative local `url` (read
through the `DataFileResolver` unit as JSON, CSV, TSV, or DSV), a named dataset, and a generated sequence are read; the
same `Load` serves the `from.data` source of a lookup transform and shares the file cache, the sandbox, and the row
budget of the call; remote data and the other recognized sources (text or object values, geographic data, no data at
all) are an empty table that is never opened or fetched, and that the feature catalog reports.

#### Verification Approach

The unit is verified by parsing small, original specification strings with the real `SpecParser`, loading the `data`
node, and asserting on the resulting table: the row count, the fields in ordinal order, and each cell's kind and
value. The unit's real collaborators (`SpecParser` for the input, `DataTable` for the output, `DataFileResolver` and
`DelimitedTextParser` for the files and cells, `FeatureCatalog` and `VegaLiteDocument` for the reports that the loader's
design leaves to them) are used rather than mocked, because the unit's contract is exactly how a parsed node becomes a
table. The file sources are real implementations of the resolver's seams: `VirtualDataFiles` (an in-memory file set
behind the `FileResolver` callback, which records every request so that a test can assert that a remote URL never
reached it and that a file was requested once) and `DataTempDirectory` (a per-test temporary tree for the base
directory). The original fixtures `data-files.csv` and `data-values.json` are read from the output folder for the
fixture scenarios. No mocking or stubbing is used; the internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Expected tables are derived by hand from the rules of the unit: an object row becomes one row; a primitive or array
row becomes one row with the single field `data`; nested objects and arrays become one field per leaf named by the
canonical path (so `{"p":{"q":1,"r":[10,20]},"s.t":3,"u":{"v":{"w":"deep"}},"e":{},"a":[]}` has exactly the fields
`p.q`, `p.r.0`, `p.r.1`, `s\.t`, and `u.v.w`, and the empty containers add none); and a field that a row lacks is
null. A sequence has `start + i * step` for each index below `ceil((stop - start) / step)`. The limit scenarios count by
eye: three rows against a `MaxDataRows` of two fail, and exactly three pass; three fields against a `MaxFieldCount` of
two fail at the pointer of the third leaf, `/data/values/0/c`; two tables of three rows against a total of five fail
at the second.

Unit tests reside in `DataLoaderTests.cs` and `DataLoaderCalendarTests.cs` within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Data` folder, with the shared helpers in
`DataTestSupport.cs` and `TimeTestSupport.cs` (the latter builds the calendars: UTC, a fixed offset, and a zone with a
daylight-saving rule). The calendar scenarios build a loader directly with a `TimeCalendar` (the `calendar` argument of
the constructor), and two of them render a whole document through `VegaLiteDocument` to show that the
`VegaLiteRenderOptions.TimeZone` option reaches the data stage; the zone arithmetic is checked by hand (at a fixed
offset of +05:30 a local `10:00` is `04:30Z`; at +01:00 it is `09:00Z`; at +05:00 it is `05:00Z`).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: Per-test temporary directories (deleted by the test) and in-memory virtual files; the fixture
  `data-values.json` under `Fixtures` in the test output folder
- **Isolation**: Each test parses and loads its own specification text with its own loader (one loader is one call);
  nothing is fetched. The cancellation test supplies an already canceled token over three thousand rows, enough to
  reach a cancellation check

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any unexpected exception, wrong table shape, wrong cell kind or value, wrong limit message or location, any
request that reached a file source for remote data, or any empty table for a local file that cannot be used constitutes
a failure.

#### Unit-Level Test Scenarios

##### Inline Values Keep Their JSON Types

**Tests**: `DataLoaderTests.DataLoader_Load_InlineValuesPreservesJsonTypes`,
`DataLoaderTests.DataLoader_Load_PrimitiveValuesBecomeRowsWithTheDataField`

Asserts that three rows with every JSON scalar type (number, string, boolean, null, a negative exponent number
`-3e2`, an empty string) load into a table with the fields `b, f, n, s, z` in ordinal order, that each value has the
JSON type it was written with, and that a field a row does not have is null whatever its type elsewhere; and that the
primitives `3, "a", true, null, 1.5` become five rows of the single field `data` with the texts `3, a, true, null, 1.5`.

##### Nested Containers Are Flattened To Path Fields

**Tests**: `DataLoaderTests.DataLoader_Load_NestedObjectsAreFlattenedToPathFields`

Asserts the field list `p.q, p.r.0, p.r.1, s\.t, u.v.w` derived above, that the values are read back by `p.q`,
`p.r[1]`, and `u.v.w`, and that a literal dot in a key needs the escape: `s\.t` finds `3` while `s.t`, which is a
nested path, finds nothing.

##### Missing, Empty, And Other Sources Give Empty Tables

**Tests**: `DataLoaderTests.DataLoader_Load_MissingDataGivesAnEmptyTable`,
`DataLoaderTests.DataLoader_Load_EmptyValuesReportsVlDataEmpty`,
`DataLoaderTests.DataLoader_Load_RemoteUrlReportsVlDataRemoteUrl`,
`DataLoaderTests.DataLoader_Load_OtherSourcesAreEmptyTablesReportedByTheCatalog`

Asserts that no node, a `null` data node, and a non-object node all give the shared empty table; that an empty values
array gives a table with no rows and no fields while the document warns `vl-data-empty` at `/data` and still renders;
that an `https` URL is never read, the table is empty, the document reports `vl-data-remote-url` at `/data`, and strict
mode turns that report into a `VegaLiteUnsupportedFeatureException`; and, as a theory, that text values, object values
(GeoJSON), the `sphere` generator, a `topojson` format, a UNC URL, an `https` URL, and an absolute path each give the
shared empty table, that the catalog reports `vl-data-values-text`, `vl-data-geo`, or `vl-data-remote-url` respectively,
and that the resolver callback, which the test supplies, received no request.

##### Local Files By Url And Format

**Tests**: `DataLoaderTests.DataLoader_Load_LocalCsvUrlUsesRelativeFile`,
`DataLoaderTests.DataLoader_InferFormatType_UsesTheExtension`,
`DataLoaderTests.DataLoader_Load_JsonPropertyAndExplicitParseAreApplied`,
`DataLoaderTests.DataLoader_Load_DefaultDelimitedTextKeepsTextValues`,
`DataLoaderTests.DataLoader_Load_FileIsReadOnceWhenNamedAgain`

Asserts that a relative `url` is read from the base directory (also from a sub folder) and that a CSV with quoted commas
and CRLF line ends gives typed columns (number, boolean, and date, with the date text in UTC) when `parse` is `"auto"`;
that the extension chooses TSV, a DSV names its delimiter, and `format.type` beats a misleading extension; that the
format is guessed as `csv`, `tsv`, or `json` for a theory of paths (any case of the extension, no extension, and a
directory named like a CSV file); that a dotted and indexed `format.property` selects the rows of a JSON file (also
from the fixture `data-values.json`, whose rows are under `report.rows`), that explicit parse maps convert strings to a
number, a date, and a boolean while other fields keep their JSON types, and that JSON is not inferred by default; that
delimited text with an absent `parse`, with `parse: null`, and as a DSV keeps `01234`, `007`, ISO-looking dates, and
`true` as strings, while a parse map converts only the field it names (no inference without `"auto"`, as in Vega); and
that a file named twice in one call is requested once and counts once for a file limit of one.

##### Named Datasets And Sequences

**Tests**: `DataLoaderTests.DataLoader_Load_NamedDatasetsUseCallerPrecedence`,
`DataLoaderTests.DataLoader_Load_CallerDateTimeMaxValuesClampToSupportedDate`,
`DataLoaderTests.DataLoader_Load_SequenceUsesExclusiveStopAndAs`,
`DataLoaderTests.DataLoader_Load_SequenceRowLimitIsCheckedBeforeAllocation`,
`DataLoaderTests.DataLoader_Load_SequenceMissingStartOrStopIsLocatedFailure`,
`DataLoaderTests.DataLoader_Load_SequenceNonNumericEndpointsAreLocatedFailure`,
`DataLoaderTests.DataLoader_Load_SequenceZeroOrNonFiniteStepIsLocatedFailure`,
`DataLoaderTests.DataLoader_Load_SequenceWrongDirectionWithPositiveStepIsEmpty`,
`DataLoaderTests.DataLoader_Load_SequenceArithmeticOverflowFailsBeforeAllocation`

Asserts that a caller's dataset replaces a top-level dataset of the same name (and keeps the kinds of its values: numbers
of several types, text, booleans, null, and a `DateTime` or `DateTimeOffset` as a UTC date, with a dotted key escaped),
that a dataset only the specification defines is used, and that one given as text is the shared empty table. A caller
dataset holding `DateTime.MaxValue`, `DateTimeOffset.MaxValue`, a local-kind `DateTime.MaxValue`, and the exact last
supported millisecond gives, for each, the date with milliseconds 253402300799999 and the text
`9999-12-31T23:59:59.999Z` instead of failing, while `DateTime.MinValue` is `0001-01-01T00:00:00.000Z`; that a
sequence runs from the start up to, and excluding, the stop in the field `data` by default, with a custom step and field
name, with a negative step counting down, and empty for a step in the wrong direction, and computes each value from
its index (ten values and no drift for a step of 0.1); and that a sequence of 1e15 values is refused against the
row limit, at `/data/sequence`, before any row is allocated, while exactly the limit is accepted. The invalid-input
scenarios build the `sequence` node directly through the internal `SpecNode` constructor (the specification parser
already refuses most of these forms and cannot represent a non-finite number, so the loader is exercised on its own):
a missing `start`, `stop`, or both; a `start` or `stop` that is text, a boolean, null, an object, `NaN`, or an infinity;
and a `step` of zero or negative zero, `NaN`, an infinity, text, a boolean, null, or an object each fail with a located
failure at `/data/sequence`, while an absent step defaults to 1. A positive step with the stop below the start, a
negative step with the stop above the start, and equal endpoints give an empty table with the field. An endpoint
difference that overflows (`-MaxValue` to `MaxValue`), a quotient that overflows with a tiny step, and a finite but
astronomically long sequence fail against `MaxDataRows` at `/data/sequence` with no `Infinity` or `NaN` in the message
and without allocating, while an overflowing difference in the wrong direction is still empty.

##### Parse Failures Are Warnings

**Tests**: `DataLoaderTests.DataLoader_Load_ParseFailuresAreCollectedAsWarnings`

Asserts that two values of a CSV that an explicit `number` conversion cannot read are null and give one
`vl-data-parse-failed` warning for the field, located at `/data`, that names the count, while a field that was not named
is untouched.

##### Calendar And Implicit Dates

**Tests**: `DataLoaderCalendarTests.DataLoader_Load_PassesTheCalendarToInlineCsvJsonAndCallerRows`,
`DataLoaderCalendarTests.DataLoader_Load_DefaultCalendarIsUtcAndCallerDatesAreNotMoved`,
`DataLoaderCalendarTests.DataLoader_Load_ImplicitDateFieldsApplyOnlyWhenGiven`,
`DataLoaderCalendarTests.DataLoader_Load_SecondaryLookupDataIsNotImplicitlyParsed`,
`DataLoaderCalendarTests.DataLoader_Render_TimeZoneOptionReachesTheDataStage`,
`DataLoaderCalendarTests.DataLoader_Render_CustomDateFormatRendersInStrictMode`

Asserts that the calendar of a render reaches every kind of source of a view: with a zone at +05:30, an explicit
`parse: {d: "date"}` of the text `2024-03-05T10:00` gives `04:30Z` (10:00 minus 5 h 30 min) for inline values, a CSV
file, a JSON file, the rows of the caller for a named dataset, and a dataset defined in the specification, each of kind
date. A loader without a calendar reads in UTC (`10:00Z`), and the dates a caller supplies as a `DateTime` are not
moved by a zone (a loader at -08:00 keeps `10:00Z`), because they are instants already. The implicit date fields (the
temporal fields of the encoding) are applied to the main data of a CSV and of a JSON source when they are passed to
`Load` (at +01:00 `2024-03-05T10:00` is `09:00Z`), an explicit `parse` entry wins (`e: "string"` stays text), and the
same data loaded as a secondary source without the fields stays text. The data of a lookup is never implicitly parsed:
when the temporal field `d` exists only in the lookup source (the main data is loaded with `TemporalFields.Collect` of
the specification), the looked-up value arrives as the text `2024-03-05` that the source holds. End to end, a document
whose rows hold `2024-03-05T10:00:00Z` and `2024-03-05T10:00:00` with an explicit date parse has the two rows at the
same position in UTC (both are 10:00Z), while with `TimeZone` at +05:00 the row without an offset is 05:00Z, five
hours earlier, and so is to the left of the other. A specification with `date:'%d/%m/%Y'` renders in strict mode, since
a custom date format is no longer an unsupported feature, and the 5th of March (`05/03/2024`) lies to the left of the
15th. The calendar that the expression context and the evaluator carry is verified in the expression units (see
_Expression Functions Verification_ and _Expression Evaluator Verification_).

##### Hard Failures

**Tests**: `DataLoaderTests.DataLoader_Load_MissingLocalFileIsHardFailure`,
`DataLoaderTests.DataLoader_Load_UnusableJsonFileIsALocatedFailure`,
`DataLoaderTests.DataLoader_Load_JsonLoneSurrogateStringIsLocatedUnicodeFailure`,
`DataLoaderTests.DataLoader_Load_JsonLoneSurrogatePropertyNameIsLocatedUnicodeFailure`,
`DataLoaderTests.DataLoader_Load_JsonLoneSurrogatePropertyLookupIsLocatedUnicodeFailure`,
`DataLoaderTests.DataLoader_Load_MissingNamedDataIsHardFailure`

Asserts that a local file that cannot be found is a "was not found" failure at `/data/url`, that a document without any
file source fails the same way and no request was made for it, that malformed JSON, a document that is not an array, a
missing or malformed `format.property`, a number outside the range of a double, and nesting deeper than the limit are
located failures at the `url` (as a theory), and that a named dataset that nobody defines fails with the dataset's name
and where to define it, located at `/data/name`, directly and through `Render` in lenient and strict mode alike.

Three scenarios read a virtual JSON file whose text holds an escaped lone surrogate (`\ud800`, `\udc00`), which the JSON
parser accepts and only a later element access rejects: in a string value, in a property name of a row, and in a
property name that the `format.property` lookup must compare. Each asserts a `VegaLiteRenderException` (a raw
`InvalidOperationException` would fail the exact-type check, because the render exception derives from it) whose message
says that `'d.json'` contains text that is not valid Unicode, located at `/data/url` in the specification file.

##### Limits And Cancellation

**Tests**: `DataLoaderTests.DataLoader_Load_RowLimitFailsWithLocation`,
`DataLoaderTests.DataLoader_Load_FieldLimitFailsWithLocationOfTheLeaf`,
`DataLoaderTests.DataLoader_Load_RowLimitIsATotalAcrossTables`,
`DataLoaderTests.DataLoader_Load_CallerTextLongerThanMaxStringLength_IsLocatedRenderError`,
`DataLoaderTests.DataLoader_Load_DelimitedLimitsAndFailuresCarryTheLocationOfTheUrl`,
`DataLoaderTests.DataLoader_Load_CanceledTokenStopsLoading`

Asserts that more rows than `MaxDataRows` fail before any row is converted with a `VegaLiteRenderException` whose
pointer is `/data/values`, whose file and line are those of the values (`rows.vl.json`, line 2), and whose message names
the limit and starts with the location, while exactly the limit is accepted; that more fields than `MaxFieldCount` fail
at the pointer of the leaf that exceeded the limit, and that the limit also applies to the union of the fields of
different rows; that the row limit is a total across the tables of one loader (a second table that would pass the total
is refused with the rows read before named, and `RowsLoaded` reports what was read); that a CSV over the row limit, and
one with a repeated column, fail at the `url` (`/data/url`, line 1); that a text of a dataset that the caller supplied
is bounded by `MaxStringLength` like a text of a file (8 characters pass a limit of 8 and 9 fail with a located failure
that names the limit); and that a canceled token throws `OperationCanceledException`.

##### Large Tables Load Completely

**Tests**: `DataLoaderTests.DataLoader_Load_LargeInlineTableLoadsAllRows`

Asserts that an inline table of 20,000 rows loads completely: the row count, the last row's string value `c19999`,
and its number `19999` are all as written.

##### Derived Rows Share The Row Budget Of The Call

**Tests**: `DataLoaderTests.DataLoader_ReserveDerivedRows_SharesTheRowBudgetOfTheCall`

Asserts, with three loaded rows and a `MaxDataRows` of ten, that reserving five derived rows for "fold transform
output" is accepted and counted (eight rows in all), that reserving three more fails with a `VegaLiteRenderException`
at the location passed in, whose message says "The fold transform output has 3 rows; with the 8 rows read before" and
names "the limit of 10 (MaxDataRows)", and that the failed reservation counted nothing; that releasing five rows gives
three, a negative release is ignored, a release of more rows than are counted stops at zero, and the data can be loaded
again afterwards; and that a count that cannot be an exact row count (positive infinity) is described as "an
unrepresentable number of" rows and refused.

##### Lookup Sources Share The Sandbox, The Cache, And The Budget

**Tests**: `DataLoaderTests.DataLoader_Load_LookupSourcesShareTheSandboxCacheAndBudgetOfTheCall`

Asserts, with a file limit of one and a row limit of four, that the main data and the source of a lookup that name the
same two-row file read it once (one request in the virtual file set), that four rows are counted in total, that a
lookup source outside the sandbox (`../secret.csv`) is a `VegaLiteRenderException` ("outside the allowed directories")
located at `/transform/1/from/data/url`, that a remote lookup source (`https`) is an empty table that no request reached
and that the feature catalog reports as `vl-data-remote-url` at `/transform/2/from/data`, and that a third table over the
row limit is refused naming `MaxDataRows`.
