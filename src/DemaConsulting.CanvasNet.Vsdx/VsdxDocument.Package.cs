using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx ooxml visio navigations ordinally

/// <summary>
///     Implements the <see cref="VsdxDocument"/> OPC (Open Packaging Conventions) package layer:
///     opening the <c>.vsdx</c> file as a ZIP archive, resolving <c>[Content_Types].xml</c>, and
///     resolving package- and part-level relationships. This file implements only the
///     package-layer (OPC/ZIP/content-types/relationships) mechanics - <c>VsdxDocument.Pages.cs</c>
///     layers page-index parsing on top of it; see <see cref="VsdxDocument"/>'s own remarks for
///     this class's full, current scope.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>The well-known part name of the package's content-types stream.</summary>
    private const string ContentTypesPartName = "[Content_Types].xml";

    /// <summary>The well-known part name of the package-level relationships part.</summary>
    private const string PackageRelationshipsPartName = "_rels/.rels";

    /// <summary>
    ///     The maximum number of characters <see cref="LoadXmlRoot"/> allows an
    ///     <see cref="XmlReader"/> to read while parsing <c>[Content_Types].xml</c>, any
    ///     <c>.rels</c> part, <c>visio/document.xml</c>, or <c>visio/pages/pages.xml</c>,
    ///     bounding the worst-case parse-time memory/CPU cost of an attacker-controlled, highly
    ///     inflated ZIP entry (an "XML bomb") before it is ever materialized into an in-memory
    ///     DOM. 2,000,000 characters mirrors <c>PptxDocument.MaxPartCharacters</c>'s own
    ///     "generous but bounded" sizing rationale for the equivalent attacker-controlled-XML
    ///     risk.
    /// </summary>
    private const int MaxPartCharacters = 2_000_000;

    /// <summary>The XML namespace used by <c>[Content_Types].xml</c>.</summary>
    private static readonly XNamespace ContentTypesNamespace =
        "http://schemas.openxmlformats.org/package/2006/content-types";

    /// <summary>The XML namespace used by every <c>.rels</c> relationships part.</summary>
    private static readonly XNamespace RelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>
    ///     The <c>[Content_Types].xml</c> default extension-to-content-type mappings (for
    ///     example <c>"xml" -&gt; "application/xml"</c>), keyed by extension without a leading
    ///     dot, case-insensitively.
    /// </summary>
    private readonly Dictionary<string, string> _defaultContentTypes;

    /// <summary>
    ///     The <c>[Content_Types].xml</c> part-specific content-type overrides, keyed by the
    ///     normalized part path (no leading slash), which take precedence over
    ///     <see cref="_defaultContentTypes"/> for the same part.
    /// </summary>
    private readonly Dictionary<string, string> _overrideContentTypes;

    /// <summary>
    ///     Caches each part's parsed relationships the first time <see cref="GetRelationships"/>
    ///     resolves against it, keyed by the normalized source part path (the empty string
    ///     represents the package root, whose relationships live in
    ///     <see cref="PackageRelationshipsPartName"/>).
    /// </summary>
    private readonly Dictionary<string, IReadOnlyDictionary<string, PackageRelationship>> _relationshipCache;

    /// <summary>
    ///     A single parsed <c>&lt;Relationship&gt;</c> element from a <c>.rels</c> part.
    /// </summary>
    /// <param name="Target">The relationship's raw (not yet resolved) <c>Target</c> attribute value.</param>
    /// <param name="Type">
    ///     The relationship's required <c>Type</c> attribute value (a URI) - a missing or empty
    ///     <c>Type</c> attribute is rejected by <see cref="ParseRelationships"/> before a
    ///     <see cref="PackageRelationship"/> is ever constructed, so this field is never empty
    ///     (see <see cref="ResolveRelationshipByType"/>, the sole consumer of this field).
    /// </param>
    /// <param name="IsExternal">Whether <c>TargetMode="External"</c> was declared.</param>
    private readonly record struct PackageRelationship(string Target, string Type, bool IsExternal);

    /// <summary>
    ///     Opens <paramref name="stream"/> as a read-only <see cref="ZipArchive"/>, wrapping any
    ///     failure to do so (an unreadable or corrupt ZIP, or a stream too short/malformed to be a
    ///     ZIP at all) in <see cref="InvalidDataException"/>.
    /// </summary>
    /// <param name="stream">The stream to open as a ZIP archive.</param>
    /// <returns>The opened, read-only <see cref="ZipArchive"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="stream"/> is not a readable ZIP archive.
    /// </exception>
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
    ///     part-name comparison rule: part names are compared ordinally (case-sensitively).
    /// </summary>
    /// <param name="archive">The archive whose entries are indexed.</param>
    /// <returns>A case-sensitive lookup from full entry path to <see cref="ZipArchiveEntry"/>.</returns>
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
    /// <param name="entry">The <c>[Content_Types].xml</c> ZIP entry.</param>
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
    /// <param name="entry">The <c>.rels</c> ZIP entry to parse.</param>
    /// <returns>A lookup from relationship <c>Id</c> to its <see cref="PackageRelationship"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the part is not well-formed XML, its root element is not the OPC
    ///     relationships namespace's <c>Relationships</c> element, any
    ///     <c>&lt;Relationship&gt;</c> child is missing a required attribute (<c>Id</c>,
    ///     <c>Type</c>, or <c>Target</c>), or two <c>&lt;Relationship&gt;</c> children declare the
    ///     same <c>Id</c> - malformed input must fail resolution outright rather than silently
    ///     skipping the malformed relationship or overwriting the first relationship with the
    ///     duplicate.
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

            if (!relationships.TryAdd(id, new PackageRelationship(target, type, isExternal)))
            {
                throw new InvalidDataException(
                    $"'{entry.FullName}' has a <Relationship> element with a duplicate 'Id' attribute value '{id}'.");
            }
        }

        return relationships;
    }

    /// <summary>
    ///     Loads and returns <paramref name="entry"/>'s root XML element, wrapping any XML parse
    ///     failure in <see cref="InvalidDataException"/>.
    /// </summary>
    /// <param name="entry">The ZIP entry to load as XML.</param>
    /// <returns>The entry's root <see cref="XElement"/>.</returns>
    /// <remarks>
    ///     Every XML part read through this method is an attacker-controlled ZIP entry (a
    ///     malicious package can declare any content for it); parsing it with an unbounded
    ///     <see cref="XmlReader"/> would let a small ZIP containing a highly inflated XML part
    ///     (an "XML bomb"/zip-bomb pattern) exhaust memory during <see cref="Open(Stream)"/>.
    ///     <see cref="MaxPartCharacters"/> bounds the reader's total character count before
    ///     <see cref="XDocument.Load(XmlReader, LoadOptions)"/> ever begins materializing the DOM
    ///     tree, and disabling DTD processing (with no XML resolver) hardens against XML External
    ///     Entity (XXE) injection - mirroring <c>PptxDocument.LoadXmlRoot</c>'s own established
    ///     hardening pattern for the same class of attacker-controlled-XML risk.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="entry"/> is not well-formed XML, or has no root element.
    /// </exception>
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
    /// <param name="partPath">The part path to normalize.</param>
    /// <returns><paramref name="partPath"/> with any single leading <c>'/'</c> removed.</returns>
    private static string NormalizePartPath(string partPath) =>
        partPath.StartsWith('/') ? partPath[1..] : partPath;

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
        ArgumentNullException.ThrowIfNull(sourcePartPath);
        ArgumentNullException.ThrowIfNull(relationshipTypeSuffix);

        if (TryResolveRelationshipByType(sourcePartPath, relationshipTypeSuffix, out var resolvedPartPath))
        {
            return resolvedPartPath;
        }

        throw new InvalidDataException(
            $"No relationship of part '{sourcePartPath}' has a Type ending with '{relationshipTypeSuffix}'.");
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
        out string resolvedPartPath)
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
                resolvedPartPath = ResolveRelativeTarget(normalizedSource, relationship.Target);
                return true;
            }
        }

        resolvedPartPath = null!;
        return false;
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
        out string resolvedPartPath)
    {
        var normalizedSource = NormalizePartPath(sourcePartPath);
        var relationships = GetRelationships(normalizedSource);

        if (relationships.TryGetValue(relationshipId, out var relationship) && !relationship.IsExternal)
        {
            resolvedPartPath = ResolveRelativeTarget(normalizedSource, relationship.Target);
            return true;
        }

        resolvedPartPath = null!;
        return false;
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
        ArgumentNullException.ThrowIfNull(partPath);

        var normalized = NormalizePartPath(partPath);
        if (!_entriesByPath.TryGetValue(normalized, out var entry))
        {
            throw new InvalidDataException($"The package does not contain a part named '{partPath}'.");
        }

        return LoadXmlRoot(entry);
    }

    /// <summary>
    ///     Returns the parsed relationships for <paramref name="normalizedSourcePartPath"/>,
    ///     parsing and caching them (in <see cref="_relationshipCache"/>) on first access. A
    ///     part with no <c>.rels</c> file of its own simply has no relationships - that is not an
    ///     error, since most parts are never a relationship source.
    /// </summary>
    /// <param name="normalizedSourcePartPath">
    ///     The normalized (no leading slash) source part path, or the empty string for the
    ///     package root.
    /// </param>
    /// <returns>A lookup from relationship <c>Id</c> to its <see cref="PackageRelationship"/>.</returns>
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
    ///     example <c>"visio/document.xml"</c> -&gt; <c>"visio/_rels/document.xml.rels"</c>). The
    ///     empty string (the package root) maps to <see cref="PackageRelationshipsPartName"/>.
    /// </summary>
    /// <param name="normalizedPartPath">The normalized (no leading slash) part path.</param>
    /// <returns>The corresponding <c>.rels</c> part path.</returns>
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
    ///     Resolves <paramref name="target"/> relative to <paramref name="normalizedBasePartPath"/>'s
    ///     own directory, following OPC's relative-target resolution rules (a target is resolved
    ///     relative to the source part's own directory, unless it begins with <c>'/'</c>, in
    ///     which case it is resolved relative to the package root).
    /// </summary>
    /// <param name="normalizedBasePartPath">The normalized (no leading slash) source part path.</param>
    /// <param name="target">The relationship's raw <c>Target</c> attribute value.</param>
    /// <returns>The resolved target part path, with no leading slash.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the resolved target would escape the package root (more <c>".."</c>
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
    /// <param name="path">The combined, not-yet-normalized path.</param>
    /// <returns>The normalized, re-joined path.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="path"/> contains a <c>".."</c> segment with no preceding
    ///     directory segment to pop (escaping the package root).
    /// </exception>
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
}
