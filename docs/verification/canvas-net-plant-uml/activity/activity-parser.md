### ActivityParser Unit Verification Design

This document describes the unit-level verification strategy for the `ActivityParser` unit in the
`DemaConsulting.CanvasNet.PlantUml.Activity` namespace.

#### Verification Approach

The unit is verified by parsing small original diagram bodies (preprocessed and detected by the Syntax subsystem) and
asserting the tree of instructions, the swimlanes, the page texts, the reports, and the exceptions. No mocking is
required. Unit tests reside in `Activity/ActivityParserTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. An instruction, a label, a report, a code, or a
location that differs from the expected literal constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Activity-ActivityParser-Actions: Actions And Nodes

**Tests**: `ActivityParserTests.PlantUmlActivityParser_Actions_ReadTextColorAndNodes`

Asserts a start, an action, a colored action of two lines (its text, its pink fill, and the line it starts on), a
stop, and an end.

##### CanvasNetPlantUml-Activity-ActivityParser-Conditions: Ifs

**Tests**: `ActivityParserTests.PlantUmlActivityParser_If_ReadsBranchesAndLabels`

Asserts the tests and labels of an if and its elseif, the label and content of the else branch, and the label of an
`is` form.

##### CanvasNetPlantUml-Activity-ActivityParser-Loops: Loops

**Tests**: `ActivityParserTests.PlantUmlActivityParser_Loops_ReadTestsLabelsAndBackward`

Asserts the test, the label, the backward action, and the exit label of a while loop, and the start label, the test,
the labels, and the backward action of a repeat loop.

##### CanvasNetPlantUml-Activity-ActivityParser-Parallel: Forks And Splits

**Tests**: `ActivityParserTests.PlantUmlActivityParser_ForksAndSplits_ReadBranchesAndEnds`

Asserts a fork of three branches with the label of its end, a merge, and a split of two branches.

##### CanvasNetPlantUml-Activity-ActivityParser-Arrows: Arrows

**Tests**: `ActivityParserTests.PlantUmlActivityParser_Arrows_LeadIntoTheNextInstruction`

Asserts the labels of two arrows on the instructions they lead into, and the blue color and dashed style of the second.

##### CanvasNetPlantUml-Activity-ActivityParser-Swimlanes: Swimlanes

**Tests**: `ActivityParserTests.PlantUmlActivityParser_Swimlanes_ReadOrderColorsAndPlacement`

Asserts two swimlanes in order, the color of the second, and the swimlane of every instruction.

##### CanvasNetPlantUml-Activity-ActivityParser-Annotations: Notes, Connectors, Groups, And Kill

**Tests**: `ActivityParserTests.PlantUmlActivityParser_NotesConnectorsGroupsAndKill_AreRead`

Asserts a framed partition with its title, a right note and a floating multi-line left note on its action, a
connector, a killed action, and a package drawn without a frame and reported at its line.

##### CanvasNetPlantUml-Activity-ActivityParser-Settings: Page Texts, Scale, And Skin Parameters

**Tests**: `ActivityParserTests.PlantUmlActivityParser_Settings_ReadPageTextsSkinParamsAndScale`

Asserts the title, header, footer, caption, and legend, the scale of 1.5, and the green activity background of a skin
parameter.

##### CanvasNetPlantUml-Activity-ActivityParser-Unsupported: Statements That Are Not Drawn

**Tests**: `ActivityParserTests.PlantUmlActivityParser_UnsupportedStatements_AreReportedAndSkipped`

Asserts `plantuml-statement-unsupported` reports at the lines of a switch block, a box-style ending, and a goto, and
that the actions around them are kept.

##### CanvasNetPlantUml-Activity-ActivityParser-Errors: Located Syntax Errors

**Tests**: `ActivityParserTests.PlantUmlActivityParser_InvalidStatements_FailLocated`

Asserts `plantuml-syntax-error` with a hint at the line of an else outside an if, an if that is not closed, an
endwhile without a loop, an action without its end, a fork again outside a fork, and a swimlane after the first
instruction.

##### CanvasNetPlantUml-Activity-ActivityParser-Limits: Limits

**Tests**: `ActivityParserTests.PlantUmlActivityParser_Limits_AreEnforced`

Asserts `plantuml-limit-exceeded` for one instruction over `MaxNodes` (at its line), one arrow over `MaxEdges`, an if
nested past `MaxNestingDepth` (at its line), and a label over `MaxLabelLength`.
