// cspell:ignore circumradius
using DemaConsulting.CanvasNet.Canvas;
using static DemaConsulting.CanvasNet.Scad.Tests.ScadRenderTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Shared helpers of the <c>projection</c> tests: the area of a 2D result measured by drawing it straight from
///     above in flat colors, so every expected value is computed from the geometry and never from a reference image.
/// </summary>
internal static class ScadProjectionTestSupport
{
    /// <summary>
    ///     Draws a program from above over a known window and returns the covered area in model units squared.
    /// </summary>
    /// <param name="source">The program text.</param>
    /// <param name="centerX">The model X at the center of the picture.</param>
    /// <param name="centerY">The model Y at the center of the picture.</param>
    /// <param name="widthUnits">The width of the window in model units.</param>
    /// <param name="heightUnits">The height of the window in model units.</param>
    /// <param name="pixelsPerUnit">The number of pixels per model unit.</param>
    /// <returns>The number of covered pixels divided by the square of <paramref name="pixelsPerUnit"/>.</returns>
    public static double Area(string source, double centerX = 0, double centerY = 0, double widthUnits = 40, double heightUnits = 40, int pixelsPerUnit = 10)
    {
        var view = ScadOrthoView.Top(centerX, centerY, widthUnits, heightUnits, pixelsPerUnit);
        using var surface = view.Render(ScadDocument.Parse(source));
        return Count(surface, p => p != White) / (double)(pixelsPerUnit * pixelsPerUnit);
    }

    /// <summary>
    ///     Draws a program from above and reports whether the picture covers a model point.
    /// </summary>
    /// <param name="source">The program text.</param>
    /// <param name="points">The model points to test.</param>
    /// <param name="centerX">The model X at the center of the picture.</param>
    /// <param name="centerY">The model Y at the center of the picture.</param>
    /// <param name="widthUnits">The width of the window in model units.</param>
    /// <param name="heightUnits">The height of the window in model units.</param>
    /// <param name="pixelsPerUnit">The number of pixels per model unit.</param>
    /// <returns>For every point, whether it is covered.</returns>
    public static bool[] Covers(
        string source,
        (double X, double Y)[] points,
        double centerX = 0,
        double centerY = 0,
        double widthUnits = 40,
        double heightUnits = 40,
        int pixelsPerUnit = 10)
    {
        var view = ScadOrthoView.Top(centerX, centerY, widthUnits, heightUnits, pixelsPerUnit);
        using var surface = view.Render(ScadDocument.Parse(source));
        return [.. points.Select(p => At(view, surface, p.X, p.Y) != White)];
    }

    /// <summary>
    ///     Gets the area of a regular polygon from the number of its sides and its circumradius.
    /// </summary>
    /// <param name="radius">The circumradius.</param>
    /// <param name="sides">The number of sides.</param>
    /// <returns>The area.</returns>
    public static double PolygonArea(double radius, int sides) => 0.5 * sides * radius * radius * Math.Sin(2.0 * Math.PI / sides);

    /// <summary>
    ///     Evaluates and builds a program and returns the warning texts and feature identifiers of the build.
    /// </summary>
    /// <param name="source">The program text.</param>
    /// <returns>The build result.</returns>
    public static ScadBuildResult BuildSource(string source) => ScadGeometryTestSupport.Build(source);
}
