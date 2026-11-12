## Mindmap

![MermaidMindmap Structure](MermaidMindmapView.svg)

### Overview

The `Mindmap` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Mindmap`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Mindmap/`) reads the text of a mindmap (the `mindmap` keyword, Mermaid's diagram
id `mindmap`) into the tree that Mermaid's mindmap database keeps, and draws it as Mermaid's mindmap renderer draws it.
A mindmap is a tree, not a layered graph: Mermaid lays it out with the cose-bilkent force-directed layout, not with
dagre, so this subsystem draws its nodes with the shared `ShapeLibrary`, has the `Cose` subsystem place them, and draws
the edges with the shared `EdgeBuilder`. It is **internal**: its types are visible only to the other subsystems of the
system and, through `InternalsVisibleTo`, to the test assembly. It contains two units:

- **MindmapParser** - the internal sealed `MindmapParser` (a partial class in `MindmapParser.cs` and
  `MindmapParser.Lexer.cs`), the token names `MindmapTokens` (in `MindmapParser.Lexer.cs`), and the node type
  `MindmapNodeType`, the node `MindmapNode`, the edge `MindmapEdge`, and the diagram `MindmapDiagram` (in
  `MindmapParser.cs`): the lexer and the grammar of Mermaid's mindmap language, the calls of Mermaid's mindmap
  database, the sections, classes, and edges that Mermaid's `getData` derives, the metadata of the diagram, the located
  errors with Mermaid's expected tokens, and the node, depth, and label limits; see _MindmapParser Unit Design_
  (`mindmap/mindmap-parser.md`)
