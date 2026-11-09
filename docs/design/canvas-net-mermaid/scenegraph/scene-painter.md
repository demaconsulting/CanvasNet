### ScenePainter

![Scenegraph Structure](MermaidScenegraphView.svg)

The `ScenePainter` unit is the second software unit in the `Scenegraph` subsystem. It consists of three source files
in the `DemaConsulting.CanvasNet.Mermaid.Scenegraph` namespace that make up the internal sealed partial class
`ScenePainter`: `ScenePainter.cs` (construction, the font resolver, the item walk, paths, dashes, the bounds checks,
and the painted-pixel charge; it also defines the record struct `ScenePlacement` and the interface
`ISceneImageLoader`), `ScenePainter.Text.cs` (text lines), and `ScenePainter.Images.cs` (pictures). The SysML2 model
names the unit `MermaidScenePainter`.

#### Purpose

`ScenePainter` draws a `Scene` of the `SceneModel` unit onto a core `Surface`; it is the only part of the package that
produces pixels. Its single responsibility is to turn the resolved geometry, paint, fonts, and pictures of a scene
into the pixels that Mermaid's SVG shows, at a placement, deterministically, and within the painted-pixel limit.

The unit's behavior is specified by the `CanvasNetMermaid-Scenegraph-ScenePainter-*` requirements in
`docs/reqstream/canvas-net-mermaid/scenegraph/scene-painter.yaml`.

#### Data Model

- **`ScenePlacement(Scale, OffsetX, OffsetY)`** - where a scene goes on the surface: device = scene x `Scale` +
  offset; the scale is positive.
- **`ISceneImageLoader`** - the contract of a picture loader: `Load(href, pixelWidth, pixelHeight)` returns a surface
  that the painter disposes, or null when the picture cannot be loaded.
- **`ScenePainter`** fields: `_fonts` (the `FontResolver` of the call), `_images` (the picture loader, or null),
  `_maxPaintedPixels` (the limit, at least 1), and `_painted` (the device pixels charged so far in this painting).
- Constants: `MaxMagnitude` = 32,768 device pixels (four times `Surface.MaxDimension`), the largest magnitude of a
  coordinate, size, or font size that is drawn; a flattening tolerance of 0.1 pixel; dash patterns with a period
  under 0.25 pixel, an entry over `MaxMagnitude` device pixels, or more than 50,000 estimated dashes are drawn solid,
  and a dash offset over `MaxMagnitude` is reduced by whole periods, so every value handed to the core
  `StrokeStyle` is finite in single precision. A text run whose device size is zero in single precision is not
  drawn.

**Invariants**: `_painted` never exceeds `_maxPaintedPixels`; an instance paints one scene on one thread.

#### Key Methods

- **`CreateFontResolver()`** - a new `FontResolver` with `MapCommonAliasesToBundled` on and `UseSystemFonts` off, so
  Mermaid's default family stack (`"trebuchet ms", verdana, arial`) resolves to the bundled Liberation families and
  text never depends on the fonts installed on the machine. The system uses one resolver per call for both measuring
  and painting.
- **`Paint(scene, width, height, placement, background, cancellationToken)`** - creates a surface of the given size,
  fills it with the background (a new surface is transparent), and draws the items in list order; a group translates
  its children by its offset times the scale and is skipped when the resulting offset is not bounded. The caller owns
  the returned surface; on any failure the surface is disposed and the exception propagates.
- **`PaintPath`** - skips an empty path or one with a coordinate, control point, or arc radius that is not finite or
  beyond `MaxMagnitude` once placed; converts it with `Scene.ToPath`; fills it (a fill with alpha) with the core
  `PathFiller` under its fill rule, after charging the path's device box; then strokes it (a stroke with alpha and a
  positive finite width) by building the outline in device space with `PathStroker` (scaled width, cap, join, miter
  limit 4, scaled dashes and offset) and filling the outline nonzero, after charging the box grown by half the width.
- **`DashFor`** - the device dash pattern, or null (solid) when there is no pattern, the offset is not finite, an
  entry is negative or not finite, the scaled period is under 0.25 pixel, or the length of the path (estimated from
  its control polygon, an upper bound) divided by the period exceeds 50,000.
- **`PaintText`** (`ScenePainter.Text.cs`) - resolves each run's face with the resolver (family, bold, italic) and
  measures it at the scaled size with `TextRenderer.MeasureText`, skipping empty runs and runs whose size is not
  bounded; places the line at its anchor (start, centered on it, or ending at it) on the scaled baseline; charges the
  line box (from the largest ascent to the largest descent) once; then fills each run with a color that has alpha
  with `TextRenderer.CreateTextPath`, one after the other. Characters missing from a face come from the core's
  bundled fallback fonts.
