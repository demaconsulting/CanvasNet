### ScriptLayout Unit Verification Design

This document describes the unit-level verification strategy for the `ScriptLayout` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified by laying out scripted elements of hand-built boxes (with chosen widths, ascents, descents, italic
corrections, and operator data) in a compact style at 100 px, through the test assembly's `InternalsVisibleTo` access,
over the bundled `MathFont` (whose `MATH` constants are the expected shifts and gaps: a constant of n design units is
n/10 CSS pixels at 100 px), and by asserting the positions of the children, the widths, and the extents. Hand-built
boxes isolate the script rules from the glyph metrics. Unit tests reside in `Layout/ScriptLayoutTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory boxes and the bundled font; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A shift, a position, a width, an extent, or an exception
that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-ScriptLayout-Subscript: The Subscript Shift

**Tests**: `ScriptLayoutTests.ScriptLayout_LayoutSub_ShiftFromConstantsAndScriptAscent`,
`ScriptLayoutTests.ScriptLayout_LayoutSub_LargeOpItalicCorrectionPullsSubscript`

Asserts that a small subscript is shifted down by SubscriptShiftDown (21 px), a subscript 70 px tall by its ascent −
SubscriptTopMax (27 px), and a subscript of a base descending 30 px by SubscriptBaselineDropMin + 30 (44 px), with the
resulting ascent and descent; and that the subscript of a large operator with an italic correction of 50 px starts
50 px before the end of the operator while its superscript starts at the end, the width being the operator, the wider
script, and SpaceAfterScript, and the operator kept on the box.

##### CanvasNetMathMl-Layout-ScriptLayout-Superscript: The Superscript Shift And The Italic Correction

**Tests**: `ScriptLayoutTests.ScriptLayout_LayoutSup_CrampedUsesCrampedShift`,
`ScriptLayoutTests.ScriptLayout_LayoutSup_ItalicCorrectionMovesSuperscript`

Asserts that a small superscript is shifted up by SuperscriptShiftUp (39 px), by SuperscriptShiftUpCramped (28 px)
when `math-shift` is compact, by SuperscriptBottomMin + its descent for a deep script (53.4 px), and by the base's
ascent − SuperscriptBaselineDropMax for a tall base (66 px); that the superscript of an operator with an italic
correction of 6 px starts 6 px after its end while its subscript and the width ignore the correction; and that a base
that is not an operator moves no script.

##### CanvasNetMathMl-Layout-ScriptLayout-SubSuperscript: Both Scripts

**Tests**: `ScriptLayoutTests.ScriptLayout_LayoutSubSup_GapIsEnforced`,
`ScriptLayoutTests.ScriptLayout_Layout_SpaceAfterScriptIsAdded`

Asserts that a 60 px tall subscript and a superscript descending 20 px, overlapping at the standard shifts, end with the
bottom of the superscript at SuperscriptBottomMaxWithSubscript (43 px), the subscript lowered to 33 px, and a gap of
exactly SubSuperscriptGapMin (16 px); that the width is the base, the wider script, and SpaceAfterScript (4 px); that a
descent below zero is clamped to zero; and that a layout without scripts throws `ArgumentException`.

##### CanvasNetMathMl-Layout-ScriptLayout-Multiscripts: Prescripts And Postscripts

**Tests**: `ScriptLayoutTests.ScriptLayout_LayoutMultiscripts_PrescriptsAndPostscriptsArePlaced`,
`ScriptLayoutTests.ScriptLayout_LayoutMultiscripts_OnePair_MatchesSubSup`

Asserts the horizontal positions of a prescript pair (after 4 px, aligned right in its 30 px column), the base and the
`mprescripts` box (at 34 px), and two postscript pairs (each followed by 4 px), the width, and the shared shifts (21 px
and 39 px); and that one postscript pair gives the same box and positions as `msubsup` with the same children.
