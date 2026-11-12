// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.GitGraph;

/// <content>
///     The bullets, commit labels, tags and texts: Mermaid's <c>drawCommitBullet</c>, <c>drawCommitLabel</c>,
///     <c>drawCommitTags</c> and <c>drawText</c>.
/// </content>
internal sealed partial class GitGraphRenderer
{
    /// <summary>The group of the bullets.</summary>
    private static readonly GitGraphElement BulletGroup = GitGraphElement.Of("g", "commit-bullets");

    /// <summary>The group of the commit labels and tags.</summary>
    private static readonly GitGraphElement LabelGroup = GitGraphElement.Of("g", "commit-labels");

    /// <summary>The group of a branch label.</summary>
    private static readonly GitGraphElement BranchLabelGroup = GitGraphElement.Of("g", "branchLabel");

    /// <summary>A text element without a class.</summary>
    private static readonly GitGraphElement PlainText = GitGraphElement.Of("text", string.Empty);

    /// <summary>A transparent color, for a text without a fill.</summary>
    private static readonly Rgba32 Transparent = new(0, 0, 0, 0);

    /// <summary>
    ///     Lays out a branch label (Mermaid's <c>drawText</c>): one row per piece of the name, each at <c>x = 0</c> with
    ///     <c>dy = 1em</c>, its spaces kept; an empty row has no box and its <c>dy</c> is not applied.
    /// </summary>
    /// <param name="name">The branch name.</param>
    /// <param name="index">The branch's index in the drawing order.</param>
    /// <returns>The label.</returns>
    private BranchLabelLayout BranchLabel(string name, int index)
    {
        var color = (index % ColorLimit).ToString(CultureInfo.InvariantCulture);
        var style = _styles.Compute([SvgElement, PlainGroup, BranchLabelGroup, GitGraphElement.Of("g", "label branch-label" + color), PlainText]);
        var font = new SceneFont(style.FontFamily, style.FontSize, false, false);
        var (ascent, descent) = _labels.VerticalMetrics(font);
        var fill = style.Faded(style.Fill) ?? Transparent;
        SceneRect? bounds = null;
        var rows = new List<(string Text, double Baseline)>();
        foreach (var row in Rows(name))
        {
            // xml:space="preserve": tabs and line breaks are spaces, nothing is collapsed
            var laid = row.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
            _meter.Charge(1 + laid.Length);
            if (laid.Length == 0)
            {
                continue;
            }

            var baseline = (rows.Count + 1) * style.FontSize;
            var (_, left, right) = _labels.SvgLineExtent(laid, font);
            var box = new SceneRect(left, baseline - ascent, right - left, ascent + descent);
            bounds = bounds is { } b ? b.Union(box) : box;
            rows.Add((laid, baseline));
        }

        IReadOnlyList<SceneItem> Draw(MermaidLocation location)
        {
            var items = new List<SceneItem>(rows.Count);
            foreach (var (laid, baseline) in rows)
            {
                var shown = Decode(laid);
                _meter.Charge(1 + shown.Length);
                var (_, left, right) = _labels.SvgLineExtent(shown, font);
                items.Add(new SceneText(0, baseline, SceneTextAnchor.Start, [new SceneTextRun(shown, font, fill)], new SceneRect(left, baseline - ascent, right - left, ascent + descent))
                {
                    Role = "git-branch-label",
                    Id = name,
                    Location = location,
                });
            }

            return items;
        }

        return new BranchLabelLayout(bounds, Draw);
    }

