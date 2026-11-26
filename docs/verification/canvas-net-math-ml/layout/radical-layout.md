### RadicalLayout Unit Verification Design

This document describes the unit-level verification strategy for the `RadicalLayout` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified by laying out square roots and roots of hand-built base and index boxes (with chosen widths,
ascents, and descents) in a compact or display style at 100 px, through the test assembly's `InternalsVisibleTo`
access, over the bundled `MathFont` (whose `MATH` constants and constructions of U+221A are the expected values: a
constant of n design units is n/10 CSS pixels at 100 px), and by asserting the extents, the positions of the children,
the painted symbol and overbar, and the limit error. The expected symbol is computed with `StretchyGlyphs` where it is
the base glyph. Hand-built boxes isolate the radical rules from the glyph metrics of the base. Unit tests reside in
`Layout/RadicalLayoutTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory boxes and the bundled font; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. An extent, a width, a position, a painted item, or an
exception that differs from the expected one constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-RadicalLayout-SquareRoot: The Symbol, The Metrics, And The Overbar

**Tests**: `RadicalLayoutTests.RadicalLayout_LayoutSquareRoot_SurdCoversBaseGapAndRule`,
`RadicalLayoutTests.RadicalLayout_LayoutSquareRoot_DisplayStyle_UsesDisplayGap`,
`RadicalLayoutTests.RadicalLayout_LayoutSquareRoot_TallBase_StretchesTheSurd`,
`RadicalLayoutTests.RadicalLayout_LayoutSquareRoot_Overbar_SpansTheBase`

Asserts for a base of 50 px by 10 px that the base glyph of the symbol covers the base, the gap of 8.9 px, and the
overbar; that the ascent is the base's plus 8.9 + 7.1 + 7.1 px (19 px of gap in display style) and the descent reaches
the bottom of the symbol plus the extra ascender; that the base follows the advance of the symbol and the box is as
wide as the base plus the preferred inline size of the symbol; that a base 400 px tall stretches the symbol into an
assembly of at least three glyphs whose ink reaches the bottom of the base; and that the overbar is one rule of 7.1 px
from the start of the base over its 50 px, 7.1 px below the top of the box, in the color of the style.

##### CanvasNetMathMl-Layout-RadicalLayout-Root: The Index

**Tests**: `RadicalLayoutTests.RadicalLayout_LayoutRoot_IndexIsKernedAndRaised`,
`RadicalLayoutTests.RadicalLayout_LayoutRoot_NarrowIndex_ClampsKernAfter`,
`RadicalLayoutTests.RadicalLayout_LayoutRoot_TallIndex_AscentAndDescentAsChrome`

Asserts that an index 60 px wide starts after the kern of 5.1 px with its bottom raised by 35/64 of the height of the
symbol from the bottom of the square root, that the square root follows after the kern of -45 px, the width and the
ascent of the root; that after an index 30 px wide the kern is clamped to -30 px, so the square root starts at
5.1 px; and that an index 150 px tall (50 px of it below its baseline), taller than the raise, gives the root the
ascent of its top (the raise above the bottom of the square root plus the depth and the ascent of the index) and the
descent of the square root minus the raise plus the height of the index, with the bottom of the index still at the
raise.

##### CanvasNetMathMl-Layout-RadicalLayout-Limit: The Assembly Limit

**Tests**: `RadicalLayoutTests.RadicalLayout_LayoutSquareRoot_HugeBase_LimitedByMaxAssemblyParts`

Asserts that a base 10,000 px tall with `MaxAssemblyParts` of 5 fails with `mathml-limit-exceeded` at the location of
the radical, naming `MaxAssemblyParts`.
