### ExpressionFunctions Unit Verification Design

This document describes the unit-level verification strategy for the `ExpressionFunctions` unit in the
`DemaConsulting.CanvasNet.VegaLite.Expressions` namespace: the internal static `ExpressionFunctions` class (the closed
function table with `Names`, `TryGet`, and `Invoke`, split over its math, text, type, regular-expression, and time files),
the `ExpressionFunction` delegate and `ExpressionFunctionContext` (the per-evaluation context that counts steps, charges
size-proportional work, checks cancellation and the string limit, carries the calendar of the date and time functions,
and caches compiled regular expressions and time formatters), and `ExpressionRegex` (the translation of a JavaScript
regular expression into a safe .NET regular expression, with its flags and its source text). The table holds exactly
the 99 whitelisted functions of the supported subset:

- **Math (27)**: `abs`, `acos`, `acosh`, `asin`, `asinh`, `atan`, `atan2`, `atanh`, `ceil`, `clamp`, `cos`, `cosh`,
  `exp`, `floor`, `log`, `log10`, `max`, `min`, `pow`, `round`, `sign`, `sin`, `sinh`, `sqrt`, `tan`, `tanh`, `trunc`
- **Text and sequence (15)**: `lower`, `upper`, `trim`, `substring`, `slice`, `indexof`, `lastindexof`, `startswith`,
  `endswith`, `includes`, `replace`, `split`, `join`, `pad`, `length`
- **Type and conversion (14)**: `isArray`, `isBoolean`, `isDate`, `isFinite`, `isNaN`, `isNumber`, `isObject`,
  `isRegExp`, `isString`, `isValid`, `toBoolean`, `toDate`, `toNumber`, `toString`
- **Regular expression (2)**: `regexp`, `test`
- **Date and time (41)**: `datetime`, `utc`, `time`, `timezoneoffset`; the getters `date`, `day`, `year`, `month`,
  `hours`, `minutes`, `seconds`, `milliseconds`, `quarter`, `week`, `isoweek`, `dayofyear` and their UTC forms
  (`utcdate` to `utcdayofyear`, 24 names); `timeOffset`, `utcOffset`, `timeSequence`, `utcSequence`;
  `timeUnitSpecifier`; `timeFormat`, `utcFormat`, `timeParse`, `utcParse`; and `monthFormat`, `monthAbbrevFormat`,
  `dayFormat`, `dayAbbrevFormat`

Number format, color, scale, and random functions are not in the table, and neither is `now` (the clock is not an input
of a pure expression): a call to them is parsed, reported by the feature catalog, and evaluates to null. The date and
time functions compute in the calendar of the evaluation (a `TimeCalendar`: UTC unless the render was given a time
zone) and never in the host's zone, clock, or culture; the calendar, the d3 time format, and the unit specifier that
they build on are verified in their own units (see _Time Calendar Verification_ and _Time Format Verification_).

#### Verification Approach

The unit is verified by two means. Most scenarios compile an expression text that calls the function with the real
`ExpressionParser` and evaluate it with the real `ExpressionEvaluator` (through the `ExpressionTestSupport` helpers),
because the function table is reached only through an evaluator in the product and the observable contract is the
value; a few scenarios call `ExpressionFunctions.Invoke` and `ExpressionFunctionContext` directly to prove the table,
the argument checks, the steps, and the regular-expression cache. No mocking or stubbing is used and the internal types
are reached through the test assembly's `InternalsVisibleTo` access.

The date and time scenarios of `ExpressionFunctionsTimeTests` evaluate through an evaluator that is given a calendar
that the test builds with `TimeTestSupport` (UTC, a fixed offset such as +05:30, or a US-like zone whose daylight time
begins at 07:00Z on 2024-03-10 and ends at 06:00Z on 2024-11-03), so that no scenario reads the host's zone, clock, or
culture. Their expected values are derived by hand, not by running JavaScript. The anchor instant is
`datetime(2024, 1, 29, 12, 34, 56, 789)` (the month counts from 0, so index 1 is February: 2024-02-29T12:34:56.789Z).
Its milliseconds: 2024-01-01 is day 19,723 since 1970 (54 x 365 = 19,710 plus 13 leap days, 1972 to 2020 and none
yet in 2024), February 29 is the 60th day of the year (31 + 29), so the day number is 19,723 + 59 = 19,782 and
19,782 x 86,400 s = 1,709,164,800 s; the time of day is 12 x 3,600 + 34 x 60 + 56 = 45,296 s; the total is
1,709,210,096 s, that is 1,709,210,096,789 ms with the 789 ms added. The day of the week follows from the day number:
19,782 = 2,826 x 7, and day 0 (1970-01-01) was a Thursday, so the anchor is a Thursday (`day` 4). The week functions
are derived from the calendar: 2024-01-01 is a Monday (19,723 mod 7 = 4, four days after Thursday), the Sundays
before February 29 are January 7, 14, 21, 28 and February 4, 11, 18, 25 (eight, so the Vega `week` is 8), and the ISO
week, which starts on Monday January 1, is 1 + (60 - 1) div 7 = 9. A second anchor is 2024-03-05T13:04:09.007Z,
1,709,643,849,007 ms (day 19,787 = 19,782 + 5 days since February 29, x 86,400 = 1,709,596,800 s, plus 47,049 s of
the time of day). The date text and the format and parse scenarios use the same arithmetic. Where a result depends on
the zone, the offset is added or subtracted by hand (12:34 UTC is 18:04 at +05:30), and where it depends on the
daylight-saving rule the instants around the change are written out (noon of March 9 at -5 h is 17:00Z, noon of March 10
at -4 h is 16:00Z, so the local day is 23 hours long).

