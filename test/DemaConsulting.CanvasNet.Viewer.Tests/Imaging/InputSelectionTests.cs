// cspell:ignore canvasnet scad vega
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for <see cref="InputReference"/> and <see cref="InputSelection"/>: how an input path with a URL fragment is
///     split, how the fragment is checked against the kind of the file, how it is laid over the options of a command, and how
///     the operations of the loader honor it.
/// </summary>
public class InputSelectionTests
{
    /// <summary>A model with two variables.</summary>
    private const string Model = "width = 5; depth = 6; cube([width, depth, 5], center = true);";

    /// <summary>A chart with a numeric parameter.</summary>
    private const string Chart =
        "{\"params\":[{\"name\":\"cutoff\",\"value\":1}],\"data\":{\"values\":[{\"x\":1,\"y\":3},{\"x\":2,\"y\":5}]},"
        + "\"transform\":[{\"filter\":\"datum.x >= cutoff\"}],\"width\":50,\"height\":50,\"mark\":\"bar\","
        + "\"encoding\":{\"x\":{\"field\":\"x\",\"type\":\"ordinal\"},\"y\":{\"field\":\"y\",\"type\":\"quantitative\"}}}";

    /// <summary>
    ///     Test that a text without a <c>#</c> is a path as it is.
    /// </summary>
    [Fact]
    public void InputReference_Parse_NoHash_IsThePath()
    {
        // Act
        var input = InputReference.Parse("some/dir/part.scad");

        // Assert
        Assert.Equal(("some/dir/part.scad", string.Empty, "some/dir/part.scad"), (input.Path, input.Fragment, input.Text));
        Assert.Throws<ArgumentNullException>(() => InputReference.Parse(null!));
    }

    /// <summary>
    ///     Test that a <c>#</c> starts a fragment when the file before it exists, that an existing file of that exact name wins,
    ///     and that the longest existing file is the path.
    /// </summary>
    [Fact]
    public void InputReference_Parse_SplitsAtTheHashOnlyWhenNoFileHasTheName()
    {
        // Arrange: a.scad, a.scad#view=top (a real file), and p#1.scad
        using var directory = new TempDirectory();
        var plain = FixtureFactory.WriteText(directory.Path, "a.scad", Model);
        var real = FixtureFactory.WriteText(directory.Path, "a.scad#view=top", Model);
        var hashed = FixtureFactory.WriteText(directory.Path, "p#1.scad", Model);

        // Act
        var split = InputReference.Parse(plain + "#def.width=3");
        var literal = InputReference.Parse(real);
        var hashedWhole = InputReference.Parse(hashed);
        var hashedSplit = InputReference.Parse(hashed + "#view=front&def.c=#ff0");

        // Assert
        Assert.Equal((plain, "def.width=3"), (split.Path, split.Fragment));
        Assert.Equal((real, string.Empty), (literal.Path, literal.Fragment));
        Assert.Equal((hashed, string.Empty), (hashedWhole.Path, hashedWhole.Fragment));
        Assert.Equal((hashed, "view=front&def.c=#ff0"), (hashedSplit.Path, hashedSplit.Fragment));
        Assert.Equal(hashed + "#view=front&def.c=#ff0", hashedSplit.Text);
    }

    /// <summary>
    ///     Test that a text that names no file is split at its first <c>#</c>, so the missing file is the one before it.
    /// </summary>
    [Theory]
    [InlineData("missing.scad#view=top", "missing.scad", "view=top")]
    [InlineData("missing#a#b", "missing", "a#b")]
    [InlineData("#only", "", "only")]
    [InlineData("trailing#", "trailing", "")]
    public void InputReference_Parse_NoFile_SplitsAtTheFirstHash(string text, string path, string fragment)
    {
        // Act
        var input = InputReference.Parse(text);

        // Assert
        Assert.Equal((path, fragment), (input.Path, input.Fragment));
    }

