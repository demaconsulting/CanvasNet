### CurveBuilder Unit Verification Design

This document describes the unit-level verification strategy for the `CurveBuilder` unit in the
`DemaConsulting.CanvasNet.VegaLite.Marks` namespace: the internal static partial `CurveBuilder` class (`TryParse`,
`SupportsArea`, `MaxSegments`, `Line`, and `Area`, in `CurveBuilder.cs`, `CurveBuilder.Splines.cs`, and
`CurveBuilder.Generators.cs`) with the `CurveKind` enumeration and the `CurvePoint` and `AreaPoint` records. The unit
turns the points of a line or an area into the absolute move, line, cubic, and close segments of a scene path with the
sixteen curves of d3-shape (the fifteen interpolations of Vega-Lite, with `monotone` resolving to the monotone-x or the
monotone-y curve), with the line and area generators that break a path at an undefined point, and it gives the upper
bound of the segments that it writes so that the caller can charge the scene items before the path exists. It reads no
data and calls no other unit of the library apart from the scene path segment type; the placement of the points (the
scales, the grouping, the sort, the invalid data modes) belongs to `MarkBuilder` and is verified there (see the
_MarkBuilder Unit Verification Design_).

#### Verification Approach

The unit is verified by calling its static methods directly with hand-built points and comparing the segments with the
path that the d3-shape definition of the curve gives. The helpers of `CurveBuilderTests.cs` spell the segments as the
text of an SVG path (`M x,y`, `L x,y`, `C x1,y1 x2,y2 x,y`, and `Z`, the numbers rounded to four decimals) so that a
whole outline is one string. No mocking or stubbing is used: the unit has no collaborators, and the internal types are
reached through the test assembly's `InternalsVisibleTo` access. The expected strings are derived by hand from the
formulas of the curves, with the derivation in the test comments and repeated here; nothing is taken from the output of
another implementation. The points are `P0`, `P1`, and so on, and a vector expression such as `(P0 + 4 P1 + P2) / 6` is
evaluated for the x and the y coordinate separately:

- **Linear.** For the points (0,0), (10,10), and (20,0) the line start moves to the first point and every other point is
  a line, `M0,0 L10,10 L20,0`; the closed curve adds the closing command, `M0,0 L10,10 L20,0 Z`; the tension is
  ignored (a tension of 0.3 gives the same path).
- **Step.** For (0,0), (10,10), and (20,5) the change of a step is at `x1 = x0 (1 - t) + x t` between an old x0 and a
  new x. With `t = 0.5` the changes are at 5 and 15: a line at the old y to x1 and a line to the new y, and because
  `0 < t < 1` and two points were seen, the line end adds the last line to the final point,
  `M0,0 L5,0 L5,10 L15,10 L15,5 L20,5`. With `t = 1` (step-after) the change is at the new x and with `t = 0`
  (step-before) at the old x, and neither needs the final line: `M0,0 L10,0 L10,10 L20,10 L20,5` and
  `M0,0 L0,10 L10,10 L10,5 L20,5`.
- **Basis.** For (0,0), (30,30), and (60,0) the path starts at the first point and goes by a line to a sixth of the way,
  `(5 P0 + P1) / 6 = (5,5)`; the first segment has the controls `(2 P0 + P1) / 3 = (10,10)` and
  `(P0 + 2 P1) / 3 = (20,20)` and ends at the knot `(P0 + 4 P1 + P2) / 6 = (30,20)`; the last segment is the same formula
  with the last point repeated, the controls (40,20) and (50,10) and the knot `(P1 + 4 P2 + P2) / 6 = (55,5)`, and a
  line to the last point ends the path: `M0,0 L5,5 C10,10 20,20 30,20 C40,20 50,10 55,5 L60,0`. For the open curve of
  (0,0), (30,30), (60,0), and (90,30) the path starts at the first knot `(P0 + 4 P1 + P2) / 6 = (30,20)` and has one
  segment with the controls (40,20) and (50,10) and the knot `(P1 + 4 P2 + P3) / 6 = (60,10)`,
  `M30,20 C40,20 50,10 60,10`. For the closed curve of (0,0), (30,30), and (60,0) the knots are (30,20), (45,5) =
  `(P1 + 4 P2 + P0) / 6`, and (15,5) = `(P2 + 4 P0 + P1) / 6`, each reached with the controls `(2 A + B) / 3` and
  `(A + 2 B) / 3` of the two surrounding points, and the loop returns to (30,20) without a closing command:
  `M30,20 C40,20 50,10 45,5 C40,0 20,0 15,5 C10,10 20,20 30,20`.