The expected values of the other functions are written by hand from the JavaScript rules and the public Vega expression
documentation, never produced by running a JavaScript engine: `round(2.5)` is `3` and `round(-2.5)` is `-2` (half
toward positive infinity), `max()` is `-Infinity` and `min()` is `Infinity`, `pow(1, Infinity)` is `NaN` while
`pow(NaN, 0)` is `1`,
`substring('hello', 3, 1)` is `el` (swapped), `slice('hello', -3)` is `llo`, `pad('ab', 5, '*', 'center')` is `*ab**`,
`pad('7', 3, '0', 'left')` is `007` (as Vega, `left` pads the start), `isNaN('x')` is false (no coercion, as
`Number.isNaN`), `toNumber('')` is null, `toString(1e21)` is `1e+21`, and `replace('abc', /(a)(b)(c)/, '$3$2$1$0')` is
`cba$0`. The transcendental functions are compared with reference values to a tolerance, because the host math library
may differ from a browser in the last bit; every exactly representable point (`cos(0)`, `log10(100)`, `sqrt(16)`) is
compared exactly.

The regular-expression scenarios prove three properties. The translation is correct for the supported subset (the
classes `\d \D \w \W` (also inside a character class) are ASCII only, `\s \S` are the JavaScript white space, `$`
without the `m` flag matches only at the end of the text, `[^]` matches any
character and `[]` nothing, the flags `g i m s` are honored, and look-ahead and back-references work through the
backtracking fallback). The engine is safe: the linear-time `NonBacktracking` engine runs every pattern it can, so
catastrophic patterns finish on 50,000 characters, and a pattern it rejects (look-around or a back-reference) runs on
the backtracking engine with a 100 ms limit that fails closed with a located error; a pattern is at most 1,024
characters. And the failures are located: an invalid pattern or flag is a `VegaLiteRenderException` at the expression
with the offset of the failing call.

