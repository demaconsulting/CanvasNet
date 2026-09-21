using System.Numerics;

namespace DemaConsulting.CanvasNet.Drawing;

// cspell:ignore Lerp

/// <summary>
///     Splits a flattened polyline into the visible "on" segments of a dash pattern.
/// </summary>
/// <remarks>
///     The splitter follows SVG dash semantics closely enough for stroke-to-fill conversion:
///     odd-length arrays are conceptually duplicated to make an even on/off cycle, the dash
///     offset phase-shifts the pattern along the path, and a closed polyline whose visible dash
///     wraps across the seam is stitched back into one contiguous emitted segment.
/// </remarks>
internal static class DashSplitter
{
    /// <summary>
    ///     Splits <paramref name="points"/> into the visible "on" dash segments described by
    ///     <paramref name="dashArray"/> and <paramref name="dashOffset"/>.
    /// </summary>
    /// <param name="points">The polyline vertices.</param>
    /// <param name="isClosed">Whether the polyline represents a closed contour.</param>
    /// <param name="dashArray">
    ///     Alternating on/off dash lengths. A <see langword="null"/>, empty, or all-zero array
    ///     is treated as a solid stroke.
    /// </param>
    /// <param name="dashOffset">The distance into the dash pattern at which the path starts.</param>
    /// <returns>Only the visible "on" segments, each preserving whether it remains closed.</returns>
    internal static List<(List<Vector2> Points, bool IsClosed)> Split(
        List<Vector2> points,
        bool isClosed,
        IReadOnlyList<float>? dashArray,
        float dashOffset)
    {
        if (points.Count == 0)
        {
            return [];
        }

        if (IsSolidDash(dashArray))
        {
            return [(new List<Vector2>(points), isClosed)];
        }

        var pattern = NormalizeDashArray(dashArray!);
        var totalPatternLength = GetPatternLength(pattern);
        if (totalPatternLength <= 0f)
        {
            return [(new List<Vector2>(points), isClosed)];
        }

        var edgeCount = isClosed ? points.Count : points.Count - 1;
        if (edgeCount <= 0)
        {
            return IsDashOnAtStart(pattern, dashOffset) ? [(new List<Vector2>(points), isClosed)] : [];
        }

        var cumulativeLengths = BuildCumulativeLengths(points, isClosed);
        var totalLength = cumulativeLengths[^1];
        if (totalLength <= 0f)
        {
            return IsDashOnAtStart(pattern, dashOffset) ? [(new List<Vector2>(points), isClosed)] : [];
        }

        var onIntervals = BuildOnIntervals(pattern, dashOffset, totalLength, isClosed, out var encounteredPositiveOffSpan);
        if (onIntervals.Count == 0)
        {
            return [];
        }

        if (isClosed && !encounteredPositiveOffSpan && onIntervals.Count == 1)
        {
            return [(new List<Vector2>(points), true)];
        }

        var segments = new List<(List<Vector2> Points, bool IsClosed)>(onIntervals.Count);
        foreach (var interval in onIntervals)
        {
            var segment = ExtractIntervalPolyline(points, isClosed, cumulativeLengths, interval.Start, interval.End);
            if (segment.Count != 0)
            {
                segments.Add((segment, false));
            }
        }

        if (isClosed
            && segments.Count > 1
            && IsNearZero(onIntervals[0].Start)
            && IsNearZero(totalLength - onIntervals[^1].End))
        {
            var stitched = new List<Vector2>(segments[^1].Points);
            foreach (var point in segments[0].Points.Skip(1))
            {
                AddPointIfDistinct(stitched, point);
            }

            segments[^1] = (stitched, false);
            segments.RemoveAt(0);
        }

        return segments;
    }

