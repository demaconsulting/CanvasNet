using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.GitGraph;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.GitGraph;

/// <summary>
///     Unit tests for <c>GitGraphRenderer</c>: the branch and commit positions in each direction, the shared state of the
///     two passes, the rerouting lanes, the browser's bounding box of the view box, the title, the paint of the style
///     sheet, the work meter and cancellation. The expected numbers are those Mermaid draws in Chrome (the seeded
///     reference renders).
/// </summary>
public sealed class GitGraphRendererTests
{
    /// <summary>The text of a two-commit graph with a feature branch.</summary>
    private const string BranchText = "gitGraph\n  commit\n  branch dev\n  commit\n";

    /// <summary>
    ///     Validates the branch positions: 50 pixels apart, 40 more with rotated commit labels, and top to bottom half the
    ///     width of the branch label more.
    /// </summary>
    [Fact]
    public void GitGraphRenderer_Draw_BranchPositions_FollowTheLabelsAndRotation()
    {
        // Act
        var rotated = BranchLines(Layout(BranchText));
        var straight = BranchLines(Layout("---\nconfig:\n  gitGraph:\n    rotateCommitLabel: false\n---\n" + BranchText));
        var topToBottom = BranchLines(Layout("gitGraph TB:\n  commit\n  branch dev\n  commit\n"));

        // Assert
        Assert.Equal([-2.0, 88.0], rotated.Select(line => line.Segments[0].Y));
        Assert.Equal([-2.0, 48.0], straight.Select(line => line.Segments[0].Y));
        Assert.Equal([0, 90 + (34.78125 / 2)], topToBottom.Select(line => line.Segments[0].X));
    }

    /// <summary>
    ///     Validates the commit positions without <c>parallelCommits</c>: 50 pixels apart from an offset of 10 left to
    ///     right, from 40 top to bottom, and in reverse order bottom to top.
    /// </summary>
    [Fact]
    public void GitGraphRenderer_Draw_CommitPositions_StepFiftyInEachDirection()
    {
        // Act
        var leftToRight = Centers(Layout("gitGraph\n  commit id: \"a\"\n  commit id: \"b\"\n"));
        var topToBottom = Centers(Layout("gitGraph TB:\n  commit id: \"a\"\n  commit id: \"b\"\n"));
        var bottomToTop = Centers(Layout("gitGraph BT:\n  commit id: \"a\"\n  commit id: \"b\"\n"));

        // Assert
        Assert.Equal([("a", 10.0, -2.0), ("b", 60.0, -2.0)], leftToRight);
        Assert.Equal([("a", 0.0, 40.0), ("b", 0.0, 90.0)], topToBottom);
        Assert.Equal([("b", 0.0, 40.0), ("a", 0.0, 90.0)], bottomToTop);
    }

    /// <summary>
    ///     Validates the state the two passes share, as Mermaid's module state: with <c>parallelCommits</c> and an id used
    ///     again, the second pass places a commit from the first pass's place of its parent, while the arrows keep the
    ///     first pass's places (Mermaid draws the bullet at 60 and its arrow to 10); the view box is Mermaid's.
    /// </summary>
    [Fact]
    public void GitGraphRenderer_Draw_ReusedIdWithParallelCommits_ArrowsKeepTheFirstPass()
    {
        // Act
        var scene = Layout("---\nconfig:\n  gitGraph:\n    parallelCommits: true\n---\ngitGraph\n  commit id: \"p\"\n  branch dev\n  commit id: \"q\"\n  checkout main\n  commit id: \"p\"\n");

        // Assert
        Assert.Contains(("q", 60.0, 88.0), Centers(scene));
        var arrow = Items<ScenePath>(scene, "git-arrow").Single(path => path.Id == "p->q");
        Assert.Equal((10.0, 88.0), (arrow.Segments[^1].X, arrow.Segments[^1].Y));
        AssertViewBox(scene, -95.78125, -20.5, 173.78125, 145.15701293945312);
    }

