## Class

![Class Structure](MermaidClassView.svg)

### Overview

The `Class` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Class`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Class/`) reads the text of a class diagram (the `classDiagram` and
`classDiagram-v2` keywords, and the legacy `class` id that a `class.defaultRenderer` other than `dagre-wrapper`
selects) into Mermaid's class database, turns that database into the input of the shared rendering, and frames the
drawing as Mermaid's class renderer does. Like the flowchart, a class diagram is laid out by the shared layered layout
and drawn by the shared unified renderer: the class boxes, the relation markers, and the cardinalities are drawn by the
`Rendering` subsystem, and this subsystem gives it the graph Mermaid's class database gives Mermaid's renderer. It is
**internal**: its types are visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to
the test assembly. It contains two units:

- **ClassParser** - the internal sealed `ClassParser` (a partial class in `ClassParser.cs` and `ClassParser.Lexer.cs`)
  and the token names of `ClassTokens` (in `ClassParser.Lexer.cs`): the lexer and the grammar of Mermaid's class
  language, the database operations it calls in Mermaid's order, the located errors with Mermaid's expected tokens, and
  the nesting and label limits; see _ClassParser Unit Design_ (`class/class-parser.md`)
- **ClassModel** - the internal sealed `ClassModel` (a partial class in `ClassModel.cs` and `ClassModel.LayoutData.cs`)
  and its element types in `ClassModel.cs` (`ClassNode`, `ClassMember`, `ClassRelation`, `ClassRelationInput`,
  `ClassRelationType`, `ClassNote`, `ClassInterface`, `ClassNamespace`, and `ClassStyleClass`): Mermaid's class
  database, the URL check of its strict security level, the diagram metadata, the node, edge, and label limits, the
  mapping to the layout input of a render, the reports of what is not drawn, and the frame of the drawing; see
  _ClassModel Unit Design_ (`class/class-model.md`)

The subsystem's namespace documentation is in `Class/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Class-*` requirements in `docs/reqstream/canvas-net-mermaid/class.yaml`. The SysML2 model names the
subsystem `MermaidClass` (see _CanvasNetMermaid System Design_).

**Attribution.** The subsystem is a port of the class diagram of the pinned Mermaid release (MIT; see
`third-party/README.md`): the parser ports the generated lexer and the grammar of
`diagrams/class/parser/classDiagram.jison`; the model ports the database of `diagrams/class/classDb.ts` (with the
accessibility texts of `diagrams/common/commonDb.ts`), the class member of `diagrams/class/classTypes.ts`
(`ClassMember`), and the settings and the `draw` of `diagrams/class/classRenderer-v3-unified.ts`. The link check of the
model is a port of `sanitizeUrl` of `@braintree/sanitize-url` (MIT, a dependency of Mermaid; see
`third-party/README.md`), which Mermaid's `formatUrl` calls at its strict security level. Every ported file carries the
provenance header comment.

### Interfaces

**Exposed** (internal only): `ClassParser.Parse(PreprocessedSource, DetectedDiagram, MermaidLimits, bool)`, which
returns the filled `ClassModel` or throws a located `MermaidSyntaxException` (the last argument is the text's own
`class.hierarchicalNamespaces` setting, true by default); and the `ClassModel` as the `IMermaidDiagram` of the
document: the metadata (`Title`, which is always null because the class grammar has no title statement, `AccTitle`,
`Direction`, `NodeCount` for the classes, `EdgeCount` for the relations, and `SubgraphCount` for the namespaces drawn
under the text's own configuration) and `Layout(DiagramRenderContext)`, which returns the laid-out `Scene`. For tests
and for the vectors the model also exposes its elements (`Classes`, `Relations`, `Notes`, `Interfaces`, `Namespaces`,
`StyleClasses`, `GetClass`, `MermaidId`, `AccessibleTitle`, and `AccessibleDescription`), `GetData(MermaidConfig)` (the
layout input), `ReportUnsupported(MermaidConfig, MermaidReportSink)`, and the static helpers `ParseGenericTypes` and
`FormatUrl`.

**Consumed**: from the `Syntax` subsystem the preprocessed text and its map to the original text (`PreprocessedSource`,
`MappedText`), the detected diagram (`DetectedDiagram`), the scanner and token cursor (`SourceScanner`, `ScanRule`,
`ScanToken`, `TokenCursor`), the character classes of `ScanPatterns` (including the `UNICODE_TEXT` letters that the
class and flowchart lexers share), JavaScript's `trim` (`Preprocessor.TrimJs`), and the text sanitizer
(`HtmlSanitizer`); from `Configuration` the effective `MermaidConfig` with its `ClassConfig` section (which carries the
spacing and title margin that Mermaid's class renderer reads from the state section), the font settings, and the style
sheet `ClassStyles`; from `Rendering` the layout input types (`LayoutData`, `LayoutNode`, `LayoutEdge`,
`LayoutClassBox`, `LayoutClassMember`) and the drawing (`ClusterLayout.RenderDiagram`, `ShapeContext`, `LabelBuilder`,
`NodeImages`); from `Dagre` the `LayoutWorkMeter`; from `Scenegraph` the `Scene`; and from the system the
`IMermaidDiagram` contract, `DiagramRenderContext`, and the public `MermaidLimits`, `MermaidLocation`,
`MermaidSyntaxException`, `MermaidDiagnosticCodes`, and `MermaidReportSink`.

**Consumers**: the document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls the
parser for a detected class diagram with the `class.hierarchicalNamespaces` setting of the configuration that the text
itself declares (its frontmatter and directives), keeps the model as the document's diagram, reads its metadata, and
calls `Layout` for every measure and render.

### Design

**Actions where jison reduces, as in Mermaid.** Mermaid's class parser calls its database as it reduces the rules of
the grammar, and its renderer asks the database for the layout data (`getData`) when it draws. The subsystem keeps that
split: the parser calls the model's operations (`AddClass`, `AddMember`, `AddRelation`, `AddNamespace`, `AddNote`,
`DefineClass`, `SetCssClass`, `SetLink`, and so on) at the points where jison runs the actions, after the lookahead that
decides the reduction has been checked, so the DOM ids, the order of the classes, the members of the namespaces, and
the order of grammar and database errors are Mermaid's. The model maps itself to the layout input for each render with
that render's configuration, so everything that depends on the configuration (the namespace mode, the paddings, the
spacing, the look, and the note colors) is applied in `GetData`, not at parse time.

