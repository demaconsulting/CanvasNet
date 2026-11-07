// cspell:ignore href hrefs
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Unit tests for the prefixed parameters of the fragment of a link (<c>def.width=20</c>, <c>param.cutoff=5</c>):
///     <see cref="ImageFragment.GetPrefixed(string)"/>, the value grammars of <see cref="ImageParameterValues"/>, and how
///     <see cref="FolderImageResolver"/> treats a prefix a source declares
///     (<see cref="IFragmentImageSource.FragmentParameterPrefixes"/>).
/// </summary>
public class ImageFragmentPrefixTests
{
    /// <summary>The extension the stubs claim.</summary>
    private const string Ext = ".stub";

    /// <summary>
    ///     A source that declares names and prefixes.
    /// </summary>
    private sealed class PrefixStubImageSource : StubImageSource, IFragmentImageSource
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PrefixStubImageSource"/> class.
        /// </summary>
        /// <param name="names">The fragment parameter names the source declares.</param>
        /// <param name="prefixes">The prefixes it declares; may contain a null or empty entry.</param>
        public PrefixStubImageSource(string[] names, IReadOnlyList<string>? prefixes)
            : base("prefix", [Ext])
        {
            FragmentParameters = names;
            FragmentParameterPrefixes = prefixes!;
        }

        /// <inheritdoc />
        public IReadOnlyList<string> FragmentParameters { get; }

