### ScenePainter Unit Verification Design

This document describes the unit-level verification strategy for the `ScenePainter` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace: the internal `ScenePainter` class (the two `Paint`
overloads, one returning a new `Surface` and one painting into an existing surface, and the internal `SymbolPath`) and
the `ScenePlacement` record (`Uniform` and `Fit`). The painter draws a `Scene` to a `Surface` in drawing order: the
background, group backgrounds and borders, rectangles, rules, polylines and polygons, paths of curved segments, arcs,
trails, symbols, and text, with fill,
stroke, dash, cap, join, miter limit, opacity, rotation, uniform scale, and clipping, using the core path filler, path
stroker, and text renderer. The painter is a partial class: the paths of segments, the arcs, the trails, and the miter
limit are in `ScenePainter.Paths.cs`, and the outline of an arc is built by the `ArcGeometry` unit (see the
_ArcGeometry Unit Verification Design_).

#### Verification Approach

The unit is verified by painting small scenes built from the model records and asserting on the pixels of the
returned `Surface`, with expectations derived by hand from the geometry. The collaborators (the core `Surface`,
`PathFiller`, `PathStroker`, `TextRenderer`, the bundled fonts, `CssPaint`, the core `CssColor`, and
`TextMeasurer`) are real, because
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
- Gradients: a 100 by 20 rectangle at (10, 10) filled from red to blue runs along its box, so a pixel column whose
  center
  is at distance d from the left edge has the fraction d / 100 of the way: the column at 59 (center 59.5) is 0.495,
  which is red 255 x 0.505 = 128.8 and blue 255 x 0.495 = 126.2, and the adjacent columns differ by 2.55 levels (the
  assertions allow a few levels for rounding); the first and last columns are within 3 levels of the end colors, and
  the pixels outside the box stay white. A 20 by 100 legend gradient from black (bottom) to white (top) is dark at the
  bottom row and light at the top row, and the same in every column. Stops at 0.25 and 0.75 leave the first color
  exactly on the first quarter and the last exactly on the last quarter (the end colors continue), and the middle is the
  mean of the two colors; the stops red 0, red 0.5, blue 0.5, blue 1 give a hard step with red exactly up to column 59
  and blue exactly from column 60 of a box that starts at 10. A circle of size 400 (20 by 20) centered at (60, 20) has
  its
  gradient over its box (x from 50 to 70), so the center column is 0.525 of the way (134) while the corners of the box
  stay white.
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
- Curved paths (`ScenePainterPathTests`): the cubic `M(5, 20) C(5, 5) (35, 5) (35, 20)` passes through its midpoint
  B(0.5) = 0.125 x P0 + 0.375 x P1 + 0.375 x P2 + 0.125 x P3 = (0.125 x 5 + 0.375 x 5 + 0.375 x 35 + 0.125 x 35,
  0.125 x 20 + 0.375 x 5 + 0.375 x 5 + 0.125 x 20) = (0.625 + 1.875 + 13.125 + 4.375, 2.5 + 1.875 + 1.875 + 2.5) =
  **(20, 8.75)**, where the tangent is horizontal, so a 2 pixel stroke covers y from 7.75 to 9.75: the pixels (19, 8)
  and (20, 8) are black, rows 5 and 12 at x = 20 and the chord (y = 20, and y = 15) are white, and the path has no
  fill. At the
  start the curve leaves upward: with t the parameter, x(t) - 5 = 90 t^2 and y(t) = 20 - 45 t, so within the pixel row
  19
  (t up to 0.022) the curve drifts right by at most 0.04, and the stroke from x = 4 to 6 covers pixel 4 by about 96
  percent (the red channel is below 40), pixel 5 fully, and pixel 2 not at all. A square from (2, 2) to (12, 12) with a
  close covers the pixels 2 to 11 and no more; the open triangle (20, 2), (30, 2), (20, 12) is filled by closing it, and
  its hypotenuse is x + y = 32, so the pixel (21, 4) (center sum 26) and the pixel (21, 9) (center sum 31) are inside
  and
  the pixel (28, 10) (center sum 39) is outside. Two moves make two subpaths: two filled squares at x = 2 to 8 and
  x = 20 to 28 leave the gap between them white, two stroked pieces (2, 10) to (8, 10) and (12, 14) to (18, 14) are not
  joined, so the pixel (10, 12) on the line between (8, 10) and (12, 14) that a join would draw (x - y = -2 at its
  center (10.5, 12.5)) is white, commands before the first move draw nothing (a 4 pixel red stroke along y = 20 would
  cover rows 18 to 21, and the pixels (20, 19) and (20, 20) stay white), and after a close the next command continues
  at the start of the closed subpath: from (2, 28) to (10, 28), close, line to (2, 34) gives the vertical piece x from
  1 to 3 (pixels 1
  and 2 at row 31, pixel 4 white) and the horizontal piece along y = 28 covers y from 27 to 29, so the pixel (6, 27) is
  also stroked.
