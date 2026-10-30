// Portions derived from Vega (BSD-3-Clause) and d3-array (ISC, Mike Bostock); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     The aggregate operations of Vega-Lite (<c>count</c>, <c>sum</c>, <c>mean</c>, ...): the names the specification may use.
/// </summary>
/// <remarks>
///     <c>average</c> is not a member: it is another name of <see cref="Mean"/>. The members that take a value from a field are
///     all but <see cref="Count"/>; <see cref="ArgMin"/> and <see cref="ArgMax"/> return the row that holds the extreme value
///     rather than a number, and <see cref="Exponential"/> and <see cref="ExponentialB"/> take a weighting parameter.
/// </remarks>
internal enum AggregateOp
{
    /// <summary>The number of rows.</summary>
    Count,

    /// <summary>The number of values that are neither null, empty text nor NaN.</summary>
    Valid,

    /// <summary>The number of null and empty-text values.</summary>
    Missing,

    /// <summary>The number of distinct values.</summary>
    Distinct,

    /// <summary>The sum of the valid values.</summary>
    Sum,

    /// <summary>The product of the valid values.</summary>
    Product,

    /// <summary>The arithmetic mean of the valid values (also written <c>average</c>).</summary>
    Mean,

    /// <summary>The sample variance.</summary>
    Variance,

    /// <summary>The population variance.</summary>
    VarianceP,

    /// <summary>The sample standard deviation.</summary>
    Stdev,

    /// <summary>The population standard deviation.</summary>
    StdevP,

    /// <summary>The standard error of the mean.</summary>
    Stderr,

    /// <summary>The median.</summary>
    Median,

    /// <summary>The first quartile.</summary>
    Q1,

    /// <summary>The third quartile.</summary>
    Q3,

    /// <summary>The lower bound of the 95% bootstrap confidence interval of the mean.</summary>
    Ci0,

    /// <summary>The upper bound of the 95% bootstrap confidence interval of the mean.</summary>
    Ci1,

    /// <summary>The smallest value.</summary>
    Min,

    /// <summary>The largest value.</summary>
    Max,

    /// <summary>The row that holds the smallest value of another field.</summary>
    ArgMin,

    /// <summary>The row that holds the largest value of another field.</summary>
    ArgMax,

    /// <summary>The array of the values.</summary>
    Values,

    /// <summary>The exponentially weighted average (weight parameter <c>r</c>).</summary>
    Exponential,

    /// <summary>The exponentially weighted average without the normalization (weight parameter <c>r</c>).</summary>
    ExponentialB,
}

/// <summary>
///     The operations that only the <c>window</c> transform has: the ranking, offset and positional operations.
/// </summary>
/// <remarks>
///     A window transform also accepts every <see cref="AggregateOp"/>, which then aggregates the frame of the row.
/// </remarks>
internal enum WindowOp
{
    /// <summary>The 1-based position of the row in its sorted partition.</summary>
    RowNumber,

    /// <summary>The rank of the row; peers share a rank and leave a gap after them.</summary>
    Rank,

    /// <summary>The rank of the row without gaps.</summary>
    DenseRank,

    /// <summary>The relative rank of the row, from 0 to 1.</summary>
    PercentRank,

    /// <summary>The cumulative distribution of the row, up to and including its peers.</summary>
    CumeDist,

    /// <summary>The number of the tile (1..n) the row falls in when the partition is cut into <c>param</c> tiles.</summary>
    Ntile,

    /// <summary>The value of a field <c>param</c> rows before the row (default 1).</summary>
    Lag,

    /// <summary>The value of a field <c>param</c> rows after the row (default 1).</summary>
    Lead,

    /// <summary>The value of a field in the first row of the frame.</summary>
    FirstValue,

    /// <summary>The value of a field in the last row of the frame.</summary>
    LastValue,

    /// <summary>The value of a field in the <c>param</c>-th row of the frame.</summary>
    NthValue,

    /// <summary>The last non-null value of a field up to the row.</summary>
    PrevValue,

    /// <summary>The first non-null value of a field from the row on.</summary>
    NextValue,
}

