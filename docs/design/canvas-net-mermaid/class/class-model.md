### ClassModel

![Class Structure](MermaidClassView.svg)

The `ClassModel` unit is the second software unit in the `Class` subsystem. It comprises two source files in the
`DemaConsulting.CanvasNet.Mermaid.Class` namespace: `ClassModel.cs` (the database operations, the
metadata, the limits, and the element types) and `ClassModel.LayoutData.cs` (the mapping to the layout input of a
render, the reports of what is not drawn, and the layout of a render). `ClassModel` is an `internal sealed partial`
class, so the two files are one type, and it implements the system's `IMermaidDiagram`. `ClassModel.cs` also defines
the sealed classes `ClassNode`, `ClassMember`, `ClassNote`, `ClassNamespace`, and `ClassStyleClass`, and the sealed
records `ClassRelation`, `ClassRelationInput`, `ClassRelationType`, and `ClassInterface`. Both files carry the
provenance header comment for the parts of Mermaid they port.

#### Purpose

`ClassModel` is Mermaid's class database (`classDb.ts`, with `ClassMember` of `classTypes.ts`) for one parsed text: it
applies the operations that the parser calls with Mermaid's semantics, gives the document the metadata of the diagram,
maps the diagram for each render to the layout data that Mermaid's `getData` gives its renderer, reports what this
version does not draw as Mermaid does, and has the shared cluster layout draw the diagram in the frame of Mermaid's
class renderer (`classRenderer-v3-unified.ts`). It exists so that the meaning of a class diagram is decided in one
place that can be compared with Mermaid's database and layout data fact by fact, independently of the grammar and of
the drawing.

The unit's behavior is specified by the `CanvasNetMermaid-Class-ClassModel-*` requirements in
`docs/reqstream/canvas-net-mermaid/class/class-model.yaml`.

#### Data Model

**The constants**: the relation codes of Mermaid's `relationType` (`Aggregation` 0, `Extension` 1, `Composition` 2,
`Dependency` 3, and `Lollipop` 4) and the line codes of its `lineType` (`SolidLine` 0 and `DottedLine` 1).

**The database** (filled by one parser on one thread, read only afterwards and then safe for concurrent renders):

- the **classes** in insertion order and by id (`ClassNode`: the id, the generic type of the name, the label, the text
  drawn as the name, the CSS classes starting with `default`, the attributes and methods as `ClassMember`, the
  annotations, the styles, the DOM id `classId-Name-N`, the qualified id of the namespace that declares it, the checked
  link, the link target, the callback flag, the tooltip, and the location of its first mention);
- the **relations** in order (`ClassRelation`: the two class or interface ids, the two cardinalities with `none` for
  none, the label, the ends and line as `ClassRelationType`, and the location of the statement);
- the **notes** (`ClassNote`: the id `noteN`, the class name as written or null for a free note, the text as written,
  the index, the declaring namespace, and the location) and the **lollipop interfaces** (`ClassInterface`: the id
  `interfaceN`, the interface name, the class on the other side, and the location of the relation);
- the **namespaces** in insertion order and by qualified id (`ClassNamespace`: the qualified id, the label, the DOM id,
  the parent, whether it was declared or only implied by a dotted id, its classes, notes, and child namespaces each
  kept once in first-insertion order, and the location of its first declaration), and the stack of the qualified ids of
  the namespaces being declared;
- the **style classes** of `classDef` in insertion order and by name (`ClassStyleClass`: the styles, and as text styles
  those that mention `color`, with the first `fill` written `bgFill`);
- the counters of the class and namespace DOM ids (Mermaid's `classCounter`, numbered per document from 0, as in a page
  that draws one diagram), the direction (the last `direction` statement, else `TB`), the accessible title and
  description, Mermaid's id of the diagram (`classDiagram` or the legacy `class`, its `diag.type`), the location of the
  diagram keyword, and `HierarchicalNamespaces`, the text's own `class.hierarchicalNamespaces` setting.

**`ClassMember`** is immutable: the name, the member type (`method` or `attribute`), the visibility (`+`, `-`, `#`, `~`,
or empty), the classifier (`*`, `$`, or empty), the parameters and return type of a method, and `Text`, the text the
class box draws (an escaped visibility, generics in angle brackets, and the angle brackets as entities). `DisplayText`
gives Mermaid's display text (visibility, name, parameters, and return type with generics in angle brackets), and
`CssStyle` the style of the classifier (`font-style:italic;` for `*`, `text-decoration:underline;` for `$`).

#### Key Methods

