### StateModel

![MermaidState Structure](MermaidStateView.svg)

The `StateModel` unit is the second software unit in the `State` subsystem. It comprises two source files in the
`DemaConsulting.CanvasNet.Mermaid.State` namespace: `StateModel.cs` (the translation of the statement tree, the
database operations, the metadata, the limits, and the statement and database types) and `StateModel.LayoutData.cs`
(the data fetcher, the mapping to the layout input of a render, the reports of what is not drawn, the shape check, and
the layout of a render). `StateModel` is an `internal sealed partial` class, so the two files are one type, and it
implements the system's `IMermaidDiagram`. `StateModel.cs` also defines the sealed classes `StateStatement`,
`StateNote`, and `StateRecord` and the sealed records `StateRelation`, `StateStyleClass`, and `StateLink`;
`StateModel.LayoutData.cs` defines the enumeration `StateNodeKind`, the sealed class `StateNode`, and the sealed record
`StateEdge`. Both files carry the provenance header comment for the parts of Mermaid they port.

#### Purpose

`StateModel` is Mermaid's state database (`stateDb.ts`, with the constants of `stateCommon.ts`) and data fetcher
(`dataFetcher.ts`) for one parsed text: it applies the statement tree the parser hands it as Mermaid's `setRootDoc`
does, gives the document the metadata of the diagram, maps the diagram for each render to the layout data that
Mermaid's `getData` gives its renderer, reports what this version does not draw as Mermaid does, and has the shared
cluster layout draw the diagram in the frame of Mermaid's unified state renderer (`stateRenderer-v3-unified.ts`). It
exists so that the meaning of a state diagram is decided in one place that can be compared with Mermaid's statement
tree, database, and layout data fact by fact, independently of the grammar and of the drawing.

The unit's behavior is specified by the `CanvasNetMermaid-State-StateModel-*` requirements in
`docs/reqstream/canvas-net-mermaid/state/state-model.yaml`.

#### Data Model

