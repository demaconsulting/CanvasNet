## Scenegraph

![Scenegraph Structure](VegaLiteScenegraphView.svg)

### Overview

The `Scenegraph` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Scenegraph`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Scenegraph/`) is where a chart becomes pixels, in two steps: an immutable scene
that holds every resolved item (the **assertion surface** of the tests) and a painter that draws the scene onto a
core `Surface`. It is **internal**: its types are visible only to the other subsystems of the system and, through
`InternalsVisibleTo`, to the test assembly. It contains six units:

- **SceneModel** - the internal scene types: `Scene`, the items `SceneItem`, `SceneGroup`, `SceneRect`, `SceneRule`,
  `ScenePath` (points or drawing segments), `SceneSymbol`, `SceneText`, `SceneImage`, `SceneArc`, and `SceneTrail`, the
  gradient types
  `SceneGradient` and `SceneGradientStop`, the value types `ScenePoint`, `ScenePathSegment`, `SceneTrailPoint`,
  `SceneClip`, and `SceneEntry`, `SceneBounds`, `SceneLists`, and the `SceneBudget`, and the internal position data
  (`SceneItem.Position` with `ScenePositionTag`, `SceneItem.Datum`, `SceneGroup.Marks` with `SceneMarkContext`, and the
  `Vertices` of a path or trail, `SceneVertex`); see _SceneModel Unit Design_
  (`scenegraph/scene-model.md`)
- **ScenePainter** - the internal `ScenePainter` (a partial class in `ScenePainter.cs`, `ScenePainter.Paths.cs`, and
  `ScenePainter.Images.cs`), `ScenePlacement`, and `SceneImageSource` (the loader of the pictures of one painting):
  drawing a scene onto a `Surface` with fills (solid or linear gradient), strokes, dashes, miter limits,
  opacity, rotation, uniform scale, clipping, paths with curved segments, arcs, trails, pictures, and text; see
  _ScenePainter Unit
  Design_ (`scenegraph/scene-painter.md`)
- **CssPaint** - the internal static `CssPaint`: the paint rules of a chart (no paint, opacity) on top of the core
  `CssColor` parser; see _CssPaint Unit Design_ (`scenegraph/css-paint.md`)
- **ArcGeometry** - the internal static `ArcGeometry` and the helper `ArcPathWriter`: the outline of a circular or
  annular
  sector (the d3-shape arc generator, with its pad angle and corner radius) as a core `Path`; see _ArcGeometry Unit
  Design_ (`scenegraph/arc-geometry.md`)
