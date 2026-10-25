namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Unit tests for <see cref="ScadEvaluationException"/>.
/// </summary>
public class ScadEvaluationExceptionTests
{
    /// <summary>
    ///     Validates the base type.
    /// </summary>
    [Fact]
    public void ScadEvaluationException_Inheritance_DerivesFromInvalidOperationException()
    {
        // Arrange / Act
        var ex = new ScadEvaluationException("bad");

        // Assert
        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    /// <summary>
    ///     Validates that a known location is exposed and prefixed to the message.
    /// </summary>
    [Fact]
    public void ScadEvaluationException_Location_MessageFormat()
    {
        // Arrange
        var location = new ScadSourceLocation("x.scad", 4, 9);

        // Act
        var ex = new ScadEvaluationException("Assertion failed", location);

        // Assert
        Assert.Equal(location, ex.Location);
        Assert.Equal("x.scad:4:9: Assertion failed", ex.Message);
    }

    /// <summary>
    ///     Validates that an unknown location leaves the message unprefixed.
    /// </summary>
    [Fact]
    public void ScadEvaluationException_UnknownLocation_MessageHasNoPrefix()
    {
        // Arrange / Act
        var ex = new ScadEvaluationException("plain", default(ScadSourceLocation));

        // Assert
        Assert.Equal("plain", ex.Message);
        Assert.Equal(0, ex.Location.Line);
    }

    /// <summary>
    ///     Validates the standard exception constructors.
    /// </summary>
    [Fact]
    public void ScadEvaluationException_StandardConstructors_SetMessageAndInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var parameterless = new ScadEvaluationException();
        var withMessage = new ScadEvaluationException("message");
        var withInner = new ScadEvaluationException("outer", inner);

        // Assert
        Assert.NotNull(parameterless.Message);
        Assert.Equal("message", withMessage.Message);
        Assert.Equal("outer", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
        Assert.Equal(0, parameterless.Location.Line);
        Assert.Equal(0, withMessage.Location.Line);
        Assert.Equal(0, withInner.Location.Line);
    }
}
