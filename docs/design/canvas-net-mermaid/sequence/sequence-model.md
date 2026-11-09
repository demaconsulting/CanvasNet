### SequenceModel

![Sequence Structure](MermaidSequenceView.svg)

The `SequenceModel` unit is the second software unit in the `Sequence` subsystem. It comprises four source files in
the `DemaConsulting.CanvasNet.Mermaid.Sequence` namespace: `SequenceModel.cs` (the database operations, the metadata,
the box colors, the links and properties, and the limits), `SequenceModel.Elements.cs` (the constants and the element
types), `SequenceModel.Statements.cs` (the statement records that the parser collects), and `SequenceModel.Render.cs`
(the diagram of the document: the reports of what is not drawn and the layout of a render). `SequenceModel` is an
`internal sealed partial` class, so the database and render files are one type, and it implements the system's
`IMermaidDiagram`. `SequenceModel.Elements.cs` defines the static constant classes `SequenceLineType`,
`SequencePlacement`, and `SequenceParticipantTypes`, the record struct `SequenceText`, the sealed classes
`SequenceActor`, `SequenceBox`, and `SequencePropertyMap`, and the sealed records `SequenceAutonumber`,
`SequenceMessage`, `SequenceNote`, and `SequenceBoxData`. `SequenceModel.Statements.cs` defines the abstract record
`SequenceStatement` and its sealed records for participants, `destroy`, single-participant signals, messages, notes,
the interaction statements (with the enumeration `SequenceActorDataKind`), box starts and ends, block markers, and
`autonumber`, and `SequenceMetadataText`. Every file carries the provenance header comment for the parts of Mermaid
it ports.

#### Purpose

`SequenceModel` is Mermaid's sequence database (`sequenceDb.ts`) for one parsed text: it applies the statements that
the parser collects with Mermaid's semantics, gives the document the metadata of the diagram, reports for each render
what this version does not draw as Mermaid does, and has the layout draw the diagram. It exists so that the meaning of
a sequence diagram is decided in one place that can be compared with Mermaid's database fact by fact, independently
of the grammar and of the drawing.

The unit's behavior is specified by the `CanvasNetMermaid-Sequence-SequenceModel-*` requirements in
`docs/reqstream/canvas-net-mermaid/sequence/sequence-model.yaml`.

#### Data Model

**The constants**: `SequenceLineType` holds Mermaid's `LINETYPE` values (the 26 arrows, `NOTE`, the start, section, and
end markers of the eight blocks, `ACTIVE_START`, `ACTIVE_END`, `AUTONUMBER`, and the central connections), with
`IsArrow` for the types drawn as arrows between participants; `SequencePlacement` holds Mermaid's `PLACEMENT` values
(left of, right of, and over); `SequenceParticipantTypes` holds the eight types Mermaid draws (`actor`, `boundary`,
`collections`, `control`, `database`, `entity`, `participant`, and `queue`), with `IsKnown` (case-sensitive, as
Mermaid's comparison).

**The database** (filled by one parser on one thread, read only afterwards and then safe for concurrent renders):

- the **participants** in insertion order and by id (`SequenceActor`: id, name, description and its wrap setting,
  type, box, previous and next participant, `Links` and `Properties` as `SequencePropertyMap`, and the location of
  the declaration);
- the **messages** in the order of the text (`SequenceMessage`: id, which is its index as text, from, to, text, wrap
  setting, line type, location, and for the kinds that have them the activation flag, the central connection, the
  note placement, and the `SequenceAutonumber` start, step, and visibility);
- the **notes** (`SequenceNote`: from, to, placement, text, and wrap setting) and the **boxes** (`SequenceBox`: title,
  fill, wrap setting, the keys of their participants, and location);
- the **created and destroyed participants**, each with the index of the first message after its statement;
- the diagram title (sanitized), the accessible title, the accessible description, the location of the diagram
  keyword, and the private state of Mermaid's database while it applies statements (the previous participant, the
  current box, and the last created and destroyed participants that still need their message).