- **Bundle.** With a beta of 1 every point stays where it is, so the bundle curve of any points is the basis curve of
  them (the five bumpy points of the test give equal paths). With a beta of 0 every point moves onto the chord from the
  first to the last point; for (0,0), (30,30), and (60,0) the chord is y = 0, the points become (0,0), (30,0), and
  (60,0), and the basis curve of collinear points is `M0,0 L5,0 C10,0 20,0 30,0 C40,0 50,0 55,0 L60,0`. The default beta
  is 0.85: the middle point moves to `0.85 x 30 + 0.15 x 0 = 25.5`, so the basis curve of (0,0), (30,25.5), and (60,0)
  is drawn, `M0,0 L5,4.25 C10,8.5 20,17 30,17 C40,17 50,8.5 55,4.25 L60,0` (25.5 / 6 = 4.25, 25.5 / 3 = 8.5, and
  4 x 25.5 / 6 = 17). A tension that is not a number counts as absent and gives the default path.
- **Cardinal.** For P0 = (0,0), P1 = (10,10), P2 = (20,0), and P3 = (30,10) the controls of the segment from P1 to P2
  are `P1 + k (P2 - P0)` and `P2 + k (P1 - P3)` with `k = (1 - tension) / 6`. With a tension of 0, `k = 1/6`: the ends
  are duplicated, so the first controls are P0 and `P1 + (P0 - P2) / 6 = (6.6667,10)`; the middle controls are
  `P1 + (P2 - P0) / 6 = (13.3333,10)` and `P2 + (P1 - P3) / 6 = (16.6667,0)`; the last are
  `P2 + (P3 - P1) / 6 = (23.3333,0)` and P3. The path is
  `M0,0 C0,0 6.6667,10 10,10 C13.3333,10 16.6667,0 20,0 C23.3333,0 30,10 30,10`. With a tension of 0.5, `k = 1/12`, and
  the offsets halve (8.3333, 11.6667, 18.3333, and 21.6667 for the x of the four controls that move); with a tension
  of 1, `k = 0`, and every control is an end point (`M0,0 C0,0 10,10 10,10 C10,10 20,0 20,0 C20,0 30,10 30,10`). The
  open curve starts at P1 and ends at P2, `M10,10 C13.3333,10 16.6667,0 20,0`. The closed curve of (0,0), (10,10), and
  (20,0) starts at P1, and the points wrap around: the segment to P2 has the controls `P1 + (P2 - P0) / 6 = (13.3333,10)`
  and `P2 + (P1 - P0) / 6 = (21.6667,1.6667)`, the segment to P0 has `P2 + (P0 - P1) / 6 = (18.3333,-1.6667)` and
  `P0 + (P2 - P1) / 6 = (1.6667,-1.6667)`, and the last to P1 has `P0 + (P1 - P2) / 6 = (-1.6667,1.6667)` and
  `P1 + (P0 - P2) / 6 = (6.6667,10)`.
- **Catmull-Rom.** An alpha of 0 makes all the chord lengths 1 and gives the cardinal curve of tension 0, also for the
  coincident points of (0,0), (0,0), (10,10), (10,10), (20,0). For the chordal curve (alpha 1) of A = (0,0), B = (10,0),
  and C = (10,30) the chords are |AB| = 10 and |BC| = 30, so the lengths to the power alpha are 10 and 30 and to the
  power 2 alpha are 100 and 900. The first segment has no chord before it, so its first control stays A; its second
  control uses `b = 2 x 900 + 3 x 30 x 10 + 100 = 2800` and `m = 3 x 30 x (30 + 10) = 3600` and is
  `(B b + A 900 - C 100) / m = (7.5,-0.8333)` (the x is (10 x 2800 - 10 x 100) / 3600 = 7.5 and the y is
  -30 x 100 / 3600 = -0.8333). The last segment has no chord after it, so its second control stays C; its first control
  uses `a = 2 x 100 + 3 x 10 x 30 + 900 = 2000` and `n = 3 x 10 x (10 + 30) = 1200` and is
  `(B a - A 900 + C 100) / n = (17.5,2.5)`, so the path is `M0,0 C0,0 7.5,-0.8333 10,0 C17.5,2.5 10,30 10,30`. The
  default alpha is 0.5 (centripetal): for the evenly spaced collinear points 0, 10, 20, and 30 on y = 0 the chords are
  all 10, so the lengths are sqrt(10) and 10, `b = 2 x 10 + 3 x 10 + 10 = 60`, and
  `m = 3 sqrt(10) x 2 sqrt(10) = 60`, and the corrections reproduce the cardinal controls, for example the second
  control of the first segment is `(10 x 60 + 0 x 10 - 20 x 10) / 60 = 6.6667 = P1 + (P0 - P2) / 6`.
