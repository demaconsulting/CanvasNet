### EdgeBuilder

![Rendering Structure](MermaidRenderingView.svg)

The `EdgeBuilder` unit is the third software unit in the `Rendering` subsystem. It is the internal static partial
class `EdgeBuilder` in four source files - `EdgeBuilder.cs` (the route, the stroke, the label, and the records
`EdgeEnd`, `EdgeRoute`, `EdgeDrawing`, and `EdgeLabelDrawing`), `EdgeBuilder.Curves.cs` (d3-shape's line generator and
edge curves, with the path context `ICurveContext` and its implementation `CurvePath`), `EdgeBuilder.Markers.cs` (the
flowchart, class diagram, state diagram, and entity relationship diagram markers, with the records `MarkerPaint`,
`UserSpaceMarker`, and `MarkerPart`), and
`EdgeBuilder.Terminals.cs` (the terminal labels of an edge). All types are in the
`DemaConsulting.CanvasNet.Mermaid.Rendering` namespace.

#### Purpose

`EdgeBuilder` is Mermaid's edges: `rendering-elements/edges.js` (`insertEdge`, `insertEdgeLabel`,
`positionEdgeLabel`), `edgeMarker.ts`, the flowchart, class, state, and entity relationship markers of `markers.js`,
`utils/lineWithOffset.ts`, and
the edge helpers of `utils.ts` (including `calcTerminalLabelPosition`), with the line generator and the edge curves of
d3-shape. It turns the route that dagre gives an edge into the drawn path - ended on the node outlines, cut at subgraph
borders, corners fixed, ends pulled back for the arrowheads, drawn with the edge's curve - strokes it by the link
stroke and the edge's styles, puts the markers on its ends, measures, places, and draws its label, and places and draws
its terminal labels (a class relation's cardinalities). The unit's behavior is specified by the
`CanvasNetMermaid-Rendering-EdgeBuilder-*` requirements in `docs/reqstream/canvas-net-mermaid/rendering/edge-builder.yaml`.

#### Data Model

- `EdgeEnd(Box, Intersect)` - one end as `insertEdge` sees it: the node's box after the layout and its intersect
  function (null for a laid-out subgraph, which keeps the layout's end point).
- `EdgeRoute(Data, Id, Points)` - an edge to draw: its `LayoutEdge`, the id of the drawn edge, the layout's points,
  the arrow types of its ends, and the boxes of the subgraphs it leaves or enters (`FromCluster`, `ToCluster`).
- `EdgeDrawing(Items, Bounds, UpdatedPath, OriginalPath, PathData)` - a drawn edge: the path and markers, the box of
  the path geometry (markers and stroke excluded, as SVG's `getBBox`), the cut route when it changed, the layout's
  route, and the SVG path data Mermaid writes.
- `EdgeLabelDrawing(Items, Bounds)` - a placed label: its backgrounds and text in a group at its center, and its box;
  also the terminal labels of an edge, as `edge-terminal` groups.
- `MarkerPaint(Fill, Stroke)` with `StrokeWidth` (the width the style sheet gives a user-space marker, 1 by default),
  and the private `MarkerGeometry` - a marker's paint and its view box, size, reference point, stroke width, and
  direction: `point` (view box 10, size 8), `circle` (view box 10, size 11), and `cross` (view box 11, size 11, stroke
  2), each with a start variant.
- `UserSpaceMarker(Type, RefX, RefY, Parts)` and `MarkerPart(Segments, Fill)` - a marker of `markers.js` that has no
  view box: its type (the CSS class its paint comes from), its reference point in marker units, its shapes in marker
  units, each with the `fill` attribute of its own element (the lollipop circle's `transparent`, the entity
  relationship rings' `white`) or null when it inherits the marker's fill, and `ScalesWithStroke`, which tells whether
  a marker unit is the path's stroke width. The class diagram markers and the state diagram's `barb` are drawn in user
  units (`markerUnits="userSpaceOnUse"`); the entity relationship markers set no `markerUnits`, so they take SVG's
  default `strokeWidth` and scale with the stroke width of the path (`ScalesWithStroke`). The `aggregation` and
  `composition` markers are the diamond (18, 7), (9, 13), (1, 7), (9, 1); the `extension` the triangle (1, 1), (1, 13),
  (18, 7) at the end and (1, 7), (18, 13), (18, 1) at the start; the `dependency` the open arrowhead; the `lollipop` a
  circle of radius 6 at (7, 7). The reference points are (1, 7) at the end and (18, 7) at the start for the first
  three, (13, 7) and (6, 7) for the dependency, and (1, 7) and (13, 7) for the lollipop. The `barb` (end only;
  `markers.js` defines no start) is the polygon (19, 7), (9, 13), (14, 7), (9, 1) with its reference point at the tip
  (19, 7). The entity relationship markers, each at both ends, are made of bars (lines), crow's feet (two quadratic
  curves through the point 18 units ahead or behind), and rings of radius 6 filled white: `onlyOne` two bars (reference
  points (0, 9) at the start and (18, 9) at the end), `zeroOrOne` a ring and a bar ((0, 9) and (30, 9)), `oneOrMore` a
  crow's foot and a bar ((18, 18) and (27, 18)), and `zeroOrMore` a ring and a crow's foot ((18, 18) and (39, 18)).
- The marker types whose colored copy is also filled with the edge's stroke color: `point`, `barb`, `composition`, and
  `dependency` (the `fill` flag of `edgeMarker.ts`'s `arrowTypesMap`).
- The constants of the terminal labels: the 25 pixel walk and the 10 pixel offset of `calcTerminalLabelPosition`, the
  marker size of 10 for an end with an arrow type, the 5 pixel shift of an end label, and the 9 pixels per character by
  12 pixels of an HTML terminal label's `foreignObject` (`setTerminalWidth`).
- `ICurveContext` and `CurvePath` - the context a d3 curve writes to (move, line, cubic Bezier, close) and the path it
  builds: scene segments and the SVG path text with numbers rounded to three decimals as d3-path writes them.
- The marker offsets of `lineWithOffset.ts` (4 pixels for `arrow_point`, 17.25 for `aggregation`, `extension`, and
  `composition`, 6 for `dependency`, 13.5 for `lollipop`, and 0 for `arrow_barb`; the entity relationship markers are
  not in `markerOffsets`, so neither a transition nor a relationship line is shortened) and the curves `basis` (the
  default),
  `linear`, `natural`, `step`, `stepBefore`, `stepAfter`, `monotoneX`, `monotoneY`, `cardinal`, `catmullRom`,
  `bumpX`, and `bumpY`, with `rounded` drawn linear over rounded points.

#### Key Methods

- `MeasureLabel(LayoutEdge edge, string label, ShapeContext context)` - the label as `insertEdgeLabel` creates it:
  wrapped at 200 pixels, with a background, not prepared again (`Prepare = false`), in the style sheet's edge label
  font (`EdgeLabel`: a flowchart's node label rule, a class diagram's `.edgeLabel`, a state diagram's
  `.label div .edgeLabel`, an entity relationship diagram's `.label` color at the 14 pixels of `.edgeLabel .label`)
  with the edge's label styles, its SVG lines wrapped in, and its background measured in, the root font size of the
  sheet (`UnifiedStyles.RootText`), as `createText` measures the label in the edge labels' root group before it is
  moved under the rules that give it its own size; an empty label has an empty box.
- `Draw(EdgeRoute edge, EdgeEnd? tail, EdgeEnd? head, ShapeContext context)` - `insertEdge`: replaces the first and
  last points with the intersections of the end nodes' outlines (when there are more than two points), cuts the route
  at the subgraph it enters (`CutPathAtIntersect`) and, reversed, at the one it leaves, drops the points whose y is not
  a number, rounds off right-angle corners (`FixCorners`, except for `rounded`), and draws the points with the curve,
  pulling the ends back by the marker offsets (`CurveWithOffsets`; `ApplyMarkerOffsetsToPoints` and `RoundedPath` with
  a radius of 5 for `rounded`). The path is styled by the style sheet's rules for its classes in sheet order
  (`edge-thickness-*`, `edge-pattern-*`, the edge's classes, and an animation class) and then by its inline styles, and
  the markers are drawn with `DrawMarkers`, which is given the path's stroke width. When the middle point of the route
  is not found in the path text (`IsLabelCoordinateInPath`), the route counts as changed.
- `LabelPosition(string label, double? x, double? y, EdgeDrawing drawing, double titleMargin)` -
  `positionEdgeLabel`: null without text; the middle by length of the changed route (`CalcLabelPosition`), else the
  layout's label position; moved down by half the subgraph title margin.
- `DrawLabel(LabelLayout label, ShapePoint position, LayoutEdge edge, string id, UnifiedStyles styles)` - the label
  centered on its position (an HTML label by its box, an SVG label by the union of the boxes of its drawn lines, as
  `insertEdgeLabel` measures the text element once it has its own font size), over the backgrounds that
  the diagram's style sheet names: for an HTML label the rules of `HtmlEdgeLabelBackgrounds` painted in order over the
  box (a flowchart's `.labelBkg` and `.edgeLabel p`, a class diagram's `.labelBkg` and `.edgeLabel .label span`), and
  for an SVG label the rectangle rule of `SvgEdgeLabelBackground` (a flowchart's `.edgeLabel rect`, a class diagram's
  `.edgeLabel .label rect`) at its opacity, half by default. An SVG label drawn in a smaller size than the root font
  (an entity relationship role of 14 pixels in a 16 pixel diagram) is centered on its 14 pixel text, while its
  background, measured at 16 pixels, is larger than the text with its padding and not centered on it, as in Mermaid.
- `Line(points, curve)` and `CreateCurve(name, context)` - d3's `line().curve(curve)` with every point defined; a name
  that is not listed draws `basis`, and a number that is not finite ends the drawn path as a browser stops at `NaN`.
- `CutPathAtIntersect`, `Intersection`, `FixCorners`, `CalcLabelPosition`, `IsLabelCoordinateInPath` - the helpers of
  `edges.js` and `utils.ts` with their quirks (a vertical segment gives the outside point back; the label search is a
  text search of the rounded coordinates in the rounded path data).
- `MarkerOf(arrowType, start)`, `DrawMarkers`, `DrawMarker`, `StartOf`, `EndOf` - the marker of an arrow type, placed
  as SVG places a marker: its reference point on the path's end vertex, scaled from its view box to its size in user
  units, turned along the path's tangent there (`orient="auto"`); the `point` marker is the miter-stroked triangle
  clipped to its view box as one filled polygon. `DrawMarkers` draws at each end the view-box marker of its arrow type,
  else its user-space marker, else none (as for an entity relationship diagram's `md_parent`, which Mermaid's map does
  not know). The markers take the style sheet's `.marker` paint (with `.marker.cross` for
  the cross, or the type's own rule, such as `.composition`, for a class marker; for the `barb`, which has no class,
  the state sheet's `[id$="-barbEnd"]` rule under its more specific `defs [id$="-barbEnd"]` rule, which fills and
  strokes it in `transitionColor`, 1 pixel wide, where the line takes `lineColor`), or the first `stroke:` of the
  edge's styles for the stroke and, for the types whose colored copy is filled, the fill (Mermaid's colored marker
  copies). The entity relationship markers take `.marker` (an important `fill: none` and the line color), so they are
  unfilled, but their rings keep their own white fill, because the attribute of the `circle` element wins over the fill
  it would inherit from the marker.
- `UserSpaceMarkerOf(arrowType, start)` and `DrawUserSpaceMarker(marker, x, y, angle, paint, id, strokeWidth)` - the
  markers without a view box (`aggregation`, `extension`, `composition`, `dependency`, and `lollipop`, each with its
  start variant; `arrow_barb` at the end only; and `only_one`, `zero_or_one`, `one_or_more`, and `zero_or_more` at
  either end, through `ErMarkerOf`; null for any other type) drawn at a vertex: every shape moved so the reference
  point is on the vertex, scaled by the path's stroke width when the marker `ScalesWithStroke` (its arcs' radii and its
  stroke width too), and turned by the path's direction there, filled with its own fill attribute or the marker's (a
  transparent fill paints nothing, so the
  lollipop is an unfilled circle and the extension takes its rule's transparent fill), and stroked with the marker's
  stroke at the rule's width. With the reference points above, the end extension's tip is 17 pixels ahead of the
  vertex and the start extension's 17 pixels behind it, the start lollipop is a circle 6 pixels behind the vertex, and
  the `barb`'s tip is on the vertex. A `zeroOrMore` end on a path 2 pixels wide is twice its size: its ring has a
  radius of 12 pixels, its center 60 pixels behind the vertex, and is stroked 2 pixels wide.
- `MeasureTerminal(LayoutEdge edge, string text, ShapeContext context)` - a terminal label as `insertEdgeLabel`
  creates it (`createLabel`): a plain string, never wrapped, without a background, in the font of the style sheet's
  `TerminalLabel` (the size of `.edgeTerminals` on the root font, colored as an edge label for an HTML label or with
  the root's fill for an SVG label), with the edge's label styles parsed without reports (they were reported with the
  edge's own label).
- `CalcTerminalLabelPosition(double markerSize, bool atEnd, IReadOnlyList<ShapePoint> points)` - Mermaid's
  `calcTerminalLabelPosition` for `start_right` and `end_left`: the point 25 pixels plus the marker size along the
  route from its start (or, for the end label, from its end, walking the reversed route) by `CalculatePoint`, and the
  position beside the middle of that walked part, 10 pixels plus half the marker size to the right of the walking
  direction; an end label is moved 5 pixels up and to the left. A route too short to walk gives null (where
  Mermaid's `calculatePoint` throws).
- `DrawTerminals(edge, id, startLabel, endLabel, arrowTypeStart, arrowTypeEnd, drawing, context)` and `DrawTerminal` -
  the start label, then the end label, of a drawn edge (each only when not empty), each measured and placed beside the
  drawn route (the cut route when it changed, else the layout's), with a marker size of 10 for an end whose arrow type
  is not empty (`none` included, as JavaScript reads any non-empty string as true). Each label is an `edge-terminal`
  group at its position. The start label is centered on it (an HTML label by its box, an SVG label by the box of its
  text), as Mermaid's inner group is; the end label is not centered, because Mermaid appends it to the outer group, so
  its box starts at its position. The box of an HTML terminal label is that of its `foreignObject`, 9 pixels per
  character wide and 12 pixels high.
- `CalculatePoint(points, distance)` - Mermaid's `calculatePoint`, shared by the middle of a changed route and the
  terminal labels: the point a distance along a route, rounded to 5 decimals, or null when the route is shorter.

**Preconditions and postconditions**: the methods are pure functions of their arguments and the style sheet; the class
is stateless and safe for concurrent use.

#### Error Handling

No method throws for a degenerate route: an empty route draws nothing, a point whose y is not a number is dropped, a
number that is not finite ends the path, and a label without a layout position gets the origin (the browser ignores
Mermaid's invalid transform there). The one exception is a terminal label on a drawn route too short to place it
(shorter than 25 pixels plus the marker size): `DrawTerminals` throws a `MermaidRenderException`
(`mermaid-render-failed`) located at the link, with a message that names the label and the link and a hint to lengthen
the link, where Mermaid's render fails ("Could not find a suitable point for the given distance"). A style declaration
outside the supported CSS subset is reported as `mermaid-style-ignored` by the style parser. Null arguments throw
`ArgumentNullException`.

#### Dependencies

- **Rendering subsystem** - `ShapeLibrary` (`SplitStyles`, `LabelFontOf`, `ShapeContext`, `NodeBox`, `ShapeIntersect`,
  `ShapePoint`), `LabelBuilder` (`LabelRequest`, `LabelLayout`, `SvgBackgroundPadding`), `LayoutEdge` (with
  `StartLabelRight` and `EndLabelLeft`)
- **Configuration subsystem** - `UnifiedStyles` (its `DiagramStyles.Root` rule, `RootText`, and `Resolve` of the
  marker selectors), `StyleDeclarations`, `StyleDeclarationSet`, `MermaidConfig` (the curve and HTML label settings)
- **Scenegraph subsystem** - `ScenePath`, `SceneSegment`, `SceneStroke`, `SceneGroup`, `SceneItem`, `SceneRect`,
  `Scene.BoundsOf`
- **Package root** - `MermaidRenderException` and `MermaidDiagnosticCodes` (the terminal label failure)
- **Core `CanvasNet`** - `Rgba32`
- **.NET base class library** - `System.Math`, `System.Text.RegularExpressions`

#### Callers

`ClusterLayout` measures every edge label with `MeasureLabel`, draws every edge with `Draw`, places and draws the
labels with `LabelPosition` and `DrawLabel`, and draws the terminal labels of every edge that has them with
`DrawTerminals`. The edges, markers, and terminal labels of a class diagram come from the layout data of `ClassModel`
(the `Class` subsystem): its relations carry the class arrow types and the cardinalities as `StartLabelRight` and
`EndLabelLeft`. The transitions of a state diagram come from the layout data of `StateModel` (the `State` subsystem),
each ending in `arrow_barb` with the class `transition`; the relationships of an entity relationship diagram come
from `ErModel` (the `Er` subsystem), with the cardinality arrow types at their ends (or `md_parent`, which draws no
marker), the class `relationshipLine`, and their roles as labels. `MindmapRenderer` (the `Mindmap` subsystem) calls
`Draw` itself for every edge of a mindmap, without `ClusterLayout`: the route is the start, middle, and end points of
the `Cose` layout, the curve `basis`, the thickness `normal`, no arrow type, and the classes `edge`,
`section-edge-k`, and `edge-depth-n`, whose single-class rules of the mindmap sheet give the edge its color and width;
both ends are given without an intersect function, so the route is not cut at the node outlines, as in Mermaid, and a
point that is not a number (cytoscape's bad line) is dropped by the same filter as any other route. The tests in
`EdgeBuilderTests.cs` call the unit directly through `InternalsVisibleTo`.

#### Known Differences From Mermaid

- Only the markers a flowchart, a class diagram, a state diagram, or an entity relationship diagram requests are ported
  (`point`, `circle`, and `cross`, and `aggregation`, `extension`, `composition`, `dependency`, and `lollipop`, each
  with its start variant; the `barb` at the end; and `onlyOne`, `zeroOrOne`, `oneOrMore`, and `zeroOrMore` at either
  end; not the markers of other diagram types or of the `neo` look), and only the two terminal labels a class relation
  sets (`startLabelRight` and `endLabelLeft`; not `startLabelLeft` or `endLabelRight`, which no drawn diagram sets).
- The `point` marker is clipped to its view box exactly; the ring of the `circle` marker is not clipped, so up to 0.55
  pixel of ring can show beyond the view box at its four extremes.
- The viewport of a marker without a view box (`markerWidth` by `markerHeight`) is not modeled as a clip. Every shape
  of the class markers lies inside it except the miter tip of the stroke of the `extensionStart` marker, so up to half
  a pixel of that tip shows where Mermaid clips it. The curves of the entity relationship markers touch the edge of
  their viewport (at x 0 and at `markerWidth`), where Mermaid clips half the stroke of their tips; the port draws those
  half strokes. Both differences change pixels only, not the layout.
- The `foreignObject` of an HTML terminal label (9 pixels per character by 12 pixels) is its box but does not clip its
  text: the line of the label (16.5 pixels high in the default 11 pixel terminal font) is drawn whole, where the
  browser clips it to the 12 pixels of the `foreignObject`.
- An animated edge is drawn as the first frame of its animation (dashed 9,5).
- `Math.Sin` and `Math.Acos` of .NET can differ from the browser's in the last bit (seen in the path text of the
  `rounded` curve, with no visible effect).
