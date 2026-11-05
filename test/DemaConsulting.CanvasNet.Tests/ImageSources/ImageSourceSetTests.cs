// cspell:ignore hrefs
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Unit tests for <see cref="ImageSourceSet"/>: the validation of claims, the precedence rule (the later source wins),
///     the immutability of <see cref="ImageSourceSet.Add"/>, the longest-suffix lookup and the scheme syntax.
/// </summary>
public class ImageSourceSetTests
{
    /// <summary>
    ///     Test that the later of two sources that claim the same extension or scheme wins, and that the lists are the
    ///     sorted union without duplicates.
    /// </summary>
    [Fact]
    public void ImageSourceSet_Constructor_SameClaims_LaterSourceWins()
    {
        // Arrange
        var builtIn = new StubImageSource("built-in", [".png", ".svg"], ["camera"]);
        var mine = new StubImageSource("mine", [".PNG", ".mine"], ["Camera", "myapp"]);

        // Act
        var set = new ImageSourceSet([builtIn, mine]);

        // Assert
        Assert.Same(mine, set.FindByExtension("a.png"));
        Assert.Same(builtIn, set.FindByExtension("a.svg"));
        Assert.Same(mine, set.FindByScheme("camera"));
        Assert.Equal([".mine", ".png", ".svg"], set.Extensions);
        Assert.Equal(["camera", "myapp"], set.Schemes);
        Assert.Equal(2, set.Count);
        Assert.Same(builtIn, set[0]);
        Assert.Equal([builtIn, mine], set);
    }

    /// <summary>
    ///     Test that the extension and scheme lists cannot be changed by casting them to an array, a list or
    ///     <see cref="IList{T}"/>.
    /// </summary>
    [Fact]
    public void ImageSourceSet_ExtensionsAndSchemes_AreReadOnly()
    {
        // Arrange
        var set = new ImageSourceSet([new StubImageSource("a", [".a"], ["alpha"])]);

        // Act / Assert
        ReadOnlyListAssert.IsReadOnly(set.Extensions, ".x");
        ReadOnlyListAssert.IsReadOnly(set.Schemes, "x");
        Assert.Equal([".a"], set.Extensions);
        Assert.Equal(["alpha"], set.Schemes);
    }

    /// <summary>
    ///     Test that <see cref="ImageSourceSet.Add"/> returns a new set with the source last and leaves the original
    ///     unchanged.
    /// </summary>
    [Fact]
    public void ImageSourceSet_Add_ReturnsNewSetAndLeavesOriginalUnchanged()
    {
        // Arrange
        var first = new StubImageSource("first", [".a"]);
        var second = new StubImageSource("second", [".a"]);
        var original = new ImageSourceSet([first]);

        // Act
        var extended = original.Add(second);

        // Assert
        Assert.NotSame(original, extended);
        Assert.Single(original);
        Assert.Same(first, original.FindByExtension("x.a"));
        Assert.Equal(2, extended.Count);
        Assert.Same(second, extended.FindByExtension("x.a"));
    }

    /// <summary>
    ///     Test that the lookup by extension picks the longest claimed suffix, ignores case and the folder part, and finds
    ///     nothing for an unclaimed name.
    /// </summary>
    [Fact]
    public void ImageSourceSet_FindByExtension_LongestSuffixCaseInsensitive()
    {
        // Arrange
        var json = new StubImageSource("json", [".json"]);
        var chart = new StubImageSource("chart", [".vl.json"]);
        var set = new ImageSourceSet([json, chart]);

        // Act / Assert
        Assert.Same(chart, set.FindByExtension("dir.json/a.vl.json"));
        Assert.Same(chart, set.FindByExtension("A.VL.JSON"));
        Assert.Same(json, set.FindByExtension("a.json"));
        Assert.Same(json, set.FindByExtension("sub\\a.JSON"));
        Assert.Null(set.FindByExtension("a.txt"));
        Assert.Null(set.FindByExtension("json"));
        Assert.Null(set.FindByExtension("dir.json/readme"));
        Assert.Throws<ArgumentNullException>(() => set.FindByExtension(null!));
    }

