package CanvasNet {
    part def VegaLiteScales {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.VegaLite.Scales namespace that is the numeric heart of the renderer: the ScaleModel unit maps values to pixels (immutable linear, time and UTC, band, point, and ordinal mappings with d3-scale arithmetic and Vega's bandspace); the ScaleResolver unit infers for each channel the scale that Vega-Lite would build (type by field type, channel, and mark; domain with the zero rule and sorting, including the Phase 5 sort of a category by an aggregate of the transformed rows; paddings, nice, and default ranges; step sizing of discrete views; mark orientation) and describes it independently of pixel extents until CreateModel is given the data rectangle; the Ticks unit ports the d3-array tick algorithms, the linear nice method, and the Vega tick-count rules, and (Phase 8) the d3-time and d3-scale time ticks and time nice method over the TimeCalendar of the Data subsystem; the ScaleResolver unit also gives a temporal field its time or UTC scale, its date domain, and its time nice rule (Phase 8); the NumberFormat unit ports the d3-format number formatter (specifier grammar, SI prefixes, grouping, exact digits from the decimal expansion of the double, the tickFormat precision rule); the ColorSchemes unit holds the 68 Vega color schemes (categorical lists and continuous ramps, with their license notices) and configures a color range from a scheme; the ColorInterpolator unit ports the d3 color interpolation (sRGB, HSL, CIELAB, HCL). Most of the ported d3 and Vega logic lives here and carries attribution headers. The subsystem is internal, culture-invariant, and free of shared mutable state; a discrete domain beyond MaxScaleDomainSize and an axis beyond MaxTicks fail closed with a located VegaLiteRenderException. As of this release, its constituent units are ScaleModel, ScaleResolver, Ticks, NumberFormat, ColorSchemes, and ColorInterpolator. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ (the unit test classes ScaleModelTests, ScaleModelTimeTests, ScaleResolverTests, ScaleResolverPlanTests, ScaleResolverColorTests, ScaleResolverTimeTests, TicksTests, TicksTimeTests, NumberFormatTests, ColorSchemesTests, and ColorInterpolatorTests) */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/scales.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/scales.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/scales.yaml */

        part scaleModel : ScaleModel;
        part scaleResolver : ScaleResolver;
        part ticks : Ticks;
        part numberFormat : NumberFormat;
        part colorSchemes : ColorSchemes;
        part colorInterpolator : ColorInterpolator;
    }
}
