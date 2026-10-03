using System.Globalization;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore nvsppr nvpr sptree pptx

/// <summary>
///     A single placeholder shape (a <c>&lt;p:sp&gt;</c> with a <c>&lt;p:ph&gt;</c> descendant)
///     parsed structurally from a slide master, slide layout, or slide - identical shape across
///     all three levels, matched between levels by
///     <see cref="PptxDocument.ResolvePlaceholderProperties"/>'s resolver.
/// </summary>
/// <param name="Type">
///     The placeholder's <c>&lt;p:ph type="..."/&gt;</c> value, or <c>"obj"</c> (the OOXML schema
///     default) when the attribute is omitted.
/// </param>
/// <param name="Idx">
///     The placeholder's <c>&lt;p:ph idx="..."/&gt;</c> value, or <c>0</c> (the OOXML schema
///     default) when the attribute is omitted.
/// </param>
/// <param name="ShapeElement">
///     The raw <c>&lt;p:sp&gt;</c> element, retained unparsed beyond this shape - later
///     phases/the Inheritance resolver extract individual property fragments (for example
///     <c>&lt;p:spPr&gt;</c>) from it directly rather than this type pre-extracting every
///     possible fragment.
/// </param>
internal sealed record PptxPlaceholder(string Type, uint Idx, XElement ShapeElement);

/// <summary>A parsed slide master: its own part path, its theme's part path, and its placeholder shapes.</summary>
/// <param name="PartPath">The master's own resolved part path.</param>
/// <param name="ThemePartPath">The master's resolved <c>/theme</c> relationship target part path.</param>
/// <param name="Placeholders">The master's immediate placeholder shapes, in document order.</param>
internal sealed record PptxMaster(string PartPath, string ThemePartPath, IReadOnlyList<PptxPlaceholder> Placeholders);

/// <summary>A parsed slide layout: its own part path, its master's part path, and its placeholder shapes.</summary>
/// <param name="PartPath">The layout's own resolved part path.</param>
/// <param name="MasterPartPath">The layout's resolved <c>/slideMaster</c> relationship target part path.</param>
/// <param name="Placeholders">The layout's immediate placeholder shapes, in document order.</param>
internal sealed record PptxLayout(string PartPath, string MasterPartPath, IReadOnlyList<PptxPlaceholder> Placeholders);

/// <summary>A parsed slide: its own part path, its layout's part path, and its placeholder shapes.</summary>
/// <param name="PartPath">The slide's own resolved part path.</param>
/// <param name="LayoutPartPath">The slide's resolved <c>/slideLayout</c> relationship target part path.</param>
/// <param name="Placeholders">
///     The slide's immediate placeholder shapes (direct children of <c>&lt;p:spTree&gt;</c> only -
///     Phase 1b does not recurse into groups), in document order.
/// </param>
internal sealed record PptxSlide(string PartPath, string LayoutPartPath, IReadOnlyList<PptxPlaceholder> Placeholders);

/// <summary>
///     Shared placeholder-shape structural parser, used identically by the master/layout/slide
///     parsers (<see cref="PptxDocument.GetMaster"/>/<see cref="PptxDocument.GetLayout"/>/
///     <see cref="PptxDocument.GetSlide(int)"/>) to avoid duplicating the same XML-shape
///     extraction three times.
/// </summary>
internal static class PptxPlaceholderParser
{
    /// <summary>
    ///     Walks <paramref name="spTreeElement"/>'s immediate <c>&lt;p:sp&gt;</c> children only
    ///     (no group/recursive descent), filters to those with a
    ///     <c>&lt;p:nvSpPr&gt;/&lt;p:nvPr&gt;/&lt;p:ph&gt;</c> descendant, and builds one
    ///     <see cref="PptxPlaceholder"/> per match, applying the <c>type</c>/<c>idx</c> schema
    ///     defaults (<c>"obj"</c>/<c>0</c>) when the corresponding attribute is omitted.
    /// </summary>
    /// <param name="spTreeElement">The <c>&lt;p:spTree&gt;</c> element to walk.</param>
    /// <returns>The placeholder shapes found, in document order.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a <c>&lt;p:ph&gt;</c> element's <c>idx</c> attribute is present but not a
    ///     valid non-negative integer.
    /// </exception>
    internal static IReadOnlyList<PptxPlaceholder> ParsePlaceholderShapes(XElement spTreeElement)
    {
        var placeholders = new List<PptxPlaceholder>();

        foreach (var sp in spTreeElement.Elements(PptxDocument.PresentationNamespace + "sp"))
        {
            var ph = sp.Element(PptxDocument.PresentationNamespace + "nvSpPr")?
                .Element(PptxDocument.PresentationNamespace + "nvPr")?
                .Element(PptxDocument.PresentationNamespace + "ph");
            if (ph is null)
            {
                continue;
            }

            var type = (string?)ph.Attribute("type") ?? "obj";
            var idx = ParseIdx(ph);

            placeholders.Add(new PptxPlaceholder(type, idx, sp));
        }

        return placeholders;
    }

    /// <summary>
    ///     Parses <c>&lt;p:ph&gt;</c>'s <c>idx</c> attribute, defaulting to <c>0</c> when absent.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the <c>idx</c> attribute is present but not a valid non-negative integer.
    /// </exception>
    private static uint ParseIdx(XElement ph)
    {
        var idxValue = (string?)ph.Attribute("idx");
        if (idxValue is null)
        {
            return 0;
        }

        if (!uint.TryParse(idxValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
        {
            throw new InvalidDataException($"A <p:ph> element has an invalid 'idx' attribute value '{idxValue}'.");
        }

        return idx;
    }
}
