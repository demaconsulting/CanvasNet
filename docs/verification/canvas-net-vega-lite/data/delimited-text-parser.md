### DelimitedTextParser Unit Verification Design

This document describes the unit-level verification strategy for the `DelimitedTextParser` unit in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace: the internal static `DelimitedTextParser` (the CSV, TSV, and DSV
grammar and the row shaping), `ParseDirectives` (the conversions of `format.parse` and the per-column inference),
`ParseFailures` (the values that an explicit conversion could not read), and the `DelimitedOptions` and
`DelimitedResult` records.

#### Verification Approach

The unit is verified by calling `DelimitedTextParser.Parse` with small, original, hand-written texts and options, and
asserting on the resulting rows (the canonical field names in order, and the kind and value of every cell), on the
`ParseFailures` that conversion collected, and on the `VegaLiteRenderException`s thrown (their message, which names
the data, and their location). Parsing is done with no conversion (`ParseDirectives.None`) when a scenario is about
the grammar, so that every cell stays text and a failure of the grammar cannot be hidden by a conversion, and with
directives built from a real `format` object parsed by the real `SpecParser` when a scenario is about conversion, so
that the property is read exactly as `DataLoader` reads it. The unit has no collaborators to replace: the value model
(`DataValue`), the field names (`FieldPath`), and the specification tree are the real types, and the feature catalog is
used for the report of a custom date format. No mocking or stubbing is used; the internal types are reached through
the test assembly's `InternalsVisibleTo` access.

Expected values are derived by hand from the rules of the unit: a number column converts when every non-empty text is a
finite invariant number; a date is the number of milliseconds since the epoch computed independently with
`DateTimeOffset` in UTC; the first failure of a field is the first row, in order, whose text cannot be converted. No
timing is asserted.

Unit tests reside in `DelimitedTextParserTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Data` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None; every text is an in-memory string
- **Isolation**: Each test parses its own text

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any unexpected exception, wrong field list, wrong cell kind or value, wrong failure count or first failure,
wrong limit message or location, or a conversion that depends on the host time zone constitutes a failure.

#### Unit-Level Test Scenarios

##### Quoting, Delimiters, And Line Ends

**Tests**: `DelimitedTextParserTests.DelimitedTextParser_Parse_CsvQuotesAndDoubledQuotes`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_MalformedQuotingIsLocatedFailure`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_TsvAndDsvUseTheirDelimiters`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_BomCrLfAndLfAreAccepted`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_HeaderNamesAreCanonicalFieldNames`

Asserts that a quoted field holds a comma, a line break, and doubled quotes, that an empty quoted field is an empty
text, and that a quote inside an unquoted field is ordinary; that a quote that is never closed and text after a closing
quote (even a space) are located failures that name the data; that comma, tab, semicolon, and pipe each split only at
their own delimiter and leave the others as text, and that a quote, a line feed, a carriage return, and half of a
surrogate pair are refused as delimiters; that a byte-order mark, line feeds, carriage return and line feed pairs, lone
carriage returns, empty lines, and a missing final line end are all accepted; and that a header name with a dot is stored
as an escaped canonical field name so that the table finds it under the path that addresses it.

##### Ragged Rows

**Tests**: `DelimitedTextParserTests.DelimitedTextParser_Parse_RaggedRowsFillMissingAndIgnoreExtra`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_ExtraFieldsAreValidatedWithoutMaterialization`

Asserts that a row shorter than the header has null in the missing fields, that entries beyond the header are ignored,
and that empty text and a header without records give no rows. It also asserts that fifty thousand valid entries beyond
the header width are ignored (the stored row has only the header's fields) while an unclosed quote, text after a closing
quote, and an over-long unquoted or quoted entry beyond the header width are still located failures: ignored entries
are consumed and validated but never stored.

##### Type Inference Per Column

**Tests**: `DelimitedTextParserTests.DelimitedTextParser_Parse_AutoInfersBooleanNumberIsoDateAndString`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_IsoDatesAreRecognizedExactly`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_AutoOutOfRangeIsoDateStaysText`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_ExplicitOutOfRangeIsoDateBecomesNullAndWarning`

