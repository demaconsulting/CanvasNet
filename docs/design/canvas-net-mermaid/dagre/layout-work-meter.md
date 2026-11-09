### LayoutWorkMeter

![Dagre Structure](MermaidDagreView.svg)

The `LayoutWorkMeter` unit is the ninth software unit in the `Dagre` subsystem. It is one source file,
`LayoutWorkMeter.cs`, in the `DemaConsulting.CanvasNet.Mermaid.Dagre` namespace, and defines the internal sealed class
`LayoutWorkMeter`. The work budget is original; the counter of `UniqueId` reproduces lodash's `_.uniqueId`.

#### Purpose

`LayoutWorkMeter` bounds the layout of one diagram. Every iterative stage of the layout charges its steps to the meter,
which also checks for cancellation, so a pathological graph from untrusted text stops early at a documented limit
instead of running for minutes, and a canceled call stops at the next charge. The meter also creates the two failures
of a layout with the diagram's location, and holds the counter that names dagre's dummy nodes.

The unit's behavior is specified by the `CanvasNetMermaid-Dagre-LayoutWorkMeter-*` requirements in
`docs/reqstream/canvas-net-mermaid/dagre/layout-work-meter.yaml`.

#### Data Model

- `MaxWork` - the most work allowed (`MermaidLimits.MaxLayoutWork`, 50,000,000 by default), at least 1.
- `Used` - the work charged so far; set to `MaxWork` when a charge fails.
- The location of the failures (the diagram keyword, or a default location in the unit tests), the cancellation
  token of the call, and the last number given out by `UniqueId`.

#### Key Methods

- `LayoutWorkMeter(long maxWork, MermaidLocation location, CancellationToken cancellationToken)` - creates a meter.
- `Charge(long units)` - checks the cancellation token, then adds the units, or throws `LimitExceeded()` when they
  would pass `MaxWork` (the comparison `units > MaxWork - Used` cannot overflow).
- `UniqueId(string prefix)` - the prefix followed by the next number of one counter shared by every prefix, in the
  invariant culture, as lodash's `_.uniqueId` gives.
- `Failure(string description)` - a `MermaidRenderException` with the code `mermaid-render-failed`, the message
  "the layout failed: " and the description, at the meter's location.
- `LimitExceeded()` - a `MermaidRenderException` with the code `mermaid-limit-exceeded`, a message that names the limit
  and `MermaidLimits.MaxLayoutWork`, the meter's location, and a hint to simplify the diagram or raise the limit.

**Preconditions and postconditions**: one meter serves one diagram on one thread and is shared by every layout of the
diagram (each extracted cluster is laid out separately) and by the cluster code of the `Rendering` subsystem. The
charges of the stages are deterministic, so the same graph always uses the same work: the complete acyclic graph on 30
nodes takes 2,955,593 steps and a four-node cyclic graph with a cluster and a label 1,369, both pinned by the subsystem
tests.

#### Error Handling

The constructor throws `ArgumentOutOfRangeException` for a limit below 1. `Charge` throws
`OperationCanceledException` when the token is canceled and the located `MermaidRenderException` of `LimitExceeded`
when the limit would be passed. `Failure` and `LimitExceeded` create the exception without throwing it, so that
`DagreLayout.Run` can throw it in place of a stage's own exception.

#### Dependencies

- **Package root** - `MermaidLocation`, `MermaidRenderException`, `MermaidDiagnosticCodes`, and (by reference in the
  message) `MermaidLimits`
- **.NET base class library** - `System.Threading.CancellationToken`, `System.Globalization.CultureInfo`

The unit has no dependency on another unit of the subsystem.

#### Callers

The flowchart renderer of the `Flowchart` subsystem creates the meter of a diagram from `MermaidLimits.MaxLayoutWork`,
the location of the diagram keyword, and the cancellation token. `ClusterLayout` of the `Rendering` subsystem passes it
to every `DagreLayout.Run` and charges its own stages to it. Every stage of the `Dagre` subsystem charges it, and
`DagreLayout.AddDummyNode` and `DagreAcyclic.Run` name nodes and edges with `UniqueId`. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/LayoutWorkMeterTests.cs` call the unit directly through
`InternalsVisibleTo`.

#### Known Differences From Mermaid

- lodash shares the counter of `_.uniqueId` among all the layouts of a page, so dagre's dummy names depend on what was
  drawn before; the port counts per meter, so a diagram always gets the same names. The names never change the
  geometry: every generated name is a non-index key (so it does not move in the key order) and the conflict pairs that
  compare names are symmetric, which is why goldens recorded in one Node process match.
- The work budget, cancellation, and located failures are additions; Mermaid's dagre has none of them.
