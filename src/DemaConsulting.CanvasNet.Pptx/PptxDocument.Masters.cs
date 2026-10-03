namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore sldmaster csld pptx

/// <summary>
///     Implements the <see cref="PptxDocument"/> slide master parser (Phase 1b): parses
///     <c>ppt/slideMasters/slideMasterN.xml</c>, resolving its <c>/theme</c> relationship and
///     enumerating its placeholder shapes. Lazy and cached by resolved part path.
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

        var spTree = root.Element(PresentationNamespace + "cSld")?.Element(PresentationNamespace + "spTree") ??
            throw new InvalidDataException($"Slide master '{masterPartPath}' has no <p:cSld>/<p:spTree> element.");

        var placeholders = PptxPlaceholderParser.ParsePlaceholderShapes(spTree);
        var themePartPath = ResolveRelationshipByType(masterPartPath, "/theme");

        var master = new PptxMaster(masterPartPath, themePartPath, placeholders);
        _masterCache[masterPartPath] = master;
        return master;
    }
}
