using System.Xml.Linq;
using DemaConsulting.CanvasNet.Charts;
using DemaConsulting.CanvasNet.Charts.OpenXml;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore graphicframe chartspace

/// <summary>
///     Implements the <see cref="PptxDocument"/> chart-graphic-frame resolver (Phase 4):
///     locating a <c>&lt;p:graphicFrame&gt;</c>'s <c>&lt;a:graphicData&gt;/&lt;c:chart
///     r:id="..."/&gt;</c> relationship reference, resolving and loading the referenced
///     <c>chart#.xml</c> OPC part, and parsing it into a <see cref="Chart"/> via
///     <see cref="OpenXmlChartParser.Parse(XElement)"/> - the integration point that lets
///     <c>DemaConsulting.CanvasNet.Pptx</c> render a <c>&lt;p:graphicFrame&gt;</c> chart instead
///     of throwing <see cref="PptxUnsupportedFeatureException"/> (feature token
///     <c>"pptx-graphic-frame-kind"</c>) the way every earlier phase did. This integration
///     respects the <c>DemaConsulting.CanvasNet.Charts</c> package's own dependency-direction
///     constraint: <c>Charts</c> never references <c>Pptx</c>, only the reverse.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>The DrawingML-Charts XML namespace (prefix <c>c:</c> in a real <c>chart#.xml</c> part).</summary>
    private static readonly XNamespace ChartNamespace = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    /// <summary>
    ///     Resolves a <c>&lt;p:graphicFrame&gt;</c>'s declared <c>&lt;a:graphicData&gt;/
    ///     &lt;c:chart&gt;</c> chart into a validated <see cref="Chart"/>.
    /// </summary>
    /// <param name="graphicData">
    ///     The <c>&lt;a:graphicData&gt;</c> element whose <c>uri</c> attribute ends in
    ///     <c>"/chart"</c> (already dispatched by <see cref="ParseShapeTree"/> - this method does
    ///     not itself re-check <paramref name="graphicData"/>'s <c>uri</c>).
    /// </param>
    /// <param name="resolveChartPart">
    ///     Resolves a chart relationship id (the <c>&lt;c:chart&gt;</c> element's own
    ///     <c>r:id</c> attribute, scoped to the owning slide/layout/master part's own
    ///     <c>.rels</c> file) to that referenced part's root <see cref="XElement"/> (a
    ///     <c>c:chartSpace</c> root) - typically
    ///     <c>id =&gt; LoadPartXmlRoot(ResolveRelationship(ownerPartPath, id))</c>, bound to the
    ///     owning part's own path by each of <see cref="GetSlide"/>/<see cref="GetLayout"/>/
    ///     <see cref="GetMaster"/>.
    /// </param>
    /// <returns>The parsed, validated <see cref="Chart"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="graphicData"/> has no <c>&lt;c:chart&gt;</c> child, or that
    ///     child has no <c>r:id</c> attribute - a malformed chart graphic frame, distinct from a
    ///     recognized-but-unsupported chart kind (see <see cref="PptxUnsupportedFeatureException"/>
    ///     below). Also thrown (wrapping the original <see cref="ArgumentException"/>) when the
    ///     referenced chart part's own data is otherwise malformed in a way
    ///     <see cref="OpenXmlChartParser.Parse(XElement)"/> only detects by delegating to the
    ///     <see cref="Chart"/>/<see cref="ChartSeries"/>/<see cref="ChartAxis"/> model
    ///     constructors (for example a cached point count that does not match its series' point
    ///     count) - every malformed chart part must surface as <see cref="InvalidDataException"/>,
    ///     never as a model-constructor <see cref="ArgumentException"/> escaping unwrapped.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown (wrapping a <see cref="ChartUnsupportedFeatureException"/> thrown by
    ///     <see cref="OpenXmlChartParser.Parse(XElement)"/>) with feature token
    ///     <c>"pptx-chart-" + ex.Feature</c> (for example <c>"pptx-chart-charts-openxml-radar-chart"</c>
    ///     for a radar chart) when the referenced chart part declares a recognized-but-unsupported
    ///     chart kind or data shape - see <see cref="OpenXmlChartParser"/>'s own remarks for the
    ///     complete supported/deferred boundary this phase inherits unchanged from
    ///     <c>DemaConsulting.CanvasNet.Charts</c>.
    /// </exception>
    internal static Chart ParseChart(XElement graphicData, Func<string, XElement> resolveChartPart)
    {
        var chartElement = graphicData.Element(ChartNamespace + "chart") ??
            throw new InvalidDataException("An <a:graphicData> chart element has no <c:chart> child.");

        var relationshipId = (string?)chartElement.Attribute(RelationshipRefNamespace + "id") ??
            throw new InvalidDataException("A <c:chart> element has no 'r:id' attribute.");

        var chartPartRoot = resolveChartPart(relationshipId);

        try
        {
            return OpenXmlChartParser.Parse(chartPartRoot);
        }
        catch (ChartUnsupportedFeatureException ex)
        {
            throw new PptxUnsupportedFeatureException(
                "pptx-chart-" + ex.Feature,
                $"Chart feature '{ex.Feature}' is not supported.",
                ex);
        }
        catch (ArgumentException ex)
        {
            // OpenXmlChartParser.Parse delegates cache/count/range validation to the Chart/
            // ChartSeries/ChartAxis constructors, which throw ArgumentException for malformed
            // chart data (for example a cached point count that disagrees with its series'
            // actual point count). The Pptx requirement promises InvalidDataException for any
            // malformed chart part, so rewrap rather than letting this escape as-is.
            throw new InvalidDataException($"Chart part referenced by relationship '{relationshipId}' has malformed data.", ex);
        }
    }
}