- **Monotone.** For the symmetric peak (0,0), (10,10), and (20,0) the slopes of the two intervals are 1 and -1, so the
  tangent at the peak is 0, and the end tangents are the one-sided slopes `(3 x 10 / 10 - 0) / 2 = 1.5` and -1.5. With
  `dx = 10 / 3` the first controls are `P0 + (dx, dx t0) = (3.3333,5)` and `P1 - (dx, dx t1) = (6.6667,10)` and the
  second are `P1 + (dx, 0) = (13.3333,10)` and `P2 - (dx, dx x -1.5) = (16.6667,5)`, so the path is
  `M0,0 C3.3333,5 6.6667,10 10,10 C13.3333,10 16.6667,5 20,0`. For the monotone data (0,0), (1,1), (2,4), (3,9), (4,10),
  (5,10), (6,12), and (8,20) every cubic has its controls within the end points and in order in both coordinates, and the
  plateau from (4,10) to (5,10) has zero tangents at both ends (the neighbor slope of 0 limits the slope), so its controls
  have y = 10 exactly. Coincident points are ignored: (0,0), (0,0), (10,10) is the line `M0,0 L10,10` and (4,4), (4,4)
  alone is the point `M4,4 Z`. The monotone-y curve of (0,0), (10,10), and (0,20) is the monotone-x curve of the swapped
  points (0,0), (10,10), and (20,0) with every pair of the output swapped back: `M0,0 C5,3.3333 10,6.6667 10,10
  C10,13.3333 5,16.6667 0,20`; the test also proves the equality on eight bumpy points with coincident ones.
- **Natural.** For the x values 0, 10, and 20 (two intervals) the control points solve a tri-diagonal system whose
  eliminated form is `a1 = 80 / 6 = 13.3333` and `a0 = (20 - a1) / 2 = 3.3333`; the first interval has the controls
  3.3333 and `2 x 10 - a1 = 6.6667` and the second has 13.3333 and `(20 + a1) / 2 = 16.6667`. For the y values 0, 10,
  and 0 the same solution is `a1 = 10` and `a0 = 5` with the controls 5 and 10, and 10 and 5, so the path is
  `M0,0 C3.3333,5 6.6667,10 10,10 C13.3333,10 16.6667,5 20,0` (the same path as the monotone peak, by the symmetry of
  the data). Four
  collinear evenly spaced points give the straight line with the controls at the thirds (3.3333, 6.6667, 13.3333,
  16.6667, 23.3333, and 26.6667 for the x); two points are a line `M1,2 L3,4` and one point is `M1,2 Z`.
