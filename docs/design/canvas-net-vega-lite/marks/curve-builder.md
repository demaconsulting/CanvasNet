### CurveBuilder

![Marks Structure](VegaLiteMarksView.svg)

The `CurveBuilder` unit is the second software unit in the `Marks` subsystem. It comprises three source files in the
`DemaConsulting.CanvasNet.VegaLite.Marks` namespace; together they define the `internal static partial class
CurveBuilder`, the types that its methods take, and the private curve classes:

- `CurveBuilder.cs` - the public surface of the unit (`TryParse`, `SupportsArea`, `MaxSegments`), the enumeration
  `CurveKind`, the two point records `CurvePoint` and `AreaPoint`, the factory that creates the curve of a kind, the
  `SegmentSink` that emulates the path context, the abstract `Curve` state machine, and the straight, stepped, and
  B-spline curves (`LinearCurve`, `LinearClosedCurve`, `StepCurve`, `BasisCurve`, `BasisOpenCurve`,
  `BasisClosedCurve`, and `BundleCurve`)
- `CurveBuilder.Splines.cs` - the spline curves (`CardinalCurve`, `CardinalOpenCurve`, `CardinalClosedCurve`,
  `CatmullRomCurve`, `MonotoneCurve`, and `NaturalCurve`)
- `CurveBuilder.Generators.cs` - the two generators `Line` and `Area`, which drive a curve through a list of points

Every file carries the attribution header comment for the d3-shape, d3-path, Vega, and Vega-Lite behavior that the unit
ports (see _CanvasNetVegaLite System Design_, Third-Party Provenance and Attribution, and the file `third-party/README.md`
of the package).

#### Purpose

`CurveBuilder` turns the pixel points of a line or an area into the list of path segments that the painter draws. It is
the one place that knows the sixteen curves of Vega-Lite's `interpolate` property, so that `MarkBuilder` only chooses a
curve, lists points, and charges the budget. It exists as a unit of its own because the curves are numeric algorithms
that must match, coordinate by coordinate, the d3-shape curves that Vega-Lite (through Vega) uses, and because the
number of segments of a path has to be bounded before the path is generated.

The curve classes are **literal ports** of the d3-shape curves, including the state machines (`lineStart`, `point`,
`lineEnd`, `areaStart`, and `areaEnd`) and the toggle (`_line` in d3-shape) that lets the baseline pass of an area
continue the path of the top line. `Line` and `Area` are the generators of d3-shape's `line` and `area`, and the
`SegmentSink` is an emulation of the path context of d3-path. The code is original C# written from the behavior of
those projects; no upstream text is copied.

The unit's behavior is specified by the `CanvasNetVegaLite-Marks-CurveBuilder-*` requirements in
`docs/reqstream/canvas-net-vega-lite/marks/curve-builder.yaml`.

#### Data Model

The class is static and keeps no state between calls: a call creates its own curve object and path context, works on
the points it is given, and appends segments to the list it is given. One call serves one thread.

**`CurveKind`** has sixteen members: `Linear`, `LinearClosed`, `Step`, `StepBefore`, `StepAfter`, `Basis`,
`BasisOpen`, `BasisClosed`, `Bundle`, `Cardinal`, `CardinalOpen`, `CardinalClosed`, `CatmullRom`, `MonotoneX`,
`MonotoneY`, and `Natural`. Vega-Lite names fifteen interpolations; `monotone` is two members, because the curve of a
horizontal area is a function of y.

**`CurvePoint(X, Y, Defined)`** is a vertex of a line in pixels. **`AreaPoint(Along, Top, Base, Defined)`** is a vertex
of an area: `Along` is the coordinate along the axis of the area (x of a vertical area, y of a horizontal one), `Top`
the coordinate of the value edge across (y1 or x1), and `Base` the coordinate of the baseline across (y0 or x0). A
point is **defined** when its flag is set and its coordinates are finite numbers; any other point ends the current run
of consecutive points (see Defined Gaps below).

**The names and the kinds** (`TryParse`) are the fifteen values of Vega-Lite's `Interpolate`, compared with ordinal,
case-sensitive equality:

