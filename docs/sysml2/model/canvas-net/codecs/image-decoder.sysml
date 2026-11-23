package CanvasNet_ImageDecoder {
    private import SoftwareItemMetadata::*;

    part def ImageDecoder {
        doc /* Format-neutral image decoding facade: the ImageFormat enum, the ImageFormats recognizer (signature and media-type detection), and the ImageDecoder dispatcher that decodes a stream or byte span into a Surface through the matching sibling codec. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Codecs/ImageFormat.cs",
                "src/DemaConsulting.CanvasNet/Codecs/ImageFormats.cs",
                "src/DemaConsulting.CanvasNet/Codecs/ImageDecoder.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tests/Codecs/ImageFormatsTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Codecs/ImageDecoderTests.cs"
            );
            designFiles = ("docs/design/canvas-net/codecs/image-decoder.md");
            verificationFiles = ("docs/verification/canvas-net/codecs/image-decoder.md");
            requirementFiles = ("docs/reqstream/canvas-net/codecs/image-decoder.yaml");
        }
    }
}
