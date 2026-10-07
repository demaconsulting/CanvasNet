using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     Implements the <see cref="VsdxDocument"/> page shape-tree parser and resolver: locating a
///     page's content part (<c>visio/pages/pageN.xml</c>), parsing its <c>&lt;Shapes&gt;</c> tree
///     into <see cref="VsdxShapeNode"/> instances, parsing its sibling <c>&lt;Connects&gt;</c>
///     section and attaching each entry to its connector shape, and resolving every shape in the
///     tree - top-level and arbitrarily nested group children alike (Master/MasterShape cell and
///     geometry-row merge, StyleSheet chain walk, transform, paint, 1-D connector endpoints) - see
///     <c>VsdxDocument.CellMerge.cs</c>/<c>VsdxDocument.Styles.cs</c>/
///     <c>VsdxDocument.Geometry.cs</c>/<c>VsdxDocument.Transform.cs</c>/
///     <c>VsdxDocument.Paint.cs</c>/<c>VsdxDocument.Connects.cs</c>/
///     <c>VsdxDocument.Arrowheads.cs</c>/<c>VsdxDocument.Groups.cs</c> for each resolution
///     concern's own implementation (the recursive, nested-group-aware resolver itself -
///     <c>ResolveShapeRecursive</c> - lives in <c>VsdxDocument.Groups.cs</c>, alongside its own
///     depth/count budget guards).
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>A per-page-index cache of the page's fully parsed and resolved top-level shapes, populated lazily by <see cref="GetPageShapes"/>.</summary>
    private readonly Dictionary<int, IReadOnlyList<VsdxShapeNode>> _resolvedPageShapesCache = new();

    /// <summary>
    ///     Returns every top-level shape declared directly under the given page's
    ///     <c>&lt;Shapes&gt;</c> element, fully resolved (Master/MasterShape cell and geometry
    ///     merge, StyleSheet chain walk, transform, paint - see this class's own remarks for the
    ///     exact set of concerns resolved). Each shape's own nested children (see
    ///     <see cref="VsdxShapeNode.Children"/>) are recursively resolved to arbitrary nesting
    ///     depth as well - see <c>VsdxDocument.Groups.cs</c>'s <c>ResolveShapeRecursive</c>.
    /// </summary>
    /// <param name="pageIndex">The zero-based page index, in <c>[0, PageCount)</c>.</param>
    /// <returns>The page's resolved top-level shapes, in document order.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this document has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside <c>[0, PageCount)</c>.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the page's content part cannot be resolved or is not well-formed XML, its
    ///     root element is not a <c>&lt;PageContents&gt;</c> element, or the page's shape tree
    ///     exceeds <c>VsdxDocument.Groups.cs</c>'s own group-nesting depth/resolved-shape-count
    ///     budget.
    /// </exception>
    internal IReadOnlyList<VsdxShapeNode> GetPageShapes(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (pageIndex < 0 || pageIndex >= _pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, $"Page index must be in [0, {_pages.Count}).");
        }

        if (_resolvedPageShapesCache.TryGetValue(pageIndex, out var cached))
        {
            return cached;
        }

        var contentPartPath = GetPageContentPartPath(pageIndex);
        var root = LoadPartXmlRoot(contentPartPath);
        if (root.Name != VsdxMainNamespace + "PageContents")
        {
            throw new InvalidDataException($"The page content part '{contentPartPath}' is not a PageContents part.");
        }

        var shapesElement = root.Element(VsdxMainNamespace + "Shapes");
        var rawShapes = shapesElement is null
            ? []
            : ParseShapeElements(shapesElement);

        var connectsElement = root.Element(VsdxMainNamespace + "Connects");
        var connects = ParseConnectsElement(connectsElement);
        AttachConnectsToShapes(rawShapes, connects);

        var resolved = new List<VsdxShapeNode>(rawShapes.Count);
        var resolvedShapeCount = 0;
        foreach (var shape in rawShapes)
        {
            ResolveShapeRecursive(shape, ResolveMasterShape(shape.MasterId), parent: null, depth: 0, ref resolvedShapeCount);
            resolved.Add(shape);
        }

        _resolvedPageShapesCache[pageIndex] = resolved;
        return resolved;
    }

    /// <summary>
    ///     Recursively parses every direct <c>&lt;Shape&gt;</c> child of
    ///     <paramref name="shapesElement"/> into a raw (unresolved) <see cref="VsdxShapeNode"/>,
    ///     including each shape's own nested <c>&lt;Shapes&gt;</c> descendants.
    /// </summary>
    /// <param name="shapesElement">A <c>&lt;Shapes&gt;</c> element (a page's own, or a group shape's nested one).</param>
    /// <returns>The parsed shapes, in document order.</returns>
    private static List<VsdxShapeNode> ParseShapeElements(XElement shapesElement)
    {
        var ns = shapesElement.Name.Namespace;
        var result = new List<VsdxShapeNode>();
        foreach (var shapeElement in shapesElement.Elements(ns + "Shape"))
        {
            result.Add(ParseShapeElement(shapeElement));
        }

        return result;
    }

    /// <summary>
    ///     Parses a single <c>&lt;Shape&gt;</c> element (its identity/master/style attributes, its
    ///     own direct <c>&lt;Cell&gt;</c>/<c>&lt;Section N="Geometry"&gt;</c> children, and any
    ///     nested <c>&lt;Shapes&gt;</c> descendants) into a raw <see cref="VsdxShapeNode"/>.
    /// </summary>
    /// <param name="shapeElement">The <c>&lt;Shape&gt;</c> element to parse.</param>
    /// <returns>The parsed, unresolved <see cref="VsdxShapeNode"/>.</returns>
    internal static VsdxShapeNode ParseShapeElement(XElement shapeElement)
    {
        var ns = shapeElement.Name.Namespace;

        var id = (string?)shapeElement.Attribute("ID") ?? string.Empty;
        var type = (string?)shapeElement.Attribute("Type") ?? string.Empty;
        var masterId = (string?)shapeElement.Attribute("Master");
        var masterShapeId = (string?)shapeElement.Attribute("MasterShape");
        var lineStyleId = (string?)shapeElement.Attribute("LineStyle");
        var fillStyleId = (string?)shapeElement.Attribute("FillStyle");
        var textStyleId = (string?)shapeElement.Attribute("TextStyle");

        var rawCells = VsdxCellBag.Parse(shapeElement);

        var geometrySections = new List<VsdxGeometrySectionRaw>();
        foreach (var sectionElement in shapeElement.Elements(ns + "Section"))
        {
            if ((string?)sectionElement.Attribute("N") != "Geometry")
            {
                continue;
            }

            var section = VsdxGeometrySectionRaw.Parse(sectionElement);
            if (section is not null)
            {
                geometrySections.Add(section);
            }
        }

        var rawText = ParseTextElement(shapeElement.Element(ns + "Text"));
        var rawCharacterRows = ParseTextSectionRows(shapeElement, "Character");
        var rawParagraphRows = ParseTextSectionRows(shapeElement, "Paragraph");

        var childShapesElement = shapeElement.Element(ns + "Shapes");
        var children = childShapesElement is null
            ? (IReadOnlyList<VsdxShapeNode>)[]
            : ParseShapeElements(childShapesElement);

        return new VsdxShapeNode(
            id,
            type,
            masterId,
            masterShapeId,
            lineStyleId,
            fillStyleId,
            textStyleId,
            rawCells,
            geometrySections,
            rawText,
            rawCharacterRows,
            rawParagraphRows,
            children);
    }
}
