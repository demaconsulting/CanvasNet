package CanvasNet {
    part def ColorSchemes {
        doc /* The internal static partial ColorSchemes (with ColorScheme, SchemeRange, and SchemeUse): the catalog of the 68 color schemes of Vega - 15 categorical lists (tableau10, the default category palette of Vega-Lite, category10, category20, the ColorBrewer qualitative sets, and others) and 53 continuous ramps (ColorBrewer, matplotlib, cividis, Turbo, d3, and Vega ramps) - held as data in ColorSchemeData.cs and decoded once on first use, with their provenance and license notices recorded in the third-party notice (ColorBrewer and Turbo Apache-2.0, the cividis Battelle license, CC0 matplotlib colormaps, and the d3 and Vega licenses). TryGetColorScheme finds a scheme by name ignoring case, TryGetScheme gives the colors of a categorical scheme for the ordinal color ranges of the ScaleResolver (a continuous scheme or an unknown name makes the resolver fall back to tableau10 and record the fallback), CategoryColor returns the color at index modulo the number of colors so that categories beyond the length of the scheme cycle deterministically, and Configure ports the scheme handling of the Vega scale transform: an interpolator over an optional extent for a continuous range, otherwise a list whose length follows the scale kind (the count or 5 for quantile and quantize, thresholds plus one, bin edges minus one, the count or the domain size or 5 for ordinal), sampled at i over count plus one without the end colors, optionally reversed, under a color limit (a VegaLiteRenderException naming VegaLiteLimits.MaxScaleDomainSize) and a cancellation check. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Scales/ColorSchemes.cs and ColorSchemeData.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ColorSchemesTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/scales/color-schemes.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/scales/color-schemes.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/scales/color-schemes.yaml */
    }
}
