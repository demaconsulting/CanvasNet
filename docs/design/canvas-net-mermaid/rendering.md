## Rendering

![Rendering Structure](MermaidRenderingView.svg)

### Overview

The `Rendering` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Rendering`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Rendering/`) is the diagram-independent geometry of Mermaid's unified renderer
(`rendering-util`): what Mermaid draws for any layout data. A diagram model (the flowchart's `FlowchartModel`, the
class diagram's `ClassModel`, the state diagram's `StateModel`, or the entity relationship diagram's `ErModel`) hands
it the layout data of one render - the nodes (subgraphs included), the edges, and the effective configuration - and
gets back the drawn diagram as scene items with the box of everything drawn, or the whole framed scene with the
diagram title and the padded view box. The subsystem is **internal**: its types are
visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the test assembly. It contains
five units:

- **ClusterLayout** - the internal `ClusterLayout` (a partial class in `ClusterLayout.cs`, `ClusterLayout.Extractor.cs`,
  `ClusterLayout.SelfLoops.cs`, and `ClusterLayout.Diagram.cs`) with the graph labels `ClusterLayoutNode` and
  `ClusterLayoutEdge`, the result `RenderedGraph`, and the input records `LayoutData`, `LayoutNode`, `LayoutEdge`,
  `LayoutClassBox`, `LayoutClassMember`, `LayoutErBox`, and `LayoutErAttribute` (`LayoutData.cs`): Mermaid's dagre
  renderer, which turns the layout data into compound graphs, lays out the subgraphs without links to the outside on
  their own, sizes every node and edge label, runs the dagre layout, and draws the result, and the shared end of the
  unified diagrams' renderers, which frames the drawing with the diagram title and the padded view box; see
  _ClusterLayout Unit Design_ (`rendering/cluster-layout.md`)
- **ShapeLibrary** - the internal static `ShapeLibrary` (a partial class in `ShapeLibrary.cs`, `ShapeLibrary.Basic.cs`,
  `ShapeLibrary.Extended.cs`, `ShapeLibrary.Images.cs`, `ShapeLibrary.Intersect.cs`, `ShapeLibrary.Parts.cs`,
  `ShapeLibrary.Clusters.cs`, `ShapeLibrary.ClassBox.cs`, `ShapeLibrary.States.cs`, and `ShapeLibrary.ErBox.cs`) with
  its value types (`ShapePoint`, `NodeBox`, `ShapeIntersect`, `NodePaint`, `ShapeTheme`, `ShapeContext`,
  `ShapeResult`, `ClusterShape`) and the shape registry `ShapeNames` (`ShapeNames.cs`): every node shape a flowchart
  can name, the class box of a class diagram, the state shapes of a state diagram, and the entity box of an entity
  relationship diagram, sized from its label, drawn in the classic look, with its intersect function, and the subgraph
  rectangle and the clusters of a state diagram; see _ShapeLibrary Unit Design_ (`rendering/shape-library.md`)
- **EdgeBuilder** - the internal static `EdgeBuilder` (a partial class in `EdgeBuilder.cs`, `EdgeBuilder.Curves.cs`,
  `EdgeBuilder.Markers.cs`, and `EdgeBuilder.Terminals.cs`) with `EdgeEnd`, `EdgeRoute`, `EdgeDrawing`,
  `EdgeLabelDrawing`, `MarkerPaint`, `UserSpaceMarker`, `MarkerPart`, `ICurveContext`, and `CurvePath`: the edge route
  cut at the node outlines and subgraph borders, drawn with a d3-shape curve, stroked and marked with the flowchart's
  markers, the class diagram's markers, the state diagram's `barb`, or the entity relationship diagram's cardinality
  markers, the edge labels measured, placed, and drawn, and the terminal labels (a class relation's cardinalities)
  placed beside the ends of the route; see _EdgeBuilder Unit Design_ (`rendering/edge-builder.md`)
- **LabelBuilder** - the internal `LabelBuilder` (a partial class in `LabelBuilder.cs`, `LabelBuilder.Html.cs`,
  `LabelBuilder.Markdown.cs`, `LabelBuilder.MarkdownLexer.cs`, `LabelBuilder.Svg.cs`, and
  `LabelBuilder.TextDimensions.cs`) with `LabelFont`, `LabelRequest`, `LabelRun`, `LabelLine`, `LabelImage`,
  `LabelLayout`, and `TextDimensions`: the label text of Mermaid's `createText` (plain and markdown strings, HTML
  labels, and SVG labels) laid out and measured with the bundled fonts, and Mermaid's text utilities, which measure and
  wrap the texts of sequence diagrams; see _LabelBuilder Unit Design_ (`rendering/label-builder.md`)
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
  drawn, and the title correction of the last subgraph); and `ClusterLayout.RenderDiagram(LayoutData, string?, double,
  double, LabelBuilder)`, which returns the whole `Scene` (the drawing as the root group, the diagram title, and the
  view box grown by the padding)
