using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.PlantUml;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the PlantUML source of the SourceLoader class: probing a diagram, drawing it at its natural and at a
///     requested size, its background, the notes and strict mode, its includes under the file-access policy, and the
///     mapping of the library's failures.
/// </summary>
public class SourceLoaderPlantUmlTests
{
    /// <summary>The fill of a participant in PlantUML's default look.</summary>
    private static readonly Rgba32 ParticipantFill = new(0xE2, 0xE2, 0xF0, 255);

    /// <summary>The fill of an action in PlantUML's default look.</summary>
    private static readonly Rgba32 ActionFill = new(0xF1, 0xF1, 0xF1, 255);

    /// <summary>The white PlantUML draws a diagram over by default.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     Copies a fixture diagram (and the folder of its includes) into a directory.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="name">The file name within <c>Fixtures/plantuml</c>.</param>
    /// <returns>The path of the copy.</returns>
    private static string CopyFixture(TempDirectory directory, string name)
    {
        var path = directory.Combine(name);
        File.Copy(FixtureFactory.FixturePath(Path.Combine("plantuml", name)), path);
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
    ///     Test that the three PlantUML extensions are recognized by name (even when the text holds an SVG element) and that
    ///     the kind is a vector diagram named <c>plantuml</c>.
    /// </summary>
    [Fact]
    public void SourceLoader_Detect_PlantUmlExtensions_ReturnPlantUml()
    {
        // Arrange: the same text under each name, with an SVG element in a label
        using var directory = new TempDirectory();
        const string text = "@startuml\nAlice -> Bob: <svg/>\n@enduml\n";
        var puml = FixtureFactory.WriteText(directory.Path, "seq.puml", text);
        var plantuml = FixtureFactory.WriteText(directory.Path, "Seq.PLANTUML", text);
        var pu = FixtureFactory.WriteText(directory.Path, "seq.pu", text);
        var iuml = FixtureFactory.WriteText(directory.Path, "part.iuml", "participant Alice\n");

        // Act / Assert: the extension decides; an include fragment is not a supported format
        Assert.Equal(SourceKind.PlantUml, SourceLoader.Detect(puml));
        Assert.Equal(SourceKind.PlantUml, SourceLoader.Detect(plantuml));
        Assert.Equal(SourceKind.PlantUml, SourceLoader.Detect(pu));
        Assert.Equal(SourceKind.PlantUml, SourceLoader.DetectContent("<svg/>"u8, ".puml"));
        Assert.Equal(ErrorCodes.UnsupportedFormat, Assert.Throws<ViewerException>(() => SourceLoader.Detect(iuml)).Code);
        Assert.True(SourceKind.PlantUml.IsVector());
        Assert.True(SourceKind.PlantUml.IsDiagram());
        Assert.False(SourceKind.PlantUml.IsChart());
        Assert.False(SourceKind.PlantUml.IsDocument());
        Assert.Equal("plantuml", SourceKind.PlantUml.ToFormatName());
    }

    /// <summary>
    ///     Test that a sequence diagram is probed without drawing it: its natural size and the facts of its text
    ///     (participants as nodes, messages as edges, boxes as subgraphs).
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_PlantUmlSequence_ReportsDiagramFacts()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyFixture(directory, "sequence.puml");

        // Act
        var info = SourceLoader.Probe(path);

        // Assert: the kind, a single page with transparency, the size the library measures, and the facts
        Assert.Equal(SourceKind.PlantUml, info.Format);
        Assert.Equal((1, 4, true), (info.PageCount, info.Channels, info.HasAlpha));
        var measured = PlantUmlDocument.Open(path).Measure(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal((measured.Width, measured.Height), (info.Width, info.Height));
        Assert.Equal(new DiagramFacts("sequence", true, null, "Order", null, 2, 2, 0), info.Diagram);
        Assert.Null(info.Chart);
        Assert.Empty(info.Modules);
    }

    /// <summary>
    ///     Test that a sequence diagram is drawn at its natural size over its own white background, with the participant
    ///     fill of PlantUML's default look, a render operation that says so, and no note.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PlantUmlSequence_DrawsAtTheNaturalSizeOverWhite()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyFixture(directory, "sequence.puml");
        var info = SourceLoader.Probe(path);

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions { Strict = true });

