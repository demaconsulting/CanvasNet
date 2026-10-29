namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Unit tests for <see cref="VegaLiteLimits"/>.
/// </summary>
public class VegaLiteLimitsTests
{
    /// <summary>
    ///     Validates the documented default values.
    /// </summary>
    [Fact]
    public void VegaLiteLimits_Default_HasDocumentedValues()
    {
        // Arrange / Act
        var limits = VegaLiteLimits.Default;

        // Assert
        Assert.Equal(4_194_304, limits.MaxSourceLength);
        Assert.Equal(64, limits.MaxJsonDepth);
        Assert.Equal(64, limits.MaxDataFiles);
        Assert.Equal(64L * 1024 * 1024, limits.MaxDataBytes);
        Assert.Equal(1_000_000L, limits.MaxDataRows);
        Assert.Equal(1_024, limits.MaxFieldCount);
        Assert.Equal(1_000, limits.MaxViews);
        Assert.Equal(16, limits.MaxCompositionDepth);
        Assert.Equal(2_000_000L, limits.MaxSceneItems);
        Assert.Equal(100_000, limits.MaxScaleDomainSize);
        Assert.Equal(1_000, limits.MaxTicks);
        Assert.Equal(4_096, limits.MaxExpressionLength);
        Assert.Equal(64, limits.MaxExpressionDepth);
        Assert.Equal(50_000_000L, limits.MaxEvaluationSteps);
        Assert.Equal(1_048_576, limits.MaxStringLength);
        Assert.Equal(1_000, limits.MaxDiagnostics);
    }

    /// <summary>
    ///     Validates that a new instance equals the defaults and that the default is shared.
    /// </summary>
    [Fact]
    public void VegaLiteLimits_Default_IsSharedInstance()
    {
        // Arrange / Act / Assert
        Assert.Same(VegaLiteLimits.Default, VegaLiteLimits.Default);
        Assert.Equal(VegaLiteLimits.Default.MaxJsonDepth, new VegaLiteLimits().MaxJsonDepth);
    }

    /// <summary>
    ///     Validates that valid values are stored.
    /// </summary>
    [Fact]
    public void VegaLiteLimits_ValidValues_AreStored()
    {
        // Arrange / Act
        var limits = new VegaLiteLimits
        {
            MaxSourceLength = 10,
            MaxJsonDepth = 256,
            MaxCompositionDepth = 64,
            MaxExpressionDepth = 256,
            MaxDataBytes = 1,
            MaxDiagnostics = 5,
        };

        // Assert
        Assert.Equal(10, limits.MaxSourceLength);
        Assert.Equal(256, limits.MaxJsonDepth);
        Assert.Equal(64, limits.MaxCompositionDepth);
        Assert.Equal(256, limits.MaxExpressionDepth);
        Assert.Equal(1, limits.MaxDataBytes);
        Assert.Equal(5, limits.MaxDiagnostics);
    }

    /// <summary>
    ///     Validates that every limit rejects values below its minimum, naming the property.
    /// </summary>
    /// <param name="property">The property.</param>
    [Theory]
    [InlineData("MaxSourceLength")]
    [InlineData("MaxJsonDepth")]
    [InlineData("MaxDataFiles")]
    [InlineData("MaxDataBytes")]
    [InlineData("MaxDataRows")]
    [InlineData("MaxFieldCount")]
    [InlineData("MaxViews")]
    [InlineData("MaxCompositionDepth")]
    [InlineData("MaxSceneItems")]
    [InlineData("MaxScaleDomainSize")]
    [InlineData("MaxTicks")]
    [InlineData("MaxExpressionLength")]
    [InlineData("MaxExpressionDepth")]
    [InlineData("MaxEvaluationSteps")]
    [InlineData("MaxStringLength")]
    [InlineData("MaxDiagnostics")]
    public void VegaLiteLimits_ZeroOrNegative_Throws(string property)
    {
        // Arrange / Act
        var zero = Assert.Throws<ArgumentOutOfRangeException>(() => Create(property, 0));
        var negative = Assert.Throws<ArgumentOutOfRangeException>(() => Create(property, -5));

        // Assert
        Assert.Equal(property, zero.ParamName);
        Assert.Equal(property, negative.ParamName);
    }

    /// <summary>
    ///     Validates the ceilings of the depth limits.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="ceiling">The largest accepted value.</param>
    [Theory]
    [InlineData("MaxJsonDepth", 256)]
    [InlineData("MaxCompositionDepth", 64)]
    [InlineData("MaxExpressionDepth", 256)]
    public void VegaLiteLimits_AboveCeiling_Throws(string property, int ceiling)
    {
        // Arrange / Act
        _ = Create(property, ceiling);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Create(property, ceiling + 1));

        // Assert
        Assert.Equal(property, ex.ParamName);
    }

    /// <summary>
    ///     Creates a limits object with one property set.
    /// </summary>
    /// <param name="property">The property name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The limits.</returns>
    private static VegaLiteLimits Create(string property, int value) => property switch
    {
        "MaxSourceLength" => new VegaLiteLimits { MaxSourceLength = value },
        "MaxJsonDepth" => new VegaLiteLimits { MaxJsonDepth = value },
        "MaxDataFiles" => new VegaLiteLimits { MaxDataFiles = value },
        "MaxDataBytes" => new VegaLiteLimits { MaxDataBytes = value },
        "MaxDataRows" => new VegaLiteLimits { MaxDataRows = value },
        "MaxFieldCount" => new VegaLiteLimits { MaxFieldCount = value },
        "MaxViews" => new VegaLiteLimits { MaxViews = value },
        "MaxCompositionDepth" => new VegaLiteLimits { MaxCompositionDepth = value },
        "MaxSceneItems" => new VegaLiteLimits { MaxSceneItems = value },
        "MaxScaleDomainSize" => new VegaLiteLimits { MaxScaleDomainSize = value },
        "MaxTicks" => new VegaLiteLimits { MaxTicks = value },
        "MaxExpressionLength" => new VegaLiteLimits { MaxExpressionLength = value },
        "MaxExpressionDepth" => new VegaLiteLimits { MaxExpressionDepth = value },
        "MaxEvaluationSteps" => new VegaLiteLimits { MaxEvaluationSteps = value },
        "MaxStringLength" => new VegaLiteLimits { MaxStringLength = value },
        "MaxDiagnostics" => new VegaLiteLimits { MaxDiagnostics = value },
        _ => throw new ArgumentException("Unknown property " + property, nameof(property)),
    };
}