- **SymbolPathParser** - the internal sealed `SymbolPathParser`: the parser, the canvas-rule drawing, and the bounds of
  the SVG path data of a custom symbol shape (a port of vega-scenegraph's path parser and renderer), with a cache of
  parsed paths; see _SymbolPathParser Unit Design_ (`scenegraph/symbol-path-parser.md`)
- **ScenePositions** - the internal static `ScenePositions`: the feature positions of a laid-out chart (titles, views,
  axes, legends, and marks) with their canonical view paths, ids, normalized values, data fields, geometry boxes, and attachment
  pixels in the pixels of the surface; see _ScenePositions Unit Design_ (`scenegraph/scene-positions.md`)

The subsystem's namespace documentation is in `Scenegraph/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Scenegraph-*` requirements in `docs/reqstream/canvas-net-vega-lite/scenegraph.yaml`. The SysML2 model
names the subsystem `VegaLiteScenegraph`, the scene unit `VegaLiteSceneModel`, and the positions unit
`VegaLiteScenePositions` (see _CanvasNetVegaLite System
Design_).

### Interfaces

**Exposed** (internal only): the immutable scene types and their enumeration helpers (`Scene.Enumerate`, `Find`,
`FindAll`, `OfType`, `CountItems`); `SceneBounds` with `Union`, `Translate`, and `Rotate`; `SceneBudget.Add`;
`ScenePainter.Paint(Scene, int, int, ScenePlacement, CancellationToken)` and `Paint(Scene, Surface, ScenePlacement,
CancellationToken)`, `ScenePainter.SymbolPath`, `ScenePainter.ResolveMiterLimit`, and `ScenePlacement.Uniform` and
`ScenePlacement.Fit`; `CssPaint.IsNone`, `TryResolvePaint`, and `WithOpacity`; and
`ArcGeometry.Build`, and `SymbolPathParser.TryParse`, `TryGet`, `IsValid`, and `Render`; and
`ScenePositions.Report`, `Collect`, `Geometry`, `ViewPath`, `ViewName`, and `FacetValues`.

**Consumed**: `TextMeasurer` from the `Guides` subsystem (the painter resolves fonts and draws the lines the measurer
produces, and the positions measure the box of a text mark with it); `MarkBuilder.TryConvert` from the `Marks`
subsystem and `LayerNode` from the `SpecModel` subsystem (the facet values and the view paths of the positions); the
public `VegaLiteLocation`, `VegaLiteRenderException`, and `VegaLiteRenderOptions` (its `PositionHandler` and
`PositionKindFilter`) of the `VegaLiteDocument` unit; and the core
`CanvasNet` public API: `Surface`, `Rgba32`, and `CssColor` (`Canvas`), `PathFiller`, `PathStroker`, and
`LinearGradient` (`Drawing`),
`Path` and `PathBuilder` (`Geometry`), `TextRenderer` (`Rendering`), and `ImagePosition`, `PositionBox`,
`PositionPoint`, and `PositionValue` (`Positions`).

**Consumers**: the builders of the `Guides`, `Layout`, and `Marks` subsystems create the items (and tag them for the
positions); `VegaLiteDocument` reports the positions and paints
the scene.

### Design

**An immutable scene is the contract between building and painting.** The builders (`TitleBuilder`, `AxisBuilder`,
`ViewLayout`, `MarkBuilder`) never draw: they produce items with resolved geometry and CSS color strings, in a fixed
order,
so that two runs produce the same list and tests can assert exact numbers and exact strings without pixels. Items have a
stable role (`root`, `view`, `title-text`, `axis-tick`, `mark-bar`, `mark-line`, `mark-arc`, and so on) by which tests
and
later stages find them, and the enumeration is iterative (an explicit stack), so a deep scene cannot overflow the stack.

**The painter adds what the core lacks.** The core library has fills, strokes, dashes, and text, but no scale and no
clip, so the painter transforms every path by the placement (uniform scale plus translation plus the enclosing groups'
translations) before filling it, draws text at the scaled font size, and implements clipping itself by drawing a clipped
group onto a transparent layer the size of the clip's pixel box and compositing it back with the coverage of the clip
rectangle. Fills are flattened at a tolerance of 0.02 pixel so that small symbols stay round, strokes are built in the
item's
own units so widths and dashes scale with the chart, and a paint that is `none`, unparseable, or fully transparent draws
nothing.

**Curves, arcs, and trails are scene data.** A path item can carry drawing commands (moves, lines, cubic Bezier curves,
and closes) instead of points, so the curve builders of the marks hand over the exact geometry of an interpolated line
or
area. An arc item carries the radii and angles of a slice and the painter asks the `ArcGeometry` unit for its outline,
which keeps the d3-shape arithmetic (pad angle, rounded corners) in one place that tests can assert without pixels. A
trail
item carries its runs of points with a width each, and the painter draws the capsules between them with the same path
writer that the arc geometry uses. A symbol whose shape is not a symbol name is SVG path data: the painter asks the
`SymbolPathParser` unit for the path scaled to the symbol size (and the legend for its bounds), and draws a circle when
the text does not parse.

**Deterministic.** The painter holds no state and the same scene, size, and placement give the same bytes on every
platform;
colors parse with the invariant culture and no named color depends on the host.

**Positions come from the scene the painter draws.** The guide and view groups carry an internal tag (kind, id, text,
view path, view name, and optional bounds), and only when a render asks for mark positions do the mark items record the
row they draw, the path marks their vertices, and each marks group the values and data fields of its rows; none of it
changes the pixels. After the painting, `ScenePositions` walks the scene once, reports the titles, views, axes, and
legends, then the marks, each in drawing order, with the box and attachment pixel of its kind mapped by the placement
the painter uses (an image mark's box is the rectangle the painter drew its picture in), so the positions are those of
the returned surface at any scale or exact size; a kind the filter rejects is skipped before its geometry is computed.

**Bounded.** The scene item count is bounded by `SceneBudget` (`MaxSceneItems`), and the pixel size by the surface
(`Surface.MaxDimension`); a clip layer is only as large as the clip's pixel box. The geometry of an arc costs a constant
(at most about 20 path commands), and the painting of a path or a trail is proportional to its commands or points, which
the builders have already charged to the segment and item limits.