    /// <summary>
    ///     Test that the lookup by scheme ignores case and finds nothing for an unclaimed scheme.
    /// </summary>
    [Fact]
    public void ImageSourceSet_FindByScheme_CaseInsensitive()
    {
        // Arrange
        var source = new StubImageSource("live", null, ["test-scheme"]);
        var set = new ImageSourceSet([source]);

        // Act / Assert
        Assert.Same(source, set.FindByScheme("TEST-SCHEME"));
        Assert.Null(set.FindByScheme("http"));
        Assert.Throws<ArgumentNullException>(() => set.FindByScheme(null!));
    }

    /// <summary>
    ///     Test that a claimed extension that is not a dot followed by a name is rejected with the name of the source.
    /// </summary>
    /// <param name="extension">The claimed extension.</param>
    [Theory]
    [InlineData("png")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData(".a/b")]
    [InlineData(".a\\b")]
    [InlineData(".a b")]
    [InlineData(".a:b")]
    public void ImageSourceSet_Constructor_InvalidExtension_ThrowsArgumentException(string extension)
    {
        // Arrange
        var source = new StubImageSource("odd", [extension]);

        // Act
        var error = Assert.Throws<ArgumentException>(() => new ImageSourceSet([source]));

        // Assert
        Assert.Contains("odd", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a null source, a nameless source and a source that returns a null list are rejected.
    /// </summary>
    [Fact]
    public void ImageSourceSet_Constructor_InvalidSources_Throw()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new ImageSourceSet(null!));
        Assert.Throws<ArgumentException>(() => new ImageSourceSet([null!]));
        Assert.Throws<ArgumentException>(() => new ImageSourceSet([new StubImageSource(" ", [".a"])]));
        Assert.Throws<ArgumentException>(() => new ImageSourceSet([new NullListSource(true)]));
        Assert.Throws<ArgumentException>(() => new ImageSourceSet([new NullListSource(false)]));
        Assert.Throws<ArgumentNullException>(() => new ImageSourceSet([]).Add(null!));
    }

    /// <summary>
    ///     Test the scheme syntax of RFC 3986 as the resolver applies it: letters, digits, <c>+</c>, <c>-</c> and
    ///     <c>.</c>, starting with a letter, ended by a colon; a single letter is a drive.
    /// </summary>
    /// <param name="href">The link.</param>
    /// <param name="expected">The scheme in lower case, or <see langword="null"/>.</param>
    [Theory]
    [InlineData("http://x", "http")]
    [InlineData("HTTPS://x", "https")]
    [InlineData("test-scheme:frame", "test-scheme")]
    [InlineData("a+b.c-d1:rest", "a+b.c-d1")]
    [InlineData("secret.png::$DATA", "secret.png")]
    [InlineData("C:/x.png", null)]
    [InlineData("C:\\x.png", null)]
    [InlineData("c:", null)]
    [InlineData("1a:x", null)]
    [InlineData("a b:x", null)]
    [InlineData("sub/dir:x", null)]
    [InlineData("plain.png", null)]
    [InlineData(":x", null)]
    [InlineData("", null)]
    public void ImageSourceSet_GetScheme_FollowsRfc3986WithDriveLetterException(string href, string? expected)
    {
        // Arrange / Act
        var scheme = ImageSourceSet.GetScheme(href);

        // Assert
        Assert.Equal(expected, scheme);
    }

    /// <summary>
    ///     A source that returns a null list, to test the validation.
    /// </summary>
    private sealed class NullListSource : IImageSource
    {
        /// <summary>Whether the extensions (rather than the schemes) are the null list.</summary>
        private readonly bool _nullExtensions;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NullListSource"/> class.
        /// </summary>
        /// <param name="nullExtensions"><see langword="true"/> for null extensions, <see langword="false"/> for null schemes.</param>
        public NullListSource(bool nullExtensions)
        {
            _nullExtensions = nullExtensions;
        }

        /// <inheritdoc />
        public string Name => "null-list";

        /// <inheritdoc />
        public IReadOnlyList<string> Extensions => _nullExtensions ? null! : [];

        /// <inheritdoc />
        public IReadOnlyList<string> Schemes => _nullExtensions ? [] : null!;

        /// <inheritdoc />
        public ImageSourceResult Render(ImageSourceRequest request)
        {
            throw new NotSupportedException();
        }
    }
}
