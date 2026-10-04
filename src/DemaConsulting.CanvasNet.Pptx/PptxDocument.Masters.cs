using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore sldmaster csld pptx txstyles titlestyle bodystyle otherstyle

/// <summary>
///     Implements the <see cref="PptxDocument"/> slide master parser (Phase 1b): parses
///     <c>ppt/slideMasters/slideMasterN.xml</c>, resolving its <c>/theme</c> relationship and
///     enumerating its placeholder shapes. As of Phase 1d, also parses the master root's own
///     <c>&lt;p:txStyles&gt;</c> child (a sibling of <c>&lt;p:cSld&gt;</c>, not nested inside it)
///     into a <see cref="PptxMasterTextStyles"/>. Lazy and cached by resolved part path.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>Caches each slide master's parsed form, keyed by its resolved part path, on first access.</summary>
    private readonly Dictionary<string, PptxMaster> _masterCache;

    /// <summary>
    ///     Returns the parsed slide master at <paramref name="masterPartPath"/>, parsing and
    ///     caching it on first access.
    /// </summary>
    /// <param name="masterPartPath">The master part's resolved path (for example <c>"ppt/slideMasters/slideMaster1.xml"</c>).</param>
    /// <returns>The parsed <see cref="PptxMaster"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the master part is missing or not well-formed XML, when its root element is
    ///     not a PresentationML <c>&lt;p:sldMaster&gt;</c> element, when its
    ///     <c>&lt;p:cSld&gt;/&lt;p:spTree&gt;</c> element is missing, when a placeholder's
    ///     <c>idx</c> attribute is invalid, or when it has no <c>/theme</c> relationship.
    /// </exception>
    internal PptxMaster GetMaster(string masterPartPath)
    {
        if (_masterCache.TryGetValue(masterPartPath, out var cached))
        {
            return cached;
        }

        var root = LoadPartXmlRoot(masterPartPath);
        if (root.Name != PresentationNamespace + "sldMaster")
        {
            throw new InvalidDataException($"Part '{masterPartPath}' is not a <p:sldMaster> part.");
        }

        var cSld = root.Element(PresentationNamespace + "cSld") ??
            throw new InvalidDataException($"Slide master '{masterPartPath}' has no <p:cSld> element.");
        var spTree = cSld.Element(PresentationNamespace + "spTree") ??
            throw new InvalidDataException($"Slide master '{masterPartPath}' has no <p:cSld>/<p:spTree> element.");

        var placeholders = PptxPlaceholderParser.ParsePlaceholderShapes(spTree);
        var themePartPath = ResolveRelationshipByType(masterPartPath, "/theme");
        var txStyles = ParseMasterTextStyles(root.Element(PresentationNamespace + "txStyles"));
        var background = cSld.Element(PresentationNamespace + "bg");

        // The master's theme is already eagerly resolved above (themePartPath), so there is no
        // added laziness concern in also eagerly resolving it here for the shape tree's own
        // <p:graphicFrame> tables (see ParseShapeTree's themeResolver parameter).
        var shapeTree = ParseShapeTree(spTree, () => GetTheme(themePartPath));

        var master = new PptxMaster(masterPartPath, themePartPath, placeholders, txStyles, background, shapeTree);
        _masterCache[masterPartPath] = master;
        return master;
    }

    /// <summary>
    ///     Parses a slide master's own <c>&lt;p:txStyles&gt;</c> element (Phase 1d) into a
    ///     <see cref="PptxMasterTextStyles"/>, each of its three schema-optional named styles
    ///     (<c>&lt;p:titleStyle&gt;</c>/<c>&lt;p:bodyStyle&gt;</c>/<c>&lt;p:otherStyle&gt;</c>)
    ///     retained unparsed as its own <c>&lt;a:lstStyle&gt;</c>-shaped element.
    /// </summary>
    /// <param name="txStylesElement">The master's <c>&lt;p:txStyles&gt;</c> element, or <see langword="null"/> when absent.</param>
    /// <returns>The parsed <see cref="PptxMasterTextStyles"/>.</returns>
    private static PptxMasterTextStyles ParseMasterTextStyles(XElement? txStylesElement)
    {
        if (txStylesElement is null)
        {
            return new PptxMasterTextStyles(null, null, null);
        }

        return new PptxMasterTextStyles(
            txStylesElement.Element(PresentationNamespace + "titleStyle"),
            txStylesElement.Element(PresentationNamespace + "bodyStyle"),
            txStylesElement.Element(PresentationNamespace + "otherStyle"));
    }
}
