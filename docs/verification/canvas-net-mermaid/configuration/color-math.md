### ColorMath Unit Verification Design

This document describes the unit-level verification strategy for the `ColorMath` unit in the
`DemaConsulting.CanvasNet.Mermaid.Configuration` namespace: the internal static `ColorMath` (the khroma color
functions, parsers, rounding, and number formatting) and its `ColorFormatException`.

#### Verification Approach

The unit is verified against the 622 khroma calls recorded in `Fixtures/upstream-vectors/themes.json`: every function
the themes use, on hexadecimal, keyword, `rgb()`/`rgba()`, and `hsl()`/`hsla()` inputs, including results that are
`NaN` and both errors of khroma; each call must give the same text, the same number, or the same error message.
Hand-written tests verify the details that the vectors show only indirectly: the written form of an unchanged color,
the rounding to ten decimals and JavaScript's rounding of halves, JavaScript's number formatting (worked out from the
ECMAScript `Number.prototype.toString` rules), the parser edges with the answers khroma gives, and the failure on a
value that is not a string. The unit has no dependencies other than the .NET base class library, so no mocking or
stubbing is required; the internal type is reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `ColorMathTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Configuration` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/themes.json` (the khroma calls), generated from the khroma that the pinned
  Mermaid uses by `benchmarks/visual-diff/mermaid/gen-theme-vectors.mjs`
- **Isolation**: Pure functions; the fixture is read only

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any result whose text, number, or error differs from khroma's constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Configuration-ColorMath-KhromaFidelity: Every Call Equals Khroma's

**Tests**: `ColorMathTests.ColorMath_Function_MatchesKhroma`

Asserts, for each recorded call of `invert`, `darken`, `lighten`, `adjust`, `rgba`, `isDark`, `luminance`, and
`channel`, the same result text or number as khroma, or the same error message.

##### CanvasNetMermaid-Configuration-ColorMath-WrittenForm: Unchanged Colors Keep Their Text

**Tests**: `ColorMathTests.ColorMath_Adjust_NoChange_KeepsWrittenForm`

Asserts that adjusting `#ECECFF` by nothing returns `#ECECFF` and that the keyword `White` returns `#ffffff`.

##### CanvasNetMermaid-Configuration-ColorMath-Numbers: JavaScript Rounding And Formatting

**Tests**: `ColorMathTests.ColorMath_Round_TenDecimalsHalfUp`,
`ColorMathTests.ColorMath_FormatNumber_FollowsJavaScript`

Asserts that 86.27450980392157 rounds to 86.2745098039, that -2.5 rounds to -2 and 2.5 to 3, and that numbers are
written as JavaScript writes them (`1e-7`, `0.000001`, `1e+21`, `1.5e+300`, `123456789012345680000`, `NaN`, `0` for
negative zero, and `250`).

##### CanvasNetMermaid-Configuration-ColorMath-Parsing: Exactly The Texts Khroma Reads

**Tests**: `ColorMathTests.ColorMath_IsColor_ParserEdges`, `ColorMathTests.ColorMath_Function_MatchesKhroma`

Asserts the answers khroma gives for white space inside functions, signs and exponents, angle units in any letter
case, a missing percent sign, an empty argument, a leading space, a two-digit hexadecimal, a CSS name that khroma does
not know (`steelblue`), and a khroma keyword (`CyanAqua`).

##### CanvasNetMermaid-Configuration-ColorMath-NotAColor: Failures Carry The Value

**Tests**: `ColorMathTests.ColorMath_Invert_NotAString_ThrowsWithValue`, `ColorMathTests.ColorMath_Function_MatchesKhroma`

Asserts that inverting the number 5 throws a `ColorFormatException` whose value is 5, and that the recorded calls on
texts that are not colors fail with khroma's message.
