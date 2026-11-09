### ScenePainter Unit Verification Design

This document describes the unit-level verification strategy for the `ScenePainter` unit in the
`DemaConsulting.CanvasNet.Mermaid.Scenegraph` namespace: the internal `ScenePainter` class (its paths, dashes, text,
and pictures), the `ScenePlacement` record struct, and the `ISceneImageLoader` contract.

#### Verification Approach

The unit is verified by painting small scenes built from `SceneModel` records onto real surfaces and asserting the
pixels that their geometry covers, worked out by hand: a filled rectangle has its exact fill color inside, its stroke
color on its edge, and full transparency outside; a dash pattern of 9 on and 5 off leaves x = 4 inked and x = 11
empty; an even-odd ring leaves its hole empty; a scaled rectangle covers twice its size; text anchored in the middle
has its ink centered on the anchor and text anchored at the end ends at the anchor. The placement is verified by
comparing `ScenePlacement.Fit` with the scale and offsets computed by hand. The painted-pixel limit is verified with
limits just under and just over the device area of two 10 by 10 fills.

Pictures are verified with a test double of `ISceneImageLoader` (`SolidLoader`, a private class of the test) that
returns solid surfaces, either at the requested size or at a fixed size of its own, and records the requests. No
other mocking or stubbing is used; the core `Surface`, `PathFiller`, `PathStroker`, `TextRenderer`, the font resolver,
and the bundled fonts are real. The internal types are reached through the test assembly's `InternalsVisibleTo`
access.

Unit tests reside in `ScenePainterTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Scenegraph` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: A recording picture loader for the picture scenarios; nothing else
- **Fonts**: The bundled Liberation fonts, through the resolver of `ScenePainter.CreateFontResolver`
- **Isolation**: Each test builds its own scene, painter, and surface; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A pixel that differs from the hand-derived color, a picture requested at another size, or a limit failure
that is missing, unlocated, or late constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Scenegraph-ScenePainter-FillStroke: Fills And Strokes Cover Their Geometry

**Tests**: `ScenePainterTests.ScenePainter_Paint_FilledStrokedRectangle_PaintsFillAndStroke`,
`ScenePainterTests.ScenePainter_Paint_EvenOdd_LeavesHole`

Asserts that a rectangle filled with Mermaid's default node fill and stroked 2 units wide with its default node stroke
has the fill inside, the stroke on its edge, and transparency outside, and that a ring filled under the even-odd rule
has the fill in the ring and an empty hole.

##### CanvasNetMermaid-Scenegraph-ScenePainter-Dashes: Dashes Leave Gaps And Invalid Patterns Are Solid

**Tests**: `ScenePainterTests.ScenePainter_Paint_DashedStroke_LeavesGaps`,
`ScenePainterTests.ScenePainter_Paint_InvalidDash_IsSolid`

Asserts that a line dashed 9 on and 5 off is inked in its first dash and empty in its first gap, and that a pattern
with a negative entry and a pattern finer than a quarter of a pixel are drawn solid.

##### CanvasNetMermaid-Scenegraph-ScenePainter-Text: Text Runs Are Drawn In The Bundled Fonts At Their Anchor

**Tests**: `ScenePainterTests.ScenePainter_Paint_MiddleAnchoredText_IsCentered`,
`ScenePainterTests.ScenePainter_Paint_EndAnchoredRuns_EndAtAnchor`

Asserts that a line in Mermaid's default family stack anchored in the middle at x = 50 has its ink centered within
two pixels of the anchor, and that a regular run followed by a bold run anchored at the end at x = 90 has its last
ink between x = 85 and x = 90.

##### CanvasNetMermaid-Scenegraph-ScenePainter-Placement: Background, Scale, And Fit

**Tests**: `ScenePainterTests.ScenePainter_Paint_ScaledWithBackground_ScalesGeometry`,
`ScenePainterTests.ScenePlacement_Fit_ScalesAndCenters`,
`ScenePainterTests.ScenePainter_Paint_FilledStrokedRectangle_PaintsFillAndStroke`

Asserts that a 10 by 10 rectangle painted at scale 2 over a white background covers 20 by 20 pixels with the white
background beyond it; that a view box of 20 by 10 at (10, 0) fitted into 100 by 100 has the scale 5 and the offsets
(-50, 25); that a view box without area is placed unscaled at the origin; and that a view box starting at (-10, -10)
maps its origin to the surface origin.

##### CanvasNetMermaid-Scenegraph-ScenePainter-Images: Pictures Are Loaded At Device Size And Fill Their Box

**Tests**: `ScenePainterTests.ScenePainter_Paint_Image_LoadsAtDeviceSizeAndComposites`,
`ScenePainterTests.ScenePainter_Paint_ImageAtItsOwnSize_IsStretchedToTheBox`,
`ScenePainterTests.ScenePainter_Paint_ImageWithoutLoader_DrawsNothing`

Asserts that a picture with a box 10.5 by 6 at (2, 2) is requested once at 11 by 6 device pixels and composited with
half opacity inside its box and nothing outside; that a 2 by 1 picture returned for a 12 by 8 box covers the box from
corner to corner and nothing outside it; and that a painter without a loader leaves the surface fully transparent.

##### CanvasNetMermaid-Scenegraph-ScenePainter-PaintedPixelLimit: The Item That Passes The Limit Fails, Located

**Tests**: `ScenePainterTests.ScenePainter_Paint_OverPaintedPixelLimit_ThrowsLimitExceeded`

Asserts that two 10 by 10 fills against a limit of 150 pixels fail with `MermaidRenderException` with the code
`mermaid-limit-exceeded`, the location of the second item, and a message that names `MermaidLimits.MaxPaintedPixels`,
and that the same scene paints both fills under a limit of 200 pixels.

##### CanvasNetMermaid-Scenegraph-ScenePainter-UnboundedGeometry: Unbounded Geometry Is Skipped

**Tests**: `ScenePainterTests.ScenePainter_Paint_UnboundedGeometry_IsSkipped`

Paints a rectangle a billion units away, a rectangle with a coordinate that is not a number, and a rectangle in a
group translated by infinity under a limit of one pixel, and asserts that nothing is painted and nothing is charged.

##### CanvasNetMermaid-Scenegraph-ScenePainter-Cancellation: A Canceled Token Stops The Painting

**Tests**: `ScenePainterTests.ScenePainter_Paint_Canceled_Throws`

Asserts that painting a scene with a token that is already canceled throws `OperationCanceledException`.
