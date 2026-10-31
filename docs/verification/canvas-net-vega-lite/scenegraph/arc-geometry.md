### ArcGeometry Unit Verification Design

This document describes the unit-level verification strategy for the `ArcGeometry` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace: the internal static `ArcGeometry` class (`Build`), which
turns an inner and an outer radius, a start and an end angle, a pad angle, and a corner radius into the outline of a
circular or annular sector as a core `Path` centered on the origin (the geometry of the `arc` mark, a port of the
d3-shape arc generator), and the internal `ArcPathWriter` class in the same file, which records the canvas calls of
d3-path (move, line, arc, close) on a core `PathBuilder` by emulating the canvas arc with an SVG elliptical arc command
and two half circles for a full circle. The painter translates the path to the center of the item and fills it (see
the _ScenePainter Unit Verification Design_); the trail painter uses the same writer for its capsules, which that unit
verifies.

#### Verification Approach

The unit is verified by calling `ArcGeometry.Build` with hand-chosen numbers and asserting on the commands of the
returned path (the start of each subpath and the kind, end point, radius, and arc flags of each command, which the
test class lists as readable tuples) and, where the area is the contract, on the coverage that the core `PathFiller`
gives the outline on a real `Surface`. The collaborators (`Path`, `PathBuilder`, `PathFiller`, and `Surface`) are real
and no mocking or stubbing is used; the internal types are reached through the test assembly's `InternalsVisibleTo`
access. Coordinates are compared within 1e-4 because the core path stores single precision numbers. The expected
values are derived by hand from the definition of the generator, with the derivation in the test comments and
repeated here. The angle of the generator is the angle of the specification minus a quarter turn, `a = angle - pi / 2`,
so that an angle of zero points to 12 o'clock and angles grow clockwise, and the end points are `(r cos a, r sin a)`
(in screen coordinates, where y grows downward); an arc command is clockwise when its sweep flag is set and a large
arc when its sweep is at least half a turn.

- **Quarter sector.** With the inner radius 0, the outer radius 10, and the angles 0 to pi / 2, `a0 = -pi / 2` and
  `a1 = 0`, so the outline starts at (10 cos a0, 10 sin a0) = **(0, -10)**, runs a clockwise arc of radius 10 (small,
  sweep set) to (10 cos 0, 10 sin 0) = **(10, 0)**, goes to the center, **(0, 0)** (the inner radius is zero, so the
  inner ring is a point), and closes: the commands are move, arc, line, close. A sector from 0 to pi + 0.5 sweeps more
  than half a turn, so its arc has the large-arc flag set and the sweep flag set, and its end point is
  (10 cos(end - pi / 2), 10 sin(end - pi / 2)) = (10 sin(end), -10 cos(end)); at the end angle pi + 0.5 that is
  (10 x -0.4794, -10 x -0.8776) = **(-4.794, 8.776)**.
- **Flattened area.** A sector of the angle `da` between the radii `r0` and `r1` has the area
  `0.5 x (r1^2 - r0^2) x da`. A quarter pie of radius 100 covers 0.5 x 10,000 x pi / 2 = **7,853.98**; the annular
  sector from 0.3 to 2.0 radians (1.7 radians) between the radii 40 and 100 covers
  0.5 x (10,000 - 1,600) x 1.7 = 0.5 x 8,400 x 1.7 = **7,140**. Both outlines are filled into a 220 by 220 pixel
  surface with the center at (110, 110) by adding the coverage of every pixel (the alpha divided by 255), and the sum
  must be within 0.5 percent of the formula (39.3 and 35.7 square pixels), which is the tolerance for the anti-aliased
  edge and the flattened curve. The same sector with the angles given in the reverse order, 2.0 to 0.3, covers the
  same area within one square pixel. A pad angle of 0.1 removes a strip of constant width along each of the two
  straight edges (the width is derived below, about 5.4 pixels for these radii, over the length of 60 pixels of the
  ring, so the area drops by roughly 650), and the test only requires that the padded area is smaller than the area
  without padding.