- Arcs: the item at (50, 50) with the radii 20 and 40 from 0 to pi (12 o'clock to 6 o'clock) covers the right half of
  the
  ring: the pixel (80, 50) is at the distance 30.5 from the center (between 20 and 40), the pixels (51, 15) and (51, 85)
  at the distances 34.5 and 35.5; the pixel (20, 50) and the pixel (48, 15) lie left of the vertical line through the
  center, the pixels (60, 50) and (50, 50) lie in the hole (the distances 10.5 and 0.7 are below 20), and the pixel
  (95, 50) at 45.5 is outside. The pie quadrant of radius 40 at (150, 50) from pi / 2 to pi (3 o'clock to 6 o'clock) is
  the lower right quadrant: the pixels (170, 70) (distance 29) and (152, 52) are red, and (130, 70), (170, 30),
  (130, 30) and (185, 85) (the distance 50.2 is beyond 40) are white. A half-transparent fill gives (255, 127, 127), as
  in the first bullet, the stroke of 4 pixels is centered on the straight edges of the quadrant at (150, 50): the edge
  to the right along y = 50 covers the rows 48 to 51 (the pixels (170, 49) and (170, 50) are black, (170, 45) is white)
  and the edge down along x = 150 covers the columns 148 to 151 ((149, 70) is black) while the interior (165, 70)
  stays white. With the placement scale 2 and the offset (4, 6) the square from (5, 5) to (10, 10) covers x from
  4 + 10 = 14 to 4 + 20 = 24 and y from 6 + 10 = 16 to 26, so the pixels 14 to 23 and 16 to 25 are red, and a pie
  quadrant at (25, 25) of radius 20 is
  centered at (54, 56) with the radius 40: the pixel (81, 83), whose center is at the offset (27.5, 27.5), is at the
  distance 38.9 and red, while its mirror points (26, 84) and (82, 28) and the far point (84, 86) at the distance 43.1
  are white.
- Trails: the half width of a trail is half the size of its points and the capsule between two points joins the offset
  points of the two radii, so along y = 20 from (10, size 4) to (50, size 12) it grows linearly, `2 + (6 - 2) t` with
  t = (x - 10) / 40 (for the radii 2 and 6). At the pixel column 14 (center 14.5, t = 4.5 / 40 = 0.1125) the half width
  is 2 + 0.45 = 2.45, so rows 18 to 21 (within 2 of y = 20) are black and the rows 16 and 23 (3 away) are white; at
  column 30 (t = 20.5 / 40 = 0.5125) it is 2 + 2.05 = 4.05, so rows 17 to 22 (within 3) are black and rows 14 and 25
  (5 away) are white; at column 46 (t = 36.5 / 40 = 0.9125) it is 2 + 3.65 = 5.65, so rows 15 to 24 (within 5) are
  black and the rows 13 and 26 (6 away) are white. The ends are half circles of the radii 2 and 6: the pixels (9, 19)
  and (9, 20) are inside
  the circle of radius 2 at (10, 20), (9, 17) (nearest corner exactly 2 from the center) and (5, 20) are outside, the
  pixels (54, 17) and (54, 19) are inside the circle of radius 6 at (50, 20) (the farthest corner of (54, 17) is at
  sqrt(25 + 9) = 5.83), and (57, 20) (nearest corner at 7) and (55, 14) (nearest corner at 7.07) are outside. Runs are
  separate: the runs from x = 5 to 15 and from 25 to 35 at y = 10 with the size 6 (radius 3) cover x from 2 to 18 and
  from 22 to 38 with their caps, so the pixel (20, 10) between them is white while (16, 10) and (23, 10) are black; a
  run
  of one point draws nothing; two coincident points of size 8 at (60, 10) draw a disc of radius 4 (x from 56 to 64: the
  pixels (57, 10) and (62, 10) are black, (65, 10) and (54, 10) are white); and a point of size 0 ends its run, so the
  run
  at y = 25 with the points at x = 5, 15, 25 (size 0), 35, and 45 draws the two runs 5 to 15 and 35 to 45 with the caps
  2 to 18 and 32 to 48: the pixels (10, 25), (16, 25), (33, 25), and (40, 25) are black and (20, 25), (25, 25), and
  (30, 25) are white.
- Miter limit: the legs from (10, 30) up to the apex (20, 10) and down to (30, 30) with the stroke width 4 have the
  half angle atan(10 / 20) = 26.57 degrees between them and the axis, so the miter ratio is 1 / sin(26.57 degrees) =
  1 / 0.4472 = 2.236 and the tip of the miter is half of 2.236 x 4 = 4.47 above the apex, y = 10 - 4.47 = 5.53, while a
  bevel stops at the straight edge between the two outer offset points of the legs at the apex: the unit normal of a leg
  is (0.894, 0.447), so the offset points are 2 x 0.894 = 1.79 to each side of the apex and 2 x 0.447 = 0.89 above it,
  (18.21, 9.11) and (21.79, 9.11), and the bevel ends at y = 9.11. The pixel (19, 7) just left of the axis is
  therefore mostly covered (the red channel is below 100) when the limit is at least the ratio 2.236 (the default 4, the
  limit 3, and the huge limit 1e9 that is clamped to 1000) and white when the limit is below it (2) or is 1 after the
  clamp (the limit 0.5 and NaN, both unusable or below 1), and the pixel (20, 8) beside it (above y = 9.11) is white for
  the limit 2. The limit that the stroker receives is 4 for no value, the value itself for 2.5, 1 for 0.2, NaN, and both
  infinities (not a usable number or below 1), and 1000 for 5000 and for 1000.
