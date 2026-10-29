### DelimitedTextParser

![Data Structure](VegaLiteDataView.svg)

The `DelimitedTextParser` unit is the fourth software unit in the `Data` subsystem. It is one source file,
`DelimitedTextParser.cs`, in the `DemaConsulting.CanvasNet.VegaLite.Data` namespace, and defines these `internal`
types: `ParseType` and `ParseMode` (enumerations), `ParseDirectives` (sealed class), `ParseFailures` (sealed class),
`DelimitedOptions` and `DelimitedResult` (records), and `DelimitedTextParser` (static class, with a private reader).

#### Purpose

`DelimitedTextParser` turns delimiter-separated text (CSV, TSV, or DSV) into rows of typed values, and holds the one
definition of how the `parse` property of a data format converts values (`ParseDirectives`), which `DataLoader` also
applies to JSON files, inline rows, and datasets. It exists so that the grammar of the cells, the type inference, and
the explicit conversions are specified and tested in one place, independent of where the text came from. The text it
reads was already decoded by the `DataFileResolver` unit; it does not touch files or the network.

The unit's behavior is specified by the `CanvasNetVegaLite-Data-DelimitedTextParser-*` requirements (`Grammar`,
`RaggedRows`, `Inference`, `ExplicitParse`, `CustomDateFormat`, `Limits`, and `Cancellation`) in
`docs/reqstream/canvas-net-vega-lite/data/delimited-text-parser.yaml`.

#### Data Model

**`DelimitedOptions`** (record) carries what one read needs: the `Delimiter`, the `ParseDirectives`, the
`VegaLiteLimits`, the `RowBudget` (how many rows the read may still produce under the total `MaxDataRows` of the call),
the `Display` name of the data for messages, the `VegaLiteLocation` of the `url` for errors, and the token.
**`DelimitedResult`** (record) is the rows (one dictionary from canonical field name to `DataValue` per row) and the
`ParseFailures`.

**Grammar.** The first record is the header; its entries are the field names, stored as canonical field names (a header
`a.b` is addressed as `a\.b`, like a JSON key with a dot, so it is not mistaken for a nested path). A record ends at a
line feed, a carriage return followed by a line feed, or a lone carriage return. A field is unquoted (it runs to the next
delimiter or line end; a quote inside it is an ordinary character) or quoted with a double quote (it may hold
delimiters and line breaks, and a doubled quote stands for one quote); any text between a closing quote and the next
delimiter or line end is an error, as is a quote that is never closed. A line without any character is skipped, a
missing final line end is fine, and a leading U+FEFF is ignored. A delimiter is any single UTF-16 unit except a quote,
a line break, and a surrogate (`IsValidDelimiter`); a comma is CSV, a tab is TSV, and DSV uses the character that the
format names.

**Ragged rows.** An entry missing from a short row is null; entries beyond the header are ignored; empty text, or a
header without records, gives no rows. A header with more names than `MaxFieldCount`, a repeated name, a field text
longer than `MaxStringLength`, and more rows than `RowBudget` are errors.

**Bounded storage.** The reader receives a storage cap with each record. The header is read with a cap of
`MaxFieldCount` and is refused as soon as it has one entry more, before that entry is read or stored, so a header of
millions of delimiters never builds a list. A data row is read with a cap of the header width: an entry beyond it is
still consumed and fully validated (quote closure, text after a closing quote, and the `MaxStringLength` rule, counted
without building the text) but no string or list entry is created for it. A valid wide row is therefore ignored as the
ragged-row rule says, and a malformed or over-long extra entry is still an error.

**`ParseDirectives`** says how values are converted. Its mode is `None` (nothing converted), `Auto` (infer per column),
or `Fields` (a map from canonical field name to `ParseType`: `Number`, `Boolean`, `Date`, `String`, or `CustomDate`).
`Read(format, defaultAuto)` reads the `parse` property: as in Vega, no data-based inference happens unless it is asked
for, so an absent property is `None` (the loader always passes `defaultAuto: false`, for delimited text, JSON, inline
rows, and datasets alike); `"auto"` is the explicit opt-in to `Auto`; `null` means `None`; an object means `Fields`,
which converts only the fields it names. Keys are field paths compared in canonical form, like the fields of an
encoding. Delimited cells therefore stay strings (a leading zero or an ISO-looking date is kept as text) unless the
specification opts in; quantitative encodings still coerce numeric text lazily when they are drawn, and temporal
parsing of text arrives in Phase 8.

