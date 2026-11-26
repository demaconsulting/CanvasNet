namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     The form of an embellished operator (MathML Core 3.2.4.2): where it stands in its row.
/// </summary>
internal enum OperatorForm
{
    /// <summary>Between operands (the default).</summary>
    Infix,

    /// <summary>Before its operand: the first of several in-flow children of a row.</summary>
    Prefix,

    /// <summary>After its operand: the last of several in-flow children of a row.</summary>
    Postfix,
}

/// <summary>
///     The direction in which an operator stretches (MathML Core 3.2.4.1, the stretch axis).
/// </summary>
internal enum StretchAxis
{
    /// <summary>Vertically, to the height of its row (fences, vertical arrows).</summary>
    Block,

    /// <summary>Horizontally, to the width of its base (horizontal arrows and accents, under and over a base).</summary>
    Inline,
}

/// <summary>
///     The categories of the operator dictionary of MathML Core (Appendix B.1, Figure 26).
/// </summary>
internal enum OperatorCategory
{
    /// <summary>An operator with no entry: 5/18 em on each side.</summary>
    Default,

    /// <summary>The infix vertical line and tilde operator: the values of <see cref="Default"/>, without trying the other forms.</summary>
    ForceDefault,

    /// <summary>Infix, 5/18 em, stretchy (arrows).</summary>
    A,

    /// <summary>Infix, 4/18 em (additive operators).</summary>
    B,

    /// <summary>Infix, 3/18 em (multiplicative operators).</summary>
    C,

    /// <summary>Prefix, no space (prefix signs, quantifiers).</summary>
    D,

    /// <summary>Postfix, no space (factorial, primes).</summary>
    E,

    /// <summary>Prefix, no space, stretchy and symmetric (opening fences).</summary>
    F,

    /// <summary>Postfix, no space, stretchy and symmetric (closing fences).</summary>
    G,

    /// <summary>Prefix, 3/18 em, symmetric large operator (integrals).</summary>
    H,

    /// <summary>Postfix, no space, stretchy (horizontal accents).</summary>
    I,

    /// <summary>Prefix, 3/18 em, symmetric large operator with movable limits (sums and products).</summary>
    J,

    /// <summary>Infix, no space (invisible operators, slashes).</summary>
    K,

    /// <summary>Prefix, 3/18 em before and none after (differentials, radicals).</summary>
    L,

    /// <summary>Infix, none before and 3/18 em after (separators).</summary>
    M,
}

/// <summary>
///     The dictionary properties of an operator: its category and the values that the category sets (MathML Core B.1).
/// </summary>
/// <param name="Category">The category.</param>
/// <param name="LeadingSpace">The <c>lspace</c> in em.</param>
/// <param name="TrailingSpace">The <c>rspace</c> in em.</param>
/// <param name="Stretchy">Whether the operator stretches.</param>
/// <param name="Symmetric">Whether it stretches symmetrically about the math axis.</param>
/// <param name="LargeOp">Whether it is drawn larger in display style.</param>
/// <param name="MovableLimits">Whether its under- and overscripts become sub- and superscripts outside display style.</param>
internal sealed record OperatorProperties(
    OperatorCategory Category,
    double LeadingSpace,
    double TrailingSpace,
    bool Stretchy,
    bool Symmetric,
    bool LargeOp,
    bool MovableLimits);

