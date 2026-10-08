using System.Globalization;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio PageSheet NameU

/// <summary>
///     Implements the <see cref="VsdxDocument"/> page-index parser: locating
///     <c>visio/document.xml</c> and <c>visio/pages/pages.xml</c> through the package's
///     relationship graph (never by filename convention), and parsing each declared
///     <c>&lt;Page&gt;</c>'s name and declared <c>PageWidth</c>/<c>PageHeight</c> size.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>
    ///     The number of EMU (English Metric Units) per inch, used to convert a page's raw,
    ///     always-inches <c>PageWidth</c>/<c>PageHeight</c> cell value into the document's
    ///     internal unit - the same constant already used by <c>PptxSlideSize</c>. Widened from
    ///     <see langword="private"/> to <see langword="internal"/> in Milestone 7 so
    ///     <c>VsdxDocument.Render.cs</c>'s public Render API can reuse this single already-defined
    ///     unit-conversion constant instead of duplicating the magic number.
    /// </summary>
    internal const double EmuPerInch = 914_400d;

    /// <summary>
    ///     The relationship <c>Type</c> URI suffix identifying the package root's relationship to
    ///     <c>visio/document.xml</c> (required).
    /// </summary>
    private const string DocumentRelationshipTypeSuffix = "/relationships/document";

    /// <summary>
    ///     The relationship <c>Type</c> URI suffix identifying <c>visio/document.xml</c>'s
    ///     relationship to <c>visio/masters/masters.xml</c> (optional - resolved if present,
    ///     stored for a later milestone's use, never required to exist; see
    ///     <see cref="InitializePages"/>'s own remarks).
    /// </summary>
    private const string MastersRelationshipTypeSuffix = "/relationships/masters";

    /// <summary>
    ///     The relationship <c>Type</c> URI suffix identifying <c>visio/document.xml</c>'s
    ///     relationship to <c>visio/pages/pages.xml</c> (required).
    /// </summary>
    private const string PagesRelationshipTypeSuffix = "/relationships/pages";

    /// <summary>
    ///     The relationship <c>Type</c> URI suffix identifying <c>visio/document.xml</c>'s
    ///     relationship to <c>visio/theme/theme1.xml</c> (optional - resolved if present, stored
    ///     for a later milestone's use, never required to exist).
    /// </summary>
    private const string ThemeRelationshipTypeSuffix = "/relationships/theme";

    /// <summary>
    ///     The XML namespace used by <c>visio/document.xml</c>, <c>visio/pages/pages.xml</c>, and
    ///     every other VisioML part (confirmed present, verbatim, in every real-world <c>.vsdx</c>
    ///     fixture inspected while designing this unit).
    /// </summary>
    private static readonly XNamespace VsdxMainNamespace = "http://schemas.microsoft.com/office/visio/2012/main";

    /// <summary>
    ///     The XML namespace of the <c>r:id</c> attribute on a VisioML <c>&lt;Rel&gt;</c> child
    ///     element (a <c>&lt;Page&gt;</c>'s or <c>&lt;Master&gt;</c>'s own pointer to its content
    ///     part) - the standard OOXML "officeDocument relationships" namespace, distinct from
    ///     <c>VsdxDocument.Package.cs</c>'s own <c>RelationshipsNamespace</c> (the <em>package</em>
    ///     relationships namespace used by <c>.rels</c> part root elements).
    /// </summary>
    private static readonly XNamespace VsdxRelationshipsNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>The resolved part path of <c>visio/document.xml</c>, set by <see cref="InitializePages"/>.</summary>
    private string _documentPartPath = string.Empty;

    /// <summary>
    ///     The resolved part path of <c>visio/masters/masters.xml</c>, or <see langword="null"/>
    ///     when the package declares no <c>masters</c> relationship, set by
    ///     <see cref="InitializePages"/>. Not parsed further this milestone - reserved for a
    ///     later milestone's master/shape-inheritance resolution.
    /// </summary>
    private string? _mastersPartPath;

    /// <summary>
    ///     The resolved part path of <c>visio/theme/theme1.xml</c>, or <see langword="null"/>
    ///     when the package declares no <c>theme</c> relationship, set by
    ///     <see cref="InitializePages"/>. Not parsed further this milestone - reserved for a
    ///     later milestone's theme/color resolution.
    /// </summary>
    private string? _themePartPath;

    /// <summary>
    ///     The parsed page index, in document order, set by <see cref="InitializePages"/>.
    /// </summary>
    private IReadOnlyList<VsdxPageInfo> _pages = [];

    /// <summary>
    ///     Each page's own content part path (<c>visio/pages/pageN.xml</c>), resolved via that
    ///     page's own <c>&lt;Rel r:id="..."/&gt;</c> child (never by filename convention), in the
    ///     same order as <see cref="_pages"/>. Set by <see cref="InitializePages"/>; consumed by
    ///     <c>VsdxDocument.Shapes.cs</c>'s shape-tree parser.
    /// </summary>
    private IReadOnlyList<string> _pageContentPartPaths = [];

    /// <summary>
    ///     Returns the given page's own content part path (<c>visio/pages/pageN.xml</c>),
    ///     resolved via that page's own <c>&lt;Rel r:id="..."/&gt;</c> child.
    /// </summary>
    /// <param name="pageIndex">The zero-based page index, in <c>[0, PageCount)</c>.</param>
    /// <returns>The page's content part path.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the page declares no <c>&lt;Rel&gt;</c> child, or its relationship could
    ///     not be resolved.
    /// </exception>
    private string GetPageContentPartPath(int pageIndex)
    {
        var partPath = _pageContentPartPaths[pageIndex];
        if (partPath.Length == 0)
        {
            throw new InvalidDataException($"Page index {pageIndex} declares no resolvable content part relationship.");
        }

        return partPath;
    }

    /// <summary>
    ///     Gets the resolved part path of <c>visio/masters/masters.xml</c>, or
    ///     <see langword="null"/> when the package declares no <c>masters</c> relationship - not
    ///     parsed further this milestone, exposed only so a later milestone's master/shape-
    ///     inheritance resolver, and this milestone's own tests, can confirm it was resolved via
    ///     the relationship graph (rather than by filename convention).
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    internal string? MastersPartPath
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _mastersPartPath;
        }
    }

    /// <summary>
    ///     Gets the resolved part path of <c>visio/theme/theme1.xml</c>, or
    ///     <see langword="null"/> when the package declares no <c>theme</c> relationship - not
    ///     parsed further this milestone, exposed only so a later milestone's theme/color
    ///     resolver, and this milestone's own tests, can confirm it was resolved via the
    ///     relationship graph (rather than by filename convention).
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    internal string? ThemePartPath
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _themePartPath;
        }
    }

    /// <summary>
    ///     Resolves <c>visio/document.xml</c> and <c>visio/pages/pages.xml</c> through the
    ///     package's relationship graph, optionally resolves <c>visio/masters/masters.xml</c> and
    ///     <c>visio/theme/theme1.xml</c> when present, and parses every declared
    ///     <c>&lt;Page&gt;</c> into <see cref="_pages"/>. Called once from the constructor,
    ///     immediately after <see cref="InitializePackage"/>.
    /// </summary>
    /// <remarks>
    ///     <c>masters.xml</c>/<c>theme1.xml</c> are resolved via
    ///     <see cref="TryResolveRelationshipByType"/> (not required to exist) rather than
    ///     <see cref="ResolveRelationshipByType"/> (required): a real, valid <c>.vsdx</c> package
    ///     may declare no masters at all (for example a diagram built from bare, unstyled
    ///     shapes), and this class's own documented <c>Open</c> exception contract lists only
    ///     <c>[Content_Types].xml</c>, <c>_rels/.rels</c>, <c>visio/document.xml</c>, and
    ///     <c>visio/pages/pages.xml</c> as parts whose absence is rejected.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the package has no <c>document</c> relationship from its root, when
    ///     <c>visio/document.xml</c> is missing or not well-formed XML, when its root element is
    ///     not a <c>&lt;VisioDocument&gt;</c> element, when <c>visio/document.xml</c> has no
    ///     <c>pages</c> relationship, when <c>visio/pages/pages.xml</c> is missing or not
    ///     well-formed XML, when its root element is not a <c>&lt;Pages&gt;</c> element, when it
    ///     declares zero <c>&lt;Page&gt;</c> children, or when any <c>&lt;Page&gt;</c> fails to
    ///     parse (see <see cref="ParsePage"/>'s own exception conditions).
    /// </exception>
    private void InitializePages()
    {
        _documentPartPath = ResolveRelationshipByType(string.Empty, DocumentRelationshipTypeSuffix);
        var documentRoot = LoadPartXmlRoot(_documentPartPath);
        if (documentRoot.Name != VsdxMainNamespace + "VisioDocument")
        {
            throw new InvalidDataException(
                $"The document part '{_documentPartPath}' is not a VisioDocument part.");
        }

        _ = TryResolveRelationshipByType(_documentPartPath, MastersRelationshipTypeSuffix, out var mastersPartPath);
        _mastersPartPath = mastersPartPath;

        _ = TryResolveRelationshipByType(_documentPartPath, ThemeRelationshipTypeSuffix, out var themePartPath);
        _themePartPath = themePartPath;

        var pagesPartPath = ResolveRelationshipByType(_documentPartPath, PagesRelationshipTypeSuffix);
        var pagesRoot = LoadPartXmlRoot(pagesPartPath);
        if (pagesRoot.Name != VsdxMainNamespace + "Pages")
        {
            throw new InvalidDataException(
                $"The pages part '{pagesPartPath}' is not a Pages part.");
        }

        var pageElements = pagesRoot.Elements(VsdxMainNamespace + "Page").ToList();
        if (pageElements.Count == 0)
        {
            throw new InvalidDataException(
                $"The pages part '{pagesPartPath}' declares no <Page> elements.");
        }

        var pages = new List<VsdxPageInfo>(pageElements.Count);
        pages.AddRange(pageElements.Select(ParsePage));

        _pages = pages;

        var contentPartPaths = new List<string>(pageElements.Count);
        foreach (var pageElement in pageElements)
        {
            var relId = (string?)pageElement.Element(VsdxMainNamespace + "Rel")?.Attribute(VsdxRelationshipsNamespace + "id");
            contentPartPaths.Add(
                relId is not null && TryResolveRelationshipById(pagesPartPath, relId, out var contentPartPath)
                    ? contentPartPath
                    : string.Empty);
        }

        _pageContentPartPaths = contentPartPaths;
    }

    /// <summary>
    ///     Parses a single <c>&lt;Page&gt;</c> element into a <see cref="VsdxPageInfo"/>: its name
    ///     (falling back to <c>NameU</c> when <c>Name</c> is absent or empty, matching how Visio
    ///     itself treats <c>NameU</c> as the universal/invariant name) and its nested
    ///     <c>&lt;PageSheet&gt;</c>'s <c>PageWidth</c>/<c>PageHeight</c> cells, converted from
    ///     inches to EMU.
    /// </summary>
    /// <param name="pageElement">The <c>&lt;Page&gt;</c> element to parse.</param>
    /// <returns>The parsed <see cref="VsdxPageInfo"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="pageElement"/> has no nested <c>&lt;PageSheet&gt;</c>
    ///     element, when that element has no <c>PageWidth</c>/<c>PageHeight</c> <c>&lt;Cell&gt;</c>
    ///     child, or when either cell's <c>V</c> attribute is missing, non-numeric, non-finite, or
    ///     not strictly positive.
    /// </exception>
    private static VsdxPageInfo ParsePage(XElement pageElement)
    {
        var name = (string?)pageElement.Attribute("Name");
        if (string.IsNullOrEmpty(name))
        {
            name = (string?)pageElement.Attribute("NameU") ?? string.Empty;
        }

        var pageSheet = pageElement.Element(VsdxMainNamespace + "PageSheet") ??
            throw new InvalidDataException("A <Page> element has no nested <PageSheet> element.");

        var widthEmu = ParseSizeCellEmu(pageSheet, "PageWidth");
        var heightEmu = ParseSizeCellEmu(pageSheet, "PageHeight");

        return new VsdxPageInfo(name, widthEmu, heightEmu);
    }

    /// <summary>
    ///     Finds <paramref name="pageSheet"/>'s <c>&lt;Cell N="{cellName}"&gt;</c> child and
    ///     converts its <c>V</c> attribute (always expressed in inches, regardless of any
    ///     <c>U=</c> attribute - see <see cref="VsdxPageInfo"/>'s own worked example) into a
    ///     rounded EMU value.
    /// </summary>
    /// <param name="pageSheet">The <c>&lt;PageSheet&gt;</c> element to search.</param>
    /// <param name="cellName">The <c>&lt;Cell&gt;</c>'s required <c>N</c> attribute value.</param>
    /// <returns>The cell's value, converted from inches to EMU and rounded to the nearest whole EMU.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when no <c>&lt;Cell N="{cellName}"&gt;</c> child exists, its <c>V</c>
    ///     attribute is missing, non-numeric, non-finite, or not strictly positive, or the
    ///     resulting EMU value would overflow <see cref="long"/> (an untrusted/malformed
    ///     page-dimension cell carrying a pathologically large but still-positive-and-finite
    ///     value - see this method's own remarks).
    /// </exception>
    private static long ParseSizeCellEmu(XElement pageSheet, string cellName)
    {
        var cell = pageSheet
            .Elements(VsdxMainNamespace + "Cell")
            .FirstOrDefault(element => (string?)element.Attribute("N") == cellName) ??
            throw new InvalidDataException($"The <PageSheet> element has no <Cell N=\"{cellName}\"> child.");

        var rawValue = (string?)cell.Attribute("V");
        if (string.IsNullOrEmpty(rawValue) ||
            !double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var inches) ||
            !double.IsFinite(inches) ||
            inches <= 0)
        {
            throw new InvalidDataException(
                $"The <Cell N=\"{cellName}\"> child has a missing, non-numeric, non-finite, or non-positive 'V' attribute.");
        }

        // A pathologically large (but still finite and positive) inch value can scale past
        // long.MaxValue once multiplied by EmuPerInch - the `checked` cast below would then
        // throw the framework's own OverflowException, breaking this method's documented
        // InvalidDataException-only error contract for malformed page dimensions. Catch that
        // overflow explicitly and re-surface it as the same InvalidDataException every other
        // malformed-cell case already throws, rather than letting an internal implementation
        // detail (the EMU conversion's own integer width) leak through as a different exception
        // type.
        var scaledEmu = Math.Round(inches * EmuPerInch, MidpointRounding.AwayFromZero);
        try
        {
            return checked((long)scaledEmu);
        }
        catch (OverflowException)
        {
            throw new InvalidDataException(
                $"The <Cell N=\"{cellName}\"> child's 'V' attribute ({rawValue}) is too large to convert to EMU.");
        }
    }
}