**Metadata from the text alone.** The counts of the document describe the diagram that is drawn: classes as nodes
(notes and lollipop interfaces are not counted), relations as edges (the links of notes are not counted), and the
namespaces drawn as subgraphs. Mermaid draws only the declared namespaces when `class.hierarchicalNamespaces` is false,
so the registry passes the setting of the text's own configuration to the parser, which keeps it on the model for
`SubgraphCount`. A `class.hierarchicalNamespaces` given only in the site configuration of the render options is not
known when the document is opened, so it changes the drawing but not the count, as every descriptive property of the
document comes from the text.

**Drawing through the shared renderer.** `ClassModel.Layout` reports first, then maps the model to `LayoutData` and has
`ClusterLayout.RenderDiagram` lay it out and draw it with the class style sheet, the label builder, the node images,
and the layout work meter of the render, and with the frontmatter title and the view box padding of 8 of Mermaid's
class renderer. The class box shape, the class markers, the terminal labels of the cardinalities, and the underline of
static members are parts of the shared `Rendering` and `Scenegraph` subsystems (see _Rendering Subsystem Design_).

**Fidelity is proven against Mermaid itself.** The upstream vectors (`class-parser.json`, 179 cases recorded from the
pinned Mermaid release in a browser, 11 of them errors) give, for each valid text, Mermaid's database facts and the
facts of its `getData()`, and for each invalid text the error Mermaid raises with the expected tokens of its parser or
the message of its database; the unit tests compare the parser and the model with all of them. The 16 original
fixtures in `Fixtures/class/` cover the language as authors use it; the subsystem tests open and lay out all of them,
and the system-level reference layout tests compare their drawings with Mermaid's.

**Fail closed when opened.** Everything that can be wrong with the text is found when the document is opened, as a
located `MermaidSyntaxException`: a grammar or lexical error (with Mermaid's expected tokens), what Mermaid's database
fails on (an annotation of a class that does not exist, a method text that Mermaid's member expression cannot read),
and the limits of the open call (`MaxNestingDepth` and `MaxLabelLength` for every text the parser reads; `MaxEdges`
for the relations, `MaxNodes` for the classes, notes, interfaces, and namespaces together, `MaxLabelLength` for
the texts the model joins, the CSS classes and styles of a class and the styles of a style class included, and
`MaxNestingDepth` for the parts of a qualified namespace id, nested keywords and dots in a name together). A render of
an opened class diagram only reports what it does not draw (a look other than classic, a layout other than dagre) and
then draws; the one limit a render checks is the nesting that class names make (a class named like a namespace nests
that namespace in its own), which depends on the namespace mode a render may set, refused as a located
`MermaidRenderException` when it passes `MaxNestingDepth`.

**Deterministic and bounded.** The parser and the model hold no shared state; a parsed model is never changed again,
so it is safe for concurrent renders. The lexer replaces every regular expression of Mermaid with matchers that cannot
backtrack and caches the far-looking direction rule per line, and the parser reads names, statements, members, and
styles in loops, so the cost of reading a text is linear in its length and only nested namespaces recurse; the model is
bounded by the limits above, and the drawing by the layout work meter and the image limits of the shared rendering.

**Known differences from Mermaid** are listed in each unit design and in _CanvasNetMermaid System Design_: errors and
limits are located CanvasNet exceptions with hints; the link, tooltip, and callback statements are kept only as data,
never drawn or bound, and links are checked as the strict security level checks them (the only level modeled); the
accessibility texts are metadata only; the style warnings of a class are located at its first mention; KaTeX math in
class texts is not detected; Mermaid's SVG fix-up of a class text that contains `&gt` is not ported; and other looks
and layouts are reported and drawn in the classic look with dagre.
