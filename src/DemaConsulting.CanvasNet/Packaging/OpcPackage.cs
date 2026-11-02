using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Packaging;

// cspell:ignore ordinally navigations

/// <summary>
///     A read-only, size-bounded view of an Open Packaging Conventions (OPC, ECMA-376 Part 2) package,
///     the ZIP-based container used by <c>.pptx</c>, <c>.vsdx</c>, and other Office Open XML formats.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Open"/> buffers the whole input stream into memory (so the caller's stream need
///         not stay open or be seekable), opens it as a ZIP archive, and parses the package's
///         <c>[Content_Types].xml</c> and package-level <c>_rels/.rels</c> parts. Afterwards the
///         instance resolves a part's content type (<see cref="ResolvePart"/>), follows relationships
///         between parts (<see cref="ResolveRelationship"/>, <see cref="ResolveRelationshipByType"/>,
///         <see cref="TryResolveRelationshipByType"/>, <see cref="TryResolveRelationshipById"/>,
///         <see cref="GetRelationshipType"/>), and reads a part as XML
///         (<see cref="LoadPartXmlRoot"/>) or as raw bytes (<see cref="GetPartBytes"/>).
///     </para>
///     <para>
///         <b>Hardening.</b> The package is treated as untrusted. The input is rejected once it
///         exceeds <see cref="MaxPackageBytes"/> bytes; a single part is rejected once its
///         decompressed content exceeds <see cref="MaxPartBytes"/> bytes (the size is counted while
///         copying, never taken from the ZIP entry's declared length); an XML part is parsed with
///         <see cref="SafeXml"/> (no DTD, no external resolution) and rejected once it exceeds
///         <see cref="MaxXmlPartCharacters"/> characters; two ZIP entries with an identical name are
///         rejected; and a relationship target that would climb above the package root is rejected.
///         Part names are compared ordinally, so parts whose names differ only by case are distinct.
///         Nothing is ever extracted to the file system.
///     </para>
///     <para>
///         <b>Errors.</b> A malformed or oversized package, a missing part, or an unknown relationship
///         throws <see cref="OpcFormatException"/>. A ZIP entry whose compressed data is corrupt
///         surfaces as the <see cref="InvalidDataException"/> raised by the ZIP layer when the part is
///         read.
///     </para>
///     <para>
///         <b>Thread safety</b>: an instance is <em>not</em> thread-safe, because it caches parsed
///         relationship parts and reads from one shared ZIP archive. Use one instance per thread, or
///         synchronize all calls to a shared instance externally. The static members are thread-safe.
///     </para>
/// </remarks>
public sealed class OpcPackage : IDisposable
{
    /// <summary>
    ///     The largest number of bytes <see cref="Open"/> buffers from the supplied stream before
    ///     rejecting it: 268,435,456 bytes (256 MiB), large enough for any realistic Office package
    ///     (including one with embedded media) while keeping the worst-case memory use for an
    ///     attacker-supplied, or non-terminating, stream bounded.
    /// </summary>
    public const long MaxPackageBytes = 268_435_456L;

    /// <summary>
    ///     The largest number of decompressed bytes <see cref="GetPartBytes"/> returns for one part:
    ///     the same value as <see cref="MaxPackageBytes"/>, since one part cannot legitimately exceed
    ///     the whole package's own bound. It defends against a part that is a "zip bomb" (a few
    ///     kilobytes that inflate to gigabytes).
    /// </summary>
    public const long MaxPartBytes = MaxPackageBytes;

    /// <summary>
    ///     The largest number of characters any XML part may contain when read by
    ///     <see cref="Open"/> (for <c>[Content_Types].xml</c> and <c>.rels</c> parts) or
    ///     <see cref="LoadPartXmlRoot"/>: 2,000,000, far above what a real part needs while keeping
    ///     the worst-case memory and time of an inflated "XML bomb" part small.
    /// </summary>
    public const int MaxXmlPartCharacters = 2_000_000;

    /// <summary>The well-known part name of the package's content-types stream.</summary>
    private const string ContentTypesPartName = "[Content_Types].xml";

    /// <summary>The well-known part name of the package-level relationships part.</summary>
    private const string PackageRelationshipsPartName = "_rels/.rels";

    /// <summary>The size, in bytes, of the chunks the bounded copy reads.</summary>
    private const int CopyChunkBytes = 81920;

