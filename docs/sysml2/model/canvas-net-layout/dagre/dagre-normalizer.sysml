package CanvasNetLayout_DagreNormalizer {
    private import SoftwareItemMetadata::*;

    part def DagreNormalizer {
        doc /* The internal static DagreNormalizer class, the port of dagre's normalize.js. Run splits every edge that spans more than one rank into a chain of one-rank edges through dummy nodes of size 0 that keep the edge's weight and name, giving the dummy at the edge's label rank the label's size and position and recording the first dummy of each chain in the graph label. Undo replaces each chain with its original edge and label after positioning, with the dummy positions as the route and the position and size of the label dummy as the label's. Every edge and dummy charges the meter one step. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Layout/Dagre/DagreNormalizer.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Layout.Tests/Dagre/DagreNormalizerTests.cs");
            designFiles = ("docs/design/canvas-net-layout/dagre/dagre-normalizer.md");
            verificationFiles = ("docs/verification/canvas-net-layout/dagre/dagre-normalizer.md");
            requirementFiles = ("docs/reqstream/canvas-net-layout/dagre/dagre-normalizer.yaml");
        }
    }
}
