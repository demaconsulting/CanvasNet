package CanvasNet_PngCodec {
    private import SoftwareItemMetadata::*;

    part def PngCodec {
        doc /* Hand-rolled loader for every non-interlaced, spec-valid PNG color type/bit depth combination, and saver for 8-bit-per-channel Truecolor and Truecolor-with-alpha, non-interlaced PNG files, converting to and from Surface pixel buffers. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Codecs/PngCodec.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Png/PngCodec.Chunks.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Png/PngCodec.Decode.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Png/PngCodec.Filtering.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Png/PngCodec.Zlib.cs",
                "src/DemaConsulting.CanvasNet/Codecs/ZlibHelper.cs",
                "src/DemaConsulting.CanvasNet/Codecs/PngFilter.cs",
                "src/DemaConsulting.CanvasNet/Codecs/ImageInfo.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tests/Codecs/PngCodecTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Codecs/PngSuiteTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Codecs/PngFilterTests.cs"
            );
            designFiles = ("docs/design/canvas-net/codecs/png-codec.md");
            verificationFiles = ("docs/verification/canvas-net/codecs/png-codec.md");
            requirementFiles = ("docs/reqstream/canvas-net/codecs/png-codec.yaml");
        }
    }
}
