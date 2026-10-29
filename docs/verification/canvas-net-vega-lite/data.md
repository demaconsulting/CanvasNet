## Data Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Data` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Data`), containing four units: `DataTable` (with the scalar value, field path, and
JavaScript number helpers of its file), `DataLoader`, `DataFileResolver`, and `DelimitedTextParser`.

### Verification Approach

The `Data` subsystem is verified at two tiers. The unit tests (see _DataTable Unit Verification Design_, _DataLoader
Unit Verification Design_, _DataFileResolver Unit Verification Design_, and _DelimitedTextParser Unit Verification
Design_ under `data/`) verify each unit in isolation, including its internal types. The subsystem-level scenarios
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
  where the host cannot create them

### Acceptance Criteria

The `Data` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the four unit verification documents passes without error or unexpected exception.

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
  Quantitative color is not drawn by the current subset and is not claimed.
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
  `DelimitedTextParserTests.DelimitedTextParser_Parse_AutoOutOfRangeIsoDateStaysText`,
  `DelimitedTextParserTests.DelimitedTextParser_Parse_ExplicitOutOfRangeIsoDateBecomesNullAndWarning`,
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_ParseFailuresAreDeliveredAsWarnings` —
  values that an explicit conversion cannot read are null and give one warning per field, delivered after the reports of
  the specification; ISO text beyond the supported date range stays text under inference and is null with the same
  warning under an explicit date conversion.
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
