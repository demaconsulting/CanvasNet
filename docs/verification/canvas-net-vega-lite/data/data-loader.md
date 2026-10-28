### DataLoader Unit Verification Design

This document describes the unit-level verification strategy for the `DataLoader` unit in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace: the internal static `DataLoader` class (`Load` and the
`PrimitiveField` constant). The loader turns the `data` node of a parsed specification into a `DataTable`: only inline
`values` arrays are read; every other source (a URL, a name, a sequence, text values, no data at all) is an empty
table that is never opened or fetched, and that the feature catalog reports.

#### Verification Approach

The unit is verified by parsing small, original specification strings with the real `SpecParser`, loading the `data`
node, and asserting on the resulting table: the row count, the fields in ordinal order, and each cell's kind and
value. The unit's real collaborators (`SpecParser` for the input, `DataTable` for the output, `FeatureCatalog` and
`VegaLiteDocument` for the reports that the loader's design leaves to them) are used rather than mocked, because the
unit's contract is exactly how a parsed node becomes a table. No mocking or stubbing is used; the internal types are
reached through the test assembly's `InternalsVisibleTo` access.

Expected tables are derived by hand from the rules of the unit: an object row becomes one row; a primitive or array
row becomes one row with the single field `data`; nested objects and arrays become one field per leaf named by the
canonical path (so `{"p":{"q":1,"r":[10,20]},"s.t":3,"u":{"v":{"w":"deep"}},"e":{},"a":[]}` has exactly the fields
`p.q`, `p.r.0`, `p.r.1`, `s\.t`, and `u.v.w`, and the empty containers add none); and a field that a row lacks is
null. The limit scenarios count by eye: three rows against a `MaxDataRows` of two fail, and exactly three pass; three
fields against a `MaxFieldCount` of two fail at the pointer of the third leaf, `/data/values/0/c`.

Unit tests reside in `DataLoaderTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Data`
folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test parses and loads its own specification text; no files are read and nothing is fetched. The
  cancellation test supplies an already canceled token over three thousand rows, enough to reach a cancellation check

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any unexpected exception, wrong table shape, wrong cell kind or value, wrong limit message or location, or
any attempt to read a URL constitutes a failure.

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
mode turns that report into a `VegaLiteUnsupportedFeatureException`; and, as a theory, that a relative URL, a name, a
sequence, text values, and a UNC URL each give the shared empty table and the catalog reports `vl-data-file`,
`vl-data-named`, `vl-data-sequence`, `vl-data-values-text`, and `vl-data-remote-url` respectively.

##### Limits And Cancellation

**Tests**: `DataLoaderTests.DataLoader_Load_RowLimitFailsWithLocation`,
`DataLoaderTests.DataLoader_Load_FieldLimitFailsWithLocationOfTheLeaf`,
`DataLoaderTests.DataLoader_Load_CanceledTokenStopsLoading`

Asserts that more rows than `MaxDataRows` fail before any row is converted with a `VegaLiteRenderException` whose
pointer is `/data/values`, whose file and line are those of the values (`rows.vl.json`, line 2), and whose message
names the limit and starts with the location, while exactly the limit is accepted; that more fields than
`MaxFieldCount` fail at the pointer of the leaf that exceeded the limit, and that the limit also applies to the union
of the fields of different rows; and that a canceled token throws `OperationCanceledException`.

##### Large Tables Load Completely

**Tests**: `DataLoaderTests.DataLoader_Load_LargeInlineTableLoadsAllRows`

Asserts that an inline table of 20,000 rows loads completely: the row count, the last row's string value `c19999`,
and its number `19999` are all as written.
