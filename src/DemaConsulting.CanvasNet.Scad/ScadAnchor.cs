using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     A declared anchor of a model, with its world point and its position in the rendered image, reported to
///     <see cref="ScadRenderOptions.AnchorHandler"/>.
/// </summary>
/// <remarks>
///     <para>
///         A model declares an anchor with the statement <c>echo("anchor", "name", [x, y, z]);</c>. The echo line is
///         still printed. The point is in the coordinate system at the statement: every enclosing
///         <c>translate</c>, <c>rotate</c>, <c>scale</c>, <c>mirror</c> and <c>multmatrix</c> is applied to it, so
///         <see cref="Point"/> is a world point. Callers use the pixel to place callouts and leader lines on the
///         rendered picture.
///     </para>
///     <para>
///         <see cref="Pixel"/> is projected with exactly the camera that draws the image (see
///         <see cref="DemaConsulting.CanvasNet.Render3D.SceneRendering.Camera.TryProject"/>): continuous coordinates
///         with the origin at the top-left corner of the image, pixel <c>i</c> spanning <c>[i, i + 1)</c>, x to the
///         right and y downward. No occlusion test is made: an anchor hidden behind geometry still has a pixel and is
///         still reported as inside the image. Several anchors with the same name are all reported, in echo order.
///     </para>
/// </remarks>
/// <param name="Name">The anchor name: the non-empty second argument of the echo.</param>
/// <param name="Point">The anchor point in world coordinates, after the enclosing transforms.</param>
/// <param name="Pixel">
///     The continuous pixel position of the point in the rendered image, or <see langword="null"/> when the point
///     cannot be projected (a perspective camera sees the point behind or at the eye). The position may lie outside the
///     image.
/// </param>
/// <param name="IsInsideImage">
///     Whether <paramref name="Pixel"/> is not <see langword="null"/> and lies within the image: <c>0 &lt;= x &lt; width</c>
///     and <c>0 &lt;= y &lt; height</c>.
/// </param>
/// <param name="Location">The location of the declaring <c>echo</c> statement.</param>
public sealed record ScadAnchor(string Name, Vector3D Point, Vector2D? Pixel, bool IsInsideImage, ScadSourceLocation Location);
