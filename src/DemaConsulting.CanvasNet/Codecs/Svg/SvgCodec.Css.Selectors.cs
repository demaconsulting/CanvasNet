// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness

using System.Text;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    // ================================================================================================
    // CSS selector parsing and matching
    // ================================================================================================

    /// <summary>
    ///     Identifies how one compound selector in a <see cref="CssComplexSelector"/> relates to
    ///     the compound selector immediately before it in the same selector.
    /// </summary>
    private enum CssCombinator
    {
        /// <summary>
        ///     No preceding compound selector - always the combinator of the first (leftmost)
        ///     compound in a <see cref="CssComplexSelector"/>.
        /// </summary>
        None,

        /// <summary>
        ///     A whitespace ("descendant") combinator: the preceding compound selector must match
        ///     some (not necessarily immediate) ancestor of the element matched by the following
        ///     compound selector.
        /// </summary>
        Descendant,

        /// <summary>
        ///     A <c>&gt;</c> ("child") combinator: the preceding compound selector must match the
        ///     immediate parent of the element matched by the following compound selector.
        /// </summary>
        Child
    }

    /// <summary>
    ///     One simple/compound CSS selector component - a type selector, the universal selector, an
    ///     id selector, and/or one or more class selectors, all required to match the same element
    ///     simultaneously (for example <c>rect.foo.bar#baz</c>). Sibling combinators (<c>+</c>/
    ///     <c>~</c>), attribute selectors (<c>[attr]</c>), and pseudo-classes (<c>:hover</c>) are
    ///     explicitly out of scope - see this class's remarks - and cause the whole selector they
    ///     appear in to be dropped during parsing (see <see cref="ParseSelectorList"/>) rather than
    ///     being represented here.
    /// </summary>
    /// <param name="Type">The required element (tag) name, or <see langword="null"/> if none was specified.</param>
    /// <param name="IsUniversal">
    ///     Whether this compound selector included the universal selector <c>*</c> (which
    ///     contributes zero to every specificity component, per the CSS specification - see
    ///     <see cref="ComputeSpecificity"/>). A compound selector can be universal and still carry
    ///     an id/class (for example <c>*.foo</c>), so this is independent of <paramref name="Type"/>.
    /// </param>
    /// <param name="Id">The required <c>id</c> attribute value, or <see langword="null"/> if none was specified.</param>
    /// <param name="Classes">
    ///     Every class this compound selector requires to be present in the element's own
    ///     space-separated <c>class</c> attribute (all of them, not any one) - empty if none were
    ///     specified.
    /// </param>
    private sealed record CssCompoundSelector(string? Type, bool IsUniversal, string? Id, IReadOnlyList<string> Classes);

    /// <summary>
    ///     One complete CSS selector - an ordered chain of <see cref="CssCompoundSelector"/>
    ///     entries, each paired with the <see cref="CssCombinator"/> connecting it to the previous
    ///     entry (the first entry's own combinator is always <see cref="CssCombinator.None"/>). A
    ///     single-entry chain (no combinator at all, for example plain <c>rect.foo</c>) is the
    ///     common case; multiple entries represent a descendant/child combinator chain (for example
    ///     <c>g &gt; rect.foo</c>).
    /// </summary>
    /// <param name="Segments">
    ///     The compound-selector chain, in left-to-right (outermost-ancestor-first) source order.
    ///     Never empty - see <see cref="ParseSelectorList"/>.
    /// </param>
    private sealed record CssComplexSelector(IReadOnlyList<(CssCompoundSelector Compound, CssCombinator Combinator)> Segments);

    /// <summary>
    ///     Parses a comma-separated CSS selector list (a rule's full selector text, before its
    ///     <c>{ ... }</c> body), dropping - individually, without discarding the rest of the list -
    ///     any selector that is malformed, uses a sibling combinator (<c>+</c>/<c>~</c>), an
    ///     attribute selector (<c>[attr]</c>), or a pseudo-class (<c>:hover</c> and similar), all of
    ///     which are explicitly out of scope for this phase's static-rendering CSS engine (see this
    ///     class's remarks): a pseudo-class can never match under one static rasterization pass with
    ///     no interactive/pointer/focus state concept at all, and a sibling combinator would require
    ///     introducing sibling-position bookkeeping nowhere else needed in this codec, for a
    ///     combinator kind rare in hand-authored/tool-exported static SVG styling.
    /// </summary>
    /// <param name="text">The raw selector-list text (everything before a rule's <c>{</c>).</param>
    /// <returns>
    ///     The successfully parsed selectors, in source order, capped at
    ///     <see cref="MaxCssSelectorsPerRule"/> entries (any selector beyond that cap is silently
    ///     dropped, exactly like an unsupported one). Empty if every selector in the list was
    ///     dropped - the caller (<see cref="ParseStylesheet"/>) treats an empty result as "drop the
    ///     whole rule."
    /// </returns>
    private static List<CssComplexSelector> ParseSelectorList(string text)
    {
        var result = new List<CssComplexSelector>();
        foreach (var part in SplitTopLevel(text, ','))
        {
            if (result.Count >= MaxCssSelectorsPerRule)
            {
                break;
            }

            var trimmed = part.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (TryParseComplexSelector(trimmed, out var selector))
            {
                result.Add(selector);
            }
        }

        return result;
    }

    /// <summary>
    ///     Attempts to parse one selector (a single comma-list member) into a
    ///     <see cref="CssComplexSelector"/>, splitting it on whitespace (descendant combinator) and
    ///     <c>&gt;</c> (child combinator) into its compound-selector chain.
    /// </summary>
    /// <param name="text">The single selector's trimmed text (no surrounding whitespace).</param>
    /// <param name="selector">The parsed selector, if this method returns <see langword="true"/>.</param>
    /// <returns>
    ///     <see langword="true"/> if every compound in the chain parsed successfully and the chain
    ///     length does not exceed <see cref="MaxCssCombinatorSegmentsPerSelector"/>;
    ///     <see langword="false"/> if the selector is malformed, uses a sibling combinator
    ///     (<c>+</c>/<c>~</c>), an attribute selector (<c>[</c>), a pseudo-class (<c>:</c>), or
    ///     exceeds the combinator-chain length cap - each tolerated by the caller dropping just this
    ///     one selector.
    /// </returns>
    private static bool TryParseComplexSelector(string text, out CssComplexSelector selector)
    {
        selector = null!;

        // A pseudo-class (":hover"), attribute selector ("[attr]"), or sibling combinator ("+"/"~")
        // anywhere in the text marks the whole selector as unsupported - see this method's remarks
        if (text.Contains(':') || text.Contains('[') || text.Contains(']') ||
            text.Contains('+') || text.Contains('~'))
        {
            return false;
        }

        var rawSegments = new List<(string CompoundText, CssCombinator Combinator)>();
        var builder = new StringBuilder();
        var pendingCombinator = CssCombinator.None;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (builder.Length > 0)
                {
                    rawSegments.Add((builder.ToString(), pendingCombinator));
                    builder.Clear();
                    pendingCombinator = CssCombinator.Descendant;
                }

                continue;
            }

            if (c == '>')
            {
                if (builder.Length > 0)
                {
                    rawSegments.Add((builder.ToString(), pendingCombinator));
                    builder.Clear();
                }

                pendingCombinator = CssCombinator.Child;
                continue;
            }

            builder.Append(c);
        }

        if (builder.Length > 0)
        {
            rawSegments.Add((builder.ToString(), pendingCombinator));
        }

        if (rawSegments.Count == 0 || rawSegments.Count > MaxCssCombinatorSegmentsPerSelector)
        {
            return false;
        }

        var segments = new List<(CssCompoundSelector, CssCombinator)>(rawSegments.Count);
        for (var i = 0; i < rawSegments.Count; i++)
        {
            if (!TryParseCompoundSelector(rawSegments[i].CompoundText, out var compound))
            {
                return false;
            }

            // The first (leftmost) segment's combinator is always None, regardless of any leading
            // whitespace/">" this parse loop may have attributed to it (a leading combinator is not
            // valid CSS, but tolerated here by simply ignoring it rather than rejecting the whole
            // selector)
            segments.Add((compound, i == 0 ? CssCombinator.None : rawSegments[i].Combinator));
        }

        selector = new CssComplexSelector(segments);
        return true;
    }

    /// <summary>Whether <paramref name="c"/> may begin a type/class/id name.</summary>
    private static bool IsNameStartChar(char c) => char.IsLetter(c) || c is '_' or '-';

    /// <summary>Whether <paramref name="c"/> may appear anywhere else within a type/class/id name.</summary>
    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-';

    /// <summary>
    ///     Attempts to parse one compound selector's text (for example <c>rect.foo.bar#baz</c> or
    ///     <c>*</c>) into a <see cref="CssCompoundSelector"/>.
    /// </summary>
    /// <param name="text">The compound selector's text (no combinator/whitespace).</param>
    /// <param name="compound">The parsed compound selector, if this method returns <see langword="true"/>.</param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="text"/> is a well-formed type/universal/class/id
    ///     compound selector (any combination); <see langword="false"/> for anything else
    ///     (malformed, or a construct - attribute selector, pseudo-class - not recognized here at
    ///     all, defense-in-depth alongside <see cref="TryParseComplexSelector"/>'s own upfront
    ///     rejection of those characters).
    /// </returns>
    private static bool TryParseCompoundSelector(string text, out CssCompoundSelector compound)
    {
        compound = null!;
        if (text.Length == 0)
        {
            return false;
        }

        string? type = null;
        var isUniversal = false;
        var i = 0;

        if (text[0] == '*')
        {
            isUniversal = true;
            i = 1;
        }
        else if (IsNameStartChar(text[0]))
        {
            var start = i;
            while (i < text.Length && IsNameChar(text[i]))
            {
                i++;
            }

            type = text[start..i];
        }

        string? id = null;
        var classes = new List<string>();
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '.')
            {
                i++;
                var start = i;
                while (i < text.Length && IsNameChar(text[i]))
                {
                    i++;
                }

                if (i == start)
                {
                    return false;
                }

                classes.Add(text[start..i]);
            }
            else if (c == '#')
            {
                i++;
                var start = i;
                while (i < text.Length && IsNameChar(text[i]))
                {
                    i++;
                }

                if (i == start)
                {
                    return false;
                }

                id = text[start..i];
            }
            else
            {
                // Any other character (an attribute selector's own "="/quote characters, a
                // pseudo-class's argument, etc.) is unsupported syntax within a compound selector
                return false;
            }
        }

        if (type == null && !isUniversal && id == null && classes.Count == 0)
        {
            return false;
        }

        compound = new CssCompoundSelector(type, isUniversal, id, classes);
        return true;
    }

    /// <summary>
    ///     Tests whether <paramref name="element"/> alone (ignoring any ancestor chain) satisfies
    ///     every requirement of <paramref name="compound"/>.
    /// </summary>
    /// <param name="element">The element to test.</param>
    /// <param name="compound">The compound selector to test against.</param>
    /// <returns><see langword="true"/> if every requirement is satisfied.</returns>
    private static bool MatchesCompound(XElement element, CssCompoundSelector compound)
    {
        if (!compound.IsUniversal && compound.Type != null &&
            !string.Equals(element.Name.LocalName, compound.Type, StringComparison.Ordinal))
        {
            return false;
        }

        if (compound.Id != null &&
            !string.Equals((string?)element.Attribute("id"), compound.Id, StringComparison.Ordinal))
        {
            return false;
        }

        if (compound.Classes.Count == 0)
        {
            return true;
        }

        var classAttribute = (string?)element.Attribute("class");
        if (string.IsNullOrWhiteSpace(classAttribute))
        {
            return false;
        }

        var elementClasses = classAttribute.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return compound.Classes.All(required => elementClasses.Contains(required, StringComparer.Ordinal));
    }

    /// <summary>
    ///     Tests whether <paramref name="element"/> matches <paramref name="selector"/> - its final
    ///     (rightmost) compound selector against <paramref name="element"/> itself, then the
    ///     remaining (leftward) compound-selector chain against the element's ancestors via
    ///     <see cref="MatchesAncestorChain"/>, per each segment's own <see cref="CssCombinator"/>
    ///     (an exact parent match for <see cref="CssCombinator.Child"/>, a backtracking search over
    ///     every ancestor for <see cref="CssCombinator.Descendant"/> - see that method's remarks).
    ///     The parsed <see cref="XDocument"/> retains live parent links for the lifetime of one
    ///     <c>Load</c> call (see this class's remarks), so no separate parent-tracking bookkeeping
    ///     is needed here.
    /// </summary>
    /// <param name="element">The element to test.</param>
    /// <param name="selector">The selector to match.</param>
    /// <returns><see langword="true"/> if <paramref name="selector"/> matches <paramref name="element"/>.</returns>
    private static bool Matches(XElement element, CssComplexSelector selector)
    {
        var segments = selector.Segments;
        var index = segments.Count - 1;
        return MatchesCompound(element, segments[index].Compound) &&
               MatchesAncestorChain(element, segments, index);
    }

    /// <summary>
    ///     Tests whether the remaining (leftward) compound-selector chain
    ///     <c>segments[0..index-1]</c> matches some ancestor chain of <paramref name="current"/>,
    ///     given that <paramref name="current"/> already satisfies <c>segments[index]</c>.
    /// </summary>
    /// <remarks>
    ///     A <see cref="CssCombinator.Child"/> segment (<c>segments[index].Combinator</c>, the
    ///     combinator connecting <c>segments[index - 1]</c> to <c>segments[index]</c>) has exactly
    ///     one candidate - <paramref name="current"/>'s immediate parent - so a failed match there
    ///     correctly gives up immediately with no backtracking possible or required.
    ///     <para>
    ///     A <see cref="CssCombinator.Descendant"/> segment, however, has CSS descendant-combinator
    ///     semantics that require trying <b>every</b> ancestor that satisfies
    ///     <c>segments[index - 1]</c>, not just the nearest one: a selector such as
    ///     <c>g &gt; .a .target</c> must still match when the nearest <c>.a</c> ancestor is not a
    ///     direct child of a <c>g</c>, provided some farther <c>.a</c> ancestor is. Committing to
    ///     the first (nearest) matching ancestor - as an earlier, non-backtracking implementation
    ///     did - silently under-matches that case. This method therefore recurses into every
    ///     matching ancestor in turn (nearest first) and succeeds as soon as any one of them leads
    ///     to a full match of the remaining chain, backtracking (trying the next farther matching
    ///     ancestor) whenever one candidate's remaining chain fails.
    ///     </para>
    ///     <para>
    ///     This backtracking search's own worst-case cost - not merely a flat per-segment cost - is
    ///     exactly what <see cref="ComputeSelectorMatchWeight"/> charges against the
    ///     <see cref="CssMatchWorkBudget"/>, so a pathologically deep chain of consecutive
    ///     <see cref="CssCombinator.Descendant"/> segments is rejected proportionally to its real
    ///     (multiplicative, not additive) cost rather than silently permitted to run unbounded.
    ///     </para>
    /// </remarks>
    /// <param name="current">The element already known to satisfy <c>segments[index]</c>.</param>
    /// <param name="segments">The full compound/combinator segment chain (see <see cref="CssComplexSelector.Segments"/>).</param>
    /// <param name="index">
    ///     The index, within <paramref name="segments"/>, of the segment <paramref name="current"/>
    ///     already satisfies. When <c>0</c>, the entire chain is already satisfied and this method
    ///     returns <see langword="true"/> immediately with no further ancestor walk.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if the remaining chain matches some ancestor path of
    ///     <paramref name="current"/>.
    /// </returns>
    private static bool MatchesAncestorChain(
        XElement current,
        IReadOnlyList<(CssCompoundSelector Compound, CssCombinator Combinator)> segments,
        int index)
    {
        // The leftmost compound is already satisfied by "current" - the whole chain matches
        if (index == 0)
        {
            return true;
        }

        var combinator = segments[index].Combinator;
        var targetCompound = segments[index - 1].Compound;

        if (combinator == CssCombinator.Child)
        {
            // Exactly one candidate (the immediate parent) - no backtracking possible, so a failed
            // match here correctly gives up without trying any farther ancestor
            var parent = current.Parent;
            return parent != null &&
                   MatchesCompound(parent, targetCompound) &&
                   MatchesAncestorChain(parent, segments, index - 1);
        }

        // Descendant combinator: try every matching ancestor (nearest first), backtracking to the
        // next farther one whenever the remaining chain fails to match from a given candidate -
        // see this method's remarks
        return current.Ancestors().Any(ancestor =>
            MatchesCompound(ancestor, targetCompound) &&
            MatchesAncestorChain(ancestor, segments, index - 1));
    }

    /// <summary>
    ///     Computes <paramref name="selector"/>'s standard CSS specificity tuple (id count, class
    ///     count, type count), summed across every compound selector in its chain. The universal
    ///     selector (<c>*</c>) contributes zero to every component, per the CSS specification - it
    ///     is deliberately never counted toward <c>Types</c>.
    /// </summary>
    /// <param name="selector">The selector to compute specificity for.</param>
    /// <returns>The summed <c>(Ids, Classes, Types)</c> specificity tuple.</returns>
    private static (int Ids, int Classes, int Types) ComputeSpecificity(CssComplexSelector selector)
    {
        var ids = 0;
        var classes = 0;
        var types = 0;
        foreach (var (compound, _) in selector.Segments)
        {
            if (compound.Id != null)
            {
                ids++;
            }

            classes += compound.Classes.Count;

            if (compound.Type != null)
            {
                types++;
            }
        }

        return (ids, classes, types);
    }
}
