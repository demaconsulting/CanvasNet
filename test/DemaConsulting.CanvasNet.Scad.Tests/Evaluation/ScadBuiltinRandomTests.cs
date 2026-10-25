using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

// cspell:ignore addb bbbf dcdaf ededu

/// <summary>
///     Unit tests for <see cref="ScadRandom"/> and the <c>rands</c> built-in function.
/// </summary>
public class ScadBuiltinRandomTests
{
    /// <summary>
    ///     Validates the SplitMix64 generator against golden outputs computed independently from the published
    ///     algorithm, so any platform or implementation drift fails this test.
    /// </summary>
    [Fact]
    public void ScadRandom_NextUInt64_MatchesGoldenSequence()
    {
        // Arrange: state zero, the reference vector of the algorithm
        var zero = new ScadRandom(0);
        var seeded = ScadRandom.FromSeed(1.0);

        // Act
        ulong[] zeroOutputs = [zero.NextUInt64(), zero.NextUInt64(), zero.NextUInt64(), zero.NextUInt64(), zero.NextUInt64()];
        ulong[] seededOutputs = [seeded.NextUInt64(), seeded.NextUInt64(), seeded.NextUInt64()];

        // Assert
        Assert.Equal(
            [0xe220a8397b1dcdafUL, 0x6e789e6aa1b965f4UL, 0x06c45d188009454fUL, 0xf88bb8a8724c81ecUL, 0x1b39896a51a8749bUL],
            zeroOutputs);
        Assert.Equal([0x88d7c35bbbf3ededuL, 0xa02addb3dc67ca4cuL, 0xef4038ca1d6d2e73uL], seededOutputs);
    }

    /// <summary>
    ///     Validates the mapping of outputs to doubles in [0, 1).
    /// </summary>
    [Fact]
    public void ScadRandom_NextDouble_MatchesGoldenValues()
    {
        // Arrange
        var random = new ScadRandom(0);

        // Act
        double[] values = [random.NextDouble(), random.NextDouble(), random.NextDouble()];

        // Assert: exact, because the mapping is an exact power-of-two scaling
        Assert.Equal([0.8833108082136426, 0.43152799704850997, 0.026433771592597743], values);
        Assert.All(values, v => Assert.InRange(v, 0.0, 0.999999999999999));
    }

    /// <summary>
    ///     Validates that the default state of the evaluation-wide generator is pinned.
    /// </summary>
    [Fact]
    public void ScadRandom_DefaultState_MatchesGoldenSequence()
    {
        // Arrange
        var random = new ScadRandom(ScadRandom.DefaultState);

        // Act / Assert
        Assert.Equal(0xb78c9f662a30a4f6UL, random.NextUInt64());
        Assert.Equal(0x06e3259d00d87364UL, random.NextUInt64());
    }

