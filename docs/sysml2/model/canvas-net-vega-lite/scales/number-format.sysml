package CanvasNet {
    part def NumberFormat {
        doc /* The internal NumberFormat (a sealed partial class) and the FormatSpecifier record, a port with attribution of d3-format and the tickFormat precision rule of d3-scale: ParseSpecifier and TryParseSpecifier read the specifier grammar (fill, align, sign, symbol, zero, width, comma, precision, trim, and the types e f g r s % p b o d x X c n and the empty type; an invalid specifier is a FormatException invalid format), Create and CreatePrefix build a formatter (the SI prefixes from yocto to yotta, the default en-US locale, the minus sign U+2212, widths above 1,000 clamped), Format reproduces JavaScript toFixed, toExponential, and toPrecision digits from the exact decimal expansion of the double (BigInteger), so the result never depends on the platform's floating-point formatting or the current culture, TickFormat derives the precision from the tick step (the label format of Vega axes, default specifier comma f), and PrecisionFixed, PrecisionPrefix, and PrecisionRound are d3's precision helpers. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Scales/NumberFormat.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/NumberFormatTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/scales/number-format.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/scales/number-format.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/scales/number-format.yaml */
    }
}
