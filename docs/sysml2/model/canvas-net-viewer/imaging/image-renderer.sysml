package CanvasNet {
    part def ImageRenderer {
        doc /* The render operation of the library: given the facts of a probed source and a request, it checks everything that needs no pixels first (the page, the plan of the outputs, the output format, and every output path against the input and the overwrite policy), opens the source once, loads each requested page, applies the operations of the request with the ImagePipeline, maps the anchors of a model through the applied steps, encodes each page to a temporary file with the ImageWriter, and only when every page was encoded publishes all the outputs together, so a failure leaves no new or partial output. It reports for each page its number, the facts of the source, the diagnostics, the printed text of a model, the operations applied, the file written, and the anchors. The unit includes the RenderRequest, RenderedPage, and RenderResult records. It is the library form of the render command and of the verb commands crop, resize, rotate, flip, and pad of the canvasnet tool. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Viewer/Imaging/ImageRenderer.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Viewer.Tests/Imaging/ImageRendererTests.cs */
        comment designRef /* Design: docs/design/canvas-net-viewer/imaging/image-renderer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-viewer/imaging/image-renderer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-viewer/imaging/image-renderer.yaml */
    }
}
