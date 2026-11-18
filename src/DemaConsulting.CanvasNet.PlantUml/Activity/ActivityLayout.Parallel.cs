// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <content>
///     The parallel blocks: forks between two bars (PlantUML's <c>ParallelBuilderFork</c>), forks ending in a merge
///     diamond (<c>ParallelBuilderMerge</c>), and splits between two thin lines (<c>ParallelBuilderSplit</c>).
/// </content>
internal sealed partial class ActivityLayout
{
    /// <summary>
    ///     Builds the tile of a fork.
    /// </summary>
    /// <param name="fork">The fork.</param>
    /// <returns>The tile.</returns>
    private Tile BuildFork(ActivityFork fork)
    {
        var branches = DecorateParallel(fork.Branches.Select(b => BuildList(b)).ToList());
        var inner = new ParallelTile(this, branches);
        var step1 = WithBar(inner, branches, fork.Lane, fork.Location);
        if (fork.Style == ActivityForkStyle.Merge)
        {
            return WithMerge(step1, branches, fork);
        }

        var bar = new BarTile(this, fork.LaneOut) { Width = step1.Geometry.Width, Location = fork.Location };
        if (fork.Label is { Length: > 0 } label)
        {
            bar.Label = Text(label, SkinElement.ActivityArrow, fork.Location);
        }

        var result = new AssemblyTile(this, step1, bar);
        var justBeforeBar2 = BarHeight + inner.Geometry.Height;
        var connections = new List<TileConnection>();
        double x = 0;
        foreach (var branch in branches)
        {
            if (branch.Geometry.HasOut)
            {
                connections.Add(ForkOut(branch, bar, x, justBeforeBar2));
            }

            x += branch.Geometry.Width;
        }

        return new ConnectedTile(result, connections);
    }

    /// <summary>
    ///     Builds the tile of a split.
    /// </summary>
    /// <param name="split">The split.</param>
    /// <returns>The tile.</returns>
    private Tile BuildSplit(ActivitySplit split)
    {
        var branches = DecorateParallel(split.Branches.Select(b => BuildList(b)).ToList());
        var inner = new ParallelTile(this, branches);

        // Step 1: the thin line on top, with an arrow to every branch
        var thinColor = branches.Exists(b => b.InLink.Style != ActivityLinkStyle.Hidden) ? ArrowColor : (Rgba32?)null;
        var thin = new ThinSplitTile(this, thinColor, branches[0].LaneIn);
        var connections = new List<TileConnection>();
        double x = 0;
        double first = 0;
        double last = 0;
        foreach (var branch in branches)
        {
            var geometry = branch.Geometry;
            if (first == 0)
            {
                first = x + geometry.Left;
            }

            last = x + geometry.Left;
            connections.Add(ParallelIn(thin, branch, x, false));
            x += geometry.Width;
        }

        Tile result = new ConnectedTile(inner, connections);
        var left = result.Geometry.Left;
        (thin.First, thin.Last, thin.Width) = (Math.Min(first, left), Math.Max(last, left), result.Geometry.Width);
        Tile step1 = new AssemblyTile(this, thin, result);

        // Step 2: the thin line below, with an arrow from every branch that continues
        if (!branches.Exists(b => b.Geometry.HasOut))
        {
            return new KilledTile(step1);
        }

        var width = step1.Geometry.Width;
        var outLine = new ThinSplitTile(this, Rainbow(step1.InLink).Color, split.LaneOut);
        var assembly = new AssemblyTile(this, step1, outLine);
        connections = [];
        (x, first, last) = (0, 0, 0);
        foreach (var branch in branches)
        {
            var geometry = branch.Geometry;
            if (geometry.HasOut)
            {
                if (first == 0)
                {
                    first = x + geometry.Left;
                }

                last = x + geometry.Left;
                connections.Add(SplitOut(branch, outLine, x, inner.Geometry.Height));
            }

            x += geometry.Width;
        }

        var stepLeft = step1.Geometry.Left;
        (outLine.First, outLine.Last, outLine.Width) = (Math.Min(first, stepLeft), Math.Max(last, stepLeft), width);
        return new ConnectedTile(assembly, connections);
    }

    /// <summary>
    ///     Pads the branches of a parallel block (PlantUML's <c>AbstractParallelFtilesBuilder</c>): 14 pixels on both
    ///     sides (plus room for a label), as high as the highest branch plus 40, plus the height of the labels of the
    ///     arrows into and out of the branches.
    /// </summary>
    /// <param name="branches">The branches.</param>
    /// <returns>The padded branches.</returns>
    private List<Tile> DecorateParallel(List<Tile> branches)
    {
        var maxHeight = branches.Max(b => b.Geometry.Height);
        var margin1 = branches.Select(b => Label(b.InLink)?.Height ?? 0).Max();
        var margin2 = branches.Select(b => Label(b.OutLink)?.Height ?? 0).Max();
        var result = new List<Tile>();
        foreach (var branch in branches)
        {
            var supp = Math.Max(LabelOverflow(branch, branch.InLink), LabelOverflow(branch, branch.OutLink));
            Tile tile = MarginTile.Of(branch, 14, 14 + supp);
            tile = new FixedHeightTile(tile, maxHeight + 40);
            tile = new FixedMarginTile(tile, margin1, margin2);
            result.Add(tile);
        }

        return result;
    }

