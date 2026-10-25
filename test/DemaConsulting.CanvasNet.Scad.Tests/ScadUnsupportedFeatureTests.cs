namespace DemaConsulting.CanvasNet.Scad.Tests;

// cspell:ignore textmetrics

/// <summary>
///     Unit tests for <see cref="ScadUnsupportedFeature"/> and <see cref="ScadUnsupportedFeatureException"/>.
/// </summary>
public class ScadUnsupportedFeatureTests
{
    /// <summary>
    ///     Validates the record members, value equality and display form.
    /// </summary>
    [Fact]
    public void ScadUnsupportedFeature_Record_ExposesMembersAndFormats()
    {
        // Arrange
        var location = new ScadSourceLocation("m.scad", 2, 7);

        // Act
        var feature = new ScadUnsupportedFeature("scad-function-textmetrics", "textmetrics() is not supported", location);

        // Assert
        Assert.Equal("scad-function-textmetrics", feature.Feature);
        Assert.Equal("textmetrics() is not supported", feature.Message);
        Assert.Equal(location, feature.Location);
        Assert.Equal(feature, new ScadUnsupportedFeature("scad-function-textmetrics", "textmetrics() is not supported", location));
        Assert.Equal("m.scad:2:7: textmetrics() is not supported [scad-function-textmetrics]", feature.ToString());
    }

    /// <summary>
    ///     Validates the exception for a known location: feature, location and prefixed message.
    /// </summary>
    [Fact]
    public void ScadUnsupportedFeatureException_Location_PrefixesMessage()
    {
        // Arrange
        var location = new ScadSourceLocation("m.scad", 2, 7);

        // Act
        var ex = new ScadUnsupportedFeatureException("scad-search-form", "search() form is not supported", location);

        // Assert
        Assert.Equal("scad-search-form", ex.Feature);
        Assert.Equal(location, ex.Location);
        Assert.Equal("m.scad:2:7: search() form is not supported", ex.Message);
        Assert.IsAssignableFrom<IOException>(ex);
    }

    /// <summary>
    ///     Validates that an unknown location leaves the message unprefixed.
    /// </summary>
    [Fact]
    public void ScadUnsupportedFeatureException_UnknownLocation_MessageHasNoPrefix()
    {
        // Arrange / Act
        var ex = new ScadUnsupportedFeatureException("f", "plain", default);

        // Assert
        Assert.Equal("plain", ex.Message);
        Assert.Equal(0, ex.Location.Line);
    }

    /// <summary>
    ///     Validates the standard exception constructors.
    /// </summary>
    [Fact]
    public void ScadUnsupportedFeatureException_StandardConstructors_SetMessageAndInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var parameterless = new ScadUnsupportedFeatureException();
        var withMessage = new ScadUnsupportedFeatureException("message");
        var withInner = new ScadUnsupportedFeatureException("outer", inner);

        // Assert
        Assert.NotNull(parameterless.Message);
        Assert.Equal("message", withMessage.Message);
        Assert.Equal("outer", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
        Assert.Empty(parameterless.Feature);
        Assert.Empty(withMessage.Feature);
        Assert.Empty(withInner.Feature);
        Assert.Equal(0, withInner.Location.Line);
    }
}
