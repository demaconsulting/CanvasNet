### CssPaint

![Scenegraph Structure](VegaLiteScenegraphView.svg)

The `CssPaint` unit is the third software unit in the `Scenegraph` subsystem. It is one source file, `CssPaint.cs`, in the
`DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace, and defines one `internal` type, the static class `CssPaint`.

#### Purpose

`CssPaint` holds the paint rules of a chart on top of the CSS color grammar. Charts spell colors the CSS way (the
Vega-Lite defaults are `#4c78a8` and `#ddd`) and scene items carry the colors exactly as the specification and
configuration spell them (so tests can assert the exact string). The grammar itself - hexadecimal forms, the 148 named
colors, `rgb()`/`rgba()`, and `hsl()`/`hsla()` - is the shared `CssColor` unit of the core `CanvasNet` library, which
this unit calls; it keeps no color table and no parser of its own. What it adds is the notion of a **paint**: a color or
no color, where null, an empty or white-space string, and `none` mean "do not draw", and the multiplication of a color's
alpha by an opacity.

The unit's behavior is specified by the `CanvasNetVegaLite-Scenegraph-CssPaint-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scenegraph/css-paint.yaml`.

#### Data Model

The unit has no state. A paint is a string; the resolved color is a core `Rgba32` with straight (not premultiplied)
alpha. Out-of-range channels are clamped by the core parser, as CSS specifies, and fractions are rounded half away from
zero.

#### Key Methods

- `IsNone(string? text)` - true for a paint that means "do not draw".
- `TryResolvePaint(string? paint, double opacity, out Rgba32 color)` - applies the no-paint rule, parses the color with
  `CssColor.TryParse`, and applies the opacity in one step (the opacity multiplies the color's alpha); false for no
  paint, for text that is neither a color nor none, and for a result that is fully transparent.
- `WithOpacity(Rgba32 color, double opacity)` - the color with its alpha scaled by an opacity from 0 to 1 (clamped, NaN
  counts as 1), rounded half away from zero.

**Preconditions and postconditions**: the methods are pure functions of their arguments, culture-invariant and
deterministic.

#### Error Handling

No method throws for bad text: an unparseable paint is "no paint", which is what a canvas does with a color it cannot
parse. The unit performs no I/O.

#### Dependencies

- **Core `CanvasNet`** - `Rgba32` and `CssColor` (the `Canvas` subsystem), through the public API
- **.NET base class library** - `System.Math`

The unit has no dependency on another unit of the system.

#### Callers

`ScenePainter` resolves every paint through `CssPaint` (and parses background and gradient-stop colors with the core
`CssColor` directly); `ScaleResolver` validates and reads color lists with `CssColor`; `VegaLiteDocument` converts the
caller's `Background` with `CssColor.ToCss`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/CssPaintTests.cs` call the paint rules directly through
`InternalsVisibleTo`.
