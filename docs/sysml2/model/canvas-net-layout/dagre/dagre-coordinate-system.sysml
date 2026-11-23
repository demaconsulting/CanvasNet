package CanvasNetLayout_DagreCoordinateSystem {
    private import SoftwareItemMetadata::*;

    part def DagreCoordinateSystem {
        doc /* The internal static DagreCoordinateSystem class, the port of dagre's coordinate-system.js. dagre positions every layout top to bottom; Adjust swaps the width and height of every node and edge label for the rank directions LR and RL before positioning, and Undo afterwards negates every y coordinate (nodes, route points, label centers) for BT and RL and swaps x and y and the sizes back for LR and RL. The rank direction is compared in lower case; an unknown or absent direction is top to bottom. The unit holds no state and does not charge the meter. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Layout/Dagre/DagreCoordinateSystem.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Layout.Tests/Dagre/DagreCoordinateSystemTests.cs");
            designFiles = ("docs/design/canvas-net-layout/dagre/dagre-coordinate-system.md");
            verificationFiles = ("docs/verification/canvas-net-layout/dagre/dagre-coordinate-system.md");
            requirementFiles = ("docs/reqstream/canvas-net-layout/dagre/dagre-coordinate-system.yaml");
        }
    }
}
