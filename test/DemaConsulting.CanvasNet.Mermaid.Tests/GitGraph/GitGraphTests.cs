using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.GitGraph;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.GitGraph;

/// <summary>
///     Subsystem tests of the GitGraph subsystem (<c>GitGraphParser</c> and <c>GitGraphRenderer</c> together): every git
///     graph fixture parses to a diagram whose commits and branches the drawing shows, a whole text gives the diagram and
///     the scene, invalid text fails closed, and the configuration and themes change the drawing.
/// </summary>
public sealed class GitGraphTests
{
    /// <summary>
    ///     Gets the names of the git graph fixtures.
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gitgraph"), "*.mmd").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    /// <summary>
    ///     Validates that a fixture parses to a git graph and lays out with one bullet group per commit, one arrow per
    ///     parent link, the branches it shows, every item inside the view box, the same way twice.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMermaid_GitGraph_Fixture_ParsesAndLaysOutConsistently(string name)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gitgraph", name + ".mmd"));
        var document = MermaidDocument.Parse(text);

        // Act
        var scene = SceneQuery.Layout(text);
        var again = SceneQuery.Layout(text);

        // Assert
        var diagram = Assert.IsType<GitGraphDiagram>(document.Diagram);
        var items = scene.Walk().Select(entry => entry.Item).ToList();
        Assert.Equal(
            diagram.OpenDatabase.Commits.Select(commit => commit.Id).Order(StringComparer.Ordinal),
            items.Where(item => item.Role == "git-commit").Select(item => item.Id!).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(document.EdgeCount, items.Count(item => item.Role == "git-arrow"));
        var bounds = Scene.BoundsOf(scene.Items)!.Value;
        var view = scene.ViewBox;
        Assert.True(bounds.X >= view.X && bounds.Y >= view.Y && bounds.Right <= view.Right + 1e-6 && bounds.Bottom <= view.Bottom + 1e-6, $"{bounds} outside {view}");
        Assert.Equal(scene.ViewBox, again.ViewBox);
        Assert.Equal(items.Count, again.Walk().Count());
    }

    /// <summary>
    ///     Validates the whole pipeline on a text: the diagram's metadata and title, the branch labels in their order,
    ///     the commit labels (a merge without an id has none), the tags and the arrows of the scene.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GitGraph_Text_GivesTheDiagramAndTheScene()
    {
        // Arrange
        const string Text = "gitGraph\n  title Release\n  commit id: \"A\" tag: \"v1\"\n  branch dev order: 1\n  commit id: \"B\"\n  checkout main\n  merge dev\n  commit id: \"C\"\n";

        // Act
        var document = MermaidDocument.Parse(Text);
        var scene = SceneQuery.Layout(Text);

        // Assert
        Assert.Equal(("gitGraph", 4, 4, "LR", "Release"), (document.DiagramType, document.NodeCount, document.EdgeCount, document.Direction, document.Title));
        var texts = scene.Walk().Select(entry => entry.Item).OfType<SceneText>().ToList();
        string[] Of(string role) => [.. texts.Where(text => text.Role == role).Select(text => text.Runs[0].Text)];
        Assert.Equal(["main", "dev"], Of("git-branch-label"));
        Assert.Equal(["A", "B", "C"], Of("git-commit-label"));
        Assert.Equal(["v1"], Of("git-tag"));
        Assert.Equal(["Release"], Of("title"));
        Assert.Equal(["A->B", "A->2-eb28011", "B->2-eb28011", "2-eb28011->C"], SceneQuery.OfRole(scene, "git-arrow").Select(item => item.Id));
    }

