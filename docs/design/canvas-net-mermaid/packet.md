## Packet

![MermaidPacket Structure](MermaidPacketView.svg)

### Overview

The `Packet` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Packet`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Packet/`) reads the text of a packet diagram (the `packet` and `packet-beta`
keywords, Mermaid's diagram id `packet`) into the state of Mermaid's packet database, splits its blocks into rows as
Mermaid's packet parser does, and draws the diagram as Mermaid's packet renderer draws it. A packet diagram is not a
graph: it is not laid out by the shared layered layout or drawn by the shared unified renderer, but placed by the fixed
geometry of Mermaid's packet renderer, so this subsystem draws its scene itself, with the text metrics of the shared
`Rendering` subsystem and the packet style sheet of the `Configuration` subsystem. It is **internal**: its types are
visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the test assembly. It contains
two units:

- **PacketParser** - the internal sealed `PacketParser` (a partial class in `PacketParser.cs` and
  `PacketParser.Lexer.cs`), the token `PacketToken` (in `PacketParser.Lexer.cs`), and the block as written
  `ParsedBlock`, the block `PacketBlock`, and the diagram `PacketDiagram` with its row split (in `PacketParser.cs`): the
  lexer and the grammar of Mermaid's packet language, the value converters, the checks of the packet database, the
  title and accessibility texts, the metadata of the diagram, the split into rows, the located errors with Mermaid's
  expected tokens, and the node and label limits; see _PacketParser Unit Design_ (`packet/packet-parser.md`)
- **PacketRenderer** - the internal sealed `PacketRenderer` (in `PacketRenderer.cs`): the drawing of Mermaid's packet
  renderer (the rectangles, labels, and bit numbers of each row, the title row, and the view box) with the texts as the
  browser measures and shows them; see _PacketRenderer Unit Design_ (`packet/packet-renderer.md`)

The subsystem's namespace documentation is in `Packet/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Packet-*` requirements in `docs/reqstream/canvas-net-mermaid/packet.yaml`. The SysML2 model names
the subsystem `MermaidPacket` (see _CanvasNetMermaid System Design_).

**Attribution.** The subsystem is a port of the packet diagram of the pinned Mermaid release (MIT; see
`third-party/README.md`): the parser ports the Langium grammar of `@mermaid-js/parser` (`src/language/packet/packet.langium`
with `src/language/common/common.langium`), its token builder (`packet/tokenBuilder.ts`, the suffix of the `packet`
keyword) and common value converter, Langium's `convertString`, `convertInt`, and the order of its token builder, and
Mermaid's packet database (`diagrams/packet/db.ts` with the `populate` and `getNextFittingBlock` of
`diagrams/packet/parser.ts`, `populateCommonDb.ts`, and the title and accessibility setters of `commonDb.ts`); the
renderer ports the `draw` and `drawWord` of `diagrams/packet/renderer.ts`. The first-match lexing and the error
positions of chevrotain, which Langium uses, are reproduced from their documented behavior, not ported. The packet style
sheet of `diagrams/packet/styles.ts` and the packet defaults of Mermaid's configuration schema are ported by the
Configuration subsystem. Every ported file carries the provenance header comment.

### Interfaces

**Exposed** (internal only): `PacketParser.Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`, which returns
the filled `PacketDiagram` or throws a located `MermaidSyntaxException`; and the `PacketDiagram` as the
`IMermaidDiagram` of the document: the metadata (`Title`, the sanitized title of the `title` statement or null;
`AccTitle`; `Direction`, always null; `NodeCount` for the block statements; and `EdgeCount` and `SubgraphCount`, always
0) and `Layout(DiagramRenderContext)`, which creates a `PacketRenderer` and returns the drawn `Scene`. For tests and for
the vectors the diagram also exposes its state (`Blocks`, `DiagramTitle`, `AccessibleTitle`, `AccessibleDescription`,
`FrontmatterTitle`, `DrawnTitle`, and `KeywordLocation`) and its row split `Rows(double, MermaidLimits,
LayoutWorkMeter)`, the parser its converters (`ConvertString`, `ConvertSingleLine`, and `ConvertMultiLine`), and the
renderer `DisplayText`.

**Consumed**: from the `Syntax` subsystem the preprocessed text and its map to the original text (`PreprocessedSource`,
`MappedText`, and the frontmatter title of `FrontmatterData`), the detected diagram (`DetectedDiagram`), JavaScript's
`trim` (`Preprocessor.TrimJs`), the JavaScript character classes of `ScanPatterns`, the text sanitizer and reference
decoder (`HtmlSanitizer`), the description of unmatched text (`SourceScanner.Describe`), the list of alternatives of a
message (`TokenCursor.JoinAlternatives`), and JavaScript's `String(number)` (`YamlSubsetParser.FormatJsNumber`); from
`Configuration` the effective `MermaidConfig` with its `PacketConfig` section and the packet style sheet `PacketStyles`
(with `ShapePaint` and `TextPaint`); from `Rendering` the text metrics and text utilities of `LabelBuilder`; from
`Dagre` the `LayoutWorkMeter`; from `Scenegraph` the `Scene` and its items (`SceneGroup`, `ScenePath`, `SceneSegment`,
`SceneText`, `SceneTextRun`, `SceneFont`, `SceneRect`, and `SceneStroke`); and from the system the `IMermaidDiagram`
contract, `DiagramRenderContext`, and the public `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`,
`MermaidRenderException`, and `MermaidDiagnosticCodes`.

**Consumers**: the document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls the
parser for a detected `packet` diagram with the limits of the open call, keeps the diagram as the document's diagram,
reads its metadata, and calls `Layout` for every measure and render.

