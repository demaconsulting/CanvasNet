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
tests. The meter bounds the date and tick work of a chart, the force-directed layout of a mindmap, and the row split and
texts of a packet diagram as well as the work of a graph layout.

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

Every caller creates the meter of a diagram from `MermaidLimits.MaxLayoutWork`, the location of the diagram keyword,
and the cancellation token.

- The flowchart, class, state, and ER models create a meter for their layout (`FlowchartModel.Render`, and the
  `LayoutData` of `ClassModel`, `StateModel`, and `ErModel`). `ClusterLayout` of the `Rendering` subsystem passes it to
  every `DagreLayout.Run` and charges its own stages to it. Every stage of the `Dagre` subsystem charges it, and
  `DagreLayout.AddDummyNode` and `DagreAcyclic.Run` name nodes and edges with `UniqueId`.
- `SequenceLayout` creates a meter in its constructor and charges the message loops and the scans of
  `SequenceBounds`.
- `PieRenderer` creates a meter and charges the sections and every text it measures.
- `PacketRenderer` creates a meter for each render; `PacketDiagram.Rows` charges 1 for each block and 1 for each part of
  the row split, and the renderer charges 1 plus the length of each text it measures.
- `MindmapRenderer` creates a meter for each render and charges the node data (nodes plus edges plus 1), each label
  (its length plus 1, before it is checked against `MaxLabelLength`), the measuring of every label through its
  `LabelBuilder`, and 4 for each drawn edge; it passes the meter to `CoseLayout.Run`. A mindmap of three nodes takes
  1,271 units.
- `CoseLayout` (the `Cose` subsystem) charges every step of the layout to the meter it is given: the input (nodes plus
  edges plus 1), the forest search (1 plus the edges of each visited node), the center search (the list length on each
  pass), the radial placement (1 plus the edges of each placed node), each iteration (nodes plus edges plus 1), each
  node's repulsion pairs, each grid (its columns times rows, before it is filled), the cells a node is put in, the
  surrounding cells scanned and their entries, each transcendental function (1 plus each extra precision step), and the
  edge points (edges plus 1). It raises the meter's `Failure` (`mermaid-render-failed`) for a grid JavaScript could not
  build or a node outside it and for a math argument outside its domain, and its `LimitExceeded()` when the radial
  recursion would exhaust the stack. A tree of two nodes takes 903 units and a path of ten nodes 5,598.
- `GanttRenderer` creates a meter and passes it to `GanttModel.Compile`, which charges each pass and task, each date
  text, the `Dayjs.Format` and `Dayjs.ParseStrict` work (by the lengths of the text and the format), and `DayCost()`
  for every day of the excluded-days loop (one unit, the date format, and the `excludes` and `includes` tokens). The
  renderer itself charges every tick step of `TimeAxis`, each axis label, `DayCost()` for every day of the excluded-day
  shading, the task lists, and its text measuring.

The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/LayoutWorkMeterTests.cs` call the unit directly through
`InternalsVisibleTo`.

#### Known Differences From Mermaid

- lodash shares the counter of `_.uniqueId` among all the layouts of a page, so dagre's dummy names depend on what was
  drawn before; the port counts per meter, so a diagram always gets the same names. The names never change the
  geometry: every generated name is a non-index key (so it does not move in the key order) and the conflict pairs that
  compare names are symmetric, which is why goldens recorded in one Node process match.
- The work budget, cancellation, and located failures are additions; Mermaid's dagre has none of them.
