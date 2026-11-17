using System.Text;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Unit tests for <see cref="ScadDocument"/>.
/// </summary>
public class ScadDocumentTests
{
    /// <summary>The maximum accepted source length (UTF-16 characters).</summary>
    private const int MaxSourceLength = 4_194_304;

    /// <summary>
    ///     A readable stream that never ends (it yields blanks forever) and counts how many bytes were consumed.
    /// </summary>
    private sealed class EndlessBlankStream : Stream
    {
        /// <summary>Gets the total number of bytes handed to readers.</summary>
        public long BytesRead { get; private set; }

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            // Nothing to flush: the stream is read-only
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Fill(buffer, (byte)' ', offset, count);
            BytesRead += count;
            return count;
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    ///     Encodes text as UTF-8 bytes, optionally with a byte-order mark.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="bom">Whether to prefix the UTF-8 byte-order mark.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Utf8(string text, bool bom) =>
        [.. bom ? new UTF8Encoding(true).GetPreamble() : [], .. Encoding.UTF8.GetBytes(text)];

    /// <summary>
    ///     Validates that a null source is rejected.
    /// </summary>
    [Fact]
    public void ScadDocument_Parse_NullSource_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        var ex = Assert.Throws<ArgumentNullException>(() => ScadDocument.Parse(null!));
        Assert.Equal("source", ex.ParamName);
    }

    /// <summary>
    ///     Validates that empty, whitespace-only and comment-only sources are valid empty documents.
    /// </summary>
    /// <param name="source">The source.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   \n\t \r\n")]
    [InlineData("// only a comment")]
    [InlineData("/* only a\nblock comment */")]
    [InlineData("\uFEFF")]
    [InlineData(";;")]
    public void ScadDocument_Parse_EmptyOrCommentOnlySource_ReturnsEmptyDocument(string source)
    {
        // Arrange / Act
        var document = ScadDocument.Parse(source);

        // Assert
        Assert.Empty(document.Directives);
        Assert.Empty(document.ModuleNames);
        Assert.Empty(document.FunctionNames);
        Assert.Null(document.SourceName);
    }

    /// <summary>
    ///     Validates that a leading byte-order mark character is ignored.
    /// </summary>
    [Fact]
    public void ScadDocument_Parse_LeadingBom_IsIgnored()
    {
        // Arrange / Act
        var document = ScadDocument.Parse("\uFEFFmodule a() {}");

        // Assert
        Assert.Equal(["a"], document.ModuleNames);
    }

    /// <summary>
    ///     Validates that a syntax error is a <see cref="ScadSyntaxException"/> with a precise location.
    /// </summary>
    [Fact]
    public void ScadDocument_Parse_SyntaxError_ThrowsScadSyntaxExceptionWithLocation()
    {
        // Arrange / Act / Assert
        ScadTestHelpers.AssertSyntaxError(() => ScadDocument.Parse("a = 1;\nb = ;"), 2, 5, "Expected an expression but found ';'");
    }

    /// <summary>
    ///     Validates that the source name option appears in error locations.
    /// </summary>
    [Fact]
    public void ScadDocument_Parse_SourceNameOption_AppearsInErrorLocation()
    {
        // Arrange
        var options = new ScadOpenOptions { SourceName = "design/box.scad" };

        // Act / Assert
        ScadTestHelpers.AssertSyntaxError(() => ScadDocument.Parse("x = ;", options), 1, 5, "Expected an expression", "design/box.scad");
    }

    /// <summary>
    ///     Validates the source name rules for strings, streams and files.
    /// </summary>
    [Fact]
    public void ScadDocument_SourceName_FromOptionsOrPath_ReflectsSupplier()
    {
        // Arrange
        var path = ScadFixtureHelpers.PathOf("basic-shapes.scad");
        using var stream = new MemoryStream(Utf8("a = 1;", false));

        // Act
        var unnamed = ScadDocument.Parse("a = 1;");
        var named = ScadDocument.Parse("a = 1;", new ScadOpenOptions { SourceName = "n.scad" });
        var fromPath = ScadDocument.Open(path);
        var fromPathOverridden = ScadDocument.Open(path, new ScadOpenOptions { SourceName = "other" });
        var fromStream = ScadDocument.Open(stream);

        // Assert
        Assert.Null(unnamed.SourceName);
        Assert.Equal("n.scad", named.SourceName);
        Assert.Equal(path, fromPath.SourceName);
        Assert.Equal("other", fromPathOverridden.SourceName);
        Assert.Null(fromStream.SourceName);
    }

