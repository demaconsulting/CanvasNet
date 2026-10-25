using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests that the <c>search</c> built-in charges the preprocessing of its arguments before building anything and
///     reads its arguments in place instead of materializing per-scalar lists.
/// </summary>
public class ScadBuiltinSearchLimitTests
{
    /// <summary>
    ///     Measures the bytes allocated on the current thread by an action.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>The bytes allocated.</returns>
    private static long Allocated(Action action)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <summary>
    ///     Validates that two huge strings are charged by their sizes before any intermediate list is built.
    /// </summary>
    [Fact]
    public void ScadBuiltinSearchLimit_Search_LargeStrings_ChargesBeforeMaterializing()
    {
        // Arrange: 100,000 characters each; the budget of 1,000 steps cannot pay for reading them
        var match = S(new string('a', 100_000));
        var source = S(new string('b', 100_000));
        var host = new ScadTestBuiltinHost { MaxSteps = 1000, MaxListLength = 1_000_000 };
        ScadEvaluationException? thrown = null;

        // Act: the per-scalar lists of the former implementation took at least three megabytes
        var allocated = Allocated(() => thrown = Record.Exception(() => host.Call("search", match, source)) as ScadEvaluationException);

        // Assert
        Assert.NotNull(thrown);
        Assert.Contains("Evaluation step limit exceeded (limit 1000)", thrown.Message, StringComparison.Ordinal);
        Assert.True(allocated < 1_000_000, $"allocated={allocated}");
    }

    /// <summary>
    ///     Validates that a match string with more scalars than the list limit is refused before the result is allocated.
    /// </summary>
    [Fact]
    public void ScadBuiltinSearchLimit_Search_MatchLongerThanListLimit_ThrowsBeforeAllocating()
    {
        // Arrange: the result would have 100,000 entries, about two and a half megabytes
        var match = S(new string('a', 100_000));
        var host = new ScadTestBuiltinHost { MaxListLength = 1000 };
        ScadEvaluationException? thrown = null;

        // Act
        var allocated = Allocated(() => thrown = Record.Exception(() => host.Call("search", match, S("abc"))) as ScadEvaluationException);

        // Assert
        Assert.NotNull(thrown);
        Assert.Contains("List too long (limit 1000)", thrown.Message, StringComparison.Ordinal);
        Assert.True(allocated < 200_000, $"allocated={allocated}");
    }

    /// <summary>
    ///     Validates that searching a long ASCII string allocates nothing per scalar.
    /// </summary>
    [Fact]
    public void ScadBuiltinSearchLimit_Search_AsciiStrings_DoNotAllocatePerScalar()
    {
        // Arrange: a 100,000-character source and a ten-character match
        var source = S(new string('a', 99_999) + "z");
        var match = S(new string('a', 9) + "z");
        var host = new ScadTestBuiltinHost { MaxListLength = 1_000_000 };
        ScadValue result = default;

        // Act
        var allocated = Allocated(() => result = host.Call("search", match, source));

        // Assert: the nine 'a' entries are index 0, the 'z' entry is the last index
        Assert.Equal(10, result.AsVector.Count);
        Assert.Equal(N(99_999), result.AsVector[9]);
        Assert.True(allocated < 200_000, $"allocated={allocated}");
    }

    /// <summary>
    ///     Validates that collecting all matches stops as soon as the list limit is reached, instead of after the
    ///     whole list of indices exists.
    /// </summary>
    [Fact]
    public void ScadBuiltinSearchLimit_Search_AllMatches_ExceedingListLimit_StopsDuringCollection()
    {
        // Arrange: a million matches against a limit of 50
        var source = S(new string('a', 1_000_000));
        var host = new ScadTestBuiltinHost { MaxListLength = 50 };
        ScadEvaluationException? thrown = null;

        // Act
        var allocated = Allocated(() => thrown = Record.Exception(() => host.Call("search", S("a"), source, N(0))) as ScadEvaluationException);

        // Assert: collecting a million indices would allocate megabytes
        Assert.NotNull(thrown);
        Assert.Contains("List too long (limit 50)", thrown.Message, StringComparison.Ordinal);
        Assert.True(allocated < 100_000, $"allocated={allocated}");
    }

