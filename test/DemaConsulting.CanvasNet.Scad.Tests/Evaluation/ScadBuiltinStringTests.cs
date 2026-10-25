using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

// cspell:ignore defg

/// <summary>
///     Unit tests for the string functions of <see cref="ScadBuiltinFunctions"/>.
/// </summary>
public class ScadBuiltinStringTests
{
    /// <summary>
    ///     Validates str: raw strings, formatted numbers, vectors and concatenation.
    /// </summary>
    [Fact]
    public void ScadBuiltinString_Str_ConcatenatesTextForms()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.Equal(S("a1.5true"), host.Call("str", S("a"), N(1.5), ScadValue.True));
        Assert.Equal(S("[1, \"x\"]"), host.Call("str", Vec(N(1), S("x"))));
        Assert.Equal(S(string.Empty), host.Call("str"));
        Assert.Equal(S("undef"), host.Call("str", ScadValue.Undef));
        Assert.Equal(S("1e+06!"), host.Call("str", N(1_000_000), S("!")));
        Assert.Equal(S("[0 : 1 : 3]"), host.Call("str", ScadValue.FromRange(0, 1, 3)));
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates that str charges the formatting of a large vector per element.
    /// </summary>
    [Fact]
    public void ScadBuiltinString_Str_LargeVector_ChargesPerElement()
    {
        // Arrange
        var host = new ScadTestBuiltinHost { MaxStringLength = 100_000 };
        var vector = ScadValue.FromVector(Enumerable.Range(0, 1000).Select(i => N(i)).ToArray());

        // Act
        host.Call("str", vector);

        // Assert: eight steps for each of the 1,000 elements, plus the final charge for the text
        Assert.True(host.Steps >= 1000 * ScadValue.FormatStepsPerElement, $"steps={host.Steps}");
    }

    /// <summary>
    ///     Validates that str never builds more than the limit plus one element: many large strings and a long vector of
    ///     large strings both stop at the first element that does not fit.
    /// </summary>
    [Fact]
    public void ScadBuiltinString_Str_ManyLargeStrings_AllocateNoMoreThanLimitPlusOneElement()
    {
        // Arrange: fifty arguments of 100,000 characters each against a limit of 100,000 (building them all would take
        // 50 * 100,000 * 2 bytes = 10 MB for the text alone); the vector holds the same strings, quoted
        var large = S(new string('a', 100_000));
        var arguments = Enumerable.Repeat(large, 50).ToArray();
        var vector = ScadValue.FromVector(arguments);
        var firstHost = new ScadTestBuiltinHost { MaxStringLength = 100_000 };
        var secondHost = new ScadTestBuiltinHost { MaxStringLength = 100_000 };
        var before = GC.GetAllocatedBytesForCurrentThread();

        // Act
        var arguments1 = Assert.Throws<ScadEvaluationException>(() => firstHost.Call("str", arguments));
        var afterArguments = GC.GetAllocatedBytesForCurrentThread();
        var vector1 = Assert.Throws<ScadEvaluationException>(() => secondHost.Call("str", vector));
        var afterVector = GC.GetAllocatedBytesForCurrentThread();

        // Assert: one result-sized string and one element of overshoot, with room for copies; ten megabytes would
        // mean the loop kept building after the limit
        Assert.Contains("String too long (limit 100000)", arguments1.Message, StringComparison.Ordinal);
        Assert.Contains("String too long (limit 100000)", vector1.Message, StringComparison.Ordinal);
        Assert.True(afterArguments - before < 2_000_000, $"arguments={afterArguments - before}");
        Assert.True(afterVector - afterArguments < 2_000_000, $"vector={afterVector - afterArguments}");
    }

    /// <summary>
    ///     Validates that a step limit stops str while it formats a large vector.
    /// </summary>
    [Fact]
    public void ScadBuiltinString_Str_StepLimit_StopsDuringFormatting()
    {
        // Arrange
        var host = new ScadTestBuiltinHost { MaxStringLength = 100_000, MaxSteps = 200 };
        var vector = ScadValue.FromVector(Enumerable.Range(0, 1000).Select(i => N(i)).ToArray());

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => host.Call("str", vector));

