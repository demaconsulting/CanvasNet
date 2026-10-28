## Data

![Data Structure](VegaLiteDataView.svg)

### Overview

The `Data` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Data`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Data/`) puts the rows of a chart into memory, safely. It is **internal**: its
types are visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the test assembly.
In the first delivery it reads **inline `values` only**; local files, named datasets, and generated sequences are
later phases and are reported by the `FeatureCatalog` unit. It contains two units:

- **DataTable** - the internal `DataTable`, `DataValue`, `DataValueKind`, `JsNumber`, and `FieldPath` types: the
  immutable typed value and table model with the deterministic comparison and JavaScript-style formatting that the
  scales and axes need; see _DataTable Unit Design_ (`data/data-table.md`)
- **DataLoader** - the internal static `DataLoader`: inline rows from the specification tree under the data limits; see
  _DataLoader Unit Design_ (`data/data-loader.md`)

The subsystem's namespace documentation is in `Data/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Data-*` requirements in `docs/reqstream/canvas-net-vega-lite/data.yaml`.

### Interfaces

**Exposed** (internal only): `DataLoader.Load(SpecNode?, string?, VegaLiteLimits?, CancellationToken)`, which returns a
`DataTable`; the table's factories `DataTable.Create`, `DataTable.FromRecords`, and `DataTable.Empty` and its typed
accessors; and the value helpers `DataValue`, `JsNumber`, and `FieldPath`.

**Consumed**: `SpecNode` and `SpecNodeKind` from the `SpecParser` unit; the public `VegaLiteLimits`,
`VegaLiteLocation`, and `VegaLiteRenderException` types of the `VegaLiteDocument` unit. The .NET base class library
supplies `System.Globalization`, `System.Numerics`, and `System.Collections.Generic`.

**Consumers**: `VegaLiteDocument` (which calls `DataLoader.Load` for a single view with a supported mark), the `Scales`
subsystem (`ScaleResolver`, `ScaleModel`, and `AxisBuilder` read `DataValue` and `DataTable`), the `Guides` subsystem
(`AxisBuilder`), and the `Marks` subsystem (`MarkBuilder`).

### Design

**Inline rows only, never a side effect.** `DataLoader` reads an array of objects (one row per object) or of primitives
(one row `{data: value}`) from the tree and returns an immutable table; every other source is an empty table and no error.
It never touches the file system or the network, so a hostile `url` costs nothing and discloses nothing; the feature
catalog tells the caller what was not loaded. This keeps "render the rest of the chart" the lenient behavior and leaves
strict mode to the report sink.

**A small typed value model.** `DataValue` is a `null`, boolean, number, or string with value equality (JavaScript's
SameValueZero, so it can be a dictionary key), a total order (null, boolean, number, string) used for discrete domains,
a quantitative reading (`TryGetQuantitative`: numbers, booleans as 1 and 0, and numeric strings), and a JavaScript-style
text form (`ToText`) so that category labels and axis labels read as they do in Vega. `JsNumber` reproduces JavaScript's
number-to-string conversion (the shortest round-trip digits) and the other number helpers that d3 relies on.

**Rows are columnar and flat.** A table has an ordered list of canonical field names and typed rows; nested objects and
arrays are flattened to leaf fields named by their canonical path (`p.q`, `p.r.0`, a literal dot in a key escaped as
`\.`), and a field a row lacks is null. `FieldPath` parses the Vega field syntax (dotted and bracketed paths with
escapes) so that an encoding's `field` finds its column.

**Limits fail closed, before the memory is spent.** The row count is checked against `MaxDataRows` before any row is
converted, and the field count against `MaxFieldCount` per row and for the union of all rows; both failures are
`VegaLiteRenderException`s with the location of the offending data. Cancellation is checked every 1,024 rows.
Recursion while flattening a row is bounded by the nesting depth that the parser accepted (at most 256).
