### ScenePositions Unit Verification Design

This document describes the unit-level verification strategy for the `ScenePositions` unit in the
`DemaConsulting.CanvasNet.Mermaid.Scenegraph` namespace: the internal static class that turns the position tags of a
laid-out scene into `ImagePosition` values in the pixels of the surface a placement draws on (`ScenePositions.Report`,
`Collect`, `PathMidpoint`, and `KindOrder`).

#### Verification Approach

The unit is verified on small scenes built by hand from `SceneModel` records, each carrying `ScenePositionTag` tags as
the renderers set them, and its results are compared with values worked out by hand: a tagged node inside a translated
group drawn at a scale and an offset, one edge drawn in two pieces whose combined length puts its midpoint in the
second piece, a symmetric cubic curve and a half-circle arc whose midpoints are at their apex, a pie slice with an
explicit aim point and a value, two parallel edges and two nodes drawn against their kind and line order, and a scene
without tags. The unit depends only on the `SceneModel` unit, the `ScenePlacement` record of the `ScenePainter` unit,
the core `Positions` types, and the core curve flattening, which are real, so no mocking or stubbing is required; the
internal types are reached through the test assembly's `InternalsVisibleTo` access.

The positions that real diagrams report through `MermaidDocument.Render` - every diagram type's kinds, source ids,
and facts, the mapping by the scale and the fit, and the unchanged pixels - are verified by the `MermaidDocument`
position tests (see *MermaidDocument Unit Verification Design*).

Unit tests reside in `ScenePositionsTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Scenegraph` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own scene; no files, fonts, or surfaces are used

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A box, a pixel, an order, or an index that differs from the hand-derived values by more than the stated
precision, or a position reported for an untagged item or without a handler, constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Scenegraph-ScenePositions-Merge: Items Of One Feature Are One Position

**Tests**: `ScenePositionsTests.ScenePositions_Collect_EdgeInPieces_MidpointOfCombinedLength`,
`ScenePositionsTests.ScenePositions_Collect_NestedGroupAndPlacement_MapsBoxAndCenter`

Asserts that two paths with the same `edge` tag, one from (0, 0) to (10, 0) and one from (10, 0) to (10, 30), are one
position with the box (0, 0, 10, 30) and the index 0; and that a node rectangle of 40 by 30 in a group at (10, 20)
inside a group translated by (100, 50) has the box (110, 70, 40, 30) before the placement, both translations applied.

##### CanvasNetMermaid-Scenegraph-ScenePositions-Aim: The Attachment Pixel Follows The Kind

**Tests**: `ScenePositionsTests.ScenePositions_PathMidpoint_SymmetricCubicAndArc_IsAtTheApex`,
`ScenePositionsTests.ScenePositions_Collect_EdgeInPieces_MidpointOfCombinedLength`,
`ScenePositionsTests.ScenePositions_Collect_ExplicitAim_UsesTheAimAndValue`

Asserts that the arc-length midpoint of a cubic curve from (0, 0) to (40, 0) with the control points at y = -40 is
(20, -30) and that of a half circle of radius 20 from (0, 0) to (40, 0) is (20, -20), both within 0.01; that the edge
drawn in two pieces, 10 and 30 long, aims along its path at (10, 10), 10 into the second piece; and that a slice in a
group at (100, 100) aimed at (5, -10) has the pixel (105, 90) rather than the center of its box.

##### CanvasNetMermaid-Scenegraph-ScenePositions-Order: Kind, Line, Then Drawing Order

**Tests**: `ScenePositionsTests.ScenePositions_Collect_KindsLinesAndFilter_OrdersAndNumbers`

Builds a scene that draws the edges `e2` (line 4) and `e1` (line 3) from `A` to `B` before the nodes `B` (line 2) and
`A` (line 1), and asserts that the positions are `A`, `B`, `e1`, and `e2` with the indexes none, none, 0, and 1, and
that a filter that accepts only `edge` gives `e1` and `e2`.

##### CanvasNetMermaid-Scenegraph-ScenePositions-Mapping: The Placement And The Facts Of The Tag

**Tests**: `ScenePositionsTests.ScenePositions_Collect_NestedGroupAndPlacement_MapsBoxAndCenter`,
`ScenePositionsTests.ScenePositions_Collect_ExplicitAim_UsesTheAimAndValue`,
`ScenePositionsTests.ScenePositions_Report_NoHandler_DoesNothing`

Asserts that the nested node drawn at scale 2 with the offset (5, 7) has the kind `node`, the id `A`, the text
`Alpha`, the line 3, no index, the box (225, 147, 80, 60), and the pixel (265, 177) at the center of the box; that the
slice reports the number 386 as its `value`; and that a scene without tags reports nothing, with options that have no
handler and with a handler.
