namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The <c>search</c> built-in function.
/// </summary>
/// <remarks>
///     <para>
///         Supported forms: the match value is a string (each Unicode scalar is one match element), a number, or a
///         vector of numbers and strings (each element is one match element). The source is a string (matched scalar
///         by scalar) or a vector; vector elements that are themselves vectors are rows and contribute the element in
///         column <c>index_col_num</c> (rows that are too short contribute nothing).
///     </para>
///     <para>
///         The result has one entry per match element. With <c>num_returns_per_match</c> equal to 1 (the default) the
///         entry is the index of the first match or an empty vector when there is none; with 0 it is the vector of all
///         matching indices; with n above 1 it is the vector of at most the first n matching indices. Every other form
///         (matching vectors or other kinds, a non-integer or negative option, a source that is neither a string nor a
///         vector) is reported as the unsupported feature <c>scad-search-form</c> and yields <c>undef</c>.
///     </para>
/// </remarks>
internal static partial class ScadBuiltinFunctions
{
    /// <summary>The stable identifier of the unsupported-feature report for unsupported <c>search</c> forms.</summary>
    internal const string SearchFormFeature = "scad-search-form";

    /// <summary>
    ///     Implements <c>search</c>.
    /// </summary>
    /// <param name="args">The bound arguments: match value, source, returns per match and column.</param>
    /// <param name="host">The evaluator services.</param>
    /// <returns>The result vector, or <c>undef</c> when the form is unsupported.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the work or the result exceeds a limit.</exception>
    private static ScadValue Search(ReadOnlySpan<ScadValue> args, IScadBuiltinHost host)
    {
        // Gather the match elements, the candidate values and the two options, or report an unsupported form
        var returns = OptionalCount(args, 2, defaultValue: 1);
        var column = OptionalCount(args, 3, defaultValue: 0);
        if (returns < 0 || column < 0 || !TryGetMatchElements(args[0], out var elements) || !TryGetCandidates(args[1], column, out var candidates))
        {
            host.Unsupported(SearchFormFeature, "This form of search() is not supported");
            return ScadValue.Undef;
        }

        // The work is the product of both sizes; charge it up front so the step limit stops a huge search at once
        host.CheckListLength(elements.Count);
        host.Charge(Math.Max(1, (long)elements.Count * candidates.Count));
        var entries = new ScadValue[elements.Count];
        long collected = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            var indices = FindIndices(elements[i], candidates, returns);
            collected += indices.Count;
            host.CheckListLength(collected);
            entries[i] = ToEntry(indices, returns);
        }

        return ScadValue.FromVector(entries);
    }

    /// <summary>
    ///     Reads an optional non-negative integer option.
    /// </summary>
    /// <param name="args">The bound arguments.</param>
    /// <param name="slot">The slot of the option.</param>
    /// <param name="defaultValue">The value when the option is absent or <c>undef</c>.</param>
    /// <returns>The option, or -1 when it is present but not a non-negative whole number.</returns>
    private static int OptionalCount(ReadOnlySpan<ScadValue> args, int slot, int defaultValue)
    {
        if (args.Length <= slot || args[slot].Kind == ScadValueKind.Undef)
        {
            return defaultValue;
        }

        var value = args[slot].Kind == ScadValueKind.Number ? args[slot].AsNumber : double.NaN;
        return value >= 0 && value <= int.MaxValue && ScadNumbers.AreEqual(value, Math.Floor(value)) ? (int)value : -1;
    }

    /// <summary>
    ///     Splits the match value into match elements.
    /// </summary>
    /// <param name="match">The match value.</param>
    /// <param name="elements">The elements: strings or numbers.</param>
    /// <returns><see langword="true"/> when the match value is a string, a number or a vector of numbers and strings.</returns>
    private static bool TryGetMatchElements(ScadValue match, out List<ScadValue> elements)
    {
        elements = [];
        switch (match.Kind)
        {
            case ScadValueKind.Number:
                elements.Add(match);
                return true;
            case ScadValueKind.String:
                elements.AddRange(ScadText.Scalars(match.AsString).Select(ScadValue.FromString));
                return true;
            case ScadValueKind.Vector:
                elements.AddRange(match.AsVector.AsSpan().ToArray());
                return elements.TrueForAll(e => e.Kind is ScadValueKind.Number or ScadValueKind.String);
            default:
                return false;
        }
    }

    /// <summary>
    ///     Builds the candidate values to compare the match elements with.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="column">The column used for rows.</param>
    /// <param name="candidates">The candidates, one per source index; a row that is too short yields <c>undef</c>, which never matches.</param>
    /// <returns><see langword="true"/> when the source is a string or a vector.</returns>
    private static bool TryGetCandidates(ScadValue source, int column, out List<ScadValue> candidates)
    {
        candidates = [];
        switch (source.Kind)
        {
            case ScadValueKind.String:
                candidates.AddRange(ScadText.Scalars(source.AsString).Select(ScadValue.FromString));
                return true;
            case ScadValueKind.Vector:
                foreach (var item in source.AsVector.AsSpan())
                {
                    candidates.Add(item.Kind == ScadValueKind.Vector ? ColumnOf(item.AsVector, column) : item);
                }

                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///     Reads one column of a row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The zero-based column.</param>
    /// <returns>The element, or <c>undef</c> (which never matches) when the row is too short.</returns>
    private static ScadValue ColumnOf(ScadVector row, int column) => column < row.Count ? row[column] : ScadValue.Undef;

    /// <summary>
    ///     Finds the indices of the candidates equal to a match element.
    /// </summary>
    /// <param name="element">The match element.</param>
    /// <param name="candidates">The candidates.</param>
    /// <param name="returns">The returns-per-match option: 0 for all matches, otherwise the maximum number to find.</param>
    /// <returns>The matching indices in ascending order.</returns>
    private static List<int> FindIndices(ScadValue element, List<ScadValue> candidates, int returns)
    {
        var indices = new List<int>();
        for (var i = 0; i < candidates.Count && (returns == 0 || indices.Count < returns); i++)
        {
            if (candidates[i].Equals(element))
            {
                indices.Add(i);
            }
        }

        return indices;
    }

    /// <summary>
    ///     Converts the indices found for one match element to its result entry.
    /// </summary>
    /// <param name="indices">The indices.</param>
    /// <param name="returns">The returns-per-match option.</param>
    /// <returns>A single index (or the empty vector) for the default option; otherwise the vector of indices.</returns>
    private static ScadValue ToEntry(List<int> indices, int returns)
    {
        if (returns == 1)
        {
            return indices.Count == 1 ? ScadValue.FromNumber(indices[0]) : ScadValue.FromVector(Array.Empty<ScadValue>());
        }

        return ScadValue.FromVector(indices.Select(i => ScadValue.FromNumber(i)).ToArray());
    }
}
