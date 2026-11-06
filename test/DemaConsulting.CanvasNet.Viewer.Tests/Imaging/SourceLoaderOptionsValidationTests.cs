// cspell:ignore scad
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the validation of <see cref="SourceOptions"/> that <see cref="SourceLoader.Load(string, SourceOptions)"/>,
///     <see cref="SourceLoader.Load(SourceInfo, SourceOptions)"/> and <see cref="SourceLoader.OpenPages(SourceInfo, SourceOptions)"/>
///     apply up front: the ranges, the error code, and that no file is touched first.
/// </summary>
public class SourceLoaderOptionsValidationTests
{
    /// <summary>
    ///     Gets the options that are not usable, each with a label.
    /// </summary>
    /// <returns>The test data.</returns>
    public static TheoryData<string, SourceOptions> InvalidOptions()
    {
        return new TheoryData<string, SourceOptions>
        {
            { "undefined filter", new SourceOptions { Filter = (ResamplingMode)999 } },
            { "width 0", new SourceOptions { Width = 0 } },
            { "width negative", new SourceOptions { Width = -5 } },
            { "height 0", new SourceOptions { Height = 0 } },
            { "height negative", new SourceOptions { Height = -1 } },
            { "scale 0", new SourceOptions { Scale = 0 } },
            { "scale negative", new SourceOptions { Scale = -2 } },
            { "scale NaN", new SourceOptions { Scale = double.NaN } },
            { "scale infinity", new SourceOptions { Scale = double.PositiveInfinity } },
            { "scale too small", new SourceOptions { Scale = 0.0009 } },
            { "scale too large", new SourceOptions { Scale = 8193 } },
            { "dpi 0", new SourceOptions { Dpi = 0 } },
            { "dpi negative", new SourceOptions { Dpi = -96 } },
            { "dpi NaN", new SourceOptions { Dpi = double.NaN } },
            { "dpi infinity", new SourceOptions { Dpi = double.PositiveInfinity } },
            { "dpi above maximum", new SourceOptions { Dpi = 2401 } },
            { "elevation above 90", new SourceOptions { Scad = new ScadViewOptions { Elevation = 91 } } },
            { "elevation below -90", new SourceOptions { Scad = new ScadViewOptions { Elevation = -91 } } },
            { "elevation NaN", new SourceOptions { Scad = new ScadViewOptions { Elevation = double.NaN } } },
            { "azimuth infinity", new SourceOptions { Scad = new ScadViewOptions { Azimuth = double.PositiveInfinity } } },
            { "time too large", new SourceOptions { Scad = new ScadViewOptions { Time = 2e9 } } },
            { "time too small", new SourceOptions { Scad = new ScadViewOptions { Time = -2e9 } } },
            { "triangle limit 0", new SourceOptions { Scad = new ScadViewOptions { MaxTriangles = 0 } } },
            { "unknown view", new SourceOptions { Scad = new ScadViewOptions { View = "sideways" } } },
            { "bad definition name", new SourceOptions { Scad = new ScadViewOptions { Defines = new Dictionary<string, object?> { ["not a name"] = 1.0 } } } }
        };
    }

    /// <summary>
    ///     Gets options at the edge of what is allowed, each with a label.
    /// </summary>
    /// <returns>The test data.</returns>
    public static TheoryData<string, SourceOptions> BoundaryOptions()
    {
        return new TheoryData<string, SourceOptions>
        {
            { "defaults", new SourceOptions() },
            { "width 1", new SourceOptions { Width = 1 } },
            { "height 1", new SourceOptions { Height = 1 } },
            { "width above the maximum", new SourceOptions { Width = 9000 } },
            { "scale minimum", new SourceOptions { Scale = 0.001 } },
            { "scale maximum", new SourceOptions { Scale = 8192 } },
            { "dpi tiny", new SourceOptions { Dpi = 0.001 } },
            { "dpi maximum", new SourceOptions { Dpi = SourceLoader.MaxDpi } },
            { "elevation top", new SourceOptions { Scad = new ScadViewOptions { Elevation = 90 } } },
            { "elevation bottom", new SourceOptions { Scad = new ScadViewOptions { Elevation = -90 } } },
            { "time limits", new SourceOptions { Scad = new ScadViewOptions { Time = 1e9 } } },
            { "negative time limit", new SourceOptions { Scad = new ScadViewOptions { Time = -1e9 } } },
            { "one triangle", new SourceOptions { Scad = new ScadViewOptions { MaxTriangles = 1 } } },
            { "a view in capitals", new SourceOptions { Scad = new ScadViewOptions { View = "FRONT" } } },
            { "a definition", new SourceOptions { Scad = new ScadViewOptions { Defines = new Dictionary<string, object?> { ["size"] = 3.0 } } } }
        };
    }

