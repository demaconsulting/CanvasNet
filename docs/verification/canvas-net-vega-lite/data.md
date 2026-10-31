## Data Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Data` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Data`), containing six units: `DataTable` (with the scalar value, field path, and
JavaScript number helpers of its file), `DataLoader`, `DataFileResolver`, `DelimitedTextParser`, `TimeCalendar` (the
calendar, the zone rules, the d3-time intervals, the Vega-Lite time units, and the datetime objects), and `TimeFormat`
(the d3-time-format formatter and parser, Vega's multi-format, and the `Date.parse` rules).

### Verification Approach

The `Data` subsystem is verified at two tiers. The unit tests (see _DataTable Unit Verification Design_, _DataLoader
Unit Verification Design_, _DataFileResolver Unit Verification Design_, _DelimitedTextParser Unit Verification
Design_, _TimeCalendar Unit Verification Design_, and _TimeFormat Unit Verification Design_ under `data/`) verify each
unit in isolation, including its internal types. The subsystem-level scenarios
verify the units working together on specifications parsed by the real `SpecParser`: the loader reads the inline
`values`, a local file, a named dataset, or a sequence of a parsed `data` node; for a file it asks the real resolver
(over a real temporary folder or an in-memory callback) for decoded text and hands delimited text to the real parser;
flattens nested containers into leaf fields named by canonical paths; applies the `parse` conversions; builds the
immutable table through `DataTable.Create`; enforces the row, field, file, and byte limits with the location of the data
node; and the resulting table is queried by Vega-Lite field paths. The same loader instance also loads the sources of
the lookup transforms through its file cache, sandbox, and row budget and reserves the rows of the tables that the
transforms derive (see the _Transforms Subsystem Verification Design_), and the table's cells can hold the composite
values of the expression engine (see the _Expressions Subsystem Verification Design_). The subsystem's unit tests
already use this combination (the loader tests build their input with `SpecParser.Parse`, read files through the real
resolver, and read the result through the table's accessors), so the subsystem-level scenarios name the unit tests that
cross the boundary and the system-level scenarios that exercise the subsystem through the public API.

The calendar flow is verified end to end as one chain. `VegaLiteDocument.Render` creates the calendar with
`TimeCalendar.Create` from the `TimeZone` of the render options (null is UTC, and a zone with a fixed offset of zero is
UTC too) and gives it to the `DataLoader` and to the `ExpressionEvaluator`; the loader reads the dates of every source
(inline values, a file, a sequence, and the rows of the caller) with that calendar, applying the `parse` directives of
the `data` node (the explicit `date` and `utc` formats, read by `TimeFormat.TryParse`, and `parse: "auto"`, whose date
test is `JsDateParser`) and the implicit date parse of the fields that `TemporalFields.Collect` found in the encoding,
the transforms, and the predicates (an explicit `parse` entry and `parse: null` win). A date-time without an offset is
therefore a wall time of the zone of the chart, a date alone is UTC midnight, an offset or `Z` is honored, the dates
that the caller supplies are never moved, and the data of a lookup, which has no temporal fields of its own, stays
text. The same calendar then reaches the transforms, the scales, the guides, and the marks (see the _Transforms_ and
_Guides Subsystem Verification Designs_), so the system-level scenarios of the time chart draw it in a zone and compare
its pixels with hand-derived columns. Every zone of these tests is a custom zone that the test creates, and no test
reads the host's zone, clock, or culture.

No mocking or stubbing is used: the subsystem depends only on the parser's tree types, `VegaLiteLimits`,
`VegaLiteOpenOptions`, `VegaLiteLocation`, and `VegaLiteRenderException`, and its file sources are real implementations
of the resolver's seams (a temporary directory and an in-memory callback that records its requests). The subsystem
performs file input only through the resolver's sandbox and never network input: remote data is an empty table that the
feature catalog reports and that never reaches a file source, which the system-level scenario "Data: Remote URLs Are
Reported And Never Fetched" confirms. The subsystem is internal, so its tests reach it through the test assembly's
`InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: Per-test temporary directories (created and deleted by the test), in-memory virtual files, and the
  original fixtures under `Fixtures` in the test output folder; no network
- **Isolation**: Each test parses and loads its own data with its own loader; the culture-independence scenario
  switches the current culture on the test thread and restores it afterwards. Tests that create real links skip only
  where the host cannot create them. Every time zone is a custom zone that the test creates with
  `TimeZoneInfo.CreateCustomTimeZone`; no test reads `TimeZoneInfo.Local`, the clock, or the host's zone database, and
  the time scenarios repeat under `tr-TR` and `de-DE`

### Acceptance Criteria

The `Data` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the six unit verification documents passes without error or unexpected exception.

### Test Scenarios

- **Inline Values Load Into A Typed Table That Is Queried By Field Path**:
  `DataLoaderTests.DataLoader_Load_InlineValuesPreservesJsonTypes`,
  `DataLoaderTests.DataLoader_Load_NestedObjectsAreFlattenedToPathFields` — loads parsed inline rows and reads them
  back by name and by Vega-Lite path (`p.q`, `p.r[1]`, `u.v.w`), asserting that each cell has the JSON type it was
  written with, that fields are in ordinal order, and that a literal dot in a key (`s\.t`) stays distinct from a
  nested path.
- **A Local File Loads Into The Same Table As Inline Rows**:
  `DataLoaderTests.DataLoader_Load_LocalCsvUrlUsesRelativeFile`,
  `DataLoaderTests.DataLoader_Load_JsonPropertyAndExplicitParseAreApplied`,
  `DataLoaderTests.DataLoader_Load_FileIsReadOnceWhenNamedAgain`,
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_LocalFileLoadsFromOpenedSpecFolder` — a CSV
  (with quoted commas and CRLF line ends), a TSV, a DSV, and a JSON file with a nested property are read through the real
  resolver and parser, each with its format chosen by `format.type` or the extension, with typed columns by explicit
  `parse: "auto"` inference and by explicit `parse` maps, a file named twice is read once, and a chart drawn from the file
  has the pixels of the same rows written inline (also when the fixture's line ends are LF or CRLF, so no test depends on
  how the checkout normalized the file).
- **Delimited Text Is Not Inferred Unless Asked, And Quantitative Channels Still Coerce**:
  `DataLoaderTests.DataLoader_Load_DefaultDelimitedTextKeepsTextValues`,
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_DefaultDelimitedTextPreservesTextAndQuantitativeRenderCoerces`
  — a CSV without `parse` keeps `01234`, `007`, ISO-looking dates, and `true` as strings (as Vega does), a parse map
  converts only its named field, and a chart whose x, y, and size are quantitative fields of unparsed CSV text has the
  pixels of the same rows with real numbers, with no data warning, so the numeric text is coerced where it is drawn.
  The color of this scenario is a nominal column, so it claims nothing about a quantitative color.
- **The Sandbox Refuses What It Must And Fails Closed**:
  `DataLoaderTests.DataLoader_Load_MissingLocalFileIsHardFailure`,
  `DataFileResolverLinkTests.DataFileResolver_Load_LinkedDirectoryIsRejectedBeforeOpen`,
  `DataFileResolverTests.DataFileResolver_Load_ExistingDirectoryAtFilePathIsLocatedReadFailure`,
  `DataFileResolverTests.DataFileResolver_Load_ExistingFileOpenFailureIsLocatedReadFailure`,
  `DataFileResolverTests.DataFileResolver_Load_FileNotFoundFromOpenIsMissingAndTriesNextRoot`,
  `DataFileResolverTests.DataFileResolver_Load_CallbackFailureIsLocatedReadFailure`,
  `DataLoaderTests.DataLoader_Load_JsonLoneSurrogateStringIsLocatedUnicodeFailure`,
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_MissingOrBlockedFileThrowsRenderException` —
  a missing file, a path outside the sandbox, and bytes that are not UTF-8 each fail with a located
  `VegaLiteRenderException`, in lenient and strict mode alike, a file behind a link is refused before it is opened
  (with the injected link probe), a directory in place of a file and a throwing resolver callback are located read
  failures (never raw exceptions), an existing file whose open fails with an access or I/O error (raised by the
  injected opener on every platform) is "cannot be read" and not "not found" while a "not found" from the open is a
  missing candidate that lets the next root be tried, JSON text with an escaped lone surrogate is a located
  invalid-Unicode failure, and an absolute path is remote data that is never opened.
- **Named Datasets And Sequences**:
  `DataLoaderTests.DataLoader_Load_NamedDatasetsUseCallerPrecedence`,
  `DataLoaderTests.DataLoader_Load_CallerDateTimeMaxValuesClampToSupportedDate`,
  `DataLoaderTests.DataLoader_Load_SequenceUsesExclusiveStopAndAs`,
  `DataLoaderTests.DataLoader_Load_SequenceMissingStartOrStopIsLocatedFailure`,
  `DataLoaderTests.DataLoader_Load_SequenceZeroOrNonFiniteStepIsLocatedFailure`,
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_SequenceAndNamedDatasetsRender` — the
  caller's dataset beats the specification's, the largest `DateTime` and `DateTimeOffset` values (and the last supported
  millisecond itself) are the date `9999-12-31T23:59:59.999Z` instead of failing, a sequence excludes its stop, and both
  draw the pixels of the same rows
  written inline without any report, while a sequence with a missing, non-finite, or zero-step input fails with a
  located error.
- **Parse Conversions And Failures**:
  `DataLoaderTests.DataLoader_Load_ParseFailuresAreCollectedAsWarnings`,
  `JsDateParserTests.JsDateParser_Iso_ExpandedYears`,
  `JsDateParserTests.JsDateParser_Iso_RangeBoundaries`,
  `ParseDirectivesDateTests.ParseDirectives_Auto_InfersLegacyDatesAndIso`,
  `ParseDirectivesDateTests.ParseDirectives_Date_NumberIsMillisecondsAndBooleanFails`,
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_ParseFailuresAreDeliveredAsWarnings` —
  values that an explicit conversion cannot read are null and give one warning per field, delivered after the reports of
  the specification; ISO text beyond the years 1 to 9999 (the range of a `DataValue` date, plan decision P8-D2) is not a
  date for the parser, so a conversion that needs a date gives null with the same warning, a number is a date in
  milliseconds and a boolean is not one, and inference reads both the ISO and the legacy forms.
- **Row, Field, File, And Byte Limits Fail Closed With The Location Of The Data**:
  `DataLoaderTests.DataLoader_Load_RowLimitFailsWithLocation`,
  `DataLoaderTests.DataLoader_Load_FieldLimitFailsWithLocationOfTheLeaf`,
  `DataLoaderTests.DataLoader_Load_RowLimitIsATotalAcrossTables`,
  `DataTableTests.DataTable_Create_LimitsFailClosedWithLocation`,
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_LimitsFailBeforeSceneBudget` — a document
  over the row limit fails before any row is converted, a row with too many fields fails at the leaf that exceeded the
  limit, the row limit is a total across the tables of a call, a file over the row or byte limit fails with that
  limit named and located at the url even when the scene limit would also fail, and all carry the pointer and position of
  the offending node.
- **Other Sources Are Empty, Never Opened**:
  `DataLoaderTests.DataLoader_Load_RemoteUrlReportsVlDataRemoteUrl`,
  `DataLoaderTests.DataLoader_Load_OtherSourcesAreEmptyTablesReportedByTheCatalog` — remote data (a scheme, an
  absolute path, a UNC path), text values, object values, and geographic sources give an empty table with the
  document-level report (`vl-data-remote-url`, `vl-data-values-text`, `vl-data-geo`), and nothing, not even a resolver
  callback, is asked for it.
- **Large Tables Load Completely**:
  `DataLoaderTests.DataLoader_Load_LargeInlineTableLoadsAllRows` — a large inline table is loaded completely and
  quickly, and loading honors cancellation (`DataLoaderTests.DataLoader_Load_CanceledTokenStopsLoading`).
- **Culture Independence**:
  `DataTableTests.JsNumber_ToString_MatchesJavaScriptNumberFormatting` — numbers are converted to the JavaScript
  `String(number)` text under a culture with a decimal comma and a different minus sign, which is the text that
  category labels and field comparisons depend on.
- **Composite Values Are Immutable Cells With Identity Equality**:
  `DataCompositeValueTests.DataValue_FromArray_CopiesElementsAndFormatsLikeJavaScript`,
  `DataCompositeValueTests.DataValue_Composites_AreEqualOnlyToThemselves`,
  `DataCompositeValueTests.DataObject_Create_KeepsInsertionOrderAndLastValueOfARepeatedKey`,
  `DataCompositeValueTests.DataValue_FromRegExp_CarriesOpaquePayload`,
  `DataCompositeValueTests.DataValue_Compare_ScalarOrderIsUnchangedAndCompositesSortLast`,
  `DataTableTests.DataTable_FromSortedCells_BuildsATableAndChecksTheShape` — the array, object, and regular-expression
  values that expressions and transforms produce copy their input, keep their order (a repeated key keeps its first
  position and takes its last value), print as JavaScript does, are equal only to themselves (also when empty), and sort
  after every scalar while the scalar order is unchanged; a table built from sorted cells holds a composite cell, a
  null cell, and a table with rows but no fields, and refuses unordered, repeated, or miscounted fields.
- **Derived Tables Share The Row Budget And The Sandbox Of The Call**:
  `DataLoaderTests.DataLoader_ReserveDerivedRows_SharesTheRowBudgetOfTheCall`,
  `DataLoaderTests.DataLoader_Load_LookupSourcesShareTheSandboxCacheAndBudgetOfTheCall`,
  `RowTransformsTests.RowTransforms_Apply_LookupSourceSharesTheRowBudget`,
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Transforms_LimitsSandboxAndCancellationFailClosed`
  — the rows of a table that a transform derives are reserved against the same `MaxDataRows` total as the loaded rows
  before they are allocated (five derived rows fit beside three loaded ones under a limit of ten, three more are
  refused with the count of the rows read before and nothing is counted for the failure, and released rows are free
  again), a second data source of the same call (the source of a lookup) reads a file once through the same cache and
  counts it once for `MaxDataFiles`, is refused outside the sandbox at its own `url`, shares the row budget, and stays an
  empty table that is never asked for when it is remote, and a fold over the limit fails at its transform.
- **The Calendar Reaches Every Source And The Expression Context**:
  `DataLoaderCalendarTests.DataLoader_Load_PassesTheCalendarToInlineCsvJsonAndCallerRows`,
  `DataLoaderCalendarTests.DataLoader_Load_DefaultCalendarIsUtcAndCallerDatesAreNotMoved`,
  `DataLoaderCalendarTests.DataLoader_Render_TimeZoneOptionReachesTheDataStage`,
  `DataLoaderCalendarTests.ExpressionFunctionContext_Calendar_DefaultsToUtc`,
  `DataLoaderCalendarTests.ExpressionEvaluator_Constructor_ForwardsTheCalendar`,
  `TimeCalendarTests.TimeCalendar_Create_NullAndZeroOffsetZones_GiveUtc` — a date text without an offset that an
  explicit parse reads from inline values, a CSV file, a JSON file, and the rows of the caller is the wall time of the
  zone of the loader, a loader without a calendar reads in UTC and does not move the dates that a caller supplies, with
  a zone of plus five hours a render places a date-time without an offset five hours away from the same date-time with
  an offset, the expression context and the evaluator carry the calendar and are UTC when none is given, and a null
  zone, the UTC zone, and a zone at +00:00 are the same UTC calendar.
- **Implicit Date Parsing Applies To Temporal Fields Only**:
  `DataLoaderCalendarTests.DataLoader_Load_ImplicitDateFieldsApplyOnlyWhenGiven`,
  `DataLoaderCalendarTests.DataLoader_Load_SecondaryLookupDataIsNotImplicitlyParsed`,
  `ParseDirectivesDateTests.ParseDirectives_Implicit_AppliesToTemporalFieldsOnly`,
  `ParseDirectivesDateTests.ParseDirectives_Implicit_ExplicitParseWins`,
  `ParseDirectivesDateTests.ParseDirectives_Implicit_ParseNullSuppresses`,
  `ParseDirectivesDateTests.ParseDirectives_Implicit_UnparsableTextIsNullWithTheParseWarning`,
  `TemporalFieldsTests.TemporalFields_Collect_TypeTimeUnitAndFormatType`,
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_ImplicitParseOfTemporalStrings` — the fields
  that the encoding reads as dates (by type, time unit, or format type) are parsed with the rules of `Date.parse`
  without a `parse` directive, so `March 5, 2024` is a date, a field outside that set stays text, an explicit entry or
  `parse: null` wins, a text that is not a date is null with the one parse warning of the field, and the data of a
  lookup loaded through the same loader stays text.
- **Custom Date Formats And Date Inference Read Text By The d3 And ECMAScript Rules**:
  `ParseDirectivesDateTests.ParseDirectives_CustomDate_ParsesDayMonthYear`,
  `ParseDirectivesDateTests.ParseDirectives_CustomDate_Utc_IgnoresTheZone`,
  `ParseDirectivesDateTests.ParseDirectives_CustomDate_FailureIsNullWithOneWarningPerField`,
  `ParseDirectivesDateTests.ParseDirectives_Auto_InfersLegacyDatesAndIso`,
  `ParseDirectivesDateTests.ParseDirectives_Auto_UsesTheCalendarZone`,
  `DataLoaderCalendarTests.DataLoader_Render_CustomDateFormatRendersInStrictMode`,
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_CustomDateParseDirectiveReadsDayMonthYear` —
  the directive `date:'%d/%m/%Y'` reads 05/03/2024 as March 5, `utc:` ignores the zone, a failure is null with one warning
  per field, `parse: "auto"` infers the ISO and the legacy forms in the zone, and a CSV whose days are 5, 15, and 25
  March 2024 draws its points 10 days apart (0, 100, and 200 pixels on a 200-pixel axis of 20 days) and is not reported
  in strict mode, while without the directive `15/03/2024` is the fifteenth month, so those rows do not draw and the
  load is reported.
- **A Time Zone Moves Dates, Unit Boundaries, And Days End To End**:
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_PointsSitAtTheirDatesOnAMultiFormatAxis`,
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_TimeZoneOptionShiftsTimeUnitBoundaries`,
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_DstDayIsNot24Hours`,
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_UtcScaleIgnoresTheZoneOption`,
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_HostZoneDoesNotMatter` — three points sit at
  their dates on an axis with a tick every two days (60 pixels apart over 300 pixels), a point at 2024-03-05T20:00Z is on
  March 6 (local 01:30) in a zone 5:30 hours ahead and on March 5 in UTC, one day of the scale (40 pixels) apart, with
  the same ticks and labels, a zone that follows the United States rule makes March 10, 2024 a day of 23 hours, so with
  the days March 9, 10, and 11 across 470 pixels (47 hours, 10 pixels an hour) the middle day starts 240 pixels in and
  not 235 as in UTC, a `utc` scale ignores the zone, and a chart without a zone and one in a zone with a zero offset are
  byte-identical.
- **Dates Do Not Depend On The Culture, The Host, Or The Run**:
  `TimeFormatTests.TimeFormat_Format_IgnoresTheCurrentCulture`,
  `TimeFormatParseTests.TimeFormatParse_IgnoresTheCurrentCulture`,
  `JsDateParserTests.JsDateParser_IgnoresTheCurrentCulture`,
  `ParseDirectivesDateTests.ParseDirectives_Date_IgnoresTheCurrentCulture`,
  `TimeCalendarZoneTests.TimeCalendarZone_Results_DoNotDependOnTheHostZone`,
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_CurrentCultureDoesNotChangeTheOutput`,
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_Determinism_RepeatedAndConcurrentRendersAreByteIdentical`
  — the names, the digits, and the parse of dates are the English ASCII ones under `tr-TR` (with its dotted capital I)
  and `de-DE`, the zone calendar never reads the host's zone, and charts that format dates in their axes and legends
  render the same pixels under the other cultures and from concurrent renders.
- **Date Parsing Is Bounded And Cancelable**:
  `ParseDirectivesDateTests.ParseDirectives_Date_HugeCellIsNotADateAndCostsNothing`,
  `ParseDirectivesDateTests.ParseDirectives_Apply_PollsCancellationEveryBatch`,
  `ParseDirectivesDateTests.ParseDirectives_Apply_StopsInsideAColumnWhenCanceledMidway`,
  `JsDateParserTests.JsDateParser_Rejects_OverLongText`,
  `TimeFormatParseTests.TimeFormatParse_LongInputFailsFast` — a cell of a million characters is not a date and costs one
  length test (the date parser examines 128 characters and the format parser 256), and a canceled token stops the parse
  of the columns inside a column and between batches.
