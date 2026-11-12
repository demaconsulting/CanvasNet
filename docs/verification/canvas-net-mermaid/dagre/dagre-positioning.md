### DagrePositioning Unit Verification Design

This document describes the unit-level verification strategy for the `DagrePositioning` unit in the
`DemaConsulting.CanvasNet.Mermaid.Dagre` namespace: the internal static partial `DagrePositioning` class (`Position`,
`PositionX`, `FindType1Conflicts`, `FindType2Conflicts`, `VerticalAlignment`, `HorizontalCompaction`,
`FindSmallestWidthAlignment`, `AlignCoordinates`, and `Balance`), the port of dagre's `position/index.js` and
`position/bk.js`.

#### Verification Approach

The unit is verified with cases ported from dagre-d3-es's `position.test.js` and `position/bk.test.js`: the conflicts
of crossings with and without dummy nodes and with cluster borders, the median alignment of small layerings, the
balance of four given assignments, the shift of assignments to a reference, the narrowest assignment with node widths,
the x coordinates of small graphs, and the y coordinates of ranks of different heights. The expected values are those
that dagre's tests assert or that its rules give by hand. No mocking or stubbing is used; the internal type is reached
through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `DagrePositioningTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Dagre` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own graph and meter; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any conflict, alignment, or coordinate that differs from dagre's constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Dagre-DagrePositioning-Ranks: Ranks Are Stacked By Their Tallest Node

**Tests**: `DagrePositioningTests.DagrePositioning_Position_Ranks_StackedByTallestNode`

Asserts that each rank is as tall as its tallest node, that the ranks are separated by the rank separation, and that
every node is centered vertically in its rank.

##### CanvasNetMermaid-Dagre-DagrePositioning-Conflicts: Conflicts With Inner Segments Are Marked

**Tests**: `DagrePositioningTests.DagrePositioning_FindType1Conflicts_Crossing_MarksNonInnerSegment`,
`DagrePositioningTests.DagrePositioning_FindType2Conflicts_InnerCrossesBorder_Marked`

Asserts that a crossing between real edges or between two inner segments marks nothing, that a crossing between an
inner segment and another edge marks the other edge, and that an inner segment that crosses a cluster's border segment
is marked.

##### CanvasNetMermaid-Dagre-DagrePositioning-Alignment: Nodes Align With A Median Neighbor

**Tests**: `DagrePositioningTests.DagrePositioning_VerticalAlignment_Medians_AlignsBlocks`

Asserts that a node aligns with its left median neighbor when possible and with the right one when the left conflicts,
and that the neighbors are taken by position rather than by name.

##### CanvasNetMermaid-Dagre-DagrePositioning-Balance: The Four Alignments Are Combined

**Tests**: `DagrePositioningTests.DagrePositioning_FindSmallestWidthAlignment_Widths_PicksNarrowest`,
`DagrePositioningTests.DagrePositioning_AlignCoordinates_ToReference_ShiftsEdges`,
`DagrePositioningTests.DagrePositioning_Balance_FourAlignments_AveragesMedians`

Asserts that the narrowest assignment is found with the node widths included, that the left assignments are shifted to
the reference's left edge and the right ones to its right edge, and that each node's x is the average of its two middle
values.

##### CanvasNetMermaid-Dagre-DagrePositioning-Coordinates: Horizontal Coordinates Match dagre

**Tests**: `DagrePositioningTests.DagrePositioning_PositionX_SmallGraphs_MatchDagre`

Asserts the x coordinates of small graphs as dagre computes them: a single node at 0, the two children of a fork placed
symmetrically 30 either side of their parent, and a graph whose joined nodes share an x while their neighbors in each
rank are separated by half of each width plus the node separation.