- the records `LayoutData`, `LayoutNode`, `LayoutEdge`, `LayoutClassBox`, `LayoutClassMember`, `LayoutErBox`, and
  `LayoutErAttribute`, the contract between a diagram model and the renderer
- `ShapeContext` (configuration, style sheet, label measurement, text width, pictures, and report sink of one render),
  `ShapeLibrary.Draw(LayoutNode, ShapeContext)`, `ShapeLibrary.DrawCluster`, `ShapeLibrary.SplitStyles`,
  `ShapeLibrary.LabelFontOf`, `ShapeLibrary.ClassHtmlLabels`, and `ShapeLibrary.ErHtmlLabels`
- `ShapeNames.IsKnown`, `HandlerOf`, `Nearest`, `ShortNames`, and `DocumentedCount`
- `EdgeBuilder.MeasureLabel`, `Draw`, `LabelPosition`, `DrawLabel`, `MeasureTerminal`, `DrawTerminals`, and
  `CalcTerminalLabelPosition`
- `LabelBuilder(FontResolver, MermaidReportSink?, NodeImages?)`, `LabelBuilder.Build(LabelRequest)`, which returns a
  `LabelLayout`, and `LabelLayout.ToScene`; and the text utilities `LabelBuilder.Meter`, `CalculateTextDimensions`,
  `CalculateTextWidth`, `WrapLabel`, `BreakString`, `ParseFontSize`, `FontPixels`, `SplitBreaks`, `SvgLineExtent`,
  `SvgRunAdvance`, `XHeight`, and `VerticalMetrics`
- `NodeImages(IImageResolver?, int, MermaidReportSink)`, `NodeImages.NaturalSize`, and `NodeImages.Load` (the
  `ISceneImageLoader` of the painter)

**Consumed**:

- the `Dagre` subsystem: `DagreLayout.Run`, the graph labels `DagreGraph`, `DagreNode`, and `DagreEdge`, and the
  `LayoutWorkMeter` that bounds the work and observes cancellation
- the `Graphlib` subsystem: the compound multigraph `Graph`
- the `Configuration` subsystem: `MermaidConfig` (with its `class` and `er` sections and the theme variables),
  `UnifiedStyles` (the style sheet of the theme as the shared units read it, which is the diagram's `FlowchartStyles`,
  `ClassStyles`, `StateStyles`, or `ErStyles`, with the sheet facts `CentersNodeSvgText`, `ClusterTitle`, and
  `RootText`), `StyleDeclarations` (the CSS subset of the node and edge styles), and `MermaidFontConfig` and
  `ConfigScalar` (the fonts of the text utilities)
- the `Class` subsystem: `ClassModel.ParseGenericTypes`, which writes the generic type of an entity attribute
  (`~T~` as `<T>`) as the class diagram writes it
- the `Syntax` subsystem: `HtmlSanitizer` (Mermaid's DOMPurify subset and the character references, also applied to
  the diagram title) and `DiagramDetector.EditDistance`
