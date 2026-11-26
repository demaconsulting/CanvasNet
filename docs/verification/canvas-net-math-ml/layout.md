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

#### CanvasNetMathMl-Layout-Operators: Explicit Forms And Script Operators

**Tests**: `LayoutTests.Layout_ExplicitFormAndScriptOperator_FollowTheDictionary`

Lays out `a + b` with `form="prefix"` on the plus, and an `msup` whose superscript is a plus, at 100 px, and asserts
that the plus takes the prefix form and is spaced by the leading and trailing space of the prefix entry of the
dictionary (not the infix spacing of its position), and that the operator of the superscript is postfix.

#### CanvasNetMathMl-Layout-Stretching: Fences Around A Tall Row

**Tests**: `LayoutTests.Layout_StretchyFencesAroundTallRow_MatchConstants`

Lays out parentheses around an `mspace` of 2 em by 1 em at 100 px and asserts that each parenthesis is centered on the
AxisHeight of the font, covers the larger half of the row on both sides of the axis, is as wide as its preferred inline
size, and is placed without spacing (categories F and G).

#### CanvasNetMathMl-Layout-Stretching: Inline Stretching, Large Operators, And The Report

**Tests**: `LayoutTests.Layout_InlineStretchLargeOperatorAndReport_FollowTheFont`

Lays out an over brace over a row of five letters, a summation inline and in display style, and a `stretchy="true"`
star beside a tall `mspace` at 100 px, and asserts that the ink of the stretched brace (an assembly of several glyphs)
spans the width of its base, that the summation in display style is drawn with another, taller glyph (a size variant)
than inline, and that the star, which nothing can stretch, is reported once as `mathml-stretch-unavailable`.

#### CanvasNetMathMl-Layout-Style And MathFont: Script Levels

**Tests**: `LayoutTests.Layout_ScriptLevel_ScalesTokensWithMathConstants`

Asserts that `scriptlevel="1"` makes the digits 60 % as wide, the ScriptPercentScaleDown constant of the font.

#### CanvasNetMathMl-Layout-Style: Colors, mathvariant, And The Inline Style

**Tests**: `LayoutTests.Layout_ColorVariantAndInlineStyle_StyleTheGlyphs`

Lays out four letters and asserts that `mathcolor="red"` and `style="color: lime"` color their glyphs, that
`mathvariant="bold"` draws the glyph of the bold letter U+1D41A, and that an invalid `mathvariant` is reported once as
`mathml-attribute-invalid` at its line while its letter keeps the automatic italic.

#### CanvasNetMathMl-Layout-MathFont: A Character No Font Has

**Tests**: `LayoutTests.Layout_MissingGlyph_IsReported`

Lays out a private-use character and asserts that it is drawn as one glyph (the missing-glyph box) and reported once
as `mathml-text-missing-glyph` at the line of its token.

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

#### CanvasNetMathMl-Layout-Tables: A Table

**Tests**: `LayoutTests.Layout_Tables_CenterOnAxisWithCorePadding`

Lays out a 2 by 2 table of `mspace` contents (one cell empty) at 100 px and asserts that its height is the padded
heights of its rows (0.5ex of the font above and below each content), that its ascent is half its height plus
AxisHeight, that its columns are their widest content plus 80 px, that the first row's baseline lies its padded ascent
below its top, and that the contents start 0.4em into their cells.

#### CanvasNetMathMl-Layout-TableExtensions: The MathML 3 Table Attributes

**Tests**: `LayoutTests.Layout_TableExtensions_AlignSpaceAndRule`

Lays out a 2 by 2 table of `mspace` contents (1 em wide in the first row, 2 em in the second) with
`columnalign="left right"`, `rowspacing="2em"`, `columnlines="solid"`, `frame="solid"`, and the invalid
`rowlines="wavy"` at 100 px, and asserts that the narrow contents are aligned with the left edge of the wide one in the
first column and with its right edge in the second, that 200 px separate the bottom of the first row's contents from the
top of the second's, that a one-pixel column line lies between the columns and a one-pixel frame rule runs along the
top of the table, and that the invalid `rowlines` is reported once as `mathml-attribute-invalid` at its line and draws
no row line. The layout is compared with Firefox at the system level.

