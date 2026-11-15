using System.Globalization;
using System.Text;
using System.Xml;
using DemaConsulting.CanvasNet.Packaging;
using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     The position bindings of <see cref="SvgFlattener"/>: where the <c>cn:</c> attributes and the coordinates they write
///     are in the text, and the edits that bake the resolved numbers into the copy.
/// </summary>
/// <remarks>
///     <para>
///         A binding (<c>cn:x2y2="#flow node:send"</c>) is resolved by the SVG library in a pass over the drawing whose
///         resolver serves each captured picture with its size and the positions recorded when it was drawn (see
///         <see cref="CaptureResolver"/>), so the numbers are those of the PNG picture that replaces the link, placed by
///         its own <c>image</c> element. Nothing is drawn or evaluated a second time.
///     </para>
///     <para>
///         A resolved binding writes its two numbers into the coordinate attributes (inserted where the binding was when
///         an attribute is missing) and its <c>cn:</c> attribute is removed; when every attribute in the CanvasNet
///         namespace was consumed (and no element uses it), its namespace declarations are removed too. A binding that was
///         not resolved keeps its attribute and the numbers written in the drawing, and is a warning.
///     </para>
/// </remarks>
public static partial class SvgFlattener
{
    /// <summary>
    ///     The namespace of the namespace declarations.
    /// </summary>
    private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

    /// <summary>
    ///     One attribute of an element and where it is in the text.
    /// </summary>
    /// <param name="LocalName">The local name.</param>
    /// <param name="NamespaceUri">The namespace URI; empty for an unprefixed attribute.</param>
    /// <param name="Start">The index of the white space before the name: the attribute is removed from here.</param>
    /// <param name="End">The index after the closing quote.</param>
    /// <param name="ValueStart">The index of the first character of the value.</param>
    /// <param name="ValueLength">The length of the value as written.</param>
    /// <param name="Quote">The quote character of the value.</param>
    internal sealed record AttributeSpan(string LocalName, string NamespaceUri, int Start, int End, int ValueStart, int ValueLength, char Quote);

    /// <summary>
    ///     The binding attributes of a drawing and where they are in the text.
    /// </summary>
    /// <param name="Elements">The attributes of each element that has a CanvasNet attribute, by document-order index (the root is 0).</param>
    /// <param name="Declarations">The <c>xmlns:p</c> declarations of the CanvasNet namespace.</param>
    /// <param name="AttributeCount">How many attributes in the CanvasNet namespace the drawing has.</param>
    /// <param name="UsedByElements">Whether an element is in the CanvasNet namespace (its declaration must then stay).</param>
    internal sealed record BindingSites(
        Dictionary<int, List<AttributeSpan>> Elements,
        List<AttributeSpan> Declarations,
        int AttributeCount,
        bool UsedByElements);

    /// <summary>
    ///     One edit of the text: the characters from <paramref name="Start"/> for <paramref name="Length"/> are replaced.
    /// </summary>
    /// <param name="Start">The index of the first character replaced.</param>
    /// <param name="Length">The number of characters replaced; zero inserts.</param>
    /// <param name="Value">The new text.</param>
    internal readonly record struct Splice(int Start, int Length, string Value);

    /// <summary>
    ///     Finds the attributes of the elements that have an attribute in the CanvasNet namespace, and the declarations of
    ///     the namespace.
    /// </summary>
    /// <param name="bytes">The bytes of the drawing.</param>
    /// <param name="text">The text of the drawing without the byte order mark.</param>
    /// <returns>The sites; empty when the drawing has no binding.</returns>
    /// <exception cref="XmlException">Thrown when the drawing is not well-formed.</exception>
    /// <remarks>
    ///     The elements are counted in the order the XML reader meets their start tags, the order the SVG library numbers
    ///     them in <see cref="SvgPositionBinding.ElementIndex"/>.
    /// </remarks>
    internal static BindingSites FindBindingSites(byte[] bytes, string text)
    {
        var lineStarts = LineStarts(text);
        var sites = new BindingSites([], [], 0, false);
        var count = 0;
        var used = false;
        var index = -1;
        using var stream = new MemoryStream(bytes, false);
        using var reader = SafeXml.CreateReader(stream, MaxInputCharacters);
        var position = (IXmlLineInfo)reader;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            index++;
            used |= reader.NamespaceURI == SvgPositionBinding.Namespace;

            // Only an element with a binding or a declaration of the namespace is located in the text
            var attributes = new List<AttributeSpan>();
            var bound = false;
            for (var more = reader.MoveToFirstAttribute(); more; more = reader.MoveToNextAttribute())
            {
                var span = LocateSpan(text, lineStarts, reader, position);
                attributes.Add(span);
                if (span.NamespaceUri == SvgPositionBinding.Namespace)
                {
                    bound = true;
                    count++;
                }
                else if (span.NamespaceUri == XmlnsNamespace && reader.Value == SvgPositionBinding.Namespace)
                {
                    sites.Declarations.Add(span);
                }
            }

            reader.MoveToElement();
            if (bound)
            {
                sites.Elements[index] = attributes;
            }
        }

