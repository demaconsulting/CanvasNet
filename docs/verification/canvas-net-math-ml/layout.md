## Layout Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Layout` subsystem of CanvasNetMathMl
(namespace `DemaConsulting.CanvasNet.MathMl.Layout`): the `LayoutEngine`, `StyleResolver`, `MathFont`, `MathBox`,
`TokenLayout`, `RowLayout`, `OperatorDictionary`, and `StretchyGlyphs` units working together as the document uses
them.

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

#### CanvasNetMathMl-Layout-UnsupportedFeatures: Rows With Reports

**Tests**: `LayoutTests.Layout_UnsupportedElementsAndAttributes_AreRowsWithReports`

Lays out an `mfrac`, an `mi` with `dir="rtl"`, and an `mn` with an invalid `mathcolor`, and asserts that the fraction is
a row of its two children on its baseline, that the three boxes follow each other, and the reports
`mathml-element-unsupported` and `mathml-attribute-unsupported`, in order, with one `mathml-attribute-invalid` warning.

#### CanvasNetMathMl-Layout-Bounds: Work And Cancellation

**Tests**: `LayoutTests.Layout_Bounds_WorkBudgetAndCancellation_StopTheLayout`

Asserts that a work limit of 3 fails the layout of a row of three tokens with `mathml-limit-exceeded` naming
`MathMlLimits.MaxLayoutWork`, that a pre-canceled token throws `OperationCanceledException`, and that the same equation
lays out within the default limits.
