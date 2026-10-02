namespace DemaConsulting.CanvasNet.Pptx;

/// <summary>
///     Provides read-only access to a PowerPoint (<c>.pptx</c>) presentation document.
/// </summary>
/// <remarks>
///     <para>
///         An instance is created by <see cref="Open(Stream)"/> or <see cref="Open(string)"/>,
///         which read and parse the OOXML (Office Open XML) package structure exactly once (the
///         entire input is buffered in memory first, so the caller's stream need not remain open
///         or seekable afterward).
///     </para>
///     <para>
///         <strong>Phase 1a scope (this release)</strong>: only the underlying OOXML package
///         layer is implemented - opening the <c>.pptx</c> file as a ZIP archive, resolving
///         <c>[Content_Types].xml</c> (both its default extension-to-content-type mappings and
///         any part-specific overrides), and resolving package-level (<c>_rels/.rels</c>) and
///         per-part (<c>{dir}/_rels/{partName}.rels</c>) relationships, including relative
///         target resolution (for example <c>../slideLayouts/slideLayout1.xml</c>). No
///         presentation-specific content is parsed yet - <c>ppt/presentation.xml</c>, slides,
///         slide layouts/masters, and any rendering surface are all deferred to later phases.
///         <see cref="Open(Stream)"/>/<see cref="Open(string)"/> will succeed on any well-formed
///         OOXML/ZIP package, even one that is not actually a presentation, since nothing beyond
///         the package layer is validated this phase.
///     </para>
/// </remarks>
public sealed partial class PptxDocument : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PptxDocument"/> class by parsing the
    ///     supplied, already fully-buffered package bytes.
    /// </summary>
    /// <param name="buffer">
    ///     The complete package bytes. Ownership transfers to this instance - the bytes back the
    ///     non-writable <see cref="_stream"/> for the lifetime of this instance.
    /// </param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when <paramref name="buffer"/> is not a readable ZIP archive, or when the
    ///     package is missing the required <c>[Content_Types].xml</c> or <c>_rels/.rels</c>
    ///     parts.
    /// </exception>
    private PptxDocument(byte[] buffer)
    {
        _stream = new MemoryStream(buffer, writable: false);
        _archive = OpenZipArchive(_stream);
        _entriesByPath = BuildEntryLookup(_archive);
        _defaultContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _overrideContentTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        _relationshipCache = new Dictionary<string, IReadOnlyDictionary<string, PackageRelationship>>(StringComparer.Ordinal);

        InitializePackage();
    }

    /// <summary>
    ///     Opens and parses a <c>.pptx</c> document from a stream.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the package from. The entire stream is read into an in-memory
    ///     buffer; this method does not take ownership of, and never disposes or closes,
    ///     <paramref name="stream"/>.
    /// </param>
    /// <returns>A new <see cref="PptxDocument"/> instance representing the parsed package.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the package cannot be parsed - see the private constructor's remarks for
    ///     the exact conditions.
    /// </exception>
    public static PptxDocument Open(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var buffered = new MemoryStream();
        stream.CopyTo(buffered);
        return new PptxDocument(buffered.ToArray());
    }

    /// <summary>
    ///     Opens and parses a <c>.pptx</c> document from a file path.
    /// </summary>
    /// <param name="path">The path of the <c>.pptx</c> file to read.</param>
    /// <returns>A new <see cref="PptxDocument"/> instance representing the parsed package.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="path"/> is empty or consists only of white space.
    /// </exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the package cannot be parsed - see <see cref="Open(Stream)"/>'s remarks for
    ///     the exact conditions.
    /// </exception>
    /// <remarks>
    ///     File-system exceptions (for example <see cref="FileNotFoundException"/>,
    ///     <see cref="DirectoryNotFoundException"/>, <see cref="UnauthorizedAccessException"/>, or
    ///     <see cref="IOException"/>) raised while opening <paramref name="path"/> propagate
    ///     uncaught to the caller.
    /// </remarks>
    public static PptxDocument Open(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return Open(stream);
    }

    /// <summary>
    ///     Releases the resources held by this <see cref="PptxDocument"/> (the underlying ZIP
    ///     archive and its backing buffer). Calling this method more than once has no effect
    ///     beyond the first call.
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
}
