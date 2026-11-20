# System Verification Design

This document describes the system-level verification strategy for CanvasNetLayout.

## Verification Approach

`CanvasNetLayout` is verified at three tiers with xUnit v3 in the test project
`test/DemaConsulting.CanvasNet.Layout.Tests/`, which references only the Layout package (no other CanvasNet project,
so the layout is verified without any diagram package):

- **System tests** (described here) in `LayoutSystemIntegrationTests` use the interface the consumers use - an input
  `Graph` with `DagreGraph`, `DagreNode`, and `DagreEdge` labels, `DagreLayout.Run`, and a consumer's work meter - end
  to end, and assert on observable outputs: the node centers, cluster boxes, edge routes, label centers, and drawing
  size written to the labels, the failures and work of the meter, and the notices copied from the package. The
  package's types are internal, so the tests reach them through `InternalsVisibleTo`.
- **Subsystem tests** exercise each subsystem through its own entry points (see *Graphlib Subsystem Verification
  Design* and *Dagre Subsystem Verification Design*); the central evidence is the 224 stage goldens recorded from
  dagre-d3-es.
- **Unit tests** exercise each unit in isolation (see the unit verification documents).

The layout package knows no consumer, so the tests run it with the test consumer `TestLayoutWorkMeter` (in `Dagre/`),
whose failures are `LayoutTestException` values; the located failures that a diagram package creates are verified with
that package (Mermaid's `MermaidLayoutWorkMeter` in *MermaidDocument Unit Verification Design*). That the move of the
layout into this package changed no Mermaid picture is verified by the unchanged Mermaid tests, goldens, and
reference layouts, which run the layout through Mermaid.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK on Windows, Linux, and macOS, for net8.0, net9.0, net10.0, and
  net11.0
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Files**: the stage goldens `Fixtures/upstream-vectors/dagre-layouts.json` and the third-party notices of the package
  (copied to `Notices/` of the test output); no network
- **Mocking**: None; the test consumer `TestLayoutWorkMeter` derives the abstract meter

## Acceptance Criteria

A system-level test run passes when every scenario below passes without an exception beyond those explicitly
asserted, on every platform and runtime of the platform requirements. A non-finite coordinate, a member outside its
cluster, a rank direction that is not followed, a failure that is not the consumer's, or results that differ between
runs constitute a failure.

## Test Scenarios

### CanvasNetLayout-LayeredLayout: Compound Cyclic Graphs Are Laid Out In Every Direction

**Tests**:
`LayoutSystemIntegrationTests.CanvasNetLayout_LayeredLayout_CompoundCyclicLabeledGraph_LaidOutInEveryRankDirection`,
`DagreTests.Dagre_Layout_UpstreamGolden_MatchesDagre`

Lays out a graph of four nodes, two of them in a cluster, with the cycle `a -> b -> c -> a` (whose first edge has a
label) and one more edge, in each of the rank directions `TB`, `BT`, `LR`, and `RL`, and asserts that every node center
and route point is finite, every edge has a route of at least two points, the label center is finite, the drawing has
a size, the cluster encloses its members, and the first edge of the cycle runs in the rank direction. That the results
are exactly those of dagre-d3-es is verified by the stage goldens (*Dagre Subsystem Verification Design*), whose
theory over every golden case is also linked here as the fidelity evidence of the system requirement: every rank,
order, coordinate, size, route, and label center of the port equals the one recorded from dagre-d3-es.

### CanvasNetLayout-Limits: The Consumer's Budget Stops A Layout

**Tests**: `LayoutSystemIntegrationTests.CanvasNetLayout_Limits_SmallBudget_ConsumerLimitFailure`

Lays out the same graph with a budget of 100 steps and asserts that the layout throws the consumer's limit failure
itself (a `LayoutTestException` marked as a limit failure) and that the whole budget is used.

### CanvasNetLayout-Cancellation: A Canceled Layout Stops

**Tests**: `LayoutSystemIntegrationTests.CanvasNetLayout_Cancellation_Canceled_ThrowsOperationCanceled`

Lays out the graph with a meter whose token is already canceled and asserts that the layout throws
`OperationCanceledException`.

### CanvasNetLayout-Determinism: Repeated And Concurrent Layouts Are Identical

**Tests**: `LayoutSystemIntegrationTests.CanvasNetLayout_Determinism_RepeatedAndParallel_Identical`

Lays out the graph eight times in parallel, each with its own meter, and asserts that the fingerprints of every node
center and route point (written with round-trip precision) and the work used are all identical.

### CanvasNetLayout-ThirdPartyNotices: The Notices Ship With The Package

**Tests**: `LayoutSystemIntegrationTests.CanvasNetLayout_ThirdPartyNotices_ShipWithPackage`

Reads the license notices and the provenance record that the package ships (copied from
`src/DemaConsulting.CanvasNet.Layout/third-party/`) and asserts that they carry the copyright notices of dagre-d3-es
(with dagre and graphlib) and lodash, that the provenance record names both license files, and that it states that
the project is not affiliated with the ported projects.

### CanvasNetLayout-Platform: Every Platform And Runtime

**Tests**:
`LayoutSystemIntegrationTests.CanvasNetLayout_LayeredLayout_CompoundCyclicLabeledGraph_LaidOutInEveryRankDirection` with
the `windows@`, `ubuntu@`, `macos@`, `net8.0@`, `net9.0@`, `net10.0@`, and `net11.0@` source filters

The representative test is evidence for each platform and runtime only from a test run on that platform or runtime.
