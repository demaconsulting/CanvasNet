package CanvasNet {
    part def PngCodec {
        doc /* Hand-rolled loader for every non-interlaced, spec-valid PNG color type/bit depth combination, and saver for 8-bit-per-channel Truecolor and Truecolor-with-alpha, non-interlaced PNG files, converting to and from Surface pixel buffers. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Codecs/PngCodec.cs, src/DemaConsulting.CanvasNet/Codecs/ImageInfo.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Codecs/PngCodecTests.cs, test/DemaConsulting.CanvasNet.Tests/Codecs/PngSuiteTests.cs */
        comment designRef /* Design: docs/design/canvas-net/codecs/png-codec.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/codecs/png-codec.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/codecs/png-codec.yaml */
    }
}
