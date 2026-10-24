package CanvasNet {
    part def SceneRenderer {
        doc /* The public static SceneRenderer (Render and RenderFitted) and immutable, init-validated SceneRenderOptions, plus the internal TileBinner, SampleResolver, and TileWorkspace: validates arguments, flattens the scene, sets up and bins triangles into fixed 32x32-pixel tiles, depth-tests each tile with a bounded per-worker workspace, resolves k-by-k ordered-grid supersamples with a premultiplied-alpha box filter (honoring background alpha) into the output Surface, runs tiles in parallel with output bit-identical to sequential rendering, and cooperatively cancels with a plain OperationCanceledException. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/SceneRendering/SceneRenderer.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/SceneRenderOptions.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/RenderBudgetExceededException.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/SceneRenderUnsupportedFeature.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/SceneRenderUnsupportedFeatureException.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/TileBinner.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/SampleResolver.cs, src/DemaConsulting.CanvasNet.Render3D/SceneRendering/TileWorkspace.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/SceneRendererTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/SceneRenderOptionsTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/SceneRenderDiagnosticsTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/TileBinnerTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/SampleResolverTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/scene-rendering/scene-renderer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/scene-rendering/scene-renderer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/scene-rendering/scene-renderer.yaml */
    }
}
