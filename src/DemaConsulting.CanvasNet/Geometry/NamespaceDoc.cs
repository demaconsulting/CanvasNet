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
internal static class NamespaceDoc
{
}