        /// <inheritdoc />
        public IReadOnlyList<string> FragmentParameterPrefixes { get; }
    }

    /// <summary>
    ///     Test that <see cref="ImageFragment.GetPrefixed(string)"/> returns the names without the prefix, in order, as
    ///     written, decoded, with the prefix matched case-insensitively, and leaves out other parameters and a bare prefix.
    /// </summary>
    [Fact]
    public void ImageFragment_GetPrefixed_ReturnsNamesWithoutPrefixInOrder()
    {
        // Arrange
        var fragment = ImageFragment.Parse("view=top&def.Width=20&DEF.label=%22A%22&def.&other=1&def.a%3Db=x");

        // Act
        var found = fragment.GetPrefixed("def.");

        // Assert: the prefix is matched without case, the names are kept as written, and "def." alone is not a parameter
        Assert.Equal(
            [new ImageFragmentParameter("Width", "20"), new ImageFragmentParameter("label", "\"A\""), new ImageFragmentParameter("a=b", "x")],
            found);
    }

    /// <summary>
    ///     Test that a name given more than once has its last value, in the position of its first occurrence, and that
    ///     names that differ in case are different names.
    /// </summary>
    [Fact]
    public void ImageFragment_GetPrefixed_DuplicateName_LastValueWinsAtFirstPosition()
    {
        // Arrange
        var fragment = ImageFragment.Parse("def.a=1&def.b=2&def.a=3&def.A=4");

        // Act
        var found = fragment.GetPrefixed("def.");

        // Assert
        Assert.Equal(
            [new ImageFragmentParameter("a", "3"), new ImageFragmentParameter("b", "2"), new ImageFragmentParameter("A", "4")],
            found);
    }

    /// <summary>
    ///     Test that an empty fragment and a fragment with a problem have no prefixed parameters, and that a null or empty
    ///     prefix is refused.
    /// </summary>
    [Fact]
    public void ImageFragment_GetPrefixed_EmptyFragmentOrBadPrefix()
    {
        // Act / Assert
        Assert.Empty(ImageFragment.Empty.GetPrefixed("def."));
        Assert.Empty(ImageFragment.Parse(new string('a', ImageFragment.MaxLength + 1)).GetPrefixed("def."));
        Assert.Throws<ArgumentNullException>(() => ImageFragment.Empty.GetPrefixed(null!));
        Assert.Throws<ArgumentException>(() => ImageFragment.Empty.GetPrefixed(string.Empty));
    }

    /// <summary>
    ///     Test the variable grammar (the one of <c>-D</c>): booleans, finite numbers, quoted and unquoted text.
    /// </summary>
    /// <param name="text">The text of the value.</param>
    /// <param name="expected">The value it means.</param>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("20", 20.0)]
    [InlineData("-1.5", -1.5)]
    [InlineData("1e3", 1000.0)]
    [InlineData("\"a b\"", "a b")]
    [InlineData("'a b'", "a b")]
    [InlineData("a b", "a b")]
    [InlineData("", "")]
    [InlineData("\"", "\"")]
    [InlineData("\"a'", "\"a'")]
    [InlineData("TRUE", "TRUE")]
    [InlineData("NaN", "NaN")]
    [InlineData("1e999", "1e999")]
    [InlineData("null", "null")]
    [InlineData("{x}", "{x}")]
    public void ImageParameterValues_TryParseVariable_ReadsTheGrammarOfDashD(string text, object expected)
    {
        // Act
        var ok = ImageParameterValues.TryParseVariable(text, out var value, out var problem);

        // Assert
        Assert.True(ok);
        Assert.Null(problem);
        Assert.Equal(expected, value);
    }

    /// <summary>
    ///     Test that a vector (a value starting with <c>[</c>) is not a variable value.
    /// </summary>
    [Fact]
    public void ImageParameterValues_TryParseVariable_Vector_IsRefused()
    {
        // Act
        var ok = ImageParameterValues.TryParseVariable("[1,2]", out var value, out var problem);

        // Assert
        Assert.False(ok);
        Assert.Null(value);
        Assert.Contains("vector", problem, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => ImageParameterValues.TryParseVariable(null!, out _, out _));
    }

    /// <summary>
    ///     Test the parameter grammar for the scalar values and for text.
    /// </summary>
    /// <param name="text">The text of the value.</param>
    /// <param name="expected">The value it means.</param>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("50", 50.0)]
    [InlineData("-0.25", -0.25)]
    [InlineData("EU", "EU")]
    [InlineData("a b", "a b")]
    [InlineData("'EU west'", "EU west")]
    [InlineData("\"EU west\"", "EU west")]
    [InlineData("\"a\\\"b\\u00e9\"", "a\"b\u00e9")]
    [InlineData("", "")]
    [InlineData("NaN", "NaN")]
    [InlineData("1e999", "1e999")]
    [InlineData("2024-01-31", "2024-01-31")]
    [InlineData("nul", "nul")]
    public void ImageParameterValues_TryParseParameter_ReadsScalarsAndText(string text, object expected)
    {
        // Act
        var ok = ImageParameterValues.TryParseParameter(text, out var value, out var problem);

        // Assert
        Assert.True(ok);
        Assert.Null(problem);
        Assert.Equal(expected, value);
    }

    /// <summary>
    ///     Test that <c>null</c> is a null value (it clears a selection).
    /// </summary>
    [Fact]
    public void ImageParameterValues_TryParseParameter_Null_IsNull()
    {
        // Act
        var ok = ImageParameterValues.TryParseParameter("null", out var value, out var problem);

        // Assert
        Assert.True(ok);
        Assert.Null(value);
        Assert.Null(problem);
        Assert.Throws<ArgumentNullException>(() => ImageParameterValues.TryParseParameter(null!, out _, out _));
    }

    /// <summary>
    ///     Test that a JSON array and a JSON object become lists and dictionaries of plain values, as a selection takes them.
    /// </summary>
    [Fact]
    public void ImageParameterValues_TryParseParameter_JsonObjectAndArray_BecomePlainValues()
    {
        // Act
        var okObject = ImageParameterValues.TryParseParameter("{\"x\":[2,6],\"y\":{\"a\":null,\"b\":true,\"c\":\"t\"}}", out var brush, out _);
        var okArray = ImageParameterValues.TryParseParameter("[2, 6.5, \"a\"]", out var window, out _);

        // Assert
        Assert.True(okObject);
        var map = Assert.IsType<Dictionary<string, object?>>(brush);
        Assert.Equal(new object?[] { 2.0, 6.0 }, Assert.IsType<List<object?>>(map["x"]));
        var inner = Assert.IsType<Dictionary<string, object?>>(map["y"]);
        Assert.Null(inner["a"]);
        Assert.True(Assert.IsType<bool>(inner["b"]));
        Assert.Equal("t", inner["c"]);
        Assert.True(okArray);
        Assert.Equal(new object?[] { 2.0, 6.5, "a" }, Assert.IsType<List<object?>>(window));
    }

    /// <summary>
    ///     Test that text that starts like JSON but is not valid JSON, is too deep, holds too many values or has a number
    ///     that is not finite is refused with a one-line reason.
    /// </summary>
    /// <param name="text">The text.</param>
    [Theory]
    [InlineData("{x")]
    [InlineData("{\"x\":}")]
    [InlineData("[1,")]
    [InlineData("[1] 2")]
    [InlineData("\"abc")]
    [InlineData("{\"x\":1e999}")]
    [InlineData("[1,]")]
    public void ImageParameterValues_TryParseParameter_BadJson_IsRefusedWithAReason(string text)
    {
        // Act
        var ok = ImageParameterValues.TryParseParameter(text, out var value, out var problem);

        // Assert
        Assert.False(ok);
        Assert.Null(value);
        Assert.False(string.IsNullOrEmpty(problem));
        Assert.DoesNotContain('\n', problem);
        Assert.DoesNotContain("LineNumber", problem, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a value nested beyond <see cref="ImageParameterValues.MaxDepth"/> levels, and one that holds more than
    ///     <see cref="ImageParameterValues.MaxValues"/> values, are refused, while the limits themselves are accepted.
    /// </summary>
    [Fact]
    public void ImageParameterValues_TryParseParameter_Limits()
    {
        // Arrange
        string Nested(int depth) => new string('[', depth) + new string(']', depth);
        var atLimit = "[" + string.Join(',', Enumerable.Repeat("0", ImageParameterValues.MaxValues - 1)) + "]";
        var overLimit = "[" + string.Join(',', Enumerable.Repeat("0", ImageParameterValues.MaxValues)) + "]";

        // Act / Assert
        Assert.True(ImageParameterValues.TryParseParameter(Nested(ImageParameterValues.MaxDepth), out _, out _));
        Assert.False(ImageParameterValues.TryParseParameter(Nested(ImageParameterValues.MaxDepth + 1), out _, out var deep));
        Assert.NotNull(deep);
        Assert.True(ImageParameterValues.TryParseParameter(atLimit, out _, out _));
        Assert.False(ImageParameterValues.TryParseParameter(overLimit, out _, out var many));
        Assert.Contains("more than 65536 values", many, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that <see cref="ImageParameterValues.ReadVariables(ImageFragment, string)"/> reads every prefixed parameter
    ///     with the variable grammar, after the fragment is split and decoded.
    /// </summary>
    [Fact]
    public void ImageParameterValues_ReadVariables_ReadsPrefixedValues()
    {
        // Arrange: the quotes of label are %22, and the ampersand inside the text is %26
        var fragment = ImageFragment.Parse("view=top&def.width=20&def.label=%22A%26B%22&def.show=true&def.name=plain");

        // Act
        var variables = ImageParameterValues.ReadVariables(fragment, "def.");

        // Assert
        Assert.Equal(4, variables.Count);
        Assert.Equal(20.0, variables["width"]);
        Assert.Equal("A&B", variables["label"]);
        Assert.True(Assert.IsType<bool>(variables["show"]));
        Assert.Equal("plain", variables["name"]);
        Assert.Empty(ImageParameterValues.ReadVariables(ImageFragment.Empty, "def."));
        Assert.Throws<ArgumentNullException>(() => ImageParameterValues.ReadVariables(null!, "def."));
    }

    /// <summary>
    ///     Test that a variable value that is not usable refuses the link with the invalid-fragment code and names the
    ///     parameter with its prefix.
    /// </summary>
    [Fact]
    public void ImageParameterValues_ReadVariables_Vector_IsRefusedAsInvalidFragment()
    {
        // Arrange
        var fragment = ImageFragment.Parse("def.size=[1,2]");

        // Act
        var ex = Assert.Throws<ImageSourceRefusedException>(() => ImageParameterValues.ReadVariables(fragment, "def."));

        // Assert
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, ex.Code);
        Assert.Contains("'def.size'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'[1,2]'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("vector values are not supported", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that <see cref="ImageParameterValues.ReadParameters(ImageFragment, string)"/> reads JSON values, and refuses
    ///     a malformed one with the invalid-fragment code.
    /// </summary>
    [Fact]
    public void ImageParameterValues_ReadParameters_ReadsJsonAndRefusesMalformedValues()
    {
        // Arrange: %7B and %7D are the braces, %22 the quotes, %3A the colon
        var good = ImageFragment.Parse("param.cutoff=5&param.brush=%7B%22x%22%3A[2,6]%7D&param.region=EU");
        var bad = ImageFragment.Parse("param.brush=%7Bx");

        // Act
        var parameters = ImageParameterValues.ReadParameters(good, "param.");
        var ex = Assert.Throws<ImageSourceRefusedException>(() => ImageParameterValues.ReadParameters(bad, "param."));

        // Assert
        Assert.Equal(5.0, parameters["cutoff"]);
        Assert.Equal("EU", parameters["region"]);
        var brush = Assert.IsType<Dictionary<string, object?>>(parameters["brush"]);
        Assert.Equal(new object?[] { 2.0, 6.0 }, Assert.IsType<List<object?>>(brush["x"]));
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, ex.Code);
        Assert.Contains("'param.brush'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a source that declares a prefix understands every parameter with it (no unknown-parameter warning),
    ///     still warns about a parameter that has neither a name nor a prefix match, and that the source gets the fragment.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_DeclaredPrefix_UnderstandsEveryParameterWithIt()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var stub = new PrefixStubImageSource(["page"], ["def."]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act: a prefixed name, the prefix in other case, a declared name, an undeclared one, and the bare prefix
        using var surface = resolver.Resolve("a.stub#def.width=20&DEF.Label=x&page=2&pag=3&def.", 10, 10);

        // Assert
        Assert.NotNull(surface);
        var unknown = resolver.Diagnostics.Where(d => d.Code == FolderImageResolver.FragmentUnknownParameterCode).ToList();
        Assert.Equal(2, unknown.Count);
        Assert.Contains("'pag'", unknown[0].Message, StringComparison.Ordinal);
        Assert.Contains("'def.'", unknown[1].Message, StringComparison.Ordinal);
        Assert.Equal(["def.width", "DEF.Label", "page", "pag", "def."], Assert.Single(stub.Requests).Fragment.Parameters.Select(p => p.Name));
    }

    /// <summary>
    ///     Test that the duplicate warning of a prefixed parameter compares the part after the prefix as written (so two
    ///     names that differ in case are two parameters) and the prefix without case.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_DeclaredPrefix_DuplicatesCompareTheNameAsWritten()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var stub = new PrefixStubImageSource([], ["def."]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act: a and A are different; def.b given twice with different values (the prefix in two cases) is a duplicate; with the same value it is not
        using var surface = resolver.Resolve("a.stub#def.a=1&def.A=2&def.b=1&DEF.b=2&def.c=3&DEF.c=3", 10, 10);

        // Assert
        Assert.NotNull(surface);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentDuplicateParameterCode, note.Code);
        Assert.Contains("'def.b'", note.Message, StringComparison.Ordinal);
        Assert.Contains("'2'", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a source that declares no prefix (the default of the interface, a null list, a null or empty entry)
    ///     reports a prefixed parameter as unknown.
    /// </summary>
    /// <param name="kind">How the prefixes are declared: 0 not at all, 1 a null list, 2 a null entry, 3 an empty entry.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void FolderImageResolver_Resolve_NoDeclaredPrefix_PrefixedParameterIsUnknown(int kind)
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        IImageSource stub = kind switch
        {
            0 => new FragmentStubImageSource("stub", ["page"], [Ext]),
            1 => new PrefixStubImageSource(["page"], null),
            2 => new PrefixStubImageSource(["page"], [null!]),
            _ => new PrefixStubImageSource(["page"], [string.Empty])
        };
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var surface = resolver.Resolve("a.stub#def.width=20", 10, 10);

        // Assert: drawn, with the unknown-parameter warning
        Assert.NotNull(surface);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, note.Code);
        Assert.Contains("'def.width'", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the default of <see cref="IFragmentImageSource.FragmentParameterPrefixes"/> is an empty list.
    /// </summary>
    [Fact]
    public void IFragmentImageSource_FragmentParameterPrefixes_DefaultsToNone()
    {
        // Arrange
        IFragmentImageSource source = new FragmentStubImageSource("stub", ["page"]);

        // Act / Assert
        Assert.Empty(source.FragmentParameterPrefixes);
    }
}