    /// <summary>
    ///     Determines whether <paramref name="dashArray"/> should be treated as a solid stroke.
    /// </summary>
    private static bool IsSolidDash(IReadOnlyList<float>? dashArray)
    {
        if (dashArray == null || dashArray.Count == 0)
        {
            return true;
        }

        for (var i = 0; i < dashArray.Count; i++)
        {
            var value = dashArray[i];
            if (value != 0f)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Duplicates an odd-length dash array to match SVG's alternating on/off semantics.
    /// </summary>
    private static float[] NormalizeDashArray(IReadOnlyList<float> dashArray)
    {
        if (dashArray.Count % 2 == 0)
        {
            var even = new float[dashArray.Count];
            for (var i = 0; i < dashArray.Count; i++)
            {
                even[i] = dashArray[i];
            }

            return even;
        }

        var duplicated = new float[dashArray.Count * 2];
        for (var i = 0; i < duplicated.Length; i++)
        {
            duplicated[i] = dashArray[i % dashArray.Count];
        }

        return duplicated;
    }

    /// <summary>
    ///     Computes the total pattern length.
    /// </summary>
    private static float GetPatternLength(IReadOnlyList<float> pattern)
    {
        var total = 0f;
        foreach (var value in pattern)
        {
            total += value;
        }

        return total;
    }

    /// <summary>
    ///     Computes one cumulative path-length entry per vertex boundary, including the seam edge
    ///     for a closed polyline.
    /// </summary>
    private static float[] BuildCumulativeLengths(IReadOnlyList<Vector2> points, bool isClosed)
    {
        var edgeCount = isClosed ? points.Count : points.Count - 1;
        var cumulative = new float[edgeCount + 1];
        for (var i = 0; i < edgeCount; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % points.Count];
            cumulative[i + 1] = cumulative[i] + Vector2.Distance(start, end);
        }

        return cumulative;
    }

    /// <summary>
    ///     Determines whether the dash phase begins in an "on" interval.
    /// </summary>
    private static bool IsDashOnAtStart(IReadOnlyList<float> pattern, float dashOffset)
    {
        var patternLength = GetPatternLength(pattern);
        var offset = NormalizeModulo(dashOffset, patternLength);
        var index = 0;
        while (offset > 0f)
        {
            var length = pattern[index];
            if (length > 0f && offset < length)
            {
                break;
            }

            offset -= length;
            index = (index + 1) % pattern.Count;
        }

        return index % 2 == 0;
    }

    /// <summary>
    ///     Produces the path-length intervals where the dash pattern is "on".
    /// </summary>
    private static List<(float Start, float End)> BuildOnIntervals(
        IReadOnlyList<float> pattern,
        float dashOffset,
        float totalLength,
        bool isClosed,
        out bool encounteredPositiveOffSpan)
    {
        encounteredPositiveOffSpan = false;
        var intervals = new List<(float Start, float End)>();
        var patternLength = GetPatternLength(pattern);
        var offset = NormalizeModulo(dashOffset, patternLength);

        var dashIndex = 0;
        while (offset > 0f)
        {
            var length = pattern[dashIndex];
            if (length > 0f && offset < length)
            {
                break;
            }

            offset -= length;
            dashIndex = (dashIndex + 1) % pattern.Count;
        }

        var remainingInDash = pattern[dashIndex] - offset;
        if (remainingInDash < 0f)
        {
            remainingInDash = 0f;
        }

        var position = 0f;
        while (position < totalLength)
        {
            if (remainingInDash <= 0f)
            {
                AdvanceDash(pattern, ref dashIndex, ref remainingInDash);
                continue;
            }

            var span = MathF.Min(remainingInDash, totalLength - position);
            if (span <= 0f)
            {
                break;
            }

            if (dashIndex % 2 == 0)
            {
                intervals.Add((position, position + span));
            }
            else if (span > 0f)
            {
                encounteredPositiveOffSpan = true;
            }

            position += span;
            remainingInDash -= span;
        }

        if (!isClosed)
        {
            return intervals;
        }

        return intervals;
    }

    /// <summary>
    ///     Advances to the next positive-length dash entry.
    /// </summary>
    private static void AdvanceDash(IReadOnlyList<float> pattern, ref int dashIndex, ref float remainingInDash)
    {
        var traversed = 0;
        do
        {
            dashIndex = (dashIndex + 1) % pattern.Count;
            remainingInDash = pattern[dashIndex];
            traversed++;
        }
        while (remainingInDash <= 0f && traversed <= pattern.Count);
    }

    /// <summary>
    ///     Extracts the sub-polyline between <paramref name="startDistance"/> and
    ///     <paramref name="endDistance"/>.
    /// </summary>
    private static List<Vector2> ExtractIntervalPolyline(
        IReadOnlyList<Vector2> points,
        bool isClosed,
        IReadOnlyList<float> cumulativeLengths,
        float startDistance,
        float endDistance)
    {
        var segment = new List<Vector2>();
        AddPointIfDistinct(segment, GetPointAtDistance(points, isClosed, cumulativeLengths, startDistance));

        var edgeCount = cumulativeLengths.Count - 1;
        for (var i = 1; i < edgeCount; i++)
        {
            if (cumulativeLengths[i] > startDistance && cumulativeLengths[i] < endDistance)
            {
                AddPointIfDistinct(segment, points[i % points.Count]);
            }
        }

        AddPointIfDistinct(segment, GetPointAtDistance(points, isClosed, cumulativeLengths, endDistance));
        return segment;
    }

    /// <summary>
    ///     Evaluates the polyline point at the specified arc-length distance.
    /// </summary>
    private static Vector2 GetPointAtDistance(
        IReadOnlyList<Vector2> points,
        bool isClosed,
        IReadOnlyList<float> cumulativeLengths,
        float distance)
    {
        if (distance <= 0f)
        {
            return points[0];
        }

        var totalLength = cumulativeLengths[^1];
        if (distance >= totalLength)
        {
            return isClosed ? points[0] : points[^1];
        }

        var edgeCount = cumulativeLengths.Count - 1;
        for (var i = 0; i < edgeCount; i++)
        {
            var startLength = cumulativeLengths[i];
            var endLength = cumulativeLengths[i + 1];
            if (distance > endLength)
            {
                continue;
            }

            var edgeStart = points[i];
            var edgeEnd = points[(i + 1) % points.Count];
            var edgeLength = endLength - startLength;
            if (edgeLength <= 0f)
            {
                return edgeStart;
            }

            var t = (distance - startLength) / edgeLength;
            return Vector2.Lerp(edgeStart, edgeEnd, t);
        }

        return isClosed ? points[0] : points[^1];
    }

    /// <summary>
    ///     Normalizes <paramref name="value"/> into the half-open interval
    ///     <c>[0, modulus)</c>.
    /// </summary>
    private static float NormalizeModulo(float value, float modulus)
    {
        var normalized = value % modulus;
        if (normalized < 0f)
        {
            normalized += modulus;
        }

        return normalized;
    }

    /// <summary>
    ///     Appends <paramref name="point"/> unless it duplicates the current final point.
    /// </summary>
    private static void AddPointIfDistinct(List<Vector2> points, Vector2 point)
    {
        if (points.Count == 0 || points[^1] != point)
        {
            points.Add(point);
        }
    }

    /// <summary>
    ///     Determines whether <paramref name="value"/> is close enough to zero to be treated as a
    ///     seam-aligned dash boundary.
    /// </summary>
    private static bool IsNearZero(float value) => MathF.Abs(value) <= 1e-5f;
}