Unit tests reside in `ExpressionFunctionsTests.cs` and `ExpressionFunctionsTimeTests.cs` within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Expressions` folder, with the shared helpers in
`ExpressionTestSupport.cs` and `TimeTestSupport.cs` (the latter in the `Data` folder); the unit specifier scenario is
in `TimeUnitSpecifierTests.cs` in the `Data` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test compiles and evaluates its own texts with its own evaluator or context; no files are read.
  No assertion measures time: the catastrophic-pattern scenarios assert only that the call finishes with the right
  answer, and the one limit scenario relies on a pattern that takes exponential time with backtracking, which is far
  slower than the 100 ms limit that stops it

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any function missing from or added to the exact whitelist, a wrong value or text, a wrong sign of zero, a
result that depends on the culture, the host's zone, or a clock, a wrong date or week number, a sequence that is not
bounded, a pattern that does not finish, an exception of the wrong type, a failure without
the location of the expression and the offset, or a string result over `MaxStringLength` that is not refused
constitutes a failure.

#### Unit-Level Test Scenarios

##### The Function Table Is Exactly The Whitelist

**Tests**: `ExpressionFunctionsTests.ExpressionFunctions_Names_AreExactlyTheWhitelist`,
`ExpressionFunctionsTests.ExpressionFunctions_Invoke_UnknownName_ThrowsArgumentException`

Asserts that `Names` returns exactly the 99 names above in ordinal order (the 57 names of the earlier phases, the 41
date and time names, and `toDate`), that `random`, `now`, `format`, `rgb`, `scale`, `if`, `eval`, and `Function` are
absent and not found by `TryGet`, that
names are case sensitive (`ABS` and `isNan` are not found), that invoking an unknown name is an `ArgumentException`, a
null context is an `ArgumentNullException`, and `abs(-3)` called directly gives 3.

##### Math Functions Are Exact At Representable Points

**Tests**: `ExpressionFunctionsTests.ExpressionFunctions_Math_ExactValuesAndSpecialCases`,
`ExpressionFunctionsTests.ExpressionFunctions_Math_KeepsNegativeZero`,
`ExpressionFunctionsTests.ExpressionFunctions_Math_TranscendentalFunctionsMatchReferenceValues`

Asserts, as a theory of fifty-eight texts, the exact values and the special cases of every math function (missing and
null arguments, text arguments converted as numbers, `clamp` in range and outside it and with `NaN`, `log(0)` and
`log(-1)`, the argument-less `max` and `min`, `NaN` propagation in `max` and `min`, the special cases of `pow`, the
rounding of halves, `sign`, `sqrt(-1)`, and `trunc`); that `round(-0.4)`, `ceil(-0.5)`, `sign(-0)`, and
`min(-0, 0)` keep negative zero (a reciprocal gives `-Infinity`) and `max(-0, 0)` gives positive zero; and, as a theory
of nineteen texts, that the transcendental functions (`sin(PI / 2)` is 1, `atan2(1, 1)` is 0.7853981633974483,
`exp(1)` is 2.718281828459045, `sqrt(2)`, and the hyperbolic functions and their inverses) are within a small tolerance
of their reference values.

##### Text Functions Follow JavaScript And Vega

**Tests**: `ExpressionFunctionsTests.ExpressionFunctions_Text_FollowJavaScriptAndVega`

Asserts, as a theory of fifty-three texts evaluated twice by one evaluator, `lower`, `upper` (also of null and a
number), `trim` (line breaks, a no-break space, and a byte-order mark), `substring` (swapped, negative, past-the-end,
and `NaN` arguments), `slice` (negative and out-of-range positions), `indexof` and `lastindexof` (also of a number and
the empty search text), `startswith` and `endswith` with a position, `includes`, `join` (null elements, the default
separator `,`, nested arrays, and a non-array giving null), `pad` (left, right, center, a multi-character fill that is
cut, an empty fill giving spaces, and a text that is already long enough), and `length` (text and arrays; a number and
an object give null).

##### Case Conversion Follows JavaScript

**Tests**: `ExpressionFunctionsTests.ExpressionFunctions_Upper_AppliesFullCaseMappings`,
`ExpressionFunctionsTests.ExpressionFunctions_Lower_AppliesFullCaseMappingsAndFinalSigma`,
`ExpressionFunctionsTests.ExpressionFunctions_CaseConversion_IsCultureInvariantAndBounded`

Asserts, as theories, that `upper` applies the full mappings of JavaScript that make longer text (the sharp s gives
`SS`, a word with it gains a letter, the ligatures `ﬀ` to `ﬆ` and the Armenian `ﬓ`, the letters with a combining mark, the
Greek letters with an iota subscript, and the dotless i giving `I`) and the plain letters and the empty text unchanged;
that `lower` gives `i` and a combining dot for the capital I with a dot above and the final sigma exactly where
JavaScript does (after a letter and not before one, ignoring a soft hyphen and the punctuation inside a word); that the
results are the same under the Turkish (set on a dedicated worker thread) and the invariant culture; and that a result
that is longer than its input is
refused when it passes `MaxStringLength` (two sharp s give four characters under a limit of 4, three give six).

##### Arrays: Slice, Search, And Membership

**Tests**: `ExpressionFunctionsTests.ExpressionFunctions_Array_SliceSearchAndMembership`

Asserts that `slice` of an array gives a new array (`2,3`; a negative start; an empty result for reversed positions;
an array for a start past the end), that `indexof` and `lastindexof` of an array use strict equality (`'1'` is not
found among numbers, `NaN` is never found, `-0` finds `0`), and that `includes` uses the SameValueZero rule (`NaN`
is found) and identity for composites (`[[1]]` does not include `[1]`).

##### Type Predicates And Conversions

**Tests**: `ExpressionFunctionsTests.ExpressionFunctions_Types_PredicatesFollowVega`,
`ExpressionFunctionsTests.ExpressionFunctions_Conversions_MapNullAndEmptyToNull`

Asserts, as a theory of thirty-three texts, that each predicate returns a boolean for every kind of value, including
that `isNaN` and `isFinite` do not coerce (`isFinite('1')` is false), that `isNumber` is true for `NaN` and `Infinity`,
that `isObject` is true for objects, arrays, and regular expressions and false for null and text, that `isValid` is
false only for null, `NaN`, and a missing argument; and that `toBoolean`, `toNumber`, and `toString` map null and the
empty text to null and otherwise convert as JavaScript does (`toBoolean('false')` and `toBoolean('0')` are false,
`toNumber(' 0x10 ')` is 16, `toNumber([])` is 0, `toNumber([-3])` is -3, `toString(1e21)` is `1e+21`,
`toString([1, 2])` is `1,2`, `toString({})` is `[object Object]`, and `toString(/a+/g)` is `/a+/g`).

##### Regular Expressions: Test And Flags

**Tests**: `ExpressionFunctionsTests.ExpressionFunctions_Regex_TestAndFlags`

Asserts, as a theory of twenty-six texts each also negated, that `test` and `regexp` honor the ASCII classes (`\d` does
not match an Arabic-Indic digit, `\w` does not match `é`), the end-of-text meaning of `$` and the effect of `m`, the
dot without and with `s`, case-insensitive matching, `[^]` and `[]`, a text pattern, `regexp` from a text and from a
literal with replaced flags, and the patterns that need the backtracking fallback (a look-ahead and a
back-reference).

##### Regular Expressions: Replace And Split

**Tests**: `ExpressionFunctionsTests.ExpressionFunctions_Replace_FollowsJavaScript`,
`ExpressionFunctionsTests.ExpressionFunctions_Split_FollowsJavaScript`,
`ExpressionFunctionsTests.ExpressionFunctions_Split_EmptyInputs`,
`ExpressionFunctionsTests.ExpressionFunctions_Split_EmptyRegexAndTextSplitBetweenCodeUnits`

Asserts, as theories of twenty-two and twenty-six texts, `replace` with a text pattern (first occurrence only) and with
regular expressions (first or all matches), the replacement patterns `$&`, `` $` ``, `$'`, `$$`, `$1` to `$9`, a
group that does not exist (kept as text), `$0` (kept as text), and named groups `$<name>`, an empty match replaced at
every position (`replace('abc', /x*/g, '-')` is `-a-b-c-`), and null as the text; `split` with a text separator, an
empty separator, a limit (zero, below, and above the number of parts), a missing or null separator, leading and
trailing empty parts, and regular-expression separators with and without capture groups, including the empty regular
expression and the patterns that match the empty text (`split('abc', /(?:)/)` and `split('abc', /x*/)` are `a`, `b`, `c`,
`split('abc', /()/)` keeps the empty captures, `split('aXbXc', /(x)|(b)/i)` keeps the groups that did not take part as null);
and the degenerate splits that have no printable form (`split('', ',')` has one empty part, `split('', '')` has none);
and that the empty regular expression and the empty text separator both split a surrogate pair into its two UTF-16 code
units.

