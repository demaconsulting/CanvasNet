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
    ///     Walks the document catalog's page tree (<c>/Pages</c> and its recursively-nested
    ///     <c>/Kids</c>), producing an ordered, flattened list of every leaf page's resolved,
    ///     rotation-adjusted <see cref="PdfPageInfo"/>.
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
        TraversePageTree(pagesReference, DefaultMediaBoxWidth, DefaultMediaBoxHeight, 0, visited, pages);
        return pages;
    }

    /// <summary>
    ///     Recursively visits a page-tree node (an intermediate <c>/Type /Pages</c> node or a leaf
    ///     <c>/Type /Page</c>), inheriting <c>/MediaBox</c> and <c>/Rotate</c> from ancestors when
    ///     not declared locally, and appending a <see cref="PdfPageInfo"/> for every leaf page
    ///     encountered, in document order.
    /// </summary>
    private void TraversePageTree(
        PdfObject nodeReference,
        double inheritedWidth,
        double inheritedHeight,
        int inheritedRotation,
        HashSet<int> visited,
        List<PdfPageInfo> pages)
    {
        if (nodeReference.Kind == PdfKind.Reference && !visited.Add(nodeReference.RefNumber))
        {
            throw new InvalidDataException("Page tree contains a reference cycle.");
        }

        var node = Resolve(nodeReference);
        if (node.Kind != PdfKind.Dictionary)
        {
            throw new InvalidDataException("Page tree node is not a dictionary.");
        }

        var (width, height) = ResolveMediaBox(node, inheritedWidth, inheritedHeight);
        var rotation = ResolveRotation(node, inheritedRotation);

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
                TraversePageTree(kid, width, height, rotation, visited, pages);
            }
        }
        else
        {
            var (displayWidth, displayHeight) = rotation is 90 or 270 ? (height, width) : (width, height);
            pages.Add(new PdfPageInfo(
                (int)Math.Round(displayWidth),
                (int)Math.Round(displayHeight),
                rotation));
        }
    }

    /// <summary>
    ///     Resolves a node's own <c>/MediaBox</c> if declared, otherwise the inherited value from
    ///     its nearest declaring ancestor (or the catalog-level US Letter default, if none exists
    ///     anywhere in the ancestry).
    /// </summary>
    private (double Width, double Height) ResolveMediaBox(PdfObject node, double inheritedWidth, double inheritedHeight)
    {
        var mediaBox = node.Get("MediaBox");
        if (mediaBox is null)
        {
            return (inheritedWidth, inheritedHeight);
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

        return (Math.Abs(values[2] - values[0]), Math.Abs(values[3] - values[1]));
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
}
