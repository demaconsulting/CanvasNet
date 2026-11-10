### ErModel

![MermaidEr Structure](MermaidErView.svg)

The `ErModel` unit is the second software unit in the `Er` subsystem. It comprises two source files in the
`DemaConsulting.CanvasNet.Mermaid.Er` namespace: `ErModel.cs` (the database operations, the completion, the metadata,
the limits, and the element types) and `ErModel.LayoutData.cs` (the mapping to the layout input of a render, the
reports of what is not drawn, and the layout of a render). `ErModel` is an `internal sealed partial` class, so the two
files are one type, and it implements the system's `IMermaidDiagram`. `ErModel.cs` also defines the sealed classes
`ErEntity` and `ErSubgraph` and the sealed records `ErAttribute`, `ErRelationshipSpec`, `ErRelationship`, and
`ErStyleClass`. Both files carry the provenance header comment for the parts of Mermaid they port.

#### Purpose

`ErModel` is Mermaid's ER database (`erDb.ts`, with the types of `erTypes.ts`) for one parsed text: it applies the
operations that the parser calls with Mermaid's semantics, gives the document the metadata of the diagram, maps the
diagram for each render to the layout data that Mermaid's `getData` gives its renderer, reports what this version does
not draw, and has the shared cluster layout draw the diagram in the frame of Mermaid's ER renderer
(`erRenderer-unified.ts`). It exists so that the meaning of an entity relationship diagram is decided in one place that
can be compared with Mermaid's database and layout data fact by fact, independently of the grammar and of the drawing.

The unit's behavior is specified by the `CanvasNetMermaid-Er-ErModel-*` requirements in
`docs/reqstream/canvas-net-mermaid/er/er-model.yaml`.

#### Data Model

**The constants**: `MermaidId` (`er`, Mermaid's `diag.type` of the diagram), `Identifying` (`IDENTIFYING`, the
relationship type drawn with a solid line), the `default` CSS class that every entity has first, the four ER markers
`only_one`, `zero_or_one`, `one_or_more`, and `zero_or_more` that the diagram defines, and the view box padding of 8.

**The database** (filled by one parser on one thread, completed at the end of the text, read only afterwards and then
safe for concurrent renders):

- the **entities** in the order they are first named and by name (`ErEntity`): the name, which is also the label; the
  id `entity-<name>-<n>`, where `n` counts the entities before it, those named after a subgraph included; the alias,
  empty for none, of which the first one that is not empty is kept; the attributes in order, a repeated attribute
  block appending to them; the CSS classes after `default`, kept as a list (`Classes`), with `CssClasses` joining them
  into Mermaid's `cssClasses` text when it is read and `CssClassesLength` keeping the length of that text, so that
  repeated classes never rebuild it; the inline styles of the `style` statements; the compiled styles and the color
  index, set at completion; and the locations of the first mention and of the last statement that gave classes;
- **`ErAttribute`**: the type as written (with generic tildes, brackets, a backticked word, or a joined `?`), the name,
  the keys in order as written (`PK`, `FK`, and `UK` in any case), and the comment without its quotes, empty for none;
- the **relationships** in order (`ErRelationship`: the id of the first entity or of the subgraph it names, the role,
  the id of the second entity or subgraph, the specification, and the location of the statement), with the
  specification `ErRelationshipSpec` holding `CardA`, the right cardinality, `RelType`, and `CardB`, the left
  cardinality, as Mermaid's grammar swaps them;
- the **style classes** of `classDef` in insertion order and by name (`ErStyleClass`: the styles, and as text styles
  those that contain `color`, with their first `fill` written `bgFill`);