##### Regular Expression Safety

**Tests**: `ExpressionFunctionsTests.ExpressionFunctions_Regex_CatastrophicPatternsAreSafe`,
`ExpressionFunctionsTests.ExpressionFunctions_Regex_BacktrackingFallbackFailsClosed`,
`ExpressionFunctionsTests.ExpressionFunctions_Regex_InvalidPatternIsLocatedRenderError`,
`ExpressionFunctionsTests.ExpressionFunctions_Regex_PatternLengthIsBounded`

Asserts that five classic catastrophic patterns (`(a+)+$`, `^(a+)+$`, `^(a|aa)+$`, `^(a|a)*$`, and `^(.*a){20}$`) tested
against 50,000 letters followed by a character that makes every one fail finish with the answer false; that
`/^(?=a)(a+)+$/` against forty letters and a bang, which forces the backtracking engine and is exponential there, fails
with a located `VegaLiteRenderException` that says the match "exceeded its match time limit" and names the offset; that
an unbalanced group and an unterminated class in a text pattern (through `regexp` and `test`), an unknown flag, a
repeated flag, the flags `u`, `v`, `y`, and `d` that the UTF-16 engine cannot honor (also in combination with a
supported one), and a regular expression built from `'*'` inside `replace` are located render errors with the offset of
the failing call (0 or 13); and that a pattern of 1,024 characters is accepted while one of 1,025 fails with "1025
characters, more than the limit of 1024".

##### String Results Are Bounded

**Tests**: `ExpressionFunctionsTests.ExpressionFunctions_StringResults_AreBoundedByMaxStringLength`,
`ExpressionFunctionsTests.ExpressionFunctionContext_StringLimitAndRegexCache_Work`

Asserts, with a `MaxStringLength` of 20, that `pad` to exactly 20 characters is accepted while 21, a billion, and
`Infinity` are located render errors, that `replace` whose result is exactly 20 characters is accepted and one more
fails, that `join` and `toString` of an array of two ten-character parts give exactly 20 characters but are refused
at 21, and, on the context directly, that a length of 5 passes a limit of 5 and 6 fails, that the same pattern and
flags give the same cached regular expression while other flags give another, that the flag text is normalized
(`ig` is `gi`), and that the cache holds many different patterns without failing.

##### Replace Charges The Replacement For Every Match

**Tests**: `ExpressionFunctionsTests.ExpressionFunctions_Replace_ChargesTheReplacementForEveryMatch`,
`ExpressionFunctionsTests.ExpressionFunctions_Replace_FailsEarlyWhenAReplacementPatternGrowsTheTextPastTheLimit`

Asserts that `replace` of a 2,000-character text by `/(x)?/g` (2,001 empty matches) with a 50,000-character
replacement of `$1` patterns that expand to nothing fails with the `MaxEvaluationSteps` error under a budget of 5,000
steps, while under a large budget it returns the text unchanged and has been charged at least 390 steps for each match;
and that a replacement of `` $` `` patterns fails with the `MaxStringLength` error as soon as the text passes the limit.

##### Steps, Charges, And Determinism

**Tests**: `ExpressionFunctionsTests.ExpressionFunctionContext_StepsAndCharges_AreExactAndCancelable`,
`ExpressionFunctionsTests.ExpressionFunctions_Repeated_ResultsAreIdentical`

Asserts, on a context with a budget of 10 steps, that nine single steps and a charge of 63 units of size (below one
step) leave 9 steps used, that a charge of 64 units is the tenth and last step, that the next step or charge throws the
budget exception, that a charge of `long.MaxValue` exhausts the budget without overflow, and that a charge that crosses
a multiple of 1,024 observes a cancellation; and that fifty evaluations of a combined text give the single result
`A+B+C003`, while `random()` is null every time because it is not in the table.

##### Date Constructors: datetime, utc, And time

**Tests**: `ExpressionFunctionsTimeTests.ExpressionFunctions_Datetime_Components_AreALocalDate`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Datetime_ComponentsOverflow_RollIntoTheNextUnit`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Datetime_YearsBelowOneHundred_AreNineteenHundreds`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Datetime_OneArgument_IsAnInstant`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Datetime_UsesTheCalendarZone`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Utc_ReturnsAMillisecondNumber`

