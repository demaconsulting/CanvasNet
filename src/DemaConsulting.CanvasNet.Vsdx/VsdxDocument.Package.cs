using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Packaging;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx ooxml visio navigations

/// <summary>
///     Implements the <see cref="VsdxDocument"/> OPC (Open Packaging Conventions) package layer as a
///     thin adapter over the core <see cref="OpcPackage"/>: the ZIP/OPC/content-types/relationships
///     mechanics, and every size and XML-hardening bound, live in <see cref="OpcPackage"/> and
///     <see cref="SafeXml"/>; this file only translates the core <see cref="OpcFormatException"/>
///     into this class's documented <see cref="InvalidDataException"/> (same message, with the core
///     exception as the inner exception). <c>VsdxDocument.Pages.cs</c> layers page-index parsing on
///     top of it; see <see cref="VsdxDocument"/>'s own remarks for this class's full, current scope.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>
    ///     The opened OPC package that backs this document for its lifetime; disposed by
    ///     <see cref="Dispose"/>.
    /// </summary>
    private readonly OpcPackage _package;

    /// <summary>
    ///     Resolves the target part path of the single relationship of
    ///     <paramref name="sourcePartPath"/> whose <c>Type</c> attribute ends with
    ///     <paramref name="relationshipTypeSuffix"/>, used for navigations that have no explicit
    ///     <c>r:id</c> reference in the source part's own XML content (for example the package
    ///     root's relationship to <c>visio/document.xml</c>, or <c>visio/document.xml</c>'s
    ///     relationship to <c>visio/pages/pages.xml</c>).
    /// </summary>
    /// <param name="sourcePartPath">The source part's path, or empty string for the package root.</param>
    /// <param name="relationshipTypeSuffix">
    ///     The relationship Type URI suffix to match (for example <c>"/pages"</c>), compared with
    ///     an ordinal, case-sensitive <see cref="string.EndsWith(string, StringComparison)"/>.
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
    private string ResolveRelationshipByType(string sourcePartPath, string relationshipTypeSuffix)
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
    ///     Non-throwing sibling of <see cref="ResolveRelationshipByType"/>: resolves the target
    ///     part path of the single relationship of <paramref name="sourcePartPath"/> whose
    ///     <c>Type</c> attribute ends with <paramref name="relationshipTypeSuffix"/>, returning
    ///     <see langword="false"/> instead of throwing when no such relationship exists. Used for
    ///     navigations to an optional, auxiliary part (for example <c>visio/masters/masters.xml</c>
    ///     or <c>visio/theme/theme1.xml</c>) whose absence must degrade gracefully rather than
    ///     fail the whole parse closed, unlike <see cref="ResolveRelationshipByType"/>'s own
    ///     fail-closed behavior for a required navigation.
    /// </summary>
    /// <param name="sourcePartPath">The source part's path, or empty string for the package root.</param>
    /// <param name="relationshipTypeSuffix">
    ///     The relationship Type URI suffix to match, compared with an ordinal, case-sensitive
    ///     <see cref="string.EndsWith(string, StringComparison)"/> - see
    ///     <see cref="ResolveRelationshipByType"/>'s matching parameter.
    /// </param>
    /// <param name="resolvedPartPath">
    ///     Set to the resolved target part path, with no leading slash, when a match is found;
    ///     otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when a non-external relationship of
    ///     <paramref name="sourcePartPath"/> has a <c>Type</c> ending with
    ///     <paramref name="relationshipTypeSuffix"/>; otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="sourcePartPath"/> or <paramref name="relationshipTypeSuffix"/>
    ///     is null.
    /// </exception>
    private bool TryResolveRelationshipByType(
        string sourcePartPath,
        string relationshipTypeSuffix,
        [NotNullWhen(true)] out string? resolvedPartPath)
    {
        try
        {
            resolvedPartPath = _package.TryResolveRelationshipByType(sourcePartPath, relationshipTypeSuffix);
        }
        catch (OpcFormatException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }

        return resolvedPartPath is not null;
    }

    /// <summary>
    ///     Resolves the target part path of the single relationship of
    ///     <paramref name="sourcePartPath"/> whose <c>Id</c> is <paramref name="relationshipId"/>
    ///     - used for every VisioML <c>&lt;Rel r:id="..."/&gt;</c> child element (a
    ///     <c>&lt;Page&gt;</c>'s or <c>&lt;Master&gt;</c>'s own pointer to its content part),
    ///     which must be addressed by its explicit relationship <c>Id</c> rather than by
    ///     <c>Type</c>: a single shared <c>.rels</c> part (for example
    ///     <c>visio/pages/_rels/pages.xml.rels</c>) commonly declares several relationships of the
    ///     identical <c>Type</c> (one per sibling <c>&lt;Page&gt;</c>/<c>&lt;Master&gt;</c>), so
    ///     <see cref="TryResolveRelationshipByType"/>'s "the one relationship of this type" search
    ///     would not disambiguate between them.
    /// </summary>
    /// <param name="sourcePartPath">The source part's path, or empty string for the package root.</param>
    /// <param name="relationshipId">The relationship's explicit <c>Id</c> attribute value, from a <c>&lt;Rel r:id="..."/&gt;</c> child element.</param>
    /// <param name="resolvedPartPath">
    ///     Set to the resolved target part path, with no leading slash, when a match is found;
    ///     otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when a non-external relationship of
    ///     <paramref name="sourcePartPath"/> has an <c>Id</c> equal to
    ///     <paramref name="relationshipId"/>; otherwise <see langword="false"/>.
    /// </returns>
    private bool TryResolveRelationshipById(
        string sourcePartPath,
        string relationshipId,
        [NotNullWhen(true)] out string? resolvedPartPath)
    {
        try
        {
            resolvedPartPath = _package.TryResolveRelationshipById(sourcePartPath, relationshipId);
        }
        catch (OpcFormatException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }

        return resolvedPartPath is not null;
    }

    /// <summary>
    ///     Loads and returns the root XML element of the part at <paramref name="partPath"/>,
    ///     wrapping a missing part or malformed XML in <see cref="InvalidDataException"/>. Shared
    ///     by every part-specific parser so each implements only its own element-shape parsing,
    ///     not part lookup/XML-load error handling.
    /// </summary>
    /// <param name="partPath">The part's path within the package, with or without a leading slash.</param>
    /// <returns>The part's root <see cref="XElement"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="partPath"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the package does not contain a part named <paramref name="partPath"/>, or
    ///     when the part is not well-formed XML.
    /// </exception>
    private XElement LoadPartXmlRoot(string partPath)
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
}