| Name | `CurveKind` | Curve class |
| --- | --- | --- |
| `linear` | `Linear` | `LinearCurve` |
| `linear-closed` | `LinearClosed` | `LinearClosedCurve` |
| `step` | `Step` | `StepCurve` (change at 0.5 of the interval) |
| `step-before` | `StepBefore` | `StepCurve` (change at 0) |
| `step-after` | `StepAfter` | `StepCurve` (change at 1) |
| `basis` | `Basis` | `BasisCurve` |
| `basis-open` | `BasisOpen` | `BasisOpenCurve` |
| `basis-closed` | `BasisClosed` | `BasisClosedCurve` |
| `bundle` | `Bundle` | `BundleCurve` (`BasisCurve` when the beta is 1) |
| `cardinal` | `Cardinal` | `CardinalCurve` |
| `cardinal-open` | `CardinalOpen` | `CardinalOpenCurve` |
| `cardinal-closed` | `CardinalClosed` | `CardinalClosedCurve` |
| `catmull-rom` | `CatmullRom` | `CatmullRomCurve` (`CardinalCurve` of tension 0 when the alpha is 0) |
| `monotone` | `MonotoneX` (`MonotoneY` for a horizontal area) | `MonotoneCurve` (reflected for `MonotoneY`) |
| `natural` | `Natural` | `NaturalCurve` |

Any other text, a null name, and the Vega-only names `catmull-rom-open` and `catmull-rom-closed` are not accepted.

**The path context.** `SegmentSink` appends `ScenePathSegment` values (absolute `Move`, `Line`, `Cubic`, and `Close`
segments) to the output list in the order d3-path would write the commands. It adds the d3-path rules that matter for the
output and two guards of its own:

- a close segment is written only after a move has been written (d3-path writes a close only when a subpath is open);
- a move or a line to a point that cannot be drawn (not finite, or beyond `ScenePainter.MaxMagnitude`, the one bound of
  the painter) is not written;
- a cubic whose control point cannot be drawn becomes a line to its end point, and a cubic or line whose end point
  cannot be drawn is not written.

So **no segment with a coordinate that is not finite, or beyond the bound, is ever emitted**, even when huge coordinates
overflow inside a curve formula or a huge `tension` makes the tangents as long as it likes.

**The curve state machine.** The abstract `Curve` holds the sink and the area pass (`Pass`, the `_line` of d3-shape:
not a number outside an area, 0 on the top line of an area, 1 on its baseline). Its helpers are `Begin` (a line to the
first point of a line on the baseline pass, so that the baseline continues the path of the top line, and a move
otherwise), `ClosesAt` (the d3-shape condition for closing the subpath at the end of a line: on the baseline pass
always, outside an area when the point counter has the value at which the curve closes, never on the top pass), and
`FlipPass`. Each curve overrides `LineStart`, `LineEnd`, and `Point`; the curves that have no area passes of their own
(`LinearClosed`, `BasisClosed`, `CardinalClosed`) override `AreaStart` and `AreaEnd` as no-ops, so an area of a closed
curve is two closed polygons; `BundleCurve` throws `NotSupportedException` from both.

**The curves.**

- _Linear_: a move to the first point and a line to each next point; at the end of a line the subpath is closed when
  the pass says so (a lone point is then a move and a close). _Linear closed_ writes the same polyline and always
  closes it.
- _Step family_: in each interval the curve runs at the old value to the change position (the first point for
  `step-before`, the middle for `step`, the second point for `step-after`), changes to the new value there, and runs on
  to the new point; a centered step finishes its last interval with a line to the final point. The step curves
  therefore write up to two lines per interval. On the baseline pass of an area the change position is flipped, because
  the baseline runs backward.
- _Basis family_ (uniform cubic B-spline): the first interval is a line to the point one sixth of the way to the second
  point followed by cubic segments whose control points are thirds of weighted sums of the three last points; the end
  adds the cubic that reaches the last point and a line to it. _Basis open_ starts at the first knot (one sixth of the
  weighted sum of the first three points) and ends at the last knot. _Basis closed_ replays its first three points at
  the end so that the loop closes.
- _Bundle_: the points of a line are buffered; at the end the points are blended with the straight line between the
  first and the last point (the point is `beta` times itself plus `1 - beta` times its place on the line) and given to
  a basis curve. A beta of exactly 1 is the basis curve itself. A line with fewer than two points draws nothing.
- _Cardinal family_: cubic segments whose control points move from the ends of the interval by `k` times the chord of
  the neighbors, with `k = (1 - tension) / 6`; the first and the last point are doubled so that the curve reaches
  them. _Cardinal open_ starts at the second point and ends at the second to last; _cardinal closed_ replays its first
  three points like the closed basis curve.
- _Catmull-Rom_: the cardinal construction with the control points corrected by the chord lengths raised to the power
  `alpha` (0.5 is the centripetal form, 1 the chordal form), where a chord shorter than `Epsilon` (1e-12) skips its
  correction. An alpha of 0 is the cardinal curve with a tension of 0.
- _Monotone_ (X): a cubic Hermite spline whose tangent at each point is the monotone slope (the sign agreement of the
  two neighbor secants: twice the least of the two secant magnitudes and half the weighted mean magnitude, and 0 when
  the secants differ in sign), the end tangents being the one-sided slope; points that coincide with the previous point
  are ignored; the division by a zero interval width
  follows the JavaScript rules that d3-shape relies on (the sign of a zero width follows the other width, and a slope
  that is not a number counts as 0). _MonotoneY_ feeds the curve the points with their coordinates swapped and swaps the
  coordinates of every written segment back.
