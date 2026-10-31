// Portions derived from Vega-Lite and Vega (BSD-3-Clause), d3-scale and d3-interpolate (ISC); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <content>
///     The colors of the scales of <c>color</c>, <c>fill</c> and <c>stroke</c> and the domain of a quantile scale (a partial file of the
///     scale resolver unit).
/// </content>
/// <remarks>
///     <para>
///         <b>Where the colors come from.</b> In Vega-Lite's order: the <c>range</c> of the scale (an array of colors, or the name of an entry
///         of <c>config.range</c>), then its <c>scheme</c> (a name, an array of colors, or an object with <c>name</c>, <c>count</c> and <c>extent</c>),
///         then the named range of the configuration that Vega-Lite's <c>defaultRange</c> picks: <c>category</c> (a nominal field) or <c>ordinal</c> for an
///         ordinal scale; <c>diverging</c> when the scale has a <c>domainMid</c>, <c>heatmap</c> for the <c>rect</c> mark and <c>ramp</c> for a continuous
///         or discretizing one. A named range is a list of colors or a scheme object (<c>scheme</c>, <c>count</c>, <c>extent</c>); a scheme name that is not one of the
///         68 schemes of <see cref="ColorSchemes"/> is a <see cref="VegaLiteSpecException"/> ("Unrecognized scheme name"), as in Vega.
///     </para>
///     <para>
///         <b>What they become.</b> A sequential or diverging scale gets an interpolator over 0 to 1: a scheme through
///         <see cref="ColorSchemes.Configure"/> (its <c>extent</c> and the reversal included; <c>interpolate</c> does not apply to a named scheme, as in Vega), a
///         list of colors piecewise through a <see cref="ColorRamp"/> in the color space that <c>interpolate</c> names (sRGB by default; with
///         the optional <c>gamma</c> of sRGB). An ordinal, quantile, quantize, threshold or bin-ordinal scale gets a list of colors: the
///         list given, or the colors <see cref="ColorSchemes.Configure"/> picks (a categorical scheme whole for an ordinal scale, otherwise the first
///         colors of it, or samples of a continuous one at <c>i / (count + 1)</c>).
///     </para>
///     <para>
///         <b>Invalid colors.</b> A list of colors (an explicit or configured <c>range</c>, a <c>scheme</c> list, or a default named range) that is empty or
///         has an entry that is not a string or not a color the renderer can read is a located <see cref="VegaLiteSpecException"/> (at the entry; at the
///         list when it is empty), as an unknown scheme name is. There is no fallback to the default range, in the lenient mode as in the strict one:
///         the mistake would otherwise go unseen, and a color that cannot be read is never handed to the painter. A configured scheme object with a
///         <c>count</c> that is not a positive whole number or an <c>extent</c> that is not two numbers is reported the same way, as the parser does for a
///         scheme of the specification.
///     </para>
///     <para>
///         <b>Limits and cancellation.</b> A scheme or a list never gives more than <see cref="VegaLiteLimits.MaxScaleDomainSize"/> colors (an
///         explicit <c>count</c> above it is a located <see cref="VegaLiteRenderException"/>); the list of colors of a range is checked by its
///         length first (a list of the configuration, a named range or a scheme list, as much as one of the specification, and reported at the place
///         that uses it); the values of a quantile domain are counted against <see cref="TableLayout.MaxCells"/> before they are read, the work (one step per
///         64 values read, and one per 64 comparisons of the sort) is charged to the evaluation-step budget of the call, and the cancellation token is polled
///         while they are read and sorted.
///     </para>
/// </remarks>
internal static partial class ScaleResolver
{
    /// <summary>The number of values between two cancellation checks while a quantile domain is sorted.</summary>
    private const int SortPollInterval = 1024;

    /// <summary>
    ///     The scheme a color scale was told to use: a name (or a list of colors) with the options of a scheme object.
    /// </summary>
    /// <param name="Name">The scheme name, or null when <paramref name="Colors"/> is used.</param>
    /// <param name="Colors">An array of colors given as the scheme, or null.</param>
    /// <param name="Count">The number of colors, or null.</param>
    /// <param name="Extent">The part of a continuous scheme to use, or null for all of it.</param>
    /// <param name="Location">Where the scheme is written (the scale's <c>scheme</c>, or its channel when a configuration names it).</param>
    private sealed record SchemeChoice(string? Name, IReadOnlyList<RangeItem>? Colors, int? Count, (double Start, double End)? Extent, VegaLiteLocation Location);

