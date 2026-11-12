// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.GitGraph;

/// <summary>
///     The git graph renderer: a port of Mermaid's <c>gitGraphRenderer.ts</c> (<c>draw</c>, <c>drawCommits</c>,
///     <c>drawBranches</c>, <c>drawArrows</c> and their helpers) that turns a <see cref="GitGraphDiagram"/> into a scene,
///     with the paint of Mermaid's git graph style sheet (<see cref="GitGraphStyles"/>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Order.</b> As in Mermaid: the branch positions (each branch 50 pixels after the one before, 40 more when the
///         commit labels are rotated, and half its label's width more top to bottom or bottom to top); a first pass over
///         the commits that only places them; the branch lines and labels; the arrows, from the places of the first pass;
///         a second pass that places the commits again and draws their bullets, labels and tags; the title; and the view
///         box. The two passes share their state, as Mermaid's module state is shared: a pass reads the places the passes
///         before it left (this matters only for commits whose id was used again).
///     </para>
///     <para>
///         <b>Places.</b> A commit is 50 pixels after the one before it (in the order of their sequence numbers, reversed
///         bottom to top), 10 pixels from its start; with <c>parallelCommits</c> it is 40 pixels after its closest parent
///         (bottom to top, after the three loops of Mermaid's <c>setParallelBTPos</c>). An arrow that would cross a commit
///         of the branch it bends to is rerouted on a lane (Mermaid's <c>findLane</c>, which keeps lanes 10 pixels apart
///         and gives up after five tries).
///     </para>
///     <para>
///         <b>Texts.</b> A text is measured as the browser's <c>getBBox</c> measures it while Mermaid draws (the ink-extended
///         advance with Mermaid's entity placeholders, white space collapsed; a branch label's rows keep their spaces and
///         an empty row has no box and no <c>dy</c>), and drawn as it is shown (the placeholders decoded). Rotated commit
///         labels and tags are rotated texts and turned outlines; the box of a rotated text is the box of its turned box.
///     </para>
///     <para>
///         <b>View box.</b> The view box is the browser's bounding box of everything drawn, grown by <c>diagramPadding</c>
///         on every side: the box of each element (a path's geometry, a line even without area, a text's
///         <c>getBBox</c>, no stroke), mapped through its transform (the box of a turned group is the box of its children's
///         union, turned), empty groups and texts left out, in single precision as the browser keeps it.
///     </para>
///     <para>
///         <b>Cost.</b> The work meter (<see cref="MermaidLimits.MaxLayoutWork"/>) is charged for each branch, each commit
///         of each pass and each parent looked at, each arrow and the commits its rerouting test scans, each lane try and
///         the lanes it compares, and the length of each measured text; it checks cancellation.
///     </para>
///     <para>
///         An instance draws one render on one thread.
///     </para>
/// </remarks>
internal sealed partial class GitGraphRenderer
{
    /// <summary>The offset of a commit from its place (<c>LAYOUT_OFFSET</c>).</summary>
    private const double LayoutOffset = 10;

    /// <summary>The step between a commit and its parent with <c>parallelCommits</c> (<c>COMMIT_STEP</c>).</summary>
    private const double CommitStep = 40;

    /// <summary>The horizontal padding of labels and tags (<c>PX</c>).</summary>
    private const double PaddingX = 4;

    /// <summary>The vertical padding of labels and tags (<c>PY</c>).</summary>
    private const double PaddingY = 2;

    /// <summary>The number of branch colors (<c>THEME_COLOR_LIMIT</c> of the renderer).</summary>
    private const int ColorLimit = 8;

    /// <summary>The first place top to bottom and bottom to top (<c>defaultPos</c>).</summary>
    private const double DefaultPos = 30;

    /// <summary>The svg element of the cascade.</summary>
    private static readonly GitGraphElement SvgElement = GitGraphElement.Of("svg", string.Empty);

    /// <summary>A group without a class.</summary>
    private static readonly GitGraphElement PlainGroup = GitGraphElement.Of("g", string.Empty);

    /// <summary>The diagram.</summary>
    private readonly GitGraphDiagram _diagram;

    /// <summary>The context of the render.</summary>
    private readonly DiagramRenderContext _context;

    /// <summary>The gitGraph configuration of the render.</summary>
    private readonly GitGraphConfig _conf;

    /// <summary>The style sheet of the render.</summary>
    private readonly GitGraphStyles _styles;

