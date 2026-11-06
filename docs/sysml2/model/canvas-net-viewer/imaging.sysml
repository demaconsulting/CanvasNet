package CanvasNet {
    part def Imaging {
        doc /* The subsystem in the DemaConsulting.CanvasNet.Viewer.Imaging namespace that turns files into pixels, transforms pixels, and turns pixels into files, free of command-line concerns: it is the library form of everything the canvasnet tool does with images, and the tool parses its arguments into the requests of this subsystem and prints the results. The SourceLoader unit recognizes a file (signature bytes, then ZIP package entries, then SVG text, then file name and extension), reads its header facts (for a document every page), and loads PNG, JPEG, BMP, TIFF, GIF, JPEG 2000, and SVG files, the pages of PDF, PPTX, and VSDX documents, views of OpenSCAD models, and Vega-Lite charts into a Surface at the size and resolution the options ask for (the size policy and the 8192-pixel limit), applying the EXIF orientation of a JPEG and flattening over a background color, and maps an explicit list of library exceptions to expected errors. The ImagePipeline unit crops, resizes, fits, rotates by any angle, flips, pads, adjusts the tone of, deskews, trims, and draws a grid on a Surface with the operations in the order they were written, reporting each; the flip, pad, trim, and tone arithmetic is the core library's Surface, which the pipeline calls. The ImageWriter unit chooses the output format, encodes PNG, JPEG, BMP, and TIFF files, flattens transparency for JPEG, writes through a temporary file, and applies the overwrite policy. The ContactSheet unit composes a grid of images, each fitted into a cell, with optional labels drawn in the bundled font. The GridOverlay unit draws a labeled coordinate grid over pixels in place. The ImageComparison unit checks a comparison of two images against thresholds and holds the defaults of the comparison options; the measures, the grid, and the difference, overlay, and side-by-side pictures are computed by the core Surface. The rotation by any angle, the skew estimation, and the analysis of colors are likewise the core Surface's. The ImageRenderer unit is the render operation (load the pages of a probed source, run the pipeline, and publish one file per page, all or nothing), the ImageInspector unit the inspect operation (the dominant colors and luma statistics, or a pixel, after the pipeline), the ImageComparer unit the compare operation (bring two inputs to one size, measure, check the thresholds as data, and write the difference pictures), and the MontageBuilder unit the montage operation (compose a contact sheet one source at a time). The commands of the canvasnet tool read, transform, compare, and write images only through this subsystem, and every expected failure is a ViewerException. Its constituent units are SourceLoader, ImagePipeline, ImageWriter, ImageComparison, ContactSheet, GridOverlay, ImageRenderer, ImageInspector, ImageComparer, and MontageBuilder. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Viewer.Tests/Imaging/ImagingSubsystemTests.cs */
        comment designRef /* Design: docs/design/canvas-net-viewer/imaging.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-viewer/imaging.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-viewer/imaging.yaml */

        part sourceLoader : SourceLoader;
        part imagePipeline : ImagePipeline;
        part imageWriter : ImageWriter;
        part imageComparison : ImageComparison;
        part contactSheet : ContactSheet;
        part gridOverlay : GridOverlay;
        part imageRenderer : ImageRenderer;
        part imageInspector : ImageInspector;
        part imageComparer : ImageComparer;
        part montageBuilder : MontageBuilder;
    }
}
