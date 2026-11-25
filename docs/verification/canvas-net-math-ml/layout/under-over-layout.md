### UnderOverLayout Unit Verification Design

This document describes the unit-level verification strategy for the `UnderOverLayout` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified by laying out under- and overscript elements of hand-built boxes (with chosen widths, ascents,
descents, italic corrections, and operator data) in a compact style at 100 px, through the test assembly's
`InternalsVisibleTo` access, over the bundled `MathFont` (whose `MATH` constants are the expected gaps: a constant of n
design units is n/10 CSS pixels at 100 px), and by asserting the positions of the children, the widths, and the extents.
The inline stretching is verified with a recording `relayout` delegate that returns boxes of the requested width.
Unit tests reside in `Layout/UnderOverLayoutTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: A recording delegate stands in for the layout engine's relayout
- **Isolation**: In-memory boxes and the bundled font; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A position, a width, an extent, a relayout call, or an
exception that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-UnderOverLayout-Limits: Limits Of A Large Operator

**Tests**: `UnderOverLayoutTests.UnderOverLayout_LayoutUnderOver_LargeOp_UsesLimitConstants`

Asserts that the upper limit of a large operator sits UpperLimitBaselineRiseMin − its descent (21.4 px) above the
operator and the lower limit LowerLimitBaselineDropMin − its ascent (43 px) below it, without extra space; that the
limits move right and left by half the italic correction (5 px); and that the operator is kept on the box.

##### CanvasNetMathMl-Layout-UnderOverLayout-Accents: Accents And Other Scripts

**Tests**: `UnderOverLayoutTests.UnderOverLayout_LayoutOver_Accent_UsesAccentBaseHeight`,
`UnderOverLayoutTests.UnderOverLayout_LayoutOver_NotAccent_UsesOverbarGap`

Asserts that a hat over a base 54 px tall is raised so that its bottom is at AccentBaseHeight (55.6 px), centered, with
OverbarExtraAscender (7.1 px) above it, and sits on a base taller than AccentBaseHeight; that an overscript and an
underscript that are not accents are OverbarVerticalGap and UnderbarVerticalGap (15 px) from the base with 7.1 px of
extra space; and that an accent underscript touches the base.

##### CanvasNetMathMl-Layout-UnderOverLayout-StretchyBase: Scripts Of A Horizontally Stretchy Operator

**Tests**: `UnderOverLayoutTests.UnderOverLayout_LayoutUnderOver_StretchyBase_UsesStretchStackConstants`

Asserts that the overscript of a brace stretchy along the inline axis sits StretchStackTopShiftUp (24.4 px) above it and
the underscript StretchStackBottomShiftDown − its ascent (35.9 px) below it, without extra space, and that the descent
of the element, below zero for a brace above the baseline, is clamped to zero.

##### CanvasNetMathMl-Layout-UnderOverLayout-InlineStretch: Stretching To The Widest Child

**Tests**: `UnderOverLayoutTests.UnderOverLayout_StretchInline_StretchesOperatorsToTheWidestChild`,
`UnderOverLayoutTests.UnderOverLayout_StretchInline_AllStretchy_UsesZeroTarget`

Asserts that the stretchy overscript of a 50 px base is laid out again with 50 px, that a base laid out with the
constraint of its parent is not laid out again while its stretchy script stretches to its 80 px, that a child without a
stretchy operator is not laid out again, and that two stretchy children are laid out with 0 px first and then with the
wider of their results (25 px).

##### CanvasNetMathMl-Layout-UnderOverLayout-Placement: Centering

**Tests**: `UnderOverLayoutTests.UnderOverLayout_LayoutUnderOver_CentersOnTheWidestChild`,
`UnderOverLayoutTests.UnderOverLayout_LayoutUnderOver_LargeOp_UsesLimitConstants`

Asserts that a base 40 px wide, an underscript 100 px wide, and an overscript 60 px wide make a box 100 px wide with the
children at 30 px, 0 px, and 20 px, that a layout without scripts throws `ArgumentException`, and that the italic
correction of a large operator moves its limits.
