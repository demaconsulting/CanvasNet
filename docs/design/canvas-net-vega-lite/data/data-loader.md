### DataLoader

![Data Structure](VegaLiteDataView.svg)

The `DataLoader` unit is the second software unit in the `Data` subsystem. It is one source file, `DataLoader.cs`, in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace, and defines one `internal` type, the static class `DataLoader`.

#### Purpose

`DataLoader` reads the data of a view from the `data` property of its specification: the inline `values` rows, under the
data limits. It exists so that the rest of the pipeline sees every kind of data source as a `DataTable` and so that no
other unit needs to know how the rows were written. It has no side effects and never touches the file system or the
network.

The unit's behavior is specified by the `CanvasNetVegaLite-Data-DataLoader-*` requirements in
`docs/reqstream/canvas-net-vega-lite/data/data-loader.yaml`.

#### Data Model

The unit has no state. It defines the constant `PrimitiveField` (`"data"`, the field that holds the value of a row when
the inline values are primitives) and the cancellation interval (1,024 rows). Its input is the `data` node of a view
(`SpecNode?`), the source name for locations, the limits, and a cancellation token; its output is a `DataTable`.

**Row rules.** An array of objects gives one row per object. An array of primitives (or of arrays) gives one row per
value with the value in the field `data`. The JSON types are kept (number, string, boolean, null). A nested object or
array is flattened to one field per leaf, named by the canonical path of the leaf (`FieldPath.Join`, which escapes a
literal dot), and a field a row does not have is null. Every other source (`values` given as text, any `url`, a `name`,
a `sequence`, no source, or no `data` property at all) is the empty table.

#### Key Methods

- `Load(SpecNode? data, string? sourceName, VegaLiteLimits? limits, CancellationToken token)` - returns
  `DataTable.Empty` unless `data` is an object with an array `values`; checks the row count against `MaxDataRows`
  before any row is converted; flattens each row into a record while checking the field count of the row against
  `MaxFieldCount` and the token every 1,024 rows; and builds the table with `DataTable.FromRecords` (which also checks
  the field count of the union).
- `Flatten` and `FlattenProperties` - add the leaves of a value to a record, recursing over objects and arrays; the
  recursion depth is bounded by the nesting depth that `SpecParser` accepted (at most 256 levels).
- `Add` - stores one leaf and enforces the per-row field limit at the leaf's location.

**Preconditions and postconditions**: the `data` node, when present, was validated by `SpecParser`; on return the table
is complete and within the limits. Root-level data inheritance for layered views is the caller's job (it passes the
right `data` node); the first delivery loads only the root view's data.

#### Error Handling

More rows than `MaxDataRows` are a `VegaLiteRenderException` located at the `values` array; more fields in a row than
`MaxFieldCount` are a `VegaLiteRenderException` located at the leaf that crossed the limit; the union of the fields is
checked again by `DataTable.FromRecords`. A canceled token is an `OperationCanceledException`, never wrapped. Sources
that cannot be loaded are not errors: the table is empty and the `FeatureCatalog` reports the reason (`vl-data-empty`,
`vl-data-remote-url`, `vl-data-file`, `vl-data-named`, `vl-data-sequence`, `vl-data-values-text`), so the rest of the
chart is still drawn.

#### Dependencies

- **`DataTable`**, **`DataValue`**, **`FieldPath`** - the value and table model of the `DataTable` unit
- **`SpecNode`** and **`SpecNodeKind`** - the tree of the `SpecParser` unit
- **`VegaLiteLimits`** and **`VegaLiteRenderException`** - the public types of the `VegaLiteDocument` unit
- **.NET base class library** - `System.Globalization` (invariant-culture array indices in field names)

#### Callers

`VegaLiteDocument` calls `Load` for a single view with a supported mark. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataLoaderTests.cs` call it directly through `InternalsVisibleTo`.
