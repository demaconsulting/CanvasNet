### EdgeBuilder Unit Verification Design

This document describes the unit-level verification strategy for the `EdgeBuilder` unit in the
`DemaConsulting.CanvasNet.Mermaid.Rendering` namespace: the internal static `EdgeBuilder` class (the route, stroke, and
labels in `EdgeBuilder.cs`, the d3-shape curves in `EdgeBuilder.Curves.cs`, and the markers in
`EdgeBuilder.Markers.cs`).

#### Verification Approach

The unit is verified at two levels. The vector test compares d3's line generator with every curve Mermaid offers for
edges against the upstream vectors `Fixtures/upstream-vectors/curves.json` (144 cases: 12 curves on 12 point sets),
recorded from d3-shape in Node by `benchmarks/visual-diff/mermaid/gen-curve-vectors.mjs`; the path text must be equal
character for character. The other tests are analytic: routes, boxes, and markers with numbers worked out by hand from
Mermaid's formulas (a right angle rounded off 5 pixels each side of the corner, an arrowhead tip 4 pixels beyond the
pulled-back end, the middle of a route by length, the quirks of `intersection` and `isLabelCoordinateInPath`). The
drawing tests use the `ShapeContext` of a flowchart text from `RenderingTestContext` (the real configuration, style
sheet, and label measurement with the bundled fonts) and the real intersect functions of `ShapeLibrary`; no mocking is
required, and the internal types are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `EdgeBuilderTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Rendering` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts for the measured edge labels
- **Files**: `Fixtures/upstream-vectors/curves.json` of the test project (copied to the output folder)
- **Isolation**: Each test builds its own routes; the vectors are read once and not changed

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A curve whose path text differs from d3-shape's in any character, or a route point, marker, stroke, or label
position that differs from the hand-derived value, constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Rendering-EdgeBuilder-Curves: Curves Are Drawn As d3-shape Draws Them

**Tests**: `EdgeBuilderTests.EdgeBuilder_Line_UpstreamVector_MatchesD3`,
`EdgeBuilderTests.EdgeBuilder_Line_UnknownCurve_DrawsBasis`,
`EdgeBuilderTests.EdgeBuilder_Line_NotFiniteNumber_EndsTheDrawnPath`,
`EdgeBuilderTests.EdgeBuilder_RoundedPath_Corner_IsAQuadraticCurve`

Asserts that every curve on every point set writes d3-shape's path text (three decimals, d3's command forms) and starts
with a move, that an unlisted curve name draws the basis curve, that a number that is not finite is written as
`NaN` and ends the drawn segments there, and that the rounded curve pulls the end back by the arrowhead offset and
turns the corner with a quadratic curve.

##### CanvasNetMermaid-Rendering-EdgeBuilder-Route: Routes End Where Mermaid Ends Them

**Tests**: `EdgeBuilderTests.EdgeBuilder_CutPathAtIntersect_RouteIntoBox_EndsOnTheBorder`,
`EdgeBuilderTests.EdgeBuilder_Intersection_TopSide_KeepsMermaidsArithmetic`,
`EdgeBuilderTests.EdgeBuilder_FixCorners_RightAngle_IsRoundedOff`,
`EdgeBuilderTests.EdgeBuilder_Draw_ArrowPoint_TipTouchesTheNode`,
`EdgeBuilderTests.EdgeBuilder_Draw_EndsAndSubgraph_CutTheRoute`

Asserts that a route into a box stops on its border, that `intersection` on the top side gives Mermaid's point and gives
the outside point back for a vertical segment, that a right angle becomes three points 5 pixels each side of the
corner, that the line of an arrowhead ends 4 pixels before the node while the marker reaches it, and that the end
nodes' intersect functions replace the first and last points while a route into a subgraph is cut (stopping, with
Mermaid's quirk, at its last point outside).

##### CanvasNetMermaid-Rendering-EdgeBuilder-Markers: Markers Are Mermaid's Markers

**Tests**: `EdgeBuilderTests.EdgeBuilder_MarkerOf_ArrowTypes_GiveMermaidsMarkers`,
`EdgeBuilderTests.EdgeBuilder_DrawMarker_PointEnd_TipIsFourPixelsAhead`,
`EdgeBuilderTests.EdgeBuilder_DrawMarker_CircleAndCross_ArePlacedBeforeTheVertex`,
`EdgeBuilderTests.EdgeBuilder_StartOfAndEndOf_Curve_TakeTheTangents`

Asserts the view box, size, reference point, and stroke of the point, circle, and cross markers and their start
variants (none for `arrow_open`), that the point marker is one filled 8 by 8 pixel triangle whose tip is 4 pixels
beyond the vertex in the direction of the path, that the circle of radius 5.5 and the cross stroked 2 pixels wide are
placed before the vertex, and that the start and end directions are the tangents of the path.

##### CanvasNetMermaid-Rendering-EdgeBuilder-Stroke: Edges Are Stroked By Their Link And Styles

**Tests**: `EdgeBuilderTests.EdgeBuilder_Draw_LinkStroke_FollowsTheClasses`,
`EdgeBuilderTests.EdgeBuilder_Draw_LinkStyleStroke_ColorsPathAndMarker`,
`EdgeBuilderTests.EdgeBuilder_Draw_AnimatedEdge_IsDashedNineFive`

Asserts that normal, thick, dotted, and invisible links are stroked 1, 3.5, 1 (dashed 2,2), and not at all in the line
color without a fill, that a `linkStyle` stroke colors the path and its arrowhead and sets its width, and that an
animated edge is drawn as the first frame of its animation, dashed 9,5 at the offset 900.

##### CanvasNetMermaid-Rendering-EdgeBuilder-LabelPlacement: Labels Go Where Mermaid Puts Them

**Tests**: `EdgeBuilderTests.EdgeBuilder_CalcLabelPosition_Route_IsTheMiddleByLength`,
`EdgeBuilderTests.EdgeBuilder_IsLabelCoordinateInPath_RoundedText_IsSearched`,
`EdgeBuilderTests.EdgeBuilder_LabelPosition_LayoutPosition_IsMovedByHalfTheTitleMargin`,
`EdgeBuilderTests.EdgeBuilder_Draw_EndsAndSubgraph_CutTheRoute`

Asserts the middle of a route by length (rounded as Mermaid rounds it; a single point; nothing for an empty route), the
text search of `isLabelCoordinateInPath` with its quirk for negative numbers, that a label goes nowhere without text,
at the layout's position moved down by half the title margin otherwise, and at the origin without a layout position,
and that the label of a route cut at a subgraph moves to the middle of the cut route.

##### CanvasNetMermaid-Rendering-EdgeBuilder-LabelDrawing: Edge Labels Are Measured And Drawn As In Mermaid

**Tests**: `EdgeBuilderTests.EdgeBuilder_MeasureLabel_Text_IsOneLineBox`,
`EdgeBuilderTests.EdgeBuilder_DrawLabel_Html_IsCenteredWithTwoBackgrounds`,
`EdgeBuilderTests.EdgeBuilder_DrawLabel_Svg_HasAHalfOpaqueBackground`

Asserts that an empty label measures an empty box and a one-line HTML label a 24 pixel line box, that a placed HTML
label is an `edge-label` group at its position with two backgrounds over its box and its text, and that an SVG label
has one background at half opacity and is centered on the box of its text.
