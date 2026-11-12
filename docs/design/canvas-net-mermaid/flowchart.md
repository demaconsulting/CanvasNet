## Flowchart

![Flowchart Structure](MermaidFlowchartView.svg)

### Overview

The `Flowchart` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Flowchart`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Flowchart/`) is one of the two diagram types of this release: it reads the
text of a
`flowchart` or `graph` diagram (and of the `flowchart-elk` keyword, which opens as a flowchart) into Mermaid's flowchart
database and turns that database into the input of the shared rendering, then draws the diagram as Mermaid's flowchart
renderer does. It is **internal**: its types are visible only to the other subsystems of the system and, through
`InternalsVisibleTo`, to the test assembly. It contains two units:

- **FlowchartParser** - the internal sealed `FlowchartParser` (a partial class in `FlowchartParser.cs`,
  `FlowchartParser.Lexer.cs`, `FlowchartParser.Statements.cs`, and `FlowchartParser.Links.cs`) and the token names of
  `FlowchartTokens` (in `FlowchartParser.Lexer.cs`): the lexer and the grammar of Mermaid's flowchart language, the
  located errors with Mermaid's expected tokens, and the subgraph nesting limit; see _FlowchartParser Unit Design_
  (`flowchart/flowchart-parser.md`)
- **FlowchartModel** - the internal sealed `FlowchartModel` (a partial class in `FlowchartModel.cs`,
  `FlowchartModel.LayoutData.cs`, and `FlowchartModel.Render.cs`) and the element types of `FlowchartModel.Elements.cs`
  (`FlowVertex`, `FlowEdge`, `FlowSubgraph`, `FlowClass`, `FlowText`, `FlowLink`, `ShapeDataText`, and `DocumentItem`):
  Mermaid's flowchart database, the diagram metadata, the mapping to the layout input of a render, the reports of what
  is not drawn, and the drawing of the diagram; see _FlowchartModel Unit Design_ (`flowchart/flowchart-model.md`)

The subsystem's namespace documentation is in `Flowchart/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Flowchart-*` requirements in `docs/reqstream/canvas-net-mermaid/flowchart.yaml`. The SysML2 model
names the subsystem `MermaidFlowchart` (see _CanvasNetMermaid System Design_).

**Attribution.** The subsystem is a port of the flowchart diagram of the pinned Mermaid release (MIT; see
`third-party/README.md`): the parser ports the generated lexer and the grammar of `diagrams/flowchart/parser/flow.jison`
and the clean-up of `flowParser.ts`; the model ports the database of `diagrams/flowchart/flowDb.ts` (with the
accessibility texts of `diagrams/common/commonDb.ts` and `getEdgeId` of `utils.ts`), the settings and the `draw` of
`diagrams/flowchart/flowRenderer-v3-unified.ts`, the layout fallback of `rendering-util/render.ts`, the Font Awesome
expression of `rendering-util/createText.ts`, the view box of `rendering-util/setupViewPortForSVG.ts`, and
`insertTitle` of `utils.ts`. Every ported file carries the provenance header comment.

### Interfaces

**Exposed** (internal only): `FlowchartParser.Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`, which
returns the filled `FlowchartModel` or throws a located `MermaidSyntaxException`; and the `FlowchartModel` as the
`IMermaidDiagram` of the document: the metadata (`Title`, `AccTitle`, `Direction`, `NodeCount`, `EdgeCount`,
`SubgraphCount`) and `Layout(DiagramRenderContext)`, which returns the laid-out `Scene`. For tests and for the
renderer the model also exposes its elements (`Vertices`, `Edges`, `Subgraphs`, `Classes`, `DefaultInterpolate`,
`DefaultStyle`, `AccessibleTitle`, `AccessibleDescription`), `GetData(MermaidConfig, string)` (the layout input), and
`ReportUnsupported(MermaidConfig, MermaidReportSink)`.

**Consumed**: from the `Syntax` subsystem the preprocessed text and its map to the original text (`PreprocessedSource`,
`MappedText`), the detected diagram (`DetectedDiagram`), the scanner and token cursor (`SourceScanner`, `ScanRule`,
`ScanToken`, `TokenCursor`), the YAML subset reader of the shape data (`YamlSubsetParser`), and the label sanitizer
(`HtmlSanitizer`); from `Configuration` the effective `MermaidConfig` and the style sheet `FlowchartStyles`; from
`Rendering` the layout input types (`LayoutData`, `LayoutNode`, `LayoutEdge`), the shape registry (`ShapeNames`), and
the drawing (`ClusterLayout`, `ShapeContext`, `LabelBuilder`, `NodeImages`); from `Dagre` the `LayoutWorkMeter`; from
`Scenegraph` the scene types; from `Graphlib` the JavaScript index rule of `ObjectKeyMap`; and from the system the
public `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`, `MermaidDiagnosticCodes`, and `MermaidReportSink`.

**Consumers**: the document pipeline of the system (`MermaidDocument.Pipeline.cs`) calls the parser for a detected
flowchart, keeps the model as the document's diagram, reads its metadata, and calls `Layout` for every measure and
render.

### Design

**Two steps, as in Mermaid.** Mermaid's parser calls its flowchart database as it reduces the rules of the grammar,
and its renderer asks the database for the layout data when it draws. The subsystem keeps that split: the parser fills
the model statement by statement with the same calls, in the same order, as Mermaid's parser (so the vertex counter of
the DOM ids, the edge ids, and the subgraph members are Mermaid's), and the model maps itself to the layout input for
each render with that render's configuration. Everything that depends on the configuration (the curve, the padding,
the spacings, the look, and `inheritDir`) is therefore applied in `GetData`, not at parse time.

**Fidelity is proven against Mermaid itself.** The upstream vectors (`flowchart-parser.json`, 207 cases recorded from
the pinned Mermaid release in a browser) give, for each valid text, Mermaid's database facts and layout data, and for
each invalid text the error Mermaid raises with the expected tokens of its parser; the unit tests compare the port with
all of them. The 42 original fixtures in `Fixtures/flowchart/` cover the language as authors use it; the subsystem
tests open all of them, and the system-level reference layout tests compare their drawings with Mermaid's.

**Fail closed when opened.** Everything that can be wrong with the text is found when the document is opened, as a
located `MermaidSyntaxException`: a grammar or lexical error (with Mermaid's expected tokens), what Mermaid's database
fails on (a subgraph without a name, an unknown shape, a `linkStyle` index without an edge, invalid YAML), and the
limits of the open call (`MaxNestingDepth` in the parser; `MaxEdges`, `MaxNodes`, and `MaxLabelLength` in the model).
A render of an opened flowchart only reports what it does not draw (look, layout, icons) and then draws; it fails,
located, only for the nesting that subgraphs chained by id make beyond `MaxNestingDepth`, which the text does not show
(`GetData`), and for the limits of the shared rendering.

**Deterministic and bounded.** The parser and the model hold no shared state; a parsed model is never changed again,
so it is safe for concurrent renders. The lexer replaces every regular expression of Mermaid with a scanner that cannot
backtrack, and caches the far-looking rules per line, so the cost of reading a text is linear in its length; the model
is bounded by the limits above, and the drawing by the layout work meter and the image and pixel limits of the shared
rendering.

**Known differences from Mermaid** are listed in each unit design: errors and limits are located CanvasNet exceptions;
click actions, tooltips, and the accessibility texts are kept only as data; other looks, other layouts, and icons are
reported and drawn in the classic look with dagre and without the icon; an animated edge is drawn as a still picture.
