### DiagramDetector

![Syntax Structure](MermaidSyntaxView.svg)

The `DiagramDetector` unit is the second software unit in the `Syntax` subsystem. It is one source file,
`DiagramDetector.cs`, in the `DemaConsulting.CanvasNet.Mermaid.Syntax` namespace, and defines the internal static
class `DiagramDetector`, the enumeration `DiagramSupport`, the records `DetectorSettings` and `DetectedDiagram`, and
the private record of one known diagram type.

#### Purpose

`DiagramDetector` decides which Mermaid diagram type a text is, as Mermaid's `detectType` does: it applies Mermaid's
clean-up before detection and tries Mermaid's detectors in Mermaid's registration order, the first that matches
winning. Every diagram type that the pinned Mermaid release registers is known, so a text that Mermaid draws is never
an unknown diagram; each type carries whether this version draws it, so that the document can report a type that is
not drawn instead of failing. A text of no known type is refused with a located error that names the nearest
keyword.

The unit's behavior is specified by the `CanvasNetMermaid-Syntax-DiagramDetector-*` requirements in
`docs/reqstream/canvas-net-mermaid/syntax/diagram-detector.yaml`.

#### Data Model

- `DiagramSupport` - `Supported` (drawn: `flowchart`, `sequence`, `class`, `state`, `er`, `pie`, `gantt`, and
  `mindmap`) and `Unsupported` (every other type).
- `DetectorSettings(FlowchartRenderer, ClassRenderer, StateRenderer)` - the `defaultRenderer` values that Mermaid's
  detectors read; `DetectorSettings.Default` is Mermaid's default (`dagre-wrapper` for all three).
- `DetectedDiagram(MermaidId, DiagramType, Keyword, Support, Location)` - Mermaid's detector id (for example
  `flowchart-v2`), the public type name of `MermaidDocument.DiagramType`, the first word as written, the support, and
  the location of the keyword; `IsSupported` is true for a drawn type.
- The table of the 40 known detectors, in Mermaid's order: `error`, `---`, then the large features `flowchart-elk`,
  `mindmap`, and `architecture`, then `c4`, `kanban`, `classDiagram`, `class`, `er`, `gantt`, `info`, `pie`,
  `requirement`, `sequence`, `swimlane`, `flowchart-v2`, `flowchart`, `timeline`, `gitGraph`, `stateDiagram`,
  `state`, `journey`, `quadrantChart`, `sankey`, `packet`, `xychart`, `block`, `eventmodeling`, `treeView`, `radar`,
  `ishikawa`, `treemap`, the four railroad detectors, `venn`, `wardley`, and `cynefin`. The public type name is
  Mermaid's id, except that `classDiagram` and `class` are `class`, `stateDiagram` and `state` are `state`, and
  `flowchart-v2`, `flowchart`, and `flowchart-elk` are `flowchart`. Both class detectors are supported: `classDiagram`
  (a `classDiagram` text with the default `dagre-wrapper` class renderer, or any `classDiagram-v2` text) and the legacy
  `class` (a `classDiagram` text with another class renderer), so `classDiagram`, `classDiagram-v2`, and the legacy
  `class` id are all drawn. Both state detectors are supported in the same way: `stateDiagram` (any
  `stateDiagram-v2` text, or a `stateDiagram` text with the default `dagre-wrapper` state renderer) and the legacy
  `state` (a `stateDiagram` text with another state renderer), so `stateDiagram`, `stateDiagram-v2`, and the legacy
  `state` id are all drawn; the `er` (`erDiagram`), `pie`, `gantt`, and `mindmap` detectors are supported too.
- The keywords offered by the nearest-keyword hint (the common diagram keywords) and the supported keywords
  (`flowchart`, `graph`, `sequenceDiagram`, `classDiagram`, `stateDiagram`, `erDiagram`, `pie`, `gantt`, and
  `mindmap`).

The unit has no mutable state and is safe for concurrent use.

#### Key Methods

- `Detect(MappedText code, DetectorSettings settings)` - removes a further frontmatter block and the directives,
  replaces every comment with a line feed (`ReplaceComments`), finds the keyword (the first word after the leading
  white space, up to white space or `;`) and its location, and returns the first known type whose detector matches.
