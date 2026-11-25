### LayoutEngine Unit Verification Design

This document describes the unit-level verification strategy for the `LayoutEngine` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified by laying out small equations through the engine with the real `StyleResolver`, `TokenLayout`,
`RowLayout`, and bundled `MathFont` (the engine creates its collaborators, and the font is deterministic), through the
test assembly's `InternalsVisibleTo` access, and by asserting the boxes, their elements, the reports of the sink, and
the bounds. The bounds scenarios use small limits and a pre-canceled token (no clock). Unit tests reside in
`Layout/LayoutEngineTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory equations and the bundled font; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A box, an element, a report, a work count, or an
exception that differs from the expected one constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-LayoutEngine-Dispatch: Tokens And Rows

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_Dispatch_LaysOutTokensAndRows`

Asserts that tokens paint glyphs, that rows place their children, and that every box keeps its element.

##### CanvasNetMathMl-Layout-LayoutEngine-UnsupportedElements: Later And Unknown Elements

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_StageBElement_FallsBackToRowWithReport`

Asserts that an element of a later version and an unknown element are laid out as rows of their children and reported
as `mathml-element-unsupported` at their locations.

##### CanvasNetMathMl-Layout-LayoutEngine-HiddenElements: No Box

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_HiddenElements_GetNoBox`

Asserts that an element with `display: none` and the annotations get no box, and that `semantics` and `maction` lay out
their first child only.

##### CanvasNetMathMl-Layout-LayoutEngine-Phantom: mphantom

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_Mphantom_IsPhantomWithSize`

Asserts that the box of `mphantom` is a phantom with the size of its content.

##### CanvasNetMathMl-Layout-LayoutEngine-UnsupportedAttributes: Attribute Reports

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_UnsupportedAttributes_AreReported`

Asserts that legacy and unknown attributes and `dir="rtl"` are reported as `mathml-attribute-unsupported`, and that
global attributes that do not change a picture are silent.

##### CanvasNetMathMl-Layout-LayoutEngine-TextInRows: Text In A Row

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_TextInRow_IsLaidOutLikeMtext`

Asserts that text directly inside a row is laid out as a space-like text box.

##### CanvasNetMathMl-Layout-LayoutEngine-Bounds: Depth, Work, And Cancellation

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_Bounds_AreEnforced`

Asserts that a nesting limit of 2 and a work limit of 5 fail with messages naming `MathMlLimits.MaxNestingDepth` and
`MathMlLimits.MaxLayoutWork`, that a pre-canceled token throws, and that the work of the equation is 7 (four elements
and three characters).
