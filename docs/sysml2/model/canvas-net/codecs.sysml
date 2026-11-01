package CanvasNet {
    part def Codecs {
        doc /* Image-format codecs subsystem: BmpCodec, PngCodec, TiffCodec, and JpegCodec, each converting to and from a Canvas subsystem Surface pixel buffer, plus GifCodec, a decode-only unit that decodes only the first frame of a GIF, Jpeg2000Codec, a decode-only unit for JPEG 2000 (JP2 and raw codestream) images, and ImageDecoder, a format-neutral facade that recognizes an image's format (signature or media type) and decodes it through the matching sibling codec. */

        comment designRef /* Design: docs/design/canvas-net/codecs.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/codecs.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/codecs.yaml */

        part bmpCodec : BmpCodec;
        part pngCodec : PngCodec;
        part tiffCodec : TiffCodec;
        part jpegCodec : JpegCodec;
        part gifCodec : GifCodec;
        part jpeg2000Codec : Jpeg2000Codec;
        part imageDecoder : ImageDecoder;

        dependency decoderUsesBmp from imageDecoder to bmpCodec;
        dependency decoderUsesPng from imageDecoder to pngCodec;
        dependency decoderUsesTiff from imageDecoder to tiffCodec;
        dependency decoderUsesJpeg from imageDecoder to jpegCodec;
        dependency decoderUsesGif from imageDecoder to gifCodec;
        dependency decoderUsesJpeg2000 from imageDecoder to jpeg2000Codec;
    }
}
