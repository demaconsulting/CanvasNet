// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.GitGraph;

/// <content>
///     The arrows: Mermaid's <c>drawArrows</c>, <c>drawArrow</c>, <c>shouldRerouteArrow</c> and <c>findLane</c>.
/// </content>
internal sealed partial class GitGraphRenderer
{
    /// <summary>The most tries of the lane search before it takes the last candidate.</summary>
    private const int MaxLaneDepth = 5;

    /// <summary>The space a lane keeps from the lanes before it.</summary>
    private const double LaneSpacing = 10;

    /// <summary>
    ///     Draws the arrows (Mermaid's <c>drawArrows</c>): for each commit in the order of the commit map, an arrow from
    ///     each parent, between the places of the first pass.
    /// </summary>
    /// <returns>The group of the arrows.</returns>
    private SceneGroup DrawArrows()
    {
        var items = new List<SceneItem>();
        foreach (var commit in _db.Commits)
        {
            foreach (var parent in commit.Parents)
            {
                items.Add(DrawArrow(_db.Find(parent)!, commit));
            }
        }

        return new SceneGroup(0, 0, items) { Role = "git-arrows", Location = _diagram.KeywordLocation };
    }

    /// <summary>
    ///     Draws the arrow from a parent to a commit (Mermaid's <c>drawArrow</c>): a rerouted arrow runs on a lane with
    ///     bends of radius 10; any other bends once with radius 20 (at the commit's end, or at the parent's end for a
    ///     merge's second parent), or is straight.
    /// </summary>
    /// <param name="a">The parent.</param>
    /// <param name="b">The commit.</param>
    /// <returns>The arrow.</returns>
    private ScenePath DrawArrow(GitCommit a, GitCommit b)
    {
        _meter.Charge(1 + _db.Commits.Count);
        var p1 = _commitPos[a.Id];
        var p2 = _commitPos[b.Id];
        var color = _branchPos[b.Branch].Index;
        var mergeFromSecond = b.Type == GitCommit.Merge && a.Id != b.Parents[0];
        if (mergeFromSecond)
        {
            color = _branchPos[a.Branch].Index;
        }

        SceneSegment[] segments;
        if (ShouldReroute(a, b, p1, p2))
        {
            const double R = 10;
            var lineY = p1.Y < p2.Y ? FindLane(p1.Y, p2.Y) : FindLane(p2.Y, p1.Y);
            var lineX = p1.X < p2.X ? FindLane(p1.X, p2.X) : FindLane(p2.X, p1.X);
            var furthest = _vertical ? p1.X < p2.X : p1.Y < p2.Y;
            if (!furthest)
            {
                color = _branchPos[a.Branch].Index;
            }

            segments = (_vertical, _bottomToTop, furthest) switch
            {
                (true, false, true) => Path(p1, (lineX - R, p1.Y), (R, true, lineX, p1.Y + R), (lineX, p2.Y - R), (R, false, lineX + R, p2.Y), p2),
                (true, false, false) => Path(p1, (lineX + R, p1.Y), (R, false, lineX, p1.Y + R), (lineX, p2.Y - R), (R, true, lineX - R, p2.Y), p2),
                (true, true, true) => Path(p1, (lineX - R, p1.Y), (R, false, lineX, p1.Y - R), (lineX, p2.Y + R), (R, true, lineX + R, p2.Y), p2),
                (true, true, false) => Path(p1, (lineX + R, p1.Y), (R, true, lineX, p1.Y - R), (lineX, p2.Y + R), (R, false, lineX - R, p2.Y), p2),
                (false, _, true) => Path(p1, (p1.X, lineY - R), (R, false, p1.X + R, lineY), (p2.X - R, lineY), (R, true, p2.X, lineY + R), p2),
                _ => Path(p1, (p1.X, lineY + R), (R, true, p1.X + R, lineY), (p2.X - R, lineY), (R, false, p2.X, lineY - R), p2),
            };
        }
        else
        {
            segments = _vertical ? VerticalArrow(p1, p2, mergeFromSecond) : HorizontalArrow(p1, p2, mergeFromSecond);
        }

        var style = _styles.Compute([SvgElement, GitGraphElement.Of("g", "commit-arrows"), GitGraphElement.Of("path", "arrow arrow" + (color % ColorLimit).ToString(CultureInfo.InvariantCulture))]);
        var path = Shape(segments, style, true, "git-arrow", $"{a.Id}->{b.Id}", b.Location);
        if (Scene.BoundsOf([path]) is { } box)
        {
            Include(box);
        }

        return path;
    }

    /// <summary>
    ///     Gets an arrow that is not rerouted, top to bottom or bottom to top: one bend of radius 20 toward the other
    ///     branch, or a straight line on one branch.
    /// </summary>
    /// <param name="p1">The parent's place.</param>
    /// <param name="p2">The commit's place.</param>
    /// <param name="mergeFromSecond">Whether the arrow comes from a merge's second parent (it bends at the parent's end).</param>
    /// <returns>The segments.</returns>
    private SceneSegment[] VerticalArrow((double X, double Y) p1, (double X, double Y) p2, bool mergeFromSecond)
    {
        const double R = 20;
        var sign = _bottomToTop ? -1 : 1;
        if (p1.X < p2.X)
        {
            return mergeFromSecond
                ? Bend(p1, (p1.X, p2.Y - (sign * R)), (R, _bottomToTop, p1.X + R, p2.Y), p2)
                : Bend(p1, (p2.X - R, p1.Y), (R, !_bottomToTop, p2.X, p1.Y + (sign * R)), p2);
        }

        if (p1.X > p2.X)
        {
            return mergeFromSecond
                ? Bend(p1, (p1.X, p2.Y - (sign * R)), (R, !_bottomToTop, p1.X - R, p2.Y), p2)
                : Bend(p1, (p2.X + R, p1.Y), (R, _bottomToTop, p2.X, p1.Y + (sign * R)), p2);
        }

        return [SceneSegment.Move(p1.X, p1.Y), SceneSegment.Line(p2.X, p2.Y)];
    }

