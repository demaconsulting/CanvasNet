// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness

using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    // ================================================================================================
    // CSS cascade: stylesheet construction, per-element rule matching, and 3-tier precedence
    // ================================================================================================

    /// <summary>
    ///     One retained CSS rule: a selector list (a rule matches an element if any list member
    ///     matches - per CSS, a selector list is equivalent to writing the same rule once per
    ///     selector), its declaration list, and its document-wide source order.
    /// </summary>
    /// <param name="Selectors">The rule's selector list (never empty - see <see cref="ParseStylesheet"/>).</param>
    /// <param name="Declarations">The rule's declarations (never empty - see <see cref="ParseStylesheet"/>).</param>
    /// <param name="SourceOrder">
    ///     A strictly increasing counter assigned across every <c>&lt;style&gt;</c> element in the
    ///     document combined (see <see cref="BuildStylesheet"/>), used as the cascade tiebreaker at
    ///     equal specificity - a later rule (whether from the same or a later <c>&lt;style&gt;</c>
    ///     element) always outranks an earlier one at equal specificity, exactly matching the CSS
    ///     cascade's own document-order tiebreak, with no special-case merging logic needed for
    ///     multiple <c>&lt;style&gt;</c> elements.
    /// </param>
    private sealed record CssRule(
        IReadOnlyList<CssComplexSelector> Selectors,
        IReadOnlyList<(string Property, string Value)> Declarations,
        int SourceOrder);

    /// <summary>
    ///     An immutable, whole-document CSS stylesheet: every retained rule from every
    ///     <c>&lt;style&gt;</c> element, combined, in document order.
    /// </summary>
    private sealed class CssStylesheet(IReadOnlyList<CssRule> rules)
    {
        /// <summary>The stylesheet with no rules at all - the common case for a document with no <c>&lt;style&gt;</c> element.</summary>
        public static readonly CssStylesheet Empty = new([]);

        /// <summary>Every retained rule, in document order.</summary>
        public IReadOnlyList<CssRule> Rules { get; } = rules;

        /// <summary>
        ///     The per-element <see cref="CssMatchWorkBudget"/> charge for this stylesheet,
        ///     computed exactly once here (at stylesheet-build time, not per element - see
        ///     <see cref="BuildElementContext"/>) as the sum, across every retained rule, of every
        ///     one of that rule's comma-separated selectors' own <see cref="ComputeSelectorMatchWeight"/>.
        ///     Charging this precomputed total - rather than the bare <see cref="Rules"/> count -
        ///     is what actually bounds real per-element matching CPU cost: the bare rule count is
        ///     blind to both how many comma-separated selectors a rule contributes (each one is a
        ///     fully independent match attempt - see <see cref="BuildElementContext"/>'s inner
        ///     loop) and how expensive a descendant-combinator selector's ancestor walk can be (up
        ///     to <see cref="MaxElementDepth"/> ancestors visited for a single failing match - see
        ///     <see cref="Matches"/>), which is exactly the gap a code review found let a
        ///     selectors-per-rule-heavy stylesheet drive up to a ~29x real wall-clock cost for the
        ///     identical charged budget value the bare rule-count formula produced.
        /// </summary>
        public long MatchWorkPerElement { get; } = rules.Sum(rule => rule.Selectors.Sum(ComputeSelectorMatchWeight));

        /// <summary>
        ///     Builds <paramref name="element"/>'s own <see cref="CssElementStyleContext"/>: matches
        ///     every stylesheet rule against <paramref name="element"/> once (not once per
        ///     property), resolving the winning declaration per property via specificity/source-
        ///     order (see <see cref="IsHigherOrEqualPrecedence"/>), and separately parses
        ///     <paramref name="element"/>'s own inline <c>style="..."</c> attribute, if present, via
        ///     the same <see cref="ParseDeclarationBlock"/> parser a rule's own <c>{ ... }</c> body
        ///     uses.
        /// </summary>
        /// <param name="element">The element to build a styling context for.</param>
        /// <param name="budget">
        ///     The shared cumulative CSS selector-matching work budget (see
        ///     <see cref="CssMatchWorkBudget"/>), charged once per element that actually has any
        ///     rule to match against, with this stylesheet's own precomputed
        ///     <see cref="MatchWorkPerElement"/> (not the bare <see cref="Rules"/> count - see that
        ///     property's remarks).
        /// </param>
        /// <returns>
        ///     The built context - <see cref="CssElementStyleContext.Empty"/>, with no allocation or
        ///     ancestor walk at all, whenever this stylesheet has no rules and
        ///     <paramref name="element"/> has no inline <c>style</c> attribute (the overwhelmingly
        ///     common, backward-compatible case - see this class's remarks on CSS resource
        ///     safety/performance).
        /// </returns>
        /// <exception cref="InvalidDataException">
        ///     Thrown via <paramref name="budget"/> once the cumulative CSS selector-matching work
        ///     budget is exceeded.
        /// </exception>
        public CssElementStyleContext BuildElementContext(XElement element, CssMatchWorkBudget budget)
        {
            var inlineRaw = (string?)element.Attribute("style");
            Dictionary<string, string>? inlineDeclarations = null;
            if (inlineRaw != null)
            {
                var parsed = ParseDeclarationBlock(inlineRaw);
                if (parsed.Count > 0)
                {
                    inlineDeclarations = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var (property, value) in parsed)
                    {
                        // Later declarations for the same property win, matching ordinary CSS
                        // declaration-block "last one wins" semantics
                        inlineDeclarations[property] = value;
                    }
                }
            }

            if (Rules.Count == 0)
            {
                return CssElementStyleContext.Create(null, inlineDeclarations);
            }

            budget.Charge(MatchWorkPerElement);

            Dictionary<string, string>? stylesheetDeclarations = null;
            Dictionary<string, (int Ids, int Classes, int Types, int SourceOrder)>? winning = null;

            foreach (var rule in Rules)
            {
                (int Ids, int Classes, int Types)? bestForRule = null;
                foreach (var ruleSelector in rule.Selectors)
                {
                    if (!Matches(element, ruleSelector))
                    {
                        continue;
                    }

                    var specificity = ComputeSpecificity(ruleSelector);
                    if (bestForRule == null || IsHigherSpecificity(specificity, bestForRule.Value))
                    {
                        bestForRule = specificity;
                    }
                }

                if (bestForRule == null)
                {
                    continue;
                }

                var (ids, classes, types) = bestForRule.Value;
                stylesheetDeclarations ??= new Dictionary<string, string>(StringComparer.Ordinal);
                winning ??= new Dictionary<string, (int, int, int, int)>(StringComparer.Ordinal);

                foreach (var (property, value) in rule.Declarations)
                {
                    var candidate = (ids, classes, types, rule.SourceOrder);
                    if (!winning.TryGetValue(property, out var current) || IsHigherOrEqualPrecedence(candidate, current))
                    {
                        winning[property] = candidate;
                        stylesheetDeclarations[property] = value;
                    }
                }
            }

            return CssElementStyleContext.Create(stylesheetDeclarations, inlineDeclarations);
        }
    }

    /// <summary>
    ///     Builds <paramref name="root"/>'s whole-document CSS stylesheet: walks every
    ///     <c>&lt;style&gt;</c> element in document order (mirroring <c>BuildIdIndex</c>'s identical
    ///     whole-document pre-pass pattern), honors each one's own <c>type</c> attribute (only
    ///     absent or <c>text/css</c> is parsed as CSS - any other non-blank value is opaque and
    ///     skipped entirely, per the CSS/SVG specification), and assigns a strictly increasing
    ///     <see cref="CssRule.SourceOrder"/> across every retained rule combined, so multiple
    ///     <c>&lt;style&gt;</c> elements merge into one cascade for free (see
    ///     <see cref="CssRule.SourceOrder"/>'s remarks).
    /// </summary>
    /// <param name="root">The document's root element.</param>
    /// <returns>
    ///     The built stylesheet - <see cref="CssStylesheet.Empty"/> when the document has no
    ///     <c>&lt;style&gt;</c> element (or none of them contain any retainable rule), so
    ///     <see cref="CssStylesheet.BuildElementContext"/> can cheaply short-circuit for the overwhelmingly common
    ///     backward-compatible case (see this class's remarks on CSS resource safety/performance).
    /// </returns>
    private static CssStylesheet BuildStylesheet(XElement root)
    {
        var rules = new List<CssRule>();
        var sourceOrder = 0;

        foreach (var styleElement in root.DescendantsAndSelf().Where(e => e.Name.LocalName == "style"))
        {
            if (rules.Count >= MaxCssRules)
            {
                break;
            }

            var type = (string?)styleElement.Attribute("type");
            if (!string.IsNullOrWhiteSpace(type) &&
                !string.Equals(type.Trim(), "text/css", StringComparison.OrdinalIgnoreCase))
            {
                // A non-CSS type (e.g. "text/less") is opaque to this codec - skip its content
                // entirely rather than misinterpreting it as CSS
                continue;
            }

            var cssText = styleElement.Value;
            if (string.IsNullOrWhiteSpace(cssText))
            {
                continue;
            }

            foreach (var (selectors, declarations) in ParseStylesheet(cssText))
            {
                if (rules.Count >= MaxCssRules)
                {
                    break;
                }

                rules.Add(new CssRule(selectors, declarations, sourceOrder++));
            }
        }

        return rules.Count == 0 ? CssStylesheet.Empty : new CssStylesheet(rules);
    }

    /// <summary>
    ///     Computes <paramref name="selector"/>'s own worst-case per-element matching cost, used by
    ///     <see cref="CssStylesheet.MatchWorkPerElement"/> to build a <see cref="CssMatchWorkBudget"/>
    ///     charge that genuinely tracks real CPU cost rather than merely counting rules (see that
    ///     property's remarks): matching a selector against one element always tests its rightmost
    ///     compound directly against the element (a fixed, cheap <c>O(1)</c> cost - the base
    ///     <c>1</c> below), then walks backward through any further (leftward) compound/combinator
    ///     pair (see <see cref="Matches"/>). A <see cref="CssCombinator.Child"/> pair costs another
    ///     fixed <c>O(1)</c> (an exact <c>.Parent</c> comparison), but a
    ///     <see cref="CssCombinator.Descendant"/> pair can, on a failing match, walk every one of
    ///     the element's ancestors before giving up - up to <see cref="MaxElementDepth"/> of them,
    ///     the codec's own hard structural ceiling on ancestor-chain length - so it is charged at
    ///     that same worst-case weight rather than a flat <c>1</c>, directly tying this charge to
    ///     the exact mechanism a code review found let a descendant-combinator-heavy selector drive
    ///     real wall-clock cost far beyond what a naive per-selector <c>1</c> would ever charge.
    /// </summary>
    /// <param name="selector">The selector to weigh.</param>
    /// <returns>
    ///     The selector's own worst-case per-element match cost: <c>1</c> for its rightmost
    ///     compound, plus <c>1</c> for every further <see cref="CssCombinator.Child"/> segment, plus
    ///     <see cref="MaxElementDepth"/> for every further <see cref="CssCombinator.Descendant"/>
    ///     segment.
    /// </returns>
    private static long ComputeSelectorMatchWeight(CssComplexSelector selector)
    {
        var segments = selector.Segments;

        // The rightmost compound is always matched directly against the element itself - a fixed,
        // cheap cost regardless of combinator
        var weight = 1L;

        // Every further (leftward) compound/combinator pair adds its own worst-case cost: a Child
        // combinator is a single exact parent comparison, while a Descendant combinator can walk
        // up to MaxElementDepth ancestors before a failing match gives up (see Matches)
        for (var i = 1; i < segments.Count; i++)
        {
            weight += segments[i].Combinator == CssCombinator.Child ? 1 : MaxElementDepth;
        }

        return weight;
    }

    /// <summary>
    ///     One element's own pre-matched CSS styling context: its winning stylesheet declaration
    ///     per property (already resolved via specificity/source-order - see
    ///     <see cref="CssStylesheet.BuildElementContext"/>) and its own inline <c>style="..."</c> declarations,
    ///     if any. <see cref="ResolveStyledValue"/> is the sole reader of this type.
    /// </summary>
    private sealed class CssElementStyleContext
    {
        /// <summary>The context for an element with no matching stylesheet rule and no inline <c>style</c> attribute.</summary>
        public static readonly CssElementStyleContext Empty = new(null, null);

        /// <summary>The element's winning stylesheet declarations, or <see langword="null"/> if none matched.</summary>
        private readonly IReadOnlyDictionary<string, string>? _stylesheetDeclarations;

        /// <summary>The element's own parsed inline <c>style</c> declarations, or <see langword="null"/> if absent/empty.</summary>
        private readonly IReadOnlyDictionary<string, string>? _inlineDeclarations;

        private CssElementStyleContext(
            IReadOnlyDictionary<string, string>? stylesheetDeclarations,
            IReadOnlyDictionary<string, string>? inlineDeclarations)
        {
            _stylesheetDeclarations = stylesheetDeclarations;
            _inlineDeclarations = inlineDeclarations;
        }

        /// <summary>Builds a context carrying the given pre-resolved declaration dictionaries.</summary>
        /// <param name="stylesheetDeclarations">The element's winning stylesheet declarations, or <see langword="null"/> if none matched.</param>
        /// <param name="inlineDeclarations">The element's own parsed inline <c>style</c> declarations, or <see langword="null"/> if absent/empty.</param>
        /// <returns>The built context.</returns>
        public static CssElementStyleContext Create(
            IReadOnlyDictionary<string, string>? stylesheetDeclarations,
            IReadOnlyDictionary<string, string>? inlineDeclarations) =>
            stylesheetDeclarations == null && inlineDeclarations == null
                ? Empty
                : new CssElementStyleContext(stylesheetDeclarations, inlineDeclarations);

        /// <summary>
        ///     Resolves <paramref name="property"/>'s effective CSS-cascaded value, implementing
        ///     this class's 3-tier precedence's upper two tiers: an inline <c>style="..."</c>
        ///     declaration (unconditionally highest) beats a matching stylesheet rule declaration,
        ///     which beats returning <see langword="null"/> so the caller falls through to reading
        ///     the plain presentation attribute (the lowest tier).
        /// </summary>
        /// <param name="property">The lower-case CSS property name to resolve.</param>
        /// <returns>
        ///     The winning raw (still-unparsed) declaration value, or <see langword="null"/> if
        ///     neither tier has a value for <paramref name="property"/>.
        /// </returns>
        public string? Resolve(string property)
        {
            if (_inlineDeclarations != null && _inlineDeclarations.TryGetValue(property, out var inlineValue))
            {
                return inlineValue;
            }

            return _stylesheetDeclarations != null && _stylesheetDeclarations.TryGetValue(property, out var stylesheetValue)
                ? stylesheetValue
                : null;
        }
    }

    /// <summary>
    ///     Tracks the cumulative CSS selector-matching work charged across a single <c>Load</c>
    ///     call, throwing once a fixed cumulative budget is exceeded - mirrors
    ///     <see cref="GeometryWorkBudget"/>'s identical "mutable reference type, charged
    ///     incrementally, hard document-rejection ceiling" pattern and rationale. Naive per-element
    ///     rule matching costs <c>O(elementCount * ruleCount)</c> in the worst case, but that alone
    ///     understates the real cost: a single rule's own comma-separated selector list (up to
    ///     <see cref="MaxCssSelectorsPerRule"/> independent match attempts) and a single selector's
    ///     own descendant-combinator ancestor walk (up to <see cref="MaxElementDepth"/> ancestors
    ///     visited on a failing match) both multiply the true per-element cost far beyond what a
    ///     bare rule count reflects - a code review found this let a selectors-per-rule-heavy
    ///     stylesheet drive a ~29x real wall-clock difference for the identical charged budget
    ///     value a bare-rule-count formula produced. Every charge therefore uses each stylesheet's
    ///     own precomputed <see cref="CssStylesheet.MatchWorkPerElement"/> (see
    ///     <see cref="ComputeSelectorMatchWeight"/>), not <see cref="CssStylesheet.Rules"/>'s bare
    ///     count, so this budget genuinely tracks real CPU cost - a cost dimension no other
    ///     existing budget (<see cref="MaxTotalRenderedElements"/> counts elements only;
    ///     <see cref="MaxCssRules"/> counts rules only) actually bounds.
    /// </summary>
    private sealed class CssMatchWorkBudget
    {
        /// <summary>
        ///     The maximum combined per-element-weighted CSS selector-matching work this codec will
        ///     charge across a single <c>Load</c> call, before rejecting the document with
        ///     <see cref="InvalidDataException"/> - a genuine resource bound, not a performance
        ///     optimization (see this class's remarks): a document that legitimately needs more
        ///     total matching work than this budget allows is rejected the same way an oversized
        ///     <c>path</c> <c>d</c> attribute already is. <c>2,000,000</c> comfortably covers, for
        ///     example, a document with <see cref="MaxCssRules"/> (2,000) simple (single-selector,
        ///     single-compound) rules - each weighing exactly <c>1</c>, see
        ///     <see cref="ComputeSelectorMatchWeight"/> - combined with 1,000 elements (2,000,000
        ///     exactly), preserving this codec's original, pre-existing headroom for that common
        ///     case unchanged, while a selector-list- or descendant-combinator-heavy stylesheet
        ///     (whose per-element weight is proportionally larger - up to
        ///     <see cref="MaxCssSelectorsPerRule"/> times <see cref="MaxElementDepth"/> per rule in
        ///     the worst case) is now rejected proportionally sooner, exactly tracking its
        ///     genuinely higher real cost rather than silently passing the identical check a simple
        ///     stylesheet would.
        /// </summary>
        private const long MaxCumulativeCssMatchWorkUnits = 2_000_000L;

        /// <summary>The running total of CSS selector-matching work charged so far.</summary>
        private long _total;

        /// <summary>
        ///     Charges <paramref name="amount"/> units of selector-matching work (one stylesheet's
        ///     own precomputed <see cref="CssStylesheet.MatchWorkPerElement"/>, charged once per
        ///     element that has any rule to match against - see
        ///     <see cref="CssStylesheet.BuildElementContext"/>) against the running total.
        /// </summary>
        /// <param name="amount">The weighted selector-matching work just performed against one element.</param>
        /// <exception cref="InvalidDataException">
        ///     Thrown once the cumulative total exceeds <see cref="MaxCumulativeCssMatchWorkUnits"/>.
        /// </exception>
        public void Charge(long amount)
        {
            // Check before adding, mirroring GeometryWorkBudget.Charge's identical
            // overflow-avoidance rationale
            if (amount > MaxCumulativeCssMatchWorkUnits - _total)
            {
                throw new InvalidDataException(
                    "SVG document resolves to too much total CSS selector-matching work.");
            }

            _total += amount;
        }
    }

    /// <summary>
    ///     Determines whether specificity/source-order tuple <paramref name="candidate"/> should
    ///     replace <paramref name="current"/> as a property's winning stylesheet declaration -
    ///     standard CSS specificity comparison (ids, then classes, then types), with document order
    ///     (<paramref name="candidate"/>'s own rule being later wins ties) as the final tiebreaker.
    /// </summary>
    /// <param name="candidate">The newly-considered declaration's specificity/source-order.</param>
    /// <param name="current">The currently-winning declaration's specificity/source-order.</param>
    /// <returns><see langword="true"/> if <paramref name="candidate"/> should win.</returns>
    private static bool IsHigherOrEqualPrecedence(
        (int Ids, int Classes, int Types, int SourceOrder) candidate,
        (int Ids, int Classes, int Types, int SourceOrder) current)
    {
        if (candidate.Ids != current.Ids)
        {
            return candidate.Ids > current.Ids;
        }

        if (candidate.Classes != current.Classes)
        {
            return candidate.Classes > current.Classes;
        }

        if (candidate.Types != current.Types)
        {
            return candidate.Types > current.Types;
        }

        // Equal specificity: the later rule (in whole-document order, spanning every merged
        // <style> element - see CssRule.SourceOrder's remarks) wins
        return candidate.SourceOrder >= current.SourceOrder;
    }

    /// <summary>
    ///     Determines whether specificity tuple <paramref name="candidate"/> outranks
    ///     <paramref name="current"/> - used only to pick the highest specificity among several
    ///     comma-list selectors of the same rule that all match the same element (per CSS, a
    ///     selector list behaves as though the rule were declared once per selector, so the
    ///     specificity of whichever selector actually matched applies).
    /// </summary>
    /// <param name="candidate">The newly-considered selector's specificity.</param>
    /// <param name="current">The currently-highest matching selector's specificity.</param>
    /// <returns><see langword="true"/> if <paramref name="candidate"/> outranks <paramref name="current"/>.</returns>
    private static bool IsHigherSpecificity(
        (int Ids, int Classes, int Types) candidate,
        (int Ids, int Classes, int Types) current)
    {
        if (candidate.Ids != current.Ids)
        {
            return candidate.Ids > current.Ids;
        }

        return candidate.Classes != current.Classes ? candidate.Classes > current.Classes : candidate.Types > current.Types;
    }

    /// <summary>
    ///     The single chokepoint resolving one cascaded property's effective value across this
    ///     class's 3-tier precedence (see this class's remarks): an inline <c>style="..."</c>
    ///     declaration (highest), any matching stylesheet rule declaration (middle), or - by
    ///     returning <see langword="null"/> - the plain presentation attribute (lowest), read
    ///     directly by the caller. Every tier hands its raw (still-unparsed) value string to the
    ///     exact same value parser <c>ApplyPresentationAttributes</c> already uses for a plain
    ///     presentation attribute - this method (and the CSS engine as a whole) never itself
    ///     understands SVG paint/numeric/keyword syntax, only property/value token boundaries (see
    ///     this class's remarks on CSS engine scope).
    /// </summary>
    /// <param name="styleContext">The element's own pre-built <see cref="CssElementStyleContext"/> (see <see cref="CssStylesheet.BuildElementContext"/>).</param>
    /// <param name="property">The lower-case CSS/presentation property name to resolve (for example <c>"fill"</c>).</param>
    /// <returns>
    ///     The winning raw declaration value from the inline or stylesheet tier, or
    ///     <see langword="null"/> if neither tier has a value - the caller then reads the plain
    ///     presentation attribute instead.
    /// </returns>
    private static string? ResolveStyledValue(CssElementStyleContext styleContext, string property) =>
        styleContext.Resolve(property);
}