    /// <summary>
    ///     Validates that a null stream is rejected.
    /// </summary>
    [Fact]
    public void ScadDocument_Open_NullStream_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        var ex = Assert.Throws<ArgumentNullException>(() => ScadDocument.Open((Stream)null!));
        Assert.Equal("stream", ex.ParamName);
    }

    /// <summary>
    ///     Validates that a stream that cannot be read is rejected.
    /// </summary>
    [Fact]
    public void ScadDocument_Open_UnreadableStream_ThrowsArgumentException()
    {
        // Arrange: a disposed stream reports CanRead == false
        var stream = new MemoryStream();
        stream.Dispose();

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() => ScadDocument.Open(stream));
        Assert.Equal("stream", ex.ParamName);
    }

    /// <summary>
    ///     Validates UTF-8 input with and without a byte-order mark, including non-ASCII text.
    /// </summary>
    [Fact]
    public void ScadDocument_Open_Utf8WithAndWithoutBom_Parses()
    {
        // Arrange: non-ASCII text in a comment and a string so a wrong decoding would be visible
        const string Source = "// caf\u00e9\nmodule m() { echo(\"\u20ac\"); }\n";

        // Act
        using var plain = new MemoryStream(Utf8(Source, false));
        using var withBom = new MemoryStream(Utf8(Source, true));
        var a = ScadDocument.Open(plain);
        var b = ScadDocument.Open(withBom);

        // Assert
        Assert.Equal(["m"], a.ModuleNames);
        Assert.Equal(["m"], b.ModuleNames);
    }

    /// <summary>
    ///     Validates that the stream is left open and that reading starts at its current position.
    /// </summary>
    [Fact]
    public void ScadDocument_Open_Stream_IsLeftOpen()
    {
        // Arrange
        using var stream = new MemoryStream(Utf8("ignored garbage ?? ", false).Concat(Utf8("module kept() {}", false)).ToArray());
        stream.Position = "ignored garbage ?? ".Length;

        // Act
        var document = ScadDocument.Open(stream);

        // Assert: still usable, and only the remainder was parsed
        Assert.True(stream.CanRead);
        Assert.Equal(stream.Length, stream.Position);
        Assert.Equal(["kept"], document.ModuleNames);
    }

    /// <summary>
    ///     Validates that a stream that never ends is rejected after a bounded amount of reading.
    /// </summary>
    [Fact]
    public void ScadDocument_Open_NonTerminatingStream_ThrowsInvalidDataException()
    {
        // Arrange
        var stream = new EndlessBlankStream();

        // Act
        var ex = Assert.Throws<InvalidDataException>(() => ScadDocument.Open(stream));

        // Assert: reading stopped within a small margin beyond the limit, and it is not a syntax exception
        Assert.IsNotType<ScadSyntaxException>(ex);
        Assert.InRange(stream.BytesRead, MaxSourceLength, MaxSourceLength + 65536);
    }

    /// <summary>
    ///     Validates that a stream over the limit is rejected.
    /// </summary>
    [Fact]
    public void ScadDocument_Open_OverLimitStream_ThrowsInvalidDataException()
    {
        // Arrange: one blank over the limit
        var bytes = new byte[MaxSourceLength + 1];
        Array.Fill(bytes, (byte)' ');
        using var stream = new MemoryStream(bytes);

        // Act / Assert
        Assert.IsType<InvalidDataException>(Assert.ThrowsAny<Exception>(() => ScadDocument.Open(stream)));
    }

    /// <summary>
    ///     Validates that a string over the limit is rejected.
    /// </summary>
    [Fact]
    public void ScadDocument_Parse_OverLimit_ThrowsInvalidDataException()
    {
        // Arrange
        var source = new string(' ', MaxSourceLength + 1);

        // Act / Assert
        Assert.IsType<InvalidDataException>(Assert.ThrowsAny<Exception>(() => ScadDocument.Parse(source)));
    }

    /// <summary>
    ///     Validates that a null path is rejected.
    /// </summary>
    [Fact]
    public void ScadDocument_Open_NullPath_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => ScadDocument.Open((string)null!));
    }

    /// <summary>
    ///     Validates that an empty or whitespace path is rejected.
    /// </summary>
    /// <param name="path">The path.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void ScadDocument_Open_WhitespacePath_ThrowsArgumentException(string path)
    {
        // Arrange / Act / Assert: ArgumentException exactly (not the null subclass)
        var ex = Assert.ThrowsAny<ArgumentException>(() => ScadDocument.Open(path));
        Assert.IsNotType<ArgumentNullException>(ex);
    }

    /// <summary>
    ///     Validates that a fixture file parses and names its source with the path as supplied.
    /// </summary>
    [Fact]
    public void ScadDocument_Open_FixtureFile_ParsesAndNamesSource()
    {
        // Arrange
        var path = ScadFixtureHelpers.PathOf("parametric-bracket.scad");

        // Act
        var document = ScadDocument.Open(path);

        // Assert
        Assert.Equal(path, document.SourceName);
        Assert.NotEmpty(document.ModuleNames);
    }

    /// <summary>
    ///     Validates the natural exceptions for a missing file or directory.
    /// </summary>
    [Fact]
    public void ScadDocument_Open_MissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var missing = Path.Combine(ScadFixtureHelpers.Directory, "does-not-exist.scad");
        var missingDirectory = Path.Combine(ScadFixtureHelpers.Directory, "no-such-folder", "x.scad");

        // Act / Assert
        Assert.Throws<FileNotFoundException>(() => ScadDocument.Open(missing));
        Assert.Throws<DirectoryNotFoundException>(() => ScadDocument.Open(missingDirectory));
    }

    /// <summary>
    ///     Validates that the effective base directory defaults to the folder of the file and can be overridden.
    /// </summary>
    [Fact]
    public void ScadDocument_Open_DefaultBaseDirectory_IsFileFolder()
    {
        // Arrange
        var path = ScadFixtureHelpers.PathOf("basic-shapes.scad");

        // Act
        var defaulted = ScadDocument.Open(path);
        var overridden = ScadDocument.Open(path, new ScadOpenOptions { BaseDirectory = "custom-base" });
        var parsed = ScadDocument.Parse("a = 1;");

        // Assert: strings and streams have no folder, so the base directory stays unset
        Assert.Equal(ScadFixtureHelpers.Directory, defaulted.Options.BaseDirectory);
        Assert.Equal("custom-base", overridden.Options.BaseDirectory);
        Assert.Null(parsed.Options.BaseDirectory);
    }

    /// <summary>
    ///     Validates that include and use directives are recorded in source order with kind, path and location.
    /// </summary>
    [Fact]
    public void ScadDocument_Directives_IncludeAndUse_AreRecordedInOrder()
    {
        // Arrange
        var options = new ScadOpenOptions { SourceName = "d.scad" };

        // Act
        var document = ScadDocument.Parse("include <a.scad>\nx = 1;\n  use <lib/b.scad>\n", options);

        // Assert
        Assert.Equal(
            [
                new ScadDirective(ScadDirectiveKind.Include, "a.scad", new ScadSourceLocation("d.scad", 1, 1)),
                new ScadDirective(ScadDirectiveKind.Use, "lib/b.scad", new ScadSourceLocation("d.scad", 3, 3)),
            ],
            document.Directives);
    }

    /// <summary>
    ///     Validates that the directive list is read-only.
    /// </summary>
    [Fact]
    public void ScadDocument_Directives_ReadOnlyList()
    {
        // Arrange
        var document = ScadDocument.Parse("include <a>\nmodule m() {}\nfunction f() = 1;");

        // Act / Assert: every public list refuses mutation
        Assert.True(((ICollection<ScadDirective>)document.Directives).IsReadOnly);
        Assert.True(((ICollection<string>)document.ModuleNames).IsReadOnly);
        Assert.True(((ICollection<string>)document.FunctionNames).IsReadOnly);
        Assert.Throws<NotSupportedException>(() => ((ICollection<string>)document.ModuleNames).Add("x"));
    }

    /// <summary>
    ///     Validates that a file resolver is never invoked while parsing.
    /// </summary>
    [Fact]
    public void ScadDocument_Directives_FileResolverNotInvokedDuringParse()
    {
        // Arrange
        var calls = 0;
        var options = new ScadOpenOptions
        {
            FileResolver = _ =>
            {
                calls++;
                return null;
            },
        };

        // Act
        var document = ScadDocument.Parse("include <a.scad>\nuse <b.scad>", options);

        // Assert
        Assert.Equal(2, document.Directives.Count);
        Assert.Equal(0, calls);
    }

    /// <summary>
    ///     Validates that module and function names are reported for file scope only, in order.
    /// </summary>
    [Fact]
    public void ScadDocument_ModuleAndFunctionNames_FileScopeOnly_InOrder()
    {
        // Arrange
        const string Source = """
            function first() = 1;
            module outer() { module inner() {} function nested() = 2; }
            module second() {}
            { module in_block() {} }
            function third() = 3;
            """;

        // Act
        var document = ScadDocument.Parse(Source);

        // Assert: nested and block-scoped definitions are not listed
        Assert.Equal(["outer", "second"], document.ModuleNames);
        Assert.Equal(["first", "third"], document.FunctionNames);
    }

    /// <summary>
    ///     Validates that duplicate definitions are kept.
    /// </summary>
    [Fact]
    public void ScadDocument_ModuleAndFunctionNames_Duplicates_AreKept()
    {
        // Arrange / Act
        var document = ScadDocument.Parse("module a() {}\nmodule a() {}\nfunction f() = 1;\nfunction f() = 2;");

        // Assert
        Assert.Equal(["a", "a"], document.ModuleNames);
        Assert.Equal(["f", "f"], document.FunctionNames);
    }
}
