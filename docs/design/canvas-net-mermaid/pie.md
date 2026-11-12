## Pie

![MermaidPie Structure](MermaidPieView.svg)

### Overview

The `Pie` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Pie`, folder `src/DemaConsulting.CanvasNet.Mermaid/Pie/`)
reads the text of a pie chart (the `pie` keyword, Mermaid's diagram id `pie`) into the state of Mermaid's pie database
and draws the chart as Mermaid's pie renderer draws it. Unlike the flowchart, class, state, and entity relationship
diagrams, a pie chart is not a graph: it is not laid out by the shared layered layout or drawn by the shared unified
renderer, but placed by the fixed geometry of Mermaid's pie renderer, so this subsystem draws its scene itself, with the
text metrics of the shared `Rendering` subsystem and the pie style sheet of the `Configuration` subsystem. It is
**internal**: its types are visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the
test assembly. It contains two units:

- **PieParser** - the internal sealed `PieParser` (a partial class in `PieParser.cs` and `PieParser.Lexer.cs`), the
  token `PieToken` (in `PieParser.Lexer.cs`), and the section `PieSection` and the diagram `PieDiagram` (in
  `PieParser.cs`): the lexer and the grammar of Mermaid's pie language, the value converters, the pie database with its
  title and accessibility texts, the metadata of the diagram, the located errors with Mermaid's expected tokens, and the
  node and label limits; see _PieParser Unit Design_ (`pie/pie-parser.md`)
- **PieRenderer** - the internal sealed `PieRenderer` (a partial class in `PieRenderer.cs` and `PieRenderer.Arcs.cs`)
  and the arc `PieArc` (in `PieRenderer.Arcs.cs`): the drawing of Mermaid's pie renderer (the outer circle, the slices,
  the percentage labels, the title, the legend in its position, the donut hole, the highlighted slice, and the view box)
  with the ports of the d3 functions it uses; see _PieRenderer Unit Design_ (`pie/pie-renderer.md`)

The subsystem's namespace documentation is in `Pie/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Pie-*` requirements in `docs/reqstream/canvas-net-mermaid/pie.yaml`. The SysML2 model names the
subsystem `MermaidPie` (see _CanvasNetMermaid System Design_).

**Attribution.** The subsystem is a port of the pie chart of the pinned Mermaid release (MIT; see
`third-party/README.md`): the parser ports the Langium grammar of `@mermaid-js/parser` (`src/language/pie/pie.langium`
with `src/language/common/common.langium`), its token builder (`tokenBuilder.ts`, the suffix of the keywords) and value
converters (`valueConverter.ts` with the expressions of `matcher.ts`), Langium's `convertString` and the order of its
token builder, and Mermaid's pie database (`diagrams/pie/pieDb.ts` with `pieParser.ts`, `populateCommonDb.ts`, and the
title and accessibility setters of `commonDb.ts`); the renderer ports the `draw` of `diagrams/pie/pieRenderer.ts` and,
from d3, `pie()` and `arc()` with its `centroid` (d3-shape), the arc of d3-path's `Path`, and `scaleOrdinal`
(d3-scale). The first-match lexing and the error positions of chevrotain, which Langium uses, are reproduced from their
documented behavior, not ported. The pie style sheet of `diagrams/pie/pieStyles.ts` and the pie defaults of Mermaid's
configuration schema are ported by the Configuration subsystem. Every ported file carries the provenance header comment.

### Interfaces

**Exposed** (internal only): `PieParser.Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`, which returns the
filled `PieDiagram` or throws a located `MermaidSyntaxException`; and the `PieDiagram` as the `IMermaidDiagram` of the
document: the metadata (`Title`, the sanitized title of the `title` statement or null; `AccTitle`; `Direction`, always
null; `NodeCount` for the sections; and `EdgeCount` and `SubgraphCount`, always 0) and `Layout(DiagramRenderContext)`,
which creates a `PieRenderer` and returns the drawn `Scene`. For tests and for the vectors the diagram also exposes its
state (`Sections`, `ShowData`, `DiagramTitle`, `IsTitleSet`, `AccessibleTitle`, `AccessibleDescription`, and
`KeywordLocation`), the
parser its converters (`ConvertString`, `ConvertSingleLine`, and `ConvertMultiLine`), and the renderer its d3 and
JavaScript ports (`Pie`, `ArcPath`, `Centroid`, `ToFixed0`) and `DisplayText`.

**Consumed**: from the `Syntax` subsystem the preprocessed text and its map to the original text (`PreprocessedSource`,
`MappedText`), the detected diagram (`DetectedDiagram`), JavaScript's `trim` (`Preprocessor.TrimJs`), the JavaScript
character classes of `ScanPatterns`, the text sanitizer and reference decoder (`HtmlSanitizer`), the description of
unmatched text (`SourceScanner.Describe`), the list of alternatives of a message (`TokenCursor.JoinAlternatives`), and
JavaScript's `String(number)` (`YamlSubsetParser.FormatJsNumber`); from `Configuration` the effective `MermaidConfig`
with its `PieConfig` section (`textPosition`, `donutHole`, `legendPosition`, and `highlightSlice`) and its theme
variables, the pie style sheet `PieStyles` (with `SlicePaint`, `ShapePaint`, and `TextPaint`), `ConfigScalar`, and
`StyleDeclarations`; from `Rendering` the text metrics and text utilities of `LabelBuilder` and the 3-digit rounding of
d3-path (`CurvePath.Round`); from `Dagre` the `LayoutWorkMeter`; from `Scenegraph` the `Scene` and its items
(`SceneGroup`, `ScenePath`, `SceneSegment`, `SceneText`, `SceneTextRun`, `SceneFont`, `SceneRect`, and
`SceneStroke`); from the core `CanvasNet` the color `Rgba32`; and from the system the `IMermaidDiagram` contract,
`DiagramRenderContext`, and the public `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`,
`MermaidRenderException`, and `MermaidDiagnosticCodes`.

**Consumers**: the document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls the
parser for a detected `pie` diagram with the limits of the open call, keeps the diagram as the document's diagram, reads
its metadata, and calls `Layout` for every measure and render.

### Design

**Parse once, draw per render, as in Mermaid.** Mermaid's pie parser fills its database from the syntax tree of the
Langium parser (`populateDb`), and its renderer reads the database and the configuration when it draws. The subsystem
keeps that split: `PieParser` reads the whole text into an immutable `PieDiagram` when the document is opened, and every
render creates a new `PieRenderer` that draws the diagram under the configuration, the theme, and the fonts of that
render. The diagram holds only what Mermaid's database holds (the sections in order, `showData`, the title, and the
accessibility texts), so the vectors compare it with Mermaid's database fact by fact.

**The language of the Langium grammar.** The lexer tries the token types at each position in the order Langium gives
them to chevrotain (the keywords, then the terminals in grammar order, then the hidden white space, YAML, directives,
and comments), the first that matches winning, with hand-written matchers that match what Langium's regular
expressions match. The grammar is LL(1) and is read with one token of lookahead. The values are converted as Mermaid's
value converters and Langium's `convertString` convert them, and the database keeps each label once (the first value
wins) and refuses a negative value, as Mermaid's `addSection` does.

**Metadata from the text alone.** The counts of the document describe the chart that is drawn: the sections (each label
once, the legend entries) as nodes, and no edges or subgraphs. A pie chart has no direction. The title is the title of
the `title` statement; when the chart has none, the document falls back to the frontmatter title, and when the chart
has no title statement with a value, the renderer draws the frontmatter title too, as Mermaid does.

**Drawing Mermaid's geometry.** `PieRenderer.Draw` follows Mermaid's `draw` step by step: the fixed chart of 450 pixels
with the pie of radius 185 centered at (225, 225); the 1% filter and d3's `pie()` for the angles; d3's `arc()` for the
outlines, with the 3 digits of d3-path; the percentage labels of `toFixed(0)` at the centroid of the label arc; the
colors of `pie1` to `pie12` by the index of each section; the title at (0, -200) from the pie's center; the legend from
the widest legend text the browser measures, in one of five positions; and the view box that takes in the chart, the
legend, and a title wider than the chart. The texts are measured as the browser measures them when Mermaid draws them
(Mermaid's entity placeholders not yet decoded) and drawn as the browser shows them, with the boxes of the browser's
`getBBox` from `LabelBuilder`. The paint comes from `PieStyles`, the pie style sheet of the theme and of `themeCSS`.
The scene items carry roles and ids (`pie`, `pie-chart`, `pie-outer-circle`, `pie-slice`, `pie-slice-label`, `title`,
`pie-legend`, `pie-legend-rect`, and `pie-legend-text`, with the section label as the id) for tests and the reference
comparison.

**Fidelity is proven against Mermaid itself.** The upstream vectors (`pie-parser.json`, 133 cases recorded from the
pinned Mermaid release in a browser, 39 of them errors) give, for each valid text, the facts of Mermaid's pie database,
and for each invalid text the first error Mermaid reports, with its place and, for a grammar error, the tokens its
parser expects; the unit tests compare the parser with all of them. The 8 original fixtures in `Fixtures/pie/` cover
the language and the options as authors use them; the subsystem tests open and draw all of them, and the system-level
reference layout tests compare their drawings with the goldens drawn by Mermaid.

**Fail closed when opened.** Everything that can be wrong with the text is found when the document is opened, as a
located `MermaidSyntaxException`: a lexical error (at the first character no token matches, before any grammar error,
as Mermaid lists the errors of its lexer first), a grammar error (with Mermaid's expected tokens), a negative value
(with Mermaid's message), and the limits of the open call (`MaxNodes` for the section statements and `MaxLabelLength`
for every text the parser keeps, converts, sanitizes, or, with `showData`, writes into the legend). A render of an
opened pie chart fails only on the limits of the render (the layout work limit among them), on cancellation, or, in
strict mode, on a `themeCSS` rule that the pie style sheet does not support.

**Deterministic and bounded.** The parser and the diagram hold no shared state; a parsed diagram is never changed
again, so it is safe for concurrent renders, and a renderer draws one render on one thread. The lexer's matchers cannot
backtrack and the grammar is read in loops, so reading a text is linear in its length and never recursive. The drawing
is bounded by the sections and texts that the limits allow, and the layout work meter of the render
(`MaxLayoutWork`) is charged for each section and for the length of each measured text and checks cancellation.

**Known differences from Mermaid** are listed in each unit design and in _CanvasNetMermaid System Design_: errors and
limits are located CanvasNet exceptions with hints, and only the first error is reported; the accessibility texts are
metadata only; the opacity of a slice is folded into its colors, and the scale of the highlighted slice is applied to
its geometry; a slice highlighted on hover is never drawn highlighted; and the texts of a pie chart give no report of a
missing glyph.
