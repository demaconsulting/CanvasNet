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
    /// <remarks>
    ///     This layout's own shape tree is parsed with <see cref="ParseShapeTree"/>'s
    ///     <c>containUnsupportedGraphicFrames</c> parameter set to <see langword="true"/>: a
    ///     <c>&lt;p:graphicFrame&gt;</c> of a recognized-but-unsupported (non-table) kind placed
    ///     directly on this layout's own shape tree is skipped rather than aborting this entire
    ///     cached parse - which would otherwise fail every slide using this layout. A slide's own
    ///     such graphic frame is unaffected and still hard-fails <see cref="GetSlide"/>.
    /// </remarks>
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

        var cSld = root.Element(PresentationNamespace + "cSld") ??
            throw new InvalidDataException($"Part '{layoutPartPath}' has no <p:cSld> element.");
        var spTree = cSld.Element(PresentationNamespace + "spTree") ??
            throw new InvalidDataException($"Slide layout '{layoutPartPath}' has no <p:cSld>/<p:spTree> element.");

        var placeholders = PptxPlaceholderParser.ParsePlaceholderShapes(spTree);
        var masterPartPath = ResolveRelationshipByType(layoutPartPath, "/slideMaster");
        var background = cSld.Element(PresentationNamespace + "bg");
        var clrMapOvr = root.Element(PresentationNamespace + "clrMapOvr");

        // The shape tree's <p:graphicFrame> tables resolve their cell fills against the layout's
        // own master's theme. Resolving the master -> theme relationship chain is deferred into
        // this lambda (mirroring GetSlide's own lazy theme resolver) so layouts with no tables at
        // all never require it to be walked.
        //
        // colorMapResolver is intentionally omitted: this layout's own _layoutCache entry is
        // shared by every slide using it (keyed by part path, not by slide), each of which may
        // declare its own distinct <p:clrMapOvr> - baking any single color map into a
        // layout-owned table's cached fill/border here would be correct for some consuming
        // slides and wrong for others. See ParseTable's own colorMap parameter XmlDoc for the
        // full rationale and GetSlide's own colorMapResolver for the case where this is safe.
        var shapeTree = ParseShapeTree(
            spTree, () => GetTheme(GetMaster(masterPartPath).ThemePartPath), tableStyleResolver: TryResolveTableStyle,
            containUnsupportedGraphicFrames: true, resolveBlipImage: blip => ResolvePictureSurface(layoutPartPath, blip));

        var layout = new PptxLayout(layoutPartPath, masterPartPath, placeholders, background, shapeTree, clrMapOvr);
        _layoutCache[layoutPartPath] = layout;
        return layout;
    }
}
