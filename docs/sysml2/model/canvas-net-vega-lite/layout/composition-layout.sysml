package CanvasNet {
    part def CompositionLayout {
        doc /* The internal static partial CompositionLayout with the records CompositionCell, HeaderGroup, CompositionHeaders, and CompositionResult, a C# port of the grid layout and the trellis layout of Vega's view layout with attribution: Place lays the cells of a concatenation or a facet out in a grid with LegendBuilder.GridLayout (the number of columns, the spacing of the columns and rows, the alignment each, all, or none, centering, full bounds or the flush width and height of the groups, a null cell or a cell without content being an empty cell that keeps its track) and places the row and column headers and footers of a facet at the edge of the cells rounded outwards plus their offset, each at the position of its row or column (the nearest earlier cell for a missing one), and the row and column titles beside them centered along the grid by their band with the position rounded half up. The result holds the placed groups as copies in the drawing order of Vega-Lite (titles, row headers and footers, column headers and footers, cells), the union of the full bounds of everything, and the origin of every cell. The unit works on pure boxes, is called by the Composer for every concatenation and facet, is stateless, throws only for null arguments, and needs no limit or cancellation point because it loops over the cells and headers of one composition that the caller has bounded. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Layout/CompositionLayout.cs and CompositionLayout.Headers.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Layout/CompositionLayoutTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/layout/composition-layout.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/layout/composition-layout.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/layout/composition-layout.yaml */
    }
}
