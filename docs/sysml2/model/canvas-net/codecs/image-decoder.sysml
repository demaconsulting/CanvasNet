package CanvasNet {
    part def ImageDecoder {
        doc /* Format-neutral image decoding facade: the ImageFormat enum, the ImageFormats recognizer (signature and media-type detection), and the ImageDecoder dispatcher that decodes a stream or byte span into a Surface through the matching sibling codec. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Codecs/ImageFormat.cs, src/DemaConsulting.CanvasNet/Codecs/ImageFormats.cs, src/DemaConsulting.CanvasNet/Codecs/ImageDecoder.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Codecs/ImageFormatsTests.cs, test/DemaConsulting.CanvasNet.Tests/Codecs/ImageDecoderTests.cs */
        comment designRef /* Design: docs/design/canvas-net/codecs/image-decoder.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/codecs/image-decoder.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/codecs/image-decoder.yaml */
    }
}