    /// <summary>
    ///     Test that probing an input without a fragment gives no selection, and that the specifier is the path.
    /// </summary>
    [Fact]
    public void SourceLoader_ProbeInputReference_NoFragment_HasNoSelection()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "part.scad", Model);

        // Act
        var info = SourceLoader.Probe(InputReference.Parse(model));

        // Assert
        Assert.Null(info.Selection);
        Assert.Equal(model, info.Specifier);
        Assert.Throws<ArgumentNullException>(() => SourceLoader.Probe((InputReference)null!));
    }

    /// <summary>
    ///     Test that a selection holds what the fragment chose for a model, and that it is laid over the options of the command
    ///     with the fragment winning by name.
    /// </summary>
    [Fact]
    public void InputSelection_ApplyTo_Model_MergesVariablesAndReplacesTheView()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "part.scad", Model);
        var info = SourceLoader.Probe(InputReference.Parse(model + "#view=top&def.width=20&def.$vpt=[0,0,2]"));
        var shared = new SourceOptions
        {
            Page = 1,
            Scad = new ScadViewOptions
            {
                View = "front",
                ViewGiven = true,
                Azimuth = 30,
                Defines = new Dictionary<string, object?> { ["width"] = 5.0, ["depth"] = 9.0 }
            }
        };

        // Act
        var merged = info.Selection!.ApplyTo(shared);

        // Assert: the view is the fragment's, the azimuth of the command stays, and the variables are merged by name
        Assert.Equal("top", merged.Scad!.View);
        Assert.True(merged.Scad.ViewGiven);
        Assert.Equal(30, merged.Scad.Azimuth);
        Assert.Equal(20.0, merged.Scad.Defines["width"]);
        Assert.Equal(9.0, merged.Scad.Defines["depth"]);
        Assert.Equal([0.0, 0.0, 2.0], Assert.IsType<List<object?>>(merged.Scad.Defines["$vpt"]));
        var again = info.Selection.ApplyTo(merged).Scad!;
        Assert.Equal((merged.Scad.View, merged.Scad.Azimuth), (again.View, again.Azimuth));
        Assert.Equal(merged.Scad.Defines.Keys.Order(), again.Defines.Keys.Order());
        Assert.Equal(model + "#view=top&def.width=20&def.$vpt=[0,0,2]", info.Specifier);
        Assert.Equal(model, info.Path);
        Assert.Empty(info.Selection.Diagnostics);
        Assert.Throws<ArgumentNullException>(() => info.Selection.ApplyTo(null!));

        // Assert: a fragment that sets no view keeps the view of the command
        var variablesOnly = SourceLoader.Probe(InputReference.Parse(model + "#def.width=20")).Selection!.ApplyTo(shared);
        Assert.Equal("front", variablesOnly.Scad!.View);
    }

    /// <summary>
    ///     Test that a selection of a document names the page, replaces the page of the command, and is checked against the page
    ///     count when the input is probed.
    /// </summary>
    [Fact]
    public void SourceLoader_ProbeInputReference_Document_PageIsCheckedAndReplacesTheOption()
    {
        // Arrange
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        var info = SourceLoader.Probe(InputReference.Parse(pdf + "#page=2"));
        var missing = Assert.Throws<ViewerException>(() => SourceLoader.Probe(InputReference.Parse(pdf + "#page=3")));
        var slide = SourceLoader.Probe(InputReference.Parse(pdf + "#slide=2"));

        // Assert: the pages of a PDF are page=N only, so slide is an unknown parameter (a warning) and the page stays 1
        Assert.Equal(2, info.Selection!.Page);
        Assert.Equal(2, info.Selection.ApplyTo(new SourceOptions { Page = 7 }).Page);
        Assert.Equal(ErrorCodes.PageOutOfRange, missing.Code);
        Assert.Null(slide.Selection!.Page);
        Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, Assert.Single(slide.Selection.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that a value that cannot be used, and a fragment over its limits, is <c>image-fragment-invalid</c> with a message
    ///     that names the input.
    /// </summary>
    /// <param name="name">The file.</param>
    /// <param name="fragment">The fragment.</param>
    [Theory]
    [InlineData("part.scad", "def.width=[1,2")]
    [InlineData("part.scad", "view=sideways")]
    [InlineData("part.scad", "def.2x=1")]
    [InlineData("chart.vl.json", "param.cutoff={bad")]
    [InlineData("source.pdf", "page=x")]
    [InlineData("flow.mmd", "theme=midnight")]
    public void SourceLoader_ProbeInputReference_InvalidValue_IsImageFragmentInvalid(string name, string fragment)
    {
        // Arrange
        using var directory = new TempDirectory();
        File.Copy(FixtureFactory.FixturePath(Path.Combine("mermaid", "flow.mmd")), directory.Combine("flow.mmd"));
        FixtureFactory.WriteText(directory.Path, "part.scad", Model);
        FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart);
        FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        var ex = Assert.Throws<ViewerException>(() => SourceLoader.Probe(InputReference.Parse(directory.Combine(name) + "#" + fragment)));

        // Assert
        Assert.Equal(ErrorCodes.ImageFragmentInvalid, ex.Code);
        Assert.Equal("image-fragment-invalid", ex.Code);
        Assert.Contains(fragment, ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.Hint);
    }

    /// <summary>
    ///     Test that a fragment over its limits is refused, and that a missing file with a fragment says why.
    /// </summary>
    [Fact]
    public void SourceLoader_ProbeInputReference_OverTheLimitsOrMissing_Throws()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "part.scad", Model);

        // Act
        var tooLong = Assert.Throws<ViewerException>(() => SourceLoader.Probe(InputReference.Parse(model + "#def.s=" + new string('a', ImageFragment.MaxLength + 1))));
        var tooMany = Assert.Throws<ViewerException>(
            () => SourceLoader.Probe(InputReference.Parse(model + "#" + string.Join('&', Enumerable.Range(0, ImageFragment.MaxParameters + 1).Select(i => $"def.v{i}=1")))));
        var missing = Assert.Throws<ViewerException>(() => SourceLoader.Probe(InputReference.Parse(directory.Combine("none.scad") + "#view=top")));
        var plainMissing = Assert.Throws<ViewerException>(() => SourceLoader.Probe(InputReference.Parse(directory.Combine("none.scad"))));

        // Assert
        Assert.Equal(ErrorCodes.ImageFragmentInvalid, tooLong.Code);
        Assert.Contains("longer than", tooLong.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.ImageFragmentInvalid, tooMany.Code);
        Assert.Contains("more than", tooMany.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.InputNotFound, missing.Code);
        Assert.Contains("a file named", missing.Message, StringComparison.Ordinal);
        Assert.Contains("fragment", missing.Hint, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.InputNotFound, plainMissing.Code);
        Assert.DoesNotContain("a file named", plainMissing.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the notes about a fragment are the codes of an SVG link, are reported once with the first page that is loaded,
    ///     and that a <c>param.</c> name the chart does not declare is dropped with <c>vl-param-unknown</c>.
    /// </summary>
    [Fact]
    public void InputSelection_Diagnostics_AreTheCodesOfALinkAndReportedOnce()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "part.scad", Model);
        var chart = FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart);
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        var modelInfo = SourceLoader.Probe(InputReference.Parse(model + "#wide=1&def.width=1&def.width=2"));
        var chartInfo = SourceLoader.Probe(InputReference.Parse(chart + "#param.cutoff=2&param.nope=1"));
        var documentInfo = SourceLoader.Probe(InputReference.Parse(pdf + "#page=1&extra=1"));
        using var pages = SourceLoader.OpenPages(documentInfo, new SourceOptions());
        using var first = pages.ReadPage(1);
        using var second = pages.ReadPage(2);

        // Assert: the model has an unknown parameter and a duplicate; the chart lost the unknown param
        Assert.Equal(
            [FolderImageResolver.FragmentUnknownParameterCode, FolderImageResolver.FragmentDuplicateParameterCode],
            modelInfo.Selection!.Diagnostics.Select(d => d.Code));
        Assert.All(modelInfo.Selection.Diagnostics, d => Assert.Equal(("warning", "input"), (d.Severity, d.Source)));
        var unknown = Assert.Single(chartInfo.Selection!.Diagnostics);
        Assert.Equal("vl-param-unknown", unknown.Code);
        Assert.Contains("declared: cutoff", unknown.Message, StringComparison.Ordinal);
        Assert.Equal(["cutoff"], chartInfo.Selection.VegaLite!.Parameters.Keys);

        // Assert: the first page carries the note, the second does not
        Assert.Contains(first.Diagnostics, d => d.Code == FolderImageResolver.FragmentUnknownParameterCode);
        Assert.DoesNotContain(second.Diagnostics, d => d.Code == FolderImageResolver.FragmentUnknownParameterCode);
    }

    /// <summary>
    ///     Test that a chart parameter of the fragment is merged with the parameters of the command by name, the fragment winning.
    /// </summary>
    [Fact]
    public void InputSelection_ApplyTo_Chart_MergesParametersByName()
    {
        // Arrange
        using var directory = new TempDirectory();
        var chart = FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart);
        var selection = SourceLoader.Probe(InputReference.Parse(chart + "#param.cutoff=2")).Selection!;
        var shared = new SourceOptions { VegaLite = new ChartOptions { Parameters = new Dictionary<string, object?> { ["cutoff"] = 1.0, ["other"] = 3.0 } } };

        // Act
        var merged = selection.ApplyTo(shared);

        // Assert: the value of the fragment, and the other parameter of the command is kept (it is the command's to report)
        Assert.Equal(2.0, merged.VegaLite!.Parameters["cutoff"]);
        Assert.Equal(3.0, merged.VegaLite.Parameters["other"]);
        Assert.False(merged.VegaLite.WarnOnUnknownParameters);
        Assert.Equal(2.0, selection.ApplyTo(new SourceOptions()).VegaLite!.Parameters["cutoff"]);
    }

    /// <summary>
    ///     Test that the theme of the fragment of a diagram input is read (without regard to case), replaces the theme of the
    ///     command, is drawn, and that a parameter a diagram does not take is the warning of an unknown parameter.
    /// </summary>
    [Fact]
    public void InputSelection_ApplyTo_Diagram_FragmentThemeReplacesTheCommandTheme()
    {
        // Arrange
        using var directory = new TempDirectory();
        var diagram = directory.Combine("flow.mmd");
        File.Copy(FixtureFactory.FixturePath(Path.Combine("mermaid", "flow.mmd")), diagram);

        // Act
        var info = SourceLoader.Probe(InputReference.Parse(diagram + "#theme=Dark&page=2"));
        var merged = info.Selection!.ApplyTo(new SourceOptions { Mermaid = new DiagramOptions { Theme = "forest" } });
        var kept = SourceLoader.Probe(InputReference.Parse(diagram + "#page=2")).Selection!.ApplyTo(new SourceOptions { Mermaid = new DiagramOptions { Theme = "forest" } });
        using var loaded = SourceLoader.Load(info, new SourceOptions());

        // Assert: the canonical name of the fragment wins, a fragment without a theme keeps the command's, and page is not a parameter of a diagram
        Assert.Equal("dark", info.Selection.Mermaid!.Theme);
        Assert.Equal("dark", merged.Mermaid!.Theme);
        Assert.Equal("forest", kept.Mermaid!.Theme);
        Assert.Null(info.Selection.Page);
        Assert.Equal([FolderImageResolver.FragmentUnknownParameterCode], info.Selection.Diagnostics.Select(d => d.Code));
        Assert.Contains(loaded.Operations, o => o.Op == "render" && o.Fields.Any(f => f.Key == "theme" && (string)f.Value == "dark"));
        Assert.Contains(loaded.Diagnostics, d => d.Code == FolderImageResolver.FragmentUnknownParameterCode);
    }

    /// <summary>
    ///     Test that a model is drawn with the variables and the view of the fragment of its input, as it is with the same options.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_InputWithFragment_DrawsWhatTheOptionsWouldDraw()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "part.scad", Model);
        var byFragment = SourceLoader.Probe(InputReference.Parse(model + "#view=top&def.width=20"));
        var plain = SourceLoader.Probe(model);
        var options = new SourceOptions { Width = 64, Height = 64 };

        // Act
        using var fragment = SourceLoader.Load(byFragment, options);
        using var expected = SourceLoader.Load(
            plain,
            options with { Scad = new ScadViewOptions { View = "top", ViewGiven = true, Defines = new Dictionary<string, object?> { ["width"] = 20.0 } } });

        // Assert
        SurfaceAssert.Equal(expected.Surface, fragment.Surface);
        Assert.Empty(fragment.Diagnostics);
    }

    /// <summary>
    ///     Test that the render, the inspection, and the montage take the page of the fragment, and that the montage labels the
    ///     cell with it.
    /// </summary>
    [Fact]
    public void Operations_InputWithPageFragment_UseThePageAndTheLabel()
    {
        // Arrange: a two-page PDF whose pages have different sizes
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var info = SourceLoader.Probe(InputReference.Parse(pdf + "#page=2"));
        var second = SourceLoader.Probe(pdf);

        // Act
        var rendered = ImageRenderer.Execute(info, new RenderRequest(directory.Combine("r.png")) { AllPages = true });
        var inspected = ImageInspector.Execute(info, new InspectRequest(InspectKind.Colors));
        var expected = ImageInspector.Execute(second, new InspectRequest(InspectKind.Colors) { Source = new SourceOptions { Page = 2 } });
        var montage = MontageBuilder.Execute([info, second], new MontageRequest(directory.Combine("m.png")) { Sheet = new SheetOptions { Labels = true } });

        // Assert: one page rendered (the second), inspected like --page 2, and labeled with the fragment (no page number after it)
        var page = Assert.Single(rendered.Pages);
        Assert.Equal(2, page.Page);
        Assert.Equal(2, inspected.Page);
        Assert.Equal((expected.Width, expected.Height), (inspected.Width, inspected.Height));
        Assert.Equal(["source.pdf#page=2", "source.pdf p1"], montage.Cells.Select(c => c.Label));
        Assert.Equal([2, 1], montage.Cells.Select(c => c.Page));
    }

    /// <summary>
    ///     Test that the page of the fragment replaces the page of a comparison input.
    /// </summary>
    [Fact]
    public void ImageComparer_Execute_InputWithPageFragment_ComparesThatPage()
    {
        // Arrange
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var byFragment = SourceLoader.Probe(InputReference.Parse(pdf + "#page=2"));
        var plain = SourceLoader.Probe(pdf);

        // Act: the second page against the second page loaded the usual way, and against the first page
        var same = ImageComparer.Execute(new CompareRequest(
            new CompareInput("a", byFragment) { Source = new SourceOptions() },
            new CompareInput("b", plain) { Page = 2, Source = new SourceOptions { Page = 2 } }));

        // Assert
        Assert.Equal(2, same.A.Page);
        Assert.True(same.Comparison.Metrics.Identical);
    }
}
