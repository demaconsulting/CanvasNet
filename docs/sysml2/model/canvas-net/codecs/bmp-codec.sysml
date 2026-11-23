package CanvasNet_BmpCodec {
    private import SoftwareItemMetadata::*;

    part def BmpCodec {
        doc /* Hand-rolled loader/saver for uncompressed 24-bit and 32-bit Windows BMP files, converting to and from Surface pixel buffers. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Codecs/BmpCodec.cs",
                "src/DemaConsulting.CanvasNet/Codecs/ImageInfo.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Codecs/BmpCodecTests.cs");
            designFiles = ("docs/design/canvas-net/codecs/bmp-codec.md");
            verificationFiles = ("docs/verification/canvas-net/codecs/bmp-codec.md");
            requirementFiles = ("docs/reqstream/canvas-net/codecs/bmp-codec.yaml");
        }
    }
}
