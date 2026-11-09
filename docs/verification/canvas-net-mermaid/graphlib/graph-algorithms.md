### GraphAlgorithms Unit Verification Design

This document describes the unit-level verification strategy for the `GraphAlgorithms` unit in the
`DemaConsulting.CanvasNet.Mermaid.Graphlib` namespace: the internal static `GraphAlgorithms` class (`Preorder` and
`Postorder`).

#### Verification Approach

The unit is verified by running each traversal on a small graph built with the `Graph` unit and comparing the result,
element by element and in order, with the result graphlib gives in JavaScript, worked out by hand; the cases follow
graphlib's own tests. The `Graph` unit is used as is (it is verified by its own tests), so no mocking or stubbing is
required; the internal types are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `GraphAlgorithmsTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Graphlib` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own graph; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A traversal that lists other nodes, or the same nodes in another order, than graphlib constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Graphlib-GraphAlgorithms-DepthFirst: Preorder And Postorder Follow The Key Order

**Tests**: `GraphAlgorithmsTests.GraphAlgorithms_PreorderPostorder_Tree_VisitsInOrder`,
`GraphAlgorithmsTests.GraphAlgorithms_Preorder_UndirectedAndUnknownRoot_FollowsNeighborsOrThrows`

Asserts that the tree `a`-`b`-`d`, `a`-`c`, `b`-`e` gives the preorder `a`, `b`, `d`, `e`, `c` and the postorder
`d`, `e`, `b`, `c`, `a`; that an undirected graph is walked through its neighbors (`c`, `b`, `a` from `c`); and that a
root that is not a node throws `ArgumentException`.