- **Classes.** `AddClass` adds a class unless it exists: a name with a generic type (`Name~T~`) is split into the name
  and the type (`SplitClassNameAndType`, which sanitizes both), the drawn text is `Name&lt;T&gt;`, and the DOM id is
  numbered by the class counter. `SetClassLabel` sets the label of `class X["label"]` and the drawn text
  `label<T>` with raw angle brackets, Mermaid's own inconsistency kept as it is.
- **Members.** `AddMember` adds the class first, then reads the trimmed text as Mermaid does: `<<x>>` is an annotation,
  a text with a `)` after its first character is a method, and any other text that is not empty is an attribute;
  `AddMembers` adds the lines of a body in order. `ClassMember.Create` sanitizes the text and splits it: a method with
  Mermaid's expression `([#+~-])?(.+)\((.*)\)([\s$*])?(.*)([$*])?`, matched as JavaScript matches it (the visibility,
  the name up to the last `(` before the last `)`, the parameters, a classifier or a space, and the return type, whose
  last character is the classifier when none came before it), without a regular expression; an attribute takes its
  visibility from its first character and its classifier from its last. `ParseGenericTypes` writes generics in
  TypeScript syntax as Mermaid's `parseGenericTypes` does (matching tildes as angle brackets, outermost first, with the
  parts of `~K, V~` joined back across the comma).
- **Annotations.** `AddAnnotation` adds an annotation to a class that must exist; only the first one is drawn.
- **Relations.** `AddRelation` checks `MaxEdges`, then adds both classes, except that a lollipop end with no end at the
  other side adds an interface node (`AddInterface`) in place of its class; the cardinalities are trimmed and sanitized.
- **Notes.** `AddNote` adds a note for a class (which need not exist) or a free note and returns its id.
- **Namespaces.** `AddNamespace` qualifies the name by the enclosing namespace, checks its depth (`CheckDepth`: the
  parts of the qualified id against `MaxNestingDepth`), creates the implicit ancestors of a dotted id outermost first
  (each labeled with its last part and linked to its parent), makes a namespace declared again explicit and may
  relabel it, enters it, and returns the qualified id; `PopNamespace` leaves it, and
  `AddClassesToNamespace` puts the classes and notes declared in it into it and sets their parent.
- **Styles.** `DefineClass` keeps each style class and gives its styles, split at commas, to the classes whose CSS
  classes already contain its name (a substring match, as in Mermaid); `SetCssClass` adds a CSS class to the classes
  named in a comma-separated list, and `SetCssStyle` adds styles, split at commas, to a class, both only to classes
  that exist; a name in `SetCssClass` or `SetLink` that starts with a digit first gets the `classId-` prefix of
  Mermaid's DOM-id lookup, so it matches no class. As in Mermaid, a name listed again gets the CSS class or the styles
  again, so the model keeps the running length of the CSS classes of each class (joined with spaces), of the styles of
  each class (`StylesLength`, joined with semicolons), and of the styles and, apart, the text styles of each style
  class, and checks each against `MaxLabelLength` as it grows.
- **Links.** `SetLink` records the checked link and the target (`_blank` when none is given) and `SetClickEvent` the
  callback flag on the classes that exist, and both add the `clickable` CSS class; `SetTooltip` records a sanitized
  tooltip. The link kept is the result of `UrlSanitizer.FormatUrl` of the `Syntax` subsystem (Mermaid's `formatUrl`
  at the strict security level, an exact port of sanitize-url's `sanitizeUrl`): no link when the trimmed link is
  empty, `about:blank` for a `javascript`, `data`, or `vbscript` link in any encoding, and otherwise the link as
  sanitize-url writes it. The link as written is never kept.
- **Texts.** `CleanupLabel` removes the leading colon and the white space of a label and sanitizes it;
  `SetAccessibleTitle` and `SetAccessibleDescription` sanitize the texts as Mermaid's `commonDb.ts` does (the title
  without leading white space, the description without the white space after each line feed). Texts are sanitized
  with `HtmlSanitizer` where Mermaid sanitizes them, an empty text staying as it is.
- **Metadata** (`IMermaidDiagram`): `Title` is always null (the class grammar has no title statement, so the document
  falls back to the frontmatter title), `AccTitle` the accessible title or null, `Direction` the direction,
  `NodeCount` the classes, `EdgeCount` the relations, and `SubgraphCount` every namespace, implicit ancestors included,
  or with `HierarchicalNamespaces` false only the declared ones, as `GetData` keeps them.
