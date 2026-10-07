using System.IO.Compression;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     Provides read-only access to a Microsoft Visio (<c>.vsdx</c>) diagram document.
/// </summary>
/// <remarks>
///     <para>
///         An instance is created by <see cref="Open(Stream)"/> or <see cref="Open(string)"/>,
///         which read and parse the OPC (Open Packaging Conventions) package structure exactly
///         once (the entire input is buffered in memory first, so the caller's stream need not
///         remain open or seekable afterward).
///     </para>
///     <para>
///         <strong>Milestone 2 (this release)</strong> implements only the underlying OPC
///         package layer and page index: opening the <c>.vsdx</c> file as a ZIP archive,
///         resolving <c>[Content_Types].xml</c>, resolving the package-level
///         (<c>_rels/.rels</c>) and per-part relationship graph (including relative target
///         resolution, for example <c>../masters/masters.xml</c>), locating
///         <c>visio/document.xml</c> and <c>visio/pages/pages.xml</c> through that relationship
///         graph (never by filename convention), and parsing each declared page's name and
///         declared size (see <see cref="PageCount"/>/<see cref="GetPageSize(int)"/>). No shape,
///         geometry, master-content, style, theme, text, or connector parsing exists yet - all
///         deferred to later milestones; see <c>canvas-net-vsdx.md</c>'s Implementation Phase
///         Plan.
///     </para>
///     <para>
///         <strong>Thread safety</strong>: a <see cref="VsdxDocument"/> instance is <em>not</em>
///         thread-safe, mirroring the sibling <c>DemaConsulting.CanvasNet.Pptx.PptxDocument</c>'s
///         own documented precedent - open one instance per thread when concurrent access is
///         needed, or synchronize all calls to a single shared instance externally.
///     </para>
/// </remarks>
public sealed partial class VsdxDocument : IDisposable
{
    /// <summary>
    ///     The maximum number of bytes <see cref="Open(Stream)"/> will buffer from a caller-
    ///     supplied stream before rejecting it with <see cref="InvalidDataException"/>.
    /// </summary>
    /// <remarks>
    ///     <see cref="Open(Stream)"/> buffers its entire input into memory before any ZIP
    ///     validation occurs; without a bound, a very large, or deliberately non-terminating,
    ///     caller-supplied stream could exhaust memory or buffer indefinitely before that
    ///     validation ever runs. 268,435,456 bytes (256 MiB) mirrors
    ///     <c>PptxDocument.MaxPackageBytes</c>'s own documented sizing rationale - large enough
    ///     for any realistic <c>.vsdx</c> package, while remaining small enough to keep worst-case
    ///     memory use bounded to a small, practical amount for an attacker-controlled input.
    /// </remarks>
    private const long MaxPackageBytes = 268_435_456L;

    /// <summary>
    ///     The non-writable, in-memory stream wrapping the package's buffered bytes that backs
    ///     <see cref="_archive"/> for the lifetime of this instance.
    /// </summary>
    private readonly MemoryStream _stream;

    /// <summary>The opened, read-only ZIP archive view of this package.</summary>
    private readonly ZipArchive _archive;

    /// <summary>
    ///     Every part name present in the package, for existence checks, keyed case-sensitively
    ///     per OPC's own ordinal part-name comparison rule.
    /// </summary>
    private readonly IReadOnlyDictionary<string, ZipArchiveEntry> _entriesByPath;

    /// <summary>Set once <see cref="Dispose"/> has been called.</summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VsdxDocument"/> class by parsing the
    ///     supplied, already fully-buffered package bytes.
    /// </summary>
    /// <param name="buffer">
    ///     The complete package bytes. Ownership transfers to this instance - the bytes back the
    ///     non-writable <see cref="_stream"/> for the lifetime of this instance.
    /// </param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="buffer"/> is not a readable ZIP archive, when the package
    ///     is missing a required part, or when any required part is malformed - see
    ///     <see cref="InitializePackage"/>/<see cref="InitializePages"/>'s own remarks for the
    ///     exact conditions.
    /// </exception>
    private VsdxDocument(byte[] buffer)
    {
        _stream = new MemoryStream(buffer, writable: false);
        _archive = OpenZipArchive(_stream);
        _entriesByPath = BuildEntryLookup(_archive);
        _defaultContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _overrideContentTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        _relationshipCache = new Dictionary<string, IReadOnlyDictionary<string, PackageRelationship>>(StringComparer.Ordinal);

        InitializePackage();
        InitializePages();
    }

