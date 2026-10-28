### ScenePainter Unit Verification Design

This document describes the unit-level verification strategy for the `ScenePainter` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace: the internal `ScenePainter` class (the two `Paint`
overloads, one returning a new `Surface` and one painting into an existing surface, and the internal `SymbolPath`) and
the `ScenePlacement` record (`Uniform` and `Fit`). The painter draws a `Scene` to a `Surface` in drawing order: the
background, group backgrounds and borders, rectangles, rules, polylines and polygons, symbols, and text, with fill,
stroke, dash, opacity, rotation, uniform scale, and clipping, using the core path filler, path stroker, and text
renderer.

#### Verification Approach

The unit is verified by painting small scenes built from the model records and asserting on the pixels of the
returned `Surface`, with expectations derived by hand from the geometry. The collaborators (the core `Surface`,
`PathFiller`, `PathStroker`, `TextRenderer`, the bundled fonts, `CssColor`, and `TextMeasurer`) are real, because
the unit's contract is what its items cover; no mocking or stubbing is used. The internal types are reached through the
test assembly's `InternalsVisibleTo` access.

The pixel expectations are analytic:

- A filled rectangle at (2, 2) of 8 by 8 pixels covers exactly the pixels 2 to 9 in each direction in its exact color,
  and (1, 1) and (10, 10) stay white. A 2 pixel black stroke on a rectangle at (15, 5) of 20 by 20 is centered on the
  edge: the pixels with x from 14 to 15 are black, from 16 on the interior is green, and 13 is white; the same holds
  for the other edges (the corner at (34, 24) is black, (33, 23) is green, and (36, 26) is white).
- Opacity: a fill of `#0000ff` at 0.5 opacity over white is 255 x (1 - 128 / 255) = 127 in the red and green channels,
  so the pixel is (127, 127, 255), and a half-transparent black stroke gives (127, 127, 127) with the inside of the
  unfilled rectangle staying white.
- A 2 pixel rule at y = 35 from x = 0 to 30 covers rows 34 and 35 and the pixels 0 to 29, with row 33, row 36, and
  column 30 white; a dashed rule with a dash of 4 on and 4 off and butt caps is black at x = 0 to 3 and 8 to 11 and
  white at 4 to 7 and 12 to 15 (the edges fall on pixel boundaries).
- Clip: a group at (5, 5) clipped to 10 by 10 holding a far larger rectangle paints exactly [5, 15) in each
  direction; at scale 2 the clip is [10, 30); a rectangle at (1, 1) of size 4 at scale 3 covers [3, 15); fitting a
  10 by 10 scene into 40 by 20 scales by 2 and centers horizontally with an offset of 10, so the rectangle covers
  [12, 20) by [2, 10); a clip on [2.5, 7.5) by [0, 5) is half covering at its edge columns (red over white gives 127
  or 128 in green and blue) and full inside, and an inner clip cuts the outer one to the intersection [8, 14) by
  [8, 14).
- Symbols: a square of size (area) 100 has side 10 and covers [5, 15) by [5, 15); rotated by 45 degrees its corners
  move to the middle of the sides at a distance of 5 x sqrt(2) = 7.07; a circle of area 100 has diameter 10, with its
  center in and the corners of its box out and an area within the anti-aliasing error of the circle of radius 5; for
  size 64 the radius is sqrt(64) / 2 = 4 and a triangle is sqrt(3) / 2 x 4 = 3.4641 high on each side of the center; a
  size that is not positive has no shape.
- Text: with a baseline at y = 20 the ink of "Hello" starts at the anchor and sits on the baseline (no letter
  descends); with a `top` baseline at 5 for a 12 pixel font the offset is round(0.79 x 12) = round(9.48) = 9, so the
  baseline is at 5 + 9 = 14; rotated by -90 degrees the run reads upward (taller than wide); center and end alignment
  put the anchor at the middle and the end of the run; at scale 2 the text is twice as large; painting twice gives
  identical pixels.

