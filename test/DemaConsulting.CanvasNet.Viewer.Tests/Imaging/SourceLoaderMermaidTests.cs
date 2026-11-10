using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the Mermaid source of the SourceLoader class: probing a diagram, drawing it at its natural and at a
///     requested size, its theme and background, the notes and strict mode, and the mapping of the library's failures.
/// </summary>
public class SourceLoaderMermaidTests
{
    /// <summary>The fill of a node in Mermaid's default theme (<c>mainBkg</c>).</summary>
    private static readonly Rgba32 DefaultNodeFill = new(0xEC, 0xEC, 0xFF, 255);

    /// <summary>The fill of a node in Mermaid's dark theme (<c>mainBkg</c>).</summary>
    private static readonly Rgba32 DarkNodeFill = new(0x1F, 0x20, 0x20, 255);

    /// <summary>The white the tool draws a diagram over by default.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     Copies a fixture diagram into a directory.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="name">The file name within <c>Fixtures/mermaid</c>.</param>
    /// <returns>The path of the copy.</returns>
    private static string CopyFixture(TempDirectory directory, string name)
    {
        var path = directory.Combine(name);
        File.Copy(FixtureFactory.FixturePath(Path.Combine("mermaid", name)), path);
        return path;
    }

    /// <summary>
    ///     Counts the pixels of a surface that have a color.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="color">The color.</param>
    /// <returns>The number of pixels.</returns>
    private static int Count(Surface surface, Rgba32 color) =>
        Enumerable.Range(0, surface.Height).Sum(y => Enumerable.Range(0, surface.Width).Count(x => surface[x, y] == color));

    /// <summary>
    ///     Test that a flowchart is probed without drawing it: its natural size and the facts of its text.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_Mermaid_ReportsDiagramFacts()
    {
        // Arrange: a flowchart with frontmatter, an accessible title, a subgraph, and a class
        using var directory = new TempDirectory();
        var path = CopyFixture(directory, "shipping.mermaid");

        // Act
        var info = SourceLoader.Probe(path);

        // Assert: the kind, a single page with transparency, the size the library measures, and the facts
        Assert.Equal(SourceKind.Mermaid, info.Format);
        Assert.Equal((1, 4, true), (info.PageCount, info.Channels, info.HasAlpha));
        var measured = MermaidDocument.Open(path).Measure(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal((measured.Width, measured.Height), (info.Width, info.Height));
        Assert.Equal(new DiagramFacts("flowchart", true, "TB", "Shipping", "Shipping steps", 3, 2, 1), info.Diagram);
        Assert.Null(info.Chart);
        Assert.Empty(info.Modules);
    }

    /// <summary>
    ///     Test that a diagram is drawn at its natural size over white, with the fill of the default theme, a render operation
    ///     that says so, and no note.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_Mermaid_DrawsAtTheNaturalSizeOverWhite()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyFixture(directory, "flow.mmd");
        var info = SourceLoader.Probe(path);

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions { Strict = true });