    /// <summary>
    ///     Finds the colors of the scale of a <c>color</c>, <c>fill</c> or <c>stroke</c> channel.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="kind">The kind of scale: sequential, diverging, ordinal, quantile, quantize, threshold or bin-ordinal.</param>
    /// <param name="domainSize">The size of the domain.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The outputs: an interpolator for a sequential or diverging scale, otherwise the list of colors.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when a scheme name is unknown or a color cannot be read; carries the location.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the scheme would give more colors than the limit; carries the location.</exception>
    private static Outputs ColorOutputs(CommonInputs input, ScaleKind kind, int domainSize, Context context)
    {
        var reverse = ReverseOf(input, kind);
        var continuous = kind is ScaleKind.Sequential or ScaleKind.Diverging;

        // What the range, the scheme or the default named range of the configuration says
        IReadOnlyList<RangeItem>? colors = null;
        SchemeChoice? scheme = null;
        var where = input.Location;
        var spec = ReadRangeSpec(input, context);
        if (spec?.Items is { } listed)
        {
            colors = listed;
            where = spec.Location;
        }
        else if (spec?.Scheme is { } configuredScheme)
        {
            (colors, scheme) = FromSchemeObject(configuredScheme, spec.Location, input, context);
            where = spec.Location;
        }
        else if (input.ScaleDef?["scheme"] is { } schemeNode)
        {
            (colors, scheme) = FromSpecScheme(schemeNode, context);
            where = schemeNode.GetLocation(context.SourceName);
        }
        else
        {
            (colors, scheme) = FromSchemeObject(DefaultRange(input, kind, context), input.Location, input, context);
        }

        // A list of colors is reported when it is empty or has an entry that is not a color, wherever it comes from; there is no fallback to the default range
        if (colors is not null)
        {
            CheckColors(colors, where);
        }

        if (continuous)
        {
            return new Outputs { Interpolator = ContinuousColors(input, colors, scheme, reverse, context) };
        }

        List<string> texts;
        if (colors is not null)
        {
            texts = [.. colors.Select(item => item.Node.AsString()!)];
            if (reverse)
            {
                texts.Reverse();
            }
        }
        else
        {
            texts = [.. Configure(scheme!, DiscreteUse(kind), domainSize, reverse, input, context).ToCssList()];
        }

        return new Outputs { Colors = texts };
    }

    /// <summary>
    ///     Maps a discrete scale kind to the way a scheme is used for it.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The use.</returns>
    private static SchemeUse DiscreteUse(ScaleKind kind) => kind switch
    {
        ScaleKind.Quantile => SchemeUse.Quantile,
        ScaleKind.Quantize => SchemeUse.Quantize,
        ScaleKind.Threshold => SchemeUse.Threshold,
        ScaleKind.BinOrdinal => SchemeUse.BinOrdinal,
        _ => SchemeUse.Ordinal,
    };

    /// <summary>
    ///     Picks the named range of <c>config.range</c> that Vega-Lite's <c>defaultRange</c> gives a color scale.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="kind">The kind of scale.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The list or scheme object of the configuration; Vega's own default when the configuration has removed it.</returns>
    private static ConfigNode DefaultRange(CommonInputs input, ScaleKind kind, Context context)
    {
        string name;
        if (kind == ScaleKind.Ordinal)
        {
            // Only nominal data uses the categorical palette by default
            name = input.Type == "nominal" ? "category" : "ordinal";
        }
        else if (input.ScaleDef?["domainMid"] is not null)
        {
            name = "diverging";
        }
        else
        {
            name = context.MarkType == "rect" ? "heatmap" : "ramp";
        }

        if (context.Config.Get("range")?[name] is { Kind: ConfigNodeKind.Array or ConfigNodeKind.Object } configured)
        {
            return configured;
        }

        // Vega's own values, in case a configuration has set the range to something else
        return name switch
        {
            "category" => ConfigNode.FromString(ColorSchemes.Tableau10Name),
            "heatmap" => ConfigNode.FromString("yellowgreenblue"),
            "diverging" => ConfigNode.FromString("blueorange"),
            _ => ConfigNode.FromString("blues"),
        };
    }

