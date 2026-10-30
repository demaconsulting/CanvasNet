package CanvasNet {
    part def ColorInterpolator {
        doc /* The internal static ColorInterpolator (with RgbColor, InterpolationSpace, and ColorRamp), own C# ports with attribution of the public color interpolation algorithms of d3-interpolate and d3-color: Interpolate and the piecewise ColorRamp interpolate colors in sRGB (with a gamma, which only sRGB has), HSL (shorter way round the hue circle), HSL long, CIELAB (D50 white point), HCL, and HCL long, with d3's rules for greys (no hue), the linear opacity, and the segment choice floor(t times n) clamped to the first and last segment so that a position outside 0 to 1 extrapolates; Range restricts an interpolator to a sub-range in either direction (Vega interpolateRange); Quantize samples an interpolator at i over count plus one without the end points under a cancellation check (Vega quantizeInterpolator); RgbColor converts to a core pixel and to lower-case hexadecimal CSS text and TryParseSpace maps the interpolate names of a scale. Every result has whole 8-bit channels (rounded half away from zero after clamping); the sRGB space is bit-exact and the HSL, CIELAB, and HCL spaces can differ by one in a channel between platforms. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Scales/ColorInterpolator.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ColorInterpolatorTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/scales/color-interpolator.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/scales/color-interpolator.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/scales/color-interpolator.yaml */
    }
}
