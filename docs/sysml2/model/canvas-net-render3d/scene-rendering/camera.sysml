package CanvasNet {
    part def Camera {
        doc /* The public Camera class and CameraProjection enumeration plus the internal ViewTransform: eye/target/up with perspective or orthographic projection, validated field of view, orthographic height and near/far planes, LookAt (substituting an alternate up vector when the view direction is parallel to up), the orbit-convention FitToBounds that tightly frames a bounding box from an azimuth/elevation, the double-precision view matrix, and the camera-relative view transform used to project geometry without precision loss far from the origin. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/SceneRendering/Camera.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/CameraProjection.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/ViewTransform.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/CameraTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/scene-rendering/camera.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/scene-rendering/camera.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/scene-rendering/camera.yaml */
    }
}