        // Assert: the size, the white corner, the participant fills, and the operation
        Assert.Equal((info.Width, info.Height), (loaded.Surface.Width, loaded.Surface.Height));
        Assert.True(loaded.IsVector);
        Assert.Equal(White, loaded.Surface[0, 0]);
        Assert.True(Count(loaded.Surface, ParticipantFill) > 200);
        Assert.Empty(loaded.Diagnostics);
        var render = Assert.Single(loaded.Operations);
        Assert.Equal("render", render.Op);
        Assert.Equal([("width", (object)info.Width), ("height", info.Height)], render.Fields.Select(f => (f.Key, f.Value)));
    }

    /// <summary>
    ///     Test that an activity diagram (the <c>.plantuml</c> extension) is probed with its counts and drawn with the
    ///     action fill of PlantUML's default look.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PlantUmlActivity_ProbesAndDraws()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyFixture(directory, "activity.plantuml");

        // Act
        var info = SourceLoader.Probe(path);
        using var loaded = SourceLoader.Load(path, new SourceOptions { Strict = true });

        // Assert: the facts match the library, and the actions are drawn
        var document = PlantUmlDocument.Open(path);
        Assert.Equal(
            new DiagramFacts("activity", true, null, null, null, document.NodeCount, document.EdgeCount, document.SubgraphCount),
            info.Diagram);
        Assert.True(document.NodeCount >= 5);
        Assert.True(Count(loaded.Surface, ActionFill) > 200);
        Assert.Empty(loaded.Diagnostics);
    }

    /// <summary>
    ///     Test that the size options draw the diagram sharply at the requested size, and that a background replaces the
    ///     diagram's own one (a background with zero alpha gives a transparent image).
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PlantUmlSizeAndBackground_FollowTheOptions()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyFixture(directory, "sequence.puml");
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
        Assert.Equal(0, transparent.Surface[0, 0].A);
        Assert.True(Count(transparent.Surface, ParticipantFill) > 200);
        var render = Assert.Single(transparent.Operations);
        Assert.Contains(render.Fields, f => f.Key == "background" && Equals(f.Value, "#00000000"));
    }

    /// <summary>
    ///     Test that a class diagram (the <c>.pu</c> extension) is probed as a supported diagram with its classes,
    ///     relationships, and packages, and drawn with PlantUML's light grey class boxes without a note.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PlantUmlClass_ReportsFactsAndDraws()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyFixture(directory, "class.pu");

        // Act
        var info = SourceLoader.Probe(path);
        using var loaded = SourceLoader.Load(path, new SourceOptions { Strict = true });

        // Assert
        Assert.Equal(new DiagramFacts("class", true, null, null, null, 2, 1, 0), info.Diagram);
        Assert.True(Count(loaded.Surface, new Rgba32(0xF1, 0xF1, 0xF1, 255)) > 200);
        Assert.Empty(loaded.Diagnostics);
    }

    /// <summary>
    ///     Test that a diagram of a type this version recognizes but does not draw (a legacy activity diagram, the
    ///     <c>.pu</c> extension) is an empty picture with one unsupported note, and that strict mode makes it an
    ///     <c>unsupported-feature</c> error.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PlantUmlUnsupportedType_IsOneNoteThenStrictError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyFixture(directory, "legacy.pu");

        // Act
        var info = SourceLoader.Probe(path);
        using var loaded = SourceLoader.Load(path, new SourceOptions());
        var strict = Assert.Throws<ViewerException>(() => SourceLoader.Load(path, new SourceOptions { Strict = true }));

        // Assert: the facts and the empty picture of 20 pixels (twice PlantUML's document margin)
        Assert.Equal(new DiagramFacts("activity-legacy", false, null, null, null, 0, 0, 0), info.Diagram);
        Assert.Equal((20, 20), (loaded.Surface.Width, loaded.Surface.Height));

        // Assert: one located note that names the type
        var note = Assert.Single(loaded.Diagnostics);
        Assert.Equal(("plantuml", "unsupported", PlantUmlDiagnosticCodes.DiagramUnsupported), (note.Source, note.Severity, note.Code));
        Assert.NotNull(note.Location);
        Assert.DoesNotContain('\n', note.Message);

        // Assert: strict mode is an error with the library's exception as the cause
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.Contains(PlantUmlDiagnosticCodes.DiagramUnsupported, strict.Message, StringComparison.Ordinal);
        Assert.Contains("--strict", strict.Hint, StringComparison.Ordinal);
        Assert.IsType<PlantUmlUnsupportedFeatureException>(strict.InnerException);
    }

    /// <summary>
    ///     Test that a warning of the library is the note <c>plantuml-warning</c> with the specific code as its detail and
    ///     the line of the text, and that an unsupported construct is its own unsupported note.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PlantUml_MapsWarningsAndUnsupportedFeatures()
    {
        // Arrange: a skin parameter outside the subset, and a theme
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(
            directory.Path,
            "noisy.puml",
            "@startuml\n!theme cerulean\nskinparam handwritten true\nAlice -> Bob: hi\n@enduml\n");

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions());

        // Assert: the warning with its detail and location, and the unsupported theme
        var warning = Assert.Single(loaded.Diagnostics, d => d.Severity == "warning");
        Assert.Equal(("plantuml", PlantUmlImageSource.WarningCode, PlantUmlDiagnosticCodes.SkinParamIgnored), (warning.Source, warning.Code, warning.Detail));
        Assert.Equal(3, warning.Location!.Line);
        Assert.Contains("noisy.puml:3:", warning.Message, StringComparison.Ordinal);
        var unsupported = Assert.Single(loaded.Diagnostics, d => d.Severity == "unsupported");
        Assert.Equal(("plantuml", PlantUmlDiagnosticCodes.ThemeUnsupported), (unsupported.Source, unsupported.Code));
        Assert.Equal(2, unsupported.Location!.Line);
        Assert.All(loaded.Diagnostics, d => Assert.DoesNotContain('\n', d.Message));
    }

    /// <summary>
    ///     Test that text that is not a valid diagram is a <c>plantuml-error</c> with the location, the code and the
    ///     library's exception as the cause.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <param name="code">The <c>plantuml-*</c> code the message names.</param>
    [Theory]
    [InlineData("@startuml\nAlice -> Bob: hi\nthis is not plantuml at all\n@enduml\n", "plantuml-syntax-error")]
    [InlineData("@startDrawing\nAlice -> Bob\n@endDrawing\n", "plantuml-unknown-diagram")]
    [InlineData("@startuml\n!include missing.iuml\nAlice -> Bob\n@enduml\n", "plantuml-include-failed")]
    public void SourceLoader_Probe_PlantUmlInvalidText_IsPlantUmlError(string text, string code)
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "bad.puml", text);

        // Act
        var error = Assert.Throws<ViewerException>(() => SourceLoader.Probe(path));

        // Assert
        Assert.Equal(ErrorCodes.PlantUmlError, error.Code);
        Assert.Equal("plantuml-error", error.Code);
        Assert.StartsWith("PlantUML syntax error: ", error.Message, StringComparison.Ordinal);
        Assert.Contains(code, error.Message, StringComparison.Ordinal);
        Assert.Contains("bad.puml:", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', error.Message);
        Assert.IsType<PlantUmlSyntaxException>(error.InnerException);
    }

    /// <summary>
    ///     Test that an included file is drawn as part of the diagram, and that an include outside the roots of the
    ///     file-access policy is <c>file-access-denied</c>.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PlantUmlInclude_FollowsTheFileAccessPolicy()
    {
        // Arrange: a diagram whose participants come from a file in a subfolder, and one that includes a file next to its folder
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("parts"));
        File.Copy(FixtureFactory.FixturePath(Path.Combine("plantuml", "parts", "people.iuml")), directory.Combine("parts", "people.iuml"));
        var path = CopyFixture(directory, "included.puml");
        FixtureFactory.WriteText(directory.Path, "outside.iuml", "participant Carol\n");
        var confined = FixtureFactory.WriteText(directory.Path, "site/main.puml", "@startuml\n!include ../outside.iuml\nCarol -> Carol: hi\n@enduml\n");
        var options = new SourceOptions { FileAccess = FileAccessPolicy.UnderRoots(directory.Combine("site")) };

        // Act
        var info = SourceLoader.Probe(path);
        using var loaded = SourceLoader.Load(path, new SourceOptions { Strict = true });
        var denied = Assert.Throws<ViewerException>(() => SourceLoader.Load(confined, options));

        // Assert: the two participants of the include and the message are drawn
        Assert.Equal(new DiagramFacts("sequence", true, null, null, null, 2, 1, 0), info.Diagram);
        Assert.True(Count(loaded.Surface, ParticipantFill) > 200);

        // Assert: refused with the reason and a hint
        Assert.Equal(ErrorCodes.FileAccessDenied, denied.Code);
        Assert.Contains("PlantUML include denied", denied.Message, StringComparison.Ordinal);
        Assert.NotNull(denied.Hint);
    }

    /// <summary>
    ///     Test that the limits of the library are mapped: more messages than the limit is <c>budget-exceeded</c>, and a
    ///     text longer than the text limit is <c>corrupt-input</c>.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_PlantUmlLimits_AreBudgetExceededAndCorruptInput()
    {
        // Arrange: one message more than the default limit, and a diagram whose statements (comments do not count) are longer
        // than the text limit
        using var directory = new TempDirectory();
        var messages = string.Join("\n", Enumerable.Range(0, PlantUmlLimits.Default.MaxEdges + 1).Select(_ => "a -> b"));
        var many = FixtureFactory.WriteText(directory.Path, "many.puml", "@startuml\n" + messages + "\n@enduml\n");
        var huge = FixtureFactory.WriteText(
            directory.Path,
            "huge.puml",
            "@startuml\n" + string.Concat(Enumerable.Range(0, (PlantUmlLimits.Default.MaxTextSize / 200) + 10).Select(i => $"participant P{i} as \"{new string('x', 200)}\"\n")) + "@enduml\n");

        // Act
        var budget = Assert.Throws<ViewerException>(() => SourceLoader.Probe(many));
        var corrupt = Assert.Throws<ViewerException>(() => SourceLoader.Probe(huge));

        // Assert
        Assert.Equal(ErrorCodes.BudgetExceeded, budget.Code);
        Assert.Contains(PlantUmlDiagnosticCodes.LimitExceeded, budget.Message, StringComparison.Ordinal);
        Assert.NotNull(budget.Hint);
        Assert.Equal(ErrorCodes.CorruptInput, corrupt.Code);
        Assert.Contains("huge.puml", corrupt.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a failure of the library is mapped by its code: a limit is <c>budget-exceeded</c>, a refused include is
    ///     <c>file-access-denied</c>, and anything else is a <c>plantuml-error</c>, each with the hint of the library.
    /// </summary>
    [Fact]
    public void SourceLoader_MapPlantUml_MapsByCode()
    {
        // Arrange
        var limit = new PlantUmlRenderException(PlantUmlDiagnosticCodes.LimitExceeded, "the layout work exceeds MaxLayoutWork", new PlantUmlLocation("a.puml", 1, 1), "split it");
        var failed = new PlantUmlRenderException(PlantUmlDiagnosticCodes.RenderFailed, "the layout failed", new PlantUmlLocation("a.puml", 2, 3));

        // Act
        var mappedLimit = SourceLoader.MapPlantUml(limit.Code, "render", limit.Message, limit.Hint, limit);
        var mappedFailed = SourceLoader.MapPlantUml(failed.Code, "render", failed.Message, failed.Hint, failed);

        // Assert
        Assert.Equal((ErrorCodes.BudgetExceeded, "split it"), (mappedLimit.Code, mappedLimit.Hint));
        Assert.Same(limit, mappedLimit.InnerException);
        Assert.Equal(ErrorCodes.PlantUmlError, mappedFailed.Code);
        Assert.Equal("PlantUML render error: a.puml:2:3: plantuml-render-failed: the layout failed", mappedFailed.Message);
        Assert.Null(mappedFailed.Hint);
    }

    /// <summary>
    ///     Test that a diagram cut anywhere, or emptied, is an expected error or a smaller valid diagram, never an
    ///     unexpected exception.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PlantUmlTruncatedOrDamaged_IsExpectedErrorOrLoads()
    {
        // Arrange: every prefix of a small diagram, and binary garbage
        using var directory = new TempDirectory();
        var text = File.ReadAllText(FixtureFactory.FixturePath(Path.Combine("plantuml", "activity.plantuml")));
        var inputs = Enumerable.Range(0, text.Length).Select(length => text[..length]).ToList();

        // Act / Assert: each one loads or fails with a code of the library mapping
        foreach (var input in inputs)
        {
            var path = FixtureFactory.WriteText(directory.Path, "cut.puml", input);
            var outcome = Record.Exception(() => SourceLoader.Load(path, new SourceOptions()).Dispose());
            if (outcome is not null)
            {
                var error = Assert.IsType<ViewerException>(outcome);
                Assert.Contains(error.Code, new[] { ErrorCodes.PlantUmlError, ErrorCodes.CorruptInput });
            }
        }

        var garbage = directory.Combine("garbage.puml");
        File.WriteAllBytes(garbage, FixtureFactory.CreateGarbage(200));
        Assert.Equal(ErrorCodes.PlantUmlError, Assert.Throws<ViewerException>(() => SourceLoader.Load(garbage, new SourceOptions())).Code);
    }
}