    /// <summary>
    ///     Validates that the results of the supported forms did not change with the in-place implementation.
    /// </summary>
    /// <param name="name">The case name.</param>
    [Theory]
    [InlineData("number-in-string")]
    [InlineData("empty-source")]
    [InlineData("empty-match")]
    [InlineData("nan-never-matches")]
    [InlineData("lone-surrogate")]
    [InlineData("pair-is-one-scalar")]
    [InlineData("mixed-source-and-rows")]
    [InlineData("string-element-vs-scalars")]
    [InlineData("all-from-rows-column")]
    [InlineData("bounded-count-over-strings")]
    public void ScadBuiltinSearchLimit_Search_VectorSource_RowsAndColumns_ResultsUnchanged(string name)
    {
        // Arrange
        var host = new ScadTestBuiltinHost();
        var (args, expected) = name switch
        {
            "number-in-string" => (new[] { N(1), S("111") }, Vec(Vec())),
            "empty-source" => (new[] { S("a"), S(string.Empty) }, Vec(Vec())),
            "empty-match" => (new[] { S(string.Empty), S("abc") }, Vec()),
            "nan-never-matches" => (new[] { N(double.NaN), Nums(double.NaN, 1) }, Vec(Vec())),
            "lone-surrogate" => (new[] { S("\uD83D"), S("a\uD83Db") }, Vec(N(1))),
            "pair-is-one-scalar" => (new[] { S("a"), S("\U0001F600a"), N(0) }, Vec(Nums(1))),
            "mixed-source-and-rows" => (new[] { S("b"), Vec(Vec(S("a")), Vec(S("b")), S("b")), N(0) }, Vec(Nums(1, 2))),
            "string-element-vs-scalars" => (new[] { Vec(S("ab"), S("b")), S("abc") }, Vec(Vec(), N(1))),
            "all-from-rows-column" => (new[] { N(7), Vec(Nums(1, 7), Nums(7, 2), Nums(3, 7), Nums(9)), N(0), N(1) }, Vec(Nums(0, 2))),
            _ => (new[] { S("ab"), S("abab"), N(2) }, Vec(Nums(0, 2), Nums(1, 3))),
        };

        // Act
        var result = host.Call("search", args);

        // Assert
        Assert.Equal(expected, result);
        Assert.Empty(host.UnsupportedReports);
    }

    /// <summary>
    ///     Validates that a search polls for cancellation at a fixed count of comparisons, so a long scan is
    ///     interruptible and the number of polls is exactly the comparisons divided by the interval.
    /// </summary>
    /// <param name="form">The search form: a vector source, a string source, or several match elements.</param>
    [Theory]
    [InlineData("vector-source")]
    [InlineData("string-source")]
    [InlineData("several-match-elements")]
    [InlineData("all-matches")]
    public void ScadBuiltinSearchLimit_Search_Scans_PollAtFixedComparisonIntervals(string form)
    {
        // Arrange: each form examines a known number of candidates in total
        var (args, comparisons) = form switch
        {
            "vector-source" => (new[] { N(7), Nums([.. Enumerable.Repeat(1.0, 200_000)]) }, 200_000L),
            "string-source" => (new[] { S("z"), S(new string('a', 200_000)) }, 200_000L),
            "several-match-elements" => (new[] { Nums(7, 8, 9, 10), Nums([.. Enumerable.Repeat(1.0, 40_000)]) }, 160_000L),
            _ => (new[] { N(1), Nums([.. Enumerable.Repeat(1.0, 100_000)]), N(0) }, 100_000L),
        };
        var host = new ScadTestBuiltinHost { MaxListLength = 1_000_000 };

        // Act
        host.Call("search", args);

        // Assert: the interval is counted, not timed, so the assertion is exact
        Assert.Equal(comparisons / ScadBuiltinFunctions.SearchPollInterval, host.Polls);
        Assert.Equal(65_536, ScadBuiltinFunctions.SearchPollInterval);
    }

    /// <summary>
    ///     Validates that a poll that throws stops a long search, and that a short search never polls.
    /// </summary>
    [Fact]
    public void ScadBuiltinSearchLimit_Search_CancellationDuringScan_StopsTheSearch()
    {
        // Arrange: 200,000 candidates would poll three times; the second poll cancels
        var source = Nums([.. Enumerable.Repeat(1.0, 200_000)]);
        var canceling = new ScadTestBuiltinHost { MaxListLength = 1_000_000, CancelAfterPolls = 2 };
        var quiet = new ScadTestBuiltinHost { MaxListLength = 1_000_000 };

        // Act
        quiet.Call("search", N(7), Nums(1, 2, 3));

        // Assert
        Assert.Throws<OperationCanceledException>(() => canceling.Call("search", N(7), source));
        Assert.Equal(2, canceling.Polls);
        Assert.Equal(0, quiet.Polls);
    }

    /// <summary>
    ///     Validates that the preprocessing of both arguments is charged linearly, in addition to the comparison work.
    /// </summary>
    [Fact]
    public void ScadBuiltinSearchLimit_Search_ChargeIncludesLinearPreprocessing()
    {
        // Arrange: a 300-character match string and a 700-element source vector
        var host = new ScadTestBuiltinHost { MaxListLength = 10_000 };
        var source = ScadValue.FromVector(Enumerable.Repeat(N(1), 700).ToArray());

        // Act
        host.Call("search", S(new string('a', 300)), source);

        // Assert: the up-front charge is 1 + 300 + 700 and the comparisons 300 * 700
        Assert.Equal(1 + 300 + 700 + (300 * 700), host.Steps);
    }
}
