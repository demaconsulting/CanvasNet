// Portions derived from PlantUML (MIT); see third-party/README.md
namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <summary>
///     Simplifies the path of merged arrows as PlantUML's <c>Worm.mergeMe</c> does: removes empty and collinear
///     segments, back-and-forth segments, and the detours of a few known patterns.
/// </summary>
/// <remarks>
///     The class is stateless and safe for concurrent use.
/// </remarks>
internal static class WormSimplifier
{
    /// <summary>
    ///     Simplifies a path in place.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="full">Whether the last pattern applies too (a fully mergeable arrow).</param>
    public static void Simplify(List<(double X, double Y)> points, bool full)
    {
        ArgumentNullException.ThrowIfNull(points);
        bool changed;
        do
        {
            changed = RemoveNullVector(points) || RemoveRedundantDirection(points) || Pattern1(points) || Pattern2(points) ||
                      Pattern3(points) || Pattern4(points) || Pattern5(points) || Pattern6(points) || Pattern7(points) ||
                      (full && Pattern8(points));
        }
        while (changed);
    }

    /// <summary>
    ///     Gets the direction of the segment that starts at a point.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="i">The index of the point.</param>
    /// <returns>The direction.</returns>
    private static ArrowDirection At(List<(double X, double Y)> points, int i) => Snake.DirectionOf(points[i], points[i + 1]);

    /// <summary>
    ///     Checks the directions of four segments from a point.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="i">The index of the first point.</param>
    /// <param name="pattern">The four directions.</param>
    /// <returns>True when the segments have them.</returns>
    private static bool Is(List<(double X, double Y)> points, int i, params ArrowDirection[] pattern)
    {
        for (var k = 0; k < pattern.Length; k++)
        {
            if (At(points, i + k) != pattern[k])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Replaces three points after an index with one.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="i">The index before them.</param>
    /// <param name="point">The new point.</param>
    private static void ReplaceThree(List<(double X, double Y)> points, int i, (double X, double Y) point)
    {
        points.RemoveRange(i + 1, 3);
        points.Insert(i + 1, point);
    }

    /// <summary>
    ///     Removes a segment of length zero.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>True when one was removed.</returns>
    private static bool RemoveNullVector(List<(double X, double Y)> points)
    {
        for (var i = 0; i < points.Count - 1; i++)
        {
            if (At(points, i) == ArrowDirection.None)
            {
                points.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Removes the point between two segments of the same direction.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>True when one was removed.</returns>
    private static bool RemoveRedundantDirection(List<(double X, double Y)> points)
    {
        for (var i = 0; i < points.Count - 2; i++)
        {
            if (At(points, i) == At(points, i + 1))
            {
                points.RemoveAt(i + 1);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Straightens a down-left-down-right (or down-right-down-left) detour.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>True when the path changed.</returns>
    private static bool Pattern1(List<(double X, double Y)> points)
    {
        for (var i = 0; i < points.Count - 5; i++)
        {
            if (Is(points, i, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Down, ArrowDirection.Right) ||
                Is(points, i, ArrowDirection.Down, ArrowDirection.Right, ArrowDirection.Down, ArrowDirection.Left))
            {
                ReplaceThree(points, i, (points[i + 1].X, points[i + 3].Y));
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Straightens a right-down-right-up (or left-down-left-up) detour.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>True when the path changed.</returns>
    private static bool Pattern2(List<(double X, double Y)> points)
    {
        for (var i = 0; i < points.Count - 5; i++)
        {
            if (Is(points, i, ArrowDirection.Right, ArrowDirection.Down, ArrowDirection.Right, ArrowDirection.Up) ||
                Is(points, i, ArrowDirection.Left, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Up))
            {
                ReplaceThree(points, i, (points[i + 3].X, points[i + 1].Y));
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Straightens a down-right-down-right (or down-left-down-left) staircase.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>True when the path changed.</returns>
    private static bool Pattern3(List<(double X, double Y)> points)
    {
        for (var i = 0; i < points.Count - 4; i++)
        {
            if (Is(points, i, ArrowDirection.Down, ArrowDirection.Right, ArrowDirection.Down, ArrowDirection.Right) ||
                Is(points, i, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Down, ArrowDirection.Left))
            {
                ReplaceThree(points, i, (points[i + 1].X, points[i + 3].Y));
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Straightens a final down-left-down-right detour that ends right of where it turned.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>True when the path changed.</returns>
    private static bool Pattern4(List<(double X, double Y)> points)
    {
        var i = points.Count - 5;
        if (i >= 0 && Is(points, i, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Down, ArrowDirection.Right) &&
            points[i + 4].X > points[i + 1].X)
        {
            ReplaceThree(points, i, (points[i + 1].X, points[i + 3].Y));
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Straightens a final down-right-down-left detour that ends well left of where it turned.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>True when the path changed.</returns>
    private static bool Pattern5(List<(double X, double Y)> points)
    {
        var i = points.Count - 5;
        if (i >= 0 && Is(points, i, ArrowDirection.Down, ArrowDirection.Right, ArrowDirection.Down, ArrowDirection.Left) &&
            points[i + 4].X + 4 < points[i + 1].X)
        {
            ReplaceThree(points, i, (points[i + 1].X, points[i + 3].Y));
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Removes a segment that goes back over the previous one.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>True when the path changed.</returns>
    private static bool Pattern6(List<(double X, double Y)> points)
    {
        for (var i = 0; i < points.Count - 2; i++)
        {
            if (Inverse(At(points, i)) == At(points, i + 1))
            {
                points.RemoveAt(i + 1);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Straightens a starting right-down-left-down detour that ends right of where it started.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>True when the path changed.</returns>
    private static bool Pattern7(List<(double X, double Y)> points)
    {
        if (points.Count > 4 && Is(points, 0, ArrowDirection.Right, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Down) &&
            points[3].X > points[0].X)
        {
            var point = (points[3].X, points[0].Y);
            points.RemoveRange(1, 2);
            points.Insert(1, point);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Straightens a left-down-left-down (or right-down-right-down) staircase.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>True when the path changed.</returns>
    private static bool Pattern8(List<(double X, double Y)> points)
    {
        for (var i = 0; i < points.Count - 4; i++)
        {
            if (Is(points, i, ArrowDirection.Left, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Down) ||
                Is(points, i, ArrowDirection.Right, ArrowDirection.Down, ArrowDirection.Right, ArrowDirection.Down))
            {
                ReplaceThree(points, i, (points[i + 3].X, points[i + 1].Y));
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Gets the opposite direction.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <returns>The opposite.</returns>
    private static ArrowDirection Inverse(ArrowDirection direction) => direction switch
    {
        ArrowDirection.Right => ArrowDirection.Left,
        ArrowDirection.Left => ArrowDirection.Right,
        ArrowDirection.Up => ArrowDirection.Down,
        ArrowDirection.Down => ArrowDirection.Up,
        _ => ArrowDirection.None,
    };
}
