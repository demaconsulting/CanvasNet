## Cose Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Cose` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Cose`), containing one unit: `CoseLayout` (the port of the cose-bilkent layout as
Mermaid runs it for a mindmap, with cytoscape's straight-edge points, the correctly rounded math, and the work charges).

### Verification Approach

The `Cose` subsystem is verified at two tiers. The unit tests (see the CoseLayout unit verification design) verify the
unit against the layouts and the math that Chrome computes with the libraries of the pinned Mermaid bundle, bit for bit,
and each rule on its own. The subsystem-level scenarios in `CoseTests` verify the layout as its consumer uses it:
`CoseLayout.Run` is called with the sizes and edges of trees shaped like mindmaps (sizes on the 1/64 pixel grid, a
parent among the last few nodes for each node), of trees whose nodes all start on top of each other, and of trees with
huge sizes, with a work meter as a render creates it, and the results are checked for the properties every layout must
have: the margin, the edge points between the centers, finite and distinct positions, determinism across threads, and
the documented failures, limits, and cancellation. The layout of whole mindmaps against Mermaid's goldens is verified
at the system level by the mindmap reference layout tests.

The scenarios use no mocking or stubbing: the meter is the real `LayoutWorkMeter` of the `Dagre` subsystem. The
subsystem is internal, so its tests reach it through the test assembly's `InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own trees in memory; no files are read

### Acceptance Criteria

The `Cose` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the CoseLayout unit verification document passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-Cose-Layout: A Tree Is Laid Out As Mermaid's Layout Does

**Tests**: `CoseTests.Cose_Layout_MindmapTree_EdgesBetweenCenters`,
`CoseTests.Cose_Layout_OverlappingStart_FiniteLayout`

Lays out a mindmap-like tree of 25 nodes and asserts that the leftmost and topmost node sides are at the margin of 15
(to 1e-9) and that every edge starts 15 pixels from its source center and ends 15 pixels from its target center, with
its middle point halfway between them; and lays out a binary tree of 12 nodes whose nodes are all 100 by 44, and the
same tree with nodes of size 0, and asserts that every center is finite and that the equal-sized nodes, which start on
top of each other, end at 12 distinct centers. The bit-for-bit agreement with Mermaid's layout in Chrome is verified at
the unit level (see the CoseLayout unit verification design).

#### CanvasNetMermaid-Cose-Determinism: The Same Tree Gives The Same Layout

**Tests**: `CoseTests.Cose_Layout_SameTree_Deterministic`

Lays out a mindmap-like tree of 40 nodes once and four more times in parallel, and asserts that every run gives the
same centers, edge points, and iteration count. That the math is correctly rounded and equal to Chrome's, and so the
same on every platform, is verified at the unit level.

#### CanvasNetMermaid-Cose-Limits: The Work Is Bounded And Stoppable

**Tests**: `CoseTests.Cose_Layout_HugeSizes_LimitOrRenderFailure`,
`CoseTests.Cose_Layout_FourHundredNodes_WithinDefaultLimit`,
`CoseTests.Cose_Layout_SmallBudgetOrCanceled_Stops`

Lays out a two-node tree whose first node is 4,000,000 by 3,000,000, 1e300 by 1e300, or the largest double wide, and
asserts that each ends in a `MermaidRenderException` with `mermaid-limit-exceeded` or `mermaid-render-failed`, never a
hang; lays out a mindmap-like tree of 400 nodes under the default `MaxLayoutWork` and asserts that it completes with
work to spare; and asserts that a 40-node tree under a budget of 1,000 units fails with `mermaid-limit-exceeded`, and
under a canceled token with an `OperationCanceledException`.