Asserts, with the explicit `ParseDirectives.Auto` opt-in, that a column of `true` and `false` in any case is boolean
with an empty cell null, a column of integers,
decimals, leading zeros, exponents, and signs (`1`, `-2.5`, `007`, `1e3`, `.5`, `+4`) is number, a column of ISO 8601
dates
and date-times is date (in UTC, with a zone offset applied), and any other column is text; that a column that mixes
kinds stays text, and that hexadecimal, `NaN`, and infinities leave their column as text; that a column with no
non-empty cell keeps its empty text; and, as a theory, that the date forms `yyyy-MM-dd`, minutes, seconds, and up to
seven fraction digits, with and without `Z` or an offset, are accepted (a text without a zone is UTC, and a leap day is
valid), while impossible dates, a day without two digits, other separators, a space instead of `T`, more than seven
fraction digits, a bare year, and the empty text are refused. The same theory covers the boundary of the supported
date range: `9999-12-31T23:59:59.999` is a date, while `9999-12-31T23:59:59.9995` and `9999-12-31T23:59:59.9999999`
(a fraction of a millisecond past the last supported millisecond, which .NET itself parses) are refused. Under
inference a column holding such text stays text with the original text kept and no failure recorded, while a column
holding the exact last millisecond is a date whose milliseconds equal 253402300799999 exactly. Under an explicit
`date` conversion the representable value converts and the out-of-range text is null with exactly one
`vl-data-parse-failed` warning naming the count, the row, and the text, and no exception escapes.

##### Explicit Parse

**Tests**: `DelimitedTextParserTests.DelimitedTextParser_Directives_ReadTheModeFromTheFormat`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_DefaultDelimitedTextKeepsLeadingZerosAndDateLikeText`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_NullKeepsTextAndExplicitMapCoercesFields`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_ExplicitConversionsOfOtherKindsFollowTheDocumentedRules`

Asserts that the directives are none when `parse` is absent (as in Vega, for every source; inference is never a
default), inference only for the explicit `"auto"`, none for `null`, and the named fields for an object (with field
names compared as canonical paths); that parsing with the default directives keeps leading zeros (`01234`, `007`),
ISO-looking dates, and `true` and `false` as strings while a map converts only the one field it names; that
`parse: null` keeps every cell as text; that a map converts only the named fields, turns empty cells into null, and
makes each value that cannot be read null while recording one failure per field and type with the count and the first
row and text; and that the conversions of other value kinds (JSON numbers, booleans, and dates) to number, boolean,
date, and string follow the documented rules.

##### Custom Date Formats Are Not Applied

**Tests**: `DelimitedTextParserTests.DelimitedTextParser_Parse_CustomDateFormatIsReportedAndNotApplied`

Asserts that a custom date format (`date:'%d/%m/%Y'`, `utc:'%Y%m%d'`) is recognized but not applied: the field
stays text as
read, no failure is recorded, and the feature catalog reports each custom format at its entry with the stable identifier
`vl-data-parse-date-format`, while an ISO `date` entry of the same specification is converted.

##### Limits And Cancellation

**Tests**: `DelimitedTextParserTests.DelimitedTextParser_Parse_RowAndFieldLimitsFailClosed`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_HeaderFieldLimitStopsMaterialization`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_CanceledTokenStopsParsing`

Asserts that more header names than `MaxFieldCount`, a repeated header name, a field text longer than `MaxStringLength`,
and more rows than the budget each fail with a `VegaLiteRenderException` that carries the location of the data and, for
a limit, names it ("the limit of ..."), that exactly each limit is accepted, that a header of a hundred thousand
entries against a limit of three is refused for the column limit (not for a later unclosed quote that the reader
never reaches, which shows that the header is rejected before the remaining entries are read or stored), and that a
canceled token stops parsing with an `OperationCanceledException`.
