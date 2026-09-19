package CanvasNet {
    part def Codecs {
        doc /* Flat image-format codecs subsystem: BmpCodec, PngCodec, TiffCodec, and JpegCodec, each converting to and from a Canvas subsystem Surface pixel buffer. */

        comment designRef /* Design: docs/design/canvas-net/codecs.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/codecs.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/codecs.yaml */

        part bmpCodec : BmpCodec;
        part pngCodec : PngCodec;
        part tiffCodec : TiffCodec;
        part jpegCodec : JpegCodec;
    }
}