**Type inference (`Auto`, only when requested)** looks at a whole column, as Vega does, only at columns whose values
are all text (a column
that holds anything else, such as JSON numbers, is left alone): the column is a **boolean** column when every non-empty
text is `true` or `false` in any case (surrounding white space ignored); else a **number** column when every non-empty
text is a finite number in the invariant culture - an integer, a decimal with a point, or an exponent, with an optional
sign (hexadecimal, thousands separators, `NaN`, infinities, and numbers outside the range of a double are not
numbers); else a **date** column when every non-empty text is an ISO 8601 date or date-time (`yyyy-MM-dd`,
`yyyy-MM-ddTHH:mm`, `...:ss`, and `...:ss.f` with up to seven fraction digits, each with an optional `Z` or `+HH:mm`
offset); otherwise text. A date without a zone is UTC, so the result never depends on the host time zone or culture.
A date counts only if the value model can hold it: the instant must lie within the years 1 to 9999 in whole
milliseconds (`DataValue.MinDateMilliseconds` to `MaxDateMilliseconds`), so `9999-12-31T23:59:59.999` is a date while
`9999-12-31T23:59:59.9995` (a fraction of a millisecond beyond the last supported one) is not, even though .NET parses
it. `TryParseIsoDate` applies that range itself, computing the milliseconds in integer arithmetic
(`DataValue.MillisecondsSinceEpoch`, because `TimeSpan.TotalMilliseconds` is inexact for the distant years), so text
beyond the range is simply "not a date" in both paths below and no `ArgumentOutOfRangeException` can reach the caller.
In a typed column an empty text is null. A column with no non-empty text stays text. The tests are in that order, so a
column of `1` and `0` is numbers and a column of dates is not numbers.

**Explicit conversion (`Fields`)** converts only the named fields, by these rules (null stays null, and an empty or
white-space text becomes null except for `string`):

- `number`: a number stays; a boolean is 1 or 0; a date is its milliseconds; a text is parsed as above; anything else is
  a failure
- `boolean`: a boolean stays; a number is true when not zero; a text is `true` or `false` in any case, or `1` or `0`; a
  date or any other text is a failure
- `date`: a date stays; a number is milliseconds since the epoch (a failure outside the years 1 to 9999); a text is an
  ISO 8601 date or date-time within the supported range (an out-of-range one is a failure: null plus the ordinary
  `vl-data-parse-failed` warning); a boolean or any other text is a failure
- `string`: the JavaScript text form of the value (`DataValue.ToText`)
- a custom date format (`date:'%fmt'`, `utc:'%fmt'`): recognized and **not applied**; the field is left as read and
  no failure is recorded; the feature catalog reports it as `vl-data-parse-date-format`

A value that cannot be converted becomes null and is recorded in **`ParseFailures`**, which keeps, per field and
type, the count and the row and text of the first failure and creates one `vl-data-parse-failed` warning per field and
type (text shortened to 40 characters, control characters shown as a question mark). The failures are sorted by field
name so that the warnings are deterministic.

**Invariants**: the rows of a result never exceed `RowBudget`; every row has exactly the header's fields; a result
holds only null, boolean, number, date, and string values; the parser keeps no state between calls.

#### Key Methods

- `DelimitedTextParser.Parse(text, options)` - validates the delimiter, reads the header with `ReadHeader` (canonical
  names and the duplicate rule; the field limit is enforced by the reader while it reads), reads the records one at a
  time under the row budget, the storage cap, and the string limit, checks the token every 1,024 records, builds one
  dictionary per row, then applies the directives and returns the rows and the failures.
- `DelimitedTextParser.IsValidDelimiter(char)` - the delimiter rule shared with `SpecParser`'s validation.
- `ParseDirectives.Read`, `TryClassify`, and `Apply` - read the property, classify a `parse` entry (also used by
  `SpecParser` and `FeatureCatalog` so that validation, reports, and loading agree), and convert the rows in place.
- `ParseDirectives.TryParseBoolean`, `TryParseNumber`, `TryParseIsoDate`, and `TryConvert` - the conversions above.
- `ApplyAuto` and `InferColumn` - the per-column inference; `ApplyField` - one explicit field.
- `ParseFailures.Add` and `ToReports` - collect failures and create the warnings.

**Preconditions and postconditions**: `text` is decoded text and `options` is not null; on return the rows are within
the budget and limits and every value is converted per the directives, or an exception was thrown and nothing is
returned.

#### Error Handling

A delimiter that is not valid, a quote that is never closed, text after a closing quote (also in an entry beyond the
header width), too many header names, a
repeated header name, a field text longer than `MaxStringLength`, and more rows than the budget are
`VegaLiteRenderException`s with the location of the `url` and, where it helps, the line of the text; the limit
messages contain "the limit of ..." so that `CanvasNetTool` maps them to `budget-exceeded`. Null arguments are
`ArgumentNullException`s (programming errors, not converted). A canceled token is an `OperationCanceledException`, never
wrapped. A value that an explicit conversion cannot read is not an error: it is null and counted for a warning, so the
rest of the chart is still drawn.

#### Dependencies

- **`DataValue`** and **`FieldPath`** - the value model and field names of the `DataTable` unit
- **`SpecNode`** (the `format` object) and **`FeatureReport`** (the warnings) - types of the `SpecParser` and
  `FeatureCatalog` units
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteRenderException`** - the public types of the
  `VegaLiteDocument` unit
- **.NET base class library** - `System.Globalization` (invariant-culture number and date parsing) and
  `System.Collections.Generic`

#### Callers

`DataLoader` calls `Parse` for CSV, TSV, and DSV files and `ParseDirectives.Read` and `Apply` for every source.
`SpecParser` (validation) and `FeatureCatalog` call `IsValidDelimiter` and `TryClassify`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/DelimitedTextParserTests.cs` call the unit directly through
`InternalsVisibleTo`.