- `ReplaceComments(MappedText)` - `detectType`'s comment expression (`/\s*%%.*\n/gm`) as a scanner: the white space
  before a comment, the comment, and its line feed become one line feed; a comment without a final line feed stays.
- `UnsupportedHint(DetectedDiagram)` - "`<type>` diagrams are not supported; supported in this version: flowchart,
  graph, sequenceDiagram, classDiagram, stateDiagram, erDiagram, pie, gantt, mindmap".
- `SupportedDiagramKeywords` and `DetectorIds` - the supported keywords and the detector ids in detection order.
- `EditDistance(string, string)` - the Levenshtein distance used by the nearest-keyword hint (and by the nearest-name
  hints of other subsystems).

Each detector is a port of its expression: `/^\s*keyword/` (a prefix test after the leading white space),
`/^\s*keyword\b/` for the detectors with a word boundary, the `i` flag where Mermaid sets it, the special forms of
`cynefin-beta` and of the `---` pseudo-diagram, and the `c4` detector that also finds its later keywords anywhere in
the text. The variants of the flowchart, class, and state detectors read the `defaultRenderer` settings as Mermaid's
detectors do (for example `elk` selects `flowchart-elk` for `graph` and `flowchart`).

**Preconditions and postconditions**: the code is the `PreprocessedSource.Code` of the `Preprocessor`; the result's
location is in the original text.

#### Error Handling

- A null argument is an `ArgumentNullException`.
- A text that starts with `---` (a frontmatter that was not closed) is a `MermaidSyntaxException` with
  `mermaid-syntax-error` and Mermaid's own message ("Diagrams beginning with --- are not valid. ..."), with a hint to
  close the frontmatter.
- A text that no detector matches is a `MermaidSyntaxException` with `mermaid-unknown-diagram` located at the keyword;
  the message quotes the keyword (shortened when long) or says that the text has no keyword, and the hint names the
  nearest known keyword when its distance is at most the larger of 2 and a third of its length.

#### Dependencies

- **Preprocessor** (same subsystem) - `MatchFrontmatter`, `RemoveDirectives`, `TrimJs`, and `MappedText`
- **SourceScanner** (same subsystem) - the JavaScript character classes of `ScanPatterns`
- **Root namespace** - `MermaidLocation`, `MermaidSyntaxException`, and `MermaidDiagnosticCodes`
- **.NET base class library** - LINQ

#### Callers

`MermaidDocument` detects every text it opens, first with the default settings and again with the settings of the
text's configuration when they differ, and reads `UnsupportedHint` for the report of a type that is not drawn; the
`Configuration` subsystem (`MermaidConfigResolver`) detects with the resolved settings and uses `EditDistance`, as
does `ShapeNames` of the `Rendering` subsystem and `SequenceModel` of the `Sequence` subsystem (for the nearest
participant type). The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Syntax/DiagramDetectorTests.cs` call the unit directly through
`InternalsVisibleTo`.

#### Known Differences From Mermaid

- **More diagram types than are drawn, by design.** The detector knows all 40 detectors of the pinned Mermaid release,
  including the newer types (`eventmodeling`, `treeView`, `ishikawa`, the railroad types, `venn`, `wardley`,
  `cynefin`, and `swimlane`), so none of them is an unknown diagram; they are reported as not supported.
- **Public type names.** The detector ids that Mermaid uses for variants of one diagram are mapped to one public type
  name (`class`, `state`, and `flowchart`); Mermaid's own id stays available as `MermaidId`.
- **Quirks kept on purpose.** The `c4` detector matches `C4Container` and its sibling keywords anywhere in the text,
  detection uses keyword prefixes (so `graphs` is a flowchart and `pieces` a pie), and the word-boundary and
  case-insensitive flags of individual detectors are honored.
- **Errors instead of Mermaid's unknown-diagram exception.** A text of no known type is a located
  `mermaid-unknown-diagram` with a nearest-keyword hint, which Mermaid does not give.
