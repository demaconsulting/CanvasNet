namespace DemaConsulting.CanvasNet.Scad.Tests;

// cspell:ignore enumerables

/// <summary>
///     Tests of the public <see cref="ScadEvaluationOptions"/> and <see cref="ScadEvaluationResult"/> types: defaults,
///     validation, copying and immutability.
/// </summary>
public class ScadEvaluationOptionsTests
{
    /// <summary>
    ///     Validates the defaults of a new options object.
    /// </summary>
    [Fact]
    public void ScadEvaluationOptions_Defaults_AreEmptyAndDefaultLimits()
    {
        // Arrange / Act
        var options = new ScadEvaluationOptions();

        // Assert
        Assert.Null(options.Variables);
        Assert.Equal(0, options.Time);
        Assert.Same(ScadLimits.Default, options.Limits);
        Assert.Null(options.EchoHandler);
        Assert.Null(options.WarningHandler);
        Assert.Null(options.UnsupportedFeatureHandler);
        Assert.False(options.ThrowOnUnsupportedFeature);
    }

    /// <summary>
    ///     Validates that a non-finite time is rejected and a finite one accepted.
    /// </summary>
    /// <param name="time">The time.</param>
    /// <param name="accepted">Whether the value is accepted.</param>
    [Theory]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(double.NegativeInfinity, false)]
    [InlineData(0.25, true)]
    [InlineData(-3.0, true)]
    public void ScadEvaluationOptions_Time_NonFiniteIsRejected(double time, bool accepted)
    {
        // Arrange / Act
        var exception = Record.Exception(() => new ScadEvaluationOptions { Time = time });

        // Assert
        if (accepted)
        {
            Assert.Null(exception);
        }
        else
        {
            Assert.IsType<ArgumentOutOfRangeException>(exception);
        }
    }

    /// <summary>
    ///     Validates that null limits are rejected and that the supplied limits object is kept.
    /// </summary>
    [Fact]
    public void ScadEvaluationOptions_Limits_NullIsRejectedAndValueKept()
    {
        // Arrange
        var limits = new ScadLimits { MaxInstances = 7 };

        // Act
        var options = new ScadEvaluationOptions { Limits = limits };

        // Assert
        Assert.Same(limits, options.Limits);
        Assert.Throws<ArgumentNullException>(() => new ScadEvaluationOptions { Limits = null! });
    }

    /// <summary>
    ///     Validates that the handlers and the throw flag are stored as supplied.
    /// </summary>
    [Fact]
    public void ScadEvaluationOptions_Handlers_AreStored()
    {
        // Arrange
        Action<string> echo = _ => { };
        Action<ScadDiagnostic> warning = _ => { };
        Action<ScadUnsupportedFeature> unsupported = _ => { };

        // Act
        var options = new ScadEvaluationOptions
        {
            EchoHandler = echo,
            WarningHandler = warning,
            UnsupportedFeatureHandler = unsupported,
            ThrowOnUnsupportedFeature = true,
        };

        // Assert
        Assert.Same(echo, options.EchoHandler);
        Assert.Same(warning, options.WarningHandler);
        Assert.Same(unsupported, options.UnsupportedFeatureHandler);
        Assert.True(options.ThrowOnUnsupportedFeature);
    }

    /// <summary>
    ///     Validates that the variables are copied when set and listed in ordinal key order.
    /// </summary>
    [Fact]
    public void ScadEvaluationOptions_Variables_AreCopiedAndOrderedByKey()
    {
        // Arrange
        var source = new Dictionary<string, object?> { ["b"] = 2, ["B"] = "x", ["a"] = null, ["$fn"] = 12.5 };

        // Act
        var options = new ScadEvaluationOptions { Variables = source };
        source["later"] = 1;
        source["a"] = 99;

        // Assert: ordinal order ('$' < 'B' < 'a' < 'b'), no later change visible
        Assert.NotNull(options.Variables);
        Assert.Equal(["$fn", "B", "a", "b"], options.Variables.Keys);
        Assert.Null(options.Variables["a"]);
        Assert.Equal(2, options.Variables["b"]);
        Assert.Equal("x", options.Variables["B"]);
    }

    /// <summary>
    ///     Validates that a sequence value is enumerated once when set and kept as an array of converted elements.
    /// </summary>
    [Fact]
    public void ScadEvaluationOptions_Variables_SequencesAreSnapshotted()
    {
        // Arrange: an iterator that can only be enumerated once with the same content
        var enumerations = 0;
        IEnumerable<object?> Sequence()
        {
            enumerations++;
            yield return 1;
            yield return new[] { "a", "b" };
            yield return true;
        }

        // Act
        var options = new ScadEvaluationOptions { Variables = new Dictionary<string, object?> { ["v"] = Sequence() } };
        var first = Assert.IsType<object?[]>(options.Variables!["v"]);
        var second = Assert.IsType<object?[]>(options.Variables["v"]);

        // Assert
        Assert.Equal(1, enumerations);
        Assert.Same(first, second);
        Assert.Equal(1.0, first[0]);
        Assert.Equal(new object?[] { "a", "b" }, Assert.IsType<object?[]>(first[1]));
        Assert.True((bool)first[2]!);
    }

    /// <summary>
    ///     Validates that every supported scalar and sequence kind is accepted.
    /// </summary>
    [Fact]
    public void ScadEvaluationOptions_Variables_AcceptSupportedKinds()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["undefined"] = null,
            ["flag"] = true,
            ["i"] = 3,
            ["l"] = 4L,
            ["f"] = 1.5f,
            ["d"] = 2.5,
            ["m"] = 7.25m,
            ["inf"] = double.PositiveInfinity,
            ["text"] = "hello",
            ["list"] = new List<int> { 1, 2, 3 },
            ["matrix"] = new[] { new[] { 1, 0 }, new[] { 0, 1 } },
            ["empty"] = Array.Empty<object>(),
        };

        // Act
        var options = new ScadEvaluationOptions { Variables = variables };

        // Assert
        Assert.Equal(variables.Count, options.Variables!.Count);
    }

    /// <summary>
    ///     Validates that invalid variable names are rejected and the message names the key.
    /// </summary>
    /// <param name="name">The invalid name.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1a")]
    [InlineData("a-b")]
    [InlineData("a b")]
    [InlineData("$")]
    [InlineData("$$a")]
    [InlineData("a$")]
    [InlineData("é")]
    [InlineData("a.b")]
    public void ScadEvaluationOptions_Variables_InvalidNameIsRejected(string name)
    {
        // Arrange
        var variables = new Dictionary<string, object?> { [name] = 1 };

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new ScadEvaluationOptions { Variables = variables });

        // Assert
        Assert.Equal("Variables", ex.ParamName);
        Assert.Contains($"'{name}'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that valid names, including special variables, are accepted.
    /// </summary>
    /// <param name="name">The valid name.</param>
    [Theory]
    [InlineData("a")]
    [InlineData("_x1")]
    [InlineData("Width2")]
    [InlineData("$fn")]
    [InlineData("$t")]
    [InlineData("$1")]
    public void ScadEvaluationOptions_Variables_ValidNameIsAccepted(string name)
    {
        // Arrange / Act
        var options = new ScadEvaluationOptions { Variables = new Dictionary<string, object?> { [name] = 1 } };

        // Assert
        Assert.Contains(name, options.Variables!.Keys);
    }

    /// <summary>
    ///     Validates that unsupported value types are rejected and the message names the key.
    /// </summary>
    [Fact]
    public void ScadEvaluationOptions_Variables_UnsupportedValueIsRejected()
    {
        // Arrange
        var objects = new[]
        {
            new object(),
            DateTime.UtcNow,
            new[] { new object() },
            new Dictionary<string, int>().Select(p => (object)p).Append(new object()),
            'c',
        };

        // Act / Assert
        foreach (var value in objects)
        {
            var ex = Assert.Throws<ArgumentException>(() => new ScadEvaluationOptions { Variables = new Dictionary<string, object?> { ["key"] = value } });
            Assert.Equal("Variables", ex.ParamName);
            Assert.Contains("'key'", ex.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Validates that a sequence nested deeper than the language limit is rejected and one at the limit accepted.
    /// </summary>
    [Fact]
    public void ScadEvaluationOptions_Variables_TooDeepNestingIsRejected()
    {
        // Arrange
        static object? Nest(int depth)
        {
            object? value = new object?[] { 1 };
            for (var i = 1; i < depth; i++)
            {
                value = new[] { value };
            }

            return value;
        }

        // Act
        var atLimit = new ScadEvaluationOptions { Variables = new Dictionary<string, object?> { ["v"] = Nest(256) } };
        var ex = Assert.Throws<ArgumentException>(() => new ScadEvaluationOptions { Variables = new Dictionary<string, object?> { ["v"] = Nest(257) } });

        // Assert
        Assert.NotNull(atLimit.Variables);
        Assert.Contains("'v'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an endless or self-referential sequence fails closed instead of hanging.
    /// </summary>
    [Fact]
    public void ScadEvaluationOptions_Variables_EndlessAndSelfReferentialSequencesAreRejected()
    {
        // Arrange
        // An effectively endless sequence: the conversion budget stops it long before the end
        var endless = Enumerable.Repeat(1, int.MaxValue);

        var selfReferential = new List<object?>();
        selfReferential.Add(selfReferential);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => new ScadEvaluationOptions { Variables = new Dictionary<string, object?> { ["e"] = endless } });
        Assert.Throws<ArgumentException>(() => new ScadEvaluationOptions { Variables = new Dictionary<string, object?> { ["s"] = selfReferential } });
    }

    /// <summary>
    ///     Validates that a null dictionary clears the variables.
    /// </summary>
    [Fact]
    public void ScadEvaluationOptions_Variables_NullMeansNone()
    {
        // Arrange / Act
        var options = new ScadEvaluationOptions { Variables = null };

        // Assert
        Assert.Null(options.Variables);
    }
}
