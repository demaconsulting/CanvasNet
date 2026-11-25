## MathMlPainter Unit Verification Design

This document describes the unit-level verification strategy for the `MathMlPainter` unit in the
`DemaConsulting.CanvasNet.MathMl` namespace.

### Verification Approach

The unit is verified by painting small hand-built box trees (glyphs of the bundled math font, backgrounds, borders,
phantoms) onto surfaces through the test assembly's `InternalsVisibleTo` access and sampling pixels. The core drawing
subsystems and the bundled font are used as is. The limit and cancellation scenarios use a small glyph limit and a
pre-canceled token (no clock). Unit tests reside in `MathMlPainterTests.cs`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory boxes and surfaces; no files, threads, or shared state

### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A sampled pixel or an exception that differs from the
expected one constitutes a failure.

### Unit-Level Test Scenarios

#### CanvasNetMathMl-MathMlPainter-Glyphs: A Glyph Inks Its Box

**Tests**: `MathMlPainterTests.MathMlPainter_Paint_Glyph_InksItsBox`

Paints a glyph at a placement and asserts ink in its color on a row inside the box its pen and baseline give, and no ink
on a row below the glyph.

#### CanvasNetMathMl-MathMlPainter-Backgrounds: Backgrounds Under Glyphs

**Tests**: `MathMlPainterTests.MathMlPainter_Paint_Background_IsUnderGlyphs`

Asserts that a glyph stays visible over an element background, and that the background of the call fills the rest of
the surface.

#### CanvasNetMathMl-MathMlPainter-Borders: Four Sides

**Tests**: `MathMlPainterTests.MathMlPainter_Paint_Border_PaintsFourSides`

Asserts the border color on each of the four sides and an untouched inside.

#### CanvasNetMathMl-MathMlPainter-Phantom: A Phantom Paints Nothing

**Tests**: `MathMlPainterTests.MathMlPainter_Paint_Phantom_PaintsNothing`

Asserts a fully transparent surface for a phantom subtree.

#### CanvasNetMathMl-MathMlPainter-Cancellation: A Canceled Token

**Tests**: `MathMlPainterTests.MathMlPainter_Paint_CanceledToken_PaintsNothing`

Asserts `OperationCanceledException` for a pre-canceled token and a surface left untouched.

#### CanvasNetMathMl-MathMlPainter-Limit: Too Many Glyphs

**Tests**: `MathMlPainterTests.MathMlPainter_Paint_TooManyGlyphs_ThrowsBeforePainting`

Asserts that more glyphs than the limit throw `mathml-limit-exceeded` located at their element and naming
`MathMlLimits.MaxPaintedGlyphs`, with nothing painted.

#### CanvasNetMathMl-MathMlPainter-Rules: A Rule Fills Its Rectangle

**Tests**: `MathMlPainterTests.MathMlPainter_Paint_Rule_FillsItsRectangle`

Asserts that a rule 20 px long and 2 px thick is painted solid over its two pixel rows and nowhere beside or below it.
