### StretchyGlyphs Unit Verification Design

This document describes the unit-level verification strategy for the `StretchyGlyphs` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified by stretching characters of the bundled Noto Sans Math at a font size of 100 px (so that a design
unit is 0.1 px), through the test assembly's `InternalsVisibleTo` access, and by asserting the kind of the result, its
glyphs, its metrics, and its errors against the constructions of the font (known facts of the font file: the 13
variants of the parenthesis from 942 to 3768 units and its assembly of three 1069-unit parts with a minimum connector
overlap of 100 units). The assembly solver is checked over many targets with invariants (the overlap lies between the
minimum and the connectors, the size covers the target, one repetition fewer would not) and against exact sizes. Unit
tests reside in `Layout/StretchyGlyphsTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: The bundled fonts and pure functions; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A kind, a glyph, a size, an overlap, a repetition count,
or an error that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-StretchyGlyphs-Variants: Base Glyph, Variants, And The Large Variant

**Tests**: `StretchyGlyphsTests.StretchyGlyphs_Stretch_SmallTarget_UsesBaseGlyph`,
`StretchyGlyphsTests.StretchyGlyphs_Stretch_MediumTarget_UsesFirstCoveringVariant`,
`StretchyGlyphsTests.StretchyGlyphs_LargeVariant_Sum_IsTallerThanBase`

Asserts that a target of 10 px keeps the base glyph of the parenthesis, that a target of 150 px takes the first variant
of at least 150 px, and that the large variant of a summation is another glyph, taller than the base.

##### CanvasNetMathMl-Layout-StretchyGlyphs-Assembly: Repetitions, Overlap, And Width

**Tests**: `StretchyGlyphsTests.StretchyGlyphs_Stretch_LargeTarget_AssemblesWithMinimalRepetitions`,
`StretchyGlyphsTests.StretchyGlyphs_Stretch_Assembly_OverlapWithinConnectorBounds` (a theory over ten targets from
377 px to 9000 px), `StretchyGlyphsTests.StretchyGlyphs_Stretch_Assembly_SizeCoversTargetExactlyWhenPossible`,
`StretchyGlyphsTests.StretchyGlyphs_Stretch_HorizontalArrow_Assembles`,
`StretchyGlyphsTests.StretchyGlyphs_Stretch_HorizontalAssembly_HasInkExtents`,
`StretchyGlyphsTests.StretchyGlyphs_PreferredInlineSize_IsMaximumOverConstructions`

Asserts that 744.4 px is a bottom, six extenders, and a top of exactly 744.4 px standing on the baseline with each part
above the previous; the overlap bounds and the minimal repetitions over the theory; exact sizes for four targets; that
an assembly without an extender or whose extenders do not grow it is invalid; that the right arrow assembles left to
right to at least 600 px, its glyphs in left-to-right order; that an over brace assembled to 1000 px has whole-pixel
ink extents above the baseline (a negative descent); and the preferred inline size as the widest of the base,
the variants, and the parts (the advance of the letter x for a character without a construction).

##### CanvasNetMathMl-Layout-StretchyGlyphs-UnicodeFallback: Unicode-Based Assemblies

**Tests**: `StretchyGlyphsTests.StretchyGlyphs_Stretch_NoMathConstruction_UsesUnicodeFallback`

Asserts that the left-right arrow, which Noto Sans Math cannot stretch horizontally, is a left arrow, horizontal line
extenders, and a right arrow of at least 500 px, and that the B.4 table has 41 entries.

##### CanvasNetMathMl-Layout-StretchyGlyphs-RuleFallback: Line Characters

**Tests**: `StretchyGlyphsTests.StretchyGlyphs_Stretch_MissingGlyphLine_UsesRuleFallback`

Asserts that the macron, which Noto Sans Math has no glyph for, is a rule 300 px long with the vertical extent of the
ink of its fallback glyph, that the overline is a line character, and that the horizontal bar stretched vertically is a
rule of the target height.

##### CanvasNetMathMl-Layout-StretchyGlyphs-Limit: The Assembly Limit

**Tests**: `StretchyGlyphsTests.StretchyGlyphs_Stretch_HugeTarget_ThrowsLimitExceeded`

Asserts that targets of 10,000,000 px and infinity fail with `mathml-limit-exceeded` at the given location, naming
`MathMlLimits.MaxAssemblyParts`, and that 5000 px stays within 1000 glyphs.

##### CanvasNetMathMl-Layout-StretchyGlyphs-Failure: Nothing Stretches

**Tests**: `StretchyGlyphsTests.StretchyGlyphs_Stretch_NothingAvailable_Fails`

Asserts that the letter x stretches along neither axis and has no large variant, while its base glyph is available.
