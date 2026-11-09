### FlowchartParser

![Flowchart Structure](MermaidFlowchartView.svg)

The `FlowchartParser` unit is the first software unit in the `Flowchart` subsystem. It comprises four source files in
the `DemaConsulting.CanvasNet.Mermaid.Flowchart` namespace: `FlowchartParser.cs` (the entry point, the header, the
statement lists, subgraphs, labels, and the error machinery), `FlowchartParser.Lexer.cs` (the lexer rules and start
conditions, and the static `FlowchartTokens` class with the token names), `FlowchartParser.Statements.cs` (vertex
statements, node groups, shape data, vertices and their brackets, ids, and the `style`, `classDef`, `class`,
`linkStyle`, and `click` statements), and `FlowchartParser.Links.cs` (links and link texts). `FlowchartParser` is an
`internal sealed partial` class, so the four files are one type; `FlowchartTokens` is an `internal static` class. Every
file carries the provenance header comment for the parts of Mermaid it ports.

#### Purpose

`FlowchartParser` reads the preprocessed text of a flowchart into a `FlowchartModel`, accepting exactly the language of
the generated parser of Mermaid's `flow.jison` and calling the model with the same operations, in the same order, as
Mermaid's parser calls its flowchart database. It exists so that every text means what it means to Mermaid, so that an
invalid text fails with Mermaid's diagnosis at the right place in the original file, and so that reading untrusted text
costs time linear in its length.

The unit's behavior is specified by the `CanvasNetMermaid-Flowchart-FlowchartParser-*` requirements in
`docs/reqstream/canvas-net-mermaid/flowchart/flowchart-parser.yaml`.

#### Data Model

**`FlowchartTokens`** holds the token kinds as constants whose values are the names of the terminals of `flow.jison`
(`GRAPH`, `DIR`, `NODIR`, `SQS`, `SQE`, `PS`, `PE`, `DIAMOND_START`, `LINK`, `START_LINK`, `LINK_ID`, `SHAPE_DATA`,
`NODE_STRING`, `UNICODE_TEXT`, `subgraph`, `end`, and so on), so that messages and expected-token lists read like
Mermaid's parse errors. `JisonName` gives the printed name (`EOF` for the end-of-file terminal, which must be told apart
from the end of the token stream) and `Number` gives jison's terminal number, which orders expected-token lists as jison
prints them.

**A parser instance** parses one text on one thread and holds:

- the `FlowchartModel` it fills and the `MermaidLimits` of the open call;
- the token cursor over a `SourceScanner` built from the lexer rules (one token of lookahead);
- the set of **expected token kinds** recorded since the last token was consumed (cleared when a token is consumed);
- the current subgraph nesting depth;
- the per-line **lexer caches**: the run of non-space characters and its last usable `@` (for the `LINK_ID` rule), and
  the line and the last `direction` word of each direction (for the rules that find `direction XX` anywhere on a line).

**Lexer tables.** The 122 rules of the generated lexer are ported one to one, in jison's order, each with a
hand-written matcher in place of its regular expression and the action of the rule (a token kind, a change of start
condition, or both); the 21 start conditions (`dir`, `vertex`, `text`, `edgeText`, `thickEdgeText`, `dottedEdgeText`,
`trapText`, `ellipseText`, `string`, `md_string`, `shapeData`, `shapeDataStr`, `shapeDataEndBracket`, `click`,
`href`, `callbackname`, `callbackargs`, `acc_title`, `acc_descr`, `acc_descr_multiline`, and the initial one) list the
indexes of their rules in the order jison tries them, the first matching rule winning. The letters outside ASCII that
the `UNICODE_TEXT` rule matches are a sorted table of code-unit ranges generated from the rule's character classes.

The grammar values are the records of `FlowchartModel.Elements.cs` that the model owns: `FlowText` (a label and its
type `text`, `string`, or `markdown`), `FlowLink` (what the link token means, its text, and its id), `ShapeDataText`
(the text of an `@{ ... }` block with the map of its characters to the original text), and `DocumentItem` (one value of
a statement list, for the members and the direction of a subgraph).

#### Key Methods

- **`Parse(PreprocessedSource, DetectedDiagram, MermaidLimits)`** creates the model (with the location of the diagram
  keyword) and a parser, removes the white space after closing braces, and runs the grammar over the parser text of
  the source, whose characters stay mapped to the original text.
- **`TrimSpaceAfterBraces(MappedText)`** applies the clean-up of `flowParser.ts` (`src.replace(/}\s*\n/g, '}\n')`)
  on the mapped text, so that every remaining character keeps its location.
- **`ParseHeader`** reads leading white space and line breaks, the `graph` or `flowchart` keyword, and either a header
  without a direction (`TB`) or a direction and the first separator, and sets the direction on the model.
- **`ParseDocument`** reads statements up to the end of the text or to the `end` of a subgraph, keeping the value of
  every statement (the text of separators too, as jison does) for the members of the subgraph; **`TryParseStatement`**
  dispatches on the lookahead: `subgraph`, the accessibility statements, `style`, `linkStyle`, `classDef`, `class`,
  `click`, `direction`, or a vertex statement.
- **`ParseSubgraph`** counts the nesting depth against `MaxNestingDepth`, reads the optional id and title, the
  statements, and `end`, and adds the subgraph to the model.
