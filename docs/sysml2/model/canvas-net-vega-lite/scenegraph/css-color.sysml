package CanvasNet {
    part def CssColor {
        doc /* The internal static CssColor: it parses CSS colors into core Rgba32 values with the invariant culture and never throws for bad text (TryParse returns false). Accepted forms are hexadecimal rgb, rgba, rrggbb, and rrggbbaa (a short digit is doubled), the 148 CSS named colors (including rebeccapurple and the grey spellings) and transparent in any letter case, rgb and rgba with numbers or percentages and an optional alpha written with commas or with spaces and a slash, and hsl and hsla with a hue in deg, rad, grad, or turn; out-of-range channels are clamped as CSS specifies and the alpha is straight (not premultiplied). A paint is a color or no color: null, empty, white space, and none mean do not draw (IsNone); TryResolvePaint applies that rule and an opacity in one step, WithOpacity scales the alpha, Parse throws a FormatException for callers that want a loud failure, and ToCss formats a color as lower-case hexadecimal CSS text. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Scenegraph/CssColor.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/CssColorTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/scenegraph/css-color.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/scenegraph/css-color.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/scenegraph/css-color.yaml */
    }
}
