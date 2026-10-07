namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio LocPin

/// <summary>
///     A shape's resolved text-box placement, in the same shape-local coordinate box as
///     <see cref="VsdxShapeTransform"/> (origin bottom-left, inches) - either the implicit
///     default (the shape's own geometry bounding box, format reference §8.3) or an explicit
///     <c>TxtPinX</c>/<c>TxtPinY</c>/<c>TxtWidth</c>/<c>TxtHeight</c>/<c>TxtAngle</c> cell-set
///     override, resolved by <c>VsdxDocument.TextBox.cs</c>'s <c>BuildTextBox</c>.
/// </summary>
/// <remarks>
///     Deliberately stores every field as <see cref="double"/>, mirroring
///     <see cref="VsdxShapeTransform"/>'s own documented precision rationale - no
///     <see cref="System.Numerics.Matrix3x2"/>/page-space composition is applied to this box this
///     milestone (deferred to Milestone 7, exactly as <see cref="VsdxShapeTransform"/> itself is
///     not yet applied to any geometry path).
/// </remarks>
/// <param name="TxtPinX">The text box's pin X position, in shape-local inches.</param>
/// <param name="TxtPinY">The text box's pin Y position, in shape-local inches.</param>
/// <param name="TxtWidth">The text box's width, in inches.</param>
/// <param name="TxtHeight">The text box's height, in inches.</param>
/// <param name="TxtLocPinX">The text box's own local X coordinate of its pin/rotation-center, in inches.</param>
/// <param name="TxtLocPinY">The text box's own local Y coordinate of its pin/rotation-center, in inches.</param>
/// <param name="TxtAngle">The text box's own rotation relative to the shape, in radians, counter-clockwise.</param>
internal sealed record VsdxTextBoxTransform(
    double TxtPinX,
    double TxtPinY,
    double TxtWidth,
    double TxtHeight,
    double TxtLocPinX,
    double TxtLocPinY,
    double TxtAngle);