**The constants**: the state types `DefaultType` (`default`) and `DividerType` (`divider`), the id `root` of the
top-level document, the CSS class `statediagram-state` of every state (Mermaid's `CSS_DIAGRAM_STATE`), the one marker
`barb` of the unified state renderer, and its view box padding of 8.

**The statement tree** (built by the parser; the translation changes the ids, documents, and start flags of its
statements, and the database sanitizes the text of their notes):

- **`StateStatement`** (Mermaid's `Stmt`): the kind (`state`, `relation`, `classDef`, `style`, `applyClass`, `click`,
  `dir`, or `root`, or null for a statement the grammar keeps as text, with its `Text`), the id, the type (`default`,
  `fork`, `join`, `choice`, or `divider`), the description or the two `DescriptionParts` of
  `state "description" as id:text`, the document of a composite state or region, the note, the start flag of a
  translated `[*]`, the `:::` classes, the direction of a `dir` statement, the styles of `classDef`, the styles of
  `style` or the class of `class` (`StyleClass`), the URL and tooltip of a click (with their quotes), the two states of
  a transition, the target of a click, and the location of the statement;
- **`StateNote`**: the position as written (`left of` or `right of`, in its case) and the text.

**The database** (Mermaid's `getStates`, `getRelations`, `getClasses`, and `getLinks` of the top-level document):

- the **states** in insertion order and by id (`StateRecord`: the id, the type, the sanitized descriptions, the document
  of a composite state, the note, the CSS classes, the styles of `style` statements, and the text styles, which the
  grammar never sets);
- the **transitions** in order (`StateRelation`: the two state ids and the sanitized label);
- the **style classes** of `classDef` in insertion order and by id (`StateStyleClass`: the styles and the text styles);
- the **links** of the click statements (`StateLink`: the click's state statement, the URL, and the tooltip, with their
  quotes), kept and never drawn.

**The data fetcher** (Mermaid's `nodeDb`, `nodes`, and `edges`):

- the record of each state id (`FetchedState`, Mermaid's `nodeDb` entry: the shape, or null where Mermaid leaves it
  undefined, the description as one text or as a list, the CSS classes, the styles, the type `group` of a cluster, and
  its direction);
- the **nodes** of the layout in insertion order and by id (`StateNode`: the id, the kind of `StateNodeKind` - `State`
  for a state, a composite state, or a concurrent region, `Note`, or `NoteGroup` - the shape, the label, the
  descriptions after the first, the CSS classes, the styles, the styles compiled from the node's style classes, the
  direction of a cluster, the DOM id, the group flag, the parent, the position of a note, and the location of the
  statement that last set the node);
- the **edges** of the layout in order (`StateEdge`: the id `edgeN` of a transition or `<from>-<to>` of a note link,
  the two ends, the sanitized label of a transition, whether it links a note, and the location of the statement).

**The counters**, each numbered per model from the start of a text: the divider ids `divider-id-N` (Mermaid's
`dividerCnt`, numbered from 1 in parse order), the generated region ids `id-random-N` (the counter of Mermaid's
`generateId`, numbered from 1 in translation order), the legacy start and end ids `startN` (Mermaid's `startEndCount`),
the data fetcher's `graphItemCount`, which the transitions and the notes share and which numbers the edge ids and the
DOM ids (`state-<id>-<n>`, `state-<id>----note-<n>`, and `state-<id>----parent-<n>`), and the count of transitions for
`MaxEdges`.

**The identity of the diagram**: Mermaid's id (`stateDiagram`, or the legacy `state`, its `diag.type`; `IsLegacy` tells
them apart), the location of the diagram keyword, the accessible title and description, and the top-level statements
after the translation (`RootDocument`). The model is filled by one parser on one thread and is never changed after
parsing, so it is safe for concurrent renders.

#### Key Methods

- **Texts.** `SetAccessibleTitle` and `SetAccessibleDescription` sanitize the texts as Mermaid's `setAccTitle` and
  `setAccDescription` do (the title without leading white space, the description without the white space after each
  line feed). `Sanitize` is Mermaid's `sanitizeText` through `HtmlSanitizer`, an empty text staying as it is;
  `TrimColon` is Mermaid's `trimColon`. `NextDividerId` gives out `divider-id-N` for the parser.
- **`SetRootDocument(List<StateStatement>)`** runs what Mermaid's `setRootDoc` runs: it keeps the statements, wraps them
  in the `root` statement, translates the tree (`Translate`) and applies the top-level statements to the database
  (`Apply`) - the current diagram translates first, the legacy one applies first, as Mermaid's legacy database does -
  then runs the data fetcher over the translated top-level statements (`FetchDocument`) and processes the labels
  (`ProcessLabels`).
- **`Translate`** (Mermaid's `docTranslator`) translates the two states of a transition (the first as a start, the
  second as an end) and each state: `[*]` becomes `<parent>_start` or `<parent>_end` (`root_start` and `root_end` at the
  top) with its start flag, and other ids are trimmed. A document with dividers becomes its concurrent regions: each
  divider gets the statements before it as its document, and the statements after the last one go into a new region
  of type `divider` with the generated id `id-random-N`, located at its composite state; a document whose last statement
  is a divider is left as it is, so its dividers stay plain states, as in Mermaid. The statement objects are reused
  where Mermaid clones them, which is equivalent because each is referenced once.
- **`Apply`** (one step of Mermaid's `extract`) applies a top-level statement: a state (`AddState`, without the
  classes of a lone `id:::class`, which Mermaid does not apply), a transition (`AddRelation`), a `classDef`
  (`AddStyleClass`), a `style` (`SetStyles`), a `class` (`SetCssClass`), or a click (the link is recorded).
- **`AddState`** (Mermaid's `addState`) adds a state unless it exists (checking `MaxNodes`) and otherwise keeps a
  document it did not have yet; adds the descriptions (`AddDescription`: a leading colon removed, sanitized), the note
  (whose text is sanitized in place, so a note in a composite state's document is left for the label pipeline to
  sanitize, as Mermaid's shapes do), and the CSS classes.
- **`AddRelation`** (Mermaid's `addRelation` for statement objects) adds both states with their descriptions, notes,
  and `:::` classes and the transition with its sanitized label; in the legacy database `StartIfNeeded` names each
  `[*]` `start` and a running number, of type `start`.
- **`AddStyleClass`** (Mermaid's `addStyleClass`) adds or extends a style class: each comma-separated attribute loses
  its first semicolon and is trimmed, and an attribute that mentions `color` also gives a text style, with its first
  `fill` written `bgFill` and then its first `color` written `fill`. **`SetStyles`** (Mermaid's `handleStyleDef`) gives
  each listed state, added when it does not exist, the comma-separated styles without any semicolon, in place of its
  styles (computed once for the statement). **`SetCssClass`** (Mermaid's `setCssClass`) adds a CSS class to
  comma-separated states, adding the states that do not exist. As in Mermaid, a state listed again gets the class
  again and a class defined again gets its styles again, so the database keeps the running length of the CSS classes of
  each state as the data fetcher joins them (`CssClassesLength`: the classes joined with spaces, then
  `statediagram-state`) and of the styles and, apart, the text styles of each style class (joined with semicolons),
  and checks each against `MaxLabelLength` as it grows; it also keeps the location of the last statement that gave a
  state a class.
- **`FetchDocument`** (Mermaid's `setupDoc`) fetches the states of a document and, for a transition, both its states,
  checks `MaxEdges`, and adds the edge `edgeN` with the sanitized label, advancing the shared counter. **`Fetch`**
  fetches a state and then its document, with the alternate flag flipped and the level one deeper.
- **`FetchState`** (Mermaid's `dataFetcher`) gives a state seen for the first time the shape of its start flag
  (`stateStart`, `stateEnd`, else `rect`), replaced by its type unless it is `default` (`fork`, `join`, `choice`, or
  `divider`; none for a note statement, whose type is undefined, as in Mermaid), the sanitized id as its text, the CSS
  classes `<classes> statediagram-state`, and the styles of the database; adds the descriptions
  (`AddFetchedDescriptions`); turns a state with a document into a cluster the first time (checking the level against
  `MaxNestingDepth`): the type `group`, the direction of its document's last `direction` statement, the shape `divider`
  for a region or `roundedWithTitle`, and the classes `statediagram-cluster` and, at every other depth,
  `statediagram-cluster-alt`; and inserts the node with the DOM id `state-<id>-<n>` and its parent (none at the top).
  A note adds a note group `<id>----parent` at the top level (shape `noteGroup`, the state's CSS classes, the DOM id
  `state-<id>----parent-<n>`) and a note `<id>----note-<n>` in it (shape `note`, class `statediagram-note`, the
  position, and the group flag of its state), advances the shared counter, inserts the group, the note, and the state
  in that order, and adds the note link without a label, from the note to the state for `left of` and from the state
  to the note otherwise.
- **`AddFetchedDescriptions`** applies the descriptions of a statement as the data fetcher does: the first replaces the
  id as the text (shape `rect`); each further one makes the state a state with a title (`rectWithTitle`) with the list
  of descriptions, sanitized again as a whole, as Mermaid's `sanitizeTextOrArray` does; a description makes a choice
  state a plain state, as in Mermaid. A `rectWithTitle` with one description goes back to `rect`, or to
  `roundedWithTitle` for a cluster.
- **`Insert`** (Mermaid's `insertOrUpdateNode`) ignores an empty id and Mermaid's `</join></fork>` and `</choice>` ids,
  gives the node the compiled styles of its CSS classes, and either updates the node of the same id
  (`StateNode.Assign`, JavaScript's `Object.assign`: every field taken except a
  parent the newer node does not have) or adds the node, checking `MaxNodes`.
- **`CompileStyles`** compiles the styles of CSS classes as `insertOrUpdateNode` does: for each space-separated class
  that is a style class, its styles, in class order, checking the running length joined with semicolons against
  `MaxLabelLength`. Mermaid compiles them again for every node it inserts; they depend only on the CSS classes and on
  the style classes, which are complete before the data fetcher runs, so `FetchState` compiles them once for each
  fetched state (again only when the state becomes a cluster and its classes change) and shares the list among the
  state's nodes and its note group, and the note class `statediagram-note` is compiled once for all notes. The result
  is the same as Mermaid's, while the work no longer grows with the mentions of a state.
- **`ProcessLabels`** (the end of Mermaid's `extract`) gives each node whose description is a list its first
  description as the label and the others as its descriptions, failing a cluster that has more than its title.
- **Metadata** (`IMermaidDiagram`): `Title` is always null (the state grammar has no title statement, so the document
  falls back to the frontmatter title), `AccTitle` the accessible title or null, `Direction` the `LayoutDirection` (the
  last top-level `direction` statement, else `TB`), `NodeCount` the nodes of kind `State` that are not clusters (plain
  states, states with descriptions, the start and end states, and the fork, join, and choice states), `EdgeCount` the
  edges that do not link a note, and `SubgraphCount` the clusters of kind `State` whose shape is not `divider` (the
  composite states). The model's own `Direction` is Mermaid's `getDirection` (the first top-level `direction`
  statement, else `TB`), which the vectors compare.
- **`GetData(MermaidConfig)`** maps the nodes and edges under the configuration of a render to `LayoutData`, in the data
  fetcher's order and with the render's look: states, composite states, and regions with the label type `markdown`, a
  padding of 8, Mermaid's radius `Rx` and `Ry` of 10, and a centered label (the first description as the label and the
  others as `Descriptions`); notes with the label type `markdown` and the padding `flowchart.padding` of the
  configuration; note groups with a padding of 16 and no label type; an empty shape for a node Mermaid leaves without
  one. The edges carry Mermaid's arrowhead (`normal` for a transition, `none` for a note link) as the type, the label
  (empty for a note link), the label type `markdown`, the label position `c`, the thickness `normal`, the classes
  `transition` or `transition note-edge`, the end marker `arrow_barb` for a transition and none for a note link, the
  arrowhead style `fill: #333`, the style `fill:none`, and a minimum length of 1 (Mermaid's state edges have no
  `minlen`, so dagre's default applies). The data carries the diagram type `stateDiagram` (the legacy id too, since it
  is drawn by the unified renderer), the layout direction, the `barb` marker, and the node and rank spacing of the state
  section (`state.nodeSpacing` and `state.rankSpacing`, each 50 when missing or zero). A state keeps the parent it was
  first given when a later statement names it at the top (`StateNode.Assign`), so a composite state named inside another
  one and declared again at the top nests its states one level deeper; chained, such statements nest composite states
  without any nesting in the text, so `GetData` checks the nesting the parents make with `LayoutData.CheckNestingDepth`
  (`composite states and their concurrent regions`, `MaxNestingDepth`).
- **`ReportUnsupported(MermaidConfig, MermaidReportSink)`** reports, in order, the legacy renderer of the legacy id
  (`mermaid-layout-unsupported`, naming the `state.defaultRenderer` and located at the top-level `state` key), a look
  other than `classic` (`mermaid-look-unsupported`), and a layout other than `dagre` (`mermaid-layout-unsupported`),
  each located at its configuration key with a hint.
- **`Layout(DiagramRenderContext)`** reports first, then maps the model with `GetData`, fails a node without a shape
  (`CheckShapes`), and has a new `ClusterLayout` draw the data through `RenderDiagram`, with a `LabelBuilder`, the
  `StateStyles` sheet of the configuration, the node image loader, and a `LayoutWorkMeter` of the render; the
  frontmatter title is drawn centered above the drawing at the state section's title margin (`state.titleTopMargin`,
  25 by default), and the view box is the drawn bounds grown by the padding of 8 of Mermaid's state renderer.

#### Error Handling

- Every error while the statement tree is applied is a `MermaidSyntaxException` located at a statement in the original
  text, with a hint:
  - Mermaid's database error (code `mermaid-syntax-error`), with Mermaid's message after the location: a composite
    state with a description beyond its title ("Group nodes can only have label. Remove the additional description for
    node [Comp]"), located at the statement that last set the node, which is the one that added the extra description;
  - more transitions than `MaxEdges` (`mermaid-limit-exceeded`, at the transition, with the message `more than N
    transitions`); the links of notes are not counted, as each comes with a note and a note group that count as nodes;
  - more than `MaxNodes` (`mermaid-limit-exceeded`, at the statement that adds one too many, with the flowchart's
    message `more than N nodes and subgraphs`), checked on two counts: the states of the database, a state that only a
    `class` or `style` statement names included (in `AddState`), and the nodes of the layout - states, composite
    states, concurrent regions (the generated last region included), notes, and note groups together (in `Insert`) - as
    the flowchart model counts vertices and subgraphs;
  - a cluster nested deeper than `MaxNestingDepth` (`mermaid-limit-exceeded`, located at the statement of the composite
    state or region too deep, checked by `CheckDepth`): the composite states and their concurrent regions each count as
    a level, because each nests one more cluster that the shared rendering lays out recursively; the parser counts only
    the composite states (see _StateParser Unit Design_);
  - a text the model joins or derives from texts the parser checked apart, longer than `MaxLabelLength`
    (`mermaid-limit-exceeded`, with the message `a label is N characters long`, checked by `CheckLabel` and
    `CheckLabelLength`): the descriptions under a title, counted as `rectWithTitle` draws them, joined with `<br/>` (5
    characters for each join), located at the statement that adds a description; the id `<parent>_start` or
    `<parent>_end` of a start or end state, located at its `[*]`; the text style of a style class (with `fill`
    written `bgFill`, 2 characters longer), located at the `classDef` statement; the CSS classes of a state, located at
    the `class` statement or `:::` mention that makes them too long; the styles and the text styles of a style class,
    located at the `classDef` statement; and the compiled styles of a state, located at the last statement that gave
    the state a class (else at the statement fetched). Every other text the model keeps was checked by the parser.
- `GetData` throws a `MermaidRenderException` with code `mermaid-limit-exceeded` when composite states nest deeper than
  `MaxNestingDepth` through their parents (`composite states and their concurrent regions are nested deeper than N
  levels`), located at the statement that last set the first state one level too deep; a cycle of parents is left to
  the layout, which fails on it.
- `ReportUnsupported` throws `MermaidUnsupportedFeatureException` through the sink at the first unsupported feature in
  strict mode.
- `Layout` fails a node without a shape (a note on a state that appears nowhere before it, on which Mermaid's render
  fails with "No such shape: undefined") with a `MermaidRenderException` of code `mermaid-render-failed`, located at the
  note statement, whose message quotes Mermaid's and whose hint asks to declare the state before its note. It
  propagates the render errors of the shared rendering: a parent cycle of composite states (`mermaid-render-failed`,
  located, from the cluster layout, as graphlib's `setParent` fails Mermaid's render), the layout work limit and the
  other render limits (`MermaidRenderException`), and `OperationCanceledException` for a canceled token.
- The public methods throw `ArgumentNullException` for null arguments.

#### Dependencies

- **Syntax subsystem** - `HtmlSanitizer` (Mermaid's `sanitizeText`), `Preprocessor.TrimJs`, and the JavaScript
  character classes of `ScanPatterns`.
- **Configuration subsystem** - `MermaidConfig` (the look, the layout, the flowchart padding of the notes, and the
  location of keys), `StateConfig` (the spacing, the title margin, and the `defaultRenderer`), and `StateStyles` (the
  state style sheet).
- **Rendering subsystem** - `LayoutData`, `LayoutNode`, and `LayoutEdge` (the layout input), and `ClusterLayout`,
  `ShapeContext`, `LabelBuilder`, and `NodeImages` (the drawing).
- **Dagre subsystem** - `LayoutWorkMeter`, the work budget and cancellation of a render.
- **Scenegraph subsystem** - `Scene`, the result of a layout.
- **System** - `IMermaidDiagram`, `DiagramRenderContext`, `MermaidLimits`, `MermaidLocation`, `MermaidReportSink`,
  `MermaidSyntaxException`, `MermaidRenderException`, and `MermaidDiagnosticCodes`.

#### Callers

- **StateParser** - creates the model, gets the divider ids and sets the accessibility texts while it reads, and hands
  it the statement tree (`SetRootDocument`).
- The document pipeline of the system - reads the metadata and calls `Layout` for every measure and render.
- The unit and subsystem tests read the statement tree, the database, the nodes, and the edges, and call `GetData` and
  `ReportUnsupported` directly, and the vector tests compare the facts and the layout data with Mermaid's.

#### Known Differences From Mermaid

- **Region ids.** Mermaid's `generateId` names the last region of a concurrent state with a random number; the model
  writes `id-random-N`, numbered per model, so the node, cluster, and DOM ids are deterministic.
- **Database and render errors are located.** Where Mermaid's database throws (a composite state with more than its
  title) or its render fails (a note on a state that appears nowhere before it, with "No such shape: undefined", and a
  parent cycle of composite states), the model raises a located `MermaidSyntaxException` or `MermaidRenderException`
  with Mermaid's message and a hint; Mermaid fails on the same texts.
- **Limits at open.** `MaxEdges` refuses more transitions, `MaxNodes` more states or more layout nodes (regions and
  note groups included), `MaxNestingDepth` composite states and regions nested deeper, and `MaxLabelLength` a longer
  joined or derived text (repeated classes and styles included), when the document is opened, as a
  `MermaidSyntaxException` with code `mermaid-limit-exceeded`; composite states nested deeper through names are refused
  when the diagram is laid out, as a `MermaidRenderException` with the same code. Mermaid has no such limits.
- **Interaction.** The links of the click statements are kept only as data; they are never drawn or bound and are not
  reported.
- **Security level.** Texts are always sanitized as Mermaid sanitizes them at its strict security level, the only level
  modeled.
- **Accessibility.** `accTitle` and `accDescr` are metadata only; a rendered picture has no SVG title or description.
- **Legacy renderer.** The legacy state renderer is not ported: the legacy id is reported as
  `mermaid-layout-unsupported` at the top-level `state` key and drawn by the unified renderer, with the identical
  pixels of a `stateDiagram-v2`.
- **Metadata direction.** The metadata gives the direction the diagram is laid out in (the last top-level `direction`
  statement), where Mermaid's `getDirection` gives the first.
- **Reports.** A look other than `classic` (the hand-drawn look) and a layout other than `dagre` (such as `elk`) are
  reported as unsupported and drawn in the classic look with dagre (see _CanvasNetMermaid System Design_, Fidelity and
  Known Differences).
