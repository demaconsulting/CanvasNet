namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Unit tests for <see cref="ScadLimits"/>.
/// </summary>
public class ScadLimitsTests
{
    /// <summary>
    ///     Validates the documented default values.
    /// </summary>
    [Fact]
    public void ScadLimits_Default_HasDocumentedValues()
    {
        // Arrange / Act
        var limits = ScadLimits.Default;

        // Assert
        Assert.Equal(5_000, limits.MaxRecursionDepth);
        Assert.Equal(50_000_000, limits.MaxEvaluationSteps);
        Assert.Equal(10_000_000, limits.MaxLoopIterations);
        Assert.Equal(1_000_000, limits.MaxListLength);
        Assert.Equal(1_048_576, limits.MaxStringLength);
        Assert.Equal(16, limits.MaxIncludeDepth);
        Assert.Equal(1_000, limits.MaxIncludedFiles);
        Assert.Equal(1_000_000, limits.MaxInstances);
        Assert.Equal(4_194_304, limits.MaxOutputLength);
        Assert.Equal(1_000, limits.MaxDiagnostics);
        Assert.Equal(5_000_000, limits.MaxTriangles);
        Assert.Same(ScadLimits.Default, ScadLimits.Default);
    }

    /// <summary>
    ///     Validates that the triangle limit accepts any positive value and rejects less than one with the property
    ///     name.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(long.MaxValue, true)]
    public void ScadLimits_MaxTriangles_MustBeAtLeastOne(long value, bool valid)
    {
        // Arrange / Act
        var act = () => new ScadLimits { MaxTriangles = value };

        // Assert
        if (valid)
        {
            Assert.Equal(value, act().MaxTriangles);
        }
        else
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(act);
            Assert.Equal(nameof(ScadLimits.MaxTriangles), ex.ParamName);
        }
    }

    /// <summary>
    ///     Validates that a new instance has the defaults and that every property accepts a valid value.
    /// </summary>
    [Fact]
    public void ScadLimits_Initialization_AcceptsValidValues()
    {
        // Arrange / Act
        var limits = new ScadLimits
        {
            MaxRecursionDepth = 16,
            MaxEvaluationSteps = 1,
            MaxLoopIterations = long.MaxValue,
            MaxListLength = 1,
            MaxStringLength = int.MaxValue,
            MaxIncludeDepth = 64,
            MaxIncludedFiles = 7,
            MaxInstances = 8,
            MaxOutputLength = 9,
            MaxDiagnostics = 10,
        };

        // Assert
        Assert.Equal(16, limits.MaxRecursionDepth);
        Assert.Equal(1, limits.MaxEvaluationSteps);
        Assert.Equal(long.MaxValue, limits.MaxLoopIterations);
        Assert.Equal(1, limits.MaxListLength);
        Assert.Equal(int.MaxValue, limits.MaxStringLength);
        Assert.Equal(64, limits.MaxIncludeDepth);
        Assert.Equal(7, limits.MaxIncludedFiles);
        Assert.Equal(8, limits.MaxInstances);
        Assert.Equal(9, limits.MaxOutputLength);
        Assert.Equal(10, limits.MaxDiagnostics);
        Assert.Equal(5_000, new ScadLimits().MaxRecursionDepth);
    }

    /// <summary>
    ///     Validates that the maximum recursion depth range is 16 to 16,384.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData(15, false)]
    [InlineData(16, true)]
    [InlineData(16_384, true)]
    [InlineData(16_385, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void ScadLimits_MaxRecursionDepth_IsRangeChecked(int value, bool valid)
    {
        // Arrange / Act
        var act = () => new ScadLimits { MaxRecursionDepth = value };

        // Assert
        if (valid)
        {
            Assert.Equal(value, act().MaxRecursionDepth);
        }
        else
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(act);
            Assert.Equal(nameof(ScadLimits.MaxRecursionDepth), ex.ParamName);
        }
    }

    /// <summary>
    ///     Validates that the include depth range is 1 to 64.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void ScadLimits_MaxIncludeDepth_IsRangeChecked(int value, bool valid)
    {
        // Arrange / Act
        var act = () => new ScadLimits { MaxIncludeDepth = value };

        // Assert
        if (valid)
        {
            Assert.Equal(value, act().MaxIncludeDepth);
        }
        else
        {
            Assert.Throws<ArgumentOutOfRangeException>(act);
        }
    }

    /// <summary>
    ///     Validates that every positive-only limit rejects zero and negative values with its own parameter name.
    /// </summary>
    [Fact]
    public void ScadLimits_PositiveLimits_RejectZeroAndNegative()
    {
        // Arrange: one initializer per positive-only property
        var cases = new (string Name, Func<int, ScadLimits> Create)[]
        {
            (nameof(ScadLimits.MaxEvaluationSteps), v => new ScadLimits { MaxEvaluationSteps = v }),
            (nameof(ScadLimits.MaxLoopIterations), v => new ScadLimits { MaxLoopIterations = v }),
            (nameof(ScadLimits.MaxListLength), v => new ScadLimits { MaxListLength = v }),
            (nameof(ScadLimits.MaxStringLength), v => new ScadLimits { MaxStringLength = v }),
            (nameof(ScadLimits.MaxIncludedFiles), v => new ScadLimits { MaxIncludedFiles = v }),
            (nameof(ScadLimits.MaxInstances), v => new ScadLimits { MaxInstances = v }),
            (nameof(ScadLimits.MaxOutputLength), v => new ScadLimits { MaxOutputLength = v }),
            (nameof(ScadLimits.MaxDiagnostics), v => new ScadLimits { MaxDiagnostics = v }),
        };

        // Act / Assert
        foreach (var (name, create) in cases)
        {
            var zero = Assert.Throws<ArgumentOutOfRangeException>(() => create(0));
            var negative = Assert.Throws<ArgumentOutOfRangeException>(() => create(-5));
            Assert.Equal(name, zero.ParamName);
            Assert.Equal(name, negative.ParamName);
            Assert.NotNull(create(1));
        }
    }

    /// <summary>
    ///     Validates that the instance is immutable: the properties have no public setters.
    /// </summary>
    [Fact]
    public void ScadLimits_Properties_AreInitOnly()
    {
        // Arrange
        var properties = typeof(ScadLimits).GetProperties().Where(p => p.Name.StartsWith("Max", StringComparison.Ordinal));

        // Act / Assert: every settable property uses an init-only setter
        foreach (var property in properties)
        {
            var setter = property.SetMethod!;
            var modifiers = setter.ReturnParameter.GetRequiredCustomModifiers();
            Assert.Contains(modifiers, m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
        }
    }
}
