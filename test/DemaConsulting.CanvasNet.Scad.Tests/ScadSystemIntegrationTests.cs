using System.Text;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     System-level integration tests for the <c>DemaConsulting.CanvasNet.Scad</c> package, exercising only its
///     public API end to end (one test method per top-level system requirement declared in
///     <c>docs/reqstream/canvas-net-scad.yaml</c> and its platform requirements). The fixtures are the
///     original-work <c>.scad</c> files copied to the test output folder.
/// </summary>
public class ScadSystemIntegrationTests
{
    /// <summary>
    ///     Validates that parsing the same fixture from a string, a stream and a file agrees on everything the
    ///     public document reports.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Open_ParseStringOpenStreamAndOpenPathAgree()
    {
        // Arrange / Act / Assert: for every valid fixture, the three entry points report the same facts
        foreach (var path in ScadFixtureHelpers.ValidFixtures())
        {
            var options = new ScadOpenOptions { SourceName = path };
            var text = File.ReadAllText(path);

            using var stream = File.OpenRead(path);
            var fromString = ScadDocument.Parse(text, options);
            var fromStream = ScadDocument.Open(stream, options);
            var fromPath = ScadDocument.Open(path);

            foreach (var other in new[] { fromStream, fromPath })
            {
                Assert.Equal(fromString.SourceName, other.SourceName);
                Assert.Equal(fromString.Directives, other.Directives);
                Assert.Equal(fromString.ModuleNames, other.ModuleNames);
                Assert.Equal(fromString.FunctionNames, other.FunctionNames);
            }
        }
    }

