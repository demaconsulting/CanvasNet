using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore pptx ooxml navigations ordinally mistargeted

/// <summary>
///     Implements the <see cref="PptxDocument"/> OOXML (Office Open XML) package layer: opening
///     the <c>.pptx</c> file as a ZIP archive, resolving <c>[Content_Types].xml</c>, and
///     resolving package- and part-level relationships. This file implements only the
///     package-layer (OPC/ZIP/content-types/relationships) mechanics - the other
///     <c>PptxDocument.*.cs</c> partials layer presentation/theme/master/layout/slide/text
///     parsing on top of it; see <see cref="PptxDocument"/>'s own remarks for this class's full,
///     current scope.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>The well-known part name of the package's content-types stream.</summary>
    private const string ContentTypesPartName = "[Content_Types].xml";

    /// <summary>The well-known part name of the package-level relationships part.</summary>
    private const string PackageRelationshipsPartName = "_rels/.rels";

    /// <summary>
    ///     The maximum number of characters <see cref="LoadXmlRoot"/> allows an
    ///     <see cref="XmlReader"/> to read while parsing <c>[Content_Types].xml</c> or any
    ///     <c>.rels</c> part, bounding the worst-case parse-time memory/CPU cost of an
    ///     attacker-controlled, highly inflated ZIP entry (an "XML bomb") before it is ever
    ///     materialized into an in-memory DOM. 2,000,000 characters is far larger than any
    ///     real-world <c>[Content_Types].xml</c> or <c>.rels</c> part (these parts list only the
    ///     package's own extension/override/relationship declarations, never arbitrary document
    ///     content), while remaining small enough to keep worst-case memory bounded to a small,
    ///     practical amount - mirroring <c>SvgCodec.MaxDocumentCharacters</c>'s own "generous but
    ///     bounded" sizing rationale for the equivalent attacker-controlled-XML risk.
    /// </summary>
    private const int MaxPartCharacters = 2_000_000;

    /// <summary>The XML namespace used by <c>[Content_Types].xml</c>.</summary>
    private static readonly XNamespace ContentTypesNamespace =
        "http://schemas.openxmlformats.org/package/2006/content-types";

    /// <summary>The XML namespace used by every <c>.rels</c> relationships part.</summary>
    private static readonly XNamespace RelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>
    ///     The non-writable, in-memory stream wrapping the package's buffered bytes that backs
    ///     <see cref="_archive"/> for the lifetime of this instance.
    /// </summary>
    private readonly MemoryStream _stream;

    /// <summary>The opened, read-only ZIP archive view of this package.</summary>
    private readonly ZipArchive _archive;

    /// <summary>
    ///     Every part name present in the package, for existence checks, keyed case-sensitively
    ///     per OPC's own ordinal part-name comparison rule (see <see cref="BuildEntryLookup"/>).
    /// </summary>
    private readonly IReadOnlyDictionary<string, ZipArchiveEntry> _entriesByPath;

    /// <summary>
    ///     The <c>[Content_Types].xml</c> default extension-to-content-type mappings (for
    ///     example <c>"xml" -&gt; "application/xml"</c>), keyed by extension without a leading dot,
    ///     case-insensitively.
    /// </summary>
    private readonly Dictionary<string, string> _defaultContentTypes;

    /// <summary>
    ///     The <c>[Content_Types].xml</c> part-specific content-type overrides, keyed by the
    ///     normalized part path (no leading slash), which take precedence over
    ///     <see cref="_defaultContentTypes"/> for the same part.
    /// </summary>
    private readonly Dictionary<string, string> _overrideContentTypes;

    /// <summary>
    ///     Caches each part's parsed relationships the first time <see cref="ResolveRelationship"/>
    ///     resolves against it, keyed by the normalized source part path (the empty string
    ///     represents the package root, whose relationships live in
    ///     <see cref="PackageRelationshipsPartName"/>).
    /// </summary>
    private readonly Dictionary<string, IReadOnlyDictionary<string, PackageRelationship>> _relationshipCache;

    /// <summary>
    ///     Set once <see cref="Dispose"/> has been called.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     A single parsed <c>&lt;Relationship&gt;</c> element from a <c>.rels</c> part.
    /// </summary>
    /// <param name="Target">The relationship's raw (not yet resolved) <c>Target</c> attribute value.</param>
    /// <param name="Type">
    ///     The relationship's required <c>Type</c> attribute value (a URI, for example
    ///     <c>"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"</c>)
    ///     - a missing or empty <c>Type</c> attribute is rejected by <see cref="ParseRelationships"/>
    ///     before a <see cref="PackageRelationship"/> is ever constructed, so this field is never
    ///     empty (see <see cref="ResolveRelationshipByType"/>, the Phase 1b consumer of this field).
    /// </param>
    /// <param name="IsExternal">Whether <c>TargetMode="External"</c> was declared.</param>
    private readonly record struct PackageRelationship(string Target, string Type, bool IsExternal);

    /// <summary>
    ///     Opens <paramref name="stream"/> as a read-only <see cref="ZipArchive"/>, wrapping any
    ///     failure to do so (an unreadable or corrupt ZIP, or a stream too short/malformed to be a
    ///     ZIP at all) in <see cref="InvalidDataException"/>.
    /// </summary>
    private static ZipArchive OpenZipArchive(Stream stream)
    {
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            // Already the correct, documented exception type - propagate unchanged.
            throw;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or EndOfStreamException)
        {
            throw new InvalidDataException("The supplied stream is not a valid ZIP/OPC package.", ex);
        }
    }

    /// <summary>
    ///     Builds a case-sensitive lookup of every entry in <paramref name="archive"/> by its full
    ///     path, per the Open Packaging Conventions (OPC, ECMA-376 Part 2) specification's own
    ///     part-name comparison rule: part names are compared ordinally (case-sensitively), so a
    ///     package legitimately may contain both <c>ppt/slides/Slide1.xml</c> and
    ///     <c>ppt/slides/slide1.xml</c> as two distinct parts, and an incorrectly-cased reserved
    ///     name (for example <c>[content_types].xml</c>) must never be accepted as if it were
    ///     <c>[Content_Types].xml</c>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the archive contains two entries whose full names are exactly identical
    ///     (a malformed/corrupt ZIP, since a well-formed ZIP never duplicates an entry name).
    /// </exception>
    private static IReadOnlyDictionary<string, ZipArchiveEntry> BuildEntryLookup(ZipArchive archive)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            try
            {
                entries.Add(entry.FullName, entry);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException(
                    $"The package contains duplicate entries named '{entry.FullName}'.", ex);
            }
        }

        return entries;
    }

    /// <summary>
    ///     Parses <c>[Content_Types].xml</c> and validates the package-level
    ///     <c>_rels/.rels</c> part is present and parseable. Called once from the private
    ///     constructor.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>[Content_Types].xml</c> or <c>_rels/.rels</c> is missing, or either is
    ///     malformed XML.
    /// </exception>
    private void InitializePackage()
    {
        if (!_entriesByPath.TryGetValue(ContentTypesPartName, out var contentTypesEntry))
        {
            throw new InvalidDataException(
                $"The package is missing the required '{ContentTypesPartName}' part.");
        }

        ParseContentTypes(contentTypesEntry);

        if (!_entriesByPath.TryGetValue(PackageRelationshipsPartName, out var packageRelsEntry))
        {
            throw new InvalidDataException(
                $"The package is missing the required '{PackageRelationshipsPartName}' part.");
        }

        // Eagerly parse and cache the package-level relationships (keyed by the empty string,
        // representing the package root) so a malformed package-level rels part fails fast at
        // Open() time rather than lazily on first use.
        _relationshipCache[string.Empty] = ParseRelationships(packageRelsEntry);
    }

    /// <summary>
    ///     Parses <c>[Content_Types].xml</c>'s <c>&lt;Default&gt;</c> and <c>&lt;Override&gt;</c>
    ///     elements into <see cref="_defaultContentTypes"/>/<see cref="_overrideContentTypes"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the part is not well-formed XML, its root element is not the OPC
    ///     content-types namespace's <c>Types</c> element, or any <c>&lt;Default&gt;</c>/
    ///     <c>&lt;Override&gt;</c> child is missing a required attribute (<c>Extension</c>/
    ///     <c>ContentType</c> for <c>Default</c>, <c>PartName</c>/<c>ContentType</c> for
    ///     <c>Override</c>) - malformed input must fail <see cref="Open(Stream)"/> outright rather
    ///     than silently resolving an incomplete content-type map.
    /// </exception>
    private void ParseContentTypes(ZipArchiveEntry entry)
    {
        var root = LoadXmlRoot(entry);
        if (root.Name != ContentTypesNamespace + "Types")
        {
            throw new InvalidDataException(
                $"'{entry.FullName}' root element is '{root.Name}', not the expected '{ContentTypesNamespace + "Types"}'.");
        }

        foreach (var defaultElement in root.Elements(ContentTypesNamespace + "Default"))
        {
            var extension = (string?)defaultElement.Attribute("Extension");
            var contentType = (string?)defaultElement.Attribute("ContentType");
            if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(contentType))
            {
                throw new InvalidDataException(
                    $"'{entry.FullName}' has a <Default> element with a missing or empty 'Extension'/'ContentType' attribute.");
            }

            _defaultContentTypes[extension] = contentType;
        }

        foreach (var overrideElement in root.Elements(ContentTypesNamespace + "Override"))
        {
            var partName = (string?)overrideElement.Attribute("PartName");
            var contentType = (string?)overrideElement.Attribute("ContentType");
            if (string.IsNullOrEmpty(partName) || string.IsNullOrEmpty(contentType))
            {
                throw new InvalidDataException(
                    $"'{entry.FullName}' has an <Override> element with a missing or empty 'PartName'/'ContentType' attribute.");
            }

            _overrideContentTypes[NormalizePartPath(partName)] = contentType;
        }
    }

    /// <summary>
    ///     Parses a <c>.rels</c> part's <c>&lt;Relationship&gt;</c> elements into a lookup from
    ///     relationship ID to its <see cref="PackageRelationship"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the part is not well-formed XML, its root element is not the OPC
    ///     relationships namespace's <c>Relationships</c> element, or any
    ///     <c>&lt;Relationship&gt;</c> child is missing a required attribute (<c>Id</c>,
    ///     <c>Type</c>, or <c>Target</c>) - malformed input must fail resolution outright rather
    ///     than silently skipping the malformed relationship.
    /// </exception>
    private static IReadOnlyDictionary<string, PackageRelationship> ParseRelationships(ZipArchiveEntry entry)
    {
        var root = LoadXmlRoot(entry);
        if (root.Name != RelationshipsNamespace + "Relationships")
        {
            throw new InvalidDataException(
                $"'{entry.FullName}' root element is '{root.Name}', not the expected '{RelationshipsNamespace + "Relationships"}'.");
        }

        var relationships = new Dictionary<string, PackageRelationship>(StringComparer.Ordinal);
        foreach (var relationshipElement in root.Elements(RelationshipsNamespace + "Relationship"))
        {
            var id = (string?)relationshipElement.Attribute("Id");
            var target = (string?)relationshipElement.Attribute("Target");
            var type = (string?)relationshipElement.Attribute("Type");
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(target) || string.IsNullOrEmpty(type))
            {
                throw new InvalidDataException(
                    $"'{entry.FullName}' has a <Relationship> element with a missing or empty 'Id'/'Type'/'Target' attribute.");
            }

            var targetMode = (string?)relationshipElement.Attribute("TargetMode");
            var isExternal = string.Equals(targetMode, "External", StringComparison.OrdinalIgnoreCase);

            relationships[id] = new PackageRelationship(target, type, isExternal);
        }

        return relationships;
    }

    /// <summary>
    ///     Loads and returns <paramref name="entry"/>'s root XML element, wrapping any XML parse
    ///     failure in <see cref="InvalidDataException"/>.
    /// </summary>
    /// <remarks>
    ///     Both <c>[Content_Types].xml</c> and every <c>.rels</c> part are attacker-controlled ZIP
    ///     entries (a malicious package can declare any content for them); parsing them with an
    ///     unbounded <see cref="XmlReader"/> would let a small ZIP containing a highly inflated
    ///     XML part (an "XML bomb"/zip-bomb pattern) exhaust memory during <see cref="Open(Stream)"/>.
    ///     <see cref="MaxPartCharacters"/> bounds the reader's total character count before
    ///     <see cref="XDocument.Load(XmlReader, LoadOptions)"/> ever begins materializing the DOM
    ///     tree, and disabling DTD processing (with no XML resolver) hardens against XML External
    ///     Entity (XXE) injection - mirroring <c>SvgCodec.LoadRootElement</c>'s own established
    ///     hardening pattern for the same class of attacker-controlled-XML risk.
    /// </remarks>
    private static XElement LoadXmlRoot(ZipArchiveEntry entry)
    {
        try
        {
            using var stream = entry.Open();
            var settings = new XmlReaderSettings
            {
                MaxCharactersInDocument = MaxPartCharacters,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var reader = XmlReader.Create(stream, settings);
            var document = XDocument.Load(reader, LoadOptions.None);
            return document.Root ?? throw new InvalidDataException(
                $"'{entry.FullName}' does not contain a root XML element.");
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException($"'{entry.FullName}' is not well-formed XML.", ex);
        }
    }

    /// <summary>Strips a single leading <c>'/'</c> from an OPC part name, if present.</summary>
    private static string NormalizePartPath(string partPath) =>
        partPath.StartsWith('/') ? partPath[1..] : partPath;

    /// <summary>
    ///     Resolves the given OPC part's content type, consulting
    ///     <see cref="_overrideContentTypes"/> (an exact, part-specific match) before falling back
    ///     to <see cref="_defaultContentTypes"/> (a match by file extension).
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
        ArgumentNullException.ThrowIfNull(partPath);

        var normalized = NormalizePartPath(partPath);
        if (!_entriesByPath.ContainsKey(normalized))
        {
            throw new InvalidDataException($"The package does not contain a part named '{partPath}'.");
        }

        if (_overrideContentTypes.TryGetValue(normalized, out var overrideType))
        {
            return overrideType;
        }

        var extension = GetExtension(normalized);
        if (extension is not null && _defaultContentTypes.TryGetValue(extension, out var defaultType))
        {
            return defaultType;
        }

        throw new InvalidDataException($"No content type could be resolved for part '{partPath}'.");
    }

    /// <summary>
    ///     Resolves the target part path of the relationship identified by
    ///     <paramref name="relationshipId"/> within <paramref name="sourcePartPath"/>'s
    ///     relationships, following OPC's relative-target resolution rules (a target is resolved
    ///     relative to the source part's own directory, unless it begins with <c>'/'</c>, in
    ///     which case it is resolved relative to the package root).
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
    ///     (<c>TargetMode="External"</c>, not supported by this phase), or when its resolved
    ///     target would escape the package root (an unbalanced leading <c>"../"</c>).
    /// </exception>
    internal string ResolveRelationship(string sourcePartPath, string relationshipId)
    {
        ArgumentNullException.ThrowIfNull(sourcePartPath);
        ArgumentNullException.ThrowIfNull(relationshipId);

        var normalizedSource = NormalizePartPath(sourcePartPath);
        var relationships = GetRelationships(normalizedSource);

        if (!relationships.TryGetValue(relationshipId, out var relationship))
        {
            throw new InvalidDataException(
                $"Relationship '{relationshipId}' was not found for part '{sourcePartPath}'.");
        }

        if (relationship.IsExternal)
        {
            throw new InvalidDataException(
                $"Relationship '{relationshipId}' for part '{sourcePartPath}' targets an external resource, which is not supported.");
        }

        return ResolveRelativeTarget(normalizedSource, relationship.Target);
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
        ArgumentNullException.ThrowIfNull(sourcePartPath);
        ArgumentNullException.ThrowIfNull(relationshipId);

        var normalizedSource = NormalizePartPath(sourcePartPath);
        var relationships = GetRelationships(normalizedSource);

        if (!relationships.TryGetValue(relationshipId, out var relationship))
        {
            throw new InvalidDataException(
                $"Relationship '{relationshipId}' was not found for part '{sourcePartPath}'.");
        }

        return relationship.Type;
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
        ArgumentNullException.ThrowIfNull(sourcePartPath);
        ArgumentNullException.ThrowIfNull(relationshipTypeSuffix);

        var normalizedSource = NormalizePartPath(sourcePartPath);
        var relationships = GetRelationships(normalizedSource);

        foreach (var relationship in relationships.Values)
        {
            if (!relationship.IsExternal &&
                relationship.Type.EndsWith(relationshipTypeSuffix, StringComparison.Ordinal))
            {
                return ResolveRelativeTarget(normalizedSource, relationship.Target);
            }
        }

        throw new InvalidDataException(
            $"No relationship of part '{sourcePartPath}' has a Type ending with '{relationshipTypeSuffix}'.");
    }

    /// <summary>
    ///     Non-throwing sibling of <see cref="ResolveRelationshipByType"/>: resolves the target
    ///     part path of the single relationship of <paramref name="sourcePartPath"/> whose
    ///     <c>Type</c> attribute ends with <paramref name="relationshipTypeSuffix"/>, returning
    ///     <see langword="null"/> instead of throwing when no such relationship exists. Used for
    ///     navigations to an optional, auxiliary part (for example <c>ppt/tableStyles.xml</c>, via
    ///     <see cref="TryResolveTableStyle"/>) whose absence must degrade gracefully rather than
    ///     fail the whole parse closed, unlike <see cref="ResolveRelationshipByType"/>'s own
    ///     fail-closed behavior for a required navigation.
    /// </summary>
    /// <param name="sourcePartPath">The source part's path, or empty string for the package root.</param>
    /// <param name="relationshipTypeSuffix">
    ///     The relationship Type URI suffix to match, compared with an ordinal, case-sensitive
    ///     <see cref="string.EndsWith(string, StringComparison)"/> - see
    ///     <see cref="ResolveRelationshipByType"/>'s matching parameter.
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
        ArgumentNullException.ThrowIfNull(sourcePartPath);
        ArgumentNullException.ThrowIfNull(relationshipTypeSuffix);

        var normalizedSource = NormalizePartPath(sourcePartPath);
        var relationships = GetRelationships(normalizedSource);

        foreach (var relationship in relationships.Values)
        {
            if (!relationship.IsExternal &&
                relationship.Type.EndsWith(relationshipTypeSuffix, StringComparison.Ordinal))
            {
                return ResolveRelativeTarget(normalizedSource, relationship.Target);
            }
        }

        return null;
    }

    /// <summary>
    ///     Loads and returns the root XML element of the part at <paramref name="partPath"/>,
    ///     wrapping a missing part or malformed XML in <see cref="InvalidDataException"/>. Shared
    ///     by every Phase 1b presentation-specific parser (Presentation/Theme/Masters/Layouts/
    ///     Slides) so each implements only its own element-shape parsing, not part lookup/XML-load
    ///     error handling.
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
        ArgumentNullException.ThrowIfNull(partPath);

        var normalized = NormalizePartPath(partPath);
        if (!_entriesByPath.TryGetValue(normalized, out var entry))
        {
            throw new InvalidDataException($"The package does not contain a part named '{partPath}'.");
        }

        return LoadXmlRoot(entry);
    }

    /// <summary>
    ///     Reads and returns the raw, undecoded bytes of the part at <paramref name="partPath"/> -
    ///     the Phase 1e counterpart to <see cref="LoadPartXmlRoot"/> for a part whose content is
    ///     not XML at all (a media part, for example <c>ppt/media/image1.png</c>, referenced by a
    ///     <c>&lt;p:pic&gt;</c>'s <c>&lt;a:blip r:embed="..."/&gt;</c> - see
    ///     <see cref="ResolvePictureSurface"/>, this method's sole Phase 1e caller).
    /// </summary>
    /// <param name="partPath">The part's path within the package, with or without a leading slash.</param>
    /// <returns>The part's full, raw byte content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="partPath"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the package does not contain a part named <paramref name="partPath"/>.
    /// </exception>
    /// <remarks>
    ///     Unlike <see cref="LoadXmlRoot"/>, no <see cref="MaxPartCharacters"/>-style bound is
    ///     applied here: a media part's raw byte size is already bounded by this package's own
    ///     overall package-size safeguard (<see cref="MaxPackageBytes"/>, enforced once for the
    ///     whole ZIP at <see cref="Open(Stream)"/> time), so no additional, narrower cap is
    ///     introduced for an individual part - keeping this method a thin, direct read with no
    ///     new risk surface beyond what <see cref="Open(Stream)"/> already bounds.
    /// </remarks>
    internal byte[] GetPartBytes(string partPath)
    {
        ArgumentNullException.ThrowIfNull(partPath);

        var normalized = NormalizePartPath(partPath);
        if (!_entriesByPath.TryGetValue(normalized, out var entry))
        {
            throw new InvalidDataException($"The package does not contain a part named '{partPath}'.");
        }

        using var entryStream = entry.Open();
        using var buffer = new MemoryStream();
        entryStream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>
    ///     Returns the parsed relationships for <paramref name="normalizedSourcePartPath"/>,
    ///     parsing and caching them (in <see cref="_relationshipCache"/>) on first access. A
    ///     part with no <c>.rels</c> file of its own simply has no relationships - that is not an
    ///     error, since most parts are never a relationship source.
    /// </summary>
    private IReadOnlyDictionary<string, PackageRelationship> GetRelationships(string normalizedSourcePartPath)
    {
        if (_relationshipCache.TryGetValue(normalizedSourcePartPath, out var cached))
        {
            return cached;
        }

        var relsPartPath = GetRelationshipsPartPath(normalizedSourcePartPath);
        var relationships = _entriesByPath.TryGetValue(relsPartPath, out var relsEntry)
            ? ParseRelationships(relsEntry)
            : new Dictionary<string, PackageRelationship>(StringComparer.Ordinal);

        _relationshipCache[normalizedSourcePartPath] = relationships;
        return relationships;
    }

    /// <summary>
    ///     Computes the <c>.rels</c> part path that would hold <paramref name="normalizedPartPath"/>'s
    ///     own relationships, per the OPC convention of a sibling <c>_rels</c> subfolder (for
    ///     example <c>"ppt/slides/slide1.xml"</c> -&gt; <c>"ppt/slides/_rels/slide1.xml.rels"</c>).
    ///     The empty string (the package root) maps to <see cref="PackageRelationshipsPartName"/>.
    /// </summary>
    private static string GetRelationshipsPartPath(string normalizedPartPath)
    {
        if (normalizedPartPath.Length == 0)
        {
            return PackageRelationshipsPartName;
        }

        var lastSlash = normalizedPartPath.LastIndexOf('/');
        var directory = lastSlash < 0 ? string.Empty : normalizedPartPath[..lastSlash];
        var fileName = lastSlash < 0 ? normalizedPartPath : normalizedPartPath[(lastSlash + 1)..];

        return directory.Length == 0
            ? $"_rels/{fileName}.rels"
            : $"{directory}/_rels/{fileName}.rels";
    }

    /// <summary>
    ///     Resolves <paramref name="target"/> (a relationship's raw <c>Target</c> attribute)
    ///     relative to <paramref name="normalizedBasePartPath"/>'s own directory, per OPC's
    ///     relative-reference resolution rules: a target beginning with <c>'/'</c> is resolved
    ///     relative to the package root instead, and any <c>"."</c>/<c>".."</c> segment in the
    ///     combined path is normalized away (mirroring familiar filesystem-path traversal).
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when normalization would require popping past the package root (more <c>".."</c>
    ///     segments than preceding directory segments).
    /// </exception>
    private static string ResolveRelativeTarget(string normalizedBasePartPath, string target)
    {
        if (target.StartsWith('/'))
        {
            return NormalizeSegments(target[1..]);
        }

        var lastSlash = normalizedBasePartPath.LastIndexOf('/');
        var baseDirectory = lastSlash < 0 ? string.Empty : normalizedBasePartPath[..lastSlash];

        var combined = baseDirectory.Length == 0 ? target : $"{baseDirectory}/{target}";
        return NormalizeSegments(combined);
    }

    /// <summary>
    ///     Splits <paramref name="path"/> on <c>'/'</c> and resolves <c>"."</c> (current
    ///     directory, dropped) and <c>".."</c> (parent directory, pops the previous segment)
    ///     segments, returning the normalized, re-joined path.
    /// </summary>
    private static string NormalizeSegments(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            switch (segment)
            {
                case ".":
                    continue;
                case "..":
                    if (stack.Count == 0)
                    {
                        throw new InvalidDataException(
                            $"Relationship target '{path}' escapes the package root.");
                    }

                    stack.RemoveAt(stack.Count - 1);
                    continue;
                default:
                    stack.Add(segment);
                    continue;
            }
        }

        return string.Join('/', stack);
    }

    /// <summary>Returns <paramref name="normalizedPartPath"/>'s extension with no leading dot, or null if it has none.</summary>
    private static string? GetExtension(string normalizedPartPath)
    {
        var lastDot = normalizedPartPath.LastIndexOf('.');
        var lastSlash = normalizedPartPath.LastIndexOf('/');
        return lastDot > lastSlash && lastDot < normalizedPartPath.Length - 1
            ? normalizedPartPath[(lastDot + 1)..]
            : null;
    }
}
