## Rendering

![Rendering Structure](MermaidRenderingView.svg)

### Overview

The `Rendering` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Rendering`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Rendering/`) is the diagram-independent geometry of Mermaid's unified renderer
(`rendering-util`): what Mermaid draws for any layout data. A diagram model (the flowchart's `FlowchartModel`) hands it
the layout data of one render - the nodes (subgraphs included), the edges, and the effective configuration - and gets
back the drawn diagram as scene items with the box of everything drawn. The subsystem is **internal**: its types are
visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the test assembly. It contains
five units:

- **ClusterLayout** - the internal `ClusterLayout` (a partial class in `ClusterLayout.cs`, `ClusterLayout.Extractor.cs`,
  and `ClusterLayout.SelfLoops.cs`) with the graph labels `ClusterLayoutNode` and `ClusterLayoutEdge`, the result
  `RenderedGraph`, and the input records `LayoutData`, `LayoutNode`, and `LayoutEdge` (`LayoutData.cs`): Mermaid's
  dagre renderer, which turns the layout data into compound graphs, lays out the subgraphs without links to the
  outside on their own, sizes every node and edge label, runs the dagre layout, and draws the result; see _ClusterLayout
  Unit Design_ (`rendering/cluster-layout.md`)
- **ShapeLibrary** - the internal static `ShapeLibrary` (a partial class in `ShapeLibrary.cs`, `ShapeLibrary.Basic.cs`,
  `ShapeLibrary.Extended.cs`, `ShapeLibrary.Images.cs`, `ShapeLibrary.Intersect.cs`, `ShapeLibrary.Parts.cs`, and
  `ShapeLibrary.Clusters.cs`) with its value types (`ShapePoint`, `NodeBox`, `ShapeIntersect`, `NodePaint`,
  `ShapeTheme`, `ShapeContext`, `ShapeResult`, `ClusterShape`) and the shape registry `ShapeNames` (`ShapeNames.cs`):
  every node shape a flowchart can name, sized from its label, drawn in the classic look, with its intersect function,
  and the subgraph rectangle; see _ShapeLibrary Unit Design_ (`rendering/shape-library.md`)
- **EdgeBuilder** - the internal static `EdgeBuilder` (a partial class in `EdgeBuilder.cs`, `EdgeBuilder.Curves.cs`,
  and `EdgeBuilder.Markers.cs`) with `EdgeEnd`, `EdgeRoute`, `EdgeDrawing`, `EdgeLabelDrawing`, `MarkerPaint`,
  `ICurveContext`, and `CurvePath`: the edge route cut at the node outlines and subgraph borders, drawn with a d3-shape
  curve, stroked and marked, and the edge labels measured, placed, and drawn; see _EdgeBuilder Unit Design_
  (`rendering/edge-builder.md`)
- **LabelBuilder** - the internal `LabelBuilder` (a partial class in `LabelBuilder.cs`, `LabelBuilder.Html.cs`,
  `LabelBuilder.Markdown.cs`, `LabelBuilder.MarkdownLexer.cs`, and `LabelBuilder.Svg.cs`) with `LabelFont`,
  `LabelRequest`, `LabelRun`, `LabelLine`, `LabelImage`, and `LabelLayout`: the label text of Mermaid's `createText`
  (plain and markdown strings, HTML labels, and SVG labels) laid out and measured with the bundled fonts; see
  _LabelBuilder Unit Design_ (`rendering/label-builder.md`)
- **NodeImages** - the internal sealed `NodeImages` (`NodeImages.cs`): the pictures of one rendering, loaded only
  through the caller's image resolver, with their natural sizes for the layout and their pixels for the painter; see
  _NodeImages Unit Design_ (`rendering/node-images.md`)

The subsystem's namespace documentation is in `Rendering/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Rendering-*` requirements in `docs/reqstream/canvas-net-mermaid/rendering.yaml`. The SysML2 model
names the subsystem `MermaidRendering` (the core library already has a `Rendering` part).

### Interfaces

**Exposed** (internal only):

- `ClusterLayout(ShapeContext, LayoutWorkMeter)` and `ClusterLayout.Render(LayoutData)`, which returns a
  `RenderedGraph` (the layers `clusters`, `edgePaths`, `edgeLabels`, and `nodes` as scene groups, the box of everything
  drawn, and the title correction of the last subgraph)
- the records `LayoutData`, `LayoutNode`, and `LayoutEdge`, the contract between a diagram model and the renderer
- `ShapeContext` (configuration, style sheet, label measurement, pictures, and report sink of one render),
  `ShapeLibrary.Draw(LayoutNode, ShapeContext)`, `ShapeLibrary.DrawCluster`, `ShapeLibrary.SplitStyles`, and
  `ShapeLibrary.LabelFontOf`
- `ShapeNames.IsKnown`, `HandlerOf`, `Nearest`, `ShortNames`, and `DocumentedCount`
- `EdgeBuilder.MeasureLabel`, `Draw`, `LabelPosition`, and `DrawLabel`
- `LabelBuilder(FontResolver, MermaidReportSink?, NodeImages?)`, `LabelBuilder.Build(LabelRequest)`, which returns a
  `LabelLayout`, and `LabelLayout.ToScene`
- `NodeImages(IImageResolver?, int, MermaidReportSink)`, `NodeImages.NaturalSize`, and `NodeImages.Load` (the
  `ISceneImageLoader` of the painter)

**Consumed**:

- the `Dagre` subsystem: `DagreLayout.Run`, the graph labels `DagreGraph`, `DagreNode`, and `DagreEdge`, and the
  `LayoutWorkMeter` that bounds the work and observes cancellation
- the `Graphlib` subsystem: the compound multigraph `Graph`
- the `Configuration` subsystem: `MermaidConfig`, `FlowchartStyles` (the style sheet of the theme), and
  `StyleDeclarations` (the CSS subset of the node and edge styles)
- the `Syntax` subsystem: `HtmlSanitizer` (Mermaid's DOMPurify subset and the character references) and
  `DiagramDetector.EditDistance`
- the `Scenegraph` subsystem: the scene items (`SceneGroup`, `ScenePath`, `SceneText`, `SceneImage`), `SceneRect`,
  `SceneFont`, and `ISceneImageLoader`
- the package root: `MermaidLocation`, `MermaidReportSink`, `MermaidDiagnosticCodes`, `MermaidRenderException`, and
  `MermaidLimits`
- the core `CanvasNet` public API: `Rgba32` and `Surface` (`Canvas`), `FontResolver` and `TrueTypeFont` (`Fonts`),
  `WordWrap` (`Rendering`), and `IImageResolver` (`ImageSources`)

**Consumers**: the `Flowchart` subsystem builds the layout data (`FlowchartModel.GetData`), validates the shape names
of `@{ shape: ... }` with `ShapeNames`, creates the `NodeImages`, `LabelBuilder`, and `ShapeContext` of a render, and
calls `ClusterLayout.Render`; the `Scenegraph` painter loads the pictures through the same `NodeImages`, so that the
image limit is shared by the layout and the painting.

### Design

**One pipeline per render.** `ClusterLayout` drives the subsystem: it builds the root compound graph from the layout
data, moves the links to subgraphs onto anchors, extracts the subgraphs without links to the outside into graphs of
their own, and then for each graph (inner graphs first) asks `ShapeLibrary` for every node's shape and size and
`EdgeBuilder` for every edge label's size, runs the dagre layout, and draws the subgraph rectangles (through
`ShapeLibrary.DrawCluster`), the edges and their labels (through `EdgeBuilder`), and the nodes. The shapes measure their
labels through the `ShapeContext.MeasureLabel` delegate, which is `LabelBuilder.Build` in a render (tests replace it
with a fixed box to check the geometry alone), and the image shapes and label pictures ask `NodeImages` for natural
sizes.

**The scene is the result.** Nothing is painted here: every unit returns scene items with Mermaid's roles and DOM ids
(`node-group` with the id `flowchart-A-0`, `cluster`, `edge` with the id `L_A_B_0`, `edge-label`, `marker`, `label`,
`subgraph`, `dummy-group`), so the tests and the reference-layout harness compare the geometry with Mermaid's SVG
without pixels, and the `Scenegraph` painter draws it later.

**A literal port.** The units follow the pinned Mermaid's code, including its quirks, because layout fidelity is
measured to hundredths of a pixel: the shape formulas and their intersect functions, the path data rounded to three
decimals as d3-path writes it, the anchors and cuts of `mermaid-graphlib.js`, and the label layout of the browser
emulated with the bundled fonts. The ported files are (see `third-party/README.md` for the provenance):

- `ClusterLayout`: `rendering-util/layout-algorithms/dagre/index.js` and `mermaid-graphlib.js`,
  `layout-algorithms/common/index.ts`, `rendering-elements/nodes.ts` (`positionNode`), and
  `utils/subGraphTitleMargins.ts`; `LayoutData` follows `rendering-util/types.ts`
- `ShapeLibrary`: `rendering-elements/shapes/*.ts`, `shapes/util.ts`, `shapes/handDrawnShapeStyles.ts`,
  `rendering-elements/nodes.ts` (`insertNode`), `rendering-util/createGraph.ts`, `rendering-elements/clusters.js`
  (`rect`), and the intersect functions of `rendering-elements/intersect/` (which Mermaid borrowed from dagre-d3);
  `ShapeNames` follows `rendering-elements/shapes.ts`
- `EdgeBuilder`: `rendering-elements/edges.js`, `edgeMarker.ts`, `markers.js`, `utils/lineWithOffset.ts`,
  `labelTransform.ts`, and the edge helpers of `utils.ts`, and the line generator and edge curves of d3-shape with
  d3-path's number rounding
- `LabelBuilder`: `rendering-util/createText.ts`, `handle-markdown-text.ts`, `splitText.ts`, the label measurement of
  `shapes/util.ts` and `shapes/labelImageUtils.ts`, and the part of the `marked` lexer that Mermaid's markdown labels
  use
- `NodeImages` is original work of this repository.

**Fonts.** Text is measured and drawn with the faces of the call's `FontResolver` (the bundled Liberation Sans, which
the resolver gives for Mermaid's default family names), with line boxes of 1.5 font sizes for HTML labels and lines 1.1
font sizes apart for SVG labels, as Mermaid's style sheet and `createFormattedText` give them.

**Bounded and deterministic.** Every stage of `ClusterLayout` charges the render's `LayoutWorkMeter`, which also polls
the cancellation token; every resolver call counts against `MermaidLimits.MaxImages`. The units hold no global state:
a `ClusterLayout`, `LabelBuilder`, and `NodeImages` belong to one render on one thread, and `ShapeLibrary`,
`EdgeBuilder`, and `ShapeNames` are stateless, so the same text gives the same scene on every platform.

### Known Differences From Mermaid

The documented differences are listed with the unit that owns them: the rough.js circles, the icons, the legacy
`ellipse`, the unloadable picture, and the cluster title styles (_ShapeLibrary Unit Design_); the SVG label box, the
HTML subset, the character references, the label pictures, and the markdown subset (_LabelBuilder Unit Design_); the
natural size of a picture (_NodeImages Unit Design_); the markers, the circle marker clipping, the animated edges, and
the last bit of the trigonometric functions (_EdgeBuilder Unit Design_); and the failures for a subgraph cycle and for a
link to a subgraph without an anchor (_ClusterLayout Unit Design_). Only what a flowchart reaches is ported: subgraphs
are drawn as `clusters.js`'s `rect` only, the markers are `point`, `circle`, and `cross` only, and terminal labels of
edges are not drawn because the flowchart never sets them. A very large diagram can need a picture larger than
`Surface.MaxDimension` at scale 1, in which case `Measure` and `Render` throw the documented
`ArgumentOutOfRangeException`.