    /// <summary>The XML namespace used by <c>[Content_Types].xml</c>.</summary>
    private static readonly XNamespace ContentTypesNamespace =
        "http://schemas.openxmlformats.org/package/2006/content-types";

    /// <summary>The XML namespace used by every <c>.rels</c> relationships part.</summary>
    private static readonly XNamespace RelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>The non-writable, in-memory stream over the buffered package bytes that backs <see cref="_archive"/>.</summary>
    private readonly MemoryStream _stream;

    /// <summary>The opened, read-only ZIP archive view of the package.</summary>
    private readonly ZipArchive _archive;

    /// <summary>Every part present in the package, keyed by exact (ordinal, case-sensitive) name.</summary>
    private readonly Dictionary<string, ZipArchiveEntry> _entriesByPath;

    /// <summary>
    ///     The <c>[Content_Types].xml</c> default extension-to-content-type mappings, keyed by
    ///     extension without a leading dot, case-insensitively.
    /// </summary>
    private readonly Dictionary<string, string> _defaultContentTypes =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The <c>[Content_Types].xml</c> part-specific overrides, keyed by normalized part path,
    ///     which take precedence over <see cref="_defaultContentTypes"/>.
    /// </summary>
    private readonly Dictionary<string, string> _overrideContentTypes =
        new(StringComparer.Ordinal);

    /// <summary>
    ///     Each source part's parsed relationships, cached on first use and keyed by normalized source
    ///     part path (the empty string is the package root, whose relationships live in
    ///     <see cref="PackageRelationshipsPartName"/>).
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, PackageRelationship>> _relationshipCache =
        new(StringComparer.Ordinal);

    /// <summary>Set once <see cref="Dispose"/> has been called.</summary>
    private bool _disposed;

