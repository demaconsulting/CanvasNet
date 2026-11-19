using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <summary>
///     A parsed activity diagram of the new syntax: its tree of instructions, its swimlanes, its page texts, and its
///     look.
/// </summary>
/// <remarks>
///     <para>
///         The model is what <see cref="ActivityParser"/> produces and what <see cref="ActivityLayout"/> and
///         <see cref="ActivityRenderer"/> draw. The root list holds the instructions of the diagram in order; ifs,
///         loops, forks, splits and groups hold their own lists.
///     </para>
///     <para>
///         For the document's facts, the nodes are the actions (with the backward actions of loops) and the start,
///         stop, end and connector nodes and the decisions (one per if or elseif test, one per while or repeat loop);
///         the edges are the arrows between them (one per instruction that follows another in its list, two per
///         branch of an if, fork or split, and three per loop); the subgraphs are the swimlanes and the groups. The
///         model is not changed after parsing and is safe for concurrent use; every <see cref="Layout"/> call lays the
///         diagram out afresh under the options of the call.
///     </para>
/// </remarks>
internal sealed class ActivityModel : IPlantUmlDiagram
{
    /// <summary>
    ///     Initializes a model.
    /// </summary>
    /// <param name="root">The instructions of the diagram.</param>
    /// <param name="lanes">The swimlanes in order of declaration.</param>
    /// <param name="page">The page texts.</param>
    /// <param name="theme">The look, with the diagram's skin parameters applied.</param>
    /// <param name="reports">The reports the parser found.</param>
    /// <param name="keywordLocation">The location the diagram type was detected at.</param>
    public ActivityModel(
        ActivityList root,
        IReadOnlyList<ActivityLane> lanes,
        PageTexts page,
        SkinTheme theme,
        IReadOnlyList<PlantUmlPendingReport> reports,
        PlantUmlLocation keywordLocation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(reports);
        Root = root;
        Lanes = lanes;
        Page = page;
        Theme = theme;
        Reports = reports;
        KeywordLocation = keywordLocation;
        var counts = new Counts();
        counts.Visit(root);
        NodeCount = counts.Nodes;
        EdgeCount = counts.Edges;
        SubgraphCount = lanes.Count + counts.Groups;
    }

    /// <summary>
    ///     Gets the instructions of the diagram.
    /// </summary>
    public ActivityList Root { get; }

    /// <summary>
    ///     Gets the swimlanes in order of declaration; empty when the diagram has none.
    /// </summary>
    public IReadOnlyList<ActivityLane> Lanes { get; }

    /// <summary>
    ///     Gets the page texts.
    /// </summary>
    public PageTexts Page { get; }

    /// <summary>
    ///     Gets the look, with the diagram's skin parameters applied.
    /// </summary>
    public SkinTheme Theme { get; }

    /// <summary>
    ///     Gets the location the diagram type was detected at (where layout failures are located).
    /// </summary>
    public PlantUmlLocation KeywordLocation { get; }

    /// <summary>
    ///     Gets the diagram's own scale (its <c>scale</c> statement); 1 by default.
    /// </summary>
    public double Scale { get; init; } = 1;

    /// <inheritdoc/>
    public string? Title => Page.Title?.Text;

    /// <inheritdoc/>
    public int NodeCount { get; }

    /// <inheritdoc/>
    public int EdgeCount { get; }

    /// <inheritdoc/>
    public int SubgraphCount { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PlantUmlPendingReport> Reports { get; }

    /// <inheritdoc/>
    public Scene Layout(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var geometry = ActivityLayout.Run(this, context);
        var body = ActivityRenderer.Render(this, geometry);
        var scene = ScenePage.Decorate(body, Page, Theme, context.Text, context.Sink, PageMetrics.Activity);
        return scene with { Background = Theme.Background, Scale = Scale };
    }

    /// <summary>
    ///     Counts the nodes, edges and groups of an instruction tree.
    /// </summary>
    private sealed class Counts
    {
        /// <summary>
        ///     Gets the number of nodes.
        /// </summary>
        public int Nodes { get; private set; }

        /// <summary>
        ///     Gets the number of edges.
        /// </summary>
        public int Edges { get; private set; }

        /// <summary>
        ///     Gets the number of groups.
        /// </summary>
        public int Groups { get; private set; }

        /// <summary>
        ///     Counts an instruction and what it holds.
        /// </summary>
        /// <param name="instruction">The instruction.</param>
        public void Visit(ActivityInstruction instruction)
        {
            switch (instruction)
            {
                case ActivityList list:
                    Edges += Math.Max(0, list.Items.Count - 1);
                    foreach (var item in list.Items)
                    {
                        Visit(item);
                    }

                    break;
                case ActivityIf branches:
                    Nodes += branches.Thens.Count;
                    foreach (var branch in branches.Thens.Append(branches.Else).OfType<ActivityBranch>())
                    {
                        Edges += 2;
                        Visit(branch.List);
                    }

                    break;
                case ActivityWhile loop:
                    Nodes += 1 + (loop.Backward is null ? 0 : 1) + (loop.SpecialOut is null ? 0 : 1);
                    Edges += 3;
                    Visit(loop.Body);
                    break;
                case ActivityRepeat repeat:
                    Nodes += 1 + (repeat.Backward is null ? 0 : 1);
                    Edges += 3;
                    Visit(repeat.Body);
                    break;
                case ActivityFork fork:
                    VisitBranches(fork.Branches);
                    break;
                case ActivitySplit split:
                    VisitBranches(split.Branches);
                    break;
                case ActivityGroup group:
                    Groups += group.Framed ? 1 : 0;
                    Visit(group.Body);
                    break;
                default:
                    Nodes++;
                    break;
            }
        }

        /// <summary>
        ///     Counts the branches of a fork or split.
        /// </summary>
        /// <param name="branches">The branches.</param>
        private void VisitBranches(IReadOnlyList<ActivityList> branches)
        {
            foreach (var branch in branches)
            {
                Edges += 2;
                Visit(branch);
            }
        }
    }
}
