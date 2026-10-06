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
    ///     <c>idx</c> attribute is invalid, when it has no <c>/slideLayout</c> relationship, or
    ///     (via <see cref="ParseShapeTree"/>, lazily resolving this slide's layout/master/theme
    ///     chain only when its shape tree actually declares a <c>&lt;p:graphicFrame&gt;</c> table)
    ///     when any part along that chain is malformed.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown (via <see cref="ParseShapeTree"/>) when this <strong>slide's own</strong>
    ///     <c>&lt;p:graphicFrame&gt;</c>'s <c>&lt;a:graphicData&gt;</c> declares a
    ///     recognized-but-unsupported (non-table) kind - <see cref="ParseShapeTree"/> is called
    ///     here with its default <c>containUnsupportedGraphicFrames: false</c>, so (unlike
    ///     <see cref="GetLayout"/>/<see cref="GetMaster"/>, which pass <see langword="true"/>)
    ///     this always propagates rather than being silently skipped.
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

        var cSld = root.Element(PresentationNamespace + "cSld") ??
            throw new InvalidDataException($"Slide '{slidePartPath}' has no <p:cSld> element.");
        var spTree = cSld.Element(PresentationNamespace + "spTree") ??
            throw new InvalidDataException($"Slide '{slidePartPath}' has no <p:cSld>/<p:spTree> element.");

        var placeholders = PptxPlaceholderParser.ParsePlaceholderShapes(spTree);
        var layoutPartPath = ResolveRelationshipByType(slidePartPath, "/slideLayout");
        var background = cSld.Element(PresentationNamespace + "bg");
        var clrMapOvr = root.Element(PresentationNamespace + "clrMapOvr");

        // The shape tree's <p:graphicFrame> tables resolve their cell fills against the slide's
        // own theme (see ParseTable's theme parameter). Resolving the layout -> master -> theme
        // relationship chain (the same chain a caller would otherwise walk manually - see
        // PptxSystemIntegrationTests.cs's own ResolveTheme helper) is deferred into this lambda
        // so slides with no tables at all never require it to be walked.
        var shapeTree = ParseShapeTree(spTree, () =>
        {
            var layout = GetLayout(layoutPartPath);
            var master = GetMaster(layout.MasterPartPath);
            return GetTheme(master.ThemePartPath);
        }, tableStyleResolver: TryResolveTableStyle,
        colorMapResolver: () =>
        {
            // Safe to compute lazily (mirroring themeResolver's own laziness) because
            // _slideCache is keyed 1:1 by slide index: this slide's own effective color map is
            // always the same deterministic value for every future consumer of this cached
            // PptxSlide - unlike GetLayout's/GetMaster's own shared caches, which are reused by
            // every slide through that layout/master and therefore intentionally left without a
            // colorMapResolver (see ParseTable's own colorMap parameter XmlDoc).
            var layout = GetLayout(layoutPartPath);
            var master = GetMaster(layout.MasterPartPath);
            return ResolveEffectiveColorMap(clrMapOvr, layout.ClrMapOvr, master.ColorMap);
        }, resolveBlipImage: blip => ResolvePictureSurface(slidePartPath, blip),
        resolveChartPart: id => LoadPartXmlRoot(ResolveRelationship(slidePartPath, id)));

        var slide = new PptxSlide(slidePartPath, layoutPartPath, placeholders, shapeTree, background, clrMapOvr);
        _slideCache[slideIndex] = slide;
        return slide;
    }
}
