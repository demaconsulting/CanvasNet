using System.Numerics;

namespace DemaConsulting.CanvasNet.Rendering;

/// <summary>
///     The role a token plays in <see cref="WordWrap.Pack{T}(IReadOnlyList{T}, float, Func{T, WordWrapTokenKind}, Func{T, float, float})"/>.
/// </summary>
public enum WordWrapTokenKind
{
    /// <summary>
    ///     Visible, non-breakable content. Consecutive word tokens form one word group that is never
    ///     split across two lines, even when the tokens come from different formatting runs.
    /// </summary>
    Word,

    /// <summary>
    ///     A collapsible gap (a space or a tab). Whitespace is the only legal wrap point, and a
    ///     whitespace token left stranded at a wrap point is carried onto the next line rather than
    ///     leaving a blank line behind.
    /// </summary>
    Whitespace,

    /// <summary>
    ///     An explicit line break. It is never added to a line: it ends the current line (even an
    ///     empty one) and starts a new one, whatever the available width.
    /// </summary>
    LineBreak,
}

/// <summary>
///     Packs a paragraph's tokens into lines with a greedy word-wrap that never splits a word group.
/// </summary>
/// <remarks>
///     <para>
///         The packer is deliberately independent of any text, font or unit system: the caller
///         classifies each token (<see cref="WordWrapTokenKind"/>) and measures it in whatever unit it
///         lays out in, and the packer only decides which tokens share a line. Consecutive
///         <see cref="WordWrapTokenKind.Word"/> tokens are accumulated into one atomic word group, a
///         <see cref="WordWrapTokenKind.Whitespace"/> token is its own unit, and each unit goes onto
///         the current line unless the line already holds a word and the unit would push the line
///         past the available width, in which case the line is closed and the unit starts the next
///         one. A unit wider than the available width is still placed whole on a line of its own
///         (overflowing it) rather than looping or being split. A line that holds only whitespace
///         does not count as having content, so such a line is never closed on its own.
///     </para>
///     <para>
///         <b>Arithmetic.</b> The <see cref="float"/> and <see cref="double"/> overloads accumulate
///         line widths in their own type, so a caller that lays out in single precision gets exactly
///         the single-precision sums it would get from writing the loop itself.
///     </para>
///     <para>
///         <b>Thread safety</b>: this is a stateless static class; concurrent calls are safe provided
///         the supplied delegates and token list are.
///     </para>
/// </remarks>
public static class WordWrap
{
    /// <summary>
    ///     Packs <paramref name="tokens"/> into lines of at most <paramref name="availableWidth"/>
    ///     (single precision).
    /// </summary>
    /// <typeparam name="T">The caller's token type.</typeparam>
    /// <param name="tokens">The paragraph's tokens, in order. The list is read, never modified.</param>
    /// <param name="availableWidth">
    ///     The width a line may fill; zero or negative is allowed (every unit then gets its own line).
    ///     Positive infinity never wraps.
    /// </param>
    /// <param name="classify">Returns the role of a token.</param>
    /// <param name="measure">
    ///     Returns a token's width given the width already used on the current line when the token
    ///     is reached, so a token whose width depends on its position (a tab stop) can be measured
    ///     exactly; a token with a fixed width ignores the second argument. For a word token that
    ///     width is the line's width before the word group it belongs to. It is never called for a
    ///     <see cref="WordWrapTokenKind.LineBreak"/> token.
    /// </param>
    /// <returns>
    ///     The lines, each the tokens placed on it in order; the caller owns the returned lists. A
    ///     line break adds a line boundary, and the final line is always kept (even empty) once there
    ///     was at least one token, so a trailing line break yields a trailing empty line. No tokens
    ///     at all yields no lines.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="tokens"/>, <paramref name="classify"/> or
    ///     <paramref name="measure"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="availableWidth"/> is NaN.</exception>
    public static List<List<T>> Pack<T>(
        IReadOnlyList<T> tokens,
        float availableWidth,
        Func<T, WordWrapTokenKind> classify,
        Func<T, float, float> measure) =>
        PackCore(tokens, availableWidth, classify, measure);

