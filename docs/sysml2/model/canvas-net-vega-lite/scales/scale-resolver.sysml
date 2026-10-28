package CanvasNet {
    part def ScaleResolver {
        doc /* The internal static ScaleResolver with the ResolvedScales, ResolvedScale, and RangeEnd types: it infers, for each encoding channel of a single view, the scale Vega-Lite would build, as a description that is independent of the size of the data rectangle until ResolvedScale.CreateModel is given it. Rules (ported from Vega-Lite and Vega with attribution): quantitative positions are linear and discrete positions are band scales for rect, bar, image, rule, and tick marks and point scales otherwise; color, fill, and stroke fields are ordinal (tableau10 or the configured scheme, with a recorded fallback for other schemes); size and opacity use the default ranges of the mark; the linear domain is the extent of the valid values with the zero rule, an explicit domain, nice, and pixel padding applied in Vega's order; a discrete domain is sorted ascending by default (sort may be ascending, descending, null, or an array); paddings follow the mark and the scale configuration; a step-driven discrete scale reports the extent the view needs; InferOrient gives the horizontal or vertical orientation of bar, tick, and rule marks. An invalid scale domain or range is a VegaLiteSpecException with the pointer, a discrete domain beyond MaxScaleDomainSize is a VegaLiteRenderException, and cancellation is polled while the data is scanned. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Scales/ScaleResolver.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ScaleResolverTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/scales/scale-resolver.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/scales/scale-resolver.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/scales/scale-resolver.yaml */
    }
}
