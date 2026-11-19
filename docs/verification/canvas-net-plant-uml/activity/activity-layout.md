### ActivityLayout Unit Verification Design

This document describes the unit-level verification strategy for the `ActivityLayout` unit in the
`DemaConsulting.CanvasNet.PlantUml.Activity` namespace.

#### Verification Approach

The unit is verified by laying out small original diagram bodies and the fixtures and asserting the placed shapes:
PlantUML's coordinates where the reference pictures give them, and the relative placement of branches, bars,
swimlanes, notes, and frames otherwise. The work limit and the cancellation are asserted on a fixture. No mocking is
required. Unit tests reside in `Activity/ActivityLayoutTests.cs`; the reference layout tests of the system compare every
fixture with PlantUML's picture.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values and the read-only fixtures; no shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A coordinate, a size, an order, or a code that differs
from the expected literal constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Activity-ActivityLayout-Sequence: Assembly And Compression

**Tests**: `ActivityLayoutTests.PlantUmlActivityLayout_Sequence_StacksOnOneAxis`,
`ActivityLayoutTests.PlantUmlActivityLayout_Fixtures_FitInTheirSize`

Asserts PlantUML's size (145 by 207), the axis of the start node, the places of two action boxes, and the 20 pixels
between them after the compression; asserts that every action box of every fixture is inside its picture.

##### CanvasNetPlantUml-Activity-ActivityLayout-Conditions: Ifs

**Tests**: `ActivityLayoutTests.PlantUmlActivityLayout_If_PlacesBranchesSideBySide`

Asserts two diamonds, the two branches side by side at the same height, below the test and above the merge.

##### CanvasNetPlantUml-Activity-ActivityLayout-Loops: Loops

**Tests**: `ActivityLayoutTests.PlantUmlActivityLayout_While_PlacesTheTestAndTheBody`

Asserts PlantUML's places of the hexagon, the first action of the body, and the action after the loop, and the exit
label left of the hexagon.

##### CanvasNetPlantUml-Activity-ActivityLayout-Parallel: Forks And Merges

**Tests**: `ActivityLayoutTests.PlantUmlActivityLayout_Fork_PlacesBranchesBetweenBars`

Asserts two bars with the branches between them at the same height, and one bar and one diamond for a merge.

##### CanvasNetPlantUml-Activity-ActivityLayout-Swimlanes: Swimlanes

**Tests**: `ActivityLayoutTests.PlantUmlActivityLayout_Swimlanes_PlaceInstructionsInTheirColumns`,
`ActivityLayoutTests.PlantUmlActivityLayout_Swimlanes_ElseIfBranchInAnotherLane_DrawsArrowsFromTheHexagons`,
`ActivityLayoutTests.PlantUmlActivityLayout_Swimlanes_ElseIfMergeLine_DrawnOnlyInLanesWithOutcomes`,
`ActivityLayoutTests.PlantUmlActivityLayout_Swimlanes_IfBranchInAnotherLane_LeavesBelowTheHexagon`

Asserts three dividers, every action between the dividers of its swimlane, and the titles above the actions. For an
elseif chain whose first branch changes swimlane, asserts an arrow head at the top center of each branch's action and
an arrow that leaves the bottom point of each of the two hexagons. For the same chain, asserts that every part of the
line that joins the branches reaches past the first divider, and that one part joins the two branches. For an if
whose then branch changes swimlane, asserts that the arrow to the branch first steps out of the hexagon on its left
and runs below the hexagon, not through it.

##### CanvasNetPlantUml-Activity-ActivityLayout-Annotations: Notes And Partitions

**Tests**: `ActivityLayoutTests.PlantUmlActivityLayout_NotesAndGroups_SurroundTheirInstructions`

Asserts a right note beside its action and a partition frame around it.

##### CanvasNetPlantUml-Activity-ActivityLayout-Limits: Work Limit And Cancellation

**Tests**: `ActivityLayoutTests.PlantUmlActivityLayout_WorkLimitAndCancellation_StopTheLayout`

Asserts `plantuml-limit-exceeded` under a work limit of 20 and `OperationCanceledException` for a canceled call.
