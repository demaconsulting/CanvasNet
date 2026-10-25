using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using DemaConsulting.CanvasNet.Scad.Language;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of <see cref="ScadArguments"/>: positional and named binding, the warnings for arguments that
///     bind to nothing, and the typed readers.
/// </summary>
public class ScadArgumentsTests
{
    /// <summary>The location the test instances carry.</summary>
    private static readonly ScadSourceLocation Here = new("t.scad", 3, 5);

    /// <summary>The parameter names used by the tests, in positional order.</summary>
    private static readonly string[] Names = ["size", "center", "extra"];

    /// <summary>
    ///     Creates an instance with arguments.
    /// </summary>
    /// <param name="arguments">The arguments in call order.</param>
    /// <returns>The instance.</returns>
    private static ScadInstance Instance(params ScadInstanceArgument[] arguments) =>
        new(ScadInstanceKind.BuiltinModule, "thing", arguments, [], ScadModifiers.None, new ScadFragmentSettings(0, 12, 2), Here);

    /// <summary>
    ///     Binds arguments and returns the bound set and the warnings.
    /// </summary>
    /// <param name="arguments">The arguments in call order.</param>
    /// <returns>The bound arguments and the warnings as text.</returns>
    private static (ScadArguments Args, string[] Warnings) Bind(params ScadInstanceArgument[] arguments)
    {
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var context = new ScadGeometryContext(ScadGeometryTestSupport.Settings(), new ScadReporter(sink, false));
        var bound = ScadArguments.Bind(Instance(arguments), Names, context);
        return (bound, [.. sink.Diagnostics.Select(d => $"{d.Location}: {d.Message}")]);
    }

    /// <summary>
    ///     Validates that unnamed arguments fill the parameters in the documented order.
    /// </summary>
    [Fact]
    public void ScadArguments_Bind_Positional_FillsInOrder()
    {
        // Arrange / Act
        var (args, warnings) = Bind(new(null, N(7)), new(null, B(true)));

        // Assert
        Assert.Empty(warnings);
        Assert.True(args.TryGet("size", out var size));
        Assert.Equal(N(7), size);
        Assert.True(args.TryGet("center", out var center));
        Assert.Equal(B(true), center);
        Assert.False(args.TryGet("extra", out _));
    }

    /// <summary>
    ///     Validates that named arguments bind by name in any order and mix with positional ones.
    /// </summary>
    [Fact]
    public void ScadArguments_Bind_NamedAndMixed_BindByName()
    {
        // Arrange / Act
        var (args, warnings) = Bind(new("extra", N(3)), new(null, N(9)), new("center", B(false)));

        // Assert
        Assert.Empty(warnings);
        Assert.True(args.TryGet("size", out var size));
        Assert.Equal(N(9), size);
        Assert.True(args.TryGet("center", out var center));
        Assert.Equal(B(false), center);
        Assert.True(args.TryGet("extra", out var extra));
        Assert.Equal(N(3), extra);
    }

    /// <summary>
    ///     Validates that an unknown name, a surplus positional value and a repeated parameter are warned about at
    ///     the instance's location, and that special variables are ignored silently.
    /// </summary>
    [Fact]
    public void ScadArguments_Bind_UnknownExtraDuplicateAndSpecial_WarnsExceptSpecial()
    {
        // Arrange / Act
        var (args, warnings) = Bind(
            new("bogus", N(1)),
            new("$fn", N(64)),
            new(null, N(1)),
            new(null, N(2)),
            new(null, N(3)),
            new(null, N(4)),
            new("size", N(5)));

        // Assert
        Assert.Equal(
            [
                "t.scad:3:5: Ignoring unknown argument 'bogus' of thing",
                "t.scad:3:5: Ignoring extra argument 4 of thing",
                "t.scad:3:5: Argument 'size' of thing is given more than once; using the last value",
            ],
            warnings);
        Assert.True(args.TryGet("size", out var size));
        Assert.Equal(N(5), size);
    }

    /// <summary>
    ///     Validates that an <c>undef</c> argument counts as not supplied.
    /// </summary>
    [Fact]
    public void ScadArguments_TryGet_UndefValue_IsNotSupplied()
    {
        // Arrange
        var (args, _) = Bind(new ScadInstanceArgument(null, ScadValue.Undef));

        // Act / Assert
        Assert.False(args.TryGet("size", out var value));
        Assert.Equal(ScadValueKind.Undef, value.Kind);
    }

    /// <summary>
    ///     Validates that asking for a name that is not a parameter is a programming error.
    /// </summary>
    [Fact]
    public void ScadArguments_TryGet_UnknownParameterName_Throws()
    {
        // Arrange
        var (args, _) = Bind();

        // Act / Assert
        Assert.Throws<ArgumentException>(() => args.TryGet("nope", out _));
    }

    /// <summary>
    ///     Validates the number readers: only finite numbers qualify, and a vector needs the exact length.
    /// </summary>
    [Fact]
    public void ScadArguments_TypedReaders_AcceptOnlyWellFormedValues()
    {
        // Arrange / Act / Assert: numbers
        Assert.True(ScadArguments.TryNumber(N(2.5), out var number));
        Assert.Equal(2.5, number);
        Assert.False(ScadArguments.TryNumber(N(double.NaN), out _));
        Assert.False(ScadArguments.TryNumber(N(double.PositiveInfinity), out _));
        Assert.False(ScadArguments.TryNumber(B(true), out _));
        Assert.False(ScadArguments.TryNumber(S("1"), out _));

        // Vectors of an exact length
        Assert.True(ScadArguments.TryNumbers(Nums(1, 2, 3), 3, out var triple));
        Assert.Equal([1.0, 2.0, 3.0], triple!);
        Assert.False(ScadArguments.TryNumbers(Nums(1, 2), 3, out var tooShort));
        Assert.Null(tooShort);
        Assert.False(ScadArguments.TryNumbers(Nums(1, double.NaN, 3), 3, out _));
        Assert.False(ScadArguments.TryNumbers(Vec(N(1), S("x"), N(3)), 3, out _));
        Assert.False(ScadArguments.TryNumbers(N(1), 1, out _));

        // Indices are non-negative whole numbers that fit
        Assert.True(ScadArguments.TryIndex(N(4), out var index));
        Assert.Equal(4, index);
        Assert.False(ScadArguments.TryIndex(N(1.5), out _));
        Assert.False(ScadArguments.TryIndex(N(-1), out _));
        Assert.False(ScadArguments.TryIndex(N(3e9), out _));
        Assert.False(ScadArguments.TryIndex(S("1"), out _));
    }
}