    /// <summary>
    ///     Validates that a text the grammar does not accept, an operation Mermaid's database rejects, and more commits
    ///     than the open limit allows are refused when the document is opened, located in the original text (frontmatter
    ///     lines included) with a hint.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GitGraph_InvalidText_FailsClosedWithLocation()
    {
        // Arrange
        var options = new MermaidOpenOptions { SourceName = "g.mmd" };
        var limited = new MermaidOpenOptions { Limits = new MermaidLimits { MaxNodes = 3 } };

        // Act
        var grammar = Assert.Throws<MermaidSyntaxException>(() =>
            MermaidDocument.Parse("---\ntitle: T\n---\ngitGraph\n    commit\n    commit id: a\n", options));
        var rejected = Assert.Throws<MermaidSyntaxException>(() =>
            MermaidDocument.Parse("gitGraph\n    commit\n    checkout dev\n", options));
        var limit = Assert.Throws<MermaidSyntaxException>(() =>
            MermaidDocument.Parse("gitGraph\n    commit\n    commit\n    commit\n", limited));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, "g.mmd", 6), (grammar.Code, grammar.Location.File, grammar.Location.Line));
        Assert.Equal(["STRING"], grammar.Expected);
        Assert.NotNull(grammar.Hint);
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, 3), (rejected.Code, rejected.Location.Line));
        Assert.Contains("Trying to checkout branch which is not yet created. (Help try using \"branch dev\")", rejected.Message, StringComparison.Ordinal);
        Assert.NotNull(rejected.Hint);
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 4), (limit.Code, limit.Location.Line));
    }

    /// <summary>
    ///     Validates that the configuration changes the drawing: hidden branches and commit labels, labels that are not
    ///     rotated, and a direction from top to bottom.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GitGraph_Configuration_ChangesTheDrawing()
    {
        // Arrange
        var document = MermaidDocument.Parse("gitGraph\n  commit id: \"A\"\n  commit id: \"B\"\n");
        var hidden = new MermaidRenderOptions { Config = "{\"gitGraph\": {\"showBranches\": false, \"showCommitLabel\": false}}" };
        var straight = new MermaidRenderOptions { Config = "{\"gitGraph\": {\"rotateCommitLabel\": false}}" };

        // Act
        var plain = document.LayoutScene(null, TestContext.Current.CancellationToken);
        var bare = document.LayoutScene(hidden, TestContext.Current.CancellationToken);
        var level = document.LayoutScene(straight, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(SceneQuery.OfRole(plain, "git-branch"));
        Assert.Empty(SceneQuery.OfRole(bare, "git-branch"));
        Assert.Empty(SceneQuery.OfRole(bare, "git-commit-label"));
        Assert.All(plain.Walk().Select(entry => entry.Item).OfType<SceneText>().Where(text => text.Role == "git-commit-label"), text => Assert.NotNull(text.Rotation));
        Assert.All(level.Walk().Select(entry => entry.Item).OfType<SceneText>().Where(text => text.Role == "git-commit-label"), text => Assert.Null(text.Rotation));
    }

    /// <summary>
    ///     Validates the paint of the drawing in the default and the dark theme: the first branch's commit color and the
    ///     dashed branch line.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GitGraph_Themes_PaintTheCommitsAndBranches()
    {
        // Arrange
        var document = MermaidDocument.Parse("gitGraph\n  commit id: \"A\"\n");
        var dark = new MermaidRenderOptions { Theme = "dark" };

        // Act
        var light = document.LayoutScene(null, TestContext.Current.CancellationToken);
        var darkScene = document.LayoutScene(dark, TestContext.Current.CancellationToken);

        // Assert
        static ScenePath Bullet(Scene scene) => Assert.IsType<ScenePath>(Assert.Single(SceneQuery.OfRole(scene, "git-commit")));
        Assert.Equal(new Rgba32(0, 0, 236, 255), Bullet(light).Fill);
        Assert.NotEqual(Bullet(light).Fill, Bullet(darkScene).Fill);
        var line = Assert.IsType<ScenePath>(Assert.Single(SceneQuery.OfRole(light, "git-branch")));
        Assert.Equal([2.0, 2.0], line.Stroke!.Dash);
    }
}
