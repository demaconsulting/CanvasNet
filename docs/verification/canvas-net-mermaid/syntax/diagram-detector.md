### DiagramDetector Unit Verification Design

This document describes the unit-level verification strategy for the `DiagramDetector` unit in the
`DemaConsulting.CanvasNet.Mermaid.Syntax` namespace: the internal static `DiagramDetector` class (`Detect`,
`ReplaceComments`, `UnsupportedHint`, `SupportedDiagramKeywords`, and `DetectorIds`) with `DetectorSettings`,
`DetectedDiagram`, and `DiagramSupport`.

#### Verification Approach

The unit is verified by detecting short texts and comparing the detector id, the public type, the support, and the
location with the values of Mermaid's `detectType` for the same texts with the same `defaultRenderer` settings. One
case per known keyword covers every detector, including the newer types and the case-insensitive ones; the
detection order is checked against Mermaid's registration order; and the quirks of Mermaid's expressions are
asserted explicitly, so that a "correction" would fail the tests. The errors are checked for their code, message,
hint, and location. The texts are wrapped in identity location maps with the shared test helper; no mocking or
stubbing is required, and the internal types are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `DiagramDetectorTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Syntax` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of in-memory text; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any text detected as another type than Mermaid's, or any error with another code, hint, or location than
expected, constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Syntax-DiagramDetector-KnownTypes: Every Mermaid Type Is Known With Its Support

**Tests**: `DiagramDetectorTests.DiagramDetector_Detect_KnownKeyword_GivesTypeAndSupport`,
`DiagramDetectorTests.DiagramDetector_DetectorIds_FollowMermaidRegistrationOrder`

Asserts the detector id, public type, and support of 39 keywords (from `flowchart` and `graph` to `cynefin-beta:` and
`Error`), including `classDiagram` and `classDiagram-v2` as the supported `class` type, `stateDiagram` and
`stateDiagram-v2` as the supported `state` type (Mermaid's `stateDiagram` detector), `erDiagram` as the supported `er`
type, and `gantt` and `pie showData` as the supported `gantt` and `pie` types, while `mindmap` stays planned, and that
the detector table has all 40 detectors.

##### CanvasNetMermaid-Syntax-DiagramDetector-MermaidOrder: Detection Follows Mermaid's Order And Quirks

**Tests**: `DiagramDetectorTests.DiagramDetector_DetectorIds_FollowMermaidRegistrationOrder`,
`DiagramDetectorTests.DiagramDetector_Detect_MermaidQuirks_AreKept`

Asserts that the first nine detectors are `error`, `---`, the large features, `c4`, `kanban`, and the two class
detectors, that `sequence` precedes `flowchart-v2`, and that `cynefin` is last; and that a flowchart that mentions
`C4Container` is a C4 diagram, `graphs` is a flowchart, `pieces` is a pie, `ishikawa-beta_x` is an Ishikawa diagram,
while `ishikawa_x`, `swimlane-beta_x`, and `cynefin-beta_x` are not diagrams.

##### CanvasNetMermaid-Syntax-DiagramDetector-RendererSettings: Renderer Settings Choose The Variant

**Tests**: `DiagramDetectorTests.DiagramDetector_Detect_RendererSettings_ChooseVariant`

Asserts that the `elk` flowchart renderer makes `graph` an ELK flowchart, that the legacy `dagre-d3` renderers make
`graph` the legacy flowchart and `classDiagram` and `stateDiagram` the legacy class and state types (the legacy state
type supported, as it is drawn), that
`stateDiagram-v2` stays the current state type, and that `flowchart` with the legacy renderer is not detected, as in
Mermaid.

##### CanvasNetMermaid-Syntax-DiagramDetector-Cleanup: Comments And Directives Before The Keyword Are Ignored

**Tests**: `DiagramDetectorTests.DiagramDetector_Detect_CleanupBeforeDetection_SkipsCommentsAndDirectives`,
`DiagramDetectorTests.DiagramDetector_ReplaceComments_FollowsAnyCommentExpression`

Asserts that a directive and an indented comment before `sequenceDiagram` are skipped and the keyword is located at
line 3, column 3, and that the comment replacement turns the white space before a comment, the comment, and its line
feed into one line feed while a comment without a final line feed stays.

##### CanvasNetMermaid-Syntax-DiagramDetector-UnknownDiagram: An Unknown Text Is Refused With The Nearest Keyword

**Tests**: `DiagramDetectorTests.DiagramDetector_Detect_Unknown_ThrowsWithNearestKeyword`,
`DiagramDetectorTests.DiagramDetector_Detect_Typo_HintsNearestKeyword`,
`DiagramDetectorTests.DiagramDetector_Detect_DashDashDash_ThrowsMermaidMessage`

Asserts that `hello world` is `mermaid-unknown-diagram` quoting `hello` without a suggestion, that a blank text says
that it has no keyword, that `SequenceDiagram` gets the hint "did you mean 'sequenceDiagram'? ..." at line 1,
column 1, and that a text that starts with `---` is `mermaid-syntax-error` with Mermaid's message.

##### CanvasNetMermaid-Syntax-DiagramDetector-UnsupportedHint: Unsupported Types Get An Unsupported Hint

**Tests**: `DiagramDetectorTests.DiagramDetector_UnsupportedHint_NamesSupportedKeywords`

Asserts the exact hints for two types that are not drawn (`journey`: "journey diagrams are not supported; supported in
this version: flowchart, graph, sequenceDiagram, classDiagram, stateDiagram, erDiagram, pie, gantt, mindmap", and
`gitGraph` with the same list), and that the supported keywords are exactly `flowchart`, `graph`, `sequenceDiagram`,
`classDiagram`, `stateDiagram`, `erDiagram`, `pie`, `gantt`, and `mindmap`, in that order.
