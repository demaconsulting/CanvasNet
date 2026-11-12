### ScenePainter Unit Verification Design

This document describes the unit-level verification strategy for the `ScenePainter` unit in the
`DemaConsulting.CanvasNet.Mermaid.Scenegraph` namespace: the internal `ScenePainter` class (its paths, dashes, drop
shadows, text with its underline bars and rotation, and pictures), the `ScenePlacement` record struct, and the
`ISceneImageLoader` contract.

#### Verification Approach

The unit is verified by painting small scenes built from `SceneModel` records onto real surfaces and asserting the
pixels that their geometry covers, worked out by hand: a filled rectangle has its exact fill color inside, its stroke
color on its edge, and full transparency outside; a dash pattern of 9 on and 5 off leaves x = 4 inked and x = 11
empty; an even-odd ring leaves its hole empty; a scaled rectangle covers twice its size; text anchored in the middle
has its ink centered on the anchor and text anchored at the end ends at the anchor; an underlined run has a bar under
its baseline where the plain run has no ink; a line turned a quarter turn about the origin covers the pixels of the
plain line turned. The underline bar of each bundled face is verified at a font size of
2048 pixels, at which one font unit of the faces is one pixel, against the position and thickness of the face's `post`
table. The placement is verified by
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
asserted. A pixel that differs from the hand-derived color, an underline bar off the face's metrics, a picture
requested at another size, or a limit failure that is missing, unlocated, or late constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Scenegraph-ScenePainter-FillStroke: Fills And Strokes Cover Their Geometry

**Tests**: `ScenePainterTests.ScenePainter_Paint_FilledStrokedRectangle_PaintsFillAndStroke`,
`ScenePainterTests.ScenePainter_Paint_EvenOdd_LeavesHole`

Asserts that a rectangle filled with Mermaid's default node fill and stroked 2 units wide with its default node stroke
has the fill inside, the stroke on its edge, and transparency outside, and that a ring filled under the even-odd rule
has the fill in the ring and an empty hole.

##### CanvasNetMermaid-Scenegraph-ScenePainter-Dashes: Dashes Leave Gaps And Invalid Patterns Are Solid

**Tests**: `ScenePainterTests.ScenePainter_Paint_DashedStroke_LeavesGaps`,
`ScenePainterTests.ScenePainter_Paint_InvalidDash_IsSolid`,
`ScenePainterTests.ScenePainter_Paint_HugeDashOrOffset_DrawsWithoutFailing`

Asserts that a line dashed 9 on and 5 off is inked in its first dash and empty in its first gap, that a pattern
with a negative entry and a pattern finer than a quarter of a pixel are drawn solid, that a pattern with an entry of
1e39 (infinite in single precision) is drawn solid, and that the 9-on 5-off pattern with a dash offset of 1e300 is
still drawn dashed (between 40 and 90 of the 100 pixels of the line inked).

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

##### CanvasNetMermaid-Scenegraph-ScenePainter-DropShadow: Drop Shadows Under Their Paths

**Tests**: `ScenePainterTests.ScenePainter_Paint_SharpShadow_OffsetsSilhouetteUnderShape`,
`ScenePainterTests.ScenePainter_Paint_BlurredShadow_FadesSymmetrically`,
`ScenePainterTests.ScenePainter_Paint_Shadow_ChargedToPaintedPixelLimit`,
`ScenePainterTests.ScenePainter_Paint_ShadowWithHugeStroke_IsTheFillsShadow`,
`ScenePainterTests.ScenePainter_ShadowRegion_SmallShadowOnLargeSurface_IsTheShadowsBounds`,
`ScenePainterTests.ScenePainter_ShadowRegion_ExtremeValues_AreClampedWithoutOverflow`,
`ScenePainterTests.ScenePainter_Paint_Shadow_ChargesItsWholeScratchRegion`

