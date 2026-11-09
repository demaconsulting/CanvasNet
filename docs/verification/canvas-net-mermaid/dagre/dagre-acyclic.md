### DagreAcyclic Unit Verification Design

This document describes the unit-level verification strategy for the `DagreAcyclic` unit in the
`DemaConsulting.CanvasNet.Mermaid.Dagre` namespace: the internal static `DagreAcyclic` class (`Run`, `Undo`, `DfsFas`,
and `GreedyFas`), the port of dagre's `acyclic.js`, `greedy-fas.js`, and `data/list.js`.

#### Verification Approach

The unit is verified on small graphs ported from dagre-d3-es's `acyclic.test.js` and `greedy-fas.test.js`: an acyclic
graph with every cycle breaker (the depth-first default, `greedy`, and an unknown name that falls back to the default),
a square cycle and a two-node cycle, a weighted cycle, two four-node cycles, and named parallel edges. The tests use the
`Graphlib` subsystem's `FindCycles` to check that no cycle remains and compare the returned feedback arc sets with the
edges dagre returns. No mocking or stubbing is used; the internal type is reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `DagreAcyclicTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Dagre` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own graph and meter; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A cycle left after `Run`, an edge set that differs from dagre's, or an edge not restored by `Undo`
constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Dagre-DagreAcyclic-BreakCycles: Cycles Are Broken

**Tests**: `DagreAcyclicTests.DagreAcyclic_Run_AcyclicGraph_Unchanged`, `DagreAcyclicTests.DagreAcyclic_Run_Cycles_Broken`

Asserts that an acyclic graph keeps every edge with each breaker, and that a square cycle and a two-node cycle have no
cycle afterwards with both breakers, the two-node cycle becoming two parallel edges in one direction.

##### CanvasNetMermaid-Dagre-DagreAcyclic-Greedy: The Greedy Breaker Picks dagre's Edges

**Tests**: `DagreAcyclicTests.DagreAcyclic_Run_Greedy_BreaksAtLowWeightEdge`,
`DagreAcyclicTests.DagreAcyclic_GreedyFas_NoCycles_Empty`,
`DagreAcyclicTests.DagreAcyclic_GreedyFas_TwoFourNodeCycles_WithinBound`,
`DagreAcyclicTests.DagreAcyclic_GreedyFas_WeightedAndMultiEdges_ReturnsLightEdges`

Asserts that the greedy breaker reverses the light edge of a weighted cycle; that the set is empty for an empty, a
single-node, and an acyclic graph; that the set for two four-node cycles breaks every cycle within the heuristic's
bound of m / 2 - n / 6 edges; and that weights steer the choice to the light edge and that both named parallel edges of
a chosen pair are returned.

##### CanvasNetMermaid-Dagre-DagreAcyclic-Restore: Reversed Edges Are Restored

**Tests**: `DagreAcyclicTests.DagreAcyclic_Undo_ReversedEdges_Restored`

Asserts, with both breakers, that after `Run` and `Undo` a two-node cycle has its two edges back in their original
directions, with the same label objects, the original name of the named edge, and no edge marked as reversed.