Asserts that `datetime(2024, 1, 29, 12, 34, 56, 789)` is the anchor, 1,709,210,096,789 ms (derived above), that the
missing components default (`datetime(2024, 1)` is February 1; a day defaults to 1, the time to zero), that text and
fractional components are truncated like numbers, and that the components roll over as in the JavaScript `Date`
constructor: month 12 is January of the next year, month -1 is December of the year before, day 32 of January is
February 1, day 0 is the last day of the previous month (`datetime(2024, 0, 0)` is 2023-12-31), hour 24 is the next
day and hour -1 the previous one, and `datetime(2024, 1, 29, 12, 61, 61, 1001)` is 12:00 + 61 min = 13:01, + 61 s =
13:02:01, + 1,001 ms = 13:02:02.001 (with 36 hours instead of 12 it is March 1 at 13:02:02.001, because 36 hours
after midnight of February 29 is March 1 at 12:00). A year
from 0 to 99 means 1900 to 1999 while year 100 is year 100; the last valid millisecond `datetime(9999, 11, 31, 23, 59,
59, 999)` is 253,402,300,799,999 (2,932,897 days x 86,400,000 ms minus 1), and anything outside the years 1 to 9999 or
non-finite (`10000`, `-1`, `NaN`, `Infinity`, a text that is no number) gives null, never an exception. With one
argument a number is milliseconds (truncated), a date is copied, a text is read by the date parser in the calendar, and
null, a boolean, an array, the empty text, text that is no date, NaN, `1e300`, and `253402300800000` give null, as does
`datetime()` (there is no clock). In a zone, the components and a text without an offset are local: at +05:30 local
midnight of 2024-01-01 is 18:30Z of December 31 (24:00 minus 5 h 30 min), while `'2024-01-01'` alone and
`'2024-01-01T00:00:00Z'` are UTC; in the US-like zone the nonexistent 02:30 of 2024-03-10 is read with the offset from
before the change (-5 h, 07:30Z, which shows as 03:30 local) and the doubled 01:30 of 2024-11-03 takes the earlier
instant (daylight time, -4 h, 05:30Z). `utc(...)` gives the **number** of milliseconds in every zone (the anchor again
at +05:30), defaults the month to 0, reads `99` as 1999, rolls month 12 over, and gives NaN with no argument, for year
10000, and for text; `isDate` is false for it and true for `datetime`.

##### Date Getters And Their UTC Forms

**Tests**: `ExpressionFunctionsTimeTests.ExpressionFunctions_Getters_AtTheAnchorInstant`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Getters_BeforeTheEpochAndWithFractions`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Getters_InvalidInstants_AreNaN`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Getters_LocalFormsFollowTheZone_UtcFormsDoNot`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Timezoneoffset_IsMinutesWestOfUtc`

Asserts, as a theory of twenty-five texts at the anchor in UTC, `year` 2024, `month` 1, `date` 29, `day` 4 (Thursday, as
derived), `hours` 12, `minutes` 34, `seconds` 56, `milliseconds` 789, `time` 1,709,210,096,789, `quarter` 1, `week` 8,
`isoweek` 9, `dayofyear` 60 (31 + 29), `timezoneoffset` 0, and the same value for every UTC form. Before the epoch the
parts count back from the end of 1969 (-1 ms is 1969-12-31T23:59:59.999Z, a Wednesday: `year` 1969, `month` 11, `date`
31, `day` 3, `hours` 23, `milliseconds` 999), a fraction of a millisecond is truncated, `time(-0.5)` is 0, the ends of
the valid range are year 1 for -62,135,596,800,000 and 9999 for 253,402,300,799,999 and NaN one millisecond outside
either, and a value that is not an instant (no argument, null, NaN, a boolean, text that is no date, 1e300, a date
past year 9999) gives NaN while a date text is read (`year('2024-03-05')` is 2024). At 2024-02-29T20:00Z the local
getters in a zone at +05:30 show Friday 2024-03-01 at 01:30 (20:00 + 5 h 30 min = 25:30), so `month` is 2, `date` 1,
`day` 5, `hours` 1, `minutes` 30, and `dayofyear` 61, while the UTC getters give month 1, the 29th, Thursday, 20, 0,
and 60, and `time` does not depend on the zone. `timezoneoffset` is minutes west of UTC: -330 at +05:30, 180 at -03:00,
never negative zero for UTC, and in the US-like zone 300 in January, 240 in July, 300 at 06:59:59.999Z on 2024-03-10
and 240 at 07:00Z (the change instant).

##### Week, Quarter, Day Of Year, And ISO Week

