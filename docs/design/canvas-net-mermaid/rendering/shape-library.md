### ShapeLibrary

![Rendering Structure](MermaidRenderingView.svg)

The `ShapeLibrary` unit is the second software unit in the `Rendering` subsystem. It is the internal static partial
class `ShapeLibrary` in seven source files - `ShapeLibrary.cs` (the entry point, the style split, and the value
types), `ShapeLibrary.Basic.cs` (the classic and bracket shapes and the small undocumented shapes),
`ShapeLibrary.Extended.cs` (the extended shape set of Mermaid), `ShapeLibrary.Images.cs` (the image and icon
shapes), `ShapeLibrary.Intersect.cs` (the intersect functions), `ShapeLibrary.Parts.cs` (path data, part paint, and the
draft that collects a node), and `ShapeLibrary.Clusters.cs` (the subgraph rectangle and the hidden `labelRect` node) -
with the value types `ShapePoint`, `NodeBox`, `ShapeIntersect`, `NodePaint`, `ShapeTheme`, `ShapeContext`,
`ShapeResult`, and `ClusterShape`. The unit also groups the shape registry, the internal static class `ShapeNames` in
`ShapeNames.cs`. All types are in the `DemaConsulting.CanvasNet.Mermaid.Rendering` namespace.

#### Purpose

`ShapeLibrary` is Mermaid's node shapes (`rendering-util/rendering-elements/shapes/*.ts`) in the classic look: the 53
documented shapes with their aliases, the bracket shapes of the flowchart syntax, and the undocumented shapes a
flowchart accepts, each sized from its label and padding with Mermaid's formulas, drawn as scene paths, and given the
intersect function on which edges end. It also draws the subgraph rectangle with its title (`clusters.js`'s `rect`).
`ShapeNames` is Mermaid's shape registry (`rendering-elements/shapes.ts`): which names exist and which drawing function
each stands for. The unit's behavior is specified by the `CanvasNetMermaid-Rendering-ShapeLibrary-*` requirements in
`docs/reqstream/canvas-net-mermaid/rendering/shape-library.yaml`.

#### Data Model

- `ShapePoint(X, Y)` and `NodeBox(X, Y, Width, Height)` - a point and a placed node's box (center and size) as the
  intersect functions read it.
- `ShapeIntersect` - the delegate `(NodeBox node, ShapePoint point) => ShapePoint`: where the line from the node's
  center to a point leaves the shape (Mermaid's `node.intersect`).
- `ShapeContext` - the settings of one render that every shape reads: the effective `MermaidConfig`, the
  `FlowchartStyles` style sheet, the `MeasureLabel` delegate (`LabelBuilder.Build` in a render, a fixed box in the
  geometry tests), the `NodeImages` of the render (or null), the report sink, and the theme colors (`ShapeTheme`: the
  `mainBkg`, `nodeBorder`, `lineColor`, `clusterBorder`, and `edgeLabelBackground` variables, read once).
- `NodePaint(Css, User, Theme)` - the paint cascade of a node: the style sheet's node rule (`.node rect` and its
  siblings), the node's own non-label styles (applied as Mermaid's inline `!important` styles), and the theme colors
  that some parts use directly.
- `ShapeResult(Handler, Bounds, Items, Label, LabelOffset, Intersect)` - a drawn node: the drawing function that drew
  it, the box of everything drawn around the node's center (whose size is the node's `Width` and `Height` for the
  layout, as `insertMeasuredNode` takes it from `getBBox`), the items, the measured label and its translation
  (`LabelBox`), and the intersect function.
- `ClusterShape(Shape, Width, Height, Diff)` - a drawn subgraph: its rectangle and title, the widened width, the
  height, and the horizontal correction `diff` for a title wider than the layout's box.
- `ShapeNames` - a frozen table of every name (short names, aliases, internal aliases, and the undocumented shapes) to
  its drawing function, and the documented shapes in Mermaid's order.
- Private helpers: `ShapeInput` (a node being drawn with its split styles and paint), `PathData` (SVG path commands
  written as Mermaid writes them, with exact arc bounds), `PartPaint`, and `Draft` (collects the parts, the label, and
  the box of everything drawn).

#### Key Methods

- `Draw(LayoutNode node, ShapeContext context)` - resolves the drawing function (`ShapeNames.HandlerOf`, or the
  legacy `ellipse`), splits and parses the node's styles, and calls the shape: each shape measures its label first
  (Mermaid's `labelHelper`), computes its geometry from the label box and the padding (and from the direction of its
  graph, which turns a fork or join upright in a left-to-right graph), adds its parts and label to a draft, and returns
  the `ShapeResult` with its intersect function.
