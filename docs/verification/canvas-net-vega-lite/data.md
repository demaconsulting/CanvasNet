## Data Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Data` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Data`), containing two units: `DataTable` (with the scalar value, field path, and
JavaScript number helpers of its file) and `DataLoader`.

### Verification Approach

The `Data` subsystem is verified at two tiers. The unit tests (see _DataTable Unit Verification Design_ and
_DataLoader Unit Verification Design_ under `data/`) verify each unit in isolation, including its internal types. The
subsystem-level scenarios verify the two units working together on specifications parsed by the real `SpecParser`:
the loader reads the inline `values` of a parsed `data` node, flattens nested containers into leaf fields named by
canonical paths, builds the immutable table through `DataTable.Create`, enforces the row and field limits with the
location of the data node, and the resulting table is queried by Vega-Lite field paths. The subsystem's unit tests
already use this combination (the loader tests build their input with `SpecParser.Parse` and read the result through
the table's accessors), so the subsystem-level scenarios name the unit tests that cross the boundary.

No mocking or stubbing is used: the subsystem depends only on the parser's tree types, `VegaLiteLimits`,
`VegaLiteLocation`, and `VegaLiteRenderException`. The subsystem performs no file or network input in this release (only
inline values are read; every other data source is an empty table that the feature catalog reports), which the
system-level scenario "Data: Remote URLs Are Reported And Never Fetched" confirms. The subsystem is internal, so its
tests reach it through the test assembly's `InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None; every specification is an in-memory string
- **Isolation**: Each test parses and loads its own data; the culture-independence scenario switches the current
  culture on the test thread and restores it afterwards

### Acceptance Criteria

The `Data` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the two unit verification documents passes without error or unexpected exception.

### Test Scenarios

- **Inline Values Load Into A Typed Table That Is Queried By Field Path**:
  `DataLoaderTests.DataLoader_Load_InlineValuesPreservesJsonTypes`,
  `DataLoaderTests.DataLoader_Load_NestedObjectsAreFlattenedToPathFields` — loads parsed inline rows and reads them
  back by name and by Vega-Lite path (`p.q`, `p.r[1]`, `u.v.w`), asserting that each cell has the JSON type it was
  written with, that fields are in ordinal order, and that a literal dot in a key (`s\.t`) stays distinct from a
  nested path.
- **Row And Field Limits Fail Closed With The Location Of The Data**:
  `DataLoaderTests.DataLoader_Load_RowLimitFailsWithLocation`,
  `DataLoaderTests.DataLoader_Load_FieldLimitFailsWithLocationOfTheLeaf`,
  `DataTableTests.DataTable_Create_LimitsFailClosedWithLocation` — a document over the row limit fails before any row
  is converted, a row with too many fields fails at the leaf that exceeded the limit, and both carry the pointer and
  position of the offending node.
- **Other Sources Are Empty, Never Opened**:
  `DataLoaderTests.DataLoader_Load_RemoteUrlReportsVlDataRemoteUrl`,
  `DataLoaderTests.DataLoader_Load_OtherSourcesAreEmptyTablesReportedByTheCatalog` — a URL, a name, a sequence, and
  text values give an empty table with the document-level report (`vl-data-remote-url`, `vl-data-file`,
  `vl-data-named`, `vl-data-sequence`, `vl-data-values-text`) and nothing is read.
- **Large Tables Load Completely**:
  `DataLoaderTests.DataLoader_Load_LargeInlineTableLoadsAllRows` — a large inline table is loaded completely and
  quickly, and loading honors cancellation (`DataLoaderTests.DataLoader_Load_CanceledTokenStopsLoading`).
- **Culture Independence**:
  `DataTableTests.JsNumber_ToString_MatchesJavaScriptNumberFormatting` — numbers are converted to the JavaScript
  `String(number)` text under a culture with a decimal comma and a different minus sign, which is the text that
  category labels and field comparisons depend on.
