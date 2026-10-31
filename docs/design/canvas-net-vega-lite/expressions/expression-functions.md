### ExpressionFunctions

![Expressions Structure](VegaLiteExpressionsView.svg)

The `ExpressionFunctions` unit is the third software unit in the `Expressions` subsystem. It
comprises seven source files in
the `DemaConsulting.CanvasNet.VegaLite.Expressions` namespace, all `internal`:

- `ExpressionFunctions.cs` - `ExpressionFunctions` (static partial class): the frozen function table, `Names`, `TryGet`,
  `Invoke`, and the math functions
- `ExpressionFunctions.Text.cs` - the string and array functions
- `ExpressionFunctions.Types.cs` - the type-checking and conversion functions (including `toDate`)
- `ExpressionFunctions.Regex.cs` - `regexp`, `test`, and the regular expression forms of `replace` and `split`
- `ExpressionFunctions.Time.cs` - the date and time functions (`AddTime`)
- `ExpressionFunctionContext.cs` - the `ExpressionFunction` delegate and `ExpressionFunctionContext`, the per-evaluation
  state
- `ExpressionRegex.cs` - `ExpressionRegex`, the translation of a JavaScript regular expression into a safe .NET one

#### Purpose

`ExpressionFunctions` is the closed table of the pure functions that an expression may call, with the per-evaluation
context that bounds their work. It exists so that the security boundary of the expression language is one frozen list: a
function is callable only if it is in this table, and every function in it is deterministic,
culture-invariant, free of side
effects, and bounded in time and memory. Number formatting, color, geographic, scale, statistical, and data-flow
functions are not in the table (they are reported by the `FeatureCatalog` and evaluate to null), and there is no
`random` function and no `now` function (the clock is not an input of a pure expression; `now` is reported as
`vl-expr-function-now` and evaluates to null). The date and time functions are in the table; they compute with the
calendar of the evaluation (see _Time Calendar Design_ and _Time Format Design_), never with the host's zone, clock, or
culture.

The unit's behavior is specified by the `CanvasNetVegaLite-Expressions-ExpressionFunctions-*` requirements in
`docs/reqstream/canvas-net-vega-lite/expressions/expression-functions.yaml`.

#### Data Model

**The table** is a `FrozenDictionary<string, ExpressionFunction>` with ordinal, case-sensitive names, built once. An
`ExpressionFunction` is a delegate
`(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context) -> DataValue`; an
argument that is not supplied is simply missing (JavaScript's `undefined`: NaN as a number, the
text `undefined` as text), and
an optional argument that is null counts as not supplied. The 99 functions are:

- **Math (27)**: `abs`, `acos`, `acosh`, `asin`, `asinh`, `atan`, `atan2`, `atanh`, `ceil`, `clamp`, `cos`, `cosh`, `exp`,
  `floor`, `log`, `log10`, `max`, `min`, `pow`, `round`, `sign`, `sin`, `sinh`, `sqrt`, `tan`,
  `tanh`, and `trunc`. They use
  the host math library (results for the transcendental functions can differ from a browser in the last bit); `round` is
  JavaScript's half-up rounding, `max` and `min` take any number of arguments, `sign` keeps NaN and zero, and negative
  zero is preserved.
- **Text and array (15)**: `lower`, `upper`, `trim`, `substring`, `slice`, `indexof`, `lastindexof`, `startswith`,
  `endswith`, `includes`, `replace`, `split`, `join`, `pad`, and `length`. Strings are UTF-16 code unit sequences as in
  JavaScript (indices, lengths, and slices count code units); comparison is ordinal and invariant; `lower` and `upper`
  apply JavaScript's full case mappings on top of the invariant culture of .NET (the sharp s becomes `SS`, the Latin and
  Armenian ligatures, the letters with a combining mark and the Greek letters with an iota subscript become several
  characters, the capital I with a dot above lowers to `i` and a combining dot, the dotless i maps to upper case `I`,
  and a capital sigma lowers to the final sigma at the end of a word) and their result is checked against `MaxStringLength`;
  `slice`, `indexof`, `lastindexof`, `includes`, and `length` accept a string or an array; `pad` follows Vega (the align
  `left` pads the start, `center` both sides, the default the end); `join` gives null for a non-array.