**Tests**: `ExpressionFunctionsTimeTests.ExpressionFunctions_WeekQuarterAndDayOfYear_FollowVega`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Isoweek_AroundTheYearBoundaries`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_WeekFunctions_LocalAndUtcFormsDiffer`

Asserts the quarter of each month (1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, for both forms), the Vega `week` (the number of
Sundays since the first of January: 2012-01-01 is a Sunday so it is week 1, January 7 is still week 1 and January 8
is week 2; 2013-01-01 is a Tuesday, so it and January 2 and 5 are week 0 and Sunday January 6 is week 1; the Sundays
before 2024-03-05 are January 7, 14, 21, 28, February 4, 11, 18, 25 and March 3, nine, so it is 9), `dayofyear` (65 for
2024-03-05 = 31 + 29 + 5, 1 for January 1, 366 for December 31 of the leap year 2024 and 365 for 2023). The ISO week
is checked around the year boundaries: 2020 starts on a Wednesday, so it has 53 weeks and Sunday 2021-01-03 is week 53
while Monday 2021-01-04 is week 1; 2024-12-30 (a Monday) belongs to week 1 of 2025 and December 29 to week 52;
2022-01-01 (a Saturday) is week 52 of 2021; December 31, 2015 and 2016-01-03 are week 53 and 2016-01-04 is week 1
(2015 started on a Thursday); and 2024-03-05 is week 10 (day 65: 1 + 64 div 7 = 10). The local forms use the zone and the
UTC forms do not: 2024-03-03T23:00Z is Sunday in UTC (day of year 63, ISO week 9) and Monday March 4 at 04:30 at
+05:30 (day of year 64, ISO week 10), and 2024-03-31T23:00Z is in the first quarter in UTC and the second at +05:30.

##### Offsets By Calendar Units

**Tests**: `ExpressionFunctionsTimeTests.ExpressionFunctions_Offset_MovesByCalendarUnits`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Offset_UnknownUnitsAndDates_AreNull`

Asserts that `timeOffset` and `utcOffset` move a date by whole units: January 31 plus one month is "February 31",
which, as with `setMonth` in a leap year, is February 29 + 2 days = March 2; minus one month from January 15 is
December 15; one year from 2024-02-29T12:00 is 2025-03-01T12:00 (2025 has no February 29); hours, minutes, seconds,
and milliseconds add exactly (10:00 + 90 min is 11:30, 1,500 ms is 1.5 s); two weeks is 14 days; the unit `quarter` is
every third month (from February 15 the next step is the next quarter-start month, April 15); the default step is 1
and a fractional step rounds down (1.9 is 1, -0.5 is -1). A local day keeps the wall time across a daylight-saving
change: noon on 2024-03-09 in the US-like zone is 17:00Z (at -5 h) and one local day later is noon at -4 h, 16:00Z on
March 10 (23 hours later), `date` does the same, adding 24 hours is 17:00Z, and the UTC form adds 24 hours. A unit
that is not a Vega unit name (`fortnight`, a different case, the `utc`-prefixed names, `isoweek`, a number, null, a
name of 40 characters), a date that is not an instant, a NaN step, a result past year 9999, and a step of 1e300 give
null.

##### Date Sequences

**Tests**: `ExpressionFunctionsTimeTests.ExpressionFunctions_Sequence_ListsTheBoundaries`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Sequence_LocalDaysFollowTheZone`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Sequence_IsBoundedByTheScaleDomainLimit`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Sequence_OfAMillionMilliseconds_IsRejected`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Sequence_ChargesOneStepPerDate`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Sequence_ObservesCancellation`

Asserts that a sequence runs from the first boundary at or after the start up to but excluding the stop: January 1 to
January 4 by day has three dates (January 1, 2, and 3); a start at 06:00 begins at the next midnight (two dates); six
hourly steps give 00:00, 06:00, 12:00, and 18:00 (four); `date` by 2 over a week gives four, `dayofyear` seven, month
twelve, quarter four (the last on October 1), year four, milliseconds five, and minutes with a step of 1.9 (taken as 1,
so three); and the result is an empty array when the stop is not after the start, the step is below 1, or an argument
is not a date, null for an unknown unit, and an array otherwise. The local form follows the zone across a daylight
change: local midnights of March 9, 10, and 11 in the US-like zone are 05:00Z, 05:00Z (the change is at 02:00 local,
after midnight), and 04:00Z (-4 h), while the UTC form starts at the next UTC midnight. A sequence is bounded by the
`MaxScaleDomainSize` limit: with a limit of 10, ten days are allowed and eleven fail with an `ExpressionRuntimeException`
(through an evaluator a located `VegaLiteRenderException`) that names `VegaLiteLimits.MaxScaleDomainSize` and "the limit
of 10", and a million-millisecond request under the default limit is refused for the same limit long before a million
dates are built. A sequence costs one step per date (a sequence of 100 days is exactly 100 steps) and a budget of 500
steps refuses 800 days with the error that names `VegaLiteLimits.MaxEvaluationSteps`; a canceled token stops it with
an `OperationCanceledException`.

##### Unit Specifier