- **Full circle and annulus.** The angle span 0 to 2 pi is a whole turn, so the outer circle is drawn as two half
  circles: the outline of the disc of radius 50 is move to (0, -50), a clockwise large-arc command of radius 50 to
  the opposite point (0, 50), a second one back to (0, -50), and close. A ring with the inner radius 20 adds a second
  subpath that starts at the inner point of the end angle, (20 cos(3 pi / 2), 20 sin(3 pi / 2)) = (0, -20), and runs
  the two half circles in the opposite direction (the sweep flag clear), so the commands are move, arc, arc, move,
  arc, arc, close and the outline has two subpaths. With the nonzero fill rule the two opposite windings cancel inside
  the inner circle, so the hole is empty. On a 120 by 120 pixel surface with the center at (60, 60) the pixel (60, 60)
  at the center and the pixel (70, 60) at the distance of about 10 from it (both inside the radius 20) have no
  coverage, the pixels (95, 60) and (60, 25) at the distances 35.5 and 34.5 (between 20 and 50) are fully covered, and
  the pixel (115, 60) at the distance 55.5 has none; the covered area is pi x (50^2 - 20^2) = pi x 2,100 =
  **6,597.3**, within 0.5 percent (33 square pixels).
- **Swapped radii and reversed angles.** The generator treats the larger radius as the outer one, so the radii
  (50, 20) give exactly the commands of (20, 50) for the same angles. An end angle below the start angle runs
  counter-clockwise: from pi / 2 to 0, `a0 = 0` and `a1 = -pi / 2`, so the outline starts at **(10, 0)**, runs a
  small arc with the sweep flag clear to (10 cos(-pi / 2), 10 sin(-pi / 2)) = **(0, -10)**, goes to the center, and
  closes: the same quarter pie as above, wound the other way. A negative radius counts as zero, so the inner radius
  -5 gives exactly the commands of the inner radius 0.
- **Pad angle.** The pad angle `pad` is split into two halves, `ap = pad / 2`, and each edge of the slice moves inward
  by an angle that depends on the radius, so that the straight gap between neighbors has the same width at the inner
  and at the outer radius: with the pad radius `rp = sqrt(r0^2 + r1^2)`, the inner edge point moves by
  `p0 = asin(rp / r0 x sin ap)` and the outer one by `p1 = asin(rp / r1 x sin ap)`. For the pad angle 0.1, the radii
  20 and 50, and the angles 0 to pi / 2: `ap = 0.05` and `sin ap = 0.04998`; `rp = sqrt(400 + 2,500) = 53.8516`;
  `p0 = asin(53.8516 / 20 x 0.04998) = asin(0.13457) =` **0.13498** and
  `p1 = asin(53.8516 / 50 x 0.04998) = asin(0.05383) =` **0.05386**, so the inner angle shift is larger than the outer
  one, as the inner arc is shorter. With `a0 = -pi / 2` and `a1 = 0` the outline is: move to the outer start at angle
  `a0 + p1`, the clockwise outer arc to angle `a1 - p1`, the line to the inner point at angle `a1 - p0`, the
  counter-clockwise inner arc back to angle `a0 + p0`, and close (move, arc, line, arc, close), with the arcs of the
  radii 50 and 20. The start edge lies on the line `x = 0`, because the angle `a0` is straight up, and the moved edge
  points are at `r1 cos(a0 + p1) = r1 sin p1 = rp x sin ap = 53.8516 x 0.04998 =` **2.6915** (the gap value that the
  test names, and equal to `r0 sin p0`) to the right of that line at both radii: the outer point is
  (2.6915, -49.9275) and the inner point is (2.6915, -19.818). The edge that the pad creates is therefore parallel to
  the original edge at a constant distance of 2.6915, and the gap between two neighbors is twice that, 5.383. A
  negative pad angle counts as no pad, giving exactly the commands of the pad angle 0.
