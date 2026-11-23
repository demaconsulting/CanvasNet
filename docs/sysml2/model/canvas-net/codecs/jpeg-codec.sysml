package CanvasNet_JpegCodec {
    private import SoftwareItemMetadata::*;

    part def JpegCodec {
        doc /* Hand-rolled loader/saver for a common real-world subset of JPEG files, converting to and from Surface pixel buffers, and a reader of the EXIF orientation (ReadExifOrientation) that examines only the marker segments in front of the image data and never throws for malformed data. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Codecs/JpegCodec.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Jpeg/JpegCodec.Huffman.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Jpeg/JpegCodec.Dct.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Jpeg/JpegCodec.Decoder.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Jpeg/JpegCodec.Decoder.Scan.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Jpeg/JpegCodec.Encoder.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Jpeg/JpegCodec.Exif.cs",
                "src/DemaConsulting.CanvasNet/Codecs/ImageInfo.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tests/Codecs/JpegCodecTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Codecs/JpegFixtureTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Codecs/JpegCodecExifTests.cs"
            );
            designFiles = ("docs/design/canvas-net/codecs/jpeg-codec.md");
            verificationFiles = ("docs/verification/canvas-net/codecs/jpeg-codec.md");
            requirementFiles = ("docs/reqstream/canvas-net/codecs/jpeg-codec.yaml");
        }
    }
}
