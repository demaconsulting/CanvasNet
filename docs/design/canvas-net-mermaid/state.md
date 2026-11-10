## State

![MermaidState Structure](MermaidStateView.svg)

### Overview

The `State` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.State`, folder
`src/DemaConsulting.CanvasNet.Mermaid/State/`) reads the text of a state diagram (the `stateDiagram` and
`stateDiagram-v2` keywords, and the legacy `state` id that a `state.defaultRenderer` other than `dagre-wrapper`
selects) into Mermaid's statement tree, applies the tree as Mermaid's state database and data fetcher do, turns the
result into the input of the shared rendering, and frames the drawing as Mermaid's unified state renderer does. Like the
flowchart and the class diagram, a state diagram is laid out by the shared layered layout and drawn by the shared
unified renderer: the state shapes, the composite states, the concurrent regions, the note groups, and the `barb`
marker are drawn by the `Rendering` subsystem, and this subsystem gives it the graph Mermaid's data fetcher gives
Mermaid's renderer. It is **internal**: its types are visible only to the other subsystems of the system and, through
`InternalsVisibleTo`, to the test assembly. It contains two units:

- **StateParser** - the internal sealed `StateParser` (a partial class in `StateParser.cs` and `StateParser.Lexer.cs`)
  and the token names of `StateTokens` (in `StateParser.Lexer.cs`): the lexer and the grammar of Mermaid's state
  language, the statement tree its actions build, the located errors with Mermaid's expected tokens, and the nesting and
  label limits; see _StateParser Unit Design_ (`state/state-parser.md`)
- **StateModel** - the internal sealed `StateModel` (a partial class in `StateModel.cs` and `StateModel.LayoutData.cs`)
  and its element types (`StateStatement`, `StateNote`, `StateRecord`, `StateRelation`, `StateStyleClass`, and
  `StateLink` in `StateModel.cs`; `StateNode`, `StateNodeKind`, and `StateEdge` in `StateModel.LayoutData.cs`):
  Mermaid's state database and data fetcher, the database error, the node, edge, nesting, and joined label limits, the
  diagram metadata, the mapping to the layout input of a render, the reports of what is not drawn, and the frame of the
  drawing; see _StateModel Unit Design_ (`state/state-model.md`)

The subsystem's namespace documentation is in `State/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-State-*` requirements in `docs/reqstream/canvas-net-mermaid/state.yaml`. The SysML2 model names the
subsystem `MermaidState` (see _CanvasNetMermaid System Design_).

**Attribution.** The subsystem is a port of the state diagram of the pinned Mermaid release (MIT; see
`third-party/README.md`): the parser ports the generated lexer and the grammar of
`diagrams/state/parser/stateDiagram.jison` and the statement tree its actions build; the model ports the database of
`diagrams/state/stateDb.ts` (`setRootDoc`, `docTranslator`, `extract`, `addState`, `addDescription`, `addStyleClass`,
`setCssClass`, `getDirection`, and Mermaid's error messages) with the constants of `diagrams/state/stateCommon.ts`, the
data fetcher of `diagrams/state/dataFetcher.ts` (the nodes and edges of the layout with their DOM ids, shapes,
descriptions, cluster classes, note groups, and note edges), and the settings and the `draw` of
`diagrams/state/stateRenderer-v3-unified.ts` (the state spacing, the `barb` marker, the title, and the view box padding
of 8). Every ported file carries the provenance header comment.

### Interfaces

**Exposed** (internal only): `StateParser.Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`, which returns the
filled `StateModel` or throws a located `MermaidSyntaxException`; and the `StateModel` as the `IMermaidDiagram` of the
document: the metadata (`Title`, which is always null because the state grammar has no title statement, `AccTitle`,
`Direction` for the direction the diagram is laid out in, `NodeCount` for the states, `EdgeCount` for the transitions,
and `SubgraphCount` for the composite states) and `Layout(DiagramRenderContext)`, which returns the laid-out `Scene`.
For the parser the model exposes `NextDividerId`, `SetAccessibleTitle`, `SetAccessibleDescription`, `SetRootDocument`,
and the static helpers `Sanitize` and `TrimColon`. For tests and for the vectors it also exposes its elements
(`MermaidId`, `IsLegacy`, `KeywordLocation`, Mermaid's `Direction` and the `LayoutDirection`, `AccessibleTitle`,
`AccessibleDescription`, `RootDocument`, `States`, `Relations`, `StyleClasses`, `Links`, `Nodes`, and `Edges`),
`GetData(MermaidConfig)` (the layout input), and `ReportUnsupported(MermaidConfig, MermaidReportSink)`.

**Consumed**: from the `Syntax` subsystem the preprocessed text and its map to the original text (`PreprocessedSource`,
`MappedText`), the detected diagram (`DetectedDiagram`), the scanner and token cursor (`SourceScanner`, `ScanRule`,
`ScanMatcher`, `ScanToken`, `TokenCursor`), the JavaScript character classes of `ScanPatterns`, JavaScript's `trim`
(`Preprocessor.TrimJs`), and the text sanitizer (`HtmlSanitizer`); from `Configuration` the effective `MermaidConfig`
with its `StateConfig` section (the spacing, the title margin, and the `defaultRenderer` named in the legacy report),
the flowchart padding that Mermaid's data fetcher gives the notes, the look, the layout, the locations of keys, and the
style sheet `StateStyles`; from `Rendering` the layout input types (`LayoutData`, `LayoutNode`, `LayoutEdge`) and the
drawing (`ClusterLayout.RenderDiagram`, `ShapeContext`, `LabelBuilder`, `NodeImages`); from `Dagre` the
`LayoutWorkMeter`; from `Scenegraph` the `Scene`; and from the system the `IMermaidDiagram` contract,
`DiagramRenderContext`, and the public `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`,
`MermaidRenderException`, `MermaidDiagnosticCodes`, and `MermaidReportSink`.

**Consumers**: the document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls the
parser for a detected state diagram (the `state` type, which covers both keywords and the legacy id), keeps the model as
the document's diagram, reads its metadata, and calls `Layout` for every measure and render.

### Design

**A statement tree applied once, as in Mermaid.** Mermaid's state parser builds a tree of statements while it reduces
the rules of its grammar and hands the whole tree to the database once, at the end (`setRootDoc`); the database then
translates the tree, extracts the states, transitions, notes, styles, and links of the top-level statements, and runs
the data fetcher, which makes the nodes and edges of the layout. The subsystem keeps that split: the parser builds the
`StateStatement` tree (calling the model only where jison's actions do, for the accessibility texts and the ids of the
concurrency dividers, numbered in parse order) and calls `SetRootDocument` once, so the ids of the start and end states,
the regions, the DOM ids, and the order of the nodes and edges are Mermaid's. The nodes and edges are made when the
text is parsed; `GetData` maps them for each render with that render's configuration, so what depends on the
configuration (the look, the padding of the notes, and the spacing) is applied then, not at parse time.

**The legacy order.** For the legacy `state` id Mermaid's legacy database applies the statements before it translates
the tree, so its start and end states are `start1`, `start2`, and so on in the database. The model does the same, while
the nodes and edges of the layout come from the translated tree as for the current diagram. The legacy renderer is not
ported: the diagram is drawn by the unified renderer, with the identical pixels of a `stateDiagram-v2`, and reported as
`mermaid-layout-unsupported` at the top-level `state` key of the configuration.

**Metadata from the text alone.** The counts of the document describe the diagram that is drawn: the states as nodes
(plain states, states with descriptions, the start and end states, and the fork, join, and choice states), the
transitions as edges, and the composite states as subgraphs; notes, note groups, the links of notes, and concurrent
regions are not counted. The direction of the metadata is the direction the diagram is laid out in, the last top-level
`direction` statement (else `TB`); the model keeps Mermaid's `getDirection` (the first one) apart for the vectors.

**Drawing through the shared renderer.** `StateModel.Layout` reports first, then maps the model to `LayoutData`, fails a
node that Mermaid leaves without a shape (a note on a state that appears nowhere before it, on which Mermaid's render
fails too), and has `ClusterLayout.RenderDiagram` lay it out and draw it with the state style sheet, the label builder,
the node images, and the layout work meter of the render, and with the frontmatter title at the title margin of the
state section and the view box padding of 8 of Mermaid's state renderer. The state shapes (`stateStart`, `stateEnd`,
`forkJoin`, `choice`, `rectWithTitle`, and `note`), the clusters of composite states, regions, and notes
(`roundedWithTitle`, `divider`, and `noteGroup`), and the `barb` marker are parts of the shared `Rendering` subsystem
(see _Rendering Subsystem Design_). A parent cycle (`state A { B }` and `state B { A }`, which Mermaid's database
accepts) fails in the cluster layout, located, with `mermaid-render-failed`, as graphlib's `setParent` fails Mermaid's
render.

**Fidelity is proven against Mermaid itself.** The upstream vectors (`state-parser.json`, 157 cases recorded from the
pinned Mermaid release in a browser: 97 inputs of Mermaid's state parser specs, 46 original texts, and the 14 fixtures,
12 of them errors) give, for each valid text, Mermaid's translated statement tree, its database facts, and the facts of
its `getData()`, and for each invalid text the error Mermaid raises with the expected tokens of its parser or the
message of its lexer or database; the unit tests compare the parser and the model with all of them. The 14 original
fixtures in `Fixtures/state/` cover the language as authors use it; the subsystem tests open and lay out all of them,
and the system-level reference layout tests compare their drawings with Mermaid's.

**Fail closed when opened.** Everything that can be wrong with the text is found when the document is opened, as a
located `MermaidSyntaxException`: a grammar or lexical error (with Mermaid's expected tokens), the errors Mermaid throws
without a token (a composite state name of two words, and a description of a composite state beyond its title), and
the limits of the open call. `MaxNestingDepth` bounds the composite states in the parser and the composite states and
their concurrent regions, each a level, in the model; `MaxLabelLength` bounds every token text the parser keeps and
every text the model joins or derives (the descriptions under a title joined with `<br/>`, the `<parent>_start` and
`<parent>_end` ids, the text styles with `fill` written `bgFill`, and, as they grow, the CSS classes of a state, the
styles of a style class, and the compiled styles of a state, which a state listed again or a class defined again
repeats as in Mermaid); `MaxNodes` bounds the states of the database
(those that only a `class` or `style` statement names included) and the nodes of the layout (states, composite states,
concurrent regions with the generated last region, notes, and note groups); and `MaxEdges` bounds the transitions, the
links of notes not counted. A render of an opened state diagram only reports what it does not draw (the legacy
renderer, a look other than classic, a layout other than dagre), and fails, located, only where Mermaid's render fails,
or where composite states nest deeper than `MaxNestingDepth` through their names (a composite state named in another
one and declared again at the top keeps that parent), which the layout data checks as a `MermaidRenderException`.

**Deterministic and bounded.** The parser and the model hold no shared state; a parsed model is never changed again,
so it is safe for concurrent renders. The generated region ids are numbered per model (`id-random-N`) instead of
Mermaid's random ids. The lexer replaces every regular expression of Mermaid with matchers that cannot backtrack and
caches its far-looking line searches (the directions, the pseudo-state markers, and the brace of the single-word rule)
per line, and the parser reads statements in loops, so the cost of reading a text is linear in its length and only
nested composite states recurse; the model is bounded by the limits above, and the drawing by the layout work meter and
the image limits of the shared rendering.

**Known differences from Mermaid** are listed in each unit design and in _CanvasNetMermaid System Design_: errors and
limits are located CanvasNet exceptions with hints; the region ids are `id-random-N`; the click statements are kept
only as data, never drawn or bound; texts are always sanitized as Mermaid sanitizes them at its strict security level,
the only level modeled; the accessibility texts are metadata only; the legacy renderer is drawn by the unified renderer
and reported; parent cycles and a note on a state that appears nowhere before it fail, located, where Mermaid's render
fails; and other looks and layouts are reported and drawn in the classic look with dagre.