- **Type and conversion (14)**: `isArray`, `isBoolean`, `isDate`, `isFinite`, `isNaN`, `isNumber`, `isObject`, `isRegExp`,
  `isString`, `isValid`, `toBoolean`, `toDate`, `toNumber`, and `toString`. `isNaN` and `isFinite` do not coerce (as
  `Number.isNaN` and `Number.isFinite`), `isObject` is true for objects, arrays, dates, and regular expressions, `isValid`
  rejects null and NaN, and the four conversions give null for null and for the empty text. `toDate` reads its argument
  as an instant (see below) and gives a date, or null when the argument is not a valid instant.
- **Regular expressions (2 more names, plus the forms of `replace` and `split`)**: `regexp` and `test`.
- **Date and time (41)**: the constructors `datetime`, `utc`, and `time`, and `timezoneoffset`; the getters `date`, `day`,
  `year`, `month`, `hours`, `minutes`, `seconds`, `milliseconds`, `quarter`, `week`, `isoweek`, and `dayofyear`, each with
  its UTC form (`utcdate`, `utcday`, ..., `utcdayofyear`; 24 names, registered from one list); `timeOffset`,
  `utcOffset`, `timeSequence`, and `utcSequence`; `timeUnitSpecifier`; `timeFormat`, `utcFormat`, `timeParse`, and
  `utcParse`; and the name formatters `monthFormat`, `monthAbbrevFormat`, `dayFormat`, and `dayAbbrevFormat`. With
  `toDate` these are the 42 functions that Phase 8 added. `now` is deliberately not among them.

**Date and time rules.** A date value is an instant in whole milliseconds since the Unix epoch (`DataValue.FromDate`),
valid in the years 1 to 9999 (`TimeCalendar.IsValid`); where JavaScript gives an `Invalid Date` a function gives null for
a date result and NaN for a number result, and it never throws for a bad value.

- **The instant argument rule.** Every function that takes a date reads it with one rule (`Instant`): a date is
  itself, a
  number is truncated to whole milliseconds (NaN outside the years 1 to 9999), a text is read by `JsDateParser` in the
  calendar of the evaluation (at most 128 characters are looked at, and the work is charged to the context), and null, a
  boolean, an array, and an object are **not** instants (JavaScript would read null as the epoch, which would hide a
  missing value). `timeFormat` and `utcFormat` are the exception, as in d3: they read the value as a number, so a date
  is its milliseconds, and a text that is not a number is an invalid date; a null value formats to the text `null`.
- **Constructors.** `datetime()` without arguments is null (there is no clock), with one argument it is that instant, and
  with two or more it is the local wall time in the calendar of the evaluation through `TimeCalendar.FromParts`
  (month from 0, day from 1; every component rolls over as in a JavaScript date; a year from 0 to 99 means 1900 to
  1999). `utc(...)` is the same in UTC and returns the **number** of milliseconds (the month defaults to 0), `time(x)`
  is the instant of its argument as a number.
- **Getters.** The local form reads the part of the instant in the calendar of the evaluation and the UTC form in
  `TimeCalendar.Utc`. `quarter` is 1 plus the month divided by three, `week` is the number of Sundays since the first of
  January (0 until the first Sunday, the Vega `week`), `isoweek` is the standard ISO 8601 week (1 to 53), `dayofyear`
  counts from 1, and `timezoneoffset` is the minutes
  to add to the local time to get UTC (positive west of Greenwich, never negative zero).
- **Offsets and sequences.** The unit name is looked up as a `TimeInterval` name (`year`, `quarter`, `month`, `week`,
  `date`, `day`, `dayofyear`, `hours`, `minutes`, `seconds`, `milliseconds`, in lower case; the singular d3 names are
  accepted; the `utc` prefix and `isoweek` are not, because the UTC form of the function is the way to ask for UTC); an
  unknown name gives null, and a quarter is every third month. `timeOffset(unit, date, step)` moves the date by whole
  units (step 1 when missing), `timeSequence(unit, start, stop, step)` gives the dates from the first unit boundary at or
  after the start up to, excluding, the stop. A sequence is capped by `MaxArrayLength` (the limit of a scale domain) and
  by the steps that remain in the budget, charges one step per date it produces, polls the cancellation token while it
  builds, and fails with a `ExpressionRuntimeException` that names `VegaLiteLimits.MaxScaleDomainSize` (or the step
  budget, when that was the tighter bound).
