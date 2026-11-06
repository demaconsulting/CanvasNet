// cspell:ignore scad
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the finer detail of the notes <see cref="SourceLoader"/> reports: <see cref="ImageDiagnostic.Detail"/>
///     and <see cref="ImageDiagnostic.Location"/> of an OpenSCAD model and of a Vega-Lite chart, their copy through the
///     notes of embedded files, and the equality of the record.
/// </summary>
public class SourceLoaderDiagnosticDetailTests
{
    /// <summary>
    ///     Test that a malformed anchor is a <c>scad-warning</c> whose detail is the finer identifier and whose location is
    ///     the line and column in the model file.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_ScadAnchorWarning_CarriesDetailAndLocation()
    {
        // Arrange: an anchor with two coordinates
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "model.scad", "cube(5);\necho(\"anchor\", \"bad\", [1, 2]);\n");

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions { Width = 32, Height = 32 });

        // Assert: the generic code is unchanged; the identifier and the position are new
        var warning = Assert.Single(loaded.Diagnostics, d => d.Severity == "warning");
        Assert.Equal("scad-warning", warning.Code);
        Assert.Equal("scad-anchor-invalid", warning.Detail);
        Assert.NotNull(warning.Location);
        Assert.Equal(path, warning.Location.File);
        Assert.Equal(2, warning.Location.Line);
        Assert.True(warning.Location.Column > 0);
        Assert.Null(warning.Location.Pointer);
    }

    /// <summary>
    ///     Test that a plain warning (the library's generic code) has no detail, and still has its location.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_ScadGenericWarning_HasNoDetailButHasLocation()
    {
        // Arrange: a call of a module that does not exist
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "model.scad", "cube(1);\nmissing();\n");

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions { Width = 32, Height = 32 });

        // Assert
        var warning = Assert.Single(loaded.Diagnostics, d => d.Severity == "warning");
        Assert.Equal("scad-warning", warning.Code);
        Assert.NotEqual("scad-warning", warning.Detail);
        Assert.Equal(2, warning.Location?.Line);
    }

    /// <summary>
    ///     Test that an unsupported feature of a model keeps its code (the feature) and gets the location of the statement.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_ScadUnsupportedFeature_CarriesLocation()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "model.scad", "cube(1);\n\nsurface(\"A\");\n");

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions { Width = 32, Height = 32 });

        // Assert
        var unsupported = Assert.Single(loaded.Diagnostics, d => d.Severity == "unsupported");
        Assert.Equal("scad-module-surface", unsupported.Code);
        Assert.Null(unsupported.Detail);
        Assert.Equal(3, unsupported.Location?.Line);
        Assert.Equal(path, unsupported.Location?.File);
    }

    /// <summary>
    ///     Test that a warning and an unsupported feature of a chart carry the <c>vl-*</c> identifier and the JSON
    ///     pointer of the location.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VegaLiteNotes_CarryDetailAndPointer()
    {
        // Arrange: a chart with an unknown property and a mark that is not drawn
        using var directory = new TempDirectory();
        const string chart =
            """
            {
              "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
              "bogus": 4,
              "width": 100,
              "height": 60,
              "data": { "values": [ { "k": "a", "v": 3 } ] },
              "mark": "geoshape"
            }
            """;
        var path = FixtureFactory.WriteText(directory.Path, "noisy.vl.json", chart);

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions());

        // Assert: the code stays generic and the detail is the identifier in front of the message
        var warning = Assert.Single(loaded.Diagnostics, d => d.Severity == "warning");
        Assert.Equal("vega-lite-warning", warning.Code);
        Assert.Equal("vl-unknown-property", warning.Detail);
        Assert.Equal("/bogus", warning.Location?.Pointer);
        Assert.True(warning.Location?.Line > 0);
        var unsupported = loaded.Diagnostics.First(d => d.Severity == "unsupported");
        Assert.Equal("vl-mark-geoshape", unsupported.Code);
        Assert.Null(unsupported.Detail);
        Assert.Equal("/mark", unsupported.Location?.Pointer);
    }

    /// <summary>
    ///     Test that a note of a PPTX or VSDX page has no location (the libraries give none).
    /// </summary>
    [Fact]
    public void SourceLoader_Load_DocumentNote_HasNoLocation()
    {
        // Arrange: a presentation whose first slide has a picture the renderer skips
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "deck.pptx");
        File.WriteAllBytes(path, FixtureFactory.CreatePptx(withMetafile: true));

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions());

        // Assert
        var note = Assert.Single(loaded.Diagnostics);
        Assert.Equal("unsupported", note.Severity);
        Assert.Equal(1, note.Page);
        Assert.Null(note.Location);
        Assert.Null(note.Detail);
    }

    /// <summary>
    ///     Test that the notes of an embedded model keep their origin, detail and location when an SVG file embeds it.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingModel_NotesKeepOriginDetailAndLocation()
    {
        // Arrange: a diagram that embeds a model with a malformed anchor
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "part.scad", "cube(5);\necho(\"anchor\", \"bad\", [1, 2]);\n");
        var svg = FixtureFactory.WriteText(
            directory.Path,
            "diagram.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><image href='part.scad' x='0' y='0' width='50' height='50'/></svg>");

        // Act
        using var loaded = SourceLoader.Load(svg, new SourceOptions());

        // Assert
        var warning = Assert.Single(loaded.Diagnostics, d => d.Code == "scad-warning");
        Assert.Equal("scad-anchor-invalid", warning.Detail);
        Assert.Equal(2, warning.Location?.Line);
        Assert.Equal(["part.scad"], warning.Origin);
    }

    /// <summary>
    ///     Test that the detail and the location take part in the equality and the hash of a note.
    /// </summary>
    [Fact]
    public void ImageDiagnostic_Equals_ComparesDetailAndLocation()
    {
        // Arrange
        var plain = new ImageDiagnostic("scad", "warning", "scad-warning", "m");
        var location = new ImageDiagnosticLocation("a.scad", 1, 2, null);
        var full = plain with { Detail = "scad-anchor-invalid", Location = location };
        var same = plain with { Detail = "scad-anchor-invalid", Location = new ImageDiagnosticLocation("a.scad", 1, 2, null) };

        // Act / Assert
        Assert.Equal(full, same);
        Assert.Equal(full.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(plain, full);
        Assert.NotEqual(plain, plain with { Detail = "x" });
        Assert.NotEqual(plain, plain with { Location = location });
        Assert.Null(plain.Detail);
        Assert.Null(plain.Location);
    }
}
