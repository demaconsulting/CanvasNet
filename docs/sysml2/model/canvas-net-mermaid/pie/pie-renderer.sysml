package CanvasNetMermaid_PieRenderer {
    private import SoftwareItemMetadata::*;

    part def PieRenderer {
        doc /* The internal sealed PieRenderer (a partial class in two files) with the PieArc type: Draw turns a PieDiagram into a scene for one render as Mermaid's pie renderer does. The chart is 450 pixels high with its pie centered at (225, 225) and a radius of 185 inside an outer circle; sections under 1% of the sum are left out of the pie, the others are cut in order by a port of d3's pie and arc (path text with three-digit rounding and the centroid) and labeled with their JavaScript toFixed(0) percentage at textPosition times the radius; the colors are the theme's pie1 to pie12 by section index; the donut hole applies between 0 and 0.9 and the highlighted slice is opaque and 1.05 times larger. The title is above the pie, and the legend lists every section (with its value when showData is set) at the right, left, top, bottom, or center, from the widest legend text as the browser measures it; the view box takes in the chart, the legend, and a title wider than the chart. The paint comes from the pie style sheet with the element opacity folded into the colors. The layout work meter is charged for the sections and the measured texts and checks cancellation. An instance draws one render on one thread. It tags the slices (label, value, mid-angle aim) and the title (at its title statement) for ScenePositions. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Mermaid/Pie/PieRenderer.cs",
                "src/DemaConsulting.CanvasNet.Mermaid/Pie/PieRenderer.Arcs.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Mermaid.Tests/Pie/PieRendererTests.cs");
            designFiles = ("docs/design/canvas-net-mermaid/pie/pie-renderer.md");
            verificationFiles = ("docs/verification/canvas-net-mermaid/pie/pie-renderer.md");
            requirementFiles = ("docs/reqstream/canvas-net-mermaid/pie/pie-renderer.yaml");
        }
    }
}