    /// <summary>
    ///     Gets an arrow that is not rerouted, left to right: one bend of radius 20 toward the other branch, or a straight
    ///     line on one branch.
    /// </summary>
    /// <param name="p1">The parent's place.</param>
    /// <param name="p2">The commit's place.</param>
    /// <param name="mergeFromSecond">Whether the arrow comes from a merge's second parent (it bends at the commit's end).</param>
    /// <returns>The segments.</returns>
    private static SceneSegment[] HorizontalArrow((double X, double Y) p1, (double X, double Y) p2, bool mergeFromSecond)
    {
        const double R = 20;
        if (p1.Y < p2.Y)
        {
            return mergeFromSecond
                ? Bend(p1, (p2.X - R, p1.Y), (R, true, p2.X, p1.Y + R), p2)
                : Bend(p1, (p1.X, p2.Y - R), (R, false, p1.X + R, p2.Y), p2);
        }

        if (p1.Y > p2.Y)
        {
            return mergeFromSecond
                ? Bend(p1, (p2.X - R, p1.Y), (R, false, p2.X, p1.Y - R), p2)
                : Bend(p1, (p1.X, p2.Y + R), (R, true, p1.X + R, p2.Y), p2);
        }

        return [SceneSegment.Move(p1.X, p1.Y), SceneSegment.Line(p2.X, p2.Y)];
    }

    /// <summary>
    ///     Gets the path <c>M p1 L a A r r, 0, 0, sweep, end L p2</c>.
    /// </summary>
    /// <param name="p1">The start.</param>
    /// <param name="a">The end of the first line.</param>
    /// <param name="arc">The radius, the sweep flag and the end of the arc.</param>
    /// <param name="p2">The end.</param>
    /// <returns>The segments.</returns>
    private static SceneSegment[] Bend((double X, double Y) p1, (double X, double Y) a, (double R, bool Sweep, double X, double Y) arc, (double X, double Y) p2) =>
    [
        SceneSegment.Move(p1.X, p1.Y),
        SceneSegment.Line(a.X, a.Y),
        SceneSegment.Arc(arc.R, arc.R, 0, false, arc.Sweep, arc.X, arc.Y),
        SceneSegment.Line(p2.X, p2.Y),
    ];

    /// <summary>
    ///     Gets the path of a rerouted arrow: <c>M p1 L a A ... L b A ... L p2</c>.
    /// </summary>
    /// <param name="p1">The start.</param>
    /// <param name="a">The end of the first line.</param>
    /// <param name="arc1">The radius, sweep flag and end of the first arc.</param>
    /// <param name="b">The end of the second line.</param>
    /// <param name="arc2">The radius, sweep flag and end of the second arc.</param>
    /// <param name="p2">The end.</param>
    /// <returns>The segments.</returns>
    private static SceneSegment[] Path(
        (double X, double Y) p1,
        (double X, double Y) a,
        (double R, bool Sweep, double X, double Y) arc1,
        (double X, double Y) b,
        (double R, bool Sweep, double X, double Y) arc2,
        (double X, double Y) p2) =>
    [
        SceneSegment.Move(p1.X, p1.Y),
        SceneSegment.Line(a.X, a.Y),
        SceneSegment.Arc(arc1.R, arc1.R, 0, false, arc1.Sweep, arc1.X, arc1.Y),
        SceneSegment.Line(b.X, b.Y),
        SceneSegment.Arc(arc2.R, arc2.R, 0, false, arc2.Sweep, arc2.X, arc2.Y),
        SceneSegment.Line(p2.X, p2.Y),
    ];

    /// <summary>
    ///     Mermaid's <c>shouldRerouteArrow</c>: whether a commit made between the parent and the commit lies on the branch
    ///     the arrow bends to (the commit's branch when the commit is further out, else the parent's).
    /// </summary>
    /// <param name="a">The parent.</param>
    /// <param name="b">The commit.</param>
    /// <param name="p1">The parent's place.</param>
    /// <param name="p2">The commit's place.</param>
    /// <returns>Whether the arrow is rerouted.</returns>
    private bool ShouldReroute(GitCommit a, GitCommit b, (double X, double Y) p1, (double X, double Y) p2)
    {
        var furthest = _vertical ? p1.X < p2.X : p1.Y < p2.Y;
        var branch = furthest ? b.Branch : a.Branch;
        return _db.Commits.Any(x => x.Seq > a.Seq && x.Seq < b.Seq && x.Branch == branch);
    }

    /// <summary>
    ///     Mermaid's <c>findLane</c>: the middle between two coordinates when it is at least 10 pixels from every lane
    ///     taken (it is then taken), else the middle with the far end moved a fifth closer, up to five times; the sixth
    ///     candidate is used without being taken.
    /// </summary>
    /// <param name="y1">The near end.</param>
    /// <param name="y2">The far end.</param>
    /// <returns>The lane.</returns>
    private double FindLane(double y1, double y2)
    {
        var depth = 0;
        while (true)
        {
            var candidate = y1 + (Math.Abs(y1 - y2) / 2);
            if (depth > MaxLaneDepth)
            {
                return candidate;
            }

            _meter.Charge(1 + _lanes.Count);
            if (_lanes.TrueForAll(lane => Math.Abs(lane - candidate) >= LaneSpacing))
            {
                _lanes.Add(candidate);
                return candidate;
            }

            y2 -= Math.Abs(y1 - y2) / 5;
            depth++;
        }
    }
}