- **MindmapRenderer** - the internal sealed `MindmapRenderer` (`MindmapRenderer.cs`): the drawing of Mermaid's mindmap
  renderer and of its cose-bilkent renderer (the node data, the changes per shape, the reports, the node sizes in
  single precision, the layout call, the edges through the layout's points, and the view box); see _MindmapRenderer
  Unit Design_ (`mindmap/mindmap-renderer.md`)

The subsystem's namespace documentation is in `Mindmap/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Mindmap-*` requirements in `docs/reqstream/canvas-net-mermaid/mindmap.yaml`. The SysML2 model names
the subsystem `MermaidMindmap` (see _CanvasNetMermaid System Design_).

**Attribution.** The subsystem is a port of the mindmap of the pinned Mermaid release (MIT; see
`third-party/README.md`): the parser ports the lexer and the grammar of `diagrams/mindmap/parser/mindmap.jison` and the
`MindmapDB` of `diagrams/mindmap/mindmapDb.ts` (`addNode`, `getParent`, `getType`, `decorateNode`, and the
`assignSections`, `flattenNodes`, and `generateEdges` of `getData`); the renderer ports the `draw` of
`diagrams/mindmap/mindmapRenderer.ts` and the cose-bilkent renderer of `layout-algorithms/cose-bilkent/render.ts`. The
two mindmap shapes (`defaultMindmapNode` and `mindmapCircle`) are ported by the `Rendering` subsystem, the mindmap style
sheet (`diagrams/mindmap/styles.ts`) and the mindmap section of the configuration by the `Configuration` subsystem, and
the layout by the `Cose` subsystem. Every ported file carries the provenance header comment.

### Interfaces

**Exposed** (internal only): `MindmapParser.Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`, which returns
the filled `MindmapDiagram` or throws a located `MermaidSyntaxException`; and the `MindmapDiagram` as the
`IMermaidDiagram` of the document: the metadata (`NodeCount` for the nodes, `EdgeCount` for the edges, one fewer than
the nodes and 0 for an empty mindmap, `SubgraphCount` always 0, and `Title`, `AccTitle`, and `Direction` always null)
and `Layout(DiagramRenderContext)`, which creates a `MindmapRenderer` and returns the drawn `Scene`. For tests and for
the vectors the diagram also exposes its state (`Nodes`, `Edges`, `Root`, and `KeywordLocation`), the parser
`TypeOf`, and the renderer `DataOf`, `Prepared`, `Box32`, and `Sizes`.

**Consumed**: from the `Syntax` subsystem the preprocessed text and its map to the original text (`PreprocessedSource`,
`MappedText`), the detected diagram (`DetectedDiagram`), the jison lexer semantics (`SourceScanner`, `ScanRule`,
`ScanToken`, `ScanPatterns`, and `TokenCursor` with `JoinAlternatives`), and the text sanitizer (`HtmlSanitizer`); from
`Configuration` the effective `MermaidConfig` with its `MindmapConfig` section (`padding` and `maxNodeWidth`), its
`Look` and `Layout`, `LocationOf`, and the mindmap style sheet `MindmapStyles`; from `Rendering` the node shapes
(`ShapeLibrary.Draw` with `ShapeContext`, `ShapeResult`, `LayoutNode`, and `LayoutEdge`), the edges (`EdgeBuilder.Draw`
with `EdgeRoute`, `EdgeEnd`, `NodeBox`, and `ShapePoint`), and the labels (`LabelBuilder` with `DecodeEntities` and
`ContainsFontAwesome`); from `Cose` the layout (`CoseLayout.Run` with `CoseSize`, `CoseLink`, `CosePoint`, and
`CoseResult`); from `Dagre` the `LayoutWorkMeter`; from `Scenegraph` the `Scene`, `SceneGroup`, `SceneItem`, and
`SceneRect`; and from the system the `IMermaidDiagram` contract, `DiagramRenderContext`, `MermaidReportSink`, and the
public `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`, `MermaidRenderException`, and
`MermaidDiagnosticCodes`.

**Consumers**: the document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls the
parser for a detected `mindmap` diagram with the limits of the open call, keeps the diagram as the document's diagram,
reads its metadata, and calls `Layout` for every measure and render.

### Design

**Parse once, draw per render, as in Mermaid.** Mermaid's jison parser calls the mindmap database while it reads the
text, and the renderer reads the database's `getData` and the configuration when it draws. The subsystem keeps that
split: `MindmapParser` reads the whole text into an immutable `MindmapDiagram` when the document is opened, the diagram
derives the sections, classes, and edges of `getData` once, and every render creates a new `MindmapRenderer` that draws
the diagram under the configuration, the theme, and the fonts of that render. The upstream vectors compare the diagram
with Mermaid's database and `getData` fact by fact.

**The language of the jison grammar.** The lexer applies Mermaid's 37 lexer rules with their five start conditions, the
first that matches winning, so a text splits into the same tokens as in Mermaid, quirks included (an id runs to the
first bracket, a blank line after a statement is white space that ends with a line feed, a tab is one character of
indentation). A recursive descent parser with one token of lookahead accepts the language of the LALR grammar and
checks each lookahead against the actions of the LALR state jison is in, so an error is reported where and with the
expected tokens that Mermaid reports. Each database call is made where jison reduces.

**The tree of the indentation.** As in Mermaid's `addNode`, the first node is the root and sets the base level, every
other node's level is its indentation less the base level, and its parent is the last node with a smaller level. The
parser keeps the path from the root to the last node, which holds that parent. A second root, or a node that no node can
take, is a located syntax error with Mermaid's message. The sections cycle through 11 among the root's children and are
inherited below, and each edge's depth class is the parent's indentation level plus one, as Mermaid writes them.

**Drawing Mermaid's mindmap.** `MindmapRenderer.Draw` follows Mermaid's `draw` and its cose-bilkent renderer: each node
gets the padding and wrap width of `getData` and the changes `draw` makes per shape, is drawn by `ShapeLibrary` with the
paint of `MindmapStyles`, and gives the layout the box of the drawn node as the browser's `getBBox` gives it, in single
precision. `CoseLayout.Run` places the nodes and gives each edge cytoscape's start, middle, and end points, through which
`EdgeBuilder` draws the edge with the `basis` curve, not cut at the node outlines, because Mermaid hands the edge drawing
node copies without an intersect function. The view box is the box of the drawing grown by `mindmap.padding`.

**Fidelity is proven against Mermaid itself.** The upstream vectors (`mindmap-parser.json`, 246 cases recorded from the
pinned Mermaid release in a browser, 42 of them errors) give, for each valid text, the nodes of Mermaid's database and
the nodes and edges of its `getData`, and for each invalid text the first error Mermaid reports; the unit tests compare
the parser and the node data with all of them. The 14 original fixtures in `Fixtures/mindmap/` cover the language and
the settings as authors use them; the hardening tests open and render all of them, and the system-level reference
layout tests compare their node sizes, their layout given the browser's sizes, and their whole drawings with the goldens
drawn by Mermaid.

**Fail closed when opened.** Everything that can be wrong with the text is found when the document is opened, as a
located `MermaidSyntaxException`: a lexical error, a grammar error (with Mermaid's expected tokens), a second root, a
decoration before any node (where Mermaid fails with a JavaScript error), and the limits of the open call (`MaxNodes`,
`MaxNestingDepth` for the depth of a node in the tree, and `MaxLabelLength` for every text the parser keeps, sanitizes,
or joins into a class list). A render of an opened mindmap fails only on the limits of the render (the label length
and the layout work among them), on a layout that Mermaid's layout cannot complete (`mermaid-render-failed`), on
cancellation, or, in strict mode, on the first unsupported feature.

**Reports.** A look other than classic, a layout other than cose-bilkent, an icon of a node, and a Font Awesome icon in
a label are reported as unsupported features, in that order, and the mindmap is still drawn in the classic look with the
cose-bilkent layout and every node with its shape and label. Mermaid's current renderer does not draw mindmap icons
either, so the picture is Mermaid's.

**Deterministic and bounded.** The parser and the diagram hold no shared state; a parsed diagram is never changed again,
so it is safe for concurrent renders, and a renderer draws one render on one thread. The lexer's matchers cannot
backtrack and the grammar is read in loops, so reading a text is linear in its length and never recursive. The layout
makes no random choice and computes its transcendental functions without the host's math library (see _Cose Design_).
The layout work meter of the render (`MaxLayoutWork`) is charged for the node data, every measured label, every step of
the layout, and every drawn edge, and checks cancellation.

**Known differences from Mermaid** are listed in each unit design and in _CanvasNetMermaid System Design_: errors and
limits are located CanvasNet exceptions with hints, and only the first error is reported; a node size that differs in
its last bit from the browser's can move the nodes of the chaotic layout far; icons are reported; a layout other than
cose-bilkent is reported and not used; an empty mindmap has the padded empty box as its view box; and the depth of the
tree is bounded.
