namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Unit tests for <see cref="VegaLiteOpenOptions"/> and <see cref="VegaLiteFontOptions"/>.
/// </summary>
public class VegaLiteOpenOptionsTests
{
    /// <summary>
    ///     Validates the defaults of a new options object.
    /// </summary>
    [Fact]
    public void VegaLiteOpenOptions_Defaults_AreUnsetAndEmpty()
    {
        // Arrange / Act
        var options = new VegaLiteOpenOptions();

        // Assert
        Assert.Null(options.BaseDirectory);
        Assert.Null(options.SourceName);
        Assert.Null(options.FileResolver);
        Assert.Null(options.Limits);
        Assert.Empty(options.DataPaths);
        Assert.Same(VegaLiteLimits.Default, options.EffectiveLimits);
    }

    /// <summary>
    ///     Validates that valid values are stored.
    /// </summary>
    [Fact]
    public void VegaLiteOpenOptions_ValidValues_AreStored()
    {
        // Arrange
        Func<string, Stream?> resolver = _ => null;
        var limits = new VegaLiteLimits { MaxJsonDepth = 8 };

        // Act
        var options = new VegaLiteOpenOptions
        {
            BaseDirectory = "base",
            DataPaths = ["d1", "d2"],
            FileResolver = resolver,
            SourceName = "name.vl.json",
            Limits = limits,
        };

        // Assert
        Assert.Equal("base", options.BaseDirectory);
        Assert.Equal(["d1", "d2"], options.DataPaths);
        Assert.Same(resolver, options.FileResolver);
        Assert.Equal("name.vl.json", options.SourceName);
        Assert.Same(limits, options.Limits);
        Assert.Same(limits, options.EffectiveLimits);
    }

    /// <summary>
    ///     Validates that empty or whitespace text values are rejected at initialization, naming the property.
    /// </summary>
    /// <param name="property">The property to initialize.</param>
    /// <param name="value">The invalid value.</param>
    [Theory]
    [InlineData("BaseDirectory", "")]
    [InlineData("BaseDirectory", "  ")]
    [InlineData("SourceName", "")]
    [InlineData("SourceName", "\t")]
    [InlineData("DataPaths", "")]
    [InlineData("DataPaths", " ")]
    public void VegaLiteOpenOptions_InvalidValues_Throw(string property, string value)
    {
        // Arrange / Act
        var ex = Assert.Throws<ArgumentException>(() => property switch
        {
            "BaseDirectory" => new VegaLiteOpenOptions { BaseDirectory = value },
            "SourceName" => new VegaLiteOpenOptions { SourceName = value },
            _ => new VegaLiteOpenOptions { DataPaths = [value] },
        });

        // Assert
        Assert.Equal(property, ex.ParamName);
    }

    /// <summary>
    ///     Validates that null list and null entries are rejected.
    /// </summary>
    [Fact]
    public void VegaLiteOpenOptions_NullDataPaths_Throw()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new VegaLiteOpenOptions { DataPaths = null! });
        Assert.Throws<ArgumentNullException>(() => new VegaLiteOpenOptions { DataPaths = [null!] });
    }

    /// <summary>
    ///     Validates that the data paths are copied and exposed read-only.
    /// </summary>
    [Fact]
    public void VegaLiteOpenOptions_DataPaths_AreCopiedAndReadOnly()
    {
        // Arrange
        var source = new List<string> { "a" };
        var options = new VegaLiteOpenOptions { DataPaths = source };

        // Act
        source.Add("b");

        // Assert
        Assert.Equal(["a"], options.DataPaths);
        Assert.True(options.DataPaths is not IList<string> list || list.IsReadOnly);
    }

    /// <summary>
    ///     Validates that the effective copy carries all settings and fills the two defaults.
    /// </summary>
    [Fact]
    public void VegaLiteOpenOptions_WithEffective_CopiesAndFillsDefaults()
    {
        // Arrange
        Func<string, Stream?> resolver = _ => null;
        var options = new VegaLiteOpenOptions { DataPaths = ["x"], FileResolver = resolver, Limits = new VegaLiteLimits { MaxTicks = 3 } };

        // Act
        var effective = options.WithEffective("dir", "spec.json");

        // Assert
        Assert.Equal("dir", effective.BaseDirectory);
        Assert.Equal("spec.json", effective.SourceName);
        Assert.Equal(["x"], effective.DataPaths);
        Assert.Same(resolver, effective.FileResolver);
        Assert.Same(options.Limits, effective.Limits);
    }
}

/// <summary>
///     Unit tests for <see cref="VegaLiteFontOptions"/>.
/// </summary>
public class VegaLiteFontOptionsTests
{
    /// <summary>
    ///     Validates that the defaults are reproducible: no system fonts, no extra directories.
    /// </summary>
    [Fact]
    public void VegaLiteFontOptions_Defaults_AreBundledOnly()
    {
        // Arrange / Act
        var options = new VegaLiteFontOptions();

        // Assert
        Assert.False(options.UseSystemFonts);
        Assert.Empty(options.AdditionalDirectories);
    }

    /// <summary>
    ///     Validates that directories are copied, validated and read-only.
    /// </summary>
    [Fact]
    public void VegaLiteFontOptions_AdditionalDirectories_AreCopiedAndValidated()
    {
        // Arrange
        var source = new List<string> { "fonts" };

        // Act
        var options = new VegaLiteFontOptions { UseSystemFonts = true, AdditionalDirectories = source };
        source.Add("more");

        // Assert
        Assert.True(options.UseSystemFonts);
        Assert.Equal(["fonts"], options.AdditionalDirectories);
        Assert.Throws<ArgumentNullException>(() => new VegaLiteFontOptions { AdditionalDirectories = null! });
        Assert.Throws<ArgumentException>(() => new VegaLiteFontOptions { AdditionalDirectories = [" "] });
        Assert.Throws<ArgumentException>(() => new VegaLiteFontOptions { AdditionalDirectories = [null!] });
    }
}
