// cspell:ignore canvasnet
namespace DemaConsulting.CanvasNet.Viewer.Tests;

/// <summary>
///     Unit tests for the ViewerException unit and the ErrorCodes contract: the stable codes that callers branch on.
/// </summary>
public class ViewerExceptionTests
{
    /// <summary>
    ///     Test that the exception carries the code, the message, the hint, and the cause it was created with.
    /// </summary>
    [Fact]
    public void ViewerException_Constructor_CarriesCodeMessageHintAndCause()
    {
        // Arrange
        var cause = new IOException("disk");

        // Act
        var exception = new ViewerException(ErrorCodes.InputUnreadable, "cannot be read", "Check the path.", cause);

        // Assert: an expected failure, so it is handled like any other InvalidOperationException
        Assert.IsAssignableFrom<InvalidOperationException>(exception);
        Assert.Equal("input-unreadable", exception.Code);
        Assert.Equal("cannot be read", exception.Message);
        Assert.Equal("Check the path.", exception.Hint);
        Assert.Same(cause, exception.InnerException);
    }

    /// <summary>
    ///     Test that the hint and the cause are optional.
    /// </summary>
    [Fact]
    public void ViewerException_Constructor_HintAndCauseAreOptional()
    {
        // Act
        var exception = new ViewerException(ErrorCodes.UsageError, "bad");

        // Assert
        Assert.Null(exception.Hint);
        Assert.Null(exception.InnerException);
    }

    /// <summary>
    ///     Test that the error code strings never change: scripts and agents branch on them.
    /// </summary>
    [Fact]
    public void ErrorCodes_Constants_AreTheDocumentedStableStrings()
    {
        // Assert: every code and its documented spelling
        Assert.Equal("usage-error", ErrorCodes.UsageError);
        Assert.Equal("operation-failed", ErrorCodes.OperationFailed);
        Assert.Equal("input-not-found", ErrorCodes.InputNotFound);
        Assert.Equal("input-unreadable", ErrorCodes.InputUnreadable);
        Assert.Equal("unsupported-format", ErrorCodes.UnsupportedFormat);
        Assert.Equal("corrupt-input", ErrorCodes.CorruptInput);
        Assert.Equal("unsupported-feature", ErrorCodes.UnsupportedFeature);
        Assert.Equal("page-out-of-range", ErrorCodes.PageOutOfRange);
        Assert.Equal("image-fragment-invalid", ErrorCodes.ImageFragmentInvalid);
        Assert.Equal("size-limit", ErrorCodes.SizeLimit);
        Assert.Equal("same-input-output", ErrorCodes.SameInputOutput);
        Assert.Equal("output-exists", ErrorCodes.OutputExists);
        Assert.Equal("output-write-failed", ErrorCodes.OutputWriteFailed);
        Assert.Equal("scad-error", ErrorCodes.ScadError);
        Assert.Equal("vega-lite-error", ErrorCodes.VegaLiteError);
        Assert.Equal("budget-exceeded", ErrorCodes.BudgetExceeded);
        Assert.Equal("size-mismatch", ErrorCodes.SizeMismatch);
        Assert.Equal("threshold-failed", ErrorCodes.ThresholdFailed);
    }
}