    /// <summary>
    ///     Gets how far the label of an arrow reaches beyond the right of a branch.
    /// </summary>
    /// <param name="branch">The branch.</param>
    /// <param name="link">The arrow.</param>
    /// <returns>The overflow, or zero.</returns>
    private double LabelOverflow(Tile branch, ActivityLink link)
    {
        if (Label(link) is not { } text)
        {
            return 0;
        }

        var right = branch.Geometry.Left + text.Width;
        return right > branch.Geometry.Width ? right - branch.Geometry.Width : 0;
    }

    /// <summary>
    ///     Adds the bar of a fork on top of its branches, with an arrow to every branch.
    /// </summary>
    /// <param name="inner">The branches side by side.</param>
    /// <param name="branches">The branches.</param>
    /// <param name="lane">The swimlane of the bar.</param>
    /// <param name="location">The location.</param>
    /// <returns>The tile.</returns>
    private AssemblyTile WithBar(ParallelTile inner, List<Tile> branches, ActivityLane? lane, PlantUmlLocation location)
    {
        var bar = new BarTile(this, lane) { Location = location };
        var connections = new List<TileConnection>();
        double x = 0;
        foreach (var branch in branches)
        {
            connections.Add(ParallelIn(bar, branch, x, true));
            x += branch.Geometry.Width;
        }

        var result = new ConnectedTile(inner, connections);
        bar.Width = result.Geometry.Width;
        return new AssemblyTile(this, bar, result);
    }

    /// <summary>
    ///     Ends a fork with a merge diamond, with an arrow from every branch that continues.
    /// </summary>
    /// <param name="step1">The bar and the branches.</param>
    /// <param name="branches">The branches.</param>
    /// <param name="fork">The fork.</param>
    /// <returns>The tile.</returns>
    private ConnectedTile WithMerge(AssemblyTile step1, List<Tile> branches, ActivityFork fork)
    {
        var style = Theme[SkinElement.Activity];
        var diamond = new DiamondTile(this, style.Fill, branches[^1].LaneOut) { Location = fork.Location };
        var result = new AssemblyTile(this, step1, diamond);
        var diamondOffset = result.Offset2;
        var connections = new List<TileConnection>();
        double x = 0;
        foreach (var branch in branches)
        {
            if (branch.Geometry.HasOut)
            {
                connections.Add(MergeOut(branch, diamond, x, diamondOffset));
            }

            x += branch.Geometry.Width;
        }

        return new ConnectedTile(result, connections);
    }

    /// <summary>
    ///     Creates the arrow from the bar or thin line on top to a branch.
    /// </summary>
    /// <param name="top">The bar or line.</param>
    /// <param name="branch">The branch.</param>
    /// <param name="x">The x of the branch.</param>
    /// <param name="fork">Whether it is a fork (whose crossing arrows the compression on x ignores).</param>
    /// <returns>The connection.</returns>
    private DelegateConnection ParallelIn(Tile top, Tile branch, double x, bool fork)
    {
        var link = Rainbow(branch.InLink);
        var label = Label(branch.InLink);
        return new DelegateConnection(
            top,
            branch,
            (canvas, ox, oy) =>
            {
                var geometry = branch.Geometry;
                canvas.Add(new Snake(link, ArrowDirection.Down) { Label = label }.Add(ox + x + geometry.Left, oy).Add(ox + x + geometry.Left, oy + geometry.InY));
            },
            (canvas, ox, oy, dx1, dx2) =>
            {
                var geometry = branch.Geometry;
                var (x1, y1) = (ox + x + geometry.Left + dx1, oy);
                var (x2, y2) = (ox + x + geometry.Left + dx2, oy + geometry.InY);
                var middle = y1 + 4;
                canvas.Add(new Snake(link, ArrowDirection.Down) { Label = label, IgnoreForCompression = fork }.Add(x1, y1).Add(x1, middle).Add(x2, middle).Add(x2, y2));
            });
    }