        // Assert
        Assert.Contains("Evaluation step limit exceeded (limit 200)", ex.Message, StringComparison.Ordinal);
        Assert.True(host.Steps <= 208, $"steps={host.Steps}");
    }

    /// <summary>
    ///     Validates that str is culture-invariant.
    /// </summary>
    [Fact]
    public void ScadBuiltinString_Str_UnderGermanCulture_UsesInvariantNumbers()
    {
        // Arrange / Act
        var text = WithCulture("de-DE", () => new ScadTestBuiltinHost().Call("str", N(1.5), S(" "), N(0.00001)).AsString);

        // Assert
        Assert.Equal("1.5 1e-05", text);
    }

    /// <summary>
    ///     Validates the string length limit of str: exactly at the limit succeeds and one over fails.
    /// </summary>
    [Fact]
    public void ScadBuiltinString_Str_AtLimit_Succeeds_OneOver_Throws()
    {
        // Arrange
        var host = new ScadTestBuiltinHost { MaxStringLength = 6 };

        // Act
        var atLimit = host.Call("str", S("abc"), S("def"));
        var ex = Assert.Throws<ScadEvaluationException>(() => host.Call("str", S("abc"), S("defg")));
        var vectorEx = Assert.Throws<ScadEvaluationException>(() => host.Call("str", Nums(1, 2, 3, 4)));

        // Assert
        Assert.Equal("abcdef", atLimit.AsString);
        Assert.Contains("String too long (limit 6)", ex.Message, StringComparison.Ordinal);
        Assert.Contains("String too long (limit 6)", vectorEx.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates chr for numbers, vectors and several arguments.
    /// </summary>
    [Fact]
    public void ScadBuiltinString_Chr_ConvertsCodePoints()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.Equal(S("A"), host.Call("chr", N(65)));
        Assert.Equal(S("ABC"), host.Call("chr", Nums(65, 66, 67)));
        Assert.Equal(S("A\U0001F600"), host.Call("chr", N(65), N(0x1F600)));
        Assert.Equal(S("AB"), host.Call("chr", N(65), Nums(66)));
        Assert.Equal(S(string.Empty), host.Call("chr", Vec()));
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates that chr skips invalid code points and non-numbers with warnings.
    /// </summary>
    [Fact]
    public void ScadBuiltinString_Chr_InvalidInput_WarnsAndSkips()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var negative = host.Call("chr", N(-1));
        var surrogate = host.Call("chr", N(0xD800));
        var tooBig = host.Call("chr", N(0x110000));
        var fraction = host.Call("chr", N(65.5));
        var text = host.Call("chr", S("A"));
        var mixed = host.Call("chr", Vec(N(65), N(-3), S("x"), N(66)));

        // Assert
        Assert.Equal(S(string.Empty), negative);
        Assert.Equal(S(string.Empty), surrogate);
        Assert.Equal(S(string.Empty), tooBig);
        Assert.Equal(S(string.Empty), fraction);
        Assert.Equal(S(string.Empty), text);
        Assert.Equal(S("AB"), mixed);
        Assert.Equal(7, host.Warnings.Count);
    }

    /// <summary>
    ///     Validates the string length limit of chr.
    /// </summary>
    [Fact]
    public void ScadBuiltinString_Chr_AtLimit_Succeeds_OneOver_Throws()
    {
        // Arrange
        var host = new ScadTestBuiltinHost { MaxStringLength = 3 };

        // Act
        var atLimit = host.Call("chr", Nums(65, 66, 67));

        // Assert
        Assert.Equal(S("ABC"), atLimit);
        Assert.Throws<ScadEvaluationException>(() => host.Call("chr", Nums(65, 66, 67, 68)));
    }

    /// <summary>
    ///     Validates ord.
    /// </summary>
    [Fact]
    public void ScadBuiltinString_Ord_ReturnsFirstCodePoint()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.Equal(97, host.Call("ord", S("abc")).AsNumber);
        Assert.Equal(0x1F600, host.Call("ord", S("\U0001F600x")).AsNumber);
        Assert.Equal(ScadValue.Undef, host.Call("ord", S(string.Empty)));
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates that ord warns for a non-string.
    /// </summary>
    [Fact]
    public void ScadBuiltinString_Ord_NonString_ReturnsUndefWithWarning()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var result = host.Call("ord", N(65));

        // Assert
        Assert.Equal(ScadValue.Undef, result);
        Assert.Equal(["Function 'ord' expects a string, got a number"], host.Warnings);
    }

    /// <summary>
    ///     Validates the round trip of ord and chr for a supplementary-plane character.
    /// </summary>
    [Fact]
    public void ScadBuiltinString_ChrOrd_RoundTrip()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var code = host.Call("ord", S("\U0001F680"));
        var back = host.Call("chr", code);

        // Assert
        Assert.Equal(S("\U0001F680"), back);
    }
}
