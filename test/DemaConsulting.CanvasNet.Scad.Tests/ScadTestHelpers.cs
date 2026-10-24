namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Shared helpers for the Scad tests: fixture discovery and syntax-error assertions.
/// </summary>
internal static class ScadTestHelpers
{
    /// <summary>
    ///     Asserts that an action throws a <see cref="ScadSyntaxException"/> at an exact location.
    /// </summary>
    /// <param name="action">The action expected to fail with a syntax error.</param>
    /// <param name="line">The expected one-based line.</param>
    /// <param name="column">The expected one-based column.</param>
    /// <param name="messageFragment">An optional text the exception message must contain.</param>
    /// <param name="file">The expected file name in the location (null when unnamed).</param>
    /// <returns>The thrown exception, for further assertions.</returns>
    public static ScadSyntaxException AssertSyntaxError(
        Action action,
        int line,
        int column,
        string? messageFragment = null,
        string? file = null)
    {
        // Run the action and capture the typed syntax exception
        var ex = Assert.Throws<ScadSyntaxException>(action);

        // Verify the exact coordinates and file name reported for the first error
        Assert.Equal(new ScadSourceLocation(file, line, column), ex.Location);

        // Verify the message mentions the expected description and is prefixed with the location
        Assert.StartsWith(ex.Location.ToString() + ": ", ex.Message, StringComparison.Ordinal);
        if (messageFragment is not null)
        {
            Assert.Contains(messageFragment, ex.Message, StringComparison.Ordinal);
        }

        return ex;
    }
}

/// <summary>
///     Locates and reads the original-work <c>.scad</c> fixtures copied next to the test assembly.
/// </summary>
internal static class ScadFixtureHelpers
{
    /// <summary>The name prefix that marks a fixture that is expected to fail to parse.</summary>
    private const string InvalidPrefix = "invalid-";

    /// <summary>
    ///     Gets the directory that holds the fixtures in the test output folder.
    /// </summary>
    public static string Directory => Path.Combine(AppContext.BaseDirectory, "ScadFixtures");

    /// <summary>
    ///     Gets the full path of a fixture.
    /// </summary>
    /// <param name="name">The fixture file name.</param>
    /// <returns>The full path.</returns>
    public static string PathOf(string name) => Path.Combine(Directory, name);

    /// <summary>
    ///     Reads a fixture as text.
    /// </summary>
    /// <param name="name">The fixture file name.</param>
    /// <returns>The file text.</returns>
    public static string ReadAllText(string name) => File.ReadAllText(PathOf(name));

    /// <summary>
    ///     Enumerates the fixtures that must parse (every <c>.scad</c> not starting with <c>invalid-</c>).
    /// </summary>
    /// <returns>The full paths, ordered by name.</returns>
    public static IReadOnlyList<string> ValidFixtures() => Fixtures(valid: true);

    /// <summary>
    ///     Enumerates the fixtures that must fail to parse (every <c>invalid-*.scad</c>).
    /// </summary>
    /// <returns>The full paths, ordered by name.</returns>
    public static IReadOnlyList<string> InvalidFixtures() => Fixtures(valid: false);

    /// <summary>
    ///     Enumerates fixtures of one class.
    /// </summary>
    /// <param name="valid">Whether to list the valid (true) or the invalid (false) fixtures.</param>
    /// <returns>The full paths, ordered by name.</returns>
    private static IReadOnlyList<string> Fixtures(bool valid) =>
        System.IO.Directory.GetFiles(Directory, "*.scad")
            .Where(p => Path.GetFileName(p).StartsWith(InvalidPrefix, StringComparison.Ordinal) != valid)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
}
