### NumberFormat Unit Verification Design

This document describes the unit-level verification strategy for the `NumberFormat` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace: the internal `NumberFormat` class (`ParseSpecifier`,
`TryParseSpecifier`, `Create`, `CreatePrefix`, `TickFormat`, `Format`, and the `PrecisionFixed`, `PrecisionPrefix`, and
`PrecisionRound` helpers) and the immutable `FormatSpecifier` record. It is an own C# implementation of the d3-format
specifier grammar and formatting steps for the English locale, with the Unicode minus sign (U+2212) for negative
numbers, used for axis labels; the third-party notice of the system names the upstream behavior that was studied.

#### Verification Approach

The unit is verified with vector tables of `(specifier, value, expected text)` whose expected strings are derived by
hand from the specifier grammar, the rounding rules of the JavaScript `toFixed`, `toExponential`, and `toPrecision`
functions (half up on the exact binary value of the number), and the d3 formatting steps (sign, symbol, zero fill,
grouping, trimming, alignment, and padding), and not by running d3 and pasting its output. The vectors are grouped so
that each rule is visible:

- Fixed notation with grouping and rounding: `,.2f` of 1234.5 is `1,234.50`; `,.1f` of 999.95 is `1,000.0` (rounding
  carries into the group); `.20f` of 0.1 is `0.10000000000000000555` (the exact binary expansion); `.0f` of 1e21 is
  `1e+21`.
- Rounding is half up on the exact binary value, so 1.005 (just below) gives `1.00`, 2.675 gives `2.67`, 0.995 gives
  `0.99`, 0.25 (exact) gives `0.3` at one digit, 0.35 (just below) gives `0.3`, and 0.45 (just above) gives `0.5`.
- Percent: `.0%` of 0.123 is `12%`; `~%` of 0.5 is `50%`; `p` and `.2p` use the percent-rounded precision.
- Signs and symbols: `+` shows a plus sign (and shows the sign of a negative that rounds to zero as `−0.00`), a space
  flag leaves room for the sign, `(` wraps negatives in parentheses, `$` is a prefix placed after the sign.
- Width, fill, and alignment: `08.2f` of -3.14159 is `−0003.14`; `>10.2f`, `<10.2f`, and `^10.2f` pad to ten
  characters; `=+8.1f` puts the padding between the sign and the digits; `010,d` of 1234567 is `01,234,567`.
- SI prefixes: `.3s` of 1500 is `1.50k`, of 0.5 is `500m`, of 0.00015 is `150µ`, of 1e-25 is `0.10y`, of 1e27 is `1000Y`;
  `~s` trims to `1.5k`; `.1s` of 1500 is `2k`.
- Exponent, significant, and rounded digits: `e` of 12345.678 is `1.234568e+4`; `.3g` of 1234.5678 is `1.23e+3`; `.3g` of
  0.0001234 is `0.000123`; `.5g` of 5e-324 is `4.9407e-324`; `.2r` of 1234.5678 is `1200`; `n` is `,g`.
- The empty type and unknown types behave as `.12~g`: 0.1 + 0.2 is `0.3`, 1e21 is `1e+21`, and negative zero is `0`.
- Integers and radixes: `d` rounds to an integer (`−2.5` gives `−3`); `x`, `X`, `#x`, `b`, `#b`, `o`, and `c` follow
  the radix rules; special values keep their sign, padding, and alignment (`NaN`, `Infinity`, `−Infinity`).

The `TickFormat` vectors derive the precision from the tick step, as d3-scale does: a step of 20 needs no decimals
(`1,234` for 1234 under the default `,f`), a step of 0.1 needs one decimal (`0.5`, `1.0`), a step of 0.01 two decimals,
`%` with a step of 0.2 needs none (`20%`), `s` with a step of 20,000 at 100,000 needs no decimals of thousands (`40k`)
while a step of 200,000 at 1e6 needs one decimal of millions (`0.4M`), and a specifier that has a precision is used as
it is (`.3f` of 2 is `2.000`). The helpers are checked against derived values (a step of 0.1 needs one decimal, a step
of 0.05 two, a step of 1 none; a step of 0.01 up to 1.01 needs three significant digits; a step of 10 thousand at 2.7
million needs two decimals of millions).

The unit has no dependencies other than the BCL (`System.Numerics.BigInteger` for the exact decimal expansion), so no
mocking or stubbing is required; the internal types are reached through the test assembly's `InternalsVisibleTo`
access.

Unit tests reside in `NumberFormatTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Scales` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of text and numbers; the culture scenario sets the current culture to `de-DE` and
  restores it in a `finally` block

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any formatted text that differs from the hand-derived expectation (compared character by character, so that
the minus sign, the micro sign, and the grouping separators are exact), any specifier accepted or rejected differently
from the grammar, or any output that depends on the current culture constitutes a failure.

