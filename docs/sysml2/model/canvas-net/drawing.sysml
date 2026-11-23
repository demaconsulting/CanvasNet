package CanvasNet_DrawingSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNet_PathFiller::*;
    public import CanvasNet_PathStroker::*;
    public import CanvasNet_GradientPaint::*;
    public import CanvasNet_TilePaint::*;

    part def Drawing {
        doc /* Antialiased vector-rendering subsystem for Geometry.Path geometry, supporting solid-color, gradient, and tiled-pattern paint. Provides PathFiller (direct path filling, solid color, GradientPaint, or TilePaint), PathStroker (stroke-to-fill conversion with standard cap/join/dash styling), GradientPaint (linear/radial gradient definition and evaluation), and TilePaint (pre-rendered tiled-pattern definition and evaluation), consuming Canvas.Surface and Geometry.Path; neither Canvas nor Geometry depends on Drawing, although callers may combine Drawing with Fonts-produced glyph paths. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/canvas-net/drawing.md");
            verificationFiles = ("docs/verification/canvas-net/drawing.md");
            requirementFiles = ("docs/reqstream/canvas-net/drawing.yaml");
        }

        part pathFiller : PathFiller;
        part pathStroker : PathStroker;
        part gradientPaint : GradientPaint;
        part tilePaint : TilePaint;
    }
}
