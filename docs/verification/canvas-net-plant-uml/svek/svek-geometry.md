### SvekGeometry Unit Verification Design

This document describes the unit-level verification strategy for the `SvekGeometry` unit in the
`DemaConsulting.CanvasNet.PlantUml.Svek` namespace.

#### Verification Approach

The unit is verified with routes, boxes, and directions given in the tests, asserting the segments of the curves, the
clipped points, and the decorations; the arrow head, the extension triangle, and the diamonds are compared with the
polygons of pictures drawn by the PlantUML jar. No mocking is required. Unit tests reside in
`Svek/SvekGeometryTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A point that differs from the expected value by more
than the stated tolerance, or a count, fill, or exception that differs from the expected one, constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Svek-SvekGeometry-Spline: Curves Through Routes

**Tests**: `SvekGeometryTests.PlantUmlSvekGeometry_Spline_TwoPoints_IsALine`,
`SvekGeometryTests.PlantUmlSvekGeometry_Spline_ThreePoints_IsTheQuadraticCurve`,
`SvekGeometryTests.PlantUmlSvekGeometry_Spline_FourPoints_IsTheBezierOfThePoints`,
`SvekGeometryTests.PlantUmlSvekGeometry_Spline_LongRoute_IsSmoothAndClamped`,
`SvekGeometryTests.PlantUmlSvekGeometry_Spline_OnePoint_Throws`,
`SvekGeometryTests.PlantUmlSvekGeometry_Flatten_FollowsTheCurve`

Asserts a line for two points, the cubic raised from the quadratic curve of three points, and the Bezier curve of four
points; for routes of 5, 6, and 9 points asserts one cubic per span, the start and end at the ends of the route, the
first and last control points on the first and last legs, and C1 joins; asserts an `ArgumentException` for one point.
Asserts the points along a line (its two ends) and along a curve of three points (8 steps: 9 points from the first end
to the last, the middle step at the middle of the quadratic curve), and an `ArgumentOutOfRangeException` for no step.

##### CanvasNetPlantUml-Svek-SvekGeometry-Clipping: Clipping And Route Ends

**Tests**: `SvekGeometryTests.PlantUmlSvekGeometry_Clip_EveryShape_EndsOnTheOutline`,
`SvekGeometryTests.PlantUmlSvekGeometry_Contains_EveryShape`,
`SvekGeometryTests.PlantUmlSvekGeometry_CutAtBox_BothEnds`,
`SvekGeometryTests.PlantUmlSvekGeometry_Shorten_MovesEndsAlongTheirLegs`,
`SvekGeometryTests.PlantUmlSvekGeometry_ArrivalAngle_BothEnds`,
`SvekGeometryTests.PlantUmlSvekGeometry_ClipToFacingSide_EndsOnTheFacingSide`

Asserts where directions from the center leave a rectangle, an ellipse, a circle, and a diamond; asserts points inside
and outside each outline; asserts a route cut where it enters a box at either end and kept when it never enters it;
asserts the ends moved back along their legs and never past the next point; asserts the arrival direction at each end,
skipping legs without length. Asserts the end on the facing side of a rectangle for points below, above, right, and
left of it: in line with the point, or 10 pixels from the corner (a quarter of a short side when that is less); and
none for a point beside the box along the ranks, an ellipse, or a diamond.

##### CanvasNetPlantUml-Svek-SvekGeometry-Extremities: Link Decorations

**Tests**: `SvekGeometryTests.PlantUmlSvekGeometry_Extremity_Arrow_MatchesPlantUml`,
`SvekGeometryTests.PlantUmlSvekGeometry_Extremity_Extends_MatchesPlantUml`,
`SvekGeometryTests.PlantUmlSvekGeometry_Extremity_Diamonds_MatchPlantUml`,
`SvekGeometryTests.PlantUmlSvekGeometry_Extremity_OtherShapes_HavePlantUmlSizes`,
`SvekGeometryTests.PlantUmlSvekGeometry_IsDrawnAndMargin_FollowPlantUml`,
`SvekGeometryTests.PlantUmlSvekGeometry_Arc_StaysOnTheCircle`

Asserts the filled arrow head, the hollow extension triangle, and the filled composition and hollow aggregation diamonds
against the polygons of PlantUML's pictures (to three decimals), with their contact lengths 5, 18, and 12; asserts the
sizes, fills, strokes, and contact lengths of the circles, the circle with a socket, the socket, the square, the circled
plus, the cross, the small triangle, and the half arrows; asserts that the crow's foot family and the redefinition
triangles are not drawn and PlantUML's margins of the decorations; asserts that an arc starts and ends on its circle in
pieces of at most a quarter circle.
