using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio Rttt

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
    /// <returns>The parsed shapes, in document order, excluding any <c>&lt;Shape Del="1"&gt;</c> deleted-child stub (see this method's own remarks).</returns>
    /// <remarks>
    ///     A shape-level <c>Del="1"</c> attribute marks a group-child stub as deleted (distinct
    ///     from <c>VsdxDocument.CellMerge.cs</c>'s <c>MergeGeometryRows</c>, which already
    ///     correctly handles a <em>row</em>-level <c>Del</c> on a geometry <c>&lt;Row&gt;</c> - a
    ///     different construct). Confirmed in <c>60973.vsdx</c>'s <c>page2.xml</c>: a correct
    ///     group child (for example the resolved "AIRttt"/"Location: ttt" card) is followed by
    ///     sibling stubs such as <c>&lt;Shape Del='1' MasterShape='8' ID='4294967295'/&gt;</c>.
    ///     Such a stub carries no useful data of its own (its sentinel <c>ID='4294967295'</c>
    ///     cannot plausibly be a real <c>&lt;Connect&gt;</c> target - see this milestone's own
    ///     plan report) - excluding it from the parsed tree entirely is simpler and more correct
    ///     than threading an <c>IsDeleted</c> flag through <see cref="VsdxShapeNode"/>/
    ///     <c>ResolveShapeRecursive</c>/rendering, and matches Visio's own behavior of never
    ///     resolving/rendering a deleted shape.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the shape tree rooted at <paramref name="shapesElement"/> exceeds
    ///     <c>VsdxDocument.Groups.cs</c>'s <see cref="MaxGroupNestingDepth"/>/
    ///     <see cref="MaxResolvedShapeCount"/> budget - see this class's own parse-time budget
    ///     remarks on <see cref="ParseShapeElement(XElement, int, ref int)"/>.
    /// </exception>
    private static List<VsdxShapeNode> ParseShapeElements(XElement shapesElement)
    {
        var parsedShapeCount = 0;
        return ParseShapeElements(shapesElement, depth: 0, ref parsedShapeCount);
    }

    /// <summary>
    ///     The depth/count-budgeted recursive worker behind
    ///     <see cref="ParseShapeElements(XElement)"/> - see that method's own remarks for the
    ///     parsing algorithm, and <see cref="ParseShapeElement(XElement, int, ref int)"/>'s own
    ///     remarks for why this budget must be enforced during parsing, not only during
    ///     resolution.
    /// </summary>
    /// <param name="shapesElement">A <c>&lt;Shapes&gt;</c> element (a page's own, or a group shape's nested one).</param>
    /// <param name="depth">The nesting depth of <paramref name="shapesElement"/>'s own direct <c>&lt;Shape&gt;</c> children - <c>0</c> for a page's (or a Master's) own top-level shapes.</param>
    /// <param name="parsedShapeCount">A running count of every shape parsed so far across the whole page/Master (top-level and nested combined), threaded through by reference exactly like <c>ResolveShapeRecursive</c>'s own <c>resolvedShapeCount</c>.</param>
    /// <returns>The parsed shapes, in document order, excluding any <c>&lt;Shape Del="1"&gt;</c> deleted-child stub.</returns>
    private static List<VsdxShapeNode> ParseShapeElements(XElement shapesElement, int depth, ref int parsedShapeCount)
    {
        var ns = shapesElement.Name.Namespace;
        var result = new List<VsdxShapeNode>();
        foreach (var shapeElement in shapesElement.Elements(ns + "Shape"))
        {
            if ((string?)shapeElement.Attribute("Del") == "1")
            {
                continue;
            }

            result.Add(ParseShapeElement(shapeElement, depth, ref parsedShapeCount));
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
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="shapeElement"/>'s own shape tree exceeds
    ///     <c>VsdxDocument.Groups.cs</c>'s <see cref="MaxGroupNestingDepth"/>/
    ///     <see cref="MaxResolvedShapeCount"/> budget - see the three-parameter overload's own
    ///     remarks for why this budget must be enforced here too.
    /// </exception>
    internal static VsdxShapeNode ParseShapeElement(XElement shapeElement)
    {
        var parsedShapeCount = 0;
        return ParseShapeElement(shapeElement, depth: 0, ref parsedShapeCount);
    }

    /// <summary>
    ///     The depth/count-budgeted recursive worker behind
    ///     <see cref="ParseShapeElement(XElement)"/>.
    /// </summary>
    /// <remarks>
    ///     PR #42 review round 2 (Finding #1, High): before this fix,
    ///     <see cref="ParseShapeElement(XElement)"/> recursively parsed every nested
    ///     <c>&lt;Shapes&gt;</c> subtree in full before <c>VsdxDocument.Groups.cs</c>'s
    ///     <c>ResolveShapeRecursive</c> ever ran, so its own <see cref="MaxGroupNestingDepth"/>/
    ///     <see cref="MaxResolvedShapeCount"/> budget check - designed specifically to guard
    ///     against a pathological or cyclic shape tree exhausting the call stack or memory - could
    ///     never trip in time: a deeply nested or very wide untrusted page/master's own parse pass
    ///     would already have exhausted the call stack or memory before resolution ever began.
    ///     This worker enforces the identical budget (reusing the very same constants, so the two
    ///     passes can never disagree) during the parse pass itself, threading <paramref name="depth"/>/
    ///     <paramref name="parsedShapeCount"/> through exactly as <c>ResolveShapeRecursive</c>
    ///     threads its own <c>depth</c>/<c>resolvedShapeCount</c>, so a hostile input is rejected
    ///     promptly during parsing rather than only (too late) during resolution.
    /// </remarks>
    /// <param name="shapeElement">The <c>&lt;Shape&gt;</c> element to parse.</param>
    /// <param name="depth"><paramref name="shapeElement"/>'s own nesting depth - <c>0</c> for a page's (or a Master's) own top-level shapes, incrementing by <c>1</c> for each level of nested <c>&lt;Shapes&gt;</c>.</param>
    /// <param name="parsedShapeCount">A running count of every shape parsed so far across the whole page/Master (top-level and nested combined), incremented before each shape's own parse and checked against <see cref="MaxResolvedShapeCount"/>.</param>
    /// <returns>The parsed, unresolved <see cref="VsdxShapeNode"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="depth"/> exceeds <see cref="MaxGroupNestingDepth"/>, or
    ///     <paramref name="parsedShapeCount"/> exceeds <see cref="MaxResolvedShapeCount"/> -
    ///     either budget's violation is treated as a pathological or cyclic shape tree, never
    ///     silently truncated, mirroring <c>ResolveShapeRecursive</c>'s own error contract.
    /// </exception>
    private static VsdxShapeNode ParseShapeElement(XElement shapeElement, int depth, ref int parsedShapeCount)
    {
        if (depth > MaxGroupNestingDepth)
        {
            throw new InvalidDataException(
                $"A shape's nested group tree exceeds the maximum supported group nesting depth of {MaxGroupNestingDepth}; " +
                "its page's (or Master's) shape tree is either pathological or cyclic.");
        }

        parsedShapeCount++;
        if (parsedShapeCount > MaxResolvedShapeCount)
        {
            throw new InvalidDataException(
                $"The page's (or Master's) shape tree exceeds the maximum supported shape count of {MaxResolvedShapeCount}; " +
                "it is either pathological or cyclic.");
        }

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
            : ParseShapeElements(childShapesElement, depth + 1, ref parsedShapeCount);

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

