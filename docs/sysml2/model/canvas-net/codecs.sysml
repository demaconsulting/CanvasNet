package CanvasNet {
    part def Codecs {
        doc /* Flat image-format codecs subsystem: BmpCodec, PngCodec, TiffCodec, and JpegCodec, each converting to and from a Canvas subsystem Surface pixel buffer, plus GifCodec, a decode-only unit that decodes only the first frame of a GIF, and Jpeg2000Codec, a decode-only unit for JPEG 2000 (JP2 and raw codestream) images. */

        comment designRef /* Design: docs/design/canvas-net/codecs.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/codecs.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/codecs.yaml */

        part bmpCodec : BmpCodec;
        part pngCodec : PngCodec;
        part tiffCodec : TiffCodec;
        part jpegCodec : JpegCodec;
        part gifCodec : GifCodec;
        part jpeg2000Codec : Jpeg2000Codec;
    }
}