- **`GetData(MermaidConfig)`** maps the model under the configuration of a render to `LayoutData`, in Mermaid's order:
  the namespaces as `rect` clusters with the padding `class.padding` or 16 (with `class.hierarchicalNamespaces` false,
  only the declared ones, labeled with their qualified id and without a parent); the classes as `classBox` nodes with
  their DOM ids, CSS classes, parent (with the setting false, the nearest declared namespace,
  `ResolveExplicitAncestor`), and a `LayoutClassBox` of the drawn name, the annotations, the attributes and methods with
  their texts and classifier styles, and the styles; the notes as `note` nodes with the padding `class.padding` or 6,
  the markdown label, and the four styles Mermaid's class diagram writes (`text-align: left`, `white-space: nowrap`, and
  the fill and stroke of the theme's `noteBkgColor` and `noteBorderColor` as text, a number or a boolean stringified
  as Mermaid's template strings do and a missing one as `undefined`, marked as trusted), each with a dotted edge
  `edgeNoteN` to its class only when the class exists; the lollipop interfaces as invisible `rect` nodes
  (`opacity: 0;`, trusted); and the relations as edges `id_A_B_N` numbered from 1, with the markdown label, the
  markers of their ends (`aggregation`, `extension`, `composition`, `dependency`, `lollipop`, or `none`), the
  cardinalities as the terminal labels `StartLabelRight` and `EndLabelLeft` (`none` as no label), and a solid or dashed
  line. Every edge has a minimum length of 1 (Mermaid's class edges have no `minlen`, so dagre's default applies). The
  data carries Mermaid's id of the diagram, the direction, the five class markers, and the node and rank spacing that
  Mermaid's class renderer reads from the state section (`ClassConfig`). A class named like a namespace shares the
  namespace's node, so it nests that namespace in the class's own; chained, such names nest namespaces without any
  nesting in the text, so `GetData` checks the nesting the parents make with `LayoutData.CheckNestingDepth`
  (`namespaces`, `MaxNestingDepth`).
- **`ReportUnsupported(MermaidConfig, MermaidReportSink)`** reports, in order, a look other than `classic`
  (`mermaid-look-unsupported`) and a layout other than `dagre` (`mermaid-layout-unsupported`), each located at its
  configuration key with a hint.
- **`Layout(DiagramRenderContext)`** reports first, then maps the model with `GetData` and has a new `ClusterLayout`
  draw it through `RenderDiagram`, with a `LabelBuilder`, the `ClassStyles` sheet of the configuration, the node image
  loader, the text width of the configured font (`calculateTextWidth`, for the wrapping width of the class box texts),
  and a `LayoutWorkMeter` of the render; the frontmatter title is drawn centered above the drawing at the state
  section's title margin, and the view box is the drawn bounds grown by the padding of 8 of Mermaid's class renderer.

#### Error Handling

