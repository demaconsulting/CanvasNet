namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore csld pptx

/// <summary>
///     Implements the <see cref="PptxDocument"/> slide parser (Phase 1b): parses
///     <c>ppt/slides/slideN.xml</c>, resolving its <c>/slideLayout</c> relationship and enumerating
///     its immediate placeholder shapes. Lazy and cached by slide index.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>Caches each slide's parsed form, keyed by its zero-based slide index, on first access.</summary>
    private readonly Dictionary<int, PptxSlide> _slideCache;

    /// <summary>
    ///     Returns the parsed slide at <paramref name="slideIndex"/> (into the ordered slide list
    ///     resolved by <see cref="InitializePresentation"/>), parsing and caching it on first
    ///     access.
    /// </summary>
    /// <param name="slideIndex">The zero-based index of the slide, in <c>&lt;p:sldIdLst&gt;</c> document order.</param>
    /// <returns>The parsed <see cref="PptxSlide"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="slideIndex"/> is negative or greater than or equal to
    ///     <see cref="SlideCount"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the slide part is missing or not well-formed XML, when its root element is
    ///     not a PresentationML <c>&lt;p:sld&gt;</c> element, when its
    ///     <c>&lt;p:cSld&gt;/&lt;p:spTree&gt;</c> element is missing, when a placeholder's
    ///     <c>idx</c> attribute is invalid, or when it has no <c>/slideLayout</c> relationship.
    /// </exception>
    internal PptxSlide GetSlide(int slideIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (slideIndex < 0 || slideIndex >= _slidePartPaths.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex), slideIndex, "Slide index is out of range.");
        }

        if (_slideCache.TryGetValue(slideIndex, out var cached))
        {
            return cached;
        }

        var slidePartPath = _slidePartPaths[slideIndex];
        var root = LoadPartXmlRoot(slidePartPath);
        if (root.Name != PresentationNamespace + "sld")
        {
            throw new InvalidDataException($"Part '{slidePartPath}' is not a <p:sld> part.");
        }

        var spTree = root.Element(PresentationNamespace + "cSld")?.Element(PresentationNamespace + "spTree") ??
            throw new InvalidDataException($"Slide '{slidePartPath}' has no <p:cSld>/<p:spTree> element.");

        var placeholders = PptxPlaceholderParser.ParsePlaceholderShapes(spTree);
        var layoutPartPath = ResolveRelationshipByType(slidePartPath, "/slideLayout");

        var slide = new PptxSlide(slidePartPath, layoutPartPath, placeholders);
        _slideCache[slideIndex] = slide;
        return slide;
    }
}
