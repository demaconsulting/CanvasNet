package CanvasNetRender3D_SceneRenderingSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetRender3D_Camera::*;
    public import CanvasNetRender3D_Lighting::*;
    public import CanvasNetRender3D_TriangleRasterizer::*;
    public import CanvasNetRender3D_FragmentBuffer::*;
    public import CanvasNetRender3D_CsgResolver::*;
    public import CanvasNetRender3D_SceneRenderer::*;

    part def SceneRendering {
        doc /* The software rasterization pipeline in the DemaConsulting.CanvasNet.Render3D.SceneRendering namespace that renders a SceneModel Scene onto a new core Canvas.Surface: a perspective/orthographic Camera (LookAt, FitToBounds framing, camera-relative double-precision view transform), an ambient/directional Lighting model with flat Lambert and optional Blinn-Phong shading, a fixed-point (24.8) TriangleRasterizer with homogeneous near-plane/guard-band clipping and the top-left fill rule, and the SceneRenderer tiled driver with ordered-grid supersampling antialiasing, deterministic tile parallelism, and cooperative cancellation. The driver uses a z-buffer for union-only scenes and a fragment-list constructive-solid-geometry path (FragmentBuffer collecting every surface crossing of a tile, CsgResolver compiling the CsgNode tree into a postfix program and resolving the visible surface of unions, differences, and intersections per sample, with a bounded per-tile fragment budget and closed-operand diagnostics) for scenes that contain a difference or intersection. As of this release, its constituent units are Camera, Lighting, TriangleRasterizer, FragmentBuffer, CsgResolver, and SceneRenderer. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/SceneRenderingTests.cs");
            designFiles = ("docs/design/canvas-net-render3d/scene-rendering.md");
            verificationFiles = ("docs/verification/canvas-net-render3d/scene-rendering.md");
            requirementFiles = ("docs/reqstream/canvas-net-render3d/scene-rendering.yaml");
        }

        part camera : Camera;
        part lighting : Lighting;
        part triangleRasterizer : TriangleRasterizer;
        part fragmentBuffer : FragmentBuffer;
        part csgResolver : CsgResolver;
        part sceneRenderer : SceneRenderer;
    }
}