#### CanvasNetMathMl-Layout-Rows, Scripts, And Radicals: Glyphs Do Not Collide

**Tests**: `LayoutTests.Layout_SlantedLettersScriptsAndRadicals_InkDoesNotCollide`

Lays out at 100 px the italic *f* between stretchy and non-stretchy braces and parentheses and before the parenthesis
of a function application, `√(1 − ζ²)` in display style, and `(√(b² − 4ac))/2` in display style (the radical in the
compact numerator), and asserts that no two consecutive glyphs of the fenced letters overlap, that the superscript 2 is
clear of the ink of ζ and of b, and that the overbar of each radical lies at least RadicalDisplayStyleVerticalGap
(display) or RadicalVerticalGap (compact) above the ink of its content, whose top is the ink of the superscript; and
that the superscripts k, n, and 2 of the slanted letters f, y, v, w, ψ, and γ, alone and at the end of a row, inline
and in display style, are clear of the ink of the letter.

#### CanvasNetMathMl-Layout-Scripts: The Integrand After An Integral With Limits

**Tests**: `LayoutTests.Layout_IntegralWithLimits_IntegrandFollowsTheLimits`

Lays out at 100 px in display style `∫` from −∞ to ∞ followed by `f`, and `∫` from 1 to ∞ followed by a fraction, and
asserts that the ink of the integrand starts between 1 px and 30 px (0.3em) after the ink of the integral and its
limits ends.

#### CanvasNetMathMl-Layout-Scripts: The Subscript Of A Character

**Tests**: `LayoutTests.Layout_SubscriptOfCharacter_IgnoresItsDescender`

Lays out the subscript n of x, γ, y, ψ, and of the row `a y` at 100 px and asserts that the four single characters put
it at the same SubscriptShiftDown whatever their descenders, while the row puts it SubscriptBaselineDropMin below its
descent, deeper.

#### CanvasNetMathMl-Layout-Tokens: Painted Ink Inside The Extent

**Tests**: `LayoutTests.Layout_PaintedInk_StaysInsideTheExtent`

Paints an integral with limits followed by an italic x, a large parenthesis around a fraction, a summation with limits
followed by an italic j, nested stretched parentheses around a sum and fractions, and a radical around tall nested
fractions, each at 16, 20, 23, 32, 57, and 96 px, into a surface 8 pixels larger on every side than their content box
united with their ink bounds rounded out to whole pixels, and asserts that every pixel outside that box stays
transparent (the antialiasing included) while the box holds ink: the ink bounds of the layout cover everything the
painter draws.

#### CanvasNetMathMl-Layout-ChildCount: An Invalid Fraction

**Tests**: `LayoutTests.Layout_WrongChildCount_IsRowWithWarning`

Lays out an `mfrac` with three children and asserts that it is a row of its children on its baseline without a bar,
with one `mathml-child-count` warning at line 2, column 2.

#### CanvasNetMathMl-Layout-UnsupportedFeatures: Rows With Reports

**Tests**: `LayoutTests.Layout_UnsupportedElementsAndAttributes_AreRowsWithReports`

Lays out an `menclose`, an `mi` with `dir="rtl"`, and an `mn` with an invalid `mathcolor`, and asserts that the
enclosure is a row of its two children on its baseline, that the three boxes follow each other (each starts where the
previous one ends), and the reports
`mathml-element-unsupported` and `mathml-attribute-unsupported`, in order, with one `mathml-attribute-invalid` warning.

#### CanvasNetMathMl-Layout-Bounds: Work, Nesting, Table Slots, And Cancellation

**Tests**: `LayoutTests.Layout_Bounds_WorkBudgetAndCancellation_StopTheLayout`

Asserts that a work limit of 3 fails the layout of a row of three tokens with `mathml-limit-exceeded` naming
`MathMlLimits.MaxLayoutWork`, that a nesting depth limit of 2 fails it with `mathml-limit-exceeded` naming
`MathMlLimits.MaxNestingDepth`, that a pre-canceled token throws `OperationCanceledException`, that a table whose cell
spans 20 columns over two rows (40 slots) fails under 39 table cells with `mathml-limit-exceeded` naming
`MathMlLimits.MaxTableCells`, and that the same equation and the same table lay out within the default limits.