- the **subgraphs** in the order they end, a nested one before the one that holds it, and by trimmed id, a later
  subgraph with the same id replacing an earlier one in the lookup (`ErSubgraph`: the trimmed id, the sanitized and
  trimmed title, the nodes, the CSS classes and the length of their text joined with spaces, the inline styles, the
  compiled styles, the direction or null, and the locations of the `subgraph` keyword and of the last statement that
  gave classes), and the running set of the nodes that the subgraphs hold (for Mermaid's `makeUniq`);
- the direction (the last top-level `direction` statement, else `TB`), the accessible title and description, the
  limits of the open call, and the location of the diagram keyword.

#### Key Methods

- **Entities.** `AddEntity` adds an entity when its name is new (checking `MaxNodes`, with the id
  `entity-<name>-<n>`) and otherwise gives an entity without an alias the alias, and returns the entity.
  `AddAttributes` adds the entity when it is new and appends the attributes in order (Mermaid's `addAttributes` adds
  them in reverse to undo the order of its recursive grammar rule, so the result is the same).
- **Relationships.** `AddRelationship` takes, for each side, the id of the subgraph of that name when one has already
  been added (it has ended), else the id of the entity of that name, added when it is new; it checks `MaxEdges` and
  keeps the relationship. Because the parser adds both entities before the relationship, as Mermaid's grammar does, a
  side named after a subgraph also has an entity of that name: it takes an entity number and counts against
  `MaxNodes`, but it is not drawn.
- **Subgraphs.** `AddSubgraph` checks `MaxNodes`, trims the id, and keeps as nodes the values of the subgraph's
  statements in order, without empty texts and without duplicates (both compared trimmed, the values kept as written,
  Mermaid's `uniq`) and without the nodes that an earlier subgraph, a nested one included, already holds (Mermaid's
  `makeUniq`, with a running set so that it stays linear); the title is sanitized and trimmed, and the trimmed id is
  returned as the subgraph's value in the enclosing document.
- **Styles.** `AddClass` adds or extends a style class: each style is kept, and a style that contains `color` also
  gives a text style with its first `fill` written `bgFill`. `SetClass` adds class names to the entities and subgraphs
  of the ids that exist, and `AddCssStyles` adds inline styles to them; unknown ids are ignored, as in Mermaid. As in
  Mermaid, an id listed again gets the classes or the styles again, so the model keeps the running length of the
  inline styles of each entity and subgraph (`CssStylesLength`) and of the styles and, apart, the text styles of each
  style class (`StylesLength`, `TextStylesLength`), each joined with semicolons, and checks it against
  `MaxLabelLength` as it grows (`AddStyles`), as it does for the CSS classes.
- **Completion.** `Complete`, which the parser calls at the end of the text, does what Mermaid's `getData` computes
  from the database: each entity that is not named after a subgraph gets a color index, counting the drawn entities in
  order (Mermaid's `colorIndex`; an entity named after a subgraph gets none and is not drawn), and the compiled styles
  of the `default` class followed by its classes; each subgraph gets the compiled styles of its classes. Compiled
  styles (Mermaid's `getCompiledStyles`) are, for each class with a `classDef`, its styles and then its text styles,
  trimmed.
- **Texts.** `SetDirection` sets the direction; `SetAccessibleTitle` and `SetAccessibleDescription` sanitize the texts
  as Mermaid's `commonDb.ts` does (the title without leading white space, the description without the white space after
  each line feed). Texts are sanitized with `HtmlSanitizer` where Mermaid sanitizes them, an empty text staying as it
  is.
- **Metadata** (`IMermaidDiagram`): `Title` is always null (the grammar's `title` statement has no token, so the
  document falls back to the frontmatter title), `AccTitle` the accessible title or null, `Direction` the direction,
  `NodeCount` the entities with a color index (the drawn ones), `EdgeCount` the relationships, and `SubgraphCount` the
  subgraphs.
- **`GetData(MermaidConfig)`** maps the model under the configuration of a render to `LayoutData`, in Mermaid's order:
  first the subgraphs, the last one first, as `rect` clusters with a padding of 8, the title as the markdown label,
  the direction, the classes joined with spaces, the inline and compiled styles, and the look; then the drawn entities
  in order as `erBox` nodes with their ids, the name as the markdown label, the CSS classes, the inline and compiled
  styles, the look, and a `LayoutErBox` of the alias and the attributes (`LayoutErAttribute`). The parent of a cluster
  or an entity is the subgraph that holds it, the first one when two hold it, because the lookup is filled from the
  last subgraph to the first. The relationships are edges `id_<A>_<B>_<n>`, numbered from 0, from the first side's id
  to the second's, with the role as the markdown label at the center (`c`), the type `normal`, the curve `basis`, the
  thickness `normal`, the class `relationshipLine`, the pattern `solid` for an identifying relationship and `dashed`
  otherwise, and the cardinalities in lower case as markers, `cardB` (the left one) at the start and `cardA` (the right
  one) at the end, where `md_parent` names no marker; every edge has a minimum length of 1 (Mermaid's ER edges have no
  `minlen`, so dagre's default applies). The data carries Mermaid's id of the diagram, the direction, the four ER
  markers, and the node and rank spacing of the `er` section (`er.nodeSpacing || 140` and `er.rankSpacing || 80`, which
  Mermaid's ER renderer writes into the spacing of the render). A subgraph named in another subgraph is nested in it
  wherever it is declared, so chained subgraphs nest without any nesting in the text; `GetData` checks the nesting the
  parents make with `LayoutData.CheckNestingDepth` (`subgraphs`, `MaxNestingDepth`).
- **`ReportUnsupported(MermaidConfig, MermaidReportSink)`** reports, in order, a look other than `classic`
  (`mermaid-look-unsupported`) and a layout other than `dagre` (`mermaid-layout-unsupported`), each located at its
  configuration key with a hint.
- **`Layout(DiagramRenderContext)`** reports first, then maps the model with `GetData` and has a new `ClusterLayout`
  draw it through `RenderDiagram`, with a `LabelBuilder`, the `ErStyles` sheet of the configuration, the node image
  loader, the text width of the configured font family, size, and weight (`calculateTextWidth`, which the entity box
  uses), and a `LayoutWorkMeter` of the render; the frontmatter title is drawn centered above the drawing at the
  `er.titleTopMargin` (25 by default), and the view box is the drawn bounds grown by the padding of 8 of Mermaid's ER
  renderer.

#### Error Handling

- Mermaid's ER database raises no errors of its own. Every error of the model while the text is parsed is a limit, a
  `MermaidSyntaxException` with code `mermaid-limit-exceeded`, located in the original text, with a hint:
  - more entities (those only named after a subgraph included) and subgraphs together than `MaxNodes` (the message
    `the diagram has more than N entities and subgraphs (MermaidLimits.MaxNodes)`, checked by `CheckNodeCount`),
    located at the statement that names the entity one too many or at the `subgraph` keyword, as the flowchart model
    counts vertices and subgraphs;
  - more relationships than `MaxEdges` (the message `the diagram has more than N relationships
    (MermaidLimits.MaxEdges)`), located at the relationship statement;
  - a text that the model joins or derives from texts the parser checked apart, longer than `MaxLabelLength` (the
    message `a label is N characters long, more than the limit of M (MermaidLimits.MaxLabelLength)`, checked by
    `CheckLabelLength`): a text style, 2 characters longer than its style because `fill` is written `bgFill`, located at
    the `classDef` statement; the CSS classes of an entity (`default` and each class after a space) or of a subgraph
    (joined with spaces), checked as each class is added and located at the `class` statement or the statement with
    `:::`; and the compiled styles joined with semicolons, checked at completion and located at the last statement that
    gave the entity or subgraph classes (for an entity styled only by the `default` class, at its first mention); the
    inline styles of an entity or a subgraph and the styles and the text styles of a style class, joined with
    semicolons, checked as each style is added and located at the `style` or `classDef` statement. Repeated `class`,
    `style`, and `classDef` statements, ids listed again, and wide `classDef` lists can make these grow faster than the
    text, so they are checked where they are made. Every other text the model keeps was checked by the parser (see
    _ErParser Unit Design_).
- `ReportUnsupported` throws `MermaidUnsupportedFeatureException` through the sink at the first unsupported feature in
  strict mode.
- `GetData` throws a `MermaidRenderException` with code `mermaid-limit-exceeded` when subgraphs nest deeper than
  `MaxNestingDepth` through their parents (`subgraphs are nested deeper than N levels`), located at the first entity or
  subgraph one level too deep (the `subgraph` keyword, or the entity's first mention); a cycle of parents is left to
  the layout, which fails on it.
- `Layout` propagates the render errors of the shared rendering (`MermaidRenderException` for the layout work limit
  and the other render limits, `OperationCanceledException` for a canceled token). Among them are the two located
  `mermaid-render-failed` failures for texts on which Mermaid's own render fails too: an edge whose end is not drawn (a
  relationship side named like a subgraph that is declared after it took the id of an entity that `GetData` then
  skips), located at the relationship by the end check of the shared cluster layout; and a subgraph that holds itself
  (a relationship inside a subgraph to that subgraph's name makes the subgraph one of its own nodes), located at its
  `subgraph` keyword by the cycle check of the cluster layout, which sets the parents before it checks the edges, as
  Mermaid does.
- The public methods throw `ArgumentNullException` for null arguments.

#### Dependencies

- **Syntax subsystem** - `HtmlSanitizer` (Mermaid's `sanitizeText`), `Preprocessor.TrimJs` (JavaScript's `trim`), and
  the JavaScript white space class of `ScanPatterns`.
- **Configuration subsystem** - `MermaidConfig` (the look, the layout, the font, the raw settings, and the location of
  keys), `ErConfig` (the spacing and the title margin), `ErStyles` (the ER style sheet), `MermaidFontConfig`, and
  `ConfigScalar`.
- **Rendering subsystem** - `LayoutData`, `LayoutNode`, `LayoutEdge`, `LayoutErBox`, and `LayoutErAttribute` (the
  layout input), and `ClusterLayout`, `ShapeContext`, `LabelBuilder`, and `NodeImages` (the drawing).
- **Dagre subsystem** - `LayoutWorkMeter`, the work budget and cancellation of a render.
- **Scenegraph subsystem** - `Scene`, the result of a layout.
- **System** - `IMermaidDiagram`, `DiagramRenderContext`, `MermaidLimits`, `MermaidLocation`, `MermaidReportSink`,
  `MermaidSyntaxException`, and `MermaidDiagnosticCodes`.

#### Callers

- **ErParser** - creates the model, calls its database operations as the grammar's actions run, and completes it.
- The document pipeline of the system - reads the metadata and calls `Layout` for every measure and render.
- The unit and subsystem tests call `GetData` and `ReportUnsupported` directly and read the elements of parsed models,
  and the vector tests compare the facts and the layout data with Mermaid's.

#### Known Differences From Mermaid

- **Limits at open.** `MaxNodes` refuses more entities (those only named after a subgraph included) and subgraphs
  together, `MaxEdges` more relationships, and `MaxLabelLength` a longer text style, joined class list, or joined
  compiled, inline, or style class styles, when the document is opened, as a `MermaidSyntaxException` with code
  `mermaid-limit-exceeded`; subgraphs nested deeper than `MaxNestingDepth` through names are refused when the diagram
  is laid out, as a `MermaidRenderException` with the same code. Mermaid has no such limits.
- **Render failures are located.** A relationship side named like a later subgraph and a subgraph that holds itself
  fail the render as Mermaid's render fails on them, but as a located `MermaidRenderException` with code
  `mermaid-render-failed` and a hint, in place of Mermaid's JavaScript error.
- **Accessibility.** `accTitle` and `accDescr` are metadata only; a rendered picture has no SVG title or description.
- **Not modeled.** The color index of an entity is kept but not drawn: the `data-color-id` attribute that the
  `redux-color` themes paint by is not modeled, nor are the background of the hand-drawn look and the markers of the
  `neo` look.
- **Reports.** A look other than `classic` (such as the hand-drawn look) and a layout other than `dagre` (such as
  `elk`) are reported as unsupported and drawn in the classic look with dagre (see _CanvasNetMermaid System Design_,
  Fidelity and Known Differences).
