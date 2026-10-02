using System.Numerics;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     The PDF specification's own documented default page size (US Letter, in points), used
    ///     when no <c>/MediaBox</c> is declared anywhere in a page's ancestry.
    /// </summary>
    private const double DefaultMediaBoxWidth = 612;

    private const double DefaultMediaBoxHeight = 792;

    /// <summary>
    ///     Every page's leaf <see cref="PdfObject"/> node and raw (pre-rotation-swap)
    ///     <c>/MediaBox</c> extent, in document order, parallel to <see cref="_pages"/> - built
    ///     once, in lock-step with <see cref="_pages"/>, by <see cref="BuildPageList(PdfObject)"/>.
    ///     Kept separate from the public <see cref="PdfPageInfo"/> shape (rather than added to
    ///     it) since a page's raw node/box origin is an internal rendering detail, not part of
    ///     this package's public page-info contract.
    /// </summary>
    private readonly List<(PdfObject Node, double X0, double Y0, double BoxWidth, double BoxHeight, PdfObject? Resources)> _pageDetails = [];

    /// <summary>
    ///     The maximum page-tree nesting depth (<c>/Pages</c> nodes recursing through
    ///     <c>/Kids</c>) <see cref="TraversePageTree"/> allows before failing closed, bounding
    ///     a chain of distinct, never-repeated nodes that the cycle-detecting <c>visited</c>
    ///     set alone cannot catch, and which could otherwise exhaust the native C# call stack.
    /// </summary>
    private const int MaxPageTreeNestingDepth = 32;

    /// <summary>
    ///     The current page-tree recursion depth, incremented/decremented around every
    ///     <see cref="TraversePageTree"/> call.
    /// </summary>
    private int _pageTreeNestingDepth;

    /// <summary>
    ///     Walks the document catalog's page tree (<c>/Pages</c> and its recursively-nested
    ///     <c>/Kids</c>), producing an ordered, flattened list of every leaf page's resolved,
    ///     rotation-adjusted <see cref="PdfPageInfo"/>, and populating <see cref="_pageDetails"/>
    ///     in lock-step.
    /// </summary>
    /// <param name="trailer">The document's trailer dictionary.</param>
    /// <returns>The document's pages, in document order.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the catalog, page tree, or any page's <c>/MediaBox</c>/<c>/Rotate</c> is
    ///     missing or malformed, or when the page tree contains a reference cycle.
    /// </exception>
    private IReadOnlyList<PdfPageInfo> BuildPageList(PdfObject trailer)
    {
        var rootReference = trailer.Get("Root") ?? throw new InvalidDataException("Trailer is missing /Root.");
        var catalog = Resolve(rootReference);
        if (catalog.Kind != PdfKind.Dictionary || GetNameValue(catalog, "Type") != "Catalog")
        {
            throw new InvalidDataException("Document catalog is missing or malformed.");
        }

        var pagesReference = catalog.Get("Pages") ?? throw new InvalidDataException("Document catalog is missing /Pages.");

        var pages = new List<PdfPageInfo>();
        var visited = new HashSet<int>();
        TraversePageTree(pagesReference, 0, 0, DefaultMediaBoxWidth, DefaultMediaBoxHeight, 0, null, visited, pages);
        return pages;
    }

    /// <summary>
    ///     Recursively visits a page-tree node (an intermediate <c>/Type /Pages</c> node or a leaf
    ///     <c>/Type /Page</c>), inheriting <c>/MediaBox</c>, <c>/Rotate</c>, and <c>/Resources</c>
    ///     from ancestors when not declared locally, and appending a <see cref="PdfPageInfo"/>
    ///     (plus its parallel <see cref="_pageDetails"/> entry) for every leaf page encountered,
    ///     in document order.
    /// </summary>
    private void TraversePageTree(
        PdfObject nodeReference,
        double inheritedX0,
        double inheritedY0,
        double inheritedWidth,
        double inheritedHeight,
        int inheritedRotation,
        PdfObject? inheritedResources,
        HashSet<int> visited,
        List<PdfPageInfo> pages)
    {
        if (nodeReference.Kind == PdfKind.Reference && !visited.Add(nodeReference.RefNumber))
        {
            throw new InvalidDataException("Page tree contains a reference cycle.");
        }

        if (_pageTreeNestingDepth >= MaxPageTreeNestingDepth)
        {
            throw new InvalidDataException(
                $"Page tree nesting exceeds the maximum supported depth of {MaxPageTreeNestingDepth}.");
        }

        _pageTreeNestingDepth++;
        try
        {
            var node = Resolve(nodeReference);
            if (node.Kind != PdfKind.Dictionary)
            {
                throw new InvalidDataException("Page tree node is not a dictionary.");
            }

            var (x0, y0, width, height) = ResolveMediaBox(node, inheritedX0, inheritedY0, inheritedWidth, inheritedHeight);
            var rotation = ResolveRotation(node, inheritedRotation);
            var resources = node.Get("Resources") ?? inheritedResources;

            var type = GetNameValue(node, "Type");
            var kids = node.Get("Kids");
            if (type == "Pages" || (type is null && kids is not null))
            {
                if (kids is not { Kind: PdfKind.Array })
                {
                    throw new InvalidDataException("/Pages node is missing a /Kids array.");
                }

                foreach (var kid in kids.Items)
                {
                    TraversePageTree(kid, x0, y0, width, height, rotation, resources, visited, pages);
                }
            }
            else
            {
                var (displayWidth, displayHeight) = rotation is 90 or 270 ? (height, width) : (width, height);
                pages.Add(new PdfPageInfo(
                    (int)Math.Round(displayWidth),
                    (int)Math.Round(displayHeight),
                    rotation));
                _pageDetails.Add((node, x0, y0, width, height, resources));
            }
        }
        finally
        {
            _pageTreeNestingDepth--;
        }
    }

    /// <summary>
    ///     Resolves a node's own <c>/MediaBox</c> if declared, otherwise the inherited value from
    ///     its nearest declaring ancestor (or the catalog-level US Letter default, if none exists
    ///     anywhere in the ancestry).
    /// </summary>
    /// <returns>
    ///     The raw box's lower-left corner (<c>X0</c>/<c>Y0</c>) and extent
    ///     (<c>Width</c>/<c>Height</c>), <em>before</em> any rotation-driven width/height swap.
    /// </returns>
    private (double X0, double Y0, double Width, double Height) ResolveMediaBox(
        PdfObject node,
        double inheritedX0,
        double inheritedY0,
        double inheritedWidth,
        double inheritedHeight)
    {
        var mediaBox = node.Get("MediaBox");
        if (mediaBox is null)
        {
            return (inheritedX0, inheritedY0, inheritedWidth, inheritedHeight);
        }

        var resolved = Resolve(mediaBox);
        if (resolved.Kind != PdfKind.Array || resolved.Items.Count != 4)
        {
            throw new InvalidDataException("/MediaBox must be an array of four numbers.");
        }

        var values = new double[4];
        for (var i = 0; i < 4; i++)
        {
            var entry = Resolve(resolved.Items[i]);
            values[i] = entry.Kind == PdfKind.Number
                ? entry.Number
                : throw new InvalidDataException("/MediaBox entries must be numbers.");
        }

        // A conforming /MediaBox lists its corners as [llx lly urx ury], but this class - like
        // most lenient readers - does not require the first pair to already be the lower-left
        // corner; the true origin is always the componentwise minimum.
        var x0 = Math.Min(values[0], values[2]);
        var y0 = Math.Min(values[1], values[3]);
        return (x0, y0, Math.Abs(values[2] - values[0]), Math.Abs(values[3] - values[1]));
    }

    /// <summary>
    ///     Resolves a node's own <c>/Rotate</c> if declared, otherwise the inherited value from
    ///     its nearest declaring ancestor (or <c>0</c>, if none exists anywhere in the ancestry),
    ///     normalized modulo 360 into <c>{0, 90, 180, 270}</c>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a declared <c>/Rotate</c> value is not a multiple of 90 degrees.
    /// </exception>
    private int ResolveRotation(PdfObject node, int inherited)
    {
        var rotate = node.Get("Rotate");
        if (rotate is null)
        {
            return inherited;
        }

        var resolved = Resolve(rotate);
        if (resolved.Kind != PdfKind.Number)
        {
            throw new InvalidDataException("/Rotate must be a number.");
        }

        var value = (int)resolved.Number;
        var normalized = ((value % 360) + 360) % 360;
        if (normalized % 90 != 0)
        {
            throw new InvalidDataException("/Rotate must be a multiple of 90 degrees.");
        }

        return normalized;
    }

    /// <summary>
    ///     Gets the given page's leaf <see cref="PdfObject"/> node, raw (pre-rotation-swap)
    ///     <c>/MediaBox</c> extent, and inherited <c>/Resources</c> dictionary, previously
    ///     captured by <see cref="TraversePageTree"/> during construction.
    /// </summary>
    /// <param name="pageIndex">The zero-based page index. Must already be range-validated by the caller.</param>
    /// <returns>The page's leaf node, raw <c>/MediaBox</c> extent, and inherited <c>/Resources</c>.</returns>
    private (PdfObject Node, double X0, double Y0, double BoxWidth, double BoxHeight, PdfObject? Resources) ResolvePageDetails(int pageIndex) =>
        _pageDetails[pageIndex];

    /// <summary>
    ///     Builds the initial content-stream current transformation matrix (CTM) for a page,
    ///     mapping PDF user-space points (relative to the raw, un-rotated <c>/MediaBox</c>) to
    ///     device pixel-space points (origin top-left, x right, y down, extent
    ///     <paramref name="width"/> x <paramref name="height"/> pixels) - see
    ///     <c>.agent-logs/planning-pdf-document-phase2-9b2e6f41.md</c>'s CTM derivation for the
    ///     full corner-mapping proof this formula was checked against.
    /// </summary>
    /// <param name="x0">The raw <c>/MediaBox</c>'s lower-left corner X coordinate.</param>
    /// <param name="y0">The raw <c>/MediaBox</c>'s lower-left corner Y coordinate.</param>
    /// <param name="boxWidth">The raw (pre-rotation-swap) <c>/MediaBox</c> width.</param>
    /// <param name="boxHeight">The raw (pre-rotation-swap) <c>/MediaBox</c> height.</param>
    /// <param name="rotation">The page's normalized effective rotation: <c>0</c>, <c>90</c>, <c>180</c>, or <c>270</c>.</param>
    /// <param name="width">The caller-requested render width, in pixels.</param>
    /// <param name="height">The caller-requested render height, in pixels.</param>
    /// <returns>The base CTM, before any content-stream <c>cm</c> operator is composed onto it.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="rotation"/> is not one of <c>0</c>/<c>90</c>/<c>180</c>/<c>270</c>
    ///     - unreachable in practice, since <see cref="ResolveRotation"/> already guarantees this,
    ///     but retained as a defensive guard against a future internal caller passing an
    ///     unnormalized value.
    /// </exception>
    private static Matrix3x2 BuildBaseCtm(
        double x0,
        double y0,
        double boxWidth,
        double boxHeight,
        int rotation,
        int width,
        int height)
    {
        // Step A: exact-multiples-of-90-degrees rotation+flip, built from literal {0, +-1}
        // components rather than Matrix3x2.CreateRotation, to avoid floating-point trig noise
        // (cos(90 degrees) ~ 6.12e-17) for an operation that must be exact. Each case also flips
        // the PDF's y-up user space into device's y-down pixel space, and translates the rotated
        // box into [0, displayWidth] x [0, displayHeight].
        var (rotationFlip, displayWidth, displayHeight) = rotation switch
        {
            0 => (new Matrix3x2(1, 0, 0, -1, 0, (float)boxHeight), boxWidth, boxHeight),
            90 => (new Matrix3x2(0, 1, 1, 0, 0, 0), boxHeight, boxWidth),
            180 => (new Matrix3x2(-1, 0, 0, 1, (float)boxWidth, 0), boxWidth, boxHeight),
            270 => (new Matrix3x2(0, -1, -1, 0, (float)boxHeight, (float)boxWidth), boxHeight, boxWidth),
            _ => throw new InvalidOperationException($"Unreachable: unnormalized rotation {rotation}."),
        };

        // Step B: independent X/Y stretch mapping the page-point display size to the caller's
        // requested pixel size - Render's own documented contract uses width/height exactly as
        // given, never clamped or aspect-corrected.
        var scaleX = displayWidth == 0 ? 1f : (float)(width / displayWidth);
        var scaleY = displayHeight == 0 ? 1f : (float)(height / displayHeight);
        var scale = Matrix3x2.CreateScale(scaleX, scaleY);

        return Matrix3x2.CreateTranslation((float)-x0, (float)-y0) * rotationFlip * scale;
    }
}
