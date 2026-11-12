package CanvasNet {
    part def DagreNormalizer {
        doc /* The internal static DagreNormalizer class, the port of dagre's normalize.js. Run splits every edge that spans more than one rank into a chain of one-rank edges through dummy nodes of size 0 that keep the edge's weight and name, giving the dummy at the edge's label rank the label's size and position and recording the first dummy of each chain in the graph label. Undo replaces each chain with its original edge and label after positioning, with the dummy positions as the route and the position and size of the label dummy as the label's. Every edge and dummy charges the meter one step. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Dagre/DagreNormalizer.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreNormalizerTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/dagre/dagre-normalizer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/dagre/dagre-normalizer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/dagre/dagre-normalizer.yaml */
    }
}
