namespace DemaConsulting.CanvasNet.Geometry;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Geometry"/> namespace provides resolution-independent vector
///     geometry building blocks: an axis-aligned bounding box (<see cref="Rect"/>), an immutable
///     vector path representation with a fluent builder (<see cref="Path"/> and
///     <see cref="PathBuilder"/>), adaptive Bezier curve flattening (<see cref="BezierFlattening"/>),
///     and SVG elliptical-arc-to-Bezier conversion (<see cref="SvgArcConverter"/>). This namespace
///     deliberately reuses <see cref="System.Numerics.Vector2"/> and <see cref="System.Numerics.Matrix3x2"/>
///     directly for points, vectors, and transforms, rather than inventing custom wrapper types,
///     since both are already available in-box on every target framework. These types are pure,
///     allocation-conscious building blocks with no dependency on any rasterizer or pixel format;
///     future drawing/rendering functionality will consume this namespace, not the other way around.
/// </summary>
/// <remarks>
///     See <see cref="PathBuilder"/> to build a <see cref="Path"/> and <see cref="Path"/> for its bounds and transforms
///     (it also has rectangle, rounded-rectangle, and circle factories). To paint or stroke a path, see
///     <see cref="DemaConsulting.CanvasNet.Drawing"/> (<see cref="DemaConsulting.CanvasNet.Drawing.PathFiller"/>) or the
///     transform-aware <see cref="DemaConsulting.CanvasNet.Rendering"/> helpers; glyph outlines come from
///     <see cref="DemaConsulting.CanvasNet.Fonts"/> as paths too.
/// </remarks>
/// <example>
///     Build a closed shape with a curved side and measure it:
///     <code language="csharp">
///     using System.Numerics;
///     using DemaConsulting.CanvasNet.Geometry;
///
///     var path = new PathBuilder()
///         .MoveTo(new Vector2(0, 0))
///         .LineTo(new Vector2(40, 0))
///         .CubicBezierTo(new Vector2(60, 0), new Vector2(60, 40), new Vector2(40, 40))
///         .Close()
///         .Build();
///     var bounds = path.GetBounds();
///     Console.WriteLine($"{bounds.Width} x {bounds.Height}");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
