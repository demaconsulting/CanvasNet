package CanvasNet {
    part def Imaging {
        doc /* The subsystem in the DemaConsulting.CanvasNet.Tool.Imaging namespace that turns files into pixels, transforms pixels, and turns pixels into files, free of command-line concerns apart from the parsing of the transformation operations. The SourceLoader unit recognizes a file (signature bytes, then ZIP package entries, then SVG text, then file name and extension), reads its header facts (for a document every page), and loads PNG, JPEG, BMP, TIFF, GIF, JPEG 2000, and SVG files, the pages of PDF, PPTX, and VSDX documents, views of OpenSCAD models, and Vega-Lite charts into a Surface at the size and resolution the options ask for (the size policy and the 8192-pixel limit), applying the EXIF orientation of a JPEG and flattening over a background color, and maps an explicit list of library exceptions to expected errors. The ImagePipeline unit crops, resizes, fits, rotates by any angle, flips, pads, adjusts the tone of, deskews, trims, and draws a grid on a Surface with the operations in the order they were written, reporting each; the flip, pad, trim, and tone arithmetic is the core library's Surface, which the pipeline calls. The ImageWriter unit chooses the output format, encodes PNG, JPEG, BMP, and TIFF files, flattens transparency for JPEG, writes through a temporary file, and applies the overwrite policy. The ContactSheet unit composes a grid of images, each fitted into a cell, with optional labels drawn in the bundled font. The ImageGeometry unit rotates by any angle onto an expanded canvas with a fill color and estimates the skew of near-horizontal lines. The GridOverlay unit draws a labeled coordinate grid over pixels in place. The ImageAnalysis unit reports the dominant colors, the luma statistics, and the pixels at a point of an image. The ImageComparison unit compares two images of the same size (exact error metrics, a windowed structural similarity, a per-cell grid, and threshold checks) and creates difference, overlay, and side-by-side pictures. The Commands subsystem reads, transforms, compares, and writes images only through this subsystem. As of this release, its constituent units are SourceLoader, ImagePipeline, ImageWriter, ImageComparison, ContactSheet, ImageGeometry, GridOverlay, and ImageAnalysis; further units are added as they are implemented. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Imaging/ImagingSubsystemTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/imaging.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/imaging.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/imaging.yaml */

        part sourceLoader : SourceLoader;
        part imagePipeline : ImagePipeline;
        part imageWriter : ImageWriter;
        part imageComparison : ImageComparison;
        part contactSheet : ContactSheet;
        part imageGeometry : ImageGeometry;
        part gridOverlay : GridOverlay;
        part imageAnalysis : ImageAnalysis;
    }
}
