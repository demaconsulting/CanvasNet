// cspell:ignore canvasnet outptu ouput zzzzzzzz rgba dwidth notacolor rrggbbaa
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Unit tests for the ArgumentReader class: option reading and the value grammars.
/// </summary>
[Collection("Sequential")]
public class ArgumentReaderTests
{
    /// <summary>
    ///     The options of the command the tests read.
    /// </summary>
    private static readonly OptionSpec[] Specs =
    [
        new("--output", ["-o"], true, "The output file", "file"),
        new("--crop", [], true, "Crop to a rectangle", "geometry"),
        new("--resize", [], true, "Resize", "size"),
        new("--azimuth", [], true, "The azimuth in degrees", "degrees"),
        new("--no-clobber", [], false, "Do not overwrite"),
        new("-D", [], true, "Define a variable", "name=value")
    ];

    /// <summary>
    ///     Reads arguments with the test option table.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The reader.</returns>
    private static ArgumentReader Read(params string[] args)
    {
        return ArgumentReader.Parse("render", args, Specs);
    }

    /// <summary>
    ///     Asserts that an action throws a usage error and returns the exception.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>The exception.</returns>
    private static ToolException AssertUsageError(Action action)
    {
        var exception = Assert.Throws<ToolException>(action);
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        return exception;
    }

    /// <summary>
    ///     Test that an option's value can follow it as the next token.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parse_SeparateValue_ReadsValue()
    {
        // Arrange / Act: read an option with its value as the next token
        var reader = Read("--output", "a.png");

        // Assert: the option is present with its value
        Assert.True(reader.Has("--output"));
        Assert.Equal("a.png", reader.Get("--output"));
    }

    /// <summary>
    ///     Test that an option's value can be attached with an equals sign, and may itself contain equals signs.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parse_EqualsValue_ReadsValue()
    {
        // Arrange / Act: read the --name=value form, with a value that contains an equals sign
        var reader = Read("--output=a=b.png");

        // Assert: everything after the first equals sign is the value
        Assert.Equal("a=b.png", reader.Get("--output"));
    }

    /// <summary>
    ///     Test that a short alias is recorded under the canonical name.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parse_ShortAlias_RecordsCanonicalName()
    {
        // Arrange / Act: read an option through its short alias
        var reader = Read("-o", "a.png");

        // Assert: the option is found by its canonical name
        Assert.Equal("a.png", reader.Get("--output"));
        Assert.Equal(new ParsedOption("--output", "a.png"), Assert.Single(reader.Ordered));
    }

    /// <summary>
    ///     Test that a short alias may carry its value attached.
    /// </summary>
    /// <param name="token">The attached form.</param>
    /// <param name="option">The canonical option name.</param>
    /// <param name="expected">The expected value.</param>
    [Theory]
    [InlineData("-oa.png", "--output", "a.png")]
    [InlineData("-o=a.png", "--output", "a.png")]
    [InlineData("-Dwidth=20", "-D", "width=20")]
    public void ArgumentReader_Parse_AttachedShortValue_ReadsValue(string token, string option, string expected)
    {
        // Arrange / Act: read an option with its value attached to the short name
        var reader = Read(token);

        // Assert: the attached text is the value
        Assert.Equal(expected, reader.Get(option));
    }

    /// <summary>
    ///     Test that a value starting with a dash is a value, not an option.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parse_ValueStartingWithDash_IsTheValue()
    {
        // Arrange / Act: read an option whose value is a negative number
        var reader = Read("--azimuth", "-45");

        // Assert: the negative number is the value and there are no positionals
        Assert.Equal("-45", reader.Get("--azimuth"));
        Assert.Empty(reader.Positionals);
    }

    /// <summary>
    ///     Test that a flag is present without a value.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parse_Flag_IsPresentWithoutValue()
    {
        // Arrange / Act: read a flag
        var reader = Read("--no-clobber");

        // Assert: present, with no value
        Assert.True(reader.Has("--no-clobber"));
        Assert.Null(reader.Get("--no-clobber"));
        Assert.False(reader.Has("--output"));
    }

