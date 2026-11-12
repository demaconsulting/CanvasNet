### ShapeLibrary Unit Verification Design

This document describes the unit-level verification strategy for the `ShapeLibrary` unit in the
`DemaConsulting.CanvasNet.Mermaid.Rendering` namespace: the internal static `ShapeLibrary` class (the node shapes, the
class box of a class diagram, the state shapes and clusters of a state diagram, the entity box of an entity
relationship diagram, the intersect functions, the style split, and the subgraph rectangle, in `ShapeLibrary.cs` and
its nine partial files) and the shape registry `ShapeNames`.

#### Verification Approach

The unit is verified at two levels. The analytic tests in `ShapeLibraryTests.cs` and `ShapeNamesTests.cs` check the
registry, the intersect functions (with points worked out by hand, including the half-unit rounding offset of the line
intersection), the bounds of arcs, the style split, the paint of the node's own styles, the documented choices
(the legacy `ellipse`, the `state` shape, the upright fork, an image without a resolver, a shape not drawn), the class
box, the styles a diagram writes for its own nodes, the state shapes and clusters, and the entity box. They use a
`ShapeContext` of a default flowchart whose label
measurement is either the real `LabelBuilder` or a fixed label box of 40 by 24 pixels, so that the geometry can be
derived by hand from the shape formulas; the class box tests use the `ShapeContext` of a class diagram text (its
configuration from the frontmatter and the class diagram's style sheet) with the same fixed label box. The state tests
use the `ShapeContext` of a default state diagram (`stateDiagram-v2` with the state diagram's style sheet) whose label
measurement gives a box chosen by the text of the request (and records the requests where a test needs them), and the
entity box tests the `ShapeContext` of an entity relationship diagram text (its frontmatter `htmlLabels` and the
entity relationship style sheet) whose labels measure 40 by 24 pixels, or nothing for an empty text, with a fixed text
width where a test needs one. The mindmap shape tests use the `ShapeContext` of a mindmap text with the mindmap's style
sheet (and, where a test needs it, a site configuration with a `themeCSS` rule or a `flowchart.wrappingWidth`) whose
labels measure 40 by 24 pixels and whose label requests are recorded.

The vector tests in `ShapeLibraryVectorTests.cs` compare every case of the upstream vectors
`Fixtures/upstream-vectors/shapes.json` (296 cases) with this library. The vectors were recorded from the pinned Mermaid
in headless Chrome with the bundled Liberation Sans by `tools/visual-diff/mermaid/gen-shape-vectors.mjs`, which
inserts every node with Mermaid's own `insertNode` and records the node's size, its drawn box, the label's translation
and measured box, the computed paint of every drawn element, and 48 intersections (24 directions at two radii). The
cases cover the 53 documented shapes with a short HTML label, a wrapping HTML label, and an SVG label, the undocumented
shapes, the bracket shapes, label, font, padding, style, image, and icon variants, and the two mindmap shapes
(`defaultMindmapNode` and `mindmapCircle`) with short, long, and SVG labels and with the mindmap's padding of 10 and a
width of 100, the node's own fields applied as Mermaid's mindmap renderer gives them. The helper `ShapeVectors` reads the
vectors and draws a case's node, either with Mermaid's measured label box (to check the geometry alone) or with this
library's label measurement (to check the node size). No mocking is required; the internal types are reached through
the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `ShapeLibraryTests.cs`, `ShapeLibraryVectorTests.cs`, and `ShapeNamesTests.cs` (with the helper
`ShapeVectors.cs`) within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Rendering` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: A fixed label box (or a box chosen by the label's text) replaces the label measurement in the analytic
  geometry tests, and a fixed text width replaces Mermaid's `calculateTextWidth` in the entity box test of the minimum
  width
- **Fonts**: The bundled Liberation fonts for the measured labels
- **Files**: `Fixtures/upstream-vectors/shapes.json` of the test project (copied to the output folder)
- **Isolation**: Each test draws its own nodes; the vectors are read once and not changed

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. With Mermaid's label box, a node size, drawn box, or label place that differs from the vector by more than 0.02
pixel (0.06 pixel for the circles that Mermaid draws through rough.js and this library as true circles, a tolerance the
vector test also applies to the end state, although its rough.js outline is ported exactly and its analytic scenario
holds it to the thousandth of a pixel), or an intersection by more than 0.01 pixel, constitutes a failure; with
this library's label measurement, a label box or node size beyond 0.02 pixel for an HTML label or 2 pixels for an SVG
label (plus the documented differences of individual cases) constitutes a failure; a class box rectangle, divider, or
row offset, a state shape, cluster, or paint, or an entity table column, row, or divider that differs from the
hand-derived value, or a report count other than the expected, constitutes a failure.

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
wrapping, and an SVG label, and (a theory with two cases) that a name that is no shape (`notAShape`, the "not drawn by
this version" arm) and `erBox` on a node without the data of an entity (a shape a flowchart node cannot draw) fail
with `mermaid-render-failed` at the node's line, with a message that names the shape and a hint whose suggested shape
is a shape of the registry.

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

##### CanvasNetMermaid-Rendering-ShapeLibrary-ClassBox: The Class Box Is Mermaid's Class Box

**Tests**: `ShapeLibraryTests.ShapeLibrary_Draw_ClassBoxWithMembers_StacksGroupsAndDividers`,
`ShapeLibraryTests.ShapeLibrary_Draw_EmptyClassBox_DrawsTheExtraCompartment`,
`ShapeLibraryTests.ShapeLibrary_Draw_EmptyClassBoxHidden_DrawsTheNameAlone`,
`ShapeLibraryTests.ShapeLibrary_Draw_ClassBoxSvgLabels_SpacesTheMembersByTheTextPadding`,
`ShapeLibraryTests.ShapeLibrary_ClassHtmlLabels_FollowsMermaidsEvaluate`,
`ShapeLibraryTests.ShapeLibrary_Draw_ClassBoxStyles_PaintTheDividersAsTheOutline`

With every text measuring 40 by 24 pixels and the default padding of 12, asserts that a class with two attributes
`+int x` and one method is drawn by the `classBox` function as a rectangle of 84 by 168 pixels from (-42, -84), that
its two dividers lie at y = -36 (under the name) and y = 36 (under the attributes), that the attribute rows start at
x = -30 (the rectangle's left plus the padding) and are 24 pixels apart, and that the intersect function is the
rectangle's (a point far to the right gives (42, 0)); that an empty class gets the extra compartment, a rectangle of
64 by 84 pixels from (-32, -42) with both dividers at y = 6 and y = 24; that with `class.hideEmptyMembersBox` the empty
class is its name alone, 64 by 48 pixels from (-32, -24), without dividers; and that with SVG labels (a top-level
`htmlLabels` of `false`) the attribute rows are 27 pixels apart, their height plus Mermaid's text padding of 3. Asserts
that HTML labels are used when `htmlLabels` is not set or is `true`, not when the top-level `htmlLabels` is `false`,
and still when only `flowchart.htmlLabels` is `false` (a theory). Asserts that both dividers of a class with an
attribute and a method are stroked as its outline, without fill: red, 3 pixels wide, and dashed 5 by 5 with the styles
`stroke:#f00`, `stroke-width:3px`, `stroke-dasharray:5 5`, and `fill:#ff0`, and the theme's `#9370DB`, 1 pixel wide,
without styles.

##### CanvasNetMermaid-Rendering-ShapeLibrary-TrustedStyles: A Diagram's Own Styles Apply Without Reports

**Tests**: `ShapeLibraryTests.ShapeLibrary_Draw_TrustedNodeStyles_ApplyWithoutReports`

Draws a note node with the styles a class diagram writes for its notes (`text-align: left`, `white-space: nowrap`,
`fill: #fff5ad`, and `stroke: #aaaa33`) and asserts that, marked as trusted, it gives no `mermaid-style-ignored`
report, that the same styles from the user give two (the alignment and the white space, which this version does not
draw), and that the fill `#FFF5AD` is applied either way (a theory).

##### CanvasNetMermaid-Rendering-ShapeLibrary-StateShapes: State Shapes Are Mermaid's State Shapes

**Tests**: `ShapeLibraryTests.ShapeLibrary_Draw_StateRect_IsARoundedRectWithTheCssRadius`,
`ShapeLibraryTests.ShapeLibrary_Draw_RectWithTitle_PlacesTitleDescriptionAndDivider`,
`ShapeLibraryTests.ShapeLibrary_Draw_StateEnd_IsTheRoughJsOutline`,
`ShapeLibraryTests.ShapeLibrary_Draw_StateFork_IsTheBarWithoutThePadding`,
`ShapeLibraryTests.ShapeLibrary_Draw_StateShapes_PaintThroughTheStateSheet`,
`ShapeLibraryTests.ShapeLibrary_Draw_StateShapesInAFlowchart_TakeTheNodeRule`

Draws a state (`rect` with the radii 10 and 10, a padding of 8, `CenterLabel`, and the class `statediagram-state`)
around a 40 by 24 label and asserts that it is drawn by `roundedRect`, a rectangle of 56 by 40 pixels from (-28, -20)
whose corner arcs have the radius 5 of the state sheet's CSS rule, with its label centered at (-20, -12). Draws a state
with descriptions (`rectWithTitle`) whose title `first line` measures 61.094 by 24 pixels and whose description
`second line` 80.938 by 24, and asserts the boxes of Mermaid's SVG: a rectangle of 88.938 by 65 pixels from
(-44.469, -32.5) with corners of radius 5, the `state-divider` line across it at y = -4.5 stroked in `nodeBorder`, and
the narrower title centered over the description at (-30.547, -29.5). Draws an end state and asserts that its outer
ring is 14.013 by 14 pixels from x = -7 (the bulge of rough.js's last curve, as Chrome's box of Mermaid's path), that
its inner circle is 5.005 by 5 pixels, and that the node is as wide as the outer ring. Draws a fork with a padding of 8
and asserts that it is the bar alone, 70 by 10 pixels from (-35, -5), on whose top a point straight above ends at
(0, -5). With the default state sheet, asserts the paint of the first part of each shape: the start circle filled and
stroked in `specialStateColor`, 1 pixel wide; the end state's outer ring in `mainBkg`, stroked in `lineColor`, 2 pixels
wide, and its inner circle filled and stroked in `nodeBorder`, 2 pixels wide; the fork filled and stroked in
`lineColor`, the choice in `mainBkg` stroked in `nodeBorder`, and the note in `noteBkgColor` stroked in
`noteBorderColor`, each 1.3 pixels wide. With a flowchart's sheet, asserts that the start, end, fork, choice, and note
shapes are painted with the node rule's fill, stroke, and stroke width, and that a fork with the style
`fill:#ff0000` is filled red.

##### CanvasNetMermaid-Rendering-ShapeLibrary-StateClusters: State Clusters Are Mermaid's Clusters

**Tests**: `ShapeLibraryTests.ShapeLibrary_DrawCluster_RoundedWithTitle_DrawsOuterTitleAndInner`,
`ShapeLibraryTests.ShapeLibrary_DrawCluster_RoundedWithTitleNarrow_IsWidenedToTheTitle`,
`ShapeLibraryTests.ShapeLibrary_DrawCluster_DividerAndNoteGroup_AreDashedAndInvisible`

Draws a composite state (`roundedWithTitle`, padding 8, title 30 by 24) laid out at (100, 100) with a size of 200 by 150
and asserts, for the classes of a composite state and, in a theory, with the alternate depth's class
`statediagram-cluster-alt`, that it keeps the size of 200 by 150 with a `diff` of -8; that the outer rectangle is
(0, 25, 200, 150) with corners of radius 5 and filled in `compositeTitleBackground`; that the inner rectangle is
(0, 51, 200, 120), without rounded corners, filled `#ffffff`, or `#f0f0f0` at the alternate depth; and that the title is
placed at (85, 26), a pixel under the top. Draws a composite state given a width of 20 for its 30 pixel title and
asserts that it is widened to 38 pixels (the title and the padding) with a `diff` of 1 and the box (-19, -50, 38, 100).
Draws a concurrent region (`divider`) and a note group (`noteGroup`) at (50, 40) with a size of 100 by 80 and asserts
that the region is one rectangle filled `#f0f0f0`, stroked in `nodeBorder`, and dashed 10, 10, of the layout's size
with a `diff` of -8, and that the note group is one unpainted `cluster` path, without fill or stroke, with the box
(0, 0, 100, 80) and a `diff` of 0.

##### CanvasNetMermaid-Rendering-ShapeLibrary-ErBox: The Entity Box Is Mermaid's Entity Box

**Tests**: `ShapeLibraryTests.ShapeLibrary_Draw_ErBoxWithAttributes_LaysOutTheTable`,
`ShapeLibraryTests.ShapeLibrary_Draw_ErBoxWithoutAttributes_IsAtLeastTheMinimumWidth`,
`ShapeLibraryTests.ShapeLibrary_Draw_ErBoxSvgEmptyCell_WidensTheBox`,
`ShapeLibraryTests.ShapeLibrary_ErHtmlLabels_FollowsTheTopLevelSetting`

With every text measuring 40 by 24 pixels and `htmlLabels` unset, so the paddings of 20 and 15 grow to 25 and 18.75,
draws an entity with the attributes `int x PK` and `int y` and asserts that it is drawn by `erBox` as a table of
195 by 128.25 pixels from (-97.5, -64.125) (three columns of 65, the empty comment column dropped, a name row and two
rows of 42.75), that the name group is at (-20, -54.75), the type cells at (-85, -12) and (-85, 30.75), and the name
cells at x = -20, that there are 8 divider paths (four dividers, each filled and stroked), that the outline and the
rows are filled `#ECECFF`, `#FFFFFF`, and `#F1F1FF` in turn, and that a point far to the right ends at (97.5, 0). Draws
an entity without attributes whose text width is 10 and, in a theory, 70, and asserts a box of 100 by 84 pixels (the
minimum entity width) and of 80 by 84 pixels (the name and twice the padding of 20), from y = -42, with the name
centered at (-20, -12). With SVG text (`htmlLabels: false`), draws an entity with the attribute `int x PK` and an empty
comment, and asserts that the table is 195 by 85.5 pixels from (-97.5, -42.75) while the node's box reaches from
x = -97.5 to x = 110, where the empty comment cell stands. Asserts in a theory that `ErHtmlLabels` gives HTML cells,
grown paddings, and an HTML plain name when `htmlLabels` is unset; HTML cells without growth when it is `true`; and SVG
cells with grown paddings and an SVG plain name when it is `false`.

##### CanvasNetMermaid-Rendering-ShapeLibrary-SvgTextAnchor: SVG Node Text Is Centered Only When The Sheet Centers It

**Tests**: `ShapeLibraryTests.ShapeLibrary_Draw_SvgNodeText_IsCenteredOnlyWhenTheSheetCentersIt`

Draws a state node with the state diagram's sheet and a rectangle node with a flowchart's sheet, recording the label
requests, and asserts that the state's label is requested with its SVG text starting at its anchor (`CenterSvgText`
false), as the state sheet has no centering rule, and the flowchart's with its SVG text centered on its anchor
(`CenterSvgText` true), as its `.node .label text` rule centers it.

##### CanvasNetMermaid-Rendering-ShapeLibrary-MindmapShapes: The Mindmap Shapes Are Mermaid's

**Tests**: `ShapeLibraryTests.ShapeLibrary_Draw_DefaultMindmapNode_DrawsPathLineAndCenteredLabel`,
`ShapeLibraryTests.ShapeLibrary_Draw_DefaultMindmapNodeInAFlowchart_DrawsNoLine`,
`ShapeLibraryTests.ShapeLibrary_Draw_MindmapCircle_TakesTheWholePadding`,
`ShapeLibraryVectorTests.ShapeLibrary_Draw_UpstreamVectorWithMermaidLabel_MatchesGeometry`

Asserts that `defaultMindmapNode` around a label of 40 by 24 with a padding of 10 is drawn by its own handler as a box
of 80 by 34 from (-40, -17) (four half paddings beside the label and one half padding above and below), its body a path
with four quadratic corners filled with the section color `#ffff78` and not stroked, its line along the bottom edge
from (-40, 17), 80 wide, stroked `#ababff` 3 pixels wide, its label offset (-20, -12), and the rectangle's intersection
(a point at (200, 0) gives (40, 0)); that in a flowchart's sheet, which has no line rule, the same node is the same size
with its body in the node colors and no line; that `mindmapCircle` is a circle of 60 by 60 (the whole padding) where
`circle` is 50 by 50 (half of it), with an intersection at x 30; and that the mindmap vector cases of both shapes
(short, long, and SVG labels, the mindmap padding and width) give Mermaid's node size, drawn box, and label place.

##### CanvasNetMermaid-Rendering-ShapeLibrary-ClassPaint: A Mindmap's Sheet Paints By Element And Classes

**Tests**: `ShapeLibraryTests.ShapeLibrary_Draw_MindmapSheet_PaintsByElementAndClasses`,
`ShapeLibraryTests.ShapeLibrary_Draw_DefaultMindmapNode_DrawsPathLineAndCenteredLabel`

Asserts that with a `themeCSS` rule `.section-1 circle { fill: #ff0000 }` the `mindmapCircle` of a node of section 1 is
red while its `rect`, `rounded`, `hexagon`, `cloud`, and `bang` take the section color `cScale2`, none of them stroked,
that the root's `defaultMindmapNode` takes `git0`, and that the labels are requested in the section's `cScaleLabel2`
and the root's `gitBranchLabel0`; and the line paint of the scenario above.

##### CanvasNetMermaid-Rendering-ShapeLibrary-WrapWidth: The Node's Width Is The Wrapping Width

**Tests**: `ShapeLibraryTests.ShapeLibrary_Draw_NodeWidth_IsTheWrappingWidthWhenTruthy`

Asserts that with a `flowchart.wrappingWidth` of 150 the labels of nodes whose width is 100, 0, NaN, and unset are
requested with the wrapping widths 100, 150, 150, and 150 (the node's width when JavaScript finds it truthy).

##### CanvasNetMermaid-Rendering-ShapeLibrary-LabelBackground: An Icon Or An Image Gives The Label Its Background

**Tests**: `ShapeLibraryTests.ShapeLibrary_Draw_IconOrImage_GivesTheLabelItsBackground`

Asserts that the label of a node with the icon `fa fa-book` or the image `a.png` is requested with a background, and
the label of a node with an empty icon or neither is not, as JavaScript's `!!` tests them.
