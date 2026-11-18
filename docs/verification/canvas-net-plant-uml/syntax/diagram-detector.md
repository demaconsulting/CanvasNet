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
`DiagramDetectorTests.PlantUmlDiagramDetector_BlockClosers_AreRecognized`

Asserts that a diagram with a title, an actor, messages, a group, a multi-line note whose text starts with `class`, a
divider, a delay, and a space is a sequence diagram recognized by `actor` at line 3, and that a diagram with start, an
action, a condition, a connector, and stop is an activity diagram; asserts the closer of a legend with an alignment,
`end legend` and `endlegend` closing it and `end legends` not, the block of an aligned footer alone on its line, no
block for an aligned header with text, an aligned header as a common statement, and `endbox` as a sequence statement.

##### CanvasNetPlantUml-Syntax-DiagramDetector-OtherTypes: Other Types Are Named

**Tests**: `DiagramDetectorTests.PlantUmlDiagramDetector_Detect_OtherTypeKeyword_IsRecognized`

Asserts the type of thirteen one-statement diagrams: class (two keywords), object, use case (keyword and parentheses),
state (`[*]` and keyword), legacy activity, component (brackets and keyword), deployment, timing, and gantt; none is
drawn.

##### CanvasNetPlantUml-Syntax-DiagramDetector-Errors: Empty And Unknown Diagrams Fail Located

**Tests**: `DiagramDetectorTests.PlantUmlDiagramDetector_Detect_EmptyDiagram_Throws`,
`DiagramDetectorTests.PlantUmlDiagramDetector_Detect_UnknownStatement_ThrowsAtStatement`

Asserts that an empty diagram and a title-only diagram are syntax errors (the second at the title), and that an
unknown statement after a message is a syntax error at its first character naming the sequence diagram.
