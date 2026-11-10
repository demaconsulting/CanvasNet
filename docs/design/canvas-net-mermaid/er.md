## Er

![MermaidEr Structure](MermaidErView.svg)

### Overview

The `Er` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Er`, folder `src/DemaConsulting.CanvasNet.Mermaid/Er/`)
reads the text of an entity relationship diagram (the `erDiagram` keyword, Mermaid's diagram id `er`) into Mermaid's ER
database, turns that database into the input of the shared rendering, and frames the drawing as Mermaid's ER renderer
does. Like the flowchart and the class diagram, an entity relationship diagram is laid out by the shared layered
layout and drawn by the shared unified renderer: the entity boxes, the cardinality markers, and the clusters of the
subgraphs are drawn by the `Rendering` subsystem, and this subsystem gives it the graph that Mermaid's ER database gives
Mermaid's renderer. It is **internal**: its types are visible only to the other subsystems of the system and, through
`InternalsVisibleTo`, to the test assembly. It contains two units:

- **ErParser** - the internal sealed `ErParser` (a partial class in `ErParser.cs` and `ErParser.Lexer.cs`) and the token
  names of `ErTokens` (in `ErParser.Lexer.cs`): the lexer and the grammar of Mermaid's ER language, the database
  operations it calls in Mermaid's order, the values of the statements that a subgraph takes as its nodes, the located
  errors with Mermaid's expected tokens, and the nesting and label limits; see _ErParser Unit Design_
  (`er/er-parser.md`)
- **ErModel** - the internal sealed `ErModel` (a partial class in `ErModel.cs` and `ErModel.LayoutData.cs`) and its
  element types in `ErModel.cs` (`ErEntity`, `ErAttribute`, `ErRelationshipSpec`, `ErRelationship`, `ErStyleClass`,
  and `ErSubgraph`): Mermaid's ER database, the diagram metadata, the node, edge, and label limits, the mapping to the
  layout input of a render, the reports of what is not drawn, and the frame of the drawing; see _ErModel Unit Design_
  (`er/er-model.md`)

The subsystem's namespace documentation is in `Er/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Er-*` requirements in `docs/reqstream/canvas-net-mermaid/er.yaml`. The SysML2 model names the
subsystem `MermaidEr` (see _CanvasNetMermaid System Design_).

**Attribution.** The subsystem is a port of the entity relationship diagram of the pinned Mermaid release (MIT; see
`third-party/README.md`): the parser ports the generated lexer and the grammar of `diagrams/er/parser/erDiagram.jison`
(its rules, its start conditions, and the order of its cardinality rules); the model ports the database of
`diagrams/er/erDb.ts` (`addEntity`, `addAttributes`, `addRelationship`, `addSubGraph` with `makeUniq`,
`addCssStyles`, `addClass`, `setClass`, `getCompiledStyles`, and `getData`) with the types of `diagrams/er/erTypes.ts`
and the accessibility texts of `diagrams/common/commonDb.ts`, and the settings and the `draw` of
`diagrams/er/erRenderer-unified.ts` (the ER spacing, the markers, the title, and the view box padding). The entity box
of `rendering-elements/shapes/erBox.ts` and the ER markers of `markers.js` are ported by the Rendering subsystem, and
the ER style sheet of `diagrams/er/styles.ts` by the Configuration subsystem. Every ported file carries the provenance
header comment.

### Interfaces

**Exposed** (internal only): `ErParser.Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`, which returns the
filled and completed `ErModel` or throws a located `MermaidSyntaxException`; and the `ErModel` as the `IMermaidDiagram`
of the document: the metadata (`Title`, which is always null because no statement of the grammar can set a title,
`AccTitle`, `Direction`, `NodeCount` for the drawn entities, `EdgeCount` for the relationships, and `SubgraphCount` for
the subgraphs) and `Layout(DiagramRenderContext)`, which returns the laid-out `Scene`. For tests and for the vectors the
model also exposes its elements (`Entities`, `Relationships`, `StyleClasses`, `Subgraphs`, `MermaidId`,
`AccessibleTitle`, `AccessibleDescription`, and `KeywordLocation`), `GetData(MermaidConfig)` (the layout input), and
the static `ReportUnsupported(MermaidConfig, MermaidReportSink)`.

**Consumed**: from the `Syntax` subsystem the preprocessed text and its map to the original text (`PreprocessedSource`,
`MappedText`), the detected diagram (`DetectedDiagram`), the scanner and token cursor (`SourceScanner`, `ScanRule`,
`ScanMatcher`, `ScanToken`, `TokenCursor`), the JavaScript character classes and the run matcher of `ScanPatterns`,
JavaScript's `trim` (`Preprocessor.TrimJs`), and the text sanitizer (`HtmlSanitizer`); from `Configuration` the
effective `MermaidConfig` with its `ErConfig` section (the spacing and the title margin), the look, the layout, the font
settings (`MermaidFontConfig`, `ConfigScalar`), and the style sheet `ErStyles`; from `Rendering` the layout input types
(`LayoutData`, `LayoutNode`, `LayoutEdge`, `LayoutErBox`, `LayoutErAttribute`) and the drawing
(`ClusterLayout.RenderDiagram`, `ShapeContext`, `LabelBuilder`, `NodeImages`); from `Dagre` the `LayoutWorkMeter`; from
`Scenegraph` the `Scene`; and from the system the `IMermaidDiagram` contract, `DiagramRenderContext`, and the public
`MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`, `MermaidDiagnosticCodes`, and `MermaidReportSink`.

**Consumers**: the document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls the
parser for a detected `er` diagram with the limits of the open call, keeps the model as the document's diagram, reads
its metadata, and calls `Layout` for every measure and render.

### Design

**Actions where jison reduces, as in Mermaid.** Mermaid's ER parser calls its database as it reduces the rules of the
grammar, and its renderer asks the database for the layout data (`getData`) when it draws. The subsystem keeps that
split: the parser calls the model's operations (`AddEntity`, `AddAttributes`, `AddRelationship`, `AddSubgraph`,
`AddClass`, `SetClass`, `AddCssStyles`, `SetDirection`, and the accessibility setters) at the points where jison runs
the actions, after the lookahead that decides the reduction has been checked, so the entity ids, the order of the
entities, the nodes of the subgraphs, and the order of grammar and limit errors are Mermaid's. As in Mermaid, every
statement also gives a value (the entity names of an entity or relationship statement, the id of a nested subgraph,
the trimmed accessibility texts, and the keyword of a `classDef`, `class`, or `style` statement), and a subgraph takes
the values of its statements as its nodes. When the text is read, the model completes what Mermaid's `getData`
computes from the database alone (the color index of each drawn entity and the compiled styles of the classes), so the
model is never changed again; it maps itself to the layout input for each render with that render's configuration
(the look and the spacing).

**Metadata from the text alone.** The counts of the document describe the diagram that is drawn: the entities as
nodes, except an entity that is only named after a subgraph (Mermaid's database adds one for a relationship to a
subgraph, but `getData` skips it), the relationships as edges, and the subgraphs as subgraphs. The direction is the
last top-level `direction` statement, else `TB`; it is also the direction the diagram is laid out in, because Mermaid's
ER renderer takes the database's direction.

**Drawing through the shared renderer.** `ErModel.Layout` reports first, then maps the model to `LayoutData` and has
`ClusterLayout.RenderDiagram` lay it out and draw it with the ER style sheet, the label builder, the node images, the
text width of the configured font, and the layout work meter of the render, and with the frontmatter title at the
`er.titleTopMargin` and the view box padding of 8 of Mermaid's ER renderer. The entity box shape (`erBox`), the ER
markers of the cardinalities, and the clusters are parts of the shared `Rendering` subsystem (see _Rendering Subsystem
Design_).

**Fidelity is proven against Mermaid itself.** The upstream vectors (`er-parser.json`, 246 cases recorded from the
pinned Mermaid release in a browser, 27 of them errors) give, for each valid text, Mermaid's database facts and the
facts of its `getData()`, and for each invalid text the error Mermaid raises with the expected tokens of its parser; the
unit tests compare the parser and the model with all of them. The 12 original fixtures in `Fixtures/er/` cover the
language as authors use it; the subsystem tests open and lay out all of them, and the system-level reference layout
tests compare their drawings with Mermaid's.

**Fail closed when opened.** Everything that can be wrong with the text is found when the document is opened, as a
located `MermaidSyntaxException`: a grammar error (with Mermaid's expected tokens), a lexical error (possible only in a
`style` or `classDef` statement), and the limits of the open call (`MaxNestingDepth` for the nesting of subgraphs and
`MaxLabelLength` for every text the parser reads or joins; `MaxNodes` for the entities, those only named after a
subgraph included, and the subgraphs together, `MaxEdges` for the relationships, and `MaxLabelLength` for the texts the
model joins or derives). Mermaid's ER database raises no errors of its own. A render of an opened ER diagram reports
what it does not draw (a look other than classic, a layout other than dagre) and then draws; the two kinds of text on
which Mermaid's own render fails, a relationship whose side is named like a subgraph declared after it (its end is an
entity that is not drawn) and a subgraph that holds itself (through a relationship to it inside it), fail the render
with a located `mermaid-render-failed` from the shared cluster layout.

**Deterministic and bounded.** The parser and the model hold no shared state; a completed model is never changed
again, so it is safe for concurrent renders. The lexer replaces every regular expression of Mermaid with matchers that
cannot backtrack and caches its far-looking searches (a direction anywhere on the line and the last tilde of a line)
per line, and the parser reads statements, id lists, styles, attributes, and keys in loops, so the cost of reading a
text is linear in its length and only nested subgraphs recurse. The model keeps the CSS classes of an entity as a list
and the nodes of the earlier subgraphs as a running set, so it stays linear too, and it is bounded by the limits above;
the drawing is bounded by the layout work meter and the image limits of the shared rendering.

**Known differences from Mermaid** are listed in each unit design and in _CanvasNetMermaid System Design_: errors and
limits are located CanvasNet exceptions with hints; the accessibility texts are metadata only; a relationship side
named like a later subgraph and a subgraph that holds itself fail located where Mermaid's render fails too; the
`data-color-id` of the `redux-color` themes, the background of the hand-drawn look, and the markers of the `neo` look
are not modeled; and other looks and layouts are reported and drawn in the classic look with dagre.
