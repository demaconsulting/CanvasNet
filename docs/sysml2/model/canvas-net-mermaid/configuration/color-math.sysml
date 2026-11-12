package CanvasNet {
    part def ColorMath {
        doc /* The internal static ColorMath (partial class in ColorMath.cs and ColorMath.Parsers.cs) with the channel model Channels and the exception ColorFormatException: the port of the subset of the khroma color library that Mermaid's themes and styles use. It keeps every detail that shows in the text of a derived color: the parsers of the hexadecimal, rgb, rgba, hsl, hsla, and keyword forms (khroma's own keyword table, matched in any letter case), the lazy RGB and HSL channel model that decides whether a result is written as a hexadecimal, rgb, rgba, hsl, or hsla color, an unchanged color returned as it was written, rounding to ten decimals with JavaScript's rounding of halves, JavaScript's number formatting, and khroma's arithmetic quirks (a missing amount gives NaN, the hue reduced with the sign-preserving remainder). The functions are adjust, darken, lighten, invert, mix, rgba, channel, luminance, and isDark. khroma's regular expressions are replaced by a scanner with the same matches, linear in the length of the text. A value that is not a string or a string in none of khroma's formats throws ColorFormatException carrying the value (khroma would give NaN for a non-string in a few paths), and changing both RGB and HSL channels throws InvalidOperationException as in khroma. The class is stateless and depends only on the .NET base class library. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Configuration/ColorMath.cs, src/DemaConsulting.CanvasNet.Mermaid/Configuration/ColorMath.Parsers.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Configuration/ColorMathTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/configuration/color-math.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/configuration/color-math.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/configuration/color-math.yaml */
    }
}
