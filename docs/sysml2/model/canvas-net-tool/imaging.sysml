package CanvasNet {
    part def Imaging {
        doc /* The subsystem in the DemaConsulting.CanvasNet.Tool.Imaging namespace that turns files into pixels, transforms pixels, and turns pixels into files, free of command-line concerns apart from the parsing of the transformation operations. The SourceLoader unit recognizes a file (signature bytes, then ZIP package entries, then SVG text, then extension), reads its header facts (for a document every page), and loads PNG, JPEG, BMP, TIFF, GIF, JPEG 2000, and SVG files, the pages of PDF, PPTX, and VSDX documents, and views of OpenSCAD models into a Surface at the size and resolution the options ask for (the size policy and the 8192-pixel limit), applying the EXIF orientation of a JPEG and flattening over a background color, and maps an explicit list of library exceptions to expected errors. The ImagePipeline unit crops, resizes, fits, rotates, flips, and pads a Surface with the operations in the order they were written, reporting each. The ImageWriter unit chooses the output format, encodes PNG, JPEG, BMP, and TIFF files, flattens transparency for JPEG, writes through a temporary file, and applies the overwrite policy. The Commands subsystem reads, transforms, and writes images only through this subsystem. As of this release, its constituent units are SourceLoader, ImagePipeline, and ImageWriter; units for comparison are added as they are implemented. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Imaging/ImagingSubsystemTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/imaging.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/imaging.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/imaging.yaml */

        part sourceLoader : SourceLoader;
        part imagePipeline : ImagePipeline;
        part imageWriter : ImageWriter;
    }
}
