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
///     <para>
///         Cost model: the linear preprocessing of the arguments is charged first, from sizes known in constant time
///         (<c>1 + match size + source size</c> steps, where a string counts its UTF-16 length), before any pass over
///         them; then the exact counts are computed without allocating, the result size is checked against the
///         list-length limit, and the comparison work (match elements times candidates) is charged before the first
///         comparison. The source is read in place (a string is walked by scalar and compared by ordinal comparison, a vector
///         is read by element or row column) and no list of per-scalar strings is built, and the list-length limit
///         is checked while the indices of a multi-match search are collected.
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
        var match = args[0];
        var source = args[1];
        var returns = OptionalCount(args, 2, defaultValue: 1);
        var column = OptionalCount(args, 3, defaultValue: 0);
        if (returns < 0 || column < 0 || !IsSearchForm(match, source))
        {
            return UnsupportedSearch(host);
        }

        // The linear preprocessing is charged from sizes known in constant time, before any pass over the arguments
        host.Charge(1 + SizeBound(match) + SizeBound(source));
        if (!HasScalarMatchElements(match))
        {
            return UnsupportedSearch(host);
        }

        // Exact counts need no allocation; the result size is checked before the result array exists
        var matchCount = match.Kind switch
        {
            ScadValueKind.Number => 1,
            ScadValueKind.String => ScadText.ScalarCount(match.AsString),
            _ => match.AsVector.Count,
        };
        host.CheckListLength(matchCount);
        var table = new SearchTable(source, column);

        // The comparison work is the product of both sizes; charge it up front so the step limit stops a huge search at once
        host.Charge(Math.Max(1, (long)matchCount * table.Count));
        var entries = new ScadValue[matchCount];
        var position = 0;
        long collected = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            var key = NextSearchKey(match, i, ref position);
            entries[i] = returns == 1
                ? FirstMatchEntry(key, table)
                : AllMatchesEntry(key, table, returns, host, ref collected);
        }

        return ScadValue.FromVector(entries);
    }

    /// <summary>
    ///     Reports an unsupported form of <c>search</c>.
    /// </summary>
    /// <param name="host">The evaluator services.</param>
    /// <returns><c>undef</c>.</returns>
    private static ScadValue UnsupportedSearch(IScadBuiltinHost host)
    {
        host.Unsupported(SearchFormFeature, "This form of search() is not supported");
        return ScadValue.Undef;
    }

    /// <summary>
    ///     Determines whether the kinds of the match value and the source are supported, without looking inside them.
    /// </summary>
    /// <param name="match">The match value.</param>
    /// <param name="source">The source value.</param>
    /// <returns><see langword="true"/> for a string, number or vector match value and a string or vector source.</returns>
    private static bool IsSearchForm(ScadValue match, ScadValue source) =>
        match.Kind is ScadValueKind.Number or ScadValueKind.String or ScadValueKind.Vector
        && source.Kind is ScadValueKind.String or ScadValueKind.Vector;

    /// <summary>
    ///     Gets an upper bound of the number of elements of a search argument in constant time.
    /// </summary>
    /// <param name="value">The match value or the source.</param>
    /// <returns>One for a number, the UTF-16 length of a string, or the element count of a vector.</returns>
    private static long SizeBound(ScadValue value) => value.Kind switch
    {
        ScadValueKind.String => value.AsString.Length,
        ScadValueKind.Vector => value.AsVector.Count,
        _ => 1,
    };

    /// <summary>
    ///     Determines whether every element of a vector match value is a number or a string.
    /// </summary>
    /// <param name="match">The match value; a string or a number always qualifies.</param>
    /// <returns><see langword="true"/> when the match value can be searched for.</returns>
    private static bool HasScalarMatchElements(ScadValue match)
    {
        if (match.Kind != ScadValueKind.Vector)
        {
            return true;
        }

        foreach (var element in match.AsVector.AsSpan())
        {
            if (element.Kind is not (ScadValueKind.Number or ScadValueKind.String))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Reads one match element without allocating.
    /// </summary>
    /// <param name="match">The match value.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="position">The UTF-16 position of the next scalar of a string match value; advanced for strings.</param>
    /// <returns>The match element.</returns>
    private static SearchKey NextSearchKey(ScadValue match, int index, ref int position)
    {
        switch (match.Kind)
        {
            case ScadValueKind.String:
                var text = match.AsString;
                var length = ScadText.ScalarLength(text, position);
                var key = new SearchKey(text, position, length, ScadValue.Undef);
                position += length;
                return key;
            case ScadValueKind.Vector:
                var element = match.AsVector[index];
                return element.Kind == ScadValueKind.String
                    ? new SearchKey(element.AsString, 0, element.AsString.Length, ScadValue.Undef)
                    : new SearchKey(null, 0, 0, element);
            default:
                return new SearchKey(null, 0, 0, match);
        }
    }

    /// <summary>
    ///     Builds the entry of the default form: the index of the first match, or the empty vector.
    /// </summary>
    /// <param name="key">The match element.</param>
    /// <param name="table">The source.</param>
    /// <returns>The index as a number, or the empty vector when nothing matches.</returns>
    private static ScadValue FirstMatchEntry(SearchKey key, SearchTable table)
    {
        var index = 0;
        var position = 0;
        return table.TryFindNext(key, ref index, ref position, out var found)
            ? ScadValue.FromNumber(found)
            : ScadValue.FromVector(Array.Empty<ScadValue>());
    }

    /// <summary>
    ///     Builds the entry of the forms that return several matches: the vector of the first matching indices.
    /// </summary>
    /// <remarks>The list-length limit is checked for every index collected, so a result over the limit stops the search at once.</remarks>
    /// <param name="key">The match element.</param>
    /// <param name="table">The source.</param>
    /// <param name="returns">The returns-per-match option: 0 for all matches, otherwise the maximum number to find.</param>
    /// <param name="host">The evaluator services.</param>
    /// <param name="collected">The number of indices collected for all match elements so far; updated.</param>
    /// <returns>The vector of matching indices in ascending order.</returns>
    private static ScadValue AllMatchesEntry(SearchKey key, SearchTable table, int returns, IScadBuiltinHost host, ref long collected)
    {
        var indices = new List<ScadValue>();
        var index = 0;
        var position = 0;
        while ((returns == 0 || indices.Count < returns) && table.TryFindNext(key, ref index, ref position, out var found))
        {
            host.CheckListLength(++collected);
            indices.Add(ScadValue.FromNumber(found));
        }

        return ScadValue.FromVector(indices.ToArray());
    }

    /// <summary>
    ///     One match element: a number, or a run of UTF-16 units of a string.
    /// </summary>
    /// <param name="Text">The string holding the element, or <see langword="null"/> for a number.</param>
    /// <param name="Start">The start of the element in <paramref name="Text"/>.</param>
    /// <param name="Length">The length of the element in <paramref name="Text"/>.</param>
    /// <param name="Value">The number, when <paramref name="Text"/> is <see langword="null"/>.</param>
    private readonly record struct SearchKey(string? Text, int Start, int Length, ScadValue Value)
    {
        /// <summary>
        ///     Determines whether a candidate value equals the element.
        /// </summary>
        /// <param name="candidate">The candidate.</param>
        /// <returns><see langword="true"/> for an equal number or a string equal by ordinal comparison.</returns>
        public bool Matches(ScadValue candidate) => Text is null
            ? candidate.Equals(Value)
            : candidate.Kind == ScadValueKind.String && candidate.AsString.AsSpan().SequenceEqual(Text.AsSpan(Start, Length));
    }

    /// <summary>
    ///     The source of a search, read in place: a string walked by scalar, or a vector read by element or row column.
    /// </summary>
    private readonly struct SearchTable
    {
        /// <summary>The string source, or <see langword="null"/> for a vector source.</summary>
        private readonly string? _text;

        /// <summary>The vector source, or <see langword="null"/> for a string source.</summary>
        private readonly ScadVector? _items;

        /// <summary>The column read from rows of a vector source.</summary>
        private readonly int _column;

        /// <summary>
        ///     Initializes the table.
        /// </summary>
        /// <param name="source">The source value: a string or a vector.</param>
        /// <param name="column">The column read from rows.</param>
        public SearchTable(ScadValue source, int column)
        {
            _column = column;
            if (source.Kind == ScadValueKind.String)
            {
                _text = source.AsString;
                _items = null;
                Count = ScadText.ScalarCount(_text);
            }
            else
            {
                _text = null;
                _items = source.AsVector;
                Count = _items.Count;
            }
        }

        /// <summary>
        ///     Gets the number of candidates: the scalar count of a string or the element count of a vector.
        /// </summary>
        public int Count { get; }

        /// <summary>
        ///     Finds the next candidate that equals a match element.
        /// </summary>
        /// <param name="key">The match element.</param>
        /// <param name="index">The index of the next candidate to examine; advanced past the match.</param>
        /// <param name="position">The UTF-16 position of that candidate in a string source; advanced past the match.</param>
        /// <param name="found">The index of the match.</param>
        /// <returns><see langword="true"/> when a further match exists.</returns>
        public bool TryFindNext(SearchKey key, ref int index, ref int position, out int found)
        {
            found = -1;
            if (_text is not null)
            {
                return TryFindNextScalar(_text, key, ref index, ref position, out found);
            }

            // A row contributes its column, a shorter row nothing (undef never matches), any other element itself
            while (index < _items!.Count)
            {
                var current = index++;
                var item = _items[current];
                var candidate = item.Kind == ScadValueKind.Vector ? RowColumn(item.AsVector) : item;
                if (key.Matches(candidate))
                {
                    found = current;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Reads the column of a row.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns>The element, or <c>undef</c> (which never matches) when the row is too short.</returns>
        private ScadValue RowColumn(ScadVector row) => _column < row.Count ? row[_column] : ScadValue.Undef;

        /// <summary>
        ///     Finds the next scalar of a string source that equals a match element.
        /// </summary>
        /// <param name="text">The source string.</param>
        /// <param name="key">The match element.</param>
        /// <param name="index">The scalar index of the next candidate; advanced past the match.</param>
        /// <param name="position">The UTF-16 position of that scalar; advanced past the match.</param>
        /// <param name="found">The scalar index of the match.</param>
        /// <returns><see langword="true"/> when a further match exists.</returns>
        private static bool TryFindNextScalar(string text, SearchKey key, ref int index, ref int position, out int found)
        {
            found = -1;

            // A number never equals a string, so nothing can match
            if (key.Text is null)
            {
                return false;
            }

            while (position < text.Length)
            {
                var start = position;
                var length = ScadText.ScalarLength(text, start);
                var current = index++;
                position += length;
                if (length == key.Length && text.AsSpan(start, length).SequenceEqual(key.Text.AsSpan(key.Start, key.Length)))
                {
                    found = current;
                    return true;
                }
            }

            return false;
        }
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
}