#### Unit-Level Test Scenarios

##### Quantitative Labels Match The Hand-Derived Vector

**Tests**: `NumberFormatTests.NumberFormat_Format_QuantitativeLabelsMatchHandDerivedExpectedVector`

Asserts more than one hundred `(specifier, value, text)` vectors covering every type (`f`, `e`, `g`, `r`, `s`, `d`,
`x`, `X`, `b`, `o`, `c`, `n`, `%`, `p`, and the empty type), every flag (`+`, space, `(`, `$`, `#`, `,`, `~`, `0`),
alignments and fills, widths, rounding edges, SI prefixes from yocto to yotta, and the special values NaN and
infinity, and reports all failures together so that one run shows every mismatch.

##### Culture Independence

**Tests**: `NumberFormatTests.NumberFormat_Format_IgnoresTheCurrentCulture`

Asserts under a `de-DE` current culture that `,.2f` of 1234.5 is `1,234.50`, `.0%` of 0.123 is `12%`, `.2s` of -1500
is `−1.5k`, and `e` of 12345.678 is `1.234568e+4`.

##### Specifier Parsing

**Tests**: `NumberFormatTests.NumberFormat_ParseSpecifier_ReadsEveryPartAndRejectsInvalidText`

Asserts that `*^+$010,.3~g` parses to fill `*`, align `^`, sign `+`, symbol `$`, zero fill, width 10, comma, precision
3, trim, and type `g` and prints back as the same text; that the empty specifier has the defaults (fill space, align
`>`, sign `-`, no symbol, type empty, no zero, comma, or trim, no width or precision) and that `d` and `.2f` print as
the default fill (a space) followed by `>-d` and `>-.2f`; that `<<5` is a fill and an alignment; that `abc`, `ff`,
`.2.2f`, `10.f`, `.f`, a leading newline, `2,,f`, `~~g`, and `$$f` throw `FormatException` (and `TryParseSpecifier`
returns false, also for null); and that a null specifier throws `ArgumentNullException`.

##### Width Is Limited

**Tests**: `NumberFormatTests.NumberFormat_Format_HugeWidthIsLimited`

Asserts that `>99999999999d` of 7 produces exactly 1,000 characters ending in `7`, so that a hostile width cannot
allocate unbounded padding.

##### Precision Helpers And SI Prefix Formatter

**Tests**: `NumberFormatTests.NumberFormat_PrecisionHelpers_MatchHandDerivedValues`,
`NumberFormatTests.NumberFormat_CreatePrefix_ScalesByThePrefixOfTheReferenceValue`

Asserts the derived precisions described above, with NaN for a zero step or a zero reference value; and that the
formatter that scales by the prefix of a reference value formats 1500 and 2500 as `1.5k` and `2.5k` for a reference of
1500, -500 as `−0.5k`, 0.00000123 as `1.23µ` for a reference of 1e-6, and, for a reference of zero, every number as
`NaN`, as d3 does.

##### Tick Format Chooses The Precision From The Tick Step

**Tests**: `NumberFormatTests.NumberFormat_TickFormat_ChoosesThePrecisionFromTheTickStep`

Asserts the derived tick format vectors above (the default `,f`, percent, SI, significant digits, exponent, negative
domains, and specifiers with an explicit precision), reporting all failures together, and that an invalid specifier
throws `FormatException`.

##### Variable-Precision Format For Log Labels

**Tests**: `NumberFormatTests.NumberFormat_FormatFloat_TrimsZeroesWithoutAPrecision`

Asserts the hand-derived texts of Vega's `formatFloat` (from `toPrecision(12)` and the trimming of the zeros): 1000 is `1,000`,
0.001 is `0.001`, 1234.5 is `1,234.5`, 1e-7 is `1e-7`, 2.5e-7 is `2.5e-7`, 1e21 is `1e+21`, `s` gives `1k` and `1.5k`,
`%` gives
`25.6%` (precision 10), `e` gives `1.2345e+4` (precision 11), a specifier with a precision keeps its zeros
(`.3f` gives `2.000`),
and an invalid specifier throws `FormatException`.

##### Zero-Span Tick Format

**Tests**: `NumberFormatTests.NumberFormat_TickFormat_ZeroSpanDomainKeepsTheDefaultPrecision`

Asserts that a domain without span (5 to 5, 0 to 0) derives no precision (the tick step is zero and d3-format 3.1.2
has no
exponent for zero), so `,f` gives `5.000000` and `0.000000` while `,d` gives `5` and `.1f` gives `5.0`.