- **Unit specifier.** `timeUnitSpecifier(units, specifiers)` gives the d3 format that Vega uses for a unit or an array of
  at most 12 units, optionally with an object of replacement specifiers (only its text members count); it delegates to
  `TimeUnitSpec.TryGetUnitSpecifier` and gives null for invalid or duplicated units instead of throwing.
- **Format and parse.** `timeFormat(value, specifier)` with a text specifier uses a compiled `TimeFormatter` from the
  context cache; with an object it builds the nine-level `TimeFormat.Multi` formatter whose members are the overrides.
  The specifier length and the length of the result are charged, and the result is bounded by `MaxStringLength`
  (`TimeFormatTooLongException` becomes the string-limit failure). A specifier (and each member of an object of
  overrides) of more than 1,024 characters, or one that expands to more than 4,096 tokens (`TimeFormat.Compile`
  throws `TimeFormatSpecifierException`), is an `ExpressionRuntimeException` ("The time format is longer than the limit
  of 1024 characters" or "... expands to more than the limit of 4096 pieces"), raised by
  `ExpressionFunctionContext.CompileTimeFormat` and `CompileMultiFormat`; `timeParse` and `utcParse` refuse a specifier of
  more than 1,024 characters the same way. `timeParse(text, specifier)` returns a date or null
  when the text does not match the whole specifier; its fields are not range-checked (as d3), and it charges the lengths
  of the text and of the specifier. The name formatters take a whole number (month from 0, weekday from Sunday = 0;
  other whole numbers roll over) and give the empty text for anything else.

**`ExpressionFunctionContext`** is the per-evaluation state: the step counter and budget (`MaxEvaluationSteps`), the
string limit (`MaxStringLength`), `MaxArrayLength` (the most elements a function may build into one array, which is the
`MaxScaleDomainSize` limit and bounds the date sequences), `Calendar` (the `TimeCalendar` of the date and time
functions: UTC unless the render was given a time zone, and never the host's zone), `CancellationToken` (for functions
that run their own loops, such as `timeSequence`), `Step()`
(one step per instruction; the step that exceeds the budget is the reported one), `ChargeSize(n)` (one extra step for every
64 characters or elements of size-proportional work, such as searching (an array search by `indexof`, `lastindexof`, or
`includes` also charges the shorter length of every pair of texts it compares), splitting, replacing (a regular-expression
`replace` also charges the length of the replacement for every match, and checks the length of the result at every
replacement pattern), matching, padding, joining, formatting and parsing dates, and
materializing a datum), `ChargeSteps(n)` (whole steps, for work that costs one step per element it produces, such as the
dates of a sequence; `ChargeSize` calls it; a charge that does not fit in the budget leaves the counter past the budget
and fails), `ThrowIfCanceled()`, `CheckStringLength` (`MaxStringLength`), `StringLimitExceeded()` (the one exception
for a string longer than the limit, shared by `CheckStringLength` and the time format functions),
`GetRegex(source, flags)` with a
cache of at most 64 compiled expressions that is cleared when full, and `GetTimeFormatter(specifier)` with the same
kind of cache (at most 64 compiled `TimeFormatter` objects by specifier, cleared when full), bounded by size as well as
by count: only a specifier of at most 64 characters (`MaxCachedSpecifierLength`) is kept, so the cache holds a few
thousand characters at most and a long specifier is compiled for its call and dropped, so a specifier that a
chart formats thousands of times is compiled once. The cancellation token is polled whenever the step
counter crosses a multiple of 1,024, including inside a bulk charge.

**`ExpressionRegex`** is an immutable, thread-safe pair of the JavaScript source and flags and the .NET `Regex` that
implements them. The accepted flags are `g`, `i`, `m`, and `s` (a repeated or unknown flag is an error, and so are `u`,
`v`, `y`, and `d`, which the UTF-16 engine cannot honor, so no pattern silently matches differently from JavaScript; `g`
makes `replace` replace every match). The pattern is at most 1,024 characters. The translation makes the common cases
match JavaScript: `\d`, `\D`, `\w`, and `\W` are the ASCII classes (also inside a character class, negated or not),
`\s` and `\S` are the JavaScript white space, `$` without the `m` flag matches only at the end of
the text, and `[^]` and `[]` have their JavaScript meaning. The `Regex` is built with `RegexOptions.NonBacktracking |
CultureInvariant`, which matches in time linear in the input, with a 5 second timeout as a backstop; a pattern that
engine rejects (look-around, back-references, atomic groups) falls back to the backtracking engine with a **100
millisecond timeout that fails closed**. Known deviations: the numbering of mixed named and unnamed groups follows .NET,
and `.` and `\s` follow the .NET classes.

#### Key Methods

- `Names()` - the 99 names in ordinal order; `TryGet(name, out function)` - a case-sensitive lookup used by the parser to
  decide whether a call is supported; `Invoke(name, arguments, context)` - calls a function (a name that is not in the table
  is an `ArgumentException`, a programming error because the parser never compiles such a call).
- Each family registers its functions in one method (`AddMath`, `AddText`, `AddTypes`,
  `AddRegex`, `AddTime`); `BuildTable` freezes the
  result. The helpers convert arguments the JavaScript way (`Num`, `Text`, an integer-or-infinity conversion, a position
  clamp, and a position that counts from the end) and check results against `MaxStringLength` before a string is built.
- `Instant`, `Components`, `OffsetDate`, `Sequence`, `FormatTime`, and `ParseTime` (private, in the time file) - the
  instant rule, the shared reading of the `datetime` and `utc` components, and the offset, sequence, format, and parse
  functions described above; `WeekNumber` and `IsoWeekNumber` compute the week numbers by civil arithmetic.
- `replace(string, pattern, replacement)` replaces the first occurrence of a text pattern or the matches of a regular
  expression, with JavaScript's replacement patterns (`$&`, `$1`, `$<name>`, `$$`, `` $` ``, and `$'`); `split(string,
  separator[, limit])` splits at a text or a regular expression with JavaScript's results for the empty inputs.
- `ExpressionRegex.Create(source, flags)` - validates the length and flags, translates the pattern, and chooses the engine.

**Preconditions and postconditions**: the arguments are values the evaluator produced; on return the result is a value and
the context has been charged for the work, or an exception was thrown.

#### Error Handling

A function signals a failure with `ExpressionRuntimeException`, which the `ExpressionEvaluator` converts into a located
`VegaLiteRenderException`: the step budget is exhausted, a result string would be longer than `MaxStringLength`, a regular
expression pattern is longer than 1,024 characters, invalid, or has an unknown or repeated flag, and a backtracking match
exceeds its 100 millisecond timeout (a pattern that would take exponential time fails closed instead of hanging), a date
sequence would hold more dates than `MaxArrayLength` allows or more than the remaining steps pay for, and a formatted
date would be longer than `MaxStringLength`. A canceled
token is an `OperationCanceledException`. A wrong type of argument is never an error: JavaScript coercion gives a value
(often NaN, null, or false), and a date function given something that is not an instant gives null (a date) or NaN (a
number); a `TimeRangeLimitException` and a `TimeFormatTooLongException` of the `Data` subsystem never escape, they become
`ExpressionRuntimeException`s. An unknown function name passed to `Invoke` is an `ArgumentException`.

#### Dependencies

- **`DataValue`**, **`DataValueKind`**, **`DataObject`**, and **`JsNumber`** - the value model
  of the `DataTable` unit of the
  `Data` subsystem
- **`ExpressionEvaluator`** - the coercion helpers (`ToNumber`, `ToJsString`, `IsTruthy`, `StrictEquals`, `NumbersEqual`)
  that the functions share so that a function and an operator agree
- **`VegaLiteLimits`** - the public limits (`MaxStringLength`, `MaxEvaluationSteps`, `MaxScaleDomainSize`)
- **`TimeCalendar`**, **`TimeInterval`**, **`TimeFormat`**, **`TimeFormatter`**, **`TimeUnitSpec`**, and
  **`JsDateParser`** - the time model of the `Data` subsystem (see _Time Calendar Design_ and _Time Format Design_)
- **.NET base class library** - `System.Collections.Frozen`, `System.Text.RegularExpressions`, `System.Math`, and
  `System.Globalization` (invariant culture)

#### Callers

`ExpressionParser` calls `TryGet` to decide whether a call is supported and stores the function in the program;
`ExpressionEvaluator` invokes the function delegates of a program and owns the `ExpressionFunctionContext`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionFunctionsTests.cs` and `ExpressionFunctionsTimeTests.cs`
call `Names`, `Invoke`, and the context
directly through `InternalsVisibleTo`.