    /// <summary>
    ///     Validates that invalid arguments are rejected by every entry point.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Open_InvalidArgumentsThrow()
    {
        // Arrange: an unreadable stream
        var closed = new MemoryStream();
        closed.Dispose();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ScadDocument.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => ScadDocument.Open((string)null!));
        Assert.Throws<ArgumentNullException>(() => ScadDocument.Open((Stream)null!));
        Assert.Throws<ArgumentException>(() => ScadDocument.Open("  "));
        Assert.Throws<ArgumentException>(() => ScadDocument.Open(closed));
        Assert.Throws<FileNotFoundException>(() => ScadDocument.Open(ScadFixtureHelpers.PathOf("missing.scad")));
    }

    /// <summary>
    ///     Validates that open options are validated and immutable.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Options_AreValidatedAndImmutable()
    {
        // Arrange
        var libraries = new List<string> { "lib" };
        var options = new ScadOpenOptions { BaseDirectory = "base", LibraryPaths = libraries, SourceName = "s.scad" };

        // Act
        libraries.Add("later");

        // Assert: invalid values are rejected, collections copied, and the name flows into diagnostics
        Assert.Throws<ArgumentException>(() => new ScadOpenOptions { BaseDirectory = " " });
        Assert.Throws<ArgumentException>(() => new ScadOpenOptions { SourceName = "" });
        Assert.Throws<ArgumentException>(() => new ScadOpenOptions { LibraryPaths = [" "] });
        Assert.Equal(["lib"], options.LibraryPaths);
        var ex = Assert.Throws<ScadSyntaxException>(() => ScadDocument.Parse("x = ;", options));
        Assert.Equal("s.scad", ex.Location.File);
    }

    /// <summary>
    ///     Validates that every authored valid fixture parses; this is also the anchor of the platform requirements.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Syntax_AllAuthoredFixturesParse()
    {
        // Arrange
        var fixtures = ScadFixtureHelpers.ValidFixtures();

        // Act: open every valid fixture from disk
        var documents = fixtures.Select(path => ScadDocument.Open(path)).ToList();

        // Assert: ten valid fixtures, each named by its path
        Assert.Equal(10, documents.Count);
        Assert.Equal(fixtures, documents.Select(d => d.SourceName));
    }

    /// <summary>
    ///     Validates that the parametric bracket fixture reports its file-scope definitions.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Syntax_ParametricPartFixtureReportsDefinitions()
    {
        // Arrange / Act
        var document = ScadDocument.Open(ScadFixtureHelpers.PathOf("parametric-bracket.scad"));

        // Assert: definitions in source order; nothing nested or imported
        Assert.Equal(["bracket_plate", "bracket_rib", "mounting_holes", "shelf_bracket"], document.ModuleNames);
        Assert.Equal(["rib_height", "hole_pitch", "hole_positions"], document.FunctionNames);
        Assert.Empty(document.Directives);
    }

    /// <summary>
    ///     Validates that each malformed fixture reports its exact file, line and column.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_SyntaxErrors_MalformedFixturesReportLocation()
    {
        // Arrange: expected (line, column, message fragment) per invalid fixture
        var expected = new Dictionary<string, (int Line, int Column, string Fragment)>
        {
            ["invalid-unterminated-block-comment.scad"] = (4, 1, "Unterminated block comment"),
            ["invalid-missing-semicolon.scad"] = (4, 1, "Expected ';' but found 'height'"),
            ["invalid-unbalanced-brace.scad"] = (5, 1, "Unexpected end of input; expected '}'"),
        };

        // Act / Assert: every invalid fixture is covered and fails exactly where documented
        var invalid = ScadFixtureHelpers.InvalidFixtures();
        Assert.Equal(expected.Count, invalid.Count);
        foreach (var path in invalid)
        {
            var (line, column, fragment) = expected[Path.GetFileName(path)];
            ScadTestHelpers.AssertSyntaxError(() => ScadDocument.Open(path), line, column, fragment, path);
        }
    }

    /// <summary>
    ///     Validates that only the first error is reported when a source has several.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_SyntaxErrors_FirstErrorIsReported()
    {
        // Arrange: three independent grammar errors on three lines
        const string Source = "a = ;\nb = ;\nc = ;";

        // Act
        var ex = Assert.Throws<ScadSyntaxException>(() => ScadDocument.Parse(Source));

        // Assert: the first one, and the exception is also a standard format exception
        Assert.Equal(new ScadSourceLocation(null, 1, 5), ex.Location);
        Assert.IsAssignableFrom<FormatException>(ex);
    }

    /// <summary>
    ///     Validates that include and use directives are recorded without touching the file system or a resolver.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Directives_IncludeAndUseRecordedWithoutFileAccess()
    {
        // Arrange: a resolver that fails the test if called and a base directory that does not exist
        var options = new ScadOpenOptions
        {
            BaseDirectory = Path.Combine(Path.GetTempPath(), "scad-missing-" + Guid.NewGuid().ToString("N")),
            LibraryPaths = ["no-such-library"],
            FileResolver = name => throw new InvalidOperationException($"Unexpected resolution of '{name}'."),
        };
        var path = ScadFixtureHelpers.PathOf("include-and-use.scad");

        // Act
        var document = ScadDocument.Parse(File.ReadAllText(path), options);

        // Assert: four directives in order, none of which exist on disk
        Assert.Equal(
            [
                (ScadDirectiveKind.Include, "parts/frames.scad", 3),
                (ScadDirectiveKind.Use, "lib/gears.scad", 4),
                (ScadDirectiveKind.Include, "./relative/../nested/deep/path/box.scad", 5),
                (ScadDirectiveKind.Use, "fasteners.scad", 6),
            ],
            document.Directives.Select(d => (d.Kind, d.Path, d.Location.Line)));
    }

    /// <summary>
    ///     Validates that excessive nesting fails closed with a typed exception rather than crashing the process.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Limits_DeepNestingFailsClosed()
    {
        // Arrange: hostile nesting in three shapes, far beyond the supported depth
        var shapes = new[]
        {
            "x = " + string.Concat(Enumerable.Repeat("(", 100000)) + "1;",
            string.Concat(Enumerable.Repeat("{", 100000)),
            "x = 1" + string.Concat(Enumerable.Repeat("+1", 100000)) + ";",
        };

        // Act / Assert
        foreach (var source in shapes)
        {
            var ex = Assert.Throws<ScadSyntaxException>(() => ScadDocument.Parse(source));
            Assert.Contains("Nesting too deep", ex.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Validates that an oversized source is rejected, from a string and from a stream, while a source of
    ///     exactly the limit is accepted.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Limits_OversizedSourceIsRejected()
    {
        // Arrange
        const int Limit = 4_194_304;
        var atLimit = "x = 1;" + new string(' ', Limit - 6);
        var overLimit = atLimit + " ";

        // Act
        var accepted = ScadDocument.Parse(atLimit);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(overLimit));

        // Assert: the oversize failures are plain InvalidDataException, not syntax errors
        Assert.NotNull(accepted);
        Assert.IsType<InvalidDataException>(Assert.ThrowsAny<Exception>(() => ScadDocument.Parse(overLimit)));
        Assert.IsType<InvalidDataException>(Assert.ThrowsAny<Exception>(() => ScadDocument.Open(stream)));
    }
}