- Dash, cap, join, and offset of a path: a 2 pixel line from x = 2 to 30 with the dashes 4 on and 4 off and butt caps is
  black on 2 to 6, white on 6 to 10, black on 10 to 14, and white on 14 to 18 (the pixels (3, 9) and (5, 9) are black,
  (7, 9) and (8, 9) white, (11, 9) black, (15, 9) and (1, 9) white); with square caps every dash extends by half the
  width, 1, on each side, so it is black on 1 to 7, white on 7 to 9, and black on 9 to 15 (pixels 1 and 6 black, 7 and 8
  white, 9 and 14 black); with the dash offset 2 the pattern starts 2 units in, so it is black on 2 to 4, white on 4 to
  8, black on 8 to 12, and white from 12 (pixels 2 and 3 black, 4 and 7 white, 8 and 11 black, 12 white). A right angle
  with the stroke width 4 at the corner (20, 38) has its outer corner at (22, 36) with a miter join (ratio 1.414 is
  below
  the default limit), so the pixel (21, 36) is black; the bevel joins the points (20, 36) and (22, 38), whose line
  keeps the pixel (61, 36) of the second corner just outside (its nearest edge touches the line), and the round join of
  radius 2 covers the part of the pixel (101, 36) that is inside the circle: the integral from u = 1 to u = sqrt(3) of
  (sqrt(4 - u^2) - 1) is 0.315 of the pixel, so the red channel is 255 x (1 - 0.315) = 175, above 100.