- **Gaps and degenerate counts.** An undefined point (or a point with a coordinate that is not finite) ends the
  subpath and the next defined point starts a new one: (0,0), (10,10), a gap, (30,0), (40,10) is
  `M0,0 L10,10 M30,0 L40,10`; leading and trailing gaps do nothing and a run of one point is a move and a close,
  `M0,0 Z M5,5 L6,6`; the step curve flushes each run on its own,
  `M0,0 L5,0 L5,10 L10,10 M20,0 L25,0 L25,5 L30,5`. With no point every curve gives the empty path. With one point at
  (3,4) every curve that draws its first point at once gives `M3,4 Z` and the open curves (basis-open, cardinal-open) and
  the bundle give nothing, because they need three or four points (or two, for the bundle) before they draw. With the
  two points (0,0) and (10,10) most curves give the line `M0,0 L10,10`; the closed linear curve adds `Z`; the step curves
  give `M0,0 L5,0 L5,10 L10,10` (step), `M0,0 L10,0 L10,10` (step-after), and `M0,0 L0,10 L10,10` (step-before); the open
  curves give nothing; basis-closed has the points a third of the way, `M6.6667,6.6667 L3.3333,3.3333 Z`
  (`(P0 + 2 P1) / 3` and `(P1 + 2 P0) / 3`); and cardinal-closed is the loop `M10,10 L0,0 Z`. Three points of the open
  curves draw only the move to the first knot and close it: `M30,20 Z` for basis-open of (0,0), (30,30), (60,0) and
  `M10,10 Z` for cardinal-open of (0,0), (10,10), (20,0).
- **Area.** The top line is drawn forward and the baseline backward as a continuation of the same path (a line, not a
  move, because the second pass has already started), and the polygon is closed. The line chart of 300 by 200 pixels
  (x 0, 150, 300; y 200, 0, 100 with the baseline y = 200) gives
  `M0,200 L150,0 L300,100 L300,200 L150,200 L0,200 Z`. The horizontal area has the axis along y: the top runs along
  x1 = 5, 15, 10 at y = 0, 10, 20 and the baseline along x0 = 0, `M5,0 L15,10 L10,20 L0,20 L0,10 L0,0 Z`. Each run of
  defined points is its own polygon and a run of one point is a line to the baseline and back,
  `M0,5 L10,15 L10,0 L0,0 Z M30,7 L30,0 Z`; the closed linear curve has no area passes, so the top and the baseline are
  two closed polygons, `M0,200 L150,0 L300,100 Z M300,200 L150,200 L0,200 Z`; a vertex with a coordinate that is not
  finite ends the run, `M0,5 L0,0 Z M20,5 L20,0 Z`. The step area over the baseline 0 and the top (0,10), (10,20),
  (20,15) is the centered step of the line ending with its final line to (20,15); the line end turns `t` into
  `1 - t = 0.5`, the baseline pass continues from (20,15) with a line to its first point (20,0), then repeats the step
  backward through (10,0) and (0,0) with its own final line, and closes:
  `M0,10 L5,10 L5,20 L15,20 L15,15 L20,15 L20,0 L15,0 L15,0 L5,0 L5,0 L0,0 Z`. The step-after area with the baselines
  0, 2, and 4 flips `t` to 0 so that the baseline, read from right to left, changes at the old x: 4 at x = 20, 2 on
  [10, 20), and 0 on [0, 10), `M0,10 L10,10 L10,20 L20,20 L20,15 L20,4 L20,2 L10,2 L10,0 L0,0 Z`. The monotone area of the
  symmetric peak over the baseline y = -10 has the controls of the peak above and, because the flat baseline has zero
  tangents, the thirds of the interval below (`C16.6667,-10 13.3333,-10 10,-10 C6.6667,-10 3.3333,-10 0,-10`), then
  closes; a second run of two points draws lines, `M40,5 L50,15 L50,-10 L40,-10 Z`. The horizontal monotone area is the
  same data with x and y exchanged (the monotone-y curve), so every pair of the vertical result is swapped. The bundle
  curve has no area in d3-shape: `SupportsArea` is false for it and true for the other fifteen curves, and `Area`
  throws `NotSupportedException` and writes nothing.
- **Names.** The fifteen names of Vega-Lite (`linear`, `linear-closed`, `step`, `step-before`, `step-after`, `basis`,
  `basis-open`, `basis-closed`, `bundle`, `cardinal`, `cardinal-open`, `cardinal-closed`, `catmull-rom`, `monotone`, and
  `natural`) map to their curves, `monotone` to `MonotoneX`; the empty text, other cases (`Linear`, `LINEAR`), trailing
  blanks, the Vega-only `catmull-rom-open` and `catmull-rom-closed`, `monotone-x`, `cubic`, `none`, and null are
  rejected. The `monotone` name is `MonotoneY` only when the area is horizontal.