    /// <summary>The label builder of the render (text metrics).</summary>
    private readonly LabelBuilder _labels;

    /// <summary>The work meter of the render.</summary>
    private readonly LayoutWorkMeter _meter;

    /// <summary>The place and color index of each branch (<c>branchPos</c>).</summary>
    private readonly Dictionary<string, (double Pos, int Index)> _branchPos = new(StringComparer.Ordinal);

    /// <summary>The place of each commit (<c>commitPos</c>), shared by the passes.</summary>
    private readonly Dictionary<string, (double X, double Y)> _commitPos = new(StringComparer.Ordinal);

    /// <summary>The lanes taken (<c>lanes</c>).</summary>
    private readonly List<double> _lanes = [];

    /// <summary>The bounding box of what is drawn so far, or null.</summary>
    private SceneRect? _bounds;

    /// <summary>The database of the render.</summary>
    private GitGraphDatabase _db = null!;

    /// <summary>Whether the direction is top to bottom or bottom to top.</summary>
    private bool _vertical;

    /// <summary>Whether the direction is bottom to top.</summary>
    private bool _bottomToTop;

    /// <summary>The end of the commits (<c>maxPos</c>).</summary>
    private double _maxPos;

    /// <summary>
    ///     Initializes a renderer for one render.
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <param name="context">The context of the render.</param>
    public GitGraphRenderer(GitGraphDiagram diagram, DiagramRenderContext context)
    {
        _diagram = diagram;
        _context = context;
        _conf = context.Config.GitGraph;
        _styles = GitGraphStyles.Create(context.Config, context.Sink);
        _meter = new LayoutWorkMeter(context.Options.Limits.MaxLayoutWork, diagram.KeywordLocation, context.CancellationToken);
        _labels = new LabelBuilder(context.Fonts, context.Sink, null) { Meter = _meter };
    }

    /// <summary>
    ///     Draws the diagram.
    /// </summary>
    /// <returns>The scene, whose view box is the natural size.</returns>
    /// <exception cref="MermaidRenderException">
    ///     Thrown, located, when the database fails under the render's main branch, when Mermaid's renderer fails
    ///     (<c>Closest parent not found for commit ...</c>), or when a limit is exceeded.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the render is canceled.</exception>
    public Scene Draw()
    {
        _db = _diagram.Database(_conf);
        _vertical = _diagram.Direction != GitGraphParser.LeftToRight;
        _bottomToTop = _diagram.Direction == GitGraphParser.BottomToTop;
        var branches = _db.BranchOrder;

        // The branch positions, from the width of each label
        var pos = 0.0;
        for (var i = 0; i < branches.Count; i++)
        {
            _meter.Charge(1);
            var (width, _) = BranchLabel(branches[i].Name, i).Size;
            _branchPos[branches[i].Name] = (pos, i);
            pos += 50 + (_conf.RotateCommitLabel ? 40 : 0) + (_vertical ? width / 2 : 0);
        }

        // The first pass places the commits; the branches, the arrows (from those places), the second pass, the title
        DrawCommits(null, null);
        var items = new List<SceneItem>();
        if (_conf.ShowBranches)
        {
            items.Add(DrawBranches(branches));
        }

        items.Add(DrawArrows());
        var bullets = new List<SceneItem>();
        var labels = new List<SceneItem>();
        DrawCommits(bullets, labels);
        items.Add(new SceneGroup(0, 0, bullets) { Role = "git-commits", Location = _diagram.KeywordLocation });
        items.Add(new SceneGroup(0, 0, labels) { Role = "git-labels", Location = _diagram.KeywordLocation });
        if (DrawTitle() is { } title)
        {
            items.Add(title);
        }

        // The view box (Mermaid's setup of the graph's view box): the bounding box grown by the padding
        var box = Box32(_bounds ?? new SceneRect(0, 0, 0, 0));
        var padding = _conf.DiagramPadding;
        var viewBox = new SceneRect(box.X - padding, box.Y - padding, box.Width + (2 * padding), box.Height + (2 * padding));
        var root = new SceneGroup(0, 0, items) { Role = "git-graph", Location = _diagram.KeywordLocation };
        return new Scene(viewBox, [root]);
    }

    /// <summary>
    ///     Gets a box as the browser's <c>getBBox</c> returns it: each side in single precision, the width and height
    ///     subtracted in single precision.
    /// </summary>
    /// <param name="box">The box.</param>
    /// <returns>The box.</returns>
    internal static SceneRect Box32(SceneRect box)
    {
        var left = (float)box.X;
        var top = (float)box.Y;
        var right = (float)box.Right;
        var bottom = (float)box.Bottom;
        return new SceneRect(left, top, right - left, bottom - top);
    }

