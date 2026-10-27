using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     A generated picture of a sketch for the tests of the photo tools.
/// </summary>
internal static partial class FixtureFactory
{
    /// <summary>
    ///     Creates the picture of a sheet of paper with tilted dark lines, like a photographed page of ruled notes.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="tiltDegrees">
    ///     The tilt of the lines in degrees; positive means the lines descend to the right (they are rotated clockwise).
    /// </param>
    /// <param name="lines">The number of lines, evenly spaced from top to bottom.</param>
    /// <param name="thickness">The thickness of a line measured vertically, in pixels.</param>
    /// <param name="paperLevel">The gray level of the paper at the middle of the picture.</param>
    /// <param name="inkContrast">How many levels darker than the paper at that place the ink is.</param>
    /// <param name="shade">
    ///     The change of the paper level from the left edge to the right edge, as uneven lighting; the ink follows the
    ///     paper so the contrast of the lines stays the same.
    /// </param>
    /// <returns>An opaque surface. The lines are drawn with plain arithmetic, independent of any rotation code.</returns>
    public static Surface SketchPhoto(
        int width,
        int height,
        double tiltDegrees,
        int lines = 6,
        int thickness = 2,
        int paperLevel = 200,
        int inkContrast = 90,
        int shade = 0)
    {
        byte PaperAt(int x)
        {
            var level = paperLevel + (int)Math.Round(shade * ((x / (double)Math.Max(1, width - 1)) - 0.5));
            return (byte)Math.Clamp(level, 0, 255);
        }

        var surface = new Surface(width, height);
        for (var x = 0; x < width; x++)
        {
            var paper = PaperAt(x);
            for (var y = 0; y < height; y++)
            {
                surface[x, y] = new Rgba32(paper, paper, paper, 255);
            }
        }

        var slope = Math.Tan(tiltDegrees * Math.PI / 180.0);
        for (var line = 1; line <= lines; line++)
        {
            var baseline = height * line / (double)(lines + 1);
            for (var x = 0; x < width; x++)
            {
                var ink = (byte)Math.Max(0, PaperAt(x) - inkContrast);
                var top = (int)Math.Round(baseline + ((x - (width / 2.0)) * slope) - (thickness / 2.0));
                for (var t = 0; t < thickness; t++)
                {
                    var y = top + t;
                    if (y >= 0 && y < height)
                    {
                        surface[x, y] = new Rgba32(ink, ink, ink, 255);
                    }
                }
            }
        }

        return surface;
    }
}
