## Layout Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Layout` subsystem of CanvasNetMathMl
(namespace `DemaConsulting.CanvasNet.MathMl.Layout`): the `LayoutEngine`, `StyleResolver`, `MathFont`, `MathBox`,
`TokenLayout`, `RowLayout`, `FractionLayout`, `RadicalLayout`, `ScriptLayout`, `UnderOverLayout`, `TableLayout`,
`OperatorDictionary`, and `StretchyGlyphs` units working together as the document uses them.

### Verification Approach

The subsystem is verified by reading equations with the Syntax subsystem and laying them out through a `LayoutEngine`
over the real bundled math font (the test helper `LayoutText`), through the test assembly's `InternalsVisibleTo` access,
and by asserting the boxes against the metrics of the font and the spacing rules, the reports collected from the sink,
and the bounds. All units are used as is; no mocking is required. Subsystem tests reside in `Layout/LayoutTests.cs`
within the `DemaConsulting.CanvasNet.MathMl.Tests` project.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory equations, the bundled font, and a pre-canceled token; no files, threads, or clocks

### Acceptance Criteria

A subsystem-level test run passes when every scenario below passes without an exception beyond those explicitly
asserted. A width, an ascent, a position, a report, or an exception that differs from the expected value constitutes a
failure.

### Subsystem-Level Test Scenarios

#### CanvasNetMathMl-Layout-Tokens, Rows, Operators, And MathFont: A Row Of Tokens

**Tests**: `LayoutTests.Layout_TokenRow_MatchesFontMetricsAndSpacing`

Lays out `x + 1` at 100 px and asserts the width as the advances of the italic x, the plus, and the one plus the
dictionary spacing of the operator (category B, 4/18 em) on both sides, the ascent as the rounded-out ink top of the
three glyphs, and the positions of the three children.

#### CanvasNetMathMl-Layout-Stretching: Fences Around A Tall Row

**Tests**: `LayoutTests.Layout_StretchyFencesAroundTallRow_MatchConstants`

Lays out parentheses around an `mspace` of 2 em by 1 em at 100 px and asserts that each parenthesis is centered on the
AxisHeight of the font, covers the larger half of the row on both sides of the axis, is as wide as its preferred inline
size, and is placed without spacing (categories F and G).

#### CanvasNetMathMl-Layout-Style And MathFont: Script Levels

**Tests**: `LayoutTests.Layout_ScriptLevel_ScalesTokensWithMathConstants`

Asserts that `scriptlevel="1"` makes the digits 60 % as wide, the ScriptPercentScaleDown constant of the font.

#### CanvasNetMathMl-Layout-Fractions, Radicals, And Style: Fractions And Square Roots

**Tests**: `LayoutTests.Layout_FractionsAndRadicals_MatchConstants`

Lays out `x/2` and the square root of 2 inline at 100 px and asserts that the numerator is drawn at the script size of
60 px, shifted up by the larger of FractionNumeratorShiftUp and the gap rule of FractionNumeratorGapMin above the bar,
that the bar has the FractionRuleThickness of the font and is centered on its AxisHeight, and that the square root rises
above its base by RadicalVerticalGap, RadicalRuleThickness, and RadicalExtraAscender.

#### CanvasNetMathMl-Layout-Scripts, UnderOver, And Style: Scripts, Limits, And Accents

**Tests**: `LayoutTests.Layout_ScriptsAndLimits_MatchConstants`

Lays out `x_i`, a summation with an upper limit in display style, and an accent hat over `x` at 100 px and asserts that
the subscript is drawn at the script size of 60 px, shifted down by the largest of SubscriptShiftDown, its ascent −
SubscriptTopMax, and SubscriptBaselineDropMin + the descent of the base, with the width of the base, the subscript, and
SpaceAfterScript; that the bottom of the upper limit is the larger of UpperLimitGapMin and UpperLimitBaselineRiseMin −
its descent above the summation; and that the hat keeps the font size of its base (100 px).

#### CanvasNetMathMl-Layout-Tables And TableExtensions: A Table

**Tests**: `LayoutTests.Layout_Tables_CenterOnAxisWithCorePadding`

Lays out a 2 by 2 table of `mspace` contents (one cell empty) at 100 px and asserts that its height is the padded
heights of its rows (0.5ex of the font above and below each content), that its ascent is half its height plus
AxisHeight, that its columns are their widest content plus 80 px, that the first row's baseline lies its padded ascent
below its top, and that the contents start 0.4em into their cells. The MathML 3 table attributes are exercised through
the subsystem by the unit tests of `TableLayout` and compared with Firefox at the system level.

#### CanvasNetMathMl-Layout-ChildCount: An Invalid Fraction

**Tests**: `LayoutTests.Layout_WrongChildCount_IsRowWithWarning`

Lays out an `mfrac` with three children and asserts that it is a row of its children on its baseline without a bar,
with one `mathml-child-count` warning at line 2, column 2.

#### CanvasNetMathMl-Layout-UnsupportedFeatures: Rows With Reports

**Tests**: `LayoutTests.Layout_UnsupportedElementsAndAttributes_AreRowsWithReports`

Lays out an `menclose`, an `mi` with `dir="rtl"`, and an `mn` with an invalid `mathcolor`, and asserts that the
enclosure is a row of its two children on its baseline, that the three boxes follow each other, and the reports
`mathml-element-unsupported` and `mathml-attribute-unsupported`, in order, with one `mathml-attribute-invalid` warning.

#### CanvasNetMathMl-Layout-Bounds: Work, Table Slots, And Cancellation

**Tests**: `LayoutTests.Layout_Bounds_WorkBudgetAndCancellation_StopTheLayout`

Asserts that a work limit of 3 fails the layout of a row of three tokens with `mathml-limit-exceeded` naming
`MathMlLimits.MaxLayoutWork`, that a pre-canceled token throws `OperationCanceledException`, that a table whose cell
spans 20 columns over two rows (40 slots) fails under 39 table cells with `mathml-limit-exceeded` naming
`MathMlLimits.MaxTableCells`, and that the same equation lays out within the default limits.
