### ScenePainter

![Scenegraph Structure](VegaLiteScenegraphView.svg)

The `ScenePainter` unit is the second software unit in the `Scenegraph` subsystem. It comprises two source files in the
`DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace: `ScenePainter.cs` and `ScenePainter.Paths.cs`.
`ScenePainter` is a `partial` class, so the two files are one type; together they define two `internal` types:
`ScenePainter` (sealed class) and `ScenePlacement` (readonly record struct, in `ScenePainter.cs`). `ScenePainter.cs` has
the placement, the painting of the groups, rectangles, rules, polylines, symbols, and text, and the fills and strokes;
`ScenePainter.Paths.cs` has the painting of paths made of segments, arcs, and trails, and the miter limit. Both files
carry the provenance header comment for the Vega rendering behavior they port (the second one also names d3-shape,
d3-path, and Vega-Lite).

#### Purpose

`ScenePainter` draws a `Scene` onto a core `Surface` using only the public CanvasNet drawing API. The core library has
fills, strokes, dashes, and text but no scale and no clip, so the painter supplies both. It exists so that all pixel output
of the system comes from one deterministic, tested place, and so that the rest of the system never touches a pixel.

The unit's behavior is specified by the `CanvasNetVegaLite-Scenegraph-ScenePainter-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scenegraph/scene-painter.yaml`.

#### Data Model

**`ScenePlacement`** `(Scale, OffsetX, OffsetY)` says where the scene goes: `Uniform(scale)` places it at the
origin enlarged by a factor, and `Fit(sceneWidth, sceneHeight, targetWidth, targetHeight)` scales it uniformly by the
largest factor that fits and centers it (a scene without area is not scaled).

**`ScenePainter`** (stateless, thread-safe) holds the `TextMeasurer` that resolves fonts and produces the lines
to draw. A private context carries the target surface, the current transform (a `Matrix3x2` holding the uniform scale and
translations), and the scale.

**Painting rules.**

- The surface is filled with the scene's background (transparent when none or unparseable), then the items are drawn in
  list order, parents first. Fills and strokes are anti-aliased by the core path filler; opacity is folded into the
  alpha of the color; a paint that is `none`, invalid, or fully transparent draws nothing; an item of opacity zero or a
  non-finite coordinate is skipped.
- Every path is transformed by the placement and the enclosing groups' translations before it is filled. The stroke of an
  item is built in the item's own units with the core `PathStroker` (width, dash, cap, join, and miter limit) and then
  filled, so widths and dashes scale with the chart. Fills are flattened to 0.02 pixel so that symbols a few
  pixels wide stay round.
- The stroke values are validated after their conversion to the `float` of the stroker, because a value that is
  finite as a `double` can overflow to infinity or underflow to zero: a stroke width that does not stay finite and
  positive draws nothing, a dash offset that overflows counts as zero, and a dash pattern with an entry that overflows,
  or whose entries all underflow to zero, is a solid stroke. The work of a dashed stroke is bounded: the length of the
  path is estimated from its commands (chords, control polygons, and for an arc the chord plus half the circumference of
  its radius) and a dash pattern is drawn solid when its period is below 0.25 surface pixels (`MinDashPeriod`, finer
  than the eye can tell from a solid line) or when the path would have more than 50,000 dashes (`MaxDashSegments`). The
  caps are generous for charts (a 1,000-pixel gridline with a 2-pixel period has 500 dashes), so ordinary dashes
  are unchanged.
- **Magnitude bound.** One constant, `ScenePainter.MaxMagnitude` (32,768 surface pixels, four times
  `Surface.MaxDimension`), and one helper, `ScenePainter.IsBoundedAt(scale, values)` (`IsBounded(values)` is the case
  of a scale of one, used by the scene builders), decide whether a value can be drawn: it must be finite and its
  size in surface pixels (the value times the scale of the placement) must be at most the bound. The painter applies
  it to **everything that has a position or a length**: the end points of the segments of a path (a cubic with a
  finite control point beyond the bound is drawn as a line to its end point instead), the center, radii, and corner
  radius of an arc, the coordinates and sizes of the points of a trail, the position, offsets, font size, and line
  height of text (the angle is bounded in degrees and the limit is clamped to the bound), the offset of a group, and
  the stroke width of every item. An item that needs a larger value is not drawn, like an item with a value that is
  not finite. *Why*: a value that overflows a single (a `double` of 1e300 converted to `float` is infinity) makes the
  flattening of a curve in the core filler recurse to its maximum depth, which costs seconds for one curve, and
  beyond a few tens of thousands of pixels the spacing of a single is no longer below the flattening tolerance of
  0.02 either, so a circle or a capsule is flattened to the maximum depth as well (measured: about 1.8 seconds for
  an annular arc and about 60 milliseconds for a trail capsule at radii of 130,000 pixels and more, against a
  millisecond or less for radii of a few thousand pixels). The cost of every item is therefore bounded. The largest
  surface is 8,192 pixels wide, so what lies beyond four times that is invisible on every chart a renderer can make.
- **Trail chunks.** The core filler cannot be interrupted, so a trail is painted in chunks: every capsule has an
  estimated cost (`TrailCapsuleWeight`, `1 + sqrt(radius * scale / 0.02)`, the number of line pieces of its round
  caps), and when the capsules written so far weigh `TrailChunkWeight` (100,000) they are filled and stroked, the
  cancellation token is polled before that, and a new path starts. A trail of ordinary size (a weight of about 11 per
  capsule) is one chunk up to about 9,000 capsules, so ordinary charts are painted exactly as before; the largest
  capsule the bound allows (a size of 32,768, a weight of about 906) gives chunks of 111 capsules. The capsules of
  two chunks overlap at the shared point, so the only difference from one fill is that the anti-aliased rim of the
  round caps at that joint is composited twice.
- A group paints its own background and border as a (rounded) rectangle behind its children; a stroked group is offset as
  Vega does (half a pixel for a one-pixel stroke, so that the line covers exactly one pixel column and row). The
  background can be a gradient.
- **Gradient fills.** An item with a `SceneGradient` is filled with the core `LinearGradient` (pad spread) through
  `PathFiller.Fill`, instead of its solid fill. The start and end of the gradient are fractions of the bounding box of
  the item's path (`Path.GetBounds`, in the item's own units, without the stroke), mapped to surface pixels with the same
  matrix as the path, so the gradient follows the scale, the placement, and the translations of the enclosing groups.
  The fill opacity (`FillOpacity` times `Opacity`) scales the alpha of every stop. A stop whose offset is not a number or
  whose color cannot be parsed is dropped and an offset outside 0 to 1 is clamped. A gradient with no usable stop, or with
  geometry that is not finite, is ignored and the solid fill is drawn; a gradient that is fully transparent draws nothing
  and does not fall back to the solid fill. Strokes and text are never filled with a gradient. The core evaluates the
  gradient per pixel, so the cost is that of a solid fill of the area plus a small constant per pixel; the stops are
  bounded by `SceneGradient.MaxStops`.
  *Deviation from Vega*: Vega computes the gradient over the bounds of the item including its stroke (the legend
  gradient has a stroke width of 0 by default, so the two agree there). *Sampling*: the core evaluates the ramp at the
  pixel centers, so a gradient across a 100-pixel box changes by about 2.55 levels of an 8-bit channel per pixel.
- **Clipping**: a group with a clip draws its children onto a transparent layer the size of the clip's pixel box and
  composites the layer back with the per-pixel coverage of the clip rectangle (exact on integer edges, anti-aliased on
  fractional edges, nested clips intersecting); the corner radius of a clip is not applied. Memory is the clip's pixel box,
  not the whole surface.
- **Symbols** use Vega's geometry with `size` as the area of the bounding box (circle, square, cross, diamond, arrow, wedge,
  the triangles, and stroke; an unknown shape is a circle), rotated about the center.
- **Paths of segments.** A `ScenePath` that has `Segments` is drawn from its commands, in item units
  (`ScenePainter.Paths.cs`): a line or cubic command **before the first move is skipped**; a command **after a close
  continues at the start of the closed subpath** (the painter reopens it with a move, as d3-path does); a move starts a
  new subpath, so one item can hold several. A command with a coordinate that is **not finite makes the whole item draw
  nothing** (checked with `ScenePathSegment.IsFinite` before anything is built, as a points path does), and so does an
  end point beyond the magnitude bound and an opacity of zero. The fill is nonzero and the stroke is that of the item,
  so a gradient fill, a dash, a cap, and a join
  work as for any path. A `ScenePath` without segments is drawn from its points as before (nothing for fewer than two
  points).
- **Arcs.** A `SceneArc` is the outline that `ArcGeometry.Build` returns for its radii, angles, pad angle, and corner
  radius, **translated to the center** of the item (a translation to the center composed with the transform of the
  enclosing groups), then filled and stroked like any path. An arc with a member that is not finite, or an opacity of
  zero, draws nothing; a center, radius, or corner radius beyond the magnitude bound draws nothing either (the
  geometry unit alone accepts up to one billion, which would cost seconds).
- **Trails.** A `SceneTrail` is drawn as Vega's trail shape does it, described here in the painter's own words. Within
  a run, every pair of consecutive points becomes a **capsule**: the unit normal of the segment is scaled by half the
  size at each point (the radius), the capsule starts at the first point moved by its radius against the normal, runs
  straight to the second point moved by its radius the same way, goes around the second point with a half circle on the
  forward side to the opposite side, runs straight back to the first point moved by its radius along the normal, goes
  around the first point with a half circle on the backward side, and closes. The width is therefore the size at each
  point, tapering linearly between two points of different size, with round ends. The capsules of **all runs are one
  path**, filled with the nonzero rule (so overlapping capsules are one shape and have no seams) and stroked as one path
  with the stroke of the item. A point whose coordinates or size are not finite, whose coordinates or size are beyond the
  magnitude bound, or whose size is not positive **ends its run** (the next usable point starts the run again, so a run
  with a gap draws two pieces), and a run needs two usable points to draw anything (a run of one point draws nothing).
  *Deviation from Vega* (the coincident points): two coincident points (the normal has no length) draw a circle of the
  radius at the second point; Vega starts that circle with a line from the previous capsule, and this painter starts it
  with a move, which has the same fill and no stray line in the stroke. The painter polls the cancellation token **every
  1,024 points** across all runs and before every chunk (see Trail chunks), and its cost is proportional to the total
  number of points.
- **Miter limit.** The stroke takes the miter limit of its item through `ScenePainter.ResolveMiterLimit`: **4 when the item
  has none** (`StrokeMiterLimit` is null), **1 when the value is not finite or is below 1**, **1,000 when it is above
  1,000**, and the value itself otherwise (as a `float`).
- **Text** is filled only (a stroke on text is ignored), at the scaled font size, with the font that the measurer resolves,
  each line drawn at its baseline using Vega's baseline offsets and alignment, rotated about the text position; the lines
  are those the measurer produced (trimmed and truncated to the limit). A text whose position, offsets, font size, or
  line height is beyond the magnitude bound, or whose font size would not be a finite positive single after scaling, draws
  nothing (a font size of 1e300 once made the core text renderer throw an `ArgumentOutOfRangeException`).

#### Key Methods

- `Paint(Scene, int width, int height, ScenePlacement, CancellationToken)` - creates a surface of the
  given size, paints the scene, and returns it; the surface is disposed if painting fails; the caller owns the result.
- `Paint(Scene scene, Surface surface, ScenePlacement placement, CancellationToken token)` - requires non-null arguments
  and a finite positive scale, clears it with the background, builds the transform, and paints the root group; the
  token is polled per group and per item.
- `SymbolPath(string shape, double size)` (internal static) - the path of a symbol shape at the origin; empty for a
  size that is not positive and finite.
- `ResolveMiterLimit(double?)` (internal static) - the miter limit that the stroker receives: 4 for null, 1 for a value
  that is not finite or is below 1, at most 1,000 otherwise.
- `IsBoundedAt(float scale, params double[] values)` and `IsBounded(...)` (internal static) - the magnitude bound described
  above, shared with `CurveBuilder` and `MarkBuilder` so that the scene never holds what the painter would skip.
- `TrailCapsuleWeight(double, float)` (internal static) and the `TrailChunkFlushed` observer (internal, null in production,
  told the capsule count of every chunk before its poll) - the estimate that chunks a trail, and the hook that lets a test
  count the chunks and cancel at a boundary without measuring time.
- `ScenePlacement.Uniform` and `ScenePlacement.Fit` - the two placements `VegaLiteDocument` uses.

**Pre- and postconditions**: the scene is non-null; on return the surface holds the drawing, deterministic for the
same scene, size, and placement.

#### Error Handling

A null scene or surface is `ArgumentNullException` and a scale that is not finite and positive is
`ArgumentOutOfRangeException`; these are programming errors and are not converted. A width or height outside the surface
limits is rejected by the `Surface` constructor (the document validates sizes first). A canceled token is an
`OperationCanceledException`, and the surface that an overload created is disposed; the token is also polled every 1,024
points inside a trail and before every chunk of its capsules. Bad content never throws: an unparseable color draws
nothing, a non-finite coordinate or a zero-sized rectangle or clip is skipped, a segment path, arc, trail, or text
member that is not finite or is beyond the magnitude bound draws nothing (a trail
point that is not usable only ends its run), and an unknown shape is a circle.

#### Dependencies

- **`Scene`** and the item types - the `SceneModel` unit
- **`CssPaint`** - the paint rules (no paint, opacity), in the `CssPaint` unit
- **Core `CssColor`** - the parsing of every color string (`DemaConsulting.CanvasNet.Canvas`, public API)
- **`ArcGeometry`** and **`ArcPathWriter`** - the outline of a `SceneArc` and the path calls for the capsules of a trail,
  in the `ArcGeometry` unit
- **`TextMeasurer`** - font resolution and text lines, in the `Guides` subsystem
- **Core `CanvasNet`** - `Surface`, `Rgba32`, and `CompositeOverSpan` (`Canvas`), `PathFiller`, `PathStroker`, and
  `LinearGradient` with `GradientStop` (`Drawing`), `Path`, `PathBuilder`, and `Transform` (`Geometry`), and
  `TextRenderer` (`Rendering`), through their public API
- **.NET base class library** - `System.Numerics` (`Matrix3x2`, `Vector2`), `System.Threading`

#### Callers

`VegaLiteDocument` paints the scene of every `Render` call (with `ScenePlacement.Uniform` or `Fit`). The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/ScenePainterTests.cs` and `ScenePainterPathTests.cs` (the segment
paths, arcs, trails, and the miter limit) call the painter directly through `InternalsVisibleTo` and check analytic pixels.