        // Assert: the size, the white corner, node fills of the default theme, and the operation
        Assert.Equal((info.Width, info.Height), (loaded.Surface.Width, loaded.Surface.Height));
        Assert.True(loaded.IsVector);
        Assert.Equal(White, loaded.Surface[0, 0]);
        Assert.True(Count(loaded.Surface, DefaultNodeFill) > 500);
        Assert.Empty(loaded.Diagnostics);
        var render = Assert.Single(loaded.Operations);
        Assert.Equal("render", render.Op);
        Assert.Equal(
            [("width", (object)info.Width), ("height", info.Height), ("background", "#FFFFFF")],
            render.Fields.Select(f => (f.Key, f.Value)));
    }

    /// <summary>
    ///     Test that the size options draw the diagram sharply at the requested size, and that a background with zero alpha keeps
    ///     the transparency of Mermaid's own picture.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidSizeAndBackground_FollowTheOptions()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyFixture(directory, "flow.mmd");
        var info = SourceLoader.Probe(path);

        // Act
        using var scaled = SourceLoader.Load(path, new SourceOptions { Scale = 2 });
        using var wide = SourceLoader.Load(path, new SourceOptions { Width = 300 });
        using var boxed = SourceLoader.Load(path, new SourceOptions { Width = 400, Height = 400 });
        using var transparent = SourceLoader.Load(path, new SourceOptions { Background = new Rgba32(0, 0, 0, 0) });

        // Assert: twice the size, the width with the aspect ratio, the exact box, and a transparent corner
        Assert.Equal((info.Width * 2, info.Height * 2), (scaled.Surface.Width, scaled.Surface.Height));
        Assert.Equal(300, wide.Surface.Width);
        Assert.Equal((int)Math.Round((double)info.Height * 300 / info.Width, MidpointRounding.AwayFromZero), wide.Surface.Height);
        Assert.Equal((400, 400), (boxed.Surface.Width, boxed.Surface.Height));
        Assert.Equal(White, boxed.Surface[200, 2]);
        Assert.Equal(0, transparent.Surface[0, 0].A);
        Assert.True(Count(transparent.Surface, DefaultNodeFill) > 500);
    }

    /// <summary>
    ///     Test that the theme of the options is applied (a dark node fill) and recorded, and that a name that is not a Mermaid
    ///     theme is a usage error before the file is read.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidTheme_IsAppliedAndValidated()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyFixture(directory, "flow.mmd");

        // Act
        using var dark = SourceLoader.Load(path, new SourceOptions { Mermaid = new DiagramOptions { Theme = "dark" } });
        var unknown = Assert.Throws<ViewerException>(() => SourceLoader.Load(path, new SourceOptions { Mermaid = new DiagramOptions { Theme = "Dark" } }));
        var missing = Assert.Throws<ViewerException>(
            () => SourceLoader.Load(directory.Combine("none.mmd"), new SourceOptions { Mermaid = new DiagramOptions { Theme = "midnight" } }));

        // Assert: the dark fill and no default fill; the theme in the operation; the canonical names only, checked first
        Assert.True(Count(dark.Surface, DarkNodeFill) > 500);
        Assert.Equal(0, Count(dark.Surface, DefaultNodeFill));
        Assert.Contains(dark.Operations.Single().Fields, f => f.Key == "theme" && (string)f.Value == "dark");
        Assert.Equal(ErrorCodes.UsageError, unknown.Code);
        Assert.Contains("default, base, dark, forest, neutral", unknown.Hint);
        Assert.Equal(ErrorCodes.UsageError, missing.Code);
    }

    /// <summary>
    ///     Test that a sequence diagram is probed as a supported diagram with its participants and messages, and drawn
    ///     strictly at its natural size with the participant boxes in the actor fill of the default theme and no note.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidSequence_ProbesAndDraws()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyFixture(directory, "sequence.mmd");

        // Act
        var info = SourceLoader.Probe(path);
        using var loaded = SourceLoader.Load(path, new SourceOptions { Strict = true });

        // Assert: the facts, the size, the white corner, the actor fill, and no note
        Assert.Equal(new DiagramFacts("sequence", true, null, null, null, 2, 1, 0), info.Diagram);
        Assert.Equal((info.Width, info.Height), (loaded.Surface.Width, loaded.Surface.Height));
        Assert.Equal(White, loaded.Surface[0, 0]);
        Assert.True(Count(loaded.Surface, DefaultNodeFill) > 2000);
        Assert.Empty(loaded.Diagnostics);
    }

    /// <summary>
    ///     Test that a class diagram is probed as a supported diagram with its classes, relations and direction, and drawn
    ///     strictly at its natural size with the class boxes in the node fill of the default theme and no note.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidClass_ProbesAndDraws()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyFixture(directory, "class.mmd");

        // Act
        var info = SourceLoader.Probe(path);
        using var loaded = SourceLoader.Load(path, new SourceOptions { Strict = true });

        // Assert: the facts, the size, the white corner, the box fill, and no note
        Assert.Equal(new DiagramFacts("class", true, "TB", null, null, 2, 1, 0), info.Diagram);
        Assert.Equal((info.Width, info.Height), (loaded.Surface.Width, loaded.Surface.Height));
        Assert.Equal(White, loaded.Surface[0, 0]);
        Assert.True(Count(loaded.Surface, DefaultNodeFill) > 2000);
        Assert.Empty(loaded.Diagnostics);
    }

    /// <summary>
    ///     Test that a diagram type this version does not draw (a user journey) is probed with <c>Supported</c> false and
    ///     drawn as an empty picture with one unsupported note that says it is not supported, and that strict mode makes it an
    ///     <c>unsupported-feature</c> error with the library's hint.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidUnsupportedType_IsOneNoteThenStrictError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyFixture(directory, "journey.mmd");

        // Act
        var info = SourceLoader.Probe(path);
        using var loaded = SourceLoader.Load(path, new SourceOptions());
        var strict = Assert.Throws<ViewerException>(() => SourceLoader.Load(path, new SourceOptions { Strict = true }));

        // Assert: the facts and the empty picture of 16 pixels (twice the diagram padding)
        Assert.Equal(new DiagramFacts("journey", false, null, null, null, 0, 0, 0), info.Diagram);
        Assert.Equal((16, 16), (loaded.Surface.Width, loaded.Surface.Height));
        Assert.Equal(White, loaded.Surface[8, 8]);

        // Assert: one note, located at the keyword, that says the type is not drawn yet and which types are
        var note = Assert.Single(loaded.Diagnostics);
        Assert.Equal(("mermaid", "unsupported", MermaidDiagnosticCodes.DiagramUnsupported), (note.Source, note.Severity, note.Code));
        Assert.Contains("not drawn by this version", note.Message, StringComparison.Ordinal);
        Assert.Contains("journey diagrams are not supported", note.Message, StringComparison.Ordinal);
        Assert.Contains("flowchart", note.Message, StringComparison.Ordinal);
        Assert.Equal(1, note.Location!.Line);
        Assert.DoesNotContain('\n', note.Message);

        // Assert: strict mode is an error with the hint of the library and its exception as the cause
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.Contains(MermaidDiagnosticCodes.DiagramUnsupported, strict.Message, StringComparison.Ordinal);
        Assert.Contains("not supported", strict.Hint, StringComparison.Ordinal);
        Assert.Contains("--strict", strict.Hint, StringComparison.Ordinal);
        Assert.IsType<MermaidUnsupportedFeatureException>(strict.InnerException);
    }

    /// <summary>
    ///     Test that a warning of the library is the note <c>mermaid-warning</c> with the specific code as its detail and the line
    ///     and column of the text, and that an unsupported construct of a flowchart (an icon) is its own unsupported note.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_Mermaid_MapsWarningsAndUnsupportedFeatures()
    {
        // Arrange: a style declaration outside the subset, and a Font Awesome icon
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "noisy.mmd", "flowchart LR\n    a[fa:fa-car Car] --> b\n    style b fill:#f00,text-decoration:underline\n");

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions());

        // Assert: the warning with its detail and location, and the unsupported icon
        var warning = Assert.Single(loaded.Diagnostics, d => d.Severity == "warning");
        Assert.Equal(("mermaid", "mermaid-warning", MermaidDiagnosticCodes.StyleIgnored), (warning.Source, warning.Code, warning.Detail));
        Assert.True(warning.Location!.Line > 1);
        Assert.Contains($"noisy.mmd:{warning.Location.Line}:", warning.Message, StringComparison.Ordinal);
        var unsupported = Assert.Single(loaded.Diagnostics, d => d.Severity == "unsupported");
        Assert.Equal(("mermaid", MermaidDiagnosticCodes.IconUnsupported), (unsupported.Source, unsupported.Code));
        Assert.Null(unsupported.Detail);
        Assert.All(loaded.Diagnostics, d => Assert.DoesNotContain('\n', d.Message));
    }

    /// <summary>
    ///     Test that text that is not a valid diagram is a <c>mermaid-error</c> with the location, the code and the hint of the
    ///     library, and the library's exception as the cause.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <param name="code">The <c>mermaid-*</c> code the message names.</param>
    [Theory]
    [InlineData("flowchart LR\n    a[open --> b\n", "mermaid-syntax-error")]
    [InlineData("drawing LR\n    a --> b\n", "mermaid-unknown-diagram")]
    [InlineData("flowchart LR\n    a@{ shape: hexagonal }\n", "mermaid-shape-unknown")]
    public void SourceLoader_Probe_MermaidInvalidText_IsMermaidError(string text, string code)
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "bad.mmd", text);

        // Act
        var error = Assert.Throws<ViewerException>(() => SourceLoader.Probe(path));

        // Assert
        Assert.Equal(ErrorCodes.MermaidError, error.Code);
        Assert.Equal("mermaid-error", error.Code);
        Assert.StartsWith("Mermaid syntax error: ", error.Message, StringComparison.Ordinal);
        Assert.Contains(code, error.Message, StringComparison.Ordinal);
        Assert.Contains("bad.mmd:", error.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(error.Hint));
        Assert.DoesNotContain('\n', error.Message);
        Assert.IsType<MermaidSyntaxException>(error.InnerException);
    }

    /// <summary>
    ///     Test that the limits of the library are mapped: more edges than the limit is <c>budget-exceeded</c>, and a text
    ///     longer than the text limit is <c>corrupt-input</c>.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_MermaidLimits_AreBudgetExceededAndCorruptInput()
    {
        // Arrange: one edge more than the default limit, and a diagram whose code (comments do not count) is longer than the limit
        using var directory = new TempDirectory();
        var edges = string.Join("\n", Enumerable.Range(0, MermaidLimits.Default.MaxEdges + 1).Select(i => $"    n{i} --> n{i + 1}"));
        var many = FixtureFactory.WriteText(directory.Path, "many.mmd", "flowchart LR\n" + edges + "\n");
        var huge = FixtureFactory.WriteText(directory.Path, "huge.mmd", "flowchart LR\n" + string.Concat(Enumerable.Repeat("    a\n", (MermaidLimits.Default.MaxTextSize / 6) + 10)));

        // Act
        var budget = Assert.Throws<ViewerException>(() => SourceLoader.Probe(many));
        var corrupt = Assert.Throws<ViewerException>(() => SourceLoader.Probe(huge));

        // Assert
        Assert.Equal(ErrorCodes.BudgetExceeded, budget.Code);
        Assert.Contains(MermaidDiagnosticCodes.LimitExceeded, budget.Message, StringComparison.Ordinal);
        Assert.NotNull(budget.Hint);
        Assert.Equal(ErrorCodes.CorruptInput, corrupt.Code);
        Assert.Contains("huge.mmd", corrupt.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a failure of the library during the layout or the render is mapped by its code: a limit is
    ///     <c>budget-exceeded</c> and anything else is a <c>mermaid-error</c>, each with the hint of the library.
    /// </summary>
    [Fact]
    public void SourceLoader_MapMermaid_MapsByCode()
    {
        // Arrange
        var limit = new MermaidRenderException(MermaidDiagnosticCodes.LimitExceeded, "the layout work exceeds MaxLayoutWork", new MermaidLocation("a.mmd", 1, 1), "split it");
        var failed = new MermaidRenderException(MermaidDiagnosticCodes.RenderFailed, "the layout failed", new MermaidLocation("a.mmd", 2, 3));

        // Act
        var mappedLimit = Assert.Throws<ViewerException>(() => SourceLoader.RenderDiagram(() => throw limit, "a.mmd"));
        var mappedFailed = Assert.Throws<ViewerException>(() => SourceLoader.RenderDiagram(() => throw failed, "a.mmd"));
        var mappedArgument = Assert.Throws<ViewerException>(() => SourceLoader.RenderDiagram(() => throw new ArgumentException("bad"), "a.mmd"));

        // Assert
        Assert.Equal((ErrorCodes.BudgetExceeded, "split it"), (mappedLimit.Code, mappedLimit.Hint));
        Assert.Same(limit, mappedLimit.InnerException);
        Assert.Equal(ErrorCodes.MermaidError, mappedFailed.Code);
        Assert.Equal("Mermaid render error: a.mmd:2:3: mermaid-render-failed: the layout failed", mappedFailed.Message);
        Assert.Null(mappedFailed.Hint);
        Assert.Equal(ErrorCodes.MermaidError, mappedArgument.Code);
    }

    /// <summary>
    ///     Test that a diagram whose natural size is larger than the largest image is probed with that size (the text of the
    ///     library's size message is pinned here), refused with <c>size-limit</c> at that size, and drawn with a smaller scale.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidLargerThanMaximum_NeedsASizeOption()
    {
        // Arrange: a long chain from left to right, wider than the largest image
        using var directory = new TempDirectory();
        var chain = string.Join(" --> ", Enumerable.Range(0, 120).Select(i => $"n{i}[Step {i}]"));
        var path = FixtureFactory.WriteText(directory.Path, "chain.mmd", "flowchart LR\n    " + chain + "\n");

        // Act
        var info = SourceLoader.Probe(path);
        var tooLarge = Assert.Throws<ViewerException>(() => SourceLoader.Load(path, new SourceOptions()));
        using var small = SourceLoader.Load(path, new SourceOptions { Scale = 0.25 });

        // Assert: the natural size is known, the default size is refused with the usual hint, and a quarter fits
        Assert.True(info.Width > Surface.MaxDimension, $"The chain is only {info.Width} pixels wide.");
        Assert.Equal(ErrorCodes.SizeLimit, tooLarge.Code);
        Assert.Contains("--scale", tooLarge.Hint, StringComparison.Ordinal);
        Assert.Equal((int)Math.Round(info.Width * 0.25, MidpointRounding.AwayFromZero), small.Surface.Width);
        Assert.True(Count(small.Surface, DefaultNodeFill) > 0);
    }

    /// <summary>
    ///     Test that a diagram cut anywhere, or emptied, is an expected error or a smaller valid diagram, never an unexpected
    ///     exception.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidTruncatedOrDamaged_IsExpectedErrorOrLoads()
    {
        // Arrange: every prefix of a small diagram, an empty file, and binary garbage
        using var directory = new TempDirectory();
        var text = File.ReadAllText(FixtureFactory.FixturePath(Path.Combine("mermaid", "shipping.mermaid")));
        var inputs = Enumerable.Range(0, text.Length).Select(length => text[..length]).ToList();

        // Act / Assert: each one loads or fails with a code of the library mapping
        foreach (var input in inputs)
        {
            var path = FixtureFactory.WriteText(directory.Path, "cut.mmd", input);
            var outcome = Record.Exception(() => SourceLoader.Load(path, new SourceOptions()).Dispose());
            if (outcome is not null)
            {
                var error = Assert.IsType<ViewerException>(outcome);
                Assert.Contains(error.Code, new[] { ErrorCodes.MermaidError, ErrorCodes.CorruptInput });
            }
        }

        var garbage = directory.Combine("garbage.mmd");
        File.WriteAllBytes(garbage, FixtureFactory.CreateGarbage(200));
        Assert.Equal(ErrorCodes.MermaidError, Assert.Throws<ViewerException>(() => SourceLoader.Load(garbage, new SourceOptions())).Code);
    }
}
