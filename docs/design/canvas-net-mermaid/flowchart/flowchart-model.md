### FlowchartModel

![Flowchart Structure](MermaidFlowchartView.svg)

The `FlowchartModel` unit is the second software unit in the `Flowchart` subsystem. It comprises four source files in
the `DemaConsulting.CanvasNet.Mermaid.Flowchart` namespace: `FlowchartModel.cs` (the database operations, the metadata,
and the limits), `FlowchartModel.Elements.cs` (the element types), `FlowchartModel.LayoutData.cs` (the mapping to the
layout input and the reports of what is not drawn), and `FlowchartModel.Render.cs` (the drawing of the diagram).
`FlowchartModel` is an `internal sealed partial` class, so the three model files are one type, and it implements the
system's `IMermaidDiagram`. `FlowchartModel.Elements.cs` defines the grouped helper types `FlowVertex`, `FlowEdge`, and
`FlowSubgraph` (sealed classes), `FlowClass`, `FlowText`, `FlowLink`, and `ShapeDataText` (sealed records), and
`DocumentItem` (a readonly record struct). Every file carries the provenance header comment for the parts of Mermaid
it ports.

#### Purpose

`FlowchartModel` is Mermaid's flowchart database (`flowDb.ts`) for one parsed text: it keeps what the statements
declare with Mermaid's semantics, gives the document the metadata of the diagram, maps itself to the layout input of
the shared rendering for every render with that render's configuration, reports what this release does not draw, and
draws the diagram as Mermaid's flowchart renderer (`flowRenderer-v3-unified.ts`) does. It exists so that the meaning of
a flowchart is decided in one place that can be compared with Mermaid's database fact by fact, independently of the
grammar and of the drawing.

The unit's behavior is specified by the `CanvasNetMermaid-Flowchart-FlowchartModel-*` requirements in
`docs/reqstream/canvas-net-mermaid/flowchart/flowchart-model.yaml`.

#### Data Model

**The database** (filled by one parser on one thread, read only afterwards and then safe for concurrent renders):

- the **vertices** in insertion order and by id (`FlowVertex`: id, DOM id `flowchart-{id}-{counter}`, label and label
  type, bracket or shape-data shape, styles, classes, the property of a `[|field:value|]` vertex, icon, form, position,
  image, asset width and height, constraint, and the location of the first mention);
- the **edges** in declaration order with the first edge of each id and the number of edges of each start and end pair
  (`FlowEdge`: start, end, id and whether the author gave it, label and label type, arrow type, stroke, length, curve,
  styles, classes, `animate` and `animation` as written, location);
- the **subgraphs** in the order they end, inner ones first, and the last subgraph of each id (`FlowSubgraph`: id,
  members, title, label type, direction, classes, the metadata of its shape data, and whether it is collapsed by
  `@{ view: collapsed }`), with the set of ids that already belong to a subgraph;
- the **classes** of `classDef` in declaration order (`FlowClass`: styles and text styles);
- the defaults of `linkStyle default` (curve and styles), the accessible title and description, the header direction
  as written, the vertex counter of the DOM ids, the counter of generated subgraph ids, and the first-header flag
  that Mermaid's lexer consults.

**The layout input** that `GetData` returns is the `LayoutData` of the `Rendering` subsystem: the cluster nodes first
(last declared first, a collapsed subgraph as a `collapsedGroup` node), then the vertices in declaration order with
their parents, shapes, padding, raw styles, compiled class styles, classes, and image and icon fields; the edges in
order with type, pattern, length, markers, styles, curve, and animation; the direction, the spacings, the markers
`point`, `circle`, and `cross`, and the configuration.

#### Key Methods

