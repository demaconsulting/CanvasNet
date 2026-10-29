### DataTable

![Data Structure](VegaLiteDataView.svg)

The `DataTable` unit is the first software unit in the `Data` subsystem. It is one source file, `DataTable.cs`, in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace, and defines five `internal` types: `DataValueKind` (enumeration),
`DataValue` (readonly struct), `JsNumber` (static class), `FieldPath` (static class), and `DataTable` (sealed class).

#### Purpose

`DataTable` is the in-memory data of a chart: an immutable table of typed values with the lookups, comparisons, and
text forms that scales, axes, and marks need. It exists so that every consumer sees one definition of what a value is,
how values order and compare, and how they read as text, independently of the culture and of the platform.

The unit's behavior is specified by the `CanvasNetVegaLite-Data-DataTable-*` requirements in
`docs/reqstream/canvas-net-vega-lite/data/data-table.yaml`.

#### Data Model

**`DataValueKind`** is `Null`, `Boolean`, `Number`, `Date`, or `String`.

**`DataValue`** (readonly struct, `IEquatable<DataValue>`) holds one value. The factories are `Null`, `FromBoolean`,
`FromNumber`, `FromString` (a null string is the null value), `FromDate(milliseconds)`, and `FromDateTime`. It exposes
`Kind`, the predicates `IsNull`, `IsBoolean`, `IsNumber`, `IsDate`, and `IsString`, and the values `Number`, `Boolean`,
`Milliseconds`, and `Text`. A **date** is an instant held as milliseconds since 1970-01-01T00:00:00Z in UTC (as
JavaScript's `Date` holds it), finite and within the years 1 to 9999 (`FromDate` throws `ArgumentOutOfRangeException`
outside that range); `FromDateTime` converts a local `DateTime` to UTC and takes an unspecified one as UTC, so a date
never depends on the host's time zone. The supported range is exposed as `MinDateMilliseconds` and `MaxDateMilliseconds`
(0001-01-01T00:00:00Z to 9999-12-31T23:59:59.999Z) so callers can test a candidate instant before creating a date, and
`MillisecondsSinceEpoch(DateTime)` computes the milliseconds of a UTC point in time from whole milliseconds in integer
arithmetic (`TimeSpan.TotalMilliseconds` divides a tick count that a double cannot hold exactly for the distant years).
Equality is JavaScript's SameValueZero (so NaN equals NaN and zero equals
negative zero), which makes the struct usable as a dictionary key. `TryGetQuantitative(out double)` reads a finite
number, a date (its milliseconds), a boolean (1 or 0), or a numeric string (trimmed), and fails for null, an empty
string, NaN, and infinity. `ToText()` is JavaScript's `String(value)`: null is `"null"`, 1 is `"1"`,
0.5 is `"0.5"`, 1e21 is `"1e+21"`, negative zero is `"0"`, and a date is its ISO 8601 form in UTC with milliseconds
(`2024-03-05T00:00:00.000Z`, like `toISOString()`). `Compare(a, b)` orders values ascending as Vega's
comparator does, with a fixed kind order (null, boolean, number, date, string) so that the order is total (a documented
deviation: JavaScript's coercions would not be); two dates compare by instant.

**`JsNumber`** reproduces JavaScript number semantics: `ToString(double)` (the shortest decimal digits that round-trip,
placed in fixed or exponential notation by JavaScript's rules), `ShortestDecimal` (the digits and the point position),
`TryParse`, `Round` (`Math.round`, half up), `IsZero`, and `ToRadix`.

**`FieldPath`** handles Vega field names: `Parse(string)` splits a path into segments (dots, brackets, quoted segments,
and backslash escapes), `Join` and `Escape` build a canonical name from segments, and `Canonicalize` maps any accepted
spelling of a path to its canonical name; a malformed path parses to null.

**`DataTable`** (sealed class, immutable) holds `Fields` (canonical names, in ordinal order), the typed rows, `RowCount`,
and `FieldCount`. `DataTable.Empty` is the shared empty table. `Create(fields, rows, limits, location)` builds a table from
a field list and rows of one value per field (field names must be unique, every row must have one value per field);
`FromRecords(records, limits, location)` builds one from dictionaries, taking the union of the keys in ordinal order and
filling missing values with null. Lookups are `IndexOfField(canonical)` and `FindField(path)` (-1 when absent or
malformed), `GetValue(row, fieldIndex)` and `GetValue(row, path)` (null for an unknown field), the strict kind accessors
`GetNumber`, `GetString`, and `GetBoolean`, `GetRow`, and `Column`.

**Invariants**: a table never exceeds `MaxDataRows` rows or `MaxFieldCount` fields; its field names are unique; a row
has exactly one value per field; nothing in a table changes after it is built.

#### Key Methods

- `DataTable.Create(...)` - checks the field count against `MaxFieldCount`, then walks the rows, checking each against
  `MaxDataRows` and the field count of the row, and stores them in one array.
- `DataTable.FromRecords(...)` - collects the key union while checking `MaxFieldCount` and the row count against
  `MaxDataRows`, orders the fields in ordinal order, and builds the rows.
- `DataValue.Compare`, `DataValue.ToText`, and `DataValue.TryGetQuantitative` - the ordering, text, and numeric readings
  described above.
- `JsNumber.ToString(double)` and `ShortestDecimal(double, out digits, out pointPosition)` - the shortest round-trip
  decimal digits of a double and their placement (also used by `NumberFormat` for exact formatting).
- `FieldPath.Parse` and `FieldPath.Canonicalize` - the field syntax.

**Preconditions and postconditions**: fields and rows are non-null and consistent; on return the table is complete and
within its limits, or an exception was thrown and nothing is returned.

#### Error Handling

An exceeded row or field limit is a `VegaLiteRenderException` carrying the supplied location (the `values` array, or
the offending leaf). A null argument is `ArgumentNullException`; duplicate field names and a row of the wrong length are
`ArgumentException`s, and a date that is not finite or is outside the years 1 to 9999 is an `ArgumentOutOfRangeException`;
these are programming errors of the caller and are not converted (the parse conversions check the range and treat an
unrepresentable date as an unconverted value). Lookups never throw for a
missing field: they return -1 or null. Numeric readings return `false` rather than throwing for text that is not a number.

#### Dependencies

- **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteRenderException`** - the public types of the
  `VegaLiteDocument` unit
- **.NET base class library** - `System.Globalization` (invariant culture), `System.Numerics` (`BigInteger`, for exact
  decimal digits), and `System.Collections.Generic`

#### Callers

`DataLoader` builds tables with `FromRecords` and `Create`, and its parse conversions create date values.
`ScaleResolver` reads the table to infer domains; `ScaleModel` keys its
discrete domains on `DataValue`; `AxisBuilder` reads `DataValue` for category labels; `MarkBuilder` reads the rows to
build marks; `NumberFormat` uses `JsNumber`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DataTableTests.cs` call the types directly through
`InternalsVisibleTo`.