Asserts that a white square from 20 to 60 with a sharp black shadow 5 pixels to the right and down stays white inside,
has the black shadow beside and below it, and nothing beyond the shadow; that a centered shadow with a blur of 2 is
symmetric on both sides of the square (within one level), fades outward, is gone 20 pixels away, and is drawn in its
own color under the white square; and that under a painted-pixel limit of 2,000 the square of 1,600 pixels is painted,
with a transparent shadow too (which paints nothing), while the square with a visible shadow fails with
`mermaid-limit-exceeded`, the shadow's pixels being charged. Asserts that a square with a blurred shadow and a stroke
that is not drawn (a width of 1e300 or 1e30 at scale 1, or 20,000 at scale 2, beyond 32,768 device pixels) paints
without an exception, with its shadow present, and with the same pixels as the square without a stroke. Asserts
through the internal `ShadowRegion` helper that the scratch region of a 20 by 20 box at (4,000, 4,000) on an 8,192 by
8,192 surface, with a stroke reach of 2, an offset of (1, 2), and a blur reach of 15, is (3,984, 3,985) to
(4,038, 4,039): 54 pixels square (20 + 2 x 2 + 2 x 15), the shadow's own bounds and not the surface's; that a stroke
reach of 1e300 or infinity is clamped to the surface (0, 0) to (8,192, 8,192) without overflow; and that an offset of
plus or minus 1e300 or a reach that is not a number gives no region. Asserts that a 20 by 20 box at the corner of a
1,024 by 1,024 surface with a centered shadow of blur 2 (a blur reach of 9) paints under a limit of 400 + 1,444 pixels
and fails with `mermaid-limit-exceeded` under one pixel less: the whole 38 by 38 scratch region is charged, its margins
beyond the surface included, and nothing in proportion to the surface.

##### CanvasNetMermaid-Scenegraph-ScenePainter-StrokedText: Stroked Text Outlines

**Tests**: `ScenePainterTests.ScenePainter_Paint_StrokedText_DrawsTheOutline`

Asserts that a line of text with a transparent fill draws nothing without a stroke and leaves more than 20 inked
pixels with a stroke of width 1, so the outline is stroked over the fill even when the fill is transparent.

##### CanvasNetMermaid-Scenegraph-ScenePainter-Underline: Underlined Runs Get A Bar Under Their Baseline

**Tests**: `ScenePainterTests.ScenePainter_Paint_UnderlinedRun_DrawsABarUnderTheBaseline`,
`ScenePainterTests.ScenePainter_UnderlineBar_BundledFaces_FollowThePostTable`

Paints the capitals `HHH` (no descenders) in Mermaid's default family stack at 32 pixels on the baseline y = 20, plain
and underlined, and asserts that the plain run leaves no ink on the row y = 22 while the underlined run covers 65 to
70 pixels of that row fully (the bar of Liberation Sans spans y = 21.05 to 23.39 under the run's advance). Builds the
underline bar of each bundled face (Liberation Sans, Serif, and Mono, regular and bold; a theory) at 2048 pixels for
a run from x = 10 with an advance of 500 on the baseline y = 100, and asserts that the bar starts at x = 10, is 500
wide, has its top at 100 minus the face's underline position (for example 167 for regular Liberation Sans, whose
position is -67), and is as high as its thickness (150 for regular Liberation Sans, 215 for bold, 100 and 195 for
Serif, 84 and 205 for Mono), to three decimals.

##### CanvasNetMermaid-Scenegraph-ScenePainter-RotatedText: Rotated Text Is Turned About Its Pivot

**Tests**: `ScenePainterTests.ScenePainter_Paint_RotatedText_IsTheLineTurnedAboutItsPivot`,
`ScenePainterTests.ScenePainter_Paint_RotatedText_ChargesItsTurnedBox`,
`ScenePainterTests.ScenePainter_Paint_RotatedTextWithoutAUsablePivotOrAngle_IsSkipped`

Paints the underlined line `Hello` at 16 pixels in a 100 by 30 view box, and the same line turned by 90 degrees about
the origin in the view box (-30, 0, 30, 100), and asserts that every pixel of the turned picture has the coverage of
the plain pixel it turns from (a quarter turn takes x, y to -y, x), its underline bar included, within 8 of 255 levels,
over more than 100 inked pixels. Asserts that the line `HHHH` with a box of 46 by 20 pixels paints under a
painted-pixel limit of 1,500 pixels, while the same line turned by -45 degrees about (40, 50), whose turned box is
about 47 by 47 pixels, fails under that limit with `mermaid-limit-exceeded` and paints ink under a limit of 2,500.
Asserts that lines whose angle is not a number, whose pivot is a billion units away, or whose pivot is infinite are
skipped: the surface stays fully transparent.