    /// <summary>
    ///     Draws a commit's bullet (Mermaid's <c>drawCommitBullet</c>): a highlighted commit's two squares, a cherry-pick's
    ///     circle with its two cherries and stems, or a circle with a merge's inner circle or a reverse commit's cross. The
    ///     class attribute of each shape holds the commit id, as in Mermaid, so the style sheet sees it.
    /// </summary>
    /// <param name="items">The bullets.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="x">The commit's x.</param>
    /// <param name="y">The commit's y.</param>
    /// <param name="branchIndex">The index of the commit's branch.</param>
    private void DrawBullet(List<SceneItem> items, GitCommit commit, double x, double y, int branchIndex)
    {
        var symbol = commit.CustomType ?? commit.Type;
        var typeClass = symbol switch
        {
            GitCommit.Reverse => "commit-reverse",
            GitCommit.Highlight => "commit-highlight",
            GitCommit.Merge => "commit-merge",
            GitCommit.CherryPick => "commit-cherry-pick",
            _ => "commit-normal",
        };
        var color = (branchIndex % ColorLimit).ToString(CultureInfo.InvariantCulture);
        var id = commit.Id;
        void Add(IReadOnlyList<SceneSegment> segments, SceneRect box, GitGraphElement element, bool filled)
        {
            items.Add(Shape(segments, _styles.Compute([SvgElement, BulletGroup, element]), filled, "git-commit", id, commit.Location));
            Include(box);
        }

        void Circle(double cx, double cy, double r, GitGraphElement element) =>
            Add(CircleSegments(cx, cy, r), new SceneRect(cx - r, cy - r, 2 * r, 2 * r), element, true);

        void Line(double x1, double y1, double x2, double y2, GitGraphElement element) =>
            Add([SceneSegment.Move(x1, y1), SceneSegment.Line(x2, y2)], new SceneRect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1)), element, false);

        if (symbol == GitCommit.Highlight)
        {
            var outer = new SceneRect(x - 10, y - 10, 20, 20);
            Add(Rectangle(outer, Placement.None), outer, GitGraphElement.Of("rect", $"commit {id} commit-highlight{color} {typeClass}-outer"), true);
            var inner = new SceneRect(x - 6, y - 6, 12, 12);
            Add(Rectangle(inner, Placement.None), inner, GitGraphElement.Of("rect", $"commit {id} commit{color} {typeClass}-inner"), true);
        }
        else if (symbol == GitCommit.CherryPick)
        {
            var mark = _context.Config.Theme == "dark" ? "#000000" : "#fff";
            var classes = $"commit {id} {typeClass}";
            Circle(x, y, 10, GitGraphElement.Of("circle", classes));
            Circle(x - 3, y + 2, 2.75, GitGraphElement.Of("circle", classes, ("fill", mark)));
            Circle(x + 3, y + 2, 2.75, GitGraphElement.Of("circle", classes, ("fill", mark)));
            Line(x + 3, y + 1, x, y - 5, GitGraphElement.Of("line", classes, ("stroke", mark)));
            Line(x - 3, y + 1, x, y - 5, GitGraphElement.Of("line", classes, ("stroke", mark)));
        }
        else
        {
            Circle(x, y, 10, GitGraphElement.Of("circle", $"commit {id} commit{color}"));
            if (symbol == GitCommit.Merge)
            {
                Circle(x, y, 6, GitGraphElement.Of("circle", $"commit {typeClass} {id} commit{color}"));
            }

            if (symbol == GitCommit.Reverse)
            {
                Add(
                    [SceneSegment.Move(x - 5, y - 5), SceneSegment.Line(x + 5, y + 5), SceneSegment.Move(x - 5, y + 5), SceneSegment.Line(x + 5, y - 5)],
                    new SceneRect(x - 5, y - 5, 10, 10),
                    GitGraphElement.Of("path", $"commit {typeClass} {id} commit{color}"),
                    true);
            }
        }
    }

    /// <summary>
    ///     Draws a commit's label (Mermaid's <c>drawCommitLabel</c>), for a commit that is not a cherry-pick and not a merge
    ///     without an id, when <c>showCommitLabel</c> is set: the id on a half-transparent rectangle, below the commit (left
    ///     to right) or to its left (top to bottom, bottom to top), turned 45 degrees with <c>rotateCommitLabel</c>.
    /// </summary>
    /// <param name="items">The labels.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="x">The commit's x.</param>
    /// <param name="y">The commit's y.</param>
    /// <param name="posWithOffset">The commit's place with the offset.</param>
    /// <param name="pos">The commit's place.</param>
    private void DrawCommitLabel(List<SceneItem> items, GitCommit commit, double x, double y, double posWithOffset, double pos)
    {
        if (commit.Type == GitCommit.CherryPick || (commit.Type == GitCommit.Merge && commit.CustomId != true) || !_conf.ShowCommitLabel)
        {
            return;
        }

        var textStyle = _styles.Compute([SvgElement, LabelGroup, PlainGroup, GitGraphElement.Of("text", "commit-label")]);
        var rectStyle = _styles.Compute([SvgElement, LabelGroup, PlainGroup, GitGraphElement.Of("rect", "commit-label-bkg")]);
        var (width, height) = Measure(commit.Id, textStyle).Size;
        SceneRect rect;
        double textX;
        double textY;
        if (_vertical)
        {
            rect = new SceneRect(x - (width + (4 * PaddingX) + 5), y - 12, width + (2 * PaddingY), height + (2 * PaddingY));
            textX = x - (width + (4 * PaddingX));
            textY = y + height - 12;
        }
        else
        {
            rect = new SceneRect(posWithOffset - (width / 2) - PaddingY, y + 13.5, width + (2 * PaddingY), height + (2 * PaddingY));
            textX = posWithOffset - (width / 2);
            textY = y + 25;
        }

        // The rotation: of the text and the rectangle top to bottom, of their group left to right
        var placement = Placement.None;
        if (_conf.RotateCommitLabel)
        {
            placement = _vertical
                ? new Placement(0, 0, -45, x, y)
                : new Placement(-7.5 - ((width + 10) / 25 * 9.5), 10 + (width / 25 * 8.5), -45, pos, y);
        }

        var children = new List<SceneItem>
        {
            Shape(Rectangle(rect, placement), rectStyle, true, "git-commit-label", commit.Id, commit.Location),
        };
        var (text, measured) = Text(commit.Id, textX, textY, SceneTextAnchor.Start, textStyle, placement, "git-commit-label", commit.Id, commit.Location);
        if (text is not null)
        {
            children.Add(text);
        }

        // The group's box is the union of its children's, mapped as a whole when the group is turned
        var union = measured is { } box ? rect.Union(box) : rect;
        if (_vertical)
        {
            Include(placement.MapRect(rect));
            if (measured is { } textBox)
            {
                Include(placement.MapRect(textBox));
            }
        }
        else
        {
            Include(placement.MapRect(union));
        }

        items.Add(new SceneGroup(0, 0, children) { Role = "git-commit-label", Id = commit.Id, Location = commit.Location });
    }

    /// <summary>
    ///     Draws a commit's tags (Mermaid's <c>drawCommitTags</c>): the last tag nearest the commit, each 20 pixels above
    ///     the one after it, on a label shape as wide as the widest tag with a hole; top to bottom and bottom to top the
    ///     shapes are turned 45 degrees beside the commit.
    /// </summary>
    /// <param name="items">The labels.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="x">The commit's x.</param>
    /// <param name="y">The commit's y.</param>
    /// <param name="posWithOffset">The commit's place with the offset.</param>
    /// <param name="pos">The commit's place.</param>
    private void DrawTags(List<SceneItem> items, GitCommit commit, double x, double y, double posWithOffset, double pos)
    {
        if (commit.Tags.Count == 0)
        {
            return;
        }

        var textStyle = _styles.Compute([SvgElement, LabelGroup, GitGraphElement.Of("text", "tag-label")]);
        var polygonStyle = _styles.Compute([SvgElement, LabelGroup, GitGraphElement.Of("polygon", "tag-label-bkg")]);
        var holeStyle = _styles.Compute([SvgElement, LabelGroup, GitGraphElement.Of("circle", "tag-hole")]);
        var tags = new List<(string Tag, double Width, double Offset)>();
        var yOffset = 0.0;
        var maxWidth = 0.0;
        var maxHeight = 0.0;
        foreach (var tag in commit.Tags.Reverse())
        {
            var (width, height) = Measure(tag, textStyle).Size;
            maxWidth = Math.Max(maxWidth, width);
            maxHeight = Math.Max(maxHeight, height);
            tags.Add((tag, width, yOffset));
            yOffset += 20;
        }

        var h2 = maxHeight / 2;
        foreach (var (tag, width, offset) in tags)
        {
            (double X, double Y)[] points;
            (double X, double Y) hole;
            (double X, double Y) at;
            Placement shape;
            Placement label;
            if (_vertical)
            {
                var origin = pos + offset;
                points =
                [
                    (x, origin + 2), (x, origin - 2), (x + LayoutOffset, origin - h2 - 2), (x + LayoutOffset + maxWidth + 4, origin - h2 - 2),
                    (x + LayoutOffset + maxWidth + 4, origin + h2 + 2), (x + LayoutOffset, origin + h2 + 2),
                ];
                hole = (x + (PaddingX / 2), origin);
                at = (x + 5, origin + 3);
                shape = new Placement(12, 12, 45, x, pos);
                label = new Placement(14, 14, 45, x, pos);
            }
            else
            {
                var ly = y - 19.2 - offset;
                points =
                [
                    (pos - (maxWidth / 2) - (PaddingX / 2), ly + PaddingY), (pos - (maxWidth / 2) - (PaddingX / 2), ly - PaddingY),
                    (posWithOffset - (maxWidth / 2) - PaddingX, ly - h2 - PaddingY), (posWithOffset + (maxWidth / 2) + PaddingX, ly - h2 - PaddingY),
                    (posWithOffset + (maxWidth / 2) + PaddingX, ly + h2 + PaddingY), (posWithOffset - (maxWidth / 2) - PaddingX, ly + h2 + PaddingY),
                ];
                hole = (pos - (maxWidth / 2) + (PaddingX / 2), ly);
                at = (posWithOffset - (width / 2), y - 16 - offset);
                shape = Placement.None;
                label = Placement.None;
            }

            // The polygon, the hole and the text, each with its own transform
            var mapped = points.Select(p => shape.Map(p.X, p.Y)).ToList();
            var outline = new List<SceneSegment>(mapped.Count + 1) { SceneSegment.Move(mapped[0].X, mapped[0].Y) };
            outline.AddRange(mapped.Skip(1).Select(p => SceneSegment.Line(p.X, p.Y)));
            outline.Add(SceneSegment.Close());
            var children = new List<SceneItem> { Shape(outline, polygonStyle, true, "git-tag", tag, commit.Location) };
            var left = points.Min(p => p.X);
            var top = points.Min(p => p.Y);
            Include(shape.MapRect(new SceneRect(left, top, points.Max(p => p.X) - left, points.Max(p => p.Y) - top)));
            var center = shape.Map(hole.X, hole.Y);
            children.Add(Shape(CircleSegments(center.X, center.Y, 1.5), holeStyle, true, "git-tag", tag, commit.Location));
            Include(shape.MapRect(new SceneRect(hole.X - 1.5, hole.Y - 1.5, 3, 3)));
            var (text, measured) = Text(tag, at.X, at.Y, SceneTextAnchor.Start, textStyle, label, "git-tag", tag, commit.Location);
            if (text is not null)
            {
                children.Add(text);
            }

            if (measured is { } box)
            {
                Include(label.MapRect(box));
            }

            items.Add(new SceneGroup(0, 0, children) { Role = "git-tag", Id = tag, Location = commit.Location });
        }
    }

    /// <summary>
    ///     Measures an SVG text as the browser's <c>getBBox</c> does while Mermaid draws: its content with Mermaid's
    ///     entity placeholders, white space collapsed.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="style">The computed style of the text.</param>
    /// <returns>The measure.</returns>
    private TextMeasure Measure(string content, GitGraphComputedStyle style)
    {
        var laid = LabelBuilder.CollapseSvgWhiteSpace(content);
        _meter.Charge(1 + laid.Length);
        if (laid.Length == 0)
        {
            return default;
        }

        var font = new SceneFont(style.FontFamily, style.FontSize, false, false);
        var (advance, left, right) = _labels.SvgLineExtent(laid, font);
        var (ascent, descent) = _labels.VerticalMetrics(font);
        return new TextMeasure(true, advance, left, right, ascent, descent);
    }

    /// <summary>
    ///     Makes an SVG text of one line: the scene text as it is shown (placed and turned by a transform; its box the box
    ///     of its turned box), and the box the browser measures while Mermaid draws, in the text's own coordinates.
    /// </summary>
    /// <param name="content">The content (with Mermaid's entity placeholders).</param>
    /// <param name="x">The x attribute.</param>
    /// <param name="y">The y attribute (the baseline).</param>
    /// <param name="anchor">The text anchor.</param>
    /// <param name="style">The computed style.</param>
    /// <param name="placement">The transform of the text.</param>
    /// <param name="role">The role.</param>
    /// <param name="id">The id, or null.</param>
    /// <param name="location">The location.</param>
    /// <returns>The scene text (null when nothing is shown or the position is not a number) and the measured box (null for an empty text).</returns>
    private (SceneText? Text, SceneRect? Measured) Text(string content, double x, double y, SceneTextAnchor anchor, GitGraphComputedStyle style, Placement placement, string role, string? id, MermaidLocation location)
    {
        var measure = Measure(content, style);
        SceneRect? measured = measure.IsSet ? measure.Box(x, y, anchor) : null;
        var shown = LabelBuilder.CollapseSvgWhiteSpace(Decode(content));
        _meter.Charge(1 + shown.Length);
        if (shown.Length == 0 || !double.IsFinite(x) || !double.IsFinite(y))
        {
            return (null, measured);
        }

        var font = new SceneFont(style.FontFamily, style.FontSize, false, false);
        var (advance, left, right) = _labels.SvgLineExtent(shown, font);
        var (ascent, descent) = _labels.VerticalMetrics(font);
        var box = new TextMeasure(true, advance, left, right, ascent, descent).Box(x, y, anchor);
        var (px, py) = (x + placement.Tx, y + placement.Ty);
        var text = new SceneText(px, py, anchor, [new SceneTextRun(shown, font, style.Faded(style.Fill) ?? Transparent)], placement.MapRect(box))
        {
            Role = role,
            Id = id,
            Location = location,
            Rotation = placement.Angle != 0 ? new SceneRotation(placement.Angle, placement.Cx + placement.Tx, placement.Cy + placement.Ty) : null,
        };
        return (text, measured);
    }

    /// <summary>
    ///     Gets the outline of a rectangle mapped through a transform.
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="placement">The transform.</param>
    /// <returns>The segments.</returns>
    private static SceneSegment[] Rectangle(SceneRect rect, Placement placement)
    {
        var a = placement.Map(rect.X, rect.Y);
        var b = placement.Map(rect.Right, rect.Y);
        var c = placement.Map(rect.Right, rect.Bottom);
        var d = placement.Map(rect.X, rect.Bottom);
        return [SceneSegment.Move(a.X, a.Y), SceneSegment.Line(b.X, b.Y), SceneSegment.Line(c.X, c.Y), SceneSegment.Line(d.X, d.Y), SceneSegment.Close()];
    }

    /// <summary>
    ///     Gets the outline of a circle, as four quarter arcs from its right end.
    /// </summary>
    /// <param name="cx">The center's x.</param>
    /// <param name="cy">The center's y.</param>
    /// <param name="r">The radius.</param>
    /// <returns>The segments.</returns>
    private static SceneSegment[] CircleSegments(double cx, double cy, double r) =>
    [
        SceneSegment.Move(cx + r, cy),
        SceneSegment.Arc(r, r, 0, false, true, cx, cy + r),
        SceneSegment.Arc(r, r, 0, false, true, cx - r, cy),
        SceneSegment.Arc(r, r, 0, false, true, cx, cy - r),
        SceneSegment.Arc(r, r, 0, false, true, cx + r, cy),
        SceneSegment.Close(),
    ];

    /// <summary>
    ///     A branch label laid out: the box of its rows (null when every row is empty) and the drawing of its rows.
    /// </summary>
    /// <param name="Bounds">The box of the rows, or null.</param>
    /// <param name="Draw">Draws the rows at a location.</param>
    private sealed record BranchLabelLayout(SceneRect? Bounds, Func<MermaidLocation, IReadOnlyList<SceneItem>> Draw)
    {
        /// <summary>Gets the width and height of the box (zero without one).</summary>
        public (double Width, double Height) Size => Bounds is { } b ? (b.Width, b.Height) : (0, 0);
    }

    /// <summary>
    ///     The measure of a line of SVG text: its advance, the left and right of its ink-extended box, and the rounded
    ///     ascent and descent of its font.
    /// </summary>
    /// <param name="IsSet">Whether the text has a box (it is not empty).</param>
    /// <param name="Advance">The advance.</param>
    /// <param name="Left">The left of the box, from the start of the line.</param>
    /// <param name="Right">The right of the box, from the start of the line.</param>
    /// <param name="Ascent">The ascent.</param>
    /// <param name="Descent">The descent.</param>
    private readonly record struct TextMeasure(bool IsSet, double Advance, double Left, double Right, double Ascent, double Descent)
    {
        /// <summary>Gets the width and height of the box (<c>getBBox</c>; zero for an empty text).</summary>
        public (double Width, double Height) Size => IsSet ? (Right - Left, Ascent + Descent) : (0, 0);

        /// <summary>
        ///     Gets the box of the text at an anchor point.
        /// </summary>
        /// <param name="x">The anchor's x.</param>
        /// <param name="baseline">The baseline.</param>
        /// <param name="anchor">The text anchor.</param>
        /// <returns>The box.</returns>
        public SceneRect Box(double x, double baseline, SceneTextAnchor anchor)
        {
            var start = anchor switch
            {
                SceneTextAnchor.Middle => x - (Advance / 2),
                SceneTextAnchor.End => x - Advance,
                _ => x,
            };
            return new SceneRect(start + Left, baseline - Ascent, Right - Left, Ascent + Descent);
        }
    }
}