**Tests**: `ExpressionFunctionsTimeTests.ExpressionFunctions_TimeUnitSpecifier_UsesTheVegaDefaults`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_TimeUnitSpecifier_InvalidUnits_AreNull`,
`TimeUnitSpecifierTests.TimeUnitSpec_TryGetUnitSpecifier_ValidUnits_JoinTheDefaults`,
`TimeUnitSpecifierTests.TimeUnitSpec_TryGetUnitSpecifier_Overrides_ReplaceTheDefaults`,
`TimeUnitSpecifierTests.TimeUnitSpec_TryGetUnitSpecifier_InvalidUnits_AreRejected`,
`TimeUnitSpecifierTests.TimeUnitSpec_Specifier_KeepsTheVegaLiteOverrides`

Asserts that `timeUnitSpecifier` gives the d3 format that Vega uses for a unit or an array of units with the Vega
defaults (the Vega-Lite overrides are not applied): `['year', 'month']` is `%Y-%m` (also with `{}` or no second
argument and in the other order, because the units are put in Vega's order first), `year, month, date` is `%Y-%m-%d`,
`hours, minutes` is `%H:%M`, `month, date` is `%b %d`, `year, quarter` is `%Y Q%q`, `year, isoweek` is `%G W%V`,
`year, hours` is `%Y %H:00`, `seconds, milliseconds` is `:%S.%L`, and a single unit gives `%Y`, `.%L`, `W%U`, and
`%j`; an object of overrides replaces a default or a whole combination (`'year-month': '%B %Y'` gives `%B %Y`, and
`year: 'Y%Y '` with `week` gives `Y%Y W%U`), and a member that is not a text is ignored. Units that Vega rejects give
null and never throw: none, no argument, null, a number, an unknown or wrongly cased name, an array with a number,
`week` with `dayofyear`, `month` with `dayofyear`, `week` with `isoweek`, a repeated unit, and an array of thirteen
units. The unit-level theory `TimeUnitSpec.TryGetUnitSpecifier` repeats this table at the `Data` level (the specifier of
`quarter, month` is `Q%q %b`, `day, hours` is `%a %H:00`, and `year, dayofyear` is `%Y %j`), shows that an override of
a single unit joins with the other defaults (`month, date` with `date: '%e'` is `%b %e`), refuses the invalid lists
including `day, dayofyear` and `quarter, dayofyear`, and shows that the Vega-Lite specifier of the unit `yearmonth`
is still `%b %Y` while the Vega one is `%Y-%m`.

##### Time Format And Parse Functions

**Tests**: `ExpressionFunctionsTimeTests.ExpressionFunctions_TimeFormat_FormatsWithTheSpecifier`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_TimeFormat_ObjectSpecifier_IsAMultiFormat`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_TimeFormat_IsBoundedAndCharged`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_TimeFormat_SpecifierLimits_AreRuntimeErrors`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_TimeParse_ReadsTheSpecifier`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_TimeParse_ChargesTheTextLength`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_NameFormatters_GiveTheEnglishNames`

Asserts that `timeFormat` and `utcFormat` with a text specifier format the second anchor, 2024-03-05T13:04:09.007Z:
`%Y-%m-%d` is `2024-03-05`, `%H:%M:%S.%L` is `13:04:09.007`, `%I:%M %p` is `01:04 PM`, the February 29 anchor with
`%A, %B %d, %Y` is `Thursday, February 29, 2024` and with `%a %b %e %j %U %W %V` is `Thu Feb 29 060 08 09 09`, a text
without directives is unchanged and `%%` is `%`; the local form uses the zone and the UTC form does not (12:34 UTC is
18:04 at +05:30 for `timeFormat`, 12:34 for `utcFormat`). A null value formats to the text `null` (as in Vega), while
everything that is not a number (NaN, no argument, a date text, 1e300) is `Invalid Date`, a text that is a number is
read as that number, and `true` reads as 1 ms. An object as the specifier is the multi-format: the label is as coarse
as the largest boundary that the instant is on (Tuesday March 5 is `Tue 05`, Sunday March 3 is `Mar 03`, March 1 is
`March`, April 1 is `April`, January 1 is `2024`, 13:00 is `01 PM`, 13:45 is `01:45`, 13:45:30 is `:30`, and 250 ms is
`.250`), the members of the object replace the defaults by level (`date: '%d/%m'` gives `05/03`, `year: 'Y%Y'` gives
`Y2024`, `hours: '%H h'` gives `13 h`) unless they are not texts, and the local multi-format works on the wall time of
the zone (2024-03-04T18:30Z is March 5 00:00 at +05:30, so `Tue 05`, while the UTC form shows `06:30`). With a limit of
10 on `MaxStringLength`, ten characters pass and eleven fail with "the limit of 10 characters
(VegaLiteLimits.MaxStringLength)", a 10,000-character specifier is refused for the same limit, and under a budget of
100 steps the work of that specifier is refused for `VegaLiteLimits.MaxEvaluationSteps`. `timeParse` and `utcParse`
read `05/03/2024` with `%d/%m/%Y` as March 5, read `13:04:09.007Z` with the matching specifier, give 1900-01-01T13:45Z
for `01:45 PM` with `%I:%M %p` (no year means 1900: 1900-01-01 is day -25,567, that is 70 x 365 + 17 leap days
before 1970, so -2,208,988,800 s, plus 49,500 s of the time of day is -2,208,939,300 s, so -2,208,939,300,000 ms),
move a local parse by the zone (midnight at +05:30 is 18:30Z the day before) and leave a UTC parse alone, and give null
for left-over text, missing text, the empty text, and null; as in d3 the fields are not range checked, so `13/13/2024`
rolls over to 2025-01-13 and a number reads through its text (`2024` with `%Y`). A text of 10,000 characters is no date
and costs one step per 64 characters of the text and the specifier (10,002 characters is 156 steps), and under a budget
of 100 steps it fails with the step-limit error. The name formatters give English names counting from 0
(`monthFormat(0)` is `January`, 8 is `September`, 11 is `December`, 12 rolls over to `January` and -1 to `December`;
`dayFormat(0)` is `Sunday`, 6 `Saturday`, 7 `Sunday`, -1 `Saturday`; the abbreviated forms are `Jan`, `Feb`, `Sep`,
`Mon`, and `Wed`) and the empty text for anything that is not a whole number (1.5, the text `'1'`, null, no argument,
NaN, Infinity), whatever the zone. A specifier of 1,025 characters is refused by `timeFormat`, `utcFormat`,
`timeParse`, and `utcParse`, and so is a `year` override of 1,025 characters in the object form, with "the limit of 1024
characters", and 400 `%c` (13 tokens each, 5,200 in all) with "the limit of 4096 pieces", while a specifier of exactly
1,024 characters formats.

##### toDate, Date Text, And Datum Access

**Tests**: `ExpressionFunctionsTimeTests.ExpressionFunctions_ToDate_ConvertsInstants`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_DateText_IsCutOffAndCheap`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Time_AgainstADatum`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Now_StaysUnsupported`

