## GitGraph

![MermaidGitGraph Structure](MermaidGitGraphView.svg)

### Overview

The `GitGraph` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.GitGraph`, folder
`src/DemaConsulting.CanvasNet.Mermaid/GitGraph/`) reads the text of a git graph (the `gitGraph` keyword, Mermaid's
diagram id `gitGraph`) into its statements, runs them against a port of Mermaid's git graph database, and draws the
diagram as Mermaid's git graph renderer draws it. A git graph is not laid out by the shared layered layout or drawn by
the shared unified renderer: Mermaid places its branches and commits with the fixed geometry of its git graph
renderer, so this subsystem draws its scene itself, with the text metrics of the shared `Rendering` subsystem and the
git graph style sheet of the `Configuration` subsystem. It is **internal**: its types are visible only to the other
subsystems of the system and, through `InternalsVisibleTo`, to the test assembly. It contains two units:

- **GitGraphParser** - the internal sealed `GitGraphParser` (a partial class in `GitGraphParser.cs` and
  `GitGraphParser.Lexer.cs`), the token `GitGraphToken` (in `GitGraphParser.Lexer.cs`), the statement kinds
  `GitStatementKind`, the statement as written `GitStatement`, and the diagram `GitGraphDiagram` (in
  `GitGraphParser.cs`), and the database `GitGraphDatabase` with its records `GitCommit` and `GitBranch` and the
  exception factory `GitGraphFailure` (in `GitGraphParser.Database.cs`): the lexer and the grammar of Mermaid's git
  graph language, the value converters, the sanitizing, the operations of Mermaid's git graph database with their
  errors, the generated commit ids, the title and accessibility texts, the metadata of the diagram, the located errors
  with Mermaid's expected tokens or messages, and the node and label limits; see _GitGraphParser Unit Design_
  (`git-graph/git-graph-parser.md`)
- **GitGraphRenderer** - the internal sealed `GitGraphRenderer` (a partial class in `GitGraphRenderer.cs`,
  `GitGraphRenderer.Arrows.cs`, and `GitGraphRenderer.Labels.cs`): the drawing of Mermaid's git graph renderer (the
  branch positions, the commit places of both passes, the branch lines and labels, the arrows with their rerouting
  lanes, the commit bullets, the commit labels and tags with their rotations, the title, and the view box of the
  browser's bounding box) with the texts as the browser measures and shows them; see _GitGraphRenderer Unit Design_
  (`git-graph/git-graph-renderer.md`)

The subsystem's namespace documentation is in `GitGraph/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-GitGraph-*` requirements in `docs/reqstream/canvas-net-mermaid/git-graph.yaml`. The SysML2 model
names the subsystem `MermaidGitGraph` (see _CanvasNetMermaid System Design_).

**Attribution.** The subsystem is a port of the git graph of the pinned Mermaid release (MIT; see
`third-party/README.md`): the parser ports the Langium grammar of `@mermaid-js/parser`
(`src/language/gitGraph/gitGraph.langium` with `src/language/common/common.langium`), its token builder (the suffix of
the `gitGraph` keyword) and common value converter, and Langium's token order with its longer alternatives,
`convertString`, and integer conversion; the database ports Mermaid's `diagrams/git/gitGraphAst.ts` with the
`populate` of `diagrams/git/gitGraphParser.ts`, the `sanitizeText` of `diagrams/common/common.ts` as the git graph
configuration runs it, the `random` of `utils.ts` for the generated ids, `populateCommonDb.ts`, and the title and
accessibility setters of `commonDb.ts`; the renderer ports `diagrams/git/gitGraphRenderer.ts`, the `insertTitle` of
`utils.ts`, and Mermaid's shared setup of the graph's view box. The first-match lexing, the longer-alternative rule, and
the error positions of chevrotain, and the ALL(*) prediction of the header that Langium's parser makes are reproduced
from their documented and observed behavior, not ported. The git graph style sheet of `diagrams/git/styles.js` and the
git graph defaults of Mermaid's configuration schema are ported by the Configuration subsystem. Every ported file
carries the provenance header comment.

### Interfaces

**Exposed** (internal only): `GitGraphParser.Parse(PreprocessedSource, DetectedDiagram, MermaidLimits, MermaidConfig)`,
whose last argument is the configuration of the text alone (its frontmatter and directives), and which returns the
`GitGraphDiagram` or throws a located `MermaidSyntaxException`; and the `GitGraphDiagram` as the `IMermaidDiagram` of
the document: the metadata (`Title`, the sanitized title of the `title` statement or null; `AccTitle`; `Direction`,
`LR`, `TB`, or `BT`; `NodeCount` for the commits; `EdgeCount` for the links to their parents; and `SubgraphCount`,
always 0) and `Layout(DiagramRenderContext)`, which creates a `GitGraphRenderer` and returns the drawn `Scene`. For
tests and for the vectors the diagram also exposes its state (`Statements`, `OpenDatabase`, `DiagramTitle`,
`IsTitleSet`, `AccessibleTitle`, `AccessibleDescription`, `FrontmatterTitle`, `DrawnTitle`, and `KeywordLocation`) and
the database of a render's configuration `Database(GitGraphConfig)`, the database its commits, branches, branch order,
current branch, and head, the parser its converters (`ConvertString`, `ConvertSingleLine`, and `ConvertMultiLine`), and
the renderer its text helpers (`Decode`, `Rows`, and `Box32`).

**Consumed**: from the `Syntax` subsystem the preprocessed text and its map to the original text (`PreprocessedSource`,
`MappedText`, and the frontmatter title of `FrontmatterData`), the detected diagram (`DetectedDiagram`), JavaScript's
`trim` (`Preprocessor.TrimJs`), the JavaScript character classes of `ScanPatterns`, the text sanitizer and reference
decoder (`HtmlSanitizer`), the description of unmatched text (`SourceScanner.Describe`), and the list of alternatives of
a message (`TokenCursor.JoinAlternatives`); from `Configuration` the effective `MermaidConfig` with its `GitGraphConfig`
section and the git graph style sheet `GitGraphStyles` (with `GitGraphElement` and `GitGraphComputedStyle`); from
`Rendering` the text metrics and text utilities of `LabelBuilder`; from `Dagre` the `LayoutWorkMeter`; from
`Scenegraph` the `Scene` and its items (`SceneGroup`, `ScenePath`, `SceneSegment`, `SceneText` with its
`SceneRotation`, `SceneTextRun`, `SceneFont`, `SceneRect`, `SceneStroke`, and `SceneShadow`); and from the system the
`IMermaidDiagram` contract, `DiagramRenderContext`, and the public `MermaidLimits`, `MermaidLocation`,
`MermaidSyntaxException`, `MermaidRenderException`, and `MermaidDiagnosticCodes`.

**Consumers**: the document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls the
parser for a detected `gitGraph` diagram with the limits of the open call and the configuration of the text, keeps the
diagram as the document's diagram, reads its metadata, and calls `Layout` for every measure and render.

### Design

**Run the statements once, again only for another main branch.** Mermaid's git graph database runs the statements with
the configuration of the render, which names the main branch and its order. In CanvasNet the configuration belongs to a
render (the site configuration of each render can change it), while the metadata and the errors of the text belong to
the open call, so the subsystem splits the work: `GitGraphParser` reads the whole text into its statements and runs them
against a `GitGraphDatabase` with the main branch name and order of the text's own configuration when the document is
opened, and the immutable `GitGraphDiagram` keeps both. Every render takes the database of its configuration from
`GitGraphDiagram.Database`: the database of the open call when the main branch name and order are the same, else the
statements run again against a new database, where an operation the database rejects is a located
`mermaid-render-failed` with Mermaid's message. The vectors compare the database with Mermaid's fact by fact, for the
text alone and for site configurations that rename or reorder the main branch.

**The language of the Langium grammar.** The lexer tries the keywords (longest first) and then the terminals at each
position in the order Langium gives them to chevrotain, the first that matches winning, with hand-written matchers that
match what Langium's regular expressions match; a keyword gives way to a branch name (`REFERENCE`) that matches strictly
more characters, as chevrotain's longer alternative does. The grammar is read with one token of lookahead, and the
header's alternatives give the outcomes of the parser's prediction (a direction without its colon ends the diagram). The
values are converted as the common value converter of `@mermaid-js/parser` and Langium's conversions convert them; a
repeated option keeps its last value, and tags add up.

**The database of Mermaid.** `GitGraphDatabase` ports Mermaid's `commit`, `branch`, `checkout` (and `switch`), `merge`,
and `cherryPick`: the texts sanitized as Mermaid's `sanitizeText` sanitizes them under the git graph configuration (the
tags of a merge kept as written), the checks of each operation in Mermaid's order with Mermaid's messages, the messages
and default tags Mermaid composes for merges and cherry-picks, the commits in the order of Mermaid's commit map (a
commit whose id is already used replacing the earlier one in its place), and the branches sorted for drawing by their
order, stably, as JavaScript's sort sorts them. A commit without an id gets an id of Mermaid's form, its sequence number,
a dash, and seven hex digits; the digits come from the mulberry32 sequence with a fixed seed, restarted for every run,
in place of Mermaid's `Math.random`, so the ids are the same in every render and equal to those of the seeded reference.

**Metadata from the text alone.** The counts of the document describe the diagram of the text's own configuration,
whatever main branch a render's site configuration names: the commits as nodes, the links to their parents as edges,
and no subgraphs. The direction is the header's (`LR` when it gives none). The title is the title of the `title`
statement; when the diagram has none, the document falls back to the frontmatter title, and when the diagram has no
title statement with a value, the renderer draws the frontmatter title too, as Mermaid does.

**Drawing Mermaid's geometry.** `GitGraphRenderer.Draw` follows Mermaid's `draw`: the branch positions from the widths
of their labels; a first pass over the commits that only places them; the branch lines and labels; the arrows, from the
places of the first pass, with Mermaid's rerouting test and lane search; a second pass that places the commits again and
draws their bullets, labels, and tags; the title, centered over everything drawn before it; and the view box, the
browser's bounding box of the drawing grown by `diagramPadding`. The two passes share their state, as Mermaid's module
state is shared, so a commit whose id is used again is drawn where Mermaid draws it. The texts are measured as the
browser's `getBBox` measures them while Mermaid draws (with Mermaid's entity placeholders) and drawn as the browser
shows them. A rotated commit label or tag is a `SceneText` with a `SceneRotation`, and its rotated rectangle, polygon,
and hole are outlines already turned. The paint comes from `GitGraphStyles`, the git graph style sheet of the theme and
of `themeCSS`, through its class-set cascade (the commit id is part of the class attribute, as in Mermaid). The scene
items carry roles and ids (`git-graph`, `git-branches`, `git-branch`, `git-branch-label`, `git-arrows`, `git-arrow`,
`git-commits`, `git-commit`, `git-labels`, `git-commit-label`, `git-tag`, and `title`) for tests and the reference
comparison.

**Fidelity is proven against Mermaid itself.** The upstream vectors (`gitgraph-parser.json`, 297 cases recorded from the
pinned Mermaid release in a browser whose `Math.random` is the seeded sequence, 101 of them errors, 92 from Mermaid's
own git graph tests and the parser package's tests) give, for each valid text, the facts of Mermaid's git graph database
(its commits with their generated ids, its branches, the branch order, the current branch, and the head) and its
effective configuration, and for each invalid text the first error Mermaid reports, with its place and, for a grammar
error, the tokens its parser expects; the unit tests compare the parser and the database with all of them. The 18
original fixtures in `Fixtures/gitgraph/` cover the language, the three directions, the settings, and the themes as
authors use them; the subsystem tests open and draw all of them, and the system-level reference layout tests compare
their drawings with the goldens drawn by Mermaid.

**Fail closed.** Everything that can be wrong with the text is found when the document is opened, as a located
`MermaidSyntaxException`: a lexical error (at the first character no token matches, before any grammar error, as
Mermaid lists the errors of its lexer first), a grammar error (with Mermaid's expected tokens), an operation the database
rejects (with Mermaid's message), and the limits of the open call (`MaxNodes` for the main branch, the branches, and
the commits, and `MaxLabelLength` for every text the parser keeps, converts, sanitizes, or composes, and for the
frontmatter title it may draw). A render fails only where the database rejects an operation under the render's main
branch, where Mermaid's renderer fails (`Closest parent not found for commit ...`), on the limits of the render (the
limits of a database run again and the layout work limit), on cancellation, or, in strict mode, on a `themeCSS` rule
that the git graph style sheet does not support.

**Deterministic and bounded.** The parser, the database, and the diagram hold no shared state; a parsed diagram and its
databases are never changed again, so they are safe for concurrent renders, and a renderer draws one render on one
thread. The lexer's matchers look at most to the end of their line, quote, brace, YAML block, or directive, and the
grammar is read in loops, so reading a text is linear in its length and never recursive; the database is linear in the
statements, with hash lookups. The layout work meter of the render (`MaxLayoutWork`) is charged for each branch, each
commit of each pass and each parent looked at, each arrow and the commits its rerouting test scans, each lane try and
the lanes it compares, and the length of each measured text, and checks cancellation.

**Known differences from Mermaid** are listed in each unit design and in _CanvasNetMermaid System Design_: errors and
limits are located CanvasNet exceptions with hints, and only the first error is reported; the generated commit ids come
from a fixed sequence; the statements run when the text is opened with the text's own main branch, so a text that relies
on a main branch that only the site configuration names fails when it is opened; a commit whose id is used again
replaces the earlier commit silently; the accessibility texts are metadata only; and the texts of a git graph give no
report of a missing glyph.
