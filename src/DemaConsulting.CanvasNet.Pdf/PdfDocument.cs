using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf;

/// <summary>
///     Provides read-only access to a PDF (Portable Document Format) document: page count, each
///     page's displayed size and rotation, and (in a later phase) rendered page content.
/// </summary>
/// <remarks>
///     <para>
///         An instance is created by <see cref="Open(System.IO.Stream)"/> or
///         <see cref="Open(string)"/>, which read and parse the entire document exactly once (the
///         resolved page list and cross-reference data are cached for the lifetime of the
///         instance, so <see cref="PageCount"/>/<see cref="GetPageInfo(int)"/>/
///         <see cref="Render(int, int, int)"/> are cheap, repeatable operations against the same
///         instance).
///     </para>
///     <para>
///         Phase 1 of this package's implementation established document parsing and the
///         page-info API surface. Phase 2 adds a content-stream interpreter:
///         <see cref="Render(int, int, int)"/> now tokenizes and executes each page's
///         <c>/Contents</c> (path-construction operators <c>m</c>/<c>l</c>/<c>c</c>/<c>v</c>/
///         <c>y</c>/<c>h</c>/<c>re</c>, path-painting operators <c>f</c>/<c>F</c>/<c>f*</c>/
///         <c>S</c>/<c>s</c>/<c>B</c>/<c>B*</c>/<c>b</c>/<c>b*</c>/<c>n</c>, and the graphics-
///         state operators <c>q</c>/<c>Q</c>/<c>cm</c>/<c>w</c>/<c>J</c>/<c>j</c>/<c>M</c>/
///         <c>d</c>), painting real path geometry onto the returned <see cref="Surface"/> in
///         the correct device-pixel position for the page's <c>/MediaBox</c> origin, effective
///         <c>/Rotate</c>, and the caller's requested render size. Every other keyword
///         (color, text, image, clipping, and additional stream filter operators) is silently
///         skipped - not an error, simply out of this phase's scope. <strong>Phase 2
///         limitation</strong>: every filled/stroked path paints in solid opaque black,
///         regardless of any color operator a content stream may issue - no color space or
///         color-setting operator is implemented yet; a later phase is expected to add real
///         color support. A page with no <c>/Contents</c> at all still renders as a fully
///         transparent (blank) <see cref="Surface"/>, exactly as every page did in Phase 1.
///     </para>
///     <para>
///         Encrypted documents (a trailer declaring an <c>/Encrypt</c> key) are rejected with
///         <see cref="UnsupportedImageFeatureException"/> - this library never attempts to
///         interpret encrypted bytes as plaintext.
///     </para>
/// </remarks>
public sealed partial class PdfDocument : IDisposable
{
    /// <summary>
    ///     The entire document's bytes, buffered once at <see cref="Open(System.IO.Stream)"/>/
    ///     <see cref="Open(string)"/> time. Every offset recorded in <see cref="_xref"/> and every
    ///     <see cref="PdfObject"/> parsed from this document indexes into this same array.
    /// </summary>
    private readonly byte[] _buffer;

    /// <summary>
    ///     The resolved cross-reference table: maps an indirect object number to where its value
    ///     can be found (a direct byte offset in <see cref="_buffer"/>, or a compressed-object
    ///     location within an object stream).
    /// </summary>
    private Dictionary<int, XrefEntry> _xref;

    /// <summary>
    ///     Caches each indirect object's parsed value the first time it is resolved via
    ///     <see cref="GetObject(int)"/>, so repeated references to the same object number are not
    ///     re-parsed.
    /// </summary>
    private readonly Dictionary<int, PdfObject> _objectCache = new();

    /// <summary>
    ///     Every page's pre-resolved, rotation-adjusted <see cref="PdfPageInfo"/>, in document
    ///     order, computed once during construction by <see cref="BuildPageList(PdfObject)"/>.
    /// </summary>
    private readonly IReadOnlyList<PdfPageInfo> _pages;

    /// <summary>
    ///     Set once <see cref="Dispose"/> has been called; every other public member checks this
    ///     flag first via <see cref="ObjectDisposedException"/>'s <c>ThrowIf</c> helper and
    ///     rejects further use once it is <see langword="true"/>.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PdfDocument"/> class by parsing the
    ///     supplied, already fully-buffered document bytes.
    /// </summary>
    /// <param name="buffer">The complete document bytes.</param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the document cannot be parsed at all (no cross-reference data or document
    ///     catalog could be located, even via the linear-scan fallback), or when a required
    ///     structure (trailer, catalog, page tree) is malformed.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the document's trailer declares an <c>/Encrypt</c> key.
    /// </exception>
    private PdfDocument(byte[] buffer)
    {
        _buffer = buffer;
        _xref = [];

        PdfObject trailer;
        try
        {
            trailer = ParseCrossReferenceChain();
            if (!IsValidCatalogRoot(trailer))
            {
                throw new InvalidDataException("Trailer /Root does not resolve to a valid /Catalog.");
            }
        }
        catch (InvalidDataException)
        {
            // Normal cross-reference parsing failed, or resolved to something other than a valid
            // catalog (for example a corrupt/missing startxref, or offsets that do not point at
            // real objects) - fall back to a linear scan for "N G obj" markers, discarding any
            // partially-cached objects resolved against the abandoned cross-reference table.
            _objectCache.Clear();
            trailer = BuildLinearScanFallback();
        }

        CheckForEncryption(trailer);
        _pages = BuildPageList(trailer);
    }

