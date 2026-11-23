package CanvasNet_GifCodec {
    private import SoftwareItemMetadata::*;

    part def GifCodec {
        doc /* Hand-rolled, decode-only loader for GIF87a/GIF89a files (first frame only), including a GIF-native LZW decoder, converting to Surface pixel buffers. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Codecs/GifCodec.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Gif/GifCodec.Decode.cs",
                "src/DemaConsulting.CanvasNet/Codecs/Gif/GifCodec.Lzw.cs",
                "src/DemaConsulting.CanvasNet/Codecs/ImageInfo.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tests/Codecs/GifCodecTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Codecs/GifFixtureTests.cs"
            );
            designFiles = ("docs/design/canvas-net/codecs/gif-codec.md");
            verificationFiles = ("docs/verification/canvas-net/codecs/gif-codec.md");
            requirementFiles = ("docs/reqstream/canvas-net/codecs/gif-codec.yaml");
        }
    }
}
