### ColorInterpolator Unit Verification Design

This document describes the unit-level verification strategy for the `ColorInterpolator` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace: the internal static `ColorInterpolator` class (`Interpolate`,
`Piecewise`, `Range`, `Quantize`, and `TryParseSpace`), the sealed `ColorRamp` class, the `RgbColor` record struct, and
the `InterpolationSpace` enumeration. It is an own C# implementation of public algorithms of d3-interpolate and
d3-color; the third-party notice of the system names the upstream behavior that was studied.

#### Verification Approach

The unit is verified with values derived by hand from the formulas, never by running d3 or Vega and pasting the
results. In sRGB the arithmetic is exact: black to white at 0.5 is 127.5 and rounds half away from zero to 128, at 0.25
it is 63.75 (64), and at 0.75 it is 191.25 (191); the channels 10, 20, and 30 moving a quarter of the way towards 20,
40, and 60 are 12.5 (13), 25, and 37.5 (38); a position of 2 or -1 extrapolates and is clamped to white or black; the
opacity from 0 to 1 at 0.5 is 0.5, which as an alpha byte is 127.5 and rounds to 128. A gamma of 2 from black to white at
0.5 is the square root of 0.5 x 255^2 = 32,512.5, that is 180.31, which is 180.

For HSL the expected colors follow from the conversion formulas: red (hue 0) to blue (hue 240) differs by 240 degrees,
which is more than 180, so the shorter way goes the other direction (the difference becomes -120) and the middle hue
is -60, which is 300, so the color is magenta (255, 0, 255); the long way takes the middle hue 120, which is green
(0, 255, 0). A grey has no hue, so grey (128, 128, 128) to red at 0.5 has hue 0, saturation 0.5, and lightness
(128 / 255 + 0.5) / 2 = 0.50098, which gives the two bounds 0.75049 and 0.25147 and the color (191, 64, 64). For CIELAB,
black to white at the middle has lightness 50, which is a linear light value of 0.18419 and an sRGB channel of 118.9,
so the grey is 119 (the tests allow one step because the transcendental functions can differ in the last bit); greys
have no hue and no chroma, so HCL gives the CIELAB result; and the end colors come back within one step in every space.
The piecewise vectors use three stops (black, red, yellow) at 0, 0.5, and 1: the quarter points are 127.5 rounded to
128 (`#800000` and `#ff8000`), the ends are exact, positions outside 0 to 1 continue along the end segments and are
clamped, NaN counts as 0, and one color is a constant. Quantizing a black-to-white ramp at three colors samples 0.25,
0.5, and 0.75 (63.75, 127.5, and 191.25 give `#404040`, `#808080`, and `#bfbfbf`).

The unit has no dependency that needs mocking, and the internal types are reached through the test assembly's
`InternalsVisibleTo` access. Unit tests reside in `ColorInterpolatorTests.cs` within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Scales` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of colors and numbers; no files, clock, or shared state; the exact bytes of the HSL,
  CIELAB, and HCL spaces are never pinned across platforms (a tolerance of one 8-bit step is used there)

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any color, text, or exception type that differs from the expectation constitutes a failure.

#### Unit-Level Test Scenarios

##### sRGB Rounding, Clamping, And Opacity

**Tests**: `ColorInterpolatorTests.ColorInterpolator_Rgb_RoundsHalfAwayAndClamps`

Asserts the rounded values, the exact ends, the clamped extrapolation, and the opacity and alpha text derived above.

##### HSL, CIELAB, And HCL

**Tests**: `ColorInterpolatorTests.ColorInterpolator_Hsl_Lab_Hcl_Match_HandDerivedVectors`

Asserts the magenta and green hue paths, the grey-to-red color, the CIELAB grey, the equality of HCL and CIELAB for greys,
the ends of every space, a color interpolated into itself, and that the shorter and the long hue ways differ.

##### Gamma

**Tests**: `ColorInterpolatorTests.ColorInterpolator_Gamma_AppliesToRgbOnly`

Asserts the gamma result 180, that a gamma of 1 changes nothing, that HSL ignores the gamma, and that a ramp applies it.

##### Piecewise Segments And Extrapolation

**Tests**: `ColorInterpolatorTests.ColorInterpolator_Piecewise_ChoosesTheSegmentAndExtrapolates`

Asserts the stops, the segment middles, the extrapolation, NaN and infinite positions, the single-color ramp, and the
rejection of an empty or null list.

##### Range And Quantize

**Tests**: `ColorInterpolatorTests.ColorInterpolator_RangeAndQuantize_SampleWithoutTheEndPoints`

Asserts the three quantized colors, the empty list for a count of 0, the sub-range and the reversed range, the argument
exceptions, and that a cancelled token stops the sampling.

##### Conversions

**Tests**: `ColorInterpolatorTests.ColorInterpolator_RgbColor_ConvertsAndParses`

Asserts hexadecimal parsing (with and without the number sign, either case; three digits, bad digits, seven digits, and
the empty text are rejected), the CSS text and pixel conversions (an alpha byte only when not opaque), the clamping of
out-of-range and NaN channels, and the names of the interpolation spaces (`cubehelix` and null are not recognized).

##### Determinism

**Tests**: `ColorInterpolatorTests.ColorInterpolator_Interpolation_IsDeterministic`

Asserts that a ramp in every space gives the same 200 colors when evaluated again and from several threads at once.