    /// <summary>A single parsed <c>&lt;Relationship&gt;</c> element.</summary>
    /// <param name="Target">The raw (not yet resolved) <c>Target</c> attribute value.</param>
    /// <param name="Type">The <c>Type</c> attribute value (never empty).</param>
    /// <param name="IsExternal">Whether <c>TargetMode="External"</c> was declared.</param>
    private readonly record struct PackageRelationship(string Target, string Type, bool IsExternal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="OpcPackage"/> class over fully buffered
    ///     package bytes.
    /// </summary>
    /// <param name="stream">The non-writable stream over the buffered bytes. Ownership transfers here.</param>
    /// <exception cref="OpcFormatException">The bytes are not a valid OPC package.</exception>
    private OpcPackage(MemoryStream stream)
    {
        _stream = stream;
        try
        {
            _archive = OpenZipArchive(stream);
            _entriesByPath = BuildEntryLookup(_archive);
            InitializePackage();
        }
        catch
        {
            _archive?.Dispose();
            _stream.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Opens an OPC package from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">
    ///     The readable stream to read the package from. The whole stream is read into memory; this
    ///     method never disposes or closes it.
    /// </param>
    /// <returns>The opened package; the caller disposes it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stream"/> is not readable.</exception>
    /// <exception cref="OpcFormatException">
    ///     Thrown when the input is larger than <see cref="MaxPackageBytes"/> bytes, is not a readable
    ///     ZIP archive, contains two entries with an identical name, lacks <c>[Content_Types].xml</c>
    ///     or <c>_rels/.rels</c>, or either of those parts is malformed (not well-formed XML, wrong
    ///     root element, missing or empty required attribute, duplicate relationship identifier).
    /// </exception>
    public static OpcPackage Open(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        var buffered = new MemoryStream();
        CopyBounded(
            stream,
            buffered,
            MaxPackageBytes,
            $"The package stream exceeds the maximum supported size of {MaxPackageBytes} bytes.");

        // Back the archive with the buffer already built (no second copy of up to 256 MiB). The
        // MemoryStream was created with its default constructor, so its buffer is always exposed.
        if (!buffered.TryGetBuffer(out var segment) || segment.Array is null)
        {
            throw new InvalidOperationException("The package buffer is not accessible.");
        }

        return new OpcPackage(new MemoryStream(segment.Array, 0, segment.Count, writable: false));
    }

    /// <summary>
    ///     Strips a single leading <c>'/'</c> from an OPC part name, if present, giving the form the
    ///     package indexes parts by (no leading slash).
    /// </summary>
    /// <param name="partPath">The part name, with or without a leading slash.</param>
    /// <returns><paramref name="partPath"/> with any single leading <c>'/'</c> removed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="partPath"/> is null.</exception>
    public static string NormalizePartPath(string partPath)
    {
        ArgumentNullException.ThrowIfNull(partPath);
        return partPath.StartsWith('/') ? partPath[1..] : partPath;
    }

    /// <summary>Returns whether the package contains a part named <paramref name="partPath"/>.</summary>
    /// <param name="partPath">The part's path (for example <c>"ppt/presentation.xml"</c>), with or without a leading slash.</param>
    /// <returns><see langword="true"/> when an entry with exactly that (case-sensitive) name exists.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="partPath"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this package has been disposed.</exception>
    public bool ContainsPart(string partPath)
    {
        ArgumentNullException.ThrowIfNull(partPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _entriesByPath.ContainsKey(NormalizePartPath(partPath));
    }

    /// <summary>
    ///     Resolves the content type of the part named <paramref name="partPath"/>, consulting the
    ///     part-specific <c>&lt;Override&gt;</c> entries of <c>[Content_Types].xml</c> before the
    ///     extension-based <c>&lt;Default&gt;</c> entries.
    /// </summary>
    /// <param name="partPath">The part's path, with or without a leading slash.</param>
    /// <returns>The content type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="partPath"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this package has been disposed.</exception>
    /// <exception cref="OpcFormatException">
    ///     Thrown when the package has no such part, or no override and no default mapping for its
    ///     extension supplies a content type.
    /// </exception>
    public string ResolvePart(string partPath)
    {
        ArgumentNullException.ThrowIfNull(partPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var normalized = NormalizePartPath(partPath);
        if (!_entriesByPath.ContainsKey(normalized))
        {
            throw new OpcFormatException($"The package does not contain a part named '{partPath}'.");
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

        throw new OpcFormatException($"No content type could be resolved for part '{partPath}'.");
    }

    /// <summary>
    ///     Resolves the target part path of the relationship identified by
    ///     <paramref name="relationshipId"/> within <paramref name="sourcePartPath"/>'s relationships,
    ///     following OPC's relative-target rules: a target is resolved against the source part's own
    ///     directory unless it begins with <c>'/'</c>, in which case it is resolved against the package
    ///     root; <c>"."</c> and <c>".."</c> segments are normalized away.
    /// </summary>
    /// <param name="sourcePartPath">
    ///     The source part's path, or the empty string for a package-level relationship (from
    ///     <c>_rels/.rels</c>).
    /// </param>
    /// <param name="relationshipId">The relationship's <c>Id</c> value (for example <c>"rId1"</c>).</param>
    /// <returns>The resolved target part path, with no leading slash.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="sourcePartPath"/> or <paramref name="relationshipId"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this package has been disposed.</exception>
    /// <exception cref="OpcFormatException">
    ///     Thrown when the source part has no such relationship, the relationship targets an external
    ///     resource (not supported), its target escapes the package root, or the source part's
    ///     <c>.rels</c> part is malformed.
    /// </exception>
    public string ResolveRelationship(string sourcePartPath, string relationshipId)
    {
        ArgumentNullException.ThrowIfNull(sourcePartPath);
        ArgumentNullException.ThrowIfNull(relationshipId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var normalizedSource = NormalizePartPath(sourcePartPath);
        var relationships = GetRelationships(normalizedSource);

        if (!relationships.TryGetValue(relationshipId, out var relationship))
        {
            throw new OpcFormatException(
                $"Relationship '{relationshipId}' was not found for part '{sourcePartPath}'.");
        }

        if (relationship.IsExternal)
        {
            throw new OpcFormatException(
                $"Relationship '{relationshipId}' for part '{sourcePartPath}' targets an external resource, which is not supported.");
        }

        return ResolveRelativeTarget(normalizedSource, relationship.Target);
    }

    /// <summary>
    ///     Returns the <c>Type</c> attribute of the relationship identified by
    ///     <paramref name="relationshipId"/> within <paramref name="sourcePartPath"/>'s relationships,
    ///     for callers that must confirm a relationship points at the expected kind of part before
    ///     trusting its target.
    /// </summary>
    /// <param name="sourcePartPath">The source part's path, or the empty string for the package root.</param>
    /// <param name="relationshipId">The relationship's <c>Id</c> value.</param>
    /// <returns>The relationship's <c>Type</c> value (never null or empty).</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="sourcePartPath"/> or <paramref name="relationshipId"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this package has been disposed.</exception>
    /// <exception cref="OpcFormatException">
    ///     Thrown when the source part has no such relationship or its <c>.rels</c> part is malformed.
    /// </exception>
    public string GetRelationshipType(string sourcePartPath, string relationshipId)
    {
        ArgumentNullException.ThrowIfNull(sourcePartPath);
        ArgumentNullException.ThrowIfNull(relationshipId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var relationships = GetRelationships(NormalizePartPath(sourcePartPath));
        if (!relationships.TryGetValue(relationshipId, out var relationship))
        {
            throw new OpcFormatException(
                $"Relationship '{relationshipId}' was not found for part '{sourcePartPath}'.");
        }

        return relationship.Type;
    }

    /// <summary>
    ///     Resolves the target part path of a non-external relationship of
    ///     <paramref name="sourcePartPath"/> whose <c>Type</c> ends with
    ///     <paramref name="relationshipTypeSuffix"/>, for navigations that carry no explicit
    ///     relationship identifier (for example the package root's relationship to its main
    ///     document). When several relationships match, which one is returned is unspecified, so
    ///     use it for navigations that are unique by type.
    /// </summary>
    /// <param name="sourcePartPath">The source part's path, or the empty string for the package root.</param>
    /// <param name="relationshipTypeSuffix">
    ///     The relationship type URI suffix to match (for example <c>"/officeDocument"</c>), compared
    ///     with an ordinal, case-sensitive <see cref="string.EndsWith(string, StringComparison)"/>.
    /// </param>
    /// <returns>The resolved target part path, with no leading slash.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="sourcePartPath"/> or <paramref name="relationshipTypeSuffix"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this package has been disposed.</exception>
    /// <exception cref="OpcFormatException">
    ///     Thrown when no non-external relationship has a matching type, the target escapes the
    ///     package root, or the source part's <c>.rels</c> part is malformed.
    /// </exception>
    public string ResolveRelationshipByType(string sourcePartPath, string relationshipTypeSuffix) =>
        TryResolveRelationshipByType(sourcePartPath, relationshipTypeSuffix)
        ?? throw new OpcFormatException(
            $"No relationship of part '{sourcePartPath}' has a Type ending with '{relationshipTypeSuffix}'.");

    /// <summary>
    ///     Non-throwing sibling of <see cref="ResolveRelationshipByType"/>: returns
    ///     <see langword="null"/> instead of throwing when no relationship matches, for navigations to
    ///     an optional part.
    /// </summary>
    /// <param name="sourcePartPath">The source part's path, or the empty string for the package root.</param>
    /// <param name="relationshipTypeSuffix">The relationship type URI suffix to match (ordinal, case-sensitive).</param>
    /// <returns>
    ///     The resolved target part path with no leading slash, or <see langword="null"/> when no
    ///     non-external relationship of the source part has a matching type (including when the source
    ///     part has no relationships at all).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="sourcePartPath"/> or <paramref name="relationshipTypeSuffix"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this package has been disposed.</exception>
    /// <exception cref="OpcFormatException">
    ///     Thrown when the matching target escapes the package root, or the source part's
    ///     <c>.rels</c> part is malformed.
    /// </exception>
    public string? TryResolveRelationshipByType(string sourcePartPath, string relationshipTypeSuffix)
    {
        ArgumentNullException.ThrowIfNull(sourcePartPath);
        ArgumentNullException.ThrowIfNull(relationshipTypeSuffix);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var normalizedSource = NormalizePartPath(sourcePartPath);
        foreach (var relationship in GetRelationships(normalizedSource).Values)
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
    ///     Resolves the target part path of the non-external relationship of
    ///     <paramref name="sourcePartPath"/> whose <c>Id</c> is <paramref name="relationshipId"/>,
    ///     returning <see langword="null"/> instead of throwing when there is none or it is external.
    /// </summary>
    /// <param name="sourcePartPath">The source part's path, or the empty string for the package root.</param>
    /// <param name="relationshipId">The relationship's <c>Id</c> value.</param>
    /// <returns>
    ///     The resolved target part path with no leading slash, or <see langword="null"/> when the
    ///     source part has no such non-external relationship.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="sourcePartPath"/> or <paramref name="relationshipId"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this package has been disposed.</exception>
    /// <exception cref="OpcFormatException">
    ///     Thrown when the target escapes the package root, or the source part's <c>.rels</c> part is malformed.
    /// </exception>
    public string? TryResolveRelationshipById(string sourcePartPath, string relationshipId)
    {
        ArgumentNullException.ThrowIfNull(sourcePartPath);
        ArgumentNullException.ThrowIfNull(relationshipId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var normalizedSource = NormalizePartPath(sourcePartPath);
        return GetRelationships(normalizedSource).TryGetValue(relationshipId, out var relationship) &&
               !relationship.IsExternal
            ? ResolveRelativeTarget(normalizedSource, relationship.Target)
            : null;
    }

    /// <summary>
    ///     Loads the root XML element of the part named <paramref name="partPath"/> with
    ///     <see cref="SafeXml"/>, bounded to <see cref="MaxXmlPartCharacters"/> characters.
    /// </summary>
    /// <param name="partPath">The part's path, with or without a leading slash.</param>
    /// <returns>The part's root element.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="partPath"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this package has been disposed.</exception>
    /// <exception cref="OpcFormatException">
    ///     Thrown when the package has no such part, or the part is not well-formed XML, declares a
    ///     DTD, exceeds <see cref="MaxXmlPartCharacters"/> characters, or has no root element.
    /// </exception>
    public XElement LoadPartXmlRoot(string partPath)
    {
        ArgumentNullException.ThrowIfNull(partPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_entriesByPath.TryGetValue(NormalizePartPath(partPath), out var entry))
        {
            throw new OpcFormatException($"The package does not contain a part named '{partPath}'.");
        }

        return LoadXmlRoot(entry);
    }

    /// <summary>
    ///     Reads the raw, undecoded content of the part named <paramref name="partPath"/> (for example
    ///     a media part such as <c>ppt/media/image1.png</c>).
    /// </summary>
    /// <param name="partPath">The part's path, with or without a leading slash.</param>
    /// <returns>The part's full content, as a new array the caller owns.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="partPath"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this package has been disposed.</exception>
    /// <exception cref="OpcFormatException">
    ///     Thrown when the package has no such part, or its decompressed content exceeds
    ///     <see cref="MaxPartBytes"/> bytes. The size is counted while copying and never taken from
    ///     the entry's declared length, so a deceptive header cannot defeat the bound.
    /// </exception>
    public byte[] GetPartBytes(string partPath)
    {
        ArgumentNullException.ThrowIfNull(partPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_entriesByPath.TryGetValue(NormalizePartPath(partPath), out var entry))
        {
            throw new OpcFormatException($"The package does not contain a part named '{partPath}'.");
        }

        using var entryStream = entry.Open();
        using var buffer = new MemoryStream();
        CopyBounded(
            entryStream,
            buffer,
            MaxPartBytes,
            $"The part '{partPath}' exceeds the maximum supported decompressed size of {MaxPartBytes} bytes (the compressed ZIP entry may be a decompression bomb).");
        return buffer.ToArray();
    }

    /// <summary>
    ///     Releases the resources held by this package (the ZIP archive and its backing buffer).
    ///     Calling this method more than once has no effect beyond the first call.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _archive.Dispose();
        _stream.Dispose();
        _disposed = true;
    }

    /// <summary>
    ///     Copies <paramref name="source"/> into <paramref name="destination"/> in fixed-size chunks,
    ///     failing the moment the running total exceeds <paramref name="maxBytes"/>, so memory and read
    ///     time stay bounded for a huge or non-terminating source.
    /// </summary>
    /// <param name="source">The stream to copy from.</param>
    /// <param name="destination">The stream to copy into.</param>
    /// <param name="maxBytes">The largest number of bytes that may be read.</param>
    /// <param name="exceededMessage">The message of the exception thrown when the bound is exceeded.</param>
    /// <exception cref="OpcFormatException">Thrown once more than <paramref name="maxBytes"/> bytes have been read.</exception>
    private static void CopyBounded(Stream source, MemoryStream destination, long maxBytes, string exceededMessage)
    {
        var buffer = new byte[CopyChunkBytes];
        var total = 0L;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new OpcFormatException(exceededMessage);
            }

            destination.Write(buffer, 0, read);
        }
    }

    /// <summary>Opens <paramref name="stream"/> as a read-only ZIP archive, mapping any failure to <see cref="OpcFormatException"/>.</summary>
    /// <param name="stream">The stream to open.</param>
    /// <returns>The archive.</returns>
    /// <exception cref="OpcFormatException">The stream is not a readable ZIP archive.</exception>
    private static ZipArchive OpenZipArchive(Stream stream)
    {
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new OpcFormatException(ex.Message, ex);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            throw new OpcFormatException("The supplied stream is not a valid ZIP/OPC package.", ex);
        }
    }

    /// <summary>
    ///     Builds a case-sensitive lookup of every entry by its full name. OPC compares part names
    ///     ordinally here, so entries that differ only by case are distinct parts, and an
    ///     incorrectly-cased reserved name (for example <c>[content_types].xml</c>) is never accepted
    ///     as <c>[Content_Types].xml</c>.
    /// </summary>
    /// <param name="archive">The archive to index.</param>
    /// <returns>The lookup.</returns>
    /// <exception cref="OpcFormatException">Two entries have an identical full name.</exception>
    private static Dictionary<string, ZipArchiveEntry> BuildEntryLookup(ZipArchive archive)
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
                throw new OpcFormatException(
                    $"The package contains duplicate entries named '{entry.FullName}'.", ex);
            }
        }

        return entries;
    }

    /// <summary>
    ///     Parses <c>[Content_Types].xml</c> and the package-level <c>_rels/.rels</c>, so a malformed
    ///     package fails at open time rather than on first use.
    /// </summary>
    /// <exception cref="OpcFormatException">Either part is missing or malformed.</exception>
    private void InitializePackage()
    {
        if (!_entriesByPath.TryGetValue(ContentTypesPartName, out var contentTypesEntry))
        {
            throw new OpcFormatException(
                $"The package is missing the required '{ContentTypesPartName}' part.");
        }

        ParseContentTypes(contentTypesEntry);

        if (!_entriesByPath.TryGetValue(PackageRelationshipsPartName, out var packageRelsEntry))
        {
            throw new OpcFormatException(
                $"The package is missing the required '{PackageRelationshipsPartName}' part.");
        }

        _relationshipCache[string.Empty] = ParseRelationships(packageRelsEntry);
    }

    /// <summary>Parses the <c>&lt;Default&gt;</c> and <c>&lt;Override&gt;</c> elements of <c>[Content_Types].xml</c>.</summary>
    /// <param name="entry">The <c>[Content_Types].xml</c> entry.</param>
    /// <exception cref="OpcFormatException">
    ///     The part is not well-formed XML, its root is not the content-types <c>Types</c> element, or
    ///     a child lacks a required attribute.
    /// </exception>
    private void ParseContentTypes(ZipArchiveEntry entry)
    {
        var root = LoadXmlRoot(entry);
        if (root.Name != ContentTypesNamespace + "Types")
        {
            throw new OpcFormatException(
                $"'{entry.FullName}' root element is '{root.Name}', not the expected '{ContentTypesNamespace + "Types"}'.");
        }

        foreach (var defaultElement in root.Elements(ContentTypesNamespace + "Default"))
        {
            var extension = (string?)defaultElement.Attribute("Extension");
            var contentType = (string?)defaultElement.Attribute("ContentType");
            if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(contentType))
            {
                throw new OpcFormatException(
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
                throw new OpcFormatException(
                    $"'{entry.FullName}' has an <Override> element with a missing or empty 'PartName'/'ContentType' attribute.");
            }

            _overrideContentTypes[NormalizePartPath(partName)] = contentType;
        }
    }

    /// <summary>Parses a <c>.rels</c> part's <c>&lt;Relationship&gt;</c> elements into a lookup by relationship identifier.</summary>
    /// <param name="entry">The <c>.rels</c> entry.</param>
    /// <returns>The relationships, keyed by <c>Id</c>.</returns>
    /// <exception cref="OpcFormatException">
    ///     The part is not well-formed XML, its root is not the <c>Relationships</c> element, a child
    ///     lacks <c>Id</c>/<c>Type</c>/<c>Target</c>, or two children declare the same <c>Id</c>.
    /// </exception>
    private static Dictionary<string, PackageRelationship> ParseRelationships(ZipArchiveEntry entry)
    {
        var root = LoadXmlRoot(entry);
        if (root.Name != RelationshipsNamespace + "Relationships")
        {
            throw new OpcFormatException(
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
                throw new OpcFormatException(
                    $"'{entry.FullName}' has a <Relationship> element with a missing or empty 'Id'/'Type'/'Target' attribute.");
            }

            var targetMode = (string?)relationshipElement.Attribute("TargetMode");
            var isExternal = string.Equals(targetMode, "External", StringComparison.OrdinalIgnoreCase);

            if (!relationships.TryAdd(id, new PackageRelationship(target, type, isExternal)))
            {
                throw new OpcFormatException(
                    $"'{entry.FullName}' has a <Relationship> element with a duplicate 'Id' attribute value '{id}'.");
            }
        }

        return relationships;
    }

    /// <summary>Loads <paramref name="entry"/> as bounded, hardened XML and returns its root element.</summary>
    /// <param name="entry">The entry to load.</param>
    /// <returns>The root element.</returns>
    /// <exception cref="OpcFormatException">The entry is not usable XML (see <see cref="SafeXml"/>) or has no root.</exception>
    private static XElement LoadXmlRoot(ZipArchiveEntry entry)
    {
        try
        {
            using var stream = entry.Open();
            var document = SafeXml.Load(stream, MaxXmlPartCharacters);
            return document.Root ?? throw new OpcFormatException(
                $"'{entry.FullName}' does not contain a root XML element.");
        }
        catch (XmlException ex)
        {
            throw new OpcFormatException($"'{entry.FullName}' is not well-formed XML.", ex);
        }
    }

    /// <summary>
    ///     Returns the parsed relationships of <paramref name="normalizedSourcePartPath"/>, parsing and
    ///     caching them on first use. A part with no <c>.rels</c> part simply has no relationships.
    /// </summary>
    /// <param name="normalizedSourcePartPath">The source part path with no leading slash, or the empty string for the package root.</param>
    /// <returns>The relationships keyed by <c>Id</c>.</returns>
    private Dictionary<string, PackageRelationship> GetRelationships(string normalizedSourcePartPath)
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
    ///     Computes the <c>.rels</c> part path that holds <paramref name="normalizedPartPath"/>'s
    ///     relationships, by the OPC convention of a sibling <c>_rels</c> folder (for example
    ///     <c>"ppt/slides/slide1.xml"</c> maps to <c>"ppt/slides/_rels/slide1.xml.rels"</c>); the empty
    ///     string (the package root) maps to <see cref="PackageRelationshipsPartName"/>.
    /// </summary>
    /// <param name="normalizedPartPath">The part path with no leading slash.</param>
    /// <returns>The <c>.rels</c> part path.</returns>
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
    ///     Resolves <paramref name="target"/> against <paramref name="normalizedBasePartPath"/>'s
    ///     directory, or against the package root when it begins with <c>'/'</c>, normalizing
    ///     <c>"."</c> and <c>".."</c> segments away.
    /// </summary>
    /// <param name="normalizedBasePartPath">The source part path with no leading slash.</param>
    /// <param name="target">The relationship's raw <c>Target</c> value.</param>
    /// <returns>The resolved part path with no leading slash.</returns>
    /// <exception cref="OpcFormatException">The target has more <c>".."</c> segments than preceding directories.</exception>
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
    ///     Splits <paramref name="path"/> on <c>'/'</c>, drops <c>"."</c> segments, lets <c>".."</c>
    ///     pop the previous segment, and re-joins the result.
    /// </summary>
    /// <param name="path">The not-yet-normalized path.</param>
    /// <returns>The normalized path.</returns>
    /// <exception cref="OpcFormatException">A <c>".."</c> segment has no preceding segment to pop (it escapes the package root).</exception>
    private static string NormalizeSegments(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>(segments.Length);

        foreach (var segment in segments.Where(static s => s != "."))
        {
            if (segment != "..")
            {
                stack.Add(segment);
            }
            else if (stack.Count > 0)
            {
                stack.RemoveAt(stack.Count - 1);
            }
            else
            {
                throw new OpcFormatException(
                    $"Relationship target '{path}' escapes the package root.");
            }
        }

        return string.Join('/', stack);
    }

    /// <summary>Returns <paramref name="normalizedPartPath"/>'s extension with no leading dot, or null if it has none.</summary>
    /// <param name="normalizedPartPath">The part path with no leading slash.</param>
    /// <returns>The extension, or null.</returns>
    private static string? GetExtension(string normalizedPartPath)
    {
        var lastDot = normalizedPartPath.LastIndexOf('.');
        var lastSlash = normalizedPartPath.LastIndexOf('/');
        return lastDot > lastSlash && lastDot < normalizedPartPath.Length - 1
            ? normalizedPartPath[(lastDot + 1)..]
            : null;
    }
}
