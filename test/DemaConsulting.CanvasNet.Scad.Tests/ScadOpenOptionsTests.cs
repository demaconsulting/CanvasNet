namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Unit tests for <see cref="ScadOpenOptions"/>.
/// </summary>
public class ScadOpenOptionsTests
{
    /// <summary>
    ///     Validates the defaults of a new options object.
    /// </summary>
    [Fact]
    public void ScadOpenOptions_Defaults_AreUnsetAndEmpty()
    {
        // Arrange / Act
        var options = new ScadOpenOptions();

        // Assert
        Assert.Null(options.BaseDirectory);
        Assert.Null(options.SourceName);
        Assert.Null(options.FileResolver);
        Assert.Empty(options.LibraryPaths);
    }

    /// <summary>
    ///     Validates that valid values are stored.
    /// </summary>
    [Fact]
    public void ScadOpenOptions_ValidValues_AreStored()
    {
        // Arrange
        Func<string, Stream?> resolver = _ => null;

        // Act
        var options = new ScadOpenOptions
        {
            BaseDirectory = "base",
            LibraryPaths = ["lib1", "lib2"],
            FileResolver = resolver,
            SourceName = "name.scad",
        };

        // Assert
        Assert.Equal("base", options.BaseDirectory);
        Assert.Equal(["lib1", "lib2"], options.LibraryPaths);
        Assert.Same(resolver, options.FileResolver);
        Assert.Equal("name.scad", options.SourceName);
    }

    /// <summary>
    ///     Validates that empty or whitespace text values are rejected at initialization.
    /// </summary>
    /// <param name="property">The property to initialize.</param>
    /// <param name="value">The invalid value.</param>
    [Theory]
    [InlineData("BaseDirectory", "")]
    [InlineData("BaseDirectory", "  ")]
    [InlineData("SourceName", "")]
    [InlineData("SourceName", "\t")]
    [InlineData("LibraryPaths", "")]
    [InlineData("LibraryPaths", " ")]
    public void ScadOpenOptions_InvalidValues_Throw(string property, string value)
    {
        // Arrange / Act
        var ex = Assert.Throws<ArgumentException>(() => Create(property, value));

        // Assert: the exception names the property
        Assert.Equal(property, ex.ParamName);
    }

    /// <summary>
    ///     Initializes a single property with an invalid value.
    /// </summary>
    /// <param name="property">The property name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The options object (never reached for invalid values).</returns>
    private static ScadOpenOptions Create(string property, string value) => property switch
    {
        "BaseDirectory" => new ScadOpenOptions { BaseDirectory = value },
        "SourceName" => new ScadOpenOptions { SourceName = value },
        _ => new ScadOpenOptions { LibraryPaths = ["ok", value] },
    };

    /// <summary>
    ///     Validates that null library path lists and entries are rejected.
    /// </summary>
    [Fact]
    public void ScadOpenOptions_NullLibraryPaths_ThrowArgumentNullException()
    {
        // Arrange / Act / Assert
        var list = Assert.Throws<ArgumentNullException>(() => new ScadOpenOptions { LibraryPaths = null! });
        var entry = Assert.Throws<ArgumentNullException>(() => new ScadOpenOptions { LibraryPaths = ["a", null!] });
        Assert.Equal("LibraryPaths", list.ParamName);
        Assert.Equal("LibraryPaths", entry.ParamName);
    }

    /// <summary>
    ///     Validates that null optional text values are accepted.
    /// </summary>
    [Fact]
    public void ScadOpenOptions_NullTextValues_AreAccepted()
    {
        // Arrange / Act
        var options = new ScadOpenOptions { BaseDirectory = null, SourceName = null };

        // Assert
        Assert.Null(options.BaseDirectory);
        Assert.Null(options.SourceName);
    }

    /// <summary>
    ///     Validates that the library paths are copied defensively.
    /// </summary>
    [Fact]
    public void ScadOpenOptions_LibraryPaths_AreCopied()
    {
        // Arrange
        var source = new List<string> { "one" };
        var options = new ScadOpenOptions { LibraryPaths = source };

        // Act: change the caller's list afterwards
        source.Add("two");
        source[0] = "changed";

        // Assert: the options are unaffected and the exposed list is read-only
        Assert.Equal(["one"], options.LibraryPaths);
        Assert.True(((ICollection<string>)options.LibraryPaths).IsReadOnly);
    }

    /// <summary>
    ///     Validates that the file-resolution options (base directory, library paths, resolver) have no effect on
    ///     parsing: parsing never accesses another file; they are consumed only when the document is evaluated.
    /// </summary>
    [Fact]
    public void ScadOpenOptions_AcceptedButNotConsumedYet_DoNotAffectParse()
    {
        // Arrange: a nonexistent base directory, library paths and a resolver that must never be called
        var options = new ScadOpenOptions
        {
            BaseDirectory = Path.Combine(Path.GetTempPath(), "scad-does-not-exist-" + Guid.NewGuid().ToString("N")),
            LibraryPaths = ["missing-lib-1", "missing-lib-2"],
            FileResolver = _ => throw new InvalidOperationException("The resolver must not be invoked while parsing."),
        };

        // Act
        var withOptions = ScadDocument.Parse("include <x.scad>\nmodule m() {}", options);
        var without = ScadDocument.Parse("include <x.scad>\nmodule m() {}");

        // Assert
        Assert.Equal(without.Directives, withOptions.Directives);
        Assert.Equal(without.ModuleNames, withOptions.ModuleNames);
    }

    /// <summary>
    ///     Validates by reflection that every property can only be set during initialization.
    /// </summary>
    [Fact]
    public void ScadOpenOptions_Properties_AreInitOnly()
    {
        // Arrange
        var properties = typeof(ScadOpenOptions).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        // Act
        var mutable = properties
            .Where(p => p.SetMethod is not null
                && !p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)))
            .Select(p => p.Name)
            .ToList();

        // Assert
        Assert.Equal(4, properties.Length);
        Assert.Empty(mutable);
        Assert.True(typeof(ScadOpenOptions).IsSealed);
    }
}
