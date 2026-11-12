using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of git graphs through the public API: every fixture renders strictly, the metadata is reported, the
///     pixels carry the theme's commit colors, the generated commit ids are the same in every render, failures and limits
///     are located, a site configuration's main branch is used, and rendering is deterministic and can be canceled.
/// </summary>
public class MermaidGitGraphIntegrationTests
{
    /// <summary>A small graph: two branches, a merge and a tag.</summary>
    private const string Simple = "gitGraph\n    commit\n    branch develop\n    commit tag: \"v1\"\n    checkout main\n    merge develop\n    commit\n";

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
    ///     Validates that every git graph fixture renders in strict mode, gives no report, measures as it renders, and
    ///     draws something.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMermaid_GitGraph_Fixture_RendersStrictly(string name)
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gitgraph", name + ".mmd"));
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(("gitGraph", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates the metadata of a git graph: the commits are the nodes, the parent links the edges, there are no
    ///     subgraphs, the direction is reported, and the title and accessible title are reported; the frontmatter title is
    ///     the fallback.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GitGraph_Metadata_CountsCommitsAndParentLinks()
    {
        // Arrange
        const string Text = "gitGraph BT:\n    title History\n    accTitle: A history\n    commit\n    branch dev\n    commit\n    checkout main\n    merge dev\n";

        // Act
        var document = MermaidDocument.Parse(Text);
        var fallback = MermaidDocument.Parse("---\ntitle: Front\n---\ngitGraph\n  commit\n");

        // Assert
        Assert.Equal(("gitGraph", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((3, 3, 0, "BT"), (document.NodeCount, document.EdgeCount, document.SubgraphCount, document.Direction));
        Assert.Equal(("History", "A history"), (document.Title, document.AccTitle));
        Assert.Equal(("Front", "LR"), (fallback.Title, fallback.Direction));
    }

    /// <summary>
    ///     Validates the pixels: the default theme's commit color of the main branch inside the first commit's bullet,
    ///     another color in the dark theme.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GitGraph_Pixels_CarryTheThemeColors()
    {
        // Arrange
        const string Text = "gitGraph\n  commit id: \"A\"\n";
        var document = MermaidDocument.Parse(Text);
        var dark = MermaidDocument.Parse("%%{init: {'theme': 'dark'}}%%\n" + Text);
        var options = new MermaidRenderOptions { Background = new Rgba32(255, 255, 255, 255) };

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        using var darkSurface = dark.Render(options, TestContext.Current.CancellationToken);

        // Assert: the view box starts at (-95.78125, -20.5), so the commit's center (10, -2) is at about (105.8, 18.5)
        Assert.Equal(new Rgba32(0, 0, 236, 255), surface.GetRowSpan(18)[105]);
        Assert.NotEqual(surface.GetRowSpan(18)[105], darkSurface.GetRowSpan(18)[105]);
    }

    /// <summary>
    ///     Validates that the ids generated for commits without one are the same in every render and every document, so
    ///     the pictures are deterministic.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GitGraph_GeneratedIds_AreDeterministic()
    {
        // Arrange
        var first = MermaidDocument.Parse(Simple);
        var second = MermaidDocument.Parse(Simple);

        // Act
        using var a = first.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var b = second.Render(cancellationToken: TestContext.Current.CancellationToken);
        var labels = SceneQuery.OfRole(first.LayoutScene(null, TestContext.Current.CancellationToken), "git-commit-label").Select(item => item.Id).Distinct();

        // Assert
        Assert.True(MermaidTestHelpers.PixelsEqual(a, b));
        Assert.Equal(["0-eb28011", "1-cc99105", "3-4bc15df"], labels);
    }

    /// <summary>
    ///     Validates that an operation Mermaid's database rejects fails at open, located at its statement, with Mermaid's
    ///     message.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GitGraph_RejectedMerge_FailsLocated()
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse("gitGraph\n  commit\n  merge feature\n", new MermaidOpenOptions { SourceName = "graph.mmd" }));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
        Assert.Equal(("graph.mmd", 3, 3), (exception.Location.File, exception.Location.Line, exception.Location.Column));
        Assert.Contains("Incorrect usage of \"merge\". Branch to be merged (feature) does not exist", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the main branch of a site configuration: the render runs the statements with the site's main branch
    ///     name, and a statement that then fails is a located render failure.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GitGraph_SiteMainBranch_IsUsedWhenDrawn()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var trunk = new MermaidRenderOptions { Config = "{\"gitGraph\": {\"mainBranchName\": \"trunk\"}}" };

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(trunk, TestContext.Current.CancellationToken));
        var plain = MermaidDocument.Parse("gitGraph\n  commit\n").LayoutScene(trunk, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.RenderFailed, 5), (exception.Code, exception.Location.Line));
        Assert.Contains("(Help try using \"branch main\")", exception.Message, StringComparison.Ordinal);
        Assert.Equal(["trunk"], SceneQuery.OfRole(plain, "git-branch").Select(item => item.Id));
    }

    /// <summary>
    ///     Validates that more commits than the node limit allows fail at open, and a work limit too small for the drawing
    ///     fails the render, both with the limit code.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GitGraph_Limits_FailWithLimitExceeded()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);

        // Act
        var nodes = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(Simple, new MermaidOpenOptions { Limits = new MermaidLimits { MaxNodes = 3 } }));
        var work = Assert.Throws<MermaidRenderException>(() => document.Measure(new MermaidRenderOptions { Limits = new MermaidLimits { MaxLayoutWork = 50 } }, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 4), (nodes.Code, nodes.Location.Line));
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, work.Code);
    }

    /// <summary>
    ///     Validates that repeated and concurrent renders of a document give identical pixels.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GitGraph_RepeatedAndConcurrentRenders_AreIdentical()
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gitgraph", "04-rerouting.mmd"));
        using var first = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var second = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var concurrent = Enumerable.Range(0, 6).AsParallel().Select(_ =>
        {
            using var surface = document.Render();
            return MermaidTestHelpers.PixelsEqual(first, surface);
        }).ToList();

        // Assert
        Assert.True(MermaidTestHelpers.PixelsEqual(first, second));
        Assert.All(concurrent, Assert.True);
    }

    /// <summary>
    ///     Validates that a canceled token stops a git graph render.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GitGraph_CanceledToken_Throws()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }
}
