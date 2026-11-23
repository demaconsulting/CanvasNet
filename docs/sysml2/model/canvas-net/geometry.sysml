package CanvasNet_GeometrySubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNet_Rect::*;
    public import CanvasNet_Path::*;
    public import CanvasNet_BezierFlattening::*;
    public import CanvasNet_SvgArcConverter::*;
    public import CanvasNet_CornerRoundEffect::*;

    part def Geometry {
        doc /* Vector-geometry primitives subsystem: axis-aligned bounds (Rect), an immutable vector path and its builder (Path/PathBuilder, covering Subpath/PathCommand/PathCommandType inline), adaptive Bezier flattening (BezierFlattening), SVG-style elliptical arc conversion (SvgArcConverter), and path-level corner rounding (CornerRoundEffect, replacing polyline corners with tangent-radius circular arcs). Distinct from the Drawing subsystem, which rasterizes this geometry onto a Canvas.Surface. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/canvas-net/geometry.md");
            verificationFiles = ("docs/verification/canvas-net/geometry.md");
            requirementFiles = ("docs/reqstream/canvas-net/geometry.yaml");
        }

        part rect : Rect;
        part path : Path;
        part bezierFlattening : BezierFlattening;
        part svgArcConverter : SvgArcConverter;
        part cornerRoundEffect : CornerRoundEffect;
    }
}
