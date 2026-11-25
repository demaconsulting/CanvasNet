### FractionLayout Unit Verification Design

This document describes the unit-level verification strategy for the `FractionLayout` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified by laying out fractions of hand-built child boxes (with chosen widths, ascents, and descents) in a
compact or display style at 100 px, through the test assembly's `InternalsVisibleTo` access, over the bundled
`MathFont` (whose `MATH` constants are the expected shifts and gaps: a constant of n design units is n/10 CSS pixels at
100 px), and by asserting the positions of the children, the extents, the padding, and the bar. The `linethickness`
scenarios resolve the attribute of hand-built `mfrac` elements with the real `StyleResolver` and a collecting report
sink. Hand-built boxes isolate the fraction rules from the glyph metrics. Unit tests reside in
`Layout/FractionLayoutTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory boxes and elements and the bundled font; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A shift, an extent, a width, a position, a bar
rectangle or color, a thickness, or a report that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-FractionLayout-Bar: Shifts, Gaps, And The Bar

**Tests**: `FractionLayoutTests.FractionLayout_Layout_DisplayStyle_UsesDisplayShiftsAndGaps`,
`FractionLayoutTests.FractionLayout_Layout_Compact_UsesTextShiftsAndGaps`,
`FractionLayoutTests.FractionLayout_Layout_TallChildren_PushedByGapRules`,
`FractionLayoutTests.FractionLayout_Layout_Bar_IsCenteredOnAxisWithItsThickness`

Asserts that small children are shifted by the display-style constants (70.4 px up, 67.3 px down) in display style,
with the resulting ascent and descent, and by the standard constants (52.9 px and 32 px) in compact style; that a
numerator descending 40 px and a denominator 60 px tall are pushed to exactly the minimum gaps of 7.1 px from the bar;
and that the bar is a rule of the given thickness, 1 px from the left edge, as wide as the content, centered on the axis
of 27.8 px, in the color of the style.

##### CanvasNetMathMl-Layout-FractionLayout-Stack: A Fraction Without A Bar

**Tests**: `FractionLayoutTests.FractionLayout_Layout_ZeroThickness_UsesStackShiftsAndGap`

Asserts that a zero thickness paints nothing and uses the stack shifts (52.9 px and 32 px) when the gap is large
enough, and that a gap of 4.9 px is widened to StackGapMin (17.8 px) by moving both children by half of the missing
12.9 px.

##### CanvasNetMathMl-Layout-FractionLayout-LineThickness: linethickness

**Tests**: `FractionLayoutTests.FractionLayout_ResolveThickness_LengthPercentNegativeAndInvalid`

Asserts that `3px` is 3 px, `200%` twice the default (14.2 px), `-1px` and `0` are zero, an absent attribute is the
default (7.1 px), and the MathML 3 name `thick` is the default with one `mathml-attribute-invalid` warning.

##### CanvasNetMathMl-Layout-FractionLayout-Placement: Centering, Padding, And The Operator

**Tests**: `FractionLayoutTests.FractionLayout_Layout_ChildrenCenteredAndPaddedByOnePixel`

Asserts that children 20 px and 30 px wide make a box of 32 px with the children at 6 px and 1 px (centered after the
1 px padding), and that the operator given for an embellished fraction is kept on its box.
