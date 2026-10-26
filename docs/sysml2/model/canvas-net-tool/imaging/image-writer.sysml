package CanvasNet {
    part def ImageWriter {
        doc /* Writes pixels to a PNG, JPEG, BMP, or TIFF file. ResolveFormat takes the format from the format option or the file name extension; CheckTarget refuses an output that is the input or a directory, and (with no-clobber) an existing file, without writing; Write flattens transparency over a background color for JPEG, encodes into a temporary file in the target directory, and moves it over the target, creating a missing directory, leaving nothing behind on failure, and reporting a write failure as an expected error. An existing output is overwritten unless no-clobber is set. The encoders are deterministic. The unit includes the WriteOptions and WriteResult records. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Imaging/ImageWriter.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Imaging/ImageWriterTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/imaging/image-writer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/imaging/image-writer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/imaging/image-writer.yaml */
    }
}
