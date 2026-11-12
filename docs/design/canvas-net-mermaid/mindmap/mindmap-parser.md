### MindmapParser

![MermaidMindmap Structure](MermaidMindmapView.svg)

The `MindmapParser` unit is the first software unit in the `Mindmap` subsystem. It comprises two source files in the
`DemaConsulting.CanvasNet.Mermaid.Mindmap` namespace: `MindmapParser.cs` (the grammar, the database calls, the node
types, the node, the edge, and the diagram with the sections, classes, and edges of Mermaid's `getData`) and
`MindmapParser.Lexer.cs` (the token names `MindmapTokens` and the 37 rules and five start conditions of the lexer).
`MindmapParser` is an `internal sealed partial` class, so the two files are one type. Both files carry the provenance
header comment for the parts of Mermaid they port.

#### Purpose

`MindmapParser` reads the text of a mindmap into a `MindmapDiagram` that holds what Mermaid's mindmap database holds
for the same text, and what its `getData` derives from it. It exists so that a mindmap means exactly what it means to
Mermaid, quirks of the lexer and of the database included, so that every invalid text is refused when the document is
opened with the place and the expected tokens Mermaid reports, and so that the size and the depth of the tree are
bounded before anything is drawn.

The unit's behavior is specified by the `CanvasNetMermaid-Mindmap-MindmapParser-*` requirements in
`docs/reqstream/canvas-net-mermaid/mindmap/mindmap-parser.yaml`.

#### Data Model

**`MindmapTokens`** holds the jison terminal names of the tokens (`SPACELINE`, `NL`, `MINDMAP`, the end of the text,
`SPACELIST`, `ICON`, `CLASS`, `NODE_DSTART`, `NODE_DESCR`, `NODE_DEND`, and `NODE_ID`) and their jison numbers, which
order an expected-token list as jison prints it; the end of the text is named `EOF` in messages.

**The lexer** is the table of the 37 rules of the generated jison lexer, in jison order, each a hand-written matcher with
the action of the rule (a token, a push, or a pop of a start condition), and the conditions table that lists, for the
initial condition and for `CLASS`, `ICON`, `NODE`, `NSTR`, and `NSTR2`, the rules it tries. The matching is
case-insensitive, as `%options case-insensitive` makes it.

**A parser instance** reads one text on one thread and holds the limits of the open call, the scanner and its token
cursor, the nodes in the order they were added, the path from the root to the last node added (where the parent of the
next node is), and the base level (the root's indentation).

**`MindmapNodeType`** is Mermaid's `nodeType`: `Default` (0), `RoundedRect` (1), `Rect` (2), `Circle` (3), `Cloud` (4),
`Bang` (5), and `Hexagon` (6).

**`MindmapNode`** is a node of Mermaid's database: `Id` (its number in the order the nodes were added), `NodeId` (the
id as written, sanitized), `Level` (the indentation less the root's; 0 for the root, positive for every other node),
`Description` (sanitized, the label), `Type`, `Location`, `Icon` and `IconLocation` (of the last `::icon(...)` after
the node, sanitized), `Class` (of the last `:::` after the node, sanitized), `Parent` (null for the root), `Depth` (the
number of ancestors), `Children` (in the order they were added), and what `getData` derives: `Section` (null for the
root), `Shape` (from `getShapeFromType`: `mindmapCircle`, `rect`, `rounded`, `cloud`, `bang`, `hexagon`, or
`defaultMindmapNode`), and `CssClasses` (`mindmap-node`, then `section-root section--1` for the root or `section-<k>`,
then the node's own class). The parser fills the decorations, the parent, and the children while it reads; a node is
never changed after parsing.

**`MindmapEdge`** is an edge of `generateEdges`: the id `edge_<parent>_<child>`, the parent and the child, and the
classes `edge section-edge-<k> edge-depth-<n>`, where `k` is the child's section and `n` the parent's level plus one.

**`MindmapDiagram`** is the parsed mindmap: the keyword location, the nodes in the order they were added (the
pre-order of the tree that `flattenNodes` walks, because a new node's parent is always on the path to the node before
it), and the edges in the order of `generateEdges` (the edge to each node but the root, in the same order). The section
count is 11 (Mermaid's `MAX_SECTIONS - 1`). Invariants: the first node, if any, is the root; every other node has a
parent added before it; there is one edge fewer than nodes, and none when there are no nodes. The diagram is never
changed after it is built and is safe for concurrent use.

#### Key Methods

- **`Parse(source, detected, limits)`** creates a parser for the limits of the open call, reads the parser text of the
  preprocessed source, and returns a `MindmapDiagram` of the nodes at the keyword's location.
- **`Run`** reads the grammar's `start`: optional blank lines (`SPACELINE`, then `SPACELINE` or `NL`), the `MINDMAP`
  keyword, an optional `NL`, then the statements, each followed by one `stop` token (`SPACELINE`, `NL`, or the end) and
  any further `NL` and end tokens, until the tokens end. After each token the lookahead is checked against the tokens the
  LALR state accepts (`CheckLookahead`), so the error comes where jison reports it.
- **`ParseStatement`** reads a blank line, a decoration, a node at indentation 0, or a `SPACELIST` followed by a
  decoration, a node at the length of the `SPACELIST` (each tab counting one), or nothing.
- **`ParseNode(indent)`** reads `NODE_ID` alone (the id is also the description) or an optional `NODE_ID`, a
  `NODE_DSTART`, a `NODE_DESCR`, and a `NODE_DEND` (a node without an id takes its description as its id), and gives the
  node the type `TypeOf` gives its brackets.
- **`TypeOf(start, end)`** is Mermaid's `getType`: `[` a rectangle, `(` a rounded rectangle when the end is `)` and a
  cloud otherwise, `((` a circle, `)` a cloud, `))` a bang, `{{` a hexagon, and anything else (`-)` and `(-`) the
  default node.
- **`AddNode(level, id, description, type, location)`** is Mermaid's `addNode`: it counts the node against `MaxNodes`;
  makes the first node the root at level 0 and sets the base level from its indentation; gives any other node the level
  of its indentation less the base level; sanitizes the id and the description and checks them against
  `MaxLabelLength`; and finds the parent as `getParent` does, the last node with a smaller level, by removing from the
  path every node whose level is not smaller. The child's depth is checked against `MaxNestingDepth`. A node that is not
  the root and finds no parent fails with Mermaid's message.
- **`ParseDecoration`** reads an `ICON` or `CLASS` token and makes `decorateNode`'s change on the last node added: the
  sanitized icon and its location, or the sanitized class. Before any node it fails, located at the decoration.
- **`MindmapDiagram(keywordLocation, nodes, limits)`** derives, in one pass over the nodes in their order, what
  `getData` derives: `assignSections` (the root none, each child of the root the next section modulo 11 by a counter of
  the root's children, every deeper node its parent's), the class list of `flattenNodes` (checked against
  `MaxLabelLength` at its node), and the edge of `generateEdges` from each node's parent.
- **`Sanitize(text)`** is Mermaid's `sanitizeText` through `HtmlSanitizer` (an empty text is kept);
  **`CheckLength`** checks a length against `MaxLabelLength`.
- **`Layout(context)`** creates a `MindmapRenderer` for the render and returns its scene.

**Lexer quirks kept on purpose**: a node id runs to the first bracket, brace, or line feed, so `%%` or `:::` after an
id are part of it; a run of white space that holds a line feed after its first character is a `SPACELINE`, so a blank
line right after a statement may not be followed by a node at the start of the next line; a quoted or markdown
description may hold brackets and runs over line feeds to its closing quote; an icon runs to its `)` over line feeds;
and a lone `{` or `}` outside a description matches no rule.

**Preconditions and postconditions**: the source is the preprocessed text of a document whose detected type is
`mindmap`; the result is an immutable diagram whose locations are in the original text, or an exception. Reading is
linear in the length of the text.

#### Error Handling

- A token the grammar does not accept is a `MermaidSyntaxException` with code `mermaid-syntax-error`, located at the
  token in the original text, whose message names the token found and the expected tokens and whose `Expected` lists the
  jison names of the tokens of the LALR state, in jison's order, with a hint from the context (close the brackets, give
  a description, or write one node per line).
- Text that no rule of the active start condition matches is a located `mermaid-syntax-error` at the first character
  not matched, with a hint about lone braces and markdown strings.
- A second root, or a node at a level no node can take as its parent, is a `mermaid-syntax-error` located at the node
  with Mermaid's message `There can be only one root. No parent could be found for ("<description>")` and a hint.
- An icon or class decoration before any node is a located `mermaid-syntax-error` that quotes the JavaScript error
  Mermaid fails with.
- More nodes than `MaxNodes`, a node deeper than `MaxNestingDepth`, and a kept text, sanitized text, or class list
  longer than `MaxLabelLength` are a `MermaidSyntaxException` with code `mermaid-limit-exceeded` located at the node or
  the decoration, naming the limit.
- Null arguments are `ArgumentNullException`s. Nothing is caught and swallowed; every exception propagates to the
  document.

#### Dependencies

- **Syntax subsystem** - `PreprocessedSource` and `MappedText` (the text and its locations), `DetectedDiagram` (the
  keyword location), `SourceScanner`, `ScanRule`, `ScanMatcher`, `ScanToken`, `ScanPatterns`, and `TokenCursor` (the
  jison lexer semantics and `JoinAlternatives`), and `HtmlSanitizer.Sanitize` (`sanitizeText`).
- **MindmapRenderer** - `MindmapDiagram.Layout` creates it for each render.
- **Scenegraph subsystem** - `Scene`, the result of `Layout`.
- **System** - `IMermaidDiagram`, `DiagramRenderContext`, `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`,
  and `MermaidDiagnosticCodes`.

#### Callers

- **MermaidDocument** - `DiagramRegistry` calls `Parse` for a detected `mindmap` diagram and keeps the diagram as the
  document's `IMermaidDiagram`.
- **MindmapRenderer** - reads the nodes, their data, and the edges, and `MindmapNodeType` for the paddings.
- The unit tests call `Parse` directly on preprocessed texts, and the vector tests compare the diagram with Mermaid's
  database.

#### Known Differences From Mermaid

- **Errors.** Mermaid draws an error picture; the parser throws a located `MermaidSyntaxException` with a hint, and
  reports only the first error. A decoration before any node, where Mermaid fails with a JavaScript `TypeError`, is a
  located syntax error.
- **Limits.** `MaxNodes`, `MaxNestingDepth`, and `MaxLabelLength` are CanvasNet additions; Mermaid has no limit on the
  depth of a mindmap.
