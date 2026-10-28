### DataTable Unit Verification Design

This document describes the unit-level verification strategy for the `DataTable` unit in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace: the internal sealed `DataTable` class (`Create`, `FromRecords`,
`Empty`, field lookup, and the typed cell accessors), and the supporting types of its file — `DataValue` and
`DataValueKind` (the scalar value model: null, boolean, number, string), `JsNumber` (JavaScript number-to-string,
rounding, and radix conversion), and `FieldPath` (Vega-Lite field path parsing and canonical names).

#### Verification Approach

The unit is verified by building tables from in-memory values and asserting on what comes back: the field order, the
cell values and their kinds, the immutability of the table, the limits, and the text and ordering of values. The unit
has no dependencies other than `VegaLiteLimits`, `VegaLiteLocation`, and `VegaLiteRenderException`, so no mocking or
stubbing is required; the internal types are reached through the test assembly's `InternalsVisibleTo` access. The
unit's interaction with the loader and the parser is verified at the subsystem tier (see _Data Subsystem
Verification Design_).

The expected values of the JavaScript number formatting are written by hand from the ECMAScript `Number::toString`
rules — shortest round-tripping digits, positional notation for magnitudes from 1e-6 up to but excluding 1e21, and
exponent notation outside that range — and not produced by running a JavaScript engine: `1` is `1`, `0.5` is `0.5`,
negative
zero is `0`, `0.000001` is `0.000001` but `0.0000001` is `1e-7`, `123456789012345680000` stays positional while `1e21`
is `1e+21`, `0.1 + 0.2` is `0.30000000000000004`, `1 / 3` is `0.3333333333333333`, the smallest subnormal is `5e-324`,
and `NaN`, `Infinity`, and `-Infinity` are spelled out. The test runs under a `de-DE` current culture, which has a
decimal comma and a different minus sign, to prove that the output does not depend on the culture. The ordering rule
(null, then boolean, then number with `NaN` first, then string in ordinal order, so `"B"` sorts before `"b"` and `9`
before `10`) and the rounding rule (`Math.round` rounds half-way cases toward positive infinity, so `2.5` is `3` and
`-2.5` is `-2`, and `0.49999999999999994` is `0`) are likewise stated from the language rules.

Unit tests reside in `DataTableTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Data`
folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own tables; the number formatting test switches the current culture on the
  test thread and restores it in a `finally` block

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any unexpected exception, wrong exception type or parameter, wrong limit message or location, wrong number
text, wrong order, or a table that changes after construction constitutes a failure.

#### Unit-Level Test Scenarios

##### Table Stores Typed Immutable Rows

**Tests**: `DataTableTests.DataTable_Create_StoresTypedImmutableRows`,
`DataTableTests.DataTable_FromRecords_UnionsKeysInOrdinalOrderAndFillsMissingWithNull`,
`DataTableTests.DataTable_FromRecords_RowsWithoutFieldsAreKept`

Asserts that a table given two fields in reverse order orders them by code unit and matches the values to the fields as
they were given; that typed accessors return a value only for the matching kind; that the table copied its input, so
later changes to the caller's arrays are not visible; that a row is a read-only view in field order and a column is
read row by row; that a table built from per-row dictionaries takes the union of the keys in ordinal order and fills
missing cells with null; and that rows without fields are still rows and the empty table has none.

##### Limits And Invalid Arguments

**Tests**: `DataTableTests.DataTable_Create_LimitsFailClosedWithLocation`,
`DataTableTests.DataTable_Create_InvalidArgumentsAreRejected`

Asserts that three rows over a `MaxDataRows` of two, and three fields (or records with three distinct keys) over a
`MaxFieldCount` of two, throw `VegaLiteRenderException` whose message names the limit and starts with the location
(`d.vl.json:4:9`) and whose pointer is `/data/values`; and that duplicate fields, ragged rows, and a null field list
are rejected with `ArgumentException` types, and out-of-range row, field, and column positions with
`ArgumentOutOfRangeException`.

##### Field Paths

**Tests**: `DataTableTests.DataTable_FindField_SupportsDottedBracketedAndEscapedPaths`,
`DataTableTests.FieldPath_Parse_SplitsPathsLikeVega`

Asserts, on a table whose fields are the canonical leaf names of `{"a":{"b":1,"c":[5,6]},"d.e":2}`, that `a.b`,
`a.c[1]`, `a['c'][0]`, and `a["b"]` find the nested leaves, that `d\.e` finds the field whose name contains a dot while
`d.e` (an unescaped dot is a separator) finds nothing, and that malformed and empty paths address nothing (the cell
read is null); and that paths split as in Vega (`a.b.c`, `a[0].b`, `a\.b` as one segment, `a['b.c']`, an escaped
bracket, an empty quoted segment, a leading or repeated dot ignored, an escaped backslash), that unterminated
brackets and quotes are malformed, and that canonical names escape backslashes and dots so that different paths
never collide.

##### JavaScript Number Formatting And Rounding

**Tests**: `DataTableTests.JsNumber_ToString_MatchesJavaScriptNumberFormatting`,
`DataTableTests.JsNumber_RoundAndRadix_FollowJavaScript`

Asserts the twenty hand-derived number-to-text pairs described above, through both `JsNumber.ToString` and
`DataValue.ToText`, under a culture with a decimal comma; that `Math.round` semantics hold (half toward positive
infinity, negative zero kept for `-0.3`, NaN unchanged); and that radix conversion of exact integers is right,
including above 2^53 (`9007199254740992` in base 16 is `20000000000000`).

##### Scalar Value Text, Ordering, Equality, And Quantitative Reading

**Tests**: `DataTableTests.DataValue_TextOrderingEqualityAndQuantitativeReading_FollowTheDocumentedRules`

Asserts the text of each kind (`null`, `true`, `false`, the string itself); that a shuffled mix of values sorts to
`null, false, true, NaN, 9, 10, B, b`; that equality is the same kind and value, with zero equal to negative zero and
`NaN` equal to `NaN`, equal values having equal hash codes, and the number `1` different from the string `"1"` and
from `true`; and that the quantitative reading accepts numbers, booleans (1 and 0), and numeric strings (including
`" 12.5 "` and `"1e3"`) and rejects `NaN`, infinity, non-numeric and blank strings, and null.
