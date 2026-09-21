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
        if (totalPatternLength <= 0d)
        {
            return [(new List<Vector2>(points), isClosed)];
        }

        // A pattern length that is not finite (astronomically large dash entries summing beyond
        // double range) cannot be phase-normalized or traversed at all; fall back to a solid
        // stroke rather than risk any downstream arithmetic on a non-finite value.
        if (!double.IsFinite(totalPatternLength))
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

        // Both cursors only ever move forward across this loop: onIntervals is produced by a
        // single monotonic walk from 0 to totalLength in BuildOnIntervals, so successive
        // Start/End values never decrease. Threading a shared vertex cursor and point-lookup
        // cursor through every interval (instead of each call scanning again from the beginning)
        // keeps the whole extraction pass O(edgeCount + onIntervals.Count) rather than
        // O(edgeCount * onIntervals.Count).
        var vertexCursor = 0;
        var pointCursor = 0;
        foreach (var interval in onIntervals)
        {
            var segment = ExtractIntervalPolyline(
                points,
                isClosed,
                cumulativeLengths,
                interval.Start,
                interval.End,
                ref vertexCursor,
                ref pointCursor);
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
    /// <remarks>
    ///     Summed in <see langword="double"/> rather than <see langword="float"/>: every entry is
    ///     individually finite (enforced by <see cref="StrokeStyle"/>'s constructor validation),
    ///     but a legal pattern such as <c>[float.MaxValue, float.MaxValue]</c> would still overflow
    ///     a running <see langword="float"/> sum to <c>+Infinity</c>. An infinite pattern length
    ///     then breaks phase normalization (<see cref="LocatePhase"/>) and can turn the
    ///     phase-traversal loops in <see cref="IsDashOnAtStart"/> and <see cref="BuildOnIntervals"/>
    ///     into an infinite loop, because subtracting any finite dash entry from
    ///     <c>float.PositiveInfinity</c> never reduces it. Double has ample headroom for the sum of
    ///     any number of finite float32 values, so this keeps the total representable.
    /// </remarks>
    private static double GetPatternLength(IReadOnlyList<float> pattern)
    {
        var total = 0d;
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
        var (index, _) = LocatePhase(pattern, dashOffset, patternLength);
        return index % 2 == 0;
    }

    /// <summary>
    ///     Locates the dash entry containing the phase at <paramref name="dashOffset"/>, and how
    ///     much of that entry remains (in the forward-traversal direction) from that phase.
    /// </summary>
    /// <remarks>
    ///     A signed offset is resolved via the exact <c>%</c> remainder against
    ///     <paramref name="patternLength"/> (never a lossy addition of the offset to the modulus -
    ///     see the catastrophic-cancellation note on <see cref="LocatePhaseBackward"/>), then
    ///     dispatched to a forward walk (from the start of the pattern) for a non-negative
    ///     remainder or a backward walk (from the end of the pattern) for a negative one. Both
    ///     walks only ever compare the residual distance against individual, finite dash entries -
    ///     never against the (potentially astronomically large) total pattern length - so a tiny
    ///     offset is never lost against a huge pattern magnitude in either direction.
    /// </remarks>
    private static (int Index, float RemainingInDash) LocatePhase(
        IReadOnlyList<float> pattern,
        float dashOffset,
        double patternLength)
    {
        var offset = dashOffset % patternLength;
        return offset >= 0d
            ? LocatePhaseForward(pattern, offset)
            : LocatePhaseBackward(pattern, -offset);
    }

    /// <summary>
    ///     Walks forward from the start of the pattern to locate a non-negative phase offset.
    /// </summary>
    /// <remarks>
    ///     A zero-length dash entry occupies no visible extent along the path, so landing exactly
    ///     at its start - whether that is the initial phase (<paramref name="offset"/> is exactly
    ///     zero, which skips the traversal loop below entirely) or a phase reached after exactly
    ///     consuming every preceding entry - means the phase has already moved past it. The
    ///     trailing loop skips forward through any such zero-length entries so the returned index
    ///     always identifies the entry actually in effect at this phase, rather than a
    ///     zero-length entry the phase is only nominally "at."
    /// </remarks>
    private static (int Index, float RemainingInDash) LocatePhaseForward(IReadOnlyList<float> pattern, double offset)
    {
        var index = 0;
        while (offset > 0d)
        {
            var length = pattern[index];
            if (length > 0f && offset < length)
            {
                break;
            }

            offset -= length;
            index = (index + 1) % pattern.Count;
        }

        while (offset == 0d && pattern[index] == 0f)
        {
            index = (index + 1) % pattern.Count;
        }

        // By the loop invariant above, offset is now strictly less than pattern[index] (a finite
        // float32 entry), so the remainder is safe to narrow back to float without any risk of
        // the overflow this two-way split exists to avoid.
        var remaining = (float)(pattern[index] - offset);
        return (index, remaining < 0f ? 0f : remaining);
    }

    /// <summary>
    ///     Walks backward from the end of the pattern to locate a negative phase offset.
    /// </summary>
    /// <remarks>
    ///     A negative <c>dashOffset</c> paired with an astronomically large pattern length (e.g.
    ///     two <see cref="float.MaxValue"/> entries) cannot be resolved by normalizing into
    ///     <c>[0, patternLength)</c> via <c>normalized += patternLength</c>: double has roughly 16
    ///     significant decimal digits, so adding a small residual (say, <c>-1</c>) to a ~1e38
    ///     magnitude modulus rounds straight back to the modulus itself, silently discarding the
    ///     offset and landing the phase at the wrong (first "on") entry instead of the final unit
    ///     of the preceding "off" entry. Walking backward from the last pattern entry avoids this
    ///     entirely: <paramref name="distanceFromWrap"/> (the offset's magnitude) is only ever
    ///     compared against and subtracted from individual dash entries - never added to or
    ///     subtracted from the huge total pattern length - so a small negative offset against an
    ///     enormous pattern remains exactly representable throughout.
    /// </remarks>
    private static (int Index, float RemainingInDash) LocatePhaseBackward(
        IReadOnlyList<float> pattern,
        double distanceFromWrap)
    {
        var index = pattern.Count - 1;
        while (distanceFromWrap > 0d)
        {
            var length = pattern[index];
            if (length > 0f && distanceFromWrap <= length)
            {
                break;
            }

            distanceFromWrap -= length;
            index = (index - 1 + pattern.Count) % pattern.Count;
        }

        if (distanceFromWrap <= 0d)
        {
            // Landed exactly back on the wrap point (the start of the pattern); mirror the
            // forward walk's zero-skip so a leading zero-length "on" entry is not mistaken for
            // the active entry.
            index = 0;
            while (pattern[index] == 0f)
            {
                index = (index + 1) % pattern.Count;
            }

            return (index, pattern[index]);
        }

        // distanceFromWrap is now within (0, pattern[index]] - already small relative to the
        // individual entry it was measured against, never the huge total pattern length - so it
        // is safe to use directly as the remaining-in-dash distance.
        var remaining = (float)distanceFromWrap;
        return (index, remaining > pattern[index] ? pattern[index] : remaining);
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
        var (dashIndex, remainingInDash) = LocatePhase(pattern, dashOffset, patternLength);

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
    /// <remarks>
    ///     <paramref name="vertexCursor"/> and <paramref name="pointCursor"/> are threaded in from
    ///     the caller and advance monotonically across every interval extracted from the same
    ///     polyline (see the call site in <see cref="Split"/>): because successive intervals never
    ///     move backward along the path, each cursor only ever walks forward past a given vertex
    ///     or edge once across the whole extraction pass, making the combined cost linear in the
    ///     number of polyline edges plus the number of intervals rather than their product.
    /// </remarks>
    private static List<Vector2> ExtractIntervalPolyline(
        IReadOnlyList<Vector2> points,
        bool isClosed,
        IReadOnlyList<float> cumulativeLengths,
        float startDistance,
        float endDistance,
        ref int vertexCursor,
        ref int pointCursor)
    {
        var segment = new List<Vector2>();
        AddPointIfDistinct(segment, GetPointAtDistance(points, isClosed, cumulativeLengths, startDistance, ref pointCursor));

        var edgeCount = cumulativeLengths.Count - 1;

        // Advance past any vertex boundaries at or before startDistance (including the implicit
        // index 0 boundary, whose cumulative length is always zero).
        while (vertexCursor < edgeCount && cumulativeLengths[vertexCursor] <= startDistance)
        {
            vertexCursor++;
        }

        // Emit every vertex strictly between startDistance and endDistance, advancing the cursor
        // forward as each is consumed.
        while (vertexCursor < edgeCount && cumulativeLengths[vertexCursor] < endDistance)
        {
            AddPointIfDistinct(segment, points[vertexCursor % points.Count]);
            vertexCursor++;
        }

        AddPointIfDistinct(segment, GetPointAtDistance(points, isClosed, cumulativeLengths, endDistance, ref pointCursor));
        return segment;
    }

    /// <summary>
    ///     Evaluates the polyline point at the specified arc-length distance.
    /// </summary>
    /// <remarks>
    ///     <paramref name="edgeCursor"/> is threaded in from the caller and only ever advances
    ///     forward: since every call made across one <see cref="Split"/> pass supplies a
    ///     non-decreasing <paramref name="distance"/> (see <see cref="ExtractIntervalPolyline"/>),
    ///     resuming the edge-bracket search from wherever the previous call left off - rather than
    ///     restarting from edge zero - keeps the search across all calls linear in the number of
    ///     polyline edges instead of quadratic in edges times calls.
    /// </remarks>
    private static Vector2 GetPointAtDistance(
        IReadOnlyList<Vector2> points,
        bool isClosed,
        IReadOnlyList<float> cumulativeLengths,
        float distance,
        ref int edgeCursor)
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
        while (edgeCursor < edgeCount - 1 && distance > cumulativeLengths[edgeCursor + 1])
        {
            edgeCursor++;
        }

        var startLength = cumulativeLengths[edgeCursor];
        var endLength = cumulativeLengths[edgeCursor + 1];
        var edgeStart = points[edgeCursor];
        var edgeEnd = points[(edgeCursor + 1) % points.Count];
        var edgeLength = endLength - startLength;
        if (edgeLength <= 0f)
        {
            return edgeStart;
        }

        var t = (distance - startLength) / edgeLength;
        return Vector2.Lerp(edgeStart, edgeEnd, t);
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
