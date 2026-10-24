package CanvasNet {
    part def Lighting {
        doc /* The public Light hierarchy (AmbientLight and DirectionalLight, each with a validated color and intensity, and the neutral CAD DefaultRig) and the internal LightingModel: per-triangle flat Lambertian shading with optional constant-viewer Blinn-Phong specular, camera-relative or world-space light directions, back-face shading from the flipped normal and a configurable back-face color, an empty light list rendering unlit (black), and clamping of the result to the displayable range. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/SceneRendering/Light.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/AmbientLight.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/DirectionalLight.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/LightingModel.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/LightTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/LightingModelTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/scene-rendering/lighting.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/scene-rendering/lighting.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/scene-rendering/lighting.yaml */
    }
}
