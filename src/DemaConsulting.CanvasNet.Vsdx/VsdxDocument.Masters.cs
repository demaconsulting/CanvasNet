using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio davehoward

/// <summary>
///     Implements the <see cref="VsdxDocument"/> Master/Stencil resolver: parsing
///     <c>visio/masters/masters.xml</c>'s <c>&lt;Master ID="N"&gt;</c> index, resolving each
///     referenced Master's own content part (<c>visio/masters/masterK.xml</c>) via its
///     <c>&lt;Rel r:id="..."/&gt;</c> child (never by filename convention - confirmed directly
///     against <c>davehoward-test3-house.vsdx</c>/<c>davehoward-test9-rect-and-line.vsdx</c>,
///     both of which resolve <c>Master ID='2'</c> to <c>master1.xml</c>, not <c>master2.xml</c>),
///     and parsing the Master's single top-level <c>&lt;Shape&gt;</c>.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>A cache of each Master ID's own raw (unresolved) top-level shape, populated lazily by <see cref="ResolveMasterShape"/>. <see langword="null"/> once <see cref="_mastersIndex"/> has failed to load at all.</summary>
    private readonly Dictionary<string, VsdxShapeNode?> _masterShapeCache = new(StringComparer.Ordinal);

    /// <summary>A cache of each Master ID's own content part relationship <c>Id</c>, parsed lazily from <c>visio/masters/masters.xml</c> by <see cref="GetMastersIndex"/>.</summary>
    private IReadOnlyDictionary<string, string>? _mastersIndex;

    /// <summary>
    ///     Returns the raw (unresolved) top-level shape declared by the Master whose ID is
    ///     <paramref name="masterId"/>, or <see langword="null"/> when <paramref name="masterId"/>
    ///     is <see langword="null"/>, the package declares no masters at all, no
    ///     <c>&lt;Master ID="{masterId}"&gt;</c> element exists, its content part could not be
    ///     resolved, or its content part declares no top-level <c>&lt;Shape&gt;</c> - every case
    ///     degrading tolerantly (a dangling/unresolvable Master reference is treated the same as
    ///     "no Master", never throwing), consistent with this package's broader never-throw
    ///     convention for recognized-but-unsupported constructs.
    /// </summary>
    /// <param name="masterId">The shape's own <c>Master=</c> attribute value, or <see langword="null"/>.</param>
    /// <returns>The Master's raw top-level shape, or <see langword="null"/>.</returns>
    internal VsdxShapeNode? ResolveMasterShape(string? masterId)
    {
        if (masterId is null)
        {
            return null;
        }

        if (_masterShapeCache.TryGetValue(masterId, out var cached))
        {
            return cached;
        }

        var shape = LoadMasterShape(masterId);
        _masterShapeCache[masterId] = shape;
        return shape;
    }

    /// <summary>Loads and parses the given Master's content part's single top-level shape, with no caching.</summary>
    /// <param name="masterId">The Master's <c>ID=</c> attribute value.</param>
    /// <returns>The Master's raw top-level shape, or <see langword="null"/> when it cannot be resolved.</returns>
    private VsdxShapeNode? LoadMasterShape(string masterId)
    {
        var index = GetMastersIndex();
        if (index is null || !index.TryGetValue(masterId, out var relationshipId))
        {
            return null;
        }

        if (!TryResolveRelationshipById(_mastersPartPath!, relationshipId, out var masterContentPartPath))
        {
            return null;
        }

        XElement root;
        try
        {
            root = LoadPartXmlRoot(masterContentPartPath);
        }
        catch (InvalidDataException)
        {
            return null;
        }

        if (root.Name != VsdxMainNamespace + "MasterContents")
        {
            return null;
        }

        var shapesElement = root.Element(VsdxMainNamespace + "Shapes");
        var topLevelShapeElement = shapesElement?.Element(VsdxMainNamespace + "Shape");
        return topLevelShapeElement is null ? null : ParseShapeElement(topLevelShapeElement);
    }

    /// <summary>
    ///     Parses and caches <c>visio/masters/masters.xml</c>'s <c>&lt;Master ID="N"&gt;</c> index
    ///     into a lookup from Master <c>ID</c> to that Master's own <c>&lt;Rel r:id="..."/&gt;</c>
    ///     child's <c>Id</c> value.
    /// </summary>
    /// <returns>The parsed index, or <see langword="null"/> when the package declares no masters relationship at all.</returns>
    private IReadOnlyDictionary<string, string>? GetMastersIndex()
    {
        if (_mastersIndex is not null)
        {
            return _mastersIndex;
        }

        if (_mastersPartPath is null)
        {
            return null;
        }

        XElement root;
        try
        {
            root = LoadPartXmlRoot(_mastersPartPath);
        }
        catch (InvalidDataException)
        {
            return null;
        }

        if (root.Name != VsdxMainNamespace + "Masters")
        {
            return null;
        }

        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var masterElement in root.Elements(VsdxMainNamespace + "Master"))
        {
            var id = (string?)masterElement.Attribute("ID");
            var relId = (string?)masterElement.Element(VsdxMainNamespace + "Rel")?.Attribute(VsdxRelationshipsNamespace + "id");
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(relId))
            {
                index[id] = relId;
            }
        }

        _mastersIndex = index;
        return index;
    }
}
