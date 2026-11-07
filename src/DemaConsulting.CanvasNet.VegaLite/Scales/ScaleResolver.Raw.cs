// Portions derived from Vega and Vega-Lite (BSD-3-Clause): the raw domain of a scale (vega-encode Scale.js configureDomain, Vega-Lite compile/scale/domain.ts parseSelectionDomain); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <content>
///     The raw domain of a scale: <c>scale.domainRaw</c> and the domain that a parameter gives (a partial file of the scale resolver unit).
/// </content>
/// <remarks>
///     <para>
///         Vega-Lite turns a <c>scale.domain</c> of the form <c>{"param": name}</c>, a <c>bin.extent</c> of that form and the interval selections bound to the scales (<c>"bind": "scales"</c>) into the
///         <c>domainRaw</c> of the Vega scale: the value of the signal of the parameter (for a selection, <c>name["field"]</c>: the selected range of one of its fields). Vega's <c>configureDomain</c> takes a raw domain before anything else and returns: the raw array is the domain, and the zero rule, <c>domainMin</c>,
///         <c>domainMax</c>, <c>domainMid</c>, the pixel padding and <c>nice</c> are not applied. A <c>domainRaw</c> that the specification writes itself takes the same path and, as in
///         Vega-Lite's assembly (the scale properties come after the selection extent), wins over a domain given by a parameter. A parameter that holds nothing (no value, an empty selection) leaves the domain of the scale as it is.
///     </para>
///     <para>
///         Only the scales that Vega-Lite gives a domain through <c>domainRaw</c> that matters here take it: the continuous scales (linear, log, pow, sqrt, symlog, time, utc, and the
///         sequential scale of a color) and the band, point and ordinal scales; the discretizing scales keep their own domain. A raw domain that no scale can use (not an array, fewer than two
///         numbers for a continuous scale, a composite or non-finite entry) is reported as <c>vl-scale-domain-raw</c> and the scale keeps its own domain: Vega draws a scale
///         with such a domain with every position undefined. A <c>domain</c> of the form <c>{"param": ...}</c> also clips the marks of the view when it is the domain of the x or y scale
///         (Vega-Lite's <c>scaleClip</c>), whether or not the parameter gives a value.
///     </para>
/// </remarks>
internal static partial class ScaleResolver
{
    /// <summary>
    ///     Tells whether the scale of a channel has a continuous domain (Vega-Lite's <c>hasContinuousDomain</c> of the scale type): a linear, log, power, square-root, symlog, time or UTC
    ///     scale, a sequential scale or a discretizing scale. The selection model needs the answer before any scale exists: an interval selection stores a range over a continuous domain and a list of values
    ///     over a discrete one.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="definition">The field definition of the channel.</param>
    /// <param name="markType">The mark type of the view.</param>
    /// <param name="encoding">The encoding object that holds the channel, or null.</param>
    /// <returns><see langword="true"/> when the scale that the channel gets has a continuous domain.</returns>
    internal static bool HasContinuousDomain(string channel, SpecNode definition, string markType, SpecNode? encoding)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(definition);
        var type = ReadType(definition, channel, encoding);
        var binning = type == "quantitative" && definition["bin"] is { IsNull: false } bin && !(bin.IsBoolean && !bin.BoolValue) && bin.StringValue != "binned";
        var unitIsUtc = definition["timeUnit"] is { IsNull: false } unit && TimeUnitSpec.TryRead(unit, out var spec) && spec.Utc;
        var kind = ChooseKind(channel, type, definition["scale"]?["type"]?.AsString(), markType, binning, unitIsUtc, out _, out _, HasNestedOffset(channel, encoding, markType));
        return kind is ScaleKind.Linear or ScaleKind.Time or ScaleKind.Utc or ScaleKind.Sequential or ScaleKind.Diverging or ScaleKind.Quantize or ScaleKind.Quantile or ScaleKind.Threshold;
    }

    /// <summary>
    ///     A raw domain that a scale takes.
    /// </summary>
    /// <param name="Values">The entries, in order.</param>
    /// <param name="Numbers">The entries as numbers (a date as its milliseconds) for a continuous scale; null for a discrete scale.</param>
    private sealed record RawDomain(IReadOnlyList<DataValue> Values, double[]? Numbers);

    /// <summary>
    ///     The extent of a scale: the parameter that gives its domain.
    /// </summary>
    /// <param name="At">The node that names the parameter (the <c>domain</c>, the <c>extent</c> of the bin, or the declaration of the selection that binds the scale).</param>
    /// <param name="Value">The value of the parameter (the selected range of the field, or the variable parameter); null when it holds nothing.</param>
    private sealed record ScaleExtent(SpecNode At, DataValue Value);

    /// <summary>
    ///     Tells whether the scale of a channel has an extent from a parameter: a <c>scale.domain</c> of the form <c>{"param": ...}</c>, a <c>bin.extent</c> of that form, or an interval selection bound to
    ///     the scales of the view (Vega-Lite's <c>selectionExtent</c> of the scale component, which also makes the marks clip, <c>scaleClip</c>).
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns><see langword="true"/> when a parameter gives the extent, whether or not it holds a value.</returns>
    private static bool HasSelectionExtent(CommonInputs input, Context context) =>
        (context.View is { } view && context.Selections?.BoundExtentOf(view, input.Channel) is not null)
        || HasParameterDomain(input.ScaleDef)
        || BinExtentOf(input) is not null;

    /// <summary>
    ///     Gets the <c>extent</c> of the bin of a channel when it is a parameter reference.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <returns>The <c>extent</c> object (<c>{"param": ...}</c>); null when the channel is not binned by an extent that a parameter gives.</returns>
    private static SpecNode? BinExtentOf(CommonInputs input) =>
        ChannelDefinitions.FieldDefinitionOf(input.Definition)["bin"] is { IsObject: true } bin && bin["extent"] is { IsObject: true } extent && extent["param"] is { IsString: true } ? extent : null;

    /// <summary>
    ///     Finds the parameter that gives the extent of a scale and reads its value (Vega-Lite's <c>selectionExtent</c> of the scale component).
    /// </summary>
    /// <remarks>
    ///     The extent comes from the <c>extent</c> of the bin, which the <c>domain</c> of the scale overrides, which a selection bound to the scales overrides (the selections are parsed last and
    ///     set the extent explicitly). A selection gives the selected values of a field; a variable parameter is the signal itself. A selection bound to the scales of a view that is not the
    ///     whole view holds nothing in a static chart (see <see cref="SelectionModel"/>), so its scales keep their domain.
    /// </remarks>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The extent; null when no parameter gives one.</returns>
    private static ScaleExtent? FindExtent(CommonInputs input, Context context)
    {
        if (context.View is { } view && context.Selections?.BoundExtentOf(view, input.Channel) is { } bound)
        {
            var read = context.Selections.ResolveExtent(view, bound.Component.Name, bound.Field, null, bound.Component.Declaration.GetLocation(context.SourceName), null);
            return new ScaleExtent(bound.Component.Declaration, read.Value);
        }

        SpecNode? node = null;
        var reportsProjection = false;
        if (HasParameterDomain(input.ScaleDef))
        {
            node = input.ScaleDef!["domain"];
            reportsProjection = true;
        }
        else if (BinExtentOf(input) is { } extent)
        {
            // The data stage reports the projection of a bin extent, once
            node = extent;
        }

        if (node is null)
        {
            return null;
        }

        var param = node["param"]!.StringValue!;
        if (context.Selections is { } selections)
        {
            var read = selections.ResolveExtent(
                context.View,
                param,
                node["field"]?.StringValue,
                node["encoding"]?.StringValue,
                node.GetLocation(context.SourceName),
                reportsProjection ? context.Warnings : null);
            if (read.IsSelection)
            {
                return new ScaleExtent(node, read.Value);
            }
        }

        return new ScaleExtent(node, context.Parameters is not null && context.Parameters.TryGetValue(param, out var value) ? value : default);
    }

    /// <summary>
    ///     Tells whether a scale definition has a <c>domain</c> that is a parameter reference (<c>{"param": ...}</c>).
    /// </summary>
    /// <param name="scaleDef">The scale object, or null.</param>
    /// <returns><see langword="true"/> when the domain is an object with a string <c>param</c>.</returns>
    private static bool HasParameterDomain(SpecNode? scaleDef) =>
        scaleDef?["domain"] is { IsObject: true } extent && extent["param"] is { IsString: true };

    /// <summary>
    ///     Reads the raw domain of a scale.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts (the parameter values and the sink of the findings).</param>
    /// <param name="continuous">Whether the scale is continuous (numbers or dates, at least two) or discrete (any scalars, at least one).</param>
    /// <returns>The raw domain; null when the scale has none, or when the one it names cannot be used (which is reported).</returns>
    private static RawDomain? ReadRawDomain(CommonInputs input, Context context, bool continuous)
    {
        var scaleDef = input.ScaleDef;

        // The domainRaw of the specification wins; otherwise the value of the parameter that gives the extent
        IReadOnlyList<DataValue>? values;
        SpecNode at;
        if (scaleDef?["domainRaw"] is { IsNull: false } literal)
        {
            at = literal;
            values = literal.IsArray ? [.. literal.Items.Select(SpecValues.ToDataValue)] : null;
        }
        else if (FindExtent(input, context) is { } extent)
        {
            // A parameter that holds nothing (no value, an empty selection) leaves the domain of the scale as it is
            if (extent.Value.IsNull)
            {
                return null;
            }

            at = extent.At;
            values = extent.Value.IsArray ? extent.Value.Elements.ToArray() : null;
        }
        else
        {
            return null;
        }

        if (values is not null && Usable(values, continuous) is { } usable)
        {
            return usable;
        }

        // Vega would draw nothing: the scale keeps its own domain, and the author is told
        var location = at.GetLocation(context.SourceName);
        const string id = "vl-scale-domain-raw";
        if (!context.Warnings.Any(w => w.Id == id && w.Location == location))
        {
            var needs = continuous ? "an array of at least two numbers or dates" : "an array of at least one value";
            context.Warnings.Add(new FeatureReport(
                id,
                $"The raw domain of the {input.Channel} scale must be {needs}; the scale keeps its own domain.",
                location,
                FeatureReportKind.Warning));
        }

        return null;
    }

    /// <summary>
    ///     Checks the entries of a raw domain.
    /// </summary>
    /// <param name="values">The entries.</param>
    /// <param name="continuous">Whether the scale is continuous.</param>
    /// <returns>The domain; null when an entry cannot be used or there are too few.</returns>
    private static RawDomain? Usable(IReadOnlyList<DataValue> values, bool continuous)
    {
        if (!continuous)
        {
            // A discrete domain is a list of scalar categories; a date is the instant it names
            return values.Count >= 1 && values.All(v => v.Kind is DataValueKind.Null or DataValueKind.Boolean or DataValueKind.Number or DataValueKind.String or DataValueKind.Date)
                ? new RawDomain(values, null)
                : null;
        }

        if (values.Count < 2)
        {
            return null;
        }

        var numbers = new double[values.Count];
        for (var i = 0; i < numbers.Length; i++)
        {
            var value = values[i];
            if (value.IsNumber && double.IsFinite(value.Number))
            {
                numbers[i] = value.Number;
            }
            else if (value.IsDate)
            {
                numbers[i] = value.Milliseconds;
            }
            else
            {
                return null;
            }
        }

        return new RawDomain(values, numbers);
    }
}