- _Natural_: the points of a line are buffered, and at the end the two control points of each interval are found by
  solving the tri-diagonal system of the natural cubic spline separately for x and for y (zero second derivative at both
  ends); with two points the curve is a line, with one a lone point.

**Tension and alpha.** Vega-Lite's `tension` property has a different meaning for three curves (Vega's `curves()`):
the `beta` of a bundle (default 0.85), the tension of a cardinal curve (default 0, all three variants), and the `alpha`
of a Catmull-Rom curve (default 0.5). Every other curve ignores it. A tension that is not a finite number counts as
absent (d3-shape would produce geometry that is not a number). Any finite tension is otherwise accepted, as d3 and
Vega-Lite accept any number (the Vega-Lite schema gives no range, so the parser applies none, to a mark definition or
to the configuration); a tension large enough to make the control points of a spline unreasonably long cannot make an
unbounded drawing, because the path context turns a cubic with a control point beyond the painter's bound into a
line to its end point.

**The area.** An area is generated per run of defined points: the curve draws the top line forward (`AreaStart`,
`LineStart`, the points `(Along, Top)`, `LineEnd`), then the baseline backward from the last point of the run to the
first as a second line of the same area (`LineStart`, the points `(Along, Base)`, `LineEnd`, `AreaEnd`). A horizontal
area swaps the coordinates (`(Top, Along)` and `(Base, Along)`). The curves close the outline through the `_line`
toggle: the top pass leaves the path open, the baseline pass continues it with a line to its first point and closes it.
The `bundle` curve has no area methods in d3-shape, so `SupportsArea(Bundle)` is false.

**Defined gaps.** `Line` and `Area` loop over the points once, with one step past the end that acts as an undefined
point. A change from undefined to defined starts a line (or the top line of an area), and a change from defined to
undefined ends it. The curve therefore starts a new subpath at each point that follows an undefined one. A run of one
point is drawn as d3-shape draws it: a move and a close for the curves that toggle, nothing for the open curves
(`BasisOpen`, `CardinalOpen`) and for `Bundle`.

**Limits** (every loop or allocation of the unit):

- _Points_: the unit allocates nothing in proportion to the points except the output segments and, for `Bundle` and
  `Natural`, the buffered coordinates of one run (two lists of doubles, and for `Natural` the arrays of its tri-diagonal
  solve, each as long as the run); the caller bounds the points (they come from the data rows, already bounded by the
  loader) and charges the
  segments.
