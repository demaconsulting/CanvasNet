package CanvasNetViewer_GridOverlay {
    private import SoftwareItemMetadata::*;

    part def GridOverlay {
        doc /* Draws a labeled coordinate grid over the pixels of a surface in place: one-pixel vertical and horizontal lines at the multiples of a spacing (8 to 4096 pixels) starting at the first column and row, each pixel blended at most once by integer source-over arithmetic with the line color, and labels with the pixel coordinate of the lines in the bundled 12-pixel font at the top edge (vertical lines) and left edge (horizontal lines), in a box with a contrasting backing at alpha 192, only every n-th line when the spacing is small and never where the box would not fit inside the surface. It reports the number of vertical lines, horizontal lines, and labels drawn. Rows are processed in parallel with disjoint output. The unit works on surfaces only and is deterministic. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Viewer/Imaging/GridOverlay.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Viewer.Tests/Imaging/GridOverlayTests.cs");
            designFiles = ("docs/design/canvas-net-viewer/imaging/grid-overlay.md");
            verificationFiles = ("docs/verification/canvas-net-viewer/imaging/grid-overlay.md");
            requirementFiles = ("docs/reqstream/canvas-net-viewer/imaging/grid-overlay.yaml");
        }
    }
}