Unit tests reside in `ScenePainterTests.cs` and `ScenePainterPathTests.cs` (the paths with segments, the arcs, the
trails,
and the miter limit) within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Scenegraph` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts through a real `TextMeasurer`
- **Isolation**: Each test builds its own scene and surface and disposes the surface; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any pixel outside its analytic expectation (exact for integer-aligned geometry; within the stated
anti-aliasing
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

##### Gradient Fills

**Tests**: `ScenePainterTests.ScenePainter_Paint_GradientFillRunsAlongTheItemBox`,
`ScenePainterTests.ScenePainter_Paint_GradientFollowsTheScaleAndTheEnclosingGroup`,
`ScenePainterTests.ScenePainter_Paint_GradientOpacityReplacementAndFallback`,
`ScenePainterTests.ScenePainter_Paint_GradientOnGroupSymbolAndPath`,
`ScenePainterTests.ScenePainter_Paint_GradientIsDeterministic`

Asserts the derived gradient pixels (horizontal, legend-style vertical, padded ends, and a hard step); that the gradient
follows the scale of the placement and the translation of an enclosing group (a 50 by 10 rectangle in a group at (5, 5)
covers columns 5 to 54 at a scale of one and 10 to 109 at a scale of two, with the middle grey in the middle of the
box); that a half-transparent fill opacity gives (255, 127, 127) for a red gradient over white, that a fully transparent
gradient draws nothing and does not fall back to the solid green fill that it replaces, that a gradient with only
unusable stops (an unparseable color, a NaN offset), with no stops, or with a start that is not a number falls back to
the
solid fill, and that the stroke is drawn over a gradient fill; that a group background, a circle symbol, and a triangle
path each use the bounding box of their own geometry (a text item with a gradient but no solid fill draws nothing); and
that painting twice and from several threads gives identical pixels for a gradient with three stops, an alpha stop, a
fractional box, and rounded corners.

##### Symbols

**Tests**: `ScenePainterTests.ScenePainter_Paint_SymbolsCoverTheirDocumentedArea`,
`ScenePainterTests.ScenePainter_SymbolPath_ShapesHaveVegaBounds`,
`ScenePainterTests.ScenePainter_IsSymbolShape_KnowsTheDrawnShapes`

Asserts the derived square, rotated square, and circle coverage and area; the bounding boxes for size 64 (radius 4) of
`square`, `cross`, `diamond`, and `circle` (-4 to 4 in both directions), of `triangle-up` and `triangle-down` (-4 to 4
by minus and plus 3.4641), of `triangle-right` and `triangle-left` (the transpose), of `stroke` (-4 to 4 by 0 to 0), and
of an unknown shape (drawn as a circle); that `arrow`, `wedge`, and `triangle` have shapes; that a size that is not
positive has no shape; and, as a theory, that `IsSymbolShape` is true for each of the twelve names that have a shape and
false for another case of a name, the empty text, and an SVG path (which `IsDrawableShape` accepts when it parses; the
custom path is drawn and measured in `SymbolPathParserTests`, see the _SymbolPathParser Unit Verification Design_).

##### Paths, Rounded Rectangles, And Group Backgrounds

**Tests**: `ScenePainterTests.ScenePainter_Paint_PathsRoundedRectanglesAndGroupBackgroundAreCrisp`

Asserts that a rounded rectangle has its corner cut away while the middle of its edges is covered, that an open polyline
is stroked along both legs (a 2 pixel stroke covers 1 pixel on each side), that a closed triangle is filled inside and
not
outside its hypotenuse, and that the one pixel border of a group is offset by half a pixel so that it covers exactly one
pixel column and row.

##### Text

**Tests**: `ScenePainterTests.ScenePainter_Paint_TextUsesBaselineRotationAndScale`,
`ScenePainterTests.ScenePainter_Paint_TextLimitTruncatesTheDrawnLine`,
`ScenePainterTests.ScenePainter_Paint_TextBeyondTheMagnitudeBoundDrawsNothingAndNeverThrows`,
`ScenePainterTests.ScenePainter_IsDrawable_Text_FollowsTheSharedRule`,
`ScenePainterTests.ScenePainter_Paint_TextWithZeroFontSizeDrawsNothing`

Asserts the derived text placement, rotation, alignment, scaling, and determinism, and that a limit of 60 pixels
truncates the drawn line so that its ink ends between 40 and 60 pixels while the same line without a limit is wider than
100 pixels. Asserts that text with a font size of 1e300 (infinity as a single; with and without a limit of 50), 3e38,
2,000,000, or 1e-300 (zero as a single), a position of 1e300 or -2,000,000, an offset of 1e300 or -5,000,000, an
angle of 1e7, a line height of 1e300, or a limit that is not a number draws no ink and throws nothing, one at a
time and all together; that a limit of 1e300 is clamped to the bound and gives a surface identical to the same text
without a limit; and that a font size of 20,000 draws no ink at a scale of 2 (40,000 pixels, beyond the bound of
32,768).

Asserts that the internal rule `ScenePainter.IsDrawable(SceneText, float, out Rgba32)`, which the painter and the
missing-glyph report of `VegaLiteDocument` share, is true for an ordinary text (and resolves the fill color) and false
for a font size of 0, -1, NaN, 1e-300 (zero as a single), or 1e300 (beyond the bound), a position, limit, or line height
that is NaN, an angle of 1e7, a fill of `none`, or an opacity of 0; and that a text with a font size of 0 draws no ink.

##### Invalid Arguments And Cancellation

**Tests**: `ScenePainterTests.ScenePainter_Paint_InvalidArgumentsAndCancellation`

Asserts that a null measurer, scene, or surface throws `ArgumentNullException`, that a placement scale of zero or NaN
throws `ArgumentOutOfRangeException`, that a canceled token throws `OperationCanceledException`, and that fitting a
scene of zero width gives the identity placement (scale 1, offsets 0).

##### Pathological Dash Patterns Fall Back To Solid

**Tests**: `ScenePainterTests.ScenePainter_Paint_PathologicalDashPatternsFallBackToSolid`

Asserts that a 2-pixel black rule with a dash period of 0.002 (below the quarter-pixel minimum), a rule 10,000,000 long
with a `[2, 2]` pattern (2.5 million dashes, above the cap of 50,000), a `[1e-50, 1e-50]` pattern (both entries
underflow
to zero as floats), and a `[1e300, 1]` pattern (an entry overflows to infinity as a float) each cover every pixel of
their
two rows of a 20-pixel wide surface, that is, are drawn solid and none throws. No timing is asserted: the fallback is
decided from the estimated dash count before the stroker runs.

##### Values Outside The Float Range Are Safe

**Tests**: `ScenePainterTests.ScenePainter_Paint_ValuesOutsideTheFloatRangeAreSafe`

Asserts that stroke widths of 1e300 (overflows as a float) and 1e-50 (rounds to zero as a float) draw nothing, that a
dash
offset of 1e300 (overflows) counts as zero so the rule is pixel-identical to the unshifted `[4, 4]` rule drawn beside it
(black from x = 0, white at x = 5), and that no `ArgumentException` escapes. The existing dash assertions of the fill
and
stroke scenario (the 4-on, 4-off pixels of an ordinary dashed rule) remain the evidence that ordinary patterns are not
affected by the caps.

##### Segment Paths Follow Their Curves And Subpaths

**Tests**: `ScenePainterPathTests.ScenePainter_Paint_SegmentPathStrokesThroughItsCubicCurve`,
`ScenePainterPathTests.ScenePainter_Paint_ClosedSegmentPathFillsItsInterior`,
`ScenePainterPathTests.ScenePainter_Paint_MoveToSegmentsStartSeparateSubpaths`

Asserts that the cubic is stroked through its midpoint (20, 8.75) and not along its chord, with nothing filled, and
leaves its start going up; that a closed square covers exactly the pixels 2 to 11 and an open triangle is filled up to
its hypotenuse x + y = 32; and that each move starts a separate subpath (two squares, two unjoined pieces), the commands
before the first move draw nothing, and a command after a close continues at the start of the closed subpath.

##### Items That Are Not Finite Draw Nothing

**Tests**: `ScenePainterPathTests.ScenePainter_Paint_NonFiniteSegmentsArcsAndTrailsDrawNothing`

Asserts that every pixel of a 30 by 30 surface stays white when the scene holds a path with a NaN line coordinate, a
path
with an infinite control point, arcs with a NaN center coordinate, an infinite outer radius, and a NaN end angle, a
trail
whose first run has a NaN coordinate (so its one remaining point draws nothing) and whose second run has a NaN size, and
a trail with the sizes 0, -3, and infinity: each of them would cover a large part of the surface if it were drawn.

##### Items Beyond The Magnitude Bound Draw Nothing Or Become Lines

**Tests**: `ScenePainterPathTests.ScenePainter_Paint_SegmentsBeyondTheMagnitudeBoundDrawNothingOrBecomeLines`,
`ScenePainterPathTests.ScenePainter_Paint_ArcsBeyondTheMagnitudeBoundDrawNothing`,
`ScenePainterPathTests.ScenePainter_Paint_TrailPointsBeyondTheMagnitudeBoundEndTheRun`,
`ScenePainterPathTests.ScenePainter_Paint_GroupBeyondTheMagnitudeBoundDrawsNothing`,
`ScenePainterPathTests.ScenePainter_Paint_TheBoundAppliesToTheSizeInSurfacePixels`

The bound is `ScenePainter.MaxMagnitude` = 32,768 surface pixels (the value times the scale of the placement).
Asserts that a path whose last end point is 2,000,000 away, or 1e300 (finite as a double, infinity as a single), or
-1e7 on the y axis, or whose first move is at -2,000,000, leaves the 30 by 30 surface white; that a cubic from
(2, 10) to (28, 10) whose control points are 2,000,000 above and below it (finite) and one whose control points are
1e300 are each the straight line between their end points: the rows 9 and 10 (the line is 2 wide around y = 10) and
the rows 19 and 20 are black at the columns 3, 15, and 27 and the rows 6, 13, 16, and 23 and the column 1 are
white; that full circles with the center 1e7 away, the outer radius 5e8, the inner radius 2e6, the corner radius
1.5e6, and the outer radius 1e300 draw nothing while a circle of radius 10 at (15, 15) has a red center and a white
corner; that in a trail the point 2,000,000 away ends its run (the capsules from 2 to 15 and from 25 to 36 are
black at x = 8 and x = 30 and the gap at x = 20 is white) and a size of 2,000,000 ends its run in the same way;
that groups at x = 1e300 and at y = 2,000,000 hide their children; and that the bound is applied to the value times
the scale: a 2-wide line to (20000, 15) is drawn at a scale of 1 (rows 14 and 15 black at x = 10, row 12 white)
and not at a scale of 2 (40,000 pixels), a stroke width of 40,000 or 1e300 is not stroked, and a ring of radius
20,000 draws nothing at a scale of 2.

##### Trails Are Painted In Chunks With A Poll Between Them

**Tests**: `ScenePainterPathTests.ScenePainter_Paint_TrailIsPaintedInChunksWithACancellationPollBetweenThem`,
`ScenePainterPathTests.ScenePainter_Paint_TranslucentTrailInSeveralChunksHasNoSeam`,
`ScenePainterPathTests.ScenePainter_Paint_TranslucentTrailOutlineInSeveralChunksHasNoSeam`,
`ScenePainterPathTests.ScenePainter_Paint_TranslucentTrailBeyondTheSurfaceEdgeHasNoSeam`,
`ScenePainterPathTests.ScenePainter_Paint_OpaqueTrailInSeveralChunksIsBlackAlongTheWholeRow`,
`ScenePainterPathTests.ScenePainter_Paint_TrailLayerPixelsAreChargedAgainstTheBudgetAndFallBackBeyondIt`,
`ScenePainterPathTests.ScenePainter_Paint_TrailLayersIgnoreAStrokeWidthThatIsNotDrawn`,
`ScenePainterPathTests.ScenePainter_Paint_OpaqueFillWithTranslucentStrokeIsChargedOneLayer`,
`ScenePainterPathTests.ScenePainter_Paint_TranslucentFillWithOpaqueStrokeKeepsTheStrokeAboveTheFill`,
`ScenePainterPathTests.ScenePainter_Paint_FullyTransparentFillIsNotLayered`,
`ScenePainterPathTests.ScenePainter_Paint_TrailThatEndsAtTheFirstChunkBoundaryAllocatesNoLayers`,
`ScenePainterPathTests.ScenePainter_Paint_TranslucentTrailBeyondTheTargetAllocatesNoLayers`,
`ScenePainterPathTests.ScenePainter_Paint_PointsThatCannotBeDrawnDoNotGrowTheLayerBox`,
`ScenePainterPathTests.ScenePainter_TrailLayerPixelBudget_DefaultIsBoundedBelowTheLayersOfTheLargestTarget`

The estimated cost of a capsule is `1 + sqrt(radius x scale / 0.02)`: 1 for no radius, 11 for a radius of 2
(`sqrt(100)`), 21 for a radius of 2 at a scale of 4 (`sqrt(400)`), and about 906.1 for the largest radius that the
bound allows (16,384: `sqrt(819,200) = 905.1`). A chunk is complete when its weight reaches `TrailChunkWeight` =
100,000; for the largest capsules `110 x 906.1 = 99,671` is below it and `111 x 906.1 = 100,577` is not, so the
first chunk of a trail of 450 points of size 32,768 has 111 capsules (the 112th point). The test asserts the four
weights, that the observer `TrailChunkFlushed` is told `[111]` (a chunk is flushed just before the next capsule is
appended, so the
observer is told only of a chunk that is followed by another one) and that a token canceled by the observer stops the
painting with an `OperationCanceledException` before the first chunk is filled (so no second chunk is reached and
no capsule of that size is ever filled: the test also runs in milliseconds), and that an ordinary trail (100 points
of size 4, 99 capsules of weight 11, a total of 1,089) is one chunk that the observer never sees and is drawn whole
(black at its middle). The test counts capsules and does not measure time.

The seam tests use 9,400 points 0.8 apart with the size 4 (a weight of 11 for each of the 9,399 capsules, so the
observer is told `[9091]`: the first chunk is
complete after 9,091 capsules and the trail is two chunks) on a 7,600 by 20 white surface, and assert on the row 10 (the
center of the trail) from x = 20 to
x = 7,500: with a black fill of the opacity 0.5 every pixel equals the pixel at x = 100 (127 or 128, a single composite
of half black over white, where the
chunk-by-chunk painting gave 120 at x = 7,280, the joint); with a black stroke of the width 2 and the opacity 0.5 and no
fill, every pixel of the row 8 (the top edges of the capsules) is the
same one composite (it was 74 at the joint); with an opaque black fill every pixel is black and the chunks are the same;
and for a trail placed at the offset -7,000 on a 600 by 20 surface (the layers are cut to
the target, the joint is at x of about 282) the row is one composite from x = 20 to x = 500.

The budget tests use the same fill-only trail (the box of its points grown by the radius 2 and the 2 pixel margin is
7,528 by 8 pixels and it has one layer, so 60,224 pixels), and assert that the observer `TrailLayersCreated` is told
`[60224]` when the budget is exactly that and the row is one composite, and that with a budget of 60,223 it is told
nothing, the same two chunks are painted straight onto the target and the row is no longer uniform (the joint of the
chunks composites twice, 127 or 128 elsewhere). The stroke-reach theory (a width of infinity, NaN, and 1e9, with a half
transparent black fill and stroke) asserts the observer is told `[60224]` (one layer of the box of the fill, not two of
the whole 7,600 by 20 target, 304,000 pixels, as before, and not no layer at all for a NaN) and the row is one
composite, 127 or 128, which is the fill alone (the stroke is never drawn). The ceiling test asserts that the default
budget (33,554,432 pixels) is at most 128 MiB of 4-byte pixels and below one layer of the largest target (8,192 by
8,192 pixels), so that the 512 MiB of a fill and a stroke layer of that target can never be allocated. The first two
tests were checked by mutation: with the previous box rule and no budget they fail (304,000 pixels reported, and
layers created beyond the budget).

The layer-plan tests use the same trail, 7,600 by 20, with a 2 wide stroke, so the box of the points is grown by the
radius 2, the stroke width 2 and the margin 2: 7,532 by 12 pixels, 90,384 for one layer and 180,768 for two. An opaque
red fill with a half transparent black stroke is charged `[90384]` (the stroke layer only: an opaque fill has no visible
seam and is painted directly) with a budget of exactly that, and nothing with 90,383 (the former rule charged
180,768); the row 8 is half of black over red (127 or 128, 0, 0, 255) along the whole trail. A half transparent black
fill with an
opaque red stroke is charged `[180768]` (the stroke layer is kept for the order) and the row 8 is exactly red
(255, 0, 0, 255): a stroke painted directly would be covered by the later composite of the fill. A fill of the opacity
0 (and a fill of none) leaves the half transparent stroke the only layer, `[90384]`. A trail of 112 points of size
32,768 on a 40 by 40 surface is 111 capsules, exactly one chunk: the observer `TrailChunkFlushed` is told nothing, no
layers are created (it failed before the flush moved: the layers were created for a trail of one chunk) and the half
transparent fill is one composite (127 or 128) at its center. The seam trail on a 600 by 20 surface at the offset
-8,000 is two chunks that are both beyond the target: the observer is told `[9091]`, no layers are created (the box is
empty) and the pixel at (100, 10) is white. The seam trail with a second run of points that cannot be drawn (NaN, 1e9,
infinity, a size of 0 and of NaN) is charged `[60224]` as the trail alone, so those points do not grow the box (a
regression pin: the drawable test is shared by the painting and the box), and the row is one composite.

##### Arcs Fill Their Wedge, Stroke, And Scale

**Tests**: `ScenePainterPathTests.ScenePainter_Paint_ArcFillsOnlyItsWedgeAndLeavesTheHoleEmpty`,
`ScenePainterPathTests.ScenePainter_Paint_ArcStrokeAndOpacityFollowTheItem`,
`ScenePainterPathTests.ScenePainter_Paint_PathsAndArcsScaleWithThePlacement`

Asserts that the right half of a ring and the lower right quadrant of a pie are red at the pixels derived above while
the
left half, the hole, the outside, and the other quadrants are white; that a half-transparent fill gives (255, 127, 127)
only inside the quadrant, that the stroke of 4 pixels is centered on the straight edges and leaves the interior
unfilled,
and that an item with no opacity draws nothing; and that the placement scale 2 with the offset (4, 6) moves and scales a
segment path and an arc as derived.

##### Trails Follow The Size Of Each Point

**Tests**: `ScenePainterPathTests.ScenePainter_Paint_TrailWidthFollowsTheSizeOfEachPoint`,
`ScenePainterPathTests.ScenePainter_Paint_TrailBreaksAtRunBoundariesAndDrawsCapsForCoincidentPoints`

Asserts that the width at the columns 14, 30, and 46 is the derived 2.45, 4.05, and 5.65 half widths (rows 18 to 21,
17 to 22, and 15 to 24 black and the rows beyond white), that the ends are half circles of the radii 2 and 6, that the
runs are separate with their caps, that a run of one point draws nothing, that coincident points draw a disc of
radius 4, and that a point of size 0 ends its run.

##### Miter Limit, Dash, Cap, Join, And Offset Of A Path

**Tests**: `ScenePainterPathTests.ScenePainter_Paint_StrokeMiterLimitBevelsASharpJoin`,
`ScenePainterPathTests.ScenePainter_Paint_PathStrokeHonorsDashCapJoinAndOffset`

Asserts that the sharp join of ratio 2.236 is mitered for the limits none (4), 3, and 1e9 and beveled for 2, 0.5, and
NaN, that `ResolveMiterLimit` gives 4 for none, 2.5 for 2.5, 1 for 0.2, NaN, and both infinities, and 1000 for 5000
and 1000; and that a segment path of an item with the dash `[4, 4]` is stroked with the butt caps, the square caps,
and the dash offset of 2 at the derived pixels and with the miter, bevel, and round join of a right angle.

##### Pictures Are Drawn By Vega's Image Rules

**Tests**: `ScenePainterImageTests.ScenePainter_Paint_ImageWithoutSize_DrawsNaturalSizeAtLeftTop`,
`ScenePainterImageTests.ScenePainter_Paint_ImageAnchor_MovesTheNaturalBox`,
`ScenePainterImageTests.ScenePainter_Paint_ImageWithSize_AsksForTheBoxInDevicePixels`,
`ScenePainterImageTests.ScenePainter_Paint_RasterWithoutSize_ScalesWithThePlacement`,
`ScenePainterImageTests.ScenePainter_Paint_VectorWithoutSize_HasTheDefaultNaturalSize`,
`ScenePainterImageTests.ScenePainter_Paint_ImageWithOneDimension_FollowsTheAspectRule`,
`ScenePainterImageTests.ScenePainter_Paint_ImageInBoxOfOtherAspect_IsFittedAndCentered`,
`ScenePainterImageTests.ScenePainter_Paint_PictureOfTheRequestedSize_FillsTheBox`,
`ScenePainterImageTests.ScenePainter_Paint_ImageOpacity_BlendsOverTheBackground`,
`ScenePainterImageTests.ScenePainter_Paint_ImageInClippedGroup_IsCut`,
`ScenePainterImageTests.ScenePainter_Paint_ImageWithoutSmoothing_KeepsHardEdges`,
`ScenePainterImageTests.ScenePainter_Paint_EmptyImage_IsNotAskedFor`,
`ScenePainterImageTests.ScenePainter_Paint_WithoutLoader_SkipsPictures`,
`ScenePainterImageTests.ScenePainter_Paint_RasterThatEqualsTheRequestAtScaleTwo_IsDrawnAtItsNaturalSize`,
`ScenePainterImageTests.ScenePainter_Paint_VectorDrawnForTheRequestAtScaleTwo_CoversTheRequestedDevicePixels`,
`ScenePainterImageTests.ScenePainter_Paint_RasterThatEqualsTheRequestInOneDimension_KeepsItsOwnAspect`,
`ScenePainterImageTests.SceneImageSource_Get_ResolverThatDoesNotSay_GivesNoKindAndCachesTheKindOfOneThatDoes`

Asserts, with a resolver that reports its kind, that a 512 by 512 raster that is exactly the size asked for at a scale of
2 (no size on the mark) covers 1024 device pixels (512 units), not the 256 units of a picture drawn for the request;
that a picture drawn for the request covers the 512 device pixels it was drawn for; that a 40 by 20 raster asked for as
40 by 40 keeps its own aspect ratio (a 20 by 10 unit box for a width of 20); and that the resolver's answer about the
kind is cached with the picture while a resolver that does not say gives no kind.

Asserts, with a fake resolver that returns generated pictures (a red and blue halved raster, a solid color of the
requested
size), that a 10 by 6 raster is drawn pixel for pixel at its anchor (`x` 20 to 30, `y` 10 to 16: red left half, blue
right
half, white around) after one request of 256 by 256; that `left`/`top`, `center`/`middle`, `right`/`bottom` and an
unknown
name put the 10 by 6 box at the left edges 30, 25, 20, 30 and the top edges 20, 17, 14, 20; that a 20 by 10 box is asked
for as 20 by 10 at a scale of 1 and 40 by 20 at a scale of 2 and drawn at (10, 5) to (30, 15) scene units; that a raster
covers twice the pixels at a scale of 2 (asked for 512 by 512, 20 by 12 device pixels drawn) and a picture of the
requested
size is a 256 unit picture; that a width of 20 on a 10 by 5 raster gives a 20 by 10 box (and a height of 10 a 20 by 10
box,
and no aspect a 20 by 5 box); that a 10 by 10 picture in a 20 by 10 box is fitted 10 by 10 and centered from x 15 to 25
(and
centered vertically in a 10 by 20 box, and filling the box without the aspect); that a picture of the size asked for
(11 by 8 for 10.5 by 7.5) fills the box; that an opacity of 0.5 gives (255, 126 to 129, 126 to 129) over white; that a
20 by 20 picture in a group clipped to 8 by 8 shows only the clipped part; that a 2 by 1 raster enlarged to 20 by 10
without
smoothing has the exact colors on both sides of the seam; and that boxes without area, with a negative or non-finite
size,
without opacity, with an empty link or beyond the magnitude bound are never asked for and leave the surface white.

##### Pictures Are Loaded Once, Within A Limit, And Released

**Tests**: `ScenePainterImageTests.SceneImageSource_Get_CachesWithinOnePaintingAndDisposesEverySurface`,
`ScenePainterImageTests.SceneImageSource_Get_BeyondTheLimit_FailsAtTheMark`,
`ScenePainterImageTests.SceneImageSource_Get_ResolverGivesNothing_ReportsOnceAndDrawsNothing`,
`ScenePainterImageTests.ScenePainter_Paint_WithoutResolver_DrawsNothingAndReportsNothing`,
`ScenePainterImageTests.SceneImageSource_Get_ResolverThrows_PropagatesUnchanged`,
`ScenePainterImageTests.SceneImageSource_Get_KeyIncludesTheDeviceSize`

Asserts that four items with one link at two sizes make two resolver calls and that every surface throws
`ObjectDisposedException` after the painting; that with a limit of two the third distinct link is not asked for and the
`VegaLiteRenderException` carries the location of the mark and names `MaxImages` while the surfaces handed out are
released; that a resolver answering `null` is asked once for a link used twice and gives one warning that starts with
`vl-mark-image-unresolved:` and names the link; that no resolver means no call, no warning, and nothing drawn; that an
exception of the resolver propagates as the same object and releases the earlier surfaces; and that the cache key
includes the device size.

##### The Paint Budget Is Charged Before Each Item, At The Item Or Its Mark

**Tests**: `ScenePainterBudgetTests.ScenePainter_Paint_FullSurfaceRects_AreChargedTheirBoxBeforeThePaint`,
`ScenePainterBudgetTests.ScenePainter_Paint_ItemThatDoesNotFit_IsNotPainted`,
`ScenePainterBudgetTests.ScenePainter_Paint_FailureIsLocatedAtTheItemOrTheNearestLocatedGroup`,
`ScenePainterBudgetTests.ScenePainter_Paint_BoxIsCutToTheSurface_AndAnItemOutsideCostsNothing`,
`ScenePainterBudgetTests.ScenePainter_Paint_StrokeIsChargedItsBoxGrownByHalfTheWidth`,
`ScenePainterBudgetTests.ScenePainter_Paint_FillAndStrokeAreTwoCharges`,
`ScenePainterBudgetTests.ScenePainter_Paint_ClipLayerAndItsContentAreCharged`,
`ScenePainterBudgetTests.ScenePainter_Paint_TextIsChargedAnEstimatedBox`,
`ScenePainterBudgetTests.ScenePainter_Paint_TextWithANonPositiveLineHeight_IsChargedEveryLine`,
`ScenePainterBudgetTests.ScenePainter_Paint_ArcsAtTheEdge_AreChargedAtLeastTheirVisibleArea`,
`ScenePainterBudgetTests.ScenePainter_Paint_WithoutALimit_CountsNothing`,
`ScenePainterBudgetTests.ScenePainter_Paint_LimitIsPerPainting`,
`ScenePainterBudgetTests.ScenePainter_Paint_PictureBox_IsChargedBeforeItIsDrawnAndLocatedAtTheMark`,
`ScenePainterBudgetTests.ScenePainter_Paint_NonSmoothPictureCopy_IsMadeOnceForTheItemsThatShareIt`,
`ScenePainterBudgetTests.SceneImageSource_GetTile_SharesCopiesAndBoundsWhatItKeeps`

Asserts, on a 100 by 100 surface (10,000 pixels) painted at a scale of one, that two rectangles that cover it fit a
limit of exactly 20,000 pixels and fail, naming `VegaLiteLimits.MaxPaintedPixels`, the item and the pixel count, with
19,999; that the item that fails leaves the surface as the earlier item painted it (the charge is made before the fill);
that the failure is located at the `Source` of the item that has one and, for an item that has none, at the nearest
enclosing group that has one; that a rectangle beyond the surface costs nothing (a limit of 1), that one that covers the
surface from far outside is charged exactly the surface, and that nothing is charged beyond it; that a horizontal rule 10
pixels wide across the surface costs exactly 1,000 pixels (the box grown by half the width) and fails with 999; that a
50 by 50 rectangle with a fill and a 2 pixel stroke costs 2,500 plus 2,704 (the stroke box is 52 by 52); that a group
clipped to 50 by 50 pixels costs its 2,500 pixel layer plus the 2,500 pixels of the rectangle in it that is cut to the
layer; that text of eleven characters at a font size of 20 costs the estimated 100 by 22 pixels, and three lines of it with
a zero or negative line height (which Vega steps upward or not at all) cost 3 lines of at least a font size each (6,000
pixels, or 10,000 when the step is taller than the surface), never a negative or smaller charge; that arcs (pies,
donuts, rounded corners, a full circle, a sliver) centered on and beyond the edges and corners of the surface are each
charged at least the pixels that they paint (a limit of one pixel less refuses them), which the box of the end point of
an arc grown by its radius did not guarantee, and that the box is widened to whole pixels; that a painter without a
limit counts nothing (a rectangle of two billion pixels is painted) and rejects a limit below one; and that the limit is
per painting. Asserts that a 60 by 60 picture costs exactly 3,600 pixels, is located at the mark of the picture and is
not painted when it fails; that three items that draw one 2 by 1 picture without smoothing in 100 by 50 boxes cost 5,000
pixels each and 5,000 once for the resampled copy (a limit of 20,000 passes and 19,999 fails, where a copy for every item
would need 30,000); and that `SceneImageSource.GetTile` gives the same copy for the same picture, size and opacity
without charging again, another copy for another size or opacity (the opacity 0.5 gives an alpha of 128), the picture
itself for a smoothed opaque picture, and, for a copy of 9,000,000 pixels that does not fit the 8,388,608 that are kept,
a copy that the caller owns and that is made (and charged) for every request.
