### AttributeValues Unit Verification Design

This document describes the unit-level verification strategy for the `AttributeValues` unit in the
`DemaConsulting.CanvasNet.MathMl.Syntax` namespace.

#### Verification Approach

The unit is verified by calling the parsers of `AttributeValues` and `StyleDeclarations.Parse` with valid and invalid
values (several through theories) and resolving lengths with `MathLength.Resolve`, through the test assembly's
`InternalsVisibleTo` access. The core `CssColor` parser is used as is. Unit tests reside in
`Syntax/AttributeValuesTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of in-memory strings; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A parsed value, a resolved length, an acceptance, or an
issue that differs from the expected one constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Syntax-AttributeValues-Lengths: Lengths And Named Spaces

**Tests**: `AttributeValuesTests.AttributeValues_TryParseLength_EachUnit_IsParsed`,
`AttributeValuesTests.AttributeValues_TryParseLength_NamedSpaces_AreEighteenthsOfAnEm`,
`AttributeValuesTests.AttributeValues_TryParseLength_InvalidInput_IsRefused`,
`AttributeValuesTests.AttributeValues_MathLength_Resolve_UsesBasesAndClamps`

Asserts every CSS unit with its conversion or relative unit, `thinmathspace` as 3/18 em and
`negativeveryverythickmathspace` as -7/18 em, the refusal of a unitless number, an unknown unit, the legacy
pseudo-units, and text, and the resolution against the font size, the x-height, and a percentage base with clamping.

##### CanvasNetMathMl-Syntax-AttributeValues-Booleans: Booleans And Unsigned Integers

**Tests**: `AttributeValuesTests.AttributeValues_TryParseBooleanAndUnsignedInteger_AcceptOnlyValidValues`

Asserts `true` (in any case, trimmed) and `false`, the refusal of other words, an unsigned integer, and the refusal of a
sign and of a number too large for an integer.

##### CanvasNetMathMl-Syntax-AttributeValues-ScriptLevel: Three Forms

**Tests**: `AttributeValuesTests.AttributeValues_TryParseScriptLevel_ReadsChangesAndLevels`

Asserts `+2` and `-1` as relative changes, `3` as a level, and the refusal of a lone sign and a fraction.

##### CanvasNetMathMl-Syntax-AttributeValues-Colors: CSS Colors

**Tests**: `AttributeValuesTests.AttributeValues_TryParseColor_ReadsCssColors`

Asserts names, `transparent` (all channels zero), the hexadecimal forms with and without alpha (`#00f`, `#0000ff80`),
the `rgb()`, `rgba()`, `hsl()`, and `hsla()` functions with their alpha (a number and a percentage, rounded to 128 and
64), and the refusal of invalid text and of an `hsl()` with a missing channel.

##### CanvasNetMathMl-Syntax-AttributeValues-Style: The Inline Style Subset

**Tests**: `AttributeValuesTests.StyleDeclarations_Parse_SupportedSubset_IsRead`,
`AttributeValuesTests.StyleDeclarations_Parse_MathDepthForms_AreRead`,
`AttributeValuesTests.StyleDeclarations_Parse_UnsupportedDeclarations_AreIssues`

Asserts every supported declaration with the last one of a property winning and `!important` and comments ignored, the
`auto-add` and `n` forms of `math-depth` and both values of `math-style`, and that declarations outside the subset are
unsupported issues and invalid values invalid issues, in order, with semicolons inside parentheses not splitting.