        return sites with { AttributeCount = count, UsedByElements = used };
    }

    /// <summary>
    ///     Finds the attribute the reader is on in the raw text, with the white space before it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="lineStarts">The start of each line.</param>
    /// <param name="reader">The reader, on an attribute.</param>
    /// <param name="position">The line information of the reader.</param>
    /// <returns>The attribute and where it is.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the text at the reported position is not the attribute.</exception>
    private static AttributeSpan LocateSpan(string text, List<int> lineStarts, XmlReader reader, IXmlLineInfo position)
    {
        var link = Locate(text, lineStarts, reader, position);
        var nameStart = lineStarts[position.LineNumber - 1] + position.LinePosition - 1;
        var start = nameStart;
        while (start > 0 && text[start - 1] is ' ' or '\t' or '\r' or '\n')
        {
            start--;
        }

        return new AttributeSpan(reader.LocalName, reader.NamespaceURI, start, link.Start + link.Length + 1, link.Start, link.Length, link.Quote);
    }

    /// <summary>
    ///     Builds the edits that bake the resolved bindings into the copy.
    /// </summary>
    /// <param name="sites">The binding attributes of the drawing.</param>
    /// <param name="reports">The outcome of every binding, from the binding pass.</param>
    /// <returns>
    ///     For each resolved binding: the new values of its coordinate attributes, and the removal of the binding attribute
    ///     (replaced by the coordinate attributes that were missing); and the removal of the namespace declarations when
    ///     every CanvasNet attribute was consumed and no element is in the namespace.
    /// </returns>
    private static List<Splice> BindingSplices(BindingSites sites, IReadOnlyList<SvgPositionBinding> reports)
    {
        var splices = new List<Splice>();
        var consumed = 0;
        foreach (var report in reports.Where(r => r.IsResolved))
        {
            if (!sites.Elements.TryGetValue(report.ElementIndex, out var attributes)
                || attributes.Find(a => a.NamespaceUri == SvgPositionBinding.Namespace && a.LocalName == report.Attribute) is not { } binding)
            {
                continue;
            }

            // The existing coordinates get their new value; a missing one takes the place of the binding attribute
            var inserted = new StringBuilder();
            foreach (var (name, value) in new[] { (report.XAttribute!, report.X!.Value), (report.YAttribute!, report.Y!.Value) })
            {
                var number = value.ToString("0.###", CultureInfo.InvariantCulture);
                if (attributes.Find(a => a.NamespaceUri.Length == 0 && a.LocalName == name) is { } existing)
                {
                    splices.Add(new Splice(existing.ValueStart, existing.ValueLength, number));
                }
                else
                {
                    inserted.Append(' ').Append(name).Append('=').Append(binding.Quote).Append(number).Append(binding.Quote);
                }
            }

            splices.Add(new Splice(binding.Start, binding.End - binding.Start, inserted.ToString()));
            consumed++;
        }

        // The declaration goes only when nothing in the drawing uses the namespace any more
        if (consumed > 0 && consumed == sites.AttributeCount && !sites.UsedByElements)
        {
            splices.AddRange(sites.Declarations.Select(d => new Splice(d.Start, d.End - d.Start, string.Empty)));
        }

        return splices;
    }
}
