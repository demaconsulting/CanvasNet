package CanvasNetLayout_DagrePositioning {
    private import SoftwareItemMetadata::*;

    part def DagrePositioning {
        doc /* The internal static partial DagrePositioning class, the port of dagre's position/index.js and position/bk.js. Position works on the flat copy of the ordered graph: PositionY stacks the ranks, each as tall as its tallest node and separated by the rank separation; PositionX applies Brandes and Koepf's method as dagre does - type-1 conflicts (edges crossing inner segments of long edges) and type-2 conflicts (inner segments crossing cluster borders) are marked, four alignments (up or down, left or right) align nodes with median neighbors into blocks and compact them with the block graph and dagre's separation rule (node or edge separation, side-label shifts), the narrowest alignment is the reference the others are shifted to, and each node's x is the average of its two middle values or the value of the alignment the graph names. An unknown alignment name throws InvalidOperationException; the passes charge the meter. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Layout/Dagre/DagrePositioning.cs",
                "src/DemaConsulting.CanvasNet.Layout/Dagre/DagrePositioning.BrandesKoepf.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Layout.Tests/Dagre/DagrePositioningTests.cs");
            designFiles = ("docs/design/canvas-net-layout/dagre/dagre-positioning.md");
            verificationFiles = ("docs/verification/canvas-net-layout/dagre/dagre-positioning.md");
            requirementFiles = ("docs/reqstream/canvas-net-layout/dagre/dagre-positioning.yaml");
        }
    }
}