- Every error while the text is parsed is a `MermaidSyntaxException` located at the statement in the original text,
  with a hint:
  - Mermaid's database errors (code `mermaid-syntax-error`), with Mermaid's JavaScript message after the location: an
    annotation of a class that does not exist ("Cannot read properties of undefined (reading 'annotations')") and a
    member with a `)` that Mermaid's method expression cannot read ("... (reading 'startsWith')");
  - more relations than `MaxEdges` (`mermaid-limit-exceeded`, at the relation statement);
  - more classes, notes, lollipop interfaces, and namespaces (implicit dotted ancestors included, whether they are
    drawn or not) together than `MaxNodes` (`mermaid-limit-exceeded`, at the statement or name that adds one too many,
    with the flowchart's message `more than N nodes and subgraphs`), as the flowchart model counts vertices and
    subgraphs; a namespace declared again is not counted again;
  - a drawn text that the model joins from texts the parser checked apart, longer than `MaxLabelLength`
    (`mermaid-limit-exceeded`, with the flowchart's label message `a label is N characters long`, checked by
    `CheckLabel`): the drawn name `label<T>` that `SetClassLabel` joins from the label and the generic type of the name,
    located at the class name, and the qualified id that `AddNamespace` joins from the names of the enclosing
    namespaces, located at the namespace name. The qualified id is the drawn label when `class.hierarchicalNamespaces`
    is false, which the site configuration may set after the text is parsed, so it is checked in both modes. The CSS
    classes of a class (joined with spaces), its styles, and the styles of a style class (joined with semicolons) are
    checked as they grow (`CheckLabelLength`), located at the `cssClass`, `classDef`, `style`, class, or click
    statement that makes them too long, because a name listed again repeats them as in Mermaid. Every other text the
    model keeps was checked by the parser (see _ClassParser Unit Design_);
  - a namespace whose qualified id has more parts than `MaxNestingDepth` (`mermaid-limit-exceeded`, located at the
    namespace name, with the parser's message `namespaces are nested deeper than N levels`, checked by `CheckDepth`):
    the parts of nested `namespace` keywords and of the dots in a name count together, because with
    `class.hierarchicalNamespaces` (the default) each part is a nested cluster that the shared rendering lays out
    recursively. The parser counts only the keywords, so without this check one dotted name could nest the clusters
    without bound and exhaust the stack. It is checked in both modes, as the setting may change after parsing.
- `ReportUnsupported` throws `MermaidUnsupportedFeatureException` through the sink at the first unsupported feature in
  strict mode.
- `GetData` throws a `MermaidRenderException` with code `mermaid-limit-exceeded` when the namespaces nest deeper than
  `MaxNestingDepth` through class names (`namespaces are nested deeper than N levels`), located at the class statement
  that nests the namespace one level too deep; a cycle of such names is left to the layout, which fails on it.
- `Layout` propagates the render errors of the shared rendering (`MermaidRenderException` for the layout work limit
  and the other render limits, `OperationCanceledException` for a canceled token); the style warnings of a class,
  reported by the class box shape, are located at the class's first mention.
- The public methods throw `ArgumentNullException` for null arguments.

#### Dependencies

- **Syntax subsystem** - `HtmlSanitizer` (Mermaid's `sanitizeText`), `UrlSanitizer` (the link check),
  `Preprocessor.TrimJs`, and the JavaScript character classes of `ScanPatterns`.
- **Configuration subsystem** - `MermaidConfig` (the look, the layout, the theme variables, the font, and the location
  of keys), `ClassConfig` (the namespace mode, the padding, the spacing, and the title margin), `ClassStyles` (the
  class style sheet), `MermaidFontConfig`, and `ConfigScalar`.
- **Rendering subsystem** - `LayoutData`, `LayoutNode`, `LayoutEdge`, `LayoutClassBox`, and `LayoutClassMember` (the
  layout input), and `ClusterLayout`, `ShapeContext`, `LabelBuilder`, and `NodeImages` (the drawing).
- **Dagre subsystem** - `LayoutWorkMeter`, the work budget and cancellation of a render.
- **Scenegraph subsystem** - `Scene`, the result of a layout.
- **System** - `IMermaidDiagram`, `DiagramRenderContext`, `MermaidLimits`, `MermaidLocation`, `MermaidReportSink`,
  `MermaidSyntaxException`, and `MermaidDiagnosticCodes`.

#### Callers

- **ClassParser** - creates the model and calls its database operations as the grammar's actions run.
- The document pipeline of the system - reads the metadata and calls `Layout` for every measure and render.
- The unit and subsystem tests call the database operations, `ParseGenericTypes`, `GetData`, and
  `ReportUnsupported` directly, and the vector tests compare the facts and the layout data with Mermaid's.

#### Known Differences From Mermaid

- **Database errors are located.** Where Mermaid's database throws a JavaScript error, the model raises a located
  `MermaidSyntaxException` with Mermaid's message after the location and code, and a hint.
- **Limits at open.** `MaxEdges` refuses more relations, `MaxNodes` more classes, notes, interfaces, and namespaces
  together, `MaxLabelLength` a longer joined label or qualified namespace id and longer CSS classes or styles of a
  class or style class, and `MaxNestingDepth` a qualified namespace id of more parts (keywords and dots together), when
  the document is opened, as a `MermaidSyntaxException` with code `mermaid-limit-exceeded`; namespaces nested deeper
  through class names are refused when the diagram is laid out, as a `MermaidRenderException` with the same code.
  Mermaid has no such limits.
- **Interaction.** The link, target, tooltip, and callback of a class are kept only as data; they are never drawn, no
  function is ever bound, and the `clickable` CSS class changes no paint.
- **Security level.** Links are always checked as Mermaid's strict security level checks them, the only level
  modeled; the `loose` level (links kept as written) and the `sandbox` level (the `_top` target) are not. The
  differences of the shared link check itself are listed in the SourceScanner unit design.
- **Accessibility.** `accTitle` and `accDescr` are metadata only; a rendered picture has no SVG title or description.
- **Report locations.** The style warnings of a class are located at the class's first mention, not at the `style` or
  `classDef` statement, because the layout data carries no style locations.
- **Math.** KaTeX math in class texts is not detected (Mermaid's `hasKatex`); `$$` is drawn as text and not reported.
- **Generic texts in SVG labels.** Mermaid's SVG fix-up of a class text that contains `&gt` (its shape utility rewrites
  the first text span) is not ported; the shared rendering decodes the entities, so a one-line text gives the same
  characters.
- **Reports.** A look other than `classic` and a layout other than `dagre` are reported as unsupported and drawn in the
  classic look with dagre (see _CanvasNetMermaid System Design_, Fidelity and Known Differences).
