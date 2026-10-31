### DelimitedTextParser Unit Verification Design

This document describes the unit-level verification strategy for the `DelimitedTextParser` unit in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace: the internal static `DelimitedTextParser` (the CSV, TSV, and DSV
grammar and the row shaping), `ParseDirectives` (the conversions of `format.parse` and the per-column inference),
`ParseFailures` (the values that an explicit conversion could not read), and the `DelimitedOptions` and
`DelimitedResult` records. Since Phase 8 the unit also converts dates in the zone of a `TimeCalendar` with the rules of
JavaScript's `Date.parse` (the `date` conversion and the inference), applies the custom formats `date:'%fmt'` and
`utc:'%fmt'`, and applies the implicit date parse of the fields that the encoding uses as temporal (see _Time Calendar
Verification_ and _Time Format Verification_ for the calendar and the format parser themselves).

#### Verification Approach

The unit is verified by calling `DelimitedTextParser.Parse` with small, original, hand-written texts and options, and
asserting on the resulting rows (the canonical field names in order, and the kind and value of every cell), on the
`ParseFailures` that conversion collected, and on the `VegaLiteRenderException`s thrown (their message, which names
the data, and their location). Parsing is done with no conversion (`ParseDirectives.None`) when a scenario is about
the grammar, so that every cell stays text and a failure of the grammar cannot be hidden by a conversion, and with
directives built from a real `format` object parsed by the real `SpecParser` when a scenario is about conversion, so
that the property is read exactly as `DataLoader` reads it. The date conversions are verified in a second class,
`ParseDirectivesDateTests`, which applies directives to rows of text cells directly (`ParseDirectives.Apply`, with an
explicit `TimeCalendar` where the zone matters) so that the zone, the implicit date fields, and the custom formats are
observed without a CSV text around them. The unit has no collaborators to replace: the value model (`DataValue`), the
field names (`FieldPath`), the calendar (`TimeCalendar`, built by the `TimeTestSupport` helpers as UTC, a fixed offset,
or a zone with a daylight-saving rule), and the specification tree are the real types, and the feature catalog is used
to show that a custom date format is no longer reported. No mocking or stubbing is used; the internal types are reached
through the test assembly's `InternalsVisibleTo` access.

Expected values are derived by hand from the rules of the unit: a number column converts when every non-empty text is a
finite invariant number; a date is the number of milliseconds since the epoch computed independently (with
`DateTimeOffset` in UTC in `DelimitedTextParserTests`, and with `TimeTestSupport.Ms` in `ParseDirectivesDateTests`); the
first failure of a field is the first row, in order, whose text cannot be converted. The zone arithmetic is checked by
hand: with a zone five hours behind UTC in March before the daylight-saving change of 2024-03-10, a local `10:00` is
`15:00Z`; with a fixed offset of +05:30 a local `10:00` is `04:30Z`; with an explicit offset `+02:00` a `10:00` is
`08:00Z`. No timing is asserted.

Unit tests reside in `DelimitedTextParserTests.cs` and `ParseDirectivesDateTests.cs` within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Data` folder.

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
`DelimitedTextParserTests.DelimitedTextParser_Parse_DatesAreRecognizedByTheDateParseRules`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_AutoOutOfRangeDateStaysText`,
`DelimitedTextParserTests.DelimitedTextParser_Parse_ExplicitOutOfRangeDateBecomesNullAndWarning`