    /// <summary>
    ///     Reads the scheme of a named range of <c>config.range</c>: a list of colors, a scheme object, or (for Vega's built-in fallback) a scheme name.
    /// </summary>
    /// <param name="node">The named range.</param>
    /// <param name="location">The location to report.</param>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The list of colors, or the scheme.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the <c>count</c> or the <c>extent</c> is malformed; carries the location.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when a list of colors has more entries than the limit; carries the location.</exception>
    private static (IReadOnlyList<RangeItem>? Colors, SchemeChoice? Scheme) FromSchemeObject(ConfigNode node, VegaLiteLocation location, CommonInputs input, Context context)
    {
        if (node.Kind == ConfigNodeKind.Array)
        {
            CheckConfigArray(node, location, input, context, "range of colors");
            return ([.. node.Items.Select(item => new RangeItem(item, location))], null);
        }

        if (node.AsString() is { } plainName)
        {
            return (null, new SchemeChoice(plainName, null, null, null, location));
        }

        // The configuration is not checked by the parser as a scheme of the specification is: the same rules apply here, at the place that uses it
        (double, double)? extent = null;
        if (node["extent"] is { } extentNode)
        {
            extent = extentNode is { Kind: ConfigNodeKind.Array, Items.Count: 2 } e && e.Items[0].AsNumber() is { } a && e.Items[1].AsNumber() is { } b
                ? (a, b)
                : throw new VegaLiteSpecException("The 'extent' of a configured color scheme must be an array of two numbers.", location);
        }

        int? count = null;
        if (node["count"] is { } countNode)
        {
            count = countNode.AsNumber() is { } n && n >= 1 && double.IsInteger(n)
                ? (int)Math.Min(n, int.MaxValue)
                : throw new VegaLiteSpecException("The 'count' of a configured color scheme must be a positive whole number.", location);
        }

        if (node["scheme"] is { Kind: ConfigNodeKind.Array } list)
        {
            CheckConfigArray(list, location, input, context, "scheme of colors");
            return ([.. list.Items.Select(item => new RangeItem(item, location))], null);
        }

        return (null, new SchemeChoice(node["scheme"]?.AsString(), null, count, extent, location));
    }

    /// <summary>
    ///     Reads the <c>scheme</c> of a scale in the specification.
    /// </summary>
    /// <param name="node">The <c>scheme</c> value: a name, an array of colors or an object.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The list of colors, or the scheme.</returns>
    private static (IReadOnlyList<RangeItem>? Colors, SchemeChoice? Scheme) FromSpecScheme(SpecNode node, Context context)
    {
        var location = node.GetLocation(context.SourceName);
        if (node.IsArray)
        {
            return ([.. node.Items.Select(item => new RangeItem(ConfigNode.FromSpec(item), item.GetLocation(context.SourceName)))], null);
        }

        if (node.IsString)
        {
            return (null, new SchemeChoice(node.StringValue, null, null, null, location));
        }

        // The parser has checked the shape of a scheme object: a name, a positive whole count and an extent of two numbers
        var extent = node["extent"] is { IsArray: true, Items.Count: 2 } e && e.Items[0].AsNumber() is { } a && e.Items[1].AsNumber() is { } b ? (a, b) : ((double, double)?)null;
        int? count = node["count"]?.AsNumber() is { } n and >= 1 ? (int)Math.Min(n, int.MaxValue) : null;
        return (null, new SchemeChoice(node["name"]?.AsString(), null, count, extent, node["name"]?.GetLocation(context.SourceName) ?? location));
    }

