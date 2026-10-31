package CanvasNet {
    part def TimeLabels {
        doc /* The internal TimeLabels unit (TimeLabels.cs) of the Guides subsystem: the label rules that AxisBuilder and LegendBuilder share for a time scale and for a field with a time unit. TimeLabels.Create chooses the label formatter in the order of Vega-Lite (a format given as text is a d3 time format in the zone of the scale or UTC; a format given as an object is the Vega multi-format with its overrides; otherwise a time unit gives its default specifier; otherwise the guide default, the multi-format for an axis and the configured time format for a legend; the format type number formats the millisecond value with the number format, and the format types time and utc force time labels), bounds every label by MaxStringLength with a located VegaLiteRenderException, IntervalRequest reads an interval tickCount (an interval name, or an object with interval and step) as a TimeTickRequest, UsesVegaDefaultCount applies Vega-Lite's default tick count of ten for a field whose time unit is exactly a month, hours, a day, or a quarter, and ReadInstant reads a date given as a guide value (a timestamp, a date text, or a datetime object). It uses the TimeFormat, TimeCalendar, and TimeUnitSpec types of the Data subsystem and the time ticks of the Scales subsystem, and is culture-invariant. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Guides/TimeLabels.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/TimeLabelsTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/guides/time-labels.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/guides/time-labels.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/guides/time-labels.yaml */
    }
}
