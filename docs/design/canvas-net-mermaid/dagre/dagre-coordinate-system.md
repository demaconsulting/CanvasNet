### DagreCoordinateSystem

![Dagre Structure](MermaidDagreView.svg)

The `DagreCoordinateSystem` unit is the eighth software unit in the `Dagre` subsystem. It is one source file,
`DagreCoordinateSystem.cs`, in the `DemaConsulting.CanvasNet.Mermaid.Dagre` namespace, and defines the internal static
class `DagreCoordinateSystem`. It ports dagre's `coordinate-system.js`.

#### Purpose

dagre positions every layout top to bottom. `DagreCoordinateSystem` turns a layout of another rank direction on its
side before positioning and back afterwards: for left to right and right to left it swaps the sizes before
positioning, and after positioning it mirrors bottom to top and right to left vertically and transposes left to right
and right to left.

The unit's behavior is specified by the `CanvasNetMermaid-Dagre-DagreCoordinateSystem-*` requirements in
`docs/reqstream/canvas-net-mermaid/dagre/dagre-coordinate-system.yaml`.

#### Data Model

The unit has no state. It reads the graph's `RankDir` in lower case (so `TB`, `BT`, `LR`, and `RL` work in either
case) and changes the `Width`, `Height`, `X`, and `Y` of every node, the `Width` and `Height` of every edge label, the
points of every route, and the label center of every edge that has one.

#### Key Methods

- `Adjust(LayoutGraph g)` - for `lr` and `rl`, swaps the width and height of every node and edge label.
- `Undo(LayoutGraph g)` - for `bt` and `rl`, negates every y (nodes, route points, label centers); then for `lr` and
  `rl`, swaps x and y everywhere and swaps the sizes back.

**Preconditions and postconditions**: `Adjust` is called after the self-edge dummies are inserted and before
positioning; `Undo` after the side labels are fixed up and before the translation to the margins, which removes any
negative coordinates.

#### Error Handling

The unit throws no exception; an unknown or absent direction is treated as top to bottom. It does constant work per
node and edge and does not charge the meter.

#### Dependencies

- **Graphlib subsystem** - `Graph`
- **Dagre subsystem** - the `LayoutModel.cs` labels

#### Callers

`DagreLayout.RunLayout` calls `Adjust` before `DagrePositioning.Position` and `Undo` after `FixupEdgeLabelCoords`. The
tests in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreCoordinateSystemTests.cs` call the unit directly
through `InternalsVisibleTo`.