    /// <summary>
    ///     Test that positional arguments are kept in order, interleaved with options.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parse_Positionals_KeptInOrder()
    {
        // Arrange / Act: read positional arguments around an option
        var reader = Read("in.png", "--output", "out.png", "extra");

        // Assert: the option's value is not a positional
        Assert.Equal(new[] { "in.png", "extra" }, reader.Positionals);
    }

    /// <summary>
    ///     Test that a lone dash and negative numbers are positional arguments.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parse_DashAndNegativeNumber_ArePositional()
    {
        // Arrange / Act: read a lone dash, a negative integer, and a negative fraction
        var reader = Read("-", "-90", "-.5");

        // Assert: all three are positional
        Assert.Equal(new[] { "-", "-90", "-.5" }, reader.Positionals);
    }

    /// <summary>
    ///     Test that everything after a bare double dash is positional.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parse_DoubleDash_EndsOptions()
    {
        // Arrange / Act: read an option-like token after a bare double dash
        var reader = Read("--", "--output", "x");

        // Assert: nothing was read as an option
        Assert.Equal(new[] { "--output", "x" }, reader.Positionals);
        Assert.Empty(reader.Ordered);
    }

    /// <summary>
    ///     Test that repeated options are kept in command-line order, and Get returns the last.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parse_RepeatedOptions_KeptInCommandLineOrder()
    {
        // Arrange / Act: read interleaved repeated options
        var reader = Read("--crop", "a", "--resize", "b", "--crop", "c", "--no-clobber");

        // Assert: the ordered list, GetAll, and Get agree
        Assert.Equal(
            new[]
            {
                new ParsedOption("--crop", "a"),
                new ParsedOption("--resize", "b"),
                new ParsedOption("--crop", "c"),
                new ParsedOption("--no-clobber", null)
            },
            reader.Ordered);
        Assert.Equal(new[] { "a", "c" }, reader.GetAll("--crop"));
        Assert.Equal("c", reader.Get("--crop"));
        Assert.Empty(reader.GetAll("--output"));
    }

    /// <summary>
    ///     Test that an unknown option is a usage error that suggests the closest option.
    /// </summary>
    /// <param name="typed">The mistyped option.</param>
    /// <param name="suggestion">The suggested option.</param>
    [Theory]
    [InlineData("--outptu", "--output")]
    [InlineData("--out", "--output")]
    [InlineData("--ouput", "--output")]
    public void ArgumentReader_Parse_UnknownOption_SuggestsClosestOption(string typed, string suggestion)
    {
        // Arrange / Act: read an option that is not declared
        var exception = AssertUsageError(() => Read(typed, "x"));

        // Assert: the message names the option (without any inline value) and the hint suggests the closest one
        Assert.StartsWith($"Unknown option '{typed.Split('=')[0]}' for 'render'.", exception.Message);
        Assert.Contains($"Did you mean '{suggestion}'?", exception.Hint);
    }

    /// <summary>
    ///     Test that an unknown option with no close match points at the help.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parse_UnknownOptionNoSuggestion_PointsAtHelp()
    {
        // Arrange / Act: read an option that is nothing like a declared one
        var exception = AssertUsageError(() => Read("--zzzzzzzz"));

        // Assert: no suggestion, the hint points at the command's help
        Assert.DoesNotContain("Did you mean", exception.Hint);
        Assert.Contains("canvasnet render --help", exception.Hint);
    }

    /// <summary>
    ///     Test that a missing value is a usage error naming the option and its placeholder.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parse_MissingValue_ThrowsUsageError()
    {
        // Arrange / Act: read a value option as the last token
        var exception = AssertUsageError(() => Read("in.png", "--output"));

        // Assert: the message names the option and the value
        Assert.Equal("Option '--output' requires a value <file>.", exception.Message);
    }

