package CanvasNet {
    part def VegaLiteLayout {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.VegaLite.Layout namespace that computes the boxes of a chart; in this release it lays out a single view (the composition layout is a later phase). Its ViewLayout unit works in two phases like Vega: Resolve reads the settings that do not depend on the content (the data rectangle from width, height, the view configuration of 300 by 300, or the step of a discrete scale, or the container size; the padding; autosize of pad or none with contains; the canvas and plot backgrounds and the view style), then, after the guide builders have measured themselves, Measure places the title, computes the bounds of everything and the canvas size, and Build assembles the immutable Scene (root group, view group with the plot background and border, the content, the title). The subsystem is internal and draws nothing, so its numbers are asserted analytically. As of this release, its only unit is ViewLayout. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Layout/ (the unit test class ViewLayoutTests) */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/layout.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/layout.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/layout.yaml */

        part viewLayout : ViewLayout;
    }
}