### Design

**Parse once, split and draw per render.** Mermaid's packet parser validates the blocks and splits them into rows in
one pass (`populate`), reading `packet.bitsPerRow` from the configuration, and its renderer draws the rows. In
CanvasNet the configuration belongs to a render (the site configuration and the theme of each render can change it), so
the subsystem splits the work: `PacketParser` reads the whole text into an immutable `PacketDiagram` when the document
is opened, filling in and checking every block as `populate` does, and every render creates a new `PacketRenderer` that
splits the blocks into rows of the render's `bitsPerRow` (`PacketDiagram.Rows`, the rest of `populate` with
`getNextFittingBlock`) and draws them. The diagram holds only what Mermaid's database holds before the split (the blocks
in order, the title, and the accessibility texts), and the vectors compare the diagram and its rows with Mermaid's
database fact by fact.

**The language of the Langium grammar.** The lexer tries the token types at each position in the order Langium gives
them to chevrotain (the keywords, then the terminals in grammar order, then the hidden white space, YAML, directives,
and comments), the first that matches winning, with hand-written matchers that match what Langium's regular
expressions match. The grammar is LL(1) and is read with one token of lookahead. The values are converted as the
common value converter of `@mermaid-js/parser` and Langium's `convertString` and `convertInt` convert them, and the
database refuses, in Mermaid's order, a block whose end is before its start, a block that does not start at the bit
after the block before it, and a block of zero bits, with Mermaid's messages.

**Rows in JavaScript arithmetic.** The split is a port of Mermaid's loop in doubles: a block that passes the end of its
row is cut there, its parts keeping its label and Mermaid's bit counts (one less than their bits, which only the
vectors see); a row is complete when a part ends at its last bit; and the split stops at Mermaid's 10,000 rows. A
fractional, zero, or negative `bitsPerRow` is split exactly as Mermaid splits it; where Mermaid's
`getNextFittingBlock` then fails ("Block start ... is greater than block end ..."), the render fails with a located
`mermaid-render-failed`.

**Metadata from the text alone.** The counts of the document describe the diagram as written, whatever row size a
render sets: the block statements as nodes, and no edges or subgraphs. A packet diagram has no direction. The title is
the title of the `title` statement; when the diagram has none, the document falls back to the frontmatter title, and
the renderer draws the frontmatter title too, as Mermaid does.

**Drawing Mermaid's geometry.** `PacketRenderer.Draw` follows Mermaid's `draw` and `drawWord`: each row the row height
plus the vertical padding (grown by 10 for the bit numbers) below the one before it; each part a rectangle at its first
bit within the row, as wide as its bits less the horizontal padding, with its label centered on it and its first and
last bit numbers 2 pixels above its corners (one centered number for a single bit); the title centered in a row of its
own below the rows; and the view box of the bits of a row plus 2 by the rows and the title row. The texts are measured
and drawn as the browser shows them, on the baseline that `dominant-baseline` gives, with the boxes of the browser's
`getBBox` from `LabelBuilder`. The paint comes from `PacketStyles`, the packet style sheet of the theme and of
`themeCSS`. The scene items carry roles and ids (`packet`, `packet-row`, `packet-block`, `packet-label`,
`packet-bit-start`, `packet-bit-end`, and `title`, with the block label as the id) for tests and the reference
comparison.

**Fidelity is proven against Mermaid itself.** The upstream vectors (`packet-parser.json`, 162 cases recorded from the
pinned Mermaid release in a browser, 60 of them errors, 23 from Mermaid's own packet tests) give, for each valid text,
the facts of Mermaid's packet database, its effective configuration, and its rows, and for each invalid text the first
error Mermaid reports, with its place and, for a grammar error, the tokens its parser expects; the unit tests compare the
parser with all of them. The 13 original fixtures in `Fixtures/packet/` cover the language, the settings, and the themes
as authors use them; the subsystem tests open and draw all of them, and the system-level reference layout tests
compare their drawings with the goldens drawn by Mermaid.

**Fail closed.** Everything that can be wrong with the text is found when the document is opened, as a located
`MermaidSyntaxException`: a lexical error (at the first character no token matches, before any grammar error, as
Mermaid lists the errors of its lexer first), a grammar error (with Mermaid's expected tokens), a block the database
refuses (with Mermaid's message), and the limits of the open call (`MaxNodes` for the block statements and
`MaxLabelLength` for every text the parser keeps, converts, or sanitizes, and for the frontmatter title it may draw).
A render fails only where Mermaid's split fails, on the limits of the render (`MaxNodes` for the parts of the rows and
the layout work limit), on cancellation, or, in strict mode, on a `themeCSS` rule that the packet style sheet does not
support.

**Deterministic and bounded.** The parser and the diagram hold no shared state; a parsed diagram is never changed
again, so it is safe for concurrent renders, and a renderer draws one render on one thread. The lexer's matchers cannot
backtrack beyond the digits of a number and the grammar is read in loops, so reading a text is linear in its length
and never recursive. The split checks `MaxNodes` as each part is made, so a range that Mermaid would split into
thousands of rows fails after at most `MaxNodes` parts, and the layout work meter of the render (`MaxLayoutWork`) is
charged for each block, each part, and the length of each measured text and checks cancellation.

**Known differences from Mermaid** are listed in each unit design and in _CanvasNetMermaid System Design_: errors and
limits are located CanvasNet exceptions with hints, and only the first error is reported; every block is checked when
the text is read, before the rows are split; the parts of the rows are bounded by `MaxNodes`; the accessibility texts
are metadata only; and the texts of a packet diagram give no report of a missing glyph.
