### DagreNormalizer

![Dagre Structure](MermaidDagreView.svg)

The `DagreNormalizer` unit is the fifth software unit in the `Dagre` subsystem. It is one source file,
`DagreNormalizer.cs`, in the `DemaConsulting.CanvasNet.Mermaid.Dagre` namespace, and defines the internal static class
`DagreNormalizer`. It ports dagre's `normalize.js`.

#### Purpose

Ordering and positioning work on edges between adjacent ranks. `DagreNormalizer` splits every edge that spans more than
one rank into a chain of one-rank edges through dummy nodes, one of which carries the edge's label at its label rank,
and after positioning turns each chain back into its edge, with the positions of the dummies as the bends of the route
and the position of the label dummy as the label's.

The unit's behavior is specified by the `CanvasNetMermaid-Dagre-DagreNormalizer-*` requirements in
`docs/reqstream/canvas-net-mermaid/dagre/dagre-normalizer.yaml`.

#### Data Model

A dummy is a `LayoutNode` of kind `edge` with the original `EdgeLabel` and `EdgeObj` and its rank, and of size 0; the
dummy at the edge's `LabelRank` is of kind `edge-label` and has the label's size and `LabelPos`. Each edge of a chain is
a new `LayoutEdge` with the original weight and the original name. The first dummy of every chain is recorded in
`LayoutGraphLabel.DummyChains`.

#### Key Methods

- `Run(LayoutGraph g, LayoutWorkMeter meter)` - clears the dummy chains and, for every edge whose head rank is not its
  tail rank plus one, removes it and adds the chain of dummies (named `_d` by the meter) from the rank after the tail to
  the rank before the head.
- `Undo(LayoutGraph g, LayoutWorkMeter meter)` - for every chain, puts the original edge back with its label, follows
  the chain removing each dummy and appending its position to the route, and copies the position and size of the label
  dummy to the label.

**Preconditions and postconditions**: `Run` is called on the ranked graph after the edge-label proxies are removed and
the ranks normalized; `Undo` after positioning and the removal of border nodes. Only whole ranks equal to the label
rank get a label dummy: the half-rank label rank that dagre's quirk leaves on some edges (see _Dagre Subsystem Design_)
never matches, so such a label gets no position, exactly as in dagre.

#### Error Handling

The unit throws no exception of its own. Every edge and every dummy charges the meter one step, in both directions.

#### Dependencies

- **Graphlib subsystem** - `Graph` and `GraphEdge`
- **Dagre subsystem** - `DagreLayout` (`AddDummyNode`, `Same`) and `Lodash` (`Num`), and the `LayoutModel.cs` labels;
  `LayoutWorkMeter`

#### Callers

`DagreLayout.RunLayout` calls `Run` after the ranks are final and `Undo` after the border nodes are removed. The tests
in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreNormalizerTests.cs` call the unit directly through
`InternalsVisibleTo`.
