package CanvasNet {
    part def Surface {
        doc /* Mutable, in-memory 32-bit RGBA pixel buffer providing span-based row access, independent-copy cropping and cloning, an opaque clipped CopyFrom of another surface, vectorized bulk pixel operations (alpha premultiplication and Porter-Duff "over" compositing), and high-quality resizing (Surface.Resize) through the internal two-pass separable resampling engine (nearest, area, bilinear, Catmull-Rom bicubic and Lanczos3 filters selected by ResamplingMode, premultiplied-alpha filtering, edge clamping, bounded-memory strips, and overflow-safe handling of extreme footprints). Implements IDisposable to establish the disposal contract for its pixel buffer ahead of a future ArrayPool-backed storage change. The class is partial: the tone and color adjustments and the whole-surface transformations are the SurfaceAdjustments and SurfaceTransforms units. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Canvas/Surface.cs, src/DemaConsulting.CanvasNet/Canvas/Rgba32.cs, src/DemaConsulting.CanvasNet/Canvas/ResamplingMode.cs, src/DemaConsulting.CanvasNet/Canvas/SurfaceResampler.cs, src/DemaConsulting.CanvasNet/Canvas/SurfaceResampler.Engine.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceTests.cs, test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceCopyTests.cs, test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceResizeTests.cs, test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceResamplerTests.cs, test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceResizeCodecRoundTripTests.cs */
        comment designRef /* Design: docs/design/canvas-net/canvas/surface.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/canvas/surface.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/canvas/surface.yaml */
    }
}
