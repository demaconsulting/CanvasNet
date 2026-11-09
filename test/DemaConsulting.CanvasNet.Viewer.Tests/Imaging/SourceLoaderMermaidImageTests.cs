using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Tests of the picture nodes of a Mermaid diagram (<c>@{ img: ... }</c>) in <see cref="SourceLoader"/>: a diagram shows
///     files that lie beside it through the sandboxed resolver of the viewer, rooted at the folder of the diagram. The tests
///     draw diagrams whose pictures are a generated PNG, a model (<c>.scad</c>) and a chart (<c>.vl.json</c>), and the
///     failures (a link that leaves the folder, a remote URL, a missing file, a cycle, a broken file), and assert the pixels,
///     the notes, the <c>embed</c> operations and the <c>--strict</c> policy, and that a picture that enlarges a diagram drawn
///     at its natural size is charged and reported at the drawn size.
/// </summary>
public class SourceLoaderMermaidImageTests
{
    /// <summary>The color of the generated PNG pictures.</summary>
    private static readonly Rgba32 Green = new(0, 255, 0, 255);

    /// <summary>The bar color of a Vega-Lite chart that chooses none.</summary>
    private static readonly Rgba32 Bar = new(0x4C, 0x78, 0xA8, 255);

    /// <summary>
    ///     A diagram of one picture node of 60 by 60 units without a label.
    /// </summary>
    /// <param name="link">The link of the picture.</param>
    /// <returns>The text of the diagram.</returns>
    private static string OnePicture(string link) => $"flowchart LR\n    p@{{ img: \"{link}\", w: 60, h: 60, constraint: \"on\" }}\n";