- **`PaintImage`** (`ScenePainter.Images.cs`) - draws nothing without a loader or with an opacity that is not
  positive; maps the box to device space and snaps it outward to whole pixels (left and top down, right and bottom
  up); skips a box that is not bounded, has no area, or exceeds `Surface.MaxDimension`; charges the box; asks the
  loader for a picture of exactly that size; stretches a picture that comes back at another size to the box with
  `Surface.Resize` (Mermaid's `image` element has `preserveAspectRatio="none"`); and composites it over the surface,
  clipped to it, with the item's opacity (at most 1) as coverage.
- **`Charge`** - clips a device box to the surface, and when the box has area adds it to `_painted`, throwing when the
  total would pass the limit.
- **`ScenePlacement.Uniform(viewBox, scale)`** - the view box at the surface origin, enlarged by the scale.
- **`ScenePlacement.Fit(viewBox, width, height)`** - the largest uniform scale at which the view box fits the target,
  centered; a view box without area is placed at the origin unscaled.

**Preconditions and postconditions**: `Paint` expects a width and height from 1 to `Surface.MaxDimension` (the core
`Surface` constructor rejects others). The same scene, size, placement, and background always give the same pixels.

#### Error Handling

- The constructor throws `ArgumentNullException` for a null resolver and `ArgumentOutOfRangeException` for a limit
  under 1; `Paint` throws `ArgumentNullException` for a null scene.
- `Paint` checks the cancellation token on entry and before every item and throws `OperationCanceledException`.
- `Charge` throws `MermaidRenderException` with the code `mermaid-limit-exceeded` (`LimitExceeded` of
  `MermaidDiagnosticCodes`), the location of the item, a message that names `MermaidLimits.MaxPaintedPixels`, and a
  hint to render smaller or raise the limit, before any pixel of the item is painted.
- Geometry that is not finite or out of bounds is skipped silently (it cannot be seen on any surface). An invalid
  dash pattern is drawn solid. A loader that returns null draws nothing; the exceptions of a loader propagate (the
  system's `NodeImages` loader reports and limits pictures itself).
- On any exception the surface created by `Paint` is disposed; every picture returned by the loader is disposed.

#### Dependencies

- **SceneModel** - the scene records and `Scene.ToPath` of the same subsystem
- **Core `CanvasNet`** - `Surface` and `Rgba32` (`Canvas`), `PathFiller`, `PathStroker`, `StrokeStyle`, and `FillRule`
  (`Drawing`), `FontResolver`, `FontResolverOptions`, and `TrueTypeFont` (`Fonts`), and `TextRenderer` (`Rendering`),
  through the public API
- **System public API** - `MermaidRenderException`, `MermaidDiagnosticCodes`, and `MermaidLimits` (for the limit's
  name)
- **.NET base class library** - `System.Math`, LINQ, and invariant-culture formatting

#### Callers

The `MermaidDocument` unit creates the font resolver of every call with `CreateFontResolver` (shared with the label
measurement of the `Rendering` subsystem), and paints the laid-out scene with a painter built from that resolver, the
scene's picture loader, and `MermaidLimits.MaxPaintedPixels`, with `ScenePlacement.Uniform` for the natural size or
`ScenePlacement.Fit` for an exact size. The `NodeImages` unit of the `Rendering` subsystem implements
`ISceneImageLoader`. The tests in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Scenegraph/ScenePainterTests.cs` call
the unit directly through `InternalsVisibleTo`.

#### Known Differences From Mermaid

- **Bundled fonts only.** A browser draws Mermaid's text with the installed fonts that the CSS family list names; the
  painter maps the common names to the bundled Liberation families and never uses installed fonts, so the pixels are
  the same on every machine but the glyphs differ from those of a browser where the browser finds another font.
- **Invalid and extreme dash patterns.** A pattern that SVG rejects is drawn solid, as browsers do; in addition, a
  pattern finer than a quarter of a pixel, one with a dash longer than 32,768 device pixels, or one that would make
  more than 50,000 dashes is drawn solid, where a browser would draw the dashes.
- **Geometry far outside the surface.** An item with a coordinate beyond 32,768 device pixels is not drawn, where a
  browser would draw the part that reaches the visible area.
- **Pictures fill their box.** A picture is always stretched to its box, as Mermaid's `preserveAspectRatio="none"`
  does; a raster picture is resampled with the core `Surface.Resize`, whose filtering may differ from a browser's.
