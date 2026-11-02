### WordWrap Unit Verification Design

This document describes the unit-level verification strategy for the `WordWrap` static class and
its `WordWrapTokenKind` enum.

#### Verification Approach

`WordWrapTests.cs` packs synthetic tokens: a small record carrying a text, a role and a fixed width,
built by the helpers `W` (word), `S` (one-unit whitespace) and `Br` (line break). Widths are small
whole numbers, so every expected line is worked out by hand, and a `Render` helper prints each line's
text with `|` between lines so an expectation reads as the wrapped text. The position-dependent
width test uses a tab-like token whose width is "up to the next multiple of four", the case that
motivated the measure callback. No fonts or surfaces are involved.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None
- **Isolation**: Each test builds its own tokens; no shared state

#### Acceptance Criteria

A unit test run passes when every scenario below passes, and when every named test method listed for
each requirement ID passes across every target framework in this repository's CI matrix.

#### Test Scenarios

##### CanvasNet-Rendering-WordWrap-Packing: Greedy Packing That Never Splits a Word Group

**Tests**: `WordWrap_Pack_TokensFit_ReturnsOneLine`,
`WordWrap_Pack_WordOverflowsLine_WrapsAtWhitespace`,
`WordWrap_Pack_AdjacentWordTokens_AreNeverSplit`,
`WordWrap_Pack_GroupWiderThanLine_IsPlacedWholeOnItsOwnLine`,
`WordWrap_Pack_NoTokens_ReturnsNoLines`,
`WordWrap_Pack_OnlyWhitespace_StaysOnOneLine`,
`WordWrap_Pack_ZeroNegativeAndInfiniteWidth_BehaveAsDocumented`,
`WordWrap_Pack_Result_IsIndependentOfInput`

Packs `aa bb` into a line of five (one line, an exact fit stays), `aa bb cc` into five (`aa bb| cc`,
the stranded space carried onto the next line), `x Hel`+`lo y` with `Hel` and `lo` as two word tokens
into four (`x |Hello| y`, the group kept whole), and a twenty-wide word into five (on its own line);
packs no tokens (no lines) and three spaces into one unit (one line, never closed); packs into widths
of zero and minus three (each unit on its own line) and positive infinity (never wraps); clears a
returned line and asserts the input list is unchanged.

##### CanvasNet-Rendering-WordWrap-LineBreaks: Explicit Breaks End Lines

**Tests**: `WordWrap_Pack_LineBreaks_EndLinesAndAreNeverPlaced`,
`WordWrap_Pack_LineBreakAfterWordGroup_KeepsGroupOnThePrecedingLine`

Packs a break between two words, first, last, doubled, and alone, and asserts the lines (`a|b`, `|a`,
`a|`, `a||b`, two empty lines) and that no break token appears in a line; packs a two-token word group
followed by a break and asserts the group stays on the preceding line.

##### CanvasNet-Rendering-WordWrap-Measure: Position-Dependent Widths and Both Precisions

**Tests**: `WordWrap_Pack_MeasureReceivesCurrentLineWidth`,
`WordWrap_Pack_FloatOverload_PacksLikeDoubleOverload`

Packs words separated by tab-like tokens whose width is the distance to the next multiple of four,
records the line widths the measure was given (2 then 6) and asserts everything fits on one line in
nine units but wraps before the last word in 8.5; packs the same tokens with the single-precision and
double-precision overloads and asserts equal lines.

##### CanvasNet-Rendering-WordWrap-Validation: Arguments Are Validated

**Test**: `WordWrap_Pack_InvalidArguments_ThrowWithParameterName`

Passes a null token list, classifier and measure (to both precisions where applicable) and a NaN
width (to both) and asserts the exception types and parameter names.
