### TimeFormat

![Data Structure](VegaLiteDataView.svg)

The `TimeFormat` unit is the sixth software unit in the `Data` subsystem. It comprises six source files in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace, all `internal`, each with the attribution header comment for the
d3-time-format, d3-time, and Vega behavior it ports:

- `TimeFormat.cs` - `TimeFormat` (static partial class: the English locale tables, the shared character helpers, and
  the constants of the limits)
- `TimeFormat.Format.cs` - `TimeFormatter` (the compiled format), `TimeFormatTooLongException`,
  `TimeFormatSpecifierException`, `FormatToken`, and `TimeFormat.Compile` (the specifier compiler)
- `TimeFormat.Parse.cs` - `ParsedTimeFields` and `TimeFormat.TryParse` (the hand scanner that is d3's `timeParse` and
  `utcParse`)
- `TimeFormat.Multi.cs` - `TimeFormat.Multi` (Vega's `timeMultiFormat`, the default labels of a time axis)
- `JsDateParser.cs` - `JsDateParser` (static partial class): JavaScript's `Date.parse` for the ECMAScript date-time
  string format
- `JsDateParser.Legacy.cs` - the documented subset of the legacy (non-ISO) forms of `Date.parse`

The unit is in `Data` for the same reason as `TimeCalendar` (plan decision P8-D1): the data parser, the expression
functions, the axes, and the legends all format or parse dates and none of them may depend on `Scales` or
`Transforms`.

#### Purpose

`TimeFormat` formats and parses dates the way d3-time-format does, with its default English locale, and parses date
text the way JavaScript's `Date.parse` does, so that axis labels, `parse` directives, the expression functions
`timeFormat`, `timeParse`, and their `utc` forms, and the implicit date parsing of temporal fields read as they do in
Vega-Lite. It exists so that date text is a faithful, bounded, culture-independent port that never consults the
platform's date formatting or `DateTime.Parse`.

The unit's behavior is specified by the `CanvasNetVegaLite-Data-TimeFormat-*` requirements in
`docs/reqstream/canvas-net-vega-lite/data/time-format.yaml`. The rules are those of d3-time-format, Vega, and the
JavaScript language, re-implemented from their public description (see _CanvasNetVegaLite System Design_,
Third-Party Provenance and Attribution).

#### Data Model

**Pure functions, no state.** `TimeFormat` has no static cache and no mutable state: `Compile` returns an immutable
`TimeFormatter` that the caller keeps (so concurrent use needs no synchronization), and the only calendar used is the
`TimeCalendar` that is passed in (see _TimeCalendar_). The names are the English d3 locale (the `en-US` locale of
d3-time-format with its default period names `AM` and `PM`), the digits are invariant ASCII digits, names match ASCII
case-insensitively without a culture, and the host's culture, zone, and clock are never read. Locales other than
`en-US` are out of scope.

**`TimeFormatter`** (immutable; safe for concurrent use) is either a **plain** formatter (a list of `FormatToken`
values: literal text, or a directive letter with its fill character) or a **multi-format** formatter (a function that
chooses a plain formatter for an instant, bound to the calendar given to `Multi`, which makes the `calendar` argument
of its `Format` ignored).

**Directives** (`Compile`). The specifier is literal text and the directives `%a %A %b %B %c %d %e %f %g %G %H %I %j
%L %m %M %p %q %Q %s %S %u %U %V %w %W %x %X %y %Y %Z %%`, each optionally with a padding flag between the percent sign
and the letter: `-` for no padding, `_` for a space, `0` for a zero (`%e` pads with a space by default). The numeric
widths are 2 for `%d %e %g %H %I %m %M %S %U %V %W %y`, 3 for `%j %L`, 4 for `%Y %G`, and 1 for `%q %u %w %Q %s`, which
never pad; `%f` is the milliseconds with three zeros appended (microseconds). `%Y` and `%G` are the year modulo 10,000,
`%I` is
the 12-hour clock (0 is 12), `%q` is the quarter, `%p` is `AM` or `PM`, `%Q` is the epoch milliseconds and `%s` the epoch
seconds (neither depends on the zone), and `%Z` is the offset of the instant as `+hhmm` (whole minutes; the seconds of
historic offsets are truncated). The week numbers are `%U` (Sunday-based), `%W` (Monday-based), and the ISO 8601 `%V`,
`%G`, and `%g`, computed with civil arithmetic that equals d3's interval counts. The locale directives expand when
compiled: `%c` is `%x, %X`, `%x` is `%-m/%-d/%Y`, and `%X` is `%-I:%M:%S %p`. **Unknown directives are not an error**:
as in d3 the percent sign is dropped and the letter is copied (`%k` gives `k`), and a percent sign at the end of the
specifier gives nothing. `pad` writes a negative number with a minus sign (the digits are padded, the sign is not
counted).

**Formatting limits and the invalid date.** `Format(ms, calendar, maxLength)` writes at most `maxLength` characters and
throws `TimeFormatTooLongException` (an `InvalidOperationException`) as soon as the text would be longer, before
more is allocated, so a huge specifier cannot allocate more than the limit. An instant that is NaN or outside the
years 1 to 9999 formats as `Invalid Date` (JavaScript's `String(new Date(NaN))`; d3 would print `NaN` pieces). Callers
pass the `MaxStringLength` limit.

**Parsing** (`TryParse(specifier, text, calendar, out ms)`). The rules are d3's, with a hand scanner instead of regular
expressions:

- A literal must match exactly; the names of `%a %A %b %B %p` match ignoring ASCII case; the text must be consumed
  completely, and (as in d3) it must have a character left for every piece of the specifier.
- A number directive reads a **window**: at most 2 characters for `%d %e %m %M %S %H %I %y %g %U %V %W`, 3 for `%j %L`,
  4 for `%Y %G`, 6 for `%f`, 1 for `%q %u %w`, and an unbounded window for `%Q` and `%s`. The window may start with
  JavaScript white space (the `\s*` of d3's expression, so `%L` is "1 to 3 digits", and `09.5` gives 5 milliseconds,
  not 500), then digits; a sign is not read.
- There is **no range validation**, as in d3: `2024-13-01` with `%Y-%m-%d` is 2025-01-01, hour 25 rolls into the next
  day. `%V` outside 1 to 53 fails. The defaults are the year 1900, January, the first, and time zero. `%y` and `%g` add
  1900 above 68 and 2000 otherwise. `%p` makes the hour a 12-hour value. `%j` is the day of the year. `%a`, `%A`, and
  `%w` set the same weekday field. `%U`, `%W`, and `%V` convert the week and the weekday (`%u` and `%w`) as d3 does.
  The padding flags `0`, `_`, and `-` are read and ignored.
- `%Z` reads `Z` or `+hh[:]mm` with d3's expression `/^(Z)|([+-]\d\d)(?::?(\d\d))?/`, whose second alternative is
  unanchored on the next six characters and advances by the **length** of the match (reproduced: `x+0200` fails), and
  makes the other fields UTC with the offset applied. `%Q` and `%s` give a timestamp directly.
- Years 0 to 99: d3 builds the date in the year -1 and then sets the year, so `0004-02-29` becomes 0004-03-01
  (reproduced and pinned).
- The calendar decides the flavor: for the UTC calendar the fields are UTC (`utcParse`), for a zone they are wall-clock
  times of that zone (`timeParse`), with the gap and overlap rule of `TimeCalendar`.
- A result outside the years 1 to 9999 fails the parse (d3 would give an invalid date), and the method never throws
  for any text or specifier.

**Vega multi-format** (`TimeFormat.Multi(calendar, overrides)`). The formatter labels an instant by the **coarsest
calendar boundary it sits on**: the test is Vega's, a level applies when the floor of the interval at the next finer
level is below the instant. The defaults are `.%L` (not on a whole second), `:%S` (a second that is not a whole
minute), `%I:%M` (a minute), `%I %p` (an hour), `%a %d` (a day that is not a week start), `%b %d` (a week start that is
not a month start), `%B` (a month or a quarter start that is not a year start), and `%Y` (a year start). The overrides
are keyed by `milliseconds`, `seconds`, `minutes`, `hours`, `date` (or `day`), `week`, `month`, `quarter`, and `year`;
the first non-empty override under the keys wins (JavaScript's `spec[date] || spec[day]`), and an empty override keeps the
default. An invalid instant gets `Invalid Date`.

**`JsDateParser`** (`TryParse(text, calendar, out ms)`) is JavaScript's `Date.parse` as Vega uses it for
`parse: "date"`, for `parse: "auto"` inference, and for the implicit parse of temporal fields (plan decision P8-D4):

- **ECMAScript date-time string format.** `YYYY`, `YYYY-MM`, or `YYYY-MM-DD` (also `+YYYYYY` and `-YYYYYY` years;
  `-000000` is invalid), optionally followed by `T` or a space and `HH:mm`, `HH:mm:ss`, or `HH:mm:ss.fff` (any number
  of fraction digits, truncated to milliseconds), and an offset (`Z`, `+HH:mm`, `+HHmm`, or `+HH`). JavaScript's mixed
  rule is applied against the chosen calendar: a **date without a time is UTC**; a **date-time without an offset is in
  the zone of the calendar** (UTC for the default zone, so existing specifications do not change); an offset or `Z` is
  honored. The time `24:00` is the end of the day, other hours above 23 are rejected, and a day that does not exist
  (`2023-02-29`) is rejected (V8 rolls some of these over, a documented deviation).
- **Documented legacy subset** (local unless a zone is given; month and weekday names in any case, full or at least
  three letters; a parenthesized comment is skipped): `Jan 2, 2006`, `January 2, 2006 10:00`, `2 Jan 2006`,
  `Mon Jan 02 2006 15:04:05 GMT+0200 (comment)`, the RFC 1123 form `Tue, 02 Jan 2006 15:04:05 GMT`, `2006/01/02`, and
  `1/2/2006` (month, day, year), with an optional time `h:mm[:ss[.fff]]` and `AM` or `PM`, a zone `UTC`, `GMT`, `UT`,
  or `Z`, optionally followed by an offset, or just an offset `+hh[:]mm`. Years in the legacy forms have four digits,
  a lower-case `z` is not a zone, and a number token has at most nine digits. **Anything else is not a date**; V8
  accepts more (two-digit years, a nonexistent day that rolls over, and so on), which is a documented deviation.
- A number given to a date parse is milliseconds (handled by the callers); the result is inside the years 1 to 9999 or
  the parse fails.

**Limits** (every loop or allocation of the unit; the caller charges steps):

- _`Compile`_: a specifier of at most 1,024 characters (`MaxSpecifierLength`; the longest real one is about 30), and at
  most 4,096 tokens after the locale directives are expanded (`MaxTokenCount`; one `%c` is 13 tokens, so 316 of them
  exceed it), otherwise `TimeFormatSpecifierException`, checked before the scan and at every token; linear in the
  specifier length, producing a list of tokens; the caller charges the specifier length and keeps the
  `TimeFormatter`.
- _`Format`_: output of at most `maxLength` characters, then `TimeFormatTooLongException`; the caller charges the
  output length (the expression functions through `ChargeSize`, and the axis and legend labels, which also keep a
  running total per guide, see _TimeLabels_).
- _`Multi`_: linear in the total length of the specifiers (nine compiles, each within the limits of `Compile`); the
  caller charges it.
- _`TryParse`_: text of at most 256 characters (`MaxParseTextLength`), longer text fails at once; linear in the text
  and the specifier, so one call is O(256).
- _`JsDateParser.TryParse`_: text of at most 128 characters (`MaxLength`), rejected before any scan, and the tokenizer
  stops at an unknown character, so a 1 MiB cell costs no more than 128 characters.

**Culture independence and determinism.** No call reads `CultureInfo`, `DateTime.Parse`, `ToLower`, `ToUpper`, or a
regular expression (a grep gate over the unit's sources is empty); the tests run the same inputs under `tr-TR` and
`de-DE` (Turkish `I` and German digits and names do not change a result) and compare repeated and concurrent calls.

#### Key Methods

- `TimeFormat.Compile(specifier)` - the d3 `timeFormat(specifier)` as a `TimeFormatter`; never fails for text of at most
  1,024 characters and 4,096 expanded tokens (`TimeFormatSpecifierException` beyond that).
- `TimeFormatter.Format(ms, calendar, maxLength)` - the text of an instant in a calendar (`timeFormat` for a zone,
  `utcFormat` for the UTC calendar); `TimeFormatTooLongException` when the text would exceed `maxLength`.
- `TimeFormat.Multi(calendar, overrides)` - Vega's `timeMultiFormat` (`utcMultiFormat` for the UTC calendar).
- `TimeFormat.TryParse(specifier, text, calendar, out ms)` - d3's `timeParse` and `utcParse` in one method (the
  calendar selects the flavor); false and NaN on any failure.
- `JsDateParser.TryParse(text, calendar, out ms)` - `Date.parse` with the ISO format and the documented legacy
  subset; its `Func<string, double>` form is the delegate that `DateTimeObject.FromScalar` plugs in (see
  _TimeCalendar_).

**Preconditions and postconditions**: a specifier and a text are non-null strings; every method is deterministic, never
depends on the current culture, and holds no state.

#### Error Handling

`Format` throws `TimeFormatTooLongException` when the output would be longer than the limit, and `Compile` (and so
`Multi`) throws `TimeFormatSpecifierException` (an `ArgumentException`) for a specifier that is longer than 1,024
characters or expands to more than 4,096 tokens. Callers map the first to a located `VegaLiteRenderException` that
contains `the limit of` (axis and legend labels), or to an `ExpressionRuntimeException` (the expression functions), and
the second to a located `VegaLiteSpecException` (the guides) or an `ExpressionRuntimeException` (the expression
functions; the cache of the evaluation compiles through `ExpressionFunctionContext`), so neither reaches a public
caller. `TryParse` and
`JsDateParser.TryParse` never throw: any text and any specifier give false and NaN when they do not match or when the
result is outside the valid range. An unknown directive is not an error (it is copied), and `Compile` accepts any
text within the limits. A null argument is an `ArgumentNullException`. The unit performs no I/O.

**Documented deviations from d3-time-format, Vega, and JavaScript** (each pinned by a test):

1. **`Invalid Date` and the range** (P8-D2): an instant outside the years 1 to 9999 or NaN formats as `Invalid Date`
   and a parse outside the range fails.
2. **`%Z` shows whole minutes** of the offset.
3. **Hand scanner with bounded text** (256 characters for `TryParse`, 128 for `JsDateParser`), where d3 and
   JavaScript have no bound.
4. **`Date.parse` is a subset** (P8-D4): the ECMAScript format and the documented legacy forms; nonexistent days are
   rejected and two-digit years are not accepted in the legacy forms.
5. **A multi-format `TimeFormatter` is bound to its calendar**; the calendar argument of `Format` is ignored for it.

#### Dependencies

- **`TimeCalendar`** and **`DateParts`** - the wall-clock parts of an instant, `FromParts`, the intervals of the
  multi-format, and the civil arithmetic helpers (the `TimeCalendar` unit)
- **`DataValue`** - the valid date range, through `TimeCalendar.IsValid` (the `DataTable` unit)
- **.NET base class library** - `System.Text` (`StringBuilder`) and `System.Globalization` (invariant-culture digits
  only)

#### Callers

`ParseDirectives` (see _DelimitedTextParser_) calls `JsDateParser.TryParse` for `parse: "date"`, for auto inference, and
for the implicit date fields, and `TimeFormat.TryParse` for `date:'%fmt'` and `utc:'%fmt'`. `ExpressionFunctions` (the
date and time group) call `Compile`, `Format`, `TryParse`, and `Multi`. `TimeLabels`, `AxisBuilder`, and `LegendBuilder`
(see _TimeLabels_) format the tick and legend labels with the multi-format, the format of the guide, or the specifier of
a time unit (`TimeUnitSpec.Specifier`, see _TimeCalendar_). `DateTimeObject.FromScalar` and `ScaleResolver` read date
text with `JsDateParser`. The tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/` (`TimeFormatTests.cs`,
`TimeFormatParseTests.cs`, `TimeFormatMultiTests.cs`, and `JsDateParserTests.cs`) call the types directly through
`InternalsVisibleTo`.
