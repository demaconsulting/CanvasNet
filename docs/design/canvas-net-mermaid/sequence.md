## Sequence

![Sequence Structure](MermaidSequenceView.svg)

### Overview

The `Sequence` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Sequence`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Sequence/`) reads the text of a sequence diagram (the `sequenceDiagram`
keyword) into Mermaid's sequence database and draws it as Mermaid's sequence renderer does. Unlike the flowchart, a
sequence diagram does not use the shared layered layout: Mermaid lays it out with its own renderer, which places the
participants from left to right and every message, note, and block from top to bottom, and the subsystem ports that
renderer and its drawing functions. It is **internal**: its types are visible only to the other subsystems of the
system and, through `InternalsVisibleTo`, to the test assembly. It contains four units:

- **SequenceParser** - the internal sealed `SequenceParser` (a partial class in `SequenceParser.cs`,
  `SequenceParser.Lexer.cs`, and `SequenceParser.Statements.cs`) and the token names of `SequenceTokens` (in
  `SequenceParser.Lexer.cs`): the lexer and the grammar of Mermaid's sequence language, the statements it collects
  for the database, the located errors with Mermaid's expected tokens, and the nesting and label limits; see
  _SequenceParser Unit Design_ (`sequence/sequence-parser.md`)
- **SequenceModel** - the internal sealed `SequenceModel` (a partial class in `SequenceModel.cs` and
  `SequenceModel.Render.cs`), its elements and constants in `SequenceModel.Elements.cs`, and the statement records in
  `SequenceModel.Statements.cs`: Mermaid's sequence database, the diagram metadata, and the reports of what is not
  drawn; see _SequenceModel Unit Design_ (`sequence/sequence-model.md`)
- **SequenceLayout** - the internal sealed `SequenceLayout` (a partial class in `SequenceLayout.cs`,
  `SequenceLayout.Bounds.cs`, `SequenceLayout.Messages.cs`, and `SequenceLayout.Blocks.cs`) and its layout state types
  in `SequenceLayout.Bounds.cs`: Mermaid's sequence renderer for one render, from the sizes of the participants to the
  view box; see _SequenceLayout Unit Design_ (`sequence/sequence-layout.md`)
- **SequenceRenderer** - the internal sealed `SequenceRenderer` (a partial class in `SequenceRenderer.cs`,
  `SequenceRenderer.Actors.cs`, `SequenceRenderer.Messages.cs`, `SequenceRenderer.Blocks.cs`, and
  `SequenceRenderer.Text.cs`) with `SceneSlot` and `SequenceTextData`: Mermaid's sequence drawing functions, which
  turn what the layout places into scene items in Mermaid's paint order; see _SequenceRenderer Unit Design_
  (`sequence/sequence-renderer.md`)

The subsystem's namespace documentation is in `Sequence/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Sequence-*` requirements in `docs/reqstream/canvas-net-mermaid/sequence.yaml`. The SysML2 model
names the subsystem `MermaidSequence` (see _CanvasNetMermaid System Design_).

**Attribution.** The subsystem is a port of the sequence diagram of the pinned Mermaid release (MIT; see
`third-party/README.md`): the parser ports the generated lexer and the grammar of
`diagrams/sequence/parser/sequenceDiagram.jison`; the model ports the database of `diagrams/sequence/sequenceDb.ts`
(with the title and accessibility texts of `diagrams/common/commonDb.ts`); the layout ports
`diagrams/sequence/sequenceRenderer.ts`; and the renderer ports `diagrams/sequence/svgDraw.js`, the message drawing of
`sequenceRenderer.ts`, and `diagrams/common/svgDrawCommon.ts`. Every ported file carries the provenance header
comment.

### Interfaces

**Exposed** (internal only): `SequenceParser.Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`, which
returns the filled `SequenceModel` or throws a located `MermaidSyntaxException`; and the `SequenceModel` as the
`IMermaidDiagram` of the document: the metadata (`Title`, `AccTitle`, `Direction`, which is null, `NodeCount` for the
participants, `EdgeCount` for the messages drawn as arrows, and `SubgraphCount` for the boxes) and
`Layout(DiagramRenderContext)`, which returns the laid-out `Scene`. For tests and for the layout the model also
exposes its elements (`Actors`, `Messages`, `Notes`, `Boxes`, `CreatedActors`, `DestroyedActors`, `DiagramTitle`,
`AccessibleTitle`, and `AccessibleDescription`).

