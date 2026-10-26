// cspell:ignore canvasnet rmse
using System.Globalization;
using System.Text.Json;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Unit tests for the ResultWriter class.
/// </summary>
[Collection("Sequential")]
public class ResultWriterTests
{
    /// <summary>
    ///     Test that the schema version is 1.
    /// </summary>
    [Fact]
    public void ResultWriter_SchemaVersion_IsOne()
    {
        // Arrange / Act / Assert: the released schema version
        Assert.Equal(1, ResultWriter.SchemaVersion);
    }

    /// <summary>
    ///     Test that the success document starts with the envelope keys and then the command's fields, byte for byte.
    /// </summary>
    [Fact]
    public void ResultWriter_Success_WithFields_WritesEnvelopeThenFields()
    {
        // Arrange / Act: write a success document with fields of several types
        var json = ResultWriter.Success("render", writer =>
        {
            writer.WriteString("output", "out.png");
            writer.WriteNumber("width", 816);
            writer.WriteNumber("scale", 1.5);
            writer.WriteBoolean("flattened", true);
            writer.WriteStartArray("operations");
            writer.WriteStringValue("crop");
            writer.WriteEndArray();
        });

        // Assert: exactly this single-line document
        Assert.Equal(
            "{\"schemaVersion\":1,\"ok\":true,\"command\":\"render\",\"output\":\"out.png\",\"width\":816,\"scale\":1.5,"
            + "\"flattened\":true,\"operations\":[\"crop\"]}",
            json);
    }

    /// <summary>
    ///     Test that a success document with no fields is just the envelope.
    /// </summary>
    [Fact]
    public void ResultWriter_Success_NoFields_WritesEnvelopeOnly()
    {
        // Arrange / Act: write a success document with an empty field writer
        var json = ResultWriter.Success("info", _ => { });

        // Assert: only the envelope
        Assert.Equal("{\"schemaVersion\":1,\"ok\":true,\"command\":\"info\"}", json);
    }

    /// <summary>
    ///     Test that the failure document carries the code, message, and hint, byte for byte.
    /// </summary>
    [Fact]
    public void ResultWriter_Failure_WithHint_WritesErrorObject()
    {
        // Arrange / Act: write a failure with a hint
        var json = ResultWriter.Failure("render", "input-not-found", "File 'x.png' was not found.", "Check the path.");

        // Assert: the error object follows the envelope, with no exit code
        Assert.Equal(
            "{\"schemaVersion\":1,\"ok\":false,\"command\":\"render\","
            + "\"error\":{\"code\":\"input-not-found\",\"message\":\"File 'x.png' was not found.\",\"hint\":\"Check the path.\"}}",
            json);
        Assert.DoesNotContain("exitCode", json);
    }

    /// <summary>
    ///     Test that a failure without a command writes null, and one without a hint omits the hint.
    /// </summary>
    [Fact]
    public void ResultWriter_Failure_NoCommandNoHint_WritesNullCommandAndOmitsHint()
    {
        // Arrange / Act: write a failure with neither command nor hint
        var json = ResultWriter.Failure(null, "usage-error", "Unsupported argument '--x'", null);

        // Assert: command is null and there is no hint key
        Assert.Equal(
            "{\"schemaVersion\":1,\"ok\":false,\"command\":null,"
            + "\"error\":{\"code\":\"usage-error\",\"message\":\"Unsupported argument '--x'\"}}",
            json);
    }

    /// <summary>
    ///     Test that a failure document is built from an exception according to its type.
    /// </summary>
    [Fact]
    public void ResultWriter_Failure_FromException_MapsExceptionToCode()
    {
        // Arrange / Act: a ToolException, an ArgumentException, and another InvalidOperationException
        var tool = ResultWriter.Failure("render", new ToolException(ErrorCodes.OutputExists, "Exists.", "Remove it."));
        var argument = ResultWriter.Failure("render", new ArgumentException("Bad argument."));
        var operation = ResultWriter.Failure(null, new InvalidOperationException("Bad state."));

        // Assert: the tool exception keeps its code and hint; the others map to usage-error and operation-failed
        Assert.Equal(
            "{\"schemaVersion\":1,\"ok\":false,\"command\":\"render\","
            + "\"error\":{\"code\":\"output-exists\",\"message\":\"Exists.\",\"hint\":\"Remove it.\"}}",
            tool);
        Assert.Equal(
            "{\"schemaVersion\":1,\"ok\":false,\"command\":\"render\","
            + "\"error\":{\"code\":\"usage-error\",\"message\":\"Bad argument.\"}}",
            argument);
        Assert.Equal(
            "{\"schemaVersion\":1,\"ok\":false,\"command\":null,"
            + "\"error\":{\"code\":\"operation-failed\",\"message\":\"Bad state.\"}}",
            operation);
    }

