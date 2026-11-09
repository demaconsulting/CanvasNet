### DagreCoordinateSystem Unit Verification Design

This document describes the unit-level verification strategy for the `DagreCoordinateSystem` unit in the
`DemaConsulting.CanvasNet.Mermaid.Dagre` namespace: the internal static `DagreCoordinateSystem` class (`Adjust` and
`Undo`), the port of dagre's `coordinate-system.js`.

#### Verification Approach

The unit is verified with cases ported from dagre-d3-es's `coordinate-system.test.js`, run for each rank direction (in
upper case and, for one direction, lower case): a node and an edge label of different width and height for `Adjust`,
and a positioned node, route point, and label center at (100, 200) for `Undo`. The expected values follow directly
from the mirror and transpose rules. No mocking or stubbing is used; the internal type is reached through the test
assembly's `InternalsVisibleTo` access.

Unit tests reside in `DagreCoordinateSystemTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in
the `Dagre` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own graph; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any size or coordinate that differs from the rule for its direction constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Dagre-DagreCoordinateSystem-Adjust: Horizontal Layouts Swap Their Sizes

**Tests**: `DagreCoordinateSystemTests.DagreCoordinateSystem_Adjust_RankDir_SwapsSizesWhenHorizontal`

Asserts that the width and height of a node and of an edge label are swapped for `LR` and `rl` and unchanged for `TB`
and `BT`.

##### CanvasNetMermaid-Dagre-DagreCoordinateSystem-Undo: Layouts Are Mirrored And Transposed

**Tests**: `DagreCoordinateSystemTests.DagreCoordinateSystem_Undo_RankDir_MirrorsAndTransposes`

Asserts that a node, a route point, and a label center at (100, 200) end at (100, 200) for `TB`, (100, -200) for `BT`,
(200, 100) for `LR`, and (-200, 100) for `RL`, and that the node's width is swapped back for the horizontal
directions.
