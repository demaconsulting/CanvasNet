package CanvasNet {
    part def MontageBuilder {
        doc /* The montage operation of the library: given the probed facts of several sources and a request, it plans one cell for each source (or for each page of a document when every page is asked for), calculates the layout with the ContactSheet (refusing a sheet larger than the maximum size before any source is decoded), checks the output against the inputs, opens each source in turn, loads its page into its cell (a vector source rendered at the cell size, a raster source fitted), adds it to the sheet with its label (the file name, and the page number of a document page), disposes it before the next, and publishes the finished sheet with the ImageWriter, so the memory in use is the sheet and one source. It reports the layout, the file written, and the position, size, and label of every cell. The unit includes the MontageRequest, MontageCell, and MontageResult records. It is the library form of the montage command of the canvasnet tool. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Viewer/Imaging/MontageBuilder.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Viewer.Tests/Imaging/MontageBuilderTests.cs */
        comment designRef /* Design: docs/design/canvas-net-viewer/imaging/montage-builder.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-viewer/imaging/montage-builder.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-viewer/imaging/montage-builder.yaml */
    }
}