Asserts that `toDate` reads a date text in the calendar (`'2024-03-05'`, `'2024-03-05T13:04:09.007Z'`, and
`'March 5, 2024'` are dates; a date-time without an offset is 10:00 local, which is 04:30Z at +05:30 and 10:00Z in
UTC), keeps a date, reads a number as milliseconds, and gives null for null, the empty text, no argument, text that is
no date, NaN, a boolean, and an array. A text of one million characters is looked at only up to 128 characters, so each
of two calls costs two steps (128 / 64) and gives no date (null from `toDate`, NaN from `year`), four steps in all.
Against a datum row a date field, a missing field (NaN or the text `null`), and a text field behave as above. `now` is
not in the table: the parser lists `vl-expr-function-now`, the call is null, and `timeFormat(now(), '%Y')` is the text
`null`.

##### Time Culture, Determinism, And Context Support

**Tests**: `ExpressionFunctionsTimeTests.ExpressionFunctions_Time_AreCultureInvariant`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Time_AreDeterministic`,
`ExpressionFunctionsTimeTests.ExpressionFunctionContext_TimeSupport_ChargesCachesAndExposesTheToken`,
`ExpressionFunctionsTimeTests.ExpressionFunctionContext_TimeFormatterCache_ClearsWhenFull`,
`ExpressionFunctionsTimeTests.ExpressionFunctionContext_TimeFormatterCache_DoesNotKeepLongSpecifiers`,
`ExpressionFunctionsTimeTests.ExpressionFunctions_Time_DefaultCalendarIsUtc`

Asserts that a combined text of month and day names, a formatted time, an offset, the ISO week, a parse, a
multi-format, a year, and a sequence element gives the same text under `tr-TR`, `de-DE`, and the invariant culture
(`September`, `Monday`, `Wed`, `01:04:09.007 PM March Tuesday`, `2024-03-05T13:05:09.007Z`, `10`,
`2024-03-05T00:00:00.000Z`, `.007`, `2024`, and `1970-01-01T01:00:00.000Z`, joined with a bar; all hand-derived from
2024-03-05T13:04:09.007Z: one minute later is 13:05:09.007Z, March 5 is in ISO week 10, and the second hourly step of a
sequence over 7,200,000 ms from the epoch is 01:00Z); that the same expression evaluated twice in
the US-like zone gives the same text, `2/29/2024, 5:34:56 PM` for the `%c` form of the local 12:34:56 (17:34:56Z at
-5 h), `.789` for the multi-format of the same wall time, and the instant of the local midnight of 2024-06-01 in
daylight time (04:00Z); and, on the context, that a charge of zero or fewer steps counts nothing, a charge of the
remaining 10 steps is accepted and one more fails with the `MaxEvaluationSteps` error, the token is exposed, the same
specifier gives the same cached formatter, the string-limit error names `MaxStringLength`, 70 distinct specifiers
overflow the 64-entry formatter cache so that an old entry is compiled again (a different object that still formats
the anchor as `2024`), a specifier of 202 characters (over the 64 the cache keeps) is a different formatter object at
every call while `%Y` is the same object, a failed compilation caches nothing, and a context with no calendar reads in UTC while one with +02:00 reads 14 for the hour of
12:34 UTC.
