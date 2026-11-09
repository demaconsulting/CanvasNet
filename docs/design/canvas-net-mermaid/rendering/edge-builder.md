### EdgeBuilder

![Rendering Structure](MermaidRenderingView.svg)

The `EdgeBuilder` unit is the third software unit in the `Rendering` subsystem. It is the internal static partial
class `EdgeBuilder` in three source files - `EdgeBuilder.cs` (the route, the stroke, the label, and the records
`EdgeEnd`, `EdgeRoute`, `EdgeDrawing`, and `EdgeLabelDrawing`), `EdgeBuilder.Curves.cs` (d3-shape's line generator and
edge curves, with the path context `ICurveContext` and its implementation `CurvePath`), and `EdgeBuilder.Markers.cs`
(the flowchart markers, with the record `MarkerPaint`). All types are in the `DemaConsulting.CanvasNet.Mermaid.Rendering`
namespace.

#### Purpose

`EdgeBuilder` is Mermaid's edges: `rendering-elements/edges.js` (`insertEdge`, `insertEdgeLabel`,
`positionEdgeLabel`), `edgeMarker.ts`, the flowchart markers of `markers.js`, `utils/lineWithOffset.ts`, and the edge
helpers of `utils.ts`, with the line generator and the edge curves of d3-shape. It turns the route that dagre gives an
edge into the drawn path - ended on the node outlines, cut at subgraph borders, corners fixed, ends pulled back for the
arrowheads, drawn with the edge's curve - strokes it by the link stroke and the edge's styles, puts the markers on its
ends, and measures, places, and draws its label. The unit's behavior is specified by the
`CanvasNetMermaid-Rendering-EdgeBuilder-*` requirements in `docs/reqstream/canvas-net-mermaid/rendering/edge-builder.yaml`.

#### Data Model