- **Corner radius.** With the radii 20 and 50, a quarter turn from 12 o'clock, and the corner radius 5 (below the
  limit of half the ring width, (50 - 20) / 2 = 15), each of the four corners is rounded by a circle of radius 5 that
  is tangent to the edge and to the ring, so the outline is: move to the tangent point on the start edge, the corner
  arc (radius 5), the outer arc (radius 50), the other corner arc (5), the line to the inner ring, then the inner
  corner arc (5), the inner arc (radius 20), and the last corner arc (5), and close (move, arc, arc, arc, line, arc,
  arc, arc, close; the arc radii are 5, 50, 5, 5, 20, 5). The outer corner circle at the start edge (the line
  `x = 0`) lies inside the outer ring and touches both: its center is 5 from the edge and 50 - 5 = 45 from the
  origin, so it is at `(5, -sqrt(45^2 - 5^2)) = (5, -sqrt(2,000)) = (5, -44.7214)`, the first tangent point on the
  edge is **(0, -44.7214)**, and the end of the first corner arc is the center scaled out to the ring,
  (5, -44.7214) x 50 / 45 = **(5.5556, -49.6904)**. The inner corner circle at the start edge lies outside the inner
  ring and touches it from outside: its center is 5 from the edge and 20 + 5 = 25 from the origin, at
  `(5, -sqrt(25^2 - 5^2)) = (5, -sqrt(600)) = (5, -24.4949)`, so the last corner arc ends on the edge at
  **(0, -24.4949)**, the eighth tuple (index 7) of the command list. A corner radius above half the ring width is
  clamped: for the radii 20 and 30 the limit is (30 - 20) / 2 = 5, so the corner radius 100 gives exactly the
  commands of the corner radius 5, and no corner arc is larger than 5. A negative corner radius counts as none. The
  generator also reduces the radius when the corner circles of a narrow sector do not fit and merges the two corners
  of an edge that meet; those branches are reached by the narrow sector of the totality scenario, which asserts that
  they complete and give a path.
- **Degenerate and non-finite input.** An outer radius of zero (or at most 1e-12 after the swap) is a point: one
  subpath that has only the move to the origin and the close (move, close), and the radii (5e-13, 1e-13) give the
  same commands. A sector of zero angle (the angles 1 and 1, radii 0 and 10) collapses to its edge: move to
  (10 sin 1, -10 cos 1) = **(8.4147, -5.4030)**, line to the center (0, 0), close (move, line, close). Any input that
  is not finite (NaN or an infinity in a radius, angle, pad angle, or corner radius) and any value whose magnitude is
  above 1e9 (the outer radius 1e10, the end angle 2e9) gives the empty path, while the radius 1e9 itself is still
  drawn. A pad angle as large as the sector (the radii 20 and 50, the angles 0 to 0.05, the pad angle 0.2:
  `p0 = asin(2.6926 x sin 0.1) = 0.272` and `p1 = asin(1.0770 x sin 0.1) = 0.108` are each larger than half the
  sector angle 0.025, so both rings collapse to the middle angle) still gives a path (a line from the middle angle at
  the outer radius to the one at the inner radius). A corner radius of 1e6 on a narrow pie slice of radius 50 (the
  angles 0 to 0.02) is limited to half the ring (25) and then by the angle to about 0.5 (the corner circles fit only
  for 50 / (100 + 1)), so that the two outer corners merge, and gives a path; a three-quarter turn with a pad angle
  and a corner radius gives a path; none of them throws.

Unit tests reside in `ArcGeometryTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Scenegraph` folder. The painting of the outline (the wedge and the hole, the stroke, and the placement) is verified
through the painter in `ScenePainterPathTests.cs` (see the _ScenePainter Unit Verification Design_).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None; no file or network is read
- **Isolation**: Each test builds its own paths and, for the area and the hole, its own surface and disposes it; the
  method is pure and uses no clock, culture, or host font

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any command list, end point, radius, or arc flag that differs from the hand-derived outline (within the
single precision tolerance), a covered area outside the stated tolerance of the sector formula, a hole that is
filled, a swapped or reversed input that does not give the outline that was derived, a pad angle or corner radius
that is not applied or not clamped as derived, an input that is not finite or too large that does not give the empty
path, or any exception constitutes a failure.