- the `Scenegraph` subsystem: `Scene`, the scene items (`SceneGroup`, `ScenePath`, `SceneText`, `SceneImage`),
  `SceneRect`, `SceneFont`, `SceneTextRun` (with its `Underline`), and `ISceneImageLoader`
- the package root: `MermaidLocation`, `MermaidReportSink`, `MermaidDiagnosticCodes`, `MermaidRenderException`, and
  `MermaidLimits`
- the core `CanvasNet` public API: `Rgba32` and `Surface` (`Canvas`), `FontResolver` and `TrueTypeFont` (`Fonts`),
  `WordWrap` (`Rendering`), and `IImageResolver` (`ImageSources`)

**Consumers**: the `Flowchart` subsystem builds the layout data (`FlowchartModel.GetData`), validates the shape names
of `@{ shape: ... }` with `ShapeNames`, creates the `NodeImages`, `LabelBuilder`, and `ShapeContext` of a render, and
calls `ClusterLayout.RenderDiagram`; the `Class` subsystem builds the layout data of a class diagram
(`ClassModel.GetData`: the class boxes, the notes, the namespaces, the hidden lollipop interfaces, and the relations
with their markers and cardinalities), creates the same objects with the class diagram's style sheet and a
`ShapeContext.TextWidth` of `LabelBuilder.CalculateTextWidth`, and calls `ClusterLayout.RenderDiagram`; the `State`
subsystem builds the layout data of a state diagram (`StateModel.GetData`: the states with their radii of 10, padding,
centered labels, and descriptions, the start, end, fork, join, choice, and note shapes, the composite states, the
concurrent regions, and the note groups as clusters, and the transitions ending in the `barb` marker), creates the
same objects with the state diagram's style sheet, and calls `ClusterLayout.RenderDiagram`; the `Er` subsystem builds
the layout data of an entity relationship diagram (`ErModel.GetData`: the entities as `erBox` nodes with their
`LayoutErBox`, the subgraphs as `rect` clusters, and the relationships with their cardinality markers), creates the
same objects with the entity relationship diagram's style sheet and a `ShapeContext.TextWidth` of
`LabelBuilder.CalculateTextWidth`, and calls `ClusterLayout.RenderDiagram`; the `Scenegraph` painter loads the pictures
through the same `NodeImages`, so that the image limit is shared by the layout and the painting; the `Sequence`
subsystem creates a `LabelBuilder` for each render and measures, wraps, and boxes its texts with the text utilities.

### Design

**One pipeline per render.** `ClusterLayout` drives the subsystem: it builds the root compound graph from the layout
data, moves the links to subgraphs onto anchors, extracts the subgraphs without links to the outside into graphs of
their own, and then for each graph (inner graphs first) asks `ShapeLibrary` for every node's shape and size and
`EdgeBuilder` for every edge label's size, runs the dagre layout, and draws the subgraph rectangles (through
`ShapeLibrary.DrawCluster`), the edges, their labels, and their terminal labels (through `EdgeBuilder`), and the nodes.
`ClusterLayout.RenderDiagram` then frames the drawing as the unified diagrams' renderers do: the drawing as the root
group, the diagram title above it, and the view box grown by the diagram's padding, so a flowchart, a class diagram, a
state diagram, and an entity relationship diagram end in the same code. The shapes measure their labels through the
`ShapeContext.MeasureLabel` delegate, which is `LabelBuilder.Build` in a render (tests replace it with a fixed box to
check the geometry alone), the class box wraps its texts at the widths of the `ShapeContext.TextWidth` delegate, the
entity box takes the wrapping widths of its texts and the test of its minimum width from the same delegate, and the
image shapes and label pictures ask `NodeImages` for natural sizes.

