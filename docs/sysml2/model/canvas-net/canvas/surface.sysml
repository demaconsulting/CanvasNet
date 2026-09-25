package CanvasNet {
    part def Surface {
        doc /* Mutable, in-memory 32-bit RGBA pixel buffer providing span-based row access, independent-copy cropping, and vectorized bulk pixel operations (alpha premultiplication and Porter-Duff "over" compositing). Implements IDisposable to establish the disposal contract for its pixel buffer ahead of a future ArrayPool-backed storage change. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Canvas/Surface.cs, src/DemaConsulting.CanvasNet/Canvas/Rgba32.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceTests.cs */
        comment designRef /* Design: docs/design/canvas-net/canvas/surface.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/canvas/surface.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/canvas/surface.yaml */
    }
}