    /// <summary>
    ///     Test that a failure document built from a ToolException with result fields appends them after the error object.
    /// </summary>
    [Fact]
    public void ResultWriter_Failure_ExceptionWithResultFields_AppendsFieldsAfterError()
    {
        // Arrange: a tool exception that carries two result fields
        var exception = new ToolException(ErrorCodes.ThresholdFailed, "rmse 5 is above the limit 1", "Raise the limit.")
        {
            ResultFields = writer =>
            {
                writer.WriteNumber("size", 3);
                writer.WriteStartObject("metrics");
                writer.WriteNumber("rmse", 5.5);
                writer.WriteEndObject();
            }
        };

        // Act
        var json = ResultWriter.Failure("compare", exception);

        // Assert: the envelope and error first, then the fields, byte for byte, as one parseable line
        Assert.Equal(
            "{\"schemaVersion\":1,\"ok\":false,\"command\":\"compare\","
            + "\"error\":{\"code\":\"threshold-failed\",\"message\":\"rmse 5 is above the limit 1\",\"hint\":\"Raise the limit.\"},"
            + "\"size\":3,\"metrics\":{\"rmse\":5.5}}",
            json);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty("ok").GetBoolean());
    }

    /// <summary>
    ///     Test that a ToolException without result fields gives the plain failure document.
    /// </summary>
    [Fact]
    public void ResultWriter_Failure_ExceptionWithoutResultFields_IsUnchanged()
    {
        // Arrange: an exception with no result fields, and the same failure written without an exception
        var exception = new ToolException(ErrorCodes.SizeMismatch, "The sizes differ.", "Use --resize-to.");

        // Act
        var fromException = ResultWriter.Failure("compare", exception);
        var plain = ResultWriter.Failure("compare", ErrorCodes.SizeMismatch, "The sizes differ.", "Use --resize-to.");

        // Assert: identical, ending with the error object
        Assert.Null(exception.ResultFields);
        Assert.Equal(plain, fromException);
        Assert.EndsWith("\"hint\":\"Use --resize-to.\"}}", fromException);
    }

    /// <summary>
    ///     Test that numbers are written with the invariant culture whatever the current culture is.
    /// </summary>
    [Fact]
    public void ResultWriter_Success_GermanCulture_WritesInvariantNumbers()
    {
        // Arrange: a culture whose decimal separator is a comma
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            // Act: write a fractional number and a large integer
            var json = ResultWriter.Success("x", writer =>
            {
                writer.WriteNumber("a", 1234.5);
                writer.WriteNumber("b", 1234567L);
            });

            // Assert: a decimal point and no thousands separators
            Assert.Equal("{\"schemaVersion\":1,\"ok\":true,\"command\":\"x\",\"a\":1234.5,\"b\":1234567}", json);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    ///     Test that special and non-ASCII characters are escaped so that the document is ASCII and parses back.
    /// </summary>
    [Fact]
    public void ResultWriter_Failure_SpecialCharacters_AreEscapedAndRoundTrip()
    {
        // Arrange: a message with quotes, a backslash, a newline, a tab, HTML characters, an accent, and an emoji (a surrogate pair)
        const string Message = "He said \"hi\" \\ line1\nline2\t<b>&'+ caf\u00e9 \U0001F600";

        // Act: write the document
        var json = ResultWriter.Failure("c", "operation-failed", Message, null);

        // Assert: ASCII only, a single line, and the message survives a round trip through a JSON parser
        Assert.All(json, c => Assert.True(c < 0x80, $"Non-ASCII character U+{(int)c:X4}"));
        Assert.DoesNotContain('\n', json);
        Assert.Contains("caf\\u00e9", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(Message, document.RootElement.GetProperty("error").GetProperty("message").GetString());
    }

    /// <summary>
    ///     Test that the same input always gives the same document.
    /// </summary>
    [Fact]
    public void ResultWriter_Success_SameInput_ProducesIdenticalOutput()
    {
        // Arrange / Act: write the same document twice
        var first = ResultWriter.Success("c", writer => writer.WriteNumber("n", 0.1));
        var second = ResultWriter.Success("c", writer => writer.WriteNumber("n", 0.1));

        // Assert: byte-identical
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void ResultWriter_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: each null argument throws
        Assert.Throws<ArgumentNullException>(() => ResultWriter.Success(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => ResultWriter.Success("c", null!));
        Assert.Throws<ArgumentNullException>(() => ResultWriter.Failure("c", null!, "m", null));
        Assert.Throws<ArgumentNullException>(() => ResultWriter.Failure("c", "x", null!, null));
        Assert.Throws<ArgumentNullException>(() => ResultWriter.Failure("c", null!));
    }
}
