package CanvasNet {
    part def VegaLiteGuides {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.VegaLite.Guides namespace that builds the parts of a chart that are not marks: the TextMeasurer unit resolves CSS font requests (generic and Arial-like families to the bundled Liberation Sans, Serif, and Mono; a named family through the additional directories and, only when allowed, the installed fonts; Liberation Sans as the last resort), measures advance widths with kerning through the core TextRenderer, splits lines, truncates with Vega's algorithm, and computes the bounds of text with Vega's rules (a line is the font size tall, further lines a line height apart, fixed baseline offsets); the TitleBuilder unit builds the title and subtitle scene items and a TitleBlock that is placed next to the view after the axes are measured (orient, anchor, frame, offset, subtitle padding, and the group-title style defaults); the AxisBuilder unit builds one axis of a position scale (grid, ticks, labels, domain line, and title as scene groups at the edge of the data rectangle with Vega's half-pixel translate, rounding, extent clamping, parity and greedy label overlap removal, and the Vega-Lite property precedence) and the extents the layout needs; the LabelOverlap unit removes overlapping labels with Vega's parity and greedy methods for the axes and the legends; the LegendBuilder unit plans the legends of the channels color, size, shape, fill, stroke, strokeDash, strokeWidth, and opacity (merging channels that share a domain) and builds each as symbols, a color gradient, or discrete bands with a title and the box it needs. An axis beyond MaxTicks fails closed with a located VegaLiteRenderException, a legend beyond the limits of its values, ticks, text, or scene items does too, an invalid axis orient or d3 format is a VegaLiteSpecException, and the font directory scan is bounded. The subsystem is internal. As of this release, its constituent units are TextMeasurer, TitleBuilder, AxisBuilder, LabelOverlap, and LegendBuilder. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/ (the unit test classes TextMeasurerTests, TitleBuilderTests, AxisBuilderTests, LabelOverlapTests, LegendPlanTests, and LegendBuilderTests) */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/guides.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/guides.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/guides.yaml */
        comment /* Within the CanvasNetVegaLite system the TextMeasurer unit of this subsystem is the only unit that uses the core CanvasNet system's Fonts subsystem (TrueTypeFont, SystemFontCatalog, and the bundled Liberation fonts) and the TextRenderer of its Rendering subsystem; see the CanvasNetVegaLiteSystem dependencies. */

        part textMeasurer : TextMeasurer;
        part titleBuilder : TitleBuilder;
        part axisBuilder : AxisBuilder;
        part labelOverlap : LabelOverlap;
        part legendBuilder : LegendBuilder;
    }
}