    /// <summary>
    ///     Test that unusable options are refused with the usage-error code by <see cref="SourceLoader.Load(string, SourceOptions)"/>
    ///     before the file is touched: the file does not exist, and the error is still about the options.
    /// </summary>
    /// <param name="label">What is wrong.</param>
    /// <param name="options">The options.</param>
    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void SourceLoader_Load_InvalidOptionsAndMissingFile_ThrowsUsageErrorBeforeTouchingTheFile(string label, SourceOptions options)
    {
        // Arrange: a path that does not exist
        using var directory = new TempDirectory();
        var missing = directory.Combine("missing.png");

        // Act
        var exception = Assert.Throws<ViewerException>(() => SourceLoader.Load(missing, options));

        // Assert
        Assert.True(exception.Code == ErrorCodes.UsageError, $"{label}: {exception.Code} {exception.Message}");
    }

    /// <summary>
    ///     Test that <see cref="SourceLoader.Load(SourceInfo, SourceOptions)"/> and
    ///     <see cref="SourceLoader.OpenPages(SourceInfo, SourceOptions)"/> refuse the same options with the same code, for
    ///     facts about a file that no longer exists.
    /// </summary>
    /// <param name="label">What is wrong.</param>
    /// <param name="options">The options.</param>
    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void SourceLoader_LoadAndOpenPagesOfInfo_InvalidOptions_ThrowUsageErrorBeforeTouchingTheFile(string label, SourceOptions options)
    {
        // Arrange: facts about a file that is then deleted
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "png");
        var info = SourceLoader.Probe(path);
        File.Delete(path);

        // Act
        var load = Assert.Throws<ViewerException>(() => SourceLoader.Load(info, options));
        var open = Assert.Throws<ViewerException>(() => SourceLoader.OpenPages(info, options));

        // Assert
        Assert.True(load.Code == ErrorCodes.UsageError, $"{label}: {load.Code} {load.Message}");
        Assert.True(open.Code == ErrorCodes.UsageError, $"{label}: {open.Code} {open.Message}");
    }

    /// <summary>
    ///     Test that options at the edge of what is allowed are accepted by the validator.
    /// </summary>
    /// <param name="label">What is at the edge.</param>
    /// <param name="options">The options.</param>
    [Theory]
    [MemberData(nameof(BoundaryOptions))]
    public void SourceLoader_FindOptionProblem_BoundaryOptions_AreAccepted(string label, SourceOptions options)
    {
        // Act
        var problem = SourceLoader.FindOptionProblem(options);

        // Assert
        Assert.True(problem is null, $"{label}: {problem?.Message}");
    }

    /// <summary>
    ///     Test that a width above the maximum is not an option problem but the size-limit error of the load, as before.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_WidthAboveTheMaximum_IsStillSizeLimit()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "svg");

        // Act
        var exception = Assert.Throws<ViewerException>(() => SourceLoader.Load(path, new SourceOptions { Width = 9000 }));

        // Assert
        Assert.Equal(ErrorCodes.SizeLimit, exception.Code);
    }

    /// <summary>
    ///     Test that a page that does not exist keeps its own error, and is not an option problem.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PageBeyondTheLast_IsStillPageOutOfRange()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        var exception = Assert.Throws<ViewerException>(() => SourceLoader.Load(path, new SourceOptions { Page = 9 }));

        // Assert
        Assert.Equal(ErrorCodes.PageOutOfRange, exception.Code);
    }

    /// <summary>
    ///     Test that the problem names the value and the hint of an unknown view lists the views.
    /// </summary>
    [Fact]
    public void SourceLoader_FindOptionProblem_UnknownView_NamesTheViewAndListsTheViews()
    {
        // Act
        var problem = SourceLoader.FindOptionProblem(new SourceOptions { Scad = new ScadViewOptions { View = "sideways" } });

        // Assert
        Assert.NotNull(problem);
        Assert.Contains("'sideways'", problem.Value.Message, StringComparison.Ordinal);
        Assert.Contains("front", problem.Value.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the maximum resolution is published as 2400.
    /// </summary>
    [Fact]
    public void SourceLoader_MaxDpi_Is2400()
    {
        // Act / Assert
        Assert.Equal(2400d, SourceLoader.MaxDpi);
    }
}
