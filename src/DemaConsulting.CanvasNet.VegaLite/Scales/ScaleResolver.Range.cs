// Portions derived from Vega-Lite and Vega (BSD-3-Clause): scale ranges, named ranges and the cardinality of discretizing scales; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <content>
///     The ranges of the scales of the non-position channels and the explicit ranges of all (a partial file of the scale resolver unit).
/// </content>
/// <remarks>
///     <para>
///         <b>Numbers.</b> A <c>size</c>, <c>strokeWidth</c>, opacity or <c>angle</c> scale maps to the range of its channel (Vega-Lite's
///         <c>defaultRange</c>: size by mark, <c>strokeWidth</c> from <c>minStrokeWidth</c> to <c>maxStrokeWidth</c>, opacities from <c>minOpacity</c> to
///         <c>maxOpacity</c>, <c>angle</c> 0 to 360); a discretizing size scale (quantile, quantize, threshold) spreads the numbers evenly
///         (<c>interpolateRange</c>) over as many values as it has intervals (<c>config.scale.quantileCount</c> and <c>quantizeCount</c>, 4; one more than the thresholds), the
///         other numeric channels keep their two-entry range. <b>Shapes and dashes.</b> A <c>shape</c> scale takes the <c>symbol</c> list of
///         <c>config.range</c>; a <c>strokeDash</c> scale five fixed patterns. <b>Named ranges.</b> A string <c>scale.range</c> names an entry
///         of <c>config.range</c> (Vega's <c>parseScaleRange</c>); a name the configuration does not have is a specification error, as
///         in Vega.
///     </para>
///     <para>
///         <b>Limits.</b> A range, a domain or a count that would hold more than <see cref="VegaLiteLimits.MaxScaleDomainSize"/> entries is
///         refused before anything is built, with a located <see cref="VegaLiteRenderException"/>; the explicit lists are bounded by the size
///         of the specification and checked by their length, the generated ones by their count (a cancellation check every 4,096 values).
///     </para>
/// </remarks>
internal static partial class ScaleResolver
{
    /// <summary>The names of the symbols of Vega's default <c>config.range.symbol</c> (used when a configuration removes it).</summary>
    private static readonly string[] DefaultSymbols =
        ["circle", "square", "triangle-up", "cross", "diamond", "triangle-right", "triangle-down", "triangle-left"];

    /// <summary>The five dash patterns Vega-Lite gives a <c>strokeDash</c> scale.</summary>
    private static readonly double[][] DefaultDashes = [[1, 0], [4, 2], [2, 1], [1, 1], [1, 2, 4, 2]];

    /// <summary>
    ///     Tells whether a shape name is one of the symbols that the painter draws.
    /// </summary>
    /// <param name="name">The shape name or path.</param>
    /// <returns><see langword="true"/> for a name the painter draws; <see langword="false"/> for any other text (a custom SVG path), which is drawn as a circle.</returns>
    internal static bool IsKnownShape(string name) => Scenegraph.ScenePainter.IsSymbolShape(name);

    /// <summary>
    ///     What one scale resolves to besides its domain: the output values of the channel.
    /// </summary>
    private sealed record Outputs
    {
        /// <summary>Gets the explicit numeric range of the specification, or null.</summary>
        public IReadOnlyList<RangeEnd>? ExplicitRange { get; init; }

        /// <summary>Gets the default numeric range of a continuous, band or point scale, or null.</summary>
        public (double Start, double End)? ValueRange { get; init; }

        /// <summary>Gets the numbers of an ordinal or discretizing scale.</summary>
        public IReadOnlyList<double> Numbers { get; init; } = [];

        /// <summary>Gets the colors of an ordinal or discretizing scale.</summary>
        public IReadOnlyList<string> Colors { get; init; } = [];

        /// <summary>Gets the shapes.</summary>
        public IReadOnlyList<string> Shapes { get; init; } = [];