    /// <summary>
    ///     Checks a list of colors: it must not be empty and every entry must be a color the renderer can read.
    /// </summary>
    /// <remarks>
    ///     A failure is a <see cref="VegaLiteSpecException"/> (strict or lenient alike, as an unknown scheme name is): the list is not
    ///     replaced by the default range, because that would hide the mistake, and a color that cannot be read is never handed to the painter.
    /// </remarks>
    /// <param name="colors">The list.</param>
    /// <param name="location">Where the list is written; reports an empty list.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the entry, when it is not a color; at the list when it is empty.</exception>
    private static void CheckColors(IReadOnlyList<RangeItem> colors, VegaLiteLocation location)
    {
        if (colors.Count == 0)
        {
            throw new VegaLiteSpecException("A color scale needs at least one color.", location);
        }

        foreach (var item in colors)
        {
            if (item.Node.AsString() is not { } text)
            {
                throw new VegaLiteSpecException($"The range of a color scale must be a list of colors; an entry of kind {item.Node.Kind} is not a color.", item.Location);
            }

            if (!CssColor.TryParse(text, out _))
            {
                throw new VegaLiteSpecException($"'{text}' is not a color.", item.Location);
            }
        }
    }

    /// <summary>
    ///     Looks up a scheme of the catalog.
    /// </summary>
    /// <param name="choice">The scheme the scale asked for.</param>
    /// <returns>The scheme.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the name is not one of the schemes.</exception>
    private static ColorScheme LookUp(SchemeChoice choice) =>
        ColorSchemes.TryGetColorScheme(choice.Name, out var found)
            ? found
            : throw new VegaLiteSpecException($"Unrecognized scheme name '{choice.Name}'.", choice.Location);

    /// <summary>
    ///     Builds the interpolator of a sequential or diverging scale.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="colors">The list of colors, or null.</param>
    /// <param name="scheme">The scheme, or null.</param>
    /// <param name="reverse">Whether the colors run backwards.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The interpolator over 0 to 1.</returns>
    private static Func<double, RgbColor> ContinuousColors(CommonInputs input, IReadOnlyList<RangeItem>? colors, SchemeChoice? scheme, bool reverse, Context context)
    {
        if (colors is not null)
        {
            // A list of colors is interpolated piecewise in the color space of the scale's interpolate (sRGB without one)
            var stops = new List<RgbColor>(colors.Count);
            foreach (var item in colors)
            {
                if (item.Node.AsString() is not { } text || !CssColor.TryParse(text, out var parsed))
                {
                    throw new VegaLiteSpecException($"'{item.Node.AsString()}' is not a color.", item.Location);
                }

                stops.Add(RgbColor.FromRgba32(parsed));
            }

            if (reverse)
            {
                stops.Reverse();
            }

            var (space, gamma) = ReadInterpolate(input.ScaleDef);
            return new ColorRamp(space, stops, gamma).At;
        }

        var range = Configure(scheme!, SchemeUse.Continuous, 0, reverse, input, context);
        return range.Interpolator!;
    }

    /// <summary>
    ///     Turns a scheme into a range (<see cref="ColorSchemes.Configure"/>), locating a failure at the scheme.
    /// </summary>
    /// <param name="choice">The scheme.</param>
    /// <param name="use">What the range is for.</param>
    /// <param name="domainSize">The size of the domain.</param>
    /// <param name="reverse">Whether the colors run backwards.</param>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The range.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the scheme is unknown.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the scheme would give more colors than the limit.</exception>
    private static SchemeRange Configure(SchemeChoice choice, SchemeUse use, int domainSize, bool reverse, CommonInputs input, Context context)
    {
        var scheme = LookUp(choice);
        try
        {
            return ColorSchemes.Configure(scheme, use, choice.Count, domainSize, choice.Extent, reverse, context.Limits.MaxScaleDomainSize, context.CancellationToken);
        }
        catch (VegaLiteRenderException ex) when (!ex.Location.IsKnown)
        {
            throw new VegaLiteRenderException(ex.Message, choice.Location.IsKnown ? choice.Location : input.Location);
        }
    }