#### Unit-Level Test Scenarios

##### Quarter Sector Has Hand-Derived Commands

**Tests**: `ArcGeometryTests.ArcGeometry_Build_QuarterSectorHasHandDerivedCommands`

Asserts the commands of the quarter pie slice (the move to (0, -10), the small clockwise arc of radius 10 to (10, 0),
the line to (0, 0), and the close) and, for the angles 0 to pi + 0.5, the large-arc flag, the sweep flag, and the end
point (10 sin(end), -10 cos(end)).

##### Flattened Area Matches The Sector Formula

**Tests**: `ArcGeometryTests.ArcGeometry_Build_FlattenedAreaMatchesTheSectorFormula`

Asserts that the filled area of the quarter pie of radius 100 is 7,853.98 and that of the annular sector 7,140, each
within 0.5 percent, that the reversed angles give the same area within one square pixel, and that a pad angle makes
the area smaller.

##### Full Circle And Annulus Use Opposite-Winding Subpaths

**Tests**: `ArcGeometryTests.ArcGeometry_Build_FullCircleAndAnnulusUseOppositeWindingSubpaths`

Asserts the disc as move, arc, arc, close with two clockwise large-arc half circles of radius 50 through (0, 50) back
to (0, -50); the ring as two subpaths (move, arc, arc, move, arc, arc, close) with the outer circle clockwise and the
inner circle of radius 20 counter-clockwise; the empty hole, the covered ring, and the empty outside at the five
pixels derived above; and the covered area pi x 2,100 within 0.5 percent.

##### Swapped Radii And Reversed Angles

**Tests**: `ArcGeometryTests.ArcGeometry_Build_SwapsRadiiAndAcceptsReversedAngles`

Asserts that the radii (50, 20) give the commands of (20, 50), that the angles pi / 2 to 0 give the quarter pie that
starts at (10, 0) and runs a small counter-clockwise arc to (0, -10), and that a negative inner radius gives the
commands of an inner radius of zero.

##### Pad Angle Moves The Edges By The Derived Angles

**Tests**: `ArcGeometryTests.ArcGeometry_Build_PadAngleMovesTheEdgesByTheDerivedAngles`

Asserts the five commands of the padded ring (move, arc, line, arc, close), the four end points at the angles
`a0 + p1`, `a1 - p1`, `a1 - p0`, and `a0 + p0` with `p0` = 0.13498 and `p1` = 0.05386, the radii and sweep flags of
the two arcs, that the two points of the start edge are at x = rp sin(pad / 2) = 2.6915 and that `p0 > p1`, and that a
negative pad angle gives the commands of no pad.

##### Corner Radius Rounds The Corners And Clamps To Half The Ring

**Tests**: `ArcGeometryTests.ArcGeometry_Build_CornerRadiusRoundsTheCornersAndClampsToHalfTheRing`

Asserts the nine commands and the six arc radii (5, 50, 5, 5, 20, 5), the first tangent point (0, -44.7214), the end
of the first corner arc (5.5556, -49.6904), the last point (0, -24.4949), that a corner radius of 100 on a ring 10
wide gives the commands of the corner radius 5 with no corner arc above 5, and that a negative corner radius gives the
commands of none.

##### Degenerate And Non-Finite Inputs Give Empty Or Point Paths

**Tests**: `ArcGeometryTests.ArcGeometry_Build_DegenerateAndNonFiniteInputsGiveEmptyOrPointPaths`

Asserts the point path (move, close) for an outer radius of zero and for radii below 1e-12, the collapsed edge (move
to (8.4147, -5.4030), line to the center, close) for a sector of zero angle, the empty path for NaN or an infinity in
each of the six inputs and for magnitudes of 1e10 and 2e9 while 1e9 is still drawn, and that a pad angle as large as
the sector, a huge corner radius on a narrow sector, and a three-quarter turn with a pad angle and a corner radius
each give a path without an exception.
