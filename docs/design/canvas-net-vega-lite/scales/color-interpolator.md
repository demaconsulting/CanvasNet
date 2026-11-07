### ColorInterpolator

![Scales Structure](VegaLiteScalesView.svg)

The `ColorInterpolator` unit is the sixth unit in the `Scales` subsystem. It is one source file,
`ColorInterpolator.cs`, in the `DemaConsulting.CanvasNet.VegaLite.Scales` namespace, and defines the `internal` types
`InterpolationSpace` (an enumeration), `RgbColor` (a record struct), the static class `ColorInterpolator`, and the
sealed class `ColorRamp`. The file carries the provenance header comment `Portions derived from d3-interpolate and
d3-color (ISC, Mike Bostock) and Vega (BSD-3-Clause)`.

#### Purpose

`ColorInterpolator` interpolates colors the way d3 and Vega do. A continuous color scale maps a number to a color by
interpolating through the stops of a scheme; a discrete range takes samples of such an interpolator; and the gradient
of a legend is made of colors it computes. The unit keeps that arithmetic in one place so that every user gets the same
8-bit colors on every machine. It is an own C# implementation of public algorithms of d3-interpolate and d3-color; the
third-party notice names what was studied.

The unit's behavior is specified by the `CanvasNetVegaLite-Scales-ColorInterpolator-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scales/color-interpolator.yaml`.

#### Data Model

`RgbColor` is an immutable record struct of the sRGB channels (0 to 255, as doubles, so that interpolation keeps
fractions) and the opacity (0 to 1); `FromBytes`, `FromRgba32`, and `TryParseHex` (six hexadecimal digits with an
optional `#`) create one, and `ToRgba32` and `ToCss` convert it back (each channel is rounded half away from zero after
clamping to 0 to 255, NaN is 0, and the CSS text is `#rrggbb`, or `#rrggbbaa` when the opacity is not 1).
`InterpolationSpace` names the spaces: `Rgb`, `Hsl`, `HslLong`, `Lab`, `Hcl`, `HclLong`, `Cubehelix`, and
`CubehelixLong` (`TryParseSpace` maps the Vega-Lite `scale.interpolate` names `rgb`, `hsl`, `hsl-long`, `lab`, `hcl`,
`hcl-long`, `cubehelix`, and `cubehelix-long`, which is every name of Vega-Lite; a name outside the list is not
recognized). A `ColorRamp` is an immutable, thread-safe piecewise
interpolator: it converts its stops to the channels of the space once and, for a position, picks the segment and
interpolates inside it.

The conversions are those of d3-color: sRGB to HSL (hue in degrees, NaN for a grey, saturation NaN for black and
white), sRGB to CIELAB with the D50 white point (`Xn = 0.96422`, `Yn = 1`, `Zn = 0.82521`, the d3 matrices), and CIELAB
to its polar form HCL (hue NaN when `a` and `b` are 0). The channel rules are those of d3-interpolate: a hue takes the
shorter way round the circle (the long variants and CIELAB interpolate the number linearly), a hue or channel that is
not a number takes the other color's value, the opacity is linear in every space, and the gamma applies to the sRGB
channels (`pow(a, gamma)`, linear, `pow(x, 1 / gamma)`) and, as `t^gamma`, to the lightness of the two cubehelix spaces
(`Interpolate` ignores it for the other spaces; `HasGamma` tells which have one).

