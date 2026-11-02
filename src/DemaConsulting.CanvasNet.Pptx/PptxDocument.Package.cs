using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Packaging;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore pptx ooxml navigations

/// <summary>
///     Implements the <see cref="PptxDocument"/> OOXML (Office Open XML) package layer as a thin
///     adapter over the core <see cref="OpcPackage"/>: the ZIP/OPC/content-types/relationships
///     mechanics, and every size and XML-hardening bound, live in <see cref="OpcPackage"/> and
///     <see cref="SafeXml"/>; this file only translates the core <see cref="OpcFormatException"/> into
///     this class's documented <see cref="InvalidDataException"/> (same message, with the core
///     exception as the inner exception). The other <c>PptxDocument.*.cs</c> partials layer
///     presentation/theme/master/layout/slide/text parsing on top of it; see
///     <see cref="PptxDocument"/>'s own remarks for this class's full, current scope.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     The opened OPC package that backs this document for its lifetime; disposed by
    ///     <see cref="Dispose"/>.
    /// </summary>
    private readonly OpcPackage _package;

    /// <summary>
    ///     Set once <see cref="Dispose"/> has been called.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     The decoded picture <see cref="Surface"/>s owned by this document - populated only by
    ///     <see cref="ResolveAndOwnPictureSurface"/> (table-cell <c>&lt;a:blipFill&gt;</c> images,
    ///     resolved once at parse time and cached inside a master/layout/slide's own cached shape
    ///     tree, so they outlive any single <see cref="Render(int, int, int, PptxRenderOptions?)"/>
    ///     call and must instead be disposed here, by <see cref="Dispose"/>).
    /// </summary>
    private readonly List<Surface> _ownedImageSurfaces = [];

    /// <summary>
    ///     Resolves the given OPC part's content type (see <see cref="OpcPackage.ResolvePart"/>).
    /// </summary>
    /// <param name="partPath">
    ///     The part's path within the package (for example <c>"ppt/presentation.xml"</c>), with
    ///     or without a leading slash.
    /// </param>
    /// <returns>The resolved content type string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="partPath"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the package does not contain a part named <paramref name="partPath"/>, or
    ///     when no content type can be resolved for it (no matching override and no default
    ///     mapping for its extension).
    /// </exception>
    internal string ResolvePart(string partPath)
    {
        try
        {
            return _package.ResolvePart(partPath);
        }
        catch (OpcFormatException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Resolves the target part path of the relationship identified by
    ///     <paramref name="relationshipId"/> within <paramref name="sourcePartPath"/>'s
    ///     relationships (see <see cref="OpcPackage.ResolveRelationship"/>).
    /// </summary>
    /// <param name="sourcePartPath">
    ///     The source part's path (for example <c>"ppt/slides/slide1.xml"</c>), or the empty
    ///     string to resolve a package-level relationship (from <c>_rels/.rels</c>).
    /// </param>
    /// <param name="relationshipId">The relationship's <c>Id</c> attribute value (for example <c>"rId1"</c>).</param>
    /// <returns>The resolved target part path, with no leading slash.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="sourcePartPath"/> or <paramref name="relationshipId"/> is null.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="sourcePartPath"/> has no relationship named
    ///     <paramref name="relationshipId"/>, when that relationship targets an external resource
    ///     (<c>TargetMode="External"</c>, not supported), or when its resolved target would escape
    ///     the package root (an unbalanced leading <c>"../"</c>).
    /// </exception>
    internal string ResolveRelationship(string sourcePartPath, string relationshipId)
    {
        try
        {
            return _package.ResolveRelationship(sourcePartPath, relationshipId);
        }
        catch (OpcFormatException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Returns the <c>Type</c> attribute value of the relationship identified by
    ///     <paramref name="relationshipId"/> within <paramref name="sourcePartPath"/>'s
    ///     relationships, used by callers (for example <c>ParseSlideIdList</c>) that must confirm
    ///     a relationship points at the expected kind of part before trusting its resolved target
    ///     - resolving a relationship's target path alone (<see cref="ResolveRelationship"/>)
    ///     cannot distinguish "points at a slide" from "points at an arbitrary, wrongly-typed
    ///     part".
    /// </summary>
    /// <param name="sourcePartPath">
    ///     The source part's path, or the empty string for a package-level relationship.
    /// </param>
    /// <param name="relationshipId">The relationship's <c>Id</c> attribute value.</param>
    /// <returns>The relationship's <c>Type</c> attribute value (never null or empty).</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="sourcePartPath"/> or <paramref name="relationshipId"/> is null.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="sourcePartPath"/> has no relationship named
    ///     <paramref name="relationshipId"/>.
    /// </exception>
    internal string GetRelationshipType(string sourcePartPath, string relationshipId)
    {
        try
        {
            return _package.GetRelationshipType(sourcePartPath, relationshipId);
        }
        catch (OpcFormatException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Resolves the target part path of the single relationship of
    ///     <paramref name="sourcePartPath"/> whose <c>Type</c> attribute ends with
    ///     <paramref name="relationshipTypeSuffix"/>, used for navigations that have no explicit
    ///     <c>r:id</c> reference in the source part's own XML content (for example the package
    ///     root's relationship to <c>ppt/presentation.xml</c>, or a slide's relationship to its
    ///     layout) - see <see cref="ResolveRelationship"/> for navigations that do carry an
    ///     explicit <c>r:id</c>.
    /// </summary>
    /// <param name="sourcePartPath">The source part's path, or empty string for the package root.</param>
    /// <param name="relationshipTypeSuffix">
    ///     The relationship Type URI suffix to match (for example <c>"/officeDocument"</c>),
    ///     compared with an ordinal, case-sensitive <see cref="string.EndsWith(string, StringComparison)"/>.
    /// </param>
    /// <returns>The resolved target part path, with no leading slash.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="sourcePartPath"/> or <paramref name="relationshipTypeSuffix"/>
    ///     is null.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when no non-external relationship of <paramref name="sourcePartPath"/> has a
    ///     <c>Type</c> ending with <paramref name="relationshipTypeSuffix"/>.
    /// </exception>
    internal string ResolveRelationshipByType(string sourcePartPath, string relationshipTypeSuffix)
    {
        try
        {
            return _package.ResolveRelationshipByType(sourcePartPath, relationshipTypeSuffix);
        }
        catch (OpcFormatException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Non-throwing sibling of <see cref="ResolveRelationshipByType"/>: returns
    ///     <see langword="null"/> instead of throwing when no such relationship exists. Used for
    ///     navigations to an optional, auxiliary part (for example <c>ppt/tableStyles.xml</c>, via
    ///     <see cref="TryResolveTableStyle"/>) whose absence must degrade gracefully rather than
    ///     fail the whole parse closed, unlike <see cref="ResolveRelationshipByType"/>'s own
    ///     fail-closed behavior for a required navigation.
    /// </summary>
    /// <param name="sourcePartPath">The source part's path, or empty string for the package root.</param>
    /// <param name="relationshipTypeSuffix">
    ///     The relationship Type URI suffix to match, compared with an ordinal, case-sensitive
    ///     <see cref="string.EndsWith(string, StringComparison)"/>.
    /// </param>
    /// <returns>
    ///     The resolved target part path, with no leading slash, or <see langword="null"/> when no
    ///     non-external relationship of <paramref name="sourcePartPath"/> has a <c>Type</c> ending
    ///     with <paramref name="relationshipTypeSuffix"/> (including when <paramref name="sourcePartPath"/>
    ///     has no relationships at all).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="sourcePartPath"/> or <paramref name="relationshipTypeSuffix"/>
    ///     is null.
    /// </exception>
    internal string? TryResolveRelationshipByType(string sourcePartPath, string relationshipTypeSuffix)
    {
        try
        {
            return _package.TryResolveRelationshipByType(sourcePartPath, relationshipTypeSuffix);
        }
        catch (OpcFormatException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Loads and returns the root XML element of the part at <paramref name="partPath"/>,
    ///     wrapping a missing part or malformed XML in <see cref="InvalidDataException"/>. Shared
    ///     by every presentation-specific parser (Presentation/Theme/Masters/Layouts/Slides) so
    ///     each implements only its own element-shape parsing, not part lookup/XML-load error
    ///     handling.
    /// </summary>
    /// <param name="partPath">The part's path within the package, with or without a leading slash.</param>
    /// <returns>The part's root <see cref="XElement"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="partPath"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the package does not contain a part named <paramref name="partPath"/>, or
    ///     when the part is not well-formed XML.
    /// </exception>
    internal XElement LoadPartXmlRoot(string partPath)
    {
        try
        {
            return _package.LoadPartXmlRoot(partPath);
        }
        catch (OpcFormatException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Reads and returns the raw, undecoded bytes of the part at <paramref name="partPath"/> -
    ///     the counterpart to <see cref="LoadPartXmlRoot"/> for a part whose content is not XML at
    ///     all (a media part, for example <c>ppt/media/image1.png</c>, referenced by a
    ///     <c>&lt;p:pic&gt;</c>'s <c>&lt;a:blip r:embed="..."/&gt;</c> - see
    ///     <see cref="ResolvePictureSurface"/>, this method's sole caller).
    /// </summary>
    /// <param name="partPath">The part's path within the package, with or without a leading slash.</param>
    /// <returns>The part's full, raw byte content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="partPath"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the package does not contain a part named <paramref name="partPath"/>, or
    ///     when the part's decompressed content exceeds <see cref="OpcPackage.MaxPartBytes"/>
    ///     (counted while copying, never taken from the ZIP entry's declared length).
    /// </exception>
    internal byte[] GetPartBytes(string partPath)
    {
        try
        {
            return _package.GetPartBytes(partPath);
        }
        catch (OpcFormatException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }
}
