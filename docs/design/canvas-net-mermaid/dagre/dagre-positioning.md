### DagrePositioning

![Dagre Structure](MermaidDagreView.svg)

The `DagrePositioning` unit is the seventh software unit in the `Dagre` subsystem, in the
`DemaConsulting.CanvasNet.Mermaid.Dagre` namespace. It is the internal static partial class `DagrePositioning` in two
source files: `DagrePositioning.cs` (the entry point and the vertical coordinates) and
`DagrePositioning.BrandesKoepf.cs` (the horizontal coordinates). It ports dagre's `position/index.js` and
`position/bk.js`.

#### Purpose

`DagrePositioning` gives every node of the ordered layout graph its center. The vertical coordinate follows from the
ranks; the horizontal coordinate comes from Brandes and Koepf's "Fast and Simple Horizontal Coordinate Assignment",
which keeps long edges straight, aligns nodes with their median neighbors, and packs the result as tightly as the node
and edge separations allow. The port follows dagre's version of the method step by step, so that every coordinate
equals dagre's.

The unit's behavior is specified by the `CanvasNetMermaid-Dagre-DagrePositioning-*` requirements in
`docs/reqstream/canvas-net-mermaid/dagre/dagre-positioning.yaml`.

#### Data Model

The results are `LayoutNode.X` and `Y` of every node of the flat copy of the layout graph (which shares the labels).
Conflicts are a set of unordered node pairs. An alignment is a pair of maps, `Root` (the block of each node) and `Align`
(the next node of its block); a coordinate assignment is a map from node name to x. Four assignments are computed,
named `ul`, `ur`, `dl`, and `dr` (up or down, left or right). The block graph of a compaction is a `Graphlib` graph
whose edges carry the least separation between two blocks.

#### Key Methods

- `Position(LayoutGraph g, LayoutWorkMeter meter)` - on the flat copy, `PositionY` then `PositionX`.
- `PositionY` - stacks the ranks: each as tall as its tallest node, separated by the rank separation, each node
  centered in its rank.
- `PositionX` - marks the conflicts, then for each of the four directions orders the layering accordingly, aligns
  (`VerticalAlignment`), compacts (`HorizontalCompaction`), and mirrors back; then aligns the four to the narrowest
  (`FindSmallestWidthAlignment`, `AlignCoordinates`) and combines them (`Balance`).
- `FindType1Conflicts` - marks each edge that crosses an inner segment (an edge between two dummy nodes).
- `FindType2Conflicts` - marks each inner segment that crosses a border segment of a cluster.
- `VerticalAlignment` - aligns each node with its median neighbor (the left one of two medians when possible, else the
  right), skipping marked conflicts and alignments that would cross an earlier one, with the neighbors taken by
  position.
- `HorizontalCompaction` - places the blocks as far left as the separations allow (`BuildBlockGraph`, `Sep`: the half
  widths of the two neighbors plus half the separation of each - the node separation for a real node, the edge
  separation for a dummy - shifted by half a width for a label dummy whose label sits at the left or right), then
  moves each block right toward its successors where that is free.
- `FindSmallestWidthAlignment`, `AlignCoordinates`, `Balance` - the narrowest assignment (node widths included) is the
  reference; the left assignments are shifted to its left edge and the right ones to its right edge; each node's x is
  the average of its two middle values of the four, or the value of the assignment the graph's `align` names.

**Preconditions and postconditions**: `Position` is called after ordering, the self-edge dummies, and the coordinate
system adjustment; afterwards every node has finite coordinates. The faithful quirk of `FindType2Conflicts` is kept:
dagre scans the border segments of a rank inside the loop over its nodes, as its code does.

#### Error Handling

`Balance` throws `InvalidOperationException` for an `align` that is not one of `UL`, `UR`, `DL`, or `DR` in any case
(where dagre would read a missing assignment and fail), reported by `DagreLayout.Run` as `mermaid-render-failed`.
Mermaid never sets `align`. The unit charges the meter: the vertical pass the nodes and ranks, the horizontal pass
twice the nodes and edges plus one step per neighbor pair scanned for conflicts.

#### Dependencies

- **Graphlib subsystem** - `Graph`
- **Dagre subsystem** - `DagreLayout` (`AsNonCompoundGraph`, `BuildLayerMatrix`, `Truthy`) and `Lodash` (`Min`, `Max`,
  `MinBy`, `SortBy`, `Num`), and the `LayoutModel.cs` labels; `LayoutWorkMeter`
- **.NET base class library** - `System.Linq`

#### Callers

`DagreLayout.RunLayout` calls `Position` between the two coordinate-system steps. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagrePositioningTests.cs` call the unit directly through
`InternalsVisibleTo`.
