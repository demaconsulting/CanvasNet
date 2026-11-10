## Dagre Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Dagre` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Dagre`), containing nine units: `DagreLayout` (the entry point, the stages of
`layout.js` and `util.js`, the input and layout labels, and the lodash and V8 semantics), `DagreAcyclic` (cycle
breaking), `DagreRanker` (ranking), `DagreNesting` (clusters), `DagreNormalizer` (long-edge chains), `DagreOrdering`
(crossing reduction), `DagrePositioning` (coordinates), `DagreCoordinateSystem` (rank directions), and
`LayoutWorkMeter` (the work budget).

### Verification Approach

The `Dagre` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `dagre/`)
verify each stage in isolation, with hand-worked expectations and cases ported from the test files of dagre-d3-es. The
subsystem-level scenarios in `DagreTests` verify the stages working together through `DagreLayout.Run`, the entry point
that the `Rendering` subsystem calls.

The central evidence is a set of **stage goldens**: 224 layouts recorded from dagre-d3-es itself by
`tools/visual-diff/mermaid/gen-dagre-goldens.mjs` and stored in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/dagre-layouts.json`. Each case holds an input
graph (from a seeded generator, so the inputs are original) and dagre's output: the rank and order of every real node,
every node center and size, every edge route and label center, and the drawing size. The helper `DagreGoldens` builds
the input graph with the same calls, in the same order, as the generator (so that the key order is the same), runs the
port, and reports the first difference; ranks and orders must be equal and every number must agree within 1e-9
relative to its size (at least 1). The families cover 50 seeded acyclic graphs, 35 cyclic graphs (half with the greedy
cycle breaker), 50 compound graphs, 30 deeply nested compound graphs, 8 large compound graphs of 40 to 70 nodes, 20
self-loop graphs, 15 simple graphs, 10 tiny or empty graphs, and 6 hand-made cases (an empty graph, a long edge, and a
labeled edge in each rank direction), with numeric and Mermaid-style names, parallel edges, every label position,
label offsets, minimum lengths 1 to 3, weights 1 to 3, random separations and margins, all three rankers, and every
alignment; nine cases exercise dagre's half-rank label quirk.

The work budget is verified with pinned work counts, so that any change to the metering of a stage is noticed, and with
a small budget that stops a large graph. No mocking or stubbing is used: the `Graphlib` graph, the meter, and every
stage are real. The subsystem is internal, so its tests reach it through the test assembly's `InternalsVisibleTo`
access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/dagre-layouts.json`, copied to the test output and read once; the helpers
  `DagreGoldens` (the golden reader and comparer) and `DagreTestGraphs` (meters and graph builders)
- **Isolation**: Each test builds its own graphs and meter; the fixture is read-only

### Acceptance Criteria

The `Dagre` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the nine unit verification documents passes without error or unexpected exception. A golden case with any rank or
order that differs, or any coordinate that differs beyond the tolerance, constitutes a failure, as does a work count
that differs from its pinned value.

### Test Scenarios

#### CanvasNetMermaid-Dagre-Layout: Layouts Match dagre-d3-es

**Tests**: `DagreTests.Dagre_Layout_UpstreamGolden_MatchesDagre`

For each of the 224 golden cases, builds the input graph, lays it out with `DagreLayout.Run`, and asserts that no
difference from the layout recorded from dagre-d3-es is found: the ranks and orders of the real nodes are equal, and
the node centers and sizes, the cluster sizes, the edge routes, the edge label centers, and the drawing size agree
within 1e-9 relative, across every family, rank direction, ranker, alignment, and label position.

#### CanvasNetMermaid-Dagre-TieBreaking: Ties Are Settled As In JavaScript

**Tests**: `DagreTests.Dagre_Layout_UpstreamGolden_MatchesDagre`

The same golden cases include graphs whose layout depends on how ties are settled: numeric node names (whose key order
differs from their insertion order), named parallel edges, equal barycenters, NaN barycenters of nodes without
neighbors, equal slacks, and many generated dummy names. Equal ranks and orders on every case show that the port
iterates, sorts, and names as dagre does in JavaScript.

#### CanvasNetMermaid-Dagre-Limits: Layout Work Is Bounded And Located

**Tests**: `DagreTests.Dagre_Layout_CompleteGraph30_SmallBudget_LimitExceeded`,
`DagreTests.Dagre_Layout_CompleteGraph30_DefaultBudget_CompletesWithPinnedWork`,
`DagreTests.Dagre_Layout_SmallGraph_PinnedWork`

Lays out the complete acyclic graph on 30 nodes (435 edges, thousands of dummy nodes): with a budget of 1,000,000 steps
the layout throws a `MermaidRenderException` with the code `mermaid-limit-exceeded` at the meter's location and the
meter reports the whole budget used; with the default `MermaidLimits.MaxLayoutWork` it completes with finite
coordinates in exactly 2,955,593 steps. A four-node cyclic graph with a cluster and a labeled edge takes exactly 1,369
steps.

#### CanvasNetMermaid-Dagre-Cancellation: A Canceled Layout Stops

**Tests**: `DagreTests.Dagre_Layout_Canceled_ThrowsOperationCanceled`

Lays out a complete graph with a meter whose token is already canceled and asserts that `DagreLayout.Run` throws
`OperationCanceledException` (it is not turned into a render failure).

#### CanvasNetMermaid-Dagre-Determinism: Repeated And Concurrent Layouts Are Identical

**Tests**: `DagreTests.Dagre_Layout_RepeatedAndParallel_Identical`

Lays out the same deeply nested golden graph eight times in parallel, each with its own meter, and asserts that the
fingerprints of every node center and route point (written with round-trip precision) are all identical.
