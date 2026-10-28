namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     A mark type found in a specification together with where it was written.
/// </summary>
/// <param name="Type">The mark type as written (for example <c>bar</c>, or the <c>type</c> of a mark definition object).</param>
/// <param name="Location">The location of the <c>mark</c> value.</param>
internal sealed record SpecMarkEntry(string Type, VegaLiteLocation Location);

/// <summary>
///     The descriptive metadata of a specification that can be read from its tree alone: what the public properties of
///     <see cref="VegaLiteDocument"/> report.
/// </summary>
/// <remarks>
///     The metadata describes the text only: nothing is evaluated, no data is read and no URL is contacted. Entries are
///     in document order (sorted by position in the text), which for the views of a composition means the order in which
///     the constructs are written, not the order in which the views are nested. The record is immutable.
/// </remarks>
/// <param name="SchemaUrl">The <c>$schema</c> value as written, or null.</param>
/// <param name="SchemaMajorVersion">The Vega-Lite major version named by the schema URL, or null.</param>
/// <param name="Title">The plain text of the top-level title, or null.</param>
/// <param name="Marks">The marks of every view in document order.</param>
/// <param name="DataReferences">The data sources of every view in document order.</param>
/// <param name="ParameterNames">The distinct parameter names in document order.</param>
internal sealed record SpecMetadata(
    string? SchemaUrl,
    int? SchemaMajorVersion,
    string? Title,
    IReadOnlyList<SpecMarkEntry> Marks,
    IReadOnlyList<VegaLiteDataReference> DataReferences,
    IReadOnlyList<string> ParameterNames)
{
    /// <summary>
    ///     Reads the metadata of a parsed specification.
    /// </summary>
    /// <param name="tree">The parsed specification. Must not be null.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tree"/> is null.</exception>
    public static SpecMetadata Read(SpecTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var root = tree.Root;
        var schema = root["$schema"]?.StringValue;

        // Collect the entries of every view with the node they came from, then order them by position in the text
        var marks = new List<(SpecNode Node, SpecMarkEntry Entry)>();
        var data = new List<(SpecNode Node, VegaLiteDataReference Reference)>();
        var parameters = new List<(SpecNode Node, string Name)>();
        foreach (var view in tree.EnumerateViews())
        {
            if (view["mark"] is { } mark && (mark.IsString ? mark.StringValue : mark["type"]?.StringValue) is { } type)
            {
                marks.Add((mark, new SpecMarkEntry(type, tree.Locate(mark))));
            }

            if (view["data"] is { IsObject: true } dataNode && ReadDataReference(tree, dataNode) is { } reference)
            {
                data.Add((dataNode, reference));
            }

            if (view["params"] is { IsArray: true } list)
            {
                parameters.AddRange(list.Items.Where(p => p["name"]?.StringValue is not null).Select(p => (p, p["name"]!.StringValue!)));
            }
        }

        // Several views may declare the same parameter; the first declaration in the text names it
        var names = parameters
            .OrderBy(p => p.Node.Line)
            .ThenBy(p => p.Node.Column)
            .Select(p => p.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new SpecMetadata(
            schema,
            schema is null ? null : SpecParser.ParseSchemaMajorVersion(schema),
            ReadTitle(root["title"]),
            marks.OrderBy(m => m.Node.Line).ThenBy(m => m.Node.Column).Select(m => m.Entry).ToList().AsReadOnly(),
            data.OrderBy(d => d.Node.Line).ThenBy(d => d.Node.Column).Select(d => d.Reference).ToList().AsReadOnly(),
            names.AsReadOnly());
    }

    /// <summary>
    ///     Reads the data source that a <c>data</c> object names.
    /// </summary>
    /// <remarks>
    ///     Inline values take precedence over a url, a sequence and a name, because a named inline data set is inline
    ///     data. A data object that names no source (for example only a <c>format</c>) has no reference.
    /// </remarks>
    /// <param name="tree">The specification.</param>
    /// <param name="data">The <c>data</c> object.</param>
    /// <returns>The reference, or null when the object names no source.</returns>
    private static VegaLiteDataReference? ReadDataReference(SpecTree tree, SpecNode data)
    {
        var location = tree.Locate(data);
        if (data["values"] is { } values)
        {
            return new VegaLiteDataReference(VegaLiteDataKind.Inline, null, values.IsArray ? values.Items.Count : null, location);
        }

        if (data["url"] is { IsString: true } url)
        {
            return new VegaLiteDataReference(VegaLiteDataKind.Url, url.StringValue, null, location);
        }

        if (data["sequence"] is { IsObject: true } sequence)
        {
            return new VegaLiteDataReference(VegaLiteDataKind.Sequence, null, SequenceLength(sequence), location);
        }

        return data["name"] is { IsString: true }
            ? new VegaLiteDataReference(VegaLiteDataKind.Name, null, null, location)
            : null;
    }

    /// <summary>
    ///     Computes how many values a <c>sequence</c> generates.
    /// </summary>
    /// <remarks>
    ///     Vega's sequence runs from <c>start</c> (inclusive) to <c>stop</c> (exclusive) in steps of <c>step</c>
    ///     (default 1), so the length is the rounded-up quotient, never negative.
    /// </remarks>
    /// <param name="sequence">The sequence object.</param>
    /// <returns>The length, or null when it cannot be told (a zero step, or a length beyond the range of a 64-bit integer).</returns>
    private static long? SequenceLength(SpecNode sequence)
    {
        var start = sequence["start"]?.AsNumber();
        var stop = sequence["stop"]?.AsNumber();
        var step = sequence["step"]?.AsNumber() ?? 1;
        if (start is null || stop is null || step == 0 || !double.IsFinite(step))
        {
            return null;
        }

        var length = Math.Ceiling((stop.Value - start.Value) / step);
        if (length < 0)
        {
            return 0;
        }

        return length < 9e18 ? (long)length : null;
    }

    /// <summary>
    ///     Reads the plain text of a title: a string, an array of strings joined with line feeds, or the <c>text</c> of
    ///     a title definition.
    /// </summary>
    /// <param name="title">The <c>title</c> value, or null.</param>
    /// <returns>The text, or null when there is none or it is an expression reference (which cannot be evaluated here).</returns>
    private static string? ReadTitle(SpecNode? title)
    {
        if (title is null)
        {
            return null;
        }

        return title.Kind switch
        {
            SpecNodeKind.String => title.StringValue,
            SpecNodeKind.Array => string.Join('\n', title.Items.Select(i => i.StringValue)),
            SpecNodeKind.Object => title["text"] is { } text && !text.IsObject ? ReadTitle(text) : null,
            _ => null,
        };
    }
}
