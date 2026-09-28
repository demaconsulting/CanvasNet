// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness

using System.Text;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    // ================================================================================================
    // CSS tokenizing/parsing (hand-rolled, dependency-free, mirroring this class's own SVG parsing)
    // ================================================================================================

    /// <summary>
    ///     The maximum number of CSS rules (across every <c>&lt;style&gt;</c> element in a single
    ///     document, combined) this codec's stylesheet cascade retains, before silently ignoring
    ///     any further rule - a resource-safety cap, not a document-rejection condition (see this
    ///     class's malformed-CSS remarks: an over-budget stylesheet still renders, just with a
    ///     truncated rule set, exactly like this class's tolerant handling of a malformed
    ///     individual rule). <c>2,000</c> is consistent in order of magnitude with
    ///     <see cref="MaxFilterPrimitivesPerFilter"/> (1,000)/<see cref="MaxClipPathShapesPerClipPath"/>
    ///     (256) - far beyond any real-world hand-authored or tool-exported SVG stylesheet's rule
    ///     count, while keeping the total document-wide rule set this codec ever matches against
    ///     bounded to a small, practical maximum.
    /// </summary>
    private const int MaxCssRules = 2_000;

    /// <summary>
    ///     The maximum number of comma-separated selectors a single CSS rule's selector list may
    ///     contribute, before any further selector in that same list is silently dropped (the rule
    ///     itself, and every selector already accepted, is still retained - only the excess list
    ///     members are ignored). Bounds one rule's own selector-list matching cost independently of
    ///     <see cref="MaxCssRules"/>'s total-rule-count cap, mirroring
    ///     <see cref="MaxConvolveMatrixOrder"/>'s "bound one construct's own complexity separately
    ///     from the total-count cap" precedent. <c>64</c> is far beyond any real-world stylesheet's
    ///     selector-list length (rarely more than a handful of selectors sharing one declaration
    ///     block).
    /// </summary>
    private const int MaxCssSelectorsPerRule = 64;

    /// <summary>
    ///     The maximum number of compound-selector segments (separated by a descendant or child
    ///     combinator) a single complex selector may chain together, before that one selector -
    ///     not the whole rule, nor the whole selector list - is silently dropped, exactly like this
    ///     class's existing sibling-combinator/pseudo-class selector-drop policy (see this class's
    ///     remarks and <c>SvgCodec.Css.Selectors.cs</c>). <c>16</c> is far beyond any real-world
    ///     descendant/child combinator chain depth (rarely more than two or three levels), while
    ///     bounding a single selector's own ancestor-walk matching cost.
    /// </summary>
    private const int MaxCssCombinatorSegmentsPerSelector = 16;

    /// <summary>
    ///     The maximum number of declarations a single CSS rule's body (or, reusing the same
    ///     <see cref="ParseDeclarationBlock"/> parser, a single inline <c>style="..."</c>
    ///     attribute) may contribute, before any further declaration is silently dropped - the rule
    ///     itself, and every declaration already parsed, is still retained. <c>256</c> is far beyond
    ///     any real-world rule/inline-style declaration count (this codec recognizes at most ~19
    ///     cascaded presentation properties in total - see <c>ApplyPresentationAttributes</c>),
    ///     while bounding a single pathological declaration block's own parsing cost.
    /// </summary>
    private const int MaxCssDeclarationsPerRule = 256;

    /// <summary>
    ///     Removes every <c>/* ... */</c> CSS comment from <paramref name="css"/>, tolerating an
    ///     unterminated trailing comment (the remainder of the text from the unterminated
    ///     <c>/*</c> onward is simply dropped, rather than throwing) - consistent with this
    ///     method's overall "never abort, degrade gracefully" malformed-CSS policy (see this
    ///     class's remarks).
    /// </summary>
    /// <param name="css">The raw CSS text.</param>
    /// <returns><paramref name="css"/> with every comment removed.</returns>
    private static string StripComments(string css)
    {
        var builder = new StringBuilder(css.Length);
        var i = 0;
        while (i < css.Length)
        {
            if (i + 1 < css.Length && css[i] == '/' && css[i + 1] == '*')
            {
                var end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end == -1 ? css.Length : end + 2;
                continue;
            }

            builder.Append(css[i]);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Splits <paramref name="text"/> on every top-level occurrence of <paramref name="separator"/>,
    ///     ignoring an occurrence nested inside balanced parentheses (so a <c>url(...)</c> value's
    ///     own internal characters, whatever they are, can never be mistaken for a declaration
    ///     separator or a selector-list comma). Shared by both <see cref="ParseDeclarationBlock"/>
    ///     (splitting on <c>;</c>) and selector-list splitting (splitting on <c>,</c>), so both call
    ///     sites reuse one parenthesis-aware splitting rule rather than each duplicating it.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="separator">The top-level separator character.</param>
    /// <returns>The split segments, in order, none of which includes the separator itself.</returns>
    private static List<string> SplitTopLevel(string text, char separator)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '(':
                    depth++;
                    break;

                case ')':
                    if (depth > 0)
                    {
                        depth--;
                    }

                    break;

                default:
                    if (text[i] == separator && depth == 0)
                    {
                        result.Add(text[start..i]);
                        start = i + 1;
                    }

                    break;
            }
        }

        result.Add(text[start..]);
        return result;
    }

    /// <summary>
    ///     Finds the index of the <c>}</c> that closes the <c>{</c> at <paramref name="openIndex"/>,
    ///     counting nested <c>{</c>/<c>}</c> pairs so an at-rule block (for example <c>@media { ... }</c>)
    ///     containing further rule blocks of its own is skipped as one unit rather than stopping at
    ///     the first inner <c>}</c>.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <param name="openIndex">The index of the opening <c>{</c>.</param>
    /// <returns>
    ///     The index of the matching closing <c>}</c>, or <c>-1</c> if <paramref name="text"/> ends
    ///     before the braces balance (an unterminated block - tolerated by the caller via
    ///     resynchronization, see <see cref="ParseRawRules"/>'s remarks for a plain rule body, or
    ///     via a simple "skip to end" fallback for an at-rule).
    /// </returns>
    private static int FindMatchingBrace(string text, int openIndex)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    ///     Splits <paramref name="css"/> (already known to be a <c>&lt;style&gt;</c> element's own
    ///     text content) into raw <c>(selectorText, declarationText)</c> rule pairs, skipping every
    ///     at-rule (<c>@media</c> and any other <c>@</c>-prefixed construct - out of scope, see this
    ///     class's remarks) as one balanced-brace (or, for a bodiless at-rule such as
    ///     <c>@import "x";</c>, semicolon-terminated) unit. Never throws: any malformed trailing
    ///     content simply narrows what is recovered, discarding only the unparseable part - never
    ///     the whole stylesheet or document (see this class's remarks).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Unterminated-rule resynchronization (exact behavior).</b> A rule body missing its
    ///         closing <c>}</c> is dropped (it can never be recovered as a well-formed rule), but -
    ///         critically - this must not also discard every later rule in the stylesheet: naive
    ///         brace-depth counting alone cannot tell "an at-rule's own legitimately nested block"
    ///         apart from "an unrelated later rule's <c>{</c>/<c>}</c> pair, wrongly absorbed into
    ///         the unterminated rule's depth count because it never returned to zero." When
    ///         <see cref="FindMatchingBrace"/> fails to find a rule's own closing brace, this method
    ///         searches for the next <c>{</c> anywhere after the failed one (the earliest point a
    ///         fresh rule could plausibly start) via <see cref="FindSelectorRecoveryStart"/>, and
    ///         resumes scanning there - so a well-formed rule occurring after a single malformed one
    ///         is still parsed and applied, at the cost of not being able to recover a full
    ///         (possibly combinator-chained) selector for the immediately-following rule if it sat
    ///         directly adjacent to the malformed one with no unambiguous boundary between them (see
    ///         <see cref="FindSelectorRecoveryStart"/>'s remarks) - strictly better than this
    ///         method's previous behavior of discarding every rule in the entire remainder of the
    ///         stylesheet.
    ///     </para>
    ///     <para>
    ///         If no further <c>{</c> exists anywhere in the remaining text either, there is truly
    ///         nothing left to recover, and the scan ends there exactly as before.
    ///     </para>
    /// </remarks>
    /// <param name="css">The raw CSS text (a single <c>&lt;style&gt;</c> element's text content).</param>
    /// <returns>The raw rule pairs found, in document order.</returns>
    private static List<(string SelectorText, string DeclarationText)> ParseRawRules(string css)
    {
        var stripped = StripComments(css);
        var rules = new List<(string, string)>();
        var i = 0;
        var n = stripped.Length;
        while (i < n)
        {
            while (i < n && char.IsWhiteSpace(stripped[i]))
            {
                i++;
            }

            if (i >= n)
            {
                break;
            }

            if (stripped[i] == '@')
            {
                // An at-rule (@media, @import, @font-face, ...) is entirely out of scope for this
                // phase's static-rendering CSS engine (see this class's remarks) - skip it as a
                // single unit, whether it has a semicolon-terminated body (e.g. @import "x";) or a
                // brace-delimited block (which may itself contain further nested rule blocks, so
                // FindMatchingBrace's depth-aware scan is required, not a naive first-'}' search)
                var braceIdx = stripped.IndexOf('{', i);
                var semiIdx = stripped.IndexOf(';', i);
                if (braceIdx == -1 && semiIdx == -1)
                {
                    // Trailing malformed at-rule with no terminator at all - nothing further to
                    // recover; stop scanning rather than looping forever
                    break;
                }

                if (braceIdx == -1 || (semiIdx != -1 && semiIdx < braceIdx))
                {
                    i = semiIdx + 1;
                    continue;
                }

                var atRuleClose = FindMatchingBrace(stripped, braceIdx);
                i = atRuleClose == -1 ? n : atRuleClose + 1;
                continue;
            }

            var openBrace = stripped.IndexOf('{', i);
            if (openBrace == -1)
            {
                // Trailing content with no rule body at all (e.g. a dangling selector with no
                // "{...}") - nothing further to recover; stop scanning
                break;
            }

            var selectorText = stripped[i..openBrace];
            var closeBrace = FindMatchingBrace(stripped, openBrace);
            if (closeBrace == -1)
            {
                // Unterminated rule body (see this method's remarks for the exact
                // resynchronization strategy applied here) - resync onto the next rule attempt
                // rather than discarding the rest of the stylesheet
                var nestedOpen = stripped.IndexOf('{', openBrace + 1);
                if (nestedOpen == -1)
                {
                    // No further '{' exists anywhere in the remaining text either - there is
                    // truly nothing left to recover; stop scanning
                    break;
                }

                i = FindSelectorRecoveryStart(stripped, nestedOpen);
                continue;
            }

            rules.Add((selectorText, stripped[(openBrace + 1)..closeBrace]));
            i = closeBrace + 1;
        }

        return rules;
    }

    /// <summary>
    ///     Locates where a fresh rule attempt's own selector text should be considered to start,
    ///     given that it was discovered by resynchronizing past a preceding unterminated rule (see
    ///     <see cref="ParseRawRules"/>'s remarks) - the opening <c>{</c> at <paramref name="braceIndex"/>
    ///     is the only reliable anchor available, since the unterminated rule swallowed any real
    ///     rule/declaration boundary that might otherwise mark where its own malformed content
    ///     ends and the next rule's selector begins. This walks backward from
    ///     <paramref name="braceIndex"/> over a single trailing compound-selector-like token (type/
    ///     class/id/universal-selector characters only, matching <see cref="TryParseCompoundSelector"/>'s
    ///     own accepted character set) with no intervening whitespace, deliberately not attempting
    ///     to recover a whitespace/<c>&gt;</c>-joined combinator chain: any such chain would itself
    ///     already have been swallowed by the preceding unterminated rule's own (ambiguous, already
    ///     discarded) content, so recovering only the single token immediately adjacent to the
    ///     brace is the one unambiguous, safely-recoverable choice - anything further back cannot be
    ///     distinguished from the abandoned rule's own malformed declaration text (for example
    ///     <c>red</c> in <c>fill: red</c>) with certainty.
    /// </summary>
    /// <param name="text">The text being scanned (already comment-stripped).</param>
    /// <param name="braceIndex">The index of the candidate new rule's own opening <c>{</c>.</param>
    /// <returns>
    ///     The index where the recovered selector text begins - equal to <paramref name="braceIndex"/>
    ///     itself (an empty, and therefore rule-dropping, selector - see <see cref="ParseSelectorList"/>)
    ///     if no such token exists immediately before it.
    /// </returns>
    private static int FindSelectorRecoveryStart(string text, int braceIndex)
    {
        var pos = braceIndex;

        // Skip whitespace directly before the brace (e.g. the space in "rect {")
        while (pos > 0 && char.IsWhiteSpace(text[pos - 1]))
        {
            pos--;
        }

        // Consume backward over the single trailing compound-selector-like token - stopping at
        // the first character that cannot appear in one (whitespace, ':', ';', the preceding
        // rule's own dangling '{', etc.), so only that one token is recovered, not anything
        // further back
        while (pos > 0 && (IsNameChar(text[pos - 1]) || text[pos - 1] is '.' or '#' or '*'))
        {
            pos--;
        }

        return pos;
    }

    /// <summary>
    ///     Tolerantly strips a trailing <c>!important</c> modifier from a declaration's value,
    ///     applying the value at its normal (non-elevated) cascade tier rather than throwing or
    ///     dropping the whole declaration - a documented, deliberate simplification (see this
    ///     class's remarks): a dedicated <c>!important</c> cascade tier is out of scope for this
    ///     phase, judged disproportionate complexity for a rarely-used feature in static,
    ///     tool-exported SVG artwork.
    /// </summary>
    /// <param name="value">The declaration's raw value text (already trimmed of surrounding whitespace).</param>
    /// <returns><paramref name="value"/> with a trailing <c>!important</c> (case-insensitive) removed.</returns>
    private static string StripImportant(string value)
    {
        const string marker = "!important";
        var trimmed = value.TrimEnd();
        return trimmed.Length >= marker.Length &&
               trimmed[^marker.Length..].Equals(marker, StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^marker.Length].TrimEnd()
            : trimmed;
    }

    /// <summary>
    ///     Parses a single declaration block's raw text - shared by both a CSS rule's own
    ///     <c>{ ... }</c> body (see <see cref="ParseRawRules"/>) and an inline <c>style="..."</c>
    ///     attribute's raw value, so both declaration-block sources are parsed by exactly one
    ///     implementation. Declaration values are kept entirely opaque strings: this parser only
    ///     ever finds the property/value token boundary (the first top-level <c>:</c>) - it never
    ///     itself understands SVG paint syntax, handing the raw value string to the exact same
    ///     value parser (<c>ParseFillRule</c>/<c>GetOptionalFloat</c>/etc.) presentation attributes
    ///     already use (see this class's remarks). A malformed individual declaration (missing
    ///     <c>:</c>, or an empty property/value) is silently skipped, resuming at the next
    ///     <c>;</c>-delimited segment, never aborting the rest of the block.
    /// </summary>
    /// <param name="text">The declaration block's raw text.</param>
    /// <returns>
    ///     The parsed <c>(property, value)</c> pairs, in document order, with the property name
    ///     lower-cased (CSS property names are ASCII case-insensitive) and any trailing
    ///     <c>!important</c> stripped from the value - capped at <see cref="MaxCssDeclarationsPerRule"/>
    ///     entries (see that constant's remarks).
    /// </returns>
    private static List<(string Property, string Value)> ParseDeclarationBlock(string text)
    {
        var result = new List<(string, string)>();
        foreach (var segment in SplitTopLevel(StripComments(text), ';'))
        {
            if (result.Count >= MaxCssDeclarationsPerRule)
            {
                break;
            }

            var trimmed = segment.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0 || colonIndex == trimmed.Length - 1)
            {
                // No ':' at all, a ':' with nothing before it, or a ':' with nothing after it - a
                // malformed declaration; skip it and resume at the next ';'-delimited segment
                continue;
            }

            var property = trimmed[..colonIndex].Trim().ToLowerInvariant();
            var value = StripImportant(trimmed[(colonIndex + 1)..].Trim());
            if (property.Length == 0 || value.Length == 0)
            {
                continue;
            }

            result.Add((property, value));
        }

        return result;
    }

    /// <summary>
    ///     Parses a whole <c>&lt;style&gt;</c> element's text content as CSS, returning every rule
    ///     whose selector list retains at least one supported selector (see
    ///     <c>SvgCodec.Css.Selectors.cs</c>'s <c>ParseSelectorList</c>) and whose declaration block
    ///     yields at least one well-formed declaration. A rule with no remaining selectors (every
    ///     comma-list member used an unsupported sibling combinator/pseudo-class) or no well-formed
    ///     declarations at all contributes nothing and is silently dropped - it can never match
    ///     anything or apply anything, so retaining it would only waste later matching work.
    /// </summary>
    /// <param name="cssText">The raw CSS text.</param>
    /// <returns>
    ///     Each retained rule's selector list and declaration list, in document order. The caller
    ///     (<c>BuildStylesheet</c>, in <c>SvgCodec.Css.Cascade.cs</c>) assigns each one a
    ///     document-wide <c>SourceOrder</c> and enforces <see cref="MaxCssRules"/>.
    /// </returns>
    private static List<(IReadOnlyList<CssComplexSelector> Selectors, IReadOnlyList<(string Property, string Value)> Declarations)> ParseStylesheet(string cssText)
    {
        var result = new List<(IReadOnlyList<CssComplexSelector>, IReadOnlyList<(string, string)>)>();
        foreach (var (selectorText, declarationText) in ParseRawRules(cssText))
        {
            var selectors = ParseSelectorList(selectorText);
            if (selectors.Count == 0)
            {
                continue;
            }

            var declarations = ParseDeclarationBlock(declarationText);
            if (declarations.Count == 0)
            {
                continue;
            }

            result.Add((selectors, declarations));
        }

        return result;
    }
}