**`SequenceText`** is a text with its explicit wrap setting, as Mermaid's `parseMessage` reads it. **`SequencePropertyMap`**
keeps the properties of a JavaScript object with JSON values, enumerated in JavaScript's order (keys that are array
indexes first, in numeric order, then the other keys in insertion order; a key set again keeps its place).

#### Key Methods

- **`Apply(IEnumerable<SequenceStatement>)`** applies the statements in order, as Mermaid's `apply` does:
  - **participants** (`AddActor`): the metadata of `@{ ... }` is read as YAML with the location map of the parser (a
    single line wrapped as a flow mapping, several lines as a block); its `type` replaces the keyword's unless it is
    null, and its `alias` applies only
    when the statement has no alias of its own. A participant declared again keeps its place and its box and is
    replaced only when the new declaration has an alias; without an alias or a type the description is the name. The
    previous participant's next participant is set, and the current box records the participant;
  - **`create`** (`ApplyCreate`) refuses an id that already exists and requires that the next message goes to the
    created participant; **`destroy`** requires that the next message comes from or goes to the destroyed one; both
    record the index of that message;
  - **messages** (`ApplyMessage`, `AddSignal`) add the signal with its line type, activation flag, and central
    connection; an `ACTIVE_END` of a participant that has no open activation is Mermaid's error;
  - **notes** (`AddNote`) add a note and a message of type `NOTE`;
  - **links**, **link**, **properties**, and **details** (`ApplyActorData`): the text is sanitized; `link` splits it
    at the first `@` (found before the entities are decoded, with JavaScript's `slice` when there is none) into a
    label and a link, `links` and `properties` read JSON as `JSON.parse` does and copy
    the enumerable properties of the value as a JavaScript `for...in` loop does (the keys of an object, the indexes of
    an array or a string), text that is not JSON is ignored as Mermaid only logs it, and `details` reads a page element
    that does not exist outside a browser, so it changes nothing;
  - **boxes**, **block markers**, and **`autonumber`** are added as Mermaid's `boxStart`, `boxEnd`, `addSignal`, and
    `sequenceIndex` do.
- **`ParseBoxData(string)`** reads the color and title of a `box` statement as Mermaid's `parseBoxData` does: a
  leading `rgb()`, `rgba()`, `hsl()`, or `hsla()` (up to the last closing parenthesis), or else a leading word, is the
  color when a browser accepts it as one (`IsBrowserColor`: a named color, `transparent`, a color keyword or system
  color, or one of the color functions without a space before its parenthesis); otherwise the color is `transparent`
  and the whole text is the title.
- **`SequenceText.Parse(string)`** trims a text, then takes a leading `wrap:` or `nowrap:` (optionally after a colon,
  case-sensitive) as the wrap setting and trims the rest; **`EffectiveWrap(bool?, SequenceConfig)`** gives the
  explicit setting, else the configuration's (Mermaid's `autoWrap`).
- **Title and accessibility** (`SetDiagramTitle`, `SetAccessibleTitle`, `SetAccessibleDescription`) sanitize the
  texts as Mermaid's `commonDb.ts` does (the title not trimmed, the accessible title without leading white space, the
  description without the white space after each line feed).
- **Metadata** (`IMermaidDiagram`): `Title` is the title of the `title` statement or null (so the document falls back
  to the frontmatter title), `AccTitle` the accessible title or null, `Direction` null, `NodeCount` the participants
  (declared, mentioned, and created), `EdgeCount` the messages drawn as arrows, and `SubgraphCount` the boxes.
  `HasAtLeastOneBox` and `HasAtLeastOneBoxWithTitle` are Mermaid's helpers of the same names.
- **`Layout(DiagramRenderContext)`** reports first and then has a new `SequenceLayout` of the render draw the scene.
  **`ReportUnsupported`** reports, in order, a look other than `classic` (`mermaid-look-unsupported`), the `fo` text
  placement (the warning `mermaid-config-invalid` at the `sequence` key), and then for each participant a type that
  Mermaid does not define (the warning `mermaid-participant-type-unknown`, whose hint names the nearest of the eight
  types by edit distance), a `properties` icon of a `participant`, `collections`, or `queue` that is text and does not
  start with `@` (`mermaid-icon-unsupported`), and math in its description; then math in the texts of the messages,
  notes, and block and section titles. **`HasKatex(string)`** is Mermaid's `hasKatex`: two dollar signs, then two more
  on the same line (`mermaid-math-unsupported`).

