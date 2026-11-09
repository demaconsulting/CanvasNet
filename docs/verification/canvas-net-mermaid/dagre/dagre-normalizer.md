### DagreNormalizer Unit Verification Design

This document describes the unit-level verification strategy for the `DagreNormalizer` unit in the
`DemaConsulting.CanvasNet.Mermaid.Dagre` namespace: the internal static `DagreNormalizer` class (`Run` and `Undo`), the
port of dagre's `normalize.js`.

#### Verification Approach

The unit is verified with cases ported from dagre-d3-es's `normalize.test.js`: a one-rank edge, a long edge with a
weight and a label rank, and the restoration of a positioned chain. The tests assert the dummy nodes (kind, rank, size,
and label position), the chain edges (weight and name), the dummy chain record, and, after `Undo`, the original edge
with its label, route, and label position and size. No mocking or stubbing is used; the internal type is reached
through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `DagreNormalizerTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Dagre` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own graph and meter; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A chain that differs from dagre's, or a restored edge whose route or label position differs from the dummy
positions, constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Dagre-DagreNormalizer-Split: Long Edges Become Chains

**Tests**: `DagreNormalizerTests.DagreNormalizer_Run_ShortEdge_Unchanged`,
`DagreNormalizerTests.DagreNormalizer_Run_LongEdge_SplitThroughDummies`

Asserts that an edge between adjacent ranks is unchanged and that a long edge is replaced by a chain through dummy
nodes of size 0, one per intermediate rank, whose edges keep the original weight.

##### CanvasNetMermaid-Dagre-DagreNormalizer-LabelNode: The Label Rank Gets The Label Size

**Tests**: `DagreNormalizerTests.DagreNormalizer_Run_LongEdge_SplitThroughDummies`

Asserts that the dummy at the edge's label rank is an edge-label dummy with the label's width, height, and position.

##### CanvasNetMermaid-Dagre-DagreNormalizer-Restore: Chains Become Routes

**Tests**: `DagreNormalizerTests.DagreNormalizer_Undo_Chain_RestoresEdgeWithRoute`

Asserts that `Undo` removes the dummies, restores the edge with its original label object, sets the route to the dummy
positions in order, and sets the label position and size from the label dummy.