        /// <summary>Gets the dash patterns.</summary>
        public IReadOnlyList<IReadOnlyList<double>> Dashes { get; init; } = [];

        /// <summary>Gets the interpolator of a sequential or diverging scale, or null.</summary>
        public Func<double, RgbColor>? Interpolator { get; init; }
    }

    /// <summary>
    ///     One entry of a range list: its value and where it is written.
    /// </summary>
    /// <param name="Node">The value.</param>
    /// <param name="Location">The location of the entry (of the scale's <c>range</c> when the list comes from the configuration).</param>
    private readonly record struct RangeItem(ConfigNode Node, VegaLiteLocation Location);

    /// <summary>
    ///     The range a scale names through its own <c>range</c> property.
    /// </summary>
    /// <param name="Items">The entries of a list (an explicit array, or a list of <c>config.range</c>), or null.</param>
    /// <param name="Scheme">The scheme object of a named range of <c>config.range</c> (<c>scheme</c>, <c>count</c>, <c>extent</c>), or null.</param>
    /// <param name="Location">The location of the <c>range</c> property.</param>
    private sealed record RangeSpec(IReadOnlyList<RangeItem>? Items, ConfigNode? Scheme, VegaLiteLocation Location);

    /// <summary>
    ///     Finds the output values of a scale, by the group of its channel.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="kind">The kind of scale.</param>
    /// <param name="domainSize">The size of the domain: the categories of an ordinal scale, the thresholds of a threshold scale, the edges of a bin-ordinal scale.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The outputs; null when the channel has no scale (a size on a mark without sizes).</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the range or the scheme of the specification is wrong; carries the location.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when a limit is exceeded; carries the location.</exception>
    private static Outputs? ResolveOutputs(CommonInputs input, ScaleKind kind, int domainSize, Context context)
    {
        switch (GroupOf(input.Channel))
        {
            case ChannelGroup.Position:
                return new Outputs { ExplicitRange = ReadRange(input, context, 2) };
            case ChannelGroup.Color:
                return ColorOutputs(input, kind, domainSize, context);
            case ChannelGroup.Discrete:
                return input.Channel == "shape" ? ShapeOutputs(input, context) : DashOutputs(input, context);
            default:
                return NumericOutputs(input, kind, domainSize, context);
        }
    }

    /// <summary>
    ///     Finds the output values of a size, <c>strokeWidth</c>, opacity or <c>angle</c> scale.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="kind">The kind of scale.</param>
    /// <param name="domainSize">The size of the domain.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The outputs; null when the mark has no such channel (a size on a mark without sizes).</returns>
    private static Outputs? NumericOutputs(CommonInputs input, ScaleKind kind, int domainSize, Context context)
    {
        if (ValueRangeFor(input.Channel, context) is not { } valueRange)
        {
            return null;
        }

        // Linear, band and point scales map to positions of a numeric range; the others pick one number per category or interval
        var explicitRange = ReadRange(input, context, kind is ScaleKind.Linear or ScaleKind.Band or ScaleKind.Point ? 2 : 1);
        if (kind is ScaleKind.Linear or ScaleKind.Band or ScaleKind.Point)
        {
            return new Outputs { ExplicitRange = explicitRange, ValueRange = valueRange };
        }

        List<double> numbers;
        if (explicitRange is not null)
        {
            numbers = [.. explicitRange.Select(entry => entry.IsExtent ? 0 : entry.Value)];
        }
        else if (input.Channel == "size" && kind is ScaleKind.Quantile or ScaleKind.Quantize or ScaleKind.Threshold)
        {
            numbers = InterpolateRange(valueRange.Start, valueRange.End, DiscretizingCount(kind, domainSize, context), input, context);
        }
        else
        {
            numbers = [valueRange.Start, valueRange.End];
        }

        if (ReverseOf(input, kind))
        {
            numbers.Reverse();
        }

        return new Outputs { Numbers = numbers };
    }