#### Error Handling

- Every error is a `MermaidSyntaxException` raised while the statements are applied, located at the statement in the
  original text, with a hint that names the participant:
  - Mermaid's database errors, with Mermaid's message (code `mermaid-syntax-error`): a participant in two boxes, a
    created participant whose next message does not create it, a destroyed participant whose next message does not
    use it, a `create` of an id that already exists, and the deactivation of a participant that is not active;
  - participant metadata that is not valid YAML or uses YAML that the reader does not support (`mermaid-yaml-invalid`
    and `mermaid-yaml-unsupported` of the YAML reader of the `Syntax` subsystem), or that nests deeper than
    `MaxYamlDepth` (`mermaid-limit-exceeded`);
  - more participants than `MaxNodes` (`mermaid-limit-exceeded`, at the statement that adds one too many).
- `ReportUnsupported` throws `MermaidUnsupportedFeatureException` through the sink at the first unsupported feature
  in strict mode; the warnings never throw.
- `Layout` propagates the render errors of the layout and the renderer (`MermaidRenderException` for the layout work
  limit and for the texts that Mermaid cannot draw, `OperationCanceledException` for a canceled token).
- The public methods throw `ArgumentNullException` for null arguments.

#### Dependencies

- **SequenceLayout** - draws the scene of a render.
- **SequenceRenderer** - `IsTruthy`, the JavaScript truthiness of a property value for the icon report.
- **Syntax subsystem** - `YamlSubsetParser` and its value types (the participant metadata), `HtmlSanitizer`
  (Mermaid's `sanitizeText`), `Preprocessor.TrimJs`, and `DiagramDetector.EditDistance` (the nearest type).
- **Configuration subsystem** - `MermaidConfig` (the look, the location of keys) and `SequenceConfig` (the text
  placement and the wrap setting).
- **Scenegraph subsystem** - `Scene`, the result of a layout.
- **Core `CanvasNet`** - `CssColor` (the browser's color check of a box), through the public API.
- **System** - `IMermaidDiagram`, `DiagramRenderContext`, `MermaidLimits`, `MermaidLocation`, `MermaidReportSink`,
  `MermaidSyntaxException`, and `MermaidDiagnosticCodes`.
- **.NET base class library** - `System.Text.Json` (the links and properties).

#### Callers

- **SequenceParser** - creates the model, sets the title and accessibility texts, and applies the collected
  statements.
- **SequenceLayout** - reads the participants, messages, notes, boxes, and created and destroyed participants, and
  `EffectiveWrap`.
- The document pipeline of the system - reads the metadata and calls `Layout` for every measure and render.
- The unit and subsystem tests call `ParseBoxData`, `SequenceText.Parse`, `EffectiveWrap`, `HasKatex`, and the
  property map directly.

#### Known Differences From Mermaid

- **Database errors are located.** Where Mermaid's database throws with its message, the model raises a located
  `MermaidSyntaxException` with Mermaid's message after the location and code, and a hint that names the participant.
- **Limits at open.** `MaxNodes` refuses more participants when the document is opened, as a `MermaidSyntaxException`
  with code `mermaid-limit-exceeded`; Mermaid has no participant limit.
- **The browser's color check.** `CSS.supports('color', ...)` is emulated with the colors that the core color parser
  reads, the color keywords and system colors, and Mermaid's color functions.
- **Interaction.** Links, link, properties, and details are kept as data; `details` reads nothing, since there is no
  page to read from.
- **Accessibility.** `accTitle` and `accDescr` are metadata only; a rendered picture has no SVG title or description.
- **Reports.** A look other than `classic`, a `properties` icon that names an image, and `$$` math are reported as
  unsupported, and a participant of an unknown type and the `fo` text placement as warnings (see _CanvasNetMermaid
  System Design_, Fidelity and Known Differences).
