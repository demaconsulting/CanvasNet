namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Unit tests for <see cref="ScadSyntaxException"/>.
/// </summary>
public class ScadSyntaxExceptionTests
{
    /// <summary>
    ///     Validates that the exception is an <see cref="FormatException"/>.
    /// </summary>
    [Fact]
    public void ScadSyntaxException_IsFormatException()
    {
        // Arrange / Act: create an exception
        var ex = new ScadSyntaxException("bad");

        // Assert: it can be caught as the BCL format exception
        Assert.IsAssignableFrom<FormatException>(ex);
    }

    /// <summary>
    ///     Validates that a known location is exposed and prefixed to the message.
    /// </summary>
    [Fact]
    public void ScadSyntaxException_Location_MessageFormat()
    {
        // Arrange: a location with a file name
        var location = new ScadSourceLocation("x.scad", 4, 9);

        // Act: build the exception
        var ex = new ScadSyntaxException("Unexpected token ';'", location);

        // Assert: location property and prefixed message
        Assert.Equal(location, ex.Location);
        Assert.Equal("x.scad:4:9: Unexpected token ';'", ex.Message);
    }

    /// <summary>
    ///     Validates that an unknown location leaves the message unprefixed.
    /// </summary>
    [Fact]
    public void ScadSyntaxException_UnknownLocation_MessageHasNoPrefix()
    {
        // Arrange / Act: an exception whose location is the default value
        var ex = new ScadSyntaxException("plain", default(ScadSourceLocation));

        // Assert: the message is the bare description
        Assert.Equal("plain", ex.Message);
        Assert.Equal(0, ex.Location.Line);
    }

    /// <summary>
    ///     Validates the standard exception constructors.
    /// </summary>
    [Fact]
    public void ScadSyntaxException_StandardConstructors_SetMessageAndInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var parameterless = new ScadSyntaxException();
        var withMessage = new ScadSyntaxException("message");
        var withInner = new ScadSyntaxException("outer", inner);

        // Assert: messages, inner exception and unknown locations
        Assert.NotNull(parameterless.Message);
        Assert.Equal("message", withMessage.Message);
        Assert.Equal("outer", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
        Assert.Equal(0, parameterless.Location.Line);
        Assert.Equal(0, withMessage.Location.Line);
        Assert.Equal(0, withInner.Location.Line);
    }
}
