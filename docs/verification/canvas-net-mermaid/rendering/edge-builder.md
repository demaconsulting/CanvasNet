### EdgeBuilder Unit Verification Design

This document describes the unit-level verification strategy for the `EdgeBuilder` unit in the
`DemaConsulting.CanvasNet.Mermaid.Rendering` namespace: the internal static `EdgeBuilder` class (the route, stroke, and
labels in `EdgeBuilder.cs`, the d3-shape curves in `EdgeBuilder.Curves.cs`, the flowchart, class, state, and entity
relationship markers in `EdgeBuilder.Markers.cs`, and the terminal labels in `EdgeBuilder.Terminals.cs`).

#### Verification Approach

The unit is verified at two levels. The vector test compares d3's line generator with every curve Mermaid offers for
edges against the upstream vectors `Fixtures/upstream-vectors/curves.json` (144 cases: 12 curves on 12 point sets),
recorded from d3-shape in Node by `benchmarks/visual-diff/mermaid/gen-curve-vectors.mjs`; the path text must be equal
character for character. The other tests are analytic: routes, boxes, markers, and terminal label positions with
numbers worked out by hand from Mermaid's formulas (a right angle rounded off 5 pixels each side of the corner, an
arrowhead tip 4 pixels beyond the pulled-back end, the middle of a route by length, the quirks of `intersection` and
`isLabelCoordinateInPath`, the reference points of the class, state, and entity relationship markers in `markers.js`,
and the walk and offsets of
`calcTerminalLabelPosition`). The drawing tests use the `ShapeContext` of a flowchart text from `RenderingTestContext`
(the real configuration, style sheet, and label measurement with the bundled fonts), or of a class diagram text
(`RenderingTestContext.CreateClass`, with the class diagram's style sheet, and SVG labels when its frontmatter sets
`htmlLabels: false`), of a state diagram text (`RenderingTestContext.CreateState`, with the state diagram's style
sheet and the theme variables of its frontmatter), or of an entity relationship diagram text
(`RenderingTestContext.CreateEr`, with the entity relationship diagram's style sheet), and the real intersect
functions of `ShapeLibrary`; no mocking is required, and the internal
types are reached through the test assembly's `InternalsVisibleTo` access.

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
asserted. A curve whose path text differs from d3-shape's in any character, a route point, marker, stroke, label
position, or terminal label position that differs from the hand-derived value, or a terminal label failure that is
missing or not located at the link constitutes a failure.

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

##### CanvasNetMermaid-Rendering-EdgeBuilder-LabelBackgrounds: Edge Label Backgrounds Come From The Diagram's Sheet

**Tests**: `EdgeBuilderTests.EdgeBuilder_DrawLabel_ClassDiagram_UsesTheClassBackgrounds`

Draws the label `uses` with the style sheet of a class diagram, once with HTML labels and once with SVG labels, and
asserts that the HTML label has two backgrounds (the class sheet's `.labelBkg` and `.edgeLabel .label span`) and the
SVG label one background rectangle (`.edgeLabel .label rect`), all painted in the class diagram's node background
`#ECECFF`.

##### CanvasNetMermaid-Rendering-EdgeBuilder-ClassMarkers: Class Markers Are Mermaid's Class Markers

**Tests**: `EdgeBuilderTests.EdgeBuilder_UserSpaceMarkerOf_ClassArrowTypes_GiveMermaidsMarkers`,
`EdgeBuilderTests.EdgeBuilder_DrawUserSpaceMarker_Extension_TipIsSeventeenPixelsFromTheVertex`,
`EdgeBuilderTests.EdgeBuilder_DrawUserSpaceMarker_LollipopStart_IsAnUnfilledCircleBehindTheVertex`,
`EdgeBuilderTests.EdgeBuilder_Draw_ClassRelation_MarkersFollowTheClassRules`,
`EdgeBuilderTests.EdgeBuilder_Draw_ClassRelationStroke_ColorsTheMarkers`

Asserts the reference points of `markers.js` for the start and end variants of the class markers - (18, 7) and (1, 7)
for `aggregation`, `extension`, and `composition`, (6, 7) and (13, 7) for `dependency`, (13, 7) and (1, 7) for
`lollipop` - with their types, the lollipop circle's own `transparent` fill, and no user-space marker for `none` or
the flowchart's `arrow_point`. Places the extension markers at the vertex (100, 50) on a path pointing right and
asserts that the end variant's triangle is (100, 44), (100, 56), (117, 50), its tip 17 pixels ahead, and the start
variant's (83, 50), (100, 56), (100, 44), its tip 17 pixels behind, each a `marker` path of the edge stroked 1 pixel
wide in the marker's color; and that the start lollipop is an unfilled circle with the box (88, 44, 12, 12), 6 pixels
behind the vertex. Draws a straight class relation of 100 pixels with a composition at its start and an extension at
its end and asserts that the line is pulled back 17.25 pixels at each end (`M0,17.25L0,50L0,82.75`), that the
composition is filled and stroked 1 pixel wide in the line color `#333333` with its tip 0.25 pixel from the route's
start, and that the extension is stroked only, its tip at y = 99.75. Draws a relation with a dependency and an
aggregation
and the style `stroke:#f00` and asserts that both markers are stroked red, the dependency also filled red, and the
aggregation not filled.

##### CanvasNetMermaid-Rendering-EdgeBuilder-TerminalLabels: Cardinalities Stand Where Mermaid Puts Them

**Tests**: `EdgeBuilderTests.EdgeBuilder_CalcTerminalLabelPosition_StraightRoute_IsBesideTheWalkedPart`,
`EdgeBuilderTests.EdgeBuilder_DrawTerminals_ShortRoute_FailsLocatedAtTheLink`,
`EdgeBuilderTests.EdgeBuilder_DrawTerminals_HtmlLabels_StartCenteredEndNot`,
`EdgeBuilderTests.EdgeBuilder_DrawTerminals_SvgLabels_StartCenteredOnItsTextBox`

On a route straight down from (0, 0) to (0, 100), asserts that with a marker size of 10 the start label goes to
(-15, 17.5) (beside the middle of the first 35 pixels, 15 pixels to the right of the walking direction), the end label
to (10, 77.5) (walked from the end, then moved 5 pixels up and left), and that without a marker the start label goes
to (-10, 12.5). Asserts that a route of 20 pixels has no position for a start label and that drawing one there fails
with `mermaid-render-failed` located at the link (`t.mmd`, line 3, column 2), with a hint. Draws the HTML terminal
labels `1` and `many` of a relation with an extension at its end and asserts that a terminal label is an HTML label
16.5 pixels high, that the two labels are `edge-terminal` groups of the edge at (-15, 17.5) and (10, 77.5), drawn in
the 11 pixel font of `.edgeTerminals`, and that their box joins the start label's 9 by 12 pixel `foreignObject`, moved
by half the measured label to center the label on its position, with the end label's 36 by 12 pixel one starting at
its position. Draws the SVG terminal label
`0..1` and asserts that, at the start, the center of its box is the position (-15, 17.5), that, at the end, its box is
the text box moved to (10, 77.5), and that empty labels draw nothing and have no box.

##### CanvasNetMermaid-Rendering-EdgeBuilder-StateMarkers: Transitions End With Mermaid's Barb

**Tests**: `EdgeBuilderTests.EdgeBuilder_UserSpaceMarkerOf_StateAndErArrowTypes_GiveMermaidsMarkers`,
`EdgeBuilderTests.EdgeBuilder_Draw_StateTransition_BarbTakesTheTransitionColor`

Asserts that the arrow type `arrow_barb` at the end of an edge gives the user-space marker `barb` with the reference
point (19, 7) of `markers.js`, drawn in user units (not scaled with the stroke width), and that there is no `barb` at
the start of an edge. Draws a state transition (class `transition`, `arrow_barb` at its end) straight down from (0, 0)
to (0, 100) with the theme variables `transitionColor` `#00aa00` and `lineColor` `#aa0000`, and asserts that the line is
not shortened (`M0,0L0,50L0,100`), that it is stroked in the transition color, and that the barb is filled and stroked
in the transition color `#00aa00`, 1 pixel wide, by the more specific `defs [id$="-barbEnd"]` rule rather than in the
line color, with its tip on the end of the route at y = 100.

##### CanvasNetMermaid-Rendering-EdgeBuilder-ErMarkers: Relationships End With Mermaid's Cardinality Markers

**Tests**: `EdgeBuilderTests.EdgeBuilder_UserSpaceMarkerOf_StateAndErArrowTypes_GiveMermaidsMarkers`,
`EdgeBuilderTests.EdgeBuilder_DrawUserSpaceMarker_ErMarker_ScalesWithTheStrokeWidth`,
`EdgeBuilderTests.EdgeBuilder_Draw_ErRelationship_MarkersAreUnfilledWithWhiteRings`

Asserts that `only_one`, `zero_or_one`, `one_or_more`, and `zero_or_more` give, at the start and at the end, the
markers `onlyOne`, `zeroOrOne`, `oneOrMore`, and `zeroOrMore` with the reference points of `markers.js` - (0, 9) and
(18, 9), (0, 9) and (30, 9), (18, 18) and (27, 18), (18, 18) and (39, 18) - all scaled with the stroke width, that the
end `zeroOrMore` is a ring with its own `white` fill and a crow's foot without one, and that `md_parent` gives no
marker. Places the end `zeroOrMore` at the vertex (100, 50) on a path 2 pixels wide pointing right and asserts that it
is twice its size: the ring's box is (28, 38, 24, 24) (radius 12, 60 pixels behind the vertex), filled white and
stroked 2 pixels wide, and the crow's foot's box is (64, 32, 72, 36). Draws a relationship (class `relationshipLine`,
`only_one` at its start and `zero_or_more` at its end) straight down from (0, 0) to (0, 100) and asserts that the line
is not shortened (`M0,0L0,50L0,100`), that its three marker paths are stroked in the line color `#333333`, 1 pixel
wide, and that the bars and the crow's foot are unfilled while the ring is white.

##### CanvasNetMermaid-Rendering-EdgeBuilder-SvgLabelCenter: An SVG Label In Its Own Size Is Centered On Its Text

**Tests**: `EdgeBuilderTests.EdgeBuilder_DrawLabel_SvgInASmallerSize_IsCenteredOnItsDrawnText`

Measures the role `has` of an entity relationship diagram with `htmlLabels: false` and places it at (100, 50), and
asserts that it is an SVG label drawn in its own font size of 14 pixels, that the center of its drawn text is the
position (100, 50) to a millionth of a pixel, and that its background, measured in the diagram's root font size of 16
pixels, is taller than the text with its padding of 2 pixels on each side.
