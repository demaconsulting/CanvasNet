using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore sldsz sldidlst sldid pptx mistargeted

/// <summary>
///     Implements the <see cref="PptxDocument"/> presentation-root parser (Phase 1b):
///     <c>ppt/presentation.xml</c>'s declared slide size (<c>&lt;p:sldSz&gt;</c>) and ordered
///     slide list (<c>&lt;p:sldIdLst&gt;</c>), located via the package root's
///     <c>/officeDocument</c> relationship (not an explicit <c>r:id</c> reference, since the
///     presentation part is the package's single navigation entry point).
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     The OOXML relationship <c>Type</c> URI suffix identifying a slide part, used to
    ///     validate each <c>&lt;p:sldId r:id="..."/&gt;</c>'s resolved relationship actually
    ///     targets a slide (rather than, for example, a theme or an arbitrary mistyped part) -
    ///     see <see cref="ParseSlideIdList"/>.
    /// </summary>
    private const string SlideRelationshipTypeSuffix = "/slide";

    /// <summary>The XML namespace used by PresentationML parts (<c>ppt/presentation.xml</c>, slides, layouts, masters).</summary>
    internal static readonly XNamespace PresentationNamespace =
        "http://schemas.openxmlformats.org/presentationml/2006/main";

    /// <summary>The XML namespace used by the <c>r:id</c> relationship-reference attribute throughout OOXML parts.</summary>
    private static readonly XNamespace RelationshipRefNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>The resolved part path of <c>ppt/presentation.xml</c>, set by <see cref="InitializePresentation"/>.</summary>
    private string _presentationPartPath = string.Empty;

    /// <summary>The presentation's declared slide size, set by <see cref="InitializePresentation"/>.</summary>
    private PptxSlideSize _slideSize;

    /// <summary>
    ///     The resolved part path of each slide declared by <c>&lt;p:sldIdLst&gt;</c>, in document
    ///     order, set by <see cref="InitializePresentation"/>.
    /// </summary>
    private IReadOnlyList<string> _slidePartPaths = [];

    /// <summary>
    ///     Parses <c>ppt/presentation.xml</c> (located via the package root's <c>/officeDocument</c>
    ///     relationship): validates it is a presentation part, reads its declared slide size, and
    ///     resolves its ordered slide list. Called once from the constructor, immediately after
    ///     <see cref="InitializePackage"/>.
    /// </summary>
    /// <remarks>
    ///     Only the presentation part itself is validated eagerly. Each resolved slide part's own
    ///     existence/well-formedness, and its layout/master/theme chain, are validated lazily on
    ///     first access via <see cref="GetSlide(int)"/> - mirroring Phase 1a's own eager
    ///     (package-level relationships)/lazy (per-part relationships) split, so <see cref="Open(Stream)"/>
    ///     only costs "is this a navigable presentation with at least one declared slide", not
    ///     "is every slide/layout/master/theme already fully valid".
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the package has no <c>/officeDocument</c> relationship, when that part is
    ///     missing or not well-formed XML, when its root element is not a PresentationML
    ///     <c>&lt;p:presentation&gt;</c> element, when <c>&lt;p:sldSz&gt;</c> is missing or has a
    ///     missing/non-numeric/non-positive <c>cx</c>/<c>cy</c>, or when <c>&lt;p:sldIdLst&gt;</c>
    ///     is missing, has zero <c>&lt;p:sldId&gt;</c> children, or any child has a missing or
    ///     unresolvable <c>r:id</c>, or an <c>r:id</c> resolving to a relationship whose
    ///     <c>Type</c> does not identify a slide part.
    /// </exception>
    private void InitializePresentation()
    {
        _presentationPartPath = ResolveRelationshipByType(string.Empty, "/officeDocument");
        var root = LoadPartXmlRoot(_presentationPartPath);

        if (root.Name != PresentationNamespace + "presentation")
        {
            throw new InvalidDataException(
                $"The officeDocument part '{_presentationPartPath}' is not a presentation part.");
        }

        _slideSize = ParseSlideSize(root);
        _slidePartPaths = ParseSlideIdList(root, _presentationPartPath);
    }

    /// <summary>
    ///     Parses the root's <c>&lt;p:sldSz cx="..." cy="..."/&gt;</c> child into a
    ///     <see cref="PptxSlideSize"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>&lt;p:sldSz&gt;</c> is missing, or either of its <c>cx</c>/<c>cy</c>
    ///     attributes is missing, non-numeric, or not a positive value.
    /// </exception>
    private static PptxSlideSize ParseSlideSize(XElement presentationRoot)
    {
        var sldSz = presentationRoot.Element(PresentationNamespace + "sldSz") ??
            throw new InvalidDataException("The presentation part has no <p:sldSz> element.");

        var cx = ParsePositiveEmuAttribute(sldSz, "cx");
        var cy = ParsePositiveEmuAttribute(sldSz, "cy");
        return new PptxSlideSize(cx, cy);
    }

    /// <summary>
    ///     Reads and validates a single positive-integer EMU attribute (<c>cx</c> or <c>cy</c>) of
    ///     <c>&lt;p:sldSz&gt;</c>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the attribute is missing, non-numeric, or not strictly positive.
    /// </exception>
    private static long ParsePositiveEmuAttribute(XElement sldSz, string attributeName)
    {
        var value = (string?)sldSz.Attribute(attributeName);
        if (string.IsNullOrEmpty(value) ||
            !long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ||
            parsed <= 0)
        {
            throw new InvalidDataException(
                $"The presentation part's <p:sldSz> has a missing or invalid '{attributeName}' attribute.");
        }

        return parsed;
    }

    /// <summary>
    ///     Parses the root's <c>&lt;p:sldIdLst&gt;</c> child, resolving each
    ///     <c>&lt;p:sldId r:id="..."/&gt;</c> child's relationship into a slide part path, in
    ///     document order.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>&lt;p:sldIdLst&gt;</c> is missing, declares zero <c>&lt;p:sldId&gt;</c>
    ///     children (an empty slide list), any child has a missing <c>r:id</c> attribute or an
    ///     <c>r:id</c> that does not resolve to a relationship of the presentation part, or a
    ///     resolved relationship's <c>Type</c> is not the OOXML slide-relationship type (a
    ///     malformed package pointing a declared slide ID at a theme, layout, or other
    ///     mistargeted part must fail here, at <see cref="Open(Stream)"/> time, rather than only
    ///     later surfacing as a confusing failure from <see cref="GetSlide(int)"/>).
    /// </exception>
    private IReadOnlyList<string> ParseSlideIdList(XElement presentationRoot, string presentationPartPath)
    {
        var sldIdLst = presentationRoot.Element(PresentationNamespace + "sldIdLst") ??
            throw new InvalidDataException("The presentation part has no <p:sldIdLst> element.");

        var slidePartPaths = new List<string>();
        foreach (var sldId in sldIdLst.Elements(PresentationNamespace + "sldId"))
        {
            var relationshipId = (string?)sldId.Attribute(RelationshipRefNamespace + "id");
            if (string.IsNullOrEmpty(relationshipId))
            {
                throw new InvalidDataException("A <p:sldId> element has a missing or empty 'r:id' attribute.");
            }

            var relationshipType = GetRelationshipType(presentationPartPath, relationshipId);
            if (!relationshipType.EndsWith(SlideRelationshipTypeSuffix, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"A <p:sldId> element's relationship '{relationshipId}' has Type '{relationshipType}', which does not identify a slide part.");
            }

            slidePartPaths.Add(ResolveRelationship(presentationPartPath, relationshipId));
        }

        if (slidePartPaths.Count == 0)
        {
            throw new InvalidDataException("The presentation declares no slides (<p:sldIdLst> is empty).");
        }

        return slidePartPaths;
    }

    /// <summary>
    ///     Converts <see cref="SlideSize"/> from its native EMU unit to pixels at the given
    ///     resolution - the EMU analogue of <c>PdfDocument.Render(int, float)</c>'s
    ///     points-to-pixels conversion, provided now (ahead of Phase 1b having any rendering
    ///     surface) so a later rendering phase reuses this exact, already-tested conversion rather
    ///     than re-deriving it.
    /// </summary>
    /// <param name="dpi">The resolution to convert at, in dots (pixels) per inch.</param>
    /// <returns>
    ///     The slide size in pixels at <paramref name="dpi"/>, each dimension rounded to the
    ///     nearest pixel (<see cref="MidpointRounding.AwayFromZero"/>).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="dpi"/> is not a positive, finite number.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when either converted dimension, rounded to the nearest pixel, would fall
    ///     outside the representable <see cref="int"/> range - <c>&lt;p:sldSz&gt;</c> accepts any
    ///     positive EMU value, so a sufficiently large declared dimension (or a sufficiently high
    ///     <paramref name="dpi"/>) must fail closed here rather than silently overflowing into a
    ///     negative or wrapped pixel size.
    /// </exception>
    public (int Width, int Height) GetSlideSizeInPixels(float dpi)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!float.IsFinite(dpi) || dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI must be a positive, finite number.");
        }

        const double emuPerInch = 914400d;
        var scale = dpi / emuPerInch;
        var roundedWidth = Math.Round(SlideSize.WidthEmu * scale, MidpointRounding.AwayFromZero);
        var roundedHeight = Math.Round(SlideSize.HeightEmu * scale, MidpointRounding.AwayFromZero);

        // Reject a rounded dimension outside int's representable range before casting - a direct
        // (int) cast on an out-of-range double silently wraps/truncates to an incorrect value
        // rather than failing closed, which would otherwise be worse than a documented exception.
        if (roundedWidth is < int.MinValue or > int.MaxValue || roundedHeight is < int.MinValue or > int.MaxValue)
        {
            throw new InvalidDataException(
                $"The slide size converted to pixels at {dpi} DPI is outside the representable range.");
        }

        return ((int)roundedWidth, (int)roundedHeight);
    }
}