    /// <summary>
    ///     Gets the default numeric range of a non-position channel.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The first and the last value; null for a size on a mark without sizes.</returns>
    private static (double Start, double End)? ValueRangeFor(string channel, Context context) => channel switch
    {
        "size" => SizeRange(context),
        "strokeWidth" => (context.Config.GetNumber("scale.minStrokeWidth") ?? 1, context.Config.GetNumber("scale.maxStrokeWidth") ?? 4),
        "opacity" or "fillOpacity" or "strokeOpacity" => (context.Config.GetNumber("scale.minOpacity") ?? 0.3, context.Config.GetNumber("scale.maxOpacity") ?? 0.8),
        "angle" => (0, 360),
        _ => null,
    };

    /// <summary>
    ///     Tells whether a scale runs backwards: <c>reverse</c>, which for a scale with a continuous domain is also set by the sort <c>descending</c>.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="kind">The kind of scale.</param>
    /// <returns><see langword="true"/> when the range is reversed.</returns>
    private static bool ReverseOf(CommonInputs input, ScaleKind kind)
    {
        var explicitReverse = input.ScaleDef?["reverse"]?.AsBool();
        var continuousDomain = kind is ScaleKind.Linear or ScaleKind.Sequential or ScaleKind.Diverging or ScaleKind.Quantize or ScaleKind.Quantile or ScaleKind.Threshold;
        return explicitReverse ?? (continuousDomain && input.Definition["sort"]?.AsString() == "descending");
    }

    /// <summary>
    ///     Works out how many output values a discretizing scale has by default (Vega-Lite's <c>defaultContinuousToDiscreteCount</c>).
    /// </summary>
    /// <param name="kind">Quantile, quantize or threshold.</param>
    /// <param name="domainSize">The number of thresholds of a threshold scale.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The count: <c>quantileCount</c> or <c>quantizeCount</c> (4), or one more than the thresholds.</returns>
    private static double DiscretizingCount(ScaleKind kind, int domainSize, Context context) => kind switch
    {
        ScaleKind.Quantile => context.Config.GetNumber("scale.quantileCount") ?? 4,
        ScaleKind.Quantize => context.Config.GetNumber("scale.quantizeCount") ?? 4,
        _ => domainSize + 1d,
    };

    /// <summary>
    ///     Spreads numbers evenly from a start to an end (Vega-Lite's <c>interpolateRange</c>, <c>sequence(min, max + step, step)</c>).
    /// </summary>
    /// <param name="start">The first number.</param>
    /// <param name="end">The last number.</param>
    /// <param name="count">The number of values; a count that is not a whole number counts rounded up (as <c>sequence</c> does), and below one gives the start only.</param>
    /// <param name="input">The channel facts (for the location of a failure).</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The numbers; a single one is the start.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the count is more than <see cref="VegaLiteLimits.MaxScaleDomainSize"/>.</exception>
    private static List<double> InterpolateRange(double start, double end, double count, CommonInputs input, Context context)
    {
        if (!(count <= context.Limits.MaxScaleDomainSize))
        {
            throw new VegaLiteRenderException(
                $"The {input.Channel} scale would have {count} values, more than the limit of {context.Limits.MaxScaleDomainSize} (VegaLiteLimits.MaxScaleDomainSize).",
                input.Location);
        }

        var n = (int)Math.Ceiling(count);
        var values = new List<double>(Math.Max(1, n));
        if (n <= 1)
        {
            values.Add(start);
            return values;
        }

        var step = (end - start) / (n - 1);
        for (var i = 0; i < n; i++)
        {
            if (i % CancellationInterval == 0)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
            }

            values.Add(start + (i * step));
        }