- **Segment bound.** `MaxSegments` is `factor x points x (area ? 2 : 1) + 6 x max(runs, 1)` with a factor of 2 for the
  three step curves (two lines per interval) and 1 otherwise, 0 for no defined points, and saturating at
  `long.MaxValue` instead of overflowing. The additive part (6 per run) covers the move, the close, and the end pieces
  of the curves; its worst case is the baseline pass of an area of basis curves, which needs `2 x points + 5`
  segments in a run (a constant of 4 per run would be one segment short). The test sweeps the sixteen curves, every
  point count from 0 to 12, every
  defined or undefined pattern for up to 8 points (2^n patterns) and four patterns for 9 to 12 points (8,432 cases),
  with a line and a vertical and a horizontal area of every curve that supports one, and requires the generated
  count never to exceed the bound; it also asserts the edges: 0 for no points, 0 for a negative count, `long.MaxValue`
  for `long.MaxValue` points, `2 x 5 x 2 + 6 = 26` for five points of a step area in one run, and `5 + 6 = 11` for five
  points of a natural line in one run.
- **Cancellation and finite coordinates.** The generators poll the token at the first point and then at every 1,024
  points, so a canceled token stops even a line of one point (and the area generator the same), and a line of 3,000
  points throws before it has written the 3,000 segments, while the same line with a live token writes exactly 3,000
  segments. No curve ever writes a coordinate that is not finite: the test feeds five inputs (gaps with not-a-number and
  infinite coordinates, coincident points, points on one vertical line with repeated y values, and huge values of 1e308
  as x and y and as y alone, whose control points overflow) to all sixteen curves with the tensions absent, 0, 1, -3,
  7, and not-a-number,
  and checks the six coordinates of every segment.

