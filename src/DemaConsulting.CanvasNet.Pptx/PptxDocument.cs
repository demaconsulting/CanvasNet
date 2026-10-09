using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore xfrm prst cust pptx patt

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
///         <strong>Phase 1a</strong> implemented only the underlying OOXML package layer -
///         opening the <c>.pptx</c> file as a ZIP archive, resolving
///         <c>[Content_Types].xml</c> (both its default extension-to-content-type mappings and
///         any part-specific overrides), and resolving package-level (<c>_rels/.rels</c>) and
///         per-part (<c>{dir}/_rels/{partName}.rels</c>) relationships, including relative
///         target resolution (for example <c>../slideLayouts/slideLayout1.xml</c>).
///     </para>
///     <para>
///         <strong>Phase 1b</strong> adds: parsing <c>ppt/presentation.xml</c>
///         (slide size, slide list - see <see cref="SlideCount"/>/<see cref="SlideSize"/>);
///         theme color/font scheme parsing; slide master/layout/slide structural models
///         (placeholder shapes only, not freeform shapes); and an isolated placeholder
///         property-inheritance resolver implementing ECMA-376's placeholder matching
///         algorithm. <see cref="Open(Stream)"/>/<see cref="Open(string)"/> now additionally
///         require the package to be a navigable presentation (a resolvable
///         <c>ppt/presentation.xml</c> part declaring a slide size and at least one slide).
///     </para>
///     <para>
///         <strong>Phase 1c</strong> adds DrawingML shape geometry and paint
///         resolution (<c>PptxDocument.Geometry.cs</c>/<c>PptxDocument.Paint.cs</c>):
///         <c>&lt;a:xfrm&gt;</c> position/rotation/flip transform resolution, <c>&lt;p:grpSp&gt;</c>
///         child-coordinate-space transform composition, preset (<c>&lt;a:prstGeom&gt;</c>) and
///         custom (<c>&lt;a:custGeom&gt;</c>) geometry resolution into the core
///         <see cref="Geometry.Path"/> type, and fill/stroke resolution into core
///         <c>DemaConsulting.CanvasNet.Drawing</c>/<c>DemaConsulting.CanvasNet.Canvas</c> paint
///         types.
///         Freeform (non-placeholder) shape *enumeration* from a slide's full <c>&lt;p:spTree&gt;</c>,
///         font loading, and a rendering surface still do not exist yet - all deferred to a later
///         phase.
///     </para>
///     <para>
///         <strong>Phase 1d (this release)</strong> adds DrawingML text layout and rendering
///         (<c>PptxDocument.Text.cs</c>/<c>PptxDocument.TextInheritance.cs</c>/
///         <c>PptxDocument.TextLayout.cs</c>/<c>PptxDocument.TextRender.cs</c>):
///         <c>&lt;p:txBody&gt;</c>/<c>&lt;a:bodyPr&gt;</c>/<c>&lt;a:p&gt;</c>/<c>&lt;a:pPr&gt;</c>/
///         <c>&lt;a:r&gt;</c>/<c>&lt;a:rPr&gt;</c>/<c>&lt;a:t&gt;</c> structural parsing; an
///         attribute-level run/paragraph property-inheritance resolver walking the
///         placeholder/layout/master <c>&lt;p:txStyles&gt;</c>/theme chain; word-wrap,
///         horizontal alignment, vertical anchor, and a three-tier autofit policy; and a
///         glyph-ink painting primitive reusing the core <see cref="Fonts.TrueTypeFont"/>/
///         <see cref="Fonts.SystemFontCatalog"/> infrastructure. Bullets/numbering, full text
///         justification, <c>spAutoFit</c> shape-resize behavior, kerning, and text clipping on
///         overflow are explicitly deferred.
///     </para>
///     <para>
///         <strong>Phase 1e</strong> adds dedicated <c>&lt;p:pic&gt;</c> picture-shape support
///         (<c>PptxDocument.Images.cs</c>: content-type dispatch, <c>&lt;a:srcRect&gt;</c>
///         crop-rectangle resolution, and y-down nearest-neighbor compositing), <c>&lt;a:tbl&gt;</c>
///         table support (<c>PptxDocument.Tables.cs</c>: structure/cell parsing reusing the Phase
///         1c/1d fill/border/text-body resolvers verbatim, merge-aware cell-rect resolution, and
///         cell fill/border/text painting), and recursive, full shape-tree parsing
///         (<c>PptxDocument.ShapeTree.cs</c>) across a slide's own <c>&lt;p:sp&gt;</c>/
///         <c>&lt;p:pic&gt;</c>/<c>&lt;p:graphicFrame&gt;</c>/<c>&lt;p:grpSp&gt;</c> elements
///         (including nested <c>&lt;p:grpSp&gt;</c> child-transform composition), exposed
///         internally as each slide's own shape tree, with lazy, invoke-on-demand theme
///         resolution so a
///         shape tree with no <c>&lt;p:graphicFrame&gt;</c> never resolves a theme at all.
///         A non-placeholder shape's own background fill via <c>&lt;a:blipFill&gt;</c>/
///         <c>&lt;a:pattFill&gt;</c> inside <c>&lt;p:spPr&gt;</c>, picture effects/shadows, nested
///         tables, table auto-sizing/banding, master/layout full shape-tree enumeration, and a
///         full per-slide public <c>Render</c> API remained deferred.
///     </para>
///     <para>
///         <strong>Phase 1f (this release)</strong> adds the public, slide-level
///         <see cref="Render(int, int, int, PptxRenderOptions?)"/>/
///         <see cref="Render(int, float, PptxRenderOptions?)"/> rendering API
///         (<c>PptxDocument.Render.cs</c>): a recursive, document-order walk of a slide's full,
///         internally-resolved shape tree, composing nested group transforms and dispatching
///         each shape/picture/table node to the already-verified Phase 1c/1d/1e resolvers and
///         painters onto a <see cref="Canvas.Surface"/> of the requested pixel dimensions. This
///         phase's documented deferred items include a slide's own
///         <c>&lt;p:bg&gt;</c> background fill, <c>&lt;p:cxnSp&gt;</c> connector shapes, nested
///         tables, table auto-sizing/banding, group-level style cascading beyond transform
///         composition, and master/layout full shape-tree rendering. With this phase, the
///         planned PPTX 1.0 feature set is complete; any remaining gaps are candidates for a
///         future, corpus-driven hardening pass (<c>pptx-phase-2</c>), not a currently planned
///         phase.
///     </para>
///     <para>
///         <strong>Phase 2 Follow-Up: Slide/Layout/Master Background Fill</strong> (this release)
///         closes the single highest-visual-impact gap left by Phase 1f's own "a slide's own
///         <c>&lt;p:bg&gt;</c> background fill" deferral above: <see cref="Render(int, int, int, PptxRenderOptions?)"/>
///         now resolves and paints a slide's own <c>&lt;p:cSld&gt;/&lt;p:bg&gt;</c> background
///         fill - falling back to its layout's, then its master's, own <c>&lt;p:bg&gt;</c> when
///         the slide declares none, and to <see cref="PptxRenderOptions.BackgroundColor"/> only
///         when none of the three declare one at all - before the shape-tree walk, reusing the
///         existing Phase 1c fill/color resolution pipeline verbatim (<c>PptxDocument.Background.cs</c>).
///         Solid-color fills and theme-indexed <c>&lt;p:bgRef&gt;</c> fills are fully supported
///         (including <c>phClr</c> substitution); a linear gradient background fill is
///         best-effort (inheriting Phase 1c's existing linear-only, non-radial/path gradient
///         boundary); a pattern or picture background fill remains deferred (inheriting
///         <see cref="ResolveFill"/>'s existing <see cref="PptxUnsupportedFeatureException"/>
///         boundary unchanged).
///     </para>
///     <para>
///         <strong>Phase 2 Follow-Up: Bullets and Numbering Rendering</strong> closes the gap left
///         by Phase 1d's own "Bullets/numbering ... are explicitly deferred" boundary above: a
///         paragraph's <c>&lt;a:buChar&gt;</c> character, <c>&lt;a:buAutoNum&gt;</c> auto-number
///         marker, or explicit <c>&lt;a:buNone&gt;</c> absence is now parsed
///         (<c>PptxDocument.Text.cs</c>), resolved through the same four independent
///         placeholder/layout/master choice-group inheritance chains already established for
///         other paragraph properties (<c>PptxDocument.TextInheritance.cs</c>'s
///         <see cref="ResolveEffectiveBulletProperties"/>), formatted into its rendered string for
///         the eleven most common Latin-numeral auto-number schemes
///         (<c>PptxDocument.Bullets.cs</c>'s <see cref="FormatAutoNumber"/>), and measured and
///         painted at the paragraph's own gutter without ever overlapping its text
///         (<c>PptxDocument.TextLayout.cs</c>).
///     </para>
///     <para>
///         <strong>Phase 2 Follow-Up: Connector Shape Rendering</strong> closes the gap left by
///         Phase 1f's own "<c>&lt;p:cxnSp&gt;</c> connector shapes" deferral above: a
///         <c>&lt;p:cxnSp&gt;</c> is now parsed into a <see cref="PptxConnectorShapeNode"/>
///         alongside every other shape-tree node kind (<c>PptxDocument.ShapeTree.cs</c>), and
///         <see cref="Render(int, int, int, PptxRenderOptions?)"/> paints its resolved
///         straight/elbow/curved line - merging its own line style with its style-reference
///         fallback, and orienting an optional stroked or filled arrowhead at either end
///         (<c>PptxDocument.Connectors.cs</c>/<c>PptxDocument.Render.cs</c>'s
///         <c>RenderConnector</c>), with an unrecognized connector preset degrading to "this one
///         connector is invisible" rather than aborting the slide.
///     </para>
///     <para>
///         <strong>Thread safety</strong>: a <see cref="PptxDocument"/> instance is <em>not</em>
///         thread-safe. <see cref="Render(int, int, int, PptxRenderOptions?)"/> and every other
///         public member lazily populate and read shared per-instance caches (parsed slides,
///         layouts, masters, themes, relationships, table styles, and owned picture surfaces)
///         without synchronization, so calling any of them concurrently from multiple threads on
///         the <em>same</em> instance is unsupported and may corrupt those caches, throw, or
///         leak/double-dispose a decoded picture. This mirrors common .NET document/parser types
///         (for example <see cref="System.Xml.Linq.XDocument"/> or <see cref="Stream"/>): open
///         one <see cref="PptxDocument"/> instance per thread (re-opening the same file is cheap
///         relative to rendering) when concurrent access is needed, or otherwise synchronize all
///         calls to a single shared instance externally.
///     </para>
/// </remarks>
public sealed partial class PptxDocument : IDisposable
{
    /// <summary>
    ///     The maximum number of bytes <see cref="Open(Stream)"/> will buffer from a caller-
    ///     supplied stream before rejecting it with <see cref="System.IO.InvalidDataException"/>.
    /// </summary>
    /// <remarks>
    ///     <see cref="Open(Stream)"/> buffers its entire input into memory before any ZIP
    ///     validation occurs (see this type's own remarks); without a bound, a very large, or
    ///     deliberately non-terminating, caller-supplied stream could exhaust memory or buffer
    ///     indefinitely before that validation ever runs. 268,435,456 bytes (256 MiB) mirrors the
    ///     core <see cref="Canvas.Surface.MaxDimension"/>'s own documented "comfortably below
    ///     <see cref="int.MaxValue"/>" byte-count precedent (<c>8192 * 32768 = 268,435,456</c>) -
    ///     large enough for any realistic <c>.pptx</c> package (including one carrying embedded
    ///     video/image media), while remaining small enough to keep worst-case memory use bounded
    ///     to a small, practical amount for an attacker-controlled input.
    /// </remarks>
    private const long MaxPackageBytes = 268_435_456L;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PptxDocument"/> class by parsing the
    ///     supplied, already fully-buffered package bytes.
    /// </summary>
    /// <param name="buffer">
    ///     The complete package bytes. Ownership transfers to this instance - the bytes back the
    ///     non-writable <see cref="_stream"/> for the lifetime of this instance.
    /// </param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when <paramref name="buffer"/> is not a readable ZIP archive, when the
    ///     package is missing the required <c>[Content_Types].xml</c> or <c>_rels/.rels</c>
    ///     parts, or when any required part's XML is malformed.
    /// </exception>
    private PptxDocument(byte[] buffer)
    {
        _stream = new MemoryStream(buffer, writable: false);
        _archive = OpenZipArchive(_stream);
        _entriesByPath = BuildEntryLookup(_archive);
        _defaultContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _overrideContentTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        _relationshipCache = new Dictionary<string, IReadOnlyDictionary<string, PackageRelationship>>(StringComparer.Ordinal);
        _themeCache = new Dictionary<string, PptxTheme>(StringComparer.Ordinal);
        _tableStylesCache = new Dictionary<string, IReadOnlyDictionary<string, XElement>>(StringComparer.Ordinal);
        _masterCache = new Dictionary<string, PptxMaster>(StringComparer.Ordinal);
        _layoutCache = new Dictionary<string, PptxLayout>(StringComparer.Ordinal);
        _slideCache = new Dictionary<int, PptxSlide>();

        InitializePackage();
        InitializePresentation();
    }

