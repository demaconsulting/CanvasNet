### SceneModel Unit Verification Design

This document describes the unit-level verification strategy for the `SceneModel` unit in the
`DemaConsulting.CanvasNet.Mermaid.Scenegraph` namespace: the immutable scene records (`SceneRect`, `SceneSegment`,
`SceneStroke`, `SceneFont`, `SceneTextRun`, the items `SceneGroup`, `ScenePath`, `SceneText`, and `SceneImage`, and
`Scene`) with `Scene.BoundsOf` and `Scene.ToPath`.

#### Verification Approach

The unit is verified by building records and comparing the results of their helpers with values worked out by hand:
rectangle unions and offsets; the fields that each segment factory fills; the bounds of a cubic curve whose control
points reach y = -40 while the curve itself reaches y = -30 (three quarters of the control height, from the Bezier
formula at t = 0.5); and the device bounds of a half circle of radius 10 converted at scale 2 and moved by (5, 50),
which reaches 20 device pixels above its chord. The unit depends only on the core `Path` and `PathBuilder`, which
are real, so no mocking or stubbing is required; the internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `SceneModelTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Scenegraph` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of records; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Bounds or coordinates that differ from the hand-derived values by more than the stated precision constitute
a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Scenegraph-SceneModel-Rectangles: Rectangle Arithmetic

**Tests**: `SceneModelTests.SceneRect_UnionAndOffset_ComputeEdges`

Asserts that the union of (0, 0, 10, 5) and (-2, 3, 4, 10) is (-2, 0, 12, 13) and that the first rectangle moved by
(1, 2) has its right edge at 11 and its bottom edge at 7.

##### CanvasNetMermaid-Scenegraph-SceneModel-Segments: Segments Carry SVG Path Commands

**Tests**: `SceneModelTests.SceneSegment_Factories_FillTheDocumentedFields`

Asserts that the quadratic, cubic, and arc factories put the end point, the control points, the radii, the rotation,
and the flags in the documented fields and that the close factory makes a close.

##### CanvasNetMermaid-Scenegraph-SceneModel-Bounds: Bounds Are Those Of An SVG Bounding Box

**Tests**: `SceneModelTests.Scene_BoundsOf_ItemsAndCurves_AreGeometryBounds`

Asserts that the bounds of a cubic curve, a text line, and a picture inside a group translated by (100, 100) run from
(0, -30) to (105, 105) - the top of the curve, not of its control points, and the picture box moved by the group - and
that a list without items and a list with only an empty path have no bounds.

##### CanvasNetMermaid-Scenegraph-SceneModel-PathConversion: Geometry Converts To A Core Path

**Tests**: `SceneModelTests.Scene_ToPath_AppliesPlacementAndArcs`

Converts a half-circle arc that does not begin with a move, followed by a close, at scale 2 with an offset of (5, 50),
and asserts that the path starts at the placed origin and spans x from 5 to 45 and y from 30 to 50.
