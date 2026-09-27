// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore unblurred premult
// cspell:ignore aliceblue antiquewhite blanchedalmond blueviolet burlywood cadetblue cornflowerblue
// cspell:ignore cornsilk darkcyan darkgoldenrod darkgray darkgreen darkgrey darkkhaki darkmagenta
// cspell:ignore darkolivegreen darkorange darkorchid darkred darksalmon darkseagreen darkslateblue
// cspell:ignore darkslategray darkslategrey darkturquoise darkviolet deeppink deepskyblue dimgray
// cspell:ignore dimgrey dodgerblue floralwhite forestgreen gainsboro ghostwhite greenyellow hotpink
// cspell:ignore indianred lavenderblush lawngreen lemonchiffon lightcoral lightcyan
// cspell:ignore lightgoldenrodyellow lightgray lightgreen lightpink lightsalmon lightseagreen
// cspell:ignore lightskyblue lightslategray lightslategrey lightsteelblue lightyellow limegreen
// cspell:ignore mediumaquamarine mediumblue mediumorchid mediumpurple mediumseagreen mediumslateblue
// cspell:ignore mediumspringgreen mediumturquoise mediumvioletred midnightblue mintcream mistyrose
// cspell:ignore navajowhite oldlace olivedrab orangered palegoldenrod palegreen paleturquoise
// cspell:ignore palevioletred papayawhip peachpuff powderblue rebeccapurple rosybrown royalblue
// cspell:ignore saddlebrown sandybrown seagreen skyblue slateblue slategray slategrey springgreen
// cspell:ignore steelblue whitesmoke yellowgreen
using System.Globalization;
using System.Numerics;
using System.Xml;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    /// <summary>
    ///     The XML namespace SVG documents use to qualify <c>href</c> attributes under their
    ///     legacy SVG 1.1 name (<c>xlink:href</c>), still common in real-world documents alongside
    ///     the unprefixed SVG 2 <c>href</c> attribute this codec also recognizes.
    /// </summary>
    private static readonly XNamespace XlinkNamespace = "http://www.w3.org/1999/xlink";

    /// <summary>
    ///     The maximum number of nested <c>use</c> references this codec follows before giving up,
    ///     guarding against a reference cycle (direct or indirect self-reference) that would
    ///     otherwise recurse indefinitely.
    /// </summary>
    private const int MaxUseDepth = 32;

    /// <summary>
    ///     The maximum number of nested <c>marker</c> references this codec follows before giving
    ///     up, guarding against a reference cycle (a <c>marker</c> whose own content references
    ///     itself, directly or indirectly, via <c>marker-start</c>/<c>marker-mid</c>/
    ///     <c>marker-end</c>) that would otherwise recurse indefinitely. Mirrors
    ///     <see cref="MaxUseDepth"/>'s value and rationale: a marker cycle is structurally the
    ///     same kind of id-resolved recursive re-entry into <see cref="RenderElement"/> as a
    ///     <c>use</c> cycle, so it is guarded the same way, with its own independent counter
    ///     rather than sharing <see cref="MaxUseDepth"/> or relying solely on
    ///     <see cref="MaxElementDepth"/> (a marker reference is not a <c>use</c> reference, and
    ///     conflating the two budgets would make an unrelated document's <c>use</c> nesting depth
    ///     affect how many marker references a separate part of the same document may chain).
    /// </summary>
    private const int MaxMarkerDepth = 32;

    /// <summary>
    ///     The maximum pixel-space effective <c>stdDeviation</c> a <c>feGaussianBlur</c> filter
    ///     primitive (see <see cref="ApplyFeGaussianBlur"/>) may use, after scaling the raw
    ///     local-space value by <see cref="EstimateUniformScale"/>. This codec's box-blur
    ///     approximation (see <see cref="ApplyFeGaussianBlur"/>'s remarks) uses a box radius of
    ///     roughly <c>1.88 * stdDeviation</c>, so <c>250</c> caps a single pass's radius at
    ///     roughly 470 pixels - generous for any realistic halo/background blur effect, while
    ///     bounding the box-blur's per-row/per-column sliding-window cost (which is proportional
    ///     to the already-region-bounded buffer size, not the radius itself, but whose zero-padded
    ///     edge handling still becomes wastefully expensive for an absurdly large radius) to a
    ///     small, practical amount. Clamped rather than skipped/thrown, unlike an oversized filter
    ///     region (see <see cref="ComputeFilterRegionPixelBounds(XElement, Rect, Matrix3x2)"/>): a clamped blur still produces
    ///     a visually reasonable, just-less-blurred result, whereas a clamped-but-still-rendered
    ///     region would be silently mis-positioned.
    /// </summary>
    private const float MaxFilterBlurStdDeviationPixels = 250f;

    /// <summary>
    ///     The maximum number of <c>fe*</c> primitive children a single <c>filter</c> element's
    ///     chain is evaluated with, before the whole filter is tolerantly skipped (see
    ///     <see cref="RenderFilteredShape"/>'s remarks). Every primitive's output buffer is exactly
    ///     the filter region's own pixel size (see <see cref="EvaluateFilterChain"/>'s remarks), so
    ///     evaluating N primitives against a region of area A costs O(N * A) - a cost dimension
    ///     neither <see cref="MaxTotalRenderedElements"/> (which counts each <c>fe*</c> child once,
    ///     assuming O(1)/O(perimeter) per-element cost, not O(region-area)-per-primitive cost) nor
    ///     <see cref="GeometryWorkBudget"/> (which only tracks path/points-list/text parsing work)
    ///     actually bounds. 1,000 is far beyond the longest real filter chain observed across this
    ///     repository's entire test/fixture corpus (10 primitives, in <c>InkscapeFilters.svg</c>'s
    ///     <c>filter48</c>), while remaining small enough that even a pathologically tiny filter
    ///     region (where <see cref="MaxFilterPrimitiveWorkUnits"/> alone would not reject quickly)
    ///     cannot force an unbounded number of primitive evaluations.
    /// </summary>
    private const int MaxFilterPrimitivesPerFilter = 1_000;

    /// <summary>
    ///     The maximum combined "primitive count times filter-region pixel area" work a single
    ///     <c>filter</c> element's chain may be charged for, before the whole filter is tolerantly
    ///     skipped (see <see cref="RenderFilteredShape"/>'s remarks) - the region-weighted
    ///     counterpart to <see cref="MaxFilterPrimitivesPerFilter"/>, bounding the complementary case
    ///     of a chain that stays under that flat count cap but targets an unreasonably large region.
    ///     5,000,000 is more than 100 times the largest single real charge (40,000: one primitive
    ///     against a 200x200 region) observed across this repository's entire test/fixture corpus,
    ///     while remaining far below the cost a pathological chain (for example 5,000 primitives
    ///     against a modest ~180x180 region, charging 162,000,000) would otherwise incur.
    /// </summary>
    private const long MaxFilterPrimitiveWorkUnits = 5_000_000L;

    /// <summary>
    ///     The maximum <see cref="RenderElement"/> recursion depth this codec descends through
    ///     while walking the element tree - covering plain <c>g</c>/<c>symbol</c> nesting as well
    ///     as <c>use</c>-reference recursion - before giving up, guarding against a
    ///     <see cref="StackOverflowException"/> (which cannot be caught and would otherwise
    ///     terminate the process outright, bypassing this class's documented
    ///     <see cref="InvalidDataException"/>-wrapping error-handling policy) from a document with
    ///     many levels of nested container elements. Real-world documents, including deeply
    ///     grouped output from illustration tools, essentially never approach this depth.
    /// </summary>
    private const int MaxElementDepth = 100;

    /// <summary>
    ///     The maximum total number of elements this codec will render across a single
    ///     <c>Load</c> call, bounding non-cyclic exponential <c>use</c> fan-out. Neither
    ///     <see cref="MaxUseDepth"/> nor <see cref="MaxElementDepth"/> bounds total work: a group
    ///     legitimately (non-cyclically) referenced by several sibling <c>use</c> elements, itself
    ///     containing further such fan-out, re-renders its entire subtree once per reference, so
    ///     the total number of elements rendered grows exponentially with nesting depth even while
    ///     every individual reference chain stays well within both depth caps. 100,000 is far
    ///     beyond the element count of any real-world SVG this codec has been exercised against
    ///     (the most complex fixture in this repository's test suite has roughly 700 elements),
    ///     but small enough to keep worst-case rendering CPU/memory bounded to a small, practical
    ///     amount regardless of how a malicious/pathological document is structured.
    /// </summary>
    private const int MaxTotalRenderedElements = 100_000;

    /// <summary>
    ///     The maximum total number of characters <see cref="LoadRootElement"/>'s
    ///     <see cref="XDocument.Load(XmlReader, LoadOptions)"/> call will read before giving up,
    ///     bounding how large a single in-memory <see cref="XDocument"/> this codec will ever
    ///     materialize for a single <c>Load</c> call. Without this bound, an attacker-supplied
    ///     stream of unbounded size would be fully parsed into an unbounded DOM tree before any of
    ///     this class's other guards (<see cref="MaxTotalRenderedElements"/>,
    ///     <see cref="GeometryWorkBudget"/>) ever get a chance to run, since those guards only
    ///     execute during the rendering walk that follows a successful parse. 5,000,000 characters
    ///     is roughly 100 times the size of the largest real-world fixture in this repository's
    ///     test suite (<c>InkscapeFilters.svg</c>, 50,381 bytes) - far beyond any real document,
    ///     but small enough to keep worst-case parse-time CPU/memory bounded to a small, practical
    ///     amount, matching this class's other budgets' "generous but bounded" order-of-magnitude
    ///     spirit.
    /// </summary>
    /// <remarks>
    ///     This is an accepted, bounded limitation, not a full incremental/streaming parse: a
    ///     well-formed document sized just under this character cap can still fully materialize
    ///     into an in-memory DOM before <see cref="MaxTotalRenderedElements"/> or
    ///     <see cref="GeometryWorkBudget"/> ever get a chance to reject a single pathological
    ///     element's content. A full streaming-parser rewrite of <c>Load</c> (replacing
    ///     <see cref="XDocument"/>/<see cref="XElement"/> entirely) would close this remaining gap
    ///     but is out of scope for this bound, which targets the specific, previously-completely-
    ///     unbounded "raw document size" dimension.
    /// </remarks>
    private const int MaxDocumentCharacters = 5_000_000;

    /// <summary>
    ///     Tracks the cumulative "geometry parsing work" - path <c>d</c> data commands,
    ///     points-list coordinate pairs, and text characters - charged across a single
    ///     <c>Load</c> call, throwing once a fixed combined budget is exceeded. This bounds the
    ///     content of a single element, a dimension <see cref="MaxTotalRenderedElements"/> does
    ///     not cover: that budget only counts how many elements are visited, so one
    ///     <c>path</c>/<c>polyline</c>/<c>polygon</c>/<c>text</c> element with an extremely large
    ///     <c>d</c>/<c>points</c>/text value would otherwise count as only a single element while
    ///     allocating or processing an unbounded amount of geometry or text.
    /// </summary>
    /// <remarks>
    ///     A mutable reference type, rather than a <c>ref int</c> counter (the convention used
    ///     for <c>totalElements</c> below and for <see cref="Fonts.GlyfLocaReader"/>'s
    ///     total-point/component counters), because <see cref="PathDataParser"/> is a long-lived
    ///     stateful instance that cannot store a <c>ref</c> parameter as a field; sharing one
    ///     instance by ordinary object reference achieves the same "one counter, many call sites"
    ///     effect without that constraint.
    /// </remarks>
    private sealed class GeometryWorkBudget
    {
        /// <summary>
        ///     The maximum combined total of path-data commands, points-list coordinate pairs,
        ///     and text characters this codec will parse across a single <c>Load</c> call. Mirrors
        ///     <see cref="Fonts.GlyfLocaReader"/>'s own <c>MaxTotalPoints</c> budget (also
        ///     <c>200_000</c>) - the same order of magnitude precedent for bounding a single
        ///     pathological element's parsing cost - and is far beyond the combined
        ///     command/coordinate/character count of any real-world document this codec has been
        ///     exercised against, while keeping worst-case CPU/memory bounded to a small,
        ///     practical amount.
        /// </summary>
        private const int MaxTotalGeometryWork = 200_000;

        /// <summary>The running total of geometry-parsing work charged so far.</summary>
        private int _total;

        /// <summary>
        ///     Charges <paramref name="amount"/> units of work against the running total,
        ///     throwing once the combined budget is exceeded - called incrementally, before or as
        ///     each unit of work is actually spent, so a single pathological element throws
        ///     partway through parsing rather than only after its entire (unbounded) content has
        ///     already been scanned.
        /// </summary>
        /// <param name="amount">The number of commands/coordinates/characters just accounted for.</param>
        /// <exception cref="InvalidDataException">
        ///     Thrown once the cumulative total exceeds <see cref="MaxTotalGeometryWork"/>.
        /// </exception>
        public void Charge(int amount)
        {
            // Check before adding (rather than adding then checking) so that a single amount
            // large enough to make the addition itself overflow int cannot bypass the budget -
            // MaxTotalGeometryWork - _total is always non-negative and small here, since the
            // invariant _total <= MaxTotalGeometryWork holds after every successful call, so the
            // subtraction itself cannot overflow. This is defense-in-depth: given today's fixed
            // constants (amount is at most MaxDocumentCharacters, far below int.MaxValue / 2),
            // _total could never legitimately climb anywhere near int.MaxValue via repeated small
            // additions before the very next charge past MaxTotalGeometryWork already throws -
            // but the check-before-add ordering is strictly more correct regardless, and remains
            // safe if either constant is ever raised without re-auditing this method.
            if (amount > MaxTotalGeometryWork - _total)
            {
                throw new InvalidDataException(
                    "SVG document resolves to too much total path/point-list/text geometry-parsing work.");
            }

            _total += amount;
        }
    }

    /// <summary>
    ///     Tracks the cumulative "primitive count times filter-region pixel area" work charged
    ///     across every filter actually evaluated (i.e. every <see cref="RenderFilteredShape"/>
    ///     call that passes its own per-filter <see cref="MaxFilterPrimitiveWorkUnits"/> ceiling
    ///     and is about to allocate a <c>SourceGraphic</c> buffer) within a single <c>Load</c>
    ///     call, so a single filter definition referenced by many shapes cannot bypass the
    ///     resource-safety bound that <see cref="MaxFilterPrimitiveWorkUnits"/> alone provides.
    /// </summary>
    /// <remarks>
    ///     <see cref="MaxFilterPrimitiveWorkUnits"/> bounds only a single filter evaluation's own
    ///     cost - it is checked independently for every shape that references a <c>filter</c>, so
    ///     a document defining one filter once and referencing it (via <c>filter="url(#f)"</c>)
    ///     from many shapes charges that same per-filter ceiling once per reference, with no bound
    ///     on the total number of references. <see cref="MaxTotalRenderedElements"/> bounds the
    ///     total number of rendered shapes, but not their filter work at all: it counts a filtered
    ///     shape identically to an unfiltered one, even though a filtered shape's own rendering
    ///     cost (allocating and evaluating a fresh <c>SourceGraphic</c>/filter chain) can be
    ///     orders of magnitude larger. This budget closes that gap by charging the same
    ///     region-weighted work unit already computed for the per-filter check into one running,
    ///     per-<c>Load</c>-call total, mirroring <see cref="GeometryWorkBudget"/>'s identical
    ///     "mutable reference type shared across the whole render walk" pattern - a plain
    ///     <c>ref long</c> parameter is not usable here because it must be threaded through the
    ///     same deeply recursive <see cref="RenderElement"/>/<see cref="RenderUse"/>/
    ///     <see cref="RenderMarkers"/>/<see cref="RenderOneMarker"/>/<see cref="RenderText"/> call
    ///     chain <see cref="GeometryWorkBudget"/> already uses, and a separate object (rather than
    ///     folding this counter into <see cref="GeometryWorkBudget"/> itself) keeps each budget's
    ///     single responsibility - geometry-parsing work versus filter-evaluation work - distinct
    ///     and independently documented/testable.
    /// </remarks>
    private sealed class FilterWorkBudget
    {
        /// <summary>
        ///     The maximum combined "primitive count times filter-region pixel area" work this
        ///     codec will evaluate, across every filter application, for a single <c>Load</c>
        ///     call. 50,000,000 is exactly 10 times <see cref="MaxFilterPrimitiveWorkUnits"/> (the
        ///     ceiling for a single filter application) - generous enough that a real-world
        ///     document legitimately reusing one filter across a modest number of shapes (for
        ///     example 10 shapes, each individually well within the per-filter ceiling) is never
        ///     rejected, while still keeping the worst-case total filter-evaluation CPU/memory for
        ///     a single document bounded to a small, fixed multiple of a single filter's own
        ///     bound, regardless of how many shapes a pathological document references the same
        ///     (or different) filters from.
        /// </summary>
        private const long MaxCumulativeFilterWorkUnits = 50_000_000L;

        /// <summary>The running total of filter-evaluation work charged so far.</summary>
        private long _total;

        /// <summary>
        ///     Attempts to charge <paramref name="amount"/> units of filter-evaluation work
        ///     against the running total, reporting whether the cumulative budget still has room
        ///     - called immediately before <see cref="RenderFilteredShape"/> allocates its
        ///     <c>SourceGraphic</c> buffer, so a filter application that would push the cumulative
        ///     total over budget is rejected before any of its own work (buffer allocation, blur
        ///     passes, compositing) begins.
        /// </summary>
        /// <param name="amount">
        ///     The region-weighted work unit for this one filter application - identical to the
        ///     value already computed for <see cref="IsFilterPrimitiveWorkWithinBudget"/>'s own
        ///     per-filter check.
        /// </param>
        /// <returns>
        ///     <see langword="true"/> if <paramref name="amount"/> was charged because the
        ///     cumulative total remains within <see cref="MaxCumulativeFilterWorkUnits"/>;
        ///     <see langword="false"/> (charging nothing) if it would exceed the budget, so the
        ///     caller can tolerantly fall back to unfiltered rendering rather than throwing -
        ///     a filter budget, unlike <see cref="GeometryWorkBudget"/>'s parsing-work budget, is
        ///     never treated as a hard document-rejection condition, consistent with every other
        ///     per-filter tolerant-fallback case in <see cref="RenderFilteredShape"/>.
        /// </returns>
        public bool TryCharge(long amount)
        {
            // Check before adding (rather than adding then checking) so that a single amount
            // large enough to make the addition itself overflow cannot bypass the budget - mirrors
            // GeometryWorkBudget.Charge's identical check-before-add reasoning. Both operands here
            // are already bounded well below long.MaxValue by MaxFilterPrimitiveWorkUnits/
            // MaxCumulativeFilterWorkUnits themselves, but the ordering remains strictly more
            // correct regardless.
            if (amount > MaxCumulativeFilterWorkUnits - _total)
            {
                return false;
            }

            _total += amount;
            return true;
        }
    }
}