/// <summary>
///     The operator dictionary of MathML Core (Appendix B.1): the category of an operator from its text and form, the
///     values of each category, the fence and separator tables, and the stretch axis of each character; and the inference
///     of the form of an operator from its position in its row (3.2.4.2).
/// </summary>
/// <remarks>
///     <para>
///         <b>Category.</b> The algorithm to determine the category of an operator of MathML Core: a text of one or two
///         UTF-16 characters is looked up by one character (a combining long solidus or vertical line overlay is dropped, a
///         two-character ASCII operator such as <c>&amp;&amp;</c> is mapped to U+0320 plus its index in
///         Operators_2_ascii_chars) in the sorted ranges of its form (Figure 25, read directly; it is equivalent to the
///         encoded Figure 27). The infix vertical line and tilde operator are <see cref="OperatorCategory.ForceDefault"/>.
///         Anything else is <see cref="OperatorCategory.Default"/>.
///     </para>
///     <para>
///         <b>Properties.</b> The algorithm for determining the properties of an embellished operator: when the form was
///         inferred and the category is <see cref="OperatorCategory.Default"/>, the infix, postfix and prefix forms are
///         tried in that order. The attributes of the <c>mo</c> element override the values, which the token layout
///         applies.
///     </para>
///     <para>
///         The tables are generated from the dated MathML Core Candidate Recommendation by
///         <c>tools/mathml-data/update-mathml-tables.ps1</c> (see <c>third-party/README.md</c>). The class is stateless
///         and safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class OperatorDictionary
{
    /// <summary>The first character of the range that MathML Core keeps out of the dictionary (and maps the two-character ASCII operators to).</summary>
    private const int ReservedFirst = 0x0320;

    /// <summary>The last character of the reserved range.</summary>
    private const int ReservedLast = 0x03FF;

    /// <summary>
    ///     Determines the category of an operator from its text and form (MathML Core B.1, the algorithm to determine the
    ///     category of an operator).
    /// </summary>
    /// <param name="content">The text of the operator.</param>
    /// <param name="form">The form.</param>
    /// <returns>The category.</returns>
    public static OperatorCategory Category(string content, OperatorForm form)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Two Arabic operators outside the Basic Multilingual Plane have a category of their own in the postfix form
        if (content.Length == 2 && char.IsSurrogatePair(content[0], content[1]))
        {
            var codePoint = char.ConvertToUtf32(content[0], content[1]);
            return codePoint is 0x1EEF0 or 0x1EEF1 && form == OperatorForm.Postfix ? OperatorCategory.I : OperatorCategory.Default;
        }

        var key = Key(content);
        if (key < 0)
        {
            return OperatorCategory.Default;
        }

        if (form == OperatorForm.Infix && key is 0x007C or 0x223C)
        {
            return OperatorCategory.ForceDefault;
        }

        var ranges = form switch
        {
            OperatorForm.Prefix => PrefixRanges,
            OperatorForm.Postfix => PostfixRanges,
            _ => InfixRanges,
        };
        return FindRange(ranges, 3, key) is { } index ? (OperatorCategory)ranges[(index * 3) + 2] : OperatorCategory.Default;
    }

    /// <summary>
    ///     Determines the dictionary properties of an operator (MathML Core 3.2.4.2): the category of its form, or, when the
    ///     form was inferred and the category is <see cref="OperatorCategory.Default"/>, of the first of the infix,
    ///     postfix and prefix forms that has an entry.
    /// </summary>
    /// <param name="content">The text of the operator.</param>
    /// <param name="form">The form.</param>
    /// <param name="formIsExplicit">Whether the form comes from the <c>form</c> attribute.</param>
    /// <returns>The properties.</returns>
    public static OperatorProperties Lookup(string content, OperatorForm form, bool formIsExplicit)
    {
        var category = Category(content, form);
        if (category == OperatorCategory.Default && !formIsExplicit)
        {
            foreach (var fallback in (OperatorForm[])[OperatorForm.Infix, OperatorForm.Postfix, OperatorForm.Prefix])
            {
                category = Category(content, fallback);
                if (category != OperatorCategory.Default)
                {
                    break;
                }
            }
        }

        return Properties(category);
    }

    /// <summary>
    ///     Gets the values of a category (MathML Core B.1, Figure 26).
    /// </summary>
    /// <param name="category">The category.</param>
    /// <returns>The properties.</returns>
    public static OperatorProperties Properties(OperatorCategory category)
    {
        foreach (var (entry, leading, trailing, stretchy, symmetric, largeOp, movableLimits) in CategoryValues)
        {
            if (entry == category)
            {
                return new OperatorProperties(category, leading, trailing, stretchy, symmetric, largeOp, movableLimits);
            }
        }

        throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown operator category.");
    }

    /// <summary>
    ///     Gets whether an operator is a fence by default (the Operators_fence table of MathML Core B.1).
    /// </summary>
    /// <param name="content">The text of the operator.</param>
    /// <returns>True for a fence.</returns>
    public static bool IsFence(string content) => Key(content) is var key and >= 0 && FindRange(FenceRanges, 2, key) is not null;

    /// <summary>
    ///     Gets whether an operator is a separator by default (the Operators_separator table of MathML Core B.1).
    /// </summary>
    /// <param name="content">The text of the operator.</param>
    /// <returns>True for a separator.</returns>
    public static bool IsSeparator(string content) => Key(content) is var key and >= 0 && FindRange(SeparatorRanges, 2, key) is not null;

    /// <summary>
    ///     Gets the stretch axis of an operator (MathML Core 3.2.4.1): inline when its text is a single character with an
    ///     inline intrinsic stretch axis (Figure 28), block otherwise.
    /// </summary>
    /// <param name="content">The text of the operator.</param>
    /// <returns>The axis.</returns>
    public static StretchAxis Axis(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var enumerator = content.EnumerateRunes();
        if (!enumerator.MoveNext())
        {
            return StretchAxis.Block;
        }

        var codePoint = enumerator.Current.Value;
        return !enumerator.MoveNext() && InlineAxisCodePoints.BinarySearch(codePoint) >= 0 ? StretchAxis.Inline : StretchAxis.Block;
    }

    /// <summary>
    ///     Infers the form of an embellished operator from its position among the in-flow children of a row that are not
    ///     space-like (MathML Core 3.2.4.2): the first of several is prefix, the last of several is postfix, any other is
    ///     infix.
    /// </summary>
    /// <param name="position">The zero-based position of the operator among the in-flow children that are not space-like.</param>
    /// <param name="count">The number of in-flow children that are not space-like.</param>
    /// <returns>The form.</returns>
    public static OperatorForm InferForm(int position, int count)
    {
        if (count <= 1)
        {
            return OperatorForm.Infix;
        }

        if (position == 0)
        {
            return OperatorForm.Prefix;
        }

        return position == count - 1 ? OperatorForm.Postfix : OperatorForm.Infix;
    }

    /// <summary>
    ///     Parses the value of the <c>form</c> attribute (ASCII case-insensitive; whitespace around it makes it invalid, as MathML Core matches the values exactly).
    /// </summary>
    /// <param name="value">The attribute value.</param>
    /// <param name="form">The form.</param>
    /// <returns>True when the value is <c>prefix</c>, <c>infix</c> or <c>postfix</c>.</returns>
    public static bool TryParseForm(string value, out OperatorForm form)
    {
        ArgumentNullException.ThrowIfNull(value);
        form = OperatorForm.Infix;
        switch (value.ToLowerInvariant())
        {
            case "prefix":
                form = OperatorForm.Prefix;
                return true;
            case "postfix":
                form = OperatorForm.Postfix;
                return true;
            case "infix":
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///     Validates the generated tables: every range is ordered, and the ranges of each table are sorted and disjoint
    ///     (the binary searches rely on it).
    /// </summary>
    /// <returns>True when every table is well-formed.</returns>
    internal static bool TablesAreWellFormed() =>
        RangesAreSortedAndDisjoint(InfixRanges, 3)
        && RangesAreSortedAndDisjoint(PrefixRanges, 3)
        && RangesAreSortedAndDisjoint(PostfixRanges, 3)
        && RangesAreSortedAndDisjoint(FenceRanges, 2)
        && RangesAreSortedAndDisjoint(SeparatorRanges, 2)
        && IsStrictlyAscending(InlineAxisCodePoints)
        && TwoAsciiOperators.Length == 18
        && CategoryValues.Length == Enum.GetValues<OperatorCategory>().Length;

    /// <summary>
    ///     Computes the dictionary key of an operator text: its character, without a combining long solidus or vertical line
    ///     overlay, or U+0320 plus the index of a two-character ASCII operator.
    /// </summary>
    /// <param name="content">The text of the operator.</param>
    /// <returns>The key, or -1 when the text has no key (it is then <see cref="OperatorCategory.Default"/>).</returns>
    private static int Key(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        switch (content.Length)
        {
            case 1:
                // The reserved range stands for the two-character ASCII operators, never for itself
                return content[0] is >= (char)ReservedFirst and <= (char)ReservedLast ? -1 : content[0];
            case 2:
                if (content[1] is '\u0338' or '\u20D2')
                {
                    return content[0];
                }

                var index = Array.IndexOf(TwoAsciiOperators, content);
                return index >= 0 ? ReservedFirst + index : -1;
            default:
                return -1;
        }
    }

    /// <summary>
    ///     Finds the range of a sorted table that holds a key.
    /// </summary>
    /// <param name="table">The table: groups of <paramref name="stride"/> values that start with the first and the last code point of a range.</param>
    /// <param name="stride">The number of values per range.</param>
    /// <param name="key">The key.</param>
    /// <returns>The index of the range, or null when no range holds the key.</returns>
    private static int? FindRange(ReadOnlySpan<int> table, int stride, int key)
    {
        // Binary search for the last range that starts at or before the key
        var low = 0;
        var high = (table.Length / stride) - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var first = table[middle * stride];
            if (key < first)
            {
                high = middle - 1;
            }
            else if (key > table[(middle * stride) + 1])
            {
                low = middle + 1;
            }
            else
            {
                return middle;
            }
        }

        return null;
    }

    /// <summary>
    ///     Checks that the ranges of a table are ordered, sorted and disjoint.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="stride">The number of values per range.</param>
    /// <returns>True when they are.</returns>
    private static bool RangesAreSortedAndDisjoint(ReadOnlySpan<int> table, int stride)
    {
        if (table.Length % stride != 0)
        {
            return false;
        }

        var previousLast = -1;
        for (var i = 0; i < table.Length; i += stride)
        {
            if (table[i] <= previousLast || table[i + 1] < table[i])
            {
                return false;
            }

            previousLast = table[i + 1];
        }

        return true;
    }

    /// <summary>
    ///     Checks that a list is strictly ascending.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>True when it is.</returns>
    private static bool IsStrictlyAscending(ReadOnlySpan<int> values)
    {
        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] <= values[i - 1])
            {
                return false;
            }
        }

        return true;
    }
}