    /// <summary>
    ///     Opens and parses a PDF document from a stream.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the document from. The entire stream is read into an in-memory
    ///     buffer; this method does not take ownership of, and never disposes or closes,
    ///     <paramref name="stream"/>.
    /// </param>
    /// <returns>A new <see cref="PdfDocument"/> instance representing the parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the document cannot be parsed.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the document's trailer declares an <c>/Encrypt</c> key.
    /// </exception>
    public static PdfDocument Open(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var buffered = new MemoryStream();
        stream.CopyTo(buffered);
        return new PdfDocument(buffered.ToArray());
    }

    /// <summary>
    ///     Opens and parses a PDF document from a file path.
    /// </summary>
    /// <param name="path">The path of the PDF file to read.</param>
    /// <returns>A new <see cref="PdfDocument"/> instance representing the parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="path"/> is empty or consists only of white space.
    /// </exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the document cannot be parsed.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the document's trailer declares an <c>/Encrypt</c> key.
    /// </exception>
    /// <remarks>
    ///     File-system exceptions (for example <see cref="FileNotFoundException"/>,
    ///     <see cref="DirectoryNotFoundException"/>, <see cref="UnauthorizedAccessException"/>, or
    ///     <see cref="IOException"/>) raised while opening <paramref name="path"/> propagate
    ///     uncaught to the caller.
    /// </remarks>
    public static PdfDocument Open(string path)
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
    ///     Gets the total number of pages in the document.
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
    ///     Gets the specified page's displayed (rotation-adjusted) size and effective rotation.
    /// </summary>
    /// <param name="pageIndex">The zero-based index of the page.</param>
    /// <returns>The page's <see cref="PdfPageInfo"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="pageIndex"/> is negative or greater than or equal to
    ///     <see cref="PageCount"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    public PdfPageInfo GetPageInfo(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (pageIndex < 0 || pageIndex >= _pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, "Page index is out of range.");
        }

        return _pages[pageIndex];
    }

    /// <summary>
    ///     Renders the specified page into a new <see cref="Surface"/> of the given dimensions,
    ///     tokenizing and executing the page's <c>/Contents</c> content stream (see the
    ///     <see cref="PdfDocument"/> class remarks for the recognized operator set and the
    ///     current "solid opaque black only" color limitation).
    /// </summary>
    /// <param name="pageIndex">The zero-based index of the page to render.</param>
    /// <param name="width">The width of the rendered surface, in pixels.</param>
    /// <param name="height">The height of the rendered surface, in pixels.</param>
    /// <returns>
    ///     A new <see cref="Surface"/> of the requested <paramref name="width"/> x
    ///     <paramref name="height"/>, painted with the page's interpreted content-stream geometry.
    ///     A page with no <c>/Contents</c> renders as a fully transparent (blank) surface.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="pageIndex"/> is negative or greater than or equal to
    ///     <see cref="PageCount"/>, or when <paramref name="width"/>/<paramref name="height"/> is
    ///     outside <see cref="Surface"/>'s own valid dimension range (propagated, unwrapped, from
    ///     the <see cref="Surface(int, int)"/> constructor).
    /// </exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when <c>/Contents</c> is malformed (neither a stream nor an array of streams,
    ///     or an array entry that does not resolve to a stream), when the content stream is not
    ///     lexically well-formed, or when a recognized operator's operand count/type does not
    ///     match its documented requirement.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    public Surface Render(int pageIndex, int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (pageIndex < 0 || pageIndex >= _pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, "Page index is out of range.");
        }

        var surface = new Surface(width, height);
        var pageInfo = _pages[pageIndex];
        var (pageNode, x0, y0, boxWidth, boxHeight) = ResolvePageNodeAndMediaBox(pageIndex);
        var baseCtm = BuildBaseCtm(x0, y0, boxWidth, boxHeight, pageInfo.Rotation, width, height);
        var contentBytes = ResolvePageContentBytes(pageNode);
        ExecuteContentStream(contentBytes, surface, baseCtm);
        return surface;
    }

    /// <summary>
    ///     Releases the resources held by this <see cref="PdfDocument"/>.
    /// </summary>
    /// <remarks>
    ///     Idempotent: calling this method more than once has no additional effect. After this
    ///     method has been called, every other public member throws
    ///     <see cref="ObjectDisposedException"/>.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}
