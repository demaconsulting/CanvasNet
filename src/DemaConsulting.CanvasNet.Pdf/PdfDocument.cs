// cspell:ignore CCITT Zapf Nonsymbolic Noto uncatchable
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf;

/// <summary>
///     Provides read-only access to a PDF (Portable Document Format) document: page count, each
///     page's displayed size and rotation, and fully rendered page content.
/// </summary>
/// <remarks>
///     <para>
///         An instance is created by <see cref="Open(System.IO.Stream, string?)"/> or
///         <see cref="Open(string, string?)"/>, which read and parse the entire document exactly once (the
///         resolved page list and cross-reference data are cached for the lifetime of the
///         instance, so <see cref="PageCount"/>/<see cref="GetPageInfo(int)"/>/
///         <see cref="Render(int, int, int, PdfRenderOptions?)"/> are cheap, repeatable operations against the same
///         instance).
///     </para>
///     <para>
///         Document parsing and the page-info API surface are backed by a content-stream
///         interpreter (path-construction/painting operators and the graphics-state stack), real
///         device color (<c>g</c>/<c>G</c>/<c>rg</c>/<c>RG</c>/<c>k</c>/<c>K</c>/<c>cs</c>/
///         <c>CS</c>/<c>sc</c>/<c>SC</c>/<c>scn</c>/<c>SCN</c>), a generalized <c>/Filter</c>/
///         <c>/DecodeParms</c> stream-decoding pipeline, and image XObjects (<c>Do</c>:
///         <c>DCTDecode</c> via <see cref="Codecs.JpegCodec"/>, or raw <c>DeviceGray</c>/
///         <c>DeviceRGB</c>/<c>DeviceCMYK</c> 8-bit samples, composited through the current
///         transformation matrix).
///     </para>
///     <para>
///         <strong>Supported stream filters:</strong> <c>FlateDecode</c>/<c>LZWDecode</c>/
///         <c>ASCII85Decode</c>/<c>ASCIIHexDecode</c>/<c>RunLengthDecode</c> are supported for any
///         stream (cross-reference/object streams, page <c>/Contents</c>, Form XObjects, and image
///         XObjects alike, including PNG/TIFF predictor reversal where declared via
///         <c>/DecodeParms</c>); <c>DCTDecode</c> and <c>CCITTFaxDecode</c> (Group 4/MMR
///         two-dimensional only, decoded by <see cref="DecodeCcittFax"/> - see
///         <c>PdfDocument.CcittFax.cs</c>) are supported specifically for image XObjects; Group 3
///         (<c>/DecodeParms /K</c> zero or greater) and an explicit <c>/EndOfLine true</c> both
///         fail closed with <see cref="UnsupportedImageFeatureException"/>; and only
///         <c>JPXDecode</c> (and any other unrecognized filter name) remains entirely unsupported
///         for image XObjects.
///     </para>
///     <para>
///         Text rendering interprets the text object/state/positioning/showing operators
///         (<c>BT</c>/<c>ET</c>, <c>Tc</c>/<c>Tw</c>/<c>Tz</c>/<c>TL</c>/<c>Tf</c>/<c>Tr</c>/
///         <c>Ts</c>, <c>Td</c>/<c>TD</c>/<c>Tm</c>/<c>T*</c>, and <c>Tj</c>/<c>'</c>/<c>"</c>/
///         <c>TJ</c>), resolving each named <c>/Resources/Font</c> entry against one of four
///         supported font subtypes: simple <c>/Subtype /TrueType</c> and <c>/Subtype /Type1</c>
///         fonts (see <c>PdfDocument.Fonts.cs</c>/<c>PdfDocument.Fonts.Type1.cs</c>), composite
///         <c>/Subtype /Type0</c> fonts with an Identity-H encoding and a <c>CIDFontType2</c> or
///         <c>CIDFontType0</c> descendant (see <c>PdfDocument.Fonts.Type0.cs</c>), and
///         procedure-painted <c>/Subtype /Type3</c> fonts, whose glyph procedures are executed as
///         nested content streams (see <c>PdfDocument.Fonts.Type3.cs</c>) - only <c>/MMType1</c>
///         (Multiple Master Type 1) fails closed with <see cref="UnsupportedImageFeatureException"/>.
///         Each resolved glyph outline is painted through the composed text-rendering matrix (see
///         <c>PdfDocument.Text.cs</c>). Only the <c>/WinAnsiEncoding</c>/<c>/MacRomanEncoding</c>
///         base encodings (plus <c>/Differences</c> overrides) are recognized for a simple font
///         (any other named base <c>/Encoding</c> fails closed), and only fill mode <c>0</c> and
///         invisible mode <c>3</c> are supported for the text-rendering mode (a stroke/clip
///         mode, <c>1</c>/<c>2</c>/<c>4</c>-<c>7</c>, fails closed) - all with
///         <see cref="UnsupportedImageFeatureException"/> rather than silently substituting
///         or skipping. A font with an embedded font program (<c>/FontFile</c>/<c>/FontFile2</c>/
///         <c>/FontFile3</c>, as appropriate to its subtype) uses that embedded font; a simple
///         font with no embedded program is automatically substituted with a matching system or
///         bundled font rather than failing closed: <see cref="Fonts.SystemFontCatalog"/> is
///         searched for the closest-matching font installed on the host operating system by
///         family name and serif/fixed-pitch/bold/italic style (derived from the Standard-14 name
///         table when <c>/BaseFont</c> is one of the 14 standard names, else from
///         <c>/FontDescriptor</c> flags/weight/angle and the name itself); when no system font
///         matches, a bundled Liberation Sans/Serif/Mono font is used instead as a deterministic
///         last resort (see <c>PdfDocument.FontFallback.cs</c>). This is fully automatic - there
///         is no new public API and no "fallback occurred" diagnostics. <c>Symbol</c> and
///         <c>ZapfDingbats</c> resolve instead via a dedicated bundled Noto substitute font union
///         (see <c>PdfDocument.FontFallback.cs</c>'s <c>ResolveSymbolicNotoFallback</c>), using
///         the built-in Symbol/ZapfDingbats Appendix D encoding rather than
///         <c>/WinAnsiEncoding</c>. Any other font whose <c>/FontDescriptor/Flags</c> declares
///         <c>Symbolic</c> without also declaring <c>Nonsymbolic</c> is still the sole remaining
///         exception: with no embedded <c>/FontFile2</c> it still fails closed with
///         <see cref="UnsupportedImageFeatureException"/>, since its symbol/dingbat glyph set
///         has no meaningful generic-family equivalent.
///     </para>
///     <para>
///         <c>/Pattern</c>-color-space shading (axial/radial/mesh, <c>/ShadingType 2</c>-<c>7</c>,
///         driven by <c>/FunctionType 0</c>/<c>2</c>/<c>3</c> functions) and tiling
///         (<c>/PaintType 1</c>/<c>2</c>) pattern fills/strokes are fully supported, as are placed
///         Form XObjects: <c>Do</c> on a <c>/Subtype /Form</c> XObject decodes its content stream
///         (via the same generic <c>/Filter</c>/<c>/DecodeParms</c> pipeline every other stream
///         uses), concatenates its optional <c>/Matrix</c> into the current transformation matrix
///         (the same left-multiply convention the <c>cm</c> operator uses), resolves its own
///         <c>/Resources</c> when present (else falling back to the invoking stream's resources),
///         and executes the decoded bytes as a nested content stream that inherits the invoking
///         stream's current graphics state and is implicitly bracketed like <c>q</c> ... <c>Q</c>
///         (CTM/color/font-selection mutations made inside the Form never leak back out once
///         <c>Do</c> returns, while path-painting/surface side effects persist, exactly like any
///         other painting operator). The Form's <c>/BBox</c> is never used to clip its content,
///         its <c>/Group</c> (transparency group) entry is never consulted, and a hard
///         recursion-depth limit of 12 nested Form XObject invocations is enforced (a Form
///         that invokes itself, directly or via a longer cycle, beyond that depth throws
///         <see cref="System.IO.InvalidDataException"/> rather than overflowing the native
///         call stack).
///     </para>
///     <para>
///         <strong>Encryption:</strong> opening a document encrypted with the PDF
///         <c>/Filter /Standard</c> security handler is supported for the extremely common
///         real-world case of an <em>empty user password</em> (a document that is merely
///         permission-restricted, not actually password-protected to open): RC4 (40/128-bit,
///         <c>/V 1</c>/<c>/V 2</c>), AES-128 (<c>/V 4</c>, <c>/CFM /AESV2</c>), and AES-256 using
///         the R5 or R6 hardened-hash key derivation (<c>/V 5</c>, <c>/R 5</c> or <c>/R 6</c>, <c>/CFM /AESV3</c>) are all
///         transparently decrypted (see <c>PdfDocument.Encryption.cs</c>'s own ISO 32000-1
///         Algorithm 1/2/4/5, ISO 32000-2 Algorithm 2.A remarks) - a page's content stream,
///         every string, and every other encrypted stream all decrypt before any other parsing
///         logic ever sees their bytes, so the rest of this class needs no awareness that a
///         document was ever encrypted at all. A non-<c>/Standard</c> security handler (for
///         example <c>/Adobe.PubSec</c>), an AES-256 <c>/R</c> other than 5 or 6, a
///         crypt filter other than the standard <c>/StdCF</c> (including <c>/Identity</c>), and a
///         document that genuinely requires a non-empty password (there is no API surface to
///         supply one) all still fail closed with <see cref="UnsupportedImageFeatureException"/>,
///         each with its own distinguishable
///         <see cref="UnsupportedImageFeatureException.Feature"/> token.
///     </para>
///     <para>
///         <strong>Documented scope boundaries</strong> (not currently supported): function-based shadings
///         (<c>/ShadingType</c> 1; types 2-7 are supported by the <c>sh</c>
///         operator and shading patterns), <c>/FunctionType 4</c> PostScript-calculator functions,
///         transparency groups, and Type 3 glyphs shown in the clipping text-rendering modes
///         (<c>Tr</c> 4-7, which do clip outline-based fonts). The <c>sh</c> operator and generic
///         path clipping (<c>W</c>/<c>W*</c>) are both supported. Every other keyword
///         not implemented is silently skipped, not an error. A page with no <c>/Contents</c> at
///         all still renders a <see cref="Surface"/> cleared to
///         <see cref="PdfRenderOptions.BackgroundColor"/> (opaque white by default; see
///         <see cref="Render(int, int, int, PdfRenderOptions?)"/>).
///     </para>
///     <para>
///         <strong>Thread safety</strong>: a <see cref="PdfDocument"/> instance is <em>not</em>
///         thread-safe. <see cref="Render(int, int, int, PdfRenderOptions?)"/> and every other
///         public member lazily populate and read shared per-instance caches (parsed pages,
///         fonts, and resolved object-stream entries) without synchronization, so calling any of
///         them concurrently from multiple threads on the <em>same</em> instance is unsupported
///         and may corrupt those caches or throw. Open one <see cref="PdfDocument"/> instance per
///         thread when concurrent access is needed, or otherwise synchronize all calls to a
///         single shared instance externally.
///     </para>
/// </remarks>
public sealed partial class PdfDocument : IDisposable
{
    /// <summary>
    ///     The entire document's bytes, buffered once at <see cref="Open(System.IO.Stream, string?)"/>/
    ///     <see cref="Open(string, string?)"/> time. Every offset recorded in <see cref="_xref"/> and every
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
    ///     Tracks the object numbers currently being resolved via <see cref="GetObject(int)"/>
    ///     (whether direct or compressed), so a crafted cross-reference table that creates a
    ///     resolution cycle - whether through compressed-object containment (an object stream
    ///     whose container is itself compressed, possibly inside the same stream) or through two
    ///     direct objects whose values reference each other (for example a stream's indirect
    ///     <c>/Length</c> forming a cycle back to the stream itself) - is rejected with an
    ///     <see cref="InvalidDataException"/> instead of recursing indefinitely into an
    ///     uncatchable <see cref="StackOverflowException"/>. Mirrors the page-tree cycle guard in
    ///     <c>TraversePageTree</c>.
    /// </summary>
    private readonly HashSet<int> _objectResolutionStack = new();

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
    /// <param name="password">
    ///     An optional password to authenticate an encrypted document with. When
    ///     <see langword="null"/> (the default), only the empty user password is authenticated -
    ///     byte-for-byte the same behavior as before this parameter existed. When non-null, it is
    ///     tried first as the user password, then - if that does not authenticate - as the owner
    ///     password; see <see cref="InitializeEncryption(PdfObject, string?)"/>'s own remarks for
    ///     the full authentication and password-encoding details. Ignored entirely when the
    ///     document is not encrypted.
    /// </param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the document cannot be parsed at all (no cross-reference data or document
    ///     catalog could be located, even via the linear-scan fallback), or when a required
    ///     structure (trailer, catalog, page tree) is malformed.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the document's trailer declares an <c>/Encrypt</c> entry whose security
    ///     handler, crypt filter, or revision is unsupported (a non-<c>/Standard</c>
    ///     security handler, an AES-256 <c>/R</c> other than 5 or 6, a non-<c>/StdCF</c> crypt filter, a genuinely-required
    ///     password that was not supplied, a supplied password that does not authenticate as
    ///     either the user or the owner password, or a non-ASCII password supplied for an
    ///     <c>/R 2</c>-<c>4</c> document), per the class remarks' encryption scope boundary.
    /// </exception>
    private PdfDocument(byte[] buffer, string? password)
    {
        _buffer = buffer;
        _xref = [];

        PdfObject trailer;
        try
        {
            trailer = ParseCrossReferenceChain();

            // Establish the file decryption key (if any) before resolving /Root: the catalog, or
            // an ancestor in its page tree, may itself live inside a compressed /Type /ObjStm
            // object stream that is only readable once the correct key is known. Validating the
            // catalog before the key exists would decompress ciphertext as if it were plaintext
            // and spuriously fail, triggering the linear-scan fallback below even though the
            // document is otherwise well-formed.
            InitializeEncryption(trailer, password);

            if (!IsValidCatalogRoot(trailer))
            {
                throw new InvalidDataException("Trailer /Root does not resolve to a valid /Catalog.");
            }
        }
        catch (InvalidDataException)
        {
            // Normal cross-reference parsing failed, the /Encrypt dictionary itself was
            // malformed, or /Root resolved to something other than a valid catalog (for example a
            // corrupt/missing startxref, or offsets that do not point at real objects) - fall back
            // to a linear scan for "N G obj" markers, discarding any partially-cached objects
            // resolved against the abandoned cross-reference table or encryption state.
            //
            // The encryption state itself must also be reset here: InitializeEncryption above may
            // already have succeeded and populated _encryptionKey/_encryptionCipher before the
            // later catalog-validation check failed. If the linear-scan fallback's recovered
            // trailer has no /Encrypt entry of its own, InitializeEncryption below returns
            // immediately as a no-op - leaving the stale key from the abandoned attempt in place
            // and causing every object parsed from the fallback path to be incorrectly
            // "decrypted" with a key that does not actually apply to it.
            _objectCache.Clear();
            _encryptionKey = null;
            _encryptionCipher = EncryptionCipher.None;
            trailer = BuildLinearScanFallback(password);
            InitializeEncryption(trailer, password);
        }

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
    /// <param name="password">
    ///     An optional password to authenticate an encrypted document with. When
    ///     <see langword="null"/> (the default), only the empty user password is authenticated -
    ///     byte-for-byte the same behavior as before this parameter existed. When non-null, it is
    ///     tried first as the user password, then as the owner password, against whichever
    ///     security handler the document's <c>/Encrypt</c> entry declares (RC4, AES-128, or
    ///     AES-256 with R5/R6 key derivation). Ignored entirely when the document is not encrypted.
    /// </param>
    /// <returns>A new <see cref="PdfDocument"/> instance representing the parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the document cannot be parsed.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the document's trailer declares an <c>/Encrypt</c> entry whose security
    ///     handler, crypt filter, or revision is unsupported (a non-<c>/Standard</c> security
    ///     handler, an AES-256 <c>/R</c> other than 5 or 6, a non-<c>/StdCF</c> crypt filter, a genuinely-required password
    ///     that was not supplied, a supplied password that does not authenticate as either the
    ///     user or the owner password, or a non-ASCII password supplied for an <c>/R 2</c>-<c>4</c>
    ///     document).
    /// </exception>
    public static PdfDocument Open(Stream stream, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var buffered = new MemoryStream();
        stream.CopyTo(buffered);
        return new PdfDocument(buffered.ToArray(), password);
    }

    /// <summary>
    ///     Opens and parses a PDF document from a file path.
    /// </summary>
    /// <param name="path">The path of the PDF file to read.</param>
    /// <param name="password">
    ///     An optional password to authenticate an encrypted document with. See
    ///     <see cref="Open(Stream, string?)"/>'s own remarks for the full authentication and
    ///     password-encoding details.
    /// </param>
    /// <returns>A new <see cref="PdfDocument"/> instance representing the parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="path"/> is empty or consists only of white space.
    /// </exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the document cannot be parsed.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown for the same reason as <see cref="Open(Stream, string?)"/>.
    /// </exception>
    /// <remarks>
    ///     File-system exceptions (for example <see cref="FileNotFoundException"/>,
    ///     <see cref="DirectoryNotFoundException"/>, <see cref="UnauthorizedAccessException"/>, or
    ///     <see cref="IOException"/>) raised while opening <paramref name="path"/> propagate
    ///     uncaught to the caller.
    /// </remarks>
    public static PdfDocument Open(string path, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return Open(stream, password);
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
    ///     <see cref="PdfDocument"/> class remarks for the recognized operator set and this
    ///     phase's documented feature limitations).
    /// </summary>
    /// <param name="pageIndex">The zero-based index of the page to render.</param>
    /// <param name="width">The width of the rendered surface, in pixels.</param>
    /// <param name="height">The height of the rendered surface, in pixels.</param>
    /// <param name="options">
    ///     Optional page-rendering configuration. When <see langword="null"/> (the default),
    ///     <see cref="PdfRenderOptions.Default"/> is used, which clears the surface to opaque
    ///     white before the page's content is painted.
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/> of the requested <paramref name="width"/> x
    ///     <paramref name="height"/>, painted with the page's interpreted content-stream geometry.
    ///     The surface is first cleared to <paramref name="options"/>'s
    ///     <see cref="PdfRenderOptions.BackgroundColor"/> (opaque white by default), so a page
    ///     with no <c>/Contents</c> renders as a surface filled with that color. Pass
    ///     <c>new PdfRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) }</c> to reproduce
    ///     the fully transparent background this method always produced before this option
    ///     existed.
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
    ///     lexically well-formed, when a recognized operator's operand count/type does not match
    ///     its documented requirement, when a <c>/Subtype /Form</c> XObject's own
    ///     <c>/Matrix</c> is malformed, or when nested <c>/Subtype /Form</c> XObject invocations
    ///     exceed the maximum supported nesting depth.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when a <c>Tf</c> operator selects a font this library does not support (a
    ///     non-<c>TrueType</c> simple font, a font with no embedded <c>/FontFile2</c>, or an
    ///     unrecognized <c>/Encoding</c> base encoding), or when a <c>Tr</c> operator selects a
    ///     defined but unsupported text-rendering mode.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    public Surface Render(int pageIndex, int width, int height, PdfRenderOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (pageIndex < 0 || pageIndex >= _pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, "Page index is out of range.");
        }

        var surface = new Surface(width, height);
        surface.Clear((options ?? PdfRenderOptions.Default).BackgroundColor);
        var pageInfo = _pages[pageIndex];
        var (pageNode, x0, y0, boxWidth, boxHeight, resources) = ResolvePageDetails(pageIndex);
        var baseCtm = BuildBaseCtm(x0, y0, boxWidth, boxHeight, pageInfo.Rotation, width, height);
        var contentBytes = ResolvePageContentBytes(pageNode);
        var resolvedResources = resources is null ? null : Resolve(resources);
        ExecuteContentStream(contentBytes, surface, baseCtm, resolvedResources);
        return surface;
    }