    /// <summary>
    ///     Writes a PNG file of one color.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="color">The color of every pixel.</param>
    private static void WriteSolidPng(string path, Rgba32 color)
    {
        using var surface = new Surface(4, 4);
        surface.Clear(color);
        PngCodec.Save(surface, path);
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
    ///     Loads a diagram at its natural size.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="strict"><see langword="true"/> to treat an unsupported feature as an error.</param>
    /// <returns>The loaded source; the caller disposes it.</returns>
    private static LoadedSource Load(string path, bool strict = false) => SourceLoader.Load(path, new SourceOptions { Strict = strict });

    /// <summary>
    ///     Gets the links of the <c>embed</c> operations of a load.
    /// </summary>
    /// <param name="loaded">The loaded source.</param>
    /// <returns>The links, in order.</returns>
    private static List<string> Embeds(LoadedSource loaded) =>
        [.. loaded.Operations.Where(o => o.Op == "embed").Select(o => (string)o.Fields.Single(f => f.Key == "href").Value)];

    /// <summary>
    ///     Test that a flowchart shows a model and a chart that lie beside it, each once in the operations, with no note in strict
    ///     mode, and that the same diagram without the files draws neither.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidScadAndVegaLitePictures_DrawsBothAndListsEmbedOperations()
    {
        // Arrange: the model and the chart of the fixtures, and a diagram that shows them side by side
        using var directory = new TempDirectory();
        File.Copy(FixtureFactory.FixturePath("cube.scad"), directory.Combine("part.scad"));
        FixtureFactory.WriteText(directory.Path, "chart.vl.json", FixtureFactory.TwoBarChart);
        const string Text = "flowchart LR\n    part@{ img: \"part.scad\", w: 120, h: 120, constraint: \"on\" } --> chart@{ img: \"chart.vl.json\", w: 160, h: 120 }\n";
        var path = FixtureFactory.WriteText(directory.Path, "pictures.mmd", Text);
        Directory.CreateDirectory(directory.Combine("empty"));
        var alone = FixtureFactory.WriteText(directory.Combine("empty"), "pictures.mmd", Text);

        // Act
        using var loaded = Load(path, strict: true);
        using var without = Load(alone);

        // Assert: the chart's bars are drawn, the model changes the picture, each file is listed once at the size it was drawn
        Assert.True(Count(loaded.Surface, Bar) > 500);
        Assert.Equal(0, Count(without.Surface, Bar));
        Assert.Equal((without.Surface.Width, without.Surface.Height), (loaded.Surface.Width, loaded.Surface.Height));
        var changed = Enumerable.Range(0, loaded.Surface.Height)
            .Sum(y => Enumerable.Range(0, loaded.Surface.Width / 2).Count(x => loaded.Surface[x, y] != without.Surface[x, y]));
        Assert.True(changed > 500, $"Only {changed} pixels of the model half changed.");
        Assert.Empty(loaded.Diagnostics);
        Assert.Equal(["part.scad", "chart.vl.json"], Embeds(loaded));
        var model = loaded.Operations.Single(o => o.Op == "embed" && (string)o.Fields.Single(f => f.Key == "href").Value == "part.scad");
        Assert.Equal(("scad", 120, 120), ((string)model.Fields.Single(f => f.Key == "format").Value, (int)model.Fields.Single(f => f.Key == "width").Value, (int)model.Fields.Single(f => f.Key == "height").Value));

        // Assert: without the files each picture is one warning of the resolver (not also the library's) and nothing is embedded
        Assert.Equal(2, without.Diagnostics.Count);
        Assert.All(without.Diagnostics, d => Assert.Equal(("svg", FolderImageResolver.UnresolvedCode), (d.Source, d.Code)));
        Assert.Empty(Embeds(without));
    }

    /// <summary>
    ///     Test that a raster picture fills the box of its node.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidPngPicture_FillsItsBox()
    {
        // Arrange
        using var directory = new TempDirectory();
        WriteSolidPng(directory.Combine("logo.png"), Green);
        var path = FixtureFactory.WriteText(directory.Path, "logo.mmd", OnePicture("logo.png"));

        // Act
        using var loaded = Load(path, strict: true);

        // Assert: about 60 by 60 green pixels, and one embed operation
        Assert.InRange(Count(loaded.Surface, Green), 50 * 50, 61 * 61);
        Assert.Equal(["logo.png"], Embeds(loaded));
        Assert.Empty(loaded.Diagnostics);
    }

    /// <summary>
    ///     Test that a diagram that shows itself stops at the cycle: the link is the warning of the resolver, and in strict mode an
    ///     error that names the diagram and the link.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidSelfCycle_IsUnresolved()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "loop.mmd", OnePicture("loop.mmd"));

        // Act
        using var loaded = Load(path);
        var strict = Assert.Throws<ViewerException>(() => Load(path, strict: true));

        // Assert
        Assert.Contains(loaded.Diagnostics, d => d.Code == FolderImageResolver.UnresolvedCode && d.Message.Contains("cycle", StringComparison.Ordinal));
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.Contains("'loop.mmd' embeds 'loop.mmd', which was not drawn", strict.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a link that leaves the folder, is a remote URL, is an absolute path or is missing is one warning of the
    ///     resolver that names the link (the library's own warning for it is left out), draws nothing, and is an error in strict mode.
    /// </summary>
    /// <param name="link">The link.</param>
    [Theory]
    [InlineData("../secret.png")]
    [InlineData("https://example.invalid/secret.png")]
    [InlineData("C:/secret.png")]
    [InlineData("missing.png")]
    public void SourceLoader_Load_MermaidPictureOutsideTheSandbox_IsRefused(string link)
    {
        // Arrange: a valid picture one folder above the diagram
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("site"));
        WriteSolidPng(directory.Combine("secret.png"), Green);
        var path = FixtureFactory.WriteText(directory.Combine("site"), "flow.mmd", OnePicture(link));

        // Act
        using var loaded = Load(path);
        var strict = Assert.Throws<ViewerException>(() => Load(path, strict: true));

        // Assert: one note, the resolver's, naming the link; nothing green; no embed operation
        var note = Assert.Single(loaded.Diagnostics);
        Assert.Equal(("svg", "warning", FolderImageResolver.UnresolvedCode), (note.Source, note.Severity, note.Code));
        Assert.Contains($"'{link}'", note.Message, StringComparison.Ordinal);
        Assert.Equal(0, Count(loaded.Surface, Green));
        Assert.Empty(Embeds(loaded));
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.Contains($"'flow.mmd' embeds '{link}', which was not drawn", strict.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a picture that is found but broken is an error with the code of its own failure that names both files.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidBrokenPicture_FailsWithItsCodeNamingBothFiles()
    {
        // Arrange
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "bad.scad", "cube(;");
        var path = FixtureFactory.WriteText(directory.Path, "flow.mmd", OnePicture("bad.scad"));

        // Act
        var error = Assert.Throws<ViewerException>(() => Load(path));

        // Assert
        Assert.Equal(ErrorCodes.ScadError, error.Code);
        Assert.Contains("flow.mmd", error.Message, StringComparison.Ordinal);
        Assert.Contains("bad.scad", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a diagram embedded in an SVG file shows its pictures through the resolver of the SVG file's render, resolved
    ///     from the folder of the diagram, and keeps the transparency of an embedded picture over the SVG background.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingDiagramWithPicture_DrawsTheDiagramsPicture()
    {
        // Arrange: sub/flow.mmd shows sub/logo.png; the SVG file places the diagram over a red rectangle
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("sub"));
        WriteSolidPng(directory.Combine("sub", "logo.png"), Green);
        FixtureFactory.WriteText(directory.Path, "sub/flow.mmd", OnePicture("logo.png"));
        var path = FixtureFactory.WriteText(
            directory.Path,
            "diagram.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100' width='100' height='100'><rect width='100' height='100' fill='#ff0000'/>"
            + "<image href='sub/flow.mmd' x='0' y='0' width='100' height='100'/></svg>");

        // Act
        using var loaded = Load(path, strict: true);

        // Assert: the picture is drawn, the red shows through around it, and nothing is reported
        Assert.True(Count(loaded.Surface, Green) > 500);
        Assert.Equal(new Rgba32(255, 0, 0, 255), loaded.Surface[1, 1]);
        Assert.Empty(loaded.Diagnostics);
        Assert.Equal(["sub/flow.mmd"], Embeds(loaded));
    }

    /// <summary>
    ///     Writes a 400 by 300 PNG and a diagram of one picture node without its <c>w</c> and <c>h</c>, which takes the size of
    ///     the picture: the probe (without pictures) measures a small diagram, the draw a much larger one.
    /// </summary>
    /// <param name="directory">The folder of the files.</param>
    /// <returns>The input of the diagram and its probe.</returns>
    private static (SourceLoader.SourceInput Input, SourceInfo Probe) WriteEnlargingPicture(TempDirectory directory)
    {
        File.WriteAllBytes(directory.Combine("big.png"), ViewerFixtures.Png(400, 300, Green));
        var path = FixtureFactory.WriteText(directory.Path, "big.mmd", "flowchart LR\n    p@{ img: \"big.png\" }\n");
        var input = SourceLoader.SourceInput.FromFile(path);
        return (input, SourceLoader.ProbeInput(input, SourceKind.Mermaid));
    }

    /// <summary>
    ///     Test that a diagram drawn at its natural size whose picture node enlarges it is charged the pixels of the surface it
    ///     returns, and reports the natural size of the drawn diagram rather than the size probed without the picture.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidPictureEnlargingTheLayout_ChargesAndReportsTheDrawnSize()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (input, probe) = WriteEnlargingPicture(directory);
        var charges = new List<(int Width, int Height)>();
        var context = new SourceLoader.LoadContext { Charge = (w, h) => charges.Add((w, h)) };

        // Act
        using var pages = SourceLoader.OpenInputPages(input, probe, new SourceOptions(), context);
        using var loaded = pages.ReadPage(1);

        // Assert: the picture enlarged the diagram, every drawn pixel was charged, and the natural size is the drawn one
        Assert.True((long)loaded.Surface.Width * loaded.Surface.Height > (long)probe.Width * probe.Height);
        Assert.Equal((long)loaded.Surface.Width * loaded.Surface.Height, charges.Sum(c => (long)c.Width * c.Height));
        Assert.Equal((loaded.Surface.Width, loaded.Surface.Height), (loaded.Info.Width, loaded.Info.Height));
    }

    /// <summary>
    ///     Test that a diagram drawn at its natural size whose picture node enlarges it beyond the pixel budget is refused for
    ///     the pixels of the whole drawn surface, even though the size the probe measured without the picture fits.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MermaidPictureEnlargingTheLayout_IsRefusedBeyondTheBudget()
    {
        // Arrange: a budget that holds the probed size but not the drawn one
        using var directory = new TempDirectory();
        var (input, probe) = WriteEnlargingPicture(directory);
        const long Budget = 10_000;
        Assert.True((long)probe.Width * probe.Height < Budget);
        long charged = 0;
        var context = new SourceLoader.LoadContext
        {
            Charge = (w, h) =>
            {
                var pixels = (long)w * h;
                if (charged + pixels > Budget)
                {
                    throw new ImageSourceBudgetException(pixels, Budget - charged);
                }

                charged += pixels;
            },
        };
        using var pages = SourceLoader.OpenInputPages(input, probe, new SourceOptions(), context);
        using var unlimitedPages = SourceLoader.OpenInputPages(input, probe, new SourceOptions(), new SourceLoader.LoadContext());
        using var unlimited = unlimitedPages.ReadPage(1);

        // Act
        var refused = Assert.Throws<ImageSourceBudgetException>(() => pages.ReadPage(1).Dispose());

        // Assert: the refusal names the whole drawn surface against the full budget (the charge before the draw given
        // back), and the hook holds only that charge, not the refused rest
        Assert.Equal((long)unlimited.Surface.Width * unlimited.Surface.Height, refused.RequestedPixels);
        Assert.Equal(Budget, refused.RemainingPixels);
        Assert.Equal((long)probe.Width * probe.Height, charged);
    }

    /// <summary>
    ///     Test that a diagram whose picture node enlarges it, drawn at a scale, reports the natural size of the diagram drawn
    ///     at a scale of 1 (the drawn size over the scale) and returns a surface of that size times the scale.
    /// </summary>
    /// <param name="scale">The scale of the load.</param>
    [Theory]
    [InlineData(2.0)]
    [InlineData(1.5)]
    public void SourceLoader_Load_MermaidPictureEnlargingTheLayout_ReportsTheUnscaledDrawnSize(double scale)
    {
        // Arrange
        using var directory = new TempDirectory();
        var (input, probe) = WriteEnlargingPicture(directory);
        using var naturalPages = SourceLoader.OpenInputPages(input, probe, new SourceOptions(), new SourceLoader.LoadContext());
        using var natural = naturalPages.ReadPage(1);

        // Act
        using var scaledPages = SourceLoader.OpenInputPages(input, probe, new SourceOptions { Scale = scale }, new SourceLoader.LoadContext());
        using var scaled = scaledPages.ReadPage(1);

        // Assert: the natural size is the drawn size at scale 1, and the surface is that size times the scale
        Assert.Equal((natural.Surface.Width, natural.Surface.Height), (scaled.Info.Width, scaled.Info.Height));
        Assert.Equal(
            ((int)Math.Round(scaled.Info.Width * scale, MidpointRounding.AwayFromZero), (int)Math.Round(scaled.Info.Height * scale, MidpointRounding.AwayFromZero)),
            (scaled.Surface.Width, scaled.Surface.Height));
    }
}