    /// <summary>
    ///     Test that giving a value to a flag is a usage error.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parse_FlagWithValue_ThrowsUsageError()
    {
        // Arrange / Act: read a flag with an inline value
        var exception = AssertUsageError(() => Read("--no-clobber=yes"));

        // Assert: the message says the flag takes no value
        Assert.Equal("Option '--no-clobber' does not take a value.", exception.Message);
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parse_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: each null argument throws
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.Parse(null!, [], Specs));
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.Parse("render", null!, Specs));
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.Parse("render", [], null!));
    }

    /// <summary>
    ///     Test that asking for an option the command does not declare is a defect, not a usage error.
    /// </summary>
    [Fact]
    public void ArgumentReader_Lookup_UndeclaredOption_ThrowsKeyNotFoundException()
    {
        // Arrange: a reader
        var reader = Read();

        // Act / Assert: every lookup of an undeclared name throws a non-expected exception type
        Assert.Throws<KeyNotFoundException>(() => reader.Has("--bogus"));
        Assert.Throws<KeyNotFoundException>(() => reader.Get("--bogus"));
        Assert.Throws<KeyNotFoundException>(() => reader.GetAll("--bogus"));
    }

    /// <summary>
    ///     Test that GetInt reads an integer within its range, and returns null when the option is absent.
    /// </summary>
    [Fact]
    public void ArgumentReader_GetInt_ValidValue_ReturnsValue()
    {
        // Arrange / Act: read a value and an absent option
        var reader = Read("--azimuth", "45");

        // Assert: the value is returned, an absent option gives null
        Assert.Equal(45, reader.GetInt("--azimuth", 0, 360));
        Assert.Null(reader.GetInt("--output", 0, 10));
    }

    /// <summary>
    ///     Test that GetInt rejects text that is not an integer and values outside the range.
    /// </summary>
    /// <param name="text">The value.</param>
    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("")]
    [InlineData("361")]
    [InlineData("-1")]
    public void ArgumentReader_GetInt_InvalidValue_ThrowsUsageError(string text)
    {
        // Arrange: a reader holding the value
        var reader = Read("--azimuth", text);

        // Act / Assert: the integer range 0..360 is enforced with a usage error naming the option
        var exception = AssertUsageError(() => reader.GetInt("--azimuth", 0, 360));
        Assert.Contains("--azimuth", exception.Message);
    }

    /// <summary>
    ///     Test that GetDouble reads a finite number within its range.
    /// </summary>
    [Fact]
    public void ArgumentReader_GetDouble_ValidValue_ReturnsValue()
    {
        // Arrange / Act: read a fractional value and an absent option
        var reader = Read("--azimuth", "-12.5");

        // Assert: the value is returned, an absent option gives null
        Assert.Equal(-12.5, reader.GetDouble("--azimuth", -360, 360));
        Assert.Null(reader.GetDouble("--output", 0, 1));
    }

    /// <summary>
    ///     Test that GetDouble rejects non-numbers, non-finite numbers, and values outside the range.
    /// </summary>
    /// <param name="text">The value.</param>
    [Theory]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("400")]
    public void ArgumentReader_GetDouble_InvalidValue_ThrowsUsageError(string text)
    {
        // Arrange: a reader holding the value
        var reader = Read("--azimuth", text);

        // Act / Assert: a usage error
        AssertUsageError(() => reader.GetDouble("--azimuth", -360, 360));
    }

    /// <summary>
    ///     Test that RequirePositionals accepts a count within the range.
    /// </summary>
    [Fact]
    public void ArgumentReader_RequirePositionals_CountInRange_DoesNotThrow()
    {
        // Arrange: a reader with one positional
        var reader = Read("in.png");

        // Act / Assert: one is within 1..2, so no exception is thrown
        Assert.Null(Record.Exception(() => reader.RequirePositionals(1, 2, "an input file")));
    }

    /// <summary>
    ///     Test that RequirePositionals rejects a count outside the range with a usage error.
    /// </summary>
    [Fact]
    public void ArgumentReader_RequirePositionals_CountOutOfRange_ThrowsUsageError()
    {
        // Arrange / Act / Assert: none given when one is expected
        var missing = AssertUsageError(() => Read().RequirePositionals(1, 1, "an input file"));
        Assert.Equal("'render' expects an input file, but none were given.", missing.Message);

        // Assert: two given when one is expected
        var surplus = AssertUsageError(() => Read("a", "b").RequirePositionals(1, 1, "an input file"));
        Assert.Equal("'render' expects an input file, but 2 were given.", surplus.Message);

        // Assert: a command that takes no arguments says so
        var none = AssertUsageError(() => Read("a").RequirePositionals(0, 0, "nothing"));
        Assert.Equal("'render' takes no arguments, but 1 were given.", none.Message);
    }

    /// <summary>
    ///     Test that integers parse with range checks and the invariant culture.
    /// </summary>
    [Fact]
    public void ArgumentReader_ParseInt_ValidAndInvalid_ParsesOrThrows()
    {
        // Arrange / Act / Assert: valid values, the bounds, and a signed value
        Assert.Equal(7, ArgumentReader.ParseInt("7", "--n", 1, 10));
        Assert.Equal(1, ArgumentReader.ParseInt("1", "--n", 1, 10));
        Assert.Equal(-3, ArgumentReader.ParseInt("-3", "--n", -5, 5));

        // Assert: out of range, and a thousands separator, are usage errors
        AssertUsageError(() => ArgumentReader.ParseInt("11", "--n", 1, 10));
        AssertUsageError(() => ArgumentReader.ParseInt("1,000", "--n", 1, 10000));
    }

    /// <summary>
    ///     Test that numbers use the invariant culture whatever the current culture is.
    /// </summary>
    [Fact]
    public void ArgumentReader_ParseNumber_GermanCulture_UsesInvariantCulture()
    {
        // Arrange: a culture whose decimal separator is a comma
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            // Act / Assert: the decimal point is accepted and the decimal comma is not
            Assert.Equal(1.5, ArgumentReader.ParseNumber("1.5", "--n", 0, 10));
            AssertUsageError(() => ArgumentReader.ParseNumber("1,5", "--n", 0, 10));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    ///     Test that valid sizes parse.
    /// </summary>
    /// <param name="text">The size text.</param>
    /// <param name="width">The expected width.</param>
    /// <param name="height">The expected height.</param>
    [Theory]
    [InlineData("800x600", 800, 600)]
    [InlineData("800X600", 800, 600)]
    [InlineData("1x1", 1, 1)]
    [InlineData("8192x8192", 8192, 8192)]
    public void ArgumentReader_ParseSize_ValidSize_ReturnsWidthAndHeight(string text, int width, int height)
    {
        // Arrange / Act: parse the size
        var size = ArgumentReader.ParseSize(text, "--resize");

        // Assert: width and height
        Assert.Equal((width, height), size);
    }

    /// <summary>
    ///     Test that malformed sizes are usage errors.
    /// </summary>
    /// <param name="text">The size text.</param>
    [Theory]
    [InlineData("800")]
    [InlineData("800x")]
    [InlineData("x600")]
    [InlineData("0x10")]
    [InlineData("10x0")]
    [InlineData("10x-5")]
    [InlineData("axb")]
    [InlineData("800x600x2")]
    [InlineData("1.5x2")]
    [InlineData("")]
    public void ArgumentReader_ParseSize_Malformed_ThrowsUsageError(string text)
    {
        // Arrange / Act: parse a malformed size
        var exception = AssertUsageError(() => ArgumentReader.ParseSize(text, "--resize"));

        // Assert: the message names the option and the value, and the hint gives an example
        Assert.Contains("--resize", exception.Message);
        Assert.Contains($"'{text}'", exception.Message);
        Assert.Contains("800x600", exception.Hint);
    }

    /// <summary>
    ///     Test that a size beyond the maximum image dimension is a size-limit error.
    /// </summary>
    [Fact]
    public void ArgumentReader_ParseSize_BeyondMaxDimension_ThrowsSizeLimit()
    {
        // Arrange / Act: parse a size one pixel over the limit
        var exception = Assert.Throws<ToolException>(() => ArgumentReader.ParseSize($"{Surface.MaxDimension + 1}x10", "--resize"));

        // Assert: the size-limit code
        Assert.Equal(ErrorCodes.SizeLimit, exception.Code);
    }

    /// <summary>
    ///     Test that both geometry forms parse, with pixel and percent values.
    /// </summary>
    /// <param name="text">The geometry text.</param>
    /// <param name="x">The expected X value.</param>
    /// <param name="xPercent">Whether X is a percentage.</param>
    /// <param name="y">The expected Y value.</param>
    /// <param name="yPercent">Whether Y is a percentage.</param>
    /// <param name="w">The expected width value.</param>
    /// <param name="wPercent">Whether the width is a percentage.</param>
    /// <param name="h">The expected height value.</param>
    /// <param name="hPercent">Whether the height is a percentage.</param>
    [Theory]
    [InlineData("10,20,30,40", 10, false, 20, false, 30, false, 40, false)]
    [InlineData("10px,20px,30px,40px", 10, false, 20, false, 30, false, 40, false)]
    [InlineData("0,0,50%,12.5%", 0, false, 0, false, 50, true, 12.5, true)]
    [InlineData("10%,5%,80%,90%", 10, true, 5, true, 80, true, 90, true)]
    [InlineData("30x40+10+20", 10, false, 20, false, 30, false, 40, false)]
    [InlineData("30x40", 0, false, 0, false, 30, false, 40, false)]
    [InlineData("30pxx40px+1px+2px", 1, false, 2, false, 30, false, 40, false)]
    [InlineData("50%x25%+10%+5%", 10, true, 5, true, 50, true, 25, true)]
    [InlineData("100X50+0+0", 0, false, 0, false, 100, false, 50, false)]
    public void ArgumentReader_ParseGeometry_ValidForms_ReturnsRectangle(
        string text,
        double x,
        bool xPercent,
        double y,
        bool yPercent,
        double w,
        bool wPercent,
        double h,
        bool hPercent)
    {
        // Arrange / Act: parse the geometry
        var geometry = ArgumentReader.ParseGeometry(text, "--crop");

        // Assert: each component has its value and unit
        Assert.Equal(new Dimension(x, xPercent), geometry.X);
        Assert.Equal(new Dimension(y, yPercent), geometry.Y);
        Assert.Equal(new Dimension(w, wPercent), geometry.Width);
        Assert.Equal(new Dimension(h, hPercent), geometry.Height);
    }

    /// <summary>
    ///     Test that malformed geometry is a usage error.
    /// </summary>
    /// <param name="text">The geometry text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1,2,3")]
    [InlineData("1,2,3,4,5")]
    [InlineData("1,2,0,5")]
    [InlineData("1,2,5,0%")]
    [InlineData("-1,0,5,5")]
    [InlineData("1.5,0,5,5")]
    [InlineData("101%,0,5,5")]
    [InlineData("0,0,5,5.5%x")]
    [InlineData("30x40+1")]
    [InlineData("30x40+1+2+3")]
    [InlineData("30x0+1+2")]
    [InlineData("x40+1+2")]
    [InlineData("30x+1+2")]
    public void ArgumentReader_ParseGeometry_Malformed_ThrowsUsageError(string text)
    {
        // Arrange / Act: parse malformed geometry
        var exception = AssertUsageError(() => ArgumentReader.ParseGeometry(text, "--crop"));

        // Assert: the message names the option and the hint gives an example
        Assert.Contains("--crop", exception.Message);
        Assert.Contains("10,10,200,100", exception.Hint);
    }

    /// <summary>
    ///     Test that colors parse in every hexadecimal form with the alpha pair last.
    /// </summary>
    /// <param name="text">The color text.</param>
    /// <param name="r">The expected red.</param>
    /// <param name="g">The expected green.</param>
    /// <param name="b">The expected blue.</param>
    /// <param name="a">The expected alpha.</param>
    [Theory]
    [InlineData("#f00", 255, 0, 0, 255)]
    [InlineData("#f008", 255, 0, 0, 0x88)]
    [InlineData("#336699", 0x33, 0x66, 0x99, 255)]
    [InlineData("#33669980", 0x33, 0x66, 0x99, 0x80)]
    [InlineData("#FF000080", 255, 0, 0, 0x80)]
    [InlineData("336699", 0x33, 0x66, 0x99, 255)]
    [InlineData("  #abc  ", 0xAA, 0xBB, 0xCC, 255)]
    public void ArgumentReader_ParseColor_HexForms_ReturnsColorWithAlphaLast(string text, byte r, byte g, byte b, byte a)
    {
        // Arrange / Act: parse the color
        var color = ArgumentReader.ParseColor(text, "--background");

        // Assert: CSS order, so the last pair is the alpha (not Rgba32.Parse's leading alpha)
        Assert.Equal(new Rgba32(r, g, b, a), color);
    }

    /// <summary>
    ///     Test that color names and transparent parse, ignoring case.
    /// </summary>
    /// <param name="text">The color text.</param>
    /// <param name="r">The expected red.</param>
    /// <param name="g">The expected green.</param>
    /// <param name="b">The expected blue.</param>
    /// <param name="a">The expected alpha.</param>
    [Theory]
    [InlineData("white", 255, 255, 255, 255)]
    [InlineData("Black", 0, 0, 0, 255)]
    [InlineData("RED", 255, 0, 0, 255)]
    [InlineData("grey", 128, 128, 128, 255)]
    [InlineData("orange", 255, 165, 0, 255)]
    [InlineData("SteelBlue", 70, 130, 180, 255)]
    [InlineData("rebeccapurple", 102, 51, 153, 255)]
    [InlineData("  lightgoldenrodyellow ", 250, 250, 210, 255)]
    [InlineData("transparent", 0, 0, 0, 0)]
    [InlineData("Transparent", 0, 0, 0, 0)]
    public void ArgumentReader_ParseColor_NamesAndTransparent_ReturnsColor(string text, byte r, byte g, byte b, byte a)
    {
        // Arrange / Act: parse the color
        var color = ArgumentReader.ParseColor(text, "--background");

        // Assert: each channel, so that a name with the wrong alpha is caught as well
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
        Assert.Equal(a, color.A);
    }

    /// <summary>
    ///     Test that text that is not a color is a usage error.
    /// </summary>
    /// <param name="text">The color text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#gggggg")]
    [InlineData("#123456789")]
    [InlineData("notacolor")]
    [InlineData("rgb(255,0,0)")]
    [InlineData("hsl(0,100%,50%)")]
    [InlineData("none")]
    public void ArgumentReader_ParseColor_Invalid_ThrowsUsageError(string text)
    {
        // Arrange / Act: parse something that is not a color
        var exception = AssertUsageError(() => ArgumentReader.ParseColor(text, "--background"));

        // Assert: the message names the option
        Assert.Contains("--background", exception.Message);
    }

    /// <summary>
    ///     Test that angles normalize into 0 up to 360.
    /// </summary>
    /// <param name="text">The angle text.</param>
    /// <param name="expected">The expected normalized angle.</param>
    [Theory]
    [InlineData("90", 90)]
    [InlineData("-90", 270)]
    [InlineData("450", 90)]
    [InlineData("360", 0)]
    [InlineData("0", 0)]
    [InlineData("12.5deg", 12.5)]
    [InlineData("180DEG", 180)]
    public void ArgumentReader_ParseDegrees_ValidAngle_ReturnsNormalizedAngle(string text, double expected)
    {
        // Arrange / Act / Assert: the angle is normalized
        Assert.Equal(expected, ArgumentReader.ParseDegrees(text, "<degrees>"));
    }

    /// <summary>
    ///     Test that text that is not a finite angle is a usage error.
    /// </summary>
    /// <param name="text">The angle text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("deg")]
    public void ArgumentReader_ParseDegrees_Invalid_ThrowsUsageError(string text)
    {
        // Arrange / Act / Assert: a usage error
        AssertUsageError(() => ArgumentReader.ParseDegrees(text, "<degrees>"));
    }

    /// <summary>
    ///     Test that flip axes parse by short and long name, ignoring case.
    /// </summary>
    [Fact]
    public void ArgumentReader_ParseFlip_ValidAxis_ReturnsAxis()
    {
        // Arrange / Act / Assert: short and long names, in any case
        Assert.Equal(FlipAxis.Horizontal, ArgumentReader.ParseFlip("h", "<axis>"));
        Assert.Equal(FlipAxis.Horizontal, ArgumentReader.ParseFlip("Horizontal", "<axis>"));
        Assert.Equal(FlipAxis.Vertical, ArgumentReader.ParseFlip("V", "<axis>"));
        Assert.Equal(FlipAxis.Vertical, ArgumentReader.ParseFlip("vertical", "<axis>"));
        Assert.Equal(FlipAxis.Both, ArgumentReader.ParseFlip("both", "<axis>"));
    }

    /// <summary>
    ///     Test that an unknown flip axis is a usage error.
    /// </summary>
    [Fact]
    public void ArgumentReader_ParseFlip_Invalid_ThrowsUsageError()
    {
        // Arrange / Act / Assert: a usage error that lists the choices
        var exception = AssertUsageError(() => ArgumentReader.ParseFlip("diagonal", "<axis>"));
        Assert.Contains("h, v, or both", exception.Message);
    }

    /// <summary>
    ///     Test that border widths parse in the single-value and four-value forms.
    /// </summary>
    [Fact]
    public void ArgumentReader_ParsePad_ValidForms_ReturnsWidths()
    {
        // Arrange / Act / Assert: one value applies to every side
        Assert.Equal(new PadSpec(10, 10, 10, 10), ArgumentReader.ParsePad("10", "--pad"));

        // Assert: four values are left, top, right, bottom
        Assert.Equal(new PadSpec(1, 2, 3, 4), ArgumentReader.ParsePad("1,2,3,4", "--pad"));
        Assert.Equal(new PadSpec(0, 0, 0, 0), ArgumentReader.ParsePad("0", "--pad"));
    }

    /// <summary>
    ///     Test that malformed border widths are usage errors.
    /// </summary>
    /// <param name="text">The padding text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("-1")]
    [InlineData("1,2")]
    [InlineData("1,2,3")]
    [InlineData("1,2,3,4,5")]
    [InlineData("1.5")]
    public void ArgumentReader_ParsePad_Malformed_ThrowsUsageError(string text)
    {
        // Arrange / Act / Assert: a usage error
        AssertUsageError(() => ArgumentReader.ParsePad(text, "--pad"));
    }

    /// <summary>
    ///     Test that a border wider than the maximum image dimension is a size-limit error.
    /// </summary>
    [Fact]
    public void ArgumentReader_ParsePad_BeyondMaxDimension_ThrowsSizeLimit()
    {
        // Arrange / Act: parse a border one pixel over the limit
        var exception = Assert.Throws<ToolException>(() => ArgumentReader.ParsePad($"{Surface.MaxDimension + 1}", "--pad"));

        // Assert: the size-limit code
        Assert.Equal(ErrorCodes.SizeLimit, exception.Code);
    }

    /// <summary>
    ///     Test that resolutions and page numbers parse and are range checked.
    /// </summary>
    [Fact]
    public void ArgumentReader_ParseDpiAndPage_ValidAndInvalid_ParsesOrThrows()
    {
        // Arrange / Act / Assert: valid values
        Assert.Equal(300f, ArgumentReader.ParseDpi("300", "--dpi"));
        Assert.Equal(72.5f, ArgumentReader.ParseDpi("72.5", "--dpi"));
        Assert.Equal(1, ArgumentReader.ParsePage("1", "--page"));
        Assert.Equal(12, ArgumentReader.ParsePage("12", "--page"));

        // Assert: invalid values are usage errors
        AssertUsageError(() => ArgumentReader.ParseDpi("0", "--dpi"));
        AssertUsageError(() => ArgumentReader.ParseDpi("-5", "--dpi"));
        AssertUsageError(() => ArgumentReader.ParseDpi("2401", "--dpi"));
        AssertUsageError(() => ArgumentReader.ParseDpi("NaN", "--dpi"));
        AssertUsageError(() => ArgumentReader.ParseDpi("fast", "--dpi"));
        AssertUsageError(() => ArgumentReader.ParsePage("0", "--page"));
        AssertUsageError(() => ArgumentReader.ParsePage("-1", "--page"));
        AssertUsageError(() => ArgumentReader.ParsePage("first", "--page"));
    }

    /// <summary>
    ///     Test that definitions parse into numbers, booleans, and strings.
    /// </summary>
    [Fact]
    public void ArgumentReader_ParseDefines_LiteralTypes_ParsedByType()
    {
        // Arrange / Act: parse one of each literal kind, a quoted string, and a later definition that replaces an earlier one
        var defines = ArgumentReader.ParseDefines(
        [
            "width=20",
            "depth=-1.5",
            "scale=1e3",
            "round=true",
            "hollow=false",
            "label=\"a b\"",
            "mode='fast'",
            "bare=text",
            "empty=",
            "$fn=64",
            "width=30"
        ]);

        // Assert: numbers are doubles, booleans are booleans, everything else is a string; the last definition wins
        Assert.Equal(30.0, defines["width"]);
        Assert.Equal(-1.5, defines["depth"]);
        Assert.Equal(1000.0, defines["scale"]);
        Assert.True((bool)defines["round"]!);
        Assert.False((bool)defines["hollow"]!);
        Assert.Equal("a b", defines["label"]);
        Assert.Equal("fast", defines["mode"]);
        Assert.Equal("text", defines["bare"]);
        Assert.Equal(string.Empty, defines["empty"]);
        Assert.Equal(64.0, defines["$fn"]);
        Assert.Equal(10, defines.Count);
    }

    /// <summary>
    ///     Test that non-finite numbers are kept as text rather than parsed as numbers.
    /// </summary>
    [Fact]
    public void ArgumentReader_ParseDefines_NonFiniteNumber_IsString()
    {
        // Arrange / Act: parse a value that double parsing would accept as infinity
        var defines = ArgumentReader.ParseDefines(["x=Infinity"]);

        // Assert: it is text
        Assert.Equal("Infinity", defines["x"]);
    }

    /// <summary>
    ///     Test that malformed definitions are usage errors.
    /// </summary>
    /// <param name="definition">The definition text.</param>
    [Theory]
    [InlineData("width")]
    [InlineData("=5")]
    [InlineData("1x=5")]
    [InlineData("a-b=5")]
    [InlineData("v=[1,2,3]")]
    public void ArgumentReader_ParseDefines_Malformed_ThrowsUsageError(string definition)
    {
        // Arrange / Act: parse a malformed definition
        var exception = AssertUsageError(() => ArgumentReader.ParseDefines([definition]));

        // Assert: the message names the definition
        Assert.Contains($"'{definition}'", exception.Message);
    }

    /// <summary>
    ///     Test that parsers reject null text.
    /// </summary>
    [Fact]
    public void ArgumentReader_Parsers_NullText_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: each parser rejects null
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.ParseSize(null!, "--x"));
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.ParseGeometry(null!, "--x"));
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.ParseColor(null!, "--x"));
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.ParseDegrees(null!, "--x"));
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.ParseFlip(null!, "--x"));
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.ParsePad(null!, "--x"));
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.ParseDpi(null!, "--x"));
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.ParseInt(null!, "--x", 0, 1));
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.ParseNumber(null!, "--x", 0, 1));
        Assert.Throws<ArgumentNullException>(() => ArgumentReader.ParseDefines(null!));
    }
}