**Cubehelix** (Phase 13; d3-color `cubehelix` and d3-interpolate `cubehelix` and `cubehelixLong`, as Vega's
`interpolate` of `vega-scale` maps `cubehelix` and `cubehelix-long`). The conversion from sRGB with `r`, `g`, `b` in 0
to 1 uses the constants `A = -0.14861`, `B = 1.78277`, `C = -0.29227`, `D = -0.90649`, and `E = 1.97294` of d3-color
(and the products `E D`, `E B`, and `B C - D A`): the lightness is
`l = ((B C - D A) b + E D r - E B g) / (B C - D A + E D - E B)`, with `bl = b - l` and
`k = (E (g - l) - C bl) / D`, the saturation is `s = sqrt(k^2 + bl^2) / (E l (1 - l))` (NaN for black and white) and the
hue is `atan2(k, bl)` in degrees minus 120, moved into 0 to 360 and NaN for a grey (`s` is 0 or NaN). The way back
(`Cubehelix.rgb` of d3-color) takes `h = (hue + 120)` in radians (a NaN hue or saturation counting as 0), `a = s l (1 -
l)`, and gives each channel `255 (l + a (c1 cos h + c2 sin h))` with the pairs `(A, B)`, `(C, D)`, and `(E, 0)`. The
interpolation takes the hue the short way (the long way for `cubehelix-long`), a NaN hue or saturation from the other
color (so a grey takes the hue of the other color), the saturation and the opacity linearly, and the lightness at
`t^gamma` (the gamma of d3's `cubehelix.gamma`, 1 by default, a gamma that is not a number counting as 1); the colors
are clamped and rounded after the conversion like those of the other spaces. The oracle values of d3 (red to blue with
gamma 1: `#ee00d1` at the midpoint, gamma 2: `#ff00f6`) are asserted by the tests.

#### Key Methods

- `Interpolate(space, start, end, t, gamma)` - one segment between two colors; positions outside 0 to 1 extrapolate
  and the channels are clamped after the interpolation.
- `ColorRamp.At(t)` and `CssAt(t)` (and `Piecewise`) - the d3 `piecewise` rule: with `n` segments the segment index is
  `floor(t * n)` clamped to 0 to `n - 1` and the position inside it is `t * n - index`, so a position below 0 or above 1
  extrapolates along the first or last segment; NaN counts as 0 and the infinities as the ends; one color is a
  constant ramp; no color is an `ArgumentException`.
- `Range(interpolator, start, end)` - Vega's `interpolateRange`: `t => interpolator(start + t * (end - start))`, so a
  sub-range and a reversal are the same operation.
- `Quantize(interpolator, count, cancellationToken)` - Vega's `quantizeInterpolator`: the `count` colors at
  `i / (count + 1)` for `i = 1 to count` (the end points are not used); the token is checked every 4,096 colors and a
  negative count is `ArgumentOutOfRangeException`.

**Preconditions and postconditions**: a ramp has at least one color; every interpolation result has whole channels in 0
to 255 and an opacity in 0 to 1; results are deterministic.

#### Error Handling

A null list or interpolator is `ArgumentNullException`, an empty list is `ArgumentException`, a negative count is
`ArgumentOutOfRangeException`, and a cancelled token is `OperationCanceledException`; these are programming or caller
errors and are not converted. Colors that cannot be interpolated (NaN channels) give 0 for the channel; the unit never
throws for the values of an interpolation. **Limits**: the unit allocates only the list that `Quantize` returns, whose
size the caller bounds (the `ColorSchemes` unit checks the count against the scale domain limit before it calls). The
unit performs no I/O.

**Determinism.** The sRGB space uses only `+ - * /` and the rounding, so its results are bit-exact on every platform.
HSL, CIELAB, HCL, and cubehelix use `Math.Pow`, `Math.Atan2`, `Math.Sin`, and `Math.Cos`, whose last bit can differ between
platforms, so a color can differ by one in a channel between machines; no scheme table, no default space, and no test
that pins exact bytes depends on that (the tests allow a tolerance of one step for those spaces).

#### Dependencies

The core `Rgba32` type (`DemaConsulting.CanvasNet.Canvas`) and the .NET base class library (`System`,
`System.Globalization`, `System.Threading`); no other unit.

#### Callers

`ColorSchemes` builds its ramps and discrete ranges with the unit; the `ScaleResolver` and `ScaleModel` (later phases)
map values to colors through `ColorRamp`; `LegendBuilder` samples it for the stops of a gradient. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ColorInterpolatorTests.cs` call the unit directly through
`InternalsVisibleTo`.
