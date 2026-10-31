### ScenePainter

![Scenegraph Structure](VegaLiteScenegraphView.svg)

The `ScenePainter` unit is the second software unit in the `Scenegraph` subsystem. It is one source file,
`ScenePainter.cs`, in the `DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace, and defines two `internal` types:
`ScenePainter` (sealed class) and `ScenePlacement` (readonly record struct). The file carries the provenance header comment
for the Vega rendering behavior it ports.

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
  item is built in the item's own units with the core `PathStroker` (width, dash, cap, and join) and then
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
- **Text** is filled only (a stroke on text is ignored), at the scaled font size, with the font that the measurer resolves,
  each line drawn at its baseline using Vega's baseline offsets and alignment, rotated about the text position; the lines
  are those the measurer produced (trimmed and truncated to the limit).

#### Key Methods

- `Paint(Scene, int width, int height, ScenePlacement, CancellationToken)` - creates a surface of the
  given size, paints the scene, and returns it; the surface is disposed if painting fails; the caller owns the result.
- `Paint(Scene scene, Surface surface, ScenePlacement placement, CancellationToken token)` - requires non-null arguments
  and a finite positive scale, clears it with the background, builds the transform, and paints the root group; the
  token is polled per group and per item.
- `SymbolPath(string shape, double size)` (internal static) - the path of a symbol shape at the origin; empty for a
  size that is not positive and finite.
- `ScenePlacement.Uniform` and `ScenePlacement.Fit` - the two placements `VegaLiteDocument` uses.

**Pre- and postconditions**: the scene is non-null; on return the surface holds the drawing, deterministic for the
same scene, size, and placement.

#### Error Handling

A null scene or surface is `ArgumentNullException` and a scale that is not finite and positive is
`ArgumentOutOfRangeException`; these are programming errors and are not converted. A width or height outside the surface
limits is rejected by the `Surface` constructor (the document validates sizes first). A canceled token is an
`OperationCanceledException`, and the surface that an overload created is disposed. Bad content never throws: an
unparseable color draws nothing, a non-finite coordinate or a zero-sized rectangle or clip is skipped, and an unknown shape
is a circle.

#### Dependencies

- **`Scene`** and the item types - the `SceneModel` unit
- **`CssColor`** - color parsing, in the `CssColor` unit
- **`TextMeasurer`** - font resolution and text lines, in the `Guides` subsystem
- **Core `CanvasNet`** - `Surface`, `Rgba32`, and `CompositeOverSpan` (`Canvas`), `PathFiller`, `PathStroker`, and
  `LinearGradient` with `GradientStop` (`Drawing`), `Path`, `PathBuilder`, and `Transform` (`Geometry`), and
  `TextRenderer` (`Rendering`), through their public API
- **.NET base class library** - `System.Numerics` (`Matrix3x2`, `Vector2`), `System.Threading`

#### Callers

`VegaLiteDocument` paints the scene of every `Render` call (with `ScenePlacement.Uniform` or `Fit`). The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/ScenePainterTests.cs` call the painter directly through
`InternalsVisibleTo` and check analytic pixels.