- **`ParseVertexStatement`**, **`ParseNodeGroup`**, **`ParseStyledVertex`**, and **`ParseVertex`** read a node group
  (nodes joined by `&`, with shape data and `:::class`), then links each followed by a node group, adding every edge
  from every node of one group to every node of the next; each vertex is added with the shape its brackets give
  (`square`, `round`, `circle`, `doublecircle`, `ellipse`, `stadium`, `subroutine`, `cylinder`, `diamond`, `hexagon`,
  `odd`, `trapezoid`, `inv_trapezoid`, `lean_right`, `lean_left`, or `rect` with a property). Shape data after a node
  is applied when jison reduces it: after the node that follows the next `&`, or before the group is used.
- **`ParseShapeData`** concatenates the `SHAPE_DATA` tokens of an `@{ ... }` block and builds the function that locates
  a position of the concatenated text in the original text (exactly where the token text is unchanged, at the token
  start otherwise), which the model passes to the YAML reader.
- **`ParseLink`** and **`ParseEdgeText`** read `[id@]-->[|text|]` or `[id@]-- text -->` in every stroke and ask the
  model to read the link token (`FlowchartModel.DestructLink`).
- **`ParseText`** and **`ParseTextNoTags`** read labels and subgraph titles, with their types.
- **`ParseStyleStatement`**, **`ParseClassDefStatement`**, **`ParseClassStatement`**, **`ParseLinkStyleStatement`**,
  and **`ParseClickStatement`** read the styling and interaction statements; every `click` form (a callback with
  arguments, `call`, `href`, or a link, with a tooltip and a target) is read completely and only marks the ids
  clickable.
- **`Is`**, **`Advance`**, **`Expect`**, and **`Fail`**: `Is` tests the lookahead and, when it does not match, records
  the kinds it tested as expected; `Advance` consumes a token and clears the record; `Fail` builds the located exception
  with the recorded kinds sorted by jison's terminal numbers. Where jison's LALR(1) tables merge states, the parser
  adjusts the record as jison's messages show: no `SPACE` after a space list, `end` listed at the top level after a
  line, every shape closer after an unterminated label, and `TESTSTR` after a link.

#### Error Handling

- A token the grammar does not accept is a `MermaidSyntaxException` with code `mermaid-syntax-error`, located at the
  token in the original text, with the message `expected 'A', 'B' or 'C', found 'KIND' ('text')` (or `found the end
  of the text`), the expected token names as `Expected`, and a hint chosen from the context (a matching closing
  bracket, the arrow of a link with text, a statement that is not complete, or a subgraph without `end`).
- Text that no lexer rule matches is a located lexical error raised by the scanner of the `Syntax` subsystem.
- A subgraph nested deeper than `MaxNestingDepth` is refused at its keyword with code `mermaid-limit-exceeded`.
- The errors that the model raises while the parser fills it (unknown shapes, invalid shape data, a `linkStyle` index
  without an edge, a subgraph without a name, and the edge, node, and label limits) propagate unchanged.
- `Parse` throws `ArgumentNullException` for a null argument.

#### Dependencies

- **FlowchartModel** - the database the parser fills, the link reader `DestructLink`, the element records, and the
  first-header flag that the header rule consults.
- **Syntax subsystem** - `SourceScanner`, `ScanRule`, `ScanMatcher`, `ScanToken`, `TokenCursor`, and `ScanPatterns`
  (the scanner, the cursor, and the JavaScript character classes), `MappedText` and `PreprocessedSource` (the text
  mapped to the original), `DetectedDiagram`, and `Preprocessor.TrimJs` (JavaScript's `trim`).
- **System** - `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`, and `MermaidDiagnosticCodes`.

#### Callers

- The document pipeline of the system (`MermaidDocument.Pipeline.cs`) calls `Parse` for a text whose detected type is
  a flowchart.
- The unit tests call `Parse` and `TrimSpaceAfterBraces` directly.

#### Known Differences From Mermaid

- **No regular expressions.** The lexer is a set of hand-written scanners with the same matches as Mermaid's regular
  expressions; the rules that backtrack or look far ahead in Mermaid (link ids, `direction` on a line, the arrows, the
  header without a direction) are matched with per-run and per-line caches, so a long line costs linear time instead
  of the backtracking of the expressions.
- **Recursive descent instead of LALR(1) tables.** The parser accepts the same language with one token of lookahead
  and emulates the expected-token lists of jison's tables, including the effects of its merged states; the 26 grammar
  errors of the upstream vectors give the identical lists in jison's order.
- **Located errors.** Mermaid's parse errors name a line of the text that its parser sees (after the frontmatter and
  the directives are removed); this unit's errors are located by line and column in the original text and carry a
  hint.
  The failures of Mermaid's database are located CanvasNet exceptions too (see the FlowchartModel unit).
- **Limits.** The nesting limit (and, through the model, the edge, node, and label limits) refuses the text when the
  document is opened, with code `mermaid-limit-exceeded`; Mermaid has no nesting or label limit.
- **Interaction and accessibility.** `click`, `call`, `href`, links, tooltips, and targets are read and ignored except
  for the `clickable` class; `accTitle` and `accDescr` are kept as metadata of the diagram only, since a rendered
  picture has no SVG to carry them.