- `EdgeEnd(Box, Intersect)` - one end as `insertEdge` sees it: the node's box after the layout and its intersect
  function (null for a laid-out subgraph, which keeps the layout's end point).
- `EdgeRoute(Data, Id, Points)` - an edge to draw: its `LayoutEdge`, the id of the drawn edge, the layout's points,
  the arrow types of its ends, and the boxes of the subgraphs it leaves or enters (`FromCluster`, `ToCluster`).
- `EdgeDrawing(Items, Bounds, UpdatedPath, OriginalPath, PathData)` - a drawn edge: the path and markers, the box of
  the path geometry (markers and stroke excluded, as SVG's `getBBox`), the cut route when it changed, the layout's
  route, and the SVG path data Mermaid writes.
- `EdgeLabelDrawing(Items, Bounds)` - a placed label: its backgrounds and text in a group at its center, and its box.
- `MarkerPaint(Fill, Stroke)` and the private `MarkerGeometry` - a marker's paint and its view box, size, reference
  point, stroke width, and direction: `point` (view box 10, size 8), `circle` (view box 10, size 11), and `cross` (view
  box 11, size 11, stroke 2), each with a start variant.
- `ICurveContext` and `CurvePath` - the context a d3 curve writes to (move, line, cubic Bezier, close) and the path it
  builds: scene segments and the SVG path text with numbers rounded to three decimals as d3-path writes them.
- The marker offsets of `lineWithOffset.ts` (4 pixels for `arrow_point`) and the curves `basis` (the default),
  `linear`, `natural`, `step`, `stepBefore`, `stepAfter`, `monotoneX`, `monotoneY`, `cardinal`, `catmullRom`,
  `bumpX`, and `bumpY`, with `rounded` drawn linear over rounded points.

#### Key Methods

- `MeasureLabel(LayoutEdge edge, string label, ShapeContext context)` - the label as `insertEdgeLabel` creates it:
  wrapped at 200 pixels, with a background, not prepared again (`Prepare = false`), in the style sheet's label font
  with the edge's label styles; an empty label has an empty box.
- `Draw(EdgeRoute edge, EdgeEnd? tail, EdgeEnd? head, ShapeContext context)` - `insertEdge`: replaces the first and
  last points with the intersections of the end nodes' outlines (when there are more than two points), cuts the route
  at the subgraph it enters (`CutPathAtIntersect`) and, reversed, at the one it leaves, drops the points whose y is not
  a number, rounds off right-angle corners (`FixCorners`, except for `rounded`), and draws the points with the curve,
  pulling the ends back by the marker offsets (`CurveWithOffsets`; `ApplyMarkerOffsetsToPoints` and `RoundedPath` with
  a radius of 5 for `rounded`). The path is styled by the style sheet's rules for its classes in sheet order
  (`edge-thickness-*`, `edge-pattern-*`, the edge's classes, and an animation class) and then by its inline styles, and
  the markers are drawn with `DrawMarkers`. When the middle point of the route is not found in the path text
  (`IsLabelCoordinateInPath`), the route counts as changed.
- `LabelPosition(string label, double? x, double? y, EdgeDrawing drawing, double titleMargin)` -
  `positionEdgeLabel`: null without text; the middle by length of the changed route (`CalcLabelPosition`), else the
  layout's label position; moved down by half the subgraph title margin.
- `DrawLabel(LabelLayout label, ShapePoint position, LayoutEdge edge, string id, FlowchartStyles styles)` - the label
  centered on its position (an HTML label by its box, an SVG label by the box of its text), over the `.labelBkg` and
  `.edgeLabel p` backgrounds of an HTML label or the `.edgeLabel rect` background (at its opacity, half by default) of
  an SVG label.
- `Line(points, curve)` and `CreateCurve(name, context)` - d3's `line().curve(curve)` with every point defined; a name
  that is not listed draws `basis`, and a number that is not finite ends the drawn path as a browser stops at `NaN`.
- `CutPathAtIntersect`, `Intersection`, `FixCorners`, `CalcLabelPosition`, `IsLabelCoordinateInPath` - the helpers of
  `edges.js` and `utils.ts` with their quirks (a vertical segment gives the outside point back; the label search is a
  text search of the rounded coordinates in the rounded path data).
- `MarkerOf(arrowType, start)`, `DrawMarkers`, `DrawMarker`, `StartOf`, `EndOf` - the marker of an arrow type, placed
  as SVG places a marker: its reference point on the path's end vertex, scaled from its view box to its size in user
  units, turned along the path's tangent there (`orient="auto"`); the `point` marker is the miter-stroked triangle
  clipped to its view box as one filled polygon. The markers take the style sheet's `.marker` paint, or the first
  `stroke:` of the edge's styles (Mermaid's colored marker copies).

**Preconditions and postconditions**: the methods are pure functions of their arguments and the style sheet; the class
is stateless and safe for concurrent use.

#### Error Handling

No method throws for a degenerate route: an empty route draws nothing, a point whose y is not a number is dropped, a
number that is not finite ends the path, and a label without a layout position gets the origin (the browser ignores
Mermaid's invalid transform there). A style declaration outside the supported CSS subset is reported as
`mermaid-style-ignored` by the style parser. Null arguments throw `ArgumentNullException`.

#### Dependencies

- **Rendering subsystem** - `ShapeLibrary` (`SplitStyles`, `LabelFontOf`, `ShapeContext`, `NodeBox`, `ShapeIntersect`),
  `LabelBuilder` (`LabelRequest`, `LabelLayout`, `SvgBackgroundPadding`), `LayoutEdge`
- **Configuration subsystem** - `FlowchartStyles`, `StyleDeclarations`, `StyleDeclarationSet`, `MermaidConfig` (the
  curve and HTML label settings)
- **Scenegraph subsystem** - `ScenePath`, `SceneSegment`, `SceneGroup`, `SceneItem`, `SceneRect`, `Scene.BoundsOf`
- **Core `CanvasNet`** - `Rgba32`
- **.NET base class library** - `System.Math`, `System.Text.RegularExpressions`

#### Callers

`ClusterLayout` measures every edge label with `MeasureLabel`, draws every edge with `Draw`, and places and draws the
labels with `LabelPosition` and `DrawLabel`. The tests in `EdgeBuilderTests.cs` call the unit directly through
`InternalsVisibleTo`.

#### Known Differences From Mermaid

- Only the markers a flowchart requests are ported (`point`, `circle`, and `cross` with their start variants; not
  `barb` or the markers of other diagram types), and the terminal labels of edges (`startLabelLeft` and the others) are
  not drawn because the flowchart never sets them.
- The `point` marker is clipped to its view box exactly; the ring of the `circle` marker is not clipped, so up to 0.55
  pixel of ring can show beyond the view box at its four extremes.
- An animated edge is drawn as the first frame of its animation (dashed 9,5).
- `Math.Sin` and `Math.Acos` of .NET can differ from the browser's in the last bit (seen in the path text of the
  `rounded` curve, with no visible effect).
