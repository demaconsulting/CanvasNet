### MindmapRenderer

![MermaidMindmap Structure](MermaidMindmapView.svg)

The `MindmapRenderer` unit is the second software unit in the `Mindmap` subsystem. It is one source file,
`MindmapRenderer.cs`, in the `DemaConsulting.CanvasNet.Mermaid.Mindmap` namespace, and defines the internal sealed
class `MindmapRenderer`. The file carries the provenance header comment for the parts of Mermaid it ports.

#### Purpose

`MindmapRenderer` draws one render of a `MindmapDiagram` into a scene as the `draw` of Mermaid's `mindmapRenderer.ts`
and the cose-bilkent renderer of `layout-algorithms/cose-bilkent/render.ts` draw the mindmap, with the paint of the
mindmap style sheet of the render's theme. It exists so that the node shapes and sizes, the positions, the edges, the
colors, and the size of a mindmap are Mermaid's for the same text, configuration, and theme, and so that what Mermaid's
mindmap asks for but this library does not draw is reported.

The unit's behavior is specified by the `CanvasNetMermaid-Mindmap-MindmapRenderer-*` requirements in
`docs/reqstream/canvas-net-mermaid/mindmap/mindmap-renderer.yaml`.

#### Data Model

**The constants of Mermaid's `draw`**: the padding of a rounded rectangle (15) and of a rectangle (10).

**A renderer instance** draws one render on one thread and holds the diagram, the `DiagramRenderContext` of the render
(configuration, report sink, fonts, options, and cancellation token), and a `LayoutWorkMeter` of `MaxLayoutWork`
located at the diagram keyword and bound to the cancellation token of the render. After a draw, `Sizes` holds the node
sizes the layout was given, in node order (empty before a draw and for an empty mindmap), for the reference tests.