/// <summary>
///     The registry of the operation names of the aggregating transforms: the names a specification may use, how each is
///     spelled, and which of them need a field or a parameter.
/// </summary>
/// <remarks>
///     <para>
///         The registry is the single place that decides whether an operation name is valid. The specification parser uses it
///         to reject an unknown name with a located error, and the encoding rules use it to tell the operations that read a field
///         from those that do not. The arithmetic of the operations is in <see cref="MeasureCell"/> (the running state of one
///         operation); this class also holds the quantile function that the order statistics share.
///     </para>
///     <para>
///         The names are those of Vega-Lite 6 (the aggregate operations) and Vega (the window operations). Names are
///         case-sensitive (compared as ordinal text).
///     </para>
/// </remarks>
internal static class AggregateMeasures
{
    /// <summary>The aggregate operations by specification name, including the alias <c>average</c>.</summary>
    private static readonly Dictionary<string, AggregateOp> AggregateNames = new(StringComparer.Ordinal)
    {
        ["count"] = AggregateOp.Count,
        ["valid"] = AggregateOp.Valid,
        ["missing"] = AggregateOp.Missing,
        ["distinct"] = AggregateOp.Distinct,
        ["sum"] = AggregateOp.Sum,
        ["product"] = AggregateOp.Product,
        ["mean"] = AggregateOp.Mean,
        ["average"] = AggregateOp.Mean,
        ["variance"] = AggregateOp.Variance,
        ["variancep"] = AggregateOp.VarianceP,
        ["stdev"] = AggregateOp.Stdev,
        ["stdevp"] = AggregateOp.StdevP,
        ["stderr"] = AggregateOp.Stderr,
        ["median"] = AggregateOp.Median,
        ["q1"] = AggregateOp.Q1,
        ["q3"] = AggregateOp.Q3,
        ["ci0"] = AggregateOp.Ci0,
        ["ci1"] = AggregateOp.Ci1,
        ["min"] = AggregateOp.Min,
        ["max"] = AggregateOp.Max,
        ["argmin"] = AggregateOp.ArgMin,
        ["argmax"] = AggregateOp.ArgMax,
        ["values"] = AggregateOp.Values,
        ["exponential"] = AggregateOp.Exponential,
        ["exponentialb"] = AggregateOp.ExponentialB,
    };

    /// <summary>The window-only operations by specification name.</summary>
    private static readonly Dictionary<string, WindowOp> WindowNames = new(StringComparer.Ordinal)
    {
        ["row_number"] = WindowOp.RowNumber,
        ["rank"] = WindowOp.Rank,
        ["dense_rank"] = WindowOp.DenseRank,
        ["percent_rank"] = WindowOp.PercentRank,
        ["cume_dist"] = WindowOp.CumeDist,
        ["ntile"] = WindowOp.Ntile,
        ["lag"] = WindowOp.Lag,
        ["lead"] = WindowOp.Lead,
        ["first_value"] = WindowOp.FirstValue,
        ["last_value"] = WindowOp.LastValue,
        ["nth_value"] = WindowOp.NthValue,
        ["prev_value"] = WindowOp.PrevValue,
        ["next_value"] = WindowOp.NextValue,
    };

    /// <summary>
    ///     Gets the name of every aggregate operation, in a stable order (the alias <c>average</c> included).
    /// </summary>
    /// <returns>The names.</returns>
    public static IReadOnlyList<string> GetAggregateNames() => [.. AggregateNames.Keys];

    /// <summary>
    ///     Gets the name of every window-only operation, in a stable order.
    /// </summary>
    /// <returns>The names.</returns>
    public static IReadOnlyList<string> GetWindowNames() => [.. WindowNames.Keys];

    /// <summary>
    ///     Looks up an aggregate operation by its specification name.
    /// </summary>
    /// <param name="name">The name (<c>average</c> gives <see cref="AggregateOp.Mean"/>); case-sensitive.</param>
    /// <param name="op">Receives the operation.</param>
    /// <returns><see langword="true"/> when the name is an aggregate operation.</returns>
    public static bool TryParse(string? name, out AggregateOp op)
    {
        op = default;
        return name is not null && AggregateNames.TryGetValue(name, out op);
    }

    /// <summary>
    ///     Looks up a window-only operation by its specification name.
    /// </summary>
    /// <param name="name">The name; case-sensitive.</param>
    /// <param name="op">Receives the operation.</param>
    /// <returns><see langword="true"/> when the name is a window-only operation (an aggregate operation is not).</returns>
    public static bool TryParseWindow(string? name, out WindowOp op)
    {
        op = default;
        return name is not null && WindowNames.TryGetValue(name, out op);
    }

    /// <summary>
    ///     Gets the canonical specification name of an aggregate operation.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns>The name (<c>mean</c> for <see cref="AggregateOp.Mean"/>, never <c>average</c>).</returns>
    public static string GetName(AggregateOp op) => op switch
    {
        AggregateOp.Count => "count",
        AggregateOp.Valid => "valid",
        AggregateOp.Missing => "missing",
        AggregateOp.Distinct => "distinct",
        AggregateOp.Sum => "sum",
        AggregateOp.Product => "product",
        AggregateOp.Mean => "mean",
        AggregateOp.Variance => "variance",
        AggregateOp.VarianceP => "variancep",
        AggregateOp.Stdev => "stdev",
        AggregateOp.StdevP => "stdevp",
        AggregateOp.Stderr => "stderr",
        AggregateOp.Median => "median",
        AggregateOp.Q1 => "q1",
        AggregateOp.Q3 => "q3",
        AggregateOp.Ci0 => "ci0",
        AggregateOp.Ci1 => "ci1",
        AggregateOp.Min => "min",
        AggregateOp.Max => "max",
        AggregateOp.ArgMin => "argmin",
        AggregateOp.ArgMax => "argmax",
        AggregateOp.Values => "values",
        AggregateOp.Exponential => "exponential",
        _ => "exponentialb",
    };

