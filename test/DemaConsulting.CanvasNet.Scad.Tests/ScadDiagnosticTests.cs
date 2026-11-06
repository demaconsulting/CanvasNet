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

    /// <summary>
    ///     Validates that a diagnostic built from a message and a location carries the default warning code.
    /// </summary>
    [Fact]
    public void ScadDiagnostic_Code_DefaultsToScadWarning()
    {
        // Arrange / Act
        var diagnostic = new ScadDiagnostic("boom", default);

        // Assert
        Assert.Equal("scad-warning", diagnostic.Code);
        Assert.Equal(ScadDiagnosticCodes.Warning, diagnostic.Code);
    }

    /// <summary>
    ///     Validates that the code takes part in value equality and does not change the display form.
    /// </summary>
    [Fact]
    public void ScadDiagnostic_Code_IsPartOfEqualityAndNotOfToString()
    {
        // Arrange
        var location = new ScadSourceLocation("a.scad", 1, 2);

        // Act
        var plain = new ScadDiagnostic("m", location);
        var coded = new ScadDiagnostic("m", location) { Code = ScadDiagnosticCodes.UnknownVariable };

        // Assert
        Assert.NotEqual(plain, coded);
        Assert.Equal(coded, new ScadDiagnostic("m", location) { Code = ScadDiagnosticCodes.UnknownVariable });
        Assert.Equal(plain.ToString(), coded.ToString());
    }

    /// <summary>
    ///     Validates that every published code is a unique, lower-case, kebab-case identifier that starts with
    ///     <c>scad-</c>, so a code can be compared and logged without surprises.
    /// </summary>
    [Fact]
    public void ScadDiagnosticCodes_Values_AreUniqueKebabCase()
    {
        // Arrange: every public constant of the codes class
        var values = typeof(ScadDiagnosticCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        // Act / Assert: kebab-case, prefixed, and distinct
        Assert.NotEmpty(values);
        Assert.All(values, value => Assert.Matches("^scad(-[a-z]+)+$", value));
        Assert.Equal(values.Count, values.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    ///     Validates the values that are part of the documented contract.
    /// </summary>
    [Fact]
    public void ScadDiagnosticCodes_ContractValues_AreStable()
    {
        // Arrange / Act / Assert
        Assert.Equal("scad-warning", ScadDiagnosticCodes.Warning);
        Assert.Equal("scad-anchor-invalid", ScadDiagnosticCodes.AnchorInvalid);
        Assert.Equal("scad-include-not-found", ScadDiagnosticCodes.IncludeNotFound);
        Assert.Equal("scad-unknown-variable", ScadDiagnosticCodes.UnknownVariable);
        Assert.Equal("scad-unknown-function", ScadDiagnosticCodes.UnknownFunction);
        Assert.Equal("scad-unknown-module", ScadDiagnosticCodes.UnknownModule);
        Assert.Equal("scad-diagnostics-suppressed", ScadDiagnosticCodes.DiagnosticsSuppressed);
        Assert.Equal("scad-syntax-error", ScadDiagnosticCodes.SyntaxError);
        Assert.Equal("scad-evaluation-error", ScadDiagnosticCodes.EvaluationError);
    }
}
