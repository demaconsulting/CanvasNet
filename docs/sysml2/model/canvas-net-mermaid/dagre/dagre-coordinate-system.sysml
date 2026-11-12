package CanvasNet {
    part def DagreCoordinateSystem {
        doc /* The internal static DagreCoordinateSystem class, the port of dagre's coordinate-system.js. dagre positions every layout top to bottom; Adjust swaps the width and height of every node and edge label for the rank directions LR and RL before positioning, and Undo afterwards negates every y coordinate (nodes, route points, label centers) for BT and RL and swaps x and y and the sizes back for LR and RL. The rank direction is compared in lower case; an unknown or absent direction is top to bottom. The unit holds no state and does not charge the meter. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Dagre/DagreCoordinateSystem.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreCoordinateSystemTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/dagre/dagre-coordinate-system.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/dagre/dagre-coordinate-system.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/dagre/dagre-coordinate-system.yaml */
    }
}
