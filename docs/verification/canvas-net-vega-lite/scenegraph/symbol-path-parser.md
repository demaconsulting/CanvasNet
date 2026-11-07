### SymbolPathParser Unit Verification Design

This document describes the unit-level verification strategy for the `SymbolPathParser` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace: the internal sealed `SymbolPathParser` class, which parses
the SVG path data of a custom symbol shape the way vega-scenegraph's `path/parse.js` does, draws it scaled to a symbol
size the way `customSymbol` does (`path/render.js`, `path/arc.js`, and the scaling of `path/symbols.js`), and computes
the bounds of the drawing with the extrema of its curves (`bound/boundContext.js`). The painter draws the returned path
and the legend uses its bounds (see the _ScenePainter Unit Verification Design_ and the _LegendBuilder Unit
Verification Design_).

#### Verification Approach

The unit is verified by parsing hand-written path texts and asserting on the number of commands, on the bounds that
`Render` returns, and, where the shape of the path is the contract, on the sub-paths and commands of the core `Path`.
No collaborator is replaced: `Path`, `PathBuilder`, and `SceneBounds` are real, and the internal types are reached
through the test assembly's `InternalsVisibleTo` access. Coordinates are compared within 1e-4 because the core path
stores single precision numbers. The tests draw at the size 4 where the coordinates must not be scaled: the radius
is `sqrt(4) / 2 = 1`, so a path coordinate is a pixel. The expected values are derived by hand from the grammar, the
radius `sqrt(size) / 2`, and the curve formulas.

- **Scaling.** The square `M0,0 L1,0 L1,1 L0,1 Z` at the sizes 4 and 16 has the radii 1 and 2, so its bounds are
  0 to 1 and 0 to 2 on each axis; the square from (-1, -1) to (1, 1) at the size 100 (the radius 5) has the bounds
  **-5 to 5**, and its only sub-path starts at (-5, -5).
- **Repetition and the number grammar.** `M0,0 4,0 4,4` has three commands (a move and two line-tos, the repeat of an
  `M` being an `L`), `m1,1 2,0 0,2` has three (the repeats of an `m` are relative) and, drawn from (1, 1) by +2 and
  +2, ends at (3, 3); `M0,0 H1 2 3` has four. `M-1.5-2.5L.5.5` reads the numbers that touch at a sign and at a second
  decimal point and has the bounds (-1.5, -2.5) to (0.5, 0.5); `M1e1,2E-1 L 3. , +4` has the exponents, a trailing
  decimal point, and a plus sign, with the bounds x from 3 to 10 and y from 0.2 to 4; `M0,0a1 1 0 0125 5` reads the
  flags `0` and `1` as single digits followed by `25` and `5`, giving two commands.
- **Errors.** `M0,x`, `M0`, `M0,0 1`, `M0,0 L1,1,2`, an arc flag of 2, and an arc with six numbers are errors of the
  parameter type; `M0,0 h`, `l`, and `c` without numbers are errors of the parameter count; `t1,1` as the first command
  is an error. Each gives a null path and a reason, and `IsValid` is false. A text of `fgi0.5 M1,1 L2,2` skips the
  text before the first letter and has two commands; a text with no command letter (`fgi`) has none, draws no
  sub-path, and has empty bounds; `z` alone has one. A path of `MaxCommands` (4,096) commands is valid and one of
  4,097 is not, whether the commands are written out or are the repetition of one command.
- **Canvas rules.** `L5,5 L10,0` (no move) is one sub-path that starts at (5, 5) and ends at (10, 0); after
  `M0,0 L2,0 L2,2 Z` the line `L5,5` starts a second sub-path at the start of the closed one, (0, 0); in
  `M0,0 L1e999,1 L3,3` the line to an infinite number is skipped and the next one (3, 3) is drawn.
- **Curve bounds and `S`.** `M10,10 S20,20 30,10` reflects the control point `(0, 0)` through the current point
  (10, 10), so the first control point is (20, 20): the cubic with the heights 10, 20, 20, and 10 has its extremum at
  `t = 0.5` with the weights 1/8, 3/8, 3/8, and 1/8, that is `(10 + 3 x 20 + 3 x 20 + 10) / 8 = 17.5`, so the bounds
  are (10, 10) to (30, **17.5**). In `M0,0 q5,5 10,0 t10,0` the quadratic reaches `2 x 0.25 x 5 = 2.5`, and the `t`
  reflects (5, 5) through (10, 0) to (15, -5) and reaches **-2.5**: the bounds are (0, -2.5) to (20, 2.5).
- **A scaled `t`.** At the size 16 (the radius 2) the same path has the quadratic from (0, 0) with the control
  (10, 10) to (20, 0), and the `t` of (20, 0) more sees `t` as the previous command, so its control is not a number and
  the canvas skips it: the path has one command, and the bounds still contain its end point (40, 0): (0, 0) to
  (40, **5**) (the extremum of the quadratic is 0.5 x 10 = 5).
- **Arcs.** The arc `M0,0 A1,1 0 0 1 2,0` is a half circle, which is drawn as two curves (a quarter turn each) and has
  the bounds (0, -1) to (2, 0) (the sweep flag 1 goes up on the screen); with the sweep flag 0 the bounds are (0, 0)
  to (2, 1); the radius 0.1, which is too small for the chord of 2, is enlarged to the half circle and gives the first
  bounds again.
- **Sharing.** `TryGet` of the same text twice returns the same object; a text that does not parse (`M0,`) is null both
  times; two renders of one size return the same `Path` and two different sizes do not.
