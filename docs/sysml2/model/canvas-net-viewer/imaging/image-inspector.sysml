package CanvasNet {
    part def ImageInspector {
        doc /* The inspect operation of the library: given the facts of a probed source and a request, it checks the page, loads it, applies the operations of the request with the ImagePipeline, and reports either the dominant colors and luma statistics of the resulting pixels (analyzed over opaque white, because the loader and the pipeline have already composited the picture over the background) or the pixel at a point with a window around it, together with the size of the analyzed pixels, the operations applied, the diagnostics, and the background. It writes no file. A point outside the image is a usage error that names the size; a pixel request without a point is a defect of the caller. The unit includes the InspectRequest and InspectResult records, the InspectKind enumeration, and the PixelPoint value. It is the library form of the inspect command of the canvasnet tool. A page that the fragment of the input names replaces the page of the options. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Viewer/Imaging/ImageInspector.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Viewer.Tests/Imaging/ImageInspectorTests.cs */
        comment designRef /* Design: docs/design/canvas-net-viewer/imaging/image-inspector.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-viewer/imaging/image-inspector.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-viewer/imaging/image-inspector.yaml */
    }
}