    /// <summary>
    ///     Gets the text the browser shows for a text that d3 sets as the content of an SVG text, before white space
    ///     handling: Mermaid's entity placeholders become character references when the SVG is written and are shown as
    ///     their characters; anything else is shown as written.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text as shown.</returns>
    internal static string Decode(string text)
    {
        const string Prefix = "\ufb02\u00b0";
        const string Suffix = "\u00b6\u00df";
        var builder = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var start = text.IndexOf(Prefix, i, StringComparison.Ordinal);
            var end = start < 0 ? -1 : text.IndexOf(Suffix, start + Prefix.Length, StringComparison.Ordinal);
            if (end < 0)
            {
                builder.Append(text, i, text.Length - i);
                break;
            }

            // The placeholder is decoded on its own (decodeEntities), then read as markup
            builder.Append(text, i, start - i);
            var reference = LabelBuilder.DecodeEntities(text[start..(end + Suffix.Length)]);
            builder.Append(HtmlSanitizer.DecodeReferences(reference));
            i = end + Suffix.Length;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Splits a branch name into the rows of Mermaid's <c>drawText</c> (<c>/\\n|\n|&lt;br\s*\/?&gt;/gi</c>), each
    ///     trimmed.
    /// </summary>
    /// <param name="name">The branch name.</param>
    /// <returns>The rows.</returns>
    internal static List<string> Rows(string name)
    {
        var rows = new List<string>();
        var start = 0;
        var i = 0;
        while (i < name.Length)
        {
            var length = BreakLength(name, i);
            if (length > 0)
            {
                rows.Add(Preprocessor.TrimJs(name[start..i]));
                i += length;
                start = i;
            }
            else
            {
                i++;
            }
        }

        rows.Add(Preprocessor.TrimJs(name[start..]));
        return rows;
    }

    /// <summary>
    ///     Gets the length of a row break at a position: a line feed, a <c>\n</c> written out, or a <c>&lt;br&gt;</c> tag.
    /// </summary>
    /// <param name="name">The branch name.</param>
    /// <param name="i">The position.</param>
    /// <returns>The length, or zero.</returns>
    private static int BreakLength(string name, int i)
    {
        if (name[i] == '\n')
        {
            return 1;
        }

        return name[i] == '\\' && i + 1 < name.Length && name[i + 1] == 'n' ? 2 : LabelBuilder.MatchBreakTag(name, i);
    }

    /// <summary>
    ///     Adds a box to the bounding box (<c>getBBox</c>'s union, a box without area included).
    /// </summary>
    /// <param name="box">The box.</param>
    private void Include(SceneRect box)
    {
        if (!double.IsFinite(box.X) || !double.IsFinite(box.Y) || !double.IsFinite(box.Width) || !double.IsFinite(box.Height))
        {
            return;
        }

        _bounds = _bounds is { } bounds ? bounds.Union(box) : box;
    }

    /// <summary>
    ///     Places the commits (Mermaid's <c>drawCommits</c>): the first pass only places them; the second also draws each
    ///     commit's bullet, label and tags.
    /// </summary>
    /// <param name="bullets">The bullets of the second pass, or null for the first pass.</param>
    /// <param name="labels">The labels and tags of the second pass, or null for the first pass.</param>
    /// <exception cref="MermaidRenderException">Thrown, located, when Mermaid's placement fails, or when the work limit is reached.</exception>
    private void DrawCommits(List<SceneItem>? bullets, List<SceneItem>? labels)
    {
        var parallel = _conf.ParallelCommits;
        var pos = _vertical ? DefaultPos : 0;
        var sorted = _db.Commits.OrderBy(commit => commit.Seq).ToList();
        if (_bottomToTop)
        {
            if (parallel)
            {
                SetParallelBottomToTop(sorted);
            }

            sorted.Reverse();
        }

        foreach (var commit in sorted)
        {
            _meter.Charge(1);
            if (parallel)
            {
                pos = CalculatePosition(commit);
            }

            // getCommitPosition
            var posWithOffset = _bottomToTop && parallel ? pos : pos + LayoutOffset;
            var branch = _branchPos[commit.Branch].Pos;
            var x = _vertical ? branch : posWithOffset;
            var y = _vertical ? posWithOffset : branch - 2;
            if (bullets is not null && labels is not null)
            {
                var index = _branchPos[commit.Branch].Index;
                DrawBullet(bullets, commit, x, y, index);
                DrawCommitLabel(labels, commit, x, y, posWithOffset, pos);
                DrawTags(labels, commit, x, y, posWithOffset, pos);
            }

            _commitPos[commit.Id] = _vertical ? (x, posWithOffset) : (posWithOffset, y);
            pos = _bottomToTop && parallel ? pos + CommitStep : pos + CommitStep + LayoutOffset;
            _maxPos = Math.Max(_maxPos, pos);
        }
    }

    /// <summary>
    ///     Mermaid's <c>calculatePosition</c>: the place of a commit with <c>parallelCommits</c>, from its closest parent.
    /// </summary>
    /// <param name="commit">The commit.</param>
    /// <returns>The place.</returns>
    private double CalculatePosition(GitCommit commit)
    {
        var current = _commitPos.GetValueOrDefault(commit.Id);
        if (_bottomToTop)
        {
            // Bottom to top, a commit with or without a placed parent moves up from its own place
            return commit.Parents.Count == 0 || ClosestParent(commit.Parents) is not null ? current.Y - CommitStep : 0;
        }

        if (commit.Parents.Count == 0)
        {
            return _vertical ? DefaultPos : 0;
        }

        if (ClosestParent(commit.Parents) is not { } closest)
        {
            return 0;
        }

        var parent = _commitPos.GetValueOrDefault(closest);
        return _vertical ? parent.Y + CommitStep : parent.X + CommitStep;
    }

    /// <summary>
    ///     Mermaid's <c>findClosestParent</c>: the placed parent furthest along (the largest x left to right, the largest y
    ///     top to bottom, the smallest y bottom to top; a later parent wins a tie).
    /// </summary>
    /// <param name="parents">The parent ids.</param>
    /// <returns>The closest parent, or null when no parent is placed.</returns>
    private string? ClosestParent(IReadOnlyList<string> parents)
    {
        string? closest = null;
        var target = _bottomToTop ? double.PositiveInfinity : 0;
        foreach (var parent in parents)
        {
            _meter.Charge(1);
            if (!_commitPos.TryGetValue(parent, out var place))
            {
                continue;
            }

            var position = _vertical ? place.Y : place.X;
            if (_bottomToTop ? position <= target : position >= target)
            {
                closest = parent;
                target = position;
            }
        }

        return closest;
    }

    /// <summary>
    ///     Mermaid's <c>setParallelBTPos</c>: places the commits bottom to top with <c>parallelCommits</c> in three loops
    ///     (each commit below its closest parent, the roots below the lowest, then each commit above its closest parent).
    /// </summary>
    /// <param name="sorted">The commits in the order of their sequence numbers.</param>
    /// <exception cref="MermaidRenderException">Thrown, located, when a commit has no placed parent (Mermaid's error).</exception>
    private void SetParallelBottomToTop(List<GitCommit> sorted)
    {
        var current = DefaultPos;
        var maxPosition = DefaultPos;
        var roots = new List<GitCommit>();
        foreach (var commit in sorted)
        {
            _meter.Charge(1);
            if (commit.Parents.Count > 0)
            {
                // calculateCommitPosition: the closest parent's place, 40 further
                if (ClosestParent(commit.Parents) is not { } closest)
                {
                    throw new MermaidRenderException(
                        MermaidDiagnosticCodes.RenderFailed,
                        $"Closest parent not found for commit {commit.Id}",
                        commit.Location,
                        "give each commit an id of its own: a commit whose parent's id is used again later cannot be placed bottom to top with parallelCommits");
                }

                current = _commitPos[closest].Y + CommitStep;
                maxPosition = Math.Max(current, maxPosition);
            }
            else
            {
                roots.Add(commit);
            }

            _commitPos[commit.Id] = (_branchPos[commit.Branch].Pos, current + LayoutOffset);
        }

        current = maxPosition;
        foreach (var root in roots)
        {
            _meter.Charge(1);
            _commitPos[root.Id] = (_branchPos[root.Branch].Pos, current + DefaultPos);
        }

        foreach (var commit in sorted)
        {
            _meter.Charge(1);
            if (commit.Parents.Count == 0)
            {
                continue;
            }

            // findClosestParentBT: the parent with the smallest y (a later parent wins a tie)
            string? closest = null;
            var lowest = double.PositiveInfinity;
            foreach (var parent in commit.Parents)
            {
                _meter.Charge(1);
                var y = _commitPos[parent].Y;
                if (y <= lowest)
                {
                    closest = parent;
                    lowest = y;
                }
            }

            current = _commitPos[closest!].Y - CommitStep;
            maxPosition = Math.Min(current, maxPosition);
            _commitPos[commit.Id] = (_branchPos[commit.Branch].Pos, current - LayoutOffset);
        }
    }

    /// <summary>
    ///     Draws the branches (Mermaid's <c>drawBranches</c>): each branch's dashed line, the rectangle of its label and
    ///     its label; each line is a lane.
    /// </summary>
    /// <param name="branches">The branches in drawing order.</param>
    /// <returns>The group of the branches.</returns>
    private SceneGroup DrawBranches(IReadOnlyList<GitBranch> branches)
    {
        var items = new List<SceneItem>();
        for (var i = 0; i < branches.Count; i++)
        {
            var branch = branches[i];
            var color = (i % ColorLimit).ToString(CultureInfo.InvariantCulture);
            var pos = _branchPos[branch.Name].Pos;
            var spine = _vertical ? pos : pos - 2;

            // The line (from the end of the commits down to the start bottom to top), and its label's top
            var labelTop = _bottomToTop ? _maxPos : 0;
            var (x1, y1, x2, y2) = _vertical ? (pos, DefaultPos, pos, _maxPos) : (0.0, spine, _maxPos, spine);
            if (_bottomToTop)
            {
                (y1, y2) = (y2, y1);
            }

            var lineStyle = _styles.Compute([SvgElement, PlainGroup, GitGraphElement.Of("line", "branch branch" + color)]);
            items.Add(Shape([SceneSegment.Move(x1, y1), SceneSegment.Line(x2, y2)], lineStyle, false, "git-branch", branch.Name, branch.Location));
            Include(new SceneRect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1)));
            _lanes.Add(spine);

            // The label's rectangle and the label
            var label = BranchLabel(branch.Name, i);
            var (width, height) = label.Size;
            var rotate = _conf.RotateCommitLabel ? 30 : 0;
            var rect = _vertical
                ? new SceneRect(pos - (width / 2) - 10, labelTop, width + 18, height + 4)
                : new SceneRect(-width - 4 - rotate - 19, -(height / 2) + 10 + spine - 12, width + 18, height + 4);
            var rectStyle = _styles.Compute([SvgElement, PlainGroup, GitGraphElement.Of("rect", "branchLabelBkg label" + color) with { Inline = _styles.BranchLabelStyle }]);
            items.Add(Shape(RoundedRectangle(rect, 4), rectStyle, true, "git-branch-label", branch.Name, branch.Location));
            Include(rect);
            var (dx, dy) = _vertical
                ? (pos - (width / 2) - 5, labelTop)
                : (-width - 14 - rotate, spine - (height / 2) - 2);
            items.Add(new SceneGroup(dx, dy, label.Draw(branch.Location)) { Role = "git-branch-label", Id = branch.Name, Location = branch.Location });
            if (label.Bounds is { } bounds)
            {
                Include(bounds.Offset(dx, dy));
            }
        }