Unit tests reside in `ScenePainterTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Scenegraph` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts through a real `TextMeasurer`
- **Isolation**: Each test builds its own scene and surface and disposes the surface; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any pixel outside its analytic expectation (exact for integer-aligned geometry; within the stated anti-aliasing
tolerance for curves and fractional clips), any item that draws where nothing is documented, any non-deterministic
output, or any wrong exception type constitutes a failure.

#### Unit-Level Test Scenarios

##### Fill, Stroke, Opacity, Dashes, And Rules

**Tests**: `ScenePainterTests.ScenePainter_Paint_RectAndRuleHonorsFillStrokeOpacity`

Asserts the derived pixels of a filled rectangle, a stroked rectangle, a half-transparent fill and stroke, an item
opacity, a solid rule, and a dashed rule.

##### No Paint Draws Nothing And The Background Fills The Surface

**Tests**: `ScenePainterTests.ScenePainter_Paint_NoPaintDrawsNothingAndBackgroundFillsTheSurface`

Asserts that items with no paint (`none`, an unparseable color, `transparent`), zero opacity, zero width, a zero stroke
width, a NaN coordinate, a degenerate rule, blank text, and text without a fill leave nothing but the white
background; that a translucent background (`rgba(255, 0, 0, 0.5)`) keeps its alpha on the surface, giving
(255, 0, 0, 128); and that a scene without a background is fully transparent.

##### Clip, Scale, And Placement

**Tests**: `ScenePainterTests.ScenePainter_Paint_ClipAndScaleMatchAnalyticPixels`,
`ScenePainterTests.ScenePainter_Paint_FractionalAndNestedClipsUseCoverage`

Asserts the derived clipped, scaled, and fitted pixels, the half-covered edge pixels of a fractional clip, and the
intersection of nested clips.

##### Symbols

**Tests**: `ScenePainterTests.ScenePainter_Paint_SymbolsCoverTheirDocumentedArea`,
`ScenePainterTests.ScenePainter_SymbolPath_ShapesHaveVegaBounds`

Asserts the derived square, rotated square, and circle coverage and area; the bounding boxes for size 64 (radius 4) of
`square`, `cross`, `diamond`, and `circle` (-4 to 4 in both directions), of `triangle-up` and `triangle-down` (-4 to 4
by minus and plus 3.4641), of `triangle-right` and `triangle-left` (the transpose), of `stroke` (-4 to 4 by 0 to 0), and
of an unknown shape (drawn as a circle); that `arrow`, `wedge`, and `triangle` have shapes; and that a size that is not
positive has no shape.

##### Paths, Rounded Rectangles, And Group Backgrounds

**Tests**: `ScenePainterTests.ScenePainter_Paint_PathsRoundedRectanglesAndGroupBackgroundAreCrisp`

Asserts that a rounded rectangle has its corner cut away while the middle of its edges is covered, that an open polyline
is stroked along both legs (a 2 pixel stroke covers 1 pixel on each side), that a closed triangle is filled inside and not
outside its hypotenuse, and that the one pixel border of a group is offset by half a pixel so that it covers exactly one
pixel column and row.

##### Text

**Tests**: `ScenePainterTests.ScenePainter_Paint_TextUsesBaselineRotationAndScale`,
`ScenePainterTests.ScenePainter_Paint_TextLimitTruncatesTheDrawnLine`

Asserts the derived text placement, rotation, alignment, scaling, and determinism, and that a limit of 60 pixels
truncates the drawn line so that its ink ends between 40 and 60 pixels while the same line without a limit is wider than
100 pixels.

##### Invalid Arguments And Cancellation

**Tests**: `ScenePainterTests.ScenePainter_Paint_InvalidArgumentsAndCancellation`

Asserts that a null measurer, scene, or surface throws `ArgumentNullException`, that a placement scale of zero or NaN
throws `ArgumentOutOfRangeException`, that a canceled token throws `OperationCanceledException`, and that fitting a
scene of zero width gives the identity placement (scale 1, offsets 0).
