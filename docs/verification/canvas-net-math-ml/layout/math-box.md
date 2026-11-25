### MathBox Unit Verification Design

This document describes the unit-level verification strategy for the `MathBox` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified by building boxes, placed children, and paint items directly through the test assembly's
`InternalsVisibleTo` access and asserting their content boxes, ink bounds, and item bounds. Nothing is mocked; the
records have no behavior to replace. Unit tests reside in `Layout/MathBoxTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A rectangle or a null that differs from the expected one
constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-MathBox-Model: Content Boxes, Ink, And Items

**Tests**: `MathBoxTests.MathBox_ContentRect_SpansAscentAndDescent`,
`MathBoxTests.MathBox_InkBounds_UnitesItemsAndChildren`,
`MathBoxTests.MathBox_InkBounds_PhantomAndEmpty_AreNull`,
`MathBoxTests.MathBox_PaintItems_ReportTheirBounds`

Asserts the content box (the width, the ascent above and the descent below the baseline), the union of the ink of the
items and of the children at their offsets, no ink for a phantom and for an empty box, and the bounds of the items (none
for a glyph without contours, its rectangle for a border).

##### CanvasNetMathMl-Layout-MathBox-Operators: The Operator Records

**Tests**: `MathBoxTests.MathBox_EmbellishedOperator_DefaultsDoNotStretch`

Asserts that an embellished operator with only its spacing is infix, Default, block-axis, not stretchy, symmetric,
large, movable, a fence, or a separator, with a minimum size of zero and no maximum size, and that an empty operator
context has neither a form nor a constraint while a copy can carry one.