        return values;
    }

    /// <summary>
    ///     Finds the shapes of a <c>shape</c> scale: the scale's <c>range</c>, else the <c>symbol</c> list of <c>config.range</c>.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The outputs.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the range is not a list of strings.</exception>
    private static Outputs ShapeOutputs(CommonInputs input, Context context)
    {
        var spec = ReadRangeSpec(input, context);
        IReadOnlyList<RangeItem> items;
        if (spec?.Items is { } listed)
        {
            items = listed;
        }
        else
        {
            var symbols = context.Config.Get("range")?["symbol"];
            if (symbols is { Kind: ConfigNodeKind.Array })
            {
                CheckConfigArray(symbols, input.Location, input, context, "config.range.symbol");
                items = [.. symbols.Items.Select(item => new RangeItem(item, input.Location))];
            }
            else
            {
                items = [.. DefaultSymbols.Select(name => new RangeItem(ConfigNode.FromString(name), input.Location))];
            }
        }

        var shapes = new List<string>(items.Count);
        foreach (var item in items)
        {
            if (item.Node.AsString() is not { } name)
            {
                throw new VegaLiteSpecException("The range of a shape scale must be a list of shape names or SVG path strings.", item.Location);
            }

            shapes.Add(name);
        }

        if (ReverseOf(input, ScaleKind.Ordinal))
        {
            shapes.Reverse();
        }

        return new Outputs { Shapes = shapes };
    }

    /// <summary>
    ///     Finds the dash patterns of a <c>strokeDash</c> scale: the scale's <c>range</c>, else five fixed patterns.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The outputs.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the range is not a list of lists of numbers.</exception>
    private static Outputs DashOutputs(CommonInputs input, Context context)
    {
        var spec = ReadRangeSpec(input, context);
        List<IReadOnlyList<double>> dashes = [];
        if (spec?.Items is { } items)
        {
            foreach (var item in items)
            {
                if (item.Node is not { Kind: ConfigNodeKind.Array } list || list.Items.Any(n => n.AsNumber() is null))
                {
                    throw new VegaLiteSpecException("The range of a strokeDash scale must be a list of lists of numbers.", item.Location);
                }

                if (list.Items.Count > context.Limits.MaxScaleDomainSize)
                {
                    throw new VegaLiteRenderException(
                        $"A dash pattern of the {input.Channel} scale lists {list.Items.Count} entries, more than the limit of {context.Limits.MaxScaleDomainSize} (VegaLiteLimits.MaxScaleDomainSize).",
                        item.Location);
                }

                dashes.Add([.. list.Items.Select(n => n.AsNumber()!.Value)]);
            }
        }
        else
        {
            dashes.AddRange(DefaultDashes);
        }

        if (ReverseOf(input, ScaleKind.Ordinal))
        {
            dashes.Reverse();
        }

        return new Outputs { Dashes = dashes };
    }

    /// <summary>
    ///     Reads an explicit <c>scale.range</c> of numbers and the keywords <c>width</c> and <c>height</c>, or the list a configured name stands for.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts (for the source name).</param>
    /// <param name="minimum">The fewest entries the scale needs: two for a continuous, band or point scale, otherwise one.</param>
    /// <returns>The ends of the range; null when the range is absent or is not a list (for example a field range).</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the range has an entry that is not a number or a keyword, or too few entries; carries the location.</exception>
    private static IReadOnlyList<RangeEnd>? ReadRange(CommonInputs input, Context context, int minimum)
    {
        // The keywords of a position: the whole width or height
        if (input.ScaleDef?["range"] is { IsString: true, StringValue: "width" or "height" })
        {
            return [new RangeEnd(0, false), new RangeEnd(0, true)];
        }

        if (ReadRangeSpec(input, context) is not { Items: { } items } spec)
        {
            return null;
        }

        var ends = new List<RangeEnd>(items.Count);
        foreach (var item in items)
        {
            if (item.Node.AsNumber() is { } number)
            {
                ends.Add(new RangeEnd(number, false));
            }
            else if (item.Node.AsString() is "width" or "height")
            {
                ends.Add(new RangeEnd(0, true));
            }
            else
            {
                throw new VegaLiteSpecException("The scale range must be an array of numbers (or the keywords \"width\" and \"height\").", item.Location);
            }
        }

        if (ends.Count < minimum)
        {
            throw new VegaLiteSpecException(
                minimum == 1 ? "The scale range needs at least one value." : "The scale range needs at least two values.",
                spec.Location);
        }

        return ends;
    }

    /// <summary>
    ///     Reads the <c>range</c> of the scale: an array, or the name of an entry of <c>config.range</c> (Vega's <c>parseScaleRange</c>).
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The list or scheme; null when the scale has no <c>range</c> or it is an object (a field range, which is not drawn).</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the name is not an entry of <c>config.range</c>.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the array has more than <see cref="VegaLiteLimits.MaxScaleDomainSize"/> entries.</exception>
    private static RangeSpec? ReadRangeSpec(CommonInputs input, Context context)
    {
        var node = input.ScaleDef?["range"];
        if (node is null)
        {
            return null;
        }

        var location = node.GetLocation(context.SourceName);
        if (node.IsArray)
        {
            CheckSpecArray(node, input, context, "range");
            return new RangeSpec([.. node.Items.Select(item => new RangeItem(ConfigNode.FromSpec(item), item.GetLocation(context.SourceName)))], null, location);
        }

        if (!node.IsString)
        {
            return null;
        }

        var name = node.StringValue!;
        var named = context.Config.Get("range")?[name];
        if (named is { Kind: ConfigNodeKind.Array })
        {
            CheckConfigArray(named, location, input, context, $"config.range.{name}");
            return new RangeSpec([.. named.Items.Select(item => new RangeItem(item, location))], null, location);
        }

        if (named is { Kind: ConfigNodeKind.Object })
        {
            return new RangeSpec(null, named, location);
        }

        throw new VegaLiteSpecException($"Unrecognized scale range value '{name}': config.range has no list or scheme of that name.", location);
    }

    /// <summary>
    ///     Checks the length of an array of the specification (a domain, a range) against <see cref="VegaLiteLimits.MaxScaleDomainSize"/>
    ///     before anything is built from it.
    /// </summary>
    /// <param name="array">The array.</param>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <param name="what">What the array is, for the message.</param>
    /// <exception cref="VegaLiteRenderException">Thrown, located at the array, when it is too long.</exception>
    private static void CheckSpecArray(SpecNode array, CommonInputs input, Context context, string what)
    {
        if (array.Items.Count > context.Limits.MaxScaleDomainSize)
        {
            throw new VegaLiteRenderException(
                $"The {what} of the {input.Channel} scale lists {array.Items.Count} entries, more than the limit of {context.Limits.MaxScaleDomainSize} (VegaLiteLimits.MaxScaleDomainSize).",
                array.GetLocation(context.SourceName));
        }
    }

    /// <summary>
    ///     Checks the length of an array of the configuration (a named range, a scheme list, a dash pattern) against
    ///     <see cref="VegaLiteLimits.MaxScaleDomainSize"/> before anything is built from it, as <see cref="CheckSpecArray"/> does for the specification.
    /// </summary>
    /// <param name="array">The array.</param>
    /// <param name="location">Where the configured value is used: the <c>scale.range</c> that names it, or the channel when it is a default.</param>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <param name="what">What the array is, for the message.</param>
    /// <exception cref="VegaLiteRenderException">Thrown, located at <paramref name="location"/>, when it is too long.</exception>
    private static void CheckConfigArray(ConfigNode array, VegaLiteLocation location, CommonInputs input, Context context, string what)
    {
        if (array.Items.Count > context.Limits.MaxScaleDomainSize)
        {
            throw new VegaLiteRenderException(
                $"The configured {what} of the {input.Channel} scale lists {array.Items.Count} entries, more than the limit of {context.Limits.MaxScaleDomainSize} (VegaLiteLimits.MaxScaleDomainSize).",
                location.IsKnown ? location : input.Location);
        }
    }
}