Asserts, with the explicit `ParseDirectives.Auto` opt-in, that a column of `true` and `false` in any case is boolean
with an empty cell null, a column of integers, decimals, leading zeros, exponents, and signs (`1`, `-2.5`, `007`,
`1e3`, `.5`, `+4`) is number, a column of ISO 8601 dates and date-times is date (in UTC, with a zone offset applied),
and any other column is text; that a column that mixes kinds stays text, and that hexadecimal, `NaN`, and infinities
leave their column as text; that a column with no non-empty cell keeps its empty text; and, as a theory, the date
rules of `ParseDirectives.TryParseDate`, which are those of JavaScript's `Date.parse` and replace the former ISO-only
reader. The accepted texts are the ECMAScript forms (`yyyy-MM-dd` read in UTC, date-times to the minute, second, and
fraction read in the zone of the calendar, UTC here, or at an explicit `Z` or offset, a leap day, surrounding white
space) and the legacy forms (`2024-03-05 10:20:30`, `March 5, 2024`, `2024/03/05`, and a bare year `2024`); the refused
texts are an impossible date (`2023-02-29`, month 13), a two-digit year with other separators (`24-03-05`), an hour of
25, text that is no date, the empty text, and the expanded year `+010000-01-01T00:00:00Z`, which lies past the last
supported millisecond. The same theory covers the boundary of the supported date range: `9999-12-31T23:59:59.999` is a
date, and so is `9999-12-31T23:59:59.9995` (as in ECMAScript, the digits beyond the millisecond are truncated; the
value is the same as for `.999`). Under inference a column holding text beyond the range stays text with the original
text kept and no failure recorded, while a column holding the exact last millisecond is a date whose milliseconds
equal 253402300799999 exactly (the hand check: the last day of year 9999 ends at `253402300800000` ms, which is
2,932,897 days x 86,400,000 ms, so one millisecond earlier is the value asserted). Under an explicit `date` conversion
the representable value converts and the out-of-range text is null with exactly one `vl-data-parse-failed` warning
naming the count, the row, and the text, and no exception escapes.

##### Date Conversion In The Zone Of The Calendar

**Tests**: `ParseDirectivesDateTests.ParseDirectives_Date_UsesTheCalendarZoneForDateTimesWithoutOffset`,
`ParseDirectivesDateTests.ParseDirectives_Date_DefaultCalendarIsUtc`,
`ParseDirectivesDateTests.ParseDirectives_Date_NumberIsMillisecondsAndBooleanFails`,
`ParseDirectivesDateTests.ParseDirectives_Auto_InfersLegacyDatesAndIso`,
`ParseDirectivesDateTests.ParseDirectives_Auto_PrefersNumbersOverDates`,
`ParseDirectivesDateTests.ParseDirectives_Auto_UsesTheCalendarZone`

Asserts that `parse: {d: "date"}` follows JavaScript's mixed rule in the zone of the calendar: with a zone five hours
behind UTC in March (before the change of 2024-03-10) `2024-03-05T10:00` is local, so it is `15:00Z`; the date alone
`2024-03-05` is UTC midnight; `2024-03-05T10:00Z` is `10:00Z`; and `2024-03-05T10:00+02:00` is `08:00Z`, so an explicit
offset is honored and the zone is irrelevant. With no calendar given the zone is UTC, so the zone never changes a
specification that sets none. A number converts to the same milliseconds (`86,400,000` stays one day), a boolean and a
null are null, and the boolean is one failure. Inference reads the legacy forms next to ISO text (`Jan 2, 2006` and
`March 5, 2024` are dates, and a column that mixes a legacy form with an ISO date is still one date column); a column
of years (`2024`, `1999`) stays a number because the order of inference is boolean, then number, then date; and an
inferred date-time without an offset uses the calendar's zone (`2024-03-05T10:00` at a fixed +05:30 is `04:30Z`, the
hand check being 10:00 minus 5 h 30 min).

##### Custom Date Formats

**Tests**: `DelimitedTextParserTests.DelimitedTextParser_Parse_CustomDateFormatIsApplied`,
`ParseDirectivesDateTests.ParseDirectives_CustomDate_ParsesDayMonthYear`,
`ParseDirectivesDateTests.ParseDirectives_CustomDate_Utc_IgnoresTheZone`,
`ParseDirectivesDateTests.ParseDirectives_CustomDate_FailureIsNullWithOneWarningPerField`,
`ParseDirectivesDateTests.ParseDirectives_CustomDate_NumberIsParsedThroughItsText`

