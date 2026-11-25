### MathFont Unit Verification Design

This document describes the unit-level verification strategy for the `MathFont` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified against the bundled Noto Sans Math, whose `MATH` constants and glyph data are known facts of the
font file (read independently with fontTools when the tests were written), through the test assembly's
`InternalsVisibleTo` access. The font without a `MATH` table is another bundled font of the core. Nothing is mocked: the
point of the unit is the real font. Unit tests reside in `Layout/MathFontTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: The bundled fonts; no files of the machine, no installed fonts

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A constant, a resolved font or glyph, a bound, or a
metric that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-MathFont-Constants: The MATH Constants

**Tests**: `MathFontTests.MathFont_Default_HasMathConstants`,
`MathFontTests.MathFont_Constructor_FontWithoutMathTable_ThrowsInvalidDataException`

Asserts the raw constants of the font (for example AxisHeight 278 and ScriptPercentScaleDown 60) and their scaled
values, and that a font without a `MATH` table throws `InvalidDataException`.

##### CanvasNetMathMl-Layout-MathFont-Fallback: Deterministic Fallback

**Tests**: `MathFontTests.MathFont_Resolve_FallsBackDeterministically`

Asserts that a mathematical character resolves to the math font, an accented letter to Noto Sans, and a character no
bundled font has to glyph 0 of the math font.

##### CanvasNetMathMl-Layout-MathFont-Metrics: Ink, Italic Correction, And Top Accent

**Tests**: `MathFontTests.MathFont_InkBounds_ScaleTheOutlineBounds`,
`MathFontTests.MathFont_ItalicCorrectionAndTopAccent_ComeFromTheMathTable`

Asserts the ink bounds of the italic x (0, 0, 547, 536 of 1000 units) scaled, the empty bounds of a space, the same
bounds on a repeated lookup, the italic correction of the italic f (145 units) and the top accent attachment of a (281
units), and that a fallback glyph has neither.
