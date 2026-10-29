### DataTable Unit Verification Design

This document describes the unit-level verification strategy for the `DataTable` unit in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace: the internal sealed `DataTable` class (`Create`, `FromRecords`,
`FromSortedCells`, `Empty`, field lookup, and the typed cell accessors), and the supporting types of its file —
`DataValue` and `DataValueKind` (the value model: the scalars null, boolean, number, date, and string, and the composite
kinds array, object, and regular expression that the expression engine produces), `JsNumber` (JavaScript
number-to-string, rounding, and radix conversion), and `FieldPath` (Vega-Lite field path parsing and canonical names) —
and `DataObject` and `DataArray` of the sibling file (the immutable, insertion-ordered object payload and the identity
wrapper of an array payload).

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
(null, then boolean, then number with `NaN` first, then date, then string in ordinal order, so `"B"` sorts before `"b"`
and `9` before `10`) and the rounding rule (`Math.round` rounds half-way cases toward positive infinity, so `2.5`
is `3` and
`-2.5` is `-2`, and `0.49999999999999994` is `0`) are likewise stated from the language rules.

Unit tests reside in `DataTableTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Data`
folder; the tests of the composite values are in `DataCompositeValueTests.cs` in the same folder, a separate file
because the composite kinds were added to the value model beside the existing scalar scenarios and the scalar
behavior is unchanged (the scalar order, equality, and text scenarios of `DataTableTests.cs` still pass as they were).

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

##### Date Values

**Tests**: `DataTableTests.DataTable_DateValuesHaveDeterministicQuantitativeAndTextForms`

Asserts, with hand-derived instants (2024-03-05T00:00:00Z is 1,709,596,800,000 milliseconds, the epoch, and a time
before the epoch), that a date is created from milliseconds or from a `DateTime` of kind UTC, unspecified (taken as
UTC), or local (converted to UTC) with the same result whatever the host time zone is; that its kind is `Date`, not
`Number`; that its quantitative reading is its milliseconds; that its text is the ISO 8601 form with milliseconds and a
trailing `Z` under a `de-DE` current culture; that a date equals only the same instant (not the number of
milliseconds, and not the text), zero equals negative zero, and equal dates have equal hash codes; that dates sort by
instant after numbers and before strings with null first; and that an instant that is not finite or is outside the years
1 to 9999 is refused with `ArgumentOutOfRangeException`, while the first and the last representable millisecond are
accepted and print as `0001-01-01T00:00:00.000Z` and `9999-12-31T23:59:59.999Z`.

##### Scalar Value Text, Ordering, Equality, And Quantitative Reading

**Tests**: `DataTableTests.DataValue_TextOrderingEqualityAndQuantitativeReading_FollowTheDocumentedRules`

Asserts the text of each kind (`null`, `true`, `false`, the string itself); that a shuffled mix of values sorts to
`null, false, true, NaN, 9, 10, B, b`; that equality is the same kind and value, with zero equal to negative zero and
`NaN` equal to `NaN`, equal values having equal hash codes, and the number `1` different from the string `"1"` and
from `true`; and that the quantitative reading accepts numbers, booleans (1 and 0), and numeric strings (including
`" 12.5 "` and `"1e3"`) and rejects `NaN`, infinity, non-numeric and blank strings, and null.

##### Table From Sorted Cells

**Tests**: `DataTableTests.DataTable_FromSortedCells_BuildsATableAndChecksTheShape`

Asserts that a table built from fields that are already in ordinal order and a flat row-major cell array (two rows of
the fields `a` and `b`, the cell `b` of the first row an array of `x` and 2) has two rows and two fields, reads each
cell back (a number, an array whose text is `x,2`, and a null), that a table of no fields with three rows keeps its three
rows, and that unordered fields, a repeated field, and a cell count that does not match the rows and fields are
`ArgumentException` errors, a null field list or cell array is an `ArgumentNullException`, and a negative row count is
an `ArgumentOutOfRangeException`.

##### Arrays Are Immutable Values With JavaScript Text

**Tests**: `DataCompositeValueTests.DataValue_FromArray_CopiesElementsAndFormatsLikeJavaScript`

Asserts that an array value copies its input (a later change of the caller's list is not visible), has the kind `Array`
and its elements in order, has the JavaScript text of `Array.prototype.toString` (`1,,a` for 1, null, and `a`; the empty
text for an empty array; nested arrays flattened by their text), can hold composites, rejects a null input with an
`ArgumentNullException`, and that a scalar has no elements, no object payload, and no regular-expression payload.

##### Shared Composites Are Scanned Once Per Node And Their Text Is Bounded

**Tests**: `DataCompositeValueTests.DataValue_SharedComposites_AreScannedOncePerNodeAndTheirTextIsBounded`

Asserts, on chains of 40 `[x, x]` arrays and of alternating arrays and objects (2^40 paths), that the text limit check
finds a 50-character text for a limit of 49 and none for 50 with a scan of at most 1,000 elements and members, and that
the text of the array chain is refused (`InvalidOperationException` from `ToText`, `false` from `TryAppendArrayText`)
instead of being built.

##### Composites Are Equal Only To Themselves

**Tests**: `DataCompositeValueTests.DataValue_Composites_AreEqualOnlyToThemselves`

Asserts that two arrays with the same elements are different, two empty arrays are different, two empty objects are
different, one value and a copy of it are equal and have equal hash codes, an array is never equal to an object or to
the text it prints as, and two regular-expression values are equal exactly when they carry the same payload, so equality
is JavaScript identity and never structure.

##### Objects Keep Insertion Order And The Last Value Of A Repeated Key

**Tests**: `DataCompositeValueTests.DataObject_Create_KeepsInsertionOrderAndLastValueOfARepeatedKey`

Asserts that an object built from the members `b`, `a`, and `b` again has the two keys `b` and `a` in that order with the
last value (3) at the position of the first, that the object copied its input, that lookup by name is exact (case
sensitive, a missing name gives null and false), that an object of twenty members, which uses an index, finds `k17` and
not `k20`, that out-of-range positions and null arguments are refused, and that the value wrapper has the kind `Object`,
shares the payload, and prints as `[object Object]`.

##### Regular Expression Values Carry An Opaque Payload

**Tests**: `DataCompositeValueTests.DataValue_FromRegExp_CarriesOpaquePayload`

Asserts that a regular-expression value has its kind, returns the very payload it was given, prints as the text of the
payload (`/a+/g`), and rejects a null payload. The payload is opaque to the `Data` subsystem; the expression engine owns
its meaning.

##### Composite Ordering Leaves The Scalar Order Unchanged

**Tests**: `DataCompositeValueTests.DataValue_Compare_ScalarOrderIsUnchangedAndCompositesSortLast`

Asserts that one value of each scalar kind (null, `false`, 5, a date, and `a`) is ordered as before, that an array sorts
after every scalar, that an object sorts after an array, that two arrays compare as equal in order, and that the kinds
null, boolean, number, date, and string keep their positions in the enumeration with the array kind appended after them
(the value 5), so no stored kind changes.
