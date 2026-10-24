package CanvasNet {
    part def SceneGraph {
        doc /* The public immutable scene graph: the closed SceneNode hierarchy (MeshNode leaves and GroupNode interior nodes, each with a validated double-precision Transform and an optional NodeAppearance color), and the Scene root that composes transforms child-then-parent with an iterative (stack-safe) depth-first pre-order traversal, applies innermost-appearance-wins color inheritance, assigns each mesh instance a stable draw-priority order, and computes tight world-space bounds. The internal SceneInstance value is the flattened (mesh, world transform, appearance, order) record the renderer consumes. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/SceneModel/SceneNode.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/MeshNode.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/GroupNode.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/NodeAppearance.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/Scene.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/SceneInstance.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/SceneModel/SceneGraphTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/scene-model/scene-graph.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/scene-model/scene-graph.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/scene-model/scene-graph.yaml */
    }
}