Asserts that the custom directives `date:'%fmt'` and `utc:'%fmt'` are applied by the d3 time format parser (see _Time
Format Verification_): `05/03/2024` and `31/12/1999` read with `%d/%m/%Y` are dates (kind date, no failure); `date:`
reads local time in the zone of the calendar while `utc:` ignores the zone (`05/03/2024 10` at five hours behind UTC is
`15:00Z` for `date:` and `10:00Z` for `utc:`, and a double-quoted format works like a single-quoted one); a text that
does not match (`yesterday`, and an ISO text that does not fit the format) and the empty text are null, and the two
unreadable texts give one warning `vl-data-parse-failed` for the field that names the count (2), the conversion `date
with a custom format`, and the first row and text (row 2, `yesterday`); and a number is read through its JavaScript
text (`2020` is the year 2020 with `%Y`, `2020.5` does not match), a date passes, and a boolean is null. The migrated
catalog scenario `DelimitedTextParser_Parse_CustomDateFormatIsApplied` shows that `date:'%d/%m/%Y'`, `utc:'%Y%m%d'`
and the plain `date` give the same UTC date for the same day (`05/03/2024`, `20240305`, `2024-03-05`), with no failure,
and that the feature catalog has nothing to report for that specification: a custom date format is no longer an
unsupported feature, so the former identifier `vl-data-parse-date-format` is not reported for it (see _Feature Catalog
Verification_).

##### Implicit Date Parse

**Tests**: `ParseDirectivesDateTests.ParseDirectives_Implicit_AppliesToTemporalFieldsOnly`,
`ParseDirectivesDateTests.ParseDirectives_Implicit_ExplicitParseWins`,
`ParseDirectivesDateTests.ParseDirectives_Implicit_ParseNullSuppresses`,
`ParseDirectivesDateTests.ParseDirectives_Implicit_UnparsableTextIsNullWithTheParseWarning`,
`ParseDirectivesDateTests.ParseDirectives_Implicit_AutoParsesTheTextThatInferenceLeaves`

Asserts that the fields that the encoding uses as temporal (the `implicitFields` given to `ParseDirectives.Read`) are
date-parsed even with no `parse` property: the mode is `Fields`, the temporal field `d` becomes a date, and the other
field `e` with the same text stays a string; that an explicit entry wins (`d: "string"` stays text, `f` uses its own
custom format, and the other implicit field `e` is still parsed); that `parse: null` sets `Suppressed` and switches the
implicit parse off (the mode is `None` and the text stays text) while an empty `format` does not; that an implicit
field whose text is no date (`n/a`) is null with the usual warning naming row 2; and that with `parse: "auto"` an
implicit field that inference left as text is date-parsed afterwards (one failure for `oops`), while a temporal field
that inference converted to number stays a number.

##### Date Conversion Limits And Culture

**Tests**: `ParseDirectivesDateTests.ParseDirectives_Date_HugeCellIsNotADateAndCostsNothing`,
`ParseDirectivesDateTests.ParseDirectives_Apply_PollsCancellationEveryBatch`,
`ParseDirectivesDateTests.ParseDirectives_Apply_StopsInsideAColumnWhenCanceledMidway`,
`ParseDirectivesDateTests.ParseDirectives_Date_IgnoresTheCurrentCulture`

Asserts that a cell of 1,048,576 characters (as many digits, as many letters, or a date padded with 1,024 spaces on
each side) is handled in bounded work: the digits and letters are no date for the plain conversion and the custom
format (null, with a failure message shorter than 300 characters), the padded date parses because the trimmed text is
within the parse cap, inference leaves the letters as text, and `TryParseDate` refuses the letters. A token that is
already canceled stops both the explicit conversion and the inference of 10,000 rows with an
`OperationCanceledException`, and a token canceled while row 5,000 of 10,000 is being read stops the conversion at the
next batch boundary, for both the explicit conversion and the inference (the rows poll the token, not only the start of
a field). Under `tr-TR` and `de-DE` the date conversions give the same values: `APRIL 5, 2024` (a Turkish capital I
does not break the month name), the legacy `05/04/2024` read as month/day/year (May 4, 2024, not April 5),
`2024-04-05T10:30:00.5Z` with 500 ms, and the custom `%B %d, %Y` matching month names ignoring case.

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
