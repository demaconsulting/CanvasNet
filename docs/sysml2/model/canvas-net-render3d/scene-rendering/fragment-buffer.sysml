package CanvasNet {
    part def FragmentBuffer {
        doc /* The internal per-worker, reusable fragment store of the fragment-list CSG path: a two-pass collection protocol (Begin, CountTriangle, EndCounting, AddTriangle) that rasterizes every triangle of one tile with private count and fill fragment visitors, builds per-sample prefix-sum offsets, stores every surface crossing (depth and triangle index) of each sample in contiguous exact-sized storage without per-sample allocation, grows only to exactly the tile's need, and enforces the MaxFragmentsPerTile budget by throwing RenderBudgetExceededException deterministically (independent of triangle order, tile, and thread) when a tile's complete triangle set would exceed it. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/SceneRendering/FragmentBuffer.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/SceneRendering/FragmentBufferTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/scene-rendering/fragment-buffer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/scene-rendering/fragment-buffer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/scene-rendering/fragment-buffer.yaml */
    }
}
