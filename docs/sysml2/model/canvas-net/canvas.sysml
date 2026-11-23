package CanvasNet_CanvasSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNet_Surface::*;
    public import CanvasNet_SurfaceAdjustments::*;
    public import CanvasNet_SurfaceTransforms::*;
    public import CanvasNet_SurfaceGeometry::*;
    public import CanvasNet_SurfaceComparison::*;
    public import CanvasNet_SurfaceAnalysis::*;
    public import CanvasNet_Rgba32::*;
    public import CanvasNet_CssColor::*;

    part def Canvas {
        doc /* Pixel-buffer primitives subsystem: the Surface unit with its SurfaceAdjustments (tone and color adjustments), SurfaceTransforms (flip, EXIF orientation, pad, trim, fit, flatten), SurfaceGeometry (rotation by any angle and skew estimation), SurfaceComparison (metrics, grid, and difference pictures), and SurfaceAnalysis (dominant colors, luma statistics, and pixel samples) parts, its supporting Rgba32 value type, and the public CssColor parser that turns CSS color text into Rgba32 values. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/canvas-net/canvas.md");
            verificationFiles = ("docs/verification/canvas-net/canvas.md");
            requirementFiles = ("docs/reqstream/canvas-net/canvas.yaml");
        }

        part surface : Surface;
        part surfaceAdjustments : SurfaceAdjustments;
        part surfaceTransforms : SurfaceTransforms;
        part surfaceGeometry : SurfaceGeometry;
        part surfaceComparison : SurfaceComparison;
        part surfaceAnalysis : SurfaceAnalysis;
        part rgba32 : Rgba32;
        part cssColor : CssColor;
    }
}
