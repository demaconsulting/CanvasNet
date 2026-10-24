package CanvasNet {
    part def DoublePrecisionMath {
        doc /* The public Vector2D, Vector3D, Matrix4x4D, and BoundingBox3D double-precision value types: component-wise vector arithmetic with dot/cross/length/normalize, a row-vector (System.Numerics-compatible) 4x4 matrix with translation/scale/rotation (exact for multiples of 90 degrees)/axis-angle/mirror factories, composition, determinant and inversion, point and direction transformation, and an axis-aligned bounding box with union and transform. All model-space math is carried out in double precision so that geometry far from the origin loses no accuracy before the renderer's camera-relative view transform. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/SceneModel/Vector2D.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/Vector3D.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/Matrix4x4D.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/BoundingBox3D.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/SceneModel/Vector2DTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/SceneModel/Vector3DTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/SceneModel/Matrix4x4DTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/SceneModel/BoundingBox3DTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/scene-model/double-precision-math.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/scene-model/double-precision-math.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/scene-model/double-precision-math.yaml */
    }
}
