// cspell:ignore vega
using DemaConsulting.CanvasNet.Tool;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Unit tests for <see cref="ArgumentReader.ParseParams(IEnumerable{string})"/>, the value grammar of
///     <c>--param name=value</c>: numbers, booleans, <c>null</c>, text, JSON arrays and objects, and the usage errors.
/// </summary>
public class ArgumentReaderParamsTests
{
    /// <summary>
    ///     Test that scalars and text are parsed by type, that a later parameter replaces an earlier one, and that the
    ///     text after the first <c>=</c> is the value (so a value may contain <c>=</c>).
    /// </summary>
    [Fact]
    public void ArgumentReader_ParseParams_ScalarsAndText_ParsedByType()
    {
        // Act
        var parameters = ArgumentReader.ParseParams(
        [
            "cutoff=50",
            "ratio=-1.5",
            "flag=true",
            "off=false",
            "gone=null",
            "region=EU",
            "label='a b'",
            "quoted=\"a b\"",
            "text=a=b",
            "empty=",
            "cutoff=60"
        ]);

        // Assert
        Assert.Equal(60.0, parameters["cutoff"]);
        Assert.Equal(-1.5, parameters["ratio"]);
        Assert.True(Assert.IsType<bool>(parameters["flag"]));
        Assert.False(Assert.IsType<bool>(parameters["off"]));
        Assert.Null(parameters["gone"]);
        Assert.Equal("EU", parameters["region"]);
        Assert.Equal("a b", parameters["label"]);
        Assert.Equal("a b", parameters["quoted"]);
        Assert.Equal("a=b", parameters["text"]);
        Assert.Equal(string.Empty, parameters["empty"]);
        Assert.Equal(10, parameters.Count);
    }

    /// <summary>
    ///     Test that JSON arrays and objects are parsed into lists and dictionaries, including the shape of an interval
    ///     selection, and that a name may be any text without <c>=</c> (a Vega-Lite parameter name is any non-empty string).
    /// </summary>
    [Fact]
    public void ArgumentReader_ParseParams_JsonValues_ParsedIntoPlainValues()
    {
        // Act
        var parameters = ArgumentReader.ParseParams(["brush={\"x\":[2,6]}", "window=[2, 6]", "my param=1"]);

        // Assert
        var brush = Assert.IsType<Dictionary<string, object?>>(parameters["brush"]);
        Assert.Equal(new object?[] { 2.0, 6.0 }, Assert.IsType<List<object?>>(brush["x"]));
        Assert.Equal(new object?[] { 2.0, 6.0 }, Assert.IsType<List<object?>>(parameters["window"]));
        Assert.Equal(1.0, parameters["my param"]);
    }

    /// <summary>
    ///     Test that a parameter without <c>=</c>, with an empty name, or with a malformed JSON value is a usage error that
    ///     names the parameter and shows an example.
    /// </summary>
    /// <param name="parameter">The parameter as typed.</param>
    [Theory]
    [InlineData("cutoff")]
    [InlineData("=5")]
    [InlineData("")]
    [InlineData("brush={x")]
    [InlineData("brush=[1,")]
    [InlineData("v=\"abc")]
    public void ArgumentReader_ParseParams_Malformed_ThrowsUsageError(string parameter)
    {
        // Act
        var exception = ToolHost.ExpectFailure(() => ArgumentReader.ParseParams([parameter]));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains($"'{parameter}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("--param", exception.Message, StringComparison.Ordinal);
        Assert.StartsWith("Example: --param ", exception.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that no parameter gives an empty dictionary and that a null list is an argument error.
    /// </summary>
    [Fact]
    public void ArgumentReader_ParseParams_NoneOrNull()
    {
        // Act / Assert
        Assert.Empty(ArgumentReader.ParseParams([]));
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.ParseParams(null!));
    }
}