    /// <summary>
    ///     Reads <c>scale.interpolate</c>: a space name, or an object with a <c>type</c> and a <c>gamma</c>.
    /// </summary>
    /// <remarks>
    ///     The parser has checked the names; <c>cubehelix</c> and <c>cubehelix-long</c> are known but not drawn (the feature catalog reports
    ///     them) and interpolate in sRGB.
    /// </remarks>
    /// <param name="scaleDef">The scale object, or null.</param>
    /// <returns>The space (sRGB by default) and the gamma (1 by default).</returns>
    private static (InterpolationSpace Space, double Gamma) ReadInterpolate(SpecNode? scaleDef)
    {
        var node = scaleDef?["interpolate"];
        var name = node?.IsString == true ? node.StringValue : node?["type"]?.AsString();
        var gamma = node?["gamma"]?.AsNumber() ?? 1;
        return ColorInterpolator.TryParseSpace(name, out var space) ? (space, gamma) : (InterpolationSpace.Rgb, gamma);
    }

    /// <summary>
    ///     Reads the domain of a quantile scale: the explicit domain, or every valid number of the field, in ascending order.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="domainNode">The explicit domain array, or null.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The numbers in ascending order (none is NaN).</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when an explicit domain has an entry that is not a number.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the values would exceed a limit.</exception>
    private static double[] QuantileDomain(CommonInputs input, SpecNode? domainNode, Context context)
    {
        if (domainNode is not null)
        {
            CheckSpecArray(domainNode, input, context, "domain");
            var listed = ReadNumericDomain(domainNode, context, 1);
            SortAscending(listed, context.CancellationToken);
            return listed;
        }

        var table = context.Data.Main;
        var field = input.FieldPath is null ? -1 : table.FindField(input.FieldPath);
        if (field < 0)
        {
            return input.Datum is not null && ToDataValue(input.Datum).TryGetQuantitative(out var datum) ? [datum] : [];
        }

        // The values are counted before they are read: the array and the sort buffer are both as long as the table
        var rows = table.RowCount;
        if ((long)rows * 2 > TableLayout.MaxCells)
        {
            throw new VegaLiteRenderException(
                $"The quantile scale of the {input.Channel} channel would hold {(long)rows * 2} values in memory, more than the limit of {TableLayout.MaxCells} values (TableLayout.MaxCells).",
                input.Location);
        }

        var meter = new WorkMeter(context.Data.Evaluation);
        var values = new double[rows];
        var count = 0;
        try
        {
            for (var row = 0; row < rows; row++)
            {
                if (row % CancellationInterval == 0)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                }

                if (table.GetValue(row, field).TryGetQuantitative(out var number))
                {
                    values[count++] = number;
                }
            }

            // Reading and sorting are charged in proportion to their size: the rows read, and the comparisons of a merge sort
            meter.Add(rows);
            meter.Add((long)count * (count < 2 ? 0 : (int)Math.Ceiling(Math.Log2(count))));
        }
        catch (ExpressionRuntimeException ex)
        {
            throw new VegaLiteRenderException(ex.Message, input.Location);
        }

        Array.Resize(ref values, count);
        SortAscending(values, context.CancellationToken);
        return values;
    }

    /// <summary>
    ///     Sorts numbers in ascending order with a bottom-up merge sort that polls the cancellation token.
    /// </summary>
    /// <param name="values">The numbers (none is NaN); sorted in place.</param>
    /// <param name="cancellationToken">The token that stops the sort.</param>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    private static void SortAscending(double[] values, CancellationToken cancellationToken)
    {
        var n = values.Length;
        if (n < 2)
        {
            return;
        }

        var source = values;
        var target = new double[n];
        var merges = 0;
        for (var width = 1; width < n; width = checked(width * 2))
        {
            for (var low = 0; low < n; low += 2 * width)
            {
                if (++merges % SortPollInterval == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var middle = Math.Min(low + width, n);
                var high = Math.Min(low + (2 * width), n);
                int left = low, right = middle, k = low;
                while (left < middle && right < high)
                {
                    target[k++] = source[right] < source[left] ? source[right++] : source[left++];
                }

                while (left < middle)
                {
                    target[k++] = source[left++];
                }

                while (right < high)
                {
                    target[k++] = source[right++];
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            (source, target) = (target, source);
        }

        if (!ReferenceEquals(source, values))
        {
            Array.Copy(source, values, n);
        }
    }
}
