package CanvasNet {
    part def LegendLayout {
        doc /* The internal static LegendLayout with the LegendPlacement record, a C# port of the legend layout of Vega's view layout with attribution: Place groups the built legends by orient and positions each group with the grid layout of the LegendBuilder (alignment each, flush boxes or full bounds, the margin of 8 pixels between boxes, rounding half up) - right and left legends in a column, top and bottom legends in a row, anchored outside the data rectangle united with the axes of their side at the largest requested offset or the layout offset of 18, along the side by the anchor start, middle, or end of the data rectangle or of the bounds, the orients top-left, top-right, bottom-left, and bottom-right at the offset from the corners inside the data rectangle, and a legend without an orient at its own legendX and legendY - reading config.legend.layout the way Vega does (the object of the orient, then the layout object, then the default); AxisBounds gives the data rectangle united with the left and right axes and with the top and bottom axes; Combine joins the bounds of the legends with the extents of the axes by the larger reach on each side so that the canvas holds them. The unit is stateless, throws only for null arguments, and needs no limit or cancellation point because it loops over the legends of a view only. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Layout/LegendLayout.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Layout/LegendLayoutTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/layout/legend-layout.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/layout/legend-layout.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/layout/legend-layout.yaml */
    }
}
