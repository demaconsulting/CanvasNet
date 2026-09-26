package CanvasNet {
    part def GifCodec {
        doc /* Hand-rolled, decode-only loader for GIF87a/GIF89a files (first frame only), including a GIF-native LZW decoder, converting to Surface pixel buffers. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Codecs/GifCodec.cs, src/DemaConsulting.CanvasNet/Codecs/ImageInfo.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Codecs/GifCodecTests.cs, test/DemaConsulting.CanvasNet.Tests/Codecs/GifFixtureTests.cs */
        comment designRef /* Design: docs/design/canvas-net/codecs/gif-codec.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/codecs/gif-codec.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/codecs/gif-codec.yaml */
    }
}
