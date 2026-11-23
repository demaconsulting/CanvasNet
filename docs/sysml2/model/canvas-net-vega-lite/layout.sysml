package CanvasNetVegaLite_VegaLiteLayoutSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetVegaLite_ViewLayout::*;
    public import CanvasNetVegaLite_LegendLayout::*;
    public import CanvasNetVegaLite_CompositionLayout::*;

    part def VegaLiteLayout {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.VegaLite.Layout namespace that computes the boxes of a chart: a single view or a layered view, and the cells of a composed chart (concatenation, facet, repeat) with their headers, footers, and titles. Its ViewLayout unit works in two phases like Vega: Resolve reads the settings that do not depend on the content (the data rectangle from width, height, the view configuration of 300 by 300, or the step of a discrete scale, or the container size; the padding; autosize of pad or none with contains; the canvas and plot backgrounds and the view style), then, after the guide builders have measured themselves, Measure places the title, computes the bounds of everything and the canvas size, and Build assembles the immutable Scene (root group, view group with the plot background and border, the content, the title). The subsystem is internal and draws nothing, so its numbers are asserted analytically. Its second unit, LegendLayout, places the built legends of a view next to its data rectangle and its axes by their orient and the config.legend.layout properties (a port of the legend layout of Vega's view layout) and joins their reach with the extents of the axes so that the canvas holds them. Its third unit, CompositionLayout, is a port of the grid layout and trellis layout of Vega: it places the cells of a concatenation or a facet in a grid (columns, spacing, alignment, centering, full or flush bounds) and the headers, footers, and titles of a facet around it, working on pure boxes; ViewLayout supplies the helpers of a composed chart (ResolveSize, CellBounds, and the composition root of Resolve). As of this release, its units are ViewLayout, LegendLayout, and CompositionLayout. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Layout/ViewLayoutTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Layout/ViewLayoutCompositionTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Layout/LegendLayoutTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Layout/CompositionLayoutTests.cs"
            );
            designFiles = ("docs/design/canvas-net-vega-lite/layout.md");
            verificationFiles = ("docs/verification/canvas-net-vega-lite/layout.md");
            requirementFiles = ("docs/reqstream/canvas-net-vega-lite/layout.yaml");
        }

        part viewLayout : ViewLayout;
        part legendLayout : LegendLayout;
        part compositionLayout : CompositionLayout;
    }
}