    /// <summary>
    ///     Gets the total number of slides declared by the presentation's <c>&lt;p:sldIdLst&gt;</c>.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    public int SlideCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _slidePartPaths.Count;
        }
    }

    /// <summary>
    ///     Gets the presentation's slide size, in EMU (English Metric Units), as declared by
    ///     <c>ppt/presentation.xml</c>'s <c>&lt;p:sldSz&gt;</c> element.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    public PptxSlideSize SlideSize
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _slideSize;
        }
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
    ///     Thrown when the package cannot be parsed - because it is not a valid ZIP/OPC archive,
    ///     is missing a required part (<c>[Content_Types].xml</c>, <c>_rels/.rels</c>, or the
    ///     <c>ppt/presentation.xml</c> part itself), a required part's XML is malformed, or the
    ///     presentation declares no slide size (<c>&lt;p:sldSz&gt;</c>) or no slides
    ///     (<c>&lt;p:sldIdLst&gt;</c>) - or when <paramref name="stream"/> supplies more than
    ///     <see cref="MaxPackageBytes"/> bytes (see <see cref="MaxPackageBytes"/>'s own remarks
    ///     for the bound's rationale).
    /// </exception>
    public static PptxDocument Open(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var buffered = new MemoryStream();
        CopyBounded(
            stream,
            buffered,
            MaxPackageBytes,
            $"The package stream exceeds the maximum supported size of {MaxPackageBytes} bytes.");
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
    ///     Copies <paramref name="source"/> into <paramref name="destination"/>, reading in fixed-
    ///     size chunks and rejecting the input the moment the running total exceeds
    ///     <paramref name="maxBytes"/> - bounding both the worst-case memory this copy can consume
    ///     and the worst-case time it can spend reading a deliberately large or non-terminating
    ///     <paramref name="source"/>, rather than calling <see cref="Stream.CopyTo(Stream)"/>
    ///     unconditionally and discovering the problem only after it has already exhausted memory.
    ///     Shared by <see cref="Open(Stream)"/> (bounding the whole buffered package stream) and
    ///     <see cref="GetPartBytes"/> (bounding a single part's decompressed byte size).
    /// </summary>
    /// <param name="source">The stream to copy from.</param>
    /// <param name="destination">The stream to copy into.</param>
    /// <param name="maxBytes">
    ///     The maximum number of bytes that may be read from <paramref name="source"/> before this
    ///     method throws.
    /// </param>
    /// <param name="exceededMessage">
    ///     The message used to construct the <see cref="System.IO.InvalidDataException"/> thrown
    ///     when <paramref name="maxBytes"/> is exceeded.
    /// </param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown once more than <paramref name="maxBytes"/> bytes have been read from
    ///     <paramref name="source"/>.
    /// </exception>
    private static void CopyBounded(Stream source, MemoryStream destination, long maxBytes, string exceededMessage)
    {
        var buffer = new byte[81920];
        var total = 0L;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new InvalidDataException(exceededMessage);
            }

            destination.Write(buffer, 0, read);
        }
    }

    /// <summary>
    ///     Releases the resources held by this <see cref="PptxDocument"/> (the underlying ZIP
    ///     archive, its backing buffer, and every cached table-cell picture <see cref="Surface"/>
    ///     this document decoded and owns - see <see cref="_ownedImageSurfaces"/>). Calling this
    ///     method more than once has no effect beyond the first call.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var surface in _ownedImageSurfaces)
        {
            surface.Dispose();
        }

        _archive.Dispose();
        _stream.Dispose();
        _disposed = true;
    }
}