Unit tests reside in `CurveBuilderTests.cs`, `CurveBuilderSplineTests.cs`, and `CurveBuilderAreaTests.cs` within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Marks` folder; the spline and area tests share the
`P`, `Gap`, `Run`, `Segments`, and `Text` helpers of `CurveBuilderTests.cs`. The curves in the whole drawing path (the
placement of the points by `MarkBuilder` and the pixels that the painter draws) are verified in `MarkBuilderPathTests.cs`
and the system tests that the _Marks Subsystem Verification Design_ lists.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None; every input is an array of points built in the test
- **Isolation**: Each test builds its own points and its own output list; no state is shared between tests, and the
  numbers are formatted with the invariant culture, so no result depends on the host's culture, clock, or zone. The
  cancellation scenario passes a token that is canceled before the call

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any segment that differs from the hand-derived path (to four decimals), a point that is written for a run that
d3-shape does not draw or left out of one that it does, a subpath that is not broken at an undefined point, a coordinate
that is not finite, a count of segments above the bound, a name that is accepted or rejected differently from the
expectation, a bundle area that is not refused, or a canceled token that does not stop the generators constitutes a
failure.

#### Unit-Level Test Scenarios

##### Linear And Step Curves Match The Anchors

**Tests**: `CurveBuilderTests.CurveBuilder_Line_LinearAndLinearClosedMatchTheAnchors`,
`CurveBuilderTests.CurveBuilder_Line_StepVariantsMatchTheHandDerivedAnchors`

Asserts the polyline `M0,0 L10,10 L20,0`, its closed form with `Z`, the unchanged path for a tension of 0.3, and the
three staircases of the points (0,0), (10,10), (20,5): the centered step with the final line to (20,5), and the
step-after and step-before paths without it.

##### Basis And Bundle Curves Match The Hand-Derived Beziers

**Tests**: `CurveBuilderTests.CurveBuilder_Line_BasisFamilyMatchesTheHandDerivedBeziers`,
`CurveBuilderTests.CurveBuilder_Line_BundleWithBetaOneEqualsBasis`

Asserts the three basis paths (basis, basis-open, and basis-closed, the last ending without a closing command) with the
knots and controls derived above; that the bundle curve with a beta of 1 equals the basis curve of five bumpy points;
and that a beta of 0 (the collinear path), the default beta of 0.85 (the middle point at 25.5), and a beta that is not a
number (the default) give the derived paths.

##### Cardinal And Catmull-Rom Curves Use The Tension Formula

**Tests**: `CurveBuilderSplineTests.CurveBuilder_Line_CardinalFamilyUsesTheTensionFormula`,
`CurveBuilderSplineTests.CurveBuilder_Line_CatmullRomWithAlphaZeroEqualsCardinal`

Asserts the cardinal paths for the tensions 0, 0.5, and 1 and the open and closed forms derived above; that the
catmull-rom curve with an alpha of 0 equals the cardinal curve (also for the repeated points); and the chordal path of A,
B, C with its controls (7.5,-0.8333) and (17.5,2.5) and the centripetal path of four collinear points, whose controls are
the cardinal ones (6.6667 and 16.6667).

##### Monotone And Natural Splines Match The Derivations

**Tests**: `CurveBuilderSplineTests.CurveBuilder_Line_MonotoneXKeepsMonotoneDataMonotone`,
`CurveBuilderSplineTests.CurveBuilder_Line_MonotoneYIsMonotoneXOfTheSwappedPoints`,
`CurveBuilderSplineTests.CurveBuilder_Line_NaturalMatchesTheHandDerivedControlPoints`

Asserts the monotone-x path of the symmetric peak; the ordered controls of the eight monotone points (eight segments,
each cubic ending at its data point), the flat plateau (y of both controls exactly 10), and the two coincident-point
cases; the monotone-y path of (0,0), (10,10), (0,20) and its equality with the swapped monotone-x path of eight points;
and the natural paths of three points, four collinear points, two points (a line), and one point.

##### Gaps And Degenerate Counts Follow The Curve Definitions

**Tests**: `CurveBuilderTests.CurveBuilder_Line_UndefinedPointsStartNewSubpaths`,
`CurveBuilderTests.CurveBuilder_Line_DegenerateCountsFollowD3`

Asserts the two runs separated by a gap, the leading and trailing gaps, the lone point `M0,0 Z`, a not-a-number or
infinite coordinate acting as a gap (for the linear, natural, and step curves), and the per-run flush of the step curve;
and, for all sixteen curves, the empty path for no point, the table of the one-point paths (`M3,4 Z` for thirteen curves,
nothing for basis-open, cardinal-open, and bundle), the table of the two-point paths, and the three-point paths of the
two open curves.

##### Names Resolve Exactly

**Tests**: `CurveBuilderTests.CurveBuilder_TryParse_AcceptsTheFifteenVegaLiteNamesAndRejectsOthers`,
`CurveBuilderTests.CurveBuilder_TryParse_MonotoneFollowsTheAreaOrientation`

Asserts that the fifteen names map to their curves, that the nine other texts and null are rejected, that `monotone`
is `MonotoneX` for a vertical mark and `MonotoneY` for a horizontal area, and that `natural` does not depend on the
orientation.

##### Areas Close Around Their Runs

**Tests**: `CurveBuilderAreaTests.CurveBuilder_Area_VerticalAndHorizontalPolygonsMatchTheAnchors`,
`CurveBuilderAreaTests.CurveBuilder_Area_CurvedTopAndBaselineClosePerDefinedRun`,
`CurveBuilderAreaTests.CurveBuilder_Area_BundleIsReportedAsUnsupported`

Asserts the vertical and horizontal polygons, the polygons of two runs and of a run of one point, the two polygons of the
closed linear curve, the run that a non-finite vertex ends, and the empty area of no points; the step, step-after,
monotone, and horizontal monotone polygons derived above, including the second run of the monotone area; and that
`SupportsArea` is false only for the bundle, that `Area` of the bundle throws `NotSupportedException` and leaves the
output empty, and that the line generator draws the bundle.

##### The Segment Bound, Cancellation, And Finite Output Hold

**Tests**: `CurveBuilderTests.CurveBuilder_MaxSegments_IsAnUpperBoundForEveryKind`,
`CurveBuilderTests.CurveBuilder_Line_PollsCancellationEvery1024Points`,
`CurveBuilderTests.CurveBuilder_Line_NeverEmitsNonFiniteCoordinates`

Asserts that the bound is never below the count that the generators write for the 8,432 cases above and the five edge
values (0, 0 for a negative count, `long.MaxValue`, 26, and 11); that a canceled token throws
`OperationCanceledException` from the line generator for 3,000 points (writing fewer than 3,000 segments), for the
natural curve of one point, and from the area generator, while a live token writes 3,000 segments; and that every
coordinate of every segment of the five-input sweep over the sixteen curves and the six tensions is finite.