    /// <summary>
    ///     Validates that bottom to top with <c>parallelCommits</c>, a commit whose parent's id is used again later fails as
    ///     Mermaid's placement fails, located at the commit.
    /// </summary>
    [Fact]
    public void GitGraphRenderer_Draw_ReusedIdBottomToTopParallel_FailsAsMermaid()
    {
        // Arrange
        var document = MermaidDocument.Parse("---\nconfig:\n  gitGraph:\n    parallelCommits: true\n---\ngitGraph BT:\n  commit id: \"p\"\n  branch dev\n  commit id: \"q\"\n  checkout main\n  commit id: \"p\"\n");

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.LayoutScene(null, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.RenderFailed, 9), (exception.Code, exception.Location.Line));
        Assert.Contains("Closest parent not found for commit q", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the rerouting lanes of the reference fixture: each rerouted arrow runs on a new lane 10 pixels from
    ///     the others, the fifth try's candidate used without being taken (Mermaid's <c>findLane</c>), and the bends of
    ///     radius 10.
    /// </summary>
    [Fact]
    public void GitGraphRenderer_Draw_ReroutedArrows_FindMermaidsLanes()
    {
        // Act
        var scene = Layout(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gitgraph", "04-rerouting.mmd")));
        var rerouted = Items<ScenePath>(scene, "git-arrow").Where(path => path.Segments.Any(s => s.Kind == SceneSegmentKind.ArcTo && s.X1 < 15)).ToList();

        // Assert
        Assert.Equal([43, 26.8, 16.432, 9.796479999999999, 70], rerouted.Select(path => path.Segments[3].Y));
        Assert.Equal(-0.20352000000000103, rerouted[3].Segments[1].Y);
    }

    /// <summary>
    ///     Validates the view box of a graph without commits: the main branch's label and its line of no length, grown by
    ///     the padding, as Mermaid draws it.
    /// </summary>
    [Fact]
    public void GitGraphRenderer_Draw_EmptyGraph_HasMermaidsViewBox()
    {
        // Act
        var scene = Layout("gitGraph\n");

        // Assert
        AssertViewBox(scene, -95.78125, -20.5, 103.78125, 37);
    }

    /// <summary>
    ///     Validates empty texts: an empty branch name gives a label rectangle of 18 by 4 pixels and no text, and an empty
    ///     tag a tag shape without text; neither text counts in the view box, which is Mermaid's.
    /// </summary>
    [Fact]
    public void GitGraphRenderer_Draw_EmptyNameAndTag_HaveNoTextBox()
    {
        // Act
        var scene = Layout("gitGraph\n  commit id: \"A\"\n  branch \"\"\n  commit id: \"B\" tag: \"\"\n");

        // Assert
        var labels = Items<ScenePath>(scene, "git-branch-label");
        var rect = labels[^1];
        Assert.Equal(new SceneRect(-53, 86, 18, 4), Scene.BoundsOf([rect]));
        Assert.Empty(Items<SceneText>(scene, "git-tag"));
        AssertViewBox(scene, -95.78125, -20.5, 203.78125, 145.85057067871094);
    }

    /// <summary>
    ///     Validates the title: a title statement without a value keeps the frontmatter title, which is centered on the
    ///     bounding box of the drawing, its baseline <c>titleTopMargin</c> above the top, as Mermaid draws it.
    /// </summary>
    [Fact]
    public void GitGraphRenderer_Draw_FrontmatterTitle_IsCenteredOverTheDrawing()
    {
        // Act
        var scene = Layout("---\ntitle: Front\n---\ngitGraph\n  title\n  commit id: \"A\"\n");

        // Assert
        var title = Items<SceneText>(scene, "title").Single();
        Assert.Equal(("Front", -18.890625, -25.0, SceneTextAnchor.Middle), (title.Runs[0].Text, title.X, title.Baseline, title.Anchor));
        AssertViewBox(scene, -95.78125, -49, 153.78125, 85.04412078857422);
    }

    /// <summary>
    ///     Validates the rotation of a commit label left to right: the label's group is moved and turned -45 degrees about
    ///     the commit's place, so the text turns about the moved place.
    /// </summary>
    [Fact]
    public void GitGraphRenderer_Draw_RotatedLabel_TurnsAboutTheMovedCommitPlace()
    {
        // Act
        var scene = Layout("gitGraph\n  commit id: \"A\"\n");

        // Assert: the label of A is 8 pixels wide, so it moves by (-7.5 - 18 / 25 * 9.5, 10 + 8 / 25 * 8.5)
        var text = Items<SceneText>(scene, "git-commit-label").Single();
        var (dx, dy) = (-7.5 - (18.0 / 25 * 9.5), 10 + (8.0 / 25 * 8.5));
        Assert.Equal(new SceneRotation(-45, 0 + dx, -2 + dy), text.Rotation);
        Assert.Equal((6 + dx, 23 + dy), (text.X, text.Baseline));
    }

    /// <summary>
    ///     Validates the paint of the style sheet: the cherries of a cherry-pick are white, black in the dark theme; a
    ///     commit id that is a class of the sheet is styled by it; the <c>neo</c> look shadows the branch labels.
    /// </summary>
    [Fact]
    public void GitGraphRenderer_Draw_Paint_FollowsTheStyleSheet()
    {
        // Arrange
        const string Picks = "gitGraph\n  commit id: \"A\"\n  branch dev\n  commit id: \"B\"\n  checkout main\n  cherry-pick id: \"B\"\n";

        // Act
        var light = Items<ScenePath>(Layout(Picks), "git-commit").Where(path => path.Id == "2-eb28011").ToList();
        var dark = Items<ScenePath>(Layout("---\nconfig:\n  theme: dark\n---\n" + Picks), "git-commit").Where(path => path.Id == "2-eb28011").ToList();
        var styled = Items<ScenePath>(Layout("gitGraph\n  commit id: \"commit-reverse\"\n"), "git-commit").Single();
        var neo = Items<ScenePath>(Layout("---\nconfig:\n  look: neo\n---\ngitGraph\n  commit\n"), "git-branch-label").Single();

        // Assert
        Assert.Equal(5, light.Count);
        Assert.Equal(CssColor.Parse("#fff"), light[1].Fill);
        Assert.Equal(CssColor.Parse("#000000"), dark[1].Fill);
        Assert.Equal(CssColor.Parse("#fff"), light[3].Stroke!.Color);
        Assert.Equal(3, styled.Stroke!.Width);
        Assert.NotNull(neo.Shadow);
    }

    /// <summary>
    ///     Validates the main branch of a render's configuration: a site configuration that renames the main branch
    ///     draws its label with the new name.
    /// </summary>
    [Fact]
    public void GitGraphRenderer_Draw_SiteMainBranchName_RunsTheStatementsAgain()
    {
        // Act
        var scene = Layout(BranchText, new MermaidRenderOptions { Config = "{\"gitGraph\": {\"mainBranchName\": \"trunk\"}}" });

        // Assert
        Assert.Equal(["trunk", "dev"], Items<SceneText>(scene, "git-branch-label").Select(text => text.Runs[0].Text));
    }

    /// <summary>
    ///     Validates the work meter and cancellation: a work limit too small for the drawing is a limit error, and a
    ///     canceled render stops.
    /// </summary>
    [Fact]
    public void GitGraphRenderer_Draw_WorkLimitAndCancellation_StopTheRender()
    {
        // Arrange
        var document = MermaidDocument.Parse(BranchText);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act
        var limited = Assert.Throws<MermaidRenderException>(() => document.LayoutScene(new MermaidRenderOptions { Limits = new MermaidLimits { MaxLayoutWork = 20 } }, TestContext.Current.CancellationToken));
        var stopped = Record.Exception(() => document.LayoutScene(null, canceled.Token));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, limited.Code);
        Assert.IsAssignableFrom<OperationCanceledException>(stopped);
    }

    /// <summary>
    ///     Validates the helpers of the texts: the rows of a branch label (split at line feeds, written <c>\n</c> and
    ///     <c>&lt;br&gt;</c> tags, trimmed, empty rows kept) and the decoding of Mermaid's entity placeholders.
    /// </summary>
    [Fact]
    public void GitGraphRenderer_TextHelpers_SplitRowsAndDecodePlaceholders()
    {
        // Act
        var rows = GitGraphRenderer.Rows("a <BR/> b\\nc\n<br><br>d");
        var decoded = GitGraphRenderer.Decode("Fish \ufb02\u00b0\u00b035\u00b6\u00df Chips");

        // Assert
        Assert.Equal(["a", "b", "c", string.Empty, string.Empty, "d"], rows);
        Assert.Equal("Fish # Chips", decoded);
    }

    /// <summary>
    ///     Lays a text out.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="options">The options, or null.</param>
    /// <returns>The scene.</returns>
    private static Scene Layout(string text, MermaidRenderOptions? options = null) => SceneQuery.Layout(text, options);

    /// <summary>
    ///     Gets the items of a role and type in paint order.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The items.</returns>
    private static List<T> Items<T>(Scene scene, string role)
        where T : SceneItem =>
        [.. scene.Walk().Select(entry => entry.Item).OfType<T>().Where(item => item.Role == role)];

    /// <summary>
    ///     Gets the branch lines.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The lines in drawing order.</returns>
    private static List<ScenePath> BranchLines(Scene scene) => Items<ScenePath>(scene, "git-branch");

    /// <summary>
    ///     Gets the center of each commit's first bullet shape, in drawing order, rounded to 0.001 pixel.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The commit ids and centers.</returns>
    private static List<(string Id, double X, double Y)> Centers(Scene scene) =>
    [
        .. Items<ScenePath>(scene, "git-commit")
            .GroupBy(path => path.Id!)
            .Select(group => Scene.BoundsOf([group.First()])!.Value)
            .Zip(Items<ScenePath>(scene, "git-commit").Select(path => path.Id!).Distinct(), (box, id) => (id, Math.Round(box.X + (box.Width / 2), 3), Math.Round(box.Y + (box.Height / 2), 3))),
    ];

    /// <summary>
    ///     Asserts a view box within 0.01 pixel.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    private static void AssertViewBox(Scene scene, double x, double y, double width, double height)
    {
        var box = scene.ViewBox;
        Assert.True(
            Math.Abs(box.X - x) <= 0.01 && Math.Abs(box.Y - y) <= 0.01 && Math.Abs(box.Width - width) <= 0.01 && Math.Abs(box.Height - height) <= 0.01,
            $"view box {box}, expected ({x}, {y}, {width}, {height})");
    }
}
