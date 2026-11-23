package CanvasNet_TiffCodec {
    private import SoftwareItemMetadata::*;

    part def TiffCodec {
        doc /* Hand-rolled loader/saver for 8-bit-per-sample RGB, RGBA, and Grayscale, strip-based TIFF 6.0 files, converting to and from Surface pixel buffers. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Codecs/TiffCodec.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Tiff/TiffCodec.DataSource.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Tiff/TiffCodec.Decode.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Tiff/TiffCodec.Ifd.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Tiff/TiffCodec.Compression.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Tiff/TiffCodec.Utils.cs",
                "src/DemaConsulting.CanvasNet/Codecs/ZlibHelper.cs",
                "src/DemaConsulting.CanvasNet/Codecs/LzwDecoder.cs",
                "src/DemaConsulting.CanvasNet/Codecs/ImageInfo.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tests/Codecs/TiffCodecTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Codecs/TiffFixtureTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Codecs/LzwDecoderTests.cs"
            );
            designFiles = ("docs/design/canvas-net/codecs/tiff-codec.md");
            verificationFiles = ("docs/verification/canvas-net/codecs/tiff-codec.md");
            requirementFiles = ("docs/reqstream/canvas-net/codecs/tiff-codec.yaml");
        }
    }
}
