### RowLayout Unit Verification Design

This document describes the unit-level verification strategy for the `RowLayout` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified by laying out rows of hand-built child boxes (with chosen widths, ascents, descents, operator data,
italic corrections, and space-likeness) and `mpadded` and `merror` around them, through the test assembly's
`InternalsVisibleTo` access, with the real `StyleResolver` for the `mpadded` attributes, and a relayout function that
records the constraints it is given and returns boxes of their size. Hand-built boxes isolate the row rules from the
glyph metrics. Unit tests reside in `Layout/RowLayoutTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory boxes; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A position, a width, an extent, an operator, or an item
that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-RowLayout-Row: Children, Operators, And Italic Correction

**Tests**: `RowLayoutTests.RowLayout_LayoutRow_Children_ShareBaselineAndTakeMaximumExtents`,
`RowLayoutTests.RowLayout_LayoutRow_NegativeExtents_ClampToZero`,
`RowLayoutTests.RowLayout_LayoutRow_Operator_GetsItsSpacing`,
`RowLayoutTests.RowLayout_LayoutRow_OperatorSpacing_FromEmbellishedOperator`,
`RowLayoutTests.RowLayout_LayoutRow_ItalicCorrection_ComesFromTheLastChild`

Asserts the left-to-right placement on a shared baseline with the maximum ascent and descent (zero for a negative
descent of a minus sign or a negative ascent of ink below the baseline), the leading and trailing
space around an operator child (also from the operator data of a stretched operator), and that italic corrections are
not added between children while the row takes the italic correction of its last child (none when that child is an
embellished operator).

##### CanvasNetMathMl-Layout-RowLayout-Stretch: Stretchy Operators

**Tests**: `RowLayoutTests.RowLayout_LayoutRow_StretchyParens_CoverTallestSibling`,
`RowLayoutTests.RowLayout_LayoutRow_AllChildrenStretchy_UseUnconstrainedSize`,
`RowLayoutTests.RowLayout_LayoutRow_NonStretchyOrInlineAxis_AreNotStretched`

With a recording relayout function, asserts that both parentheses of a row are laid out again with the largest ascent
and descent of the other children and that their new boxes are placed; that when every child stretches, the operators
are laid out with (0, 0) first and then with the largest of those sizes; and that a non-stretchy operator, an operator
with an inline stretch axis, and a row without a relayout function are not stretched.

##### CanvasNetMathMl-Layout-RowLayout-Grouping: Embellished Operators And Space-Likeness

**Tests**: `RowLayoutTests.RowLayout_LayoutRow_Grouping_PropagatesOperatorAndSpaceLikeness`

Asserts that a row of one operator and space-like children is an embellished operator without inner spacing (unless it
is `math`), that the anonymous row of `msqrt` or `mtd` has no inner spacing either but carries no operator, and that a
row of space-like children is space-like.

##### CanvasNetMathMl-Layout-RowLayout-Padded: mpadded

**Tests**: `RowLayoutTests.RowLayout_LayoutPadded_Attributes_SetSizeAndMoveContent`

Asserts the requested width, height, and depth, the content moved by `lspace` and `voffset`, that percentages and
negative values are ignored or clamped, and that a negative width keeps the inner width with a
`mathml-attribute-invalid` warning at the attribute.

##### CanvasNetMathMl-Layout-RowLayout-Error: merror

**Tests**: `RowLayoutTests.RowLayout_LayoutError_AddsBorderPadding`

Asserts that the box grows by one pixel on every side and that the border item covers it.