- **Painter and legend.** At the size 100 (the radius 5) the rectangle `M-1,-1 L1,-1 L1,3 L-1,3 Z` is drawn from
  (-5, -5) to (5, 15); a path that does not parse (`M0,0L`) gives the commands of a circle of the radius 5; the symbol
  bounds of the legend for that shape at (10, 20) are (5, 15) to (15, 35) and, with a stroke of width 2, (1, 11) to
  (19, 39) (the stroke grows the bounds by 4 pixels on every side, the allowance of the stroke bounds); a text that
  draws nothing (`fgi`) has empty bounds; `IsDrawableShape` is true for `M0,0L1,1` and `circle` and false for `M0,0L`.

The scenarios cover the requirements of the unit as follows: `CanvasNetVegaLite-Scenegraph-SymbolPathParser-Parse` by
the repetition, number grammar, error, skipped text, and limit scenarios; `-RenderCanvasRules` by the canvas rules, the
reflected control points, the scaled `t`, and the arcs; `-Bounds` by the curve extrema, the scaled `t`, and the painter
and legend bounds; `-Cache` by the sharing scenario, the scenario that a repeated invalid path is cached as a failure and
parsed once while a path longer than 8,192 characters is not cached, and the scenario that unique long paths added from
four threads never make the cache exceed
its 256 entries or 262,144 characters; and `-Scaling` by the scaling scenario and the painter scenario.

Unit tests reside in `SymbolPathParserTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Scenegraph` folder. The report of the invalid path by the catalog is verified in `FeatureCatalogTests.cs` (see the
_FeatureCatalog Unit Verification Design_), and the whole path from a specification is verified in the system test
`VegaLiteChartIntegrationTests.CanvasNetVegaLite_SystemIntegration_CustomPathShapeIsDrawnAtTheSymbolRadius`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None; no file or network is read
- **Isolation**: Each test parses its own text; the shared cache of parsed paths holds only immutable parsed paths, so
  the tests do not depend on each other; the unit uses no clock, culture, or host font

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any command count, bounds, sub-path, or end point that differs from the hand-derived value (within the single
precision tolerance), a text that vega rejects and is accepted (or the reverse), a path that is not scaled by the radius,
a cache that does not share, or any exception constitutes a failure.

#### Unit-Level Test Scenarios

##### Render Scales The Coordinates By The Radius

**Tests**: `SymbolPathParserTests.SymbolPathParser_Render_ScalesTheCoordinatesByTheRadius`

Asserts the bounds 1, 2, and 5 times the path at the sizes 4, 16, and 100 and the start of the sub-path (-5, -5).

##### Extended Parameter Lists Repeat The Command

**Tests**: `SymbolPathParserTests.SymbolPathParser_TryParse_ExtendedParameterListsRepeatTheCommand`

Asserts the command counts of the absolute and relative move-to repeats and of `H`, the bounds of the absolute path, and
the start and end of the relative one.

##### Reads The Numbers Of Vega

**Tests**: `SymbolPathParserTests.SymbolPathParser_TryParse_ReadsTheNumbersOfVega`

Asserts the bounds of the touching numbers, the exponents, the trailing decimal point, the plus sign, and the mixed
separators, and the two commands of the arc with touching flags.

##### Rejects What Vega Throws For

**Tests**: `SymbolPathParserTests.SymbolPathParser_TryParse_RejectsWhatVegaThrowsFor`

Asserts for the ten texts above a null result, the reason (parameter type, parameter count, or first command), and
`IsValid` false.

##### Skips Text Before The First Command And Accepts No Command

**Tests**: `SymbolPathParserTests.SymbolPathParser_TryParse_SkipsTextBeforeTheFirstCommandAndAcceptsNoCommand`

Asserts the two commands after the skipped text, the zero commands, the empty path and bounds of `fgi`, and the one
command of `z`.

##### Rejects More Commands Than The Limit

**Tests**: `SymbolPathParserTests.SymbolPathParser_TryParse_RejectsMoreCommandsThanTheLimit`

Asserts that 4,097 commands (written out or from one command) are null with a reason that names the commands and that
4,096 are accepted.

##### Render Follows The Canvas Rules

**Tests**: `SymbolPathParserTests.SymbolPathParser_Render_FollowsTheCanvasRules`

Asserts the sub-paths of the line without a move, of the line after a close, and of the skipped infinite number.

##### Curves Have Their Extrema And Smooth Curves Reflect The Last Control

**Tests**: `SymbolPathParserTests.SymbolPathParser_Render_CurvesHaveTheirExtremaAndSmoothCurvesReflectTheLastControl`

Asserts the bounds (10, 10) to (30, 17.5) and (0, -2.5) to (20, 2.5) derived above.

##### Scaled Smooth Quadratic Sees Itself As The Previous Command

**Tests**: `SymbolPathParserTests.SymbolPathParser_Render_ScaledSmoothQuadraticSeesItselfAsThePreviousCommand`

Asserts the single drawn command and the bounds (0, 0) to (40, 5).

##### Arcs Are Bezier Curves Of At Most A Quarter Turn

**Tests**: `SymbolPathParserTests.SymbolPathParser_Render_ArcsAreBezierCurvesOfAtMostAQuarterTurn`

Asserts the two curves of the half circle, the three bounds of the two sweeps and of the enlarged radius.

##### Parsed Paths And Drawings Are Shared

**Tests**: `SymbolPathParserTests.SymbolPathParser_TryGet_SharesParsedPathsAndDrawings`

Asserts the shared parsed path, the remembered failure, the shared drawing of one size, and the distinct drawing of
another.

##### The Painter Draws Custom Paths And Falls Back To A Circle

**Tests**: `SymbolPathParserTests.ScenePainter_SymbolPath_DrawsCustomPathsAndFallsBackToACircle`

Asserts the path bounds (-5, -5) to (5, 15), the circle fallback, the legend symbol bounds without and with a stroke,
the empty bounds of a path that draws nothing, and `IsDrawableShape`.
