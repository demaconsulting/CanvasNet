using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Svg;

// SvgCodec.Unsupported.cs - reports of features the codec cannot draw, through SvgLoadOptions.UnsupportedFeatureHandler
// (see SvgUnsupportedFeature).
//
// Text is the content a reader misses most when it silently disappears, so every way a text (or a part of one) is
// dropped reports svg-text-not-drawn, and every feature a drawn text uses but the codec ignores reports
// svg-text-unsupported. A filter that is applied with primitiveUnits="objectBoundingBox" or a primitive subregion, which
// the codec draws differently from the specification, reports svg-filter-unsupported. Detection never changes what is
// painted: the painter does exactly what it did before, and the
// collector only records. The collector is created only when a handler is set, and the bounds and position-binding
// pre-passes never report, so a document is reported once, for what is painted.
public static partial class SvgCodec
{
    /// <summary>The most distinct features or reasons a report names; further ones are counted as "and more".</summary>
    private const int MaxReportedFeatures = 16;

    /// <summary>The most characters of an element's text a report quotes.</summary>
    private const int MaxReportedTextCharacters = 24;

    /// <summary>
    ///     Reports that the content of a container the painter skips holds text that is therefore not drawn:
    ///     the HTML of a <c>foreignObject</c>, or a <c>text</c> inside a nested <c>svg</c>, an unknown element, a
    ///     <c>switch</c> without a drawable child, or a subtree with a non-finite transform.
    /// </summary>
    /// <param name="element">The skipped element.</param>
    /// <param name="context">The fixed per-document render context; nothing happens when it has no collector.</param>
    /// <param name="reason">The reason the content is not drawn, for the message (for example <c>"inside &lt;svg&gt;"</c>).</param>
    /// <remarks>
    ///     A <c>foreignObject</c> reports any non-white-space character content, since its HTML is text the codec does
    ///     not draw; any other element reports only when it is itself a <c>text</c> or contains one. The scan runs at most
    ///     once per element (see <see cref="UnsupportedFeatureCollector.TryBeginScan"/>), so a <c>use</c> fan-out of a
    ///     skipped subtree stays linear.
    /// </remarks>
    private static void ReportSkippedText(XElement element, RenderContext context, string reason)
    {
        if (context.Unsupported is not { } collector || !collector.TryBeginScan(element))
        {
            return;
        }

        var holdsText = element.Name.LocalName == "foreignObject"
            ? element.DescendantNodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value))
            : element.Name.LocalName == "text" || element.Descendants().Any(d => d.Name.LocalName == "text");
        if (holdsText)
        {
            collector.Add(SvgUnsupportedFeature.TextNotDrawnCode, reason, element);
        }
    }

    /// <summary>
    ///     Reports the features of a filter that is being applied which the codec draws differently from the
    ///     specification: <c>primitiveUnits="objectBoundingBox"</c> (not read: primitive values stay in user units)
    ///     and a primitive subregion (<c>x</c>, <c>y</c>, <c>width</c> or <c>height</c> on a direct <c>fe*</c> child,
    ///     read as a fraction of the filter region).
    /// </summary>
    /// <param name="filterElement">The <c>filter</c> element being applied.</param>
    /// <param name="context">The fixed per-document render context; nothing happens when it has no collector.</param>
    /// <remarks>
    ///     Called once per filter application, from the real render pass only, so the report counts the distinct
    ///     <c>filter</c> elements that are applied. The scan reads only the filter's direct children, whose number the
    ///     filter work budget already bounds before any application.
    /// </remarks>
    private static void ReportFilterUnsupportedFeatures(XElement filterElement, RenderContext context)
    {
        if (context.Unsupported is not { } collector)
        {
            return;
        }

        if (string.Equals((string?)filterElement.Attribute("primitiveUnits"), "objectBoundingBox", StringComparison.Ordinal))
        {
            collector.Add(SvgUnsupportedFeature.FilterUnsupportedCode, "primitiveUnits=\"objectBoundingBox\"", filterElement);
        }

        if (filterElement.Elements().Any(PrimitiveDeclaresSubregion))
        {
            collector.Add(SvgUnsupportedFeature.FilterUnsupportedCode, "primitive subregion (x, y, width, height)", filterElement);
        }
    }

    /// <summary>
    ///     Collects the reports of one render and hands them to the host after painting, one per code.
    /// </summary>
    /// <param name="handler">The host callback.</param>
    /// <remarks>
    ///     Per code it keeps the first element (for the message), the distinct elements (for the count) and the distinct
    ///     features or reasons (sorted, capped at <see cref="MaxReportedFeatures"/>), so a hostile document cannot make
    ///     the collection large. Not thread-safe: it belongs to one render on one thread.
    /// </remarks>
    private sealed class UnsupportedFeatureCollector(Action<SvgUnsupportedFeature> handler)
    {
        /// <summary>The codes in report order.</summary>
        private static readonly string[] CodeOrder =
        [
            SvgUnsupportedFeature.TextNotDrawnCode,
            SvgUnsupportedFeature.TextUnsupportedCode,
            SvgUnsupportedFeature.FilterUnsupportedCode
        ];

        /// <summary>The collected entries per code.</summary>
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

        /// <summary>The elements whose skipped content was already scanned for text.</summary>
        private readonly HashSet<XElement> _scanned = [];

        /// <summary>
        ///     Marks an element as scanned for skipped text.
        /// </summary>
        /// <param name="element">The skipped element.</param>
        /// <returns><see langword="true"/> the first time; <see langword="false"/> when it was already scanned.</returns>
        public bool TryBeginScan(XElement element) => _scanned.Add(element);

        /// <summary>
        ///     Records one feature or reason for an element.
        /// </summary>
        /// <param name="code">
        ///     The code: <see cref="SvgUnsupportedFeature.TextNotDrawnCode"/>, <see cref="SvgUnsupportedFeature.TextUnsupportedCode"/>
        ///     or <see cref="SvgUnsupportedFeature.FilterUnsupportedCode"/>.
        /// </param>
        /// <param name="feature">The feature or reason, for example <c>"rotate"</c> or <c>"textPath"</c>.</param>
        /// <param name="element">The element the report is about (counted once however often it is recorded).</param>
        public void Add(string code, string feature, XElement element)
        {
            if (!_entries.TryGetValue(code, out var entry))
            {
                entry = new Entry(Describe(element));
                _entries[code] = entry;
            }

            entry.Elements.Add(element);
            if (entry.Features.Count < MaxReportedFeatures || entry.Features.Contains(feature))
            {
                entry.Features.Add(feature);
            }
            else
            {
                entry.MoreFeatures = true;
            }
        }

        /// <summary>
        ///     Calls the handler once per collected code, in code order.
        /// </summary>
        public void Report()
        {
            foreach (var code in CodeOrder)
            {
                if (!_entries.TryGetValue(code, out var entry))
                {
                    continue;
                }

                var features = string.Join(", ", entry.Features) + (entry.MoreFeatures ? ", and more" : string.Empty);
                var count = entry.Elements.Count;
                var where = count == 1 ? "1 element" : $"{count.ToString(CultureInfo.InvariantCulture)} elements";
                var what = code switch
                {
                    SvgUnsupportedFeature.TextNotDrawnCode => "text is not drawn",
                    SvgUnsupportedFeature.TextUnsupportedCode => "text is drawn without",
                    _ => "filter is drawn without"
                };
                handler(new SvgUnsupportedFeature
                {
                    Code = code,
                    Count = count,
                    Message = $"{what}: {features} ({where}; first: {entry.First})"
                });
            }
        }

        /// <summary>
        ///     Names an element for a message: its name, its <c>id</c> when it has one, and the start of its text with the
        ///     white space collapsed.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>For example <c>&lt;text id="title"&gt; 'Quarterly results'</c>.</returns>
        private static string Describe(XElement element)
        {
            var id = (string?)element.Attribute("id");
            var name = id == null ? $"<{element.Name.LocalName}>" : $"<{element.Name.LocalName} id=\"{id}\">";

            // Only as much of the text as is quoted is read, so a huge element costs little
            var quoted = new StringBuilder();
            var pendingSpace = false;
            foreach (var node in element.DescendantNodes().OfType<XText>())
            {
                foreach (var c in node.Value)
                {
                    if (char.IsWhiteSpace(c))
                    {
                        pendingSpace = quoted.Length > 0;
                        continue;
                    }

                    if (quoted.Length >= MaxReportedTextCharacters)
                    {
                        return $"{name} '{quoted}...'";
                    }

                    if (pendingSpace)
                    {
                        quoted.Append(' ');
                        pendingSpace = false;
                    }

                    quoted.Append(c);
                }
            }

            return quoted.Length == 0 ? name : $"{name} '{quoted}'";
        }

        /// <summary>
        ///     The collected state of one code.
        /// </summary>
        /// <param name="first">The description of the first element.</param>
        private sealed class Entry(string first)
        {
            /// <summary>Gets the description of the first element.</summary>
            public string First { get; } = first;

            /// <summary>Gets the distinct elements.</summary>
            public HashSet<XElement> Elements { get; } = [];

            /// <summary>Gets the distinct features or reasons, sorted.</summary>
            public SortedSet<string> Features { get; } = new(StringComparer.Ordinal);

            /// <summary>Gets or sets a value indicating whether features beyond the cap were dropped.</summary>
            public bool MoreFeatures { get; set; }
        }
    }
}