**One renderer for every unified diagram.** The shared units read a diagram only through its layout data and its style
sheet: `LayoutData.DiagramType` decides whether a self-loop is merged into one compact loop (as Mermaid decides by
diagram type), a node's `Shape` selects the flowchart shapes, the class box, the state shapes, or the entity box (and a
`rect` with both radii set, a state, is drawn as a `roundedRect`, as Mermaid's `insertNode` draws it), a subgraph's
`Shape` selects the subgraph rectangle or a state diagram's `roundedWithTitle`, `divider`, or `noteGroup` cluster, an
edge's arrow types select the flowchart's, the class diagram's, the state diagram's, or the entity relationship
diagram's markers, and the selectors that `UnifiedStyles` names (the node label, edge label, note label, terminal
label, cluster title, root text, and title rules, the backgrounds of an edge label, and whether the sheet centers the
SVG text of a node label) come from the diagram's own sheet. The parts that a state or entity relationship diagram
paints through selectors of its own (`.node circle.state-start`, `.statediagram-cluster .inner`,
`[id$="-barbEnd"]`, and so on) are looked up in the sheet by those selectors, so a sheet without them (a flowchart's)
keeps the paint it had; the parts that Mermaid draws through rough.js take their paint attributes unless the sheet
has a `.node path` rule. The styles a diagram writes for its own nodes (a class diagram's note colors and its hidden
lollipop interfaces) are marked `LayoutNode.TrustedStyles` and applied without reports.

**The scene is the result.** Nothing is painted here: every unit returns scene items with Mermaid's roles and DOM ids
(`node-group` with the id `flowchart-A-0`, `cluster`, `edge` with the id `L_A_B_0`, `edge-label`, `edge-terminal`,
`marker`, `label`, `subgraph`, `dummy-group`, the class box rows `class-annotation`, `class-label`, `class-member`, and
`class-method` with their `class-divider` lines, the `state-divider` line of a state with descriptions and the
`cluster-inner` area of a composite state, the entity box cells `er-name`, `er-attribute-type`, `er-attribute-name`,
`er-attribute-keys`, and `er-attribute-comment` with their `er-divider` lines, and the framed scene's `root` and
`title`), so the tests and the reference-layout harness compare the geometry with Mermaid's SVG without pixels, and the
`Scenegraph` painter draws it later.

**A literal port.** The units follow the pinned Mermaid's code, including its quirks, because layout fidelity is
measured to hundredths of a pixel: the shape formulas and their intersect functions, the path data rounded to three
decimals as d3-path writes it, the anchors and cuts of `mermaid-graphlib.js`, and the label layout of the browser
emulated with the bundled fonts. The ported files are (see `third-party/README.md` for the provenance):

- `ClusterLayout`: `rendering-util/layout-algorithms/dagre/index.js` and `mermaid-graphlib.js`,
  `layout-algorithms/common/index.ts`, `rendering-elements/nodes.ts` (`positionNode`), and
  `utils/subGraphTitleMargins.ts`; the shared end of `flowRenderer-v3-unified.ts`, `classRenderer-v3-unified.ts`,
  `stateRenderer-v3-unified.ts`, and `erRenderer-unified.ts` (`insertTitle` and `setupViewPortForSVG`); `LayoutData`
  follows `rendering-util/types.ts`
- `ShapeLibrary`: `rendering-elements/shapes/*.ts` (including `classBox.ts`, `rectWithTitle.ts`, the state shapes
  `stateStart.ts`, `stateEnd.ts`, `forkJoin.ts`, `choice.ts`, and `note.ts`, and `erBox.ts`), `shapes/util.ts`,
  `shapes/handDrawnShapeStyles.ts`, the text placement of the class diagram's `shapeUtil.ts` (`textHelper`),
  `rendering-elements/nodes.ts` (`insertNode`), `rendering-util/createGraph.ts`, `rendering-elements/clusters.js`
  (`rect`, `roundedWithTitle`, `divider`, and `noteGroup`), the ellipse of rough.js without roughness (the end state's
  outline), and the intersect functions of `rendering-elements/intersect/` (which Mermaid borrowed from dagre-d3);
  `ShapeNames` follows `rendering-elements/shapes.ts`
- `EdgeBuilder`: `rendering-elements/edges.js` (with the terminal labels of `insertEdgeLabel` and
  `positionEdgeLabel`), `edgeMarker.ts`, the flowchart, class, state (`barb`), and entity relationship markers of
  `markers.js`, `utils/lineWithOffset.ts`,
  `labelTransform.ts`, and the edge helpers of `utils.ts` (with `calcTerminalLabelPosition`), and the line generator
  and edge curves of d3-shape with d3-path's number rounding
- `LabelBuilder`: `rendering-util/createText.ts`, `handle-markdown-text.ts`, `splitText.ts`, the label measurement of
  `shapes/util.ts` and `shapes/labelImageUtils.ts`, the part of the `marked` lexer that Mermaid's markdown labels
  use, and the text utilities of `utils.ts` (`calculateTextDimensions`, `wrapLabel`, `breakString`, `parseFontSize`)
  and `diagrams/common/common.ts` (`splitBreaks`)
- `NodeImages` is original work of this repository.

**Fonts.** Text is measured and drawn with the faces of the call's `FontResolver` (the bundled Liberation Sans, which
the resolver gives for Mermaid's default family names), with line boxes of 1.5 font sizes for HTML labels and lines 1.1
font sizes apart for SVG labels, as Mermaid's style sheet and `createFormattedText` give them; each run of SVG text
advances by its width rounded up to a 64th of a pixel, as the browser lays out SVG text runs.

**Bounded and deterministic.** Every stage of `ClusterLayout` charges the render's `LayoutWorkMeter`, which also polls
the cancellation token; every resolver call counts against `MermaidLimits.MaxImages`. The units hold no global state:
a `ClusterLayout`, `LabelBuilder`, and `NodeImages` belong to one render on one thread, and `ShapeLibrary`,
`EdgeBuilder`, and `ShapeNames` are stateless, so the same text gives the same scene on every platform.

### Known Differences From Mermaid

The documented differences are listed with the unit that owns them: the rough.js circles (all but the end state's,
which is ported exactly), the icons, the legacy `ellipse`, the unloadable picture, the cluster title styles, and the
CSS radii of a state (_ShapeLibrary Unit Design_); the SVG label box, the HTML subset, the character references, the
label pictures, and the markdown subset (_LabelBuilder Unit Design_); the natural size of a picture (_NodeImages Unit
Design_); the markers, the circle marker clipping, the half-pixel miter tip of the `extensionStart` marker, the
viewport clip of the entity relationship marker curves, the clipping of HTML terminal labels, the animated edges, and
the last bit of the trigonometric functions (_EdgeBuilder Unit Design_); and the failures for a subgraph cycle (also a
state inside itself or an entity relationship subgraph that is its own parent), for a link to a subgraph without an
anchor, and for an edge end that is not drawn (_ClusterLayout Unit Design_). The position of the underline of a static
class member is a difference of the painter (_ScenePainter Unit Design_). Only what a flowchart, a class diagram, a
state diagram, and an entity relationship diagram reach is ported: subgraphs, namespaces, and entity relationship
subgraphs are drawn as `clusters.js`'s `rect`, and the clusters of a state diagram as its `roundedWithTitle`,
`divider`, and `noteGroup`; the markers are the flowchart's `point`, `circle`, and `cross`, the class diagram's
`aggregation`, `extension`, `composition`, `dependency`, and `lollipop`, the state diagram's `barb`, and the entity
relationship diagram's `onlyOne`, `zeroOrOne`, `oneOrMore`, and `zeroOrMore`; and the terminal labels are the two a
class relation sets (`startLabelRight` and `endLabelLeft`). A very large diagram can need a picture larger than
`Surface.MaxDimension` at scale 1, in which case `Measure` and `Render` throw the documented
`ArgumentOutOfRangeException`.
