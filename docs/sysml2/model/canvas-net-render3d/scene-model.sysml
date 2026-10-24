package CanvasNet {
    part def SceneModel {
        doc /* The format-agnostic, immutable 3D data model in the DemaConsulting.CanvasNet.Render3D.SceneModel namespace: double-precision Vector2D/Vector3D/Matrix4x4D/BoundingBox3D math, validated triangle Mesh instances with the MeshBuilder welding builder, and the Scene graph of MeshNode/GroupNode nodes carrying double-precision transforms and optional NodeAppearance colors. As of this release, its constituent units are DoublePrecisionMath, Mesh, and SceneGraph. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/SceneModel/SceneModelTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/scene-model.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/scene-model.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/scene-model.yaml */

        part doublePrecisionMath : DoublePrecisionMath;
        part mesh : Mesh;
        part sceneGraph : SceneGraph;
    }
}