- **Database operations** (called by the parser where Mermaid's grammar reduces):
  - `SetDirection` maps `<`, `^`, `>`, `v`, and `TD` to `RL`, `BT`, `LR`, `TB`, and `TB` and keeps anything else.
  - `AddVertex` creates or merges a vertex (the label sanitized as Mermaid's `sanitizeText` does through the
    `HtmlSanitizer`, the bracket shape, styles, and property), or applies `@{ ... }` shape data: the block is read as
    YAML (one line wrapped as a flow mapping) with the location map of the parser; for a vertex it sets the checked
    shape, label and label type, icon, form, position, image, constraint, and asset size (an icon or image without a
    label hides the id), for an edge id the animation and the curve, and for a subgraph id its metadata.
  - `AddLink` adds an edge from every start to every end, the link id going to the edge from the last start to the
    first end; each edge gets the given id once, or `L_{start}_{end}_{n}` where `n` counts the earlier edges between
    the same nodes and skips 1, and a length capped at 10.
  - `DestructLink` reads a link token as Mermaid's `destructLink` does: the arrow type at each end, the stroke
    (`normal`, `thick`, `dotted`, or `invisible`), and the length, checked against the start of a link with text
    (`INVALID` when the strokes or the heads do not match).
  - `UpdateLink` and `UpdateLinkInterpolate` apply `linkStyle` to `default` or to edge indexes (read as JavaScript
    array indexes); the edges of one statement share one style list, which gets `fill:none` unless a style sets the
    fill.
  - `AddClass` splits `classDef` styles at commas (`\,` stays a comma) and adds a style that mentions a color also as
    a text style with `fill` renamed `bgFill`; `SetClass` adds a class to the vertices, edges, and subgraphs of the ids;
    `SetClickable` adds the class `clickable`.
  - `AddSubgraph` gives a subgraph its id (a generated `subGraph{n}` for a title with white space that is also its id),
    its sanitized title and label type, the direction of its last direction statement, and its unique, non-blank
    members that no earlier subgraph has.
  - `SetAccessibleTitle` and `SetAccessibleDescription` sanitize the texts as Mermaid's `commonDb.ts` does.
- **Metadata** (`IMermaidDiagram`): `Title` is null (a flowchart has no title statement; the document falls back to the
  frontmatter title), `AccTitle` is the accessible title or null, `Direction` is the trimmed header direction,
  `NodeCount` counts the vertices that are not subgraph ids, `EdgeCount` the edges, and `SubgraphCount` the subgraphs.
- **`GetData(MermaidConfig, string)`** is Mermaid's `getData` with the settings of the flowchart renderer: the shape of
  a vertex as `getTypeFromVertex` gives it (`imageSquare` for an image, `icon`, `iconCircle`, `iconSquare`, or
  `iconRounded` for an icon, `squareRect` and `roundedRect` for the legacy bracket names, else the shape as given), the
  vertex padding `flowchart.padding` (8 when zero), the compiled styles of the `default` and `node` classes and the
  vertex's classes (`getCompiledStyles`), the subgraph direction of `SubgraphDirection` (its own, else the diagram's
  when `flowchart.inheritDir` is set), the edge markers of `destructEdgeType` (none for open and invisible edges), the
  edge styles (`linkStyle default` and then its own), the curve (its own, else `linkStyle default interpolate`, else
  `flowchart.curve`), and the spacings `nodeSpacing` and `rankSpacing` (50 when zero). A collapsed subgraph hides the
  subgraphs and vertices inside it, an edge into or out of it ends at it, and an edge inside it is dropped.
- **`ReportUnsupported(MermaidConfig, MermaidReportSink)`** reports, in order, a look other than `classic`, a layout
  other than `dagre` (the `flowchart-elk` keyword sets `elk`), every node icon, and every label of a vertex, edge, or
  subgraph in which `LabelBuilder.ContainsFontAwesome` (the Rendering subsystem's shared test) finds a Font Awesome
  icon as Mermaid's expression does (`fa`, `fab`, `fak`, `fal`, `far`, or `fas`, then `:fa-` and a name of word
  characters and dashes), each located and with a hint.
