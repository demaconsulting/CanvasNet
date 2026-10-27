package CanvasNet {
    part def SceneGraph {
        doc /* The public immutable scene graph: the closed SceneNode hierarchy (MeshNode leaves and GroupNode, CsgNode, and OverlayNode interior nodes, each with a validated double-precision Transform and an optional NodeAppearance color; a CsgNode combines its children with a CsgOperation of Union, Difference, or Intersection; an OverlayNode is an independent translucent body with a forced alpha-honoring color that is false in every ancestor boolean), and the Scene root that composes transforms child-then-parent with an iterative (stack-safe) depth-first pre-order traversal over group, CSG, and overlay children, applies innermost-appearance-wins color inheritance (an overlay forcing its own color), assigns each mesh instance a stable draw-priority order, and computes tight world-space bounds of all operands with or without the overlay bodies (ComputeBounds(bool)). The internal SceneInstance value is the flattened (mesh, world transform, appearance, order, source MeshNode, in-overlay flag) record the renderer consumes. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/SceneModel/SceneNode.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/MeshNode.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/GroupNode.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/CsgNode.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/CsgOperation.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/OverlayNode.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/NodeAppearance.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/Scene.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/SceneInstance.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/SceneModel/SceneGraphTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/scene-model/scene-graph.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/scene-model/scene-graph.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/scene-model/scene-graph.yaml */
    }
}
