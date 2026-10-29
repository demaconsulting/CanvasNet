package CanvasNet {
    part def ColorSchemes {
        doc /* The internal static ColorSchemes: the category colors of the Vega-Lite default configuration and the lookup of a color scheme by name. The only scheme in this release is tableau10, the ten default category colors of Vega-Lite (a port with attribution of the palette; no ColorBrewer scheme is used, so no ColorBrewer notice applies); TryGetScheme finds it by name ignoring case and reports any other name as unavailable (the ScaleResolver then falls back to tableau10 and records the fallback), CategoryColor returns the color at index modulo the number of colors so that categories beyond the length of the scheme cycle deterministically, and Tableau10Color applies it to tableau10. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Scales/ColorSchemes.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ColorSchemesTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/scales/color-schemes.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/scales/color-schemes.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/scales/color-schemes.yaml */
    }
}