    /// <summary>
    ///     Validates rands with a seed: golden numbers, repeatability and that each call starts afresh.
    /// </summary>
    [Fact]
    public void ScadBuiltinRandom_Rands_WithSeed_IsDeterministicAndRestartsEachCall()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var first = host.Call("rands", N(0), N(1), N(3), N(1));
        var second = host.Call("rands", N(0), N(1), N(3), N(1));
        var other = host.Call("rands", N(0), N(1), N(3), N(42));

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(Nums(0.5345422839126993, 0.6256540836442631, 0.9345736974186606), first);
        Assert.Equal(Nums(0.6776231762504039, 0.019940763566203334, 0.36827667810469633), other);
    }

    /// <summary>
    ///     Validates scaling to the requested range, including a reversed range.
    /// </summary>
    [Fact]
    public void ScadBuiltinRandom_Rands_ScalesToRange()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var values = host.Call("rands", N(10), N(20), N(1000), N(7)).AsVector;
        var reversed = host.Call("rands", N(5), N(-5), N(100), N(7)).AsVector;

        // Assert
        for (var i = 0; i < values.Count; i++)
        {
            Assert.InRange(values[i].AsNumber, 10.0, 20.0);
        }

        for (var i = 0; i < reversed.Count; i++)
        {
            Assert.InRange(reversed[i].AsNumber, -5.0, 5.0);
        }

        Assert.Equal(1000, values.Count);
        Assert.Equal(10 + (10 * 0.6776231762504039), host.Call("rands", N(10), N(20), N(1), N(42)).AsVector[0].AsNumber, 12);
    }

    /// <summary>
    ///     Validates that unseeded rands is deterministic per evaluation: the stream advances call after call and a
    ///     fresh host reproduces the same stream.
    /// </summary>
    [Fact]
    public void ScadBuiltinRandom_Rands_WithoutSeed_AdvancesDeterministicStream()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();
        var again = new ScadTestBuiltinHost();

        // Act
        var first = host.Call("rands", N(0), N(1), N(2));
        var second = host.Call("rands", N(0), N(1), N(2));
        var replayFirst = again.Call("rands", N(0), N(1), N(2));
        var undefSeed = again.Call("rands", N(0), N(1), N(2), ScadValue.Undef);

        // Assert
        Assert.NotEqual(first, second);
        Assert.Equal(first, replayFirst);
        Assert.Equal(second, undefSeed);
        Assert.Equal(Nums(0.7169894813943665, 0.026903487044224184), first);
    }

    /// <summary>
    ///     Validates the named parameters of rands.
    /// </summary>
    [Fact]
    public void ScadBuiltinRandom_Rands_NamedArguments_AreAccepted()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var named = host.CallNamed("rands", ("seed_value", N(1)), ("value_count", N(3)), ("max_value", N(1)), ("min_value", N(0)));
        var positional = host.Call("rands", N(0), N(1), N(3), N(1));

        // Assert
        Assert.Equal(positional, named);
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates that the seed parameter is named <c>seed_value</c>: a named seeded call is deterministic and equals
    ///     the positional one, while the old name <c>seed</c> is an unknown argument.
    /// </summary>
    [Fact]
    public void ScadBuiltinRandom_Rands_SeedValueName_IsDeterministicAndSeedIsUnknown()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();
        var other = new ScadTestBuiltinHost();

        // Act
        var first = host.CallNamed("rands", ("min_value", N(0)), ("max_value", N(10)), ("value_count", N(4)), ("seed_value", N(42)));
        var second = other.CallNamed("rands", ("seed_value", N(42)), ("min_value", N(0)), ("max_value", N(10)), ("value_count", N(4)));
        var positional = other.Call("rands", N(0), N(10), N(4), N(42));
        var wrongName = host.CallNamed("rands", ("min_value", N(0)), ("max_value", N(10)), ("value_count", N(4)), ("seed", N(42)));

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(first, positional);
        Assert.Empty(other.Warnings);
        Assert.Equal("Ignoring unknown argument 'seed' of function 'rands'", Assert.Single(host.Warnings));
        Assert.Equal(4, wrongName.AsVector.Count);
    }
    /// <summary>
    ///     Validates that a fractional count is truncated and that a zero count gives an empty vector.
    /// </summary>
    [Fact]
    public void ScadBuiltinRandom_Rands_Count_IsTruncated()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.Equal(2, host.Call("rands", N(0), N(1), N(2.9), N(1)).AsVector.Count);
        Assert.Equal(0, host.Call("rands", N(0), N(1), N(0), N(1)).AsVector.Count);
    }

    /// <summary>
    ///     Validates the invalid forms of rands.
    /// </summary>
    [Fact]
    public void ScadBuiltinRandom_Rands_InvalidArguments_ReturnUndefWithWarning()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var results = new[]
        {
            host.Call("rands", S("a"), N(1), N(1)),
            host.Call("rands", N(0), N(1), N(-1)),
            host.Call("rands", N(0), N(1), N(double.NaN)),
            host.Call("rands", N(0), N(1), N(1), S("seed")),
            host.Call("rands", N(0), N(1), Nums(1)),
        };

        // Assert
        Assert.All(results, r => Assert.Equal(ScadValue.Undef, r));
        Assert.Equal(5, host.Warnings.Count);
    }

    /// <summary>
    ///     Validates the list length limit of rands: exactly at the limit succeeds, one over (and an absurd count) fails.
    /// </summary>
    [Fact]
    public void ScadBuiltinRandom_Rands_AtLimit_Succeeds_OneOver_Throws()
    {
        // Arrange
        var host = new ScadTestBuiltinHost { MaxListLength = 10 };

        // Act
        var atLimit = host.Call("rands", N(0), N(1), N(10), N(1));

        // Assert
        Assert.Equal(10, atLimit.AsVector.Count);
        Assert.Throws<ScadEvaluationException>(() => host.Call("rands", N(0), N(1), N(11), N(1)));
        Assert.Throws<ScadEvaluationException>(() => host.Call("rands", N(0), N(1), N(1e300), N(1)));
        Assert.Throws<ScadEvaluationException>(() => host.Call("rands", N(0), N(1), N(double.PositiveInfinity), N(1)));
    }
}
