using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Unit tests for <see cref="VegaLiteRenderOptions"/>.
/// </summary>
public class VegaLiteRenderOptionsTests
{
    /// <summary>
    ///     A resolver that resolves nothing, to be stored in the options.
    /// </summary>
    private sealed class NoResolver : IImageResolver
    {
        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight) => null;
    }

    /// <summary>
    ///     Validates the documented defaults.
    /// </summary>
    [Fact]
    public void VegaLiteRenderOptions_Defaults_AreDocumented()
    {
        // Arrange / Act
        var options = new VegaLiteRenderOptions();

        // Assert
        Assert.Equal(1, options.Scale);
        Assert.Null(options.Background);
        Assert.Null(options.Config);
        Assert.Null(options.Parameters);
        Assert.Null(options.Datasets);
        Assert.Null(options.TimeZone);
        Assert.Equal(400, options.ContainerWidth);
        Assert.Equal(300, options.ContainerHeight);
        Assert.Null(options.Fonts);
        Assert.Null(options.ImageResolver);
        Assert.Same(VegaLiteLimits.Default, options.Limits);
        Assert.Null(options.WarningHandler);
        Assert.Null(options.UnsupportedFeatureHandler);
        Assert.False(options.ThrowOnUnsupportedFeature);
        Assert.False(options.EffectiveFonts.UseSystemFonts);
    }

    /// <summary>
    ///     Validates that valid values are stored.
    /// </summary>
    [Fact]
    public void VegaLiteRenderOptions_ValidValues_AreStored()
    {
        // Arrange
        var fonts = new VegaLiteFontOptions { UseSystemFonts = true };
        var limits = new VegaLiteLimits { MaxTicks = 7 };
        var resolver = new NoResolver();
        Action<VegaLiteDiagnostic> warn = _ => { };
        Action<VegaLiteUnsupportedFeature> unsupported = _ => { };

        // Act
        var options = new VegaLiteRenderOptions
        {
            Scale = 2.5,
            Background = new Rgba32(1, 2, 3, 4),
            Config = "{\"view\":{\"stroke\":null}}",
            TimeZone = TimeZoneInfo.Utc,
            ContainerWidth = 640,
            ContainerHeight = 480,
            Fonts = fonts,
            ImageResolver = resolver,
            Limits = limits,
            WarningHandler = warn,
            UnsupportedFeatureHandler = unsupported,
            ThrowOnUnsupportedFeature = true,
        };

        // Assert
        Assert.Equal(2.5, options.Scale);
        Assert.Equal(new Rgba32(1, 2, 3, 4), options.Background);
        Assert.Equal("{\"view\":{\"stroke\":null}}", options.Config);
        Assert.Same(TimeZoneInfo.Utc, options.TimeZone);
        Assert.Equal(640, options.ContainerWidth);
        Assert.Equal(480, options.ContainerHeight);
        Assert.Same(fonts, options.Fonts);
        Assert.Same(fonts, options.EffectiveFonts);
        Assert.Same(resolver, options.ImageResolver);
        Assert.Same(limits, options.Limits);
        Assert.Same(warn, options.WarningHandler);
        Assert.Same(unsupported, options.UnsupportedFeatureHandler);
        Assert.True(options.ThrowOnUnsupportedFeature);
    }

    /// <summary>
    ///     Validates that invalid scales are rejected at initialization.
    /// </summary>
    /// <param name="scale">The invalid scale.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(8193.0)]
    public void VegaLiteRenderOptions_InvalidScale_Throws(double scale)
    {
        // Arrange / Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new VegaLiteRenderOptions { Scale = scale });

        // Assert
        Assert.Equal("Scale", ex.ParamName);
    }

    /// <summary>
    ///     Validates that invalid container sizes are rejected.
    /// </summary>
    [Fact]
    public void VegaLiteRenderOptions_InvalidContainerSize_Throws()
    {
        // Arrange / Act / Assert
        Assert.Equal("ContainerWidth", Assert.Throws<ArgumentOutOfRangeException>(() => new VegaLiteRenderOptions { ContainerWidth = 0 }).ParamName);
        Assert.Equal("ContainerHeight", Assert.Throws<ArgumentOutOfRangeException>(() => new VegaLiteRenderOptions { ContainerHeight = Surface.MaxDimension + 1 }).ParamName);
    }

    /// <summary>
    ///     Validates that a null limits object is rejected.
    /// </summary>
    [Fact]
    public void VegaLiteRenderOptions_NullLimits_Throws()
    {
        // Arrange / Act
        var ex = Assert.Throws<ArgumentNullException>(() => new VegaLiteRenderOptions { Limits = null! });

        // Assert
        Assert.Equal("Limits", ex.ParamName);
    }

    /// <summary>
    ///     Validates that configuration text must be a JSON object.
    /// </summary>
    /// <param name="config">The invalid configuration text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("{\"a\":")]
    [InlineData("not json")]
    public void VegaLiteRenderOptions_InvalidConfig_Throws(string config)
    {
        // Arrange / Act
        var ex = Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions { Config = config });

        // Assert
        Assert.Equal("Config", ex.ParamName);
    }

    /// <summary>
    ///     Validates that parameters are copied, read-only, and checked for names and value kinds.
    /// </summary>
    [Fact]
    public void VegaLiteRenderOptions_Parameters_AreCopiedAndValidated()
    {
        // Arrange
        var source = new Dictionary<string, object?> { ["a"] = 1, ["b"] = "text", ["c"] = null, ["d"] = 2.5, ["e"] = true, ["f"] = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc) };

        // Act
        var options = new VegaLiteRenderOptions { Parameters = source };
        source["a"] = 99;
        source["z"] = 1;

        // Assert
        Assert.Equal(6, options.Parameters!.Count);
        Assert.Equal(1, options.Parameters["a"]);
        Assert.True(options.Parameters is not IDictionary<string, object?> dictionary || dictionary.IsReadOnly);
        Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { [" "] = 1 } });
        var bad = Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["x"] = new object() } });
        Assert.Contains("'x'", bad.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["x"] = double.NaN } });
        Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["x"] = float.PositiveInfinity } });
    }

    /// <summary>
    ///     Validates that datasets are deep-copied, read-only and validated.
    /// </summary>
    [Fact]
    public void VegaLiteRenderOptions_Datasets_AreDeepCopiedAndValidated()
    {
        // Arrange
        var row = new Dictionary<string, object?> { ["x"] = 1, ["y"] = "a" };
        var rows = new List<IReadOnlyDictionary<string, object?>> { row };
        var source = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> { ["table"] = rows };

        // Act
        var options = new VegaLiteRenderOptions { Datasets = source };
        row["x"] = 2;
        rows.Add(new Dictionary<string, object?>());
        source["other"] = rows;

        // Assert
        Assert.Single(options.Datasets!);
        Assert.Single(options.Datasets["table"]);
        Assert.Equal(1, options.Datasets["table"][0]["x"]);
        Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions
        {
            Datasets = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> { [""] = rows },
        });
        Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions
        {
            Datasets = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> { ["t"] = null! },
        });
        Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions
        {
            Datasets = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> { ["t"] = new List<IReadOnlyDictionary<string, object?>> { null! } },
        });
        Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions
        {
            Datasets = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>
            {
                ["t"] = new List<IReadOnlyDictionary<string, object?>> { new Dictionary<string, object?> { ["x"] = new object() } },
            },
        });
    }

    /// <summary>
    ///     Validates that a parameter accepts a structured value (a sequence or a dictionary of values, nested), that the whole value is copied when the options are created, and that the
    ///     copy is read-only.
    /// </summary>
    [Fact]
    public void VegaLiteRenderOptions_Parameters_AcceptStructuredValuesAndCopyThem()
    {
        // Arrange: an array, a list that holds a dictionary, and a dictionary that holds an array
        var numbers = new List<object?> { 2, 6.5 };
        var member = new Dictionary<string, object?> { ["x"] = new object?[] { 1, "two", null, true } };
        var source = new Dictionary<string, object?> { ["range"] = numbers, ["map"] = member, ["plain"] = 3 };

        // Act
        var options = new VegaLiteRenderOptions { Parameters = source };
        numbers.Add(99);
        member["x"] = 0;
        source["extra"] = 1;

        // Assert: later changes do not reach the options
        Assert.Equal(["map", "plain", "range"], options.Parameters!.Keys.Order(StringComparer.Ordinal));
        var range = Assert.IsAssignableFrom<IReadOnlyList<object?>>(options.Parameters["range"]);
        Assert.Equal([2, 6.5], range);
        var map = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(options.Parameters["map"]);
        Assert.Equal([1, "two", null, true], Assert.IsAssignableFrom<IReadOnlyList<object?>>(map["x"]));
        Assert.IsNotType<List<object?>>(range);
        Assert.Equal(3, options.Parameters["plain"]);
    }

    /// <summary>
    ///     Validates that a structured parameter value is bounded: its nesting (32 levels), its size (65,536 values), the kinds of its members (the scalars of a parameter) and the names of
    ///     its members.
    /// </summary>
    [Fact]
    public void VegaLiteRenderOptions_Parameters_RejectUnboundedOrUnsupportedStructuredValues()
    {
        // Arrange: a value nested 40 deep, a list that contains itself, a value of too many items, an unsupported member and a member without a name
        object? deep = 1;
        for (var i = 0; i < 40; i++)
        {
            deep = new object?[] { deep };
        }

        var cycle = new List<object?>();
        cycle.Add(cycle);

        // Act / Assert
        Assert.Equal("Parameters", Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["p"] = deep } }).ParamName);
        Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["p"] = cycle } });
        Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["p"] = new object?[65_537] } });
        Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["p"] = new object?[] { 1, new object() } } });
        Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["p"] = new object?[] { double.NaN } } });
        Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["p"] = new Dictionary<string, object?> { ["x"] = new object() } } });

        // Assert: the largest accepted value is accepted
        Assert.NotNull(new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["p"] = new object?[65_535] } }.Parameters);
    }
}
