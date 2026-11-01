package CanvasNet {
    part def CssColor {
        doc /* Public static CSS color parser and formatter, next to Rgba32: the one CSS color grammar shared by every CanvasNet package. TryParse and Parse accept hexadecimal rgb, rgba, rrggbb, and rrggbbaa (a short digit is doubled, the alpha pair comes last), the 148 CSS named colors and transparent in any letter case, rgb and rgba with numbers or percentages and an optional alpha written in the comma syntax or in the space syntax with a slash (never mixed), and hsl and hsla with a hue in deg, rad, grad, or turn (a non-finite converted hue is rejected) and percentages for saturation and lightness (numbers too in the space syntax); out-of-range channels are clamped as CSS specifies and fractions are rounded half away from zero; the alpha is straight (not premultiplied). TryParse never throws for bad text; Parse throws ArgumentNullException for null and FormatException for unsupported text. TryParseHex (the digits after the hash) and TryGetNamedColor (an exact, case-insensitive name lookup) are the building blocks for consumers with a narrower grammar, such as the OpenSCAD color module and the command-line tool; ToCss formats a color as lower-case hexadecimal CSS text; NamedColorCount is 148. Used by the SVG codec, the Vega-Lite renderer, the OpenSCAD renderer, and the command-line tool. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Canvas/CssColor.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Canvas/CssColorTests.cs */
        comment designRef /* Design: docs/design/canvas-net/canvas/css-color.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/canvas/css-color.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/canvas/css-color.yaml */
    }
}