    /// <summary>
    ///     Packs <paramref name="tokens"/> into lines of at most <paramref name="availableWidth"/>
    ///     (double precision).
    /// </summary>
    /// <typeparam name="T">The caller's token type.</typeparam>
    /// <param name="tokens">The paragraph's tokens, in order. The list is read, never modified.</param>
    /// <param name="availableWidth">
    ///     The width a line may fill; zero or negative is allowed (every unit then gets its own line).
    ///     Positive infinity never wraps.
    /// </param>
    /// <param name="classify">Returns the role of a token.</param>
    /// <param name="measure">
    ///     Returns a token's width given the width already used on the current line when the token
    ///     is reached; see the single-precision overload for the exact contract.
    /// </param>
    /// <returns>The lines; see the single-precision overload for the exact contract.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="tokens"/>, <paramref name="classify"/> or
    ///     <paramref name="measure"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="availableWidth"/> is NaN.</exception>
    public static List<List<T>> Pack<T>(
        IReadOnlyList<T> tokens,
        double availableWidth,
        Func<T, WordWrapTokenKind> classify,
        Func<T, double, double> measure) =>
        PackCore(tokens, availableWidth, classify, measure);

    /// <summary>The packing algorithm, shared by both precisions.</summary>
    /// <typeparam name="T">The caller's token type.</typeparam>
    /// <typeparam name="TNumber">The width type.</typeparam>
    /// <param name="tokens">The paragraph's tokens.</param>
    /// <param name="availableWidth">The width a line may fill.</param>
    /// <param name="classify">Returns the role of a token.</param>
    /// <param name="measure">Returns a token's width given the width already used on the line.</param>
    /// <returns>The lines.</returns>
    private static List<List<T>> PackCore<T, TNumber>(
        IReadOnlyList<T> tokens,
        TNumber availableWidth,
        Func<T, WordWrapTokenKind> classify,
        Func<T, TNumber, TNumber> measure)
        where TNumber : struct, INumber<TNumber>
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(classify);
        ArgumentNullException.ThrowIfNull(measure);
        if (TNumber.IsNaN(availableWidth))
        {
            throw new ArgumentOutOfRangeException(nameof(availableWidth), availableWidth, "The available width must not be NaN.");
        }

        var lines = new List<List<T>>();
        var currentLine = new List<T>();
        var currentWidth = TNumber.Zero;
        var currentLineHasWord = false;
        var sawAnyToken = false;

        var pendingGroup = new List<T>();
        var pendingGroupWidth = TNumber.Zero;

        // Places one wrap unit (a whole word group, or a single whitespace token) on the current
        // line, first closing the line when it already holds a word and the unit would overflow it.
        // A line holding only (stranded) whitespace is never closed on its own: the whitespace
        // is carried forward onto the same line as whatever comes next.
        void Place(IReadOnlyList<T> unit, TNumber unitWidth, bool unitIsWord)
        {
            if (currentLineHasWord && currentWidth + unitWidth > availableWidth)
            {
                lines.Add(currentLine);
                currentLine = [];
                currentWidth = TNumber.Zero;
                currentLineHasWord = false;
            }

            currentLine.AddRange(unit);
            currentWidth += unitWidth;
            currentLineHasWord |= unitIsWord;
        }

        void FlushPendingGroup()
        {
            if (pendingGroup.Count == 0)
            {
                return;
            }

            Place(pendingGroup, pendingGroupWidth, unitIsWord: true);
            pendingGroup.Clear();
            pendingGroupWidth = TNumber.Zero;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            sawAnyToken = true;

            switch (classify(token))
            {
                case WordWrapTokenKind.LineBreak:
                    FlushPendingGroup();
                    lines.Add(currentLine);
                    currentLine = [];
                    currentWidth = TNumber.Zero;
                    currentLineHasWord = false;
                    break;

                case WordWrapTokenKind.Whitespace:
                    FlushPendingGroup();

                    // Measured after the pending group is placed, so a position-dependent width
                    // (a tab stop) sees the true width of the line it is about to join.
                    Place([token], measure(token, currentWidth), unitIsWord: false);
                    break;

                default:
                    // Accumulate into the pending word group rather than placing immediately, so a
                    // word split across token boundaries is never wrapped mid-word.
                    pendingGroup.Add(token);
                    pendingGroupWidth += measure(token, currentWidth);
                    break;
            }
        }

        FlushPendingGroup();

        // The final (or only) line is always kept, even if empty: a trailing line break still
        // produces the trailing blank line it implies. No tokens at all intentionally yields no
        // lines, leaving the caller to decide what an empty paragraph looks like.
        if (sawAnyToken)
        {
            lines.Add(currentLine);
        }

        return lines;
    }
}