- _Segments_: `MaxSegments(kind, definedPoints, runs, area)` is the upper bound of the number of segments that `Line` or
  `Area` writes: `factor * definedPoints * (area ? 2 : 1)` plus the extra segments of the runs, with `factor` 2 for the
  three step curves (two lines per interval) and 1 for every other curve, and 0 when there are no defined points. The
  extra part covers the moves, the closes, and the end pieces of the curves and depends on the curve and on whether an
  area is generated: a run of `n` points writes `e(n)` segments beyond the points part, which is 0 or 1 for most curves
  (one move or one close), 2 for the closed area outlines and the lines of the basis curves (the end pieces), and 5 for
  the area of a basis curve with three points or more (the end pieces of the top line and of the baseline, and the
  close); the open curves and the step-before and step-after curves write fewer segments than their points and count
  as 0. The bound keeps two numbers for each curve and mode, `small` for runs of one or two points and `big` for any
  run, and counts at most `(D - R) / 2` runs as long (a run of three or more points has two points more than the one
  that every run has), `big` for those and `small` for the other runs, so a line of isolated points (`D = R`) is
  charged one point and `small` (at most one) segment per run, not the worst case of a long run: 500,000 isolated
  points cost at most 1,000,000 segments for every curve. A bundle has the numbers of the basis curve (a beta of 1 is
  the basis curve). The proof in
  the remarks of `MaxSegments` rests on three facts: the runs are independent, the segment count of a d3-shape curve does
  not depend on the coordinates (the path context can only drop a segment), and the curves are state machines with at
  most four states, so `e(n)` is constant from three points on. A run that has points counts as at least one run, and
  there are never more runs than points. The caller must count the points that are drawable by the same rule as the
  generators (finite and within the painter's bound), because a point that the generators treat as undefined would
  split a run. The result saturates at the largest `long` instead of overflowing (an input above one sixteenth of it
  gives the largest `long`). A test checks that the real count never exceeds the bound, for every kind, for lines and
  areas, for the tensions that change the class of a curve, and for every pattern of defined points up to a length.
- _Cancellation_: the generators poll the token when the loop index is 0 and then every 1,024 points (the loop index
  includes the one step past the end), so a canceled token stops even a line of one point, and a path of a million
  points can be abandoned.

**Determinism.** The unit uses only the points it is given and double arithmetic (and `Math.Pow` and `Math.Sqrt` for the
chord lengths of the Catmull-Rom curve); no clock, culture, or random source. The same points give the same segments on
every call and thread.

**Documented deviations from d3-shape and Vega-Lite**:

1. **`MaxSegments`** counts the extra segments of the runs by curve and mode (see Limits) and not by one constant: the
   worst case is the area of a basis curve with three points or more, which needs `2 * points + 5` segments per run, and
   most other cases need one or none.
2. **`catmull-rom-open` and `catmull-rom-closed`** are Vega-only values, not Vega-Lite values, and are not ported; the
   sixteen-member `CurveKind` is the contract.
3. **`bundle` has no area**: `SupportsArea` is false and `Area` refuses it; `MarkBuilder` draws such an area with the
   `linear` curve and the feature catalog reports `vl-mark-interpolate-bundle-area`.
4. **The closed curves** (`BasisClosed`, `CardinalClosed`) end a loop of three or more points with the replayed
   segments and write no close segment, as their state machines do; a closed outline is therefore not marked closed
   for those curves.
5. **A `tension` that is not finite** counts as absent; **any finite tension** is accepted (no range check, as in d3 and
   Vega-Lite) and cannot produce a drawing of unbounded cost, because control points beyond the painter's bound become lines.
6. **A point with a coordinate that is not finite, or beyond `ScenePainter.MaxMagnitude`**, is undefined, and no such
   coordinate is written (a cubic with such a control point becomes a line).
7. **`TryParse`** takes a nullable name; null is not a name.

#### Key Methods

- `CurveBuilder.TryParse(string?, bool, out CurveKind)` - resolves one of the fifteen names to its kind; the second
  argument says whether the mark is a horizontal area, which makes `monotone` the monotone-y curve; returns false (and
  `Linear`) for any other text.
- `CurveBuilder.SupportsArea(CurveKind)` - false for `Bundle`, true for every other kind.
- `CurveBuilder.MaxSegments(CurveKind, long, long, bool)` - the upper bound of the segments of a line or an area, as
  described under Limits.
- `CurveBuilder.Line(CurveKind, double?, ReadOnlySpan<CurvePoint>, List<ScenePathSegment>, CancellationToken)` - creates
  the curve (with the tension where it applies), walks the points with the defined predicate, and appends the segments of
  the line to the output list.
- `CurveBuilder.Area(CurveKind, double?, bool, ReadOnlySpan<AreaPoint>, List<ScenePathSegment>, CancellationToken)` -
  the same for an area: per run of defined points the top line forward and the baseline backward, horizontal or
  vertical.

**Preconditions and postconditions**: the output list is non-null (the segments are appended, the list is not
cleared); `Area` is not called with `Bundle`; on return the list holds the segments of the path with finite coordinates
only, or an exception was thrown (the list may then hold the segments written so far).

#### Error Handling

The unit has few failure modes, because a point that cannot be drawn is a gap and not an error: a point that is not
defined or whose coordinates are not drawable (not finite, or beyond the painter's bound) ends the current run; a
coordinate that cannot be drawn inside a curve is
skipped or turned into a line, as described above; no warning is produced. `Area` with `Bundle` throws
`NotSupportedException`, a programming error that `MarkBuilder` avoids by asking `SupportsArea` first. A canceled token
is an `OperationCanceledException`, never wrapped. A null output list is an `ArgumentNullException`. The unit raises no
`VegaLiteRenderException`: it does not know the budgets, and it is the caller that charges the scene items and the
evaluation steps with `MaxSegments` before it calls a generator (see _MarkBuilder_). The unit performs no I/O.

#### Dependencies

- **`ScenePathSegment`** - the segment value (`Move`, `Line`, `Cubic`, and `Close`, absolute coordinates) of the
  `Scenegraph` subsystem (see _SceneModel_)
- **.NET base class library** - `System.Collections.Generic` (`List<T>`), `System.Threading` (`CancellationToken`), and
  `System.Math`

#### Callers

`MarkBuilder` (`MarkBuilder.Paths.cs`) is the only caller: it calls `TryParse` and `SupportsArea` to choose the curve of
a line or an area (a name that Vega-Lite does not know, which can come from the configuration, and `bundle` for an area
give `Linear`), `MaxSegments` to charge the scene budget and the evaluation steps before it creates the segment list,
and `Line` or `Area` to generate the path of each group of rows. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Marks/` (`CurveBuilderTests.cs`, `CurveBuilderSplineTests.cs`, and
`CurveBuilderAreaTests.cs`) call the unit directly through `InternalsVisibleTo`.
