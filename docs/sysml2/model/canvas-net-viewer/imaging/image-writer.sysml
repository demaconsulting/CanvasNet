package CanvasNetViewer_ImageWriter {
    private import SoftwareItemMetadata::*;

    part def ImageWriter {
        doc /* Writes pixels to a PNG, JPEG, BMP, or TIFF file. ResolveFormat takes the format from the format option or the file name extension; CheckTarget refuses an output that is the input or a directory, and (with no-clobber) an existing file, without writing; CheckTargets checks every output of a command against every input and against each other (two outputs may not be one file) in one step, without writing; Write encodes into a temporary file in the target directory and moves it over the target, creating a missing directory, leaving nothing behind on failure, and reporting a write failure as an expected error; Stage does the encoding and returns a staged output whose Commit performs the move and whose disposal deletes the temporary file, so a command writing several outputs can publish them together only when all were encoded. An existing output is overwritten unless no-clobber is set. The encoders are deterministic. The unit includes the WriteOptions and WriteResult records. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Viewer/Imaging/ImageWriter.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Viewer.Tests/Imaging/ImageWriterTests.cs");
            designFiles = ("docs/design/canvas-net-viewer/imaging/image-writer.md");
            verificationFiles = ("docs/verification/canvas-net-viewer/imaging/image-writer.md");
            requirementFiles = ("docs/reqstream/canvas-net-viewer/imaging/image-writer.yaml");
        }
    }
}