- **`Layout(DiagramRenderContext)`** draws the diagram (Mermaid's `draw`): it reports first, gets the layout input,
  creates the picture loader (`NodeImages`, bounded by `MaxImages`), the label builder, the shape context with the
  flowchart style sheet, and the layout work meter of the render, and hands the input, the frontmatter title,
  `flowchart.titleTopMargin`, and `flowchart.diagramPadding` to `ClusterLayout.RenderDiagram`, the end of the draw
  function that the flowchart shares with the class diagram. `RenderDiagram` renders the input as the root group,
  places the title (`insertTitle`: sanitized, centered over the bounds of the drawing, its baseline the title margin
  above the origin, in the title style of the flowchart's sheet, `.flowchartTitleText`), and returns a `Scene` whose
  view box is the bounds grown by the padding (`setupViewPortForSVG`) and whose pictures are those of the loader.

#### Error Handling

- Every error is a `MermaidSyntaxException` raised while the parser fills the model, located at the element in the
  original text, with a hint:
  - a subgraph without an id or a title (`mermaid-syntax-error`, at the keyword);
  - shape data that is null (`mermaid-syntax-error`); shape data that is not valid YAML or uses YAML that the reader
    does not support (`mermaid-yaml-invalid` and `mermaid-yaml-unsupported` of the YAML reader of the `Syntax`
    subsystem), or that nests deeper than `MaxYamlDepth` (`mermaid-limit-exceeded`);
  - a shape that is not a name, not lower case without underscores, or not a known shape (`mermaid-shape-unknown`,
    with the nearest known name in the hint);
  - a `linkStyle` index that is not a plain array index of an edge declared before it (`mermaid-syntax-error`, the hint
    giving the valid range);
  - more than `MaxEdges` edges, more than `MaxNodes` vertices and subgraphs together, or a label, title, link text,
    shape-data label, accessible title, or accessible description longer than `MaxLabelLength`
    (`mermaid-limit-exceeded`).
- `ReportUnsupported` throws `MermaidUnsupportedFeatureException` through the sink at the first report in strict mode.
- `Layout` propagates the render errors of the shared rendering (`MermaidRenderException` for the layout work, image,
  and pixel limits and for layouts Mermaid cannot draw, `OperationCanceledException` for a canceled token).
- The public methods throw `ArgumentNullException` for null arguments.

#### Dependencies

- **Syntax subsystem** - `YamlSubsetParser` and its value types (the shape data), `HtmlSanitizer` (Mermaid's
  `sanitizeText`), `Preprocessor.TrimJs`, and `ScanPatterns`.
- **Configuration subsystem** - `MermaidConfig` (look, layout, flowchart settings, locations of keys) and
  `FlowchartStyles` (the style sheet of the render).
- **Rendering subsystem** - `LayoutData`, `LayoutNode`, and `LayoutEdge` (the layout input), `ShapeNames` (the known
  shapes and the nearest name), and `ClusterLayout` (its `RenderDiagram`: the drawing, the title, and the view box),
  `ShapeContext`, `LabelBuilder`, and `NodeImages` (the drawing).
- **Dagre subsystem** - `LayoutWorkMeter` (the shared layout work budget of a render).
- **Scenegraph subsystem** - `Scene` (the result of `Layout`, which `ClusterLayout.RenderDiagram` builds).
- **Graphlib subsystem** - `ObjectKeyMap.IsArrayIndex` (the JavaScript rule for a `linkStyle` index).
- **System** - `IMermaidDiagram`, `DiagramRenderContext`, `MermaidLimits`, `MermaidLocation`, `MermaidReportSink`,
  `MermaidSyntaxException`, and `MermaidDiagnosticCodes`.

#### Callers

- **FlowchartParser** - creates the model and calls the database operations as it reads the text.
- The document pipeline of the system - reads the metadata and calls `Layout` for every measure and render.
- The unit and subsystem tests call `GetData`, `ReportUnsupported`, and the database operations directly.

#### Known Differences From Mermaid

- **Database errors are located.** Where Mermaid's database fails with a JavaScript `TypeError` (a subgraph without a
  name, a `linkStyle` index without an edge), with "No such shape", or with a js-yaml error, the model raises a located
  `MermaidSyntaxException` with a CanvasNet message and a hint (codes `mermaid-syntax-error`, `mermaid-shape-unknown`,
  and `mermaid-yaml-*`).
- **Limits at open.** `MaxEdges`, `MaxNodes` (vertices and subgraphs together), and `MaxLabelLength` (every label and
  title, and the accessible title and description) refuse the text when the document is opened, as a
  `MermaidSyntaxException` with code `mermaid-limit-exceeded`. Mermaid's `maxEdges` of a site configuration is not
  consulted; only `MermaidLimits.MaxEdges` (with the same default of 500) applies.
- **Shape-data values.** Values that are not strings (a numeric `label`, `icon`, or `curve`) are converted with
  JavaScript's `String()`; `curve: null` is ignored; `shape: constructor`, which Mermaid accepts through a JavaScript
  prototype accident, is refused as an unknown shape.
- **`inheritDir`** is applied when the layout input is made, with the configuration of the render; Mermaid applies it
  while parsing with the same configuration, so the results are equal.
- **Interaction.** `click`, `call`, `href`, links, and tooltips only add the `clickable` class; the actions are not
  kept, since a rendered picture is not interactive.
- **Accessibility.** `accTitle` and `accDescr` are metadata only; a rendered picture has no SVG title or description.
- **Looks, layouts, and icons.** A look other than `classic` (such as `handDrawn`) is reported as
  `mermaid-look-unsupported` and drawn in the classic look; a layout other than `dagre` (such as `elk`) is reported as
  `mermaid-layout-unsupported` and laid out with dagre; node icons (`@{ icon }`) and Font Awesome icons (`fa:fa-...`)
  are reported as `mermaid-icon-unsupported`, the node keeping its shape and label and the label being drawn without
  the icon.
- **Animated edges** (`animate` and `animation` of an edge's shape data) are passed to the rendering, which draws a
  still picture (the first frame).
- **Title.** The flowchart has no title statement; the frontmatter title is drawn above the diagram with
  `titleTopMargin`, as Mermaid's `insertTitle` does.
- **The ellipse bracket.** `A(-text-)` gives the shape `ellipse` as in Mermaid's database; the pinned Mermaid release
  then fails to draw it (no such shape), while the shape library of the `Rendering` subsystem draws an ellipse.