    /// <summary>
    ///     Creates the arrow from a branch of a fork down to the bar below.
    /// </summary>
    /// <param name="branch">The branch.</param>
    /// <param name="bar">The bar.</param>
    /// <param name="x">The x of the branch.</param>
    /// <param name="justBeforeBar2">The y of the bar below.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection ForkOut(Tile branch, Tile bar, double x, double justBeforeBar2)
    {
        var link = Rainbow(branch.OutLink);
        var label = Label(branch.OutLink);
        return new DelegateConnection(
            branch,
            bar,
            (canvas, ox, oy) =>
            {
                var geometry = branch.Geometry;
                canvas.Add(new Snake(link, ArrowDirection.Down) { Label = label }
                    .Add(ox + x + geometry.Left, oy + BarHeight + geometry.OutY).Add(ox + x + geometry.Left, oy + justBeforeBar2));
            },
            (canvas, ox, oy, dx1, dx2) =>
            {
                var geometry = branch.Geometry;
                var (x1, y1) = (ox + x + geometry.Left + dx1, oy + BarHeight + geometry.OutY);
                var (x2, y2) = (ox + x + geometry.Left + dx2, oy + justBeforeBar2);
                var middle = y2 - 14;
                canvas.Add(new Snake(link, ArrowDirection.Down) { Label = label, IgnoreForCompression = true }.Add(x1, y1).Add(x1, middle).Add(x2, middle).Add(x2, y2));
            });
    }

    /// <summary>
    ///     Creates the arrow from a branch of a split down to the thin line below.
    /// </summary>
    /// <param name="branch">The branch.</param>
    /// <param name="line">The line.</param>
    /// <param name="x">The x of the branch.</param>
    /// <param name="height">The height of the branches.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection SplitOut(Tile branch, Tile line, double x, double height)
    {
        var link = Rainbow(branch.OutLink);
        var label = Label(branch.OutLink);
        const double dy = 1.5;
        return new DelegateConnection(
            branch,
            line,
            (canvas, ox, oy) =>
            {
                var geometry = branch.Geometry;
                canvas.Add(new Snake(link, ArrowDirection.Down) { Label = label }
                    .Add(ox + x + geometry.Left, oy + dy + geometry.OutY).Add(ox + x + geometry.Left, oy + dy + height));
            },
            (canvas, ox, oy, dx1, dx2) =>
            {
                var geometry = branch.Geometry;
                var (x1, y1) = (ox + x + geometry.Left + dx1, oy + dy + geometry.OutY);
                var (x2, y2) = (ox + x + geometry.Left + dx2, oy + dy + height);
                var middle = y2 - 14;
                canvas.Add(new Snake(link, ArrowDirection.Down) { Label = label }.Add(x1, y1).Add(x1, middle).Add(x2, middle).Add(x2, y2));
            });
    }

    /// <summary>
    ///     Creates the arrow from a branch of a fork to the merge diamond.
    /// </summary>
    /// <param name="branch">The branch.</param>
    /// <param name="diamond">The diamond.</param>
    /// <param name="x">The x of the branch.</param>
    /// <param name="diamondOffset">The offset of the diamond.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection MergeOut(Tile branch, Tile diamond, double x, (double X, double Y) diamondOffset)
    {
        var link = Rainbow(branch.OutLink);
        return new DelegateConnection(branch, diamond, (canvas, ox, oy) =>
        {
            var geometry = branch.Geometry;
            var d = diamond.Geometry;
            var (x1, y1) = (x + geometry.Left, BarHeight + geometry.OutY);
            var diamondOut = (X: diamondOffset.X + d.Left, Y: diamondOffset.Y + d.OutY);
            double ax, ay;
            ArrowDirection head;
            if (x1 < diamondOut.X - (d.Width / 2))
            {
                (ax, ay, head) = (-d.Width / 2, -d.Height / 2, ArrowDirection.Right);
            }
            else if (x1 > diamondOut.X + (d.Width / 2))
            {
                (ax, ay, head) = (d.Width / 2, -d.Height / 2, ArrowDirection.Left);
            }
            else
            {
                (ax, ay, head) = (0, -d.Height, ArrowDirection.Down);
            }

            var (x2, y2) = (diamondOut.X + ax, diamondOut.Y + ay);
            canvas.Add(new Snake(link, head).Add(ox + x1, oy + y1).Add(ox + x1, oy + y2).Add(ox + x2, oy + y2));
        });
    }
}

/// <summary>
///     The branches of a parallel block side by side (PlantUML's <c>FtileForkInner</c>).
/// </summary>
/// <param name="layout">The layout.</param>
/// <param name="branches">The branches.</param>
internal sealed class ParallelTile(ActivityLayout layout, IReadOnlyList<Tile> branches) : Tile(layout)
{
    /// <inheritdoc/>
    public override ActivityLane? LaneIn => branches[0].LaneIn;

    /// <inheritdoc/>
    protected override IReadOnlySet<ActivityLane> ComputeLanes() => MergeLanes([.. branches]);

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        double position = 0;
        foreach (var branch in branches)
        {
            canvas.Tile(branch, x + position, y);
            position += branch.Geometry.Width;
        }
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var width = branches.Sum(b => b.Geometry.Width);
        var height = branches.Max(b => b.Geometry.Height);
        return new TileGeometry(width, height, width / 2, 0, height);
    }
}
