package CanvasNet {
    part def Jpeg2000Codec {
        doc /* Hand-rolled, decode-only, dependency-free ISO/IEC 15444-1 (JPEG 2000 Part 1) decoder for JP2 files and raw codestreams, converting to Surface pixel buffers or to a Jpeg2000Image of 8-bit component samples. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Codecs/Jpeg2000Codec.cs, src/DemaConsulting.CanvasNet/Codecs/Jpeg2000Image.cs, src/DemaConsulting.CanvasNet/Codecs/Jpeg2000ColorSpace.cs, src/DemaConsulting.CanvasNet/Codecs/Jpeg2000/Jpeg2000Codec.Codestream.cs, src/DemaConsulting.CanvasNet/Codecs/Jpeg2000/Jpeg2000Codec.Geometry.cs, src/DemaConsulting.CanvasNet/Codecs/Jpeg2000/Jpeg2000Codec.Packets.cs, src/DemaConsulting.CanvasNet/Codecs/Jpeg2000/Jpeg2000Codec.Mq.cs, src/DemaConsulting.CanvasNet/Codecs/Jpeg2000/Jpeg2000Codec.Tier1.cs, src/DemaConsulting.CanvasNet/Codecs/Jpeg2000/Jpeg2000Codec.Dwt.cs, src/DemaConsulting.CanvasNet/Codecs/Jpeg2000/Jpeg2000Codec.Decoder.cs, src/DemaConsulting.CanvasNet/Codecs/Jpeg2000/Jpeg2000Codec.Jp2.cs, src/DemaConsulting.CanvasNet/Codecs/ImageInfo.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Codecs/Jpeg2000CodecTests.cs, test/DemaConsulting.CanvasNet.Tests/Codecs/Jpeg2000TestEncoder.cs */
        comment designRef /* Design: docs/design/canvas-net/codecs/jpeg2000-codec.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/codecs/jpeg2000-codec.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/codecs/jpeg2000-codec.yaml */
    }
}
