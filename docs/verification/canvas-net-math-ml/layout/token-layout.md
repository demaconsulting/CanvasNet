### TokenLayout Unit Verification Design

This document describes the unit-level verification strategy for the `TokenLayout` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified by laying out tokens with the bundled math font (whose glyph data are known facts of the font
file), the real `StyleResolver`, and a collecting report sink, through the test assembly's `InternalsVisibleTo` access,
and by asserting the code points drawn, the box metrics, the operator properties, the stretched and large operators, and
the reports; an `mo` gets its form and stretch constraint directly through an `OperatorContext`. The `mathvariant`
mappings are checked against the Unicode code charts in a theory over every value. Unit tests reside in
`Layout/TokenLayoutTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory equations and the bundled fonts; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A code point, a width, an ascent, a descent, a spacing,
or a report that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-TokenLayout-AutoItalic: The Automatic Italic

**Tests**: `TokenLayoutTests.TokenLayout_LayoutToken_SingleCharMi_IsItalic`,
`TokenLayoutTests.TokenLayout_LayoutToken_MultiCharMi_IsUpright`,
`TokenLayoutTests.TokenLayout_LayoutToken_MathvariantNormal_CancelsItalic`

Asserts that `<mi>x</mi>` draws U+1D465, that `<mi>sin</mi>` draws the upright letters, and that
`mathvariant="normal"` draws the upright x.

##### CanvasNetMathMl-Layout-TokenLayout-Mathvariant: Every Value

**Tests**: `TokenLayoutTests.TokenLayout_LayoutToken_EveryMathvariant_MapsLatinGreekDigits`,
`TokenLayoutTests.TokenLayout_LayoutToken_LetterlikeHoles_MapToLetterlikeSymbols`

Asserts the styled code points of Latin and Greek letters, digits, and Arabic letters for every value, a character
without a styled form left unchanged, and the letterlike symbols that fill the holes of the block.

##### CanvasNetMathMl-Layout-TokenLayout-Metrics: The Token Box

**Tests**: `TokenLayoutTests.TokenLayout_LayoutToken_InkAscentDescent_FollowGlyphBounds`,
`TokenLayoutTests.TokenLayout_LayoutToken_TextWithSpace_UsesInkMetrics`

Asserts the box of the italic f (advance 450, ink 20, -240, 575, 765 of 1000 units) with its ascent and descent rounded
out to whole pixels and its italic correction of 145 units, and that a text with a space has the ink metrics of its
glyphs and the advance of the space.

##### CanvasNetMathMl-Layout-TokenLayout-InvisibleCharacters: Invisible Operators

**Tests**: `TokenLayoutTests.TokenLayout_LayoutToken_InvisibleOperators_HaveZeroWidthAndNoReport`

Asserts zero width, no glyph, and no report for U+2061 to U+2064.

##### CanvasNetMathMl-Layout-TokenLayout-Space: mspace

**Tests**: `TokenLayoutTests.TokenLayout_LayoutToken_Mspace_UsesLengths`

Asserts the width, height, and depth of `mspace`, a negative depth clamped, a negative width ignored with a warning, and
a percentage ignored with a report.

##### CanvasNetMathMl-Layout-TokenLayout-String: ms

**Tests**: `TokenLayoutTests.TokenLayout_LayoutToken_Ms_DrawsTextWithoutQuotes`

Asserts that `ms` draws its text as written, without quotes.

##### CanvasNetMathMl-Layout-TokenLayout-OperatorProperties: The Dictionary And The Attributes

**Tests**: `TokenLayoutTests.TokenLayout_LayoutToken_Mo_HasDictionarySpacing`,
`TokenLayoutTests.TokenLayout_LayoutToken_OperatorAttributes_OverrideDictionary`

Asserts the category B spacing of an infix plus (4/18 em on each side), no spacing for a prefix plus, explicit `lspace`
and `rspace` lengths and a percentage of the dictionary value; that every attribute (`form` in another case, `fence`,
`separator`, `stretchy`, `symmetric`, `largeop`, `movablelimits`, `minsize`, and a percentage `maxsize`) overrides the
dictionary, that an explicit form without an entry gives the Default category, and that an invalid `form` and an
invalid boolean are reported and ignored.

##### CanvasNetMathMl-Layout-TokenLayout-Stretch: Stretched Operators

**Tests**: `TokenLayoutTests.TokenLayout_LayoutOperator_StretchConstraint_CoversTargetCenteredOnAxis`,
`TokenLayoutTests.TokenLayout_LayoutOperator_MinMaxSize_ClampTarget`,
`TokenLayoutTests.TokenLayout_LayoutOperator_NoConstruction_KeepsGlyphAndReports`

Asserts that a parenthesis stretched to the constraint (200, 100) covers it symmetrically about the math axis in a box
of its preferred inline size, that `symmetric="false"` centers it on the constraint instead, that `minsize` grows and
`maxsize` (in length and in percent) shrinks the target about the axis, that the empty constraint gives the unstretched
height centered on the axis, and that a stretchy `x` keeps its glyph centered on the target with one
`mathml-stretch-unavailable` warning, none when the target is shorter than the glyph.

##### CanvasNetMathMl-Layout-TokenLayout-LargeOp: Large Operators

**Tests**: `TokenLayoutTests.TokenLayout_LayoutToken_LargeOpDisplayStyle_UsesDisplayOperatorMinHeight`,
`TokenLayoutTests.TokenLayout_LayoutToken_LargeOpInline_KeepsBaseGlyph`

Asserts that a summation in display style draws its large variant, taller than inline and centered on the math axis,
and that it keeps its base glyph inline and with `largeop="false"`.

##### CanvasNetMathMl-Layout-TokenLayout-MissingGlyph: A Character No Font Has

**Tests**: `TokenLayoutTests.TokenLayout_LayoutToken_MissingGlyph_IsReported`

Asserts that U+E000 is drawn as glyph 0 of the math font and reported as `mathml-text-missing-glyph` at the text.
