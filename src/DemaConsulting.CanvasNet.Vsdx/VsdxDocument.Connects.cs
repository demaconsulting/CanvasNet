using System.Globalization;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio davehoward jgreywolfvsdxjs

/// <summary>
///     Implements the <see cref="VsdxDocument"/> <c>&lt;Connects&gt;</c> parser: a page's
///     <c>&lt;Connects&gt;</c> element sits as a sibling of its <c>&lt;Shapes&gt;</c> element
///     directly under <c>&lt;PageContents&gt;</c> (not nested inside any <c>&lt;Shape&gt;</c> -
///     see the format reference's §7.1), one <c>&lt;Connect&gt;</c> child per glued connector
///     endpoint. Each parsed <see cref="VsdxConnect"/> is attached to its connector shape (matched
///     by <see cref="VsdxConnect.ConnectorShapeId"/> against a page's resolved top-level shapes)
///     via <see cref="VsdxShapeNode.Connects"/>.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>The <see cref="VsdxConnect.ToCell"/> prefix identifying connection-point-indexed glue (for example <c>"Connections.X1"</c>).</summary>
    private const string ConnectionsCellPrefix = "Connections.X";

    /// <summary>The <see cref="VsdxConnect.ToCell"/> literal value identifying whole-shape-pin glue.</summary>
    private const string WholeShapePinToCell = "PinX";

    /// <summary>The <see cref="VsdxConnect.Endpoint"/>-identifying <c>FromCell</c> literal value for a connector's begin endpoint.</summary>
    private const string BeginEndpointFromCell = "BeginX";

    /// <summary>
    ///     Parses every direct <c>&lt;Connect&gt;</c> child of <paramref name="connectsElement"/>
    ///     into a <see cref="VsdxConnect"/>, tolerantly skipping (never throwing for) any entry
    ///     missing a required <c>FromSheet</c>/<c>ToSheet</c>/<c>ToCell</c> attribute - a
    ///     malformed, well-formed-XML-but-out-of-scope construct that this package tolerates
    ///     rather than treats as fatal.
    /// </summary>
    /// <param name="connectsElement">The page's <c>&lt;Connects&gt;</c> element, or <see langword="null"/> when the page declares no such element (confirmed optional/page-dependent by the format reference's §13 inventory: <c>FlowchartShapes.vsdx</c>'s own page1 has no <c>&lt;Connects&gt;</c> section at all).</param>
    /// <returns>The parsed <see cref="VsdxConnect"/> entries, in document order. Empty when <paramref name="connectsElement"/> is <see langword="null"/> or declares no <c>&lt;Connect&gt;</c> children.</returns>
    internal static IReadOnlyList<VsdxConnect> ParseConnectsElement(XElement? connectsElement)
    {
        if (connectsElement is null)
        {
            return [];
        }

        var ns = connectsElement.Name.Namespace;
        var result = new List<VsdxConnect>();
        foreach (var connectElement in connectsElement.Elements(ns + "Connect"))
        {
            var fromSheet = (string?)connectElement.Attribute("FromSheet");
            var fromCell = (string?)connectElement.Attribute("FromCell");
            var toSheet = (string?)connectElement.Attribute("ToSheet");
            var toCell = (string?)connectElement.Attribute("ToCell");

            if (string.IsNullOrEmpty(fromSheet) || string.IsNullOrEmpty(toSheet) || string.IsNullOrEmpty(toCell))
            {
                // A malformed/incomplete <Connect> entry: skip it tolerantly rather than throwing
                // or aborting the whole page's Connects parse.
                continue;
            }

            var fromPart = (string?)connectElement.Attribute("FromPart") ?? string.Empty;
            var toPart = (string?)connectElement.Attribute("ToPart") ?? string.Empty;
            var endpoint = fromCell == BeginEndpointFromCell ? VsdxConnectEndpoint.Begin : VsdxConnectEndpoint.End;

            var isWholeShapePin = toCell == WholeShapePinToCell;
            var connectionPointIndex = !isWholeShapePin &&
                toCell.StartsWith(ConnectionsCellPrefix, StringComparison.Ordinal) &&
                int.TryParse(
                    toCell.AsSpan(ConnectionsCellPrefix.Length),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsedIndex)
                ? parsedIndex
                : (int?)null;

            result.Add(
                new VsdxConnect(
                    fromSheet,
                    endpoint,
                    fromPart,
                    toSheet,
                    toCell,
                    toPart,
                    isWholeShapePin,
                    connectionPointIndex));
        }

        return result;
    }

    /// <summary>
    ///     Groups <paramref name="connects"/> by <see cref="VsdxConnect.ConnectorShapeId"/> and
    ///     attaches each group to the matching top-level shape's own
    ///     <see cref="VsdxShapeNode.Connects"/> (matched by <see cref="VsdxShapeNode.Id"/>). A
    ///     <see cref="VsdxConnect"/> whose <see cref="VsdxConnect.ConnectorShapeId"/> matches no
    ///     top-level shape in <paramref name="shapes"/> is silently dropped - not an error, since a
    ///     connector declared only as a nested group child is out of this milestone's "top-level
    ///     shapes only" resolution scope (see <see cref="VsdxShapeNode"/>'s own remarks).
    /// </summary>
    /// <param name="shapes">The page's parsed (not yet necessarily resolved) top-level shapes.</param>
    /// <param name="connects">The page's parsed <see cref="VsdxConnect"/> entries.</param>
    private static void AttachConnectsToShapes(IReadOnlyList<VsdxShapeNode> shapes, IReadOnlyList<VsdxConnect> connects)
    {
        if (connects.Count == 0)
        {
            return;
        }

        var byConnectorId = new Dictionary<string, List<VsdxConnect>>(StringComparer.Ordinal);
        foreach (var connect in connects)
        {
            if (!byConnectorId.TryGetValue(connect.ConnectorShapeId, out var list))
            {
                list = [];
                byConnectorId[connect.ConnectorShapeId] = list;
            }

            list.Add(connect);
        }

        foreach (var shape in shapes)
        {
            if (byConnectorId.TryGetValue(shape.Id, out var shapeConnects))
            {
                shape.Connects = shapeConnects;
            }
        }
    }
}
