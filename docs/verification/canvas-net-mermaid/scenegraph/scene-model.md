### SceneModel Unit Verification Design

This document describes the unit-level verification strategy for the `SceneModel` unit in the
`DemaConsulting.CanvasNet.Mermaid.Scenegraph` namespace: the immutable scene records (`SceneRect`, `SceneSegment`,
`SceneStroke`, `SceneShadow`, `SceneFont`, `SceneTextRun` (with its underline), `SceneRotation`, the items
`SceneGroup`, `ScenePath`, `SceneText` (with its rotation), and `SceneImage`, and `Scene`) with `Scene.BoundsOf` and
`Scene.ToPath`.

#### Verification Approach

The unit is verified by building records and comparing the results of their helpers with values worked out by hand:
rectangle unions and offsets; the fields that each segment factory fills; the bounds of a cubic curve whose control
points reach y = -40 while the curve itself reaches y = -30 (three quarters of the control height, from the Bezier
formula at t = 0.5); the device bounds of a half circle of radius 10 converted at scale 2 and moved by (5, 50),
which reaches 20 device pixels above its chord; the underline flag of a text run, off by default and without effect
on the bounds of its line; and the rotation of a text line, none by default and without effect on its bounds. The
unit depends only on the core `Path` and `PathBuilder`, which
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

##### CanvasNetMermaid-Scenegraph-SceneModel-Shadow: A Path Can Carry A Drop Shadow

**Tests**: `SceneModelTests.ScenePath_Shadow_DefaultsToNoneAndKeepsBounds`

Asserts that a path has no drop shadow unless one is given, that a path given the shadow (1, 2, 2, `#B9B9B9`) carries
it, and that the shadow does not change the bounds of the path, as SVG's `getBBox` ignores filters.

##### CanvasNetMermaid-Scenegraph-SceneModel-Underline: A Text Run Can Be Underlined

**Tests**: `SceneModelTests.SceneTextRun_Underline_DefaultsToFalse`

Asserts that a text run is not underlined unless marked, that a copy marked underlined is, and that a text line of the
underlined run keeps the box it was given (0, 0, 8, 18) as its bounds, so the underline does not change what
`Scene.BoundsOf` reports.

##### CanvasNetMermaid-Scenegraph-SceneModel-Rotation: A Text Line Can Be Turned

**Tests**: `SceneModelTests.SceneText_Rotation_DefaultsToNoneAndKeepsTheBox`

Asserts that a text line has no rotation unless one is given, that a copy given the rotation
`SceneRotation(-45, 10, 20)` carries the angle of -45 degrees and the pivot (10, 20), as SVG's `rotate(-45, 10, 20)`
gives them, and that the turned line keeps the box it was given as its bounds, so the rotation does not change what
`Scene.BoundsOf` reports; the producer gives the box it means to be measured.