**The node data** is a `LayoutNode` per node, as `getData` gives it: the id (the node's number), the DOM id
`node_<id>`, the label (the description) of type `markdown`, the shape, the width `mindmap.maxNodeWidth`, the padding
`mindmap.padding` (doubled for a rectangle, a rounded rectangle, and a hexagon), the classes, the look, the icon, and
the location.

**The scene** has one group with the role `root`, located at the keyword, holding the groups `edgePaths` (the drawn
edges, each with the edge's id), `edgeLabels` (always empty, as in Mermaid), and `nodes` (one group per node, with the
role `node-group`, the DOM id, and the node's location, at the center the layout gives it). The view box is the box of
the drawing, in single precision, grown by `mindmap.padding` on every side.

#### Key Methods

- **`Draw()`** follows Mermaid's `draw`:
  - It reports the unsupported features (`ReportUnsupported`). An empty mindmap then returns a scene with nothing drawn
    and the view box of the empty box grown by the padding (20 by 20 at the default padding).
  - It charges the meter for the node data (nodes, edges, and 1), creates a `LabelBuilder` over the fonts of the render
    that charges the meter, and a `ShapeContext` with the configuration and the `MindmapStyles` of the configuration.
  - For each node in order, it takes the node data (`DataOf`) with the changes per shape (`Prepared`), checks the label
    (`CheckLabel`), draws the node with `ShapeLibrary.Draw`, and keeps the box of the drawn node in single precision
    (`Box32`) as the size the layout is given.
  - It lays out the sizes and the edges (each the indexes of the parent and the child, in the order of
    `generateEdges`) with `CoseLayout.Run`, which gives each node's center and each edge's start, middle, and end
    points.
  - For each edge, it charges the meter 4 and draws the edge with `EdgeBuilder.Draw` through the three points, with
    the `basis` curve, a normal thickness, and the edge's classes; both ends are boxes at the layout's centers without
    an intersect function, so the route is not cut at the node outlines, and a point that is not a number is dropped by
    the edge builder, as Mermaid's `insertEdge` drops it.
  - It places each node's group at its center (a center that is not a number places it at the origin, as the browser
    ignores an invalid transform) and grows the box of the drawing by the node's box at that center, skipping boxes
    that are not finite.
- **`DataOf(node, config)`** gives the node data of `getData` before `draw` changes it.
- **`Prepared(data)`** applies the changes `draw` makes per shape: a rounded rectangle gets the padding 15 and the width
  0, a rectangle the padding 10 and the width 0, and a hexagon the width 0. A width of 0 makes the label wrap at
  `flowchart.wrappingWidth` rather than at `mindmap.maxNodeWidth`. Mermaid's `circle` branch is never taken (a circle
  node's shape is `mindmapCircle`), and the radius, taper, stroke, and height it sets change nothing the classic shapes
  draw, so they are not ported.
- **`Box32(bounds)`** gives the box as the browser's `getBBox` gives it: each side rounded to single precision, and the
  width and height subtracted in single precision. Boxes on the 1/64 pixel grid are unchanged.
- **`ReportUnsupported(config, sink)`** reports, in order: a look other than `classic` (`mermaid-look-unsupported` at
  the `look` key), a layout other than `cose-bilkent` (`mermaid-layout-unsupported` at the `layout` key: "the mindmap is
  laid out with cose-bilkent"), then for each node its icon (`mermaid-icon-unsupported` at the icon: "the node keeps
  its shape and label") and a Font Awesome icon in its label (`mermaid-icon-unsupported` at the node, found by
  `LabelBuilder.ContainsFontAwesome`), each with a hint.
- **`CheckLabel(data)`** charges the meter for the label's length plus 1 and checks the label as `labelHelper` builds it
  (entities decoded, then sanitized) against the render's `MaxLabelLength`, before it is measured; a label already
  longer than the limit is refused without being decoded.

**Preconditions and postconditions**: the diagram was parsed and the context belongs to one render; the result is a
scene whose view box is the natural size, or an exception.

#### Error Handling

- A label longer than the render's `MaxLabelLength` is a `MermaidRenderException` with code `mermaid-limit-exceeded`,
  located at the node and naming the limit.
- The layout work meter throws a `MermaidRenderException` with code `mermaid-limit-exceeded`, located at the diagram
  keyword, when the work of the node data, the labels, the layout, and the edges exceeds `MaxLayoutWork`, and an
  `OperationCanceledException` when the render is canceled; both propagate.
- `CoseLayout.Run` throws a `MermaidRenderException` with code `mermaid-render-failed` where Mermaid's layout would fail
  (a repulsion grid that cannot be built or a node outside it); it propagates.
- The report sink throws `MermaidUnsupportedFeatureException` at the first report in strict mode, and
  `MindmapStyles.Create` at the first unsupported `themeCSS` rule; they propagate.
- Null arguments of the constructor and of `DataOf` and `Prepared` are `ArgumentNullException`s.

#### Dependencies

- **MindmapParser** - `MindmapDiagram` (the nodes, the edges, and the keyword location), `MindmapNode`,
  `MindmapNodeType`, and `MindmapEdge`.
- **Configuration subsystem** - `MermaidConfig` (the `Mindmap` section `MindmapConfig`, `Look`, `Layout`, and
  `LocationOf`) and `MindmapStyles` (the mindmap style sheet).
- **Rendering subsystem** - `ShapeLibrary.Draw` with `ShapeContext`, `ShapeResult`, and `LayoutNode` (the node shapes,
  the `defaultMindmapNode` and `mindmapCircle` shapes among them); `EdgeBuilder.Draw` with `LayoutEdge`, `EdgeRoute`,
  `EdgeEnd`, `NodeBox`, and `ShapePoint` (the edges); and `LabelBuilder` (the labels, `DecodeEntities`, and
  `ContainsFontAwesome`).
- **Cose subsystem** - `CoseLayout.Run` with `CoseSize`, `CoseLink`, `CosePoint`, and `CoseResult` (the layout).
- **Syntax subsystem** - `HtmlSanitizer.Sanitize` (the label as `labelHelper` builds it).
- **Dagre subsystem** - `LayoutWorkMeter`, the work budget and cancellation of a render.
- **Scenegraph subsystem** - `Scene`, `SceneGroup`, `SceneItem`, and `SceneRect`, the drawing.
- **System** - `DiagramRenderContext`, `MermaidReportSink`, `MermaidLimits`, `MermaidRenderException`, and
  `MermaidDiagnosticCodes`.

#### Callers

- **MindmapParser** - `MindmapDiagram.Layout` creates a renderer for each render and returns the scene of `Draw`.
- The unit tests call `DataOf`, `Prepared`, and `Box32` directly and draw texts through the document; the reference
  layout tests read `Sizes`.

#### Known Differences From Mermaid

- **Chaotic layout.** The node sizes the layout is given are the browser's when the label widths and the shape bounds
  are the same doubles; the bounds of the cloud and bang arcs, which the browser computes in single precision, and the
  height of a multi-line SVG label can differ in the last bit, and the cose-bilkent layout can turn such a difference
  into a different picture (see _CanvasNetMermaid System Design_, item 49).
- **Icons.** A node's icon is not drawn and is reported; Mermaid's current renderer does not draw mindmap icons either,
  and an icon still gives an SVG label its background, as in Mermaid.
- **Other layouts.** A `layout` other than `cose-bilkent` (which mermaid-cli can load) is reported and not used.
- **Empty mindmap.** Mermaid sets no view box for a mindmap without nodes; the renderer gives the empty box grown by the
  padding.