    /// <summary>
    ///     Opens and parses a <c>.vsdx</c> document from a stream.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the package from. The entire stream is read into an in-memory
    ///     buffer; this method does not take ownership of, and never disposes or closes,
    ///     <paramref name="stream"/>.
    /// </param>
    /// <returns>A new <see cref="VsdxDocument"/> instance representing the parsed package.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the package cannot be parsed - see the private constructor's remarks for
    ///     the exact conditions - or when <paramref name="stream"/> supplies more than
    ///     <see cref="MaxPackageBytes"/> bytes (see <see cref="MaxPackageBytes"/>'s own remarks
    ///     for the bound's rationale).
    /// </exception>
    public static VsdxDocument Open(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var buffered = CopyBounded(stream);
        return new VsdxDocument(buffered.ToArray());
    }

    /// <summary>
    ///     Opens and parses a <c>.vsdx</c> document from a file path.
    /// </summary>
    /// <param name="path">The path of the <c>.vsdx</c> file to read.</param>
    /// <returns>A new <see cref="VsdxDocument"/> instance representing the parsed package.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="path"/> is empty or consists only of white space.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the package cannot be parsed - see <see cref="Open(Stream)"/>'s remarks for
    ///     the exact conditions.
    /// </exception>
    /// <remarks>
    ///     File-system exceptions (for example <see cref="FileNotFoundException"/>,
    ///     <see cref="DirectoryNotFoundException"/>, <see cref="UnauthorizedAccessException"/>, or
    ///     <see cref="IOException"/>) raised while opening <paramref name="path"/> propagate
    ///     uncaught to the caller.
    /// </remarks>
    public static VsdxDocument Open(string path)
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
    ///     Copies <paramref name="source"/> into a new, fully-buffered <see cref="MemoryStream"/>,
    ///     reading in fixed-size chunks and rejecting the input the moment the running total
    ///     exceeds <see cref="MaxPackageBytes"/> - bounding both the worst-case memory this copy
    ///     can consume and the worst-case time it can spend reading a deliberately large or
    ///     non-terminating <paramref name="source"/>, rather than calling
    ///     <see cref="Stream.CopyTo(Stream)"/> unconditionally and discovering the problem only
    ///     after it has already exhausted memory.
    /// </summary>
    /// <param name="source">The stream to copy from.</param>
    /// <returns>
    ///     A new, position-reset-to-zero <see cref="MemoryStream"/> containing every byte read
    ///     from <paramref name="source"/>.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown once more than <see cref="MaxPackageBytes"/> bytes have been read from
    ///     <paramref name="source"/>.
    /// </exception>
    private static MemoryStream CopyBounded(Stream source)
    {
        var destination = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0L;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > MaxPackageBytes)
            {
                destination.Dispose();
                throw new InvalidDataException(
                    $"The package stream exceeds the maximum supported size of {MaxPackageBytes} bytes.");
            }

            destination.Write(buffer, 0, read);
        }

        destination.Position = 0;
        return destination;
    }

    /// <summary>
    ///     Gets the total number of pages declared by <c>visio/pages/pages.xml</c>'s page index.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    public int PageCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _pages.Count;
        }
    }

    /// <summary>
    ///     Returns the requested page's declared name and size (converted from inches to EMU,
    ///     English Metric Units).
    /// </summary>
    /// <param name="pageIndex">The zero-based page index, in <c>[0, PageCount)</c>.</param>
    /// <returns>The requested page's <see cref="VsdxPageInfo"/>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="pageIndex"/> is outside <c>[0, PageCount)</c>.
    /// </exception>
    public VsdxPageInfo GetPageSize(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (pageIndex < 0 || pageIndex >= _pages.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageIndex),
                pageIndex,
                $"pageIndex must be in the range [0, {_pages.Count}).");
        }

        return _pages[pageIndex];
    }

    /// <summary>
    ///     Releases the resources held by this <see cref="VsdxDocument"/> (the underlying ZIP
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
