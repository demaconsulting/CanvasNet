namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore sldlayout csld pptx

/// <summary>
///     Implements the <see cref="PptxDocument"/> slide layout parser (Phase 1b): parses
///     <c>ppt/slideLayouts/slideLayoutN.xml</c>, resolving its <c>/slideMaster</c> relationship and
///     enumerating its placeholder shapes. Lazy and cached by resolved part path.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>Caches each slide layout's parsed form, keyed by its resolved part path, on first access.</summary>
    private readonly Dictionary<string, PptxLayout> _layoutCache;

    /// <summary>
    ///     Returns the parsed slide layout at <paramref name="layoutPartPath"/>, parsing and
    ///     caching it on first access.
    /// </summary>
    /// <param name="layoutPartPath">The layout part's resolved path (for example <c>"ppt/slideLayouts/slideLayout1.xml"</c>).</param>
    /// <returns>The parsed <see cref="PptxLayout"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the layout part is missing or not well-formed XML, when its root element is
    ///     not a PresentationML <c>&lt;p:sldLayout&gt;</c> element, when its
    ///     <c>&lt;p:cSld&gt;/&lt;p:spTree&gt;</c> element is missing, when a placeholder's
    ///     <c>idx</c> attribute is invalid, or when it has no <c>/slideMaster</c> relationship.
    /// </exception>
    internal PptxLayout GetLayout(string layoutPartPath)
    {
        if (_layoutCache.TryGetValue(layoutPartPath, out var cached))
        {
            return cached;
        }

        var root = LoadPartXmlRoot(layoutPartPath);
        if (root.Name != PresentationNamespace + "sldLayout")
        {
            throw new InvalidDataException($"Part '{layoutPartPath}' is not a <p:sldLayout> part.");
        }

        var spTree = root.Element(PresentationNamespace + "cSld")?.Element(PresentationNamespace + "spTree") ??
            throw new InvalidDataException($"Slide layout '{layoutPartPath}' has no <p:cSld>/<p:spTree> element.");

        var placeholders = PptxPlaceholderParser.ParsePlaceholderShapes(spTree);
        var masterPartPath = ResolveRelationshipByType(layoutPartPath, "/slideMaster");

        var layout = new PptxLayout(layoutPartPath, masterPartPath, placeholders);
        _layoutCache[layoutPartPath] = layout;
        return layout;
    }
}
