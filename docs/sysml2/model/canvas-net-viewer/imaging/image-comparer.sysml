package CanvasNetViewer_ImageComparer {
    private import SoftwareItemMetadata::*;

    part def ImageComparer {
        doc /* The compare operation of the library: given two inputs (each a probed source, a page, and its load options) and a request, it checks the picture outputs first, loads both inputs (a vector input at the size of the target, a raster input fitted into it over the background), refuses inputs of different sizes when no resize was requested and a grid larger than the images, measures the difference with the core Surface using the tolerance and grid of the request, checks the thresholds with the ImageComparison unit (a threshold that is not met is data in the result, not an exception), draws the difference, side-by-side, and overlay pictures that were asked for, and publishes them together with the ImageWriter. The unit includes the CompareInput, ResizeTarget, CompareRequest, CompareSide, ComparedPicture, and CompareResult records. It is the library form of the compare command of the canvasnet tool. A page that the fragment of an input names replaces the page of its CompareInput. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Viewer/Imaging/ImageComparer.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Viewer.Tests/Imaging/ImageComparerTests.cs");
            designFiles = ("docs/design/canvas-net-viewer/imaging/image-comparer.md");
            verificationFiles = ("docs/verification/canvas-net-viewer/imaging/image-comparer.md");
            requirementFiles = ("docs/reqstream/canvas-net-viewer/imaging/image-comparer.yaml");
        }
    }
}
