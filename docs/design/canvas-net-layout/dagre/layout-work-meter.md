### LayoutWorkMeter

![Dagre Structure](LayoutDagreView.svg)

The `LayoutWorkMeter` unit is the ninth software unit in the `Dagre` subsystem. It is one source file,
`LayoutWorkMeter.cs`, in the `DemaConsulting.CanvasNet.Layout.Dagre` namespace, and defines the internal abstract class
`LayoutWorkMeter`. The work budget is original; the counter of `UniqueId` reproduces lodash's `_.uniqueId`.

#### Purpose

`LayoutWorkMeter` bounds the layout of one diagram. Every iterative stage of the layout charges its steps to the meter,
which also checks for cancellation, so a pathological graph from untrusted text stops early at a documented limit
instead of running for minutes, and a canceled call stops at the next charge. The meter also holds the counter that
names dagre's dummy nodes.

The meter is **abstract** because the failures of a layout belong to its consumer: each diagram package derives its
own meter, whose `Failure` and `LimitExceeded` create the package's own located exceptions (its diagnostic codes, the
name of its limit, and the location of the diagram), and whose `IsFailure` recognizes them. The layout package
therefore reports errors in the words of the package that called it and needs no reference to any diagram package.
Mermaid's meter is `MermaidLayoutWorkMeter` of the `MermaidDocument` unit (see *MermaidDocument Unit Design*); the
tests use a test meter.

The unit's behavior is specified by the `CanvasNetLayout-Dagre-LayoutWorkMeter-*` requirements in
`docs/reqstream/canvas-net-layout/dagre/layout-work-meter.yaml`.

#### Data Model

- `MaxWork` - the most work allowed, at least 1 (Mermaid gives `MermaidLimits.MaxLayoutWork`, 50,000,000 by default).
- `Used` - the work charged so far; set to `MaxWork` when a charge fails.
- The cancellation token of the call and the last number given out by `UniqueId`. A derived meter adds what its
  failures need (Mermaid's keeps the location of the diagram keyword).

#### Key Methods

- `LayoutWorkMeter(long maxWork, CancellationToken cancellationToken)` - the protected constructor of a derived meter.
- `Charge(long units)` - checks the cancellation token, then adds the units, or throws `LimitExceeded()` when they
  would pass `MaxWork` (the comparison `units > MaxWork - Used` cannot overflow).
- `UniqueId(string prefix)` - the prefix followed by the next number of one counter shared by every prefix, in the
  invariant culture, as lodash's `_.uniqueId` gives.
- `Failure(string description)` (abstract) - the consumer's render failure of a layout that dagre cannot complete,
  carrying the description (Mermaid: a `MermaidRenderException` with the code `mermaid-render-failed`, the message
  "the layout failed: " and the description, at the meter's location).
- `LimitExceeded()` (abstract) - the consumer's limit failure (Mermaid: a `MermaidRenderException` with the code
  `mermaid-limit-exceeded`, a message that names the limit and `MermaidLimits.MaxLayoutWork`, the meter's location,
  and a hint to simplify the diagram or raise the limit).
- `IsFailure(Exception exception)` (abstract) - whether an exception is one of the consumer's own failures, which
  `DagreLayout.Run` lets pass unchanged instead of wrapping it with `Failure` (Mermaid: every
  `MermaidRenderException`). A consumer's failures derive from `InvalidOperationException` as dagre's own errors do,
  so this test is what tells them apart.

**Preconditions and postconditions**: one meter serves one diagram on one thread and is shared by every layout of the
diagram (each extracted cluster is laid out separately) and by the consumer's own code (in Mermaid, the cluster code
of the `Rendering` subsystem). The charges of the stages are deterministic, so the same graph always uses the same
work: the complete acyclic graph on 30 nodes takes 2,955,593 steps and a four-node cyclic graph with a cluster and a
label 1,369, both pinned by the subsystem tests. In Mermaid the meter also bounds the date and tick work of a chart,
the force-directed layout of a mindmap, the row split and texts of a packet diagram, and the placing, arrows, lanes,
and texts of a git graph as well as the work of a graph layout.

#### Error Handling

The constructor throws `ArgumentOutOfRangeException` for a limit below 1. `Charge` throws
`OperationCanceledException` when the token is canceled and the exception of `LimitExceeded` when the limit would be
passed. `Failure` and `LimitExceeded` create the exception without throwing it, so that `DagreLayout.Run` can throw it
in place of a stage's own exception; a derived meter must return an exception that its `IsFailure` recognizes.

#### Dependencies

- **.NET base class library** - `System.Threading.CancellationToken`, `System.Globalization.CultureInfo`

The unit has no dependency on another unit of the subsystem and none on any consumer.

#### Callers

Every stage of the `Dagre` subsystem charges the meter, and `DagreLayout.AddDummyNode` and `DagreAcyclic.Run` name
nodes and edges with `UniqueId`. In the CanvasNetMermaid system (through `InternalsVisibleTo`), every caller creates the
`MermaidLayoutWorkMeter` of a diagram from `MermaidLimits.MaxLayoutWork`, the location of the diagram keyword, and the
cancellation token, and passes it on as a `LayoutWorkMeter`:

- The flowchart, class, state, and ER models create a meter for their layout (`FlowchartModel.Render`, and the
  `LayoutData` of `ClassModel`, `StateModel`, and `ErModel`). `ClusterLayout` of the `Rendering` subsystem passes it to
  every `DagreLayout.Run` and charges its own stages to it.
- `SequenceLayout` creates a meter in its constructor and charges the message loops and the scans of
  `SequenceBounds`.
- `PieRenderer` creates a meter and charges the sections and every text it measures.
- `PacketRenderer` creates a meter for each render; `PacketDiagram.Rows` charges 1 for each block and 1 for each part of
  the row split, and the renderer charges 1 plus the length of each text it measures.
- `GitGraphRenderer` creates a meter for each render and charges 1 for each branch, for each commit of each placing
  pass, for each parent it looks at, and for each visit of the bottom-to-top loops of `parallelCommits`; 1 plus the
  number of commits for each arrow (its rerouting test); 1 plus the number of lanes for each lane try; and 1 plus the
  length of each text it measures or draws.
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

The tests in `test/DemaConsulting.CanvasNet.Layout.Tests/Dagre/LayoutWorkMeterTests.cs` call the unit directly
through `InternalsVisibleTo`, with the test meter `TestLayoutWorkMeter`.

#### Known Differences From Mermaid

- lodash shares the counter of `_.uniqueId` among all the layouts of a page, so dagre's dummy names depend on what was
  drawn before; the port counts per meter, so a diagram always gets the same names. The names never change the
  geometry: every generated name is a non-index key (so it does not move in the key order) and the conflict pairs that
  compare names are symmetric, which is why goldens recorded in one Node process match.
- The work budget, cancellation, and located failures are additions; Mermaid's dagre has none of them.
