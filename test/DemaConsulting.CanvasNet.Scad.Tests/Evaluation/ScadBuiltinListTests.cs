using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for the list functions of <see cref="ScadBuiltinFunctions"/>.
/// </summary>
public class ScadBuiltinListTests
{
    /// <summary>
    ///     Validates len of vectors and strings, counting Unicode scalars.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Len_VectorsAndStrings_ReturnsCount()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.Equal(3, host.Call("len", Nums(1, 2, 3)).AsNumber);
        Assert.Equal(0, host.Call("len", Vec()).AsNumber);
        Assert.Equal(2, host.Call("len", Vec(Nums(1, 2, 3), Vec())).AsNumber);
        Assert.Equal(5, host.Call("len", S("hello")).AsNumber);
        Assert.Equal(0, host.Call("len", S(string.Empty)).AsNumber);
        Assert.Equal(3, host.Call("len", S("a\U0001F600b")).AsNumber);
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates that len of other kinds is undef with a warning, including a range.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Len_OtherKinds_ReturnsUndefWithWarning()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var number = host.Call("len", N(5));
        var range = host.Call("len", ScadValue.FromRange(0, 1, 3));

        // Assert
        Assert.Equal(ScadValue.Undef, number);
        Assert.Equal(ScadValue.Undef, range);
        Assert.Equal(
            ["Function 'len' expects a vector or a string, got a number", "Function 'len' expects a vector or a string, got a range"],
            host.Warnings);
    }

    /// <summary>
    ///     Validates concat: flattening exactly one level and appending non-vectors.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Concat_FlattensOneLevel()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var result = host.Call("concat", Nums(1, 2), N(3), Vec(Nums(4)), S("s"), Vec());

        // Assert
        Assert.Equal(Vec(N(1), N(2), N(3), Nums(4), S("s")), result);
        Assert.Equal(Vec(), host.Call("concat"));
    }

    /// <summary>
    ///     Validates the list length limit of concat: exactly at the limit succeeds, one over fails.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Concat_AtLimit_Succeeds_OneOver_Throws()
    {
        // Arrange
        var host = new ScadTestBuiltinHost { MaxListLength = 5 };

        // Act
        var atLimit = host.Call("concat", Nums(1, 2, 3), Nums(4, 5));
        var ex = Assert.Throws<ScadEvaluationException>(() => host.Call("concat", Nums(1, 2, 3), Nums(4, 5), N(6)));

        // Assert
        Assert.Equal(5, atLimit.AsVector.Count);
        Assert.Contains("List too long (limit 5)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates norm.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Norm_ReturnsEuclideanLength()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.Equal(5, host.Call("norm", Nums(3, 4)).AsNumber);
        Assert.Equal(0, host.Call("norm", Vec()).AsNumber);
        Assert.Equal(Math.Sqrt(14), host.Call("norm", Nums(1, 2, 3)).AsNumber, 12);
        Assert.Equal(ScadValue.Undef, host.Call("norm", N(3)));
        Assert.Equal(ScadValue.Undef, host.Call("norm", Vec(N(1), S("a"))));
        Assert.Equal(2, host.Warnings.Count);
    }

    /// <summary>
    ///     Validates cross for 3-vectors and 2-vectors, and the invalid forms.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Cross_ThreeAndTwoDimensions()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.Equal(Nums(0, 0, 1), host.Call("cross", Nums(1, 0, 0), Nums(0, 1, 0)));
        Assert.Equal(Nums(0, 0, -1), host.Call("cross", Nums(0, 1, 0), Nums(1, 0, 0)));
        Assert.Equal(Nums(-3, 6, -3), host.Call("cross", Nums(1, 2, 3), Nums(4, 5, 6)));
        Assert.Equal(N(1), host.Call("cross", Nums(1, 0), Nums(0, 1)));
        Assert.Equal(N(-2), host.Call("cross", Nums(1, 2), Nums(3, 4)));
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates that cross warns for the wrong dimensions or kinds.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Cross_InvalidOperands_ReturnsUndefWithWarning()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var results = new[]
        {
            host.Call("cross", Nums(1, 2, 3), Nums(1, 2)),
            host.Call("cross", Nums(1), Nums(1)),
            host.Call("cross", Nums(1, 2, 3, 4), Nums(1, 2, 3, 4)),
            host.Call("cross", N(1), Nums(1, 2)),
            host.Call("cross", Nums(1, 2), Vec(N(1), S("a"))),
        };

        // Assert
        Assert.All(results, r => Assert.Equal(ScadValue.Undef, r));
        Assert.Equal(5, host.Warnings.Count);
    }

    /// <summary>
    ///     Validates lookup interpolation, clamping, exact keys and unsorted tables.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Lookup_InterpolatesAndClamps()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();
        var table = Vec(Nums(0, 0), Nums(10, 100), Nums(20, 50));

        // Act / Assert
        Assert.Equal(50, host.Call("lookup", N(5), table).AsNumber, 12);
        Assert.Equal(100, host.Call("lookup", N(10), table).AsNumber, 12);
        Assert.Equal(75, host.Call("lookup", N(15), table).AsNumber, 12);
        Assert.Equal(0, host.Call("lookup", N(-5), table).AsNumber);
        Assert.Equal(50, host.Call("lookup", N(99), table).AsNumber);
        Assert.Equal(0, host.Call("lookup", N(0), table).AsNumber);
        Assert.Equal(50, host.Call("lookup", N(20), table).AsNumber);
        Assert.Equal(0, host.Call("lookup", N(double.NegativeInfinity), table).AsNumber);
        Assert.Equal(75, host.CallNamed("lookup", ("table", table), ("key", N(15))).AsNumber, 12);
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates that lookup does not need a sorted table and that the first of equal keys wins.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Lookup_UnsortedTable_StillFindsNeighbours()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();
        var unsorted = Vec(Nums(20, 50), Nums(0, 0), Nums(10, 100));
        var duplicate = Vec(Nums(1, 5), Nums(1, 9));

        // Act / Assert
        Assert.Equal(75, host.Call("lookup", N(15), unsorted).AsNumber, 12);
        Assert.Equal(5, host.Call("lookup", N(1), duplicate).AsNumber);
    }

    /// <summary>
    ///     Validates that lookup skips invalid rows and rejects bad keys and tables.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Lookup_InvalidInput_SkipsRowsOrWarns()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();
        var mixed = Vec(N(1), Nums(1), Vec(S("k"), N(1)), Vec(N(1), S("v")), Nums(0, 10), Nums(10, 20, 99));

        // Act
        var skipped = host.Call("lookup", N(5), mixed);
        var badKey = host.Call("lookup", S("a"), mixed);
        var nanKey = host.Call("lookup", N(double.NaN), mixed);
        var badTable = host.Call("lookup", N(1), N(5));
        var noPairs = host.Call("lookup", N(1), Vec(N(1), Nums(1)));
        var empty = host.Call("lookup", N(1), Vec());

        // Assert
        Assert.Equal(15, skipped.AsNumber, 12);
        Assert.All(new[] { badKey, nanKey, badTable, noPairs, empty }, r => Assert.Equal(ScadValue.Undef, r));
        Assert.Equal(5, host.Warnings.Count);
    }

    /// <summary>
    ///     Validates search for numbers, strings and vectors against vectors with the default single result.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Search_DefaultReturnsFirstIndexOrEmpty()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();
        var source = Nums(5, 7, 5, 9);

        // Act / Assert
        Assert.Equal(Vec(N(1)), host.Call("search", N(7), source));
        Assert.Equal(Vec(N(0)), host.Call("search", N(5), source));
        Assert.Equal(Vec(Vec()), host.Call("search", N(8), source));
        Assert.Equal(Vec(N(3), N(0), Vec()), host.Call("search", Nums(9, 5, 4), source));
        Assert.Equal(Vec(), host.Call("search", Vec(), source));
        Assert.Empty(host.UnsupportedReports);
    }

    /// <summary>
    ///     Validates search in strings, per Unicode scalar.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Search_Strings_MatchesScalars()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.Equal(Vec(N(1), N(2), Vec()), host.Call("search", S("abz"), S("xabc")));
        Assert.Equal(Vec(N(1)), host.Call("search", S("\U0001F600"), S("a\U0001F600b")));
        Assert.Equal(Vec(N(2)), host.Call("search", Vec(S("b")), Vec(S("a"), S("x"), S("b"))));
        Assert.Equal(Vec(Vec()), host.Call("search", Vec(S("ab")), S("abc")));
        Assert.Equal(Vec(N(1), N(0)), host.Call("search", Vec(S("q"), N(1)), Vec(N(1), S("q"))));
    }

    /// <summary>
    ///     Validates search against rows (vectors of vectors) with a column index.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Search_Rows_UsesColumn()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();
        var rows = Vec(Vec(S("a"), N(10)), Vec(S("b"), N(20)), Vec(S("c"), N(10)), Vec(S("short")));

        // Act / Assert
        Assert.Equal(Vec(N(1)), host.Call("search", S("b"), rows));
        Assert.Equal(Vec(N(0)), host.Call("search", N(10), rows, N(1), N(1)));
        Assert.Equal(Vec(Nums(0, 2)), host.Call("search", N(10), rows, N(0), N(1)));
        Assert.Equal(Vec(Vec()), host.Call("search", N(10), rows, N(1), N(5)));
        Assert.Equal(Vec(N(3)), host.CallNamed("search", ("match_value", Vec(S("short"))), ("search_vector", rows)));
    }

    /// <summary>
    ///     Validates the returns-per-match option: all matches, a bounded number, and a gap that selects the default.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Search_ReturnsPerMatch_AllOrBounded()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();
        var source = Nums(1, 2, 1, 2, 1);

        // Act / Assert
        Assert.Equal(Vec(Nums(0, 2, 4)), host.Call("search", N(1), source, N(0)));
        Assert.Equal(Vec(Nums(0, 2)), host.Call("search", N(1), source, N(2)));
        Assert.Equal(Vec(Nums(0, 2, 4)), host.Call("search", N(1), source, N(99)));
        Assert.Equal(Vec(Vec()), host.Call("search", N(7), source, N(0)));
        Assert.Equal(Vec(N(0)), host.Call("search", N(1), source, ScadValue.Undef));
        Assert.Equal(Vec(Nums(1, 3)), host.CallNamed("search", ("match_value", N(2)), ("search_vector", source), ("num_returns_per_match", N(0))));
    }

    /// <summary>
    ///     Validates that unsupported search forms are reported and yield undef.
    /// </summary>
    /// <param name="name">The case name.</param>
    [Theory]
    [InlineData("match-vector-element")]
    [InlineData("match-bool")]
    [InlineData("match-undef")]
    [InlineData("match-range")]
    [InlineData("source-number")]
    [InlineData("source-undef")]
    [InlineData("returns-negative")]
    [InlineData("returns-fraction")]
    [InlineData("returns-string")]
    [InlineData("column-negative")]
    [InlineData("column-nan")]
    public void ScadBuiltinList_Search_UnsupportedForms_ReportFeature(string name)
    {
        // Arrange
        var host = new ScadTestBuiltinHost();
        ScadValue[] args = name switch
        {
            "match-vector-element" => [Vec(Nums(1)), Vec(Nums(1))],
            "match-bool" => [ScadValue.True, Nums(1)],
            "match-undef" => [ScadValue.Undef, Nums(1)],
            "match-range" => [ScadValue.FromRange(0, 1, 2), Nums(1)],
            "source-number" => [N(1), N(1)],
            "source-undef" => [N(1), ScadValue.Undef],
            "returns-negative" => [N(1), Nums(1), N(-1)],
            "returns-fraction" => [N(1), Nums(1), N(1.5)],
            "returns-string" => [N(1), Nums(1), S("1")],
            "column-negative" => [N(1), Nums(1), N(1), N(-1)],
            _ => [N(1), Nums(1), N(1), N(double.NaN)],
        };

        // Act
        var result = host.Call("search", args);

        // Assert
        Assert.Equal(ScadValue.Undef, result);
        Assert.Equal("scad-search-form", Assert.Single(host.UnsupportedReports).Feature);
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates that a search whose work or result is too large fails closed.
    /// </summary>
    [Fact]
    public void ScadBuiltinList_Search_TooMuchWorkOrResult_Throws()
    {
        // Arrange
        var work = new ScadTestBuiltinHost { MaxSteps = 50 };
        var result = new ScadTestBuiltinHost { MaxListLength = 5 };
        var source = Nums(1, 1, 1, 1);

        // Act / Assert: 10 x 10 comparisons exceed 50 steps; collecting 8 indices exceeds a list limit of 5
        Assert.Throws<ScadEvaluationException>(() => work.Call("search", Nums(1, 2, 3, 4, 5, 6, 7, 8, 9, 10), Nums(1, 2, 3, 4, 5, 6, 7, 8, 9, 10)));
        Assert.Throws<ScadEvaluationException>(() => result.Call("search", Nums(1, 1), source, N(0)));
    }
}