- `DrawCluster(LayoutNode node, double x, double y, double width, double height, ShapeContext context)` - the subgraph
  rectangle of the layout's size, widened to its title plus the padding when the title is wider, with the title
  measured without wrapping (a markdown title wraps at the layout's width) and placed on the top edge.
- `SplitStyles(LayoutNode)` and `SplitStyles(IEnumerable<string>)` - Mermaid's `styles2String`: the class styles,
  then the inline styles, then the label style, each `key:value` with a later key winning in its first place, split into
  the label styles (`isLabelStyle`: color and text properties) and the node styles.
- `LabelFontOf(StyleDeclarationSet, TextPaint)` - the font a label starts from: the style sheet's text rule with the
  element's own `font-family`, `font-size`, `font-weight`, `font-style`, and `color` over it.
- `IntersectRect`, `IntersectEllipse`, `IntersectCircle`, `IntersectLine`, and `IntersectPolygon` - the intersect
  functions of `rendering-elements/intersect/` with their exact arithmetic (the half-denominator rounding offset of the
  Graphics Gems line intersection, and the second-segment test that never rejects a crossing beyond the point).
- `SegmentBounds` - the box of path segments as SVG's `getBBox` gives it, including the extremes of elliptical arcs
  and the radius scaling SVG requires for an arc too small to reach its end.
- `ShapeNames.IsKnown`, `HandlerOf`, `Nearest`, `ShortNames`, and `DocumentedCount` - the registry: a case-sensitive
  test (Mermaid's `isValidShape`), the drawing function of a name, the nearest name a flowchart accepts (lower case, no
  underscore) by edit distance, and the 53 documented short names.

**Preconditions and postconditions**: `Draw` is given a node that is not a subgraph; the result is a pure function of
the node, the configuration, the style sheet, and the label measurement. The class is stateless and safe for
concurrent use.

#### Error Handling

- A shape name that this version does not draw (the shapes of other diagram types, such as `classBox` or `erBox`)
  throws a located `MermaidRenderException` (`mermaid-render-failed`) that suggests the nearest documented shape.
- A style declaration outside the supported CSS subset is reported as `mermaid-style-ignored` (once per distinct
  declaration, through the sink) by the style parser and is not applied.
- An image node whose picture cannot be loaded is drawn as its frame, with the warning `mermaid-image-unresolved`
  (reported by `NodeImages`, or here when the render has no `NodeImages`).
- The errors of the label measurement and of `NodeImages` (the image limit) propagate unchanged.

#### Dependencies

- **Rendering subsystem** - `LabelBuilder` (through `ShapeContext.MeasureLabel`; the request and result records),
  `NodeImages` (natural sizes of pictures), `LayoutNode`
- **Configuration subsystem** - `MermaidConfig`, `FlowchartStyles`, `ShapePaint`, `TextPaint`, `StyleDeclarations`,
  `StyleDeclarationSet`
- **Scenegraph subsystem** - `ScenePath`, `SceneSegment`, `SceneImage`, `SceneItem`, `SceneRect`
- **Syntax subsystem** - `DiagramDetector.EditDistance` (for `ShapeNames.Nearest`)
- **Package root** - `MermaidRenderException`, `MermaidDiagnosticCodes`, `MermaidReportSink`
- **Core `CanvasNet`** - `Rgba32`

#### Callers

`ClusterLayout` draws every node and subgraph through `Draw` and `DrawCluster`; `EdgeBuilder` uses `SplitStyles` and
`LabelFontOf` for the edge labels; `FlowchartModel` (the `Flowchart` subsystem) validates the names of
`@{ shape: ... }` with `ShapeNames.IsKnown` and `Nearest`. The tests in `ShapeLibraryTests.cs`,
`ShapeLibraryVectorTests.cs`, and `ShapeNamesTests.cs` call the unit directly through `InternalsVisibleTo`.

#### Known Differences From Mermaid

- The circles that Mermaid draws through rough.js (`f-circ`, `fr-circ`, `cross-circ`, `anchor`, and the icon circle)
  are drawn as true circles; rough.js's curve is up to 0.1 percent wider, at most 0.06 pixel.
- Icons are not drawn (the diagram model reports them); an icon shape keeps its frame and counts the icon as a square of
  its size, which is the size of the placeholder the browser draws, so the node sizes match.
- The legacy `ellipse` of the syntax `A(-text-)`, which the pinned Mermaid fails to render, is drawn as an ellipse
  with the radii of half the label width plus the padding and half the label height plus half the padding, with an
  elliptical intersect function, and without a report.
- An image node whose picture cannot be loaded, on which Mermaid's render fails, is drawn as its frame with a natural
  size of 0 by 0 and an aspect ratio of 1 (so `w` and `h` still size it) and reported as `mermaid-image-unresolved`.
- Mermaid applies the `font-*` styles of a subgraph title after it has measured the title; this library does not
  apply them (only the `color`, as for the HTML span), so the subgraph has Mermaid's size but its title keeps the style
  sheet's font.
- Only the shapes and the subgraph drawing a flowchart can reach are ported; subgraphs are drawn as `clusters.js`'s
  `rect` only.
