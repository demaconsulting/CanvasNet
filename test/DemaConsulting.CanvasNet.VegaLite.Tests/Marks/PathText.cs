using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Spells the segments of a scene path as the text of an SVG path (<c>M0,200 L150,0 Z</c>), so that the tests compare a hand-derived outline with one string.
/// </summary>
internal static class PathText
{
    /// <summary>
    ///     Spells the segments of a path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The commands separated by a space, with the numbers rounded to six decimals.</returns>
    public static string Of(ScenePath path)
    {
        var text = new StringBuilder();
        foreach (var segment in path.Segments)
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            switch (segment.Kind)
            {
                case ScenePathSegmentKind.MoveTo:
                    text.Append('M').Append(N(segment.X)).Append(',').Append(N(segment.Y));
                    break;
                case ScenePathSegmentKind.LineTo:
                    text.Append('L').Append(N(segment.X)).Append(',').Append(N(segment.Y));
                    break;
                case ScenePathSegmentKind.CubicTo:
                    text.Append('C').Append(N(segment.X1)).Append(',').Append(N(segment.Y1)).Append(' ')
                        .Append(N(segment.X2)).Append(',').Append(N(segment.Y2)).Append(' ')
                        .Append(N(segment.X)).Append(',').Append(N(segment.Y));
                    break;
                default:
                    text.Append('Z');
                    break;
            }
        }

        return text.ToString();
    }

    /// <summary>
    ///     Formats a number with at most six decimals and the invariant culture (negative zero is zero).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string N(double value)
    {
        var rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
        return (rounded == 0 ? 0 : rounded).ToString("0.######", CultureInfo.InvariantCulture);
    }
}
