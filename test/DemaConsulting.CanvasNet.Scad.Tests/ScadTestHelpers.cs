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