    /// <summary>
    ///     Renders the specified page at a given resolution, preserving the page's own aspect
    ///     ratio.
    /// </summary>
    /// <remarks>
    ///     A convenience wrapper over <see cref="Render(int, int, int, PdfRenderOptions?)"/> for the common case of
    ///     wanting undistorted, uniformly-scaled output: this overload reads the page's
    ///     rotation-adjusted <see cref="PdfPageInfo.Width"/>/<see cref="PdfPageInfo.Height"/> (in
    ///     points, 1/72 inch) via <see cref="GetPageInfo"/>, scales both by <paramref name="dpi"/>
    ///     / 72, and rounds to the nearest pixel before delegating to
    ///     <see cref="Render(int, int, int, PdfRenderOptions?)"/>. Callers needing independent X/Y scaling (for
    ///     example, non-square pixels, or a specific pixel size regardless of aspect ratio) should
    ///     call <see cref="Render(int, int, int, PdfRenderOptions?)"/> directly instead.
    /// </remarks>
    /// <param name="pageIndex">The zero-based index of the page to render.</param>
    /// <param name="dpi">The resolution to render at, in dots (pixels) per inch.</param>
    /// <param name="options">
    ///     Optional page-rendering configuration, forwarded unchanged to
    ///     <see cref="Render(int, int, int, PdfRenderOptions?)"/>. When <see langword="null"/>
    ///     (the default), <see cref="PdfRenderOptions.Default"/> is used, which clears the
    ///     surface to opaque white before the page's content is painted.
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/> sized to the page's aspect ratio at <paramref name="dpi"/>,
    ///     painted with the page's interpreted content-stream geometry. The surface is first
    ///     cleared to <paramref name="options"/>'s <see cref="PdfRenderOptions.BackgroundColor"/>
    ///     (opaque white by default) - see
    ///     <see cref="Render(int, int, int, PdfRenderOptions?)"/> for details, including how to
    ///     reproduce the fully transparent background this method always produced before this
    ///     option existed.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="pageIndex"/> is negative or greater than or equal to
    ///     <see cref="PageCount"/>, when <paramref name="dpi"/> is not a positive, finite number,
    ///     or when the computed pixel width/height is outside <see cref="Surface"/>'s own valid
    ///     dimension range (propagated, unwrapped, from the <see cref="Surface(int, int)"/>
    ///     constructor).
    /// </exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     See <see cref="Render(int, int, int, PdfRenderOptions?)"/>.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     See <see cref="Render(int, int, int, PdfRenderOptions?)"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    public Surface Render(int pageIndex, float dpi, PdfRenderOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!float.IsFinite(dpi) || dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI must be a positive, finite number.");
        }

        var info = GetPageInfo(pageIndex);
        var scale = dpi / 72.0;
        var width = (int)Math.Round(info.Width * scale, MidpointRounding.AwayFromZero);
        var height = (int)Math.Round(info.Height * scale, MidpointRounding.AwayFromZero);
        return Render(pageIndex, width, height, options);
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
