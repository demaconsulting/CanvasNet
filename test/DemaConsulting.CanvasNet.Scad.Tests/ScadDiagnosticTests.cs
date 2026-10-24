namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Unit tests for <see cref="ScadDiagnostic"/>.
/// </summary>
public class ScadDiagnosticTests
{
    /// <summary>
    ///     Validates the record members and value equality.
    /// </summary>
    [Fact]
    public void ScadDiagnostic_Properties_AreExposedWithValueEquality()
    {
        // Arrange
        var location = new ScadSourceLocation("a.scad", 3, 5);

        // Act
        var diagnostic = new ScadDiagnostic("Ignoring unknown variable 'x'", location);

        // Assert
        Assert.Equal("Ignoring unknown variable 'x'", diagnostic.Message);
        Assert.Equal(location, diagnostic.Location);
        Assert.Equal(diagnostic, new ScadDiagnostic("Ignoring unknown variable 'x'", location));
        Assert.NotEqual(diagnostic, new ScadDiagnostic("other", location));
    }

    /// <summary>
    ///     Validates the display form with a named and an unnamed location, and with an unknown one.
    /// </summary>
    [Fact]
    public void ScadDiagnostic_ToString_PrefixesLocation()
    {
        // Arrange / Act / Assert
        Assert.Equal("a.scad:3:5: boom", new ScadDiagnostic("boom", new ScadSourceLocation("a.scad", 3, 5)).ToString());
        Assert.Equal("3:5: boom", new ScadDiagnostic("boom", new ScadSourceLocation(null, 3, 5)).ToString());
        Assert.Equal("?: boom", new ScadDiagnostic("boom", default).ToString());
    }

    /// <summary>
    ///     Validates that the display form does not depend on the current culture.
    /// </summary>
    [Fact]
    public void ScadDiagnostic_ToString_IsCultureInvariant()
    {
        // Arrange
        var diagnostic = new ScadDiagnostic("m", new ScadSourceLocation(null, 1234567, 8));
        var original = System.Globalization.CultureInfo.CurrentCulture;

        // Act
        string text;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
            text = diagnostic.ToString();
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }

        // Assert
        Assert.Equal("1234567:8: m", text);
    }
}
