### ShapeLibrary Unit Verification Design

This document describes the unit-level verification strategy for the `ShapeLibrary` unit in the
`DemaConsulting.CanvasNet.Mermaid.Rendering` namespace: the internal static `ShapeLibrary` class (the node shapes, the
intersect functions, the style split, and the subgraph rectangle, in `ShapeLibrary.cs` and its six partial files) and
the shape registry `ShapeNames`.

#### Verification Approach

The unit is verified at two levels. The analytic tests in `ShapeLibraryTests.cs` and `ShapeNamesTests.cs` check the
registry, the intersect functions (with points worked out by hand, including the half-unit rounding offset of the line
intersection), the bounds of arcs, the style split, the paint of the node's own styles, and the documented choices
(the legacy `ellipse`, the `state` shape, the upright fork, an image without a resolver, a shape not drawn). They use a
`ShapeContext` of a default flowchart whose label measurement is either the real `LabelBuilder` or a fixed label box of
40 by 24 pixels, so that the geometry can be derived by hand from the shape formulas.

The vector tests in `ShapeLibraryVectorTests.cs` compare every case of the upstream vectors
`Fixtures/upstream-vectors/shapes.json` (286 cases) with this library. The vectors were recorded from the pinned Mermaid
in headless Chrome with the bundled Liberation Sans by `benchmarks/visual-diff/mermaid/gen-shape-vectors.mjs`, which
inserts every node with Mermaid's own `insertNode` and records the node's size, its drawn box, the label's translation
and measured box, the computed paint of every drawn element, and 48 intersections (24 directions at two radii). The
cases cover the 53 documented shapes with a short HTML label, a wrapping HTML label, and an SVG label, the undocumented
shapes, the bracket shapes, label, font, padding, style, image, and icon variants. The helper `ShapeVectors` reads the
vectors and draws a case's node, either with Mermaid's measured label box (to check the geometry alone) or with this
library's label measurement (to check the node size). No mocking is required; the internal types are reached through
the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `ShapeLibraryTests.cs`, `ShapeLibraryVectorTests.cs`, and `ShapeNamesTests.cs` (with the helper
`ShapeVectors.cs`) within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Rendering` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: A fixed label box replaces the label measurement in the analytic geometry tests
- **Fonts**: The bundled Liberation fonts for the measured labels
- **Files**: `Fixtures/upstream-vectors/shapes.json` of the test project (copied to the output folder)
- **Isolation**: Each test draws its own nodes; the vectors are read once and not changed

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. With Mermaid's label box, a node size, drawn box, or label place that differs from the vector by more than 0.02
pixel (0.06 pixel for the rough.js circles), or an intersection by more than 0.01 pixel, constitutes a failure; with
this library's label measurement, a label box or node size beyond 0.02 pixel for an HTML label or 2 pixels for an SVG
label (plus the documented differences of individual cases) constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Rendering-ShapeLibrary-Registry: The Registry Has Mermaid's Shape Names

**Tests**: `ShapeNamesTests.ShapeNames_ShortNames_AreTheFiftyThreeDocumentedShapes`,
`ShapeNamesTests.ShapeNames_HandlerOf_Alias_GivesItsShape`,
`ShapeNamesTests.ShapeNames_IsKnown_UnknownOrWrongCase_IsFalse`,
`ShapeNamesTests.ShapeNames_Nearest_Typo_SuggestsAcceptedName`

Asserts that the registry has the 53 documented shapes each known by its short name, that aliases and internal aliases
name the drawing function of their shape, that unknown names and names in the wrong case are not shapes and have no
drawing function, and that a misspelled name gets the nearest name the flowchart accepts.

##### CanvasNetMermaid-Rendering-ShapeLibrary-EveryShape: Every Accepted Shape Is Drawn

**Tests**: `ShapeLibraryTests.ShapeLibrary_Draw_EveryDocumentedName_IsDrawn`,
`ShapeLibraryVectorTests.ShapeLibrary_Vectors_CoverEveryDocumentedShape`,
`ShapeLibraryTests.ShapeLibrary_Draw_UndrawnShape_ThrowsRenderFailed`

Asserts that every documented short name, the undocumented shapes a flowchart accepts, the icon and image shapes, and
the legacy `ellipse` are drawn with a positive size, that the vectors cover every documented shape with a short, a
wrapping, and an SVG label, and that `classBox`, a shape of another diagram type, fails with `mermaid-render-failed` at
the node's line.

##### CanvasNetMermaid-Rendering-ShapeLibrary-Geometry: Shapes Have Mermaid's Geometry

**Tests**: `ShapeLibraryVectorTests.ShapeLibrary_Draw_UpstreamVectorWithMermaidLabel_MatchesGeometry`,
`ShapeLibraryTests.ShapeLibrary_Draw_State_SizesToTheLabel`,
`ShapeLibraryTests.ShapeLibrary_Draw_ForkInLeftToRight_IsUpright`,
`ShapeLibraryTests.ShapeLibrary_SegmentBounds_Arcs_IncludeTheirExtremes`

Draws every vector case with Mermaid's measured label box and asserts the node's width and height, its drawn box, the
label's place, and the 48 intersections against the vector; asserts that the `state` shape, whose rectangle Mermaid
sizes as not a number, draws and measures only its label (a box of -20, -12, 40, 24); that a fork in a left-to-right
graph is 10 by 70 pixels; and that the box of an arc includes its extremes and scales up a radius too small for it.

##### CanvasNetMermaid-Rendering-ShapeLibrary-NodeSize: Nodes Measure As Mermaid's Nodes

**Tests**: `ShapeLibraryVectorTests.ShapeLibrary_Draw_UpstreamVector_MatchesNodeSize`

Draws every vector case with this library's label measurement and asserts the label's box and the node's size against
the vector: HTML labels as the browser measures them, SVG labels within the browser's glyph rounding.

##### CanvasNetMermaid-Rendering-ShapeLibrary-Intersections: Outlines Are Cut With Mermaid's Arithmetic

**Tests**: `ShapeLibraryTests.ShapeLibrary_IntersectRect_Point_LeavesThroughTheRightSide`,
`ShapeLibraryTests.ShapeLibrary_IntersectEllipse_Points_LieOnTheEllipse`,
`ShapeLibraryTests.ShapeLibrary_IntersectLine_Crossing_KeepsTheRoundingOffset`,
`ShapeLibraryTests.ShapeLibrary_IntersectLine_NoCrossing_GivesNull`,
`ShapeLibraryTests.ShapeLibrary_IntersectPolygon_Square_GivesNearestCrossing`

Asserts that the rectangle intersection picks the side the line leaves through, that the ellipse intersections lie on
the ellipse on the axes and a diagonal, that the line intersection keeps the half-denominator rounding of its integer
original (two lines crossing at the origin come out half a unit off) and gives nothing for separated or parallel
segments, and that the polygon intersection places the polygon in the node's box and returns the crossing nearest the
point, even for a point inside, as Mermaid's line test never rejects.

##### CanvasNetMermaid-Rendering-ShapeLibrary-Paint: Shapes Are Painted As Mermaid Paints Them

**Tests**: `ShapeLibraryVectorTests.ShapeLibrary_Draw_UpstreamVector_MatchesPaint`,
`ShapeLibraryTests.ShapeLibrary_SplitStyles_Styles_SplitsLabelFromNode`,
`ShapeLibraryTests.ShapeLibrary_Draw_UserStyles_PaintTheShape`

Asserts that the parts of the short-label, style, and image cases are painted with the fills, strokes, and stroke
widths that Mermaid's elements compute to; that the style split takes the class styles, then the inline styles, a
later key winning in its first place, with `color` going to the label; and that a node's own fill, stroke width, and
dash win over the style sheet while the sheet's stroke color stays.

##### CanvasNetMermaid-Rendering-ShapeLibrary-LegacyEllipse: The Legacy Ellipse Is An Ellipse

**Tests**: `ShapeLibraryTests.ShapeLibrary_Draw_LegacyEllipse_IsAnEllipse`

Asserts that the legacy `ellipse` around a 40 by 24 label with a padding of 15 is 70 by 39 pixels and that its
intersect function is elliptical (a point far to the right gives the point 35, 0).

##### CanvasNetMermaid-Rendering-ShapeLibrary-Images: Image Nodes Are Sized And Reported As Documented

**Tests**: `ShapeLibraryTests.ShapeLibrary_Draw_ImageWithoutResolver_ReportsAndDrawsNoPicture`,
`ShapeLibraryVectorTests.ShapeLibrary_Draw_UpstreamVectorWithMermaidLabel_MatchesGeometry`

Asserts that an image node of width 60 and height 40 without an image resolver keeps its 60 by 40 frame, draws no
picture, and is reported once as `mermaid-image-unresolved` at its line, and that the image cases of the vectors (sizes
from the natural size, `w`, `h`, and the aspect constraint, label above or below) have Mermaid's geometry.
