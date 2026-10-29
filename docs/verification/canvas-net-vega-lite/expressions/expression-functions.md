### ExpressionFunctions Unit Verification Design

This document describes the unit-level verification strategy for the `ExpressionFunctions` unit in the
`DemaConsulting.CanvasNet.VegaLite.Expressions` namespace: the internal static `ExpressionFunctions` class (the closed
function table with `Names`, `TryGet`, and `Invoke`, split over its math, text, type, and regular-expression files),
the `ExpressionFunction` delegate and `ExpressionFunctionContext` (the per-evaluation context that counts steps, charges
size-proportional work, checks cancellation and the string limit, and caches compiled regular expressions), and
`ExpressionRegex` (the translation of a JavaScript regular expression into a safe .NET regular expression, with its
flags and its source text). The table holds exactly the 57 whitelisted functions of the supported subset:

- **Math (27)**: `abs`, `acos`, `acosh`, `asin`, `asinh`, `atan`, `atan2`, `atanh`, `ceil`, `clamp`, `cos`, `cosh`,
  `exp`, `floor`, `log`, `log10`, `max`, `min`, `pow`, `round`, `sign`, `sin`, `sinh`, `sqrt`, `tan`, `tanh`, `trunc`
- **Text and sequence (15)**: `lower`, `upper`, `trim`, `substring`, `slice`, `indexof`, `lastindexof`, `startswith`,
  `endswith`, `includes`, `replace`, `split`, `join`, `pad`, `length`
- **Type and conversion (13)**: `isArray`, `isBoolean`, `isDate`, `isFinite`, `isNaN`, `isNumber`, `isObject`,
  `isRegExp`, `isString`, `isValid`, `toBoolean`, `toNumber`, `toString`
- **Regular expression (2)**: `regexp`, `test`

Date and time, format, color, scale, and random functions are not in the table: a call to them is parsed, reported by
the feature catalog, and evaluates to null.

#### Verification Approach

The unit is verified by two means. Most scenarios compile an expression text that calls the function with the real
`ExpressionParser` and evaluate it with the real `ExpressionEvaluator` (through the `ExpressionTestSupport` helpers),
because the function table is reached only through an evaluator in the product and the observable contract is the
value; a few scenarios call `ExpressionFunctions.Invoke` and `ExpressionFunctionContext` directly to prove the table,
the argument checks, the steps, and the regular-expression cache. No mocking or stubbing is used and the internal types
are reached through the test assembly's `InternalsVisibleTo` access.

The expected values are written by hand from the JavaScript rules and the public Vega expression documentation, never
produced by running a JavaScript engine: `round(2.5)` is `3` and `round(-2.5)` is `-2` (half toward positive infinity),
`max()` is `-Infinity` and `min()` is `Infinity`, `pow(1, Infinity)` is `NaN` while `pow(NaN, 0)` is `1`,
`substring('hello', 3, 1)` is `el` (swapped), `slice('hello', -3)` is `llo`, `pad('ab', 5, '*', 'center')` is `*ab**`,
`pad('7', 3, '0', 'left')` is `007` (as Vega, `left` pads the start), `isNaN('x')` is false (no coercion, as
`Number.isNaN`), `toNumber('')` is null, `toString(1e21)` is `1e+21`, and `replace('abc', /(a)(b)(c)/, '$3$2$1$0')` is
`cba$0`. The transcendental functions are compared with reference values to a tolerance, because the host math library
may differ from a browser in the last bit; every exactly representable point (`cos(0)`, `log10(100)`, `sqrt(16)`) is
compared exactly.

The regular-expression scenarios prove three properties. The translation is correct for the supported subset (the
classes `\d \D \w \W` are ASCII only, `$` without the `m` flag matches only at the end of the text, `[^]` matches any
character and `[]` nothing, the flags `g i m s` are honored, and look-ahead and back-references work through the
backtracking fallback). The engine is safe: the linear-time `NonBacktracking` engine runs every pattern it can, so
catastrophic patterns finish on 50,000 characters, and a pattern it rejects (look-around or a back-reference) runs on
the backtracking engine with a 100 ms limit that fails closed with a located error; a pattern is at most 1,024
characters. And the failures are located: an invalid pattern or flag is a `VegaLiteRenderException` at the expression
with the offset of the failing call.

Unit tests reside in `ExpressionFunctionsTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in
the `Expressions` folder, with the shared helpers in `ExpressionTestSupport.cs`.

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
result that depends on the culture, a pattern that does not finish, an exception of the wrong type, a failure without
the location of the expression and the offset, or a string result over `MaxStringLength` that is not refused
constitutes a failure.

#### Unit-Level Test Scenarios

##### The Function Table Is Exactly The Whitelist

**Tests**: `ExpressionFunctionsTests.ExpressionFunctions_Names_AreExactlyTheWhitelist`,
`ExpressionFunctionsTests.ExpressionFunctions_Invoke_UnknownName_ThrowsArgumentException`

Asserts that `Names` returns exactly the 57 names above in ordinal order, that `random`, `now`, `datetime`, `date`,
`year`, `format`, `timeFormat`, `rgb`, `scale`, `if`, `eval`, and `Function` are absent and not found by `TryGet`, that
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

##### Steps, Charges, And Determinism

**Tests**: `ExpressionFunctionsTests.ExpressionFunctionContext_StepsAndCharges_AreExactAndCancelable`,
`ExpressionFunctionsTests.ExpressionFunctions_Repeated_ResultsAreIdentical`

Asserts, on a context with a budget of 10 steps, that nine single steps and a charge of 63 units of size (below one
step) leave 9 steps used, that a charge of 64 units is the tenth and last step, that the next step or charge throws the
budget exception, that a charge of `long.MaxValue` exhausts the budget without overflow, and that a charge that crosses
a multiple of 1,024 observes a cancellation; and that fifty evaluations of a combined text give the single result
`A+B+C003`, while `random()` is null every time because it is not in the table.
