using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore pptx ooxml

/// <summary>
///     Implements the <see cref="PptxDocument"/> OOXML (Office Open XML) package layer: opening
///     the <c>.pptx</c> file as a ZIP archive, resolving <c>[Content_Types].xml</c>, and
///     resolving package- and part-level relationships. This is the sole functionality
///     implemented as of Phase 1a - no presentation-specific content (<c>ppt/presentation.xml</c>,
///     slides, slide layouts/masters) is parsed by this or any other part of this class yet.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>The well-known part name of the package's content-types stream.</summary>
    private const string ContentTypesPartName = "[Content_Types].xml";

    /// <summary>The well-known part name of the package-level relationships part.</summary>
    private const string PackageRelationshipsPartName = "_rels/.rels";

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

    /// <summary>Every part name present in the package, for existence checks, keyed case-insensitively.</summary>
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
    /// <param name="IsExternal">Whether <c>TargetMode="External"</c> was declared.</param>
    private readonly record struct PackageRelationship(string Target, bool IsExternal);

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

    /// <summary>Builds a case-insensitive lookup of every entry in <paramref name="archive"/> by its full path.</summary>
    private static IReadOnlyDictionary<string, ZipArchiveEntry> BuildEntryLookup(ZipArchive archive)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            entries[entry.FullName] = entry;
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
    /// <exception cref="InvalidDataException">Thrown when the part is not well-formed XML.</exception>
    private void ParseContentTypes(ZipArchiveEntry entry)
    {
        var root = LoadXmlRoot(entry);

        foreach (var defaultElement in root.Elements(ContentTypesNamespace + "Default"))
        {
            var extension = (string?)defaultElement.Attribute("Extension");
            var contentType = (string?)defaultElement.Attribute("ContentType");
            if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(contentType))
            {
                continue;
            }

            _defaultContentTypes[extension] = contentType;
        }

        foreach (var overrideElement in root.Elements(ContentTypesNamespace + "Override"))
        {
            var partName = (string?)overrideElement.Attribute("PartName");
            var contentType = (string?)overrideElement.Attribute("ContentType");
            if (string.IsNullOrEmpty(partName) || string.IsNullOrEmpty(contentType))
            {
                continue;
            }

            _overrideContentTypes[NormalizePartPath(partName)] = contentType;
        }
    }

    /// <summary>
    ///     Parses a <c>.rels</c> part's <c>&lt;Relationship&gt;</c> elements into a lookup from
    ///     relationship ID to its <see cref="PackageRelationship"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the part is not well-formed XML.</exception>
    private static IReadOnlyDictionary<string, PackageRelationship> ParseRelationships(ZipArchiveEntry entry)
    {
        var root = LoadXmlRoot(entry);

        var relationships = new Dictionary<string, PackageRelationship>(StringComparer.Ordinal);
        foreach (var relationshipElement in root.Elements(RelationshipsNamespace + "Relationship"))
        {
            var id = (string?)relationshipElement.Attribute("Id");
            var target = (string?)relationshipElement.Attribute("Target");
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(target))
            {
                continue;
            }

            var targetMode = (string?)relationshipElement.Attribute("TargetMode");
            var isExternal = string.Equals(targetMode, "External", StringComparison.OrdinalIgnoreCase);

            relationships[id] = new PackageRelationship(target, isExternal);
        }

        return relationships;
    }

    /// <summary>
    ///     Loads and returns <paramref name="entry"/>'s root XML element, wrapping any XML parse
    ///     failure in <see cref="InvalidDataException"/>.
    /// </summary>
    private static XElement LoadXmlRoot(ZipArchiveEntry entry)
    {
        try
        {
            using var stream = entry.Open();
            var document = XDocument.Load(stream, LoadOptions.None);
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