**Consumed**: from the `Syntax` subsystem the preprocessed text and its map to the original text (`PreprocessedSource`,
`MappedText`), the detected diagram (`DetectedDiagram`), the scanner and token cursor (`SourceScanner`, `ScanRule`,
`ScanToken`, `TokenCursor`), the YAML subset reader of the participant metadata (`YamlSubsetParser`), the text
sanitizer (`HtmlSanitizer`), and the edit distance of the nearest-name hints (`DiagramDetector.EditDistance`); from
`Configuration` the effective `MermaidConfig` with its `SequenceConfig` section and the style sheet `SequenceStyles`;
from `Rendering` the text utilities of `LabelBuilder` (`CalculateTextDimensions`, `WrapLabel`, `SplitBreaks`, and the
SVG text boxes); from `Dagre` the `LayoutWorkMeter`; from `Scenegraph` the scene types, including the drop shadow of
a path and the stroke of a text; and from the system the public `MermaidLimits`, `MermaidLocation`,
`MermaidSyntaxException`, `MermaidRenderException`, `MermaidDiagnosticCodes`, and `MermaidReportSink`.

**Consumers**: the document pipeline of the system (`MermaidDocument.Pipeline.cs`) calls the parser for a detected
sequence diagram, keeps the model as the document's diagram, reads its metadata, and calls `Layout` for every measure
and render.

### Design

**Collect, then apply, as in Mermaid.** Mermaid's sequence grammar builds a list of statements and hands the whole
list to its database (`yy.apply`) once the text has parsed; only the title and accessibility statements are set while
parsing. The parser keeps that split: it collects `SequenceStatement` records in Mermaid's order and the model applies
them afterwards, so a grammar error anywhere in the text is reported before a database error earlier in it, and the
database rules (participant order, replaced declarations, created and destroyed participants, the activation check)
see the statements in the same order as Mermaid's database.

**Layout and drawing feed each other.** Mermaid's renderer draws each element as it places it and reads back the
heights that the browser measures (`getBBox`). The subsystem keeps that loop: `SequenceLayout` walks the messages with
Mermaid's `bounds` object and calls the drawing methods of `SequenceRenderer`, which return the heights Mermaid would
measure; text sizes come from the text utilities of `LabelBuilder`, which emulate the browser's SVG text boxes with
the bundled fonts. The paint comes from `SequenceStyles`, which resolves the cascade of the theme's sequence style
sheet over the attributes that Mermaid writes, so the CSS facts that change pixels (a lifeline widened to 2 pixels by
the `line` rule, the node border and drop shadow of a box) are Mermaid's.

**Fidelity is proven against Mermaid itself.** The upstream vectors (`sequence-parser.json`, 242 cases recorded from
the pinned Mermaid release in a browser, 35 of them errors) give, for each valid text, Mermaid's database facts, and
for each invalid text the error Mermaid raises with the expected tokens of its parser or the message of its database;
the unit tests compare the parser and the model with all of them. The 24 original fixtures in `Fixtures/sequence/`
cover the language as authors use it; the subsystem tests open and lay out all of them, and the system-level
reference layout tests compare their drawings with mermaid-cli renders.

**Fail closed when opened.** Everything that can be wrong with the text is found when the document is opened, as a
located `MermaidSyntaxException`: a grammar or lexical error (with Mermaid's expected tokens), what Mermaid's database
fails on (a participant in two boxes, a misused `create` or `destroy`, the deactivation of a participant that is not
active, invalid participant metadata), and the limits of the open call (`MaxNestingDepth` and `MaxLabelLength` in the
parser; `MaxNodes` in the model). A render of an opened sequence diagram reports what it does not draw as Mermaid does
(look, participant icons, math, unknown participant types, the `fo` text placement) and then draws; the few texts
that Mermaid draws at coordinates that are not numbers or crashes on fail the render with a located
`mermaid-render-failed`.

**Deterministic and bounded.** The parser, the model, the layout, and the renderer hold no shared state; a parsed
model is never changed again, so it is safe for concurrent renders, and each render has its own layout and renderer.
The lexer replaces every regular expression of Mermaid with matchers that cannot backtrack, so reading a text costs
time linear in its length; the model is bounded by the limits above, and the layout charges its measuring, wrapping,
and placing to the layout work meter of the render, which also observes cancellation.

**Known differences from Mermaid** are listed in each unit design and in _CanvasNetMermaid System Design_: errors and
limits are located CanvasNet exceptions; links, menus, and the accessibility texts are kept as data and menus are not
interactive; other looks, participant image icons, and math are reported and drawn without them; the `fo` text
placement is reported and drawn as the default placement; and the text boxes of the browser are emulated with the
bundled fonts.