        return new SceneGroup(0, 0, items) { Role = "git-branches", Location = _diagram.KeywordLocation };
    }

    /// <summary>
    ///     Draws the title (Mermaid's <c>insertTitle</c>): centered on the bounding box of what is drawn, its baseline
    ///     <c>titleTopMargin</c> above the top of the drawing's coordinates.
    /// </summary>
    /// <returns>The title, or null without a title.</returns>
    private SceneText? DrawTitle()
    {
        var title = _diagram.DrawnTitle;
        if (title.Length == 0)
        {
            return null;
        }

        var bounds = Box32(_bounds ?? new SceneRect(0, 0, 0, 0));
        var style = _styles.Compute([SvgElement, GitGraphElement.Of("text", "gitTitleText")]);
        var x = bounds.X + (bounds.Width / 2);
        var y = -_conf.TitleTopMargin;
        var (text, measured) = Text(title, x, y, SceneTextAnchor.Middle, style, Placement.None, "title", null, _diagram.KeywordLocation);
        if (measured is { } box)
        {
            Include(box);
        }

        return text;
    }

    /// <summary>
    ///     Makes a path painted with a computed style.
    /// </summary>
    /// <param name="segments">The geometry.</param>
    /// <param name="style">The computed style.</param>
    /// <param name="filled">Whether the shape has an inside to fill (a line has none).</param>
    /// <param name="role">The role.</param>
    /// <param name="id">The id, or null.</param>
    /// <param name="location">The location.</param>
    /// <returns>The path.</returns>
    private static ScenePath Shape(IReadOnlyList<SceneSegment> segments, GitGraphComputedStyle style, bool filled, string role, string? id, MermaidLocation location)
    {
        SceneStroke? stroke = null;
        if (style.Faded(style.Stroke) is { } color && style.StrokeWidth > 0)
        {
            stroke = new SceneStroke(color, style.StrokeWidth)
            {
                Dash = style.Dash.Count > 0 ? style.Dash : null,
                Cap = style.LineCap switch
                {
                    "round" => LineCap.Round,
                    "square" => LineCap.Square,
                    _ => LineCap.Butt,
                },
            };
        }

        return new ScenePath(segments)
        {
            Role = role,
            Id = id,
            Location = location,
            Fill = filled ? style.Faded(style.Fill) : null,
            Stroke = stroke,
            Shadow = style.Shadow is { } s ? new SceneShadow(s.OffsetX, s.OffsetY, s.Blur, s.Color) : null,
        };
    }

    /// <summary>
    ///     Gets the outline of an SVG rectangle with rounded corners (the radii clamped to half the width and height, as
    ///     SVG clamps them).
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="radius">The corner radius (<c>rx</c> and <c>ry</c>).</param>
    /// <returns>The segments.</returns>
    private static SceneSegment[] RoundedRectangle(SceneRect rect, double radius)
    {
        var (x, y, w, h) = (rect.X, rect.Y, rect.Width, rect.Height);
        var rx = Math.Min(radius, w / 2);
        var ry = Math.Min(radius, h / 2);
        return
        [
            SceneSegment.Move(x + rx, y),
            SceneSegment.Line(x + w - rx, y),
            SceneSegment.Arc(rx, ry, 0, false, true, x + w, y + ry),
            SceneSegment.Line(x + w, y + h - ry),
            SceneSegment.Arc(rx, ry, 0, false, true, x + w - rx, y + h),
            SceneSegment.Line(x + rx, y + h),
            SceneSegment.Arc(rx, ry, 0, false, true, x, y + h - ry),
            SceneSegment.Line(x, y + ry),
            SceneSegment.Arc(rx, ry, 0, false, true, x + rx, y),
            SceneSegment.Close(),
        ];
    }

    /// <summary>
    ///     A transform of an SVG element: <c>translate(tx, ty) rotate(angle, cx, cy)</c> (the rotation applied first).
    /// </summary>
    /// <param name="Tx">The translation's x.</param>
    /// <param name="Ty">The translation's y.</param>
    /// <param name="Angle">The rotation in degrees (clockwise on the screen).</param>
    /// <param name="Cx">The x of the rotation's center.</param>
    /// <param name="Cy">The y of the rotation's center.</param>
    private readonly record struct Placement(double Tx, double Ty, double Angle, double Cx, double Cy)
    {
        /// <summary>Gets the identity.</summary>
        public static Placement None => default;

        /// <summary>
        ///     Maps a point.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>The mapped point.</returns>
        public (double X, double Y) Map(double x, double y)
        {
            if (Angle == 0)
            {
                return (x + Tx, y + Ty);
            }

            var radians = Angle * Math.PI / 180;
            var (sin, cos) = Math.SinCos(radians);
            var dx = x - Cx;
            var dy = y - Cy;
            return (Cx + (dx * cos) - (dy * sin) + Tx, Cy + (dx * sin) + (dy * cos) + Ty);
        }

        /// <summary>
        ///     Maps a box: the box of its mapped corners (as the browser maps a child's box through its transform).
        /// </summary>
        /// <param name="box">The box.</param>
        /// <returns>The mapped box.</returns>
        public SceneRect MapRect(SceneRect box)
        {
            if (Angle == 0)
            {
                return box.Offset(Tx, Ty);
            }

            var corners = new[] { Map(box.X, box.Y), Map(box.Right, box.Y), Map(box.Right, box.Bottom), Map(box.X, box.Bottom) };
            var left = corners.Min(c => c.X);
            var top = corners.Min(c => c.Y);
            return new SceneRect(left, top, corners.Max(c => c.X) - left, corners.Max(c => c.Y) - top);
        }
    }
}
