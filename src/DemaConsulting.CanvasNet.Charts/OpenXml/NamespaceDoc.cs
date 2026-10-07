namespace DemaConsulting.CanvasNet.Charts.OpenXml;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Charts.OpenXml"/> namespace provides a public,
///     format-agnostic parser (<see cref="OpenXmlChartParser"/>) that produces a
///     <see cref="Chart"/> from a raw ECMA-376 DrawingML-Charts <c>c:chartSpace</c> (or bare
///     <c>c:chart</c>) <see cref="System.Xml.Linq.XElement"/>/<see cref="System.Xml.Linq.XDocument"/>,
///     and a dedicated exception type (<see cref="ChartUnsupportedFeatureException"/>) for a
///     recognized-but-out-of-scope chart kind or data shape.
/// </summary>
/// <remarks>
///     <para>
///     This namespace is deliberately format-agnostic: it consumes only
///     <see cref="System.Xml.Linq.XElement"/>/<see cref="System.Xml.Linq.XDocument"/> content and
///     has no knowledge whatsoever of OPC/ZIP packaging, part relationships, or any specific host
///     document format (PresentationML/<c>.pptx</c>, SpreadsheetML/<c>.xlsx</c>, or a future
///     <c>.vsdx</c>). A caller is responsible for first locating and opening the relevant
///     <c>chart#.xml</c> part from whatever package format it is embedded in (for example
///     <c>DemaConsulting.CanvasNet.Pptx</c>'s own OPC-reading machinery, in a later phase) and
///     handing this namespace only the resulting XML content. This boundary is what keeps
///     <see cref="OpenXmlChartParser"/> reusable by any future document-format library that
///     embeds an OOXML chart part - not just <c>DemaConsulting.CanvasNet.Pptx</c> - mirroring the
///     same "this package must never reference a host document-format package" dependency-
///     direction constraint the parent <see cref="DemaConsulting.CanvasNet.Charts"/> namespace
///     already documents for its data model and renderer.
///     </para>
///     <para>
///     <see cref="OpenXmlChartParser"/> reads only cached values: a series' value cache
///     (<c>c:numCache</c>), category cache (<c>c:strCache</c>/<c>c:numCache</c>), and name cache
///     (<c>c:strCache</c> or a literal <c>c:v</c>) are read directly; the sibling formula element
///     (<c>c:f</c>, for example <c>Sheet1!$B$2:$B$5</c>) that a spreadsheet application would use
///     to recompute those cached values is always ignored. A <c>chart#.xml</c> part embedded in a
///     document is, by construction, already a point-in-time snapshot - the authoring
///     application wrote the cached values precisely so a consumer with no access to (or
///     understanding of) the original source worksheet can still render the chart correctly -
///     making live recalculation both unnecessary and, for this phase's purposes, entirely out of
///     scope (mirroring <c>DemaConsulting.CanvasNet.Pptx</c>'s own documented
///     <c>&lt;a:fld&gt;</c> "paint the cached value, never recompute" convention).
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
