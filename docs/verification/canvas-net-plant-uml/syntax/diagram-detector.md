### DiagramDetector Unit Verification Design

This document describes the unit-level verification strategy for the `DiagramDetector` unit in the
`DemaConsulting.CanvasNet.PlantUml.Syntax` namespace: the internal static `DiagramDetector` class and its result
record `DetectedDiagram`.

#### Verification Approach

The unit is verified by detecting the type of small original texts after the `Preprocessor` unit of the same
subsystem (used as is, because the detector reads its result) and comparing the type, the support flag, the keyword,
the location, and the errors with literal values. No mocking is required; the internal types are reached through the
test assembly's `InternalsVisibleTo` access. Unit tests reside in `DiagramDetectorTests.cs` within the
`DemaConsulting.CanvasNet.PlantUml.Tests` project, in the `Syntax` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of in-memory text; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A type, a flag, a keyword, a location, or a code that differs from the expected literal constitutes a
failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Syntax-DiagramDetector-Tags: The Tag Names The Type

**Tests**: `DiagramDetectorTests.PlantUmlDiagramDetector_Detect_OtherTag_NamesType`,
`DiagramDetectorTests.PlantUmlDiagramDetector_Detect_UnknownTag_ThrowsWithNearestTag`,
`DiagramDetectorTests.PlantUmlDiagramDetector_EditDistance_CountsEdits`

Asserts that `@startmindmap` is a mindmap that is not drawn, that `@startMindMaps` is a `plantuml-unknown-diagram`
error at its line whose hint names `@startmindmap`, and the edit distance of equal, one-letter, and empty words.

##### CanvasNetPlantUml-Syntax-DiagramDetector-FactoryOrder: Sequence First, Then Activity

**Tests**: `DiagramDetectorTests.PlantUmlDiagramDetector_Detect_SequenceStatements_IsSequence`,
`DiagramDetectorTests.PlantUmlDiagramDetector_Detect_ActivityStatements_IsActivity`,
`DiagramDetectorTests.PlantUmlDiagramDetector_BlockClosers_AreRecognized`,
`DiagramDetectorTests.PlantUmlDiagramDetector_Detect_BorderMessages_IsSequence`,
`DiagramDetectorTests.PlantUmlDiagramDetector_IsBorderMessage_ReadsTheBorderEnds`

Asserts that a diagram with a title, an actor, messages, a group, a multi-line note whose text starts with `class`, a
divider, a delay, and a space is a sequence diagram recognized by `actor` at line 3, and that a diagram with start, an
action, a condition, a connector, and stop is an activity diagram; asserts the closer of a legend with an alignment,
`end legend` and `endlegend` closing it and `end legends` not, the block of an aligned footer alone on its line, no
block for an aligned header with text, an aligned header as a common statement, and `endbox` as a sequence statement.
Asserts that diagrams with messages from and to the border (`[->`, `[o->`, `[x->`, `->]`, `->x]`, `->o]`, `?->`,
`->?`, `->o?`, `?o->`, `<-]`, `[<->`, and a colored `-[#red]>]`) are drawn sequence diagrams, and which statements are
border messages: the left and right border forms with a circle, a cross, a reversed arrow, a color, or an activation
are; plain messages, a message whose label holds `[-x]` and `x->]`, a cross head, a colored arrow, a link between
components, a link to `[*]`, and a color without a head are not.

##### CanvasNetPlantUml-Syntax-DiagramDetector-GraphFamilies: Graph Families In PlantUML's Order

**Tests**: `DiagramDetectorTests.PlantUmlDiagramDetector_Detect_ClassFamily_IsDrawn`,
`DiagramDetectorTests.PlantUmlDiagramDetector_Detect_ComponentDiagram_IsDrawn`,
`DiagramDetectorTests.PlantUmlDiagramDetector_Detect_UseCaseDiagram_IsDrawn`,
`DiagramDetectorTests.PlantUmlDiagramDetector_Detect_DeploymentDiagram_IsDrawn`,
`DiagramDetectorTests.PlantUmlDiagramDetector_Detect_UnclosedActionOfActivity_IsActivity`,
`DiagramDetectorTests.PlantUmlDiagramDetector_Detect_GraphFamilies_FollowPlantUmlOrder`

Asserts that a class, an interface, an extension, a dependency, a member link, a member line, a package with a class,
and a class with a body followed by a link are drawn class diagrams, that an object or a map is a drawn object diagram,
and that a transition from the start, a state, a composite state with regions, and descriptions with links are drawn
state diagrams; asserts that a link between two components, a component keyword, an interface written `() HTTP`, and a
component in a node are drawn component diagrams; asserts that a use case keyword, a link between use cases in
parentheses, actors written `:Guest:` and `:Account Owner: as AO` (whose action is never closed), and an actor with a
boundary holding a colored use case `(Pay) #back:white;line:green` are drawn use case diagrams; asserts that a node,
an artifact linked to a node, a queue before a storage that holds a file, and a person linked to a hexagon are drawn
deployment diagrams; and that an activity
diagram whose action is never closed (with text after it no graph family reads) stays an activity diagram; asserts
that plain messages and a message with a circle are sequence
diagrams, an extension a class diagram, links to use cases use case diagrams, links to components component diagrams,
nodes and clouds a deployment diagram, a state with a link a state diagram, a class with an actor a class diagram, and
`(*)` a legacy activity diagram.

##### CanvasNetPlantUml-Syntax-DiagramDetector-OtherTypes: Other Types Are Named

**Tests**: `DiagramDetectorTests.PlantUmlDiagramDetector_Detect_OtherTypeKeyword_IsRecognized`,
`DiagramDetectorTests.PlantUmlDiagramDetector_UnsupportedHint_ListsDrawnTypes`

Asserts the type of one-statement diagrams: legacy activity, timing, and gantt; none is drawn. Asserts that the hint of
a legacy activity diagram names it and lists the activity, class, component, deployment, object, sequence, state, and
usecase diagrams as drawn, and the joined lists of two and three types.

##### CanvasNetPlantUml-Syntax-DiagramDetector-Errors: Empty And Unknown Diagrams Fail Located

**Tests**: `DiagramDetectorTests.PlantUmlDiagramDetector_Detect_EmptyDiagram_Throws`,
`DiagramDetectorTests.PlantUmlDiagramDetector_Detect_UnknownStatement_ThrowsAtStatement`

Asserts that an empty diagram and a title-only diagram are syntax errors (the second at the title), and that an
unknown statement after a message is a syntax error at its first character naming the sequence diagram.