    /// <summary>
    ///     Gets the canonical specification name of a window-only operation.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns>The name, such as <c>row_number</c>.</returns>
    public static string GetName(WindowOp op) => op switch
    {
        WindowOp.RowNumber => "row_number",
        WindowOp.Rank => "rank",
        WindowOp.DenseRank => "dense_rank",
        WindowOp.PercentRank => "percent_rank",
        WindowOp.CumeDist => "cume_dist",
        WindowOp.Ntile => "ntile",
        WindowOp.Lag => "lag",
        WindowOp.Lead => "lead",
        WindowOp.FirstValue => "first_value",
        WindowOp.LastValue => "last_value",
        WindowOp.NthValue => "nth_value",
        WindowOp.PrevValue => "prev_value",
        _ => "next_value",
    };

    /// <summary>
    ///     Determines whether an aggregate operation reads a field (every operation except <see cref="AggregateOp.Count"/>).
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns><see langword="true"/> when the operation needs a <c>field</c>.</returns>
    public static bool RequiresField(AggregateOp op) => op != AggregateOp.Count;

    /// <summary>
    ///     Determines whether a window-only operation reads a field (the offset and positional operations do, the ranking ones do not).
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns><see langword="true"/> when the operation needs a <c>field</c>.</returns>
    public static bool RequiresField(WindowOp op) =>
        op is WindowOp.Lag or WindowOp.Lead or WindowOp.FirstValue or WindowOp.LastValue or WindowOp.NthValue or WindowOp.PrevValue or WindowOp.NextValue;

    /// <summary>
    ///     Determines whether an operation returns the row that holds an extreme value (<c>argmin</c> and <c>argmax</c>).
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns><see langword="true"/> for <see cref="AggregateOp.ArgMin"/> and <see cref="AggregateOp.ArgMax"/>.</returns>
    public static bool IsArgOp(AggregateOp op) => op is AggregateOp.ArgMin or AggregateOp.ArgMax;

    /// <summary>
    ///     Determines whether an operation takes the weighting parameter <c>r</c> (<c>exponential</c> and <c>exponentialb</c>).
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns><see langword="true"/> for the two exponential operations.</returns>
    public static bool IsExponential(AggregateOp op) => op is AggregateOp.Exponential or AggregateOp.ExponentialB;

    /// <summary>
    ///     Determines whether a window-only operation takes a <c>param</c> (<c>ntile</c>, <c>lag</c>, <c>lead</c> and <c>nth_value</c>).
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns><see langword="true"/> when the operation reads <c>param</c>.</returns>
    public static bool TakesParameter(WindowOp op) => op is WindowOp.Ntile or WindowOp.Lag or WindowOp.Lead or WindowOp.NthValue;

    /// <summary>
    ///     Determines whether an operation can order the categories of an encoding <c>sort</c> (every operation that yields a number or a
    ///     comparable value: all but the row, array and parameterized ones).
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns><see langword="true"/> when the operation can be the <c>op</c> of a sort object.</returns>
    public static bool CanOrderCategories(AggregateOp op) =>
        !IsArgOp(op) && !IsExponential(op) && op != AggregateOp.Values;

    /// <summary>
    ///     Computes a quantile of numbers that are already sorted, by linear interpolation between the closest ranks (the R-7 method
    ///     of d3's <c>quantileSorted</c>).
    /// </summary>
    /// <param name="sorted">The numbers in ascending order; none is NaN.</param>
    /// <param name="p">The probability, from 0 to 1 (a value outside gives the first or last number).</param>
    /// <returns>The quantile; NaN when there are no numbers or <paramref name="p"/> is NaN.</returns>
    public static double Quantile(ReadOnlySpan<double> sorted, double p)
    {
        var n = sorted.Length;
        if (n == 0 || double.IsNaN(p))
        {
            return double.NaN;
        }

        if (p <= 0 || n < 2)
        {
            return sorted[0];
        }

        if (p >= 1)
        {
            return sorted[n - 1];
        }

        var position = (n - 1) * p;
        var lower = (int)Math.Floor(position);
        return sorted[lower] + ((sorted[lower + 1] - sorted[lower]) * (position - lower));
    }
}
